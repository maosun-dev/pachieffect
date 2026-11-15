using NUnit.Framework;
using UnityEngine;

namespace PachiMoji.Tests
{
    /// <summary>写真を書き出しの枠（9:16、幅を 1 とする）に合わせる計算</summary>
    public class PhotoFitTests
    {
        const float FrameH = 16f / 9f;   // 枠の高さ（幅 1 のとき）

        [Test]
        public void 横長の写真は高さで合わせる()
        {
            var s = PhotoFit.Size(4f / 3f, 1);
            Assert.AreEqual(FrameH, s.y, 1e-5f);
            Assert.AreEqual(FrameH * 4f / 3f, s.x, 1e-5f);
        }

        [Test]
        public void 縦長の写真は幅で合わせる()
        {
            // 枠（9:16）より縦長の写真（横÷縦 0.4）は、幅を枠に合わせる
            var s = PhotoFit.Size(0.4f, 1);
            Assert.AreEqual(1f, s.x, 1e-5f);
            Assert.AreEqual(2.5f, s.y, 1e-5f);
        }

        [Test]
        public void どんな写真でも枠を覆う([Values(0.3f, 0.5f, 9f / 16f, 1f, 4f / 3f, 3f)] float img, [Values(1f, 2.5f)] float zoom)
        {
            var s = PhotoFit.Size(img, zoom);
            Assert.GreaterOrEqual(s.x, 1f - 1e-5f);
            Assert.GreaterOrEqual(s.y, FrameH - 1e-5f);
            Assert.AreEqual(img, s.x / s.y, 1e-4f);
        }

        [Test]
        public void ずらしは枠を覆う範囲に収まる()
        {
            var s = PhotoFit.Size(4f / 3f, 1);
            var o = PhotoFit.ClampOffset(new Vector2(5, 5), 4f / 3f, 1);
            Assert.AreEqual((s.x - 1) / 2, o.x, 1e-5f);
            Assert.AreEqual((s.y - FrameH) / 2, o.y, 1e-5f);
            var o2 = PhotoFit.ClampOffset(new Vector2(-5, -5), 4f / 3f, 1);
            Assert.AreEqual(-(s.x - 1) / 2, o2.x, 1e-5f);
        }

        [Test]
        public void 倍率は1から4()
        {
            Assert.AreEqual(1f, PhotoFit.ClampZoom(0.3f));
            Assert.AreEqual(4f, PhotoFit.ClampZoom(9f));
            Assert.AreEqual(2f, PhotoFit.ClampZoom(2f));
        }
    }
}
