# iPhone 版の組み立て手順（ユーザーがやること）

Windows のままで iPhone 版を作るために、GitHub の組み立て機（Linux と Mac）を使います。
パスワードや鍵などの**秘密の入力は、ご自分でやってください**（Claude は中身を見ません）。

組み立ての流れ：

1. GitHub の Linux 機で、Unity が「Xcode の作り」（iPhone 用アプリの材料一式）を作る
2. GitHub の Mac で、Xcode がアプリに仕上げて TestFlight に送る

---

## 1. Unity のライセンスを GitHub に渡す

GitHub の Linux 機で Unity を動かすには、あなたの Unity の無料ライセンスが要ります。

1. Unity Hub を開く → 左下の歯車（Preferences）→ **Licenses** → **Add** → **Get a free personal license**
2. エクスプローラーで `C:\ProgramData\Unity\` を開く（隠しフォルダです。上の「表示」→「隠しファイル」にチェック）
3. `Unity_lic.ulf` をメモ帳で開き、**全文**をコピー
4. GitHub の pachieffect → **Settings** → **Secrets and variables** → **Actions** → **New repository secret** で、次の3つを作る

| 名前 | 中身 |
|---|---|
| `UNITY_LICENSE` | 3 でコピーした全文 |
| `UNITY_EMAIL` | Unity アカウントのメールアドレス |
| `UNITY_PASSWORD` | Unity アカウントのパスワード |

うまくいかないときの心当たり：
- **Unity アカウントを「Google でサインイン」で作った**場合、パスワードがありません。組み立て機は Google のボタンを押せないので、https://id.unity.com/password/new でパスワードを作ってから `UNITY_PASSWORD` に入れます（2026-10-18 に実際にこれで止まった。ログインに失敗し続けると Unity から「Temporary lock out」のメールが来て、10分ロックされる）
- **Unity Hub にライセンスが表示されていても、`Unity_lic.ulf` が無い**ことがあります。Hub の「ライセンスを加える」→「無料の個人用ライセンスを取得」を押し直すと作られます
- Unity アカウントの2段階認証に「認証アプリ」を使っていると、組み立て機からログインできないことがあります

ターミナルから入れる方法（ファイルを開かずに済む）：

```powershell
Get-Content -Raw "C:\ProgramData\Unity\Unity_lic.ulf" | & "C:\Program Files\GitHub CLI\gh.exe" secret set UNITY_LICENSE -R maosun-dev/pachieffect
& "C:\Program Files\GitHub CLI\gh.exe" secret set UNITY_EMAIL -R maosun-dev/pachieffect
& "C:\Program Files\GitHub CLI\gh.exe" secret set UNITY_PASSWORD -R maosun-dev/pachieffect
```

## 2. Apple の鍵を GitHub に渡す

天落杯に入れたのと**同じ値**を、pachieffect の Secrets にも入れます（リポジトリごとに入れ直しが要ります）。

| 名前 | 中身 |
|---|---|
| `APPLE_TEAM_ID` | Apple のチーム ID（10文字） |
| `APPSTORE_API_KEY_ID` | App Store Connect API キーの Key ID |
| `APPSTORE_API_ISSUER_ID` | 同じキーの Issuer ID |
| `APPSTORE_API_PRIVATE_KEY` | `AuthKey_XXXX.p8` をメモ帳で開いた**全文** |

.p8 ファイルが手元に無いときは、App Store Connect → **ユーザとアクセス** → **統合** → **App Store Connect API** → **キー** で作り直せます（.p8 は作ったときに一度しかダウンロードできません）。

## 3. Bundle ID（アプリの識別名）を登録する

1. https://developer.apple.com/account → **Certificates, Identifiers & Profiles** → **Identifiers** → **＋**
2. **App IDs** → **App** → 続ける
3. Description：`脳汁メーカー`、Bundle ID：**Explicit** に `com.maosundev.noujiru`
4. 機能（Capabilities）は何も足さずに登録

## 4. App Store Connect にアプリを作る

1. https://appstoreconnect.apple.com → **アプリ** → 左上の **＋** → **新規アプリ**
2. プラットフォーム：**iOS**
3. 名前：`脳汁メーカー - パチンコ激アツ文字演出`
4. プライマリ言語：**日本語**
5. バンドル ID：3 で作った `com.maosundev.noujiru`
6. SKU：`noujiru`
7. ユーザアクセス：アクセス制限なし

「その名前は使われています」と出たら、そこで止めて Claude に相談してください。

## 5. TestFlight で受け取る準備

1. 作ったアプリ → **TestFlight** → 内部テストの **＋** でグループを作り、自分を入れる
2. iPhone に App Store から **TestFlight** アプリを入れておく

## 6. 組み立てを動かす

1. GitHub の pachieffect → **Actions** → 左の **iPhone版をTestFlightへ** → 右の **Run workflow**
2. 最初は **TestFlight まで送る** のチェックを外して、Unity の段だけ試す（30〜60分）
3. Unity の段が通ったら、チェックを入れてもう一度（Mac の段がさらに 30〜60分）
4. 送り終えてから数分〜30分で、iPhone の TestFlight アプリに「脳汁メーカー」が出ます

Mac の段がいつまでも始まらないときは、**仕上げに使う Mac** を `macos-latest` に変えて動かし直してください。
