using UnityEngine;

namespace PachiMoji
{
    public enum LightColor { IceBlue, Gold, Red, Rainbow }   // プチュンの光の色：青白・金・赤・虹
    public enum LightSize { Small, Medium, Large }
    public enum TextFont { ReggaeOne, Mincho }
    // 激アツの帯の選び：文字の厚み・厚みの奥の赤み・文字の後ろの光
    public enum ObiDepth { Thin, Low, Mid, Full }   // 厚い（Full）が最大。そこから薄くできる
    public enum ObiRed { None, Half, Strong }
    public enum ObiGlow { None, Gold, Fire }
    public enum ObiGlowAmount { Low, Mid, High }
    public enum ObiSpin { None, Slow, Fast }      // 激アツの帯の字の回転：なし・ゆっくり・速い（1文字ずつ順番に回る）
    public enum ObiLayout { Slant, Straight }   // 激アツの帯の置き方：斜め・上下をまっすぐ（縦・横どちらの書き出しにも効く）
    /// <summary>演出の好み。端末（PlayerPrefs）に残し、再生にも書き出しにも同じように効く</summary>
    public class EffectSettings
    {
        // 前の版の「はじまり・おわり」の保存の数（0=銀・1=金・2=虹）
        static readonly Tier[] OldMedals = { Tier.Silver, Tier.Gold, Tier.Rainbow };

        public bool SparksOn = true;
        public LightColor Light = LightColor.IceBlue;
        public LightSize Size = LightSize.Medium;
        public TextFont Font = TextFont.ReggaeOne;
        public Tier[] Chain = (Tier[])PachiMoji.Chain.Default.Clone();   // 昇格の色（格の順、1色以上）。最初の色で着地
        public int TeaseCount = TeasePlan.MaxChars;   // チラ見せの文字数（0＝なし〜12。言葉より多ければ全部）
        public Tier TeaseText = Tier.Silver;          // チラ見せの文字色
        public Tier TeaseBack = Tier.Blue;            // チラ見せの背景
        public string EffectId = "oudou";             // 選んでいる演出（知らない ID は EffectCatalog.Get が最初の演出にする）
        public string PhotoPath = "";                 // 激アツの帯の背景の写真（端末の中に写したもの。空＝なし）
        public Vector2 PhotoOffset;                   // 写真のずらし（書き出しの枠の幅を 1 とする。PhotoFit）
        public float PhotoZoom = 1;                   // 写真の倍率（1〜4）
        public float VideoStart, VideoEnd;            // 背景が動画のとき、使う区間（秒）。書き出しはこの長さ
        public int VideoRotation;                     // 背景の動画を表示するとき回す角度（時計回りの度。縦に撮った動画は 90）
        public ObiDepth ObiDepth = ObiDepth.Full;     // 激アツの帯：文字の厚み
        public ObiRed ObiRed = ObiRed.None;           // 激アツの帯：厚みの奥の赤み
        public ObiGlow ObiGlow = ObiGlow.None;        // 激アツの帯：文字の後ろの光（なし・金の光・炎）
        public ObiGlowAmount ObiGlowAmount = ObiGlowAmount.Mid;   // 激アツの帯：光の量（広さと明るさ）
        public bool ObiBlueBack;                         // 激アツの帯：背景を青一色にする（ブルーバック。写真・模様は出さない。金の字と反対の色で抜きやすい）
        // 激アツの帯：上の列・下の列のずらし（画面の高さを 10 とする単位）と大きさ（指で合わせる）
        public Vector2 ObiTopOffset, ObiBottomOffset;
        public ObiLayout ObiLayout = ObiLayout.Slant;   // 激アツの帯：帯の置き方
        public ObiSpin ObiSpin = ObiSpin.None;        // 激アツの帯：字の回転
        /// <summary>10 秒に回る回数（整数。くり返しでつなぎ目が出ない）</summary>
        public int ObiSpinCount => ObiSpin switch { ObiSpin.Slow => 2, ObiSpin.Fast => 4, _ => 0 };
        public bool ExportWide;                       // 書き出す動画の向き（false＝縦 1080×1920、true＝横 1920×1080）。どの演出にも効く
        public float ObiTopScale = 1, ObiBottomScale = 1;

        public const float ObiScaleMin = 0.5f, ObiScaleMax = 2f, ObiOffsetMax = 6f;
        public static float ClampObiScale(float s) => Mathf.Clamp(s, ObiScaleMin, ObiScaleMax);
        public static Vector2 ClampObiOffset(Vector2 o) => new Vector2(Mathf.Clamp(o.x, -ObiOffsetMax, ObiOffsetMax), Mathf.Clamp(o.y, -ObiOffsetMax, ObiOffsetMax));

        /// <summary>光の帯の幅の倍率（少なめは字の後ろに隠れるくらい、多めは字の上下へはみ出す）</summary>
        public float ObiGlowWidth => ObiGlowAmount switch { ObiGlowAmount.Low => 1f, ObiGlowAmount.High => 1.9f, _ => 1.45f };
        /// <summary>光の明るさの倍率</summary>
        public float ObiGlowBright => ObiGlowAmount switch { ObiGlowAmount.Low => 0.8f, ObiGlowAmount.High => 1.25f, _ => 1f };

