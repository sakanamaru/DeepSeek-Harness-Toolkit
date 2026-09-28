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

        public BackupResult Create(string sourceDir, BackupKind kind, int keep = 3, string workspaceRoot = null)
        {
            try
            {
                string root = BackupsRoot;
                Directory.CreateDirectory(root);
                string dest = Path.Combine(root, "dsh-data-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + Dsht.Domain.Services.BackupPackage.Suffix(kind));
                int skipped = CopyTree(sourceDir, dest, true);
                // Package the workspace too (written as _workspace/, which the restore side reads as the
                // legacy single-workspace layout and merges back into the workspace root). Guarded both
                // ways so a workspace nested in the data root (or the reverse) can never recurse.
                if (!string.IsNullOrEmpty(workspaceRoot) && Directory.Exists(workspaceRoot))
                {
                    try
                    {
                        string wsFull = Dsht.Domain.Services.PathUtil.TrimTrailingSep(workspaceRoot);
                        string dataFull = Dsht.Domain.Services.PathUtil.TrimTrailingSep(sourceDir);
                        bool wsInsideData = Dsht.Domain.Services.PathUtil.IsSubPath(dataFull, wsFull);
                        bool dataInsideWs = Dsht.Domain.Services.PathUtil.IsSubPath(wsFull, dataFull);
                        if (!wsInsideData && !dataInsideWs)
                            skipped += CopyTree(wsFull, Path.Combine(dest, "_workspace"), true);
                    }
                    catch { }
                }
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
                    foreach (string victim in Dsht.Domain.Services.BackupRetention.SelectForDeletion(names, keep <= 0 ? 3 : keep))
                    {
                        try { Directory.Delete(paths[victim], true); } catch { }
                    }
                }
                catch { }
                return new BackupResult(dest, skipped);
            }
            catch { return null; }
        }

        /// <summary>导出备份副本：复制到 dstDir/&lt;源目录名&gt;（只读源；best-effort 复制）。返回目标路径；失败返回 null。</summary>
        public string Export(string src, string dstDir)
        {
            try
            {
                if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(dstDir)) return null;
                Directory.CreateDirectory(dstDir);
                string target = Path.Combine(dstDir, Path.GetFileName(src.TrimEnd('\\', '/')));
                CopyTree(src, target, true);
                return target;
            }
            catch { return null; }
        }
        public void Delete(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            try
            {
                // 先清只读属性再删（对齐 v2.x 的 ClearReadOnlyRecursive 意图：只读文件不该阻碍删除）
                foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                }
                Directory.Delete(dir, true);
            }
            catch { }
        }

        /// <summary>真实合并恢复（与 Windows 实现同语义，复现 v2.x 的 RestoreFromSource + RestoreWorkspaces）：
        ///   顶层目录逐个 CopyTree（**恢复模式**：失败如实抛出）→ 顶层文件覆盖复制 → _workspace 下的工作区。
        /// 合并语义：目标端独有的文件不会被删除。</summary>
        public RestoreOutcome Restore(string backupDir, string dataRoot, string workspaceRoot)
        {
            RestoreOutcome o = new RestoreOutcome();
            try
            {
                string src = Dsht.Domain.Services.PathUtil.TrimTrailingSep(backupDir);
                string dst = Dsht.Domain.Services.PathUtil.TrimTrailingSep(dataRoot);
                Directory.CreateDirectory(dst);
                // 读不到备份包内容时**必须失败**：静默当成"空包"会打印 RESTORE_OK 却一个文件都没恢复
                string[] dirs = Directory.GetDirectories(src);
                foreach (string d in dirs)
                {
                    string name = Path.GetFileName(d.TrimEnd('\\', '/'));
                    if (name == "_workspace") continue;                       // v2.x：工作区单独处理
                    CopyTree(d, Path.Combine(dst, name), false);
                    o.TopDirs++;
                }
                string[] files = Directory.GetFiles(src);
                foreach (string f in files)
                {
                    File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
                    o.TopFiles++;
                }
                string ws = Path.Combine(src, "_workspace");
                if (Directory.Exists(ws)) RestoreWorkspaces(ws, workspaceRoot, o);
                o.Ok = true;
            }
            catch (Exception ex) { o.Ok = false; o.Error = ex.Message; }
            return o;
        }

        /// <summary>工作区恢复：新格式（子目录含 .dshws 标记）逐个恢复；否则整个 _workspace 视为一个工作区。</summary>
        private static void RestoreWorkspaces(string wsRoot, string workspaceRoot, RestoreOutcome o)
        {
            string[] subs = Directory.GetDirectories(wsRoot);   // 读不到 → 交给外层 catch（不静默跳过）
            bool anyNew = false;
            for (int i = 0; i < subs.Length; i++) { if (File.Exists(Path.Combine(subs[i], ".dshws"))) { anyNew = true; break; } }
            if (anyNew)
            {
                for (int i = 0; i < subs.Length; i++)
                {
                    if (!File.Exists(Path.Combine(subs[i], ".dshws"))) { o.WorkspacesUnrecognized++; continue; }
                    RestoreOneWorkspace(subs[i], true, workspaceRoot, o);
                }
            }
            else RestoreOneWorkspace(wsRoot, false, workspaceRoot, o);
        }

        /// <summary>单个工作区恢复。非交互语义（v2.x 的 inputEof=true）：目标固定取自动探测到的工作区根，
        /// 取不到或不存在就跳过——不询问、不自定义、不删除目标端独有文件。</summary>
        private static void RestoreOneWorkspace(string srcDir, bool isNewFormat, string target, RestoreOutcome o)
        {
            if (string.IsNullOrEmpty(target) || !Directory.Exists(target)) { o.WorkspacesSkipped++; return; }
            if (isNewFormat)
            {
                foreach (string d in Directory.GetDirectories(srcDir))
                    CopyTree(d, Path.Combine(target, Path.GetFileName(d.TrimEnd('\\', '/'))), false);
                foreach (string f in Directory.GetFiles(srcDir))
                    if (Path.GetFileName(f) != ".dshws") File.Copy(f, Path.Combine(target, Path.GetFileName(f)), true);
            }
            else CopyTree(srcDir, target, false);
            o.WorkspacesRestored++;
        }

        /// <summary>复现 v2.x 的 CopyTree（best-effort 模式）：跳过 node_modules / backup / dsh-data-* / reparse；
        /// 文件用 FileShare.ReadWrite|Delete 打开（被独占的文件才失败，正常读取中的文件可复制）；失败记数不中断。
        /// 返回被跳过的嵌套 dsh-data-* 目录数（供上层提示）。skipLocked=false 时（恢复模式）失败如实抛出。</summary>
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
