using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PachiMoji.EditorTools
{
    /// <summary>
    /// アプリ化の手元確認：くり返し再生の時刻の計算と、2つの画面比率での絵（pachieffect/captures/play_*.png）。
    /// コマンドから -executeMethod PachiMoji.EditorTools.PachiAppCheck.Run で動かす（app-check.sh）。
    /// </summary>
    public static class PachiAppCheck
    {
        static int failed;

        public static void Run()
        {
            failed = 0;
            try
            {
                // くり返し再生の時刻（開きっぱなしでも壊れないこと）
                Expect(Mathf.Approximately(PachiPlayer.LoopTime(3f, 10f, 1.5f), 3f), "再生中はそのままの時刻");
                Expect(Mathf.Approximately(PachiPlayer.LoopTime(10.8f, 10f, 1.5f), 10f), "終わってからの間は終わりの姿で止まる");
                Expect(Mathf.Approximately(PachiPlayer.LoopTime(11.5f, 10f, 1.5f), 0f), "間が明けたら頭から");
                Expect(Mathf.Approximately(PachiPlayer.LoopTime(14.5f, 10f, 1.5f), 3f), "2周目も同じ時刻の姿");
                Expect(PachiPlayer.LoopTime(36000f + 3f, 10f, 1.5f) <= 10f, "10時間開きっぱなしでも終わりを超えない");

                // 再生だけのシーンがあること
                Expect(File.Exists(Path.Combine(Application.dataPath, "../", PachiBuild.PlayScene)), "シーン " + PachiBuild.PlayScene);

                // iPhone 用の設定（PachiBuild.Configure が当てたもの）
                Expect(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS) == PachiBuild.BundleId, "Bundle ID");
                Expect(PlayerSettings.productName == PachiBuild.AppName, "ホーム画面の名前");
                Expect(PlayerSettings.iOS.targetDevice == iOSTargetDevice.iPhoneOnly, "iPhone だけ");
                Expect(PlayerSettings.defaultInterfaceOrientation == UIOrientation.Portrait, "縦向き固定");
                Expect(EditorBuildSettings.scenes.Length == 1 && EditorBuildSettings.scenes[0].path == PachiBuild.PlayScene, "入れるシーンは Play だけ");
                foreach (var s in PachiBuild.Shaders) Expect(PachiBuild.AlwaysIncluded(s), "いつも入れるシェーダー: " + s);
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(PachiBuild.IconPath);
                Expect(icon != null && icon.width == 1024 && icon.height == 1024, "アイコン 1024 四方");
                // CreatePrimitive(Quad) が付ける MeshCollider を、iPhone 版の「使わない部品を削る」から守る
                var linkXml = Path.Combine(Application.dataPath, "link.xml");
                Expect(File.Exists(linkXml) && File.ReadAllText(linkXml).Contains("UnityEngine.MeshCollider"), "link.xml で MeshCollider を残す");
                // iPhone は Mobile の画質で動く。0.8 倍で描いて引き伸ばすと金の縁がぼやけるので 1 倍
                Expect(Mathf.Approximately(PachiBuild.MobilePipeline().renderScale, 1f), "Mobile の描く細かさが 1 倍");

                // 2つの画面比率の絵（iPhone SE の 9:16 と、ノッチのある機種の 9:19.5）
                string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../captures"));
                Directory.CreateDirectory(outDir);
                Shot(720, 1280, Path.Combine(outDir, "play_9x16.png"));
                Shot(720, 1560, Path.Combine(outDir, "play_9x19.png"));

                // プチュンの光の色と大きさ（閃光が引いて、光の点が点滅している 0.55 秒目）
                Shot(720, 1280, Path.Combine(outDir, "puchun_gold_large.png"),
                    new EffectSettings { Light = LightColor.Gold, Size = LightSize.Large }, 0.55f);
                Shot(720, 1280, Path.Combine(outDir, "puchun_rainbow_small.png"),
                    new EffectSettings { Light = LightColor.Rainbow, Size = LightSize.Small }, 0.55f);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                failed++;
            }
            if (failed == 0) Debug.Log("[PachiAppCheck] OK");
            else Debug.LogError($"[PachiAppCheck] NG {failed} 件");
            if (Application.isBatchMode) EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        static void Expect(bool ok, string what)
        {
            if (ok) return;
            failed++;
            Debug.LogError("[PachiAppCheck] NG: " + what);
        }

        // 1枚描く。時刻を決めなければ、虹で回り終えた余韻の姿（回転中だと文字が横向きで収まりが見えない）
        static void Shot(int w, int h, string path, EffectSettings settings = null, float t = -1)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var st = PachiStage.Build(PachiPlayer.Words, (float)w / h, settings);
            st.Evaluate(t >= 0 ? t : st.End - 0.5f);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            st.cam.targetTexture = rt;
            st.cam.Render();
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            st.cam.targetTexture = null;
            rt.Release();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("[PachiAppCheck] 書き出し " + path);
        }
    }
}
