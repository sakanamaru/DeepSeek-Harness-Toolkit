using System;
using Dsht.Domain.Model;

namespace Dsht.Domain.Services
{
    /// <summary>完整性判定与闸门策略（纯函数）。对齐 v2.x 的 SelfIntegrity + IntegrityGate：
    ///   · 期望哈希缺失（无 manifest / manifest 不含自身）→ Unknown
    ///   · 实际哈希缺失（取哈希失败）→ Unknown
    ///   · 相等（忽略大小写）→ Match；否则 → Mismatch
    ///   · **只有 Mismatch 拦截**高风险操作；Unknown 放行（开发/非官方布局不阻断）</summary>
    public static class IntegrityJudge
    {
        public static IntegrityVerdict Judge(string expectedHash, string actualHash)
        {
            if (string.IsNullOrEmpty(expectedHash)) return IntegrityVerdict.Unknown;
            if (string.IsNullOrEmpty(actualHash)) return IntegrityVerdict.Unknown;
            return string.Compare(expectedHash.Trim(), actualHash.Trim(), StringComparison.OrdinalIgnoreCase) == 0
                ? IntegrityVerdict.Match
                : IntegrityVerdict.Mismatch;
        }

        /// <summary>是否应拦截高风险操作（卸载清数据 / 恢复 / 更新 dsh）。</summary>
        public static bool ShouldBlock(IntegrityVerdict v)
        {
            return v == IntegrityVerdict.Mismatch;
        }
    }
}