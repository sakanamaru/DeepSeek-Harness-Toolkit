# verify_restore_apply.ps1 — 端到端验证 V3 的真实 restore（--apply 隔离写盘）
#
# 为什么需要它：compare_markers.ps1 只能比对 v2.x 也有的命令面，而 `--apply` 是 V3 独有的开关，
# 且它的**真实写盘**必须在隔离数据根下验证。本脚本就是那条"能真跑起来"的路径：
#   $DSH_HOME 指向临时数据根 + exe 也放在临时目录（状态目录=exe 目录）→ 备份与恢复全部落在临时目录内。
#
# 安全设计（三条硬约束，缺一不可）：
#   ① 所有写操作都发生在 $iso 之下；脚本结束前比对**真实默认数据根**的快照，证明零写入；
#   ② 绝不把 $DSH_HOME 指向真实默认数据根去测 "apply-not-isolated"——那条分支由纯领域契约测试覆盖
#      （把"应当拒绝"的用例指向真实数据根，一旦判定有 bug 就会真写用户数据，这正是以前踩过的坑）；
#   ③ 每个用例结束后都复核 $iso 之外无残留（%TEMP%\backup 不存在）。
#
# 用法：
#   powershell -ExecutionPolicy Bypass -File v3\tests\verify_restore_apply.ps1 -Repo .
#   powershell -ExecutionPolicy Bypass -File v3\tests\verify_restore_apply.ps1 -Repo . -Keep   # 保留隔离目录供检查
[CmdletBinding()]
param(
    [string]$Repo = ".",
    [switch]$Keep
)

$ErrorActionPreference = "Stop"
# ★★★ **假绿修复（实测发现 —— 与 `compare_markers` / `verify_switchover` 同源）** ✓✓
#   ✗ `Stop` + 下面 `Run()` 里的 `& $exe @cmdArgs 2>&1` ✗ —— V3 exe 会往 stderr 打**诊断**
#     （`INTEGRITY_SKIPPED`：本地源码构建的 exe 旁没有 hashes.txt ✓ 完全正常 ✓）
#     → PS 5.1 把它变成 **NativeCommandError** → **Stop 终止** ✗✗
#     → **脚本在第 55 行就死掉** ✗ → **从不打印 `== N/N passed`** ✗
#     → 调用它的 `verify_switchover` 的 gate5 **只能报"未解析到结果行"** ✓✓
#   ✓ 现在：**只在调外部命令时放宽为 Continue** ✓✓ 并**不再用 `2>&1` 把诊断混进输出** ✓
function Invoke-External([scriptblock]$sb) {
    $old = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $sb } finally { $ErrorActionPreference = $old }
}
$script:pass = 0
$script:fail = 0

function Check([string]$name, [bool]$ok) {
    if ($ok) { $script:pass++; Write-Host ("  [PASS] " + $name) }
    else { $script:fail++; Write-Host ("  [FAIL] " + $name) -ForegroundColor Red }
}
function Section([string]$t) { Write-Host ""; Write-Host ("== " + $t) }

function Get-RealDataRoots {
    $c = @()
    if ($env:USERPROFILE) { $c += (Join-Path $env:USERPROFILE ".dsh") }
    if ($env:APPDATA) { $c += (Join-Path $env:APPDATA ".dsh") }
    if ($env:LOCALAPPDATA) { $c += (Join-Path $env:LOCALAPPDATA ".dsh") }
    return $c
}

# 真实数据根快照：只读、只看顶层（名字 + 文件长度）。用于证明"零写入"。
function Get-RootSnapshot([string]$root) {
    if (-not (Test-Path -LiteralPath $root)) { return "(absent)" }
    $items = Get-ChildItem -LiteralPath $root -Force -ErrorAction SilentlyContinue | Sort-Object Name | ForEach-Object {
        if ($_.PSIsContainer) { "D:" + $_.Name } else { "F:" + $_.Name + ":" + $_.Length }
    }
    return ($items -join "|")
}

function Read-Text([string]$p) {
    if (-not (Test-Path -LiteralPath $p)) { return $null }
    return [System.IO.File]::ReadAllText($p)
}

function Run([string]$exe, [string[]]$cmdArgs) {
    $all = (Invoke-External { & $exe @cmdArgs }) | Out-String
    return $all
}

$repoFull = (Resolve-Path -LiteralPath $Repo).Path
$v3src = Join-Path $repoFull "v3\src"
if (-not (Test-Path -LiteralPath $v3src)) { Write-Host ("找不到 V3 源码: " + $v3src); exit 2 }

$iso = Join-Path $env:TEMP ("dsht_restore_apply_" + [guid]::NewGuid().ToString("N"))
$exeDir = Join-Path $iso "exe"
$data = Join-Path $iso "data"
$exe = Join-Path $exeDir "dsht_v3.exe"
$tempBackup = Join-Path $env:TEMP "backup"

$realRoots = Get-RealDataRoots
$before = @{}
foreach ($r in $realRoots) { $before[$r] = (Get-RootSnapshot $r) }

