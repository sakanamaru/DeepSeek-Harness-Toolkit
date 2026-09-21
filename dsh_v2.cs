// ============================================================================
//  DeepSeek Harness Toolkit V2.7.2  ——  DeepSeek Harness(dsh) 安装 / 启动 / 卸载 / 备份恢复工具箱
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
using System.Text.RegularExpressions;
using System.Threading;

[assembly: AssemblyTitle("DeepSeek Harness Toolkit V2.7.2")]
[assembly: AssemblyDescription("DeepSeek Harness(dsh) 安装/启动/卸载/备份恢复工具箱。v1: SOGR-Momono Dango(QwenPaw/DeepseekAPI-V4-Flash-0731)；v2: DeepSeek DSH(DSH/DeepseekAPI-V4-Flash-0731)；GitHub @sakanamaru")]
[assembly: AssemblyCompany("SOGR-Momono Dango / DeepSeek DSH / @sakanamaru")]
[assembly: AssemblyProduct("DeepSeek Harness Toolkit")]
[assembly: AssemblyVersion("2.7.2.0")]
[assembly: AssemblyFileVersion("2.7.2.0")]

partial class Program
{

    enum Lang { Auto, Zh, En }


    const string NPM_MIRROR   = "https://registry.npmmirror.com";
    const string NPM_OFFICIAL = "https://registry.npmjs.org";
    const string GITHUB_HANDLE = "github.com/sakanamaru";

    const string DATA_DIR     = ".dsh";

    const string ROOT_MARKER  = ".dsh_launcher_root";   // 工具箱根目录标记文件（防误删验证；随包分发）

    const int    WEB_PORT     = 3080;

    static string webHost = "127.0.0.1";

    const int    AUTO_SECONDS = 5;


    static Lang lang = Lang.Auto;

    static string StateDir;

    static bool autoApplied = false;   // 自动倒计时是否已在本程序本次运行中用过

#if !UNIT
    public static void Main(string[] args)
    {
        // .NET Framework 长路径支持：开启后 >260 字符路径可用（须在首次文件操作前设置）
        try { AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false); } catch { }
        try { AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false); } catch { }
        try { Console.OutputEncoding = new UTF8Encoding(false); } catch { }
        try { Console.Title = "DeepSeek Harness Toolkit V2.7.2"; } catch { }
        // v2.7：显式启用 TLS 1.2。.NET Framework 4.x 的 SecurityProtocol 默认只含 Ssl3|Tls，
        // 访问 GitHub HTTPS（更新检查 / 完整性校验）会直接抛"未能创建 SSL/TLS 安全通道"。
        // 只做 |= 追加，不动系统默认值；失败静默（老系统上最差退化为原行为）。
        try { System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072; } catch { }
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
                case "shortcut":  case "sc": ShortcutCli(args); return; // 创建桌面快捷方式（--exe/--name 可指定目标；GUI 用它建自己的）
                case "backup":    case "b": NIBackup();  return;   // 非交互备份（GUI/脚本用）
                case "backup-list": case "bl": NiListBackups(HasFlag(args, "--detail") || HasFlag(args, "-detail")); return;   // 非交互列出有效备份目录（--detail 附类型/大小/时间，GUI 备份管理页用）
                case "backup-export": case "be": NIBackupExport(args); return;   // v2.6：导出备份副本到指定目录
                case "backup-delete": case "bd": NIBackupDelete(args); return;   // v2.6：删除指定备份（仅限备份根内 dsh-data-*）
                case "update-info": case "ui": UpdateInfo(); return;   // v2.7：Update Center 只读数据源
                case "config-get": case "cg": ConfigGet(); return;   // v2.8：设置页只读数据源
                case "config-set": case "cs": ConfigSet(args); return;   // v2.8：白名单配置写入
                case "restore":   case "r":
                    if (HasFlag(args, "--dry-run") || HasFlag(args, "-dry-run")) { NIRestoreDryRun(FlagValue(args, "--path") ?? FlagValue(args, "-path")); return; }   // v2.6 Dry-Run：只读预演
                    if (args.Length > 1 && (args[1] == "--path" || args[1] == "-path"))
                        NIRestorePath(args.Length > 2 ? args[2] : "");
                    else NIRestore();
                    return;   // 非交互恢复（GUI/脚本用；--path 恢复指定备份）
                case "status": StatusCli(HasFlag(args, "--detail") || HasFlag(args, "-detail")); return;   // 服务三态（GUI 状态灯用）；--detail 追加 PID/启动时间/运行时长（v2.7 状态栏，只读）
                case "help":      case "h": Help();    return;
                case "selftest": Selftest(args); return;
                case "doctor":    case "d": Doctor(args); return;   // v2.5：体检/诊断（GUI 体检页用）
                case "profilecheck":  case "pc": ProfileCheckCli(args); return;      // v2.7：profile 静态预检（只读，不用等它崩）
                case "bootdiag":      case "bdiag": BootDiagCli(args); return;      // v2.7：启动失败堆栈解析（只读；"bd" 已被 backup-delete 占用）
                case "profilepatch":  case "pp": ProfilePatchCli(args); return;  // v2.7：受控单行插入修复（需 --yes；先备份可回滚）
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
        DetectBadDir();   // 桌面/下载目录直跑 → 黄字提醒（不阻塞）
        Menu();
    }

