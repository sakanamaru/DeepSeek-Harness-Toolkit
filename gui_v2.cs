// ============================================================================
//  DeepSeek Harness Toolkit GUI  ——  GUI 辅助面板（P2：进程层 + 状态轮询）
//  ----------------------------------------------------------------------------
//  架构：本体不变，GUI 通过非交互 CLI 协同（backup/restore/status/start --bg/stop/shortcut，
//        install/update/uninstall 弹可见窗口交互）。
//  本文件 = 无边框窗口 + 深/浅主题 + 中英双语 + 三页切换 + 状态灯 + 完整进程层。
//  约束：C#5 / .NET 4.x / 零第三方依赖（仅 WinForms + GDI+）。
//  发布：单独 Toolkit GUI.exe（与核心 DeepSeek Harness Toolkit.exe 同目录运行）。
//  v1 脚本协助：SOGR-Momono Dango（QwenPaw/DeepseekAPI-V4-Flash-0731）
//  v2 重构封装：DeepSeek DSH（DSH/DeepseekAPI-V4-Flash-0731）
// ============================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// ---------------- 主题 ----------------

class Theme
{
    public Color Bg;
    public Color Panel;
    public Color PanelAlt;
    public Color Fg;
    public Color FgDim;
    public Color Accent;
    public Color AccentFg;
    public Color Border;
    public Color Hover;
    public Color LedOk;
    public Color LedWarn;
    public Color LedBad;
    public Color LedIdle;

    public static readonly Theme Dark = MakeDark();
    public static readonly Theme Light = MakeLight();

    static Theme MakeDark()
    {
        var t = new Theme();
        t.Bg = Color.FromArgb(0x1E, 0x21, 0x2B);
        t.Panel = Color.FromArgb(0x26, 0x2A, 0x36);
        t.PanelAlt = Color.FromArgb(0x2E, 0x33, 0x40);
        t.Fg = Color.FromArgb(0xE8, 0xEA, 0xF0);
        t.FgDim = Color.FromArgb(0x9A, 0xA1, 0xB1);
        t.Accent = Color.FromArgb(0x4C, 0x8D, 0xFF);
        t.AccentFg = Color.White;
        t.Border = Color.FromArgb(0x3A, 0x40, 0x50);
        t.Hover = Color.FromArgb(0x33, 0x39, 0x49);
        t.LedOk = Color.FromArgb(0x3D, 0xDC, 0x84);
        t.LedWarn = Color.FromArgb(0xF5, 0xC5, 0x42);
        t.LedBad = Color.FromArgb(0xF0, 0x62, 0x6E);
        t.LedIdle = Color.FromArgb(0x5A, 0x61, 0x72);
        return t;
    }

    static Theme MakeLight()
    {
        var t = new Theme();
        t.Bg = Color.White;
        t.Panel = Color.FromArgb(0xF2, 0xF4, 0xF8);
        t.PanelAlt = Color.FromArgb(0xE9, 0xED, 0xF3);
        t.Fg = Color.FromArgb(0x1A, 0x1E, 0x2A);
        t.FgDim = Color.FromArgb(0x6B, 0x74, 0x82);
        t.Accent = Color.FromArgb(0x2E, 0x6A, 0xE6);
        t.AccentFg = Color.White;
        t.Border = Color.FromArgb(0xD8, 0xDD, 0xE6);
        t.Hover = Color.FromArgb(0xE0, 0xE5, 0xEE);
        t.LedOk = Color.FromArgb(0x22, 0xA6, 0x5C);
        t.LedWarn = Color.FromArgb(0xE0, 0xA8, 0x20);
        t.LedBad = Color.FromArgb(0xD9, 0x44, 0x4E);
        t.LedIdle = Color.FromArgb(0xC3, 0xCA, 0xD4);
        return t;
    }
}

// ---------------- 双语 ----------------

static class L10N
{
    static bool zh = true;
    static readonly Dictionary<string, string[]> M = new Dictionary<string, string[]>();

    static void Add(string k, string z, string e) { M[k] = new string[] { z, e }; }
    public static bool IsZh { get { return zh; } }
    public static void SetLang(bool chinese) { zh = chinese; }
    public static void Toggle() { zh = !zh; }
    public static string _(string k)
    {
        string[] v;
        if (M.TryGetValue(k, out v)) return zh ? v[0] : v[1];
        return k;
    }

