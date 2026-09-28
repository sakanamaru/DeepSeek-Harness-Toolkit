namespace Dsht.Domain.Model
{
    /// <summary>一个会话的投影统计。**全部来自 dsh 自己的投影（只读）**：只有计数、时间与元数据，
    /// **不含任何对话正文**（正文在 zstd 压缩的 `session.jsonl.zstd` 里，本工具不读）。
    /// 字段存在性用 Has* 标记：缺字段时**不假装 0**（0 与"没有这个数据"是两件事）。</summary>
    public sealed class SessionStat
    {
        public string Id = "";              // 会话 id（来自文件名或投影总表的键）
        public string Title = "";           // 投影里的标题（可能为空）
        public string Cwd = "";             // identity.cwd
        public string CreatedAt = "";       // identity.createdAt（数字 → UTC ISO-8601；空 = 没有该字段）
        public long CreatedAtEpochMs;       // 原值（epoch 毫秒；0 = 未知）
        public string LastPromptAt = "";    // sessionListMetadata.lastPromptAt（同上）
        public long LastPromptEpochMs;      // 原值（epoch 毫秒；0 = 未知）
        public bool Blank;                  // 空会话（dsh 自己标的）

        /// <summary>是否**正在运行**（来自桥接插件的快照：dsh `listSessions()` 的 `live` 标记）。
        /// 磁盘投影拿不到这个事实，所以插件缺失时恒为 false —— 这是"面板少了实时部分"的诚实体现。</summary>
        public bool Live;

        public long Turns, Steps;
        public long LlmMs, ToolMs, TtftMs, DecodeMs, DecodeTokens, TtftSteps;
        public long UncachedInputTokens, OutputTokens, CacheReadTokens, CacheWriteTokens;
        public long SurfaceTokens, ContextWindow, PressureTokens;
        public long SystemTokens, ToolsTokens, MessageTokens;

        public bool HasStats;      // sessionStats 存在
        public bool HasTokens;     // tokenUsage.totals 存在
        public bool HasPressure;   // contextPressure 存在
        public bool HasBreakdown;  // contextBreakdown 存在

        /// <summary>输入侧总 token（未命中缓存 + 命中缓存）。</summary>
        public long TotalInputTokens { get { return UncachedInputTokens + CacheReadTokens; } }
    }

    /// <summary>多会话汇总（合计 + 加权速度/命中率）。</summary>
    public sealed class SessionTotals
    {
        public int Count;                  // 参与汇总的会话数
        public int NonBlankCount;          // 其中非空会话数
        public long UncachedInputTokens, OutputTokens, CacheReadTokens, CacheWriteTokens;
        public long DecodeMs, DecodeTokens;

        /// <summary>缓存命中率（%）；分母为 0 → -1 表示**未知**（不假装 0%）。</summary>
        public double CacheHitPercent = -1;

        /// <summary>解码速度（tokens/s）；decodeMs 为 0 → -1 表示未知。</summary>
        public double DecodeTokensPerSec = -1;
    }
}
