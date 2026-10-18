using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PachiMoji.EditorTools
{
    /// <summary>
    /// アプリの組み立てまわり：再生だけのシーン・仮のアイコン・iPhone 用の設定・組み立ての入口。
    /// コマンドから -executeMethod PachiMoji.EditorTools.PachiBuild.○○ で動かす。
    ///   MakePlayScene … 再生だけのシーンを作る
    ///   MakeIcon      … 仮のアイコンを作る（本物は順番5で作る）
    ///   ConfigureCli  … iPhone 用の設定を当てる（手元用）
    ///   BuildIos      … GitHub の組み立て機（GameCI）から呼ばれる。設定を当てて Xcode の作りを書き出す
    /// </summary>
    public static class PachiBuild
    {
        public const string PlayScene = "Assets/Scenes/Play.unity";
        public const string BundleId = "com.maosundev.noujiru";
        public const string AppName = "脳汁メーカー";
        public const string IconPath = "Assets/PachiMoji/Icons/AppIcon.png";
        // Shader.Find で探すだけのシェーダーは、ここに並べて「いつも入れる」にしないとアプリから抜け落ちる
        public static readonly string[] Shaders =
            { "PachiMoji/Burst", "PachiMoji/Flash", "PachiMoji/Glint", "PachiMoji/Metal", "PachiMoji/Rays", "PachiMoji/Spark" };
        const string GraphicsSettingsPath = "ProjectSettings/GraphicsSettings.asset";

        /// <summary>PachiPlayer だけが載ったシーンを作る（カメラ・ライトは置かない。舞台は PachiStage が作る）</summary>
        public static void MakePlayScene()
        {
            RunCli(() =>
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("PachiPlayer").AddComponent<PachiPlayer>();
                if (!EditorSceneManager.SaveScene(scene, PlayScene)) throw new Exception("シーンを保存できない: " + PlayScene);
                Debug.Log("[PachiBuild] シーンを作った " + PlayScene);
            });
        }

        /// <summary>仮のアイコン：金になった「脳汁」を正方形で撮り、透明なしの 1024 四方 PNG にする</summary>
        public static void MakeIcon()
        {
            RunCli(() =>
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var st = PachiStage.Build(new[] { "脳汁" }, 1f);
                st.Evaluate(st.GoldAt + 0.5f);
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

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(PlayScene, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[PachiBuild] iPhone 用の設定を当てた");
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
