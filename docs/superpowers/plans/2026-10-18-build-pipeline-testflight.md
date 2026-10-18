# 作る順番1「組み立ての道を先に通す」作業計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 起動すると今の演出（PachiStage）を固定の言葉「定時／退社」でくり返し再生するだけの iPhone アプリを、GitHub の組み立て機だけで作って TestFlight に届ける。

**Architecture:** Unity 側には「再生だけのシーン」と「iPhone 用の設定と組み立ての入口（PachiBuild）」を足す。組み立ては1つのワークフローに2つの仕事を並べる：①Linux 機で GameCI が Unity から Xcode の作りを作り、固めて渡す ②Mac 機で xcodebuild が仕上げて TestFlight に送る（鍵の扱いは天落杯と同じ）。手元の Windows では、iPhone 用は組み立てず、設定と絵の確認だけをする。

**Tech Stack:** Unity 6000.6.6f1（URP）、GameCI `game-ci/unity-builder@v4`（イメージ `unityci/editor:ubuntu-6000.6.6f1-ios-3` があることを 2026-10-18 に確認済み）、GitHub Actions（ubuntu-latest / macos-15-intel）、Xcode（latest-stable）、App Store Connect API キー。

**Spec:** `docs/superpowers/specs/2026-10-18-app-release-design.md`（「作る順番」の1、「3. 組み立てと配信」）

## Global Constraints

- お金をかけない：有料の部品・有料の組み立てサービスは使わない（GitHub の公開リポジトリの無料枠だけ）
- 手元の Windows に iOS 用の部品（iOS Build Support）は入れない
- Bundle ID：`com.maosundev.noujiru`
- ホーム画面の名前：`脳汁メーカー`／App Store の名前：`脳汁メーカー - パチンコ激アツ文字演出`
- リポジトリ：`maosun-dev/pachieffect`、**公開**、main に直接上げる（プルリクエストは作らない）
- 秘密（Secrets）の入力、Apple Developer・App Store Connect での登録は**ユーザーがやる**。鍵ファイル・ライセンスファイルの中身は読まない
- GitHub への公開（初めての push）は、押す前にユーザーに確認する
- 演出の中身（PachiStage の流れ）は変えない
- コードのコメント・コミットメッセージは日本語。コミットの最後に `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`
- Unity の裏起動（`unity run ... -executeMethod`）は、Unity エディタでこのプロジェクトを閉じてから行う（開いたままだと止まる）

## Review Focus

1. **演出用シェーダーがアプリから抜け落ちる**：`Shader.Find("PachiMoji/...")` は、シーンや素材から使われていないシェーダーをアプリに入れない。抜けると文字や背景が消える／ピンクになる。→ Task 2 で「いつも入れるシェーダー」に6つ全部を入れ、確認でも数える
2. **画面の縦横比が機種で違う**（iPhone SE は 9:16、ノッチのある機種は約 9:19.5）：どちらでも言葉が画面からはみ出さないこと。→ Task 1 で2つの比率の絵を書き出して見る。iPad は対象外にする（Task 2、iPad 用の画面写真を求められないように）
3. **名前が日本語**：Xcode の中でアプリの入れ物の名前（`○○.app`）が日本語になっても、ワークフローが探し当てられること。→ Task 3 で決め打ちせず `*.app` で探す
4. **組み立て番号の重複**：TestFlight は同じ番号を2度受け取らない。「失敗した仕事だけやり直す（Re-run）」でも番号が増えること。→ Task 3 で `実行番号×100＋やり直し回数` にする
5. **アプリを開きっぱなし・裏に回して戻る**：時間が進みすぎても再生が壊れず、くり返し続けること。→ Task 1 の `LoopTime` を確かめる

---

## ファイルの地図

