using System;
using System.Collections.Generic;
using UnityEngine;

namespace PachiMoji
{
    /// <summary>
    /// ほかの流れ（iPhone の広告の部品の返事など）から入れた仕事を、Unity の流れで毎コマ取り出して動かす待ち行列。
    /// 広告の部品の「返事を Unity の流れで受ける」設定は、広告を始める前の同意の確認の返事を止めてしまうので使わない
    /// </summary>
    public class MainThreadQueue
    {
        readonly object gate = new object();
        List<Action> pending = new List<Action>(), running = new List<Action>();

        public void Post(Action a)
        {
            lock (gate) pending.Add(a);
        }

        /// <summary>今たまっている分だけ動かす（動いている最中に入れた分は次の取り出しで）</summary>
        public void Drain()
        {
            lock (gate) (pending, running) = (running, pending);
            foreach (var a in running)
            {
                try { a(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            running.Clear();
        }
    }
}
