namespace Dsht.Domain.Abstractions
{
    /// <summary>端口是否有监听（实现方负责超时与异常吞掉，失败一律返回 false）。</summary>
    public interface IPortProbe
    {
        bool IsOpen(int port, int timeoutMs);
    }

    /// <summary>HTTP 探测：2xx/3xx 视为就绪。</summary>
    public interface IHttpProbe
    {
        bool IsReady(string url, int timeoutMs);

        /// <summary>任意状态码（含 401/403/404）也算有应答（对齐 v2.x 的 HttpResponds）。</summary>
        bool Responds(string url, int timeoutMs);
    }

    /// <summary>进程观测：按端口找 PID、判定某 PID 的命令行是否属于 dsh。</summary>
    public interface IProcessQuery
    {
        int PidListeningOn(int port);
        bool IsDshCommandLine(int pid);

        /// <summary>进程启动时间（本地时间）；取不到返回 null（对应 v2.x 的 haveStart=false）。</summary>
        System.DateTime? StartTime(int pid);

        /// <summary>是否存在名为 <paramref name="name"/> 的进程（按名字精确匹配，不做模糊）。
        /// 用途：判断某个**已知名字**的客户端是否在跑（例如官方桌面端进程 "DeepSeek Harness"）。
        /// 语义保持中立：域层不知道也不关心调用者拿它判断什么。</summary>
        bool AnyProcessNamed(string name);

        /// <summary>按名字取 PID（精确匹配）；**没找到返回 0** ✓ 不猜 ✓。
        /// 用途：官方桌面端在跑时，概览要显示它的 PID/启动时间/已运行 ✓</summary>
        int PidOfNamed(string name);

        /// <summary>进程命令行原文（诊断报告用）；取不到返回空串。</summary>
        string CommandLine(int pid);
    }
}