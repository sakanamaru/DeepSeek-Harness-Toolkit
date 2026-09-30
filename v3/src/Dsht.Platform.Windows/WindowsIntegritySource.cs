using System;
using System.IO;
using System.Reflection;
using System.Text;
using Dsht.Domain.Abstractions;

namespace Dsht.Platform.Windows
{
    /// <summary>自身完整性数据源（Windows）：自身 exe 的 SHA-256 与同目录 hashes.txt。
    /// 只负责取数据，判定交给领域层的 ManifestParser + IntegrityJudge。</summary>
    public sealed class WindowsIntegritySource : IIntegritySource
    {
        public string SelfPath()
        {
            try
            {
                string loc = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(loc) && File.Exists(loc)) return loc;
                // ✗✗ 单文件发布（.NET 5+）时 Location 是**空的** ✗
                //    → 实测：SelfHash/SelfFileName/ReadManifest **全为 null** → Judge 得 Unknown → 整条自检"跳过" ✓✓
                //    （与版本号那次是**同一个坑** ✓ 单文件下要用**进程主模块路径** ✓）
                try
                {
                    System.Diagnostics.Process proc = System.Diagnostics.Process.GetCurrentProcess();
                    if (proc != null && proc.MainModule != null && !string.IsNullOrEmpty(proc.MainModule.FileName))
                        return proc.MainModule.FileName;
                }
                catch { }
                return "";
            }
            catch { return ""; }
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