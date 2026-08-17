# Security Policy

DeepSeek Harness Toolkit is an **unofficial** Windows toolbox for DeepSeek Harness (dsh).
It handles your dsh data directory (typically `~/.dsh`), files stored inside it, and can
delete data — so treat it as a data-management tool, not just a launcher.

## Reporting a Vulnerability

If you discover a security issue — especially anything involving:

- data deletion / wipe behavior,
- backup / restore / import path handling,
- or exposure of credentials stored inside the dsh data directory,

please **report it privately** instead of opening a public issue:

- Use GitHub's private report flow: https://github.com/sakanamaru/DeepSeek-Harness-Toolkit/security/advisories/new
- Or contact the maintainer: https://github.com/sakanamaru

Please include:

1. A short description of the issue.
2. The release tag / commit you tested.
3. Steps to reproduce (paths, OS version, what ran).
4. Any relevant output or proof-of-concept.

## Scope

| In scope | Out of scope |
|---|---|
| This repository's latest release tag | **DeepSeek Harness itself** (https://github.com/deepseek-ai/dsh) |
| The toolkit's own source (`dsh_v2.cs`) and CI pipeline | Node.js / npm / third-party packages |
| Backup / restore / wipe data handling | |

## Data-safety notes

- The toolkit **never deletes your dsh data without explicit interaction**: wipe requires a
  two-step confirmation (today's date + `yes`), the dsh web service must be stopped, and an
  automatic pre-backup is made before wiping, restoring or importing.
- Manual backups (no suffix) are kept forever; only auto/protection backups
  (`-auto`, `-pre-*`) are cleaned by the retention policy.
- Backups are plain files/directories — keep them somewhere you trust.
- This is an unofficial project and is **not affiliated with DeepSeek**.