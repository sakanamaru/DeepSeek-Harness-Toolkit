namespace Dsht.Domain.Model
{
    /// <summary>目录快照（纯数据）：名字 + 直接子条目名（文件或目录不区分）。
    /// 由基础设施层读盘生成，领域层只做判定 → 领域保持零 IO。
    /// 条目比较用 OrdinalIgnoreCase：与 v2.x 在 Windows 上的行为一致（Linux 上 dsh 数据目录为小写，无副作用）。</summary>
    public sealed class DirSnapshot
    {
        public string Name { get; private set; }
        public string[] Entries { get; private set; }

        public DirSnapshot(string name, string[] entries)
        {
            Name = name == null ? "" : name;
            Entries = entries == null ? new string[0] : entries;
        }

        public bool Has(string entryName)
        {
            if (string.IsNullOrEmpty(entryName)) return false;
            for (int i = 0; i < Entries.Length; i++)
            {
                if (string.Compare(Entries[i], entryName, System.StringComparison.OrdinalIgnoreCase) == 0) return true;
            }
            return false;
        }
    }
}