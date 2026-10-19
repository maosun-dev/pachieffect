using Unity.Collections;

namespace PachiMoji
{
    public enum WriteResult { Ok, Busy, Failed }
    public enum FinishState { Writing, Done, Failed }

    /// <summary>書き出したコマを受け取って、動画（iPhone）や PNG の並び（Windows）にする書き手</summary>
    public interface IVideoWriter
    {
        long FreeBytes(string dir);                              // その場所の空き容量
        ExportError Begin(string path, int w, int h, int fps);   // 作りかけがあれば消してから始める
        WriteResult Append(NativeArray<byte> bgra, int frame);   // 1コマ（BGRA、下の行から）。Busy は次の Step でもう一度
        void Finish();                                           // 仕上げを始める（終わりは Poll で待つ）
        FinishState Poll(out ExportError error);
        void Cancel();                                           // 止めて、作りかけを消す
    }
}
