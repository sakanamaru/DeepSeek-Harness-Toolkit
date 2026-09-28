using System;

namespace Dsht.Domain.Services
{
    /// <summary>复制/遍历时的目录跳过规则（纯函数）。逐条对齐 v2.x 的 CopySkipDir：
    ///   node_modules（依赖可重装）、backup（防自嵌套）、dsh-data-*（防把备份包再备份进去）；
    ///   reparse point（符号链接/junction）由调用方判定后传入。</summary>
    public static class SkipRules
    {
        public static bool SkipDir(string name, bool isReparsePoint)
        {
            if (isReparsePoint) return true;
            if (string.IsNullOrEmpty(name)) return false;
            if (name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.Equals("backup", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith("dsh-data-", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}