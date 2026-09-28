using Dsht.Domain.Model;

namespace Dsht.Domain.Abstractions
{
    /// <summary>一个"可被工具箱管理的 dsh 形态"（web / headless / acp / desktop）。
    /// 实现方必须只依据可观测事实作答；探测失败一律返回 Down + Basis 说明原因，
    /// 不得抛异常、不得猜测形态。</summary>
    public interface IServiceTarget
    {
        AppKind Kind { get; }

        /// <summary>本机是否装有这种形态（可执行文件/包存在等）。</summary>
        bool IsAvailable();

        /// <summary>三态探测。必须在有限时间内返回（实现方自行设超时）。</summary>
        ServiceReport Probe();

        /// <summary>该形态当前进程 PID；找不到返回 0。</summary>
        int FindPid();

        /// <summary>一句话描述（状态页/诊断报告用）。</summary>
        string Describe();
    }
}