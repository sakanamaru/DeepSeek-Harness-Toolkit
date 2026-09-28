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
                // setsid：新会话，彻底脱离调用方（真机 ssh 测试发现：不脱离时子进程占住 ssh 通道 ✗
                // → "Connection closed by remote host"；在脚本/CI 里同样会把管道占住导致挂起 ✗）
                ProcessStartInfo psi = new ProcessStartInfo("setsid", fileName + " " + arguments);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                // 不继承调用方 stdio；并后台抽干，避免管道写满把子进程卡住 ✗
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                if (!string.IsNullOrEmpty(workingDirectory)) psi.WorkingDirectory = workingDirectory;
                Process p = Process.Start(psi);
                if (p == null) { error = "Process.Start 返回 null"; return false; }
                System.Threading.Tasks.Task.Run(delegate { try { p.StandardOutput.ReadToEnd(); } catch { } });
                System.Threading.Tasks.Task.Run(delegate { try { p.StandardError.ReadToEnd(); } catch { } });
                pid = p.Id;   // setsid 通常直接 exec 目标程序 → PID 即目标；即便不是，stop 也是按端口观测取 PID ✓
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public bool Stop(int pid, out string error)
        {
            error = "";
            if (pid <= 1) { error = "PID " + pid + " 被安全保护拒绝（不可能是目标进程）"; return false; }
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