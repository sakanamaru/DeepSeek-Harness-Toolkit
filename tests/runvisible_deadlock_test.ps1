# RunVisible pipe deadlock regression test
# Simulates npm-like large output (>pipe buffer) to confirm the fixed
# drain-and-forward logic never deadlocks. Mirrors dsh_v2.cs RunVisible.
$ErrorActionPreference = 'Stop'
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

$testSrc = @'
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

class T {
    static void Main(string[] args) {
        // args[1] = result file path (bypass console buffering for reliable observation)
        string resultFile = args[1];
        var psi = new ProcessStartInfo("cmd.exe", args[0]) { UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true };
        using (var p = Process.Start(psi)) {
            Drain(p.StandardOutput, Console.Out);
            Drain(p.StandardError, Console.Error);
            bool exited = p.WaitForExit(10 * 60 * 1000);
            if (!exited) { try { p.Kill(); } catch { } p.WaitForExit(); File.WriteAllText(resultFile, "TIMEOUT"); return; }
            File.WriteAllText(resultFile, "EXIT " + p.ExitCode);
        }
    }
    static void Drain(System.IO.StreamReader src, System.IO.TextWriter dst) {
        ThreadPool.QueueUserWorkItem(_ => { try { string l; while ((l = src.ReadLine()) != null) { try { if (dst != null) dst.WriteLine(l); } catch { } } } catch { } });
    }
}
'@
$tf = Join-Path $env:TEMP "rvtest.cs"
$te = Join-Path $env:TEMP "rvtest.exe"
$rc = Join-Path $env:TEMP "rv_result.txt"
Remove-Item $rc -Force -ErrorAction SilentlyContinue
Set-Content -Path $tf -Value $testSrc -Encoding ASCII
& $csc /nologo /optimize+ "/out:$te" $tf 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "rvtest compile failed" }

function Test-Case($name, $cmdline) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $tin = Join-Path $env:TEMP ("rv_in_" + [guid]::NewGuid().ToString("N") + ".txt")
    Remove-Item $tin -Force -ErrorAction SilentlyContinue
    $p = Start-Process -FilePath $te -ArgumentList "`"$cmdline`" `"$tin`"" -PassThru
    $done = $p.WaitForExit(30000)
    $sw.Stop()
    if (-not $done) { $p.Kill(); Write-Host "$name : FAIL (hung, 30s no exit)" -ForegroundColor Red; Remove-Item $tin -Force -ErrorAction SilentlyContinue; return 1 }
    $last = Get-Content $tin -ErrorAction SilentlyContinue | Select-Object -Last 1
    $ok = $last -match "EXIT 0"
    if (-not $ok) { Write-Host "$name : FAIL (bad result, got: $last)" -ForegroundColor Red; Remove-Item $tin -Force -ErrorAction SilentlyContinue; return 1 }
    Write-Host "$name : PASS ($($sw.ElapsedMilliseconds)ms)" -ForegroundColor Green
    Remove-Item $tin -Force -ErrorAction SilentlyContinue
    return 0
}

$f = 0
$f += Test-Case "stdout 1MB drain" '/c for /l %i in (1,1,20000) do echo line %i'
$f += Test-Case "stderr 1MB drain" '/c for /l %i in (1,1,20000) do (echo err %i 1>&2 & echo o %i)'
$f += Test-Case "small output ok" '/c echo hello'

Remove-Item $tf, $te, $rc -Force -ErrorAction SilentlyContinue
if ($f -eq 0) { Write-Host "== deadlock regression: 3/3 PASS ==" -ForegroundColor Green; exit 0 }
Write-Host "== deadlock regression: $f FAILED ==" -ForegroundColor Red; exit 1