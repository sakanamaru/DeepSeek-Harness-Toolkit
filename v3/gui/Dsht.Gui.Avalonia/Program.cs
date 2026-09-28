using System;
using Avalonia;

namespace Dsht.Gui.Avalonia
{
    /// <summary>跨平台 GUI（Avalonia）入口。骨架阶段：一个窗口 + 左导航 + 右侧 CLI 输出。</summary>
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
        }
    }
}
