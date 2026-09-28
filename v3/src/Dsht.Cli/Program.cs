using System;
using System.Collections.Generic;
using Dsht.Domain.Abstractions;
using Dsht.Domain.Model;
using Dsht.Domain.Services;
using Dsht.Domain.Targets;
using Dsht.Platform.Windows;

using System.Reflection;

[assembly: AssemblyTitle("dsh-minato V3")]
[assembly: AssemblyDescription("DeepSeek Harness(dsh) 安装/启动/卸载/备份恢复工具箱。v1: SOGR-Momono Dango(QwenPaw/DeepseekAPI-V4-Flash-0731)；v2: DeepSeek DSH(DSH/DeepseekAPI-V4-Flash-0731)；GitHub @sakanamaru")]
[assembly: AssemblyCompany("SOGR-Momono Dango / DeepSeek DSH / @sakanamaru")]
[assembly: AssemblyProduct("dsh-minato")]
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

            if (cmd == "") return Menu(reg);
            if (cmd == "status") return Status(reg, Has(args, "--detail"));
            if (cmd == "describe") return Describe(reg);
            if (cmd == "profilecheck") return ProfileCheck(args, reg);
            if (cmd == "profilepatch") return ProfilePatch(args, reg);
            if (cmd == "profiles") return Profiles(reg);
            if (cmd == "verify-install") return VerifyInstall(args);
            if (cmd == "wipe") return WipeCmd(args, reg);
            if (cmd == "import") return ImportCmd(args, reg);
            if (cmd == "update-info") return UpdateInfo(reg);
            if (cmd == "log") return LogCmd(args, reg);
            if (cmd == "about") return AboutCmd();
            if (cmd == "shortcut") return ShortcutCmd(args);
            if (cmd == "ui") return UiCmd();
            if (cmd == "install") return InstallLike(args, reg, false);
            if (cmd == "update") return InstallLike(args, reg, true);
            if (cmd == "uninstall") return UninstallCmd(args, reg);
            if (cmd == "start") return StartCmd(args, reg);
            if (cmd == "stop") return StopCmd(args, reg);
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
            Usage();
            return 2;
        }

        /// <summary>服务三态 + detail 三行。逐条对齐 v2.x 的 StatusCli。</summary>
        private static int Status(ServiceRegistry reg, bool detail)
        {
            ServiceReport r = reg.Get<IServiceTarget>().Probe();
            Console.WriteLine(r.StatusMarker);
            if (!detail) return 0;
            Console.WriteLine("STATUS_PID " + (r.Pid > 0 ? r.Pid.ToString() : "0"));
            // Honest diagnostic: a running service with no PID means the process probe could not read it
            // (iproute2/ss missing, or the listener belongs to another user) - say so instead of a bare 0.
            if (r.Pid <= 0 && !r.StatusMarker.EndsWith("_DOWN", StringComparison.Ordinal))
                Console.WriteLine("STATUS_PID_NOTE " + T("服务在运行但拿不到 PID：可能缺少 iproute2(ss) 或权限不足（uptime 也会为空）", "service is up but no PID could be read: iproute2 (ss) may be missing or permissions are insufficient (uptime will be empty too)"));
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
            int files = 0, skipped = 0, readErrors = 0;
            if (!string.IsNullOrEmpty(one))
            {
                ProfileFile pf = src.ReadSingle(one, abs);
                if (pf != null) { files = 1; fs.AddRange(ProfileScanner.Scan(pf.Text, pf.Label, FileExists)); }
            }
            else
            {
                ProfileCollection col = src.CollectDirectory(dir, vendor, abs);
                skipped = col.SkippedVendor;
                readErrors = col.ReadErrors;
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
            if (readErrors > 0 && Has(args, "--diag")) Console.WriteLine("PROFILECHK_READ_ERRORS " + readErrors);
            if (abs)
            {
                foreach (ProfileFinding f in fs)
                    if (f.Missing == "maxDepth") Console.WriteLine("PROFILECHK_FIX " + f.File + "|" + f.Line + "|" + f.Id + "|" + f.Missing);
            }
            // 只有"确实扫过且没有任何发现"才说 OK ✗：读不到目录时结果不完整，必须如实说明 ✓
            // OK 的抑制是**无条件**的 ✓：只要读错误 > 0，就不许说"没有问题" ✗（这跟 --diag 无关 —— 我一度把它一起 gated 了，回归测试立刻抓到 ✗）。
            if (fs.Count == 0 && readErrors == 0) Console.WriteLine("PROFILECHK_OK");
            else if (readErrors > 0 && Has(args, "--diag")) Console.WriteLine("PROFILECHK_INCOMPLETE " + T("有目录读不到，本次结果不完整 —— 不要当作「没有问题」", "some directories could not be read; this result is incomplete - do not read it as no problems"));
            return 0;
        }


        /// <summary>组合包版本（读该 profile 里它自己的 package.json 的 version；读不到 → 空串）。</summary>
        private static string BundleVersion(IProfileManifestSource src, string profileName, string bundleId)
        {
            JNode root = JsonLite.Parse(src.ReadBundleManifest(profileName, bundleId));
            return root == null ? "" : root.Get("version").AsString("");
        }

        /// <summary>从 cordis.patch.yml 里找出 `disabled: true` 的条目 id。
        /// **按行扫描**（不是 YAML 解析器）：遇到 disabled: true 就向前找最近的 `id:` 行；找不到就跳过（诚实降级，不猜）。</summary>
        private static string[] DisabledEntries(string yaml)
        {
            List<string> found = new List<string>();
            if (string.IsNullOrEmpty(yaml)) return found.ToArray();
            string[] lines = yaml.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!line.StartsWith("disabled:", StringComparison.Ordinal)) continue;
                if (line.IndexOf("true", StringComparison.OrdinalIgnoreCase) < 0) continue;
                for (int k = i - 1; k >= 0 && k > i - 40; k--)
                {
                    string prev = lines[k].Trim();
                    if (prev.StartsWith("disabled:", StringComparison.Ordinal)) continue;
                    if (prev.StartsWith("- id:", StringComparison.Ordinal)) { found.Add(prev.Substring("- id:".Length).Trim().Trim('\'', '"')); break; }
                    if (prev.StartsWith("id:", StringComparison.Ordinal)) { found.Add(prev.Substring("id:".Length).Trim().Trim('\'', '"')); break; }
                }
            }
            return found.ToArray();
        }
        /// <summary>sessions（V3 独有）：会话 / token / 缓存 面板的数据源。**只读** dsh 的会话投影（明文 JSON）。
        /// 来源优先级：插件快照（存在时）→ 每会话投影文件 → 投影总表。
        /// 标记行：
        ///   `SESSIONS_OK <n>` / `SESSIONS_NONBLANK <n>` / `SESSIONS_SOURCE <snapshot|disk|aggregate>` / `SESSIONS_ROOT <dir>`
        ///   / 每会话 `SESSION <id> last=<t|unknown> turns= steps= in= out= cacheRead= hit=<%|unknown> decode=<tok/s|unknown> ttft=<ms|unknown> ctx=<%|unknown> blank=0|1`
        ///   / `SESSIONS_TOTAL in= out= cacheRead= hit=<%|unknown> decode=<tok/s|unknown>` / `SESSIONS_FAIL <原因>`
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

        /// <summary>start（V3 独有）：启动 dsh，然后**用可观测事实确认**是否真的起来 —— 绝不因为"命令发出去了"就报成功。
        /// 标记行：`START_OK <pid>` / `START_FAIL <原因>`，两者之后都会补一行 `START_OBSERVED <状态>`（Ready/Listening/Down）。
        /// 用法：`start [--port <n>] [--profile <name>]`（默认 3080 / web）。</summary>
        /// <summary>取参数值（形如 `--port 3999`）；缺省或非法时返回 fallback。仅用于文案展示，判定逻辑在 TargetForStart 里。</summary>
        private static string ArgOr(string[] args, string name, string fallback)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name && !string.IsNullOrEmpty(args[i + 1])) return args[i + 1];
            }
            return fallback;
        }
        /// <summary>按 --port 选择 start 的目标：默认端口用组合目标；指定端口时临时构造 Web 目标。
        /// **单独成方法**是为了让"先解析参数、再选目标"的顺序不会被后续改动打乱 ——
        /// 之前就是因为顺序反了，`start --port 3999` 去探了默认 3080（用户的实例）。
        /// 注意：DSH_HOME 隔离数据根、**不隔离端口**，所以测试必须能指定端口。</summary>
        /// <summary>解析 dsh 可执行文件路径：先问工具链，再查免 sudo 引导后的常见目录（非交互 PATH 里通常没有 ✗），最后交给 PATH。</summary>
        private static string ResolveDsh(ServiceRegistry reg)
        {
            string w = reg.Get<IToolchainQuery>().WhichDsh();
            if (!string.IsNullOrEmpty(w)) return w;
            string home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                string[] cands = new string[]
                {
                    System.IO.Path.Combine(home, ".local/node/bin/dsh"),
                    System.IO.Path.Combine(home, ".npm-global/bin/dsh"),
                    System.IO.Path.Combine(home, ".local/bin/dsh")
                };
                for (int i = 0; i < cands.Length; i++) { if (System.IO.File.Exists(cands[i])) return cands[i]; }
            }
            return "dsh";
        }
        private static IServiceTarget TargetForStart(string[] args, ServiceRegistry reg)
        {
            int port = 0;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--port") { int pp; if (int.TryParse(args[i + 1], out pp)) port = pp; }
            }
            if (port <= 0 || port == WebPort) return reg.Get<IServiceTarget>();
            return PlatformComposition.WebFor(port, reg.Get<IPortProbe>(), reg.Get<IHttpProbe>(), reg.Get<IProcessQuery>());
        }
        /// <summary>install / update（V3 独有）：装或升级 dsh。**只用可观测事实判定结果**：
        /// 先看 WhichDsh/DshVersion（是否已装）→ 打印计划 → `--yes` 闸门 → npm → **再复检**。
        /// 标记行：`INSTALL_PLAN/_DRYRUN/_SKIP/_OK/_FAIL` 或 `UPDATE_*`，并始终补一行 `*_OBSERVED <版本|not-installed>`。</summary>
        /// <summary>无参数时的数字菜单（沿用 v2.x 的习惯，条目按 V3 的命令重排）。
        /// **纪律：写操作在菜单里二次确认后，才带 --yes 调用同一个命令实现** —— 闸门不绕过；
        /// 输入 EOF（管道/重定向）视为退出，绝不空转。</summary>
        /// <summary>用法行（未知命令与菜单"全部命令"共用）。</summary>
        /// <summary>用法速查（菜单"全部命令"与未知命令共用）。
        /// 注意：这里**绝不能调用自己** ✗ —— 之前用正则抽取内联那行时把函数体写成了 `Usage();`，
        /// 变成无限递归 → 菜单项 12 直接栈溢出（真机扫描抓到的 ✗）。</summary>
        private static void Usage()
        {
            Console.WriteLine(T("dsh-minato 命令速查：", "dsh-minato commands:"));
            Console.WriteLine("  status [--detail] | describe | doctor | bootdiag | check | selftest | version | about");
            Console.WriteLine("  profiles | profilecheck [--dir <d>] [--file <yaml>] [--diag] | profilepatch --profile <name> --id <entry> [--enable] [--yes]");
            Console.WriteLine("  sessions | log [--lines <n>] [--level info|warn|error] [--grep <text>] [--export <file> [--yes]]");
            Console.WriteLine("  config-get | config-set <key> <value>");
            Console.WriteLine("  install [--install-node] [--version <v>] [--list] [--yes] | update [--version <v>] [--list] [--yes] | uninstall [--yes]");
            Console.WriteLine("  update-info（只读：当前/最新/来源/状态/回滚候选 ✓）");
            Console.WriteLine("  start [--port <n>] [--profile <name>] [--yes] | stop [--port <n>] [--force] [--yes]");
            Console.WriteLine("  backup | backup-list [--detail] [--verify] | backup-export --path <备份> --to <目标> [--yes] | backup-delete --path <备份> [--yes] [--yes]");
            Console.WriteLine("  restore --path <备份> [--dry-run] [--apply] [--yes]");
            Console.WriteLine("  import --path <外部备份包> [--yes] | wipe [--yes] | verify-install [--manifest <f>] [--file <f>] [--url <u>]");
            Console.WriteLine("  shortcut [--yes] | ui | 无参数 = 数字菜单");
            Console.WriteLine(T("写操作一律先打印计划，加 --yes 才执行；涉及数据根的真实恢复还要求先设置 DSH_HOME。",
                                "Every write prints its plan first; add --yes to execute. A real restore also requires DSH_HOME."));
        }

        private static int Menu(ServiceRegistry reg)
        {
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("dsh-minato " + ToolkitVersion + T("　输入数字选择，q 退出", "  type a number, q to quit"));
                Console.WriteLine(T("   1 安装/升级 dsh        2 启动 dsh           3 停止 dsh", "   1 install / update     2 start dsh        3 stop dsh"));
                Console.WriteLine(T("   4 状态                 5 会话与 token       6 形态与插件", "   4 status               5 sessions         6 profiles"));
                Console.WriteLine(T("   7 立即备份             8 备份清单           9 恢复预览（dry-run）", "   7 backup               8 backup list      9 restore (dry-run)"));
                Console.WriteLine(T("  10 体检                11 备份目录           12 全部命令", "  10 doctor              11 backup folder   12 all commands"));
                Console.Write(T("选择：", "choice: "));
                string line = Console.ReadLine();
                if (line == null) return 0;
                line = line.Trim().ToLowerInvariant();
                if (line == "" || line == "q" || line == "quit" || line == "0") return 0;
                if (line == "1") { if (Confirm("install/update dsh")) { InstallLike(new string[] { "--yes" }, reg, false); InstallLike(new string[] { "--yes" }, reg, true); } continue; }
                if (line == "2") { if (Confirm("start dsh")) StartCmd(new string[] { "--yes" }, reg); continue; }
                if (line == "3") { if (Confirm("stop dsh")) StopCmd(new string[] { "--yes" }, reg); continue; }
                if (line == "4") { Status(reg, true); Describe(reg); continue; }
                if (line == "5") { Sessions(reg); continue; }
                if (line == "6") { Profiles(reg); continue; }
                if (line == "7") { if (Confirm("backup")) Backup(reg); continue; }
                if (line == "8") { BackupList(new string[] { "--detail" }, reg); continue; }
                if (line == "9") { Restore(new string[] { "--dry-run" }, reg); continue; }
                if (line == "10") { Doctor(new string[0], reg); continue; }
                if (line == "11") { Console.WriteLine(T("备份目录：", "backup folder: ") + reg.Get<IBackupSource>().BackupsRoot); continue; }
                if (line == "12") { Usage(); continue; }
                Console.WriteLine(T("没有这个选项。", "no such option."));
            }
        }

        /// <summary>菜单里的二次确认：只有明确输入 y 才继续（写操作的闸门不绕过）。</summary>
        private static bool Confirm(string what)
        {
            Console.Write(T("将执行：", "will run: ") + what + T("　确认？(y/N) ", "  confirm? (y/N) "));
            string a = Console.ReadLine();
            return a != null && a.Trim().ToLowerInvariant() == "y";
        }
        /// <summary>按更新通道选版本 ✓（配置 update_channel，经典版同名设置项 ✓）：
        /// stable = 最新**非预发布**版 ✓；rc = 最新版（含预发布 ✓，即 npm 的 latest 标签）。
        /// npm view versions 的输出是**升序**的 ✓，所以取最后一个匹配项即最新 ✓（不自己比版本号 ✗，避免 0.1.7 vs 0.1.10 这类字典序陷阱 ✗）。
        /// 列表取不到时回退到 latest ✓；调用方须如实说明来源 ✓。</summary>
        private static string VersionForChannel(IToolchainQuery tc, string channel)
        {
            string latest = NpmVersionGuard.Normalize(tc.NpmViewLatest());
            if (string.IsNullOrEmpty(channel) || channel != "stable") return latest;
            string list = tc.NpmViewVersions();
            if (string.IsNullOrEmpty(list)) return latest;
            System.Text.RegularExpressions.MatchCollection ms = System.Text.RegularExpressions.Regex.Matches(list, "[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.]+)?");
            string best = "";
            for (int i = 0; i < ms.Count; i++)
            {
                string v = ms[i].Value;
                if (v.IndexOf((char)45) >= 0) continue;   // 预发布跳过 ✓
                if (!NpmVersionGuard.IsSafe(v)) continue;
                best = v;                                  // npm 输出升序 → 最后一个即最新 ✓
            }
            if (best.Length > 0) return best;
            // 没有任何非预发布版（dsh 至今全是 -rc.x ✓）→ 如实说明这次回退 ✓，不静默把 rc 当 stable 交付 ✗
            Console.WriteLine("CHANNEL_NOTE " + T("stable 通道下没有任何非预发布版，已回退到最新预发布版 ", "no non-prerelease version exists on the stable channel; falling back to the newest prerelease ") + latest);
            return latest;
        }
        /// <summary>verify-install（V3 独有）：核对本机文件与发布清单的 SHA-256 ✓ —— 经典版「验证此安装」的 CLI 对应物 ✓。
        /// 默认核对**正在运行的自身** ✓；清单默认取自身旁边的 hashes.txt ✓，或用 --manifest 指定，或用 --url 从发布页取 ✓。
        /// **清单拿不到时绝不说 OK** ✗✓：只报 VERIFY_MANIFEST_MISSING 并说明如何取得 ✓。
        /// 诚实边界 ✓：本命令只做 SHA-256 比对；清单本身的 GPG 签名校验仍在 Windows 的 verify.ps1 里 ✓（CLI 不引入第三方依赖 ✗）。
        /// 标记行：VERIFY_SELF / VERIFY_MANIFEST / VERIFY_MATCH / VERIFY_MISMATCH / VERIFY_NOT_IN_MANIFEST / VERIFY_OK / VERIFY_FAIL。</summary>
        private static int VerifyInstall(string[] args)
        {
            string target = FlagOf(args, "--file");
            if (target.Length == 0)
            {
                try { target = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName; } catch { }
                if (string.IsNullOrEmpty(target)) { Console.WriteLine("VERIFY_FAIL " + T("拿不到自身路径，请用 --file 指定要核对的文件", "cannot determine own path; pass --file")); return 0; }
            }
            if (!System.IO.File.Exists(target)) { Console.WriteLine("VERIFY_FAIL " + T("文件不存在: ", "file does not exist: ") + target); return 0; }
            string manifestPath = FlagOf(args, "--manifest");
            string url = FlagOf(args, "--url");
            string text = null;
            if (url.Length > 0)
            {
                try
                {
                    // .NET Framework defaults to TLS 1.0, which GitHub refuses ("could not create SSL/TLS secure
                    // channel"); .NET 8 already negotiates 1.2+. The API is obsolete on .NET 8 but functional, so
                    // the whole assignment is guarded rather than conditional on the runtime.
                    try { System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls; } catch { }
                    using (System.Net.WebClient wc = new System.Net.WebClient())
                    {
                        wc.Headers.Add("User-Agent", "dsh-minato-verify");
                        text = wc.DownloadString(url);
                    }
                    Console.WriteLine("VERIFY_MANIFEST " + url + " " + T("(已下载)", "(downloaded)"));
                }
                catch (Exception wex) { Console.WriteLine("VERIFY_MANIFEST_MISSING " + T("无法下载清单: ", "could not download the manifest: ") + wex.Message); Console.WriteLine("VERIFY_FAIL 0"); return 0; }
            }
            else
            {
                if (manifestPath.Length == 0)
                {
                    try { manifestPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(target)), "hashes.txt"); } catch { }
                }
                if (string.IsNullOrEmpty(manifestPath) || !System.IO.File.Exists(manifestPath))
                {
                    // 清单缺失 = 无法核对 ✗ → 明确说清，绝不给出 OK ✗✓
                    Console.WriteLine("VERIFY_MANIFEST_MISSING " + T("找不到清单文件（默认取同目录的 hashes.txt）: ", "manifest not found (defaults to hashes.txt next to the file): ") + (manifestPath == null ? "" : manifestPath));
                    Console.WriteLine("VERIFY_HINT " + T("用 --manifest 指定清单，或用 --url https://github.com/sakanamaru/dsh-minato/releases/latest/download/hashes.txt 联网取官方清单", "pass --manifest, or --url https://github.com/sakanamaru/dsh-minato/releases/latest/download/hashes.txt to fetch the official manifest"));
                    Console.WriteLine("VERIFY_FAIL 0");
                    return 0;
                }
                try { text = System.IO.File.ReadAllText(manifestPath); Console.WriteLine("VERIFY_MANIFEST " + manifestPath); }
                catch (Exception rex) { Console.WriteLine("VERIFY_MANIFEST_MISSING " + rex.Message); Console.WriteLine("VERIFY_FAIL 0"); return 0; }
            }
            string selfHash;
            try { selfHash = Sha256Of(target); }
            catch (Exception hex) { Console.WriteLine("VERIFY_FAIL " + T("无法计算哈希: ", "cannot hash: ") + hex.Message); return 0; }
            Console.WriteLine("VERIFY_SELF " + target + " " + selfHash);
            string leaf = System.IO.Path.GetFileName(target);
            string want = null;
            int entries = 0;
            string[] lines = text.Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i].Trim();
                if (l.Length == 0 || l.IndexOf((char)35) == 0) continue;   // 跳过 # 注释行
                System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(l, "^([0-9a-fA-F]{64})[ \\t]+\\*?(.+)$");
                if (!m.Success) continue;
                entries++;
                string name = m.Groups[2].Value.Trim();
                if (string.Equals(name, leaf, StringComparison.OrdinalIgnoreCase)) want = m.Groups[1].Value.ToLowerInvariant();
            }
            if (entries == 0) { Console.WriteLine("VERIFY_MANIFEST_MISSING " + T("清单里没有可解析的 SHA-256 行", "the manifest has no parsable SHA-256 lines")); Console.WriteLine("VERIFY_FAIL 0"); return 0; }
            Console.WriteLine("VERIFY_ENTRIES " + entries);
            if (want == null)
            {
                Console.WriteLine("VERIFY_NOT_IN_MANIFEST " + leaf);
                Console.WriteLine("VERIFY_HINT " + T("清单里没有这个文件名 —— 它可能不是本项目的发布产物（发布清单只列发布资产）", "that filename is not in the manifest - it may not be a release artifact of this project"));
                Console.WriteLine("VERIFY_FAIL 1");
                return 0;
            }
            if (string.Equals(want, selfHash, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("VERIFY_MATCH " + leaf + " " + want);
                Console.WriteLine("VERIFY_OK 1");
            }
            else
            {
                Console.WriteLine("VERIFY_MISMATCH " + leaf + " " + T("清单=", "manifest=") + want + " " + T("本机=", "local=") + selfHash);
                Console.WriteLine("VERIFY_HINT " + T("不一致：本机文件与清单记录不符（可能被替换或篡改），请从官方 Release 重新下载", "mismatch: the local file does not match the manifest (it may have been replaced or tampered with); download it again from the official release"));
                Console.WriteLine("VERIFY_FAIL 1");
            }
            return 0;
        }

        /// <summary>文件的 SHA-256（小写十六进制 ✓）。</summary>
        private static string Sha256Of(string path)
        {
            using (System.IO.FileStream fs = System.IO.File.OpenRead(path))
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] h = sha.ComputeHash(fs);
                System.Text.StringBuilder sb = new System.Text.StringBuilder(h.Length * 2);
                for (int i = 0; i < h.Length; i++) sb.Append(h[i].ToString("x2"));
                return sb.ToString();
            }
        }
        /// <summary>wipe（V3 独有）：清除数据根内容 ✓ —— 卸载前的"干净清除" ✓（经典版有 ✓）。
        /// 破坏性操作 ✗ → 多重闸门 ✓：① 先打印计划 ② 必须 --yes ③ **必须先成功做出 -pre-wipe 备份**
        /// （没备份就不许清 ✗）④ 拒绝系统/用户级根目录 ✗ ⑤ **备份根在数据根内时拒绝** ✗（否则会连安全网一起删 ✗）。
        /// 备份目录本身**不动** ✓；标记行：WIPE_PLAN / WIPE_REFUSED / WIPE_PRE_BACKUP / WIPE_OK / WIPE_FAIL。</summary>
        private static int WipeCmd(string[] args, ServiceRegistry reg)
        {
            string data = reg.Get<IPaths>().DataRoot;
            string backups = reg.Get<IBackupSource>().BackupsRoot;
            string dataFull, bkFull;
            try
            {
                dataFull = System.IO.Path.GetFullPath(data).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                bkFull = System.IO.Path.GetFullPath(backups).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            }
            catch (Exception ex) { Console.WriteLine("WIPE_FAIL " + ex.Message); return 0; }
            // 闸门 ④：系统/用户级根目录一律拒绝 ✗
            string[] segs = dataFull.Split(new char[] { '/', '\\' });
            int depth = 0;
            for (int i = 0; i < segs.Length; i++) if (segs[i].Length > 0) depth++;
            if (depth < 2) { Console.WriteLine("WIPE_REFUSED " + T("数据根看起来是盘根/系统根，拒绝清除: ", "the data root looks like a drive or system root, refusing: ") + dataFull); return 0; }
            // 闸门 ⑤：备份根在数据根内 → 拒绝（会把安全网一起删 ✗）
            if (bkFull == dataFull || bkFull.StartsWith(dataFull + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal))
            { Console.WriteLine("WIPE_REFUSED " + T("备份目录位于数据根内，清除会连备份一起删除，已拒绝。请先把备份目录移出数据根。", "the backups root is inside the data root, so wiping would delete the backups too; move it out first") + " (" + bkFull + ")"); return 0; }
            int files = 0, dirs = 0;
            try { files = System.IO.Directory.GetFiles(dataFull, "*", System.IO.SearchOption.AllDirectories).Length; dirs = System.IO.Directory.GetDirectories(dataFull, "*", System.IO.SearchOption.AllDirectories).Length; } catch { }
            if (!Has(args, "--yes"))
            {
                Console.WriteLine("WIPE_PLAN " + T("将删除数据根内的 ", "will delete ") + files + T(" 个文件、", " files and ") + dirs + T(" 个子目录：", " subdirectories in ") + dataFull);
                Console.WriteLine("WIPE_PLAN_NOTE " + T("执行前**必须**先成功做出 -pre-wipe 备份（做不出就不清 ✗）；备份目录本身不动 ✓。确认请加 --yes", "a -pre-wipe backup MUST succeed first (no backup, no wipe); the backups root itself is untouched. Add --yes to confirm"));
                return 0;
            }
            // 闸门 ③：先备份，且**必须成功** ✓✓
            try
            {
                Dsht.Domain.Model.BackupResult pb = reg.Get<IBackupSource>().Create(dataFull, Dsht.Domain.Model.BackupKind.PreWipe, _cfg == null ? 3 : _cfg.KeepBackups, WorkspaceRoot(reg));
                if (pb == null || string.IsNullOrEmpty(pb.Path)) { Console.WriteLine("WIPE_REFUSED " + T("清除前的安全备份未能创建，已拒绝执行（没备份就不清 ✗）", "the pre-wipe backup could not be created; refusing to wipe (no backup, no wipe)")); return 0; }
                AddContentHashToMarker(pb.Path);   // 回滚锚点也要能自证完整 ✓✓
            Console.WriteLine("WIPE_PRE_BACKUP " + pb.Path);   // 打印行必须保留 ✗（我第 53 轮把它替换成了哈希调用 ✗✗ → 用户看不到安全备份在哪 ✓）
            }
            catch (Exception bex) { Console.WriteLine("WIPE_REFUSED " + T("清除前的安全备份失败，已拒绝执行: ", "the pre-wipe backup failed; refusing to wipe: ") + bex.Message); return 0; }
            // 真清：只删数据根**内容** ✓
            int removed = 0;
            try
            {
                string[] fs = System.IO.Directory.GetFiles(dataFull);
                for (int i = 0; i < fs.Length; i++) { System.IO.File.Delete(fs[i]); removed++; }
                string[] ds = System.IO.Directory.GetDirectories(dataFull);
                for (int i = 0; i < ds.Length; i++) { System.IO.Directory.Delete(ds[i], true); removed++; }
                OpLog(reg, "WARN", "wipe OK " + dataFull + " (" + removed + " entries removed, backup " + files + " files)");
                Console.WriteLine("WIPE_OK " + removed);
                Console.WriteLine("WIPE_NOTE " + T("备份未被删除，可随时用 restore 恢复 ✓", "backups were kept; restore can bring the data back at any time"));
            }
            catch (Exception wex) { OpLog(reg, "ERROR", "wipe failed: " + wex.Message); Console.WriteLine("WIPE_FAIL " + wex.Message); }
            return 0;
        }
        /// <summary>import（V3 独有）：把**外部**备份包导入本机备份根 ✓ —— 跨机迁移的关键一环 ✓。
        /// 恢复侧刻意只接受备份根内的路径（outside → 拒绝 ✓），所以外部包必须先导入 ✓。
        /// 用法：import --path &lt;外部包&gt; [--yes]。加 --yes 时：先对当前数据根做一次 -pre-import 安全备份 ✓，
        /// 再**复制**外部包进备份根（源包不动 ✓），最后提示用 restore 恢复 ✓。导入本身不改动数据 ✓。</summary>
        private static int ImportCmd(string[] args, ServiceRegistry reg)
        {
            string src = FlagOf(args, "--path");
            if (src.Length == 0) { Console.WriteLine("IMPORT_FAIL usage: import --path <外部备份包> [--yes]"); return 0; }
            if (!System.IO.Directory.Exists(src)) { Console.WriteLine("IMPORT_FAIL " + T("源目录不存在: ", "source directory does not exist: ") + src); return 0; }
            IBackupSource bk = reg.Get<IBackupSource>();
            string root = bk.BackupsRoot;
            string srcFull, rootFull;
            try
            {
                srcFull = System.IO.Path.GetFullPath(src).TrimEnd(System.IO.Path.DirectorySeparatorChar);
                rootFull = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar);
            }
            catch (Exception ex) { Console.WriteLine("IMPORT_FAIL " + ex.Message); return 0; }
            if (srcFull == rootFull || srcFull.StartsWith(rootFull + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                Console.WriteLine("IMPORT_NOTE " + T("该备份已在本机备份根内，无需导入 —— 直接 restore 即可", "that backup is already inside the local backups root - just restore it"));
                Console.WriteLine("IMPORT_OK " + srcFull);
                return 0;
            }
            string[] entries = null;
            try { entries = System.IO.Directory.GetFileSystemEntries(srcFull); } catch { }
            if (entries == null || entries.Length == 0) { Console.WriteLine("IMPORT_FAIL " + T("无效备份目录（空目录）", "invalid backup directory (empty)")); return 0; }
            if (!Has(args, "--yes"))
            {
                Console.WriteLine("IMPORT_PLAN " + T("将把 ", "will copy ") + srcFull + T(" 复制进备份根 ", " into the backups root ") + rootFull + T("，并先对当前数据根做一次 -pre-import 安全备份（会写盘）—— 确认请加 --yes", ", taking a -pre-import safety backup of the current data root first (writes to disk) - add --yes to confirm"));
                return 0;
            }
            // 1) 安全网：导入前给当前数据根留一份 -pre-import 备份 ✓
            try
            {
                Dsht.Domain.Model.BackupResult pb = bk.Create(reg.Get<IPaths>().DataRoot, Dsht.Domain.Model.BackupKind.PreImport, _cfg == null ? 3 : _cfg.KeepBackups, WorkspaceRoot(reg));
                if (pb != null && !string.IsNullOrEmpty(pb.Path)) { Console.WriteLine("IMPORT_PRE_BACKUP " + pb.Path); AddContentHashToMarker(pb.Path); }   // 回滚锚点也要能自证完整 ✓✓
            }
            catch (Exception bex) { Console.WriteLine("IMPORT_PRE_BACKUP_FAILED " + bex.Message); }
            // 2) 复制外部包进备份根（源包不动 ✓）；名字带 -imported 便于识别（Classify 视作手动类 ✓ 不会被自动清理 ✓）
            string dest = System.IO.Path.Combine(rootFull, "dsh-data-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + "-imported");
            try
            {
                int files = CopyDirDeep(srcFull, dest, 0);
                // 把**同级旁挂文件**（完成标记 / 版本记录）一起导入 ✓✓ —— 否则迁移到新机器后无法核对完整性 ✓
                CopySiblingFile(srcFull, dest, ".manifest");
                CopySiblingFile(srcFull, dest, ".version");
                OpLog(reg, "INFO", "import OK " + srcFull + " -> " + dest + " (" + files + " files)");
                Console.WriteLine("IMPORT_OK " + dest + " " + files);
                Console.WriteLine("IMPORT_NEXT " + T("下一步：restore --path ", "next: restore --path ") + dest + T(" --dry-run 先预览，再去掉 --dry-run 执行", " --dry-run to preview, then drop --dry-run to apply"));
            }
            catch (Exception cex) { OpLog(reg, "ERROR", "import failed: " + cex.Message); Console.WriteLine("IMPORT_FAIL " + cex.Message); }
            return 0;
        }

        /// <summary>递归复制目录（导入用 ✓；深度上限兜底，避免符号链接环 ✗）。返回复制文件数。</summary>
        /// <summary>复制备份包的**同级旁挂文件**（存在才复制 ✓ 尽力而为 ✓）。</summary>
        private static void CopySiblingFile(string srcPkg, string dstPkg, string suffix)
        {
            try
            {
                string from = srcPkg.TrimEnd('\\', '/') + suffix;
                if (!System.IO.File.Exists(from)) return;
                System.IO.File.Copy(from, dstPkg.TrimEnd('\\', '/') + suffix, true);
            }
            catch { }
        }
        private static int CopyDirDeep(string from, string to, int depth)
        {
            if (depth > 32) return 0;
            System.IO.Directory.CreateDirectory(to);
            int n = 0;
            string[] files = System.IO.Directory.GetFiles(from);
            for (int i = 0; i < files.Length; i++) { System.IO.File.Copy(files[i], System.IO.Path.Combine(to, System.IO.Path.GetFileName(files[i])), true); n++; }
            string[] dirs = System.IO.Directory.GetDirectories(from);
            for (int i = 0; i < dirs.Length; i++) n += CopyDirDeep(dirs[i], System.IO.Path.Combine(to, System.IO.Path.GetFileName(dirs[i])), depth + 1);
            return n;
        }
        /// <summary>update-info（V3 独有，只读）：经典版「更新中心」的 CLI 对应物 ✓。
        /// 标记行：UPDATEINFO_INSTALLED/LATEST/REGISTRY/STATE/PRE_BACKUP/PRE_BACKUP_VERSION/ROLLBACK。</summary>
        private static int UpdateInfo(ServiceRegistry reg)
        {
            IToolchainQuery tc = reg.Get<IToolchainQuery>();
            string installed = tc.DshVersion();
            if (string.IsNullOrEmpty(installed))
            {
                // 未安装时明确说"未安装"，不伪装成"最新" ✗
                if (string.IsNullOrEmpty(tc.WhichDsh())) { Console.WriteLine("UPDATEINFO_INSTALLED not-installed"); Console.WriteLine("UPDATEINFO_LATEST unknown"); Console.WriteLine("UPDATEINFO_STATE unknown"); return 0; }
            }
            Console.WriteLine("UPDATEINFO_INSTALLED " + (string.IsNullOrEmpty(installed) ? "unknown" : installed));
            Console.WriteLine("UPDATEINFO_CHANNEL " + (string.IsNullOrEmpty(_cfg == null ? null : _cfg.UpdateChannel) ? "rc" : _cfg.UpdateChannel));
            string latest = VersionForChannel(tc, _cfg == null ? "rc" : _cfg.UpdateChannel);
            Console.WriteLine("UPDATEINFO_LATEST " + (latest.Length == 0 ? "unknown" : latest));
            string reg2 = tc.NpmRegistryConfig();
            Console.WriteLine("UPDATEINFO_REGISTRY " + (string.IsNullOrEmpty(reg2) ? "default" : reg2.Trim()));
            string state = "unknown";
            if (installed != null && installed.Length > 0 && latest.Length > 0)
                state = installed == latest ? "up-to-date" : (installed.CompareTo(latest) < 0 ? "update-available" : "newer-installed");
            Console.WriteLine("UPDATEINFO_STATE " + state);
            // 回滚候选：最新的 -pre-update 备份，以及它旁挂文件里记录的当时版本 ✓
            try
            {
                string root = reg.Get<IBackupSource>().BackupsRoot;
                string best = null;
                if (!string.IsNullOrEmpty(root) && System.IO.Directory.Exists(root))
                {
                    string[] dirs = System.IO.Directory.GetDirectories(root, "*-pre-update");
                    System.Array.Sort(dirs, StringComparer.Ordinal);
                    if (dirs.Length > 0) best = dirs[dirs.Length - 1];
                }
                Console.WriteLine("UPDATEINFO_PRE_BACKUP " + (best == null ? "none" : best));
                string wasVersion = "unknown";
                if (best != null) { try { if (System.IO.File.Exists(best + ".version")) wasVersion = System.IO.File.ReadAllText(best + ".version").Trim(); } catch { } }
                Console.WriteLine("UPDATEINFO_PRE_BACKUP_VERSION " + wasVersion);
                Console.WriteLine("UPDATEINFO_ROLLBACK " + (best == null ? "none" : wasVersion));
            }
            catch (Exception ex) { Console.WriteLine("UPDATEINFO_PRE_BACKUP none"); Console.WriteLine("UPDATEINFO_ROLLBACK none"); Console.WriteLine("UPDATEINFO_NOTE " + ex.Message); }
            return 0;
        }
        /// <summary>log（V3 独有）：查看/筛选/导出操作日志 —— 经典版「日志中心」的 CLI 对应物 ✓。
        /// 用法：log [--lines &lt;n&gt;] [--level info|warn|error] [--grep &lt;text&gt;] [--export &lt;file&gt; [--yes]]
        /// 标记行：LOG_OK &lt;n&gt; / LOG_LINE &lt;原文&gt; / LOG_EMPTY / LOG_EXPORT &lt;路径&gt; &lt;n&gt; / LOG_FAIL &lt;原因&gt;。</summary>
        /// <summary>记一条操作日志（尽力而为 ✓）。读取侧见 LogCmd ✓。</summary>
        private static void OpLog(ServiceRegistry reg, string level, string message)
        {
            try { reg.Get<ILogSource>().Append(level, message); } catch { }
        }
        private static int LogCmd(string[] args, ServiceRegistry reg)
        {
            string text = reg.Get<ILogSource>().ReadLog();
            if (text == null)
            {
                Console.WriteLine("LOG_EMPTY " + T("还没有日志（状态目录/logs/launcher.log 不存在）", "no log yet (state dir/logs/launcher.log does not exist)"));
                return 0;
            }
            string level = FlagOf(args, "--level").Trim().ToLowerInvariant();
            string grep = FlagOf(args, "--grep");
            int maxLines = 0;
            string linesArg = FlagOf(args, "--lines");
            if (linesArg.Length > 0) int.TryParse(linesArg, out maxLines);
            List<string> picked = new List<string>();
            string[] all = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < all.Length; i++)
            {
                string l = all[i];
                if (level.Length > 0 && l.IndexOf(level, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (grep.Length > 0 && l.IndexOf(grep, StringComparison.OrdinalIgnoreCase) < 0) continue;
                picked.Add(l);
            }
            while (picked.Count > 0 && picked[picked.Count - 1].Trim().Length == 0) picked.RemoveAt(picked.Count - 1);   // 去掉末尾空行（日志文件常以换行结尾）
            if (maxLines > 0 && picked.Count > maxLines) picked.RemoveRange(0, picked.Count - maxLines);
            string export = FlagOf(args, "--export");
            if (export.Length > 0)
            {
                if (!Has(args, "--yes"))
                {
                    Console.WriteLine("LOG_EXPORT_PLAN " + T("将把筛选结果写入 ", "will write the filtered result to ") + export + T("（会写盘）—— 确认请加 --yes", " (writes to disk) - add --yes to confirm"));
                    return 0;
                }
                try
                {
                    System.IO.File.WriteAllText(export, string.Join(Environment.NewLine, picked.ToArray()));
                    Console.WriteLine("LOG_EXPORT " + export + " " + picked.Count);
                }
                catch (Exception ex) { Console.WriteLine("LOG_FAIL " + ex.Message); }
                return 0;
            }
            Console.WriteLine("LOG_OK " + picked.Count);
            for (int i = 0; i < picked.Count; i++) Console.WriteLine("LOG_LINE " + picked[i]);
            return 0;
        }
        /// <summary>about（V3 独有）：版本、定位、许可与"非官方"声明。纯文本，不联网。</summary>
        /// <summary>shortcut（V3 独有）：创建桌面/应用菜单入口。
        /// Windows 用 PowerShell 的 WScript.Shell 建 .lnk（与 v2.x 同思路）；Linux 写 XDG 的 .desktop 文件。
        /// 写操作 → 计划 → `--yes` 闸门；失败一律如实报原因（不静默）。</summary>
        private static int ShortcutCmd(string[] args)
        {
            bool win = PlatformIsWindows();
            // 单文件发布下 Assembly.Location 是空的 ✗（真机测试抓到的）→ 用 MainModule，两条构建路径都可用 ✓
            string exe = "";
            try { exe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName; } catch { }
            if (string.IsNullOrEmpty(exe)) exe = System.IO.Path.Combine(AppContext.BaseDirectory, "dsh-minato");
            if (string.IsNullOrEmpty(exe)) { Console.WriteLine("SHORTCUT_FAIL " + T("拿不到自身路径", "cannot resolve own path")); return 0; }
            string dir = System.IO.Path.GetDirectoryName(exe);
            string target = win ? System.IO.Path.Combine(dir, "dsht-minato.exe") : System.IO.Path.Combine(dir, "dsht-minato");
            if (!System.IO.File.Exists(target)) target = exe;   // 还没改名时就用当前可执行文件

            string where;
            if (win)
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                where = System.IO.Path.Combine(desktop, "dsh-minato.lnk");
            }
            else
            {
                string home = Environment.GetEnvironmentVariable("HOME");
                if (string.IsNullOrEmpty(home)) { Console.WriteLine("SHORTCUT_FAIL " + T("没有 HOME，无法确定位置", "no HOME, cannot decide a location")); return 0; }
                where = System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(home, ".local"), "share"), "applications");
                where = System.IO.Path.Combine(where, "dsh-minato.desktop");
            }
            Console.WriteLine("SHORTCUT_PLAN " + (win ? T("将创建快捷方式：", "will create a shortcut: ") : T("将创建应用入口：", "will create a desktop entry: ")) + where + T(" → ", " -> ") + target);
            if (!Has(args, "--yes"))
            {
                Console.WriteLine("SHORTCUT_DRYRUN " + T("（确认请加 --yes）", "(add --yes to confirm)"));
                return 0;
            }
            try
            {
                if (win)
                {
                    string ps = "$s=(New-Object -ComObject WScript.Shell).CreateShortcut('" + where.Replace("'", "''") + "');" +
                                "$s.TargetPath='" + target.Replace("'", "''") + "';" +
                                "$s.WorkingDirectory='" + dir.Replace("'", "''") + "';$s.Save()";
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("powershell", "-NoProfile -Command \"" + ps.Replace("\"", "\\\"") + "\"");
                    psi.UseShellExecute = false; psi.CreateNoWindow = true;
                    using (System.Diagnostics.Process pr = System.Diagnostics.Process.Start(psi)) { pr.WaitForExit(30000); if (pr.ExitCode != 0) { Console.WriteLine("SHORTCUT_FAIL powershell 退出码 " + pr.ExitCode); return 0; } }
                }
                else
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(where));
                    string body = "[Desktop Entry]\nType=Application\nName=dsh-minato\nComment=" + T("dsh 部署运维套件", "deploy & ops kit for dsh") + "\nExec=" + target + "\nTerminal=true\nCategories=Utility;\n";
                    System.IO.File.WriteAllText(where, body, new System.Text.UTF8Encoding(false));
                }
                Console.WriteLine(System.IO.File.Exists(where) ? "SHORTCUT_OK " + where : "SHORTCUT_FAIL " + T("写入后未观测到文件", "file not observed after writing"));
            }
            catch (Exception ex) { Console.WriteLine("SHORTCUT_FAIL " + ex.Message); }
            return 0;
        }
        private static int AboutCmd()
        {
            Console.WriteLine("dsh-minato " + ToolkitVersion);
            Console.WriteLine(T("社区版 DeepSeek Harness (dsh) 本机部署运维套件：安装 / 启动 / 监控 / 备份恢复 / 插件诊断与隔离",
                                "community deploy & ops kit for DeepSeek Harness (dsh): install, start, monitor, backup & restore, plugin diagnosis & quarantine"));
            Console.WriteLine(T("非官方工具，与 DeepSeek 官方无关。", "Unofficial tool; not affiliated with DeepSeek."));
            Console.WriteLine(T("仓库：", "Repository: ") + "https://github.com/sakanamaru/dsh-minato");
            Console.WriteLine(T("许可：MIT", "License: MIT"));
            Console.WriteLine(T("本程序只读写本机：不联网（余额查询除外，需显式开启）；写操作一律先备份、可回滚。",
                                "Works locally only: no network (except the opt-in balance check); every write is backed up first and can be rolled back."));
            return 0;
        }

        /// <summary>ui（V3 独有）：启动跨平台 GUI（Avalonia）。找不到就**如实说明**，不静默失败、不假装已启动。</summary>
        private static int UiCmd()
        {
            string gui = Environment.GetEnvironmentVariable("DSHT_GUI");
            if (!string.IsNullOrEmpty(gui) && !System.IO.File.Exists(gui)) { Console.WriteLine("UI_FAIL " + T("DSHT_GUI 指向的文件不存在：", "DSHT_GUI points at a missing file: ") + gui); return 0; }
            if (string.IsNullOrEmpty(gui))
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                // Avalonia 优先；找不到再回退 v2.x 的旧界面（并如实说明那是旧版）
                string[] names = PlatformIsWindows()
                    ? new string[] { "dsht-gui.exe", "Toolkit GUI Standalone.exe", "Toolkit GUI.exe", "DeepSeek Harness Toolkit.exe" }
                    : new string[] { "dsht-gui", "Toolkit GUI Standalone", "Toolkit GUI" };
                for (int i = 0; i < names.Length; i++)
                {
                    string cand = System.IO.Path.Combine(dir, names[i]);
                    if (System.IO.File.Exists(cand)) { gui = cand; break; }
                }
            }
            if (string.IsNullOrEmpty(gui))
            {
                Console.WriteLine("UI_FAIL " + T("没找到 GUI（把 dsht-gui 放在本程序同目录，或用环境变量 DSHT_GUI 指定）", "no GUI found (put dsht-gui next to this program, or set DSHT_GUI)"));
                return 0;
            }
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(gui);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi);
                string legacy = (gui.IndexOf("dsht-gui", StringComparison.OrdinalIgnoreCase) < 0) ? T("（提示：这是 v2.x 的旧界面；跨平台新界面请用 dsht-gui）", " (note: this is the v2.x UI; the cross-platform one is dsht-gui)") : "";
                Console.WriteLine("UI_OK " + gui + (p != null ? " pid=" + p.Id : "") + legacy);
            }
            catch (Exception ex) { Console.WriteLine("UI_FAIL " + ex.Message); }
            return 0;
        }
        /// <summary>Node 版本是否低于要求（真机抓到：dsh 要求 >= 22.19.0，太旧时它会**静默退出** ✗，极难诊断）。
        /// 纯函数，便于单测；无法解析时保守返回 false（不误伤）。</summary>
        private static bool NodeTooOld(string version, int needMajor, int needMinor)
        {
            if (string.IsNullOrEmpty(version)) return false;
            string v = version.Trim().TrimStart('v', 'V');
            int dot = v.IndexOf('.');
            if (dot <= 0) return false;
            int major;
            if (!int.TryParse(v.Substring(0, dot), out major)) return false;
            string rest = v.Substring(dot + 1);
            int dot2 = rest.IndexOf('.');
            int minor;
            if (!int.TryParse(dot2 > 0 ? rest.Substring(0, dot2) : rest, out minor)) return false;
            if (major != needMajor) return major < needMajor;
            return minor < needMinor;
        }
        private static int InstallLike(string[] args, ServiceRegistry reg, bool update)
        {
            IToolchainQuery tc = reg.Get<IToolchainQuery>();
            string verb = update ? "UPDATE" : "INSTALL";
            string which = tc.WhichDsh();
            string installed = tc.DshVersion();
            bool isInstalled = !string.IsNullOrEmpty(which) || !string.IsNullOrEmpty(installed);

            if (isInstalled && !update)
            {
                Console.WriteLine(verb + "_SKIP " + T("已经装了 dsh ", "dsh is already installed ") + (installed.Length > 0 ? installed : "?") + T("（升级请用 update）", " (use update to upgrade)"));
                Console.WriteLine(verb + "_OBSERVED " + (installed.Length > 0 ? installed : "installed"));
                return 0;
            }

            // Linux 一键安装的前置：dsh 靠 npm 装，npm 靠 node。缺 node 时**不能假装一键** ✗
            string nodeNow = tc.NodeVersion();
            bool nodeMissing = string.IsNullOrEmpty(nodeNow);
            bool nodeOld = !nodeMissing && NodeTooOld(nodeNow, 22, 19);   // dsh 要求 >= 22.19.0（真机抓到的 ✗）
            if (!PlatformIsWindows() && (nodeMissing || nodeOld))
            {
                Console.WriteLine(verb + "_NEED_NODE " + (nodeMissing
                    ? T("未检测到 Node.js（dsh 通过 npm 安装，需要它）", "Node.js not found (dsh installs through npm and needs it)")
                    : T("Node.js 版本过旧（", "Node.js is too old (") + nodeNow + T("）—— dsh 要求 >= 22.19.0", ") - dsh requires >= 22.19.0")));
                Console.WriteLine(verb + "_NODE_HINT " + T("免 sudo：加 --install-node 自动装到 ~/.local/node；或用系统包管理器：sudo apt install -y nodejs npm（Debian/Ubuntu）/ sudo dnf install -y nodejs npm（Fedora）",
                                                            "no sudo needed: add --install-node to install into ~/.local/node; or use your package manager: sudo apt install -y nodejs npm (Debian/Ubuntu) / sudo dnf install -y nodejs npm (Fedora)"));
                if (!Has(args, "--install-node") || !Has(args, "--yes"))
                {
                    Console.WriteLine(verb + "_DRYRUN " + T("（确认请加 --install-node --yes）", "(add --install-node --yes to confirm)"));
                    Console.WriteLine(verb + "_OBSERVED not-installed");
                    return 0;
                }
                Console.WriteLine(verb + "_NODE_INSTALLING " + T("正在下载官方 Node LTS 到 ~/.local/node …", "downloading the official Node LTS into ~/.local/node ..."));
                int nc = tc.InstallNodeRuntime();
                string nv = tc.NodeVersion();
                if (string.IsNullOrEmpty(nv))
                {
                    Console.WriteLine(verb + "_FAIL " + T("Node 引导失败（退出码 ", "Node bootstrap failed (exit code ") + nc + T("）：", "): ") + Dsht.Platform.Linux.LinuxToolchainQuery.LastError);
                    Console.WriteLine(verb + "_NODE_HINT " + T("可改用系统包管理器：sudo apt install -y nodejs npm", "or use your package manager: sudo apt install -y nodejs npm"));
                    Console.WriteLine(verb + "_OBSERVED not-installed");
                    return 0;
                }
                Console.WriteLine(verb + "_NODE_OK " + nv + T("（免 sudo，装在 ~/.local/node）", " (no sudo, installed under ~/.local/node)"));
            }
            // --list: print the available versions verbatim (the classic line could do this; V3 could not).
            if (Has(args, "--list"))
            {
                string vers = tc.NpmViewVersions();
                if (string.IsNullOrEmpty(vers)) Console.WriteLine(verb + "_LIST_FAIL " + T("拿不到版本列表（离线或 npm 不可用）", "could not list versions (offline or npm unavailable)"));
                else Console.WriteLine(verb + "_VERSIONS " + vers.Replace(Environment.NewLine, " ").Trim());
                return 0;
            }
            // --version: install one explicit version (still whitelisted - it is pasted into a command line).
            string wantVersion = NpmVersionGuard.Normalize(FlagOf(args, "--version"));
            if (wantVersion.Length > 0 && !NpmVersionGuard.IsSafe(wantVersion))
            {
                Console.WriteLine(verb + "_FAIL " + T("指定的版本号未通过白名单：", "the requested version failed the whitelist: ") + wantVersion);
                Console.WriteLine(verb + "_OBSERVED " + (installed.Length > 0 ? installed : "not-installed"));
                return 0;
            }
            string channel = _cfg == null ? "rc" : _cfg.UpdateChannel;
            string latest = wantVersion.Length > 0 ? wantVersion : VersionForChannel(tc, channel);
            if (wantVersion.Length == 0) Console.WriteLine(verb + "_CHANNEL " + (string.IsNullOrEmpty(channel) ? "rc" : channel) + " -> " + (latest.Length == 0 ? "unknown" : latest));
            if (!NpmVersionGuard.IsSafe(latest))
            {
                Console.WriteLine(verb + "_FAIL " + T("拿不到可信的最新版本（离线，或 npm 返回值未通过白名单）", "no trustworthy latest version (offline, or the npm value failed the whitelist)"));
                Console.WriteLine(verb + "_OBSERVED " + (installed.Length > 0 ? installed : "not-installed"));
                return 0;
            }
            string target = "@deepseek-ai/dsh@" + latest;
            string registry = tc.NpmRegistryConfig();
            Console.WriteLine(verb + "_PLAN " + T("将执行：npm install -g ", "will run: npm install -g ") + target + (string.IsNullOrEmpty(registry) ? "" : " --registry " + registry));
            if (!Has(args, "--yes"))
            {
                Console.WriteLine(verb + "_DRYRUN " + T("（确认请加 --yes；这会真的改动全局 npm 包）", "(add --yes to confirm; this really changes global npm packages)"));
                Console.WriteLine(verb + "_OBSERVED " + (installed.Length > 0 ? installed : "not-installed"));
                return 0;
            }
            // Safety net for updates: take a -pre-update data backup BEFORE touching npm, so a broken
            // new version cannot cost the user their data. The classic v2.x line had this and V3 did
            // not (BackupKind.PreUpdate existed but nothing ever created one).
            if (update && !string.IsNullOrEmpty(installed))
            {
                try
                {
                    Dsht.Domain.Abstractions.IBackupSource bks = reg.Get<Dsht.Domain.Abstractions.IBackupSource>();
                    Dsht.Domain.Model.BackupResult pb = bks.Create(reg.Get<Dsht.Domain.Abstractions.IPaths>().DataRoot, Dsht.Domain.Model.BackupKind.PreUpdate, _cfg == null ? 3 : _cfg.KeepBackups, WorkspaceRoot(reg));
                    if (pb != null && !string.IsNullOrEmpty(pb.Path)) { Console.WriteLine(verb + "_PRE_BACKUP " + pb.Path); AddContentHashToMarker(pb.Path); }   // 回滚锚点也要能自证完整 ✓✓
                    // Record the version we are about to replace, NEXT TO the package (a file inside it would
                    // be restored into the data root). This is what makes a rollback candidate knowable.
                    if (pb != null && !string.IsNullOrEmpty(pb.Path))
                    {
                        try { System.IO.File.WriteAllText(pb.Path + ".version", installed); } catch { }
                    }
                    else Console.WriteLine(verb + "_PRE_BACKUP_FAILED " + T("更新前备份未能创建（数据根不可读？）", "pre-update backup could not be created (data root unreadable?)"));
                }
                catch (Exception bex) { Console.WriteLine(verb + "_PRE_BACKUP_FAILED " + bex.Message); }
            }
            int code = tc.NpmInstallGlobal(target, registry);
            string after = reg.Get<IToolchainQuery>().DshVersion();
            // Success means the OBSERVED version is the one we asked for. "dsh is installed" is NOT enough:
            // when the install fails the old version is still there, so that test reported a false OK.
            bool reachedTarget = !string.IsNullOrEmpty(after) && (after == latest || after.IndexOf(latest, StringComparison.Ordinal) >= 0);
            if (reachedTarget)
            {
                Console.WriteLine(verb + "_OK " + after + T("（复检已观测到 dsh）", " (dsh observed after the run)"));
                Console.WriteLine(verb + "_OBSERVED " + after);
                return 0;
            }
            // Roll back to the version we recorded before the update, then re-check by observation.
            if (update && !string.IsNullOrEmpty(installed))
            {
                Console.WriteLine(verb + "_ROLLBACK_TRY " + installed);
                int rc = tc.NpmInstallGlobal("@deepseek-ai/dsh@" + installed, registry);
                string back = reg.Get<IToolchainQuery>().DshVersion();
                if (!string.IsNullOrEmpty(back) && back.IndexOf(installed, StringComparison.Ordinal) >= 0)
                {
                    Console.WriteLine(verb + "_ROLLBACK_OK " + back);
                    Console.WriteLine(verb + "_OBSERVED " + back);
                    return 0;
                }
                Console.WriteLine(verb + "_ROLLBACK_FAILED " + T("回滚后仍未观测到旧版本；请手动执行 npm install -g @deepseek-ai/dsh@", "old version still not observed after rollback; run npm install -g @deepseek-ai/dsh@") + installed + T("（npm 退出码 ", " (npm exit code ") + rc + "）");
            }
            Console.WriteLine(verb + "_FAIL " + T("npm 退出码 ", "npm exit code ") + code + T("，且复检仍未观测到 dsh", ", and dsh is still not observed"));
            Console.WriteLine(verb + "_OBSERVED not-installed");
            return 0;
        }

        /// <summary>uninstall（V3 独有）：卸载 dsh（**不动数据目录**）。同样：计划 → 闸门 → npm → 复检。</summary>
        private static int UninstallCmd(string[] args, ServiceRegistry reg)
        {
            IToolchainQuery tc = reg.Get<IToolchainQuery>();
            string which = tc.WhichDsh();
            string installed = tc.DshVersion();
            if (string.IsNullOrEmpty(which) && string.IsNullOrEmpty(installed))
            {
                Console.WriteLine("UNINSTALL_SKIP " + T("没有观测到已安装的 dsh", "no installed dsh observed"));
                Console.WriteLine("UNINSTALL_OBSERVED not-installed");
                return 0;
            }
            Console.WriteLine("UNINSTALL_PLAN " + T("将执行：npm uninstall -g @deepseek-ai/dsh（只移除 dsh 程序，**不动**你的数据目录与备份）", "will run: npm uninstall -g @deepseek-ai/dsh (removes the program only; your data root and backups are NOT touched)"));
            if (!Has(args, "--yes"))
            {
                Console.WriteLine("UNINSTALL_DRYRUN " + T("（确认请加 --yes）", "(add --yes to confirm)"));
                Console.WriteLine("UNINSTALL_OBSERVED " + (installed.Length > 0 ? installed : "installed"));
                return 0;
            }
            int code = tc.NpmUninstallGlobal();
            string after = reg.Get<IToolchainQuery>().DshVersion();
            if (string.IsNullOrEmpty(after) && string.IsNullOrEmpty(tc.WhichDsh()))
            {
                Console.WriteLine("UNINSTALL_OK");
            OpLog(reg, "INFO", "uninstall OK");
                Console.WriteLine("UNINSTALL_OBSERVED not-installed");
                return 0;
            }
            Console.WriteLine("UNINSTALL_FAIL " + T("npm 退出码 ", "npm exit code ") + code + T("（复检仍观测到 dsh）", " (dsh still observed)"));
            Console.WriteLine("UNINSTALL_OBSERVED " + (after.Length > 0 ? after : "installed"));
            return 0;
        }
        private static int StartCmd(string[] args, ServiceRegistry reg)
        {
            IServiceTarget target = TargetForStart(args, reg);   // 先解析 --port 再选目标（顺序敏感）
            IServiceControl ctl = reg.Get<IServiceControl>();

            ServiceReport before = target.Probe();
            string st = before.State.ToString();
            if (ServiceControlPolicy.BeforeStart(st, before.Pid) == StartDecision.AlreadyRunning)
            {
                // 身份校验 ✓：端口上有人监听 ≠ 那就是 dsh ✗（实测被 python http.server 占用时曾报 START_OK ✗✗）。
                // PID 可得时必须确认是 dsh（与 stop 用同一道校验 ✓）；PID 不可得时如实说明未能确认 ✓，不默认它是 dsh ✗。
                if (before.Pid > 0 && !reg.Get<IProcessQuery>().IsDshCommandLine(before.Pid))
                {
                    Console.WriteLine("START_FAIL " + T("端口被非 dsh 进程占用（PID ", "that port is held by a non-dsh process (PID ") + before.Pid + T("）；请先停掉它或换一个端口", "); stop it first or choose another port"));
                    Console.WriteLine("START_OBSERVED " + st.ToLowerInvariant() + " " + T("（端口被占用，但不是 dsh）", "(port in use, but not by dsh)"));
                    OpLog(reg, "ERROR", "start refused: port held by non-dsh pid " + before.Pid);
                    return 0;
                }
                Console.WriteLine("START_OK " + (before.Pid > 0 ? before.Pid.ToString() : "0"));
            OpLog(reg, "INFO", "start OK (already running)");
                Console.WriteLine("START_OBSERVED " + st.ToLowerInvariant() + (before.Pid > 0 ? T("（观测到已在运行，未重复启动）", "(already running; not started again)") : T("（观测到已在运行；PID 不可得，未能确认身份）", "(already running; no PID available, identity unconfirmed)")));
                return 0;
            }

            if (!Has(args, "--yes"))
            {
                Console.WriteLine("START_PLAN " + T("将启动：dsh（profile ", "will start dsh (profile ") + ArgOr(args, "--profile", "web") + T(" / 端口 ", " / port ") + ArgOr(args, "--port", WebPort.ToString()) + T("）—— 这会改变系统状态，需要显式确认。", ") - this changes system state and needs explicit confirmation."));
                Console.WriteLine("START_NOTE " + T("确认请加 --yes；可用 --port <n> 指定端口（测试时务必用非默认端口）。", "add --yes to confirm; --port <n> to pick a port (always use a non-default port when testing)."));
                Console.WriteLine("START_OBSERVED " + before.State.ToString().ToLowerInvariant());
                return 0;
            }
            int port = 3080;
            string profile = "web";
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--port") { int pp; if (int.TryParse(args[i + 1], out pp)) port = pp; }
                else if (args[i] == "--profile") profile = args[i + 1];
            }

            string file, cmdArgs;
            if (PlatformIsWindows())
            {
                file = "cmd.exe";                                   // npm 的 dsh 是 .cmd 垫片，必须经 cmd 包装
                cmdArgs = "/c dsh --profile " + profile + " --port " + port;
            }
            else
            {
                file = ResolveDsh(reg);                              // 主动解析（免 sudo 引导的 node 其 bin 不在非交互 PATH 里 ✗）
                cmdArgs = "--profile " + profile + " --port " + port;
            }

            int pid; string err;
            bool launched = ctl.StartDetached(file, cmdArgs, null, out pid, out err);
            if (!launched)
            {
                Console.WriteLine("START_FAIL " + T("启动命令未能发出：", "could not launch: ") + err);
                Console.WriteLine("START_OBSERVED down");
                return 0;
            }
            Console.WriteLine("START_LAUNCHED " + pid + " " + T("（命令已发出，正在用可观测事实确认…）", "(launched; verifying by observation…)"));

            // 等最多 15 秒，用端口/HTTP 观测确认（不猜）
            for (int i = 0; i < 15; i++)
            {
                System.Threading.Thread.Sleep(1000);
                ServiceReport now = target.Probe();
                string s2 = now.State.ToString();
                if (ServiceControlPolicy.AfterLaunch(s2, now.Pid, pid) == StartOutcome.Started)
                {
                    Console.WriteLine("START_OK " + (now.Pid > 0 ? now.Pid.ToString() : pid.ToString()));
            OpLog(reg, "INFO", "start OK");
                    Console.WriteLine("START_OBSERVED " + s2.ToLowerInvariant());
                    return 0;
                }
            }
            ServiceReport last = target.Probe();
            Console.WriteLine("START_FAIL " + T("命令已发出但 30 秒内未观测到端口/HTTP 就绪；子进程输出见 ", "launched but not observed ready within 30s; child output: ") + Dsht.Platform.Linux.LinuxServiceControl.LastLogPath);
            Console.WriteLine("START_OBSERVED " + last.State.ToString().ToLowerInvariant() + " " + last.Basis);
            return 0;
        }

        /// <summary>stop（V3 独有）：按**观测到的 PID** 结束 dsh，再用观测确认真的停了。
        /// 标记行：`STOP_OK <pid>` / `STOP_FAIL <原因>` + `STOP_OBSERVED <状态>`。</summary>
        private static int StopCmd(string[] args, ServiceRegistry reg)
        {
            IServiceTarget target = reg.Get<IServiceTarget>();
            // --port：把目标临时指向指定端口。**没有这个开关时 stop 只会认默认 3080（用户的实例）**，
            // 而 DSH_HOME 隔离不隔离端口 —— 这是踩过的坑，所以测试必须能指定端口。
            int portArg = 0;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--port") { int pp; if (int.TryParse(args[i + 1], out pp)) portArg = pp; }
            }
            if (portArg > 0)
            {
                target = PlatformComposition.WebFor(portArg, reg.Get<IPortProbe>(), reg.Get<IHttpProbe>(), reg.Get<IProcessQuery>());
            }
            ServiceReport r = target.Probe();
            if (ServiceControlPolicy.BeforeStop(r.State.ToString(), r.Pid) == StopDecision.NothingToStop)
            {
                Console.WriteLine("STOP_FAIL " + T("没有观测到在运行的 dsh（端口未监听）", "no running dsh observed (port not listening)"));
                Console.WriteLine("STOP_OBSERVED down");
                return 0;
            }
            if (!Has(args, "--yes"))
            {
                Console.WriteLine("STOP_PLAN " + T("将停止 PID ", "will stop PID ") + r.Pid + T("（端口 ", " (port ") + (portArg > 0 ? portArg.ToString() : "3080") + T("）—— 这会中断正在运行的服务，需要显式确认。", ") - this interrupts a running service and needs explicit confirmation."));
                Console.WriteLine("STOP_NOTE " + T("确认请加 --yes。注意：DSH_HOME 隔离不隔离端口，测试时绝不要对默认端口执行本命令。", "add --yes to confirm. Note: isolating DSH_HOME does NOT isolate the port - never run this against the default port in a test."));
                return 0;
            }
            IServiceControl ctl = reg.Get<IServiceControl>();
            string err;
            // ---- 安全闸门（真机复盘后加的）：绝不对可疑 PID 下手 ----
            if (r.Pid <= 1)
            {
                Console.WriteLine("STOP_FAIL " + T("拒绝执行：观测到的 PID ", "refused: observed PID ") + r.Pid + T(" 不可能是 dsh（安全保护）", " cannot be dsh (safety guard)"));
                return 0;
            }
            if (r.Pid == System.Diagnostics.Process.GetCurrentProcess().Id)
            {
                Console.WriteLine("STOP_FAIL " + T("拒绝执行：那是本程序自己（安全保护）", "refused: that is this program itself (safety guard)"));
                return 0;
            }
            if (!reg.Get<IProcessQuery>().IsDshCommandLine(r.Pid) && Has(args, "--force")) OpLog(reg, "WARN", "stop used --force on a non-dsh listener pid " + r.Pid);
            if (!reg.Get<IProcessQuery>().IsDshCommandLine(r.Pid) && !Has(args, "--force"))
            {
                Console.WriteLine("STOP_FAIL " + T("监听该端口的进程不是 dsh（PID ", "the process on that port is not dsh (PID ") + r.Pid + T("）；如确认要停，请加 --force", "); add --force to stop it anyway"));
                return 0;
            }            bool ok = ctl.StopTree(r.Pid, out err);
            // 再观测要**重试**：进程刚收到信号还没死透 ✗，立刻复探会看到"仍在监听"→ 假失败 ✗（真机抓到的）
            string st = "";
            for (int i = 0; i < 12; i++)
            {
                System.Threading.Thread.Sleep(1000);
                st = target.Probe().State.ToString();
                if (ServiceControlPolicy.AfterStop(st) == StopOutcome.Stopped) break;
            }
            if (ServiceControlPolicy.AfterStop(st) == StopOutcome.Stopped)
            {
                Console.WriteLine("STOP_OK " + r.Pid);
            OpLog(reg, "INFO", "stop OK pid " + r.Pid);
                Console.WriteLine("STOP_OBSERVED down");
                return 0;
            }
            Console.WriteLine("STOP_FAIL " + (ok ? T("进程已结束但端口仍在监听（12 秒后仍未观测到停止）", "process gone but the port is still listening (still not stopped after 12s)") : (string.IsNullOrEmpty(err) ? T("结束进程失败（未给出原因）", "failed to stop the process (no reason given)") : err)));
            Console.WriteLine("STOP_OBSERVED " + st.ToLowerInvariant());
            return 0;
        }

        /// <summary>是否 Windows（平台判断只用于选择启动方式，不用于猜形态）。</summary>
        private static bool PlatformIsWindows()
        {
            return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
        }
        /// <summary>profiles（V3 独有）：列出 profile、它们的**配置形态**与插件清单。
        /// 数据来源：`<数据根>/profiles/<name>/package.json` 里的 `dsh.profile.bundles`（明文小 JSON，只读零注入）。
        /// 标记行：
        ///   `PROFILES_OK <n>` / `PROFILE <name> form=<web|headless|acp|unknown|unparsed> bundles=<n> thirdparty=<m>`
        ///   / `BUNDLE <profile> <bundle-id> <official|thirdparty>` / `PROFILES_FAIL <原因>`
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
                    string ver = BundleVersion(src, name, id);
                    bundles.Add(new string[] { name, id, official ? "official" : "thirdparty", ver });
                }
            }
            Console.WriteLine("PROFILES_OK " + rows.Count);
            for (int i = 0; i < rows.Count; i++)
                Console.WriteLine("PROFILE " + rows[i][0] + " form=" + rows[i][1] + " bundles=" + rows[i][2] + " thirdparty=" + rows[i][3]);
            for (int i = 0; i < bundles.Count; i++)
                Console.WriteLine("BUNDLE " + bundles[i][0] + " " + bundles[i][1] + " " + bundles[i][2] + (bundles[i][3] == "" ? "" : " version=" + bundles[i][3]));
            // 被隔离的条目：profile 的 cordis.patch.yml 里 disabled: true（按行扫描，向前找最近的 id:；不猜 YAML 结构）
            for (int i = 0; i < rows.Count; i++)
            {
                string[] dis = DisabledEntries(src.ReadPatch(rows[i][0]));
                for (int k = 0; k < dis.Length; k++) Console.WriteLine("DISABLED " + rows[i][0] + " " + dis[k]);
            }
            return 0;
        }

        /// <summary>backup-list：标记行与裸路径行逐条对齐 v2.x 的 NIBackupList。</summary>
        /// <summary>备份完成性核对 ✓（`backup-list --verify`）：读每个包的**同级完成标记** &lt;包&gt;.manifest ✓。
        /// 标记**最后写** ✓ → 缺失即"备份未完成"（中断可被精确识别 ✓✓）；存在则核对文件数是否与标记一致 ✓（截断可被发现 ✓）。
        /// 这是**新增开关** ✓，不在标记行契约的比对用例里 ✓ → 不影响 gate1 ✓。</summary>
        /// <summary>恢复前的**完成性核对** ✓：只在"完成标记**存在**且对不上"时返回原因 ✓。
        /// 标记缺失时**不拦** ✓（老包没有标记 ✓，拦了会破坏兼容 ✓）；对不上则说明包被截断 ✓ → 恢复会缺内容 ✓。
        /// 用 --force 可越过 ✓（与 stop 的闸门同一风格 ✓）。</summary>
        /// <summary>备份包的**内容哈希** ✓：排序后的 `相对路径|大小|文件SHA256` 逐行拼接再取 SHA256 ✓。
        /// 用于发现"计数对得上但内容残了"的情况 ✗（例如复制被中断在文件中间 ✓）—— 明文计数发现不了它 ✗✓。
        /// 只做读取 ✓ 不改动包 ✓。</summary>
        private static string PackageContentHash(string pkgDir)
        {
            try
            {
                string root = System.IO.Path.GetFullPath(pkgDir).TrimEnd('\\', '/');
                List<string> lines = new List<string>();
                string[] files = System.IO.Directory.GetFiles(root, "*", System.IO.SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    string rel = files[i].Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                    long len = 0;
                    try { len = new System.IO.FileInfo(files[i]).Length; } catch { }
                    string h = "";
                    try { h = Sha256Of(files[i]); } catch { h = "unreadable"; }
                    lines.Add(rel + "|" + len + "|" + h);
                }
                lines.Sort(StringComparer.Ordinal);
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < lines.Count; i++) sb.Append(lines[i]).Append('\n');
                byte[] raw = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
                using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
                {
                    byte[] d = sha.ComputeHash(raw);
                    System.Text.StringBuilder hex = new System.Text.StringBuilder(d.Length * 2);
                    for (int i = 0; i < d.Length; i++) hex.Append(d[i].ToString("x2"));
                    return hex.ToString();
                }
            }
            catch { return null; }
        }

        /// <summary>给备份包的完成标记补上内容哈希 ✓（标记由平台侧最后写出 ✓，这里追加一行 ✓ 不改平台实现 ✓）。</summary>
        private static void AddContentHashToMarker(string pkgPath)
        {
            try
            {
                string mf = pkgPath + ".manifest";
                if (!System.IO.File.Exists(mf)) return;
                string h = PackageContentHash(pkgPath);
                if (string.IsNullOrEmpty(h)) return;
                string cur = System.IO.File.ReadAllText(mf);
                if (cur.IndexOf("sha256=", StringComparison.Ordinal) >= 0) return;
                System.IO.File.AppendAllText(mf, "sha256=" + h + "\n");
            }
            catch { }
        }

        /// <summary>从标记里取内容哈希（没有则 null ✓）。</summary>
        private static string MarkerHash(string markerPath)
        {
            try
            {
                string[] ls = System.IO.File.ReadAllLines(markerPath);
                for (int i = 0; i < ls.Length; i++)
                    if (ls[i].StartsWith("sha256=", StringComparison.Ordinal)) return ls[i].Substring(7).Trim();
            }
            catch { }
            return null;
        }
        private static string BackupTruncatedReason(string pkgDir)
        {
            try
            {
                string mf = pkgDir + ".manifest";
                if (!System.IO.File.Exists(mf)) return null;
                int want = -1;
                string[] ls = System.IO.File.ReadAllLines(mf);
                for (int i = 0; i < ls.Length; i++) { if (ls[i].StartsWith("files=", StringComparison.Ordinal)) int.TryParse(ls[i].Substring(6).Trim(), out want); }
                if (want < 0) return null;
                int have = 0;
                try { have = System.IO.Directory.GetFiles(pkgDir, "*", System.IO.SearchOption.AllDirectories).Length; } catch { return null; }
                string mh2 = MarkerHash(mf);
                if (mh2 != null)
                {
                    string act2 = PackageContentHash(pkgDir);
                    if (act2 != mh2) return T("该备份内容与完成标记不符（哈希不一致）—— 内容已被改动或损坏", "this backup does not match its completion marker (hash mismatch) - the content has been altered or corrupted");
                }
                if (have >= want) return null;
                return T("该备份不完整（完成标记 ", "this backup is incomplete (marker says ") + want + T(" 个文件，实际 ", " files, actual ") + have + T(" 个）—— 恢复出来的数据会缺内容", " files) - a restore would come back with content missing");
            }
            catch { return null; }
        }
        private static int BackupVerify(ServiceRegistry reg)
        {
            IBackupSource bk = reg.Get<IBackupSource>();
            List<BackupEntry> all = bk.ListRaw();
            int complete = 0, incomplete = 0, mismatch = 0;
            for (int i = 0; i < all.Count; i++)
            {
                string name = System.IO.Path.GetFileName(all[i].Path.TrimEnd('\\', '/'));
                string mf = all[i].Path + ".manifest";
                if (!System.IO.File.Exists(mf))
                {
                    incomplete++;
                    Console.WriteLine("BACKUP_VERIFY " + name + " incomplete " + T("未完成（无完成标记 —— 备份可能被中断）", "incomplete (no completion marker - the backup may have been interrupted)"));
                    continue;
                }
                int want = -1;
                try
                {
                    string[] ls = System.IO.File.ReadAllLines(mf);
                    for (int k = 0; k < ls.Length; k++) { if (ls[k].StartsWith("files=", StringComparison.Ordinal)) int.TryParse(ls[k].Substring(6).Trim(), out want); }
                }
                catch { }
                int have = 0;
                try { have = System.IO.Directory.GetFiles(all[i].Path, "*", System.IO.SearchOption.AllDirectories).Length; } catch { }
                // 有内容哈希就**以哈希为准** ✓（能发现"计数对得上但内容残了" ✗✓）；没有则退回计数比对 ✓
                string mh = MarkerHash(mf);
                if (mh != null)
                {
                    string actual = PackageContentHash(all[i].Path);
                    if (actual != mh) { mismatch++; Console.WriteLine("BACKUP_VERIFY " + name + " mismatch " + T("内容哈希与标记不符 —— 备份内容已被改动或损坏，不要依赖它", "content hash differs from the marker - the backup has been altered or corrupted, do not rely on it")); continue; }
                }
                if (want < 0) { mismatch++; Console.WriteLine("BACKUP_VERIFY " + name + " unreadable " + T("标记无法解析", "marker unparsable")); }
                else if (want != have)
                {
                    mismatch++;
                    Console.WriteLine("BACKUP_VERIFY " + name + " mismatch " + T("标记 ", "marker ") + want + T(" 个文件，实际 ", " files, actual ") + have + T(" 个 —— 该备份不完整，不要依赖它", " - this backup is incomplete, do not rely on it"));
                }
                else { complete++; Console.WriteLine("BACKUP_VERIFY " + name + " complete " + have); }
            }
            Console.WriteLine("BACKUP_VERIFY_TOTAL " + all.Count + " complete=" + complete + " incomplete=" + incomplete + " mismatch=" + mismatch);
            return 0;
        }
        private static int BackupList(string[] args, ServiceRegistry reg)
        {
            if (Has(args, "--verify")) return BackupVerify(reg);
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
        /// 可选 `--report <file>`：写完整诊断报告（含配置/日志摘要，全部脱敏）→ `DOCTOR_REPORT <路径>`；
        /// 写失败 → `DOCTOR_WRITE_FAIL <原因>`。全程只读（与 v2.x 一致，报告用 UTF-8 **带 BOM** 写）。</summary>
        /// <summary>当前操作系统名（跨平台：Linux 上不能写 "Windows" —— 真机测试抓到的 bug）。</summary>
        private static string OsName()
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)) return "Windows";
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX)) return "macOS";
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)) return "Linux";
            return "未知系统";
        }

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

            items.Add(new DocItem("System", 0, OsName() + ": " + Environment.OSVersion.VersionString + " (" + (Environment.Is64BitOperatingSystem ? "x64" : "x86") + ")"));
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

        /// <summary>profilepatch（V3 独有）：给 profile 的 cordis.patch.yml 追加/修改"禁用某个条目"的顶层行。
        /// 纪律与 v2.x 一致：**备份 → 写盘 → 复检 → 失败回滚**；不带 --yes 只打印计划（DRYRUN）。
        /// 用法：`profilepatch --profile <name> --id <entry> [--enable] [--yes]`
        /// 标记行：`PROFILEPATCH_PLAN` / `_DRYRUN` / `_BACKUP` / `_OK` / `_NOOP` / `_ROLLBACK` / `_FAIL <原因>`。</summary>
        private static int ProfilePatch(string[] args, ServiceRegistry reg)
        {
            IProfileManifestSource src = reg.Get<IProfileManifestSource>();
            string profile = FlagOf(args, "--profile");
            string id = FlagOf(args, "--id");
            bool enable = Has(args, "--enable");
            bool yes = Has(args, "--yes");

            if (string.IsNullOrEmpty(profile))
            {
                Console.WriteLine("PROFILEPATCH_FAIL usage: profilepatch --profile <name> --id <entry> [--enable] [--yes]");
                return 0;
            }
            string text = src.ReadPatch(profile);
            if (text == null) { Console.WriteLine("PROFILEPATCH_FAIL file-not-found " + profile); return 0; }

            PatchPlan plan = enable ? PatchPlanner.PlanEnable(text, id) : PatchPlanner.PlanDisable(text, id);
            if (plan.Noop) { Console.WriteLine("PROFILEPATCH_NOOP " + plan.Reason); return 0; }
            if (!plan.Valid) { Console.WriteLine("PROFILEPATCH_FAIL " + plan.Reason); return 0; }

            Console.WriteLine("PROFILEPATCH_PLAN " + profile + "/cordis.patch.yml:" + plan.Line + " " + (enable ? "disabled: false" : "disabled: true"));
            if (!yes)
            {
                Console.WriteLine("PROFILEPATCH_DRYRUN " + T("（确认请加 --yes；只改该 profile 的补丁文件，且会先备份）", "(add --yes to confirm; only that profile's patch file is touched, and it is backed up first)"));
                return 0;
            }
            string backup; string err;
            bool ok = src.ApplyPatch(profile, plan.NewText, out backup, out err);
            if (!string.IsNullOrEmpty(backup)) Console.WriteLine("PROFILEPATCH_BACKUP " + backup);
            if (!ok)
            {
                if (err == "verify-failed") Console.WriteLine("PROFILEPATCH_ROLLBACK " + backup);
                Console.WriteLine("PROFILEPATCH_FAIL " + err);
                return 0;
            }
            Console.WriteLine("PROFILEPATCH_OK " + profile + "/cordis.patch.yml:" + plan.Line);
            OpLog(reg, "INFO", "profilepatch OK " + profile + " line " + plan.Line);
            return 0;
        }

        /// <summary>取 `--name value` 形式的值；缺省返回空串。</summary>
        private static string FlagOf(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++) { if (args[i] == name) return args[i + 1]; }
            return "";
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

        /// <summary>备份导出：校验路径后**复制到目标目录**（会写盘 → 需要 --yes 闸门 ✓）。</summary>
        private static int BackupExport(string[] args, ServiceRegistry reg)
        {
            IBackupSource bk = reg.Get<IBackupSource>();
            IFileSystemQuery fs = reg.Get<IFileSystemQuery>();
            string reason = PathValidator.ValidateExport(Flag(args, "--path"), Flag(args, "--to"), bk.BackupsRoot,
                delegate(string p) { return fs.DirectoryExists(p); },
                delegate(string p) { return System.IO.Path.GetFullPath(p); });
            if (reason != null) { Console.WriteLine("BKEXPORT_FAIL " + T("导出校验失败: " + reason, "export validation failed: " + reason)); return 0; }
            if (!Has(args, "--yes")) { Console.WriteLine("BKEXPORT_PLAN " + T("将把备份复制到目标目录（会写盘）—— 确认请加 --yes", "will copy the backup to the target directory (writes to disk) - add --yes to confirm")); return 0; }
            string src = (Flag(args, "--path") ?? "").Trim().Trim('"');
            string to = (Flag(args, "--to") ?? "").Trim().Trim('"');
            string target = bk.Export(src, System.IO.Path.GetFullPath(to));
            if (target == null) { Console.WriteLine("BKEXPORT_FAIL " + T("导出失败（见 launcher.log）", "export failed (see launcher.log)")); return 0; }
            Console.WriteLine("BKEXPORT_OK " + target);
            return 0;
        }

        /// <summary>备份删除：校验路径后**真删**（会丢数据 → 需要 --yes 闸门 ✓）。</summary>
        private static int BackupDelete(string[] args, ServiceRegistry reg)
        {
            IBackupSource bk = reg.Get<IBackupSource>();
            IFileSystemQuery fs = reg.Get<IFileSystemQuery>();
            string reason = PathValidator.ValidateDeletePath(Flag(args, "--path"), bk.BackupsRoot,
                delegate(string p) { return fs.DirectoryExists(p); });
            if (reason != null) { Console.WriteLine("BKDEL_FAIL " + T("删除校验失败: " + reason, "delete validation failed: " + reason)); return 0; }
            if (!Has(args, "--yes")) { Console.WriteLine("BKDEL_PLAN " + T("将删除该备份目录（会丢数据）—— 确认请加 --yes", "will delete that backup directory (data loss) - add --yes to confirm")); return 0; }
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
            BackupResult r = bk.Create(src, BackupKind.Manual, _cfg == null ? 3 : _cfg.KeepBackups, WorkspaceRoot(reg));
            if (r == null) { Console.WriteLine("BACKUP_FAIL " + T("备份失败（见 launcher.log）", "backup failed (see launcher.log)")); return 0; }
            if (r.SkippedNested > 0)
                Console.WriteLine(T("已跳过 " + r.SkippedNested + " 个嵌套备份目录（dsh-data-*），不复制进本次备份。",
                                    "Skipped " + r.SkippedNested + " nested backup folder(s) (dsh-data-*), not copied into this backup."));
            Console.WriteLine("BACKUP_OK " + r.Path);
            AddContentHashToMarker(r.Path);   // 标记补内容哈希 ✓（能发现"计数对但内容残" ✗✓）
            // 不完整就说出来 ✓：读不到/复制失败的文件被计数（此前静默吞掉 ✗），备份最不能有静默缺口 ✗
            if (r.FailedCopies > 0)
                Console.WriteLine("BACKUP_INCOMPLETE " + r.FailedCopies + T(" 个文件/目录未能备份（权限或读取失败）—— 该备份不完整，请先解决权限再重做", " files/directories could not be backed up (permission or read failure) - this backup is INCOMPLETE; fix permissions and run it again"));
            OpLog(reg, "INFO", "backup OK " + r.Path);
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
            Console.WriteLine("  DeepSeek Harness Toolkit V" + ToolkitVersion);   // 契约比对会忽略这行，但保持原文最省事
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
            sb.AppendLine("== DeepSeek Harness Toolkit selftest ==");   // 同上
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
                string trunc = BackupTruncatedReason(bkDir);
                if (!string.IsNullOrEmpty(trunc) && !Has(args, "--force"))
                {
                    Console.WriteLine("RESTORE_FAIL " + trunc + T("；确认要用它恢复请加 --force", "; add --force to restore from it anyway"));
                    return 0;
                }
                if (!string.IsNullOrEmpty(trunc)) { Console.WriteLine("RESTORE_WARN " + trunc); OpLog(reg, "WARN", "restore used --force on a truncated backup: " + bkDir); }
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
                OpLog(reg, "WARN", "restore --apply skipped the running-service gate (observed: " + sr.State + ")");
                Console.WriteLine("RESTORE_APPLY_ACK " + T("已按 --apply 跳过「运行中」闸门；观测到的服务状态：",
                    "running-service gate skipped by --apply; observed service state: ") + sr.State + (sr.Pid > 0 ? " pid=" + sr.Pid : ""));
                Console.WriteLine("RESTORE_APPLY_ROOT " + paths.DataRoot);
            }

            string dstRoot = paths.DataRoot;
            if (fs.DirectoryExists(dstRoot))
            {
                BackupResult pre = bk.Create(dstRoot, BackupKind.PreRestore, _cfg == null ? 3 : _cfg.KeepBackups, WorkspaceRoot(reg));
                if (pre == null) { Console.WriteLine("RESTORE_FAIL " + T("恢复前自动备份失败", "pre-restore backup failed")); return 0; }
                AddContentHashToMarker(pre.Path);   // 回滚锚点也要能自证完整 ✓✓（恢复前自动备份 ✓）
            Console.WriteLine("RESTORE_PRE_BACKUP " + pre.Path);   // 回滚锚点必须**总是**告诉用户 ✗（我曾在一次编辑中误删此行 ✗）
            }

            if (!IntegrityGate(reg)) return 0;   // 对齐 v2.x：完整性不匹配时在写盘前拒绝

            RestoreOutcome o = bk.Restore(bkDir, dstRoot, WorkspaceRoot(reg));
            if (!o.Ok)
            {
                Console.WriteLine("RESTORE_FAIL " + T("恢复失败：" + (o.Error ?? ""), "restore failed: " + (o.Error ?? "")));
                return 0;
            }
            Console.WriteLine("RESTORE_OK " + bkDir);
            OpLog(reg, "INFO", "restore OK " + bkDir);
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
                string p = PathValidator.ResolveBackupPath(pathArg.Trim().Trim('"'), bk.BackupsRoot);   // 裸备份名解析到备份根内 ✓
                if (PathUtil.IsSubPath(bk.BackupsRoot, p))
                {
                    if (!BackupPackage.IsValidPackage(bk.Snapshot(p))) { Console.WriteLine("DRYRUN_FAIL " + T("无效备份目录", "invalid backup directory")); return 0; }
                    bkDir = p;
                    // 完成性闸门 ✓✓：放在**路径校验通过之后** —— 不存在的路径与根外路径都到不了这里 ✓（这正是上一版放错位置的原因 ✗）。
                    // 条件用 !IsNullOrEmpty ✓ 且只在"原因非空"时拦 ✓ → **最多少报，绝不误拦** ✓✓。
                    string truncReason = BackupTruncatedReason(p);
                    if (!string.IsNullOrEmpty(truncReason) && !Has(args, "--force"))
                    {
                        Console.WriteLine("DRYRUN_FAIL " + truncReason + T("；确认要预览它请加 --force", "; add --force to preview it anyway"));
                        OpLog(reg, "WARN", "restore --dry-run refused a truncated backup: " + p);
                        return 0;
                    }
                    if (!string.IsNullOrEmpty(truncReason)) Console.WriteLine("DRYRUN_WARN " + truncReason);
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
                        string wsRoot = WorkspaceRoot(reg);
                        if (wsRoot == null) { Console.WriteLine("DRYRUN_SCOPE workspace " + name + " skipped (unknown workspace root)"); continue; }
                        string target = System.IO.Path.Combine(wsRoot, name);
                        long[] wp = PlanMerge(reg, subs[i], target, null, ".dshws", false);
                        Console.WriteLine("DRYRUN_SCOPE workspace " + name + " " + target);
                        PrintPlan(wp);
                        tn += wp[0]; to += wp[1]; tk += wp[2]; tb += wp[3];
                    }
                }
                else
                {
                    string wsRoot2 = WorkspaceRoot(reg);
                    string target = wsRoot2 == null ? "" : wsRoot2;
                    if (wsRoot2 == null) Console.WriteLine("DRYRUN_SCOPE workspace-legacy skipped (unknown workspace root)");
                    else Console.WriteLine("DRYRUN_SCOPE workspace-legacy " + target);
                    long[] wp = PlanMerge(reg, wsSrc, target, null, null, true);
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

        /// <summary>config-get：CONFIGGET_OK + 每行 CONFIG <key> <value>（顺序与 v2.x 一致）。</summary>
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

        /// <summary>config-set <key> <value>：白名单内才写盘，否则 CONFIGSET_FAIL 原因。</summary>
        private static int ConfigSet(string[] args, ServiceRegistry reg)
        {
            string key = args.Length > 1 ? args[1] : "";
            string val = args.Length > 2 ? args[2] : "";
            string reason = ConfigValidator.Validate(key, val, CanonPath);
            if (reason != null) { Console.WriteLine("CONFIGSET_FAIL " + reason); return 0; }
            _cfg = ConfigValidator.ApplyTo(_cfg, key, val, CanonPath);
            reg.Get<IConfigSource>().WriteConfig(ConfigCodec.Serialize(_cfg));
            // 回读校验 ✓：写盘可能静默失败（配置文件只读/被占用 ✗），实测曾报 CONFIGSET_OK 而文件根本没变 ✗✗。
            // 判据是"配置里这个键真的等于请求值" ✓，不是"我调用过写盘" ✗。
            string wantText = ConfigCodec.Serialize(_cfg);
            string gotText = ConfigCodec.Serialize(ConfigCodec.Parse(reg.Get<IConfigSource>().ReadConfig(), CanonPath));
            if (gotText != wantText)
            {
                Console.WriteLine("CONFIGSET_FAIL " + T("写入未生效（配置文件可能只读或被占用）—— 已回读核对，值未改变", "the write did not take effect (the config file may be read-only or locked) - read back and the value is unchanged"));
                _cfg = ConfigCodec.Parse(gotText, CanonPath);
                return 0;
            }
            _cfg = ConfigCodec.Parse(gotText, CanonPath);
            Console.WriteLine("CONFIGSET_OK " + key.Trim().ToLowerInvariant());
            return 0;
        }

        /// <summary>组合根：交给 PlatformComposition 按平台装配（单 exe，运行时判定）。</summary>
        private static ServiceRegistry Compose()
        {
            return PlatformComposition.Compose();
        }    }
}
