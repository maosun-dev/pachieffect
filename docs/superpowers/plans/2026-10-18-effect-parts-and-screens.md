# 作る順番2「演出の部品化と設定、メイン画面、設定画面」作業計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 固定の言葉をくり返すだけの今のアプリ（PachiPlayer）を、「言葉を入れて赤い PUSH で演出が流れ、設定（火花・プチュンの光・書体・昇格の範囲）が効いて端末に残る」アプリに置き換え、TestFlight で iPhone に届ける。

**Architecture:** 状態を持たない計算（昇格の範囲から時刻の並び・入力の決まり・設定の保存・再生の時計）は、小さな別の部品（asmdef「PachiMoji.Core」）に分け、EditMode の自動テストで確かめる。演出は `IPachiEffect` の形にそろえ、今の `PachiStage` を「王道昇格」（`OudouEffect`）として包み、`EffectCatalog` に1行で載せる。画面は uGUI＋TextMeshPro を、演出と同じく**コードで組み立てる**（シーンには `PachiApp` を1つ置くだけ）。ボタンや枠の絵は、既製のアイコンを使わずコードで描く。

**Tech Stack:** Unity 6000.6.6f1（URP）、uGUI 2.6.0（TextMeshPro 入り）、Unity Test Framework（EditMode、`com.unity.test-framework`）、PlayerPrefs、GitHub Actions（順番1の `ios-testflight.yml` をそのまま使う）。

**Spec:** `docs/superpowers/specs/2026-10-18-app-release-design.md`（「作る順番」の2、「1. 画面と流れ」、「2. 中の作り」の演出の部品・昇格の範囲・再生）

## Global Constraints

- お金をかけない：有料の部品・有料のサービスは使わない
- 色（仕様書どおり）：背景 `#000000`／クロム銀 `#C8CDD4`（枠・入力欄・文字ボタン）／チャンスボタンの赤 `#E3001B`（PUSH だけ）／演出の金 `#F2C14E`（オンの印だけ）／説明文の灰 `#8A8F98`
- 書体：見出し・ボタンは **Reggae One**、説明文は **BIZ UDPゴシック**（どちらも OFL）。演出の文字の書体は Reggae One／明朝（Zen Old Mincho Black）
- 入力の決まり：**2行まで・1行6文字まで**。空なら PUSH を押せない。書体にない文字は入力の時点で知らせる
- 昇格の範囲：はじまり ≦ おわり の6通り。既定は**銀→虹**（今の流れと時刻がまったく同じになること）。長さの目安：銀だけ約9秒、銀→虹約15秒
- 設定は PlayerPrefs に保存し、再生に効く（順番3の書き出しにも同じ設定を渡せる形にする）
- 演出の中身（銀→金→虹の流れ・見た目）は、既定の範囲では変えない
- 第1弾は無音。書き出し・広告・課金・購入の復元は**この計画に入れない**（順番3・4）。ただし下のバナーの場所は空けておく
- 手元の Windows に iOS 用の部品は入れない。Unity の裏起動は、エディタでこのプロジェクトを閉じてから行う（同時に2つ動かさない）
- コードのコメント・コミットメッセージは日本語。コミットの最後に `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`。main に直接コミットし、プルリクエストは作らない
- ファイルを落とす（書体）・GitHub に上げる（push）は、する前にユーザーに確認する

## Review Focus

1. **アプリを裏に回して戻る・着信で中断する**：再生の途中で裏に回っても、戻ったら続きから流れ、いきなり終わりに飛ばない。→ Task 3 の `PlayClock.Step` のテスト（1回に進む時間は最大 0.1 秒）
2. **保存した設定が壊れている・古い**（はじまり＞おわり、範囲外の数字）：起動して落ちず、直せる値は直し、直せない値は既定に戻る。→ Task 3 の `EffectSettings` のテスト
3. **入力のふち**：前後の空白、全角の空白だけ、1行目が空で2行目だけ、6文字ちょうど・7文字、絵文字（2つ分の文字コード）、書体を明朝に替えたら出せなくなる字。→ Task 2 の `InputRules` のテスト
4. **言葉を何度も打ち直す**：組み立て直すたびに古い舞台（立体・材質・カメラ）が残ると、だんだん重くなって落ちる。→ Task 5 の手元確認で「10回組み立てて片付けたら、数が元に戻る」を数える
5. **12文字（6文字×2行）・ノッチのある機種**：言葉やボタンが画面やノッチ・ホームバーにかからない。→ Task 5・7 の手元確認で、12文字の絵と、9:16／9:19.5 の画面の絵を書き出して見る

---

## ファイルの地図

| ファイル | 役目 |
|---|---|
| Create `PachiMoji/Assets/PachiMoji/Core/PachiMoji.Core.asmdef` | テストしたい計算だけをまとめた部品（UnityEngine だけに頼る） |
| Create `PachiMoji/Assets/PachiMoji/Core/PromoTimeline.cs` | 昇格の範囲（`Tier`）から時刻の並び（`PromoTimes`）を作る |
| Create `PachiMoji/Assets/PachiMoji/Core/InputRules.cs` | 入力の決まり（2行・6文字・書体にない文字）と、画面に出す文 |
| Create `PachiMoji/Assets/PachiMoji/Core/EffectSettings.cs` | 設定の中身と PlayerPrefs への保存・読み込み |
| Create `PachiMoji/Assets/PachiMoji/Core/AppRules.cs` | 再生の時計（`PlayClock`）・画面の消灯（`SleepPolicy`）・メダルのなぞり（`RangePicker`） |
| Create `PachiMoji/Assets/PachiMoji/Core/IPachiEffect.cs` | すべての演出が持つ形 |
| Move `Scripts/TtfFont.cs` → `Core/TtfFont.cs` | 書体にその字があるかをテストから確かめられるように（`git mv`、.meta も一緒に） |
| Create `PachiMoji/Assets/PachiMoji/Tests/Editor/PachiMoji.Tests.asmdef` ほか `*Tests.cs` | EditMode の自動テスト（エディタだけ。アプリには入らない） |
| Create `run-tests.sh` | 自動テストを裏で動かし、結果を短く出す |
| Modify `PachiMoji/Assets/PachiMoji/Scripts/PachiStage.cs` | 設定を受け取る・範囲で時刻を決める・待ちの姿（`Idle`）・片付け（`Dispose`） |
| Modify `PachiMoji/Assets/PachiMoji/Shaders/PachiRays.shader` | プチュンの光の色（`_PTint`・`_PRainbow`） |
| Create `PachiMoji/Assets/PachiMoji/Scripts/OudouEffect.cs`・`EffectCatalog.cs` | 王道昇格の包みと、演出の一覧表 |
| Create `PachiMoji/Assets/PachiMoji/Scripts/UI/UiTheme.cs`・`UiArt.cs` | 色・大きさの決まりと、コードで描くボタン・枠・歯車・メダルの絵 |
| Create `PachiMoji/Assets/PachiMoji/Scripts/UI/MainScreen.cs`・`SettingsScreen.cs` | 2つの画面の組み立て |
| Create `PachiMoji/Assets/PachiMoji/Scripts/PachiApp.cs` | 画面の流れ（待ち→再生→終わり、設定）。PachiPlayer の後継 |
| Delete `PachiMoji/Assets/PachiMoji/Scripts/PachiPlayer.cs` | 置き換えたので消す |
| Create `PachiMoji/Assets/PachiMoji/Editor/PachiUiAssets.cs` | TMP の基本素材の取り込みと、画面用の書体（TMP フォント）作り |
| Create `PachiMoji/Assets/PachiMoji/UI/Fonts/*.ttf`、`Resources/UI/*SDF.asset` | 画面用の書体とその TMP フォント |
| Modify `PachiMoji/Assets/PachiMoji/Editor/PachiAppCheck.cs`・`PachiBuild.cs`・`PachiMovie.cs` | 手元確認・シーン作り・範囲ごとの書き出し |
| Modify `PachiMoji/Packages/manifest.json` | `com.unity.test-framework` を直接の依存に書く |

