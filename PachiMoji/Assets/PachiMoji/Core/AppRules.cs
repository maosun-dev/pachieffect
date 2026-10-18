using UnityEngine;

namespace PachiMoji
{
    /// <summary>アプリの状態：入力を待つ・再生中・終わって止まっている・設定画面</summary>
    public enum AppState { Idle, Playing, Finished, Settings }

    /// <summary>再生の時計。裏に回して戻っても、続きから流れる（終わりに飛ばない）</summary>
    public static class PlayClock
    {
        public const float MaxStep = 0.1f;   // 1回に進める時間の上限（秒）

        public static float Step(float t, float dt) => t + Mathf.Clamp(dt, 0f, MaxStep);
    }

    /// <summary>画面の自動消灯：再生中だけ消さない</summary>
    public static class SleepPolicy
    {
        public static int For(AppState s) => s == AppState.Playing ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
    }

    /// <summary>昇格の範囲のメダル（0=銀, 1=金, 2=虹）を指でなぞったときの範囲</summary>
    public static class RangePicker
    {
        public static (Tier from, Tier to) FromDrag(int downIndex, int upIndex)
        {
            int a = Mathf.Clamp(downIndex, 0, 2), b = Mathf.Clamp(upIndex, 0, 2);
            return ((Tier)Mathf.Min(a, b), (Tier)Mathf.Max(a, b));
        }
    }
}
