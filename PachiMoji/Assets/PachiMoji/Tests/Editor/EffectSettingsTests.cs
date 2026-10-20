using NUnit.Framework;
using UnityEngine;

namespace PachiMoji.Tests
{
    /// <summary>設定の保存と読み込み（本物の設定を汚さないよう、テスト用の名前で保存する）</summary>
    public class EffectSettingsTests
    {
        const string P = "test.fx.";

        [TearDown]
        public void Clean()
        {
            foreach (var k in new[] { "sparks", "light", "size", "font", "from", "to", "firstText", "firstBack", "chain", "teaseCount", "teaseText", "teaseBack" }) PlayerPrefs.DeleteKey(P + k);
        }

        [Test]
        public void 何も保存していなければ既定()
        {
            var s = EffectSettings.Load(P);
            Assert.IsTrue(s.SparksOn);
            Assert.AreEqual(LightColor.IceBlue, s.Light);
            Assert.AreEqual(LightSize.Medium, s.Size);
            Assert.AreEqual(TextFont.ReggaeOne, s.Font);
            Assert.AreEqual("ReggaeOne", s.FontName);
        }

        [Test]
        public void 保存して読み戻すと同じ()
        {
            new EffectSettings { SparksOn = false, Light = LightColor.Rainbow, Size = LightSize.Large, Font = TextFont.Mincho }.Save(P);
            var s = EffectSettings.Load(P);
            Assert.IsFalse(s.SparksOn);
            Assert.AreEqual(LightColor.Rainbow, s.Light);
            Assert.AreEqual(LightSize.Large, s.Size);
            Assert.AreEqual(TextFont.Mincho, s.Font);
            Assert.AreEqual("ZenOldMinchoBlack", s.FontName);
        }

        [Test]
        public void 古いはじまりがおわりより上でも範囲として読む()
        {
            PlayerPrefs.SetInt(P + "from", 2);
            PlayerPrefs.SetInt(P + "to", 0);
            CollectionAssert.AreEqual(new[] { Tier.Silver, Tier.Gold, Tier.Rainbow }, EffectSettings.Load(P).Chain);
        }

        [Test]
        public void 範囲外の数字は既定に戻す()
        {
            PlayerPrefs.SetInt(P + "light", 99);
            PlayerPrefs.SetInt(P + "font", -1);
            PlayerPrefs.SetInt(P + "to", 7);
            var s = EffectSettings.Load(P);
            Assert.AreEqual(LightColor.IceBlue, s.Light);
            Assert.AreEqual(TextFont.ReggaeOne, s.Font);
            CollectionAssert.AreEqual(new[] { Tier.Silver, Tier.Gold, Tier.Rainbow }, s.Chain);   // 古い to の壊れた値は虹（既定）
        }

        [Test]
        public void 光の大きさ()
        {
            Assert.AreEqual(0.75f, new EffectSettings { Size = LightSize.Small }.LightScale, 1e-5f);
            Assert.AreEqual(1f, new EffectSettings { Size = LightSize.Medium }.LightScale, 1e-5f);
            Assert.AreEqual(1.3f, new EffectSettings { Size = LightSize.Large }.LightScale, 1e-5f);
        }

        // ───────── 昇格の色・チラ見せ ─────────

        [Test]
        public void 昇格の色とチラ見せの既定()
        {
            var s = EffectSettings.Load(P);
            CollectionAssert.AreEqual(new[] { Tier.Silver, Tier.Gold, Tier.Rainbow }, s.Chain);
            Assert.AreEqual(12, s.TeaseCount);
            Assert.AreEqual(Tier.Silver, s.TeaseText);
            Assert.AreEqual(Tier.Blue, s.TeaseBack);
        }

        [Test]
        public void 昇格の色とチラ見せを保存して読み戻す()
        {
            new EffectSettings { Chain = new[] { Tier.Green, Tier.Red, Tier.Rainbow }, TeaseCount = 3, TeaseText = Tier.Gold, TeaseBack = Tier.Purple }.Save(P);
            var s = EffectSettings.Load(P);
            CollectionAssert.AreEqual(new[] { Tier.Green, Tier.Red, Tier.Rainbow }, s.Chain);
            Assert.AreEqual(3, s.TeaseCount);
            Assert.AreEqual(Tier.Gold, s.TeaseText);
            Assert.AreEqual(Tier.Purple, s.TeaseBack);
        }

        // 前の版の「はじまり・おわり」（0=銀・1=金・2=虹）だけを保存している人
        static Tier[] OldRange(int from, int to)
        {
            PlayerPrefs.DeleteKey(P + "chain");
            PlayerPrefs.SetInt(P + "from", from);
            PlayerPrefs.SetInt(P + "to", to);
            return EffectSettings.Load(P).Chain;
        }

        [Test]
        public void 古いはじまりとおわりを読み替える()
        {
            CollectionAssert.AreEqual(new[] { Tier.Gold }, OldRange(1, 1));
            CollectionAssert.AreEqual(new[] { Tier.Silver, Tier.Gold, Tier.Rainbow }, OldRange(0, 2));
            CollectionAssert.AreEqual(new[] { Tier.Gold, Tier.Rainbow }, OldRange(1, 2));
            CollectionAssert.AreEqual(new[] { Tier.Silver }, OldRange(0, 0));
        }

        [Test]
        public void chainがあれば古い値は見ない()
        {
            PlayerPrefs.SetInt(P + "chain", 4);
            PlayerPrefs.SetInt(P + "from", 0);
            PlayerPrefs.SetInt(P + "to", 2);
            CollectionAssert.AreEqual(new[] { Tier.Green }, EffectSettings.Load(P).Chain);
        }

        [Test]
        public void 壊れた値は既定()
        {
            PlayerPrefs.SetInt(P + "chain", 0);
            Assert.AreEqual(Chain.ToMask(Chain.Default), Chain.ToMask(EffectSettings.Load(P).Chain));
            PlayerPrefs.SetInt(P + "chain", 999);
            Assert.AreEqual(Chain.ToMask(Chain.Default), Chain.ToMask(EffectSettings.Load(P).Chain));
            PlayerPrefs.SetInt(P + "teaseCount", -1);
            Assert.AreEqual(12, EffectSettings.Load(P).TeaseCount);
            PlayerPrefs.SetInt(P + "teaseCount", 13);
            Assert.AreEqual(12, EffectSettings.Load(P).TeaseCount);
            PlayerPrefs.SetInt(P + "teaseText", 7);
            PlayerPrefs.SetInt(P + "teaseBack", -1);
            var s = EffectSettings.Load(P);
            Assert.AreEqual(Tier.Silver, s.TeaseText);
            Assert.AreEqual(Tier.Blue, s.TeaseBack);
        }

        [Test]
        public void 写しの昇格の色は別物()
        {
            var a = new EffectSettings();
            var b = a.Clone();
            b.Chain[0] = Tier.Red;
            Assert.AreEqual(Tier.Silver, a.Chain[0]);
        }

        [Test]
        public void 写しは別物()
        {
            var a = new EffectSettings();
            var b = a.Clone();
            b.SparksOn = false;
            Assert.IsTrue(a.SparksOn);
        }
    }
}
