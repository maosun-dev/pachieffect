# 作る順番3「動画の書き出し・印・写真に保存・共有」作業計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 終わりの画面の「書き出す」を動かす。演出を画面に出さない 1080×1920 の板に 1/30 秒ずつ描き、右下に「脳汁メーカー」の印を重ねて、iPhone では mp4（H.264・30fps・無音）にし、写真に保存・共有できるようにする。Windows では PNG を並べる版で同じ流れを確かめる。

**Architecture:** 計算（コマ数・時刻・％・要る空き容量・印の位置・知らせの文）は `PachiMoji.Core` に置いて EditMode テストで確かめる。書き出しの本体 `VideoExporter` は演出を新しく1つ組み立てて板に描き、`AsyncGPUReadback` で1コマずつ読み出して `IVideoWriter` に渡す（iPhone＝自前の Objective-C `PachiVideo.mm`、それ以外＝PNG）。`Step()` を毎コマ呼ぶ形なので、アプリは Update から、手元確認は裏起動の中のループから同じものを動かせる。

**Tech Stack:** Unity 6000.6.6f1・URP・uGUI＋TMP・AsyncGPUReadback・Objective-C（AVAssetWriter・Photos・UIActivityViewController）・NUnit（EditMode）

**Spec:** `docs/superpowers/specs/2026-10-18-app-release-design.md`（「流れ」4、「中の作り › 書き出し」）

## Global Constraints

- 書き出しの大きさ 1080×1920、30コマ/秒、H.264、無音。iPhone 側の部品は `Assets/Plugins/iOS/` に Objective-C で自前で書く（有料・外部の部品は使わない。**お金はかけない**）
- 印は右下に「脳汁メーカー」。印を出すかは引数 `watermark` で受ける（順番4で Entitlements につなぐ。今は常に true）
- 写真への保存は初回だけ許可を聞く。Info.plist に `NSPhotoLibraryAddUsageDescription`（文：`書き出した動画を写真に保存するために使います`）
- 失敗したら「何が起きたか」と「直し方」を出し、作りかけのファイルを消す。進み具合は％で出す
- 画面の色は `UiTheme`（黒 #000000・クロム銀 #C8CDD4・赤 #E3001B は PUSH だけ・金 #F2C14E・灰 #8A8F98）、見出し・数字は Reggae One、説明文は BIZ UDPゴシック
- 画面の文・コメント・コミットメッセージは日本語。コミットの最後に `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`
- 動的な TMP フォント（`Resources/UI/*SDF.asset`）は Unity を動かすと書き換わる。コミット前に `git checkout -- "PachiMoji/Assets/PachiMoji/Resources/UI/BIZUDPGothic SDF.asset" "PachiMoji/Assets/PachiMoji/Resources/UI/ReggaeOne SDF.asset"` で戻し、自分の変えたファイルだけコミットする
- Unity の裏起動は同時に1つだけ（run-tests.sh・app-check.sh を並べて動かさない）
- 手元の Windows には iOS 用の部品が無い。`.mm` と `#if UNITY_IOS && !UNITY_EDITOR` の中は手元でコンパイルされないので、GitHub の組み立て（Mac の段）で初めて確かめられる

## Review Focus

1. **書き出し中にアプリを裏に回す**（電話・ホームに戻る）→ AVAssetWriter は裏で止まり壊れた mp4 が残る。裏に回ったら書き出しをやめ、作りかけを消し、「書き出しを中断しました…」を出す（Task 1 の文のテスト、Task 4 の OnApplicationPause）
2. **写真への保存を「許可しない」にした人** → 黙って失敗せず、設定アプリでの直し方を出す（Task 1 の文のテスト、Task 3 の denied の返し）
3. **空き容量が少ない端末** → 描き始める前に要る容量を見積もって止める。途中で満杯になったら空き容量の知らせ（Task 1 の NeedBytes テスト、Task 2 の Start の確認、Task 3 の満杯の見分け）
4. **最後のコマ** → 演出の最後の姿（終わりの時刻ちょうど）が動画に入る。コマ数の端の数え違いで1コマ欠けない（Task 1 の FrameCount／TimeOf テスト、Task 2 のコマ数の確認）
5. **書き出しを2回以上くり返す・やめる** → 前の板・演出・ファイルが残らず、2回目もふつうに書ける（Task 2 の app-check：2回続けて書き出し、物の数が戻る／やめたら作りかけが消える）

---

## ファイルの地図

