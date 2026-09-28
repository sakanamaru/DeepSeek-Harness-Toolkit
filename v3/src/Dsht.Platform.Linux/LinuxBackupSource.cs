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
                    result.Add(new BackupEntry(name, d, Snapshot(d)));
                }
            }
            catch { }
            return result;
        }

        public static DirSnapshot Snapshot(string dir)
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
    }
}