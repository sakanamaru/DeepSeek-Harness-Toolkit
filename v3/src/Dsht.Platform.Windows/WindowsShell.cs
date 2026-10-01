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
            // 120s, not 15s: this default also covers network queries (npm view), and a cold registry
            // lookup can easily exceed 15s - which showed up as a transient "could not list versions".
            // Matches the ceiling the Linux side already uses.
            return Capture(exe, args, 120000);
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
                    // ★ 第 2 轮抓到（回归 ✓✓）：v2 的 RunCapture 返回 so.Trim() ✓ 而这里没 trim ✗
                    //   → 版本字符串带回车换行 → installed == latest 永远为假 → 更新中心把"已是最新"报成"比最新还新" ✗
                    //   → 换行还会把 UPDATECENTER_ITEM 标记行劈成两行 → GUI 显示"最新 unknown" ✗
                    lock (sb) { return sb.ToString().Trim(); }
                }
            }
            catch { return ""; }
        }
    }
}
