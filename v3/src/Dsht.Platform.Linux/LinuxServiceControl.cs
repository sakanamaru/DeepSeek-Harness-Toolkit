using System;
using System.Diagnostics;
using Dsht.Domain.Abstractions;

namespace Dsht.Platform.Linux
{
    /// <summary>进程控制（Linux）：直接执行（dsh 是带 shebang 的可执行文件）；停止先 SIGTERM 再兜底强杀。</summary>
    public sealed class LinuxServiceControl : IServiceControl
    {
        /// <summary>最近一次启动的子进程输出日志路径（失败时 CLI 会指向它 ✓）。</summary>
        public static string LastLogPath = "";

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
                // 必须重定向并抽干 ✗：不重定向子进程会占住调用方管道（脚本/CI/ssh 会挂住 ✗ —— 真机踩过）
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                // PATH 必须注入 ✗：dsh 是 `#!/usr/bin/env node` 的脚本，node 不在 PATH 上就报
                // "env: 'node': No such file or directory"（真机启动日志抓到的 ✗）
                try
                {
                    string home = Environment.GetEnvironmentVariable("HOME");
                    if (!string.IsNullOrEmpty(home))
                    {
                        string prefix = home + "/.local/node/bin:" + home + "/.npm-global/bin:" + home + "/.local/bin";
                        string cur = psi.EnvironmentVariables["PATH"];
                        psi.EnvironmentVariables["PATH"] = prefix + (string.IsNullOrEmpty(cur) ? "" : ":" + cur);
                    }
                }
                catch { }
                // 不继承调用方 stdio；并后台抽干，避免管道写满把子进程卡住 ✗
                if (!string.IsNullOrEmpty(workingDirectory)) psi.WorkingDirectory = workingDirectory;
                Process p = Process.Start(psi);
                if (p == null) { error = "Process.Start 返回 null"; return false; }
                LastLogPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dsh-minato-start.log");
                string logPath = LastLogPath;
                try { System.IO.File.WriteAllText(logPath, "== dsh-minato start " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ==" + Environment.NewLine); } catch { }
                System.Threading.Tasks.Task.Run(delegate { try { string o = p.StandardOutput.ReadToEnd(); if (o.Length > 0) System.IO.File.AppendAllText(logPath, o); } catch { } });
                System.Threading.Tasks.Task.Run(delegate { try { string e2 = p.StandardError.ReadToEnd(); if (e2.Length > 0) System.IO.File.AppendAllText(logPath, e2); } catch { } });
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