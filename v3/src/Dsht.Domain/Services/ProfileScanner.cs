using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Dsht.Domain.Model;

namespace Dsht.Domain.Services
{
    /// <summary>profile 块级扫描（纯函数，无 IO）。
    /// 逐条对齐 v2.x 的 ProfileCheckText：不做 YAML 全解析——`- id: x`（insert 条目）或顶层 `id: x`（修改条目）开块。
    /// 找出两类问题：① 需要 maxDepth 却没写的条目；② mcp-client 里 failOnStartupError=true 但 command 指向不存在文件的条目。
    /// 与 v2.x 的两处刻意差异：
    ///   · 字符串前缀比较用 Ordinal（v2.x 用默认比较，受区域设置影响）→ 结果确定、可复现
    ///   · 文件存在性改为注入的 pathExists 委托（v2.x 直接读磁盘判断）→ 领域层保持零 IO</summary>
    public static class ProfileScanner
    {
        public const string MissingMaxDepth = "maxDepth";
        public const string MissingCommand = "command";
        public const string McpClientPlugin = "@deepseek-ai/dsh-mcp-client";

        /// <summary>需要 maxDepth 的插件包：provider 自己管不了递归预算的那些。</summary>
        public static bool NeedsMaxDepthPlugin(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.Trim().Trim('\'', '"');
            return n == "@deepseek-ai/dsh-tool-subagent" || n == "@deepseek-ai/dsh-subagent-acp";
        }

        /// <summary>YAML 标量清洗：去引号 + 去掉引号外的行尾注释（引号内的 # 不当注释）。纯函数。</summary>
        public static string CleanYamlScalar(string v)
        {
            if (v == null) return "";
            string s = v.Trim();
            bool inS = false, inD = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\'' && !inD) inS = !inS;
                else if (c == '"' && !inS) inD = !inD;
                else if (c == '#' && !inS && !inD && i > 0 && s[i - 1] == ' ') { s = s.Substring(0, i).Trim(); break; }
            }
            if (s.Length >= 2 && ((s[0] == '\'' && s[s.Length - 1] == '\'') || (s[0] == '"' && s[s.Length - 1] == '"')))
                s = s.Substring(1, s.Length - 2);
            return s.Trim();
        }

        /// <summary>看着像路径（含分隔符或常见可执行后缀）。</summary>
        public static bool LooksLikeCommandPath(string v)
        {
            if (string.IsNullOrEmpty(v)) return false;
            if (v.IndexOf('\\') >= 0 || v.IndexOf('/') >= 0) return true;
            string l = v.ToLowerInvariant();
            return l.EndsWith(".exe", StringComparison.Ordinal) || l.EndsWith(".cmd", StringComparison.Ordinal)
                || l.EndsWith(".bat", StringComparison.Ordinal) || l.EndsWith(".ps1", StringComparison.Ordinal);
        }

        /// <summary>扫描一段 profile 文本，返回发现列表。</summary>
        public static List<ProfileFinding> Scan(string text, string fileLabel, Func<string, bool> pathExists)
        {
            List<ProfileFinding> list = new List<ProfileFinding>();
            if (string.IsNullOrEmpty(text)) return list;
            string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            int i = 0;
            while (i < lines.Length)
            {
                int entryIndent; string body; bool dashForm;
                if (!TryEntryStart(lines[i], out entryIndent, out body, out dashForm)) { i++; continue; }
                Match idm = Regex.Match(body, dashForm ? @"^-\s+id:\s*(.*)$" : @"^id:\s*(.*)$");
                string id = CleanYamlScalar(idm.Groups[1].Value);
                int j = BlockEnd(lines, i, entryIndent, dashForm);

                string name = null;
                bool hasConfig = false, hasMaxDepth = false, hasKeyAnywhere = false;
                int configIndent = -1;
                string mcpCommand = null; bool mcpFailOnStartup = false;
                for (int k = i + 1; k < j; k++)
                {
                    string s = (lines[k] == null ? "" : lines[k]).TrimEnd();
                    if (s.Trim().Length == 0 || s.TrimStart().StartsWith("#", StringComparison.Ordinal)) continue;
                    int ind = 0; while (ind < s.Length && s[ind] == ' ') ind++;
                    string b = s.Substring(ind);
                    if (name == null && b.StartsWith("name:", StringComparison.Ordinal)) name = CleanYamlScalar(b.Substring(5));
                    if (b.StartsWith("maxDepth:", StringComparison.Ordinal)) { hasKeyAnywhere = true; if (hasConfig && ind > configIndent) hasMaxDepth = true; }
                    if (!hasConfig && b.StartsWith("config:", StringComparison.Ordinal)) { hasConfig = true; configIndent = ind; continue; }
                    if (b.StartsWith("command:", StringComparison.Ordinal)) mcpCommand = CleanYamlScalar(b.Substring(8));
                    if (b.StartsWith("failOnStartupError:", StringComparison.Ordinal)) mcpFailOnStartup = CleanYamlScalar(b.Substring(19)).ToLowerInvariant() == "true";
                }

                if (NeedsMaxDepthPlugin(name) && !hasMaxDepth && !hasKeyAnywhere)
                {
                    ProfileFinding f = new ProfileFinding();
                    f.File = fileLabel; f.Line = i + 1; f.Id = id; f.Missing = MissingMaxDepth;
                    f.Hint = hasConfig ? "set maxDepth: 'provider-managed'" : "set maxDepth: 'provider-managed' (no config: block - manual)";
                    list.Add(f);
                }
                else if (!string.IsNullOrEmpty(name) && name.Trim().Trim('\'', '"') == McpClientPlugin
                         && mcpFailOnStartup && LooksLikeCommandPath(mcpCommand)
                         && !(pathExists != null && pathExists(mcpCommand)))
                {
                    ProfileFinding f = new ProfileFinding();
                    f.File = fileLabel; f.Line = i + 1; f.Id = id; f.Missing = MissingCommand;
                    f.Hint = "command path not found: " + mcpCommand;
                    list.Add(f);
                }
                i = j;
            }
            return list;
        }

        private static bool TryEntryStart(string line, out int indent, out string body, out bool dashForm)
        {
            indent = 0; body = ""; dashForm = false;
            if (line == null) return false;
            string t = line.TrimEnd();
            if (t.Trim().Length == 0 || t.TrimStart().StartsWith("#", StringComparison.Ordinal)) return false;
            while (indent < t.Length && t[indent] == ' ') indent++;
            body = t.Substring(indent);
            if (Regex.IsMatch(body, @"^-\s+id:\s*\S")) { dashForm = true; return true; }
            if (Regex.IsMatch(body, @"^id:\s*\S")) { dashForm = false; return true; }
            return false;
        }

        private static int BlockEnd(string[] lines, int start, int entryIndent, bool dashForm)
        {
            int j = start + 1;
            while (j < lines.Length)
            {
                string s = (lines[j] == null ? "" : lines[j]).TrimEnd();
                if (s.Trim().Length == 0) { j++; continue; }
                int ind = 0; while (ind < s.Length && s[ind] == ' ') ind++;
                string b = s.Substring(ind);
                if (b.StartsWith("#", StringComparison.Ordinal)) { j++; continue; }
                if (dashForm)
                {
                    if (ind < entryIndent) break;
                    if (ind == entryIndent && (b.StartsWith("- ", StringComparison.Ordinal) || b == "-")) break;
                }
                else
                {
                    if (ind <= entryIndent && (b.StartsWith("id:", StringComparison.Ordinal) || b.StartsWith("- ", StringComparison.Ordinal) || b == "-")) break;
                }
                j++;
            }
            return j;
        }
    }
}