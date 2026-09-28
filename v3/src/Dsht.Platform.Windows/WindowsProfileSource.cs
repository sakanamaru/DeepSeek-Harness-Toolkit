using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Dsht.Domain.Abstractions;
using Dsht.Domain.Model;
using Dsht.Domain.Services;

namespace Dsht.Platform.Windows
{
    /// <summary>profile 文件来源（Windows）。逐条对齐 v2.x 的 ProfileCheckScan：
    ///   · 目录默认 = 数据根/profiles；数据根优先 用户主目录/.dsh，其次 %APPDATA%/.dsh、%LOCALAPPDATA%/.dsh
    ///   · 递归收集 *.yml 与 *.yaml，按 OrdinalIgnoreCase 排序（与 v2.x 同序）
    ///   · 默认跳过 node_modules 下的 vendor 补丁层并计数（透明）
    ///   · 标签默认脱敏为 ~/.dsh/&lt;相对路径&gt;；--abs 时用绝对路径
    /// 差异：vendor 判定改用领域层 IsVendorPath（按路径段匹配）→ 不依赖 Windows 分隔符，Linux 可复用同一规则。</summary>
    public sealed class WindowsProfileSource : IProfileSource
    {
        private const string DataDirName = ".dsh";
        private readonly string _dataRoot;

        public WindowsProfileSource() { _dataRoot = ResolveDataRoot(); }

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
                if (!string.IsNullOrEmpty(path) && path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    string rel = path.Substring(root.Length).TrimStart('\\', '/');
                    return "~/" + DataDirName + "/" + rel.Replace('\\', '/');
                }
            }
            catch { }
            return path;
        }

        private static string ResolveDataRoot()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] candidates = new string[]
            {
                Path.Combine(home, DataDirName),
                Path.Combine(appdata, DataDirName),
                Path.Combine(local, DataDirName)
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                try { if (Directory.Exists(candidates[i])) return candidates[i]; } catch { }
            }
            return candidates[0];
        }
    }
}