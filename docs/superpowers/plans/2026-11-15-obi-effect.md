# 2つ目の演出「激アツの帯」 作り方の計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 言葉をくり返した豪華な金の帯が、傾いた輪のように画面を囲んで流れ続ける2つ目の演出を足し、演出を選ぶ画面と、写真を背景に入れて指で合わせる仕組みを作って、1.1 として出せる形にする。

**Architecture:** 帯の形・速さ・写真の合わせ方の計算は Core（テストできる純粋な計算）に置き、舞台の組み立ては王道昇格と同じ `IPachiEffect` の部品 `ObiEffect`（中身は `ObiStage`）で作る。時刻から姿を決める作り（状態を持たない）を守り、再生と書き出しで同じ絵にする。写真選びは自前の iOS 部品（PHPicker）で、選んだ写真を縮めて端末の中に写し、演出はそこから読む。

**Tech Stack:** Unity 6000.6.6f1・URP・uGUI＋TMP（画面はコードで組む）・NUnit の EditMode テスト・Objective-C の iOS 部品（PHPicker）・GitHub Actions で TestFlight

**Spec:** `docs/superpowers/specs/2026-11-15-obi-effect-design.md`（必ずいっしょに読む）

## Global Constraints

- 文章・コメント・コミットメッセージは日本語。コミットの最後に `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`
- 帯の形（決まり）：上の帯＝半径 6.2・倒し 32°・回し -32°・中心 (0, 1.25)、下の帯＝半径 6.2・倒し 24°・回し -32°・中心 (0, 0.2)。基準の画面（9:19.5）で z=0 の面の高さ 10、カメラの画角 32°、文字の高さ 0.95、帯の幅 1.35、速さの目安 1 秒に 1.6
- 上の帯は左へ、下の帯は右へ流れる。どちらの帯も文字は左から右に読める
- 長さ 10 秒。0 秒目と 10 秒目が同じ絵（くり返し再生でつなぎ目なし）。始まりの溶け込みは入れない
- 文字は今の金属文字（`PachiText`・`PachiMetal`）、色は金、書体は設定の「書体」
- 演出の ID は `obi`、名前「激アツの帯」、無料（`RequiredProduct = null`）
- 写真：長い辺 2048 画素ほどに縮めて `Application.persistentDataPath` に写す。端末の外へ送らない。位置と大きさは書き出しの枠（9:16）が基準
- 写真選びは PHPicker（許可を聞かない）。Info.plist に新しい許可の文は足さない
- 確認は `sh run-tests.sh`（EditMode）・`sh app-check.sh`（アプリの組み立ての確かめ）・撮影の道具の画像。動画は自分で見て、ユーザーには結果を文章で伝える（送らない）
- 見た目は「女神降臨の帯と並べて見劣りしない」が物差し。絵柄はまねない。見た目の作業の前に `frontend-design:frontend-design` スキルを読む
- 版は 1.1（`PachiBuild` の `PlayerSettings.bundleVersion`）。提出・公開はユーザーに確かめてから

## Review Focus

1. **とても大きい写真**（4800 万画素・HEIC・パノラマ）：iPhone の部品の中で縮めてから写すので、Unity が読むのは長い辺 2048 以下。落ちない・重くならない → Task 5 のテスト（Windows 側の読み込みも縮める）
2. **写真のファイルが消えている**（端末の掃除・入れ直し）：黒い背景に戻り、保存してある写真の設定を消す。落ちない → Task 3 の確かめ
3. **言葉の長さの両端**（1 文字・12 文字）：帯の文字が詰まりすぎ・空きすぎず、10 秒で元の並びに戻る → Task 1 のテスト
4. **帯が 10 秒で 1 周しない長さ**（長い言葉で輪が長いとき）：速さを上げてでも最低 1 周させ、つなぎ目を出さない → Task 1 のテスト
5. **演出・写真を変えたのに舞台が古いまま**（演出の切り替え、写真の入れ替え・合わせ直しのあと）：組み立て直しの合図（builtKey）に演出の ID と写真の設定を入れる → Task 4 の確かめ

---

## ファイルの地図

