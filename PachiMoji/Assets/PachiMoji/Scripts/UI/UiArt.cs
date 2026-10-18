using System.Collections.Generic;
using UnityEngine;

namespace PachiMoji
{
    /// <summary>
    /// 画面の絵を、既製のアイコンを使わずコードで1画素ずつ描く（シェーダーを増やさないので、iPhone 版で抜け落ちる心配がない）。
    /// 作った絵は使い回す。
    /// </summary>
    public static class UiArt
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>チャンスボタン：クロムの縁に囲まれた赤いドーム。押せないときは赤を沈める</summary>
        public static Sprite Dome(bool enabled) => Cached("dome" + enabled, () =>
        {
            const int N = 512;
            var px = new Color[N * N];
            Color center = enabled ? UiTheme.Hex(0xFF3B4E) : UiTheme.Hex(0x4A0A12);
            Color mid = enabled ? UiTheme.ChanceRed : UiTheme.Hex(0x3A0710);
            Color edge = enabled ? UiTheme.Hex(0x7A000E) : UiTheme.Hex(0x1E0306);
            float shine = enabled ? 0.55f : 0.2f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float nx = (x + 0.5f) / N * 2 - 1, ny = (y + 0.5f) / N * 2 - 1;   // ny は上がプラス
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    Color c;
                    if (r > 0.92f) c = ChromeAt(Mathf.InverseLerp(1f, 0.92f, r), ny, 0.55f);          // 外の暗いクロム
                    else if (r > 0.88f) c = ChromeAt(0.5f, ny, 1.1f);                                  // 明るいクロムの縁
                    else if (r > 0.84f) c = Color.Lerp(UiTheme.Hex(0x1A0003), UiTheme.Hex(0x050000), Mathf.InverseLerp(0.88f, 0.84f, r));   // 縁とドームのすき間の影
                    else
                    {
                        float rr = r / 0.84f;
                        c = rr < 0.7f ? Color.Lerp(center, mid, rr / 0.7f) : Color.Lerp(mid, edge, (rr - 0.7f) / 0.3f);
                        c *= 0.85f + 0.25f * ny;   // 上から光が当たる
                        // 左上の横長の照り返し
                        float hx = (nx + 0.22f) / 0.42f, hy = (ny - 0.42f) / 0.2f;
                        float h = Mathf.Clamp01(1 - (hx * hx + hy * hy));
                        c = Color.Lerp(c, Color.white, h * h * shine);
                        // 下側の照り返し（ドームの丸さ）
                        float lx = nx / 0.5f, ly = (ny + 0.62f) / 0.1f;
                        float l = Mathf.Clamp01(1 - (lx * lx + ly * ly));
                        c = Color.Lerp(c, center * 1.3f, l * 0.35f);
                    }
                    c.a = Mathf.Clamp01((1 - r) * N * 0.5f);   // ふちのギザギザを消す
                    px[y * N + x] = c;
                }
            return ToSprite(px, N, N, Vector4.zero);
        });

        /// <summary>銀メッキの枠に囲まれた、暗くくぼんだ板（入力欄・文字ボタン）。9スライス</summary>
        public static Sprite ChromePlate() => Cached("plate", () =>
        {
            const int N = 96, B = 28;
            const float Rad = 20, Bezel = 9;
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = RoundRectDist(x + 0.5f, y + 0.5f, N, N, Rad);   // 外の縁からの内側への距離
                    float ny = (y + 0.5f) / N * 2 - 1;
                    Color c;
                    if (d < Bezel)
                    {
                        // 縁の断面：外は丸く明るく、内側へ落ちる。上の辺は明るく、下の辺は暗く
                        float u = d / Bezel;
                        c = ChromeAt(u, ny, 1f);
                    }
                    else
                    {
                        // くぼみ：ほぼ黒。縁のすぐ内側に細い影
                        float s = Mathf.Clamp01((d - Bezel) / 5f);
                        c = Color.Lerp(UiTheme.Hex(0x000000), UiTheme.Hex(0x0B0D10), s);
                    }
                    c.a = Mathf.Clamp01(d + 0.5f);
                    px[y * N + x] = c;
                }
            return ToSprite(px, N, N, new Vector4(B, B, B, B));
        });

        /// <summary>設定の歯車（8枚歯、銀）</summary>
        public static Sprite Gear() => Cached("gear", () =>
        {
            const int N = 128;
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float nx = (x + 0.5f) / N * 2 - 1, ny = (y + 0.5f) / N * 2 - 1;
                    float r = Mathf.Sqrt(nx * nx + ny * ny), a = Mathf.Atan2(ny, nx);
                    // 歯：台形に近い山
                    float tooth = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.15f, 0.35f, Mathf.Cos(a * 8)));
                    float outer = Mathf.Lerp(0.74f, 0.96f, tooth);
                    float sdf = Mathf.Min(outer - r, r - 0.3f) * N * 0.5f;   // 外の歯と中の穴
                    var c = ChromeAt(Mathf.Clamp01(Mathf.Abs(r - 0.6f) / 0.36f), ny, 1f);
                    c.a = Mathf.Clamp01(sdf + 0.5f);
                    px[y * N + x] = c;
                }
            return ToSprite(px, N, N, Vector4.zero);
        });

        /// <summary>昇格の範囲のメダル：銀・金・虹</summary>
        public static Sprite Medal(Tier t) => Cached("medal" + t, () =>
        {
            const int N = 192;
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float nx = (x + 0.5f) / N * 2 - 1, ny = (y + 0.5f) / N * 2 - 1;
                    float r = Mathf.Sqrt(nx * nx + ny * ny), a = Mathf.Atan2(ny, nx);
                    Color face = t switch
                    {
                        Tier.Gold => Color.Lerp(UiTheme.Hex(0xFFE9A8), UiTheme.Hex(0xB07A12), Mathf.Clamp01(r * 0.9f - ny * 0.35f + 0.1f)),
                        Tier.Rainbow => Color.HSVToRGB(Mathf.Repeat(a / (2 * Mathf.PI) + 0.25f, 1f), 0.55f, 1f) * (1.05f - 0.35f * r),
                        _ => Color.Lerp(UiTheme.Hex(0xF4F6F8), UiTheme.Hex(0x7D838C), Mathf.Clamp01(r * 0.9f - ny * 0.35f + 0.1f)),
                    };
                    Color c;
                    if (r > 0.88f) c = face * (0.55f + 0.3f * ny);                       // 外のフチ
                    else if (r > 0.72f && r < 0.76f) c = face * 0.6f;                     // 刻んだ輪
                    else c = face;
                    // 左上の照り返し
                    float hx = (nx + 0.3f) / 0.35f, hy = (ny - 0.35f) / 0.22f;
                    float h = Mathf.Clamp01(1 - (hx * hx + hy * hy));
                    c = Color.Lerp(c, Color.white, h * 0.5f);
                    c.a = Mathf.Clamp01((1 - r) * N * 0.5f);
                    px[y * N + x] = c;
                }
            return ToSprite(px, N, N, Vector4.zero);
        });

        /// <summary>メダルをつなぐ金の帯（横に伸ばす。9スライス）</summary>
        public static Sprite Rail() => Cached("rail", () =>
        {
            const int W = 64, H = 32;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float d = RoundRectDist(x + 0.5f, y + 0.5f, W, H, H * 0.5f);
                    float v = (y + 0.5f) / H;   // 下 0 → 上 1
                    Color c = v > 0.55f ? Color.Lerp(UiTheme.Gold, UiTheme.Hex(0xFFE9A8), (v - 0.55f) / 0.45f)
                                        : Color.Lerp(UiTheme.Hex(0x9A6A0E), UiTheme.Gold, v / 0.55f);
                    c.a = Mathf.Clamp01(d + 0.5f);
                    px[y * W + x] = c;
                }
            return ToSprite(px, W, H, new Vector4(16, 0, 16, 0));
        });

        // クロムの色。u＝縁の断面の位置（0 外 → 1 内）、ny＝上下（上 +1）、gain＝明るさ
        static Color ChromeAt(float u, float ny, float gain)
        {
            // 断面：外の丸みで明るく、中ほどに暗い映り込みの帯、内側でまた少し明るい
            float band = 0.78f + 0.3f * Mathf.Cos(u * Mathf.PI * 2.2f);
            float lightTop = 0.82f + 0.2f * ny;
            var c = UiTheme.Chrome * band * lightTop * gain;
            c.a = 1;
            return c;
        }

        // 角の丸い四角の、外の縁から内側への距離（外はマイナス）
        static float RoundRectDist(float x, float y, float w, float h, float rad)
        {
            float qx = Mathf.Max(Mathf.Abs(x - w * 0.5f) - (w * 0.5f - rad), 0);
            float qy = Mathf.Max(Mathf.Abs(y - h * 0.5f) - (h * 0.5f - rad), 0);
            float corner = rad - Mathf.Sqrt(qx * qx + qy * qy);
            float side = Mathf.Min(Mathf.Min(x, w - x), Mathf.Min(y, h - y));
            return (qx > 0 && qy > 0) ? corner : Mathf.Min(side, corner);
        }

        static Sprite Cached(string key, System.Func<Sprite> make)
        {
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            s = make();
            s.name = "UiArt_" + key;
            cache[key] = s;
            return s;
        }

        static Sprite ToSprite(Color[] px, int w, int h, Vector4 border)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels(px);
            tex.Apply(false, true);   // 画素は GPU にだけ残す
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
        }
    }
}
