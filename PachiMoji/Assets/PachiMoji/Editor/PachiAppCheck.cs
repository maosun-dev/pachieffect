using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
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
                // 書き出しの iPhone の部品：iPhone だけに入り、動画・写真の仕組みにつながる。写真の許可の文は組み立ての Mac の段で足す
                var mm = PluginImporter.GetAtPath("Assets/Plugins/iOS/PachiVideo.mm") as PluginImporter;
                Expect(mm != null && mm.GetCompatibleWithPlatform(BuildTarget.iOS) && !mm.GetCompatibleWithEditor()
                       && !mm.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64), "PachiVideo.mm は iPhone だけ");
                string deps = mm != null ? mm.GetPlatformData(BuildTarget.iOS, "FrameworkDependencies") : "";
                foreach (var fw in new[] { "AVFoundation", "CoreMedia", "CoreVideo", "Photos" })
                    Expect(deps.Contains(fw + ";"), "PachiVideo.mm が使う仕組み " + fw);
                var workflow = Path.Combine(Application.dataPath, "../../.github/workflows/ios-testflight.yml");
                Expect(File.Exists(workflow) && File.ReadAllText(workflow).Contains("NSPhotoLibraryAddUsageDescription string 書き出した動画を写真に保存するために使います"), "写真の許可の文");
                Expect(PlayerSettings.allowUnsafeCode, "unsafe を許す（コマの中身の場所を mm に渡す）");

                // 広告と課金の部品（版を固定）と、組み立ての道
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
                string manifest = File.ReadAllText(Path.Combine(root, "PachiMoji/Packages/manifest.json"));
                Expect(manifest.Contains("\"com.unity.purchasing\": \"5.4.4\""), "課金の部品 5.4.4");
                Expect(manifest.Contains("\"com.google.ads.mobile\": \"11.5.0\""), "広告の部品 11.5.0");
                Expect(manifest.Contains("\"com.google.external-dependency-manager\": \"1.2.189\""), "External Dependency Manager 1.2.189");
                Expect(manifest.Contains("https://package.openupm.com"), "OpenUPM の登録先");
                string gvhPath = Path.Combine(root, "PachiMoji/ProjectSettings/GvhProjectSettings.xml");
                string gvh = File.Exists(gvhPath) ? File.ReadAllText(gvhPath) : "";
                Expect(gvh.Contains("name=\"Google.IOSResolver.UseProjectSettings\" value=\"True\""), "iOS の部品の設定はプロジェクトのもの");
                Expect(gvh.Contains("name=\"Google.IOSResolver.CocoapodsInstallEnabled\" value=\"False\""), "Linux では pod install しない");
                Expect(gvh.Contains("name=\"Google.IOSResolver.CocoapodsIntegrationMethod\" value=\"2\""), "xcworkspace でつなぐ");
                string wf = File.ReadAllText(Path.Combine(root, ".github/workflows/ios-testflight.yml"));
                Expect(wf.Contains("production:") && wf.Contains("-pachiProduction"), "ストアに出す版の印（production）");
                Expect(wf.Contains("pod install") && wf.Contains("-workspace iOS/Unity-iPhone.xcworkspace"), "Mac の段で pod install して xcworkspace を組み立てる");
                // AdMob のアプリ ID と追跡の許可の文は、広告の部品が組み立てのときに Info.plist へ書く（設定は PachiBuild.Configure が入れる）
                string ads = File.Exists(Path.Combine(root, "PachiMoji", PachiBuild.AdsSettingsPath)) ? File.ReadAllText(Path.Combine(root, "PachiMoji", PachiBuild.AdsSettingsPath)) : "";
                Expect(ads.Contains("adMobIOSAppId: " + AdIds.TestAppId), "広告の部品の設定はテスト用のアプリ ID");
                Expect(ads.Contains("userTrackingUsageDescription: ") && !ads.Contains("userTrackingUsageDescription: \n"), "追跡の許可の文");
                Expect(!PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS).Contains(PachiBuild.ProductionDefine), "手元の設定に本番の印が付いていない");
                Expect(!AdIds.Production && AdIds.Banner == AdIds.TestBanner && AdIds.Rewarded == AdIds.TestRewarded, "手元はテスト広告");
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

                // 最初の文字色と背景：ドンで着地して金に変わる前（銀の段）の姿を、色の組み合わせごとに撮る
                foreach (var (ft, fb) in new[] { (FirstText.Silver, FirstBack.Blue), (FirstText.Blue, FirstBack.Blue), (FirstText.Green, FirstBack.Green),
                             (FirstText.Red, FirstBack.Red), (FirstText.Purple, FirstBack.Purple), (FirstText.Red, FirstBack.Green), (FirstText.Purple, FirstBack.Red) })
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    var st = PachiStage.Build(new[] { "残業", "確定です" }, 9f / 16, new EffectSettings { FirstText = ft, FirstBack = fb });
                    st.Evaluate((st.ImpactEnd + st.GoldAt) / 2);
                    Save(st.cam, 360, 640, Path.Combine(outDir, $"first_{ft}_{fb}.png"));
                    st.Dispose();
                }

                // 虹で終わる演出は、最後の 0.4 秒の背景が虹のまま（余韻の青・炎への切り替えで終わらない）。動画の最後の絵になるため
                foreach (var from in new[] { Tier.Silver, Tier.Gold, Tier.Rainbow })
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    var st = PachiStage.Build(Words, 9f / 16, new EffectSettings { From = from, To = Tier.Rainbow });
                    var bg = st.cam.transform.Find("Background").GetComponent<MeshRenderer>().sharedMaterial;
                    bool rainbowEnd = true;
                    for (float k = 0; k <= 0.4f; k += 1f / 30)
                    {
                        st.Evaluate(st.End - k);
                        rainbowEnd &= Mathf.Approximately(bg.GetFloat("_Mode"), 1);
                    }
                    Expect(rainbowEnd, $"虹で終わる演出（{from}→虹）は最後の背景が虹");
                    st.Dispose();
                }

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

                // 書き出しの画面：書き出し中・できた（保存済み）・失敗の知らせ（9:19.5）
                ShotExport(720, 1560, Path.Combine(outDir, "main_9x19_exporting.png"), AppState.Exporting, ui => ui.SetExportProgress(73));
                ShotExport(720, 1560, Path.Combine(outDir, "main_9x19_exported.png"), AppState.Exported, ui => ui.SetSaved(true));
                ShotMain(720, 1560, Path.Combine(outDir, "main_9x19_export_error.png"), "定時", "退社", AppState.Finished,
                    ui => ui.EndNotice.text = ExportRules.Message(ExportError.NoSpace));
                // ネットが無いとき：「書き出す」を押せず、知らせを出す
                ShotMain(720, 1560, Path.Combine(outDir, "main_9x19_need_network.png"), "定時", "退社", AppState.Finished, ui =>
                {
                    ui.SetExportReady(false);
                    ui.EndNotice.text = ExportGate.Message(GateAction.NeedNetwork);
                    Expect(!ui.Export.interactable, "ネットが無ければ「書き出す」を押せない");
                });

                // 設定画面：既定と、全部を変えたとき
                ShotSettings(720, 1560, Path.Combine(outDir, "settings_default.png"), new EffectSettings());
                ShotSettings(720, 1560, Path.Combine(outDir, "settings_changed.png"), new EffectSettings
                    { SparksOn = false, Light = LightColor.Rainbow, Size = LightSize.Large, Font = TextFont.Mincho, From = Tier.Gold, To = Tier.Rainbow });
                // いちばん下まで動かした姿（最初の色）：はじまりが銀なら選べる、金なら薄くて押せない（iPhone SE の 9:16 でも）
                ShotSettings(720, 1280, Path.Combine(outDir, "settings_bottom_silver.png"), new EffectSettings { FirstText = FirstText.Red, FirstBack = FirstBack.Purple },
                    sc => { Expect(sc.FirstColorsEnabled, "はじまりが銀なら最初の色を選べる"); Expect(sc.Scrollable, "設定画面を縦に動かせる"); });
                ShotSettings(720, 1280, Path.Combine(outDir, "settings_bottom_gold.png"), new EffectSettings { From = Tier.Gold },
                    sc => Expect(!sc.FirstColorsEnabled, "はじまりが金なら最初の色は押せない"), bottom: true);

                // 買い物：何も買っていない（3つ）・印を消す済み（残り1つ）・両方済み（買ったものと復元だけ）・ストアにつながらない
                Func<string, string> price = id => id == ProductIds.Set ? "¥500" : "¥300";
                ShotSettings(720, 1560, Path.Combine(outDir, "settings_shop_none.png"), new EffectSettings(), sc =>
                {
                    sc.ShowShop(new Entitlements(new string[0]), ShopState.Ready, price);
                    Expect(sc.ShopItemCount == 3, "何も買っていなければ商品は3つ");
                });
                ShotSettings(720, 1560, Path.Combine(outDir, "settings_shop_nomark.png"), new EffectSettings(), sc =>
                {
                    sc.ShowShop(new Entitlements(new[] { ProductIds.NoMark }), ShopState.Ready, price);
                    sc.ShowShopNotice("ありがとうございます。購入しました");
                    Expect(sc.ShopItemCount == 1, "片方を買ったら商品は残りの1つ");
                });
                ShotSettings(720, 1560, Path.Combine(outDir, "settings_shop_both.png"), new EffectSettings(), sc =>
                {
                    sc.ShowShop(new Entitlements(new[] { ProductIds.Set }), ShopState.Ready, price);
                    Expect(sc.ShopItemCount == 0, "両方買ったら商品は無し");
                });
                ShotSettings(720, 1280, Path.Combine(outDir, "settings_shop_unavailable.png"), new EffectSettings(), sc =>
                    sc.ShowShop(new Entitlements(new string[0]), ShopState.Unavailable, _ => null));

                // 広告：追跡の許可（ATT）の部品は iPhone だけ。手元（Editor）では広告を動かさず、ごほうびは見終えた扱い
                var att = PluginImporter.GetAtPath("Assets/Plugins/iOS/PachiTracking.mm") as PluginImporter;
                Expect(att != null && att.GetCompatibleWithPlatform(BuildTarget.iOS) && !att.GetCompatibleWithEditor()
                       && att.GetPlatformData(BuildTarget.iOS, "FrameworkDependencies").Contains("AppTrackingTransparency;"), "PachiTracking.mm は iPhone だけ・ATT の仕組みにつながる");
                Expect(new Ads().Rewarded == AdState.Ready, "手元ではごほうび広告は Ready 扱い");

                // 課金：ストアにつなぐ前から、端末に残っていた買ったもので始まる。届いた買い物は記録して知らせる（本物の記録は元に戻す）
                {
                    string backup = PlayerPrefs.GetString("ent.owned", "");
                    var shop = new Shop(new Entitlements(new string[0]));
                    Expect(shop.State == ShopState.Connecting && shop.Owned.ShowAds && shop.PriceOf(ProductIds.NoAds) == null, "課金はつなぐ前の姿で始まる");
                    bool changed = false;
                    shop.Changed += () => changed = true;
                    shop.Grant(ProductIds.NoAds);
                    Expect(changed && !shop.Owned.ShowAds && !Entitlements.Load().ShowAds, "届いた買い物は記録して知らせる");
                    PlayerPrefs.SetString("ent.owned", backup);
                    PlayerPrefs.Save();
                }

                // 動画の書き出し（PNG の版で、アプリと同じ VideoExporter を動かす）
                CheckExport(outDir);
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
        static void ShotMain(int w, int h, string path, string l1, string l2, AppState state, Action<MainScreen> extra = null)
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
            Expect(ui.Export.interactable, "「書き出す」を押せる");
            extra?.Invoke(ui);
            RenderUi(rt, path, uiCam);
            stage.Dispose();
        }

        /// <summary>
        /// 書き出しの画面を1枚に撮る。アプリと同じく画面の舞台は片付けてあり、書き出しの板（9:16）だけを見せる。
        /// 板の代わりに、書き出し中は 6 秒目・できたは最後の姿を描く
        /// </summary>
        static void ShotExport(int w, int h, string path, AppState state, Action<MainScreen> extra)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            float pt = w / UiTheme.ReferenceSize.x;
            var safe = new Rect(0, 34 * pt, w, h - (47 + 34) * pt);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var uiCam = MainScreen.CreateUiCamera();
            uiCam.targetTexture = rt;
            var ui = MainScreen.Build(MainScreen.CreateCanvas(uiCam), safe, new Vector2(w, h));
            ui.SetDeckSink(1);
            ui.ShowState(state);

            var e = EffectCatalog.Get("oudou").Create();
            e.Build(Words, (float)ExportPlan.Width / ExportPlan.Height, new EffectSettings());
            e.Evaluate(state == AppState.Exporting ? 6f : e.Duration);
            var board = new RenderTexture(ExportPlan.Width / 2, ExportPlan.Height / 2, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            e.Camera.targetTexture = board;
            e.Camera.cullingMask &= ~(1 << MainScreen.UiLayer);
            e.Camera.Render();
            e.Camera.enabled = false;
            ui.ExportView.texture = board;
            extra(ui);
            RenderUi(rt, path, uiCam);
            e.Dispose();
            board.Release();
            UnityEngine.Object.DestroyImmediate(board);
        }

        static void ShotSettings(int w, int h, string path, EffectSettings settings, Action<SettingsScreen> check = null, bool bottom = false)
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
            check?.Invoke(screen);
            Canvas.ForceUpdateCanvases();
            if (check != null || bottom) screen.ScrollTo(0);   // いちばん下
            RenderUi(rt, path, uiCam);
        }

        // ───────── 動画の書き出し ─────────

        static void CheckExport(string outDir)
        {
            // 印の絵：640×128、四隅は透明、真ん中の行に字がある
            var wm = Resources.Load<Texture2D>("Export/Watermark");
            Expect(wm != null && wm.width == 640 && wm.height == 128, "印の絵が 640×128");
            if (wm != null)
            {
                Expect(wm.GetPixel(0, 0).a == 0 && wm.GetPixel(639, 0).a == 0 && wm.GetPixel(0, 127).a == 0 && wm.GetPixel(639, 127).a == 0, "印の四隅が透明");
                bool ink = false;
                for (int x = 0; x < 640; x++) ink |= wm.GetPixel(x, 64).a > 0.5f;
                Expect(ink, "印に字がある");
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 最後まで書き出す：コマ数・％・最後の姿と印
            string dir = Path.Combine(outDir, "export_frames");
            var ex = new VideoExporter(Words, new EffectSettings(), true, new PngVideoWriter(), dir);
            Expect(ex.Start() == ExportError.None, "書き出しを始められる");
            var pct = RunExport(ex);
            Expect(ex.Status == ExportStatus.Done, "書き出しが最後まで行く（" + ex.Status + " " + ex.Error + "）");
            var files = Frames(dir);
            Debug.Log($"[PachiAppCheck] 書き出し コマ {files.Length}／{ex.TotalFrames} 長さ {ex.Duration:F2} 秒");
            Expect(files.Length == ex.TotalFrames && ex.TotalFrames == ExportPlan.FrameCount(ex.Duration), "コマ数が終わりの時刻まで");
            Expect(ex.Percent == 100, "終わったら 100%");
            bool up = true;
            for (int i = 1; i < pct.Count; i++) up &= pct[i] >= pct[i - 1];
            Expect(up, "％が下がらない");
            Expect(ex.Preview != null && ex.Preview.width == ExportPlan.Width && ex.Preview.height == ExportPlan.Height, "見せる板が 1080×1920");
            ex.Dispose();
            // 数える基準は1回目のあと（1回目で URP が 1080×1920 用の作業用の板と材質を作り、使い回すため）。2回目からは増えないこと
            int go0 = CountSceneObjects(), mat0 = Count<Material>(), mesh0 = Count<Mesh>(), rt0 = Count<RenderTexture>();
            if (files.Length > 0)
            {
                File.Copy(files[files.Length - 1], Path.Combine(outDir, "export_last.png"), true);
                // 演出は真っ暗から始まり、0.3 秒ごろにプチュンの閃光で白く飛ぶ
                Expect(files.Length > 10 && Brightness(files[10], null) > 0.5f, "10コマ目（プチュンの閃光）が明るい");
                Expect(Brightness(files[files.Length / 2], null) > 0.01f, "真ん中のコマが真っ黒でない");
            }

            // 印なし：印の範囲の見た目が変わる
            string dirNo = Path.Combine(outDir, "export_frames_nomark");
            var exNo = new VideoExporter(Words, new EffectSettings(), false, new PngVideoWriter(), dirNo);
            Expect(exNo.Start() == ExportError.None, "印なしの書き出しを始められる");
            RunExport(exNo);
            exNo.Dispose();
            var filesNo = Frames(dirNo);
            if (files.Length > 0 && filesNo.Length == files.Length && wm != null)
            {
                var r = ExportPlan.WatermarkRect(wm.width, wm.height);
                float a = Brightness(files[files.Length - 1], r), b = Brightness(filesNo[filesNo.Length - 1], r);
                Debug.Log($"[PachiAppCheck] 印の範囲の明るさ あり {a:F3} なし {b:F3}");
                Expect(Mathf.Abs(a - b) > 0.05f, "印ありとなしで右下が違う");
            }
            else Expect(false, "印なしのコマ数");

            // 受け取り待ち（Busy）：iPhone の動画づくりは、ときどき「まだ受け取れない」を返す。
            // 次の Step でもう一度渡したコマが、ふつうに書いたコマと同じ中身であること
            string dirBusy = Path.Combine(outDir, "export_frames_busy");
            var exB = new VideoExporter(Words, new EffectSettings(), true, new BusyWriter(), dirBusy);
            exB.Start();
            for (int i = 0; i < 200 && exB.Status == ExportStatus.Running && !File.Exists(Path.Combine(dirBusy, "f0030.png")); i++)
            {
                exB.Step();
                AsyncGPUReadback.WaitAllRequests();
            }
            bool same = File.Exists(Path.Combine(dirBusy, "f0030.png")) && files.Length > 30;
            for (int f = 0; same && f <= 30; f++)
                same = File.ReadAllBytes(Path.Combine(dirBusy, $"f{f:D4}.png")).AsSpan().SequenceEqual(File.ReadAllBytes(files[f]));
            Expect(same, "受け取り待ちのあとに渡したコマも、ふつうと同じ中身");
            exB.Dispose();
            DeleteDir(dir); DeleteDir(dirNo); DeleteDir(dirBusy);   // 1回で 1GB 近くになるので残さない

            // やめる：作りかけが消える
            var exC = new VideoExporter(Words, new EffectSettings(), true, new PngVideoWriter(), dir);
            exC.Start();
            for (int i = 0; i < 30; i++) { exC.Step(); AsyncGPUReadback.WaitAllRequests(); }
            exC.Cancel();
            Expect(exC.Status == ExportStatus.Cancelled && !Directory.Exists(dir), "やめたら作りかけが消える");
            exC.Dispose();

            // 書いている途中で失敗：知らせの種類と、作りかけが消える
            var exF = new VideoExporter(Words, new EffectSettings(), true, new FailingWriter(), dir);
            exF.Start();
            RunExport(exF);
            Expect(exF.Status == ExportStatus.Failed && exF.Error == ExportError.WriteFailed && !Directory.Exists(dir), "途中で失敗したら作りかけが消える");
            exF.Dispose();

            // 空き容量が無い：始めない
            var exS = new VideoExporter(Words, new EffectSettings(), true, new FullDiskWriter(), dir);
            Expect(exS.Start() == ExportError.NoSpace && !Directory.Exists(dir), "空き容量が無ければ始めない");
            exS.Dispose();

            int go1 = CountSceneObjects(), mat1 = Count<Material>(), mesh1 = Count<Mesh>(), rt1 = Count<RenderTexture>();
            Debug.Log($"[PachiAppCheck] 書き出しの前後 物 {go0}→{go1} 材質 {mat0}→{mat1} メッシュ {mesh0}→{mesh1} 板 {rt0}→{rt1}");
            Expect(go1 == go0 && mat1 == mat0 && mesh1 == mesh0 && rt1 == rt0, "書き出しをくり返しても物が残らない");
        }

        static List<int> RunExport(VideoExporter ex)
        {
            var pct = new List<int>();
            for (int guard = 0; ex.Status == ExportStatus.Running && guard < 5000; guard++)
            {
                ex.Step();
                AsyncGPUReadback.WaitAllRequests();
                pct.Add(ex.Percent);
            }
            return pct;
        }

        static string[] Frames(string dir)
        {
            if (!Directory.Exists(dir)) return new string[0];
            var f = Directory.GetFiles(dir, "f*.png");
            Array.Sort(f, StringComparer.Ordinal);
            return f;
        }

        static void DeleteDir(string dir)
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        // PNG の明るさの平均（範囲 r を決めればその中だけ、左下原点の画素）
        static float Brightness(string png, Rect? r)
        {
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(png));
            var area = r ?? new Rect(0, 0, tex.width, tex.height);
            int x0 = (int)area.x, y0 = (int)area.y, w = (int)area.width, h = (int)area.height;
            var px = tex.GetPixels(x0, y0, w, h);
            float sum = 0;
            foreach (var c in px) sum += c.grayscale;
            UnityEngine.Object.DestroyImmediate(tex);
            return sum / px.Length;
        }

        // 10コマ目で書けなくなる書き手
        class FailingWriter : PngVideoWriter
        {
            public override WriteResult Append(NativeArray<byte> bgra, int frame) => frame == 10 ? WriteResult.Failed : base.Append(bgra, frame);
        }

        // コマごとに1回目は「まだ受け取れない」を返す書き手
        class BusyWriter : PngVideoWriter
        {
            int last = -1;
            public override WriteResult Append(NativeArray<byte> bgra, int frame)
            {
                if (frame != last) { last = frame; return WriteResult.Busy; }
                return base.Append(bgra, frame);
            }
        }

        // 空き容量が無い書き手
        class FullDiskWriter : PngVideoWriter
        {
            public override long FreeBytes(string dir) => 0;
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
