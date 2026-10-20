# 昇格の色を7段から選ぶ・チラ見せの色と文字数 作業計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 色の格を「銀・青・緑・紫・赤・金・虹」の7段にし、昇格で通る色を7枚のメダルから選べるようにする。チラ見せは文字色・背景（各7色）と文字数（0〜12）を選べるようにして、言葉の文字を全部まで見せる。

**Architecture:** 色の格・色の作り方・昇格の時刻の並び・チラ見せの並び・設定の保存は `PachiMoji.Core` の小さな計算に分け、EditMode テストで確かめる。PachiStage はその計算の結果（`PromoPlan`・`TeasePlan`）を見て姿を決めるだけにする。文字の色は昨日作った「銀の対応表に色を乗せる」作り、背景は青の背景の色相を回す作りに、銀用の「色を抜く」を足す。

**Tech Stack:** Unity 6000.6.6f1・URP・uGUI＋TMP・HLSL（PachiBurst.shader）・NUnit

**Spec:** `docs/superpowers/specs/2026-10-20-promo-chain-and-tease-design.md`

## Global Constraints

- 色の格の順：**銀 → 青 → 緑 → 紫 → 赤 → 金 → 虹**（`Tier` の値 0〜6）
- 文字の色の芯：青 `#2A7BFF`・緑 `#19D14A`・紫 `#A040FF`・赤 `#FF2030`（銀・金・虹は今の対応表のまま）
- 背景の色相の回し（青の背景から）：青 0°・緑 -85°・紫 65°・赤 125°。銀は灰色・白の熱くない暗がり、金は炎、虹は虹
- 昇格の時刻：ドンが動き出してから 1.3 秒で最初の白飛び（`GoldDelay`）。昇格のあと次の出来事まで 1.7 秒（`GoldHold`）を、段が進むごとに 0.85 倍、最短 0.7 秒。白く飛ぶ長さ 0.12 秒、余韻 1.8 秒、虹の特別な形（切り裂き 0.15・灰色のタメ 1.2・回転 0.7×2＋止まる 0.45）、虹だけで着地したら 0.5 秒後に回転 — どれも今の値のまま
- **既定の「銀 → 金 → 虹」・4文字以上の言葉は、今の時刻の並びと同じになること**（今の演出を崩さない）
- チラ見せ：1文字の長さ 0.9 → 0.75 → 0.6 → 0.55 → 0.5 → 0.45（最短）、あいだの暗転 0.5 → 0.4 → 0.3 → 0.2（最短）。文字数 0〜12、既定 12。言葉の文字数より多ければ言葉の文字数
- 既定：昇格の色「銀・金・虹」、チラ見せの文字色 銀、背景 青、文字数 12
- 保存の名前（PlayerPrefs、頭に `fx.`）：`chain`（7色の印の数。銀=1、青=2、緑=4…虹=64）、`teaseCount`、`teaseText`、`teaseBack`。古い `from`・`to`（0=銀・1=金・2=虹）だけがある人は、その範囲の銀・金・虹の組み合わせに読み替える
- 画面の文・コメント・コミットは日本語。コミットの最後に `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`。動的な TMP フォント（`Resources/UI/*SDF.asset`）はコミットしない
- 見た目に触れる作業（Task 4・5）の前に frontend-design を読み込む
- Unity の裏起動は同時に1つだけ。push と TestFlight はユーザーに確認してから

## Review Focus

1. **古い設定の人**：前の版で「金だけ」「銀→虹」などを選んでいた人が、新しい版でも同じ流れで見える（Task 3 の読み替えのテスト）
2. **チラ見せ 0 文字**：暗転からすぐ光の放射 → 白の閃光 → ドン。真っ暗のまま止まったり、文字が出たりしない（Task 2 のテスト、Task 4 の app-check の絵）
3. **1文字の言葉に文字数 12**：1文字だけ見せる。空の文字を探して落ちない（Task 2 のテスト）
4. **虹を含まない組み合わせ・虹だけ・1色だけ**：切り裂きや回転が起きない／虹だけは着地のあと回転／1色だけは昇格なしで余韻（Task 2 のテスト）
5. **最後のメダルを外そうとする**：外れない。少なくとも1色は残る（Task 1 のテスト、Task 5 の画面）

