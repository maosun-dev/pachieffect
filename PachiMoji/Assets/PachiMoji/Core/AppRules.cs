using UnityEngine;

namespace PachiMoji
{
    /// <summary>アプリの状態：入力を待つ・再生中・終わって止まっている・設定画面・書き出し中・書き出しできた</summary>
    public enum AppState { Idle, Playing, Finished, Settings, Exporting, Exported, Shop }

    /// <summary>再生の時計。裏に回して戻っても、続きから流れる（終わりに飛ばない）</summary>
    public static class PlayClock
    {
        public const float MaxStep = 0.1f;   // 1回に進める時間の上限（秒）

        public static float Step(float t, float dt) => t + Mathf.Clamp(dt, 0f, MaxStep);
    }

    /// <summary>画面の自動消灯：再生中と書き出し中だけ消さない</summary>
    public static class SleepPolicy
    {
        public static int For(AppState s) => s == AppState.Playing || s == AppState.Exporting ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
    }

    /// <summary>
    /// 舞台の絵を、画面の一部（範囲、0〜1）に縮めて置く投影。
    /// URP はカメラを重ねるとき後のカメラの描く範囲を変えられないので、舞台のカメラは画面いっぱいに描き、
    /// 投影のほうで範囲に収める（範囲の外は背景を消した黒のまま）。
    /// </summary>
    public static class StageFraming
    {
        /// <param name="aspect">範囲の横÷縦（画素で）</param>
        public static Matrix4x4 Projection(float fovDeg, float aspect, float near, float far, Rect viewport)
        {
            var p = Matrix4x4.Perspective(fovDeg, aspect, near, far);
            // 画面全体（-1〜1）を、範囲の中へ縮めて動かす
            var m = Matrix4x4.TRS(new Vector3(viewport.center.x * 2 - 1, viewport.center.y * 2 - 1, 0), Quaternion.identity,
                new Vector3(viewport.width, viewport.height, 1));
            return m * p;
        }
    }
}
