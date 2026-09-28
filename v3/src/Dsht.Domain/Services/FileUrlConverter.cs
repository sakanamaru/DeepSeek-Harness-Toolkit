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
            bool localForm = false;
            if (u.StartsWith("file:///", StringComparison.Ordinal)) { u = u.Substring(8); localForm = true; }
            else if (u.StartsWith("file://", StringComparison.Ordinal)) u = u.Substring(7);
            else if (u.StartsWith("file:/", StringComparison.Ordinal)) u = u.Substring(6);
            try { u = Uri.UnescapeDataString(u); } catch { }
            if (Regex.IsMatch(u, @"^/[A-Za-z]:/")) u = u.Substring(1);
            // 只在**Windows 盘符路径**上把 / 换成 \ ✗：Linux 上 file:///home/u/a.yml 会被换成
            // \home\u\a.yml → BootDiagParser 的存在性判定恒 false → bootdiag 谎报"定位不到文件"
            // （共享代码审计抓到的高严重度 bug ✗）。其余路径保持 / —— .NET 在 Windows 上同样接受 / ✓。
            // file:///home/u/a.yml 去掉 "file:///" 后是 home/u/a.yml —— **少了根斜杠** ✗（Windows 的
            // file:///C:/… 没这问题）→ 非盘符路径要把 / 补回来，否则 Linux 上路径必然不存在 ✗
            if (localForm && u.Length > 0 && !(u.Length >= 2 && u[1] == ':' && char.IsLetter(u[0]))) u = "/" + u;
            if (u.Length >= 2 && u[1] == ':' && char.IsLetter(u[0])) return u.Replace('/', '\\');
            return u;
        }
    }
}