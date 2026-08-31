# DeepSeek Harness Toolkit - 集成测试（端到端打桩矩阵）
# 用法:  pwsh -NoProfile -File tests\integration.ps1 [-RepoRoot <仓库根>]   （默认取脚本上级目录）
# 退出码: 0=全过（SKIP 不计失败）, 1=有失败, 2=环境/锚点错误
# 说明: ① 只碰打桩数据目录 ~/.dsh_test，绝不接触真实 ~/.dsh；
#       ② 变体 A=端口打桩（菜单/卸载流程全可测）；变体 C=真实端口（仅测"运行中"路径，3080 未开时相关用例标 SKIP）；
#       ③ 锚点断言保证本脚本与生产源码同步，源码改动导致锚点漂移时立即报错。
#       ④ 建议用 pwsh 7 运行；文件为 UTF-8 with BOM，PowerShell 5.1 亦可。
param([string]$RepoRoot = "")
$ErrorActionPreference = 'Stop'
if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }
$src = Join-Path $RepoRoot 'dsh_v2.cs'
if (-not (Test-Path -LiteralPath $src)) { Write-Error "找不到 $src"; exit 2 }
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path -LiteralPath $csc)) { Write-Error "找不到 csc：$csc（需 Windows + .NET Framework 4.x）"; exit 2 }

$results = New-Object System.Collections.Generic.List[string]
function TC($name, $ok, $extra = '') {
    $results.Add(("{0,-28} {1} {2}" -f $name, $(if ($ok) { 'PASS' } else { 'FAIL' }), $extra))
}
function NewDir($p) { if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Recurse -Force }; New-Item -ItemType Directory -Path $p -Force | Out-Null }

function Build-Variant($variant, $outExe) {
    $text = [IO.File]::ReadAllText($src, [Text.UTF8Encoding]::new($false))
    $pats = @(
        @{ o = 'const string DATA_DIR     = ".dsh";';                                                   n = 'const string DATA_DIR     = ".dsh_test";' },
        @{ o = '"DeepSeek-Harness-Toolkit-single"';                                                        n = '"DSH-Toolkit-TEST-single"' },
        @{ o = '"DSH-Toolkit-V2.0.0-single"';                                                           n = '"DSH-Toolkit-TEST-legacy"' },
        @{ o = 'string choice = autoApplied ? ReadChoice("  > ") : (installed ? CountdownInput("  > ", def) : ReadChoice("  > "));'; n = 'string choice = ReadLineTrim(); // TEST line-driven' },
        @{ o = 'int code = RunVisible("cmd.exe", "/c npm install -g --registry=" + registries[i] + " " + pkg);'; n = 'int code = 0; // TEST install no-op' },
        @{ o = 'int code = RunVisible("cmd.exe", "/c npm uninstall -g @deepseek-ai/dsh");';            n = 'int code = 0; // TEST npm no-op' }
    )
    if ($variant -eq 'A') {
        # 变体 A：端口全打桩（菜单/卸载流程可离线跑通）；变体 C 保留真实端口（测"运行中"路径）
        $pats += @{ o = 'if (ProbeService() == ServiceState.Ready)'; n = 'if (false && ProbeService() == ServiceState.Ready)' }
        $pats += @{ o = 'if (IsPortOpen(WEB_PORT, 600))'; n = 'if (false && IsPortOpen(WEB_PORT, 600))' }
        # v2.1：ProbeService 也要打桩——它内部裸调 IsPortOpen，不打桩会让"运行中拒绝"误触发（16/17 在 3080 开启时被拒）
        $pats += @{ o = 'return JudgeState(IsPortOpen(WEB_PORT, 800), HttpReady(WebUrl(), 800));'; n = 'return JudgeState(false, false); // TEST stub' }
    }
    foreach ($p in $pats) {
        if ([regex]::Matches($text, [regex]::Escape($p.o)).Count -lt 1) { Write-Error ("锚点漂移（请同步 integration.ps1）: " + $p.o.Substring(0, 50)); exit 2 }
        $text = $text.Replace($p.o, $p.n)
    }
    $tmp = Join-Path $env:TEMP ("t_dsh_" + $variant + ".cs")
    [IO.File]::WriteAllText($tmp, $text, [Text.UTF8Encoding]::new($false))
    & $csc /nologo /optimize+ /target:exe ("/out:" + $outExe) $tmp /warn:4 | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Error ("变体" + $variant + " 编译失败"); exit 2 }
    Remove-Item -LiteralPath $tmp -Force
}

