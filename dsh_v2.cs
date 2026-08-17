// ============================================================================
//  DeepSeek Harness Toolkit V2.1.0  ——  DeepSeek Harness(dsh) 安装 / 启动 / 卸载 / 备份恢复工具箱
// ----------------------------------------------------------------------------
//  v1 脚本协助：SOGR-Momono Dango（QwenPaw/DeepseekAPI-V4-Flash-0731）
//  v2 重构封装：DeepSeek DSH（DSH/DeepseekAPI-V4-Flash-0731）
//
//  功能：安装/修复、启动 Web 界面、运行状态监控、卸载（含两步确认清数据）、
//        数据备份/恢复、多语言、自动倒计时选择、彩色输出。
//
//  编译： csc.exe /nologo /optimize+ /target:exe /win32icon:icon.ico /out:"DeepSeek Harness Toolkit.exe" dsh_v2.cs /warn:4
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

[assembly: AssemblyTitle("DeepSeek Harness Toolkit V2.1.0")]
[assembly: AssemblyDescription("DeepSeek Harness(dsh) 安装/启动/卸载/备份恢复工具箱。v1: SOGR-Momono Dango(QwenPaw/DeepseekAPI-V4-Flash-0731)；v2: DeepSeek DSH(DSH/DeepseekAPI-V4-Flash-0731)；GitHub @sakanamaru")]
[assembly: AssemblyCompany("SOGR-Momono Dango / DeepSeek DSH / @sakanamaru")]
[assembly: AssemblyProduct("DeepSeek Harness Toolkit")]
[assembly: AssemblyVersion("2.1.0.0")]
[assembly: AssemblyFileVersion("2.1.0.0")]

public static class Program
{
    enum Lang { Auto, Zh, En }

    const string NPM_MIRROR   = "https://registry.npmmirror.com";
    const string NPM_OFFICIAL = "https://registry.npmjs.org";
    const string GITHUB_HANDLE = "github.com/sakanamaru";
    const string DATA_DIR     = ".dsh";
    const string ROOT_MARKER  = ".dsh_launcher_root";   // 工具箱根目录标记文件（防误删验证；随包分发）
    const int    WEB_PORT     = 3080;
    static string webHost = "127.0.0.1";
    static string cfgWs = null;          // 手动指定的工作区路径（配置 ws=，空=自动探测）   // 访问入口：127.0.0.1 / localhost（浏览器缓存异常时可切换）
    static int cfgKeep = 10;             // 备份保留策略：自动类备份最多保留份数（配置 keep_backups=，最小 3）
    static bool cfgCheckUpdate = true;   // 启动时静默检查更新（配置 check_update=off 关闭）
    const int    AUTO_SECONDS = 5;

    static Lang lang = Lang.Auto;
    static string StateDir;
    static bool autoApplied = false;   // 自动倒计时是否已在本程序本次运行中用过
    static Mutex _singleMutex;         // 单例防多开：交互模式同一时刻只允许一个实例
    static Mutex _legacyMutex;         // 旧 v2.0 锁（已发布的 v2.0 exe 使用该名，保证新旧版本互斥）

    // ---------------- 入口 ----------------

#if !UNIT
    public static void Main(string[] args)
    {
        // .NET Framework 长路径支持：开启后 >260 字符路径可用（须在首次文件操作前设置）
        try { AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false); } catch { }
        try { AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false); } catch { }
        try { Console.OutputEncoding = new UTF8Encoding(false); } catch { }
        try { Console.Title = "DeepSeek Harness Toolkit V2.1.0"; } catch { }
        StateDir = ResolveStateDir();
        EnsureRootMarker();   // 完整安装 → 静默补标记（新包自带标记，此行主要兼容旧版本目录）
        LoadConfig();
        if (args.Length > 0)
        {
            switch (args[0].TrimStart('-', '/').ToLowerInvariant())
            {
                case "install":   case "i": Install(); return;
                case "start":     case "s": Start();   return;
                case "uninstall": case "u": Uninstall(); return;
                case "check":     case "c": Check();   return;
                case "about":     case "a": About();   return;
                case "help":      case "h": Help();    return;
                case "selftest": Selftest(args); return;
                default:
                    Console.WriteLine(T("未知参数：", "Unknown argument: ") + args[0]);
                    Help();
                    return;
            }
        }
        // 单例防多开：交互模式检测已有实例则提示退出（CLI 子命令不受限制，便于脚本/自检调用）
        // v2.1.0：同时持有产品级新锁 + v2.0 旧锁——与已发布的 v2.0 exe（旧锁名）双向互斥，
        // 同时保证 v2.1 及未来版本之间互斥（新锁）
        _singleMutex = new Mutex(false, "DeepSeek-Harness-Toolkit-single");   // 产品级固定单实例锁
        _legacyMutex = new Mutex(false, "DSH-Toolkit-V2.0.0-single");         // 旧锁名：与已发布的 v2.0 exe 互斥
        bool haveLock;
        try { haveLock = _singleMutex.WaitOne(0); }
        catch (AbandonedMutexException) { haveLock = true; } // 上一实例异常退出，本实例接管
        bool haveLegacy = true;
        try { haveLegacy = _legacyMutex.WaitOne(0); }
        catch (AbandonedMutexException) { haveLegacy = true; }
        if (!haveLock || !haveLegacy)
        {
            if (haveLock) { try { _singleMutex.ReleaseMutex(); } catch { } }    // 只释放自己已拿到的
            if (haveLegacy) { try { _legacyMutex.ReleaseMutex(); } catch { } }
            Info(T("检测到程序已在运行（含旧版本 v2.0），请切换到已打开的窗口（本实例自动退出）。",
                   "The launcher is already running (incl. v2.0) — switch to the open window (this instance exits)."));
            return;
        }
        Menu();
    }
