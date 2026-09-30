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
                if (line.StartsWith("DOCTOR_WARN", StringComparison.Ordinal)) { d.Ok = true; continue; }   // 标记行只表示"体检跑过"，计数以条目行为准
                if (line.StartsWith("DOCTOR_ERROR", StringComparison.Ordinal)) { d.Ok = true; continue; }
                if (line.StartsWith("[错误]", StringComparison.Ordinal) || line.StartsWith("[ERROR", StringComparison.OrdinalIgnoreCase)) { d.Error++; if (d.ErrorLines.Count < 8) d.ErrorLines.Add(line); continue; }
                if (line.StartsWith("[提醒]", StringComparison.Ordinal) || line.StartsWith("[WARN", StringComparison.OrdinalIgnoreCase)) { d.Warn++; if (d.WarnLines.Count < 8) d.WarnLines.Add(line); continue; }
                if (line.StartsWith("[OK]", StringComparison.Ordinal) || line.StartsWith("[通过]", StringComparison.Ordinal)) { d.Pass++; continue; }
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
namespace Dsht.Gui.Avalonia.Markers
{
    /// <summary>一条备份（`backup-list --detail` 的 BACKUP_ITEM 行）。</summary>
    public sealed class BackupItem
    {
        public string Name = "";
        public string Kind = "Manual";
        /// <summary>F14 FIX: the CLI lists entries it cannot restore from and marks them. This
        /// lets the page show them as invalid instead of as ordinary backups.</summary>
        public bool Invalid = false;
        public long Bytes;
        public string Time = "";
        public string KindText { get { return Invalid ? "无效（不能用于恢复）" : (Kind == "Manual" ? "手动" : (Kind == "Auto" ? "自动" : Kind)); } }
        public string SizeText { get { return Invalid ? "—" : SessionRow.Human(Bytes); } }
    }

    /// <summary>一个配置项（`config-get` 的 CONFIG 行）。</summary>
    public sealed class ConfigItem
    {
        public string Key = "";
        public string Value = "";
        /// <summary>人话说明（治"这键是干什么的"）。</summary>
        public string Desc
        {
            get
            {
                switch (Key)
                {
                    case "lang": return "界面语言（auto / zh / en）";
                    case "host": return "dsh 监听地址（默认 127.0.0.1）";
                    case "ws": return "默认工作区目录（留空=自动探测）";
                    case "keep_backups": return "自动备份保留份数（最少 3）";
                    case "check_update": return "启动时检查工具箱更新";
                    case "check_dsh_update": return "启动时检查 dsh 更新";
                    case "update_channel": return "更新通道（stable / beta）";
                    case "close_action": return "关闭窗口时的行为";
                    case "auto_start": return "启动时自动启动 dsh";
                    case "dsh_versions": return "已安装的 dsh 版本（只读）";
                    default: return "";
                }
            }
        }
        public bool ReadOnly { get { return Key == "dsh_versions"; } }
        public bool IsSwitch { get { return Key == "check_update" || Key == "check_dsh_update" || Key == "auto_start"; } }
    }

    public static class ConfigMarkers
    {
        public static List<ConfigItem> Parse(string output)
        {
            List<ConfigItem> list = new List<ConfigItem>();
            if (string.IsNullOrEmpty(output)) return list;
            string[] lines = output.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] == null ? "" : lines[i].Trim();
                if (!line.StartsWith("CONFIG ", StringComparison.Ordinal)) continue;
                string rest = line.Substring("CONFIG ".Length);
                int sp = rest.IndexOf(' ');
                ConfigItem c = new ConfigItem();
                if (sp < 0) { c.Key = rest; }
                else { c.Key = rest.Substring(0, sp); c.Value = rest.Substring(sp + 1).Trim(); }
                list.Add(c);
            }
            return list;
        }
    }

    public static class BackupItems
    {
        public static List<BackupItem> Parse(string output)
        {
            List<BackupItem> list = new List<BackupItem>();
            if (string.IsNullOrEmpty(output)) return list;
            string[] lines = output.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] == null ? "" : lines[i].Trim();
                if (line.StartsWith("BACKUP_ITEM_INVALID ", StringComparison.Ordinal))
                {
                    string badName = line.Substring("BACKUP_ITEM_INVALID ".Length).Trim();
                    if (badName.Length > 0)
                    {
                        BackupItem bad = new BackupItem();
                        bad.Name = badName;
                        bad.Kind = "Invalid";
                        bad.Invalid = true;
                        list.Add(bad);
                    }
                    continue;
                }
                if (!line.StartsWith("BACKUP_ITEM ", StringComparison.Ordinal)) continue;
                string[] p = line.Substring("BACKUP_ITEM ".Length).Split(new char[] { ' ' }, 4);
                if (p.Length < 1) continue;
                BackupItem b = new BackupItem();
                b.Name = p[0];
                if (p.Length > 1) b.Kind = p[1];
                long n; if (p.Length > 2 && long.TryParse(p[2], out n)) b.Bytes = n;
                if (p.Length > 3) b.Time = p[3].Trim();
                list.Add(b);
            }
            return list;
        }
    }
}