---

## ファイルの地図

| ファイル | 役目 |
|---|---|
| Modify `Core/PromoTimeline.cs` | `Tier` を7段に。`PromoPlan`（昇格の時刻の並び）・`TeasePlan`（チラ見せの並び） |
| Create `Core/TierColors.cs` | 色の格ごとの文字の芯・背景の種類と色相・名前。`Chain`（組み合わせの印・並び・押したとき・文字） |
| Modify `Core/EffectSettings.cs` | 新しい4つの設定と保存・読み替え。古い From/To/FirstText/FirstBack/FirstColors は Task 6 で消す |
| Modify `Core/AppRules.cs` | `RangePicker` を消す（Task 5） |
| Tests: `Tests/Editor/TierColorsTests.cs`（新）・`PromoTimelineTests.cs`（書き直し）・`EffectSettingsTests.cs`・`AppRulesTests.cs` | |
| Modify `Shaders/PachiBurst.shader` | 青の背景の作りに `_Desat`（色を抜いて灰白にする） |
| Modify `Scripts/PachiText.cs` | 見た目を `Tier` で選ぶ（青〜赤は銀に色を乗せる） |
| Modify `Scripts/PachiStage.cs` | チラ見せ（数・色・0文字）、昇格の段、背景の種類を新しい計算でつなぐ |
| Modify `Scripts/UI/UiArt.cs` | メダルを7色に |
| Modify `Scripts/UI/SettingsScreen.cs` | 昇格の色（7枚）・チラ見せの文字数（◀ ▶）・チラ見せの文字色と背景（7色）。最初の色の項目を消す |
| Modify `Scripts/PachiApp.cs` | 組み立て直しの印に新しい設定、設定を開くとき言葉の文字数を渡す |
| Modify `Editor/PachiMovie.cs`・`movie.sh` | 昇格の色を文字で渡す（`PACHI_CHAIN`） |
| Modify `Editor/PachiAppCheck.cs`・`Editor/PachiBuild.cs` | 絵の確かめを新しい設定に、アイコンの時刻を新しい計算に |

---

### Task 1: 色の格を7段にし、色の作り方と組み合わせを Core に

**Files:**
- Modify: `Core/PromoTimeline.cs`（`enum Tier`）、`Core/AppRules.cs`（`RangePicker`）、`Scripts/UI/SettingsScreen.cs`（メダル3枚の並びが `(Tier)i` で数えている所だけ）
- Create: `Core/TierColors.cs`
- Test: `Tests/Editor/TierColorsTests.cs`、`Tests/Editor/AppRulesTests.cs`

**Interfaces:**
- Produces:
  - `public enum Tier { Silver = 0, Blue = 1, Green = 2, Purple = 3, Red = 4, Gold = 5, Rainbow = 6 }`
  - `public enum BackKind { Gray, Hue, Fire, Rainbow }`
  - `public static class TierColors`：`string TextBase(Tier t)`（青・緑・紫・赤だけ色、ほかは null）、`BackKind Back(Tier t)`、`float BackHue(Tier t)`（青 0・緑 -85・紫 65・赤 125、ほかは 0）、`string Name(Tier t)`（「銀」「青」「緑」「紫」「赤」「金」「虹」）
  - `public static class Chain`：`Tier[] Default`（銀・金・虹）、`int ToMask(IEnumerable<Tier>)`、`Tier[] FromMask(int mask)`（格の順。空なら `Default`）、`Tier[] Toggle(Tier[] chain, Tier t)`（入っていれば外す。ただし最後の1色は外さない。入っていなければ足す。結果はいつも格の順）、`string Label(Tier[] chain)`（「緑 → 赤 → 虹」、1色なら「緑だけ」）
  - `RangePicker.FromDrag(a, b)` は、メダルの位置 0・1・2 を銀・金・虹に読み替える（今の3枚の画面を Task 5 まで動かすため）

- [ ] **Step 1: 失敗するテストを書く**（`TierColorsTests`）