    static L10N()
    {
        Add("app.title", "DeepSeek Harness Toolkit GUI", "DeepSeek Harness Toolkit GUI");
        Add("nav.home", "首页", "Home");
        Add("nav.log", "日志", "Log");
        Add("nav.about", "关于", "About");

        Add("home.status.title", "服务状态", "Service Status");
        Add("home.status.up", "运行中", "RUNNING");
        Add("home.status.starting", "启动中", "STARTING");
        Add("home.status.down", "已停止", "STOPPED");
        Add("home.status.unknown", "未检测", "UNKNOWN");
        Add("home.address", "Web 地址", "Web Address");
        Add("home.version", "dsh 版本", "dsh Version");

        Add("act.refresh", "刷新状态", "Refresh");
        Add("act.install", "安装 dsh", "Install dsh");
        Add("act.start", "启动 Web", "Start Web");
        Add("act.stop", "停止服务", "Stop Service");
        Add("act.backup", "立即备份", "Backup Now");
        Add("act.restore", "恢复备份", "Restore Backup");
        Add("act.update", "检查 / 更新", "Check / Update");
        Add("act.uninstall", "卸载 dsh", "Uninstall dsh");
        Add("act.shortcut", "桌面快捷方式", "Desktop Shortcut");

        Add("log.title", "操作日志", "Operation Log");
        Add("log.clear", "清空", "Clear");
        Add("log.empty", "（暂无日志）", "(no log yet)");

        Add("about.copy", "非官方工具箱 · 独立于 DeepSeek 官方", "Unofficial toolkit · independent of DeepSeek");
        Add("disclaimer", "非官方项目，与 DeepSeek 官方无隶属关系。",
            "This is an unofficial project and is not affiliated with DeepSeek.");

        Add("theme.toggle", "主题", "Theme");
        Add("lang.toggle", "语言", "Lang");

        Add("ph.notyet", "「{0}」将在下一步（P2）接入。", "\"{0}\" will be wired up in the next step (P2).");
        Add("ph.refreshing", "正在检测服务状态…", "Detecting service status...");

        // P2 进程层文案
        Add("op.running", "执行「{0}」…", "Running \"{0}\"...");
        Add("op.ok", "完成", "OK");
        Add("op.fail", "失败", "Failed");
        Add("op.timeout", "超时", "Timed out");
        Add("op.busy", "上一操作仍在进行，请稍候…", "Previous operation still running, please wait...");
        Add("op.coremissing", "未找到核心程序（DeepSeek Harness Toolkit.exe，请与 GUI 同目录）",
            "Core exe not found (DeepSeek Harness Toolkit.exe, place it next to the GUI)");
        Add("op.launchfailed", "启动失败：", "Launch failed: ");
        Add("dsh.notinstalled", "未安装", "not installed");
        Add("dsh.verreadfail", "读取失败", "read failed");
    }
}

// ---------------- 进程调用结果 ----------------

class CoreRunResult
{
    public int ExitCode = -1;
    public string FirstLine = "";   // 单行标记（BACKUP_OK / STATUS_UP / START_OK ...）
    public string All = "";         // 完整 stdout(+stderr)（日志用）
    public bool TimedOut = false;
}

// ---------------- 状态灯（自绘） ----------------

enum SKind { Unknown, Up, Starting, Down }

class Led : Control
{
    SKind kind = SKind.Unknown;
    Theme th = Theme.Dark;
    Color bg = Color.Transparent;

    public Led()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Width = 16; Height = 16;
    }

    public void Set(SKind k) { kind = k; Invalidate(); }
    public void SetTheme(Theme t, Color cardBg) { th = t; bg = cardBg; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        // 先填充父卡片背景色（不依赖透明，常规 Panel 父控件上也能正确绘制）
        using (SolidBrush bgB = new SolidBrush(bg == Color.Transparent ? th.PanelAlt : bg))
            g.FillRectangle(bgB, 0, 0, Width, Height);
        Color c = th.LedIdle;
        if (kind == SKind.Up) c = th.LedOk;
        else if (kind == SKind.Starting) c = th.LedWarn;
        else if (kind == SKind.Down) c = th.LedBad;
        using (SolidBrush b = new SolidBrush(c))
        {
            int r = Math.Min(Width, Height) - 6;
            if (r < 4) r = 4;
            int x = (Width - r) / 2;
            int y = (Height - r) / 2;
            g.FillEllipse(b, x, y, r, r);
        }
    }
}

// ---------------- 主窗体 ----------------

public class App : Form
{
    bool dark = true;
    Theme Th { get { return dark ? Theme.Dark : Theme.Light; } }

    // 标题栏
    Panel titleBar;
    Button btnMin, btnClose, btnTheme, btnLang;
    Label lblTitle;

    // 导航
    Panel nav;
    Button[] navBtns;
    Panel content;
    Panel[] pages;   // 0=home 1=log 2=about

    // 首页
    Led led;
    Label lblStatusText, lblWebAddr, lblDshVer;
    TableLayoutPanel actionGrid;

