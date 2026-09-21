# verify_move_only.ps1 —— 「只搬不改」回归守卫
# 断言：拆分前基线（v2.7.2 的 dsh_v2.cs）里的每一个「有效行」都仍存在于当前源码集合中。
#   允许新增（阶段 2/3 的接缝与 Linux 实现、段落注释），但**不允许丢失任何一行**。
# 退出码：0=通过；1=有丢失；2=基线不可用（浅克隆等）→ 调用方视为 SKIP
param([string]$Repo = ".")
$ErrorActionPreference = "Stop"
$base = ""
foreach ($rev in @('v2.7.2', '05ce437')) {
    $tmp = Join-Path $env:TEMP ("mo_base_" + [Guid]::NewGuid().ToString("N") + ".cs")
    & cmd /c "git -C `"$Repo`" show $rev`:dsh_v2.cs > `"$tmp`" 2>nul" | Out-Null
    if ((Test-Path $tmp) -and (Get-Item $tmp).Length -gt 1000) { $base = $tmp; break }
    Remove-Item $tmp -Force -ErrorAction SilentlyContinue
}
if ($base -eq "") { Write-Host "SKIP: 基线不可用（需要 v2.7.2 tag 或 05ce437 提交）"; exit 2 }
function EffectiveLines([string]$path, [bool]$isNew) {
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($ln in ([System.IO.File]::ReadAllText($path) -replace "`r`n", "`n") -split "`n") {
        $tr = $ln.Trim()
        if ($tr.Length -eq 0) { continue }
        if ($tr -match '^using\s' -or $tr -match '^\[assembly:') { continue }
        if ($tr -match '^[{};,\s]*$') { continue }
        # 类声明行本身是「预期内的合法变化」：原 public static class Program → 各 partial 文件的 partial class Program
        if ($tr -match '^(public\s+|internal\s+)?(static\s+)?(partial\s+)?class\s+Program\s*$') { continue }
        $out.Add($tr)
    }
    return $out
}
$o = EffectiveLines $base $false
$files = @((Join-Path $Repo 'dsh_v2.cs')) + (Get-ChildItem (Join-Path $Repo 'src') -Recurse -Filter *.cs | ForEach-Object FullName)
$n = New-Object System.Collections.Generic.List[string]
foreach ($f in $files) { foreach ($ln in (EffectiveLines $f $true)) { $n.Add($ln) } }
$d = Compare-Object ($o | Sort-Object) ($n | Sort-Object)
$lost = @($d | Where-Object { $_.SideIndicator -eq '<=' })
$added = @($d | Where-Object { $_.SideIndicator -eq '=>' })
Write-Host ("基线有效行={0}  当前有效行={1}  丢失={2}  新增={3}（源文件 {4} 个）" -f $o.Count, $n.Count, $lost.Count, $added.Count, $files.Count)
Remove-Item $base -Force -ErrorAction SilentlyContinue
if ($lost.Count -gt 0) {
    Write-Host "RESULT: FAIL —— 有行在拆分中丢失：" -ForegroundColor Red
    $lost | Select-Object -First 10 | ForEach-Object { Write-Host ("  " + $_.InputObject) }
    exit 1
}
Write-Host "RESULT: MOVE-ONLY OK（基线每一行都仍在，新增仅为接缝/注释）" -ForegroundColor Green
exit 0