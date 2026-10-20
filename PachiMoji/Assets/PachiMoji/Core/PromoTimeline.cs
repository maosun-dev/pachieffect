using System.Collections.Generic;

namespace PachiMoji
{
    /// <summary>色の格（パチンコの期待度の順）：銀 → 青 → 緑 → 紫 → 赤 → 金 → 虹</summary>
    public enum Tier { Silver = 0, Blue = 1, Green = 2, Purple = 3, Red = 4, Gold = 5, Rainbow = 6 }

    /// <summary>昇格の1段：白く飛んで（WhiteAt）、少しあとに次の色へ切り替わる（SnapAt）</summary>
    public readonly struct PromoStep
    {
        public readonly Tier To;
        public readonly float WhiteAt;
        public float SnapAt => WhiteAt + PromoTimeline.WhiteGold;
        public PromoStep(Tier to, float whiteAt) { To = to; WhiteAt = whiteAt; }
    }

    /// <summary>昇格の色の組み合わせから決まる、ドンのあとの時刻の並び。起きない出来事は +∞</summary>
    public class PromoPlan
    {
        public Tier Land { get; }            // 着地の色（組み合わせの最初の色）
        public Tier Last { get; }            // 最後の色
        public PromoStep[] Steps { get; }    // 虹の特別な形の手前までの昇格
        public float SlashAt { get; }        // 紫の光線で切り裂かれる時刻
        public float RainbowAt { get; }      // 灰色のタメのあと、虹に切り替わる時刻
        public float SpinAt { get; }         // 虹の1回転目の始まり
        public float End { get; }            // 演出の終わり

        public PromoPlan(Tier land, Tier last, PromoStep[] steps, float slash, float rainbow, float spin, float end)
        {
            Land = land; Last = last; Steps = steps; SlashAt = slash; RainbowAt = rainbow; SpinAt = spin; End = end;
        }

        /// <summary>その時刻までに切り替わった段の数（白く飛んでいるあいだは数えない）</summary>
        public int SnappedCount(float t)
        {
            int n = 0;
            while (n < Steps.Length && t >= Steps[n].SnapAt) n++;
            return n;
        }

        /// <summary>その時刻の色（虹に切り替わったら虹）</summary>
        public Tier TierAt(float t)
        {
            if (t >= RainbowAt) return Tier.Rainbow;
            int n = SnappedCount(t);
            return n == 0 ? Land : Steps[n - 1].To;
        }
    }

    /// <summary>チラ見せの1文字：始まる時刻と長さ</summary>
    public readonly struct TeaseSlot
    {
        public readonly float At, Len;
        public TeaseSlot(float at, float len) { At = at; Len = len; }
    }

    /// <summary>チラ見せの並び：1文字ずつ、だんだん速く。あいだに暗転</summary>
    public static class TeasePlan
    {
        public const int MaxChars = 12;

        /// <param name="setting">設定の文字数（0〜12）</param>
        /// <param name="wordChars">言葉の文字数</param>
        public static TeaseSlot[] Plan(float start, int setting, int wordChars)
        {
            int n = System.Math.Max(0, System.Math.Min(System.Math.Min(setting, wordChars), MaxChars));
            var slots = new TeaseSlot[n];
            float cur = start;
            for (int k = 0; k < n; k++)
            {
                if (k > 0) cur += System.Math.Max(0.2f, 0.6f - 0.1f * k);   // あいだの暗転：0.5 → 0.4 → 0.3 → 0.2
                // 1文字の長さ：0.9 → 0.75 → 0.6 → 0.55 → 0.5 → 0.45
                float len = k <= 3 ? System.Math.Max(0.55f, 0.9f - 0.15f * k) : System.Math.Max(0.45f, 0.55f - 0.05f * (k - 3));
                slots[k] = new TeaseSlot(cur, len);
                cur += len;
            }
            return slots;
        }

        /// <summary>光の放射の始まり（最後の文字の終わり。0 文字なら start）</summary>
        public static float End(float start, TeaseSlot[] slots) =>
            slots.Length == 0 ? start : slots[slots.Length - 1].At + slots[slots.Length - 1].Len;
    }

    /// <summary>昇格の色の組み合わせから、ドンのあとの時刻の並びを決める</summary>
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

        /// <summary>snapped 段目の切り替えのあと、次の出来事（白飛び・切り裂き）まで。段が進むごとに 0.85 倍、最短 0.7 秒。0 段（着地のあと）は GoldHold</summary>
        public static float HoldAfter(int snapped) =>
            snapped <= 0 ? GoldHold : System.Math.Max(0.7f, GoldHold * (float)System.Math.Pow(0.85, snapped - 1));

        /// <summary>
        /// 昇格の色の組み合わせ（格の順、1色以上）から時刻を並べる。最初の色で着地し、順に白飛び→切り替え。
        /// 最後が虹で2色以上なら、虹の手前で紫の光線の切り裂き → 灰色のタメ → 虹 → 回転。虹だけなら着地のあと回転
        /// </summary>
        public static PromoPlan Plan(float impactEnd, IReadOnlyList<Tier> chain)
        {
            const float Inf = float.PositiveInfinity;
            Tier land = chain[0], last = chain[chain.Count - 1];
            bool slashRainbow = last == Tier.Rainbow && chain.Count >= 2;
            int stepEnd = slashRainbow ? chain.Count - 1 : chain.Count;   // 白飛びで切り替える段の終わり（虹の特別な形は除く）
            var steps = new List<PromoStep>();
            float prev = impactEnd;   // 直前の出来事（着地・切り替え）の時刻
            for (int i = 1; i < stepEnd; i++)
            {
                float white = steps.Count == 0 ? impactEnd + GoldDelay : prev + HoldAfter(steps.Count);
                steps.Add(new PromoStep(chain[i], white));
                prev = steps[steps.Count - 1].SnapAt;
            }
            float slash = Inf, rainbow = Inf, spin = Inf, end = prev;
            if (slashRainbow)
            {
                slash = prev + HoldAfter(steps.Count);
                rainbow = slash + SlashDraw + GrayHold;
                spin = rainbow;
            }
            else if (land == Tier.Rainbow) spin = impactEnd + LandSpinDelay;
            if (!float.IsInfinity(spin)) end = spin + 2 * Spin + SpinRest;
            return new PromoPlan(land, last, steps.ToArray(), slash, rainbow, spin, end + Linger);
        }
    }
}
