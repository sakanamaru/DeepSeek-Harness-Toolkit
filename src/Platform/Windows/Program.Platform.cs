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

    static Mutex _singleMutex;         // 单例防多开：交互模式同一时刻只允许一个实例

    static Mutex _legacyMutex;         // 旧 v2.0 锁（已发布的 v2.0 exe 使用该名，保证新旧版本互斥）

    // ---------------- 入口 ----------------


    // ---------------- 防误用提醒：桌面/下载目录直接运行 ----------------
    // 只随交互模式触发（CLI 子命令保持 stdout 纯净，GUI 调用不受影响）
    static void DetectBadDir()
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
                    Warn(T("检测到你在桌面/下载目录直接运行本程序：备份与日志会写到该目录，文件容易被误删或丢失。建议移到独立文件夹（如 D:\\Tools\\DSHToolkit）后运行。",
                           "Running from Desktop/Downloads: backups & logs land there and are easy to lose. Move to a dedicated folder (e.g. D:\\Tools\\DSHToolkit)."));
                    return;
                }
            }
        }
        catch { }
    }


    // 真实"下载"目录：优先注册表 User Shell Folders（支持重定向，如 E:\Downloads），
    // 失败回退 %USERPROFILE%\Downloads。
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

    // ---------------- 语言 ----------------


    static void C(ConsoleColor color, string s)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(s);
        Console.ForegroundColor = prev;
    }


    static void CL(ConsoleColor color, string s) { C(color, s + Environment.NewLine); }

    static void SafeClear() { try { Console.Clear(); } catch { } }

    /// <summary>监听指定端口的进程 PID（netstat -ano -p tcp），找不到/出错返回 0。</summary>
    static int FindPortPid(int port)
    {
        return ParsePortPid(RunCapture("cmd.exe", "/c netstat -ano -p tcp"), port);
    }


    /// <summary>读进程命令行（powershell Get-CimInstance，15 秒超时）；失败返回 ""。</summary>
    static string GetProcessCommandLine(int pid)
    {
        string query = "Get-CimInstance Win32_Process -Filter 'ProcessId=" + pid + "' | Select-Object -ExpandProperty CommandLine";
        return RunCapture("powershell.exe", "-NoProfile -NonInteractive -Command \"" + query + "\"");
    }


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


    /// <summary>纯判定（v2.4.2）：端口开 +（HTTP 2xx/3xx 或 监听进程确为 dsh）→ Ready。
    /// 监听身份用委托惰性求值——HTTP 已就绪时不再多花一次 netstat/WMI。</summary>
    static ServiceState JudgeState3(bool portOpen, bool httpOk, Func<bool> listenerIsDsh)
    {
        if (!portOpen) return ServiceState.Down;
        if (httpOk) return ServiceState.Ready;
        bool byProc = false;
        try { byProc = listenerIsDsh != null && listenerIsDsh(); }
        catch { byProc = false; }
        return byProc ? ServiceState.Ready : ServiceState.Listening;
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


    /// <summary>HTTP 是否有应答（任意状态码，含 401/403/404）——服务活着但要求鉴权时用。</summary>
    static bool HttpResponds(string url, int ms)
    {
        try
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = ms;
            req.AllowAutoRedirect = true;
            using (var resp = (HttpWebResponse)req.GetResponse()) { return true; }
        }
        catch (WebException ex) { return ex.Response is HttpWebResponse; }   // 401/403… 服务器确实在应答
        catch { return false; }
    }


    /// <summary>监听 3080 的进程是否确为 dsh（v2.4.2 新增兜底判定）。
    /// 背景：dsh 0.1.5+ 对未授权请求统一返回 401（本机浏览器靠会话 cookie 才能 200），
    /// 于是 HttpReady 永久为假、服务永远显示"启动中"。这里改用监听进程身份兜底，
    /// 依据与 stop 防护同源（命令行含 dsh）。命令行读不到（权限/超时）时退回
    /// "node 进程 + HTTP 有应答"，仍强于"仅端口开"。
    /// 注意：止于显示与启动判定；真正会杀进程的 stop 仍走严格的 IsOurDshProcess。</summary>
    static bool ListenerIsDsh()
    {
        if (listenerIsDshAt != DateTime.MinValue && (DateTime.Now - listenerIsDshAt).TotalSeconds < 10)
            return listenerIsDshCached;
        bool ok = false;
        try
        {
            int pid = FindPortPid(WEB_PORT);
            if (pid > 0)
            {
                ok = IsDshCommandLine(GetProcessCommandLine(pid));
                if (!ok)
                {
                    string pname = "";
                    try { pname = Process.GetProcessById(pid).ProcessName ?? ""; } catch { }
                    if (pname.IndexOf("node", StringComparison.OrdinalIgnoreCase) >= 0)
                        ok = HttpResponds(WebUrl(), 800);
                }
            }
        }
        catch { ok = false; }
        listenerIsDshCached = ok;
        listenerIsDshAt = DateTime.Now;
        return ok;
    }


    /// <summary>服务三态探测入口：先端口（快速/可打桩），再 HTTP，最后监听进程身份兜底。</summary>
    static ServiceState ProbeService()
    {
        return JudgeState3(IsPortOpen(WEB_PORT, 800), HttpReady(WebUrl(), 800), ListenerIsDsh);
    }

    // ---------------- 自动检查更新（v2.1: 启动静默查询 GitHub Releases，发现新版本才提示） ----------------


    /// <summary>创建桌面快捷方式；返回 null=成功，否则=原因。
    /// v2.7：可指定目标 exe / 基名 / 描述——GUI 调用时指向 GUI 自己的 exe
    /// （此前固定指向核心 exe，导致在 GUI 里点「桌面快捷方式」建出来的是 CLI 的，属错位）。
    /// targetExe 为空时指向本程序；必须是已存在的 .exe 文件。</summary>
    static string CreateDesktopShortcut(string desktopDir, string targetExe, string lnkBase, string desc)
    {
        try
        {
            string exe = string.IsNullOrEmpty(targetExe) ? Assembly.GetExecutingAssembly().Location : targetExe;
            if (string.IsNullOrEmpty(exe)) return "无法定位 exe 路径";
            if (!File.Exists(exe)) return "目标 exe 不存在：" + exe;
            string name = SafeShortcutName(lnkBase);
            if (name.Length == 0) name = SHORTCUT_NAME;
            if (!Directory.Exists(desktopDir))
            {
                try { Directory.CreateDirectory(desktopDir); } catch { }
                if (!Directory.Exists(desktopDir)) return "桌面目录不可用：" + desktopDir;
            }
            string lnk = Path.Combine(desktopDir, name + ".lnk");
            // COM WScript.Shell 创建 .lnk（.NET 4.x 无内置 .lnk 写入 API；WScript.Shell 为 Windows 自带组件）
            Type wsType = Type.GetTypeFromProgID("WScript.Shell");
            if (wsType == null) return "WScript.Shell 组件不可用";
            object shell = Activator.CreateInstance(wsType);
            object sc = wsType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
            Type scType = sc.GetType();
            scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { exe });
            scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(exe) });
            scType.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { string.IsNullOrEmpty(desc) ? name : desc });
            try { scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { exe + ",0" }); } catch { }
            scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            return File.Exists(lnk) ? null : "快捷方式文件未生成";
        }
        catch (Exception ex) { LogErr("CreateDesktopShortcut: " + ex.Message); return ex.Message; }
    }


    /// <summary>默认（指向核心本程序）的桌面快捷方式。</summary>
    static string CreateDesktopShortcut(string desktopDir)
    {
        return CreateDesktopShortcut(desktopDir, null, SHORTCUT_NAME, "DeepSeek Harness Toolkit");
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

}
