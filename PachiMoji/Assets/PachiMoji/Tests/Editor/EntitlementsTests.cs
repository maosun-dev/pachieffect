using NUnit.Framework;
using UnityEngine;

namespace PachiMoji.Tests
{
    /// <summary>買ったもの → 印・広告・見せる商品。端末への保存（本物を汚さないよう "test.ent" に保存する）</summary>
    public class EntitlementsTests
    {
        const string K = "test.ent";

        [TearDown] public void Clean() => PlayerPrefs.DeleteKey(K);

        [Test]
        public void 何も買っていなければ印も広告も出してセットを含む3つを見せる()
        {
            var e = new Entitlements(new string[0]);
            Assert.IsTrue(e.ShowWatermark); Assert.IsTrue(e.ShowAds);
            CollectionAssert.AreEqual(new[] { ProductIds.NoMark, ProductIds.NoAds, ProductIds.Set }, e.ShopItems);
        }

        [Test]
        public void 印を消すを買えば印だけ消え残りの1つだけ見せる()
        {
            var e = new Entitlements(new[] { ProductIds.NoMark });
            Assert.IsFalse(e.ShowWatermark); Assert.IsTrue(e.ShowAds);
            CollectionAssert.AreEqual(new[] { ProductIds.NoAds }, e.ShopItems);
        }

        [Test]
        public void 広告を消すを買えば広告だけ消え残りの1つだけ見せる()
        {
            var e = new Entitlements(new[] { ProductIds.NoAds });
            Assert.IsTrue(e.ShowWatermark); Assert.IsFalse(e.ShowAds);
            CollectionAssert.AreEqual(new[] { ProductIds.NoMark }, e.ShopItems);
        }

        [Test]
        public void セットは両方に効き何も見せない()
        {
            var e = new Entitlements(new[] { ProductIds.Set });
            Assert.IsFalse(e.ShowWatermark); Assert.IsFalse(e.ShowAds);
            CollectionAssert.IsEmpty(e.ShopItems);
        }

        [Test] public void 両方を別々に買っても何も見せない() =>
            CollectionAssert.IsEmpty(new Entitlements(new[] { ProductIds.NoMark, ProductIds.NoAds }).ShopItems);

        [Test] public void 知らないIDは無視する() => Assert.IsTrue(new Entitlements(new[] { "x.y" }).ShowAds);

        [Test] public void 同じものが何度届いても同じ() =>
            CollectionAssert.AreEqual(new[] { ProductIds.NoMark }, new Entitlements(new string[0]).With(ProductIds.NoAds).With(ProductIds.NoAds).ShopItems);

        [Test]
        public void 保存して読み戻すと同じ()
        {
            new Entitlements(new[] { ProductIds.NoAds }).Save(K);
            var e = Entitlements.Load(K);
            Assert.IsTrue(e.Owns(ProductIds.NoAds)); Assert.IsFalse(e.Owns(ProductIds.NoMark));
        }

        [Test] public void 何も保存していなければ何も持っていない() => Assert.IsTrue(Entitlements.Load(K).ShowAds);
    }
}
