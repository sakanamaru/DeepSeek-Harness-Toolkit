using System;
using Dsht.Domain.Abstractions;
using Dsht.Domain.Model;
using Dsht.Domain.Services;

namespace Dsht.Domain.Targets
{
    /// <summary>dsh web 形态（浏览器界面，通常监听本地端口）。
    /// 观测顺序与 v2.x 的 ProbeService 一致：端口 → HTTP → 监听进程身份（懒求值）。</summary>
    public sealed class WebTarget : IServiceTarget
    {
        private readonly IPortProbe _port;
        private readonly IHttpProbe _http;
        private readonly IProcessQuery _proc;
        private readonly WebTargetOptions _opt;

        public WebTarget(IPortProbe port, IHttpProbe http, IProcessQuery proc, WebTargetOptions opt)
        {
            _port = port;
            _http = http;
            _proc = proc;
            _opt = opt;
        }

        public AppKind Kind { get { return AppKind.Web; } }

        /// <summary>web 形态本身总是"可用"的；dsh 是否安装由组合根（CLI）判定。</summary>
        public bool IsAvailable() { return true; }

        public ServiceReport Probe()
        {
            bool portOpen = Safe(_port);
            ServiceState st = ServiceJudge.Judge(portOpen, portOpen && SafeHttp(), ListenerIsDsh);
            int pid = (st == ServiceState.Down) ? 0 : FindPid();
            return new ServiceReport(AppKind.Web, st, pid, Basis(portOpen, st));
        }

        public int FindPid()
        {
            if (_proc == null) return 0;
            try { return _proc.PidListeningOn(_opt.Port); }
            catch { return 0; }
        }

        public string Describe()
        {
            return "dsh web（浏览器界面，本地端口 " + _opt.Port + "）";
        }

        private bool Safe(IPortProbe p)
        {
            if (p == null) return false;
            try { return p.IsOpen(_opt.Port, _opt.PortTimeoutMs); }
            catch { return false; }
        }

        private bool SafeHttp()
        {
            if (_http == null) return false;
            try { return _http.IsReady(_opt.Url, _opt.HttpTimeoutMs); }
            catch { return false; }
        }

        private bool ListenerIsDsh()
        {
            if (_proc == null) return false;
            try
            {
                int pid = _proc.PidListeningOn(_opt.Port);
                return pid > 0 && _proc.IsDshCommandLine(pid);
            }
            catch { return false; }
        }

        private string Basis(bool portOpen, ServiceState st)
        {
            if (!portOpen) return "端口 " + _opt.Port + " 无监听";
            if (st == ServiceState.Ready) return "端口已开 + HTTP 就绪（或监听进程身份确认为 dsh）";
            return "端口已开，但 HTTP 未就绪且监听进程身份未确认";
        }
    }
}