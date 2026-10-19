# 作る順番4「広告（バナー・ごほうび）と課金3つ・購入の復元」作業計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 下にいつもバナー広告、「書き出す」の前にごほうび広告（ネットが無い・読めないなら書き出せない知らせ）を出し、買い切り3つ（印を消す300円・広告を消す300円・セット500円）と購入の復元を設定画面から使えるようにして、TestFlight に届ける。組み立て中はテスト広告。

**Architecture:** 「買ったもの → 印・広告・見せる商品」と「書き出しの前に何をするか」は `PachiMoji.Core` の小さな関数（`Entitlements`・`ExportGate`）に切り離して EditMode テストで確かめる。課金は Unity IAP 5 を包む `Shop`、広告は Google Mobile Ads Unity Plugin を包む `Ads` が持ち、PachiApp・画面はその2つと Core の判定だけを見る。広告の追跡の許可（ATT）は自前の小さな Objective-C（`PachiTracking.mm`）。

**Tech Stack:** Unity 6000.6.6f1・Unity IAP `com.unity.purchasing` 5.4.4・Google Mobile Ads Unity Plugin `com.google.ads.mobile` 11.5.0（OpenUPM）＋ External Dependency Manager（CocoaPods）・Objective-C（AppTrackingTransparency）・NUnit

**Spec:** `docs/superpowers/specs/2026-10-18-app-release-design.md`（「流れ」3、「設定画面」の買い物、「中の作り › 広告」「課金」）

## Global Constraints

- **お金はかけない**：どれも無料の部品。AdMob はほかのアプリと同じアカウント（`pub-7017663942238206`）
- 版は固定：`com.unity.purchasing` **5.4.4**、`com.google.ads.mobile` **11.5.0**（11.6.0 は出て1週間なので見送る）、External Dependency Manager は 11.5.0 が求める `com.google.external-dependency-manager` **1.2.187 以上**（OpenUPM の 1.2.189 を入れる）
- 商品（どれも買い切り＝非消耗型）：

| 商品 | ID | 値段 | 効くこと |
|---|---|---|---|
| 印を消す | `com.maosundev.noujiru.nomark` | 300円 | 動画の印が消える |
| 広告を消す | `com.maosundev.noujiru.noads` | 300円 | バナーと書き出し前の広告が消え、ネットなしでも書き出せる |
| セット | `com.maosundev.noujiru.set` | 500円 | 上の2つとも |

- セットは、どちらも買っていない人にだけ見せる。片方を買った人には残り1つだけを見せる
- 組み立て中はテスト広告（Google のテスト用の広告枠）、ストアに出す版だけ本物（天落杯と同じく「組み立てるときに production の印を付けた版だけ本物」）
- 広告の追跡の許可（ATT）を初回に聞く。許可されなくても広告は出す（追跡しない広告になる）
- 文は日本語、専門用語にはひとこと説明。画面の色は `UiTheme`（赤は PUSH だけ、金は「オン・買ったもの」だけ）
- コミットは自分の変えたファイルだけ。動的な TMP フォント（`Resources/UI/*SDF.asset`）は入れない。メッセージの最後に `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`
- Unity の裏起動は同時に1つだけ。手元の Windows に iOS の部品は無い（`.mm` と `#if UNITY_IOS` の中は TestFlight の組み立てで初めて確かめる）
- push と TestFlight はユーザーに確認してから（「push は後でまとめて」と言われている。順番4の最後にまとめて聞く）
- 秘密・登録・支払いはユーザーがやる：AdMob でアプリと広告枠2つ（バナー・ごほうび）を作る、App Store Connect で3つの商品を登録し「有料App契約」を結ぶ。どれも Task 6 でまとめて頼む（テスト広告と TestFlight の試し買い（サンドボックス）は、商品の登録が済めば動く）

## Review Focus

