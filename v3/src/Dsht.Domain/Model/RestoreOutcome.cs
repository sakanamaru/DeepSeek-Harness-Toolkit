namespace Dsht.Domain.Model
{
    /// <summary>真实恢复的结果（平台实现填事实，CLI 只负责呈现与本地化）。
    /// 计数项用于让 CLI 打印与 v2.x 同义的提示（跳过无效工作区 / 无法识别的工作区条目）。</summary>
    public sealed class RestoreOutcome
    {
        /// <summary>是否整体成功（任一复制失败即 false，并填 Error）。</summary>
        public bool Ok;

        /// <summary>失败原因（异常消息原文，未本地化）。</summary>
        public string Error;

        /// <summary>恢复的顶层目录数（不含 _workspace）。</summary>
        public int TopDirs;

        /// <summary>恢复的顶层文件数。</summary>
        public int TopFiles;

        /// <summary>成功恢复的工作区数。</summary>
        public int WorkspacesRestored;

        /// <summary>因目标目录无效而跳过的工作区数（v2.x 的"目标目录无效，已跳过该工作区"）。</summary>
        public int WorkspacesSkipped;

        /// <summary>因缺少 .dshws 标记而无法识别的工作区条目数。</summary>
        public int WorkspacesUnrecognized;
    }
}
