# verify_domain_pure.ps1 —— V3 领域层「纯净度」守卫
# 断言 Dsht.Domain 里不出现：IO / 网络 / 进程 / 控制台 / 平台分支 / 时钟耦合 / 平台专有 API。
# 理由：领域逻辑必须能 100% 单测，并被任何平台实现复用；一旦引入这些依赖，"纯"就没了。
# 退出码：0=通过；1=有违规
param([string]$Repo = ".")
$ErrorActionPreference = "Stop"
$dir = Join-Path $Repo 'v3\src\Dsht.Domain'
if (-not (Test-Path $dir)) { Write-Host "SKIP: 领域层尚未建立（$dir）"; exit 2 }
$forbidden = @(
    'using System.IO', 'System.IO.', 'File.', 'Directory.', 'Path.Combine',        # 文件系统
    'using System.Net', 'System.Net.', 'Socket', 'HttpWebRequest',                 # 网络
    'using System.Diagnostics', 'Process.', 'ProcessStartInfo',                    # 进程
    'Console.',                                                                    # 控制台
    'System.Windows.Forms', 'System.Drawing', 'Microsoft.Win32', 'DllImport',      # 平台专有
    'RuntimeInformation', 'OSVersion', 'Environment.SpecialFolder',                # 平台分支
    'Environment.GetEnvironmentVariable',                                          # 环境读取
    'DateTime.Now', 'DateTime.UtcNow', 'DateTimeOffset.Now', 'Thread.Sleep'        # 时钟/时间耦合
)
$files = @(Get-ChildItem $dir -Recurse -Filter *.cs -ErrorAction SilentlyContinue | ForEach-Object FullName)
$viol = New-Object System.Collections.Generic.List[string]
foreach ($f in $files) {
    $txt = [System.IO.File]::ReadAllText($f)
    foreach ($pat in $forbidden) {
        $c = ([regex]::Matches($txt, [regex]::Escape($pat))).Count
        if ($c -gt 0) { $viol.Add(($f.Substring($Repo.Length).TrimStart('\') + " : " + $pat + " x" + $c)) }
    }
}
Write-Host ("扫描领域层源文件 {0} 个；违规 {1} 处" -f $files.Count, $viol.Count)
if ($viol.Count -gt 0) {
    Write-Host "RESULT: FAIL —— 领域层出现非纯净依赖：" -ForegroundColor Red
    $viol | ForEach-Object { Write-Host ("  " + $_) }
    exit 1
}
Write-Host "RESULT: DOMAIN PURE OK（零 IO / 零平台 / 零时钟耦合）" -ForegroundColor Green
exit 0