| ファイル | 役目 |
|---|---|
| `Core/ObiPlan.cs`（新） | 帯の形の数・輪の上の点・帯の長さから文字の並びと速さ（10 秒で元に戻る）を決める計算 |
| `Core/PhotoFit.cs`（新） | 写真を 9:16 の枠いっぱいに置く大きさと、ずらし・倍率の範囲の計算 |
| `Core/EffectSettings.cs`（直す） | 演出の ID・写真の置き場所・ずらし・倍率の保存 |
| `Core/AppRules.cs`（直す） | `AppState` に `EffectPick`・`PhotoAdjust` を足す |
| `Scripts/Obi/ObiStage.cs`（新） | 舞台の組み立て（カメラ・写真の板・帯・文字・光）と `Evaluate(t)` |
| `Scripts/Obi/ObiBand.cs`（新） | 帯の立体（飾り彫りの本体・縁）を作る |
| `Scripts/Obi/ObiEffect.cs`（新） | `IPachiEffect` の形で `ObiStage` を包む |
| `Scripts/EffectCatalog.cs`（直す） | `obi` を足す |
| `Scripts/UI/StageScreen.cs`・`Scripts/Export/VideoExporter.cs`（直す） | `"oudou"` の決め打ちをやめ、設定の演出を使う |
| `Scripts/Photo/PhotoPicker.cs`・`PhotoPickerBridge.cs`（新）・`Plugins/iOS/PachiPhoto.mm`（新） | 写真選び |
| `Scripts/Photo/PhotoStore.cs`（新） | 写した写真の読み込み（Texture2D）・消す |
| `Scripts/UI/EffectPickScreen.cs`（新） | 演出を選ぶ画面（ICover） |
| `Scripts/UI/PhotoAdjustView.cs`（新） | 写真を合わせる画面（9:16 の枠・ドラッグ・ピンチ・ボタン） |
| `Scripts/UI/MainScreen.cs`・`UiArt.cs`・`Scripts/PachiApp.cs`（直す） | 左上の「演出」ボタン、「写真」ボタン、流れのつなぎ |
| `Editor/ObiCapture.cs`（新）・`obi.sh`（新）・`Editor/PachiMovie.cs`（直す） | 帯の演出を撮る道具、演出を選ぶ画面の見本画像づくり |
| `Editor/ObiSpike.cs`・`obi-spike.sh` | 試し。Task 3 が終わったら消す |

---

### Task 1: 帯の形と流れの計算（ObiPlan）

**Files:**
- Create: `PachiMoji/Assets/PachiMoji/Core/ObiPlan.cs`
- Test: `PachiMoji/Assets/PachiMoji/Tests/Editor/ObiPlanTests.cs`

**Interfaces:**
- Produces:
  - `public readonly struct ObiRing { float R, Tilt, Roll; Vector2 Center; Vector3 Point(float th); }` … `Point` は試しの `ObiSpike.RingPoint` と同じ式（半径 R の円を tilt 度倒し、上半分が奥 z>0、z 軸で roll 度回し、Center を足す）
  - `public static class ObiPlan`：
    - 定数 `Duration = 10f`・`ViewH = 10f`・`Fov = 32f`・`BaseAspect = 9f / 19.5f`・`LetterH = 0.95f`・`BandW = 1.35f`・`BaseSpeed = 1.6f`・`Spacing = LetterH * 1.08f`
    - `static readonly ObiRing Top`（6.2, 32, -32, (0,1.25)）・`Bottom`（6.2, 24, -32, (0,0.2)）
    - `static ObiRing TopFor(float aspect)`・`BottomFor(float aspect)` … 横幅を基準の画面と同じに保ち、縦が短い画面（9:16 など）では、見える高さ `H = ViewH * BaseAspect / aspect` が減った半分だけ上の帯の中心を下げ、下の帯の中心を上げる（`dy = (ViewH - H) / 2`。縦が長い画面では逆）
    - `static Vector3[] ArcPoints(ObiRing ring, bool top, int n)` … 上は θ=π→0、下は θ=π→2π（どちらも画面の左→右）
    - `static int LetterSlots(float arcLength, int wordChars)` … `ceil(arcLength / Spacing) + 1` を `wordChars + 1`（言葉＋区切り1つ）の倍数に切り上げ
    - `static float LoopSpeed(float loopLength)` … `loopLength * max(1, round(BaseSpeed * Duration / loopLength)) / Duration`
    - `static float SlotPos(float s0, int dir, float speed, float t, float loopLength, float arcLength)` … `Repeat(s0 + dir * speed * t, loopLength) - (loopLength - arcLength) / 2`（帯の上の位置。0〜arcLength の外は見えない）

