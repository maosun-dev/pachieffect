using System;
using System.IO;
using Unity.Collections;
using UnityEngine;

namespace PachiMoji
{
    /// <summary>Windows・Editor 用の書き手：コマを PNG にしてフォルダに並べる（path はフォルダ）</summary>
    public class PngVideoWriter : IVideoWriter
    {
        string dir;
        Texture2D tex;

        public virtual long FreeBytes(string d) => new DriveInfo(Path.GetPathRoot(Path.GetFullPath(d))).AvailableFreeSpace;

        public ExportError Begin(string path, int w, int h, int fps)
        {
            dir = path;
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return ExportError.CannotOpen;
            }
            tex = new Texture2D(w, h, TextureFormat.BGRA32, false);
            return ExportError.None;
        }

        public virtual WriteResult Append(NativeArray<byte> bgra, int frame)
        {
            try
            {
                tex.LoadRawTextureData(bgra);
                File.WriteAllBytes(Path.Combine(dir, $"f{frame:D4}.png"), tex.EncodeToPNG());
                return WriteResult.Ok;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return WriteResult.Failed;
            }
        }

        public void Finish() => DestroyTex();

        public FinishState Poll(out ExportError error)
        {
            error = ExportError.None;
            return FinishState.Done;
        }

        public void Cancel()
        {
            DestroyTex();
            if (dir != null && Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        void DestroyTex()
        {
            if (tex == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(tex); else UnityEngine.Object.DestroyImmediate(tex);
            tex = null;
        }
    }
}
