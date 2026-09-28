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

        public string NpmViewLatest() { return WindowsShell.Capture("cmd.exe", "/c npm view @deepseek-ai/dsh version 2>nul"); }
    }
}