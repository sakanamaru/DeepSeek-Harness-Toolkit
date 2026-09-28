using System;
using System.Collections.Generic;
using System.IO;
using Dsht.Domain.Abstractions;
using Dsht.Domain.Model;

namespace Dsht.Platform.Windows
{
    /// <summary>备份来源（Windows）。逐条对齐 v2.x：
    ///   · 备份根 = 状态目录/backup；状态目录 = exe 所在目录（可写时），否则 %APPDATA%\DeepSeekHarnessLauncher
    ///   · 列出 dsh-data-* 直接子目录，Array.Sort 升序（时间戳字典序=时间序）
    ///   · 目录大小 = 迭代栈遍历累加文件长度（与 v2.x DirSize 一致）
    ///   · 最后写入时间取目录时间；失败返回 null（呈现为 "(unknown)"）</summary>
    public sealed class WindowsBackupSource : IBackupSource
    {
        private readonly string _stateDir;

        public WindowsBackupSource(IPaths paths) { _stateDir = paths == null ? ResolveStateDir() : paths.StateDir; }

        public string StateDir { get { return _stateDir; } }

        public string BackupsRoot { get { return Path.Combine(_stateDir, "backup"); } }

        public List<BackupEntry> ListRaw()
        {
            List<BackupEntry> result = new List<BackupEntry>();
            try
            {
                string root = BackupsRoot;
                if (!Directory.Exists(root)) return result;
                string[] dirs = Directory.GetDirectories(root, "dsh-data-*");
                Array.Sort(dirs, StringComparer.Ordinal);
                foreach (string d in dirs)
                {
                    string name = Path.GetFileName(d.TrimEnd('\\', '/'));
                    result.Add(new BackupEntry(name, d, SnapshotDir(d)));
                }
            }
            catch { }
            return result;
        }

        /// <summary>读取目录快照（名字 + 直接子条目名）供领域层做有效性判定。</summary>
        public static DirSnapshot SnapshotDir(string dir)
        {
            string name = Path.GetFileName(dir.TrimEnd('\\', '/'));
            List<string> entries = new List<string>();
            try
            {
                foreach (string f in Directory.GetFiles(dir)) entries.Add(Path.GetFileName(f));
                foreach (string d in Directory.GetDirectories(dir)) entries.Add(Path.GetFileName(d));
            }
            catch { }
            return new DirSnapshot(name, entries.ToArray());
        }

        public long DirSize(string path)
        {
            long total = 0;
            try
            {
                Stack<string> stack = new Stack<string>();
                stack.Push(path);
                while (stack.Count > 0)
                {
                    string d = stack.Pop();
                    string[] files;
                    try { files = Directory.GetFiles(d); } catch { files = new string[0]; }
                    foreach (string f in files) { try { total += new FileInfo(f).Length; } catch { } }
                    string[] subs;
                    try { subs = Directory.GetDirectories(d); } catch { subs = new string[0]; }
                    foreach (string sd in subs) stack.Push(sd);
                }
            }
            catch { }
            return total;
        }

        public DateTime? LastWrite(string path)
        {
            try { return Directory.GetLastWriteTime(path); } catch { return null; }
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

        public DirSnapshot Snapshot(string dir) { return SnapshotDir(dir); }

        /// <summary>备份目录定位：自身有效则返回自身；否则若恰好一个 dsh-data-* 子目录且有效则返回它。</summary>
        public string Resolve(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return null;
                DirSnapshot self = SnapshotDir(path);
                System.Collections.Generic.List<DirSnapshot> subs = new System.Collections.Generic.List<DirSnapshot>();
                System.Collections.Generic.List<string> subPaths = new System.Collections.Generic.List<string>();
                if (Directory.Exists(path))
                {
                    foreach (string d in Directory.GetDirectories(path))
                    {
                        subs.Add(SnapshotDir(d));
                        subPaths.Add(d);
                    }
                }
                string name = Dsht.Domain.Services.BackupPackage.Resolve(self, subs.ToArray());
                if (name == null) return null;
                if (name == self.Name) return path;
                for (int i = 0; i < subs.Count; i++) { if (subs[i].Name == name) return subPaths[i]; }
                return null;
            }
            catch { return null; }
        }
    }
}