$T = Join-Path $env:TEMP ("tkint_" + [guid]::NewGuid().ToString('N'))
NewDir $T
$tA = Join-Path $T 'tA.exe'; $tC = Join-Path $T 'tC.exe'
Build-Variant 'A' $tA
Build-Variant 'C' $tC
# 交互用例统一关闭启动更新检查（避免每次启动连 GitHub 的随机耗时/超时）
[IO.File]::WriteAllText((Join-Path $T 'launcher.config'), ('lang=zh' + [Environment]::NewLine + 'check_update=off' + [Environment]::NewLine))
$portLive = Test-NetConnection -ComputerName 127.0.0.1 -Port 3080 -InformationLevel Quiet -WarningAction SilentlyContinue
$date = Get-Date -Format 'yyyyMMdd'
$dt = Join-Path $env:USERPROFILE '.dsh_test'
function Seed-DT { Remove-Item -LiteralPath $dt -Recurse -Force -ErrorAction SilentlyContinue; New-Item -ItemType File -Path (Join-Path $dt 'settings.yaml') -Force | Out-Null }

Write-Output ("环境: 3080=" + $(if ($portLive) { '运行中' } else { '未运行' }) + "  仓库根=" + $RepoRoot)

# 1-5 基础 / CLI
& $tA selftest *> $null;   TC '1 selftest' ($LASTEXITCODE -eq 0)
$o = (& $tA help 2>&1 | Out-String);    TC '2 help' ($o.Contains('install | start | uninstall | update'))
$o = (& $tA about 2>&1 | Out-String);   TC '3 about' (($o.Contains('DeepSeek Harness Toolkit V2.1.4')) -and ($o.Contains('sakanamaru')))
$o = (& $tA check 2>&1 | Out-String);   TC '4 check CLI' ($o.Contains('dsh'))
$sw = [Diagnostics.Stopwatch]::StartNew(); $o = ("0`n" | & $tA 2>&1 | Out-String); $sw.Stop()
TC '5 menu exit 0' ($sw.ElapsedMilliseconds -lt 2000) ($sw.ElapsedMilliseconds.ToString() + 'ms')

# 23 单实例锁：双锁任一被占（新锁 / v2.0 旧锁）→ 第二个实例拒绝（轮询 holder 就绪，避免冷启动竞态）
$hcs = Join-Path $T 'holder.cs'
[IO.File]::WriteAllText($hcs, 'using System; using System.Threading; class H { [STAThread] static void Main(string[] a) { string n = (a != null && a.Length > 0) ? a[0] : "x"; var m = new Mutex(false, n); bool g = false; try { g = m.WaitOne(0); } catch (AbandonedMutexException) { g = true; } if (g) { Console.WriteLine("OK"); Thread.Sleep(30000); } } }', [Text.Encoding]::ASCII)
$holderExe = Join-Path $T 'holder.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo ("/out:"+$holderExe) $hcs 2>&1 | Out-Null
function Start-Holder([string]$lockName, [string]$outFile) {
    $hp = Start-Process -FilePath $holderExe -ArgumentList $lockName -RedirectStandardOutput $outFile -PassThru
    $deadline = (Get-Date).AddSeconds(6)
    while ((Get-Date) -lt $deadline) {
        if ((Test-Path $outFile) -and ((Get-Content $outFile -ErrorAction SilentlyContinue) -contains 'OK')) { return $hp }
        if ($hp.HasExited) { return $hp }
        Start-Sleep -Milliseconds 200
    }
    return $hp
}
$h1 = Start-Holder 'DSH-Toolkit-TEST-single' (Join-Path $T 'h1.txt')
$o23a = ("0`n" | & $tA 2>&1 | Out-String)
TC '23a second instance refused (new lock held)' ($o23a.Contains('已在运行') -or $o23a.Contains('already running'))
Stop-Process -Id $h1.Id -Force -ErrorAction SilentlyContinue
$h2 = Start-Holder 'DSH-Toolkit-TEST-legacy' (Join-Path $T 'h2.txt')
$o23b = ("0`n" | & $tA 2>&1 | Out-String)
TC '23b second instance refused (legacy v2.0 lock held)' ($o23b.Contains('已在运行') -or $o23b.Contains('already running'))
Stop-Process -Id $h2.Id -Force -ErrorAction SilentlyContinue

