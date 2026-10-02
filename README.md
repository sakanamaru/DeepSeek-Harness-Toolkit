# DeepSeek Harness Toolkit — `dsh-minato`

**A no-terminal-needed installer, monitor, backup and repair toolbox for the DeepSeek Harness (dsh) Web UI.**
Windows and Linux. Runs locally. No telemetry, no accounts, no uploads.

> ⚠️ **Unofficial.** This project is not affiliated with, endorsed by, or connected to DeepSeek. It is a third-party
> helper that drives the `dsh` command line you already have.
>
> **English** · [简体中文](README_zh-CN.md) · [日本語](README_ja.md)

---

<p align="center">
  <img src="logo.png" alt="dsh-minato" width="180">
</p>

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
  This is **deliberate, not an oversight**: the Windows artefacts currently carry **no trusted Authenticode
  publisher**, so there is no signature to check. Integrity is provided separately instead — the published
  `.sha256`, and the GPG-signed `hashes.txt` — so verify the download against those before running it.
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
  `check`, `update-info`, `update-center`, `doctor`, `install`, `update`, `verify-install --url`, and `balance` (only
  when you have set `balance_key` in settings; without it the command is fully offline). Everything else — status,
  sessions, backup, restore, logs — never opens a connection. (`doctor` is in the list above on purpose: one of its
  checks probes npm-registry reachability, and it reports "unreachable" instead of failing when offline.)
- **Writes are protected where it matters.** Before `restore` a backup is always taken first; `update` and `import` try to and continue if that fails; `wipe` only prints the path to delete by hand and neither deletes nor backs up
  and its location is printed, so a failed operation can be rolled back. Not every write is preceded by a backup:
  settings changes (`config-set`, `backup-dir --set`), profile patches and shortcut/PATH edits are not.
- **Your data is not deleted by uninstall.** Uninstall removes the tool's own files. Data removal is a separate,
  explicit action.
- **Backup integrity is checked, not assumed.** A package carries a completion marker written last and a per-file
  hash; a package that was interrupted, or whose contents changed, is refused.
- **Zero third-party runtime dependencies** for the CLI, the installer, the launcher and the dsh plugin. The
  **GUI is built on Avalonia** (a UI framework), which is the one exception — see [docs/PRIVACY.md](docs/PRIVACY.md) and
  [docs/ASSETS.md](docs/ASSETS.md).
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

**V3 is released — `3.0.0` is the first stable version.** The CLI and the Linux tools are complete and verified on
real machines; the GUI is feature-complete for the ten pages above and is being polished. **V2.x remains
maintained** for existing users, but new work goes into V3.

Known limitations, stated plainly:

- The Windows artefacts are **not digitally signed** — no trusted Authenticode publisher (a free code-signing
  application was declined). Verify the `.sha256` and the GPG signature instead.
- `verify_fixes.ps1` matches text; it can confirm a fix is present, not that it is reachable.
- The GUI is built on Avalonia and therefore is not dependency-free, unlike the rest of the project.

---

## License and credits

MIT — see [LICENSE](LICENSE). Privacy details in [docs/PRIVACY.md](docs/PRIVACY.md); asset licensing and the icon's provenance
in [docs/ASSETS.md](docs/ASSETS.md).

`dsh-minato` (みなと, "harbour") was previously published as `DeepSeek-Harness-Toolkit`; the rename is recorded in the
git history. This is an independent project and is not affiliated with DeepSeek.


---

## The bridge plugin (optional) — what it is for, and whether you want it

The toolkit itself is an **independent process**: it does not inject into dsh, and it works **without dsh installed**.
But one fact **only the dsh process itself knows**:

> **how many sessions are running right now** (plus each session's live token and context pressure).

That is **in-process runtime state**, and dsh **never writes it to disk** — so the on-disk projection does not have it,
and the toolkit can only show `unknown`.

| | without the plugin | with the plugin |
|---|---|---|
| session list / tokens / cache hit rate / decode speed | ✅ (from the disk projection) | ✅ (from the disk projection) |
| **the "running" flag** | ❌ `unknown` | ✅ **live** |

**What it does not do** (hard constraints): ❌ no model requests (no token spend) · ❌ does not write dsh state ·
❌ does not read conversation content · ❌ no network · ❌ never blocks (all try/catch — **a broken plugin must not
affect dsh**).

**Should you install it?**

- **Command line only** → **no need**. `unknown` affects one field; nothing else is missing.
- **You use the GUI's session page and want to know whether anything is running** → **worth it** (one command).

```bash
# the repository address works (the root manifest declares dsh.bundle)
dsh plugin --profile web add "https://github.com/sakanamaru/dsh-minato"

# or the local folder (most reliable, no network)
dsh plugin --profile web add "<repo>/plugin/dsh-minato-bridge"
```

> ⚠️ The `desktop` profile is **managed exclusively by dsh's desktop application** and cannot be installed from the
> command line — paste either line into its "add plugin" dialog. Afterwards confirm the profile's
> `cordis.patch.yml` contains a `shio-bridge` entry: without it the plugin **does not load**, and dsh **does not
> report an error** (a pitfall we hit ourselves).

**The choice is yours** — this tool will not decide for you, and will not pretend the plugin is required.

## Why it is still maintained although few people use it

Honestly: this project has **few users**. The reasons it keeps moving, in order of actual weight:

1. **I use it myself** — it solves real problems for me; whether others use it does not change that.
2. **Practice** — doing the whole stack properly (cross-platform CLI + GUI + backup/restore + release chain + CI)
   is the point in itself.
3. **Something built should be finished** — leaving a half-done project to rot is worse than never starting.
4. **Leave something usable for whoever comes next** — if someone hits the same pitfalls, at least there is a
   readable, checkable implementation that does not lie to them.

That is also why the documentation is **wordier than the code**: stating where things are uncertain
(`unknown` is never faked as 0, checks the local gate cannot run are listed as such, unverified fixes are marked)
matters more than one more feature.

## About the AI assistance

**AI assistance was used heavily** in this project. It is written down so you can judge for yourself:

- **v1 script assistance**: SOGR-Momono Dango (QwenPaw / DeepseekAPI-V4-Flash-0731)
- **v2 rewrite and packaging**: DeepSeek DSH (DSH / DeepseekAPI-V4-Flash-0731)
- **v3 and this document**: mostly AI coding agents, reviewed, decided and accepted by the maintainer
- **Icons**: the **new logo is generative-AI output (tool: Kimi)**; the **old logo was produced with ChatGPT
  (OpenAI)**; prompts written by the maintainer

**This does not mean "AI wrote it, so it is untrustworthy", nor "AI wrote it, so it is fine".**
The basis for judgement should be **whether you can check it yourself**: every artifact ships with SHA-256
(`hashes.txt`), and every claim can be re-run with `v3/tests/` and `verify.ps1`.

## Why the repository root looks like this

The v2 tree now lives in [`v2/`](v2): source, tests, the trust anchor, the signing key, the build script, the
manifest and the application manifest all moved together, so the root is down to the files that genuinely have to
be there. What remains is there **because something references it by path**: the CI workflow passes
`/win32icon:icon.ico` and `/resource:logo.png`, GitHub reads the three READMEs and `LICENSE`, and the plugin
installer reads `package.json`. Which file is referenced by what, and what moving it would break, is written down
in [`docs/repo-layout.md`](docs/repo-layout.md); how the move was done - including the path couplings that stopped
the earlier attempts - is in [`docs/v2-migration-plan.md`](docs/v2-migration-plan.md).