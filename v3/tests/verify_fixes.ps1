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
  @('多工作区前置约束',          '_wss.Length >= 2 ? null', 1),
  @('通道说明',                  'CHANNEL_NOTE', 1)
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
if ($miss.Count -eq 0) { Write-Host ("== 修复复核：" + $checks.Count + "/" + $checks.Count + " 全部仍在代码里 =="); exit 0 }
Write-Host ("== 修复复核：缺失 " + $miss.Count + " 项：" + ($miss -join ', ') + " =="); exit 1