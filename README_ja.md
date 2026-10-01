# DeepSeek Harness Toolkit — `dsh-minato`

**DeepSeek Harness（dsh）の Web UI を、ターミナルなしで導入・監視・バックアップ・修復するツールボックス。**
Windows と Linux に対応。すべて手元のマシン内で完結し、どこかへ送信することはありません。

> ⚠️ **非公式ツールです。** 本プロジェクトは DeepSeek 公式とは無関係で、承認や提携もありません。
> すでにお持ちの `dsh` コマンドラインを操作するだけの第三者製ヘルパーです。
>
> [English](README.md) · [简体中文](README_zh-CN.md) · **日本語**

---

## スクリーンショット

以下はすべて実際の画面です。ただしデータは**隔離したフィクスチャ用ディレクトリ**のもので、
実在のセッション・パス・数値は一切含まれていません。

| ダッシュボード | セッションとトークン | バックアップ |
|---|---|---|
| ![ダッシュボード](docs/screenshots/gui-kanban.png) | ![セッション](docs/screenshots/gui-sessions.png) | ![バックアップ](docs/screenshots/gui-backup.png) |

| ヘルスチェック | 設定 | 更新 |
|---|---|---|
| ![ヘルスチェック](docs/screenshots/gui-doctor.png) | ![設定](docs/screenshots/gui-settings.png) | ![更新](docs/screenshots/gui-update.png) |

<details>
<summary>残りのページ（概要・プラグイン・このツールについて・ログ）とコマンドライン</summary>

| 概要 | プラグイン | このツールについて | ログ |
|---|---|---|---|
| ![概要](docs/screenshots/gui-overview.png) | ![プラグイン](docs/screenshots/gui-plugins.png) | ![について](docs/screenshots/gui-about.png) | ![ログ](docs/screenshots/gui-logs.png) |

| CLI メニュー | CLI ステータス |
|---|---|
| ![CLI メニュー](docs/screenshots/cli-menu.png) | ![CLI ステータス](docs/screenshots/cli-status.png) |

</details>

---

## できること

| | |
|---|---|
| **dsh の導入** | ダブルクリックだけ。ターミナルも Node.js の知識も不要です。CLI を入れ、PATH とスタートメニューを整え、インストール先の隣にアンインストーラを残します。 |
| **起動 / 停止 / 監視** | ボタン一つで Web UI を起動し、状態と開くべき URL を表示します。 |
| **バックアップと復元** | セッション・設定・認証情報をまとめます。**完了マーカー**とファイル単位のハッシュを持ち、**中断されたパッケージや改変されたパッケージは復元せずに拒否**します。 |
| **バックアップ管理** | 触る前に、各パッケージの中身を正確に確認できます。 |
| **別の PC への移行** | パッケージを書き出し、新しいマシンで取り込みます。 |
| **更新センター** | Web UI・公式デスクトップアプリ・本ツール・導入済みプラグインを一箇所で更新。 |
| **ヘルスチェック** | 「どこが壊れているのか」を特定し、直すための一行を示します。 |
| **アンインストール** | **既定ではデータを一切削除しません。** また、インストール先に見えないディレクトリは削除を拒否します。 |

---

## インストール

### Windows

[Releases](../../releases) から `dsh-minato-<版>-win-x64-setup.exe` を取得して実行します。

- インストーラは**デジタル署名されていません。** 初回に SmartScreen の「不明な発行元」が出ることがあります。
  これは未署名バイナリの通常の挙動です。先に公開されている `.sha256` で照合してください。
- 展開して使いたい場合は `dsh-minato-win-x64.zip` を解凍し、`gui\dsht-gui.exe` を実行します。

### Linux

`dsh-minato-linux-x64.tar.gz` を取得してから:

```bash
tar -xzf dsh-minato-linux-x64.tar.gz
cd dsh-minato-linux-x64
./install.sh            # 現在のユーザーに導入。--prefix <dir> で場所を指定
```

削除は `./install.sh --uninstall`（自分で設置場所を移動した場合のみ `--force` を追加）。

---

## クイックスタート

