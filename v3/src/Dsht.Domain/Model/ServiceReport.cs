namespace Dsht.Domain.Model
{
    /// <summary>一次服务探测的结果快照：形态 + 状态 + PID + 人类可读说明 + 依据。
    /// 依据（Basis）用于证据链：说明"这个结论是靠什么观测到的"。</summary>
    public sealed class ServiceReport
    {
        public AppKind Kind { get; private set; }
        public ServiceState State { get; private set; }
        public int Pid { get; private set; }
        public string Basis { get; private set; }

        public ServiceReport(AppKind kind, ServiceState state, int pid, string basis)
        {
            Kind = kind;
            State = state;
            Pid = pid;
            Basis = basis == null ? "" : basis;
        }

        public bool IsReady { get { return State == ServiceState.Ready; } }

        /// <summary>机器可读标记（与 v2.x 的 STATUS_* 契约对齐时使用）。</summary>
        public string StatusMarker
        {
            get
            {
                if (State == ServiceState.Ready) return "STATUS_UP";
                if (State == ServiceState.Listening) return "STATUS_STARTING";
                return "STATUS_DOWN";
            }
        }

        public override string ToString()
        {
            return Kind + "/" + State + (Pid > 0 ? " pid=" + Pid : "") + (Basis.Length > 0 ? " (" + Basis + ")" : "");
        }
    }
}