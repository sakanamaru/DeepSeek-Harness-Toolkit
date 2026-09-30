# dsh-minato 安装器构建脚本（Windows ✓）
#
# 为什么用 Roslyn csc 而不是 in-box csc（踩过的坑 ✓）：
#   · in-box 的 `Framework64\v4.0.30319\csc.exe` 只有 **C# 5** ✗ —— 连字符串插值都编不过（CS1056）✓
#   · VS2022 自带的 **Roslyn csc 4.14** 能编现代 C# 且**直接定位 net48** ✓（无需 targeting pack ✓）
#   · net48 是 **Windows 系统组件** ✓ → 不算第三方依赖 ✓✓（与 C 运行时同类 ✓）
#
# 为什么 ZipArchive 要显式 /r:（子代理实测 ✓ 我也复现了 ✓）：
#   · 默认引用集里**没有** System.IO.Compression.FileSystem ✗ → 必须显式 /r: 到 GAC ✓
#
# 用法：
#   pwsh -File v3\tools\build_installer.ps1 -PayloadDir <已打包的目录> -Version 3.0.0 -Out <输出exe>
#   或先 `-PayloadZip <已有zip>` 直接复用 ✓
param(
    [string]$PayloadDir = "",
    [string]$PayloadZip = "",
    [string]$Version = "3.0.0",
    [string]$Out = "",
    [string]$Repo = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)
)
$ErrorActionPreference = "Stop"
$u8 = New-Object System.Text.UTF8Encoding($false)

$roslyn = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe"
if (-not (Test-Path $roslyn)) { throw "找不到 Roslyn csc：$roslyn（本机靠 VS2022 ✓ 没装就用不了现代 C# ✗）" }
$ico = Join-Path $Repo "v3\gui\Dsht.Gui.Avalonia\Assets\logo-icon.ico"
$src = Join-Path $Repo "v3\tools\installer.cs"
if (-not (Test-Path $src)) { throw "找不到安装器源码：$src" }

$work = Join-Path $env:TEMP ("dsht-setup-" + [Guid]::NewGuid().ToString("N").Substring(0, 8))
New-Item -ItemType Directory -Path $work -Force | Out-Null

# ---- ① 载荷：优先用现成 zip ✓ 否则从目录打一个 ✓ ----
if ($PayloadZip -eq "") {
    if ($PayloadDir -eq "" -or -not (Test-Path $PayloadDir)) { throw "必须给 -PayloadDir 或 -PayloadZip" }
    $PayloadZip = Join-Path $work "payload.zip"
    Write-Host "打包载荷：$PayloadDir"
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    # 用 ZipFile 但**统一正斜杠** ✓（解压侧也做了 zip-slip 校验 ✓）
    [System.IO.Compression.ZipFile]::CreateFromDirectory($PayloadDir, $PayloadZip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
    Write-Host ("  载荷 " + [Math]::Round((Get-Item $PayloadZip).Length / 1MB, 1) + " MB ✓")
}
if (-not (Test-Path $PayloadZip)) { throw "载荷 zip 不存在：$PayloadZip" }

# ---- ② AssemblyInfo（csc 没有 /version: ✓ 必须自己给 ✓ 否则 ARP 里显示 0.0.0 ✗）----
$ai = @"
using System.Reflection;
[assembly: AssemblyVersion("$Version.0")]
[assembly: AssemblyFileVersion("$Version.0")]
[assembly: AssemblyTitle("dsh-minato setup")]
[assembly: AssemblyProduct("dsh-minato")]
[assembly: AssemblyCompany("dsh-minato (unofficial)")]
[assembly: AssemblyDescription("dsh-minato self-extracting installer")]
"@
[System.IO.File]::WriteAllText((Join-Path $work "AssemblyInfo.cs"), $ai, $u8)

# ---- ③ 编译 ✓ ----
$gac = "C:\Windows\Microsoft.NET\assembly\GAC_MSIL"
$refs = @(
    "/r:$gac\System.IO.Compression\v4.0_4.0.0.0__b77a5c561934e089\System.IO.Compression.dll",
    "/r:$gac\System.IO.Compression.FileSystem\v4.0_4.0.0.0__b77a5c561934e089\System.IO.Compression.FileSystem.dll"
)
if ($Out -eq "") { $Out = Join-Path $Repo ("dsh-minato-" + $Version + "-win-x64-setup.exe") }
# ---- ③a 先编**不带载荷**的小卸载器 ✓✓（评审要求：卸载器不能带 70MB 载荷 ✗ 实测装出来 302MB ✗）----
$unExe = Join-Path $work "uninstall.exe"
Write-Host "编译卸载器（不带载荷 ✓）…"
$argsU = @("/nologo", "/target:winexe", "/platform:anycpu", "/win32icon:$ico", "/out:$unExe") + $refs + @($src, (Join-Path $work "AssemblyInfo.cs"))
$ru = & $roslyn @argsU 2>&1
$eu = @($ru | Where-Object { $_ -match "error" })
if ($eu.Count -gt 0) {
    $eu | Select-Object -First 6 | ForEach-Object { Write-Host ("  " + $_.ToString().Trim()) }
    throw "卸载器编译失败"
}
Write-Host ("  卸载器 " + [Math]::Round((Get-Item $unExe).Length / 1KB, 1) + " KB ✓")

# ---- ③b 再编**带载荷 + 内嵌小卸载器**的安装器 ✓✓ ----
$args = @("/nologo", "/target:winexe", "/platform:anycpu", "/win32icon:$ico", "/out:$Out",
          "/resource:$PayloadZip,payload.zip", "/resource:$unExe,uninstall.exe") + $refs + @($src, (Join-Path $work "AssemblyInfo.cs"))
Write-Host "编译安装器…"
$r = & $roslyn @args 2>&1
$errs = @($r | Where-Object { $_ -match "error" })
if ($errs.Count -gt 0) {
    $errs | Select-Object -First 8 | ForEach-Object { Write-Host ("  " + $_.ToString().Trim()) }
    throw "编译失败（$($errs.Count) 个错）"
}
$fi = Get-Item $Out
Write-Host ("✓ 安装器生成：" + $Out)
Write-Host ("  大小 " + [Math]::Round($fi.Length / 1MB, 1) + " MB（= 载荷 + ~40 KB 壳 ✓）")
Write-Host ""
Write-Host "用法（安装器自身支持）："
Write-Host "  dsh-minato-*-setup.exe                     图形界面安装"
Write-Host "  dsh-minato-*-setup.exe --silent --dir=<路径>  静默安装（Scoop/winget/CI 用 ✓）"
Write-Host "  dsh-minato-*-setup.exe --silent --no-path --no-shortcuts"
Write-Host "  <安装目录>\uninstall.exe --uninstall        卸载（**默认不删数据** ✓）"
Write-Host ""
Write-Host "签名（可选 ✓ 你拿到证书后）："
Write-Host "  signtool sign /fd SHA256 /a <内层 exe>… 然后**最后**签外层安装器 ✓"
Write-Host "  （先签内层再签外层 ✓ 反了会让内层签名失效 ✗ 因为追加载荷会改外层 ✓）"
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
