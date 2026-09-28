using System;
using Dsht.Domain.Model;

namespace Dsht.Domain.Services
{
    /// <summary>三信号服务判定（纯函数）。
    /// 真值表逐条对齐 v2.x 的 JudgeState3：
    ///   端口未开 → Down；HTTP 就绪 → Ready；
    ///   端口开但 HTTP 未就绪 → 再看监听进程身份：是 dsh → Ready，否则 Listening；
    ///   监听进程判定抛异常 → 按 false 处理（v2.x 的 try/catch 语义）。</summary>
    public static class ServiceJudge
    {
        public static ServiceState Judge(bool portOpen, bool httpOk, Func<bool> listenerIsDsh)
        {
            if (!portOpen) return ServiceState.Down;
            if (httpOk) return ServiceState.Ready;
            bool byProc = false;
            try { byProc = listenerIsDsh != null && listenerIsDsh(); }
            catch { byProc = false; }
            return byProc ? ServiceState.Ready : ServiceState.Listening;
        }

        /// <summary>两信号简化版（与 v2.x 的 JudgeState 对齐）。</summary>
        public static ServiceState Judge(bool portOpen, bool httpOk)
        {
            if (!portOpen) return ServiceState.Down;
            return httpOk ? ServiceState.Ready : ServiceState.Listening;
        }
    }
}