| ファイル | 役目 |
|---|---|
| Create `PachiMoji/Assets/PachiMoji/Scripts/PachiPlayer.cs` | 起動したら固定の言葉で演出を組み立て、くり返し再生する |
| Create `PachiMoji/Assets/Scenes/Play.unity` | PachiPlayer だけが載ったシーン（PachiBuild.MakePlayScene で作る） |
| Create `PachiMoji/Assets/PachiMoji/Editor/PachiBuild.cs` | シーン作り・仮アイコン作り・iPhone 用の設定・組み立ての入口 |
| Create `PachiMoji/Assets/PachiMoji/Editor/PachiAppCheck.cs` | 手元での確認（くり返しの計算・設定の中身・2つの比率の絵） |
| Create `PachiMoji/Assets/PachiMoji/Icons/AppIcon.png` | 仮のアイコン（演出の金の瞬間を撮ったもの。本物は順番5で作る） |
| Create `app-check.sh` | PachiAppCheck を裏で動かす（capture.sh と同じ形） |
| Modify `PachiMoji/ProjectSettings/ProjectSettings.asset`、`GraphicsSettings.asset`、`EditorBuildSettings.asset` | PachiBuild.Configure が書き換えた結果をそのままコミットする（手で編集しない） |
| Create `.github/workflows/ios-testflight.yml` | Linux で Xcode の作り → Mac で TestFlight へ |
| Create `docs/ios-build.md` | ユーザーがやること（Secrets・Apple 側の登録・動かし方）の手順書 |

---

### Task 1: 再生だけのシーン

**Files:**
- Create: `PachiMoji/Assets/PachiMoji/Scripts/PachiPlayer.cs`
- Create: `PachiMoji/Assets/PachiMoji/Editor/PachiBuild.cs`（このタスクでは `MakePlayScene` だけ）
- Create: `PachiMoji/Assets/PachiMoji/Editor/PachiAppCheck.cs`
- Create: `app-check.sh`
- Create: `PachiMoji/Assets/Scenes/Play.unity`（コマンドで作る）

**Interfaces:**
- Consumes: `PachiStage.Build(string[] lines, float aspect, string font = "ReggaeOne")`、`PachiStage.Evaluate(float t)`、`PachiStage.End`、`PachiStage.RainbowAt`、`PachiStage.cam`
- Produces:
  - `namespace PachiMoji { public class PachiPlayer : MonoBehaviour }`
    - `public static readonly string[] Words = { "定時", "退社" };`
    - `public const float Gap = 1.5f;`（終わりの姿で止まっている秒数。そのあと頭から）
    - `public static float LoopTime(float elapsed, float end, float gap)` … `elapsed` を `end + gap` で割った余りを、`end` で頭打ちにして返す
    - `Start()`：`Application.targetFrameRate = 60`、`PachiStage.Build(Words, (float)Screen.width / Screen.height)`
    - `Update()`：`stage.Evaluate(LoopTime(Time.timeSinceLevelLoad, stage.End, Gap))`
  - `namespace PachiMoji.EditorTools { public static class PachiBuild }`
    - `public const string PlayScene = "Assets/Scenes/Play.unity";`
    - `public static void MakePlayScene()` … 空のシーンに `GameObject "PachiPlayer"` ＋ `PachiPlayer` を置いて `PlayScene` に保存（カメラ・ライトは置かない。舞台は PachiStage が作る）。裏起動なら最後に `EditorApplication.Exit(0/1)`
  - `namespace PachiMoji.EditorTools { public static class PachiAppCheck }`
    - `public static void Run()` … 確認を順に行い、失敗は `Debug.LogError("[PachiAppCheck] NG: …")`、全部通れば `Debug.Log("[PachiAppCheck] OK")`。裏起動なら失敗ありで終了コード1
    - `static void Expect(bool ok, string what)` … 失敗を数える小さな助け

- [ ] **Step 1: 確認を先に書く（`PachiAppCheck.Run` の中身）**

`PachiPlayer.LoopTime` の確認（Review Focus 5）：

```csharp
Expect(Mathf.Approximately(PachiPlayer.LoopTime(3f, 10f, 1.5f), 3f), "再生中はそのままの時刻");
Expect(Mathf.Approximately(PachiPlayer.LoopTime(10.8f, 10f, 1.5f), 10f), "終わってからの間は終わりの姿で止まる");
Expect(Mathf.Approximately(PachiPlayer.LoopTime(11.5f, 10f, 1.5f), 0f), "間が明けたら頭から");
Expect(Mathf.Approximately(PachiPlayer.LoopTime(14.5f, 10f, 1.5f), 3f), "2周目も同じ時刻の姿");
Expect(PachiPlayer.LoopTime(36000f + 3f, 10f, 1.5f) <= 10f, "10時間開きっぱなしでも終わりを超えない");
```

