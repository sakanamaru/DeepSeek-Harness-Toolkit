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

    static void Banner()
    {
        CL(ConsoleColor.Cyan,   "==============================================");
        CL(ConsoleColor.Cyan,   "  DeepSeek Harness Toolkit V2.7.2");
        CL(ConsoleColor.Cyan,   "==============================================");
        C(ConsoleColor.Gray,    "  v1 脚本协助 : "); CL(ConsoleColor.White, "SOGR-Momono Dango（QwenPaw/DeepseekAPI-V4-Flash-0731）");
        C(ConsoleColor.Gray,    "  v2 重构封装 : "); CL(ConsoleColor.White, "DeepSeek DSH （DSH/DeepseekAPI-V4-Flash-0731）");
        C(ConsoleColor.Gray,    "  GitHub    : "); CL(ConsoleColor.White, "@sakanamaru  https://" + GITHUB_HANDLE);
        CL(ConsoleColor.DarkGray, "----------------------------------------------");
        CL(ConsoleColor.DarkYellow, "  " + T("⚠ 非官方工具，由社区独立开发，与 DeepSeek 官方无关。", "⚠ Unofficial community tool, not affiliated with DeepSeek."));
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
            bool countdown = installed && cfgAutoStart && !autoApplied;   // v2.7：auto_start=off 时彻底不进入倒计时
            if (countdown)
            {
                Info(T("检测到 dsh 已安装，5 秒后将自动【启动 Web 界面】（按任意键可手动选择）",
                       "dsh detected. Auto-running【Start Web UI】in 5s (press any key to choose manually)."));
            }
            else if (!autoApplied)
            {
                if (!installed)
                    CL(ConsoleColor.Gray, T("  dsh 未安装：按 1 开始安装（默认官方源），其余操作可正常使用。",
                                            "  dsh not installed: press 1 to install (official registry default); other actions still work."));
                else
                    CL(ConsoleColor.Gray, T("  自动启动已关闭（配置 auto_start=off）：请手动选择。",
                                            "  Auto-start disabled (auto_start=off): please choose manually."));
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

            string choice = countdown ? CountdownInput("  > ", def) : ReadChoice("  > ");
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


    /// <summary>非交互停止 dsh web：找监听 3080 的进程树并终止，验证端口释放。
    /// 输出 STOP_OK / STOP_FAIL &lt;原因&gt;；已停止时同样 STOP_OK（幂等）。不 Pause、不读输入。
    /// 安全：终止前校验监听进程确为 dsh（命令行含 dsh），其他程序占用 3080 时拒绝停止，避免误杀。</summary>
    static void StopCli()
    {
        if (ProbeService() == ServiceState.Down) { Console.WriteLine("STOP_OK"); return; }   // 已停止 → 幂等成功
        int pid = FindPortPid(WEB_PORT);
        if (pid <= 0) { Console.WriteLine("STOP_FAIL " + T("未找到监听 3080 的进程", "no process listening on 3080")); return; }
        if (!IsOurDshProcess(pid))
        {
            Console.WriteLine("STOP_FAIL " + T("3080 被其他程序占用（未确认是 dsh），已拒绝停止以避免误杀", "port 3080 is held by another program (not confirmed as dsh); stop refused to avoid killing it"));
            return;
        }
        // TOCTOU 缓解：终止前最后一刻再次校验监听进程身份（防 pid 复用/竞态窗口误杀）
        if (!IsOurDshProcess(pid))
        {
            Console.WriteLine("STOP_FAIL " + T("复检发现 3080 监听进程已变化（不再确认为 dsh），已拒绝停止", "re-check: listener on 3080 changed (no longer confirmed as dsh); stop refused"));
            return;
        }
        KillProcessTree(pid);   // 进程树终止：连带杀派生的 node 子进程
        for (int i = 0; i < 20; i++)
        {
            Thread.Sleep(250);
            if (ProbeService() == ServiceState.Down) { Console.WriteLine("STOP_OK"); return; }
        }
        Console.WriteLine("STOP_FAIL " + T("端口未释放", "port still in use"));
    }


    static void Uninstall()
    {
        if (!IntegrityGate("卸载（含清除数据）", "uninstall/wipe")) return;
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

        // v2.6 Dry-Run：两步确认前先只读预演"将删多少"（不写任何东西；删除前仍会先自动备份）
        string wipeTarget = DataRoot();
        if (Directory.Exists(wipeTarget))
        {
            long[] wplan = PlanDelete(wipeTarget);
            Warn(T("  Dry-Run 预演：将删除 " + wplan[0] + " 个文件、" + wplan[1] + " 个目录，共 " + HumanSize(wplan[2]) + "（删除前会先自动备份）。",
                   "  Dry-Run preview: will DELETE " + wplan[0] + " files in " + wplan[1] + " directories, " + HumanSize(wplan[2]) + " total (an automatic backup is made first)."));
        }

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


    /// <summary>非交互创建桌面快捷方式（CLI shortcut 用）：成功输出 SHORTCUT_OK，失败 SHORTCUT_FAIL 原因。
    /// v2.7：支持 --exe &lt;目标 exe&gt; / --name &lt;基名&gt; / --desc &lt;描述&gt;（GUI 传自己的 exe，避免建成 CLI 的）。</summary>
    static void ShortcutCli(string[] args)
    {
        string exe = FlagValue(args, "--exe") ?? FlagValue(args, "-exe");
        string name = FlagValue(args, "--name") ?? FlagValue(args, "-name");
        string desc = FlagValue(args, "--desc") ?? FlagValue(args, "-desc");
        if (string.IsNullOrEmpty(name)) name = SHORTCUT_NAME;
        string err = CreateDesktopShortcut(DesktopDir(), exe, name, desc);
        if (err == null)
        {
            Console.WriteLine("SHORTCUT_OK " + Path.Combine(DesktopDir(), SafeShortcutName(name) + ".lnk"));
            return;
        }
        Console.WriteLine("SHORTCUT_FAIL " + err);
        Environment.Exit(1);
    }


    /// <summary>非交互服务三态：输出 STATUS_UP / STATUS_STARTING / STATUS_DOWN。
    /// v2.7：detail=true 追加 STATUS_PID / STATUS_START / STATUS_UPTIME 三行（GUI 底部状态栏数据源，全程只读）。
    /// 注意 STATUS_START 取的是监听进程的本地启动时间；仅当端口有人在听时才去查 PID，
    /// 避免服务已停时误报上一次的残留进程信息。</summary>
    static void StatusCli(bool detail)
    {
        ServiceState st = ProbeService();
        if (st == ServiceState.Ready) Console.WriteLine("STATUS_UP");
        else if (st == ServiceState.Listening) Console.WriteLine("STATUS_STARTING");
        else Console.WriteLine("STATUS_DOWN");
        if (!detail) return;
        int pid = (st == ServiceState.Down) ? 0 : FindPortPid(WEB_PORT);
        Console.WriteLine("STATUS_PID " + (pid > 0 ? pid.ToString() : "0"));
        bool haveStart = false;
        DateTime start = DateTime.MinValue;
        if (pid > 0) { try { start = Process.GetProcessById(pid).StartTime; haveStart = true; } catch { haveStart = false; } }
        Console.WriteLine("STATUS_START " + (haveStart ? start.ToString("yyyy-MM-dd HH:mm:ss") : ""));
        Console.WriteLine("STATUS_UPTIME " + (haveStart ? FormatUptime(DateTime.Now - start) : ""));
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

    // ---------------- 体检 / 诊断（v2.5：doctor） ----------------


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

}
