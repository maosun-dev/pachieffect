using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PachiMoji.EditorTools
{
    /// <summary>
    /// App Store の画面写真（Dynamic Island 搭載 iPhone 用 1206×2622）を pachieffect/captures/store/ に撮る。
    /// コマンドから -executeMethod PachiMoji.EditorTools.PachiAppCheck.RunStoreShots で動かす（store-shots.sh）
    /// </summary>
    public static partial class PachiAppCheck
    {
        const int StoreW = 1206, StoreH = 2622;

        public static void RunStoreShots()
        {
            failed = 0;
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../captures/store"));
                Directory.CreateDirectory(dir);

                // 1. 虹で回り終えた姿（いちばんの見せ場）
                StageShot(Path.Combine(dir, "1_rainbow.png"), new[] { "定時", "退社" }, new EffectSettings(), st => st.End - 0.2f);
                // 2. 金で着地した瞬間
                StageShot(Path.Combine(dir, "2_gold.png"), new[] { "合格", "確定" },
                    new EffectSettings { Chain = new[] { Tier.Gold, Tier.Rainbow } }, st => st.ImpactEnd + 0.9f);
                // 3. チラ見せ（立体の文字を下からあおる）
                StageShot(Path.Combine(dir, "3_tease.png"), new[] { "誕生日", "おめでとう" },
                    new EffectSettings { TeaseText = Tier.Gold, TeaseBack = Tier.Purple }, st => st.Tease[1].At + st.Tease[1].Len / 2);
                // 4. 赤で着地した瞬間（金・虹へ昇格していく）
                StageShot(Path.Combine(dir, "4_red.png"), new[] { "宝くじ", "当選" },
                    new EffectSettings { Chain = new[] { Tier.Red, Tier.Gold, Tier.Rainbow } }, st => st.ImpactEnd + 0.9f);
                // 5. 言葉を入れて PUSH を待つ画面
                ShotMain(StoreW, StoreH, Path.Combine(dir, "5_input.png"), "定時", "退社", AppState.Idle);
                // 6. 設定（昇格の色を選ぶメダル）
                ShotSettings(StoreW, StoreH, Path.Combine(dir, "6_settings.png"),
                    new EffectSettings { Chain = new[] { Tier.Green, Tier.Red, Tier.Rainbow } }, sc => sc.SetWordLength(4), scrollTo: 1f);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                failed++;
            }
            Debug.Log("[PachiAppCheck] 画面写真 " + (failed == 0 ? "OK" : "NG " + failed));
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        static void StageShot(string path, string[] words, EffectSettings settings, System.Func<PachiStage, float> at)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var st = PachiStage.Build(words, (float)StoreW / StoreH, settings);
            st.Evaluate(at(st));
            Save(st.cam, StoreW, StoreH, path);
            st.Dispose();
            Debug.Log("[PachiAppCheck] 書き出し " + path);
        }
    }
}