- [ ] **Step 1: 失敗するテストを書く**（`ObiPlanTests`）
  - `上下の帯は決まりの形`：`ObiPlan.Top.R==6.2f`・`Tilt==32`・`Roll==-32`・`Center==(0,1.25)`、`Bottom` は 24・(0,0.2)
  - `輪の点は試しの帯と同じ`：`Top.Point(Mathf.PI/2)` が `Quaternion.Euler(0,0,-32) * (0, 6.2*sin32°, 6.2*cos32°) + (0,1.25,0)` と 1e-4 で一致
  - `10秒で元の並びに戻る`：言葉 1・4・12 文字それぞれで、`loop = LetterSlots(19.5f, n) * Spacing`、`v = LoopSpeed(loop)` のとき `SlotPos(s0, ±1, v, 0, …) == SlotPos(s0, ±1, v, Duration, …)`（1e-3、s0 は 0・1.3・7.7）
  - `速さは目安に近い`：`loop=19.5*…` で `|LoopSpeed - 1.6| <= 0.6`
  - `輪が長くても最低1周`：`LoopSpeed(40f) == 40f / 10f`
  - `文字の数は言葉と区切りの倍数`：`LetterSlots(19.5f, 4) % 5 == 0`、`LetterSlots(19.5f, 12) % 13 == 0`、`LetterSlots(19.5f, 1) % 2 == 0`
  - `9:16では上の帯が下がり下の帯が上がる`：`TopFor(9/16f).Center.y < Top.Center.y`、`BottomFor(9/16f).Center.y > Bottom.Center.y`、`TopFor(BaseAspect).Center == Top.Center`
- [ ] **Step 2: `sh run-tests.sh` で失敗を確かめる**（ObiPlan が無いのでコンパイルエラー）
- [ ] **Step 3: `ObiPlan.cs` を書く**（上の Interfaces どおり）
- [ ] **Step 4: `sh run-tests.sh` で全部通るのを確かめる**（Expected: failed="0"）
- [ ] **Step 5: コミット**「激アツの帯の形と流れの計算を足す」

### Task 2: 写真の合わせ方と、演出・写真の保存（PhotoFit・EffectSettings・AppState）

**Files:**
- Create: `Core/PhotoFit.cs`、Test: `Tests/Editor/PhotoFitTests.cs`
- Modify: `Core/EffectSettings.cs`、`Core/AppRules.cs`、`Tests/Editor/EffectSettingsTests.cs`

**Interfaces:**
- Produces:
  - `EffectSettings` に `public string EffectId = "oudou";`（保存 `fx.effect`。知らない ID は `"oudou"` に戻すのは `EffectCatalog.Get` の役目なので、ここは文字列のまま）、`public string PhotoPath = "";`（`fx.photo`）、`public Vector2 PhotoOffset;`（`fx.photoX`・`fx.photoY`）、`public float PhotoZoom = 1;`（`fx.photoZoom`、1〜4 に収める）
  - `public static class PhotoFit`：
    - `const float FrameAspect = 9f / 16f`、`const float MaxZoom = 4`
    - `static Vector2 Size(float imgAspect, float zoom)` … 枠の幅を 1 としたときの写真の幅・高さ。zoom=1 で 9:16 の枠をちょうど覆う（はみ出す向きだけ大きい）
    - `static Vector2 ClampOffset(Vector2 offset, float imgAspect, float zoom)` … 写真が枠を覆い続ける範囲に収める（枠の幅を 1 とする単位）
    - `static float ClampZoom(float z)` … 1〜MaxZoom
  - `AppState` に `EffectPick`、`PhotoAdjust` を足す（末尾に足して、今の数を変えない）

- [ ] **Step 1: 失敗するテストを書く**
  - `PhotoFitTests`：`横長の写真は高さで合わせる`（imgAspect 4/3 → Size.y == 16/9、Size.x == 16/9*4/3）、`縦長の写真は幅で合わせる`（imgAspect 3/4 → Size.x == 1、Size.y == 4/3）、`ずらしは枠を覆う範囲に収まる`（4/3・zoom1 で offset (5,5) → x は (Size.x-1)/2、y は (Size.y-16/9)/2）、`倍率は1から4`（0.3→1、9→4）
  - `EffectSettingsTests`：`演出と写真の既定`（"oudou"・""・(0,0)・1）、`演出と写真を保存して読み戻す`（"obi"・"a/b.jpg"・(0.1,-0.2)・2.5）、`倍率が範囲外なら収める`（PlayerPrefs に 10 を入れて Load → 4）。TearDown の消すキーに `effect`・`photo`・`photoX`・`photoY`・`photoZoom` を足す