続けて、絵の確認（Review Focus 2）：`PlayScene` を開き、`PachiPlayer.Words` で `PachiStage.Build` を比率 9/16 と 9/19.5 で1回ずつ作り、`Evaluate(stage.RainbowAt + 0.3f)` → 720 幅の RenderTexture に `cam.Render()` → `captures/play_9x16.png`・`captures/play_9x19.png` に保存（PachiMovie の描き方にならう）。作るたびに `EditorSceneManager.NewScene(EmptyScene)` でまっさらにしてから。

- [ ] **Step 2: `app-check.sh` を作って動かし、失敗するのを見る**

`capture.sh` と同じ形で、`-executeMethod PachiMoji.EditorTools.PachiAppCheck.Run`、grep する印は `[PachiAppCheck]`。

Run: `sh app-check.sh`
Expected: `error CS0103`（PachiPlayer が無い）で止まる

- [ ] **Step 3: `PachiPlayer` と `PachiBuild.MakePlayScene` を書く**（Interfaces のとおり）

- [ ] **Step 4: シーンを作る**

Run: `unity run "C:/Users/USER/Desktop/game/pachieffect/PachiMoji" --editor-version 6000.6.6f1 --no-banner -- -executeMethod PachiMoji.EditorTools.PachiBuild.MakePlayScene -logFile captures/unity.log`
Expected: 終了コード 0、`PachiMoji/Assets/Scenes/Play.unity` と `.meta` ができる

- [ ] **Step 5: 確認を通す**

Run: `sh app-check.sh`
Expected: `[PachiAppCheck] OK`、`exit=0`。`captures/play_9x16.png`・`play_9x19.png` を Read で開き、どちらも虹の「定時／退社」が左右に切れず画面に収まっていることを目で見る

- [ ] **Step 6: コミット**

```bash
git add PachiMoji/Assets/PachiMoji/Scripts/PachiPlayer.cs* PachiMoji/Assets/PachiMoji/Editor/PachiBuild.cs* PachiMoji/Assets/PachiMoji/Editor/PachiAppCheck.cs* PachiMoji/Assets/Scenes/Play.unity* app-check.sh .gitignore
git commit -m "起動したら演出をくり返し再生するだけのシーンを作る"
```

（コミットの前に、リポジトリのいちばん上の `.gitignore` に `captures/play_*.png` を足す。確認の絵は毎回変わるのでコミットしない）

---

### Task 2: iPhone 用の設定と組み立ての入口

**Files:**
- Modify: `PachiMoji/Assets/PachiMoji/Editor/PachiBuild.cs`
- Modify: `PachiMoji/Assets/PachiMoji/Editor/PachiAppCheck.cs`
- Create: `PachiMoji/Assets/PachiMoji/Icons/AppIcon.png`（コマンドで作る）
- Modify（Configure の結果）: `PachiMoji/ProjectSettings/ProjectSettings.asset`、`GraphicsSettings.asset`、`EditorBuildSettings.asset`

