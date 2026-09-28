using System;
using System.Collections.Generic;
using Dsht.Domain.Model;

namespace Dsht.Domain.Services
{
    /// <summary>会话投影解析与派生指标（纯函数）。
    /// 数据来源（dsh 0.1.5-rc.2 本机实测，明文 JSON、只读、持续更新）：
    ///   · 单会话：`&lt;数据根&gt;/storages/session_projcache/sessions/&lt;id&gt;.json` = `{version, record:{identity, rows:[…]}}`
    ///   · 投影总表：`&lt;数据根&gt;/storages/session_projcache.json` = `{unit, global, tables:{sessions:{&lt;key&gt;:{identity, rows:[…]}}}}`
    ///   · 插件快照（可选，由我们的桥接插件写）：见 <see cref="SnapshotFormatVersion"/> 的说明
    /// **诚实边界**：字段缺失 → Has*=false、派生指标返回 -1（未知），**绝不假装 0**；
    /// 格式/版本不认 → 返回 null / 空数组，由调用方诚实降级。
    /// **隐私边界**：只读计数、时间与元数据（id/标题/cwd）；对话正文在 zstd 压缩的 `session.jsonl.zstd` 里，本工具不读。</summary>
    public static class SessionStats
    {
        /// <summary>插件快照格式版本（我们的桥接插件与 CLI 之间的约定）。</summary>
        public const int SnapshotFormatVersion = 2;

        /// <summary>仍接受的最小版本（v1 无 live 字段）。</summary>
        public const int SnapshotFormatVersionMin = 1;

        /// <summary>未知值（派生指标的分母为 0 时返回它，而不是 0）。</summary>
        public const double Unknown = -1;

        // ---------------- 解析 ----------------

        /// <summary>解析单个会话投影文件 → SessionStat；格式不认 → null。</summary>
        public static SessionStat ParseSessionProjection(string json, string id)
        {
            JNode root = JsonLite.Parse(json);
            if (root == null || !root.IsObject) return null;
            JNode record = root.Get("record");
            if (record == null || !record.IsObject) return null;
            JNode row = RowOf(record.Get("rows"));
            if (row == null) return null;
            SessionStat s = FromRow(row, id);
            if (s == null) return null;
            FillIdentity(s, record.Get("identity"));
            return s;
        }

        /// <summary>解析投影总表 → 全部会话（表键即 id，去掉 `session-` 前缀）。格式不认 → 空数组。</summary>
        public static SessionStat[] ParseAggregate(string json)
        {
            List<SessionStat> list = new List<SessionStat>();
            JNode root = JsonLite.Parse(json);
            if (root == null) return list.ToArray();
            JNode sessions = root.Path("tables", "sessions");
            if (sessions == null || !sessions.IsObject || sessions.Members == null) return list.ToArray();
            foreach (KeyValuePair<string, JNode> kv in sessions.Members)
            {
                JNode entry = kv.Value;
                if (entry == null || !entry.IsObject) continue;
                JNode row = RowOf(entry.Get("rows"));
                if (row == null) continue;
                string id = kv.Key == null ? "" : kv.Key;
                if (id.StartsWith("session-", StringComparison.Ordinal)) id = id.Substring("session-".Length);
                SessionStat s = FromRow(row, id);
                if (s == null) continue;
                FillIdentity(s, entry.Get("identity"));
                list.Add(s);
            }
            return list.ToArray();
        }