#endif

    // ---------------- 语言 ----------------

    static bool IsZh
    {
        get
        {
            if (lang == Lang.Zh) return true;
            if (lang == Lang.En) return false;
            try { return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"; }
            catch { LogErr("IsZh: 读取系统语言异常，默认中文"); return true; }
        }
    }

    static string T(string zh, string en) { return IsZh ? zh : en; }

    // ---------------- 彩色输出 ----------------

    static void C(ConsoleColor color, string s)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(s);
        Console.ForegroundColor = prev;
    }

    static void CL(ConsoleColor color, string s) { C(color, s + Environment.NewLine); }
    static void SafeClear() { try { Console.Clear(); } catch { } }
    static void Info(string s)    { C(ConsoleColor.Cyan, "  " + s + Environment.NewLine); }
    static void Success(string s) { C(ConsoleColor.Green, "  ✓ " + s + Environment.NewLine); }
    static void Warn(string s)    { C(ConsoleColor.Yellow, "  [!] " + s + Environment.NewLine); }
    static void Error(string s)   { C(ConsoleColor.Red, "  [x] " + s + Environment.NewLine); LogErr(s); }

    // ---------------- 错误日志 ----------------

    /// <summary>把错误追加写入 StateDir\logs\launcher.log（带时间戳；超过 1MB 归档为 launcher.log.1，不再直接丢弃）。</summary>
    static void LogErr(string msg)
    {
        try
        {
            string dir = Path.Combine(StateDir, "logs");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "launcher.log");
            RotateLogIfNeeded(file, 1024 * 1024);
            File.AppendAllText(file, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + msg + Environment.NewLine, new UTF8Encoding(false));
        }
        catch { }
    }

    /// <summary>日志轮转：现有文件超过 maxBytes 时归档为 file+".1"（覆盖旧归档）并留下新的空文件，返回是否发生轮转。（单测可直接调用）</summary>
    static bool RotateLogIfNeeded(string file, long maxBytes)
    {
        try
        {
            if (File.Exists(file) && new FileInfo(file).Length > maxBytes)
            {
                if (File.Exists(file + ".1")) File.Delete(file + ".1");
                File.Move(file, file + ".1");
                File.WriteAllText(file, "");   // 轮转后留下新的空日志
                return true;
            }
        }
        catch { }
        return false;
    }

    static void Banner()
    {
        CL(ConsoleColor.Cyan,   "==============================================");
        CL(ConsoleColor.Cyan,   "  DeepSeek Harness Toolkit V2.1.0");
        CL(ConsoleColor.Cyan,   "==============================================");
        C(ConsoleColor.Gray,    "  v1 脚本协助 : "); CL(ConsoleColor.White, "SOGR-Momono Dango（QwenPaw/DeepseekAPI-V4-Flash-0731）");
        C(ConsoleColor.Gray,    "  v2 重构封装 : "); CL(ConsoleColor.White, "DeepSeek DSH （DSH/DeepseekAPI-V4-Flash-0731）");
        C(ConsoleColor.Gray,    "  GitHub    : "); CL(ConsoleColor.White, "@sakanamaru  https://" + GITHUB_HANDLE);
        CL(ConsoleColor.DarkGray, "----------------------------------------------");
    }

    // ---------------- 菜单（5 秒倒计时自动选择） ----------------

    static void Menu()
    {
        bool updateChecked = !cfgCheckUpdate;   // v2.1：菜单首次显示后静默检查一次更新（失败/离线静默）
        // 打开即检测（仅启动时一次）：服务已就绪（端口+HTTP）→ 直接进入状态监控页（返回后进常规菜单，不再自动执行）
        // 用三态探测而非裸端口：外部程序占用 3080 只会显示"启动中"，不再误触发整个服务流程
        if (ProbeService() == ServiceState.Ready)
        {
            autoApplied = true;
            Start();
        }
        while (true)
        {
            SafeClear();
            Banner();
            if (!updateChecked)
            {
                updateChecked = true;
                string nu = LatestVersion();
                if (nu != null)
                    Info(T("发现新版本 v" + nu + "（当前 v" + CurrentVersion() + "）。前往 GitHub Releases 下载更新。",
                           "Update available: v" + nu + " (current v" + CurrentVersion() + "). Visit GitHub Releases to download."));
            }
            bool installed = LocateDsh() != null;
            string def = installed ? "2" : "1";
            Console.WriteLine();
            if (!autoApplied)
            {
                if (installed)
                    Info(T("检测到 dsh 已安装，5 秒后将自动【启动 Web 界面】（按任意键可手动选择）",
                           "dsh detected. Auto-running【Start Web UI】in 5s (press any key to choose manually)."));
                else
                    CL(ConsoleColor.Gray, T("  dsh 未安装：按 1 开始安装（默认官方源），其余操作可正常使用。",
                                            "  dsh not installed: press 1 to install (official registry default); other actions still work."));
            }
            Console.WriteLine();
            CL(ConsoleColor.White, "  1) " + T("安装 / 修复 dsh", "Install / Repair dsh"));
            CL(ConsoleColor.White, "  2) " + T("启动 Web 界面", "Start Web UI"));
            CL(ConsoleColor.White, "  3) " + T("关于 / 署名", "About / Credits"));
            CL(ConsoleColor.White, "  4) " + T("语言 / Language", "Language"));
            CL(ConsoleColor.White, "  5) " + T("备份 / 恢复", "Backup / Restore"));
            CL(ConsoleColor.White, "  6) " + T("卸载 dsh", "Uninstall dsh"));
            CL(ConsoleColor.White, "  7) " + T("访问入口 / Entry", "Entry Address"));
            CL(ConsoleColor.White, "  0) " + T("退出", "Exit"));
            Console.WriteLine();

            string choice = autoApplied ? ReadChoice("  > ") : (installed ? CountdownInput("  > ", def) : ReadChoice("  > "));
            autoApplied = true;   // 首次倒计时（含按键接管）后，本次运行不再自动执行
            SafeClear();
            switch (choice)
            {
                case "1": Install(); break;
                case "2": Start();    break;
                case "3": About();    break;
                case "4": ChangeLang(); break;
                case "5": BackupMenu(); break;
                case "6": Uninstall(); break;
                case "7": EntryMenu(); break;
                case "0":
                case "q":
                    CL(ConsoleColor.Gray, T("  再见~", "  Bye~"));
                    return;
                default:
                    Warn(T("无效输入，请重新选择。", "Invalid input, please choose again."));
                    if (inputEof) return;   // 输入流已结束（如管道测试/重定向），避免死循环
                    break;
            }
        }
    }

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

    static void Install()
    {
        Banner();
        CheckNode();
        Console.WriteLine();
        Info(T("[2/3] 开始安装 dsh（安装源仅对本次安装生效，不修改全局配置）...",
               "[2/3] Installing dsh (source applies to this install only)..."));
        Console.WriteLine();
        CL(ConsoleColor.White, T("  安装源：", "  Install source:"));
        CL(ConsoleColor.White, "  1) " + T("官方源 npmjs.org（默认，推荐）", "Official npmjs.org (default, recommended)"));
        CL(ConsoleColor.White, "  2) " + T("国内镜像 npmmirror（更快）", "China mirror npmmirror (faster)"));
        Console.Write("  > ");
        string srcSel = ReadLineTrim().Trim();
        string[] registries = srcSel == "2" ? new string[] { NPM_MIRROR, NPM_OFFICIAL } : new string[] { NPM_OFFICIAL, NPM_MIRROR };
        bool ok = false;
        for (int i = 0; i < registries.Length; i++)
        {
            Info(string.Format(T("第 {0}/{1} 次尝试，源：{2}", "Attempt {0}/{1}, registry: {2}"), i + 1, registries.Length, registries[i]));
            int code = RunVisible("cmd.exe", "/c npm install -g --registry=" + registries[i] + " @deepseek-ai/dsh");
            if (code == 0) { ok = true; break; }
            Warn(string.Format(T("失败（退出码 {0}）", "Failed (exit code {0})"), code));
        }
        if (!ok) { Error(T("安装失败。请检查网络后重试。", "Install failed. Check your network and retry.")); Pause(); return; }

        Console.WriteLine();
        Success(T("安装成功！正在验证...", "Installed! Verifying..."));
        string ver = RunDshVersion();
        Success(T("dsh 版本：" + (string.IsNullOrWhiteSpace(ver) ? T("？（请新开终端验证）", "? (verify in a new terminal)") : ver),
                  "dsh version: " + (string.IsNullOrWhiteSpace(ver) ? "? (verify in a new terminal)" : ver)));
        Console.WriteLine();
        Info(T("接下来：选择【2 启动 Web 界面】即可打开浏览器。", "Next: choose【Start Web UI】to open the browser."));
        Pause();
    }

    static void CheckNode()
    {
        string v = RunCapture("node.exe", "--version");
        if (string.IsNullOrWhiteSpace(v))
        {
            Warn(T("未检测到 Node.js，尝试用 winget 自动安装...", "Node.js not found. Trying winget..."));
            int c = RunVisible("winget.exe", "install --id OpenJS.NodeJS.LTS --accept-source-agreements --accept-package-agreements");
            if (c != 0)
            {
                Error(T("winget 安装 Node 失败。请手动安装：https://nodejs.org 后重试。",
                        "winget failed. Please install Node.js LTS from https://nodejs.org and retry."));
                Pause();
                Environment.Exit(1);
            }
            Info(T("Node.js 安装完成，请关闭窗口后重新运行本程序。", "Node.js installed. Close this window and re-run."));
            Pause();
            Environment.Exit(0);
        }
        Success(T("Node.js " + v, "Node.js " + v));
        int major = 0;
        if (v.StartsWith("v")) int.TryParse(v.Substring(1).Split('.')[0], out major);
        if (major > 0 && major < 16)
            Warn(string.Format(T("Node 版本偏低（v{0}），如异常请升级到 LTS", "Node v{0} is old; upgrade to LTS if issues occur"), major));
    }

    // ---------------- 启动 ----------------

    static void Start()
    {
        Banner();
        string dsh = LocateDsh();
        if (dsh == null)
        {
            Error(T("未找到 dsh。请先选择【1 安装 / 修复 dsh】。", "dsh not found. Choose【Install / Repair dsh】first."));
            Pause();
            return;
        }
        if (ProbeService() != ServiceState.Down)   // 服务在跑（含启动中）→ 不重复启动
        {
            Info(T("检测到服务已在运行。", "Service already running."));
        }
        else
        {
            Info(T("启动 dsh web（服务器窗口请保持开启）...", "Starting dsh web (keep the server window open)..."));
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/k \"" + dsh + "\" web")   // 用 LocateDsh 完整路径启动，不受 PATH/npm prefix 影响
                {
                    UseShellExecute = true,
                    WorkingDirectory = WorkspaceRoot() ?? AppDomain.CurrentDomain.BaseDirectory   // 与备份/恢复的工作区保持一致
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Error(T("启动失败：" + ex.Message, "Start failed: " + ex.Message));
                Pause();
                return;
            }
            Info(T("等待服务就绪（最长 60 秒）...", "Waiting for the service (up to 60s)..."));
            bool up = false;
            for (int i = 0; i < 60; i++)
            {
                if (ProbeService() == ServiceState.Ready) { up = true; break; }   // 端口 + HTTP 均就绪才算启动成功
                Thread.Sleep(1000);
            }
            if (up) Success(T("服务已就绪。", "Service is ready."));
            else Error(T("60 秒内未就绪。请查看 dsh web 窗口日志（端口占用或启动报错）。",
                         "Not ready in 60s. Check the dsh web window log (port in use or startup error)."));
        }
        if (ProbeService() == ServiceState.Ready) OpenBrowser();   // 服务就绪才打开浏览器
        StatusMonitor();   // 无论成败都进入状态监控页
    }

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
            Console.Write("      ");
            C(ConsoleColor.White, "  2) " + T("打开 WebUI", "Open Web UI"));
            Console.WriteLine();
            Console.WriteLine();
            bool redir = true;
            try { redir = Console.IsInputRedirected; } catch { redir = true; }
            if (redir) return;   // v2.1：管道/重定向模式显示一轮即返回（供脚本/测试取状态），不空等
            string k = WaitKeyChar(3000);
            if (k == "1") return;            // 返回菜单
            if (k == "2") OpenBrowser();     // 快捷打开 WebUI，留在监控页
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

    /// <summary>标记文件随安装包分发；此处仅在"目录看起来是完整安装（含配套文件）"时静默补建，兼容旧版本目录。
    /// 单独复制的 exe（无配套文件）永远不会自建标记，从而永久被删除类操作拒绝。</summary>
    static void EnsureRootMarker()
    {
        try
        {
            string p = Path.Combine(StateDir, ROOT_MARKER);
            if (File.Exists(p)) return;
            if (!LooksLikeFullInstall(StateDir)) return;
            File.WriteAllText(p, "DeepSeek Harness Toolkit V2.1.0" + Environment.NewLine);
            File.SetAttributes(p, FileAttributes.Hidden);
        }
        catch (Exception ex) { LogErr("创建根目录标记失败: " + ex.Message); }
    }

    /// <summary>目录是否包含足够多的配套文件，可视为完整安装（标记随包分发 + 兼容旧目录的迁移判断）。</summary>
    static bool LooksLikeFullInstall(string dir)
    {
        int n = 0;
        foreach (string f in new string[] { "dsh_v2.cs", "build_exe.cmd", "hashes.txt", "icon.ico", "README.md" })
            try { if (File.Exists(Path.Combine(dir, f))) n++; } catch { }
        return n >= 2;
    }

    // ---------------- 卸载 ----------------

    static void Uninstall()
    {
        Banner();
        C(ConsoleColor.Red, T("  即将卸载 dsh（本程序与 npm 全局包会被移除）。\n  默认【保留】数据目录（会话/设置/凭据）。\n", 
                             "  About to uninstall dsh (this program and the npm global package).\n  Data (sessions/settings/credentials) is KEPT by default.\n"));
        Console.Write(T("  确认卸载？输入 y 继续，其他任意键取消：", "  Confirm uninstall? Type y to continue, any other key to cancel: "));
        string confirm = ReadLineTrim();
        if (confirm != "y" && confirm != "Y") { Warn(T("已取消。", "Cancelled.")); return; }

        // 卸载前必须确认服务已停止：运行中卸载会导致文件占用、部分文件残留。运行中则阻止删除
        if (IsPortOpen(WEB_PORT, 600))
        {
            Error(T("检测到 dsh Web 服务正在运行（端口 " + WEB_PORT + "）。\n  为避免文件占用与数据损坏，请先关闭 dsh web 的黑色服务窗口，再重新执行卸载。",
                    "dsh Web service is running (port " + WEB_PORT + ").\n  Close the dsh web window first, then retry the uninstall."));
            Pause();
            return;
        }

        Info(T("执行 npm 卸载...", "Running npm uninstall..."));
        int code = RunVisible("cmd.exe", "/c npm uninstall -g @deepseek-ai/dsh");
        if (code != 0)
        {
            LogErr("npm uninstall 退出码 " + code);
            Warn(T("npm 卸载未成功（"+code+"）。可手动在终端运行：npm uninstall -g @deepseek-ai/dsh",
                   "npm uninstall failed ("+code+"). Run manually: npm uninstall -g @deepseek-ai/dsh"));
        }

        if (LocateDsh() != null)
            Warn(T("检测到 dsh 可能仍存在，可手动删除：" + Path.GetDirectoryName(LocateDsh()),
                   "dsh may still exist. You can manually remove: " + Path.GetDirectoryName(LocateDsh())));
        else
            Success(T("dsh 已卸载。", "dsh uninstalled."));

        Console.WriteLine();
        Console.Write(T("  是否同时【清除全部数据】（会话记录/设置/API 凭据）？\n  输入 y 继续，其他任意键保留数据：",
                        "  Also WIPE ALL DATA (sessions/settings/API credentials)?\n  Type y to continue, any other key keeps data: "));
        string wipeAsk = ReadLineTrim();
        if (wipeAsk != "y" && wipeAsk != "Y") { Info(T("数据已保留。", "Data kept.")); Pause(); return; }

        if (!TwoStepConfirm()) { Warn(T("已取消清除数据。", "Wipe cancelled.")); Pause(); return; }

        // 清除数据前必须确认服务已停止：运行中被占用的文件会导致递归删除失败（参数错误）
        if (IsPortOpen(WEB_PORT, 600))
        {
            Error(T("dsh web 仍在运行，文件被占用无法安全清除。\n  请先关闭 dsh web 的黑色服务窗口，再重新执行清除数据。",
                    "dsh web is still running; files are locked and cannot be wiped safely.\n  Close the dsh web window first, then retry the wipe."));
            Pause();
            return;
        }

        string dir = DataRoot();
        // 防误删验证：必须来自完整安装（随包分发的标记文件，或 ≥2 个配套文件）；单独复制的 exe 一律拒绝
        bool rootOk = File.Exists(Path.Combine(StateDir, ROOT_MARKER)) || LooksLikeFullInstall(StateDir);
        if (!rootOk)
        {
            Error(T("未检测到完整安装（缺少标记文件 " + ROOT_MARKER + " 且缺少配套文件）。\n  请从解压后的完整目录运行本程序；切勿将 exe 单独复制后执行清除。已拒绝删除。",
                    "This does not look like a full installation (no marker " + ROOT_MARKER + " and no companion files).\n  Run from the complete extracted folder; do not copy the exe alone. Deletion refused."));
            Pause();
            return;
        }
        // 双重验证 2/2：目标必须"看起来像 dsh 数据目录"才允许清除，杜绝路径错乱/误操作误删其他文件夹
        if (Directory.Exists(dir) && !LooksLikeDshData(dir))
        {
            Error(T("拒绝清除：" + dir + "\n  该目录不含 dsh 数据标记（settings.yaml / credentials.yaml / sessions 等），为防止误删已中止。",
                    "Refused to wipe: " + dir + "\n  No dsh data markers found (settings.yaml / credentials.yaml / sessions etc.); aborted to prevent accidental deletion."));
            Pause();
            return;
        }
        // 清除数据前自动备份到备份目录
        if (Directory.Exists(dir))
        {
            Info(T("清除前自动备份数据到备份目录...", "Auto-backing up data before wipe..."));
            string bk = DoBackup(dir, null, BackupKind.PreWipe);   // 清除数据前的自动安全备份
            if (bk != null) Success(T("已备份：" + bk, "Backup saved: " + bk));
            else
            {
                Error(T("清除前自动备份失败，已中止清除（请先手动备份或检查磁盘空间）。", "Pre-wipe backup failed; wipe aborted (back up manually or check disk space first)."));
                Pause();
                return;
            }
        }
        C(ConsoleColor.Red, T("  正在删除：" + dir + " ...", "  Deleting: " + dir + " ..."));
        Console.WriteLine();
        if (Directory.Exists(dir))
        {
            if (DeleteTreeRobust(dir)) Success(T("数据已清除。", "Data wiped."));
        }
        else Success(T("数据目录不存在（无需清除）。", "Data directory not found."));
        Pause();
    }

    /// <summary>稳健递归删除：先清只读属性，失败自动重试 3 次；仍失败则说明并列出占用文件。返回是否成功。</summary>
    static bool DeleteTreeRobust(string dir)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                ClearReadOnlyRecursive(dir);
                Directory.Delete(dir, true);
                return true;
            }
            catch (Exception ex)
            {
                if (attempt == 2)
                {
                    string locked = FindFirstLockedFile(dir);
                    Error(T("删除失败：" + ex.Message + (locked == null ? "" : "（占用文件：" + locked + "）"),
                            "Delete failed: " + ex.Message + (locked == null ? "" : " (locked file: " + locked + ")")));
                }
                Thread.Sleep(800);
            }
        }
        return false;
    }

    static void ClearReadOnlyRecursive(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (string f in Directory.GetFiles(dir))
            try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
        foreach (string d in Directory.GetDirectories(dir))
        {
            try { File.SetAttributes(d, FileAttributes.Normal); } catch { }
            ClearReadOnlyRecursive(d);
        }
    }

    static string FindFirstLockedFile(string dir)
    {
        try
        {
            foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    // 只读打开 + 允许共享：只有真正被独占的文件才报锁定，正常读取中的文件不再误报
                    using (var s = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { }
                }
                catch { return f; }
            }
        }
        catch { LogErr("FindLockedFile: 枚举目录异常"); }
        return null;
    }

    /// <summary>防呆校验：目录含任一 dsh 数据标记（文件或子目录）即视为 dsh 数据目录。</summary>
    static bool LooksLikeDshData(string dir)
    {
        foreach (string marker in new string[] { "settings.yaml", "credentials.yaml", "sessions", "profiles", "storages" })
        {
            string p = Path.Combine(dir, marker);
            if (File.Exists(p) || Directory.Exists(p)) return true;
        }
        return false;
    }

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

    static void BackupMenu()
    {
        while (true)
        {
            SafeClear();
            Banner();
            Console.WriteLine();
            CL(ConsoleColor.White, "  1) " + T("备份数据", "Backup data"));
            CL(ConsoleColor.White, "  2) " + T("恢复数据", "Restore data"));
            CL(ConsoleColor.White, "  3) " + T("导入备份（其他电脑）", "Import backup (other PC)"));
            CL(ConsoleColor.White, "  4) " + T("打开备份文件夹", "Open backup folder"));
            CL(ConsoleColor.White, "  0) " + T("返回", "Back"));
            Console.WriteLine();
            Console.Write("  > ");
            string k = ReadLineTrim();
            if (k == "1") BackupData();
            else if (k == "2") RestoreData();
            else if (k == "3") ImportBackup();
            else if (k == "4") OpenBackupFolder();
            else if (k == "0" || k == "q") return;
            else if (k == "" && inputEof) return;   // 输入流已结束（如管道测试），避免死循环
            else Warn(T("无效输入。", "Invalid input."));
        }
    }

    static string BackupsRoot() { return Path.Combine(StateDir, "backup"); }

    /// <summary>定位 dsh 数据目录：优先 用户主目录 ~/.dsh，其次 %APPDATA%/.dsh、%LOCALAPPDATA%/.dsh。</summary>
    static string DataRoot()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] candidates = {
            Path.Combine(home, DATA_DIR),
            Path.Combine(appdata, DATA_DIR),
            Path.Combine(local, DATA_DIR)
        };
        foreach (string c in candidates)
            if (Directory.Exists(c)) return c;
        return candidates[0];
    }

    static void BackupData()
    {
        string src = DataRoot();
        if (!Directory.Exists(src)) { Warn(T("未找到数据目录：" + src, "Data directory not found: " + src)); Pause(); return; }
        var wsList = new List<string>();
        string guess = WorkspaceRoot();
        if (guess != null && Directory.Exists(guess))
        {
            Console.Write(T("  检测到工作区（" + guess + "）。是否同时备份？输入 y 包含：",
                            "  Workspace detected (" + guess + "). Include it? Type y: "));
            string addAsk = ReadLineTrim();
            if (addAsk == "y" || addAsk == "Y") wsList.Add(guess);
        }
        // 多工作区：逐个输入路径，直接回车结束
        while (true)
        {
            Console.Write(T("  输入要附加备份的工作区路径（直接回车结束）：",
                            "  Workspace folder path to include (Enter to finish): "));
            string p = ReadLineTrim().Trim().Trim('"');
            if (p.Length == 0) break;
            string full = null;
            try { full = Path.GetFullPath(p); } catch { full = null; }
            if (full == null) { Warn(T("路径无效，请重新输入。", "Invalid path, try again.")); continue; }
            if (!Directory.Exists(full)) { Warn(T("目录不存在：" + p + "（直接回车可结束）。", "Not found: " + p + " (Enter to finish).")); continue; }
            bool dup = false, nested = false;
            foreach (string e in wsList)
            {
                string ee = e.TrimEnd('\\');
                string ff = full.TrimEnd('\\');
                if (string.Equals(ee, ff, StringComparison.OrdinalIgnoreCase)) { dup = true; break; }
                if (ff.StartsWith(ee + "\\", StringComparison.OrdinalIgnoreCase)) { nested = true; break; }
            }
            if (dup) Warn(T("该目录已在列表中，跳过：" + full, "Already in the list, skipped: " + full));
            else if (nested) Warn(T("该目录位于已选工作区之内，跳过：" + full, "Inside an already-selected workspace, skipped: " + full));
            else { wsList.Add(full); Success(T("已添加工作区：" + full, "Workspace added: " + full)); }
        }
        Info(T("正在备份（自动跳过 node_modules）...", "Backing up (skipping node_modules)..."));
        string bk = DoBackup(src, wsList, BackupKind.Manual);   // 用户主动备份（手动，永久保留）
        if (bk != null)
        {
            Success(T("备份完成：" + bk, "Backup done: " + bk));
            if (wsList.Count > 0) Info(T("已包含 " + wsList.Count + " 个工作区副本（备份包 _workspace 下）。", wsList.Count + " workspace(s) included (under _workspace)."));
        }
        else Error(T("备份失败。详情见 logs\\launcher.log", "Backup failed. See logs\\launcher.log"));
        Pause();
    }

    static void RestoreData()
    {
        string root = BackupsRoot();
        if (!Directory.Exists(root)) { Warn(T("没有找到任何备份。", "No backups found.")); Pause(); return; }
        string[] dirs = Directory.GetDirectories(root);
        Array.Sort(dirs);
        Array.Reverse(dirs);
        if (dirs.Length == 0) { Warn(T("备份目录为空，尚无任何备份。", "Backup folder is empty; no backups yet.")); Pause(); return; }
        Console.WriteLine();
        for (int i = 0; i < dirs.Length; i++)
            CL(ConsoleColor.White, "  " + (i + 1) + ") " + Path.GetFileName(dirs[i]));
        Console.WriteLine();
        Console.Write(T("  选择要恢复的备份序号（回车=最新）：", "  Choose backup number (Enter = latest): "));
        string sel = ReadLineTrim();
        int idx = 0;
        if (sel.Length > 0 && !int.TryParse(sel, out idx)) { Warn(T("输入无效。", "Invalid input.")); return; }
        if (idx < 1 || idx > dirs.Length) idx = 1;
        string bk = dirs[idx - 1];
        string dst = DataRoot();
        Console.Write(T("  恢复将覆盖当前数据（建议先关闭 dsh web）。确认？输入 y 继续：",
                        "  Restore overwrites current data (close dsh web first). Type y to continue: "));
        string restoreAsk = ReadLineTrim();
        if (restoreAsk != "y" && restoreAsk != "Y") { Warn(T("已取消。", "Cancelled.")); return; }
        // v2.1 安全：dsh 运行中拒绝恢复（与卸载/清除一致，防止覆盖正在使用的数据）
        if (ProbeService() != ServiceState.Down)
        {
            Error(T("dsh web 仍在运行，数据被占用无法安全恢复。\n  请先关闭 dsh web，再重新执行恢复。",
                    "dsh web is still running; data is in use and cannot be restored safely.\n  Close the dsh web window first, then retry the restore."));
            Pause();
            return;
        }
        if (Directory.Exists(dst))
        {
            Info(T("恢复前自动备份当前数据...", "Auto-backing up current data before restore..."));
            string preBk = DoBackup(dst, null, BackupKind.PreRestore);
            if (preBk == null)
            {
                Error(T("恢复前自动备份失败，已中止恢复（请先手动备份或检查磁盘空间）。", "Pre-restore backup failed; restore aborted (back up manually or check disk space first)."));
                Pause();
                return;
            }
        }
        RestoreFromSource(bk);
        Pause();
    }

    /// <summary>备份数据目录到备份目录（自动跳过 node_modules 与被锁文件），返回备份路径；失败返回 null。</summary>
    static string DoBackup(string source) { return DoBackup(source, null, BackupKind.Manual); }

    /// <summary>备份数据目录；wsList 非空时把每个工作区放入备份包 _workspace\<名字>\（含 .dshws 标记）；kind 决定目录名来源后缀，备份成功后自动执行保留策略清理。</summary>
    static string DoBackup(string source, List<string> wsList, BackupKind kind)
    {
        string dest = null;
        try
        {
            if (!Directory.Exists(source)) return null;
            string root = BackupsRoot();
            Directory.CreateDirectory(root);
            dest = Path.Combine(root, "dsh-data-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + BackupSuffix(kind));
            CopyTree(source, dest, true);
            if (wsList != null)
            {
                var used = new List<string>();
                foreach (string w in wsList)
                {
                    string name = SanitizeName(Path.GetFileName(Path.GetFullPath(w).TrimEnd('\\')));
                    if (name.Length == 0) name = "workspace";
                    string sub = name; int k = 2;
                    while (used.Contains(sub)) { sub = name + "_" + k; k++; }
                    used.Add(sub);
                    string wsDest = Path.Combine(dest, "_workspace", sub);
                    CopyTree(w, wsDest, true);
                    File.WriteAllText(Path.Combine(wsDest, ".dshws"),
                                      "DeepSeek Harness Toolkit workspace\n" + w + "\n",
                                      new UTF8Encoding(false));
                }
            }
            // v2.1 保留策略：备份成功后清理自动类旧备份（手动备份永久保留）
            var removed = EnforceBackupRetention();
            if (removed.Count > 0)
                Info(T("保留策略：已清理 " + removed.Count + " 个旧自动备份：" + string.Join(", ", removed.ToArray()),
                       "Retention: removed " + removed.Count + " old auto backup(s): " + string.Join(", ", removed.ToArray())));
            return dest;
        }
        catch (Exception ex)
        {
            LogErr("备份失败: " + ex);
            if (dest != null) { try { if (Directory.Exists(dest)) { ClearReadOnlyRecursive(dest); Directory.Delete(dest, true); } } catch { } }   // 清理半成品，避免残目录伪装成有效备份
            return null;
        }
    }

    // ---------------- 备份保留策略（v2.1：只清理自动类，手动永久保留） ----------------

    /// <summary>备份来源：Manual=用户主动备份（永久保留）；Auto/Pre*=系统自动产生（参与保留策略清理）。</summary>
    public enum BackupKind { Manual, Auto, PreRestore, PreImport, PreWipe }

    /// <summary>自动类备份目录名的来源后缀（手动无后缀，兼容旧版产物）。</summary>
    static string BackupSuffix(BackupKind k)
    {
        switch (k)
        {
            case BackupKind.Auto: return "-auto";
            case BackupKind.PreRestore: return "-pre-restore";
            case BackupKind.PreImport: return "-pre-import";
            case BackupKind.PreWipe: return "-pre-wipe";
            default: return "";
        }
    }

    /// <summary>备份目录名是否属于"自动类"（自动类参与保留策略清理；手动备份永久保留）。</summary>
    static bool IsAutoBackupName(string name)
    {
        return name.EndsWith("-auto") || name.EndsWith("-pre-restore") || name.EndsWith("-pre-import") || name.EndsWith("-pre-wipe");
    }

    /// <summary>保留策略：仅清理自动类备份（-auto / -pre-*），手动备份永久保留；自动类超过 cfgKeep 份时按最旧删除、保底 3 份。返回被清理的目录名列表（空=未清理）。</summary>
    static List<string> EnforceBackupRetention()
    {
        var removed = new List<string>();
        try
        {
            string root = BackupsRoot();
            if (!Directory.Exists(root)) return removed;
            var autos = new List<string>();
            foreach (string d in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(d);
                if (name.StartsWith("dsh-data-") && IsAutoBackupName(name)) autos.Add(d);
            }
            autos.Sort();   // 名字时间戳字典序 = 时间序，旧的在前面
            int keep = cfgKeep; if (keep < 3) keep = 3;
            for (int i = 0; i < autos.Count - keep; i++)
            {
                try { Directory.Delete(autos[i], true); removed.Add(Path.GetFileName(autos[i])); }
                catch (Exception ex) { LogErr("保留策略清理失败: " + autos[i] + " : " + ex.Message); }
            }
        }
        catch (Exception ex) { LogErr("保留策略执行异常: " + ex.Message); }
        return removed;
    }

    /// <summary>把字符串变成安全的文件夹名（去掉 Windows 非法字符）。</summary>
    static string SanitizeName(string s)
    {
        if (s == null) return "";
        char[] bad = new char[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
        foreach (char c in bad) s = s.Replace(c.ToString(), "_");
        return s.Trim().Trim('.');
    }

    static void CopyTree(string src, string dst) { CopyTree(src, dst, false); }

    static void CopyTree(string src, string dst, bool skipLocked)
    {
        src = src.TrimEnd('\\'); dst = dst.TrimEnd('\\');
        Directory.CreateDirectory(P(dst));
        foreach (string d in Directory.GetDirectories(P(src)))
        {
            string name = Path.GetFileName(TrimP(d));
            if (name == "node_modules") continue;        // 依赖可重装，备份时跳过
            if (name == "backup") continue;              // 防止备份目录把自身备份递归复制进去
            if (name.StartsWith("dsh-data-")) continue;  // 嵌套的备份包不再复制（防递归与超长路径）
            try { CopyTree(TrimP(d), Path.Combine(dst, name), skipLocked); }
            catch (Exception ex)
            {
                if (!skipLocked) throw;
                LogErr("跳过无法复制的子目录: " + TrimP(d) + " : " + ex.Message);
            }
        }
        foreach (string f in Directory.GetFiles(P(src)))
        {
            string fs = TrimP(f), fd = Path.Combine(dst, Path.GetFileName(f));
            try
            {
                using (var s = new FileStream(P(fs), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var t = new FileStream(P(fd), FileMode.Create, FileAccess.Write, FileShare.None))
                    s.CopyTo(t);
            }
            catch (Exception ex) { if (!skipLocked) throw; LogErr("备份: 跳过无法复制的文件 " + TrimP(f) + " : " + ex.Message); }   // 备份模式：被锁/坏文件跳过并记日志；恢复模式：如实报错
        }
    }

    /// <summary>加 \\?\ 前缀绕过 260 字符限制（配合 Main 里的长路径开关，兼容无注册表策略的机器）。</summary>
    static string P(string p)   // 统一转 \\?\ 前缀（UNC 走 \\?\UNC\，否则 System.IO 对长路径会失败）
    {
        if (string.IsNullOrEmpty(p)) return p;
        if (p.StartsWith(@"\\?\")) return p;
        if (p.StartsWith(@"\\")) return @"\\?\UNC\" + p.Substring(2);   // \\server\share → \\?\UNC\server\share
        return @"\\?\" + p;
    }

    static string TrimP(string p)   // \\?\ 前缀还原（UNC 还原为 \\server\share）
    {
        if (string.IsNullOrEmpty(p)) return p;
        if (p.StartsWith(@"\\?\UNC")) return @"\\" + p.Substring(8);
        if (p.StartsWith(@"\\?\")) return p.Substring(4);
        return p;
    }

    /// <summary>工作区：本程序所在目录的上两级（exe 在 …\DeepSeek Harness Toolkit\ 时，工作区为 …\）。
    /// 若探测结果落在用户主目录/桌面/Windows/盘根等明显不合理位置，返回 null（由调用方改为手动输入）。</summary>
    static string WorkspaceRoot()
    {
        if (cfgWs != null && cfgWs.Length > 0)
        {
            string c = null;
            try { c = Path.GetFullPath(cfgWs); } catch { c = null; }
            if (c != null && Directory.Exists(c)) return c;
            return null;   // 配置了但目录不存在：不再退回自动探测，避免误备份
        }
        string ws = null;
        try { ws = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..")); }
        catch { LogErr("WorkspaceRoot: 自动探测路径异常"); return null; }
        return LooksLikeWorkspace(ws) ? ws : null;
    }

    /// <summary>判定路径是否像"合理的用户工作区"。仅用于自动探测：系统级/用户级/常见奇怪目录一律拒绝；
    /// 手动输入的路径（备份附加、恢复目标、ws= 配置）不受本函数限制。</summary>
    static bool LooksLikeWorkspace(string p)
    {
        try
        {
            p = Path.GetFullPath(p).TrimEnd('\\');
            if (p.Length == 0) return false;
            if (p.Length <= 3 && p[1] == ':') return false;                 // 盘根：C:\ D:\
            string pc = p.ToLowerInvariant();
            // 各盘根下（或 UNC 根）的保留名字：只查第一段，避免误伤深层同名目录
            string[] topNames = { "$recycle.bin", "system volume information", "perflogs", "inetpub",
                                  "recovery", "windows.old", "$windows.~bt", "$windows.~ws", "$winreagent", "users" };
            string[] segs = p.Split('\\');
            foreach (string r in topNames)
                if (segs.Length > 1 && segs[1].ToLowerInvariant() == r) return false;
            // 系统/用户根目录及其子树
            string[] roots = {
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),          // 用户主目录（含桌面/下载/文档）
                Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)), // C:\Users 整级
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),              // C:\Windows
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),// C:\ProgramData
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),         // C:\Program Files
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)       // C:\Program Files (x86)
            };
            foreach (string r in roots)
            {
                if (string.IsNullOrEmpty(r)) continue;
                string rc = r.TrimEnd('\\').ToLowerInvariant();
                if (rc.Length == 0) continue;
                if (pc == rc || pc.StartsWith(rc + "\\")) return false;
            }
            return true;
        }
        catch { LogErr("LooksLikeWorkspace: 路径异常，按拒绝处理 " + p); return false; }
    }

    static void OpenBackupFolder()
    {
        try
        {
            Directory.CreateDirectory(BackupsRoot());
            Process.Start(BackupsRoot());
        }
        catch (Exception ex) { Error(T("打开失败：" + ex.Message, "Failed: " + ex.Message)); }
    }

    /// <summary>把任一备份目录（本机或从其他电脑复制来的）的数据/工作区恢复到当前位置。</summary>
    static void RestoreFromSource(string path)
    {
        string dst = DataRoot();
        bool hasWs = Directory.Exists(Path.Combine(path, "_workspace"));
        try
        {
            Directory.CreateDirectory(dst);
            Info(T("正在恢复数据...", "Restoring data..."));
            foreach (string d in Directory.GetDirectories(path))
                if (Path.GetFileName(d) != "_workspace")
                    CopyTree(d, Path.Combine(dst, Path.GetFileName(d)));
            foreach (string f in Directory.GetFiles(path))
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            if (hasWs) RestoreWorkspaces(Path.Combine(path, "_workspace"));
            Success(T("恢复完成。请重启 dsh web。", "Restore done. Restart dsh web."));
        }
        catch (Exception ex) { LogErr("恢复失败: " + ex); Error(T("恢复失败：" + ex.Message, "Restore failed: " + ex.Message)); }
    }

    /// <summary>恢复备份包里的工作区：新格式为 _workspace\<名字>\（含 .dshws 标记，多工作区）；旧格式为 _workspace 直接存放内容。</summary>
    static void RestoreWorkspaces(string wsRoot)
    {
        string[] subs = Directory.GetDirectories(wsRoot);
        bool anyNew = false;
        foreach (string s in subs)
            if (File.Exists(Path.Combine(s, ".dshws"))) { anyNew = true; break; }
        if (anyNew)
        {
            foreach (string s in subs)
            {
                if (!File.Exists(Path.Combine(s, ".dshws")))
                { Warn(T("无法识别的工作区条目，跳过：" + Path.GetFileName(s), "Unrecognized workspace entry, skipped: " + Path.GetFileName(s))); continue; }
                RestoreOneWorkspace(s, Path.GetFileName(s), true);
            }
        }
        else RestoreOneWorkspace(wsRoot, null, false);   // 旧格式：整个 _workspace 视为一个工作区
    }

    /// <summary>单个工作区恢复：目标默认取当前检测的工作区目录，可输入其他路径；输入 0 跳过。</summary>
    static void RestoreOneWorkspace(string srcDir, string label, bool isNewFormat)
    {
        string def = WorkspaceRoot();
        Console.WriteLine();
        if (label != null) CL(ConsoleColor.Gray, T("  工作区「" + label + "」", "  Workspace: " + label));
        C(ConsoleColor.Gray, T("  恢复目标（直接回车=" + (def ?? T("未检测到", "none")) + "，输入路径自定义，输入 0 跳过）：",
                               "  Restore target (Enter=" + (def ?? "none") + ", type a path, 0 to skip): "));
        string t = ReadLineTrim().Trim().Trim('"');
        if (t == "0") { Info(T("已跳过该工作区。", "Workspace skipped.")); return; }
        string target = null;
        if (t.Length > 0) { try { target = Path.GetFullPath(t); } catch { target = null; } }
        if (target == null) target = def;
        if (target == null || !Directory.Exists(target))
        {
            Warn(T("目标目录无效，已跳过该工作区。", "Invalid target, skipped."));
            return;
        }
        if (isNewFormat)
        {
            foreach (string d in Directory.GetDirectories(srcDir))
                CopyTree(d, Path.Combine(target, Path.GetFileName(d)));
            foreach (string f in Directory.GetFiles(srcDir))
                if (Path.GetFileName(f) != ".dshws")
                    File.Copy(f, Path.Combine(target, Path.GetFileName(f)), true);
        }
        else CopyTree(srcDir, target);
        Success(T("已恢复工作区到 " + target, "Workspace restored to " + target));
    }

    static void ImportBackup()
    {
        Console.WriteLine();
        C(ConsoleColor.Gray, T("  跨电脑导入：把备份文件夹（dsh-data-日期）从其他电脑复制到本机后，输入它的完整路径。",
                               "  Import: copy a backup folder (dsh-data-YYYYMMDD-HHMMSS) from another PC, then type its full path."));
        Console.WriteLine();
        Console.Write(T("  备份目录路径：", "  Backup directory path: "));
        string path = ReadLineTrim();
        if (path.Length == 0 || !Directory.Exists(path)) { Warn(T("目录不存在。", "Directory not found.")); Pause(); return; }
        bool hasWs = Directory.Exists(Path.Combine(path, "_workspace"));
        bool hasData = Directory.GetFiles(path).Length > 0 || Directory.GetDirectories(path).Length > (hasWs ? 1 : 0);
        Console.WriteLine();
        C(ConsoleColor.Gray, T("  导入内容：", "  Import contents:"));
        C(ConsoleColor.Gray, "    - " + T("dsh 数据", "dsh data") + " : "); CL(ConsoleColor.White, hasData ? T("有", "yes") : T("无", "no"));
        C(ConsoleColor.Gray, "    - " + T("工作区", "workspace") + " : "); CL(ConsoleColor.White, hasWs ? T("有", "yes") : T("无", "no"));
        if (!hasData && !hasWs) { Warn(T("该目录不是有效的备份。", "Not a valid backup directory.")); Pause(); return; }
        Console.Write(T("  确认导入？输入 y 继续：", "  Confirm import? Type y: "));
        string importAsk = ReadLineTrim();
        if (importAsk != "y" && importAsk != "Y") { Warn(T("已取消。", "Cancelled.")); return; }
        // v2.1 安全：dsh 运行中拒绝导入（与恢复/清除一致）
        if (ProbeService() != ServiceState.Down)
        {
            Error(T("dsh web 仍在运行，数据被占用无法安全导入。\n  请先关闭 dsh web，再重新执行导入。",
                    "dsh web is still running; data is in use and cannot be imported safely.\n  Close the dsh web window first, then retry the import."));
            Pause();
            return;
        }
        string dst = DataRoot();
        if (Directory.Exists(dst))
        {
            Info(T("导入前自动备份当前数据...", "Auto-backing up current data before import..."));
            string preBk = DoBackup(dst, null, BackupKind.PreImport);
            if (preBk == null)
            {
                Error(T("导入前自动备份失败，已中止导入（请先手动备份或检查磁盘空间）。", "Pre-import backup failed; import aborted (back up manually or check disk space first)."));
                Pause();
                return;
            }
        }
        RestoreFromSource(path);
        Pause();
    }

    // ---------------- 语言设置 ----------------

    static void ChangeLang()
    {
        SafeClear();
        Console.WriteLine();
        CL(ConsoleColor.White, "  " + T("语言 / Language", "Language / 语言"));
        CL(ConsoleColor.White, "  1) " + T("跟随系统（默认）", "Follow system (default)"));
        CL(ConsoleColor.White, "  2) 简体中文");
        CL(ConsoleColor.White, "  3) English");
        Console.Write("  > ");
        string k = ReadLineTrim();
        if (k == "1") lang = Lang.Auto;
        else if (k == "2") lang = Lang.Zh;
        else if (k == "3") lang = Lang.En;
        else { Warn(T("无效输入。", "Invalid input.")); return; }
        SaveConfig();
        Success(T("语言已更新。", "Language updated."));
    }

    // ---------------- 访问入口 ----------------

    static void EntryMenu()
    {
        SafeClear();
        Banner();
        Console.WriteLine();
        CL(ConsoleColor.White, "  " + T("访问入口（浏览器打开 WebUI 用的地址）", "Entry address (used to open WebUI in browser)"));
        C(ConsoleColor.Gray, T("  当前: ", "  Current: ")); CL(ConsoleColor.White, WebUrl());
        C(ConsoleColor.Gray, T("  工作区: ", "  Workspace: "));
        CL(ConsoleColor.White, cfgWs != null && cfgWs.Length > 0 ? cfgWs : T("（未设置，自动探测）", "(auto-detect)"));
        Console.WriteLine();
        C(ConsoleColor.Gray, T("  提示: 若 127.0.0.1 打开后异常（可能为浏览器残留旧缓存导致），", "  Tip: if 127.0.0.1 opens abnormally (possibly due to stale browser cache),"));
        CL(ConsoleColor.Gray, T("        切到 localhost 即可，两者在浏览器中视为不同站点。", "        switch to localhost - they are different sites in the browser."));
        Console.WriteLine();
        CL(ConsoleColor.White, "  1) 127.0.0.1");
        CL(ConsoleColor.White, "  2) localhost");
        CL(ConsoleColor.White, "  3) " + T("设置工作区路径", "Set workspace path"));
        CL(ConsoleColor.White, "  0) " + T("返回", "Back"));
        Console.Write("  > ");
        string k = ReadLineTrim();
        if (k == "1") { webHost = "127.0.0.1"; SaveConfig(); Success(T("入口已设为 127.0.0.1", "Entry set to 127.0.0.1")); }
        else if (k == "2") { webHost = "localhost"; SaveConfig(); Success(T("入口已设为 localhost", "Entry set to localhost")); }
        else if (k == "3") SetWorkspacePrompt();
        else if (k != "0" && k != "q") Warn(T("无效输入。", "Invalid input."));
    }

    /// <summary>设置（或清除）手动指定的工作区路径，持久化到 launcher.config 的 ws= 行。</summary>
    static void SetWorkspacePrompt()
    {
        Console.WriteLine();
        C(ConsoleColor.Gray, T("  当前工作区: ", "  Current workspace: "));
        CL(ConsoleColor.White, (cfgWs != null && cfgWs.Length > 0) ? cfgWs : T("（未设置）", "(none)"));
        Console.Write(T("  输入新工作区路径（直接回车清除自定义设置）：", "  New workspace path (Enter to clear): "));
        string p = ReadLineTrim().Trim().Trim('"');
        if (p.Length == 0)
        {
            cfgWs = null; SaveConfig();
            Info(T("已清除自定义工作区，恢复自动探测。", "Custom workspace cleared, auto-detect restored."));
            return;
        }
        string full = null;
        try { full = Path.GetFullPath(p); } catch { full = null; }
        if (full == null || !Directory.Exists(full)) { Warn(T("目录不存在，未保存。", "Directory not found, not saved.")); return; }
        cfgWs = full; SaveConfig();
        Success(T("工作区已设为 " + full, "Workspace set to " + full));
    }

    // ---------------- 配置 / 状态文件 ----------------

    static string ResolveStateDir()
    {
        try
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            var fi = new FileInfo(Path.Combine(dir, ".write-test"));
            using (fi.Create()) { }
            File.Delete(fi.FullName);
            return dir;
        }
        catch
        {
            string alt = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeepSeekHarnessLauncher");   // 兼容旧版（改名前的备用状态目录），不随产品改名迁移
            try { Directory.CreateDirectory(alt); } catch { }
            StateDir = alt;   // 先回填 StateDir，LogErr 才能写入日志
            LogErr("ResolveStateDir: 目录不可写，改用备用目录 " + alt);
            return alt;
        }
    }

    static string ConfigPath() { return Path.Combine(StateDir, "launcher.config"); }

    static void LoadConfig()
    {
        try
        {
            foreach (string line in File.ReadAllLines(ConfigPath()))
            {
                string t = line.Trim();
                if (t.StartsWith("lang=")) { string v = t.Substring(5).Trim().ToLowerInvariant();
                    if (v == "zh") lang = Lang.Zh; else if (v == "en") lang = Lang.En; else lang = Lang.Auto; }
                if (t.StartsWith("host=")) { string v = t.Substring(5).Trim().ToLowerInvariant();
                    if (v == "localhost" || v == "127.0.0.1") webHost = v; }
                if (t.StartsWith("ws=")) { string v = t.Substring(3).Trim().Trim('"');
                    if (v.Length > 0) { try { cfgWs = Path.GetFullPath(v); } catch { cfgWs = null; } } }
                if (t.StartsWith("keep_backups=")) { int v; if (int.TryParse(t.Substring(13).Trim(), out v) && v >= 3) cfgKeep = v; }   // 备份保留策略：自动类最多保留份数（最小 3）
                if (t.StartsWith("check_update=")) { string v = t.Substring(13).Trim().ToLowerInvariant(); if (v.Length > 0) cfgCheckUpdate = v != "off"; }   // 启动更新检查开关
            }
        }
        catch { }
    }

    static void SaveConfig()
    {
        try
        {
            string v = lang == Lang.Zh ? "zh" : (lang == Lang.En ? "en" : "auto");
            File.WriteAllText(ConfigPath(), "lang=" + v + Environment.NewLine + "host=" + webHost + Environment.NewLine + "ws=" + (cfgWs ?? "") + Environment.NewLine + "keep_backups=" + cfgKeep + Environment.NewLine + "check_update=" + (cfgCheckUpdate ? "on" : "off") + Environment.NewLine, new UTF8Encoding(false));
        }
        catch { }
    }

    // ---------------- 通用工具 ----------------

    /// <summary>解析可执行文件：带路径直接返回；裸名先查 PATH，再查常见安装目录；都找不到原样返回（交给系统报错）。</summary>
    static string ResolveExe(string name)
    {
        try
        {
            if (name.IndexOf(Path.DirectorySeparatorChar) >= 0 || name.IndexOf('/') >= 0)
                return name;   // 带路径原样返回（交给系统报错）
            foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                string d = dir.Trim();
                if (d.Length == 0) continue;
                try { string f = Path.Combine(d, name); if (File.Exists(f)) return f; } catch { }
            }
            string[] extra = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", name),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "nodejs", name),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", name)
            };
            foreach (string f in extra) { try { if (File.Exists(f)) return f; } catch { } }
        }
        catch { }
        return name;
    }

    /// <summary>给被启动进程补充 node/npm 常用目录的 PATH，避免 winget 新装后当前会话 PATH 未刷新导致找不到命令。</summary>
    static void MergeNodePath(ProcessStartInfo psi)
    {
        try
        {
            var add = new System.Collections.Generic.List<string>();
            add.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm"));
            add.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs"));
            add.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "nodejs"));
            string merged = string.Join(";", add) + ";" + (Environment.GetEnvironmentVariable("PATH") ?? "");
            psi.EnvironmentVariables["PATH"] = merged;
        }
        catch { }
    }

    /// <summary>后台采集命令输出：双流异步排空 + 15 秒超时强杀，杜绝子进程挂起导致的卡死。</summary>
    static string RunCapture(string exe, string args)
    {
        try
        {
            exe = ResolveExe(exe);
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            MergeNodePath(psi);
            using (var p = Process.Start(psi))
            {
                var tOut = p.StandardOutput.ReadToEndAsync();
                var tErr = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(15000))
                {
                    try { p.Kill(); } catch { }
                    p.WaitForExit();
                    LogErr("命令执行超时（15 秒），已强制结束: " + exe + " " + args);
                    return "";
                }
                string so = tOut.Result;
                string err = tErr.Result;
                if (!string.IsNullOrWhiteSpace(err)) LogErr("命令 stderr: " + err.Trim());
                return so.Trim();
            }
        }
        catch { LogErr("RunCapture: 执行异常，返回空 " + exe); return ""; }
    }

    static int RunVisible(string file, string args)
    {
        try
        {
            file = ResolveExe(file);
            var psi = new ProcessStartInfo(file, args) { UseShellExecute = false };
            MergeNodePath(psi);
            using (var p = Process.Start(psi))
            {
                p.WaitForExit();
                return p.ExitCode;
            }
        }
        catch { LogErr("进程启动失败: " + file + " " + args); return -1; }   // 启动失败（如参数错误/被拦截）返回 -1，由调用方友好提示
    }

    static string RunDshVersion()
    {
        string v = RunCapture("cmd.exe", "/c dsh --version 2>nul");
        if (string.IsNullOrWhiteSpace(v))
        {
            string dsh = LocateDsh();
            if (dsh != null) v = RunCapture("cmd.exe", "/c \"" + dsh + "\" --version");
        }
        return v;
    }

    static string LocateDsh()
    {
        string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string npmPath = Path.Combine(appdata, "npm", "dsh.cmd");
        if (File.Exists(npmPath)) return npmPath;
        string where = RunCapture("cmd.exe", "/c where dsh 2>nul");
        if (!string.IsNullOrWhiteSpace(where))
        {
            string first = where.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
            if (File.Exists(first)) return first;
        }
        return null;
    }

    static bool IsPortOpen(int port, int timeoutMs)
    {
        try
        {
            using (var c = new TcpClient())
            {
                var t = c.ConnectAsync(IPAddress.Loopback, port);
                return t.Wait(timeoutMs) && c.Connected;   // Wait 超时返回 false；连接快速失败(faulted)抛 AggregateException 时按 false 处理
            }
        }
        catch { return false; }   // 端口关闭/连接被拒等一律视为"未打开"
    }

    // ---------------- 服务状态三态（v2.1: 端口 + HTTP 探测，避免被其他程序占用 3080 误判） ----------------

    public enum ServiceState { Down, Listening, Ready }

    /// <summary>纯判定：端口开 + HTTP 就绪 → Ready；仅端口开 → Listening；否则 Down。（单测可直接调用）</summary>
    static ServiceState JudgeState(bool portOpen, bool httpOk)
    {
        if (!portOpen) return ServiceState.Down;
        return httpOk ? ServiceState.Ready : ServiceState.Listening;
    }

    /// <summary>HTTP GET 探测：2xx/3xx 视为服务就绪（dsh 主页 200/302 均算）。</summary>
    static bool HttpReady(string url, int ms)
    {
        try
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = ms;
            req.AllowAutoRedirect = true;
            using (var resp = (HttpWebResponse)req.GetResponse())
            {
                int code = (int)resp.StatusCode;
                return code >= 200 && code < 400;
            }
        }
        catch { return false; }
    }

    /// <summary>服务三态探测入口：先端口（快速/可打桩），再 HTTP 验证身份（确认是 dsh 而非其他程序）。</summary>
    static ServiceState ProbeService()
    {
        return JudgeState(IsPortOpen(WEB_PORT, 800), HttpReady(WebUrl(), 800));   // HTTP 探测 800ms 上限：正常 <100ms，挂起时快速降级
    }

    // ---------------- 自动检查更新（v2.1: 启动静默查询 GitHub Releases，发现新版本才提示） ----------------

    static Func<string, int, string> HttpGetImpl = null;   // 单测注入点（为空走真实实现）

    /// <summary>GET 指定 URL，成功返回正文，失败/超时返回 null。GitHub API 要求 User-Agent。</summary>
    static string HttpGet(string url, int ms)
    {
        if (HttpGetImpl != null) return HttpGetImpl(url, ms);
        try
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = ms;
            req.UserAgent = "DeepSeek-Harness-Toolkit";
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }
        catch { return null; }
    }

    /// <summary>从 GitHub Releases API JSON 提取 tag_name（"v2.1.0" → "2.1.0"），失败返回 null。（单测可直接调用）</summary>
    static string ParseLatestTag(string body)
    {
        try
        {
            int i = body.IndexOf("\"tag_name\"", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;
            int q1 = body.IndexOf('"', i + 10);
            int q2 = body.IndexOf('"', q1 + 1);
            if (q1 < 0 || q2 < 0) return null;
            return body.Substring(q1 + 1, q2 - q1 - 1).TrimStart('v', 'V');
        }
        catch { return null; }
    }

    /// <summary>版本号比较："a 低于 b" 返回负数，"相等" 0，"a 高于 b" 正数。支持 "2.1.0" 式分段。（单测可直接调用）</summary>
    static int CompareVersions(string a, string b)
    {
        string[] pa = (a ?? "").Split('.');
        string[] pb = (b ?? "").Split('.');
        for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            int x = 0, y = 0;
            int.TryParse(i < pa.Length ? pa[i] : "0", out x);
            int.TryParse(i < pb.Length ? pb[i] : "0", out y);
            if (x != y) return x < y ? -1 : 1;
        }
        return 0;
    }

    static string CurrentVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v.Major + "." + v.Minor + "." + v.Build;
    }

    /// <summary>查询最新版：无更新或网络失败返回 null（静默），有更新返回新版本号。</summary>
    static string LatestVersion()
    {
        string body = HttpGet("https://api.github.com/repos/sakanamaru/DeepSeek-Harness-Toolkit/releases/latest", 4000);
        if (body == null) return null;
        string tag = ParseLatestTag(body);
        if (tag == null) return null;
        return CompareVersions(CurrentVersion(), tag) < 0 ? tag : null;
    }

    static bool inputEof = false;
    static string ReadLineTrim()
    {
        if (inputEof) return "";
        string s = null;
        try { s = Console.ReadLine(); } catch { }
        if (s == null) { inputEof = true; return ""; }
        return s.Trim();
    }

    // ---------------- 体检 / 关于 / 帮助 ----------------

    static void Check()
    {
        Banner();
        string node = RunCapture("node.exe", "--version");
        C(ConsoleColor.Gray, "  Node.js    : "); CL(ConsoleColor.White, string.IsNullOrWhiteSpace(node) ? T("未检测到", "not found") : node);
        string npm = RunCapture("cmd.exe", "/c npm --version 2>nul");
        C(ConsoleColor.Gray, "  npm        : "); CL(ConsoleColor.White, string.IsNullOrWhiteSpace(npm) ? T("未检测到", "not found") : npm);
        string dsh = LocateDsh();
        C(ConsoleColor.Gray, "  dsh        : "); CL(ConsoleColor.White, dsh == null ? T("未安装", "not installed") : dsh + " ✓");
        if (dsh != null)
        {
            string v = RunDshVersion();
            C(ConsoleColor.Gray, "  dsh 版本   : "); CL(ConsoleColor.White, string.IsNullOrWhiteSpace(v) ? T("（读取失败）", "(read failed)") : v);
        }
        C(ConsoleColor.Gray, "  Web 服务   : "); ServiceState stC = ProbeService();
        CL(ConsoleColor.White, stC == ServiceState.Ready ? WebUrl() + " " + T("已在运行", "running")
            : (stC == ServiceState.Listening ? T("启动中（端口已开，服务未就绪）", "starting (port open, not ready)") : T("未启动", "not started")));
        C(ConsoleColor.Gray, "  UI 语言    : "); CL(ConsoleColor.White, lang == Lang.Auto ? T("跟随系统", "follow system") : (lang == Lang.Zh ? "简体中文" : "English"));
        Pause();
    }

    static void About()
    {
        Banner();
        Console.WriteLine();
        C(ConsoleColor.Gray, "  版本       : "); CL(ConsoleColor.White, Assembly.GetExecutingAssembly().GetName().Version.ToString());
        C(ConsoleColor.Gray, "  说明       : "); CL(ConsoleColor.White, T("DeepSeek Harness(dsh) 安装/启动/卸载/备份助手", "DeepSeek Harness (dsh) install/start/uninstall/backup helper"));
        C(ConsoleColor.Gray, "  v1 脚本协助: "); CL(ConsoleColor.White, "SOGR-Momono Dango（QwenPaw/DeepseekAPI-V4-Flash-0731）");
        C(ConsoleColor.Gray, "  v2 重构封装: "); CL(ConsoleColor.White, "DeepSeek DSH （DSH/DeepseekAPI-V4-Flash-0731）");
        C(ConsoleColor.Gray, "  GitHub     : "); CL(ConsoleColor.White, "@sakanamaru");
        Console.WriteLine();
        C(ConsoleColor.DarkGray, T("  按 G 打开 GitHub（不会自动打开），其他键返回...",
                                   "  Press G to open GitHub (never auto-opened), any other key to return..."));
        bool openGh = false;
        try { var kk = Console.ReadKey(true); openGh = (kk.KeyChar == 'g' || kk.KeyChar == 'G'); } catch { }
        if (openGh) OpenUrl("https://" + GITHUB_HANDLE);
        Console.WriteLine();
    }

    static void Help()
    {
        Banner();
        Console.WriteLine(T("用法：", "Usage:"));
        Console.WriteLine("  DeepSeek Harness Toolkit v2.1.0 install | start | uninstall | check | about | help");
        Console.WriteLine(T("  不带参数启动交互菜单（dsh 已安装时 5 秒自动启动；未安装时按 1 选择安装）。",
                            "  Without arguments: interactive menu (auto-start in 5s when dsh is installed; press 1 to install when not)."));
    }

    // ---------------- 自检 ----------------

    static void Selftest(string[] args)
    {
        var sb = new StringBuilder();
        sb.AppendLine("== DeepSeek Harness Toolkit selftest ==");
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var title = (AssemblyTitleAttribute)Attribute.GetCustomAttribute(asm, typeof(AssemblyTitleAttribute));
            var company = (AssemblyCompanyAttribute)Attribute.GetCustomAttribute(asm, typeof(AssemblyCompanyAttribute));
            var desc = (AssemblyDescriptionAttribute)Attribute.GetCustomAttribute(asm, typeof(AssemblyDescriptionAttribute));
            sb.AppendLine("title   : " + (title == null ? "(null)" : title.Title));
            sb.AppendLine("company : " + (company == null ? "(null)" : company.Company));
            sb.AppendLine("desc    : " + (desc == null ? "(null)" : desc.Description));
            sb.AppendLine("version : " + asm.GetName().Version);
            sb.AppendLine("ui lang : " + CultureInfo.CurrentUICulture.Name);
            sb.AppendLine("dsh installed (live): " + (LocateDsh() != null));

            sb.AppendLine("port 1 (expect False): " + IsPortOpen(1, 500));
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            bool open = IsPortOpen(port, 500);
            l.Stop();
            sb.AppendLine("self-listener (expect True): " + open);

            sb.AppendLine("dsh loc  : " + (LocateDsh() ?? "(null)"));
            sb.AppendLine("node ver : " + (RunCapture("node.exe", "--version") ?? "(empty)"));
            sb.AppendLine("state dir: " + StateDir);
            sb.AppendLine("data root: " + DataRoot());
        }
        catch (Exception ex) { sb.AppendLine("EXCEPTION: " + ex); }
        string report = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "dsh_selftest.txt");
        try { File.WriteAllText(report, sb.ToString(), new UTF8Encoding(true)); Console.WriteLine("report -> " + report); }
        catch (Exception ex) { Console.WriteLine("write report failed: " + ex.Message); }
    }

    static void Pause()
    {
        Console.WriteLine();
        C(ConsoleColor.DarkGray, T("  按任意键继续...", "  Press any key to continue..."));
        try { Console.ReadKey(true); } catch { }
        Console.WriteLine();
    }

