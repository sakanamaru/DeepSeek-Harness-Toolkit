# Changelog / 更新日志

All notable changes to **DeepSeek Harness Toolkit** (unofficial). Full release notes, assets and verification data live on the [Releases page](../../releases).

**DeepSeek Harness Toolkit**（非官方）的主要变更记录。完整发布说明、产物与校验信息见 [Releases 页面](../../releases)。

---

## v2.7.1 — 未发布 / Unreleased

### Fixed / 修复

- **GUI 的「桌面快捷方式」建出来的是 CLI 的快捷方式** — `CreateDesktopShortcut` 此前固定指向核心 exe、固定命名 `DeepSeek Harness Toolkit.lnk`，所以在 GUI 里点这个按钮得到的是命令行程序的快捷方式。现在 `shortcut` 支持 `--exe <目标>` / `--name <基名>` / `--desc <描述>`，GUI 传自己的 exe 与 `DeepSeek Harness Toolkit GUI` 基名：GUI 建出的快捷方式指向 GUI 自己，且与 CLI 的快捷方式**并存不互相覆盖**。核心 CLI 的默认行为不变（仍指向核心自己）。
  **GUI's "Desktop Shortcut" created a CLI shortcut** — `CreateDesktopShortcut` was hardcoded to the core exe and to the name `DeepSeek Harness Toolkit.lnk`, so clicking that button in the GUI produced a shortcut to the command-line program. `shortcut` now accepts `--exe <target>` / `--name <base name>` / `--desc <description>`, and the GUI passes its own exe plus the `DeepSeek Harness Toolkit GUI` base name, so the GUI shortcut points at the GUI and the two coexist without overwriting each other. The core CLI's default behaviour is unchanged (still points at the core).
  - 顺带加固：目标必须是**存在的 .exe 文件**（否则 `SHORTCUT_FAIL` 且不落文件）；快捷方式基名做净化（只取文件名部分、剔除 `\ / : * ? " < > |` 与控制字符、最长 80 字），**路径分隔符一律剔除**，防止写出桌面目录之外。
  - Hardening along the way: the target must be an existing `.exe` (otherwise `SHORTCUT_FAIL` and nothing is written), and the base name is sanitized (file-name part only, `\ / : * ? " < > |` and control characters stripped, max 80 chars) so a crafted name cannot escape the desktop directory.

### Tests / 测试

- 单元测试 **277 → 284**：新增自定义目标/基名、GUI 快捷方式与 CLI 快捷方式互不覆盖、基名净化（`..\..\evil` → `evil`、非法字符剔除）、空名拒绝、目标不存在拒绝。
  Unit tests **277 → 284**: custom target/base name, GUI vs CLI shortcut coexistence, base-name sanitizing (`..\..\evil` → `evil`, invalid characters stripped), blank name rejected, missing target rejected.

---

## v2.7.0 — 2026-09-18 —（托盘 · 关闭行为 · 状态栏 · 快捷键 · 验证此安装 / Tray, Close Behavior, Status Bar, Shortcuts & Verify This Install）

### Added / 新增

- **Tray icon & close-behavior memory (GUI)** — a tray icon (Show Window / Start dsh / Stop dsh / Exit) and one-time close prompting: the first time you close the window it asks whether to minimize to tray or exit directly and remembers the answer in `close_action` (`ask | tray | exit`; empty = never asked), changeable later on the Settings page.
  **托盘图标与关闭行为记忆（GUI）**——托盘菜单（显示主窗口 / 启动 dsh / 停止 dsh / 退出）；首次关窗只问一次「最小化到托盘 / 直接退出」并把答案记入 `close_action`（`ask | tray | exit`，空=还没问过），之后可在设置页修改。
- **Bottom status bar & keyboard shortcuts (GUI)** — the status bar shows service state, PID, uptime, current theme and language plus shortcut hints; `Ctrl+1`~`Ctrl+7` switch pages, `F5` refreshes status, `Ctrl+B` runs a backup (text-input fields keep their own keys).
  **底部状态栏与快捷键（GUI）**——状态栏显示服务状态 / PID / 运行时长 / 当前主题 / 语言与快捷键提示；`Ctrl+1`~`Ctrl+7` 切页、`F5` 刷新状态、`Ctrl+B` 立即备份（文本输入框内按键不受影响）。
