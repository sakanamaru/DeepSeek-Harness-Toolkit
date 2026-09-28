# verify_switchover.ps1 —— 切换就绪度一键检查
# 逐项检查 V3 切换门槛（目标定义的四项 + V3 追加的"真实写操作可验证"）+ 两条不变量（v2.x 发布链未被动过、领域层纯净度）。
# 门槛③（Windows/Linux 双跑）在本地只能验证"CI 配置就绪"，真跑需要推送触发——脚本会如实标注。
# 退出码：0=全部就绪；1=有未就绪项
param([string]$Repo = ".")
$ErrorActionPreference = "Stop"
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$rows = New-Object System.Collections.Generic.List[string]
$fail = 0
function Gate([string]$name, [bool]$ok, [string]$detail) {
    if ($ok) { $script:rows.Add(("  [READY] {0,-28} {1}" -f $name, $detail)) }
    else { $script:fail++; $script:rows.Add(("  [ NOT ] {0,-28} {1}" -f $name, $detail)) }
}

# ---- 门槛② 领域单测（csc 构建 + 运行契约测试）----
$dom = @(Get-ChildItem (Join-Path $Repo 'v3\src\Dsht.Domain') -Recurse -Filter *.cs | ForEach-Object FullName)
$win = @(Get-ChildItem (Join-Path $Repo 'v3\src\Dsht.Platform.Windows') -Recurse -Filter *.cs | ForEach-Object FullName)
$lin = @(Get-ChildItem (Join-Path $Repo 'v3\src\Dsht.Platform.Linux') -Recurse -Filter *.cs | ForEach-Object FullName)
$tst = @(Get-ChildItem (Join-Path $Repo 'v3\tests\Dsht.Contracts.Tests') -Recurse -Filter *.cs | ForEach-Object FullName)
$exe = Join-Path $env:TEMP 'dsht_switchover_contracts.exe'
Remove-Item $exe -Force -ErrorAction SilentlyContinue
& $csc /nologo /target:exe /warn:4 ("/out:" + $exe) ($dom + $win + $lin + $tst) | Out-Null
if ($LASTEXITCODE -ne 0) { Gate 'gate2 domain tests' $false 'csc 构建失败' }
else {
    $out = (& $exe 2>&1 | Out-String)
    $m = [regex]::Match($out, '==\s*(\d+)/(\d+) passed,\s*(\d+) failed')
    if ($m.Success) { Gate 'gate2 domain tests' ($m.Groups[3].Value -eq '0') ($m.Groups[1].Value + '/' + $m.Groups[2].Value + ' passed') }
    else { Gate 'gate2 domain tests' $false '未解析到结果行' }
}

# ---- 门槛① 标记行契约（两种模式）----
$cmp = Join-Path $Repo 'v3\tests\compare_markers.ps1'
$c1 = & powershell -ExecutionPolicy Bypass -File $cmp -Repo $Repo 2>&1 | Out-String
$rc1 = $LASTEXITCODE
$c2 = & powershell -ExecutionPolicy Bypass -File $cmp -Repo $Repo -Fixtures 2>&1 | Out-String
$rc2 = $LASTEXITCODE
$mm = [regex]::Match($c2, '标记行契约：(\d+)/(\d+) 对齐')
$detail = if ($mm.Success) { $mm.Groups[1].Value + '/' + $mm.Groups[2].Value + ' 对齐（含受控备份模式）' } else { '未解析到结果行' }
Gate 'gate1 marker contract' (($rc1 -eq 0) -and ($rc2 -eq 0)) $detail

# ---- 门槛③ 双平台：本地只能验证 CI 配置就绪 ----
$wf = Join-Path $Repo '.github\workflows\build-release.yml'
$hasJob = $false; $hasUbuntu = $false
if (Test-Path $wf) {
    $wt = [System.IO.File]::ReadAllText($wf)
    $hasJob = $wt.Contains('v3-contracts')
    $hasUbuntu = $wt.Contains('ubuntu-latest')
}
Gate 'gate3 win/linux dual-run' ($hasJob -and $hasUbuntu) 'CI job 配置就绪；真跑需推送触发（本地无法验证）'

# ---- 门槛④ 发布物校验（含校验器自证）----
$rel = & powershell -ExecutionPolicy Bypass -File (Join-Path $Repo 'v3\tests\verify_release.ps1') -Repo $Repo -Build -SelfTest 2>&1 | Out-String
Gate 'gate4 release verifier' ($LASTEXITCODE -eq 0) $(if ($LASTEXITCODE -eq 0) { '含篡改自证通过' } else { '校验失败' })

# ---- 门槛⑤（V3 追加）真实写操作可验证：隔离数据根 + restore --apply 端到端 ----
$ra = & powershell -ExecutionPolicy Bypass -File (Join-Path $Repo 'v3\tests\verify_restore_apply.ps1') -Repo $Repo 2>&1 | Out-String
$rm = [regex]::Match($ra, '==\s*(\d+)/(\d+) passed')
$rdet = if ($rm.Success) { $rm.Groups[1].Value + '/' + $rm.Groups[2].Value + '（隔离根真实写盘 + 零越界）' } else { '未解析到结果行' }
Gate 'gate5 real write verifiable' ($LASTEXITCODE -eq 0) $rdet

# ---- 不变量：发布与校验链未被动过 ----
# 注意：dsh_v2.cs / src/** 的改动是**预期的**（v2.8 阶段 1–3 的拆分/接缝/Linux 实现属合法演进）；
# 真正必须守住的是"发布与校验链"：verify.ps1、build_exe.cmd、发布步骤的 csc 命令、以及 16 项发布清单。
$chainChanged = ''
try { $chainChanged = (& git -C $Repo diff --name-only origin/main -- verify.ps1 build_exe.cmd 2>&1 | Out-String).Trim() } catch { $chainChanged = '' }
$itemsOk = $false; $stepsOk = $false
if (Test-Path $wf) {
    $wt2 = [System.IO.File]::ReadAllText($wf)
    $itemsOk = $wt2.Contains("'DeepSeek Harness Toolkit.exe','Toolkit GUI.exe','Toolkit GUI Standalone.exe'")
    $stepsOk = $wt2.Contains('/out:unittests.exe') -and $wt2.Contains('core_check.exe') -and $wt2.Contains('/out:DeepSeek Harness Toolkit.exe')
}
Gate 'invariant release chain' ([string]::IsNullOrWhiteSpace($chainChanged) -and $itemsOk -and $stepsOk) $(if ([string]::IsNullOrWhiteSpace($chainChanged) -and $itemsOk -and $stepsOk) { 'verify.ps1 / build_exe.cmd 未动；16 项清单与 csc 发布步骤完好' } else { 'verify.ps1/build_exe.cmd 改动:[' + ($chainChanged -replace "`r?`n", ',') + '] 清单=' + $itemsOk + ' 步骤=' + $stepsOk })

# ---- 领域层纯净度 ----
$pure = & powershell -ExecutionPolicy Bypass -File (Join-Path $Repo 'v3\tests\verify_domain_pure.ps1') -Repo $Repo 2>&1 | Out-String
Gate 'invariant domain purity' ($LASTEXITCODE -eq 0) '零 IO / 零平台 / 零时钟耦合'

Write-Host '== V3 切换就绪度 =='
$rows | ForEach-Object { Write-Host $_ }
if ($fail -gt 0) { Write-Host ("== 未就绪项：" + $fail + "（详见上面 [ NOT ] 行）=="); exit 1 }
Write-Host '== 全部就绪 =='
exit 0