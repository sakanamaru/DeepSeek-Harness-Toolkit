using System;
using System.Collections.Generic;
using Dsht.Domain.Abstractions;
using Dsht.Domain.Model;
using Dsht.Domain.Services;
using Dsht.Domain.Targets;
using Dsht.Platform.Windows;

using System.Reflection;

[assembly: AssemblyTitle("DeepSeek Harness Toolkit V3")]
[assembly: AssemblyDescription("DeepSeek Harness(dsh) 安装/启动/卸载/备份恢复工具箱。v1: SOGR-Momono Dango(QwenPaw/DeepseekAPI-V4-Flash-0731)；v2: DeepSeek DSH(DSH/DeepseekAPI-V4-Flash-0731)；GitHub @sakanamaru")]
[assembly: AssemblyCompany("SOGR-Momono Dango / DeepSeek DSH / @sakanamaru")]
[assembly: AssemblyProduct("DeepSeek Harness Toolkit")]
[assembly: AssemblyVersion("3.0.0.0")]
[assembly: AssemblyFileVersion("3.0.0.0")]
namespace Dsht.Cli
{
    /// <summary>V3 CLI 组合根 + 命令面。每个命令的标记行都要与 v2.x 逐字一致（见 v3/tests/compare_markers.ps1）。</summary>
    public static class Program
    {
        private const int WebPort = 3080;
        private const string WebUrl = "http://127.0.0.1:3080";

        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = new System.Text.UTF8Encoding(false); } catch { }
            ServiceRegistry reg = Compose();
            _cfg = LoadConfig(reg);
            string cmd = args.Length > 0 ? args[0] : "";

            if (cmd == "status") return Status(reg, Has(args, "--detail"));
            if (cmd == "describe") return Describe(reg);
            if (cmd == "profilecheck") return ProfileCheck(args, reg);
            if (cmd == "profiles") return Profiles(reg);
            if (cmd == "sessions") return Sessions(reg);
            if (cmd == "backup-list") return BackupList(args, reg);
            if (cmd == "doctor") return Doctor(args, reg);
            if (cmd == "version") { Console.WriteLine("DSHT_VERSION " + ToolkitVersion); return 0; }
            if (cmd == "config-get") return ConfigGet();
            if (cmd == "config-set") return ConfigSet(args, reg);
            if (cmd == "bootdiag") return BootDiag(args, reg);
            if (cmd == "restore") return Restore(args, reg);
            if (cmd == "selftest") return SelfTest(args, reg);
            if (cmd == "check") return Check(reg);
            if (cmd == "backup") return Backup(reg);
            if (cmd == "backup-export") return BackupExport(args, reg);
            if (cmd == "backup-delete") return BackupDelete(args, reg);

