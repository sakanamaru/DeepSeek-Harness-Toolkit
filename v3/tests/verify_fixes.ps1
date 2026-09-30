# ============================================================================
#  verify_fixes.ps1 —— 修复复核（"每项修复是否仍在代码里"）
#  ---------------------------------------------------------------------------
#  为什么需要它：本项目的修复经常以"替换一行"的方式落地，而**替换会静默删除原语句**。
#  真实发生过两次：stop 的守卫条件被覆盖 → stop 永远失败；WIPE_PRE_BACKUP 打印行被覆盖
#  → 用户看不到安全备份在哪。两次都是**真机测试**才发现的，代价是几十轮。
#  这个脚本把"每项修复的关键字符串仍在代码里"变成一次可重复的机械检查。
#
#  方法（三条纪律合体）：
#    · 关键字符串匹配（#23：替换后立刻核对原语句是否还在）
#    · **排除注释行**（#24：一个注释就能骗过计数 —— 第 54 轮的真实教训）
#    · 要求**最少处数**（不是"≥1 就算"）
#
#  退出码：0 = 全部在；1 = 有缺失
#
#  运行环境：需要 PowerShell 7（`pwsh`）。**实测：原版 Ubuntu 26.04 上没有 pwsh** ✗ ——
#            所以在纯 Linux 环境里这个脚本跑不了，只能在 CI 的 Windows job 或装了 pwsh
#            的机器上跑。脚本本身只做**纯文本匹配**（平台无关），限制纯粹来自运行环境。
# ============================================================================
param([string]$Repo = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$ErrorActionPreference = 'Stop'
$srcDir = Join-Path $Repo 'v3\src'
if (-not (Test-Path $srcDir)) { Write-Host "SKIP: 找不到 v3\src（-Repo 指向仓库根）"; exit 2 }
$files = @(Get-ChildItem $srcDir -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' })

# 每项：名称 / 关键字符串 / 最少代码处数
$checks = @(
  @('备份不完整诚实',            'BACKUP_INCOMPLETE', 1),
  @('失败计数',                  '_copyFailures', 2),
  @('完成标记 files=',           '"files=" + finalFiles', 2),
  @('内容哈希 sha256',           '"sha256=" + h', 1),
  @('标记重算 files',            'int nowFiles = System.IO.Directory.GetFiles(pkgPath', 1),
  @('--verify 三态',             'BACKUP_VERIFY ', 3),
  @('verify 带出 failed',        'failedInMarker', 2),
  @('截断闸门（函数）',          'BackupTruncatedReason', 3),
  @('截断闸门（apply 调用）',    'string trunc = BackupTruncatedReason', 1),
  @('截断闸门（dry-run 调用）',  'string truncReason = BackupTruncatedReason', 1),
  @('config-set 回读',           'CONFIGSET_FAIL', 1),
  @('start 身份校验',            'START_FAIL ', 1),
  @('profilecheck 读错误',       'PROFILECHK_READ_ERRORS', 1),
  @('更新通道',                  'VersionForChannel', 2),
  @('操作日志',                  'private static void OpLog', 1),
  @('import 命令',               'IMPORT_OK', 1),
  @('keep 参数',                 'int keep = 3', 1),
  @('恢复锚点打印',              'RESTORE_PRE_BACKUP ', 1),
  @('wipe 锚点打印',             'WIPE_PRE_BACKUP ', 1),
  @('stop 守卫条件',             'IsDshCommandLine(r.Pid) && !Has(args, "--force")', 1),
  @('标记哈希辅助',              'private static void AddContentHashToMarker', 1),
  @('多工作区打包',              'private static int PackageAllWorkspaces', 1),
  @('命令 backup-dir',           'cmd == "backup-dir"', 1),
  @('命令 bridge-install',       'cmd == "bridge-install"', 1),
  @('命令 update-center',        'cmd == "update-center"', 1),
  @('命令 autostart',            'cmd == "autostart"', 1),
  @('多工作区前置约束',          '_wss.Length >= 2 ? null', 1),
  @('通道说明',                  'CHANNEL_NOTE', 1),
  # ---- 命令面（31 个 ✓）：删掉任何一个 = **静默失去一个功能** ✗ ----
  @('命令 about',                'cmd == "about"', 1),
  @('命令 backup',               'cmd == "backup"', 1),
  @('命令 backup-delete',        'cmd == "backup-delete"', 1),
  @('命令 backup-export',        'cmd == "backup-export"', 1),
  @('命令 backup-list',          'cmd == "backup-list"', 1),
  @('命令 bootdiag',             'cmd == "bootdiag"', 1),
  @('命令 check',                'cmd == "check"', 1),
  @('命令 config-get',           'cmd == "config-get"', 1),
  @('命令 config-set',           'cmd == "config-set"', 1),
  @('命令 describe',             'cmd == "describe"', 1),
  @('命令 doctor',               'cmd == "doctor"', 1),
  @('命令 import',               'cmd == "import"', 1),
  @('命令 install',              'cmd == "install"', 1),
  @('命令 log',                  'cmd == "log"', 1),
  @('命令 profilecheck',         'cmd == "profilecheck"', 1),
  @('命令 profilepatch',         'cmd == "profilepatch"', 1),
  @('命令 profiles',             'cmd == "profiles"', 1),
  @('命令 restore',              'cmd == "restore"', 1),
  @('命令 selftest',             'cmd == "selftest"', 1),
  @('命令 sessions',             'cmd == "sessions"', 1),
  @('命令 shortcut',             'cmd == "shortcut"', 1),
  @('命令 start',                'cmd == "start"', 1),
  @('命令 status',               'cmd == "status"', 1),
  @('命令 stop',                 'cmd == "stop"', 1),
  @('命令 ui',                   'cmd == "ui"', 1),
  @('命令 uninstall',            'cmd == "uninstall"', 1),
  @('命令 update',               'cmd == "update"', 1),
  @('命令 update-info',          'cmd == "update-info"', 1),
  @('命令 verify-install',       'cmd == "verify-install"', 1),
  @('命令 version',              'cmd == "version"', 1),
  @('命令 wipe',                 'cmd == "wipe"', 1),
  # ---- 更早轮次的关键闸门（V3 自身 ✓）----
  @('wipe 拒绝路径',             'WIPE_REFUSED', 1),
  @('wipe 计划路径',             'WIPE_PLAN', 1),
  @('恢复跳过运行中闸门 ACK',    'RESTORE_APPLY_ACK', 1),
  @('start 已运行分支',          'START_OBSERVED', 1),
  @('备份导出',                  'BKEXPORT_OK', 1),
  @('备份删除',                  'BKDEL_OK', 1),
  @('profilecheck 不完整',       'PROFILECHK_INCOMPLETE', 1),
  @('工作区护栏（内→外）',       'wsInsideData', 1),
  @('工作区护栏（外→内）',       'dataInsideWs', 1),
  @('ss 端口精确匹配',           'ParseSsOutput', 1),
  @('不删目标端独有文件',        'PlanMerge', 2),
  # 并发同名碰撞修复 ✓：三处命名（两平台 + CLI import）都必须带 PID 拼接 ✓
  # 否则同毫秒并发会撞名 → 两个源混进同一个包 → 而 --verify 还报 complete ✗✗（真机复现过 ✓）
  @('命名带 PID（三处 ✓）',      'yyyyMMdd-HHmmssfff', 3)
)
$miss = @()
foreach ($c in $checks) {
  $code = 0
  foreach ($f in $files) {
    $hits = Select-String -Path $f.FullName -Pattern $c[1] -SimpleMatch -ErrorAction SilentlyContinue
    foreach ($h in $hits) { if ($h.Line.Trim() -notmatch '^(//|/\*|\*|///)') { $code++ } }
  }
  if ($code -ge $c[2]) { Write-Host ("  [OK]   " + $c[0].PadRight(22) + " 代码里 " + $code + " 处（需 >= " + $c[2] + "）") }
  else { $miss += $c[0]; Write-Host ("  [MISS] " + $c[0].PadRight(22) + " 只有 " + $code + " 处（需 >= " + $c[2] + "）") }
}
# ---- 自检：命令面检查表必须与代码里的命令**一一对应** ----
# 为什么：这张表是手写的。若有人新增命令却忘了加检查，表会**慢慢过时**而没人发现。
# 这条自检让"代码里有 N 个命令 → 表里也必须正好 N 个"，两边都不许漏。
$inCode = New-Object System.Collections.Generic.List[string]
foreach ($f in $files) {
  $hits = Select-String -Path $f.FullName -Pattern 'cmd == "' -SimpleMatch -ErrorAction SilentlyContinue
  foreach ($h in $hits) {
    if ($h.Line.Trim() -match '^(//|/\*|\*|///)') { continue }
    # 用 Matches 取**该行全部**命令名 ✓ —— Match 只取第一个，同一行有两个比较时会漏 ✓
    # （对照实验发现的：把 about 那行改成 else if 形式后，about 被误报为"表里多余" ✗）
    foreach ($m in [regex]::Matches($h.Line, 'cmd == "([a-z0-9\-]+)"')) {
      if (-not $inCode.Contains($m.Groups[1].Value)) { $inCode.Add($m.Groups[1].Value) }
    }
  }
}
$inTable = New-Object System.Collections.Generic.List[string]
foreach ($c in $checks) { if ($c[0] -like '命令 *') { $inTable.Add(($c[0] -replace '^命令 ', '')) } }
$onlyCode = @($inCode | Where-Object { -not $inTable.Contains($_) })
$onlyTable = @($inTable | Where-Object { -not $inCode.Contains($_) })
Write-Host ("  [SELF] 命令面：代码里 " + $inCode.Count + " 个 ；检查表 " + $inTable.Count + " 个")
if ($onlyCode.Count -gt 0) { foreach ($x in $onlyCode) { $miss += ("命令面缺检查：" + $x) }; Write-Host ("  [MISS] 代码里有但表里没有：" + ($onlyCode -join ', ')) }
if ($onlyTable.Count -gt 0) { foreach ($x in $onlyTable) { $miss += ("命令面多余检查：" + $x) }; Write-Host ("  [MISS] 表里有但代码里没有：" + ($onlyTable -join ', ')) }
if ($onlyCode.Count -eq 0 -and $onlyTable.Count -eq 0) { Write-Host "  [OK]   命令面一一对应 ✓" }
# #43：**总项数下限**断言 ✓✓ —— 否则"删掉一条非命令类检查"**无人会察觉** ✗
# （命令面有"一一对应"自检 ✓ 但总项数一直没有断言 ✗ —— 本轮补上 ✓）
$EXPECTED_MIN_CHECKS = 67
if ($checks.Count -lt $EXPECTED_MIN_CHECKS) {
    $miss += ("检查表项数不足：" + $checks.Count + " < " + $EXPECTED_MIN_CHECKS)
    Write-Host ("  [MISS] 检查表项数不足：只有 " + $checks.Count + " 项（需 >= " + $EXPECTED_MIN_CHECKS + "）")
}
if ($miss.Count -eq 0) { Write-Host ("== 修复复核：" + $checks.Count + "/" + $checks.Count + " 全部仍在代码里 =="); exit 0 }
Write-Host ("== 修复复核：缺失 " + $miss.Count + " 项：" + ($miss -join ', ') + " =="); exit 1
