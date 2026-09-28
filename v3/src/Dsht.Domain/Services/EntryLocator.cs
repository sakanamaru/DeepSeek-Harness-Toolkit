using System;
using System.Text.RegularExpressions;

namespace Dsht.Domain.Services
{
    /// <summary>在一段 YAML 文本里定位某个条目 id 的行号（1-based）；找不到返回 0。纯函数。</summary>
    public static class EntryLocator
    {
        public static int FindLine(string text, string entry)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(entry)) return 0;
            string[] ls = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            for (int k = 0; k < ls.Length; k++)
            {
                int ind; string body; bool dash;
                if (!ProfileScanner.TryEntryStart(ls[k], out ind, out body, out dash)) continue;
                Match m = Regex.Match(body, dash ? @"^-\s+id:\s*(.*)$" : @"^id:\s*(.*)$");
                if (ProfileScanner.CleanYamlScalar(m.Groups[1].Value) == entry) return k + 1;
            }
            return 0;
        }
    }
}