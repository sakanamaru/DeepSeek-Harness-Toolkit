using System;
using System.Collections.Generic;
using System.IO;
using Dsht.Domain.Abstractions;

namespace Dsht.Platform.Windows
{
    /// <summary>会话投影来源（Windows）：`&lt;数据根&gt;/storages/…`，只读。</summary>
    public sealed class WindowsSessionStatsSource : ISessionStatsSource
    {
        private readonly string _dataRoot;

        public WindowsSessionStatsSource(IPaths paths) { _dataRoot = paths == null ? "" : (paths.DataRoot ?? ""); }

        public string SessionsDir { get { return Path.Combine(Path.Combine(Path.Combine(Path.Combine(_dataRoot, "storages"), "session_projcache"), "sessions")); } }

        public string AggregatePath { get { return Path.Combine(Path.Combine(_dataRoot, "storages"), "session_projcache.json"); } }

        public string SnapshotPath { get { return Path.Combine(Path.Combine(_dataRoot, "toolkit-bridge"), "sessions.json"); } }

        public string[] ListSessionFiles()
        {
            try
            {
                if (!Directory.Exists(SessionsDir)) return new string[0];
                string[] files = Directory.GetFiles(SessionsDir, "*.json");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                return files;
            }
            catch { return new string[0]; }
        }

        public string ReadText(string path)
        {
            try { return (string.IsNullOrEmpty(path) || !File.Exists(path)) ? null : File.ReadAllText(path); }
            catch { return null; }
        }
    }
}
