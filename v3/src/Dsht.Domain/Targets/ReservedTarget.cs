using Dsht.Domain.Abstractions;
using Dsht.Domain.Model;

namespace Dsht.Domain.Targets
{
    /// <summary>预留形态（headless / acp / desktop）：**形态已被承认，但当前没有可观测事实可据以判定**。
    /// 它的存在就是为了让"未识别"成为一等公民：报 Down + 说明原因，绝不假装 Ready。
    /// 待 dsh 桌面端/ACP 的真实形态可观测（进程名、IPC 通道等）后，再换成真正的观测实现。</summary>
    public sealed class ReservedTarget : IServiceTarget
    {
        private readonly AppKind _kind;
        private readonly string _why;

        public ReservedTarget(AppKind kind, string why)
        {
            _kind = kind;
            _why = why == null ? "" : why;
        }

        public AppKind Kind { get { return _kind; } }

        public bool IsAvailable() { return false; }

        public ServiceReport Probe()
        {
            return new ServiceReport(_kind, ServiceState.Down, 0, "形态已预留但暂无可观测证据：" + _why);
        }

        public int FindPid() { return 0; }

        public string Describe()
        {
            return _kind + "（预留形态；暂无可观测事实：" + _why + "）";
        }
    }
}