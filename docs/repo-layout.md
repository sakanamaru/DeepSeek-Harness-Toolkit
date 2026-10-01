# 仓库结构说明（为什么根目录有那些看起来"该分类"的文件）

> 结论先说：**根目录那圈 v2 文件不能搬** —— 它们被 **v2.x 发布与校验链**按**路径**硬引用，
> 搬动会让发布构建与信任锚失效。这里逐条写清，免得以后有人（包括我自己）又想"顺手整理一下"。

## 被锁住的路径

| 路径 | 谁在引用 | 搬动后果 |
|---|---|---|
| `verify.ps1` | `v3/tests/verify_switchover.ps1` 第 108 行**逐字比对它的路径与内容** | 门槛 `invariant release chain` **红** |
| `dsh_v2.cs` | 门槛第 124 行按此**路径**编译；workflow 的 csc 步骤 | 门槛 `invariant v2.x release build` **红** |
| `src/**` | 同上（v2.8 分层后的源码） | 同上 |
| `icon.ico` | workflow 里 **5 处** `/win32icon:icon.ico`；**发布清单第 231 行** | **v2 发布构建断** |
| `logo.png` | workflow 里 **4 处** `/resource:logo.png` | 同上 |
| `app.manifest` | workflow 的 `/win32manifest:app.manifest` | 同上 |
| `keys/sakanamaru-gpg.asc` | `verify.ps1` 里**写死引用 7 次**（签名公钥） | 校验链断 |
| `build_exe.cmd` | workflow 的发布步骤 | 本地重编译脚本失效 |
| `hashes.txt` | `verify.ps1` 的指纹比对基准 | 校验失去意义 |

## 已经整理好的部分

- `v3/` —— V3 全部（CLI / GUI / 测试 / 工具 / 共享层）
- `plugin/` —— 可选的只读桥接插件
- `docs/` —— 截图与发布说明
- `.github/` —— CI
- 根目录的 `README*.md` / `LICENSE` / `docs/ASSETS.md` / `docs/PRIVACY.md` / `.github/SECURITY.md` / `package.json` —— 门面

## 已经不在库里的

`.gitignore` 里写着 `/DeepSeek Harness Toolkit.exe`、`/Toolkit GUI.exe`、`/Toolkit GUI Standalone.exe`、
`logs/`、`bin/`、`obj/` 等 —— 这些**构建/运行时产物不进仓库**（发布物由 CI 从源码构建）。