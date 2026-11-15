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
            foreach (var k in new[] { "sparks", "light", "size", "font", "from", "to", "firstText", "firstBack", "chain", "teaseCount", "teaseText", "teaseBack", "effect", "photo", "photoX", "photoY", "photoZoom", "obiDepth", "obiRed", "obiGlow", "obiGlowAmount", "obiBlueBack", "obiTopX", "obiTopY", "obiTopS", "obiBotX", "obiBotY", "obiBotS", "exportWide", "obiLayout", "obiSpin" }) PlayerPrefs.DeleteKey(P + k);
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
    
        [Test]
        public void 演出と写真の既定()
        {
            var s = EffectSettings.Load(P);
            Assert.AreEqual("oudou", s.EffectId);
            Assert.AreEqual("", s.PhotoPath);
            Assert.AreEqual(Vector2.zero, s.PhotoOffset);
            Assert.AreEqual(1f, s.PhotoZoom);
        }

        [Test]
        public void 演出と写真を保存して読み戻す()
        {
            new EffectSettings { EffectId = "obi", PhotoPath = "a/b.jpg", PhotoOffset = new Vector2(0.1f, -0.2f), PhotoZoom = 2.5f }.Save(P);
            var s = EffectSettings.Load(P);
            Assert.AreEqual("obi", s.EffectId);
            Assert.AreEqual("a/b.jpg", s.PhotoPath);
            Assert.AreEqual(0.1f, s.PhotoOffset.x, 1e-6f);
            Assert.AreEqual(-0.2f, s.PhotoOffset.y, 1e-6f);
            Assert.AreEqual(2.5f, s.PhotoZoom, 1e-6f);
        }

        [Test]
        public void 写真の倍率が範囲外なら収める()
        {
            PlayerPrefs.SetFloat(P + "photoZoom", 10);
            Assert.AreEqual(PhotoFit.MaxZoom, EffectSettings.Load(P).PhotoZoom);
            PlayerPrefs.SetFloat(P + "photoZoom", 0.2f);
            Assert.AreEqual(1f, EffectSettings.Load(P).PhotoZoom);
        }

        [Test]
        public void 帯の選びの既定()
        {
            var s = EffectSettings.Load(P);
            Assert.AreEqual(ObiDepth.Full, s.ObiDepth);
            Assert.AreEqual(ObiRed.None, s.ObiRed);
            Assert.AreEqual(ObiGlow.None, s.ObiGlow);
            Assert.AreEqual(620f, s.ObiDepthValue);
            Assert.AreEqual(0f, s.ObiRedValue);
        }

        [Test]
        public void 帯の選びを保存して読み戻す()
        {
            new EffectSettings { ObiDepth = ObiDepth.Thin, ObiRed = ObiRed.Half, ObiGlow = ObiGlow.Fire }.Save(P);
            var s = EffectSettings.Load(P);
            Assert.AreEqual(ObiDepth.Thin, s.ObiDepth);
            Assert.AreEqual(ObiRed.Half, s.ObiRed);
            Assert.AreEqual(ObiGlow.Fire, s.ObiGlow);
            Assert.AreEqual(150f, s.ObiDepthValue);
            Assert.AreEqual(0.5f, s.ObiRedValue);
        }

        [Test]
        public void 帯の選びが範囲外なら既定()
        {
            PlayerPrefs.SetInt(P + "obiDepth", 9);
            PlayerPrefs.SetInt(P + "obiRed", -1);
            PlayerPrefs.SetInt(P + "obiGlow", 3);
            var s = EffectSettings.Load(P);
            Assert.AreEqual(ObiDepth.Full, s.ObiDepth);
            Assert.AreEqual(ObiRed.None, s.ObiRed);
            Assert.AreEqual(ObiGlow.None, s.ObiGlow);
        }

        [Test]
        public void 光の量の既定はふつうで保存できる()
        {
            var d = EffectSettings.Load(P);
            Assert.AreEqual(ObiGlowAmount.Mid, d.ObiGlowAmount);
            new EffectSettings { ObiGlowAmount = ObiGlowAmount.High }.Save(P);
            var s = EffectSettings.Load(P);
            Assert.AreEqual(ObiGlowAmount.High, s.ObiGlowAmount);
            Assert.Greater(s.ObiGlowWidth, d.ObiGlowWidth);
            Assert.Greater(s.ObiGlowBright, d.ObiGlowBright);
            Assert.Less(new EffectSettings { ObiGlowAmount = ObiGlowAmount.Low }.ObiGlowWidth, d.ObiGlowWidth);
        }

        [Test]
        public void ブルーバックの既定はなしで保存できる()
        {
            Assert.IsFalse(EffectSettings.Load(P).ObiBlueBack);
            new EffectSettings { ObiBlueBack = true }.Save(P);
            Assert.IsTrue(EffectSettings.Load(P).ObiBlueBack);
        }

        [Test]
        public void 帯の位置と大きさの既定と保存()
        {
            var d = EffectSettings.Load(P);
            Assert.AreEqual(Vector2.zero, d.ObiTopOffset);
            Assert.AreEqual(1f, d.ObiTopScale);
            Assert.AreEqual(Vector2.zero, d.ObiBottomOffset);
            Assert.AreEqual(1f, d.ObiBottomScale);
            new EffectSettings { ObiTopOffset = new Vector2(0.5f, 1.2f), ObiTopScale = 1.3f, ObiBottomOffset = new Vector2(-0.4f, 0), ObiBottomScale = 0.8f }.Save(P);
            var s = EffectSettings.Load(P);
            Assert.AreEqual(1.2f, s.ObiTopOffset.y, 1e-5f);
            Assert.AreEqual(1.3f, s.ObiTopScale, 1e-5f);
            Assert.AreEqual(-0.4f, s.ObiBottomOffset.x, 1e-5f);
            Assert.AreEqual(0.8f, s.ObiBottomScale, 1e-5f);
        }

        [Test]
        public void 帯の大きさと位置は範囲に収める()
        {
            Assert.AreEqual(EffectSettings.ObiScaleMin, EffectSettings.ClampObiScale(0.1f));
            Assert.AreEqual(EffectSettings.ObiScaleMax, EffectSettings.ClampObiScale(9f));
            var o = EffectSettings.ClampObiOffset(new Vector2(99, -99));
            Assert.AreEqual(EffectSettings.ObiOffsetMax, o.x);
            Assert.AreEqual(-EffectSettings.ObiOffsetMax, o.y);
        }

        [Test]
        public void 書き出しの向きの既定は縦で保存できる()
        {
            Assert.IsFalse(EffectSettings.Load(P).ExportWide);
            new EffectSettings { ExportWide = true }.Save(P);
            Assert.IsTrue(EffectSettings.Load(P).ExportWide);
        }

        [Test]
        public void 帯の置き方の既定は斜めで保存できる()
        {
            Assert.AreEqual(ObiLayout.Slant, EffectSettings.Load(P).ObiLayout);
            new EffectSettings { ObiLayout = ObiLayout.Straight }.Save(P);
            Assert.AreEqual(ObiLayout.Straight, EffectSettings.Load(P).ObiLayout);
        }

        [Test]
        public void 字の回転の既定はなしで保存できる()
        {
            var d = EffectSettings.Load(P);
            Assert.AreEqual(ObiSpin.None, d.ObiSpin);
            Assert.AreEqual(0, d.ObiSpinCount);
            new EffectSettings { ObiSpin = ObiSpin.Fast }.Save(P);
            var s = EffectSettings.Load(P);
            Assert.AreEqual(ObiSpin.Fast, s.ObiSpin);
            Assert.Greater(s.ObiSpinCount, new EffectSettings { ObiSpin = ObiSpin.Slow }.ObiSpinCount);
        }
    }
}
