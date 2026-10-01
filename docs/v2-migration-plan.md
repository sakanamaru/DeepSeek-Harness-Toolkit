# V2 冻结与迁移方案（下一轮执行）

## 一、V2 已冻结

**v2.x 不再发布新版本。** 最后一次发布是 `v2.7.3`。

**为什么可以冻结**：
- v2 的每个已发布包**自带** `verify.ps1`（在 `upload-package.zip` 里）
- 它取维护者公钥的逻辑是：**先找脚本同目录的 `keys/sakanamaru-gpg.asc`，找不到就按 `-Tag` 从 raw 下载**
  （`https://raw.githubusercontent.com/<owner>/<repo>/<Tag>/keys/sakanamaru-gpg.asc`）
- **tag 是不变的** → 所以**仓库后续怎么搬，都不会破坏老版本的校验** ✓✓

→ 结论：**V2 冻结后，把 v2 那圈整体搬进 `v2/` 是安全的**，不会伤到任何已有用户。

## 二、目标

根目录条目从 **25 → 约 10**（文件列表一屏放得下）。

## 三、搬什么（**下一步执行时的准确清单**）

```
根 → v2/
  dsh_v2.cs · gui_v2.cs · verify.ps1 · hashes.txt · build_exe.cmd · app.manifest
  DeepSeekHarnessToolkit.Core.csproj · .dsh_launcher_root
  src/ → v2/src/ · tests/ → v2/tests/ · keys/ → v2/keys/

✗ **不要搬**：logo.png · icon.ico
  —— README 的 logo 要用它们 ✓ 而 workflow 里 `/win32icon:icon.ico` 与 `/resource:logo.png`
     按**根路径**引用 ✓ → 搬了会同时断 README 与 v2 构建 ✓（我第一次就搬错了 ✗ 中途搬回 ✓）
```

## 四、必须同步改的地方

### 1. `.github/workflows/build-release.yml`（约 25 处）

已经验证过的替换表（第一次执行时逐条成功过 ✓）：

| 原 | 新 | 出现次数 |
|---|---|---|
| `'dsh_v2.cs'` | `'v2/dsh_v2.cs'` | 5 |
| `Get-ChildItem src -Recurse` | `Get-ChildItem v2/src -Recurse` | 4 |
| `tests\unit_tests.cs` | `v2/tests/unit_tests.cs` | 1 |
| `tests\gui_logic_tests.cs` | `v2/tests/gui_logic_tests.cs` | 1 |
| `.\tests\integration.ps1` | `.\v2\tests\integration.ps1` | 1 |
| `gui_v2.cs` | `v2/gui_v2.cs` | 7 |
| `/win32manifest:app.manifest` | `/win32manifest:v2/app.manifest` | 4 |
| `'build_exe.cmd'` | `'v2/build_exe.cmd'` | 2 |
| `DeepSeekHarnessToolkit.Core.csproj` | `v2/DeepSeekHarnessToolkit.Core.csproj` | 1 |
| `Select-String -Path dsh_v2.cs` | `Select-String -Path v2/dsh_v2.cs` | 1 |
| `Get-Content hashes.txt` | `Get-Content v2/hashes.txt` | 1 |
| `git add hashes.txt` | `git add v2/hashes.txt` | 1 |
| `--detach-sign --armor hashes.txt` | `--detach-sign --armor v2/hashes.txt` | 1 |
| `--verify hashes.txt.asc hashes.txt` | `--verify v2/hashes.txt.asc v2/hashes.txt` | 1 |
| `ls -l hashes.txt.asc` | `ls -l v2/hashes.txt.asc` | 1 |
| `Upload "hashes.txt" $tag` | `Upload "v2/hashes.txt" $tag` | 1 |
| `Upload "hashes.txt.asc" $tag` | `Upload "v2/hashes.txt.asc" $tag` | 1 |

**还要检查**（第一次没改到的）：
- 第 231 行的发布清单 `$files = @(...)` 里的 `dsh_v2.cs` / `build_exe.cmd`
- 第 338 行的 `$items = @(...)`
- 第 320-322 行的说明文字
- `Join-Path $PWD 'hashes.txt'` → `'v2/hashes.txt'`

### 2. `v3/tests/verify_switchover.ps1`（**两条不变量**，第一次卡在这里）

**① `invariant release chain`（第 108 行）**

```powershell
# ✗ 第一次改错：origin/main 上没有 v2/verify.ps1 → diff 永远显示"新增" → 必然红
$chainChanged = (& git -C $Repo diff --name-only origin/main -- v2/verify.ps1 ...)

# ✓ 正确：改成**内容比对**（信任锚的内容没变才算通过）
$chainChanged = (& git -C $Repo diff origin/main:verify.ps1 v2/verify.ps1 ...)
```

⚠️ **第一次执行时这一行"改了但复核仍是旧文本"** ✗ —— 下次务必**改完立刻读回该行确认** ✓

**② `invariant v2.x release build`（第 124 行）**

```powershell
# 第一次复核时这行**已经是对的**：
$v2src = @('v2/dsh_v2.cs') + @(Get-ChildItem (Join-Path $Repo 'v2/src') -Recurse ...)
# 但报错仍指向 repo\dsh_v2.cs ✗ → 下次要查清：
#   · 是否有**第二处**赋值（全文搜 `dsh_v2.cs`）
#   · 或读文件时被 BOM/编码影响
```

### 3. 文档与注释里的路径说明（约 20 处）

- `v3/README.md`（5 处：`dsh_v2.cs` / `verify.ps1` / 16 项清单的说明）
- `docs/repo-layout.md`（8 处：那张"谁引用谁"的表要整体更新）
- `docs/CHANGELOG.md`（16 处：历史记录 —— **建议不改** ✓ 它是历史事实 ✓ 只在开头加一句"v2 已移入 v2/"）
- `docs/LINUX-TEST.md`（2 处）
- `README*.md`（各 1 处：`verify.ps1` → `v2/verify.ps1`）

⚠️ **不要改**：`v3/tools/installer.cs` / `launcher.cs` / `install.sh` / `verify-linux.sh` 里
那几十处 `hashes.txt` —— 它们指的是**发布包内**的清单文件 ✓ **与仓库根路径无关** ✓

## 五、验证（做完必须全过）

1. `pwsh -File v3/tests/verify_switchover.ps1` → **`== 全部就绪 ==`**
2. **`workflow_dispatch` 手动跑一遍 v2 构建**（`unit + integration tests` job）→ 全绿
3. 老版本自足性复核：`v2.7.3` 的 `verify.ps1` 仍从 `.../v2.7.3/keys/...` 取公钥（tag 不变 ✓）
4. 根目录条目数确认（目标约 10）

## 六、回滚

`git revert <commit>`（全部是 `git mv` + 文本替换 ✓ 没有删除内容 ✓）