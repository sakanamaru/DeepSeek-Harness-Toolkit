// dsh-minato GUI 启动器（用户要求："用 bat 启动 gui 有一点不优雅，套个 exe 壳子上个 logo" ✓✓）
// 设计要点（全部零第三方依赖 ✓）：
//   · /target:winexe → **不弹控制台黑框** ✓（这是 .cmd 最不优雅的地方 ✓）
//   · /win32icon:logo-icon.ico → 任务栏/资源管理器**显示自己的图标** ✓
//   · 启动同目录的 gui\dsht-gui.exe ✓，并把 DSHT_CLI 指给同目录的 CLI ✓
//   · **启动前校验 GUI 与 CLI 的指纹** ✓✓（用户要求："反银狐木马感染/伪造的机制，至少被感染无法运行" ✓）
//     银狐会**静态感染正常 exe** ✗ → 只拦 CLI 不够 ✗（GUI 被感染就绕过了 ✓）
//     → 壳这里核对 gui\dsht-gui.exe 与 dsh-minato.exe 的 SHA-256 vs 随包 hashes.txt ✓
//     → 不一致 → **弹窗警告 + 拒绝启动** ✓✓（措辞：说事实"已被改动" ✓ 说可能"可能被木马感染" ✓）
//   · 找不到 GUI / 找不到清单 → 分别给出**明确**提示 ✓（不静默退出 ✓）
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

internal static class Launcher
{
    [STAThread]
    private static int Main(string[] args)
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        string gui = Path.Combine(Path.Combine(dir, "gui"), "dsht-gui.exe");
        string cli = Path.Combine(dir, "dsh-minato.exe");
        string manifest = Path.Combine(dir, "hashes.txt");

        if (!File.Exists(gui))
        {
            Show("找不到 GUI", "找不到：" + gui + Environment.NewLine + Environment.NewLine +
                 "请把本启动器放在**完整包目录**里（与 gui\\ 目录同级），不要单独复制出去。");
            return 2;
        }

        // —— 指纹校验 ✓✓ ——
        string bad = CheckFingerprints(manifest, dir, gui, cli);
        if (bad != null)
        {
            Show("警告：文件已经被改动，可能被木马感染",
                "本工具的每个文件都有官方指纹（SHA-256）。下面这个文件的指纹和官方清单**对不上** ——" + Environment.NewLine +
                "说明它**被改过**。银狐一类木马正是这样干的：给正常程序打补丁。" + Environment.NewLine + Environment.NewLine +
                bad + Environment.NewLine + Environment.NewLine +
                "**已拒绝启动**（刻意的：宁可你打不开，也不让你在不知情的情况下运行被改过的程序）" + Environment.NewLine + Environment.NewLine +
                "请这样做：" + Environment.NewLine +
                "  1. 不要继续使用这个文件" + Environment.NewLine +
                "  2. 把它删掉（或先移到隔离目录）" + Environment.NewLine +
                "  3. 从官方 Releases 重新下载：" + Environment.NewLine +
                "     https://github.com/sakanamaru/dsh-minato/releases" + Environment.NewLine +
                "  4. 建议用杀毒软件全盘扫描一次（木马通常不止感染一个文件）");
            return 4;
        }

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(gui);
            psi.UseShellExecute = false;
            psi.WorkingDirectory = dir;
            if (File.Exists(cli)) psi.EnvironmentVariables["DSHT_CLI"] = cli;
            if (args != null && args.Length > 0) psi.Arguments = string.Join(" ", args);
            Process.Start(psi);
            return 0;
        }
        catch (Exception ex)
        {
            Show("启动 GUI 失败", ex.Message);
            return 3;
        }
    }

    /// <summary>核对指纹 ✓；返回 null = 通过（或没有清单 → **放行** ✓ 与 CLI 同语义 ✓），否则返回说明文字。</summary>
    private static string CheckFingerprints(string manifest, string dir, string gui, string cli)
    {
        try
        {
            if (!File.Exists(manifest)) return null;   // 无清单 → 放行 ✓（源码/单独复制属正常 ✓ 与 CLI 一致 ✓）
            string[] lines = File.ReadAllLines(manifest);
            string[] names = new string[] { "dsht-gui.exe", "dsh-minato.exe" };
            string[] paths = new string[] { gui, cli };
            for (int i = 0; i < names.Length; i++)
            {
                if (!File.Exists(paths[i])) continue;
                string want = FindHash(lines, names[i]);
                if (string.IsNullOrEmpty(want)) continue;   // 清单里没这个文件 → 跳过 ✓
                string got = Sha256(paths[i]);
                if (string.IsNullOrEmpty(got)) continue;    // 取不到哈希 → 放行 ✓（不能让校验把工具变成砖 ✓）
                if (!string.Equals(want, got, StringComparison.OrdinalIgnoreCase))
                    return "不一致的文件：" + paths[i].Substring(dir.Length).TrimStart('\\') + Environment.NewLine +
                           "  官方指纹：" + want + Environment.NewLine +
                           "  实际指纹：" + got;
            }
            return null;
        }
        catch { return null; }   // 校验本身出错 → 放行 ✓
    }

    private static string FindHash(string[] lines, string fileName)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            string t = lines[i] == null ? "" : lines[i].Trim();
            if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal)) continue;
            int sp = t.IndexOf(' ');
            if (sp <= 0) continue;
            if (string.Equals(t.Substring(sp + 1).Trim(), fileName, StringComparison.OrdinalIgnoreCase))
                return t.Substring(0, sp).Trim().ToLowerInvariant();
        }
        return null;
    }

    private static string Sha256(string path)
    {
        try
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(path))
            {
                byte[] h = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
        catch { return null; }
    }

    /// <summary>弹一个提示框 ✓。
    /// ✗ 原来用 `mshta` + javascript → **实测没弹出来** ✗（进程 0 个 ✓ 用户被拒绝后什么都看不到 ✗✗）
    /// ✓ 改用 WinForms 的 MessageBox ✓ —— .NET Framework 的**系统程序集** ✓ 不算第三方依赖 ✓✓</summary>
    private static void Show(string title, string body)
    {
        try
        {
            System.Windows.Forms.MessageBox.Show(body, title,
                System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
        }
        catch { }
    }
}
