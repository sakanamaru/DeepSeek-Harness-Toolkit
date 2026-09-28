# compare_markers.ps1 —— V3 与 v2.x 的「标记行契约」比对（目标切换门槛之一）
# 默认只比对机器可读标记行（^STATUS_/PROFILECHK_/...）；full=$true 的命令比对整份输出（含裸路径行）。
# -Fixtures：临时造 3 个受控备份（覆盖 backup-list --detail 分支与有效性过滤），跑完即清理。
# 关键：V3 exe 必须与 v2.x exe **同目录**——因为两者都把状态目录解析为 exe 所在目录（备份根 = 状态目录/backup）。
# 退出码：0=全部对齐；1=有差异；2=环境不足（缺 v2.x exe 或 csc）
param([string]$Repo = ".", [switch]$Fixtures)
$ErrorActionPreference = "Stop"
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { Write-Host "SKIP: 找不到 csc（需 Windows + .NET Framework 4.x）"; exit 2 }
$v2 = Join-Path $Repo 'DeepSeek Harness Toolkit.exe'
if (-not (Test-Path $v2)) { Write-Host "SKIP: 找不到 v2.x exe（$v2）——先在仓库根构建 v2.x"; exit 2 }
$v3exe = Join-Path $Repo 'dsht_v3_contract.exe'          # 与 v2.x 同目录 → 状态目录一致
$files = @(Get-ChildItem (Join-Path $Repo 'v3\src') -Recurse -Filter *.cs | ForEach-Object FullName)
& $csc /nologo /target:exe /warn:4 ("/out:" + $v3exe) $files | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "FAIL: V3 编译失败"; exit 2 }

$created = New-Object System.Collections.Generic.List[string]
if ($Fixtures) {
    $bk = Join-Path $Repo 'backup'
    New-Item -ItemType Directory -Path $bk -Force | Out-Null
    $specs = @(
        @{ n = 'dsh-data-20990101-000000-auto';     f = @('settings.yaml','sessions') },
        @{ n = 'dsh-data-20990102-000000-pre-wipe'; f = @('credentials.yaml') },
        @{ n = 'dsh-data-20990103-000000';          f = @('readme.txt') }   # 名字合法但内容无特征 → 应判无效
    )
    foreach ($s in $specs) {
        $d = Join-Path $bk $s.n
        New-Item -ItemType Directory -Path $d -Force | Out-Null
        foreach ($fn in $s.f) { [System.IO.File]::WriteAllText((Join-Path $d $fn), 'x', (New-Object System.Text.UTF8Encoding($false))) }
        $created.Add($d)
    }
    Write-Host ("  [fixtures] 已造 {0} 个受控备份" -f $created.Count)
    $fixture = Join-Path $env:TEMP 'dsht_bootdiag_fixture.txt'
    $profileYml = Join-Path $env:USERPROFILE '.dsh\profiles\web\cordis.patch.yml'
    $nl = [Environment]::NewLine
    $bdl = @(
        'Error: plugin tree failed to load',
        '  failed to apply loader entry include (cordis:include)',
        '  failed to apply loader entry subagent-acp-kimi (@deepseek-ai/dsh-subagent-acp)',
        '  provider cannot enforce maxDepth',
        '  at file:///' + ($profileYml -replace '\\', '/') + '#subagent-acp-kimi',
        "  set maxDepth: 'provider-managed'"
    )
    [System.IO.File]::WriteAllText($fixture, ($bdl -join $nl) + $nl, (New-Object System.Text.UTF8Encoding($false)))
    $created.Add($fixture)
    $unk = Join-Path $env:TEMP 'dsht_bootdiag_unknown.txt'
    [System.IO.File]::WriteAllText($unk, ('some random crash' + [Environment]::NewLine + 'Error: boom' + [Environment]::NewLine), (New-Object System.Text.UTF8Encoding($false)))
    $created.Add($unk)
}

