using System;
using System.Collections.Generic;

namespace Dsht.Gui.Avalonia.Markers
{
    /// <summary>`status --detail` 的解析结果。</summary>
    public sealed class StatusSnapshot
    {
        public bool Ok;
        public int State;                 // 0=运行中 1=启动中 2=未运行 3=未知
        public string Pid = "";
        public string Start = "";
        public string Uptime = "";
        /// <summary>未识别的 STATUS_* 标记（原样保留 —— 未来 dsh 加新标记时界面不会漏信息）。</summary>
        public List<KeyValuePair<string, string>> Extras = new List<KeyValuePair<string, string>>();

        public string StateText
        {
            get
            {
                if (State == 0) return "运行中";
                if (State == 1) return "启动中";
                if (State == 2) return "未运行";
                return "未知";
            }
        }
    }

    /// <summary>解析 CLI `status --detail` 的标记行（纯函数，**绝不抛**）。
    /// 依据只来自可观测事实（端口监听 + 进程），界面不替它下别的结论。</summary>
    public static class StatusMarkers
    {
        public static StatusSnapshot Parse(string output)
        {
            StatusSnapshot s = new StatusSnapshot();
            if (string.IsNullOrEmpty(output)) return s;
            string[] lines = output.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] == null ? "" : lines[i].Trim();
                if (line.Length == 0 || !line.StartsWith("STATUS_", StringComparison.Ordinal)) continue;
                string[] parts = line.Split(new char[] { ' ' }, 2);
                string key = parts[0];
                string val = parts.Length > 1 ? parts[1].Trim() : "";
                if (key == "STATUS_UP") { s.State = 0; s.Ok = true; }
                else if (key == "STATUS_STARTING") { s.State = 1; s.Ok = true; }
                else if (key == "STATUS_DOWN") { s.State = 2; s.Ok = true; }
                else if (key == "STATUS_PID") { s.Pid = val; s.Ok = true; }
                else if (key == "STATUS_START") { s.Start = val; s.Ok = true; }
                else if (key == "STATUS_UPTIME") { s.Uptime = val; s.Ok = true; }
                else s.Extras.Add(new KeyValuePair<string, string>(key, val));
            }
            return s;
        }
    }
}