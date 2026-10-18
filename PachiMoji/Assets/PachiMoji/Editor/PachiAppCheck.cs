using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PachiMoji.EditorTools
{
    /// <summary>
    /// アプリ化の手元確認：iPhone 用の設定・片付け、演出と画面の絵（pachieffect/captures/*.png）。
    /// コマンドから -executeMethod PachiMoji.EditorTools.PachiAppCheck.Run で動かす（app-check.sh）。
    /// </summary>
    public static class PachiAppCheck
    {
        static int failed;
        static readonly string[] Words = { "定時", "退社" };

        public static void Run()
        {
            failed = 0;
            try
            {
                // アプリのシーンがあり、PachiApp が載っていること
                string scenePath = Path.Combine(Application.dataPath, "../", PachiBuild.PlayScene);
                Expect(File.Exists(scenePath) && File.ReadAllText(scenePath).Contains("m_Name: PachiApp"), "シーン " + PachiBuild.PlayScene + " に PachiApp");

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

                // 画面の素材の一覧（コードで描いた絵と、2つの画面用の書体）
                Expect(UiTheme.Display != null && UiTheme.Body != null, "画面用の書体が読める");
                ShotUi(720, 1280, Path.Combine(outDir, "ui_parts.png"), PartsSheet);

                // メイン画面：待ち・知らせが出た待ち・終わり（9:16 と 9:19.5）
                foreach (var (w, h, tag) in new[] { (720, 1280, "9x16"), (720, 1560, "9x19") })
                {
                    ShotMain(w, h, Path.Combine(outDir, $"main_{tag}_idle.png"), "定時", "退社", AppState.Idle);
                    ShotMain(w, h, Path.Combine(outDir, $"main_{tag}_notice.png"), "定時", "退社♨", AppState.Idle);
                    ShotMain(w, h, Path.Combine(outDir, $"main_{tag}_end.png"), "定時", "退社", AppState.Finished);
                }

                // 設定画面：既定と、全部を変えたとき
                ShotSettings(720, 1560, Path.Combine(outDir, "settings_default.png"), new EffectSettings());
                ShotSettings(720, 1560, Path.Combine(outDir, "settings_changed.png"), new EffectSettings
                    { SparksOn = false, Light = LightColor.Rainbow, Size = LightSize.Large, Font = TextFont.Mincho, From = Tier.Gold, To = Tier.Rainbow });
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

        // 素材を並べる：ドーム2つ・銀の板・歯車・メダル3枚と金の帯・書体の見本
        static void PartsSheet(RectTransform root)
        {
            Img(root, UiArt.Dome(true), new Vector2(-95, 260), new Vector2(170, 170));
            Img(root, UiArt.Dome(false), new Vector2(95, 260), new Vector2(170, 170));
            Text(root, "PUSH", UiTheme.Display, 40, Color.white, new Vector2(-95, 260), new Vector2(170, 60));
            var plate = Img(root, UiArt.ChromePlate(), new Vector2(0, 90), new Vector2(330, 104));
            plate.type = Image.Type.Sliced;
            Text(root, "定時", UiTheme.Display, 30, UiTheme.Chrome, new Vector2(0, 112), new Vector2(300, 44));
            Text(root, "2行目（なくてもOK）", UiTheme.Body, 16, UiTheme.Muted, new Vector2(0, 66), new Vector2(300, 40));
            Img(root, UiArt.Gear(), new Vector2(150, 360), new Vector2(36, 36));
            var rail = Img(root, UiArt.Rail(), new Vector2(-50, -60), new Vector2(200, 22));
            rail.type = Image.Type.Sliced;
            Img(root, UiArt.Medal(Tier.Silver), new Vector2(-100, -60), new Vector2(64, 64));
            Img(root, UiArt.Medal(Tier.Gold), new Vector2(0, -60), new Vector2(64, 64));
            Img(root, UiArt.Medal(Tier.Rainbow), new Vector2(100, -60), new Vector2(64, 64));
            Text(root, "脳汁メーカー", UiTheme.Display, 34, UiTheme.Chrome, new Vector2(0, -160), new Vector2(340, 50));
            Text(root, "言葉を入れると PUSH できます", UiTheme.Body, 16, UiTheme.Muted, new Vector2(0, -205), new Vector2(340, 30));
            Text(root, "「😀」はこの書体で出せません", UiTheme.Body, 16, UiTheme.Muted, new Vector2(0, -235), new Vector2(340, 30));
        }

        static Image Img(RectTransform parent, Sprite sp, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Img", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = sp;
            return img;
        }

        static void Text(RectTransform parent, string s, TMP_FontAsset font, float size, Color c, Vector2 pos, Vector2 box)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos; rt.sizeDelta = box;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = font; t.fontSize = size; t.color = c; t.text = s;
            t.alignment = TextAlignmentOptions.Center;
        }

        /// <summary>画面の部品だけを1枚に撮る。build は基準 390×844 の Canvas の根っこに部品を並べる</summary>
        static void ShotUi(int w, int h, string path, Action<RectTransform> build)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var uiCam = MainScreen.CreateUiCamera();
            uiCam.targetTexture = rt;
            build(MainScreen.CreateCanvas(uiCam));
            foreach (var tr in UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include)) tr.gameObject.layer = MainScreen.UiLayer;
            RenderUi(rt, path, uiCam);
        }

        /// <summary>
        /// メイン画面を1枚に撮る（PachiApp と同じ部品・同じ確かめ方で）。
        /// 縦長（9:19.5）のときは、ノッチ（上 47pt）とホームバー（下 34pt）を避けた範囲に置く
        /// </summary>
        static void ShotMain(int w, int h, string path, string l1, string l2, AppState state)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            float pt = w / UiTheme.ReferenceSize.x;
            bool notch = h * 9 > w * 17;
            var safe = notch ? new Rect(0, 34 * pt, w, h - (47 + 34) * pt) : new Rect(0, 0, w, h);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var uiCam = MainScreen.CreateUiCamera();
            uiCam.targetTexture = rt;
            var ui = MainScreen.Build(MainScreen.CreateCanvas(uiCam), safe, new Vector2(w, h));
            ui.Line1.text = l1; ui.Line2.text = l2;
            var font = PachiText.LoadFont("ReggaeOne");
            var check = InputRules.Check(l1, l2, cp => font.GlyphIndex(cp) != 0);
            ui.Notice.text = InputRules.Message(check, MainScreen.CanShowInBody(check.BadChar));
            ui.SetPushEnabled(check.CanPlay);
            ui.ShowState(state);
            if (state != AppState.Idle) ui.SetDeckSink(1);
            Canvas.ForceUpdateCanvases();

            var stage = new StageScreen(ui, uiCam);
            if (check.CanPlay)
            {
                stage.Build(check.Lines, new EffectSettings(), new Vector2(w, h));
                stage.SetFull(state != AppState.Idle);
                if (state == AppState.Idle) stage.Effect.Idle(1f); else stage.Effect.Evaluate(stage.Effect.Duration);
                stage.Frame();
                stage.Effect.Camera.Render();   // アプリでは毎コマ自動で描かれる
            }
            RenderUi(rt, path, uiCam);
            stage.Dispose();
        }

        static void ShotSettings(int w, int h, string path, EffectSettings settings)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            float pt = w / UiTheme.ReferenceSize.x;
            var safe = new Rect(0, 34 * pt, w, h - (47 + 34) * pt);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var uiCam = MainScreen.CreateUiCamera();
            uiCam.targetTexture = rt;
            var screen = SettingsScreen.Build(MainScreen.CreateCanvas(uiCam), safe, new Vector2(w, h), settings, _ => { });
            screen.Show(true);
            screen.SetSlide(0);
            RenderUi(rt, path, uiCam);
        }

        // 画面のカメラで板へ描いて保存する
        static void RenderUi(RenderTexture rt, string path, Camera uiCam)
        {
            Canvas.ForceUpdateCanvases();
            uiCam.Render();
            Canvas.ForceUpdateCanvases();
            uiCam.Render();   // 1回目は TMP の字の形が作られる前に描かれることがあるので、もう1回
            var prev = RenderTexture.active;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            uiCam.targetTexture = null;
            rt.Release();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("[PachiAppCheck] 書き出し " + path);
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
            var st = PachiStage.Build(Words, (float)w / h, settings);
            st.Evaluate(t >= 0 ? t : st.End - 0.5f);
            Save(st.cam, w, h, path);
        }
    }
}
