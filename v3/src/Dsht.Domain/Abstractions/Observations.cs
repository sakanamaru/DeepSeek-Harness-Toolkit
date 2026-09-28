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
    }

    /// <summary>进程观测：按端口找 PID、判定某 PID 的命令行是否属于 dsh。</summary>
    public interface IProcessQuery
    {
        int PidListeningOn(int port);
        bool IsDshCommandLine(int pid);

        /// <summary>进程启动时间（本地时间）；取不到返回 null（对应 v2.x 的 haveStart=false）。</summary>
        System.DateTime? StartTime(int pid);
    }
}