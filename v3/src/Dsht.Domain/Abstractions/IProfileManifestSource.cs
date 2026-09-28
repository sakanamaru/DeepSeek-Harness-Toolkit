namespace Dsht.Domain.Abstractions
{
    /// <summary>profile 清单来源（平台实现负责读盘）：只读 `&lt;数据根&gt;/profiles/&lt;name&gt;/package.json`。
    /// 解析与形态推断由领域层的 ProfileManifest 负责（纯函数）。</summary>
    public interface IProfileManifestSource
    {
        /// <summary>`&lt;数据根&gt;/profiles`（可能不存在）。</summary>
        string ProfilesRoot { get; }

        /// <summary>列出 profiles 目录下的子目录名（升序；可能含 node_modules 等非 profile 目录，由调用方过滤）。</summary>
        string[] ListProfiles();

        /// <summary>读某个 profile 的 package.json 文本；不存在或读不到 → null。</summary>
        string ReadManifest(string profileName);
    }
}
