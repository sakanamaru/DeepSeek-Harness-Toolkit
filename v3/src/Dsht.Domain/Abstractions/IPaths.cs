namespace Dsht.Domain.Abstractions
{
    /// <summary>路径解析（平台实现负责）。v2.x 语义：
    ///   StateDir   = exe 所在目录（可写时），否则 %APPDATA%\DeepSeekHarnessLauncher
    ///   DataRoot   = 用户主目录/.dsh，其次 %APPDATA%/.dsh、%LOCALAPPDATA%/.dsh
    /// ⚠ **`BackupsRoot` 曾经也在这里，已删除（2026-10-01，架构审计 S3）** ✓
    ///   · 它**没有任何生产调用点** ✗ —— 所有命令读的是 `IBackupSource.BackupsRoot` ✓
    ///   · 但它长得像"备份根的唯一来源" ✗ → **我一度把安全修复做在了它上面** ✗✗
    ///     （真机审查发现：改的是死代码 ✓ 而真正在用的那份没改 ✓）
    ///   · 删掉它 = **让后来者不可能再踩同一个坑** ✓✓（编译期就拦住 ✓）
    ///   · 备份根的正确来源：**`IBackupSource.BackupsRoot`** ✓（它才带归一化 ✓）</summary>
    public interface IPaths
    {
        string StateDir { get; }
        string DataRoot { get; }

        /// <summary>自动探测到的工作区；无则 null（V3 尚未移植工作区自动探测，诚实返回 null）。</summary>
        string WorkspaceRoot { get; }
    }
}