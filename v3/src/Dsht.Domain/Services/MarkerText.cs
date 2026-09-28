using System;
using System.Globalization;
using System.Text;

namespace Dsht.Domain.Services
{
    /// <summary>标记行里的**自由文本**编解码（纯函数）。
    /// 为什么需要：标记行用 `键=值` 且以空格分隔，而会话标题可能含空格、换行、`%` 等
    /// （本机实测标题形如 `API 密钥消费额度超限报错`）——不转义就会把一行拆成好几段。
    /// 规则：`%` → `%25`，空格 → `%20`，制表/换行 → `%09`/`%0A`/`%0D`，其余原样（含中文，直接 UTF-8 输出）。
    /// 两端（CLI 打印 / GUI 解析）必须一致；两边的单测用同一组样例做往返校验。</summary>
    public static class MarkerText
    {
        /// <summary>空/未知 → `-`（标记行里不用空值）。</summary>
        public static string Encode(string text)
        {
            if (string.IsNullOrEmpty(text)) return "-";
            StringBuilder sb = new StringBuilder(text.Length + 8);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '%') sb.Append("%25");
                else if (c == ' ') sb.Append("%20");
                else if (c == '\t') sb.Append("%09");
                else if (c == '\n') sb.Append("%0A");
                else if (c == '\r') sb.Append("%0D");
                else sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>解码；`-` 或无法解码 → 空串（诚实降级，不猜）。</summary>
        public static string Decode(string text)
        {
            if (string.IsNullOrEmpty(text) || text == "-") return "";
            StringBuilder sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != '%' || i + 2 >= text.Length) { sb.Append(c); continue; }
                int code;
                if (!int.TryParse(text.Substring(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code)) { sb.Append(c); continue; }
                sb.Append((char)code);
                i += 2;
            }
            return sb.ToString();
        }
    }
}