# V3 底层重构 · 开发者说明

> 本目录是 **v2.x 的平行树**：`main` 上 v2.x 的 `dsh_v2.cs` + `src/**` + `verify.ps1` + 16 项发布清单
> **保持不变、随时可发布**；V3 在这里独立演进，达到切换门槛后再接管。
> 设计与决策依据：`04-规划文档\V3.0底层重构方案（桌面端驱动）.md`。

---

## 1. 目录结构

```
v3/
  src/
    Dsht.Domain/            纯领域：零 IO、零平台、零时钟耦合（有守卫强制）
      Model/                AppKind / ServiceState / ServiceReport / DocItem / ProfileFinding /
                            IntegrityVerdict / BackupEntry / BackupKind / DirSnapshot / ProfileFile ...
      Abstractions/         IServiceTarget / IPortProbe / IHttpProbe / IProcessQuery / IFileSystemQuery /
                            IToolchainQuery / IIntegritySource / IProfileSource / IBackupSource / IPaths
      Services/             ServiceJudge / UptimeFormatter / BackupRetention / BackupPackage /
                            ProfileScanner / ManifestParser / IntegrityJudge / DoctorSummary /
                            SizeFormatter / ReportSanitizer / BackupAge
      Targets/              WebTarget / UnknownTarget / ReservedTarget / CompositeServiceTarget
    Dsht.Platform.Windows/  Windows 实现（netstat / Get-CimInstance / where / taskkill / %APPDATA%）
    Dsht.Platform.Linux/    Linux 实现（ss / /proc/<pid>/cmdline / ps / PATH 扫描 / $XDG_* / $DSH_HOME）
    Dsht.Cli/               组合根（自写 ServiceRegistry，零第三方 DI）+ 命令面 + 平台装配
  tests/
    Dsht.Contracts.Tests/   契约测试宿主（零第三方断言，206 项）
    verify_domain_pure.ps1  领域层纯净度守卫（扫描前剥离注释）
    compare_markers.ps1     与 v2.x 的标记行契约比对（可 -Fixtures 造受控备份）
    verify_release.ps1      发布物校验（v2.x verify.ps1 等价物，含校验器自证）
```

---

## 2. 本地怎么构建与验证（**不需要 .NET SDK**）

V3 的代码刻意保持 **C#5 兼容**，因此可以用现役的 `csc`（.NET Framework 4.x）把**整个 V3 树**编成一个 exe 在本地跑：

```powershell
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$src = @(Get-ChildItem v3\src -Recurse -Filter *.cs | ForEach-Object FullName)
& $csc /nologo /target:exe /warn:4 /out:"$env:TEMP\dsht_v3.exe" $src
& "$env:TEMP\dsht_v3.exe" status --detail
& "$env:TEMP\dsht_v3.exe" doctor
& "$env:TEMP\dsht_v3.exe" describe
```

三道门禁（都在 `v3/tests/`）：

```powershell
# ① 领域层纯净度（零 IO / 零平台 / 零时钟耦合）
powershell -ExecutionPolicy Bypass -File v3\tests\verify_domain_pure.ps1 -Repo .

# ② 与 v2.x 的标记行契约比对（需要仓库根已构建 v2.x exe；-Fixtures 造受控备份覆盖 detail 分支）
powershell -ExecutionPolicy Bypass -File v3\tests\compare_markers.ps1 -Repo .
powershell -ExecutionPolicy Bypass -File v3\tests\compare_markers.ps1 -Repo . -Fixtures
powershell -ExecutionPolicy Bypass -File v3\tests\compare_markers.ps1 -Repo . -Heavy   # 追加真实 backup 比对（每次约写 400MB，跑完自动清理）

# ③ 发布物校验（-Build 本地造发布物；-SelfTest 篡改一字节证明校验器有效）
powershell -ExecutionPolicy Bypass -File v3\tests\verify_release.ps1 -Repo . -Build -SelfTest
```

> 注意：`-ExecutionPolicy Bypass` 不能省——默认执行策略常禁止直接运行 `.ps1`（会报 UnauthorizedAccess）。

