// ============================================================================
//  v2.8 阶段 3：Linux 平台实现（Platform/Linux）
//  ----------------------------------------------------------------------------
//  状态：**骨架 + 尽力实现**，尚未在真机 Linux 上验收（本机无 SDK/Linux 环境）。
//        在 Windows 上这些类型不会被选中（见 Program.PlatformSeams.cs 的 Platform.Init），
//        因此 Windows 行为零变化、297 单测 / 33 集成不受影响。
//
//  设计取舍（诚实标注）：
//    · 路径：$DSH_HOME 优先，其次 $HOME/.dsh（与 Windows 侧 DATA_DIR=".dsh" 语义一致）
//    · 桌面：$XDG_DESKTOP_DIR 优先，其次 $HOME/Desktop
//    · 快捷方式：Linux 无 .lnk，按 freedesktop 约定写 ~/.local/share/applications/*.desktop
//    · 监听进程身份：读 /proc/<pid>/cmdline（无 WMI/netstat 依赖）
//    · 工作区自动探测：Linux 上暂无可靠等价物 → 返回 null（不猜，交回菜单让用户手填 ws=）
//    · 端口探测 / HTTP 探测 / 三态判定：与平台无关，直接复用现有实现
// ============================================================================
using System;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

partial class Program
{
    class LinuxPathService : IPathService
    {
        public string DataRoot()
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("DSH_HOME");
                if (!string.IsNullOrWhiteSpace(env)) return env.Trim();
                string home = Environment.GetEnvironmentVariable("HOME");
                if (string.IsNullOrWhiteSpace(home))
                    home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, DATA_DIR);
            }
            catch { return Path.Combine(".", DATA_DIR); }
        }

        public string BackupsRoot() { return Path.Combine(StateDir, "backup"); }

        public string DesktopDir()
        {
            try
            {
                string xdg = Environment.GetEnvironmentVariable("XDG_DESKTOP_DIR");
                if (!string.IsNullOrWhiteSpace(xdg)) return xdg.Trim();
                string home = Environment.GetEnvironmentVariable("HOME");
                if (string.IsNullOrWhiteSpace(home))
                    home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, "Desktop");
            }
            catch { return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); }
        }

        // Linux 上不做自动工作区探测（宁可不猜，也不误判把系统目录当工作区）
        public string WorkspaceRoot() { return null; }

        public string Normalize(string path) { return Program.P(path); }
        public string TrimTrailingSep(string path) { return Program.TrimP(path); }
    }

    class LinuxServiceProbe : IServiceProbe
    {
        public bool PortOpen(int port, int timeoutMs) { return Program.IsPortOpen(port, timeoutMs); }   // TcpClient，跨平台
        public bool HttpReady(string url, int ms) { return Program.HttpReady(url, ms); }               // HttpWebRequest，跨平台
        public ServiceState Judge(bool portOpen, bool httpOk, Func<bool> listenerIsDsh) { return Program.JudgeState3(portOpen, httpOk, listenerIsDsh); }
        public ServiceState Probe() { return Program.ProbeService(); }

        /// <summary>监听进程是否 dsh：读 /proc/&lt;pid&gt;/cmdline（等价于 Windows 侧的命令行判定）。</summary>
        public bool ListenerIsDsh()
        {
            try
            {
                int pid = Program.FindPortPid(WEB_PORT);
                if (pid <= 0) return false;
                string cmdline = "";
                try { cmdline = File.ReadAllText("/proc/" + pid + "/cmdline").Replace('\0', ' '); } catch { return false; }
                return Program.IsDshCommandLine(cmdline);
            }
            catch { return false; }
        }
        /// <summary>监听指定端口的进程 PID（Linux）：解析 `ss -ltnp` 的 pid= 字段。
        /// 依赖 iproute2（主流发行版默认自带）；取不到返回 0（与 Windows 侧"找不到返回 0"语义一致）。</summary>
        public int FindPortPid(int port)
        {
            try
            {
                var psi = new ProcessStartInfo("ss", "-ltnp");
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                using (Process p = Process.Start(psi))
                {
                    if (p == null) return 0;
                    string outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(5000);
                    foreach (string ln in outp.Split('\n'))
                    {
                        string t = ln.Trim();
                        if (t.Length == 0) continue;
                        if (t.IndexOf(":" + port, StringComparison.Ordinal) < 0) continue;
                        Match mm = Regex.Match(t, @"pid=(\d+)");
                        if (mm.Success)
                        {
                            int pid;
                            if (int.TryParse(mm.Groups[1].Value, out pid) && pid > 0) return pid;
                        }
                    }
                }
            }
            catch { }
            return 0;
        }
    }

    class LinuxShellRunner : IShellRunner
    {
        public string Capture(string exe, string args) { return Program.RunCapture(exe, args); }

        /// <summary>Linux 无"可见控制台窗口"概念：进程直接用继承的 stdio 运行（交互命令仍可输入）。</summary>
        public int Visible(string file, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(file, args);
                psi.UseShellExecute = false;
                Process p = Process.Start(psi);
                if (p == null) return -1;
                p.WaitForExit();
                return p.ExitCode;
            }
            catch { return -1; }
        }

        /// <summary>在 PATH 里找 dsh（Linux 没有 where）。找到返回绝对路径，否则 null。</summary>
        public string WhichDsh()
        {
            try
            {
                string path = Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (string d in path.Split(':'))
                {
                    string dir = d.Trim();
                    if (dir.Length == 0) continue;
                    try { string f = Path.Combine(dir, "dsh"); if (File.Exists(f)) return f; } catch { }
                }
            }
            catch { }
            return null;
        }

        /// <summary>dsh --version（Linux 直接执行，无需 cmd.exe 包装）。失败返回 null。</summary>
        public string DshVersion() { return Program.RunCapture("dsh", "--version"); }

        /// <summary>终止进程（尽力连带子进程）：先 SIGTERM，短暂等待后 SIGKILL。返回 0 表示至少发过一次信号。
        /// 诚实标注：未在真机 Linux 上验收；失败时与 Windows 侧一样只记录日志，不抛异常。</summary>
        public int KillTree(int pid)
        {
            if (pid <= 0) return -1;
            int rc = Kill(pid, "-TERM");
            try { System.Threading.Thread.Sleep(300); } catch { }
            int rc2 = Kill(pid, "-KILL");
            if (rc == 0 || rc2 == 0) return 0;
            return rc != -1 ? rc : rc2;
        }

        int Kill(int pid, string sig)
        {
            try
            {
                var psi = new ProcessStartInfo("kill", sig + " " + pid);
                psi.UseShellExecute = false;
                psi.RedirectStandardError = true;
                psi.RedirectStandardOutput = true;
                using (Process p = Process.Start(psi))
                {
                    if (p == null) return -1;
                    string e = p.StandardError.ReadToEnd();
                    p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);
                    if (p.ExitCode != 0 && !string.IsNullOrWhiteSpace(e)) Program.LogErr("kill " + sig + " " + pid + ": " + e.Trim());
                    return p.ExitCode;
                }
            }
            catch { return -1; }
        }
        /// <summary>node --version（Linux 下可执行名就是 node，无 .exe 后缀）。失败返回 null。</summary>
        public string NodeVersion() { return Program.RunCapture("node", "--version"); }

        /// <summary>npm --version（Linux 直接执行，无需 cmd.exe 包装）。失败返回 null。</summary>
        public string NpmVersion() { return Program.RunCapture("npm", "--version"); }

        /// <summary>npm view <pkg> versions（Linux 直接执行）。失败/离线返回 null。</summary>
        public string NpmViewVersions() { return Program.RunCapture("npm", "view @deepseek-ai/dsh versions"); }

        /// <summary>npm view <pkg> version（Linux 直接执行）。失败/离线返回 null。</summary>
        public string NpmViewLatest() { return Program.RunCapture("npm", "view @deepseek-ai/dsh version"); }
        /// <summary>全局安装 npm 包（Linux 直接执行 npm，无需 cmd.exe 包装）。返回退出码，-1=启动失败。</summary>
        public int NpmInstallGlobal(string pkg, string registry)
        {
            try
            {
                var psi = new ProcessStartInfo("npm", "install -g --registry=" + registry + " " + pkg);
                psi.UseShellExecute = false;
                using (Process p = Process.Start(psi)) { if (p == null) return -1; p.WaitForExit(); return p.ExitCode; }
            }
            catch { return -1; }
        }

        /// <summary>全局卸载 dsh（Linux 直接执行 npm）。返回退出码，-1=启动失败。</summary>
        public int NpmUninstallGlobal()
        {
            try
            {
                var psi = new ProcessStartInfo("npm", "uninstall -g @deepseek-ai/dsh");
                psi.UseShellExecute = false;
                using (Process p = Process.Start(psi)) { if (p == null) return -1; p.WaitForExit(); return p.ExitCode; }
            }
            catch { return -1; }
        }

        /// <summary>Linux 没有 winget：返回 -1，由调用方走既有的「请手动安装 Node」提示（诚实降级，不假装成功）。
        /// 真实发行版可在此接 apt/dnf/pacman，但那属于"改用户系统"的动作，须由用户显式确认后再做。</summary>
        public int WingetInstallNode() { return -1; }    }

    class LinuxShortcutService : IShortcutService
    {
        /// <summary>按 freedesktop 约定写 .desktop 启动器（Linux 没有 .lnk）。
        /// 目标必须是存在的可执行文件；名字做净化，防目录穿越。</summary>
        public string Create(string desktopDir, string targetExe, string lnkBase, string desc)
        {
            try
            {
                string exe = string.IsNullOrEmpty(targetExe) ? Assembly.GetExecutingAssembly().Location : targetExe;
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return "目标可执行文件不存在：" + exe;
                string name = SafeShortcutName(lnkBase);
                if (name.Length == 0) return "启动器名称非法";
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "applications");
                try { Directory.CreateDirectory(dir); } catch { }
                if (!Directory.Exists(dir)) return "应用目录不可用：" + dir;
                string file = Path.Combine(dir, name.Replace(' ', '-').ToLowerInvariant() + ".desktop");
                var sb = new StringBuilder();
                sb.AppendLine("[Desktop Entry]");
                sb.AppendLine("Type=Application");
                sb.AppendLine("Name=" + name);
                sb.AppendLine("Comment=" + (string.IsNullOrEmpty(desc) ? name : desc));
                sb.AppendLine("Exec=\"" + exe + "\"");
                sb.AppendLine("Terminal=true");
                sb.AppendLine("Categories=Utility;");
                File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
                try { Process.Start("chmod", "+x \"" + file + "\""); } catch { }
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }
    }
}