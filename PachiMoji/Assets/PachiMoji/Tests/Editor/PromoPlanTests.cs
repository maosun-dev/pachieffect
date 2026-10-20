using NUnit.Framework;

namespace PachiMoji.Tests
{
    /// <summary>昇格の色の組み合わせから時刻を並べる部分（impactEnd = 10）と、チラ見せの並び</summary>
    public class PromoPlanTests
    {
        const float Inf = float.PositiveInfinity;
        static Tier[] C(params Tier[] t) => t;

        [Test]
        public void 既定は今と同じ時刻()
        {
            var p = PromoTimeline.Plan(10, C(Tier.Silver, Tier.Gold, Tier.Rainbow));
            Assert.AreEqual(1, p.Steps.Length); Assert.AreEqual(Tier.Gold, p.Steps[0].To);
            Assert.AreEqual(11.3f, p.Steps[0].WhiteAt, 1e-4f); Assert.AreEqual(13.12f, p.SlashAt, 1e-4f);
            Assert.AreEqual(14.47f, p.RainbowAt, 1e-4f); Assert.AreEqual(14.47f, p.SpinAt, 1e-4f); Assert.AreEqual(18.12f, p.End, 1e-4f);
            Assert.AreEqual(Tier.Silver, p.Land);
        }

        [Test]
        public void 銀だけ()
        {
            var p = PromoTimeline.Plan(10, C(Tier.Silver));
            Assert.AreEqual(0, p.Steps.Length); Assert.AreEqual(Inf, p.SlashAt); Assert.AreEqual(11.8f, p.End, 1e-4f);
        }

        [Test] public void 銀から金() => Assert.AreEqual(13.22f, PromoTimeline.Plan(10, C(Tier.Silver, Tier.Gold)).End, 1e-4f);

        [Test]
        public void 金から虹()
        {
            var p = PromoTimeline.Plan(10, C(Tier.Gold, Tier.Rainbow));
            Assert.AreEqual(11.7f, p.SlashAt, 1e-4f); Assert.AreEqual(16.7f, p.End, 1e-4f);
        }

        [Test]
        public void 虹だけは着地のあと回る()
        {
            var p = PromoTimeline.Plan(10, C(Tier.Rainbow));
            Assert.AreEqual(10.5f, p.SpinAt, 1e-4f); Assert.AreEqual(Inf, p.SlashAt); Assert.AreEqual(14.15f, p.End, 1e-4f);
        }

        [Test]
        public void 虹を含まなければ切り裂かない()
        {
            var p = PromoTimeline.Plan(10, C(Tier.Green, Tier.Red));
            Assert.AreEqual(Inf, p.SlashAt); Assert.AreEqual(Inf, p.SpinAt); Assert.AreEqual(Tier.Red, p.Last);
        }

        [Test]
        public void 段が進むほど間が詰まり最短を下回らない()
        {
            var p = PromoTimeline.Plan(10, C(Tier.Silver, Tier.Blue, Tier.Green, Tier.Purple, Tier.Red, Tier.Gold, Tier.Rainbow));
            Assert.AreEqual(5, p.Steps.Length);
            for (int i = 1; i < p.Steps.Length; i++)
            {
                float gap = p.Steps[i].WhiteAt - p.Steps[i - 1].SnapAt;
                Assert.That(gap, Is.GreaterThanOrEqualTo(0.7f - 1e-4f));
                if (i > 1) Assert.That(gap, Is.LessThanOrEqualTo(p.Steps[i - 1].WhiteAt - p.Steps[i - 2].SnapAt + 1e-4f));
            }
            Assert.AreEqual(1.7f, PromoTimeline.HoldAfter(1), 1e-4f);
            Assert.AreEqual(1.445f, PromoTimeline.HoldAfter(2), 1e-4f);
            Assert.AreEqual(0.7f, PromoTimeline.HoldAfter(9), 1e-4f);
        }

        [Test]
        public void どの組み合わせでも時刻は前から順()
        {
            for (int m = 1; m < 128; m++)
            {
                var chain = Chain.FromMask(m);
                var p = PromoTimeline.Plan(10, chain);
                float prev = 10;
                foreach (var s in p.Steps) { Assert.That(s.WhiteAt, Is.GreaterThan(prev), Chain.Label(chain)); prev = s.SnapAt; }
                if (!float.IsInfinity(p.SlashAt))
                {
                    Assert.That(p.SlashAt, Is.GreaterThan(prev), Chain.Label(chain));
                    Assert.That(p.RainbowAt, Is.GreaterThan(p.SlashAt), Chain.Label(chain));
                }
                Assert.That(p.End, Is.GreaterThan(prev), Chain.Label(chain));
                Assert.IsFalse(float.IsInfinity(p.End), Chain.Label(chain));
                Assert.AreEqual(chain[chain.Length - 1], p.Last, Chain.Label(chain));
                Assert.AreEqual(chain[0], p.Land, Chain.Label(chain));
            }
        }

        [Test]
        public void その時刻の色()
        {
            var p = PromoTimeline.Plan(10, C(Tier.Green, Tier.Red, Tier.Rainbow));
            Assert.AreEqual(Tier.Green, p.TierAt(10.5f));
            Assert.AreEqual(Tier.Red, p.TierAt(p.Steps[0].SnapAt + 0.01f));
            Assert.AreEqual(Tier.Rainbow, p.TierAt(p.RainbowAt + 0.01f));
            Assert.AreEqual(1, p.SnappedCount(p.Steps[0].SnapAt + 0.01f));
            Assert.AreEqual(0, p.SnappedCount(p.Steps[0].WhiteAt + 0.01f));   // 白く飛んでいるあいだは、まだ切り替わっていない
        }

        // ───────── チラ見せ ─────────

        [Test]
        public void チラ見せは今と同じ4文字()
        {
            var s = TeasePlan.Plan(2, 12, 4);
            Assert.AreEqual(4, s.Length);
            Assert.AreEqual(2f, s[0].At, 1e-4f); Assert.AreEqual(0.9f, s[0].Len, 1e-4f);
            Assert.AreEqual(3.4f, s[1].At, 1e-4f); Assert.AreEqual(0.55f, s[3].Len, 1e-4f);
        }

        [Test]
        public void チラ見せは全部まで速くなり最短を下回らない()
        {
            var s = TeasePlan.Plan(0, 12, 12);
            Assert.AreEqual(12, s.Length);
            Assert.AreEqual(0.45f, s[11].Len, 1e-4f);
            Assert.AreEqual(0.2f, s[11].At - (s[10].At + s[10].Len), 1e-4f);
        }

        [Test] public void 言葉より多い数は言葉の文字数() => Assert.AreEqual(1, TeasePlan.Plan(0, 12, 1).Length);

        [Test]
        public void 文字数0なら無しで放射はすぐ()
        {
            var s = TeasePlan.Plan(2, 0, 6);
            Assert.AreEqual(0, s.Length);
            Assert.AreEqual(2f, TeasePlan.End(2, s));
        }

        [Test]
        public void 放射の始まりは最後の文字の終わり()
        {
            var s = TeasePlan.Plan(2, 2, 6);
            Assert.AreEqual(s[1].At + s[1].Len, TeasePlan.End(2, s), 1e-4f);
        }
    }
}