1. **ネットなしで開いた、広告を消した人**：買ったものが手元に残っていて、バナーは出ず、書き出しもすぐできる（Task 1 の保存と読み込みのテスト、Task 3 の起動時の読み込み）
2. **ごほうび広告を途中で閉じた**：書き出さず、「最後まで見ると書き出せます」と出す。もう一度押せば見直せる（Task 1 の ExportGate のテスト、Task 5 の流れ）
3. **買ったあとアプリが落ちた・再起動した**：次に開いたとき同じ買い物がもう一度届く。二重に数えず、確実に効く（Task 3：届いたら記録してから確定する。記録は「持っているか」なので何度届いても同じ）
4. **保護者の承認待ち（Ask to Buy）**：まだ効かせず「保護者の承認を待っています」と出す（Task 3・Task 5）
5. **買った瞬間**：アプリを開き直さなくても、バナーが消え・印が消え・設定の商品の並びが変わる（Task 5 の app-check の絵：買っていない・片方・両方）

---

## ファイルの地図

| ファイル | 役目 |
|---|---|
| Create `PachiMoji/Assets/PachiMoji/Core/Entitlements.cs` | 商品 ID、買ったもの → 印・広告・見せる商品、端末への保存 |
| Create `PachiMoji/Assets/PachiMoji/Core/ExportGate.cs` | 書き出しの前に何をするか（すぐ書く・広告・待つ・ネットが要る）と知らせの文 |
| Create `PachiMoji/Assets/PachiMoji/Tests/Editor/EntitlementsTests.cs`、`ExportGateTests.cs` | 上のテスト |
| Modify `PachiMoji/Packages/manifest.json` | OpenUPM の登録先、3つの部品 |
| Create `PachiMoji/ProjectSettings/GvhProjectSettings.xml` | External Dependency Manager の設定（Linux では pod install しない・xcworkspace を作る） |
| Create `PachiMoji/Assets/PachiMoji/Scripts/Shop/Shop.cs` | Unity IAP を包む：つなぐ・値段・買う・復元・届いたら記録して確定 |
| Create `PachiMoji/Assets/PachiMoji/Scripts/Ads/AdIds.cs`、`Ads.cs` | 広告枠の ID（テスト・本物）、同意（UMP）→ ATT → 始める、バナー、ごほうび |
| Create `PachiMoji/Assets/Plugins/iOS/PachiTracking.mm`（＋`.meta`） | ATT の許可を聞く |
| Modify `PachiMoji/Assets/PachiMoji/Scripts/UI/SettingsScreen.cs` | 「買い物」の項目（商品・値段・購入済み・復元・知らせ）、バナーのぶん下を空ける |
| Modify `PachiMoji/Assets/PachiMoji/Scripts/UI/MainScreen.cs` | 「書き出す」を押せない形と知らせ |
| Modify `PachiMoji/Assets/PachiMoji/Scripts/PachiApp.cs` | Shop・Ads をつなぐ。書き出しの前に ExportGate、印は Entitlements |
| Modify `PachiMoji/Assets/PachiMoji/Editor/PachiBuild.cs` | production の印（`-pachiProduction`）で `PACHI_PRODUCTION` を付ける。本物の ID が仮のままなら止める |
| Modify `PachiMoji/Assets/PachiMoji/Editor/PachiAppCheck.cs` | 部品・設定・ワークフローの確かめ、買い物の画面の絵 |
| Modify `.github/workflows/ios-testflight.yml` | production の入力、pod install と xcworkspace、Info.plist に ATT の文と AdMob のアプリ ID |

---

### Task 1: 買ったものと書き出しの前の判定（Core）

**Files:**
- Create: `Core/Entitlements.cs`、`Core/ExportGate.cs`
- Test: `Tests/Editor/EntitlementsTests.cs`、`Tests/Editor/ExportGateTests.cs`
（パスは `PachiMoji/Assets/PachiMoji/` の下）

**Interfaces:**
- Produces:
  - `public static class ProductIds { const string NoMark = "com.maosundev.noujiru.nomark", NoAds = "com.maosundev.noujiru.noads", Set = "com.maosundev.noujiru.set"; static readonly string[] All = { NoMark, NoAds, Set }; }`
  - `public class Entitlements`：`Entitlements(IEnumerable<string> owned)`、`bool Owns(string id)`、`bool ShowWatermark`、`bool ShowAds`、`string[] ShopItems`（見せる商品の ID を並べる順）、`Entitlements With(string id)`（1つ足した新しいもの）、`void Save(string key = "ent.owned")`、`static Entitlements Load(string key = "ent.owned")`
  - `public enum AdState { Loading, Ready, Failed }`、`public enum GateAction { ExportNow, ShowAd, Wait, NeedNetwork }`
  - `public static class ExportGate`：`GateAction Decide(bool showAds, bool online, AdState ad)`、`string Message(GateAction a)`、`const string NotWatched = "最後まで見ると書き出せます"`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
