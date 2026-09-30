using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using Dsht.Gui.Avalonia.Markers;
using Dsht.Gui.Avalonia.ViewModels;

namespace Dsht.Gui.Avalonia
{
    /// <summary>主窗口：取数据 + 管导航状态（主菜单 / 子菜单 / 布局），界面由 Shells 构建。
    /// 纪律：**GUI 只是呈现适配器** —— 不引用核心程序集，只运行 CLI 并解析标记行（V3.0 方案 §7.3）。
    /// 设计方向参考了 March7thAssistant（GPL-3.0）的做法，**未复制其任何代码、图标、字体或图片资源**。</summary>
    public partial class MainWindow : Window
    {
        /// <summary>主菜单（侧栏一级）。</summary>
        public static readonly string[] NavItems = new string[] { "概览", "看板", "会话与 Token", "形态与插件", "备份", "体检", "设置", "说明" };
        /// <summary>主菜单图标（FluentIcons，编译期检查）。</summary>
        public static readonly FluentIcons.Common.Symbol[] NavIcons = new FluentIcons.Common.Symbol[]
        {
            FluentIcons.Common.Symbol.Home, FluentIcons.Common.Symbol.DataBarVertical, FluentIcons.Common.Symbol.ChatMultiple,
            FluentIcons.Common.Symbol.PuzzlePiece, FluentIcons.Common.Symbol.Archive, FluentIcons.Common.Symbol.Shield,
            FluentIcons.Common.Symbol.Settings, FluentIcons.Common.Symbol.Question
        };
        private static readonly string[][] NavCli = new string[][]
        {
            new string[] { "status", "--detail" },
            new string[] { "status", "--detail" },
            new string[] { "sessions" },
            new string[] { "profiles" },
            new string[] { "backup-list", "--detail" },
            new string[] { "doctor" },
            new string[] { "config-get" },
            new string[] { "describe" }
        };
        private static readonly string[][] NavSubs = new string[][]
        {
            new string[] { "概览", "原始输出" },
            new string[] { "指标", "图表" },
            new string[] { "整体", "父会话", "子代理", "统计" },   // 用户要求的三视图 + 原有统计 ✓
            new string[] { "原始输出" },
            new string[] { "原始输出" },
            new string[] { "原始输出" },
            new string[] { "原始输出" },
            new string[] { "原始输出" }
        };
        private static readonly string[][] NavDesc = new string[][]
        {
            new string[] { "一键启动/停止 dsh，以及 token 消耗、缓存命中、解码速度、会话数。", "运行时长、进程 PID、启动时间与原始标记行。" },
            new string[] { "关键指标（KPI）总览，以及一键启动/停止等操作的回执。", "手绘图表：近 14 天新增会话、缓存命中率分布。" },
            new string[] { "逐条会话：标题、token、缓存命中率、解码速度、上下文压力（排序用工具栏的下拉）。", "汇总统计：总量、命中率、速度，以及最耗 token 的会话排行。" },
            new string[] { "每个 profile 启用了哪个形态（web/headless/acp）以及装了哪些插件（含第三方）。" },
            new string[] { "备份清单：每个备份的时间、范围与大小。" },
            new string[] { "体检：配置、日志、网络与安装完整性检查。" },
            new string[] { "当前配置项（脱敏后）。" },
            new string[] { "工具箱对当前安装的判断与依据。" }
        };

        private SessionsSnapshot _data;
        private ProfilesSnapshot _profiles;
        private StatusSnapshot _status;
        private BackupSummary _backups;
        private List<BackupItem> _backupItems = new List<BackupItem>();
        private List<ConfigItem> _config = new List<ConfigItem>();
        public List<BackupItem> BackupItems { get { return _backupItems; } }
        public List<ConfigItem> Config { get { return _config; } }
        /// <summary>待二次确认的破坏性操作（删除备份）；空=没有待确认项。</summary>
        public string PendingDelete = "";
        private List<SessionRowVm> _rows = new List<SessionRowVm>();
        private int _shell = Shells.Shells.Hybrid;     // 默认：混合式（主菜单 + 子菜单）
        private int _filter = SessionsView.FilterAll;
        private string _rawOutput = "";
        private int _mainSection = 1;                  // 默认停在「会话与 Token」
        private int _subTab;

        public StackPanel DetailHost;

        public MainWindow()
        {
            InitializeComponent();
            SetWindowIcon();
            BuildWindowChrome();
            InitChrome();
            for (int i = 0; i < 5; i++) BindShell(i);
            for (int i = 0; i < 4; i++) BindStyle(i);
            Refresh();
        }

        private readonly List<Button> _windowButtons = new List<Button>();

        /// <summary>自绘窗口标题栏（无边框窗口）：整条顶栏可拖动（落在按钮上的按下不算）、双击最大化/还原、
        /// 右侧三个窗口按钮。颜色由 ApplyChrome 按 Palette 刷，风格切换时自动跟随。</summary>
        private void BuildWindowChrome()
        {
            Border bar = this.FindControl<Border>("AppBar");
            StackPanel right = this.FindControl<StackPanel>("AppBarRight");
            if (bar != null)
            {
                bar.PointerPressed += delegate(object s, PointerPressedEventArgs e)
                {
                    if (IsFromButton(e.Source as Control)) return;
                    try { if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e); }
                    catch { /* 某些平台/状态下不允许拖动：忽略，不要因此崩 */ }
                };
                bar.DoubleTapped += delegate(object s, TappedEventArgs e) { ToggleMaximize(); };
            }
            if (right != null)
            {
                right.Children.Add(MakeWindowButton("—", delegate { WindowState = WindowState.Minimized; }, false, "最小化"));
                right.Children.Add(MakeWindowButton("□", delegate { ToggleMaximize(); }, false, "最大化 / 还原"));
                right.Children.Add(MakeWindowButton("✕", delegate { Close(); }, true, "关闭"));
            }
            RecolorWindowButtons();
        }

