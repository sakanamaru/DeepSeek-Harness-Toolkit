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

        /// <summary>复现 v2.x 源侧遍历：顶层目录按 skipTopDir/topRules 过滤、顶层文件按 skipTopFile 过滤；
        /// 键为**相对 src 的路径**（含顶层目录名），值为文件字节数。applySkipRules 时套用 SkipRules。</summary>
        System.Collections.Generic.Dictionary<string, long> WalkSource(string src, string skipTopDir, string skipTopFile, bool topRules);

        /// <summary>复现 v2.x 目标侧遍历：只跳过 reparse point；键为相对 dst 的路径。</summary>
        System.Collections.Generic.Dictionary<string, long> WalkDestination(string dst);

        /// <summary>目录的直接子目录（只读；失败返回空数组）。</summary>
        string[] ListDirectories(string path);

        /// <summary>路径是否 reparse point（符号链接/junction）。</summary>
        bool IsReparse(string path);

        /// <summary>文件是否存在且可读（用于 .dshws 标记探测）。</summary>
        bool FileExists(string path);
    }
}