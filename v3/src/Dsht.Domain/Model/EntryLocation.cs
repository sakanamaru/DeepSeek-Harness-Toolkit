namespace Dsht.Domain.Model
{
    /// <summary>条目定位结果：命中的文件 + 行号（1-based；0=未找到）。</summary>
    public sealed class EntryLocation
    {
        public string File = "";
        public int Line;

        public EntryLocation() { }
        public EntryLocation(string file, int line) { File = file == null ? "" : file; Line = line; }
    }
}