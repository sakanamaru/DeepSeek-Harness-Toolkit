// ============================================================================
//  DeepSeek Harness Toolkit GUI  ——  GUI 辅助面板（P3：收尾 + 资源打包）
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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("DeepSeek Harness Toolkit GUI")]
[assembly: AssemblyDescription("DeepSeek Harness(dsh) 非官方图形辅助面板。v1: SOGR-Momono Dango；v2: DeepSeek DSH；GitHub @sakanamaru")]
[assembly: AssemblyCompany("SOGR-Momono Dango / DeepSeek DSH / @sakanamaru")]
[assembly: AssemblyProduct("DeepSeek Harness Toolkit GUI")]
[assembly: AssemblyVersion("2.4.0.0")]
[assembly: AssemblyFileVersion("2.4.0.0")]

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

        // P3 恢复选择对话框
        Add("rp.title", "恢复备份 — 选择备份文件夹", "Restore Backup — Pick a Backup Folder");
        Add("rp.warn", "恢复会用所选备份覆盖当前 dsh 数据；恢复前会自动备份当前数据。",
            "Restoring will overwrite current dsh data with the chosen backup; current data is auto-backed up first.");
        Add("rp.pick", "双击选择备份文件夹：", "Double-click a backup folder:");
        Add("rp.restore", "恢复此备份", "Restore This Backup");
        Add("rp.cancel", "取消", "Cancel");
        Add("rp.empty", "没有可恢复的备份", "No restorable backups");
        Add("rp.fetchfail", "读取备份列表失败", "Failed to read backup list");
        Add("rp.canceled", "已取消恢复", "Restore canceled");
        Add("rp.confirm.title", "确认恢复", "Confirm Restore");
        Add("rp.confirm.text", "确定恢复所选备份？\n当前 dsh 数据将被自动备份后覆盖。",
            "Restore the selected backup?\nCurrent dsh data will be auto-backed up before being overwritten.");
        Add("rp.confirm.ok", "确定恢复", "Restore");
        Add("rp.confirm.cancel", "再想想", "Cancel");
        Add("rp.running", "dsh 正在运行，需先停止服务后才能恢复。",
            "dsh is running. Stop the service first to restore.");
        Add("start.openweb", "已在运行 → 打开 Web 界面…", "Already running — opening the Web UI...");
    }
}

// ---------------- 进程调用结果 ----------------

class CoreRunResult
{
    public int ExitCode = -1;
    public string FirstLine = "";   // 第一行非空输出（仅供参考）
    public string MarkLine = "";    // 扫描全输出找到的机器标记行（BACKUP_OK / RESTORE_FAIL / STATUS_UP ...）
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
        // 先填充父卡片背景色（像素对齐，避免四周过渡痕迹）
        g.SmoothingMode = SmoothingMode.None;
        using (SolidBrush bgB = new SolidBrush(bg == Color.Transparent ? th.PanelAlt : bg))
            g.FillRectangle(bgB, 0, 0, Width, Height);
        Color c = th.LedIdle;
        if (kind == SKind.Up) c = th.LedOk;
        else if (kind == SKind.Starting) c = th.LedWarn;
        else if (kind == SKind.Down) c = th.LedBad;
        // 圆点抗锯齿
        g.SmoothingMode = SmoothingMode.AntiAlias;
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

// ---------------- 圆角按钮（自绘，供标题栏图标 / 导航 / 操作按钮复用） ----------------

class RButton : Button
{
    bool hover;
    bool pressed;
    public bool Checked;                        // 导航当前页高亮
    public bool AccentBar;                      // 导航左侧 3px 强调竖条
    public Action<Graphics, Rectangle> Icon;    // 标题栏图标绘制（╳ / ─ / 月牙）
    public Color HoverTint = Color.Empty;       // hover 强调背景（关闭按钮=红）；空则用 th.Hover
    public Color Surround = Color.Transparent;  // 父容器背景色（铺满四角，消除黑白边）
    Theme th = Theme.Dark;

    public RButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
    }

    public void SetTheme(Theme t) { SetTheme(t, Color.Transparent); }
    public void SetTheme(Theme t, Color surround) { th = t; Surround = surround; Invalidate(); }

    // 抑制默认背景绘制（避免默认 BackColor 在圆角四角露白/黑边）
    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;

        // 先铺满父背景色（消除圆角四角的黑白边）；像素对齐，避免外圈过渡
        g.SmoothingMode = SmoothingMode.None;
        if (Surround != Color.Transparent)
        {
            using (SolidBrush sb = new SolidBrush(Surround))
                g.FillRectangle(sb, 0, 0, Width, Height);
        }

        Color bg = th.PanelAlt;
        if (!Enabled) bg = th.Panel;
        else if (pressed) bg = th.Hover;
        else if (hover) bg = HoverTint.IsEmpty ? th.Hover : HoverTint;
        else if (Checked) bg = th.Accent;

        int rad = 8;
        FillRoundRect(g, new Rectangle(0, 0, Width, Height), rad, bg);

        if (AccentBar && Checked)
        {
            using (SolidBrush ab = new SolidBrush(th.Accent))
                g.FillRectangle(ab, 0, 10, 3, Height - 20);
        }