- **New config keys `close_action` / `auto_start`** — both whitelisted in `config-set` and reported by `config-get`; `auto_start=off` turns off the interactive menu's 5-second auto-start countdown (the menu then waits for a manual choice and says so).
  **新配置键 `close_action` / `auto_start`**——两者均加入 `config-set` 白名单、由 `config-get` 报告；`auto_start=off` 关闭交互菜单的 5 秒自动启动倒计时（改为明确提示、等待手动选择）。
- **Read-only `status --detail`** — still prints the three-state marker line (`STATUS_UP` / `STATUS_STARTING` / `STATUS_DOWN`) and adds `STATUS_PID`, `STATUS_START`, `STATUS_UPTIME`; it feeds the GUI status bar and writes nothing.
  **只读 `status --detail`**——仍输出三态标记行（`STATUS_UP` / `STATUS_STARTING` / `STATUS_DOWN`），并追加 `STATUS_PID` / `STATUS_START` / `STATUS_UPTIME` 三行；作为 GUI 状态栏数据源，全程不写任何东西。
- **Doctor: 7th category `Integrity`** — compares the running exe against the bundled `hashes.txt`: match / mismatch (reported as an error) / no manifest found (normal for a single copied exe).
  **体检新增第 7 类 `Integrity`**——把运行中的 exe 与随包 `hashes.txt` 比对：一致 / 不一致（按错误报告）/ 未找到清单（单独复制 exe 属正常）。
- **About page: "Verify This Install"** — downloads the official `hashes.txt` (plain text: no JSON parsing, no third-party dependency) and compares the SHA-256 of the core exe and the GUI exe against it, with three states (match / mismatch / could not verify). It is explicitly a **hash-consistency** check, not signature verification.
  **关于页「验证此安装」**——下载官方 `hashes.txt`（纯文本：不解析 JSON、无第三方依赖），比对核心 exe 与 GUI exe 的 SHA-256，三种结果（一致 / 不一致 / 未能验证）；明确标注为「哈希一致性」比对，**不是**签名验证。
- **GUI startup & first-run checks** — every launch does a local consistency check against the bundled `hashes.txt` (local only, no network) and detects the "Mark of the Web" (downloaded-from-internet) marker, which only writes a log line; the very first launch runs an integrity self-check **only** — deliberately no environment inventory and no backup nagging, since a fresh machine has none of that yet.
  **GUI 启动检查与首次启动**——每次启动都做随包 `hashes.txt` 的本地一致性检查（纯本地、不联网），并检测「来自网络」标记（Mark of the Web），只记一行日志；**首次**启动只做完整性自检——刻意不列环境清单、不谈备份（新机器上这些本来就不存在）。
- **Toast feedback & unified restore flow (GUI)** — backup / restore / export / delete results are reported as tray balloon toasts; the "Restore" action switches to the Backup page (select → Dry-Run preview → confirm) instead of opening a second picker dialog, and restoring while the service is running is blocked up front with a clear reason.
  **气泡反馈与恢复流程统一（GUI）**——备份 / 恢复 / 导出 / 删除结果改用托盘气泡提示；「恢复」不再弹第二个选择框，而是切到备份页（选择 → Dry-Run 预演 → 确认）；服务运行中恢复会在开始前直接阻止并说明原因。
- **Home & Settings page updates (GUI)** — Home's Start and Install/Repair buttons swapped so Start is top-left, and the install button label follows detection ("Install dsh" when dsh is missing, "Repair dsh" when it is present); the Settings page gained two dropdowns (menu auto-start countdown; close-window behavior), both in the Toolkit group.
  **首页与设置页调整（GUI）**——首页「启动」与「安装/修复」按钮对调，「启动」位于左上；安装按钮文案跟随检测结果（未装 dsh 显示「安装 dsh」，已装显示「修复 dsh」）；设置页新增两个下拉项（菜单倒计时自动启动 / 关闭主窗口时），同属「工具箱」组。
