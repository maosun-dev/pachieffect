using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace PachiMoji.EditorTools
{
    /// <summary>
    /// アプリの組み立てまわり：再生だけのシーン・アイコン・iPhone 用の設定・組み立ての入口。
    /// コマンドから -executeMethod PachiMoji.EditorTools.PachiBuild.○○ で動かす。
    ///   MakePlayScene … アプリのシーンを作る（PachiApp を1つ置くだけ）
    ///   MakeIcon      … アプリのアイコンを作る（虹の「脳汁」）
    ///   MakeWatermark … 動画の右下に重ねる印「脳汁メーカー」を作る
    ///   ConfigureCli  … iPhone 用の設定を当てる（手元用）
    ///   BuildIos      … GitHub の組み立て機（GameCI）から呼ばれる。設定を当てて Xcode の作りを書き出す
    /// </summary>
    public static class PachiBuild
    {
        public const string PlayScene = "Assets/Scenes/Play.unity";
        public const string BundleId = "com.maosundev.noujiru";
        public const string AppName = "脳汁メーカー";
        public const string IconPath = "Assets/PachiMoji/Icons/AppIcon.png";
        public const string WatermarkPath = "Assets/PachiMoji/Resources/Export/Watermark.png";
        public const int WatermarkW = 640, WatermarkH = 128;
        // Shader.Find で探すだけのシェーダーは、ここに並べて「いつも入れる」にしないとアプリから抜け落ちる
        public static readonly string[] Shaders =
            { "PachiMoji/Burst", "PachiMoji/Flash", "PachiMoji/Glint", "PachiMoji/Metal", "PachiMoji/Rays", "PachiMoji/Spark" };
        const string GraphicsSettingsPath = "ProjectSettings/GraphicsSettings.asset";

        /// <summary>PachiApp だけが載ったシーンを作る（カメラ・ライト・画面の部品は置かない。どれも PachiApp がコードで作る）</summary>
        public static void MakePlayScene()
        {
            RunCli(() =>
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("PachiApp").AddComponent<PachiApp>();
                if (!EditorSceneManager.SaveScene(scene, PlayScene)) throw new Exception("シーンを保存できない: " + PlayScene);
                Debug.Log("[PachiBuild] シーンを作った " + PlayScene);
            });
        }

        /// <summary>
        /// アイコン：虹になり終えた「脳汁」を、ほぼ正面から枠いっぱいに撮り、透明なしの 1024 四方 PNG にする。
        /// ホーム画面では 60px 前後に縮むので、字を大きく・背景は暗く静かにして、字だけが目に入るようにする
        /// </summary>
        public static void MakeIcon()
        {
            RunCli(() =>
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var st = PachiStage.Build(new[] { "脳汁" }, 1f);
                // 2回転して止まり、揺れが落ち着いたころ
                st.Evaluate(st.SpinAt + 2 * PachiStage.Spin + PachiStage.SpinRest + 1.2f);

                // 文字：ほぼ正面。少しだけ下から・右から見て、厚みの側面をのぞかせる
                st.text.rotation = Quaternion.Euler(7, -12, 0);
                st.text.localScale = Vector3.one;
                PachiText.SetHeat(st.rig, 0, 0, 0);   // 色相を流さない＝紫から緑まで、いちばん虹らしい並び
                var b = st.text.GetComponent<MeshRenderer>().bounds;
                float fitH = Mathf.Max(b.size.x, b.size.y) * 1.1f;   // 角丸で欠けないよう、四隅に少し余白
                float dist = fitH * 0.5f / Mathf.Tan(st.cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                st.cam.transform.position = b.center + new Vector3(0, -fitH * 0.08f, -dist);
                st.cam.transform.LookAt(b.center);

                // 背景：暗めの虹。稲妻・光の筋・白い中心は消して、字を主役にする
                var cam = st.cam.transform;
                var bg = cam.Find("Background").GetComponent<MeshRenderer>().sharedMaterial;
                bg.SetFloat("_Mode", 1);
                bg.SetFloat("_Gain", 0.22f);
                bg.SetFloat("_Core", 0.25f);
                bg.SetFloat("_Bolts", 0);
                bg.SetFloat("_Sparks", 0.4f);
                bg.SetFloat("_Rays", 0);
                cam.Find("Rays").GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Rays", 0);
                cam.Find("Flash").GetComponent<MeshRenderer>().sharedMaterial.SetColor("_Color", new Color(0, 0, 0, 0));
                // 2倍で描いて縮める（ギザギザを消す）
                var big = new RenderTexture(2048, 2048, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var small = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                st.cam.targetTexture = big;
                st.cam.Render();
                Graphics.Blit(big, small);
                var tex = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
                var prev = RenderTexture.active;
                RenderTexture.active = small;
                tex.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                st.cam.targetTexture = null;
                big.Release(); small.Release();
                Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
                File.WriteAllBytes(IconPath, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(IconPath);
                Debug.Log("[PachiBuild] アイコンを作った " + IconPath);
            });
        }

        /// <summary>
        /// 動画の印：Reggae One の「脳汁メーカー」1行を、透明の背景に 640×128 で描く。
        /// どの場面の上でも読めるよう、クロム（上が白・下が灰）に黒いフチと影。飾りは付けず、演出の邪魔をしない
        /// </summary>
        public static void MakeWatermark()
        {
            RunCli(() =>
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var rt = new RenderTexture(WatermarkW, WatermarkH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var cam = MainScreen.CreateUiCamera();
                cam.backgroundColor = new Color(0, 0, 0, 0);
                cam.targetTexture = rt;
                var go = new GameObject("Canvas", typeof(Canvas));
                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1;
                var label = new GameObject("Label", typeof(RectTransform));
                var lr = (RectTransform)label.transform;
                lr.SetParent(go.transform, false);
                lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
                lr.offsetMin = new Vector2(16, 0); lr.offsetMax = new Vector2(-16, 0);
                var t = label.AddComponent<TextMeshProUGUI>();
                t.font = UiTheme.Display;
                t.text = "脳汁メーカー";
                t.alignment = TextAlignmentOptions.Center;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.enableAutoSizing = true; t.fontSizeMin = 20; t.fontSizeMax = 200;
                t.color = Color.white;
                t.enableVertexGradient = true;
                t.colorGradient = new VertexGradient(Color.white, Color.white, UiTheme.Muted, UiTheme.Muted);
                var mat = t.fontMaterial;   // この字だけの材質（共有の材質は変えない）
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                mat.SetColor(ShaderUtilities.ID_UnderlayColor, Color.black);
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
                mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.5f);   // 影を太らせて、白い場面でも字の形が浮く
                mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.3f);
                foreach (var tr in go.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = MainScreen.UiLayer;

                for (int i = 0; i < 2; i++) { Canvas.ForceUpdateCanvases(); cam.Render(); }   // 1回目は字の形ができる前のことがある
                var tex = new Texture2D(WatermarkW, WatermarkH, TextureFormat.RGBA32, false);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, WatermarkW, WatermarkH), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                cam.targetTexture = null;
                rt.Release();
                Directory.CreateDirectory(Path.GetDirectoryName(WatermarkPath));
                File.WriteAllBytes(WatermarkPath, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(WatermarkPath);
                var imp = (TextureImporter)AssetImporter.GetAtPath(WatermarkPath);
                imp.textureType = TextureImporterType.Default;
                imp.mipmapEnabled = false;
                imp.npotScale = TextureImporterNPOTScale.None;   // 640×128 のまま（2の累乗に丸めない）
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.alphaIsTransparency = true;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.isReadable = true;   // 手元確認で画素を見る
                imp.SaveAndReimport();
                Debug.Log("[PachiBuild] 印を作った " + WatermarkPath);
            });
        }

        /// <summary>iPhone 用の設定を当てる。何度呼んでも同じ結果になる</summary>
        public static void Configure()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.productName = AppName;
            PlayerSettings.companyName = "maosun-dev";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;   // 署名は GitHub の Mac の段で付ける

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null) throw new Exception("アイコンが無い（先に MakeIcon）: " + IconPath);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            var gs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(GraphicsSettingsPath)[0]);
            var list = gs.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in Shaders)
            {
                if (AlwaysIncluded(name)) continue;
                var shader = Shader.Find(name);
                if (shader == null) throw new Exception("シェーダーが見つからない: " + name);
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                gs.ApplyModifiedPropertiesWithoutUndo();
            }

            // iPhone は Mobile の画質で動く。0.8 倍で描いて引き伸ばすと金の縁がぼやけるので 1 倍にする（描くものは軽い）
            var mobile = MobilePipeline();
            if (!Mathf.Approximately(mobile.renderScale, 1f))
            {
                mobile.renderScale = 1f;
                EditorUtility.SetDirty(mobile);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(PlayScene, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[PachiBuild] iPhone 用の設定を当てた");
        }

        /// <summary>iPhone が使う画質（Mobile）の描画設定</summary>
        public static UniversalRenderPipelineAsset MobilePipeline()
        {
            int i = Array.IndexOf(QualitySettings.names, "Mobile");
            if (i < 0) throw new Exception("画質 Mobile が無い");
            return (UniversalRenderPipelineAsset)QualitySettings.GetRenderPipelineAssetAt(i);
        }

        /// <summary>そのシェーダーが「いつも入れるシェーダー」に入っているか</summary>
        public static bool AlwaysIncluded(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null) return false;
            var list = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(GraphicsSettingsPath)[0])
                .FindProperty("m_AlwaysIncludedShaders");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) return true;
            return false;
        }

        public static void ConfigureCli() => RunCli(Configure);

        /// <summary>GameCI から呼ばれる入口。設定を当てて、Xcode の作りを -customBuildPath（無ければ build/iOS/iOS）に書き出す</summary>
        public static void BuildIos()
        {
            RunCli(() =>
            {
                Configure();
                var args = Environment.GetCommandLineArgs();
                string path = "build/iOS/iOS";
                int i = Array.IndexOf(args, "-customBuildPath");
                if (i >= 0 && i + 1 < args.Length) path = args[i + 1];
                var report = BuildPipeline.BuildPlayer(new[] { PlayScene }, path, BuildTarget.iOS, BuildOptions.None);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new Exception($"失敗: {report.summary.result}（エラー {report.summary.totalErrors} 件）");
                Debug.Log("[PachiBuild] Xcode の作りを書き出した " + path);
            });
        }

        // 裏起動なら、うまくいけば 0、失敗なら 1 で終わる
        static void RunCli(Action body)
        {
            int code = 0;
            try { body(); }
            catch (Exception e)
            {
                Debug.LogError("[PachiBuild] " + e.Message);
                Debug.LogException(e);
                code = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
