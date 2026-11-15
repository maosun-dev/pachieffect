using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PachiMoji
{
    /// <summary>
    /// 写真を合わせる画面。舞台を画面いっぱいに広げた上に、書き出す動画の枠（9:16）を重ねて見せる。
    /// 枠の外は暗くして、帯と写真の位置を仕上がりのとおりに見ながら、指で動かす（1本）・大きさを変える（2本）。
    /// 下に「写真を変える」「写真を外す」「できた」
    /// </summary>
    public class PhotoAdjustView
    {
        public Button Change, Remove, Done;
        /// <summary>写真のずらし・倍率が変わったとき（PhotoFit で収めたあとの値）</summary>
        public event Action<Vector2, float> Moved;

        RectTransform panel, playArea, frame, dimTop, dimBottom;
        CanvasGroup group;
        Vector2 offset; float zoom = 1, imgAspect = 1;

        public Vector2 Offset => offset;
        public float Zoom => zoom;

        public static PhotoAdjustView Build(RectTransform canvasRoot, Rect safePx, Vector2 screenPx, RectTransform playArea)
        {
            var m = new PhotoAdjustView { playArea = playArea };
            m.panel = MainScreen.Rect(canvasRoot, "PhotoAdjust");
            m.panel.anchorMin = Vector2.zero; m.panel.anchorMax = Vector2.one;
            m.panel.offsetMin = m.panel.offsetMax = Vector2.zero;
            m.group = m.panel.gameObject.AddComponent<CanvasGroup>();

            // 指を受ける面（透明）。ボタンより奥
            var touch = MainScreen.Rect(m.panel, "Touch");
            touch.anchorMin = Vector2.zero; touch.anchorMax = Vector2.one;
            touch.offsetMin = touch.offsetMax = Vector2.zero;
            touch.gameObject.AddComponent<Image>().color = Color.clear;
            touch.gameObject.AddComponent<PhotoGesture>().View = m;

            // 枠の外を暗く（上と下。枠は画面の幅いっぱいなので左右は無い）
            m.dimTop = Dim(m.panel, "DimTop");
            m.dimBottom = Dim(m.panel, "DimBottom");
            // 書き出しの枠：細い金の線
            m.frame = MainScreen.Rect(m.panel, "Frame");
            m.frame.anchorMin = m.frame.anchorMax = new Vector2(0.5f, 0.5f);
            foreach (var (n, amin, amax, size) in new[]
            {
                ("T", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 2)), ("B", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 2)),
                ("L", new Vector2(0, 0), new Vector2(0, 1), new Vector2(2, 0)), ("R", new Vector2(1, 0), new Vector2(1, 1), new Vector2(2, 0)),
            })
            {
                var line = MainScreen.Rect(m.frame, n);
                line.anchorMin = amin; line.anchorMax = amax; line.pivot = new Vector2(0.5f, 0.5f);
                line.sizeDelta = size; line.anchoredPosition = Vector2.zero;
                var img = line.gameObject.AddComponent<Image>();
                img.color = new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, 0.85f);
                img.raycastTarget = false;
            }

            var safe = MainScreen.Rect(m.panel, "Safe");
            safe.anchorMin = new Vector2(safePx.xMin / screenPx.x, safePx.yMin / screenPx.y);
            safe.anchorMax = new Vector2(safePx.xMax / screenPx.x, safePx.yMax / screenPx.y);
            safe.offsetMin = safe.offsetMax = Vector2.zero;

            // 上：どうすればいいか（ひとこと）
            var hint = MainScreen.Rect(safe, "Hint");
            MainScreen.Place(hint, new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(340, 40));
            var bg = hint.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.6f);
            bg.raycastTarget = false;
            MainScreen.Label(hint, "指で動かす・2本の指で大きさを変える", UiTheme.Body, 13, UiTheme.Chrome);

            // 下：黒い帯に3つ
            var bar = MainScreen.Rect(safe, "Bar");
            bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(1, 0);
            bar.pivot = new Vector2(0.5f, 0);
            bar.sizeDelta = new Vector2(0, 76);
            bar.anchoredPosition = new Vector2(0, UiTheme.BannerHeight);
            bar.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.82f);
            var mid = new Vector2(0.5f, 0.5f);
            m.Change = MainScreen.PlateButton(bar, "写真を変える", mid, new Vector2(-124, 0), new Vector2(116, 44), UiTheme.Body, 14, UiTheme.Chrome);
            m.Remove = MainScreen.PlateButton(bar, "写真を外す", mid, new Vector2(0, 0), new Vector2(116, 44), UiTheme.Body, 14, UiTheme.Chrome);
            m.Done = MainScreen.PlateButton(bar, "できた", mid, new Vector2(124, 0), new Vector2(116, 44), UiTheme.Display, 18, UiTheme.Gold);

            MainScreen.SetLayer(m.panel.gameObject, MainScreen.UiLayer);
            m.Show(false);
            return m;
        }

        static RectTransform Dim(RectTransform parent, string name)
        {
            var rt = MainScreen.Rect(parent, name);
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.62f);
            img.raycastTarget = false;
            return rt;
        }

        /// <summary>合わせ始める（今のずらし・倍率と、写真の横÷縦）</summary>
        public void Begin(Vector2 offset, float zoom, float imgAspect)
        {
            this.imgAspect = imgAspect;
            this.zoom = PhotoFit.ClampZoom(zoom);
            this.offset = PhotoFit.ClampOffset(offset, imgAspect, this.zoom);
            Layout();
            Show(true);
        }

        // 書き出しの枠を、再生の範囲のまん中に置く（幅いっぱい、高さは幅の 16/9）
        void Layout()
        {
            Canvas.ForceUpdateCanvases();
            var c = new Vector3[4];
            playArea.GetWorldCorners(c);
            Vector2 a = panel.InverseTransformPoint(c[0]), b = panel.InverseTransformPoint(c[2]);
            float w = b.x - a.x, h = w / PhotoFit.FrameAspect;
            Vector2 center = (a + b) / 2;
            frame.sizeDelta = new Vector2(w, h);
            frame.anchoredPosition = center;
            float ph = panel.rect.height;
            float frameTop = center.y + h / 2 + ph / 2, frameBottom = center.y - h / 2 + ph / 2;   // 下からの高さ
            dimBottom.anchoredPosition = Vector2.zero;
            dimBottom.sizeDelta = new Vector2(0, Mathf.Max(0, frameBottom));
            dimTop.anchoredPosition = new Vector2(0, frameTop);
            dimTop.sizeDelta = new Vector2(0, Mathf.Max(0, ph - frameTop));
        }

        /// <summary>枠の幅（画面の画素）。指の動きを、枠の幅を 1 とする単位に直すのに使う</summary>
        float FramePx()
        {
            var c = new Vector3[4];
            frame.GetWorldCorners(c);
            var cam = panel.GetComponentInParent<Canvas>().worldCamera;
            return Mathf.Max(1, RectTransformUtility.WorldToScreenPoint(cam, c[2]).x - RectTransformUtility.WorldToScreenPoint(cam, c[0]).x);
        }

        internal void DragBy(Vector2 deltaPx)
        {
            SetFit(offset + deltaPx / FramePx(), zoom);
        }

        internal void ZoomBy(float k)
        {
            SetFit(offset, zoom * k);
        }

        /// <summary>ずらし・倍率を決める（PhotoFit で収めて、Moved で知らせる）</summary>
        public void SetFit(Vector2 o, float z)
        {
            zoom = PhotoFit.ClampZoom(z);
            offset = PhotoFit.ClampOffset(o, imgAspect, zoom);
            Moved?.Invoke(offset, zoom);
        }

        public void Show(bool on)
        {
            group.alpha = on ? 1 : 0;
            group.interactable = group.blocksRaycasts = on;
        }
    }

    /// <summary>写真を合わせる画面の指の動き：1本でドラッグ、2本でピンチ（手元ではマウスのホイールで大きさ）</summary>
    public class PhotoGesture : MonoBehaviour, IDragHandler
    {
        public PhotoAdjustView View;
        float pinchDist;

        public void OnDrag(PointerEventData e)
        {
            if (TouchCount() >= 2) return;   // 2本のときはピンチだけ
            View.DragBy(e.delta);
        }

        void Update()
        {
            if (View == null) return;
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
                if (pinchDist > 1 && d > 1) View.ZoomBy(d / pinchDist);
                pinchDist = d;
            }
            else pinchDist = 0;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float s = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(s) > 0.01f) View.ZoomBy(1 + Mathf.Sign(s) * 0.06f);
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
}
