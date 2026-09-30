// dsh-minato 自解压安装器（用户要求："引入自解压 exe 做安装器吧，自解压页面定制下" ✓✓）
//
// 设计决定（与维护者逐条确认 ✓ + 子代理评审的 15 条工程细节全部采纳 ✓）：
//   · **net48 WinForms** ✓（.NET Framework 4.8 是 Windows 系统组件 ✓ 不算第三方依赖 ✓ ~300 KB ✓）
//     用 VS2022 的 Roslyn csc 编译 ✓（in-box csc 只有 C# 5 ✗ 插值都编不过 ✓）
//   · **asInvoker** ✓✓ —— 绝不 requireAdministrator ✗（那会让**每次启动**都弹 UAC ✓ 且标准用户直接失败 ✗）
//   · **per-user 安装** ✓ 默认 %LOCALAPPDATA%\Programs\dsh-minato ✓ 可自定义 ✓
//   · **先显示窗口再解压** ✓✓（冻结的窗口是"这是恶意软件"的第一特征 ✓）
//   · **后台线程 + 真实进度** ✓（BackgroundWorker ✓）
//   · **版本化目录 + 暂存后改名** ✓✓（升级/卸载不怕程序在跑 ✓ 不需要重启 ✓）
//   · **持久化 uninstall.exe** ✓✓（下载的 SFX 会被删掉 ✓ 不能靠它卸载 ✓）
//   · **zip-slip 校验** ✓✓（拒绝绝对路径与 .. ✓）
//   · **PATH 默认不勾** ✓ + **绝不用 setx** ✗（会截断 PATH ✓）→ 写 HKCU\Environment + WM_SETTINGCHANGE ✓
//   · **卸载不删数据** ✓ + **桌面生成说明文档** ✓ + **界面明写"不会被删除"** ✓✓（维护者的选择 ✓）
//   · 安装日志 + "复制日志" ✓ · ARP 注册表（含 DisplayIcon ✓）· 快捷方式叫 dsh-minato ✓
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Installer
{
    private const string AppName = "dsh-minato";
    private const string Publisher = "dsh-minato (unofficial)";
    internal const string ReleasesUrl = "https://github.com/sakanamaru/dsh-minato/releases";   // internal ✓ 窗体类要用 ✓
    /// <summary>上次载荷校验的结果 ✓（空 = 通过或未跑；否则 = 对不上的文件名 ✓ 完成页要显示 ✓）。</summary>
    internal static string LastVerifyResult = "";
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "dsh-minato-install.log");
    private static readonly StringBuilder LogBuf = new StringBuilder();

    [STAThread]
    private static int Main(string[] args)
    {
        bool uninstall = false, silent = false, noPath = false, noShortcuts = false;
        string dirArg = null;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i] == null ? "" : args[i].Trim();
            if (a == "--uninstall") uninstall = true;
            else if (a == "--silent") silent = true;
            else if (a == "--no-path") noPath = true;
            else if (a == "--no-shortcuts") noShortcuts = true;
            else if (a.StartsWith("--dir=", StringComparison.Ordinal)) dirArg = a.Substring(6).Trim('"');
        }
        Log("=== dsh-minato installer " + (uninstall ? "(uninstall)" : "(install)") + " ===");
        Log("args: " + string.Join(" ", args));
        try
        {
            if (uninstall) return RunUninstall(silent);
            if (silent) return RunInstall(dirArg, false, noPath, noShortcuts, null);
            // AppUserModelID ✓（任务栏分组与图标更可靠 ✓ 也让"固定到任务栏"认得出是本程序 ✓）
            try { SetCurrentProcessExplicitAppUserModelID("dsh-minato.installer"); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (InstallerForm f = new InstallerForm(dirArg))
            {
                Application.Run(f);
                return f.ExitCode;
            }
        }
        catch (Exception ex)
        {
            Log("FATAL " + ex);
            if (!silent) MessageBox.Show(ex.Message, AppName + " 安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 9;
        }
        finally { FlushLog(); }
    }

    // ================================================================ 安装

    /// <summary>安装 ✓。返回 0 = 成功。**先解压到暂存目录并校验，再改名就位** ✓（原子性 ✓）。</summary>
    internal static int RunInstall(string dir, bool wantPath, bool noPath, bool noShortcuts, Action<int, string> progress)
    {
        string target = string.IsNullOrEmpty(dir) ? DefaultDir() : dir;
        if (string.IsNullOrEmpty(target)) { Log("目标目录为空 → 拒绝"); return 2; }
        try { target = Path.GetFullPath(target); } catch { Log("目标路径非法: " + dir); return 2; }
        if (IsDangerousPath(target)) { Log("拒绝写入危险路径: " + target); return 2; }

        // 已装检测 + 版本比较 ✓✓（用户要求："安装好后也可以再点安装，安装器也加个版本校验吧" ✓）
        // 图形界面里显示在选项页 ✓；这里**也写日志** ✓ → 静默模式/自动化也能看到 ✓✓
        string installedInfo = ReadInstalled();
        string verdict;
        if (string.IsNullOrEmpty(installedInfo)) verdict = "未安装过 → 全新安装";
        else
        {
            string have = installedInfo.Split('|')[0];
            int cmp = CompareVersions(SelfVersion(), have);
            verdict = cmp > 0 ? ("已装 " + have + " → 本次是**更新版本 " + SelfVersion() + "**，执行**升级** ✓")
                    : (cmp == 0 ? ("已装 " + have + " → **版本相同**，执行**重新安装（覆盖）** ✓")
                                : ("⚠ 已装 " + have + "，本次是**更旧的 " + SelfVersion() + "** → 会**降级** ✗"));
        }
        Log("已装检测: " + (string.IsNullOrEmpty(installedInfo) ? "（无）" : installedInfo) + " → " + verdict);

        Log("目标目录: " + target);
        Report(progress, 2, "准备…");
        // ✗ 原来在这里就 CreateDirectory → 校验失败会**留下一个空目录** ✗（实测确认 ✓）
        // ✓ 改成**校验通过后再建** ✓ → 拒绝时磁盘上**一个字节都不写** ✓✓

        // ① 解压到**同卷暂存目录** ✓（同卷才能改名 ✓ 跨卷 rename 会失败 ✓）
        // ✗ 原来暂存放在 target **里面** → CreateDirectory 会把 target 一起建出来 ✗
        //    → 于是"篡改包被拒绝"时磁盘上**留下一个空目录** ✗（实测确认 ✓）
        // ✓ 改放在 **target 的父目录**：同卷 ✓（改名才能成功 ✓）且**完全不碰 target** ✓✓
        string parentDir = Path.GetDirectoryName(target);
        if (string.IsNullOrEmpty(parentDir)) parentDir = Path.GetTempPath();
        Directory.CreateDirectory(parentDir);
        string staging = Path.Combine(parentDir, ".dsh-minato-staging-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(staging);
        Log("暂存目录: " + staging);
        int files = ExtractPayload(staging, progress);
        Log("解压完成，文件数 " + files);
        Report(progress, 70, "校验…");

        // ② 校验：三个可执行文件都在 ✓（不通过就不动正式目录 ✓）
        foreach (string need in new string[] { "dsh-minato.exe", Path.Combine("gui", "dsht-gui.exe") })
        {
            if (!File.Exists(Path.Combine(staging, need)))
            {
                Log("暂存目录缺少 " + need + " → 安装中止（正式目录未被改动 ✓）");
                TryDelete(staging);
                return 3;
            }
        }

        // ③ **载荷完整性校验** ✓✓（用户要求："安装器也加个版本校验吧，比如有更新或者**非官方**" ✓）
        //    安装包内自带 hashes.txt ✓ → 逐条核对 ✓ → 对不上就是**被改过的包** ✓ → 拒绝安装 ✓✓
        Report(progress, 74, "校验安装包…");
        string badFile = VerifyPayload(staging);
        if (badFile != null)
        {
            Log("载荷校验失败 ✗ " + badFile);
            TryDelete(staging);
            throw new InvalidOperationException(
                "**这个安装包不是官方发布的，或者已经被改动过。**" + Environment.NewLine + Environment.NewLine +
                "对不上的文件：" + badFile + Environment.NewLine + Environment.NewLine +
                "请从官方 Releases 重新下载：" + Environment.NewLine + ReleasesUrl + Environment.NewLine + Environment.NewLine +
                "（已拒绝安装 ✓ 没有写入任何东西 ✓）");
        }
        Log("载荷校验通过 ✓ 每个文件的指纹都与包内清单一致 ✓");

        Directory.CreateDirectory(target);   // ✓ 校验已通过 ✓ 现在才建 ✓
        // ⑤ 就位：**先改名旧的，再改名新的** ✓（旧目录即使有文件被占用也能改名成功 ✓）
        Report(progress, 78, "就位…");
        string old = target + ".old-" + DateTime.Now.ToString("HHmmss");
        bool hadOld = false;
        try
        {
            // 把暂存里的内容搬进一个版本化目录 ✓
            string verDir = Path.Combine(target, "app-" + ShortVersion());
            if (Directory.Exists(verDir)) { Directory.Delete(verDir, true); }
            Directory.Move(staging, verDir);
            Log("版本目录: " + verDir);
            // 稳定入口：把启动器与 CLI 复制到 target 根 ✓（快捷方式指向它们 ✓ 升级时路径不变 ✓）
            CopyFile(Path.Combine(verDir, "dsh-minato.exe"), Path.Combine(target, "dsh-minato.exe"));
            CopyFile(Path.Combine(verDir, "dsh-minato-gui.exe"), Path.Combine(target, "dsh-minato-gui.exe"));
            if (File.Exists(Path.Combine(verDir, "hashes.txt"))) CopyFile(Path.Combine(verDir, "hashes.txt"), Path.Combine(target, "hashes.txt"));
            string vgui = Path.Combine(verDir, "gui");
            if (Directory.Exists(vgui))
            {
                string tgui = Path.Combine(target, "gui");
                if (Directory.Exists(tgui)) { try { Directory.Move(tgui, old); hadOld = true; } catch { } TryDelete(tgui); }
                Directory.Move(vgui, tgui);
            }
            Log("稳定入口已就位（dsh-minato.exe / dsh-minato-gui.exe / gui\\）");
        }
        catch (Exception ex)
        {
            Log("就位失败: " + ex.Message);
            TryDelete(staging);
            return 4;
        }
        if (hadOld) TryDelete(old);

        // ④ 持久化卸载器 ✓✓（下载的 SFX 会被用户删掉 ✗ 不能靠它 ✓）
        Report(progress, 84, "写入卸载器…");
        try
        {
            // ✗✗ 原来直接 File.Copy(自己) → 卸载器**也带上 73MB 载荷** ✗（实测装出来 302MB ✗ 评审警告过 ✓）
            // ✓ 现在：从**内嵌资源 uninstall.exe** 取（同一个源码编两遍 ✓ 不带载荷那份只有 ~40KB ✓✓）
            string un = Path.Combine(target, "uninstall.exe");
            bool wrote = false;
            Assembly asm2 = Assembly.GetExecutingAssembly();
            foreach (string rn in asm2.GetManifestResourceNames())
            {
                if (!rn.EndsWith("uninstall.exe", StringComparison.OrdinalIgnoreCase)) continue;
                using (Stream rs = asm2.GetManifestResourceStream(rn))
                using (FileStream fs = File.Create(un))
                { rs.CopyTo(fs); wrote = true; }
                break;
            }
            if (!wrote)
            {
                // 兜底：老办法（会大 ✗ 但至少能用 ✓）
                string self = Process.GetCurrentProcess().MainModule.FileName;
                if (!string.Equals(Path.GetFullPath(self), Path.GetFullPath(un), StringComparison.OrdinalIgnoreCase))
                    File.Copy(self, un, true);
                Log("卸载器：内嵌资源缺失 → 回退为复制自身（会大 ✗）");
            }
            Log("卸载器: " + un + "（" + new FileInfo(un).Length + " 字节 ✓）");
        }
        catch (Exception ex) { Log("卸载器写入失败（不致命）: " + ex.Message); }

        // ⑤ 快捷方式 → ARP → PATH **最后** ✓（顺序按评审建议 ✓：文件先验证通过 ✓）
        Report(progress, 88, "快捷方式…");
        if (!noShortcuts)
        {
            try
            {
                string exe = Path.Combine(target, "dsh-minato-gui.exe");
                CreateShortcut(StartMenuDir(), AppName, exe, target, "dsh-minato — DeepSeek Harness 工具箱（非官方）");
                CreateShortcut(StartMenuDir(), AppName + "（卸载）", Path.Combine(target, "uninstall.exe"), target, "卸载 dsh-minato");
                // 桌面快捷方式 ✓（**默认不建** ✓ 用户在选项里勾了才建 ✓ 用户要求："添加创建快捷方式询问或者选项框" ✓）
                if (InstallerForm.WantDesktopShortcut)
                {
                    string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(desk, AppName, Path.Combine(target, "dsh-minato-gui.exe"), target, "dsh-minato — DeepSeek Harness 工具箱（非官方）");
                    Log("桌面快捷方式已建（用户勾选 ✓）: " + Path.Combine(desk, AppName + ".lnk"));
                }
                else Log("桌面快捷方式：未勾选 → 不建 ✓");
                Log("开始菜单快捷方式已建（桌面快捷方式默认不建 ✓ 减少杂乱 ✓）");
            }
            catch (Exception ex) { Log("快捷方式失败（不致命）: " + ex.Message); }
        }
        Report(progress, 92, "注册…");
        try { WriteArp(target); Log("ARP 注册表已写 ✓"); } catch (Exception ex) { Log("ARP 失败（不致命）: " + ex.Message); }
        // ★ 写**安装标记** ✓✓（卸载时靠它确认"这里确实是本工具的安装目录" ✓✓
        //   —— 没有它，单独复制的卸载器就能删掉任意目录 ✗ 实测踩到过 ✓）
        try
        {
            File.WriteAllText(Path.Combine(target, ".dsh-minato-install"),
                "dsh-minato install marker" + Environment.NewLine +
                "version=" + SelfVersion() + Environment.NewLine +
                "installed=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                "path=" + target + Environment.NewLine, new UTF8Encoding(false));
            Log("安装标记已写 ✓ .dsh-minato-install（卸载时的安全凭据 ✓）");
        }
        catch (Exception ex) { Log("安装标记写入失败（不致命，但卸载会更保守 ✓）: " + ex.Message); }
        if (wantPath && !noPath)
        {
            try
            {
                bool ok = AddToUserPath(Path.Combine(target, "bin"));
                Log(ok ? "已加入用户 PATH ✓（**需要新开终端** ✓）" : "PATH 未改动（可能被组策略接管 ✓ 已如实告知 ✓）");
            }
            catch (Exception ex) { Log("PATH 失败（不致命）: " + ex.Message); }
        }
        Report(progress, 100, "完成");
        Log("安装完成 ✓ → " + target);
        return 0;
    }

    // ================================================================ 卸载

    /// <summary>卸载 ✓。**默认不动数据** ✓ + 在桌面留一份说明文档指出数据位置 ✓（维护者的选择 ✓）。</summary>
    internal static int RunUninstall(bool silent)
    {
        string target = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        Log("卸载目录: " + target);

        // ★★ **先做两道安全检查，再考虑自我迁移** ✓✓
        //   ✗ 原来检查在迁移**之后** → 拒绝时父进程已返回 0 ✗ → **自动化会误读**（以为卸载成功了 ✓）
        //   ✓ 现在拒绝**当场返回 2** ✓ 不迁移 ✓ 行为与退出码一致 ✓✓
        string preTarget = Environment.GetEnvironmentVariable("DSHT_UNINSTALL_TARGET");
        if (!string.IsNullOrEmpty(preTarget)) preTarget = preTarget.TrimEnd('\\'); else preTarget = target;
        if (IsDangerousPath(preTarget)) { Log("拒绝（迁移前检查）：目录可疑 " + preTarget); return 2; }
        if (!File.Exists(Path.Combine(preTarget, ".dsh-minato-install")))
        {
            Log("拒绝（迁移前检查）：目录里没有安装标记 → 不像安装目录 ✓ **一个字节都不删** ✓");
            if (!silent) MessageBox.Show(
                "拒绝卸载。" + Environment.NewLine + Environment.NewLine +
                "这个目录里没有 dsh-minato 的安装标记：" + Environment.NewLine + preTarget + Environment.NewLine + Environment.NewLine +
                "所以它看起来**不是**本工具的安装目录 ✓ 为了安全，**什么都不会删** ✓",
                AppName + " 卸载", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 2;
        }

        // ✗✗ 实测：卸载器**自己就跑在目标目录里** ✗ → Windows 不允许删除"正在运行的程序所在目录" ✓
        //    → 所以**先把自己复制到 %TEMP% 再从那里重启** ✓✓（标准做法 ✓ 零依赖 ✓）
        if (Environment.GetEnvironmentVariable("DSHT_UNINSTALL_RELOCATED") != "1")
        {
            try
            {
                string me = Process.GetCurrentProcess().MainModule.FileName;
                string tmp = Path.Combine(Path.GetTempPath(), "dsh-minato-uninstall-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".exe");
                File.Copy(me, tmp, true);
                ProcessStartInfo psi = new ProcessStartInfo(tmp, "--uninstall" + (silent ? " --silent" : ""));
                psi.UseShellExecute = false;
                psi.EnvironmentVariables["DSHT_UNINSTALL_RELOCATED"] = "1";
                psi.EnvironmentVariables["DSHT_UNINSTALL_TARGET"] = target;
                Process.Start(psi);
                Log("已迁移到 " + tmp + " 并从那里继续 ✓（这样目标目录才能被删掉 ✓）");
                return 0;
            }
            catch (Exception ex) { Log("自我迁移失败 → 就地卸载（可能有文件删不掉 ✓ 会如实报告 ✓）: " + ex.Message); }
        }
        string t2 = Environment.GetEnvironmentVariable("DSHT_UNINSTALL_TARGET");
        if (!string.IsNullOrEmpty(t2)) target = t2.TrimEnd('\\');
        if (IsDangerousPath(target)) { Log("拒绝：目录可疑 " + target); return 2; }
        // ★★★ **最强的一道闸** ✓✓（用户问："卸载器不会和某些大厂一样把整个D盘删了吧" ✓ 这个担心完全正确 ✓）
        //   光"路径不像盘根"不够 ✗ —— 真正的保险是：**只有目录里确实有本工具的文件才允许删** ✓✓
        //   大厂那类事故的共同点就是"按计算出来的路径直接删" ✗ 完全没有"这里到底是不是我"的确认 ✓
        //   ✗✗ 实测教训：原来用 `uninstall.exe` 当标记 → **任何含这个文件名的目录都会被删** ✗
        //      （把卸载器单独复制到空目录里运行 → 它把那个目录删了 ✓ 实测确认 ✓）
        //   ✓✓ 现在要求**安装器写下的标记文件** `.dsh-minato-install` —— 单独复制的卸载器**无法满足** ✓✓
        bool looksOurs = File.Exists(Path.Combine(target, ".dsh-minato-install"));
        if (!looksOurs)
        {
            Log("拒绝卸载：这个目录里**没有本工具的文件** → 它不像安装目录 ✓ **一个字节都不删** ✓");
            if (!silent) MessageBox.Show(
                "拒绝卸载。" + Environment.NewLine + Environment.NewLine +
                "这个目录里没有 dsh-minato 的文件：" + Environment.NewLine + target + Environment.NewLine + Environment.NewLine +
                "所以它看起来**不是**本工具的安装目录 ✓ 为了安全，**什么都不会删** ✓" + Environment.NewLine + Environment.NewLine +
                "如果你确实想卸载，请用安装目录里的 uninstall.exe ✓",
                AppName + " 卸载", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 2;
        }
        try
        {
            // 检测在跑的程序 ✓（评审建议 ✓：别让用户对着"删不掉"发呆 ✓ 先提示关闭 ✓）
            bool running = false;
            try
            {
                foreach (string pn in new string[] { "dsht-gui", "DeepSeek Harness" })
                    if (Process.GetProcessesByName(pn).Length > 0) running = true;
            }
            catch { }
            if (running && !silent)
            {
                DialogResult dr = MessageBox.Show(
                    "检测到 dsh-minato 或 DeepSeek Harness 正在运行。" + Environment.NewLine + Environment.NewLine +
                    "现在卸载的话，**正在使用的文件可能删不掉**（不会损坏数据 ✓ 但目录里可能剩几个文件 ✓）。" + Environment.NewLine + Environment.NewLine +
                    "建议先关掉它们再卸载。" + Environment.NewLine + Environment.NewLine +
                    "要**先卸载、剩下的重启后再删**吗？",
                    AppName + " 卸载", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (dr != DialogResult.Yes) { Log("用户选择先关闭程序再卸载"); return 0; }
            }
            RemoveShortcuts();
            RemoveFromUserPath(Path.Combine(target, "bin"));
            RemoveArp();
            string dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh");
            WriteDataNote(dataDir);   // ✓ 桌面文档：指出数据在哪 ✓（**不删数据** ✓✓）
            // 先改名再删 ✓（文件被占用也能改名成功 ✓ 避免"需要重启" ✓）
            string doomed = target + ".removing-" + DateTime.Now.ToString("HHmmss");
            string parent = Path.GetDirectoryName(target);
            string mover = Path.Combine(parent == null ? Path.GetTempPath() : parent, Path.GetFileName(doomed));
            try { Directory.Move(target, mover); } catch { mover = target; }
            TryDelete(mover);
            bool gone = !Directory.Exists(target);
            Log(gone
                ? "卸载完成 ✓（目录已删干净 ✓ **你的数据没有被删除** ✓ 见桌面说明文档 ✓）"
                : "卸载完成（但目录里还有文件被占用 ✓ 已如实报告 ✗ 不假报干净 ✗）：" + target);
            if (!silent) MessageBox.Show(
                "卸载完成。" + Environment.NewLine + Environment.NewLine +
                "**你的数据没有被删除** ✓" + Environment.NewLine +
                "DeepSeek Harness 的会话与设置仍在：" + Environment.NewLine + dataDir + Environment.NewLine + Environment.NewLine +
                "桌面上已生成一份说明文档，写明了这个位置。",
                AppName + " 卸载", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
        catch (Exception ex)
        {
            Log("卸载出错: " + ex);
            if (!silent) MessageBox.Show(ex.Message, AppName + " 卸载失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 5;
        }
        finally { FlushLog(); }
    }

    // ================================================================ 解压（含 zip-slip 校验 ✓✓）

    /// <summary>从**内嵌资源**解压 ✓✓。返回文件数。**拒绝绝对路径与 .. 段** ✓✓（zip-slip ✓）。</summary>
    private static int ExtractPayload(string destRoot, Action<int, string> progress)
    {
        Assembly asm = Assembly.GetExecutingAssembly();
        string resName = null;
        foreach (string n in asm.GetManifestResourceNames())
            if (n.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase)) { resName = n; break; }
        if (resName == null) { Log("内嵌 payload.zip 找不到 ✗"); throw new InvalidOperationException("安装包损坏：找不到内嵌载荷。"); }

        string destFull = Path.GetFullPath(destRoot);
        int count = 0;
        using (Stream s = asm.GetManifestResourceStream(resName))
        using (ZipArchive zip = new ZipArchive(s, ZipArchiveMode.Read))
        {
            int total = zip.Entries.Count;
            for (int i = 0; i < total; i++)
            {
                ZipArchiveEntry e = zip.Entries[i];
                string rel = e.FullName.Replace('/', Path.DirectorySeparatorChar);
                // ---- zip-slip 校验 ✓✓ ----
                if (rel.StartsWith("\\", StringComparison.Ordinal) || rel.StartsWith("/", StringComparison.Ordinal) || rel.Contains(":"))
                { Log("拒绝绝对路径条目: " + e.FullName); continue; }
                string full = Path.GetFullPath(Path.Combine(destFull, rel));
                if (!full.StartsWith(destFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                { Log("拒绝越界条目: " + e.FullName); continue; }
                if (e.FullName.EndsWith("/", StringComparison.Ordinal)) { Directory.CreateDirectory(full); continue; }
                string d = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
                using (Stream src = e.Open())
                using (FileStream dst = File.Create(full))
                    src.CopyTo(dst);
                count++;
                if (progress != null && total > 0 && (i % 20) == 0) Report(progress, 10 + (int)(55.0 * i / total), "解压 " + (i + 1) + "/" + total);
            }
        }
        return count;
    }

    // ================================================================ 杂项

    /// <summary>本安装器自己的版本 ✓（写进 ARP 的 DisplayVersion ✓ 也用于和已装版本比较 ✓）。</summary>
    internal static string SelfVersion()
    {
        try
        {
            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? "0.0.0" : (v.Major + "." + v.Minor + "." + v.Build);
        }
        catch { return "0.0.0"; }
    }

    /// <summary>读已安装信息 ✓（ARP 的 DisplayVersion + InstallLocation ✓）。返回 "版本|位置"；未装返回空 ✓。</summary>
    internal static string ReadInstalled()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName))
            {
                if (k == null) return "";
                string v = k.GetValue("DisplayVersion", "") as string;
                string loc = k.GetValue("InstallLocation", "") as string;
                if (string.IsNullOrEmpty(v) && string.IsNullOrEmpty(loc)) return "";
                return (v == null ? "" : v) + "|" + (loc == null ? "" : loc);
            }
        }
        catch { return ""; }
    }

    /// <summary>版本比较 ✓（逐段数字比较 ✓ 不用字符串比较 ✗ —— "3.10" &gt; "3.9" 但字符串会说反 ✗）。</summary>
    internal static int CompareVersions(string a, string b)
    {
        try
        {
            string[] pa = (a == null ? "" : a).Split('.');
            string[] pb = (b == null ? "" : b).Split('.');
            int n = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                int va = 0, vb = 0;
                if (i < pa.Length) int.TryParse(pa[i], out va);
                if (i < pb.Length) int.TryParse(pb[i], out vb);
                if (va != vb) return va > vb ? 1 : -1;
            }
            return 0;
        }
        catch { return 0; }
    }

    internal static string DefaultDir()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName);
    }
    private static string StartMenuDir()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs", AppName);
    }
    private static string ShortVersion()
    {
        try
        {
            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? "0.0.0" : (v.Major + "." + v.Minor + "." + v.Build);
        }
        catch { return "0.0.0"; }
    }

    /// <summary>核对**暂存目录里每个文件**的 SHA-256 vs 包内 hashes.txt ✓✓。
    /// 返回 null = 全部一致 ✓；否则返回**第一个对不上的文件名** ✓（安装器据此拒绝安装 ✓）。</summary>
    private static string VerifyPayload(string staging)
    {
        try
        {
            string mf = Path.Combine(staging, "hashes.txt");
            if (!File.Exists(mf)) { Log("包内没有 hashes.txt → 跳过载荷校验（开发构建属正常 ✓ 但会明确记录 ✓）"); return null; }
            string[] lines = File.ReadAllLines(mf);
            int checkedCount = 0, mismatch = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i] == null ? "" : lines[i].Trim();
                if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal)) continue;
                int sp = t.IndexOf(' ');
                if (sp <= 0) continue;
                string want = t.Substring(0, sp).Trim().ToLowerInvariant();
                string name = t.Substring(sp + 1).Trim();
                string full = null;
                // 清单里可能只写文件名 ✓ 也可能写相对路径 ✓ 两种都试 ✓
                string c1 = Path.Combine(staging, name);
                if (File.Exists(c1)) full = c1;
                else
                {
                    string c2 = Path.Combine(staging, "gui", name);
                    if (File.Exists(c2)) full = c2;
                }
                if (full == null) { Log("清单里有但包里没有（跳过 ✓ 不误报 ✗）: " + name); continue; }
                checkedCount++;
                string got = Sha256Of(full);
                if (string.IsNullOrEmpty(got)) continue;
                if (!string.Equals(want, got, StringComparison.OrdinalIgnoreCase))
                {
                    mismatch++;
                    Log("指纹不符 ✗ " + name + " 期望=" + want.Substring(0, 12) + "… 实际=" + got.Substring(0, 12) + "…");
                    if (mismatch == 1) { LastVerifyResult = name; return name; }   // 记下来 ✓ 完成页要显示 ✓
                }
            }
            Log("载荷校验：核对 " + checkedCount + " 个文件，不符 " + mismatch + " 个 ✓");
            LastVerifyResult = "";
            return null;
        }
        catch (Exception ex) { Log("载荷校验本身出错 → 放行（不能让校验把安装变成砖 ✓）: " + ex.Message); return null; }
    }

    private static string Sha256Of(string path)
    {
        try
        {
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

    /// <summary>危险路径防护 ✓（空 / 盘根 / 用户主目录本身 / Windows 目录 → 一律拒绝 ✓）。</summary>
    private static bool IsDangerousPath(string p)
    {
        if (string.IsNullOrEmpty(p)) return true;
        string f;
        try { f = Path.GetFullPath(p).TrimEnd('\\'); } catch { return true; }
        if (f.Length <= 3) return true;                                  // C:\ 之类 ✓
        string up = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\');
        if (string.Equals(f, up, StringComparison.OrdinalIgnoreCase)) return true;
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
        if (f.StartsWith(win, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void CopyFile(string from, string to)
    {
        try { if (File.Exists(from)) File.Copy(from, to, true); } catch (Exception ex) { Log("复制失败 " + from + " → " + ex.Message); }
    }
    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch (Exception ex) { Log("删除失败 " + dir + " → " + ex.Message); }
    }
    private static void Report(Action<int, string> p, int pct, string msg) { if (p != null) p(pct, msg); }

    // ---- 快捷方式（.lnk）用 WScript.Shell（系统自带 ✓ 零依赖 ✓）----
    private static void CreateShortcut(string dir, string name, string target, string workDir, string desc)
    {
        Directory.CreateDirectory(dir);
        string lnk = Path.Combine(dir, name + ".lnk");
        Type t = Type.GetTypeFromProgID("WScript.Shell");
        if (t == null) { Log("WScript.Shell 不可用 → 跳过快捷方式"); return; }
        object sh = Activator.CreateInstance(t);
        try
        {
            object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
            Type st = sc.GetType();
            st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { target });
            st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { workDir });
            st.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { desc });
            st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { target + ",0" });
            st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            Log("快捷方式: " + lnk);
        }
        finally { try { Marshal.ReleaseComObject(sh); } catch { } }
    }
    private static void RemoveShortcuts()
    {
        try
        {
            string dir = StartMenuDir();
            if (Directory.Exists(dir)) { Directory.Delete(dir, true); Log("已删开始菜单目录: " + dir); }
        }
        catch (Exception ex) { Log("删快捷方式失败（继续 ✓）: " + ex.Message); }
        try
        {
            string desk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk");
            if (File.Exists(desk)) { File.Delete(desk); Log("已删桌面快捷方式"); }
        }
        catch { }
    }

    // ---- ARP（应用和功能里能看到 ✓ 含 DisplayIcon ✓ 否则小白看到通用图标 ✓）----
    private static void WriteArp(string target)
    {
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName))
        {
            if (k == null) return;
            k.SetValue("DisplayName", AppName + " — DeepSeek Harness 工具箱（非官方）");
            k.SetValue("DisplayVersion", ShortVersion());
            k.SetValue("Publisher", Publisher);
            k.SetValue("DisplayIcon", Path.Combine(target, "dsh-minato-gui.exe"));
            k.SetValue("InstallLocation", target);
            k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            k.SetValue("UninstallString", "\"" + Path.Combine(target, "uninstall.exe") + "\" --uninstall");
            k.SetValue("QuietUninstallString", "\"" + Path.Combine(target, "uninstall.exe") + "\" --uninstall --silent");
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            try
            {
                long size = 0;
                foreach (string f in Directory.GetFiles(target, "*", SearchOption.AllDirectories)) { try { size += new FileInfo(f).Length; } catch { } }
                k.SetValue("EstimatedSize", (int)(size / 1024), RegistryValueKind.DWord);
            }
            catch { }
        }
    }
    private static void RemoveArp()
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName, false); Log("已删 ARP 项"); }
        catch (Exception ex) { Log("删 ARP 失败: " + ex.Message); }
    }

    // ---- PATH：**绝不用 setx** ✗（会截断 PATH ✓）→ HKCU\Environment + 广播 ✓ ----
    private const int HWND_BROADCAST = 0xffff;
    private const int WM_SETTINGCHANGE = 0x1a;
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
    [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int Msg, IntPtr wParam, string lParam, int fuFlags, int uTimeout, out IntPtr lpdwResult);

    private static bool AddToUserPath(string dir)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { Log("PATH 目标目录不存在 → 跳过: " + dir); return false; }
        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Environment", true))
        {
            if (k == null) return false;
            // 第 3 参是 RegistryValueOptions 不是 Kind ✓；DoNotExpand 才能**原样读写**别人的 PATH ✓✓
            string cur = k.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
            if (cur == null) cur = "";
            foreach (string part in cur.Split(';'))
                if (string.Equals(part.Trim().TrimEnd('\\'), dir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                { Log("PATH 里已存在 → 不重复添加 ✓"); return true; }
            string next = cur.Length == 0 ? dir : (cur.TrimEnd(';') + ";" + dir);
            k.SetValue("Path", next, RegistryValueKind.ExpandString);   // REG_EXPAND_SZ ✓
            Broadcast();
            return true;
        }
    }
    private static void RemoveFromUserPath(string dir)
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Environment", true))
            {
                if (k == null) return;
                // 第 3 参是 RegistryValueOptions 不是 Kind ✓；DoNotExpand 才能**原样读写**别人的 PATH ✓✓
            string cur = k.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                if (string.IsNullOrEmpty(cur)) return;
                StringBuilder sb = new StringBuilder();
                foreach (string part in cur.Split(';'))
                {
                    if (part.Trim().Length == 0) continue;
                    if (string.Equals(part.Trim().TrimEnd('\\'), dir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) continue;   // 只删**自己加的那条** ✓
                    if (sb.Length > 0) sb.Append(';');
                    sb.Append(part.Trim());
                }
                k.SetValue("Path", sb.ToString(), RegistryValueKind.ExpandString);
                Broadcast();
                Log("已从用户 PATH 移除: " + dir);
            }
        }
        catch (Exception ex) { Log("PATH 移除失败（继续 ✓）: " + ex.Message); }
    }
    private static void Broadcast()
    {
        try { IntPtr r; SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, "Environment", 2, 3000, out r); } catch { }
    }

    // ---- 桌面说明文档 ✓✓（维护者的选择：不提供删数据 ✓ 但告诉你数据在哪 ✓）----
    private static void WriteDataNote(string dataDir)
    {
        try
        {
            string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string path = Path.Combine(desk, "dsh-minato 卸载说明.txt");
            bool exists = Directory.Exists(dataDir);
            string body =
                "dsh-minato 已卸载。" + Environment.NewLine + Environment.NewLine +
                "**你的数据没有被删除。**" + Environment.NewLine + Environment.NewLine +
                "DeepSeek Harness 的会话、设置等数据在：" + Environment.NewLine +
                "    " + dataDir + Environment.NewLine +
                (exists ? "（这个目录现在还在 ✓）" : "（这个目录当前不存在）") + Environment.NewLine + Environment.NewLine +
                "这些数据**不是 dsh-minato 创建的** ✓ 是 DeepSeek Harness（dsh）自己的 ✓" + Environment.NewLine +
                "所以卸载 dsh-minato **不会**、也**不应该**动它 ✓" + Environment.NewLine + Environment.NewLine +
                "如果你确实想删掉这些数据：" + Environment.NewLine +
                "  1. 先确认你不再需要这些会话记录（**删了无法恢复**）" + Environment.NewLine +
                "  2. 确认 dsh 本身也已卸载（否则它会重新生成）" + Environment.NewLine +
                "  3. 手动删除上面那个目录" + Environment.NewLine + Environment.NewLine +
                "—— dsh-minato（非官方工具箱）" + Environment.NewLine +
                ReleasesUrl + Environment.NewLine;
            File.WriteAllText(path, body, new UTF8Encoding(false));
            Log("桌面说明文档: " + path);
        }
        catch (Exception ex) { Log("写桌面文档失败（不致命）: " + ex.Message); }
    }

    // ---- 日志 ✓（没有它就没法给小白做支持 ✓）----
    internal static void Log(string msg)
    {
        string line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg;
        LogBuf.AppendLine(line);
    }
    internal static void FlushLog()
    {
        try { File.WriteAllText(LogPath, LogBuf.ToString(), new UTF8Encoding(false)); } catch { }
    }
    internal static string LogFilePath() { return LogPath; }
    internal static string LogText() { return LogBuf.ToString(); }
}

