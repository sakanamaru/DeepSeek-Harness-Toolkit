namespace Dsht.Domain.Services
{
    /// <summary>剪切过长文本（诊断输出用，纯函数）。实现体从 v2.x 的 ClipText 原样抽取，保证逐字一致。</summary>
    public static class TextClipper
    {
        public static string Clip(string s, int n)
        {
if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= n ? s : s.Substring(0, n) + "…";
        }
    }
}