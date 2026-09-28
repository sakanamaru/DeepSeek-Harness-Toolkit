namespace Dsht.Domain.Abstractions
{
    /// <summary>文件系统查询（只读；平台实现负责读盘）。</summary>
    public interface IFileSystemQuery
    {
        bool DirectoryExists(string path);

        /// <summary>能否枚举该目录（权限检查，对应 v2.x 的 Directory.GetFiles 探测）。</summary>
        bool CanEnumerate(string path);

        /// <summary>目录总字节数（递归累加文件长度，跳过读不到的项）。</summary>
        long DirSize(string path);
    }
}