---

### Task 1: 自動テストの土台と「昇格の範囲 → 時刻の並び」

テスト用の asmdef は**要る**。今のコードは asmdef の無い既定の部品（Assembly-CSharp）にあり、テストの asmdef からは参照できないため。ただし演出全体を asmdef にすると、ThirdParty（Clipper2・LibTessDotNet）まで巻き込むので、**テストしたい計算だけを `PachiMoji.Core` に分ける**。Core は `autoReferenced: true` なので、今のコード（Assembly-CSharp・エディタ用）からはそのまま使える。

**Files:**
- Create: `PachiMoji/Assets/PachiMoji/Core/PachiMoji.Core.asmdef`
- Create: `PachiMoji/Assets/PachiMoji/Core/PromoTimeline.cs`
- Create: `PachiMoji/Assets/PachiMoji/Tests/Editor/PachiMoji.Tests.asmdef`
- Test: `PachiMoji/Assets/PachiMoji/Tests/Editor/PromoTimelineTests.cs`
- Create: `run-tests.sh`
- Modify: `PachiMoji/Packages/manifest.json`（`"com.unity.test-framework": "1.8.0"` を足す。今は他の部品経由で入っているだけ）

**Interfaces:**
- Produces（namespace `PachiMoji`）:
  - `public enum Tier { Silver = 0, Gold = 1, Rainbow = 2 }`
  - `public readonly struct PromoTimes { public readonly float GoldAt, SlashAt, RainbowAt, SpinAt, End; }`（起きない出来事は `float.PositiveInfinity`。比較 `t >= X` が自然に偽になる）
  - `public static class PromoTimeline`：定数 `GoldDelay = 1.3f, WhiteGold = 0.12f, GoldHold = 1.7f, SlashDraw = 0.15f, GrayHold = 1.2f, Spin = 0.7f, SpinRest = 0.45f, LandSpinDelay = 0.5f, Linger = 1.8f` と `public static PromoTimes Plan(float impactEnd, Tier from, Tier to)`

- [ ] **Step 1: asmdef を2つ作る**

`PachiMoji.Core.asmdef`：`{"name":"PachiMoji.Core","rootNamespace":"PachiMoji","references":[],"autoReferenced":true}`

`PachiMoji.Tests.asmdef`：
```json
{
  "name": "PachiMoji.Tests",
  "references": ["PachiMoji.Core", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
  "includePlatforms": ["Editor"],
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"]
}
```

- [ ] **Step 2: `run-tests.sh` を作る**（app-check.sh と同じ形）

`unity run "$ROOT/PachiMoji" --editor-version 6000.6.6f1 --no-banner -- -runTests -testPlatform EditMode -testResults "$ROOT/captures/test-results.xml" -logFile "$ROOT/captures/unity-test.log"`。終わったら `test-results.xml` の `<test-run ...>` から `total`・`passed`・`failed` を、失敗したテストは名前とメッセージを grep で出す。`error CS` もログから拾う。最後に `exit=$code`。

- [ ] **Step 3: 失敗するテストを書く**（`PromoTimelineTests.cs`。数値は `impactEnd = 10` のときの値。誤差 1e-4）

```csharp
static void Times(PromoTimes p, float gold, float slash, float rainbow, float spin, float end) { /* 5つを AreEqual。∞ は IsPositiveInfinity */ }
const float Inf = float.PositiveInfinity;

[Test] public void 銀だけ() => Times(PromoTimeline.Plan(10, Tier.Silver, Tier.Silver), Inf, Inf, Inf, Inf, 11.8f);
[Test] public void 銀から金() => Times(PromoTimeline.Plan(10, Tier.Silver, Tier.Gold), 11.3f, Inf, Inf, Inf, 13.22f);
[Test] public void 銀から虹_今の流れと同じ() => Times(PromoTimeline.Plan(10, Tier.Silver, Tier.Rainbow), 11.3f, 13.12f, 14.47f, 14.47f, 18.12f);
[Test] public void 金だけ() => Times(PromoTimeline.Plan(10, Tier.Gold, Tier.Gold), Inf, Inf, Inf, Inf, 11.8f);
[Test] public void 金から虹() => Times(PromoTimeline.Plan(10, Tier.Gold, Tier.Rainbow), Inf, 11.7f, 13.05f, 13.05f, 16.7f);
[Test] public void 虹だけ_着地のあとに回る() => Times(PromoTimeline.Plan(10, Tier.Rainbow, Tier.Rainbow), Inf, Inf, Inf, 10.5f, 14.15f);
[Test] public void はじまりがおわりより上なら_はじまりだけにする()
    => Times(PromoTimeline.Plan(10, Tier.Rainbow, Tier.Silver), Inf, Inf, Inf, 10.5f, 14.15f);
// 「定時／退社」の ImpactEnd は 7.59 秒
[Test] public void 長さの目安() {
    Assert.That(PromoTimeline.Plan(7.59f, Tier.Silver, Tier.Silver).End, Is.InRange(8.5f, 10f));
    Assert.That(PromoTimeline.Plan(7.59f, Tier.Silver, Tier.Rainbow).End, Is.InRange(14.5f, 16.5f));
}
[Test] public void どの範囲でも時刻は前から順に並ぶ() { /* 6通りすべてで、有限の値だけを取り出して GoldAt<SlashAt<RainbowAt<=SpinAt<End、かつ全部 impactEnd より後 */ }
```