- [ ] **Step 2: `sh run-tests.sh` で失敗を確かめる**
- [ ] **Step 3: `PhotoFit.cs` を書き、`EffectSettings` の Load・Save・Clone に足し、`AppState` に2つ足す**（`SleepPolicy` はそのままで、新しい2つは眠ってよい）
- [ ] **Step 4: `sh run-tests.sh` で全部通るのを確かめる**
- [ ] **Step 5: コミット**「演出の選択と写真の合わせ方を保存できるようにする」

### Task 3: 帯の舞台（ObiStage・ObiEffect）を、まず試しの見た目で本番の形にする

**Files:**
- Create: `Scripts/Obi/ObiStage.cs`、`Scripts/Obi/ObiEffect.cs`、`Scripts/Obi/ObiBand.cs`、`Editor/ObiCapture.cs`、`obi.sh`（リポジトリの一番上、`capture.sh` と同じ形）
- Modify: `Scripts/EffectCatalog.cs`、`Editor/PachiMovie.cs`、`movie.sh`（5つめの引数を `PACHI_EFFECT` に渡し、出力を `captures/movie_<演出>.mp4` に）、`Editor/PachiAppCheck.cs`
- Delete: `Editor/ObiSpike.cs`（.meta も）、`obi-spike.sh`

**Interfaces:**
- Consumes: Task 1 の `ObiPlan`・`ObiRing`、Task 2 の `EffectSettings.PhotoPath/PhotoOffset/PhotoZoom`・`PhotoFit`
- Produces:
  - `public class ObiEffect : IPachiEffect`（`Duration => ObiPlan.Duration`、`Idle(t)` は `Evaluate(Mathf.Repeat(t, Duration))`）
  - `public class ObiStage { public static ObiStage Build(string[] lines, float aspect, EffectSettings s, Texture2D photo); public void Evaluate(float t); public Camera cam; public void Dispose(); }`
  - `public static class ObiBand { public static GameObject Build(Vector3[] arc, Vector3 camPos, float bandW, …); }`
  - `EffectCatalog.All` に `new EffectEntry { Id = "obi", Name = "激アツの帯", RequiredProduct = null, Create = () => new ObiEffect() }`
  - `sh obi.sh` … `captures/obi_9x19.png`・`obi_9x16.png`・`obi_photo_9x16.png`（写真あり）・`obi_t*.png`（0・2.5・5・7.5・10 秒）を撮る。`PACHI_PHOTO` に画像のパスを入れるとそれを写真に使う（無ければ `captures/` にある確認用の画像を作って使う：灰色の地に顔の位置の丸）
  - `PachiMovie` は `PACHI_EFFECT`（既定 `oudou`）で演出を選べる。`sh movie.sh` の5つめの引数で渡す

- [ ] **Step 1: `ObiStage` を試しの `ObiSpike` から移して書く**
  - カメラ：画角は `ObiPlan.Fov` を、基準の画面の横幅が保たれるように縦の画角へ直して使う（`aspect` が基準より縦に短ければ縦の画角を狭める）。背景は黒
  - 帯：`ObiPlan.TopFor(aspect)`・`BottomFor(aspect)` から点の並び → 文字の立体を1文字ずつ（文字の型は1文字1つ作って複製）、`LetterSlots`・`LoopSpeed`・`SlotPos` で置く。言葉＝`string.Concat(lines)` から空白を抜いたもの。区切りの枠は空けておく（飾りは Task 7）
  - 帯の本体は `ObiBand.Build`（この段階では試しと同じ黒い地＋金の縁でよい）
  - 写真：`photo` があれば、帯の奥（z=+14 あたり）に、9:16 の枠を覆う板を置く。大きさは `PhotoFit.Size`、ずらしは `PhotoFit.ClampOffset`、その奥行きで枠の幅が画面の横幅になるように拡大する。材質は光の影響を受けない（Unlit のテクスチャ）
  - 光のにじみ（Bloom）は王道昇格と同じ値
  - `Evaluate(t)`：文字の位置と向きだけを決める（状態を持たない）