        /// <summary>按下是否来自某个按钮（含其后代）——是的话不要开始拖动窗口。</summary>
        private static bool IsFromButton(Control c)
        {
            Control cur = c;
            while (cur != null)
            {
                if (cur is Button) return true;
                cur = cur.Parent as Control;
            }
            return false;
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private Button MakeWindowButton(string glyph, Action onClick, bool danger, string tip)
        {
            Button b = new Button
            {
                Content = glyph,
                Width = 34,
                Height = 26,
                Padding = new Thickness(0),
                FontSize = 12,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(b, tip);
            b.Click += delegate { onClick(); };
            _windowButtons.Add(b);
            return b;
        }

        /// <summary>窗口按钮随主题重上色（关闭键用警示色，其余用次要文字色）。</summary>
        private void RecolorWindowButtons()
        {
            for (int i = 0; i < _windowButtons.Count; i++)
                _windowButtons[i].Foreground = i == _windowButtons.Count - 1 ? Palette.TextDim : Palette.TextDim;
        }
        // ---------------- 备份 / 设置 的操作（都走 CLI，异步，不阻塞界面） ----------------

        public void CreateBackup() { RunCliAction("backup", "立即备份"); }

        public void ExportBackup(string name) { RunCliAction("backup-export --path " + name + " --to " + Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "export"), "导出备份"); }

        public void DeleteBackup(string name) { RunCliAction("backup-delete --path " + name, "删除备份"); }

        public void DryRunRestore(string name) { RunCliAction("restore --dry-run --path " + name, "恢复预览"); }

        /// <summary>应用恢复。**只在隔离数据根里允许**（CLI 自己的准入闸门会拒绝其它情况，界面把它的话原样显示）。</summary>
        public void ApplyRestore(string name) { RunCliAction("restore --path " + name + " --apply", "应用恢复"); }

        public void SetConfig(string key, string value) { RunCliAction("config-set " + key + " \"" + (value == null ? "" : value.Replace("\"", "")) + "\"", "保存设置 " + key); }

        private void RunCliAction(string args, string label)
        {
            _actionLog = "已发起" + label + "…";
            BuildShell();
            _ = RunCliActionAsync(args, label);
        }

        private async System.Threading.Tasks.Task RunCliActionAsync(string args, string label)
        {
            string cli = CliPath();
            // ✗ 原来等待期间**什么都不显示** → 备份 818MB 要几秒，用户感觉"卡住" ✓
            // 现在**先显示"进行中…"** ✓（与体检页同一办法 ✓）→ 用户知道它在干活 ✓✓
            _actionLog = label + "进行中…（" + args + "）";
            Refresh();
            string outp = cli == null ? "未找到工具箱 CLI。" : await System.Threading.Tasks.Task.Run(delegate { return Run(cli, args); });
            _actionLog = label + "结果：" + Environment.NewLine + outp.Trim();
            Refresh();
            // 启动成功后**自动打开浏览器** ✓✓（用户点"启动"就是想用它 ✓）
            // 失败时不打开 ✗；"已在运行"也算成功 ✓（那时打开正好能用 ✓）
            if (args != null && args.StartsWith("start", StringComparison.Ordinal) && outp.IndexOf("START_FAIL", StringComparison.Ordinal) < 0)
                // ✗ 原来打开的是**硬编码裸端口** → dsh 会要求 token → 认证失败 ✗
                // （2026-09-30 真机反馈："dsh web authentication required; reopen the URL printed by dsh web" ✓）
                // 现在只用 CLI 报的 START_URL ✓；取不到就**不打开** ✗（宁可不跳，也不跳到一个必然失败的地址 ✓）
                {
                    string su = "";
                    int ui = outp.IndexOf("START_URL ", StringComparison.Ordinal);
                    if (ui >= 0)
                    {
                        int end = outp.IndexOf('\n', ui);
                        su = (end < 0 ? outp.Substring(ui + 10) : outp.Substring(ui + 10, end - ui - 10)).Trim();
                    }
                    if (su.StartsWith("http", StringComparison.OrdinalIgnoreCase)) OpenUrl(su);
                }
        }
        /// <summary>看板上的操作日志（一键启动/停止的结果，原样展示给用户）。</summary>
        private string _actionLog = "";
        public string ActionLog { get { return _actionLog; } }

        /// <summary>一键启动 dsh（调用工具箱核心的 `start`：非交互，GUI 用）。</summary>
        /// <summary>一键启动的方式：0 = webui（dsh web，走 CLI ✓）；1 = desktop（官方桌面端应用 ✓）。
        /// （用户要求："一键启动按钮底下可选默认启动 desktop 还是 webui" ✓✓）</summary>
        public int StartMode = 0;
        /// <summary>看板图表的日期范围（天 ✓ 7/14/30 可切 ✓）。</summary>
        public int ChartDays = 14;
        // —— 排障开关的值（从 CLI 的 config-get 读 ✓ 用户要求的那三个 ✓）——
        public string BrowserMode = "auto";
        public bool UiParallel = true;
        public void SetChartDays(int d) { ChartDays = d; BuildShell(); }
        public void SetStartMode(int m) { StartMode = m; BuildShell(); }

        /// <summary>一键部署：按当前方式行动 ✓（用户要求："web/desktop 也要加一键部署，desktop 直接官网下安装包就行" ✓✓）
        /// · webui   → 调 CLI 的 install（装/升级 dsh 本体 ✓ 官方 npm 包 ✓）
        /// · desktop → **打开官方下载页** ✓（本工具不重打包、不改官方安装包 ✓）</summary>
        public void DeployForMode()
        {
            if (StartMode == 1)
            {
                bool win = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
                bool mac = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);
                if (!win && !mac)
                {
                    _actionLog = "官方桌面端**暂未发行 Linux 版**（官方目前只提供 Windows 与 macOS）。\nLinux 请用「webui」方式一键部署：装 dsh 本体 + 启动 dsh web。";
                    Refresh();
                    return;
                }
                _actionLog = "官方桌面端请在**官方安装页**下载（那是官方自己的安装包，本工具不重打包、也不改它）：\nhttps://www.deepseek.com/harness/\n装好后回到这里，把方式切到 desktop 点「一键启动」即可。";
                OpenUrl("https://www.deepseek.com/harness/");
                Refresh();
                return;
            }
            RunCliAction("install --yes", "一键部署 dsh（webui）");
        }

        public void StartDsh()
        {
            if (StartMode == 1) { StartDesktopApp(); return; }
            RunCliAction("start --yes", "启动");
        }

