using NUnit.Framework;
using UnityEngine;

namespace PachiMoji.Tests
{
    /// <summary>再生の時計・画面の消灯・メダルのなぞり</summary>
    public class AppRulesTests
    {
        [Test] public void 時計はふつうに進む() => Assert.AreEqual(1.0167f, PlayClock.Step(1f, 1f / 60), 1e-4f);
        [Test] public void 裏から戻っても飛ばない() => Assert.AreEqual(5.1f, PlayClock.Step(5f, 30f), 1e-4f);
        [Test] public void 時計は戻らない() => Assert.AreEqual(5f, PlayClock.Step(5f, -1f), 1e-4f);

        [Test]
        public void 再生中だけ消灯しない()
        {
            Assert.AreEqual(SleepTimeout.NeverSleep, SleepPolicy.For(AppState.Playing));
            Assert.AreEqual(SleepTimeout.SystemSetting, SleepPolicy.For(AppState.Idle));
            Assert.AreEqual(SleepTimeout.SystemSetting, SleepPolicy.For(AppState.Finished));
            Assert.AreEqual(SleepTimeout.SystemSetting, SleepPolicy.For(AppState.Settings));
            Assert.AreEqual(SleepTimeout.NeverSleep, SleepPolicy.For(AppState.Exporting));   // 書き出し中も消さない
            Assert.AreEqual(SleepTimeout.SystemSetting, SleepPolicy.For(AppState.Exported));
        }

        // 舞台の絵を画面の一部（範囲）に縮めて置く投影。カメラの正面の点は範囲のまん中に、画角の端は範囲の端に映る
        static Vector2 ToViewport(Matrix4x4 p, Vector3 viewPos)
        {
            var c = p * new Vector4(viewPos.x, viewPos.y, viewPos.z, 1);
            return new Vector2(c.x / c.w * 0.5f + 0.5f, c.y / c.w * 0.5f + 0.5f);
        }

        [Test]
        public void 舞台の正面は範囲のまん中に映る()
        {
            var r = new Rect(0, 0.4f, 1, 0.6f);
            var p = StageFraming.Projection(32, 0.8f, 0.05f, 100, r);
            var v = ToViewport(p, new Vector3(0, 0, -10));   // カメラの見る向きは -z
            Assert.AreEqual(0.5f, v.x, 1e-4f);
            Assert.AreEqual(0.7f, v.y, 1e-4f);
        }

        [Test]
        public void 画角の上の端は範囲の上の端に映る()
        {
            var r = new Rect(0, 0.4f, 1, 0.6f);
            float y = 10 * Mathf.Tan(16 * Mathf.Deg2Rad);   // 画角 32° の半分
            var v = ToViewport(StageFraming.Projection(32, 0.8f, 0.05f, 100, r), new Vector3(0, y, -10));
            Assert.AreEqual(1f, v.y, 1e-4f);
        }

        [Test]
        public void 範囲が画面全体なら_ふつうの投影と同じ()
        {
            var p = StageFraming.Projection(32, 0.5f, 0.05f, 100, new Rect(0, 0, 1, 1));
            var q = Matrix4x4.Perspective(32, 0.5f, 0.05f, 100);
            for (int i = 0; i < 16; i++) Assert.AreEqual(q[i], p[i], 1e-5f);
        }
    }
}