# 6-9 配置 / 安装源
$d = Join-Path $T 'lang'; NewDir $d; Copy-Item $tA (Join-Path $d 't.exe')
$o = ("4`n2`n0`n" | & (Join-Path $d 't.exe') 2>&1 | Out-String); $c1 = [IO.File]::ReadAllText((Join-Path $d 'launcher.config'))
$o = ("4`n3`n0`n" | & (Join-Path $d 't.exe') 2>&1 | Out-String); $c2 = [IO.File]::ReadAllText((Join-Path $d 'launcher.config'))
TC '6 lang persist' ($c1.Contains('lang=zh') -and $c2.Contains('lang=en'))
$o = ("7`n2`n0`n" | & (Join-Path $d 't.exe') 2>&1 | Out-String); $h1 = [IO.File]::ReadAllText((Join-Path $d 'launcher.config'))
$o = ("7`n1`n7`n3`n`n0`n" | & (Join-Path $d 't.exe') 2>&1 | Out-String); $h2 = [IO.File]::ReadAllText((Join-Path $d 'launcher.config'))
TC '7 entry+ws clear' ($h1.Contains('host=localhost') -and $h2.Contains('host=127.0.0.1') -and -not ($h2 -match 'ws=\S'))
$o = ("1`n1`n`n0`n" | & (Join-Path $d 't.exe') 2>&1 | Out-String); TC '8 install src official' ($o.Contains('registry.npmjs'))
$o = ("1`n2`n`n0`n" | & (Join-Path $d 't.exe') 2>&1 | Out-String); TC '9 install src mirror' ($o.Contains('npmmirror'))

# 10-12 卸载守卫
$d = Join-Path $T 'stray'; NewDir $d; Copy-Item $tA (Join-Path $d 't.exe'); Seed-DT
$o = ("6`ny`ny`n$date`nyes`n0`n" | & (Join-Path $d 't.exe') 2>&1 | Out-String)
TC '10 stray uninstall refused' (($o.Contains('未检测到完整安装') -or $o.Contains('does not look like a full installation')) -and (Test-Path (Join-Path $dt 'settings.yaml')))
# 10b stray exe + 2 个配套文件（伪造"像安装目录"）+ 无 marker -> 必须拒绝（防 LooksLikeFullInstall 绕过回归）
$d = Join-Path $T 'strayCmp'; NewDir $d; Copy-Item $tA (Join-Path $d 't.exe')
[IO.File]::WriteAllText((Join-Path $d 'dsh_v2.cs'), 'x'); [IO.File]::WriteAllText((Join-Path $d 'build_exe.cmd'), '@echo off')
$o = ("6`ny`ny`n$date`nyes`n0`n" | & (Join-Path $d 't.exe') 2>&1 | Out-String)
TC '10b stray+companion refused' ($o.Contains('未检测到完整安装') -or $o.Contains('does not look like a full installation'))
$d = Join-Path $T 'full'; NewDir $d; Copy-Item $tA (Join-Path $d 't.exe')
[IO.File]::WriteAllText((Join-Path $d '.dsh_launcher_root'), 'DeepSeek Harness Toolkit V2.1.4' + [Environment]::NewLine)
$o = ("6`ny`ny`n$date`nyes`n0`n" | & (Join-Path $d 't.exe') 2>&1 | Out-String)
TC '11 full uninstall wiped' (($o.Contains('数据已清除') -or $o.Contains('Data wiped')) -and (-not (Test-Path (Join-Path $dt 'settings.yaml'))))
$d = Join-Path $T 'cli'; NewDir $d; Copy-Item $tA (Join-Path $d 't.exe'); Seed-DT
$o = ("y`ny`n$date`nyes`n0`n" | & (Join-Path $d 't.exe') uninstall 2>&1 | Out-String)
TC '12 cli uninstall refused' ($o.Contains('未检测到完整安装') -or $o.Contains('does not look like a full installation'))

