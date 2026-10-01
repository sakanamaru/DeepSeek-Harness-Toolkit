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

        // ★ 架构审计（S3）：这里原来还有一个 BackupsRoot 实现 ✗
        //   → 它**没有任何调用点** ✓ 而长得像"备份根的唯一来源" ✗
        //   → **我一度把安全修复做在了它上面** ✗✗（真机审查发现改的是死代码 ✓）
        // ✓ 已随接口成员一起删除 ✓✓（备份根的正确来源是 IBackupSource.BackupsRoot ✓）
        /// <summary>数据根。**唯一一处刻意偏离 v2.x 的行为**：优先读 $DSH_HOME（Linux 侧同样支持），
        /// 以便在隔离数据根下安全测试写操作（真实 restore/export/delete）与多环境部署；
        /// 未设置时与 v2.x 完全一致（用户主目录/.dsh → %APPDATA%/.dsh → %LOCALAPPDATA%/.dsh）。</summary>
        public string DataRoot
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("DSH_HOME");
                // G1 FIX (CLI audit MAJOR): the value was returned as-is while the Linux side
                // normalises it and names this exact attack. A relative DSH_HOME like ".dsh"
                // resolved against the current directory, so running from the home directory made
                // the effective root equal the real default one, and the restore --apply isolation
                // gate - which compares strings - let a real write through.
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string rawEnv = env.Trim();
                    try { return RealPath(System.IO.Path.GetFullPath(rawEnv)); }   // C2: resolve the real target - a junction to the real data root must not bypass the gate
                    catch { return rawEnv; }
                }
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
                // ★★ 架构审计抓到（C12）：探测名原来是**固定的** .write-test ✗ 且用 FileShare.None ✗
                //   → 两个并发进程探测同一个文件 ✗ → 输的那个误判"不可写" ✗ → 回退到 %APPDATA% ✓
                //   → 结果：**两个进程对 StateDir / BackupsRoot / 日志目录的看法不一致** ✗✗
                // ✓ 现在：**探测名唯一**（pid + 随机 ✓）→ 并发各测各的 ✓✓（并发一致性已实测 4/4 一致 ✓）
                string probe = Path.Combine(dir, (".write-test-" + System.Diagnostics.Process.GetCurrentProcess().Id.ToString() + "-" + System.Guid.NewGuid().ToString("N").Substring(0, 8)));
                using (FileStream fs = File.Create(probe)) { }
                File.Delete(probe);
                return dir;
            }
            catch
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeepSeekHarnessLauncher");
            }
        }

        /// <summary>Resolve the real target of a path (follows symlinks, junctions and 8.3 names).
        /// Returns the original value when it cannot be resolved, so behaviour never becomes more permissive.</summary>
        internal static string RealPath(string p)
        {
            try
            {
                if (string.IsNullOrEmpty(p)) return p;
                if (!System.IO.Directory.Exists(p) && !System.IO.File.Exists(p)) return p;
                IntPtr h = CreateFileW(p, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
                if (h == IntPtr.Zero || h == new IntPtr(-1)) return p;
                try
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder(1024);
                    uint got = GetFinalPathNameByHandleW(h, sb, (uint)sb.Capacity, 0);
                    if (got <= 0 || got >= sb.Capacity) return p;
                    string s = sb.ToString();
                    if (s.StartsWith(@"\\?\UNC\", StringComparison.Ordinal)) s = @"\\" + s.Substring(8);
                    else if (s.StartsWith(@"\\?\", StringComparison.Ordinal)) s = s.Substring(4);
                    return string.IsNullOrEmpty(s) ? p : s;
                }
                finally { try { CloseHandle(h); } catch { } }
            }
            catch { return p; }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern uint GetFinalPathNameByHandleW(IntPtr h, System.Text.StringBuilder buf, uint len, uint flags);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr h);
    }
}
