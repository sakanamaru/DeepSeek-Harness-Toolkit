# DeepSeek Harness Toolkit

<div align="center">

**[English](README.md) · [简体中文](README_zh-CN.md)**

<img src="logo.png" alt="DeepSeek Harness Toolkit" width="220"/>

**Windows installer, monitor, backup & restore tool for the DeepSeek Harness (dsh) Web UI — double-click and go.**

</div>

A third-party, **unofficial** launcher / ops tool for [DeepSeek Harness](https://www.npmjs.com/package/@deepseek-ai/dsh) (dsh).
Install, start, monitor and uninstall the dsh Web UI, with data backup / restore built in. **No terminal required.**

> ⚠️ This project is **unofficial** and is not affiliated with DeepSeek.

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
2. **Trust boundary.** Distributing a Windows exe carries an inherent trust cost — hence this project is **fully open source (MIT)** and ships `hashes.txt` (SHA-256 fingerprints) so anyone can verify releases.
3. **Positioning.** If you are comfortable with the terminal, the official npm commands are leaner; this tool is for people who do not want to touch one.

## Features

| Feature | Description |
| --- | --- |
| Smart Start | Detects service status on launch — **running / starting / stopped** (TCP + HTTP verified, so a foreign service on :3080 is not mistaken for dsh): running → status page; stopped → 5-second countdown auto-start (only when dsh is installed; if not installed, the menu waits for your choice — nothing is auto-installed) |
| Install & Repair | Asks for the source: **official npmjs.org by default**, npmmirror as opt-in; always retries the other registry on failure; never touches your global npm config; Enter=latest, `L`=list historical versions to install |
| Status Monitor | Auto-refreshes service status / port / uptime every 3s; red alert on disconnect; 1=back / 2=open WebUI |
| Update dsh (Menu 8) | New-version detection → version pick (Enter=latest / `L`=list, pre-release/rc supported) → ⚠️ destructive **double confirm** → auto backup first (`-pre-update`) → npm install → local history remembered (max 10, `*` marked); **refused while dsh is running**; on failure there is **no automatic rollback** — the tool prints the backup location and the manual rollback command; recover via the `-pre-update` backup or the historical version list |
| Update Check | On launch, silently queries GitHub Releases; only prompts when a newer version exists (with a link); offline / API failure is silent; disable with `check_update=off`; dsh update detection shown by `check` (`check_dsh_update=off`) |
| Backup & Restore | One-click backup of the data directory to `backup\` (**multiple workspaces** supported: auto-detected, then add paths one by one, stored under `_workspace\name\`, restored one by one); auto-skips `node_modules` and its own backup folders; list restore, cross-PC import, open-backup-folder; **manual backups (no suffix) are kept forever**; auto/protection backups (`-auto` / `-pre-*`) beyond `keep_backups` (default 10, min 3) are cleaned oldest-first; **restore/import are refused while dsh is running** (same guard as wipe) |
| Uninstall | Data kept by default; wiping the data requires two-step confirmation (today's date + `yes`) with **auto-backup first**; wiped only when dsh is stopped |
| Data Location | Auto-locates the dsh data directory (prefers `~/.dsh`, falls back to `%APPDATA%` etc.) |
| Long Paths | Built-in long path support (`\\?\`, >260 chars) in backup/restore; nested backup packages (`dsh-data-*`) are auto-skipped |
| Entry | Pick and remember `127.0.0.1` / `localhost`; if the entry opens abnormally (often stale browser cache), switch with one key |
| Log Rotation | `logs\launcher.log` archived to `launcher.log.1` once it exceeds 1 MB (one history file kept) — no silent log loss |
| Multi-language | Follow system / Simplified Chinese / English, persisted |

## 🖥️ GUI panel (Alpha)

Since v2.4.0 a **graphical panel** `Toolkit GUI.exe` ships as an Alpha preview:

- **Three pages**: Home (status LED + dsh version + Web address + action buttons) · Log (operation history) · About
- Dark/light theme + Chinese/English, borderless rounded window, logo embedded (single-file distribution)
- **Actions**: Install / Start Web / Stop Service / Backup Now / **Restore Backup (picker dialog to choose a backup folder + confirmation)** / Check for Updates / Uninstall / Desktop Shortcut / Refresh Status
- Restore details: newest backup first and preselected; while the service is running the "restore" button is disabled with a red explanation — stop the service first (current data is auto-backed up before restoring)
- Starting Web while the service is already running simply opens the browser

Usage: put `Toolkit GUI.exe` next to the core `DeepSeek Harness Toolkit.exe` and double-click the GUI.

> Alpha: UI/UX may still change; the underlying CLI is exactly the core (install/update/uninstall still open a real console window for interaction).

## Quick start

1. **Unzip** the release into its own folder (e.g. `D:\tools\`) — backups and config live next to the exe; putting it on the Desktop makes a mess.
2. **Double-click the exe.** dsh not installed → the menu waits for you; press **1** to install (official registry by default, npmmirror as an option, ~1–3 min). Run it again afterwards.
3. Choose **2 Start Web UI** and the browser opens automatically.

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

## Build from Source

Requires the built-in .NET Framework 4.x on Windows (preinstalled on Win10 / Win11):

```
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /optimize+ /target:exe /win32icon:icon.ico /out:"DeepSeek Harness Toolkit.exe" dsh_v2.cs
```

Or double-click `build_exe.cmd` in this directory.

**Reproducible releases (source == artifact):** each GitHub Release exe is compiled from this source by **GitHub Actions CI**, and `hashes.txt` is regenerated by CI in the same run. The repository stores no binaries.

## Development / Testing

No test framework or third-party dependency is required.

- **Unit tests (103)** — same-assembly test proxy (`/define:UNIT`; the test entry point is `tests\unit_tests.cs`, everything else is the production code being tested):
  ```
  "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /define:UNIT /out:unittests.exe dsh_v2.cs tests\unit_tests.cs
  unittests.exe
  ```
  Exit code 0 = all green. Covers: path round-trips (incl. UNC / non-ASCII), workspace blacklist, dsh-data markers, root marker strictness, backup dir validation, log rotation, backup naming + retention policy, service-state judging, version compare / release parsing / update detection.

- **Integration tests (25 cases)** — stubbed end-to-end matrix (variants A/C, real 3080 probing; retention policy, restore/import blocked while running, bilingual asserts):
  ```
  pwsh -NoProfile -File tests\integration.ps1
  ```
  Touches only the stubbed data dir `~/.dsh_test` — never your real `~/.dsh`. When port 3080 is closed, "running"-related cases are SKIPped, not failed. Exit code 0 = all green.

- **CI** — GitHub Actions runs both test suites automatically on **every push to `main`, every pull request, and `v*` tag push**; release assets + `hashes.txt` are rebuilt only on `v*` tag push or manual dispatch (`workflow_dispatch`).

## Directory Layout

```
DeepSeek Harness Toolkit.exe   Main program (with icon)
dsh_v2.cs            v2 source (C#5, single file, no third-party deps)
build_exe.cmd        Rebuild script
icon.ico             Program icon source
logo.png             Product logo PNG (1536×1536)
tests/               Unit & integration tests (no third-party deps; not shipped in releases)
.dsh_launcher_root   Install marker (shipped with the package; deletion guard)
README.md            Docs (English)
README_zh-CN.md      Docs (Simplified Chinese)
hashes.txt           SHA-256 manifest of released files
backup/              Backup dir (gitignored — never commit)
logs/                Error log dir (gitignored — never commit)
```

## Error Log

- Runtime errors (backup / restore / uninstall failures, process start failures, etc.) are written to `logs\launcher.log` next to the exe (timestamped; rotated to `launcher.log.1` once it exceeds 1 MB).
- The log records error messages and file paths only — never passwords / API credentials. It is gitignored and never committed.

## Security Notes

- Full policy: see `SECURITY.md` (private reporting via GitHub Security Advisories).
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