#endif


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


    static string WebUrl() { return "http://" + webHost + ":" + WEB_PORT; }
    static void OpenBrowser() { OpenUrl(WebUrl()); }


    static void OpenUrl(string url)
    {
        try { Process.Start(url); }
        catch (Exception ex) { Warn(T("打开失败：" + ex.Message + "（可手动访问 " + url + "）",
                                      "Failed to open: " + ex.Message + " (visit " + url + " manually).")); }
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


    /// <summary>停止前进程归属校验：监听 3080 的进程必须确认为 dsh 才允许终止。
    /// 判定依据：进程命令行含 "dsh"（dsh.cmd / npm / node 启动链命令行必含 dsh 字样，如 @deepseek-ai\dsh）；
    /// 命令行读取失败时保守拒绝——宁可不杀，不可误杀他人程序。</summary>
    static bool IsOurDshProcess(int pid)
    {
        if (pid <= 0) return false;
        try { return IsDshCommandLine(GetProcessCommandLine(pid)); }
        catch { return false; }
    }


    /// <summary>纯函数（可单测）：命令行是否属于 dsh 进程。空/未知 → false。</summary>
    static bool IsDshCommandLine(string cmdline)
    {
        if (string.IsNullOrWhiteSpace(cmdline)) return false;
        return cmdline.IndexOf("dsh", StringComparison.OrdinalIgnoreCase) >= 0;
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


    public enum ServiceState { Down, Listening, Ready }


    /// <summary>纯判定：端口开 + HTTP 就绪 → Ready；仅端口开 → Listening；否则 Down。（单测可直接调用）</summary>
    static ServiceState JudgeState(bool portOpen, bool httpOk)
    {
        if (!portOpen) return ServiceState.Down;
        return httpOk ? ServiceState.Ready : ServiceState.Listening;
    }


    // 监听进程身份判定缓存（同一进程内 10 秒内复用）：状态页每 3 秒刷新一次，
    // 不做缓存就会每轮都拉起 netstat + WMI。
    static bool listenerIsDshCached = false;

    static DateTime listenerIsDshAt = DateTime.MinValue;


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


    static bool inputEof = false;

    /// <summary>核心（CLI）桌面快捷方式基名。</summary>
    const string SHORTCUT_NAME = "DeepSeek Harness Toolkit";


    /// <summary>桌面快捷方式是否已存在（监控页条件显示 I 选项）。</summary>
    static bool ShortcutExists()
    {
        return File.Exists(Path.Combine(DesktopDir(), SHORTCUT_NAME + ".lnk"));
    }

    // ---------------- 非交互 CLI（GUI 集成地基：单行机器可读标记，不 Pause、不读输入） ----------------


    /// <summary>restore --path 路径校验（纯逻辑，供单测）；返回 null=通过，否则失败原因键（no-path/outside/invalid）。</summary>
    static string NIValidateRestorePath(string pathArg, string backupsRoot)
    {
        string bk = (pathArg ?? "").Trim().Trim('"');
        if (bk.Length == 0) return "no-path";
        if (!IsSubPath(backupsRoot, bk)) return "outside";
        if (!IsValidBackupDir(bk)) return "invalid";
        return null;
    }


    /// <summary>非交互列出全部有效备份目录（最新在前）：首行 BACKUP_LIST_OK &lt;n&gt;，其后每行一个绝对路径。
    /// 无备份/全部无效时输出 BACKUP_LIST_OK 0（GUI 选择框据此判空）。</summary>
    static void NiListBackups(bool detail)
    {
        string root = BackupsRoot();
        if (!Directory.Exists(root)) { Console.WriteLine("BACKUP_LIST_OK 0"); return; }
        string[] dirs;
        try { dirs = Directory.GetDirectories(root, "dsh-data-*"); }
        catch { Console.WriteLine("BACKUP_LIST_OK 0"); return; }
        Array.Sort(dirs);       // 时间戳升序
        Array.Reverse(dirs);    // 最新在前（GUI 默认选第一项）
        List<string> valid = new List<string>();
        foreach (string d in dirs) { if (IsValidBackupDir(d)) valid.Add(d); }
        Console.WriteLine("BACKUP_LIST_OK " + valid.Count);
        foreach (string d in valid)
        {
            Console.WriteLine(d);
            if (detail)
            {
                string name = Path.GetFileName(d);
                long bytes = DirSize(d);
                string mt = "(unknown)";
                try { mt = Directory.GetLastWriteTime(d).ToString("yyyy-MM-dd HH:mm:ss"); } catch { }
                Console.WriteLine("BACKUP_ITEM " + name + " " + BackupKindName(name) + " " + bytes + " " + mt);
            }
        }
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


    /// <summary>配置摘要：launcher.config 的非注释行。</summary>
    static string ConfigSummary()
    {
        try
        {
            if (!File.Exists(ConfigPath())) return "(无配置文件)";
            var kept = new List<string>();
            foreach (string l in File.ReadAllLines(ConfigPath()))
            {
                string t = l.Trim();
                if (t.Length == 0 || t.StartsWith("#")) continue;
                kept.Add(t);
            }
            return kept.Count == 0 ? "(空配置)" : string.Join(" ; ", kept.ToArray());
        }
        catch (Exception ex) { return "读取失败: " + ex.Message; }
    }


    /// <summary>日志摘要：launcher.log 行数 + 最近 3 行。</summary>
    static string LogSummary()
    {
        string file = Path.Combine(StateDir, "logs", "launcher.log");
        try
        {
            if (!File.Exists(file)) return "(无日志)";
            string[] lines = File.ReadAllLines(file);
            string tail = "";
            for (int i = Math.Max(0, lines.Length - 3); i < lines.Length; i++)
                tail += (tail.Length == 0 ? "" : " | ") + lines[i];
            return "共 " + lines.Length + " 行；最近: " + tail;
        }
        catch (Exception ex) { return "读取失败: " + ex.Message; }
    }


    static bool HasFlag(string[] args, string f) { foreach (string a in args) if (string.Equals(a, f, StringComparison.OrdinalIgnoreCase)) return true; return false; }

    static string FlagValue(string[] args, string f) { for (int i = 1; i < args.Length - 1; i++) if (string.Equals(args[i], f, StringComparison.OrdinalIgnoreCase)) return args[i + 1]; return null; }


    static void WalkFiles(string root, string dir, Dictionary<string, long> acc, bool copyRules)
    {
        string[] subs;
        try { subs = Directory.GetDirectories(P(dir)); } catch { subs = new string[0]; }
        foreach (string d in subs)
        {
            if (copyRules) { if (CopySkipDir(TrimP(d), Path.GetFileName(TrimP(d)))) continue; }
            else { if (IsReparse(d)) continue; }
            WalkFiles(root, d, acc, copyRules);
        }
        string[] files;
        try { files = Directory.GetFiles(P(dir)); } catch { files = new string[0]; }
        string rootPrefix = P(root);
        foreach (string f in files)
        {
            try
            {
                if (IsReparse(f)) continue;
                var fi = new FileInfo(P(f));
                string rel = fi.FullName.Length > rootPrefix.Length ? fi.FullName.Substring(rootPrefix.Length).TrimStart('\\', '/') : fi.Name;
                acc[rel] = fi.Length;
            }
            catch { }
        }
    }


    /// <summary>export 校验（供单测）：返回 null=通过，否则原因键（no-path/no-to/outside/not-found/bad-target/nested）。</summary>
    static string NIValidateExport(string srcArg, string toArg, string backupsRoot)
    {
        string src = (srcArg ?? "").Trim().Trim('"');
        string to = (toArg ?? "").Trim().Trim('"');
        if (src.Length == 0) return "no-path";
        if (to.Length == 0) return "no-to";
        if (!IsSubPath(backupsRoot, src)) return "outside";
        if (!Directory.Exists(src)) return "not-found";
        string dst;
        try { dst = Path.GetFullPath(to); } catch { return "bad-target"; }
        if (string.Equals(dst, src, StringComparison.OrdinalIgnoreCase) || IsSubPath(src, dst)) return "nested";
        return null;
    }


    /// <summary>backup-delete 校验（供单测）：返回 null=通过，否则原因键（no-path/outside/not-backup/not-found）。</summary>
    static string NIValidateBackupDelete(string srcArg, string backupsRoot)
    {
        string src = (srcArg ?? "").Trim().Trim('"');
        if (src.Length == 0) return "no-path";
        if (!IsSubPath(backupsRoot, src)) return "outside";
        string name = Path.GetFileName(src.TrimEnd('\\', '/'));
        if (!name.StartsWith("dsh-data-", StringComparison.OrdinalIgnoreCase)) return "not-backup";
        if (!Directory.Exists(src)) return "not-found";
        return null;
    }


    /// <summary>npm versions 列表里最后一个 -rc 版本号（发布序）；无/离线返回 null。</summary>
    static string GetLatestRcVersion()
    {
        string raw = RunCapture("cmd.exe", "/c npm view @deepseek-ai/dsh versions 2>nul");
        string[] all = ParseNpmVersions(raw);
        string last = null;
        foreach (string v in all) if (v.IndexOf("-rc", StringComparison.OrdinalIgnoreCase) >= 0) last = v;
        return last;
    }


    /// <summary>高风险操作闸门（卸载含清数据 / 恢复 / 更新 dsh）：完整性不匹配即拒绝；无 manifest 时放行。返回是否可继续。</summary>
    static bool IntegrityGate(string opZh, string opEn)
    {
        bool? ok = SelfIntegrity();
        if (ok != false) return true;
        Console.WriteLine(T("自身完整性校验失败：当前程序与随包 hashes.txt 不匹配（可能被篡改）。已拒绝执行「" + opZh + "」。请从官方 Releases 重新下载。",
                            "Self-integrity FAILED: this executable does not match the shipped hashes.txt (possible tampering). '" + opEn + "' refused. Re-download from the official Releases."));
        LogErr("IntegrityGate refused: " + opEn);
        return false;
    }

    // ---------------- 自检 ----------------


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
        public static ServiceState JudgeState3(bool port, bool http, Func<bool> listener) { return Program.JudgeState3(port, http, listener); }
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
        public static string ShortcutT(string desktopDir, string exe, string name, string desc) { return Program.CreateDesktopShortcut(desktopDir, exe, name, desc); }
        public static string ShortcutNameT(string raw) { return Program.SafeShortcutName(raw); }
        public static string Desktop(string dir) { string old = Environment.GetEnvironmentVariable("DSH_TEST_DESKTOP"); try { Environment.SetEnvironmentVariable("DSH_TEST_DESKTOP", dir); return Program.DesktopDir(); } finally { Environment.SetEnvironmentVariable("DSH_TEST_DESKTOP", old); } }
        public static bool ShortcutExists(string dir) { string old = Environment.GetEnvironmentVariable("DSH_TEST_DESKTOP"); try { Environment.SetEnvironmentVariable("DSH_TEST_DESKTOP", dir); return Program.ShortcutExists(); } finally { Environment.SetEnvironmentVariable("DSH_TEST_DESKTOP", old); } }
        public static int ParsePort(string netstat, int port) { return Program.ParsePortPid(netstat, port); }
        public static string NIValidateRestorePath(string pathArg, string backupsRoot) { return Program.NIValidateRestorePath(pathArg, backupsRoot); }
        public static bool IsDshCmd(string cmdline) { return Program.IsDshCommandLine(cmdline); }
        // ---- v2.5 doctor 体检 ----
        public static string Sand(string s) { return Program.SanitizeForReport(s); }
        public static DocItem DI(string cat, int level, string text) { return new DocItem(cat, level, text); }
        public static string DocSum(List<DocItem> items) { return Program.DoctorSummary(items); }
        public static long DirSizeOf(string d) { return Program.DirSize(d); }
        public static string HumanOf(long b) { return Program.HumanSize(b); }
        public static string DocLvl(int level) { return Program.DocLevel(level); }
        public static string ManHash(string manifest, string name) { return Program.ParseManifestHash(manifest, name); }
        public static bool? SelfInteg() { return Program.SelfIntegrity(); }
        public static long[] PlanMergeT(string src, string dst, string skipDir, string skipFile) { return Program.PlanMerge(src, dst, skipDir, skipFile); }
        public static long[] PlanMergeLegacyT(string src, string dst) { return Program.PlanMergeLegacy(src, dst); }
        public static long[] PlanDeleteT(string root) { return Program.PlanDelete(root); }
        public static string KindName(string n) { return Program.BackupKindName(n); }
        public static string ValExport(string src, string to, string root) { return Program.NIValidateExport(src, to, root); }
        public static string ValBkDel(string src, string root) { return Program.NIValidateBackupDelete(src, root); }
        public static string LatestBk(string suffix) { return Program.LatestBackupWithSuffix(suffix); }
        public static int CountBk() { return Program.CountValidBackups(); }
        public static string ValCfg(string key, string value) { return Program.NIValidateConfigSet(key, value); }
        public static string UptimeT(double seconds) { return Program.FormatUptime(TimeSpan.FromSeconds(seconds)); }
        // ---- v2.7 profile 诊断与修复 ----
        public static string[] ProfChkT(string text)
        {
            List<string> r = new List<string>();
            foreach (Program.ProfileFinding f in Program.ProfileCheckText(text, "T")) r.Add(f.Line + "|" + f.Id + "|" + f.Missing);
            return r.ToArray();
        }
        public static string[] FileUrlT(string url) { string frag; string p = Program.FileUrlToPath(url, out frag); return new string[] { p, frag }; }
        public static string[] BootDiagT(string text)
        {
            Program.BootDiagResult r = Program.BootDiagText(text);
            return new string[] { r.Recognized ? "OK" : "FAIL", r.Kind, r.Plugin, r.Entry, r.File, r.Line.ToString(), r.Hint, r.FirstError };
        }
        public static string[] PatchT(string text, string id, string key, string value)
        {
            Program.ProfilePatchPlan p = Program.PlanProfilePatch(text, id, key, value);
            return new string[] { p.Noop ? "NOOP" : (p.InsertAt < 0 ? "FAIL:" + p.Reason : "PLAN"), p.Line.ToString(), p.Indent, p.NewText == null ? "" : p.NewText };
        }
        public static string PatchApplyT(string file, string id, bool simulateFail, out string bk) { return Program.ProfilePatchApply(file, id, "maxDepth", "provider-managed", simulateFail, out bk); }
        // ---- v2.7.2 隔离处方 ----
        public static string[] PatchDisableT(string text, string id)
        {
            Program.ProfilePatchPlan p = Program.PlanProfileDisable(text, id);
            return new string[] { p.Noop ? "NOOP" : (p.InsertAt < 0 ? "FAIL:" + p.Reason : "PLAN"), p.Line.ToString(), p.NewText == null ? "" : p.NewText };
        }
        public static string PatchDisableApplyT(string file, string id, bool simulateFail, out string bk) { return Program.ProfilePatchDisableApply(file, id, simulateFail, out bk); }
        public static bool PatchHasDisabledT(string text, string id) { return Program.PatchHasDisabled(text, id); }
        public static bool SafePatchIdT(string id) { return Program.IsSafePatchId(id); }
        public static string CleanScalarT(string v) { return Program.CleanYamlScalar(v); }
    }

#endif
}