- [ ] **Step 2: `ObiEffect` を書き、`EffectCatalog` に1行足す**。`ObiEffect.Build` は `settings.PhotoPath` が空でなければ `PhotoStore.Load`（Task 5）で読む。Task 5 がまだのあいだは `photo = null` で組む（`PhotoStore` を呼ぶのは Task 5 の Step 4 で足す）
- [ ] **Step 3: `ObiCapture.cs`・`obi.sh` を書き、`PachiMovie` に `PACHI_EFFECT` を足す**
- [ ] **Step 4: `sh obi.sh` を流し、撮れた画像を自分で見る**
  - Expected：試しの `captures/obi_spike_final.png` と同じ位置に帯がある（9:19.5）。9:16 でも上の帯が上のふちから流れ込み、下の帯が左下を通る。`obi_t0` と `obi_t10` が同じ絵
- [ ] **Step 5: `sh movie.sh 0 -1 quick "" obi` で動画とコマの一覧を作り、自分で見る**（くり返しのつなぎ目・流れる向き）
- [ ] **Step 6: `PachiAppCheck` に確かめを足して `sh app-check.sh`**：`EffectCatalog.Get("obi").Id == "obi"`、`RequiredProduct == null`、`ObiEffect` を組んで `Evaluate(0)`・`Evaluate(10)` が例外なく動く、写真のパスが存在しない設定でも組める（黒になる）。Expected: exit=0
- [ ] **Step 7: 試しの `ObiSpike.cs`・`obi-spike.sh` を消す**
- [ ] **Step 8: コミット**「激アツの帯の舞台を作る（見た目は仮）」

### Task 4: 選んだ演出で再生・書き出しする（StageScreen・VideoExporter・PachiApp）

**Files:**
- Modify: `Scripts/UI/StageScreen.cs:36`、`Scripts/Export/VideoExporter.cs:57`、`Scripts/PachiApp.cs`（`Rebuild` の builtKey）、`Editor/PachiAppCheck.cs`

**Interfaces:**
- Consumes: `EffectSettings.EffectId`、`EffectCatalog.Get(id)`
- Produces: `PachiApp` の組み立て直しの合図は、今の中身に `settings.EffectId`・`settings.PhotoPath`・`PhotoOffset`・`PhotoZoom` を足したもの

- [ ] **Step 1: `StageScreen.Build` と `VideoExporter.Start` を `EffectCatalog.Get(settings.EffectId).Create()` にする**
- [ ] **Step 2: `PachiApp.Rebuild` の builtKey に演出の ID と写真の設定を足す**
- [ ] **Step 3: `PachiAppCheck` に確かめを足す**：設定の EffectId を "obi" にして `VideoExporter` を `PngVideoWriter` で数コマ書かせると、帯の演出で書ける（例外なし・コマ数 300）。知らない ID（"xxx"）は王道昇格で書ける
- [ ] **Step 4: `sh app-check.sh`**（Expected: exit=0）**と `sh run-tests.sh`**（failed="0"）
- [ ] **Step 5: コミット**「選んだ演出で再生・書き出しするようにする」

### Task 5: 写真選び（iPhone の部品と読み込み）

**Files:**
- Create: `PachiMoji/Assets/Plugins/iOS/PachiPhoto.mm`、`Scripts/Photo/PhotoPicker.cs`、`Scripts/Photo/PhotoPickerBridge.cs`、`Scripts/Photo/PhotoStore.cs`
- Modify: `Scripts/Obi/ObiEffect.cs`（写真を読む）、`Editor/PachiAppCheck.cs`

