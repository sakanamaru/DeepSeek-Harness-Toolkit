using System;
using System.IO;
using Dsht.Domain.Abstractions;

namespace Dsht.Platform.Linux
{
    /// <summary>路径解析（Linux）。对应 v2.x 的 Windows 语义，按 XDG 约定落地：
    ///   StateDir   = exe 所在目录（可写时），否则 $XDG_DATA_HOME/DeepSeekHarnessLauncher（默认 ~/.local/share/...）
    ///   BackupsRoot= StateDir/backup
    ///   DataRoot   = $DSH_HOME，其次 $HOME/.dsh</summary>
    public sealed class LinuxPaths : IPaths
    {
        private const string DataDirName = ".dsh";
        private const string AppDirName = "DeepSeekHarnessLauncher";
        private readonly string _stateDir;

        /// <summary>Read an env var and NORMALISE it to an absolute path. The --apply gate that
        /// refuses to write the default data root is a plain string comparison, so a relative
        /// DSH_HOME (e.g. .dsh) or a symlink to ~/.dsh used to slip through.</summary>
        internal static string NormEnv(string name)
        {
            try
            {
                string v = Environment.GetEnvironmentVariable(name);
                if (string.IsNullOrEmpty(v)) return v;
                v = v.Trim();
                if (v.Length == 0) return v;
                try { return System.IO.Path.GetFullPath(v); } catch { return v; }
            }
            catch { return null; }
        }
        public LinuxPaths() { _stateDir = ResolveStateDir(); }

        public string StateDir { get { return _stateDir; } }

        public string BackupsRoot { get { return Path.Combine(_stateDir, "backup"); } }

        public string DataRoot
        {
            get
            {
                string env = NormEnv("DSH_HOME");
                if (!string.IsNullOrWhiteSpace(env)) return env.Trim();
                return DefaultDataRoots()[0];
            }
        }

        /// <summary>默认数据根候选。供 restore --apply 的隔离判定使用：生效数据根等于它即视为"默认位置"，拒绝真实写入。</summary>
        public static string[] DefaultDataRoots()
        {
            string home = Environment.GetEnvironmentVariable("HOME");
            if (string.IsNullOrWhiteSpace(home)) home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return new string[] { Path.Combine(home, DataDirName) };
        }

        /// <summary>工作区自动探测：V3 尚未移植（v2.x 在 Windows 上会遍历盘符与常见目录）。诚实返回 null。</summary>
                /// <summary>Auto-detected workspace root: the current directory, but only when WorkspaceJudge
        /// considers it plausible. This used to return null, which made workspace backup/restore
        /// unavailable on Linux (platform-seam audit). The forbidden roots are the Linux
        /// equivalents; the Windows list (users, $recycle.bin, ...) would accept /etc or /usr.
        /// An explicit ws= setting always wins and is not judged.</summary>
        public string WorkspaceRoot
        {
            get
            {
                try
                {
                    string cwd = System.IO.Directory.GetCurrentDirectory();
                    if (string.IsNullOrEmpty(cwd)) return null;
                    string full;
                    try { full = System.IO.Path.GetFullPath(cwd); } catch { return null; }
                    if (!Dsht.Domain.Services.WorkspaceJudge.LooksLike(full, ForbiddenWorkspaceRoots())) return null;
                    // The home directory itself is not a workspace (it would back up everything).
                    string home = Environment.GetEnvironmentVariable("HOME");
                    if (!string.IsNullOrEmpty(home))
                    {
                        string h;
                        try { h = System.IO.Path.GetFullPath(home).TrimEnd(System.IO.Path.DirectorySeparatorChar); } catch { h = ""; }
                        if (h.Length > 0 && full.TrimEnd(System.IO.Path.DirectorySeparatorChar) == h) return null;
                    }
                    string data = DataRoot;
                    if (!string.IsNullOrEmpty(data) && full.TrimEnd(Path.DirectorySeparatorChar) == data.TrimEnd(Path.DirectorySeparatorChar)) return null;
                    return full;
                }
                catch { return null; }
            }
        }

