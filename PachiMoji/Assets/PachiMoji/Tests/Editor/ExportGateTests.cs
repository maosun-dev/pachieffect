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

        // 終わりの画面の知らせ：広告を途中で閉じた知らせは、次に「書き出す」を押すまで、読み込み中の知らせで消さない
        static readonly string Net = ExportGate.Message(GateAction.NeedNetwork), Wait = ExportGate.Message(GateAction.Wait);
        [Test] public void 読み込み中の知らせを出す() => Assert.AreEqual(Wait, ExportGate.NoticeFor(GateAction.Wait, ""));
        [Test] public void 見なかった知らせは読み込み中でも残す() => Assert.AreEqual(ExportGate.NotWatched, ExportGate.NoticeFor(GateAction.Wait, ExportGate.NotWatched));
        [Test] public void 見なかった知らせは読めても残す() => Assert.AreEqual(ExportGate.NotWatched, ExportGate.NoticeFor(GateAction.ShowAd, ExportGate.NotWatched));
        [Test] public void ネットが切れたらネットの知らせ() => Assert.AreEqual(Net, ExportGate.NoticeFor(GateAction.NeedNetwork, ExportGate.NotWatched));
        [Test] public void 直ったら待ちとネットの知らせは消す()
        {
            Assert.AreEqual("", ExportGate.NoticeFor(GateAction.ShowAd, Wait));
            Assert.AreEqual("", ExportGate.NoticeFor(GateAction.ExportNow, Net));
        }
        [Test] public void ほかの知らせは残す() => Assert.AreEqual("空き容量が…", ExportGate.NoticeFor(GateAction.ShowAd, "空き容量が…"));

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