`dotnet`（net8.0）路径由 CI 负责：`.github/workflows/build-release.yml` 的 `v3-contracts` job
在 **windows-latest + ubuntu-latest** 双平台跑 `dotnet build` + 契约测试。

---

## 3. 已实现的命令面（标记行与 v2.x 逐字对齐）

| 命令 | 标记 |
|---|---|
| `status` / `status --detail` | `STATUS_UP` / `STATUS_STARTING` / `STATUS_DOWN` + `STATUS_PID` / `STATUS_START` / `STATUS_UPTIME` |
| `profilecheck [--dir X] [--file Y] [--vendor] [--abs]` | `PROFILECHK_WARN` / `_TOTAL` / `_SKIPPED_VENDOR` / `_FIX` / `_OK` |
| `backup-list [--detail]` | `BACKUP_LIST_OK` + 裸路径行 + `BACKUP_ITEM` |
| `doctor` | `DOCTOR_OK` / `DOCTOR_WARN` / `DOCTOR_ERROR` + `[级别] 类别 描述` |
| `restore --dry-run [--path <dir>]` | `DRYRUN_OK` + `DRYRUN_SRC` + 每个作用域 `DRYRUN_SCOPE`/`_NEW`/`_OVERWRITE`/`_KEEP`/`_BYTES` + `DRYRUN_TOTAL` + `DRYRUN_NOTE`（失败 `DRYRUN_FAIL 原因`） |
| `config-get` | `CONFIGGET_OK` + `CONFIG <key> <value>` × 11 |
| `config-set <key> <value>` | `CONFIGSET_OK <key>` / `CONFIGSET_FAIL <reason>` |
| `bootdiag --from <file>` | `BOOTDIAG_OK`/`_FAIL` + `_KIND`/`_PLUGIN`/`_ENTRY`/`_FILE`/`_LINE`/`_HINT`（未识别时 `_FIRST`） |
| `backup` | `BACKUP_OK <路径>` / `BACKUP_FAIL <原因>`（真实写盘；比对需 `-Heavy`，目录名含时间戳会归一化） |
| `restore --path <dir>`（非 dry-run） | 校验路径 → `RESTORE_FAIL <原因>`（no-path/outside/invalid）；**校验通过后仍明确拒绝**真实写入 |
| `backup-export` / `backup-delete` | `BKEXPORT_FAIL`/`BKDEL_FAIL 校验失败: <原因>`；**真实复制/删除明确拒绝** |
| `check` | 横幅 + `Node.js`/`npm`/`dsh`/`dsh 版本`/`dsh 最新`/`Web 服务`/`UI 语言` 七行（GUI 检查页数据源） |
| `selftest [<report>]` | 写自检报告并打印 `report -> <路径>`（报告正文 11 行与 v2.x 一致；产品标识行本就不同） |
| `describe`（V3 独有） | 说明"考虑过哪些形态、为什么暂时观测不到" |
| `version`（V3 独有） | `DSHT_VERSION <版本>` |

比对工具当前结论：**19/19 对齐**（另有 1 项 `backup` 需 `-Heavy`，届时 20/20）（doctor 的 Integrity 行按规则忽略，见该脚本注释）。

---

## 4. 服务模型：为什么"未识别形态"是一等公民

驱动事件是 **dsh 可能出桌面端**：桌面端未必监听 3080，甚至可能走 stdio/命名管道。
因此 V3 不把"dsh 在跑"等同于"3080 有监听"，而是：

```csharp
enum AppKind { Unknown, Web, Headless, Acp, Desktop }
interface IServiceTarget { AppKind Kind; bool IsAvailable(); ServiceReport Probe(); int FindPid(); string Describe(); }
```

- `WebTarget`：真实观测（端口 → HTTP → 监听进程身份，懒求值）
- `ReservedTarget`：**形态已承认但暂无可观测事实** → 报 Down + 说明原因，**绝不假装 Ready**
- `CompositeServiceTarget`：按 `Ready > Listening > Down`、同状态"已知形态优先于 Unknown"确定性择一；
  全 Down 时报 Unknown 并列出"已尝试的形态"

待 dsh 桌面端/ACP 的真实形态可观测（进程名、IPC 通道）后，把对应的 `ReservedTarget` 换成真实实现即可，
**上层判定、报告与 CLI 都不需要改**。

