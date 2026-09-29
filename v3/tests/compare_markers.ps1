# compare_markers.ps1 —— V3 与 v2.x 的「标记行契约」比对（目标切换门槛之一）
# 默认只比对机器可读标记行（^STATUS_/PROFILECHK_/...）；full=$true 的命令比对整份输出（含裸路径行）。
# -Fixtures：临时造 3 个受控备份（覆盖 backup-list --detail 分支与有效性过滤），跑完即清理。
# 关键：V3 exe 必须与 v2.x exe **同目录**——因为两者都把状态目录解析为 exe 所在目录（备份根 = 状态目录/backup）。
# 退出码：0=全部对齐；1=有差异；2=环境不足（缺 v2.x exe 或 csc）
param([string]$Repo = ".", [switch]$Fixtures, [switch]$Heavy)
$ErrorActionPreference = "Stop"
# 统一转成绝对路径：v2.x 的 P() 会给相对路径加 \\?\ 前缀（\\?\.\backup\x 是非法 Win32 路径），
# 于是 `--path .\backup\...` 在 v2.x 里源侧遍历静默失败（DRYRUN_NEW/OVERWRITE 全 0），
# 而 V3 用相对路径能正常遍历 → 用 `-Repo .` 调用时会比对出**假差异**。绝对路径两边都正确。
$Repo = (Resolve-Path -LiteralPath $Repo).Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { Write-Host "SKIP: 找不到 csc（需 Windows + .NET Framework 4.x）"; exit 2 }
$v2 = Join-Path $Repo 'DeepSeek Harness Toolkit.exe'
if (-not (Test-Path $v2)) { Write-Host "SKIP: 找不到 v2.x exe（$v2）——先在仓库根构建 v2.x"; exit 2 }
$v3exe = Join-Path $Repo 'dsht_v3_contract.exe'          # 与 v2.x 同目录 → 状态目录一致
$files = @(Get-ChildItem (Join-Path $Repo 'v3\src') -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' } | ForEach-Object FullName)
& $csc /nologo /target:exe /warn:4 ("/out:" + $v3exe) $files | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "FAIL: V3 编译失败"; exit 2 }