            Console.WriteLine("usage: dsht status [--detail] | describe | profilecheck [...] | profiles | sessions | backup-list [--detail] | doctor [--report <file>] | version | config-get | config-set <key> <value> | bootdiag --from <file> | restore --dry-run [--path <backup>] | restore [--path <backup>] [--apply] | selftest [<report>] | check | backup | backup-export --path <bk> --to <dir> | backup-delete --path <bk>");
            return 2;
        }

        /// <summary>服务三态 + detail 三行。逐条对齐 v2.x 的 StatusCli。</summary>
        private static int Status(ServiceRegistry reg, bool detail)
        {
            ServiceReport r = reg.Get<IServiceTarget>().Probe();
            Console.WriteLine(r.StatusMarker);
            if (!detail) return 0;
            Console.WriteLine("STATUS_PID " + (r.Pid > 0 ? r.Pid.ToString() : "0"));
            bool haveStart = false;
            DateTime start = DateTime.MinValue;
            if (r.Pid > 0)
            {
                DateTime? s = reg.Get<IProcessQuery>().StartTime(r.Pid);
                if (s.HasValue) { start = s.Value; haveStart = true; }
            }
            Console.WriteLine("STATUS_START " + (haveStart ? start.ToString("yyyy-MM-dd HH:mm:ss") : ""));
            Console.WriteLine("STATUS_UPTIME " + (haveStart ? UptimeFormatter.Format(DateTime.Now - start) : ""));
            return 0;
        }

        private static int Describe(ServiceRegistry reg)
        {
            IServiceTarget target = reg.Get<IServiceTarget>();
            Console.WriteLine(target.Describe());
            Console.WriteLine("basis: " + target.Probe().Basis);
            return 0;
        }

        /// <summary>profilecheck：标记行逐条对齐 v2.x 的 ProfileCheckCli。</summary>
        private static int ProfileCheck(string[] args, ServiceRegistry reg)
        {
            string dir = Flag(args, "--dir");
            string one = Flag(args, "--file");
            bool vendor = Has(args, "--vendor");
            bool abs = Has(args, "--abs");
            IProfileSource src = reg.Get<IProfileSource>();
            List<ProfileFinding> fs = new List<ProfileFinding>();
            int files = 0, skipped = 0;
            if (!string.IsNullOrEmpty(one))
            {
                ProfileFile pf = src.ReadSingle(one, abs);
                if (pf != null) { files = 1; fs.AddRange(ProfileScanner.Scan(pf.Text, pf.Label, FileExists)); }
            }
            else
            {
                ProfileCollection col = src.CollectDirectory(dir, vendor, abs);
                skipped = col.SkippedVendor;
                foreach (ProfileFile pf in col.Files)
                {
                    files++;
                    fs.AddRange(ProfileScanner.Scan(pf.Text, pf.Label, FileExists));
                }
            }
            foreach (ProfileFinding f in fs)
                Console.WriteLine("PROFILECHK_WARN " + f.File + " " + f.Line + " " + f.Id + " " + f.Missing + " " + f.Hint);
            Console.WriteLine("PROFILECHK_TOTAL " + fs.Count + " " + files);
            if (skipped > 0) Console.WriteLine("PROFILECHK_SKIPPED_VENDOR " + skipped);
            if (abs)
            {
                foreach (ProfileFinding f in fs)
                    if (f.Missing == "maxDepth") Console.WriteLine("PROFILECHK_FIX " + f.File + "|" + f.Line + "|" + f.Id + "|" + f.Missing);
            }
            if (fs.Count == 0) Console.WriteLine("PROFILECHK_OK");
            return 0;
        }


        /// <summary>sessions（V3 独有）：会话 / token / 缓存 面板的数据源。**只读** dsh 的会话投影（明文 JSON）。
        /// 来源优先级：插件快照（存在时）→ 每会话投影文件 → 投影总表。
        /// 标记行：
        ///   `SESSIONS_OK &lt;n&gt;` / `SESSIONS_NONBLANK &lt;n&gt;` / `SESSIONS_SOURCE &lt;snapshot|disk|aggregate&gt;` / `SESSIONS_ROOT &lt;dir&gt;`
        ///   / 每会话 `SESSION &lt;id&gt; last=&lt;t|unknown&gt; turns= steps= in= out= cacheRead= hit=&lt;%|unknown&gt; decode=&lt;tok/s|unknown&gt; ttft=&lt;ms|unknown&gt; ctx=&lt;%|unknown&gt; blank=0|1`
        ///   / `SESSIONS_TOTAL in= out= cacheRead= hit=&lt;%|unknown&gt; decode=&lt;tok/s|unknown&gt;` / `SESSIONS_FAIL &lt;原因&gt;`
        /// **诚实边界**：只读计数/时间/元数据，**不读对话正文**；字段缺失打印 `unknown`（不假装 0）；
        /// "有几个会话在运行"这里只能给**最后活动时间**——运行态是进程内事实，需要插件。</summary>
        private static int Sessions(ServiceRegistry reg)
        {
            ISessionStatsSource src = reg.Get<ISessionStatsSource>();
            List<SessionStat> list = new List<SessionStat>();
            string source = "disk";
            string snap = src.ReadText(src.SnapshotPath);
            if (snap != null)
            {
                SessionStat[] fromSnap = SessionStats.ParseSnapshot(snap);
                if (fromSnap.Length > 0) { list.AddRange(fromSnap); source = "snapshot"; }
            }
            if (list.Count == 0)
            {
                string[] files = src.ListSessionFiles();
                for (int i = 0; i < files.Length; i++)
                {
                    string id = System.IO.Path.GetFileNameWithoutExtension(files[i]);
                    if (id != null && id.StartsWith("session-", StringComparison.Ordinal)) id = id.Substring("session-".Length);
                    SessionStat s = SessionStats.ParseSessionProjection(src.ReadText(files[i]), id);
                    if (s != null) list.Add(s);
                }
                if (list.Count == 0)
                {
                    string agg = src.ReadText(src.AggregatePath);
                    if (agg != null)
                    {
                        SessionStat[] a = SessionStats.ParseAggregate(agg);
                        if (a.Length > 0) { list.AddRange(a); source = "aggregate"; }
                    }
                }
            }
            if (list.Count == 0)
            {
                Console.WriteLine("SESSIONS_FAIL " + T("没有可读的会话投影（dsh 未初始化，或该 dsh 版本的投影格式不认）",
                    "no readable session projection (dsh not initialized, or an unrecognized projection format)"));
                return 0;
            }
            SessionTotals tot = SessionStats.Aggregate(list);
            int liveCount = 0;
            for (int i = 0; i < list.Count; i++) if (list[i].Live) liveCount++;
            Console.WriteLine("SESSIONS_OK " + list.Count);
            Console.WriteLine("SESSIONS_NONBLANK " + tot.NonBlankCount);
            Console.WriteLine("SESSIONS_LIVE " + liveCount);   // 只在有插件快照时可能 > 0（磁盘投影没有"在跑"这个事实）
            Console.WriteLine("SESSIONS_SOURCE " + source);
            Console.WriteLine("SESSIONS_ROOT " + src.SessionsDir);
            for (int i = 0; i < list.Count; i++)
            {
                SessionStat s = list[i];
                Console.WriteLine("SESSION " + s.Id
                    + " title=" + MarkerText.Encode(s.Title)
                    + " created=" + (string.IsNullOrEmpty(s.CreatedAt) ? "unknown" : s.CreatedAt)
                    + " last=" + (string.IsNullOrEmpty(s.LastPromptAt) ? "unknown" : s.LastPromptAt)
                    + " turns=" + s.Turns
                    + " steps=" + s.Steps
                    + " in=" + s.TotalInputTokens
                    + " out=" + s.OutputTokens
                    + " cacheRead=" + s.CacheReadTokens
                    + " hit=" + Num1(SessionStats.CacheHitPercent(s))
                    + " decode=" + Num1(SessionStats.DecodeTokensPerSec(s))
                    + " ttft=" + (s.TtftMs > 0 ? s.TtftMs.ToString(System.Globalization.CultureInfo.InvariantCulture) : "unknown")
                    + " ctx=" + Num1(SessionStats.ContextPressurePercent(s))
                    + " blank=" + (s.Blank ? "1" : "0")
                    + " live=" + (s.Live ? "1" : "0"));
            }
            Console.WriteLine("SESSIONS_TOTAL in=" + (tot.UncachedInputTokens + tot.CacheReadTokens)
                + " out=" + tot.OutputTokens
                + " cacheRead=" + tot.CacheReadTokens
                + " hit=" + Num1(tot.CacheHitPercent)
                + " decode=" + Num1(tot.DecodeTokensPerSec));
            return 0;
        }

        /// <summary>派生指标格式化：未知（-1）→ `unknown`，否则一位小数；**固定 InvariantCulture**（标记行必须机器可读，不受区域设置影响）。</summary>
        private static string Num1(double v)
        {
            return v < 0 ? "unknown" : v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>profiles（V3 独有）：列出 profile、它们的**配置形态**与插件清单。
        /// 数据来源：`&lt;数据根&gt;/profiles/&lt;name&gt;/package.json` 里的 `dsh.profile.bundles`（明文小 JSON，只读零注入）。
        /// 标记行：
        ///   `PROFILES_OK &lt;n&gt;` / `PROFILE &lt;name&gt; form=&lt;web|headless|acp|unknown|unparsed&gt; bundles=&lt;n&gt; thirdparty=&lt;m&gt;`
        ///   / `BUNDLE &lt;profile&gt; &lt;bundle-id&gt; &lt;official|thirdparty&gt;` / `PROFILES_FAIL &lt;原因&gt;`
        /// **诚实边界**：这是**配置形态**（manifest 里启用了哪个 app bundle），**不是运行形态**——
        /// "dsh 在跑"仍必须由端口/进程等运行时事实判断（见 describe/status）。</summary>
        private static int Profiles(ServiceRegistry reg)
        {
            IProfileManifestSource src = reg.Get<IProfileManifestSource>();
            if (!reg.Get<IFileSystemQuery>().DirectoryExists(src.ProfilesRoot))
            {
                Console.WriteLine("PROFILES_FAIL " + T("找不到 profiles 目录", "profiles directory not found"));
                return 0;
            }
            string[] names = src.ListProfiles();
            List<string[]> rows = new List<string[]>();
            List<string[]> bundles = new List<string[]>();
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                if (string.Equals(name, "node_modules", StringComparison.OrdinalIgnoreCase)) continue;   // 插件安装目录，不是 profile
                string manifestText = src.ReadManifest(name);
                if (manifestText == null) continue;                    // 没有 package.json → 不是 profile（诚实跳过，不报噪声）
                ProfileManifestInfo info = ProfileManifest.Parse(manifestText);
                string form = info.Parsed ? ProfileManifest.FormName(info.ConfiguredForm) : "unparsed";
                rows.Add(new string[] { name, form, info.Bundles.Length.ToString(), info.ThirdPartyPlugins.Length.ToString() });
                for (int b = 0; b < info.Bundles.Length; b++)
                {
                    string id = info.Bundles[b];
                    bool official = id != null && id.StartsWith("@deepseek-ai/", StringComparison.Ordinal);
                    bundles.Add(new string[] { name, id, official ? "official" : "thirdparty" });
                }
            }
            Console.WriteLine("PROFILES_OK " + rows.Count);
            for (int i = 0; i < rows.Count; i++)
                Console.WriteLine("PROFILE " + rows[i][0] + " form=" + rows[i][1] + " bundles=" + rows[i][2] + " thirdparty=" + rows[i][3]);
            for (int i = 0; i < bundles.Count; i++)
                Console.WriteLine("BUNDLE " + bundles[i][0] + " " + bundles[i][1] + " " + bundles[i][2]);
            return 0;
        }

        /// <summary>backup-list：标记行与裸路径行逐条对齐 v2.x 的 NIBackupList。</summary>
        private static int BackupList(string[] args, ServiceRegistry reg)
        {
            bool detail = Has(args, "--detail");
            IBackupSource src = reg.Get<IBackupSource>();
            List<BackupEntry> all = src.ListRaw();
            List<BackupEntry> valid = new List<BackupEntry>();
            for (int i = 0; i < all.Count; i++)
            {
                if (BackupPackage.IsValidPackage(all[i].Snapshot)) valid.Add(all[i]);
            }
            // v2.x：升序后反转 → 最新在前
            valid.Reverse();
            Console.WriteLine("BACKUP_LIST_OK " + valid.Count);
            foreach (BackupEntry e in valid)
            {
                Console.WriteLine(e.Path);
                if (detail)
                {
                    long bytes = src.DirSize(e.Path);
                    DateTime? mt = src.LastWrite(e.Path);
                    string mts = mt.HasValue ? mt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "(unknown)";
                    Console.WriteLine("BACKUP_ITEM " + e.Name + " " + BackupPackage.KindLabel(BackupPackage.Classify(e.Name)) + " " + bytes + " " + mts);
                }
            }
            return 0;
        }


        /// <summary>doctor：七类体检。首行 DOCTOR_OK|WARN|ERROR n，其后每行 [级别] 类别 描述。逐条对齐 v2.x。
        /// 可选 `--report &lt;file&gt;`：写完整诊断报告（含配置/日志摘要，全部脱敏）→ `DOCTOR_REPORT &lt;路径&gt;`；
        /// 写失败 → `DOCTOR_WRITE_FAIL &lt;原因&gt;`。全程只读（与 v2.x 一致，报告用 UTF-8 **带 BOM** 写）。</summary>
        private static int Doctor(string[] args, ServiceRegistry reg)
        {
            List<DocItem> items = new List<DocItem>();
            DoctorCollect(reg, items);
            string summary = DoctorSummary.Summary(items);
            Console.WriteLine(summary);
            foreach (DocItem it in items)
                Console.WriteLine("[" + DoctorSummary.Level(it.Level) + "] " + it.Cat + " " + it.Text);

            string report = Flag(args, "--report");
            if (report == null) report = Flag(args, "-report");
            if (report != null)
            {
                string text = DoctorReport.Build(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), ToolkitVersion,
                    Environment.OSVersion.VersionString, items,
                    ConfigSummaryBuilder.Build(reg.Get<IConfigSource>().ReadConfig()),
                    LogSummaryBuilder.Build(reg.Get<ILogSource>().ReadLog()), summary);
                try
                {
                    System.IO.File.WriteAllText(report, text, new System.Text.UTF8Encoding(true));
                    Console.WriteLine("DOCTOR_REPORT " + report);
                }
                catch (Exception ex) { Console.WriteLine("DOCTOR_WRITE_FAIL " + ex.Message); }
            }
            return 0;
        }

        private static void DoctorCollect(ServiceRegistry reg, List<DocItem> items)
        {
            IToolchainQuery tc = reg.Get<IToolchainQuery>();
            IFileSystemQuery fs = reg.Get<IFileSystemQuery>();
            IBackupSource bk = reg.Get<IBackupSource>();
            IHttpProbe http = reg.Get<IHttpProbe>();
            IProcessQuery proc = reg.Get<IProcessQuery>();
            IIntegritySource integ = reg.Get<IIntegritySource>();
            IPaths paths = reg.Get<IPaths>();
            ServiceReport sr = reg.Get<IServiceTarget>().Probe();

            items.Add(new DocItem("System", 0, "Windows: " + Environment.OSVersion.VersionString + " (" + (Environment.Is64BitOperatingSystem ? "x64" : "x86") + ")"));
            string node = tc.NodeVersion();
            if (string.IsNullOrWhiteSpace(node)) items.Add(new DocItem("System", 1, "Node.js 未找到（dsh 依赖 npm 安装）"));
            else items.Add(new DocItem("System", 0, "Node.js: " + node.Trim()));
            string npm = tc.NpmVersion();
            items.Add(new DocItem("System", string.IsNullOrWhiteSpace(npm) ? 1 : 0, string.IsNullOrWhiteSpace(npm) ? "npm 不可用" : "npm: " + npm.Trim()));

            string dsh = tc.WhichDsh();
            if (dsh == null) items.Add(new DocItem("Harness", 2, "dsh 未安装（交互菜单按 1 安装）"));
            else
            {
                items.Add(new DocItem("Harness", 0, "dsh 已安装: " + ReportSanitizer.Sanitize(dsh)));
                string dv = tc.DshVersion();
                if (string.IsNullOrWhiteSpace(dv)) items.Add(new DocItem("Harness", 1, "dsh --version 无输出"));
                else items.Add(new DocItem("Harness", 0, "dsh 版本: " + ReportSanitizer.Sanitize(dv.Trim().Replace("\r", " ").Replace("\n", " "))));
            }

            if (sr.State == ServiceState.Down)
            {
                items.Add(new DocItem("Service", 2, "端口 " + WebPort + " 未监听（服务未运行；菜单按 2 启动）"));
            }
            else
            {
                int pid = sr.Pid;
                items.Add(new DocItem("Service", 0, "端口 " + WebPort + " 监听中" + (pid > 0 ? "（PID " + pid + "）" : "")));
                bool isDsh = pid > 0 && proc.IsDshCommandLine(pid);
                string who = isDsh ? "监听进程确为 dsh" : (pid > 0 ? "监听进程不是 dsh！命令行: " + ReportSanitizer.Sanitize(proc.CommandLine(pid)) : "无法确认监听进程身份");
                items.Add(new DocItem("Service", isDsh ? 0 : 2, who));
                bool httpOk = http.Responds(WebUrl, 800);
                items.Add(new DocItem("Service", 0, "HTTP: " + (httpOk ? "有应答（dsh 未授权统一 401 属正常门控）" : "无应答")));
                items.Add(new DocItem("Service", sr.State == ServiceState.Ready ? 0 : 1, "服务状态: " + (sr.State == ServiceState.Ready ? "运行中" : (sr.State == ServiceState.Listening ? "启动中" : "已停止"))));
            }

            string data = paths.DataRoot;
            if (string.IsNullOrEmpty(data) || !fs.DirectoryExists(data))
            {
                items.Add(new DocItem("Workspace", 2, "数据目录不存在: " + data + "（dsh 尚未初始化）"));
            }
            else
            {
                bool enumerable = fs.CanEnumerate(data);
                items.Add(new DocItem("Workspace", enumerable ? 0 : 2, "数据目录: " + ReportSanitizer.Sanitize(data) + (enumerable ? "" : "（无读取权限）")));
                long size = fs.DirSize(data);
                items.Add(new DocItem("Workspace", size > 1024L * 1024 * 1024 ? 1 : 0, "数据大小: " + SizeFormatter.Human(size) + (size > 1024L * 1024 * 1024 ? "（较大，备份耗时会增加）" : "")));
            }

            string bkRoot = bk.BackupsRoot;
            if (!fs.DirectoryExists(bkRoot))
            {
                items.Add(new DocItem("Backup", 1, "备份目录不存在（尚未备份过；建议定期备份）"));
            }
            else
            {
                items.Add(new DocItem("Backup", 0, "备份目录: " + ReportSanitizer.Sanitize(bkRoot)));
                List<BackupEntry> all = bk.ListRaw();
                string latest = null;
                for (int i = all.Count - 1; i >= 0; i--) { if (BackupPackage.IsValidPackage(all[i].Snapshot)) { latest = all[i].Name; break; } }
                if (latest == null) items.Add(new DocItem("Backup", 1, "无有效备份（全部无效或为空）"));
                else
                {
                    items.Add(new DocItem("Backup", 0, "最新备份: " + ReportSanitizer.Sanitize(latest)));
                    int? days = BackupAge.DaysSince(latest, DateTime.Now);
                    if (days.HasValue) items.Add(new DocItem("Backup", days.Value > 7 ? 1 : 0, "距上次备份: " + days.Value + " 天" + (days.Value > 7 ? "（建议更新备份）" : "")));
                }
            }

            string cfgReg = tc.NpmRegistryConfig();
            string registry = string.IsNullOrWhiteSpace(cfgReg) ? NpmOfficial : cfgReg.Trim();
            bool reach = http.Responds(registry, 4000);
            items.Add(new DocItem("Network", reach ? 0 : 1, "npm registry " + ReportSanitizer.Sanitize(registry) + (reach ? " 可达" : " 不可达（离线或网络受限；不影响本地功能）")));

            string expected = ManifestParser.ParseHash(integ.ReadManifest(), integ.SelfFileName());
            IntegrityVerdict verdict = IntegrityJudge.Judge(expected, integ.SelfHash());
            if (verdict == IntegrityVerdict.Match) items.Add(new DocItem("Integrity", 0, "自身 exe 与随包 hashes.txt 一致（未被改动）"));
            else if (verdict == IntegrityVerdict.Mismatch) items.Add(new DocItem("Integrity", 2, "自身 exe 与随包 hashes.txt 不一致！（可能被篡改或替换，请从官方 Release 重新下载）"));
            else items.Add(new DocItem("Integrity", 0, "旁无 hashes.txt，跳过自身校验（单独复制 exe 或源码编译属正常；如需校验请使用官方发布包）"));
        }

        /// <summary>工具箱版本（源码常量：保证 csc 与 dotnet 两种构建报告一致）。</summary>
        internal const string ToolkitVersion = "3.0.0-dev";

        private const string NpmOfficial = "https://registry.npmjs.org";

        private static bool FileExists(string p) { try { return System.IO.File.Exists(p); } catch { return false; } }

        private static string Flag(string[] args, string name)
        {
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        private static bool Has(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++) if (args[i] == name) return true;
            return false;
        }

        /// <summary>工作区解析（对齐 v2.x 的 WorkspaceRoot）：`ws=` 配置优先——配置了但目录不存在 → null
        /// （**不回退自动探测**，避免误备份/误恢复）；未配置 → 用平台自动探测（Windows：exe 上两级 + 合理性判定；
        /// Linux：诚实返回 null）。dry-run 与真实恢复都走这里，保证两处目标一致。</summary>
        private static string WorkspaceRoot(ServiceRegistry reg)
        {
            return WorkspaceResolver.Resolve(_cfg == null ? null : _cfg.Workspace, reg.Get<IPaths>().WorkspaceRoot,
                delegate(string p) { return System.IO.Path.GetFullPath(p); },
                delegate(string p) { return System.IO.Directory.Exists(p); });
        }







        /// <summary>有效备份目录判定（注入给领域校验器）。</summary>
        private static Func<string, bool> IsValidBackupDirFn(ServiceRegistry reg)
        {
            IBackupSource bk = reg.Get<IBackupSource>();
            return delegate(string dir) { return BackupPackage.IsValidPackage(bk.Snapshot(dir)); };
        }

        /// <summary>备份导出：只做校验；真实复制会写盘，V3 尚未移植 → 明确拒绝（与真实 restore 同一策略）。</summary>
        private static int BackupExport(string[] args, ServiceRegistry reg)
        {
            IBackupSource bk = reg.Get<IBackupSource>();
            IFileSystemQuery fs = reg.Get<IFileSystemQuery>();
            string reason = PathValidator.ValidateExport(Flag(args, "--path"), Flag(args, "--to"), bk.BackupsRoot,
                delegate(string p) { return fs.DirectoryExists(p); },
                delegate(string p) { return System.IO.Path.GetFullPath(p); });
            if (reason != null) { Console.WriteLine("BKEXPORT_FAIL " + T("导出校验失败: " + reason, "export validation failed: " + reason)); return 0; }
            string src = (Flag(args, "--path") ?? "").Trim().Trim('"');
            string to = (Flag(args, "--to") ?? "").Trim().Trim('"');
            string target = bk.Export(src, System.IO.Path.GetFullPath(to));
            if (target == null) { Console.WriteLine("BKEXPORT_FAIL " + T("导出失败（见 launcher.log）", "export failed (see launcher.log)")); return 0; }
            Console.WriteLine("BKEXPORT_OK " + target);
            return 0;
        }

        /// <summary>备份删除：只做校验；真实删除会丢数据，V3 尚未移植 → 明确拒绝。</summary>
        private static int BackupDelete(string[] args, ServiceRegistry reg)
        {
            IBackupSource bk = reg.Get<IBackupSource>();
            IFileSystemQuery fs = reg.Get<IFileSystemQuery>();
            string reason = PathValidator.ValidateDeletePath(Flag(args, "--path"), bk.BackupsRoot,
                delegate(string p) { return fs.DirectoryExists(p); });
            if (reason != null) { Console.WriteLine("BKDEL_FAIL " + T("删除校验失败: " + reason, "delete validation failed: " + reason)); return 0; }
            string src = (Flag(args, "--path") ?? "").Trim().Trim('"');
            try
            {
                bk.Delete(src);
                if (fs.DirectoryExists(src)) { Console.WriteLine("BKDEL_FAIL " + T("删除后目录仍存在", "directory still exists after delete")); return 0; }
                Console.WriteLine("BKDEL_OK " + System.IO.Path.GetFileName(src.TrimEnd('\\', '/')));
            }
            catch (Exception ex) { Console.WriteLine("BKDEL_FAIL " + ex.Message); }
            return 0;
        }

        /// <summary>backup：非交互备份（手动类）。标记逐条对齐 v2.x 的 NIBackup。</summary>
        private static int Backup(ServiceRegistry reg)
        {
            IPaths paths = reg.Get<IPaths>();
            IBackupSource bk = reg.Get<IBackupSource>();
            string src = paths.DataRoot;
            if (!reg.Get<IFileSystemQuery>().DirectoryExists(src))
            {
                Console.WriteLine("BACKUP_FAIL " + T("数据目录不存在：" + src, "data dir not found: " + src));
                return 0;
            }
            BackupResult r = bk.Create(src, BackupKind.Manual);
            if (r == null) { Console.WriteLine("BACKUP_FAIL " + T("备份失败（见 launcher.log）", "backup failed (see launcher.log)")); return 0; }
            if (r.SkippedNested > 0)
                Console.WriteLine(T("已跳过 " + r.SkippedNested + " 个嵌套备份目录（dsh-data-*），不复制进本次备份。",
                                    "Skipped " + r.SkippedNested + " nested backup folder(s) (dsh-data-*), not copied into this backup."));
            Console.WriteLine("BACKUP_OK " + r.Path);
            return 0;
        }

        private const string GithubHandle = "github.com/sakanamaru";

        /// <summary>命令输出净化：去首尾空白，空串 → null（v2.x 的捕获不带尾换行）。</summary>
        private static string TrimOrNull(string s)
        {
            if (s == null) return null;
            string t = s.Trim();
            return t.Length == 0 ? null : t;
        }

        /// <summary>横幅：与 v2.x 同构（版本行按产品版本不同，比对时忽略）。</summary>
        private static void Banner()
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("  DeepSeek Harness Toolkit V" + ToolkitVersion);
            Console.WriteLine("==============================================");
            Console.WriteLine("  v1 脚本协助 : SOGR-Momono Dango（QwenPaw/DeepseekAPI-V4-Flash-0731）");
            Console.WriteLine("  v2 重构封装 : DeepSeek DSH （DSH/DeepseekAPI-V4-Flash-0731）");
            Console.WriteLine("  GitHub    : @sakanamaru  https://" + GithubHandle);
            Console.WriteLine("----------------------------------------------");
            Console.WriteLine("  " + T("⚠ 非官方工具，由社区独立开发，与 DeepSeek 官方无关。", "⚠ Unofficial community tool, not affiliated with DeepSeek."));
        }

        private static void Pause()
        {
            Console.WriteLine();
            Console.WriteLine(T("  按任意键继续...", "  Press any key to continue..."));
            try { Console.ReadKey(true); } catch { }
            Console.WriteLine();
        }

        /// <summary>check：安装/版本/更新/服务/语言一览（GUI 检查页数据源）。逐条对齐 v2.x 的 Check()。</summary>
        private static int Check(ServiceRegistry reg)
        {
            IToolchainQuery tc = reg.Get<IToolchainQuery>();
            Banner();
            string node = TrimOrNull(tc.NodeVersion());
            Console.WriteLine("  Node.js    : " + (string.IsNullOrWhiteSpace(node) ? T("未检测到", "not found") : node));
            string npm = TrimOrNull(tc.NpmVersion());
            Console.WriteLine("  npm        : " + (string.IsNullOrWhiteSpace(npm) ? T("未检测到", "not found") : npm));
            string dsh = tc.WhichDsh();
            Console.WriteLine("  dsh        : " + (dsh == null ? T("未安装", "not installed") : dsh + " ✓"));
            if (dsh != null)
            {
                string v = TrimOrNull(tc.DshVersion());
                Console.WriteLine("  dsh 版本   : " + (string.IsNullOrWhiteSpace(v) ? T("（读取失败）", "(read failed)") : v));
                if (_cfg.CheckDshUpdate)
                {
                    string latest = VersionComparer.SanitizeLatest(tc.NpmViewLatest());
                    string line;
                    if (string.IsNullOrEmpty(latest)) line = T("（离线，未获取）", "(offline, n/a)");
                    else if (string.IsNullOrWhiteSpace(v) || VersionComparer.Compare(v, latest) < 0)
                        line = latest + (string.IsNullOrWhiteSpace(v) ? "" : T("（当前 " + v + "，有更新）", " (current " + v + ", update available)"));
                    else line = latest + T("（已是最新）", " (up to date)");
                    Console.WriteLine("  dsh 最新   : " + line);
                }
            }
            ServiceReport sr = reg.Get<IServiceTarget>().Probe();
            string ws = sr.State == ServiceState.Ready ? WebUrl + " " + T("已在运行", "running")
                : (sr.State == ServiceState.Listening ? T("启动中（端口已开，服务未就绪）", "starting (port open, not ready)") : T("未启动", "not started"));
            Console.WriteLine("  Web 服务   : " + ws);
            Console.WriteLine("  UI 语言    : " + (_cfg.Lang == "auto" ? T("跟随系统", "follow system") : (_cfg.Lang == "zh" ? "简体中文" : "English")));
            Pause();
            return 0;
        }

        /// <summary>selftest：写自检报告并打印 report -> 路径。内容逐条对齐 v2.x 的 Selftest。</summary>
        private static int SelfTest(string[] args, ServiceRegistry reg)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("== DeepSeek Harness Toolkit selftest ==");
            try
            {
                System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
                System.Reflection.AssemblyTitleAttribute title = (System.Reflection.AssemblyTitleAttribute)System.Attribute.GetCustomAttribute(asm, typeof(System.Reflection.AssemblyTitleAttribute));
                System.Reflection.AssemblyCompanyAttribute company = (System.Reflection.AssemblyCompanyAttribute)System.Attribute.GetCustomAttribute(asm, typeof(System.Reflection.AssemblyCompanyAttribute));
                System.Reflection.AssemblyDescriptionAttribute desc = (System.Reflection.AssemblyDescriptionAttribute)System.Attribute.GetCustomAttribute(asm, typeof(System.Reflection.AssemblyDescriptionAttribute));
                sb.AppendLine("title   : " + (title == null ? "(null)" : title.Title));
                sb.AppendLine("company : " + (company == null ? "(null)" : company.Company));
                sb.AppendLine("desc    : " + (desc == null ? "(null)" : desc.Description));
                sb.AppendLine("version : " + asm.GetName().Version);
                sb.AppendLine("ui lang : " + System.Globalization.CultureInfo.CurrentUICulture.Name);

                IToolchainQuery tc = reg.Get<IToolchainQuery>();
                IPortProbe ports = reg.Get<IPortProbe>();
                IPaths paths = reg.Get<IPaths>();
                string dshLoc = tc.WhichDsh();
                sb.AppendLine("dsh installed (live): " + (dshLoc != null));

                sb.AppendLine("port 1 (expect False): " + ports.IsOpen(1, 500));
                bool selfOpen;
                System.Net.Sockets.TcpListener l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
                l.Start();
                int port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
                selfOpen = ports.IsOpen(port, 500);
                l.Stop();
                sb.AppendLine("self-listener (expect True): " + selfOpen);

                sb.AppendLine("dsh loc  : " + (dshLoc == null ? "(null)" : dshLoc));
                string nodeVer = tc.NodeVersion();
                sb.AppendLine("node ver : " + (string.IsNullOrEmpty(nodeVer) ? "(empty)" : nodeVer));
                sb.AppendLine("state dir: " + paths.StateDir);
                sb.AppendLine("data root: " + paths.DataRoot);
            }
            catch (Exception ex) { sb.AppendLine("EXCEPTION: " + ex); }

            string report = args.Length > 1 ? args[1] : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dsh_selftest.txt");
            try
            {
                System.IO.File.WriteAllText(report, sb.ToString(), new System.Text.UTF8Encoding(true));
                Console.WriteLine("report -> " + report);
            }
            catch (Exception ex) { Console.WriteLine("write report failed: " + ex.Message); }
            return 0;
        }

        /// <summary>最小本地化：dryrun/restore 的失败与说明文案在 v2.x 里走 T()，必须同语言才能比对。</summary>
        private static string T(string zh, string en) { return IsEn() ? en : zh; }

        /// <summary>当前是否英文（与 v2.x 的 T() 同一判据）。</summary>
        private static bool IsEn() { return _cfg != null && _cfg.Lang == "en"; }

        private static void PrintPlan(long[] p)
        {
            Console.WriteLine("DRYRUN_NEW " + p[0]);
            Console.WriteLine("DRYRUN_OVERWRITE " + p[1]);
            Console.WriteLine("DRYRUN_KEEP " + p[2]);
            Console.WriteLine("DRYRUN_BYTES " + p[3]);
        }

        private static long[] PlanMerge(ServiceRegistry reg, string src, string dst, string skipTopDir, string skipTopFile, bool topRules)
        {
            IFileSystemQuery fs = reg.Get<IFileSystemQuery>();
            return MergePlanner.Plan(fs.WalkSource(src, skipTopDir, skipTopFile, topRules), fs.WalkDestination(dst));
        }

        /// <summary>restore：真实合并恢复。顺序与 v2.x 的 NIRestoreCore 一致：
        ///   定位/校验备份 → （--apply 准入）→ 运行中拒绝 → 恢复前自动备份 → 自身完整性闸门 → 恢复。
        /// V3 独有：`--apply` 显式开关，且只允许写入**隔离数据根**（见 RestoreApplyPolicy）——
        /// 生效数据根等于默认位置时直接拒绝，因此 V3 的真实恢复永远不会写进用户默认数据根。
        /// 与 v2.x 的有意差异（更诚实）：只有真正成功才打印 RESTORE_OK（v2.x 在恢复失败时也会打印 RESTORE_OK）。</summary>
        private static int Restore(string[] args, ServiceRegistry reg)
        {
            if (Has(args, "--dry-run") || Has(args, "-dry-run")) return DryRun(args, reg);
            bool apply = Has(args, "--apply");
            IBackupSource bk = reg.Get<IBackupSource>();
            IPaths paths = reg.Get<IPaths>();
            IFileSystemQuery fs = reg.Get<IFileSystemQuery>();
            string bkDir;
            string pathArg = Flag(args, "--path");
            if (pathArg != null)
            {
                string reason = PathValidator.ValidateRestorePath(pathArg, bk.BackupsRoot, IsValidBackupDirFn(reg));
                if (reason != null)
                {
                    if (reason == "no-path") Console.WriteLine("RESTORE_FAIL " + T("未指定备份目录", "no backup specified"));
                    else if (reason == "outside") Console.WriteLine("RESTORE_FAIL " + T("备份目录不在备份根内", "backup dir is outside the backups root"));
                    else Console.WriteLine("RESTORE_FAIL " + T("无效备份目录", "invalid backup directory"));
                    return 0;
                }
                bkDir = pathArg.Trim().Trim('"');
            }
            else
            {
                if (!fs.DirectoryExists(bk.BackupsRoot)) { Console.WriteLine("RESTORE_FAIL " + T("没有备份", "no backups")); return 0; }
                List<BackupEntry> all = bk.ListRaw();
                string latest = null;
                for (int i = all.Count - 1; i >= 0; i--) { if (BackupPackage.IsValidPackage(all[i].Snapshot)) { latest = all[i].Path; break; } }
                if (latest == null) { Console.WriteLine("RESTORE_FAIL " + T("无有效备份", "no valid backup")); return 0; }
                bkDir = latest;
            }

            // --apply 准入：真实写盘只允许发生在隔离数据根上（默认数据根永不被 V3 恢复写入）
            string applyReason = RestoreApplyPolicy.Judge(apply, Environment.GetEnvironmentVariable("DSH_HOME"),
                paths.DataRoot, PlatformComposition.DefaultDataRoots());
            if (applyReason != null)
            {
                Console.WriteLine("RESTORE_FAIL " + RestoreApplyPolicy.Message(applyReason, !IsEn()));
                return 0;
            }

            // 安全闸门（逐条对齐 v2.x 的 NIRestoreCore）：运行中拒绝 → 恢复前自动备份
            ServiceReport sr = reg.Get<IServiceTarget>().Probe();
            if (sr.State != ServiceState.Down && !apply)
            {
                Console.WriteLine("RESTORE_FAIL " + T("dsh 正在运行，无法恢复", "dsh is running; cannot restore"));
                return 0;
            }
            if (apply)
            {
                // 跳过"运行中"闸门是人类的显式断言，但观测到的事实必须原样打出来（证据链，不静默）
                Console.WriteLine("RESTORE_APPLY_ACK " + T("已按 --apply 跳过「运行中」闸门；观测到的服务状态：",
                    "running-service gate skipped by --apply; observed service state: ") + sr.State + (sr.Pid > 0 ? " pid=" + sr.Pid : ""));
                Console.WriteLine("RESTORE_APPLY_ROOT " + paths.DataRoot);
            }

            string dstRoot = paths.DataRoot;
            if (fs.DirectoryExists(dstRoot))
            {
                BackupResult pre = bk.Create(dstRoot, BackupKind.PreRestore);
                if (pre == null) { Console.WriteLine("RESTORE_FAIL " + T("恢复前自动备份失败", "pre-restore backup failed")); return 0; }
                Console.WriteLine("RESTORE_PRE_BACKUP " + pre.Path);   // V3 追加：把回滚锚点直接给出来
            }

            if (!IntegrityGate(reg)) return 0;   // 对齐 v2.x：完整性不匹配时在写盘前拒绝

            RestoreOutcome o = bk.Restore(bkDir, dstRoot, WorkspaceRoot(reg));
            if (!o.Ok)
            {
                Console.WriteLine("RESTORE_FAIL " + T("恢复失败：" + (o.Error ?? ""), "restore failed: " + (o.Error ?? "")));
                return 0;
            }
            Console.WriteLine("RESTORE_OK " + bkDir);
            if (o.WorkspacesRestored > 0) Console.WriteLine("RESTORE_WS_RESTORED " + o.WorkspacesRestored);
            if (o.WorkspacesSkipped > 0) Console.WriteLine("RESTORE_WS_SKIPPED " + o.WorkspacesSkipped);
            if (o.WorkspacesUnrecognized > 0) Console.WriteLine("RESTORE_WS_UNRECOGNIZED " + o.WorkspacesUnrecognized);
            return 0;
        }

        /// <summary>高风险操作闸门（对齐 v2.x 的 IntegrityGate）：自身与随包 hashes.txt 不匹配即拒绝；旁无 manifest 时放行。</summary>
        private static bool IntegrityGate(ServiceRegistry reg)
        {
            IIntegritySource integ = reg.Get<IIntegritySource>();
            string expected = ManifestParser.ParseHash(integ.ReadManifest(), integ.SelfFileName());
            if (!IntegrityJudge.ShouldBlock(IntegrityJudge.Judge(expected, integ.SelfHash()))) return true;
            Console.WriteLine("RESTORE_FAIL " + T("自身完整性校验失败：当前程序与随包 hashes.txt 不匹配（可能被篡改）。已拒绝执行恢复，请从官方 Releases 重新下载。",
                "self-integrity FAILED: this executable does not match the shipped hashes.txt (possible tampering); restore refused. Re-download from the official Releases."));
            return false;
        }

        /// <summary>dryrun：只读合并计划。标记与文案逐条对齐 v2.x 的 NIRestoreDryRun。</summary>
        private static int DryRun(string[] args, ServiceRegistry reg)
        {
            string pathArg = Flag(args, "--path");
            IBackupSource bk = reg.Get<IBackupSource>();
            IPaths paths = reg.Get<IPaths>();
            IFileSystemQuery fs = reg.Get<IFileSystemQuery>();
            string bkDir = null;

            if (string.IsNullOrWhiteSpace(pathArg))
            {
                List<BackupEntry> all = bk.ListRaw();
                for (int i = all.Count - 1; i >= 0; i--) { if (BackupPackage.IsValidPackage(all[i].Snapshot)) { bkDir = all[i].Path; break; } }
                if (bkDir == null) { Console.WriteLine("DRYRUN_FAIL " + T("无有效备份", "no valid backup")); return 0; }
            }
            else
            {
                string p = pathArg.Trim().Trim('"');
                if (PathUtil.IsSubPath(bk.BackupsRoot, p))
                {
                    if (!BackupPackage.IsValidPackage(bk.Snapshot(p))) { Console.WriteLine("DRYRUN_FAIL " + T("无效备份目录", "invalid backup directory")); return 0; }
                    bkDir = p;
                }
                else
                {
                    string resolved = bk.Resolve(p);
                    if (resolved == null) { Console.WriteLine("DRYRUN_FAIL " + T("不是有效备份包", "not a valid backup package")); return 0; }
                    bkDir = resolved;
                }
            }

            Console.WriteLine("DRYRUN_OK");
            Console.WriteLine("DRYRUN_SRC " + bkDir);
            long tn = 0, to = 0, tk = 0, tb = 0;
            string dst = paths.DataRoot;
            long[] dp = PlanMerge(reg, bkDir, dst, "_workspace", null, false);
            Console.WriteLine("DRYRUN_SCOPE data " + dst);
            PrintPlan(dp);
            tn += dp[0]; to += dp[1]; tk += dp[2]; tb += dp[3];

            string wsSrc = System.IO.Path.Combine(bkDir, "_workspace");
            if (fs.DirectoryExists(wsSrc))
            {
                string[] subs = fs.ListDirectories(wsSrc);
                bool anyNew = false;
                for (int i = 0; i < subs.Length; i++) { if (fs.FileExists(System.IO.Path.Combine(subs[i], ".dshws"))) { anyNew = true; break; } }
                if (anyNew)
                {
                    for (int i = 0; i < subs.Length; i++)
                    {
                        if (!fs.FileExists(System.IO.Path.Combine(subs[i], ".dshws"))) continue;
                        string name = System.IO.Path.GetFileName(subs[i]);
                        string target = System.IO.Path.Combine(WorkspaceRoot(reg) == null ? dst : WorkspaceRoot(reg), name);
                        long[] wp = PlanMerge(reg, subs[i], target, null, ".dshws", false);
                        Console.WriteLine("DRYRUN_SCOPE workspace " + name + " " + target);
                        PrintPlan(wp);
                        tn += wp[0]; to += wp[1]; tk += wp[2]; tb += wp[3];
                    }
                }
                else
                {
                    string target = WorkspaceRoot(reg) == null ? dst : WorkspaceRoot(reg);
                    long[] wp = PlanMerge(reg, wsSrc, target, null, null, true);
                    Console.WriteLine("DRYRUN_SCOPE workspace-legacy " + target);
                    PrintPlan(wp);
                    tn += wp[0]; to += wp[1]; tk += wp[2]; tb += wp[3];
                }
            }
            Console.WriteLine("DRYRUN_TOTAL " + tn + " " + to + " " + tk + " " + tb);
            Console.WriteLine("DRYRUN_NOTE " + T("合并语义：仅目标端存在的文件不会被删除；交互恢复时每个工作区可自定义目标或跳过。", "Merge semantics: destination-only files are NOT deleted; interactive restore allows per-workspace custom target or skip."));
            return 0;
        }
        /// <summary>bootdiag：解析启动失败输出。标记逐条对齐 v2.x 的 BootDiagCli。</summary>
        private static int BootDiag(string[] args, ServiceRegistry reg)
        {
            string from = Flag(args, "--from");
            if (string.IsNullOrEmpty(from)) { Console.WriteLine("BOOTDIAG_FAIL no-input"); return 0; }
            string text = null;
            try { if (System.IO.File.Exists(from)) text = System.IO.File.ReadAllText(from, new System.Text.UTF8Encoding(false)); } catch { }
            if (text == null) { Console.WriteLine("BOOTDIAG_FAIL cannot-read " + ReportSanitizer.Sanitize(from)); return 0; }
            BootDiagResult r = BootDiagParser.Parse(text, LocateEntry(reg), FileExists);
            if (!r.Recognized)
            {
                Console.WriteLine("BOOTDIAG_FAIL");
                Console.WriteLine("BOOTDIAG_KIND unknown");
                Console.WriteLine("BOOTDIAG_FIRST " + TextClipper.Clip(r.FirstError, 200));
                return 0;
            }
            Console.WriteLine("BOOTDIAG_OK");
            Console.WriteLine("BOOTDIAG_KIND " + r.Kind);
            Console.WriteLine("BOOTDIAG_PLUGIN " + r.Plugin);
            Console.WriteLine("BOOTDIAG_ENTRY " + r.Entry);
            Console.WriteLine("BOOTDIAG_FILE " + ReportSanitizer.Sanitize(r.File));
            Console.WriteLine("BOOTDIAG_LINE " + r.Line);
            Console.WriteLine("BOOTDIAG_HINT " + r.Hint);
            return 0;
        }

        /// <summary>v2.x 的 LocateEntryLine：给定文件 → 该文件；给定目录 → 目录下 yml/yaml；都不是 → 整个 profiles 目录。</summary>
        private static Func<string, string, EntryLocation> LocateEntry(ServiceRegistry reg)
        {
            return delegate(string fileOrDir, string entry)
            {
                try
                {
                    if (System.IO.File.Exists(fileOrDir))
                    {
                        int ln = EntryLocator.FindLine(System.IO.File.ReadAllText(fileOrDir, new System.Text.UTF8Encoding(false)), entry);
                        if (ln > 0) return new EntryLocation(fileOrDir, ln);
                        return null;
                    }
                    if (System.IO.Directory.Exists(fileOrDir))
                    {
                        EntryLocation d = ScanDir(fileOrDir, entry);
                        if (d != null) return d;
                        return null;
                    }
                    IProfileSource src = reg.Get<IProfileSource>();
                    ProfileCollection col = src.CollectDirectory(null, true, true);
                    foreach (ProfileFile pf in col.Files)
                    {
                        int ln = EntryLocator.FindLine(pf.Text, entry);
                        if (ln > 0) return new EntryLocation(pf.Path, ln);
                    }
                }
                catch { }
                return null;
            };
        }

        private static EntryLocation ScanDir(string dir, string entry)
        {
            try
            {
                string[] files = System.IO.Directory.GetFiles(dir, "*.yml", System.IO.SearchOption.AllDirectories);
                string[] files2 = System.IO.Directory.GetFiles(dir, "*.yaml", System.IO.SearchOption.AllDirectories);
                for (int pass = 0; pass < 2; pass++)
                {
                    string[] cur = pass == 0 ? files : files2;
                    foreach (string f in cur)
                    {
                        int ln = EntryLocator.FindLine(System.IO.File.ReadAllText(f, new System.Text.UTF8Encoding(false)), entry);
                        if (ln > 0) return new EntryLocation(f, ln);
                    }
                }
            }
            catch { }
            return null;
        }

        private static ToolkitConfig _cfg = new ToolkitConfig();

        /// <summary>ws 规范化（平台侧真实路径校验，供领域层注入）。</summary>
        private static string CanonPath(string p) { return System.IO.Path.GetFullPath(p); }

        private static ToolkitConfig LoadConfig(ServiceRegistry reg)
        {
            try { return ConfigCodec.Parse(reg.Get<IConfigSource>().ReadConfig(), CanonPath); }
            catch { return new ToolkitConfig(); }
        }

        /// <summary>config-get：CONFIGGET_OK + 每行 CONFIG &lt;key&gt; &lt;value&gt;（顺序与 v2.x 一致）。</summary>
        private static int ConfigGet()
        {
            Console.WriteLine("CONFIGGET_OK");
            Console.WriteLine("CONFIG lang " + _cfg.Lang);
            Console.WriteLine("CONFIG host " + _cfg.Host);
            Console.WriteLine("CONFIG ws " + (_cfg.Workspace == null ? "" : _cfg.Workspace));
            Console.WriteLine("CONFIG keep_backups " + _cfg.KeepBackups);
            Console.WriteLine("CONFIG check_update " + (_cfg.CheckUpdate ? "on" : "off"));
            Console.WriteLine("CONFIG check_dsh_update " + (_cfg.CheckDshUpdate ? "on" : "off"));
            Console.WriteLine("CONFIG update_channel " + _cfg.UpdateChannel);
            Console.WriteLine("CONFIG close_action " + _cfg.CloseAction);
            Console.WriteLine("CONFIG auto_start " + (_cfg.AutoStart ? "on" : "off"));
            Console.WriteLine("CONFIG dsh_versions " + _cfg.DshVersions);
            return 0;
        }

        /// <summary>config-set &lt;key&gt; &lt;value&gt;：白名单内才写盘，否则 CONFIGSET_FAIL 原因。</summary>
        private static int ConfigSet(string[] args, ServiceRegistry reg)
        {
            string key = args.Length > 1 ? args[1] : "";
            string val = args.Length > 2 ? args[2] : "";
            string reason = ConfigValidator.Validate(key, val, CanonPath);
            if (reason != null) { Console.WriteLine("CONFIGSET_FAIL " + reason); return 0; }
            _cfg = ConfigValidator.ApplyTo(_cfg, key, val, CanonPath);
            reg.Get<IConfigSource>().WriteConfig(ConfigCodec.Serialize(_cfg));
            Console.WriteLine("CONFIGSET_OK " + key.Trim().ToLowerInvariant());
            return 0;
        }

        /// <summary>组合根：交给 PlatformComposition 按平台装配（单 exe，运行时判定）。</summary>
        private static ServiceRegistry Compose()
        {
            return PlatformComposition.Compose();
        }    }
}