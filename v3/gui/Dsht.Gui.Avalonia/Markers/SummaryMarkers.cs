using System;
using System.Collections.Generic;

namespace Dsht.Gui.Avalonia.Markers
{
    /// <summary>体检摘要（`doctor` 的 DOCTOR_OK/WARN/ERROR 与分级条目行）。</summary>
    public sealed class DoctorSummary
    {
        public bool Ok;
        public int Pass;
        public int Warn;
        public int Error;
        public List<string> WarnLines = new List<string>();
        public List<string> ErrorLines = new List<string>();
        public string Headline
        {
            get
            {
                if (!Ok) return "未运行体检";
                if (Error > 0) return "发现 " + Error + " 个错误";
                if (Warn > 0) return "发现 " + Warn + " 个提醒";
                return "一切正常";
            }
        }
    }

    /// <summary>备份摘要（`backup-list` 的 BACKUP_LIST_OK 与路径行）。</summary>
    public sealed class BackupSummary
    {
        public bool Ok;
        public int Count;
        public string Latest = "";
    }

    public static class SummaryMarkers
    {
        public static DoctorSummary ParseDoctor(string output)
        {
            DoctorSummary d = new DoctorSummary();
            if (string.IsNullOrEmpty(output)) return d;
            string[] lines = output.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] == null ? "" : lines[i].Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("DOCTOR_OK", StringComparison.Ordinal)) { d.Ok = true; continue; }
                if (line.StartsWith("DOCTOR_WARN", StringComparison.Ordinal)) { d.Warn++; d.Ok = true; continue; }
                if (line.StartsWith("DOCTOR_ERROR", StringComparison.Ordinal)) { d.Error++; d.Ok = true; continue; }
                if (line.StartsWith("[错误]", StringComparison.Ordinal) || line.StartsWith("[ERROR", StringComparison.OrdinalIgnoreCase)) { if (d.ErrorLines.Count < 8) d.ErrorLines.Add(line); continue; }
                if (line.StartsWith("[提醒]", StringComparison.Ordinal) || line.StartsWith("[WARN", StringComparison.OrdinalIgnoreCase)) { if (d.WarnLines.Count < 8) d.WarnLines.Add(line); continue; }
                if (line.StartsWith("[通过]", StringComparison.Ordinal)) { d.Pass++; continue; }
            }
            return d;
        }

        public static BackupSummary ParseBackups(string output)
        {
            BackupSummary b = new BackupSummary();
            if (string.IsNullOrEmpty(output)) return b;
            string[] lines = output.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] == null ? "" : lines[i].Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("BACKUP_LIST_OK", StringComparison.Ordinal))
                {
                    b.Ok = true;
                    string rest = line.Substring("BACKUP_LIST_OK".Length).Trim();
                    int n;
                    if (int.TryParse(rest, out n)) b.Count = n;
                    continue;
                }
                if (line.StartsWith("BACKUP_ITEM", StringComparison.Ordinal)) { if (b.Latest.Length == 0) b.Latest = line.Substring("BACKUP_ITEM".Length).Trim(); continue; }
                if (!line.StartsWith("BACKUP_", StringComparison.Ordinal) && b.Latest.Length == 0 && line.IndexOf(':') == 1) b.Latest = line;
            }
            return b;
        }
    }
}