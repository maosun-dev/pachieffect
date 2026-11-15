using UnityEngine;

namespace PachiMoji
{
    /// <summary>
    /// 写真を書き出しの枠（9:16）いっぱいに置く計算。単位は枠の幅を 1 とする（枠の高さは 16/9）。
    /// 倍率 1 で枠をちょうど覆い、ずらしは写真が枠を覆い続ける範囲に収める
    /// </summary>
    public static class PhotoFit
    {
        public const float FrameAspect = 9f / 16f;
        public const float MaxZoom = 4;

        /// <summary>写真の幅・高さ（imgAspect は写真の横÷縦、frameAspect は枠の横÷縦。既定は縦の 9:16）</summary>
        public static Vector2 Size(float imgAspect, float zoom, float frameAspect = FrameAspect)
        {
            float s = Mathf.Max(1 / imgAspect, 1 / frameAspect) * ClampZoom(zoom);
            return new Vector2(imgAspect * s, s);
        }

        public static Vector2 ClampOffset(Vector2 offset, float imgAspect, float zoom, float frameAspect = FrameAspect)
        {
            var s = Size(imgAspect, zoom, frameAspect);
            float mx = (s.x - 1) / 2, my = (s.y - 1 / frameAspect) / 2;
            return new Vector2(Mathf.Clamp(offset.x, -mx, mx), Mathf.Clamp(offset.y, -my, my));
        }

        public static float ClampZoom(float z) => Mathf.Clamp(z, 1, MaxZoom);
    }
}
