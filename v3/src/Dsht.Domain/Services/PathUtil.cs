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

        /// <summary>纯词法规范化：解析 `.` 与 `..`，统一分隔符。**不做 IO、不查磁盘、不看当前目录** ✓（领域纯净 ✓）。
        /// 返回 null = 路径试图逃逸到根之上 ✓（调用方必须按"不安全"处理 ✓）。</summary>
        public static string NormalizeLexical(string p)
        {
            if (string.IsNullOrEmpty(p)) return p;
            char sep = p.IndexOf('\\') >= 0 ? '\\' : '/';
            string[] parts = p.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            string prefix = null;
            int start = 0;
            if (parts.Length > 0 && parts[0].Length == 2 && parts[0][1] == ':') { prefix = parts[0]; start = 1; }   // 盘符 C:
            else if (p.Length > 0 && (p[0] == '\\' || p[0] == '/')) { prefix = ""; start = 0; }                     // 根 / 或 \
            var stack = new System.Collections.Generic.List<string>();
            for (int i = start; i < parts.Length; i++)
            {
                string s = parts[i];
                if (s == ".") continue;
                if (s == "..")
                {
                    if (stack.Count == 0) return null;   // ★ 逃逸 → 不安全 ✓✓
                    stack.RemoveAt(stack.Count - 1);
                    continue;
                }
                stack.Add(s);
            }
            string body = string.Join(sep.ToString(), stack.ToArray());
            if (prefix == null) return body;
            if (prefix.Length == 0) return sep + body;
            return prefix + sep + body;
        }

        /// <summary>child 是否位于 parent 子树内（含相等）；大小写不敏感。
        /// ★ **先做纯词法规范化再比** ✓✓ —— 否则 `&lt;根&gt;\..\..\重要目录` 会因为字符串前缀相同而被当成"在根内" ✗
        ///   （真机审查抓到：`backup-delete` 能借此递归删除根外的任意同名目录 ✗✗）
        /// 任一侧含逃逸（`..` 跑到根之上）→ **一律返回 false** ✓（fail-closed ✓）。</summary>
        public static bool IsSubPath(string parent, string child)
        {
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(child)) return false;
            string na = NormalizeLexical(parent);
            string nb = NormalizeLexical(child);
            if (na == null || nb == null) return false;   // 有逃逸 → 不在子树内 ✓✓
            string a = TrimTrailingSep(na).ToLowerInvariant();
            string b = TrimTrailingSep(nb).ToLowerInvariant();
            if (b == a) return true;
            return b.StartsWith(a + "\\", StringComparison.Ordinal) || b.StartsWith(a + "/", StringComparison.Ordinal);
        }
    }
}