- **Fewer processes, less dead code (GUI)** — the dsh version is cached and re-read only every 10th poll (plus right after operations) instead of spawning a process every 3 seconds; the now-redundant restore dialogs were removed (−348 lines) along with 18 unused localization keys.
  **少起进程、清理死代码（GUI）**——dsh 版本改为缓存，每 10 次轮询才重读一次（操作后立即重读），不再每 3 秒起一个进程；移除已冗余的恢复对话框（−348 行）与 18 个无用本地化键。
- **Boot-failure triage CLI — `profilecheck` / `bootdiag` / `profilepatch`** — three zero-dependency commands for the "dsh will not start" case (aliases `pc` / `bdiag` / `pp`). `profilecheck` statically scans `~/.dsh/profiles/**/*.yaml|*.yml` and reports the profile entries that make dsh fail to boot (`PROFILECHK_WARN <file> <line> <id> <key> <hint>`, `PROFILECHK_TOTAL <warnings> <files>`, `PROFILECHK_SKIPPED_VENDOR <n>`, `PROFILECHK_OK`, plus a machine-readable `PROFILECHK_FIX` line with `--abs`); `--dir` / `--file` pick another target, and `--vendor` also scans `node_modules` (skipped by default — those are package-shipped patch files). It also flags `@deepseek-ai/dsh-mcp-client` entries with `failOnStartupError: true` whose `command:` points at a missing file (report-only, never auto-fixed). `bootdiag --from <captured.txt>` parses captured dsh startup output and extracts the innermost cause from the error chain (`BOOTDIAG_OK` / `BOOTDIAG_KIND` / `BOOTDIAG_PLUGIN` / `BOOTDIAG_ENTRY` / `BOOTDIAG_FILE` / `BOOTDIAG_LINE` / `BOOTDIAG_HINT`); an unknown error prints `BOOTDIAG_KIND unknown` plus the first error line — it never guesses. `profilepatch --file <yaml> --id <entry> --set maxDepth=provider-managed [--yes]` is the controlled write: a one-line plan (`PROFILEPATCH_PLAN`), no write without `--yes` (`PROFILEPATCH_DRYRUN`), a backup into `<toolkit dir>\backup\bootdiag-<timestamp>\` before writing, exactly one inserted line with matching indentation, idempotent on a second run (`PROFILEPATCH_NOOP`), and a rescan with automatic rollback if verification fails (`PROFILEPATCH_ROLLBACK`). It accepts that single key/value pair only — deliberately not a general YAML editor.
  **启动失败诊断与修复命令 `profilecheck` / `bootdiag` / `profilepatch`**——三个零依赖命令，专治「dsh 起不来」（别名 `pc` / `bdiag` / `pp`）。`profilecheck` 静态扫描 `~/.dsh/profiles/**/*.yaml|*.yml`，报出会让 dsh 启动失败的 profile 条目（`PROFILECHK_WARN <文件> <行> <id> <键> <提示>`、`PROFILECHK_TOTAL <告警数> <文件数>`、`PROFILECHK_SKIPPED_VENDOR <n>`、`PROFILECHK_OK`；带 `--abs` 时另给机器可读的 `PROFILECHK_FIX` 行）；`--dir` / `--file` 可换扫描目标，`--vendor` 才一并扫描 `node_modules`（默认跳过——那是包自带的补丁文件）。它还会标记 `@deepseek-ai/dsh-mcp-client` 中 `failOnStartupError: true` 但 `command:` 指向不存在文件的条目（**只报不修**）。`bootdiag --from <捕获的启动输出.txt>` 解析启动输出、从错误链里取最内层病灶（`BOOTDIAG_OK` / `BOOTDIAG_KIND` / `BOOTDIAG_PLUGIN` / `BOOTDIAG_ENTRY` / `BOOTDIAG_FILE` / `BOOTDIAG_LINE` / `BOOTDIAG_HINT`）；识别不了时报 `BOOTDIAG_KIND unknown` 并附第一条错误行——绝不猜测。`profilepatch --file <yaml> --id <条目> --set maxDepth=provider-managed [--yes]` 是受控写入：先给一行计划（`PROFILEPATCH_PLAN`），没有 `--yes` 绝不落盘（`PROFILEPATCH_DRYRUN`），写入前先备份到 `<工具箱目录>\backup\bootdiag-<时间戳>\`，只按同级缩进插入一行，二次运行幂等（`PROFILEPATCH_NOOP`），写入后复扫、校验不过自动回滚（`PROFILEPATCH_ROLLBACK`）。它只接受这一个键值对——刻意不做通用 YAML 改写器。
- **Doctor page: "Config Check / 配置自检" (GUI)** — a new Doctor-page button runs `profilecheck`; when fixable risks are found it lists them and asks for confirmation, then backs them up and fixes them through `profilepatch` and rescans (result reported as a toast plus log lines). The scan is read-only, and the fix backs up first and rolls back automatically if verification fails.
  **体检页新增「配置自检」按钮（GUI）**——体检页新按钮运行 `profilecheck`；发现可修风险时先列出警告并弹框确认，确认后经 `profilepatch` 先备份再修复，随后复扫（结果以气泡 + 日志反馈）。扫描全程只读；修复先备份、校验失败自动回滚。

### Fixed / 修复

- **GitHub HTTPS requests failed on .NET Framework** — the default `SecurityProtocol` did not include TLS 1.2, so any HTTPS request to GitHub (update check, integrity check) failed with "could not create SSL/TLS secure channel"; TLS 1.2 is now enabled explicitly.
  **.NET Framework 下 GitHub HTTPS 请求失败**——默认 `SecurityProtocol` 不含 TLS 1.2，导致所有发往 GitHub 的 HTTPS 请求（更新检查、完整性检查）报「无法创建 SSL/TLS 安全通道」；现已显式启用 TLS 1.2。
- **Status bar text ghosting** — on some themes the transparent status label picked up pixels from the title bar and left artifacts; it is now drawn with an opaque surround colour.
  **状态栏文字残影**——部分主题下透明标签会拾取标题栏像素、留下残影；改为不透明底色绘制。
- **Toolkit version wording drift** — the version the GUI displays is now kept aligned with the core version (the two had drifted apart in wording).
  **工具箱版本文案漂移**——GUI 显示的版本与核心版本对齐（此前两者措辞已漂移不一致）。

### Tests / 测试

- Unit tests **225 → 244** — `close_action` / `auto_start` configuration whitelist (accepted values, empty `close_action`, case-insensitive key name, rejected values) and status-bar uptime formatting (`FormatUptime` across the second / minute / hour / day boundaries). Integration tests unchanged at **33**; CI still runs both plus the three-variant GUI compile guard.
  单元测试 **225 → 244**——`close_action` / `auto_start` 配置白名单（合法值、`close_action` 空值、键名大小写不敏感、非法值拒绝）与状态栏运行时长格式化（`FormatUptime` 的秒 / 分 / 时 / 天边界）。集成测试维持 **33**；CI 仍跑两套测试与三形态 GUI 编译守卫。
- Unit tests **244 → 277** — profile block scanning (a missing `maxDepth`, an `mcp-client` entry whose `command` file does not exist, the package-patch skip, a nested `- id:` not mistaken for a new entry), boot-output parsing (`file:///…#entry` → path + line, the innermost cause of a nested error chain, unknown errors reported as `unknown` with the first error line), and the controlled patch path (plan / idempotent NOOP / unknown entry refused, backup taken before writing, exactly one inserted line at sibling indentation, rescan + rollback when verification fails, BOM preserved). Integration tests unchanged at **33**; CI still runs both plus the three-variant GUI compile guard.
  单元测试 **244 → 277**——profile 块扫描（缺 `maxDepth`、`mcp-client` 条目 `command` 指向不存在文件、包内补丁跳过、嵌套 `- id:` 不被误判为新条目）、启动输出解析（`file:///…#entry` → 路径 + 行号、多层错误链取最内层病灶、未知错误报 `unknown` 并附首条错误行）与受控写入（计划 / 幂等 NOOP / 未知条目拒绝、写入前备份、只插一行且缩进同级、复扫失败回滚、BOM 保留）。集成测试维持 **33**；CI 仍跑两套测试与三形态 GUI 编译守卫。

