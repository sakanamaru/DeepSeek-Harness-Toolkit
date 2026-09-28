using System;
using System.Diagnostics;
using Dsht.Domain.Abstractions;

namespace Dsht.Platform.Windows
{
    /// <summary>进程观测（Windows）。
    /// 逐条对齐 v2.x：FindPortPid = netstat -ano -p tcp + ParsePortPid；
    /// 监听身份 = 命令行含 "dsh"（忽略大小写），否则若进程名含 node 再用 HTTP 应答兜底。
    /// 与 v2.x 的差异：**不做 10 秒缓存**——v2.x 缓存是因为 GUI 每 3 秒轮询；V3 CLI 每次进程只探一次，缓存无收益。</summary>
    public sealed class WindowsProcessQuery : IProcessQuery
    {
        private readonly WindowsHttpProbe _http;
        private readonly string _probeUrl;
        private readonly int _httpTimeoutMs;

        public WindowsProcessQuery(WindowsHttpProbe http, string probeUrl, int httpTimeoutMs)
        {
            _http = http;
            _probeUrl = probeUrl == null ? "" : probeUrl;
            _httpTimeoutMs = httpTimeoutMs;
        }

        public int PidListeningOn(int port)
        {
            return ParsePortPid(WindowsShell.Capture("cmd.exe", "/c netstat -ano -p tcp"), port);
        }

        public bool IsDshCommandLine(int pid)
        {
            if (pid <= 0) return false;
            try
            {
                string cmd = WindowsShell.Capture("powershell.exe",
                    "-NoProfile -NonInteractive -Command \"Get-CimInstance Win32_Process -Filter 'ProcessId=" + pid + "' | Select-Object -ExpandProperty CommandLine\"");
                if (IsDshCommandLineText(cmd)) return true;
                string pname = "";
                try { pname = Process.GetProcessById(pid).ProcessName ?? ""; } catch { }
                if (pname.IndexOf("node", StringComparison.OrdinalIgnoreCase) >= 0)
                    return _http != null && _http.Responds(_probeUrl, _httpTimeoutMs);
                return false;
            }
            catch { return false; }
        }

        /// <summary>命令行是否属于 dsh（纯文本判定，可单测）。</summary>
        public static bool IsDshCommandLineText(string cmdline)
        {
            if (string.IsNullOrWhiteSpace(cmdline)) return false;
            return cmdline.IndexOf("dsh", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>netstat 输出解析（纯函数，可单测）。格式: TCP  127.0.0.1:3080  0.0.0.0:0  LISTENING  1234</summary>
        public static int ParsePortPid(string netstatOutput, int port)
        {
            if (string.IsNullOrWhiteSpace(netstatOutput)) return 0;
            string suffix = ":" + port;
            foreach (string raw in netstatOutput.Split('\n'))
            {
                string t = raw.Trim();
                if (t.Length == 0 || t.IndexOf("LISTENING", StringComparison.OrdinalIgnoreCase) < 0) continue;
                string[] parts = t.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) continue;
                if (!parts[1].EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
                int pid;
                if (int.TryParse(parts[parts.Length - 1], out pid) && pid > 0) return pid;
            }
            return 0;
        }
    }
}