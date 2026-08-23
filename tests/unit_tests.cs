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

        // ---- v2.1 F: 日志轮转 ----
        Console.WriteLine("[5] log rotation");
        string lr = Path.Combine(Path.GetTempPath(), "ulrot_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(lr);
        string lf = Path.Combine(lr, "launcher.log");
        Check(!Program.Test.RotateLog(lf, 100), "no file -> no rotation");
        File.WriteAllText(lf, new string('a', 5000));
        Check(Program.Test.RotateLog(lf, 100), "over limit -> rotated");
        Check(File.Exists(lf + ".1") && File.Exists(lf), "archive + new file exist");
        Check(!Program.Test.RotateLog(lf, 1000000), "under limit -> no rotation");
        File.WriteAllText(lf + ".1", "old");
        File.WriteAllText(lf, new string('b', 99));
        Check(Program.Test.RotateLog(lf, 50) && File.ReadAllText(lf + ".1") == new string('b', 99), "over limit rotates and overwrites old .1");
        try { Directory.Delete(lr, true); } catch { }

        // ---- v2.1 G: 备份命名规则 ----
        Console.WriteLine("[6] backup naming (kind suffix)");
        Check(Program.Test.BkSuffix(Program.BackupKind.Manual) == "", "manual -> no suffix");
        Check(Program.Test.BkSuffix(Program.BackupKind.Auto) == "-auto", "auto -> -auto");
        Check(Program.Test.BkSuffix(Program.BackupKind.PreRestore) == "-pre-restore", "pre-restore suffix");
        Check(Program.Test.BkSuffix(Program.BackupKind.PreImport) == "-pre-import", "pre-import suffix");
        Check(Program.Test.BkSuffix(Program.BackupKind.PreWipe) == "-pre-wipe", "pre-wipe suffix");
        Check(!Program.Test.IsAutoName("dsh-data-20260101-000001"), "manual name -> not auto");
        Check(Program.Test.IsAutoName("dsh-data-20260101-000001-") == false, "bare dash -> not auto");
        Check(Program.Test.IsAutoName("dsh-data-20260101-000001-auto") && Program.Test.IsAutoName("dsh-data-20260101-000001-pre-restore"), "auto/pre names -> auto");
        Check(Program.Test.IsAutoName("dsh-data-20260101-000001-pre-wipe") && Program.Test.IsAutoName("dsh-data-20260101-000001-pre-import"), "pre-wipe/pre-import -> auto");

        // ---- v2.1 G: 保留策略（只清自动类 / 保底 / 手动永久） ----
        Console.WriteLine("[7] backup retention");
        // Retention() 扫描 StateDir\backup = 本 exe 运行目录；单测进程不跑生产 Main，需注入 StateDir
        string runDir = AppDomain.CurrentDomain.BaseDirectory;
        Program.Test.SetStateDir(runDir);
        string rb = Path.Combine(runDir, "backup");
        if (Directory.Exists(rb)) Directory.Delete(rb, true);
        Directory.CreateDirectory(rb);
        // 手动 2 份（无后缀）
        Directory.CreateDirectory(Path.Combine(rb, "dsh-data-20260101-000001"));
        Directory.CreateDirectory(Path.Combine(rb, "dsh-data-20260101-000002"));
        // 自动类 5 份（pre-restore）
        for (int i = 1; i <= 5; i++)
            Directory.CreateDirectory(Path.Combine(rb, "dsh-data-20260101-00000" + i + "-pre-restore"));
        Program.Test.SetKeep(4);
        var rm = Program.Test.Retention();
        Check(rm.Count == 1 && rm[0] == "dsh-data-20260101-000001-pre-restore", "5 auto with keep=4 -> removed only oldest auto");
        Check(!Directory.Exists(Path.Combine(rb, "dsh-data-20260101-000001-pre-restore")), "oldest auto gone");
        Check(Directory.Exists(Path.Combine(rb, "dsh-data-20260101-000005-pre-restore")), "newest auto kept");
        Check(Directory.Exists(Path.Combine(rb, "dsh-data-20260101-000001")) && Directory.Exists(Path.Combine(rb, "dsh-data-20260101-000002")), "manual backups never removed");
        // 保底 3：keep<3 时按 3 处理
        for (int i = 1; i <= 5; i++)
            Directory.CreateDirectory(Path.Combine(rb, "dsh-data-20260102-00000" + i + "-pre-import"));
        Program.Test.SetKeep(2);
        var rm2 = Program.Test.Retention();
        int autoLeft = 0;
        foreach (string d in Directory.GetDirectories(rb))
            if (Program.Test.IsAutoName(Path.GetFileName(d))) autoLeft++;
        Check(rm2.Count == 6 && autoLeft == 3, "keep=2 clamps to 3 (floor); oldest 6 across all auto kinds removed, 3 kept");
        try { Directory.Delete(rb, true); } catch { }
        Program.Test.SetKeep(10);

        // ---- v2.1 B: 服务状态三态判定 ----
        Console.WriteLine("[8] service state judge");
        Check(Program.Test.JudgeState(false, false) == Program.ServiceState.Down, "port closed -> Down");
        Check(Program.Test.JudgeState(false, true) == Program.ServiceState.Down, "port closed (+http) -> Down");
        Check(Program.Test.JudgeState(true, false) == Program.ServiceState.Listening, "port open, http not ready -> Listening");
        Check(Program.Test.JudgeState(true, true) == Program.ServiceState.Ready, "port + http ready -> Ready");

        // ---- v2.1 A: 版本比较 / 更新解析 / 更新探测（注入假网络） ----
        Console.WriteLine("[9] update check");
        Check(Program.Test.CmpVer("2.0.0", "2.1.0") < 0, "2.0.0 < 2.1.0");
        Check(Program.Test.CmpVer("2.1.0", "2.1.0") == 0, "equal");
        Check(Program.Test.CmpVer("2.2.0", "2.1.0") > 0, "2.2.0 > 2.1.0");
        Check(Program.Test.CmpVer("2.10.0", "2.9.9") > 0, "2.10.0 > 2.9.9 (numeric, not lexicographic)");
        Check(Program.Test.CmpVer("2.1", "2.1.0") == 0, "2.1 == 2.1.0 (missing segment = 0)");
        Check(Program.Test.ParseTag("{ \"tag_name\": \"v2.1.0\" }") == "2.1.0", "parse tag with v prefix");
        Check(Program.Test.ParseTag("{ \"something\": 1 }") == null, "no tag_name -> null");
        Check(Program.Test.ParseTag("not json") == null, "garbage -> null");
        Program.Test.SetHttpGet(delegate(string u, int ms) { return "{ \"tag_name\": \"v2.2.0\" }"; });
        string cur2 = Program.Test.CurVer();
        Check(Program.Test.Latest() == "2.2.0", "newer release detected (" + cur2 + " -> 2.2.0)");
        Program.Test.SetHttpGet(delegate(string u, int ms) { return "{ \"tag_name\": \"v" + cur2 + "\" }"; });
        Check(Program.Test.Latest() == null, "same version -> no update");
        Program.Test.SetHttpGet(delegate(string u, int ms) { return null; });
        Check(Program.Test.Latest() == null, "network failure -> silent null");
        Program.Test.SetHttpGet(null);

        Console.WriteLine("[10] dsh update management (v2.1.1)");
        // BackupKind.PreUpdate 后缀 + 自动类识别
        Check(Program.Test.BkSuffix(Program.BackupKind.PreUpdate) == "-pre-update", "pre-update suffix");
        Check(Program.Test.IsAutoName("dsh-data-20260101-000001-pre-update"), "pre-update -> auto");
        // 版本输出解析（单行数组 + JSON 多行 + 空）
        string[] p1 = Program.Test.ParseVersions("[ '0.1.0', '1.0.0', '2.0.0' ]");
        Check(p1.Length == 3 && p1[1] == "1.0.0", "parse single-line array");
        string[] p2 = Program.Test.ParseVersions("[\n  \"1.0.0\",\n  \"2.0.0\"\n]");
        Check(p2.Length == 2 && p2[0] == "1.0.0", "parse JSON multiline");
        Check(Program.Test.ParseVersions(null).Length == 0 && Program.Test.ParseVersions("").Length == 0, "empty -> empty");
        // 干净版本判断
        Check(Program.Test.CleanVer("2.1.0") && Program.Test.CleanVer("1.0") && !Program.Test.CleanVer("2.1.0-beta.1") && !Program.Test.CleanVer("abc") && !Program.Test.CleanVer("1.2.3.4"), "clean version filter");
        // 版本过滤：保留 pre-release（核心段干净即可）、排序、取最近 N 倒序
        string[] f1 = Program.Test.FilterVers(new string[] { "1.0.0", "2.0.0", "0.9.0", "2.1.0-beta.1", "garbage", "1.5.0" }, 3);
        Check(f1.Length == 3 && f1[0] == "2.1.0-beta.1" && f1[1] == "2.0.0" && f1[2] == "1.5.0", "filter keeps pre-release, sorts desc, takes N");
        string[] f2 = Program.Test.FilterVers(new string[] { "1.0.0", "2.0.0" }, 10);
        Check(f2.Length == 2 && f2[0] == "2.0.0", "N > count keeps all, newest first");
        // v2.1.2 回归修复：pre-release（rc）版本支持——dsh 正式发布均为 rc
        Check(Program.Test.SanitizeLatest("0.1.1-rc.2") == "0.1.1-rc.2" && Program.Test.SanitizeLatest("v2.1.0") == "2.1.0", "sanitize accepts rc / v-prefix");
        Check(Program.Test.SanitizeLatest("garbage") == null && Program.Test.SanitizeLatest("latest") == null && Program.Test.SanitizeLatest("") == null && Program.Test.SanitizeLatest(null) == null, "sanitize rejects garbage");
        Check(Program.Test.CmpVer("0.1.1", "0.1.1-rc.2") == 0 && Program.Test.CmpVer("0.1.1-rc.2", "0.2.0") < 0 && Program.Test.CmpVer("2.1.0-beta.1", "2.1.0") == 0, "CompareVersions strips pre-release suffix");
        // 历史版本记忆：去重、最新在前、截断 10
        Program.Test.ResetVersions();
        Program.Test.RecordVer("v2.0.0");
        Program.Test.RecordVer("2.1.0");
        Program.Test.RecordVer("2.0.0");   // 重复 -> 去重并提到最前
        Check(Program.Test.DshVersions() == "2.0.0,2.1.0", "record dedupes, newest first");
        for (int i = 0; i < 12; i++) Program.Test.RecordVer("1." + i + ".0");
        string[] hv = Program.Test.DshVersions().Split(',');
        Check(hv.Length == 10 && hv[0] == "1.11.0", "history truncated to 10, newest first");
        Program.Test.ResetVersions();
        // P1 修复契约：备份源不存在 -> null（UpdateDsh 据此跳过 pre-update 备份而非中止）
        Check(Program.Test.DoBackup(Path.Combine(Path.GetTempPath(), "dsht-no-such-" + Guid.NewGuid().ToString("N"))) == null, "DoBackup missing source -> null");

        Console.WriteLine("[11] protection backup strictness (v2.1.2)");
        // Pre*（保护性）备份遇到被锁文件 -> 整体失败(null)；手动备份 best-effort 成功且跳过被锁文件
        string sdir = Path.Combine(Path.GetTempPath(), "dsht-strict-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sdir);
        File.WriteAllText(Path.Combine(sdir, "ok.txt"), "1");
        string lockF = Path.Combine(sdir, "locked.bin");
        File.WriteAllText(lockF, "x");
        var lockStream = new FileStream(lockF, FileMode.Open, FileAccess.ReadWrite, FileShare.None);   // 独占锁定：复制必然失败
        try
        {
            Check(Program.Test.DoBackupKind(sdir, Program.BackupKind.PreRestore) == null, "Pre* backup fails when a file is locked (strict)");
            Check(Program.Test.DoBackupKind(sdir, Program.BackupKind.Manual) != null, "Manual backup skips locked file (best-effort)");
        }
        finally { lockStream.Dispose(); }
        try { Directory.Delete(sdir, true); } catch { }
        try { Directory.Delete(Path.Combine(runDir, "backup"), true); } catch { }   // 清掉本块产生的备份目录

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