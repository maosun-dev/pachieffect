using UnityEngine;

namespace PachiMoji
{
    /// <summary>
    /// すべての演出が持つ形。2つ目の演出は、この形の部品を1つ書いて EffectCatalog に1行足すだけで増える。
    /// </summary>
    public interface IPachiEffect
    {
        void Build(string[] lines, float aspect, EffectSettings settings);  // 組み立てる
        float Duration { get; }                                             // 長さ（秒）
        void Evaluate(float t);                                             // 時刻 t の姿にする（状態を持たない）
        void Idle(float t);                                                 // 入力を待っているあいだの姿（仕様書の形に足したもの）
        Camera Camera { get; }                                              // 描くカメラ
        void Dispose();                                                     // 片付ける
    }
}