// EntitlementsTests（PlayerPrefs は "test.ent" に保存して TearDown で消す）
[Test] public void 何も買っていなければ印も広告も出してセットを含む3つを見せる()
{
    var e = new Entitlements(new string[0]);
    Assert.IsTrue(e.ShowWatermark); Assert.IsTrue(e.ShowAds);
    CollectionAssert.AreEqual(new[] { ProductIds.NoMark, ProductIds.NoAds, ProductIds.Set }, e.ShopItems);
}
[Test] public void 印を消すを買えば印だけ消え残りの1つだけ見せる()
{
    var e = new Entitlements(new[] { ProductIds.NoMark });
    Assert.IsFalse(e.ShowWatermark); Assert.IsTrue(e.ShowAds);
    CollectionAssert.AreEqual(new[] { ProductIds.NoAds }, e.ShopItems);
}
[Test] public void 広告を消すを買えば広告だけ消え残りの1つだけ見せる()
{
    var e = new Entitlements(new[] { ProductIds.NoAds });
    Assert.IsTrue(e.ShowWatermark); Assert.IsFalse(e.ShowAds);
    CollectionAssert.AreEqual(new[] { ProductIds.NoMark }, e.ShopItems);
}
[Test] public void セットは両方に効き何も見せない()
{
    var e = new Entitlements(new[] { ProductIds.Set });
    Assert.IsFalse(e.ShowWatermark); Assert.IsFalse(e.ShowAds);
    CollectionAssert.IsEmpty(e.ShopItems);
}
[Test] public void 両方を別々に買っても何も見せない() =>
    CollectionAssert.IsEmpty(new Entitlements(new[] { ProductIds.NoMark, ProductIds.NoAds }).ShopItems);
[Test] public void 知らないIDは無視する() => Assert.IsTrue(new Entitlements(new[] { "x.y" }).ShowAds);
[Test] public void 同じものが何度届いても同じ() =>
    CollectionAssert.AreEqual(new[] { ProductIds.NoMark }, new Entitlements(new string[0]).With(ProductIds.NoAds).With(ProductIds.NoAds).ShopItems);
[Test] public void 保存して読み戻すと同じ()
{
    new Entitlements(new[] { ProductIds.NoAds }).Save("test.ent");
    var e = Entitlements.Load("test.ent");
    Assert.IsTrue(e.Owns(ProductIds.NoAds)); Assert.IsFalse(e.Owns(ProductIds.NoMark));
}
[Test] public void 何も保存していなければ何も持っていない() => Assert.IsTrue(Entitlements.Load("test.ent").ShowAds);

