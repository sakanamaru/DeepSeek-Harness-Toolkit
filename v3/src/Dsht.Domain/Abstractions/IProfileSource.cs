using Dsht.Domain.Model;

namespace Dsht.Domain.Abstractions
{
    /// <summary>profile 文件来源（平台实现负责读盘；领域层只消费 ProfileFile 列表）。</summary>
    public interface IProfileSource
    {
        ProfileCollection CollectDirectory(string dirOverride, bool includeVendor, bool absoluteLabel);
        ProfileFile ReadSingle(string path, bool absoluteLabel);
    }
}