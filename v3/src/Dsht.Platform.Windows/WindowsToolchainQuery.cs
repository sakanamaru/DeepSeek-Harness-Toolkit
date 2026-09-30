using System;
using System.IO;
using Dsht.Domain.Abstractions;

namespace Dsht.Platform.Windows
{
    /// <summary>工具链版本查询（Windows）。命令串与 v2.x 逐字一致：
    ///   node.exe --version / cmd.exe /c npm --version 2>nul / cmd.exe /c dsh --version 2>nul
    ///   WhichDsh：先看 %APPDATA%\npm\dsh.cmd，再 where dsh 并**逐行净化**（拒绝含 % 或命令结构字符的候选）
    ///   npm registry：cmd.exe /c npm config get registry 2>nul</summary>
    public sealed class WindowsToolchainQuery : IToolchainQuery
    {
        private const string Q = "\"";

        public string NodeVersion() { return WindowsShell.Capture("node.exe", "--version"); }

        public string NpmVersion() { return WindowsShell.Capture("cmd.exe", "/c npm --version 2>nul"); }

        public string DshVersion()
        {
            string v = WindowsShell.Capture("cmd.exe", "/c dsh --version 2>nul");
            if (string.IsNullOrWhiteSpace(v))
            {
                string dsh = WhichDsh();
                if (dsh != null) v = WindowsShell.Capture("cmd.exe", "/c " + Q + dsh + Q + " --version");
            }
            return v;
        }

        public string WhichDsh()
        {
            try
            {
                string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string npmPath = Path.Combine(appdata, "npm", "dsh.cmd");
                if (File.Exists(npmPath)) return npmPath;
                string where = WindowsShell.Capture("cmd.exe", "/c where dsh 2>nul");
                if (!string.IsNullOrWhiteSpace(where))
                {
                    string[] lines = where.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string cand in lines)
                    {
                        string c = cand.Trim();
                        if (c.Length == 0) continue;
                        if (c.IndexOf('%') >= 0) continue;
                        if (c.IndexOfAny(new char[] { '&', '|', ';', '>', '<', '^' }) >= 0) continue;
                        if (File.Exists(c)) return c;
                    }
                }
            }
            catch { }
            return null;
        }

        public string NpmRegistryConfig() { return WindowsShell.Capture("cmd.exe", "/c npm config get registry 2>nul"); }

        /// <summary>全局安装（Windows 经 cmd.exe 包装 npm；registry 为空则用默认源）。返回退出码，-1 = 未能执行。</summary>
        public int NpmInstallGlobal(string pkg, string registry)
        {
            // I3 FIX (CLI audit MINOR): the registry value comes from `npm config get registry`, and
            // npm reads ./.npmrc from the current directory - a file an attacker can plant. It was
            // pasted straight into a cmd.exe command line. The project's own version guard names this
            // exact surface and whitelists the version; the registry was not checked at all. Only a
            // plain http(s) URL is accepted, and anything else is refused rather than executed.
            // N2 FIX (CLI audit MAJOR): the captured value ends with CRLF and the pattern cannot
            // match it - so every real registry was rejected and install/update always failed.
            registry = (registry ?? "").Trim();
            if (!string.IsNullOrEmpty(registry) &&
                // F-C FIX (CLI final review): the whitelist still allowed ! and $, which the comment above named as rejected.
                // They are inert here (no delayed expansion) but the code should match its own description.
                !System.Text.RegularExpressions.Regex.IsMatch(registry, @"^https?://[A-Za-z0-9._~:/?#\[\]@*+,;=-]+$"))   // N3 FIX: & % ^ ! ( ) are cmd.exe metacharacters
            {
                return -3;   // -3 = refused: the registry value is not a plain URL
            }
            string args = "/c npm install -g " + (string.IsNullOrEmpty(registry) ? "" : "--registry " + registry + " ") + pkg;
            return RunExit("cmd.exe", args);
        }

        /// <summary>全局卸载 dsh。返回退出码，-1 = 未能执行。</summary>
        public int NpmUninstallGlobal() { return RunExit("cmd.exe", "/c npm uninstall -g @deepseek-ai/dsh"); }

        /// <summary>跑一个命令并返回退出码（-1 = 未能执行/超时）。</summary>
        private static int RunExit(string file, string args)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(file, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
                {
                    System.Threading.Tasks.Task<string> so = System.Threading.Tasks.Task.Run(delegate { return p.StandardOutput.ReadToEnd(); });
                    System.Threading.Tasks.Task<string> se = System.Threading.Tasks.Task.Run(delegate { return p.StandardError.ReadToEnd(); });
                    if (!p.WaitForExit(600000)) { try { p.Kill(); } catch { } return -1; }
                    System.Threading.Tasks.Task.WaitAll(so, se);
                    return p.ExitCode;
                }
            }
            catch { return -1; }
        }
        /// <summary>Windows 上不代装 Node（安装器负责）；返回 -1，由调用方如实说明。</summary>
        public int InstallNodeRuntime() { return -1; }

        /// <summary>列出可用版本（原样返回 npm 输出 ✓）。</summary>
        public string NpmViewVersions() { return WindowsShell.Capture("cmd.exe", "/c npm view @deepseek-ai/dsh versions 2>nul"); }

        public string NpmViewLatest() { return WindowsShell.Capture("cmd.exe", "/c npm view @deepseek-ai/dsh version 2>nul"); }
    }
}