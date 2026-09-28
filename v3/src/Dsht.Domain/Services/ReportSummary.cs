using System;
using System.Collections.Generic;

namespace Dsht.Domain.Services
{
    /// <summary>配置摘要（纯函数）。逐条对齐 v2.x 的 ConfigSummary：
    /// 非注释行（去掉首尾空白、跳过空行与 `#` 开头）以 " ; " 连接；
    /// 文件不存在 → "(无配置文件)"；没有有效行 → "(空配置)"。
    /// 差异（有意）：v2.x 读取异常时报 "读取失败: &lt;msg&gt;"，这里把"读不到"统一按不存在处理
    /// （平台侧只回传文本或 null）——报告内容不参与标记行契约。</summary>
    public static class ConfigSummaryBuilder
    {
        public static string Build(string text)
        {
            if (text == null) return "(无配置文件)";
            List<string> kept = new List<string>();
            string[] lines = SplitLines(text);
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i] == null ? "" : lines[i].Trim();
                if (t.Length == 0) continue;
                if (t.StartsWith("#", StringComparison.Ordinal)) continue;
                kept.Add(t);
            }
            if (kept.Count == 0) return "(空配置)";
            return string.Join(" ; ", kept.ToArray());
        }

        /// <summary>按 CRLF / LF / CR 三种换行拆行（不引入 System.IO 依赖）。
        /// 语义对齐 v2.x 用的 File.ReadAllLines：空文本 → 0 行；**末尾换行不产生额外空行**。</summary>
        public static string[] SplitLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return new string[0];
            string[] raw = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (raw.Length > 0 && raw[raw.Length - 1].Length == 0)
            {
                string[] trimmed = new string[raw.Length - 1];
                Array.Copy(raw, trimmed, trimmed.Length);
                return trimmed;
            }
            return raw;
        }
    }

    /// <summary>日志摘要（纯函数）。逐条对齐 v2.x 的 LogSummary：`共 N 行；最近: <最后 3 行以 " | " 连接>`；
    /// 文件不存在 → "(无日志)"。</summary>
    public static class LogSummaryBuilder
    {
        public static string Build(string text)
        {
            if (text == null) return "(无日志)";
            string[] lines = ConfigSummaryBuilder.SplitLines(text);
            string tail = "";
            for (int i = Math.Max(0, lines.Length - 3); i < lines.Length; i++)
                tail += (tail.Length == 0 ? "" : " | ") + lines[i];
            return "共 " + lines.Length + " 行；最近: " + tail;
        }
    }
}
