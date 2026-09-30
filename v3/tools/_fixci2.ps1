$ErrorActionPreference = 'Continue'
$u8 = New-Object System.Text.UTF8Encoding($false)
$repo = $args[0]
$wf = Join-Path $repo '.github\workflows\build-release.yml'

function FixAll($path, $a, $b, $label) {
    $enc = New-Object System.Text.UTF8Encoding($false)
    $raw = [System.IO.File]::ReadAllText($path, $enc)
    $crlf = $raw.Contains("`r`n")
    $t = $raw.Replace("`r`n", "`n")
    $n = ([regex]::Matches($t, [regex]::Escape($a))).Count
    if ($n -ge 1) {
        $t = $t.Replace($a, $b.Replace("`r`n", "`n"))
        if ($crlf) { $t = $t.Replace("`n", "`r`n") }
        [System.IO.File]::WriteAllText($path, $t, $enc)
        Write-Host ("  OK   " + $label + " (" + $n + ")")
    } else {
        Write-Host ("  MISS " + $label)
    }
}

Write-Host "=== m-2b (cont): the setup name must use the normalised version too ==="
$a = '          $setup = "dsh-minato-${{ github.ref_name }}-win-x64-setup.exe"' + "`n" + '          if (-not (Test-Path $setup)) { throw "安装器没有生成 ✗" }'
$b = '          $setup = "dsh-minato-$vtag-win-x64-setup.exe"' + "`n" + '          if (-not (Test-Path $setup)) { throw "安装器没有生成 ✗" }'
FixAll $wf $a $b 'm-2b setup name'

Write-Host "=== m-2c MAJOR: add a real install/uninstall smoke test ==="
$a2 = '          if (-not (Test-Path $setup)) { throw "安装器没有生成 ✗" }' + "`n" + '          $h = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLower()'
$b2 = '          if (-not (Test-Path $setup)) { throw "安装器没有生成 ✗" }' + "`n" +
      '          # m-2c FIX (CI audit MAJOR): this step only checked that a file existed and hashed it,' + "`n" +
      '          # which is why the bug where the uninstaller deleted NOTHING reached users - nothing' + "`n" +
      '          # here ever ran the installer. Install silently into a scratch folder, confirm the' + "`n" +
      '          # payload and the marker landed, then uninstall and confirm our own files are gone.' + "`n" +
      '          $smoke = Join-Path $env:TEMP ("dsht-smoke-" + [guid]::NewGuid().ToString("N").Substring(0,8))' + "`n" +
      '          Write-Output "smoke: installing into $smoke"' + "`n" +
      '          & $setup --silent "--dir=$smoke"' + "`n" +
      '          if ($LASTEXITCODE -ne 0) { throw "smoke: the installer exited $LASTEXITCODE" }' + "`n" +
      '          if (-not (Test-Path (Join-Path $smoke "dsh-minato.exe"))) { throw "smoke: dsh-minato.exe is missing" }' + "`n" +
      '          if (-not (Test-Path (Join-Path $smoke ".dsh-minato-install"))) { throw "smoke: the install marker is missing" }' + "`n" +
      '          $smokeUninst = Join-Path $smoke "uninstall.exe"' + "`n" +
      '          if (-not (Test-Path $smokeUninst)) { throw "smoke: uninstall.exe is missing" }' + "`n" +
      '          & $smokeUninst --uninstall --silent' + "`n" +
      '          $rc = $LASTEXITCODE' + "`n" +
      '          Write-Output "smoke: the uninstaller exited $rc"' + "`n" +
      '          if (Test-Path (Join-Path $smoke "dsh-minato.exe")) { throw "smoke: the uninstaller left dsh-minato.exe behind - this is the exact bug that shipped once" }' + "`n" +
      '          if (Test-Path (Join-Path $smoke "gui")) { throw "smoke: the uninstaller left the gui folder behind" }' + "`n" +
      '          Write-Output "smoke: OK - install and uninstall both did what they should"' + "`n" +
      '          Remove-Item $smoke -Recurse -Force -ErrorAction SilentlyContinue' + "`n" +
      '          $h = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLower()'
FixAll $wf $a2 $b2 'm-2c smoke test'

Write-Host "=== verify ==="
$l = Get-Content $wf -Encoding UTF8
$glued = 0
foreach ($ln in $l) { if ($ln -match '\}\s+- name:') { $glued++ } }
Write-Host ("  glued step headers: " + $glued + $(if ($glued -eq 0) { "  OK" } else { "  BAD" }))
$usesRef = @($l | Where-Object { $_ -match 'github\.ref_name' }).Count
Write-Host ("  remaining github.ref_name uses: " + $usesRef)
foreach ($k in 0..($l.Count-1)) { if ($l[$k] -match 'github\.ref_name|smoke:|vtag') { Write-Host ("    " + ($k+1) + "|" + $l[$k].Trim().Substring(0,[Math]::Min(120,$l[$k].Trim().Length))) } }
Write-Host "=== done ==="
