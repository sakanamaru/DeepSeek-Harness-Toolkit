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
            _cachedPid = -1;   // ★ 每次 Probe 重置 ✓✓（绝不用上一次的陈旧 pid ✗）
            ServiceState st = ServiceJudge.Judge(portOpen, portOpen && SafeHttp(), ListenerIsDsh);
            int pid = (st == ServiceState.Down) ? 0 : FindPid();
            return new ServiceReport(AppKind.Web, st, pid, Basis(portOpen, st));
        }

        // ★★ 审查抓到：`ListenerIsDsh()` 起一次 netstat ✓ 而 `FindPid()` 又起一次 ✗
        //   → 每次 status 探测**两次 netstat** ✓（外加 PowerShell + 两次 tasklist ✓ 共 5 个外部进程 ✗）
        // ✓ 现在：**同一个探测周期内只问一次** ✓✓（Probe 会重置它 ✓ 不会用陈旧值 ✓）
        private int _cachedPid = -1;

        /// <summary>本探测周期内监听该端口的 pid（**只问一次** ✓✓）。</summary>
        private int ListenerPid()
        {
            if (_cachedPid >= 0) return _cachedPid;
            try { _cachedPid = (_proc == null) ? 0 : _proc.PidListeningOn(_opt.Port); }
            catch { _cachedPid = 0; }
            return _cachedPid;
        }

        public int FindPid()
        {
            return ListenerPid();
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
                int pid = ListenerPid();   // ★ 复用同一个探测周期的结果 ✓✓（不再起第二次 netstat ✗）
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