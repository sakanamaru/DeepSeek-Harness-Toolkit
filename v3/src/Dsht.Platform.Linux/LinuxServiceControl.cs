using System;
using System.Diagnostics;
using Dsht.Domain.Abstractions;

namespace Dsht.Platform.Linux
{
    /// <summary>进程控制（Linux）：直接执行（dsh 是带 shebang 的可执行文件）；停止先 SIGTERM 再兜底强杀。</summary>
    public sealed class LinuxServiceControl : IServiceControl
    {
        public bool StartDetached(string fileName, string arguments, string workingDirectory, out int pid, out string error)
        {
            pid = 0; error = "";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(fileName, arguments);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                if (!string.IsNullOrEmpty(workingDirectory)) psi.WorkingDirectory = workingDirectory;
                Process p = Process.Start(psi);
                if (p == null) { error = "Process.Start 返回 null"; return false; }
                pid = p.Id;
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public bool Stop(int pid, out string error)
        {
            error = "";
            if (pid <= 0) { error = "PID 无效"; return false; }
            try
            {
                using (Process k = Process.Start(new ProcessStartInfo("kill", "-TERM " + pid) { UseShellExecute = false, CreateNoWindow = true }))
                {
                    k.WaitForExit(5000);
                }
                for (int i = 0; i < 20; i++)
                {
                    if (!Alive(pid)) return true;
                    System.Threading.Thread.Sleep(200);
                }
                return StopTree(pid, out error);
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public bool StopTree(int pid, out string error)
        {
            error = "";
            try
            {
                using (Process k = Process.Start(new ProcessStartInfo("kill", "-KILL " + pid) { UseShellExecute = false, CreateNoWindow = true }))
                {
                    k.WaitForExit(5000);
                }
                return !Alive(pid);
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        private static bool Alive(int pid)
        {
            try { return System.IO.Directory.Exists("/proc/" + pid); } catch { return false; }
        }
    }
}