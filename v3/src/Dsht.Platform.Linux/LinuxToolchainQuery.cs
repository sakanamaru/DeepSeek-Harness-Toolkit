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