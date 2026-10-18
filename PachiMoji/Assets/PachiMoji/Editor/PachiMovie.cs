using System;
using System.IO;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PachiMoji.EditorTools
{
    /// <summary>
    /// 確認用：演出を1コマずつ描いて mp4 に書き出す（pachieffect/captures）。
    /// あわせて、0.25 秒ごとのコマを並べた一覧画像も作る。
    /// コマンドから -executeMethod PachiMoji.EditorTools.PachiMovie.Run で動かす。
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
                Render(new[] { "定時", "退社" }, 720, 1280, Path.Combine(outDir, "movie_teiji.mp4"), Path.Combine(outDir, "movie_teiji_sheet.png"));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static void Render(string[] lines, int w, int h, string mp4, string sheetPath)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var st = PachiStage.Build(lines, (float)w / h);

            // 2倍の大きさで描いて縮める（ギザギザを消す）
            var big = new RenderTexture(w * 2, h * 2, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var small = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var frame = new Texture2D(w, h, TextureFormat.RGBA32, false);
            st.cam.targetTexture = big;

            int total = Mathf.CeilToInt(st.End * Fps);
            // 一覧画像：0.25 秒ごと
            int every = Fps / 4;
            int thumbs = total / every + 1, cols = 8, tw = 180, th = Mathf.RoundToInt(180f * h / w);
            int rows = (thumbs + cols - 1) / cols;
            var sheet = new Texture2D(cols * tw, rows * th, TextureFormat.RGBA32, false);
            var clear = new Color32[sheet.width * sheet.height];
            sheet.SetPixels32(clear);

            var attr = new VideoTrackAttributes
            {
                frameRate = new MediaRational(Fps),
                width = (uint)w,
                height = (uint)h,
                includeAlpha = false,
                bitRateMode = VideoBitrateMode.High,
            };
            if (File.Exists(mp4)) File.Delete(mp4);
            using (var enc = new MediaEncoder(mp4, attr))
            {
                for (int f = 0; f < total; f++)
                {
                    float t = f / (float)Fps;
                    st.Evaluate(t);
                    st.cam.Render();
                    Graphics.Blit(big, small);
                    var prev = RenderTexture.active;
                    RenderTexture.active = small;
                    frame.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    frame.Apply();
                    RenderTexture.active = prev;
                    enc.AddFrame(frame);

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
            Debug.Log($"[PachiMovie] saved {Path.GetFileName(mp4)} frames={total} seconds={st.End:F2}");
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
