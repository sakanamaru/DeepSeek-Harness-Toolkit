using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Dsht.Domain.Abstractions;
using Dsht.Domain.Model;
using Dsht.Domain.Services;

namespace Dsht.Platform.Linux
{
    /// <summary>profile 文件来源（Linux）：与 Windows 同语义（数据根/profiles 下递归 *.yml+*.yaml，跳过 node_modules，标签脱敏为 ~/.dsh/...）。</summary>
    public sealed class LinuxProfileSource : IProfileSource
    {
        private readonly string _dataRoot;

        public LinuxProfileSource(IPaths paths)
        {
            _dataRoot = paths == null ? Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? "", ".dsh") : paths.DataRoot;
        }

        public string DataRoot { get { return _dataRoot; } }
        public string ProfilesRoot { get { return Path.Combine(_dataRoot, "profiles"); } }

                public ProfileCollection CollectDirectory(string dirOverride, bool includeVendor, bool absoluteLabel)
        {
            ProfileCollection result = new ProfileCollection();
            string dir = string.IsNullOrEmpty(dirOverride) ? ProfilesRoot : dirOverride;
            if (!Directory.Exists(dir)) return result;   // 目录不存在不是错误，返回空（CLI 会看 files=0）
            List<string> found = new List<string>();
            CollectInto(dir, found, result, 0);
            found.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string f in found)
            {
                if (!includeVendor && ProfileScanner.IsVendorPath(f)) { result.SkippedVendor++; continue; }
                string text;
                try { text = File.ReadAllText(f, new UTF8Encoding(false)); }
                catch { result.ReadErrors++; continue; }   // 读不到就计数，不再静默丢弃
                result.Files.Add(new ProfileFile(absoluteLabel ? f : Pretty(f), f, text));
            }
            return result;
        }

        /// <summary>Recursively collect .yml/.yaml with a CASE-INSENSITIVE extension check (Linux is
        /// case sensitive, so the old *.yml glob silently missed .YML). Each directory gets its own
        /// try/catch: one unreadable subdirectory now costs one error instead of discarding the whole
        /// tree, which used to make profilecheck report a false "no problems found".</summary>
        private static void CollectInto(string dir, List<string> found, ProfileCollection result, int depth)
        {
            if (depth > 24) return;
            string[] files;
            try { files = Directory.GetFiles(dir); }
            catch { result.ReadErrors++; return; }
            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileName(files[i]);
                if (name.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)) found.Add(files[i]);
            }
            string[] subs;
            try { subs = Directory.GetDirectories(dir); }
            catch { result.ReadErrors++; return; }
            for (int i = 0; i < subs.Length; i++)
            {
                string leaf = Path.GetFileName(subs[i]);
                if (leaf.Equals("node_modules", StringComparison.OrdinalIgnoreCase)) continue;
                CollectInto(subs[i], found, result, depth + 1);
            }
        }

        public ProfileFile ReadSingle(string path, bool absoluteLabel)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                string text = File.ReadAllText(path, new UTF8Encoding(false));
                return new ProfileFile(absoluteLabel ? path : Pretty(path), path, text);
            }
            catch { return null; }
        }

        public string Pretty(string path)
        {
            try
            {
                string root = _dataRoot.TrimEnd('\\', '/');
                if (!string.IsNullOrEmpty(path) && path.StartsWith(root, StringComparison.Ordinal))
                {
                    string rel = path.Substring(root.Length).TrimStart('\\', '/');
                    return "~/.dsh/" + rel;
                }
            }
            catch { }
            return path;
        }
    }
}