- [ ] **Step 4: 失敗を確かめる** — Run: `sh run-tests.sh` / Expected: `error CS0246`（`Tier`・`PromoTimeline` が無い）

- [ ] **Step 5: `PromoTimeline.Plan` を書く**

決まり：`to = max(from, to)`。金への白飛び `GoldAt = impactEnd + GoldDelay`（はじまりが銀で、おわりが金以上のときだけ）。切り裂き `SlashAt` は、金に切り替わった時刻（銀はじまりなら `GoldAt + WhiteGold`、金はじまりなら `impactEnd`）＋ `GoldHold`（おわりが虹のときだけ）。`RainbowAt = SlashAt + SlashDraw + GrayHold`。`SpinAt` は昇格して虹になるなら `RainbowAt`、虹はじまりなら `impactEnd + LandSpinDelay`。`End` は最後の出来事（銀だけ・金だけは `impactEnd`、銀→金は金に切り替わった時刻、虹は `SpinAt + 2*Spin + SpinRest`）＋ `Linger`。

- [ ] **Step 6: 通ることを確かめる** — Run: `sh run-tests.sh` / Expected: `failed="0"`、9件通る

- [ ] **Step 7: コミット** — `git add` 上のファイル（.meta も）→ `git commit -m "昇格の範囲から時刻の並びを作る関数と、自動テストの土台を足す"`

---

### Task 2: 入力の決まり

**Files:**
- Move: `PachiMoji/Assets/PachiMoji/Scripts/TtfFont.cs(.meta)` → `PachiMoji/Assets/PachiMoji/Core/`（`git mv`。中身は変えない）
- Create: `PachiMoji/Assets/PachiMoji/Core/InputRules.cs`
- Test: `PachiMoji/Assets/PachiMoji/Tests/Editor/InputRulesTests.cs`

**Interfaces:**
- Consumes: `TtfFont.GlyphIndex(int codepoint)`（0 なら書体に無い）
- Produces:
  - `public enum InputProblem { None, Empty, TooLong, MissingGlyph }`
  - `public readonly struct InputCheck { public readonly InputProblem Problem; public readonly string[] Lines; public readonly string BadChar; public bool CanPlay => Problem == InputProblem.None; }`
  - `public static class InputRules { public const int MaxLines = 2, MaxChars = 6; public static InputCheck Check(string line1, string line2, System.Func<int, bool> hasGlyph); public static string Message(InputCheck c); }`

入力欄は**1行ずつの欄を2つ縦に並べる**（1つの欄で改行させると、iPhone の日本語入力で「改行」と「確定」がぶつかるため）。見た目は1枚の銀の板の中に2段。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
static readonly System.Func<int, bool> All = _ => true;
[Test] public void 二行とも空なら押せない() => Assert.AreEqual(InputProblem.Empty, InputRules.Check("", "", All).Problem);
[Test] public void 空白だけも空() => Assert.AreEqual(InputProblem.Empty, InputRules.Check(" 　", "　", All).Problem);
[Test] public void 前後の空白は落とす() => CollectionAssert.AreEqual(new[] { "定時", "退社" }, InputRules.Check(" 定時　", "退社 ", All).Lines);
[Test] public void 一行目が空なら二行目を一行にする() => CollectionAssert.AreEqual(new[] { "退社" }, InputRules.Check("", "退社", All).Lines);
[Test] public void 六文字ちょうどは通る() => Assert.IsTrue(InputRules.Check("脳汁ドバドバ", "", All).CanPlay);
[Test] public void 七文字は長すぎ() => Assert.AreEqual(InputProblem.TooLong, InputRules.Check("脳汁ドバドバ！", "", All).Problem);
[Test] public void 中の空白は一文字に数える() => Assert.AreEqual(InputProblem.TooLong, InputRules.Check("脳汁 ドバドバ", "", All).Problem);
[Test] public void 書体にない字を知らせる() {
    var c = InputRules.Check("定時", "退社", cp => cp != '社');
    Assert.AreEqual(InputProblem.MissingGlyph, c.Problem); Assert.AreEqual("社", c.BadChar);
    StringAssert.Contains("「社」", InputRules.Message(c));
}
[Test] public void 絵文字は二つ分の文字コードでも一文字として知らせる() {
    var c = InputRules.Check("激アツ😀", "", All);   // 0xFFFF を超える字は、舞台が1文字ずつ切り出せないので書体にあっても断る
    Assert.AreEqual(InputProblem.MissingGlyph, c.Problem); Assert.AreEqual("😀", c.BadChar);
}
[Test] public void 本物の書体で確かめる() {
    var reggae = new TtfFont(Resources.Load<TextAsset>("Fonts/ReggaeOne").bytes);
    System.Func<int, bool> has = cp => reggae.GlyphIndex(cp) != 0;
    Assert.IsTrue(InputRules.Check("定時", "退社", has).CanPlay);
    Assert.AreEqual(InputProblem.MissingGlyph, InputRules.Check("♨", "", has).Problem);   // 実際に無い字を1つ確かめて選ぶ
}
```

- [ ] **Step 2: 失敗を確かめる** — `sh run-tests.sh` / Expected: `InputRules` が無いというコンパイルエラー

- [ ] **Step 3: `InputRules.Check` と `Message` を書く**

数えるのは文字コード（サロゲートの組は1文字）。空白（`char.IsWhiteSpace`）は書体を確かめない。調べる順は Empty → TooLong → MissingGlyph（最初に見つかった1字）。`Message` の文（そのまま画面に出す）：Empty＝`言葉を入れると PUSH できます`、TooLong＝`1行は6文字までです`、MissingGlyph＝`「X」はこの書体で出せません。ほかの字にしてください`、None＝空文字。

- [ ] **Step 4: 通ることを確かめる** — `sh run-tests.sh` / Expected: `failed="0"`。あわせて `sh capture.sh` が `exit=0`（TtfFont を動かしても演出の文字が作れること）

- [ ] **Step 5: コミット** — `git commit -m "入力の決まり（2行・6文字・書体にない字）とテストを足す"`

---

### Task 3: 設定と保存、再生の時計・消灯・メダルのなぞり

**Files:**
- Create: `PachiMoji/Assets/PachiMoji/Core/EffectSettings.cs`
- Create: `PachiMoji/Assets/PachiMoji/Core/AppRules.cs`
- Test: `PachiMoji/Assets/PachiMoji/Tests/Editor/EffectSettingsTests.cs`・`AppRulesTests.cs`

**Interfaces:**
- Consumes: `Tier`（Task 1）
- Produces:
  - `public enum LightColor { IceBlue, Gold, Red, Rainbow }`、`public enum LightSize { Small, Medium, Large }`、`public enum TextFont { ReggaeOne, Mincho }`
  - `public class EffectSettings { public bool SparksOn = true; public LightColor Light = LightColor.IceBlue; public LightSize Size = LightSize.Medium; public TextFont Font = TextFont.ReggaeOne; public Tier From = Tier.Silver; public Tier To = Tier.Rainbow; public string FontName { get; } public float LightScale { get; } public static EffectSettings Load(string prefix = "fx."); public void Save(string prefix = "fx."); public EffectSettings Clone(); }`
    - `FontName`：`"ReggaeOne"`／`"ZenOldMinchoBlack"`（`Resources/Fonts/` の名前）。`LightScale`：小 0.75／中 1／大 1.3
  - `public enum AppState { Idle, Playing, Finished, Settings }`
  - `public static class PlayClock { public const float MaxStep = 0.1f; public static float Step(float t, float dt); }`
  - `public static class SleepPolicy { public static int For(AppState s); }`（再生中だけ `SleepTimeout.NeverSleep`、ほかは `SleepTimeout.SystemSetting`）
  - `public static class RangePicker { public static (Tier from, Tier to) FromDrag(int downIndex, int upIndex); }`（0=銀,1=金,2=虹。範囲外は 0〜2 に収める）

- [ ] **Step 1: 失敗するテストを書く**（設定のテストは `prefix = "test.fx."` を使い、`[TearDown]` でそのキーを全部消す。本物の設定を汚さない）

```csharp
[Test] public void 何も保存していなければ既定() { var s = EffectSettings.Load(P); Assert.IsTrue(s.SparksOn); Assert.AreEqual(Tier.Silver, s.From); Assert.AreEqual(Tier.Rainbow, s.To); Assert.AreEqual("ReggaeOne", s.FontName); }
[Test] public void 保存して読み戻すと同じ() { /* 全項目を既定以外にして Save → Load で一致 */ }
[Test] public void はじまりがおわりより上なら入れ替える() { PlayerPrefs.SetInt(P + "from", 2); PlayerPrefs.SetInt(P + "to", 0); var s = EffectSettings.Load(P); Assert.AreEqual(Tier.Silver, s.From); Assert.AreEqual(Tier.Rainbow, s.To); }
[Test] public void 範囲外の数字は既定に戻す() { PlayerPrefs.SetInt(P + "light", 99); PlayerPrefs.SetInt(P + "font", -1); var s = EffectSettings.Load(P); Assert.AreEqual(LightColor.IceBlue, s.Light); Assert.AreEqual(TextFont.ReggaeOne, s.Font); }
[Test] public void 光の大きさ() { /* Small 0.75 / Medium 1 / Large 1.3 */ }

