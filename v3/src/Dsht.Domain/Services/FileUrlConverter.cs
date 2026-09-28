using System;
using System.Text.RegularExpressions;

namespace Dsht.Domain.Services
{
    /// <summary>file:///C:/a/b#entry → C:\a\b + entry（纯函数）。逐条对齐 v2.x 的 FileUrlToPath。</summary>
    public static class FileUrlConverter
    {
        public static string ToPath(string url, out string fragment)
        {
            fragment = "";
            if (string.IsNullOrEmpty(url)) return "";
            string u = url.Trim().Trim('\'', '"');
            int hash = u.IndexOf('#');
            if (hash >= 0) { fragment = u.Substring(hash + 1); u = u.Substring(0, hash); }
            if (u.StartsWith("file:///", StringComparison.Ordinal)) u = u.Substring(8);
            else if (u.StartsWith("file://", StringComparison.Ordinal)) u = u.Substring(7);
            else if (u.StartsWith("file:/", StringComparison.Ordinal)) u = u.Substring(6);
            try { u = Uri.UnescapeDataString(u); } catch { }
            if (Regex.IsMatch(u, @"^/[A-Za-z]:/")) u = u.Substring(1);
            return u.Replace('/', '\\');
        }
    }
}