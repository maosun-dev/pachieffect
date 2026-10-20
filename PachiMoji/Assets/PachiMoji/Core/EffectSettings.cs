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
        public bool SparksOn = true;
        public LightColor Light = LightColor.IceBlue;
        public LightSize Size = LightSize.Medium;
        public TextFont Font = TextFont.ReggaeOne;
        public Tier From = Tier.Silver;   // 昇格のはじまりの色
        public Tier To = Tier.Rainbow;    // 昇格のおわりの色
        public FirstText FirstText = FirstText.Silver;
        public FirstBack FirstBack = FirstBack.Blue;

        /// <summary>最初の色は、昇格のはじまりが銀のときだけ効く（銀の段の見た目を置き換える）</summary>
        public bool FirstColorsApply => From == Tier.Silver;

        /// <summary>Resources/Fonts/ の書体の名前</summary>
        public string FontName => Font == TextFont.Mincho ? "ZenOldMinchoBlack" : "ReggaeOne";

        /// <summary>プチュンの光の大きさの倍率</summary>
        public float LightScale => Size switch { LightSize.Small => 0.75f, LightSize.Large => 1.3f, _ => 1f };

        public EffectSettings Clone() => (EffectSettings)MemberwiseClone();

        public static EffectSettings Load(string prefix = "fx.")
        {
            var d = new EffectSettings();
            var s = new EffectSettings
            {
                SparksOn = PlayerPrefs.GetInt(prefix + "sparks", 1) != 0,
                Light = (LightColor)Pick(prefix + "light", (int)d.Light, 3),
                Size = (LightSize)Pick(prefix + "size", (int)d.Size, 2),
                Font = (TextFont)Pick(prefix + "font", (int)d.Font, 1),
                From = RangePicker.Medals[Pick(prefix + "from", 0, 2)],   // 保存は 0=銀・1=金・2=虹
                To = RangePicker.Medals[Pick(prefix + "to", 2, 2)],
                FirstText = (FirstText)Pick(prefix + "firstText", (int)d.FirstText, 4),
                FirstBack = (FirstBack)Pick(prefix + "firstBack", (int)d.FirstBack, 3),
            };
            if (s.From > s.To) (s.From, s.To) = (s.To, s.From);
            return s;
        }

        public void Save(string prefix = "fx.")
        {
            PlayerPrefs.SetInt(prefix + "sparks", SparksOn ? 1 : 0);
            PlayerPrefs.SetInt(prefix + "light", (int)Light);
            PlayerPrefs.SetInt(prefix + "size", (int)Size);
            PlayerPrefs.SetInt(prefix + "font", (int)Font);
            PlayerPrefs.SetInt(prefix + "from", System.Array.IndexOf(RangePicker.Medals, From));
            PlayerPrefs.SetInt(prefix + "to", System.Array.IndexOf(RangePicker.Medals, To));
            PlayerPrefs.SetInt(prefix + "firstText", (int)FirstText);
            PlayerPrefs.SetInt(prefix + "firstBack", (int)FirstBack);
            PlayerPrefs.Save();
        }

        // 保存した数字が 0〜max の外なら既定に戻す
        static int Pick(string key, int def, int max)
        {
            int v = PlayerPrefs.GetInt(key, def);
            return v < 0 || v > max ? def : v;
        }
    }
}
