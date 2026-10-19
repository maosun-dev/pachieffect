using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace PachiMoji
{
    /// <summary>
    /// iPhone 用の書き手：自前の部品 Plugins/iOS/PachiVideo.mm（AVAssetWriter）で mp4 にする。
    /// 部品は iPhone の組み立てにだけ入るので、ほかの環境で呼ぶと落ちる（PachiApp が iPhone のときだけ使う）
    /// </summary>
    public class IosVideoWriter : IVideoWriter
    {
        [DllImport("__Internal")] static extern long PachiVideo_FreeBytes(string dir);
        [DllImport("__Internal")] static extern int PachiVideo_Begin(string path, int w, int h, int fps, int bitRate);
        [DllImport("__Internal")] static extern unsafe int PachiVideo_Append(void* bgra, int frame);
        [DllImport("__Internal")] static extern void PachiVideo_Finish();
        [DllImport("__Internal")] static extern int PachiVideo_Poll();
        [DllImport("__Internal")] static extern int PachiVideo_LastError();
        [DllImport("__Internal")] static extern void PachiVideo_Cancel();

        public long FreeBytes(string dir) => PachiVideo_FreeBytes(dir);

        public ExportError Begin(string path, int w, int h, int fps) =>
            (ExportError)PachiVideo_Begin(path, w, h, fps, (int)ExportPlan.BitRate);

        // コマの中身は写さず、場所だけを渡す（1コマ 8MB あるため）
        public unsafe WriteResult Append(NativeArray<byte> bgra, int frame) =>
            (WriteResult)PachiVideo_Append(NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bgra), frame);

        public void Finish() => PachiVideo_Finish();

        public FinishState Poll(out ExportError error)
        {
            var s = (FinishState)PachiVideo_Poll();
            error = (ExportError)PachiVideo_LastError();
            return s;
        }

        public void Cancel() => PachiVideo_Cancel();
    }
}
