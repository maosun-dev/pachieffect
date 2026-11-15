using System;
using UnityEngine;
using UnityEngine.Video;

namespace PachiMoji
{
    /// <summary>
    /// 激アツの帯の背景の動画（Unity の VideoPlayer を板に描く）。
    /// 再生（Live）は流しっぱなしにして、ずれたときだけ合わせる。
    /// 書き出し（Seek）は止めて1コマずつ位置を合わせ、そのコマが描けたら Ready（帯と同じ時刻の絵にするため）
    /// </summary>
    public class ObiVideo
    {
        readonly GameObject go;
        readonly VideoPlayer vp;
        RenderTexture rt;
        float start, end;
        long wantFrame = -1, readyFrame = -1;
        int waits;

        public bool Prepared { get; private set; }
        public bool Failed { get; private set; }
        public float Length { get; private set; }
        public string Path { get; }

        /// <summary>準備ができたとき（描く板と、その横÷縦）</summary>
        public event Action<Texture, float> OnReady;

        public ObiVideo(string path, float start, float end, Transform parent)
        {
            Path = path;
            this.start = start; this.end = end;
            go = new GameObject("ObiVideo");
            go.transform.SetParent(parent, false);
            vp = go.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.source = VideoSource.Url;
            // iPhone でも確実に読めるよう、ファイルの場所は file:// の形で渡す
            vp.url = new System.Uri(path).AbsoluteUri;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.isLooping = false;
            vp.skipOnDrop = true;
            vp.waitForFirstFrame = true;
            vp.audioOutputMode = VideoAudioOutputMode.Direct;
            vp.sendFrameReadyEvents = true;
            vp.frameReady += (p, f) => readyFrame = f;
            vp.seekCompleted += p => { if (!p.isPlaying) readyFrame = wantFrame; };
            vp.errorReceived += (p, m) => { Failed = true; Debug.LogWarning("[ObiVideo] " + m); };
            vp.prepareCompleted += Prepare;
            vp.Prepare();
        }

        void Prepare(VideoPlayer p)
        {
            int w = Mathf.Max(2, (int)p.width), h = Mathf.Max(2, (int)p.height);
            rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "ObiVideo" };
            rt.Create();
            p.targetTexture = rt;
            if (p.audioTrackCount > 0) p.EnableAudioTrack(0, true);
            Length = (float)p.length;
            Prepared = true;
            OnReady?.Invoke(rt, (float)w / h);
        }

        public void SetRange(float s, float e) { start = s; end = e; }

        /// <summary>再生：区間のはじめから t 秒の位置を流す。ずれが大きいときだけ合わせる</summary>
        public void Live(float t, bool sound)
        {
            if (!Prepared) return;
            Mute(!sound);
            double want = start + t;
            if (!vp.isPlaying)
            {
                vp.time = want;
                vp.Play();
            }
            else if (Math.Abs(vp.time - want) > 0.25) vp.time = want;
        }

        /// <summary>書き出し：区間のはじめから t 秒のコマに合わせる。そのコマが描けたら true（待ちすぎたら先へ進む）</summary>
        public bool Seek(float t)
        {
            if (Failed) return true;
            if (!Prepared) return false;
            Mute(true);
            if (vp.isPlaying) vp.Pause();
            long last = Math.Max(0, (long)vp.frameCount - 1);
            long f = Math.Min(last, Math.Max(0, (long)Math.Round((start + t) * vp.frameRate)));
            if (f != wantFrame)
            {
                wantFrame = f;
                readyFrame = -1;
                waits = 0;
                vp.frame = f;
            }
            waits++;
            return readyFrame == f || waits > 90;
        }

        /// <summary>止める（待ちの画面で音を消すときなど）</summary>
        public void Stop()
        {
            if (vp != null && vp.isPlaying) vp.Pause();
        }

        void Mute(bool on)
        {
            if (vp.audioOutputMode == VideoAudioOutputMode.Direct && vp.audioTrackCount > 0) vp.SetDirectAudioMute(0, on);
        }

        public void Dispose()
        {
            if (vp != null) vp.Stop();
            Kill(go);
            if (rt != null) { rt.Release(); Kill(rt); rt = null; }
        }

        static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o); else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