> ⚠️ 非官方工具，与 DeepSeek 官方无关。Unofficial community tool, not affiliated with DeepSeek.

---

## v2.6.0 — 2026-09-15 —（备份管理器 · Dry-Run · 更新中心 · 设置 · 日志中心 / Backup Manager, Dry-Run, Update Center, Settings & Log Center）

### Added / 新增

- **Seven-page GUI** — navigation is now Home / Backups / Update / Settings / Log / Doctor / About (v2.5.0 had four pages).
  **七页导航**——首页 / 备份 / 更新 / 设置 / 日志 / 体检 / 关于（v2.5.0 为四页）。
- **Backup Manager (GUI "Backups" page)** — every backup listed as a row (time / kind / size / validity) with Restore / Export / Delete for the selection and one-click Backup Now; list auto-refreshes after changes.
  **备份管理器（GUI「备份」页）**——每条备份一行（时间/类型/大小/有效性）+ 恢复/导出/删除 + 立即备份；变更后自动刷新。
- **Dry-Run before destructive operations** — `restore [--path X] --dry-run` prints a machine-readable merge plan (`DRYRUN_NEW/OVERWRITE/KEEP/BYTES/TOTAL`; merge semantics: destination-only files are never deleted); GUI restore shows the plan in a confirm dialog before doing anything; interactive wipe previews delete counts (files / dirs / total size) before the two-step confirmation. The preview shares the execution-side skip rules (node_modules / nested backups / symlink·junction not followed), so the numbers are what actually happens — including the restore-side distinction between a top-level and a nested skipped directory.
  **破坏性操作前 Dry-Run**——`restore --dry-run` 输出机器可读合并计划（合并语义：仅目标端文件不删）；GUI 恢复先弹预演确认；交互清除在两步确认前预演删除量。预演与执行共用同一套跳过规则（node_modules / 嵌套备份 / symlink·junction 不跟随），**所见即所得**（含恢复侧顶层与嵌套被跳过目录的区别）。
