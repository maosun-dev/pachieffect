using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PachiMoji
{
    /// <summary>
    /// 買い物の画面：印を消す・広告を消す・セット、購入を復元。メイン画面の右上の投入口から開く。黒い全面が下からせり上がる。
    /// 見せる商品は Entitlements.ShopItems（セットは何も買っていない人だけ）。値段と買ったものは ShowShop で描き直す。
    /// </summary>
    public class ShopScreen : ICover
    {
        public Button Back;
        RectTransform panel, box; CanvasGroup group;
        TMP_Text notice;
        readonly List<GameObject> rows = new List<GameObject>();

        /// <summary>商品の板を押したとき（商品 ID）</summary>
        public Action<string> Buy;
        public Button Restore;

        /// <summary>いま見せている商品の数</summary>
        public int ItemCount { get; private set; }

        const string Unavailable = "いまは買えません。ネットにつないで、開き直してください";

        // 商品の名前と説明（どれも買い切り）
        static readonly Dictionary<string, (string name, string note)> Products = new Dictionary<string, (string, string)>
        {
            [ProductIds.NoMark] = ("印を消す", "動画の右下の「脳汁メーカー」を消します"),
            [ProductIds.NoAds] = ("広告を消す", "広告を消し、ネットなしでも書き出せます"),
            [ProductIds.Set] = ("セット", "印と広告を、どちらも消します"),
        };

        public static ShopScreen Build(RectTransform canvasRoot, Rect safePx, Vector2 screenPx)
        {
            var m = new ShopScreen();
            m.panel = MainScreen.Rect(canvasRoot, "Shop");
            m.panel.anchorMin = Vector2.zero; m.panel.anchorMax = Vector2.one;
            m.panel.offsetMin = m.panel.offsetMax = Vector2.zero;
            m.panel.gameObject.AddComponent<Image>().color = UiTheme.Black;   // 下の画面を隠す・押させない
            m.group = m.panel.gameObject.AddComponent<CanvasGroup>();

            var safe = MainScreen.Rect(m.panel, "Safe");
            safe.anchorMin = new Vector2(safePx.xMin / screenPx.x, safePx.yMin / screenPx.y);
            safe.anchorMax = new Vector2(safePx.xMax / screenPx.x, safePx.yMax / screenPx.y);
            safe.offsetMin = safe.offsetMax = Vector2.zero;

            var top = new Vector2(0.5f, 1);
            m.Back = MainScreen.PlateButton(safe, "もどる", new Vector2(0, 1), new Vector2(56, -32), new Vector2(96, 44), UiTheme.Body, 15, UiTheme.Chrome);
            var title = MainScreen.Rect(safe, "Title");
            MainScreen.Place(title, top, new Vector2(0, -32), new Vector2(200, 44));
            MainScreen.Label(title, "買い物", UiTheme.Display, 26, UiTheme.Chrome);
            var lead = MainScreen.Rect(safe, "Lead");
            MainScreen.Place(lead, top, new Vector2(0, -84), new Vector2(340, 22));
            MainScreen.Label(lead, "どれも買い切りです。一度買えば、ずっと使えます", UiTheme.Body, 12, UiTheme.Muted);

            m.box = MainScreen.Rect(safe, "Items");
            m.box.anchorMin = new Vector2(0, 1); m.box.anchorMax = new Vector2(1, 1);
            m.box.pivot = new Vector2(0.5f, 1);
            m.box.anchoredPosition = new Vector2(0, -112);
            m.Restore = MainScreen.PlateButton(m.box, "購入を復元", top, Vector2.zero, new Vector2(150, 40), UiTheme.Body, 14, UiTheme.Chrome);
            var n = MainScreen.Rect(m.box, "Notice");
            n.anchorMin = n.anchorMax = top;
            n.pivot = top;
            n.sizeDelta = new Vector2(340, 36);
            m.notice = MainScreen.Label(n, "", UiTheme.Body, 12, UiTheme.Muted);
            m.notice.textWrappingMode = TextWrappingModes.Normal;
            m.ShowShop(new Entitlements(new string[0]), ShopState.Connecting, _ => null);

            MainScreen.SetLayer(m.panel.gameObject, MainScreen.UiLayer);
            m.Show(false);
            return m;
        }

        public void Show(bool on)
        {
            group.alpha = on ? 1 : 0;
            group.interactable = group.blocksRaycasts = on;
        }

        /// <summary>せり上がり具合（0＝全部見えている、1＝下に隠れている）</summary>
        public void SetSlide(float k)
        {
            float h = ((RectTransform)panel.parent).rect.height;
            panel.offsetMin = new Vector2(0, -k * h);
            panel.offsetMax = new Vector2(0, -k * h);
        }

        /// <summary>
        /// 今の状態で描き直す。値段がまだ無い・ストアにつながらないときは「—」で押せない。買ったものは金の字で並べる
        /// </summary>
        public void ShowShop(Entitlements owned, ShopState state, Func<string, string> priceOf)
        {
            foreach (var go in rows)
                if (Application.isPlaying) UnityEngine.Object.Destroy(go); else UnityEngine.Object.DestroyImmediate(go);
            rows.Clear();
            float y = 0;
            var items = owned.ShopItems;
            ItemCount = items.Length;
            foreach (var id in items)
                rows.Add(Plate(id, state == ShopState.Ready ? priceOf(id) : null, ref y));

            var bought = new List<string>();
            if (owned.Owns(ProductIds.NoMark)) bought.Add(Products[ProductIds.NoMark].name);
            if (owned.Owns(ProductIds.NoAds)) bought.Add(Products[ProductIds.NoAds].name);
            if (bought.Count > 0)
            {
                var line = MainScreen.Rect(box, "Bought");
                MainScreen.Place(line, new Vector2(0.5f, 1), new Vector2(0, y - 14), new Vector2(340, 28));
                var t = MainScreen.Label(line, "買ったもの：" + string.Join("、", bought), UiTheme.Display, 16, UiTheme.Gold);
                t.alignment = TextAlignmentOptions.Left;
                rows.Add(line.gameObject);
                y -= 36;
            }

            y -= 8;
            ((RectTransform)Restore.transform).anchoredPosition = new Vector2(0, y - 20);
            Restore.interactable = state != ShopState.Connecting;
            y -= 48;
            ((RectTransform)notice.transform.parent).anchoredPosition = new Vector2(0, y);
            if (state == ShopState.Unavailable) notice.text = Unavailable;
            else if (notice.text == Unavailable) notice.text = "";
            box.sizeDelta = new Vector2(0, -y + 44);
            MainScreen.SetLayer(box.gameObject, MainScreen.UiLayer);
        }

        /// <summary>買い物の知らせ（購入しました・承認待ち・復元の結果など）</summary>
        public void ShowNotice(string text) => notice.text = text;

        // 商品1つ：銀の板に名前（左）・値段（右）・説明（灰）。押すと Buy
        GameObject Plate(string id, string price, ref float y)
        {
            var (name, note) = Products[id];
            var b = MainScreen.PlateButton(box, name, new Vector2(0.5f, 1), new Vector2(0, y - 36), new Vector2(340, 68),
                UiTheme.Display, 19, UiTheme.Chrome);
            var title = b.GetComponentInChildren<TMP_Text>();
            title.alignment = TextAlignmentOptions.TopLeft;
            title.rectTransform.offsetMin = new Vector2(18, 0);
            title.rectTransform.offsetMax = new Vector2(-90, -10);
            var desc = MainScreen.Label((RectTransform)b.transform, note, UiTheme.Body, 11, UiTheme.Muted);
            desc.alignment = TextAlignmentOptions.BottomLeft;
            desc.enableAutoSizing = true; desc.fontSizeMin = 9; desc.fontSizeMax = 11;   // 1行に収める（名前と重ならないように）
            desc.rectTransform.offsetMin = new Vector2(18, 11);
            desc.rectTransform.offsetMax = new Vector2(-90, 0);
            var cost = MainScreen.Label((RectTransform)b.transform, price ?? "—", UiTheme.Display, 22, price != null ? UiTheme.Chrome : UiTheme.Muted);
            cost.alignment = TextAlignmentOptions.Right;
            cost.rectTransform.offsetMax = new Vector2(-18, 0);
            b.interactable = price != null;
            b.onClick.AddListener(() => Buy?.Invoke(id));
            y -= 76;
            return b.gameObject;
        }
    }

    /// <summary>メイン画面を覆って下からせり上がる画面（設定・買い物）</summary>
    public interface ICover
    {
        void Show(bool on);
        /// <summary>0＝全部見えている、1＝下に隠れている</summary>
        void SetSlide(float k);
    }
}
