using System;

namespace Dsht.Domain.Services
{
    /// <summary>hashes.txt 解析（纯函数）。格式：每行 "&lt;64hex&gt;  &lt;文件名&gt;"，允许 # 注释与空行。</summary>
    public static class ManifestParser
    {
        /// <summary>解析指定文件的 SHA-256（小写）。未找到/格式非法返回 null。
        /// 语义逐条对齐 v2.x 的 ParseManifestHash：跳过 # 与空行；文件名比较忽略大小写；
        /// hash 必须是 64 位十六进制，否则视为非法（返回 null）。</summary>
        public static string ParseHash(string manifest, string fileName)
        {
            if (string.IsNullOrEmpty(manifest) || string.IsNullOrEmpty(fileName)) return null;
            string[] lines = manifest.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string ln in lines)
            {
                string t = ln.Trim();
                if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal)) continue;
                int sp = t.IndexOf(' ');
                if (sp <= 0) continue;
                string hash = t.Substring(0, sp).Trim().ToLowerInvariant();
                string name = t.Substring(sp + 1).Trim();
                if (string.Compare(name, fileName, StringComparison.OrdinalIgnoreCase) != 0) continue;
                if (hash.Length != 64) return null;
                foreach (char c in hash) { if (!Uri.IsHexDigit(c)) return null; }
                return hash;
            }
            return null;
        }
    }
}