- **Update Center (GUI "Update" page)** — read-only visualization of the whole update picture: current dsh version, latest stable / latest rc (npm), update channel (`update_channel=stable|rc`), most recent pre-update backup, rollback candidates (valid backup count), dsh release-notes link. Network failures degrade to `unknown`, never block. "Update dsh…" still routes through the interactive flow (version list + destructive-action double confirmation): **checks may be automatic, updates never are**.
  **更新中心（GUI「更新」页）**——只读可视化：当前版本 / 最新 stable / 最新 rc / 通道 / 更新前备份 / 回滚候选 / 发布说明链接；网络失败降级不阻断；更新仍走交互双确认（检查可自动，更新永不自动）。
- **Settings page (GUI "Settings")** — four groups (Harness / Backup / Update / Toolkit): web host, workspace path, auto-backup retention (≥3), startup update check, dsh update detection, update channel, UI language. **Save submits only changed keys** (invalid values are refused core-side); the config file stays plain `key=value` (cross-platform friendly).
  **设置页（GUI「设置」）**——四组控件（Harness / 备份 / 更新 / 工具箱）：Web 主机、工作区路径、自动备份保留份数（≥3）、启动更新检查、dsh 更新检测、更新通道、界面语言；**保存只提交变化项**（非法值核心侧拒绝）；配置保持 `key=value`（跨平台友好）。
- **Log Center (GUI "Log" page)** — the Log page is now a structured operation log: every entry carries a level (`INFO / WARN / ERROR`) and timestamp; one-click level filters, live search, **Export** (UTF-8 file) / **Copy**; failures (timeouts, refused operations, missing core) are logged as WARN/ERROR.
  **日志中心（GUI「日志」页）**——结构化操作日志：级别（INFO/WARN/ERROR）+ 时间戳、级别筛选、实时搜索、导出（UTF-8）/复制；失败（超时、被拒绝的操作、核心缺失）记 WARN/ERROR。
