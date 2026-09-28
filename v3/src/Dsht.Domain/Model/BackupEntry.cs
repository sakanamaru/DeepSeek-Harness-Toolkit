namespace Dsht.Domain.Model
{
    /// <summary>备份根下的一个候选目录（纯数据）：名字、路径、目录快照（供 BackupPackage 判定有效性）。</summary>
    public sealed class BackupEntry
    {
        public string Name { get; private set; }
        public string Path { get; private set; }
        public DirSnapshot Snapshot { get; private set; }

        public BackupEntry(string name, string path, DirSnapshot snapshot)
        {
            Name = name == null ? "" : name;
            Path = path == null ? "" : path;
            Snapshot = snapshot;
        }
    }
}