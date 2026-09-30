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
        /// <summary>官方桌面端进程名（`STATUS_DESKTOP <name>` ✓）；空 = 没检测到。
        /// 桌面端**不监听 3080**（实测走 19387 ✓）→ 端口探测会说"未运行" ✓ 但它确实在跑 ✓✓</summary>
        public string DesktopClient = "";
        public string DesktopPid = "";
        public string DesktopStart = "";
        public string DesktopUptime = "";
        /// <summary>未识别的 STATUS_* 标记（原样保留 —— 未来 dsh 加新标记时界面不会漏信息）。</summary>
        public List<KeyValuePair<string, string>> Extras = new List<KeyValuePair<string, string>>();

        public string StateText
        {
            get
            {
                if (State == 0) return "运行中";
                if (State == 1) return "启动中";
                // 桌面端在跑时**不能显示"未运行"** ✗ —— 端口没监听是真的，但 dsh 确实在运行 ✓
                // （2026-09-30 用户反馈："监测到 dsh desktop 应该显示 desktop 运行中而不是未运行" ✓✓）
                if (State == 2 && !string.IsNullOrEmpty(DesktopClient)) return "桌面端运行中";
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
                    else if (key == "STATUS_DESKTOP") { s.DesktopClient = val; s.Ok = true; }   // 官方桌面端在跑 ✓
                    else if (key == "STATUS_DESKTOP_PID") { s.DesktopPid = val; s.Ok = true; }
                    else if (key == "STATUS_DESKTOP_START") { s.DesktopStart = val; s.Ok = true; }
                    else if (key == "STATUS_DESKTOP_UPTIME") { s.DesktopUptime = val; s.Ok = true; }
                else s.Extras.Add(new KeyValuePair<string, string>(key, val));
            }
            return s;
        }
    }
}