$cases = @(
    @{ name = 'status';               args = @('status') },
    @{ name = 'status --detail';      args = @('status','--detail') },
    @{ name = 'profilecheck';         args = @('profilecheck') },
    @{ name = 'profilecheck --abs';   args = @('profilecheck','--abs') },
    @{ name = 'backup-list';          args = @('backup-list'); full = $true },
    @{ name = 'backup-list --detail'; args = @('backup-list','--detail'); full = $true },
    # doctor：Integrity 行依赖 exe 身份（v2.x 的 exe 名在 hashes.txt 里、本地构建哈希不匹配 → ERROR；V3 临时 exe 名不在清单 → 跳过校验）。正式发布时 V3 用同名 exe，该类别行为一致。
    @{ name = 'bootdiag (no input)';   args = @('bootdiag'); full = $true },
    @{ name = 'bootdiag (fixture)';    args = @('bootdiag','--from',(Join-Path $env:TEMP 'dsht_bootdiag_fixture.txt')); full = $true },
    @{ name = 'bootdiag (unrecognised)'; args = @('bootdiag','--from',(Join-Path $env:TEMP 'dsht_bootdiag_unknown.txt')); full = $true },
    @{ name = 'config-get';          args = @('config-get'); full = $true },
    @{ name = 'doctor';               args = @('doctor'); full = $true; ignore = '^\[(OK|WARN|ERROR)\] Integrity '; ignoreSummary = $true }
)
$fail = 0
foreach ($c in $cases) {
    $o2 = (& $v2 @($c.args) 2>&1 | Out-String)
    $o3 = (& $v3exe @($c.args) 2>&1 | Out-String)
    $ignored = 0
    if ($c.full) {
        $m2 = @(($o2 -split "`r?`n") | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
        $m3 = @(($o3 -split "`r?`n") | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
        if ($c.ignore) {
            $before = $m2.Count
            $m2 = @($m2 | Where-Object { $_ -notmatch $c.ignore })
            $m3 = @($m3 | Where-Object { $_ -notmatch $c.ignore })
            $ignored = $before - $m2.Count
        }
        if ($c.ignoreSummary) {
            # 汇总行（DOCTOR_* n）会因被忽略的条目而不同 → 只作 INFO；汇总逻辑本身有单测覆盖
            $s2 = $m2[0]; $s3 = $m3[0]
            $m2 = @($m2 | Select-Object -Skip 1)
            $m3 = @($m3 | Select-Object -Skip 1)
            Write-Host ("      [info] 汇总: v2.x=$s2 / V3=$s3（差异源于被忽略条目；汇总逻辑由契约测试覆盖）")
        }
    } else {
        $m2 = @(($o2 -split "`r?`n") | Where-Object { $_ -match '^(STATUS|PROFILECHK|DOCTOR|BACKUP|DRYRUN)_[A-Z0-9_]+' } | ForEach-Object { $_.Trim() })
        $m3 = @(($o3 -split "`r?`n") | Where-Object { $_ -match '^(STATUS|PROFILECHK|DOCTOR|BACKUP|DRYRUN)_[A-Z0-9_]+' } | ForEach-Object { $_.Trim() })
    }
    if ((($m2 -join '|') -eq ($m3 -join '|'))) {
        Write-Host ("  {0,-18} PASS  [{1} 行{2}]" -f $c.name, $m2.Count, $(if ($ignored -gt 0) { "，按规则忽略 $ignored 行" } else { "" }))
    } else {
        $fail++
        Write-Host ("  {0,-18} FAIL" -f $c.name) -ForegroundColor Red
        Write-Host ("      v2.x: " + (($m2 | Select-Object -First 6) -join ' || '))
        Write-Host ("      V3  : " + (($m3 | Select-Object -First 6) -join ' || '))
    }
}

# 清理：受控备份 + 临时 exe（只删本次创建的）
foreach ($d in $created) { try { Remove-Item $d -Recurse -Force -ErrorAction SilentlyContinue } catch { } }
$bkRoot = Join-Path $Repo 'backup'
if ($Fixtures -and (Test-Path $bkRoot) -and ((Get-ChildItem $bkRoot -ErrorAction SilentlyContinue | Measure-Object).Count -eq 0)) { Remove-Item $bkRoot -Force -ErrorAction SilentlyContinue }
Remove-Item $v3exe -Force -ErrorAction SilentlyContinue

Write-Host ("== 标记行契约：{0}/{1} 对齐 ==" -f ($cases.Count - $fail), $cases.Count)
if ($fail -gt 0) { exit 1 }
exit 0