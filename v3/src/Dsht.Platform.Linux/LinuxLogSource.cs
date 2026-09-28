using System;
using System.IO;
using Dsht.Domain.Abstractions;

namespace Dsht.Platform.Linux
{
    /// <summary>日志来源（Linux）：与 Windows 实现同语义（状态目录/logs/launcher.log）。只读；
    /// 文件不存在或读不到返回 null（摘要格式由领域层的 LogSummaryBuilder 负责）。</summary>
    public sealed class LinuxLogSource : ILogSource
    {
        private readonly string _stateDir;

        public LinuxLogSource(IPaths paths) { _stateDir = paths == null ? AppDomain.CurrentDomain.BaseDirectory : paths.StateDir; }

        public string LogPath { get { return Path.Combine(Path.Combine(_stateDir, "logs"), "launcher.log"); } }

        public string ReadLog()
        {
            try { return File.Exists(LogPath) ? File.ReadAllText(LogPath) : null; }
            catch { return null; }
        }
    }
}