        if (Icon != null)
        {
            Icon(g, ClientRectangle);
        }
        else if (!string.IsNullOrEmpty(Text))
        {
            Color fg = th.Fg;
            if (!Enabled) fg = th.FgDim;
            else if (Checked) fg = th.AccentFg;
            Rectangle rc = new Rectangle(Padding.Left, Padding.Top, Width - Padding.Horizontal, Height - Padding.Vertical);
            // 灰阶抗锯齿（AntiAliasGridFit）避免 ClearType 在深色背景产生红/蓝彩边
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (StringFormat sf = new StringFormat())
            {
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                if (TextAlign == ContentAlignment.MiddleLeft) { sf.Alignment = StringAlignment.Near; sf.LineAlignment = StringAlignment.Center; }
                else if (TextAlign == ContentAlignment.MiddleRight) { sf.Alignment = StringAlignment.Far; sf.LineAlignment = StringAlignment.Center; }
                else { sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; }
                using (SolidBrush tb = new SolidBrush(fg))
                    g.DrawString(Text, Font, tb, rc, sf);
            }
        }
    }

    public static GraphicsPath RoundRect(int x, int y, int w, int h, int r)
    {
        var p = new GraphicsPath();
        int d = r * 2;
        if (w < d) d = w; if (h < d) d = h;
        if (d < 2) { p.AddRectangle(new Rectangle(x, y, w, h)); return p; }
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // 高质量圆角填充：直边用 FillRectangle + SmoothingMode.None（像素对齐，无亚像素过渡），
    // 四角弧用 FillPie + AntiAlias（仅圆弧抗锯齿）。
    // 关键：GDI+ 在 AntiAlias 下连 FillRectangle 的边界也会被混合成 1px 过渡色，
    // 导致按钮/卡片直边出现贯穿的浅色痕迹。故直边必须关闭抗锯齿。
    public static void FillRoundRect(Graphics g, Rectangle rect, int r, Color c)
    {
        int x = rect.X, y = rect.Y, w = rect.Width, h = rect.Height;
        if (r < 2 || r * 2 >= w || r * 2 >= h)
        {
            g.SmoothingMode = SmoothingMode.None;
            using (SolidBrush b = new SolidBrush(c)) g.FillRectangle(b, rect);
            return;
        }
        int d = r * 2;
        // 直边：关闭抗锯齿，像素对齐
        g.SmoothingMode = SmoothingMode.None;
        using (SolidBrush b = new SolidBrush(c))
        {
            g.FillRectangle(b, x + r, y, w - d, h);       // 中央竖条
            g.FillRectangle(b, x, y + r, w, h - d);       // 上下横条
        }
        // 圆角弧：开启抗锯齿
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (SolidBrush b = new SolidBrush(c))
        {
            g.FillPie(b, x, y, d, d, 180, 90);               // 左上
            g.FillPie(b, x + w - d, y, d, d, 270, 90);       // 右上
            g.FillPie(b, x + w - d, y + h - d, d, d, 0, 90); // 右下
            g.FillPie(b, x, y + h - d, d, d, 90, 90);        // 左下
        }
    }
}

// ---------------- 图标绘制（标题栏 ╳ / ─ / 月牙） ----------------

static class Glyphs
{
    // 关闭 ╳
    public static void Close(Graphics g, Rectangle r)
    {
        int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, d = 5;
        using (Pen p = new Pen(Color.LightGray, 1.7f))
        {
            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
            g.DrawLine(p, cx - d, cy - d, cx + d, cy + d);
            g.DrawLine(p, cx - d, cy + d, cx + d, cy - d);
        }
    }

    // 最小化 ─
    public static void Min(Graphics g, Rectangle r)
    {
        int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        using (Pen p = new Pen(Color.LightGray, 1.7f))
        {
            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
            g.DrawLine(p, cx - 5, cy, cx + 5, cy);
        }
    }

    // 月牙（主题切换）：外圆减偏移内圆（Region 差集），消除"球挡球"叠加痕迹
    public static void Moon(Graphics g, Rectangle r, Color fg, Color bg)
    {
        int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, dd = 12;
        using (GraphicsPath outer = new GraphicsPath())
        using (GraphicsPath inner = new GraphicsPath())
        {
            outer.AddEllipse(cx - dd / 2, cy - dd / 2, dd, dd);
            inner.AddEllipse(cx - dd / 2 + dd / 3 - 1, cy - dd / 2 - 2, dd, dd);
            using (Region moon = new Region(outer))
            {
                moon.Exclude(inner);
                using (SolidBrush b = new SolidBrush(fg))
                    g.FillRegion(b, moon);
            }
        }
    }

    // 太阳（浅色主题下提示切深色）
    public static void Sun(Graphics g, Rectangle r, Color fg)
    {
        int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, rr = 5;
        using (SolidBrush b = new SolidBrush(fg))
            g.FillEllipse(b, cx - rr, cy - rr, rr * 2, rr * 2);
        using (Pen p = new Pen(fg, 1.4f))
        {
            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                int x1 = cx + (int)(Math.Cos(a) * (rr + 3));
                int y1 = cy + (int)(Math.Sin(a) * (rr + 3));
                int x2 = cx + (int)(Math.Cos(a) * (rr + 6));
                int y2 = cy + (int)(Math.Sin(a) * (rr + 6));
                g.DrawLine(p, x1, y1, x2, y2);
            }
        }
    }
}

// ---------------- 圆角面板（状态卡 / 信息卡背景） ----------------

class RPanel : Panel
{
    int radius = 12;
    Theme th = Theme.Dark;
    Color surround = Color.Transparent;

    public RPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public void SetTheme(Theme t) { SetTheme(t, Color.Transparent); }
    public void SetTheme(Theme t, Color s)
    {
        th = t;
        surround = s;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;
        if (surround != Color.Transparent)
        {
            using (SolidBrush sb = new SolidBrush(surround))
                g.FillRectangle(sb, 0, 0, Width, Height);
        }
        RButton.FillRoundRect(g, new Rectangle(0, 0, Width, Height), radius, th.PanelAlt);
    }
}

// ---------------- 自绘 Label（灰阶抗锯齿，避免 ClearType 深色背景彩边） ----------------

