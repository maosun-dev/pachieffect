using UnityEngine;

namespace PachiMoji
{
    /// <summary>
    /// 帯の輪の形：半径 R の円を Tilt 度だけ奥へ倒し（上半分が奥 z&gt;0）、画面の奥行きの軸で Roll 度まわし、Center を足す
    /// </summary>
    public readonly struct ObiRing
    {
        public readonly float R, Tilt, Roll;
        public readonly Vector2 Center;

        public ObiRing(float r, float tilt, float roll, Vector2 center) { R = r; Tilt = tilt; Roll = roll; Center = center; }

        public ObiRing WithCenter(Vector2 c) => new ObiRing(R, Tilt, Roll, c);

        public Vector3 Point(float th)
        {
            float a = Tilt * Mathf.Deg2Rad;
            var p = new Vector3(Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(a), Mathf.Sin(th) * Mathf.Cos(a)) * R;
            return Quaternion.Euler(0, 0, Roll) * p + (Vector3)Center;
        }
    }

    /// <summary>
    /// 2つ目の演出「激アツの帯」の形と流れの計算。
    /// 上の帯は左へ、下の帯は右へ流れ、10 秒でちょうど元の並びに戻る（書き出した動画をくり返してもつなぎ目が出ない）
    /// </summary>
    public static class ObiPlan
    {
        public const float Duration = 10f;
        public const float ViewH = 10f;               // 基準の画面で、z=0 の面に見える高さ
        public const float Fov = 32f;                 // 基準の画面の縦の画角
        public const float BaseAspect = 9f / 19.5f;   // 基準の画面（横÷縦）
        public const float LetterH = 1.6f;            // 文字の高さ（炎の帯の高さいっぱい）
        public const float Squeeze = 0.6f;            // 文字の横の縮め（縦長に細く。参考の帯の文字）
        public const float Slant = 0.2f;              // 文字の傾き（上へ行くほど進む向きへずらす。斜めの字）
        public const float BandW = 1.35f;
        public const float BaseSpeed = 1.6f;          // 速さの目安（1 秒あたり）
        public const float Spacing = LetterH * 0.53f; // 文字の間隔（縦長に細くした文字が詰まって、少し重なって並ぶ）

        // 帯の位置（ユーザーと決めた形。上下は別の輪から取る。つなぎ目は画面の外）
        public static readonly ObiRing Top = new ObiRing(6.2f, 32, -32, new Vector2(0, 1.25f));
        public static readonly ObiRing Bottom = new ObiRing(6.2f, 24, -32, new Vector2(0, 0.2f));

        // 横幅を基準の画面と同じに保つので、縦が短い画面では見える高さが減る。その半分だけ帯を内へ寄せる
        static float Shrink(float aspect) => ViewH * (1 - BaseAspect / aspect) / 2;

        public static readonly float CamDist = ViewH * 0.5f / Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);   // カメラから z=0 の面まで

        // 帯の一番外側（上の帯の一番上は奥 z=+R cos、下の帯の一番下は手前 z=-R cos）が、画面の上で Shrink だけ内へ寄るように動かす。
        // 遠近で縮む・大きく見える分を足す（奥は多めに、手前は少なめに動かす）
        static float ShiftAt(float z, float shrink) => shrink * (CamDist + z) / CamDist;
        static float FarZ(ObiRing r) => r.R * Mathf.Cos(r.Tilt * Mathf.Deg2Rad);

        public static ObiRing TopFor(float aspect) => Top.WithCenter(Top.Center - new Vector2(0, ShiftAt(FarZ(Top), Shrink(aspect))));
        public static ObiRing BottomFor(float aspect) => Bottom.WithCenter(Bottom.Center + new Vector2(0, ShiftAt(-FarZ(Bottom), Shrink(aspect))));

        /// <summary>帯の点の並び（画面の左→右）。上は θ=π→0、下は θ=π→2π</summary>
        public static Vector3[] ArcPoints(ObiRing ring, bool top, int n)
        {
            var pts = new Vector3[n + 1];
            float end = top ? 0 : 2 * Mathf.PI;
            for (int i = 0; i <= n; i++) pts[i] = ring.Point(Mathf.Lerp(Mathf.PI, end, i / (float)n));
            return pts;
        }

        /// <summary>帯1本に並べる枠の数（言葉＋区切り1つの倍数。帯の長さより長くなる）</summary>
        public static int LetterSlots(float arcLength, int wordChars)
        {
            int unit = Mathf.Max(1, wordChars) + 1;
            int n = Mathf.CeilToInt(arcLength / Spacing) + 1;
            return Mathf.CeilToInt(n / (float)unit) * unit;
        }

        /// <summary>10 秒でちょうど何周かする速さ（目安に近く、最低1周）</summary>
        public static float LoopSpeed(float loopLength) =>
            loopLength * Mathf.Max(1, Mathf.Round(BaseSpeed * Duration / loopLength)) / Duration;

        /// <summary>枠の帯の上の位置（0〜arcLength の外は見えない）。dir：-1 左へ・+1 右へ</summary>
        public static float SlotPos(float s0, int dir, float speed, float t, float loopLength, float arcLength) =>
            Mathf.Repeat(s0 + dir * speed * t, loopLength) - (loopLength - arcLength) / 2;
    }
}
