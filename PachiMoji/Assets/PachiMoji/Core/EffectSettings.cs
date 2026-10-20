using UnityEngine;

namespace PachiMoji
{
    public enum LightColor { IceBlue, Gold, Red, Rainbow }   // プチュンの光の色：青白・金・赤・虹
    public enum LightSize { Small, Medium, Large }
    public enum TextFont { ReggaeOne, Mincho }
    public enum FirstText { Silver, Blue, Green, Red, Purple }   // 最初（銀の段）の文字の色
    public enum FirstBack { Blue, Green, Red, Purple }           // 最初（銀の段）の背景の色

    /// <summary>最初の色の作り方：文字は銀の明るさの並びに色の芯を乗せ、背景は今の青の背景の色相を回す</summary>
    public static class FirstColors
    {
        /// <summary>文字の色の芯（null は今の銀のまま）</summary>
        public static string TextBase(FirstText c) => c switch
        {
            FirstText.Blue => "#2A7BFF",
            FirstText.Green => "#19D14A",
            FirstText.Red => "#FF2030",
            FirstText.Purple => "#A040FF",
            _ => null,
        };

        /// <summary>青の背景から回す色相（度）</summary>
        public static float BackHue(FirstBack c) => c switch
        {
            FirstBack.Green => -85f,
            FirstBack.Red => 125f,
            FirstBack.Purple => 65f,
            _ => 0f,
        };
    }

    /// <summary>演出の好み。端末（PlayerPrefs）に残し、再生にも書き出しにも同じように効く</summary>
    public class EffectSettings
    {
        // 前の版の「はじまり・おわり」の保存の数（0=銀・1=金・2=虹）
        static readonly Tier[] OldMedals = { Tier.Silver, Tier.Gold, Tier.Rainbow };

        public bool SparksOn = true;
        public LightColor Light = LightColor.IceBlue;
        public LightSize Size = LightSize.Medium;
        public TextFont Font = TextFont.ReggaeOne;
        public Tier From = Tier.Silver;   // 昇格のはじまりの色
        public Tier To = Tier.Rainbow;    // 昇格のおわりの色
        public Tier[] Chain = (Tier[])PachiMoji.Chain.Default.Clone();   // 昇格の色（格の順、1色以上）。最初の色で着地
        public int TeaseCount = TeasePlan.MaxChars;   // チラ見せの文字数（0＝なし〜12。言葉より多ければ全部）
        public Tier TeaseText = Tier.Silver;          // チラ見せの文字色
        public Tier TeaseBack = Tier.Blue;            // チラ見せの背景
        public FirstText FirstText = FirstText.Silver;
        public FirstBack FirstBack = FirstBack.Blue;

        /// <summary>最初の色は、昇格のはじまりが銀のときだけ効く（銀の段の見た目を置き換える）</summary>
        public bool FirstColorsApply => From == Tier.Silver;

        /// <summary>Resources/Fonts/ の書体の名前</summary>
        public string FontName => Font == TextFont.Mincho ? "ZenOldMinchoBlack" : "ReggaeOne";

        /// <summary>プチュンの光の大きさの倍率</summary>
        public float LightScale => Size switch { LightSize.Small => 0.75f, LightSize.Large => 1.3f, _ => 1f };

        public EffectSettings Clone()
        {
            var c = (EffectSettings)MemberwiseClone();
            c.Chain = (Tier[])Chain.Clone();
            return c;
        }

        public static EffectSettings Load(string prefix = "fx.")
        {
            var d = new EffectSettings();
            var s = new EffectSettings
            {
                SparksOn = PlayerPrefs.GetInt(prefix + "sparks", 1) != 0,
                Light = (LightColor)Pick(prefix + "light", (int)d.Light, 3),
                Size = (LightSize)Pick(prefix + "size", (int)d.Size, 2),
                Font = (TextFont)Pick(prefix + "font", (int)d.Font, 1),
                From = OldMedals[Pick(prefix + "from", 0, 2)],   // 保存は 0=銀・1=金・2=虹
                To = OldMedals[Pick(prefix + "to", 2, 2)],
                FirstText = (FirstText)Pick(prefix + "firstText", (int)d.FirstText, 4),
                FirstBack = (FirstBack)Pick(prefix + "firstBack", (int)d.FirstBack, 3),
            };
            if (s.From > s.To) (s.From, s.To) = (s.To, s.From);
            s.Chain = LoadChain(prefix);
            s.TeaseCount = Pick(prefix + "teaseCount", d.TeaseCount, TeasePlan.MaxChars);
            s.TeaseText = (Tier)Pick(prefix + "teaseText", (int)d.TeaseText, (int)Tier.Rainbow);
            s.TeaseBack = (Tier)Pick(prefix + "teaseBack", (int)d.TeaseBack, (int)Tier.Rainbow);
            return s;
        }

        public void Save(string prefix = "fx.")
        {
            PlayerPrefs.SetInt(prefix + "sparks", SparksOn ? 1 : 0);
            PlayerPrefs.SetInt(prefix + "light", (int)Light);
            PlayerPrefs.SetInt(prefix + "size", (int)Size);
            PlayerPrefs.SetInt(prefix + "font", (int)Font);
            PlayerPrefs.SetInt(prefix + "from", System.Array.IndexOf(OldMedals, From));
            PlayerPrefs.SetInt(prefix + "to", System.Array.IndexOf(OldMedals, To));
            PlayerPrefs.SetInt(prefix + "chain", PachiMoji.Chain.ToMask(Chain));
            PlayerPrefs.SetInt(prefix + "teaseCount", TeaseCount);
            PlayerPrefs.SetInt(prefix + "teaseText", (int)TeaseText);
            PlayerPrefs.SetInt(prefix + "teaseBack", (int)TeaseBack);
            PlayerPrefs.SetInt(prefix + "firstText", (int)FirstText);
            PlayerPrefs.SetInt(prefix + "firstBack", (int)FirstBack);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 昇格の色：保存があればそれ（壊れていれば既定）。無くて前の版の「はじまり・おわり」（0=銀・1=金・2=虹）があれば、
        /// その範囲の銀・金・虹に読み替える
        /// </summary>
        static Tier[] LoadChain(string prefix)
        {
            if (PlayerPrefs.HasKey(prefix + "chain"))
            {
                int m = PlayerPrefs.GetInt(prefix + "chain");
                return m > 0 && m < 128 ? PachiMoji.Chain.FromMask(m) : (Tier[])PachiMoji.Chain.Default.Clone();
            }
            if (!PlayerPrefs.HasKey(prefix + "from") && !PlayerPrefs.HasKey(prefix + "to")) return (Tier[])PachiMoji.Chain.Default.Clone();
            int a = Pick(prefix + "from", 0, 2), b = Pick(prefix + "to", 2, 2);
            var list = new System.Collections.Generic.List<Tier>();
            for (int i = Mathf.Min(a, b); i <= Mathf.Max(a, b); i++) list.Add(OldMedals[i]);
            return list.ToArray();
        }

        // 保存した数字が 0〜max の外なら既定に戻す
        static int Pick(string key, int def, int max)
        {
            int v = PlayerPrefs.GetInt(key, def);
            return v < 0 || v > max ? def : v;
        }
    }
}
