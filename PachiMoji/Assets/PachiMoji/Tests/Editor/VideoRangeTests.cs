using NUnit.Framework;

namespace PachiMoji.Tests
{
    /// <summary>背景の動画：写真か動画かの見分けと、使う区間の決め方</summary>
    public class VideoRangeTests
    {
        [Test]
        public void 拡張子で動画を見分ける()
        {
            Assert.IsTrue(MediaKind.IsVideo("a/b/photo_1.mp4"));
            Assert.IsTrue(MediaKind.IsVideo("a/b/photo_1.MOV"));
            Assert.IsTrue(MediaKind.IsVideo("x.m4v"));
            Assert.IsFalse(MediaKind.IsVideo("a/b/photo_1.jpg"));
            Assert.IsFalse(MediaKind.IsVideo(""));
            Assert.IsFalse(MediaKind.IsVideo(null));
        }

        [Test]
        public void 区間は動画の長さの中に収める()
        {
            var r = VideoRange.Clamp(-3, 99, 12);
            Assert.AreEqual(0f, r.start);
            Assert.AreEqual(12f, r.end);
        }

        [Test]
        public void 区間は最短の長さを保つ()
        {
            var r = VideoRange.Clamp(5, 5.1f, 12);
            Assert.AreEqual(VideoRange.MinLength, r.end - r.start, 1e-4f);
            var e = VideoRange.Clamp(11.9f, 12, 12);   // 終わりの近くでも、はじめを前へずらして最短を保つ
            Assert.AreEqual(12f, e.end, 1e-4f);
            Assert.AreEqual(VideoRange.MinLength, e.end - e.start, 1e-4f);
        }

        [Test]
        public void 上限は無い()
        {
            var r = VideoRange.Clamp(0, 600, 600);
            Assert.AreEqual(600f, r.end - r.start);
        }

        [Test]
        public void 短い動画は全体を使う()
        {
            var r = VideoRange.Clamp(0, 0.2f, 0.2f);
            Assert.AreEqual(0f, r.start);
            Assert.AreEqual(0.2f, r.end, 1e-5f);
        }
    }
}