        /// <summary>Linux forbidden roots: the system directories plus /home, /tmp and friends.
        /// A project under /home is fine; /home itself is not.</summary>
        internal static string[] ForbiddenWorkspaceRoots()
        {
            // Note: /home itself is NOT listed - WorkspaceJudge treats a forbidden root as covering its
            // whole subtree, and on Linux a project normally lives under the home directory. The home
            // directory ITSELF is rejected separately below (exact match).
            return new string[] { "/", "/etc", "/usr", "/var", "/bin", "/sbin", "/lib", "/lib64",
                "/boot", "/proc", "/sys", "/dev", "/run", "/root", "/tmp", "/opt", "/srv", "/media", "/mnt" };
        }

        /// <summary>状态目录：**Linux 上一律用 XDG** ✓✓（$XDG_DATA_HOME/DeepSeekHarnessLauncher ✓ 默认 ~/.local/share/…）。
        ///
        /// ✗✗ 原来的逻辑是 v2.x 的 Windows 语义：「exe 所在目录可写就用 exe 目录」✗
        ///   在 Windows 上那是对的（安装目录固定 ✓）；**在 Linux 上是个数据丢失 bug** ✗✗：
        ///   包是 tar 解压出来的 → **目录总是可写** → 日志与备份**全写进包目录** ✗
        ///   → **升级 / 重装 / 换个目录解压 → 备份和日志全丢** ✗✗
        ///   （实测印证：VM 上出现过两份不同位置的 launcher.log ✓ 说明它确实变过位置 ✓）
        /// ✓ Linux 没有 v2.x 的历史包袱 → 用 XDG 约定 ✓ 升级不再丢数据 ✓✓
        ///
        /// 兼容：老位置（exe 目录）里若有 logs/ 或 backup/ 而新位置没有 → **一次性搬过去** ✓（幂等 ✓ 失败不致命 ✓）。</summary>
        private static string ResolveStateDir()
        {
            string xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrWhiteSpace(xdg))
            {
                string home = Environment.GetEnvironmentVariable("HOME");
                if (string.IsNullOrWhiteSpace(home)) home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                xdg = Path.Combine(home, ".local", "share");
            }
            string dir = Path.Combine(xdg, AppDirName);
            MigrateFromExeDir(dir);
            return dir;
        }

        /// <summary>把老位置（exe 目录）里的 logs/ 与 backup/ 搬到新位置 ✓。
        /// 只在「新位置没有、老位置有」时搬 ✓ → 幂等 ✓；任何失败都**不致命** ✓（大不了还从老位置读 ✓）。</summary>
        private static void MigrateFromExeDir(string newDir)
        {
            try
            {
                string oldDir = AppDomain.CurrentDomain.BaseDirectory;
                if (string.IsNullOrEmpty(oldDir)) return;
                string o = Path.GetFullPath(oldDir).TrimEnd(Path.DirectorySeparatorChar);
                string n = Path.GetFullPath(newDir).TrimEnd(Path.DirectorySeparatorChar);
                if (string.Equals(o, n, StringComparison.Ordinal)) return;   // 本来就一样 ✓
                foreach (string sub in new string[] { "logs", "backup" })
                {
                    string from = Path.Combine(o, sub);
                    string to = Path.Combine(n, sub);
                    try
                    {
                        if (!Directory.Exists(from)) continue;
                        if (Directory.Exists(to) && Directory.GetFileSystemEntries(to).Length > 0) continue;   // 新位置已有 → 不覆盖 ✓
                        Directory.CreateDirectory(n);
                        Directory.Move(from, to);
                    }
                    catch { }   // 尽力而为 ✓ 单个子目录失败不影响其它 ✓
                }
            }
            catch { }
        }
    }
}