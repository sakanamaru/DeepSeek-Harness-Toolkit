using System;
using System.IO;
using System.Text;
using Dsht.Domain.Abstractions;

namespace Dsht.Platform.Windows
{
    /// <summary>配置读写（Windows）：状态目录/launcher.config，UTF-8 无 BOM（与 v2.x SaveConfig 一致）。</summary>
    public sealed class WindowsConfigSource : IConfigSource
    {
        private readonly string _stateDir;
        public WindowsConfigSource(IPaths paths) { _stateDir = paths == null ? AppDomain.CurrentDomain.BaseDirectory : paths.StateDir; }
        public string ConfigPath { get { return Path.Combine(_stateDir, "launcher.config"); } }

        public string ReadConfig()
        {
            try { return File.Exists(ConfigPath) ? File.ReadAllText(ConfigPath, new UTF8Encoding(false)) : null; }
            catch { return null; }
        }

        public void WriteConfig(string text)
        {
            try { File.WriteAllText(ConfigPath, text, new UTF8Encoding(false)); }
            catch { }
        }
    }
}