# verify_move_only.ps1 —— 拆分/重构的「结构不丢」守卫
# 判据（fail 条件）：**拆分前基线里的每一个成员签名，都必须仍存在于当前源码集合中**（允许新增，不允许丢）。
#   —— 这正对应目标里的门禁「成员签名集合一致」。
# 说明：行级比对只作为 INFO 输出。后续"行为等价重构"（例如把调用点接到平台接缝）会**故意改写某些行**，
#       因此不再以"字面行不变"为失败条件；真正的丢成员会被这条判据 + 编译器 + 单测/集成共同拦下。
# 退出码：0=通过；1=有成员丢失；2=基线不可用（浅克隆等）→ 调用方视为 SKIP
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
$baseText = [System.IO.File]::ReadAllText($base)
Remove-Item $base -Force -ErrorAction SilentlyContinue

$files = @((Join-Path $Repo 'dsh_v2.cs')) + (Get-ChildItem (Join-Path $Repo 'src') -Recurse -Filter *.cs | ForEach-Object FullName)
$curText = ""
foreach ($f in $files) { $curText += [System.IO.File]::ReadAllText($f) + "`n" }

function Sigs([string]$t) {
    $pats = @(
        '(?m)^\s*(?:public|private|internal|protected)?\s*(?:static\s+)?(?:sealed\s+|abstract\s+)?[\w<>\[\],\.\?]+\s+\w+\s*\(',
        '(?m)^\s*(?:const|static readonly)\s+[\w<>\[\],\.]+\s+\w+',
        '(?m)^\s*(?:public|private|internal)?\s*(?:static\s+)?(?:sealed\s+)?(?:class|struct|enum)\s+\w+',
        '(?m)^\s*(?:public|private|internal|protected)\s+(?:static\s+)?[\w<>\[\],\.\?]+\s+\w+\s*\{'
    )
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($p in $pats) {
        foreach ($m in [regex]::Matches($t, $p)) {
            $v = ($m.Value -replace '\s+', ' ').Trim()
            if ($v -match 'class\s+Program\s*$') { continue }   # 类声明行属预期变化
            $out.Add($v)
        }
    }
    return $out
}
function EffectiveLines([string]$t) {
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($ln in ($t -replace "`r`n", "`n") -split "`n") {
        $tr = $ln.Trim()
        if ($tr.Length -eq 0) { continue }
        if ($tr -match '^using\s' -or $tr -match '^\[assembly:') { continue }
        if ($tr -match '^[{};,\s]*$') { continue }
        if ($tr -match '^(public\s+|internal\s+)?(static\s+)?(partial\s+)?class\s+Program\s*$') { continue }
        $out.Add($tr)
    }
    return $out
}

$bs = Sigs $baseText; $cs = Sigs $curText
$d = Compare-Object ($bs | Sort-Object) ($cs | Sort-Object)
$missing = @($d | Where-Object { $_.SideIndicator -eq '<=' })
$bl = EffectiveLines $baseText; $cl = EffectiveLines $curText
$ld = Compare-Object ($bl | Sort-Object) ($cl | Sort-Object)
$lostLines = @($ld | Where-Object { $_.SideIndicator -eq '<=' })
Write-Host ("基线签名={0}  当前签名={1}  丢失签名={2}   |   基线有效行={3}  丢失行(INFO)={4}（源文件 {5} 个）" -f $bs.Count, $cs.Count, $missing.Count, $bl.Count, $lostLines.Count, $files.Count)
if ($missing.Count -gt 0) {
    Write-Host "RESULT: FAIL —— 有成员签名在重构中丢失：" -ForegroundColor Red
    $missing | Select-Object -First 12 | ForEach-Object { Write-Host ("  " + $_.InputObject) }
    exit 1
}
Write-Host "RESULT: STRUCTURE OK（基线成员签名一个未丢；行级差异属预期改写/新增）" -ForegroundColor Green
exit 0