using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PachiMoji
{
    /// <summary>
    /// 設定画面：火花・プチュンの光の色と大きさ・書体・昇格の範囲。黒い全面が下からせり上がる。
    /// 選んでいるものは金の文字と細い金の線。変えたらすぐ onChanged を呼ぶ（保存は PachiApp）。
    /// 買い物の項目は順番4で足す。
    /// </summary>
    public class SettingsScreen
    {
        public Button Back;
        RectTransform panel; CanvasGroup group;
        EffectSettings settings;
        Action<EffectSettings> onChanged;
        RectTransform rail; RectTransform[] medals; TMP_Text rangeLabel;
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

            float y = -92;
            m.Choice(safe, ref y, "火花", new[] { "あり", "なし" }, null, settings.SparksOn ? 0 : 1, 150,
                i => settings.SparksOn = i == 0);
            m.Choice(safe, ref y, "プチュンの光の色", new[] { "青白", "金", "赤", "虹" },
                new[] { UiTheme.Hex(0x8CBFFF), UiTheme.Hex(0xFFC74D), UiTheme.Hex(0xFF4033), Color.clear },
                (int)settings.Light, 80, i => settings.Light = (LightColor)i);
            m.Choice(safe, ref y, "プチュンの光の大きさ", new[] { "小", "中", "大" }, null, (int)settings.Size, 104,
                i => settings.Size = (LightSize)i);
            m.Choice(safe, ref y, "書体", new[] { "ポップ", "明朝" }, null, (int)settings.Font, 150,
                i => settings.Font = (TextFont)i);
            m.Range(safe, y);

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
        void Range(RectTransform parent, float y)
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
                mi.sprite = UiArt.Medal((Tier)i); mi.raycastTarget = false;
            }
            var drag = row.gameObject.AddComponent<MedalDrag>();
            drag.IndexAt = local => Mathf.Clamp(Mathf.RoundToInt(local.x / 110f) + 1, 0, 2);
            drag.Preview = (a, b) => ShowRange(RangePicker.FromDrag(a, b));
            drag.Commit = (a, b) =>
            {
                (settings.From, settings.To) = RangePicker.FromDrag(a, b);
                ShowRange((settings.From, settings.To));
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
        }

        // 金の帯を、はじまりのメダルからおわりのメダルまで伸ばし、メダルの明るさで範囲を見せる
        void ShowRange((Tier from, Tier to) r)
        {
            float x0 = ((int)r.from - 1) * 110, x1 = ((int)r.to - 1) * 110;
            MainScreen.Place(rail, new Vector2(0.5f, 0.5f), new Vector2((x0 + x1) / 2, 0), new Vector2(x1 - x0 + 40, 22));
            for (int i = 0; i < 3; i++)
            {
                bool inside = i >= (int)r.from && i <= (int)r.to;
                medals[i].GetComponent<Image>().color = inside ? Color.white : new Color(0.35f, 0.35f, 0.35f, 1);
            }
            rangeLabel.text = r.from == r.to ? TierNames[(int)r.from] + "だけ" : TierNames[(int)r.from] + " → " + TierNames[(int)r.to];
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
