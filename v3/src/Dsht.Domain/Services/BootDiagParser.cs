using System;
using System.Text.RegularExpressions;
using Dsht.Domain.Model;

namespace Dsht.Domain.Services
{
    /// <summary>解析 dsh 启动输出（纯函数，IO 由注入的委托提供）。逐条对齐 v2.x 的 BootDiagText：
    ///   · 识别签名 "plugin tree failed to load"；识别不到时 Recognized=false（不做猜测）
    ///   · Kind：含 "cannot enforce maxDepth" → maxDepth-missing，否则 plugin-tree-load
    ///   · 病灶定位：cause 链层层包裹，优先取**带 @ 的包名**匹配（最靠后者=最内层）
    ///   · file:///...#entry 的片段是最可靠的条目 id；再据此定位行号
    /// 注入：locateLine(fileOrDir, entry) → 行号（0=未找到），fileExists(path)。</summary>
    public static class BootDiagParser
    {
        public static BootDiagResult Parse(string text, Func<string, string, EntryLocation> locateLine, Func<string, bool> fileExists)
        {
            BootDiagResult r = new BootDiagResult();
            if (string.IsNullOrEmpty(text)) return r;
            string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            foreach (string ln in lines) { string t = (ln ?? "").Trim(); if (t.Length > 0) { r.FirstError = t; break; } }

            foreach (string ln in lines)
            {
                string t = (ln ?? "").Trim();
                if (t.IndexOf("plugin tree failed to load", StringComparison.OrdinalIgnoreCase) >= 0) { r.FirstError = t; r.Recognized = true; break; }
            }
            if (!r.Recognized)
            {
                foreach (string ln in lines)
                {
                    string t = (ln ?? "").Trim();
                    if (t.StartsWith("Error", StringComparison.OrdinalIgnoreCase) || t.IndexOf("error:", StringComparison.OrdinalIgnoreCase) >= 0) { r.FirstError = t; break; }
                }
                return r;
            }

            r.Kind = (text.IndexOf("cannot enforce maxDepth", StringComparison.OrdinalIgnoreCase) >= 0) ? "maxDepth-missing" : "plugin-tree-load";

            MatchCollection mc = Regex.Matches(text, @"failed to apply loader entry\s+([^\s(]+)\s*\(([^)]+)\)");
            string lastEntry = "", lastPlugin = "", pkgEntry = "", pkgPlugin = "";
            foreach (Match mm in mc)
            {
                string e = mm.Groups[1].Value.Trim();
                string p = mm.Groups[2].Value.Trim();
                lastEntry = e; lastPlugin = p;
                if (p.StartsWith("@", StringComparison.Ordinal)) { pkgEntry = e; pkgPlugin = p; }
            }
            r.Entry = pkgEntry.Length > 0 ? pkgEntry : lastEntry;
            r.Plugin = pkgPlugin.Length > 0 ? pkgPlugin : lastPlugin;

            Match mh = Regex.Match(text, @"set maxDepth:\s*'([^']+)'");
            r.Hint = mh.Success ? ("set maxDepth: '" + mh.Groups[1].Value + "'") : "set maxDepth: 'provider-managed'";

            Match mf = Regex.Match(text, "file:///[^\\s\"']+");
            if (mf.Success)
            {
                string frag;
                string p = FileUrlConverter.ToPath(mf.Value, out frag);
                if (frag.Length > 0) r.Entry = frag;
                r.File = p;
                EntryLocation loc = locateLine == null ? null : SafeLocate(locateLine, p, r.Entry);
                if (loc != null && loc.Line > 0) { r.File = loc.File; r.Line = loc.Line; }
                else if (fileExists != null && fileExists(p)) r.File = p;
            }
            return r;
        }

        private static EntryLocation SafeLocate(Func<string, string, EntryLocation> f, string p, string entry)
        {
            try { return f(p, entry); } catch { return null; }
        }
    }
}