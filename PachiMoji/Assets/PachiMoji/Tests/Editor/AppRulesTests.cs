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
        }

        [Test]
        public void なぞった向きに関係なく小さい方がはじまり()
        {
            Assert.AreEqual((Tier.Silver, Tier.Rainbow), RangePicker.FromDrag(2, 0));
            Assert.AreEqual((Tier.Gold, Tier.Gold), RangePicker.FromDrag(1, 1));
            Assert.AreEqual((Tier.Gold, Tier.Rainbow), RangePicker.FromDrag(1, 5));
            Assert.AreEqual((Tier.Silver, Tier.Gold), RangePicker.FromDrag(-3, 1));
        }
    }
}
