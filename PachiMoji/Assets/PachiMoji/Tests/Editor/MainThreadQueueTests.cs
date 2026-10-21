using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

namespace PachiMoji.Tests
{
    /// <summary>広告の部品の返事を、Unity の流れ（毎コマ）で受け取る待ち行列</summary>
    public class MainThreadQueueTests
    {
        [Test]
        public void 取り出すまでは動かない()
        {
            var q = new MainThreadQueue();
            bool ran = false;
            q.Post(() => ran = true);
            Assert.IsFalse(ran);
            q.Drain();
            Assert.IsTrue(ran);
        }

        [Test]
        public void ほかの流れから入れても取り出した流れで順に動く()
        {
            var q = new MainThreadQueue();
            var order = new List<int>();
            int drainThread = Thread.CurrentThread.ManagedThreadId, ranOn = -1;
            var t = new Thread(() =>
            {
                q.Post(() => order.Add(1));
                q.Post(() => { order.Add(2); ranOn = Thread.CurrentThread.ManagedThreadId; });
            });
            t.Start();
            t.Join();
            q.Drain();
            CollectionAssert.AreEqual(new[] { 1, 2 }, order);
            Assert.AreEqual(drainThread, ranOn);
        }

        [Test]
        public void 動いている最中に入れたものは次の取り出しで動く()
        {
            var q = new MainThreadQueue();
            int count = 0;
            q.Post(() => { count++; q.Post(() => count += 10); });
            q.Drain();
            Assert.AreEqual(1, count);
            q.Drain();
            Assert.AreEqual(11, count);
        }

        [Test]
        public void 一つが失敗しても残りは動く()
        {
            var q = new MainThreadQueue();
            bool after = false;
            q.Post(() => throw new System.Exception("試しの失敗"));
            q.Post(() => after = true);
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            q.Drain();
            Assert.IsTrue(after);
        }
    }
}