1. 上記の方法で導入し、**dsh-minato** を開きます。
2. ダッシュボードが現在の状態を表示します。`dsh` が無ければヘルスチェックが指摘し、対処法を示します。
3. **起動** を押し、表示された Web UI の URL を開きます。
4. 何かを変更する前に、必ず**バックアップ**を押します。

---

## 画面（GUI）

左側のナビゲーションに 10 ページあります:

| ページ | 用途 |
|---|---|
| **概要** | 何が入っていて、何が動いていて、何が古いかを 1 画面で。 |
| **ダッシュボード** | セッション数・キャッシュヒット率・トークンなどの主要指標と、起動/停止。 |
| **セッションとトークン** | セッションごとの内訳。親子のグループ化と並べ替えに対応。 |
| **プラグイン** | あるプロファイルのプラグイン、無効化されているもの、起動を壊すもの。 |
| **バックアップ** | 作成・確認・検証・復元・書き出し・削除。 |
| **ヘルスチェック** | 「どこが壊れているか」の報告と、具体的な処方。 |
| **設定** | 言語・ポート・挙動を YAML を触らずに変更。 |
| **このツールについて** | バージョン、クレジット、そして**意図的にやらないこと**。 |
| **更新** | dsh・デスクトップアプリ・本ツール・プラグインの更新。 |
| **ログ** | ランチャーのログを絞り込み・検索・書き出し。 |

---

## コマンドライン

GUI が呼んでいるのと同じ CLI で、単体でも使えます:

```text
dsh-minato status [--detail]      何が入っている / 動いている / 待ち受けている
dsh-minato start | stop           dsh Web UI の起動と停止
dsh-minato install | update       dsh（と本ツール）の導入・更新
dsh-minato uninstall              本ツールの削除（データには触れません）
dsh-minato sessions               セッションごとのトークンとキャッシュ
dsh-minato backup [--to <dir>]    バックアップパッケージの作成
dsh-minato backup-list [--verify] 一覧と内容の検証
dsh-minato backup-dir [--set <d>] バックアップの保存先
dsh-minato restore --path <pkg> [--apply] [--yes]
dsh-minato backup-export | backup-delete
dsh-minato doctor                 総合ヘルスチェックと処方
dsh-minato profiles | profilecheck | profilepatch | bridge-install
dsh-minato bootdiag               dsh が起動しない理由
dsh-minato verify-install         ダウンロードしたファイルの検証
dsh-minato log | config-get | config-set | autostart | shortcut
dsh-minato version | about | selftest
```

各コマンドは**機械可読なマーカー行**（`STATUS_OK`、`BACKUP_OK`、`RESTORE_FAIL` など）を出力します。
スクリプトや GUI は文章から推測せず、これを解析できます。

---

## 安全性とプライバシー — **コードが実際にしていること**だけを書きます

この節はコードの挙動のみを述べます。ここに書いてあることをコードがしていなければ、それはバグです。

- **ローカルのみ。** 自分のマシンを読み書きするだけです。ネットワークに接続するのは
  `check`、`update-info`、`update-center`、`install`、`update`、`verify-install --url` の 6 つだけです。
  それ以外（status、sessions、backup、restore、doctor、ログ）は**接続しません**。
- **守るべき所は守ります。** `restore`・`wipe`・`update`・`import` の前には**必ずバックアップ**を取り、
  その場所を表示するので、失敗しても巻き戻せます。ただし**すべての書き込み**がそうではありません:
  設定変更（`config-set`、`backup-dir --set`）、プロファイルの変更、ショートカット/PATH の編集は違います。
- **アンインストールでデータは消えません。** 消えるのは本ツール自身のファイルだけです。データ削除は別の、
  明示的な操作です。
- **バックアップの完全性は検証します。** パッケージには**最後に書かれる完了マーカー**とファイル単位の
  ハッシュがあり、中断されたものや内容が変わったものは拒否されます。
- **CLI・インストーラ・ランチャー・dsh プラグインは、実行時の第三者依存がゼロです。**
  **GUI だけは Avalonia**（UI フレームワーク）を使います。これが唯一の例外です —
  [PRIVACY.md](PRIVACY.md) と [ASSETS.md](ASSETS.md) を参照。