**Interfaces:**
- Consumes: Task 1 の `PachiBuild.PlayScene`、`PachiPlayer`
- Produces（`PachiBuild` に足す）:
  - `public const string BundleId = "com.maosundev.noujiru";`
  - `public const string AppName = "脳汁メーカー";`
  - `public const string IconPath = "Assets/PachiMoji/Icons/AppIcon.png";`
  - `public static readonly string[] Shaders = { "PachiMoji/Burst", "PachiMoji/Flash", "PachiMoji/Glint", "PachiMoji/Metal", "PachiMoji/Rays", "PachiMoji/Spark" };`
  - `public static void MakeIcon()` … `PachiStage.Build(new[] { "脳汁" }, 1f)` を `Evaluate(stage.GoldAt + 0.5f)` で 2048 四方に描き、1024 四方に縮めて**透明なし（RGB24）**の PNG で `IconPath` に保存し、`AssetDatabase.ImportAsset`。仮のアイコン（本物は順番5）
  - `public static void Configure()` … 何度呼んでも同じ結果になること。決める値：
    - `PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId)`
    - `PlayerSettings.productName = AppName`、`PlayerSettings.companyName = "maosun-dev"`、`PlayerSettings.bundleVersion = "1.0.0"`
    - `PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait`、`PlayerSettings.statusBarHidden = true`
    - `PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly`、`PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK`、`PlayerSettings.iOS.appleEnableAutomaticSigning = false`（署名は Mac の段で付ける）。最低 iOS は今の `15.0` のまま
    - アイコン：`PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { IconPath のテクスチャ }, IconKind.Any)`
    - `GraphicsSettings` の「いつも入れるシェーダー」（`ProjectSettings/GraphicsSettings.asset` の `m_AlwaysIncludedShaders`、SerializedObject で触る）に `Shaders` のうち無いものを足す（Review Focus 1）
    - `EditorBuildSettings.scenes = { PlayScene }`（SampleScene は外す）
    - 最後に `AssetDatabase.SaveAssets()`
  - `public static void BuildIos()` … GameCI から呼ばれる入口。`Configure()` → コマンドの引数 `-customBuildPath` の次の値（無ければ `build/iOS/iOS`）へ `BuildPipeline.BuildPlayer(new[] { PlayScene }, path, BuildTarget.iOS, BuildOptions.None)`。結果が `Succeeded` 以外なら `Debug.LogError("[PachiBuild] 失敗: …")` して `EditorApplication.Exit(1)`、成功なら `Exit(0)`
  - `public static void ConfigureCli()` … 手元の裏起動用。`Configure()` して `Exit(0/1)`

- [ ] **Step 1: 確認を足す（`PachiAppCheck.Run` に設定の確認）**

```csharp
Expect(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS) == PachiBuild.BundleId, "Bundle ID");
Expect(PlayerSettings.productName == PachiBuild.AppName, "ホーム画面の名前");
Expect(PlayerSettings.iOS.targetDevice == iOSTargetDevice.iPhoneOnly, "iPhone だけ");
Expect(PlayerSettings.defaultInterfaceOrientation == UIOrientation.Portrait, "縦向き固定");
Expect(EditorBuildSettings.scenes.Length == 1 && EditorBuildSettings.scenes[0].path == PachiBuild.PlayScene, "入れるシーンは Play だけ");
foreach (var s in PachiBuild.Shaders) Expect(AlwaysIncluded(s), "いつも入れるシェーダー: " + s);
var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(PachiBuild.IconPath);
Expect(icon != null && icon.width == 1024 && icon.height == 1024, "アイコン 1024 四方");
```

`AlwaysIncluded(string name)` は `GraphicsSettings.asset` の `m_AlwaysIncludedShaders` に `Shader.Find(name)` が入っているかを返す小さな助け。

- [ ] **Step 2: 失敗するのを見る**

Run: `sh app-check.sh`
Expected: `PachiBuild.BundleId` などが無くてコンパイル エラー

- [ ] **Step 3: `PachiBuild` に Interfaces の中身を書く**

`BuildTarget.iOS` を使う行は、iOS の部品が無い Windows でもコンパイルは通る（列挙の値があるだけ）。`BuildIos` を手元で呼ばないこと。

- [ ] **Step 4: 仮アイコンを作り、設定を当てる**

Run: `unity run "C:/Users/USER/Desktop/game/pachieffect/PachiMoji" --editor-version 6000.6.6f1 --no-banner -- -executeMethod PachiMoji.EditorTools.PachiBuild.MakeIcon -logFile captures/unity.log`
Run: `unity run "C:/Users/USER/Desktop/game/pachieffect/PachiMoji" --editor-version 6000.6.6f1 --no-banner -- -executeMethod PachiMoji.EditorTools.PachiBuild.ConfigureCli -logFile captures/unity.log`
Expected: どちらも終了コード 0。`AppIcon.png` を Read で開いて、金の「脳汁」が真ん中に見え、角まで黒で埋まっている（透明でない）ことを見る

- [ ] **Step 5: 確認を通す**

Run: `sh app-check.sh`
Expected: `[PachiAppCheck] OK`、`exit=0`

- [ ] **Step 6: 書き換わった設定を見る**

Run: `git diff --stat PachiMoji/ProjectSettings`
Expected: `ProjectSettings.asset`・`GraphicsSettings.asset`・`EditorBuildSettings.asset` だけが変わっている。`git diff PachiMoji/ProjectSettings/ProjectSettings.asset | grep -E "noujiru|productName|statusBarHidden"` で値が入っていることを見る