```csharp
[Test] public void 格の順() => CollectionAssert.AreEqual(new[] { Tier.Silver, Tier.Blue, Tier.Green, Tier.Purple, Tier.Red, Tier.Gold, Tier.Rainbow },
    (Tier[])System.Enum.GetValues(typeof(Tier)));
[Test] public void 文字の芯()
{
    Assert.IsNull(TierColors.TextBase(Tier.Silver)); Assert.IsNull(TierColors.TextBase(Tier.Gold)); Assert.IsNull(TierColors.TextBase(Tier.Rainbow));
    Assert.AreEqual("#2A7BFF", TierColors.TextBase(Tier.Blue)); Assert.AreEqual("#19D14A", TierColors.TextBase(Tier.Green));
    Assert.AreEqual("#A040FF", TierColors.TextBase(Tier.Purple)); Assert.AreEqual("#FF2030", TierColors.TextBase(Tier.Red));
}
[Test] public void 背景()
{
    Assert.AreEqual(BackKind.Gray, TierColors.Back(Tier.Silver)); Assert.AreEqual(BackKind.Fire, TierColors.Back(Tier.Gold));
    Assert.AreEqual(BackKind.Rainbow, TierColors.Back(Tier.Rainbow)); Assert.AreEqual(BackKind.Hue, TierColors.Back(Tier.Red));
    Assert.AreEqual(0f, TierColors.BackHue(Tier.Blue)); Assert.AreEqual(-85f, TierColors.BackHue(Tier.Green));
    Assert.AreEqual(65f, TierColors.BackHue(Tier.Purple)); Assert.AreEqual(125f, TierColors.BackHue(Tier.Red));
}
[Test] public void 名前() => Assert.AreEqual("銀青緑紫赤金虹", string.Concat(System.Array.ConvertAll((Tier[])System.Enum.GetValues(typeof(Tier)), TierColors.Name)));
[Test] public void 印と並び()
{
    Assert.AreEqual(1 + 32 + 64, Chain.ToMask(Chain.Default));
    CollectionAssert.AreEqual(new[] { Tier.Green, Tier.Red, Tier.Rainbow }, Chain.FromMask(4 + 16 + 64));
    CollectionAssert.AreEqual(Chain.Default, Chain.FromMask(0));
    CollectionAssert.AreEqual(new[] { Tier.Silver, Tier.Gold, Tier.Rainbow }, Chain.Default);
}
[Test] public void 押すと入る_いつも格の順() =>
    CollectionAssert.AreEqual(new[] { Tier.Green, Tier.Red, Tier.Rainbow }, Chain.Toggle(new[] { Tier.Green, Tier.Rainbow }, Tier.Red));
[Test] public void 押すと外れる() => CollectionAssert.AreEqual(new[] { Tier.Gold }, Chain.Toggle(new[] { Tier.Gold, Tier.Rainbow }, Tier.Rainbow));
[Test] public void 最後の1色は外れない() => CollectionAssert.AreEqual(new[] { Tier.Gold }, Chain.Toggle(new[] { Tier.Gold }, Tier.Gold));
[Test] public void 文字()
{
    Assert.AreEqual("緑 → 赤 → 虹", Chain.Label(new[] { Tier.Green, Tier.Red, Tier.Rainbow }));
    Assert.AreEqual("緑だけ", Chain.Label(new[] { Tier.Green }));
}
```

  `AppRulesTests.なぞった向きに関係なく小さい方がはじまり` は今の期待値（銀・金・虹）のまま通ること。

- [ ] **Step 2: `sh run-tests.sh` で失敗を見る** — 期待：`TierColors`・`Chain` が無い `error CS0103`
- [ ] **Step 3: `Tier` を7段にし、`TierColors.cs` を書き、`RangePicker` と設定画面のメダル3枚を `{ Tier.Silver, Tier.Gold, Tier.Rainbow }[i]` で数える形にする**
- [ ] **Step 4: `sh run-tests.sh`・`sh app-check.sh` が通るのを見る**（今の演出と画面は変わらない）
- [ ] **Step 5: コミット**「色の格を銀・青・緑・紫・赤・金・虹の7段にし、色の作り方と組み合わせを足す」

---

### Task 2: 昇格の時刻の並び（PromoPlan）とチラ見せの並び（TeasePlan）

**Files:**
- Modify: `Core/PromoTimeline.cs`
- Test: `Tests/Editor/PromoTimelineTests.cs`（今の `Plan(from, to)` のテストは、同じ意味の組み合わせの `Plan(chain)` に書き直す）

