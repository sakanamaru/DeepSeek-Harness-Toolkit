# ============================================================================
#  after_edit.ps1 —— 改完代码后的**一条命令**验证
#  ---------------------------------------------------------------------------
#  为什么有这个脚本：本会话多次出现"改完忘了跑某一项"（最典型：用无 BOM 的写法
#  改 .ps1，把中文注释变成乱码，而 invariant ps1 utf8 bom 正是为此存在 —— 却没跑）。
#  教训不是"要记得"，而是**把要记得的事变成一条命令**。
#
#  用法：  pwsh -NoProfile -File v3/tools/after_edit.ps1 [-Repo <仓库根>] [-Heavy]
#  退出码：0 = 全绿；1 = 有失败
#
#  覆盖：① csc 编译（v3/src）② 标记行契约 gate1（默认 22 项；-Heavy 加 1 项）
#        ③ 修复复核 verify_fixes ④ ps1 BOM 不变量（**含非 ASCII 的 .ps1 必须带 BOM**）
# ============================================================================
param(
  [string]$Repo = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
  [switch]$Heavy
)
$ErrorActionPreference = 'Continue'
$fail = 0
function Step($name, $ok, $detail) {
  if ($ok) { Write-Host ("  [OK]   " + $name.PadRight(28) + " " + $detail) }
  else { Write-Host ("  [FAIL] " + $name.PadRight(28) + " " + $detail); $script:fail++ }
}
Write-Host "== after_edit：改完代码的一条命令验证 =="

# ---- ① csc 编译 ----
$csc = Join-Path $env:WINDIR 'Microsoft.NET.Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe' }
$outExe = Join-Path $env:TEMP ('dsht_afteredit_' + $PID + '.exe')
$src = @(Get-ChildItem (Join-Path $Repo 'v3\src') -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' } | ForEach-Object FullName)
$r = & $csc /nologo /target:exe /warn:4 ("/out:" + $outExe) $src 2>&1
$errs = @($r | Where-Object { $_ -match 'error' })
Step 'csc build (v3/src)' ($errs.Count -eq 0) ($src.Count.ToString() + ' 个源文件，errors=' + $errs.Count)
if ($errs.Count -gt 0) { $errs | Select-Object -First 3 | ForEach-Object { Write-Host ("         " + $_.ToString().Trim()) } }
Remove-Item $outExe -Force -ErrorAction SilentlyContinue

# ---- ② gate1 标记行契约 ----
$cmp = Join-Path $Repo 'v3\tests\compare_markers.ps1'
$cmpArgs = @('-ExecutionPolicy','Bypass','-File',$cmp,'-Repo',$Repo)
if ($Heavy) { $cmpArgs += '-Heavy' }
$g = & powershell @cmpArgs 2>&1
$gline = ($g | Select-String -Pattern '标记行契约' | Select-Object -Last 1)
$gtext = if ($gline) { $gline.ToString().Trim() } else { '(无输出)' }
Step 'gate1 标记行契约' ($gtext -match '(\d+)/\1 对齐') $gtext

# gate1 的 -Fixtures 模式：夹具覆盖**有效性过滤**（含一个故意无效的受控备份 ✓）
# —— 第 111 轮我就是只跑了默认模式，漏掉了"新加的行在夹具里会出现"这件事 ✗ → 契约被破坏 ✓
# 现在默认就两种模式都跑 ✓（-Fixtures 会临时造夹具并自动清理 ✓）
$gf = & powershell -ExecutionPolicy Bypass -File $cmp -Repo $Repo -Fixtures 2>&1
$gfline = ($gf | Select-String -Pattern '标记行契约' | Select-Object -Last 1)
$gftext = if ($gfline) { $gfline.ToString().Trim() } else { '(无输出)' }
Step 'gate1（-Fixtures 夹具模式）' ($gftext -match '(\d+)/\1 对齐') $gftext

# ---- ③ 修复复核 ----
$vf = Join-Path $Repo 'v3\tests\verify_fixes.ps1'
$v = & powershell -ExecutionPolicy Bypass -File $vf -Repo $Repo 2>&1
$vline = ($v | Select-String -Pattern '修复复核' | Select-Object -Last 1)
$vtext = if ($vline) { $vline.ToString().Trim() } else { '(无输出)' }
Step 'verify_fixes 修复复核' ($vtext -match '全部仍在代码里') $vtext

# ---- ④ ps1 BOM 不变量（上一轮就栽在这 ✗）----
$bad = @()
foreach ($f in (Get-ChildItem (Join-Path $Repo 'v3') -Recurse -Filter *.ps1 -ErrorAction SilentlyContinue)) {
  $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
  $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
  $hasNonAscii = $false
  try { $txt = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8); if ($txt -match '[^\x00-\x7F]') { $hasNonAscii = $true } } catch { }
  if ($hasNonAscii -and -not $hasBom) { $bad += $f.Name }
}
Step 'invariant ps1 BOM' ($bad.Count -eq 0) $(if ($bad.Count -eq 0) { '含非 ASCII 的 .ps1 全部带 BOM' } else { '缺 BOM：' + ($bad -join ', ') })

Write-Host ("== 结果：" + $(if ($fail -eq 0) { '全绿 ✓' } else { ($fail.ToString() + ' 项失败 ✗') }) + " ==")
if ($fail -eq 0) { exit 0 } else { exit 1 }