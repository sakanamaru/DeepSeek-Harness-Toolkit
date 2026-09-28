# Linux 构建与试用指引（v2.8 阶段 3 · **已在真机验收**）

> 状态：**Linux 平台实现已落地，并已在 Ubuntu 26.04 真机验收**（CLI 全部命令、一键装 Node、一键起停、备份/恢复/导出/删除、配置、快捷方式、菜单 ✓）。
> 本文件给出在 Linux 虚拟机（VMware 等）里**从源码构建并试用**的精确步骤、预期输出，
> 以及**已知缺口**——目的是让第一次真机测试尽量少走弯路，而不是宣称已经支持 Linux。

---

## 1. 前提

| 项 | 要求 |
|---|---|
| 发行版 | Ubuntu 22.04 / 24.04 或同级（需要 `ss`，即 iproute2，主流发行版默认自带） |
| .NET SDK | **8.0**（构建需要；运行若用框架依赖模式也需要 8.0 运行时） |
| Node.js | 仅 `install` / `update` 等 dsh 相关命令需要；`backup` / `restore` / `status` / `doctor` 不需要 |

装 SDK（二选一）：
```bash
sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0
# 或者官方脚本：
# curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
```

---

## 2. 构建

在仓库根目录（含 `DeepSeekHarnessToolkit.Core.csproj` 与 `dsh_v2.cs`）：

```bash
dotnet publish DeepSeekHarnessToolkit.Core.csproj -c Release -r linux-x64 \
  --self-contained false -p:PublishSingleFile=true
```

产物：
```
bin/Release/net8.0/linux-x64/publish/DeepSeek Harness Toolkit
```

> 说明：CI 的 `v3-linux` job 现在会产出并校验 `dsh-minato-linux-x64.tar.gz`（两份 CLI + 自包含 GUI + `.desktop` + 图标 + 冒烟脚本 + sha256 ✓），可用 `v3/tools/verify-linux.sh` 校验。下面从源码构建的步骤仍然适用于想自己编译的人。
> `--self-contained false` 表示依赖系统 .NET 8 运行时；若想完全自包含（体积更大），把该参数改成 `true`。

---

## 3. 试用手册（建议按序）

```bash
cd bin/Release/net8.0/linux-x64/publish
chmod +x "DeepSeek Harness Toolkit"

./"DeepSeek Harness Toolkit" about          # 版本/署名/非官方声明
./"DeepSeek Harness Toolkit" doctor         # 体检七类（含 Integrity）
./"DeepSeek Harness Toolkit" status         # 服务三态
./"DeepSeek Harness Toolkit" status --detail  # 追加 PID / 启动时间 / 运行时长
./"DeepSeek Harness Toolkit" profilecheck   # 扫 ~/.dsh/profiles，报缺 maxDepth 的条目
./"DeepSeek Harness Toolkit" backup-list --detail
./"DeepSeek Harness Toolkit" backup         # 备份 ~/.dsh
```

期望：以上命令输出的机器标记与 Windows 侧**同名同义**（`STATUS_UP`、`DOCTOR_*`、`PROFILECHK_*`、`BACKUP_OK` …），
因为核心逻辑是平台无关的，平台差异只在接缝实现里。

---

## 4. 平台差异与已知缺口（诚实清单）

| 能力 | Linux 现状 | 备注 |
|---|---|---|
| 数据目录 | `$DSH_HOME` → `$HOME/.dsh` | 与 Windows 侧 `DATA_DIR=".dsh"` 语义一致 |
| 桌面目录 | `$XDG_DESKTOP_DIR` → `$HOME/Desktop` | |
| 监听端口→PID | 解析 `ss -ltnp` 的 `pid=` | 依赖 iproute2；取不到返回 0 |
| 监听进程身份 | 读 `/proc/<pid>/cmdline` | 等价于 Windows 侧的 WMI 命令行判定 |
| 终止进程 | `kill -TERM` → 300ms → `kill -KILL` | 尽力连带子进程；**未在真机验收** |
| 定位 dsh | **PATH 扫描**（Linux 无 `where`） | |
| node/npm/dsh 版本 | 直接执行 `node` / `npm` / `dsh` | 无需 Windows 的 `cmd.exe` 包装 |
| npm 安装/卸载 | 直接执行 `npm install -g …` / `npm uninstall -g …` | |
| winget 装 Node | **返回 -1** | Linux 无 winget → 交回"请手动安装 Node"提示；接 apt/dnf 属改用户系统，须用户显式确认后再做 |
| 桌面快捷方式 | 写 `~/.local/share/applications/*.desktop` | Linux 没有 .lnk |
| **工作区自动探测** | **返回 null（不猜）** | 需要手动指定：`config-set ws /path/to/workspace` |
| 桌面/下载目录误用警告 | Windows 规则（盘符根/`%TEMP%`/注册表取下载目录） | 在 Linux 上可能失效或静默跳过；**属阶段 3 收尾项** |
| GUI（经典 WinForms 七页） | **不构建、不支持** | 见设计稿 §7；跨平台 GUI 由 V3 的 Avalonia 面板承担（`v3/gui/Dsht.Gui.Avalonia`，可编译并运行 ✓） |

---

## 5. 出问题怎么报

请附上：
1. `dotnet --info` 前 6 行（SDK/运行时版本）
2. 构建命令**完整输出**（含报错）
3. 出问题那条命令的**完整输出**（不要截断标记行）
4. `uname -a` 与发行版版本

---

## 6. 与仓库纪律的关系

- 本文件**不在**发布 zip 的资产清单里（16 项固定清单未变），因此不影响已发布产物的校验链。
- Linux 侧代码位于 `src/Platform/Linux/Program.Platform.Linux.cs`；接缝接口与持有者在 `src/Core/Program.PlatformSeams.cs`；
  Windows 实现体在 `src/Platform/Windows/Program.Platform.Windows.cs`。
- 本地已有两道守卫可随时自查：`tests/verify_move_only.ps1`（成员签名不丢）、`tests/verify_core_portable.ps1`（核心零 Windows-only 编译期 API）。