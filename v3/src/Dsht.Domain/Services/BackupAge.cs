using System;
using System.Globalization;

namespace Dsht.Domain.Services
{
    /// <summary>备份包年龄（纯函数，时间由调用方注入）。
    /// 对齐 v2.x：取名字里 "dsh-data-" 之后的 18 个字符按 yyyyMMdd-HHmmssfff 解析，算到 now 的整天数。
    /// 名字前缀不符或时间戳无法解析 → null（v2.x 此时不输出该条）。</summary>
    public static class BackupAge
    {
        public const string StampFormat = "yyyyMMdd-HHmmssfff";

        public static int? DaysSince(string backupDirName, DateTime now)
        {
            if (string.IsNullOrEmpty(backupDirName)) return null;
            if (!backupDirName.StartsWith(BackupPackage.Prefix, StringComparison.OrdinalIgnoreCase)) return null;
            string ts = backupDirName.Substring(BackupPackage.Prefix.Length);
            if (ts.Length >= 18) ts = ts.Substring(0, 18);
            DateTime lt;
            if (!DateTime.TryParseExact(ts, StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out lt)) return null;
            return (int)(now - lt).TotalDays;
        }
    }
}