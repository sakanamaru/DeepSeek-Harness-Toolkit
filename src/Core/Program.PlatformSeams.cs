// ============================================================================
//  v2.8 阶段 2/3：平台接缝（Platform Seams）
//  ----------------------------------------------------------------------------
//  目的：为「核心跨平台」建立可替换的缝。本文件**只声明接口 + 默认（Windows）实现**，
//        实现体一律转调现有的静态方法 —— 因此**不改变任何调用点、不改变任何行为**，
//        现有 297 单测 / 33 集成仍然全绿，这就是"可验证的接缝"。
//
//  阶段 3（Linux）只需要：另写一套 XxxLinux : IPathService/IServiceProbe/IShellRunner/IShortcutService，
//        在启动早期按 RuntimeInformation.IsOSPlatform 选择实现并赋给 Platform.* 即可。
//
//  约束：零第三方依赖；不得出现 Windows-only 编译期 API（本文件只有纯 BCL 引用）。
// ============================================================================
using System;

partial class Program
{
    // ---- 路径与目录 ----
    interface IPathService
    {
        string DataRoot();          // ~/.dsh（或 %APPDATA%/.dsh 兜底）
        string BackupsRoot();       // 工具箱备份根（StateDir\backup）
        string DesktopDir();        // 当前用户桌面（Windows 用 SpecialFolder）
        string WorkspaceRoot();     // 自动探测到的工作区
        string Normalize(string path);   // 统一规范化（现有 P）
        string TrimTrailingSep(string path);   // 去尾部分隔符但保留盘根/共享根语义（现有 TrimP）
    }

    // ---- 服务探测（三态 + 监听进程身份）----
    interface IServiceProbe
    {
        bool PortOpen(int port, int timeoutMs);
        bool HttpReady(string url, int ms);
        bool ListenerIsDsh();
        ServiceState Judge(bool portOpen, bool httpOk, Func<bool> listenerIsDsh);
        ServiceState Probe();
    }

    // ---- 外部进程调用 ----
    interface IShellRunner
    {
        string Capture(string exe, string args);   // 捕获输出（现有 RunCapture）
        int Visible(string file, string args);     // 可见窗口（现有 RunVisible）
    }

    // ---- 桌面快捷方式 ----
    interface IShortcutService
    {
        string Create(string desktopDir, string targetExe, string lnkBase, string desc);
    }

    // ---- 当前平台实现（默认 Windows；阶段 3 会在启动时按平台替换）----
    static class Platform
    {
        public static IPathService Paths = new WindowsPathService();
        public static IServiceProbe Probe = new WindowsServiceProbe();
        public static IShellRunner Shell = new WindowsShellRunner();
        public static IShortcutService Shortcuts = new WindowsShortcutService();
    }

    // ---- Windows 实现：纯转调，零行为差异 ----
    class WindowsPathService : IPathService
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
        public ServiceState Judge(bool portOpen, bool httpOk, Func<bool> listenerIsDsh) { return Program.JudgeState3(portOpen, httpOk, listenerIsDsh); }
        public ServiceState Probe() { return Program.ProbeService(); }
    }

    class WindowsShellRunner : IShellRunner
    {
        public string Capture(string exe, string args) { return Program.RunCapture(exe, args); }
        public int Visible(string file, string args) { return Program.RunVisible(file, args); }
    }

    class WindowsShortcutService : IShortcutService
    {
        public string Create(string desktopDir, string targetExe, string lnkBase, string desc) { return Program.CreateDesktopShortcut(desktopDir, targetExe, lnkBase, desc); }
    }
}