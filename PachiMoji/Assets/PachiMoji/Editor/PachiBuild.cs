using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PachiMoji.EditorTools
{
    /// <summary>
    /// アプリの組み立てまわり：再生だけのシーンを作る。
    /// コマンドから -executeMethod PachiMoji.EditorTools.PachiBuild.MakePlayScene で動かす。
    /// </summary>
    public static class PachiBuild
    {
        public const string PlayScene = "Assets/Scenes/Play.unity";

        /// <summary>PachiPlayer だけが載ったシーンを作る（カメラ・ライトは置かない。舞台は PachiStage が作る）</summary>
        public static void MakePlayScene()
        {
            int code = 0;
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("PachiPlayer").AddComponent<PachiPlayer>();
                if (!EditorSceneManager.SaveScene(scene, PlayScene)) throw new Exception("シーンを保存できない: " + PlayScene);
                Debug.Log("[PachiBuild] シーンを作った " + PlayScene);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
