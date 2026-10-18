using System.Collections.Generic;
using UnityEngine;

namespace PachiMoji
{
    /// <summary>
    /// 明るさ（0〜1）→ 色 の対応表（トーンカーブ）。途中にわざと暗い谷を入れて、ギラギラの帯を作る。
    /// </summary>
    public static class PachiRamps
    {
        static readonly Dictionary<string, (float, string)[]> Keys = new Dictionary<string, (float, string)[]>
        {
            // 金：空が映る上半分は橙金、地面が映る下半分は焦げ茶、水平線は黒。明るい側に谷を1本
            ["gold"] = new[] { (0f, "#000000"), (0.05f, "#080300"), (0.15f, "#1e0c02"), (0.25f, "#3a1a06"), (0.32f, "#5a2a08"),
                (0.45f, "#8a4a00"), (0.6f, "#e88c00"), (0.7f, "#ffb21c"), (0.78f, "#ffc838"), (0.86f, "#ffe070"),
                (0.9f, "#f0a000"), (0.95f, "#fff4b8"), (1f, "#ffffff") },
            // 外フチ：表の面より一段深い橙で、文字の形を浮かせる
            ["goldRim"] = new[] { (0f, "#000000"), (0.15f, "#2a1004"), (0.3f, "#7a3c00"), (0.5f, "#c87000"), (0.65f, "#ffa818"),
                (0.78f, "#ffd050"), (0.9f, "#fff2b8"), (1f, "#ffffff") },
            // 細い明るい線（面取り）
            ["goldLine"] = new[] { (0f, "#2a1000"), (0.3f, "#a05a00"), (0.6f, "#ffb020"), (0.8f, "#ffe080"), (1f, "#fff6d0") },
            // ツヤのある黒（溝・きわ）
            ["black"] = new[] { (0f, "#000000"), (0.6f, "#050508"), (0.8f, "#101218"), (0.95f, "#3a4050"), (1f, "#8088a0") },
            // 側面：影は紺、光の当たる面は金
            ["goldSide"] = new[] { (0f, "#05030a"), (0.15f, "#1a0c06"), (0.3f, "#5a2c00"), (0.45f, "#a05a00"), (0.6f, "#e08a00"),
                (0.75f, "#ffb428"), (0.88f, "#ffe080"), (1f, "#fff8e0") },
            // 銀
            ["silver"] = new[] { (0f, "#000000"), (0.1f, "#0a0c10"), (0.25f, "#2a303a"), (0.35f, "#4a5260"), (0.5f, "#8590a0"),
                (0.65f, "#c8d0dc"), (0.75f, "#eef2f8"), (0.82f, "#9aa4b4"), (0.9f, "#f4f8ff"), (1f, "#ffffff") },
            ["silverLine"] = new[] { (0f, "#101420"), (0.4f, "#8a96b0"), (0.75f, "#f0f4ff"), (1f, "#ffffff") },
            ["silverSide"] = new[] { (0f, "#020308"), (0.3f, "#10142a"), (0.55f, "#4a5878"), (0.75f, "#a8b4cc"), (1f, "#ffffff") },
        };

        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string name)
        {
            if (cache.TryGetValue(name, out var tex) && tex != null) return tex;
            var keys = Keys[name];
            tex = new Texture2D(256, 1, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Ramp_" + name };
            var px = new Color[256];
            for (int i = 0; i < 256; i++)
            {
                float t = i / 255f;
                int k = 0;
                while (k < keys.Length - 2 && t > keys[k + 1].Item1) k++;
                var (t0, h0) = keys[k]; var (t1, h1) = keys[k + 1];
                ColorUtility.TryParseHtmlString(h0, out var c0);
                ColorUtility.TryParseHtmlString(h1, out var c1);
                px[i] = Color.Lerp(c0, c1, Mathf.InverseLerp(t0, t1, t));
            }
            tex.SetPixels(px);
            tex.Apply();
            cache[name] = tex;
            return tex;
        }
    }
}
