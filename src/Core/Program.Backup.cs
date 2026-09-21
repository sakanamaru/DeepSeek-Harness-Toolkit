using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

partial class Program
{

    /// <summary>备份目录格式校验：目录名须为本工具生成的 "dsh-data-&lt;时间戳&gt;[后缀]" 形式，
    /// 且内容含 dsh 数据特征或工作区子目录（_workspace）。仅目录存在不算数。</summary>
    static bool IsValidBackupDir(string dir)
    {
        try
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
            string name = Path.GetFileName(dir.TrimEnd('\\'));
            if (!name.StartsWith("dsh-data-", StringComparison.OrdinalIgnoreCase)) return false;
            if (LooksLikeDshData(dir)) return true;
            string ws = Path.Combine(dir, "_workspace");
            if (Directory.Exists(ws)) return true;
            return false;
        }
        catch { return false; }
    }


    static void BackupMenu()
    {
        while (true)
        {
            SafeClear();
            Banner();
            Console.WriteLine();
            CL(ConsoleColor.White, "  1) " + T("备份数据", "Backup data"));
            CL(ConsoleColor.White, "  2) " + T("恢复数据", "Restore data"));
            CL(ConsoleColor.White, "  3) " + T("导入备份（其他电脑）", "Import backup (other PC)"));
            CL(ConsoleColor.White, "  4) " + T("打开备份文件夹", "Open backup folder"));
            CL(ConsoleColor.White, "  0) " + T("返回", "Back"));
            Console.WriteLine();
            Console.Write("  > ");
            string k = ReadLineTrim();
            if (k == "1") BackupData();
            else if (k == "2") RestoreData();
            else if (k == "3") ImportBackup();
            else if (k == "4") OpenBackupFolder();
            else if (k == "0" || k == "q") return;
            else if (k == "" && inputEof) return;   // 输入流已结束（如管道测试），避免死循环
            else Warn(T("无效输入。", "Invalid input."));
        }
    }


    static string BackupsRoot() { return Path.Combine(StateDir, "backup"); }


    static void BackupData()
    {
        string src = DataRoot();
        if (!Directory.Exists(src)) { Warn(T("未找到数据目录：" + src, "Data directory not found: " + src)); Pause(); return; }
        var wsList = new List<string>();
        string guess = WorkspaceRoot();
        if (guess != null && Directory.Exists(guess))
        {
            Console.Write(T("  检测到工作区（" + guess + "）。是否同时备份？输入 y 包含：",
                            "  Workspace detected (" + guess + "). Include it? Type y: "));
            string addAsk = ReadLineTrim();
            if (addAsk == "y" || addAsk == "Y") wsList.Add(guess);
        }
        // 多工作区：逐个输入路径，直接回车结束
        while (true)
        {
            Console.Write(T("  输入要附加备份的工作区路径（直接回车结束）：",
                            "  Workspace folder path to include (Enter to finish): "));
            string p = ReadLineTrim().Trim().Trim('"');
            if (p.Length == 0) break;
            string full = null;
            try { full = Path.GetFullPath(p); } catch { full = null; }
            if (full == null) { Warn(T("路径无效，请重新输入。", "Invalid path, try again.")); continue; }
            // M-7：手动输入同样过工作区黑名单（盘根/用户主目录/系统目录等整盘复制风险），命中需显式二次确认
            if (!LooksLikeWorkspace(full))
            {
                Warn(T("该路径位于系统/用户目录（盘根、用户主目录、Windows、Program Files 等），整目录备份可能包含大量无关甚至敏感文件。",
                        "This path is under a system/user directory (drive root, user profile, Windows, Program Files, ...); backing it up whole may include unrelated or sensitive files."));
                CL(ConsoleColor.White, T("  仍要包含该目录吗？输入 yes 确认（其他键跳过）：", "  Include it anyway? Type yes to confirm (anything else skips): "));
                string c2 = ReadLineTrim().Trim();
                if (c2 != "yes") { Warn(T("已跳过该目录。", "Directory skipped.")); continue; }
            }
            if (!Directory.Exists(full)) { Warn(T("目录不存在：" + p + "（直接回车可结束）。", "Not found: " + p + " (Enter to finish).")); continue; }
            bool dup = false, nested = false;
            foreach (string e in wsList)
            {
                string ee = e.TrimEnd('\\');
                string ff = full.TrimEnd('\\');
                if (string.Equals(ee, ff, StringComparison.OrdinalIgnoreCase)) { dup = true; break; }
                if (ff.StartsWith(ee + "\\", StringComparison.OrdinalIgnoreCase) ||
                    ee.StartsWith(ff + "\\", StringComparison.OrdinalIgnoreCase)) { nested = true; break; }
            }
            if (dup) Warn(T("该目录已在列表中，跳过：" + full, "Already in the list, skipped: " + full));
            else if (nested) Warn(T("该目录位于已选工作区之内，跳过：" + full, "Inside an already-selected workspace, skipped: " + full));
            else { wsList.Add(full); Success(T("已添加工作区：" + full, "Workspace added: " + full)); }
        }
        Info(T("正在备份（自动跳过 node_modules）...", "Backing up (skipping node_modules)..."));
        string bk = DoBackup(src, wsList, BackupKind.Manual);   // 用户主动备份（手动，永久保留）
        if (bk != null)
        {
            Success(T("备份完成：" + bk, "Backup done: " + bk));
            if (wsList.Count > 0) Info(T("已包含 " + wsList.Count + " 个工作区副本（备份包 _workspace 下）。", wsList.Count + " workspace(s) included (under _workspace)."));
        }
        else Error(T("备份失败。详情见 logs\\launcher.log", "Backup failed. See logs\\launcher.log"));
        Pause();
    }


    static void RestoreData()
    {
        string root = BackupsRoot();
        if (!Directory.Exists(root)) { Warn(T("没有找到任何备份。", "No backups found.")); Pause(); return; }
        string[] dirs = Directory.GetDirectories(root);
        Array.Sort(dirs);
        Array.Reverse(dirs);
        if (dirs.Length == 0) { Warn(T("备份目录为空，尚无任何备份。", "Backup folder is empty; no backups yet.")); Pause(); return; }
        Console.WriteLine();
        for (int i = 0; i < dirs.Length; i++)
            CL(ConsoleColor.White, "  " + (i + 1) + ") " + Path.GetFileName(dirs[i]));
        Console.WriteLine();
        Console.Write(T("  选择要恢复的备份序号（回车=最新）：", "  Choose backup number (Enter = latest): "));
        string sel = ReadLineTrim();
        int idx = 0;
        if (sel.Length > 0 && !int.TryParse(sel, out idx)) { Warn(T("输入无效。", "Invalid input.")); return; }
        if (idx < 1 || idx > dirs.Length) idx = 1;
        string bk = dirs[idx - 1];
        // 恢复前校验备份格式：目录名必须 dsh-data-* 且含数据特征/工作区（防误把任意目录当备份恢复）
        if (!IsValidBackupDir(bk))
        {
            Warn(T("所选目录不是有效的备份包（应为 dsh-data-时间戳 格式且含数据）：" + Path.GetFileName(bk) +
                   "\n  已取消恢复，请检查备份目录。", "Not a valid backup package (expected dsh-data-TIMESTAMP with data): " +
                   Path.GetFileName(bk) + "\n  Restore cancelled; check the backup folder."));
            Pause();
            return;
        }
        string dst = DataRoot();
        Console.Write(T("  恢复将覆盖当前数据（建议先关闭 dsh web）。确认？输入 y 继续：",
                        "  Restore overwrites current data (close dsh web first). Type y to continue: "));
        string restoreAsk = ReadLineTrim();
        if (restoreAsk != "y" && restoreAsk != "Y") { Warn(T("已取消。", "Cancelled.")); return; }
        // v2.1 安全：dsh 运行中拒绝恢复（与卸载/清除一致，防止覆盖正在使用的数据）
        if (ProbeService() != ServiceState.Down)
        {
            Error(T("dsh web 仍在运行，数据被占用无法安全恢复。\n  请先关闭 dsh web，再重新执行恢复。",
                    "dsh web is still running; data is in use and cannot be restored safely.\n  Close the dsh web window first, then retry the restore."));
            Pause();
            return;
        }
        if (Directory.Exists(dst))
        {
            Info(T("恢复前自动备份当前数据...", "Auto-backing up current data before restore..."));
            string preBk = DoBackup(dst, null, BackupKind.PreRestore);
            if (preBk == null)
            {
                Error(T("恢复前自动备份失败，已中止恢复（请先手动备份或检查磁盘空间）。", "Pre-restore backup failed; restore aborted (back up manually or check disk space first)."));
                Pause();
                return;
            }
        }
        RestoreFromSource(bk);
        Pause();
    }


    /// <summary>备份来源：Manual=用户主动备份（永久保留）；Auto/Pre*=系统自动产生（参与保留策略清理）。</summary>
    public enum BackupKind { Manual, Auto, PreRestore, PreImport, PreWipe, PreUpdate }


    /// <summary>自动类备份目录名的来源后缀（手动无后缀，兼容旧版产物）。</summary>
    static string BackupSuffix(BackupKind k)
    {
        switch (k)
        {
            case BackupKind.Auto: return "-auto";
            case BackupKind.PreRestore: return "-pre-restore";
            case BackupKind.PreImport: return "-pre-import";
            case BackupKind.PreWipe: return "-pre-wipe";
            case BackupKind.PreUpdate: return "-pre-update";
            default: return "";
        }
    }


    static void CopyTree(string src, string dst) { CopyTree(src, dst, false); }


    static void CopyTree(string src, string dst, bool skipLocked)
    {
        src = TrimTrailingSep(src); dst = TrimTrailingSep(dst);
        Directory.CreateDirectory(P(dst));
        int skippedNested = 0;   // L-7：统计被跳过的 dsh-data-* 嵌套备份包，最后统一 Warn 提示（避免静默丢用户数据）
        foreach (string d in Directory.GetDirectories(P(src)))
        {
            string name = Path.GetFileName(TrimP(d));
            if (name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)) continue;   // 依赖可重装，备份时跳过
            if (name.Equals("backup", StringComparison.OrdinalIgnoreCase)) continue;         // 防止备份目录把自身备份递归复制进去
            if (name.StartsWith("dsh-data-", StringComparison.OrdinalIgnoreCase))            // L-7：大小写不敏感（用户同名业务目录也一并跳过并提示，不静默）
            { skippedNested++; continue; }
            try { if ((File.GetAttributes(P(d)) & FileAttributes.ReparsePoint) != 0) { LogErr("跳过 reparse point（symlink/junction）目录，不进入: " + TrimP(d)); continue; } } catch { }
            try { CopyTree(TrimP(d), Path.Combine(dst, name), skipLocked); }
            catch (Exception ex)
            {
                if (!skipLocked) throw;
                LogErr("跳过无法复制的子目录: " + TrimP(d) + " : " + ex.Message);
            }
        }
        if (skippedNested > 0)
            Warn(T("已跳过 " + skippedNested + " 个嵌套备份目录（dsh-data-*），不复制进本次备份。",
                    "Skipped " + skippedNested + " nested backup folder(s) (dsh-data-*), not copied into this backup."));
        foreach (string f in Directory.GetFiles(P(src)))
        {
            string fs = TrimP(f), fd = Path.Combine(dst, Path.GetFileName(f));
            try
            {
                if ((File.GetAttributes(P(fs)) & FileAttributes.ReparsePoint) != 0) { LogErr("跳过 reparse point（symlink/junction）文件: " + TrimP(fs)); continue; }
                using (var s = new FileStream(P(fs), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var t = new FileStream(P(fd), FileMode.Create, FileAccess.Write, FileShare.None))
                    s.CopyTo(t);
            }
            catch (Exception ex) { if (!skipLocked) throw; LogErr("备份: 跳过无法复制的文件 " + TrimP(f) + " : " + ex.Message); }   // 备份模式：被锁/坏文件跳过并记日志；恢复模式：如实报错
        }
    }


    /// <summary>把任一备份目录（本机或从其他电脑复制来的）的数据/工作区恢复到当前位置。</summary>
    static void RestoreFromSource(string path)
    {
        if (!IntegrityGate("恢复", "restore")) return;
        string dst = DataRoot();
        bool hasWs = Directory.Exists(Path.Combine(path, "_workspace"));
        try
        {
            Directory.CreateDirectory(P(dst));   // M-5：恢复侧同样走 \\?\ 长路径前缀
            Info(T("正在恢复数据...", "Restoring data..."));
            foreach (string d in Directory.GetDirectories(P(path)))
                if (Path.GetFileName(d) != "_workspace")
                    CopyTree(d, Path.Combine(dst, Path.GetFileName(d)));
            foreach (string f in Directory.GetFiles(P(path)))
                File.Copy(P(f), P(Path.Combine(dst, Path.GetFileName(f))), true);   // M-5：恢复侧同样走 \\?\ 长路径前缀
            if (hasWs) RestoreWorkspaces(Path.Combine(path, "_workspace"));
            Success(T("恢复完成。请重启 dsh web。", "Restore done. Restart dsh web."));
        }
        catch (Exception ex) { LogErr("恢复失败: " + ex); Error(T("恢复失败：" + ex.Message, "Restore failed: " + ex.Message)); }
    }


    /// <summary>恢复备份包里的工作区：新格式为 _workspace\<名字>\（含 .dshws 标记，多工作区）；旧格式为 _workspace 直接存放内容。</summary>
    static void RestoreWorkspaces(string wsRoot)
    {
        string[] subs = Directory.GetDirectories(wsRoot);
        bool anyNew = false;
        foreach (string s in subs)
            if (File.Exists(Path.Combine(s, ".dshws"))) { anyNew = true; break; }
        if (anyNew)
        {
            foreach (string s in subs)
            {
                if (!File.Exists(Path.Combine(s, ".dshws")))
                { Warn(T("无法识别的工作区条目，跳过：" + Path.GetFileName(s), "Unrecognized workspace entry, skipped: " + Path.GetFileName(s))); continue; }
                RestoreOneWorkspace(s, Path.GetFileName(s), true);
            }
        }
        else RestoreOneWorkspace(wsRoot, null, false);   // 旧格式：整个 _workspace 视为一个工作区
    }


    /// <summary>单个工作区恢复：目标默认取当前检测的工作区目录，可输入其他路径；输入 0 跳过。</summary>
    static void RestoreOneWorkspace(string srcDir, string label, bool isNewFormat)
    {
        string def = WorkspaceRoot();
        Console.WriteLine();
        if (label != null) CL(ConsoleColor.Gray, T("  工作区「" + label + "」", "  Workspace: " + label));
        C(ConsoleColor.Gray, T("  恢复目标（直接回车=" + (def ?? T("未检测到", "none")) + "，输入路径自定义，输入 0 跳过）：",
                               "  Restore target (Enter=" + (def ?? "none") + ", type a path, 0 to skip): "));
        string t = ReadLineTrim().Trim().Trim('"');
        if (t == "0") { Info(T("已跳过该工作区。", "Workspace skipped.")); return; }
        string target = null;
        bool custom = t.Length > 0;
        if (custom) { try { target = Path.GetFullPath(t); } catch { target = null; } }
        if (target == null) target = def;
        if (target == null || !Directory.Exists(target))
        {
            Warn(T("目标目录无效，已跳过该工作区。", "Invalid target, skipped."));
            return;
        }
        // 自定义恢复目标确认：可能覆盖已有数据或指向错误位置，恢复前要求确认
        if (custom && (def == null || !string.Equals(target, def, StringComparison.OrdinalIgnoreCase)))
        {
            bool hasContent = Directory.GetFileSystemEntries(target).Length > 0;
            string ask = hasContent
                ? T("  警告：目标目录非空（将合并/覆盖其中文件）：" + target + "\n  确认恢复？输入 y 继续：",
                    "  Warning: target directory is not empty (files will be merged/overwritten): " + target + "\n  Continue restore? Type y: ")
                : T("  恢复目标：" + target + "。确认？输入 y 继续：",
                    "  Restore target: " + target + ". Continue? Type y: ");
            Console.Write(ask);
            string confirm = ReadLineTrim();
            if (confirm != "y" && confirm != "Y") { Warn(T("已取消。", "Cancelled.")); return; }
        }
        if (isNewFormat)
        {
            foreach (string d in Directory.GetDirectories(srcDir))
                CopyTree(d, Path.Combine(target, Path.GetFileName(d)));
            foreach (string f in Directory.GetFiles(srcDir))
                if (Path.GetFileName(f) != ".dshws")
                    File.Copy(P(f), P(Path.Combine(target, Path.GetFileName(f))), true);   // M-5：恢复侧同样走 \\?\ 长路径前缀
        }
        else CopyTree(srcDir, target);
        Success(T("已恢复工作区到 " + target, "Workspace restored to " + target));
    }


    /// <summary>非交互备份数据目录：输出 BACKUP_OK &lt;路径&gt; / BACKUP_FAIL &lt;原因&gt;。不附加工交互工作区。</summary>
    static void NIBackup()
    {
        string src = DataRoot();
        if (!Directory.Exists(src)) { Console.WriteLine("BACKUP_FAIL " + T("数据目录不存在：" + src, "data dir not found: " + src)); return; }
        string bk = DoBackup(src, null, BackupKind.Manual);
        if (bk != null) Console.WriteLine("BACKUP_OK " + bk);
        else Console.WriteLine("BACKUP_FAIL " + T("备份失败（见 launcher.log）", "backup failed (see launcher.log)"));
    }


    /// <summary>非交互恢复最新有效备份：输出 RESTORE_OK &lt;路径&gt; / RESTORE_FAIL &lt;原因&gt;。
    /// 沿用交互版安全顺序：运行中拒绝 + 恢复前自动备份 + 目标仅默认工作区（不询问）。</summary>
    static void NIRestore()
    {
        string root = BackupsRoot();
        if (!Directory.Exists(root)) { Console.WriteLine("RESTORE_FAIL " + T("没有备份", "no backups")); return; }
        string[] dirs = Directory.GetDirectories(root, "dsh-data-*");
        Array.Sort(dirs);
        Array.Reverse(dirs);
        string bk = null;
        foreach (string d in dirs) { if (IsValidBackupDir(d)) { bk = d; break; } }   // 最新有效备份
        if (bk == null) { Console.WriteLine("RESTORE_FAIL " + T("无有效备份", "no valid backup")); return; }
        NIRestoreCore(bk);
    }


    /// <summary>非交互恢复指定备份（GUI 选择框推路径）：restore --path &lt;dir&gt;。
    /// 校验：非空净化引号、必须在备份根之内、必须为有效备份目录；安全流程与恢复最新完全一致。</summary>
    static void NIRestorePath(string pathArg)
    {
        string reason = NIValidateRestorePath(pathArg, BackupsRoot());
        if (reason != null)
        {
            if (reason == "no-path") Console.WriteLine("RESTORE_FAIL " + T("未指定备份目录", "no backup specified"));
            else if (reason == "outside") Console.WriteLine("RESTORE_FAIL " + T("备份目录不在备份根内", "backup dir is outside the backups root"));
            else Console.WriteLine("RESTORE_FAIL " + T("无效备份目录", "invalid backup directory"));
            return;
        }
        string bk = (pathArg ?? "").Trim().Trim('"');
        NIRestoreCore(bk);
    }


    /// <summary>统一恢复执行：bk 已确认是备份根内的有效备份目录。运行中拒绝 + 恢复前自动备份 + 恢复。</summary>
    static void NIRestoreCore(string bk)
    {
        if (ProbeService() != ServiceState.Down) { Console.WriteLine("RESTORE_FAIL " + T("dsh 正在运行，无法恢复", "dsh is running; cannot restore")); return; }
        string dst = DataRoot();
        if (Directory.Exists(dst))
        {
            string preBk = DoBackup(dst, null, BackupKind.PreRestore);
            if (preBk == null) { Console.WriteLine("RESTORE_FAIL " + T("恢复前自动备份失败", "pre-restore backup failed")); return; }
        }
        inputEof = true;   // 非交互：工作区恢复走默认目标，任何 ReadLineTrim 立即返回 ""（不阻塞 stdin）
        RestoreFromSource(bk);
        Console.WriteLine("RESTORE_OK " + bk);
    }


    /// <summary>目录总大小（字节）。不可读部分跳过，不抛异常。</summary>
    static long DirSize(string dir)
    {
        long total = 0;
        try
        {
            var stack = new Stack<string>();
            stack.Push(dir);
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


    /// <summary>人类可读大小。</summary>
    static string HumanSize(long b)
    {
        if (b < 1024) return b + " B";
        if (b < 1024L * 1024) return (b / 1024.0).ToString("0.0") + " KB";
        if (b < 1024L * 1024 * 1024) return (b / (1024.0 * 1024)).ToString("0.0") + " MB";
        return (b / (1024.0 * 1024 * 1024)).ToString("0.00") + " GB";
    }


    /// <summary>备份目录名 → 类型（Manual/Auto/PreRestore/PreImport/PreWipe/PreUpdate）。</summary>
    static string BackupKindName(string dirName)
    {
        if (dirName.EndsWith("-pre-restore")) return "PreRestore";
        if (dirName.EndsWith("-pre-import")) return "PreImport";
        if (dirName.EndsWith("-pre-update")) return "PreUpdate";
        if (dirName.EndsWith("-pre-wipe")) return "PreWipe";
        if (dirName.EndsWith("-auto")) return "Auto";
        return "Manual";
    }


    /// <summary>目录级跳过判定，必须与 CopyTree 内联规则保持一致（依赖可重装/防自嵌套 + reparse point 不进入）；改动需两处同步。</summary>
    static bool CopySkipDir(string fullPath, string name)
    {
        if (name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.Equals("backup", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.StartsWith("dsh-data-", StringComparison.OrdinalIgnoreCase)) return true;
        return IsReparse(fullPath);
    }


    /// <summary>合并式恢复 Dry-Run 计划：返回 [新增, 覆盖, 保留, 待复制字节]。
    /// 源侧复现 RestoreFromSource / 新格式工作区恢复语义：恢复侧自己逐个顶层目录调 CopyTree，故顶层名字规则不适用
    /// （顶层各目录内部仍套用跳过规则，排除 skipTopDir）+ 顶层文件（排除 skipTopFile）；
    /// 目标侧全量统计。合并语义：仅目标端存在的文件不会被删除（计入保留）。</summary>
    static long[] PlanMerge(string src, string dst, string skipTopDir, string skipTopFile)
    {
        return PlanMergeCore(src, dst, skipTopDir, skipTopFile, false);
    }


    /// <summary>旧格式工作区 Dry-Run：恢复侧是 CopyTree(wsSrc,target) 单次调用，顶层目录同样套用跳过规则。</summary>
    static long[] PlanMergeLegacy(string src, string dst) { return PlanMergeCore(src, dst, null, null, true); }


    static long[] PlanMergeCore(string src, string dst, string skipTopDir, string skipTopFile, bool topRules)
    {
        var srcMap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (string d in Directory.GetDirectories(P(src)))
            {
                string name = Path.GetFileName(TrimP(d));
                if (skipTopDir != null && name.Equals(skipTopDir, StringComparison.OrdinalIgnoreCase)) continue;
                if (topRules && CopySkipDir(TrimP(d), name)) continue;
                WalkFiles(src, d, srcMap, true);
            }
            foreach (string f in Directory.GetFiles(P(src)))
            {
                string name = Path.GetFileName(TrimP(f));
                if (skipTopFile != null && name.Equals(skipTopFile, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    if (IsReparse(f)) continue;
                    srcMap[name] = new FileInfo(P(f)).Length;
                }
                catch { }
            }
        }
        catch { }
        var dstMap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(dst)) { try { WalkFiles(dst, dst, dstMap, false); } catch { } }
        long nw = 0, ow = 0, keep = 0, bytes = 0;
        foreach (var kv in srcMap) { if (dstMap.ContainsKey(kv.Key)) ow++; else nw++; bytes += kv.Value; }
        foreach (var kv in dstMap) if (!srcMap.ContainsKey(kv.Key)) keep++;
        return new long[] { nw, ow, keep, bytes };
    }


    /// <summary>清除（wipe）Dry-Run 计划：返回 [文件数, 目录数, 字节]。reparse point 计为条目但不进入（删链接不删目标）。</summary>
    static long[] PlanDelete(string root)
    {
        long files = 0, dirs = 0, bytes = 0;
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            string d = stack.Pop();
            dirs++;
            string[] subs;
            try { subs = Directory.GetDirectories(P(d)); } catch { subs = new string[0]; }
            foreach (string s in subs)
            {
                bool rep = false;
                try { rep = (File.GetAttributes(P(s)) & FileAttributes.ReparsePoint) != 0; } catch { }
                if (rep) { dirs++; continue; }
                stack.Push(s);
            }
            string[] fs;
            try { fs = Directory.GetFiles(P(d)); } catch { fs = new string[0]; }
            foreach (string f in fs)
            {
                files++;
                try { if ((File.GetAttributes(P(f)) & FileAttributes.ReparsePoint) == 0) bytes += new FileInfo(P(f)).Length; } catch { }
            }
        }
        return new long[] { files, dirs, bytes };
    }


    /// <summary>restore/import Dry-Run：只读合并计划（不要求服务停止、不写任何东西、不弹交互）。
    /// 输出 DRYRUN_OK / DRYRUN_SRC / 每个作用域 DRYRUN_SCOPE+NEW/OVERWRITE/KEEP/BYTES / DRYRUN_TOTAL / DRYRUN_NOTE；失败 DRYRUN_FAIL 原因。</summary>
    static void NIRestoreDryRun(string pathArg)
    {
        string bk = null;
        if (string.IsNullOrWhiteSpace(pathArg))
        {
            string root = BackupsRoot();
            if (Directory.Exists(root))
            {
                string[] dirs = Directory.GetDirectories(root, "dsh-data-*");
                Array.Sort(dirs);
                Array.Reverse(dirs);
                foreach (string d in dirs) { if (IsValidBackupDir(d)) { bk = d; break; } }
            }
            if (bk == null) { Console.WriteLine("DRYRUN_FAIL " + T("无有效备份", "no valid backup")); return; }
        }
        else
        {
            string p = pathArg.Trim().Trim('"');
            if (IsSubPath(BackupsRoot(), p))
            {
                if (!IsValidBackupDir(p)) { Console.WriteLine("DRYRUN_FAIL " + T("无效备份目录", "invalid backup directory")); return; }
                bk = p;
            }
            else
            {
                string r = ResolveBackupDir(p);
                if (r == null) { Console.WriteLine("DRYRUN_FAIL " + T("不是有效备份包", "not a valid backup package")); return; }
                bk = r;
            }
        }
        Console.WriteLine("DRYRUN_OK");
        Console.WriteLine("DRYRUN_SRC " + bk);
        long tn = 0, to = 0, tk = 0, tb = 0;
        string dst = DataRoot();
        long[] dp = PlanMerge(bk, dst, "_workspace", null);
        Console.WriteLine("DRYRUN_SCOPE data " + dst);
        Console.WriteLine("DRYRUN_NEW " + dp[0]);
        Console.WriteLine("DRYRUN_OVERWRITE " + dp[1]);
        Console.WriteLine("DRYRUN_KEEP " + dp[2]);
        Console.WriteLine("DRYRUN_BYTES " + dp[3]);
        tn += dp[0]; to += dp[1]; tk += dp[2]; tb += dp[3];
        string wsSrc = Path.Combine(bk, "_workspace");
        if (Directory.Exists(wsSrc))
        {
            string[] subs = Directory.GetDirectories(wsSrc);
            bool anyNew = false;
            foreach (string s in subs) if (File.Exists(Path.Combine(s, ".dshws"))) { anyNew = true; break; }
            if (anyNew)
            {
                foreach (string s in subs)
                {
                    if (!File.Exists(Path.Combine(s, ".dshws"))) continue;
                    string name = Path.GetFileName(s);
                    string target = Path.Combine(WorkspaceRoot() ?? dst, name);
                    long[] wp = PlanMerge(s, target, null, ".dshws");
                    Console.WriteLine("DRYRUN_SCOPE workspace " + name + " " + target);
                    Console.WriteLine("DRYRUN_NEW " + wp[0]);
                    Console.WriteLine("DRYRUN_OVERWRITE " + wp[1]);
                    Console.WriteLine("DRYRUN_KEEP " + wp[2]);
                    Console.WriteLine("DRYRUN_BYTES " + wp[3]);
                    tn += wp[0]; to += wp[1]; tk += wp[2]; tb += wp[3];
                }
            }
            else
            {
                string target = WorkspaceRoot() ?? dst;
                long[] wp = PlanMergeLegacy(wsSrc, target);
                Console.WriteLine("DRYRUN_SCOPE workspace-legacy " + target);
                Console.WriteLine("DRYRUN_NEW " + wp[0]);
                Console.WriteLine("DRYRUN_OVERWRITE " + wp[1]);
                Console.WriteLine("DRYRUN_KEEP " + wp[2]);
                Console.WriteLine("DRYRUN_BYTES " + wp[3]);
                tn += wp[0]; to += wp[1]; tk += wp[2]; tb += wp[3];
            }
        }
        Console.WriteLine("DRYRUN_TOTAL " + tn + " " + to + " " + tk + " " + tb);
        Console.WriteLine("DRYRUN_NOTE " + T("合并语义：仅目标端存在的文件不会被删除；交互恢复时每个工作区可自定义目标或跳过。",
                                             "Merge semantics: destination-only files are NOT deleted; interactive restore allows per-workspace custom target or skip."));
    }


    /// <summary>非交互导出备份：backup-export --path &lt;bk&gt; --to &lt;dir&gt;（复制一份到目标目录，只读源）。</summary>
    static void NIBackupExport(string[] args)
    {
        string reason = NIValidateExport(FlagValue(args, "--path") ?? FlagValue(args, "-path"), FlagValue(args, "--to") ?? FlagValue(args, "-to"), BackupsRoot());
        if (reason != null) { Console.WriteLine("BKEXPORT_FAIL " + T("导出校验失败: " + reason, "export validation failed: " + reason)); return; }
        string src = (FlagValue(args, "--path") ?? FlagValue(args, "-path")).Trim().Trim('"');
        string dst = Path.GetFullPath((FlagValue(args, "--to") ?? FlagValue(args, "-to")).Trim().Trim('"'));
        string target = Path.Combine(dst, Path.GetFileName(src.TrimEnd('\\', '/')));
        try
        {
            Directory.CreateDirectory(P(dst));
            CopyTree(src, target, true);
            Console.WriteLine("BKEXPORT_OK " + target);
        }
        catch (Exception ex) { LogErr("备份导出失败: " + ex); Console.WriteLine("BKEXPORT_FAIL " + ex.Message); }
    }


    /// <summary>非交互删除备份：backup-delete --path &lt;bk&gt;（仅限备份根内 dsh-data-* 目录；写审计日志）。</summary>
    static void NIBackupDelete(string[] args)
    {
        string reason = NIValidateBackupDelete(FlagValue(args, "--path") ?? FlagValue(args, "-path"), BackupsRoot());
        if (reason != null) { Console.WriteLine("BKDEL_FAIL " + T("删除校验失败: " + reason, "delete validation failed: " + reason)); return; }
        string src = (FlagValue(args, "--path") ?? FlagValue(args, "-path")).Trim().Trim('"');
        try
        {
            ClearReadOnlyRecursive(src);
            Directory.Delete(src, true);
            LogErr("审计: 用户删除备份 " + src);
            Console.WriteLine("BKDEL_OK " + Path.GetFileName(src.TrimEnd('\\', '/')));
        }
        catch (Exception ex) { LogErr("备份删除失败: " + ex); Console.WriteLine("BKDEL_FAIL " + ex.Message); }
    }

    // ---------------- profile 诊断与修复（v2.7：profilecheck / bootdiag / profilepatch） ----------------
    // 背景（真实故障样本）：dsh web 启动时 plugin tree 加载失败——
    //   tool-subagent: provider "kimi" cannot enforce maxDepth (no depthLimit capability)
    //   — set maxDepth: 'provider-managed' to leave the recursion budget to the provider
    // profile 补丁层是 YAML，语义：带 id 的顶层条目 = 改已有行；新增行必须放进 insert: 列表。
    // 所以本功能只做「在某个已存在条目的 config: 块内补一行」，绝不改顶层结构、绝不新增/删除条目。
    // 三个命令全程零第三方依赖：块扫描（不做 YAML 全解析）+ 单行插入。


    /// <summary>备份 profile 文件到 StateDir\backup\bootdiag-&lt;时间戳&gt;\（保留相对 ~/.dsh 的路径）。</summary>
    static string BackupProfileFile(string file)
    {
        try
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string root = Path.Combine(BackupsRoot(), "bootdiag-" + stamp);
            string dest = Path.Combine(root, RelToDataRoot(file, false));
            string dir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.Copy(file, dest, true);
            LogErr("profilepatch: 备份 " + SanitizeForReport(file) + " → " + dest);
            return dest;
        }
        catch { return null; }
    }


    /// <summary>带指定后缀的最新有效备份（名字字典序=时间序）；无则 null。</summary>
    static string LatestBackupWithSuffix(string suffix)
    {
        try
        {
            string root = BackupsRoot();
            if (!Directory.Exists(root)) return null;
            string[] dirs = Directory.GetDirectories(root, "dsh-data-*" + suffix);
            Array.Sort(dirs);
            Array.Reverse(dirs);
            foreach (string d in dirs) if (IsValidBackupDir(d)) return d;
        }
        catch { }
        return null;
    }


    /// <summary>有效备份总数（回滚候选数）。</summary>
    static int CountValidBackups()
    {
        int n = 0;
        try
        {
            string root = BackupsRoot();
            if (!Directory.Exists(root)) return 0;
            foreach (string d in Directory.GetDirectories(root, "dsh-data-*")) if (IsValidBackupDir(d)) n++;
        }
        catch { }
        return n;
    }

}