| ファイル | 役目 |
|---|---|
| Create `PachiMoji/Assets/PachiMoji/Core/ExportRules.cs` | `ExportPlan`（大きさ・コマ数・時刻・％・要る容量・印の位置）、`ExportError`、`ExportRules.Message` |
| Modify `PachiMoji/Assets/PachiMoji/Core/AppRules.cs` | `AppState` に `Exporting`・`Exported` を足す。`SleepPolicy` は書き出し中も消灯しない |
| Create `PachiMoji/Assets/PachiMoji/Tests/Editor/ExportRulesTests.cs` | 上の計算のテスト |
| Modify `PachiMoji/Assets/PachiMoji/Tests/Editor/AppRulesTests.cs` | 消灯のテストに2つの状態を足す |
| Create `PachiMoji/Assets/PachiMoji/Scripts/Export/IVideoWriter.cs` | 書き手の形 |
| Create `PachiMoji/Assets/PachiMoji/Scripts/Export/PngVideoWriter.cs` | Windows・Editor 用：PNG を並べる |
| Create `PachiMoji/Assets/PachiMoji/Scripts/Export/IosVideoWriter.cs` | iPhone 用：`PachiVideo.mm` を呼ぶ |
| Create `PachiMoji/Assets/PachiMoji/Scripts/Export/VideoExporter.cs` | 板に描く・印・読み出し・書き手へ渡す・後片付け |
| Create `PachiMoji/Assets/PachiMoji/Scripts/Export/VideoShare.cs` | 写真に保存・共有（iPhone は mm、ほかはログだけ）と返事の受け口 |
| Create `PachiMoji/Assets/Plugins/iOS/PachiVideo.mm`（＋`.meta`） | AVAssetWriter・Photos・UIActivityViewController |
| Create `PachiMoji/Assets/PachiMoji/Resources/Export/Watermark.png`（＋`.meta`） | 印の絵（PachiBuild.MakeWatermark が作る） |
| Modify `PachiMoji/Assets/PachiMoji/Editor/PachiBuild.cs` | `MakeWatermark` を足す |
| Modify `PachiMoji/Assets/PachiMoji/Editor/PachiAppCheck.cs` | 書き出し・やめる・失敗・mm の設定・plist の確認、書き出し画面の絵 |
| Modify `PachiMoji/Assets/PachiMoji/Scripts/UI/MainScreen.cs` | 書き出し中・できたの画面、「書き出す」を押せるように |
| Modify `PachiMoji/Assets/PachiMoji/Scripts/PachiApp.cs` | 書き出しの流れ |
| Modify `.github/workflows/ios-testflight.yml` | Info.plist に写真の許可の文を足す |

---

### Task 1: 書き出しの計算と知らせの文（Core）

**Files:**
- Create: `PachiMoji/Assets/PachiMoji/Core/ExportRules.cs`
- Modify: `PachiMoji/Assets/PachiMoji/Core/AppRules.cs`（`AppState`・`SleepPolicy`）
- Test: `PachiMoji/Assets/PachiMoji/Tests/Editor/ExportRulesTests.cs`、`AppRulesTests.cs`

**Interfaces:**
- Produces:
  - `enum AppState { Idle, Playing, Finished, Settings, Exporting, Exported }`（後ろに足すだけ。PlayerPrefs に保存はしていない）
  - `static class ExportPlan`：`const int Width = 1080, Height = 1920, Fps = 30; const long BitRate = 12_000_000;`
    `int FrameCount(float duration)`、`float TimeOf(int frame, float duration)`、`int Percent(int appended, int total, bool finished)`、`long NeedBytes(float duration)`、`Rect WatermarkRect(int texW, int texH)`（左下原点の画素）
  - `enum ExportError { None, NoSpace, CannotOpen, WriteFailed, Interrupted, PhotosDenied, SaveFailed }`
  - `static class ExportRules`：`string Message(ExportError e)`

- [ ] **Step 1: 失敗するテストを書く**（`ExportRulesTests.cs`、名前空間 `PachiMoji.Tests`、AppRulesTests と同じ1行テストの書き方）

