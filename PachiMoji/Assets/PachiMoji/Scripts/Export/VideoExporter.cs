using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace PachiMoji
{
    public enum ExportStatus { Running, Done, Failed, Cancelled }

    /// <summary>
    /// 演出を画面に出さない 1080×1920 の板に 1/30 秒ずつ描き、印を重ねて、書き手（IVideoWriter）に1コマずつ渡す。
    /// 演出は画面の舞台とは別に組み立てる（画面の比率に縮めない、9:16 そのままの姿）。
    /// Step() を毎コマ呼ぶ形なので、アプリは Update から、手元確認は裏起動のループから同じものを動かせる。
    /// </summary>
    public class VideoExporter : IDisposable
    {
        const int MaxPerStep = 2;    // 1回の Step で描くコマの上限
        const int MaxInFlight = 3;   // 読み出しを待つコマの上限

        readonly string[] lines;
        readonly EffectSettings settings;
        readonly bool watermark;
        readonly IVideoWriter writer;
        readonly Queue<(int frame, AsyncGPUReadbackRequest req)> pending = new Queue<(int, AsyncGPUReadbackRequest)>();
        IPachiEffect effect;
        RenderTexture rt;
        Texture2D mark;
        bool started, finishing;
        int rendered, appended;

        public ExportStatus Status { get; private set; } = ExportStatus.Running;
        public ExportError Error { get; private set; }
        public int TotalFrames { get; private set; }
        public float Duration { get; private set; }
        public int Percent => ExportPlan.Percent(appended, TotalFrames, Status == ExportStatus.Done);
        public Texture Preview => rt;
        public string OutputPath { get; }

        public VideoExporter(string[] lines, EffectSettings settings, bool watermark, IVideoWriter writer, string outPath)
        {
            this.lines = lines;
            this.settings = settings;
            this.watermark = watermark;
            this.writer = writer;
            OutputPath = outPath;
        }

        /// <summary>空き容量を確かめ、演出を組み立て、書き始める。None 以外なら何も残さない</summary>
        public ExportError Start()
        {
            effect = EffectCatalog.Get("oudou").Create();
            effect.Build(lines, (float)ExportPlan.Width / ExportPlan.Height, settings);
            Duration = effect.Duration;
            TotalFrames = ExportPlan.FrameCount(Duration);

            string dir = Path.GetDirectoryName(Path.GetFullPath(OutputPath));
            if (writer.FreeBytes(dir) < ExportPlan.NeedBytes(Duration)) return Fail(ExportError.NoSpace);

            rt = new RenderTexture(ExportPlan.Width, ExportPlan.Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Export" };
            rt.Create();
            var cam = effect.Camera;
            cam.targetTexture = rt;
            cam.cullingMask &= ~(1 << MainScreen.UiLayer);
            cam.enabled = false;   // 自動では描かせず、Step で1コマずつ描く
            if (watermark) mark = Resources.Load<Texture2D>("Export/Watermark");

            started = true;
            var e = writer.Begin(OutputPath, ExportPlan.Width, ExportPlan.Height, ExportPlan.Fps);
            return e == ExportError.None ? e : Fail(e);
        }

        /// <summary>毎コマ呼ぶ：描く → 読み出しの終わったコマから順に書き手へ → 全部渡したら仕上げを待つ</summary>
        public void Step()
        {
            if (Status != ExportStatus.Running || !started) return;
            if (finishing)
            {
                var st = writer.Poll(out var err);
                if (st == FinishState.Done)
                {
                    Status = ExportStatus.Done;
                    ReleaseEffect();   // 板は Preview として残す（Dispose で消える）
                    Debug.Log($"[VideoExporter] frames={TotalFrames} 秒={Duration:F1}");
                }
                else if (st == FinishState.Failed) Fail(err == ExportError.None ? ExportError.WriteFailed : err);
                return;
            }

            for (int n = 0; n < MaxPerStep && rendered < TotalFrames && pending.Count < MaxInFlight; n++)
                RenderFrame(rendered++);

            while (pending.Count > 0)
            {
                var (frame, req) = pending.Peek();
                if (!req.done) break;
                if (req.hasError) { Fail(ExportError.WriteFailed); return; }
                var r = writer.Append(req.GetData<byte>(), frame);
                if (r == WriteResult.Busy) break;
                if (r == WriteResult.Failed)
                {
                    writer.Poll(out var err);
                    Fail(err == ExportError.None ? ExportError.WriteFailed : err);
                    return;
                }
                pending.Dequeue();
                appended++;
            }

            if (appended == TotalFrames)
            {
                writer.Finish();
                finishing = true;
            }
        }

        void RenderFrame(int i)
        {
            effect.Evaluate(ExportPlan.TimeOf(i, Duration));
            effect.Camera.Render();
            if (mark != null) DrawMark();
            pending.Enqueue((i, AsyncGPUReadback.Request(rt, 0, TextureFormat.BGRA32)));
        }

        // 右下に印を重ねる（板の上に直接、画素の座標で）
        void DrawMark()
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.PushMatrix();
            // DrawTexture は上から下へ数える座標で描く（下から数えると絵が上下さかさまになる）
            GL.LoadPixelMatrix(0, ExportPlan.Width, ExportPlan.Height, 0);
            var r = ExportPlan.WatermarkRect(mark.width, mark.height);
            r.y = ExportPlan.Height - r.y - r.height;
            // DrawTexture の色は 0.5 がそのまま（2倍される）。不透明度 0.9
            Graphics.DrawTexture(r, mark, new Rect(0, 0, 1, 1), 0, 0, 0, 0, new Color(0.5f, 0.5f, 0.5f, 0.45f));
            GL.PopMatrix();
            RenderTexture.active = prev;
        }

        /// <summary>やめる：作りかけを消す</summary>
        public void Cancel()
        {
            if (Status != ExportStatus.Running) return;
            Status = ExportStatus.Cancelled;
            if (started) writer.Cancel();
            pending.Clear();
            ReleaseEffect();
            ReleaseRt();
        }

        ExportError Fail(ExportError e)
        {
            Error = e;
            Status = ExportStatus.Failed;
            writer.Cancel();
            pending.Clear();
            ReleaseEffect();
            ReleaseRt();
            Debug.LogWarning("[VideoExporter] 失敗 " + e);
            return e;
        }

        /// <summary>演出・板を片付ける（書いている途中ならやめる。何度呼んでもよい）</summary>
        public void Dispose()
        {
            Cancel();
            ReleaseEffect();
            ReleaseRt();
        }

        void ReleaseEffect()
        {
            effect?.Dispose();
            effect = null;
        }

        void ReleaseRt()
        {
            if (rt == null) return;
            rt.Release();
            if (Application.isPlaying) UnityEngine.Object.Destroy(rt); else UnityEngine.Object.DestroyImmediate(rt);
            rt = null;
        }
    }
}