    // 操作按钮字典（key → Button，用于禁用态管理）
    Dictionary<string, Button> actBtns = new Dictionary<string, Button>();

    // 日志页
    TextBox txtLog;
    Button btnClearLog;

    // 关于页
    PictureBox picLogo;

    // 底部
    Label lblDisclaimer;

    // 拖拽
    Point dragStart;

    // 状态刷新防重入
    int refreshing = 0;

    // 操作忙碌（任一操作进行中禁用所有操作按钮，防重入）
    int busy = 0;

    // 3 秒状态轮询
    System.Windows.Forms.Timer pollTimer;

    // 当前 dsh 版本（轮询顺带读取）
    string dshVer = "";

    public App()
    {
        Text = L10N._("app.title");
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(940, 640);
        MinimumSize = new Size(760, 520);
        BackColor = Th.Bg;
        Font = new Font("Microsoft YaHei UI", 9.5f);

        Build();
        ApplyTheme();
        ApplyLang();
        ShowPage(0);
        RefreshStatus();

        // 3 秒状态轮询（定时器，不阻塞 UI）
        pollTimer = new System.Windows.Forms.Timer();
        pollTimer.Interval = 3000;
        pollTimer.Tick += delegate(object s, EventArgs e) { RefreshStatus(); };
        pollTimer.Start();
    }

    void Build()
    {
        // ---- 标题栏 ----
        titleBar = new Panel();
        titleBar.Dock = DockStyle.Top;
        titleBar.Height = 46;
        titleBar.Cursor = Cursors.SizeAll;
        titleBar.MouseDown += delegate(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragStart = new Point(e.X, e.Y);
                titleBar.Capture = true;
            }
        };
        titleBar.MouseMove += delegate(object s, MouseEventArgs e)
        {
            if (titleBar.Capture && e.Button == MouseButtons.Left)
            {
                Left += e.X - dragStart.X;
                Top += e.Y - dragStart.Y;
            }
        };
        titleBar.MouseUp += delegate(object s, MouseEventArgs e) { titleBar.Capture = false; };

        lblTitle = new Label();
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
        lblTitle.Location = new Point(16, 12);
        titleBar.Controls.Add(lblTitle);

        btnTheme = MakeTitleBtn("🌓", 0);
        btnLang = MakeTitleBtn("🌐", 1);
        btnMin = MakeTitleBtn("－", 2);
        btnClose = MakeTitleBtn("✕", 3);

        // ---- 主体（Fill 最先 Add → 逆序 dock 时最后布局，填满剩余空间，不被边缘控件覆盖）----
        Panel body = new Panel();
        body.Dock = DockStyle.Fill;
        Controls.Add(body);

        // 导航（左侧）与内容：Fill 先 Add、Left 后 Add（逆序 dock：nav 先占左，content 再填右）
        nav = new Panel();
        nav.Dock = DockStyle.Left;
        nav.Width = 176;

        content = new Panel();
        content.Dock = DockStyle.Fill;
        body.Controls.Add(content);
        body.Controls.Add(nav);

        // 标题栏（Top 后 Add → 逆序 dock 时先布局，占顶部）
        Controls.Add(titleBar);

        pages = new Panel[3];
        pages[0] = BuildHome();
        pages[1] = BuildLog();
        pages[2] = BuildAbout();
        foreach (Panel p in pages)
        {
            p.Dock = DockStyle.Fill;
            p.Visible = false;
            content.Controls.Add(p);
        }

