# V3 跨平台 GUI（Avalonia）—— 骨架

> 状态：**骨架 + 会话/token 面板页**（2026-09-28 起）。这是 V3.0 方案 §7.5 拍板的"跨平台 GUI 重做"的第一步：
> 先立起可编译、可运行的工程骨架，再逐页把 CLI 标记行接成界面。
> **它现在长什么样，要你自己跑一遍看**——界面效果我无法替你验收。

## 怎么跑

```bash
# 需要 .NET SDK 8
dotnet run --project v3/gui/Dsht.Gui.Avalonia
```

GUI 需要能找到**工具箱 CLI**，按这个顺序找：
1. 环境变量 `DSHT_CLI`（指向 exe 全路径）
2. GUI 同目录的 `dsht.exe` / `dsht_v3.exe` / `dsh-shio.exe`

最省事的做法：把 V3 CLI 编译到 GUI 的输出目录旁，或直接 `export DSHT_CLI=<路径>`。

```bash
# 例：先编 CLI（本地无需 SDK 时也可用 csc），再指定给它
export DSHT_CLI="$(pwd)/dsht_v3.exe"      # Windows PowerShell: $env:DSHT_CLI="D:\...\dsht_v3.exe"
dotnet run --project v3/gui/Dsht.Gui.Avalonia
```

## 现在的骨架里有什么

- 一个窗口：顶部非官方声明 · 左侧页面导航 · 右侧 CLI 输出 · 底部数据来源说明
- 七个导航项对应 CLI 命令：`status --detail` / `sessions` / `profiles` / `backup-list --detail` / `doctor` / `config-get` / `describe`
- **GUI 不引用核心程序集**：它只运行 CLI 并原样展示标记行 —— 这是 §7.3"呈现层与核心隔开"的兑现，也是"换 UI 不用改核心"的前提

## 还没做的（诚实清单）

| 项 | 说明 |
|---|---|
| 真正的界面 | 现在是把标记行贴在只读文本框里；**没有卡片、表格、状态灯、图表** |
| 会话/token 面板页 | ✅ **已做**：汇总条（会话数/非空/运行中/token/缓存命中/解码）+ 会话列表（短 id · 状态 · 轮次 · 最后活动 · 输入/输出 token · 缓存命中率 · 解码速度 · 首 token · 上下文压力）+ 数据来源说明 |
| i18n | 骨架里是硬编码中英混排；**没有** L10N 机制（v2.x WinForms 那套 i18n 强制检查还没搬过来） |
| 逻辑测试 | ✅ 有：`v3/gui/Dsht.Gui.LogicTests`（21 项，**不依赖 Avalonia**，CI 里 windows+ubuntu 都跑）——覆盖标记行解析、unknown 语义、脏数据容错 |
| 发布形态 | **未定**：self-contained（60–90 MB，免装运行时）还是 framework-dependent（小，要 .NET 8）——需要拍板 |
| CI | 还没加进 CI（避免在骨架期动发布链）；等界面稳定后再加"编译 + 逻辑测试"守卫 |
| 与 WinForms 的关系 | 并行存在；v2.x 发布线仍是 WinForms 版，成熟前不替换 |

## 与 v2.x WinForms 版的关系

| | v2.x WinForms | V3 Avalonia（本目录） |
|---|---|---|
| 平台 | 仅 Windows | Windows / Linux / macOS |
| 编译 | `csc` 单文件 exe（零依赖） | net8 + NuGet（Avalonia） |
| 状态 | **已发布**（v2.7.3） | **骨架** |
| 复用 | — | 复用同一套 CLI 标记行契约与领域逻辑 |
