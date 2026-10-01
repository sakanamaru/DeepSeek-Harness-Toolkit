using System;
using System.Collections.Generic;
using Dsht.Domain.Abstractions;
using Dsht.Domain.Model;
using Dsht.Domain.Services;

namespace Dsht.Cli
{
    /// <summary>会话统计相关（`sessions` 命令 + 它的辅助函数）✓。
    /// ★★ 架构审计（S1）：从 `Program.cs` 里**原样搬出来**的 ✓✓（一行逻辑都没改 ✓）
    ///   · 动机：那个文件曾经 3500+ 行 / 94 个方法 ✗ → 任何改动都碰同一个文件 ✓
    ///   · 做法：`partial class` ✓ 同程序集 ✓ **零调用点变更** ✓ 纯机械 ✓
    ///   · 纪律：搬完立刻编译 + 跑门槛 ✓（编译器 + 11 项门槛双重把关 ✓）</summary>
    public static partial class Program
    {
        /// <summary>快照的年龄（秒）✓。**解析不出来 → 返回 0** ✓（当作新鲜 ✓ —— 绝不能因为解析失败就把 live 清掉 ✗）。</summary>
        private static long SnapshotAgeSeconds(string snap)
        {
            try
            {
                System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(
                    snap, "\"generatedAt\"\\s*:\\s*\"([^\"]+)\"");
                if (!m.Success) return 0;
                System.DateTime t;
                if (!System.DateTime.TryParse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out t))
                    return 0;
                double age = (System.DateTime.UtcNow - t).TotalSeconds;
                return age < 0 ? 0 : (long)age;
            }
            catch { return 0; }
        }
    }
}
