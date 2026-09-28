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

        /// <summary>读某个 profile 的 cordis.patch.yml 文本（用于找出 `disabled: true` 的条目）；读不到 → null。</summary>
        string ReadPatch(string profileName);

        /// <summary>读某个 profile 里某个组合包自己的 package.json（取版本号）；读不到 → null。</summary>
        string ReadBundleManifest(string profileName, string bundleId);

        /// <summary>把新的补丁文本写回该 profile（**平台实现必须：先备份 → 再写 → 复检 → 不一致就回滚**）。
        /// 返回值：是否成功写且复检通过；backupPath 为备份文件路径（失败也要尽量给出）；error 为原因键或异常信息。</summary>
        bool ApplyPatch(string profileName, string newText, out string backupPath, out string error);
    }
}