- [ ] **Step 7: コミット**

```bash
git add PachiMoji/Assets/PachiMoji/Editor PachiMoji/Assets/PachiMoji/Icons PachiMoji/Assets/PachiMoji/Icons.meta PachiMoji/ProjectSettings
git commit -m "iPhone 用の設定（Bundle ID・名前・縦向き・シェーダー）と組み立ての入口を作る"
```

---

### Task 3: 組み立てのワークフローと、ユーザーがやることの手順書

**Files:**
- Create: `.github/workflows/ios-testflight.yml`
- Create: `docs/ios-build.md`

**Interfaces:**
- Consumes: Task 2 の `PachiMoji.EditorTools.PachiBuild.BuildIos`（GameCI の `buildMethod`）、出力先 `build/iOS/iOS`
- Produces: ワークフロー名 `iPhone版をTestFlightへ`、手動の入力 `testflight`（boolean）・`runner`（choice）。Secrets 名：
  `UNITY_LICENSE`、`UNITY_EMAIL`、`UNITY_PASSWORD`、`APPLE_TEAM_ID`、`APPSTORE_API_KEY_ID`、`APPSTORE_API_ISSUER_ID`、`APPSTORE_API_PRIVATE_KEY`

仕様書の「2段」は、1つのファイルに2つの仕事（`unity` → `testflight`）として書く。こうすると Xcode の作りの受け渡しと組み立て番号が1回の実行の中で閉じ、Linux 段だけを試す（`testflight` をオフ）こともできる。

- [ ] **Step 1: ワークフローを書く**

決めごと（ここに書いた以外は天落杯の `ios-testflight.yml` をそのまま写す）：

```yaml
name: iPhone版をTestFlightへ
on:
  workflow_dispatch:
    inputs:
      testflight:
        description: 'TestFlight まで送る（オフなら Unity で Xcode の作りを作るところまで）'
        type: boolean
        default: true
      runner:
        description: '仕上げに使う Mac（新しいチップの Mac は混んでいて始まらないことがある）'
        type: choice
        options: [macos-15-intel, macos-latest]
        default: macos-15-intel
permissions:
  contents: read

jobs:
  unity:
    runs-on: ubuntu-latest
    timeout-minutes: 90
    steps:
      - uses: actions/checkout@v4
      - uses: jlumbroso/free-disk-space@v1.3.1   # Unity のイメージが大きいので空きを作る
        with: { tool-cache: false, large-packages: false }
      - uses: actions/cache@v4
        with:
          path: PachiMoji/Library
          key: Library-iOS-${{ hashFiles('PachiMoji/Assets/**', 'PachiMoji/Packages/**', 'PachiMoji/ProjectSettings/**') }}
          restore-keys: Library-iOS-
      - uses: game-ci/unity-builder@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
        with:
          projectPath: PachiMoji
          targetPlatform: iOS
          buildMethod: PachiMoji.EditorTools.PachiBuild.BuildIos
          versioning: None
      # そのまま渡すと実行の許可（シェルの印）が消えて Xcode の手順が動かないので、固めて渡す
      - run: sudo tar -czf xcode-project.tgz -C build/iOS iOS
      - uses: actions/upload-artifact@v4
        with: { name: xcode-project, path: xcode-project.tgz, retention-days: 1 }

  testflight:
    needs: unity
    if: ${{ inputs.testflight }}
    runs-on: ${{ inputs.runner || 'macos-15-intel' }}
    timeout-minutes: 90
    env: （天落杯と同じ TEAM_ID / KEY_ID / ISSUER_ID）
```

