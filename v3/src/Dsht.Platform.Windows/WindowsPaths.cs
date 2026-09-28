using System;
using System.IO;
using Dsht.Domain.Abstractions;

namespace Dsht.Platform.Windows
{
    /// <summary>路径解析（Windows），是 StateDir / BackupsRoot / DataRoot 的唯一来源（其余平台类都从这里取，避免重复实现）。</summary>
    public sealed class WindowsPaths : IPaths
    {
        private const string DataDirName = ".dsh";
        private readonly string _stateDir;

        public WindowsPaths()
        {
            _stateDir = ResolveStateDir();
        }

        public string StateDir { get { return _stateDir; } }

        public string BackupsRoot { get { return Path.Combine(_stateDir, "backup"); } }

        /// <summary>数据根。**唯一一处刻意偏离 v2.x 的行为**：优先读 $DSH_HOME（Linux 侧同样支持），
        /// 以便在隔离数据根下安全测试写操作（真实 restore/export/delete）与多环境部署；
        /// 未设置时与 v2.x 完全一致（用户主目录/.dsh → %APPDATA%/.dsh → %LOCALAPPDATA%/.dsh）。</summary>
        public string DataRoot
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("DSH_HOME");
                if (!string.IsNullOrWhiteSpace(env)) return env.Trim();
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string[] candidates = new string[]
                {
                    Path.Combine(home, DataDirName),
                    Path.Combine(appdata, DataDirName),
                    Path.Combine(local, DataDirName)
                };
                for (int i = 0; i < candidates.Length; i++)
                {
                    try { if (Directory.Exists(candidates[i])) return candidates[i]; } catch { }
                }
                return candidates[0];
            }
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
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeepSeekHarnessLauncher");
            }
        }
    }
}