# 13-14,19 真实端口（3080 未开则 SKIP）
if ($portLive) {
    $pn0 = (Get-Process node -ErrorAction SilentlyContinue | Measure-Object).Count
    $o = ("2`n0`n" | & $tC 2>&1 | Out-String); $pn1 = (Get-Process node -ErrorAction SilentlyContinue | Measure-Object).Count
    TC '13 running->monitor' (($o.Contains('运行中') -or $o.Contains('RUNNING')) -and ($pn1 -le $pn0 + 1)) ('node:' + $pn0 + '->' + $pn1)
    $o = (& $tC check 2>&1 | Out-String); TC '14 check live' ($o.Contains('已在运行') -or $o.Contains('running'))
    $d = Join-Path $T 'fullC'; NewDir $d; Copy-Item $tC (Join-Path $d 't.exe')
    [IO.File]::WriteAllText((Join-Path $d 'launcher.config'), ('lang=zh' + [Environment]::NewLine + 'check_update=off' + [Environment]::NewLine))
    [IO.File]::WriteAllText((Join-Path $d '.dsh_launcher_root'), 'DeepSeek Harness Toolkit V2.1.4' + [Environment]::NewLine)
    $o = ("6`ny`ny`n$date`nyes`n0`n" | & (Join-Path $d 't.exe') 2>&1 | Out-String)
    TC '19 uninstall blocked while running' ($o.Contains('请先关闭') -or $o.Contains('Close the dsh web window first'))
} else {
    $o = (& $tC check 2>&1 | Out-String); TC '14 check stopped' ($o.Contains('未启动') -or $o.Contains('not started'))
    $results.Add('13 monitor(needs 3080)              SKIP')
    $results.Add('19 uninstall-block(needs 3080)      SKIP')
}

# 27/28 监控页 I 选项条件显示（变体 C 真实端口；DSH_TEST_DESKTOP 隔离，不碰真实桌面）
if ($portLive) {
    # 27：快捷方式不存在 -> 显示 I 行
    $desk27 = Join-Path $T 'desk27'; NewDir $desk27
    $env:DSH_TEST_DESKTOP = $desk27
    $o27 = ("2`n0`n" | & $tC 2>&1 | Out-String)
    Remove-Item Env:DSH_TEST_DESKTOP -ErrorAction SilentlyContinue
    TC '27 monitor shows I when no shortcut' (($o27.Contains('I)')) -and ($o27.Contains('打开 WebUI') -or $o27.Contains('Open Web UI')))
    # 28：快捷方式已存在 -> 隐藏 I 行
    $desk28 = Join-Path $T 'desk28'; NewDir $desk28
    $ws = New-Object -ComObject WScript.Shell
    $sc = $ws.CreateShortcut((Join-Path $desk28 'DeepSeek Harness Toolkit.lnk'))
    $sc.TargetPath = "$env:WINDIR\System32\notepad.exe"
    $sc.Save()
    $env:DSH_TEST_DESKTOP = $desk28
    $o28 = ("2`n0`n" | & $tC 2>&1 | Out-String)
    Remove-Item Env:DSH_TEST_DESKTOP -ErrorAction SilentlyContinue
    TC '28 monitor hides I when shortcut exists' (($o28.Contains('打开 WebUI') -or $o28.Contains('Open Web UI')) -and (-not $o28.Contains('I)')))
} else {
    $results.Add('27/28 monitor-I(needs 3080)        SKIP')
}

