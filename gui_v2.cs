// ============================================================================
//  DeepSeek Harness Toolkit GUI  ——  GUI 辅助面板（P3：收尾 + 资源打包）
//  ----------------------------------------------------------------------------
//  架构：本体不变，GUI 通过非交互 CLI 协同（backup/restore/status/start --bg/stop/shortcut，
//        install/update/uninstall 弹可见窗口交互）。
//  本文件 = 无边框窗口 + 深/浅主题 + 中英双语 + 三页切换 + 状态灯 + 完整进程层。
    //  v2.7 = 托盘驻留 + 关闭行为记忆 + 底部状态栏 + 快捷键 + Toast + 首启完整性检查 + 验证此安装。
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
[assembly: AssemblyVersion("2.7.0.0")]
[assembly: AssemblyFileVersion("2.7.0.0")]

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
        Add("nav.doctor", "体检", "Doctor");
        Add("nav.backup", "备份", "Backups");
        Add("backup.title", "备份管理", "Backup Manager");
        Add("backup.now", "立即备份", "Backup Now");
        Add("backup.refresh", "刷新", "Refresh");
        Add("backup.restore", "恢复所选", "Restore");
        Add("backup.export", "导出所选", "Export");
        Add("backup.delete", "删除所选", "Delete");
        Add("backup.nosel", "请先选择一条备份。", "Select a backup first.");
        Add("backup.col.time", "时间", "Time");
        Add("backup.col.kind", "类型", "Type");
        Add("backup.col.size", "大小", "Size");
        Add("backup.col.state", "状态", "State");
        Add("backup.col.name", "名称", "Name");
        Add("backup.dryrun.title", "恢复预演（Dry-Run）", "Restore Dry-Run");
        Add("backup.dryrun.msg", "将对 {4} 执行恢复：\n新增 {0} 个文件、覆盖 {1} 个、保留 {2} 个（仅目标端存在、不会被删除）\n待复制约 {3}。\n\n确认执行？",
            "Restore onto {4}:\nNEW {0} file(s), OVERWRITE {1}, KEEP {2} (destination-only, NOT deleted)\nabout {3} to copy.\n\nProceed?");
        Add("backup.del.title", "删除备份", "Delete backup");
        Add("backup.del.msg", "确认删除备份 {0}？\n此操作不可撤销。", "Delete backup {0}?\nThis cannot be undone.");
        Add("bk.export", "导出备份", "Export backup");
        Add("bk.delete", "删除备份", "Delete backup");
        Add("nav.update", "更新", "Update");
        Add("update.title", "更新中心", "Update Center");
        Add("update.current", "当前 dsh 版本", "Current dsh version");
        Add("update.latest.stable", "最新稳定版", "Latest stable");
        Add("update.latest.rc", "最新预发布", "Latest rc");
        Add("update.channel", "更新通道", "Channel");
        Add("update.prebackup", "最近更新前备份", "Latest pre-update backup");
        Add("update.rollback", "回滚候选（有效备份数）", "Rollback candidates (valid backups)");
        Add("update.notes", "dsh 发布说明", "dsh release notes");
        Add("update.check", "检查更新", "Check");
        Add("update.go", "更新 dsh…", "Update dsh…");
        Add("nav.settings", "设置", "Settings");
        Add("settings.title", "设置", "Settings");
        Add("settings.g.harness", "Harness", "Harness");
        Add("settings.g.backup", "备份", "Backup");
        Add("settings.g.update", "更新", "Update");
        Add("settings.g.toolkit", "工具箱", "Toolkit");
        Add("settings.host", "Web 访问入口", "Web host");
        Add("settings.ws", "工作区路径（空=自动探测）", "Workspace path (empty=auto)");
        Add("settings.keep", "自动备份保留份数（≥3）", "Auto-backup retention (≥3)");
        Add("settings.chkupd", "启动时检查工具箱更新", "Check toolkit updates at startup");
        Add("settings.chkdsh", "检测 dsh 本体更新", "Detect dsh updates");
        Add("settings.channel", "dsh 更新通道", "dsh update channel");
        Add("settings.lang", "界面语言", "UI language");
        Add("settings.save", "保存", "Save");
        Add("settings.note", "部分设置（语言/入口）立即生效，其余下次启动生效。", "Some settings (language/host) apply immediately; others on next start.");
        Add("log.all", "全部", "All");
        Add("log.info", "信息", "Info");
        Add("log.warn", "警告", "Warn");
        Add("log.error", "错误", "Error");
        Add("log.export", "导出", "Export");
        Add("log.copy", "复制", "Copy");
        Add("nav.about", "关于", "About");

        Add("home.status.title", "服务状态", "Service Status");
        Add("home.status.up", "运行中", "RUNNING");
        Add("home.status.starting", "启动中", "STARTING");
        Add("home.status.down", "已停止", "STOPPED");
        Add("home.status.unknown", "未检测", "UNKNOWN");
        Add("home.status.nocore", "未找到核心程序（CLI）", "Core exe (CLI) not found");
        Add("home.status.nocore.tip", "请将核心 exe 与本 GUI 放在同一文件夹，或改用单文件集成版",
            "place the core exe next to this GUI, or use the standalone build");
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

        Add("doc.title", "体检 / Doctor", "Health Check / Doctor");
        Add("doc.recheck", "重新体检", "Re-check");
        Add("doc.export", "导出报告", "Export");
        Add("doc.running", "体检中，请稍候…", "Checking, please wait…");
        Add("doc.empty", "（尚无体检结果）", "(no result yet)");
        Add("doc.exported", "报告已导出: ", "Report exported: ");
        Add("doc.exportfail", "导出失败: ", "Export failed: ");
        Add("log.clear", "清空", "Clear");
        Add("log.empty", "（暂无日志）", "(no log yet)");

        Add("about.copy", "非官方工具箱 · 独立于 DeepSeek 官方", "Unofficial toolkit · independent of DeepSeek");
        Add("disclaimer", "非官方项目，与 DeepSeek 官方无隶属关系。",
            "This is an unofficial project and is not affiliated with DeepSeek.");



        // P2 进程层文案
        Add("op.running", "执行「{0}」…", "Running \"{0}\"...");
        Add("op.ok", "完成", "OK");
        Add("op.fail", "失败", "Failed");
        Add("op.timeout", "超时", "Timed out");
        Add("op.busy", "上一操作仍在进行，请稍候…", "Previous operation still running, please wait...");
        Add("op.coremissing", "未找到核心程序（DeepSeek Harness Toolkit.exe，请与 GUI 同目录）",
            "Core exe not found (DeepSeek Harness Toolkit.exe, place it next to the GUI)");
        Add("op.coreextracted", "已自动解出内嵌核心 exe（单文件集成版）",
            "Embedded core exe extracted automatically (standalone build)");
        Add("about.standalone", "（单文件集成版）", "(standalone build)");
        Add("badir.title", "不建议在此目录运行", "Not recommended to run here");
        Add("badir.msg", "你在桌面/下载目录直接运行本程序：备份与日志会写到该目录，文件容易被误删或丢失。\n建议移到独立文件夹（例如 D:\\Tools\\DSHToolkit）后再使用。\n\n仍要继续吗？",
            "You are running directly from Desktop/Downloads: backups & logs land there and are easy to lose.\nMove to a dedicated folder (e.g. D:\\Tools\\DSHToolkit) instead.\n\nContinue anyway?");
        Add("op.launchfailed", "启动失败：", "Launch failed: ");
        Add("dsh.verreadfail", "读取失败", "read failed");

        // P3 恢复选择对话框
        Add("rp.running", "dsh 正在运行，需先停止服务后才能恢复。",
            "dsh is running. Stop the service first to restore.");
        Add("start.openweb", "已在运行 → 打开 Web 界面…", "Already running — opening the Web UI...");
        Add("act.repair", "修复 dsh", "Repair dsh");
        Add("backup.goto", "已切换到备份页：选中一份备份后点【恢复此备份】。",
            "Switched to the Backup page: pick a backup, then click [Restore This Backup].");
        Add("tray.show", "显示主窗口", "Show Window");
        Add("tray.start", "启动 dsh", "Start dsh");
        Add("tray.stop", "停止 dsh", "Stop dsh");
        Add("tray.exit", "退出", "Exit");
        Add("tray.balloon.title", "仍在后台运行", "Still running in the background");
        Add("tray.balloon.body", "工具箱已最小化到托盘，dsh 服务不受影响；双击托盘图标可恢复窗口。",
            "The toolkit is minimized to the tray; the dsh service is unaffected. Double-click the tray icon to restore.");
        Add("close.title", "关闭窗口", "Close Window");
        Add("close.body", "关闭主窗口时要怎么做？（之后可在【设置】页修改）",
            "What should happen when you close the main window? (Changeable on the Settings page)");
        Add("close.tray", "最小化到托盘", "Minimize to Tray");
        Add("close.exit", "直接退出", "Exit Directly");
        Add("stat.pid", "PID", "PID");
        Add("stat.uptime", "运行", "up");
        Add("stat.theme.dark", "深色", "Dark");
        Add("stat.theme.light", "浅色", "Light");
        Add("stat.lang", "语言", "Lang");
        Add("settings.autostart", "菜单倒计时自动启动", "Menu auto-start countdown");
        Add("settings.closeact", "关闭主窗口时", "On closing the window");
        Add("verify.btn", "验证此安装", "Verify This Install");
        Add("verify.running", "正在联网比对官方清单…", "Comparing against the official manifest...");
        Add("verify.state.idle", "尚未验证：点上面的按钮，联网比对官方 Release 的 hashes.txt。",
            "Not verified yet: click the button above to compare against the official hashes.txt.");
        Add("verify.state.ok", "一致：本机文件与官方清单记录的 SHA-256 相同。",
            "Match: local files have the same SHA-256 as the official manifest.");
        Add("verify.state.bad", "不一致！本机文件与清单记录不符（可能被替换或篡改），请从官方 Release 重新下载。",
            "MISMATCH! Local files differ from the manifest (possibly replaced/tampered). Re-download from the official Release.");
        Add("verify.state.none", "未能验证：", "Could not verify: ");
        Add("verify.reason.offline", "离线或无法访问 GitHub", "offline or GitHub unreachable");
        Add("verify.reason.nolist", "随包没有 hashes.txt（单独复制 exe 属正常）", "no hashes.txt next to the exe (normal for a single-file copy)");
        Add("verify.line.ok", "{0}：SHA-256 与清单一致", "{0}: SHA-256 matches the manifest");
        Add("verify.line.bad", "{0}：与清单不一致 ← 可疑", "{0}: differs from the manifest <-- suspicious");
        Add("verify.line.skip", "{0}：清单里没有记录，跳过", "{0}: not listed in the manifest, skipped");
        Add("verify.line.nolist", "{0}：无清单可比", "{0}: no manifest to compare");
        Add("verify.note", "说明：这里只做「与官方清单的哈希一致性」比对，不是签名验证，也无法证明发布者身份；"
            + "签名校验请用仓库里的 verify.ps1（GPG + Release→Tag→Commit 链）。",
            "Note: this only compares SHA-256 against the official manifest. It is NOT signature verification and cannot prove publisher identity; "
            + "use verify.ps1 from the repo (GPG + Release->Tag->Commit chain) for that.");
        Add("verify.localmismatch", "本机 exe 与随包 hashes.txt 不一致，请从官方 Release 重新下载。",
            "Local exe does not match the bundled hashes.txt. Re-download from the official Release.");
        Add("motw.warn", "本程序带有「来自网络」标记（Mark of the Web）：SmartScreen / 杀软提示属常见现象，可用 verify.ps1 校验签名。",
            "This program carries the Mark of the Web: SmartScreen/AV prompts are common; verify the signature with verify.ps1.");
        Add("firstrun.title", "首次启动：完整性自检", "First run: integrity self-check");
        Add("firstrun.log", "首次启动完整性自检已完成。", "First-run integrity self-check completed.");
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
        // 直边 + 圆角弧全部关闭抗锯齿：AA 会在小半径弧线边缘产生 1px 混合色"线"（如 31,34,44）。
        // r=8 的短弧 1px 台阶在浅色主题下比过渡线更干净。
        g.SmoothingMode = SmoothingMode.None;
        using (SolidBrush b = new SolidBrush(c))
        {
            g.FillRectangle(b, x + r, y, w - d, h);       // 中央竖条
            g.FillRectangle(b, x, y + r, w, h - d);       // 上下横条
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


// ---------------- 主窗体 ----------------

public class App : Form
{
    bool dark = false;   // 默认浅色主题（白色模式），右上角月牙/太阳可切换
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
    // 日志控件（日志页）创建前产生的日志行先暂存：首页构建即会触发内嵌核心解出，
    // 那条日志早于 BuildLog()，此前会被静默丢弃（v2.4.2 修复）。
    readonly List<string[]> pendingLog = new List<string[]>();   // 日志页构建前暂存：[time, level, text]
    readonly List<string[]> logEntries = new List<string[]>();   // v2.9 Log Center 结构化日志
    string curLogLevel = "ALL";
    string logQuery = "";
    RButton btnLogAll, btnLogInfo, btnLogWarn, btnLogErr, btnLogExport, btnLogCopy;
    TextBox txtLogSearch;
    RButton btnClearLog;

    // 关于页
    PictureBox picLogo;

    // 体检页（v2.5 doctor）
    TextBox txtDoctor;
    RButton btnDocRecheck, btnDocExport;
    string doctorReportPath = "";

    // 备份管理页（v2.6 Backup Manager）
    ListView lvBackups;
    RButton btnBkRefresh, btnBkNow, btnBkRestore, btnBkExport, btnBkDelete;
    readonly List<string[]> bkEntries = new List<string[]>();   // name, kind, bytes, mtime, path

    // 更新中心页（v2.7 Update Center）
    Label lblUpCur, lblUpStable, lblUpRc, lblUpChannel, lblUpPre, lblUpRoll, lblUpNotes;
    RButton btnUpCheck, btnUpGo;
    bool upInfoLoaded = false;

    // 设置页（v2.8 Configuration）
    ComboBox cmbHost, cmbChkUpd, cmbChkDshUpd, cmbChannel, cmbLang, cmbAutoStart, cmbCloseAct;
    TextBox txtWs, txtKeep;
    RButton btnSetSave;
    readonly Dictionary<string, string> setOrig = new Dictionary<string, string>();

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

    // ---- v2.7：底部状态栏 ----
    Panel statBar;
    RLabel lblStatusBar;
    string svcPid = "";          // 状态栏 PID（status --detail）
    string svcUptime = "";       // 状态栏运行时长（status --detail）

    // ---- v2.7：托盘 + 关闭行为 ----
    NotifyIcon trayIcon;
    ContextMenuStrip trayMenu;
    string cfgClose = "";        // 关闭行为记忆（config-get close_action；空=还没问过 → 首次关窗询问）
    bool forceExit = false;      // 用户明确"直接退出"→ 放行 Close
    bool trayTipShown = false;   // 首次最小化到托盘的提示只弹一次
    const int POLL_VISIBLE = 3000;   // 窗口可见时的轮询间隔
    const int POLL_HIDDEN = 10000;   // 最小化到托盘后的轮询间隔（省电，服务仍在跑）

    // ---- v2.7：状态轮询计数 + dsh 版本缓存（原先每 3 秒都 spawn 一次 cmd 读版本，浪费）----
    int pollTicks = 0;
    bool verDirty = true;        // true=下次轮询强制重读版本（安装/更新/卸载后置位）

    // ---- v2.7：完整性校验（关于页 + 首启自检）----
    RButton btnVerifyInstall;
    RLabel lblVerifyResult;
    int verifyState = 0;         // 0=未验证 1=一致 2=不一致 3=未能验证 9=进行中
    string verifyReport = "";    // 明细（语言切换后按当前语言重渲染）

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

        KeyPreview = true;              // v2.7：快捷键（Ctrl+1~7 切页 / F5 刷新 / Ctrl+B 备份）
        InitTray();                     // v2.7：托盘驻留（关闭行为依赖它）
        LoadCloseAction();              // v2.7：读回上次记住的关闭行为（异步，读完即生效）
        FormClosing += delegate(object s, FormClosingEventArgs e) { OnAppClosing(e); };

        // 3 秒状态轮询（定时器，不阻塞 UI）
        pollTimer = new System.Windows.Forms.Timer();
        pollTimer.Interval = POLL_VISIBLE;
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
        CheckBadDirWarning();            // 防误用：桌面/下载目录直接运行 → 弹窗提醒（可取消继续）
        CheckMotwWarning();              // v2.7：网络来源标记提醒（只记日志，不弹窗）
        CheckLocalIntegrityAtStartup();  // v2.7：随包清单一致性（纯本地，不联网）
        CheckFirstRun();                 // v2.7：首次启动只做完整性自检（一次性）
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

        pages = new Panel[7];
        pages[0] = BuildHome();
        pages[1] = BuildBackup();
        pages[2] = BuildUpdate();
        pages[3] = BuildSettings();
        pages[4] = BuildLog();
        pages[5] = BuildDoctor();
        pages[6] = BuildAbout();
        foreach (Panel p in pages)
        {
            p.Dock = DockStyle.Fill;
            p.Visible = false;
            content.Controls.Add(p);
        }

        // 导航按钮（RButton 圆角 + 当前页高亮指示条，绝对定位避免 Dock.Top 逆序）
        // 顺序必须与 pages 索引一一对应：0=home 1=log 2=doctor 3=about（关于固定最底）
        string[] navKeys = new string[] { "nav.home", "nav.backup", "nav.update", "nav.settings", "nav.log", "nav.doctor", "nav.about" };
        navBtns = new RButton[7];
        for (int i = 0; i < 7; i++)
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

        // ---- v2.7 底部状态栏（Dock 逆序：先 Add → 后布局 → 落在 disclaimer 之上）----
        statBar = new Panel();
        statBar.Dock = DockStyle.Bottom;
        statBar.Height = 26;
        lblStatusBar = new RLabel();
        lblStatusBar.Dock = DockStyle.Fill;
        lblStatusBar.TextAlign = ContentAlignment.MiddleLeft;
        lblStatusBar.Padding = new Padding(16, 0, 0, 0);
        lblStatusBar.Tag = "statusbar";
        lblStatusBar.ForeColor = Th.FgDim;
        statBar.Controls.Add(lblStatusBar);
        Controls.Add(statBar);

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

        AddActionButton(0, 0, "act.start", 1);     // v2.7：日常最常用的是"启动"，与"安装/修复"互换到左上
        AddActionButton(1, 0, "act.install", 1);
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
            // 恢复备份：切到备份页（选择/预演/确认都在那一页，不再弹第二个选择框）
            if (key == "act.restore") { ShowPage(1); LogLine(L10N._("backup.goto")); return; }
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
            if (CoreExePath() == null) { LogWarn(L10N._("op.coremissing")); keepBusy = false; }
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
        if (core == null) { LogWarn(L10N._("op.coremissing")); return; }
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
            verDirty = true;   // v2.7：交互命令结束后 dsh 版本可能变了，下次轮询强制重读
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
        if (core == null) { LogWarn(L10N._("op.coremissing")); return; }
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
        if (r.TimedOut) { LogWarn(L10N._(key) + " → " + L10N._("op.timeout")); Interlocked.Exchange(ref busy, 0); UpdateActionButtons(); return; }
        string line = (r.MarkLine ?? "").Trim();
        bool ok = false;
        if (key == "act.start") ok = line.StartsWith("START_OK");
        else if (key == "act.stop") ok = line.StartsWith("STOP_OK");
        else if (key == "act.backup") ok = line.StartsWith("BACKUP_OK");
        else if (key == "act.restore") ok = line.StartsWith("RESTORE_OK");
        else if (key == "act.shortcut") ok = line.StartsWith("SHORTCUT_OK");
        else if (key == "bk.export") ok = line.StartsWith("BKEXPORT_OK");
        else if (key == "bk.delete") ok = line.StartsWith("BKDEL_OK");
        if (ok) LogLine(L10N._(key) + " → " + L10N._("op.ok") + "  (" + line + ")");
        else
        {
            // 失败原因：优先用标记行内容，否则第一行，否则全部输出
            string reason = line;
            if (string.IsNullOrEmpty(reason)) reason = string.IsNullOrEmpty(r.FirstLine) ? "" : r.FirstLine;
            if (string.IsNullOrEmpty(reason)) reason = string.IsNullOrEmpty(r.All) ? "" : r.All;
            LogWarn(L10N._(key) + " → " + L10N._("op.fail") + (string.IsNullOrEmpty(reason) ? "" : "  (" + reason + ")"));
        }
        Interlocked.Exchange(ref busy, 0);
        UpdateActionButtons();
        if (ok && (key == "bk.delete" || key == "act.backup")) LoadBackupList();   // 备份列表变化后自动刷新管理页
        // v2.7：后台捕获型操作的结果用托盘气泡反馈一次（窗口在后台/最小化时也能看到）
        if (ok && (key == "act.backup" || key == "act.restore" || key == "bk.export" || key == "bk.delete"))
            ShowToast(L10N._(key) + " → " + L10N._("op.ok"));
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
        if (File.Exists(core)) return core;
        TryExtractCore();   // 单文件集成版：核心缺失时从内嵌资源解出（附加版无资源则静默）
        return File.Exists(core) ? core : null;
    }

    bool coreExtractTried = false;

    /// <summary>是否单文件集成版（编译时内嵌了核心资源 DSHCore.exe）。</summary>
    static bool HasEmbeddedCore()
    {
        try
        {
            foreach (string n in typeof(App).Assembly.GetManifestResourceNames())
                if (n.EndsWith("DSHCore.exe", StringComparison.OrdinalIgnoreCase)) return true;
        }
        catch { }
        return false;
    }

    /// <summary>单文件集成版：把内嵌的核心 exe（/resource:"DeepSeek Harness Toolkit.exe,DSHCore.exe"）
    /// 解出到 GUI 同目录。只尝试一次；失败静默（后续操作会提示未找到核心）。
    /// 先写 .tmp 再原子改名——避免磁盘满/中断留下半截 exe 被误判为有效核心。
    /// 附加版（未内嵌）此方法无资源可解，行为与旧版完全一致。</summary>
    void TryExtractCore()
    {
        if (coreExtractTried) return;
        coreExtractTried = true;
        string dest = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeepSeek Harness Toolkit.exe");
        string tmp = dest + ".tmp";
        try
        {
            foreach (string n in typeof(App).Assembly.GetManifestResourceNames())
            {
                if (!n.EndsWith("DSHCore.exe", StringComparison.OrdinalIgnoreCase)) continue;
                using (Stream s = typeof(App).Assembly.GetManifestResourceStream(n))
                {
                    if (s == null) return;
                    using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        byte[] buf = new byte[65536];
                        int r;
                        while ((r = s.Read(buf, 0, buf.Length)) > 0) fs.Write(buf, 0, r);
                    }
                }
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(tmp, dest);
                LogLine("✓ " + L10N._("op.coreextracted"));
                break;
            }
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }

    /// <summary>真实"下载"目录：优先注册表 User Shell Folders（支持重定向，如 E:\Downloads），
    /// 失败回退 %USERPROFILE%\Downloads。</summary>
    static string RealDownloadsDir()
    {
        try
        {
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders"))
            {
                if (k != null)
                {
                    object v = k.GetValue("{374DE290-123F-4565-9164-39C4925E467B}");
                    if (v is string)
                    {
                        string p = Environment.ExpandEnvironmentVariables((string)v);
                        if (!string.IsNullOrEmpty(p)) return p;
                    }
                }
            }
        }
        catch { }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
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
        top.Height = 78;   // v2.9：两行（标题行 + 筛选行）
        // 注意：不在此处 Controls.Add(top)。WinForms 的 Dock 布局按"后加入的先排"计算，
        // 若 top 先加入，后加入的 Fill 日志区会先占满整块区域、首行文字被顶部条盖住，
        // 表现为"日志页永远空白"（v2.4.2 修复）。改为日志区先加入、top 最后加入。

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

        // v2.9 筛选行：级别切换 + 搜索 + 导出/复制
        int fx = 20;
        btnLogAll = AddLogFilterBtn(top, ref fx, "log.all", "ALL");
        btnLogInfo = AddLogFilterBtn(top, ref fx, "log.info", "INFO");
        btnLogWarn = AddLogFilterBtn(top, ref fx, "log.warn", "WARN");
        btnLogErr = AddLogFilterBtn(top, ref fx, "log.error", "ERROR");
        btnLogAll.Checked = true;
        txtLogSearch = new TextBox();
        txtLogSearch.Location = new Point(fx + 8, 48);
        txtLogSearch.Size = new Size(180, 24);
        txtLogSearch.TextChanged += delegate(object s, EventArgs e) { logQuery = txtLogSearch.Text.Trim(); RenderLog(); };
        top.Controls.Add(txtLogSearch);
        btnLogExport = new RButton();
        btnLogExport.Size = new Size(64, 24);
        btnLogExport.Location = new Point(fx + 196, 48);
        btnLogExport.Text = L10N._("log.export");
        btnLogExport.Click += delegate(object s, EventArgs e) { ExportLog(); };
        top.Controls.Add(btnLogExport);
        btnLogCopy = new RButton();
        btnLogCopy.Size = new Size(64, 24);
        btnLogCopy.Location = new Point(fx + 268, 48);
        btnLogCopy.Text = L10N._("log.copy");
        btnLogCopy.Click += delegate(object s, EventArgs e) { CopyLog(); };
        top.Controls.Add(btnLogCopy);

        txtLog = new TextBox();
        txtLog.Multiline = true;
        txtLog.ReadOnly = true;
        txtLog.Dock = DockStyle.Fill;
        txtLog.ScrollBars = ScrollBars.Both;   // v1 审查 #11：横向也滚动
        txtLog.WordWrap = false;
        p.Controls.Add(txtLog);
        // 补写日志页构建前暂存的行（如"已自动解出内嵌核心"）
        foreach (string[] e in pendingLog) logEntries.Add(e);
        pendingLog.Clear();
        RenderLog();
        p.Controls.Add(top);   // 顶部条最后加入：Fill 的日志区才能拿到"剩余空间"（Dock 后加入的先排）

        return p;
    }

    // ---- 体检页（v2.5 doctor）----
    // ---- 备份管理页（v2.6 Backup Manager）----
    static string FmtSize(long b)
    {
        if (b < 1024) return b + " B";
        if (b < 1024L * 1024) return (b / 1024.0).ToString("0.0") + " KB";
        if (b < 1024L * 1024 * 1024) return (b / (1024.0 * 1024)).ToString("0.0") + " MB";
        return (b / (1024.0 * 1024 * 1024)).ToString("0.00") + " GB";
    }

    Panel BuildBackup()
    {
        Panel p = new Panel();
        Panel top = new Panel();
        top.Dock = DockStyle.Top;
        top.Height = 44;

        Label t = new RLabel();
        t.AutoSize = true;
        t.Location = new Point(20, 12);
        t.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
        t.Text = L10N._("backup.title");
        t.Tag = "backup.title";
        top.Controls.Add(t);

        btnBkRefresh = new RButton();
        btnBkRefresh.Size = new Size(72, 28);
        btnBkRefresh.Location = new Point(560, 8);
        btnBkRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnBkRefresh.Text = L10N._("backup.refresh");
        btnBkRefresh.Click += delegate(object s, EventArgs e) { LoadBackupList(); };
        top.Controls.Add(btnBkRefresh);

        btnBkNow = new RButton();
        btnBkNow.Size = new Size(92, 28);
        btnBkNow.Location = new Point(460, 8);
        btnBkNow.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnBkNow.Text = L10N._("backup.now");
        btnBkNow.Click += delegate(object s, EventArgs e) { OnAction("act.backup"); };
        top.Controls.Add(btnBkNow);
        top.Resize += delegate(object s, EventArgs e)
        {
            btnBkRefresh.Location = new Point(top.ClientSize.Width - 72 - 20, 8);
            btnBkNow.Location = new Point(top.ClientSize.Width - 72 - 92 - 28, 8);
        };

        Panel bottom = new Panel();
        bottom.Dock = DockStyle.Bottom;
        bottom.Height = 46;
        btnBkRestore = new RButton();
        btnBkRestore.Size = new Size(110, 30);
        btnBkRestore.Location = new Point(20, 8);
        btnBkRestore.Text = L10N._("backup.restore");
        btnBkRestore.Click += delegate(object s, EventArgs e) { BkRestoreSelected(); };
        bottom.Controls.Add(btnBkRestore);
        btnBkExport = new RButton();
        btnBkExport.Size = new Size(110, 30);
        btnBkExport.Location = new Point(140, 8);
        btnBkExport.Text = L10N._("backup.export");
        btnBkExport.Click += delegate(object s, EventArgs e) { BkExportSelected(); };
        bottom.Controls.Add(btnBkExport);
        btnBkDelete = new RButton();
        btnBkDelete.Size = new Size(110, 30);
        btnBkDelete.Location = new Point(260, 8);
        btnBkDelete.Text = L10N._("backup.delete");
        btnBkDelete.Click += delegate(object s, EventArgs e) { BkDeleteSelected(); };
        bottom.Controls.Add(btnBkDelete);

        lvBackups = new ListView();
        lvBackups.Dock = DockStyle.Fill;
        lvBackups.View = View.Details;
        lvBackups.FullRowSelect = true;
        lvBackups.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        lvBackups.Columns.Add(L10N._("backup.col.time"), 150);
        lvBackups.Columns.Add(L10N._("backup.col.kind"), 90);
        lvBackups.Columns.Add(L10N._("backup.col.size"), 90);
        lvBackups.Columns.Add(L10N._("backup.col.state"), 60);
        lvBackups.Columns.Add(L10N._("backup.col.name"), 280);

        p.Controls.Add(lvBackups);   // Fill 先加
        p.Controls.Add(bottom);      // Bottom 次之
        p.Controls.Add(top);         // Top 最后加（Dock 逆序规则，同日志页）
        return p;
    }

    void LoadBackupList()
    {
        string core = CoreExePath();
        if (core == null) { LogWarn(L10N._("op.coremissing")); return; }
        ThreadPool.QueueUserWorkItem(delegate(object _)
        {
            CoreRunResult r = RunCoreCapture(core, "backup-list --detail", 15000);
            BeginInvoke((Action)delegate { OnBackupListReady(r); });
        });
    }

    void OnBackupListReady(CoreRunResult r)
    {
        lvBackups.Items.Clear();
        bkEntries.Clear();
        string[] lines = (r.All ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string pendingPath = null;
        foreach (string ln in lines)
        {
            if (ln.StartsWith("BACKUP_ITEM "))
            {
                string[] parts = ln.Substring("BACKUP_ITEM ".Length).Split(new[] { ' ' }, 4);
                if (pendingPath != null && parts.Length >= 4)
                {
                    bkEntries.Add(new string[] { parts[0], parts[1], parts[2], parts[3], pendingPath });
                    string size = parts[2];
                    long bv;
                    if (long.TryParse(size, out bv)) size = FmtSize(bv);
                    lvBackups.Items.Add(new ListViewItem(new string[] { parts[3], parts[1], size, "✓", parts[0] }));
                }
                pendingPath = null;
            }
            else if (ln.StartsWith("BACKUP_LIST_OK")) { pendingPath = null; }
            else if (pendingPath == null && (ln.IndexOf('\\') >= 0 || ln.IndexOf('/') >= 0)) { pendingPath = ln; }
        }
    }

    string[] BkSelected()
    {
        if (lvBackups == null || lvBackups.SelectedIndices.Count == 0) return null;
        int i = lvBackups.SelectedIndices[0];
        return (i >= 0 && i < bkEntries.Count) ? bkEntries[i] : null;
    }

    void BkRestoreSelected()
    {
        string[] e = BkSelected();
        if (e == null) { LogLine(L10N._("backup.nosel")); return; }
        // v2.7：恢复必须先停服务——先拦下来，省得 dry-run 白跑一趟再报错
        if (currentKind == SKind.Up || currentKind == SKind.Starting)
        {
            LogWarn(L10N._("backup.restore") + " → " + L10N._("rp.running"));
            MessageBox.Show(this, L10N._("rp.running"), L10N._("backup.restore"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        string core = CoreExePath();
        if (core == null) { LogWarn(L10N._("op.coremissing")); return; }
        Interlocked.Exchange(ref busy, 1);
        UpdateActionButtons();
        string path = e[4];
        ThreadPool.QueueUserWorkItem(delegate(object _)
        {
            CoreRunResult r = RunCoreCapture(core, "restore --path \"" + path + "\" --dry-run", 60000);
            BeginInvoke((Action)delegate { OnBkDryRunReady(path, r); });
        });
    }

    void OnBkDryRunReady(string path, CoreRunResult r)
    {
        Interlocked.Exchange(ref busy, 0);
        UpdateActionButtons();
        string all = r.All ?? "";
        if (!all.Contains("DRYRUN_OK")) { LogLine(L10N._("backup.restore") + " → " + L10N._("op.fail") + " (dry-run)"); return; }
        long tn = 0, to = 0, tk = 0, tb = 0;
        foreach (string ln in all.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pp = ln.Split(new[] { ' ' });
            if (pp.Length >= 5 && pp[0] == "DRYRUN_TOTAL")
            {
                long.TryParse(pp[1], out tn);
                long.TryParse(pp[2], out to);
                long.TryParse(pp[3], out tk);
                long.TryParse(pp[4], out tb);
            }
        }
        string msg = string.Format(L10N._("backup.dryrun.msg"), tn, to, tk, FmtSize(tb), Path.GetFileName(path));
        if (MessageBox.Show(this, msg, L10N._("backup.dryrun.title"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        Interlocked.Exchange(ref busy, 1);
        UpdateActionButtons();
        LaunchCapture("restore --path \"" + path + "\"", "act.restore");
    }

    void BkExportSelected()
    {
        string[] e = BkSelected();
        if (e == null) { LogLine(L10N._("backup.nosel")); return; }
        using (var dlg = new FolderBrowserDialog())
        {
            dlg.Description = L10N._("backup.export");
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            Interlocked.Exchange(ref busy, 1);
            UpdateActionButtons();
            LaunchCapture("backup-export --path \"" + e[4] + "\" --to \"" + dlg.SelectedPath + "\"", "bk.export");
        }
    }

    void BkDeleteSelected()
    {
        string[] e = BkSelected();
        if (e == null) { LogLine(L10N._("backup.nosel")); return; }
        if (MessageBox.Show(this, string.Format(L10N._("backup.del.msg"), e[0]), L10N._("backup.del.title"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        Interlocked.Exchange(ref busy, 1);
        UpdateActionButtons();
        LaunchCapture("backup-delete --path \"" + e[4] + "\"", "bk.delete");
    }

    // ---- 设置页（v2.8 Configuration）----
    Panel BuildSettings()
    {
        Panel p = new Panel();
        Panel top = new Panel();
        top.Dock = DockStyle.Top;
        top.Height = 44;

        Label t = new RLabel();
        t.AutoSize = true;
        t.Location = new Point(20, 12);
        t.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
        t.Text = L10N._("settings.title");
        t.Tag = "settings.title";
        top.Controls.Add(t);

        btnSetSave = new RButton();
        btnSetSave.Size = new Size(92, 28);
        btnSetSave.Location = new Point(600, 8);
        btnSetSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnSetSave.Text = L10N._("settings.save");
        btnSetSave.Click += delegate(object s, EventArgs e) { SaveSettings(); };
        top.Controls.Add(btnSetSave);
        top.Resize += delegate(object s, EventArgs e)
        {
            btnSetSave.Location = new Point(top.ClientSize.Width - 92 - 20, 8);
        };

        int y = 56;
        AddSetGroup(p, ref y, "settings.g.harness");
        cmbHost = AddSetCombo(p, ref y, "settings.host", new string[] { "127.0.0.1", "localhost" });
        txtWs = AddSetText(p, ref y, "settings.ws", 360);
        AddSetGroup(p, ref y, "settings.g.backup");
        txtKeep = AddSetText(p, ref y, "settings.keep", 80);
        AddSetGroup(p, ref y, "settings.g.update");
        cmbChkUpd = AddSetCombo(p, ref y, "settings.chkupd", new string[] { "on", "off" });
        cmbChkDshUpd = AddSetCombo(p, ref y, "settings.chkdsh", new string[] { "on", "off" });
        cmbChannel = AddSetCombo(p, ref y, "settings.channel", new string[] { "stable", "rc" });
        AddSetGroup(p, ref y, "settings.g.toolkit");
        cmbLang = AddSetCombo(p, ref y, "settings.lang", new string[] { "auto", "zh", "en" });
        cmbAutoStart = AddSetCombo(p, ref y, "settings.autostart", new string[] { "on", "off" });
        cmbCloseAct = AddSetCombo(p, ref y, "settings.closeact", new string[] { "ask", "tray", "exit" });

        Label note = new RLabel();
        note.AutoSize = true;
        note.Location = new Point(24, y + 6);
        note.Text = L10N._("settings.note");
        note.Tag = "settings.note";
        p.Controls.Add(note);

        p.Controls.Add(top);   // top 最后加（Dock 逆序规则）
        return p;
    }

    void AddSetGroup(Panel parent, ref int y, string tag)
    {
        Label l = new RLabel();
        l.AutoSize = true;
        l.Location = new Point(24, y);
        l.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
        l.Text = L10N._(tag);
        l.Tag = tag;
        parent.Controls.Add(l);
        y += 26;
    }

    ComboBox AddSetCombo(Panel parent, ref int y, string tag, string[] items)
    {
        Label l = new RLabel();
        l.AutoSize = true;
        l.Location = new Point(40, y + 3);
        l.Text = L10N._(tag);
        l.Tag = tag;
        parent.Controls.Add(l);
        ComboBox c = new ComboBox();
        c.DropDownStyle = ComboBoxStyle.DropDownList;
        c.Location = new Point(240, y);
        c.Size = new Size(160, 24);
        foreach (string it in items) c.Items.Add(it);
        parent.Controls.Add(c);
        y += 30;
        return c;
    }

    TextBox AddSetText(Panel parent, ref int y, string tag, int width)
    {
        Label l = new RLabel();
        l.AutoSize = true;
        l.Location = new Point(40, y + 3);
        l.Text = L10N._(tag);
        l.Tag = tag;
        parent.Controls.Add(l);
        TextBox tb = new TextBox();
        tb.Location = new Point(240, y);
        tb.Size = new Size(width, 24);
        parent.Controls.Add(tb);
        y += 30;
        return tb;
    }

    void LoadSettings()
    {
        string core = CoreExePath();
        if (core == null) { LogWarn(L10N._("op.coremissing")); return; }
        ThreadPool.QueueUserWorkItem(delegate(object _)
        {
            CoreRunResult r = RunCoreCapture(core, "config-get", 10000);
            BeginInvoke((Action)delegate { OnSettingsReady(r); });
        });
    }

    void OnSettingsReady(CoreRunResult r)
    {
        setOrig.Clear();
        foreach (string ln in (r.All ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!ln.StartsWith("CONFIG ")) continue;
            string[] pp = ln.Substring(7).Split(new[] { ' ' }, 2);
            if (pp.Length >= 1) setOrig[pp[0]] = pp.Length >= 2 ? pp[1] : "";
        }
        string g;
        if (cmbHost != null && setOrig.TryGetValue("host", out g)) cmbHost.SelectedItem = g;
        if (txtWs != null && setOrig.TryGetValue("ws", out g)) txtWs.Text = g;
        if (txtKeep != null && setOrig.TryGetValue("keep_backups", out g)) txtKeep.Text = g;
        if (cmbChkUpd != null && setOrig.TryGetValue("check_update", out g)) cmbChkUpd.SelectedItem = g;
        if (cmbChkDshUpd != null && setOrig.TryGetValue("check_dsh_update", out g)) cmbChkDshUpd.SelectedItem = g;
        if (cmbChannel != null && setOrig.TryGetValue("update_channel", out g)) cmbChannel.SelectedItem = g;
        if (cmbLang != null && setOrig.TryGetValue("lang", out g)) cmbLang.SelectedItem = g;
        if (cmbAutoStart != null && setOrig.TryGetValue("auto_start", out g)) cmbAutoStart.SelectedItem = g;
        // close_action：配置里空串="还没问过"，在设置页显示为 ask（首次关窗仍会询问并记住）
        if (cmbCloseAct != null)
        {
            string ca = "";
            if (setOrig.TryGetValue("close_action", out g)) ca = g;
            cmbCloseAct.SelectedItem = ca.Length == 0 ? "ask" : ca;
        }
    }

    void SaveSettings()
    {
        var changes = new List<string[]>();
        string g;
        if (cmbHost != null && cmbHost.SelectedItem != null && setOrig.TryGetValue("host", out g) && cmbHost.SelectedItem.ToString() != g) changes.Add(new string[] { "host", cmbHost.SelectedItem.ToString() });
        if (txtWs != null && setOrig.TryGetValue("ws", out g) && txtWs.Text.Trim() != g) changes.Add(new string[] { "ws", txtWs.Text.Trim() });
        if (txtKeep != null && setOrig.TryGetValue("keep_backups", out g) && txtKeep.Text.Trim() != g) changes.Add(new string[] { "keep_backups", txtKeep.Text.Trim() });
        if (cmbChkUpd != null && cmbChkUpd.SelectedItem != null && setOrig.TryGetValue("check_update", out g) && cmbChkUpd.SelectedItem.ToString() != g) changes.Add(new string[] { "check_update", cmbChkUpd.SelectedItem.ToString() });
        if (cmbChkDshUpd != null && cmbChkDshUpd.SelectedItem != null && setOrig.TryGetValue("check_dsh_update", out g) && cmbChkDshUpd.SelectedItem.ToString() != g) changes.Add(new string[] { "check_dsh_update", cmbChkDshUpd.SelectedItem.ToString() });
        if (cmbChannel != null && cmbChannel.SelectedItem != null && setOrig.TryGetValue("update_channel", out g) && cmbChannel.SelectedItem.ToString() != g) changes.Add(new string[] { "update_channel", cmbChannel.SelectedItem.ToString() });
        if (cmbLang != null && cmbLang.SelectedItem != null && setOrig.TryGetValue("lang", out g) && cmbLang.SelectedItem.ToString() != g) changes.Add(new string[] { "lang", cmbLang.SelectedItem.ToString() });
        if (cmbAutoStart != null && cmbAutoStart.SelectedItem != null && setOrig.TryGetValue("auto_start", out g) && cmbAutoStart.SelectedItem.ToString() != g) changes.Add(new string[] { "auto_start", cmbAutoStart.SelectedItem.ToString() });
        if (cmbCloseAct != null && cmbCloseAct.SelectedItem != null)
        {
            string ca = "";
            if (setOrig.TryGetValue("close_action", out g)) ca = g;
            string want = cmbCloseAct.SelectedItem.ToString();
            if (want != ca) changes.Add(new string[] { "close_action", want });
        }
        if (changes.Count == 0) { LogLine(L10N._("settings.title") + " → " + L10N._("op.ok")); return; }
        Interlocked.Exchange(ref busy, 1);
        UpdateActionButtons();
        ThreadPool.QueueUserWorkItem(delegate(object _)
        {
            string core = CoreExePath();
            var results = new List<string>();
            if (core != null)
            {
                foreach (string[] kv in changes)
                {
                    CoreRunResult rr = RunCoreCapture(core, "config-set " + kv[0] + " \"" + kv[1] + "\"", 10000);
                    results.Add(kv[0] + " → " + (((rr.MarkLine ?? "").StartsWith("CONFIGSET_OK")) ? "ok" : "fail"));
                }
            }
            CoreRunResult gr = (core == null) ? new CoreRunResult() : RunCoreCapture(core, "config-get", 10000);
            BeginInvoke((Action)delegate
            {
                Interlocked.Exchange(ref busy, 0);
                UpdateActionButtons();
                foreach (string s in results) LogLine(L10N._("settings.title") + ": " + s);
                OnSettingsReady(gr);
            });
        });
    }

    // ---- 更新中心页（v2.7 Update Center）----
    Panel BuildUpdate()
    {
        Panel p = new Panel();
        Panel top = new Panel();
        top.Dock = DockStyle.Top;
        top.Height = 44;

        Label t = new RLabel();
        t.AutoSize = true;
        t.Location = new Point(20, 12);
        t.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
        t.Text = L10N._("update.title");
        t.Tag = "update.title";
        top.Controls.Add(t);

        btnUpGo = new RButton();
        btnUpGo.Size = new Size(110, 28);
        btnUpGo.Location = new Point(600, 8);
        btnUpGo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnUpGo.Text = L10N._("update.go");
        btnUpGo.Click += delegate(object s, EventArgs e) { OnAction("act.update"); };   // 交互 CLI：版本列表 + 破坏性双确认（更新必须用户确认）
        top.Controls.Add(btnUpGo);
        btnUpCheck = new RButton();
        btnUpCheck.Size = new Size(92, 28);
        btnUpCheck.Location = new Point(480, 8);
        btnUpCheck.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnUpCheck.Text = L10N._("update.check");
        btnUpCheck.Click += delegate(object s, EventArgs e) { LoadUpdateInfo(); };
        top.Controls.Add(btnUpCheck);
        top.Resize += delegate(object s, EventArgs e)
        {
            btnUpGo.Location = new Point(top.ClientSize.Width - 110 - 20, 8);
            btnUpCheck.Location = new Point(top.ClientSize.Width - 110 - 92 - 28, 8);
        };

        int y = 56;   // 顶栏 44px 之下起排
        lblUpCur = AddUpRow(p, ref y, "update.current");
        lblUpStable = AddUpRow(p, ref y, "update.latest.stable");
        lblUpRc = AddUpRow(p, ref y, "update.latest.rc");
        lblUpChannel = AddUpRow(p, ref y, "update.channel");
        lblUpPre = AddUpRow(p, ref y, "update.prebackup");
        lblUpRoll = AddUpRow(p, ref y, "update.rollback");
        lblUpNotes = AddUpRow(p, ref y, "update.notes");

        p.Controls.Add(top);   // 行标签为绝对定位普通子控件；top 最后加（Dock 逆序规则）
        return p;
    }

    Label AddUpRow(Panel parent, ref int y, string tag)
    {
        Label l = new RLabel();
        l.AutoSize = true;
        l.Location = new Point(24, y);
        l.Text = L10N._(tag) + ": —";
        l.Tag = tag;
        parent.Controls.Add(l);
        y += 30;
        return l;
    }

    void LoadUpdateInfo()
    {
        string core = CoreExePath();
        if (core == null) { LogWarn(L10N._("op.coremissing")); return; }
        ThreadPool.QueueUserWorkItem(delegate(object _)
        {
            CoreRunResult r = RunCoreCapture(core, "update-info", 30000);
            BeginInvoke((Action)delegate { OnUpdateInfoReady(r); });
        });
    }

    void OnUpdateInfoReady(CoreRunResult r)
    {
        var kv = new Dictionary<string, string>();
        foreach (string ln in (r.All ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!ln.StartsWith("UPDATEINFO_")) continue;
            int sp = ln.IndexOf(' ');
            if (sp <= 0) continue;
            kv[ln.Substring(0, sp)] = ln.Substring(sp + 1);
        }
        string g;
        if (lblUpCur != null) lblUpCur.Text = L10N._("update.current") + ": " + (kv.TryGetValue("UPDATEINFO_CURRENT", out g) ? g : "?");
        if (lblUpStable != null) lblUpStable.Text = L10N._("update.latest.stable") + ": " + (kv.TryGetValue("UPDATEINFO_LATEST_STABLE", out g) ? g : "?");
        if (lblUpRc != null) lblUpRc.Text = L10N._("update.latest.rc") + ": " + (kv.TryGetValue("UPDATEINFO_LATEST_RC", out g) ? g : "?");
        if (lblUpChannel != null) lblUpChannel.Text = L10N._("update.channel") + ": " + (kv.TryGetValue("UPDATEINFO_CHANNEL", out g) ? g : "?");
        if (lblUpPre != null) lblUpPre.Text = L10N._("update.prebackup") + ": " + (kv.TryGetValue("UPDATEINFO_PREBACKUP", out g) ? g : "?");
        if (lblUpRoll != null) lblUpRoll.Text = L10N._("update.rollback") + ": " + (kv.TryGetValue("UPDATEINFO_ROLLBACK", out g) ? g : "?");
        if (lblUpNotes != null) lblUpNotes.Text = L10N._("update.notes") + ": " + (kv.TryGetValue("UPDATEINFO_NOTES_URL", out g) ? g : "?");
    }

    Panel BuildDoctor()
    {
        Panel p = new Panel();
        Panel top = new Panel();
        top.Dock = DockStyle.Top;
        top.Height = 44;

        Label t = new RLabel();
        t.AutoSize = true;
        t.Location = new Point(20, 12);
        t.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
        t.Text = L10N._("doc.title");
        t.Tag = "doc.title";
        top.Controls.Add(t);

        btnDocRecheck = new RButton();
        btnDocRecheck.Size = new Size(92, 28);
        // 初始坐标必须为正（构建期 top 宽度可能为 0，负坐标会把按钮甩到窗口外、点击落空）；
        // Anchor 右对齐 + top.Resize 双保险重排。
        btnDocRecheck.Location = new Point(552, 8);
        btnDocRecheck.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnDocRecheck.Text = L10N._("doc.recheck");
        btnDocRecheck.Click += delegate(object s, EventArgs e) { RunDoctor(); };
        top.Controls.Add(btnDocRecheck);

        btnDocExport = new RButton();
        btnDocExport.Size = new Size(92, 28);
        btnDocExport.Location = new Point(652, 8);
        btnDocExport.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnDocExport.Text = L10N._("doc.export");
        btnDocExport.Click += delegate(object s, EventArgs e) { ExportDoctorReport(); };
        top.Controls.Add(btnDocExport);
        top.Resize += delegate(object s, EventArgs e)
        {
            btnDocRecheck.Location = new Point(top.ClientSize.Width - 92 - 92 - 28, 8);
            btnDocExport.Location = new Point(top.ClientSize.Width - 92 - 20, 8);
        };

        txtDoctor = new TextBox();
        txtDoctor.Multiline = true;
        txtDoctor.ReadOnly = true;
        txtDoctor.Dock = DockStyle.Fill;
        txtDoctor.ScrollBars = ScrollBars.Both;
        txtDoctor.WordWrap = false;
        txtDoctor.Text = L10N._("doc.empty");
        p.Controls.Add(txtDoctor);
        p.Controls.Add(top);   // top 最后加入（Dock 逆序规则，同日志页）

        return p;
    }

    // ---- 体检：后台跑 core doctor --report <temp>，结果回显（全程只读）----
    void RunDoctor()
    {
        string core = CoreExePath();
        if (core == null) { txtDoctor.Text = L10N._("op.coremissing"); return; }
        btnDocRecheck.Enabled = false;
        txtDoctor.Text = L10N._("doc.running");
        LogLine(L10N._("doc.title") + ": " + L10N._("doc.recheck") + "…");
        string rep = Path.Combine(Path.GetTempPath(), "dsh_doctor_report_" + Process.GetCurrentProcess().Id + ".txt");
        ThreadPool.QueueUserWorkItem(delegate(object _)
        {
            CoreRunResult r = RunCoreCapture(core, "doctor --report \"" + rep + "\"", 60000);
            BeginInvoke((Action)delegate
            {
                doctorReportPath = rep;
                string txt = r.All;
                if (string.IsNullOrWhiteSpace(txt)) txt = L10N._("doc.empty");
                txtDoctor.Text = txt;
                string first = (r.All ?? "").Trim();
                int nl = first.IndexOf('\n');
                if (nl > 0) first = first.Substring(0, nl);
                LogLine(L10N._("doc.title") + " → " + (first.Length > 0 ? first : L10N._("op.ok")));
                btnDocRecheck.Enabled = true;
                RefreshStatus();
            });
        });
    }
    void ExportDoctorReport()
    {
        if (string.IsNullOrEmpty(doctorReportPath) || !File.Exists(doctorReportPath))
        {
            LogLine(L10N._("doc.empty"));
            return;
        }
        using (var dlg = new SaveFileDialog())
        {
            dlg.Title = L10N._("doc.export");
            dlg.FileName = "dsh-doctor-report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
            dlg.Filter = "Text (*.txt)|*.txt";
            dlg.DefaultExt = "txt";
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    File.Copy(doctorReportPath, dlg.FileName, true);
                    LogLine(L10N._("doc.exported") + dlg.FileName);
                }
                catch (Exception ex) { LogLine(L10N._("doc.exportfail") + ex.Message); }
            }
        }
    }

    // ---- 关于页 ----
    Panel BuildAbout()
    {
        Panel p = new Panel();
        picLogo = new PictureBox();
        picLogo.Size = new Size(120, 120);
        picLogo.SizeMode = PictureBoxSizeMode.Zoom;
        picLogo.BackColor = Color.Transparent;
        picLogo.Location = new Point((PageWidth() - 120) / 2, 20);
        p.Controls.Add(picLogo);
        LoadLogo();
        // 关于页 logo 圆角（Region 裁剪，与整体圆角设计一致）
        using (GraphicsPath logoPath = RButton.RoundRect(0, 0, 120, 120, 18))
            picLogo.Region = new Region(logoPath);

        Label name = new RLabel();
        name.AutoSize = true;
        name.Font = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold);
        name.Text = "DeepSeek Harness Toolkit";
        name.Location = new Point(CenterX(name), 160);
        p.Controls.Add(name);

        Label ver = new RLabel();
        ver.AutoSize = true;
        ver.Text = "GUI " + AssemblyVersion() + (HasEmbeddedCore() ? " " + L10N._("about.standalone") : "");
        ver.Location = new Point(CenterX(ver), 194);
        p.Controls.Add(ver);

        Label copy = new RLabel();
        copy.AutoSize = true;
        copy.Text = L10N._("about.copy");
        copy.Tag = "about.copy";
        copy.Location = new Point(CenterX(copy), 220);
        p.Controls.Add(copy);

        Label cred = new RLabel();
        cred.SetBounds(0, 250, 620, 44);
        cred.TextAlign = ContentAlignment.TopCenter;   // 两行各自水平居中
        cred.Text = "v1 脚本协助 : SOGR-Momono Dango（QwenPaw/DeepseekAPI-V4-Flash-0731）\nv2 重构封装 : DeepSeek DSH（DSH/DeepseekAPI-V4-Flash-0731）";
        p.Controls.Add(cred);

        // v2.7：验证此安装（联网比对官方 Release 的 hashes.txt；只读，不改任何文件）
        btnVerifyInstall = new RButton();
        btnVerifyInstall.Size = new Size(160, 30);
        btnVerifyInstall.Location = new Point((PageWidth() - 160) / 2, 302);
        btnVerifyInstall.Text = L10N._("verify.btn");
        btnVerifyInstall.Click += delegate(object s, EventArgs e) { RunVerifyInstall(); };
        p.Controls.Add(btnVerifyInstall);

        lblVerifyResult = new RLabel();
        lblVerifyResult.SetBounds(20, 344, 580, 116);
        lblVerifyResult.TextAlign = ContentAlignment.TopLeft;
        lblVerifyResult.Font = new Font("Microsoft YaHei UI", 8.5f);
        lblVerifyResult.ForeColor = Th.FgDim;
        lblVerifyResult.Tag = "verify.result";
        p.Controls.Add(lblVerifyResult);
        RenderVerifyResult();

        return p;
    }

    // ================= v2.7：完整性校验（关于页「验证此安装」+ 首启自检） =================

    // 官方最新 Release 的纯文本清单（零 JSON、零第三方依赖）
    const string OFFICIAL_MANIFEST_URL = "https://github.com/sakanamaru/DeepSeek-Harness-Toolkit/releases/latest/download/hashes.txt";

    /// <summary>文件 SHA-256（小写 hex）；失败返回 null。</summary>
    static string Sha256OfFile(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            using (FileStream fs = File.OpenRead(path))
            {
                byte[] h = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
        catch { return null; }
    }

    /// <summary>从 hashes.txt 文本取某文件的 SHA-256（小写）；未找到/格式非法返回 null。纯函数。</summary>
    static string ManifestHashOf(string manifest, string fileName)
    {
        if (string.IsNullOrEmpty(manifest) || string.IsNullOrEmpty(fileName)) return null;
        string[] lines = manifest.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string ln in lines)
        {
            string t = ln.Trim();
            if (t.Length == 0 || t.StartsWith("#")) continue;
            int sp = t.IndexOf(' ');
            if (sp <= 0) continue;
            string hash = t.Substring(0, sp).Trim().ToLowerInvariant();
            string nm = t.Substring(sp + 1).Trim();
            if (string.Compare(nm, fileName, StringComparison.OrdinalIgnoreCase) != 0) continue;
            if (hash.Length != 64) return null;
            for (int i = 0; i < hash.Length; i++) { if (!Uri.IsHexDigit(hash[i])) return null; }
            return hash;
        }
        return null;
    }

    /// <summary>本机随包 hashes.txt（与 exe 同目录）；没有则 null。</summary>
    static string LocalManifest()
    {
        try
        {
            string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hashes.txt");
            if (!File.Exists(p)) return null;
            return File.ReadAllText(p, Encoding.UTF8);
        }
        catch { return null; }
    }

    /// <summary>联网取官方最新 Release 的 hashes.txt；离线/失败/内容不像清单 → null。</summary>
    static string FetchOfficialManifest()
    {
        try { System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072; } catch { }
        try
        {
            System.Net.WebRequest req = System.Net.WebRequest.Create(OFFICIAL_MANIFEST_URL);
            req.Timeout = 8000;
            try { req.Headers.Add("User-Agent", "dsh-toolkit-gui"); } catch { }
            using (System.Net.WebResponse resp = req.GetResponse())
            using (Stream s = resp.GetResponseStream())
            using (StreamReader sr = new StreamReader(s, Encoding.UTF8))
            {
                string txt = sr.ReadToEnd();
                // 认一下内容：必须像我们的清单（含 SHA-256 表头），避免把错误页当清单用
                if (txt != null && txt.IndexOf("SHA-256", StringComparison.OrdinalIgnoreCase) >= 0) return txt;
                return null;
            }
        }
        catch { return null; }
    }

    /// <summary>单文件比对：官方清单优先（remoteCompared=true），否则用随包清单。
    /// compared=清单里有这个文件（否则无从比对）；mismatch=找到了但哈希不一致。</summary>
    static void VerifyOneExe(string file, string localManifest, string remoteManifest, out bool compared, out bool remoteCompared, out bool mismatch)
    {
        compared = false; remoteCompared = false; mismatch = false;
        try
        {
            string name = Path.GetFileName(file);
            string remote = ManifestHashOf(remoteManifest, name);
            string want = remote;
            remoteCompared = (remote != null);
            if (want == null) want = ManifestHashOf(localManifest, name);
            if (want == null) return;                 // 两个清单都没有该文件
            compared = true;
            string got = Sha256OfFile(file);
            mismatch = (got == null) || (got != want);
        }
        catch { }
    }

    string CoreExePathForVerify() { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeepSeek Harness Toolkit.exe"); }

    /// <summary>跑一次三态报告：返回 0=未验证 1=一致 2=不一致 3=未能验证，并填充 verifyReport。</summary>
    int BuildVerifyResult(string remote, string local)
    {
        bool c1, r1, m1, c2, r2, m2;
        VerifyOneExe(CoreExePathForVerify(), local, remote, out c1, out r1, out m1);
        VerifyOneExe(Application.ExecutablePath, local, remote, out c2, out r2, out m2);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(LineFor("DeepSeek Harness Toolkit.exe", c1, m1, remote != null || local != null));
        // 报告里用真实文件名：attached 版是 Toolkit GUI.exe，standalone 版是 Toolkit GUI Standalone.exe
        // （清单查找本来就按真实文件名，这里只是让显示与实际核对的对象一致）
        string guiName = "Toolkit GUI.exe";
        try { guiName = Path.GetFileName(Application.ExecutablePath); } catch { }
        sb.AppendLine(LineFor(guiName, c2, m2, remote != null || local != null));
        sb.Append(L10N._("verify.note"));
        verifyReport = sb.ToString();

        if (!c1 && !c2) return 3;                       // 两份都没比对上 → 未能验证
        if ((c1 && m1) || (c2 && m2)) return 2;         // 任一不一致 → 不一致
        return 1;
    }

    string LineFor(string name, bool compared, bool mismatch, bool haveAnyList)
    {
        if (!haveAnyList) return string.Format(L10N._("verify.line.nolist"), name);
        if (!compared) return string.Format(L10N._("verify.line.skip"), name);
        return string.Format(L10N._(mismatch ? "verify.line.bad" : "verify.line.ok"), name);
    }

    /// <summary>关于页结果区渲染（三态 + 明细；语言切换后调用即可按新语言重排）。</summary>
    void RenderVerifyResult()
    {
        if (lblVerifyResult == null) return;
        string head;
        if (verifyState == 9) head = L10N._("verify.running");
        else if (verifyState == 1) head = L10N._("verify.state.ok");
        else if (verifyState == 2) head = L10N._("verify.state.bad");
        else if (verifyState == 3) head = L10N._("verify.state.none") + L10N._("verify.reason.nolist");
        else head = L10N._("verify.state.idle");
        lblVerifyResult.Text = head + (verifyReport.Length > 0 ? "\n" + verifyReport : "");
        lblVerifyResult.ForeColor = (verifyState == 2) ? Color.FromArgb(0xE8, 0x11, 0x23) : Th.FgDim;
    }

    /// <summary>关于页「验证此安装」：联网取官方清单 → 比对核心 exe 与 GUI exe（只读，不改任何文件）。</summary>
    void RunVerifyInstall()
    {
        if (Interlocked.CompareExchange(ref busy, 1, 0) != 0) { LogLine(L10N._("op.busy")); return; }
        UpdateActionButtons();
        verifyState = 9;
        RenderVerifyResult();
        LogLine(L10N._("verify.running"));
        ThreadPool.QueueUserWorkItem(delegate(object _)
        {
            int state;
            string remote = null;
            try
            {
                remote = FetchOfficialManifest();
                string local = LocalManifest();
                state = BuildVerifyResult(remote, local);
            }
            catch { state = 3; }
            string rep = verifyReport;
            BeginInvoke((Action)delegate
            {
                verifyReport = rep;
                verifyState = state;
                RenderVerifyResult();
                Interlocked.Exchange(ref busy, 0);
                UpdateActionButtons();
                if (state == 1) LogLine(L10N._("verify.state.ok"));
                else if (state == 2) LogErr(L10N._("verify.state.bad"));
                else LogWarn(L10N._("verify.state.none") + (remote == null ? L10N._("verify.reason.offline") : L10N._("verify.reason.nolist")));
                ShowToast(L10N._(state == 1 ? "verify.state.ok" : (state == 2 ? "verify.state.bad" : "verify.state.none")));
            });
        });
    }

    /// <summary>启动时的本地一致性检查（不联网）：随包清单存在且本机 exe 不符 → 记错误 + 托盘提示。</summary>
    void CheckLocalIntegrityAtStartup()
    {
        try
        {
            string local = LocalManifest();
            if (local == null) return;   // 单独复制 exe：无从比对，静默（不打扰）
            bool c1, r1, m1, c2, r2, m2;
            VerifyOneExe(CoreExePathForVerify(), local, null, out c1, out r1, out m1);
            VerifyOneExe(Application.ExecutablePath, local, null, out c2, out r2, out m2);
            if ((c1 && m1) || (c2 && m2))
            {
                verifyState = 2;
                BuildVerifyResult(null, local);   // 填充明细（不复用上面的 out，保持代码直观）
                RenderVerifyResult();
                LogErr(L10N._("verify.localmismatch"));
                ShowToast(L10N._("verify.localmismatch"));
            }
        }
        catch { }
    }

    /// <summary>「来自网络」标记（Mark of the Web）提醒：只记日志，不弹窗（杀软误报属常见现象）。</summary>
    void CheckMotwWarning()
    {
        try
        {
            string ads = Application.ExecutablePath + ":Zone.Identifier";
            if (!File.Exists(ads)) return;
            string txt = File.ReadAllText(ads);
            int zone = 0;
            foreach (string ln in txt.Split('\n'))
            {
                string t = ln.Trim();
                if (t.StartsWith("ZoneId=", StringComparison.OrdinalIgnoreCase)) { int.TryParse(t.Substring(7).Trim(), out zone); }
            }
            if (zone == 3 || zone == 4) { LogWarn(L10N._("motw.warn")); return; }
            // Win10/11 有时写的是 [ZoneTransfer] 段而不是 ZoneId：只要出现该段就按"来自网络"处理
            if (txt.IndexOf("ZoneTransfer", StringComparison.OrdinalIgnoreCase) >= 0) LogWarn(L10N._("motw.warn"));
        }
        catch { }
    }

    /// <summary>本机状态目录（与核心一致：exe 目录可写就用它，否则 %APPDATA%\DeepSeekHarnessLauncher）。</summary>
    string StateDirGuess()
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        try
        {
            if (File.Exists(Path.Combine(dir, "launcher.config"))) return dir;
            string probe = Path.Combine(dir, ".gui-write-test");
            using (File.Create(probe)) { }
            File.Delete(probe);
            return dir;
        }
        catch { }
        string alt = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeepSeekHarnessLauncher");
        return alt;
    }

    /// <summary>首次启动：只做完整性自检（用户明确要求：首启环境本来就是空的，不列环境清单、不谈备份）。</summary>
    void CheckFirstRun()
    {
        try
        {
            string dir = StateDirGuess();
            string mark = Path.Combine(dir, ".gui_firstrun_done");
            if (File.Exists(mark)) return;
            try { File.WriteAllText(mark, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), new UTF8Encoding(false)); } catch { }

            string local = LocalManifest();
            int state = BuildVerifyResult(null, local);
            string head = (state == 2) ? L10N._("verify.state.bad")
                        : (state == 1 ? L10N._("verify.state.ok")
                        : (local == null ? L10N._("verify.reason.nolist") : L10N._("verify.state.none")));
            string body = head + "\n\n" + verifyReport;
            try { MessageBox.Show(this, body, L10N._("firstrun.title"), MessageBoxButtons.OK, MessageBoxIcon.Information); }
            catch { }
            verifyState = state;
            RenderVerifyResult();
            LogLine(L10N._("firstrun.log"));
        }
        catch { }
    }

    // 关于页内容区宽（content Frame 宽度）
    int PageWidth() { return 620; }

    // 文字水平居中 X（按控件当前 Font/Text 测量）
    int CenterX(Label lbl)
    {
        try
        {
            Size s = TextRenderer.MeasureText(lbl.Text ?? "", lbl.Font);
            return (PageWidth() - s.Width) / 2;
        }
        catch { return 0; }
    }

    void LoadLogo()
    {
        if (picLogo == null) return;
        // 优先内嵌资源（编译时 /resource:logo.png，单文件分发无需外部 logo.png），缺失则退读同目录文件
        try
        {
            foreach (string n in typeof(App).Assembly.GetManifestResourceNames())
            {
                if (n.EndsWith("logo.png", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream s = typeof(App).Assembly.GetManifestResourceStream(n))
                    {
                        if (s != null) { picLogo.Image = Image.FromStream(s); return; }
                    }
                }
            }
        }
        catch { }
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
        catch { return "2.7.0"; }
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
        if (idx == 1 && lvBackups != null && bkEntries.Count == 0) LoadBackupList();   // 首次进入备份页自动拉列表
        if (idx == 2 && !upInfoLoaded) { upInfoLoaded = true; LoadUpdateInfo(); }      // 首次进入更新页自动拉数据
        if (idx == 3 && setOrig.Count == 0) LoadSettings();                            // 首次进入设置页自动拉配置
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

        // v2.7 状态栏（围绕 = Panel 色，避免透明底拾到标题栏像素产生重影）
        if (statBar != null) statBar.BackColor = t.Panel;
        if (lblStatusBar is RLabel) { (lblStatusBar as RLabel).Surround = t.Panel; }
        if (lblStatusBar != null) lblStatusBar.ForeColor = t.FgDim;

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
        UpdateStatusBar();       // v2.7：状态栏含主题/语言字样，主题切换后重渲染
        RenderVerifyResult();    // v2.7：关于页结果区（不一致时是红字，需跟随主题恢复）

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
        string[] navL10N = new string[] { "nav.home", "nav.backup", "nav.update", "nav.settings", "nav.log", "nav.doctor", "nav.about" };
        for (int i = 0; i < navBtns.Length; i++)
            navBtns[i].Text = L10N._(navL10N[i]);
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
        foreach (Control c in pages[4].Controls)
        {
            if (c is Label && c.Tag is string && (string)c.Tag == "log.title") c.Text = L10N._("log.title");
        }
        btnClearLog.Text = L10N._("log.clear");
        if (btnLogAll != null) btnLogAll.Text = L10N._("log.all");
        if (btnLogInfo != null) btnLogInfo.Text = L10N._("log.info");
        if (btnLogWarn != null) btnLogWarn.Text = L10N._("log.warn");
        if (btnLogErr != null) btnLogErr.Text = L10N._("log.error");
        if (btnLogExport != null) btnLogExport.Text = L10N._("log.export");
        if (btnLogCopy != null) btnLogCopy.Text = L10N._("log.copy");
        // 备份页
        foreach (Control c in pages[1].Controls)
        {
            if (c is Label && c.Tag is string && (string)c.Tag == "backup.title") c.Text = L10N._("backup.title");
        }
        if (lvBackups != null && lvBackups.Columns.Count >= 5)
        {
            lvBackups.Columns[0].Text = L10N._("backup.col.time");
            lvBackups.Columns[1].Text = L10N._("backup.col.kind");
            lvBackups.Columns[2].Text = L10N._("backup.col.size");
            lvBackups.Columns[3].Text = L10N._("backup.col.state");
            lvBackups.Columns[4].Text = L10N._("backup.col.name");
        }
        if (btnBkNow != null) btnBkNow.Text = L10N._("backup.now");
        if (btnBkRefresh != null) btnBkRefresh.Text = L10N._("backup.refresh");
        if (btnBkRestore != null) btnBkRestore.Text = L10N._("backup.restore");
        if (btnBkExport != null) btnBkExport.Text = L10N._("backup.export");
        if (btnBkDelete != null) btnBkDelete.Text = L10N._("backup.delete");
        // 设置页
        foreach (Control c in pages[3].Controls)
        {
            if (c is Label && c.Tag is string)
            {
                string tag = (string)c.Tag;
                if (tag.StartsWith("settings.")) c.Text = L10N._(tag);
            }
        }
        // 体检页
        foreach (Control c in pages[5].Controls)
        {
            if (c is Label && c.Tag is string && (string)c.Tag == "doc.title") c.Text = L10N._("doc.title");
        }
        // 关于页
        foreach (Control c in pages[6].Controls)
        {
            if (c is Label && c.Tag is string && (string)c.Tag == "about.copy") c.Text = L10N._("about.copy");
        }
        if (btnDocRecheck != null) btnDocRecheck.Text = L10N._("doc.recheck");
        if (btnDocExport != null) btnDocExport.Text = L10N._("doc.export");
        if (upInfoLoaded) LoadUpdateInfo();   // 语言切换后以新语言刷新更新中心数值
        RefreshTrayTexts();                   // v2.7：托盘菜单文案跟随语言
        UpdateStatusBar();
        UpdateInstallButtonLabel();           // 安装/修复 按钮文案跟随语言
        if (btnVerifyInstall != null) btnVerifyInstall.Text = L10N._("verify.btn");
        RenderVerifyResult();                 // v2.7：关于页验证结果跟随语言
    }

    /// <summary>托盘右键菜单文案跟随语言（菜单项不是 Control，ApplyLang 的遍历覆盖不到）。</summary>
    void RefreshTrayTexts()
    {
        try
        {
            if (trayMenu == null || trayMenu.Items.Count < 6) return;
            trayMenu.Items[0].Text = L10N._("tray.show");
            trayMenu.Items[2].Text = L10N._("tray.start");
            trayMenu.Items[3].Text = L10N._("tray.stop");
            trayMenu.Items[5].Text = L10N._("tray.exit");
        }
        catch { }
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
        if (CoreExePath() == null) return L10N._("home.status.nocore");
        return L10N._("home.status.unknown");
    }

    bool coreWarnLogged = false;

    // 防误用：exe 被直接拖到桌面/下载目录运行时弹窗提醒。
    // 不强行阻止（用户自由），但明确告知并建议移到独立文件夹。
    void CheckBadDirWarning()
    {
        try
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            string[] bad = new string[] {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                RealDownloadsDir()
            };
            foreach (string b in bad)
            {
                if (string.IsNullOrEmpty(b)) continue;
                if (string.Equals(exeDir, b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    DialogResult r = MessageBox.Show(this, L10N._("badir.msg"), L10N._("badir.title"),
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                    if (r != DialogResult.OK) { Close(); return; }
                    LogLine(L10N._("badir.msg").Replace("\n", " "));
                    return;
                }
            }
        }
        catch { }
    }

    void RefreshStatus()
    {
        if (Interlocked.CompareExchange(ref refreshing, 1, 0) != 0) return;   // 防重入
        string core = CoreExePath();
        if (core == null)
        {
            Interlocked.Exchange(ref refreshing, 0);
            if (!coreWarnLogged)
            {
                coreWarnLogged = true;   // 只提示一次，避免每 3 秒刷屏
                if (!HasEmbeddedCore()) LogLine(L10N._("op.coremissing") + " — " + L10N._("home.status.nocore.tip"));
            }
            SetStatus(SKind.Unknown);
            return;
        }
        ThreadPool.QueueUserWorkItem(delegate(object _)
        {
            try
            {
                CoreRunResult r = RunCoreCapture(core, "status --detail", 10000);
                string k = (r.MarkLine ?? "").Trim();
                SKind st = SKind.Unknown;
                if (k == "STATUS_UP") st = SKind.Up;
                else if (k == "STATUS_STARTING") st = SKind.Starting;
                else if (k == "STATUS_DOWN") st = SKind.Down;
                // v2.7：--detail 追加的 PID / 运行时长（只读，状态栏用）
                string pid = "", up = "";
                foreach (string ln in (r.All ?? "").Split('\n'))
                {
                    string t = ln.Trim();
                    if (t.StartsWith("STATUS_PID ")) { string v = t.Substring(11).Trim(); if (v != "0") pid = v; }
                    else if (t.StartsWith("STATUS_UPTIME ")) up = t.Substring(14).Trim();
                }
                // v2.7：dsh 版本改为缓存读取——每 10 次轮询或操作后（verDirty）才真的 spawn 一次 cmd
                pollTicks++;
                bool wantVer = verDirty || (pollTicks % 10 == 0);
                string ver = wantVer ? ReadDshVersion() : null;   // 轮询顺带读 dsh 版本
                if (wantVer) verDirty = false;
                string verArg = ver;
                BeginInvoke((Action)delegate { SetStatus(st); SetStatusBarData(pid, up); if (wantVer) SetDshVersion(verArg); });
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
        UpdateStatusBar();
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
        UpdateInstallButtonLabel();   // v2.7：装/未装 → 安装 dsh / 修复 dsh
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

    // ================= v2.7：托盘 / 关闭行为 / 状态栏 / 快捷键 / Toast =================

    /// <summary>托盘图标 + 右键菜单（显示窗口 / 启停 dsh / 退出）。失败静默：没有托盘也必须能用。</summary>
    void InitTray()
    {
        try
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add(L10N._("tray.show"), null, delegate(object s, EventArgs e) { RestoreWindow(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(L10N._("tray.start"), null, delegate(object s, EventArgs e) { RestoreWindow(); OnAction("act.start"); });
            trayMenu.Items.Add(L10N._("tray.stop"), null, delegate(object s, EventArgs e) { RestoreWindow(); OnAction("act.stop"); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(L10N._("tray.exit"), null, delegate(object s, EventArgs e) { forceExit = true; Close(); });

            trayIcon = new NotifyIcon();
            Icon ico = null;
            try { ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { ico = null; }
            trayIcon.Icon = (ico != null) ? ico : SystemIcons.Application;
            trayIcon.Text = "DeepSeek Harness Toolkit " + AssemblyVersion();   // 托盘提示上限 63 字符
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.DoubleClick += delegate(object s, EventArgs e) { RestoreWindow(); };
            trayIcon.Visible = true;
        }
        catch { trayIcon = null; }   // 托盘不可用：关闭行为退化为"直接退出"，不影响其他功能
    }

    /// <summary>从托盘恢复窗口。</summary>
    void RestoreWindow()
    {
        try
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
            SetPollInterval(POLL_VISIBLE);
        }
        catch { }
    }

    /// <summary>最小化到托盘（窗口 Hide；进程与 dsh 服务继续运行）。</summary>
    void HideToTray()
    {
        try
        {
            Hide();
            SetPollInterval(POLL_HIDDEN);   // 看不见时降频轮询
            if (!trayTipShown && trayIcon != null)
            {
                trayTipShown = true;
                try { trayIcon.ShowBalloonTip(3000, L10N._("tray.balloon.title"), L10N._("tray.balloon.body"), ToolTipIcon.Info); } catch { }
            }
        }
        catch { }
    }

    /// <summary>托盘气泡提示（操作结果反馈）。托盘不可用时静默——日志里已经有同样的结果行。</summary>
    void ShowToast(string msg)
    {
        try { if (trayIcon != null) trayIcon.ShowBalloonTip(3000, L10N._("app.title"), msg, ToolTipIcon.Info); } catch { }
    }

    void SetPollInterval(int ms)
    {
        try { if (pollTimer != null && pollTimer.Interval != ms) pollTimer.Interval = ms; } catch { }
    }

    /// <summary>关闭窗口行为（v2.7）：未记忆 → 询问并记住；"ask" → 每次都问；"tray"/"exit" → 直接执行。
    /// 非用户关闭（系统关机/注销）一律放行，避免拖住关机。</summary>
    void OnAppClosing(FormClosingEventArgs e)
    {
        if (forceExit || e.CloseReason != CloseReason.UserClosing) return;
        string act = cfgClose;
        if (act.Length == 0 || act == "ask")
        {
            string picked = AskCloseAction();
            if (picked == null) { e.Cancel = true; return; }                 // 取消 → 不关窗
            act = picked;
            if (cfgClose.Length == 0) { cfgClose = picked; SaveCloseAction(picked); }   // 只有"首次"才记忆
        }
        if (act == "tray" && trayIcon != null) { e.Cancel = true; HideToTray(); return; }
        forceExit = true;   // exit（或托盘不可用）→ 真正退出
    }

    /// <summary>首次关闭时的询问框：最小化到托盘 / 直接退出；关掉对话框=取消（窗口留着）。</summary>
    string AskCloseAction()
    {
        try
        {
            using (Form dlg = new Form())
            {
                dlg.Text = L10N._("close.title");
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MinimizeBox = false;
                dlg.MaximizeBox = false;
                dlg.ShowInTaskbar = false;
                dlg.ClientSize = new Size(420, 132);
                dlg.BackColor = Th.Panel;
                dlg.ForeColor = Th.Fg;

                Label lbl = new Label();
                lbl.Text = L10N._("close.body");
                lbl.Location = new Point(18, 16);
                lbl.Size = new Size(384, 56);
                lbl.ForeColor = Th.Fg;
                dlg.Controls.Add(lbl);

                Button bTray = new Button();
                bTray.Text = L10N._("close.tray");
                bTray.Location = new Point(94, 86);
                bTray.Size = new Size(150, 30);
                bTray.DialogResult = DialogResult.Yes;
                dlg.Controls.Add(bTray);

                Button bExit = new Button();
                bExit.Text = L10N._("close.exit");
                bExit.Location = new Point(256, 86);
                bExit.Size = new Size(150, 30);
                bExit.DialogResult = DialogResult.No;
                dlg.Controls.Add(bExit);

                DialogResult r = dlg.ShowDialog(this);
                if (r == DialogResult.Yes) return "tray";
                if (r == DialogResult.No) return "exit";
                return null;   // 对话框被关掉 = 取消
            }
        }
        catch { return "exit"; }   // 弹窗本身失败时不要卡住用户：按退出处理
    }

    /// <summary>把关闭行为写回配置（后台线程，不阻塞关窗）。</summary>
    void SaveCloseAction(string act)
    {
        try
        {
            string core = CoreExePath();
            if (core == null) return;
            string a = act;
            ThreadPool.QueueUserWorkItem(delegate(object _) { try { RunCoreCapture(core, "config-set close_action " + a, 8000); } catch { } });
        }
        catch { }
    }

    /// <summary>启动时异步读回 close_action（不拖慢启动）。</summary>
    void LoadCloseAction()
    {
        try
        {
            string core = CoreExePath();
            if (core == null) return;
            ThreadPool.QueueUserWorkItem(delegate(object _)
            {
                try
                {
                    CoreRunResult r = RunCoreCapture(core, "config-get", 8000);
                    string all = r.All ?? "";
                    foreach (string ln in all.Split('\n'))
                    {
                        string t = ln.Trim();
                        if (t.StartsWith("CONFIG close_action "))
                        {
                            string v = t.Substring(20).Trim();
                            BeginInvoke((Action)delegate { cfgClose = v; });
                            break;
                        }
                    }
                }
                catch { }
            });
        }
        catch { }
    }

    /// <summary>状态栏数据（来自 status --detail，只读）。</summary>
    void SetStatusBarData(string pid, string uptime)
    {
        svcPid = pid ?? "";
        svcUptime = uptime ?? "";
        UpdateStatusBar();
    }

    /// <summary>状态栏文案：服务状态 · PID · 运行时长 · 主题 · 语言 · 快捷键提示。</summary>
    void UpdateStatusBar()
    {
        if (lblStatusBar == null) return;
        string s = StatusText(currentKind);
        if (svcPid.Length > 0) s += "   ·   " + L10N._("stat.pid") + " " + svcPid;
        if (svcUptime.Length > 0) s += "   ·   " + L10N._("stat.uptime") + " " + svcUptime;
        s += "   ·   " + (dark ? L10N._("stat.theme.dark") : L10N._("stat.theme.light"));
        s += "   ·   " + L10N._("stat.lang") + " " + (L10N.IsZh ? "中文" : "EN");
        s += "   ·   Ctrl+1~7  F5  Ctrl+B";
        lblStatusBar.Text = s;
    }

    /// <summary>安装按钮文案：检测到已装 dsh → "修复 dsh"，未装 → "安装 dsh"（v2.7）。</summary>
    void UpdateInstallButtonLabel()
    {
        try
        {
            Button b;
            if (actBtns == null || !actBtns.TryGetValue("act.install", out b)) return;
            b.Text = string.IsNullOrEmpty(dshVer) ? L10N._("act.install") : L10N._("act.repair");
        }
        catch { }
    }

    /// <summary>深度优先找当前焦点控件（判断是否在文本输入框里）。</summary>
    Control FocusedCtl(Control root)
    {
        try
        {
            if (root == null) return null;
            if (root.Focused) return root;
            foreach (Control c in root.Controls)
            {
                Control f = FocusedCtl(c);
                if (f != null) return f;
            }
        }
        catch { }
        return null;
    }

    /// <summary>焦点在文本输入控件上时，快捷键让位（输入框里按 Ctrl+B 不该触发备份）。</summary>
    bool IsTextInputFocused()
    {
        Control f = FocusedCtl(this);
        if (f == null) return false;
        return (f is TextBox) || (f is RichTextBox) || (f is NumericUpDown) || (f is MaskedTextBox);
    }

    /// <summary>快捷键（v2.7）：Ctrl+1~7 切页、F5 刷新状态、Ctrl+B 备份。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || IsTextInputFocused()) return;
        if (e.Control && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D7) { ShowPage(e.KeyCode - Keys.D1); e.Handled = true; return; }
        if (e.Control && e.KeyCode >= Keys.NumPad1 && e.KeyCode <= Keys.NumPad7) { ShowPage(e.KeyCode - Keys.NumPad1); e.Handled = true; return; }
        if (e.KeyCode == Keys.F5) { RefreshStatus(); e.Handled = true; return; }
        if (e.Control && e.KeyCode == Keys.B) { OnAction("act.backup"); e.Handled = true; return; }
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
    void LogLine(string s) { LogAdd("INFO", s); }
    void LogWarn(string s) { LogAdd("WARN", s); }
    void LogErr(string s) { LogAdd("ERROR", s); }

    void LogAdd(string level, string s)
    {
        string[] e = new string[] { DateTime.Now.ToString("HH:mm:ss"), level, s };
        if (txtLog == null) { pendingLog.Add(e); return; }   // 日志页尚未构建 → 暂存，BuildLog 时补写
        logEntries.Add(e);
        Action act = delegate { RenderLog(); };
        if (txtLog.InvokeRequired) txtLog.BeginInvoke(act);
        else act();
    }

    /// <summary>v2.9 Log Center：按级别筛选 + 搜索重渲染日志区。</summary>
    void RenderLog()
    {
        if (txtLog == null) return;
        var sb = new System.Text.StringBuilder();
        int shown = 0;
        foreach (string[] e in logEntries)
        {
            if (curLogLevel != "ALL" && e[1] != curLogLevel) continue;
            if (logQuery.Length > 0 && e[2].IndexOf(logQuery, StringComparison.OrdinalIgnoreCase) < 0) continue;
            sb.Append(e[0]).Append("  [").Append(e[1]).Append("] ").AppendLine(e[2]);
            shown++;
        }
        txtLog.Text = shown == 0 ? L10N._("log.empty") : sb.ToString();
        txtLog.SelectionStart = txtLog.TextLength;
        txtLog.ScrollToCaret();
    }

    void SetLogLevel(string lvl)
    {
        curLogLevel = lvl;
        if (btnLogAll != null) btnLogAll.Checked = lvl == "ALL";
        if (btnLogInfo != null) btnLogInfo.Checked = lvl == "INFO";
        if (btnLogWarn != null) btnLogWarn.Checked = lvl == "WARN";
        if (btnLogErr != null) btnLogErr.Checked = lvl == "ERROR";
        RenderLog();
    }

    RButton AddLogFilterBtn(Panel parent, ref int x, string tag, string level)
    {
        RButton b = new RButton();
        b.Size = new Size(52, 24);
        b.Location = new Point(x, 48);
        b.Text = L10N._(tag);
        b.Tag = tag;
        string lv = level;
        b.Click += delegate(object s, EventArgs e) { SetLogLevel(lv); };
        if (parent != null) parent.Controls.Add(b);
        x += 60;
        return b;
    }

    void ExportLog()
    {
        using (var dlg = new SaveFileDialog())
        {
            dlg.Title = L10N._("log.export");
            dlg.FileName = "dsh-toolkit-log-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
            dlg.Filter = "Text (*.txt)|*.txt";
            dlg.DefaultExt = "txt";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var sb = new System.Text.StringBuilder();
                foreach (string[] e in logEntries) sb.Append(e[0]).Append("  [").Append(e[1]).Append("] ").AppendLine(e[2]);
                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                LogLine(L10N._("log.export") + " → " + dlg.FileName);
            }
            catch (Exception ex) { LogWarn(L10N._("log.export") + ": " + ex.Message); }
        }
    }

    void CopyLog()
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            foreach (string[] e in logEntries) sb.Append(e[0]).Append("  [").Append(e[1]).Append("] ").AppendLine(e[2]);
            Clipboard.SetText(sb.ToString());
            LogLine(L10N._("log.copy") + " → ok");
        }
        catch (Exception ex) { LogWarn(L10N._("log.copy") + ": " + ex.Message); }
    }

    void ClearLog()
    {
        logEntries.Clear();
        RenderLog();
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
