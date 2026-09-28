# 给 GUI 重构者的交接说明（V3 跨平台 GUI · Avalonia）

> 这份文档是给**接手改界面的人/模型**看的。目标：**只改外观与交互，不要弄坏数据契约、测试与安全边界**。
> 当前代码是**本地提交、尚未推送到 GitHub**（远端 `v3-linux` 停在更早的提交）——请在这份代码上直接改，别丢历史。

## 1. 位置与命令

```
源码：<repo>\v3\gui\Dsht.Gui.Avalonia\          ← GUI 本体（Avalonia 11.2.1，net8.0）
测试：<repo>\v3\gui\Dsht.Gui.LogicTests\        ← 呈现层逻辑测试（48 项，**不依赖 Avalonia**，无图形环境也能跑）
CLI ：<repo>\v3\src\Dsht.Cli\                   ← GUI 的数据来源（只通过命令行标记行交互）
```
（`<repo>` = `D:\dsh-workspace\技术\DSHToolkit\09-源码仓库\repo`）

```powershell
# 编译 / 运行（本机 dotnet 在 %USERPROFILE%\.dotnet，PATH 里那个只有运行时）
$dn = "$env:USERPROFILE\.dotnet\dotnet.exe"
& $dn build  v3\gui\Dsht.Gui.Avalonia\Dsht.Gui.Avalonia.csproj -c Release
& $dn run    --project v3\gui\Dsht.Gui.LogicTests -c Release      # 必须 48/48 通过

# 自己发布一个免 SDK 的自包含包（方便肉眼验收）
& $dn publish v3\gui\Dsht.Gui.Avalonia\Dsht.Gui.Avalonia.csproj -c Release -r win-x64 --self-contained true -o <输出目录>
# 记得把 dsht_v3.exe（CLI）和 dsh-shio.exe（核心，供 profilepatch/profilecheck）放到同目录
```

## 2. 文件地图

| 文件 | 作用 | 改动自由度 |
|---|---|---|
| `MainWindow.axaml` / `.axaml.cs` | 外壳宿主：顶部两条切换栏（布局/风格）+ 导航状态 + 取数据 | 布局可自由改；**取数与导航回调的语义别改** |
| `Shells\Shells.cs` | **五种布局外壳** + 所有内容构建器（KPI/会话卡/profile 卡/状态台/说明区） | **主要美化战场**，随便重构 |
| `ViewModels\SessionRowVm.cs` | `Palette`（配色与四个风格 demo）+ 会话行视图模型 | 配色可自由改；`Palette.Apply(kind)` 的四个风格语义保留 |
| `Markers\SessionsMarkers.cs` | 解析 `sessions` 标记行（会话/token/缓存/速度） | **解析契约不能改**（见 §3） |
| `Markers\ProfilesMarkers.cs` | 解析 `profiles` 标记行（形态/插件/版本/已隔离） | 同上 |
| `Markers\StatusMarkers.cs` | 解析 `status --detail` 标记行 | 同上 |
| `Markers\SummaryMarkers.cs` | 解析 `doctor` / `backup-list` 摘要 | 同上（**这两个还没有单测，欢迎补**） |

## 3. 必须保留的契约与不变量（改界面时最容易踩坏的东西）

1. **GUI 不引用核心程序集**：界面只通过运行 CLI 并解析**标记行**拿数据（这是刻意的解耦，换 UI 不动核心）。
2. **标记行格式是契约**：解析器的字段名/顺序与 CLI 打印一一对应（见 `<repo>\v3\README.md` 的命令表）。改解析器 = 改契约，必须同步改 CLI 与测试。
3. **`unknown` 不许当 0**：缺失字段一律显示 `unknown` / 空，**不要用 0 冒充**（这是本项目的诚实原则）。
4. **写操作必须两次确认**：插件页的「隔离」按钮第一次点击只是变成「再点一次确认隔离」，第二次才真调 `profilepatch`（它会先备份再改、失败回滚）。**不要把这一步简化成一次点击**。
5. **诚实边界文案要留着**：状态页的"只依据端口/进程"、插件页的"这是配置形态不是运行形态"、面板的"未装桥接插件所以没有『正在运行』这一项"——这些是产品态度，不是装饰。
6. **非官方声明**：顶部必须保留"非官方工具，与 DeepSeek 官方无关"。

## 4. 可以自由发挥的地方

配色与视觉体系 · 布局与栅格 · 图标（已在用 `FluentIcons.Avalonia`）· 动画（已引入 `Avalonia.Xaml.Behaviors`）· 圆角/阴影/间距/字体层级 · 空态/加载态/错误态设计 · 把硬编码的 C# 构建改成 XAML + MVVM（更符合 Avalonia 习惯）。

## 5. 已知待办（欢迎一起做）

- `Markers\SummaryMarkers.cs` 的两个解析器**补单测**（现有 48 项里没有它们）
- 把配色从 `Palette`（C# 静态类）搬进 `Palette.axaml` + 各控件 `Styles\*.axaml`（参考 Hollow 的做法）
- i18n（参考 Hollow 用的 `Antelcat.I18N.Avalonia`）
- `Avalonia.Controls.DataGrid` 表格视图（146 条会话用真表格更清楚，可做"卡片/表格"切换）
- 「备份 / 体检 / 配置 / 说明」四页仍是标记行原文，照概览页的做法图形化

## 6. 参考项目与许可边界（重要）

本地已克隆两个参考（在 `06-数据与杂项\参考-GUI仓库\`）：
- **March7thAssistant**（PySide6 + qfluentwidgets，**GPL-3.0**）→ **只可借鉴设计方向，代码/图标/字体/截图一律不能抄**（会污染我们 MIT）。
- **Hollow**（**Avalonia 11.2.1，MIT**，与我们同栈）→ 它的做法（Palette 资源字典 + 每控件 Styles + FluentIcons + Antelcat.I18N + DataGrid）值得照做，但**仍建议自己写**，不要复制文件。

## 7. 验收方式

1. `dotnet build` 0 警告 0 错误；`Dsht.Gui.LogicTests` **48/48 通过**（不许为了好看而删测试）
2. 发布自包含包，肉眼过一遍：五种布局 × 四个风格都能切、146 条会话能显示、状态/概览/插件页有数据
3. 交付时给**截图**（我看不到界面，只能靠截图验收）