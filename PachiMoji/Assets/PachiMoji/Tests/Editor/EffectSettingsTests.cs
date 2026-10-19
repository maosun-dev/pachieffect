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
            foreach (var k in new[] { "sparks", "light", "size", "font", "from", "to", "firstText", "firstBack" }) PlayerPrefs.DeleteKey(P + k);
        }

        [Test]
        public void 何も保存していなければ既定()
        {
            var s = EffectSettings.Load(P);
            Assert.IsTrue(s.SparksOn);
            Assert.AreEqual(LightColor.IceBlue, s.Light);
            Assert.AreEqual(LightSize.Medium, s.Size);
            Assert.AreEqual(TextFont.ReggaeOne, s.Font);
            Assert.AreEqual(Tier.Silver, s.From);
            Assert.AreEqual(Tier.Rainbow, s.To);
            Assert.AreEqual("ReggaeOne", s.FontName);
            Assert.AreEqual(FirstText.Silver, s.FirstText);
            Assert.AreEqual(FirstBack.Blue, s.FirstBack);
        }

        [Test]
        public void 保存して読み戻すと同じ()
        {
            new EffectSettings { SparksOn = false, Light = LightColor.Rainbow, Size = LightSize.Large, Font = TextFont.Mincho, From = Tier.Gold, To = Tier.Gold,
                FirstText = FirstText.Purple, FirstBack = FirstBack.Red }.Save(P);
            var s = EffectSettings.Load(P);
            Assert.IsFalse(s.SparksOn);
            Assert.AreEqual(LightColor.Rainbow, s.Light);
            Assert.AreEqual(LightSize.Large, s.Size);
            Assert.AreEqual(TextFont.Mincho, s.Font);
            Assert.AreEqual(Tier.Gold, s.From);
            Assert.AreEqual(Tier.Gold, s.To);
            Assert.AreEqual("ZenOldMinchoBlack", s.FontName);
            Assert.AreEqual(FirstText.Purple, s.FirstText);
            Assert.AreEqual(FirstBack.Red, s.FirstBack);
        }

        [Test]
        public void はじまりがおわりより上なら入れ替える()
        {
            PlayerPrefs.SetInt(P + "from", 2);
            PlayerPrefs.SetInt(P + "to", 0);
            var s = EffectSettings.Load(P);
            Assert.AreEqual(Tier.Silver, s.From);
            Assert.AreEqual(Tier.Rainbow, s.To);
        }

        [Test]
        public void 範囲外の数字は既定に戻す()
        {
            PlayerPrefs.SetInt(P + "light", 99);
            PlayerPrefs.SetInt(P + "font", -1);
            PlayerPrefs.SetInt(P + "to", 7);
            PlayerPrefs.SetInt(P + "firstText", 5);
            PlayerPrefs.SetInt(P + "firstBack", 4);
            var s = EffectSettings.Load(P);
            Assert.AreEqual(FirstText.Silver, s.FirstText);
            Assert.AreEqual(FirstBack.Blue, s.FirstBack);
            Assert.AreEqual(LightColor.IceBlue, s.Light);
            Assert.AreEqual(TextFont.ReggaeOne, s.Font);
            Assert.AreEqual(Tier.Rainbow, s.To);
        }

        [Test]
        public void 光の大きさ()
        {
            Assert.AreEqual(0.75f, new EffectSettings { Size = LightSize.Small }.LightScale, 1e-5f);
            Assert.AreEqual(1f, new EffectSettings { Size = LightSize.Medium }.LightScale, 1e-5f);
            Assert.AreEqual(1.3f, new EffectSettings { Size = LightSize.Large }.LightScale, 1e-5f);
        }

        [Test]
        public void 最初の色ははじまりが銀のときだけ効く()
        {
            Assert.IsTrue(new EffectSettings { From = Tier.Silver, To = Tier.Rainbow }.FirstColorsApply);
            Assert.IsFalse(new EffectSettings { From = Tier.Gold, To = Tier.Rainbow }.FirstColorsApply);
            Assert.IsFalse(new EffectSettings { From = Tier.Rainbow, To = Tier.Rainbow }.FirstColorsApply);
        }

        [Test]
        public void 背景は今の青から色相を回して作る()
        {
            Assert.AreEqual(0f, FirstColors.BackHue(FirstBack.Blue));
            Assert.AreEqual(-85f, FirstColors.BackHue(FirstBack.Green));
            Assert.AreEqual(125f, FirstColors.BackHue(FirstBack.Red));
            Assert.AreEqual(65f, FirstColors.BackHue(FirstBack.Purple));
        }

        [Test]
        public void 文字の色の芯()
        {
            Assert.IsNull(FirstColors.TextBase(FirstText.Silver));   // 銀は今の銀の対応表のまま
            Assert.AreEqual("#2A7BFF", FirstColors.TextBase(FirstText.Blue));
            Assert.AreEqual("#19D14A", FirstColors.TextBase(FirstText.Green));
            Assert.AreEqual("#FF2030", FirstColors.TextBase(FirstText.Red));
            Assert.AreEqual("#A040FF", FirstColors.TextBase(FirstText.Purple));
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
