// DeepSeek Harness Toolkit - 单元测试（与生产源码同程序集编译）
// 构建: csc /nologo /target:exe /define:UNIT /out:unittests.exe dsh_v2.cs tests\unit_tests.cs
// 运行: unittests.exe   （退出码 0=全过，1=有失败）
// 说明: 生产文件 dsh_v2.cs 中 Main 被 #if !UNIT 包裹，本文件提供测试入口；
//       所有被测方法为 dsh_v2.cs 内的内部 static 方法，同程序集直接访问，无第三方依赖。
using System;
using System.IO;

public static class UnitTests
{
    static int fails = 0;
    static int total = 0;

    static void Check(bool ok, string name)
    {
        total++;
        if (ok) { Console.WriteLine("  [PASS] " + name); }
        else { fails++; Console.WriteLine("  [FAIL] " + name); }
    }

    public static void Main()
    {
        try { AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false); } catch { }
        try { AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false); } catch { }
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        Console.WriteLine("== DeepSeek Harness Toolkit unit tests ==");

        // ---- P / TrimP 往返（UNC / 中文空格 / 深路径 / 盘根） ----
        Console.WriteLine("[1] P/TrimP roundtrip");
        string[] paths = {
            @"\\nas\share\team",
            @"C:\x\y",
            @"C:\x",
            @"D:\work\recovery\sub",
            @"\\server\deep\dir with space",
            @"D:\work\中文 目录\node_modules\.bin",
            @"C:\Program Files (x86)\Node"
        };
        foreach (string c in paths)
        {
            string t = Program.Test.PathP(c);
            string back = Program.Test.PathTrim(t);
            Check(back.Equals(c, StringComparison.OrdinalIgnoreCase), "roundtrip " + c);
        }
        Check(Program.Test.PathTrim(Program.Test.PathP(@"D:\work\中文 目录\node_modules\.bin")).Equals(@"D:\work\中文 目录\node_modules\.bin", StringComparison.OrdinalIgnoreCase), "deep roundtrip (unsafe->safe->unsafe)");

        // ---- LooksLikeWorkspace 黑名单 ----
        Console.WriteLine("[2] LooksLikeWorkspace blacklist");
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] bad = {
            home, home + @"\Public", home + @"\Documents\myproj", @"C:\Program Files",
            @"C:\Program Files (x86)", @"C:\ProgramData", @"C:\PerfLogs", @"C:\Inetpub",
            @"C:\Recovery", @"C:\$Recycle.Bin", @"C:\System Volume Information",
            home + @"\Desktop", @"D:\", @"C:\", @"E:\", @"D:\windows.old",
            @"C:\$winreagent", home + @"\AppData", home + @"\.dsh"
        };
        foreach (string b in bad)
            Check(!Program.Test.WorkspaceOk(b), "reject " + b);
        string[] good = { @"D:\work\recovery\sub", @"\\nas\share\team", @"D:\dev\proj-a" };
        foreach (string g in good)
            Check(Program.Test.WorkspaceOk(g), "allow " + g);

        // ---- LooksLikeDshData ----
        Console.WriteLine("[3] LooksLikeDshData markers");
        string td = Path.Combine(Path.GetTempPath(), "ulldd_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(td);
        Check(!Program.Test.DshData(td), "empty dir -> false");
        File.WriteAllText(Path.Combine(td, "settings.yaml"), "x");
        Check(Program.Test.DshData(td), "settings.yaml -> true");
        File.Delete(Path.Combine(td, "settings.yaml"));
        Directory.CreateDirectory(Path.Combine(td, "sessions"));
        Check(Program.Test.DshData(td), "sessions dir -> true");
        Directory.Delete(Path.Combine(td, "sessions"));
        Check(!Program.Test.DshData(Path.Combine(td, "no-such-dir")), "missing dir -> false");
        try { Directory.Delete(td); } catch { }

        // ---- IsPortOpen（环境自适应 + 恒否） ----
        Console.WriteLine("[4] IsPortOpen");
        bool live3080 = IO.Port3080Open();
        Check(Program.Test.PortOpen(3080, 500) == live3080, "3080 matches live state (" + live3080 + ")");
        Check(!Program.Test.PortOpen(59999, 400), "59999 (unused) -> false");

        Console.WriteLine("");
        Console.WriteLine("== " + (total - fails) + "/" + total + " passed, " + fails + " failed ==");
        Environment.Exit(fails == 0 ? 0 : 1);
    }
}

// 仅用于 3080 自适应的辅助（与生产逻辑无关的独立端口探测）
public static class IO
{
    public static bool Port3080Open()
    {
        try
        {
            var c = new System.Net.Sockets.TcpClient();
            var t = c.ConnectAsync("127.0.0.1", 3080);
            bool ok = t.Wait(600);
            if (ok && c.Connected) { c.Close(); return true; }
            c.Close(); return false;
        }
        catch { return false; }
    }
}