---

## 5. 诚实边界（不要高估当前进度）

| 项 | 现状 |
|---|---|
| Linux 实现 | **只到"编译过 + 纯逻辑有单测"**（ss 解析、DSH_HOME 路径解析等已验）；真机运行需 ubuntu CI job，而 CI 需推送才能触发 |
| `doctor --report` | **未移植**（v2.x 的报告含配置/日志摘要） |
| `Environment.OSVersion.VersionString` | .NET Framework 与 net8 下字符串不同 → 将来 V3 真正用 net8 发布时需要归一化 |
| headless / acp / desktop | **预留**，无可观测事实前不实现猜测逻辑 |
| macOS | 未开始（设计稿决策：Linux 优先，macOS 视需求后补） |
| 命令面广度 | 已覆盖 GUI 消费的主要命令（含 `restore --dry-run` 预览、`selftest`、`check`、真实 `backup`）；非交互真实 `restore`/`backup-export`/`backup-delete` 的**写操作**尚未移植（校验路径已对齐，写操作**明确拒绝**而不是静默失败） |
| GUI | Windows-only WinForms 保持不变；跨平台 GUI 只留架构能力（见设计稿 §7） |
| **`DSH_HOME` 环境变量** | **唯一一处刻意偏离 v2.x 的行为**：Windows 侧也优先读 `$DSH_HOME`（Linux 侧本就支持）→ 便于在隔离数据根下安全测试写操作与多环境部署；未设置时与 v2.x 完全一致 |
---

## 6. 怎么让门槛③（Win/Linux 双跑）变绿 —— 你自己也能做

门槛③ 只差"**在 CI 上真跑一次**"，而 CI 只在推送后触发。若你不想让我推送，可以自己推一个分支（**不动 main**）：

```powershell
cd "D:\dsh-workspace\技术\DSHToolkit\09-源码仓库\repo"
git switch -c v3-linux              # 建分支，不动 main
git push -u origin v3-linux         # 只推这个分支
gh run watch                        # 看 CI：会跑 windows-latest + ubuntu-latest
```

**预期结果**：`v3-contracts` job 在两个平台上都绿（`dotnet build v3/src/Dsht.Cli` + 契约测试 180/180）。
若 ubuntu 上失败，那正是有价值的信号——说明 Linux 实现里还有**只在真机才暴露**的问题（我本地只能做到"编译过 + 纯逻辑单测"，见 §5）。

跑完后删分支即可（不影响 main）：

```powershell
git push origin --delete v3-linux
git branch -D v3-linux
```

**或者**：你放行让我推送（我会推同样的分支，不碰 main），我负责跑通并把结果写回文档。

> 说明：`v3-linux` 分支上是**未合并的 41 个本地提交**（含 v2.8 阶段的拆分/接缝/Linux 实现 + V3 全部工作）。
> main 上仍是 `8f885ce`，`verify.ps1` 与 16 项发布清单完好，**随时可发布**。
---

## 7. 怎么安全地测试写操作（真实 restore/export/delete）

V3 的 Windows 路径解析支持 **`$DSH_HOME`**（唯一一处刻意偏离 v2.x 的行为），因此可以在**隔离数据根**下真实测试写操作，完全不碰你的 `~/.dsh`：

```powershell
$iso = Join-Path $env:TEMP ("v3_iso_" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path "$iso\data" -Force | Out-Null
'x' | Set-Content "$iso\data\settings.yaml"        # 造一个假数据根
Copy-Item "$env:TEMP\dsht_v3.exe" $iso -Force      # exe 与数据根同处隔离目录

$env:DSH_HOME = "$iso\data"
& "$iso\dsht_v3.exe" doctor                        # 应看到隔离数据根与 1 B 大小
& "$iso\dsht_v3.exe" backup                        # 备份应写进 $iso\backup
Remove-Item Env:\DSH_HOME; Remove-Item $iso -Recurse -Force
```

实测结论（本轮）：`doctor` 显示隔离数据根（1 B）· `backup` 写出 1 个文件的备份 · **真实 `~/.dsh` 未被触碰** · 不设该变量时标记行契约仍 **19/19**（零回归）。