```csharp
[Test] public void 最後のコマまで数える() { Assert.AreEqual(451, ExportPlan.FrameCount(15f)); Assert.AreEqual(271, ExportPlan.FrameCount(9f)); }
[Test] public void 割り切れない長さは最後に終わりの時刻のコマを足す() => Assert.AreEqual(272, ExportPlan.FrameCount(9.02f));
[Test] public void 時刻は30分の1秒ずつ() { Assert.AreEqual(0f, ExportPlan.TimeOf(0, 15f)); Assert.AreEqual(1f, ExportPlan.TimeOf(30, 15f), 1e-5f); }
[Test] public void 最後のコマは終わりの時刻ちょうど() { Assert.AreEqual(15f, ExportPlan.TimeOf(450, 15f), 1e-5f); Assert.AreEqual(9.02f, ExportPlan.TimeOf(271, 9.02f), 1e-5f); }
[Test] public void 進み具合は仕上げまで99で止まる()
{
    Assert.AreEqual(0, ExportPlan.Percent(0, 451, false));
    Assert.AreEqual(49, ExportPlan.Percent(225, 451, false));
    Assert.AreEqual(99, ExportPlan.Percent(451, 451, false));
    Assert.AreEqual(100, ExportPlan.Percent(451, 451, true));
}
[Test] public void 要る空き容量は見積もりの1点5倍と50MB() => Assert.AreEqual(86_178_800L, ExportPlan.NeedBytes(15f));
[Test] public void 印は右下に幅の3割()
{
    var r = ExportPlan.WatermarkRect(640, 128);   // 印の絵は 640×128
    Assert.AreEqual(324f, r.width, 1e-3f);
    Assert.AreEqual(64.8f, r.height, 1e-3f);
    Assert.AreEqual(1080f - 37.8f - 324f, r.x, 1e-3f);   // 余白は幅の 3.5%
    Assert.AreEqual(37.8f, r.y, 1e-3f);
}
[Test] public void 知らせの文()
{
    Assert.AreEqual("", ExportRules.Message(ExportError.None));
    Assert.AreEqual("空き容量が足りません。写真やアプリを整理してから、もう一度「書き出す」を押してください", ExportRules.Message(ExportError.NoSpace));
    Assert.AreEqual("動画を作れませんでした。アプリを開き直して、もう一度「書き出す」を押してください", ExportRules.Message(ExportError.CannotOpen));
    Assert.AreEqual("動画を作れませんでした。アプリを開き直して、もう一度「書き出す」を押してください", ExportRules.Message(ExportError.WriteFailed));
    Assert.AreEqual("書き出しを中断しました。もう一度「書き出す」を押してください", ExportRules.Message(ExportError.Interrupted));
    Assert.AreEqual("写真への保存が許可されていません。設定アプリ →「脳汁メーカー」→「写真」で「追加のみ」を選んでください", ExportRules.Message(ExportError.PhotosDenied));
    Assert.AreEqual("写真に保存できませんでした。空き容量を確かめて、もう一度「写真に保存」を押してください", ExportRules.Message(ExportError.SaveFailed));
}
```

`AppRulesTests.再生中だけ消灯しない` に足す：`Exporting` は `NeverSleep`、`Exported` は `SystemSetting`。

- [ ] **Step 2: `sh run-tests.sh` で失敗を見る** — 期待：`ExportPlan` が無いコンパイルエラー（`error CS0103` など）

- [ ] **Step 3: `ExportRules.cs` を書き、`AppState`・`SleepPolicy` を直す**
  - `FrameCount(d) = Mathf.CeilToInt(d * Fps - 1e-3f) + 1`、`TimeOf(i, d) = Mathf.Min(i / (float)Fps, d)`（割り切れない長さでも、最後のコマ `FrameCount-1` が `d` ちょうどになる）
  - `Percent`：`finished ? 100 : Mathf.Min(99, appended * 100 / total)`（total が 0 なら 0）
  - `NeedBytes(d) = (long)(d * BitRate / 8 * 1.5) + 50L * 1024 * 1024`
  - `WatermarkRect`：幅 `Width * 0.30f`、高さは絵の縦横比のまま、余白 `Width * 0.035f`、右下に置く
  - `SleepPolicy.For`：`Playing` と `Exporting` で `NeverSleep`

- [ ] **Step 4: `sh run-tests.sh` で通るのを見る** — 期待：`result="Passed"`、failed="0"

- [ ] **Step 5: コミット** — `git add` は上の3ファイル＋AppRulesTests.cs だけ。メッセージ「書き出しの計算（コマ数・時刻・％・空き容量・印の位置）と知らせの文を足す」

---

### Task 2: 板に描いて書き手へ渡す本体（VideoExporter・PNG 版・印の絵）

