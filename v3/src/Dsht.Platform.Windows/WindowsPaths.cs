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

                public string BackupsRoot
                {
                    get
                    {
                        // ★★★ **用户要求（2026-09-30）**：「第一次备份必须手动设置目录，避免卸载时删掉备份」✓✓
                        //   背景：默认备份根是 `StateDir/backup` ✓ 而 **Windows 上 StateDir 就是安装目录** ✗
                        //     → 备份**物理上躺在安装目录里** ✓（真机确认：575 个文件就在那里 ✓）
                        //     → 卸载时**理论上**会连它一起清 ✗（安装器侧已加保险：显式跳过 backup/ ✓✓）
                        //   现在：**可用 `DSH_MINATO_BACKUP_DIR` 指定备份根** ✓✓
                        //     · 设置后 → 备份放到你指定的地方 ✓ 放在安装目录之外才真正稳妥 ✓
                        //     · 未设置 → **保持原行为** ✓（向后兼容 ✓ 已有备份不会突然找不到 ✓✓）
                        //     · `backup --to <目录>` 会在本次运行里设置它 ✓✓（一次性的"手动指定目录" ✓）
                        string env = Environment.GetEnvironmentVariable("DSH_MINATO_BACKUP_DIR");
                        if (!string.IsNullOrWhiteSpace(env)) return env.Trim();
                        return Path.Combine(_stateDir, "backup");
                    }
                }

        /// <summary>数据根。**唯一一处刻意偏离 v2.x 的行为**：优先读 $DSH_HOME（Linux 侧同样支持），
        /// 以便在隔离数据根下安全测试写操作（真实 restore/export/delete）与多环境部署；
        /// 未设置时与 v2.x 完全一致（用户主目录/.dsh → %APPDATA%/.dsh → %LOCALAPPDATA%/.dsh）。</summary>
        public string DataRoot
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("DSH_HOME");
                if (!string.IsNullOrWhiteSpace(env)) return env.Trim();
                string[] candidates = DefaultDataRoots();
                for (int i = 0; i < candidates.Length; i++)
                {
                    try { if (Directory.Exists(candidates[i])) return candidates[i]; } catch { }
                }
                return candidates[0];
            }
        }

        /// <summary>默认数据根候选（顺序即 DataRoot 的自动探测顺序）。供 restore --apply 的隔离判定使用：
        /// 生效数据根等于其中任何一个即视为"默认位置"，拒绝真实写入。</summary>
        public static string[] DefaultDataRoots()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new string[]
            {
                Path.Combine(home, DataDirName),
                Path.Combine(appdata, DataDirName),
                Path.Combine(local, DataDirName)
            };
        }

        /// <summary>工作区自动探测（逐条对齐 v2.x 的 WorkspaceRoot）：exe 所在目录的**上两级**
        /// （exe 在 …\dsh-minato\ 时，工作区为 …\）；结果落在用户主目录/桌面/Windows/盘根
        /// 等明显不合理位置 → null（调用方改为手动输入或配置 `ws=`）。
        /// 注意：这里只做**自动探测**；`ws=` 配置优先由 CLI 经 WorkspaceResolver 处理（配置了但不存在 → null，不回退探测）。</summary>
        public string WorkspaceRoot
        {
            get
            {
                try
                {
                    string ws = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
                    return Dsht.Domain.Services.WorkspaceJudge.LooksLike(ws, ForbiddenWorkspaceRoots()) ? ws : null;
                }
                catch { return null; }
            }
        }

        /// <summary>自动探测时要拒绝的系统/用户级根目录（平台侧提供环境路径，领域层只做判定）。</summary>
        public static string[] ForbiddenWorkspaceRoots()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return new string[]
            {
                home,
                Path.GetDirectoryName(home),                                             // C:\Users 整级
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };
        }

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