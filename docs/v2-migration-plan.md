# v2 迁移记录：把 v2 整棵树搬进 `v2/`（2026-10-02 完成）

> **结果**：根目录受版本控制的条目 **24 → 14**，13 项就绪度门槛全绿，CI 全绿，
> 且**不影响任何已发布产物**（v2 的发布包自带 `verify.ps1`，其公钥回退按不可变的 **tag** 从 raw 下载）。
>
> 这份文档记的是**过程**：前三次尝试分别死在一处新的路径耦合上。
> 留着它是因为"搬 v2"看起来是纯机械操作，实际上它**跨越了发布链上每一处路径假设**。

## 一、为什么可以安全地搬

- v2 的每个已发布包**自带** `verify.ps1`（在 `upload-package.zip` 里）。
- 它取维护者公钥的逻辑是：先找**脚本同目录**的 `keys/sakanamaru-gpg.asc`，找不到就按 `-Tag`
  从 `https://raw.githubusercontent.com/<owner>/<repo>/<Tag>/keys/sakanamaru-gpg.asc` 下载。
- **tag 不可变** → 仓库后续怎么搬，都不会破坏老版本的校验链 ✓

→ 所以 `verify.ps1` 与 `keys/` 一起搬进 `v2/` 是安全的（`verify.ps1` 按脚本目录找 `keys\`，
两者必须**同进同出**）。

## 二、搬了什么（最终清单）

```
根 → v2/
  dsh_v2.cs · gui_v2.cs · verify.ps1 · hashes.txt · build_exe.cmd · app.manifest
  DeepSeekHarnessToolkit.Core.csproj · .dsh_launcher_root
  src/ → v2/src/ · tests/ → v2/tests/ · keys/ → v2/keys/

✗ 留在根：logo.png · icon.ico
  —— README 的 logo 用它们，而 workflow 的 /win32icon:icon.ico 与 /resource:logo.png
     按**根路径**引用；搬了会同时断 README 与 v2 构建。
