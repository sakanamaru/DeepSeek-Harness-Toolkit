using Dsht.Domain.Model;

namespace Dsht.Domain.Abstractions
{
    /// <summary>自身完整性数据源（平台实现负责取自身哈希与随包清单）。判定由领域层的 IntegrityJudge 负责。</summary>
    public interface IIntegritySource
    {
        /// <summary>自身哈希（小写 hex）；取不到返回 null。</summary>
        string SelfHash();

        /// <summary>随包 hashes.txt 的文本内容；不存在返回 null。</summary>
        string ReadManifest();

        /// <summary>自身文件名（在清单里按此名查找）。</summary>
        string SelfFileName();
    }
}