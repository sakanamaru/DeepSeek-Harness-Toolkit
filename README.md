# DeepSeek Harness Toolkit — `dsh-minato`

**A no-terminal-needed installer, monitor, backup and repair toolbox for the DeepSeek Harness (dsh) Web UI.**
Windows and Linux. Runs locally, writes nothing anywhere else.

> ⚠️ **Unofficial.** This project is not affiliated with, endorsed by, or connected to DeepSeek. It is a third-party
> helper that drives the `dsh` command line you already have.
>
> **English** · [简体中文](README_zh-CN.md) · [日本語](README_ja.md)

---

## Screenshots

Everything below is the real interface, captured against a **fixture data directory** — no real session, path or
figure appears in these images.

| Dashboard | Sessions & tokens | Backup |
|---|---|---|
| ![Dashboard](docs/screenshots/gui-kanban.png) | ![Sessions](docs/screenshots/gui-sessions.png) | ![Backup](docs/screenshots/gui-backup.png) |

| Health check | Settings | Update centre |
|---|---|---|
| ![Doctor](docs/screenshots/gui-doctor.png) | ![Settings](docs/screenshots/gui-settings.png) | ![Update](docs/screenshots/gui-update.png) |

<details>
<summary>The remaining pages (overview, plugins, about, logs) and the CLI</summary>

| Overview | Plugins | About | Logs |
|---|---|---|---|
| ![Overview](docs/screenshots/gui-overview.png) | ![Plugins](docs/screenshots/gui-plugins.png) | ![About](docs/screenshots/gui-about.png) | ![Logs](docs/screenshots/gui-logs.png) |

| CLI menu | CLI status |
|---|---|
| ![CLI menu](docs/screenshots/cli-menu.png) | ![CLI status](docs/screenshots/cli-status.png) |

</details>

---

## What it does

| | |
|---|---|
| **Install dsh** | Double-click. No terminal, no Node.js knowledge required. It installs the CLI, sets up the PATH and Start-menu entry, and keeps an uninstaller beside the install. |
| **Start / stop / monitor** | One button for the Web UI, with a live status line and the URL to open. |
| **Back up and restore** | Sessions, settings and credentials — packaged with a completion marker and a content hash so a truncated or tampered package is refused rather than restored. |
| **Backup manager** | See exactly what each package contains before you touch it. |
| **Migrate to another PC** | Export a package, import it on the new machine. |
| **Update centre** | One place for the web UI, the official desktop app, this tool, and installed plugins. |
| **Health check** | "What exactly is broken?" — it names the failing piece and prints the one line that fixes it. |
| **Uninstall** | **Never deletes your data by default**, and refuses to delete a directory that does not look like an install. |

---

## Install

### Windows

Download `dsh-minato-<version>-win-x64-setup.exe` from [Releases](../../releases) and run it.

- The installer is **not digitally signed**, so SmartScreen may show an "unknown publisher" warning the first time.
  That is what an unsigned binary looks like — verify the download against the published `.sha256` first.
- Prefer a portable copy? Use `dsh-minato-win-x64.zip`, unzip, and run `gui\dsht-gui.exe`.

### Linux

Download `dsh-minato-linux-x64.tar.gz`, then:

```bash
tar -xzf dsh-minato-linux-x64.tar.gz
cd dsh-minato-linux-x64
./install.sh            # installs for the current user; --prefix <dir> to choose the location
```

Uninstall with `./install.sh --uninstall` (add `--force` only if you have moved the installation yourself).

---

## Quick start

1. Install (above) and open **dsh-minato**.
2. The dashboard shows the current state. If `dsh` is missing, the health check says so and offers the fix.
3. Press **Start** and open the Web UI URL it prints.
4. Before you change anything, press **Backup**.

---

## The GUI

Ten pages, all reachable from the left rail:

| Page | What it is for |
|---|---|
| **Overview** | One screen: what is installed, what is running, what is out of date. |
| **Dashboard** | Key figures — sessions, cache-hit rate, tokens — and one-click start/stop. |
| **Sessions & tokens** | Per-session breakdown, parent/child grouping, sortable. |
| **Plugins** | Which plugins a profile has, which are disabled, and which would break a boot. |
| **Backup** | Create, inspect, verify, restore, export and delete packages. |
| **Health check** | The "what is broken" report, with the exact prescription. |
| **Settings** | Language, port, behaviour — without editing YAML. |
| **About** | Version, credits, and what this tool deliberately does not do. |
| **Update centre** | Update dsh, the desktop app, this tool or a plugin. |
| **Logs** | Filter, search and export the launcher log. |

---

## Command line

The GUI drives the same CLI, which is also usable on its own:

```text
dsh-minato status [--detail]      what is installed / running / listening
dsh-minato start | stop           start or stop the dsh Web UI
dsh-minato install | update       install or update dsh (and this tool)
dsh-minato uninstall              remove the tool (never touches your data)
dsh-minato sessions               per-session token and cache figures
dsh-minato backup [--to <dir>]    create a backup package
dsh-minato backup-list [--verify] list packages, and verify their contents
dsh-minato backup-dir [--set <d>] where backups are written
dsh-minato restore --path <pkg> [--apply] [--yes]
dsh-minato backup-export | backup-delete
dsh-minato doctor                 full health check, with prescriptions
dsh-minato profiles | profilecheck | profilepatch | bridge-install
dsh-minato bootdiag               why did dsh fail to start?
dsh-minato verify-install         check the files you downloaded
dsh-minato log | config-get | config-set | autostart | shortcut
dsh-minato version | about | selftest
```

