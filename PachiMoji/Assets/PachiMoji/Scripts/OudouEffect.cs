using UnityEngine;

namespace PachiMoji
{
    /// <summary>1つ目の演出「王道昇格」：プチュン → チラ見せ → ドン → 銀・金・虹の昇格（中身は PachiStage）</summary>
    public class OudouEffect : IPachiEffect
    {
        PachiStage stage;

        public void Build(string[] lines, float aspect, EffectSettings settings) => stage = PachiStage.Build(lines, aspect, settings);
        public float Duration => stage.End;
        public void Evaluate(float t) => stage.Evaluate(t);
        public void Idle(float t) => stage.Idle(t);
        public Camera Camera => stage.cam;

        public void Dispose()
        {
            stage?.Dispose();
            stage = null;
        }
    }
}
