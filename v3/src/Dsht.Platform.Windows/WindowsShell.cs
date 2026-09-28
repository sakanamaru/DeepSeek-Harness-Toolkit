using System;
using System.Diagnostics;
using System.Text;

namespace Dsht.Platform.Windows
{
    /// <summary>最小进程调用（对应 v2.x 的 RunCapture 语义）：捕获 stdout+stderr，超时返回空串。
    /// 只做平台实现需要的最小能力，不引入任何第三方依赖。</summary>
    internal static class WindowsShell
    {
        public static string Capture(string exe, string args)
        {
            return Capture(exe, args, 15000);
        }

        public static string Capture(string exe, string args, int timeoutMs)
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
                    StringBuilder sb = new StringBuilder();
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) { lock (sb) { sb.AppendLine(e.Data); } } };
                    p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) { lock (sb) { sb.AppendLine(e.Data); } } };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return ""; }
                    try { p.WaitForExit(); } catch { }
                    lock (sb) { return sb.ToString(); }
                }
            }
            catch { return ""; }
        }
    }
}