$created = New-Object System.Collections.Generic.List[string]
if ($Fixtures) {
    $bk = Join-Path $Repo 'backup'
    New-Item -ItemType Directory -Path $bk -Force | Out-Null
    $specs = @(
        @{ n = 'dsh-data-20990101-000000-auto';     f = @('settings.yaml','sessions') },
        @{ n = 'dsh-data-20990102-000000-pre-wipe'; f = @('credentials.yaml') },
        @{ n = 'dsh-data-20990103-000000';          f = @('readme.txt') },   # 名字合法但内容无特征 → 应判无效
        # 带工作区（新格式：_workspace\<名字>\.dshws）→ 覆盖 dry-run 的 workspace 作用域分支。
        # 目标 = 自动探测到的工作区根 + 工作区名（两侧同 exe 目录，所以目标一致、可比对）；
        # 刻意**不**做旧格式（_workspace 直接放内容）用例：那时目标=工作区根本身，会去遍历一棵大树，
        # 而两次运行之间树会变（日志/临时文件）→ 计数不稳定。
        @{ n = 'dsh-data-20990104-000000-auto';     f = @('settings.yaml'); ws = @('proj1') }
    )
    foreach ($s in $specs) {
        $d = Join-Path $bk $s.n
        New-Item -ItemType Directory -Path $d -Force | Out-Null
        foreach ($fn in $s.f) { [System.IO.File]::WriteAllText((Join-Path $d $fn), 'x', (New-Object System.Text.UTF8Encoding($false))) }
        if ($s.ws) {
            foreach ($wn in $s.ws) {
                $wd = Join-Path (Join-Path $d '_workspace') $wn
                New-Item -ItemType Directory -Path $wd -Force | Out-Null
                [System.IO.File]::WriteAllText((Join-Path $wd '.dshws'), '', (New-Object System.Text.UTF8Encoding($false)))
                [System.IO.File]::WriteAllText((Join-Path $wd 'a.txt'), 'A', (New-Object System.Text.UTF8Encoding($false)))
            }
        }
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
    @{ name = 'profilecheck';         args = @('profilecheck') ; ignore = 'PROFILECHK_READ_ERRORS|PROFILECHK_INCOMPLETE' },
    @{ name = 'profilecheck --abs';   args = @('profilecheck','--abs') },
    @{ name = 'backup-list';          args = @('backup-list'); full = $true; ignore = '^BACKUP_LIST_IGNORED ' },
    @{ name = 'backup-list --detail'; args = @('backup-list','--detail'); full = $true; ignore = '^BACKUP_LIST_IGNORED ' },
    # doctor：Integrity 行依赖 exe 身份（v2.x 的 exe 名在 hashes.txt 里、本地构建哈希不匹配 → ERROR；V3 临时 exe 名不在清单 → 跳过校验）。正式发布时 V3 用同名 exe，该类别行为一致。
    @{ name = 'bootdiag (no input)';   args = @('bootdiag'); full = $true },
    @{ name = 'bootdiag (fixture)';    args = @('bootdiag','--from',(Join-Path $env:TEMP 'dsht_bootdiag_fixture.txt')); full = $true },
    @{ name = 'bootdiag (unrecognised)'; args = @('bootdiag','--from',(Join-Path $env:TEMP 'dsht_bootdiag_unknown.txt')); full = $true },
    @{ name = 'restore --dry-run';          args = @('restore','--dry-run'); full = $true },
    @{ name = 'restore --dry-run --path';   args = @('restore','--dry-run','--path',(Join-Path $Repo 'backup\dsh-data-20990101-000000-auto')); full = $true },
    # 工作区作用域（受控备份里带 _workspace\<名字>\.dshws）：目标 = 自动探测的工作区根 + 工作区名，
    # 两侧 exe 同目录 → 目标一致。这条同时钉住"工作区自动探测"与"dry-run 的 workspace 分支"。
    @{ name = 'restore --dry-run (_workspace)'; args = @('restore','--dry-run','--path',(Join-Path $Repo 'backup\dsh-data-20990104-000000-auto')); full = $true },
    # selftest：stdout 只有 "report -> 路径"，真正的价值在报告正文 → post='report' 时比较报告内容
    # 产品标识行（title/version）在 v2.x 与 V3 之间本就不同，按规则忽略
    @{ name = 'selftest (report body)'; args = @('selftest'); post = 'report'; ignore = '^(title|version)\s+:' },
    # check：横幅与"dsh 最新"行按规则忽略（前者是产品版本差异，后者依赖网络）
    @{ name = 'check'; args = @('check'); full = $true; ignore = '^(=+|-+)$|^\s*(DeepSeek Harness Toolkit V|v1 脚本协助|v2 重构封装|GitHub\s|⚠|dsh 最新\s+:)' },
    # backup：真实写盘（每次约 400MB）→ 用 -Heavy 按需开启；目录名含时间戳，比对时归一化
    @{ name = 'backup (heavy)'; args = @('backup'); full = $true; heavy = $true; mask = 'dsh-data-\d{8}-\d{9,}(-\d+)?'; ignore = '已跳过 \d+ 个嵌套备份目录' },
    @{ name = 'restore --path (outside)'; args = @('restore','--path','C:\nope\outside'); full = $true },
    @{ name = 'restore --path (invalid, never exists)'; args = @('restore','--path',(Join-Path $Repo 'backup\dsh-data-19990101-000000000')); full = $true },
    @{ name = 'backup-delete (outside)'; args = @('backup-delete','--path','C:\nope\x'); full = $true },
    @{ name = 'backup-export (no-to)'; args = @('backup-export','--path',(Join-Path $Repo 'backup\dsh-data-1')); full = $true },
    # restore（无参）：有受控备份时会走到"运行中拒绝"闸门（dsh 在跑 → 不写任何东西，安全可比对）。
    # needsService：**只有服务在运行时才允许跑**——否则 v2.x 会真的把受控备份恢复进真实 ~/.dsh。
    @{ name = 'restore (latest)'; args = @('restore'); full = $true; needsService = $true },
    @{ name = 'config-get';          args = @('config-get'); full = $true },
    @{ name = 'doctor';               args = @('doctor'); full = $true; ignore = '^\[(OK|WARN|ERROR)\] Integrity |^\[(OK|WARN|ERROR)\] Network |^\[(OK|WARN|ERROR)\] Backup |^\[(OK|WARN|ERROR)\] Workspace 数据大小'; ignoreSummary = $true },
    # doctor --report：比对**报告正文**（postFile 模式）。
    # 忽略：生成时间/Toolkit/系统三行（时间戳与版本必然不同）、自身完整性条目（v2.x 的 exe 在清单里但本地构建
    # 哈希不匹配 → ERROR；V3 的临时 exe 名不在清单 → 跳过）、npm registry 可达性（网络抖动会让两侧不同 → 假失败）、
    # 结果行（汇总数受被忽略条目影响）。
    # 掩码：日志摘要行（两次运行之间日志会增长，且内容含时间戳）——掩码后仍能验证"该行两侧都存在且前缀一致"。
    @{ name = 'doctor --report (body)'; args = @('doctor','--report',(Join-Path $env:TEMP 'dsht_doctor_report_cmp.txt')); postFile = (Join-Path $env:TEMP 'dsht_doctor_report_cmp.txt'); ignore = '^(生成时间|Toolkit|系统)\s*:|^\[(OK|WARN|ERROR)\] (自身 exe 与随包|旁无 hashes\.txt|npm registry )|^结果\s*:'; mask = '共 \d+ 行；最近: .*' }
)
$fail = 0
$skipped = 0
# 服务是否在运行：restore 类用例的**唯一**安全依据（v2.x 忽略 $DSH_HOME，只会写真实数据根）
$svcUp = ((& $v2 status 2>&1 | Out-String) -match 'STATUS_UP')
if (-not $svcUp) { Write-Host "  [warn] 服务未运行：restore 类用例将 SKIP（服务在跑时它们才只走到拒绝闸门、不写盘）" -ForegroundColor Yellow }
# 真实数据根快照（安全网）：整轮跑完必须一模一样，否则说明有用例真的写了用户数据
$realRoots = @()
foreach ($cand in @((Join-Path $env:USERPROFILE '.dsh'), (Join-Path $env:APPDATA '.dsh'), (Join-Path $env:LOCALAPPDATA '.dsh'))) {
    if ($cand -and (Test-Path -LiteralPath $cand)) {
        $items = Get-ChildItem -LiteralPath $cand -Force -ErrorAction SilentlyContinue | Sort-Object Name | ForEach-Object { if ($_.PSIsContainer) { 'D:' + $_.Name } else { 'F:' + $_.Name + ':' + $_.Length } }
        $realRoots += @{ path = $cand; snap = ($items -join '|') }
    }
}
# 备份状态只在循环前捕获一次（否则后续用例会误判"原本就存在"）
$bkRoot2 = Join-Path $Repo 'backup'
$bkExisted = Test-Path $bkRoot2
$bkBefore = @()
if ($bkExisted) { $bkBefore = @(Get-ChildItem $bkRoot2 -Directory -ErrorAction SilentlyContinue | ForEach-Object { $_.Name }) }
foreach ($c in $cases) {
    if ($c.heavy -and -not $Heavy) { Write-Host ("  {0,-18} SKIP  （需 -Heavy）" -f $c.name); $script:skipped++; continue }
    if ($c.needsService -and -not $svcUp) { Write-Host ("  {0,-18} SKIP  （服务未运行：真实恢复用例只在服务运行时才安全）" -f $c.name); $script:skipped++; continue }
    $o2 = (& $v2 @($c.args) 2>&1 | Out-String)
    if ($c.post -eq 'report') {
        $rp = Join-Path $env:TEMP 'dsh_selftest.txt'
        if (Test-Path $rp) { $o2 = [System.IO.File]::ReadAllText($rp) }
    }
    if ($c.postFile) { if (Test-Path -LiteralPath $c.postFile) { $o2 = [System.IO.File]::ReadAllText($c.postFile) } }
    $o3 = (& $v3exe @($c.args) 2>&1 | Out-String)
    if ($c.post -eq 'report') {
        $rp2 = Join-Path $env:TEMP 'dsh_selftest.txt'
        if (Test-Path $rp2) { $o3 = [System.IO.File]::ReadAllText($rp2) }
    }
    if ($c.postFile) { if (Test-Path -LiteralPath $c.postFile) { $o3 = [System.IO.File]::ReadAllText($c.postFile) } }
    $ignored = 0
    
    if ($c.full -or $c.post -eq 'report' -or $c.postFile) {
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
    if ($c.mask) {
        $m2 = @($m2 | ForEach-Object { [regex]::Replace($_, $c.mask, 'dsh-data-TS') })
        $m3 = @($m3 | ForEach-Object { [regex]::Replace($_, $c.mask, 'dsh-data-TS') })
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

# 安全网：真实数据根必须与开跑前完全一致——任何"用例真的写了用户数据"都会在这里暴露
$rootDirty = ''
foreach ($r in $realRoots) {
    $items = Get-ChildItem -LiteralPath $r.path -Force -ErrorAction SilentlyContinue | Sort-Object Name | ForEach-Object { if ($_.PSIsContainer) { 'D:' + $_.Name } else { 'F:' + $_.Name + ':' + $_.Length } }
    if (($items -join '|') -ne $r.snap) { $rootDirty += ($r.path + ' ') }
}
if ($rootDirty -ne '') { $fail++; Write-Host ("  [FAIL] 真实数据根被改动：" + $rootDirty + "——有用例真的写了用户数据！") -ForegroundColor Red }
else { Write-Host "  [ok] 真实数据根未被触碰（快照比对通过）" }

# 清理：受控备份 + 本次 backup 用例新建的备份 + 临时 exe
if ($Heavy -and (Test-Path $bkRoot2)) {
    if (-not $bkExisted) { Remove-Item $bkRoot2 -Recurse -Force -ErrorAction SilentlyContinue }
    else {
        foreach ($d in @(Get-ChildItem $bkRoot2 -Directory -ErrorAction SilentlyContinue)) {
            if ($bkBefore -notcontains $d.Name) { Remove-Item $d.FullName -Recurse -Force -ErrorAction SilentlyContinue }
        }
    }
}
foreach ($d in $created) { try { Remove-Item $d -Recurse -Force -ErrorAction SilentlyContinue } catch { } }
$bkRoot = Join-Path $Repo 'backup'
if ($Fixtures -and (Test-Path $bkRoot) -and ((Get-ChildItem $bkRoot -ErrorAction SilentlyContinue | Measure-Object).Count -eq 0)) { Remove-Item $bkRoot -Force -ErrorAction SilentlyContinue }
Remove-Item $v3exe -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:TEMP 'dsht_doctor_report_cmp.txt') -Force -ErrorAction SilentlyContinue   # doctor --report 用例的产物

$total = $cases.Count - $skipped
Write-Host ("== 标记行契约：{0}/{1} 对齐{2} ==" -f ($total - $fail), $total, $(if ($skipped -gt 0) { "（另有 $skipped 项需 -Heavy）" } else { "" }))
if ($fail -gt 0) { exit 1 }
exit 0