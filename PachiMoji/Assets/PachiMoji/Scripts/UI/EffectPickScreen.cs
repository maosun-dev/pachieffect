using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PachiMoji
{
    /// <summary>
    /// 演出を選ぶ画面。メイン画面の左上の「演出」ボタンから開く。黒い全面が下からせり上がる。
    /// 演出ごとに、台の液晶のように銀の枠に入れた止め絵（Resources/UI/EffectPreview/ID）と名前・ひとこと。
    /// 今の演出は名前を金にして下に金の線（設定の画面の「選んでいる」と同じ印）
    /// </summary>
    public class EffectPickScreen : ICover
    {
        public Button Back;
        public event Action<string> Picked;
        RectTransform panel; CanvasGroup group;
        readonly List<(string id, TMP_Text name, Image bar)> cards = new List<(string, TMP_Text, Image)>();

        // 演出のひとこと（一覧に無い演出は名前だけ）
        static readonly Dictionary<string, string> Notes = new Dictionary<string, string>
        {
            ["oudou"] = "言葉が銀から金、\n虹へと昇格する",
            ["obi"] = "言葉の金の帯が回り続ける。\n写真も入れられる",
        };

        public static EffectPickScreen Build(RectTransform canvasRoot, Rect safePx, Vector2 screenPx)
        {
            var m = new EffectPickScreen();
            m.panel = MainScreen.Rect(canvasRoot, "EffectPick");
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
            MainScreen.Label(title, "演出", UiTheme.Display, 26, UiTheme.Chrome);

            // 演出の札：2つずつ横に並べる（幅 390 の基準で 1 枚 168）
            const float cardW = 168, gap = 14, imgH = cardW * 16f / 9f;
            int i = 0;
            foreach (var e in EffectCatalog.All)
            {
                float x = (i % 2 == 0 ? -1 : 1) * (cardW + gap) / 2;
                float y = -96 - (i / 2) * (imgH + 120);
                m.cards.Add(m.Card(safe, e, new Vector2(x, y), cardW, imgH));
                i++;
            }

            MainScreen.SetLayer(m.panel.gameObject, MainScreen.UiLayer);
            m.Show(false);
            return m;
        }

        // 札1枚：銀の枠の中に止め絵、下に名前とひとこと。札ぜんたいが押せる
        (string, TMP_Text, Image) Card(RectTransform parent, EffectEntry e, Vector2 pos, float w, float imgH)
        {
            var rt = MainScreen.Rect(parent, "Card " + e.Id);
            MainScreen.Place(rt, new Vector2(0.5f, 1), pos + new Vector2(0, -(imgH + 110) / 2), new Vector2(w, imgH + 110));
            rt.gameObject.AddComponent<Image>().color = Color.clear;   // 押せる範囲
            var b = rt.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            rt.gameObject.AddComponent<PressSink>();
            string id = e.Id;
            b.onClick.AddListener(() => Picked?.Invoke(id));

            // 台の液晶のような銀の枠
            var bezel = MainScreen.Rect(rt, "Bezel");
            bezel.anchorMin = new Vector2(0, 1); bezel.anchorMax = new Vector2(1, 1);
            bezel.pivot = new Vector2(0.5f, 1);
            bezel.sizeDelta = new Vector2(0, imgH);
            bezel.anchoredPosition = Vector2.zero;
            var frame = bezel.gameObject.AddComponent<Image>();
            frame.sprite = UiArt.ChromePlate();
            frame.type = Image.Type.Sliced;
            frame.raycastTarget = false;
            var shot = MainScreen.Rect(bezel, "Shot");
            shot.anchorMin = Vector2.zero; shot.anchorMax = Vector2.one;
            shot.offsetMin = new Vector2(7, 7); shot.offsetMax = new Vector2(-7, -7);
            var raw = shot.gameObject.AddComponent<RawImage>();
            raw.texture = Resources.Load<Texture2D>("UI/EffectPreview/" + e.Id);
            raw.color = raw.texture != null ? Color.white : UiTheme.Black;
            raw.raycastTarget = false;
            var fit = shot.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = raw.texture != null ? (float)raw.texture.width / raw.texture.height : 9f / 16f;
            shot.gameObject.AddComponent<RectMask2D>();
            bezel.gameObject.AddComponent<RectMask2D>().padding = new Vector4(7, 7, 7, 7);

            var nameRt = MainScreen.Rect(rt, "Name");
            MainScreen.Place(nameRt, new Vector2(0.5f, 1), new Vector2(0, -imgH - 24), new Vector2(w, 30));
            var name = MainScreen.Label(nameRt, e.Name, UiTheme.Display, 19, UiTheme.Chrome);
            var barRt = MainScreen.Rect(rt, "Bar");
            MainScreen.Place(barRt, new Vector2(0.5f, 1), new Vector2(0, -imgH - 42), new Vector2(w * 0.62f, 3));
            var bar = barRt.gameObject.AddComponent<Image>();
            bar.color = UiTheme.Gold;
            bar.raycastTarget = false;
            var noteRt = MainScreen.Rect(rt, "Note");
            MainScreen.Place(noteRt, new Vector2(0.5f, 1), new Vector2(0, -imgH - 74), new Vector2(w, 40));
            var note = MainScreen.Label(noteRt, Notes.TryGetValue(e.Id, out var n) ? n : "", UiTheme.Body, 11, UiTheme.Muted);
            note.textWrappingMode = TextWrappingModes.Normal;
            return (e.Id, name, bar);
        }

        /// <summary>今の演出に印を付ける</summary>
        public void SetCurrent(string id)
        {
            string cur = EffectCatalog.Get(id).Id;
            foreach (var (cid, name, bar) in cards)
            {
                bool on = cid == cur;
                name.color = on ? UiTheme.Gold : UiTheme.Chrome;
                bar.enabled = on;
            }
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

        /// <summary>確かめ用：その演出の札を押したことにする</summary>
        public void PressForCheck(string id) => Picked?.Invoke(id);
    }
}