`testflight` の手順：
1. `actions/download-artifact@v4`（`xcode-project`）→ `tar -xzf xcode-project.tgz`（`iOS/Unity-iPhone.xcodeproj` ができる）
2. `maxim-lobanov/setup-xcode@v1`（latest-stable）
3. 「App Store Connect の鍵を置く」：天落杯とまったく同じ
4. 「番号と申告を書き込む」：`BUILD=$(( ${{ github.run_number }} * 100 + ${{ github.run_attempt }} ))`（Review Focus 4）。`/usr/libexec/PlistBuddy` で `iOS/Info.plist` の `CFBundleVersion` を `$BUILD` に、`ITSAppUsesNonExemptEncryption` を `false`（bool）に（無ければ Add、あれば Set）。暗号の申告を毎回聞かれないようにするため
5. 「アプリを組み立てる（archive）」：`-project iOS/Unity-iPhone.xcodeproj -scheme Unity-iPhone`、ほかの引数は天落杯と同じ（`CODE_SIGNING_ALLOWED=NO`、`DEVELOPMENT_TEAM`、`CURRENT_PROJECT_VERSION=$BUILD`）。そのあと仮の署名：`APP=$(ls -d "$RUNNER_TEMP/App.xcarchive/Products/Applications/"*.app)`（名前を決め打ちしない。Review Focus 3）で、`$APP/Frameworks/*.framework`（UnityFramework）→ `$APP` の順に `codesign --force --sign - --timestamp=none`。天落杯と違い entitlements は付けない（この段では Game Center も課金も使わない）
6. 「TestFlight に送る」：天落杯とまったく同じ
7. 「鍵を消す」（`if: always()`）：天落杯とまったく同じ

ファイルの頭には天落杯と同じく、動かし方と必要な Secrets（7つ）を日本語のコメントで書く。

- [ ] **Step 2: 書き方の誤りがないか確かめる**

Run: `python -c "import yaml,sys; d=yaml.safe_load(open('.github/workflows/ios-testflight.yml',encoding='utf-8')); print(list(d['jobs']))"`
Expected: `['unity', 'testflight']`（`on:` は yaml では True という名前になるが気にしない）

- [ ] **Step 3: 手順書 `docs/ios-build.md` を書く**

中学生でも分かる言葉で、画面のどこを押すかまで書く。中身：
1. **Unity のライセンスを GitHub に渡す**：Unity Hub で個人ライセンスを有効にする（Preferences → Licenses → Add → Get a free personal license）→ `C:\ProgramData\Unity\Unity_lic.ulf` をメモ帳で開き、全文を Secrets の `UNITY_LICENSE` に貼る。`UNITY_EMAIL`・`UNITY_PASSWORD` も入れる。（Claude はこのファイルを開かない）
2. **Apple 側の4つ**：天落杯に入れたのと同じ値を、このリポジトリの Secrets にも入れる（`APPLE_TEAM_ID`・`APPSTORE_API_KEY_ID`・`APPSTORE_API_ISSUER_ID`・`APPSTORE_API_PRIVATE_KEY`）。.p8 ファイルが手元に無ければ、App Store Connect → ユーザとアクセス → 統合 → キー で作り直す
3. **Bundle ID を登録**：developer.apple.com → Certificates, Identifiers & Profiles → Identifiers → ＋ → App IDs → App → 説明「脳汁メーカー」、Explicit に `com.maosundev.noujiru`、機能は何も足さない
4. **App Store Connect にアプリを作る**：アプリ → ＋ → 新規アプリ → iOS、名前 `脳汁メーカー - パチンコ激アツ文字演出`、言語 日本語、Bundle ID は上で作ったもの、SKU `noujiru`。名前が使われていると言われたら、そこで止めて相談
5. **TestFlight で受け取る**：アプリの TestFlight → 内部テスト → グループを作り自分を入れる。iPhone に TestFlight アプリを入れておく
6. **動かし方**：GitHub の pachieffect → Actions → 「iPhone版をTestFlightへ」→ Run workflow。最初は `testflight` をオフで Unity の段だけ試す

- [ ] **Step 4: コミット**

```bash
git add .github/workflows/ios-testflight.yml docs/ios-build.md
git commit -m "Linux で Xcode の作りを作り、Mac で TestFlight に送るワークフローを足す"
```

---

### Task 4: GitHub に公開リポジトリを作り、Unity の段を通す

**Files:** なし（GitHub 側の作業）

**Interfaces:**
- Consumes: Task 3 のワークフロー・Secrets 名・手順書

- [ ] **Step 1: 公開してよいものか見る**

