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

        /// <summary>是否保护性备份（严格模式：任一文件复制失败即整体失败）。</summary>
        public static bool IsProtective(BackupKind k)
        {
            return k == BackupKind.PreRestore || k == BackupKind.PreImport || k == BackupKind.PreWipe || k == BackupKind.PreUpdate;
        }
    }
}