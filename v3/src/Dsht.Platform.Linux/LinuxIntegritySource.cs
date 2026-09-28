using System;
using System.IO;
using System.Reflection;
using System.Text;
using Dsht.Domain.Abstractions;

namespace Dsht.Platform.Linux
{
    /// <summary>自身完整性数据源（Linux）：与 Windows 同语义（自身 exe 的 SHA-256 + 同目录 hashes.txt）。</summary>
    public sealed class LinuxIntegritySource : IIntegritySource
    {
        public string SelfPath()
        {
            try { return Assembly.GetExecutingAssembly().Location; } catch { return ""; }
        }

        public string SelfFileName()
        {
            try { return Path.GetFileName(SelfPath()); } catch { return ""; }
        }

        public string SelfHash()
        {
            try
            {
                string path = SelfPath();
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
                using (FileStream fs = File.OpenRead(path))
                {
                    byte[] h = sha.ComputeHash(fs);
                    StringBuilder sb = new StringBuilder();
                    foreach (byte b in h) sb.Append(b.ToString("x2"));
                    return sb.ToString();
                }
            }
            catch { return null; }
        }

        public string ReadManifest()
        {
            try
            {
                string exe = SelfPath();
                if (string.IsNullOrEmpty(exe)) return null;
                string dir = Path.GetDirectoryName(exe);
                if (string.IsNullOrEmpty(dir)) return null;
                string manifest = Path.Combine(dir, "hashes.txt");
                if (!File.Exists(manifest)) return null;
                return File.ReadAllText(manifest);
            }
            catch { return null; }
        }
    }
}