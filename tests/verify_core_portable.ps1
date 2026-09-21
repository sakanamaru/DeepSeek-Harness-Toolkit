# verify_core_portable.ps1 —— 核心「平台中立」守卫
# 断言：核心（dsh_v2.cs + src/Core/** + src/Cli/**）里不出现 Windows-only 编译期 API；
#       Windows 专有实现只能待在 src/Platform/Windows/** 里（豁免区）。
# 运行期的 Windows 命令调用（cmd.exe / taskkill / netstat / where）**不算违规**，只统计数量，
# 因为它们是行为耦合而非编译耦合，属阶段 3 要用接缝抽象的对象。
# 退出码：0=通过；1=发现违规
param([string]$Repo = ".")
$ErrorActionPreference = "Stop"
$forbidden = @('System.Windows.Forms','System.Drawing','Microsoft.Win32','DllImport','System.Management','Microsoft.VisualBasic','Console.Beep','System.Web','System.ServiceProcess')
$files = @((Join-Path $Repo 'dsh_v2.cs'))
foreach ($sub in @('src\Core','src\Cli')) {
    $d = Join-Path $Repo $sub
    if (Test-Path $d) { $files += (Get-ChildItem $d -Recurse -Filter *.cs | ForEach-Object FullName) }
}
$viol = New-Object System.Collections.Generic.List[string]
$runtimeCalls = 0
foreach ($f in $files) {
    $txt = [System.IO.File]::ReadAllText($f)
    foreach ($pat in $forbidden) {
        $c = ([regex]::Matches($txt, [regex]::Escape($pat))).Count
        if ($c -gt 0) { $viol.Add(($f.Substring($Repo.Length).TrimStart('\') + " : " + $pat + " x" + $c)) }
    }
    $runtimeCalls += ([regex]::Matches($txt, 'cmd\.exe|taskkill|netstat|where dsh')).Count
}
Write-Host ("扫描核心源文件 {0} 个；运行期 Windows 命令调用 {1} 处（允许，阶段 3 用接缝抽象）" -f $files.Count, $runtimeCalls)
if ($viol.Count -gt 0) {
    Write-Host "RESULT: FAIL —— 核心出现 Windows-only 编译期依赖：" -ForegroundColor Red
    $viol | ForEach-Object { Write-Host ("  " + $_) }
    exit 1
}
Write-Host "RESULT: CORE PORTABLE OK（核心零 Windows-only 编译期 API）" -ForegroundColor Green
exit 0