**Interfaces:**
- Produces:
  - `PhotoPicker.Pick(Action<PhotoPickResult> done)`。`enum PhotoPickResult { Picked, Cancelled, Failed }`。選べたら `PhotoStore.PathFor()` に JPEG（品質 0.9、長い辺 2048 以下、向きは正しく直したもの）ができている
  - `PhotoStore`：`static string PathFor()`（`persistentDataPath/photo_<日時>.jpg`。前の写真は新しい写真ができてから消す）、`static Texture2D Load(string path)`（無い・壊れていれば null。長い辺が 2048 を超えていれば縮める）、`static void Delete(string path)`
  - iOS の部品：`void PachiPhoto_Pick(const char* outPath)`。PHPickerViewController（画像だけ・1 枚）→ `loadObjectOfClass:UIImage` → 向きを直して縮めて JPEG で `outPath` へ → `UnitySendMessage("PachiPhotoBridge", "OnPicked", "ok"|"cancel"|"failed")`。`PachiVideo.mm` と同じく `UnityGetGLViewController` から出す
  - iPhone 以外：環境変数 `PACHI_PHOTO` のパスの画像を `PathFor()` に写して `Picked`（無ければ `Cancelled`）
- [ ] **Step 1: `PhotoStore` と `PhotoPicker`（iPhone 以外の道）を書く**
- [ ] **Step 2: `PachiAppCheck` に確かめを足す**：5000×3000 の画像を作って `PACHI_PHOTO` 経由で選ぶ → `Load` の長い辺が 2048 以下、壊れたファイル（中身 3 バイト）の `Load` は null、無いパスの `Load` は null
- [ ] **Step 3: `PachiPhoto.mm` を書く**（手元ではコンパイルできない。初めてのコンパイルは TestFlight の組み立て）
- [ ] **Step 4: `ObiEffect.Build` で `PhotoStore.Load(settings.PhotoPath)` を使う**。読めなければ黒で組み、`settings.PhotoPath` を空にする合図を返す（`ObiEffect.PhotoMissing` が true）。`PachiApp` は組み立てのあとそれを見て設定を空にして保存する
- [ ] **Step 5: `sh app-check.sh`・`sh run-tests.sh`**
- [ ] **Step 6: コミット**「写真を選んで端末の中に写す仕組みを作る」

### Task 6: 画面（演出を選ぶ・写真を入れる・写真を合わせる）

見た目の作業。始める前に `frontend-design:frontend-design` スキルを読む。絵は `UiArt` の金属の枠・メダルにそろえて自作する（既存のアイコンや絵文字は使わない）。

**Files:**
- Create: `Scripts/UI/EffectPickScreen.cs`、`Scripts/UI/PhotoAdjustView.cs`、`Resources/UI/EffectPreview/oudou.png`・`obi.png`（`ObiCapture` と `PachiCapture` で撮って縮めたもの）
- Modify: `Scripts/UI/MainScreen.cs`、`Scripts/UI/UiArt.cs`、`Scripts/PachiApp.cs`、`Editor/PachiAppCheck.cs`（画面の撮影に足す）

**Interfaces:**
- Consumes: `ICover`、`AppState.EffectPick`・`PhotoAdjust`、`PhotoPicker`、`PhotoFit`、`EffectSettings`
- Produces:
  - `MainScreen.EffectButton`（左上。待ちのときだけ見える。終わりの画面の「言葉を変える」と同じ場所なので、待ち以外では隠す）、`MainScreen.PhotoButton`（激アツの帯のときだけ、待ちの画面の舞台の左下）
  - `EffectPickScreen : ICover`：`Build(root, safe, screenPx)`、`Back`、`event Action<string> Picked`、`SetCurrent(string id)`。演出の板＝見本の画像＋名前、今の演出に印
  - `PhotoAdjustView`：画面いっぱいの舞台の上に 9:16 の枠（枠の外を暗く）、ドラッグで `PhotoOffset`、ピンチで `PhotoZoom`（どちらも `PhotoFit` で収める）、下に「写真を変える」「写真を外す」「できた」
- 流れ（`PachiApp`）：
  - 「演出」→ `OpenCover(effectPick, AppState.EffectPick)` → 選ぶと `settings.EffectId` を保存して閉じ、`Rebuild()`
  - 「写真」：写真が無ければ `PhotoPicker.Pick` → `Picked` なら設定の写真を新しいパスに、ずらし (0,0)・倍率 1 にして保存 → `PhotoAdjust` へ。写真があれば `PhotoAdjust` へ
  - `PhotoAdjust`：`stage.SetFull(true)` で舞台を広げ、指の動きのたびに設定を変えて舞台を組み直す（重ければ写真の板だけ動かす `ObiEffect.SetPhotoFit(offset, zoom)` を足す）。「できた」で保存して待ちへ。「写真を外す」で写真を消して待ちへ。「写真を変える」で選び直し
