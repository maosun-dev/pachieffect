using System;
using System.IO;
using UnityEngine;

namespace PachiMoji
{
    /// <summary>
    /// 端末の中に写した写真（激アツの帯の背景）。写真は外へ送らず、persistentDataPath の中だけで使う。
    /// 長い辺は MaxSide 以下にする（iPhone の部品も写すときに縮めるが、読むときにも念のため縮める）
    /// </summary>
    public static class PhotoStore
    {
        public const int MaxSide = 2048;

        /// <summary>新しい写真の置き場所（前の写真は、新しい写真を使い始めてから消す）</summary>
        public static string PathFor() =>
            Path.Combine(Application.persistentDataPath, $"photo_{DateTime.Now:yyyyMMddHHmmssfff}.jpg");

        /// <summary>読む。無い・壊れていれば null</summary>
        public static Texture2D Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (Exception) { return null; }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                Kill(tex);
                return null;
            }
            tex = Shrink(tex, MaxSide);
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        /// <summary>画像のファイルを縮めて JPEG で写す（iPhone 以外で写真を選ぶとき）</summary>
        public static bool Import(string src, string dst)
        {
            var tex = Load(src);
            if (tex == null) return false;
            try
            {
                File.WriteAllBytes(dst, tex.EncodeToJPG(90));
                return true;
            }
            catch (Exception) { return false; }
            finally { Kill(tex); }
        }

        public static void Delete(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception e) { Debug.LogWarning("[PhotoStore] 消せなかった " + e.Message); }
        }

        // 長い辺が max を超えていれば、GPU で縮めて作り直す
        static Texture2D Shrink(Texture2D tex, int max)
        {
            int side = Mathf.Max(tex.width, tex.height);
            if (side <= max) return tex;
            float k = (float)max / side;
            int w = Mathf.Max(1, Mathf.RoundToInt(tex.width * k)), h = Mathf.Max(1, Mathf.RoundToInt(tex.height * k));
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var small = new Texture2D(w, h, TextureFormat.RGBA32, false);
            small.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            small.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Kill(tex);
            return small;
        }

        static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o); else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