**Interfaces:**
- Consumes: Task 1 の `Tier`
- Produces:

```csharp
public readonly struct PromoStep { public readonly Tier To; public readonly float WhiteAt; public float SnapAt => WhiteAt + PromoTimeline.WhiteGold; }
public class PromoPlan
{
    public Tier Land { get; }                 // 着地の色（組み合わせの最初の色）
    public PromoStep[] Steps { get; }         // 虹の特別な形の手前までの昇格（白飛び→切り替え）
    public float SlashAt, RainbowAt, SpinAt, End;   // 起きないものは +∞（End は必ず有限）
    public Tier TierAt(float t);              // その時刻の色（虹に切り替わったら虹）
    public int SnappedCount(float t);         // その時刻までに切り替わった段の数
    public Tier Last { get; }                 // 最後の色
}
public static PromoPlan PromoTimeline.Plan(float impactEnd, IReadOnlyList<Tier> chain);
public static float PromoTimeline.HoldAfter(int snapped);   // snapped 段目の切り替えのあと、次の出来事まで：max(0.7, 1.7 × 0.85^(snapped-1))。0 段（着地のあと）は GoldHold 1.7

public readonly struct TeaseSlot { public readonly float At, Len; }
public static class TeasePlan
{
    public static TeaseSlot[] Plan(float start, int setting, int wordChars);   // 数 = clamp(min(setting, wordChars), 0, 12)
    public static float End(float start, TeaseSlot[] slots);                  // 光の放射の始まり（0 文字なら start）
}
```

  並べ方（体の決まらない所）：
  - 着地のあと最初の白飛び：`impactEnd + GoldDelay`（1.3）。その後の白飛び・切り裂きは、直前の切り替え（`SnapAt`）＋ `HoldAfter(切り替えた段の数)`
  - 虹の特別な形が起きるのは「最後が虹で、組み合わせが2色以上」のとき。そのときの `Steps` は、2色目から「虹の1つ手前」まで。切り裂き＝最後の切り替え（段が無ければ `impactEnd`）＋ `HoldAfter(段の数)`。灰色のタメと回転は今のまま
  - 1色だけで虹なら `SpinAt = impactEnd + LandSpinDelay`
  - `End` ＝ 最後の出来事（回転の終わり・最後の切り替え・着地）＋ `Linger`
  - チラ見せ：k 文字目の長さ `k<=3 ? max(0.55, 0.9-0.15k) : max(0.45, 0.55-0.05(k-3))`、k 文字目の前の暗転（k≥1）`max(0.2, 0.6-0.1k)`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
