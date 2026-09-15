# Changelog / 更新日志

All notable changes to **DeepSeek Harness Toolkit** (unofficial). Full release notes, assets and verification data live on the [Releases page](../../releases).

**DeepSeek Harness Toolkit**（非官方）的主要变更记录。完整发布说明、产物与校验信息见 [Releases 页面](../../releases)。

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
