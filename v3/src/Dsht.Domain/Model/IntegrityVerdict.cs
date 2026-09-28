namespace Dsht.Domain.Model
{
    /// <summary>自身完整性判定三态（对应 v2.x 的 bool?）：
    ///   Match    = 与随包 hashes.txt 匹配（官方包）
    ///   Mismatch = 不匹配（疑似被篡改）→ **只有这一种会拦截高风险操作**
    ///   Unknown  = 旁无 manifest / manifest 不含自身 / 取哈希失败（开发或非官方布局，不阻断）</summary>
    public enum IntegrityVerdict
    {
        Unknown = 0,
        Match = 1,
        Mismatch = 2
    }
}