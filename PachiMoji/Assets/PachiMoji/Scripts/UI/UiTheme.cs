using TMPro;
using UnityEngine;

namespace PachiMoji
{
    /// <summary>
    /// 画面の決まり：パチンコ台の「盤面の下」を手のひらに置く。主役は赤いチャンスボタンだけ、ほかは黒と銀。
    /// 大きさの目盛りは iPhone の pt と同じ（基準 390×844）。
    /// </summary>
    public static class UiTheme
    {
        public static readonly Color Black = Hex(0x000000);
        public static readonly Color Chrome = Hex(0xC8CDD4);     // 台枠のクロム銀（枠・入力欄・文字ボタン）
        public static readonly Color ChanceRed = Hex(0xE3001B);  // チャンスボタンの赤（PUSH だけ）
        public static readonly Color Gold = Hex(0xF2C14E);       // 演出の金（オンの印・買ったものだけ）
        public static readonly Color Muted = Hex(0x8A8F98);      // 説明文の灰

        public static readonly Vector2 ReferenceSize = new Vector2(390, 844);
        public const float BannerHeight = 62;   // 下のバナー広告の場所（順番4で広告を置く）
        public const float PushDiameter = 176;

        static TMP_FontAsset display, body;
        /// <summary>見出し・ボタン：Reggae One</summary>
        public static TMP_FontAsset Display => display ??= Load("UI/ReggaeOne SDF");
        /// <summary>説明文：BIZ UDPゴシック</summary>
        public static TMP_FontAsset Body => body ??= Load("UI/BIZUDPGothic SDF");

        static TMP_FontAsset Load(string path)
        {
            var f = Resources.Load<TMP_FontAsset>(path);
            if (f == null) Debug.LogError("[UiTheme] 書体が無い: " + path);
            return f;
        }

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1);
    }
}
