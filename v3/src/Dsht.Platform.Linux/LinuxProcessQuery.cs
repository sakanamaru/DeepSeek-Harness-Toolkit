using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Dsht.Domain.Abstractions;

namespace Dsht.Platform.Linux
{
    /// <summary>进程观测（Linux）。
    ///   PidListeningOn：解析 `ss -ltnp` 的 pid= 字段（iproute2，主流发行版自带）
    ///   CommandLine  ：读 /proc/&lt;pid&gt;/cmdline（NUL 分隔 → 空格）
    ///   StartTime    ：`ps -o lstart= -p &lt;pid&gt;` + InvariantCulture 解析（时区随系统本地时间）
    ///   IsDshCommandLine：命令行含 "dsh"；否则若进程名含 node 用 HTTP 应答兜底（与 Windows 同语义）
    /// 诚实标注：本文件尚未在真机 Linux 上验收（本地只做了编译级验证）。</summary>
    public sealed class LinuxProcessQuery : IProcessQuery
    {
        private const string Q = "\"";
        private readonly LinuxHttpProbe _http;
        private readonly string _probeUrl;
        private readonly int _httpTimeoutMs;

        public LinuxProcessQuery(LinuxHttpProbe http, string probeUrl, int httpTimeoutMs)
        {
            _http = http;
            _probeUrl = probeUrl == null ? "" : probeUrl;
            _httpTimeoutMs = httpTimeoutMs;
        }

        /// <summary>按名字精确查进程（pgrep -x）。取不到就返回 false —— 不猜 ✓</summary>
        public bool AnyProcessNamed(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            try
            {
                string outp = Capture("pgrep", "-x " + Q + name + Q);
                return !string.IsNullOrEmpty(outp) && outp.Trim().Length > 0;
            }
            catch { return false; }
        }

        public int PidListeningOn(int port)
        {
            return ParseSsOutput(Capture("ss", "-ltnp"), port);
        }

        public string CommandLine(int pid)
        {
            if (pid <= 0) return "";
            try
            {
                string raw = File.ReadAllText("/proc/" + pid + "/cmdline");
                return raw.Replace('\0', ' ').Trim();
            }
            catch { return ""; }
        }

        public DateTime? StartTime(int pid)
        {
            if (pid <= 0) return null;
            try
            {
                string s = Capture("ps", "-o etimes= -p " + pid).Trim();
                if (s.Length == 0) return null;
                DateTime dt;
                int secs;
                if (int.TryParse(s, out secs) && secs >= 0) return DateTime.Now.AddSeconds(-secs);   // locale-independent
                if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) return dt;
                return null;
            }
            catch { return null; }
        }

        public bool IsDshCommandLine(int pid)
        {
            if (pid <= 0) return false;
            try
            {
                if (IsDshCommandLineText(CommandLine(pid))) return true;
                string pname = "";
                try { pname = Process.GetProcessById(pid).ProcessName ?? ""; } catch { }
                if (pname.IndexOf("node", StringComparison.OrdinalIgnoreCase) >= 0)
                    return _http != null && _http.Responds(_probeUrl, _httpTimeoutMs);
                return false;
            }
            catch { return false; }
        }

        public static bool IsDshCommandLineText(string cmdline)
        {
            if (string.IsNullOrWhiteSpace(cmdline)) return false;
            return cmdline.IndexOf("dsh", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>ss -ltnp 输出解析（纯函数，可单测）：本地地址列形如 127.0.0.1:3080 / *:3080 / [::]:3080，pid= 取进程号。</summary>
        public static int ParseSsOutput(string output, int port)
        {
            if (string.IsNullOrWhiteSpace(output)) return 0;
            foreach (string raw in output.Split('\n'))
            {
                string t = raw.Trim();
                if (t.Length == 0) continue;
                // 端口必须**精确匹配** ✗：子串匹配会让 "127.0.0.1:30800" 命中 3080 → 取到错 PID → stop 杀错进程
                // （平台缝审计抓到的阻塞项 ✗）。ss -ltnp 行格式：State Recv-Q Send-Q Local:Port Peer:Port Process
                string[] cols = Regex.Split(t, @"\s+");
                if (cols.Length < 5) continue;
                string local = cols[3];
                int colon = local.LastIndexOf(':');
                if (colon < 0) continue;
                int localPort;
                if (!int.TryParse(local.Substring(colon + 1), out localPort) || localPort != port) continue;
                Match m = Regex.Match(t, @"pid=(\d+)");
                if (!m.Success) continue;
                int pid;
                if (int.TryParse(m.Groups[1].Value, out pid) && pid > 0) return pid;
            }
            return 0;
        }

        private static string Capture(string exe, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi))
                {
                    if (p == null) return "";
                    // 并发读两个流 ✗：先读完 stdout 再读 stderr 会死锁（stderr 写满时互等 → 超时 → 空结果）
                    System.Threading.Tasks.Task<string> soT = System.Threading.Tasks.Task.Run(delegate { return p.StandardOutput.ReadToEnd(); });
                    System.Threading.Tasks.Task<string> seT = System.Threading.Tasks.Task.Run(delegate { return p.StandardError.ReadToEnd(); });
                    if (!p.WaitForExit(15000)) { try { p.Kill(); } catch { } return soT.IsCompleted ? soT.Result : ""; }
                    string outp = soT.Result;
                    seT.Wait(2000);
                    return outp;
                }
            }
            catch { return ""; }
        }
    }
}