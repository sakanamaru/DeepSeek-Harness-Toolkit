using System;
using System.Collections.Generic;
using System.IO;
using Dsht.Domain.Abstractions;
using Dsht.Domain.Model;
using Dsht.Domain.Services;

namespace Dsht.Platform.Shared
{
    /// <summary>两端备份实现逐字相同的那部分（架构审计 S2）。
    /// 动机：Windows 与 Linux 的实现有 310 行逐行相同（占 65%），一处修 bug 必须记得改两处，
    ///   而这已经出过问题（归一化修复只做在一端）。
    /// 做法：这些方法在两端逐字相同且全是 static，不可能依赖实例状态，
    ///   于是提成基类的方法，两端调用点一行都不用改。
    /// 链接：两个 csproj 用 Compile Include + Link 指向同一份源文件（与 MarkerText.cs 同一手法），
    ///   没有新程序集、没有新依赖。
    /// 纪律：每搬一块就编译 + 跑真实往返测试 + 11 项门槛。
    /// 进度：CopySibling ✓ · SnapshotDir ✓ · RestoreWorkspaces ✓ · RestoreOneWorkspace ✓ ·
    ///       DirSizeForManifest ✓ · CopyTree ✓（5/6 搬完（SnapshotDir 两端不同，留待单独处理） ✓✓）</summary>
    public abstract class BackupSourceCommon
    {
        /// <summary>复制备份包的同级旁挂文件（.manifest / .version）：存在才复制，尽力而为。
        /// 不这么做的话，export 之后标记就丢了，包到了别处无法核对完整性。</summary>
        protected static void CopySibling(string srcPkg, string dstPkg, string suffix)
        {
            try
            {
                string from = srcPkg.TrimEnd('\\', '/') + suffix;
                if (!System.IO.File.Exists(from)) return;
                System.IO.File.Copy(from, dstPkg.TrimEnd('\\', '/') + suffix, true);
            }
            catch { }
        }

        /// <summary>工作区恢复：新格式（子目录含 .dshws 标记）逐个恢复；否则整个 _workspace 视为一个工作区。</summary>
        protected static void RestoreWorkspaces(string wsRoot, string workspaceRoot, RestoreOutcome o)
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
        protected static void RestoreOneWorkspace(string srcDir, bool isNewFormat, string target, RestoreOutcome o)
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

        /// <summary>目录总字节数（仅用于完成标记 ✓ 容错 ✓）。</summary>
        protected static long DirSizeForManifest(string dir)
        {
            long total = 0;
            string[] fs;
            try { fs = Directory.GetFiles(dir); } catch { return 0; }
            for (int i = 0; i < fs.Length; i++) { try { total += new FileInfo(fs[i]).Length; } catch { } }
            string[] ds;
            try { ds = Directory.GetDirectories(dir); } catch { return total; }
            for (int i = 0; i < ds.Length; i++) total += DirSizeForManifest(ds[i]);
            return total;
        }

        protected static int CopyTree(string src, string dst, bool skipLocked)
        {
            int skippedNested = 0;
            src = src.TrimEnd('\\', '/'); dst = dst.TrimEnd('\\', '/');
            // ★★★ 架构审计抓到（MAJOR C3）：只在**源**侧跳过 reparse point ✗
            //   → 而**目标**侧的 junction / 符号链接会被**写穿** ✗✗
            //   → 数据根里一个名为 sessions 的 junction 会让恢复**写到根外** ✗（且没有任何包含性检查 ✗）
            // ✓ 现在：**目标侧也查** ✓✓ 是 reparse point 就拒绝写入（宁可失败也不写穿 ✓）
            bool dstIsLink = false;
            try { if (Directory.Exists(dst)) dstIsLink = (File.GetAttributes(dst) & FileAttributes.ReparsePoint) != 0; } catch { }
            if (dstIsLink) { if (!skipLocked) throw new IOException("destination is a reparse point: " + dst); return skippedNested; }
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
                    // ★ 同上：**目标文件**是 reparse point 也不能写穿 ✓✓（写之前查一次 ✓）
                    string dstFileGuard = Path.Combine(dst, Path.GetFileName(f));
                    try { if (File.Exists(dstFileGuard) && (File.GetAttributes(dstFileGuard) & FileAttributes.ReparsePoint) != 0) continue; } catch { }
                    using (FileStream s = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (FileStream t = new FileStream(Path.Combine(dst, Path.GetFileName(f)), FileMode.Create, FileAccess.Write, FileShare.None))
                        s.CopyTo(t);
                }
                catch { if (!skipLocked) throw; }
            }
            return skippedNested;
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
    }
}
