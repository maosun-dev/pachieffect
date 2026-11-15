using NUnit.Framework;
using UnityEngine;

namespace PachiMoji.Tests
{
    /// <summary>激アツの帯の形と流れの計算</summary>
    public class ObiPlanTests
    {
        [Test]
        public void 上下の帯は決まりの形()
        {
            Assert.AreEqual(6.2f, ObiPlan.Top.R, 1e-5f);
            Assert.AreEqual(32f, ObiPlan.Top.Tilt, 1e-5f);
            Assert.AreEqual(-32f, ObiPlan.Top.Roll, 1e-5f);
            Assert.AreEqual(new Vector2(0, 1.25f), ObiPlan.Top.Center);
            Assert.AreEqual(6.2f, ObiPlan.Bottom.R, 1e-5f);
            Assert.AreEqual(24f, ObiPlan.Bottom.Tilt, 1e-5f);
            Assert.AreEqual(-32f, ObiPlan.Bottom.Roll, 1e-5f);
            Assert.AreEqual(new Vector2(0, 0.2f), ObiPlan.Bottom.Center);
        }

        [Test]
        public void 輪の点は試しの帯と同じ()
        {
            float a = 32 * Mathf.Deg2Rad;
            var want = Quaternion.Euler(0, 0, -32) * new Vector3(0, 6.2f * Mathf.Sin(a), 6.2f * Mathf.Cos(a)) + new Vector3(0, 1.25f, 0);
            var got = ObiPlan.Top.Point(Mathf.PI / 2);
            Assert.Less((got - want).magnitude, 1e-4f);
        }

        [Test]
        public void 十秒で元の並びに戻る([Values(1, 4, 12)] int chars, [Values(1, -1)] int dir)
        {
            const float arc = 19.5f;
            float loop = ObiPlan.LetterSlots(arc, chars) * ObiPlan.Spacing;
            float v = ObiPlan.LoopSpeed(loop);
            foreach (float s0 in new[] { 0f, 1.3f, 7.7f })
            {
                float a = ObiPlan.SlotPos(s0, dir, v, 0, loop, arc);
                float b = ObiPlan.SlotPos(s0, dir, v, ObiPlan.Duration, loop, arc);
                // 1周ぶんずれて端と端に分かれることもあるので、周の長さで割った余りで比べる
                float d = Mathf.Abs(Mathf.Repeat(a - b + loop / 2, loop) - loop / 2);
                Assert.Less(d, 1e-3f, $"chars={chars} dir={dir} s0={s0}");
            }
        }

        [Test]
        public void 画面に見える長さの帯なら速さは目安に近い()
        {
            // 帯は画面に見える範囲（＋少し外）だけにして使う。そのくらいの長さなら目安の速さに近い
            float loop = ObiPlan.LetterSlots(12f, 4) * ObiPlan.Spacing;
            Assert.LessOrEqual(Mathf.Abs(ObiPlan.LoopSpeed(loop) - ObiPlan.BaseSpeed), 0.6f);
        }

        [Test]
        public void 輪が長くても最低1周()
        {
            Assert.AreEqual(40f / 10f, ObiPlan.LoopSpeed(40f), 1e-5f);
        }

        [Test]
        public void 文字の数は言葉と区切りの倍数()
        {
            Assert.AreEqual(0, ObiPlan.LetterSlots(19.5f, 4) % 5);
            Assert.AreEqual(0, ObiPlan.LetterSlots(19.5f, 12) % 13);
            Assert.AreEqual(0, ObiPlan.LetterSlots(19.5f, 1) % 2);
            Assert.GreaterOrEqual(ObiPlan.LetterSlots(19.5f, 4) * ObiPlan.Spacing, 19.5f);
        }

        [Test]
        public void 縦が短い画面では上の帯が下がり下の帯が上がる()
        {
            Assert.Less(ObiPlan.TopFor(9f / 16f).Center.y, ObiPlan.Top.Center.y);
            Assert.Greater(ObiPlan.BottomFor(9f / 16f).Center.y, ObiPlan.Bottom.Center.y);
            Assert.AreEqual(ObiPlan.Top.Center, ObiPlan.TopFor(ObiPlan.BaseAspect).Center);
            Assert.AreEqual(ObiPlan.Bottom.Center, ObiPlan.BottomFor(ObiPlan.BaseAspect).Center);
        }

