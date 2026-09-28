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

        public BackupResult Create(string sourceDir, BackupKind kind)
        {
            try
            {
                string root = BackupsRoot;
                Directory.CreateDirectory(root);
                string dest = Path.Combine(root, "dsh-data-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + Dsht.Domain.Services.BackupPackage.Suffix(kind));
                int skipped = CopyTree(sourceDir, dest, true);
                // 保留策略：只清自动类（手动永久保留）
                try
                {
                    System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string>();
                    System.Collections.Generic.Dictionary<string, string> paths = new System.Collections.Generic.Dictionary<string, string>();
                    foreach (string d in Directory.GetDirectories(root))
                    {
                        string n = Path.GetFileName(d.TrimEnd('\\', '/'));
                        names.Add(n); paths[n] = d;
                    }
                    foreach (string victim in Dsht.Domain.Services.BackupRetention.SelectForDeletion(names, 3))
                    {
                        try { Directory.Delete(paths[victim], true); } catch { }
                    }
                }
                catch { }
                return new BackupResult(dest, skipped);
            }
            catch { return null; }
        }

        public void Delete(string dir)
        {
            try { if (!string.IsNullOrEmpty(dir)) Directory.Delete(dir, true); } catch { }
        }

        /// <summary>复现 v2.x 的 CopyTree（best-effort 模式）：跳过 node_modules / backup / dsh-data-* / reparse；
        /// 文件用 FileShare.ReadWrite|Delete 打开（被独占的文件才失败，正常读取中的文件可复制）；失败记数不中断。
        /// 返回被跳过的嵌套 dsh-data-* 目录数（供上层提示）。</summary>
        private static int CopyTree(string src, string dst, bool skipLocked)
        {
            int skippedNested = 0;
            src = src.TrimEnd('\\', '/'); dst = dst.TrimEnd('\\', '/');
            Directory.CreateDirectory(dst);
            string[] subs;
            try { subs = Directory.GetDirectories(src); } catch { subs = new string[0]; }
            foreach (string d in subs)
            {
                string name = Path.GetFileName(d.TrimEnd('\\', '/'));
                if (name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals("backup", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.StartsWith("dsh-data-", StringComparison.OrdinalIgnoreCase)) { skippedNested++; continue; }
                bool rep = false;
                try { rep = (File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0; } catch { }
                if (rep) continue;
                try { skippedNested += CopyTree(d, Path.Combine(dst, name), skipLocked); }
                catch { if (!skipLocked) throw; }
            }
            string[] files;
            try { files = Directory.GetFiles(src); } catch { files = new string[0]; }
            foreach (string f in files)
            {
                try
                {
                    if ((File.GetAttributes(f) & FileAttributes.ReparsePoint) != 0) continue;
                    using (FileStream s = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (FileStream t = new FileStream(Path.Combine(dst, Path.GetFileName(f)), FileMode.Create, FileAccess.Write, FileShare.None))
                        s.CopyTo(t);
                }
                catch { if (!skipLocked) throw; }
            }
            return skippedNested;
        }
    }
}