const float Inf = float.PositiveInfinity;
static Tier[] C(params Tier[] t) => t;
[Test] public void 既定は今と同じ時刻()   // 銀→金→虹（今の 銀から虹_今の流れと同じ と同じ数）
{
    var p = PromoTimeline.Plan(10, C(Tier.Silver, Tier.Gold, Tier.Rainbow));
    Assert.AreEqual(1, p.Steps.Length); Assert.AreEqual(Tier.Gold, p.Steps[0].To);
    Assert.AreEqual(11.3f, p.Steps[0].WhiteAt, 1e-4f); Assert.AreEqual(13.12f, p.SlashAt, 1e-4f);
    Assert.AreEqual(14.47f, p.RainbowAt, 1e-4f); Assert.AreEqual(14.47f, p.SpinAt, 1e-4f); Assert.AreEqual(18.12f, p.End, 1e-4f);
}
[Test] public void 銀だけ() { var p = PromoTimeline.Plan(10, C(Tier.Silver)); Assert.AreEqual(0, p.Steps.Length); Assert.AreEqual(Inf, p.SlashAt); Assert.AreEqual(11.8f, p.End, 1e-4f); }
[Test] public void 銀から金() => Assert.AreEqual(13.22f, PromoTimeline.Plan(10, C(Tier.Silver, Tier.Gold)).End, 1e-4f);
[Test] public void 金から虹() { var p = PromoTimeline.Plan(10, C(Tier.Gold, Tier.Rainbow)); Assert.AreEqual(11.7f, p.SlashAt, 1e-4f); Assert.AreEqual(16.7f, p.End, 1e-4f); }
[Test] public void 虹だけは着地のあと回る() { var p = PromoTimeline.Plan(10, C(Tier.Rainbow)); Assert.AreEqual(10.5f, p.SpinAt, 1e-4f); Assert.AreEqual(Inf, p.SlashAt); Assert.AreEqual(14.15f, p.End, 1e-4f); }
[Test] public void 虹を含まなければ切り裂かない() { var p = PromoTimeline.Plan(10, C(Tier.Green, Tier.Red)); Assert.AreEqual(Inf, p.SlashAt); Assert.AreEqual(Inf, p.SpinAt); Assert.AreEqual(Tier.Red, p.Last); }
[Test] public void 段が進むほど間が詰まり最短を下回らない()
{
    var p = PromoTimeline.Plan(10, C(Tier.Silver, Tier.Blue, Tier.Green, Tier.Purple, Tier.Red, Tier.Gold, Tier.Rainbow));
    Assert.AreEqual(5, p.Steps.Length);
    for (int i = 1; i < p.Steps.Length; i++)
    {
        float gap = p.Steps[i].WhiteAt - p.Steps[i - 1].SnapAt;
        Assert.That(gap, Is.GreaterThanOrEqualTo(0.7f - 1e-4f));
        if (i > 1) Assert.That(gap, Is.LessThanOrEqualTo(p.Steps[i - 1].WhiteAt - p.Steps[i - 2].SnapAt + 1e-4f));
    }
    Assert.AreEqual(1.7f, PromoTimeline.HoldAfter(1), 1e-4f); Assert.AreEqual(1.445f, PromoTimeline.HoldAfter(2), 1e-4f); Assert.AreEqual(0.7f, PromoTimeline.HoldAfter(9), 1e-4f);
}
[Test] public void どの組み合わせでも時刻は前から順()   // 1〜127 の印すべて
{
    for (int m = 1; m < 128; m++)
    {
        var p = PromoTimeline.Plan(10, Chain.FromMask(m));
        float prev = 10;
        foreach (var s in p.Steps) { Assert.That(s.WhiteAt, Is.GreaterThan(prev)); prev = s.SnapAt; }
        if (!float.IsInfinity(p.SlashAt)) { Assert.That(p.SlashAt, Is.GreaterThan(prev)); Assert.That(p.RainbowAt, Is.GreaterThan(p.SlashAt)); }
        Assert.That(p.End, Is.GreaterThan(prev)); Assert.IsFalse(float.IsInfinity(p.End));
        Assert.AreEqual(Chain.FromMask(m)[^1], p.Last);
    }
}
[Test] public void その時刻の色()
{
    var p = PromoTimeline.Plan(10, C(Tier.Green, Tier.Red, Tier.Rainbow));
    Assert.AreEqual(Tier.Green, p.TierAt(10.5f)); Assert.AreEqual(Tier.Red, p.TierAt(p.Steps[0].SnapAt + 0.01f));
    Assert.AreEqual(Tier.Rainbow, p.TierAt(p.RainbowAt + 0.01f)); Assert.AreEqual(1, p.SnappedCount(p.Steps[0].SnapAt + 0.01f));
}
// チラ見せ
[Test] public void チラ見せは今と同じ4文字() { var s = TeasePlan.Plan(2, 12, 4); Assert.AreEqual(4, s.Length);
    Assert.AreEqual(2f, s[0].At, 1e-4f); Assert.AreEqual(0.9f, s[0].Len, 1e-4f); Assert.AreEqual(3.4f, s[1].At, 1e-4f); Assert.AreEqual(0.55f, s[3].Len, 1e-4f); }
[Test] public void チラ見せは全部まで速くなり最短を下回らない() { var s = TeasePlan.Plan(0, 12, 12); Assert.AreEqual(12, s.Length);
    Assert.AreEqual(0.45f, s[11].Len, 1e-4f); Assert.AreEqual(0.2f, s[11].At - (s[10].At + s[10].Len), 1e-4f); }
