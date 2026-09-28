using System;

namespace Dsht.Domain.Services
{
    /// <summary>非交互命令的路径校验（纯函数，IO 靠注入）。逐条对齐 v2.x：
    ///   NIValidateRestorePath → no-path / outside / invalid
    ///   NIValidateExport      → no-path / no-to / outside / not-found / bad-target / nested
    ///   NIValidateBackupDelete→ no-path / outside / not-backup / not-found
    /// 返回 null=通过，否则原因键。</summary>
    public static class PathValidator
    {
        public static string ValidateRestorePath(string pathArg, string backupsRoot, Func<string, bool> isValidBackupDir)
        {
            string bk = ResolveBackupPath(Trim(pathArg), backupsRoot);
            if (bk.Length == 0) return "no-path";
            if (!PathUtil.IsSubPath(backupsRoot, bk)) return "outside";
            if (isValidBackupDir == null || !Safe(isValidBackupDir, bk)) return "invalid";
            return null;
        }

        public static string ValidateExport(string srcArg, string toArg, string backupsRoot,
            Func<string, bool> dirExists, Func<string, string> fullPath)
        {
            string src = ResolveBackupPath(Trim(srcArg), backupsRoot);
            string to = Trim(toArg);
            if (src.Length == 0) return "no-path";
            if (to.Length == 0) return "no-to";
            if (!PathUtil.IsSubPath(backupsRoot, src)) return "outside";
            if (dirExists == null || !Safe(dirExists, src)) return "not-found";
            string dst;
            try { dst = fullPath == null ? to : fullPath(to); }
            catch { return "bad-target"; }
            if (string.Equals(dst, src, StringComparison.OrdinalIgnoreCase) || PathUtil.IsSubPath(src, dst)) return "nested";
            return null;
        }

        public static string ValidateDeletePath(string srcArg, string backupsRoot, Func<string, bool> dirExists)
        {
            string src = ResolveBackupPath(Trim(srcArg), backupsRoot);
            if (src.Length == 0) return "no-path";
            if (!PathUtil.IsSubPath(backupsRoot, src)) return "outside";
            string name = BaseName(src);
            if (!name.StartsWith("dsh-data-", StringComparison.OrdinalIgnoreCase)) return "not-backup";
            if (dirExists == null || !Safe(dirExists, src)) return "not-found";
            return null;
        }

        /// <summary>把"裸备份名"解析到备份根内 ✓ —— GUI 与命令行都可能只给名字（列表里显示的那个 ✓）。
        /// 只做**根内**解析：含分隔符或盘符的按路径处理 ✓，解析后仍要过 IsSubPath ✓ 所以 `..` 逃逸照样被拒 ✓✓。
        /// 纯字符串拼接（不引 System.IO ✓ 保持领域纯净 ✓）；两种分隔符 IsSubPath 都认 ✓。</summary>
        public static string ResolveBackupPath(string p, string backupsRoot)
        {
            if (string.IsNullOrEmpty(p)) return p;
            if (p.IndexOf('/') >= 0 || p.IndexOf('\\') >= 0) return p;
            if (p.Length >= 2 && p[1] == ':') return p;
            string root = (backupsRoot == null ? "" : backupsRoot).TrimEnd('\\', '/');
            if (root.Length == 0) return p;
            char sep = root.IndexOf('\\') >= 0 ? '\\' : '/';   // 跟随备份根自身的风格 ✓（纯字符串判断，不引 System.IO ✓）
            return root + sep + p;
        }

        private static string Trim(string s) { return (s == null ? "" : s).Trim().Trim('"'); }

        private static string BaseName(string p)
        {
            string t = p.TrimEnd('\\', '/');
            int i = t.LastIndexOfAny(new char[] { '\\', '/' });
            return i >= 0 ? t.Substring(i + 1) : t;
        }

        private static bool Safe(Func<string, bool> f, string p)
        {
            try { return f(p); } catch { return false; }
        }
    }
}