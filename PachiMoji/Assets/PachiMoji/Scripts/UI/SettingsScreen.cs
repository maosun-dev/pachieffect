using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PachiMoji
{
    /// <summary>
    /// 設定画面：火花・プチュンの光の色と大きさ・書体・昇格の範囲・最初の文字色と背景。黒い全面が下からせり上がる。
    /// 項目は縦に動かせる（見出しと「もどる」は動かない）。
    /// 選んでいるものは金の文字と細い金の線。変えたらすぐ onChanged を呼ぶ（保存は PachiApp）。
    /// いちばん下に買い物（印を消す・広告を消す・セット、購入を復元）。値段と買ったものは ShowShop で描き直す。
    /// </summary>
    public class SettingsScreen
    {
        public Button Back;
        RectTransform panel; CanvasGroup group;
        EffectSettings settings;
        Action<EffectSettings> onChanged;
        RectTransform rail; RectTransform[] medals; TMP_Text rangeLabel;
        ScrollRect scroll; CanvasGroup firstGroup; TMP_Text firstNote;
        RectTransform content, shopBox; float shopTop; TMP_Text shopNotice;
        readonly List<GameObject> shopRows = new List<GameObject>();

        /// <summary>商品の板を押したとき（商品 ID）</summary>
        public Action<string> Buy;
        public Button Restore;

        // 商品の名前と説明（どれも買い切り）
        static readonly Dictionary<string, (string name, string note)> Products = new Dictionary<string, (string, string)>
        {
            [ProductIds.NoMark] = ("印を消す", "動画の右下の「脳汁メーカー」を消します"),
            [ProductIds.NoAds] = ("広告を消す", "広告を消し、ネットなしでも書き出せます"),
            [ProductIds.Set] = ("セット", "印と広告を、どちらも消します"),
        };
        static readonly string[] TierNames = { "銀", "金", "虹" };

        public static SettingsScreen Build(RectTransform canvasRoot, Rect safePx, Vector2 screenPx,
            EffectSettings settings, Action<EffectSettings> onChanged)
        {
            var m = new SettingsScreen { settings = settings, onChanged = onChanged };
            m.panel = MainScreen.Rect(canvasRoot, "Settings");
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
            MainScreen.Label(title, "設定", UiTheme.Display, 26, UiTheme.Chrome);

            // 見出しより下を、縦に動かせる範囲にする
            var view = MainScreen.Rect(safe, "Scroll");
            view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one;
            view.offsetMin = Vector2.zero; view.offsetMax = new Vector2(0, -64);
            view.gameObject.AddComponent<Image>().color = Color.clear;   // 余白をなぞっても動かせるように
            view.gameObject.AddComponent<RectMask2D>();
            var content = m.content = MainScreen.Rect(view, "Content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            m.scroll = view.gameObject.AddComponent<ScrollRect>();
            m.scroll.viewport = view;
            m.scroll.content = content;
            m.scroll.horizontal = false;
            m.scroll.movementType = ScrollRect.MovementType.Elastic;
            m.scroll.scrollSensitivity = 20;

            float y = -28;
            var parent = content;
            m.Choice(parent, ref y, "火花", new[] { "あり", "なし" }, null, settings.SparksOn ? 0 : 1, 150,
                i => settings.SparksOn = i == 0);
            m.Choice(parent, ref y, "プチュンの光の色", new[] { "青白", "金", "赤", "虹" },
                new[] { UiTheme.Hex(0x8CBFFF), UiTheme.Hex(0xFFC74D), UiTheme.Hex(0xFF4033), Color.clear },
                (int)settings.Light, 80, i => settings.Light = (LightColor)i);
            m.Choice(parent, ref y, "プチュンの光の大きさ", new[] { "小", "中", "大" }, null, (int)settings.Size, 104,
                i => settings.Size = (LightSize)i);
            m.Choice(parent, ref y, "書体", new[] { "ポップ", "明朝" }, null, (int)settings.Font, 150,
                i => settings.Font = (TextFont)i);
            m.Range(parent, ref y);
            m.First(parent, ref y);
            m.ShopSection(parent, y);

            MainScreen.SetLayer(m.panel.gameObject, MainScreen.UiLayer);
            m.Show(false);
            return m;
        }

        /// <summary>バナー広告を出すあいだは、縦に動かす範囲の下をバナーのぶん空ける（最後の項目に重ならないように）</summary>
        public void SetBannerSpace(bool on)
        {
            var view = (RectTransform)scroll.transform;
            view.offsetMin = new Vector2(0, on ? UiTheme.BannerHeight : 0);
        }

        /// <summary>いま見せている商品の数</summary>
        public int ShopItemCount { get; private set; }

        /// <summary>縦の位置（1＝いちばん上、0＝いちばん下）</summary>
        public void ScrollTo(float normalized)
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = normalized;
        }

        /// <summary>中身が見える範囲より長く、縦に動かせるか</summary>
        public bool Scrollable => scroll.content.rect.height > scroll.viewport.rect.height;

        /// <summary>最初の色を選べるか（昇格のはじまりが銀のときだけ）</summary>
        public bool FirstColorsEnabled => firstGroup.interactable;

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

        // 項目1つ：見出し（灰）と、銀の板の選択肢を横に並べる
        void Choice(RectTransform parent, ref float y, string label, string[] options, Color[] dots, int selected,
            float width, Action<int> pick)
        {
            var head = MainScreen.Rect(parent, label);
            MainScreen.Place(head, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(340, 22));
            var ht = MainScreen.Label(head, label, UiTheme.Body, 13, UiTheme.Muted);
            ht.alignment = TextAlignmentOptions.Left;
            y -= 40;

            var texts = new List<TMP_Text>();
            var lines = new List<Image>();
            float gap = 8, total = options.Length * width + (options.Length - 1) * gap;
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                float x = -total / 2 + width / 2 + i * (width + gap);
                var b = MainScreen.PlateButton(parent, options[i], new Vector2(0.5f, 1), new Vector2(x, y), new Vector2(width, 48),
                    UiTheme.Display, 18, UiTheme.Chrome);
                var text = b.GetComponentInChildren<TMP_Text>();
                if (dots != null)
                {
                    // 光の色の見本：文字の左に小さな光の点（虹は色相が回る点）
                    var dot = MainScreen.Rect(b.transform, "Dot");
                    MainScreen.Place(dot, new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(18, 18));
                    var di = dot.gameObject.AddComponent<Image>();
                    di.sprite = dots[i].a > 0 ? UiArt.Dot(dots[i]) : UiArt.RainbowDot();
                    di.raycastTarget = false;
                    text.rectTransform.offsetMin = new Vector2(18, 0);
                }
                var line = MainScreen.Rect(b.transform, "On");
                MainScreen.Place(line, new Vector2(0.5f, 0), new Vector2(0, 9), new Vector2(width * 0.5f, 2));
                var li = line.gameObject.AddComponent<Image>();
                li.color = UiTheme.Gold;
                li.raycastTarget = false;
                texts.Add(text); lines.Add(li);
                b.onClick.AddListener(() =>
                {
                    pick(index);
                    Mark(texts, lines, index);
                    onChanged(settings);
                });
            }
            Mark(texts, lines, selected);
            y -= 52;
        }

        static void Mark(List<TMP_Text> texts, List<Image> lines, int selected)
        {
            for (int i = 0; i < texts.Count; i++)
            {
                texts[i].color = i == selected ? UiTheme.Gold : UiTheme.Chrome;
                lines[i].enabled = i == selected;
            }
        }

        // 昇格の範囲：銀・金・虹のメダルを並べ、指でなぞった端から端までを範囲にする
        void Range(RectTransform parent, ref float y)
        {
            var head = MainScreen.Rect(parent, "RangeHead");
            MainScreen.Place(head, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(340, 22));
            MainScreen.Label(head, "昇格の範囲", UiTheme.Body, 13, UiTheme.Muted).alignment = TextAlignmentOptions.Left;
            y -= 56;

            var row = MainScreen.Rect(parent, "Medals");
            MainScreen.Place(row, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(320, 84));
            row.gameObject.AddComponent<Image>().color = Color.clear;   // なぞれる範囲
            rail = MainScreen.Rect(row, "Rail");
            var ri = rail.gameObject.AddComponent<Image>();
            ri.sprite = UiArt.Rail(); ri.type = Image.Type.Sliced; ri.raycastTarget = false;
            medals = new RectTransform[3];
            for (int i = 0; i < 3; i++)
            {
                medals[i] = MainScreen.Rect(row, "Medal" + i);
                MainScreen.Place(medals[i], new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 110, 0), new Vector2(72, 72));
                var mi = medals[i].gameObject.AddComponent<Image>();
                mi.sprite = UiArt.Medal(RangePicker.Medals[i]); mi.raycastTarget = false;
            }
            var drag = row.gameObject.AddComponent<MedalDrag>();
            drag.IndexAt = local => Mathf.Clamp(Mathf.RoundToInt(local.x / 110f) + 1, 0, 2);
            drag.Preview = (a, b) => ShowRange(RangePicker.FromDrag(a, b));
            drag.Commit = (a, b) =>
            {
                (settings.From, settings.To) = RangePicker.FromDrag(a, b);
                ShowRange((settings.From, settings.To));
                ShowFirstEnabled();
                onChanged(settings);
            };
            y -= 60;

            var label = MainScreen.Rect(parent, "RangeLabel");
            MainScreen.Place(label, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(340, 30));
            rangeLabel = MainScreen.Label(label, "", UiTheme.Display, 20, UiTheme.Chrome);
            y -= 30;
            var help = MainScreen.Rect(parent, "RangeHelp");
            MainScreen.Place(help, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(340, 24));
            MainScreen.Label(help, "メダルを指でなぞって、はじまりとおわりの色を選びます", UiTheme.Body, 12, UiTheme.Muted);
            ShowRange((settings.From, settings.To));
            y -= 48;
        }

        // 最初の文字色と背景：銀の段（ドンで着地してから金に変わるまで）の色。はじまりが銀のときだけ選べる
        void First(RectTransform parent, ref float y)
        {
            var box = MainScreen.Rect(parent, "First");
            box.anchorMin = new Vector2(0, 1); box.anchorMax = new Vector2(1, 1);
            box.pivot = new Vector2(0.5f, 1);
            box.anchoredPosition = new Vector2(0, y);
            firstGroup = box.gameObject.AddComponent<CanvasGroup>();
            float by = 0;
            Choice(box, ref by, "最初の文字色", new[] { "銀", "青", "緑", "赤", "紫" },
                new[] { UiTheme.Chrome, Hex(FirstColors.TextBase(FirstText.Blue)), Hex(FirstColors.TextBase(FirstText.Green)),
                        Hex(FirstColors.TextBase(FirstText.Red)), Hex(FirstColors.TextBase(FirstText.Purple)) },
                (int)settings.FirstText, 62, i => settings.FirstText = (FirstText)i);
            Choice(box, ref by, "最初の背景", new[] { "青", "緑", "赤", "紫" },
                new[] { Hex(FirstColors.TextBase(FirstText.Blue)), Hex(FirstColors.TextBase(FirstText.Green)),
                        Hex(FirstColors.TextBase(FirstText.Red)), Hex(FirstColors.TextBase(FirstText.Purple)) },
                (int)settings.FirstBack, 80, i => settings.FirstBack = (FirstBack)i);
            box.sizeDelta = new Vector2(0, -by);
            y += by;

            var note = MainScreen.Rect(parent, "FirstNote");
            MainScreen.Place(note, new Vector2(0.5f, 1), new Vector2(0, y + 8), new Vector2(340, 24));
            firstNote = MainScreen.Label(note, "", UiTheme.Body, 12, UiTheme.Muted);
            y -= 24;
            ShowFirstEnabled();
        }

        // はじまりが銀でないときは、最初の色を薄くして押せなくし、わけを出す
        void ShowFirstEnabled()
        {
            bool on = settings.FirstColorsApply;
            firstGroup.alpha = on ? 1 : 0.3f;
            firstGroup.interactable = firstGroup.blocksRaycasts = on;
            firstNote.text = on ? "ドンと着地してから、金に変わるまでの色です" : "昇格のはじまりが銀のときに効きます";
        }

        // 買い物：見出し・商品の板（作り直す）・買ったもの・購入を復元・知らせ
        void ShopSection(RectTransform parent, float y)
        {
            var head = MainScreen.Rect(parent, "ShopHead");
            MainScreen.Place(head, new Vector2(0.5f, 1), new Vector2(0, y - 8), new Vector2(340, 22));
            MainScreen.Label(head, "買い物", UiTheme.Body, 13, UiTheme.Muted).alignment = TextAlignmentOptions.Left;
            shopBox = MainScreen.Rect(parent, "Shop");
            shopBox.anchorMin = new Vector2(0, 1); shopBox.anchorMax = new Vector2(1, 1);
            shopBox.pivot = new Vector2(0.5f, 1);
            shopTop = y - 36;
            Restore = MainScreen.PlateButton(shopBox, "購入を復元", new Vector2(0.5f, 1), Vector2.zero, new Vector2(150, 40), UiTheme.Body, 14, UiTheme.Chrome);
            var notice = MainScreen.Rect(shopBox, "ShopNotice");
            notice.anchorMin = notice.anchorMax = new Vector2(0.5f, 1);
            notice.pivot = new Vector2(0.5f, 1);
            notice.sizeDelta = new Vector2(340, 36);
            shopNotice = MainScreen.Label(notice, "", UiTheme.Body, 12, UiTheme.Muted);
            shopNotice.textWrappingMode = TextWrappingModes.Normal;
            ShowShop(new Entitlements(new string[0]), ShopState.Connecting, _ => null);
        }

        /// <summary>
        /// 買い物を今の状態で描き直す。見せる商品は Entitlements.ShopItems（セットは何も買っていない人だけ）。
        /// 値段がまだ無い・ストアにつながらないときは「—」で押せない。買ったものは金の字で並べる
        /// </summary>
        public void ShowShop(Entitlements owned, ShopState state, Func<string, string> priceOf)
        {
            foreach (var go in shopRows)
                if (Application.isPlaying) UnityEngine.Object.Destroy(go); else UnityEngine.Object.DestroyImmediate(go);
            shopRows.Clear();
            float y = 0;
            var items = owned.ShopItems;
            ShopItemCount = items.Length;
            foreach (var id in items)
            {
                string price = state == ShopState.Ready ? priceOf(id) : null;
                shopRows.Add(ShopPlate(id, price, ref y));
            }

            var bought = new List<string>();
            if (owned.Owns(ProductIds.NoMark)) bought.Add(Products[ProductIds.NoMark].name);
            if (owned.Owns(ProductIds.NoAds)) bought.Add(Products[ProductIds.NoAds].name);
            if (bought.Count > 0)
            {
                var line = MainScreen.Rect(shopBox, "Bought");
                MainScreen.Place(line, new Vector2(0.5f, 1), new Vector2(0, y - 14), new Vector2(340, 28));
                var t = MainScreen.Label(line, "買ったもの：" + string.Join("、", bought), UiTheme.Display, 16, UiTheme.Gold);
                t.alignment = TextAlignmentOptions.Left;
                shopRows.Add(line.gameObject);
                y -= 36;
            }

            y -= 8;
            var restore = (RectTransform)Restore.transform;
            restore.anchoredPosition = new Vector2(0, y - 20);
            Restore.interactable = state != ShopState.Connecting;
            y -= 48;
            ((RectTransform)shopNotice.transform.parent).anchoredPosition = new Vector2(0, y);
            if (state == ShopState.Unavailable) shopNotice.text = "いまは買えません。ネットにつないで、開き直してください";
            else if (shopNotice.text == "いまは買えません。ネットにつないで、開き直してください") shopNotice.text = "";
            y -= 44;

            shopBox.anchoredPosition = new Vector2(0, shopTop);
            shopBox.sizeDelta = new Vector2(0, -y);
            content.sizeDelta = new Vector2(0, -(shopTop + y) + 24);
            MainScreen.SetLayer(shopBox.gameObject, MainScreen.UiLayer);
        }

        /// <summary>買い物の知らせ（購入しました・承認待ち・復元の結果など）</summary>
        public void ShowShopNotice(string text) => shopNotice.text = text;

        // 商品1つ：銀の板に名前（左）・値段（右）・説明（灰）。押すと Buy
        GameObject ShopPlate(string id, string price, ref float y)
        {
            var (name, note) = Products[id];
            var b = MainScreen.PlateButton(shopBox, name, new Vector2(0.5f, 1), new Vector2(0, y - 36), new Vector2(340, 68),
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

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        // 金の帯を、はじまりのメダルからおわりのメダルまで伸ばし、メダルの明るさで範囲を見せる
        void ShowRange((Tier from, Tier to) r)
        {
            int from = System.Array.IndexOf(RangePicker.Medals, r.from), to = System.Array.IndexOf(RangePicker.Medals, r.to);
            float x0 = (from - 1) * 110, x1 = (to - 1) * 110;
            MainScreen.Place(rail, new Vector2(0.5f, 0.5f), new Vector2((x0 + x1) / 2, 0), new Vector2(x1 - x0 + 40, 22));
            for (int i = 0; i < 3; i++)
            {
                bool inside = i >= from && i <= to;
                medals[i].GetComponent<Image>().color = inside ? Color.white : new Color(0.35f, 0.35f, 0.35f, 1);
            }
            rangeLabel.text = r.from == r.to ? TierNames[from] + "だけ" : TierNames[from] + " → " + TierNames[to];
        }
    }

    /// <summary>メダルの列をなぞる：指を置いたメダルから、いま指があるメダルまで</summary>
    public class MedalDrag : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Func<Vector2, int> IndexAt;
        public Action<int, int> Preview, Commit;
        int down = -1;

        int At(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out var local);
            return IndexAt(local);
        }

        public void OnPointerDown(PointerEventData e) { down = At(e); Preview?.Invoke(down, down); }
        public void OnDrag(PointerEventData e) { if (down >= 0) Preview?.Invoke(down, At(e)); }

        public void OnPointerUp(PointerEventData e)
        {
            if (down < 0) return;
            Commit?.Invoke(down, At(e));
            down = -1;
        }
    }
}
