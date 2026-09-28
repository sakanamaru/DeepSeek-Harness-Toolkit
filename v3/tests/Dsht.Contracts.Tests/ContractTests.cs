using System;
using System.Collections.Generic;
using Dsht.Domain.Abstractions;
using Dsht.Domain.Model;
using Dsht.Domain.Services;
using Dsht.Domain.Targets;

// 契约测试：先用假观测跑通判定与形态语义；V3-4 会把同一套断言跑在真实平台实现上。
sealed class FakePort : IPortProbe
{
    public bool Open;
    public bool Throw;
    public bool IsOpen(int port, int timeoutMs) { if (Throw) throw new Exception("boom"); return Open; }
}
sealed class FakeHttp : IHttpProbe
{
    public bool Ready;
    public bool Throw;
    public bool IsReady(string url, int timeoutMs) { if (Throw) throw new Exception("boom"); return Ready; }
    public bool Responds(string url, int timeoutMs) { return Ready; }
}
sealed class FakeProc : IProcessQuery
{
    public int Pid;
    public bool IsDsh;
    public bool Throw;
    public int PidListeningOn(int port) { if (Throw) throw new Exception("boom"); return Pid; }
    public bool IsDshCommandLine(int pid) { return IsDsh; }
    public System.DateTime? StartTime(int pid) { return pid > 0 ? new System.DateTime(2026, 9, 28, 11, 53, 14) : (System.DateTime?)null; }
    public string CommandLine(int pid) { return ""; }
}

static class ContractTests
{
    static int _pass, _fail;
    static void Check(string name, bool ok)
    {
        if (ok) { _pass++; Console.WriteLine("  [PASS] " + name); }
        else { _fail++; Console.WriteLine("  [FAIL] " + name); }
    }

    static WebTarget Make(FakePort p, FakeHttp h, FakeProc q)
    {
        return new WebTarget(p, h, q, new WebTargetOptions(3080, "http://127.0.0.1:3080/", 800, 800));
    }

    static bool NoFile(string p) { return false; }
    static bool AnyFile(string p) { return true; }