[Test] public void 時計はふつうに進む() => Assert.AreEqual(1.0167f, PlayClock.Step(1f, 1f / 60), 1e-4f);
[Test] public void 裏から戻っても飛ばない() => Assert.AreEqual(5.1f, PlayClock.Step(5f, 30f), 1e-4f);
[Test] public void 再生中だけ消灯しない() { Assert.AreEqual(SleepTimeout.NeverSleep, SleepPolicy.For(AppState.Playing)); Assert.AreEqual(SleepTimeout.SystemSetting, SleepPolicy.For(AppState.Idle)); /* Finished・Settings も SystemSetting */ }
[Test] public void なぞった向きに関係なく小さい方がはじまり() { Assert.AreEqual((Tier.Silver, Tier.Rainbow), RangePicker.FromDrag(2, 0)); Assert.AreEqual((Tier.Gold, Tier.Gold), RangePicker.FromDrag(1, 1)); Assert.AreEqual((Tier.Gold, Tier.Rainbow), RangePicker.FromDrag(1, 5)); }
```

- [ ] **Step 2: 失敗を確かめる** — `sh run-tests.sh` / Expected: コンパイルエラー

- [ ] **Step 3: 書く**

PlayerPrefs のキーは `prefix + "sparks" / "light" / "size" / "font" / "from" / "to"`（すべて int）。`Save` の最後に `PlayerPrefs.Save()`。読むときは enum の範囲外を既定に、`from > to` は入れ替える。

- [ ] **Step 4: 通ることを確かめる** — `sh run-tests.sh` / Expected: `failed="0"`

- [ ] **Step 5: コミット** — `git commit -m "設定の保存と、再生の時計・消灯・メダルのなぞりの決まりを足す"`

---

### Task 4: 王道昇格に設定を効かせる（範囲・プチュンの光・火花・書体）

**Files:**
- Modify: `PachiMoji/Assets/PachiMoji/Scripts/PachiStage.cs`
- Modify: `PachiMoji/Assets/PachiMoji/Shaders/PachiRays.shader`
- Modify: `PachiMoji/Assets/PachiMoji/Editor/PachiMovie.cs`、`movie.sh`

**Interfaces:**
- Consumes: `PromoTimeline.Plan`、`PromoTimes`、`Tier`、`EffectSettings`
- Produces: `public static PachiStage Build(string[] lines, float aspect, EffectSettings settings = null)`（null は既定の設定。今の `font` 引数は `settings.FontName` に置き換える。呼んでいるのは PachiMovie・PachiBuild.MakeIcon・PachiAppCheck だけ）

- [ ] **Step 1: 時刻を `PromoTimes` から取るようにする**

`Build` で `times = PromoTimeline.Plan(ImpactEnd, settings.From, settings.To)` を持ち、`GoldAt`・`SlashAt`・`RainbowAt`・`End` は `times` の値を返す。PachiStage の定数 `WhiteGold`・`SlashDraw`・`GrayHold`・`Spin`・`SpinRest` は `PromoTimeline` の同じ名前の定数を使う（値を2か所に書かない）。

- [ ] **Step 2: `Evaluate` の「虹か」を3つに分ける**

今の `rain` が受け持っている役目を分ける：(a) 見た目が虹か＝`fin == Rainbow`（色の流れ・背景の切り替え）、(b) 回転＝`SpinAt` からの経過で 1回転→止まる→1回転、余韻の揺れも回り終えてから、(c) 跳ね・閃光・光の筋（`pop`）＝**昇格した瞬間だけ**（`RainbowAt`・金への切り替え）。昇格前の色（着地の色）は `settings.From` の色。金で着地したときは、金に切り替わったあとの「光の筋が横切る」を着地の 0.2〜0.65 秒後に出す。虹の色の流れ `hue` は虹になった時刻（昇格なら `RainbowAt`、虹で着地なら `ImpactEnd`）からの経過で回す。

- [ ] **Step 3: プチュンの光の色と大きさ**

`PachiRays.shader` に `_PTint ("Puchun Tint", Color) = (0.55, 0.75, 1, 1)` と `_PRainbow ("Puchun Rainbow", Float) = 0` を足し、`float3 ice = ...` を `_PTint.rgb`（`_PRainbow` が 1 なら、中心からの角度と `_T` で色相が回る `hsv2rgb(frac(角度/2π + _T*0.5), 0.6, 1)`）に置き換える。色：青白 `(0.55, 0.75, 1.0)`／金 `(1.0, 0.78, 0.30)`／赤 `(1.0, 0.25, 0.20)`／虹。大きさは `Evaluate` の `pScale` に `settings.LightScale` を掛ける。火花は `SparksOn = settings.SparksOn`。

- [ ] **Step 4: 範囲ごとに書き出せるようにする**

`PachiMovie` で環境変数 `PACHI_RANGE`（`SS`/`SG`/`SR`/`GG`/`GR`/`RR`、無ければ `SR`）を読み、`EffectSettings` の From/To にして `Build` に渡す。範囲を指定したときの一覧画像の名前は `movie_range_<範囲>_sheet.png`。`movie.sh` の4つ目の引数で渡す（例：`sh movie.sh 0 -1 quick GR`）。

- [ ] **Step 5: 既定が変わっていないことを確かめる** — Run: `sh run-tests.sh` と `sh movie.sh 0 -1 quick` / Expected: テスト全部通る、ログの `seconds=15.71`（今と同じ長さ）。一覧画像を見て、今の流れ（銀→金→切り裂き→虹→回転）と同じであること

- [ ] **Step 6: 6通りを書き出して見る** — Run: `sh movie.sh 0 -1 quick SS`（GG・RR・SG・GR も同じく）/ Expected: 一覧画像で、着地の色がはじまりの色・おわりの色で余韻・虹だけは着地のあとに回転、が見える。ログの長さが SS 約9.4秒・RR 約11.7秒。気になる絵があればユーザーに見せて相談する（動画は送らず、一覧画像を見て文章で伝える）

- [ ] **Step 7: プチュンの光を確かめる** — `PachiAppCheck` に「金・大」「虹・小」でプチュンの 0.4 秒目を1枚ずつ撮る処理を足し（`captures/puchun_gold_large.png`・`puchun_rainbow_small.png`）、`sh app-check.sh` / Expected: `[PachiAppCheck] OK`、絵で光の色と大きさが変わっている

- [ ] **Step 8: コミット** — `git commit -m "王道昇格に昇格の範囲・プチュンの光の色と大きさ・火花・書体の設定を効かせる"`

---

### Task 5: 演出の部品化（IPachiEffect・王道昇格の包み・一覧表・待ちの姿・片付け）

**Files:**
- Create: `PachiMoji/Assets/PachiMoji/Core/IPachiEffect.cs`
- Create: `PachiMoji/Assets/PachiMoji/Scripts/OudouEffect.cs`、`EffectCatalog.cs`
- Modify: `PachiMoji/Assets/PachiMoji/Scripts/PachiStage.cs`（`Idle`・`Dispose`）
- Modify: `PachiMoji/Assets/PachiMoji/Scripts/PachiText.cs`（`Rig` に作ったメッシュを覚えさせる）
- Modify: `PachiMoji/Assets/PachiMoji/Editor/PachiAppCheck.cs`

**Interfaces:**
- Consumes: `PachiStage.Build(lines, aspect, settings)`（Task 4）、`EffectSettings`
- Produces:
  ```csharp
  public interface IPachiEffect
  {
      void Build(string[] lines, float aspect, EffectSettings settings);  // 組み立てる
      float Duration { get; }                                             // 長さ（秒）
      void Evaluate(float t);                                             // 時刻 t の姿にする（状態を持たない）
      void Idle(float t);                                                 // 入力を待っているあいだの姿（仕様書の形に足したもの）
      Camera Camera { get; }                                              // 描くカメラ
      void Dispose();                                                     // 片付ける
  }
  public class EffectEntry { public string Id; public string Name; public string RequiredProduct; public System.Func<IPachiEffect> Create; }
  public static class EffectCatalog { public static readonly EffectEntry[] All; public static EffectEntry Get(string id); }   // 知らない ID は All[0]
  ```
  `All` は今は1行：`Id = "oudou", Name = "王道昇格", RequiredProduct = null（無料）, Create = () => new OudouEffect()`

`Idle` を足す理由：メイン画面の舞台では「銀の立体がゆっくり揺れて待つ」姿が要るが、`Evaluate` のどの時刻もその姿ではない（背景が激しい・範囲によっては金や虹）。

- [ ] **Step 1: 手元確認に「片付けの数え」と「12文字の絵」を足す**（`PachiAppCheck.Run`、Task 4 の確認の続き）

```csharp
// 10回組み立てて片付けたら、シーンの物・材質・メッシュの数が元に戻る
int go0 = CountSceneObjects(), mat0 = Count<Material>(), mesh0 = Count<Mesh>();
for (int i = 0; i < 10; i++) { var e = EffectCatalog.Get("oudou").Create(); e.Build(new[] { "定時" + i, "退社" }, 0.5f, new EffectSettings()); e.Evaluate(9f); e.Dispose(); }
Expect(CountSceneObjects() == go0 && Count<Material>() == mat0 && Count<Mesh>() == mesh0, "組み立て直しても物が残らない");
Expect(EffectCatalog.Get("知らない").Id == "oudou", "知らない ID は王道昇格");
```
`Count<T>()` は `Resources.FindObjectsOfTypeAll<T>().Length`。あわせて、`脳汁ドバドバ／激アツ確定だ`（6文字×2行）を 9:19.5 で虹の余韻と `Idle(1f)` の2枚撮る（`captures/fit_12_end.png`・`fit_12_idle.png`）。

- [ ] **Step 2: 失敗を確かめる** — `sh app-check.sh` / Expected: `IPachiEffect`・`EffectCatalog` が無いというコンパイルエラー

- [ ] **Step 3: `PachiStage.Dispose()` を書く**

Build で作ったもの（文字・チラ見せの1文字ずつ・火花・カメラ（背景・閃光・光の板は子）・Volume の GameObject、`new Material` した材質すべて、PachiTextBuilder が作ったメッシュ、VolumeProfile）を記録し、全部消す。**Quad の共有メッシュと `PachiRamps` の色表は消さない**（ほかの舞台も使う）。再生中は `Object.Destroy`、エディタの裏起動では `Object.DestroyImmediate`（`Application.isPlaying` で分ける）。PachiText・PachiSparks が中で作った材質・メッシュは、`Rig`・`PachiSparks` に覚えさせて拾う。

- [ ] **Step 4: `PachiStage.Idle(float t)` を書く**

文字は銀（`SetLook` 銀）、基本の向きから `sin(t*0.7)*6°` の揺れとごくわずかな上下、角のキラッはまたたく。背景は青（`_Mode` 2）を `_Gain 0.22`・稲妻なし・火花 0.3 で暗く。閃光・光の板は 0、火花の粒は出さない、チラ見せ用の立体は隠す、カメラは基本の位置・画角。

- [ ] **Step 5: `OudouEffect` と `EffectCatalog` を書く** — `OudouEffect` は PachiStage にそのまま渡すだけ（`Duration => stage.End`、`Camera => stage.cam`）

- [ ] **Step 6: 通ることを確かめる** — `sh app-check.sh` / Expected: `[PachiAppCheck] OK`。`fit_12_*.png` で12文字が画面の内側に収まっている

- [ ] **Step 7: コミット** — `git commit -m "演出の形 IPachiEffect と王道昇格の包み・一覧表を作り、舞台に待ちの姿と片付けを足す"`

---

### Task 6: 画面の素材（TMP・書体・コードで描く絵）

見た目の方針（frontend-design で詰めたもの）：**パチンコ台の「盤面の下」をそのまま手のひらに置く**。上は演出の舞台（真っ黒）、その下に台の下皿まわりのような**クロムの横長の板**、そのまん中に**本物のチャンスボタンの形をした赤いドーム**。主役はドームだけで、ほかは黒と銀でおとなしく。動きは「PUSH がゆっくり脈打つ（実機のボタンの点滅）」の1つだけに絞る。英字の大文字の見出し・番号・カードの並び・矢印付きのボタンなど、ありがちな飾りは使わない。

**Files:**
- Create: `PachiMoji/Assets/PachiMoji/Editor/PachiUiAssets.cs`
- Create: `PachiMoji/Assets/PachiMoji/UI/Fonts/ReggaeOne-UI.ttf`（`Resources/Fonts/ReggaeOne.bytes` の写し。演出側は .bytes を読むので、画面用に .ttf として別に置く）、`BIZUDPGothic-Regular.ttf`、`OFL-BIZUDPGothic.txt`
- Create: `PachiMoji/Assets/PachiMoji/Resources/UI/ReggaeOne SDF.asset`、`BIZUDPGothic SDF.asset`
- Create: `PachiMoji/Assets/TextMesh Pro/`（TMP の基本素材。取り込んだまま）
- Create: `PachiMoji/Assets/PachiMoji/Scripts/UI/UiTheme.cs`、`UiArt.cs`
- Modify: `PachiMoji/Assets/PachiMoji/Editor/PachiAppCheck.cs`

**Interfaces:**
- Produces:
  - `public static class UiTheme`：色 `Black #000000, Chrome #C8CDD4, ChanceRed #E3001B, Gold #F2C14E, Muted #8A8F98`、`ReferenceSize = new Vector2(390, 844)`（iPhone の pt と同じ目盛り）、`BannerHeight = 62`、`PushDiameter = 176`、`static TMP_FontAsset Display`（Reggae One）、`static TMP_FontAsset Body`（BIZ UDPゴシック）
  - `public static class UiArt`（作った絵は使い回す）：`Sprite Dome(bool enabled)`（赤いドーム＋クロムの縁＋上の照り返し、512px）、`Sprite ChromePlate()`（角の丸い銀メッキの板、9スライス。横に光の帯）、`Sprite Gear()`（自作の歯車：8枚歯、銀）、`Sprite Medal(Tier t)`（銀・金・虹のメダル、虹は色相が一周）、`Sprite Rail()`（メダルをつなぐ金の帯、9スライス）

