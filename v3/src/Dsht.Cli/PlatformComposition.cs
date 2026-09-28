using System.IO;
using Dsht.Domain.Abstractions;
using Dsht.Domain.Model;
using Dsht.Domain.Targets;
using Dsht.Platform.Linux;
using Dsht.Platform.Windows;

namespace Dsht.Cli
{
    /// <summary>平台装配：按运行平台选择实现（单 exe、运行时判定）。
    /// 判定用 Path.DirectorySeparatorChar 而非 RuntimeInformation —— 前者在 .NET Framework 与 .NET 8 上行为一致，
    /// 且不需要额外 API（这也是本地用 csc 就能编译验证整套代码的原因）。</summary>
    internal static class PlatformComposition
    {
        internal const int WebPort = 3080;
        internal const string WebUrl = "http://127.0.0.1:3080/";

        public static bool IsWindows()
        {
            return Path.DirectorySeparatorChar == '\\';
        }

        public static ServiceRegistry Compose()
        {
            ServiceRegistry reg = new ServiceRegistry();
            if (IsWindows())
            {
                WindowsPaths paths = new WindowsPaths();
                WindowsHttpProbe http = new WindowsHttpProbe();
                WindowsPortProbe port = new WindowsPortProbe();
                WindowsProcessQuery proc = new WindowsProcessQuery(http, WebUrl, 800);
                reg.Add<IPaths>(paths);
                reg.Add<IPortProbe>(port);
                reg.Add<IHttpProbe>(http);
                reg.Add<IProcessQuery>(proc);
                reg.Add<IToolchainQuery>(new WindowsToolchainQuery());
                reg.Add<IFileSystemQuery>(new WindowsFileSystemQuery());
                reg.Add<IIntegritySource>(new WindowsIntegritySource());
                reg.Add<IProfileSource>(new WindowsProfileSource(paths));
                reg.Add<IBackupSource>(new WindowsBackupSource(paths));
                reg.Add<IServiceTarget>(new WebTarget(port, http, proc, new WebTargetOptions(WebPort, WebUrl, 800, 800)));
            }
            else
            {
                LinuxPaths paths = new LinuxPaths();
                LinuxHttpProbe http = new LinuxHttpProbe();
                LinuxPortProbe port = new LinuxPortProbe();
                LinuxProcessQuery proc = new LinuxProcessQuery(http, WebUrl, 800);
                reg.Add<IPaths>(paths);
                reg.Add<IPortProbe>(port);
                reg.Add<IHttpProbe>(http);
                reg.Add<IProcessQuery>(proc);
                reg.Add<IToolchainQuery>(new LinuxToolchainQuery());
                reg.Add<IFileSystemQuery>(new LinuxFileSystemQuery());
                reg.Add<IIntegritySource>(new LinuxIntegritySource());
                reg.Add<IProfileSource>(new LinuxProfileSource(paths));
                reg.Add<IBackupSource>(new LinuxBackupSource(paths));
                reg.Add<IServiceTarget>(new WebTarget(port, http, proc, new WebTargetOptions(WebPort, WebUrl, 800, 800)));
            }
            return reg;
        }
    }
}