using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Dsht.Gui.Avalonia
{
    /// <summary>跨平台 GUI 主窗口（骨架）。
    /// 设计纪律：**GUI 只是呈现适配器** —— 它不引用核心程序集，只运行 CLI 并解析标记行；
    /// 因此换 UI 不影响核心、换核心也不影响 UI（V3.0 方案 §7.3）。</summary>
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

        /// <summary>按当前选中的导航项运行对应 CLI 命令，把标记行原样显示出来（骨架阶段不做美化）。</summary>
        private void Refresh()
        {
            var nav = this.FindControl<ListBox>("NavList");
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
                output.Text = "未找到工具箱 CLI（把 dsht.exe / dsht_v3.exe 放到本 GUI 同目录，或用 --cli <路径> 指定）。";
                if (status != null) status.Text = "CLI: 未找到";
                return;
            }
            string text = Run(cli, args);
            output.Text = text;
            if (status != null) status.Text = "CLI: " + Path.GetFileName(cli) + "  " + args;
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
