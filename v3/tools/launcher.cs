// dsh-minato GUI 启动器（用户要求："用 bat 启动 gui 有一点不优雅，套个 exe 壳子上个 logo" ✓✓）
// 设计要点（全部零第三方依赖 ✓）：
//   · /target:winexe → **不弹控制台黑框** ✓（这是 .cmd 最不优雅的地方 ✓）
//   · /win32icon:logo-icon.ico → 任务栏/资源管理器**显示自己的图标** ✓
//   · 只做一件事：启动同目录的 gui\dsht-gui.exe ✓，并把 DSHT_CLI 指给同目录的 CLI ✓
//     （GUI 的 CLI 查找顺序里 DSHT_CLI 排第一 ✓ → 这样不管从哪启动都能找到 ✓✓）
//   · 找不到 GUI 时**明确报错**（不静默退出 ✓ 不用户对着空气发呆 ✓）
using System;
using System.Diagnostics;
using System.IO;

internal static class Launcher
{
    [STAThread]
    private static int Main(string[] args)
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        string gui = Path.Combine(Path.Combine(dir, "gui"), "dsht-gui.exe");
        string cli = Path.Combine(dir, "dsh-minato.exe");

        if (!File.Exists(gui))
        {
            // 用 cmd 的 pause 展示错误 ✓（不引 WinForms ✓ 保持最小 ✓）
            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe", "/c echo. & echo   [dsh-minato] 找不到 GUI： " + gui + " & echo   请把本启动器放在完整包目录里（与 gui\\ 同级）& echo. & pause")
                { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Normal });
            }
            catch { }
            return 2;
        }

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(gui);
            psi.UseShellExecute = false;
            psi.WorkingDirectory = dir;
            if (File.Exists(cli)) psi.EnvironmentVariables["DSHT_CLI"] = cli;
            // 把参数原样转给 GUI ✓（将来 GUI 支持参数时不用改这里 ✓）
            if (args != null && args.Length > 0) psi.Arguments = string.Join(" ", args);
            Process.Start(psi);
            return 0;
        }
        catch (Exception ex)
        {
            try { Process.Start(new ProcessStartInfo("cmd.exe", "/c echo. & echo   [dsh-minato] 启动 GUI 失败： " + ex.Message + " & echo. & pause") { UseShellExecute = true }); } catch { }
            return 3;
        }
    }
}