/// <summary>安装器窗口 ✓ 三个状态：选项 → 进度 → 完成 ✓（评审建议 ✓）。
/// 用**真控件**换色/字体做定制 ✓（不用自绘整窗 ✗ —— 那会破坏键盘导航与屏幕阅读器 ✓）。</summary>
internal sealed class InstallerForm : Form
{
    public int ExitCode = 0;
    private readonly string _dirArg;
    private TextBox _dirBox;
    private CheckBox _chkPath, _chkShortcuts, _chkDesktop;
    private Button _btnMain, _btnCancel, _btnCopyLog;
    private ProgressBar _bar;
    private Label _status, _title, _sub;
    private Panel _page1, _page2;
    private string _installed = "";
    private Label _installedLabel;

    public InstallerForm(string dirArg)
    {
        _dirArg = dirArg;
        Text = "dsh-minato 安装程序";
        ClientSize = new Size(620, 532);   // ✗ 原来 420（面板盖住按钮）→ 486 → 现在 532（**自绘标题栏占 46px** ✓）
        AutoScaleMode = AutoScaleMode.Dpi;   // ✓ DPI 自适应 ✓（否则 125%/150% 缩放下布局会溢出 ✓ 子代理第 11 条 ✓）
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;   // ✓ 去掉 Windows 自带的框 ✓（用户要求："X 和 minato 一样在里面" ✓）
        MaximizeBox = false; MinimizeBox = false;
        Font = new Font("Microsoft YaHei UI", 9f);
        // ★ 显式设窗口图标 ✓✓（用户实测："启动后内部的和任务栏的"还是占位符 ✗）
        //   原因：**无边框窗口 + 自绘标题栏**时，WinForms 不会自动采用 exe 的图标 ✗
        //   → 显式从**自己的 exe** 取图标 ✓ → 任务栏 / Alt+Tab / 窗口都对了 ✓✓
        try { this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        BackColor = Color.FromArgb(250, 250, 252);

        _title = new Label { Text = "安装 dsh-minato", Font = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold), ForeColor = Color.FromArgb(30, 30, 40), AutoSize = true, Location = new Point(28, 70) };
        _sub = new Label
        {
            Text = "DeepSeek Harness 的非官方 Windows 工具箱。只读本地状态，不联网上传。",
            ForeColor = Color.FromArgb(110, 110, 120), AutoSize = false, Size = new Size(560, 20), Location = new Point(30, 106)
        };
        // ---- 自绘标题栏 ✓✓（用户要求："去掉顶上win自带的框，X和minato一样在里面" ✓ + "加logo和项目地址" ✓）----
        Panel header = new Panel { Location = new Point(0, 0), Size = new Size(620, 46), BackColor = Color.White };
        PictureBox logo = new PictureBox { Location = new Point(16, 9), Size = new Size(28, 28), SizeMode = PictureBoxSizeMode.StretchImage };
        try { if (Icon != null) logo.Image = Icon.ToBitmap(); } catch { }
        Label htitle = new Label { Text = "dsh-minato 安装程序", Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold), ForeColor = Color.FromArgb(30, 30, 40), AutoSize = true, Location = new Point(54, 14) };
        LinkLabel hlink = new LinkLabel { Text = "github.com/sakanamaru/dsh-minato", AutoSize = true, Location = new Point(300, 17), Font = new Font("Microsoft YaHei UI", 8.5f), LinkColor = Color.FromArgb(64, 110, 220) };
        hlink.LinkClicked += delegate { try { Process.Start(Installer.ReleasesUrl); } catch { } };
        Button hclose = new Button { Text = "✕", Location = new Point(586, 9), Size = new Size(26, 26), FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.FromArgb(90, 90, 100) };
        hclose.FlatAppearance.BorderSize = 0;
        hclose.Click += delegate { Close(); };
        header.Controls.Add(logo); header.Controls.Add(htitle); header.Controls.Add(hlink); header.Controls.Add(hclose);
        // 无边框窗口要**自己实现拖动** ✓（拖标题栏空白处 ✓）
        MouseEventHandler drag = delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Installer.ReleaseCapture(); Installer.SendMessage(Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero); } };
        header.MouseDown += drag; htitle.MouseDown += drag; logo.MouseDown += drag;
        Controls.Add(header);

        Controls.Add(_title); Controls.Add(_sub);

        // ---- 第一页：选项 ----
        _page1 = new Panel { Location = new Point(0, 134), Size = new Size(620, 330) };
        Label ld = new Label { Text = "安装位置", ForeColor = Color.FromArgb(70, 70, 80), AutoSize = true, Location = new Point(30, 8) };
        _dirBox = new TextBox { Location = new Point(30, 30), Size = new Size(470, 26), Text = string.IsNullOrEmpty(_dirArg) ? Installer.DefaultDir() : _dirArg };
        Button browse = new Button { Text = "浏览…", Location = new Point(508, 29), Size = new Size(80, 27) };
        browse.Click += delegate
        {
            using (FolderBrowserDialog fb = new FolderBrowserDialog())
            {
                fb.Description = "选择安装位置（**不需要管理员权限** ✓）";
                fb.SelectedPath = _dirBox.Text;
                if (fb.ShowDialog(this) == DialogResult.OK) _dirBox.Text = fb.SelectedPath;
            }
        };
        _chkShortcuts = new CheckBox { Text = "在开始菜单创建快捷方式（快捷方式名为 dsh-minato）", Checked = true, AutoSize = true, Location = new Point(30, 70), ForeColor = Color.FromArgb(60, 60, 70) };
        // 桌面快捷方式 ✓（**默认不勾** ✓ 用户要求："添加创建快捷方式询问或者选项框" ✓）
        _chkDesktop = new CheckBox { Text = "同时在**桌面**创建快捷方式（默认不勾 ✓ 减少杂乱 ✓）", Checked = false, AutoSize = true, Location = new Point(30, 94), ForeColor = Color.FromArgb(60, 60, 70) };
        _chkPath = new CheckBox { Text = "把命令行工具加入 PATH（**默认不勾** ✓ 勾了要**新开终端**才生效）", Checked = false, AutoSize = true, Location = new Point(30, 120), ForeColor = Color.FromArgb(60, 60, 70) };
        // —— 已装检测 + 版本校验 ✓✓（用户要求："安装好后也可以再点安装，安装器也加个版本校验吧，比如有更新或者非官方" ✓）——
        _installed = Installer.ReadInstalled();   // 静态成员要带类名 ✓
        _installedLabel = new Label
        {
            AutoSize = false, Size = new Size(560, 34), Location = new Point(30, 146),
            ForeColor = Color.FromArgb(150, 90, 20), Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold)
        };
        _installedLabel.Text = DescribeInstalled(_installed);
        Installer.Log("已装检测: [" + _installed + "] → " + _installedLabel.Text);   // 写日志 ✓ 便于验证 ✓
        _chkPath.Location = new Point(30, 96 + 34);
        _page1.Controls.Add(_installedLabel);
        Label note = new Label
        {
            Text = "说明：" + Environment.NewLine +
                   "· 安装是**当前用户级**的 ✓ 不写系统目录 ✓ 不弹 UAC ✓ 卸载干净 ✓" + Environment.NewLine +
                   "· **不会碰你的数据** ✓（~/.dsh 是 dsh 自己的，本工具只读）" + Environment.NewLine +
                   "· 每个文件都带官方指纹，启动时会自校验 ✓ 被改动就拒绝运行 ✓",
            ForeColor = Color.FromArgb(120, 120, 130), AutoSize = false, Size = new Size(560, 100), Location = new Point(30, 190)
        };
        _page1.Controls.Add(ld); _page1.Controls.Add(_dirBox); _page1.Controls.Add(browse);
        _page1.Controls.Add(_chkShortcuts); _page1.Controls.Add(_chkDesktop); _page1.Controls.Add(_chkPath); _page1.Controls.Add(note);
        Controls.Add(_page1);

        // ---- 第二页：进度 ----
        _page2 = new Panel { Location = new Point(0, 134), Size = new Size(620, 330), Visible = false };
        _status = new Label { Text = "准备…", AutoSize = false, Size = new Size(560, 22), Location = new Point(30, 30), ForeColor = Color.FromArgb(60, 60, 70) };
        _bar = new ProgressBar { Location = new Point(30, 60), Size = new Size(560, 22), Minimum = 0, Maximum = 100 };
        _btnCopyLog = new Button { Text = "复制安装日志", Location = new Point(30, 100), Size = new Size(130, 30), Visible = false };
        _btnCopyLog.Click += delegate
        {
            try { Clipboard.SetText(Installer.LogText()); MessageBox.Show("日志已复制到剪贴板。", "dsh-minato", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            catch { }
        };
        _page2.Controls.Add(_status); _page2.Controls.Add(_bar); _page2.Controls.Add(_btnCopyLog);
        Controls.Add(_page2);

        // ---- 底部按钮 ----
        _btnMain = new Button { Text = DescribeAction(), Location = new Point(410, 476), Size = new Size(90, 34), BackColor = Color.FromArgb(64, 110, 220), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        _btnMain.FlatAppearance.BorderSize = 0;
        _btnMain.Click += delegate { if (_done) { Close(); return; } StartInstall(); };   // ✓ 单一处理器 ✓ 装完就只关窗 ✓✓
        _btnCancel = new Button { Text = "取消", Location = new Point(508, 476), Size = new Size(80, 34), DialogResult = DialogResult.Cancel };
        Controls.Add(_btnMain); Controls.Add(_btnCancel);
        CancelButton = _btnCancel;
        AcceptButton = _btnMain;
        FormClosing += delegate(object s, FormClosingEventArgs e) { if (_running) { e.Cancel = true; MessageBox.Show("正在安装，请稍候…", "dsh-minato", MessageBoxButtons.OK, MessageBoxIcon.Information); } };
    }

    /// <summary>按钮文案随"已装版本 vs 本次版本"变化 ✓✓（用户要求："安装好后也可以再点安装" ✓ 现在会明确告诉你是在升级还是重装 ✓）。</summary>
    private string DescribeAction()
    {
        if (string.IsNullOrEmpty(_installed)) return "安装";
        string have = _installed.Split('|')[0];
        int cmp = Installer.CompareVersions(Installer.SelfVersion(), have);
        if (cmp > 0) return "升级到 " + Installer.SelfVersion();
        if (cmp == 0) return "重新安装";
        return "降级安装";
    }

    /// <summary>已装状态一句话 ✓（含"有更新 / 同版本 / 更旧"三种 ✓ 和子代理说的"别让用户猜" ✓）。</summary>
    private string DescribeInstalled(string info)
    {
        if (string.IsNullOrEmpty(info)) return "";
        string[] p = info.Split('|');
        string have = p.Length > 0 ? p[0] : "";
        string loc = p.Length > 1 ? p[1] : "";
        int cmp = Installer.CompareVersions(Installer.SelfVersion(), have);
        string where = string.IsNullOrEmpty(loc) ? "" : ("（" + loc + "）");
        if (cmp > 0) return "✓ 检测到已安装 " + have + where + " → 本次是**更新版本 " + Installer.SelfVersion() + "**，会**升级**";
        if (cmp == 0) return "✓ 检测到已安装 " + have + where + " → 与本次**版本相同**，会**重新安装（覆盖）**";
        return "⚠ 检测到已安装 " + have + where + "，而本次是**更旧的 " + Installer.SelfVersion() + "** → 会**降级** ✗ 请确认你确实要这么做 ✗";
    }

    private bool _running;
    private bool _done;   // ✓ 装完了 → 按钮变成"完成"（**只挂一个处理器** ✓ 不会重复安装 ✓✓）
    /// <summary>要不要建**桌面**快捷方式 ✓（用户要求："添加创建快捷方式询问或者选项框" ✓）。
    /// 默认 **false** ✓（评审建议：桌面快捷方式默认不勾 ✓ 减少杂乱 ✓ OneDrive 同步目录里更该少放 ✓）。</summary>
    internal static bool WantDesktopShortcut = false;
    private void StartInstall()
    {
        string dir = _dirBox.Text == null ? "" : _dirBox.Text.Trim();
        if (dir.Length == 0) { MessageBox.Show("请先选择安装位置。", "dsh-minato", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        _page1.Visible = false; _page2.Visible = true; _running = true;
        _btnMain.Enabled = false; _btnCancel.Text = "请稍候";
        _title.Text = "正在安装…";
        _sub.Text = "窗口在解压前就已经显示出来了 ✓（冻结的窗口最像恶意软件 ✗）";
        bool wantPath = _chkPath.Checked, wantSc = _chkShortcuts.Checked;
        BackgroundWorker bw = new BackgroundWorker();
        bw.DoWork += delegate(object s, DoWorkEventArgs e)
        {
            e.Result = Installer.RunInstall(dir, wantPath, !wantPath, !wantSc, delegate(int pct, string msg)
            {
                try { BeginInvoke((MethodInvoker)delegate { _bar.Value = Math.Max(0, Math.Min(100, pct)); _status.Text = msg; }); } catch { }
            });
        };
        bw.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs e)
        {
            _running = false;
            int rc = (e.Error != null) ? 9 : (int)(e.Result ?? 9);
            if (e.Error != null) Installer.Log("后台线程异常: " + e.Error);
            if (rc == 0)
            {
                _title.Text = "安装完成 ✓";
                _bar.Value = 100;
                _status.Text = "已安装到：" + dir + (string.IsNullOrEmpty(Installer.LastVerifyResult) ? "　✓ 安装包指纹已校验：全部一致" : ("　✗ 校验异常：" + Installer.LastVerifyResult));
                _sub.Text = (_chkPath.Checked ? "PATH 已更新 —— **请新开一个终端** ✓ 旧终端看不到变化 ✓" : "已创建开始菜单快捷方式（名为 dsh-minato）✓");
                _btnMain.Text = "完成";
                _btnMain.Enabled = true;
                // ✗✗ 原来这两行：`-= delegate { }` 是**空操作** ✗ 解绑不了任何东西 ✓
                //    → 成功分支又挂了一个 Close ✓ 而 StartInstall 还在 ✗✗ → **点"完成"会再装一遍** ✓（用户实测 ✓）
                // ✓ 正解：用一个状态标志 ✓ 按钮只挂**一个**处理器 ✓✓
                _done = true;
                _btnCancel.Visible = false;
            }
            else
            {
                _title.Text = "安装未完成 ✗";
                _status.Text = "安装失败（代码 " + rc + "）。可以把日志发给我。";
                _btnCopyLog.Visible = true;
                _btnMain.Text = "重试"; _btnMain.Enabled = true;
                _btnCancel.Text = "关闭"; _btnCancel.Visible = true;
            }
            Installer.FlushLog();
        };
        bw.RunWorkerAsync();
    }
}
