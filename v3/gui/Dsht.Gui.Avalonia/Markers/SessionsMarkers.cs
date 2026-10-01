using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Dsht.Gui.Avalonia.Markers
{
    /// <summary>一个会话行（来自 CLI `sessions` 的 `SESSION` 标记行）。缺失/未知的数值用 -1 表示，**不假装 0**。</summary>
    public sealed class SessionRow
    {
        public string Id = "";
        public string Title = "";          // dsh 自己生成的会话标题（可能为空）
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
        /// <summary>父会话 id（空 = 它是根会话 ✓）。来自 CLI 的 `SESSION_CHILD <父> <子>` ✓✓
        /// —— dsh 把子会话 id 记在父会话文件的 "childId" 字段里 ✓（2026-09-30 实测 98 组 ✓）</summary>
        public string ParentId = "";
        /// <summary>它的子会话 id 列表（子代理 ✓）。</summary>
        public List<string> ChildIds = new List<string>();
        public bool IsSubAgent { get { return ParentId.Length > 0; } }
        public bool Live;
        /// <summary>是否**知道**运行态（只有桥接插件快照才提供 live；磁盘投影没有这个事实）。</summary>
        public bool LiveKnown;

        /// <summary>相对最大输入量的条形长度（0–100，由 SessionsView.AttachBars 计算）</summary>
        public int TokenBar;

        public string ShortId { get { return Id.Length > 8 ? Id.Substring(0, 8) : Id; } }

        /// <summary>标题（空则给一个人话兜底，不留空白让人猜）。</summary>
        public string TitleText { get { return string.IsNullOrEmpty(Title) ? "（未命名会话）" : Title; } }

        /// <summary>状态语义：0=运行中 1=已结束 2=空会话（颜色由界面层决定）。</summary>
        public int StatusKind { get { return Live ? 0 : (Blank ? 2 : 1); } }

        public string LiveText { get { if (!LiveKnown) return Blank ? "空会话" : "运行态未知"; return Live ? "运行中" : (Blank ? "空会话" : "已结束"); } }

        public string HitText { get { return HitPercent < 0 ? "unknown" : HitPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%"; } }
        public string DecodeText { get { return DecodeTps < 0 ? "unknown" : DecodeTps.ToString("0.0", CultureInfo.InvariantCulture) + " tok/s"; } }
        public string CtxText { get { return CtxPercent < 0 ? "unknown" : CtxPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%"; } }

        /// <summary>首 token：超过 1 秒改用秒显示（dsh 投影里是多步累计的毫秒数，这里只换算单位，不改语义）。</summary>
        public string TtftText
        {
            get
            {
                if (TtftMs < 0) return "unknown";
                if (TtftMs < 1000) return TtftMs.ToString(CultureInfo.InvariantCulture) + " ms";
                return (TtftMs / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s";
            }
        }

        public string InText { get { return Human(In); } }
        public string OutText { get { return Human(Out); } }
        public string CacheReadText { get { return Human(CacheRead); } }
        public string TurnsText { get { return Turns + " 轮 / " + Steps + " 步"; } }

        public string LastShort { get { return ShortTime(Last); } }
        public string CreatedShort { get { return ShortTime(Created); } }

        /// <summary>缓存命中率等级：0=未知 1=低 2=中 3=高（越高越省钱）。</summary>
        public int HitLevel { get { return Level(HitPercent, 70, 90, true); } }

        /// <summary>上下文压力等级：0=未知 1=低 2=中 3=高（越低越安全）。</summary>
        public int CtxLevel { get { return Level(CtxPercent, 50, 80, false); } }

        public int HitBar { get { return Clamp(HitPercent); } }
        public int CtxBar { get { return Clamp(CtxPercent); } }

        /// <summary>人读数字：1.2K / 45.6M / 1.2B（固定 InvariantCulture）。</summary>
        public static string Human(long v)
        {
            if (v < 0) return "unknown";
            double d = v;
            if (v < 1000) return v.ToString(CultureInfo.InvariantCulture);
            if (v < 1000000) return (d / 1000).ToString("0.0", CultureInfo.InvariantCulture) + "K";
            if (v < 1000000000) return (d / 1000000).ToString("0.0", CultureInfo.InvariantCulture) + "M";
            return (d / 1000000000).ToString("0.0", CultureInfo.InvariantCulture) + "B";
        }

        private static int Clamp(double v)
        {
            if (v < 0) return 0;
            if (v > 100) return 100;
            return (int)Math.Round(v);
        }

        private static int Level(double v, double low, double high, bool higherIsBetter)
        {
            if (v < 0) return 0;
            if (higherIsBetter)
            {
                if (v >= high) return 3;
                if (v >= low) return 2;
                return 1;
            }
            if (v >= high) return 3;
            if (v >= low) return 2;
            return 1;
        }

        /// <summary>"2026-09-23T01:48:28Z" → "09-23 01:48"（人眼扫读；解析不了就原样返回）。</summary>
        private static string ShortTime(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "unknown";
            if (iso.Length >= 16 && iso[10] == 'T') return iso.Substring(5, 5) + " " + iso.Substring(11, 5);
            return iso;
        }
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
        /// <summary>子代理（有父会话的）数量 ✓；RootCount = 其余（根会话/普通会话 ✓）。</summary>
        public int SubAgentCount;
        public int RootCount;

        /// <summary>数据来源的中文说明（给界面用；插件缺失时如实说明少了什么）。</summary>
        public string SourceText
        {
            get
            {
                // ★★★ **真机 GUI 冒烟抓到的回归（截图里写着「数据来源：未知」）** ✓✓
                //   ✗ CLI 的合并修复引入了新值 **`snapshot+disk`**（快照 + 磁盘补齐 ✓）✗
                //     → 而这里只认 snapshot / disk / aggregate ✗ → **落到"未知"** ✗✗
                //     → 它恰好是**最常见的真实情况**（dsh 在跑 + 有历史会话 ✓）
                //   ✓ 现在：**如实说明两者都在用** ✓✓（并说清历史来自磁盘 ✓）
                if (Source == "snapshot+disk") return "数据来源：桥接插件快照（实时）+ 磁盘投影（历史）";
                if (Source == "snapshot") return "数据来源：桥接插件快照（含实时「运行中」标记）";
                if (Source == "disk") return "数据来源：磁盘投影（未装桥接插件 —— 因此没有「正在运行」这一项）";
                if (Source == "aggregate") return "数据来源：投影总表（每会话投影文件缺失时的兜底）";
                return "数据来源：未知（CLI 报告的是「" + (Source ?? "") + "」）";
            }
        }

        public bool IsLiveSource { get { return Source == "snapshot" || Source == "snapshot+disk"; } }
    }

    /// <summary>列表的过滤与排序（纯函数，供界面调用；也可单测）。</summary>
    public static class SessionsView
    {
        public const int FilterAll = 0;
        public const int FilterNonBlank = 1;
        public const int FilterLive = 2;

        public static List<SessionRow> Filter(List<SessionRow> rows, int mode)
        {
            List<SessionRow> r = new List<SessionRow>();
            if (rows == null) return r;
            for (int i = 0; i < rows.Count; i++)
            {
                SessionRow s = rows[i];
                if (s == null) continue;
                if (mode == FilterNonBlank && s.Blank) continue;
                if (mode == FilterLive && !s.Live) continue;
                r.Add(s);
            }
            return r;
        }

        /// <summary>排序：0=最后活动（新→旧） 1=输入 token（多→少） 2=缓存命中率（低→高，最该看的排前面） 3=上下文压力（高→低） 4=解码速度（快→慢）。</summary>
        public static List<SessionRow> Sort(List<SessionRow> rows, int mode)
        {
            List<SessionRow> r = rows == null ? new List<SessionRow>() : new List<SessionRow>(rows);
            r.Sort(delegate(SessionRow a, SessionRow b)
            {
                int c;
                switch (mode)
                {
                    case 1: c = b.In.CompareTo(a.In); break;
                    case 2: c = Rank(a.HitPercent).CompareTo(Rank(b.HitPercent)); break;
                    case 3: c = b.CtxPercent.CompareTo(a.CtxPercent); break;
                    case 4: c = RankDesc(b.DecodeTps).CompareTo(RankDesc(a.DecodeTps)); break;
                    default: c = string.CompareOrdinal(b.Last, a.Last); break;
                }
                if (c != 0) return c;
                return string.CompareOrdinal(a.Id, b.Id);
            });
            return r;
        }

        /// <summary>未知值（-1）排在最后。</summary>
        private static double Rank(double v) { return v < 0 ? double.MaxValue : v; }

        /// <summary>降序时未知值（-1）排在最后。</summary>
        private static double RankDesc(double v) { return v < 0 ? double.MinValue : v; }

        /// <summary>给每行算出相对最大输入量的条形长度（0–100），用于横向对比。</summary>
        public static void AttachBars(List<SessionRow> rows)
        {
            if (rows == null) return;
            long max = 0;
            for (int i = 0; i < rows.Count; i++) if (rows[i] != null && rows[i].In > max) max = rows[i].In;
            for (int i = 0; i < rows.Count; i++)
            {
                SessionRow s = rows[i];
                if (s == null) continue;
                s.TokenBar = max <= 0 ? 0 : (int)Math.Round(s.In * 100.0 / max);
                if (s.TokenBar < 2 && s.In > 0) s.TokenBar = 2;
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
            Dictionary<string, string> links = new Dictionary<string, string>();   // 子 id → 父 id ✓
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
                    // 父子链接 ✓：CLI 扫会话文件的 childId 得到 ✓（先收集 ✓ 循环后统一归类 ✓）
                    if (line.StartsWith("SESSION_CHILD ", StringComparison.Ordinal))
                    {
                        string[] pc = line.Substring("SESSION_CHILD ".Length).Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (pc.Length >= 2) links[pc[1]] = pc[0];   // 子 → 父 ✓
                        continue;
                    }
                }
                catch
                {
                    /* 单行解析失败不影响其它行 —— 界面绝不能因为一行脏数据而崩 */
                }
            }
            // 运行态只有桥接插件快照才知道：磁盘投影没有这个事实（unknown 不等于"已结束"）
            bool liveKnown = s.Source == "snapshot";
            for (int i = 0; i < s.Rows.Count; i++) s.Rows[i].LiveKnown = liveKnown;
            // 归类：子代理挂到父会话下 ✓（98/98 子 id 都有 SESSION 行 ✓ 所以都能归类 ✓）
            // ★★ 审查抓到：这里对**每个子代理**都线性扫一遍全部行去找父 ✗ → O(子 × 行) ✗
            //   而且 Parse 是在 **UI 线程**上跑的 ✓（await 之后回到 UI 上下文 ✓）→ 会话多就卡界面 ✓
            // ✓ 现在：**先建 id → 行 的字典** ✓✓ 父查找 O(1) ✓
            System.Collections.Generic.Dictionary<string, SessionRow> byId2 =
                new System.Collections.Generic.Dictionary<string, SessionRow>(StringComparer.Ordinal);
            for (int rk = 0; rk < s.Rows.Count; rk++)
            {
                SessionRow rr = s.Rows[rk];
                if (rr != null && rr.Id != null && !byId2.ContainsKey(rr.Id)) byId2[rr.Id] = rr;
            }
            for (int ri = 0; ri < s.Rows.Count; ri++)
            {
                string cid2 = s.Rows[ri].Id;
                string pid3;
                if (cid2 != null && links.TryGetValue(cid2, out pid3))
                {
                    s.Rows[ri].ParentId = pid3;
                    SessionRow parentRow;
                    if (byId2.TryGetValue(pid3, out parentRow) && parentRow != null) parentRow.ChildIds.Add(cid2);
                }
            }
            int subCount = 0;
            for (int ri = 0; ri < s.Rows.Count; ri++) if (s.Rows[ri].IsSubAgent) subCount++;
            s.SubAgentCount = subCount;
            s.RootCount = s.Rows.Count - subCount;
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
            r.Title = Decode(Clean(Get(kv, "title")));
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

        /// <summary>标记行自由文本解码（与 CLI 侧 MarkerText.Encode 对称；`-`/空 → 空串）。</summary>
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

        /// <summary>"unknown" → 空串（界面显示 unknown 由文本属性负责）。</summary>
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
