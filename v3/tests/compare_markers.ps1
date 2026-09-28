# compare_markers.ps1 —— V3 与 v2.x 的「标记行契约」比对（目标切换门槛之一）
# 做法：用 csc 把整个 V3 树编成 exe（V3 代码刻意保持 C#5 兼容），对同一组命令分别跑 v2.x 与 V3，
#       只比较**机器可读标记行**（^STATUS_ 等），逐条给出 PASS/FAIL 与差异。
# 退出码：0=全部对齐；1=有差异；2=环境不足（缺 v2.x exe 或 csc）
param([string]$Repo = ".")
$ErrorActionPreference = "Stop"
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { Write-Host "SKIP: 找不到 csc（需 Windows + .NET Framework 4.x）"; exit 2 }
$v2 = Join-Path $Repo 'DeepSeek Harness Toolkit.exe'
if (-not (Test-Path $v2)) { Write-Host "SKIP: 找不到 v2.x exe（$v2）——先在仓库根构建 v2.x"; exit 2 }
$v3exe = Join-Path $env:TEMP 'dsht_v3_contract.exe'
$files = @(Get-ChildItem (Join-Path $Repo 'v3\src') -Recurse -Filter *.cs | ForEach-Object FullName)
& $csc /nologo /target:exe /warn:4 ("/out:" + $v3exe) $files | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "FAIL: V3 编译失败"; exit 2 }

# 待比对命令：V3 尚未实现的命令会如实报"未对齐"
$cases = @(
    @{ name = 'status';          args = @('status') },
    @{ name = 'status --detail'; args = @('status','--detail') }
)
$fail = 0
foreach ($c in $cases) {
    $o2 = (& $v2 @($c.args) 2>&1 | Out-String)
    $o3 = (& $v3exe @($c.args) 2>&1 | Out-String)
    $m2 = @(($o2 -split "`r?`n") | Where-Object { $_ -match '^STATUS_[A-Z]+' } | ForEach-Object { $_.Trim() })
    $m3 = @(($o3 -split "`r?`n") | Where-Object { $_ -match '^STATUS_[A-Z]+' } | ForEach-Object { $_.Trim() })
    $same = (($m2 -join '|') -eq ($m3 -join '|'))
    if ($same) { Write-Host ("  {0,-16} PASS  [{1}]" -f $c.name, ($m2 -join ' ')) }
    else {
        $fail++
        Write-Host ("  {0,-16} FAIL" -f $c.name) -ForegroundColor Red
        Write-Host ("      v2.x: " + ($m2 -join ' | '))
        Write-Host ("      V3  : " + ($m3 -join ' | '))
    }
}
Write-Host ("== 标记行契约：{0}/{1} 对齐 ==" -f ($cases.Count - $fail), $cases.Count)
if ($fail -gt 0) { exit 1 }
exit 0