[Test] public void 言葉より多い数は言葉の文字数() => Assert.AreEqual(1, TeasePlan.Plan(0, 12, 1).Length);
[Test] public void 文字数0なら無しで放射はすぐ() { var s = TeasePlan.Plan(2, 0, 6); Assert.AreEqual(0, s.Length); Assert.AreEqual(2f, TeasePlan.End(2, s)); }
[Test] public void 放射の始まりは最後の文字の終わり() { var s = TeasePlan.Plan(2, 2, 6); Assert.AreEqual(s[1].At + s[1].Len, TeasePlan.End(2, s), 1e-4f); }
```

- [ ] **Step 2: 失敗を見る** — 期待：`PromoPlan`・`TeasePlan` が無い
- [ ] **Step 3: `PromoPlan`・`TeasePlan`・`HoldAfter` を書く**。古い `Plan(impactEnd, from, to)` と `PromoTimes` は Task 4 で PachiStage を移すまで残す
- [ ] **Step 4: `sh run-tests.sh` が通るのを見る**
- [ ] **Step 5: コミット**「昇格の色の組み合わせから時刻を並べる計算と、チラ見せの並びを足す」

---

### Task 3: 設定（昇格の色・チラ見せの文字数・文字色・背景）と保存

**Files:**
- Modify: `Core/EffectSettings.cs`
- Test: `Tests/Editor/EffectSettingsTests.cs`

**Interfaces:**
- Consumes: Task 1 の `Tier`・`Chain`
- Produces：`EffectSettings` に `Tier[] Chain = Chain.Default`（いつも格の順・1色以上）、`int TeaseCount = 12`、`Tier TeaseText = Tier.Silver`、`Tier TeaseBack = Tier.Blue`。`Clone()` は `Chain` の配列も写す。古い `From`・`To`・`FirstText`・`FirstBack`・`FirstColors`・`FirstColorsApply` は Task 6 まで残す（使わない）

- [ ] **Step 1: 失敗するテストを書く**（キーは `test.fx.` ＋ `chain`・`teaseCount`・`teaseText`・`teaseBack`・`from`・`to` を TearDown で消す）
  - `何も保存していなければ既定`：`Chain` が 銀・金・虹、`TeaseCount` 12、`TeaseText` 銀、`TeaseBack` 青
  - `保存して読み戻すと同じ`：`Chain = {緑, 赤, 虹}`、`TeaseCount = 3`、`TeaseText = 金`、`TeaseBack = 紫`
  - `古いはじまりとおわりを読み替える`：`from=1, to=1` だけを保存 → `Chain == {金}`。`from=0, to=2` → `{銀, 金, 虹}`。`from=1, to=2` → `{金, 虹}`
  - `chain があれば古い値は見ない`：`chain=4`・`from=0`・`to=2` → `{緑}`
  - `壊れた値は既定`：`chain=0` → 既定、`chain=999` → 既定（128 以上は壊れている）、`teaseCount=-1`・`13` → 12、`teaseText=7` → 銀、`teaseBack=-1` → 青
  - `写しは別物`：`Clone()` の `Chain` を書き換えても元が変わらない
- [ ] **Step 2: 失敗を見る**
- [ ] **Step 3: `EffectSettings` に足し、`Load`・`Save`・`Clone` を直す**（古い from/to は 0→銀・1→金・2→虹 に読み替え、`Mathf.Min/Max` の範囲の銀・金・虹を並べる）
- [ ] **Step 4: 通るのを見る**
- [ ] **Step 5: コミット**「設定に昇格の色・チラ見せの文字数・文字色・背景を足し、古い設定を読み替える」

---

### Task 4: 演出を新しい計算につなぐ（チラ見せ・昇格の段・背景）

**Files:**
- Modify: `Shaders/PachiBurst.shader`、`Scripts/PachiText.cs`、`Scripts/PachiStage.cs`、`Editor/PachiAppCheck.cs`、`Editor/PachiBuild.cs`（`MakeIcon` の時刻）

**Interfaces:**
- Consumes: Task 1 の `TierColors`、Task 2 の `PromoPlan`・`TeasePlan`、Task 3 の設定
- Produces：`PachiStage.Plan`（`PromoPlan`）を公開（`GoldAt`・`SlashAt`・`RainbowAt`・`SpinAt` は `Plan` から読む形に）。`PachiText.SetLook(Rig, Tier from, Tier to, float wipe, float glow)`（`Finish` と `silverTint` の組は中で決める）、`PachiText.Create(lines, Tier, font)`

  - シェーダー：`_Desat`（0〜1）。`_Mode` が青（2）のとき、色相を回したあとで `c = lerp(c, dot(c, (0.3,0.59,0.11)) * (0.85, 0.9, 1.0), _Desat)`（白っぽい冷たい灰）
  - 背景の決め方（`BgOf(Tier, float t)`）：Gray → mode 2・desat 1、Hue → mode 2・hue、Fire → mode 0、Rainbow → 今の切り替え（最後の 0.4 秒は虹）
  - チラ見せ：`TeasePlan.Plan(TeaseStart, settings.TeaseCount, 言葉の文字数)` で並べ、1文字ずつの立体を `settings.TeaseText` の色で作る。背景は `settings.TeaseBack` の `BgOf` を今の暗さ（`bgGain 0.2`・`bgCore 0.8`）で。0文字のときは、暗転のあと `RayStart` から基本の位置のカメラで光の放射だけを出す
  - 昇格：`fin = plan.TierAt(t)`。前ぶれ・白飛びは「次の段」の `WhiteAt`、跳ね・閃光は「直前の段」の `SnapAt` から（今の `PromoAt` を2つの段に分けて使う）。切り替えのあとの光の筋（今の金のあと）は、どの段のあとにも出し、銀以外の色で着地したときは着地のあとにも出す。閃光の色は、銀なら白・ほかは今の暖かい白
  - 昨日の `firstTint`・`firstHue` は消す

- [ ] **Step 1: frontend-design を読み込む**
- [ ] **Step 2: app-check の確かめを書き換える（まだ失敗する）**
  - 昨日の `first_*.png` と `PachiStage.GoldAt` を使う確かめを、新しい形に：チラ見せ（文字色 金・背景 紫、2文字目のまん中）`tease_gold_purple.png`、（文字色 虹・背景 銀）`tease_rainbow_silver.png`／ドン後の各色（銀・青・緑・紫・赤・金）の着地の絵を1枚に並べた `land_tiers.png`
  - 「虹で終わる演出は最後の 0.4 秒が虹」を、組み合わせ `{銀,金,虹}`・`{緑,赤,虹}`・`{虹}` で
  - チラ見せ 0 文字で、`RayStart` の少しあとが真っ黒でない（光の放射が出ている）
  - 12文字の言葉（`脳汁ドバドバ`・`激アツ確定だ`）で文字数 12：チラ見せの立体が12個作られ、12個目のまん中の時刻に12個目だけが見えている
- [ ] **Step 3: 失敗を見る**
- [ ] **Step 4: シェーダー・PachiText・PachiStage を書き換える**。`PromoTimeline.Plan(impactEnd, from, to)` と `PromoTimes` はここで消す
- [ ] **Step 5: `sh app-check.sh`・`sh run-tests.sh` が通るのを見る。絵を Read で見る**（色の質感が金属のまま・銀の背景が冷たい灰・チラ見せの背景が暗がりのまま）
- [ ] **Step 6: 既定の流れを短い動画で確かめる** — `sh movie.sh 0 -1 quick` で `captures/movie_teiji_sheet.png` を見て、今と同じ流れ（銀→金→虹）であること
- [ ] **Step 7: コミット**「演出を新しい昇格の色とチラ見せの設定につなぐ（7色・全部の文字・0文字）」

---

### Task 5: 設定画面（昇格の色の7枚・チラ見せの文字数・文字色・背景）

**Files:**
- Modify: `Scripts/UI/UiArt.cs`（`Medal(Tier)` を7色に。青〜赤は銀のメダルの明るさに `TierColors.TextBase` の色を乗せる）、`Scripts/UI/SettingsScreen.cs`、`Scripts/PachiApp.cs`、`Core/AppRules.cs`（`RangePicker` と `MedalDrag` を消す）、`Tests/Editor/AppRulesTests.cs`（RangePicker のテストを消す）、`Editor/PachiAppCheck.cs`

**Interfaces:**
- Consumes: Task 1 の `Chain`・`TierColors`、Task 3 の設定
- Produces：`SettingsScreen.SetWordLength(int n)`（設定を開く前に PachiApp が呼ぶ。チラ見せの文字数の下の「（全部）」に使う）、`SettingsScreen.ChainLabel`（`TMP_Text`、確かめ用）

見た目（frontend-design を読んでから。今の銀の板と金の線の決まりのまま）：

```
昇格の色
 ◎ ○ ◎ ○ ◎ ○ ◎        ← 7枚のメダル（直径 40、間隔 46）。点いたものは明るく、消えたものは暗く
 ━━━━━━━━━━━━━━━        ← 点いた最初のメダルから最後のメダルまで金の帯（後ろを通る）
 緑 → 赤 → 虹           ← Reggae One
 メダルを押して、通る色を選びます
