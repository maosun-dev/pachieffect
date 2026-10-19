using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PachiMoji
{
    /// <summary>書き出した動画を写真に保存する・共有画面を出す（iPhone は PachiVideo.mm、ほかはログだけ）</summary>
    public static class VideoShare
    {
        [DllImport("__Internal")] static extern void PachiVideo_SaveToPhotos(string path);
        [DllImport("__Internal")] static extern void PachiVideo_Share(string path);

        static Action<ExportError> pending;

        static bool OnIos => Application.platform == RuntimePlatform.IPhonePlayer;

        /// <summary>写真に保存。初回だけ許可を聞く。終わったら done（None＝保存できた）</summary>
        public static void SaveToPhotos(string path, Action<ExportError> done)
        {
            if (!OnIos)
            {
                Debug.Log("[VideoShare] 写真に保存（iPhone だけで動く） " + path);
                done?.Invoke(ExportError.None);
                return;
            }
            if (GameObject.Find(VideoShareBridge.Name) == null)
            {
                var go = new GameObject(VideoShareBridge.Name);
                go.AddComponent<VideoShareBridge>();
                UnityEngine.Object.DontDestroyOnLoad(go);
            }
            pending = done;
            PachiVideo_SaveToPhotos(path);
        }

        /// <summary>iPhone の共有画面（LINE・X・TikTok など）を出す</summary>
        public static void Share(string path)
        {
            if (!OnIos)
            {
                Debug.Log("[VideoShare] 共有（iPhone だけで動く） " + path);
                return;
            }
            PachiVideo_Share(path);
        }

        /// <summary>PachiVideo.mm からの返事（"ok"／"denied"／"failed"）</summary>
        internal static void Reply(string result)
        {
            var cb = pending;
            pending = null;
            cb?.Invoke(result == "ok" ? ExportError.None : result == "denied" ? ExportError.PhotosDenied : ExportError.SaveFailed);
        }
    }
}