        /// <summary>解析插件快照（格式 v1，见 <see cref="SnapshotFormatVersion"/>）；版本不认/格式不认 → 空数组。
        /// 快照形状：`{ "formatVersion":1, "generatedAt":"…", "sessions":[ { "id":"…", "title":"…", "turns":1, "steps":2,
        /// "uncachedInputTokens":3, "outputTokens":4, "cacheReadTokens":5, "cacheWriteTokens":6, "decodeMs":7,
        /// "decodeTokens":8, "ttftMs":9, "contextWindow":10, "pressureTokens":11, "lastPromptAt":"…", "blank":false } ] }`</summary>
        public static SessionStat[] ParseSnapshot(string json)
        {
            List<SessionStat> list = new List<SessionStat>();
            JNode root = JsonLite.Parse(json);
            if (root == null || !root.IsObject) return list.ToArray();
            JNode ver = root.Get("formatVersion");
            long v = ver == null ? -1 : (long)ver.AsNumber(-1);
            if (v != SnapshotFormatVersionMin && v != SnapshotFormatVersion) return list.ToArray();   // v1（无 live）与 v2（含 live）都认
            JNode arr = root.Get("sessions");
            if (arr == null || !arr.IsArray) return list.ToArray();
            for (int i = 0; i < arr.Items.Count; i++)
            {
                JNode n = arr.Items[i];
                if (n == null || !n.IsObject) continue;
                SessionStat s = new SessionStat();
                s.Id = Str(n, "id");
                s.Title = Str(n, "title");
                s.Cwd = Str(n, "cwd");
                s.CreatedAt = Str(n, "createdAt");
                s.LastPromptAt = Str(n, "lastPromptAt");
                s.Blank = n.Get("blank") != null && n.Get("blank").AsBool(false);
                s.Live = n.Get("live") != null && n.Get("live").AsBool(false);
                s.Turns = Num(n, "turns");
                s.Steps = Num(n, "steps");
                s.LlmMs = Num(n, "llmMs");
                s.ToolMs = Num(n, "toolMs");
                s.TtftMs = Num(n, "ttftMs");
                s.DecodeMs = Num(n, "decodeMs");
                s.DecodeTokens = Num(n, "decodeTokens");
                s.UncachedInputTokens = Num(n, "uncachedInputTokens");
                s.OutputTokens = Num(n, "outputTokens");
                s.CacheReadTokens = Num(n, "cacheReadTokens");
                s.CacheWriteTokens = Num(n, "cacheWriteTokens");
                s.ContextWindow = Num(n, "contextWindow");
                s.PressureTokens = Num(n, "pressureTokens");
                s.SurfaceTokens = Num(n, "surfaceTokens");
                s.HasStats = n.Get("turns") != null;
                s.HasTokens = n.Get("uncachedInputTokens") != null || n.Get("outputTokens") != null;
                s.HasPressure = n.Get("contextWindow") != null;
                list.Add(s);
            }
            return list.ToArray();
        }

        // ---------------- 派生指标（纯函数） ----------------

        /// <summary>缓存命中率（%）= cacheRead / (cacheRead + uncachedInput)；分母 0 → Unknown。</summary>
        public static double CacheHitPercent(SessionStat s)
        {
            if (s == null) return Unknown;
            long denom = s.CacheReadTokens + s.UncachedInputTokens;
            if (denom <= 0) return Unknown;
            return s.CacheReadTokens * 100.0 / denom;
        }

        /// <summary>解码速度（tokens/s）= decodeTokens / decodeMs × 1000；decodeMs 0 → Unknown。</summary>
        public static double DecodeTokensPerSec(SessionStat s)
        {
            if (s == null || s.DecodeMs <= 0) return Unknown;
            return s.DecodeTokens * 1000.0 / s.DecodeMs;
        }

        /// <summary>上下文压力（%）= pressureTokens / contextWindow × 100；窗口 0 → Unknown。</summary>
        public static double ContextPressurePercent(SessionStat s)
        {
            if (s == null || s.ContextWindow <= 0) return Unknown;
            return s.PressureTokens * 100.0 / s.ContextWindow;
        }

        /// <summary>多会话汇总：合计 token/解码量，并按合计值算加权命中率与速度（避免对百分比求平均）。</summary>
        public static SessionTotals Aggregate(IList<SessionStat> list)
        {
            SessionTotals t = new SessionTotals();
            if (list == null) return t;
            for (int i = 0; i < list.Count; i++)
            {
                SessionStat s = list[i];
                if (s == null) continue;
                t.Count++;
                if (!s.Blank) t.NonBlankCount++;
                t.UncachedInputTokens += s.UncachedInputTokens;
                t.OutputTokens += s.OutputTokens;
                t.CacheReadTokens += s.CacheReadTokens;
                t.CacheWriteTokens += s.CacheWriteTokens;
                t.DecodeMs += s.DecodeMs;
                t.DecodeTokens += s.DecodeTokens;
            }
            long denom = t.CacheReadTokens + t.UncachedInputTokens;
            t.CacheHitPercent = denom <= 0 ? Unknown : t.CacheReadTokens * 100.0 / denom;
            t.DecodeTokensPerSec = t.DecodeMs <= 0 ? Unknown : t.DecodeTokens * 1000.0 / t.DecodeMs;
            return t;
        }

        // ---------------- 内部 ----------------