- [ ] **Step 1: BIZ UDPゴシックを落としてよいかユーザーに聞く**

聞く内容：「Google Fonts の BIZ UDPゴシック（`BIZUDPGothic-Regular.ttf`、無料・OFL、数MB）を `https://github.com/google/fonts/raw/main/ofl/bizudpgothic/` から落とします。いいですか？」。OK なら `.ttf` と `OFL.txt`（→ `OFL-BIZUDPGothic.txt`）を落とす。だめなら説明文も Reggae One にする（`UiTheme.Body = Display`）。

- [ ] **Step 2: `PachiUiAssets.Make`（`-executeMethod` で動かす）を書く**

(1) `Assets/TextMesh Pro` が無ければ `AssetDatabase.ImportPackage("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage", false)`。(2) 2つの .ttf から `TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic)`、`isMultiAtlasTexturesEnabled = true`（日本語は字が多い）。`AssetDatabase.CreateAsset` のあと、材質と文字の絵（atlasTexture）を `AddObjectToAsset` で同じファイルに入れる（入れないと保存後に消える）。何度呼んでも同じ結果。

- [ ] **Step 3: `UiTheme`・`UiArt` を書く**（絵は C# で1画素ずつ塗る。シェーダーを増やさないので、iPhone 版で抜け落ちる心配がない）