**Files:**
- Create: `Scripts/Export/IVideoWriter.cs`、`Scripts/Export/PngVideoWriter.cs`、`Scripts/Export/VideoExporter.cs`
- Create: `Resources/Export/Watermark.png`（MakeWatermark で作る）
- Modify: `Editor/PachiBuild.cs`（`MakeWatermark`）、`Editor/PachiAppCheck.cs`
（パスはどれも `PachiMoji/Assets/PachiMoji/` の下）

**Interfaces:**
- Consumes: Task 1 の `ExportPlan`・`ExportError`、`EffectCatalog.Get("oudou").Create()`（`IPachiEffect`）
- Produces:

```csharp
public enum WriteResult { Ok, Busy, Failed }
public enum FinishState { Writing, Done, Failed }
public interface IVideoWriter
{
    long FreeBytes(string dir);                                      // その場所の空き容量
    ExportError Begin(string path, int w, int h, int fps);           // 作りかけがあれば消してから始める
    WriteResult Append(NativeArray<byte> bgra, int frame);           // 1コマ（BGRA、下の行から）。Busy は次の Step でもう一度
    void Finish();                                                   // 仕上げを始める（終わりは Poll で待つ）
    FinishState Poll(out ExportError error);
    void Cancel();                                                   // 止めて、作りかけを消す
}

public enum ExportStatus { Running, Done, Failed, Cancelled }
public class VideoExporter : System.IDisposable
{
    public VideoExporter(string[] lines, EffectSettings settings, bool watermark, IVideoWriter writer, string outPath);
    public ExportError Start();          // 空き容量を確かめ、演出を組み立て、Begin する。None 以外なら何も残さない
    public void Step();                  // 毎コマ呼ぶ
    public ExportStatus Status { get; }
    public ExportError Error { get; }
    public int Percent { get; }          // ExportPlan.Percent
    public int TotalFrames { get; }
    public Texture Preview { get; }      // 描いている板（画面に薄く出す）
    public string OutputPath { get; }
    public void Cancel();                // Status=Cancelled、作りかけを消す
    public void Dispose();               // 演出・板を片付ける（何度呼んでもよい）
}
```

- [ ] **Step 1: 印の絵を作る `PachiBuild.MakeWatermark()` を書く**
  - 出力：`Assets/PachiMoji/Resources/Export/Watermark.png`、640×128、透明の背景
  - 中身：Reggae One の「脳汁メーカー」1行、文字の高さいっぱい（左右に 16px の余白）。色はクロム：上 `#FFFFFF` → 下 `#8A8F98` の縦のグラデーション（TMP の `enableVertexGradient`）、黒いフチ（outline 0.22）、黒い影（underlay、ずらし 0.5/−0.5、にじみ 0.4）。アイコンや飾りは付けない（どの動画の上でも読めること、演出の邪魔をしないことだけを狙う）
  - 撮り方：PachiAppCheck の ShotUi と同じく、透明（0,0,0,0）で消すカメラで Canvas を RenderTexture に描き、`TextureFormat.RGBA32` で読んで PNG にする
  - 取り込みの設定（TextureImporter）：mipmap なし、Clamp、`alphaIsTransparency = true`、圧縮なし
  - 裏起動：`unity run "$ROOT/PachiMoji" --editor-version 6000.6.6f1 --no-banner -- -executeMethod PachiMoji.EditorTools.PachiBuild.MakeWatermark -logFile "$ROOT/captures/unity.log"`、期待：ログに `[PachiBuild] 印を作った`
  - できた PNG を Read で見て、字がくっきり・フチと影が出ている・四隅が透明なことを確かめる

