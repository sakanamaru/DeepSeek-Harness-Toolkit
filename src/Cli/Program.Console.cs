using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

partial class Program
{

    /// <summary>倒计时等待输入；超时返回默认值（单键选择，无需回车）。</summary>
    static string CountdownInput(string prompt, string defaultChoice)
    {
        bool redirected = false;
        try { redirected = Console.IsInputRedirected; } catch { redirected = true; }
        for (int left = AUTO_SECONDS; left > 0; left--)
        {
            Console.Write("\r  " + prompt);
            C(ConsoleColor.Yellow, string.Format(T("[{0} 秒后自动: {1}]", "[auto in {0}s: {1}]"), left, defaultChoice));
            Console.Write("   ");
            bool key = false;
            try { key = Console.KeyAvailable; } catch { key = false; }
            if (key && !redirected)
            {
                var k = Console.ReadKey(true);
                Console.WriteLine();
                return k.KeyChar.ToString();
            }
            Thread.Sleep(1000);
        }
        Console.WriteLine();
        return defaultChoice;
    }



    /// <summary>阻塞读取单键选择（供自动倒计时之后的菜单页使用，不会自动执行）。</summary>
    static string ReadChoice(string prompt)
    {
        Console.Write(prompt);
        try
        {
            var k = Console.ReadKey(true);
            Console.WriteLine();
            return k.KeyChar.ToString();
        }
        catch { inputEof = true; Console.WriteLine(); Thread.Sleep(2000); return ""; }
    }

    // ---------------- 安装 ----------------



    static string WebUrl() { return "http://" + webHost + ":" + WEB_PORT; }
    static void OpenBrowser() { OpenUrl(WebUrl()); }



    static void OpenUrl(string url)
    {
        try { Process.Start(url); }
        catch (Exception ex) { Warn(T("打开失败：" + ex.Message + "（可手动访问 " + url + "）",
                                      "Failed to open: " + ex.Message + " (visit " + url + " manually).")); }
    }



    /// <summary>启动后的实时运行状态监控页：每 3 秒刷新，按任意键返回菜单。</summary>
    static void StatusMonitor()
    {
        string node = RunCapture("node.exe", "--version");
        string dver = RunDshVersion();
        DateTime? upSince = null;
        bool wasUp = false;
        while (true)
        {
            SafeClear();
            Banner();
            CL(ConsoleColor.White, "  " + T("▍ 运行状态监控", "▍ Runtime Status Monitor"));
            Console.WriteLine();
            ServiceState st = ProbeService();
            bool up = st == ServiceState.Ready;
            if (up && upSince == null) upSince = DateTime.Now;
            if (!up) upSince = null;
            C(ConsoleColor.Gray, "  Web 服务  : ");
            if (st == ServiceState.Ready) CL(ConsoleColor.Green, T("● 运行中", "● RUNNING"));
            else if (st == ServiceState.Listening) CL(ConsoleColor.Yellow, T("● 启动中（端口已开，服务未就绪）", "● STARTING (port open, not ready)"));
            else CL(ConsoleColor.Red, T("● 已停止", "● STOPPED"));
            C(ConsoleColor.Gray, "  地址      : "); CL(ConsoleColor.White, WebUrl());
            C(ConsoleColor.Gray, "  运行时长  : ");
            CL(up && upSince != null ? ConsoleColor.Green : ConsoleColor.Gray,
               up && upSince != null ? (DateTime.Now - upSince.Value).ToString(@"hh\:mm\:ss") : "-");
            C(ConsoleColor.Gray, "  dsh 版本  : "); CL(ConsoleColor.White, string.IsNullOrWhiteSpace(dver) ? "-" : dver);
            C(ConsoleColor.Gray, "  Node.js   : "); CL(ConsoleColor.White, string.IsNullOrWhiteSpace(node) ? "-" : node);
            C(ConsoleColor.Gray, "  最近刷新  : "); CL(ConsoleColor.DarkGray, DateTime.Now.ToString("HH:mm:ss"));
            if (!up && wasUp)
                Error(T("服务在运行中停止了！", "The service stopped while running!"));
            if (!up)
                Warn(T("可返回菜单按 2 重新启动，或查看 dsh web 窗口日志。",
                       "Back to menu and press 2 to restart, or check the dsh web window log."));
            wasUp = up;
            Console.WriteLine();
            Console.WriteLine();
            C(ConsoleColor.White, "  1) " + T("返回菜单", "Back to menu"));
            Console.WriteLine();
            C(ConsoleColor.White, "  2) " + T("打开 WebUI", "Open Web UI"));
            if (!ShortcutExists())
            {
                Console.WriteLine();
                C(ConsoleColor.White, "  I) " + T("创建桌面快捷方式", "Create desktop shortcut"));
            }
            Console.WriteLine();
            Console.WriteLine();
            bool redir = true;
            try { redir = Console.IsInputRedirected; } catch { redir = true; }
            if (redir) return;   // v2.1：管道/重定向模式显示一轮即返回（供脚本/测试取状态），不空等
            string k = WaitKeyChar(3000);
            if (k == "1") return;            // 返回菜单
            if (k == "2") OpenBrowser();     // 快捷打开 WebUI，留在监控页
            if ((k == "i" || k == "I") && !ShortcutExists())
            {
                string serr = CreateDesktopShortcut(DesktopDir());
                Console.WriteLine();
                if (serr == null) Success(T("桌面快捷方式已创建：DeepSeek Harness Toolkit.lnk", "Desktop shortcut created: DeepSeek Harness Toolkit.lnk"));
                else Error(T("桌面快捷方式创建失败：" + serr, "Desktop shortcut creation failed: " + serr));
            }
            // 其他按键忽略，继续自动刷新
        }
    }



    /// <summary>等待最多 ms 毫秒；期间有按键立即返回键字符，超时返回 null。输入被重定向（无控制台）时按时间流逝。</summary>
    static string WaitKeyChar(int ms)
    {
        bool redirected = false;
        try { redirected = Console.IsInputRedirected; } catch { redirected = true; }
        if (redirected) { Thread.Sleep(ms); return null; }
        int waited = 0;
        while (waited < ms)
        {
            try
            {
                if (Console.KeyAvailable)
                {
                    var k = Console.ReadKey(true);
                    return k.KeyChar.ToString();
                }
            }
            catch { }
            Thread.Sleep(100);
            waited += 100;
        }
        return null;
    }

    // ---------------- 根目录标记（防误删验证） ----------------



    /// <summary>清除数据的两步确认：第 1 步输入当天日期（yyyyMMdd），第 2 步输入 yes。</summary>
    static bool TwoStepConfirm()
    {
        string today = DateTime.Now.ToString("yyyyMMdd");
        C(ConsoleColor.Red, T("  （第 1/2 步）请输入今天日期以确认（格式 yyyyMMdd，例如 " + today + "）：",
                              "  (Step 1/2) Type today's date to confirm (yyyyMMdd, e.g. " + today + "): "));
        string d = ReadLineTrim();
        if (d != today) { Warn(T("日期不符，已取消。", "Date mismatch. Cancelled.")); return false; }
        C(ConsoleColor.Red, T("  （第 2/2 步）输入 yes 确认卸载：", "  (Step 2/2) Type yes to confirm the wipe: "));
        if (ReadLineTrim() != "yes") { Warn(T("未输入 yes，已取消。", "Not confirmed. Cancelled.")); return false; }
        return true;
    }

    // ---------------- 备份 / 恢复 ----------------


}
