using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
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
        public static readonly string[] NavItems = new string[] { "状态", "会话与 Token", "形态与插件", "备份", "体检", "配置", "说明" };
        /// <summary>主菜单图标（FluentIcons，编译期检查）。</summary>
        public static readonly FluentIcons.Common.Symbol[] NavIcons = new FluentIcons.Common.Symbol[]
        {
            FluentIcons.Common.Symbol.Home, FluentIcons.Common.Symbol.ChartMultiple, FluentIcons.Common.Symbol.PuzzlePiece,
            FluentIcons.Common.Symbol.Archive, FluentIcons.Common.Symbol.Shield, FluentIcons.Common.Symbol.Settings, FluentIcons.Common.Symbol.Question
        };
        private static readonly string[][] NavCli = new string[][]
        {
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
            new string[] { "总览", "缓存命中率", "解码速度", "上下文压力" },
            new string[] { "原始输出" },
            new string[] { "原始输出" },
            new string[] { "原始输出" },
            new string[] { "原始输出" },
            new string[] { "原始输出" }
        };
        private static readonly string[][] NavDesc = new string[][]
        {
            new string[] { "dsh 是否在跑、跑在哪个端口、启动时间 —— 只依据可观测事实。", "status --detail 的原始标记行。" },
            new string[] { "总览：会话数、token、缓存命中、解码速度、上下文压力。", "按缓存命中率从低到高排 —— 最该优化的排最前。", "按解码速度从快到慢排。", "按上下文压力从高到低排 —— 最接近压缩的排最前。" },
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
            InitChrome();
            for (int i = 0; i < 5; i++) BindShell(i);
            for (int i = 0; i < 4; i++) BindStyle(i);
            Refresh();
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
            if (core == null) { _health = "未找到工具箱核心程序（DeepSeek Harness Toolkit.exe）——健康检查需要它。"; BuildShell(); return; }
            _health = Run(core, "profilecheck");
            _doctor = SummaryMarkers.ParseDoctor(Run(core, "doctor"));
            BuildShell();
        }

        /// <summary>隔离/恢复一个插件条目：调用工具箱核心的 profilepatch（写操作：它会先备份、再改、失败逐字节回滚）。</summary>
        public void PatchEntry(string profile, string entryId, bool disable)
        {
            string core = ToolkitCore();
            if (core == null) { _health = "未找到工具箱核心程序（DeepSeek Harness Toolkit.exe），无法执行隔离。"; BuildShell(); return; }
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
                _health = "打开目录失败：" + ex.Message + Environment.NewLine + path;
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
        public bool IsSessionsSection { get { return _mainSection == 1; } }
        public string RawOutput { get { return _rawOutput; } }
        public string[] SubTabs { get { return NavSubs[_mainSection]; } }

        public string PageTitle
        {
            get { return NavItems[_mainSection].Replace("　", " ").Trim(); }
        }

        public string SubtitleText
        {
            get
            {
                string[] d = NavDesc[_mainSection];
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
                SortMode = idx == 1 ? 2 : (idx == 2 ? 4 : (idx == 3 ? 3 : 0));
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

        public void Refresh()
        {
            string cli = CliPath();
            if (cli == null)
            {
                _data = null;
                _rawOutput = "未找到工具箱 CLI。请把 dsht.exe / dsht_v3.exe 放到本程序同目录，或设置环境变量 DSHT_CLI 指向它。";
                BuildShell();
                return;
            }

            if (_mainSection == 0)
            {
                _rawOutput = Run(cli, "status --detail");
                _status = StatusMarkers.Parse(_rawOutput);
                // 概览页顺带把这几样也取回来（都很快，且都是只读）
                _profiles = ProfilesMarkers.Parse(Run(cli, "profiles"));
                _data = SessionsMarkers.Parse(Run(cli, "sessions"));
                _backups = SummaryMarkers.ParseBackups(Run(cli, "backup-list"));
                if (_doctor == null) _doctor = new DoctorSummary();
                BuildShell();
                return;
            }
            if (_mainSection == 2)
            {
                _rawOutput = Run(cli, "profiles");
                _profiles = ProfilesMarkers.Parse(_rawOutput);
                for (int i = 0; i < _rawOutput.Length && _profilesRoot.Length == 0; i++) { }
                _profilesRoot = ProfilesRootFrom(cli);
                BuildShell();
                return;
            }
            if (!IsSessionsSection)
            {
                _rawOutput = Run(cli, string.Join(" ", NavCli[_mainSection]));
                BuildShell();
                return;
            }

            string text = Run(cli, "sessions");
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
            string[] names = new string[] { "dsht.exe", "dsht_v3.exe", "DeepSeek Harness Toolkit.exe" };
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
                    sb.Append(p.StandardOutput.ReadToEnd());
                    string err = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(30000)) { try { p.Kill(); } catch { } return "（超时 30 秒）" + Environment.NewLine + sb; }
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