- [ ] **Step 2: app-check に書き出しの確かめを足す（まだ失敗する）**。`PachiAppCheck.Run` の最後のほうに：
  - 印の絵：`Resources.Load<Texture2D>("Export/Watermark")` が 640×128、四隅の alpha が 0、真ん中の行に alpha>0.5 の画素がある
  - 書き出し：`new VideoExporter(Words, new EffectSettings(), true, new PngVideoWriter(), captures/export_frames)` → `Start()==None` → `while (Status==Running) { Step(); AsyncGPUReadback.WaitAllRequests(); }`（上限 5000 回）→ `Status==Done`、PNG の枚数 == `TotalFrames` == `ExportPlan.FrameCount(演出の長さ)`、`Percent==100`、0.25 秒ごとの％が下がらない
  - 絵：最後の PNG を `captures/export_last.png` に写す（印・最後の姿を目で見るため）。1枚目と真ん中の PNG が真っ黒でない（平均の明るさ > 0.01。1枚目はプチュンの白の頭なので明るい）。最後の PNG の `WatermarkRect` の中に、明るい画素（R>0.7）がある＝印が入っている
  - 印なし：`watermark:false` で別のフォルダ（captures/export_frames_nomark）に書き出し、最後の PNG の印の範囲の明るさの平均が `watermark:true` のときより 0.05 以上低いこと。確かめたらそのフォルダは消す
  - やめる：30 Step 進めて `Cancel()` → `Status==Cancelled`、出力フォルダが無い
  - 失敗：`PngVideoWriter` を継いで 10 コマ目の `Append` で `Failed` を返す書き手 → `Status==Failed`、`Error==WriteFailed`、出力フォルダが無い
  - くり返し：上を全部終えて `Dispose()` したあと、物・材質・メッシュ・RenderTexture の数が始める前と同じ（今の「組み立て直しても物が残らない」と同じ数え方に `Count<RenderTexture>()` を足す）
  - 空き容量：`FreeBytes` が 0 を返す書き手で `Start()==NoSpace`、出力フォルダが無い

- [ ] **Step 3: `sh app-check.sh` で失敗を見る** — 期待：`VideoExporter` が無い `error CS0246`

- [ ] **Step 4: `IVideoWriter`・`PngVideoWriter`・`VideoExporter` を書く**
  - `PngVideoWriter`：`Begin` は出力フォルダを消して作り直す。`Append` は `Texture2D(w,h,BGRA32)` に `LoadRawTextureData` して `f{frame:D4}.png`。`Finish` は何もせず `Poll` は `Done`。`Cancel` はフォルダを消す。`FreeBytes` は `new DriveInfo(Path.GetPathRoot(dir)).AvailableFreeSpace`
  - `VideoExporter.Start`：`writer.FreeBytes(フォルダ) < ExportPlan.NeedBytes(Duration)` なら `NoSpace`。演出は画面の舞台とは別に、`aspect = 1080/1920` で組み立てる。板は `RenderTexture(1080,1920,24,ARGB32,sRGB)`、`Effect.Camera.targetTexture = 板`。カメラの投影は演出が決めたままにする（画面の舞台のように範囲へ縮めない）
  - `Step`：1回に最大2コマ描く。読み出し待ちは最大3つ。描くのは `Evaluate(TimeOf(i))` → `Camera.Render()` → 印 → `AsyncGPUReadback.Request(板, 0, TextureFormat.BGRA32)`。待ちの先頭から順に、終わっていれば `Append`（`Busy` ならその Step はそこでやめる、`Failed` か読み出しの `hasError` なら失敗）。全部渡し終えたら `Finish()`、そのあとは `Poll`
  - 印：`RenderTexture.active = 板; GL.PushMatrix(); GL.LoadPixelMatrix(0, 1080, 0, 1920); Graphics.DrawTexture(WatermarkRect, 印, new Rect(0,0,1,1), 0,0,0,0, 白(alpha 0.9));` のあと元に戻す。export_last.png で上下が逆なら `GL.LoadPixelMatrix(0,1080,1920,0)` と位置の y を `1920 - y - h` にして直す
  - 失敗・やめる：`writer.Cancel()`、待ちの読み出しは捨てる、演出と板を片付ける（`Dispose` と同じ）。`Done` になったら演出だけ片付け、板は `Preview` として残す（`Dispose` で消える）
  - `Debug.Log($"[VideoExporter] frames={TotalFrames} 秒={…:F1}")` を終わりに出す

- [ ] **Step 5: `sh app-check.sh` で通るのを見る** — 期待：`[PachiAppCheck] OK`、`exit=0`。`captures/export_last.png` を Read で見て、虹の最後の姿と右下の印（上下・左右が正しい）を確かめる

- [ ] **Step 6: `sh run-tests.sh` も通るのを見る** — 期待：failed="0"

- [ ] **Step 7: コミット** — TMP フォント2つを戻してから。`Scripts/Export/` の3つ、`Resources/Export/Watermark.png(.meta)`、`PachiBuild.cs`、`PachiAppCheck.cs`。メッセージ「動画の書き出しの本体を作る：板に1コマずつ描き、印を重ね、PNG の版で確かめる」

---

### Task 3: iPhone の部品（PachiVideo.mm）と写真の許可