Run: `git log --format='%an <%ae>' | sort -u`
Expected: `pachieffect <334407377+maosun-dev@users.noreply.github.com>` だけ（個人のメールが無い。2026-10-18 時点で確認済み）
Run: `git ls-files | grep -iE "\.p8|\.ulf|\.env|secret|key"`
Expected: 何も出ない

- [ ] **Step 2: ユーザーに確認する**

「GitHub に maosun-dev/pachieffect を**公開**で作って、いまの中身（演出・書体のファイル・確認用の画像）を上げます。一度公開すると、そのあいだに写されたものは消せません。進めてよいですか？」と聞き、はっきり「はい」をもらうまで次へ進まない。

- [ ] **Step 3: 作って上げる**

Run: `"/c/Program Files/GitHub CLI/gh.exe" repo create maosun-dev/pachieffect --public --source . --remote origin --push`
Expected: リポジトリの URL が出る。`git status -sb` が `## main...origin/main`

- [ ] **Step 4: ユーザーに Secrets を入れてもらう**

`docs/ios-build.md` の1・2を案内する（入力はユーザー）。入ったかは名前だけ見る：
Run: `"/c/Program Files/GitHub CLI/gh.exe" secret list -R maosun-dev/pachieffect`
Expected: 7つの名前が並ぶ（中身は表示されない）

- [ ] **Step 5: Unity の段だけ動かす**

Run: `"/c/Program Files/GitHub CLI/gh.exe" workflow run ios-testflight.yml -R maosun-dev/pachieffect -f testflight=false`
そのあと `gh run list -R maosun-dev/pachieffect -L 1` で番号を見て、`gh run watch <番号> -R maosun-dev/pachieffect` を裏で動かす（30〜60分かかる。始めるときにユーザーに伝える）
Expected: `unity` が成功、`testflight` は飛ばされる。成果物 `xcode-project` ができている（`gh run view <番号> -R maosun-dev/pachieffect` で見る）

失敗したら `gh run view <番号> --log-failed` を読み、superpowers:systematic-debugging で原因を確かめる。ライセンスの有効化で止まるときは、仕様書の決まりどおり**ここで止めてユーザーに相談**（Unity アカウントの2段階認証が「認証アプリ」だと通らないことがある、など）。

---

### Task 5: TestFlight まで通して、iPhone で見てもらう

**Files:** なし（直しが要れば Task 2・3 のファイル）

- [ ] **Step 1: ユーザーに Apple 側の登録をしてもらう**

`docs/ios-build.md` の3〜5を案内する。登録が終わったと言われるまで待つ。

- [ ] **Step 2: 全部の段を動かす**

Run: `"/c/Program Files/GitHub CLI/gh.exe" workflow run ios-testflight.yml -R maosun-dev/pachieffect`
Task 4 と同じく裏で `gh run watch`（Mac の段は 30〜60 分。Intel の Mac が混んで始まらないときは `-f runner=macos-latest`）
Expected: `unity`・`testflight` とも成功。最後の段に `Upload succeeded` かそれに近い文言

- [ ] **Step 3: App Store Connect 側で処理が終わるのを待ってもらう**

送ってから TestFlight に出るまで数分〜30分ほど。「処理中」が終わって iPhone の TestFlight アプリに「脳汁メーカー」が出たら入れてもらう。メールで「Missing Compliance」などの指摘が来たら内容を教えてもらう。

- [ ] **Step 4: iPhone で見てもらう（確かめること）**

- ホーム画面の名前が「脳汁メーカー」、アイコンが金の「脳汁」
- 起動すると黒い画面からプチュン → … → 虹 → 1.5秒止まって頭から、をくり返す
- 文字・背景・光が消えたりピンクになったりしていない（Review Focus 1）
- 言葉が左右に切れていない（Review Focus 2）
- ホームに戻ってまた開いても動き続ける（Review Focus 5）
- カクつきが気になるか（気になれば順番2以降の課題として書き留める。描画の細かさ `m_RenderScale 0.8` もここで見る）

- [ ] **Step 5: 区切りの記録**

メモリの `pachieffect-project.md` に「順番1が終わった（TestFlight に届いた番号）」「次は順番2の作業計画」を書く。手順書に、実際に詰まったところと直し方を書き足してコミット：

```bash
git add docs/ios-build.md
git commit -m "組み立ての手順書に、実際に通したときの注意を書き足す"
git push
```
