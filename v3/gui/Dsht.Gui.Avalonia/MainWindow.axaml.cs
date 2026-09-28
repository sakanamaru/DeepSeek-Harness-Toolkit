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
        public static readonly string[] NavItems = new string[] { "▤　状态", "◔　会话与 Token", "⚙　形态与插件", "▣　备份", "✚　体检", "≡　配置", "？　说明" };
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
            for (int i = 0; i < 5; i++) BindShell(i);
            Refresh();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // ---------------- 给 Shells 用的状态 ----------------

        public SessionsSnapshot Data { get { return _data; } }
        public ProfilesSnapshot Profiles { get { return _profiles; } }
        public List<SessionRowVm> Rows { get { return _rows; } }
        public int SortMode { get; set; }
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
            TextBlock hint = this.FindControl<TextBlock>("ShellHint");
            if (hint != null) hint.Text = "当前：" + Shells.Shells.Name(_shell) + "　（点按钮切换布局；左侧主菜单 + 顶部子菜单都已接通）";
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

            if (_mainSection == 2)
            {
                _rawOutput = Run(cli, "profiles");
                _profiles = ProfilesMarkers.Parse(_rawOutput);
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
