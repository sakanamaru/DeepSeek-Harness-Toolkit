using System;

namespace Dsht.Domain.Services
{
    /// <summary>DeepSeek 平台余额接口的**纯解析结果**（IO 在 CLI 层 ✓ 领域层零 IO ✓ 门槛可查 ✓）。
    /// 缺字段一律空串 ✗ **不假装 0** ✓（与 SessionStat 的 Has* 纪律一致 ✓）。</summary>
    public sealed class BalanceInfo
    {
        /// <summary>响应形状认出来了 ✓（false = 不认 → CLI 如实报 BALANCE_UNAVAILABLE ✗ 绝不冒充数字 ✓✓）。</summary>
        public bool Parsed;
        /// <summary>平台的 is_available 断言（false = 账户当前不可用，可能欠费/key 被停 ✓ 只转述 ✓）。</summary>
        public bool IsAvailable;
        public string Currency = "";   // "CNY" …（空 = 平台没给 ✓）
        public string Total = "";      // total_balance（总计）
        public string Topup = "";      // topped_up_balance（充值余额 ✓ 用户要求显示这个 ✓）
        public string Granted = "";    // granted_balance（活动赠送余额 ✓ 用户要求显示这个 ✓）
    }

    /// <summary>解析 `https://api.deepseek.com/user/balance` 的响应体（**纯函数** ✓ 可单测 ✓ 零 IO ✓）。
    /// 形状：`{ "is_available": bool, "balance_infos": [ { "currency", "total_balance",
    /// "granted_balance", "topped_up_balance" } ] }`。
    /// · 不认 → Parsed=false ✓（key 无效 / 平台改格式 都走这里 ✓ 上层如实报 ✗ 不猜 ✗✓）
    /// · balance_infos 是数组 → **只取第一个条目** ✗ 不做跨币种加总 ✗（加总是猜测 ✓；
    ///   平台当前只回一条 CNY ✓ 取第一条是"有依据的简化" ✓✓）
    /// · is_available 没给 → 当作可用 ✓（不因缺字段就断言人家账户停用 ✗）。</summary>
    public static class BalanceJson
    {
        public static BalanceInfo Parse(string json)
        {
            BalanceInfo b = new BalanceInfo();
            if (string.IsNullOrEmpty(json)) return b;
            try
            {
                JNode root = JsonLite.Parse(json);
                if (root == null || !root.IsObject) return b;
                JNode infos = root.Get("balance_infos");
                if (infos == null || !infos.IsArray || infos.Items == null || infos.Items.Count == 0) return b;
                JNode first = infos.Items[0];
                if (first == null || !first.IsObject) return b;
                b.Parsed = true;
                JNode avail = root.Get("is_available");
                b.IsAvailable = avail == null || avail.AsBool(true);
                b.Currency = Str(first, "currency");
                b.Total = Str(first, "total_balance");
                b.Topup = Str(first, "topped_up_balance");
                b.Granted = Str(first, "granted_balance");
                return b;
            }
            catch
            {
                return new BalanceInfo();   // 任何解析异常 → 不认 ✓ 绝不抛 ✓ 绝不半截数据 ✓
            }
        }

        private static string Str(JNode n, string name)
        {
            JNode v = n.Get(name);
            return v == null ? "" : v.AsString("");
        }
    }
}
