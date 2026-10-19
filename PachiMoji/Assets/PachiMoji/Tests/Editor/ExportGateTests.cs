using NUnit.Framework;

namespace PachiMoji.Tests
{
    /// <summary>「書き出す」を押したとき、すぐ書くか・広告を見せるか・待つか・ネットが要るか</summary>
    public class ExportGateTests
    {
        [Test]
        public void 広告を消した人はネットが無くてもすぐ書き出す()
        {
            Assert.AreEqual(GateAction.ExportNow, ExportGate.Decide(false, false, AdState.Failed));
            Assert.AreEqual(GateAction.ExportNow, ExportGate.Decide(false, true, AdState.Loading));
        }

        [Test] public void 広告が読めていれば広告を見せる() => Assert.AreEqual(GateAction.ShowAd, ExportGate.Decide(true, true, AdState.Ready));

        [Test]
        public void ネットが無ければネットが要る()
        {
            Assert.AreEqual(GateAction.NeedNetwork, ExportGate.Decide(true, false, AdState.Ready));
            Assert.AreEqual(GateAction.NeedNetwork, ExportGate.Decide(true, false, AdState.Loading));
        }

        [Test] public void 広告が読めなければネットが要る() => Assert.AreEqual(GateAction.NeedNetwork, ExportGate.Decide(true, true, AdState.Failed));

        [Test] public void 読み込み中は待つ() => Assert.AreEqual(GateAction.Wait, ExportGate.Decide(true, true, AdState.Loading));

        [Test]
        public void 知らせの文()
        {
            Assert.AreEqual("ネットにつなぐと書き出せます", ExportGate.Message(GateAction.NeedNetwork));
            Assert.AreEqual("広告を読み込んでいます。少し待ってから、もう一度「書き出す」を押してください", ExportGate.Message(GateAction.Wait));
            Assert.AreEqual("", ExportGate.Message(GateAction.ExportNow));
            Assert.AreEqual("", ExportGate.Message(GateAction.ShowAd));
            Assert.AreEqual("最後まで見ると書き出せます", ExportGate.NotWatched);
        }
    }
}