ドームの塗り：外側から、暗いクロムの縁（幅 8%）→ 明るいクロムの縁（4%）→ 赤の半球（中心 `#FF3B4E`、外へ `#E3001B`、縁で `#7A000E`）→ 左上に横長の白い照り返し（透明度 0.55）。押せないときは赤を `#4A0A12` まで沈め、照り返しを 0.2 に。

- [ ] **Step 4: 素材の一覧を撮る手元確認を足す** — `PachiAppCheck` に、`UiArt` の全部の絵と、2つの TMP フォントで「脳汁メーカー」「言葉を入れると PUSH できます」を並べた 1 枚を撮る処理（`captures/ui_parts.png`）

- [ ] **Step 5: 動かして見る** — Run: `unity run ... -executeMethod PachiMoji.EditorTools.PachiUiAssets.Make` のあと `sh app-check.sh` / Expected: `[PachiAppCheck] OK`、`ui_parts.png` でドームが本物のチャンスボタンらしく立体に見える・日本語が豆腐（□）にならない

- [ ] **Step 6: コミット** — `git commit -m "画面の素材：TMP の基本素材・画面用の書体・コードで描くボタンと枠を足す"`（書体と OFL の文も入れる）

---

### Task 7: メイン画面と流れ（PachiPlayer を置き換える）

