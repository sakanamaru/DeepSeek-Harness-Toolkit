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
            new string[] { "指标", "图表" },
            new string[] { "会话列表", "统计" },
            new string[] { "原始输出" },
            new string[] { "原始输出" },
            new string[] { "原始输出" },
            new string[] { "原始输出" },
            new string[] { "原始输出" }
        };
        private static readonly string[][] NavDesc = new string[][]
        {
            new string[] { "一键启动/停止 dsh，以及 token 消耗、缓存命中、解码速度、会话数。", "运行时长、进程 PID、启动时间与原始标记行。" },
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
            string outp = cli == null ? "未找到工具箱 CLI。" : await System.Threading.Tasks.Task.Run(delegate { return Run(cli, args); });
            _actionLog = label + "结果：" + Environment.NewLine + outp.Trim();
            Refresh();
        }
        /// <summary>看板上的操作日志（一键启动/停止的结果，原样展示给用户）。</summary>
        private string _actionLog = "";
        public string ActionLog { get { return _actionLog; } }

        /// <summary>一键启动 dsh（调用工具箱核心的 `start`：非交互，GUI 用）。</summary>
        public void StartDsh() { RunCoreAction("start", "启动"); }

        /// <summary>停止 dsh（核心的 `stop`）。</summary>
        public void StopDsh() { RunCoreAction("stop", "停止"); }

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
        public void LoadHealth()
        {
            if (!string.IsNullOrEmpty(_health)) { BuildShell(); return; }
            string core = ToolkitCore();
            if (core == null) { _health = CoreMissingText(); BuildShell(); return; }
            _health = Run(core, "profilecheck");
            _doctor = SummaryMarkers.ParseDoctor(Run(core, "doctor"));
            BuildShell();
        }

        /// <summary>隔离/恢复一个插件条目：调用工具箱核心的 profilepatch（写操作：它会先备份、再改、失败逐字节回滚）。</summary>
        public void PatchEntry(string profile, string entryId, bool disable)
        {
            string core = ToolkitCore();
            if (core == null) { _actionLog = "无法执行隔离：" + CoreMissingText(); BuildShell(); return; }
            string yaml = Path.Combine(Path.Combine(_profilesRoot, profile), "cordis.patch.yml");
            string args = "profilepatch --file " + yaml + " --id " + entryId + (disable ? " --disable" : " --set disabled=false") + " --yes";
            string outp = Run(core, args);
            _health = "profilepatch " + (disable ? "--disable" : "--set disabled=false") + " " + entryId + " 的结果：" + Environment.NewLine + outp;
            BuildShell();
        }

        /// <summary>在文件管理器里打开插件目录（Windows 资源管理器 / Linux 文件管理器）。</summary>
        public void OpenFolder(string path)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(path);
                psi.UseShellExecute = true;
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
            body.Content = Shells.Shells.Build(_shell, this);
        }

        private bool _busy;
        public void Refresh() { if (_busy) return; _busy = true; _ = RefreshAsync(); }   // 重入保护：刷新期间再点不叠加   // 异步：CLI 调用不占 UI 线程

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            string cli = CliPath();
            if (cli == null)
            {
                _data = null;
                _rawOutput = "未找到工具箱 CLI。请把 dsht.exe / dsht_v3.exe 放到本程序同目录，或设置环境变量 DSHT_CLI 指向它。";
                BuildShell();
                return;
            }

            if (IsOverviewLike)
            {
                _rawOutput = await System.Threading.Tasks.Task.Run(delegate { return Run(cli, "status --detail"); });
                _status = StatusMarkers.Parse(_rawOutput);
                // 概览页顺带把这几样也取回来（都很快，且都是只读）
                string pfText = await System.Threading.Tasks.Task.Run(delegate { return Run(cli, "profiles"); });
                _profiles = ProfilesMarkers.Parse(pfText);
                string seText = await System.Threading.Tasks.Task.Run(delegate { return Run(cli, "sessions"); });
                _data = SessionsMarkers.Parse(seText);
                _backups = SummaryMarkers.ParseBackups(await System.Threading.Tasks.Task.Run(delegate { return Run(cli, "backup-list"); }));
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
                    if (d != null && d.Parent != null) return Path.Combine(d.Parent.FullName, "profiles");
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
                ? new string[] { "dsht.exe", "dsht_v3.exe", "DeepSeek Harness Toolkit.exe" }
                : new string[] { "dsht", "dsht_v3", "DeepSeek Harness Toolkit" };   // Unix 的 apphost 没有扩展名
            for (int i = 0; i < names.Length; i++)
            {
                string p = Path.Combine(dir, names[i]);
                if (File.Exists(p)) return p;
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
                    if (!string.IsNullOrEmpty(err)) sb.Append(Environment.NewLine).Append("[stderr] ").Append(err);
                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                return "运行 CLI 失败: " + ex.Message;
            }
        }
    }
}
