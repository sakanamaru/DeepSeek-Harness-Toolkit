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
            // ★★★ **M-9 修复（安装器审计 MAJOR）** ✓✓
            //   ✗ 原来**只验 2 个文件**（`dsht-gui.exe` / `dsh-minato.exe`）✗✗
            //     → **`gui\dsht-gui.dll` 才是 GUI 的真身** ✓ 改它照样能启动 ✗
            //     → 而"被感染就无法运行"正是这道校验存在的**唯一理由** ✗（银狐给 GUI 打补丁那个场景 ✓）
            //   ✗ 而且 basename 回退**会选错条目** ✗（清单里同名文件在 `gui\` 和 `app-<ver>\gui\` 各有一份 ✓
            //     清单按路径排序 → 副本在前 ✓ → 取到副本 → **假报"被木马感染"** ✗✗）
            //   ✓ 现在：**逐条验证清单里每一个"磁盘上存在"的文件** ✓✓（精确相对路径 ✓ **没有回退** ✗）
            //     · 不在磁盘上 → 跳过 ✓（清单可能含未装的可选文件 ✓ 不能因此变砖 ✓）
            //     · 取不到哈希 → 跳过 ✓（权限/占用 ✓ 同上 ✓）
            //     · **一个都对不上 → 拒绝** ✓✓（清单与目录不匹配 = 被换过 ✓）
            //   ✓ 顺带：`gui`/`cli` 两个参数保留但不再用于查找 ✓（签名不动 ✓ 调用方不用改 ✓）
            int checkedCount = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i] == null ? "" : lines[i].Trim();
                if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal)) continue;
                int sp = t.IndexOf(' ');
                if (sp <= 0) continue;
                string want = t.Substring(0, sp).Trim().ToLowerInvariant();
                string rel = t.Substring(sp + 1).Trim().Replace('/', '\\');
                string full;
                try { full = Path.Combine(dir, rel); } catch { continue; }
                if (!File.Exists(full)) continue;   // 磁盘上没有 → 没得验 ✓
                string got = Sha256(full);
                if (string.IsNullOrEmpty(got)) continue;
                checkedCount++;
                if (!string.Equals(want, got, StringComparison.OrdinalIgnoreCase))
                    return "不一致的文件：" + rel + Environment.NewLine +
                           "  官方指纹：" + want + Environment.NewLine +
                           "  实际指纹：" + got;
            }
            if (checkedCount == 0)
                return "清单里**没有任何文件**能在安装目录里找到 ✗" + Environment.NewLine +
                       "  清单：" + manifest + Environment.NewLine +
                       "  目录：" + dir + Environment.NewLine +
                       "  → 清单与目录不匹配 ✓ **拒绝启动** ✓（这是被整体替换过的特征 ✓）";
            return null;
        }
        catch { return null; }   // 校验本身出错 → 放行 ✓（不能让校验把工具变成砖 ✓）
    }

    /// <summary>按**完整相对路径**精确找哈希 ✓✓（优先于 basename ✓
    /// 审计 M10 + 用户实测：basename 匹配会取到 `app-3.0.0\` 里的**副本** ✗ → 误判被篡改 → 拒绝启动 ✗）。</summary>
    private static string FindHashExact(string[] lines, string relPath)
    {
        if (string.IsNullOrEmpty(relPath)) return null;
        for (int i = 0; i < lines.Length; i++)
        {
            string t = lines[i] == null ? "" : lines[i].Trim();
            if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal)) continue;
            int sp = t.IndexOf(' ');
            if (sp <= 0) continue;
            string name = t.Substring(sp + 1).Trim().Replace('/', '\\');
            if (string.Equals(name, relPath, StringComparison.OrdinalIgnoreCase))
                return t.Substring(0, sp).Trim().ToLowerInvariant();
        }
        return null;
    }

    private static string FindHash(string[] lines, string fileName)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            string t = lines[i] == null ? "" : lines[i].Trim();
            if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal)) continue;
            int sp = t.IndexOf(' ');
            if (sp <= 0) continue;
            // ✗✗ 审计 #5：原来拿清单里的**整段名字**与裸文件名比 ✗
                //   而新清单写的是 `gui\dsht-gui.exe`（带反斜杠路径 ✓）→ **永远不相等** ✗
                //   → `gui\dsht-gui.exe` **再也不被校验** ✗✗ ← 正是"银狐给 GUI 打补丁"那个场景 ✓
                // ✓ 修：**取 basename 比对** ✓（`gui\dsht-gui.exe` 与 `dsht-gui.exe` 都认 ✓）
                string nameInManifest = t.Substring(sp + 1).Trim().Replace('/', '\\');
                int bs = nameInManifest.LastIndexOf('\\');
                string baseInManifest = bs >= 0 ? nameInManifest.Substring(bs + 1) : nameInManifest;
                if (string.Equals(baseInManifest, fileName, StringComparison.OrdinalIgnoreCase))
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
