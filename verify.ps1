<#
.SYNOPSIS
  DeepSeek Harness Toolkit - release artifact one-click verification.

.DESCRIPTION
  Downloads the core + GUI executables and hashes.txt of a release, then:

    1) SHA-256 check of every downloaded artifact against CI-generated hashes.txt
    2) GPG signature verification of hashes.txt (hashes.txt.asc) against the PINNED maintainer
       fingerprint A2F67D170B5BE4845612642C240979232B4E4CE4, using a temporary isolated keyring
       (the local keyring is never trusted)
    3) Release -> Tag -> Commit provenance chain (tag object, commit, commit URL)
    4) Prints the CI build / release provenance links

  Read-only: downloads to -OutDir, imports NOTHING into the system, installs nothing.

  Public key (maintainer): keys/sakanamaru-gpg.asc in the repository.
  Fingerprint: A2F67D170B5BE4845612642C240979232B4E4CE4

.PARAMETER Tag
  Release tag (e.g. v2.4.2). Default: newest release (including prereleases).

.PARAMETER OutDir
  Download destination. Default: current directory.

.PARAMETER GpgPath
  Path to gpg.exe. Default: auto-detect (PATH, then Git-for-Windows build).

.PARAMETER Token
  Optional GitHub token; only needed when no -Tag is given (newest-release mode).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File verify.ps1 -OutDir D:\verify
  powershell -ExecutionPolicy Bypass -File verify.ps1 -Tag v2.4.2