- **New CLI** — `backup-list --detail` (kind / size / mtime per backup as `BACKUP_ITEM` lines), `backup-export --path <bk> --to <dir>` (copy-out), `backup-delete --path <bk>` (restricted to backups root `dsh-data-*`, audit-logged), `restore … --dry-run` (read-only merge preview), `update-info` (`UPDATEINFO_*` read-only data source), `config-get` / `config-set <key> <value>` (whitelisted read/write).
  **新命令**——`backup-list --detail`、`backup-export`、`backup-delete`（限备份根内、写审计日志）、`restore … --dry-run`、`update-info`、`config-get` / `config-set`（白名单读写）。

### Tests / 测试

- Unit tests **183 → 225** — dry-run merge/delete planning (incl. restore-side skip-rule fidelity for top-level vs nested `node_modules`), backup kind parsing, export & delete validation, rollback-candidate lookup, configuration whitelist. Integration tests unchanged at **33**; CI runs both plus the three-variant GUI compile guard.
  单元测试 **183 → 225**——Dry-Run 合并/删除计划（含恢复侧顶层 vs 嵌套 `node_modules` 跳过规则一致性）、备份类型解析、导出与删除校验、回滚候选查询、配置白名单。集成测试维持 **33**；CI 另跑两套测试与三形态 GUI 编译守卫。

---

## v2.5.0 — 2026-09-14 —（体检 / Doctor + 防篡改加固 / Diagnostics & supply-chain hardening）

### Added / 新增

- **Doctor / health check** — `doctor` CLI command and a GUI **Doctor** page (nav: Home / Log / Doctor / About). Read-only six-category check: System (Windows / Node / npm), Harness (installed & version), Service (port, listener identity, HTTP, 3-state), Workspace (path, permissions, size), Backup (dir, latest, age), Network (registry reachability). Machine-readable verdict line `DOCTOR_OK 0 | DOCTOR_WARN n | DOCTOR_ERROR n`; `doctor --report <file>` exports a full diagnostic report with API keys / tokens / cookies / passwords **redacted**.
  **体检 / Doctor**：`doctor` 命令 + GUI 体检页。只读六类检查（系统 / Harness / 服务 / 工作区 / 备份 / 网络），机器可读结论行；`--report` 导出**脱敏**诊断报告。
- **Self-integrity gate** — uninstall (incl. wipe), restore and dsh-update now refuse to run when a `hashes.txt` ships beside the executable and the executable's SHA-256 does not match it (tamper protection); builds without a manifest are not blocked.
  **自身完整性闸门**：卸载（含清除）/恢复/更新前自检 SHA-256，与随包 manifest 不符即拒绝；无 manifest 不阻断。
- **Supply-chain hardening** — immutable releases enabled; tag & main rulesets (no delete / no history rewrite, bypass never); CI least-privilege permissions; actions pinned to commit SHAs; build-provenance attestations on tag builds; release flow switched to draft → attach → publish; signed tags (`git tag -s`) from this version on; README/SECURITY official-distribution statements.
  **供应链加固**：不可变发布、tag/main ruleset、CI 最小权限、Actions 固定 SHA、构建溯源证明、draft→attach→publish 发布流、本版起签名 tag、官方渠道声明。

### Fixed / 修复

- **Service readiness misjudgment** carry-over verified end-to-end (GUI green *running*, CLI monitor, auto-open) — see v2.4.2.
- Backup/restore now **skip reparse points** (symlink / junction) with an audit log line instead of following them.
  备份/恢复遇 Symlink/Junction 跳过并记审计日志，不再跟随。
- `stop` re-verifies the :3080 listener identity immediately before killing (TOCTOU hardening).
  `stop` 终止前最后一刻复检监听身份（TOCTOU 加固）。

### Tests / 测试

