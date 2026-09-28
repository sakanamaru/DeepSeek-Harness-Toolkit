namespace Dsht.Domain.Abstractions
{
    /// <summary>路径解析（平台实现负责）。v2.x 语义：
    ///   StateDir   = exe 所在目录（可写时），否则 %APPDATA%\DeepSeekHarnessLauncher
    ///   BackupsRoot= StateDir/backup
    ///   DataRoot   = 用户主目录/.dsh，其次 %APPDATA%/.dsh、%LOCALAPPDATA%/.dsh</summary>
    public interface IPaths
    {
        string StateDir { get; }
        string BackupsRoot { get; }
        string DataRoot { get; }
    }
}