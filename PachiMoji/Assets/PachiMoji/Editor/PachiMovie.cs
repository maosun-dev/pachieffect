using System;
using System.IO;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PachiMoji.EditorTools
{
    /// <summary>
    /// 確認用：演出を1コマずつ描いて PNG の連番に書き出す（pachieffect/captures/frames）。
    /// mp4 には movie.sh が ffmpeg でまとめる（Unity の MediaEncoder は画質が足りず、
    /// 背景が細かく動く場面でブロック状に崩れ、暗転に前の絵の残像が出たため使わない）。
    /// あわせて、0.25 秒ごとのコマを並べた一覧画像も作る。
    /// コマンドから -executeMethod PachiMoji.EditorTools.PachiMovie.Run で動かす。
    /// 確認を速くするため、環境変数で書き出す範囲と画質を絞れる（movie.sh の引数から渡す）：
    ///   PACHI_FROM / PACHI_TO＝書き出す秒の範囲、PACHI_QUICK=1＝2倍で描いて縮めるのをやめる（細部は粗くなる）
    ///   PACHI_RANGE＝昇格の範囲（SS/SG/SR/GG/GR/RR。S=銀・G=金・R=虹。無ければ既定の SR）
    /// </summary>
    public static class PachiMovie
    {
        const int Fps = 30;

        public static void Run()
        {
            int code = 0;
            try
            {
                string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../captures"));
                Directory.CreateDirectory(outDir);
                float from = EnvFloat("PACHI_FROM", 0), to = EnvFloat("PACHI_TO", -1);
                bool quick = Environment.GetEnvironmentVariable("PACHI_QUICK") == "1";
                bool part = from > 0 || to >= 0;
                string range = (Environment.GetEnvironmentVariable("PACHI_RANGE") ?? "").Trim().ToUpperInvariant();
                var settings = new EffectSettings();
                string sheet = part ? "movie_part_sheet.png" : "movie_teiji_sheet.png";
                if (range.Length > 0)
                {
                    if (range.Length != 2) throw new Exception("PACHI_RANGE は2文字（例 GR）: " + range);
                    settings.From = TierOf(range[0]);
                    settings.To = TierOf(range[1]);
                    sheet = $"movie_range_{range}_sheet.png";
                }
                Render(new[] { "定時", "退社" }, 720, 1280, Path.Combine(outDir, "frames"),
                    Path.Combine(outDir, sheet), from, to, quick, settings);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static Tier TierOf(char c) => c switch
        {
            'S' => Tier.Silver,
            'G' => Tier.Gold,
            'R' => Tier.Rainbow,
            _ => throw new Exception("昇格の色は S/G/R のどれか: " + c),
        };

        static float EnvFloat(string name, float def) =>
            float.TryParse(Environment.GetEnvironmentVariable(name), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : def;

        static void Render(string[] lines, int w, int h, string frameDir, string sheetPath, float from, float to, bool quick, EffectSettings settings)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var st = PachiStage.Build(lines, (float)w / h, settings);

            // 2倍の大きさで描いて縮める（ギザギザを消す）
            int ss = quick ? 1 : 2;
            var big = new RenderTexture(w * ss, h * ss, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var small = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var frame = new Texture2D(w, h, TextureFormat.RGBA32, false);
            st.cam.targetTexture = big;

            int first = Mathf.Max(0, Mathf.FloorToInt(from * Fps));
            int last = Mathf.CeilToInt((to >= 0 ? Mathf.Min(to, st.End) : st.End) * Fps);
            int total = last - first;
            // 一覧画像：全体なら 0.25 秒ごと、範囲を絞ったときは 0.1 秒ごと
            int every = from > 0 || to >= 0 ? 3 : Fps / 4;
            int thumbs = total / every + 1, cols = 8, tw = 180, th = Mathf.RoundToInt(180f * h / w);
            int rows = (thumbs + cols - 1) / cols;
            var sheet = new Texture2D(cols * tw, rows * th, TextureFormat.RGBA32, false);
            var clear = new Color32[sheet.width * sheet.height];
            sheet.SetPixels32(clear);

            if (Directory.Exists(frameDir)) Directory.Delete(frameDir, true);
            Directory.CreateDirectory(frameDir);
            {
                for (int f = 0; f < total; f++)
                {
                    float t = (first + f) / (float)Fps;
                    st.Evaluate(t);
                    st.cam.Render();
                    Graphics.Blit(big, small);
                    var prev = RenderTexture.active;
                    RenderTexture.active = small;
                    frame.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    frame.Apply();
                    RenderTexture.active = prev;
                    File.WriteAllBytes(Path.Combine(frameDir, $"f{f:D4}.png"), frame.EncodeToPNG());

                    if (f % every == 0)
                    {
                        int i = f / every;
                        var thumb = Scale(frame, tw, th);
                        int x = (i % cols) * tw, y = (rows - 1 - i / cols) * th;
                        sheet.SetPixels(x, y, tw, th, thumb);
                    }
                }
            }
            sheet.Apply();
            File.WriteAllBytes(sheetPath, sheet.EncodeToPNG());
            st.cam.targetTexture = null;
            big.Release(); small.Release();
            Debug.Log($"[PachiMovie] saved frames={total} from={first / (float)Fps:F2} seconds={st.End:F2} quick={quick} range={settings.From}-{settings.To}");
        }

        // 縮小（近い画素を拾うだけの簡単なもの）
        static Color[] Scale(Texture2D src, int w, int h)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = src.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h);
            return px;
        }
    }
}
