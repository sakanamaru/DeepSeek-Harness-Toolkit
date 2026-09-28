using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Dsht.Domain.Services
{
    /// <summary>极简 JSON 解析（纯函数，零依赖）。
    /// 为什么自己写：V3 整树要能用 .NET Framework 4 的 csc 本地编译（CI 才用 net8），
    /// 而 `System.Text.Json` 在 .NET Framework 4 上不存在、`DataContractJsonSerializer` 又需要额外引用与类型标注；
    /// 我们只读 dsh 落盘的两个固定形状（profile manifest / 会话投影），所以一个 ~200 行的递归下降解析器
    /// 就够，且**完全可单测**。
    /// 语义：严格 JSON（不支持注释/尾逗号）；解析失败返回 null —— 调用方必须按"格式不认"诚实降级，绝不猜。</summary>
    public sealed class JNode
    {
        public enum Kind { Null, Bool, Number, String, Array, Object }

        public Kind NodeKind;
        public bool BoolValue;
        public double NumberValue;
        public string StringValue;
        public List<JNode> Items;                                   // Array
        public Dictionary<string, JNode> Members;                   // Object（大小写敏感，与 JSON 一致）

        public bool IsObject { get { return NodeKind == Kind.Object; } }
        public bool IsArray { get { return NodeKind == Kind.Array; } }
        public bool IsString { get { return NodeKind == Kind.String; } }

        /// <summary>对象取成员；不存在或不是对象 → null。</summary>
        public JNode Get(string name)
        {
            if (NodeKind != Kind.Object || Members == null || name == null) return null;
            JNode v;
            return Members.TryGetValue(name, out v) ? v : null;
        }

        /// <summary>按路径取（每层一个成员名）；任一层缺失 → null。</summary>
        public JNode Path(params string[] names)
        {
            JNode cur = this;
            if (names == null) return null;
            for (int i = 0; i < names.Length; i++)
            {
                if (cur == null) return null;
                cur = cur.Get(names[i]);
            }
            return cur;
        }

        public string AsString(string fallback) { return NodeKind == Kind.String ? StringValue : fallback; }
        public double AsNumber(double fallback) { return NodeKind == Kind.Number ? NumberValue : fallback; }
        public bool AsBool(bool fallback) { return NodeKind == Kind.Bool ? BoolValue : fallback; }

        /// <summary>字符串数组（元素里的非字符串按 fallback 跳过）。</summary>
        public string[] AsStringArray()
        {
            if (NodeKind != Kind.Array || Items == null) return new string[0];
            List<string> r = new List<string>();
            for (int i = 0; i < Items.Count; i++)
                if (Items[i] != null && Items[i].NodeKind == Kind.String) r.Add(Items[i].StringValue);
            return r.ToArray();
        }
    }

    /// <summary>极简 JSON 解析器（见 JNode 的说明）。</summary>
    public static class JsonLite
    {
        /// <summary>解析失败返回 null（绝不抛、绝不猜）。</summary>
        public static JNode Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            try
            {
                int i = 0;
                JNode n = ParseValue(text, ref i, 0);
                if (n == null) return null;
                SkipWs(text, ref i);
                if (i != text.Length) return null;      // 尾部还有垃圾 → 视为格式不认
                return n;
            }
            catch { return null; }
        }

        private const int MaxDepth = 64;

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') { i++; continue; }
                break;
            }
        }

        private static JNode ParseValue(string s, ref int i, int depth)
        {
            if (depth > MaxDepth) return null;
            SkipWs(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i, depth);
            if (c == '[') return ParseArray(s, ref i, depth);
            if (c == '"') { string str = ParseString(s, ref i); return str == null ? null : new JNode { NodeKind = JNode.Kind.String, StringValue = str }; }
            if (c == 't') { if (Match(s, ref i, "true")) return new JNode { NodeKind = JNode.Kind.Bool, BoolValue = true }; return null; }
            if (c == 'f') { if (Match(s, ref i, "false")) return new JNode { NodeKind = JNode.Kind.Bool, BoolValue = false }; return null; }
            if (c == 'n') { if (Match(s, ref i, "null")) return new JNode { NodeKind = JNode.Kind.Null }; return null; }
            return ParseNumber(s, ref i);
        }

        private static bool Match(string s, ref int i, string word)
        {
            if (i + word.Length > s.Length) return false;
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        private static JNode ParseObject(string s, ref int i, int depth)
        {
            i++;   // {
            JNode o = new JNode { NodeKind = JNode.Kind.Object, Members = new Dictionary<string, JNode>() };
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return o; }
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"') return null;
                string key = ParseString(s, ref i);
                if (key == null) return null;
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') return null;
                i++;
                JNode v = ParseValue(s, ref i, depth + 1);
                if (v == null) return null;
                o.Members[key] = v;
                SkipWs(s, ref i);
                if (i >= s.Length) return null;
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return o; }
                return null;
            }
        }

        private static JNode ParseArray(string s, ref int i, int depth)
        {
            i++;   // [
            JNode a = new JNode { NodeKind = JNode.Kind.Array, Items = new List<JNode>() };
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return a; }
            while (true)
            {
                JNode v = ParseValue(s, ref i, depth + 1);
                if (v == null) return null;
                a.Items.Add(v);
                SkipWs(s, ref i);
                if (i >= s.Length) return null;
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return a; }
                return null;
            }
        }

        private static string ParseString(string s, ref int i)
        {
            i++;   // 开引号
            StringBuilder sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) return null;
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) return null;
                        int cp;
                        if (!int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out cp)) return null;
                        sb.Append((char)cp);
                        i += 4;
                        break;
                    default: return null;
                }
            }
            return null;
        }

        private static JNode ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            bool any = false;
            while (i < s.Length && ((s[i] >= '0' && s[i] <= '9') || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+' || s[i] == '-')) { i++; any = true; }
            if (!any) return null;
            double d;
            if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return null;
            return new JNode { NodeKind = JNode.Kind.Number, NumberValue = d };
        }
    }
}
