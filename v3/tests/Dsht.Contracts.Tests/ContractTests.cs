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
    static bool ExistsTrue(string p) { return true; }
    static EntryLocation Loc7(string path, string entry) { return new EntryLocation("C:\\x\\y.yml", 7); }
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
        Console.WriteLine("[14] 配置解析/序列化/校验（逐条对齐 v2.x）");
        System.Func<string,string> canon = delegate(string wsPath) { if (wsPath.IndexOf('<') >= 0) throw new Exception("bad"); return wsPath; };
        ToolkitConfig def = ConfigCodec.Parse(null, canon);
        Check("空配置 → 默认值", def.Lang == "auto" && def.Host == "127.0.0.1" && def.KeepBackups == 10 && def.CheckUpdate && def.AutoStart && def.UpdateChannel == "stable" && def.CloseAction == "");

        ToolkitConfig c1 = ConfigCodec.Parse("lang=zh\nhost=localhost\nws=C:\\ws\nkeep_backups=2\ncheck_update=off\ncheck_dsh_update=off\nupdate_channel=rc\nclose_action=tray\nauto_start=off\ndsh_versions=1.0,1.1\n", canon);
        Check("逐键解析", c1.Lang == "zh" && c1.Host == "localhost" && c1.Workspace == "C:\\ws" && c1.KeepBackups == 3 && !c1.CheckUpdate && !c1.CheckDshUpdate && c1.UpdateChannel == "rc" && c1.CloseAction == "tray" && !c1.AutoStart && c1.DshVersions == "1.0,1.1");
        Check("非法 lang → auto", ConfigCodec.Parse("lang=xx\n", canon).Lang == "auto");
        Check("非法 close_action → 空（回到未询问）", ConfigCodec.Parse("close_action=whatever\n", canon).CloseAction == "");
        Check("非法 update_channel → stable", ConfigCodec.Parse("update_channel=beta\n", canon).UpdateChannel == "stable");
        Check("非法 host 保留默认", ConfigCodec.Parse("host=evil.com\n", canon).Host == "127.0.0.1");
        Check("ws 规范化失败 → null", ConfigCodec.Parse("ws=a<b\n", canon).Workspace == null);

        string ser = ConfigCodec.Serialize(new ToolkitConfig());
        Check("序列化键顺序与 v2.x 一致", ser.StartsWith("lang=auto\r\nhost=127.0.0.1\r\nws=\r\nkeep_backups=10\r\ncheck_update=on\r\ncheck_dsh_update=on\r\ndsh_versions=\r\nupdate_channel=stable\r\nclose_action=\r\nauto_start=on\r\n"));
        ToolkitConfig rt = ConfigCodec.Parse(ser, canon);
        Check("序列化→解析往返一致", rt.Lang == "auto" && rt.Host == "127.0.0.1" && rt.KeepBackups == 10 && rt.CheckUpdate && rt.AutoStart);

        Check("校验：空键 → no-key", ConfigValidator.Validate("", "x", canon) == "no-key");
        Check("校验：未知键 → unknown-key", ConfigValidator.Validate("dsh_versions", "x", canon) == "unknown-key");
        Check("校验：lang 合法/非法", ConfigValidator.Validate("lang", "en", canon) == null && ConfigValidator.Validate("lang", "fr", canon) == "bad-value");
        Check("校验：keep_backups 必须 ≥3", ConfigValidator.Validate("keep_backups", "2", canon) == "bad-value" && ConfigValidator.Validate("keep_backups", "3", canon) == null);
        Check("校验：ws 走注入的规范化", ConfigValidator.Validate("ws", "a<b", canon) == "bad-value" && ConfigValidator.Validate("ws", "C:\\ok", canon) == null);
        ToolkitConfig ap = ConfigValidator.ApplyTo(new ToolkitConfig(), "keep_backups", "2", canon);
        Check("应用：keep_backups 夹到 3", ap.KeepBackups == 3);
        Console.WriteLine("[15] bootdiag（启动失败堆栈解析，逐条对齐 v2.x）");
        string frag2;
        Check("file:/// URL → 路径 + 片段", Dsht.Domain.Services.FileUrlConverter.ToPath("file:///C:/a/b.yml#ent", out frag2) == "C:\\a\\b.yml" && frag2 == "ent");
        Check("URL 解码 %20", Dsht.Domain.Services.FileUrlConverter.ToPath("file:///C:/a%20b/c.yml", out frag2) == "C:\\a b\\c.yml");
        Check("空 URL → 空", Dsht.Domain.Services.FileUrlConverter.ToPath(null, out frag2) == "" && frag2 == "");

        string yml = "insert:\n  - id: subagent-acp-kimi\n    name: '@deepseek-ai/dsh-subagent-acp'\n";
        Check("EntryLocator 命中行号", Dsht.Domain.Services.EntryLocator.FindLine(yml, "subagent-acp-kimi") == 2);
        Check("EntryLocator 未命中 → 0", Dsht.Domain.Services.EntryLocator.FindLine(yml, "nope") == 0);

        string fail = "Error: plugin tree failed to load\n  failed to apply loader entry include (cordis:include)\n  failed to apply loader entry subagent-acp-kimi (@deepseek-ai/dsh-subagent-acp)\n  provider \"kimi\" cannot enforce maxDepth\n  at file:///C:/x/y.yml#subagent-acp-kimi\n  set maxDepth: 'provider-managed'\n";
        BootDiagResult br = Dsht.Domain.Services.BootDiagParser.Parse(fail, Loc7, ExistsTrue);
        Check("识别 + Kind=maxDepth-missing", br.Recognized && br.Kind == "maxDepth-missing");
        Check("取带 @ 的包名（最内层）", br.Plugin == "@deepseek-ai/dsh-subagent-acp" && br.Entry == "subagent-acp-kimi");
        Check("Hint 取自输出", br.Hint == "set maxDepth: 'provider-managed'");
        Check("FILE/LINE 来自定位结果", br.File == "C:\\x\\y.yml" && br.Line == 7);

        string unknown = "some random crash\nError: boom\n";
        BootDiagResult bu = Dsht.Domain.Services.BootDiagParser.Parse(unknown, null, null);
        Check("未识别 → Recognized=false 且 KIND unknown", !bu.Recognized && bu.Kind == "unknown");
        Check("未识别时 FirstError 被首个 Error 行覆盖（与 v2.x 两阶段行为一致）", bu.FirstError == "Error: boom");

        string pkgOnly = "plugin tree failed to load\n  failed to apply loader entry e1 (plain-plugin)\n";
        BootDiagResult bp = Dsht.Domain.Services.BootDiagParser.Parse(pkgOnly, null, null);
        Check("无 @ 包名 → 退回最后一个匹配", bp.Recognized && bp.Plugin == "plain-plugin" && bp.Entry == "e1");
        Check("无 set maxDepth → 默认提示", bp.Hint == "set maxDepth: 'provider-managed'");
        Check("TextClipper 短串不变", Dsht.Domain.Services.TextClipper.Clip("abc", 200) == "abc");
        Console.WriteLine("[16] dryrun 领域纯函数（路径判定 / 跳过规则 / 合并计划）");
        Check("TrimTrailingSep：普通目录", Dsht.Domain.Services.PathUtil.TrimTrailingSep("C:\\a\\b\\") == "C:\\a\\b");
        Check("TrimTrailingSep：盘根保留", Dsht.Domain.Services.PathUtil.TrimTrailingSep("D:\\") == "D:\\");
        Check("IsSubPath：子树内", Dsht.Domain.Services.PathUtil.IsSubPath("C:\\bk", "C:\\bk\\dsh-data-1"));
        Check("IsSubPath：相等算在内", Dsht.Domain.Services.PathUtil.IsSubPath("C:\\bk\\", "C:\\bk"));
        Check("IsSubPath：外部不算", !Dsht.Domain.Services.PathUtil.IsSubPath("C:\\bk", "C:\\other"));
        Check("IsSubPath：大小写不敏感", Dsht.Domain.Services.PathUtil.IsSubPath("C:\\BK", "c:\\bk\\x"));

        Check("跳过：node_modules", Dsht.Domain.Services.SkipRules.SkipDir("node_modules", false));
        Check("跳过：backup（防自嵌套）", Dsht.Domain.Services.SkipRules.SkipDir("backup", false));
        Check("跳过：dsh-data-*（防嵌套备份）", Dsht.Domain.Services.SkipRules.SkipDir("dsh-data-20260101-000000-auto", false));
        Check("跳过：reparse point", Dsht.Domain.Services.SkipRules.SkipDir("normal", true));
        Check("不跳过：普通目录", !Dsht.Domain.Services.SkipRules.SkipDir("sessions", false));

        System.Collections.Generic.Dictionary<string, long> sm = new System.Collections.Generic.Dictionary<string, long>();
        sm["a.txt"] = 10; sm["sub/b.txt"] = 20; sm["sub/c.txt"] = 30;
        System.Collections.Generic.Dictionary<string, long> dm = new System.Collections.Generic.Dictionary<string, long>();
        dm["a.txt"] = 5; dm["only-here.txt"] = 99;
        long[] pl = Dsht.Domain.Services.MergePlanner.Plan(sm, dm);
        Check("计划：新增 2 覆盖 1 保留 1 字节 60", pl[0] == 2 && pl[1] == 1 && pl[2] == 1 && pl[3] == 60);
        long[] pl2 = Dsht.Domain.Services.MergePlanner.Plan(null, dm);
        Check("计划：空源 → 全保留、0 字节", pl2[0] == 0 && pl2[1] == 0 && pl2[2] == 2 && pl2[3] == 0);
        long[] pl3 = Dsht.Domain.Services.MergePlanner.Plan(sm, null);
        Check("计划：空目标 → 全新", pl3[0] == 3 && pl3[1] == 0 && pl3[2] == 0 && pl3[3] == 60);
        Console.WriteLine("[17] 版本号处理（对齐 v2.x；含命令注入白名单）");
        Check("Core：去预发布段", Dsht.Domain.Services.VersionComparer.Core("0.1.1-rc.2") == "0.1.1");
        Check("IsClean：1-3 段数字", Dsht.Domain.Services.VersionComparer.IsClean("2.7.2") && Dsht.Domain.Services.VersionComparer.IsClean("2") && !Dsht.Domain.Services.VersionComparer.IsClean("2.7.2.1") && !Dsht.Domain.Services.VersionComparer.IsClean("2.x"));
        Check("白名单：合法版本", Dsht.Domain.Services.VersionComparer.IsValidNpmVersion("0.1.5-rc.2") && Dsht.Domain.Services.VersionComparer.IsValidNpmVersion("2.7.2"));
        Check("白名单：拒绝命令注入字符", !Dsht.Domain.Services.VersionComparer.IsValidNpmVersion("1.0.0 & del x") && !Dsht.Domain.Services.VersionComparer.IsValidNpmVersion("1.0.0;rm") && !Dsht.Domain.Services.VersionComparer.IsValidNpmVersion("1.0.0|x") && !Dsht.Domain.Services.VersionComparer.IsValidNpmVersion("1.0.0$(x)") && !Dsht.Domain.Services.VersionComparer.IsValidNpmVersion("1.0.0\"x"));
        Check("白名单：只允许一个 -", !Dsht.Domain.Services.VersionComparer.IsValidNpmVersion("1.0.0-rc-2"));
        Check("Sanitize：去前导 v", Dsht.Domain.Services.VersionComparer.SanitizeLatest("v2.7.2") == "2.7.2");
        Check("Sanitize：非法 → null", Dsht.Domain.Services.VersionComparer.SanitizeLatest("1.0.0 && x") == null && Dsht.Domain.Services.VersionComparer.SanitizeLatest("") == null);
        Check("比较：核心段", Dsht.Domain.Services.VersionComparer.Compare("2.7.2", "2.7.1") > 0 && Dsht.Domain.Services.VersionComparer.Compare("2.7.2", "2.7.2") == 0);
        Check("比较：正式版高于预发布", Dsht.Domain.Services.VersionComparer.Compare("2.1.2", "2.1.2-rc") > 0);
        Check("比较：rc.1 < rc.2 < rc.10（数字段数值序）", Dsht.Domain.Services.VersionComparer.Compare("0.1.5-rc.1", "0.1.5-rc.2") < 0 && Dsht.Domain.Services.VersionComparer.Compare("0.1.5-rc.2", "0.1.5-rc.10") < 0);
        Check("比较：缺段更低（rc < rc.1）", Dsht.Domain.Services.VersionComparer.Compare("1.0.0-rc", "1.0.0-rc.1") < 0);
        Check("比较：段数不齐按 0 补", Dsht.Domain.Services.VersionComparer.Compare("1.0", "1.0.0") == 0);
        Console.WriteLine("[18] 路径校验（restore/export/delete，对齐 v2.x 原因键）");
        System.Func<string,bool> valid = delegate(string vp) { return true; };
        System.Func<string,bool> invalid = delegate(string vp) { return false; };
        System.Func<string,bool> exists = delegate(string vp) { return true; };
        System.Func<string,string> full = delegate(string vp) { return vp; };
        Check("restore：空 → no-path", Dsht.Domain.Services.PathValidator.ValidateRestorePath("", "C:\\bk", valid) == "no-path");
        Check("restore：根外 → outside", Dsht.Domain.Services.PathValidator.ValidateRestorePath("C:\\other", "C:\\bk", valid) == "outside");
        Check("restore：无效 → invalid", Dsht.Domain.Services.PathValidator.ValidateRestorePath("C:\\bk\\x", "C:\\bk", invalid) == "invalid");
        Check("restore：通过 → null", Dsht.Domain.Services.PathValidator.ValidateRestorePath("C:\\bk\\dsh-data-1", "C:\\bk", valid) == null);
        Check("restore：去引号", Dsht.Domain.Services.PathValidator.ValidateRestorePath("\"C:\\bk\\dsh-data-1\"", "C:\\bk", valid) == null);
        Check("export：无目标 → no-to", Dsht.Domain.Services.PathValidator.ValidateExport("C:\\bk\\dsh-data-1", "", "C:\\bk", exists, full) == "no-to");
        Check("export：嵌套目标 → nested", Dsht.Domain.Services.PathValidator.ValidateExport("C:\\bk\\dsh-data-1", "C:\\bk\\dsh-data-1\\sub", "C:\\bk", exists, full) == "nested");
        Check("export：目标等于源 → nested", Dsht.Domain.Services.PathValidator.ValidateExport("C:\\bk\\dsh-data-1", "C:\\bk\\dsh-data-1", "C:\\bk", exists, full) == "nested");
        Check("delete：非备份名 → not-backup", Dsht.Domain.Services.PathValidator.ValidateDeletePath("C:\\bk\\other", "C:\\bk", exists) == "not-backup");
        Check("delete：不存在 → not-found", Dsht.Domain.Services.PathValidator.ValidateDeletePath("C:\\bk\\dsh-data-1", "C:\\bk", invalid) == "not-found");
        Check("delete：通过 → null", Dsht.Domain.Services.PathValidator.ValidateDeletePath("C:\\bk\\dsh-data-1", "C:\\bk", exists) == null);
        Console.WriteLine("[19] Windows 数据根支持 DSH_HOME（隔离测试与多环境部署的前提）");
        string oldWinHome = Environment.GetEnvironmentVariable("DSH_HOME");
        try
        {
            Environment.SetEnvironmentVariable("DSH_HOME", @"C:\tmp\v3-win-home");
            Check("Windows DataRoot 优先取 DSH_HOME", new Dsht.Platform.Windows.WindowsPaths().DataRoot == @"C:\tmp\v3-win-home");
            Check("BackupsRoot 跟随数据根？不——跟随状态目录（与 v2.x 一致）", new Dsht.Platform.Windows.WindowsPaths().BackupsRoot.EndsWith("backup"));
            Environment.SetEnvironmentVariable("DSH_HOME", null);
            string wdr = new Dsht.Platform.Windows.WindowsPaths().DataRoot;
            Check("未设置时回退到 <home>/.dsh（与 v2.x 一致）", wdr != null && wdr.EndsWith(".dsh"));
        }
        finally { Environment.SetEnvironmentVariable("DSH_HOME", oldWinHome); }
        Console.WriteLine("[20] restore --apply 准入（V3 独有：真实写盘只允许隔离数据根）");
        string[] defs = new string[] { @"C:\Users\u\.dsh", @"C:\Users\u\AppData\Roaming\.dsh", @"C:\Users\u\AppData\Local\.dsh" };
        Check("未给 --apply → 判定不介入（放行）", Dsht.Domain.Services.RestoreApplyPolicy.Judge(false, null, null, defs) == null);
        Check("--apply + 未设 $DSH_HOME → apply-needs-dsh-home", Dsht.Domain.Services.RestoreApplyPolicy.Judge(true, null, @"D:\iso\data", defs) == Dsht.Domain.Services.RestoreApplyPolicy.NeedsDshHome);
        Check("--apply + $DSH_HOME 空白 → apply-needs-dsh-home", Dsht.Domain.Services.RestoreApplyPolicy.Judge(true, "   ", @"D:\iso\data", defs) == Dsht.Domain.Services.RestoreApplyPolicy.NeedsDshHome);
        Check("--apply + 数据根为空 → apply-needs-dsh-home", Dsht.Domain.Services.RestoreApplyPolicy.Judge(true, @"D:\iso\data", "  ", defs) == Dsht.Domain.Services.RestoreApplyPolicy.NeedsDshHome);
        Check("--apply + 数据根=默认位置 → apply-not-isolated", Dsht.Domain.Services.RestoreApplyPolicy.Judge(true, @"C:\Users\u\.dsh", @"C:\Users\u\.dsh", defs) == Dsht.Domain.Services.RestoreApplyPolicy.NotIsolated);
        Check("--apply + 默认位置（大小写/尾分隔符不敏感）→ apply-not-isolated", Dsht.Domain.Services.RestoreApplyPolicy.Judge(true, @"C:\USERS\U\.DSH\", @"c:\users\u\.dsh", defs) == Dsht.Domain.Services.RestoreApplyPolicy.NotIsolated);
        Check("--apply + 默认位置的子目录 → 放行（不等于默认位置本身）", Dsht.Domain.Services.RestoreApplyPolicy.Judge(true, @"C:\Users\u\.dsh\sandbox", @"C:\Users\u\.dsh\sandbox", defs) == null);
        Check("--apply + 隔离数据根 → 放行", Dsht.Domain.Services.RestoreApplyPolicy.Judge(true, @"D:\iso\data", @"D:\iso\data", defs) == null);
        Check("--apply + 候选表为 null → 放行（无默认位置可判）", Dsht.Domain.Services.RestoreApplyPolicy.Judge(true, @"D:\iso\data", @"D:\iso\data", null) == null);
        Check("文案：中文 needs-dsh-home 提到 DSH_HOME", Dsht.Domain.Services.RestoreApplyPolicy.Message(Dsht.Domain.Services.RestoreApplyPolicy.NeedsDshHome, true).IndexOf("DSH_HOME") >= 0);
        Check("文案：英文 not-isolated 提到 isolated", Dsht.Domain.Services.RestoreApplyPolicy.Message(Dsht.Domain.Services.RestoreApplyPolicy.NotIsolated, false).IndexOf("isolated") >= 0);
        Check("文案：未知原因码有兜底", Dsht.Domain.Services.RestoreApplyPolicy.Message("whatever", true).Length > 0 && Dsht.Domain.Services.RestoreApplyPolicy.Message("whatever", false).Length > 0);
        string[] wdefs = Dsht.Platform.Windows.WindowsPaths.DefaultDataRoots();
        bool wdefsOk = wdefs.Length == 3;
        for (int i = 0; i < wdefs.Length; i++) if (!wdefs[i].EndsWith(".dsh")) wdefsOk = false;
        Check("Windows 默认数据根候选：3 个且都以 .dsh 结尾", wdefsOk);
        string[] ldefs = Dsht.Platform.Linux.LinuxPaths.DefaultDataRoots();
        Check("Linux 默认数据根候选：至少 1 个且以 .dsh 结尾", ldefs.Length >= 1 && ldefs[0].EndsWith(".dsh"));
        Console.WriteLine("[21] doctor --report 组装（纯函数，格式对齐 v2.x 的 ConfigSummary/LogSummary/报告）");
        Check("配置摘要：null → (无配置文件)", Dsht.Domain.Services.ConfigSummaryBuilder.Build(null) == "(无配置文件)");
        Check("配置摘要：只有注释与空行 → (空配置)", Dsht.Domain.Services.ConfigSummaryBuilder.Build("# c\r\n\r\n   \r\n") == "(空配置)");
        Check("配置摘要：非注释行以 ' ; ' 连接", Dsht.Domain.Services.ConfigSummaryBuilder.Build("# c\nlang=auto\nhost=127.0.0.1\n") == "lang=auto ; host=127.0.0.1");
        Check("配置摘要：行首尾空白去掉", Dsht.Domain.Services.ConfigSummaryBuilder.Build("  a=1  \n") == "a=1");
        Check("配置摘要：空文本 → (空配置)", Dsht.Domain.Services.ConfigSummaryBuilder.Build("") == "(空配置)");
        Check("日志摘要：null → (无日志)", Dsht.Domain.Services.LogSummaryBuilder.Build(null) == "(无日志)");
        Check("日志摘要：空文本 → 共 0 行（对齐 File.ReadAllLines）", Dsht.Domain.Services.LogSummaryBuilder.Build("") == "共 0 行；最近: ");
        Check("日志摘要：末尾换行不产生额外空行", Dsht.Domain.Services.LogSummaryBuilder.Build("a\nb\n") == "共 2 行；最近: a | b");
        Check("日志摘要：只保留最近 3 行", Dsht.Domain.Services.LogSummaryBuilder.Build("1\n2\n3\n4\n") == "共 4 行；最近: 2 | 3 | 4");
        Check("日志摘要：CRLF 也按行拆", Dsht.Domain.Services.LogSummaryBuilder.Build("x\r\ny\r\n") == "共 2 行；最近: x | y");
        List<DocItem> ritems = new List<DocItem>();
        ritems.Add(new DocItem("A", 0, "ok"));
        ritems.Add(new DocItem("A", 2, "bad"));
        ritems.Add(new DocItem("B", 1, "warn"));
        string rep = Dsht.Domain.Services.DoctorReport.Build("2026-09-28 12:00:00", "3.0.0-dev", "SYS", ritems, "cfg", "log", "DOCTOR_WARN 1");
        Check("报告：头部三行（时间/Toolkit/系统）", rep.IndexOf("生成时间: 2026-09-28 12:00:00") >= 0 && rep.IndexOf("Toolkit : 3.0.0-dev") >= 0 && rep.IndexOf("系统    : SYS") >= 0);
        Check("报告：分类小节标题只在换类时出现一次", rep.Split(new string[] { "-- A --" }, StringSplitOptions.None).Length - 1 == 1 && rep.IndexOf("-- B --") >= 0);
        Check("报告：条目带 [级别] 前缀", rep.IndexOf("  [OK] ok") >= 0 && rep.IndexOf("  [ERROR] bad") >= 0 && rep.IndexOf("  [WARN] warn") >= 0);
        Check("报告：含配置/日志摘要小节与结果行", rep.IndexOf("-- 配置摘要（脱敏） --") >= 0 && rep.IndexOf("-- 日志摘要（脱敏） --") >= 0 && rep.IndexOf("结果: DOCTOR_WARN 1") >= 0);
        Check("报告：条目与摘要都脱敏", Dsht.Domain.Services.DoctorReport.Build("t", "v", "s", null, "token=SECRET", "token=SECRET", "x").IndexOf("SECRET") < 0);
        Check("报告：items 为 null 也不炸", Dsht.Domain.Services.DoctorReport.Build("t", "v", "s", null, "c", "l", "x").IndexOf("-- 配置摘要（脱敏） --") >= 0);
        Console.WriteLine("[22] 工作区自动探测判定与解析（对齐 v2.x 的 LooksLikeWorkspace / WorkspaceRoot）");
        string uhome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] forb = Dsht.Platform.Windows.WindowsPaths.ForbiddenWorkspaceRoots();
        Check("拒绝：盘根 C:\\", !Dsht.Domain.Services.WorkspaceJudge.LooksLike(@"C:\", forb));
        Check("拒绝：用户主目录", !Dsht.Domain.Services.WorkspaceJudge.LooksLike(uhome, forb));
        Check("拒绝：主目录子树（Desktop）", !Dsht.Domain.Services.WorkspaceJudge.LooksLike(System.IO.Path.Combine(uhome, "Desktop"), forb));
        Check("拒绝：C:\\Users 整级", !Dsht.Domain.Services.WorkspaceJudge.LooksLike(System.IO.Path.GetDirectoryName(uhome), forb));
        Check("拒绝：Program Files", !Dsht.Domain.Services.WorkspaceJudge.LooksLike(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), forb));
        Check("拒绝：盘根保留名 $Recycle.Bin（大小写不敏感）", !Dsht.Domain.Services.WorkspaceJudge.LooksLike(@"D:\$RECYCLE.BIN", forb));
        Check("拒绝：盘根保留名 users", !Dsht.Domain.Services.WorkspaceJudge.LooksLike(@"D:\Users", forb));
        Check("接受：普通工作区目录", Dsht.Domain.Services.WorkspaceJudge.LooksLike(@"D:\work\myproj", forb));
        Check("接受：UNC 共享下的目录", Dsht.Domain.Services.WorkspaceJudge.LooksLike(@"\\nas\share\team", forb));
        Check("接受：UNC 根（第一段是服务器名，不误伤）", Dsht.Domain.Services.WorkspaceJudge.LooksLike(@"\\server\share", forb));
        Check("拒绝：空串与 null", !Dsht.Domain.Services.WorkspaceJudge.LooksLike("", forb) && !Dsht.Domain.Services.WorkspaceJudge.LooksLike(null, forb));
        System.Func<string, string> ident = delegate(string vp) { return vp; };
        System.Func<string, string> prefix = delegate(string vp) { return @"D:\abs\" + vp; };
        System.Func<string, string> boom = delegate(string vp) { throw new Exception("boom"); };
        System.Func<string, bool> yes = delegate(string vp) { return true; };
        System.Func<string, bool> no = delegate(string vp) { return false; };
        Check("解析：未配置 → 用自动探测结果", Dsht.Domain.Services.WorkspaceResolver.Resolve(null, @"D:\detected", ident, yes) == @"D:\detected");
        Check("解析：配置为空串 → 用自动探测结果", Dsht.Domain.Services.WorkspaceResolver.Resolve("", @"D:\detected", ident, yes) == @"D:\detected");
        Check("解析：配置了且存在 → 用配置（经绝对化）", Dsht.Domain.Services.WorkspaceResolver.Resolve(@"ws\sub", @"D:\detected", prefix, yes) == @"D:\abs\ws\sub");
        Check("解析：配置了但不存在 → null（不回退探测）", Dsht.Domain.Services.WorkspaceResolver.Resolve(@"D:\nope", @"D:\detected", ident, no) == null);
        Check("解析：绝对化抛异常 → null", Dsht.Domain.Services.WorkspaceResolver.Resolve(@"bad", @"D:\detected", boom, yes) == null);
        Console.WriteLine();
        Console.WriteLine("== " + _pass + "/" + (_pass + _fail) + " passed, " + _fail + " failed ==");
        return _fail == 0 ? 0 : 1;
    }
}