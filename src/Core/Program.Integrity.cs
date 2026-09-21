using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

partial class Program
{

    /// <summary>从 manifest 文本（hashes.txt 格式：每行 "&lt;64hex&gt;  &lt;文件名&gt;"）解析指定文件的 SHA-256；
    /// 返回小写 hash；未找到/格式非法返回 null。纯函数，可单测。</summary>
    static string ParseManifestHash(string manifest, string fileName)
    {
        if (string.IsNullOrEmpty(manifest) || string.IsNullOrEmpty(fileName)) return null;
        string[] lines = manifest.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string ln in lines)
        {
            string t = ln.Trim();
            if (t.Length == 0 || t.StartsWith("#")) continue;
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


    /// <summary>自身 exe 的 SHA-256（小写 hex）；失败返回 null。</summary>
    static string SelfSha256()
    {
        try
        {
            string path = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var fs = File.OpenRead(path))
            {
                byte[] h = sha.ComputeHash(fs);
                var sb = new StringBuilder();
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
        catch { return null; }
    }


    /// <summary>自身完整性：true=与随包 hashes.txt 匹配（官方包）；false=不匹配（疑似被篡改）；
    /// null=旁无 manifest 或 manifest 不含自身（开发/非官方布局，不阻断）。</summary>
    static bool? SelfIntegrity()
    {
        try
        {
            string exe = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(exe)) return null;
            string manifestPath = Path.Combine(Path.GetDirectoryName(exe), "hashes.txt");
            if (!File.Exists(manifestPath)) return null;
            string want = ParseManifestHash(File.ReadAllText(manifestPath), Path.GetFileName(exe));
            if (want == null) return null;
            string actual = SelfSha256();
            if (actual == null) return null;
            return actual == want;
        }
        catch { return null; }
    }

}
