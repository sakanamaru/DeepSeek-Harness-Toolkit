using System;
using System.Collections.Generic;
using Dsht.Domain.Abstractions;
using Dsht.Domain.Model;
using Dsht.Domain.Services;
using Dsht.Domain.Targets;
using Dsht.Platform.Windows;

namespace Dsht.Cli
{
    /// <summary>V3 CLI 组合根 + 命令面。每个命令的标记行都要与 v2.x 逐字一致（见 v3/tests/compare_markers.ps1）。</summary>
    public static class Program
    {
        private const int WebPort = 3080;
        private const string WebUrl = "http://127.0.0.1:3080/";

        public static int Main(string[] args)
        {
            ServiceRegistry reg = Compose();
            _cfg = LoadConfig(reg);
            string cmd = args.Length > 0 ? args[0] : "";

            if (cmd == "status") return Status(reg, Has(args, "--detail"));
            if (cmd == "describe") return Describe(reg);
            if (cmd == "profilecheck") return ProfileCheck(args, reg);
            if (cmd == "backup-list") return BackupList(args, reg);
            if (cmd == "doctor") return Doctor(reg);
            if (cmd == "version") { Console.WriteLine("DSHT_VERSION " + ToolkitVersion); return 0; }
            if (cmd == "config-get") return ConfigGet();
            if (cmd == "config-set") return ConfigSet(args, reg);
            if (cmd == "bootdiag") return BootDiag(args, reg);

            Console.WriteLine("usage: dsht status [--detail] | describe | profilecheck [...] | backup-list [--detail] | doctor | version | config-get | config-set <key> <value> | bootdiag --from <file>");
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
        /// 注：--report 尚未移植（v2.x 的报告含配置/日志摘要，属后续工作）。</summary>
        private static int Doctor(ServiceRegistry reg)
        {
            List<DocItem> items = new List<DocItem>();
            DoctorCollect(reg, items);
            Console.WriteLine(DoctorSummary.Summary(items));
            foreach (DocItem it in items)
                Console.WriteLine("[" + DoctorSummary.Level(it.Level) + "] " + it.Cat + " " + it.Text);
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