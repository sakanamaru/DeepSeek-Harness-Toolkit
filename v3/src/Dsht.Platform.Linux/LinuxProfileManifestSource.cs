using System;
using System.Collections.Generic;
using System.IO;
using Dsht.Domain.Abstractions;

namespace Dsht.Platform.Linux
{
    /// <summary>profile 清单来源（Linux）：与 Windows 实现同语义（`&lt;数据根&gt;/profiles/&lt;name&gt;/package.json`，只读）。</summary>
    public sealed class LinuxProfileManifestSource : IProfileManifestSource
    {
        private readonly string _dataRoot;

        public LinuxProfileManifestSource(IPaths paths) { _dataRoot = paths == null ? "" : (paths.DataRoot ?? ""); }

        public string ProfilesRoot { get { return Path.Combine(_dataRoot, "profiles"); } }

        public string[] ListProfiles()
        {
            try
            {
                if (!Directory.Exists(ProfilesRoot)) return new string[0];
                string[] dirs = Directory.GetDirectories(ProfilesRoot);
                Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
                List<string> names = new List<string>();
                for (int i = 0; i < dirs.Length; i++) names.Add(Path.GetFileName(dirs[i].TrimEnd('\\', '/')));
                return names.ToArray();
            }
            catch { return new string[0]; }
        }

        public string ReadPatch(string profileName)
        {
            try
            {
                if (string.IsNullOrEmpty(profileName)) return null;
                string p = Path.Combine(Path.Combine(ProfilesRoot, profileName), "cordis.patch.yml");
                return File.Exists(p) ? File.ReadAllText(p) : null;
            }
            catch { return null; }
        }

        public string ReadBundleManifest(string profileName, string bundleId)
        {
            try
            {
                if (string.IsNullOrEmpty(profileName) || string.IsNullOrEmpty(bundleId)) return null;
                string p = Path.Combine(Path.Combine(Path.Combine(ProfilesRoot, profileName), "node_modules"), Path.Combine(bundleId.Split('/')));
                p = Path.Combine(p, "package.json");
                return File.Exists(p) ? File.ReadAllText(p) : null;
            }
            catch { return null; }
        }

        public string ReadManifest(string profileName)
        {
            try
            {
                if (string.IsNullOrEmpty(profileName)) return null;
                string p = Path.Combine(Path.Combine(ProfilesRoot, profileName), "package.json");
                return File.Exists(p) ? File.ReadAllText(p) : null;
            }
            catch { return null; }
        }
    }
}
