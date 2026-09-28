namespace Dsht.Domain.Model
{
    /// <summary>一次备份的结果（纯数据）：新备份目录的完整路径；被跳过的嵌套 dsh-data-* 目录数（供上层提示，不静默）。</summary>
    public sealed class BackupResult
    {
        public string Path;
        public int SkippedNested;
        /// <summary>**读不到/复制失败**的文件或目录数 ✓。&gt; 0 时上层必须如实告知用户"备份不完整" ✗（此前这些失败被静默吞掉 ✗）。</summary>
        public int FailedCopies;

        public BackupResult(string path, int skippedNested)
        {
            Path = path;
            SkippedNested = skippedNested;
        }

        public BackupResult(string path, int skippedNested, int failedCopies)
        {
            Path = path;
            SkippedNested = skippedNested;
            FailedCopies = failedCopies;
        }
    }
}
