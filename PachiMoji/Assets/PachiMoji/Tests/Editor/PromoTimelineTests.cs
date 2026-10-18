using NUnit.Framework;

namespace PachiMoji.Tests
{
    /// <summary>昇格の範囲から時刻の並びを作る部分（impactEnd = 10 のときの値で確かめる）</summary>
    public class PromoTimelineTests
    {
        const float Inf = float.PositiveInfinity;

        static void Same(float expected, float actual, string what)
        {
            if (float.IsPositiveInfinity(expected)) Assert.IsTrue(float.IsPositiveInfinity(actual), what + " は起きないはず: " + actual);
            else Assert.AreEqual(expected, actual, 1e-4f, what);
        }

        static void Times(PromoTimes p, float gold, float slash, float rainbow, float spin, float end)
        {
            Same(gold, p.GoldAt, "GoldAt");
            Same(slash, p.SlashAt, "SlashAt");
            Same(rainbow, p.RainbowAt, "RainbowAt");
            Same(spin, p.SpinAt, "SpinAt");
            Same(end, p.End, "End");
        }

        [Test] public void 銀だけ() => Times(PromoTimeline.Plan(10, Tier.Silver, Tier.Silver), Inf, Inf, Inf, Inf, 11.8f);
        [Test] public void 銀から金() => Times(PromoTimeline.Plan(10, Tier.Silver, Tier.Gold), 11.3f, Inf, Inf, Inf, 13.22f);
        [Test] public void 銀から虹_今の流れと同じ() => Times(PromoTimeline.Plan(10, Tier.Silver, Tier.Rainbow), 11.3f, 13.12f, 14.47f, 14.47f, 18.12f);
        [Test] public void 金だけ() => Times(PromoTimeline.Plan(10, Tier.Gold, Tier.Gold), Inf, Inf, Inf, Inf, 11.8f);
        [Test] public void 金から虹() => Times(PromoTimeline.Plan(10, Tier.Gold, Tier.Rainbow), Inf, 11.7f, 13.05f, 13.05f, 16.7f);
        [Test] public void 虹だけ_着地のあとに回る() => Times(PromoTimeline.Plan(10, Tier.Rainbow, Tier.Rainbow), Inf, Inf, Inf, 10.5f, 14.15f);

        [Test]
        public void はじまりがおわりより上なら_はじまりだけにする()
            => Times(PromoTimeline.Plan(10, Tier.Rainbow, Tier.Silver), Inf, Inf, Inf, 10.5f, 14.15f);

        // 「定時／退社」の ImpactEnd は 7.59 秒
        [Test]
        public void 長さの目安()
        {
            Assert.That(PromoTimeline.Plan(7.59f, Tier.Silver, Tier.Silver).End, Is.InRange(8.5f, 10f));
            Assert.That(PromoTimeline.Plan(7.59f, Tier.Silver, Tier.Rainbow).End, Is.InRange(14.5f, 16.5f));
        }

        [Test]
        public void どの範囲でも時刻は前から順に並ぶ()
        {
            for (int a = 0; a < 3; a++)
                for (int b = a; b < 3; b++)
                {
                    var p = PromoTimeline.Plan(10, (Tier)a, (Tier)b);
                    float prev = 10;
                    foreach (var x in new[] { p.GoldAt, p.SlashAt, p.RainbowAt, p.SpinAt, p.End })
                    {
                        if (float.IsPositiveInfinity(x)) continue;
                        Assert.That(x, Is.GreaterThanOrEqualTo(prev), $"{(Tier)a}→{(Tier)b}");
                        prev = x;
                    }
                    Assert.That(p.End, Is.GreaterThan(10f));
                    Assert.IsFalse(float.IsPositiveInfinity(p.End));
                }
        }
    }
}