- Unit tests **146 → 183** (doctor summary/sanitize/size + manifest parsing); integration suite unchanged (33 cases).

---

## v2.4.2 — 2026-09-13

### Fixed / 修复

- **Service readiness was permanently misjudged as “starting”.** dsh 0.1.5+ answers unauthenticated HTTP requests with **401**, so the old “HTTP 2xx = ready” check never saw a running service: the GUI showed a yellow *starting* state forever, the CLI monitor carried a bogus warning, and the browser was never auto-opened. Readiness now also accepts “the :3080 listener is a dsh process (command line verified)” — the same evidence the `stop` guard already used — while `stop` keeps its strict check.
  **服务就绪被永久误判为“启动中”。** dsh 0.1.5+ 对未授权 HTTP 请求统一返回 **401**，旧的“HTTP 2xx 才算就绪”判定因此永远看不到已在运行的服务：GUI 一直黄灯「启动中」、CLI 监控页带无意义的警告、浏览器也不再自动打开。现在改为「端口开 + 监听进程确为 dsh（校验命令行）」兜底判定——与 `stop` 防护同源；而 `stop` 仍保留严格校验。
- **The GUI operation log was always blank.** The log text box was docked *before* the header panel, and WinForms lays out later-added controls first — the fill area took the whole page and the first lines of text were painted behind the header. Dock order fixed; log lines emitted before the log page was built are now buffered instead of dropped (e.g. “embedded core extracted”).
  **GUI 操作日志永远是空白。** 日志文本框比顶部标题栏先加入，而 WinForms 的 Dock 按“后加入的先排”计算——填充区占满整页、首行文字被标题栏盖住。已修正 Dock 顺序；日志页构建前产生的日志行改为暂存而非丢弃（例如「已自动解出内嵌核心」）。

### Added / 新增

- README screenshots (GUI light/dark, log, about + CLI live monitor) and this changelog. / README 截图（GUI 浅/深色、日志、关于 + CLI 实时监控）与本更新日志。
- Repository topics for discoverability. / 仓库 topics 便于被检索到。

> ⚠️ 非官方工具，与 DeepSeek 官方无关。Unofficial community tool, not affiliated with DeepSeek.

## v2.4.1 — 2026-09-13 —（GUI 三版本 / GUI in Three Variants）

- **Three GUI variants** from one source: CLI core (A), GUI attached (B, needs the sibling core), GUI standalone (C, embeds the core and extracts it next to itself on first launch — temp file + atomic rename, so an interrupted extraction never leaves a broken exe). / **三种形态**：命令行核心（A）、GUI 附加版（B，依赖同目录核心）、GUI 单文件集成版（C，内嵌核心并在首次启动解出；先写临时文件再原子改名）。
- **Misuse guard**: launching from Desktop/Downloads now warns — GUI confirmation dialog (Cancel quits) and CLI yellow notice; Downloads detection follows redirected profiles (registry `User Shell Folders`). / **防误用**：桌面/下载目录直跑会提醒——GUI 弹确认框（取消即退出）、CLI 黄字警告；下载目录检测支持重定向路径（读注册表）。
- GUI attached variant detects a missing core at startup (status area + one-time log notice). / GUI 附加版启动即检测核心缺失（状态区 + 日志一次性提示）。
- `verify.ps1`: `-Tag` mode uses fixed download URLs and never calls the GitHub API (immune to anonymous rate limits); UTF-8 BOM so PowerShell 5.1 parses it correctly; retry + `-UseBasicParsing`. / `verify.ps1`：`-Tag` 模式走固定链接、不调 API；改存 UTF-8 BOM 以兼容 PS 5.1 解析；加重试与 `-UseBasicParsing`。
- Logo/icon orientation restored to the orientation used since v2.0. / logo 与图标方向恢复为 v2.0 以来的正确方向。

## v2.4.0-gui-alpha — 2026-09-01 —（GUI 正式版 / GUI Edition）