**画面の作り**（下から上へ。すべて `Screen.safeArea` の内側に置く根っこの下）：

```
┌───────────────────┐
│                ⚙  │ ← 自作の歯車（44pt 四方の押せる範囲）。押すと設定画面
│   （演出の舞台）    │ ← 待ち：入力した言葉が銀で揺れている（舞台カメラの描く範囲＝ここだけ）
│     定 時          │
│     退 社          │
│ ┌───────────────┐ │
│ │ 1行目（6文字まで）│ │ ← 銀メッキの板の中に、1行ずつの欄が2段
│ │ 2行目（なくてもOK）│ │
│ └───────────────┘ │
│ 「社」はこの書体で…  │ ← 知らせ（灰。問題が無ければ空）
│        ◉ PUSH      │ ← 赤いドーム。押せないときは沈んだ色
├───────────────────┤
│   （バナーの場所）   │ ← BannerHeight の空き。順番4で広告を置く
└───────────────────┘
```

- Canvas は Screen Space - Camera で、専用の UI カメラ（正投影、舞台カメラより後に描き、色は消さない）に描く。手元確認の撮影では、舞台→UI の順に同じ RenderTexture に描けば画面の絵になる
- CanvasScaler：Scale With Screen Size、基準 390×844、幅に合わせる（match 0）
- 舞台カメラの描く範囲（`Camera.rect`）を、待ちでは「入力の板より上」、再生では「バナーより上の全部」にする。`Build` に渡す縦横比は再生のときの範囲で決める（待ちの範囲では文字が少し小さく映るだけで、はみ出さない）
- PUSH を押す → ドームが 0.08 秒沈む → 入力の板・PUSH・歯車が 0.3 秒で下へ沈んで消える → 範囲を全部にして `t = 0` から再生（演出の頭は真っ暗なので切り替えが目立たない）
- 終わり（`t ≥ Duration`）→ 終わりの姿で止め、下に銀の板のボタン「もう一回」「書き出す」と、左上に小さく「言葉を変える」。「書き出す」は順番3まで押せない形で出し、下に灰で `書き出しは次の版でできます`。「言葉を変える」は待ちに戻る（仕様書の2つのボタンだけでは入力に戻れないので足す）
- 舞台を組み立て直すのは「入力を確定したとき」（欄の `onEndEdit`）と「設定を閉じたとき」で、言葉と設定が前と同じなら作り直さない。作り直す前に前の演出を `Dispose`
- 書体にあるかの確かめは、そのときの設定の書体で行う：`cp => PachiText.LoadFont(settings.FontName).GlyphIndex(cp) != 0`
- 再生の時刻は `t = PlayClock.Step(t, Time.unscaledDeltaTime)`、状態が変わるたびに `Screen.sleepTimeout = SleepPolicy.For(state)`
- iPhone の入力：`TMP_InputField` の `shouldHideMobileInput = false`（キーボードの上に iPhone の入力欄が出る形。日本語の変換が確実）、`characterLimit = 6`、`lineType = SingleLine`。1行目で改行キー → 2行目へ

**Files:**
- Create: `PachiMoji/Assets/PachiMoji/Scripts/UI/MainScreen.cs`、`PachiMoji/Assets/PachiMoji/Scripts/PachiApp.cs`
- Delete: `PachiMoji/Assets/PachiMoji/Scripts/PachiPlayer.cs(.meta)`
- Modify: `PachiMoji/Assets/PachiMoji/Editor/PachiBuild.cs`（`MakePlayScene` は `PachiApp` を置く）、`PachiMoji/Assets/Scenes/Play.unity`（コマンドで作り直す）
- Modify: `PachiMoji/Assets/PachiMoji/Editor/PachiAppCheck.cs`（`LoopTime` の確かめを消し、画面の絵を足す）

**Interfaces:**
- Consumes: `IPachiEffect`・`EffectCatalog`（Task 5）、`InputRules`（Task 2）、`EffectSettings`・`PlayClock`・`SleepPolicy`・`AppState`（Task 3）、`UiTheme`・`UiArt`（Task 6）
- Produces:
  - `public class MainScreen { public static MainScreen Build(Transform canvasRoot); public TMP_InputField Line1, Line2; public Button Push, Again, Export, ChangeWords, Gear; public TMP_Text Notice; public RectTransform StageIdleArea, StagePlayArea; public void ShowState(AppState s); public void SetPushEnabled(bool on); }`
  - `public class PachiApp : MonoBehaviour`（流れを持つ。設定画面は Task 8 でつなぐ）
  - `public static Rect ViewportOf(RectTransform area, Camera uiCam)`（`MainScreen` に置く。舞台カメラの `rect` に使う）

- [ ] **Step 1: 手元確認に画面の絵を足す**（`PachiAppCheck`）

9:16（720×1280）と 9:19.5（720×1560）で、(a) 待ち（「定時／退社」が舞台で揺れている、`Idle(1f)`）、(b) 知らせが出た待ち（2行目に `♨`、PUSH が沈んだ色）、(c) 終わり（`Evaluate(Duration)` と「もう一回」「書き出す」）を撮る（`captures/main_<比率>_<idle|notice|end>.png`）。edit モードでは `Start` が動かないので、`MainScreen.Build` と演出を直接組み、`Canvas.ForceUpdateCanvases()` してから撮る。safe area は 9:19.5 のとき上 47pt・下 34pt として計算する関数に渡す（`MainScreen` の配置を safe area の矩形を引数に取る形にしておく）。`LoopTime` の3つの確かめは消し、`EditorBuildSettings` のシーンが Play だけ、の確かめは残す。

- [ ] **Step 2: 失敗を確かめる** — `sh app-check.sh` / Expected: `MainScreen` が無いというコンパイルエラー

- [ ] **Step 3: `MainScreen` と `PachiApp` を書き、`PachiPlayer` を消す**

- [ ] **Step 4: シーンを作り直す** — Run: `unity run ... -executeMethod PachiMoji.EditorTools.PachiBuild.MakePlayScene` / Expected: `[PachiBuild] シーンを作った`、`Play.unity` に `PachiApp` だけ

