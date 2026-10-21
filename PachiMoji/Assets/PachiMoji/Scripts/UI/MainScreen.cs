using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PachiMoji
{
    /// <summary>
    /// メイン画面の組み立て（設定と買い物は、上に重なる別の画面）。上は演出の舞台、その下に台の下皿まわりのような銀の板（入力欄）、
    /// まん中に赤いチャンスボタン（PUSH）、いちばん下はバナー広告の場所。
    /// 部品の置き場所だけを持ち、流れ（いつ何を出すか）は PachiApp が決める。
    /// </summary>
    public class MainScreen
    {
        public const int UiLayer = 5;

        public TMP_InputField Line1, Line2;
        public Button Push, Again, Export, ChangeWords, Gear, ShopButton;
        public TMP_Text Notice;
        public RawImage ExportView;                          // 書き出している板（9:16）を範囲に収めて見せる
        public Button CancelExport, SavePhoto, Share, CloseExport;
        public TMP_Text EndNotice;
        public TMP_Text AdDiag;                              // テスト用の版だけ：広告の段階（いちばん上に小さく）                           // 終わり・できたの画面の知らせ（失敗の直し方・保存しました）
        public RectTransform StageIdleArea, StagePlayArea;   // 舞台の絵を置く範囲（待ち・再生）
        public RawImage StageView;                           // 舞台の絵を貼る板（StageScreen が使う）
        RectTransform deck; CanvasGroup deckGroup, endGroup, gearGroup, shopGroup, changeGroup;
        CanvasGroup runGroup, doneGroup, closeGroup, endNoticeGroup;
        TMP_Text percentLabel, saveLabel; RectTransform progressFill; float progressWidth;
        Image dome; TMP_Text pushLabel;
        float deckHeight; bool pushOn = true;

        /// <summary>UI カメラに描く Canvas を作る（基準 390×844、幅に合わせる）。返すのは根っこ</summary>
        public static RectTransform CreateCanvas(Camera uiCam)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = UiLayer;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCam;
            canvas.planeDistance = 1;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiTheme.ReferenceSize;
            scaler.matchWidthOrHeight = 0;
            return (RectTransform)go.transform;
        }

        /// <param name="safePx">ノッチ・ホームバーを避けた範囲（Screen.safeArea と同じ、画素）</param>
        /// <param name="screenPx">画面全体の大きさ（画素）</param>
        public static MainScreen Build(RectTransform root, Rect safePx, Vector2 screenPx)
        {
            var m = new MainScreen();
            float canvasH = UiTheme.ReferenceSize.x * screenPx.y / screenPx.x;   // 幅 390 に合わせたときの高さ
            float topInset = (screenPx.y - safePx.yMax) / screenPx.y * canvasH;

            var safe = Rect(root, "Safe");
            safe.anchorMin = new Vector2(safePx.xMin / screenPx.x, safePx.yMin / screenPx.y);
            safe.anchorMax = new Vector2(safePx.xMax / screenPx.x, safePx.yMax / screenPx.y);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            float safeH = (safe.anchorMax.y - safe.anchorMin.y) * canvasH;

            // いちばん下：バナー広告の場所（順番4で広告を置く）
            var banner = Rect(safe, "BannerSlot");
            Bottom(banner, 0, UiTheme.BannerHeight);

            // 入力の板・知らせ・PUSH をまとめた台（PUSH を押すと下へ沈んで消える）
            float d = Mathf.Min(UiTheme.PushDiameter, safeH * 0.21f);
            m.deckHeight = 14 + d + 30 + 112 + 10;
            m.deck = Rect(safe, "Deck");
            Bottom(m.deck, UiTheme.BannerHeight, m.deckHeight);
            m.deckGroup = m.deck.gameObject.AddComponent<CanvasGroup>();

            // 赤いチャンスボタン
            var domeRt = Rect(m.deck, "Push");
            Place(domeRt, new Vector2(0.5f, 0), new Vector2(0, 14 + d / 2), new Vector2(d, d));
            m.dome = domeRt.gameObject.AddComponent<Image>();
            m.dome.sprite = UiArt.Dome(true);
            m.Push = domeRt.gameObject.AddComponent<Button>();
            m.Push.transition = Selectable.Transition.None;
            domeRt.gameObject.AddComponent<PressSink>();
            m.pushLabel = Label(domeRt, "PUSH", UiTheme.Display, d * 0.24f, Color.white);
            m.pushLabel.rectTransform.anchoredPosition = new Vector2(0, d * 0.03f);

            // 知らせ（問題が無ければ空）
            var notice = Rect(m.deck, "Notice");
            Place(notice, new Vector2(0.5f, 0), new Vector2(0, 14 + d + 15), new Vector2(350, 28));
            m.Notice = Label(notice, "", UiTheme.Body, 13, UiTheme.Muted);
            m.Notice.enableAutoSizing = true; m.Notice.fontSizeMin = 9; m.Notice.fontSizeMax = 13;

            // 銀メッキの板の中に、1行ずつの入力欄が2段
            var plate = Rect(m.deck, "Plate");
            Place(plate, new Vector2(0.5f, 0), new Vector2(0, 14 + d + 30 + 56), new Vector2(340, 112));
            var plateImg = plate.gameObject.AddComponent<Image>();
            plateImg.sprite = UiArt.ChromePlate();
            plateImg.type = Image.Type.Sliced;
            m.Line1 = Field(plate, "Line1", new Vector2(0, 24), "1行目（6文字まで）");
            m.Line2 = Field(plate, "Line2", new Vector2(0, -24), "2行目（なくてもOK）");
            var rule = Rect(plate, "Rule");
            Place(rule, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(290, 1));
            rule.gameObject.AddComponent<Image>().color = new Color(UiTheme.Chrome.r, UiTheme.Chrome.g, UiTheme.Chrome.b, 0.18f);

            // 舞台の範囲：待ちは入力の板より上、再生はバナーより上の全部（どちらもノッチの下まで伸ばす）
            m.StageIdleArea = Rect(safe, "StageIdle");
            Stretch(m.StageIdleArea, UiTheme.BannerHeight + m.deckHeight, -topInset);
            m.StagePlayArea = Rect(safe, "StagePlay");
            Stretch(m.StagePlayArea, UiTheme.BannerHeight, -topInset);
            m.StageIdleArea.SetAsFirstSibling(); m.StagePlayArea.SetAsFirstSibling();
            var view = Rect(m.StagePlayArea, "StageView");
            view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one;
            view.offsetMin = view.offsetMax = Vector2.zero;
            m.StageView = view.gameObject.AddComponent<RawImage>();
            m.StageView.raycastTarget = false;
            m.StageView.enabled = false;

            // 右上：設定の歯車
            var gear = Rect(safe, "Gear");
            Place(gear, new Vector2(1, 1), new Vector2(-30, -30), new Vector2(44, 44));
            gear.gameObject.AddComponent<Image>().color = Color.clear;   // 押せる範囲
            m.Gear = gear.gameObject.AddComponent<Button>();
            var gearIcon = Rect(gear, "Icon");
            Place(gearIcon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28, 28));
            gearIcon.gameObject.AddComponent<Image>().sprite = UiArt.Gear();
            m.gearGroup = gear.gameObject.AddComponent<CanvasGroup>();

            // 右上・歯車の左：買い物（玉貸機の投入口）
            var shop = Rect(safe, "ShopButton");
            Place(shop, new Vector2(1, 1), new Vector2(-80, -30), new Vector2(44, 44));
            shop.gameObject.AddComponent<Image>().color = Color.clear;   // 押せる範囲
            m.ShopButton = shop.gameObject.AddComponent<Button>();
            var shopIcon = Rect(shop, "Icon");
            Place(shopIcon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            shopIcon.gameObject.AddComponent<Image>().sprite = UiArt.Shop();
            m.shopGroup = shop.gameObject.AddComponent<CanvasGroup>();

            // 左上：言葉を変える（終わったあとだけ）
            m.ChangeWords = PlateButton(safe, "言葉を変える", new Vector2(0, 1), new Vector2(76, -32), new Vector2(136, 44), UiTheme.Body, 15, UiTheme.Chrome);
            m.changeGroup = m.ChangeWords.gameObject.AddComponent<CanvasGroup>();

            // 終わったあと：もう一回・書き出す
            var end = Rect(safe, "EndBar");
            Bottom(end, UiTheme.BannerHeight, 112);
            m.endGroup = end.gameObject.AddComponent<CanvasGroup>();
            // 明るい演出の上でもボタンと文が読めるように、下から暗くぼかす
            var scrim = Rect(end, "Scrim");
            scrim.anchorMin = Vector2.zero; scrim.anchorMax = Vector2.one;
            scrim.offsetMin = Vector2.zero; scrim.offsetMax = new Vector2(0, 90);
            var scrimImg = scrim.gameObject.AddComponent<Image>();
            scrimImg.sprite = UiArt.Scrim();
            scrimImg.raycastTarget = false;
            var bottom = new Vector2(0.5f, 0);
            m.Again = PlateButton(end, "もう一回", bottom, new Vector2(-88, 72), new Vector2(164, 60), UiTheme.Display, 22, UiTheme.Chrome);
            m.Export = PlateButton(end, "書き出す", bottom, new Vector2(88, 72), new Vector2(164, 60), UiTheme.Display, 22, UiTheme.Chrome);

            BuildExport(m, safe, end);

            var diag = Rect(safe, "AdDiag");
            Place(diag, new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(380, 12));
            m.AdDiag = Label(diag, "", UiTheme.Body, 9, UiTheme.Muted);
            m.AdDiag.enableAutoSizing = true; m.AdDiag.fontSizeMin = 6; m.AdDiag.fontSizeMax = 9;

            SetLayer(root.gameObject, UiLayer);
            m.ShowState(AppState.Idle);
            return m;
        }

        /// <summary>状態に合わせて部品を出し分ける（台の沈み込みは SetDeckSink で別に動かす）</summary>
        public void ShowState(AppState s)
        {
            bool idle = s == AppState.Idle || s == AppState.Settings || s == AppState.Shop;
            Show(gearGroup, idle);
            Show(shopGroup, idle);
            bool exporting = s == AppState.Exporting, exported = s == AppState.Exported;
            Show(endGroup, s == AppState.Finished);
            Show(changeGroup, s == AppState.Finished);
            Show(runGroup, exporting);
            Show(doneGroup, exported);
            Show(closeGroup, exported);
            Show(endNoticeGroup, s == AppState.Finished || exported);
            ExportView.enabled = exporting || exported;
            // 書き出し中は板を暗く透かして、進み具合の数字を主役にする
            ExportView.color = exporting ? new Color(0.35f, 0.35f, 0.35f, 1) : Color.white;
            deckGroup.interactable = deckGroup.blocksRaycasts = idle;
        }

        /// <summary>
        /// 書き出しの画面。主役は進み具合の数字ひとつ（演出と同じ Reggae One の金）。ほかは黒と銀で静かに。
        /// できたの画面は、終わりの画面と同じ場所・同じ大きさの銀の板（写真に保存・共有）と、左上の「閉じる」
        /// </summary>
        static void BuildExport(MainScreen m, RectTransform safe, RectTransform end)
        {
            // 書き出している板：再生の範囲の中に 9:16 のまま収める
            var view = Rect(m.StagePlayArea, "ExportView");
            view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one;
            view.offsetMin = view.offsetMax = Vector2.zero;
            m.ExportView = view.gameObject.AddComponent<RawImage>();
            m.ExportView.raycastTarget = false;
            m.ExportView.enabled = false;
            var fit = view.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = (float)ExportPlan.Width / ExportPlan.Height;

            // 書き出し中：数字・溝と金の帯・説明・やめる
            var run = Rect(m.StagePlayArea, "ExportRun");
            run.anchorMin = Vector2.zero; run.anchorMax = Vector2.one;
            run.offsetMin = run.offsetMax = Vector2.zero;
            m.runGroup = run.gameObject.AddComponent<CanvasGroup>();
            var num = Rect(run, "Percent");
            Place(num, new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(320, 150));
            m.percentLabel = Label(num, "", UiTheme.Display, 120, UiTheme.Gold);
            m.percentLabel.richText = true;
            var groove = Rect(run, "Groove");
            Place(groove, new Vector2(0.5f, 0.5f), new Vector2(0, -42), new Vector2(260, 22));
            var grooveImg = groove.gameObject.AddComponent<Image>();
            grooveImg.sprite = UiArt.Groove();
            grooveImg.type = Image.Type.Sliced;
            m.progressFill = Rect(groove, "Fill");
            m.progressFill.anchorMin = m.progressFill.anchorMax = new Vector2(0, 0.5f);
            m.progressFill.pivot = new Vector2(0, 0.5f);
            m.progressFill.anchoredPosition = new Vector2(3, 0);
            m.progressWidth = 260 - 6;
            var fillImg = m.progressFill.gameObject.AddComponent<Image>();
            fillImg.sprite = UiArt.Rail();
            fillImg.type = Image.Type.Sliced;
            var note = Rect(run, "Note");
            Place(note, new Vector2(0.5f, 0.5f), new Vector2(0, -76), new Vector2(300, 24));
            Label(note, "書き出し中です", UiTheme.Body, 13, UiTheme.Muted);
            m.CancelExport = PlateButton(run, "やめる", new Vector2(0.5f, 0.5f), new Vector2(0, -132), new Vector2(136, 48), UiTheme.Body, 16, UiTheme.Chrome);

            // できた：写真に保存・共有（終わりの画面の2つと同じ場所）
            var done = Rect(safe, "ExportDone");
            done.anchorMin = end.anchorMin; done.anchorMax = end.anchorMax; done.pivot = end.pivot;
            done.anchoredPosition = end.anchoredPosition; done.sizeDelta = end.sizeDelta;
            m.doneGroup = done.gameObject.AddComponent<CanvasGroup>();
            var scrim = Rect(done, "Scrim");
            scrim.anchorMin = Vector2.zero; scrim.anchorMax = Vector2.one;
            scrim.offsetMin = Vector2.zero; scrim.offsetMax = new Vector2(0, 90);
            var scrimImg = scrim.gameObject.AddComponent<Image>();
            scrimImg.sprite = UiArt.Scrim();
            scrimImg.raycastTarget = false;
            var bottom = new Vector2(0.5f, 0);
            m.SavePhoto = PlateButton(done, "写真に保存", bottom, new Vector2(-88, 72), new Vector2(164, 60), UiTheme.Display, 20, UiTheme.Chrome);
            m.saveLabel = m.SavePhoto.GetComponentInChildren<TMP_Text>();
            m.Share = PlateButton(done, "共有", bottom, new Vector2(88, 72), new Vector2(164, 60), UiTheme.Display, 22, UiTheme.Chrome);
            m.CloseExport = PlateButton(safe, "閉じる", new Vector2(0, 1), new Vector2(76, -32), new Vector2(136, 44), UiTheme.Body, 15, UiTheme.Chrome);
            m.closeGroup = m.CloseExport.gameObject.AddComponent<CanvasGroup>();

            // 知らせ：終わり（失敗の直し方）とできた（保存しました）の、ボタンの下に2行まで
            var notice = Rect(safe, "EndNotice");
            Place(notice, new Vector2(0.5f, 0), new Vector2(0, UiTheme.BannerHeight + 22), new Vector2(350, 36));
            m.endNoticeGroup = notice.gameObject.AddComponent<CanvasGroup>();
            m.EndNotice = Label(notice, "", UiTheme.Body, 12, UiTheme.Chrome);
            m.EndNotice.textWrappingMode = TextWrappingModes.Normal;
            m.EndNotice.enableAutoSizing = true; m.EndNotice.fontSizeMin = 9; m.EndNotice.fontSizeMax = 12;
            m.SetExportProgress(0);
        }

        /// <summary>「書き出す」を押せるか（ネットが無い・広告が読めないときは押せない。文字を灰にする）</summary>
        public void SetExportReady(bool ready)
        {
            Export.interactable = ready;
            Export.GetComponentInChildren<TMP_Text>().color = ready ? UiTheme.Chrome : UiTheme.Muted;
        }

        /// <summary>書き出しの進み具合（0〜100）</summary>
        public void SetExportProgress(int percent)
        {
            percent = Mathf.Clamp(percent, 0, 100);
            percentLabel.text = percent + "<size=33%>%</size>";
            // 帯は端の丸みのぶん（高さ 16）から伸びる。0% は帯を隠す
            progressFill.sizeDelta = new Vector2(Mathf.Lerp(16, progressWidth, percent / 100f), 16);
            progressFill.gameObject.SetActive(percent > 0);
        }

        /// <summary>写真に保存できたら、ボタンを押せなくして知らせる</summary>
        public void SetSaved(bool saved)
        {
            SavePhoto.interactable = !saved;
            saveLabel.color = saved ? UiTheme.Muted : UiTheme.Chrome;
            EndNotice.text = saved ? "写真に保存しました" : "";
        }

        /// <summary>台の沈み具合（0＝いつもの位置、1＝下へ沈んで見えない）</summary>
        public void SetDeckSink(float k)
        {
            deck.anchoredPosition = new Vector2(0, UiTheme.BannerHeight - k * (deckHeight + UiTheme.BannerHeight));
            deckGroup.alpha = 1 - k;
        }

        public void SetPushEnabled(bool on)
        {
            pushOn = on;
            Push.interactable = on;
            dome.sprite = UiArt.Dome(on);
            dome.color = Color.white;
            pushLabel.color = on ? Color.white : new Color(1, 1, 1, 0.3f);
        }

        /// <summary>PUSH がゆっくり脈打つ（実機のチャンスボタンの点滅）。押せないときは止める</summary>
        public void Pulse(float time)
        {
            if (!pushOn) return;
            float k = 0.5f + 0.5f * Mathf.Sin(time * 2 * Mathf.PI / 1.6f);
            dome.color = Color.Lerp(new Color(0.8f, 0.8f, 0.8f, 1), Color.white, k);
        }

        /// <summary>画面の部品が占める範囲を、カメラの描く範囲（0〜1）にする</summary>
        public static Rect ViewportOf(RectTransform area, Camera uiCam)
        {
            var c = new Vector3[4];
            area.GetWorldCorners(c);
            Vector3 a = uiCam.WorldToViewportPoint(c[0]), b = uiCam.WorldToViewportPoint(c[2]);
            float x0 = Mathf.Clamp01(a.x), y0 = Mathf.Clamp01(a.y), x1 = Mathf.Clamp01(b.x), y1 = Mathf.Clamp01(b.y);
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }

        /// <summary>画面に描くただ1台のカメラ（黒で消して、画面の部品を描く。舞台は StageView に貼った絵）</summary>
        public static Camera CreateUiCamera()
        {
            var cam = new GameObject("UiCamera").AddComponent<Camera>();
            cam.transform.position = new Vector3(0, -1000, 0);   // 舞台から遠くに置いて映り込ませない
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 1 << UiLayer;
            return cam;
        }

        /// <summary>説明文の書体で出せる1字か（出せない字は、知らせの文で場所を伝える）</summary>
        public static bool CanShowInBody(string s) =>
            s != null && s.Length == 1 && UiTheme.Body != null && UiTheme.Body.HasCharacter(s[0], false, true);

        /// <summary>舞台のカメラの絵を、描き先の中の範囲（0〜1）に収める。targetPx は描き先の画素。毎コマ、演出が画角を決めたあとに呼ぶ</summary>
        public static void FrameStage(Camera stageCam, Rect viewport, Vector2 targetPx)
        {
            float aspect = viewport.width * targetPx.x / Mathf.Max(1, viewport.height * targetPx.y);
            stageCam.projectionMatrix = StageFraming.Projection(stageCam.fieldOfView, aspect,
                stageCam.nearClipPlane, stageCam.farClipPlane, viewport);
        }

        // ───────── 組み立ての小道具 ─────────

        internal static void Show(CanvasGroup g, bool on)
        {
            g.alpha = on ? 1 : 0;
            g.interactable = g.blocksRaycasts = on;
        }

        internal static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        // 親の下から y の高さに h の帯
        static void Bottom(RectTransform rt, float y, float h)
        {
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, y);
            rt.sizeDelta = new Vector2(0, h);
        }

        // 親いっぱいに広げ、下を bottom だけ空け、上を top だけ動かす（マイナスで外へ伸ばす）
        static void Stretch(RectTransform rt, float bottom, float top)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(0, bottom);
            rt.offsetMax = new Vector2(0, -top);
        }

        internal static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        internal static TMP_Text Label(RectTransform parent, string s, TMP_FontAsset font, float size, Color c)
        {
            var rt = Rect(parent, "Label");
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font; t.fontSize = size; t.color = c; t.text = s;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        // 銀の板の文字ボタン。押せないときも板は薄めず、文字の色（灰）で伝える
        internal static Button PlateButton(RectTransform parent, string text, Vector2 anchor, Vector2 pos, Vector2 size,
            TMP_FontAsset font, float fontSize, Color textColor)
        {
            var rt = Rect(parent, text);
            Place(rt, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiArt.ChromePlate();
            img.type = Image.Type.Sliced;
            var b = rt.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            rt.gameObject.AddComponent<PressSink>();
            Label(rt, text, font, fontSize, textColor);
            return b;
        }

        static TMP_InputField Field(RectTransform plate, string name, Vector2 pos, string placeholder)
        {
            var rt = Rect(plate, name);
            Place(rt, new Vector2(0.5f, 0.5f), pos, new Vector2(300, 44));
            rt.gameObject.AddComponent<Image>().color = Color.clear;   // 押せる範囲
            var area = Rect(rt, "Text Area");
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(8, 0); area.offsetMax = new Vector2(-8, 0);
            area.gameObject.AddComponent<RectMask2D>();
            var ph = Label(area, placeholder, UiTheme.Body, 15, UiTheme.Muted);
            var text = Label(area, "", UiTheme.Display, 28, UiTheme.Chrome);
            text.richText = false;
            var f = rt.gameObject.AddComponent<TMP_InputField>();
            f.textViewport = area;
            f.textComponent = text;
            f.placeholder = ph;
            f.characterLimit = InputRules.MaxChars;
            f.lineType = TMP_InputField.LineType.SingleLine;
            f.shouldHideMobileInput = false;   // キーボードの上に iPhone の入力欄が出る形（日本語の変換が確実）
            f.richText = false;
            f.caretColor = UiTheme.Gold;
            f.customCaretColor = true;
            f.selectionColor = new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, 0.35f);
            return f;
        }

        internal static void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }

    /// <summary>押しているあいだ、少し沈む（ボタンを押した手ごたえ）</summary>
    public class PressSink : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public void OnPointerDown(PointerEventData e)
        {
            var s = GetComponent<Selectable>();
            if (s != null && !s.interactable) return;
            transform.localScale = Vector3.one * 0.94f;
        }

        public void OnPointerUp(PointerEventData e) => transform.localScale = Vector3.one;
    }
}