        [Test]
        public void 縦が短い画面でも帯はふちから同じ見え方の距離にある()
        {
            // 見える高さが減った半分だけ、帯の一番外側が画面の上でも内へ寄る（遠近で縮む分も足して）
            float a = 9f / 16f;
            float dist = ObiPlan.ViewH * 0.5f / Mathf.Tan(ObiPlan.Fov * 0.5f * Mathf.Deg2Rad);
            float shrink = (ObiPlan.ViewH - ObiPlan.ViewH * ObiPlan.BaseAspect / a) / 2;
            float Proj(Vector3 p) => p.y * dist / (dist + p.z);
            Assert.AreEqual(Proj(ObiPlan.Top.Point(Mathf.PI / 2)) - shrink, Proj(ObiPlan.TopFor(a).Point(Mathf.PI / 2)), 1e-3f);
            Assert.AreEqual(Proj(ObiPlan.Bottom.Point(-Mathf.PI / 2)) + shrink, Proj(ObiPlan.BottomFor(a).Point(-Mathf.PI / 2)), 1e-3f);
        }

        [Test]
        public void 横長の画面の斜めの帯は基準の輪のまま()
        {
            Assert.AreEqual(ObiPlan.Top.Center, ObiPlan.TopFor(16f / 9f).Center);
            Assert.AreEqual(ObiPlan.Top.Roll, ObiPlan.TopFor(16f / 9f).Roll);
            Assert.AreEqual(ObiPlan.Bottom.Center, ObiPlan.BottomFor(16f / 9f).Center);
        }

        [Test]
        public void まっすぐの帯は上と下を左から右へ水平に通る()
        {
            var top = ObiPlan.StraightPoints(true, 100, 8f, 2f, 0);
            var bot = ObiPlan.StraightPoints(false, 100, 8f, 2f, 0);
            Assert.AreEqual(101, top.Length);
            Assert.Less(top[0].x, top[100].x);
            Assert.Less(bot[0].x, bot[100].x);
            Assert.AreEqual(top[0].y, top[100].y, 1e-5f);
            Assert.Greater(top[50].y, 0);
            Assert.Less(bot[50].y, 0);
            Assert.LessOrEqual(top[0].x, -8f);
            Assert.GreaterOrEqual(top[100].x, 8f);
        }

        [Test]
        public void まっすぐの帯は少し斜めにできて上下の傾きは同じ()
        {
            var top = ObiPlan.StraightPoints(true, 100, 8f, 2f, ObiPlan.StraightSlant);
            var bot = ObiPlan.StraightPoints(false, 100, 8f, 2f, ObiPlan.StraightSlant);
            Assert.Greater(top[0].y, top[100].y);   // 左が高く、右へ下がる
            float st = (top[100].y - top[0].y) / (top[100].x - top[0].x);
            float sb = (bot[100].y - bot[0].y) / (bot[100].x - bot[0].x);
            Assert.AreEqual(st, sb, 1e-5f);
            Assert.AreEqual(2f, top[50].y, 1e-4f);
            Assert.AreEqual(-2f, bot[50].y, 1e-4f);
        }

        [Test]
        public void 帯の点は左から右に並ぶ()
        {
            var top = ObiPlan.ArcPoints(ObiPlan.Top, true, 100);
            var bottom = ObiPlan.ArcPoints(ObiPlan.Bottom, false, 100);
            Assert.AreEqual(101, top.Length);
            // 回しを戻すと x が増えていく
            var un = Quaternion.Euler(0, 0, 32);
            Assert.Less((un * (top[0] - (Vector3)ObiPlan.Top.Center)).x, (un * (top[100] - (Vector3)ObiPlan.Top.Center)).x);
            Assert.Less((un * (bottom[0] - (Vector3)ObiPlan.Bottom.Center)).x, (un * (bottom[100] - (Vector3)ObiPlan.Bottom.Center)).x);
            // 上の帯は奥（z>0）、下の帯は手前（z<0）
            Assert.Greater(top[50].z, 0);
            Assert.Less(bottom[50].z, 0);
        }
    }
}
