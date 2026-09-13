<#
.SYNOPSIS
  DeepSeek Harness Toolkit - release artifact one-click verification.

.DESCRIPTION
  Downloads the core + GUI executables and hashes.txt of a release, then:

    1) SHA-256 check of every downloaded artifact against CI-generated hashes.txt
    2) GPG signature verification of hashes.txt (hashes.txt.asc, maintainer's key) if GPG is available
    3) Prints the CI build / release provenance links

  Read-only: downloads to -OutDir, imports NOTHING into the system, installs nothing.

  Public key (maintainer): keys/sakanamaru-gpg.asc in the repository.
  Fingerprint: A2F67D170B5BE4845612642C240979232B4E4CE4

.PARAMETER Tag
  Release tag (e.g. v2.4.0-gui-alpha). Default: newest release (including prereleases).

.PARAMETER OutDir
  Download destination. Default: current directory.

.PARAMETER GpgPath
  Path to gpg.exe. Default: auto-detect (PATH, then Git-for-Windows build).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File verify.ps1 -OutDir D:\verify
  powershell -ExecutionPolicy Bypass -File verify.ps1 -Tag v2.4.0-gui-alpha
#>
param(
  [string]$Tag = "",
  [string]$OutDir = ".",
  [string]$GpgPath = ""
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Owner = "sakanamaru"
$Repo  = "DeepSeek-Harness-Toolkit"
$Api   = "https://api.github.com/repos/$Owner/$Repo"

function Write-Step([string]$msg) { Write-Host ("`n== " + $msg) -ForegroundColor Cyan }

function Get-GpgPath {
  if ($GpgPath -and (Test-Path $GpgPath)) { return $GpgPath }
  $cmd = Get-Command gpg -ErrorAction SilentlyContinue
  if ($cmd) { return $cmd.Source }
  $gitGpg = "C:\Program Files\Git\usr\bin\gpg.exe"
  if (Test-Path $gitGpg) { return $gitGpg }
  return ""
}

function Invoke-GpgCheck([string]$sigFile, [string]$dataFile) {
  # Git-for-Windows gpg needs the MSYS environment to find keyboxd.
  $bash = "C:\Program Files\Git\bin\bash.exe"
  if (Test-Path $bash) {
    $sigU = $sigFile -replace '\\', '/' -replace '^([A-Za-z]):', '/$1'
    $datU = $dataFile -replace '\\', '/' -replace '^([A-Za-z]):', '/$1'
    & $bash -lc ("gpg --verify '" + $sigU + "' '" + $datU + "' 2>&1")
    return $LASTEXITCODE
  }
  $gpg = Get-GpgPath
  if (-not $gpg) { return -1 }
  & $gpg --verify $sigFile $dataFile 2>&1
  return $LASTEXITCODE
}

# ---- resolve release ----
Write-Step "Resolving release"
if ($Tag) {
  $rel = Invoke-RestMethod -Uri "$Api/releases/tags/$Tag" -Headers @{ "User-Agent" = "verify.ps1" }
} else {
  $list = Invoke-RestMethod -Uri "$Api/releases" -Headers @{ "User-Agent" = "verify.ps1" }
  $rel = $list[0]
}
Write-Host ("release : " + $rel.name + "  (" + $rel.tag_name + ")")
Write-Host ("url     : " + $rel.html_url)
if ($rel.prerelease) { Write-Host "note    : prerelease (Alpha/Beta)" -ForegroundColor Yellow }

# ---- download hashes.txt + executables ----
Write-Step "Downloading artifacts to $OutDir"
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$wantNames = @("hashes.txt", "DeepSeek.Harness.Toolkit.exe", "Toolkit.GUI.exe", "hashes.txt.asc")
$saved = @{}
foreach ($a in $rel.assets) {
  if ($wantNames -contains $a.name) {
    $dest = Join-Path $OutDir $a.name
    Write-Host ("downloading " + $a.name + " (" + [math]::Round($a.size / 1KB, 1) + " KB)")
    Invoke-WebRequest -Uri $a.browser_download_url -OutFile $dest -Headers @{ "User-Agent" = "verify.ps1" }
    $saved[$a.name] = $dest
  }
}

# ---- SHA-256 check ----
Write-Step "SHA-256 verification against CI hashes.txt"
if (-not $saved.ContainsKey("hashes.txt")) { Write-Host "FATAL: hashes.txt not found in release assets" -ForegroundColor Red; exit 1 }
$hashLines = Get-Content $saved["hashes.txt"]
$hashMap = @{}
foreach ($ln in $hashLines) {
  if ($ln -match '^\s*([0-9a-fA-F]{64})\s+(\S.*)$') { $hashMap[$matches[2]] = $matches[1].ToLowerInvariant() }
}

# asset file name (GitHub mangles spaces to dots) -> hashes.txt entry name
$map = @{
  "DeepSeek.Harness.Toolkit.exe" = "DeepSeek Harness Toolkit.exe"
  "Toolkit.GUI.exe"               = "Toolkit GUI.exe"
}
$pass = 0; $fail = 0
foreach ($k in @("DeepSeek.Harness.Toolkit.exe", "Toolkit.GUI.exe")) {
  if (-not $saved.ContainsKey($k)) { Write-Host ("SKIP  " + $k + " (not in release assets)") -ForegroundColor DarkGray; continue }
  $entryName = $map[$k]
  if (-not $hashMap.ContainsKey($entryName)) { Write-Host ("FAIL  " + $k + " : no entry in hashes.txt") -ForegroundColor Red; $fail++; continue }
  $h = (Get-FileHash $saved[$k] -Algorithm SHA256).Hash.ToLowerInvariant()
  if ($h -eq $hashMap[$entryName]) { Write-Host ("PASS  " + $k) -ForegroundColor Green; $pass++ }
  else { Write-Host ("FAIL  " + $k + " : hash mismatch (downloaded " + $h + " vs " + $hashMap[$entryName] + ")") -ForegroundColor Red; $fail++ }
}
Write-Host ("--- " + $pass + " passed, " + $fail + " failed (SHA-256)")

# ---- GPG signature check ----
if ($fail -eq 0 -and $saved.ContainsKey("hashes.txt.asc")) {
  Write-Step "GPG signature verification of hashes.txt"
  $rc = Invoke-GpgCheck $saved["hashes.txt.asc"] $saved["hashes.txt"]
  if ($rc -eq 0) {
    Write-Host "PASS  hashes.txt.asc : Good signature from the maintainer" -ForegroundColor Green
  } else {
    Write-Host "FAIL  hashes.txt.asc : signature check returned $rc" -ForegroundColor Red
    $fail++
  }
} else {
  Write-Host "GPG signature check skipped (hashes.txt.asc missing or SHA-256 already failed; get gpg + the public key from keys/sakanamaru-gpg.asc to verify manually)" -ForegroundColor DarkGray
}

# ---- provenance links ----
Write-Step "Provenance"
Write-Host ("release  : " + $rel.html_url)
Write-Host ("CI builds: https://github.com/$Owner/$Repo/actions")
Write-Host ("source   : https://github.com/$Owner/$Repo (main@" + $rel.target_commitish + ")")
Write-Host ("pubkey   : keys/sakanamaru-gpg.asc  fingerprint A2F67D170B5BE4845612642C240979232B4E4CE4")

Write-Host ""
if ($fail -eq 0) {
  Write-Host "RESULT: ALL CHECKS PASSED" -ForegroundColor Green
  exit 0
} else {
  Write-Host "RESULT: $fail CHECK(S) FAILED - do not run this release" -ForegroundColor Red
  exit 1
}