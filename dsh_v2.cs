// ============================================================================
//  DeepSeek Harness Toolkit V2.4.0  ——  DeepSeek Harness(dsh) 安装 / 启动 / 卸载 / 备份恢复工具箱
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

[assembly: AssemblyTitle("DeepSeek Harness Toolkit V2.4.0")]
[assembly: AssemblyDescription("DeepSeek Harness(dsh) 安装/启动/卸载/备份恢复工具箱。v1: SOGR-Momono Dango(QwenPaw/DeepseekAPI-V4-Flash-0731)；v2: DeepSeek DSH(DSH/DeepseekAPI-V4-Flash-0731)；GitHub @sakanamaru")]
[assembly: AssemblyCompany("SOGR-Momono Dango / DeepSeek DSH / @sakanamaru")]
[assembly: AssemblyProduct("DeepSeek Harness Toolkit")]
[assembly: AssemblyVersion("2.4.0.0")]
[assembly: AssemblyFileVersion("2.4.0.0")]

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
    static bool cfgCheckDshUpdate = true; // 检测 dsh 本体更新开关（配置 check_dsh_update=off 关闭）
    static string cfgDshVersions = "";    // 本机装过的 dsh 历史版本（逗号分隔，最近 10 个）
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
        try { Console.Title = "DeepSeek Harness Toolkit V2.4.0"; } catch { }
        StateDir = ResolveStateDir();
        // 注意：根目录标记 .dsh_launcher_root 只随发布包分发，本程序永不自行补建——
        // 若启动时"看起来像完整安装"就自动写标记，攻击者可诱导用户将 exe 与任意同名文件
        // 放一处后自动补建标记，削弱"单独复制 exe 永远不能 wipe"的安全边界。
        LoadConfig();
        if (args.Length > 0)
        {
            switch (args[0].TrimStart('-', '/').ToLowerInvariant())
            {
                case "install":   case "i": Install(); return;
                case "start":     case "s":
                    if (args.Length > 1 && (args[1] == "--bg" || args[1] == "-bg")) StartBg();   // GUI 后台启动：启动后立即返回，不进监控页
                    else Start();
                    return;
                case "stop": StopCli(); return;   // 非交互停止 dsh web（GUI 用）
                case "uninstall": case "u": Uninstall(); return;
                case "check":     case "c": Check();   return;
                case "update":    case "up": UpdateDsh(); return;
                case "about":     case "a": About();   return;
                case "shortcut":  case "sc": ShortcutCli(); return; // 创建桌面快捷方式（脚本/安装后调用）
                case "backup":    case "b": NIBackup();  return;   // 非交互备份（GUI/脚本用）
                case "restore":   case "r": NIRestore(); return;   // 非交互恢复最新备份（GUI/脚本用）
                case "status": StatusCli(); return;   // 服务三态（GUI 状态灯用）
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
        CL(ConsoleColor.Cyan,   "  DeepSeek Harness Toolkit V2.4.0");
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
            CL(ConsoleColor.White, "  8) " + T("更新 dsh", "Update dsh"));
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
                case "8": UpdateDsh(); break;
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

    /// <summary>npm 全局安装/更新 dsh 指定版本（version 空=最新）；多源依次尝试，返回 0=成功，-1=全部失败。
    /// 版本号会拼进 cmd 命令行（.NET 4.x ProcessStartInfo 无 ArgumentList，只能字符串拼接），
    /// 故入口做双层防线：① 版本整串白名单复核（IsValidNpmVersion，防注入）② 包名整体加引号、注册表仅取代码内常量。
    /// registry 参数绝不出自用户输入/网络数据（仅 NPM_OFFICIAL / NPM_MIRROR 常量），避免任何注入面。</summary>
    static int NpmInstallDsh(string version, string[] registries)
    {
        if (!string.IsNullOrEmpty(version) && !IsValidNpmVersion(version))
        {
            Error(T("版本号不合法，已拒绝安装：" + version, "Invalid version string; install refused: " + version));
            return -1;
        }
        string pkg = "\"@deepseek-ai/dsh" + (string.IsNullOrEmpty(version) ? "" : "@" + version) + "\"";
        for (int i = 0; i < registries.Length; i++)
        {
            Info(string.Format(T("第 {0}/{1} 次尝试，源：{2}", "Attempt {0}/{1}, registry: {2}"), i + 1, registries.Length, registries[i]));
            int code = RunVisible("cmd.exe", "/c npm install -g --registry=" + registries[i] + " " + pkg);
            if (code == 0) return 0;
            if (code == -2)
                Warn(T("安装/更新超时（10 分钟），已终止。请检查网络或稍后重试。", "Timed out after 10 minutes; terminated. Check the network and retry."));
            else
                Warn(string.Format(T("失败（退出码 {0}）", "Failed (exit code {0})"), code));
        }
        return -1;
    }

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
        // 版本选择（回车=最新，L=历史版本列表）
        string ver = "";
        CL(ConsoleColor.White, T("  版本：回车=最新版，L=查看历史版本", "  Version: Enter=latest, L=list versions"));
        Console.Write("  > ");
        string vsel = ReadLineTrim().Trim();
        if (vsel == "L" || vsel == "l")
        {
            ver = ListDshVersions();
            if (ver == null) { Info(T("已取消安装。", "Install cancelled.")); Pause(); return; }
        }
        int code0 = NpmInstallDsh(ver, registries);
        if (code0 != 0) { Error(T("安装失败。请检查网络后重试。", "Install failed. Check your network and retry.")); Pause(); return; }

        Console.WriteLine();
        Success(T("安装成功！正在验证...", "Installed! Verifying..."));
        string nv = RunDshVersion();
        string disp = string.IsNullOrWhiteSpace(nv) ? ver : nv;
        if (disp.Length > 0) RecordDshVersion(disp);
        Success(T("dsh 版本：" + (string.IsNullOrWhiteSpace(nv) ? T("？（请新开终端验证）", "? (verify in a new terminal)") : nv),
                  "dsh version: " + (string.IsNullOrWhiteSpace(nv) ? "? (verify in a new terminal)" : nv)));
        Console.WriteLine();
        Info(T("接下来：选择【2 启动 Web 界面】即可打开浏览器。", "Next: choose【Start Web UI】to open the browser."));
        Console.WriteLine();
        CL(ConsoleColor.White, T("  是否创建桌面快捷方式？(Y/N，默认 N) ", "  Create a desktop shortcut? (Y/N, default N) "));
        string sn = ReadLineTrim();
        if (sn == "y" || sn == "Y")
        {
            string serr = CreateDesktopShortcut(DesktopDir());
            if (serr == null) Success(T("桌面快捷方式已创建：DeepSeek Harness Toolkit.lnk", "Desktop shortcut created: DeepSeek Harness Toolkit.lnk"));
            else Error(T("桌面快捷方式创建失败：" + serr, "Desktop shortcut creation failed: " + serr));
        }
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

    /// <summary>后台启动 dsh web（GUI/脚本用）：启动后立即返回，不进监控页、不打开浏览器。
    /// 输出 START_OK / START_FAIL &lt;原因&gt;；已在运行时同样 START_OK（幂等）。不 Pause、不读输入。</summary>
    static void StartBg()
    {
        string dsh = LocateDsh();
        if (dsh == null) { Console.WriteLine("START_FAIL " + T("未找到 dsh", "dsh not found")); return; }
        if (ProbeService() != ServiceState.Down) { Console.WriteLine("START_OK"); return; }   // 已在运行（含启动中）→ 幂等成功
        try
        {
            var psi = new ProcessStartInfo("cmd.exe", "/k \"" + dsh + "\" web")
            {
                UseShellExecute = true,
                WorkingDirectory = WorkspaceRoot() ?? AppDomain.CurrentDomain.BaseDirectory
            };
            Process.Start(psi);
            Console.WriteLine("START_OK");
        }
        catch (Exception ex) { Console.WriteLine("START_FAIL " + ex.Message); }
    }

    /// <summary>从 netstat 输出解析监听指定端口的进程 PID；找不到返回 0。纯解析，便于单测。</summary>
    static int ParsePortPid(string netstatOutput, int port)
    {
        if (string.IsNullOrWhiteSpace(netstatOutput)) return 0;
        string suffix = ":" + port;
        foreach (string raw in netstatOutput.Split('\n'))
        {
            string t = raw.Trim();
            if (t.Length == 0 || t.IndexOf("LISTENING", StringComparison.OrdinalIgnoreCase) < 0) continue;
            string[] parts = t.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            // 格式: TCP  127.0.0.1:3080  0.0.0.0:0  LISTENING  1234   （本地地址为第 2 列，PID 末列）
            if (parts.Length < 5) continue;
            if (!parts[1].EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
            int pid;
            if (int.TryParse(parts[parts.Length - 1], out pid) && pid > 0) return pid;
        }
        return 0;
    }

    /// <summary>监听指定端口的进程 PID（netstat -ano -p tcp），找不到/出错返回 0。</summary>
    static int FindPortPid(int port)
    {
        return ParsePortPid(RunCapture("cmd.exe", "/c netstat -ano -p tcp"), port);
    }

    /// <summary>非交互停止 dsh web：找监听 3080 的进程树并终止，验证端口释放。
    /// 输出 STOP_OK / STOP_FAIL &lt;原因&gt;；已停止时同样 STOP_OK（幂等）。不 Pause、不读输入。</summary>
    static void StopCli()
    {
        if (ProbeService() == ServiceState.Down) { Console.WriteLine("STOP_OK"); return; }   // 已停止 → 幂等成功
        int pid = FindPortPid(WEB_PORT);
        if (pid <= 0) { Console.WriteLine("STOP_FAIL " + T("未找到 dsh 进程", "dsh process not found")); return; }
        KillProcessTree(pid);   // 进程树终止：连带杀派生的 node 子进程
        for (int i = 0; i < 20; i++)
        {
            Thread.Sleep(250);
            if (ProbeService() == ServiceState.Down) { Console.WriteLine("STOP_OK"); return; }
        }
        Console.WriteLine("STOP_FAIL " + T("端口未释放", "port still in use"));
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

    /// <summary>标记文件有效：存在且内容含产品名（防"伪造空 marker 文件"诱导清除；版本无关，兼容未来版本演进）。</summary>
    static bool RootMarkerValid(string dir)
    {
        try
        {
            string p = Path.Combine(dir, ROOT_MARKER);
            if (!File.Exists(p)) return false;
            string c = File.ReadAllText(p, new UTF8Encoding(false));
            return c.IndexOf("DeepSeek Harness Toolkit", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch { return false; }
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
        // 防误删验证（严格）：必须存在**有效**的根目录标记 .dsh_launcher_root（随包分发，内容含产品名）；
        // 仅"看起来像完整安装"（≥2 个配套文件）或伪造的空 marker 不再放行——杜绝诱导清除。
        // M-6：marker 校验固定锚定 exe 目录（BaseDirectory），而非可能漂移到 %APPDATA% 的 StateDir——
        // 避免"完整包放只读位置时 wipe 被永久拒绝"以及"攻击者写 APPDATA 即解锁任意位置 exe"两个方向的问题。
        bool rootOk = RootMarkerValid(AppDomain.CurrentDomain.BaseDirectory);
        if (!rootOk)
        {
            Error(T("未检测到完整安装（缺少有效标记文件 " + ROOT_MARKER + "）。\n  请从解压后的完整目录运行本程序；切勿将 exe 单独复制后执行清除。已拒绝删除。",
                    "This does not look like a full installation (no valid marker " + ROOT_MARKER + ").\n  Run from the complete extracted folder; do not copy the exe alone. Deletion refused."));
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
        // L-6：备份目录/状态目录若落在删除目标子树内，pre-wipe 备份会随删除一起被删（安全网失效）→ 显式拒绝
        string bRoot = BackupsRoot();
        string stDir = StateDir;
        if (IsSubPath(dir, bRoot) || IsSubPath(dir, stDir))
        {
            Error(T("拒绝清除：备份目录或状态目录位于删除目标之内（" + dir + "），清除会连带删除安全备份。请先迁移备份目录。",
                    "Refused to wipe: the backup/state dir lies inside the target (" + dir + "), so wiping would also delete the safety backup. Move the backup dir first."));
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

    /// <summary>备份目录格式校验：目录名须为本工具生成的 "dsh-data-&lt;时间戳&gt;[后缀]" 形式，
    /// 且内容含 dsh 数据特征或工作区子目录（_workspace）。仅目录存在不算数。</summary>
    static bool IsValidBackupDir(string dir)
    {
        try
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
            string name = Path.GetFileName(dir.TrimEnd('\\'));
            if (!name.StartsWith("dsh-data-", StringComparison.OrdinalIgnoreCase)) return false;
            if (LooksLikeDshData(dir)) return true;
            string ws = Path.Combine(dir, "_workspace");
            if (Directory.Exists(ws)) return true;
            return false;
        }
        catch { return false; }
    }

    /// <summary>备份目录定位：本身是有效备份包（dsh-data-* + 数据特征/工作区）则返回；
    /// 若所选目录下恰好只含一个 dsh-data-* 子目录（用户可能选了备份的父目录），自动下探定位；
    /// 其余一律返回 null（不是备份包）。防把任意文件夹当备份恢复/导入。</summary>
    static string ResolveBackupDir(string dir)
    {
        if (IsValidBackupDir(dir)) return dir;
        try
        {
            var subs = new List<string>();
            foreach (string d in Directory.GetDirectories(dir))
            {
                string n = Path.GetFileName(d.TrimEnd('\\'));
                if (n.StartsWith("dsh-data-", StringComparison.OrdinalIgnoreCase)) subs.Add(d);
            }
            if (subs.Count == 1 && IsValidBackupDir(subs[0])) return subs[0];
        }
        catch { }
        return null;
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
            // M-7：手动输入同样过工作区黑名单（盘根/用户主目录/系统目录等整盘复制风险），命中需显式二次确认
            if (!LooksLikeWorkspace(full))
            {
                Warn(T("该路径位于系统/用户目录（盘根、用户主目录、Windows、Program Files 等），整目录备份可能包含大量无关甚至敏感文件。",
                        "This path is under a system/user directory (drive root, user profile, Windows, Program Files, ...); backing it up whole may include unrelated or sensitive files."));
                CL(ConsoleColor.White, T("  仍要包含该目录吗？输入 yes 确认（其他键跳过）：", "  Include it anyway? Type yes to confirm (anything else skips): "));
                string c2 = ReadLineTrim().Trim();
                if (c2 != "yes") { Warn(T("已跳过该目录。", "Directory skipped.")); continue; }
            }
            if (!Directory.Exists(full)) { Warn(T("目录不存在：" + p + "（直接回车可结束）。", "Not found: " + p + " (Enter to finish).")); continue; }
            bool dup = false, nested = false;
            foreach (string e in wsList)
            {
                string ee = e.TrimEnd('\\');
                string ff = full.TrimEnd('\\');
                if (string.Equals(ee, ff, StringComparison.OrdinalIgnoreCase)) { dup = true; break; }
                if (ff.StartsWith(ee + "\\", StringComparison.OrdinalIgnoreCase) ||
                    ee.StartsWith(ff + "\\", StringComparison.OrdinalIgnoreCase)) { nested = true; break; }
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
        // 恢复前校验备份格式：目录名必须 dsh-data-* 且含数据特征/工作区（防误把任意目录当备份恢复）
        if (!IsValidBackupDir(bk))
        {
            Warn(T("所选目录不是有效的备份包（应为 dsh-data-时间戳 格式且含数据）：" + Path.GetFileName(bk) +
                   "\n  已取消恢复，请检查备份目录。", "Not a valid backup package (expected dsh-data-TIMESTAMP with data): " +
                   Path.GetFileName(bk) + "\n  Restore cancelled; check the backup folder."));
            Pause();
            return;
        }
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
            dest = Path.Combine(root, "dsh-data-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + BackupSuffix(kind));
            // v2.1.2 安全：保护性备份（Pre*）严格模式——任意文件复制失败 → 整个备份失败 → 中止后续危险操作；
            // 手动/自动备份保持 best-effort（跳过被锁文件并记日志）
            bool strict = kind == BackupKind.PreWipe || kind == BackupKind.PreRestore || kind == BackupKind.PreImport || kind == BackupKind.PreUpdate;
            CopyTree(source, dest, !strict);
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
                    CopyTree(w, wsDest, !strict);
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
    public enum BackupKind { Manual, Auto, PreRestore, PreImport, PreWipe, PreUpdate }

    /// <summary>自动类备份目录名的来源后缀（手动无后缀，兼容旧版产物）。</summary>
    static string BackupSuffix(BackupKind k)
    {
        switch (k)
        {
            case BackupKind.Auto: return "-auto";
            case BackupKind.PreRestore: return "-pre-restore";
            case BackupKind.PreImport: return "-pre-import";
            case BackupKind.PreWipe: return "-pre-wipe";
            case BackupKind.PreUpdate: return "-pre-update";
            default: return "";
        }
    }

    /// <summary>备份目录名是否属于"自动类"（自动类参与保留策略清理；手动备份永久保留）。</summary>
    static bool IsAutoBackupName(string name)
    {
        return name.EndsWith("-auto") || name.EndsWith("-pre-restore") || name.EndsWith("-pre-import") || name.EndsWith("-pre-wipe") || name.EndsWith("-pre-update");
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
        src = TrimTrailingSep(src); dst = TrimTrailingSep(dst);
        Directory.CreateDirectory(P(dst));
        int skippedNested = 0;   // L-7：统计被跳过的 dsh-data-* 嵌套备份包，最后统一 Warn 提示（避免静默丢用户数据）
        foreach (string d in Directory.GetDirectories(P(src)))
        {
            string name = Path.GetFileName(TrimP(d));
            if (name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)) continue;   // 依赖可重装，备份时跳过
            if (name.Equals("backup", StringComparison.OrdinalIgnoreCase)) continue;         // 防止备份目录把自身备份递归复制进去
            if (name.StartsWith("dsh-data-", StringComparison.OrdinalIgnoreCase))            // L-7：大小写不敏感（用户同名业务目录也一并跳过并提示，不静默）
            { skippedNested++; continue; }
            try { CopyTree(TrimP(d), Path.Combine(dst, name), skipLocked); }
            catch (Exception ex)
            {
                if (!skipLocked) throw;
                LogErr("跳过无法复制的子目录: " + TrimP(d) + " : " + ex.Message);
            }
        }
        if (skippedNested > 0)
            Warn(T("已跳过 " + skippedNested + " 个嵌套备份目录（dsh-data-*），不复制进本次备份。",
                    "Skipped " + skippedNested + " nested backup folder(s) (dsh-data-*), not copied into this backup."));
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

    /// <summary>判断 child 是否位于 parent 子树内（含相等）；大小写不敏感。用于 wipe 前校验备份/状态目录不被误删。</summary>
    static bool IsSubPath(string parent, string child)
    {
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(child)) return false;
        string a = TrimTrailingSep(parent).ToLowerInvariant();
        string b = TrimTrailingSep(child).ToLowerInvariant();
        return b == a || b.StartsWith(a + "\\");
    }

    /// <summary>去掉结尾分隔符，但保留盘根语义（D:\ 不会变成 D:，UNC 共享根不会丢失尾部斜杠）。</summary>
    static string TrimTrailingSep(string p)
    {
        if (string.IsNullOrEmpty(p)) return p;
        string root = null;
        try { root = Path.GetPathRoot(p); } catch { root = null; }
        p = p.TrimEnd('\\');
        if (root != null && p.Length < root.Length) return root;   // 盘根被 trim 掉时还原
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
            Directory.CreateDirectory(P(dst));   // M-5：恢复侧同样走 \\?\ 长路径前缀
            Info(T("正在恢复数据...", "Restoring data..."));
            foreach (string d in Directory.GetDirectories(P(path)))
                if (Path.GetFileName(d) != "_workspace")
                    CopyTree(d, Path.Combine(dst, Path.GetFileName(d)));
            foreach (string f in Directory.GetFiles(P(path)))
                File.Copy(P(f), P(Path.Combine(dst, Path.GetFileName(f))), true);   // M-5：恢复侧同样走 \\?\ 长路径前缀
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
        bool custom = t.Length > 0;
        if (custom) { try { target = Path.GetFullPath(t); } catch { target = null; } }
        if (target == null) target = def;
        if (target == null || !Directory.Exists(target))
        {
            Warn(T("目标目录无效，已跳过该工作区。", "Invalid target, skipped."));
            return;
        }
        // 自定义恢复目标确认：可能覆盖已有数据或指向错误位置，恢复前要求确认
        if (custom && (def == null || !string.Equals(target, def, StringComparison.OrdinalIgnoreCase)))
        {
            bool hasContent = Directory.GetFileSystemEntries(target).Length > 0;
            string ask = hasContent
                ? T("  警告：目标目录非空（将合并/覆盖其中文件）：" + target + "\n  确认恢复？输入 y 继续：",
                    "  Warning: target directory is not empty (files will be merged/overwritten): " + target + "\n  Continue restore? Type y: ")
                : T("  恢复目标：" + target + "。确认？输入 y 继续：",
                    "  Restore target: " + target + ". Continue? Type y: ");
            Console.Write(ask);
            string confirm = ReadLineTrim();
            if (confirm != "y" && confirm != "Y") { Warn(T("已取消。", "Cancelled.")); return; }
        }
        if (isNewFormat)
        {
            foreach (string d in Directory.GetDirectories(srcDir))
                CopyTree(d, Path.Combine(target, Path.GetFileName(d)));
            foreach (string f in Directory.GetFiles(srcDir))
                if (Path.GetFileName(f) != ".dshws")
                    File.Copy(P(f), P(Path.Combine(target, Path.GetFileName(f))), true);   // M-5：恢复侧同样走 \\?\ 长路径前缀
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
        // 备份格式校验：必须是 dsh-data-* 备份包（或所选目录下恰好一个）；防把任意文件夹当备份导入
        string bkPath = ResolveBackupDir(path);
        if (bkPath == null)
        {
            Warn(T("所选目录不是有效的备份包（目录名须为 dsh-data-时间戳，且内容含 dsh 数据或工作区）。\n  请选择备份文件夹本身（或仅含一个备份子目录的父目录）。",
                   "Not a valid backup package (directory name must be dsh-data-TIMESTAMP and contain dsh data or workspaces).\n  Pick the backup folder itself (or a parent containing exactly one)."));
            Pause();
            return;
        }
        path = bkPath;
        bool hasWs = Directory.Exists(Path.Combine(path, "_workspace"));
        bool hasData = LooksLikeDshData(path);
        Console.WriteLine();
        C(ConsoleColor.Gray, T("  导入内容：", "  Import contents:"));
        C(ConsoleColor.Gray, "    - " + T("dsh 数据", "dsh data") + " : "); CL(ConsoleColor.White, hasData ? T("有", "yes") : T("无", "no"));
        C(ConsoleColor.Gray, "    - " + T("工作区", "workspace") + " : "); CL(ConsoleColor.White, hasWs ? T("有", "yes") : T("无", "no"));
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
                if (t.StartsWith("keep_backups=")) { int v; if (int.TryParse(t.Substring(13).Trim(), out v)) cfgKeep = (v < 3) ? 3 : v; }   // 备份保留策略：自动类最多保留份数（最小 3，直接夹到 3 而非忽略）
                if (t.StartsWith("check_update=")) { string v = t.Substring(13).Trim().ToLowerInvariant(); if (v.Length > 0) cfgCheckUpdate = v != "off"; }   // 启动更新检查开关
                if (t.StartsWith("check_dsh_update=")) { string v = t.Substring(17).Trim().ToLowerInvariant(); if (v.Length > 0) cfgCheckDshUpdate = v != "off"; }   // dsh 本体更新检测开关
                if (t.StartsWith("dsh_versions=")) { string v = t.Substring(13).Trim(); if (v.Length > 0) cfgDshVersions = v; }   // 本机 dsh 历史版本
            }
        }
        catch { }
    }

    static void SaveConfig()
    {
        try
        {
            string v = lang == Lang.Zh ? "zh" : (lang == Lang.En ? "en" : "auto");
            File.WriteAllText(ConfigPath(), "lang=" + v + Environment.NewLine + "host=" + webHost + Environment.NewLine + "ws=" + (cfgWs ?? "") + Environment.NewLine + "keep_backups=" + cfgKeep + Environment.NewLine + "check_update=" + (cfgCheckUpdate ? "on" : "off") + Environment.NewLine + "check_dsh_update=" + (cfgCheckDshUpdate ? "on" : "off") + Environment.NewLine + "dsh_versions=" + cfgDshVersions + Environment.NewLine, new UTF8Encoding(false));
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
                    KillProcessTree(p.Id);   // 进程树终止：连带杀派生 npm/node 子进程，杜绝孤儿进程
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

    /// <summary>前台执行可见子进程；npm/winget 等长操作设置 10 分钟超时，超时强杀并返回 -2（明确失败），启动失败返回 -1。
    /// 双流后台实时排空并转发到主控制台：既保持过程可见，又防止管道缓冲写满导致子进程挂起（经典死锁）。
    /// 改法对齐 RunCapture 的异步排空范式（.NET 4.x 无 async/await，用 ThreadPool + ReadLine 循环）。</summary>
    static int RunVisible(string file, string args)
    {
        try
        {
            file = ResolveExe(file);
            var psi = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            MergeNodePath(psi);
            using (var p = Process.Start(psi))
            {
                // 双流各自持续排空并转发，避免缓冲满 → 子进程 write 阻塞 → 与 WaitForExit 互等死锁
                DrainAndForward(p.StandardOutput, Console.Out);
                DrainAndForward(p.StandardError, Console.Error);
                if (!p.WaitForExit(10 * 60 * 1000))   // v2.1.2：10 分钟超时（原无限等待，npm/winget 挂起会卡死）
                {
                    KillProcessTree(p.Id);   // 进程树终止：连带杀派生 npm/node 子进程，杜绝孤儿进程
                    p.WaitForExit();
                    LogErr("长时间操作超时（10 分钟），已强制结束: " + file + " " + args);
                    return -2;   // 超时终止
                }
                return p.ExitCode;
            }
        }
        catch { LogErr("进程启动失败: " + file + " " + args); return -1; }   // 启动失败（如参数错误/被拦截）返回 -1，由调用方友好提示
    }

    /// <summary>后台线程逐行读取子进程输出流并转发到主控制台（转发保持 npm/winget 进度可见；排空防止管道死锁）。</summary>
    static void DrainAndForward(System.IO.StreamReader src, System.IO.TextWriter dst)
    {
        try
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string line;
                    while ((line = src.ReadLine()) != null)
                    {
                        try { if (dst != null) dst.WriteLine(line); } catch { }
                    }
                }
                catch { }
            });
        }
        catch { }
    }

    /// <summary>进程树终止：taskkill /T /F 连带杀派生子进程（npm.cmd→node.exe 等），失败时回退 p.Kill()。
    /// 解决仅 kill 直接进程（cmd.exe）导致 npm/node 孤儿进程继续运行的问题（与重试产生文件锁竞态）。</summary>
    static void KillProcessTree(int pid)
    {
        try
        {
            var psi = new ProcessStartInfo("taskkill.exe", "/PID " + pid + " /T /F")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var k = Process.Start(psi))
            {
                if (k != null)
                {
                    var o = k.StandardOutput.ReadToEndAsync();
                    var e = k.StandardError.ReadToEndAsync();
                    if (!k.WaitForExit(5000)) { try { k.Kill(); } catch { } }
                    else { string r = o.Result.Trim(); LogErr("taskkill 结果: " + (string.IsNullOrWhiteSpace(r) ? "(empty)" : r)); }
                }
            }
        }
        catch (Exception ex)
        {
            LogErr("taskkill 失败，回退直接 Kill: " + ex.Message);
            try { var p = Process.GetProcessById(pid); p.Kill(); } catch { }
        }
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
            // L-1：where 结果逐行净化——拒绝含 %（cmd 变量展开面）或控制符的候选，防 PATH 中恶意同名 dsh.cmd 改写命令结构
            string[] lines = where.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string cand in lines)
            {
                string c = cand.Trim();
                if (c.Length == 0) continue;
                if (c.IndexOf('%') >= 0) continue;                       // %VAR% 会被 cmd /c 展开
                if (c.IndexOfAny(new char[] { '&', '|', ';', '>', '<', '^' }) >= 0) continue;   // 命令结构字符
                if (File.Exists(c)) return c;
            }
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

    /// <summary>取版本核心段（去掉 -rc/-beta 等 pre-release 后缀）："0.1.1-rc.2" → "0.1.1"。</summary>
    static string CoreVersion(string v)
    {
        if (string.IsNullOrEmpty(v)) return v ?? "";
        int d = v.IndexOf('-');
        return d >= 0 ? v.Substring(0, d) : v;
    }

    /// <summary>版本号比较（semver 语义）：核心段数字比较；核心段相同再比较 pre-release 后缀——
    /// ① 正式版（无后缀）高于 rc/beta（2.1.2 > 2.1.2-rc）；② 同带后缀按 `.` 分段逐段比较，数字段按数值序、
    /// 字母段按字典序（rc.1 &lt; rc.2 &lt; rc.10），缺段视为更低。
    /// "a 低于 b" 返回负数，"相等" 0，"a 高于 b" 正数。</summary>
    static int CompareVersions(string a, string b)
    {
        string[] pa = CoreVersion(a).Split('.');
        string[] pb = CoreVersion(b).Split('.');
        for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            int x = 0, y = 0;
            int.TryParse(i < pa.Length ? pa[i] : "0", out x);
            int.TryParse(i < pb.Length ? pb[i] : "0", out y);
            if (x != y) return x < y ? -1 : 1;
        }
        return ComparePreRelease(a, b);
    }

    /// <summary>比较 pre-release 后缀（仅当核心段已相等时调用）：无后缀（正式版）> 有后缀；
    /// 同带后缀按 `.` 分段逐段比（数字段数值序、字母/混合段字典序，ASCII 序下数字段天然低于字母段），缺段更低。</summary>
    static int ComparePreRelease(string a, string b)
    {
        int da = a.IndexOf('-');
        int db = b.IndexOf('-');
        string pa = da >= 0 ? a.Substring(da + 1) : "";
        string pb = db >= 0 ? b.Substring(db + 1) : "";
        if (pa.Length == 0 && pb.Length == 0) return 0;
        if (pa.Length == 0) return 1;    // 正式版（无后缀）高于预发布
        if (pb.Length == 0) return -1;
        string[] sa = pa.Split('.');
        string[] sb = pb.Split('.');
        for (int i = 0; i < Math.Max(sa.Length, sb.Length); i++)
        {
            string x = i < sa.Length ? sa[i] : null;
            string y = i < sb.Length ? sb[i] : null;
            if (x == null && y == null) return 0;
            if (x == null) return -1;    // 较短后缀更低：rc < rc.1
            if (y == null) return 1;
            int nx, ny;
            bool xn = int.TryParse(x, out nx);
            bool yn = int.TryParse(y, out ny);
            if (xn && yn)
            {
                if (nx != ny) return nx < ny ? -1 : 1;
            }
            else
            {
                int c = string.CompareOrdinal(x, y);
                if (c != 0) return c < 0 ? -1 : 1;
            }
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
        tag = SanitizeLatestVersion(tag);   // L-8：tag 展示/比较前过严格白名单，拒绝控制字符污染控制台
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

    // ---------------- 桌面快捷方式 ----------------

    /// <summary>创建桌面快捷方式指向本程序 exe；返回 null=成功，否则=原因。</summary>
    static string CreateDesktopShortcut(string desktopDir)
    {
        try
        {
            string exe = Assembly.GetExecutingAssembly().Location;   // 本体 exe 绝对路径
            if (string.IsNullOrEmpty(exe)) return "无法定位本体 exe 路径";
            if (!Directory.Exists(desktopDir))
            {
                try { Directory.CreateDirectory(desktopDir); } catch { }
                if (!Directory.Exists(desktopDir)) return "桌面目录不可用：" + desktopDir;
            }
            string lnk = Path.Combine(desktopDir, "DeepSeek Harness Toolkit.lnk");
            // COM WScript.Shell 创建 .lnk（.NET 4.x 无内置 .lnk 写入 API；WScript.Shell 为 Windows 自带组件）
            Type wsType = Type.GetTypeFromProgID("WScript.Shell");
            if (wsType == null) return "WScript.Shell 组件不可用";
            object shell = Activator.CreateInstance(wsType);
            object sc = wsType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
            Type scType = sc.GetType();
            scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { exe });
            scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(exe) });
            scType.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { "DeepSeek Harness Toolkit" });
            scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            return File.Exists(lnk) ? null : "快捷方式文件未生成";
        }
        catch (Exception ex) { LogErr("CreateDesktopShortcut: " + ex.Message); return ex.Message; }
    }

    /// <summary>当前用户桌面目录（SpecialFolder.DesktopDirectory，重定向到 OneDrive 桌面也生效）。
    /// 测试隔离：设 DSH_TEST_DESKTOP 环境变量时改用该目录（仅测试用，不设置则无影响）。</summary>
    static string DesktopDir()
    {
        try
        {
            string test = Environment.GetEnvironmentVariable("DSH_TEST_DESKTOP");
            if (!string.IsNullOrWhiteSpace(test)) return test;
            return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }
        catch { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop"); }
    }

    /// <summary>非交互创建桌面快捷方式（CLI shortcut 用）：成功输出 SHORTCUT_OK，失败 SHORTCUT_FAIL 原因。</summary>
    static void ShortcutCli()
    {
        string err = CreateDesktopShortcut(DesktopDir());
        if (err == null) { Console.WriteLine("SHORTCUT_OK " + Path.Combine(DesktopDir(), "DeepSeek Harness Toolkit.lnk")); return; }
        Console.WriteLine("SHORTCUT_FAIL " + err);
        Environment.Exit(1);
    }

    /// <summary>桌面快捷方式是否已存在（监控页条件显示 I 选项）。</summary>
    static bool ShortcutExists()
    {
        return File.Exists(Path.Combine(DesktopDir(), "DeepSeek Harness Toolkit.lnk"));
    }

    // ---------------- 非交互 CLI（GUI 集成地基：单行机器可读标记，不 Pause、不读输入） ----------------

    /// <summary>非交互备份数据目录：输出 BACKUP_OK &lt;路径&gt; / BACKUP_FAIL &lt;原因&gt;。不附加工交互工作区。</summary>
    static void NIBackup()
    {
        string src = DataRoot();
        if (!Directory.Exists(src)) { Console.WriteLine("BACKUP_FAIL " + T("数据目录不存在：" + src, "data dir not found: " + src)); return; }
        string bk = DoBackup(src, null, BackupKind.Manual);
        if (bk != null) Console.WriteLine("BACKUP_OK " + bk);
        else Console.WriteLine("BACKUP_FAIL " + T("备份失败（见 launcher.log）", "backup failed (see launcher.log)"));
    }

    /// <summary>非交互恢复最新有效备份：输出 RESTORE_OK &lt;路径&gt; / RESTORE_FAIL &lt;原因&gt;。
    /// 沿用交互版安全顺序：运行中拒绝 + 恢复前自动备份 + 目标仅默认工作区（不询问）。</summary>
    static void NIRestore()
    {
        string root = BackupsRoot();
        if (!Directory.Exists(root)) { Console.WriteLine("RESTORE_FAIL " + T("没有备份", "no backups")); return; }
        string[] dirs = Directory.GetDirectories(root, "dsh-data-*");
        Array.Sort(dirs);
        Array.Reverse(dirs);
        string bk = null;
        foreach (string d in dirs) { if (IsValidBackupDir(d)) { bk = d; break; } }   // 最新有效备份
        if (bk == null) { Console.WriteLine("RESTORE_FAIL " + T("无有效备份", "no valid backup")); return; }
        if (ProbeService() != ServiceState.Down) { Console.WriteLine("RESTORE_FAIL " + T("dsh 正在运行，无法恢复", "dsh is running; cannot restore")); return; }
        string dst = DataRoot();
        if (Directory.Exists(dst))
        {
            string preBk = DoBackup(dst, null, BackupKind.PreRestore);
            if (preBk == null) { Console.WriteLine("RESTORE_FAIL " + T("恢复前自动备份失败", "pre-restore backup failed")); return; }
        }
        inputEof = true;   // 非交互：工作区恢复走默认目标，任何 ReadLineTrim 立即返回 ""（不阻塞 stdin）
        RestoreFromSource(bk);
        Console.WriteLine("RESTORE_OK " + bk);
    }

    /// <summary>非交互服务三态：输出 STATUS_UP / STATUS_STARTING / STATUS_DOWN。</summary>
    static void StatusCli()
    {
        ServiceState st = ProbeService();
        if (st == ServiceState.Ready) Console.WriteLine("STATUS_UP");
        else if (st == ServiceState.Listening) Console.WriteLine("STATUS_STARTING");
        else Console.WriteLine("STATUS_DOWN");
    }

    // ---------------- dsh 更新管理 ----------------

    /// <summary>校验并清洗 npm view 返回的最新版本：整串必须通过严格白名单（数字核心段 + 可选预发布段），
    /// 防止恶意 registry 返回带命令字符（空格/&/;/|/&gt;/&lt; 等）的版本串导致注入；非法/垃圾返回 null。</summary>
    static string SanitizeLatestVersion(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string v = raw.Trim();
        if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase)) v = v.Substring(1);
        return IsValidNpmVersion(v) ? v : null;
    }

    /// <summary>查询 npm 上 @deepseek-ai/dsh 的最新版本；失败/离线/版本非法返回 null。</summary>
    static string GetLatestDshVersion()
    {
        return SanitizeLatestVersion(RunCapture("cmd.exe", "/c npm view @deepseek-ai/dsh version 2>nul"));
    }

    /// <summary>解析 npm 版本输出为字符串数组（容错单行数组 / JSON 多行格式）。</summary>
    static string[] ParseNpmVersions(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new string[0];
        string s = raw.Trim();
        if (s.StartsWith("[")) s = s.Substring(1);
        if (s.EndsWith("]")) s = s.Substring(0, s.Length - 1);
        string[] parts = s.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        var list = new List<string>();
        foreach (string p in parts)
        {
            string v = p.Trim().Trim('\'', '"');
            if (v.Length > 0) list.Add(v);
        }
        return list.ToArray();
    }

    /// <summary>过滤干净的版本串（严格白名单：数字核心段 + 可选预发布段，防注入），升序后取最近 n 个并倒序（最新在前）。</summary>
    static string[] FilterVersions(string[] all, int n)
    {
        var clean = new List<string>();
        foreach (string v in all)
        {
            if (!IsValidNpmVersion(v)) continue;   // 严格整串校验（预发布段仅字母数字与 .-，拒绝任何命令字符）
            clean.Add(v);
        }
        clean.Sort(CompareVersions);
        var recent = new List<string>();
        for (int i = Math.Max(0, clean.Count - n); i < clean.Count; i++) recent.Add(clean[i]);
        recent.Reverse();
        return recent.ToArray();
    }

    /// <summary>是否为 数字[.数字[.数字]] 的干净版本串。</summary>
    static bool IsCleanVersion(string v)
    {
        if (v.Length == 0) return false;
        string[] seg = v.Split('.');
        if (seg.Length < 1 || seg.Length > 3) return false;
        foreach (string s in seg)
        {
            if (s.Length == 0) return false;
            for (int i = 0; i < s.Length; i++)
                if (s[i] < '0' || s[i] > '9') return false;
        }
        return true;
    }

    /// <summary>严格 npm 版本白名单：核心段数字 . 分隔（1-3 段），允许一个 - 预发布段，其字符仅限 [0-9A-Za-z.-]。
    /// 任何空格/&amp; /; /| /&gt; /&lt; /$ /引号等命令注入字符一律拒绝；调用方可放心的用它拼进命令行。</summary>
    static bool IsValidNpmVersion(string v)
    {
        if (string.IsNullOrWhiteSpace(v)) return false;
        if (!IsCleanVersion(CoreVersion(v))) return false;
        int d = v.IndexOf('-');
        if (d < 0) return true;                       // 无预发布段：核心段已是白名单（IsCleanVersion 只含数字与点）
        if (v.IndexOf('-', d + 1) >= 0) return false; // 只允许一个 -
        string pre = v.Substring(d + 1);
        if (pre.Length == 0) return false;
        foreach (char c in pre)
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '.' || c == '-'))
                return false;
        return true;
    }

    /// <summary>显示历史版本列表（0/回车=取消），返回用户选中的版本；取消返回 null。本机装过的版本带 * 标记。</summary>
    static string ListDshVersions()
    {
        string raw = RunCapture("cmd.exe", "/c npm view @deepseek-ai/dsh versions 2>nul");
        string[] recent = FilterVersions(ParseNpmVersions(raw), 10);
        if (recent.Length == 0)
        {
            Warn(T("无法获取版本列表（离线或源不可用）。", "Cannot get version list (offline or registry unavailable)."));
            return null;
        }
        Console.WriteLine();
        CL(ConsoleColor.White, T("  可选版本（* = 本机安装过）：", "  Available versions (* = installed before):"));
        for (int i = 0; i < recent.Length; i++)
            CL(ConsoleColor.White, "  " + (i + 1) + ") v" + recent[i] + (HasDshVersion(recent[i]) ? " *" : ""));
        CL(ConsoleColor.Gray, "  0) " + T("取消", "Cancel"));
        Console.Write("  > ");
        string sel = ReadLineTrim().Trim();
        int idx;
        if (int.TryParse(sel, out idx) && idx >= 1 && idx <= recent.Length) return recent[idx - 1];
        return null;
    }

    /// <summary>记录本机装过的 dsh 版本（去重、最新在前、最多 10 个）。</summary>
    static void RecordDshVersion(string ver)
    {
        ver = ver.Trim().TrimStart('v', 'V');
        // L-8：过滤逗号/控制符——逗号会污染历史列表的逗号分隔解析，控制符会污染 config 与后续展示
        ver = ver.Replace(",", "");
        var sb = new StringBuilder();
        foreach (char c in ver)
            if (c >= ' ' && c != '\x7f') sb.Append(c);   // 仅保留可打印非控制字符
        ver = sb.ToString();
        if (ver.Length == 0) return;
        var list = new List<string>();
        if (cfgDshVersions.Length > 0)
            list.AddRange(cfgDshVersions.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        list.RemoveAll(x => x.Trim().Equals(ver, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, ver);
        while (list.Count > 10) list.RemoveAt(list.Count - 1);
        cfgDshVersions = string.Join(",", list.ToArray());
        SaveConfig();
    }

    /// <summary>历史列表中是否含指定版本。</summary>
    static bool HasDshVersion(string ver)
    {
        if (cfgDshVersions.Length == 0) return false;
        foreach (string x in cfgDshVersions.Split(','))
            if (x.Trim().Equals(ver, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>更新/换版本 dsh（菜单 8）。守卫 → 检测 → 选版本 → 双确认 → pre-update 备份 → 安装 → 记忆。</summary>
    static void UpdateDsh()
    {
        Banner();
        if (ProbeService() != ServiceState.Down)   // 守卫与备份/恢复/导入一致：启动中（Listening）也拒绝，避免半启动状态文件占用竞态
        {
            Error(T("dsh Web 服务正在运行，请先停止再更新（菜单 2 启动界面中可停止）。", "dsh web is running. Stop it first (from the Start UI screen)."));
            Pause(); return;
        }
        string cur = RunDshVersion();
        if (string.IsNullOrWhiteSpace(cur))
        {
            Warn(T("未检测到已安装的 dsh。请先通过菜单 1 安装。", "No installed dsh detected. Install via menu 1 first."));
            Pause(); return;
        }
        string latest = GetLatestDshVersion();
        if (latest != null)
        {
            if (CompareVersions(cur, latest) >= 0)
                Info(T("当前已是最新版本 v" + cur + "。输入 L 可重装或切换历史版本。", "Already up to date (v" + cur + "). Enter L to reinstall or switch versions."));
            else
                Info(T("发现新版本：当前 v" + cur + " → 最新 v" + latest, "Update available: v" + cur + " -> v" + latest));
        }
        else
        {
            Warn(T("无法获取最新版本（离线或源不可用）。输入 L 可尝试历史版本列表。", "Cannot reach the registry. Enter L to list versions."));
        }
        Console.WriteLine();
        CL(ConsoleColor.White, T("  回车=安装最新版，L=列出历史版本，其他=取消：", "  Enter=install latest, L=list versions, anything else=cancel:"));
        Console.Write("  > ");
        string sel = ReadLineTrim().Trim();
        if (inputEof) { Info(T("已取消。", "Cancelled.")); Pause(); return; }
        string target = null;
        if (sel == "L" || sel == "l")
        {
            target = ListDshVersions();
            if (target == null) { Info(T("已取消。", "Cancelled.")); Pause(); return; }
        }
        else if (sel.Length == 0)
        {
            if (latest == null) { Error(T("无法确定目标版本，请改用 L 手动选择。", "Cannot determine target version; use L to pick one.")); Pause(); return; }
            target = latest;
        }
        else
        {
            Info(T("已取消。", "Cancelled.")); Pause(); return;
        }
        // 双确认：① y/Y ② 输入 update
        Console.WriteLine();
        CL(ConsoleColor.Yellow, T("  ⚠️ 警告：更新可能存在破坏性变更（覆盖当前 dsh 安装；配置/插件可能不兼容）。", "  ⚠️ Warning: this update may be destructive (overwrites the dsh install; config/plugins may be incompatible)."));
        Info(T("  当前 v" + cur + " → 目标 v" + target + "。确认后将先自动备份 ~/.dsh 数据，再执行安装。", "  v" + cur + " -> v" + target + ". Your ~/.dsh data will be backed up first, then install runs."));
        CL(ConsoleColor.White, T("  是否继续？(y/N)：", "  Continue? (y/N): "));
        string a1 = ReadLineTrim().Trim();
        if (a1 != "y" && a1 != "Y") { Info(T("已取消，未做任何更改。", "Cancelled; nothing changed.")); Pause(); return; }
        CL(ConsoleColor.White, T("  请再次确认：输入 update 执行更新（其他键取消）：", "  Confirm again: type update to proceed (anything else cancels): "));
        string a2 = ReadLineTrim().Trim();
        if (a2 != "update") { Info(T("已取消，未做任何更改。", "Cancelled; nothing changed.")); Pause(); return; }
        // pre-update 备份（失败即中止，沿用 v2.1 安全逻辑；数据目录尚未生成时跳过备份——与 wipe 的 L606 判断同构）
        string preBk = null;
        if (Directory.Exists(DataRoot()))
        {
            preBk = DoBackup(DataRoot(), null, BackupKind.PreUpdate);
            if (preBk == null)
            {
                Error(T("更新前自动备份失败，已中止更新（请先手动备份或检查磁盘空间）。", "Pre-update backup failed; update aborted (back up manually or check disk space first)."));
                Pause(); return;
            }
            try { File.WriteAllText(Path.Combine(preBk, "version.txt"), cur, new UTF8Encoding(false)); } catch { }   // 记录旧版本号供回滚
            Info(T("已自动备份：" + preBk, "Auto backup: " + preBk));
        }
        else Info(T("无数据目录，跳过更新前备份。", "No data directory; skipping pre-update backup."));
        // 执行安装
        string[] regs = new string[] { NPM_OFFICIAL, NPM_MIRROR };
        int code = NpmInstallDsh(target, regs);
        if (code == 0)
        {
            string nv = RunDshVersion();
            // M-2：安装退出码 0 不算完，必须验证实际版本 == 目标版本（nv 为空或不等都判失败并走回滚）
            string nvClean = SanitizeLatestVersion(nv);   // 去掉 v 前缀/脏字符，非法返回 null
            bool ok = !string.IsNullOrWhiteSpace(nvClean) && CompareVersions(nvClean, target) == 0;
            if (ok)
            {
                Success(T("更新成功！已安装 v" + nvClean, "Updated! Now on v" + nvClean));
                RecordDshVersion(nvClean);
                Info(T("数据已备份于 " + (preBk ?? T("（本次无数据目录，未备份）", "(no data dir this run, not backed up)")), "Data backed up at " + (preBk ?? "(no data dir this run, not backed up)")));
            }
            else
            {
                string reason = string.IsNullOrWhiteSpace(nv)
                    ? T("安装返回成功但无法读取新版本号", "install returned success but the new version could not be read")
                    : T("安装返回成功但版本不符（期望 v" + target + "，实际 " + (nvClean ?? nv) + "）", "install returned success but version mismatch (expected v" + target + ", got " + (nvClean ?? nv) + ")");
                Error(T("更新验证失败：" + reason, "Update verification failed: " + reason));
                RollbackUpdate(cur, preBk);
            }
        }
        else
        {
            Error(T("更新失败（退出码 " + code + "）。", "Update failed (exit code " + code + ")."));
            RollbackUpdate(cur, preBk);
        }
        Pause();
    }

    /// <summary>更新失败后自动回滚到旧版本 cur 并验证；回滚失败给出明确手动指引 + 备份位置（M-1）。</summary>
    static void RollbackUpdate(string cur, string preBk)
    {
        Info(T("正在自动回滚到 v" + cur + " ...", "Auto-rolling back to v" + cur + " ..."));
        string[] regs = new string[] { NPM_OFFICIAL, NPM_MIRROR };
        int rc = NpmInstallDsh(cur, regs);
        string rv = rc == 0 ? RunDshVersion() : null;
        string rvClean = SanitizeLatestVersion(rv);
        bool rolledBack = rc == 0 && !string.IsNullOrWhiteSpace(rvClean) && CompareVersions(rvClean, cur) == 0;
        if (rolledBack)
        {
            Success(T("已回滚到 v" + cur, "Rolled back to v" + cur));
        }
        else
        {
            Error(T("自动回滚失败。请手动执行：npm install -g @deepseek-ai/dsh@" + cur,
                    "Auto-rollback failed. Manually run: npm install -g @deepseek-ai/dsh@" + cur));
        }
        if (!string.IsNullOrWhiteSpace(preBk))
            Info(T("数据已备份于：" + preBk, "Data backed up at: " + preBk));
    }

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
            if (cfgCheckDshUpdate)
            {
                string latest = GetLatestDshVersion();
                C(ConsoleColor.Gray, "  dsh 最新   : ");
                if (string.IsNullOrEmpty(latest)) CL(ConsoleColor.Gray, T("（离线，未获取）", "(offline, n/a)"));
                else if (string.IsNullOrWhiteSpace(v) || CompareVersions(v, latest) < 0)
                    CL(ConsoleColor.Yellow, latest + (string.IsNullOrWhiteSpace(v) ? "" : T("（当前 " + v + "，有更新）", " (current " + v + ", update available)")));
                else CL(ConsoleColor.White, latest + T("（已是最新）", " (up to date)"));
            }
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
        Console.WriteLine("  DeepSeek Harness Toolkit v" + CurrentVersion() + " install | start [--bg] | stop | uninstall | update | check | backup | restore | status | about | shortcut | help");
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
        public static string[] ParseVersions(string raw) { return Program.ParseNpmVersions(raw); }
        public static string[] FilterVers(string[] all, int n) { return Program.FilterVersions(all, n); }
        public static bool CleanVer(string v) { return Program.IsCleanVersion(v); }
        public static string SanitizeLatest(string raw) { return Program.SanitizeLatestVersion(raw); }
        public static string DshVersions() { return Program.cfgDshVersions; }
        public static void RecordVer(string v) { Program.RecordDshVersion(v); }
        public static void ResetVersions() { Program.cfgDshVersions = ""; }
        public static string DoBackup(string src) { return Program.DoBackup(src); }
        public static string DoBackupKind(string src, BackupKind k) { return Program.DoBackup(src, null, k); }
        public static bool RootMarker(string dir) { return Program.RootMarkerValid(dir); }
        public static bool ValidBackup(string dir) { return Program.IsValidBackupDir(dir); }
        public static string ResolveBackup(string dir) { return Program.ResolveBackupDir(dir); }
        public static string Shortcut(string desktopDir) { return Program.CreateDesktopShortcut(desktopDir); }
        public static string Desktop(string dir) { string old = Environment.GetEnvironmentVariable("DSH_TEST_DESKTOP"); try { Environment.SetEnvironmentVariable("DSH_TEST_DESKTOP", dir); return Program.DesktopDir(); } finally { Environment.SetEnvironmentVariable("DSH_TEST_DESKTOP", old); } }
        public static bool ShortcutExists(string dir) { string old = Environment.GetEnvironmentVariable("DSH_TEST_DESKTOP"); try { Environment.SetEnvironmentVariable("DSH_TEST_DESKTOP", dir); return Program.ShortcutExists(); } finally { Environment.SetEnvironmentVariable("DSH_TEST_DESKTOP", old); } }
        public static int ParsePort(string netstat, int port) { return Program.ParsePortPid(netstat, port); }
    }
#endif
}