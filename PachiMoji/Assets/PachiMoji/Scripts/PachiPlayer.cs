using UnityEngine;

namespace PachiMoji
{
    /// <summary>
    /// アプリ化の第1歩：起動したら固定の言葉で演出を組み立て、くり返し再生するだけ。
    /// 終わりの姿で Gap 秒止まってから、頭に戻る。
    /// </summary>
    public class PachiPlayer : MonoBehaviour
    {
        public static readonly string[] Words = { "定時", "退社" };
        public const float Gap = 1.5f;   // 終わりの姿で止まっている秒数

        PachiStage stage;

        /// <summary>経過時間を、くり返し再生の中の時刻にする（終わり〜次の頭までは終わりの姿のまま）</summary>
        public static float LoopTime(float elapsed, float end, float gap)
        {
            float c = elapsed % (end + gap);
            return Mathf.Min(c, end);
        }

        void Start()
        {
            Application.targetFrameRate = 60;
            stage = PachiStage.Build(Words, (float)Screen.width / Screen.height);
        }

        void Update()
        {
            stage.Evaluate(LoopTime(Time.timeSinceLevelLoad, stage.End, Gap));
        }
    }
}