```

根目录剩下的 14 个受版本控制条目及其引用关系见 [`repo-layout.md`](repo-layout.md)。

## 三、前三次尝试各死在哪（每次都是一处**新的**路径耦合）

| 次 | 卡在哪 | 根因 | 修法 |
|---|---|---|---|
| 1 | 门槛 `invariant release chain` | `git diff --name-only origin/main -- verify.ps1`：路径一变就把"移动"报成"改动" → 永远红 | 改成**按内容**比对，且**两边各自解析路径** |
| 2 | 门槛 `invariant v2.x release build` | csc 把相对路径 `v2/dsh_v2.cs` 解析成仓库根的 `dsh_v2.cs` → `CS1504` | 一律用**绝对路径**（`Join-Path` / `"$PWD\v2\..."`） |
| 3 | CI `unit + integration tests`（294/318） | `unit_tests.cs` 按相对路径读 `tests/fixtures/` → 搬走后读到空文件 | 夹具路径改成**候选探测**（两种布局都认） |

**共同点**：三次都不是"替换表漏了"，而是**某处代码/脚本对"我在哪"有隐含假设**。

## 四、第四次一次做成，靠的是三件事

### 1. 先做**穷尽式侦察**，而不是照抄替换表

把"谁会按路径引用 v2 文件"全部找出来（`grep` 覆盖 workflow / 门槛 / 测试 / 构建脚本 / 文档），
结果发现替换表里**没有**的耦合：

- `v2/tests/gui_logic_tests.cs` 的 `SrcPath()`：只从 CWD **向上**找 `gui_v2.cs` → 搬进子目录后永远找不到。
  后果不只是 1 条 FAIL：**整个 i18n 强制检查块被静默跳过**（46 项 vs 修好后的 52 项）。
- `v2/tests/integration.ps1`：`$RepoRoot` 默认取**脚本上级目录** → 自己就适应了新布局，
  但 workflow 传的是仓库根 → 必须一起改（现已做成两种布局都认）。
- `v2/build_exe.cmd`：写死 `dsh_v2.cs` / `src` / `icon.ico` 的相对路径。
- `.gitignore` 里的 `.dsh_launcher_root`。

### 2. 把耦合改成**布局无关**，而不是"改成新路径"

能自适应的就不写死：夹具候选探测、`SrcPath()` 增加 `v2\` 探测、`integration.ps1` 自动识别布局、
门槛的 `$v2root` 自动判断。这样**迁移前后都能跑**，回滚也不会再踩一遍。

### 3. **在本地复刻 CI**（这是最关键的改进）

第三次失败暴露的真正问题是：**本地门槛看不到 v2 的单元测试** → 只有推上去才知道红。
所以这次写了一个本地复刻脚本，逐条照抄 workflow 的命令：
单元测试 → `dotnet build` → GUI 变体编译 → GUI 逻辑测试 → 集成测试 → 发布编译 → 清单/zip 生成。
**它在推送前就抓到了 `SrcPath()` 那个问题**（45/46），以及发布清单的名字错位。

## 五、顺带修掉的两个**既有**缺陷

这两个都不是搬迁引入的，是这次"必须逐行重写那几行"时被逼出来的：

1. **`.dsh_launcher_root` 曾被取消跟踪**（上一个提交），而发布清单与 `upload-package.zip` 仍按路径取它。
   → `build` job（只在 `v2*` tag / `workflow_dispatch` 跑）会在 `Get-FileHash` 处**直接失败**，
   且发布包会**丢掉卸载器的"防误删"闸门**（`Program.Cli.cs` 严格校验它才允许清除数据）。
   当时的理由是"运行时产物"，但代码事实相反：`Program.Platform.cs` **只读不写**，
   程序"永不自行补建"，它**只随包分发**。→ 已恢复跟踪。
2. **发布清单里的名字与包内实际条目对不上**：`Compress-Archive` 只取**叶子名**，
   所以 `.github/SECURITY.md` 在 zip 里叫 `SECURITY.md`，而清单写的是 `.github/SECURITY.md`。
   → 生成清单时把 `v2\` 与 `.github\` 前缀都剥掉，清单从此能被逐条核对。

## 六、发布包布局 ≠ 仓库布局（最容易踩的一处）

workflow 的 `$files`（清单）与 `$items`（zip）**既是仓库路径、又是包内名字**：

- 取文件时用 `v2\...`；
- `Compress-Archive` 对**文件**只取叶子名、对**目录**取目录名 → zip 条目自动是平铺的 ✓
- 但**清单是手写字符串** → 必须显式剥掉 `v2\` 前缀，否则清单会写 `v2\dsh_v2.cs`，
  而包里是 `dsh_v2.cs` ✗

已加断言：清单里每个名字都必须能在 zip 里找到（本地复刻脚本会跑这条）。

## 七、验证

1. 本地复刻 CI 全绿（单元 318/318、GUI 逻辑 52/52、集成 25 PASS / 0 FAIL、三个 exe 编译通过、
   清单 28 行全部平铺、zip 31 个条目全部平铺且与清单一一对应）。
2. 13 项就绪度门槛 → **`== 全部就绪 ==`**。
3. `v2.7.3` 的 `verify.ps1` 仍从 `.../v2.7.3/keys/...` 取公钥（tag 不变 ✓）。
4. 根目录条目数：**24 → 14**。

## 八、回滚

`git revert <commit>`（全部是 `git mv` + 文本替换，没有删除内容）。

## 九、遗留的本地/CI 差异（如实记录）

- 本机只有 **Windows PowerShell 5.1**，没有 `pwsh`；CI 用的是 `pwsh` 7。
  含中文的临时脚本**必须带 UTF-8 BOM**，否则 5.1 按 ANSI 读 → 锚点匹配不上。
- 本机 `C:\Program Files\dotnet` **没有 SDK**，SDK 在 `%USERPROFILE%\.dotnet` → 复刻脚本用绝对路径。
- 本地复刻**不等于** CI：Linux 平台实现、5 个 shell 验证脚本、打包 job 都只在 CI 跑。
