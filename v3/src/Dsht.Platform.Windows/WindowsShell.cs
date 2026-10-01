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
                // ★★ 架构审计抓到（v2 对等）：v2 启动前会 ResolveExe + 补 node PATH ✗ 而 v3 没有 ✗
                //   → winget/npm 刚装完、当前会话 PATH 未刷新时 ✗ → 裸名 node/npm/dsh **找不到** ✗
                //   → Linux 侧有等价的 InjectPath 兜底 ✓ → **两边不一致** ✗
                // ✓ 现在：**移植 v2 的两步** ✓✓
                exe = ResolveExe(exe);
                ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                MergeNodePath(psi);
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

        /// <summary>解析可执行文件：带路径原样返回；裸名先查 PATH，再查常见安装目录；都找不到原样返回（交给系统报错 ✓）。
        /// ★ 从 v2.x 移植 ✓（架构审计：v3 丢了它 ✗ → winget 装完当前会话找不到命令 ✗）。</summary>
        public static string ResolveExe(string name)
        {
            try
            {
                if (string.IsNullOrEmpty(name)) return name;
                if (name.IndexOf(System.IO.Path.DirectorySeparatorChar) >= 0 || name.IndexOf('/') >= 0) return name;
                string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
                string[] dirs = pathEnv.Split(';');
                for (int i = 0; i < dirs.Length; i++)
                {
                    string d = dirs[i] == null ? "" : dirs[i].Trim();
                    if (d.Length == 0) continue;
                    try { string f = System.IO.Path.Combine(d, name); if (System.IO.File.Exists(f)) return f; } catch { }
                }
                string[] extra = new string[]
                {
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", name),
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "nodejs", name),
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", name)
                };
                for (int i = 0; i < extra.Length; i++) { try { if (System.IO.File.Exists(extra[i])) return extra[i]; } catch { } }
            }
            catch { }
            return name;
        }

        /// <summary>给被启动进程补充 node/npm 常用目录的 PATH ✓ —— 避免 winget 新装后当前会话 PATH 未刷新导致找不到命令。
        /// ★ 从 v2.x 移植 ✓。</summary>
        public static void MergeNodePath(ProcessStartInfo psi)
        {
            try
            {
                if (psi == null) return;
                System.Collections.Generic.List<string> add = new System.Collections.Generic.List<string>();
                add.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm"));
                add.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs"));
                add.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "nodejs"));
                string merged = string.Join(";", add.ToArray()) + ";" + (Environment.GetEnvironmentVariable("PATH") ?? "");
                psi.EnvironmentVariables["PATH"] = merged;
            }
            catch { }
        }
    }
}
