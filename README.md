# DeepSeek Harness Unofficial Launcher V2.0.0

**中文**：DeepSeek Harness（dsh）Web 界面的第三方非官方启动 / 运维小工具：安装、启动、监控、卸载，附带数据备份/恢复，双击即用。
**English**: A third-party, unofficial launcher / ops tool for the DeepSeek Harness (dsh) Web UI: install, start, monitor, uninstall, plus data backup & restore — double-click and go.

> ⚠️ **中文**：本项目为**非官方**工具，与 DeepSeek 官方无关。
> ⚠️ **English**: This project is **unofficial** and is not affiliated with DeepSeek.

## 署名 / Credits

- 🧩 **v1 脚本协助 : SOGR-Momono Dango（QwenPaw/DeepseekAPI-V4-Flash-0731）**
- 🚀 **v2 重构封装 : DeepSeek DSH （DSH/DeepseekAPI-V4-Flash-0731）**
- 📦 **GitHub    : @sakanamaru  https://github.com/sakanamaru**

## 功能一览 / Features

| 功能 / Feature | 说明 / Description |
| --- | --- |
| 智能启动 / Smart Start | 打开即检测服务状态：已在运行 → 直接进状态页；未运行 → 5 秒倒计时自动启动/安装<br>Detects service status on launch: running → status page; stopped → 5-second countdown auto-start/install |
| 安装 / 修复 / Install & Repair | 国内镜像源 npmmirror，失败自动换官方源重试，不污染全局 npm 配置<br>Uses npmmirror mirror, retries official registry on failure, never touches your global npm config |
| 状态监控 / Status Monitor | 每 3 秒自动刷新服务状态/端口/运行时长，服务掉线红字提醒；按 1 返回 / 2 打开 WebUI<br>Auto-refreshes service status/port/uptime every 3s; red alert on disconnect; 1=back / 2=open WebUI |
| 备份 / 恢复 / Backup & Restore | 一键备份数据目录到 `backup\`（可添加**多个工作区**：自动探测并校验，之后逐个输入路径、留空结束，备份包按 `_workspace\名称\` 分包存放并可逐一恢复），自动跳过 node_modules 与自身备份目录；支持列表恢复、跨电脑导入、直接打开备份文件夹<br>One-click backup of the data directory to `backup\` (supports **multiple workspaces**: auto-detected, then add paths one by one, empty Enter to finish; stored separately under `_workspace\name\`, restorable one by one); auto-skips node_modules and its own backup folders; supports list restore, cross-PC import, and opening the backup folder |
| 卸载 / Uninstall | 默认保留数据；清除数据需两步确认（当天日期 + `yes`）、**清除前自动备份**；dsh web 运行中会阻止清除（避免文件占用）<br>Data kept by default; wiping needs two-step confirmation (today's date + `yes`) with **auto-backup first**; wiping is blocked while dsh web is running (avoid file locks) |
| 数据定位 / Data Location | 自动识别 dsh 数据目录（优先 `~/.dsh`，兼容 `%APPDATA%` 等位置）<br>Auto-locates the dsh data directory (prefers `~/.dsh`, falls back to `%APPDATA%` etc.) |
| 访问入口 / Entry | `127.0.0.1` / `localhost` 可选并记忆：入口打开异常（可能为浏览器残留旧缓存所致）时，一键切换即可<br>Pick and remember `127.0.0.1` / `localhost`: if the entry opens abnormally (often stale browser cache), switch with one key |
| 长路径支持 / Long Paths | 备份/恢复内置 `\\?\` 长路径支持（>260 字符），并自动跳过嵌套的备份包目录（`dsh-data-*`）<br>Built-in long path support (`\\?\`, >260 chars) for backup/restore; nested backup packages (`dsh-data-*`) are auto-skipped |
| 多语言 / Multi-language | 跟随系统 / 简体中文 / English，选择持久化<br>Follow system / Simplified Chinese / English, persisted |
| 署名 / Credits | 程序内及文件属性均标注 v1 / v2 贡献者<br>v1 / v2 contributors credited in-app and in file properties |

## 和官方部署方式的关系 / Relation to the Official Deployment

官方推荐的部署方式其实只有两步，并不复杂：/ The official way is just two commands:

```
npm install -g @deepseek-ai/dsh
dsh web
```

本工具**不取代官方方式**，而是为「不想（或不方便）使用终端」的使用场景服务。
This tool does **not replace** the official way; it serves those who prefer not to (or cannot) use a terminal.

| | 官方方式（npm 命令）/ Official (npm CLI) | 本工具 / This tool |
| --- | --- | --- |
| 适合人群 / Target users | 熟悉终端的开发者<br>Developers comfortable with the terminal | 普通用户 / 批量装机 / 远程协助<br>Regular users / batch installs / remote assistance |
| 安装 / Install | 先装 Node.js，再敲命令<br>Install Node.js first, then type commands | 双击 exe：自动检测环境、自动安装、自动启动<br>Double-click the exe: detects the environment, installs and starts automatically |
| 日常使用 / Daily use | 每次手动开终端、敲命令、开浏览器<br>Manually open a terminal and browser every time | 打开即检测服务：自动启动并打开浏览器，每 3 秒监控状态<br>Detects on launch: auto-starts, opens the browser, monitors every 3s |
| 运维能力 / Ops | 无<br>None | 备份/恢复/跨电脑导入、卸载清理（两步确认）、入口切换、中英文界面<br>Backup/restore/cross-PC import, uninstall (two-step confirm), entry switch, bilingual UI |
| 故障排查 / Troubleshooting | 面对终端报错<br>Raw terminal errors | 中文提示、体检页、自检报告（selftest）<br>Friendly messages, health page, selftest report |

**需要知道的边界（诚实说明）/ Honest limits:**

1. **非官方维护**：本项目为第三方非官方维护，不承诺与未来 dsh 版本的兼容性；若 dsh 日后变更默认端口 / 启动命令 / 数据目录，本工具需要跟进更新（目前这些点位保持稳定）。<br>**Unofficial maintenance**: no compatibility promise with future dsh versions; if dsh ever changes its default port / start command / data directory, this tool must be updated (those points are stable so far).
2. **信任门槛**：分发的是 Windows exe，天然存在信任门槛——因此本项目**完全开源（MIT）**，并随发布提供 `hashes.txt`（SHA-256 指纹），任何人可核对发布物是否一致。<br>**Trust boundary**: distributing a Windows exe carries an inherent trust cost — hence this project is **fully open source (MIT)** and ships `hashes.txt` (SHA-256 fingerprints) so anyone can verify the release.
3. **定位**：如果你是终端熟练用户，直接用官方 npm 命令更轻快；这个工具是给「不想碰终端」的人准备的。<br>**Positioning**: if you are comfortable with the terminal, the official npm commands are leaner; this tool is for people who do not want to touch one.

## 使用 / Usage

**中文**：双击 `DeepSeek Harness Unofficial Launcher V2.0.0.exe` 即可；或命令行：
**English**: Double-click `DeepSeek Harness Unofficial Launcher V2.0.0.exe`, or use the command line:

```
DeepSeek Harness Unofficial Launcher V2.0.0.exe install|start|uninstall|check|about|help
```

无参数启动为交互菜单：首次运行 5 秒倒计时自动选择（可按键接管），之后每次打开自动启动 Web 界面；服务已在运行时直接进入状态页。
Launching without arguments opens the interactive menu: first run auto-selects after a 5-second countdown (interruptible), later launches auto-start the Web UI, and go straight to the status page when the service is already running.

**关于工作区 / About workspaces:** 备份时会自动探测工作区（exe 上两级目录，并拒绝系统/用户目录等明显不合理位置）；也可在主菜单 **7 访问入口 → 3 设置工作区路径** 手动指定并持久保存（`launcher.config` 的 `ws=` 行），且支持**多个工作区**逐个输入路径（直接回车结束），一并打包到 `_workspace\名称\` 下、恢复时逐一还原。
Backup auto-detects the workspace (two levels above the exe, rejecting obvious system/user dirs); you can also set it manually and persistently via menu **7 Entry → 3 Set workspace path** (the `ws=` line in `launcher.config`). **Multiple workspaces** are supported — add paths one by one (empty Enter to finish) — packed under `_workspace\name\` and restored one by one.

### 第一次用（三步）/ First run (3 steps)

1. **解压**上传包到独立文件夹（如 `D:\工具\`）——程序的备份与配置写在 exe 所在目录，放在桌面上会让桌面变乱<br>**Unzip** the release into its own folder (e.g. `D:\tools\`) — backups and config live next to the exe; putting it on the Desktop makes a mess
2. **双击 exe**：首次运行自动安装 dsh（自动选用国内镜像源，一般 1–3 分钟），安装成功后再双击即可<br>**Double-click the exe**: it auto-installs dsh on first run (npmmirror by default, ~1–3 min); run it again afterwards
3. 安装完成后在菜单中选择 **2 启动 Web 界面**，浏览器自动打开<br>Then choose **2 Start Web UI** in the menu and the browser opens automatically

### 常见问题 / FAQ

| 现象 / Issue | 解决 / Fix |
| --- | --- |
| 打开 Web 界面 403 / 空白 / 403 or blank page | 菜单选 **7 访问入口**，切换 `127.0.0.1` ↔ `localhost`（浏览器把两者当不同站点，旧缓存会导致异常）<br>Menu **7 Entry**, switch `127.0.0.1` ↔ `localhost` (the browser treats them as different sites; stale cache causes issues) |
| 卸载/清除数据时提示删除失败 / Delete failed during uninstall/wipe | 先关闭 dsh web 服务窗口（文件被占用），再重新执行；仍失败看 `logs\launcher.log`<br>Close the dsh web window first (file locks), retry; see `logs\launcher.log` if it still fails |
| 备份失败 / Backup failed | 查看 exe 目录 `logs\launcher.log` 中的真实原因<br>Check `logs\launcher.log` next to the exe for the real reason |
| 备份失败（路径太长 PathTooLongException）/ Backup failed (PathTooLongException) | 已内置 `\\?\` 长路径支持并自动跳过嵌套备份包（`dsh-data-*` 目录）；若日志仍显示路径问题，把该目录移出工作区再试<br>Long path support and `dsh-data-*` skipping are built in; if the log still shows path issues, move that folder out of the workspace |
| 备份时提示输入附加路径 / Prompted for extra paths | 自动探测未命中或想备份其他目录时，逐个输入要附加的工作区路径（留空结束）；也可在 **7 访问入口 → 3** 里预设常备工作区<br>Type each extra workspace path (empty Enter to finish), or preset one under **7 Entry → 3** |

## 从源码构建 / Build from Source

需要 Windows 自带的 .NET Framework 4.x（Win10 / Win11 默认已安装）。
Requires the built-in .NET Framework 4.x on Windows (preinstalled on Win10/Win11).

```
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /optimize+ /target:exe /win32icon:icon.ico /out:"DeepSeek Harness Unofficial Launcher V2.0.0.exe" dsh_v2.cs
```

或双击本目录 `build_exe.cmd`。 / Or double-click `build_exe.cmd` in this directory.

## 目录结构 / Directory Layout

```
DeepSeek Harness Unofficial Launcher V2.0.0.exe   主程序（带图标）/ Main program (with icon)
dsh_v2.cs           v2 源码（C#5，单文件，无第三方依赖）/ v2 source (C#5, single file, no third-party deps)
build_exe.cmd       重编译脚本 / Rebuild script
icon.ico            程序图标源文件 / Program icon source
logo.png            logo 源图（PNG，由 ChatGPT 生成）/ Original logo PNG (generated with ChatGPT)
hashes.txt          发布文件 SHA-256 校验清单 / SHA-256 manifest of released files
backup/             数据备份目录（已被 .gitignore 排除，切勿提交）/ Backup dir (gitignored — never commit)
logs/               运行错误日志目录（已被 .gitignore 排除，切勿提交）/ Error log dir (gitignored — never commit)
```

## 错误日志 / Error Log

- 运行中的错误（备份/恢复/卸载异常、进程启动失败等）会自动写入 exe 所在目录的 `logs\launcher.log`（带时间戳，超过 200KB 自动重置）<br>Runtime errors (backup/restore/uninstall failures, process start failures, etc.) are written to `logs\launcher.log` next to the exe (timestamped, auto-reset over 200 KB)
- 日志只记录错误信息与涉及的文件路径，不包含密码/API 凭据；已被 `.gitignore` 排除，不会提交<br>The log records error messages and file paths only — never passwords/API credentials; it is gitignored and never committed

## 安全提示 / Security Notes

- 卸载「清除全部数据」会删除 dsh 数据目录（`~/.dsh`，含会话与 API 凭据），程序会在清除前自动备份到 `backup\` 目录<br>Uninstall "Wipe all data" deletes the dsh data directory (`~/.dsh`, including sessions and API credentials) — the tool auto-backs it up to `backup\` first
- 本工具仅操作本机数据，源码不包含任何凭据或个人信息<br>This tool only touches local data; the source contains no credentials or personal information

## 转载与署名 / Redistribution & Credits（请务必阅读 / Please read）

本项目以 MIT 许可开源，代码可自由使用、修改、分发，但必须遵守以下约定。
This project is open source under the MIT License. You are free to use, modify and redistribute it, but you MUST follow these rules:

1. **保留署名 / Keep the credits**：程序内（启动横幅 / 关于页 / 文件属性）与本文档中的 v1 / v2 贡献者署名及 GitHub 链接不得删除或替换为他人。<br>The v1/v2 contributor credits and GitHub links in the app (startup banner / About page / file properties) and in this document must not be removed or replaced with others.
2. **保留声明 / Keep the claims**：「非官方 / Unofficial」标示及本 LICENSE 版权声明须随副本一起分发。<br>The "Unofficial" notice and this LICENSE copyright statement must be distributed with every copy.
3. **如实来源 / Truthful attribution**：商用或二次发布请注明来源仓库与原作者；删除署名即视为侵权，作者保留投诉（含 DMCA）与法律追责的权利。<br>For commercial or redistributed releases, credit the source repository and original authors; removing credits is treated as infringement, and the authors reserve the right to file complaints (incl. DMCA) and pursue legal action.
4. **校验发布 / Verify releases**：本仓库 `hashes.txt` 记录了官方发布文件的 SHA-256 指纹，任何"自称官方编译"的二进制均可通过比对指纹甄别。<br>`hashes.txt` in this repository records SHA-256 fingerprints of official release files; any binary claiming to be "officially compiled" can be verified against it.

## 许可 / License

[MIT License](LICENSE)

## 致谢 / Acknowledgements

- [DeepSeek Harness (dsh)](https://www.npmjs.com/package/@deepseek-ai/dsh)
- 图标 / Logo：由 ChatGPT（OpenAI）协助生成 / designed with the assistance of ChatGPT (OpenAI)
- v1 脚本协助 : SOGR-Momono Dango（QwenPaw/DeepseekAPI-V4-Flash-0731）
- v2 重构封装 : DeepSeek DSH （DSH/DeepseekAPI-V4-Flash-0731）
- GitHub    : @sakanamaru  https://github.com/sakanamaru