// ExportGateTests
[Test] public void 広告を消した人はネットが無くてもすぐ書き出す()
{
    Assert.AreEqual(GateAction.ExportNow, ExportGate.Decide(false, false, AdState.Failed));
    Assert.AreEqual(GateAction.ExportNow, ExportGate.Decide(false, true, AdState.Loading));
}
[Test] public void 広告が読めていれば広告を見せる() => Assert.AreEqual(GateAction.ShowAd, ExportGate.Decide(true, true, AdState.Ready));
[Test] public void ネットが無ければネットが要る()
{
    Assert.AreEqual(GateAction.NeedNetwork, ExportGate.Decide(true, false, AdState.Ready));
    Assert.AreEqual(GateAction.NeedNetwork, ExportGate.Decide(true, false, AdState.Loading));
}
[Test] public void 広告が読めなければネットが要る() => Assert.AreEqual(GateAction.NeedNetwork, ExportGate.Decide(true, true, AdState.Failed));
[Test] public void 読み込み中は待つ() => Assert.AreEqual(GateAction.Wait, ExportGate.Decide(true, true, AdState.Loading));
[Test] public void 知らせの文()
{
    Assert.AreEqual("ネットにつなぐと書き出せます", ExportGate.Message(GateAction.NeedNetwork));
    Assert.AreEqual("広告を読み込んでいます。少し待ってから、もう一度「書き出す」を押してください", ExportGate.Message(GateAction.Wait));
    Assert.AreEqual("", ExportGate.Message(GateAction.ExportNow));
    Assert.AreEqual("", ExportGate.Message(GateAction.ShowAd));
    Assert.AreEqual("最後まで見ると書き出せます", ExportGate.NotWatched);
}
```

- [ ] **Step 2: `sh run-tests.sh` で失敗を見る** — 期待：`Entitlements`・`ExportGate` が無い `error CS0246`／`CS0103`
- [ ] **Step 3: 2つのファイルを書く** — `Entitlements` は持っている ID の集まり（知らない ID は捨てる）。`Set` を持っていれば NoMark・NoAds も持っている扱い。`ShopItems`：何も持っていなければ `{NoMark, NoAds, Set}`、片方だけなら残りの1つ、両方なら空。保存は `PlayerPrefs.SetString(key, string.Join(",", ids))` ＋ `Save()`
- [ ] **Step 4: `sh run-tests.sh` で通るのを見る** — 期待：failed="0"
- [ ] **Step 5: コミット**「買ったものの判定と書き出しの前の判定を足す（Entitlements・ExportGate）」

---

### Task 2: 部品を入れ、組み立ての道を広告・課金に合わせる

**Files:**
- Modify: `PachiMoji/Packages/manifest.json`
- Create: `PachiMoji/ProjectSettings/GvhProjectSettings.xml`
- Create: `PachiMoji/Assets/PachiMoji/Scripts/Ads/AdIds.cs`
- Modify: `Editor/PachiBuild.cs`、`Editor/PachiAppCheck.cs`、`.github/workflows/ios-testflight.yml`

**Interfaces:**
- Produces: `public static class AdIds { static string Banner; static string Rewarded; static bool Production; }`（`#if PACHI_PRODUCTION` で本物、それ以外はテスト）。`AdIds.RealAppId`・`AdIds.TestAppId`（ワークフローと揃える文字列）
- テスト用の値（Google の公開しているテスト用）：アプリ ID `ca-app-pub-3940256099942544~1458002511`、バナー `ca-app-pub-3940256099942544/2934735716`、ごほうび `ca-app-pub-3940256099942544/1712485313`
- 本物の値：ユーザーが AdMob で作るまで `ca-app-pub-7017663942238206~TODO` などの仮。`PachiBuild` は production の組み立てで ID に `TODO` が残っていれば止める

- [ ] **Step 1: app-check に確かめを足す（まだ失敗する）**
  - `Packages/manifest.json` に `com.unity.purchasing: 5.4.4`・`com.google.ads.mobile: 11.5.0`・`com.google.external-dependency-manager: 1.2.189`、`scopedRegistries` に `https://package.openupm.com`（scopes `com.google`）
  - `ProjectSettings/GvhProjectSettings.xml` で iOS Resolver が「pod install しない（`Google.IOSResolver.CocoapodsInstallEnabled` = False）」「Podfile は作る」「つなぎ方は Xcode Workspace（`Google.IOSResolver.CocoapodsIntegrationMethod` = 2）」
  - ワークフローに `production` の入力、`pod install`、`-workspace iOS/Unity-iPhone.xcworkspace`、`NSUserTrackingUsageDescription`、`GADApplicationIdentifier`
  - `AdIds.Production == false`（手元はテスト広告）
- [ ] **Step 2: `sh app-check.sh` で失敗を見る**
- [ ] **Step 3: manifest に部品と登録先を足す**（Unity を起動すると取り込まれる。`packages-lock.json` もコミットする）
- [ ] **Step 4: `GvhProjectSettings.xml` を書く**（上の3つの値）
- [ ] **Step 5: `AdIds.cs` を書く**
- [ ] **Step 6: `PachiBuild.BuildIos` に production を足す** — コマンドに `-pachiProduction` があれば `PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.iOS, "PACHI_PRODUCTION")`、無ければ外す。production で `AdIds` の本物の ID に `TODO` が残っていれば例外で止める（「AdMob の本物の ID を入れてから」）
- [ ] **Step 7: ワークフローを直す**
  - `workflow_dispatch.inputs.production`（boolean・既定 false・説明「ストアに出す版（本物の広告）」）。Unity の段の `game-ci/unity-builder` に `customParameters: ${{ inputs.production && '-pachiProduction' || '' }}`
  - Mac の段：「Xcode の作りを広げる」のあと `cd iOS && pod install --repo-update`。archive は `-workspace iOS/Unity-iPhone.xcworkspace -scheme Unity-iPhone`
  - 「番号と申告を書き込む」に `NSUserTrackingUsageDescription`（文：`あなたに合った広告を出すために使います。許可しなくても、アプリはすべて使えます`）と、`GADApplicationIdentifier`（production ならユーザーの本物、そうでなければテスト用）を Delete→Add で書く