#if UNIT
    // 单元测试代理（仅 /define:UNIT 构建存在）：嵌套类可访问外层 private 成员，生产构建无此类型
    public static class Test
    {
        public static string PathP(string p) { return Program.P(p); }
        public static string PathTrim(string p) { return Program.TrimP(p); }
        public static bool WorkspaceOk(string p) { return Program.LooksLikeWorkspace(p); }
        public static bool DshData(string p) { return Program.LooksLikeDshData(p); }
        public static bool PortOpen(int port, int ms) { return Program.IsPortOpen(port, ms); }
        public static void SetKeep(int v) { Program.cfgKeep = v; }
        public static void SetStateDir(string d) { Program.StateDir = d; }
        public static bool RotateLog(string file, long max) { return Program.RotateLogIfNeeded(file, max); }
        public static string BkSuffix(BackupKind k) { return Program.BackupSuffix(k); }
        public static bool IsAutoName(string n) { return Program.IsAutoBackupName(n); }
        public static List<string> Retention() { return Program.EnforceBackupRetention(); }
        public static ServiceState JudgeState(bool port, bool http) { return Program.JudgeState(port, http); }
        public static int CmpVer(string a, string b) { return Program.CompareVersions(a, b); }
        public static string ParseTag(string body) { return Program.ParseLatestTag(body); }
        public static string Latest() { return Program.LatestVersion(); }
        public static string CurVer() { return Program.CurrentVersion(); }
        public static void SetHttpGet(Func<string, int, string> f) { Program.HttpGetImpl = f; }
    }
#endif
}