- First official GUI release: three pages (home status LED + actions · log · about), dark/light theme, bilingual, restore picker with confirmation. / 首个 GUI 正式版：三页界面、深浅主题、中英双语、恢复选择框。
- **Trust mechanism**: `verify.ps1` one-click verification, CI-generated `hashes.txt` **GPG-signed** (`hashes.txt.asc`), `SECURITY.md` boundaries; “unofficial” notices in the banner and About page. / **信任机制**：一键核验 + CI 自动 GPG 签名 + 安全边界声明 + 非官方标示。
- **Safety fix**: `stop` no longer kills a foreign process holding :3080 (the listener's command line must verify as dsh). / **安全修复**：`stop` 不再误杀占用 3080 的其他程序。
- Core CLI additions: `backup-list`, `restore --path <dir>`. / 核心新增 `backup-list`、`restore --path`。
- Full CI release pipeline (build from source, generate + sign hashes, upload assets). / 发布流水线全自动化。

## v2.4.0 — 2026-08-31 —（CLI 核心 · GUI 地基 / CLI core, GUI foundation）

- Five non-interactive CLI subcommands with single-line machine-readable markers (`backup` / `restore` / `status` / `start --bg` / `stop`) — the foundation the GUI is built on. / 五个非交互 CLI 子命令（单行机器标记），GUI 的地基。

## v2.3.0 — 2026-08-31 —（安全加固 + 更新健壮性 / Security & update hardening）

- Update failure now auto-rolls back and re-verifies the version; timeouts kill the whole process tree; unified update guard; numeric pre-release ordering (`rc.1 < rc.2 < rc.10`); symmetric long-path restore; wipe marker anchored to the exe directory; manual workspace blacklist. / 更新失败自动回滚并复验版本、超时终止进程树、更新守卫统一、rc 数字序、恢复长路径对称、marker 锚定 exe 目录、手动工作区黑名单。

## v2.1.4 — 2026-08-24 —（桌面快捷方式 + 管道死锁修复 / Desktop shortcut & pipe-deadlock fix）

- Fixed the classic pipe-buffer deadlock in `RunVisible` (npm/winget output is drained on background threads); added the desktop shortcut feature (CLI `shortcut`, monitor-page `I`). / 修复 `RunVisible` 管道缓冲死锁；新增桌面快捷方式。

## v2.1.3 — 2026-08-23 —（安全加固 / Security hardening）

- The root marker is never self-created (a stray exe is permanently refused regardless of neighbouring files); wipe trusts the root marker only; strict backup-directory validation; SemVer release > rc. / 根标记永不自建（单独复制的 exe 永久拒绝清除）；清除只认根标记；备份目录严格校验；正式版 > rc。

## v2.1.2 — 2026-08-23 —（更新管理 / Update management）

- dsh update management (menu 8 / `update`): version list incl. rc, pre-update backup, double confirm, local version history. ⚠️ Superseded by v2.1.3 for security reasons. / dsh 更新管理。⚠️ 因安全问题已被 v2.1.3 取代。

## v2.1.0 — 2026-08-17 —（备份保留策略 / Backup retention）

- Backup retention (manual backups kept forever, auto/protection backups pruned), 3-state service detection (TCP + HTTP), restore/import refused while running, silent update check, log rotation, product-level single-instance lock. / 备份保留策略、服务三态检测、运行中禁止恢复/导入、静默更新检查、日志轮转、产品级单实例锁。

## v2.0.0 — 2026-08-15 —（多工作区备份 / Multi-workspace backup）

- Multi-workspace backup (`_workspace\name\`), old-format import, long-path support (`\\?\`), persistent workspace path and entry memory. / 多工作区备份、旧格式导入、长路径支持、工作区路径与入口记忆。

---

## Notes / 说明

- This project is an **unofficial** community tool and is not affiliated with DeepSeek. / 本项目为社区**非官方**工具，与 DeepSeek 官方无关。
- Every release is built from source by GitHub Actions; `hashes.txt` + `hashes.txt.asc` let you verify before running (`verify.ps1 -Tag <tag>`). / 每个发布物均由 CI 从源码构建，可用 `hashes.txt` 与 GPG 签名核验。
