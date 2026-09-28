using System;
using System.IO;
using Dsht.Domain.Abstractions;

namespace Dsht.Platform.Linux
{
    /// <summary>工具链版本查询（Linux）：直接执行 node/npm/dsh（无 cmd.exe 包装）。
    /// WhichDsh = 扫 PATH 找可执行文件 dsh；NpmRegistryConfig = npm config get registry。</summary>
    public sealed class LinuxToolchainQuery : IToolchainQuery
    {
        public string NodeVersion() { return Capture("node", "--version"); }

        public string NpmVersion() { return Capture("npm", "--version"); }

        public string DshVersion() { return Capture("dsh", "--version"); }

        public string WhichDsh()
        {
            try
            {
                string path = Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (string d in path.Split(':'))
                {
                    string dir = d.Trim();
                    if (dir.Length == 0) continue;
                    try
                    {
                        string f = Path.Combine(dir, "dsh");
                        if (File.Exists(f)) return f;
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        public string NpmRegistryConfig() { return Capture("npm", "config get registry"); }

        /// <summary>全局安装（Linux 直接执行 npm；registry 为空则用默认源）。返回退出码，-1 = 未能执行。</summary>
        public int NpmInstallGlobal(string pkg, string registry)
        {
            string args = "/c npm install -g " + (string.IsNullOrEmpty(registry) ? "" : "--registry " + registry + " ") + pkg;
            return RunExit("npm", args.Replace("/c ", ""));
        }

        /// <summary>全局卸载 dsh。返回退出码，-1 = 未能执行。</summary>
        public int NpmUninstallGlobal() { return RunExit("npm", "uninstall -g @deepseek-ai/dsh"); }

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
        public string NpmViewLatest() { return Capture("npm", "view @deepseek-ai/dsh version"); }

        private static string Capture(string exe, string args)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(exe, args);
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
                {
                    if (p == null) return "";
                    string outp = p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    p.WaitForExit(15000);
                    return outp;
                }
            }
            catch { return ""; }
        }
    }
}