- [ ] **Step 5: 確かめる** — Run: `sh run-tests.sh` と `sh app-check.sh` / Expected: テスト全部通る・`[PachiAppCheck] OK`。6枚の絵で：言葉・ボタン・歯車がノッチとホームバーにかからない、PUSH がいちばん目立つ、知らせの文が切れない、バナーの場所が空いている

- [ ] **Step 6: コミット** — `git commit -m "メイン画面と再生の流れを作り、PachiPlayer を置き換える"`（`Play.unity`・消した `PachiPlayer.cs.meta` も）

---

### Task 8: 設定画面

**画面の作り**：黒の全面が下からせり上がる（0.25 秒）。上に「設定」（Reggae One）と左上に「もどる」。項目は上から：

| 項目 | 選べるもの | 形 |
|---|---|---|
| 火花 | あり／なし | 銀の板を2つ並べ、選んでいる方の文字を金・下に細い金の線 |
| プチュンの光の色 | 青白／金／赤／虹 | 同じ形。文字の左にその色の小さな光の点（`UiArt` で描く） |
| プチュンの光の大きさ | 小／中／大 | 同じ形 |
| 書体 | ポップ（Reggae One）／明朝 | 同じ形。選択肢の文字をその書体で書く |
| 昇格の範囲 | 銀・金・虹のメダル3枚 | メダルの後ろを金の帯（`Rail`）がはじまり〜おわりまでつなぐ。指を置いたメダルから離したメダルまでが範囲（`RangePicker.FromDrag`）。1枚だけ触れば、その色だけ。下に灰で `メダルを指でなぞって、はじまりとおわりの色を選びます` と、今の範囲 `銀 → 虹` |

- 買い物の項目は順番4で足す（この計画では入れない）
- 変えたらすぐ `EffectSettings.Save`。「もどる」で閉じたとき、演出に関わる設定が開いたときと違えば舞台を組み立て直す（Task 7 の決まり）。書体を替えたら入力を確かめ直す（明朝に無い字の知らせが出る）
- 設定画面のあいだは `AppState.Settings`

**Files:**
- Create: `PachiMoji/Assets/PachiMoji/Scripts/UI/SettingsScreen.cs`
- Modify: `PachiMoji/Assets/PachiMoji/Scripts/PachiApp.cs`（歯車で開く・閉じたら反映）
- Modify: `PachiMoji/Assets/PachiMoji/Scripts/UI/UiArt.cs`（光の点 `Sprite Dot(Color c)`）
- Modify: `PachiMoji/Assets/PachiMoji/Editor/PachiAppCheck.cs`

**Interfaces:**
- Consumes: `EffectSettings`・`RangePicker`・`Tier`（Task 3）、`UiTheme`・`UiArt`（Task 6）
- Produces: `public class SettingsScreen { public static SettingsScreen Build(Transform canvasRoot, EffectSettings settings, System.Action<EffectSettings> onChanged); public Button Back; public void Show(bool on); }`

- [ ] **Step 1: 手元確認に設定画面の絵を足す** — 既定の設定と「なし・虹・大・明朝・金→虹」の2通りを 9:19.5 で撮る（`captures/settings_default.png`・`settings_changed.png`）。あわせて、`onChanged` で受け取った設定を `Save`→`Load` して同じになることを確かめる

- [ ] **Step 2: 失敗を確かめる** — `sh app-check.sh` / Expected: `SettingsScreen` が無いというコンパイルエラー

- [ ] **Step 3: `SettingsScreen` を書き、`PachiApp` につなぐ**

- [ ] **Step 4: 確かめる** — `sh run-tests.sh` と `sh app-check.sh` / Expected: 全部通る・OK。絵で、選んでいるものが金ではっきり分かる・メダルの金の帯が範囲どおり・文字が切れない

- [ ] **Step 5: コミット** — `git commit -m "設定画面（火花・プチュンの光・書体・昇格の範囲）を作る"`

---

### Task 9: iPhone で確かめる

**Files:** なし（ワークフローは順番1のまま）

- [ ] **Step 1: 手元でまとめて確かめる** — `sh run-tests.sh`・`sh app-check.sh`・`sh movie.sh 0 -1 quick` / Expected: 全部通る、既定の長さ `seconds=15.71`

- [ ] **Step 2: push してよいかユーザーに聞き、OK なら `git push`**

- [ ] **Step 3: TestFlight へ組み立てる** — Run: `"/c/Program Files/GitHub CLI/gh.exe" workflow run ios-testflight.yml --repo maosun-dev/pachieffect`、続けて `gh run watch` / Expected: 2つの仕事とも成功。失敗したら `gh run view --log-failed` で読む（画面の撮影はしない）。よくある原因：TMP のシェーダー・素材がアプリに入っていない、テストの asmdef が iPhone 用の組み立てに混ざる（`includePlatforms: Editor` で防いでいるはず）

- [ ] **Step 4: ユーザーに iPhone で見てもらう**（確かめてほしいこと）
  - 日本語を入れて変換・確定できる。7文字目は入らない。絵文字を入れると知らせが出て PUSH が押せない
  - PUSH で演出が滑らかに流れ、終わると「もう一回」「書き出す（次の版で）」「言葉を変える」が出る
  - 設定を変えると演出に効く（火花なし・光の色と大きさ・明朝・昇格の範囲の6通り）。アプリを完全に閉じて開き直しても設定が残っている
  - 再生中に画面が暗くならない。待ちの画面では、いつもどおり自動で暗くなる
  - 再生の途中でホームに戻って開き直すと、続きから流れる（終わりに飛ばない）
  - ノッチ・ホームバーに文字やボタンがかからない

---

## 自己点検のメモ（計画を書いた側が確かめたこと）

- 仕様書の順番2の中身との対応：IPachiEffect と王道昇格の包み（Task 5）、EffectCatalog（Task 5）、EffectSettings と PlayerPrefs（Task 3）、昇格の範囲と時刻の並びのテスト（Task 1・4）、入力の決まり（Task 2・7）、メイン画面（Task 6・7）、設定画面（Task 8）、Screen.sleepTimeout（Task 3・7）、PachiPlayer の置き換え（Task 7）
- 仕様書から変えた・足したところ：`IPachiEffect` に `Idle` を足した／入力欄を1行ずつ2段にした／終わりの画面に「言葉を変える」を足した／「書き出す」は順番3まで押せない形で出す。どれも理由は各タスクに書いた
- 仕様書の確かめ方の「設定の保存と読み込み」「入力の決まり」「昇格の範囲から時刻の並び」は EditMode テスト、「買ったかどうかの判定」は順番4
