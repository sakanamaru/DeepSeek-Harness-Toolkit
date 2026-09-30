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
                // B2 FIX (audit MAJOR): the comment below claims the child is streamed into the start log,
                // but without these two the ReadLine loops throw at once, the child inherits the console,
                // and START_URL can never be found on Windows.
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                // 不继承调用方 stdio；并后台抽干（否则子进程占住管道，脚本/CI 场景会挂住 ✗ —— 与 Linux 侧同类问题）
                if (!string.IsNullOrEmpty(workingDirectory)) psi.WorkingDirectory = workingDirectory;
                Process p = Process.Start(psi);
                if (p == null) { error = "Process.Start 返回 null"; return false; }
                // ✗ 原来把输出**丢弃**（ReadToEnd 后不落盘）→ Windows 上取不到 dsh 打印的带 token 的 URL ✗
                //   （2026-09-30 真机：Windows 侧日志文件根本不存在 ✓ 而 CLI 又要从日志读 URL ✓）
                // 改成**逐行流式写日志** ✓ 与 Linux 侧同一路径 ✓ → 两个平台一致 ✓✓
                string logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dsh-minato-start.log");
                try { System.IO.File.WriteAllText(logPath, "== dsh-minato start " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine); } catch { }
                System.Threading.Tasks.Task.Run(delegate
                {
                    try { string ln; while ((ln = p.StandardOutput.ReadLine()) != null) { try { System.IO.File.AppendAllText(logPath, ln + Environment.NewLine); } catch { } } } catch { }
                });
                System.Threading.Tasks.Task.Run(delegate
                {
                    try { string ln2; while ((ln2 = p.StandardError.ReadLine()) != null) { try { System.IO.File.AppendAllText(logPath, ln2 + Environment.NewLine); } catch { } } } catch { }
                });
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
                psi.RedirectStandardOutput = true;   // B1 FIX (audit MAJOR): without this StandardOutput.ReadToEnd() throws, so every stop reported failure while taskkill had actually killed the process.
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                // B2 FIX (audit MAJOR): the comment below claims the child is streamed into the start log,
                // but without these two the ReadLine loops throw at once, the child inherits the console,
                // and START_URL can never be found on Windows.
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