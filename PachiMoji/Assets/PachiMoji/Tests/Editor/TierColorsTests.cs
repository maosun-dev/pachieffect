using NUnit.Framework;

namespace PachiMoji.Tests
{
    /// <summary>色の格（銀・青・緑・紫・赤・金・虹）、色の作り方、昇格の色の組み合わせ</summary>
    public class TierColorsTests
    {
        static Tier[] All => (Tier[])System.Enum.GetValues(typeof(Tier));

        [Test] public void 格の順() =>
            CollectionAssert.AreEqual(new[] { Tier.Silver, Tier.Blue, Tier.Green, Tier.Purple, Tier.Red, Tier.Gold, Tier.Rainbow }, All);

        [Test]
        public void 文字の芯()
        {
            Assert.IsNull(TierColors.TextBase(Tier.Silver)); Assert.IsNull(TierColors.TextBase(Tier.Gold)); Assert.IsNull(TierColors.TextBase(Tier.Rainbow));
            Assert.AreEqual("#2A7BFF", TierColors.TextBase(Tier.Blue)); Assert.AreEqual("#19D14A", TierColors.TextBase(Tier.Green));
            Assert.AreEqual("#A040FF", TierColors.TextBase(Tier.Purple)); Assert.AreEqual("#FF2030", TierColors.TextBase(Tier.Red));
        }

        [Test]
        public void 背景()
        {
            Assert.AreEqual(BackKind.Gray, TierColors.Back(Tier.Silver)); Assert.AreEqual(BackKind.Fire, TierColors.Back(Tier.Gold));
            Assert.AreEqual(BackKind.Rainbow, TierColors.Back(Tier.Rainbow)); Assert.AreEqual(BackKind.Hue, TierColors.Back(Tier.Red));
            Assert.AreEqual(0f, TierColors.BackHue(Tier.Blue)); Assert.AreEqual(-85f, TierColors.BackHue(Tier.Green));
            Assert.AreEqual(65f, TierColors.BackHue(Tier.Purple)); Assert.AreEqual(125f, TierColors.BackHue(Tier.Red));
        }

        [Test] public void 名前() => Assert.AreEqual("銀青緑紫赤金虹", string.Concat(System.Array.ConvertAll(All, TierColors.Name)));

        [Test]
        public void 印と並び()
        {
            Assert.AreEqual(1 + 32 + 64, Chain.ToMask(Chain.Default));
            CollectionAssert.AreEqual(new[] { Tier.Green, Tier.Red, Tier.Rainbow }, Chain.FromMask(4 + 16 + 64));
            CollectionAssert.AreEqual(Chain.Default, Chain.FromMask(0));
            CollectionAssert.AreEqual(new[] { Tier.Silver, Tier.Gold, Tier.Rainbow }, Chain.Default);
        }

        [Test] public void 押すと入る_いつも格の順() =>
            CollectionAssert.AreEqual(new[] { Tier.Green, Tier.Red, Tier.Rainbow }, Chain.Toggle(new[] { Tier.Green, Tier.Rainbow }, Tier.Red));

        [Test] public void 押すと外れる() => CollectionAssert.AreEqual(new[] { Tier.Gold }, Chain.Toggle(new[] { Tier.Gold, Tier.Rainbow }, Tier.Rainbow));

        [Test] public void 最後の1色は外れない() => CollectionAssert.AreEqual(new[] { Tier.Gold }, Chain.Toggle(new[] { Tier.Gold }, Tier.Gold));

        [Test]
        public void 文字()
        {
            Assert.AreEqual("緑 → 赤 → 虹", Chain.Label(new[] { Tier.Green, Tier.Red, Tier.Rainbow }));
            Assert.AreEqual("緑だけ", Chain.Label(new[] { Tier.Green }));
        }
    }
}
