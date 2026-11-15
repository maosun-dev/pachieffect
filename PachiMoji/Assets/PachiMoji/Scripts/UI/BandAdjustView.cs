using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PachiMoji
{
    /// <summary>
    /// 激アツの帯の位置と大きさを合わせる画面。舞台を画面いっぱいに広げた上で、
    /// 上の列・下の列を選んで、指で動かす（1本）・大きさを変える（2本）。
    /// 下に「上の列／下の列」「元に戻す」「できた」
    /// </summary>
    public class BandAdjustView
    {
        public Button TopTab, BottomTab, Reset, Done;
        /// <summary>列のずらし・大きさが変わったとき（列 0＝上・1＝下、ずらし、大きさ）</summary>
        public event Action<int, Vector2, float> Moved;

        RectTransform panel;
        CanvasGroup group;
        readonly Vector2[] offset = new Vector2[2];
        readonly float[] scale = { 1, 1 };
        Image topBar, bottomBar;
        TMP_Text topText, bottomText;
        float unitsPerScreen = 10;   // 画面の縦の長さを、帯の動く単位に直す倍率

        public int Lane { get; private set; }
        public Vector2 Offset(int lane) => offset[lane];
        public float Scale(int lane) => scale[lane];

        public static BandAdjustView Build(RectTransform canvasRoot, Rect safePx, Vector2 screenPx)
        {
            var m = new BandAdjustView();
            m.panel = MainScreen.Rect(canvasRoot, "BandAdjust");
            m.panel.anchorMin = Vector2.zero; m.panel.anchorMax = Vector2.one;
            m.panel.offsetMin = m.panel.offsetMax = Vector2.zero;
            m.group = m.panel.gameObject.AddComponent<CanvasGroup>();

            // 指を受ける面（透明）。ボタンより奥
            var touch = MainScreen.Rect(m.panel, "Touch");
            touch.anchorMin = Vector2.zero; touch.anchorMax = Vector2.one;
            touch.offsetMin = touch.offsetMax = Vector2.zero;
            touch.gameObject.AddComponent<Image>().color = Color.clear;
            var g = touch.gameObject.AddComponent<PhotoGesture>();
            g.Drag = d => m.DragBy(d);
            g.Zoom = k => m.ZoomBy(k);

            var safe = MainScreen.Rect(m.panel, "Safe");
            safe.anchorMin = new Vector2(safePx.xMin / screenPx.x, safePx.yMin / screenPx.y);
            safe.anchorMax = new Vector2(safePx.xMax / screenPx.x, safePx.yMax / screenPx.y);
            safe.offsetMin = safe.offsetMax = Vector2.zero;

            var hint = MainScreen.Rect(safe, "Hint");
            MainScreen.Place(hint, new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(340, 40));
            var bg = hint.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.6f);
            bg.raycastTarget = false;
            MainScreen.Label(hint, "列を選んで、指で動かす・2本の指で大きさを変える", UiTheme.Body, 12, UiTheme.Chrome);

            var bar = MainScreen.Rect(safe, "Bar");
            bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(1, 0);
            bar.pivot = new Vector2(0.5f, 0);
            bar.sizeDelta = new Vector2(0, 128);
            bar.anchoredPosition = new Vector2(0, UiTheme.BannerHeight);
            bar.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.82f);
            var mid = new Vector2(0.5f, 0.5f);
            m.TopTab = MainScreen.PlateButton(bar, "上の列", mid, new Vector2(-62, 30), new Vector2(116, 44), UiTheme.Display, 16, UiTheme.Chrome);
            m.BottomTab = MainScreen.PlateButton(bar, "下の列", mid, new Vector2(62, 30), new Vector2(116, 44), UiTheme.Display, 16, UiTheme.Chrome);
            m.Reset = MainScreen.PlateButton(bar, "元に戻す", mid, new Vector2(-62, -30), new Vector2(116, 44), UiTheme.Body, 14, UiTheme.Chrome);
            m.Done = MainScreen.PlateButton(bar, "できた", mid, new Vector2(62, -30), new Vector2(116, 44), UiTheme.Display, 18, UiTheme.Gold);
            m.topText = m.TopTab.GetComponentInChildren<TMP_Text>();
            m.bottomText = m.BottomTab.GetComponentInChildren<TMP_Text>();
            m.topBar = Underline(m.TopTab);
            m.bottomBar = Underline(m.BottomTab);
            m.TopTab.onClick.AddListener(() => m.Select(0));
            m.BottomTab.onClick.AddListener(() => m.Select(1));
            m.Reset.onClick.AddListener(() => { m.SetFit(m.Lane, Vector2.zero, 1); });

            MainScreen.SetLayer(m.panel.gameObject, MainScreen.UiLayer);
            m.Show(false);
            return m;
        }

        // 選んでいる列の印（設定の画面と同じ金の下線）
        static Image Underline(Button b)
        {
            var rt = MainScreen.Rect((RectTransform)b.transform, "Bar");
            MainScreen.Place(rt, new Vector2(0.5f, 0), new Vector2(0, 7), new Vector2(60, 3));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = UiTheme.Gold;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>合わせ始める（今のずらし・大きさと、画面の縦の長さを帯の単位に直す倍率）</summary>
        public void Begin(Vector2 topOffset, float topScale, Vector2 bottomOffset, float bottomScale, float unitsPerScreenHeight)
        {
            offset[0] = topOffset; scale[0] = topScale;
            offset[1] = bottomOffset; scale[1] = bottomScale;
            unitsPerScreen = unitsPerScreenHeight;
            Select(0);
            Show(true);
        }

        public void Select(int lane)
        {
            Lane = lane;
            topBar.enabled = lane == 0;
            bottomBar.enabled = lane == 1;
            topText.color = lane == 0 ? UiTheme.Gold : UiTheme.Chrome;
            bottomText.color = lane == 1 ? UiTheme.Gold : UiTheme.Chrome;
        }

        void DragBy(Vector2 deltaPx) => SetFit(Lane, offset[Lane] + deltaPx / Mathf.Max(1, Screen.height) * unitsPerScreen, scale[Lane]);
        void ZoomBy(float k) => SetFit(Lane, offset[Lane], scale[Lane] * k);

        /// <summary>列のずらし・大きさを決める（範囲に収めて、Moved で知らせる）</summary>
        public void SetFit(int lane, Vector2 o, float s)
        {
            offset[lane] = EffectSettings.ClampObiOffset(o);
            scale[lane] = EffectSettings.ClampObiScale(s);
            Moved?.Invoke(lane, offset[lane], scale[lane]);
        }

        public void Show(bool on)
        {
            group.alpha = on ? 1 : 0;
            group.interactable = group.blocksRaycasts = on;
        }
    }
}
