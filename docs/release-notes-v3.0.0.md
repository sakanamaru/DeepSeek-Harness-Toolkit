<p align="center">
  <img src="https://raw.githubusercontent.com/sakanamaru/dsh-minato/main/logo.png" alt="dsh-minato" width="180">
</p>

# dsh-minato 3.0.0 — 2026-10-02（首个正式版 / first stable release）

> ⚠️ 非官方工具，由社区独立开发，与 DeepSeek 官方无关。本工具**不是 dsh 插件**：它是独立进程，不注入 dsh，dsh 没装也能用。

**一句话**：V3 线转正的第一个正式版 —— 跨平台 CLI + 十页图形界面、备份/恢复引擎重写、47 项缺陷修复，
外加一项可选的只读桥接插件（装了才能实时看到「哪些会话还活着」）。

**In one line**: the first stable release of the V3 line — a cross-platform CLI plus a ten-page GUI, a rewritten
backup/restore engine, 47 defect fixes, and an optional read-only bridge plugin.

---

## 新增 / Added

- **V3 跨平台 CLI（`dsh-minato`）**：31 个具名命令 + 无参数数字菜单 —— 安装/更新/卸载、起停、状态、体检、会话、
  profile 扫描与处方、备份/恢复/导出/删除、配置读写、引导诊断、自检、快捷方式、`about`。
  **V3 CLI**: 31 named commands plus a numeric menu.
- **V3 图形界面（Avalonia，Windows / Linux 通用）**：概览 · 看板 · 会话与 Token · 形态与插件 · 备份 · 体检 ·
  设置 · 说明 · 更新 · 日志。
  **V3 GUI (Avalonia, Windows and Linux)**: ten pages.
- **Linux 支持（x86-64）**：CLI 全命令可用；免 sudo 一键装 Node（curl/wget/python3 任一，按 CPU 架构选包）；
  一键起停按**可观测事实**判定；`shortcut` 写应用菜单项；备份/恢复**含工作区**。
  **Linux support (x86-64)**, including a no-sudo Node bootstrap.
- **备份 / 恢复引擎重写**：完成标记 + 逐文件内容哈希 + 恢复前自动锚点（可回滚）+ 截断包拒收 + 不写穿符号链接。
  **Rewritten backup/restore engine** with a completion marker, content hashes and a pre-restore anchor.
- **可选的只读桥接插件** `dsh-minato-bridge`：把「哪些会话还活着」从 dsh 进程内投影出来。装不装由你决定，
  不装只是少一个字段（见下方专节）。
  **Optional read-only bridge plugin**; without it one field falls back to `unknown`.

## 修复 / Fixed

- **47 项缺陷**（三轮子代理复审 + 真机审查逐条修），代表性条目：
  **47 defect fixes**, three rounds of review plus a real-machine audit. Representative ones:
  - 恢复失败会自动回滚（此前失败后数据留在半途，无路可退）
  - 符号链接 / junction 不再绕过恢复隔离闸门（两端都解析真实路径；真机双向验证）
  - Node 运行时下载后必须过官方 `SHASUMS256` 校验，不匹配就拒绝解压（fail-closed）
  - `..` 越界删除：纯词法规范化，逃逸一律拒绝
  - 中断的备份会被识别为不完整（完成标记 + 内容哈希），恢复时拒收并说明原因
  - 不再写穿目标端的符号链接；不再把部分失败报成成功
  - `wipe` 只打印手动删除路径，**不删也不备份**（本版有**行为断言**守住这条承诺）
- **Windows 停桌面端时的真实死锁**：异步排空两条输出流 + 超时如实报失败 + 复核进程真的没了。
- **状态目录探测竞态**：探测名唯一（pid + 随机），并发进程不再得出不同结论。
- **发布产物不再混入 v2 旧产物**：v2 的 build job 原来对任何 tag 都触发，v3 tag 上会产出 v2 的 exe 与 zip；
  现在只对 `v2*` tag 触发。
  **The v2 build job no longer runs on v3 tags.**
- **单文件产物开启压缩**（体积更小）。

## 变更 / Changed

- **CLI 拆分**：`Program.cs` **3595 → 1060 行**（−70.5%），拆成 7 个 partial 文件；调用点与行为未变。
  **CLI split** into seven partial files, no call-site or behaviour change.
