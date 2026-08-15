# DeepSeek Harness Unofficial Launcher V2.0.0

<div align="center">

**[English](README.md) · [简体中文](README_zh-CN.md)**

<img src="logo.png" alt="DeepSeek Harness Unofficial Launcher" width="220"/>

</div>

A third-party, **unofficial** launcher / ops tool for the [DeepSeek Harness](https://www.npmjs.com/package/@deepseek-ai/dsh) (dsh) Web UI — install, start, monitor, uninstall, plus data backup & restore. Double-click and go.

> ⚠️ This project is **unofficial** and is not affiliated with DeepSeek.

## Credits

- 🧩 **v1 脚本协助 : SOGR-Momono Dango（QwenPaw/DeepseekAPI-V4-Flash-0731）**
- 🚀 **v2 重构封装 : DeepSeek DSH （DSH/DeepseekAPI-V4-Flash-0731）**
- 📦 **GitHub    : @sakanamaru  https://github.com/sakanamaru**

*(Credit lines kept verbatim from the app — see the banner / About page / file properties.)*

## Features

| Feature | Description |
| --- | --- |
| Smart Start | Detects service status on launch: running → status page; stopped → 5-second countdown auto-start/install |
| Install & Repair | Uses the npmmirror mirror, retries the official registry on failure, never touches your global npm config |
| Status Monitor | Auto-refreshes service status/port/uptime every 3s; red alert on disconnect; 1=back / 2=open WebUI |
| Backup & Restore | One-click backup of the data directory to `backup\` (supports **multiple workspaces**: auto-detected, then add paths one by one, empty Enter to finish; stored separately under `_workspace\name\`, restorable one by one); auto-skips `node_modules` and its own backup folders; supports list restore, cross-PC import, and opening the backup folder |
| Uninstall | Data kept by default; wiping needs two-step confirmation (today's date + `yes`) with **auto-backup first**; wiping is blocked while dsh web is running (avoid file locks) |
| Data Location | Auto-locates the dsh data directory (prefers `~/.dsh`, falls back to `%APPDATA%` etc.) |
| Entry | Pick and remember `127.0.0.1` / `localhost`: if the entry opens abnormally (often stale browser cache), switch with one key |
| Long Paths | Built-in long path support (`\\?\`, >260 chars) for backup/restore; nested backup packages (`dsh-data-*`) are auto-skipped |
| Multi-language | Follow system / Simplified Chinese / English, persisted |
| Credits | v1 / v2 contributors credited in-app and in file properties |

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
| Install | Install Node.js first, then type commands | Double-click the exe: detects the environment, installs and starts automatically |
| Daily use | Manually open a terminal and browser every time | Detects on launch: auto-starts, opens the browser, monitors every 3s |
| Ops | None | Backup/restore/cross-PC import, uninstall (two-step confirm), entry switch, bilingual UI |
| Troubleshooting | Raw terminal errors | Friendly messages, health page, selftest report |

**Honest limits:**

1. **Unofficial maintenance**: no compatibility promise with future dsh versions; if dsh ever changes its default port / start command / data directory, this tool must be updated (those points are stable so far).
2. **Trust boundary**: distributing a Windows exe carries an inherent trust cost — hence this project is **fully open source (MIT)** and ships `hashes.txt` (SHA-256 fingerprints) so anyone can verify the release.
3. **Positioning**: if you are comfortable with the terminal, the official npm commands are leaner; this tool is for people who do not want to touch one.

## Usage

Double-click `DeepSeek Harness Unofficial Launcher V2.0.0.exe`, or use the command line:

```
DeepSeek Harness Unofficial Launcher V2.0.0.exe install|start|uninstall|check|about|help
```

Launching without arguments opens the interactive menu: first run auto-selects after a 5-second countdown (interruptible), later launches auto-start the Web UI, and go straight to the status page when the service is already running.

**About workspaces:** backup auto-detects the workspace (two levels above the exe, rejecting obvious system/user dirs); you can also set it manually and persistently via menu **7 Entry → 3 Set workspace path** (the `ws=` line in `launcher.config`). **Multiple workspaces** are supported — add paths one by one (empty Enter to finish) — packed under `_workspace\name\` and restored one by one.

### First run (3 steps)

1. **Unzip** the release into its own folder (e.g. `D:\tools\`) — backups and config live next to the exe; putting it on the Desktop makes a mess
2. **Double-click the exe**: it auto-installs dsh on first run (npmmirror by default, ~1–3 min); run it again afterwards
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
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /optimize+ /target:exe /win32icon:icon.ico /out:"DeepSeek Harness Unofficial Launcher V2.0.0.exe" dsh_v2.cs
```

Or double-click `build_exe.cmd` in this directory.

**Reproducible releases:** the exe attached to each GitHub Release is compiled from this source by **GitHub Actions CI** (source == artifact), and `hashes.txt` is regenerated by CI in the same run — no binaries are stored in this repository.

## Directory Layout

```
DeepSeek Harness Unofficial Launcher V2.0.0.exe   Main program (with icon)
dsh_v2.cs            v2 source (C#5, single file, no third-party deps)
build_exe.cmd        Rebuild script
icon.ico             Program icon source
logo.png             Original logo PNG (generated with ChatGPT)
README.md            Docs (English)
README_zh-CN.md      Docs (Simplified Chinese)
hashes.txt           SHA-256 manifest of released files
backup/              Backup dir (gitignored — never commit)
logs/                Error log dir (gitignored — never commit)
```

## Error Log

- Runtime errors (backup/restore/uninstall failures, process start failures, etc.) are written to `logs\launcher.log` next to the exe (timestamped, auto-reset over 200 KB)
- The log records error messages and file paths only — never passwords/API credentials; it is gitignored and never committed

## Security Notes

- Uninstall "Wipe all data" deletes the dsh data directory (`~/.dsh`, including sessions and API credentials) — the tool auto-backs it up to `backup\` first
- Deletion is guarded **three ways**: uninstall is **blocked while the dsh Web service is running**; wiping requires the launcher root marker (`.dsh_launcher_root`, left in the launcher folder by a previous session — guards against a stray exe copied elsewhere) **plus** dsh-data markers inside the target (`settings.yaml` / `credentials.yaml` / `sessions` …); any mismatch is refused
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