# 15-18 备份/恢复/导入/嵌套
$d = Join-Path $T 'bk'; NewDir $d; Copy-Item $tA (Join-Path $d 't.exe'); Seed-DT
$wsdir = Join-Path $T 'ws_data'; NewDir $wsdir; [IO.File]::WriteAllText((Join-Path $wsdir 'report.txt'), 'ws placeholder')
# M-7：$T 位于 UserProfile 子树，命中工作区黑名单 → 需输入 yes 显式确认后仍可备份（顺带覆盖 M-7 二次确认路径）
$o = ("5`n1`n$wsdir`nyes`n`n0`n" | & (Join-Path $d 't.exe') 2>&1 | Out-String)
$bkdir = Join-Path $d 'backup'
$B = Get-ChildItem -LiteralPath $bkdir -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like 'dsh-data-*' } | Select-Object -First 1
$ok15 = $false
if ($B) { $ok15 = (Test-Path -LiteralPath (Join-Path $B.FullName '_workspace')) -and (Test-Path -LiteralPath (Join-Path $B.FullName 'settings.yaml')) }
TC '15 backup e2e' $ok15
$wr = Join-Path $T 'restore_ws'; NewDir $wr
$o = ("5`n2`n1`ny`n$wr`ny`n0`n" | & (Join-Path $d 't.exe') 2>&1 | Out-String)
# 注意：恢复前预备份与主备份可能落在同一秒（dest 同名合并），故不按"备份数+1"断言，改用文案+文件
TC '16 restore e2e (+pre-backup)' (($o.Contains('恢复前自动备份当前数据') -or $o.Contains('Auto-backing up current data before restore')) -and ($o.Contains('恢复完成') -or $o.Contains('Restore done')) -and (Test-Path (Join-Path $wr 'report.txt')) -and (Test-Path (Join-Path $dt 'settings.yaml')))
$foreign = Join-Path $T 'foreign'; NewDir $foreign
if ($B) { Copy-Item -LiteralPath $B.FullName -Destination (Join-Path $foreign $B.Name) -Recurse -Force }
$o = ("5`n3`n$foreign`ny`n0`n" | & (Join-Path $d 't.exe') 2>&1 | Out-String)
$bk2 = @(Get-ChildItem -LiteralPath $bkdir -Directory -ErrorAction SilentlyContinue).Count
TC '17 import e2e' (($o.Contains('导入前自动备份当前数据') -or $o.Contains('Auto-backing up current data before import')) -and ($o.Contains('恢复完成') -or $o.Contains('Restore done')) -and ($bk2 -ge 1))
$wsdir2 = Join-Path $T 'ws_nested'; NewDir $wsdir2; NewDir (Join-Path $wsdir2 'dsh-data-20250101-000000')
[IO.File]::WriteAllText((Join-Path $wsdir2 'dsh-data-20250101-000000\x.txt'), 'fake')
# M-7：同样位于 UserProfile 子树，需 yes 确认后才真正进入备份，验证 CopyTree 跳过嵌套 dsh-data-*
$o = ("5`n1`n$wsdir2`nyes`n`n0`n" | & (Join-Path $d 't.exe') 2>&1 | Out-String)
$dummy = @(Get-ChildItem -LiteralPath $bkdir -Recurse -Filter 'x.txt' -ErrorAction SilentlyContinue).Count
TC '18 nested backup skipped' ((($o -join ' ').Contains('跳过') -or ($o -join ' ').Contains('skipping') -or ($o -join ' ').Contains('skipped')) -and ($dummy -eq 0))

# 20 保留策略（v2.1）：只清自动类（超 keep_backups 删最旧），手动备份永久保留
$d20 = Join-Path $T 'ret'; NewDir $d20; Copy-Item $tA (Join-Path $d20 't.exe')
[IO.File]::WriteAllText((Join-Path $d20 'launcher.config'), ('lang=zh' + [Environment]::NewLine + 'keep_backups=4' + [Environment]::NewLine))
$bk20 = Join-Path $d20 'backup'; NewDir $bk20
foreach ($n in @('01', '02')) { NewDir (Join-Path $bk20 ('dsh-data-20260101-0000' + $n)) }               # 手动 2 份（无后缀）
foreach ($n in @('01', '02', '03', '04', '05')) { NewDir (Join-Path $bk20 ('dsh-data-20260101-000' + $n + '-pre-restore')) }   # 自动类 5 份
$o = ("5`n1`n`n0`n" | & (Join-Path $d20 't.exe') 2>&1 | Out-String)   # 手动备份触发保留策略
$preCnt = @(Get-ChildItem -LiteralPath $bk20 -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like '*pre-restore' }).Count
$man1 = Test-Path (Join-Path $bk20 'dsh-data-20260101-000001')
$man2 = Test-Path (Join-Path $bk20 'dsh-data-20260101-000002')
$oldestGone = -not (Test-Path (Join-Path $bk20 'dsh-data-20260101-000001-pre-restore'))
$retMsg = (($o -join ' ').Contains('保留策略') -or ($o -join ' ').Contains('Retention'))
TC '20 retention auto-only' (($preCnt -eq 4) -and $man1 -and $man2 -and $oldestGone -and $retMsg)