        // 导航按钮（绝对定位，避免 Dock.Top 逆序）
        string[] navKeys = new string[] { "nav.home", "nav.log", "nav.about" };
        navBtns = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            Button b = new Button();
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.Location = new Point(0, 8 + i * 48);
            b.Size = new Size(176, 48);
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Padding = new Padding(20, 0, 0, 0);
            b.Click += delegate(object s, EventArgs e) { ShowPage(idx); };
            navBtns[i] = b;
            nav.Controls.Add(b);
        }
        nav.Resize += delegate
        {
            for (int i = 0; i < navBtns.Length; i++)
                navBtns[i].Width = Math.Max(0, nav.ClientSize.Width);
        };

        // ---- 底部 disclaimer ----
        lblDisclaimer = new Label();
        lblDisclaimer.Dock = DockStyle.Bottom;
        lblDisclaimer.Height = 30;
        lblDisclaimer.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(lblDisclaimer);
    }

    Button MakeTitleBtn(string glyph, int orderFromRight)
    {
        Button b = new Button();
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.Size = new Size(44, 34);
        b.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        b.Text = glyph;
        b.Font = new Font("Segoe UI Symbol", 11f);
        b.Location = new Point(Width - 14 - 44 * (orderFromRight + 1) - 6 * orderFromRight, 6);
        b.Click += delegate(object s, EventArgs e) { TitleAct(orderFromRight); };
        titleBar.Controls.Add(b);
        return b;
    }

    void TitleAct(int id)
    {
        if (id == 0) { dark = !dark; ApplyTheme(); }
        else if (id == 1) { L10N.Toggle(); ApplyLang(); }
        else if (id == 2) { WindowState = FormWindowState.Minimized; }
        else { Close(); }
    }

    // ---- 首页 ----
    Panel BuildHome()
    {
        Panel p = new Panel();

        // 状态卡片
        Panel card = new Panel();
        card.Location = new Point(24, 20);
        card.Size = new Size(520, 150);
        card.Padding = new Padding(20);
        p.Controls.Add(card);

        Label lblStatusTitle = new Label();
        lblStatusTitle.AutoSize = true;
        lblStatusTitle.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
        lblStatusTitle.Location = new Point(0, 0);
        lblStatusTitle.Text = L10N._("home.status.title");
        lblStatusTitle.Tag = "status.title";
        card.Controls.Add(lblStatusTitle);

        led = new Led();
        led.Location = new Point(0, 34);
        card.Controls.Add(led);

        lblStatusText = new Label();
        lblStatusText.AutoSize = true;
        lblStatusText.Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold);
        lblStatusText.Location = new Point(30, 26);
        lblStatusText.Text = L10N._("home.status.unknown");
        lblStatusText.Tag = "status.text";
        card.Controls.Add(lblStatusText);

        lblWebAddr = new Label();
        lblWebAddr.AutoSize = true;
        lblWebAddr.Location = new Point(0, 78);
        lblWebAddr.Text = L10N._("home.address") + ": http://127.0.0.1:3080";
        lblWebAddr.Tag = "webaddr";
        card.Controls.Add(lblWebAddr);

        lblDshVer = new Label();
        lblDshVer.AutoSize = true;
        lblDshVer.Location = new Point(0, 106);
        lblDshVer.Text = L10N._("home.version") + ": —";
        lblDshVer.Tag = "dshver";
        card.Controls.Add(lblDshVer);

        // 操作按钮区（8 操作按钮 2 列×4 行 + 刷新跨 2 列）
        actionGrid = new TableLayoutPanel();
        actionGrid.ColumnCount = 2;
        actionGrid.RowCount = 5;
        actionGrid.Location = new Point(24, 190);
        actionGrid.Size = new Size(520, 310);
        for (int c = 0; c < 2; c++) actionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        for (int r = 0; r < 5; r++) actionGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
        p.Controls.Add(actionGrid);

        AddActionButton(0, 0, "act.install", 1);
        AddActionButton(1, 0, "act.start", 1);
        AddActionButton(0, 1, "act.stop", 1);
        AddActionButton(1, 1, "act.backup", 1);
        AddActionButton(0, 2, "act.restore", 1);
        AddActionButton(1, 2, "act.update", 1);
        AddActionButton(0, 3, "act.uninstall", 1);
        AddActionButton(1, 3, "act.shortcut", 1);
        AddActionButton(0, 4, "act.refresh", 2);   // 刷新跨 2 列

        return p;
    }

    void AddActionButton(int col, int row, string key, int colSpan)
    {
        Button b = new Button();
        b.FlatStyle = FlatStyle.Flat;
        b.Dock = DockStyle.Fill;
        b.Margin = new Padding(6);
        b.Tag = key;
        b.Click += delegate(object s, EventArgs e) { OnAction(key); };
        actionGrid.Controls.Add(b, col, row);
        if (colSpan > 1) actionGrid.SetColumnSpan(b, colSpan);
        actBtns[key] = b;
    }

    void OnAction(string key)
    {
        if (Interlocked.CompareExchange(ref busy, 1, 0) != 0) { LogLine(L10N._("op.busy")); return; }
        try
        {
            if (key == "act.refresh") { RefreshStatus(); return; }
            if (key == "act.install") { LaunchInteractive("install", key); return; }
            if (key == "act.update") { LaunchInteractive("update", key); return; }
            if (key == "act.uninstall") { LaunchInteractive("uninstall", key); return; }
            // 其余非交互：后台捕获单行标记（busy 延迟到 OnCaptureDone 清零）
            string args = "";
            if (key == "act.start") args = "start --bg";
            else if (key == "act.stop") args = "stop";
            else if (key == "act.backup") args = "backup";
            else if (key == "act.restore") args = "restore";
            else if (key == "act.shortcut") args = "shortcut";
            else { Interlocked.Exchange(ref busy, 0); return; }
            UpdateActionButtons();   // 立即禁用所有操作按钮
            LaunchCapture(args, key);
            return;   // busy 保持，由 OnCaptureDone 清零
        }
        catch { Interlocked.Exchange(ref busy, 0); }
        Interlocked.Exchange(ref busy, 0);   // 交互命令/刷新等同步路径到达此处
        UpdateActionButtons();
    }

    // ---- 进程层：可见窗口（交互命令 install/update/uninstall） ----
    // 铁律①：Process.Start(UseShellExecute=true) 立即返回，绝不在 UI 线程 WaitForExit。
    void LaunchInteractive(string args, string key)
    {
        string core = CoreExePath();
        if (core == null) { LogLine(L10N._("op.coremissing")); return; }
        LogLine(string.Format(L10N._("op.running"), L10N._(key)));
        try
        {
            var psi = new ProcessStartInfo(core, args)
            {
                UseShellExecute = true,               // 弹独立控制台窗口，用户直接在窗口内交互
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            };
            Process.Start(psi);
            LogLine(L10N._(key) + " → " + L10N._("op.ok"));
            // 交互命令结束后服务状态可能变化，稍后触发一次状态刷新
            ThreadPool.QueueUserWorkItem(delegate(object _)
            {
                Thread.Sleep(1500);
                BeginInvoke((Action)delegate { RefreshStatus(); });
            });
        }
        catch (Exception ex) { LogLine(L10N._("op.launchfailed") + ex.Message); }
    }

    // ---- 进程层：后台捕获（非交互命令） ----
    // 铁律②：先 WaitForExit(timeout) 再读输出。
    // 铁律③：stdout/stderr 各用独立后台线程排空，避免管道缓冲满死锁。
    void LaunchCapture(string args, string key)
    {
        string core = CoreExePath();
        if (core == null) { LogLine(L10N._("op.coremissing")); return; }
        LogLine(string.Format(L10N._("op.running"), L10N._(key)));
        int capTimeout = 30000;
        ThreadPool.QueueUserWorkItem(delegate(object _)
        {
            CoreRunResult r = RunCoreCapture(core, args, capTimeout);
            BeginInvoke((Action)delegate
            {
                OnCaptureDone(key, r);
                RefreshStatus();   // 操作后刷新状态灯 + dsh 版本
            });
        });
    }

    void OnCaptureDone(string key, CoreRunResult r)
    {
        if (r.TimedOut) { LogLine(L10N._(key) + " → " + L10N._("op.timeout")); Interlocked.Exchange(ref busy, 0); UpdateActionButtons(); return; }
        string line = (r.FirstLine ?? "").Trim();
        bool ok = false;
        if (key == "act.start") ok = line.StartsWith("START_OK");
        else if (key == "act.stop") ok = line.StartsWith("STOP_OK");
        else if (key == "act.backup") ok = line.StartsWith("BACKUP_OK");
        else if (key == "act.restore") ok = line.StartsWith("RESTORE_OK");
        else if (key == "act.shortcut") ok = line.StartsWith("SHORTCUT_OK");
        if (ok) LogLine(L10N._(key) + " → " + L10N._("op.ok") + "  (" + line + ")");
        else
        {
            string reason = string.IsNullOrEmpty(line) ? (string.IsNullOrEmpty(r.All) ? "" : r.All) : line;
            LogLine(L10N._(key) + " → " + L10N._("op.fail") + (string.IsNullOrEmpty(reason) ? "" : "  (" + reason + ")"));
        }
        Interlocked.Exchange(ref busy, 0);
        UpdateActionButtons();
    }

    string CoreExePath()
    {
        string core = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeepSeek Harness Toolkit.exe");
        return File.Exists(core) ? core : null;
    }

    // 在后台线程调用；返回单行标记 + 完整输出。
    CoreRunResult RunCoreCapture(string core, string args, int timeoutMs)
    {
        var res = new CoreRunResult();
        try
        {
            var psi = new ProcessStartInfo(core, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            };
            using (Process p = Process.Start(psi))
            {
                if (p == null) { res.All = L10N._("op.fail"); return res; }
                // 独立后台线程排空 stdout / stderr（铁律③）
                string stdoutAll = "";
                string stderrAll = "";
                Thread tOut = new Thread(delegate() { try { stdoutAll = p.StandardOutput.ReadToEnd(); } catch { } });
                Thread tErr = new Thread(delegate() { try { stderrAll = p.StandardError.ReadToEnd(); } catch { } });
                tOut.IsBackground = true; tErr.IsBackground = true;
                tOut.Start(); tErr.Start();
                // 铁律②：先等退出（超时则杀），不 ReadToEnd 同步阻塞
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                    res.TimedOut = true;
                    res.All = L10N._("op.timeout");
                    return res;
                }
                tOut.Join(2000); tErr.Join(2000);
                res.ExitCode = p.ExitCode;
                res.All = stdoutAll;
                if (!string.IsNullOrWhiteSpace(stderrAll)) res.All += (res.All.Length > 0 ? "\n" : "") + stderrAll;
                // 取第一行非空作为标记
                if (!string.IsNullOrEmpty(stdoutAll))
                {
                    string[] lines = stdoutAll.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 0) res.FirstLine = lines[0];
                }
                return res;
            }
        }
        catch (Exception ex) { res.All = ex.Message; return res; }
    }

    // ---- 日志页 ----
    Panel BuildLog()
    {
        Panel p = new Panel();
        Panel top = new Panel();
        top.Dock = DockStyle.Top;
        top.Height = 44;
        p.Controls.Add(top);

        Label t = new Label();
        t.AutoSize = true;
        t.Location = new Point(20, 12);
        t.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
        t.Text = L10N._("log.title");
        t.Tag = "log.title";
        top.Controls.Add(t);

        btnClearLog = new Button();
        btnClearLog.FlatStyle = FlatStyle.Flat;
        btnClearLog.FlatAppearance.BorderSize = 1;
        btnClearLog.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnClearLog.Size = new Size(72, 28);
        btnClearLog.Location = new Point(0, 8);
        btnClearLog.Click += delegate(object s, EventArgs e) { ClearLog(); };
        top.Controls.Add(btnClearLog);

        txtLog = new TextBox();
        txtLog.Multiline = true;
        txtLog.ReadOnly = true;
        txtLog.Dock = DockStyle.Fill;
        txtLog.ScrollBars = ScrollBars.Both;   // v1 审查 #11：横向也滚动
        txtLog.WordWrap = false;
        p.Controls.Add(txtLog);

        return p;
    }

    // ---- 关于页 ----
    Panel BuildAbout()
    {
        Panel p = new Panel();
        picLogo = new PictureBox();
        picLogo.Size = new Size(120, 120);
        picLogo.SizeMode = PictureBoxSizeMode.Zoom;
        picLogo.BackColor = Color.Transparent;
        picLogo.Location = new Point(0, 20);
        p.Controls.Add(picLogo);
        LoadLogo();

        Label name = new Label();
        name.AutoSize = true;
        name.Font = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold);
        name.Location = new Point(140, 40);
        name.Text = "DeepSeek Harness Toolkit";
        p.Controls.Add(name);

        Label ver = new Label();
        ver.AutoSize = true;
        ver.Location = new Point(142, 82);
        ver.Text = "GUI " + AssemblyVersion();
        p.Controls.Add(ver);

        Label copy = new Label();
        copy.AutoSize = true;
        copy.Location = new Point(142, 108);
        copy.Text = L10N._("about.copy");
        copy.Tag = "about.copy";
        p.Controls.Add(copy);

        Label cred = new Label();
        cred.AutoSize = true;
        cred.Location = new Point(0, 160);
        cred.Text = "v1 脚本协助 : SOGR-Momono Dango（QwenPaw/DeepseekAPI-V4-Flash-0731）\nv2 重构封装 : DeepSeek DSH（DSH/DeepseekAPI-V4-Flash-0731）";
        p.Controls.Add(cred);

        return p;
    }

    void LoadLogo()
    {
        try
        {
            string lp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");
            if (File.Exists(lp)) picLogo.Image = Image.FromFile(lp);
        }
        catch { }
    }

    string AssemblyVersion()
    {
        try { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.Major + "." + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.Minor + "." + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.Build; }
        catch { return "2.4.0"; }
    }

    // ---- 页面切换 ----
    void ShowPage(int idx)
    {
        for (int i = 0; i < pages.Length; i++)
            pages[i].Visible = (i == idx);
        if (pages[idx] == pages[1]) RefreshLogColors();
    }

    // ---- 主题 ----
    void ApplyTheme()
    {
        SuspendLayout();
        Theme t = Th;

        BackColor = t.Bg;
        titleBar.BackColor = t.Panel;
        lblTitle.ForeColor = t.Fg;
        nav.BackColor = t.Panel;
        content.BackColor = t.Bg;

        foreach (Button b in navBtns)
        {
            b.ForeColor = t.FgDim;
            b.BackColor = t.Panel;
            b.FlatAppearance.MouseOverBackColor = t.Hover;
            b.FlatAppearance.MouseDownBackColor = t.PanelAlt;
        }

        StyleTitleButton(btnTheme, t);
        StyleTitleButton(btnLang, t);
        StyleTitleButton(btnMin, t);
        StyleTitleButton(btnClose, t);

        foreach (Panel pg in pages) { pg.BackColor = t.Bg; ThemeRecurse(pg, t); }

        lblDisclaimer.BackColor = t.Panel;
        lblDisclaimer.ForeColor = t.FgDim;

        led.SetTheme(t, t.PanelAlt);
        ApplyStatusColor();

        // 日志页
        txtLog.BackColor = t.Panel;
        txtLog.ForeColor = t.Fg;
        StyleButton(btnClearLog, t);

        UpdateActionButtons();   // 禁用态着色后重新应用

        ResumeLayout();
        Invalidate(true);
    }

    // 递归着色容器里的已知控件类型（含卡片内 Label 等）
    void ThemeRecurse(Control parent, Theme t)
    {
        foreach (Control c in parent.Controls)
        {
            if (c is Panel)
            {
                // 卡片类面板（有背景色且非透明）统一着色
                if (c.BackColor != Color.Transparent && c == parent) continue;
                Panel pn = c as Panel;
                bool isCard = pn.Padding.Horizontal > 0 || (pn.Controls.Count > 0 && pn.BackColor != Color.Transparent);
                if (pn.Padding.Horizontal > 0) pn.BackColor = t.PanelAlt;
                ThemeRecurse(pn, t);
                continue;
            }
            if (c is Label)
            {
                c.ForeColor = t.Fg;
                // 状态标题等次标题用 Dim
            }
            if (c is Button)
            {
                Button b = c as Button;
                StyleButton(b, t);
            }
            if (c is TableLayoutPanel)
            {
                c.BackColor = t.Bg;
                ThemeRecurse(c, t);
            }
            if (c is TextBox)
            {
                c.BackColor = t.Panel;
                c.ForeColor = t.Fg;
            }
            ThemeRecurse(c, t);
        }
    }

    void StyleTitleButton(Button b, Theme t)
    {
        b.BackColor = t.Panel;
        b.ForeColor = t.FgDim;
        b.FlatAppearance.MouseOverBackColor = t.Hover;
        b.FlatAppearance.MouseDownBackColor = t.PanelAlt;
    }

    void StyleButton(Button b, Theme t)
    {
        b.BackColor = t.PanelAlt;
        b.ForeColor = b.Enabled ? t.Fg : t.FgDim;
        b.FlatAppearance.MouseOverBackColor = t.Hover;
        b.FlatAppearance.MouseDownBackColor = t.Panel;
        b.FlatAppearance.BorderColor = t.Border;
    }

    void ApplyStatusColor()
    {
        if (lblStatusText == null) return;
        SKind k = currentKind;
        Color c = Th.LedIdle;
        if (k == SKind.Up) c = Th.LedOk;
        else if (k == SKind.Starting) c = Th.LedWarn;
        else if (k == SKind.Down) c = Th.LedBad;
        lblStatusText.ForeColor = c;
    }

    // ---- 语言 ----
    void ApplyLang()
    {
        lblTitle.Text = L10N._("app.title");
        for (int i = 0; i < navBtns.Length; i++)
            navBtns[i].Text = L10N._(new string[] { "nav.home", "nav.log", "nav.about" }[i]);
        lblDisclaimer.Text = L10N._("disclaimer");

        // 状态页
        foreach (Control c in pages[0].Controls)
        {
            if (c is Label && c.Tag is string)
            {
                string tag = (string)c.Tag;
                if (tag == "status.title") c.Text = L10N._("home.status.title");
                else if (tag == "status.text") { c.Text = StatusText(currentKind); }
                else if (tag == "webaddr") c.Text = L10N._("home.address") + ": http://127.0.0.1:3080";
                else if (tag == "dshver") { SetDshVersion(dshVer); }
            }
        }
        RefreshActionButtons();
        // 日志页
        foreach (Control c in pages[1].Controls)
        {
            if (c is Label && c.Tag is string && (string)c.Tag == "log.title") c.Text = L10N._("log.title");
        }
        btnClearLog.Text = L10N._("log.clear");
        // 关于页
        foreach (Control c in pages[2].Controls)
        {
            if (c is Label && c.Tag is string && (string)c.Tag == "about.copy") c.Text = L10N._("about.copy");
        }
    }

    void RefreshActionButtons()
    {
        foreach (Control c in actionGrid.Controls)
        {
            if (c is Button && c.Tag is string)
            {
                Button b = c as Button;
                string key = (string)b.Tag;
                if (key == "act.refresh") b.Text = L10N._("act.refresh");
                else b.Text = L10N._(key);
            }
        }
    }

    // ---- 状态探测（调核心 status CLI） ----
    SKind currentKind = SKind.Unknown;

    string StatusText(SKind k)
    {
        if (k == SKind.Up) return L10N._("home.status.up");
        if (k == SKind.Starting) return L10N._("home.status.starting");
        if (k == SKind.Down) return L10N._("home.status.down");
        return L10N._("home.status.unknown");
    }

    void RefreshStatus()
    {
        if (Interlocked.CompareExchange(ref refreshing, 1, 0) != 0) return;   // 防重入
        string core = CoreExePath();
        if (core == null) { Interlocked.Exchange(ref refreshing, 0); SetStatus(SKind.Unknown); return; }
        ThreadPool.QueueUserWorkItem(delegate(object _)
        {
            try
            {
                CoreRunResult r = RunCoreCapture(core, "status", 10000);
                string k = (r.FirstLine ?? "").Trim();
                SKind st = SKind.Unknown;
                if (k == "STATUS_UP") st = SKind.Up;
                else if (k == "STATUS_STARTING") st = SKind.Starting;
                else if (k == "STATUS_DOWN") st = SKind.Down;
                string ver = ReadDshVersion();   // 轮询顺带读 dsh 版本
                BeginInvoke((Action)delegate { SetStatus(st); SetDshVersion(ver); });
            }
            catch { BeginInvoke((Action)delegate { SetStatus(SKind.Unknown); }); }
            finally { Interlocked.Exchange(ref refreshing, 0); }
        });
    }

    void SetStatus(SKind k)
    {
        currentKind = k;
        if (led != null) led.Set(k);
        if (lblStatusText != null) { lblStatusText.Text = StatusText(k); ApplyStatusColor(); }
        UpdateActionButtons();
    }

    // ---- dsh 版本读取（后台线程调用） ----
    string ReadDshVersion()
    {
        try
        {
            CoreRunResult r = RunCoreCapture("cmd.exe", "/c dsh --version 2>nul", 8000);
            string v = (r.FirstLine ?? "").Trim();
            if (v.Length == 0) return "";   // 未安装/读取失败
            if (v.IndexOfAny(new char[] { '&', '|', ';', '>', '<', '^', '%' }) >= 0) return "";   // 净化，防日志注入
            return v.Length > 30 ? v.Substring(0, 30) : v;
        }
        catch { return ""; }
    }

    void SetDshVersion(string v)
    {
        dshVer = v;
        if (lblDshVer == null) return;
        string dv = v;
        if (string.IsNullOrEmpty(dv))
            dv = L10N._("dsh.verreadfail");
        lblDshVer.Text = L10N._("home.version") + ": " + dv;
    }

    // ---- 按钮禁用态：操作进行中全禁用；按服务状态部分禁用 ----
    void UpdateActionButtons()
    {
        if (actionGrid == null) return;
        foreach (KeyValuePair<string, Button> kv in actBtns)
        {
            string key = kv.Key;
            Button b = kv.Value;
            bool enabled = true;
            if (Interlocked.CompareExchange(ref busy, 0, 0) != 0) enabled = false;      // 操作中
            else if (key == "act.start") enabled = (currentKind != SKind.Up);           // 已运行则禁用启动
            else if (key == "act.stop") enabled = (currentKind == SKind.Up);            // 未运行则禁用停止
            else if (key == "act.restore") enabled = (currentKind != SKind.Up);         // 运行中禁恢复（核心会拒绝）
            b.Enabled = enabled;
        }
    }

    // ---- 日志 ----
    void LogLine(string s)
    {
        Action act = delegate
        {
            if (txtLog == null) return;
            if (txtLog.Text == L10N._("log.empty")) txtLog.Clear();
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + s + Environment.NewLine);
        };
        if (txtLog != null && txtLog.InvokeRequired) txtLog.BeginInvoke(act);
        else act();
    }

    void ClearLog()
    {
        txtLog.Clear();
        txtLog.Text = L10N._("log.empty");
    }

    void RefreshLogColors() { }
}

// ---------------- 入口（单实例锁） ----------------

public static class Program
{
    [STAThread]
    public static void Main()
    {
        bool createdNew;
        Mutex mutex = null;
        try { mutex = new Mutex(true, "DeepSeek-Harness-Toolkit-GUI-single", out createdNew); }
        catch { createdNew = true; }
        try
        {
            if (!createdNew)
            {
                MessageBox.Show(L10N._("app.title") + " 已在运行。", L10N._("app.title"),
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new App());
        }
        finally
        {
            if (mutex != null) { try { mutex.ReleaseMutex(); mutex.Dispose(); } catch { } }
        }
    }
}