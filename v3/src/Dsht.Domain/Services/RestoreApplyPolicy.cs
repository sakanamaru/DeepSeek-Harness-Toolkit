using System;

namespace Dsht.Domain.Services
{
    /// <summary>restore --apply 的准入判定（纯函数，零 IO）。
    /// V3 独有的安全规则：**真实恢复只允许写入显式指定的隔离数据根**——
    ///   ① 必须显式给出 --apply（人类可问责的断言：我确认没有 dsh 正在使用这个数据根）；
    ///   ② 必须设置 $DSH_HOME（数据根来自环境变量，而不是自动探测出来的默认位置）；
    ///   ③ 生效数据根不得等于任何默认候选（&lt;home&gt;/.dsh、%APPDATA%\.dsh、%LOCALAPPDATA%\.dsh）。
    /// 于是 V3 的真实恢复**永远不可能**写进用户默认数据根；未给 --apply 时本判定不介入，
    /// 流程与 v2.x 完全一致（运行中拒绝 → 恢复前自动备份 → 恢复）。
    /// 判定结果是**原因码**而非文案：文案由 CLI 本地化（与 v2.x 的 T() 同语言选择规则），
    /// 这样领域层不需要知道语言，也不会把中文写进纯领域层。</summary>
    public static class RestoreApplyPolicy
    {
        /// <summary>未设置 $DSH_HOME（或数据根为空）：无法证明"隔离"，拒绝。</summary>
        public const string NeedsDshHome = "apply-needs-dsh-home";

        /// <summary>生效数据根等于某个默认候选：那正是最需要保护的位置，拒绝。</summary>
        public const string NotIsolated = "apply-not-isolated";

        /// <summary>返回 null 表示放行；否则返回原因码。</summary>
        public static string Judge(bool applyRequested, string envHome, string dataRoot, string[] defaultCandidates)
        {
            if (!applyRequested) return null;                 // 未请求 --apply：本判定不介入
            if (string.IsNullOrWhiteSpace(envHome)) return NeedsDshHome;
            if (string.IsNullOrWhiteSpace(dataRoot)) return NeedsDshHome;
            string root = PathUtil.TrimTrailingSep(dataRoot.Trim());
            if (root.Length == 0) return NeedsDshHome;
            if (defaultCandidates != null)
            {
                for (int i = 0; i < defaultCandidates.Length; i++)
                {
                    string c = defaultCandidates[i];
                    if (string.IsNullOrWhiteSpace(c)) continue;
                    if (string.Equals(root, PathUtil.TrimTrailingSep(c.Trim()), StringComparison.OrdinalIgnoreCase)) return NotIsolated;
                }
            }
            return null;
        }

        /// <summary>原因码 → 人类可读文案。</summary>
        public static string Message(string reason, bool zh)
        {
            if (reason == NeedsDshHome)
                return zh ? "--apply 需要先设置 $DSH_HOME（隔离数据根）：V3 的真实恢复不会写入默认数据根"
                          : "--apply requires $DSH_HOME (an isolated data root): V3 never restores into a default data root";
            if (reason == NotIsolated)
                return zh ? "--apply 被拒绝：生效数据根就是默认位置，真实恢复只允许写入隔离数据根"
                          : "--apply refused: the effective data root is a default location; real restore only writes to an isolated data root";
            return zh ? "restore --apply 被拒绝" : "restore --apply refused";
        }
    }
}
