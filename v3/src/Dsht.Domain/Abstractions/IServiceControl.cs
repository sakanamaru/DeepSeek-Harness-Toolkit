namespace Dsht.Domain.Abstractions
{
    /// <summary>启动/停止 dsh 的能力（平台实现）。
    /// **纪律**：这里只负责"把进程起起来/停下来"，**绝不判断是否成功** —— 调用方必须用可观测事实
    /// （端口监听 / HTTP 就绪）确认结果，不能因为"命令发出去了"就报成功。
    /// 这也是 V3 把 start/stop 从 Windows 专有的 v2.x 核心搬进来的第一步（跨平台的前提）。</summary>
    public interface IServiceControl
    {
        /// <summary>脱离父进程地启动一个命令。成功返回 true；**拿不到 PID 时 pid=0（不假装知道）**。</summary>
        bool StartDetached(string fileName, string arguments, string workingDirectory, out int pid, out string error);

        /// <summary>按 PID 结束进程（先温和后强制）。返回是否成功；进程本就不在时 error 说明原因。</summary>
        bool Stop(int pid, out string error);

        /// <summary>结束整棵进程树（Windows 上 dsh 常由 cmd 包装，只杀 PID 会留下子进程）。</summary>
        bool StopTree(int pid, out string error);
    }
}