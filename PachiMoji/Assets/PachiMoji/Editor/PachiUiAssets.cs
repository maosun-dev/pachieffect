using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace PachiMoji.EditorTools
{
    /// <summary>
    /// 画面の素材づくり：画面用の書体（TMP フォント）を作る。
    /// コマンドから -executeMethod PachiMoji.EditorTools.PachiUiAssets.Make で動かす。何度呼んでも同じ結果になる。
    /// </summary>
    public static class PachiUiAssets
    {
        const string TmpPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
        const string FontDir = "Assets/PachiMoji/UI/Fonts";
        const string OutDir = "Assets/PachiMoji/Resources/UI";

        public static void Make()
        {
            int code = 0;
            try
            {
                // TMP の基本素材は、同じ起動の中では取り込みが終わらないので、先に起動オプション -importPackage で取り込んでおく
                if (!AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
                    throw new Exception("TMP の基本素材が無い。先に Unity を -importPackage \"" + TmpPackage + "\" で起動して取り込む");
                MakeFont(FontDir + "/ReggaeOne-UI.ttf", OutDir + "/ReggaeOne SDF.asset");
                MakeFont(FontDir + "/BIZUDPGothic-Regular.ttf", OutDir + "/BIZUDPGothic SDF.asset");
            }
            catch (Exception e)
            {
                Debug.LogError("[PachiUiAssets] " + e.Message);
                Debug.LogException(e);
                code = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        // 日本語は字が多いので、使った字だけをその場で書き足す（Dynamic）・板が足りなければ増やす（multi atlas）
        static void MakeFont(string ttf, string outPath)
        {
            if (File.Exists(outPath)) { Debug.Log("[PachiUiAssets] もうある " + outPath); return; }
            AssetDatabase.ImportAsset(ttf);
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
            if (font == null) throw new Exception("書体が読めない: " + ttf);
            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (fa == null) throw new Exception("TMP フォントが作れない: " + ttf);
            string name = Path.GetFileNameWithoutExtension(outPath);
            fa.name = name;
            AssetDatabase.CreateAsset(fa, outPath);
            // 文字の絵と材質を同じファイルに入れる（入れないと保存したあとで消える）
            var tex = fa.atlasTextures[0];
            tex.name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(tex, fa);
            fa.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            Debug.Log("[PachiUiAssets] 作った " + outPath);
        }
    }
}
