# verify_release.ps1 —— V3 发布物校验（v2.x verify.ps1 的等价物）
# 校验内容（全部本地可跑，无需 GPG）：
#   C1 产物存在        C2 hashes.txt 存在且格式合法（每行 <64hex>  <name>）
#   C3 清单内每个文件哈希一致   C4 磁盘上无清单外文件（白名单：hashes.txt）
#   C5 自身完整性（exe 的哈希在清单里且一致）  C6 版本一致性（exe 报告的版本 == -Version）
#   C7 冒烟（status 必须输出 STATUS_ 标记）
# -SelfTest：把发布物复制一份并篡改 1 字节 → 断言校验器**必须报错**（证明它不是"永远通过"）
# 退出码：0=通过；1=有失败；2=环境不足
# -Keep：保留临时发布物/篡改副本供检查（默认跑完即删——它们每次都在 %TEMP% 累积）
param(
  [string]$Repo = ".",
  [string]$ReleaseDir,
  [string]$Version = "3.0.0-dev",
  [switch]$Build,
  [switch]$SelfTest,
  [switch]$Keep
)
$ErrorActionPreference = "Stop"
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { Write-Host "SKIP: 找不到 csc"; exit 2 }

function Build-Release([string]$dir) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $src = @(Get-ChildItem (Join-Path $Repo 'v3\src') -Recurse -Filter *.cs | ForEach-Object FullName)
    $exe = Join-Path $dir 'dsht.exe'
    & $csc /nologo /target:exe /warn:4 ("/out:" + $exe) $src | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Host "FAIL: V3 构建失败"; exit 2 }
    $h = (Get-FileHash -Algorithm SHA256 -LiteralPath $exe).Hash.ToLower()
    [System.IO.File]::WriteAllText((Join-Path $dir 'hashes.txt'), ($h + "  dsht.exe`r`n"), (New-Object System.Text.UTF8Encoding($false)))
}

function Get-Manifest([string]$path) {
    $map = @{}
    foreach ($ln in ([System.IO.File]::ReadAllText($path) -split "`r?`n")) {
        $t = $ln.Trim()
        if ($t.Length -eq 0 -or $t.StartsWith('#')) { continue }
        $sp = $t.IndexOf(' ')
        if ($sp -le 0) { continue }
        $h = $t.Substring(0, $sp).Trim().ToLower()
        $n = $t.Substring($sp + 1).Trim()
        if ($h.Length -eq 64) { $map[$n] = $h }
    }
    return $map
}

function Invoke-Checks([string]$dir, [string]$wantVersion) {
    $res = @{ pass = 0; fail = 0 }
    function C([string]$name, [bool]$ok, [string]$extra = '') {
        if ($ok) { $res.pass++; Write-Host ("  [PASS] " + $name + $(if ($extra) { " ($extra)" })) }
        else { $res.fail++; Write-Host ("  [FAIL] " + $name + $(if ($extra) { " ($extra)" })) -ForegroundColor Red }
    }
    $exe = Join-Path $dir 'dsht.exe'
    $man = Join-Path $dir 'hashes.txt'
    C 'C1 产物存在' (Test-Path -LiteralPath $exe)
    C 'C2 清单存在' (Test-Path -LiteralPath $man)
    if (-not (Test-Path $exe) -or -not (Test-Path $man)) { return $res }

    $map = Get-Manifest $man
    C 'C2b 清单格式合法（含 dsht.exe）' ($map.ContainsKey('dsht.exe'))

    $mismatch = 0
    foreach ($k in $map.Keys) {
        $p = Join-Path $dir $k
        if (-not (Test-Path -LiteralPath $p)) { $mismatch++; continue }
        if ((Get-FileHash -Algorithm SHA256 -LiteralPath $p).Hash.ToLower() -ne $map[$k]) { $mismatch++ }
    }
    C 'C3 清单内文件哈希一致' ($mismatch -eq 0) ("不一致 " + $mismatch + " 个")

    $extraFiles = @(Get-ChildItem -LiteralPath $dir -File | Where-Object { $_.Name -ne 'hashes.txt' -and -not $map.ContainsKey($_.Name) })
    C 'C4 无清单外文件' ($extraFiles.Count -eq 0) (($extraFiles | ForEach-Object { $_.Name }) -join ',')

    $selfHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $exe).Hash.ToLower()
    C 'C5 自身完整性（exe 与清单一致）' ($map.ContainsKey('dsht.exe') -and $map['dsht.exe'] -eq $selfHash)

    $ver = (& $exe version 2>&1 | Out-String).Trim()
    C 'C6 版本一致性' ($ver -eq ("DSHT_VERSION " + $wantVersion)) $ver

    $st = (& $exe status 2>&1 | Out-String)
    C 'C7 冒烟（status 有标记）' ($st -match 'STATUS_(UP|DOWN|STARTING)')
    return $res
}

if ($Build -or [string]::IsNullOrEmpty($ReleaseDir)) {
    $ReleaseDir = Join-Path $env:TEMP ("dsht_v3_rel_" + [Guid]::NewGuid().ToString("N"))
    Build-Release $ReleaseDir
    Write-Host ("  [build] 本地发布物: " + $ReleaseDir)
}

Write-Host "== V3 发布物校验 =="
$r1 = Invoke-Checks $ReleaseDir $Version
Write-Host ("  → 通过 " + $r1.pass + " / 失败 " + $r1.fail)

if ($SelfTest) {
    $tam = Join-Path $env:TEMP ("dsht_v3_tamper_" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $tam -Force | Out-Null
    Copy-Item (Join-Path $ReleaseDir '*') $tam -Force
    $b = [System.IO.File]::ReadAllBytes((Join-Path $tam 'dsht.exe'))
    $b[$b.Length - 50] = [byte](($b[$b.Length - 50] + 1) % 256)
    [System.IO.File]::WriteAllBytes((Join-Path $tam 'dsht.exe'), $b)
    Write-Host "== 校验器自证（篡改 1 字节后必须报错）=="
    $r2 = Invoke-Checks $tam $Version
    Write-Host ("  → 通过 " + $r2.pass + " / 失败 " + $r2.fail)
    if ($r2.fail -gt 0) { Write-Host "  [PASS] 校验器能检出篡改 ✓" } else { Write-Host "  [FAIL] 校验器漏检篡改 ✗" -ForegroundColor Red; $r1.fail++ }
}

if ($r1.fail -gt 0) { Write-Host "RESULT: FAIL"; exit 1 }
Write-Host "RESULT: RELEASE VERIFY OK"
if (-not $Keep) {
    # 清理本次自建的临时目录（不清理用户用 -ReleaseDir 指定的目录）
    if ($Build -and -not [string]::IsNullOrEmpty($ReleaseDir)) { Remove-Item -LiteralPath $ReleaseDir -Recurse -Force -ErrorAction SilentlyContinue }
    if ($SelfTest -and -not [string]::IsNullOrEmpty($tam)) { Remove-Item -LiteralPath $tam -Recurse -Force -ErrorAction SilentlyContinue }
}
exit 0