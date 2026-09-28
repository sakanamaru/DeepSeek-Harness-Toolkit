namespace Dsht.Domain.Services
{
    /// <summary>npm 返回值的校验与清洗（**纯函数**）。
    /// 为什么需要：`npm view` 的输出来自网络，直接拼进命令行就是命令注入面 ——
    /// v2.x 为此有严格白名单，V3 保持同样的纪律。
    /// 规则：先去空白/换行；只允许 `[0-9A-Za-z.\-+_]`；长度 1..64；必须含至少一个数字；不得以 `-` 开头。</summary>
    public static class NpmVersionGuard
    {
        public static string Normalize(string raw)
        {
            if (raw == null) return "";
            return raw.Trim().Trim('\'', '"');
        }

        public static bool IsSafe(string version)
        {
            string v = Normalize(version);
            if (v.Length == 0 || v.Length > 64) return false;
            if (v[0] == '-') return false;
            bool hasDigit = false;
            for (int i = 0; i < v.Length; i++)
            {
                char c = v[i];
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '.' || c == '-' || c == '+' || c == '_';
                if (!ok) return false;
                if (c >= '0' && c <= '9') hasDigit = true;
            }
            return hasDigit;
        }
    }
}