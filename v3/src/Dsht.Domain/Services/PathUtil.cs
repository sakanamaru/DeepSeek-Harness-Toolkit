using System;

namespace Dsht.Domain.Services
{
    /// <summary>路径判定（纯函数）。
    /// TrimTrailingSep 逐条对齐 v2.x；IsSubPath 在 v2.x 里只认反斜杠，这里同时认正反斜杠
    /// （Linux 上路径用 /）——对 Windows 输入结果完全一致。</summary>
    public static class PathUtil
    {
        /// <summary>去尾部分隔符，但保留盘根语义（D:\ 不变成 D:，UNC 共享根保留尾部斜杠）。</summary>
        public static string TrimTrailingSep(string p)
        {
            if (string.IsNullOrEmpty(p)) return p;
            while (p.Length > 1 && (p.EndsWith("\\", StringComparison.Ordinal) || p.EndsWith("/", StringComparison.Ordinal)))
            {
                // 盘根：X:\ 保留
                if (p.Length == 3 && p[1] == ':' && (p[2] == '\\' || p[2] == '/')) break;
                // UNC 共享根：\\server\share 保留尾部斜杠（此处保守：只保留形如 \\a\b 的最小形态）
                string t = p.TrimEnd('\\', '/');
                int sep = t.IndexOf('\\');
                if (p.StartsWith("\\\\", StringComparison.Ordinal) && t.Length - t.LastIndexOf('\\') >= 0 && t.IndexOf('\\', sep + 2 < t.Length ? sep + 2 : t.Length) < 0) break;
                p = t;
            }
            return p;
        }

        /// <summary>child 是否位于 parent 子树内（含相等）；大小写不敏感。</summary>
        public static bool IsSubPath(string parent, string child)
        {
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(child)) return false;
            string a = TrimTrailingSep(parent).ToLowerInvariant();
            string b = TrimTrailingSep(child).ToLowerInvariant();
            if (b == a) return true;
            return b.StartsWith(a + "\\", StringComparison.Ordinal) || b.StartsWith(a + "/", StringComparison.Ordinal);
        }
    }
}