using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Dsht.Gui.Avalonia.Markers;
using Dsht.Gui.Avalonia.Shells;
using Dsht.Gui.Avalonia.ViewModels;

namespace Dsht.Gui.Avalonia
{
    /// <summary>主窗口：只负责"取数据 + 切布局"，界面由 Shells 里的四个布局框架构建。
    /// 纪律：**GUI 只是呈现适配器** —— 不引用核心程序集，只运行 CLI 并解析标记行（V3.0 方案 §7.3）。
    /// 设计方向参考了 March7thAssistant（GPL-3.0）的做法，**未复制其任何代码、图标、字体或图片资源**。</summary>
    public partial class MainWindow : Window
    {
        private SessionsSnapshot _data;
        private List<SessionRowVm> _rows = new List<SessionRowVm>();
        private int _shell;
        private int _filter = SessionsView.FilterAll;
        private string _failText = "";

        /// <summary>主从式布局的右侧详情容器（由 Shells 注入）。</summary>
        public StackPanel DetailHost;

        public MainWindow()
        {
            InitializeComponent();
            for (int i = 0; i < 4; i++) BindShell(i);
            Refresh();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // ---------------- 对外给 Shells 用的状态 ----------------

        public SessionsSnapshot Data { get { return _data; } }
        public List<SessionRowVm> Rows { get { return _rows; } }
        public int SortMode { get; set; }

        public string SubtitleText
        {
            get { return "读取 dsh 自己的会话投影：token 用量、缓存命中率、解码速度、上下文压力。"; }
        }

        public string SourceText
        {
            get
            {
                if (_data == null) return "数据来源：—";
                return _data.SourceText + "　投影目录：" + _data.Root + "　（GUI 不引用核心程序集，只解析 CLI 标记行）";
            }
        }

        public void SetFilter(int mode)
        {
            _filter = mode;
            Rerender();
        }

        /// <summary>按当前过滤/排序重建行集合，然后重画当前布局。</summary>
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

        // ---------------- 数据 ----------------

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
            if (hint != null) hint.Text = "当前：" + Shells.Shells.Name(_shell) + "　（点上面的按钮实时切换，内容与数据完全相同）";

            if (_data == null || !_data.Ok)
            {
                body.Content = new TextBox
                {
                    IsReadOnly = true,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    Text = string.IsNullOrEmpty(_failText) ? "正在读取…" : _failText,
                    Margin = new Thickness(20),
                    FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace")
                };
                return;
            }
            DetailHost = null;
            body.Content = Shells.Shells.Build(_shell, this);
        }

        public void Refresh()
        {
            string cli = CliPath();
            if (cli == null)
            {
                _data = null;
                _failText = "未找到工具箱 CLI。请把 dsht.exe / dsht_v3.exe 放到本程序同目录，或设置环境变量 DSHT_CLI 指向它。";
                BuildShell();
                return;
            }
            string text = Run(cli, "sessions");
            _data = SessionsMarkers.Parse(text);
            if (!_data.Ok)
            {
                _failText = string.IsNullOrEmpty(_data.FailReason)
                    ? text
                    : "读取会话投影失败：" + _data.FailReason + Environment.NewLine + Environment.NewLine + text;
            }
            else
            {
                _failText = "";
                List<SessionRow> rows = SessionsView.Sort(SessionsView.Filter(_data.Rows, _filter), SortMode);
                SessionsView.AttachBars(rows);
                List<SessionRowVm> vms = new List<SessionRowVm>();
                for (int i = 0; i < rows.Count; i++) vms.Add(new SessionRowVm(rows[i]));
                _rows = vms;
            }
            BuildShell();
        }

        /// <summary>找 CLI：环境变量 DSHT_CLI → 同目录的 dsht.exe / dsht_v3.exe → v2.x 的核心 exe。</summary>
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

        /// <summary>运行 CLI 并取回输出（超时 20 秒；失败也把 stderr 带回来，不静默）。</summary>
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
                    if (!p.WaitForExit(20000)) { try { p.Kill(); } catch { } return "（超时 20 秒）" + Environment.NewLine + sb; }
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