        /// <summary>启动**官方桌面端**（Electron 应用 ✓ 独立安装 ✓ 不走 3080 ✓）。
        /// 找不到就**如实说明** ✓ —— Linux 上官方**暂未发行** ✓（用户要求："如果 Linux 没有，就提示暂未发行" ✓✓）</summary>
        public void StartDesktopApp()
        {
            try
            {
                bool win = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
                bool mac = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);
                if (!win && !mac)
                {
                    _actionLog = "官方桌面端**暂未发行 Linux 版**（官方目前只提供 Windows 与 macOS）。\nLinux 上请用「webui」方式：启动 dsh web 后在浏览器里打开。";
                    Refresh();
                    return;
                }
                if (win)
                {
                    string p = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "DeepSeek Harness", "DeepSeek Harness.exe");
                    if (System.IO.File.Exists(p)) { Process.Start(new ProcessStartInfo(p) { UseShellExecute = true }); _actionLog = "已启动官方桌面端：" + p; Refresh(); return; }
                    _actionLog = "没找到官方桌面端。默认安装位置：\n" + p + "\n装好后这个按钮就能直接启动它。";
                    Refresh();
                    return;
                }
                Process.Start(new ProcessStartInfo("open", "-a \"DeepSeek Harness\"") { UseShellExecute = false });
                _actionLog = "已尝试启动官方桌面端（macOS）。";
                Refresh();
            }
            catch (Exception ex) { _actionLog = "启动官方桌面端失败：" + ex.Message; Refresh(); }
        }

        /// <summary>停止 dsh（核心的 `stop`）。</summary>
        public void StopDsh() { RunCliAction("stop --yes", "停止"); }

        /// <summary>只停 **web**（3080 ✓ 不动桌面端 ✓）。两个都开着时用 ✓✓</summary>
        public void StopWebOnly() { RunCliAction("stop --yes", "停止 web"); }

        /// <summary>检查更新 ✓（**只读** ✓ 调 CLI 的 `update-info` ✓ 结果进右下角 toast ✓✓）。</summary>
        public void CheckUpdate() { RunCliAction("update-info", "检查更新"); }

        /// <summary>执行更新 ✓（CLI 的 `update` ✓ **先备份再更新** ✓ 有回滚点 ✓；结果进 toast ✓）。</summary>
        public void RunUpdate() { RunCliAction("update --yes", "更新 dsh"); }

        /// <summary>只停**官方桌面端**（Electron 多进程 → CLI 用 StopTree 杀整棵 ✓ 不动 web ✓）。</summary>
        public void StopDesktopOnly() { RunCliAction("stop --target desktop --yes", "停止桌面端"); }

        private void RunCoreAction(string verb, string label)
        {
            string core = ToolkitCore();
            if (core == null)
            {
                _actionLog = "无法" + label + "：" + CoreMissingText();
                BuildShell();
                return;
            }
            _actionLog = "已发起" + label + "…（" + Path.GetFileName(core) + " " + verb + "）";
            BuildShell();
            string outp = RunQuick(core, verb, 8);
            _actionLog = label + "结果：" + Environment.NewLine + outp.Trim();
            Refresh();
        }

        /// <summary>短超时运行（启动/停止这类命令可能一直挂着，不能把界面卡住 30 秒）。</summary>
        private static string RunQuick(string cli, string args, int seconds)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(cli, args);
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.StandardOutputEncoding = new UTF8Encoding(false);
                psi.StandardErrorEncoding = new UTF8Encoding(false);
                using (Process p = Process.Start(psi))
                {
                    StringBuilder sb = new StringBuilder();
                    string err = "";
                    System.Threading.Tasks.Task<string> soT = System.Threading.Tasks.Task.Run(delegate { return p.StandardOutput.ReadToEnd(); });
                    System.Threading.Tasks.Task<string> seT = System.Threading.Tasks.Task.Run(delegate { return p.StandardError.ReadToEnd(); });
                    if (!p.WaitForExit(seconds * 1000)) { try { p.Kill(); } catch { } return "（超过 " + seconds + " 秒未结束，已结束该进程）"; }
                    sb.Append(soT.Result);
                    err = seT.Result;
                    if (!string.IsNullOrEmpty(err)) sb.Append(Environment.NewLine).Append("[stderr] ").Append(err);
                    return sb.Length == 0 ? "（无输出）" : sb.ToString();
                }
            }
            catch (Exception ex) { return "执行失败：" + ex.Message; }
        }
        /// <summary>窗口图标（任务栏/标题栏）。资源 URI 用**程序集名** dsht-gui —— 用命名空间会静默失败。</summary>
        private void SetWindowIcon()
        {
            try
            {
                using (System.IO.Stream s = global::Avalonia.Platform.AssetLoader.Open(new Uri("avares://dsht-gui/Assets/logo-icon.png")))
                {
                    Icon = new WindowIcon(s);
                }
            }
            catch { /* 图标缺失不影响功能 */ }
        }
        /// <summary>核心程序不可用时的说明（区分"平台不支持"与"文件缺失"）。</summary>
        private static string CoreMissingText()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                return "此功能需要 Windows 专有的 v2.x 核心（DeepSeek Harness Toolkit.exe）—— 当前平台不支持。"
                     + Environment.NewLine + "跨平台可用的替代：状态/概览/会话/token/形态与插件/备份清单/设置（都走 V3 CLI）。";
            return "未找到工具箱核心程序（DeepSeek Harness Toolkit.exe）：请把它与 GUI 放在同一目录，或设置环境变量 DSHT_CORE。";
        }
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // ---------------- 窗口壳配色（跟随 Palette，风格切换时重刷） ----------------

        /// <summary>顶栏切换器的内容与提示（只装一次；颜色在 ApplyChrome 里刷）。</summary>
        private void InitChrome()
        {
            Symbol[] shellIcons = new Symbol[] { Symbol.PanelLeft, Symbol.Tab, Symbol.Grid, Symbol.PanelRight, Symbol.Board };
            for (int i = 0; i < 5; i++)
            {
                Button b = this.FindControl<Button>("Shell" + i);
                if (b == null) continue;
                b.Content = new SymbolIcon { Symbol = shellIcons[i], IconVariant = IconVariant.Regular, FontSize = 15 };
                ToolTip.SetTip(b, "布局：" + Shells.Shells.Name(i));
            }
            for (int i = 0; i < 4; i++)
            {
                Button b = this.FindControl<Button>("Style" + i);
                if (b == null) continue;
                b.Content = new TextBlock { Text = ((char)('A' + i)).ToString(), FontSize = 12, FontWeight = FontWeight.SemiBold };
                ToolTip.SetTip(b, "风格：" + Palette.StyleName(i));
            }
        }

        /// <summary>把 Palette 应用到窗口壳：主题变体（让 Fluent 控件跟随明暗）、顶栏、两个 segmented 切换器的激活态。</summary>
        private void ApplyChrome()
        {
            Background = Palette.PageBg;
            if (Application.Current != null)
                Application.Current.RequestedThemeVariant = Palette.Dark ? ThemeVariant.Dark : ThemeVariant.Light;

            Border appBar = this.FindControl<Border>("AppBar");
            if (appBar != null) { appBar.Background = Palette.SidebarBg; appBar.BorderBrush = Palette.Border; }

            Border mark = this.FindControl<Border>("BrandMark");
            if (mark != null)
                mark.Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = new GradientStops
                    {
                        new GradientStop(((SolidColorBrush)Palette.Accent).Color, 0),
                        new GradientStop(((SolidColorBrush)Palette.AccentHover).Color, 1)
                    }
                };
            SetFg("BrandTitle", Palette.Text);
            SetFg("BrandSub", Palette.TextFaint);

            Border pill = this.FindControl<Border>("DisclaimerPill");
            if (pill != null) pill.Background = Palette.WarnSoft;
            SetFg("DisclaimerIcon", Palette.Warn);
            SetFg("DisclaimerText", Palette.TextDim);

            RecolorWindowButtons();
            PaintSwitch("ShellSwitch");
            PaintSwitch("StyleSwitch");
            for (int i = 0; i < 5; i++) PaintSwitchButton("Shell" + i, i == _shell);
            for (int i = 0; i < 4; i++) PaintSwitchButton("Style" + i, i == Palette.StyleKind);
        }

        private void SetFg(string name, IBrush fg)
        {
            TextBlock t = this.FindControl<TextBlock>(name);
            if (t != null) t.Foreground = fg;
        }

        private void PaintSwitch(string name)
        {
            Border b = this.FindControl<Border>(name);
            if (b != null) { b.Background = Palette.InsetBg; b.BorderBrush = Palette.Border; b.BorderThickness = new Thickness(1); }
        }

        private void PaintSwitchButton(string name, bool active)
        {
            Button b = this.FindControl<Button>(name);
            if (b == null) return;
            b.Padding = new Thickness(10, 5);
            b.CornerRadius = new CornerRadius(7);
            b.BorderThickness = new Thickness(0);
            b.Background = active ? (IBrush)Palette.CardBg : Brushes.Transparent;
            IBrush fg = active ? Palette.Accent : Palette.TextDim;
            SymbolIcon si = b.Content as SymbolIcon;
            if (si != null) si.Foreground = fg;
            TextBlock tb = b.Content as TextBlock;
            if (tb != null) tb.Foreground = fg;
        }

        // ---------------- 给 Shells 用的状态 ----------------

        public SessionsSnapshot Data { get { return _data; } }
        public ProfilesSnapshot Profiles { get { return _profiles; } }
        public StatusSnapshot Status { get { return _status; } }
        public BackupSummary Backups { get { return _backups; } }
        public DoctorSummary Doctor { get { return _doctor; } }
        private DoctorSummary _doctor;
        public int ProfilesFilter { get; set; }
        public string ProfileSearch = "";
        /// <summary>待二次确认的隔离操作（"profile|entryId"）；空=没有待确认项。写操作必须点两次。</summary>
        public string PendingPatch = "";
        public void Rebuild() { BuildShell(); }

        public void SetProfilesFilter(int mode) { ProfilesFilter = mode; BuildShell(); }

        /// <summary>健康检查（profilecheck）的原始输出（懒加载一次）。</summary>
        public string Health { get { return _health; } }
        private string _health = "";
        public void LoadHealth() { _ = LoadHealthAsync(); }

        /// <summary>体检：**异步**跑 profilecheck + doctor ✓✓
        /// 原来这两次调用是同步的、跑在 UI 线程上 ✗ —— doctor 要查网络，最坏各 30 秒超时
        /// → 点"运行检查"会让整个窗口冻结近一分钟 ✗（2026-09-30 真机反馈"体检页面卡住" ✓）
        /// 现在：先立刻显示"检查中…"（页面有反馈 ✓），两次调用都在后台线程 ✓，完成后刷新 ✓</summary>
        private async System.Threading.Tasks.Task LoadHealthAsync()
        {
            if (!string.IsNullOrEmpty(_health) && _health != "检查中…") { BuildShell(); return; }
            string cli = CliPath();
            if (cli == null) { _health = "未找到工具箱 CLI。"; BuildShell(); return; }
            _health = "检查中…（profilecheck 与 doctor 在后台运行；doctor 会查网络，慢时可能十几秒）";
            BuildShell();
            string h = await System.Threading.Tasks.Task.Run(delegate { return Run(cli, "profilecheck"); });
            string d = await System.Threading.Tasks.Task.Run(delegate { return Run(cli, "doctor"); });
            _health = h;
            _doctor = SummaryMarkers.ParseDoctor(d);
            BuildShell();
        }

        /// <summary>隔离/恢复一个插件条目：调用工具箱核心的 profilepatch（写操作：它会先备份、再改、失败逐字节回滚）。</summary>
        public void PatchEntry(string profile, string entryId, bool disable)
        {
            string cli = CliPath();
            if (cli == null) { _actionLog = "无法执行隔离：未找到工具箱 CLI。"; BuildShell(); return; }
            string yaml = Path.Combine(Path.Combine(_profilesRoot, profile), "cordis.patch.yml");
            string args = "profilepatch --file " + yaml + " --id " + entryId + (disable ? " --disable" : " --set disabled=false") + " --yes";
            string outp = Run(cli, args);
            _health = "profilepatch " + (disable ? "--disable" : "--set disabled=false") + " " + entryId + " 的结果：" + Environment.NewLine + outp;
            BuildShell();
        }

        /// <summary>在文件管理器里打开插件目录（Windows 资源管理器 / Linux 文件管理器）。</summary>
        /// <summary>用系统默认程序打开一个 URL（Windows: ShellExecute ✓；Linux: xdg-open ✓）。</summary>
        public void OpenUrl(string url)
        {
            try
            {
                bool win = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
                if (win)
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    return;
                }
                // Linux：**不加引号** ✓（没有 shell 参与 ✓）· 并**检测退出码** ✗ —— 系统没设默认浏览器时 xdg-open 会失败 ✓
                // （2026-09-30 真机：xdg-settings 返回空 → xdg-open 静默失败 ✗ 而 Process.Start 成功 ✓ → 用户看不到任何反应 ✓）
                // ✗✗ 真机实测（2026-09-30）：GNOME 的 xdg-open(gio) **自己会用 HTTP 客户端请求这个 URL** ✓
                //    → dsh 对它的请求返回 401 ✗ → gio 报 "Unauthorized" 就**放弃** ✗ → 浏览器永远打不开 ✓✓
                //    （URL 本身是对的 ✓ 带 token ✓ —— 问题在 xdg-open 的实现 ✗）
                // → 所以 Linux 上**先直接试浏览器** ✓✓（装了哪个用哪个 ✓）；xdg-open 只给 2 秒做兜底 ✓
                // ✗✗ 真机实测（2026-09-30）第二层原因：这台机器上 **firefox 是 snap 包** ✓
                //    → snap 应用有 **cgroup 限制** ✗：非会话启动器直接跑 `firefox` 会报
                //      "…is not a snap cgroup for tag snap.firefox.firefox" ✓✓
                //    → **`snap run firefox` 能自建正确的 cgroup** ✓✓ 实测只差 DISPLAY ✓（GUI 在会话内有 ✓✓）
                // → 所以 Linux 上顺序：snap run firefox → firefox → chromium 系 → xdg-open ✓
                // ✗✗ 第三层（真机实测 2026-09-30）：snap firefox 还**必须有 `WAYLAND_DISPLAY`** ✓✓
                //    报错原文：Missing Wayland display, WAYLAND_DISPLAY is empty ✓
                //    → 如果 GUI 自己的环境里没有它（例如被非常规方式启动 ✗）→ **替它探测出来** ✓✓
                // 排障开关 ✓：browser_mode 决定**走哪条路**（用户可切换 ✓ 真的接线 ✓）
                bool trySnap = BrowserMode == "auto" || BrowserMode == "snap";
                bool tryDirect = BrowserMode == "auto" || BrowserMode == "direct";
                bool tryXdg = BrowserMode == "auto" || BrowserMode == "xdg";
                string wd = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
                if (string.IsNullOrEmpty(wd))
                {
                    try
                    {
                        string rd = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
                        if (string.IsNullOrEmpty(rd)) rd = "/run/user/" + Environment.GetEnvironmentVariable("UID");
                        if (!string.IsNullOrEmpty(rd) && System.IO.Directory.Exists(rd))
                        {
                            string[] socks = System.IO.Directory.GetFiles(rd, "wayland-*");
                            for (int si = 0; si < socks.Length; si++)
                            {
                                string nm = System.IO.Path.GetFileName(socks[si]);
                                if (nm.EndsWith(".lock", StringComparison.Ordinal)) continue;
                                wd = nm; break;
                            }
                        }
                    }
                    catch { }
                }
                if (trySnap)
                {
                    try
                    {
                        ProcessStartInfo sp = new ProcessStartInfo("snap", "run firefox \"" + url + "\"") { UseShellExecute = false };
                        if (!string.IsNullOrEmpty(wd)) sp.Environment["WAYLAND_DISPLAY"] = wd;   // 补上它 ✓✓
                        Process p0 = Process.Start(sp);
                        if (p0 != null) return;   // snap 不在的话会抛异常 ✓ 落到下面的候选 ✓
                    }
                    catch { }
                }
                string[] browsers0 = new string[] { "firefox", "chromium", "chromium-browser", "google-chrome", "epiphany" };
                if (tryDirect)
                {
                    for (int bi0 = 0; bi0 < browsers0.Length; bi0++)
                    {
                        try { Process.Start(new ProcessStartInfo(browsers0[bi0], url) { UseShellExecute = false }); return; } catch { }
                    }
                }
                if (!tryXdg) return;   // browser_mode 指定了别的路 → 不走 xdg ✓
                bool opened = false;
                try
                {
                    ProcessStartInfo xp = new ProcessStartInfo("xdg-open", url) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
                    using (Process xp2 = Process.Start(xp))
                    {
                        if (xp2 != null) { xp2.WaitForExit(2000); opened = xp2.HasExited && xp2.ExitCode == 0; }   // 只等 2 秒 ✓（它在 GNOME 上必然失败 ✗ 不值得等 6 秒 ✓）
                    }
                }
                catch { opened = false; }
                if (opened) return;
                // 回退：直接试常见浏览器 ✓（装了哪个用哪个 ✓ 不依赖系统默认关联 ✓）
                string[] browsers = new string[] { "firefox", "chromium", "chromium-browser", "google-chrome", "epiphany" };
                for (int bi = 0; bi < browsers.Length; bi++)
                {
                    try { Process.Start(new ProcessStartInfo(browsers[bi], url) { UseShellExecute = false }); return; } catch { }
                }
                _actionLog = "打不开浏览器：系统没有设置默认浏览器，也没找到常见浏览器。请手动打开这个地址：" + Environment.NewLine + url;
            }
            catch (Exception ex)
            {
                _actionLog = "打开浏览器失败：" + ex.Message + Environment.NewLine + url;
            }
        }
        public void OpenFolder(string path)
        {
            try
            {
                bool win = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
                ProcessStartInfo psi = win
                    ? new ProcessStartInfo(path) { UseShellExecute = true }
                    : new ProcessStartInfo("xdg-open", "\"" + path + "\"") { UseShellExecute = false };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                _actionLog = "打开目录失败：" + ex.Message + Environment.NewLine + path;
                BuildShell();
            }
        }

        /// <summary>插件安装目录（构造即可，不需要 CLI：<profiles>/<name>/node_modules/<id>）。</summary>
        public string BundleFolder(string profile, string bundleId)
        {
            string root = _profilesRoot;
            if (string.IsNullOrEmpty(root)) return "";
            return Path.Combine(Path.Combine(Path.Combine(root, profile), "node_modules"), bundleId.Replace('/', Path.DirectorySeparatorChar));
        }

        private string _profilesRoot = "";
        public string ProfilesRoot { get { return _profilesRoot; } }

        /// <summary>找工具箱核心程序（有 profilepatch 的那个 v2.x exe）。</summary>
        private static string ToolkitCore()
        {
            string env = Environment.GetEnvironmentVariable("DSHT_CORE");
            if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                return null;   // v2.x 核心是 Windows/.NET Framework 专有：非 Windows 上不是"文件缺失"，而是平台不支持
            string[] names = new string[] { "DeepSeek Harness Toolkit.exe", "dsht.exe" };
            for (int i = 0; i < names.Length; i++)
            {
                string p = Path.Combine(dir, names[i]);
                if (File.Exists(p)) return p;
            }
            return null;
        }
        public void SetProfileSearch(string text) { ProfileSearch = text == null ? "" : text; BuildShell(); }
        public List<SessionRowVm> Rows { get { return _rows; } }
        /// <summary>当前视图要显示的列表（过滤后 ✓；默认 = 全部 ✓）。</summary>
        public List<SessionRowVm> ListSource { get { return _listSource == null ? _rows : _listSource; } }
        private List<SessionRowVm> _listSource;
        public void SetListSource(List<SessionRowVm> src) { _listSource = src; }
        public int SortMode { get; set; }
        public int Filter { get { return _filter; } }
        public int StyleKind { get { return Palette.StyleKind; } }

        /// <summary>切换视觉 demo（A/B/C/D）：换配色与密度后重画外壳。</summary>
        public void SetStyle(int kind)
        {
            Palette.Apply(kind);
            BuildShell();
        }
        public int MainSection { get { return _mainSection; } }
        public int SubTab { get { return _subTab; } }
        public bool IsSessionsSection { get { return _mainSection == 2; } }
        /// <summary>概览与看板都需要 status/profiles/sessions 这批数据。</summary>
        public bool IsOverviewLike { get { return _mainSection <= 1; } }
        public string RawOutput { get { return _rawOutput; } }
        /// <summary>导航表访问一律带范围保护 —— 菜单项数与表长度不一致时不允许越界（审计发现过 UI 线程越界崩溃）。</summary>
        public string[] SubTabs { get { return _mainSection >= 0 && _mainSection < NavSubs.Length && NavSubs[_mainSection] != null ? NavSubs[_mainSection] : new string[0]; } }

        public string PageTitle
        {
            get { return NavItems[_mainSection].Replace("　", " ").Trim(); }
        }

        public string SubtitleText
        {
            get
            {
                string[] d = _mainSection >= 0 && _mainSection < NavDesc.Length && NavDesc[_mainSection] != null ? NavDesc[_mainSection] : new string[0];
                if (d.Length == 0) return "";
            int i = _subTab < d.Length ? _subTab : 0;
                return d[i];
            }
        }

        /// <summary>子菜单在会话页切的是"排序视角"，这里把当前视角说清楚。</summary>
        public string FocusText
        {
            get
            {
                switch (_subTab)
                {
                    case 1: return "当前视角：缓存命中率（低→高）—— 命中率低的会话排在最前，最值得先看。";
                    case 2: return "当前视角：解码速度（快→慢）—— 反映生成 token 的速率。";
                    case 3: return "当前视角：上下文压力（高→低）—— 越靠前越接近触发压缩。";
                    default: return "当前视角：总览（按最后活动排序）—— 想看别的角度，点上面的子菜单。";
                }
            }
        }

        public string SourceText
        {
            get
            {
                if (_data == null) return "数据来源：—";
                return _data.SourceText + "　投影目录：" + _data.Root + "　（GUI 不引用核心程序集，只解析 CLI 标记行）";
            }
        }

        // ---------------- 导航与渲染 ----------------

        public void SetMainSection(int idx)
        {
            if (idx < 0 || idx >= NavItems.Length) return;
            _mainSection = idx;
            _subTab = 0;
            SortMode = 0;
            Refresh();
        }

        public void SetSubTab(int idx)
        {
            _subTab = idx;
            if (IsSessionsSection)
            {
                // 子菜单即"排序视角"：总览=最后活动，命中率=低→高，解码=快→慢，压力=高→低
                // 子菜单只决定"看什么"（列表 / 统计）；排序一律交给工具栏的下拉 —— 原先两者都管排序，互相冲突。
                Rerender();
            }
            else
            {
                BuildShell();
            }
        }

        public void SetFilter(int mode)
        {
            _filter = mode;
            Rerender();
        }

        public void Rerender()
        {
            if (_data == null || !_data.Ok) return;
            List<SessionRow> rows = SessionsView.Filter(_data.Rows, _filter);
            rows = SessionsView.Sort(rows, SortMode);
            SessionsView.AttachBars(rows);
            List<SessionRowVm> vms = new List<SessionRowVm>();
            for (int i = 0; i < rows.Count; i++) vms.Add(new SessionRowVm(rows[i]));
            _rows = vms;
            BuildShell();
        }

        public void ShowDetail(SessionRowVm vm)
        {
            Shells.Shells.FillDetail(DetailHost, vm);
        }

        private void BindStyle(int id)
        {
            Button b = this.FindControl<Button>("Style" + id);
            if (b == null) return;
            b.Click += delegate(object s, RoutedEventArgs e) { SetStyle(id); };
        }

        private void BindShell(int id)
        {
            Button b = this.FindControl<Button>("Shell" + id);
            if (b == null) return;
            b.Click += delegate(object s, RoutedEventArgs e)
            {
                _shell = id;
                BuildShell();
            };
        }

        private void BuildShell()
        {
            ContentControl body = this.FindControl<ContentControl>("Body");
            if (body == null) return;
            ApplyChrome();
            DetailHost = null;
            // 页面外面包一层 Grid ✓ 把 toast 作为**浮层**加在最后 ✓✓
            // （用户要求：那种提示改成"窗口内右下角弹窗" ✓ 原来是页面流里的一张卡片 ✗）
            Grid wrap = new Grid();
            wrap.Children.Add(Shells.Shells.Build(_shell, this));
            wrap.Children.Add(ToastLayer());
            body.Content = wrap;
            // 有新的操作日志 → 弹一次 ✓（去重：同一条不重复弹 ✓）
            if (!string.IsNullOrEmpty(_actionLog) && _actionLog != _lastToasted)
            {
                _lastToasted = _actionLog;
                ShowToast(_actionLog);
            }
        }

        // —— 右下角弹窗（toast）✓✓ 用户要求："这个绿色框的提示改为在窗口内右下角弹窗提示吧" ——
        private Border _toast;
        private TextBlock _toastText;
        private string _lastToasted = "";
        private global::Avalonia.Threading.DispatcherTimer _toastTimer;

        /// <summary>浮层容器：一个**右下角对齐**的 Border ✓ 初始隐藏 ✓ 不挡操作 ✓（只有它自己那块可点 ✓）。</summary>
        private Control ToastLayer()
        {
            _toastText = new TextBlock { Text = "", FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Palette.Text };
            StackPanel sp = new StackPanel { Spacing = 6 };
            sp.Children.Add(_toastText);
            sp.Children.Add(new TextBlock { Text = "点一下关闭", FontSize = 10.5, Foreground = Palette.TextFaint });
            _toast = new Border
            {
                Child = sp,
                MaxWidth = 460,
                Background = Palette.CardBg,
                BorderBrush = Palette.Warn,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 11),
                Margin = new Thickness(0, 0, 20, 20),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                IsVisible = false,
                BoxShadow = new BoxShadows(new BoxShadow { Blur = 18, OffsetY = 4, Color = Color.FromArgb(60, 0, 0, 0) })
            };
            _toast.PointerPressed += delegate { HideToast(); };
            return _toast;
        }

        /// <summary>弹一条 ✓（8 秒后自动消失 ✓ 也可以点掉 ✓）。</summary>
        public void ShowToast(string text)
        {
            if (_toast == null || _toastText == null || string.IsNullOrEmpty(text)) return;
            _toastText.Text = text;
            _toast.IsVisible = true;
            if (_toastTimer == null)
            {
                _toastTimer = new global::Avalonia.Threading.DispatcherTimer();
                _toastTimer.Interval = TimeSpan.FromSeconds(8);
                _toastTimer.Tick += delegate { HideToast(); };
            }
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        private void HideToast()
        {
            if (_toastTimer != null) _toastTimer.Stop();
            if (_toast != null) _toast.IsVisible = false;
        }

        private bool _busy;
        public void Refresh() { if (_busy) return; _busy = true; _ = RefreshGuardedAsync(); }   // 重入保护：刷新期间再点不叠加

        /// <summary>GUI 启动时按 `auto_start` **自动起一次** dsh ✓（用户要求："GUI/CLI 启动时自动起" ✓✓）
        /// 约束（重要 ✓）：① **每次 GUI 会话只试一次** ✗（不能每次刷新都起 ✓）
        ///               ② **只在服务没在跑时** ✓（STATUS_DOWN 才起 ✓ 不重复启动 ✓）
        ///               ③ `auto_start=off` 时**什么都不做** ✓✓</summary>
        private bool _autoStartTried = false;
        /// <summary>解析排障开关（`CONFIG browser_mode …` / `CONFIG ui_parallel …` ✓）。
        /// 解析失败就保持默认 ✓ 不猜 ✓。</summary>
        private void ParseTroubleshootSwitches(string cfg)
        {
            if (string.IsNullOrEmpty(cfg)) return;
            string[] ls = cfg.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < ls.Length; i++)
            {
                string t2 = ls[i] == null ? "" : ls[i].Trim();
                if (t2.StartsWith("CONFIG browser_mode ", StringComparison.Ordinal)) BrowserMode = t2.Substring("CONFIG browser_mode ".Length).Trim();
                else if (t2.StartsWith("CONFIG ui_parallel ", StringComparison.Ordinal)) UiParallel = t2.Substring("CONFIG ui_parallel ".Length).Trim() != "off";
            }
        }

        private async System.Threading.Tasks.Task AutoStartOnceAsync(string cli)
        {
            if (_autoStartTried) return;
            _autoStartTried = true;
            try
            {
                string cfg = await System.Threading.Tasks.Task.Run(delegate { return Run(cli, "config-get"); });
                bool wantAuto = cfg != null && cfg.IndexOf("auto_start on", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!wantAuto) return;
                string st = await System.Threading.Tasks.Task.Run(delegate { return Run(cli, "status"); });
                bool down = st != null && st.IndexOf("STATUS_DOWN", StringComparison.Ordinal) >= 0;
                // ✗✗ 关键修正：**只有桌面端在跑时，status 也报 STATUS_DOWN**（它只探 3080 ✓）
                // → 于是 auto-start 会**再起一个 webui** ✗ → 两个同时跑 ✓（用户实测反馈 ✓）
                // → 检测到桌面端就**不再起 web** ✓✓（用户在用桌面端 ✓ 不需要 web ✓）
                bool desktopUp = st != null && st.IndexOf("STATUS_DESKTOP", StringComparison.Ordinal) >= 0;
                if (desktopUp)
                {
                    _actionLog = "检测到官方桌面端正在运行 → **不启动 webui**（避免两个同时跑；要用 web 请先在侧栏点「停止」或关掉桌面端）";
                    BuildShell();
                    return;
                }
                if (!down) return;   // 已经在跑 → 不动它 ✓
                _actionLog = "auto_start=on → 正在自动启动 dsh…";
                BuildShell();
                string outp = await System.Threading.Tasks.Task.Run(delegate { return Run(cli, "start --yes"); });
                _actionLog = "auto_start 自动启动结果：" + Environment.NewLine + (outp == null ? "" : outp.Trim());
                Refresh();
            }
            catch { }
        }   // 异步：CLI 调用不占 UI 线程

        /// <summary>保证 _busy 一定复位：刷新中途抛异常也不许把界面锁死成一次性。</summary>
        private async System.Threading.Tasks.Task RefreshGuardedAsync()
        {
            try { await RefreshAsync(); }
            finally { _busy = false; }
        }

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            string cli = CliPath();
            if (cli != null && !_autoStartTried) _ = AutoStartOnceAsync(cli);   // 启动时自动起一次 ✓（内部有"只一次 + 只在没跑时"约束 ✓）
            if (cli == null)
            {
                _data = null;
                _rawOutput = "未找到工具箱 CLI。请把 dsh-minato.exe（或 dsht.exe / dsht_v3.exe）放到本程序同目录，或设置环境变量 DSHT_CLI 指向它。";
                BuildShell();
                return;
            }

            if (IsOverviewLike)
            {
                _rawOutput = await System.Threading.Tasks.Task.Run(delegate { return Run(cli, "status --detail"); });
                _status = StatusMarkers.Parse(_rawOutput);
                // 概览页顺带把这几样也取回来（都很快，且都是只读）
                // ✗ 原来是**串行** await 三次 → 每次切页都等 3×100~200ms ≈ 0.5~1 秒 ✗（用户反馈"切换卡片响应不及时" ✓）
                // 现在**并行** ✓✓ —— 三者互相独立（profiles / sessions / backup-list ✓）→ 总耗时 = 最慢那个 ✓
                // 先读一次排障开关 ✓（必须**在读之前** ✓ 否则 UiParallel 永远是默认值 ✗ —— 我上一轮就是漏了这步 ✓）
                string cfgText2 = await System.Threading.Tasks.Task.Run(delegate { return Run(cli, "config-get"); });
                ParseTroubleshootSwitches(cfgText2);
                if (!UiParallel)
                {
                    // 排障开关 off → **回到老行为（串行）** ✓ 真的接线 ✓ 不做摆设 ✗
                    _profiles = ProfilesMarkers.Parse(await System.Threading.Tasks.Task.Run(delegate { return Run(cli, "profiles"); }));
                    _data = SessionsMarkers.Parse(await System.Threading.Tasks.Task.Run(delegate { return Run(cli, "sessions"); }));
                    _backups = SummaryMarkers.ParseBackups(await System.Threading.Tasks.Task.Run(delegate { return Run(cli, "backup-list"); }));
                }
                else
                {
                System.Threading.Tasks.Task<string> tPf = System.Threading.Tasks.Task.Run(delegate { return Run(cli, "profiles"); });
                System.Threading.Tasks.Task<string> tSe = System.Threading.Tasks.Task.Run(delegate { return Run(cli, "sessions"); });
                System.Threading.Tasks.Task<string> tBk = System.Threading.Tasks.Task.Run(delegate { return Run(cli, "backup-list"); });
                await System.Threading.Tasks.Task.WhenAll(tPf, tSe, tBk);
                _profiles = ProfilesMarkers.Parse(tPf.Result);
                _data = SessionsMarkers.Parse(tSe.Result);
                _backups = SummaryMarkers.ParseBackups(tBk.Result);
                }   // ✗ 原来带 --detail → 每份备份都要算目录大小（重 I/O ✗）→ 概览每次刷新都卡几秒 ✓✓ 这里只要 Count/Latest ✓ 不需要大小 ✓（方案 A ✓）
                if (_doctor == null) _doctor = new DoctorSummary();
                BuildShell();
                return;
            }
            if (_mainSection == 3)
            {
                _rawOutput = await System.Threading.Tasks.Task.Run(delegate { return Run(cli, "profiles"); });
                _profiles = ProfilesMarkers.Parse(_rawOutput);
                for (int i = 0; i < _rawOutput.Length && _profilesRoot.Length == 0; i++) { }
                _profilesRoot = ProfilesRootFrom(cli);
                BuildShell();
                return;
            }
            if (!IsSessionsSection)
            {
                _rawOutput = await System.Threading.Tasks.Task.Run(delegate { return Run(cli, string.Join(" ", (_mainSection >= 0 && _mainSection < NavCli.Length && NavCli[_mainSection] != null ? NavCli[_mainSection] : new string[0]))); });
                if (_mainSection == 5) _doctor = SummaryMarkers.ParseDoctor(_rawOutput);   // 体检页吃解析结果，不是只吃原文
                BuildShell();
                return;
            }

            string text = await System.Threading.Tasks.Task.Run(delegate { return Run(cli, "sessions"); });
            _data = SessionsMarkers.Parse(text);
            _rawOutput = text;
            if (!_data.Ok)
            {
                _rows = new List<SessionRowVm>();
            }
            else
            {
                List<SessionRow> rows = SessionsView.Sort(SessionsView.Filter(_data.Rows, _filter), SortMode);
                SessionsView.AttachBars(rows);
                List<SessionRowVm> vms = new List<SessionRowVm>();
                for (int i = 0; i < rows.Count; i++) vms.Add(new SessionRowVm(rows[i]));
                _rows = vms;
            }
            BuildShell();
        }

        /// <summary>从 CLI 的 SESSIONS_ROOT 风格路径推出 profiles 根（profiles 命令不直接给，这里用数据根 + profiles）。</summary>
        private static string ProfilesRootFrom(string cli)
        {
            try
            {
                string outp = Run(cli, "sessions");
                string[] lines = outp.Replace("\r\n", "\n").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].StartsWith("SESSIONS_ROOT ", StringComparison.Ordinal)) continue;
                    string p = lines[i].Substring("SESSIONS_ROOT ".Length).Trim();
                    DirectoryInfo d = Directory.GetParent(p);
                    if (d != null) return Path.Combine(d.FullName, "profiles");
                }
            }
            catch { }
            return "";
        }

        private static string CliPath()
        {
            string env = Environment.GetEnvironmentVariable("DSHT_CLI");
            if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            bool win = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
            string[] names = win
                ? new string[] { "dsh-minato.exe", "dsht.exe", "dsht_v3.exe", "DeepSeek Harness Toolkit.exe" }
                : new string[] { "dsh-minato", "dsht", "dsht_v3", "DeepSeek Harness Toolkit" };   // Unix 的 apphost 没有扩展名
            for (int i = 0; i < names.Length; i++)
            {
                string p = Path.Combine(dir, names[i]);
                if (File.Exists(p)) return p;
            }
            // 只在**同级**找是不够的 ✗：发布包里 GUI 在 <包>\gui\，而 CLI 在 <包>\ 与 <包>\cli-small\
            // → 真机反馈"未找到 CLI" ✓ 就是这个原因。改为向上、向已知子目录、以及 PATH 都找 ✓✓。
            string[] extraDirs = new string[]
            {
                Path.Combine(dir, ".."),                    // 包根（GUI 在 gui\ 时 ✓）
                Path.Combine(dir, "..", ".."),              // 再上一层（GUI 嵌得更深时 ✓）
                Path.Combine(dir, "cli-small"),             // 包内的小体积版 ✓
                Path.Combine(dir, "..", "cli-small"),
                Path.Combine(dir, "..", "..", "cli-small")
            };
            for (int d = 0; d < extraDirs.Length; d++)
            {
                string ed;
                try { ed = Path.GetFullPath(extraDirs[d]); } catch { continue; }
                for (int i = 0; i < names.Length; i++)
                {
                    try { string p = Path.Combine(ed, names[i]); if (File.Exists(p)) return p; } catch { }
                }
            }
            // 兜底：PATH 里找（用户可能已经把 CLI 装到 PATH ✓）
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                string[] parts = pathEnv.Split(Path.PathSeparator);
                for (int d = 0; d < parts.Length; d++)
                {
                    if (string.IsNullOrEmpty(parts[d])) continue;
                    for (int i = 0; i < names.Length; i++)
                    {
                        try { string p = Path.Combine(parts[d], names[i]); if (File.Exists(p)) return p; } catch { }
                    }
                }
            }
            return null;
        }

        private static string Run(string cli, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(cli, args);
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.StandardOutputEncoding = new UTF8Encoding(false);
                psi.StandardErrorEncoding = new UTF8Encoding(false);
                using (Process p = Process.Start(psi))
                {
                    StringBuilder sb = new StringBuilder();
                    string err = "";
                    System.Threading.Tasks.Task<string> soT = System.Threading.Tasks.Task.Run(delegate { return p.StandardOutput.ReadToEnd(); });
                    System.Threading.Tasks.Task<string> seT = System.Threading.Tasks.Task.Run(delegate { return p.StandardError.ReadToEnd(); });
                    if (!p.WaitForExit(30000)) { try { p.Kill(); } catch { } return "（超时 30 秒，已结束该进程）"; }
                    sb.Append(soT.Result);
                    err = seT.Result;
                    if (!string.IsNullOrEmpty(err)) sb.Append(Environment.NewLine).Append("[stderr] ").Append(err);
                    return sb.Length == 0 ? "（无输出）" : sb.ToString();
                }
            }
            catch (Exception ex)
            {
                return "运行 CLI 失败: " + ex.Message;
            }
        }
    }
}
