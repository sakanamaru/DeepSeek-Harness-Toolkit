namespace Dsht.Domain.Abstractions
{
    /// <summary>工具链版本查询（平台实现负责调用外部命令）。空串/null 表示"未找到/不可用"。</summary>
    public interface IToolchainQuery
    {
        string NodeVersion();
        string NpmVersion();
        string WhichDsh();          // null = 未安装
        string DshVersion();
        string NpmRegistryConfig(); // npm config get registry（空 = 未配置，调用方回退官方源）

        /// <summary>npm view @deepseek-ai/dsh version（最新版本串；离线/失败返回空）。</summary>
        string NpmViewLatest();

        /// <summary>在**不需要 sudo** 的前提下引导一个 Node 运行时（Linux：官方 LTS 包解到 ~/.local/node；
        /// Windows：不支持，返回 -1）。返回 0 表示成功；调用方必须用 NodeVersion() 复检。</summary>
        int InstallNodeRuntime();

        /// <summary>全局安装（`npm install -g --registry &lt;reg&gt; &lt;pkg&gt;`），返回退出码（-1 = 未能执行）。
        /// 调用方必须用可观测事实复检（WhichDsh/DshVersion），不能只看退出码。</summary>
        /// <summary>npm view &lt;pkg&gt; versions 的原始输出（列出可用版本 ✓；离线/失败返回空）。</summary>
        string NpmViewVersions();

        int NpmInstallGlobal(string pkg, string registry);

        /// <summary>全局卸载 dsh，返回退出码（-1 = 未能执行）。</summary>
        int NpmUninstallGlobal();
    }
}