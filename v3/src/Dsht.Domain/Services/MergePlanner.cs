using System.Collections.Generic;

namespace Dsht.Domain.Services
{
    /// <summary>合并式恢复计划（纯函数）。逐条对齐 v2.x 的 PlanMergeCore 计数部分：
    ///   源侧每条 → 目标侧已有则 overwrite++，否则 new++，并累加源侧字节；
    ///   目标侧多出来的 → keep++（**合并语义：目标端独有的文件不会被删除**）。
    /// 返回 [新增, 覆盖, 保留, 待复制字节]。</summary>
    public static class MergePlanner
    {
        public static long[] Plan(Dictionary<string, long> srcMap, Dictionary<string, long> dstMap)
        {
            long nw = 0, ow = 0, keep = 0, bytes = 0;
            if (srcMap != null)
            {
                foreach (KeyValuePair<string, long> kv in srcMap)
                {
                    if (dstMap != null && dstMap.ContainsKey(kv.Key)) ow++;
                    else nw++;
                    bytes += kv.Value;
                }
            }
            if (dstMap != null)
            {
                foreach (KeyValuePair<string, long> kv in dstMap)
                {
                    if (srcMap == null || !srcMap.ContainsKey(kv.Key)) keep++;
                }
            }
            return new long[] { nw, ow, keep, bytes };
        }
    }
}