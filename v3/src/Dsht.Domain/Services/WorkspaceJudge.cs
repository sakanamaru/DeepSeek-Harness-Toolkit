using System;

namespace Dsht.Domain.Services
{
    /// <summary>自动探测到的工作区是否"合理"（纯函数，逐条对齐 v2.x 的 LooksLikeWorkspace）。
    /// **只用于自动探测结果的判定**；用户手输或 `ws=` 配置的路径不受此限制（v2.x 同语义）。
    /// 拒绝：盘根（C:\ D:\）、各盘根下的保留名字（只看第一段）、系统/用户级根目录及其子树。
    /// 注意：领域层不能调 Path.GetFullPath（System.IO 被守卫禁止）——调用方必须传入**已绝对化**的路径。</summary>
    public static class WorkspaceJudge
    {
        /// <summary>各盘根/UNC 根下的保留名字（只比较第一段，避免误伤深层同名目录）。</summary>
        private static readonly string[] ReservedTopNames = new string[]
        {
            "$recycle.bin", "system volume information", "perflogs", "inetpub",
            "recovery", "windows.old", "$windows.~bt", "$windows.~ws", "$winreagent", "users"
        };

        /// <summary>forbiddenRoots 由平台侧提供（用户主目录 / 其上一级 / Windows / ProgramData / Program Files ×2）。</summary>
        public static bool LooksLike(string absolutePath, string[] forbiddenRoots)
        {
            if (string.IsNullOrEmpty(absolutePath)) return false;
            string p = absolutePath.TrimEnd('\\', '/');
            if (p.Length == 0) return false;
            if (p.Length <= 3 && p.Length >= 2 && p[1] == ':') return false;        // 盘根：C:\ D:\
            string lower = p.ToLowerInvariant();
            string[] segs = lower.Split(new char[] { '\\', '/' });
            for (int i = 0; i < ReservedTopNames.Length; i++)
                if (segs.Length > 1 && segs[1] == ReservedTopNames[i]) return false;
            if (forbiddenRoots != null)
            {
                for (int i = 0; i < forbiddenRoots.Length; i++)
                {
                    string r = forbiddenRoots[i];
                    if (string.IsNullOrEmpty(r)) continue;
                    string rc = r.TrimEnd('\\', '/').ToLowerInvariant();
                    if (rc.Length == 0) continue;
                    if (lower == rc) return false;
                    if (lower.StartsWith(rc + "\\", StringComparison.Ordinal)) return false;
                    if (lower.StartsWith(rc + "/", StringComparison.Ordinal)) return false;
                }
            }
            return true;
        }
    }

    /// <summary>工作区解析（纯函数，逐条对齐 v2.x 的 WorkspaceRoot）：
    ///   · 配置了 `ws=` → 绝对化后**必须存在**，否则返回 null（**不回退自动探测**，避免误备份/误恢复）；
    ///   · 未配置 → 直接用平台自动探测的结果。
    /// 绝对化与存在性判定由调用方以委托注入（领域层不做 IO）。</summary>
    public static class WorkspaceResolver
    {
        public static string Resolve(string configuredWorkspace, string detectedRoot,
                                     Func<string, string> fullPath, Func<string, bool> directoryExists)
        {
            if (!string.IsNullOrEmpty(configuredWorkspace))
            {
                string c = null;
                try { c = (fullPath == null) ? configuredWorkspace : fullPath(configuredWorkspace); }
                catch { c = null; }
                if (c != null && directoryExists != null && directoryExists(c)) return c;
                return null;
            }
            return detectedRoot;
        }
    }
}
