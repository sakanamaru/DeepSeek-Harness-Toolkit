using System;
using System.Collections.Generic;
using System.IO;
using Dsht.Domain.Abstractions;
using Dsht.Domain.Model;

namespace Dsht.Platform.Linux
{
    /// <summary>备份来源（Linux）：与 Windows 实现同语义（备份根 = 状态目录/backup；dsh-data-* 升序；目录大小递归累加）。</summary>
    public sealed class LinuxBackupSource : IBackupSource
    {
        private readonly string _stateDir;

        public LinuxBackupSource(IPaths paths) { _stateDir = paths == null ? AppDomain.CurrentDomain.BaseDirectory : paths.StateDir; }

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

        public long DirSize(string path) { return new LinuxFileSystemQuery().DirSize(path); }

        public DateTime? LastWrite(string path)
        {
            try { return Directory.GetLastWriteTime(path); } catch { return null; }
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
