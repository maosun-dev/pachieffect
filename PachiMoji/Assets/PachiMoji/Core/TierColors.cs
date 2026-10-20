using System.Collections.Generic;
using System.Linq;

namespace PachiMoji
{
    /// <summary>背景の種類：銀は色を抜いた灰、青〜赤は青の背景の色相を回したもの、金は炎、虹は虹</summary>
    public enum BackKind { Gray, Hue, Fire, Rainbow }

    /// <summary>色の格ごとの作り方（文字の色の芯・背景・名前）</summary>
    public static class TierColors
    {
        /// <summary>銀の対応表に乗せる色の芯（銀・金・虹は自分の対応表があるので null）</summary>
        public static string TextBase(Tier t) => t switch
        {
            Tier.Blue => "#2A7BFF",
            Tier.Green => "#19D14A",
            Tier.Purple => "#A040FF",
            Tier.Red => "#FF2030",
            _ => null,
        };

        public static BackKind Back(Tier t) => t switch
        {
            Tier.Silver => BackKind.Gray,
            Tier.Gold => BackKind.Fire,
            Tier.Rainbow => BackKind.Rainbow,
            _ => BackKind.Hue,
        };

        /// <summary>青の背景から回す色相（度）。大きいほど黄色寄りに回る</summary>
        public static float BackHue(Tier t) => t switch
        {
            Tier.Green => -85f,
            Tier.Purple => 65f,
            Tier.Red => 125f,
            _ => 0f,
        };

        public static string Name(Tier t) => "銀青緑紫赤金虹".Substring((int)t, 1);
    }

    /// <summary>昇格の色の組み合わせ：通る色を格の順に並べたもの（1色以上）。保存は色ごとの印（銀=1、青=2…虹=64）</summary>
    public static class Chain
    {
        public static readonly Tier[] Default = { Tier.Silver, Tier.Gold, Tier.Rainbow };

        public static int ToMask(IEnumerable<Tier> chain) => chain.Aggregate(0, (m, t) => m | (1 << (int)t));

        /// <summary>印から格の順に並べる。空なら既定</summary>
        public static Tier[] FromMask(int mask)
        {
            var list = new List<Tier>();
            for (int i = 0; i <= (int)Tier.Rainbow; i++) if ((mask & (1 << i)) != 0) list.Add((Tier)i);
            return list.Count > 0 ? list.ToArray() : (Tier[])Default.Clone();
        }

        /// <summary>メダルを押したとき：入っていれば外す（最後の1色は外さない）、無ければ足す</summary>
        public static Tier[] Toggle(Tier[] chain, Tier t)
        {
            int mask = ToMask(chain);
            int next = mask ^ (1 << (int)t);
            return next == 0 ? FromMask(mask) : FromMask(next);
        }

        /// <summary>「緑 → 赤 → 虹」。1色なら「緑だけ」</summary>
        public static string Label(Tier[] chain) =>
            chain.Length == 1 ? TierColors.Name(chain[0]) + "だけ" : string.Join(" → ", chain.Select(TierColors.Name));
    }
}
