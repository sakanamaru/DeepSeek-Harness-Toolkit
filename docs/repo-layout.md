# 仓库结构说明（为什么根目录是这些文件）

> 2026-10-02 起，**v2 整棵树在 `v2/` 里**。根目录只剩 14 个受版本控制的条目，
> 每一个都因为**有东西按路径引用它**才留在那里。这份文档逐条记下"谁引用谁、搬了会坏什么"，
> 免得以后有人（包括我自己）又想"顺手整理一下"。

## 现在的布局

```
根/
  v2/          v2.x 全部：dsh_v2.cs · gui_v2.cs · verify.ps1 · hashes.txt · build_exe.cmd
               app.manifest · DeepSeekHarnessToolkit.Core.csproj · .dsh_launcher_root
               src/ · tests/ · keys/
  v3/          V3 全部（CLI / GUI / 测试 / 工具 / 共享层）
  plugin/      可选的只读桥接插件
  docs/        截图与发布说明（含本文件）
  .github/     CI（workflows/build-release.yml）与 SECURITY.md
  README.md · README_zh-CN.md · README_ja.md · LICENSE · icon.ico · logo.png
  package.json · .gitignore · .gitattributes
```

## 根目录为什么不能更空

| 路径 | 谁在引用 | 搬动后果 |
|---|---|---|
| `icon.ico` | workflow 里 **5 处** `/win32icon:icon.ico` | v2 发布构建断 |
| `logo.png` | workflow 里 **4 处** `/resource:logo.png` | 内嵌 logo 丢失 |
| `README*.md` / `LICENSE` | GitHub 语言识别与门面；README 里链接 `docs/`、`v2/`、`v3/` | 首页与语言标签坏 |
| `package.json` | **插件 URL 安装**：`github:sakanamaru/dsh-minato` 靠它把仓库当插件装 | 桥接插件装不上 |
| `.github/workflows/` | GitHub 只从根读 workflow | CI 消失 |
| `.gitignore` / `.gitattributes` | 只对根生效（`*.sh eol=lf`、`*.ps1` 保 BOM） | 脚本在 Linux 上换行/BOM 坏 |

## `v2/` 内部谁引用谁

| 路径 | 谁在引用 | 说明 |
|---|---|---|
| `v2/verify.ps1` | `v3/tests/verify_switchover.ps1` 按**内容**比对（blob 对 blob） | 信任锚：改了门槛就红 |
| `v2/keys/sakanamaru-gpg.asc` | `verify.ps1` 按**脚本同目录**找 `keys\sakanamaru-gpg.asc` | 所以 `keys/` 必须跟着 `verify.ps1` 一起走 |
| `v2/dsh_v2.cs` + `v2/src/**` | workflow 的 csc 步骤、`build_exe.cmd`、门槛 `invariant v2.x release build` | 全部改用**绝对路径**（见下） |
| `v2/app.manifest` | workflow 的 `/win32manifest:` | GUI 应用清单 |
| `v2/hashes.txt` | CI 生成并 GPG 签名（`v2/hashes.txt.asc`）后作为**发布资产**上传 | `gh` 取 basename → 资产名仍是 `hashes.txt` |
| `v2/tests/unit_tests.cs` | 读 `tests/fixtures/*.yaml`（布局自适应探测） | 见下 |
| `v2/tests/gui_logic_tests.cs` | `SrcPath()` 向上 + `v2\` 子目录探测 `gui_v2.cs` | 见下 |
| `v2/tests/integration.ps1` | `$RepoRoot` 默认取脚本上级目录，并自动识别两种布局 | 见下 |

## 这次搬迁踩到的坑（都留了自适应代码，不再依赖布局）

1. **csc 不吃相对源码路径**：`v2/dsh_v2.cs` 会被解析成仓库根的 `dsh_v2.cs` → `CS1504`。
   → workflow 与门槛一律用**绝对路径**（`"$PWD\v2\..."`、`Join-Path`）。
2. **信任锚的比对方式**：`git diff --name-only origin/main -- verify.ps1` 在搬家后必然把"移动"报成
   "改动"，永远红。→ 改成按**内容**比对，且**两边各自解析路径**
   （`origin/main:verify.ps1` ↔ `v2/verify.ps1`；合并进 main 之后自动切到 `origin/main:v2/verify.ps1`）。
3. **测试按相对路径读文件**：`unit_tests.cs` 的 `tests/fixtures`、`gui_logic_tests.cs` 的 `SrcPath()`。
   后者更隐蔽 —— 找不到 `gui_v2.cs` 时它只报一条 FAIL，而 **i18n 强制检查整块会静默跳过**
   （修好后从 46 项变成 52 项）。
4. **发布包布局 ≠ 仓库布局**：workflow 的 `$files` / `$items` 既是仓库路径、又是包内名字。
   → 取文件用 `v2\...`，写进清单 / 打进 zip 时**剥掉 `v2\` 前缀**。
   （`Compress-Archive` 本身只取叶子名，所以 zip 条目自动是平铺的；清单需要显式剥前缀。）
5. **本机 `pwsh` 与 BOM**：本地只有 Windows PowerShell 5.1，任何**含中文的临时脚本都必须带 UTF-8 BOM**，
   否则 5.1 按 ANSI 读 → 锚点匹配不上（这次踩了两次）。

## 已经不在库里的

`.gitignore` 里写着 `/DeepSeek Harness Toolkit.exe`、`/Toolkit GUI.exe`、`/Toolkit GUI Standalone.exe`、
`logs/`、`bin/`、`obj/` 等 —— 这些**构建/运行时产物不进仓库**（发布物由 CI 从源码构建）。

⚠️ **例外，务必保留**：`v2/.dsh_launcher_root` **必须留在版本库里**（2026-10 修正）。
它是 v2 发布包**随包分发**的"防误删"标记 —— `src/Cli/Program.Cli.cs` 严格校验它（内容含产品名）
才允许清除数据，`src/Platform/Windows/Program.Platform.cs` 只读不写、程序永不自行补建；
CI 的发布清单与 `upload-package.zip` 都按路径取它。曾经取消跟踪 → `build` job 会在 `Get-FileHash`
处直接失败，且发布包会丢掉这道闸门。
