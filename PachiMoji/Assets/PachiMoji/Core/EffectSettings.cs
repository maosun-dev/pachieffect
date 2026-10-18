using UnityEngine;

namespace PachiMoji
{
    public enum LightColor { IceBlue, Gold, Red, Rainbow }   // プチュンの光の色：青白・金・赤・虹
    public enum LightSize { Small, Medium, Large }
    public enum TextFont { ReggaeOne, Mincho }

    /// <summary>演出の好み。端末（PlayerPrefs）に残し、再生にも書き出しにも同じように効く</summary>
    public class EffectSettings
    {
        public bool SparksOn = true;
        public LightColor Light = LightColor.IceBlue;
        public LightSize Size = LightSize.Medium;
        public TextFont Font = TextFont.ReggaeOne;
        public Tier From = Tier.Silver;   // 昇格のはじまりの色
        public Tier To = Tier.Rainbow;    // 昇格のおわりの色

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
                From = (Tier)Pick(prefix + "from", (int)d.From, 2),
                To = (Tier)Pick(prefix + "to", (int)d.To, 2),
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
            PlayerPrefs.SetInt(prefix + "from", (int)From);
            PlayerPrefs.SetInt(prefix + "to", (int)To);
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
