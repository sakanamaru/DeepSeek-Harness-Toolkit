namespace Dsht.Domain.Abstractions
{
    /// <summary>日志来源（平台实现负责读盘）：返回日志全文；文件不存在或读不到返回 null。
    /// 摘要格式由领域层的 LogSummaryBuilder 负责（与 v2.x 的 LogSummary 同格式）。</summary>
    public interface ILogSource
    {
        /// <summary>日志全文（状态目录/logs/launcher.log）；不存在或读不到返回 null。</summary>
        string ReadLog();
    }
}
