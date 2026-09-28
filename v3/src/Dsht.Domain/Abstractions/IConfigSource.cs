namespace Dsht.Domain.Abstractions
{
    /// <summary>配置文件读写（平台实现）。路径语义：状态目录/launcher.config。</summary>
    public interface IConfigSource
    {
        string ReadConfig();
        void WriteConfig(string text);
    }
}