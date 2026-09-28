using System;
using Dsht.Domain.Model;

namespace Dsht.Domain.Services
{
    /// <summary>备份包判定（纯函数）。语义逐条对齐 v2.x 的 IsValidBackupDir / LooksLikeDshData / ResolveBackupDir：
    ///   · 目录名须以 dsh-data- 开头（忽略大小写）
    ///   · 且内容含 dsh 数据特征（settings.yaml / credentials.yaml / sessions / profiles / storages 任一，文件或目录）
    ///     或含工作区子目录 _workspace
    ///   · **仅目录存在不算数**（防把任意文件夹当备份恢复/导入）
    ///   · 定位时允许下探一层：所选目录下恰好一个 dsh-data-* 子目录且其有效 → 取它；否则 null
    /// 与 v2.x 的差异：全部改为接收"目录快照"而非自己读盘 → 领域零 IO、可 100% 单测。</summary>
    public static class BackupPackage
    {
        public const string Prefix = "dsh-data-";
        public const string WorkspaceDir = "_workspace";

        /// <summary>dsh 数据特征标记（v2.x 原文顺序，保持可读性与报告一致）。</summary>
        public static readonly string[] DataMarkers = new string[]
        {
            "settings.yaml", "credentials.yaml", "sessions", "profiles", "storages"
        };

        /// <summary>目录名是否是本工具生成的备份包名。</summary>
        public static bool IsValidBackupName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>目录内容是否含 dsh 数据特征。</summary>
        public static bool HasDshData(DirSnapshot s)
        {
            if (s == null) return false;
            for (int i = 0; i < DataMarkers.Length; i++)
            {
                if (s.Has(DataMarkers[i])) return true;
            }
            return false;
        }

        /// <summary>是否是有效备份包（名字 + 内容）。</summary>
        public static bool IsValidPackage(DirSnapshot s)
        {
            if (s == null) return false;
            if (!IsValidBackupName(s.Name)) return false;
            return HasDshData(s) || s.Has(WorkspaceDir);
        }

        /// <summary>备份目录定位（下探一层）。self=所选目录快照，subDirs=其直接子目录快照。
        /// 返回定位到的目录名；无法定位返回 null。</summary>
        public static string Resolve(DirSnapshot self, DirSnapshot[] subDirs)
        {
            if (IsValidPackage(self)) return self.Name;
            if (subDirs == null) return null;
            DirSnapshot only = null; int n = 0;
            for (int i = 0; i < subDirs.Length; i++)
            {
                if (!IsValidBackupName(subDirs[i].Name)) continue;
                n++; only = subDirs[i];
            }
            if (n == 1 && IsValidPackage(only)) return only.Name;
            return null;
        }

        /// <summary>按名字后缀判定备份类型（手动为默认）。</summary>
        public static BackupKind Classify(string name)
        {
            if (string.IsNullOrEmpty(name)) return BackupKind.Manual;
            if (name.EndsWith("-auto", StringComparison.OrdinalIgnoreCase)) return BackupKind.Auto;
            if (name.EndsWith("-pre-restore", StringComparison.OrdinalIgnoreCase)) return BackupKind.PreRestore;
            if (name.EndsWith("-pre-import", StringComparison.OrdinalIgnoreCase)) return BackupKind.PreImport;
            if (name.EndsWith("-pre-wipe", StringComparison.OrdinalIgnoreCase)) return BackupKind.PreWipe;
            if (name.EndsWith("-pre-update", StringComparison.OrdinalIgnoreCase)) return BackupKind.PreUpdate;
            return BackupKind.Manual;
        }

        /// <summary>目录名后缀。逐字对齐 v2.x 的 BackupSuffix（Manual 无后缀）。
        /// <para>**并发命名约束** ✓✓（真机复现过一次数据互混 ✗，故记于此）：包名 = `dsh-data-&lt;毫秒时间戳&gt;-&lt;PID&gt;&lt;本后缀&gt;`。</para>
        /// <para>· 后缀必须**留在结尾** ✗ —— BackupRetention 用 EndsWith("-auto") 等识别自动类；BackupAge 只解析前缀后的 18 个字符。</para>
        /// <para>· PID 覆盖**跨进程**同毫秒（两个 CLI 进程各跑一次备份 ✓ 这是最常见的"双击两次" ✓）。</para>
        /// <para>· **同进程同类**目前不可达 ✓ —— 每个命令最多创建一次同类备份（import 的两个包后缀不同 ✓）。
        ///   若将来出现"一个进程内同类创建两次"的命令 ✗，需在此处**加重试**（目录已存在则换名 ✓ 且新名字必须仍以后缀结尾 ✓）。</para>
        /// </summary>
        public static string Suffix(BackupKind k)
        {
            if (k == BackupKind.Auto) return "-auto";
            if (k == BackupKind.PreRestore) return "-pre-restore";
            if (k == BackupKind.PreImport) return "-pre-import";
            if (k == BackupKind.PreWipe) return "-pre-wipe";
            if (k == BackupKind.PreUpdate) return "-pre-update";
            return "";
        }

        /// <summary>类型显示标签。逐字对齐 v2.x 的 BackupKindName（英文、不本地化）。</summary>
        public static string KindLabel(BackupKind k)
        {
            if (k == BackupKind.PreRestore) return "PreRestore";
            if (k == BackupKind.PreImport) return "PreImport";
            if (k == BackupKind.PreUpdate) return "PreUpdate";
            if (k == BackupKind.PreWipe) return "PreWipe";
            if (k == BackupKind.Auto) return "Auto";
            return "Manual";
        }

        /// <summary>是否保护性备份（严格模式：任一文件复制失败即整体失败）。</summary>
        public static bool IsProtective(BackupKind k)
        {
            return k == BackupKind.PreRestore || k == BackupKind.PreImport || k == BackupKind.PreWipe || k == BackupKind.PreUpdate;
        }
    }
}