class RLabel : Label
{
    public Color Surround = Color.Transparent;

    public RLabel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;
        if (Surround != Color.Transparent)
        {
            using (SolidBrush sb = new SolidBrush(Surround))
                g.FillRectangle(sb, 0, 0, Width, Height);
        }
        if (!string.IsNullOrEmpty(Text))
        {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (SolidBrush tb = new SolidBrush(ForeColor))
            using (StringFormat sf = MakeFormat())
                g.DrawString(Text, Font, tb, new RectangleF(0, 0, Width, Height), sf);
        }
    }

    StringFormat MakeFormat()
    {
        var sf = new StringFormat();
        switch (TextAlign)
        {
            case ContentAlignment.TopLeft: sf.Alignment = StringAlignment.Near; sf.LineAlignment = StringAlignment.Near; break;
            case ContentAlignment.TopCenter: sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Near; break;
            case ContentAlignment.TopRight: sf.Alignment = StringAlignment.Far; sf.LineAlignment = StringAlignment.Near; break;
            case ContentAlignment.MiddleLeft: sf.Alignment = StringAlignment.Near; sf.LineAlignment = StringAlignment.Center; break;
            case ContentAlignment.MiddleRight: sf.Alignment = StringAlignment.Far; sf.LineAlignment = StringAlignment.Center; break;
            case ContentAlignment.BottomLeft: sf.Alignment = StringAlignment.Near; sf.LineAlignment = StringAlignment.Far; break;
            case ContentAlignment.BottomCenter: sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Far; break;
            case ContentAlignment.BottomRight: sf.Alignment = StringAlignment.Far; sf.LineAlignment = StringAlignment.Far; break;
            default: sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; break;
        }
        return sf;
    }
}

// ---------------- 窗口圆角公共助手（Win11 DWM 系统圆角，Win10 降级 Region） ----------------

static class WinRound
{
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    // 调用前须确保窗口 Handle 已创建（OnLoad/OnHandleCreated 内）
    public static void Apply(Form f, int fallbackRadius)
    {
        try
        {
            int pref = DWMWCP_ROUND;
            if (DwmSetWindowAttribute(f.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, 4) == 0)
            {
                f.Region = null;
                return;
            }
        }
        catch { }
        try
        {
            using (GraphicsPath p = RButton.RoundRect(0, 0, f.Width, f.Height, fallbackRadius))
                f.Region = new Region(p);
        }
        catch { }
    }
}

// ---------------- 恢复备份：确认对话框（自绘，主题一致） ----------------

class RestoreConfirm : Form
{
    Point dragStart;

    public RestoreConfirm(string line1, string line2, Theme th)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(430, 168);
        ShowInTaskbar = false;
        Font = new Font("Microsoft YaHei UI", 10f);
        BackColor = th.Panel;
        Text = "";

        RLabel lbl = new RLabel();
        lbl.Surround = th.Panel;
        lbl.ForeColor = th.Fg;
        lbl.Font = new Font("Microsoft YaHei UI", 10f);
        lbl.Text = line1;
        lbl.SetBounds(24, 28, 382, 26);
        Controls.Add(lbl);

        RLabel lblName = new RLabel();
        lblName.Surround = th.Panel;
        lblName.ForeColor = th.FgDim;
        lblName.Font = new Font("Microsoft YaHei UI", 9f);
        lblName.Text = line2;
        lblName.SetBounds(24, 58, 382, 22);
        Controls.Add(lblName);

        RButton ok = new RButton();
        ok.SetTheme(th, th.Panel);
        ok.Checked = true;   // Accent 高亮 = 主操作
        ok.Text = L10N._("rp.confirm.ok");
        ok.SetBounds(230, 108, 86, 32);
        ok.DialogResult = DialogResult.OK;
        Controls.Add(ok);

        RButton cancel = new RButton();
        cancel.SetTheme(th, th.Panel);
        cancel.Text = L10N._("rp.confirm.cancel");
        cancel.SetBounds(322, 108, 86, 32);
        cancel.DialogResult = DialogResult.Cancel;
        Controls.Add(cancel);

        // 标题栏（拖动）
        Panel bar = new Panel();
        bar.Dock = DockStyle.Top;
        bar.Height = 8;
        bar.BackColor = th.Panel;
        bar.MouseDown += delegate(object s, MouseEventArgs e) { dragStart = e.Location; };
        bar.MouseMove += delegate(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                Location = new Point(Location.X + e.X - dragStart.X, Location.Y + e.Y - dragStart.Y);
        };
        Controls.Add(bar);
        bar.BringToFront();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        WinRound.Apply(this, 12);
    }

    public static bool Ask(IWin32Window owner, string line1, string line2, Theme th)
    {
        using (RestoreConfirm f = new RestoreConfirm(line1, line2, th))
            return f.ShowDialog(owner) == DialogResult.OK;
    }
}

// ---------------- 恢复备份：选择对话框（自绘，列出备份文件夹，默认选最新） ----------------

class RestorePicker : Form
{
    Theme th;
    string[] items;
    bool running;
    string picked;
    ListBox list;
    RLabel lblDetail;
    Point dragStart;
    RButton btnRestore;

    public RestorePicker(string[] backupPaths, bool serviceRunning, Theme theme)
    {
        th = theme;
        items = backupPaths;
        running = serviceRunning;
        picked = null;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 402);
        ShowInTaskbar = false;
        Font = new Font("Microsoft YaHei UI", 10f);
        BackColor = th.Bg;

