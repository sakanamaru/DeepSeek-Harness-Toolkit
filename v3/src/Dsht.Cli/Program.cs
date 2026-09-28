using System;
using Dsht.Domain.Abstractions;
using Dsht.Domain.Model;
using Dsht.Domain.Targets;
using Dsht.Platform.Windows;

namespace Dsht.Cli
{
    /// <summary>V3 CLI 组合根 + 命令面（第一步只做 status，用于与 v2.x 对标记行契约）。</summary>
    public static class Program
    {
        private const int WebPort = 3080;
        private const string WebUrl = "http://127.0.0.1:3080/";

        public static int Main(string[] args)
        {
            ServiceRegistry reg = Compose();
            string cmd = args.Length > 0 ? args[0] : "";
            bool detail = false;
            for (int i = 1; i < args.Length; i++) { if (args[i] == "--detail") detail = true; }

            if (cmd == "status")
            {
                IServiceTarget target = reg.Get<IServiceTarget>();
                ServiceReport r = target.Probe();
                Console.WriteLine(r.StatusMarker);
                if (detail)
                {
                    Console.WriteLine("STATUS_PID " + (r.Pid > 0 ? r.Pid.ToString() : "0"));
                    // TODO(V3-3b)：STATUS_START / STATUS_UPTIME 需要进程启动时间与时长格式化，下一轮补
                }
                return 0;
            }
            if (cmd == "describe")
            {
                IServiceTarget target = reg.Get<IServiceTarget>();
                Console.WriteLine(target.Describe());
                ServiceReport r = target.Probe();
                Console.WriteLine("basis: " + r.Basis);
                return 0;
            }
            Console.WriteLine("usage: dsht status [--detail] | describe");
            return 2;
        }

        /// <summary>组合根：装配平台实现 → 领域服务。形态识别只用可观测事实。</summary>
        private static ServiceRegistry Compose()
        {
            WindowsHttpProbe http = new WindowsHttpProbe();
            WindowsPortProbe port = new WindowsPortProbe();
            WindowsProcessQuery proc = new WindowsProcessQuery(http, WebUrl, 800);

            ServiceRegistry reg = new ServiceRegistry();
            reg.Add<IPortProbe>(port);
            reg.Add<IHttpProbe>(http);
            reg.Add<IProcessQuery>(proc);
            reg.Add<IServiceTarget>(new WebTarget(port, http, proc, new WebTargetOptions(WebPort, WebUrl, 800, 800)));
            return reg;
        }
    }
}