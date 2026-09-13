# Changelog / 更新日志

All notable changes to **DeepSeek Harness Toolkit** (unofficial). Full release notes, assets and verification data live on the [Releases page](../../releases).

**DeepSeek Harness Toolkit**（非官方）的主要变更记录。完整发布说明、产物与校验信息见 [Releases 页面](../../releases)。

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
