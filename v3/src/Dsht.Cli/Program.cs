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
            string cmd = args.Length > 0 ? args[0] : "";

            if (cmd == "status") return Status(reg, Has(args, "--detail"));
            if (cmd == "describe") return Describe(reg);
            if (cmd == "profilecheck") return ProfileCheck(args);
            if (cmd == "backup-list") return BackupList(args);

            Console.WriteLine("usage: dsht status [--detail] | describe | profilecheck [...] | backup-list [--detail]");
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
        private static int ProfileCheck(string[] args)
        {
            string dir = Flag(args, "--dir");
            string one = Flag(args, "--file");
            bool vendor = Has(args, "--vendor");
            bool abs = Has(args, "--abs");
            WindowsProfileSource src = new WindowsProfileSource();
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
        private static int BackupList(string[] args)
        {
            bool detail = Has(args, "--detail");
            IBackupSource src = new WindowsBackupSource();
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

        /// <summary>组合根：装配平台实现 → 领域服务。形态识别只用可观测事实。</summary>
        private static ServiceRegistry Compose()
        {
            WindowsHttpProbe http = new WindowsHttpProbe();
            WindowsPortProbe port = new WindowsPortProbe();
            WindowsProcessQuery proc = new WindowsProcessQuery(http, WebUrl, 800);

            ServiceRegistry reg = new ServiceRegistry();
            reg.Add<IPortProbe>(port);
            reg.Add<IHttpProbe>(http);
            reg.Add<IProcessQuery>(proc);
            reg.Add<IProfileSource>(new WindowsProfileSource());
            reg.Add<IServiceTarget>(new WebTarget(port, http, proc, new WebTargetOptions(WebPort, WebUrl, 800, 800)));
            return reg;
        }
    }
}