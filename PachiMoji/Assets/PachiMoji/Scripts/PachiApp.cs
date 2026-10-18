using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace PachiMoji
{
    /// <summary>
    /// アプリの流れ：言葉を入れて待つ → PUSH で演出 → 終わったら「もう一回」「言葉を変える」。
    /// 舞台を組み立て直すのは、入力を確定したときと設定を閉じたときだけ（言葉と設定が前と同じなら作り直さない）。
    /// </summary>
    public class PachiApp : MonoBehaviour
    {
        const string WordsKey1 = "words.1", WordsKey2 = "words.2";
        const float SinkTime = 0.3f;   // 入力の板と PUSH が沈んで消えるまで（秒）

        EffectSettings settings;
        StageScreen stage;
        string builtKey;
        InputCheck check;
        MainScreen ui;
        Camera uiCam;
        AppState state;
        float t, idleT, sink;
        int sinkDir;   // +1 沈む、-1 戻る、0 止まっている

        Vector2 ScreenPx => new Vector2(Screen.width, Screen.height);

        void Start()
        {
            Application.targetFrameRate = 60;
            settings = EffectSettings.Load();

            uiCam = MainScreen.CreateUiCamera();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            ui = MainScreen.Build(MainScreen.CreateCanvas(uiCam), Screen.safeArea, ScreenPx);
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
            ui.Pulse(Time.unscaledTime);

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

        void OnDestroy() => stage?.Dispose();

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
                         + settings.SparksOn + settings.Light + settings.Size;
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

        // 設定画面は Task 8 でつなぐ
        void OpenSettings() { }

        void SetState(AppState s)
        {
            state = s;
            Screen.sleepTimeout = SleepPolicy.For(s);
            ui.ShowState(s);
            // 舞台の絵を置く範囲：待ちは入力の板より上、再生・終わりはバナーより上の全部
            stage.SetFull(s == AppState.Playing || s == AppState.Finished);
            stage.Frame();
        }
    }
}