- [ ] **Step 8: `sh app-check.sh`・`sh run-tests.sh` が通るのを見る**（部品が入ってもコンパイルが通ること）
- [ ] **Step 9: コミット**「広告と課金の部品を入れ、組み立ての道を合わせる（テスト広告・pod install・production の印）」

---

### Task 3: 課金（Shop）

**Files:**
- Create: `Scripts/Shop/Shop.cs`
- Modify: `Editor/PachiAppCheck.cs`（コンパイルと、Shop が買ったものを保存する道の確かめ）

**Interfaces:**
- Consumes: Task 1 の `ProductIds`・`Entitlements`
- Produces:

```csharp
public enum ShopState { Connecting, Ready, Unavailable }
public class Shop
{
    public Shop(Entitlements saved);                    // 端末に残っていた買ったもので始める（ネットなしでも効く）
    public ShopState State { get; }
    public Entitlements Owned { get; }
    public string PriceOf(string id);                   // ストアの値段の文字（例「¥300」）。まだなら null
    public event System.Action Changed;                 // 状態・値段・買ったものが変わった
    public event System.Action<string> Notice;          // 画面に出す知らせ（買えなかった・承認待ち・復元の結果）
    public System.Threading.Tasks.Task Connect();       // UnityIAPServices.StoreController() → イベント登録 → Connect → FetchProducts・FetchPurchases
    public void Buy(string id);
    public void Restore();
    public void Grant(string id);                       // 買ったものに足して端末に保存し Changed（届いた買い物と手元確認の入口）
}
```

- [ ] **Step 1: app-check に足す（まだ失敗する）** — `new Shop(new Entitlements(new string[0]))` が `Connecting` で始まる、`Shop.Grant(ProductIds.NoAds)`（内部の付与の入口、`internal` ではなく `public` にしてテストから呼ぶ）で `Owned.ShowAds == false` になり `Entitlements.Load("ent.owned")` にも残る（確かめのあと元に戻す）
- [ ] **Step 2: `sh app-check.sh` で失敗を見る**
- [ ] **Step 3: `Shop.cs` を書く**（unity:implement-in-app-purchases の v5 の形どおり）
  - 3つの `ProductDefinition`（すべて `ProductType.NonConsumable`）
  - `Connect` の前にすべてのイベントを登録：`OnPurchasePending`・`OnPurchaseConfirmed`・`OnPurchaseFailed`・`OnPurchaseDeferred`・`OnStoreConnected`・`OnStoreDisconnected`・`OnProductsFetched`・`OnProductsFetchFailed`・`OnPurchasesFetched`・`OnPurchasesFetchFailed`
  - `OnPurchasePending`：商品 ID を取り出し `Grant(id)`（`Owned = Owned.With(id)` → `Owned.Save()` → `Changed`）→ 保存のあとで `ConfirmPurchase(order)`
  - `OnPurchasesFetched`：`ConfirmedOrders` の商品を全部 `Grant`
  - `OnPurchaseFailed`：ユーザーがやめたとき（`PurchaseFailureReason.UserCancelled`）は知らせを出さない。ほかは「買えませんでした。少し待ってから、もう一度お試しください」
  - `OnPurchaseDeferred`：「保護者の承認を待っています。承認されると自動で使えるようになります」
  - `Restore`：`RestoreTransactions` → 成功「購入を復元しました」（何も無ければ「復元できる購入はありませんでした」）、失敗「復元できませんでした。ネットにつないで、もう一度お試しください」
  - ストアにつながらない・商品が取れない → `Unavailable`（値段は出さず、ボタンは押せない）
