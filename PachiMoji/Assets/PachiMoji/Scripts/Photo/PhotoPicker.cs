using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PachiMoji
{
    public enum PhotoPickResult { Picked, Cancelled, Failed }

    /// <summary>
    /// 写真を1枚選んで、端末の中に縮めて写す（iPhone は PachiPhoto.mm の PHPicker。写真へのアクセスの許可は聞かない）。
    /// iPhone 以外は、環境変数 PACHI_PHOTO の画像を写す（無ければやめた扱い）
    /// </summary>
    public static class PhotoPicker
    {
        [DllImport("__Internal")] static extern void PachiPhoto_Pick(string outPath, int maxSide);

        static Action<PhotoPickResult, string, float> pending;
        static string pendingPath;

        /// <summary>選び終わったら done（Picked のとき path に写真か動画がある。動画なら長さ（秒）、写真は 0）</summary>
        public static void Pick(Action<PhotoPickResult, string, float> done)
        {
            string path = PhotoStore.PathFor();
            if (Application.platform != RuntimePlatform.IPhonePlayer)
            {
                string src = Environment.GetEnvironmentVariable("PACHI_PHOTO");
                if (string.IsNullOrEmpty(src) || !File.Exists(src)) { done?.Invoke(PhotoPickResult.Cancelled, null, 0); return; }
                if (MediaKind.IsVideo(src))
                {
                    // 手元では変換せずにそのまま写す（長さは分からないので 0＝あとで動画から決める）
                    string v = Path.ChangeExtension(path, Path.GetExtension(src).ToLowerInvariant());
                    File.Copy(src, v, true);
                    done?.Invoke(PhotoPickResult.Picked, v, 0);
                    return;
                }
                bool ok = PhotoStore.Import(src, path);
                done?.Invoke(ok ? PhotoPickResult.Picked : PhotoPickResult.Failed, ok ? path : null, 0);
                return;
            }
            if (GameObject.Find(PhotoPickerBridge.Name) == null)
            {
                var go = new GameObject(PhotoPickerBridge.Name);
                go.AddComponent<PhotoPickerBridge>();
                UnityEngine.Object.DontDestroyOnLoad(go);
            }
            pending = done;
            pendingPath = path;
            PachiPhoto_Pick(path, PhotoStore.MaxSide);
        }

        /// <summary>PachiPhoto.mm からの返事（"ok"／"video:<秒>"／"cancel"／"failed"）。動画は拡張子を .mp4 にした場所にある</summary>
        internal static void Reply(string result)
        {
            var cb = pending; var path = pendingPath;
            pending = null; pendingPath = null;
            float len = 0;
            if (result != null && result.StartsWith("video:"))
            {
                path = Path.ChangeExtension(path, ".mp4");
                float.TryParse(result.Substring(6), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out len);
                result = "ok";
            }
            var r = result == "ok" && File.Exists(path) ? PhotoPickResult.Picked
                  : result == "cancel" ? PhotoPickResult.Cancelled : PhotoPickResult.Failed;
            cb?.Invoke(r, r == PhotoPickResult.Picked ? path : null, len);
        }
    }
}
