namespace Dsht.Domain.Model
{
    /// <summary>备份类型（由目录名后缀判定）。
    /// 手动备份永久保留；其余四种 Pre* 属"保护性备份"（严格模式，任一文件复制失败即整体失败）。</summary>
    public enum BackupKind
    {
        Manual = 0,
        Auto = 1,
        PreRestore = 2,
        PreImport = 3,
        PreWipe = 4,
        PreUpdate = 5
    }
}