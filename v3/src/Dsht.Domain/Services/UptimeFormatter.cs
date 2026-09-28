using System;

namespace Dsht.Domain.Services
{
    /// <summary>运行时长格式化（纯函数）。逐字对齐 v2.x 的 FormatUptime：
    ///   &lt;60 秒 → "n 秒"；&lt;60 分 → "n 分"；&lt;24 小时 → "n 小时 m 分"；再长 → "n 天 m 小时"。
    /// 注意：这些是**标记行契约字符串**（v2.x 硬编码中文，不走 T()），因此不随界面语言变化。</summary>
    public static class UptimeFormatter
    {
        public static string Format(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;      // 时钟回拨保护：不显示负数时长
            if (t.TotalSeconds < 60) return ((int)t.TotalSeconds) + " 秒";
            if (t.TotalMinutes < 60) return ((int)t.TotalMinutes) + " 分";
            if (t.TotalHours < 24) return ((int)t.TotalHours) + " 小时 " + t.Minutes + " 分";
            return ((int)t.TotalDays) + " 天 " + t.Hours + " 小时";
        }
    }
}