- [ ] **Step 1: 左上の「演出」ボタンの絵、「写真」ボタン、演出の板の絵を `UiArt` に足し、`MainScreen` に置く**
- [ ] **Step 2: `EffectPickScreen` を書く。見本の画像を撮って `Resources/UI/EffectPreview/` に置く**
- [ ] **Step 3: `PhotoAdjustView` を書く**
- [ ] **Step 4: `PachiApp` につなぐ**
- [ ] **Step 5: `PachiAppCheck` の画面の撮影に、演出を選ぶ画面・帯の待ちの画面（写真なし／あり）・写真を合わせる画面を足して `sh app-check.sh`。撮れた画像を自分で見る**
- [ ] **Step 6: 画像をユーザーに見せ、ボタンの絵と置き場所を直す（OK が出るまで）**
- [ ] **Step 7: コミット**「演出を選ぶ画面と、写真を入れて合わせる画面を作る」

### Task 7: 帯を豪華にする（見た目の作り込み）

撮ってはユーザーに見せて直すくり返し。始める前に `frontend-design:frontend-design` スキルを読む。参考の `帯.mp4`（デスクトップ）と並べて見比べる。1 回撮るのに 2〜3 分。

**Files:**
- Modify: `Scripts/Obi/ObiBand.cs`、`Scripts/Obi/ObiStage.cs`、必要なら `Shaders/` に帯の材質（光の筋・模様）を足す

**順番（仕様書の表の順。1つ足すたびに撮る）：**
- [ ] **Step 1: 帯の本体を金の飾り彫りの立体にする**：縁から飾りがはみ出すでこぼこの形（くり返しの飾りの輪郭を帯に沿わせて押し出す）、表面に模様、文字のまわりはやや沈める。材質は `PachiMetal`（goldRim・goldSide）
- [ ] **Step 2: 文字を帯の幅いっぱいの大きさにし、言葉の区切りに飾りの印を置く**（`LetterH` を変えるなら `ObiPlan` のテストの値も直す）
- [ ] **Step 3: 帯の上を光の筋が走り抜ける・文字が順番にキラッと光る**（時刻から決める。10 秒で元に戻る周期にする）
- [ ] **Step 4: 帯から金の粒・火の粉がこぼれる、帯のまわりに赤い光のにじみ**（粒も時刻から決める。10 秒でつながる）
- [ ] **Step 5: 写真が無いときの黒に、うっすら動く暗い模様**（10 秒でつながる）
- [ ] **Step 6: それぞれの段で `sh obi.sh` → 自分で見る → ユーザーに見せる。ユーザーの OK が出たら `sh movie.sh 0 -1 quick "" obi` で動きも確かめる**（つなぎ目・重さ）
- [ ] **Step 7: 段ごとにコミット**（例「帯の本体を金の飾り彫りにする」）

### Task 8: 1.1 として組み立てて、iPhone で確かめる

**Files:**
- Modify: `Editor/PachiBuild.cs:181`（`"1.1"`）、`Editor/PachiAppCheck.cs`（版の確かめがあれば）

- [ ] **Step 1: 版を 1.1 にして `sh app-check.sh`・`sh run-tests.sh`**
- [ ] **Step 2: コミットして push し、TestFlight の組み立て（テスト広告）を流す**。様子は `gh run watch` で文字で見る
- [ ] **Step 3: ユーザーに iPhone で確かめてもらう**：写真を選ぶ（PHPicker が開く・許可を聞かれない）・合わせる・外す・演出を切り替える・帯の再生・書き出し（写真に保存・共有）・王道昇格がこれまでどおり動く・アプリを開き直しても演出と写真が残っている
- [ ] **Step 4: 直すところがあれば直して組み立て直す**
- [ ] **Step 5: プライバシーポリシー（maosun-dev.com/noujiru/privacy.html）に「写真は端末の中だけで使い、外へ送らない」を足すか、ユーザーと決める。ストアの説明文・画面写真に新しい演出を足すかも決める**
- [ ] **Step 6: ユーザーの OK のあと、本物の広告の版（production）を組み立て、ASC で 1.1 を作って審査に出す**（提出の前にユーザーに確かめる）
- [ ] **Step 7: メモ（pachieffect-project）に進み具合と次にやることを書く**
