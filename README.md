# DeepSeek Harness Toolkit V2.1.0

<div align="center">

**[English](README.md) · [简体中文](README_zh-CN.md)**

<img src="logo.png" alt="DeepSeek Harness Toolkit" width="220"/>

**DeepSeek Harness Toolkit — Windows installer, monitor, backup & restore tool**

</div>

A third-party, **unofficial** launcher / ops tool for the [DeepSeek Harness](https://www.npmjs.com/package/@deepseek-ai/dsh) (dsh) Web UI. **Double-click and go.**

**What makes it different from running the npm package bare:**

- 🔧 **Install & repair** — official npmjs.org registry by default, npmmirror as an opt-in; auto-fallback to the other source; never touches your global npm config
- 📊 **Smart start + 3-state monitor** — TCP + HTTP verified: **running / starting / stopped** (a foreign service on :3080 is no longer mistaken for dsh); 3s status auto-refresh with red alert on disconnect
- 💾 **Backup & retention** — one-click backup to `backup\` with **multi-workspace** support; **manual backups are kept forever**, auto/protection backups are auto-cleaned (newest N kept, floor 3, `keep_backups=`); restore/import are refused while dsh is running; cross-PC import, long-path safe
- 🛡️ **Wipe protection** — two-step confirm (date + `yes`) with **auto-backup first**; blocked while the web service is running; a package-shipped root marker permanently refuses a stray exe copied elsewhere
- 🌍 **One exe, bilingual menu** — Simplified Chinese / English, built on .NET Framework 4.x (preinstalled on Win10/11); **silent update check** on launch (GitHub Releases, `check_update=off` to disable); **1 MB log rotation** (`launcher.log → launcher.log.1`)

> ⚠️ This project is **unofficial** and is not affiliated with DeepSeek.

## Credits

- 🧩 **v1 脚本协助 : SOGR-Momono Dango（QwenPaw/DeepseekAPI-V4-Flash-0731）**
- 🚀 **v2 重构封装 : DeepSeek DSH （DSH/DeepseekAPI-V4-Flash-0731）**
- 📦 **GitHub    : @sakanamaru  https://github.com/sakanamaru**

*(Credit lines kept verbatim from the app — see the banner / About page / file properties.)*

## Features

| Feature | Description |
| --- | --- |
| Smart Start | Detects service status on launch (3-state: **running / starting / stopped** — TCP + HTTP verified so a foreign service on :3080 is not mistaken for dsh): running → status page; stopped → 5-second countdown auto-start (only when dsh is installed; if not installed, the menu waits for your choice — nothing is auto-installed) |
| Install & Repair | Asks for the source: **official npmjs.org by default**, npmmirror offered as opt-in; always retries the other registry on failure; never touches your global npm config |
| Status Monitor | Auto-refreshes service status (3-state)/port/uptime every 3s; red alert on disconnect; 1=back / 2=open WebUI |
| Backup & Restore | One-click backup of the data directory to `backup\` (supports **multiple workspaces**: auto-detected, then add paths one by one, empty Enter to finish; stored separately under `_workspace\name\`, restorable one by one); auto-skips `node_modules` and its own backup folders; supports list restore, cross-PC import, and opening the backup folder; **manual backups (no suffix) are kept forever**, auto/protection backups (`-auto` / `-pre-*`) beyond `keep_backups` (default 10, min 3) are oldest-first cleaned; **restore/import are refused while dsh is running** (same guard as wipe) |
| Uninstall | Data kept by default; wiping needs two-step confirmation (today's date + `yes`) with **auto-backup first**; wiping is blocked while dsh web is running (avoid file locks) |
| Update Check | On launch, silently queries the GitHub Releases API; only prompts when a newer version exists (with a link); offline/API failure is silent; disable with `check_update=off` |
| Log Rotation | `logs\launcher.log` archived to `launcher.log.1` once it exceeds 1 MB (one history file kept) — no more silent log loss |
| Data Location | Auto-locates the dsh data directory (prefers `~/.dsh`, falls back to `%APPDATA%` etc.) |
| Entry | Pick and remember `127.0.0.1` / `localhost`: if the entry opens abnormally (often stale browser cache), switch with one key |
| Long Paths | Built-in long path support (`\\?\`, >260 chars) for backup/restore; nested backup packages (`dsh-data-*`) are auto-skipped |
| Multi-language | Follow system / Simplified Chinese / English, persisted |

## What's New in v2.1

- **Backup retention policy** — backups without a suffix (manual) are **kept forever**; auto / protection backups (`-auto`, `-pre-restore`, `-pre-import`, `-pre-wipe`) beyond `keep_backups` (default 10, min 3) are cleaned oldest-first, with the list printed before deletion
- **Three-state service detection** — TCP open + HTTP 2xx/3xx = **running**; TCP only = **starting (not ready)**; neither = **stopped** (startup wait, monitor, `check` and open-browser all use it)
- **Restore / Import refused while dsh is running** — same guard as uninstall/wipe (no more "you should close it first" only)
- **Silent update check** on launch (GitHub Releases API; prompts only when a newer version exists; `check_update=off` to disable) and **1 MB log rotation** (`launcher.log → launcher.log.1`)
- **Version-free single-instance lock** (`DeepSeek-Harness-Toolkit-single`) — v2.0 and v2.1 can never open two interactive instances on the same machine at once
- Internal version banner / About / assembly metadata now say **V2.1.0** (the exe file name keeps the product name `… V2.0.0.exe`; it is a fixed product name, not a version marker)

## Relation to the Official Deployment

The official way is just two commands:

```
npm install -g @deepseek-ai/dsh
dsh web
```

This tool does **not replace** the official way; it serves those who prefer not to (or cannot) use a terminal.

| | Official (npm CLI) | This tool |
| --- | --- | --- |
| Target users | Developers comfortable with the terminal | Regular users / batch installs / remote assistance |
| Install | Install Node.js first, then type commands | Double-click the exe: detects the environment and starts automatically; you choose **(press 1)** whether to install dsh |
| Daily use | Manually open a terminal and browser every time | Detects on launch: auto-starts, opens the browser, monitors every 3s |
| Ops | None | Backup/restore/cross-PC import, uninstall (two-step confirm), entry switch, bilingual UI |
| Troubleshooting | Raw terminal errors | Friendly messages, health page, selftest report |

**Honest limits:**

1. **Unofficial maintenance**: no compatibility promise with future dsh versions; if dsh ever changes its default port / start command / data directory, this tool must be updated (those points are stable so far).
2. **Trust boundary**: distributing a Windows exe carries an inherent trust cost — hence this project is **fully open source (MIT)** and ships `hashes.txt` (SHA-256 fingerprints) so anyone can verify the release.
3. **Positioning**: if you are comfortable with the terminal, the official npm commands are leaner; this tool is for people who do not want to touch one.

## Usage

Double-click `DeepSeek Harness Toolkit V2.0.0.exe`, or use the command line:

```
DeepSeek Harness Toolkit V2.0.0.exe install|start|uninstall|check|about|help
```

Launching without arguments opens the interactive menu: when dsh is already installed the first run auto-starts the Web UI after a 5-second countdown (interruptible), and later launches auto-start too; when dsh is **not** installed the menu waits for your choice (press 1 to install) — nothing is auto-installed. With the service already running, it goes straight to the status page.

**About workspaces:** backup auto-detects the workspace (two levels above the exe, rejecting obvious system/user dirs); you can also set it manually and persistently via menu **7 Entry → 3 Set workspace path** (the `ws=` line in `launcher.config`). **Multiple workspaces** are supported — add paths one by one (empty Enter to finish) — packed under `_workspace\name\` and restored one by one.

### First run (3 steps)

1. **Unzip** the release into its own folder (e.g. `D:\tools\`) — backups and config live next to the exe; putting it on the Desktop makes a mess
2. **Double-click the exe**: dsh not installed → the menu waits for you; press **1** to install (official registry by default, npmmirror as an option, ~1–3 min); run it again afterwards
3. Then choose **2 Start Web UI** in the menu and the browser opens automatically

### FAQ

| Issue | Fix |
| --- | --- |
| 403 or blank page | Menu **7 Entry**, switch `127.0.0.1` ↔ `localhost` (the browser treats them as different sites; stale cache causes issues) |
| Delete failed during uninstall/wipe | Close the dsh web window first (file locks), retry; see `logs\launcher.log` if it still fails |
| Backup failed | Check `logs\launcher.log` next to the exe for the real reason |
| Backup failed (PathTooLongException) | Long path support and `dsh-data-*` skipping are built in; if the log still shows path issues, move that folder out of the workspace |
| Prompted for extra paths | Type each extra workspace path (empty Enter to finish), or preset one under **7 Entry → 3** |

## Build from Source

Requires the built-in .NET Framework 4.x on Windows (preinstalled on Win10/Win11).

```
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /optimize+ /target:exe /win32icon:icon.ico /out:"DeepSeek Harness Toolkit V2.0.0.exe" dsh_v2.cs
```

Or double-click `build_exe.cmd` in this directory.

**Reproducible releases:** the exe attached to each GitHub Release is compiled from this source by **GitHub Actions CI** (source == artifact), and `hashes.txt` is regenerated by CI in the same run — no binaries are stored in this repository.

## Development / Testing

No test framework or third-party dependency is required.

- **Unit tests** — same-assembly test proxy (`/define:UNIT`; the test entry point, everything else is the production code being tested):
  ```
  "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /define:UNIT /out:unittests.exe dsh_v2.cs tests\unit_tests.cs
  unittests.exe
  ```
  Exit code 0 = all green. Path round-trips (incl. UNC / non-ASCII), workspace blacklist (19 reject + 3 allow), dsh-data markers, log rotation, backup naming + retention policy, service-state judging, version compare / release parsing / update detection.

- **Integration tests** — 22-case stubbed end-to-end matrix (variants A/C, real 3080 probing; retention policy, restore/import blocked while running, bilingual asserts):
  ```
  pwsh -NoProfile -File tests\integration.ps1
  ```
  Touches only the stubbed data dir `~/.dsh_test` — never your real `~/.dsh`. When port 3080 is closed the "running" cases are SKIPped, not failed. Exit code 0 = all green.

- **CI**: the GitHub Actions `test` job runs both suites on every push / manual dispatch.

## Directory Layout

```
DeepSeek Harness Toolkit V2.0.0.exe   Main program (with icon)
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

- Runtime errors (backup/restore/uninstall failures, process start failures, etc.) are written to `logs\launcher.log` next to the exe (timestamped; once it exceeds 1 MB it is rotated to `launcher.log.1` and a fresh log starts — one history file is kept)
- The log records error messages and file paths only — never passwords/API credentials; it is gitignored and never committed

## Security Notes

- Uninstall "Wipe all data" deletes the dsh data directory (`~/.dsh`, including sessions and API credentials) — the tool auto-backs it up to `backup\` first
- Deletion is guarded **three ways**: uninstall is **blocked while the dsh Web service is running**; wiping requires the launcher root marker (`.dsh_launcher_root`, shipped inside the package folder — a stray exe copied elsewhere is permanently refused and never self-seeds the marker) **plus** dsh-data markers inside the target (`settings.yaml` / `credentials.yaml` / `sessions` …); any mismatch is refused
- This tool only touches local data; the source contains no credentials or personal information

## Redistribution & Credits (please read)

This project is open source under the MIT License. You are free to use, modify and redistribute it, but you MUST follow these rules:

1. **Keep the credits**: the v1/v2 contributor credits and GitHub links in the app (startup banner / About page / file properties) and in this document must not be removed or replaced with others.
2. **Keep the claims**: the "Unofficial" notice and this LICENSE copyright statement must be distributed with every copy.
3. **Truthful attribution**: for commercial or redistributed releases, credit the source repository and original authors; removing credits is treated as infringement, and the authors reserve the right to file complaints (incl. DMCA) and pursue legal action.
4. **Verify releases**: `hashes.txt` in this repository records SHA-256 fingerprints of official release files; any binary claiming to be "officially compiled" can be verified against it.

## License

[MIT License](LICENSE)

## Acknowledgements

- [DeepSeek Harness (dsh)](https://www.npmjs.com/package/@deepseek-ai/dsh)
- Logo: designed with the assistance of ChatGPT (OpenAI)
- v1 脚本协助 : SOGR-Momono Dango（QwenPaw/DeepseekAPI-V4-Flash-0731）
- v2 重构封装 : DeepSeek DSH （DSH/DeepseekAPI-V4-Flash-0731）
- GitHub    : @sakanamaru  https://github.com/sakanamaru