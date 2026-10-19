using UnityEngine;

namespace PachiMoji
{
    /// <summary>動画の書き出しの決まり：大きさ・コマ数・時刻・進み具合・要る空き容量・印の位置</summary>
    public static class ExportPlan
    {
        public const int Width = 1080, Height = 1920, Fps = 30;
        public const long BitRate = 12_000_000;   // 1秒あたりの量（ビット）。細かく動く背景でも崩れないよう多めに

        /// <summary>0秒から終わりの時刻ちょうどまでのコマ数（割り切れない長さでも、最後に終わりの時刻のコマを入れる）</summary>
        public static int FrameCount(float duration) => Mathf.CeilToInt(duration * Fps - 1e-3f) + 1;

        /// <summary>そのコマの時刻（終わりの時刻を超えない）</summary>
        public static float TimeOf(int frame, float duration) => Mathf.Min(frame / (float)Fps, duration);

        /// <summary>進み具合（％）。全部渡し終えても、動画の仕上げが終わるまでは 99</summary>
        public static int Percent(int appended, int total, bool finished)
        {
            if (finished) return 100;
            if (total <= 0) return 0;
            return Mathf.Min(99, appended * 100 / total);
        }

        /// <summary>書き出しに要る空き容量（見積もりの 1.5 倍と、ゆとり 50MB）</summary>
        public static long NeedBytes(float duration) => (long)(duration * BitRate / 8 * 1.5) + 50L * 1024 * 1024;

        /// <summary>印を置く場所（左下原点の画素）。幅は動画の 3 割、右下に幅の 3.5% の余白</summary>
        public static Rect WatermarkRect(int texW, int texH)
        {
            float w = Width * 0.30f, h = w * texH / texW, m = Width * 0.035f;
            return new Rect(Width - m - w, m, w, h);
        }
    }

    /// <summary>書き出し・保存で起きること</summary>
    public enum ExportError { None, NoSpace, CannotOpen, WriteFailed, Interrupted, PhotosDenied, SaveFailed }

    public static class ExportRules
    {
        /// <summary>何が起きたかと直し方</summary>
        public static string Message(ExportError e) => e switch
        {
            ExportError.NoSpace => "空き容量が足りません。写真やアプリを整理してから、もう一度「書き出す」を押してください",
            ExportError.CannotOpen or ExportError.WriteFailed => "動画を作れませんでした。アプリを開き直して、もう一度「書き出す」を押してください",
            ExportError.Interrupted => "書き出しを中断しました。もう一度「書き出す」を押してください",
            ExportError.PhotosDenied => "写真への保存が許可されていません。設定アプリ →「脳汁メーカー」→「写真」で「追加のみ」を選んでください",
            ExportError.SaveFailed => "写真に保存できませんでした。空き容量を確かめて、もう一度「写真に保存」を押してください",
            _ => "",
        };
    }
}
