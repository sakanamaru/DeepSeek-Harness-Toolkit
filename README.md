# DeepSeek Harness Toolkit

<div align="center">

**[English](README.md) · [简体中文](README_zh-CN.md)**

<img src="logo.png" alt="DeepSeek Harness Toolkit" width="220"/>

**Windows installer, monitor, backup & restore tool for the DeepSeek Harness (dsh) Web UI — double-click and go.**

</div>

A third-party, **unofficial** launcher / ops tool for [DeepSeek Harness](https://www.npmjs.com/package/@deepseek-ai/dsh) (dsh).
Install, start, monitor and uninstall the dsh Web UI, with data backup / restore built in. **No terminal required.**

> ⚠️ This project is **unofficial** and is not affiliated with DeepSeek.

## Official downloads

Only this repository's [Releases page](https://github.com/sakanamaru/DeepSeek-Harness-Toolkit/releases) ships official binaries — anything else (cloud-drive re-uploads, "paid / cracked / modified" editions, other websites or accounts) is **not official**. The project is free and open source (MIT); **no one is authorized to sell it**. Verify before running with `verify.ps1`; the trust model, supply-chain controls and manual verification steps live in [SECURITY.md](SECURITY.md).

## Screenshots

**GUI panel** (variants B / C):

| Home — status & actions (light) | Home (dark) |
|:---:|:---:|
| <img src="docs/screenshots/gui-home-light.png" width="440" alt="GUI home, light theme"/> | <img src="docs/screenshots/gui-home-dark.png" width="440" alt="GUI home, dark theme"/> |

| Backups — list, restore / export / delete | Update Center — read-only update picture |
|:---:|:---:|
| <img src="docs/screenshots/gui-backup-light.png" width="440" alt="GUI backups page"/> | <img src="docs/screenshots/gui-update-light.png" width="440" alt="GUI update page"/> |

| Settings — four config groups | Log Center — levels, filters, search, export |
|:---:|:---:|
| <img src="docs/screenshots/gui-settings-light.png" width="440" alt="GUI settings page"/> | <img src="docs/screenshots/gui-log-light.png" width="440" alt="GUI log page — structured log with level filters"/> |

| About — version, credits, unofficial notice |
|:---:|
| <img src="docs/screenshots/gui-about-light.png" width="440" alt="GUI about page"/> |

**CLI core** (variant A — the original form of this tool; the GUI is built on top of it):

| Interactive menu | Live status monitor (3-state detection) |
|:---:|:---:|
| <img src="docs/screenshots/cli-menu.png" width="440" alt="CLI interactive menu"/> | <img src="docs/screenshots/cli-status.png" width="440" alt="CLI live status monitor — running / starting / stopped"/> |

<sub>Left: the interactive menu — everything is also scriptable (`install · start --bg · stop · backup · restore · status · about · …`). Right: the live monitor — 3-state detection (TCP + HTTP verified), Web address, uptime, dsh &amp; Node.js versions, refreshed every 3s.</sub>

## What you can do with it

### Install DeepSeek Harness on Windows — double-click, no terminal

One exe detects the Node.js/npm environment, installs `@deepseek-ai/dsh` from the **official registry by default** (npmmirror opt-in, auto-retry with the other source on failure), then verifies what got installed. Nothing is installed without your keypress.

### Start, stop and monitor the dsh Web UI

Launch detects the service state — **running / starting / stopped** (TCP + HTTP verified, so a foreign process on :3080 is never mistaken for dsh) — auto-starts dsh with a 5-second countdown, opens your browser, and keeps a live status view (state / port / uptime, refreshed every 3s; red alert on disconnect).

### Back up and restore dsh data — including sessions and credentials

One-click **full backup** of the dsh data directory (`~/.dsh`) into `backup\` next to the exe; list-restore with confirmation, open-backup-folder; manual backups are kept forever, automatic ones follow the retention policy; every dangerous operation (restore / import / wipe / update) **auto-backs up first**.

### Migrate dsh to another PC

Copy the backup folder to the new machine and use **Import** — multi-workspace aware (`_workspace\name\`), compatible with old backup formats, long-path safe (`\\?\`, >260 chars).

### Backup Manager — see exactly what you backed up, before you touch anything

The GUI **Backups** page lists every backup as a row (time, kind: Manual / Auto / Pre-update / Pre-restore / Pre-wipe, size, validity) with **Restore / Export / Delete** for the selection and a one-click **Backup Now**; the list refreshes itself after any change. Restoring always runs a **Dry-Run first**: the confirm dialog shows how many files will be added, overwritten, or kept (destination-only files are never deleted) and roughly how much data will be copied — nothing changes until you confirm. The same plan is available headlessly via `restore --path <backup> --dry-run` (machine-readable `DRYRUN_*` lines), and the interactive wipe flow previews its delete counts (files / dirs / total size) before the two-step confirmation.

### Update or cleanly uninstall dsh

Menu-driven dsh updates (version list incl. rc pre-releases, destructive-action double confirm, pre-update backup; failure prints the backup location + manual rollback command — no silent half-states) and uninstall (data kept by default; wiping requires two-step confirmation and only runs while dsh is stopped).

### Update Center — know before you update

The GUI **Update** page visualizes the whole update picture read-only: current dsh version, latest stable, latest rc, update channel (`update_channel=stable|rc`), the most recent pre-update backup, rollback candidates (valid backup count), and a link to dsh release notes. **Check** refreshes on demand; **actually updating always goes through the interactive flow** (version list + destructive-action double confirmation) — checks may be automatic, updates never are.

### Settings page — change behaviour without editing config files

The GUI **Settings** page edits the toolkit's own configuration (`launcher.config`, still plain `key=value`) in four groups: **Harness** (Web host, workspace path), **Backup** (auto-backup retention, ≥3), **Update** (startup update check, dsh update detection, update channel), **Toolkit** (UI language). **Save submits only the keys you actually changed**, and out-of-range values are refused core-side — a typo cannot quietly corrupt your config. Headless equivalents: `config-get` (read every key) and `config-set <key> <value>` (whitelisted write).

### Health check — "what exactly is broken?"

`doctor` (CLI) and the GUI **Doctor** page run a read-only six-category check — System (Windows / Node / npm), Harness (installed & version), Service (port, listener identity, HTTP, 3-state), Workspace (path, permissions, size), Backup (dir, latest, age), Network (registry reachability) — and end with a machine-readable verdict (`DOCTOR_OK 0` / `DOCTOR_WARN n` / `DOCTOR_ERROR n`). `doctor --report <file>` exports a full diagnostic report with API keys / tokens / cookies / passwords redacted.

### Log Center — filter, search, export

The GUI **Log** page is a structured operation log: every entry carries a level (`INFO / WARN / ERROR`) and a timestamp, with one-click level filters, a live search box, and **Export / Copy** buttons (export writes a UTF-8 text file). Failures — timeouts, refused operations, missing core — are logged as WARN/ERROR so you can jump straight to what went wrong.

## Why this tool

| | Official (npm CLI) | This tool |
| --- | --- | --- |
| Target users | Developers comfortable with the terminal | Regular users / batch installs / remote assistance |
| Install | Install Node.js first, then type commands | Double-click the exe: detects the environment; you choose **(press 1)** whether to install dsh |
| Daily use | Manually open a terminal and browser every time | Detects on launch: auto-starts, opens the browser, monitors every 3s |
| Ops | None | Backup / restore / cross-PC import, uninstall (two-step confirm), entry switch, bilingual UI |
| Troubleshooting | Raw terminal errors | Friendly messages, health check, selftest report |

**Honest limits:**

1. **Unofficial maintenance.** No compatibility promise with future dsh versions. If dsh ever changes its default port / start command / data directory, this tool must be updated (those points have been stable so far).
2. **Trust boundary.** Distributing a Windows exe carries an inherent trust cost — hence this project is **fully open source (MIT)**, every release is **built by GitHub Actions CI from source**, and ships `hashes.txt` (SHA-256) + a **GPG signature** so anyone can verify releases.
3. **Positioning.** If you are comfortable with the terminal, the official npm commands are leaner; this tool is for people who do not want to touch one.

## 🖥️ GUI panel (three variants)

Since v2.4.1 a **graphical panel** ships in three forms — pick what fits (the panel is now **seven pages**: Home · Backups · Update · Settings · Log · Doctor · About; the screenshots above show Home / Log / About):

| Variant | File(s) | Unzip / run | For |
|---|---|---|---|
| **A. CLI core** | `DeepSeek Harness Toolkit.exe` | unzip fully, double-click | terminal users, scripts/automation |
| **B. GUI attached** | `Toolkit GUI.exe` + core **next to it** | **must unzip fully** — the GUI depends on the sibling core exe; a stray copy shows "Core exe (CLI) not found" | GUI users deploying with the core |
| **C. GUI standalone** | `Toolkit GUI Standalone.exe` | **single file, fully independent** — embeds the core and extracts it next to itself on first launch | "one exe handles everything" users |

**Shared features**: **seven pages** (Home — status LED + dsh version + Web address + action buttons · Backups — backup list with Restore / Export / Delete + Backup Now · Update — read-only update picture · Settings — four config groups · Log — structured log with level filters, search, export/copy · Doctor — six-category read-only health check · About), dark/light theme, Chinese/English, borderless rounded window, embedded logo; actions Install / Start Web / Stop Service / Backup Now / **Restore Backup (Dry-Run confirm dialog first)** / Check for Updates / Uninstall / Desktop Shortcut / Refresh Status. Starting Web while already running just opens the browser; current data is auto-backed up before any restore.

**Recommended usage**:

- **Everyday ops → C (standalone)**: backup/restore/start/stop all inside the GUI, one file is all you need
- **Scripts / automation / remote help → A (CLI core)**: programmatic (`status / start --bg / stop / backup / restore --path ...`)
- The **full zip** contains all three exes + docs; unzip and pick one. B and C can coexist (same core exe name, no conflict)

**Must unzip fully**:

- **Variant B**: GUI and core must live in the same folder; a lone GUI refuses operations with an explanation in the log
- Uninstall "Wipe all data" relies on the `.dsh_launcher_root` marker shipped in the package (**anti-mistake design; the program never creates it**): if you copy variant C to a fresh folder, that folder lacks the marker and wiping is safely refused — operate from the full package folder when you need to wipe

**Runs standalone**:

- **Variant C**: single exe, full functionality (embedded core extracted on first launch — via temp file + atomic rename, an interrupted extraction can never leave a broken exe)
- **Variant A**: single exe covers start/stop/backup/restore; install/wipe still work best from the full package (`.dsh_launcher_root` marker)

> The underlying CLI is exactly the core (install/update/uninstall still open a real console window for interaction).

## Quick start

1. **Download** the latest release from the [Releases page](../../releases/latest) — for everyday use grab `Toolkit.GUI.Standalone.exe` (variant C).
2. **Unzip** into its own folder (e.g. `D:\tools\`) — backups and config live next to the exe; putting it on the Desktop makes a mess. (Both the GUI and the CLI detect launches from Desktop/Downloads and warn you: the GUI asks for confirmation before continuing, the CLI prints a notice.)
3. **Double-click the exe.** dsh not installed → the menu waits for you; press **1** to install (official registry by default, npmmirror as an option, ~1–3 min). Run it again afterwards — the Web UI opens automatically.

## Usage

Double-click `DeepSeek Harness Toolkit.exe`, or use the command line:

```
DeepSeek Harness Toolkit.exe install|start|uninstall|update|check|about|help
```

Launching without arguments opens the interactive menu: with dsh installed the first run auto-starts the Web UI after a 5-second countdown (interruptible); later launches auto-start too. If dsh is **not** installed, the menu waits for your choice (press 1) — nothing is auto-installed. With the service already running, it goes straight to the status page.

**About workspaces:** backup auto-detects the workspace (two levels above the exe, rejecting obvious system/user dirs); you can also set it manually and persistently via menu **7 Entry → 3 Set workspace path** (the `ws=` line in `launcher.config`). **Multiple workspaces** are supported — add paths one by one (empty Enter to finish) — packed under `_workspace\name\` and restored one by one.

### FAQ

| Issue | Fix |
| --- | --- |
| 403 or blank page | Menu **7 Entry**, switch `127.0.0.1` ↔ `localhost` (the browser treats them as different sites; stale cache causes issues) |
| Delete failed during uninstall / wipe | Close the dsh web window first (file locks), retry; see `logs\launcher.log` if it still fails |
| Backup failed | Check `logs\launcher.log` next to the exe for the real reason |
| Backup failed (PathTooLongException) | Long path support and `dsh-data-*` skipping are built in; if the log still shows path issues, move that folder out of the workspace |
| Prompted for extra paths | Type each extra workspace path (empty Enter to finish), or preset one under **7 Entry → 3** |
| GUI says "Core exe (CLI) not found" | Variant B must sit **next to** `DeepSeek Harness Toolkit.exe` — unzip the full package, or use variant C (standalone) instead |

## Build from Source

Requires the built-in .NET Framework 4.x on Windows (preinstalled on Win10 / Win11):

```
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /optimize+ /target:exe /win32icon:icon.ico /out:"DeepSeek Harness Toolkit.exe" dsh_v2.cs
```

Or double-click `build_exe.cmd` in this directory. The GUI compiles from the same-rules single file `gui_v2.cs` (one source → both attached and standalone variants; the standalone adds `/resource:<core exe>,DSHCore.exe`).

**Reproducible releases (source == artifact):** each GitHub Release exe is compiled from this source by **GitHub Actions CI**, and `hashes.txt` is regenerated + **GPG-signed** by CI in the same run. The repository stores no binaries.

## Development / Testing

No test framework or third-party dependency is required.

- **Unit tests (225)** — same-assembly test proxy (`/define:UNIT`; the test entry point is `tests\unit_tests.cs`, everything else is the production code being tested):
  ```
  "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /define:UNIT /out:unittests.exe dsh_v2.cs tests\unit_tests.cs
  unittests.exe
  ```
  Exit code 0 = all green. Covers: path round-trips (incl. UNC / non-ASCII), workspace blacklist, dsh-data markers, root marker strictness, backup dir validation, log rotation, backup naming + retention policy, service-state judging, version compare / release parsing / update detection, netstat PID parsing, dry-run merge/delete planning (incl. restore-side skip-rule fidelity), backup kind parsing, export / delete validation, rollback-candidate lookup, configuration whitelist.

- **Integration tests (33 cases)** — stubbed end-to-end matrix (variants A/C, real 3080 probing; retention policy, restore/import blocked while running, bilingual asserts):
  ```
  pwsh -NoProfile -File tests\integration.ps1
  ```
  Touches only the stubbed data dir `~/.dsh_test` — never your real `~/.dsh`. When port 3080 is closed, "running"-related cases are SKIPped, not failed. Exit code 0 = all green.

- **CI** — GitHub Actions runs both test suites **plus a three-variant GUI compile guard** on every push to `main`, every pull request, and every `v*` tag; release assets + `hashes.txt` (+ GPG signature) are rebuilt only on `v*` tag push or manual dispatch (`workflow_dispatch`).

## Directory Layout

```
dsh_v2.cs            CLI core source (C#5, single file, no third-party deps)
gui_v2.cs            GUI source (WinForms; one file → attached + standalone variants)
app.manifest         GUI manifest (DPI awareness / compat)
build_exe.cmd        Rebuild script (core)
icon.ico             Program icon
logo.png             Product logo (1536×1536)
verify.ps1           One-click release verification (SHA-256 + GPG)
keys/                Maintainer GPG public key
SECURITY.md          Security policy, data & network boundaries
CHANGELOG.md         Release history (bilingual)
hashes.txt           SHA-256 manifest (regenerated by CI per release)
tests/               Unit (225) & integration (33) tests — no third-party deps
docs/screenshots/    README screenshots
.github/workflows/   CI: tests on push/PR; release build + GPG sign on tag/dispatch
.dsh_launcher_root   Install marker (shipped in the package; deletion guard)
backup/  logs/       Runtime dirs (gitignored — never committed)
```

## Error Log

- Runtime errors (backup / restore / uninstall failures, process start failures, etc.) are written to `logs\launcher.log` next to the exe (timestamped; rotated to `launcher.log.1` once it exceeds 1 MB).
- The log records error messages and file paths only — never passwords / API credentials. It is gitignored and never committed.

## Security Notes

- Full policy: see `SECURITY.md` (private reporting via GitHub Security Advisories).
- **Verify before you run (≈20 seconds)**:

  ```powershell
  powershell -ExecutionPolicy Bypass -File verify.ps1 -Tag v2.4.2 -OutDir D:\verify
  ```

  `verify.ps1` (shipped in the package) downloads the release artifacts (all three
  variants + `hashes.txt`), checks SHA-256 against the CI-generated `hashes.txt`,
  verifies the GPG signature (`hashes.txt.asc`) when GPG is available, and prints the
  provenance links. Read-only — installs nothing. With `-Tag` it uses fixed release
  download URLs and never calls the GitHub API (immune to anonymous rate limits);
  without `-Tag` it resolves the newest release via the API (optional `-Token` for
  rate-limited networks).
- **GPG signature**: `hashes.txt` is signed with the maintainer's key (`hashes.txt.asc`);
  public key `keys/sakanamaru-gpg.asc`, fingerprint
  `A2F67D170B5BE4845612642C240979232B4E4CE4`.
- Uninstall **"Wipe all data"** deletes the dsh data directory (`~/.dsh`, including sessions and API credentials) — the tool auto-backs it up to `backup\` first.
- Deletion is guarded **three ways**:
  1. **Blocked while the dsh Web service is running** (avoids file locks).
  2. The launcher root marker (`.dsh_launcher_root`) must exist **inside the package folder** — it is shipped with the release package, the program never creates it itself, and a stray exe copied elsewhere is permanently refused.
  3. The target directory must contain dsh-data markers (`settings.yaml` / `credentials.yaml` / `sessions` …).
  Any mismatch → deletion is refused.
- This tool only touches local data; the source contains no credentials or personal information.
- Backups are a best-effort file copy, not a transaction snapshot — for the most consistent backup, stop dsh before backing up.

## Redistribution & Credits (please read)

This project is open source under the MIT License. You are free to use, modify and redistribute it, but you MUST follow these rules:

1. **Keep the credits.** The v1 / v2 contributor credits and GitHub links in the app (startup banner / About page / file properties) and in this document must not be removed or replaced.
2. **Keep the claims.** The "Unofficial" notice and this LICENSE copyright statement must be distributed with every copy.
3. **Truthful attribution.** For commercial or redistributed releases, credit the source repository and original authors; removing credits is treated as infringement, and the authors reserve the right to file complaints (incl. DMCA) and pursue legal action.
4. **Verify releases.** `hashes.txt` in this repository records SHA-256 fingerprints of official release files; any binary claiming to be "officially compiled" can be verified against it.

## License

[MIT License](LICENSE)

## Acknowledgements

- [DeepSeek Harness (dsh)](https://www.npmjs.com/package/@deepseek-ai/dsh)
- Logo: designed with the assistance of ChatGPT (OpenAI), re-cropped for v2
- v1 script assistance: SOGR-Momono Dango（QwenPaw/DeepseekAPI-V4-Flash-0731）
- v2 rewrite & packaging: DeepSeek DSH（DSH/DeepseekAPI-V4-Flash-0731）
- GitHub: @sakanamaru  https://github.com/sakanamaru

If this tool helped you, a ⭐ on the repo's top right would mean a lot — it keeps this project going.