**Files:**
- Create: `PachiMoji/Assets/Plugins/iOS/PachiVideo.mm`、`PachiVideo.mm.meta`
- Create: `PachiMoji/Assets/PachiMoji/Scripts/Export/IosVideoWriter.cs`、`Scripts/Export/VideoShare.cs`
- Modify: `.github/workflows/ios-testflight.yml`（「番号と申告を書き込む」）、`Editor/PachiAppCheck.cs`

**Interfaces:**
- Consumes: Task 2 の `IVideoWriter`・`WriteResult`・`FinishState`、Task 1 の `ExportError`
- Produces:
  - C の入口（`extern "C"`、`IosVideoWriter` が `[DllImport("__Internal")]` で呼ぶ）：
    `long long PachiVideo_FreeBytes(const char* dir)`、`int PachiVideo_Begin(const char* path, int w, int h, int fps, int bitRate)`（0＝始めた、それ以外は `ExportError` の数）、`int PachiVideo_Append(const void* bgra, int frame)`（0 Ok／1 Busy／2 Failed）、`void PachiVideo_Finish(void)`、`int PachiVideo_Poll(void)`（0 書いている／1 できた／2 失敗）、`int PachiVideo_LastError(void)`（`ExportError` の数）、`void PachiVideo_Cancel(void)`、`void PachiVideo_SaveToPhotos(const char* path)`、`void PachiVideo_Share(const char* path)`
  - `public static class VideoShare`：`static void SaveToPhotos(string path, System.Action<ExportError> done)`、`static void Share(string path)`。返事は `UnitySendMessage("PachiVideoBridge", "OnSaved", "ok"|"denied"|"failed")` で受ける（`VideoShare` が最初に呼ばれたとき、その名前の GameObject に受け口の MonoBehaviour を作り `DontDestroyOnLoad`）
  - `IosVideoWriter`・`VideoShare` の iPhone の中身は `#if UNITY_IOS && !UNITY_EDITOR` の中。それ以外では `VideoShare.SaveToPhotos` はログを出して `done(None)`、`Share` はログだけ

- [ ] **Step 1: `PachiVideo.mm` を書く**（静的な変数で1本だけ持つ。数十〜百数十行）
  - Begin：古いファイルを消す → `AVAssetWriter assetWriterWithURL:fileType:AVFileTypeMPEG4` → 入力 `AVMediaTypeVideo`、設定 `AVVideoCodecKey: AVVideoCodecTypeH264`・幅高さ・`AVVideoCompressionPropertiesKey: { AVVideoAverageBitRateKey: bitRate, AVVideoProfileLevelKey: AVVideoProfileLevelH264HighAutoLevel }`、`expectsMediaDataInRealTime = NO` → `AVAssetWriterInputPixelBufferAdaptor`（`kCVPixelFormatType_32BGRA`、幅高さ）→ `startWriting`・`startSessionAtSourceTime:kCMTimeZero`。作れなければ `CannotOpen`
  - Append：`isReadyForMoreMediaData` でなければ 1。adaptor の `pixelBufferPool` から1枚取り、**行を上下さかさまに**写す（Unity の読み出しは下の行から、動画は上の行から。行ごとに `CVPixelBufferGetBytesPerRow` を使う）→ `appendPixelBuffer:withPresentationTime:CMTimeMake(frame, fps)`。失敗なら writer の error を見て、空き不足（`NSFileWriteOutOfSpaceError`、または `AVErrorDiskFull`）は `NoSpace`、ほかは `WriteFailed`
  - Finish：`markAsFinished` → `finishWritingWithCompletionHandler:` で状態を 1／2 にする。Cancel：`cancelWriting` してファイルを消す
  - FreeBytes：`NSURLVolumeAvailableCapacityForImportantUsageKey`
  - SaveToPhotos：`[PHPhotoLibrary requestAuthorizationForAccessLevel:PHAccessLevelAddOnly handler:]` → 許可なら `performChanges:` の中で `[PHAssetChangeRequest creationRequestForAssetFromVideoAtFileURL:]`、終わりに main で `UnitySendMessage`。許可されていなければ "denied"
  - Share：main で `UIActivityViewController initWithActivityItems:@[NSURL fileURLWithPath:] applicationActivities:nil` を `UnityGetGLViewController()` から出す（`popoverPresentationController.sourceView` に画面の view を入れておく）
  - `#include "UnityInterface.h"` で `UnitySendMessage`・`UnityGetGLViewController` を使う