- [ ] **Step 4: `sh app-check.sh`・`sh run-tests.sh` が通るのを見る**
- [ ] **Step 5: コミット**「課金（Unity IAP）の包みを作る：つなぐ・値段・買う・復元・届いたら記録して確定」

---

### Task 4: 広告（Ads）と ATT

**Files:**
- Create: `Scripts/Ads/Ads.cs`、`PachiMoji/Assets/Plugins/iOS/PachiTracking.mm`（＋`.meta`、iOS だけ・`-fobjc-arc`・`FrameworkDependencies: AppTrackingTransparency;`）
- Modify: `Editor/PachiAppCheck.cs`

**Interfaces:**
- Consumes: Task 2 の `AdIds`、Task 1 の `AdState`
- Produces:

```csharp
public class Ads
{
    public void Start(bool showAds);              // 同意（UMP）→ ATT → MobileAds.Initialize → showAds ならバナーとごほうびを読む
    public AdState Rewarded { get; }
    public void SetBanner(bool show);             // 広告を消したらすぐ false
    public void ShowRewarded(System.Action<bool> done);   // done(true)＝最後まで見た。閉じたら次を読み込み始める
}
```

  - `PachiTracking.mm`：`void PachiTracking_Request(void)` → `ATTrackingManager requestTrackingAuthorizationWithCompletionHandler`（すでに決まっていれば何もしない）→ main で `UnitySendMessage("PachiTrackingBridge", "OnTracking", "")`。C# 側は返事が来たら広告を始める（iPhone 以外はすぐ始める）
  - `MobileAds.RaiseAdEventsOnUnityMainThread = true`（広告の返事を Unity の流れの中で受ける）
  - バナー：`new BannerView(AdIds.Banner, AdSize.Banner, AdPosition.Bottom)`（320×50。下の 62 の場所に収まる）
  - ごほうび：`RewardedAd.Load(AdIds.Rewarded, new AdRequest(), …)`。失敗したら `Failed` にして 30 秒後に読み直す。`Show(reward => earned = true)`、`OnAdFullScreenContentClosed` で `done(earned)`、`OnAdFullScreenContentFailed` で `done(false)`
  - UMP：`ConsentInformation.Update(new ConsentRequestParameters(), err => ConsentForm.LoadAndShowConsentFormIfRequired(_ => …))`（日本では何も出ない。ヨーロッパの人には同意の画面）
  - Editor・Windows：何もしない（`Rewarded` は `Ready` 扱いで、`ShowRewarded` はすぐ `done(true)`）。手元確認で広告の部品を動かさない
- [ ] **Step 1: app-check に足す（まだ失敗する）** — `PachiTracking.mm` が iOS だけ・`AppTrackingTransparency;` を含む／Editor で `new Ads().Rewarded == AdState.Ready`
- [ ] **Step 2: 失敗を見る**
- [ ] **Step 3: `PachiTracking.mm`・`Ads.cs` を書く**（`Ads` の中の Google の部品を呼ぶところは `#if UNITY_IOS && !UNITY_EDITOR` にしない。コンパイルは手元でも通るので、呼ぶかどうかを `Application.platform` で分ける）
- [ ] **Step 4: 通るのを見る**
- [ ] **Step 5: コミット**「広告（同意・ATT・バナー・ごほうび）の包みを作る」

---

### Task 5: 画面と流れにつなぐ

**Files:**
- Modify: `Scripts/UI/SettingsScreen.cs`、`Scripts/UI/MainScreen.cs`、`Scripts/PachiApp.cs`、`Editor/PachiAppCheck.cs`

**Interfaces:**
- Consumes: Task 1 の `Entitlements`・`ExportGate`、Task 3 の `Shop`、Task 4 の `Ads`
- Produces：`SettingsScreen.ShowShop(Shop shop)`（商品の並び・値段・購入済み・知らせを今の状態で描き直す）、`SettingsScreen.Buy`（`Action<string>`）・`SettingsScreen.Restore`（`Button`）、`MainScreen.SetExportReady(bool ready)`

見た目（frontend-design を読み込んでから。今の銀の板と金の線の決まりのまま）：

