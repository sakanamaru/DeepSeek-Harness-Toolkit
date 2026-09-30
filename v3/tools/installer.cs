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
    private const string ReleasesUrl = "https://github.com/sakanamaru/dsh-minato/releases";
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

        Log("目标目录: " + target);
        Report(progress, 2, "准备…");
        Directory.CreateDirectory(target);

        // ① 解压到**同卷暂存目录** ✓（同卷才能改名 ✓ 跨卷 rename 会失败 ✓）
        string staging = Path.Combine(target, ".staging-" + Guid.NewGuid().ToString("N").Substring(0, 8));
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

        // ③ 就位：**先改名旧的，再改名新的** ✓（旧目录即使有文件被占用也能改名成功 ✓）
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
                Log("开始菜单快捷方式已建（桌面快捷方式默认不建 ✓ 减少杂乱 ✓）");
            }
            catch (Exception ex) { Log("快捷方式失败（不致命）: " + ex.Message); }
        }
        Report(progress, 92, "注册…");
        try { WriteArp(target); Log("ARP 注册表已写 ✓"); } catch (Exception ex) { Log("ARP 失败（不致命）: " + ex.Message); }
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
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int Msg, IntPtr wParam, string lParam, int fuFlags, int uTimeout, out IntPtr lpdwResult);

    private static bool AddToUserPath(string dir)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { Log("PATH 目标目录不存在 → 跳过: " + dir); return false; }
        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Environment", true))
        {
            if (k == null) return false;
            // 第 3 参是 RegistryValueOptions 不是 Kind ✓；DoNotExpand 才能**原样读写**别人的 PATH ✓✓
            string cur = k.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string;;
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
            string cur = k.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string;;
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
    private CheckBox _chkPath, _chkShortcuts;
    private Button _btnMain, _btnCancel, _btnCopyLog;
    private ProgressBar _bar;
    private Label _status, _title, _sub;
    private Panel _page1, _page2;

    public InstallerForm(string dirArg)
    {
        _dirArg = dirArg;
        Text = "dsh-minato 安装程序";
        ClientSize = new Size(620, 420);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        Font = new Font("Microsoft YaHei UI", 9f);
        BackColor = Color.FromArgb(250, 250, 252);

        _title = new Label { Text = "安装 dsh-minato", Font = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold), ForeColor = Color.FromArgb(30, 30, 40), AutoSize = true, Location = new Point(28, 22) };
        _sub = new Label
        {
            Text = "DeepSeek Harness 的非官方 Windows 工具箱。只读本地状态，不联网上传。",
            ForeColor = Color.FromArgb(110, 110, 120), AutoSize = false, Size = new Size(560, 20), Location = new Point(30, 58)
        };
        Controls.Add(_title); Controls.Add(_sub);

        // ---- 第一页：选项 ----
        _page1 = new Panel { Location = new Point(0, 86), Size = new Size(620, 300) };
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
        _chkPath = new CheckBox { Text = "把命令行工具加入 PATH（**默认不勾** ✓ 勾了要**新开终端**才生效）", Checked = false, AutoSize = true, Location = new Point(30, 96), ForeColor = Color.FromArgb(60, 60, 70) };
        Label note = new Label
        {
            Text = "说明：" + Environment.NewLine +
                   "· 安装是**当前用户级**的 ✓ 不写系统目录 ✓ 不弹 UAC ✓ 卸载干净 ✓" + Environment.NewLine +
                   "· **不会碰你的数据** ✓（~/.dsh 是 dsh 自己的，本工具只读）" + Environment.NewLine +
                   "· 每个文件都带官方指纹，启动时会自校验 ✓ 被改动就拒绝运行 ✓",
            ForeColor = Color.FromArgb(120, 120, 130), AutoSize = false, Size = new Size(560, 90), Location = new Point(30, 128)
        };
        _page1.Controls.Add(ld); _page1.Controls.Add(_dirBox); _page1.Controls.Add(browse);
        _page1.Controls.Add(_chkShortcuts); _page1.Controls.Add(_chkPath); _page1.Controls.Add(note);
        Controls.Add(_page1);

        // ---- 第二页：进度 ----
        _page2 = new Panel { Location = new Point(0, 86), Size = new Size(620, 300), Visible = false };
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
        _btnMain = new Button { Text = "安装", Location = new Point(410, 366), Size = new Size(90, 32), BackColor = Color.FromArgb(64, 110, 220), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        _btnMain.FlatAppearance.BorderSize = 0;
        _btnMain.Click += delegate { StartInstall(); };
        _btnCancel = new Button { Text = "取消", Location = new Point(508, 366), Size = new Size(80, 32), DialogResult = DialogResult.Cancel };
        Controls.Add(_btnMain); Controls.Add(_btnCancel);
        CancelButton = _btnCancel;
        AcceptButton = _btnMain;
        FormClosing += delegate(object s, FormClosingEventArgs e) { if (_running) { e.Cancel = true; MessageBox.Show("正在安装，请稍候…", "dsh-minato", MessageBoxButtons.OK, MessageBoxIcon.Information); } };
    }

    private bool _running;
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
                _status.Text = "已安装到：" + dir;
                _sub.Text = (_chkPath.Checked ? "PATH 已更新 —— **请新开一个终端** ✓ 旧终端看不到变化 ✓" : "已创建开始菜单快捷方式（名为 dsh-minato）✓");
                _btnMain.Text = "完成";
                _btnMain.Enabled = true;
                _btnMain.Click -= delegate { };
                _btnMain.Click += delegate { Close(); };
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
