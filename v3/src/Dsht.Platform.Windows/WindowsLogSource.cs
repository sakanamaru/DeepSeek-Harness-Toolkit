using System;
using System.IO;
using Dsht.Domain.Abstractions;

namespace Dsht.Platform.Windows
{
    /// <summary>日志来源（Windows）：状态目录/logs/launcher.log（与 v2.x 同路径）。只读；
    /// 文件不存在或读不到返回 null（摘要格式由领域层的 LogSummaryBuilder 负责）。</summary>
    public sealed class WindowsLogSource : ILogSource
    {
        private readonly string _stateDir;

        public WindowsLogSource(IPaths paths) { _stateDir = paths == null ? AppDomain.CurrentDomain.BaseDirectory : paths.StateDir; }

        public string LogPath { get { return Path.Combine(Path.Combine(_stateDir, "logs"), "launcher.log"); } }

        public string ReadLog()
        {
            try { return File.Exists(LogPath) ? File.ReadAllText(LogPath) : null; }
            catch { return null; }
        }
    }
}
