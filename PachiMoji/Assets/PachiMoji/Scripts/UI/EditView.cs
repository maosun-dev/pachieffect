using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PachiMoji
{
    /// <summary>
    /// 激アツの帯の「編集」の画面（1つにまとめた）。下の帯で動かすものを選ぶ：写真・動画／上の帯／下の帯。
    /// 選んだものだけを、指で動かす（1本）・大きさを変える（2本）。舞台は書き出す動画と同じ形で、画面いっぱいに見せる。
    /// 写真・動画を選んでいるときは「入れる・変える・外す」と、動画なら使う区間のつまみ。帯を選んでいるときは「元に戻す」
    /// </summary>
    public class EditView
    {
        public enum Target { Media, Top, Bottom }

        public Button Pick, Remove, Reset, Done;
        /// <summary>写真のずらし・倍率が変わったとき</summary>
        public event Action<Vector2, float> MediaMoved;
        /// <summary>帯の列（0＝上・1＝下）のずらし・大きさが変わったとき</summary>
        public event Action<int, Vector2, float> LaneMoved;
        /// <summary>動画の使う区間が変わったとき（はじめ・おわり、秒）</summary>
        public event Action<float, float> RangeChanged;

        RectTransform panel;
        CanvasGroup group;
        readonly Button[] tabs = new Button[3];
        readonly Image[] tabBars = new Image[3];
        readonly TMP_Text[] tabTexts = new TMP_Text[3];
        TMP_Text pickText, hint;
        public Target Current { get; private set; }

        // 写真
        Vector2 mediaOffset; float mediaZoom = 1, imgAspect = 1, frameAspect = PhotoFit.FrameAspect;
        bool hasMedia;
        RectTransform stageView;   // 書き出す動画の形に収めた舞台の板（指の動きを、この幅を 1 とする単位に直す）
        public Vector2 MediaOffset => mediaOffset;
        public float MediaZoom => mediaZoom;

        // 帯
        readonly Vector2[] laneOffset = new Vector2[2];
        readonly float[] laneScale = { 1, 1 };
        float unitsPerScreen = 10;
        public Vector2 LaneOffset(int lane) => laneOffset[lane];
        public float LaneScale(int lane) => laneScale[lane];

        // 動画の区間
        RectTransform rangeBox, rail, fill, hStart, hEnd;
        TMP_Text rangeLabel;
        float vStart, vEnd, vLength;
        public float VideoStart => vStart;
        public float VideoEnd => vEnd;

        public static EditView Build(RectTransform canvasRoot, Rect safePx, Vector2 screenPx, RectTransform stageView)
        {
            var m = new EditView { stageView = stageView };
            m.panel = MainScreen.Rect(canvasRoot, "Edit");
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

            // 上：いま何を動かしているか（ひとこと）
            var hintRt = MainScreen.Rect(safe, "Hint");
            MainScreen.Place(hintRt, new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(340, 40));
            var bg = hintRt.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.6f);
            bg.raycastTarget = false;
            m.hint = MainScreen.Label(hintRt, "", UiTheme.Body, 13, UiTheme.Chrome);

            // 下：黒い帯に、動かすものの切り替え（3つ）と、その下にボタン
            var bar = MainScreen.Rect(safe, "Bar");
            bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(1, 0);
            bar.pivot = new Vector2(0.5f, 0);
            bar.sizeDelta = new Vector2(0, 128);
            bar.anchoredPosition = new Vector2(0, UiTheme.BannerHeight);
            bar.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.85f);
            var mid = new Vector2(0.5f, 0.5f);
            string[] names = { "写真・動画", "上の帯", "下の帯" };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                m.tabs[i] = MainScreen.PlateButton(bar, names[i], mid, new Vector2((i - 1) * 118, 30), new Vector2(110, 44), UiTheme.Display, 15, UiTheme.Chrome);
                m.tabTexts[i] = m.tabs[i].GetComponentInChildren<TMP_Text>();
                m.tabBars[i] = Underline(m.tabs[i]);
                m.tabs[i].onClick.AddListener(() => m.Select((Target)k));
            }
            m.Pick = MainScreen.PlateButton(bar, "写真・動画を入れる", mid, new Vector2(-118, -30), new Vector2(110, 44), UiTheme.Body, 12, UiTheme.Chrome);
            m.pickText = m.Pick.GetComponentInChildren<TMP_Text>();
            m.Remove = MainScreen.PlateButton(bar, "外す", mid, new Vector2(0, -30), new Vector2(110, 44), UiTheme.Body, 14, UiTheme.Chrome);
            m.Reset = MainScreen.PlateButton(bar, "元に戻す", mid, new Vector2(-118, -30), new Vector2(110, 44), UiTheme.Body, 14, UiTheme.Chrome);
            m.Done = MainScreen.PlateButton(bar, "できた", mid, new Vector2(118, -30), new Vector2(110, 44), UiTheme.Display, 18, UiTheme.Gold);
            m.Reset.onClick.AddListener(() =>
            {
                if (m.Current == Target.Media) m.SetMedia(Vector2.zero, 1);
                else m.SetLane((int)m.Current - 1, Vector2.zero, 1);
            });

            // 動画の区間：下の帯のすぐ上に、細い金のレールと、はじめ・おわりの2つのつまみ
            m.rangeBox = MainScreen.Rect(safe, "Range");
            m.rangeBox.anchorMin = new Vector2(0, 0); m.rangeBox.anchorMax = new Vector2(1, 0);
            m.rangeBox.pivot = new Vector2(0.5f, 0);
            m.rangeBox.sizeDelta = new Vector2(0, 74);
            m.rangeBox.anchoredPosition = new Vector2(0, UiTheme.BannerHeight + 128);
            m.rangeBox.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.85f);
            var lab = MainScreen.Rect(m.rangeBox, "Label");
            MainScreen.Place(lab, new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(340, 20));
            m.rangeLabel = MainScreen.Label(lab, "", UiTheme.Body, 12, UiTheme.Chrome);
            m.rail = MainScreen.Rect(m.rangeBox, "Rail");
            MainScreen.Place(m.rail, new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(300, 4));
            var railImg = m.rail.gameObject.AddComponent<Image>();
            railImg.color = new Color(UiTheme.Chrome.r, UiTheme.Chrome.g, UiTheme.Chrome.b, 0.35f);
            railImg.raycastTarget = false;
            m.fill = MainScreen.Rect(m.rail, "Fill");
            m.fill.anchorMin = new Vector2(0, 0); m.fill.anchorMax = new Vector2(0, 1);
            m.fill.pivot = new Vector2(0, 0.5f);
            var fillImg = m.fill.gameObject.AddComponent<Image>();
            fillImg.color = UiTheme.Gold;
            fillImg.raycastTarget = false;
            m.hStart = Handle(m.rail, "Start", f => m.DragHandle(true, f));
            m.hEnd = Handle(m.rail, "End", f => m.DragHandle(false, f));
            m.rangeBox.gameObject.SetActive(false);

            MainScreen.SetLayer(m.panel.gameObject, MainScreen.UiLayer);
            m.Show(false);
            return m;
        }

        // 選んでいるものの印（設定の画面と同じ金の下線）
        static Image Underline(Button b)
        {
            var rt = MainScreen.Rect((RectTransform)b.transform, "Bar");
            MainScreen.Place(rt, new Vector2(0.5f, 0), new Vector2(0, 7), new Vector2(60, 3));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = UiTheme.Gold;
            img.raycastTarget = false;
            return img;
        }

        // つまみ（押せる範囲は大きめ、見た目は金の丸）。f はレールの左端からの割合（0〜1）
        static RectTransform Handle(RectTransform rail, string name, Action<float> drag)
        {
            var h = MainScreen.Rect(rail, name);
            h.anchorMin = h.anchorMax = new Vector2(0, 0.5f);
            h.sizeDelta = new Vector2(44, 44);
            h.gameObject.AddComponent<Image>().color = Color.clear;
            var dot = MainScreen.Rect(h, "Dot");
            MainScreen.Place(dot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));
            var img = dot.gameObject.AddComponent<Image>();
            img.sprite = UiArt.Dot(UiTheme.Gold);
            img.raycastTarget = false;
            h.gameObject.AddComponent<RailHandle>().Drag = drag;
            return h;
        }

        /// <summary>
        /// 編集を始める。写真（ずらし・倍率・横÷縦・入っているか）、帯（上下のずらし・大きさ）、
        /// 画面の縦を帯の単位に直す倍率、書き出す動画の横÷縦
        /// </summary>
        public void Begin(Vector2 offset, float zoom, float imgAspect, bool hasMedia,
            Vector2 topOffset, float topScale, Vector2 bottomOffset, float bottomScale, float unitsPerScreenHeight, float frameAspect)
        {
            this.imgAspect = imgAspect;
            this.frameAspect = frameAspect;
            mediaZoom = PhotoFit.ClampZoom(zoom);
            mediaOffset = PhotoFit.ClampOffset(offset, imgAspect, mediaZoom, frameAspect);
            laneOffset[0] = topOffset; laneScale[0] = topScale;
            laneOffset[1] = bottomOffset; laneScale[1] = bottomScale;
            unitsPerScreen = unitsPerScreenHeight;
            SetHasMedia(hasMedia);
            Select(hasMedia ? Target.Media : Target.Top);
            Show(true);
        }

        /// <summary>写真・動画が入っているか（ボタンの字と「外す」の出し入れ）</summary>
        public void SetHasMedia(bool on)
        {
            hasMedia = on;
            pickText.text = on ? "写真・動画を変える" : "写真・動画を入れる";
            Select(Current);
        }

        /// <summary>写真・動画の横÷縦（動画は準備ができてから分かる）</summary>
        public void SetImageAspect(float a) => imgAspect = a;

        public void Select(Target t)
        {
            Current = t;
            for (int i = 0; i < 3; i++)
            {
                bool on = (int)t == i;
                tabBars[i].enabled = on;
                tabTexts[i].color = on ? UiTheme.Gold : UiTheme.Chrome;
            }
            bool media = t == Target.Media;
            Pick.gameObject.SetActive(media);
            Remove.gameObject.SetActive(media && hasMedia);
            Reset.gameObject.SetActive(!media);
            rangeBox.gameObject.SetActive(media && vLength > 0);
            hint.text = media
                ? (hasMedia ? "写真・動画を、指で動かす・2本の指で大きさを変える" : "写真・動画を入れると、帯の後ろに映ります")
                : (t == Target.Top ? "上の帯を、指で動かす・2本の指で大きさを変える" : "下の帯を、指で動かす・2本の指で大きさを変える");
        }

        // 指の動き（画面の画素）を、選んでいるものの動きに直す
        void DragBy(Vector2 deltaPx)
        {
            if (Current == Target.Media)
            {
                if (!hasMedia) return;
                SetMedia(mediaOffset + deltaPx / FramePx(), mediaZoom);
            }
            else
            {
                int lane = (int)Current - 1;
                SetLane(lane, laneOffset[lane] + deltaPx / Mathf.Max(1, FramePxHeight()) * unitsPerScreen, laneScale[lane]);
            }
        }

        void ZoomBy(float k)
        {
            if (Current == Target.Media) { if (hasMedia) SetMedia(mediaOffset, mediaZoom * k); }
            else { int lane = (int)Current - 1; SetLane(lane, laneOffset[lane], laneScale[lane] * k); }
        }

        void SetMedia(Vector2 o, float z)
        {
            mediaZoom = PhotoFit.ClampZoom(z);
            mediaOffset = PhotoFit.ClampOffset(o, imgAspect, mediaZoom, frameAspect);
            MediaMoved?.Invoke(mediaOffset, mediaZoom);
        }

        public void SetLane(int lane, Vector2 o, float s)
        {
            laneOffset[lane] = EffectSettings.ClampObiOffset(o);
            laneScale[lane] = EffectSettings.ClampObiScale(s);
            LaneMoved?.Invoke(lane, laneOffset[lane], laneScale[lane]);
        }

        // 舞台の板の、画面の上での幅・高さ（画素）
        float FramePx() => ScreenSize().x;
        float FramePxHeight() => ScreenSize().y;
        Vector2 ScreenSize()
        {
            var c = new Vector3[4];
            stageView.GetWorldCorners(c);
            var cam = panel.GetComponentInParent<Canvas>().worldCamera;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]), b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
            return new Vector2(Mathf.Max(1, b.x - a.x), Mathf.Max(1, b.y - a.y));
        }

        void DragHandle(bool isStart, float f)
        {
            float t = Mathf.Clamp01(f) * vLength;
            if (isStart) SetRange(t, vEnd); else SetRange(vStart, t);
            RangeChanged?.Invoke(vStart, vEnd);
        }

        /// <summary>動画の区間のつまみを出す（length は動画の長さ。0 なら出さない）</summary>
        public void SetVideo(float start, float end, float length)
        {
            vLength = length;
            if (length > 0) SetRange(start, end > start ? end : length);
            rangeBox.gameObject.SetActive(Current == Target.Media && length > 0);
        }

        void SetRange(float s, float e)
        {
            var r = VideoRange.Clamp(s, e, vLength);
            vStart = r.start; vEnd = r.end;
            float w = rail.rect.width > 0 ? rail.rect.width : 300;
            hStart.anchoredPosition = new Vector2(vStart / vLength * w, 0);
            hEnd.anchoredPosition = new Vector2(vEnd / vLength * w, 0);
            fill.anchoredPosition = new Vector2(vStart / vLength * w, 0);
            fill.sizeDelta = new Vector2((vEnd - vStart) / vLength * w, 0);
            rangeLabel.text = $"使うところ　{vStart:0.0}秒 〜 {vEnd:0.0}秒（{vEnd - vStart:0.0}秒）";
        }

        public void Show(bool on)
        {
            group.alpha = on ? 1 : 0;
            group.interactable = group.blocksRaycasts = on;
        }
    }

    /// <summary>合わせる画面の指の動き：1本でドラッグ、2本でピンチ（手元ではマウスのホイールで大きさ）</summary>
    public class PhotoGesture : MonoBehaviour, IDragHandler
    {
        public Action<Vector2> Drag;
        public Action<float> Zoom;
        float pinchDist;

        public void OnDrag(PointerEventData e)
        {
            if (TouchCount() >= 2) return;   // 2本のときはピンチだけ
            Drag?.Invoke(e.delta);
        }

        void Update()
        {
            if (Drag == null) return;
            var ts = Touchscreen.current;
            if (ts != null && TouchCount() >= 2)
            {
                Vector2 a = Vector2.zero, b = Vector2.zero; int n = 0;
                foreach (var t in ts.touches)
                {
                    if (!t.press.isPressed) continue;
                    if (n == 0) a = t.position.ReadValue(); else b = t.position.ReadValue();
                    if (++n == 2) break;
                }
                float d = Vector2.Distance(a, b);
                if (pinchDist > 1 && d > 1) Zoom?.Invoke(d / pinchDist);
                pinchDist = d;
            }
            else pinchDist = 0;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float s = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(s) > 0.01f) Zoom?.Invoke(1 + Mathf.Sign(s) * 0.06f);
            }
        }

        static int TouchCount()
        {
            var ts = Touchscreen.current;
            if (ts == null) return 0;
            int n = 0;
            foreach (var t in ts.touches) if (t.press.isPressed) n++;
            return n;
        }
    }

    /// <summary>区間のつまみ：レールの上を横に動かす（f はレールの左端からの割合）</summary>
    public class RailHandle : MonoBehaviour, IDragHandler
    {
        public Action<float> Drag;

        public void OnDrag(PointerEventData e)
        {
            var rail = (RectTransform)transform.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rail, e.position, e.pressEventCamera, out var p))
                Drag?.Invoke((p.x - rail.rect.xMin) / Mathf.Max(1, rail.rect.width));
        }
    }
}
