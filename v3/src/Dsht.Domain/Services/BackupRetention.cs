using System;
using System.Collections.Generic;

namespace Dsht.Domain.Services
{
    /// <summary>备份保留策略（纯函数，无 IO）。
    /// 语义逐条对齐 v2.x 的 IsAutoBackupName + EnforceBackupRetention：
    ///   · 只清理**自动类**备份（-auto / -pre-restore / -pre-import / -pre-wipe / -pre-update），手动备份永久保留
    ///   · 只考虑名字以 dsh-data- 开头的目录
    ///   · 保底保留 3 份（cfgKeep &lt; 3 时按 3 处理）
    ///   · 名字里的时间戳字典序即时间序，故按名字升序后删最前面的（最旧）
    /// 排序显式使用 Ordinal：v2.x 用的是 List.Sort() 的默认比较（受区域设置影响），
    /// 这里改成文化无关的确定性排序——对 "dsh-data-YYYYMMDD-HHMMSS-*" 这类纯数字时间戳，两者结果一致，
    /// 但 Ordinal 不会因系统区域设置而漂移。</summary>
    public static class BackupRetention
    {
        /// <summary>保底保留份数（与 v2.x 一致）。</summary>
        public const int MinimumKeep = 3;

        /// <summary>备份目录名前缀（与 v2.x 的过滤条件一致）。</summary>
        public const string BackupPrefix = "dsh-data-";

        /// <summary>是否自动类备份（可被保留策略清理）。</summary>
        public static bool IsAutoBackupName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.EndsWith("-auto", StringComparison.Ordinal)
                || name.EndsWith("-pre-restore", StringComparison.Ordinal)
                || name.EndsWith("-pre-import", StringComparison.Ordinal)
                || name.EndsWith("-pre-wipe", StringComparison.Ordinal)
                || name.EndsWith("-pre-update", StringComparison.Ordinal);
        }

        /// <summary>是否是备份目录名（保留策略只看这些）。</summary>
        public static bool IsBackupDirName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.StartsWith(BackupPrefix, StringComparison.Ordinal);
        }

        /// <summary>实际保留份数：cfgKeep 与保底值取大。</summary>
        public static int EffectiveKeep(int cfgKeep)
        {
            return cfgKeep < MinimumKeep ? MinimumKeep : cfgKeep;
        }

        /// <summary>选出应删除的备份目录名（**最旧在前**，与 v2.x 的删除顺序一致）。
        /// 输入为目录名集合（不含路径）；不修改输入。</summary>
        public static List<string> SelectForDeletion(IEnumerable<string> dirNames, int cfgKeep)
        {
            List<string> result = new List<string>();
            if (dirNames == null) return result;

            List<string> autos = new List<string>();
            foreach (string n in dirNames)
            {
                if (IsBackupDirName(n) && IsAutoBackupName(n)) autos.Add(n);
            }
            autos.Sort(StringComparer.Ordinal);   // 升序 = 时间序（旧 → 新）

            int keep = EffectiveKeep(cfgKeep);
            for (int i = 0; i < autos.Count - keep; i++) result.Add(autos[i]);
            return result;
        }
    }
}