#>
param(
  [string]$Tag = "",
  [string]$OutDir = ".",
  [string]$GpgPath = "",
  [string]$Token = ""
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
# 维护者 GPG 主密钥指纹：唯一可信锚点。签名"看起来是好签名"不算通过，必须由这把钥匙签出。
$PinnedFingerprint = "A2F67D170B5BE4845612642C240979232B4E4CE4"
# 脚本所在目录（用于找仓库内随包的公钥 keys/sakanamaru-gpg.asc；不存在时再按 tag 下载）
$ScriptDir = ""
try { if ($PSCommandPath) { $ScriptDir = Split-Path -Parent $PSCommandPath } } catch { }
# PS 5.1 解码子进程（bash/gpg）UTF-8 输出可能挂起 - 统一编码到 UTF-8
try {
  $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
  [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
} catch { }

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

function Invoke-GpgCheck([string]$sigFile, [string]$dataFile, [string]$pubKeyFile) {
  # 不再信任本机钥匙串（旧实现只看 gpg 退出码：钥匙串里"任意"一把钥匙签得好也会 PASS）。
  # 改为：临时 GNUPGHOME → 只导入随包/按 tag 取回的维护者公钥 → --status-fd 1 取 VALIDSIG 指纹
  #       → 强制等于 $PinnedFingerprint，否则一律 FAIL 并打印实际指纹。
  # 坑①：Git-for-Windows 的 gpg 把 GNUPGHOME 当 MSYS 路径解释（直接给 C:\... 会报
  #       keyblock resource '/d/.../C:\.../pubring.kbx': No such file）→ 必须经 bash 且用 /c/... 形式。
  # 坑②：加 LC_ALL=C，避免本地化输出影响正则匹配。
  $bash = "C:\Program Files\Git\bin\bash.exe"
  if (-not (Test-Path $bash)) {
    return @{ Ok = $false; Rc = -1; Fingerprint = ""; Detail = "Git-for-Windows bash not found; cannot build an isolated keyring" }
  }
  if (-not (Test-Path $pubKeyFile)) {
    return @{ Ok = $false; Rc = -1; Fingerprint = ""; Detail = "public key file not available (" + $pubKeyFile + ")" }
  }
  $gpgHome = Join-Path ([System.IO.Path]::GetTempPath()) ("dsht_gpg_" + [Guid]::NewGuid().ToString("N"))
  New-Item -ItemType Directory -Path $gpgHome -Force | Out-Null
  try {
    $homeU = $gpgHome    -replace '\\', '/' -replace '^([A-Za-z]):', '/$1'
    $pubU  = $pubKeyFile -replace '\\', '/' -replace '^([A-Za-z]):', '/$1'
    $sigU  = $sigFile   -replace '\\', '/' -replace '^([A-Za-z]):', '/$1'
    $datU  = $dataFile  -replace '\\', '/' -replace '^([A-Za-z]):', '/$1'
    $cmd = "export LC_ALL=C; gpg --homedir '$homeU' --batch --no-tty --quiet --import '$pubU' >/dev/null 2>&1; " +
           "gpg --homedir '$homeU' --batch --no-tty --status-fd 1 --verify '$sigU' '$datU' 2>/dev/null"
    $out = & $bash -lc $cmd 2>&1
    $rc = $LASTEXITCODE
    $fp = ""
    foreach ($line in @($out)) {
      if ([string]$line -match '^\[GNUPG:\]\s+VALIDSIG\s+([0-9A-Fa-f]{40})') { $fp = $matches[1].ToUpperInvariant() }
    }
    $ok = ($rc -eq 0) -and ($fp -eq $PinnedFingerprint)
    $detail = ""
    if ($fp -eq "") { $detail = "no VALIDSIG line (unsigned, bad signature, or the key could not be used)" }
    elseif ($fp -ne $PinnedFingerprint) { $detail = "signed by " + $fp + ", but the pinned maintainer key is " + $PinnedFingerprint }
    return @{ Ok = $ok; Rc = $rc; Fingerprint = $fp; Detail = $detail }
  } finally {
    Remove-Item $gpgHome -Recurse -Force -ErrorAction SilentlyContinue
  }
}

# 维护者公钥来源：优先仓库内 keys/sakanamaru-gpg.asc；否则按 tag 从 raw 下载。
# 公钥来源不影响信任判断——指纹被钉死在脚本里，换一把钥匙只会得到不同的 VALIDSIG 而被拒。
function Get-MaintainerKey([string]$Tag, [string]$OutDir) {
  $local = ""
  if ($ScriptDir) { $local = Join-Path $ScriptDir "keys\sakanamaru-gpg.asc" }
  if ($local -and (Test-Path $local)) { return $local }
  if (-not $Tag) { return "" }
  $dest = Join-Path $OutDir "sakanamaru-gpg.asc"
  $url = "https://raw.githubusercontent.com/$Owner/$Repo/$Tag/keys/sakanamaru-gpg.asc"
  try {
    Invoke-WebRequest -Uri $url -OutFile $dest -Headers @{ "User-Agent" = "verify.ps1" } -UseBasicParsing -TimeoutSec 60
    return $dest
  } catch {
    return ""
  }
}

# ---- resolve release ----
# 有 -Tag 时完全不依赖 GitHub API（固定下载 URL 模板，走 CDN）。
# 匿名 API 会被限速；-Token 仅"无 Tag 取最新"时可选传入。
Write-Step "Resolving release"
$relUrl = ""
if ($Tag) {
  $relUrl = "https://github.com/$Owner/$Repo/releases/tag/$Tag"
  Write-Host ("release : tag " + $Tag)
  Write-Host ("url     : " + $relUrl)
} else {
  $apiHeaders = @{ "User-Agent" = "verify.ps1" }
  if ($Token) { $apiHeaders["Authorization"] = "Bearer " + $Token }
  try {
    $list = Invoke-RestMethod -Uri "$Api/releases" -Headers $apiHeaders -TimeoutSec 60
    $rel = $list[0]
    $Tag = [string]$rel.tag_name
    $relUrl = "https://github.com/$Owner/$Repo/releases/tag/$Tag"
    Write-Host ("release : " + $rel.name + "  (" + $rel.tag_name + ")")
    Write-Host ("url     : " + $rel.html_url)
    if ($rel.prerelease) { Write-Host "note    : prerelease (Alpha/Beta)" -ForegroundColor Yellow }
  } catch {
    Write-Host "FATAL: GitHub API 不可用（可能限速）。请改用 -Tag 参数——该路径直接走 release 下载链接、不依赖 API。" -ForegroundColor Red
    Write-Host ("  " + $_.Exception.Message) -ForegroundColor Red
    exit 1
  }
}

# ---- download hashes.txt + executables ----
Write-Step "Downloading artifacts to $OutDir"
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$wantNames = @("hashes.txt", "DeepSeek.Harness.Toolkit.exe", "Toolkit.GUI.exe", "Toolkit.GUI.Standalone.exe", "hashes.txt.asc")
$dlBase = "https://github.com/$Owner/$Repo/releases/download/$Tag"
$saved = @{}
foreach ($n in $wantNames) {
  $dest = Join-Path $OutDir $n
  # -UseBasicParsing: PS 5.1 在管道环境中 IWR 进度条渲染会把大文件下载拖到挂起
  # TimeoutSec + 双次重试：避免单次网络差错导致脚本整体卡死
  $ok = $false
  foreach ($try in 1..2) {
    try {
      Invoke-WebRequest -Uri "$dlBase/$n" -OutFile $dest -Headers @{ "User-Agent" = "verify.ps1" } -UseBasicParsing -TimeoutSec 120
      $ok = $true
      break
    } catch {
      Write-Host ("  retry " + $try + ": " + $_.Exception.Message) -ForegroundColor Yellow
      Start-Sleep -Seconds 2
    }
  }
  if (-not $ok) { Write-Host ("FAIL  download " + $n) -ForegroundColor Red; exit 1 }
  $saved[$n] = $dest
  $szKb = [math]::Round((Get-Item $dest).Length / 1024, 1)
  Write-Host ("downloaded " + $n + " (" + $szKb + " KB)")
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
  "Toolkit.GUI.Standalone.exe"    = "Toolkit GUI Standalone.exe"
}
$pass = 0; $fail = 0
foreach ($k in @("DeepSeek.Harness.Toolkit.exe", "Toolkit.GUI.exe", "Toolkit.GUI.Standalone.exe")) {
  if (-not $saved.ContainsKey($k)) { Write-Host ("SKIP  " + $k + " (not in release assets)") -ForegroundColor DarkGray; continue }
  $entryName = $map[$k]
  if (-not $hashMap.ContainsKey($entryName)) { Write-Host ("FAIL  " + $k + " : no entry in hashes.txt") -ForegroundColor Red; $fail++; continue }
  $h = (Get-FileHash $saved[$k] -Algorithm SHA256).Hash.ToLowerInvariant()
  if ($h -eq $hashMap[$entryName]) { Write-Host ("PASS  " + $k) -ForegroundColor Green; $pass++ }
  else { Write-Host ("FAIL  " + $k + " : hash mismatch (downloaded " + $h + " vs " + $hashMap[$entryName] + ")") -ForegroundColor Red; $fail++ }
}
Write-Host ("--- " + $pass + " passed, " + $fail + " failed (SHA-256)")

# ---- GPG signature check（pin 指纹 + 隔离钥匙串）----
if ($fail -eq 0 -and $saved.ContainsKey("hashes.txt.asc")) {
  Write-Step "GPG signature verification of hashes.txt"
  $pubKey = Get-MaintainerKey $Tag $OutDir
  if (-not $pubKey) {
    Write-Host "FAIL  hashes.txt.asc : maintainer public key unavailable (put keys/sakanamaru-gpg.asc next to this script, or run with -Tag)" -ForegroundColor Red
    $fail++
  } else {
    $g = Invoke-GpgCheck $saved["hashes.txt.asc"] $saved["hashes.txt"] $pubKey
    if ($g.Ok) {
      Write-Host ("PASS  hashes.txt.asc : signed by the pinned maintainer key " + $PinnedFingerprint) -ForegroundColor Green
    } else {
      Write-Host ("FAIL  hashes.txt.asc : " + $g.Detail + "  (gpg rc=" + $g.Rc + ")") -ForegroundColor Red
      $fail++
    }
  }
} else {
  Write-Host "GPG signature check skipped (hashes.txt.asc missing or SHA-256 already failed; get gpg + the public key from keys/sakanamaru-gpg.asc to verify manually)" -ForegroundColor DarkGray
}

# ---- Release -> Tag -> Commit 链（匿名 API；失败不致命，但会明确说"取不到"）----
Write-Step "Release -> Tag -> Commit chain"
if ($Tag) {
  $chainHeaders = @{ "User-Agent" = "verify.ps1" }
  if ($Token) { $chainHeaders["Authorization"] = "Bearer " + $Token }
  try {
    $ref = Invoke-RestMethod -Uri "$Api/git/ref/tags/$Tag" -Headers $chainHeaders -TimeoutSec 30
    $objSha = [string]$ref.object.sha
    $objType = [string]$ref.object.type
    Write-Host ("tag object : " + $objSha + "  (" + $objType + ")")
    if ($objType -eq "tag") {
      $tagObj = Invoke-RestMethod -Uri "$Api/git/tags/$objSha" -Headers $chainHeaders -TimeoutSec 30
      $commitSha = [string]$tagObj.object.sha
      $firstLine = ([string]$tagObj.message -split [char]10)[0]
      Write-Host ("commit     : " + $commitSha)
      Write-Host ("commit url : https://github.com/$Owner/$Repo/commit/" + $commitSha)
      Write-Host ("tag msg    : " + $firstLine)
    } else {
      Write-Host ("commit     : " + $objSha + "  (lightweight tag: points straight at the commit)")
    }
  } catch {
    Write-Host ("chain      : unavailable (" + $_.Exception.Message + ") - not fatal; check the release page manually") -ForegroundColor DarkGray
  }
} else {
  Write-Host "chain      : skipped (no tag resolved)" -ForegroundColor DarkGray
}

# ---- provenance links ----
Write-Step "Provenance"
Write-Host ("release  : " + $relUrl)
Write-Host ("CI builds: https://github.com/$Owner/$Repo/actions")
Write-Host ("source   : https://github.com/$Owner/$Repo")
Write-Host ("pubkey   : keys/sakanamaru-gpg.asc  fingerprint A2F67D170B5BE4845612642C240979232B4E4CE4 (pinned in this script)")

Write-Host ""
if ($fail -eq 0) {
  Write-Host "RESULT: ALL CHECKS PASSED" -ForegroundColor Green
  exit 0
} else {
  Write-Host "RESULT: $fail CHECK(S) FAILED - do not run this release" -ForegroundColor Red
  exit 1
}