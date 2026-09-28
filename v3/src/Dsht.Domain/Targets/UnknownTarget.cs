using Dsht.Domain.Abstractions;
using Dsht.Domain.Model;

namespace Dsht.Domain.Targets
{
    /// <summary>未识别形态。当所有已知形态都没有观测证据时使用：
    /// 明确报 Down + 说明"未识别"，**不猜测、不假装 Ready**。</summary>
    public sealed class UnknownTarget : IServiceTarget
    {
        private readonly string _detail;

        public UnknownTarget(string detail)
        {
            _detail = detail == null ? "" : detail;
        }

        public AppKind Kind { get { return AppKind.Unknown; } }

        public bool IsAvailable() { return false; }

        public ServiceReport Probe()
        {
            string basis = "未识别形态：web/headless/acp/desktop 均无观测证据";
            if (_detail.Length > 0) basis = basis + "（" + _detail + "）";
            return new ServiceReport(AppKind.Unknown, ServiceState.Down, 0, basis);
        }

        public int FindPid() { return 0; }

        public string Describe()
        {
            return "未识别形态：未发现可识别的 dsh 运行方式；不会假定它在运行";
        }
    }
}