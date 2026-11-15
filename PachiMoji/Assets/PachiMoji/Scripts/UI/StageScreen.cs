using UnityEngine;
using UnityEngine.UI;

namespace PachiMoji
{
    /// <summary>
    /// 画面の中の演出の舞台。演出は見えない板（RenderTexture）に描き、その板を画面の部品（RawImage）として貼る。
    /// （URP はカメラを2台重ねると後のカメラが画面を消してしまうので、画面に描くカメラは UI の1台だけにする。
    /// 順番3の動画の書き出しも、同じく板に描く形になる）
    /// 板は再生の範囲（バナーより上の全部）の大きさ。待ちのあいだは、その中の入力の板より上に縮めて描く。
    /// </summary>
    public class StageScreen
    {
        readonly MainScreen ui;
        readonly Camera uiCam;
        RenderTexture rt;
        Rect frame = new Rect(0, 0, 1, 1);   // 板の中で絵を置く範囲（0〜1）
        bool fit;            // 書き出す動画と同じ形で、範囲の中に収めて見せる（激アツの帯）
        float fitAspect = 1;
        bool full;

        public IPachiEffect Effect { get; private set; }

        public StageScreen(MainScreen ui, Camera uiCam)
        {
            this.ui = ui;
            this.uiCam = uiCam;
        }

        /// <summary>言葉と設定で舞台を組み立てる（前の舞台は片付ける）。screenPx は画面全体の画素</summary>
        public void Build(string[] lines, EffectSettings settings, Vector2 screenPx)
        {
            Dispose();
            var play = MainScreen.ViewportOf(ui.StagePlayArea, uiCam);
            int w = Mathf.Max(16, Mathf.RoundToInt(play.width * screenPx.x));
            int h = Mathf.Max(16, Mathf.RoundToInt(play.height * screenPx.y));
            // 激アツの帯は、書き出す動画と同じ形（縦 9:16・横 16:9）で組み立て、待ちでも再生でもその形のまま見せる
            // （待ちの画面だけ切り取られて、書き出しと見え方が違うと分かりにくいため）
            fit = EffectCatalog.Get(settings?.EffectId).Id == "obi";
            if (fit)
            {
                var size = ExportPlan.Size(settings != null && settings.ExportWide);
                fitAspect = (float)size.x / size.y;
                if ((float)w / h > fitAspect) w = Mathf.Max(16, Mathf.RoundToInt(h * fitAspect));
                else h = Mathf.Max(16, Mathf.RoundToInt(w / fitAspect));
            }
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Stage" };
            rt.Create();
            Effect = EffectCatalog.Get(settings?.EffectId).Create();
            Effect.Build(lines, (float)w / h, settings);
            Effect.Camera.targetTexture = rt;
            Effect.Camera.cullingMask &= ~(1 << MainScreen.UiLayer);
            ui.StageView.texture = rt;
            ui.StageView.enabled = true;
        }

        /// <summary>見せるか（入力が空などで、前の言葉を出したままにしないとき false）</summary>
        public void SetVisible(bool on)
        {
            ui.StageView.enabled = on && Effect != null;
            if (Effect != null) Effect.Camera.enabled = on;
        }

        public bool Visible => Effect != null && ui.StageView.enabled;

        /// <summary>再生・終わりは板いっぱい、待ちは板の中の入力の板より上</summary>
        public void SetFull(bool full)
        {
            this.full = full;
            if (fit)
            {
                // 板（RawImage）を、待ち・再生の範囲の中に、書き出す動画と同じ形で収める
                frame = new Rect(0, 0, 1, 1);
                var area = full ? ui.StagePlayArea : ui.StageIdleArea;
                var playRt = ui.StagePlayArea;
                var c = new Vector3[4];
                area.GetWorldCorners(c);
                Vector2 a = playRt.InverseTransformPoint(c[0]), b = playRt.InverseTransformPoint(c[2]);
                float aw = b.x - a.x, ah = b.y - a.y;
                float vw = aw, vh = aw / fitAspect;
                if (vh > ah) { vh = ah; vw = ah * fitAspect; }
                var view = ui.StageView.rectTransform;
                view.anchorMin = view.anchorMax = new Vector2(0.5f, 0.5f);
                view.pivot = new Vector2(0.5f, 0.5f);
                view.sizeDelta = new Vector2(vw, vh);
                view.anchoredPosition = (a + b) / 2 - (Vector2)playRt.rect.center;
                return;
            }
            var v = ui.StageView.rectTransform;
            v.anchorMin = Vector2.zero; v.anchorMax = Vector2.one;
            v.offsetMin = v.offsetMax = Vector2.zero;
            if (full) { frame = new Rect(0, 0, 1, 1); return; }
            var play = MainScreen.ViewportOf(ui.StagePlayArea, uiCam);
            var idle = MainScreen.ViewportOf(ui.StageIdleArea, uiCam);
            frame = new Rect((idle.x - play.x) / play.width, (idle.y - play.y) / play.height,
                idle.width / play.width, idle.height / play.height);
        }

        /// <summary>毎コマ、演出が姿（画角）を決めたあとに呼ぶ：絵を置く範囲に収める</summary>
        public void Frame()
        {
            if (Effect == null) return;
            MainScreen.FrameStage(Effect.Camera, frame, new Vector2(rt.width, rt.height));
        }

        public void Dispose()
        {
            Effect?.Dispose();
            Effect = null;
            if (rt != null)
            {
                ui.StageView.texture = null;
                ui.StageView.enabled = false;
                rt.Release();
                if (Application.isPlaying) Object.Destroy(rt); else Object.DestroyImmediate(rt);
                rt = null;
            }
        }
    }
}
