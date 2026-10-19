using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace PachiMoji
{
    /// <summary>
    /// アプリの流れ：言葉を入れて待つ → PUSH で演出 → 終わったら「もう一回」「書き出す」「言葉を変える」。
    /// 書き出すと、画面の舞台を片付けて書き出し用の演出を別に組み立て、終わったら「写真に保存」「共有」。
    /// 広告を消していない人は、書き出しの前にごほうび広告を最後まで見る（ExportGate）。印は買ったもので決まる（Entitlements）。
    /// 舞台を組み立て直すのは、入力を確定したときと設定を閉じたときだけ（言葉と設定が前と同じなら作り直さない）。
    /// </summary>
    public class PachiApp : MonoBehaviour
    {
        const string WordsKey1 = "words.1", WordsKey2 = "words.2";
        const float SinkTime = 0.3f;    // 入力の板と PUSH が沈んで消えるまで（秒）
        const float SlideTime = 0.25f;  // 設定画面がせり上がるまで（秒）

        EffectSettings settings;
        StageScreen stage;
        VideoExporter exporter;
        Shop shop;
        Ads ads;
        float gateCheckT;   // ネットのつながりを確かめ直すまで（秒）
        string builtKey;
        InputCheck check;
        MainScreen ui;
        SettingsScreen settingsUi;
        Camera uiCam;
        AppState state;
        float t, idleT, sink, slide = 1;
        int sinkDir;    // +1 沈む、-1 戻る、0 止まっている
        int slideDir;   // -1 せり上がる、+1 下がる、0 止まっている

        Vector2 ScreenPx => new Vector2(Screen.width, Screen.height);

        void Start()
        {
            Application.targetFrameRate = 60;
            settings = EffectSettings.Load();

            uiCam = MainScreen.CreateUiCamera();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var root = MainScreen.CreateCanvas(uiCam);
            ui = MainScreen.Build(root, Screen.safeArea, ScreenPx);
            settingsUi = SettingsScreen.Build(root, Screen.safeArea, ScreenPx, settings, s => s.Save());
            settingsUi.Back.onClick.AddListener(CloseSettings);
            stage = new StageScreen(ui, uiCam);
            ui.Line1.text = PlayerPrefs.GetString(WordsKey1, "定時");
            ui.Line2.text = PlayerPrefs.GetString(WordsKey2, "退社");
            ui.Line1.onValueChanged.AddListener(_ => Validate());
            ui.Line2.onValueChanged.AddListener(_ => Validate());
            ui.Line1.onEndEdit.AddListener(_ => Rebuild());
            ui.Line2.onEndEdit.AddListener(_ => Rebuild());
            ui.Line1.onSubmit.AddListener(_ => ui.Line2.ActivateInputField());   // 1行目で改行キー → 2行目へ
            ui.Push.onClick.AddListener(OnPush);
            ui.Again.onClick.AddListener(StartPlay);
            ui.ChangeWords.onClick.AddListener(BackToIdle);
            ui.Gear.onClick.AddListener(OpenSettings);
            ui.Export.onClick.AddListener(OnExportPressed);
            ui.CancelExport.onClick.AddListener(CancelExport);
            ui.SavePhoto.onClick.AddListener(SavePhoto);
            ui.Share.onClick.AddListener(() => VideoShare.Share(exporter?.OutputPath));
            ui.CloseExport.onClick.AddListener(BackToFinished);

            // 課金と広告：端末に残っていた買ったもので始める（ネットなしでも効く）
            shop = new Shop(Entitlements.Load());
            shop.Changed += OnShopChanged;
            shop.Notice += settingsUi.ShowShopNotice;
            settingsUi.Buy = shop.Buy;
            settingsUi.Restore.onClick.AddListener(shop.Restore);
            ads = new Ads();
            ads.Changed += RefreshExportGate;
            ads.Start(shop.Owned.ShowAds);
            OnShopChanged();
            _ = shop.Connect();

            Canvas.ForceUpdateCanvases();
            Rebuild();
            SetState(AppState.Idle);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (sinkDir != 0)
            {
                sink = Mathf.Clamp01(sink + sinkDir * dt / SinkTime);
                ui.SetDeckSink(sink * sink);   // 沈み始めはゆっくり、だんだん速く
                if (sinkDir > 0 && sink >= 1) { sinkDir = 0; StartPlay(); }
                else if (sinkDir < 0 && sink <= 0) sinkDir = 0;
            }
            if (slideDir != 0)
            {
                slide = Mathf.Clamp01(slide + slideDir * dt / SlideTime);
                settingsUi.SetSlide(Smooth(slide));   // 1＝下に隠れている、0＝全部見えている
                if (slideDir > 0 && slide >= 1) { slideDir = 0; settingsUi.Show(false); }
                else if (slideDir < 0 && slide <= 0) slideDir = 0;
            }
            ui.Pulse(Time.unscaledTime);
            if (state == AppState.Exporting) StepExport();
            // 終わりの画面では、機内モードの切り替えなどに合わせて「書き出す」を押せるかを1秒ごとに決め直す
            if (state == AppState.Finished && (gateCheckT -= dt) <= 0)
            {
                gateCheckT = 1;
                RefreshExportGate();
            }

            if (!stage.Visible) return;
            var fx = stage.Effect;
            switch (state)
            {
                case AppState.Idle:
                case AppState.Settings:
                    idleT = Mathf.Repeat(idleT + dt, 1000f);
                    fx.Idle(idleT);
                    break;
                case AppState.Playing:
                    t = PlayClock.Step(t, dt);
                    fx.Evaluate(Mathf.Min(t, fx.Duration));
                    if (t >= fx.Duration) SetState(AppState.Finished);
                    break;
            }
            stage.Frame();
        }

        void OnDestroy()
        {
            exporter?.Dispose();
            stage?.Dispose();
        }

        // 書き出し中に裏へ回ると、iPhone の動画づくりは止まって壊れたファイルが残る。やめて、知らせる
        void OnApplicationPause(bool paused)
        {
            if (!paused || state != AppState.Exporting) return;
            exporter.Cancel();
            ExportFailed(ExportError.Interrupted);
        }

        // ───────── 書き出し ─────────

        // 「書き出す」：広告を消した人はすぐ。ほかはごほうび広告を最後まで見てから
        void OnExportPressed()
        {
            if (state != AppState.Finished || exporter != null) return;
            var gate = Gate();
            switch (gate)
            {
                case GateAction.ExportNow:
                    StartExport();
                    break;
                case GateAction.ShowAd:
                    ads.ShowRewarded(watched =>
                    {
                        if (state != AppState.Finished) return;
                        if (watched) StartExport();
                        else ui.EndNotice.text = ExportGate.NotWatched;
                    });
                    break;
                default:
                    RefreshExportGate();
                    break;
            }
        }

        GateAction Gate() => ExportGate.Decide(shop.Owned.ShowAds,
            Application.internetReachability != NetworkReachability.NotReachable, ads.Rewarded);

        // 終わりの画面の「書き出す」を押せるかと、その知らせ
        void RefreshExportGate()
        {
            if (state != AppState.Finished || exporter != null) return;
            var gate = Gate();
            ui.SetExportReady(gate == GateAction.ExportNow || gate == GateAction.ShowAd);
            string msg = ExportGate.Message(gate);
            if (msg != "") ui.EndNotice.text = msg;
            else if (ui.EndNotice.text == ExportGate.Message(GateAction.NeedNetwork) || ui.EndNotice.text == ExportGate.Message(GateAction.Wait))
                ui.EndNotice.text = "";   // 直ったら、待ち・ネットの知らせだけ消す（失敗の知らせは残す）
        }

        // 買ったものが変わった：設定の買い物を描き直し、広告を消した人はすぐバナーを消す
        void OnShopChanged()
        {
            settingsUi.ShowShop(shop.Owned, shop.State, shop.PriceOf);
            settingsUi.SetBannerSpace(shop.Owned.ShowAds);
            if (!shop.Owned.ShowAds) ads.SetBanner(false);
            RefreshExportGate();
        }

        void StartExport()
        {
            if (state != AppState.Finished || exporter != null) return;
            ui.EndNotice.text = "";
            // 画面の舞台と書き出しの舞台が同じ場所に重ならないよう、画面のほうは片付ける（戻るときに組み立て直す）
            stage.Dispose();
            builtKey = null;
            bool ios = Application.platform == RuntimePlatform.IPhonePlayer;
            string path = Path.Combine(Application.temporaryCachePath, ios ? "noujiru.mp4" : "noujiru_frames");
            IVideoWriter writer = ios ? new IosVideoWriter() : new PngVideoWriter();
            exporter = new VideoExporter(check.Lines, settings, watermark: shop.Owned.ShowWatermark, writer, path);
            var e = exporter.Start();
            if (e != ExportError.None)
            {
                ExportFailed(e);
                return;
            }
            ui.ExportView.texture = exporter.Preview;
            ui.SetExportProgress(0);
            SetState(AppState.Exporting);
        }

        void StepExport()
        {
            exporter.Step();
            ui.SetExportProgress(exporter.Percent);
            if (exporter.Status == ExportStatus.Done)
            {
                ui.SetSaved(false);
                SetState(AppState.Exported);
            }
            else if (exporter.Status == ExportStatus.Failed) ExportFailed(exporter.Error);
        }

        void CancelExport()
        {
            if (state != AppState.Exporting) return;
            exporter.Cancel();
            BackToFinished();
        }

        void ExportFailed(ExportError e)
        {
            BackToFinished();
            ui.EndNotice.text = ExportRules.Message(e);
        }

        // 終わりの画面に戻る：書き出しを片付け、画面の舞台を組み立て直して最後の姿にする（mp4 は共有のため次の書き出しまで残す）
        void BackToFinished()
        {
            exporter?.Dispose();
            exporter = null;
            ui.ExportView.texture = null;
            ui.EndNotice.text = "";
            SetState(AppState.Finished);
            Rebuild();
            if (stage.Effect != null) stage.Effect.Evaluate(stage.Effect.Duration);
            stage.Frame();
        }

        void SavePhoto()
        {
            if (state != AppState.Exported) return;
            ui.SavePhoto.interactable = false;   // 返事が来るまで2回押せないように
            VideoShare.SaveToPhotos(exporter.OutputPath, e =>
            {
                if (state != AppState.Exported) return;
                if (e == ExportError.None) ui.SetSaved(true);
                else
                {
                    ui.SavePhoto.interactable = true;
                    ui.EndNotice.text = ExportRules.Message(e);
                }
            });
        }

        /// <summary>打つたびに確かめる：知らせを出し、PUSH を押せるかを決める（舞台は作り直さない）</summary>
        void Validate()
        {
            var font = PachiText.LoadFont(settings.FontName);
            check = InputRules.Check(ui.Line1.text, ui.Line2.text, cp => font.GlyphIndex(cp) != 0);
            ui.Notice.text = InputRules.Message(check, MainScreen.CanShowInBody(check.BadChar));
            ui.SetPushEnabled(check.CanPlay);
        }

        /// <summary>入力を確定したとき：言葉か設定が変わっていれば舞台を組み立て直す</summary>
        void Rebuild()
        {
            Validate();
            if (!check.CanPlay)
            {
                stage.SetVisible(false);   // 前の言葉を出したままにしない
                return;
            }
            string key = string.Join("\n", check.Lines) + "|" + settings.FontName + settings.From + settings.To
                         + settings.SparksOn + settings.Light + settings.Size + settings.FirstText + settings.FirstBack;
            if (stage.Effect == null || key != builtKey)
            {
                stage.Build(check.Lines, settings, ScreenPx);
                builtKey = key;
                PlayerPrefs.SetString(WordsKey1, ui.Line1.text);
                PlayerPrefs.SetString(WordsKey2, ui.Line2.text);
                PlayerPrefs.Save();
            }
            stage.SetVisible(true);
            stage.SetFull(state == AppState.Playing || state == AppState.Finished);
            if (state == AppState.Idle) stage.Effect.Idle(idleT);
            stage.Frame();
        }

        void OnPush()
        {
            Rebuild();
            if (!check.CanPlay || !stage.Visible || state != AppState.Idle || sinkDir != 0) return;
            EventSystem.current?.SetSelectedGameObject(null);   // キーボードを閉じる
            sinkDir = 1;
        }

        void StartPlay()
        {
            t = 0;
            SetState(AppState.Playing);
        }

        void BackToIdle()
        {
            SetState(AppState.Idle);
            sinkDir = -1;
        }

        static float Smooth(float x) => x * x * (3 - 2 * x);

        void OpenSettings()
        {
            if (state != AppState.Idle || sinkDir != 0) return;
            EventSystem.current?.SetSelectedGameObject(null);
            SetState(AppState.Settings);
            stage.SetVisible(false);   // 設定画面は全面を覆うので、裏の舞台は描かない
            settingsUi.SetSlide(1);   // 下に隠れた位置から（開いた最初のコマで全部見えないように）
            settingsUi.Show(true);
            slideDir = -1;
        }

        // 閉じたら、演出に関わる設定が変わっていれば舞台を組み立て直し、書体が変わっていれば入力を確かめ直す（Rebuild が両方する）
        void CloseSettings()
        {
            if (state != AppState.Settings) return;
            slideDir = 1;
            SetState(AppState.Idle);
            Rebuild();
        }

        void SetState(AppState s)
        {
            state = s;
            Screen.sleepTimeout = SleepPolicy.For(s);
            ui.ShowState(s);
            if (s == AppState.Finished) gateCheckT = 0;   // 終わりの画面に入ったら、すぐ「書き出す」を押せるか決める
            // 舞台の絵を置く範囲：待ちは入力の板より上、再生・終わりはバナーより上の全部
            stage.SetFull(s == AppState.Playing || s == AppState.Finished);
            stage.Frame();
        }
    }
}
