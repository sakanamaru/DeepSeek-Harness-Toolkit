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
        public string WorkspaceRoot { get { return null; } }

        private static string ResolveStateDir()
        {
            try
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                string probe = Path.Combine(dir, ".write-test");
                using (FileStream fs = File.Create(probe)) { }
                File.Delete(probe);
                return dir;
            }
            catch
            {
                string xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                if (string.IsNullOrWhiteSpace(xdg))
                {
                    string home = Environment.GetEnvironmentVariable("HOME");
                    if (string.IsNullOrWhiteSpace(home)) home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    xdg = Path.Combine(home, ".local", "share");
                }
                return Path.Combine(xdg, AppDirName);
            }
        }
    }
}