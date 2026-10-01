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
        int FindPortPid(int port);   // 监听指定端口的进程 PID（Windows: netstat；Linux: ss）
        ServiceState Judge(bool portOpen, bool httpOk, Func<bool> listenerIsDsh);
        ServiceState Probe();
    }

    // ---- 外部进程调用 ----
    interface IShellRunner
    {
        string Capture(string exe, string args);   // 捕获输出（现有 RunCapture）
        int Visible(string file, string args);     // 可见窗口（现有 RunVisible）
        string WhichDsh();                          // 定位 dsh 可执行文件（Windows: where；Linux: PATH 扫描）
        string DshVersion();                        // dsh --version（Linux 无需 cmd.exe 包装）
        int KillTree(int pid);                      // 终止进程树（Windows: taskkill /T /F；Linux: kill -TERM/-KILL）
        string NodeVersion();                       // node --version（Windows 走 node.exe）
        string NpmVersion();                        // npm --version（Windows 走 cmd.exe 包装）
        string NpmViewVersions();                   // npm view <pkg> versions
        string NpmViewLatest();                     // npm view <pkg> version
        int NpmInstallGlobal(string pkg, string registry);   // 全局安装（Windows: cmd.exe /c npm install -g --registry=…）
        int NpmUninstallGlobal();                   // 全局卸载（Windows: cmd.exe /c npm uninstall -g @deepseek-ai/dsh）
        int WingetInstallNode();                    // Windows: winget 装 Node LTS；Linux: -1（无对应物，交回手动指引）
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

        /// <summary>按当前操作系统选择实现（启动早期调用一次）。Windows 上不做任何替换 → 行为零变化。</summary>
        public static void Init()
        {
            try
            {
                bool win = true;
                try { win = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows); }
                catch { try { win = System.Environment.OSVersion.Platform == System.PlatformID.Win32NT; } catch { win = true; } }
                if (win) return;
                Paths = new LinuxPathService();
                Probe = new LinuxServiceProbe();
                Shell = new LinuxShellRunner();
                Shortcuts = new LinuxShortcutService();
            }
            catch { }   // 平台识别失败时保守留在 Windows 实现
        }
    }

    // ---- Windows 实现：纯转调，零行为差异 ----
}