チラ見せの文字数
   ◀    12    ▶         ← 銀の小さい板の矢印。数は Reggae One。0 は「なし」
       （全部）          ← 数が言葉の文字数以上のとき
チラ見せの文字色
 [銀][青][緑][紫][赤][金][虹]  ← 幅 44 の板7つ。字をその色で（銀は銀、金は金、虹は虹色のグラデーション）。選んだものは金の下線
チラ見せの背景
 [銀][青][緑][紫][赤][金][虹]
```

  - 色の字の板は、選んでも字の色を金に変えない（下線だけ）。今の `Choice` に「字の色を指定する」形を足す
  - 昨日の「最初の文字色」「最初の背景」とその説明、`FirstColorsEnabled` を消す
  - PachiApp：組み立て直しの印に `Chain.ToMask(settings.Chain)`・`TeaseCount`・`TeaseText`・`TeaseBack`、設定を開くとき `SetWordLength(言葉の文字数)`
- [ ] **Step 1: frontend-design を読み込む**
- [ ] **Step 2: app-check を書き換える（まだ失敗する）** — 設定画面の絵（既定・`{緑,赤,虹}`＋文字数 3＋文字色 金＋背景 紫）。`ChainLabel.text` が「緑 → 赤 → 虹」。メダルを押す道（`Chain.Toggle` と同じ結果になる）を1回試す。昨日の `FirstColorsEnabled` の確かめを消す
- [ ] **Step 3: 失敗を見る**
- [ ] **Step 4: UiArt・SettingsScreen・PachiApp・AppRules を書き換える**
- [ ] **Step 5: `sh app-check.sh`・`sh run-tests.sh` が通り、絵を見る**（7枚と7つの板が iPhone SE の幅に収まる・重ならない）
- [ ] **Step 6: コミット**「設定画面に昇格の色（7枚）・チラ見せの文字数・文字色・背景を足し、最初の色の項目を消す」

---

### Task 6: 片付けと確認の道具

**Files:**
- Modify: `Core/EffectSettings.cs`（古い `From`・`To`・`FirstText`・`FirstBack`・`FirstColors`・`FirstColorsApply` を消す。読み替えは残す）、`Tests/Editor/EffectSettingsTests.cs`（昨日の最初の色のテストを消す）、`Editor/PachiMovie.cs`・`movie.sh`（`PACHI_RANGE` を `PACHI_CHAIN` に。文字 S=銀 B=青 G=緑 P=紫 R=赤 K=金 N=虹、例 `GRN`）
- [ ] **Step 1: 古いものを消し、`sh run-tests.sh` と `sh app-check.sh` が通るのを見る**
- [ ] **Step 2: `sh movie.sh 0 -1 quick SBGPRKN` で7色すべての流れの一覧（`captures/movie_chain_SBGPRKN_sheet.png`）を見る** — 色が格の順に変わり、最後に虹の特別な形
- [ ] **Step 3: コミット**「古い昇格の範囲と最初の色の設定を片付け、書き出しの道具を昇格の色に合わせる」

---

### Task 7: 組み立てて iPhone で確かめる

- [ ] **Step 1: superpowers:verification-before-completion を読み、`sh run-tests.sh`・`sh app-check.sh` を流す**
- [ ] **Step 2: push と TestFlight をユーザーに確認** → `gh workflow run ios-testflight.yml` → 見張る（全体で約1時間。pod install が長い）
- [ ] **Step 3: iPhone で確かめてもらう項目** — 設定の7枚のメダル・文字数の矢印・色の板／緑→赤→虹などの流れ／チラ見せ 0・3・全部／チラ見せの色／前に選んでいた昇格の範囲が引き継がれている
- [ ] **Step 4: 進み具合のメモに書く**（movie.sh の文字の決まりも）
