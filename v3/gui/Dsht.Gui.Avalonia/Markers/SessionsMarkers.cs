using System;
using System.Collections.Generic;
using System.Globalization;

namespace Dsht.Gui.Avalonia.Markers
{
    /// <summary>一个会话行（来自 CLI `sessions` 的 `SESSION` 标记行）。缺失/未知的数值用 -1 表示，**不假装 0**。</summary>
    public sealed class SessionRow
    {
        public string Id = "";
        public string Created = "";        // "unknown" → 空串
        public string Last = "";           // 同上
        public long Turns;
        public long Steps;
        public long In;                    // 输入侧总量（未命中缓存 + 命中缓存）
        public long Out;
        public long CacheRead;
        public double HitPercent = -1;     // 缓存命中率（%）
        public double DecodeTps = -1;      // 解码速度（tok/s）
        public long TtftMs = -1;           // 首 token（dsh 投影原值，多步累计；未知 = -1）
        public double CtxPercent = -1;     // 上下文压力（%）
        public bool Blank;
        public bool Live;

        /// <summary>列表里显示的短 id（前 8 位，便于人眼对齐）。</summary>
        public string ShortId { get { return Id.Length > 8 ? Id.Substring(0, 8) : Id; } }

        public string LiveText { get { return Live ? "运行中" : (Blank ? "空会话" : "已结束"); } }