- [ ] **Step 2: `.mm.meta` で iPhone だけに入れ、要る仕組みをつなぐ** — PluginImporter の iOS 設定を有効にし、`FrameworkDependencies: AVFoundation;CoreMedia;CoreVideo;Photos;`。ほかの環境（Editor・Standalone）は無効

- [ ] **Step 3: `IosVideoWriter.cs`・`VideoShare.cs` を書く** — `Append` は `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr` で中身の場所を渡す（asmdef ではなく Assembly-CSharp なので、Player Settings の「unsafe を許す」を `PachiBuild.Configure` で `PlayerSettings.allowUnsafeCode = true` にする）

- [ ] **Step 4: 組み立ての Mac の段で写真の許可の文を足す** — 「番号と申告を書き込む」に `Delete`→`Add :NSPhotoLibraryAddUsageDescription string 書き出した動画を写真に保存するために使います`（今の暗号の申告と同じ書き方）

- [ ] **Step 5: app-check に足す** — `PluginImporter.GetAtPath("Assets/Plugins/iOS/PachiVideo.mm")` が iOS で有効・Editor で無効・`GetPlatformData(BuildTarget.iOS, "FrameworkDependencies")` に4つとも入っている／ワークフローのファイルに `NSPhotoLibraryAddUsageDescription` がある／`PlayerSettings.allowUnsafeCode`

- [ ] **Step 6: `sh app-check.sh` と `sh run-tests.sh` が通るのを見る** — 期待：`[PachiAppCheck] OK`・failed="0"（mm の中身はここでは確かめられない。Task 5 の組み立てで初めてコンパイルされる）

- [ ] **Step 7: コミット** — メッセージ「iPhone の書き出しの部品（AVAssetWriter）と写真への保存・共有を足す」

---

### Task 4: 書き出しの画面と流れ

**Files:**
- Modify: `PachiMoji/Assets/PachiMoji/Scripts/UI/MainScreen.cs`、`Scripts/PachiApp.cs`、`Editor/PachiAppCheck.cs`

**Interfaces:**
- Consumes: Task 1 の `AppState.Exporting/Exported`・`ExportRules.Message`、Task 2 の `VideoExporter`、Task 3 の `VideoShare`・`IosVideoWriter`
- Produces（MainScreen に足すもの）：`public RawImage ExportView; public Button CancelExport, SavePhoto, Share, CloseExport; public TMP_Text EndNotice;`、`public void SetExportProgress(int percent)`、`public void SetSaved(bool saved)`

見た目（主役は「％の数字」ひとつ。ほかは今の黒と銀のまま静かにする）：

```
書き出し中                         できた
┌─────────────────┐           ┌─────────────────┐
│                 │           │閉じる            │ ← 言葉を変えると同じ銀の板・同じ場所
│  （描いている板が │           │ ┌─────────────┐ │
│   暗く透けて見える）│           │ │ 書き出した動画の │ │ ← 9:16 の最後の姿（印つき）を
│       73        │ ← Reggae One│ │  最後の姿     │ │    範囲に収めて全部見せる
│        %        │   金・特大   │ └─────────────┘ │
│ ━━━━━━━━━━━──── │ ← UiArt.Rail│ 写真に保存   共有 │ ← 銀の板2つ（今の「もう一回／書き出す」と同じ大きさと位置）
│ 書き出し中です   │   に金の帯   │ 写真に保存しました │ ← EndNotice（説明文の灰）
│     やめる       │           ├─────────────────┤
├─────────────────┤           │   バナーの場所     │
│   バナーの場所    │           └─────────────────┘
└─────────────────┘
```

- 書き出し中：ExportView（AspectRatioFitter `FitInParent`・9/16、StagePlayArea の中）に `Preview` を色 `(0.35,0.35,0.35,1)` で出す。中央に％の数字（Reggae One・金・120、数字だけ。「%」は 40 で数字の右下）、その下に `UiArt.Rail()` の溝と金の帯（幅＝％）、灰の「書き出し中です」、銀の板「やめる」
- できた：ExportView を白（そのまま）で出す。下の帯に「写真に保存」「共有」、左上に「閉じる」。保存できたら EndNotice「写真に保存しました」、「写真に保存」の文字を灰にして押せなくする（`SetSaved(true)`）
- 失敗：終わりの画面（Finished）に戻り、EndNotice に `ExportRules.Message` を出す（2行まで、灰ではなくクロム銀で読みやすく）。「書き出しは次の版でできます」の文は消し、「書き出す」は文字をクロム銀にして押せるようにする