# 21/22 运行中拒绝（v2.1 🔴2：Restore/Import 在 dsh 运行中禁止；变体 C 真实端口）
if ($portLive) {
    $d21 = Join-Path $T 'fullC21'; NewDir $d21; Copy-Item $tC (Join-Path $d21 't.exe')
    [IO.File]::WriteAllText((Join-Path $d21 'launcher.config'), ('lang=zh' + [Environment]::NewLine + 'check_update=off' + [Environment]::NewLine))
    NewDir (Join-Path $d21 'backup\dsh-data-20260101-000001')
    [IO.File]::WriteAllText((Join-Path $d21 'backup\dsh-data-20260101-000001\settings.yaml'), 'x')
    $o21 = ("5`n2`n1`ny`n0`n" | & (Join-Path $d21 't.exe') 2>&1 | Out-String)
    TC '21 restore blocked while running' ($o21.Contains('请先关闭') -or $o21.Contains('Close the dsh web window first'))
    $foreign21 = Join-Path $T 'foreign21'; NewDir $foreign21
    if ($B) { Copy-Item -LiteralPath $B.FullName -Destination (Join-Path $foreign21 $B.Name) -Recurse -Force }
    $o22 = ("5`n3`n$foreign21`ny`n0`n" | & (Join-Path $d21 't.exe') 2>&1 | Out-String)
    TC '22 import blocked while running' ($o22.Contains('请先关闭') -or $o22.Contains('Close the dsh web window first'))
} else {
    $results.Add('21 restore-block(needs 3080)        SKIP')
    $results.Add('22 import-block(needs 3080)         SKIP')
}

# 25 更新运行中拒绝（UpdateDsh 守卫，变体 C 真实端口，CLI update）
if ($portLive) {
    $d25 = Join-Path $T 'updC'; NewDir $d25; Copy-Item $tC (Join-Path $d25 't.exe')
    [IO.File]::WriteAllText((Join-Path $d25 'launcher.config'), ('lang=zh' + [Environment]::NewLine + 'check_update=off' + [Environment]::NewLine))
    $o25 = ("" | & (Join-Path $d25 't.exe') update 2>&1 | Out-String)
    TC '25 update blocked while running' ($o25.Contains('正在运行') -or $o25.Contains('is running'))
} else {
    $results.Add('25 update-block(needs 3080)        SKIP')
}

# 26 桌面快捷方式 CLI（DSH_TEST_DESKTOP 隔离到临时目录，不碰真实桌面）
$d26 = Join-Path $T 'short'; NewDir $d26; Copy-Item $tA (Join-Path $d26 't.exe')
$desk26 = Join-Path $T 'desktop'; NewDir $desk26
$env:DSH_TEST_DESKTOP = $desk26
$o26 = (& (Join-Path $d26 't.exe') shortcut 2>&1 | Out-String)
Remove-Item Env:DSH_TEST_DESKTOP -ErrorAction SilentlyContinue
$lnk26 = Test-Path (Join-Path $desk26 'DeepSeek Harness Toolkit.lnk')
TC '26 shortcut CLI' (($o26.Contains('SHORTCUT_OK')) -and $lnk26)

Write-Output ""; Write-Output "=== integration results ==="
$results | ForEach-Object { Write-Output $_ }
$fail = ($results | Where-Object { $_ -match ' FAIL ' }).Count
$pass = ($results | Where-Object { $_ -match ' PASS ' }).Count
$skip = @($results | Where-Object { $_ -match ' SKIP ' }).Count
Write-Output ("PASS " + $pass + "  FAIL " + $fail + "  SKIP " + $skip)
Remove-Item -LiteralPath $T -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $dt -Recurse -Force -ErrorAction SilentlyContinue
exit $(if ($fail -eq 0) { 0 } else { 1 })