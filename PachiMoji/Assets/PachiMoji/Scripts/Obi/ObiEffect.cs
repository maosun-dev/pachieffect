using UnityEngine;

namespace PachiMoji
{
    /// <summary>2つ目の演出「激アツの帯」：黒（または写真）の上を、言葉をくり返した金の帯が流れ続ける（中身は ObiStage）</summary>
    public class ObiEffect : IPachiEffect
    {
        ObiStage stage;
        Texture2D photo;
        ObiVideo video;
        float videoStart, videoEnd;
        bool exporting, ready = true;

        /// <summary>再生の音を出すか（待ちの画面では消す）</summary>
        public bool Sound { get; set; }
        /// <summary>背景の動画（無ければ null）と、使う区間</summary>
        public string VideoPath => video?.Path;
        public float VideoStart => videoStart;
        public bool HasVideo => video != null;
        /// <summary>動画の全体の長さ（準備ができる前は 0）</summary>
        public float VideoLength => video != null && video.Prepared ? video.Length : 0;

        /// <summary>設定に写真があるのに読めなかった（消えていた・壊れていた）。PachiApp が設定から外す</summary>
        public bool PhotoMissing { get; private set; }

        public void Build(string[] lines, float aspect, EffectSettings settings)
        {
            bool isVideo = settings != null && !settings.ObiBlueBack && MediaKind.IsVideo(settings.PhotoPath);
            if (isVideo)
            {
                PhotoMissing = !System.IO.File.Exists(settings.PhotoPath);
                videoStart = settings.VideoStart; videoEnd = settings.VideoEnd;
            }
            else photo = LoadPhoto(settings);
            stage = ObiStage.Build(lines, aspect, settings, photo, isVideo && !PhotoMissing);
            if (isVideo && !PhotoMissing)
            {
                video = new ObiVideo(settings.PhotoPath, videoStart, videoEnd, stage.cam.transform.parent);
                video.OnReady += (tex, a) => stage?.SetMedia(tex, a);
            }
        }

        /// <summary>動画の使う区間を変える（区間を合わせるあいだ、組み立て直さずに）</summary>
        public void SetVideoRange(float s, float e)
        {
            videoStart = s; videoEnd = e;
            video?.SetRange(s, e);
        }

        // 写真の読み込み。設定に写真があるのに読めなければ PhotoMissing（黒で組む）
        Texture2D LoadPhoto(EffectSettings s)
        {
            if (s == null || string.IsNullOrEmpty(s.PhotoPath)) { PhotoMissing = false; return null; }
            var tex = PhotoStore.Load(s.PhotoPath);
            PhotoMissing = tex == null;
            return tex;
        }

        /// <summary>長さ：動画があればその区間の長さ（上限なし）、なければ 10 秒</summary>
        public float Duration => video != null && videoEnd > videoStart ? videoEnd - videoStart : ObiPlan.Duration;
        public bool Loops => true;
        public bool Ready => ready;
        public void SetExporting(bool on) { exporting = on; if (on) video?.Stop(); }

        public void Evaluate(float t)
        {
            stage.Evaluate(t);
            if (video == null) { ready = true; return; }
            if (exporting) ready = video.Seek(t);
            else { video.Live(t, Sound); ready = true; }
        }

        public void Idle(float t)
        {
            float d = Duration;
            stage.Evaluate(Mathf.Repeat(t, ObiPlan.Duration));
            if (video != null) video.Live(Mathf.Repeat(t, d), false);
        }
        public Camera Camera => stage.cam;

        /// <summary>写真の位置合わせのあいだ、組み立て直さずに写真だけ動かす</summary>
        public void SetPhotoFit(Vector2 offset, float zoom) => stage?.SetPhotoFit(offset, zoom);

        /// <summary>写真が入っているか・写真の横÷縦</summary>
        public bool HasPhoto => stage != null && stage.HasPhoto;

        /// <summary>列（0＝上・1＝下）を指で合わせるあいだ、組み立て直さずに動かす</summary>
        public void SetLaneFit(int lane, Vector2 offset, float scale) => stage?.SetLaneFit(lane, offset, scale);
        public float UnitsPerScreenHeight => stage != null ? stage.UnitsPerScreenHeight : 1;
        public float PhotoAspect => stage != null ? stage.PhotoAspect : 1;

        public void Dispose()
        {
            video?.Dispose();
            video = null;
            stage?.Dispose();
            stage = null;
            if (photo != null)
            {
                if (Application.isPlaying) Object.Destroy(photo); else Object.DestroyImmediate(photo);
                photo = null;
            }
        }
    }
}
