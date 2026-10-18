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

                // 言葉を何度も打ち直しても、古い舞台（立体・材質・カメラ）が残らない
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                int go0 = CountSceneObjects(), mat0 = Count<Material>(), mesh0 = Count<Mesh>();
                for (int i = 0; i < 10; i++)
                {
                    var e = EffectCatalog.Get("oudou").Create();
                    e.Build(new[] { "定時" + i, "退社" }, 0.5f, new EffectSettings());
                    e.Evaluate(9f);
                    e.Idle(1f);
                    e.Dispose();
                }
                int go1 = CountSceneObjects(), mat1 = Count<Material>(), mesh1 = Count<Mesh>();
                Debug.Log($"[PachiAppCheck] 組み立て直しの前後 物 {go0}→{go1} 材質 {mat0}→{mat1} メッシュ {mesh0}→{mesh1}");
                Expect(go1 == go0 && mat1 == mat0 && mesh1 == mesh0, "組み立て直しても物が残らない");
                Expect(EffectCatalog.Get("知らない").Id == "oudou", "知らない ID は王道昇格");
                Expect(EffectCatalog.All[0].RequiredProduct == null, "王道昇格は無料");

                // 12文字（6文字×2行）がノッチのある機種の比率でも画面に収まる
                var twelve = new[] { "脳汁ドバドバ", "激アツ確定だ" };
                ShotEffect(720, 1560, Path.Combine(outDir, "fit_12_end.png"), twelve, e => e.Evaluate(e.Duration - 0.5f));
                ShotEffect(720, 1560, Path.Combine(outDir, "fit_12_idle.png"), twelve, e => e.Idle(1f));
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

        static int Count<T>() where T : UnityEngine.Object => Resources.FindObjectsOfTypeAll<T>().Length;

        static int CountSceneObjects()
        {
            int n = 0;
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                n += root.GetComponentsInChildren<Transform>(true).Length;
            return n;
        }

        // 演出の部品で1枚描く（pose で姿を決める）
        static void ShotEffect(int w, int h, string path, string[] lines, Action<IPachiEffect> pose)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var e = EffectCatalog.Get("oudou").Create();
            e.Build(lines, (float)w / h, new EffectSettings());
            pose(e);
            Save(e.Camera, w, h, path);
            e.Dispose();
        }

        static void Save(Camera cam, int w, int h, string path)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            cam.Render();
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log("[PachiAppCheck] 書き出し " + path);
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
            Save(st.cam, w, h, path);
        }
    }
}
