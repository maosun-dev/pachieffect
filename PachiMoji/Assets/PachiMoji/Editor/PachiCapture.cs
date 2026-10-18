using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PachiMoji.EditorTools
{
    /// <summary>
    /// 確認用：文字を置いて画像に撮り、pachieffect/captures に保存する。
    /// コマンドから -executeMethod PachiMoji.EditorTools.PachiCapture.Run で動かす。
    /// </summary>
    public static class PachiCapture
    {
        struct Shot
        {
            public string file; public string[] lines; public PachiText.Finish finish; public int w, h;
            public float yaw, pitch; public string font;
        }

        public static void Run()
        {
            int code = 0;
            try
            {
                var shots = new[]
                {
                    new Shot { file = "kakutei_gold", lines = new[] { "確定" }, finish = PachiText.Finish.Gold, w = 1600, h = 900, yaw = -10, pitch = 6 },
                    new Shot { file = "kakutei_rainbow", lines = new[] { "確定" }, finish = PachiText.Finish.Rainbow, w = 1600, h = 900, yaw = 8, pitch = 6 },
                    new Shot { file = "gekiatsu_gold", lines = new[] { "激アツ" }, finish = PachiText.Finish.Gold, w = 1600, h = 900, yaw = -8, pitch = 4 },
                    new Shot { file = "teiji_rainbow", lines = new[] { "定時", "退社" }, finish = PachiText.Finish.Rainbow, w = 1080, h = 1920, yaw = -8, pitch = 6 },
                    new Shot { file = "kakutei_silver_mincho", lines = new[] { "確定" }, finish = PachiText.Finish.Silver, w = 1600, h = 900, yaw = 10, pitch = 6, font = "ZenOldMinchoBlack" },
                };
                string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../captures"));
                Directory.CreateDirectory(outDir);
                foreach (var s in shots) Capture(s, outDir);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static void Capture(Shot s, string outDir)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 文字
            var text = PachiText.Create(s.lines, s.finish, s.font ?? "ReggaeOne");
            text.transform.rotation = Quaternion.Euler(s.pitch, s.yaw, 0);
            var b = text.GetComponent<MeshRenderer>().bounds;

            // カメラ：少し下から見上げる
            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 28;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 100;
            float aspect = (float)s.w / s.h;
            float fitH = Mathf.Max(b.size.y, b.size.x / aspect) * 1.25f;
            float dist = fitH * 0.5f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            camGo.transform.position = b.center + new Vector3(0, -fitH * 0.12f, -dist);
            camGo.transform.LookAt(b.center + new Vector3(0, fitH * 0.02f, 0));
            var acd = cam.GetUniversalAdditionalCameraData();
            acd.renderPostProcessing = true;

            // 背景：中心の爆発
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(bg.GetComponent<Collider>());
            float bgDist = dist + 8;
            bg.transform.position = camGo.transform.position + camGo.transform.forward * bgDist;
            bg.transform.rotation = camGo.transform.rotation;
            float bgH = 2 * bgDist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
            bg.transform.localScale = new Vector3(bgH * aspect, bgH, 1);
            var bgMat = new Material(Shader.Find("PachiMoji/Burst"));
            if (s.finish == PachiText.Finish.Silver)
            {
                bgMat.SetColor("_C1", new Color(0.55f, 0.75f, 1f)); bgMat.SetColor("_C2", new Color(0.12f, 0.25f, 0.9f)); bgMat.SetColor("_C3", new Color(0.02f, 0.02f, 0.15f));
            }
            bg.GetComponent<MeshRenderer>().sharedMaterial = bgMat;

            // 光のにじみ（ブルーム）
            var volGo = new GameObject("Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(1.4f);
            bloom.scatter.Override(0.65f);
            var tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.None);
            vol.sharedProfile = profile;

            // 2倍の大きさで撮って縮める（ギザギザを消す）
            var big = new RenderTexture(s.w * 2, s.h * 2, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var small = new RenderTexture(s.w, s.h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = big;
            cam.Render();
            Graphics.Blit(big, small);
            var prev = RenderTexture.active;
            RenderTexture.active = small;
            var tex = new Texture2D(s.w, s.h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, s.w, s.h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(outDir, s.file + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            big.Release(); small.Release();
            Debug.Log($"[PachiCapture] saved {s.file}.png  bounds={b.size}");
        }
    }
}
