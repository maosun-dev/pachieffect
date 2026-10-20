namespace PachiMoji
{
    /// <summary>色の格（パチンコの期待度の順）：銀 → 青 → 緑 → 紫 → 赤 → 金 → 虹</summary>
    public enum Tier { Silver = 0, Blue = 1, Green = 2, Purple = 3, Red = 4, Gold = 5, Rainbow = 6 }

    /// <summary>ドンのあとの出来事の時刻（秒）。起きない出来事は +∞（比較 t >= X が自然に偽になる）</summary>
    public readonly struct PromoTimes
    {
        public readonly float GoldAt;     // 金への白飛びの始まり（色が切り替わるのは +WhiteGold）
        public readonly float SlashAt;    // 紫の光線で切り裂かれる時刻
        public readonly float RainbowAt;  // 灰色のタメのあと、虹に切り替わる時刻
        public readonly float SpinAt;     // 1回転目の始まり
        public readonly float End;        // 演出の終わり

        public PromoTimes(float goldAt, float slashAt, float rainbowAt, float spinAt, float end)
        {
            GoldAt = goldAt; SlashAt = slashAt; RainbowAt = rainbowAt; SpinAt = spinAt; End = end;
        }
    }

    /// <summary>昇格の範囲（はじまりの色・おわりの色）から、ドンのあとの時刻の並びを決める</summary>
    public static class PromoTimeline
    {
        public const float GoldDelay = 1.3f;      // ドンが動き出してから、金への白飛びまで
        public const float WhiteGold = 0.12f;     // 金に変わる前に白く飛ぶ長さ
        public const float GoldHold = 1.7f;       // 金になってから切り裂かれるまで
        public const float SlashDraw = 0.15f;     // 切り裂きの光線が画面を横切る長さ
        public const float GrayHold = 1.2f;       // 切り裂かれて灰色に固まったままタメる長さ
        public const float Spin = 0.7f;           // 虹になったあとの1回転の長さ
        public const float SpinRest = 0.45f;      // 1回転と1回転のあいだに止まる長さ
        public const float LandSpinDelay = 0.5f;  // 虹で着地したとき、回り始めるまで
        public const float Linger = 1.8f;         // 最後の出来事のあとの余韻

        /// <param name="impactEnd">ドンのヒットストップが明けて動き出す時刻</param>
        public static PromoTimes Plan(float impactEnd, Tier from, Tier to)
        {
            const float Inf = float.PositiveInfinity;
            if (to < from) to = from;
            float gold = Inf, slash = Inf, rainbow = Inf, spin = Inf;
            float last = impactEnd;   // 最後の出来事の時刻

            // 金に切り替わる時刻（銀はじまりは昇格、金はじまりは着地のとき）
            float goldSnap = Inf;
            if (from == Tier.Silver && to >= Tier.Gold)
            {
                gold = impactEnd + GoldDelay;
                goldSnap = gold + WhiteGold;
                last = goldSnap;
            }
            else if (from == Tier.Gold) goldSnap = impactEnd;

            if (to == Tier.Rainbow)
            {
                if (from == Tier.Rainbow) spin = impactEnd + LandSpinDelay;
                else
                {
                    slash = goldSnap + GoldHold;
                    rainbow = slash + SlashDraw + GrayHold;
                    spin = rainbow;
                }
                last = spin + 2 * Spin + SpinRest;
            }
            return new PromoTimes(gold, slash, rainbow, spin, last + Linger);
        }
    }
}