        public string HitText { get { return HitPercent < 0 ? "unknown" : HitPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%"; } }
        public string DecodeText { get { return DecodeTps < 0 ? "unknown" : DecodeTps.ToString("0.0", CultureInfo.InvariantCulture) + " tok/s"; } }
        public string TtftText { get { return TtftMs < 0 ? "unknown" : TtftMs.ToString(CultureInfo.InvariantCulture) + " ms"; } }
        public string CtxText { get { return CtxPercent < 0 ? "unknown" : CtxPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%"; } }
        public string LastText { get { return string.IsNullOrEmpty(Last) ? "unknown" : Last; } }

        public string InText { get { return In.ToString("N0", CultureInfo.InvariantCulture); } }
        public string OutText { get { return Out.ToString("N0", CultureInfo.InvariantCulture); } }
        public string CacheReadText { get { return CacheRead.ToString("N0", CultureInfo.InvariantCulture); } }
    }

    /// <summary>`sessions` 命令的完整结果（含汇总与数据来源）。</summary>
    public sealed class SessionsSnapshot
    {
        public bool Ok;
        public string FailReason = "";
        public int Count;
        public int NonBlank;
        public int Live;
        public string Source = "";         // snapshot | disk | aggregate
        public string Root = "";
        public long TotalIn;
        public long TotalOut;
        public long TotalCacheRead;
        public double TotalHitPercent = -1;
        public double TotalDecodeTps = -1;
        public List<SessionRow> Rows = new List<SessionRow>();

        /// <summary>数据来源的中文说明（给界面用；插件缺失时如实说明少了什么）。</summary>
        public string SourceText
        {
            get
            {
                if (Source == "snapshot") return "数据来源：桥接插件快照（含实时 live 标记）";
                if (Source == "disk") return "数据来源：磁盘投影（插件未装或未就绪 —— 没有“正在运行”这一项）";
                if (Source == "aggregate") return "数据来源：投影总表（每会话投影文件缺失时的兜底）";
                return "数据来源：未知";
            }
        }
    }

    /// <summary>解析 CLI `sessions` 的标记行（纯函数，**绝不抛**：无法解析的行跳过）。
    /// 契约见 `v3/README.md` 的命令表；这里的解析器与 CLI 的打印格式是同一份约定的两端。</summary>
    public static class SessionsMarkers
    {
        public static SessionsSnapshot Parse(string output)
        {
            SessionsSnapshot s = new SessionsSnapshot();
            if (string.IsNullOrEmpty(output)) return s;
            string[] lines = output.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] == null ? "" : lines[i].Trim();
                if (line.Length == 0) continue;
                try
                {
                    if (line.StartsWith("SESSIONS_FAIL", StringComparison.Ordinal))
                    {
                        s.FailReason = line.Substring("SESSIONS_FAIL".Length).Trim();
                        continue;
                    }
                    if (line.StartsWith("SESSIONS_OK", StringComparison.Ordinal)) { s.Count = (int)Num(Tail(line, "SESSIONS_OK"), 0); s.Ok = true; continue; }
                    if (line.StartsWith("SESSIONS_NONBLANK", StringComparison.Ordinal)) { s.NonBlank = (int)Num(Tail(line, "SESSIONS_NONBLANK"), 0); continue; }
                    if (line.StartsWith("SESSIONS_LIVE", StringComparison.Ordinal)) { s.Live = (int)Num(Tail(line, "SESSIONS_LIVE"), 0); continue; }
                    if (line.StartsWith("SESSIONS_SOURCE", StringComparison.Ordinal)) { s.Source = Tail(line, "SESSIONS_SOURCE").Trim(); continue; }
                    if (line.StartsWith("SESSIONS_ROOT", StringComparison.Ordinal)) { s.Root = Tail(line, "SESSIONS_ROOT").Trim(); continue; }
                    if (line.StartsWith("SESSIONS_TOTAL", StringComparison.Ordinal))
                    {
                        Dictionary<string, string> kv = Pairs(Tail(line, "SESSIONS_TOTAL"));
                        s.TotalIn = Num(Get(kv, "in"), 0);
                        s.TotalOut = Num(Get(kv, "out"), 0);
                        s.TotalCacheRead = Num(Get(kv, "cacheRead"), 0);
                        s.TotalHitPercent = Pct(Get(kv, "hit"));
                        s.TotalDecodeTps = Pct(Get(kv, "decode"));
                        continue;
                    }
                    if (line.StartsWith("SESSION ", StringComparison.Ordinal)) s.Rows.Add(ParseRow(line));
                }
                catch
                {
                    /* 单行解析失败不影响其它行 —— 界面绝不能因为一行脏数据而崩 */
                }
            }
            return s;
        }

        private static SessionRow ParseRow(string line)
        {
            string rest = line.Substring("SESSION ".Length).Trim();
            int sp = rest.IndexOf(' ');
            SessionRow r = new SessionRow();
            if (sp < 0) { r.Id = rest; return r; }
            r.Id = rest.Substring(0, sp);
            Dictionary<string, string> kv = Pairs(rest.Substring(sp + 1));
            r.Created = Clean(Get(kv, "created"));
            r.Last = Clean(Get(kv, "last"));
            r.Turns = Num(Get(kv, "turns"), 0);
            r.Steps = Num(Get(kv, "steps"), 0);
            r.In = Num(Get(kv, "in"), 0);
            r.Out = Num(Get(kv, "out"), 0);
            r.CacheRead = Num(Get(kv, "cacheRead"), 0);
            r.HitPercent = Pct(Get(kv, "hit"));
            r.DecodeTps = Pct(Get(kv, "decode"));
            r.TtftMs = Num(Get(kv, "ttft"), -1);
            r.CtxPercent = Pct(Get(kv, "ctx"));
            r.Blank = Get(kv, "blank") == "1";
            r.Live = Get(kv, "live") == "1";
            return r;
        }

        private static string Tail(string line, string marker)
        {
            return line.Length > marker.Length ? line.Substring(marker.Length) : "";
        }

        /// <summary>"unknown" → 空串（界面显示 unknown 由 Get 文本负责）。</summary>
        private static string Clean(string v) { return v == null || v == "unknown" ? "" : v; }

        private static Dictionary<string, string> Pairs(string text)
        {
            Dictionary<string, string> kv = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text)) return kv;
            string[] parts = text.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                if (p.Length == 0) continue;
                int eq = p.IndexOf('=');
                if (eq <= 0) continue;
                kv[p.Substring(0, eq)] = p.Substring(eq + 1);
            }
            return kv;
        }

        private static string Get(Dictionary<string, string> kv, string key)
        {
            string v;
            return kv.TryGetValue(key, out v) ? v : null;
        }

        private static long Num(string v, long fallback)
        {
            if (string.IsNullOrEmpty(v) || v == "unknown") return fallback;
            long n;
            return long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : fallback;
        }

        /// <summary>百分比/速率：`unknown` 或非法 → -1（未知），绝不当作 0。</summary>
        private static double Pct(string v)
        {
            if (string.IsNullOrEmpty(v) || v == "unknown") return -1;
            double d;
            return double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : -1;
        }
    }
}
