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
            try
            {
                string dir = string.IsNullOrEmpty(dirOverride) ? ProfilesRoot : dirOverride;
                if (!Directory.Exists(dir)) return result;
                List<string> merged = new List<string>();
                merged.AddRange(Directory.GetFiles(dir, "*.yml", SearchOption.AllDirectories));
                merged.AddRange(Directory.GetFiles(dir, "*.yaml", SearchOption.AllDirectories));
                merged.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (string f in merged)
                {
                    if (!includeVendor && ProfileScanner.IsVendorPath(f)) { result.SkippedVendor++; continue; }
                    string text;
                    try { text = File.ReadAllText(f, new UTF8Encoding(false)); }
                    catch { continue; }
                    result.Files.Add(new ProfileFile(absoluteLabel ? f : Pretty(f), f, text));
                }
            }
            catch { }
            return result;
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