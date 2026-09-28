using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Dsht.Gui.Avalonia.Markers;

namespace Dsht.Gui.Avalonia
{
    /// <summary>跨平台 GUI 主窗口。
    /// 设计纪律：**GUI 只是呈现适配器** —— 它不引用核心程序集，只运行 CLI 并解析标记行；
    /// 因此换 UI 不影响核心、换核心也不影响 UI（V3.0 方案 §7.3）。
    /// 数据来源的诚实边界由 CLI 的标记行给出（`SESSIONS_SOURCE`），界面原样转述，不美化。</summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var nav = this.FindControl<ListBox>("NavList");
            var refresh = this.FindControl<Button>("RefreshBtn");
            if (nav != null) nav.SelectionChanged += delegate { Refresh(); };
            if (refresh != null) refresh.Click += delegate(object s, RoutedEventArgs e) { Refresh(); };
            Refresh();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>按当前选中的导航项运行对应 CLI 命令：
        /// 会话页走结构化面板，其它页面原样展示标记行（骨架阶段）。</summary>
        private void Refresh()
        {
            var nav = this.FindControl<ListBox>("NavList");
            var pane = this.FindControl<Grid>("SessionsPane");
            var output = this.FindControl<TextBox>("Output");
            var title = this.FindControl<TextBlock>("PageTitle");
            var status = this.FindControl<TextBlock>("StatusLine");
            if (nav == null || output == null) return;

            string args = "status --detail";
            string label = "状态 / Status";
            if (nav.SelectedItem is ListBoxItem item)
            {
                if (item.Tag is string t) args = t;
                if (item.Content is string c) label = c;
            }
            if (title != null) title.Text = label;

            string cli = CliPath();
            if (cli == null)
            {
                if (pane != null) pane.IsVisible = false;
                output.IsVisible = true;
                output.Text = "未找到工具箱 CLI（把 dsht.exe / dsht_v3.exe 放到本 GUI 同目录，或用环境变量 DSHT_CLI 指定）。";
                if (status != null) status.Text = "CLI: 未找到";
                return;
            }

            bool sessions = string.Equals(args.Trim(), "sessions", StringComparison.Ordinal);
            if (pane != null) pane.IsVisible = sessions;
            output.IsVisible = !sessions;

            string text = Run(cli, args);
            if (sessions) RenderSessions(text);
            else output.Text = text;

            if (status != null) status.Text = "CLI: " + Path.GetFileName(cli) + "  " + args;
        }

        /// <summary>把 `sessions` 的标记行渲染成面板（解析失败/无数据时**如实说明**，不显示假表格）。</summary>
        private void RenderSessions(string cliOutput)
        {
            var output = this.FindControl<TextBox>("Output");
            SessionsSnapshot snap = SessionsMarkers.Parse(cliOutput);
            if (!snap.Ok)
            {
                var pane = this.FindControl<Grid>("SessionsPane");
                if (pane != null) pane.IsVisible = false;
                if (output != null)
                {
                    output.IsVisible = true;
                    output.Text = string.IsNullOrEmpty(snap.FailReason)
                        ? cliOutput
                        : "读取会话投影失败：" + snap.FailReason + Environment.NewLine + Environment.NewLine + cliOutput;
                }
                return;
            }

            SetText("SumSessions", "会话 " + snap.Count + "（非空 " + snap.NonBlank + "）");
            SetText("SumLive", "运行中 " + snap.Live);
            SetText("SumTokens", "输入 " + N(snap.TotalIn) + " · 输出 " + N(snap.TotalOut) + " · 缓存读 " + N(snap.TotalCacheRead));
            SetText("SumHit", "缓存命中 " + Pct(snap.TotalHitPercent));
            SetText("SumDecode", "解码 " + Tps(snap.TotalDecodeTps));
            SetText("SessionsSource", snap.SourceText + "　投影目录：" + snap.Root);

            var list = this.FindControl<ItemsControl>("SessionList");
            if (list != null) list.ItemsSource = snap.Rows;
        }

        private void SetText(string name, string text)
        {
            var tb = this.FindControl<TextBlock>(name);
            if (tb != null) tb.Text = text;
        }

        private static string N(long v) { return v.ToString("N0", CultureInfo.InvariantCulture); }

        private static string Pct(double v)
        {
            return v < 0 ? "unknown" : v.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        private static string Tps(double v)
        {
            return v < 0 ? "unknown" : v.ToString("0.0", CultureInfo.InvariantCulture) + " tok/s";
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
