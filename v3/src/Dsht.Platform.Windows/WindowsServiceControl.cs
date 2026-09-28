using System;
using System.Diagnostics;
using Dsht.Domain.Abstractions;

namespace Dsht.Platform.Windows
{
    /// <summary>进程控制（Windows）：启动时用 cmd.exe 包装（npm 的 dsh 是 .cmd 垫片），停止时用 taskkill /T 杀整棵树。</summary>
    public sealed class WindowsServiceControl : IServiceControl
    {
        public bool StartDetached(string fileName, string arguments, string workingDirectory, out int pid, out string error)
        {
            pid = 0; error = "";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(fileName, arguments);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                // 不继承调用方 stdio；并后台抽干（否则子进程占住管道，脚本/CI 场景会挂住 ✗ —— 与 Linux 侧同类问题）
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                if (!string.IsNullOrEmpty(workingDirectory)) psi.WorkingDirectory = workingDirectory;
                Process p = Process.Start(psi);
                if (p == null) { error = "Process.Start 返回 null"; return false; }
                System.Threading.Tasks.Task.Run(delegate { try { p.StandardOutput.ReadToEnd(); } catch { } });
                System.Threading.Tasks.Task.Run(delegate { try { p.StandardError.ReadToEnd(); } catch { } });
                pid = p.Id;
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public bool Stop(int pid, out string error) { return StopTree(pid, out error); }

        public bool StopTree(int pid, out string error)
        {
            error = "";
            if (pid <= 1) { error = "PID " + pid + " 被安全保护拒绝（不可能是目标进程）"; return false; }
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("taskkill", "/T /F /PID " + pid);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process k = Process.Start(psi))
                {
                    string outp = k.StandardOutput.ReadToEnd();
                    string err = k.StandardError.ReadToEnd();
                    k.WaitForExit(15000);
                    if (k.ExitCode != 0) { error = (err + outp).Trim(); return false; }
                    return true;
                }
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }
    }
}