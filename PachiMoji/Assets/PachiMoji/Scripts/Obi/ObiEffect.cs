using UnityEngine;

namespace PachiMoji
{
    /// <summary>2つ目の演出「激アツの帯」：黒（または写真）の上を、言葉をくり返した金の帯が流れ続ける（中身は ObiStage）</summary>
    public class ObiEffect : IPachiEffect
    {
        ObiStage stage;
        Texture2D photo;

        /// <summary>設定に写真があるのに読めなかった（消えていた・壊れていた）。PachiApp が設定から外す</summary>
        public bool PhotoMissing { get; private set; }

        public void Build(string[] lines, float aspect, EffectSettings settings)
        {
            photo = LoadPhoto(settings);
            stage = ObiStage.Build(lines, aspect, settings, photo);
        }

        // 写真の読み込み。設定に写真があるのに読めなければ PhotoMissing（黒で組む）
        Texture2D LoadPhoto(EffectSettings s)
        {
            if (s == null || string.IsNullOrEmpty(s.PhotoPath)) { PhotoMissing = false; return null; }
            var tex = PhotoStore.Load(s.PhotoPath);
            PhotoMissing = tex == null;
            return tex;
        }

        public float Duration => ObiPlan.Duration;
        public bool Loops => true;
        public void Evaluate(float t) => stage.Evaluate(t);
        public void Idle(float t) => stage.Evaluate(Mathf.Repeat(t, ObiPlan.Duration));
        public Camera Camera => stage.cam;

        /// <summary>写真の位置合わせのあいだ、組み立て直さずに写真だけ動かす</summary>
        public void SetPhotoFit(Vector2 offset, float zoom) => stage?.SetPhotoFit(offset, zoom);

        /// <summary>写真が入っているか・写真の横÷縦</summary>
        public bool HasPhoto => stage != null && stage.HasPhoto;
        public float PhotoAspect => stage != null ? stage.PhotoAspect : 1;

        public void Dispose()
        {
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