- [ ] **Step 1: app-check に画面の絵を足す（まだ失敗する）** — `ShotMain` と同じ形で `main_9x19_exporting.png`（％ 73）と `main_9x19_exported.png`（保存済み）と `main_9x19_export_error.png`（EndNotice に NoSpace の文）を撮る。`MainScreen.Export.interactable == true` を確かめる

- [ ] **Step 2: `sh app-check.sh` で失敗を見る** — 期待：`SetExportProgress` が無いコンパイルエラー

- [ ] **Step 3: MainScreen に画面を足し、`ShowState` を2つの状態に合わせる** — Exporting は書き出しの群れだけ、Exported はできたの群れと「閉じる」だけを出す（歯車・言葉を変える・もう一回は隠す）

- [ ] **Step 4: PachiApp の流れを書く**
  - 「書き出す」：`state == Finished` のときだけ。`stage.Dispose()`（画面の舞台と書き出しの舞台が同じ場所に重ならないように）→ `exporter = new VideoExporter(check.Lines, settings, watermark: true /* 順番4で Entitlements */, 書き手, 出力先)`、書き手は iPhone なら `IosVideoWriter`（出力先 `Application.temporaryCachePath/noujiru.mp4`）、それ以外は `PngVideoWriter`（`Application.temporaryCachePath/noujiru_frames`）。`Start()` が None 以外なら Finished に戻して知らせる。None なら `SetState(Exporting)`
  - Update の Exporting：`exporter.Step()`、`ui.SetExportProgress(exporter.Percent)`。`Done` → `SetState(Exported)`（ExportView に Preview）。`Failed` → 片付けて Finished に戻し、知らせ
  - 「やめる」：`Cancel()` → Finished に戻す（知らせは出さない）
  - `OnApplicationPause(true)` で Exporting なら `Cancel()` して Finished に戻し、`Interrupted` の知らせ
  - 「写真に保存」：`VideoShare.SaveToPhotos(OutputPath, e => …)`、None なら `SetSaved(true)`、ほかは EndNotice に文。「共有」：`VideoShare.Share(OutputPath)`
  - 「閉じる」・Finished に戻るとき：`exporter.Dispose()`、`builtKey = null` で `Rebuild()`、舞台は `Evaluate(Duration)` で最後の姿にして Finished。mp4 は次の書き出しまで残す（共有のあと読み取られるため）
  - 書き出し中は `PlayClock` を進めない・設定を開けない

- [ ] **Step 5: `sh app-check.sh` で通るのを見て、3枚の絵を Read で見る** — 期待：`[PachiAppCheck] OK`。％の数字が主役で、帯・ボタン・知らせが重ならず、9:19.5 でバナーの場所にかからない。気になれば直してから次へ

- [ ] **Step 6: `sh run-tests.sh` が通るのを見る**

- [ ] **Step 7: コミット** — メッセージ「書き出しの画面（進み具合・できた・知らせ）と流れを作り、「書き出す」を押せるようにする」

---

### Task 5: 組み立てて iPhone で確かめる

- [ ] **Step 1: superpowers:verification-before-completion を読み、`sh run-tests.sh` と `sh app-check.sh` を最後にもう一度流す** — 期待：failed="0"・`[PachiAppCheck] OK`
- [ ] **Step 2: push してよいかユーザーに聞く**（アイコンの直し 3026c63 も一緒に届くと伝える）。OK なら `git push`、`"C:\Program Files\GitHub CLI\gh.exe" workflow run ios-testflight.yml`、`gh run watch` で文字で見る（Unity 約6分＋Mac 約20分）。Mac の段で mm のコンパイルが落ちたら `gh run view --log-failed` で読み、superpowers:systematic-debugging で原因を確かめて直す
- [ ] **Step 3: ユーザーに iPhone で確かめてもらう項目を伝える** — 書き出しが最後まで行く・％が進む・かかった秒数／写真に保存（初回に許可を聞く・写真アプリで再生できる・上下が正しい・右下に印・最後の虹まで入っている）／共有で LINE などに送れる／「やめる」／書き出し中にホームに戻ると中断の知らせ／もう一度書き出せる
- [ ] **Step 4: 進み具合のメモ（memory/pachieffect-project.md）に書く** — 番号・結果・気になった点、次は順番4