        /// <summary>取投影行：dsh 的 `rows` 是**对象**（投影名 → `{ver,seq,val}`，本机实测确认），
        /// 同时兼容数组形态（防御 dsh 版本差异）。</summary>
        private static JNode RowOf(JNode rows)
        {
            if (rows == null) return null;
            if (rows.IsObject) return rows;
            if (rows.IsArray && rows.Items.Count > 0) return rows.Items[0];
            return null;
        }

        private static SessionStat FromRow(JNode row, string id)
        {
            if (row == null || !row.IsObject) return null;
            SessionStat s = new SessionStat();
            s.Id = id == null ? "" : id;

            JNode st = row.Path("sessionStats", "val");
            if (st != null && st.IsObject)
            {
                s.HasStats = true;
                s.Turns = Num(st, "turns");
                s.Steps = Num(st, "steps");
                s.LlmMs = Num(st, "llmMs");
                s.ToolMs = Num(st, "toolMs");
                s.TtftMs = Num(st, "ttftMs");
                s.TtftSteps = Num(st, "ttftSteps");
                s.DecodeMs = Num(st, "decodeMs");
                s.DecodeTokens = Num(st, "decodeTokens");
            }
            JNode tu = row.Path("tokenUsage", "val", "totals");
            if (tu != null && tu.IsObject)
            {
                s.HasTokens = true;
                s.UncachedInputTokens = Num(tu, "uncachedInputTokens");
                s.OutputTokens = Num(tu, "outputTokens");
                s.CacheReadTokens = Num(tu, "cacheReadTokens");
                s.CacheWriteTokens = Num(tu, "cacheWriteTokens");
            }
            JNode cp = row.Path("contextPressure", "val");
            if (cp != null && cp.IsObject)
            {
                s.HasPressure = true;
                s.SurfaceTokens = Num(cp, "surfaceTokens");
                s.ContextWindow = Num(cp, "contextWindow");
                s.PressureTokens = Num(cp, "pressureTokens");
            }
            JNode cb = row.Path("contextBreakdown", "val");
            if (cb != null && cb.IsObject)
            {
                s.HasBreakdown = true;
                s.SystemTokens = Num(cb, "systemTokens");
                s.ToolsTokens = Num(cb, "toolsTokens");
                s.MessageTokens = Num(cb, "messageTokens");
            }
            JNode lm = row.Path("sessionListMetadata", "val");
            if (lm != null && lm.IsObject)
            {
                s.Blank = lm.Get("blank") != null && lm.Get("blank").AsBool(false);
                s.LastPromptEpochMs = Num(lm, "lastPromptAt");
                s.LastPromptAt = s.LastPromptEpochMs > 0 ? EpochMsToIso(s.LastPromptEpochMs) : Str(lm, "lastPromptAt");
            }
            JNode title = row.Path("title", "val");
            if (title != null && title.IsString) s.Title = title.StringValue;
            return s;
        }

        private static void FillIdentity(SessionStat s, JNode identity)
        {
            if (s == null || identity == null || !identity.IsObject) return;
            s.Cwd = Str(identity, "cwd");
            s.CreatedAtEpochMs = Num(identity, "createdAt");
            s.CreatedAt = s.CreatedAtEpochMs > 0 ? EpochMsToIso(s.CreatedAtEpochMs) : Str(identity, "createdAt");
        }

        /// <summary>epoch 毫秒 → UTC ISO-8601（纯函数：固定纪元，**不读时钟、不带本机时区**）。
        /// dsh 投影里的 `createdAt` / `lastPromptAt` 是 Int64 epoch 毫秒（本机实测）。</summary>
        public static string EpochMsToIso(long ms)
        {
            if (ms <= 0) return "";
            try
            {
                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ms)
                    .ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { return ""; }
        }

        private static long Num(JNode obj, string name)
        {
            JNode n = obj == null ? null : obj.Get(name);
            if (n == null) return 0;
            if (n.NodeKind == JNode.Kind.Number) return (long)n.NumberValue;
            if (n.NodeKind == JNode.Kind.String)
            {
                double d;
                if (double.TryParse(n.StringValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) return (long)d;
            }
            return 0;
        }

        private static string Str(JNode obj, string name)
        {
            JNode n = obj == null ? null : obj.Get(name);
            return (n != null && n.IsString) ? n.StringValue : "";
        }
    }
}
