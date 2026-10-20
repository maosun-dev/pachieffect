using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PachiMoji
{
    /// <summary>
    /// 設定画面：火花・プチュンの光の色と大きさ・書体・昇格の色・チラ見せの文字数と色。黒い全面が下からせり上がる。
    /// 項目は縦に動かせる（見出しと「もどる」は動かない）。
    /// 選んでいるものは金の文字と細い金の線。変えたらすぐ onChanged を呼ぶ（保存は PachiApp）。
    /// 買い物は別の画面（ShopScreen）。
    /// </summary>
    public class SettingsScreen : ICover
    {
        public Button Back;
        RectTransform panel; CanvasGroup group;
        EffectSettings settings;
        Action<EffectSettings> onChanged;
        RectTransform rail; Image[] medals;
        TMP_Text teaseCountLabel, teaseAllLabel; int wordLength;
        ScrollRect scroll;

        /// <summary>昇格の色の文字（「緑 → 赤 → 虹」）</summary>
        public TMP_Text ChainLabel { get; private set; }
        RectTransform content;

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
            m.ChainSection(parent, ref y);
            m.TeaseCountSection(parent, ref y);
            m.Choice(parent, ref y, "チラ見せの文字色", TierNames7(), null, (int)settings.TeaseText, 44,
                i => settings.TeaseText = (Tier)i, TierTextColors());
            m.Choice(parent, ref y, "チラ見せの背景", TierNames7(), null, (int)settings.TeaseBack, 44,
                i => settings.TeaseBack = (Tier)i, TierTextColors());
            content.sizeDelta = new Vector2(0, -y + 24);

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

        /// <summary>縦の位置（1＝いちばん上、0＝いちばん下）</summary>
        public void ScrollTo(float normalized)
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = normalized;
        }

        /// <summary>中身が見える範囲より長く、縦に動かせるか</summary>
        public bool Scrollable => scroll.content.rect.height > scroll.viewport.rect.height;

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
        // textColors を渡すと、字をその色で書き（Color.clear は虹色）、選んでも字の色は変えず金の下線だけで示す
        void Choice(RectTransform parent, ref float y, string label, string[] options, Color[] dots, int selected,
            float width, Action<int> pick, Color[] textColors = null)
        {
            var head = MainScreen.Rect(parent, label);
            MainScreen.Place(head, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(340, 22));
            var ht = MainScreen.Label(head, label, UiTheme.Body, 13, UiTheme.Muted);
            ht.alignment = TextAlignmentOptions.Left;
            y -= 40;

            var texts = new List<TMP_Text>();
            var lines = new List<Image>();
            float gap = options.Length >= 7 ? 5 : 8, total = options.Length * width + (options.Length - 1) * gap;
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                float x = -total / 2 + width / 2 + i * (width + gap);
                var b = MainScreen.PlateButton(parent, options[i], new Vector2(0.5f, 1), new Vector2(x, y), new Vector2(width, 48),
                    UiTheme.Display, 18, UiTheme.Chrome);
                var text = b.GetComponentInChildren<TMP_Text>();
                if (textColors != null)
                {
                    text.color = textColors[i].a > 0 ? textColors[i] : Color.white;
                    if (textColors[i].a == 0)
                    {
                        text.enableVertexGradient = true;   // 虹：左上から右下へ色が回る
                        text.colorGradient = new VertexGradient(Color.HSVToRGB(0f, 0.7f, 1), Color.HSVToRGB(0.33f, 0.7f, 1),
                            Color.HSVToRGB(0.6f, 0.7f, 1), Color.HSVToRGB(0.8f, 0.7f, 1));
                    }
                }
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
                    Mark(texts, lines, index, textColors != null);
                    onChanged(settings);
                });
            }
            Mark(texts, lines, selected, textColors != null);
            y -= 52;
        }

        static void Mark(List<TMP_Text> texts, List<Image> lines, int selected, bool keepColors)
        {
            for (int i = 0; i < texts.Count; i++)
            {
                if (!keepColors) texts[i].color = i == selected ? UiTheme.Gold : UiTheme.Chrome;
                lines[i].enabled = i == selected;
            }
        }

        const float MedalSize = 40, MedalGap = 47;   // 7枚が1列に収まる大きさ

        // 見出し（灰）
        static void Head(RectTransform parent, float y, string text)
        {
            var head = MainScreen.Rect(parent, text);
            MainScreen.Place(head, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(340, 22));
            MainScreen.Label(head, text, UiTheme.Body, 13, UiTheme.Muted).alignment = TextAlignmentOptions.Left;
        }

        // 昇格の色：7枚のメダルを格の順に並べ、押すと入る・外れる（最後の1枚は外れない）
        void ChainSection(RectTransform parent, ref float y)
        {
            Head(parent, y, "昇格の色");
            y -= 44;
            var row = MainScreen.Rect(parent, "Medals");
            MainScreen.Place(row, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(340, 48));
            rail = MainScreen.Rect(row, "Rail");
            var ri = rail.gameObject.AddComponent<Image>();
            ri.sprite = UiArt.Rail(); ri.type = Image.Type.Sliced; ri.raycastTarget = false;
            medals = new Image[7];
            for (int i = 0; i < 7; i++)
            {
                var tier = (Tier)i;
                var rt = MainScreen.Rect(row, "Medal" + i);
                MainScreen.Place(rt, new Vector2(0.5f, 0.5f), new Vector2((i - 3) * MedalGap, 0), new Vector2(MedalSize, MedalSize));
                medals[i] = rt.gameObject.AddComponent<Image>();
                medals[i].sprite = UiArt.Medal(tier);
                var b = rt.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                rt.gameObject.AddComponent<PressSink>();
                b.onClick.AddListener(() => PressMedal(tier));
            }
            y -= 40;
            var label = MainScreen.Rect(parent, "ChainLabel");
            MainScreen.Place(label, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(340, 30));
            ChainLabel = MainScreen.Label(label, "", UiTheme.Display, 20, UiTheme.Chrome);
            y -= 28;
            var help = MainScreen.Rect(parent, "ChainHelp");
            MainScreen.Place(help, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(340, 24));
            MainScreen.Label(help, "メダルを押して、通る色を選びます。最初の色でドンと着地します", UiTheme.Body, 11, UiTheme.Muted);
            y -= 44;
            ShowChain();
        }

        /// <summary>メダルを押したとき（手元確認からも呼ぶ）</summary>
        public void PressMedal(Tier t)
        {
            settings.Chain = Chain.Toggle(settings.Chain, t);
            ShowChain();
            onChanged(settings);
        }

        // 点いたメダルは明るく、消えたメダルは暗く。点いた最初から最後まで金の帯を後ろに通す
        void ShowChain()
        {
            var chain = settings.Chain;
            int first = (int)chain[0], last = (int)chain[chain.Length - 1];
            float x0 = (first - 3) * MedalGap, x1 = (last - 3) * MedalGap;
            MainScreen.Place(rail, new Vector2(0.5f, 0.5f), new Vector2((x0 + x1) / 2, 0), new Vector2(x1 - x0 + 16, 14));
            rail.gameObject.SetActive(first != last);
            for (int i = 0; i < 7; i++)
                medals[i].color = System.Array.IndexOf(chain, (Tier)i) >= 0 ? Color.white : new Color(0.45f, 0.45f, 0.45f, 1);   // 消えたメダルも何色か分かる明るさ
            ChainLabel.text = Chain.Label(chain);
        }

        // チラ見せの文字数：左右の矢印で 0（なし）〜12。言葉の文字数以上なら「（全部）」
        void TeaseCountSection(RectTransform parent, ref float y)
        {
            Head(parent, y, "チラ見せの文字数");
            y -= 40;
            var minus = ArrowButton(parent, false, new Vector2(-96, y));
            var plus = ArrowButton(parent, true, new Vector2(96, y));
            minus.onClick.AddListener(() => StepTease(-1));
            plus.onClick.AddListener(() => StepTease(1));
            var num = MainScreen.Rect(parent, "TeaseCount");
            MainScreen.Place(num, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(120, 48));
            teaseCountLabel = MainScreen.Label(num, "", UiTheme.Display, 26, UiTheme.Chrome);
            y -= 32;
            var all = MainScreen.Rect(parent, "TeaseAll");
            MainScreen.Place(all, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(200, 20));
            teaseAllLabel = MainScreen.Label(all, "", UiTheme.Body, 12, UiTheme.Muted);
            y -= 36;
            ShowTeaseCount();
        }

        // 矢印の銀の板（矢印はコードで描いた三角）
        Button ArrowButton(RectTransform parent, bool right, Vector2 pos)
        {
            var b = MainScreen.PlateButton(parent, right ? "Next" : "Prev", new Vector2(0.5f, 1), pos, new Vector2(64, 48), UiTheme.Display, 1, Color.clear);
            var icon = MainScreen.Rect(b.transform, "Arrow");
            MainScreen.Place(icon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18));
            var img = icon.gameObject.AddComponent<Image>();
            img.sprite = UiArt.Arrow(right);
            img.raycastTarget = false;
            return b;
        }

        void StepTease(int d)
        {
            settings.TeaseCount = Mathf.Clamp(settings.TeaseCount + d, 0, TeasePlan.MaxChars);
            ShowTeaseCount();
            onChanged(settings);
        }

        void ShowTeaseCount()
        {
            int n = settings.TeaseCount;
            teaseCountLabel.text = n == 0 ? "なし" : n.ToString();
            teaseAllLabel.text = n > 0 && wordLength > 0 && n >= wordLength ? "（全部）" : "";
        }

        /// <summary>今の言葉の文字数（設定を開く前に PachiApp が渡す。「（全部）」に使う）</summary>
        public void SetWordLength(int n)
        {
            wordLength = n;
            ShowTeaseCount();
        }

        // 色の字の見本：銀は銀、金は金、虹は虹色（Color.clear で虹の印）
        static Color[] TierTextColors() => new[]
        {
            UiTheme.Chrome, Hex(TierColors.TextBase(Tier.Blue)), Hex(TierColors.TextBase(Tier.Green)), Hex(TierColors.TextBase(Tier.Purple)),
            Hex(TierColors.TextBase(Tier.Red)), UiTheme.Gold, Color.clear,
        };

        static string[] TierNames7() => System.Array.ConvertAll((Tier[])System.Enum.GetValues(typeof(Tier)), TierColors.Name);

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