Every command prints machine-readable markers (`STATUS_OK`, `BACKUP_OK`, `RESTORE_FAIL`, …) so scripts and the GUI can
parse results instead of guessing from prose.

---

## Safety and privacy — what is actually true

This section states only what the code does. If a claim here is not backed by the code, it is a bug; please report it.

- **Local only.** The tool reads and writes your own machine. It contacts the network **only** in these commands:
  `check`, `update-info`, `update-center`, `doctor`, `install`, `update`, and `verify-install --url`. Everything else — status,
  sessions, backup, restore, doctor, logs — never opens a connection.
- **Writes are protected where it matters.** Before `restore` a backup is always taken first; `update` and `import` try to and continue if that fails; `wipe` only prints the path to delete by hand and neither deletes nor backs up
  and its location is printed, so a failed operation can be rolled back. Not every write is preceded by a backup:
  settings changes (`config-set`, `backup-dir --set`), profile patches and shortcut/PATH edits are not.
- **Your data is not deleted by uninstall.** Uninstall removes the tool's own files. Data removal is a separate,
  explicit action.
- **Backup integrity is checked, not assumed.** A package carries a completion marker written last and a per-file
  hash; a package that was interrupted, or whose contents changed, is refused.
- **Zero third-party runtime dependencies** for the CLI, the installer, the launcher and the dsh plugin. The
  **GUI is built on Avalonia** (a UI framework), which is the one exception — see [PRIVACY.md](PRIVACY.md) and
  [ASSETS.md](ASSETS.md).
- **No telemetry, no accounts, no uploads.**

---

## Troubleshooting

**"dsh won't start", or a plugin failed to load.** Run the health check — it names the offending profile entry and
prints the exact line to change:

```bash
dsh-minato doctor              # read-only: which entries would break a boot?
dsh-minato profilecheck        # the same question, per profile
```

**A restore or backup was refused.** That is deliberate. Check what the package actually contains:

```bash
dsh-minato backup-list --verify
```

**Antivirus flagged the installer.** A self-extracting installer that writes to `%LOCALAPPDATA%`, adds a PATH entry
and creates shortcuts behaves like a dropper even when it is not one. Verify the download against the published
`.sha256`, or use the zip, which has no self-extracting wrapper.

---

## Build from source

The CLI and tools are plain C# and PowerShell — no third-party packages.

```powershell
# Windows CLI (in-box compiler is enough)
csc /target:exe /out:dsh-minato.exe (Get-ChildItem v3\src -Recurse -Filter *.cs).FullName

# GUI (needs the .NET SDK, because Avalonia is a NuGet package)
dotnet build v3\gui\Dsht.Gui.Avalonia\Dsht.Gui.Avalonia.csproj -c Release
```

```bash
# Linux CLI
dotnet publish v3/src/Dsht.Cli/Dsht.Cli.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

---

## Development and testing

Every claim in this README is checked by a gate that can be run locally:

| Gate | What it proves |
|---|---|
| `v3/tests/verify_switchover.ps1` | All nine readiness gates at once — the single command to run. |
| `v3/tests/compare_markers.ps1` | The CLI's machine-readable output still matches the v2.x contract. |
| `v3/tests/verify_fixes.ps1` | 70 previously-fixed defects are still fixed in the source. |
| `v3/tests/verify_restore_apply.ps1` | A real restore into an isolated root, with zero writes outside it. |
| `v3/tests/Dsht.Contracts.Tests` | 334 domain and platform contract checks. |
| `v3/gui/Dsht.Gui.LogicTests` | 58 GUI marker-parsing checks. |
| `plugin/dsh-minato-bridge/test` | 23 plugin checks, including the dependency declaration. |
| `v3/tools/verify_backup_chain.sh` | The backup trust chain end to end (Linux). |
| `v3/tools/verify-linux.sh` | A release tarball is what it claims to be. |

The same gates run in CI on every push, on Windows and Linux.

---

## Status

**V3 is a preview.** The CLI and the Linux tools are complete and verified on real machines; the GUI is feature-
complete for the ten pages above and is being polished. **V2.x remains maintained** until V3 reaches full parity.

Known limitations, stated plainly:

- The Windows artefacts are **not digitally signed** (a free code-signing application was declined). Verify hashes.
- `verify_fixes.ps1` matches text; it can confirm a fix is present, not that it is reachable.
- The GUI is built on Avalonia and therefore is not dependency-free, unlike the rest of the project.

---

## License and credits

MIT — see [LICENSE](LICENSE). Privacy details in [PRIVACY.md](PRIVACY.md); asset licensing and the icon's provenance
in [ASSETS.md](ASSETS.md).

`dsh-minato` (みなと, "harbour") was previously published as `DeepSeek-Harness-Toolkit`; the rename is recorded in the
git history. This is an independent project and is not affiliated with DeepSeek.
