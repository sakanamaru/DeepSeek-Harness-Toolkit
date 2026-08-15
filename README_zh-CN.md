# DeepSeek Harness Toolkit V2.0.0

<div align="center">

**[English](README.md) · [简体中文](README_zh-CN.md)**

<img src="logo.png" alt="DeepSeek Harness Toolkit" width="220"/>

**DeepSeek Harness 非官方 Windows 安装、监控与数据管理工具**

</div>

DeepSeek Harness（dsh）Web 界面的第三方非官方启动 / 运维小工具：
安装、启动、监控、卸载，附带数据备份/恢复，**双击即用**。

**相比直接跑 npm 包，它多了这些特色：**

- 🔧 **安装 / 修复** — 默认官方源 npmjs.org，国内镜像可选手动；失败自动换另一源重试，不污染全局 npm 配置
- 📊 **智能启动 + 实时监控** — 已安装时 5 秒倒计时自动启动；每 3 秒自动刷新服务状态，掉线红字提醒
- 💾 **备份 / 恢复** — 一键备份数据目录到 `backup\`，支持**多工作区**（按 `_workspace\名称\` 分包、可逐一恢复）与**跨电脑导入**，长路径安全
- 🛡️ **清除数据三重防护** — 两步确认（日期 + yes）且**清除前自动备份**；Web 服务运行中禁止清除；随包分发的根目录标记让"单独复制的 exe"永久无法清除
- 🌍 **单 exe 双语界面** — 简体中文 / English，基于 .NET Framework 4.x（Win10/11 自带），无需额外安装

> ⚠️ 本项目为**非官方**工具，与 DeepSeek 官方无关。

## 署名 / Credits

- 🧩 **v1 脚本协助 : SOGR-Momono Dango（QwenPaw/DeepseekAPI-V4-Flash-0731）**
- 🚀 **v2 重构封装 : DeepSeek DSH （DSH/DeepseekAPI-V4-Flash-0731）**
- 📦 **GitHub    : @sakanamaru  https://github.com/sakanamaru**

## 功能一览

| 功能 | 说明 |
| --- | --- |
| 智能启动 | 打开即检测服务状态：已在运行 → 直接进状态页；未运行 → 5 秒倒计时自动启动（仅当 dsh 已安装；未安装时菜单等待你选择，**不会自动安装**） |
| 安装 / 修复 | 安装时询问源：**默认官方源 npmjs.org**，国内镜像 npmmirror 可选；失败自动换另一源重试，不污染全局 npm 配置 |
| 状态监控 | 每 3 秒自动刷新服务状态/端口/运行时长，服务掉线红字提醒；按 1 返回 / 2 打开 WebUI |
| 备份 / 恢复 | 一键备份数据目录到 `backup\`（可添加**多个工作区**：自动探测并校验，之后逐个输入路径、留空结束，备份包按 `_workspace\名称\` 分包存放并可逐一恢复），自动跳过 node_modules 与自身备份目录；支持列表恢复、跨电脑导入、直接打开备份文件夹 |
| 卸载 | 默认保留数据；清除数据需两步确认（当天日期 + `yes`）、**清除前自动备份**；dsh web 运行中会阻止清除（避免文件占用） |
| 数据定位 | 自动识别 dsh 数据目录（优先 `~/.dsh`，兼容 `%APPDATA%` 等位置） |
| 访问入口 | `127.0.0.1` / `localhost` 可选并记忆：入口打开异常（可能为浏览器残留旧缓存所致）时，一键切换即可 |
| 长路径支持 | 备份/恢复内置 `\\?\` 长路径支持（>260 字符），并自动跳过嵌套的备份包目录（`dsh-data-*`） |
| 多语言 | 跟随系统 / 简体中文 / English，选择持久化 |

## 和官方部署方式的关系

官方推荐的部署方式其实只有两步，并不复杂：

```
npm install -g @deepseek-ai/dsh
dsh web
```

本工具**不取代官方方式**，而是为「不想（或不方便）使用终端」的使用场景服务：

| | 官方方式（npm 命令） | 本工具 |
| --- | --- | --- |
| 适合人群 | 熟悉终端的开发者 | 普通用户 / 批量装机 / 远程协助 |
| 安装 | 先装 Node.js，再敲命令 | 双击 exe：自动检测环境并自动启动；是否安装 dsh 由你选择（按 1） |
| 日常使用 | 每次手动开终端、敲命令、开浏览器 | 打开即检测服务：自动启动并打开浏览器，每 3 秒监控状态 |
| 运维能力 | 无 | 备份/恢复/跨电脑导入、卸载清理（两步确认）、入口切换、中英文界面 |
| 故障排查 | 面对终端报错 | 中文提示、体检页、自检报告（selftest） |

**需要知道的边界（诚实说明）：**

- 本项目为**第三方非官方维护**，不承诺与未来 dsh 版本的兼容性；若 dsh 日后变更默认端口 / 启动命令 / 数据目录，本工具需要跟进更新（目前这些点位保持稳定）
- 分发的是 Windows exe，天然存在信任门槛——因此本项目**完全开源（MIT）**，并随发布提供 `hashes.txt`（SHA-256 指纹），任何人可核对发布物是否一致
- 如果你是终端熟练用户，直接用官方 npm 命令更轻快；这个工具是给「不想碰终端」的人准备的

## 使用

双击 `DeepSeek Harness Toolkit V2.0.0.exe` 即可；或命令行：

```
DeepSeek Harness Toolkit V2.0.0.exe install|start|uninstall|check|about|help
```

无参数启动为交互菜单：dsh 已安装时首次运行 5 秒倒计时自动启动（可按键接管），之后每次打开也自动启动 Web 界面；
dsh **未安装**时菜单等待你选择（按 1 安装），不会自动安装；服务已在运行时直接进入状态页。

**关于工作区**：备份时会自动探测工作区（exe 上两级目录，并拒绝系统/用户目录等明显不合理位置）；
也可在主菜单 **7 访问入口 → 3 设置工作区路径** 手动指定并持久保存（`launcher.config` 的 `ws=` 行），
且支持**多个工作区**逐个输入路径（直接回车结束），一并打包到 `_workspace\名称\` 下、恢复时逐一还原。

### 第一次用（三步）

1. **解压**上传包到独立文件夹（如 `D:\工具\`）——程序的备份与配置写在 exe 所在目录，放在桌面上会让桌面变乱
2. **双击 exe**：dsh 未安装 → 菜单等待你选择，按 **1** 安装（默认官方源，镜像可选，一般 1–3 分钟），安装成功后再双击即可
3. 安装完成后在菜单中选择 **2 启动 Web 界面**，浏览器自动打开

### 常见问题

| 现象 | 解决 |
| --- | --- |
| 打开 Web 界面 403 / 空白 | 菜单选 **7 访问入口**，切换 `127.0.0.1` ↔ `localhost`（浏览器把两者当不同站点，旧缓存会导致异常） |
| 卸载/清除数据时提示删除失败 | 先关闭 dsh web 服务窗口（文件被占用），再重新执行；仍失败看 `logs\launcher.log` |
| 备份失败 | 查看 exe 目录 `logs\launcher.log` 中的真实原因 |
| 备份失败（路径太长 PathTooLongException） | 已内置 `\\?\` 长路径支持并自动跳过嵌套备份包（`dsh-data-*` 目录）；若日志仍显示路径问题，把该目录移出工作区再试 |
| 备份时提示输入附加路径 | 自动探测未命中或想备份其他目录时，逐个输入要附加的工作区路径（留空结束）；也可在 **7 访问入口 → 3** 里预设常备工作区 |

## 从源码构建

需要 Windows 自带的 .NET Framework 4.x（Win10 / Win11 默认已安装）：

```
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /optimize+ /target:exe /win32icon:icon.ico /out:"DeepSeek Harness Toolkit V2.0.0.exe" dsh_v2.cs
```

或双击本目录 `build_exe.cmd`。

**可复现发布（源码即产物）**：每个 GitHub Release 的 exe 均由 **GitHub Actions CI** 从本仓库源码自动编译生成，并在同一流水线里重新生成 `hashes.txt`——仓库自身不存放任何二进制文件。

## 目录结构

```
DeepSeek Harness Toolkit V2.0.0.exe   主程序（带图标）
dsh_v2.cs           v2 源码（C#5，单文件，无第三方依赖）
build_exe.cmd       重编译脚本
icon.ico            程序图标源文件
logo.png            logo 源图（PNG，由 ChatGPT 生成）
.dsh_launcher_root  安装标记（随包分发，防误删验证）
README.md           说明文档（中英双语）
README_zh-CN.md     说明文档（简体中文版）
hashes.txt          发布文件 SHA-256 校验清单
backup/             数据备份目录（已被 .gitignore 排除，切勿提交）
logs/               运行错误日志目录（已被 .gitignore 排除，切勿提交）
```

## 错误日志

- 运行中的错误（备份/恢复/卸载异常、进程启动失败等）会自动写入 exe 所在目录的 `logs\launcher.log`（带时间戳，超过 200KB 自动重置）
- 日志只记录错误信息与涉及的文件路径，不包含密码/API 凭据；已被 `.gitignore` 排除，不会提交

## 安全提示

- 卸载「清除全部数据」会删除 dsh 数据目录（`~/.dsh`，含会话与 API 凭据），程序会在清除前自动备份到 `backup\` 目录
- **删除操作三重防误删**：dsh Web 服务运行中直接**阻止卸载**；清除数据要求启动器根目录存在标记文件 `.dsh_launcher_root`（须为上一次运行所留，防止 exe 被单独复制到其他位置后误删）**且**目标目录含 dsh 数据特征（settings.yaml / credentials.yaml / sessions 等）；任一不满足即拒绝删除
- 本工具仅操作本机数据，源码不包含任何凭据或个人信息

## 转载与署名（请务必阅读）

本项目以 MIT 许可开源，代码可自由使用、修改、分发，但必须遵守以下约定：

1. **保留署名**：程序内（启动横幅 / 关于页 / 文件属性）与本文档中的 v1 / v2 贡献者署名及 GitHub 链接不得删除或替换为他人
2. **保留声明**：「非官方 / Unofficial」标示及本 LICENSE 版权声明须随副本一起分发
3. **如实来源**：商用或二次发布请注明来源仓库与原作者；删除署名即视为侵权，作者保留投诉（含 DMCA）与法律追责的权利
4. **校验发布**：本仓库 `hashes.txt` 记录了官方发布文件的 SHA-256 指纹，任何"自称官方编译"的二进制均可通过比对指纹甄别

## 许可

[MIT License](LICENSE)

## 致谢

- [DeepSeek Harness (dsh)](https://www.npmjs.com/package/@deepseek-ai/dsh)
- 图标 / Logo：由 ChatGPT（OpenAI）协助生成
- v1 脚本协助 : SOGR-Momono Dango（QwenPaw/DeepseekAPI-V4-Flash-0731）
- v2 重构封装 : DeepSeek DSH （DSH/DeepseekAPI-V4-Flash-0731）
- GitHub    : @sakanamaru  https://github.com/sakanamaru