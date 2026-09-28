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
    }
}