$oldDshHome = $env:DSH_HOME

try {
    New-Item -ItemType Directory -Path $exeDir -Force | Out-Null
    New-Item -ItemType Directory -Path $data -Force | Out-Null

    Section "0. 构建 V3 exe（csc，不需要 .NET SDK）"
    $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    if (-not (Test-Path -LiteralPath $csc)) { Write-Host ("找不到 csc: " + $csc); exit 2 }
    if (Test-Path -LiteralPath $exe) { Remove-Item -LiteralPath $exe -Force }   # 绝不留下旧 exe 造成"假通过"
    $src = @(Get-ChildItem -LiteralPath $v3src -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' } | ForEach-Object { $_.FullName })
    $build = (Invoke-External { & $csc /nologo /target:exe /warn:4 "/out:$exe" $src }) | Out-String
    Check ("构建成功（" + $src.Count + " 个源文件）") (Test-Path -LiteralPath $exe)
    if (-not (Test-Path -LiteralPath $exe)) { Write-Host $build; exit 2 }

    # 隔离数据根：造出"可辨识"的初始内容
    [System.IO.File]::WriteAllText((Join-Path $data "settings.yaml"), "v1")
    New-Item -ItemType Directory -Path (Join-Path $data "sessions") -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $data "sessions\s1.txt"), "S1")
    $env:DSH_HOME = $data

    Section "1. 隔离根下真实备份（备份应落在 exe 目录旁，而不是真实数据根）"
    # ★★★ **假绿修复的连锁（实测发现）** ✓✓
    #   ✗ 这里原来是裸 `backup` ✗ —— 而产品的契约是**第一次备份必须显式给目录**
    #     （`BACKUP_NEEDS_DIR` + "请加 --to <目录>" ✓ 由 D4 修复引入 ✓）
    #     → 隔离根下没有 `backup/` → 产品**拒绝** ✓ → 后面 11 项**全部连锁失败** ✗✗
    #     → 而脚本第 55 行早就死了 ✓ **这个红一直没人看见** ✓✓
    #   ✓ 现在：**按产品契约传 `--to`** ✓✓（意图不变：备份必须落在隔离目录内 ✓）
    $bkTo = Join-Path $iso 'backups'
    New-Item -ItemType Directory -Path $bkTo -Force | Out-Null
    $out = Run $exe @("backup", "--to", $bkTo)
    Check "backup 打印 BACKUP_OK" ($out -match "BACKUP_OK")
    $bkLine = ($out -split "`r?`n" | Where-Object { $_ -match "^BACKUP_OK " } | Select-Object -First 1)
    $bkDir = $null
    if ($bkLine) { $bkDir = $bkLine.Substring("BACKUP_OK ".Length).Trim() }
    Check "备份目录位于隔离目录内" ($bkDir -ne $null -and $bkDir.StartsWith($iso, [StringComparison]::OrdinalIgnoreCase))
    # ★★★ **门槛完整性审计 M5a2 —— `--to` 本身原来没被断言** ✓✓
    #   ✗ 只断言"在 `$iso` 内" ✗ → 变异证明：**完全忽略 `--to`、改用默认根**（也在 `$iso` 内 ✓）照样 25/25 ✗✗
    #   ✓ 现在：**必须落在 `--to` 指定的那个目录下** ✓✓
    Check "备份目录落在 --to 指定的目录下" ($bkDir -ne $null -and $bkDir.StartsWith($bkTo, [StringComparison]::OrdinalIgnoreCase))
    Check "备份内容含 settings.yaml" ($bkDir -ne $null -and (Test-Path -LiteralPath (Join-Path $bkDir "settings.yaml")))
    Check "备份内容含 sessions\s1.txt" ($bkDir -ne $null -and (Test-Path -LiteralPath (Join-Path $bkDir "sessions\s1.txt")))

    Section "2. 篡改数据根后 restore --apply（真实写盘 + 合并语义 + 恢复前自动备份）"
    [System.IO.File]::WriteAllText((Join-Path $data "settings.yaml"), "v2-BROKEN")
    Remove-Item -LiteralPath (Join-Path $data "sessions\s1.txt") -Force
    [System.IO.File]::WriteAllText((Join-Path $data "only-local.txt"), "LOCAL")   # 目标端独有文件：恢复不应删除

    $out = Run $exe @("restore", "--apply")
    Check "打印 RESTORE_APPLY_ACK（跳过运行中闸门的事实已留证）" ($out -match "RESTORE_APPLY_ACK")
    Check "打印 RESTORE_APPLY_ROOT 且指向隔离数据根" ($out -match "RESTORE_APPLY_ROOT" -and $out.Contains($data))
    Check "打印 RESTORE_PRE_BACKUP（回滚锚点）" ($out -match "RESTORE_PRE_BACKUP")
    Check "打印 RESTORE_OK" ($out -match "RESTORE_OK")
    Check "被篡改的 settings.yaml 已恢复为 v1" ((Read-Text (Join-Path $data "settings.yaml")) -eq "v1")
    Check "被删除的 sessions\s1.txt 已恢复" ((Read-Text (Join-Path $data "sessions\s1.txt")) -eq "S1")
    Check "目标端独有文件未被删除（合并语义）" ((Read-Text (Join-Path $data "only-local.txt")) -eq "LOCAL")

    $preLine = ($out -split "`r?`n" | Where-Object { $_ -match "^RESTORE_PRE_BACKUP " } | Select-Object -First 1)
    $preDir = $null
    if ($preLine) { $preDir = $preLine.Substring("RESTORE_PRE_BACKUP ".Length).Trim() }
    Check "恢复前自动备份存在且位于隔离目录内" ($preDir -ne $null -and $preDir.StartsWith($iso, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $preDir))
    Check "恢复前自动备份里保存的是被篡改前的内容（v2-BROKEN）" ((Read-Text (Join-Path $preDir "settings.yaml")) -eq "v2-BROKEN")
    Check "恢复前自动备份带 -pre-restore 后缀" ($preDir -ne $null -and $preDir.EndsWith("-pre-restore"))

    Section "3. 不给 --apply 时不越界：要么被运行中闸门拒绝（零写入），要么真实恢复到隔离根"
    # 确定性：前面几步的 --apply 恢复各产生一个 -pre-restore 包 → "最新备份"已变 ✗
    # 不清掉的话，这步恢复的是某个 pre-restore 包，内容当然不是 v1 → 与产品无关的假失败 ✓
    # 备份根 = 那个已知好包的父目录 ✓（不是 $bkRoot —— 那个变量根本不存在 ✗，被 SilentlyContinue 吞掉了 ✗）
    # 只留下 $bkDir 这一个包 → "最新备份"确定 ✓ → 默认路径恢复出来的必然是 v1 ✓✓
    if ($bkDir) {
        $bkRootReal = Split-Path $bkDir -Parent
        Get-ChildItem $bkRootReal -Directory -ErrorAction SilentlyContinue | Where-Object { $_.FullName -ne $bkDir } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        Check '（前置）备份根里只剩已知好包，默认路径的"最新"因此确定' (@(Get-ChildItem $bkRootReal -Directory -ErrorAction SilentlyContinue).Count -eq 1)
    }
    [System.IO.File]::WriteAllText((Join-Path $data "settings.yaml"), "v3-DIRTY")
    $out = Run $exe @("restore")
    $refused = ($out -match "RESTORE_FAIL")
    $now = Read-Text (Join-Path $data "settings.yaml")
    if ($refused) {
        $why = ($out -split "`r?`n" | Where-Object { $_ -match "RESTORE_FAIL" } | Select-Object -First 1)
        if ($why) { $why = $why.Trim() }
        Check ("被拒绝（" + $why + "）且零写入") ($now -eq "v3-DIRTY")
    } else {
        # 服务确实没在跑：这是 v2.x 的默认路径，但数据根仍是隔离的，所以安全
        if ($now -ne "v1") { Write-Host ("      [诊断] 实际内容 = " + $now) }
        Check "服务未运行时默认路径真的恢复了（且只写隔离根）" ($now -eq "v1")
    }

    '4. --apply 但未设 $DSH_HOME → 明确拒绝且零写入'
    Remove-Item Env:\DSH_HOME -ErrorAction SilentlyContinue
    [System.IO.File]::WriteAllText((Join-Path $data "settings.yaml"), "v4-DIRTY")
    $out = Run $exe @("restore", "--apply")
    Check "打印 RESTORE_FAIL" ($out -match "RESTORE_FAIL")
    Check "原因提到 DSH_HOME（apply-needs-dsh-home）" ($out -match "DSH_HOME")
    Check "拒绝后数据未被写入" ((Read-Text (Join-Path $data "settings.yaml")) -eq "v4-DIRTY")
    Check "拒绝后没有产生恢复前备份" ($out -notmatch "RESTORE_PRE_BACKUP")

    Section "5. 越界证明：真实默认数据根与 %TEMP%\backup 未被触碰"
    foreach ($r in $realRoots) {
        $after = Get-RootSnapshot $r
        Check ("真实数据根未变: " + $r) ($after -eq $before[$r])
    }
    Check "%TEMP%\backup 不存在（无残留）" (-not (Test-Path -LiteralPath $tempBackup))
}
finally {
    if ($oldDshHome) { $env:DSH_HOME = $oldDshHome } else { Remove-Item Env:\DSH_HOME -ErrorAction SilentlyContinue }
    if (-not $Keep) { Remove-Item -LiteralPath $iso -Recurse -Force -ErrorAction SilentlyContinue }
    else { Write-Host ("隔离目录保留在: " + $iso) }
}

Write-Host ""
Write-Host ("== " + $script:pass + "/" + ($script:pass + $script:fail) + " passed, " + $script:fail + " failed ==")
if ($script:fail -ne 0) { exit 1 }
exit 0
