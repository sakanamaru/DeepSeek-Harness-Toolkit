namespace Dsht.Domain.Services
{
    /// <summary>人类可读大小（纯函数）。逐字对齐 v2.x 的 HumanSize（含 GB 用两位小数）。</summary>
    public static class SizeFormatter
    {
        public static string Human(long b)
        {
            if (b < 1024) return b + " B";
            if (b < 1024L * 1024) return (b / 1024.0).ToString("0.0") + " KB";
            if (b < 1024L * 1024 * 1024) return (b / (1024.0 * 1024)).ToString("0.0") + " MB";
            return (b / (1024.0 * 1024 * 1024)).ToString("0.00") + " GB";
        }
    }
}