- **テレメトリなし、アカウントなし、アップロードなし。**

---

## トラブルシューティング

**「dsh が起動しない」、またはプラグインの読み込みに失敗する。** ヘルスチェックを実行してください。
問題のあるプロファイル項目と、変更すべき一行を示します:

```bash
dsh-minato doctor              # 読み取り専用: どの項目が起動を壊すか
dsh-minato profilecheck        # 同じ問いをプロファイルごとに
```

**復元やバックアップが拒否された。** これは**意図的**です。中身を確認してください:

```bash
dsh-minato backup-list --verify
```

**ウイルス対策がインストーラを検知した。** `%LOCALAPPDATA%` に書き込み、PATH を変更し、ショートカットを作る
自己解凍インストーラは、そうでなくてもドロッパーに見えます。公開されている `.sha256` で照合するか、
自己解凍ラッパーの無い zip を使ってください。

---

## ソースからのビルド

CLI とツールは素の C# と PowerShell です — 第三者パッケージは使いません。

```powershell
# Windows CLI（同梱のコンパイラで十分）
csc /target:exe /out:dsh-minato.exe (Get-ChildItem v3\src -Recurse -Filter *.cs).FullName

# GUI（Avalonia が NuGet パッケージなので .NET SDK が必要）
dotnet build v3\gui\Dsht.Gui.Avalonia\Dsht.Gui.Avalonia.csproj -c Release
```

```bash
# Linux CLI
dotnet publish v3/src/Dsht.Cli/Dsht.Cli.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

---

## 開発とテスト

この README の各主張は、ローカルで実行できるゲートで守られています:

| ゲート | 証明する内容 |
|---|---|
| `v3/tests/verify_switchover.ps1` | 9 つの準備ゲートを一度に。まずこれを実行。 |
| `v3/tests/compare_markers.ps1` | CLI の機械可読出力が v2.x の契約と一致し続けていること。 |
| `v3/tests/verify_fixes.ps1` | 修正済みの 70 件が今もコード内で修正されていること。 |
| `v3/tests/verify_restore_apply.ps1` | 隔離ルートへの**実際の**復元と、外への書き込みゼロ。 |
| `v3/tests/Dsht.Contracts.Tests` | ドメインとプラットフォームの契約 334 件。 |
| `v3/gui/Dsht.Gui.LogicTests` | GUI のマーカー解析 58 件。 |
| `plugin/dsh-minato-bridge/test` | プラグイン 23 件（依存宣言を含む）。 |
| `v3/tools/verify_backup_chain.sh` | バックアップの信頼チェーンを端から端まで（Linux）。 |
| `v3/tools/verify-linux.sh` | リリース tar が主張どおりの中身であること。 |

同じゲートが CI で、push のたびに Windows と Linux の両方で実行されます。

---

## 現状

**V3 はプレビューです。** CLI と Linux 側のツールは完成し、実機で検証済みです。GUI は上記 10 ページ分が
機能的に揃っており、現在は磨き込みの段階です。**V2.x は V3 が完全に追いつくまで保守を続けます。**

既知の制限を率直に:

- Windows の成果物は**デジタル署名されていません**（無償コード署名の申請は承認されませんでした）。ハッシュを照合してください。
- `verify_fixes.ps1` は**テキスト一致**です。修正がコードに存在することは確認できますが、そのコードに到達できるかは確認できません。
- GUI は Avalonia を使うため、プロジェクトの他の部分と違い**依存ゼロではありません**。

---

## ライセンスとクレジット

MIT — [LICENSE](LICENSE) を参照。プライバシーは [PRIVACY.md](PRIVACY.md)、素材のライセンスとアイコンの出所は
[ASSETS.md](ASSETS.md) にあります。

`dsh-minato`（みなと、「港」）は以前 `DeepSeek-Harness-Toolkit` という名前でした。改名は git の履歴に残っています。
本プロジェクトは独立したもので、DeepSeek 公式とは関係ありません。