        // 标题栏（拖动 + 关闭）
        Panel bar = new Panel();
        bar.Dock = DockStyle.Top;
        bar.Height = 42;
        bar.BackColor = th.Panel;
        bar.MouseDown += delegate(object s, MouseEventArgs e) { dragStart = e.Location; };
        bar.MouseMove += delegate(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                Location = new Point(Location.X + e.X - dragStart.X, Location.Y + e.Y - dragStart.Y);
        };
        Controls.Add(bar);
        bar.BringToFront();

        RLabel title = new RLabel();
        title.Surround = th.Panel;
        title.ForeColor = th.Fg;
        title.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
        title.Text = L10N._("rp.title");
        title.SetBounds(16, 10, 380, 24);
        bar.Controls.Add(title);

        RButton close = new RButton();
        close.SetTheme(th, th.Panel);
        close.Icon = delegate(Graphics g, Rectangle r) { Glyphs.Close(g, r); };
        close.HoverTint = Color.FromArgb(0xE8, 0x11, 0x23);
        close.SetBounds(478, 7, 34, 28);
        close.Click += delegate(object s, EventArgs e) { picked = null; DialogResult = DialogResult.Cancel; Close(); };
        bar.Controls.Add(close);

        // 提示
        RLabel hint = new RLabel();
        hint.Surround = th.Bg;
        hint.ForeColor = th.FgDim;
        hint.Text = L10N._("rp.pick");
        hint.SetBounds(20, 52, 480, 22);
        Controls.Add(hint);

        // 列表（自绘，最新在前，默认选第一项）
        list = new ListBox();
        list.SetBounds(20, 78, 480, 216);
        list.BorderStyle = BorderStyle.None;
        list.DrawMode = DrawMode.OwnerDrawFixed;
        list.ItemHeight = 34;
        list.BackColor = th.PanelAlt;
        foreach (string p in items) list.Items.Add(Path.GetFileName(p));
        if (list.Items.Count > 0) list.SelectedIndex = 0;
        list.DrawItem += delegate(object s, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            Color bgc = sel ? th.Accent : th.PanelAlt;
            using (SolidBrush b = new SolidBrush(bgc)) e.Graphics.FillRectangle(b, e.Bounds);
            string txt = Convert.ToString(list.Items[e.Index]);
            if (txt != null)
            {
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using (SolidBrush b = new SolidBrush(sel ? th.AccentFg : th.Fg))
                    e.Graphics.DrawString(txt, Font, b, new RectangleF(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 20, e.Bounds.Height),
                        new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center });
            }
        };
        list.SelectedIndexChanged += delegate(object s, EventArgs e)
        {
            UpdateDetail();
        };
        Controls.Add(list);

        // 详情（所选备份路径）
        lblDetail = new RLabel();
        lblDetail.Surround = th.Bg;
        lblDetail.ForeColor = th.FgDim;
        lblDetail.Text = "";
        lblDetail.SetBounds(20, 300, 480, 20);
        Controls.Add(lblDetail);

        // 警告：运行中 = 红字提示需先停止服务；未运行 = 橙色覆盖警告
        RLabel warn = new RLabel();
        warn.Surround = th.Bg;
        if (running)
        {
            warn.ForeColor = th.LedBad;
            warn.Text = L10N._("rp.running");
        }
        else
        {
            warn.ForeColor = th.LedWarn;
            warn.Text = L10N._("rp.warn");
        }
        warn.SetBounds(20, 326, 480, 22);
        Controls.Add(warn);

        // 按钮：运行中「恢复此备份」置灰（红字已说明原因），未运行正常
        btnRestore = new RButton();
        btnRestore.SetTheme(th, th.Bg);
        btnRestore.Text = L10N._("rp.restore");
        btnRestore.SetBounds(282, 360, 110, 32);
        btnRestore.Enabled = !running;
        btnRestore.Click += delegate(object s, EventArgs e)
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= items.Length) return;
            string fn = Path.GetFileName(items[list.SelectedIndex]);
            bool yes = RestoreConfirm.Ask(this, L10N._("rp.confirm.text"), fn, th);
            if (yes)
            {
                picked = items[list.SelectedIndex];
                DialogResult = DialogResult.OK;
                Close();
            }
        };
        Controls.Add(btnRestore);

        RButton cancel = new RButton();
        cancel.SetTheme(th, th.Bg);
        cancel.Text = L10N._("rp.cancel");
        cancel.SetBounds(398, 360, 102, 32);
        cancel.Click += delegate(object s, EventArgs e) { picked = null; DialogResult = DialogResult.Cancel; Close(); };
        Controls.Add(cancel);

        UpdateDetail();
    }

    void UpdateDetail()
    {
        if (lblDetail == null || list == null) return;
        if (list.SelectedIndex >= 0 && list.SelectedIndex < items.Length)
            lblDetail.Text = items[list.SelectedIndex];
        else lblDetail.Text = "";
    }

    public string Picked { get { return picked; } }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        WinRound.Apply(this, 12);
    }
}

// ---------------- 主窗体 ----------------

public class App : Form
{
    bool dark = true;
    Theme Th { get { return dark ? Theme.Dark : Theme.Light; } }

    // 标题栏
    Panel titleBar;
    RButton btnMin, btnClose, btnTheme, btnLang;
    Label lblTitle;

    // 导航
    Panel nav;
    RButton[] navBtns;
    Panel content;
    Panel[] pages;   // 0=home 1=log 2=about

    // 首页
    Led led;
    Label lblStatusText, lblWebAddr, lblDshVer, lblStatusTitleDim;
    TableLayoutPanel actionGrid;

    // 操作按钮字典（key → Button，用于禁用态管理）
    Dictionary<string, Button> actBtns = new Dictionary<string, Button>();

    // 日志页
    TextBox txtLog;
    RButton btnClearLog;

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

    // 当前页索引（导航高亮）
    int curPage = 0;

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

