// ============================================================================
//  v2.8 阶段 3：Windows 平台实现（Platform/Windows）
//  ----------------------------------------------------------------------------
//  这里是四个接缝接口的 **Windows 实现体**：一律转调 Program 里既有的静态方法
//  （原命令串逐字符保留），因此 Windows 行为零变化。
//  分层纪律：本文件是 Windows 专有实现唯一的落点；`src/Core/Program.PlatformSeams.cs`
//  只允许放接口声明与 `Platform` 持有者，不得出现 Windows 命令串或 Windows-only API。
// ============================================================================
using System;
using System.Collections.Generic;

partial class Program
{    class WindowsPathService : IPathService
    {
        public string DataRoot() { return Program.DataRoot(); }
        public string BackupsRoot() { return Program.BackupsRoot(); }
        public string DesktopDir() { return Program.DesktopDir(); }
        public string WorkspaceRoot() { return Program.WorkspaceRoot(); }
        public string Normalize(string path) { return Program.P(path); }
        public string TrimTrailingSep(string path) { return Program.TrimP(path); }
    }

    class WindowsServiceProbe : IServiceProbe
    {
        public bool PortOpen(int port, int timeoutMs) { return Program.IsPortOpen(port, timeoutMs); }
        public bool HttpReady(string url, int ms) { return Program.HttpReady(url, ms); }
        public bool ListenerIsDsh() { return Program.ListenerIsDsh(); }
        public int FindPortPid(int port) { return Program.FindPortPid(port); }
        public ServiceState Judge(bool portOpen, bool httpOk, Func<bool> listenerIsDsh) { return Program.JudgeState3(portOpen, httpOk, listenerIsDsh); }
        public ServiceState Probe() { return Program.ProbeService(); }
    }

    class WindowsShellRunner : IShellRunner
    {
        public string Capture(string exe, string args) { return Program.RunCapture(exe, args); }
        public int Visible(string file, string args) { return Program.RunVisible(file, args); }
        public string WhichDsh() { return Program.LocateDsh(); }
        public string DshVersion() { return Program.RunDshVersion(); }
        public int KillTree(int pid) { Program.KillProcessTree(pid); return 0; }
        public string NodeVersion() { return Program.RunCapture("node.exe", "--version"); }
        public string NpmVersion() { return Program.RunCapture("cmd.exe", "/c npm --version 2>nul"); }
        public string NpmViewVersions() { return Program.RunCapture("cmd.exe", "/c npm view @deepseek-ai/dsh versions 2>nul"); }
        public string NpmViewLatest() { return Program.RunCapture("cmd.exe", "/c npm view @deepseek-ai/dsh version 2>nul"); }
        public int NpmInstallGlobal(string pkg, string registry) { return Program.RunVisible("cmd.exe", "/c npm install -g --registry=" + registry + " " + pkg); }
        public int NpmUninstallGlobal() { return Program.RunVisible("cmd.exe", "/c npm uninstall -g @deepseek-ai/dsh"); }
        public int WingetInstallNode() { return Program.RunVisible("winget.exe", "install --id OpenJS.NodeJS.LTS --accept-source-agreements --accept-package-agreements"); }
    }

    class WindowsShortcutService : IShortcutService
    {
        public string Create(string desktopDir, string targetExe, string lnkBase, string desc) { return Program.CreateDesktopShortcut(desktopDir, targetExe, lnkBase, desc); }
    }
}
