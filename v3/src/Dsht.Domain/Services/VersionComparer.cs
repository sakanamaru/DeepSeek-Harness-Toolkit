using System;

namespace Dsht.Domain.Services
{
    /// <summary>版本号处理（纯函数）。逐条对齐 v2.x 的 CoreVersion / IsCleanVersion / IsValidNpmVersion /
    /// SanitizeLatestVersion / CompareVersions / ComparePreRelease。
    /// 安全要点：IsValidNpmVersion 是**严格白名单**——核心段只允许数字与点（1-3 段），
    /// 至多一个 - 预发布段且字符仅限 [0-9A-Za-z.-]；空格/&amp;/;/|/&gt;/&lt;/$/引号等一律拒绝，
    /// 因此调用方可以放心把它拼进命令行。</summary>
    public static class VersionComparer
    {
        /// <summary>取版本核心段："0.1.1-rc.2" → "0.1.1"。</summary>
        public static string Core(string v)
        {
            if (string.IsNullOrEmpty(v)) return v == null ? "" : v;
            int d = v.IndexOf('-');
            return d >= 0 ? v.Substring(0, d) : v;
        }

        /// <summary>核心段是否是干净的 x[.y[.z]]（每段非空且全为数字）。</summary>
        public static bool IsClean(string v)
        {
            if (string.IsNullOrEmpty(v)) return false;
            string[] seg = v.Split('.');
            if (seg.Length < 1 || seg.Length > 3) return false;
            for (int i = 0; i < seg.Length; i++)
            {
                if (seg[i].Length == 0) return false;
                for (int k = 0; k < seg[i].Length; k++)
                    if (seg[i][k] < '0' || seg[i][k] > '9') return false;
            }
            return true;
        }

        /// <summary>严格 npm 版本白名单（见类注释）。</summary>
        public static bool IsValidNpmVersion(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return false;
            if (!IsClean(Core(v))) return false;
            int d = v.IndexOf('-');
            if (d < 0) return true;
            if (v.IndexOf('-', d + 1) >= 0) return false;
            string pre = v.Substring(d + 1);
            if (pre.Length == 0) return false;
            for (int i = 0; i < pre.Length; i++)
            {
                char c = pre[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '.' || c == '-'))
                    return false;
            }
            return true;
        }

        /// <summary>清洗 npm 返回的版本串：去空白、去前导 v；非法返回 null。</summary>
        public static string SanitizeLatest(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string s = raw.Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
            return IsValidNpmVersion(s) ? s : null;
        }

        /// <summary>semver 比较：核心段数值比较；核心段相同再比 pre-release 后缀。
        /// a 低于 b 返回负数、相等 0、高于返回正数。</summary>
        public static int Compare(string a, string b)
        {
            string[] pa = Core(a).Split('.');
            string[] pb = Core(b).Split('.');
            int n = pa.Length > pb.Length ? pa.Length : pb.Length;
            for (int i = 0; i < n; i++)
            {
                int x, y;
                int.TryParse(i < pa.Length ? pa[i] : "0", out x);
                int.TryParse(i < pb.Length ? pb[i] : "0", out y);
                if (x != y) return x < y ? -1 : 1;
            }
            return ComparePreRelease(a, b);
        }

        /// <summary>比较 pre-release 后缀（仅在核心段相等时调用）：
        /// 无后缀（正式版）&gt; 有后缀；同带后缀按 . 分段逐段比（数字段数值序、其余字典序），缺段更低。</summary>
        public static int ComparePreRelease(string a, string b)
        {
            int da = a == null ? -1 : a.IndexOf('-');
            int db = b == null ? -1 : b.IndexOf('-');
            string pa = da >= 0 ? a.Substring(da + 1) : "";
            string pb = db >= 0 ? b.Substring(db + 1) : "";
            if (pa.Length == 0 && pb.Length == 0) return 0;
            if (pa.Length == 0) return 1;    // 正式版高于预发布
            if (pb.Length == 0) return -1;
            string[] sa = pa.Split('.');
            string[] sb = pb.Split('.');
            int n = sa.Length > sb.Length ? sa.Length : sb.Length;
            for (int i = 0; i < n; i++)
            {
                string x = i < sa.Length ? sa[i] : null;
                string y = i < sb.Length ? sb[i] : null;
                if (x == null && y == null) return 0;
                if (x == null) return -1;    // 较短后缀更低：rc < rc.1
                if (y == null) return 1;
                int nx, ny;
                bool xn = int.TryParse(x, out nx);
                bool yn = int.TryParse(y, out ny);
                if (xn && yn) { if (nx != ny) return nx < ny ? -1 : 1; }
                else { int c = string.CompareOrdinal(x, y); if (c != 0) return c < 0 ? -1 : 1; }
            }
            return 0;
        }
    }
}