    // 窗口圆角：Win11 用 DWM 系统圆角（抗锯齿），Win10 降级 Region
    // DWM 圆角属性
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWCP_ROUND = 2;
    bool dwmRounded = false;

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    void ApplyWindowRound()
    {
        try
        {
            int pref = DWMWCP_ROUND;
            if (DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, 4) == 0)
            {
                dwmRounded = true;
                Region = null;   // 交给系统抗锯齿圆角
                return;
            }
        }
        catch { }
        // 降级：Region 圆角（硬边界，无抗锯齿）
        dwmRounded = false;
        ApplyWindowRegion();
    }

    void ApplyWindowRegion()
    {
        if (WindowState != FormWindowState.Normal) { Region = null; return; }
        int r = 14;
        using (GraphicsPath path = RButton.RoundRect(0, 0, Width, Height, r))
            Region = new Region(path);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ApplyWindowRound();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (!dwmRounded) ApplyWindowRegion();
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

        lblTitle = new RLabel();
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
        lblTitle.Location = new Point(16, 12);
        titleBar.Controls.Add(lblTitle);

        btnTheme = MakeTitleBtn(0);
        btnLang = MakeTitleBtn(1);
        btnMin = MakeTitleBtn(2);
        btnClose = MakeTitleBtn(3);

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
        titleBar.Resize += delegate(object s, EventArgs e) { RelayoutTitleButtons(); };

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

        // 导航按钮（RButton 圆角 + 当前页高亮指示条，绝对定位避免 Dock.Top 逆序）
        string[] navKeys = new string[] { "nav.home", "nav.log", "nav.about" };
        navBtns = new RButton[3];
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            RButton b = new RButton();
            b.Location = new Point(10, 14 + i * 52);
            b.Size = new Size(156, 42);
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Padding = new Padding(18, 0, 0, 0);
            b.AccentBar = true;
            b.Text = L10N._(navKeys[i]);
            b.Click += delegate(object s, EventArgs e) { ShowPage(idx); };
            navBtns[i] = b;
            nav.Controls.Add(b);
        }
        nav.Resize += delegate
        {
            int w = Math.Max(0, nav.ClientSize.Width - 20);
            for (int i = 0; i < navBtns.Length; i++)
                navBtns[i].Width = w;
        };

        // ---- 底部 disclaimer ----
        lblDisclaimer = new RLabel();
        lblDisclaimer.Dock = DockStyle.Bottom;
        lblDisclaimer.Height = 30;
        lblDisclaimer.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(lblDisclaimer);
    }

    RButton MakeTitleBtn(int orderFromRight)
    {
        RButton b = new RButton();
        b.Size = new Size(42, 32);
        // 绝对定位：X 由 RelayoutTitleButtons 统一重算（Anchor=Right 在构建期会漂移出窗口）
        b.Tag = orderFromRight;
        b.Click += delegate(object s, EventArgs e) { TitleAct(orderFromRight); };
        int id = orderFromRight;
        if (id == 0) b.Icon = delegate(Graphics g, Rectangle r)   // 主题切换：月牙↔太阳
        {
            if (dark) Glyphs.Moon(g, r, Th.FgDim, Th.Panel);
            else Glyphs.Sun(g, r, Th.FgDim);
        };
        else if (id == 1) { b.Icon = null; b.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold); b.Text = L10N.IsZh ? "EN" : "中"; }  // 语言切换
        else if (id == 2) b.Icon = delegate(Graphics g, Rectangle r) { Glyphs.Min(g, r); };                        // 最小化
        else b.Icon = delegate(Graphics g, Rectangle r) { Glyphs.Close(g, r); };                                   // 关闭
        if (id == 3) b.HoverTint = Color.FromArgb(0xE8, 0x11, 0x23);   // 关闭按钮 hover 红色（QQ NT 风格）
        titleBar.Controls.Add(b);
        return b;
    }

    // 标题栏按钮绝对定位（随标题栏宽度重算，避免 Anchor=Right 构建期漂移）
    // 从右到左依次：关闭、最小化、语言、主题（符合常规窗口按钮顺序习惯）
    void RelayoutTitleButtons()
    {
        if (btnTheme == null || titleBar == null) return;
        RButton[] bs = new RButton[] { btnClose, btnMin, btnLang, btnTheme };
        for (int i = 0; i < bs.Length; i++)
        {
            bs[i].Location = new Point(titleBar.ClientSize.Width - 12 - 42 * (i + 1) - 4 * i, 8);
        }
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

        // 状态卡片（圆角 RPanel）
        RPanel card = new RPanel();
        card.Location = new Point(24, 20);
        card.Size = new Size(620, 150);
        p.Controls.Add(card);

        // 注意：Panel.Padding 对绝对定位(Location)的子控件不生效，故用显式内边距坐标，
        // 避免子控件矩形背景盖住卡片左上圆角弧（radius 12，故内边距 ≥ 22）。
        Label lblStatusTitle = new RLabel();
        lblStatusTitle.AutoSize = true;
        lblStatusTitle.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);
        lblStatusTitle.Location = new Point(22, 16);
        lblStatusTitle.Text = L10N._("home.status.title");
        lblStatusTitle.Tag = "status.title";
        lblStatusTitle.ForeColor = Th.FgDim;
        card.Controls.Add(lblStatusTitle);
        lblStatusTitleDim = lblStatusTitle;

        led = new Led();
        led.Location = new Point(22, 42);
        card.Controls.Add(led);

        lblStatusText = new RLabel();
        lblStatusText.AutoSize = true;
        lblStatusText.Font = new Font("Microsoft YaHei UI", 22f, FontStyle.Bold);
        lblStatusText.Location = new Point(48, 34);
        lblStatusText.Text = L10N._("home.status.unknown");
        lblStatusText.Tag = "status.text";
        card.Controls.Add(lblStatusText);

        lblWebAddr = new RLabel();
        lblWebAddr.AutoSize = true;
        lblWebAddr.Location = new Point(22, 92);
        lblWebAddr.Text = L10N._("home.address") + ": http://127.0.0.1:3080";
        lblWebAddr.Tag = "webaddr";
        lblWebAddr.ForeColor = Th.FgDim;
        card.Controls.Add(lblWebAddr);

        lblDshVer = new RLabel();
        lblDshVer.AutoSize = true;
        lblDshVer.Location = new Point(22, 118);
        lblDshVer.Text = L10N._("home.version") + ": —";
        lblDshVer.Tag = "dshver";
        lblDshVer.ForeColor = Th.FgDim;
        card.Controls.Add(lblDshVer);

        // 操作按钮区（8 操作按钮 2 列×4 行 + 刷新跨 2 列）
        actionGrid = new TableLayoutPanel();
        actionGrid.ColumnCount = 2;
        actionGrid.RowCount = 5;
        actionGrid.Location = new Point(24, 192);
        actionGrid.Size = new Size(620, 300);
        for (int c = 0; c < 2; c++) actionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        for (int r = 0; r < 5; r++) actionGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
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
        RButton b = new RButton();
        b.Dock = DockStyle.Fill;
        b.Margin = new Padding(8);
        b.Tag = key;
        b.Text = L10N._(key);
        b.Click += delegate(object s, EventArgs e) { OnAction(key); };
        actionGrid.Controls.Add(b, col, row);
        if (colSpan > 1) actionGrid.SetColumnSpan(b, colSpan);
        actBtns[key] = b;
    }

    void OnAction(string key)
    {
        if (Interlocked.CompareExchange(ref busy, 1, 0) != 0) { LogLine(L10N._("op.busy")); return; }
        // keepBusy=true 仅用于非交互后台命令：busy 保持到 OnCaptureDone 清零；
        // 其余路径（刷新/交互/未知/异常）一律在 finally 立即清零。
        bool keepBusy = false;
        try
        {
            if (key == "act.refresh") { RefreshStatus(); return; }
            if (key == "act.install") { LaunchInteractive("install", key); return; }
            if (key == "act.update") { LaunchInteractive("update", key); return; }
            if (key == "act.uninstall") { LaunchInteractive("uninstall", key); return; }
            // 恢复备份：先拉备份列表 → 弹选择框 → 确认后推 CLI restore --path（busy 全程保持）
            if (key == "act.restore") { keepBusy = true; FetchRestoreList(); return; }
            // 运行中点「启动 Web」= 直接打开 Web 界面（不阻止点击，给真实反馈）
            if (key == "act.start" && currentKind == SKind.Up) { OpenWebUI(); return; }
            // 其余非交互：后台捕获单行标记（busy 延迟到 OnCaptureDone 清零）
            string args = "";
            if (key == "act.start") args = "start --bg";
            else if (key == "act.stop") args = "stop";
            else if (key == "act.backup") args = "backup";
            else if (key == "act.shortcut") args = "shortcut";
            else return;   // 未知 key：finally 清零
            keepBusy = true;
            if (CoreExePath() == null) { LogLine(L10N._("op.coremissing")); keepBusy = false; }
            else
            {
                UpdateActionButtons();   // 立即禁用所有操作按钮
                LaunchCapture(args, key);
            }
        }
        catch { }
        finally
        {
            if (!keepBusy)
            {
                Interlocked.Exchange(ref busy, 0);
                UpdateActionButtons();
            }
        }
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
        string line = (r.MarkLine ?? "").Trim();
        bool ok = false;
        if (key == "act.start") ok = line.StartsWith("START_OK");
        else if (key == "act.stop") ok = line.StartsWith("STOP_OK");
        else if (key == "act.backup") ok = line.StartsWith("BACKUP_OK");
        else if (key == "act.restore") ok = line.StartsWith("RESTORE_OK");
        else if (key == "act.shortcut") ok = line.StartsWith("SHORTCUT_OK");
        if (ok) LogLine(L10N._(key) + " → " + L10N._("op.ok") + "  (" + line + ")");
        else
        {
            // 失败原因：优先用标记行内容，否则第一行，否则全部输出
            string reason = line;
            if (string.IsNullOrEmpty(reason)) reason = string.IsNullOrEmpty(r.FirstLine) ? "" : r.FirstLine;
            if (string.IsNullOrEmpty(reason)) reason = string.IsNullOrEmpty(r.All) ? "" : r.All;
            LogLine(L10N._(key) + " → " + L10N._("op.fail") + (string.IsNullOrEmpty(reason) ? "" : "  (" + reason + ")"));
        }
        Interlocked.Exchange(ref busy, 0);
        UpdateActionButtons();
    }

    // ---- 恢复备份：列表 → 选择框 → 确认 → 推 CLI restore --path ----
    // busy 在进入本流程前已置 1（keepBusy），直到恢复完成（OnCaptureDone）或取消/失败时手动清零。
    void FetchRestoreList()
    {
        string core = CoreExePath();
        if (core == null)
        {
            LogLine(L10N._("op.coremissing"));
            Interlocked.Exchange(ref busy, 0);
            UpdateActionButtons();
            return;
        }
        LogLine(string.Format(L10N._("op.running"), L10N._("act.restore")));
        ThreadPool.QueueUserWorkItem(delegate(object _)
        {
            CoreRunResult r = RunCoreCapture(core, "backup-list", 10000);
            BeginInvoke((Action)delegate { OnRestoreListReady(r); });
        });
    }

    void OnRestoreListReady(CoreRunResult r)
    {
        List<string> paths = null;
        try
        {
            bool keepBusyForRestore = false;
            if (r.TimedOut)
            {
                LogLine(L10N._("act.restore") + " → " + L10N._("op.timeout"));
            }
            else
            {
                string mark = (r.MarkLine ?? "").Trim();
                if (!mark.StartsWith("BACKUP_LIST_OK"))
                {
                    LogLine(L10N._("act.restore") + " → " + L10N._("op.fail") + "  (" + L10N._("rp.fetchfail") + ")");
                }
                else
                {
                    paths = ParseBackupList(r.All);
                    if (paths.Count == 0)
                    {
                        LogLine(L10N._("act.restore") + " → " + L10N._("rp.empty"));
                    }
                    else
                    {
                        string picked = null;
                        using (RestorePicker dlg = new RestorePicker(paths.ToArray(), currentKind == SKind.Up, Th))
                        {
                            if (dlg.ShowDialog(this) == DialogResult.OK) picked = dlg.Picked;
                        }
                        if (picked != null)
                        {
                            LogLine(string.Format(L10N._("op.running"), L10N._("act.restore")));
                            keepBusyForRestore = LaunchRestorePath(picked);
                        }
                        else LogLine(L10N._("act.restore") + " → " + L10N._("rp.canceled"));
                    }
                }
            }
            // busy 释放：仅当未成功发起恢复时才在本函数清（恢复路径由 OnCaptureDone 清）
            if (!keepBusyForRestore) { Interlocked.Exchange(ref busy, 0); UpdateActionButtons(); }
        }
        catch (Exception ex)
        {
            LogLine(L10N._("act.restore") + " → " + L10N._("op.fail") + "  (" + ex.Message + ")");
            Interlocked.Exchange(ref busy, 0);
            UpdateActionButtons();
        }
    }

    // 解析 backup-list 输出：BACKUP_LIST_OK 标记后的绝对路径行（dsh-data-*）
    static List<string> ParseBackupList(string all)
    {
        List<string> list = new List<string>();
        if (string.IsNullOrEmpty(all)) return list;
        string[] lines = all.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        bool markSeen = false;
        foreach (string ln in lines)
        {
            string t = ln.Trim();
            if (t.StartsWith("BACKUP_LIST_OK")) { markSeen = true; continue; }
            if (markSeen && t.Length > 0 && t.IndexOf("dsh-data-", StringComparison.OrdinalIgnoreCase) >= 0 && Path.IsPathRooted(t))
                list.Add(t);
        }
        return list;
    }

    // 恢复所选备份（用户已在选择框 + 确认框双重确认）；返回 true=已发起（busy 交给 OnCaptureDone 释放）
    bool LaunchRestorePath(string path)
    {
        string core = CoreExePath();
        if (core == null)
        {
            LogLine(L10N._("op.coremissing"));
            return false;   // 未发起 → OnRestoreListReady 释放 busy
        }
        string arg = "restore --path \"" + path + "\"";
        ThreadPool.QueueUserWorkItem(delegate(object _)
        {
            CoreRunResult r = RunCoreCapture(core, arg, 120000);   // 恢复大目录可能较久
            BeginInvoke((Action)delegate
            {
                OnCaptureDone("act.restore", r);
                RefreshStatus();
            });
        });
        return true;
    }

    // 从完整输出中扫描机器标记行（核心可能在标记前后打印进度文案，如「正在恢复数据...」）。
    static string FindMarker(string all)
    {
        if (string.IsNullOrEmpty(all)) return "";
        string[] lines = all.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string ln in lines)
        {
            string t = ln.Trim();
            if (t.StartsWith("BACKUP_OK") || t.StartsWith("BACKUP_FAIL") ||
                t.StartsWith("BACKUP_LIST_OK") ||
                t.StartsWith("RESTORE_OK") || t.StartsWith("RESTORE_FAIL") ||
                t.StartsWith("STATUS_UP") || t.StartsWith("STATUS_STARTING") || t.StartsWith("STATUS_DOWN") ||
                t.StartsWith("START_OK") || t.StartsWith("START_FAIL") ||
                t.StartsWith("STOP_OK") || t.StartsWith("STOP_FAIL") ||
                t.StartsWith("SHORTCUT_OK") || t.StartsWith("SHORTCUT_FAIL"))
                return t;
        }
        return "";
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
                if (!string.IsNullOrEmpty(stdoutAll))
                {
                    string[] lines = stdoutAll.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 0) res.FirstLine = lines[0];
                }
                // 真正用于判定的标记行：核心可能先打进度文案再打标记
                res.MarkLine = FindMarker(res.All);
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

        Label t = new RLabel();
        t.AutoSize = true;
        t.Location = new Point(20, 12);
        t.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
        t.Text = L10N._("log.title");
        t.Tag = "log.title";
        top.Controls.Add(t);

        btnClearLog = new RButton();
        btnClearLog.Size = new Size(72, 28);
        btnClearLog.Location = new Point(0, 8);
        btnClearLog.Text = L10N._("log.clear");
        btnClearLog.Click += delegate(object s, EventArgs e) { ClearLog(); };
        top.Controls.Add(btnClearLog);
        top.Resize += delegate(object s, EventArgs e)
        {
            btnClearLog.Location = new Point(top.ClientSize.Width - 72 - 20, 8);
        };

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
        // 关于页 logo 圆角（Region 裁剪，与整体圆角设计一致）
        using (GraphicsPath logoPath = RButton.RoundRect(0, 0, 120, 120, 18))
            picLogo.Region = new Region(logoPath);

        Label name = new RLabel();
        name.AutoSize = true;
        name.Font = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold);
        name.Location = new Point(140, 40);
        name.Text = "DeepSeek Harness Toolkit";
        p.Controls.Add(name);

        Label ver = new RLabel();
        ver.AutoSize = true;
        ver.Location = new Point(142, 82);
        ver.Text = "GUI " + AssemblyVersion();
        p.Controls.Add(ver);

        Label copy = new RLabel();
        copy.AutoSize = true;
        copy.Location = new Point(142, 108);
        copy.Text = L10N._("about.copy");
        copy.Tag = "about.copy";
        p.Controls.Add(copy);

        Label cred = new RLabel();
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
        curPage = idx;
        for (int i = 0; i < pages.Length; i++)
            pages[i].Visible = (i == idx);
        for (int i = 0; i < navBtns.Length; i++)
        {
            navBtns[i].Checked = (i == idx);
            navBtns[i].Refresh();
        }
    }

    // ---- 主题 ----
    void ApplyTheme()
    {
        SuspendLayout();
        Theme t = Th;

        BackColor = t.Bg;
        titleBar.BackColor = t.Panel;
        if (lblTitle is RLabel) { (lblTitle as RLabel).Surround = t.Panel; }
        lblTitle.ForeColor = t.Fg;
        nav.BackColor = t.Panel;
        content.BackColor = t.Bg;

        foreach (RButton b in navBtns)
            b.SetTheme(t, t.Panel);   // 导航项父 = nav（Panel 色）

        btnTheme.SetTheme(t, t.Panel);
        btnLang.SetTheme(t, t.Panel);
        btnMin.SetTheme(t, t.Panel);
        btnClose.SetTheme(t, t.Panel);
        btnLang.ForeColor = t.FgDim;   // 语言文字按钮

        foreach (Panel pg in pages) { pg.BackColor = t.Bg; ThemeRecurse(pg, t, t.Bg); }

        if (lblDisclaimer is RLabel) { (lblDisclaimer as RLabel).Surround = t.Panel; }
        lblDisclaimer.ForeColor = t.FgDim;

        led.SetTheme(t, t.PanelAlt);
        ApplyStatusColor();

        // 状态卡次标签恢复 Dim 色（ThemeRecurse 会统一成 Fg）
        if (lblWebAddr != null) lblWebAddr.ForeColor = t.FgDim;
        if (lblDshVer != null) lblDshVer.ForeColor = t.FgDim;
        if (lblStatusTitleDim != null) lblStatusTitleDim.ForeColor = t.FgDim;

        // 日志页
        txtLog.BackColor = t.Panel;
        txtLog.ForeColor = t.Fg;
        btnClearLog.SetTheme(t, t.Bg);   // 清空按钮父 = 日志页 top（Bg 色）

        UpdateActionButtons();   // 禁用态着色后重新应用

        ResumeLayout();
        Invalidate(true);
    }

    // 递归着色容器里的已知控件类型；surround = 当前容器的背景色（传给圆角子控件铺四角）
    void ThemeRecurse(Control parent, Theme t, Color surround)
    {
        foreach (Control c in parent.Controls)
        {
            if (c is RPanel)
            {
                (c as RPanel).SetTheme(t, surround);
                // RPanel 内部背景 = PanelAlt，故其子控件 surround 传 PanelAlt
                ThemeRecurse(c, t, t.PanelAlt);
                continue;
            }
            if (c is RButton)
            {
                (c as RButton).SetTheme(t, surround);
                continue;
            }
            if (c is Panel)
            {
                if (c.BackColor != Color.Transparent && c == parent) continue;
                Panel pn = c as Panel;
                if (pn.Padding.Horizontal > 0) { pn.BackColor = t.PanelAlt; ThemeRecurse(pn, t, t.PanelAlt); }
                else { pn.BackColor = t.Bg; ThemeRecurse(pn, t, t.Bg); }
                continue;
            }
            if (c is RLabel)
            {
                (c as RLabel).Surround = surround;
                c.ForeColor = t.Fg;
                continue;
            }
            if (c is Label)
            {
                c.ForeColor = t.Fg;
                c.BackColor = surround;   // 文字背景必须与容器一致，消除文字周围杂边/白块
            }
            if (c is TableLayoutPanel)
            {
                c.BackColor = t.Bg;
                ThemeRecurse(c, t, t.Bg);
                continue;
            }
            if (c is TextBox)
            {
                c.BackColor = t.Panel;
                c.ForeColor = t.Fg;
            }
            ThemeRecurse(c, t, surround);
        }
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
                string k = (r.MarkLine ?? "").Trim();
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

    // ---- 按钮禁用态：操作进行中全禁用（入口按钮不再按服务状态置灰——
    // 运行中点启动=打开网页、恢复弹窗内给出红字原因，永远给用户真实反馈） ----
    void UpdateActionButtons()
    {
        if (actionGrid == null) return;
        bool busyNow = Interlocked.CompareExchange(ref busy, 0, 0) != 0;
        foreach (KeyValuePair<string, Button> kv in actBtns)
        {
            kv.Value.Enabled = !busyNow;
        }
    }

    // 运行中直接打开 Web 界面（UseShellExecute，立即返回）
    void OpenWebUI()
    {
        try
        {
            LogLine(L10N._("start.openweb"));
            Process.Start(new ProcessStartInfo("http://127.0.0.1:3080/") { UseShellExecute = true });
        }
        catch (Exception ex) { LogLine(L10N._("act.start") + " → " + L10N._("op.fail") + "  (" + ex.Message + ")"); }
    }

    // ---- 日志 ----
    void LogLine(string s)
    {
        Action act = delegate
        {
            if (txtLog == null) return;
            if (txtLog.Text == L10N._("log.empty")) txtLog.Clear();
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + s + Environment.NewLine);
            // 自动滚到最新一行
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
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