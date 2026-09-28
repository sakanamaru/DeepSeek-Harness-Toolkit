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

        /// <summary>Quote a path when it contains spaces (setsid would otherwise split it).</summary>
        private static string QuoteIfNeeded(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.IndexOf(' ') < 0) return s;
            return "\"" + s + "\"";
        }

        public bool StartDetached(string fileName, string arguments, string workingDirectory, out int pid, out string error)
        {
            pid = 0; error = "";
            try
            {
                // setsid：新会话，彻底脱离调用方（真机 ssh 测试发现：不脱离时子进程占住 ssh 通道 ✗
                // → "Connection closed by remote host"；在脚本/CI 里同样会把管道占住导致挂起 ✗）
                ProcessStartInfo psi = new ProcessStartInfo("setsid", QuoteIfNeeded(fileName) + " " + arguments);
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

                /// <summary>Graceful stop: SIGTERM the whole PROCESS GROUP, wait, then SIGKILL the group.
        /// Killing only a single PID leaves the node children dsh spawned alive (platform-seam audit).</summary>
        public bool Stop(int pid, out string error)
        {
            error = "";
            if (pid <= 1) { error = "PID " + pid + " refused by safety guard"; return false; }
            string e1;
            if (KillGroup(pid, false, out e1)) return true;
            for (int i = 0; i < 20; i++) { if (!Alive(pid)) return true; System.Threading.Thread.Sleep(200); }
            return KillGroup(pid, true, out error);
        }

        /// <summary>End the whole process tree: SIGTERM the group, wait, then SIGKILL the group.
        /// Reports honestly when the process survives instead of claiming success.</summary>
        public bool StopTree(int pid, out string error)
        {
            error = "";
            if (pid <= 1) { error = "PID " + pid + " refused by safety guard"; return false; }
            string e1;
            KillGroup(pid, false, out e1);
            for (int i = 0; i < 20; i++) { if (!Alive(pid)) return true; System.Threading.Thread.Sleep(200); }
            return KillGroup(pid, true, out error);
        }

        /// <summary>Signal the whole process group (kill -SIG -pgid), falling back to the single PID.
        /// Returns whether the process is confirmed gone; otherwise error explains why.</summary>
        private static bool KillGroup(int pid, bool hard, out string error)
        {
            error = "";
            string sig = hard ? "-KILL" : "-TERM";
            string outp;
            int code = RunKill(sig + " -" + pid, out outp);
            if (code != 0) { code = RunKill(sig + " " + pid, out outp); }
            if (!Alive(pid)) return true;
            error = "process " + pid + " survived kill " + sig + " (exit " + code + (outp.Length > 0 ? ": " + outp.Trim() : "") + ")";
            return false;
        }

        private static int RunKill(string args, out string output)
        {
            output = "";
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("kill", args);
                psi.UseShellExecute = false; psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
                using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
                {
                    System.Threading.Tasks.Task<string> so = System.Threading.Tasks.Task.Run(delegate { return p.StandardOutput.ReadToEnd(); });
                    System.Threading.Tasks.Task<string> se = System.Threading.Tasks.Task.Run(delegate { return p.StandardError.ReadToEnd(); });
                    if (!p.WaitForExit(5000)) { try { p.Kill(); } catch { } return -1; }
                    output = (so.Result == null ? "" : so.Result) + (se.Result == null ? "" : se.Result);
                    return p.ExitCode;
                }
            }
            catch (Exception ex) { output = ex.Message; return -1; }
        }

        private static bool Alive(int pid)
        {
            try { return System.IO.Directory.Exists("/proc/" + pid); } catch { return false; }
        }
    }
}