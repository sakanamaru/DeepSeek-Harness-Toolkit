namespace Dsht.Domain.Abstractions
{
    /// <summary>会话投影来源（平台实现负责读盘，**只读**）。
    /// 路径语义（dsh 0.1.5-rc.2 实测）：`&lt;数据根&gt;/storages/session_projcache/sessions/*.json`（每会话一份、持续更新）
    /// 与 `&lt;数据根&gt;/storages/session_projcache.json`（总表）。解析与派生指标由领域层的 SessionStats 负责（纯函数）。</summary>
    public interface ISessionStatsSource
    {
        /// <summary>每会话投影目录（可能不存在）。</summary>
        string SessionsDir { get; }

        /// <summary>投影总表路径（可能不存在）。</summary>
        string AggregatePath { get; }

        /// <summary>插件快照路径（可选增强；由我们的桥接插件写，不存在则只用磁盘投影）。</summary>
        string SnapshotPath { get; }

        /// <summary>列出投影文件全路径（升序；失败返回空数组）。</summary>
        string[] ListSessionFiles();

        /// <summary>读文本；不存在或读不到 → null。</summary>
        string ReadText(string path);
    }
}
