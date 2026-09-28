namespace Dsht.Domain.Model
{
    /// <summary>一次备份的结果（纯数据）：新备份目录的完整路径；被跳过的嵌套 dsh-data-* 目录数（供上层提示，不静默）。</summary>
    public sealed class BackupResult
    {
        public string Path;
        public int SkippedNested;

        public BackupResult(string path, int skippedNested)
        {
            Path = path;
            SkippedNested = skippedNested;
        }
    }
}