        /// <summary>帯の文字の厚み（字の高さ 1000 に対して）</summary>
        public float ObiDepthValue => ObiDepth switch { ObiDepth.Thin => 150f, ObiDepth.Low => 300f, ObiDepth.Mid => 460f, _ => 620f };
        /// <summary>帯の厚みの奥の赤み（0〜1）</summary>
        public float ObiRedValue => ObiRed == ObiRed.Half ? 0.5f : ObiRed == ObiRed.Strong ? 1f : 0f;

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
            };
            s.Chain = LoadChain(prefix);
            s.TeaseCount = Pick(prefix + "teaseCount", d.TeaseCount, TeasePlan.MaxChars);
            s.TeaseText = (Tier)Pick(prefix + "teaseText", (int)d.TeaseText, (int)Tier.Rainbow);
            s.TeaseBack = (Tier)Pick(prefix + "teaseBack", (int)d.TeaseBack, (int)Tier.Rainbow);
            s.EffectId = PlayerPrefs.GetString(prefix + "effect", d.EffectId);
            s.PhotoPath = PlayerPrefs.GetString(prefix + "photo", "");
            s.PhotoOffset = new Vector2(PlayerPrefs.GetFloat(prefix + "photoX", 0), PlayerPrefs.GetFloat(prefix + "photoY", 0));
            s.PhotoZoom = PhotoFit.ClampZoom(PlayerPrefs.GetFloat(prefix + "photoZoom", 1));
            s.VideoStart = Mathf.Max(0, PlayerPrefs.GetFloat(prefix + "videoStart", 0));
            s.VideoEnd = Mathf.Max(0, PlayerPrefs.GetFloat(prefix + "videoEnd", 0));
            s.VideoRotation = ((PlayerPrefs.GetInt(prefix + "videoRot", 0) % 360) + 360) % 360;
            s.ObiDepth = (ObiDepth)Pick(prefix + "obiDepth", (int)d.ObiDepth, 3);
            s.ObiRed = (ObiRed)Pick(prefix + "obiRed", (int)d.ObiRed, 2);
            s.ObiGlow = (ObiGlow)Pick(prefix + "obiGlow", (int)d.ObiGlow, 2);
            s.ObiGlowAmount = (ObiGlowAmount)Pick(prefix + "obiGlowAmount", (int)d.ObiGlowAmount, 2);
            s.ObiBlueBack = PlayerPrefs.GetInt(prefix + "obiBlueBack", 0) != 0;
            s.ExportWide = PlayerPrefs.GetInt(prefix + "exportWide", 0) != 0;
            s.ObiLayout = (ObiLayout)Pick(prefix + "obiLayout", 0, 1);
            s.ObiSpin = (ObiSpin)Pick(prefix + "obiSpin", 0, 2);
            s.ObiTopOffset = ClampObiOffset(new Vector2(PlayerPrefs.GetFloat(prefix + "obiTopX", 0), PlayerPrefs.GetFloat(prefix + "obiTopY", 0)));
            s.ObiBottomOffset = ClampObiOffset(new Vector2(PlayerPrefs.GetFloat(prefix + "obiBotX", 0), PlayerPrefs.GetFloat(prefix + "obiBotY", 0)));
            s.ObiTopScale = ClampObiScale(PlayerPrefs.GetFloat(prefix + "obiTopS", 1));
            s.ObiBottomScale = ClampObiScale(PlayerPrefs.GetFloat(prefix + "obiBotS", 1));
            return s;
        }

        public void Save(string prefix = "fx.")
        {
            PlayerPrefs.SetInt(prefix + "sparks", SparksOn ? 1 : 0);
            PlayerPrefs.SetInt(prefix + "light", (int)Light);
            PlayerPrefs.SetInt(prefix + "size", (int)Size);
            PlayerPrefs.SetInt(prefix + "font", (int)Font);
            PlayerPrefs.SetInt(prefix + "chain", PachiMoji.Chain.ToMask(Chain));
            PlayerPrefs.SetInt(prefix + "teaseCount", TeaseCount);
            PlayerPrefs.SetInt(prefix + "teaseText", (int)TeaseText);
            PlayerPrefs.SetInt(prefix + "teaseBack", (int)TeaseBack);
            PlayerPrefs.SetString(prefix + "effect", EffectId);
            PlayerPrefs.SetString(prefix + "photo", PhotoPath ?? "");
            PlayerPrefs.SetFloat(prefix + "photoX", PhotoOffset.x);
            PlayerPrefs.SetFloat(prefix + "photoY", PhotoOffset.y);
            PlayerPrefs.SetFloat(prefix + "photoZoom", PhotoZoom);
            PlayerPrefs.SetFloat(prefix + "videoStart", VideoStart);
            PlayerPrefs.SetFloat(prefix + "videoEnd", VideoEnd);
            PlayerPrefs.SetInt(prefix + "videoRot", VideoRotation);
            PlayerPrefs.SetInt(prefix + "obiDepth", (int)ObiDepth);
            PlayerPrefs.SetInt(prefix + "obiRed", (int)ObiRed);
            PlayerPrefs.SetInt(prefix + "obiGlow", (int)ObiGlow);
            PlayerPrefs.SetInt(prefix + "obiGlowAmount", (int)ObiGlowAmount);
            PlayerPrefs.SetInt(prefix + "obiBlueBack", ObiBlueBack ? 1 : 0);
            PlayerPrefs.SetInt(prefix + "exportWide", ExportWide ? 1 : 0);
            PlayerPrefs.SetInt(prefix + "obiLayout", (int)ObiLayout);
            PlayerPrefs.SetInt(prefix + "obiSpin", (int)ObiSpin);
            PlayerPrefs.SetFloat(prefix + "obiTopX", ObiTopOffset.x);
            PlayerPrefs.SetFloat(prefix + "obiTopY", ObiTopOffset.y);
            PlayerPrefs.SetFloat(prefix + "obiTopS", ObiTopScale);
            PlayerPrefs.SetFloat(prefix + "obiBotX", ObiBottomOffset.x);
            PlayerPrefs.SetFloat(prefix + "obiBotY", ObiBottomOffset.y);
            PlayerPrefs.SetFloat(prefix + "obiBotS", ObiBottomScale);
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