```
買い物                                   ← 見出し（灰）。最初の色の下に置く
┏━━━━━━━━━━━━━━━━━━━━━━━━━┓
┃ 印を消す              ¥300 ┃ ← 銀の板1枚に名前（Reggae One）と値段（右寄せ）
┃ 動画の右下の「脳汁メーカー」  ┃    説明は灰の小さい字
┗━━━━━━━━━━━━━━━━━━━━━━━━━┛
（広告を消す・セット も同じ形。見せるのは Entitlements.ShopItems だけ）
買ったもの：印を消す ✓             ← 金の字（買ったものだけ金）
        購入を復元                  ← 銀の小さい板
知らせ（灰）                         ← Shop.Notice
```

  - 値段が来ていないあいだは「—」で押せない。`Unavailable` のときは「いまは買えません。ネットにつないで、開き直してください」
  - 設定の縦に動かす範囲の下を、広告を出すあいだ `UiTheme.BannerHeight` 空ける（バナーが最後の項目に重ならない）
  - 「書き出す」：`ExportGate.Decide(owned.ShowAds, Application.internetReachability != NotReachable, ads.Rewarded)` が `NeedNetwork` なら押せない形（文字を灰）にして、EndNotice に知らせ。`Wait` も知らせ。`ShowAd` なら `ads.ShowRewarded(watched => watched ? StartExport() : EndNotice = NotWatched)`。`ExportNow` ならすぐ書き出す
  - 終わりの画面に入るたび・広告の状態が変わるたびに `SetExportReady` を決め直す
  - 書き出しの印：`watermark: shop.Owned.ShowWatermark`（順番3の `true` を置き換える）
  - バナー：起動時 `ads.Start(owned.ShowAds)`、買い物で `ShowAds` が false になったら `ads.SetBanner(false)`
- [ ] **Step 1: app-check に絵を足す（まだ失敗する）** — 設定のいちばん下：買っていない（3つ）・印を消す済み（広告を消すだけ）・両方済み（買ったもの2つと復元だけ）、Unavailable。終わりの画面：ネットが要る（「書き出す」が灰で知らせ）
- [ ] **Step 2: 失敗を見る**
- [ ] **Step 3: SettingsScreen・MainScreen・PachiApp を書く**
- [ ] **Step 4: `sh app-check.sh` が通り、絵を見る** — 重なり・はみ出しが無い、9:16（iPhone SE）でも最後まで動かして見える
- [ ] **Step 5: `sh run-tests.sh`**
- [ ] **Step 6: コミット**「設定に買い物の項目を足し、書き出しの前のごほうび広告と印を買ったものにつなぐ」

---

### Task 6: 組み立てて iPhone で確かめる

- [ ] **Step 1: superpowers:verification-before-completion を読み、`sh run-tests.sh`・`sh app-check.sh` を流す**
- [ ] **Step 2: ユーザーに頼むことをまとめて伝える**（どれも画面の場所まで書く）
  - App Store Connect：「契約／税金／口座情報」で有料App契約を結ぶ（口座・税の入力はユーザー）。アプリ「脳汁メーカー」の「App内課金」で3つを非消耗型で登録（ID・値段は上の表どおり、表示名と説明も）
  - AdMob：アプリ（iOS・脳汁メーカー）と広告枠2つ（バナー・リワード）を作り、出てきた ID を教えてもらう（秘密ではない。順番5で本物に切り替えるときに使う。今回の TestFlight はテスト広告なので無くてもよい）
- [ ] **Step 3: push と TestFlight をユーザーに確認**（順番3の直し・最初の色の3コミットも一緒に届く）→ `gh workflow run ios-testflight.yml`（production はオフ）→ `gh run watch` で文字で見る。pod install・xcworkspace まわりで落ちたら `gh run view --log-failed` を読み、systematic-debugging で直す
- [ ] **Step 4: iPhone で確かめてもらう項目** — 初回に追跡の許可を聞かれる／下に「Test Ad」のバナー／「書き出す」でテストのごほうび広告 → 最後まで見ると書き出し、途中で閉じると知らせ／機内モードで「ネットにつなぐと書き出せます」／設定の買い物で値段が出る・サンドボックスで買える（お金はかからない）・買った瞬間にバナーが消える・印が消える・並びが変わる／アプリを消して入れ直し「購入を復元」で戻る
- [ ] **Step 5: 進み具合のメモに書く**
