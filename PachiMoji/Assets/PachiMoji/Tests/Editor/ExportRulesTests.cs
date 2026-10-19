using NUnit.Framework;

namespace PachiMoji.Tests
{
    /// <summary>動画の書き出しの計算（コマ数・時刻・％・空き容量・印の位置）と知らせの文</summary>
    public class ExportRulesTests
    {
        [Test] public void 最後のコマまで数える() { Assert.AreEqual(451, ExportPlan.FrameCount(15f)); Assert.AreEqual(271, ExportPlan.FrameCount(9f)); }
        [Test] public void 割り切れない長さは最後に終わりの時刻のコマを足す() => Assert.AreEqual(272, ExportPlan.FrameCount(9.02f));
        [Test] public void 時刻は30分の1秒ずつ() { Assert.AreEqual(0f, ExportPlan.TimeOf(0, 15f)); Assert.AreEqual(1f, ExportPlan.TimeOf(30, 15f), 1e-5f); }
        [Test] public void 最後のコマは終わりの時刻ちょうど() { Assert.AreEqual(15f, ExportPlan.TimeOf(450, 15f), 1e-5f); Assert.AreEqual(9.02f, ExportPlan.TimeOf(271, 9.02f), 1e-5f); }

        [Test]
        public void 進み具合は仕上げまで99で止まる()
        {
            Assert.AreEqual(0, ExportPlan.Percent(0, 451, false));
            Assert.AreEqual(49, ExportPlan.Percent(225, 451, false));
            Assert.AreEqual(99, ExportPlan.Percent(451, 451, false));
            Assert.AreEqual(100, ExportPlan.Percent(451, 451, true));
        }

        [Test] public void 要る空き容量は見積もりの1点5倍と50MB() => Assert.AreEqual(86_178_800L, ExportPlan.NeedBytes(15f));

        [Test]
        public void 印は右下に幅の3割()
        {
            var r = ExportPlan.WatermarkRect(640, 128);   // 印の絵は 640×128
            Assert.AreEqual(324f, r.width, 1e-3f);
            Assert.AreEqual(64.8f, r.height, 1e-3f);
            Assert.AreEqual(1080f - 37.8f - 324f, r.x, 1e-3f);   // 余白は幅の 3.5%
            Assert.AreEqual(37.8f, r.y, 1e-3f);
        }

        [Test]
        public void 知らせの文()
        {
            Assert.AreEqual("", ExportRules.Message(ExportError.None));
            Assert.AreEqual("空き容量が足りません。写真やアプリを整理してから、もう一度「書き出す」を押してください", ExportRules.Message(ExportError.NoSpace));
            Assert.AreEqual("動画を作れませんでした。アプリを開き直して、もう一度「書き出す」を押してください", ExportRules.Message(ExportError.CannotOpen));
            Assert.AreEqual("動画を作れませんでした。アプリを開き直して、もう一度「書き出す」を押してください", ExportRules.Message(ExportError.WriteFailed));
            Assert.AreEqual("書き出しを中断しました。もう一度「書き出す」を押してください", ExportRules.Message(ExportError.Interrupted));
            Assert.AreEqual("写真への保存が許可されていません。設定アプリ →「脳汁メーカー」→「写真」で「追加のみ」を選んでください", ExportRules.Message(ExportError.PhotosDenied));
            Assert.AreEqual("写真に保存できませんでした。空き容量を確かめて、もう一度「写真に保存」を押してください", ExportRules.Message(ExportError.SaveFailed));
        }
    }
}