- **备份引擎去重**：Windows 与 Linux 两端**逐字相同**的 6 个方法提成共享基类，两端各减 111 行 ——
  此前「一处修 bug 必须记得改两处」，而**已经漏过一次**。
  **Backup engine de-duplicated** into a shared base; 111 lines removed on each side.
- **删除无人调用的 `IPaths.BackupsRoot`**（接口 + 2 份实现 + 2 条断言，−78 行）。
- **本地就绪度门槛 11 → 13 项**：新增「CI 接线不变量」与「`wipe` 行为诚实性」。

---

## 可选的桥接插件

工具箱本体是独立进程：不注入 dsh，dsh 没装也能用。但有一个事实**只有 dsh 进程自己知道** —— 当前有几个会话正在运行。
这是进程内的运行态，dsh 不落盘，所以磁盘投影里没有它，工具箱只能显示 `unknown`。

| | 不装插件 | 装了插件 |
|---|---|---|
| 会话清单 / token / 命中率 / 速度 | ✅（读磁盘投影） | ✅ |
| **「运行中」标记** | ❌ `unknown` | ✅ 实时 |

它不做什么：❌ 不发模型请求（不消耗 token）· ❌ 不写 dsh 状态 · ❌ 不读会话正文 · ❌ 不联网 · ❌ 不阻塞。

```bash
dsh plugin --profile web add "https://github.com/sakanamaru/dsh-minato"
# 或本地目录（最稳，不依赖网络）
dsh plugin --profile web add "<本仓库路径>/plugin/dsh-minato-bridge"
```

> ⚠️ `desktop` profile 由 dsh 桌面端独占管理，命令行装不进去 —— 请在桌面端的「添加插件」对话框里粘贴上面任一条。

## 产物 / Assets

| 文件 | 说明 |
|---|---|
| `dsh-minato-3.0.0-win-x64-setup.exe` (+`.sha256`) | Windows 安装器 |
| `dsh-minato-win-x64.zip` (+`.sha256`) | Windows 免安装包 |
| `dsh-minato-linux-x64.tar.gz` (+`.sha256`) | Linux x64 包（`./install.sh`） |
| `hashes.txt` / `hashes.txt.asc` | 全部产物的 SHA-256，及其 GPG 签名 |

## 验证 / Verification

- **自动化**（每次提交都跑）：契约测试 **403 项** · GUI 逻辑 **58 项** · 插件测试 **23 项**；
  备份/恢复/删除**真实往返**（隔离根）· 符号链接/junction 双向验证 · 发布物自证（篡改可检出）· 领域层纯净度 ·
  v2.x 发布链完好性。
- **真机**：Windows（GUI 四视图截图 · 安装器静默安装 · 备份/恢复端到端）；Linux（VMware 虚拟机：
  下载 → `sha256sum -c` → `./install.sh` → `version`/`about`/`doctor`/`backup` → `--uninstall` 全部通过）。
- 真机这一步抓到了三个自动化门槛**全都漏掉**的问题：tar 包里没有 `hashes.txt`（安装器因此「未做任何校验」）、
  5 个 shell 脚本丢了可执行位、桥接插件要求用户手工补 patch 行 —— 三个都已在 3.0.0 里修掉。

## 已知边界 / Known limits

- **体积**：自包含单文件约 76 MB（含 .NET 运行时）。（3.0.1 起 GUI 启用裁剪，整包降到约 50 MB。）
- **桥接插件需要 pnpm**（`npm i -g pnpm`）；`desktop` profile 由官方桌面端管理。
- 图形界面基于 Avalonia，因此不是零依赖 —— 项目其余部分是。

## 开发阶段记录 / development stages

> 以下阶段是 3.0.0 在同一天内逐轮收敛的过程记录，**内容都已包含在正式版里**：
> `v3.0.0-preview.1`（V3 CLI 与 GUI 首版）· `preview.2`（审计修复）· `preview.3`（Linux 真机修复）。

## 许可 / License

[MIT](https://github.com/sakanamaru/dsh-minato/blob/main/LICENSE)（代码）。**图标不是 MIT** —— 见 `docs/ASSETS.md`。
GitHub：[@sakanamaru](https://github.com/sakanamaru)
