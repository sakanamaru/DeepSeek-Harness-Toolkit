# verify_domain_pure.ps1 —— V3 领域层「纯净度」守卫
# 断言 Dsht.Domain 里不出现：IO / 网络 / 进程 / 控制台 / 平台分支 / 时钟耦合 / 平台专有 API。
# 实现要点：**扫描前先剥离注释**（上一版直接子串匹配，连注释里提到 File.Exists 都会误报）。
# ★★ G8 补强（2026-10-01）：原来有两处**可绕过** ✗ ——
#   ① `System . IO`（C# 允许点号旁有空格 ✓）→ 现在**先做点号两侧空白归一化** ✓
#   ② `using IO = System.IO;`（别名 ✓）→ 现在**加裸命名空间模式** ✓（别名行里没有尾点 ✗）
# ⚠ **诚实的边界**：这仍是**文本门槛** ✓ —— 用**字符串拼接 + 反射**（Type.GetType("System" + ".IO...")）
#   理论上仍能绕过 ✓ 而那种写法在领域层里**一眼就可疑** ✓。
#   真正强的做法是**编译期/IL 级证明** ✗ —— 成本高 ✓ 暂不做 ✓ 但**不假装这门槛是强验证** ✓。
# 退出码：0=通过；1=有违规；2=领域层尚未建立
param([string]$Repo = ".")
$ErrorActionPreference = "Stop"
$dir = Join-Path $Repo 'v3/src/Dsht.Domain'
if (-not (Test-Path $dir)) { Write-Host "SKIP: 领域层尚未建立（$dir）"; exit 2 }

# 剥离注释：字符级状态机（识别 // 行注释、/* */ 块注释、'...' 与 "..." 字符串）
function Strip-Comments([string]$src) {
    $sb = New-Object System.Text.StringBuilder
    $i = 0; $n = $src.Length
    $inLine = $false; $inBlock = $false; $inS = $false; $inD = $false
    while ($i -lt $n) {
        $c = $src[$i]
        $next = if ($i + 1 -lt $n) { $src[$i + 1] } else { [char]0 }
        if ($inLine) { if ($c -eq "`n") { $inLine = $false; [void]$sb.Append($c) }; $i++; continue }
        if ($inBlock) { if ($c -eq '*' -and $next -eq '/') { $inBlock = $false; $i += 2; continue }; if ($c -eq "`n") { [void]$sb.Append($c) }; $i++; continue }
        if ($inS) { [void]$sb.Append($c); if ($c -eq "'") { $inS = $false }; $i++; continue }
        if ($inD) { [void]$sb.Append($c); if ($c -eq '"') { $inD = $false }; $i++; continue }
        if ($c -eq '/' -and $next -eq '/') { $inLine = $true; $i += 2; continue }
        if ($c -eq '/' -and $next -eq '*') { $inBlock = $true; $i += 2; continue }
        if ($c -eq "'") { $inS = $true; [void]$sb.Append($c); $i++; continue }
        if ($c -eq '"') { $inD = $true; [void]$sb.Append($c); $i++; continue }
        [void]$sb.Append($c); $i++
    }
    return $sb.ToString()
}

$forbidden = @(
    # ★★ G8 补强：加**裸命名空间**模式 ✓ —— 别名 using IO = System.IO; 里没有尾点 ✗
    #    只有裸 System.IO 能抓到它 ✓（点号归一化后仍无尾点 ✓）
    'using System.IO', 'System.IO', 'File.', 'Directory.', 'Path.Combine',
    'System.Net', 'System.Diagnostics',
    'using System.Net', 'System.Net.', 'Socket', 'HttpWebRequest',
    'using System.Diagnostics', 'Process.', 'ProcessStartInfo',
    'Console.',
    'System.Windows.Forms', 'System.Drawing', 'Microsoft.Win32', 'DllImport',
    'RuntimeInformation', 'OSVersion', 'Environment.SpecialFolder',
    'Environment.GetEnvironmentVariable',
    'DateTime.Now', 'DateTime.UtcNow', 'DateTimeOffset.Now', 'Thread.Sleep'
)
$files = @(Get-ChildItem $dir -Recurse -Filter *.cs -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' } | ForEach-Object FullName)
$viol = New-Object System.Collections.Generic.List[string]
foreach ($f in $files) {
    $txt = Strip-Comments ([System.IO.File]::ReadAllText($f))
    # ★★ G8 补强：**点号两侧空白归一化** ✓ —— C# 允许 `System . IO` ✗ 而旧模式匹配不到 ✗
    $txt = $txt -replace '\s*\.\s*', '.'
    foreach ($pat in $forbidden) {
        $c = ([regex]::Matches($txt, [regex]::Escape($pat))).Count
        if ($c -gt 0) { $viol.Add(($f.Substring($Repo.Length).TrimStart('\') + " : " + $pat + " x" + $c)) }
    }
}
Write-Host ("扫描领域层源文件 {0} 个（已剥离注释）；违规 {1} 处" -f $files.Count, $viol.Count)
if ($viol.Count -gt 0) {
    Write-Host "RESULT: FAIL —— 领域层出现非纯净依赖：" -ForegroundColor Red
    $viol | ForEach-Object { Write-Host ("  " + $_) }
    exit 1
}
Write-Host "RESULT: DOMAIN PURE OK（零 IO / 零平台 / 零时钟耦合）" -ForegroundColor Green
exit 0