    static int Main()
    {
        Console.WriteLine("== V3 契约测试（领域判定 + 形态语义）==");

        Console.WriteLine("[1] ServiceJudge 真值表（逐条对齐 v2.x JudgeState3）");
        Check("端口关 → Down", ServiceJudge.Judge(false, false, null) == ServiceState.Down);
        Check("端口关+HTTP就绪 → Down（端口优先）", ServiceJudge.Judge(false, true, null) == ServiceState.Down);
        Check("端口开+HTTP就绪 → Ready", ServiceJudge.Judge(true, true, null) == ServiceState.Ready);
        Check("端口开+HTTP未就绪+监听是dsh → Ready", ServiceJudge.Judge(true, false, delegate { return true; }) == ServiceState.Ready);
        Check("端口开+HTTP未就绪+监听非dsh → Listening", ServiceJudge.Judge(true, false, delegate { return false; }) == ServiceState.Listening);
        Check("端口开+HTTP未就绪+委托为null → Listening", ServiceJudge.Judge(true, false, null) == ServiceState.Listening);
        Check("监听判定抛异常 → 按false（Listening）", ServiceJudge.Judge(true, false, delegate { throw new Exception("boom"); }) == ServiceState.Listening);
        Check("两信号版：端口开+HTTP未就绪 → Listening", ServiceJudge.Judge(true, false) == ServiceState.Listening);

        Console.WriteLine("[2] WebTarget 形态语义");
        FakePort p = new FakePort(); FakeHttp h = new FakeHttp(); FakeProc q = new FakeProc();
        WebTarget t = Make(p, h, q);
        Check("Kind=Web", t.Kind == AppKind.Web);
        Check("Describe 提到端口", t.Describe().IndexOf("3080") >= 0);

        ServiceReport r1 = t.Probe();
        Check("端口关 → Down/无PID", r1.State == ServiceState.Down && r1.Pid == 0 && r1.StatusMarker == "STATUS_DOWN");
        Check("端口关 → Basis 说明无监听", r1.Basis.IndexOf("无监听") >= 0);

        p.Open = true; h.Ready = true;
        ServiceReport r2 = t.Probe();
        Check("端口开+HTTP就绪 → Ready/STATUS_UP", r2.State == ServiceState.Ready && r2.StatusMarker == "STATUS_UP");

        h.Ready = false; q.Pid = 4242; q.IsDsh = true;
        ServiceReport r3 = t.Probe();
        Check("端口开+HTTP未就绪+监听是dsh → Ready", r3.State == ServiceState.Ready);
        Check("Ready 时带出 PID", r3.Pid == 4242);

        q.IsDsh = false;
        ServiceReport r4 = t.Probe();
        Check("端口开+HTTP未就绪+监听非dsh → Listening/STATUS_STARTING", r4.State == ServiceState.Listening && r4.StatusMarker == "STATUS_STARTING");

        FakePort pt = new FakePort(); pt.Throw = true;
        Check("端口探测抛异常 → 按 Down 处理", Make(pt, h, q).Probe().State == ServiceState.Down);

        Console.WriteLine("[2b] 观测抛异常时的降级（v2.x 的 try/catch 语义）");
        FakePort p2 = new FakePort(); p2.Open = true;
        FakeHttp h2 = new FakeHttp(); h2.Throw = true;
        FakeProc q2 = new FakeProc(); q2.Pid = 7; q2.IsDsh = true;
        Check("HTTP 探测抛异常 → 按未就绪，但监听是 dsh 仍判 Ready", Make(p2, h2, q2).Probe().State == ServiceState.Ready);
        q2.IsDsh = false;
        Check("HTTP 抛异常 + 监听非 dsh → Listening", Make(p2, h2, q2).Probe().State == ServiceState.Listening);
        FakeProc q3 = new FakeProc(); q3.Throw = true;
        ServiceReport r5 = Make(p2, new FakeHttp(), q3).Probe();
        Check("进程查询抛异常 → 非 dsh → Listening 且 PID=0", r5.State == ServiceState.Listening && r5.Pid == 0);
        Console.WriteLine("[3] UnknownTarget 诚实性");
        UnknownTarget u = new UnknownTarget("测试注入");
        ServiceReport ru = u.Probe();
        Check("Kind=Unknown", u.Kind == AppKind.Unknown);
        Check("State=Down（不假装 Ready）", ru.State == ServiceState.Down && ru.StatusMarker == "STATUS_DOWN");
        Check("IsAvailable=false", !u.IsAvailable());
        Check("Describe 明确说明未识别", u.Describe().IndexOf("未识别") >= 0);
        Check("Basis 明确说明未识别", ru.Basis.IndexOf("未识别") >= 0);

        Console.WriteLine("[4] BackupRetention 保留策略（逐条对齐 v2.x）");
        Check("自动类：-auto", BackupRetention.IsAutoBackupName("dsh-data-20260921-193000-auto"));
        Check("自动类：-pre-restore/-pre-import/-pre-wipe/-pre-update",
            BackupRetention.IsAutoBackupName("x-pre-restore") && BackupRetention.IsAutoBackupName("x-pre-import")
            && BackupRetention.IsAutoBackupName("x-pre-wipe") && BackupRetention.IsAutoBackupName("x-pre-update"));
        Check("手动备份不算自动类", !BackupRetention.IsAutoBackupName("dsh-data-20260921-193000"));
        Check("非备份目录名被排除", !BackupRetention.IsBackupDirName("some-other-dir"));
        Check("保底 3 份：keep=1 → 3", BackupRetention.EffectiveKeep(1) == 3);
        Check("keep=10 → 10", BackupRetention.EffectiveKeep(10) == 10);

        System.Collections.Generic.List<string> autos5 = new System.Collections.Generic.List<string>();
        autos5.Add("dsh-data-20260901-100000-auto");
        autos5.Add("dsh-data-20260902-100000-auto");
        autos5.Add("dsh-data-20260903-100000-auto");
        autos5.Add("dsh-data-20260904-100000-auto");
        autos5.Add("dsh-data-20260905-100000-auto");
        System.Collections.Generic.List<string> del5 = BackupRetention.SelectForDeletion(autos5, 3);
        Check("5 份自动 + keep=3 → 删 2 份最旧", del5.Count == 2);
        Check("删除顺序为最旧在前", del5.Count == 2 && del5[0].IndexOf("20260901") > 0 && del5[1].IndexOf("20260902") > 0);
        Check("keep 超出总数 → 不删", BackupRetention.SelectForDeletion(autos5, 10).Count == 0);
        Check("空输入 → 不删", BackupRetention.SelectForDeletion(new System.Collections.Generic.List<string>(), 3).Count == 0);

        System.Collections.Generic.List<string> mixed = new System.Collections.Generic.List<string>();
        mixed.Add("dsh-data-20260901-100000");                 // 手动（永久保留）
        mixed.Add("dsh-data-20260902-100000");                 // 手动
        mixed.Add("dsh-data-20260903-100000-auto");
        mixed.Add("dsh-data-20260904-100000-auto");
        mixed.Add("dsh-data-20260905-100000-pre-wipe");
        mixed.Add("dsh-data-20260906-100000-auto");
        mixed.Add("dsh-data-20260907-100000-auto");
        mixed.Add("not-a-backup-auto");
        System.Collections.Generic.List<string> delMixed = BackupRetention.SelectForDeletion(mixed, 3);
        Check("混合：5 份自动 + keep=3 → 删 2 份最旧，手动永不删", delMixed.Count == 2 && delMixed[0].IndexOf("20260903") > 0 && delMixed[1].IndexOf("20260904") > 0);
        Check("混合：手动备份未被选中", delMixed.IndexOf("dsh-data-20260901-100000") < 0 && delMixed.IndexOf("dsh-data-20260902-100000") < 0);
        Check("非备份前缀不参与", delMixed.IndexOf("not-a-backup-auto") < 0);
        Console.WriteLine("[5] ProfileScanner 块级扫描（逐条对齐 v2.x ProfileCheckText）");
        Check("需要 maxDepth 的插件名（含引号）", ProfileScanner.NeedsMaxDepthPlugin("'@deepseek-ai/dsh-tool-subagent'") && ProfileScanner.NeedsMaxDepthPlugin("@deepseek-ai/dsh-subagent-acp"));
        Check("普通插件不需要 maxDepth", !ProfileScanner.NeedsMaxDepthPlugin("@deepseek-ai/dsh-tool-web"));
        Check("YAML 标量：去引号+去行尾注释", ProfileScanner.CleanYamlScalar("'a-b' # note") == "a-b");
        Check("YAML 标量：引号内 # 不当注释", ProfileScanner.CleanYamlScalar("\"a#b\"") == "a#b");
        Check("像路径判定", ProfileScanner.LooksLikeCommandPath("C:\\x\\y.exe") && !ProfileScanner.LooksLikeCommandPath("justtext"));

        string dashNoMax = "- id: subagent-acp\n  name: '@deepseek-ai/dsh-subagent-acp'\n  config:\n    someKey: 1\n";
        System.Collections.Generic.List<Dsht.Domain.Model.ProfileFinding> fs1 = ProfileScanner.Scan(dashNoMax, "profiles/web/cordis.patch.yml", NoFile);
        Check("dash 形式缺 maxDepth → 1 条发现", fs1.Count == 1 && fs1[0].Missing == "maxDepth");
        Check("行号与 id 正确", fs1.Count == 1 && fs1[0].Line == 1 && fs1[0].Id == "subagent-acp");
        Check("可自动修（对应 FIX 行）", fs1.Count == 1 && fs1[0].AutoFixable);
        Check("Hint 指明 provider-managed", fs1.Count == 1 && fs1[0].Hint.IndexOf("provider-managed") >= 0);

        string withMax = "- id: subagent-acp\n  name: '@deepseek-ai/dsh-subagent-acp'\n  config:\n    maxDepth: 'provider-managed'\n";
        Check("config 块内有 maxDepth → 无发现", ProfileScanner.Scan(withMax, "f", NoFile).Count == 0);

        string maxElsewhere = "- id: subagent-acp\n  name: '@deepseek-ai/dsh-subagent-acp'\n  maxDepth: 8\n";
        Check("maxDepth 出现在别处（非 config 下）→ 无发现（与 v2.x 一致）", ProfileScanner.Scan(maxElsewhere, "f", NoFile).Count == 0);

        string otherPlugin = "- id: web\n  name: '@deepseek-ai/dsh-tool-web'\n";
        Check("非目标插件 → 无发现", ProfileScanner.Scan(otherPlugin, "f", NoFile).Count == 0);

        string topForm = "id: subagent-acp\nname: '@deepseek-ai/dsh-tool-subagent'\n";
        System.Collections.Generic.List<Dsht.Domain.Model.ProfileFinding> fs2 = ProfileScanner.Scan(topForm, "f", NoFile);
        Check("顶层 id: 形式也能识别", fs2.Count == 1 && fs2[0].Id == "subagent-acp");

        string mcpMissing = "- id: mcp\n  name: '@deepseek-ai/dsh-mcp-client'\n  failOnStartupError: true\n  command: C:\\nope\\missing.exe\n";
        System.Collections.Generic.List<Dsht.Domain.Model.ProfileFinding> fs3 = ProfileScanner.Scan(mcpMissing, "f", NoFile);
        Check("mcp command 不存在 → 1 条 command 发现", fs3.Count == 1 && fs3[0].Missing == "command");
        Check("command 类不可自动修（只报不修）", fs3.Count == 1 && !fs3[0].AutoFixable);
        Check("mcp command 存在 → 无发现", ProfileScanner.Scan(mcpMissing, "f", AnyFile).Count == 0);
        Check("mcp 未开 failOnStartupError → 无发现", ProfileScanner.Scan("- id: mcp\n  name: '@deepseek-ai/dsh-mcp-client'\n  command: C:\\nope\\x.exe\n", "f", NoFile).Count == 0);
        Check("空文本 → 无发现", ProfileScanner.Scan("", "f", NoFile).Count == 0);
        Console.WriteLine("[6] 完整性判定与 manifest 解析（逐条对齐 v2.x）");
        string hex64 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        string manifest = "# 头部注释\n\n" + hex64 + "  DeepSeek Harness Toolkit.exe\n" + hex64 + "  README.md\n";
        Check("解析命中（文件名忽略大小写）", ManifestParser.ParseHash(manifest, "deepseek harness toolkit.exe") == hex64);
        Check("解析未命中 → null", ManifestParser.ParseHash(manifest, "other.exe") == null);
        Check("跳过 # 注释与空行", ManifestParser.ParseHash("#x\n\ny\n", "z") == null);
        Check("hash 长度非 64 → null（视为非法）", ManifestParser.ParseHash("abc  f.exe\n", "f.exe") == null);
        Check("非十六进制 → null", ManifestParser.ParseHash(new string('z', 64) + "  f.exe\n", "f.exe") == null);
        Check("空输入 → null", ManifestParser.ParseHash(null, "f.exe") == null && ManifestParser.ParseHash("x", "") == null);

        Check("期望缺失 → Unknown", IntegrityJudge.Judge(null, hex64) == IntegrityVerdict.Unknown);
        Check("实际缺失 → Unknown", IntegrityJudge.Judge(hex64, null) == IntegrityVerdict.Unknown);
        Check("相等 → Match", IntegrityJudge.Judge(hex64, hex64) == IntegrityVerdict.Match);
        Check("忽略大小写 → Match", IntegrityJudge.Judge(hex64.ToUpperInvariant(), hex64) == IntegrityVerdict.Match);
        Check("不等 → Mismatch", IntegrityJudge.Judge(hex64, new string('0', 64)) == IntegrityVerdict.Mismatch);
        Check("只有 Mismatch 拦截高风险操作", IntegrityJudge.ShouldBlock(IntegrityVerdict.Mismatch)
            && !IntegrityJudge.ShouldBlock(IntegrityVerdict.Match) && !IntegrityJudge.ShouldBlock(IntegrityVerdict.Unknown));
        Console.WriteLine("[7] BackupPackage 备份包判定（逐条对齐 v2.x）");
        Check("名字前缀（忽略大小写）", BackupPackage.IsValidBackupName("dsh-data-20260921-193000-auto") && BackupPackage.IsValidBackupName("DSH-DATA-x") && !BackupPackage.IsValidBackupName("other"));
        Check("数据特征：settings.yaml", BackupPackage.HasDshData(new DirSnapshot("d", new string[] { "settings.yaml" })));
        Check("数据特征：credentials/sessions/profiles/storages", BackupPackage.HasDshData(new DirSnapshot("d", new string[] { "credentials.yaml" })) && BackupPackage.HasDshData(new DirSnapshot("d", new string[] { "sessions" })) && BackupPackage.HasDshData(new DirSnapshot("d", new string[] { "profiles" })) && BackupPackage.HasDshData(new DirSnapshot("d", new string[] { "storages" })));
        Check("无特征 → 非数据目录", !BackupPackage.HasDshData(new DirSnapshot("d", new string[] { "readme.txt" })));

        Check("有效包：名字+数据", BackupPackage.IsValidPackage(new DirSnapshot("dsh-data-1", new string[] { "settings.yaml" })));
        Check("有效包：名字+仅 _workspace", BackupPackage.IsValidPackage(new DirSnapshot("dsh-data-1", new string[] { "_workspace" })));
        Check("仅目录存在不算数（空目录）", !BackupPackage.IsValidPackage(new DirSnapshot("dsh-data-1", new string[] { })));
        Check("名字不符 → 无效（即使有数据）", !BackupPackage.IsValidPackage(new DirSnapshot("backup-1", new string[] { "settings.yaml" })));

        DirSnapshot self = new DirSnapshot("mydir", new string[] { "readme.txt" });
        Check("Resolve：自身有效 → 自身", BackupPackage.Resolve(new DirSnapshot("dsh-data-1", new string[] { "sessions" }), null) == "dsh-data-1");
        Check("Resolve：父目录下恰好一个有效备份 → 下探", BackupPackage.Resolve(self, new DirSnapshot[] { new DirSnapshot("dsh-data-1", new string[] { "profiles" }) }) == "dsh-data-1");
        Check("Resolve：两个备份子目录 → null（不猜）", BackupPackage.Resolve(self, new DirSnapshot[] { new DirSnapshot("dsh-data-1", new string[] { "profiles" }), new DirSnapshot("dsh-data-2", new string[] { "sessions" }) }) == null);
        Check("Resolve：唯一子目录但无效 → null", BackupPackage.Resolve(self, new DirSnapshot[] { new DirSnapshot("dsh-data-1", new string[] { "readme.txt" }) }) == null);
        Check("Resolve：无子目录 → null", BackupPackage.Resolve(self, new DirSnapshot[] { }) == null);

        Check("类型判定：手动为默认", BackupPackage.Classify("dsh-data-20260921-193000") == Dsht.Domain.Model.BackupKind.Manual);
        Check("类型判定：-auto", BackupPackage.Classify("dsh-data-x-auto") == Dsht.Domain.Model.BackupKind.Auto);
        Check("类型判定：四种 pre-*", BackupPackage.Classify("x-pre-restore") == Dsht.Domain.Model.BackupKind.PreRestore && BackupPackage.Classify("x-pre-import") == Dsht.Domain.Model.BackupKind.PreImport && BackupPackage.Classify("x-pre-wipe") == Dsht.Domain.Model.BackupKind.PreWipe && BackupPackage.Classify("x-pre-update") == Dsht.Domain.Model.BackupKind.PreUpdate);
        Check("保护性备份（严格模式）仅限 pre-*", BackupPackage.IsProtective(Dsht.Domain.Model.BackupKind.PreWipe) && !BackupPackage.IsProtective(Dsht.Domain.Model.BackupKind.Auto) && !BackupPackage.IsProtective(Dsht.Domain.Model.BackupKind.Manual));
        Console.WriteLine("[8] UptimeFormatter 运行时长（逐字对齐 v2.x FormatUptime）");
        Check("负数 → 0 秒（时钟回拨保护）", UptimeFormatter.Format(TimeSpan.FromSeconds(-5)) == "0 秒");
        Check("30 秒", UptimeFormatter.Format(TimeSpan.FromSeconds(30)) == "30 秒");
        Check("59 秒", UptimeFormatter.Format(TimeSpan.FromSeconds(59)) == "59 秒");
        Check("60 秒 → 1 分", UptimeFormatter.Format(TimeSpan.FromSeconds(60)) == "1 分");
        Check("59 分", UptimeFormatter.Format(TimeSpan.FromMinutes(59)) == "59 分");
        Check("1 小时 30 分", UptimeFormatter.Format(new TimeSpan(1, 30, 0)) == "1 小时 30 分");
        Check("23 小时 59 分", UptimeFormatter.Format(new TimeSpan(0, 23, 59, 0)) == "23 小时 59 分");
        Check("25 小时 → 1 天 1 小时", UptimeFormatter.Format(new TimeSpan(1, 1, 0, 0)) == "1 天 1 小时");
        Check("3 天 5 小时", UptimeFormatter.Format(new TimeSpan(3, 5, 0, 0)) == "3 天 5 小时");
        Console.WriteLine("[9] vendor 路径判定（平台无关）");
        Check("Windows 反斜杠路径命中", ProfileScanner.IsVendorPath("C:\\x\\node_modules\\pkg\\a.yml"));
        Check("Linux 正斜杠路径命中", ProfileScanner.IsVendorPath("/home/u/.dsh/profiles/node_modules/pkg/a.yml"));
        Check("普通路径不命中", !ProfileScanner.IsVendorPath("C:\\x\\profiles\\web\\cordis.patch.yml"));
        Check("大小写不敏感", ProfileScanner.IsVendorPath("C:\\x\\NODE_MODULES\\a.yml"));
        Check("空/空串不命中", !ProfileScanner.IsVendorPath("") && !ProfileScanner.IsVendorPath(null));
        Console.WriteLine("[10] 备份类型显示标签（逐字对齐 v2.x BackupKindName）");
        Check("Manual", BackupPackage.KindLabel(Dsht.Domain.Model.BackupKind.Manual) == "Manual");
        Check("Auto", BackupPackage.KindLabel(Dsht.Domain.Model.BackupKind.Auto) == "Auto");
        Check("PreRestore/PreImport/PreUpdate/PreWipe",
            BackupPackage.KindLabel(Dsht.Domain.Model.BackupKind.PreRestore) == "PreRestore"
            && BackupPackage.KindLabel(Dsht.Domain.Model.BackupKind.PreImport) == "PreImport"
            && BackupPackage.KindLabel(Dsht.Domain.Model.BackupKind.PreUpdate) == "PreUpdate"
            && BackupPackage.KindLabel(Dsht.Domain.Model.BackupKind.PreWipe) == "PreWipe");
        Check("标签与 Classify 往返一致", BackupPackage.KindLabel(BackupPackage.Classify("dsh-data-x-pre-update")) == "PreUpdate");
        Console.WriteLine("[11] doctor 领域纯函数（汇总/级别/大小/脱敏/备份天数）");
        List<DocItem> di = new List<DocItem>();
        Check("空列表 → DOCTOR_OK 0", DoctorSummary.Summary(di) == "DOCTOR_OK 0");
        di.Add(new DocItem("System", 0, "x"));
        Check("全 OK → DOCTOR_OK 0", DoctorSummary.Summary(di) == "DOCTOR_OK 0");
        di.Add(new DocItem("Backup", 1, "y"));
        Check("有 WARN → DOCTOR_WARN 1", DoctorSummary.Summary(di) == "DOCTOR_WARN 1");
        di.Add(new DocItem("Harness", 2, "z"));
        Check("有 ERROR → ERROR 优先", DoctorSummary.Summary(di) == "DOCTOR_ERROR 1");
        Check("级别名", DoctorSummary.Level(0) == "OK" && DoctorSummary.Level(1) == "WARN" && DoctorSummary.Level(2) == "ERROR");

        Check("大小：B", SizeFormatter.Human(512) == "512 B");
        Check("大小：KB 一位小数", SizeFormatter.Human(2048) == "2.0 KB");
        Check("大小：MB 一位小数", SizeFormatter.Human(3 * 1024L * 1024) == "3.0 MB");
        Check("大小：GB 两位小数", SizeFormatter.Human(2 * 1024L * 1024 * 1024) == "2.00 GB");

        Check("脱敏：URL token", ReportSanitizer.Sanitize("http://x/?token=abc123&b=1").IndexOf("abc123") < 0);
        Check("脱敏：key: value", ReportSanitizer.Sanitize("api_key: sk-abcdef").IndexOf("sk-abcdef") < 0);
        Check("脱敏：40+ 位 hex", ReportSanitizer.Sanitize("hash 0123456789abcdef0123456789abcdef01234567").IndexOf("0123456789abcdef") < 0);
        Check("脱敏：普通文本不动", ReportSanitizer.Sanitize("dsh 已安装: C:\\npm\\dsh.cmd") == "dsh 已安装: C:\\npm\\dsh.cmd");

        DateTime now = new DateTime(2026, 9, 28, 12, 0, 0);
        Check("备份天数：3 天前", BackupAge.DaysSince("dsh-data-20260925-120000000-auto", now) == 3);
        Check("备份天数：名字前缀不符 → null", BackupAge.DaysSince("other-20260925-120000000", now) == null);
        Check("备份天数：时间戳非法 → null", BackupAge.DaysSince("dsh-data-notatimestamp", now) == null);
        Console.WriteLine("[12] Linux 平台侧纯逻辑（ss 解析 / 路径解析）");
        string ss = "State  Recv-Q Send-Q Local Address:Port Peer Address:Port Process\n" +
                    "LISTEN 0      511          127.0.0.1:3080      0.0.0.0:*    users:((\"node\",pid=4242,fd=22))\n" +
                    "LISTEN 0      511              [::]:3080         [::]:*    users:((\"node\",pid=4242,fd=23))\n";
        Check("ss 解析命中 pid", Dsht.Platform.Linux.LinuxProcessQuery.ParseSsOutput(ss, 3080) == 4242);
        Check("ss 解析：端口不符 → 0", Dsht.Platform.Linux.LinuxProcessQuery.ParseSsOutput(ss, 9999) == 0);
        Check("ss 解析：空输入 → 0", Dsht.Platform.Linux.LinuxProcessQuery.ParseSsOutput("", 3080) == 0);
        Check("ss 解析：无 pid= → 0", Dsht.Platform.Linux.LinuxProcessQuery.ParseSsOutput("LISTEN 0 511 127.0.0.1:3080 0.0.0.0:*", 3080) == 0);
        Check("Linux 命令行判定（含 dsh）", Dsht.Platform.Linux.LinuxProcessQuery.IsDshCommandLineText("node /usr/lib/node_modules/@deepseek-ai/dsh/lib/bin.js"));
        Check("Linux 命令行判定（无关进程）", !Dsht.Platform.Linux.LinuxProcessQuery.IsDshCommandLineText("nginx: worker process"));

        string oldDshHome = Environment.GetEnvironmentVariable("DSH_HOME");
        try
        {
            Environment.SetEnvironmentVariable("DSH_HOME", "/tmp/dsh-home-probe");
            Check("Linux DataRoot 优先取 DSH_HOME", new Dsht.Platform.Linux.LinuxPaths().DataRoot == "/tmp/dsh-home-probe");
            Environment.SetEnvironmentVariable("DSH_HOME", null);
            string ldr2 = new Dsht.Platform.Linux.LinuxPaths().DataRoot;
            Check("Linux DataRoot 回退到 <home>/.dsh", ldr2 != null && ldr2.EndsWith(".dsh"));
            Check("Linux BackupsRoot = StateDir/backup", new Dsht.Platform.Linux.LinuxPaths().BackupsRoot.EndsWith("backup"));
        }
        finally { Environment.SetEnvironmentVariable("DSH_HOME", oldDshHome); }
        Console.WriteLine("[13] 组合服务目标与预留形态（桌面端驱动的核心语义）");
        FakePort cp = new FakePort(); FakeHttp ch = new FakeHttp(); FakeProc cq = new FakeProc();
        WebTarget web = Make(cp, ch, cq);
        ReservedTarget hd = new ReservedTarget(AppKind.Headless, "test");
        ReservedTarget dt = new ReservedTarget(AppKind.Desktop, "test");
        CompositeServiceTarget comp = new CompositeServiceTarget(new IServiceTarget[] { web, hd, dt });

        cp.Open = true; ch.Ready = true;
        ServiceReport cr1 = comp.Probe();
        Check("web 就绪 → 组合选 web", cr1.Kind == AppKind.Web && cr1.State == ServiceState.Ready);
        Check("组合 Kind 反映所选形态", comp.Kind == AppKind.Web);

        ch.Ready = false; cq.Pid = 9; cq.IsDsh = true;
        Check("web Listening/Ready 优先于预留形态", comp.Probe().Kind == AppKind.Web);

        cp.Open = false;
        ServiceReport cr2 = comp.Probe();
        Check("web 停 + 预留形态全 Down → 报 Unknown/Down（不假装 Ready）", cr2.Kind == AppKind.Unknown && cr2.State == ServiceState.Down);
        Check("组合 Basis 说明已尝试的形态", cr2.Basis.IndexOf("Headless") >= 0 && cr2.Basis.IndexOf("Desktop") >= 0);
        Check("组合 Describe 列出各形态", comp.Describe().IndexOf("dsh web") >= 0 && comp.Describe().IndexOf("Desktop") >= 0);

        Check("预留形态永不 Ready", hd.Probe().State == ServiceState.Down && !hd.IsAvailable());
        Check("预留形态 Basis 说明原因", hd.Probe().Basis.IndexOf("预留") >= 0);
        Check("空组合 → Unknown/Down", new CompositeServiceTarget(new IServiceTarget[0]).Probe().Kind == AppKind.Unknown);
        Console.WriteLine();
        Console.WriteLine("== " + _pass + "/" + (_pass + _fail) + " passed, " + _fail + " failed ==");
        return _fail == 0 ? 0 : 1;
    }
}