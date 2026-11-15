using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PachiMoji.EditorTools
{
    /// <summary>
    /// 確認用：激アツの帯を撮って captures/ に保存する（sh obi.sh）。
    /// obi_9x19.png・obi_9x16.png（写真なし）、obi_photo_9x16.png（写真あり）、obi_t*.png（0・2.5・5・7.5・10 秒）
    /// 写真は環境変数 PACHI_PHOTO の画像。無ければ確認用の絵（灰色の地に顔の位置の丸）を作って使う
    /// </summary>
    public static class ObiCapture
    {
        static readonly string[] Words = { "緋弾", "覚醒" };   // 参考の帯と同じ言葉で見比べる

        public static void Run()
        {
            int code = 0;
            try
            {
                string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../captures"));
                Directory.CreateDirectory(outDir);
                var settings = new EffectSettings { EffectId = "obi" };
                Shoot(Path.Combine(outDir, "obi_9x19.png"), 1080, 2340, 2, 0, settings, false);
                Shoot(Path.Combine(outDir, "obi_9x16.png"), 1080, 1920, 2, 0, settings, false);
                Shoot(Path.Combine(outDir, "obi_photo_9x16.png"), 1080, 1920, 2, 0, settings, true);
                // 帯の選びの見比べ：厚み（うすい・やや薄い・ふつう。厚いが既定）、奥の赤み（少し・強い）、後ろの光（金の光・炎）
                foreach (var d in new[] { ObiDepth.Thin, ObiDepth.Low, ObiDepth.Mid })
                    Shoot(Path.Combine(outDir, $"obi_depth_{d}.png"), 1080, 2340, 2, 0, new EffectSettings { EffectId = "obi", ObiDepth = d }, false);
                Shoot(Path.Combine(outDir, "obi_red_half.png"), 1080, 2340, 2, 0, new EffectSettings { EffectId = "obi", ObiRed = ObiRed.Half }, false);
                Shoot(Path.Combine(outDir, "obi_red_strong.png"), 1080, 2340, 2, 0, new EffectSettings { EffectId = "obi", ObiRed = ObiRed.Strong }, false);
                Shoot(Path.Combine(outDir, "obi_glow_gold.png"), 1080, 1920, 2, 0, new EffectSettings { EffectId = "obi", ObiGlow = ObiGlow.Gold }, true);
                Shoot(Path.Combine(outDir, "obi_glow_fire.png"), 1080, 1920, 2, 0, new EffectSettings { EffectId = "obi", ObiGlow = ObiGlow.Fire }, true);
                Shoot(Path.Combine(outDir, "obi_16x9.png"), 1920, 1080, 2, 0, settings, false);
                Shoot(Path.Combine(outDir, "obi_16x9_photo.png"), 1920, 1080, 2, 0, settings, true);
                Shoot(Path.Combine(outDir, "obi_9x16_straight.png"), 1080, 1920, 2, 0, new EffectSettings { EffectId = "obi", ObiLayout = ObiLayout.Straight }, true);
                for (float t = 0; t < 2.6f; t += 0.2f)
                    Shoot(Path.Combine(outDir, $"obi_spin_{t:0.0}.png"), 540, 960, 1, t, new EffectSettings { EffectId = "obi", ObiSpin = ObiSpin.Slow, ObiLayout = ObiLayout.Straight }, false);
                Shoot(Path.Combine(outDir, "obi_16x9_straight.png"), 1920, 1080, 2, 0, new EffectSettings { EffectId = "obi", ObiLayout = ObiLayout.Straight }, true);
                Shoot(Path.Combine(outDir, "obi_fit.png"), 1080, 2340, 2, 0, new EffectSettings { EffectId = "obi", ObiTopOffset = new Vector2(0, 0.8f), ObiTopScale = 0.8f, ObiBottomScale = 1.2f }, false);
                Shoot(Path.Combine(outDir, "obi_blueback.png"), 1080, 1920, 2, 0, new EffectSettings { EffectId = "obi", ObiBlueBack = true }, true);
                foreach (var g in new[] { ObiGlow.Gold, ObiGlow.Fire })
                    foreach (var k in new[] { ObiGlowAmount.Low, ObiGlowAmount.High })
                        Shoot(Path.Combine(outDir, $"obi_glow_{g}_{k}.png"), 1080, 1920, 2, 0, new EffectSettings { EffectId = "obi", ObiGlow = g, ObiGlowAmount = k }, true);
                foreach (float t in new[] { 0f, 2.5f, 5f, 7.5f, 10f })
                    Shoot(Path.Combine(outDir, $"obi_t{t:0.0}.png"), 540, 1170, 1, t, settings, false);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        /// <summary>確認用の写真：PACHI_PHOTO の画像か、灰色の地に顔の位置の丸（3:4 の横長寄り）</summary>
        public static Texture2D TestPhoto()
        {
            string path = Environment.GetEnvironmentVariable("PACHI_PHOTO");
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var t = new Texture2D(2, 2);
                t.LoadImage(File.ReadAllBytes(path));
                return t;
            }
            int w = 900, h = 1200;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - w * 0.5f) / (w * 0.22f), dy = (y - h * 0.55f) / (w * 0.28f);
                    bool face = dx * dx + dy * dy < 1;
                    byte g = (byte)(60 + 80 * y / h);
                    px[y * w + x] = face ? new Color32(220, 180, 150, 255) : new Color32(g, (byte)(g + 10), (byte)(g + 30), 255);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static void Shoot(string file, int w, int h, int ss, float t, EffectSettings settings, bool withPhoto)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // 新しい場面を作ると、それより前に作った画像は消されるので、写真はここで作る
            var photo = withPhoto ? TestPhoto() : null;
            var st = ObiStage.Build(Words, (float)w / h, settings, photo);
            st.Evaluate(t);
            var big = new RenderTexture(w * ss, h * ss, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var small = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            st.cam.targetTexture = big;
            st.cam.Render();
            Graphics.Blit(big, small);
            RenderTexture.active = small;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(file, tex.EncodeToPNG());
            st.cam.targetTexture = null;
            big.Release(); small.Release();
            st.Dispose();
            if (photo != null) UnityEngine.Object.DestroyImmediate(photo);
            Debug.Log($"[ObiCapture] saved {Path.GetFileName(file)} t={t}");
        }
    }
}
