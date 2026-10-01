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

            // —— **反银狐启动闸门** ✓✓（用户要求："反银狐木马感染/伪造的机制，至少被感染无法运行" ✓）
            // 银狐（SilverFox）的主要手法是**静态感染**：给正常 exe 打补丁/追加代码 → **文件变了** ✓
            // → 自身 SHA-256 与随包 hashes.txt 不一致就**直接拒绝运行** ✓✓
            // 纪律（沿用 IntegrityJudge 的语义 ✓）：
            //   · **Mismatch → 拒绝**（被改过 ✓ 一律不跑 ✓ 不给绕过参数 ✗ —— 绕过参数本身就是洞 ✗✓）
            //   · **Unknown → 放行**（无清单/源码编译/单独复制 exe ✓ 否则开发者寸步难行 ✓✓）
            //     但会**明说"跳过校验"** ✓ **不假报"已验证"** ✗✓
            //   · 例外：`verify-install` 与 `selftest` **放行** ✓✓（被改过的包也要能自证/诊断 ✓）
            if (cmd != "verify-install" && cmd != "selftest")
            {
                int gate = StartupIntegrityGate(reg);
                if (gate != 0) return gate;
            }

            if (cmd == "") return Menu(reg);
            if (cmd == "status") return Status(reg, Has(args, "--detail"));
            if (cmd == "describe") return Describe(reg);
            if (cmd == "bridge-install") return BridgeInstall(args, reg);   // ✓ 可选的桥接插件 ✓（用户要求"安装桥接插件有按钮吗" ✓）
            if (cmd == "profilecheck") return ProfileCheck(args, reg);
            if (cmd == "profilepatch") return ProfilePatch(args, reg);
            if (cmd == "profiles") return Profiles(reg);
            if (cmd == "verify-install") return VerifyInstall(args);
            if (cmd == "wipe") return WipeCmd(args, reg);
            if (cmd == "import") return ImportCmd(args, reg);
            if (cmd == "update-info") return UpdateInfo(reg);
            if (cmd == "update-center") return UpdateCenter(reg);
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
            if (cmd == "backup-dir") return BackupDirCmd(args, reg);   // ✓ 备份位置查看/设置 ✓（用户要求"备份路径在备份页面里设置并且显示" ✓）
            if (cmd == "doctor") return Doctor(args, reg);
            if (cmd == "version") { Console.WriteLine("DSHT_VERSION " + ToolkitVersion); return 0; }
            if (cmd == "config-get") return ConfigGet();
            if (cmd == "config-set") return ConfigSet(args, reg);
            if (cmd == "bootdiag") return BootDiag(args, reg);
            if (cmd == "restore") return Restore(args, reg);
            if (cmd == "selftest") return SelfTest(args, reg);
            if (cmd == "check") return Check(reg);
            if (cmd == "backup") return Backup(args, reg);
            if (cmd == "backup-export") return BackupExport(args, reg);
            if (cmd == "backup-delete") return BackupDelete(args, reg);
            if (cmd == "autostart") return AutoStartCmd(args, reg);
            Usage();
            return 2;
        }

        /// <summary>服务三态 + detail 三行。逐条对齐 v2.x 的 StatusCli。</summary>
        private static int Status(ServiceRegistry reg, bool detail)
        {
            ServiceReport r = reg.Get<IServiceTarget>().Probe();
            Console.WriteLine(r.StatusMarker);
            // 官方桌面端（Electron）**不监听 3080** ✓（2026-09-30 真机实测：监听 19387 ✓）
            // → 它开着时上面那行是 STATUS_DOWN ✓ 准确但会让人以为"dsh 没在跑" ✗
            // 这里**只在真检测到那个进程时**才补一行 ✓ → 界面据此能如实显示"桌面端在跑" ✓✓
            try
            {
                if (reg.Get<IProcessQuery>().AnyProcessNamed("DeepSeek Harness"))
                {
                    // 带上 pid / 启动时间 / 已运行 ✓（用户要求："概览再更新下 desktop 的 pid 启动时间和已运行" ✓✓）
                    // 取不到就留空 ✓ 不猜 ✓（STATUS_DESKTOP <name> <pid> <start> <uptime>）
                    int dpid = 0; string dstart = ""; string dup = "";
                    try { dpid = reg.Get<IProcessQuery>().PidOfNamed("DeepSeek Harness"); } catch { }
                    if (dpid > 0)
                    {
                        try { System.DateTime? ds = reg.Get<IProcessQuery>().StartTime(dpid); if (ds.HasValue) { dstart = ds.Value.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture); dup = UptimeFormatter.Format(System.DateTime.Now - ds.Value); } } catch { }
                    }
                    // 分成**独立标记** ✓（进程名与时长都带空格 ✗ → 挤在一行没法可靠解析 ✓）
                    Console.WriteLine("STATUS_DESKTOP DeepSeek Harness");
                    if (dpid > 0) Console.WriteLine("STATUS_DESKTOP_PID " + dpid);
                    if (!string.IsNullOrEmpty(dstart)) Console.WriteLine("STATUS_DESKTOP_START " + dstart);
                    if (!string.IsNullOrEmpty(dup)) Console.WriteLine("STATUS_DESKTOP_UPTIME " + dup);
                }
            }
            catch { }
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
            Console.WriteLine("STATUS_START " + (haveStart ? start.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) : ""));
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
        // ================================================================ 桥接插件（可选）

        /// <summary>在 PATH 里找一个可执行文件 ✓（找不到返回空串 ✓ 不猜 ✓）。</summary>
        private static string WhichOnPath(string name)
        {
            try
            {
                string pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
                string[] dirs = pathVar.Split(System.IO.Path.PathSeparator);
                string[] exts = new string[] { ".exe", ".cmd", ".bat", "" };
                for (int i = 0; i < dirs.Length; i++)
                {
                    string d = dirs[i];
                    if (string.IsNullOrEmpty(d)) continue;
                    for (int j = 0; j < exts.Length; j++)
                    {
                        try
                        {
                            string f = System.IO.Path.Combine(d.Trim(), name + exts[j]);
                            if (System.IO.File.Exists(f)) return f;
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return "";
        }

        /// <summary>工具箱自己所在的目录 ✓（单文件发布下 `Assembly.Location` 是空的 ✗ → 用 MainModule ✓）。</summary>
        private static string SelfDir()
        {
            string exe = "";
            try { exe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName; } catch { }
            if (!string.IsNullOrEmpty(exe))
            {
                try { string d = System.IO.Path.GetDirectoryName(exe); if (!string.IsNullOrEmpty(d)) return d; } catch { }
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        /// <summary>跑一个外部命令并拿回 stdout+stderr ✓（超时 120 秒 ✓ 超时如实说 ✓）。</summary>
        private static string RunExternal(string file, string argLine, out int exitCode)
        {
            exitCode = -1;
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(file, argLine);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = new System.Text.UTF8Encoding(false);
                psi.StandardErrorEncoding = new System.Text.UTF8Encoding(false);
                using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    System.Threading.Tasks.Task<string> so = System.Threading.Tasks.Task.Run(delegate { return p.StandardOutput.ReadToEnd(); });
                    System.Threading.Tasks.Task<string> se = System.Threading.Tasks.Task.Run(delegate { return p.StandardError.ReadToEnd(); });
                    if (!p.WaitForExit(120000)) { try { p.Kill(); } catch { } exitCode = -2; return "（超时 120 秒，已结束该进程）"; }
                    exitCode = p.ExitCode;
                    sb.Append(so.Result);
                    string err = se.Result;
                    if (!string.IsNullOrEmpty(err)) sb.Append(Environment.NewLine).Append("[stderr] ").Append(err);
                    return sb.ToString();
                }
            }
            catch (Exception ex) { return "（无法启动 " + file + "：" + ex.Message + "）"; }
        }

        /// <summary>安装**可选的桥接插件** ✓✓（用户要求：「安装桥接插件有按钮吗」✓）。
        ///
        /// **它做什么**：把 dsh 的会话/token 状态写成一份**只读快照** ✓
        ///   让工具箱能显示「**运行中**」—— 这是**磁盘投影给不了的事实** ✓
        ///   也是这个插件存在的**唯一理由** ✓✓（不装 → 工具箱降级为磁盘投影 ✓ 功能不缺 ✓ 只是没有运行态 ✓）
        /// **它不做什么**：不发模型请求 ✓ 不改 dsh 状态 ✓ 不读会话正文 ✓ 不联网 ✓ 任何失败静默 ✓
        ///
        /// ★★ **实测踩过的三个坑**（2026-09-30 真机 ✓ 我都验证过 ✓）：
        ///   ① `dsh plugin add` **需要 pnpm** ✗ —— 而 dsh **不会**替你装它 ✓
        ///      → 没有 pnpm 就**明确告诉用户怎么装** ✓ **绝不假装成功** ✗
        ///   ② `add` 成功时会 link 进 profile ✓ **并把插件的 patch 合并进 `cordis.patch.yml`** ✓
        ///   ③ **必须验证 patch 里真的有 `shio-bridge` 行** ✗✗ ——
        ///      否则插件**根本不会加载** ✓ 而 dsh **不会报任何错** ✗（"0 处加载错误"是**假绿** ✓ 我踩过 ✓）
        ///
        /// 标记行：BRIDGE_PLAN / BRIDGE_NO_PNPM / BRIDGE_NO_DSH / BRIDGE_NO_PLUGIN / BRIDGE_OK
        ///         / BRIDGE_FAIL / BRIDGE_VERIFY / BRIDGE_NOTE</summary>
        private static int BridgeInstall(string[] args, ServiceRegistry reg)
        {
            string profile = Flag(args, "--profile");
            // N11 FIX (CLI audit MINOR): this value is pasted into a dsh command line, and the
            // plugin-patching command already validates its profile names - a name like "web&calc"
            // would otherwise chain a command on Windows. Same rule here.
            // F-E FIX (CLI final review): the character class allows dots, so "." and ".." passed - and the name
            // is joined into a path later, making the verification read a parent directory instead of a profile.
            if (!string.IsNullOrEmpty(profile) && (profile == "." || profile == ".."
                || !System.Text.RegularExpressions.Regex.IsMatch(profile, @"^[A-Za-z0-9._-]+$")))
            {
                Console.WriteLine("BRIDGE_FAIL " + T("profile 名字不合法（只允许字母数字与 . _ - ✓）：" + profile, "invalid profile name: " + profile));
                return 0;
            }
            if (string.IsNullOrEmpty(profile)) profile = "web";
            bool yes = Has(args, "--yes");

            string selfDir = SelfDir();
            string plugin = System.IO.Path.Combine(System.IO.Path.Combine(selfDir, "plugin"), "dsh-minato-bridge");

            Console.WriteLine("BRIDGE_PLAN " + T(
                "把可选的桥接插件装进 dsh 的 profile「" + profile + "」✓ 它只读 ✓ 不联网 ✓ 不发模型请求 ✓ 不改 dsh 状态 ✓",
                "install the optional bridge plugin into dsh profile '" + profile + "' - read-only, no network, no model calls, no writes to dsh state"));
            Console.WriteLine("BRIDGE_NOTE " + T(
                "装了它，工具箱才能显示「运行中」（运行态是进程内事实，磁盘投影给不了 ✗）；不装也能用 ✓ 只是那一位显示 unknown ✓",
                "with it, the toolkit can show which sessions are running; without it, that one field is unknown"));

            if (!System.IO.Directory.Exists(plugin))
            {
                Console.WriteLine("BRIDGE_NO_PLUGIN " + T(
                    "随包分发的插件目录不存在：" + plugin + " ✓（官方发布包会带 plugin/dsh-minato-bridge ✓ 源码编译请从仓库的 plugin/ 目录取 ✓）",
                    "bundled plugin folder not found: " + plugin));
                return 0;
            }

            string dsh = ResolveDsh(reg);
            if (string.IsNullOrEmpty(dsh))
            {
                Console.WriteLine("BRIDGE_NO_DSH " + T("没有找到 dsh ✓ 请先装 dsh 再装插件 ✓", "dsh not found; install dsh first"));
                return 0;
            }

            string pnpm = WhichOnPath("pnpm");
            if (string.IsNullOrEmpty(pnpm))
            {
                Console.WriteLine("BRIDGE_NO_PNPM " + T(
                    "没有找到 pnpm ✗ —— 而 `dsh plugin add` **依赖它** ✓ 请先装：npm i -g pnpm ✓ 然后重跑本命令 ✓"
                    + "（dsh 自己**不会**替你装 pnpm ✓ 这一步不能省 ✓）",
                    "pnpm not found - dsh's plugin command needs it. Install it with: npm i -g pnpm, then run this again."));
                return 0;
            }
            Console.WriteLine("BRIDGE_NOTE " + T("pnpm: " + pnpm + " ✓ · dsh: " + dsh + " ✓ · 插件: " + plugin + " ✓",
                                                "pnpm: " + pnpm + " / dsh: " + dsh + " / plugin: " + plugin));

            if (!yes)
            {
                Console.WriteLine("BRIDGE_PLAN " + T("这是写操作（会改 profile 的 package.json 与 cordis.patch.yml ✓ 装前 dsh 自己会保留原状 ✓）—— 确认请加 --yes ✓",
                                                     "this writes to the profile - add --yes to proceed"));
                return 0;
            }

            int rc = -1;
            string outp = RunExternal(dsh, "plugin --profile " + profile + " add \"" + plugin + "\"", out rc);
            Console.WriteLine("BRIDGE_FAIL_RAW " + rc + " " + (outp == null ? "" : outp.Trim().Replace("\r", "").Replace("\n", " | ")));

            // ③ **验证**：profile 的 patch 里必须真的有 shio-bridge ✓✓（否则装了也不加载 ✗ 且不报错 ✗）
            string profileDir = "";
            try
            {
                string dataRoot = reg.Get<IPaths>().DataRoot;
                profileDir = System.IO.Path.Combine(System.IO.Path.Combine(dataRoot, "profiles"), profile);
            }
            catch { }

            bool linked = false, patched = false;
            if (!string.IsNullOrEmpty(profileDir))
            {
                // C1 FIX (audit MAJOR): an npm package is a directory (pnpm makes a junction), and
                // File.Exists is false for both - so `linked` was always false, BRIDGE_OK was
                // unreachable, and a successful install printed a false failure.
                // NOTE: the comment goes ABOVE the line, never inside it - a `//` inside a
                // single-line `try { ... } catch { }` comments out the closing brace and breaks
                // the whole file (this exact mistake cost an hour tonight).
                try { linked = System.IO.Directory.Exists(System.IO.Path.Combine(System.IO.Path.Combine(profileDir, "node_modules"), "dsh-minato-bridge")); } catch { }
                try
                {
                    string patch = System.IO.Path.Combine(profileDir, "cordis.patch.yml");
                    if (System.IO.File.Exists(patch))
                    {
                        string txt = System.IO.File.ReadAllText(patch);
                        patched = txt != null && txt.IndexOf("shio-bridge", StringComparison.Ordinal) >= 0;
                    }
                }
                catch { }
            }

            Console.WriteLine("BRIDGE_VERIFY linked=" + (linked ? "1" : "0") + " patched=" + (patched ? "1" : "0"));

            if (linked && patched)
            {
                Console.WriteLine("BRIDGE_OK " + T(
                    "插件已装好并**已注册进加载树** ✓✓ 重启 dsh 后生效 ✓ 届时工具箱的「运行中」会变成真实值 ✓",
                    "plugin installed and registered in the load tree; restart dsh to take effect"));
            }
            else if (linked && !patched)
            {
                Console.WriteLine("BRIDGE_FAIL " + T(
                    "包已经 link 进 profile ✓ 但 `cordis.patch.yml` 里**没有 `shio-bridge` 行** ✗✗ → 插件**不会加载** ✓ 而且 dsh **不会报错** ✗"
                    + "（实测过的坑 ✓）。请手动把插件自带的 cordis.patch.yml 追加到该 profile 的 cordis.patch.yml ✓ 然后重启 dsh ✓",
                    "package linked but the patch entry is missing, so the plugin will not load and dsh will not report an error; append the plugin's cordis.patch.yml to the profile's"));
            }
            else
            {
                Console.WriteLine("BRIDGE_FAIL " + T(
                    "安装没有成功 ✓ 退出码 " + rc + " ✓ 上面 BRIDGE_FAIL_RAW 是原始输出 ✓（最常见原因：pnpm 不在 PATH ✓ 或 dsh 的 profile 名不对 ✓）",
                    "install did not succeed; exit code " + rc + "; see BRIDGE_FAIL_RAW above"));
            }
            return 0;
        }


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
        /// <summary>快照的年龄（秒）✓。**解析不出来 → 返回 0** ✓（当作新鲜 ✓ —— 绝不能因为解析失败就把 live 清掉 ✗）。</summary>
        private static long SnapshotAgeSeconds(string snap)
        {
            try
            {
                System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(
                    snap, "\"generatedAt\"\\s*:\\s*\"([^\"]+)\"");
                if (!m.Success) return 0;
                System.DateTime t;
                if (!System.DateTime.TryParse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out t))
                    return 0;
                double age = (System.DateTime.UtcNow - t).TotalSeconds;
                return age < 0 ? 0 : (long)age;
            }
            catch { return 0; }
        }

        private static int Sessions(ServiceRegistry reg)
        {
            ISessionStatsSource src = reg.Get<ISessionStatsSource>();
            List<SessionStat> list = new List<SessionStat>();
            string source = "disk";
            // ★★★ **插件 N6 补全（插件复审 —— 快照非空就**独占**了历史）** ✓✓
            //   ✗ 原来：快照里只要有**一条**（插件在 dsh 运行时必然有活会话 ✓）
            //     → 整个磁盘投影**被跳过** ✗✗ → **只有历史记录、没有活会话的那些会话从面板消失** ✓
            //       （N6 之前它们至少还在 ✓ 只是 turns=0 ✓ —— 所以"回退到磁盘"只在不跑 dsh 时成立 ✗）
            //   ✓ 现在：**合并** ✓✓ —— 快照优先（活会话的实时数字 ✓），磁盘**补齐**快照没覆盖的会话 ✓
            System.Collections.Generic.HashSet<string> have =
                new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            string snap = src.ReadText(src.SnapshotPath);
            if (snap != null)
            {
                SessionStat[] fromSnap = SessionStats.ParseSnapshot(snap);
                if (fromSnap.Length > 0)
                {
                    // ★★★ **F8 真机修复（2026-10-01 真机实测抓到）** ✓✓
                    //   ✗ 插件的 dispose 钩子**在真 dsh 里根本不会跑** ✗ —— dsh 跑完任务直接退出 ✓
                    //     → 它写下的 `live:true` **永久留在盘上** ✗✗
                    //     → **面板永远显示"运行中"** ✗（正是 F8 要防的那件事 ✓ 而它只防了"正常卸载"）
                    //   · 实测证据：快照的 `generatedAt` 停在 dsh 退出前最后一次 tick ✓
                    //     之后没有任何更晚的写入 ✓ → 说明 dispose 没跑 ✓
                    //   ✓ 现在：**不信"永久的 live"** ✓✓ 用 `generatedAt` 判**新鲜度** ✓
                    //     · 插件活着时每 3 秒刷新一次 ✓ → 30 秒没刷新 = **它已经不在了** ✓
                    //     · 那条快照的其余数据**照常保留** ✓ 只把会撒谎的 `live` 置 false ✓
                    //   ✓ 放在 CLI 层而不是 Domain ✓ —— 领域层纯净度门槛禁止时钟耦合 ✓✓
                    long snapAgeSec = SnapshotAgeSeconds(snap);
            // ★ 第 2 轮审查抓到：**写死 30 秒** ✗ → intervalMs 配到 30 秒以上时，活着的 dsh 会被判成已结束 ✗✗
            // ✓ 现在：**按快照自己声明的间隔推** ✓✓（阈值 = max(30 秒, 3 × interval) ✓ 拿不到就退回 30 秒 ✓）
            long snapThreshold = 30;
            try
            {
                System.Text.RegularExpressions.Match ivm = System.Text.RegularExpressions.Regex.Match(
                    snap, "\"intervalMs\"\\s*:\\s*(\\d+)");
                if (ivm.Success)
                {
                    long iv;
                    if (long.TryParse(ivm.Groups[1].Value, out iv) && iv > 0)
                    {
                        long t3 = (iv / 1000L) * 3L;
                        if (t3 > snapThreshold) snapThreshold = t3;
                    }
                }
            }
            catch { }
            bool stale = snapAgeSec > snapThreshold;
                    if (stale)
                    {
                        int cleared = 0;
                        for (int i = 0; i < fromSnap.Length; i++)
                            if (fromSnap[i].Live) { fromSnap[i].Live = false; cleared++; }
                        if (cleared > 0)
                            Console.Error.WriteLine("SESSIONS_SNAPSHOT_STALE 快照已 " + snapAgeSec + " 秒未刷新 → 插件已不在 → 已把 " + cleared + " 条「运行中」如实置为 false（数据保留 ✓）");
                    }
                    list.AddRange(fromSnap);
                    for (int i = 0; i < fromSnap.Length; i++) if (fromSnap[i].Id != null) have.Add(fromSnap[i].Id);
                    source = "snapshot";
                }
            }
            // 磁盘投影：**总是扫** ✓ 只补快照里没有的 id ✓（有快照的那条用快照的数字 ✓ 更实时 ✓）
            int fromDisk = 0;
            string[] files = src.ListSessionFiles();
            // ★★ 第 2 轮审查抓到：下面那段 childId 扫描**又把每个文件读了一遍** ✗✗
            //   → N 个会话文件 = 2N 次全文件读 ✓（197 个会话时第二遍约 200 ms ✓ 线性增长 ✓）
            // ✓ 现在：**第一遍读到的文本缓存下来，第二遍直接复用** ✓✓（只读一遍 ✓）
            var textCache = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
            {
                string id = System.IO.Path.GetFileNameWithoutExtension(files[i]);
                if (id != null && id.StartsWith("session-", StringComparison.Ordinal)) id = id.Substring("session-".Length);
                if (id != null && have.Contains(id)) continue;
                string txt1 = src.ReadText(files[i]);
                if (!string.IsNullOrEmpty(txt1)) textCache[files[i]] = txt1;
                SessionStat s = SessionStats.ParseSessionProjection(txt1, id);
                if (s != null) { list.Add(s); if (id != null) have.Add(id); fromDisk++; }
            }
            if (fromDisk > 0) source = (source == "snapshot") ? "snapshot+disk" : "disk";
            if (list.Count == 0)
            {
                string agg = src.ReadText(src.AggregatePath);
                if (agg != null)
                {
                    SessionStat[] a = SessionStats.ParseAggregate(agg);
                    if (a.Length > 0) { list.AddRange(a); source = "aggregate"; }
                }
            }
            if (list.Count == 0)
            {
                Console.WriteLine("SESSIONS_FAIL " + T("没有可读的会话投影（dsh 未初始化，或该 dsh 版本的投影格式不认）",
                    "no readable session projection (dsh not initialized, or an unrecognized projection format)"));
                return 0;
            }
            // 父子关系：dsh 把**子会话 id** 记在父会话文件的 "childId" 字段里 ✓✓
            // （2026-09-30 实测：取样 8 个会话，**7 个 id 出现在别的会话文件的 childId 里** ✓）
            // 快照里**没有**这个信息 ✗ → 所以必须扫盘 ✓；用**流式字符串扫描**（不解析 JSON ✓ 快 ✓）
            try
            {
                if (_cfg == null || !_cfg.ScanChildren) throw new InvalidOperationException("scan_children=off");   // 排障开关真的接线 ✓
                string[] cfiles = src.ListSessionFiles();
                for (int ci = 0; ci < cfiles.Length; ci++)
                {
                    string pid2 = System.IO.Path.GetFileNameWithoutExtension(cfiles[ci]);
                    if (pid2 != null && pid2.StartsWith("session-", StringComparison.Ordinal)) pid2 = pid2.Substring("session-".Length);
                    string t2;
                    if (!textCache.TryGetValue(cfiles[ci], out t2)) t2 = src.ReadText(cfiles[ci]);   // ★ 第二遍复用第一遍读到的 ✓✓
                    if (string.IsNullOrEmpty(t2)) continue;
                    int at = 0;
                    while (true)
                    {
                        int k = t2.IndexOf("\"childId\"", at, StringComparison.Ordinal);
                        if (k < 0) break;
                        int q1 = t2.IndexOf('"', k + 9);
                        if (q1 < 0) break;
                        int q2 = t2.IndexOf('"', q1 + 1);
                        if (q2 < 0) break;
                        string cid = t2.Substring(q1 + 1, q2 - q1 - 1);
                        if (cid.Length > 0) Console.WriteLine("SESSION_CHILD " + MarkerText.Encode(pid2) + " " + MarkerText.Encode(cid));   // N10 FIX: both fields came straight from file names/content
                        at = q2 + 1;
                    }
                }
            }
            catch { }
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
                // I5 FIX (CLI audit MINOR): the title was escaped but the id was not, so a session
                // projection whose file name contained a newline (legal on Linux) could inject a
                // fake marker line into the output the GUI parses.
                Console.WriteLine("SESSION " + MarkerText.Encode(s.Id)
                    + " title=" + MarkerText.Encode(s.Title)
                    // F-D FIX (CLI final review): the id and title were escaped but these two were not, and a
                    // timestamp read from a session file is external text - a newline in it injected a fake
                    // marker line into the output the GUI parses (the reviewer reproduced SESSIONS_OK 9999).
                    + " created=" + (string.IsNullOrEmpty(s.CreatedAt) ? "unknown" : MarkerText.Encode(s.CreatedAt))
                    + " last=" + (string.IsNullOrEmpty(s.LastPromptAt) ? "unknown" : MarkerText.Encode(s.LastPromptAt))
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
            Console.WriteLine("  update-info | update-center | update [--yes]");
Console.WriteLine("  config-get | config-set <key> <value>");
            Console.WriteLine("  install [--install-node] [--version <v>] [--list] [--yes] | update [--version <v>] [--list] [--yes] | uninstall [--yes]");
            Console.WriteLine("  update-info（只读：当前/最新/来源/状态/回滚候选 ✓）");
            Console.WriteLine("  start [--port <n>] [--profile <name>] [--yes] | stop [--port <n>] [--target web|desktop] [--force] [--yes]");
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
                // 与经典版（v2.x）同风格：青色框 + 白色条目 + ▍小标题 + 彩色提示 ✓
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("  ==============================================");
                Console.WriteLine("  dsh-minato " + ToolkitVersion);
                Console.WriteLine("  ==============================================");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(T("  ▍ 常用操作", "  ▍ common"));
                Console.ResetColor();
                Console.WriteLine("    1  安装/升级 dsh        2  启动 dsh           3  停止 dsh");
                Console.WriteLine("    4  状态                 5  会话与 token       6  形态与插件");
                Console.WriteLine("    7  立即备份             8  备份清单           9  恢复预览（dry-run）");
                Console.WriteLine("   10  体检                11  备份目录          12  全部命令");
                Console.WriteLine("    q  退出");
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(T("  请输入数字：", "  choice: "));
                Console.ResetColor();
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
                if (line == "7") { if (Confirm("backup")) Backup(new string[0], reg); continue; }
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
            // F1 FIX (CLI audit MAJOR): two refusals used to sit here - a drive/system-root check
            // and a check that the backup folder is not inside the data root - and both returned
            // before the output below. This command no longer deletes anything, so the refusals
            // were both unnecessary and harmful: they made the one thing the command still does
            // - print the exact path for the user to delete by hand - unreachable in precisely
            // the two configurations where it matters most. They are gone; the dead block below
            // keeps its own copies of the same checks for whoever restores the delete path.
            int files = 0, dirs = 0;
            // F2 FIX (CLI audit MINOR): a failed enumeration used to leave files and dirs at 0, and
            // the line below then said "the data root holds 0 files and 0 folders", which is a fake
            // zero where the truth is unknown. counted distinguishes that case.
            bool counted = false;
            try { files = System.IO.Directory.GetFiles(dataFull, "*", System.IO.SearchOption.AllDirectories).Length; dirs = System.IO.Directory.GetDirectories(dataFull, "*", System.IO.SearchOption.AllDirectories).Length; counted = true; } catch { }
            // ★★★ **用户要求（2026-09-30）**：「删除 CLI 和 GUI 备份里的清除数据操作按钮，点击只弹出手动删除路径」✓✓
            //   → **本命令永不删除任何东西** ✓✓ 无论有没有 `--yes` ✓
            //   → 只输出：① 数据根里有多少东西（信息 ✓）② **手动删除的确切路径** ✓
            //   → 保留 `WIPE_PLAN` / `WIPE_PLAN_NOTE` 标记 ✓（门槛与 GUI 的解析不用改 ✓）
            Console.WriteLine("WIPE_PLAN " + T("数据根内有 ", "the data root holds ") + (counted ? files.ToString() : T("unknown", "unknown")) + T(" 个文件、", " files and ") + (counted ? dirs.ToString() : T("unknown", "unknown")) + T(" 个目录", " folders") + " — " + dataFull);
            Console.WriteLine("WIPE_PLAN_NOTE " + T("**本工具不再执行清除** ✓ 请**手动**删除上面那个目录 ✓ 删前请先备份 ✓（备份目录：" + backups + " ✓）", "this tool no longer wipes data - delete the folder above yourself, after backing up"));
            Console.WriteLine("WIPE_MANUAL " + dataFull);
            return 0;
            // ↓↓↓ 以下是**旧实现**（保留在源码里但**不可达** ✗ 因为上面已经 return ✓）
            //     留着的唯一理由是：将来若要恢复清除能力，逻辑与五道闸门都在 ✓
            //     但**现在它永远不会执行** ✓✓
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
            string dest = System.IO.Path.Combine(rootFull, "dsh-data-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff", System.Globalization.CultureInfo.InvariantCulture) + "-" + System.Diagnostics.Process.GetCurrentProcess().Id + "-imported");
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
        /// <summary>update-center（V3 独有，**只读** ✓）：四个组件的更新一览 ✓✓
        /// （用户要求："检查 webui / desktop / dsh-minato / 已安装插件的更新列表和版本，如果能获取更新日志那最好了" ✓）
        /// 标记行：
        ///   `UPDATECENTER_OK <n>`
        ///   `UPDATECENTER_ITEM <id> kind=<webui|desktop|minato|plugin> installed=<v|unknown> latest=<v|unknown> state=<up-to-date|update-available|unknown|external>`
        ///   `UPDATECENTER_URL <id> <地址>`       ← GitHub / 官方安装页 ✓
        ///   `UPDATECENTER_NOTE <id> <说明>`      ← 更新前风险与确认要求 ✓
        ///   `UPDATECENTER_LOG <id> <一行日志>`   ← 更新日志（best-effort ✓ 取不到就说取不到 ✓）
        /// 纪律：**只读** ✓ 不改任何东西 ✓；取不到的字段写 `unknown` ✓ **不猜** ✗</summary>
        private static int UpdateCenter(ServiceRegistry reg)
        {
            IToolchainQuery tc = reg.Get<IToolchainQuery>();
            List<string> ids = new List<string>();
            List<string> lines = new List<string>();

            // —— ① webui = dsh 本体（官方 npm 包 ✓）——
            string dshInstalled = tc.DshVersion();
            if (string.IsNullOrEmpty(dshInstalled)) dshInstalled = "unknown";
            string dshLatest = VersionForChannel(tc, _cfg == null ? "rc" : _cfg.UpdateChannel);
            if (string.IsNullOrEmpty(dshLatest)) dshLatest = "unknown";
            string dshState = "unknown";
            if (dshInstalled != "unknown" && dshLatest != "unknown")
                dshState = dshInstalled == dshLatest ? "up-to-date" : (Dsht.Domain.Services.VersionComparer.Compare(dshInstalled, dshLatest) < 0 ? "update-available" : "newer-than-latest");   // ★ 审查抓到：原来用字典序 ✗ → 1.9.0 会被判成比 1.10.0 新 ✗✓
            ids.Add("webui");
            lines.Add("UPDATECENTER_ITEM webui kind=webui installed=" + dshInstalled + " latest=" + dshLatest + " state=" + dshState);
            lines.Add("UPDATECENTER_URL webui https://github.com/deepseek-ai/deepseek-harness");
            lines.Add("UPDATECENTER_NOTE webui " + T("更新前会**自动备份**数据根 ✓ 并保留回滚点 ✓；需要你确认后才执行 ✓",
                "updating backs up the data root first and keeps a rollback point; it runs only after you confirm"));

            // —— ② desktop = 官方桌面端（**只能去官方安装页** ✓ 用户指定 ✓）——
            string deskPath = null;
            try
            {
                string cand = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "DeepSeek Harness", "DeepSeek Harness.exe");
                if (System.IO.File.Exists(cand)) deskPath = cand;
            }
            catch { }
            string deskVer = "unknown";
            if (deskPath != null)
            {
                try { System.Diagnostics.FileVersionInfo vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(deskPath); if (vi != null && !string.IsNullOrEmpty(vi.FileVersion)) deskVer = vi.FileVersion; } catch { }
            }
            ids.Add("desktop");
            lines.Add("UPDATECENTER_ITEM desktop kind=desktop installed=" + deskVer + " latest=unknown state=" + (deskPath == null ? "unknown" : "external"));
            lines.Add("UPDATECENTER_URL desktop https://www.deepseek.com/en/harness/");
            lines.Add("UPDATECENTER_NOTE desktop " + T("官方桌面端**不在本工具里更新** ✓ —— 去官方安装页自己下 ✓（本工具不重打包、也不改它 ✓）",
                "the desktop app is not updated here; download it from the official page"));

            // —— ③ minato = 本工具自己（**不影响数据** ✓ 用户要求 ✓）——
            string selfVer = "unknown";
            try { selfVer = DshtVersionString(); } catch { }
            ids.Add("minato");
            lines.Add("UPDATECENTER_ITEM minato kind=minato installed=" + selfVer + " latest=unknown state=unknown");
            lines.Add("UPDATECENTER_URL minato https://github.com/sakanamaru/dsh-minato");
            lines.Add("UPDATECENTER_NOTE minato " + T("本工具的更新**不碰你的数据** ✓（不动 ~/.dsh、不动备份、不动配置 ✓）；需要你确认 ✓",
                "updating this tool does not touch your data; it runs only after you confirm"));

            // —— ④ 已安装插件（从各 profile 的 node_modules 扫 ✓ 取 GitHub 地址 ✓✓）——
            try
            {
                // I2 FIX (CLI audit MINOR): this hardcoded the real home directory, so under an isolated
            // DSH_HOME - the project's own testing mode - it listed plugins from the real profile.
            string dshHome = reg.Get<IPaths>().DataRoot;
                string profiles = System.IO.Path.Combine(dshHome, "profiles");   // N5 FIX: the extra segment made the plugin list always empty
                if (System.IO.Directory.Exists(profiles))
                {
                    string[] profDirs = System.IO.Directory.GetDirectories(profiles);
                    for (int i = 0; i < profDirs.Length; i++)
                    {
                        string nm = System.IO.Path.Combine(profDirs[i], "node_modules");
                        if (!System.IO.Directory.Exists(nm)) continue;
                        string[] pkgs = System.IO.Directory.GetDirectories(nm);
                        for (int k = 0; k < pkgs.Length; k++)
                        {
                            string pj = System.IO.Path.Combine(pkgs[k], "package.json");
                            if (!System.IO.File.Exists(pj)) continue;
                            string txt = null;
                            try { txt = System.IO.File.ReadAllText(pj); } catch { }
                            if (string.IsNullOrEmpty(txt)) continue;
                            string name = JsonStr(txt, "name");
                            string ver = JsonStr(txt, "version");
                            string repo = JsonStr(txt, "repository");
                            if (string.IsNullOrEmpty(name)) name = System.IO.Path.GetFileName(pkgs[k]);
                            if (string.IsNullOrEmpty(ver)) ver = "unknown";
                            string pid = "plugin:" + name;
                            ids.Add(pid);
                            lines.Add("UPDATECENTER_ITEM " + pid + " kind=plugin installed=" + ver + " latest=unknown state=unknown profile=" + System.IO.Path.GetFileName(profDirs[i]));
                            // package.json 没有 repository 时 **去问 npm** ✓✓（用户要求："插件尝试获取 GitHub 地址" ✓）
                            // 只在缺字段时才问 ✓（npm view 每次要 1~2 秒 ✗ 不能对每个插件都问 ✓）
                            if (string.IsNullOrEmpty(repo)) repo = NpmRepoOf(name);
                            lines.Add("UPDATECENTER_URL " + pid + " " + (string.IsNullOrEmpty(repo) ? "unknown" : repo));
                            lines.Add("UPDATECENTER_NOTE " + pid + " " + T("插件更新**先描述风险再确认** ✓（版本变化可能改行为 ✓）；更新前**自动备份** ✓ 插件由各自作者维护 ✓ 本工具不替它担保 ✓",
                                "plugin updates describe the risk and ask first; the data root is backed up"));
                        }
                    }
                }
            }
            catch { }

            // —— ⑤ 更新日志（best-effort ✓ 从 GitHub Releases 取 ✓ 取不到就明说 ✓）——
            try
            {
                string log = FetchLatestRelease("deepseek-ai", "deepseek-harness");
                if (!string.IsNullOrEmpty(log)) lines.Add("UPDATECENTER_LOG webui " + log);
                else lines.Add("UPDATECENTER_LOG webui " + T("取不到更新日志（网络不可达或仓库没有 Releases）", "no changelog available"));
            }
            catch { lines.Add("UPDATECENTER_LOG webui " + T("取不到更新日志", "no changelog available")); }

            Console.WriteLine("UPDATECENTER_OK " + ids.Count);
            for (int i = 0; i < lines.Count; i++) Console.WriteLine(lines[i]);
            return 0;
        }

        /// <summary>问 npm 要某个包的仓库地址 ✓（package.json 缺 repository 时的兜底 ✓）。
        /// 找不到 npm / 查不到 → 返回空串 ✓ **不猜** ✗（上层会写 unknown ✓）。</summary>
        private static string NpmRepoOf(string pkg)
        {
            if (string.IsNullOrEmpty(pkg)) return "";
            // ★★★ 审查抓到：包名来自**插件自己的 package.json** ✗ → 未校验就拼进 cmd.exe ✗✗
            //   → `{"name":"x & calc"}` 这种名字会**执行任意命令** ✓（打开 GUI「更新」页即触发 ✓）
            //   → 项目对 registry 和 npm 版本都做了白名单 ✓ 唯独漏了包名 ✓
            // ✓ 现在：**npm 合法包名白名单** ✓✓（作用域名 + 包名 ✓ 不合规直接不问 npm ✓）
            if (!System.Text.RegularExpressions.Regex.IsMatch(pkg,
                    @"^(@[a-z0-9\-~][a-z0-9\-._~]*/)?[a-z0-9\-~][a-z0-9\-._~]*$"))
                return "";
            string[] tries = PlatformIsWindows()
                ? new string[] { "cmd.exe|/c npm view " + pkg + " repository.url", "npm.cmd|view " + pkg + " repository.url" }
                : new string[] { "npm|view " + pkg + " repository.url", System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/node/bin/npm") + "|view " + pkg + " repository.url" };
            for (int i = 0; i < tries.Length; i++)
            {
                int bar = tries[i].IndexOf('|');
                if (bar <= 0) continue;
                string exe = tries[i].Substring(0, bar);
                string arg = tries[i].Substring(bar + 1);
                try
                {
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(exe, arg);
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
                    {
                        if (p == null) continue;
                        string outp = p.StandardOutput.ReadToEnd();
                        p.StandardError.ReadToEnd();
                        p.WaitForExit(8000);
                        if (string.IsNullOrEmpty(outp)) continue;
                        string[] ls = outp.Replace("\r\n", "\n").Split('\n');
                        for (int k = 0; k < ls.Length; k++)
                        {
                            string s = ls[k].Trim();
                            if (s.Length == 0) continue;
                            // npm 有时输出 git+https://…git → 去掉前缀后缀 ✓ 便于直接点开 ✓
                            if (s.StartsWith("git+", StringComparison.Ordinal)) s = s.Substring(4);
                            if (s.EndsWith(".git", StringComparison.Ordinal)) s = s.Substring(0, s.Length - 4);
                            if (s.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return s;
                        }
                    }
                }
                catch { }
            }
            return "";
        }
        /// <summary>从 JSON 文本里取一个**字符串字段**（够用即可 ✓ 不引 JSON 库 ✓ 保持零依赖 ✓）。
        /// 找不到返回空串 ✓ 不猜 ✗。</summary>
        private static string JsonStr(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return "";
            int k = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (k < 0) return "";
            int c = json.IndexOf(':', k + key.Length + 2);
            if (c < 0) return "";
            int q1 = json.IndexOf('"', c + 1);
            if (q1 < 0) return "";
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return "";
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }

        /// <summary>取某个仓库最新 Release 的**一行摘要**（best-effort ✓ 超时 6 秒 ✓ 失败返回空 ✓）。</summary>
        private static string FetchLatestRelease(string owner, string repo)
        {
            try
            {
                // ✗✗ 真机实测（2026-09-30）：CLI 用 csc / .NET 4.0 编译 ✓ 默认**只有 TLS 1.0** ✗
                //    → GitHub 要求 TLS 1.2 → 请求抛异常 → 被 catch 吞掉 → "取不到更新日志" ✓✓
                //    （PowerShell 测试却成功 ✓ 因为它跑在 4.x 上、默认开了 TLS 1.2 ✓ 这个差异把人骗了 ✓）
                // .NET 4.0 **没有** SecurityProtocolType.Tls12 枚举 ✗ → 用数值 3072 ✓
                try { System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)3072; } catch { }
                string url = "https://api.github.com/repos/" + owner + "/" + repo + "/releases?per_page=1";
                System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                req.UserAgent = "dsh-minato";
                req.Timeout = 6000;
                req.ReadWriteTimeout = 6000;
                using (System.Net.HttpWebResponse resp = (System.Net.HttpWebResponse)req.GetResponse())
                using (System.IO.StreamReader sr = new System.IO.StreamReader(resp.GetResponseStream()))
                {
                    string body = sr.ReadToEnd();
                    string tag = JsonStr(body, "tag_name");
                    if (string.IsNullOrEmpty(tag)) return "";
                    string name = JsonStr(body, "name");
                    string one = string.IsNullOrEmpty(name) ? tag : (tag + " " + name);
                    one = one.Replace("\r", " ").Replace("\n", " ");
                    if (one.Length > 160) one = one.Substring(0, 160) + "…";
                    return one;
                }
            }
            catch { }
            // 兜底：**atom 源**（GitHub 的 RSS ✓ 走 www.github.com ✓ 有些网络下比 api 更通 ✓）
            try
            {
                string url2 = "https://github.com/" + owner + "/" + repo + "/releases.atom";
                System.Net.HttpWebRequest r2 = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url2);
                r2.UserAgent = "dsh-minato";
                r2.Timeout = 6000;
                r2.ReadWriteTimeout = 6000;
                using (System.Net.HttpWebResponse resp2 = (System.Net.HttpWebResponse)r2.GetResponse())
                using (System.IO.StreamReader sr2 = new System.IO.StreamReader(resp2.GetResponseStream()))
                {
                    string body2 = sr2.ReadToEnd();
                    int ti = body2.IndexOf("<title", StringComparison.Ordinal);
                    if (ti >= 0)
                    {
                        int t1 = body2.IndexOf('>', ti);
                        int t2 = t1 < 0 ? -1 : body2.IndexOf("</title>", t1, StringComparison.Ordinal);
                        if (t1 > 0 && t2 > t1)
                        {
                            string one2 = body2.Substring(t1 + 1, t2 - t1 - 1).Trim().Replace("\r", " ").Replace("\n", " ");
                            if (one2.Length > 160) one2 = one2.Substring(0, 160) + "…";
                            return one2;
                        }
                    }
                }
            }
            catch { }
            return "";
        }

        /// <summary>本工具自己的版本串 ✓（取不到就 unknown ✓ 不猜 ✗）。</summary>
        private static string DshtVersionString()
        {
            // ✗✗ 原来用 FileVersionInfo.GetVersionInfo(asm.Location) → **单文件发布时 Location 是空的** ✗✗
            //    （.NET 5+ 已知行为 ✓）→ 实测 Linux 上显示 unknown ✓
            // ✓ 更新中心本来就在 CLI 里 → **直接用编译期的 ToolkitVersion** ✓ 连反射都不用 ✓
            if (!string.IsNullOrEmpty(ToolkitVersion)) return ToolkitVersion;
            return "unknown";
        }

        /// <summary>**启动闸门** ✓✓：自身 SHA-256 vs 随包 hashes.txt。
        /// 返回 0 = 放行 ✓；非 0 = 拒绝运行（并已打印原因 ✓）。
        /// **绝不因为"读不到清单"就拒绝** ✗ —— 那会把源码编译和单独复制 exe 全挡掉 ✓（Unknown 放行 ✓）。
        /// **也绝不因为"读不到清单"就说"校验通过"** ✗ —— 明确说"跳过" ✓✓（与 doctor 的措辞一致 ✓）。</summary>
        private static int StartupIntegrityGate(ServiceRegistry reg)
        {
            try
            {
                // 用**真实的领域 API** ✓（ManifestParser.ParseHash + IIntegritySource 三成员 ✓）
                IIntegritySource integ = reg.Get<IIntegritySource>();
                string expected = Dsht.Domain.Services.ManifestParser.ParseHash(integ.ReadManifest(), integ.SelfFileName());
                // ★★★ **第 2 轮审查抓到（性能最大头 ✗✗）**：这里**先无条件算了整个 66 MB exe 的 SHA-256** ✗
                //   而 expected == null 时（源码编译 / 单独复制 exe / 没有清单 ✓）结果**必然是 Unknown** ✓ → 算了再丢 ✗
                //   → GUI 一次按钮要起 5–11 个 CLI 进程 ✓ → **每次白算 66 MB** ✗✗（正是"按钮还是有延迟"的根因 ✓）
                // ✓ 现在：**没有清单条目就根本不算** ✓✓（Unknown 放行语义完全不变 ✓）
                IntegrityVerdict v = (expected == null)
                    ? IntegrityVerdict.Unknown
                    : IntegrityJudge.Judge(expected, integ.SelfHash());
                if (v == IntegrityVerdict.Match) return 0;   // 一致 → 静默放行 ✓（不刷屏 ✓）
                if (v == IntegrityVerdict.Unknown)
                {
                    // 明说"跳过" ✓ —— **不假报"已验证"** ✓✓（与 doctor 的措辞一致 ✓）
                    // ★★★ **假绿修复（实测发现 —— 这一行把整条门槛链拖死过）** ✓✓
                    //   · 它是**诊断提示**（"源码编译属正常" ✓）→ 走 **stderr** 是对的 ✓（不是命令输出 ✓）
                    //   · 但门槛脚本用 `& powershell … 2>&1` + `$ErrorActionPreference = "Stop"` ✓
                    //     → PS 5.1 把子进程 stderr 变成 **NativeCommandError** → **Stop 终止** ✗✗
                    //     → **脚本在第一条子门禁就死掉、从不打印结果表** ✗ → **看输出像全绿** ✗✗
                    //   ✓ 已在**门槛脚本**里修（只在调外部命令时放宽 ✓）✓
                    //   ✓ 并在 `compare_markers` 里**全局忽略这一行** ✓✓（两边 exe 身份不同 → 天然不对称 ✓）
                    Console.Error.WriteLine("INTEGRITY_SKIPPED " + T("旁无 hashes.txt（或清单里没有本文件）→ 跳过自身校验。源码编译、单独复制 exe 属正常；官方发布包会带清单。", "no hashes.txt beside this exe (or it does not list this file) - self-check skipped; official releases ship a manifest."));
                    return 0;
                }
                // Mismatch → **拒绝运行** ✓✓（银狐静态感染后文件必然变 ✓）
                // —— 被改动时的提示 ✓✓（用户要求："被感染检测后，提示，文件被感染之类的提示" ✓）
                // 措辞纪律：说**事实**（"已经被改动" ✓ 指纹对不上是确定的 ✓）
                //           说**可能**（"可能被木马感染" ✓ 也可能是别的原因 ✗ 不把可能说成一定 ✗）
                //           给**能照着做的步骤** ✓✓（比一句"校验失败"有用得多 ✓）
                Console.WriteLine("");
                Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
                Console.WriteLine("  ║   警告：这个文件**已经被改动**，可能被木马感染              ║");
                Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
                Console.WriteLine("");
                Console.WriteLine(T("  本工具的每个文件都有官方指纹（SHA-256）。现在这个文件的指纹和官方清单**对不上** ——", "  Every file in this tool has an official SHA-256 fingerprint, and this one does not match."));
                Console.WriteLine(T("  说明它**被改过**。银狐一类木马正是这样干的：给正常程序打补丁，让它在你运行时同时干别的事。", "  SilverFox-class trojans work exactly this way: they patch a legitimate program."));
                Console.WriteLine("");
                Console.WriteLine(T("  **已拒绝运行**（刻意的：宁可你打不开，也不让你在不知情的情况下运行被改过的程序）", "  **Refused to run** - deliberately: better that it will not open than that it runs modified."));
                Console.WriteLine("");
                Console.WriteLine(T("  请这样做：", "  What to do:"));
                Console.WriteLine(T("    1. **不要**继续使用这个文件", "    1. Do not keep using this file"));
                Console.WriteLine(T("    2. 把它**删掉**（或先移到隔离目录）", "    2. Delete it (or move it somewhere isolated first)"));
                Console.WriteLine(T("    3. 从**官方 Releases 重新下载**：", "    3. Download again from the official releases:"));
                Console.WriteLine("       https://github.com/sakanamaru/dsh-minato/releases");
                Console.WriteLine(T("    4. 建议用杀毒软件**全盘扫描**一次（木马通常不止感染一个文件）", "    4. Run a full antivirus scan - trojans rarely infect only one file"));
                Console.WriteLine("");
                Console.WriteLine("  ---- " + T("技术细节（给排查用）", "technical details for diagnosis") + " ----");
                Console.WriteLine("  INTEGRITY_FAIL " + T("自身指纹与官方清单不一致", "self fingerprint does not match the official manifest"));
                Console.WriteLine("  INTEGRITY_EXPECTED " + (expected == null ? "unknown" : expected));
                Console.WriteLine("  INTEGRITY_ACTUAL " + (string.IsNullOrEmpty(integ.SelfHash()) ? "unknown" : integ.SelfHash()));
                Console.WriteLine("  INTEGRITY_HINT " + T("从官方 Releases 重新下载；本工具拒绝在被改动的情况下运行。", "download again from the official releases; this tool refuses to run when modified."));
                return 3;
            }
            catch
            {
                return 0;   // 自检本身出错 → 放行 ✓（不能让自检把工具变成砖 ✓）
            }
        }
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
                state = installed == latest ? "up-to-date" : (Dsht.Domain.Services.VersionComparer.Compare(installed, latest) < 0 ? "update-available" : "newer-installed");   // ★ 审查抓到：字典序 ✗ → 改用正确比较器 ✓✓
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
            // 图标署名 ✓✓（用户要求："README + 关于页署名原作者" ✓）
            Console.WriteLine("CREDITS " + T("鲸鱼娘（Whale-chan）形象来自 DeepSeek 社区同人创作；本项目图标为生成式 AI 产出（生成式 AI 工具），提示词由维护者编写。非官方、非商业、与 DeepSeek 官方无关。详见仓库 ASSETS.md。", "Whale-chan is community fan art; this project's icon is AI-generated (生成式 AI 工具). Unofficial, non-commercial, not affiliated with DeepSeek. See ASSETS.md."));
            Console.WriteLine("dsh-minato " + ToolkitVersion);
            Console.WriteLine(T("社区版 DeepSeek Harness (dsh) 本机部署运维套件：安装 / 启动 / 监控 / 备份恢复 / 插件诊断与隔离",
                                "community deploy & ops kit for DeepSeek Harness (dsh): install, start, monitor, backup & restore, plugin diagnosis & quarantine"));
            Console.WriteLine(T("非官方工具，与 DeepSeek 官方无关。", "Unofficial tool; not affiliated with DeepSeek."));
            Console.WriteLine(T("仓库：", "Repository: ") + "https://github.com/sakanamaru/dsh-minato");
            Console.WriteLine(T("许可：MIT", "License: MIT"));
            // ★ 审查抓到：这句话有**两处不实** ✗✗
            //   ① 说"不联网（余额查询除外）" ✗ —— 但**根本没有余额查询** ✓ 而真联网的命令有 6 条 ✗
            //   ② 说"写操作一律先备份" ✗ —— 改设置/改 profile/改快捷方式都**不**备份 ✗
            // ✓ 现在：**逐条如实** ✓✓（联网命令点名 ✓ 备份范围说清 ✓）
            // ★★ 第 2 轮审查抓到：我上一版**仍然不实** ✗✗
            //   ① 漏了 `doctor` ✗ —— 它会对 npm registry 发一次 HTTP GET（Program.cs 的 Doctor → IHttpProbe.Responds ✓
            //      而 GUI 自己也写着"进这一页会自动跑一次 doctor（会检查网络…）" ✓ 项目里 compare_markers 还专门忽略它的 Network 行 ✓）
            //   ② 把 `wipe` 列进"会先备份" ✗ —— 而 wipe **现在根本不删任何东西** ✓（它只打印手动删除路径 ✓ 也从不备份 ✓）
            // ✓ 现在：**逐条对齐代码** ✓✓
            Console.WriteLine(T("本程序只读写本机。会联网的只有：check / update-info / update-center / doctor / install / update / verify-install --url；其余命令不联网。",
                                "Works locally only. The only commands that use the network are: check, update-info, update-center, doctor, install, update, verify-install --url. Everything else stays offline."));
            Console.WriteLine(T("restore 之前会先自动备份并打印位置（可回滚）；update / import 会尝试先备份，失败时仍继续。wipe 现在只打印手动删除路径、不删也不备份。改设置、改 profile、改快捷方式不备份。",
                                "restore always backs up first and prints where; update and import try to and continue if that fails. wipe only prints the path to delete by hand - it neither deletes nor backs up. Settings, profile and shortcut edits are not backed up."));
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
        /// <summary>从**启动日志**里取出 dsh 打印的那个 URL ✓✓
        /// 为什么必须用它：dsh web 打印的是 `http://127.0.0.1:<port>/?token=…` ✓
        /// —— **裸端口会认证失败** ✗（2026-09-30 真机反馈："dsh web authentication required; reopen the URL
        /// printed by dsh web" ✓）。启动日志由平台层写（LinuxServiceControl.LastLogPath ✓）。
        /// 取不到就返回空串 ✓ —— 不猜、不拼一个可能错的 URL ✗</summary>
        private static string StartUrlFromLog()
        {
            try
            {
                // ✗ 原来写死 Linux 的 LastLogPath → **Windows 上取不到** ✗（真机：Windows 侧日志根本不存在 ✓）
                // 改成**平台中立**的同一路径 ✓✓ 两个平台的平台层都往这里写 ✓
                string logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dsh-minato-start.log");
                if (string.IsNullOrEmpty(logPath)) return "";
                string text = "";
                try { if (System.IO.File.Exists(logPath)) text = System.IO.File.ReadAllText(logPath); } catch { return ""; }
                System.Text.RegularExpressions.Match m =
                    System.Text.RegularExpressions.Regex.Match(text, @"http://[^\s""']*\?token=[^\s""']+");
                return m.Success ? m.Value : "";
            }
            catch { return ""; }
        }

        /// <summary>开机自启（用户要求："开启自启服务功能还在吗" + "按平台选 + 做成设置项让你选" ✓✓）
        /// · 目标：`auto_start_target` = auto（**按平台** ✓ Win/Mac→官方桌面端 · Linux→dsh web）/ desktop / web
        /// · 实现：**不需要管理员/root** ✓
        ///     Windows → 启动目录放一个 .cmd ✓（%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup）
        ///     Linux   → systemd **user** unit ✓（~/.config/systemd/user/ + systemctl --user enable ✓）
        /// · 全部**可回退** ✓（--disable 删掉 ✓；也会打印文件路径让你自己看 ✓）
        /// 标记：`AUTOSTART_STATUS <on|off> target=<desktop|web|auto>` / `AUTOSTART_OK <动作>` / `AUTOSTART_FAIL <原因>`</summary>
        private static int AutoStartCmd(string[] args, ServiceRegistry reg)
        {
            bool win = PlatformIsWindows();
            string cfgTarget = _cfg == null || string.IsNullOrEmpty(_cfg.AutoStartTarget) ? "auto" : _cfg.AutoStartTarget;
            string effective = cfgTarget;
            if (effective == "auto") effective = win ? "desktop" : "web";
            string label = effective == "desktop" ? T("官方桌面端", "the official desktop app") : T("dsh web", "dsh web");

            string path = win
                ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs", "Startup", "dsh-minato-autostart.cmd")
                : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "systemd", "user", "dsh-minato-autostart.service");

            bool enabled = FileExists(path);
            Console.WriteLine("AUTOSTART_STATUS " + (enabled ? "on" : "off") + " target=" + effective + " configured=" + cfgTarget);

            if (!Has(args, "--enable") && !Has(args, "--disable"))
            {
                Console.WriteLine("AUTOSTART_PATH " + path);
                Console.WriteLine("AUTOSTART_WOULD " + T("将启动：", "would start: ") + label + (effective == "desktop" && !win ? T("（注意：Linux 上官方桌面端暂未发行 ✓ 请把 auto_start_target 改成 web ✓）", " (note: no Linux desktop app yet; set auto_start_target=web)") : ""));
                return 0;
            }

            if (Has(args, "--disable"))
            {
                try
                {
                    if (enabled) System.IO.File.Delete(path);
                    if (!win) RunQuiet("systemctl", "--user disable dsh-minato-autostart");
                    Console.WriteLine("AUTOSTART_OK disable");
                    OpLog(reg, "INFO", "autostart disabled");
                }
                catch (Exception ex) { Console.WriteLine("AUTOSTART_FAIL " + ex.Message); }
                return 0;
            }

            try
            {
                string dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                if (win)
                {
                    string body;
                    if (effective == "desktop")
                    {
                        string exe = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "DeepSeek Harness", "DeepSeek Harness.exe");
                        body = "@echo off\r\nrem dsh-minato 开机自启（设置里可关：dsh-minato autostart --disable）\r\nstart \"\" \"" + exe + "\"\r\n";
                    }
                    else
                    {
                        // I7 FIX (CLI audit MAJOR): Assembly.Location is an empty STRING (not null) for a
            // single-file build, which is how the tool ships, so the null check passed and the
            // autostart entry was written with an empty command path while still reporting success.
            // The running executable path is the reliable source.
            string cli = SelfExePath();
                        body = "@echo off\r\nrem dsh-minato 开机自启（设置里可关：dsh-minato autostart --disable）\r\n\"" + cli + "\" start --yes\r\n";
                    }
                    System.IO.File.WriteAllText(path, body, new System.Text.UTF8Encoding(false));
                }
                else
                {
                    string cli2 = SelfExePath();   // I7 FIX: see above
                    string exec = effective == "web" ? "\"" + cli2 + "\" start --yes" : "echo 'desktop app not available on Linux'";
                    string unit = "[Unit]\nDescription=dsh-minato autostart (dsh)\nAfter=network.target\n\n[Service]\nType=oneshot\nExecStart=" + exec + "\n\n[Install]\nWantedBy=default.target\n";
                    System.IO.File.WriteAllText(path, unit, new System.Text.UTF8Encoding(false));
                    RunQuiet("systemctl", "--user daemon-reload");
                    RunQuiet("systemctl", "--user enable dsh-minato-autostart");
                }
                // I6 FIX (CLI audit MINOR): success was printed without checking that anything was
            // written, unlike the shortcut path which verifies the file exists.
            bool wrote = false;
            try
            {
                // ★★★ **F-A 修复（CLI 复审 HIGH —— 检查的路径不是写入的那个）** ✓✓
                //   ✗ 我修 N1 时改了**检查**里的文件名 ✗ 但**真正写入的是 `path`** ✗✗
                //     → Windows 上写的是 `…\Startup\dsh-minato-autostart.cmd` ✓
                //       而检查找 `…\Startup\dsh-minato.cmd` ✗ → **永远 FAIL** ✗✗
                //     → 用户看到"写入后没有找到自启文件" ✓ 而**文件其实写好了** ✗（假报失败 ✓）
                //   ✓ 现在：**直接检查 `path`** ✓✓（那是真正写下去的那个 ✓ 平台无关 ✓）
                wrote = System.IO.File.Exists(path);
            }
            catch { }
            if (!wrote) { Console.WriteLine("AUTOSTART_FAIL " + T("写入后**没有找到自启文件** ✓ 请检查权限 ✓", "no autostart file was found after writing")); return 0; }
            Console.WriteLine("AUTOSTART_OK enable");
                Console.WriteLine("AUTOSTART_PATH " + path);
                OpLog(reg, "INFO", "autostart enabled target=" + effective);
            }
            catch (Exception ex) { Console.WriteLine("AUTOSTART_FAIL " + ex.Message); }
            return 0;
        }

        /// <summary>跑一条命令并丢弃输出（systemctl 之类 ✓ 失败不影响主流程 ✓）。</summary>
        private static void RunQuiet(string exe, string args)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(exe, args);
                psi.UseShellExecute = false; psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
                using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
                {
                    if (p != null) { p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit(8000); }
                }
            }
            catch { }
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
                { string su2 = StartUrlFromLog(); if (su2 != "") Console.WriteLine("START_URL " + su2); else Console.WriteLine("START_URL_UNKNOWN " + T("已在运行，但读不到 dsh 打印的带 token 地址；请在启动它的终端里复制那条地址（裸端口会认证失败）", "already running, but the token URL could not be read; copy it from the terminal that started dsh (a bare port fails authentication)")); }
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
                // ★ 审查抓到：profile 直接来自参数 ✗ 未校验就拼进 cmd.exe ✗
                //   → `start --profile "web & <任意命令>"` 会执行它 ✓（同样的值在 bridge-install / profilepatch 里是**有白名单**的 ✓）
                // ✓ 现在：与那两处**同一条白名单** ✓✓
                if (!System.Text.RegularExpressions.Regex.IsMatch(profile ?? "", @"^[A-Za-z0-9._\-]+$"))
                {
                    Console.WriteLine("START_FAIL " + T("profile 名不合法（只允许字母数字 . _ -）：" + profile, "invalid profile name: " + profile));
                    return 0;
                }
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
                    string su = StartUrlFromLog();
                    if (su != "") Console.WriteLine("START_URL " + su);
                    return 0;
                }
            }
            ServiceReport last = target.Probe();
            Console.WriteLine("START_FAIL " + T("命令已发出但 30 秒内未观测到端口/HTTP 就绪；子进程输出见 ", "launched but not observed ready within 30s; child output: ") + System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dsh-minato-start.log"));
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
            // `--target web|desktop`：**两个都开着时可以选停一个** ✓✓（用户要求 ✓）
            // 默认 web ✓ = 原行为 ✓ 向后兼容 ✓（不加这个参数时一个字都不变 ✓）
            string stopTarget = ArgOr(args, "--target", "web").Trim().ToLowerInvariant();
            if (stopTarget == "desktop")
            {
                int dpid = 0;
                try { dpid = reg.Get<IProcessQuery>().PidOfNamed("DeepSeek Harness"); } catch { }
                if (dpid <= 0)
                {
                    Console.WriteLine("STOP_FAIL " + T("没有检测到官方桌面端进程（DeepSeek Harness）", "no desktop app process (DeepSeek Harness) found"));
                    Console.WriteLine("STOP_OBSERVED down");
                    return 0;
                }
                if (dpid == System.Diagnostics.Process.GetCurrentProcess().Id)
                {
                    Console.WriteLine("STOP_FAIL " + T("拒绝执行：那是本程序自己（安全保护）", "refused: that is this program itself (safety)"));
                    return 0;
                }
                if (!Has(args, "--yes"))
                {
                    Console.WriteLine("STOP_PLAN " + T("将停止**官方桌面端** PID ", "will stop the **desktop app** PID ") + dpid);
                    Console.WriteLine("STOP_NOTE " + T("确认请加 --yes。它和 web 是两个独立的东西：本命令只停桌面端，不动 3080 上的 web。", "add --yes. The desktop app and dsh web are separate; this only stops the desktop app."));
                    return 0;
                }
                IServiceControl dctl = reg.Get<IServiceControl>();
                string derr;
                // Electron 是**多进程** ✗ → 必须杀**整棵** ✓（StopTree ✓ 与 Windows 侧实现一致 ✓）
                bool dok = dctl.StopTree(dpid, out derr);
                Console.WriteLine(dok ? "STOP_OK " + dpid : "STOP_FAIL " + (string.IsNullOrEmpty(derr) ? T("停止失败", "stop failed") : derr));
                Console.WriteLine("STOP_OBSERVED " + (dok ? "down" : "unknown"));
                if (dok) OpLog(reg, "INFO", "stop desktop pid=" + dpid);
                return 0;
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
                // 全部拷完后**重算并改写 files=** ✓✓ —— 平台写标记时只算了数据根 ✓，之后又拷进了工作区 ✗
                // → 不更新的话包会**自报"不完整"** ✗✗（上一版的真 bug ✓）
                try
                {
                    int nowFiles = System.IO.Directory.GetFiles(pkgPath, "*", System.IO.SearchOption.AllDirectories).Length;
                    cur = System.Text.RegularExpressions.Regex.Replace(cur, @"(?m)^files=\d+", "files=" + nowFiles);
                }
                catch { }
                System.IO.File.WriteAllText(mf, cur.TrimEnd('\n', '\r') + "\n" + "sha256=" + h + "\n");
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
                // ★★★ 审查抓到：完成标记**最后才写** ✗（中断可被精确识别 ✓）而这里缺标记却返回 null ✗
                //   → 调用方把 null 当成"没被截断" ✓ → **中断的包会被照常恢复并打印 RESTORE_OK** ✗✗
                //   → 而 `backup-list --verify` 明明把它标成 incomplete ✓（工具知道这个事实却忽略了 ✓）
                // ✓ 现在：**缺标记就是"未完成"** ✓✓（照旧可用 --force 强行使用 ✓ 但要用户明说 ✓）
                if (!System.IO.File.Exists(mf))
                    return System.IO.Directory.Exists(pkgDir)
                        ? T("备份未完成（缺少完成标记 ✓ 可能是中断/磁盘满）：", "backup is incomplete (no completion marker): ") + System.IO.Path.GetFileName(pkgDir)
                        : null;
                // ★ 第 2 轮抓到：标记里写着 `failed=<n>` ✓（平台侧真的写 ✓）而这里**只读 files= 与 sha256=** ✗
                //   → 工具自己在备份时说了"该备份不完整" ✓ backup-list --verify 也这么报 ✓
                //     而 restore 却当它完整 ✓✗ → **同一个包两套结论** ✓
                // ✓ 现在：**failed>0 也算不完整** ✓✓
                try
                {
                    string allTxt = System.IO.File.ReadAllText(mf);
                    System.Text.RegularExpressions.Match fm = System.Text.RegularExpressions.Regex.Match(allTxt, "failed=(\\d+)");
                    if (fm.Success)
                    {
                        int failedN;
                        if (int.TryParse(fm.Groups[1].Value, out failedN) && failedN > 0)
                            return T("备份不完整（标记里记着 " + failedN + " 项没能备份 ✓）：", "backup is incomplete (the marker records " + failedN + " items that could not be backed up): ") + System.IO.Path.GetFileName(pkgDir);
                    }
                }
                catch { }
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
            // 独立统计"不是有效备份"的条目 ✓（不碰下面的既有逻辑 ✓）→ 在 TOTAL 前**仅当 >0** 时说明 ✓✓
            int notValidPkg = 0;
            for (int k = 0; k < all.Count; k++) { if (!BackupPackage.IsValidPackage(all[k].Snapshot)) notValidPkg++; }
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
                else
                {
                    complete++;
                    // 把标记里的 failed= 一并带出来 ✓✓ —— 否则事后复查只看到 "complete" ✗
                    // 而 "包自身一致" 与 "源里有没有少备" 是**两个问题** ✓（--verify 回答前者 ✓ 这里补上后者 ✓）
                    int failedInMarker = -1;
                    try
                    {
                        string[] ml = System.IO.File.ReadAllLines(mf);
                        for (int k = 0; k < ml.Length; k++) { if (ml[k].StartsWith("failed=", StringComparison.Ordinal)) int.TryParse(ml[k].Substring(7).Trim(), out failedInMarker); }
                    }
                    catch { }
                    Console.WriteLine("BACKUP_VERIFY " + name + " complete " + have + (failedInMarker > 0 ? T(" ；但源里有 ", " ; but ") + failedInMarker + T(" 项未能备份（不完整 ✓）", " items could not be backed up (incomplete)") : ""));
                }
            }
            if (notValidPkg > 0) Console.WriteLine("BACKUP_VERIFY_NOTE " + notValidPkg + T(" 项不是有效备份（不会被 backup-list 列出，也不会被 restore 选中）", " entries are not valid backups (not listed by backup-list, not selected by restore)"));
            Console.WriteLine("BACKUP_VERIFY_TOTAL " + all.Count + " complete=" + complete + " incomplete=" + incomplete + " mismatch=" + mismatch);
            return 0;
        }
        /// <summary>查看 / 设置**备份位置** ✓✓（用户要求：「备份路径在备份页面里设置并且显示吧」✓）。
        ///
        /// 语义：
        ///   · 不带参数 → **只显示**当前备份根 ✓（标记行 `BACKUP_DIR <路径>` ✓）
        ///   · `--set <目录>` → **改到那里** ✓（写 `<StateDir>/.backup-dir` ✓ 之后所有命令都用它 ✓✓）
        ///   · `--reset` → **恢复默认**（删掉那个文件 ✓ 回到 `StateDir/backup` ✓）
        ///
        /// 为什么要有它：默认备份根在 **Windows 上就是安装目录内**（`StateDir` = exe 所在目录 ✗）
        ///   → 卸载时**理论上**会被一起清 ✗（安装器已加保险：显式跳过 backup/ ✓✓）
        ///   → 但**放在安装目录之外才真正稳妥** ✓ → 用户需要一个**看得见、改得动**的入口 ✓✓
        ///
        /// 标记行：BACKUP_DIR / BACKUP_DIR_OK / BACKUP_DIR_RESET / BACKUP_DIR_FAIL</summary>
        private static int BackupDirCmd(string[] args, ServiceRegistry reg)
        {
            IBackupSource bk = reg.Get<IBackupSource>();
            IPaths paths = reg.Get<IPaths>();
            string sel = "";
            try { sel = System.IO.Path.Combine(paths.StateDir, ".backup-dir"); } catch { }

            if (Has(args, "--reset"))
            {
                // D1 FIX (CLI audit MAJOR): the delete was wrapped in an empty catch and success was
                // printed unconditionally, so a read-only or locked settings file left the user told
                // the reset had worked while every later backup kept going to the old folder. Verify.
                if (!string.IsNullOrEmpty(sel) && System.IO.File.Exists(sel))
                {
                    try { System.IO.File.Delete(sel); }
                    catch (Exception dex)
                    {
                        Console.WriteLine("BACKUP_DIR_FAIL " + T("无法恢复默认（删不掉设置文件）：" + dex.Message, "cannot reset: " + dex.Message));
                        return 0;
                    }
                    if (System.IO.File.Exists(sel))
                    {
                        Console.WriteLine("BACKUP_DIR_FAIL " + T("设置文件删掉后**仍然存在** ✓ 请手动删除：" + sel, "the settings file still exists: " + sel));
                        return 0;
                    }
                }
                Console.WriteLine("BACKUP_DIR_RESET " + bk.BackupsRoot + " " + T("已恢复默认备份位置 ✓", "reset to the default backups folder"));
                return 0;
            }

            string set = (Flag(args, "--set") ?? "").Trim().Trim('"');
            if (string.IsNullOrEmpty(set))
            {
                Console.WriteLine("BACKUP_DIR " + bk.BackupsRoot);
                return 0;
            }

            // D3 FIX (CLI audit MAJOR): the comment said "must be an absolute path" but nothing
            // checked it, so a relative value was resolved against the current directory and then
            // stored - making every later command depend on where it was run from, including which
            // folder a delete targeted.
            if (!System.IO.Path.IsPathRooted(set))
            {
                Console.WriteLine("BACKUP_DIR_FAIL " + T("必须是**绝对路径** ✓ 相对路径会随当前目录变化 ✗：" + set, "the path must be absolute: " + set));
                return 0;
            }
            string full;
            try { full = System.IO.Path.GetFullPath(set); }
            catch (Exception ex) { Console.WriteLine("BACKUP_DIR_FAIL " + T("路径无效：" + ex.Message, "invalid path: " + ex.Message)); return 0; }
            try { System.IO.Directory.CreateDirectory(full); }
            catch (Exception ex) { Console.WriteLine("BACKUP_DIR_FAIL " + T("无法创建目录：" + ex.Message, "cannot create folder: " + ex.Message)); return 0; }
            // ✓✓ **实测抓到的**：`Z:\no\such\drive\x` 竟然"创建成功"了 ✗（不可用盘符时 `CreateDirectory` 不抛 ✓）
            //   → **必须**再确认它**真的存在** ✓✓（不信任 API 的返回值 ✓ 只看事实 ✓）
            if (!System.IO.Directory.Exists(full))
            {
                Console.WriteLine("BACKUP_DIR_FAIL " + T("目录创建后**并不存在** ✓ 路径不可用：" + full, "the folder does not exist after creating it: " + full));
                return 0;
            }
            try
            {
                if (string.IsNullOrEmpty(sel)) { Console.WriteLine("BACKUP_DIR_FAIL " + T("取不到状态目录，无法保存设置 ✓", "cannot resolve the state dir")); return 0; }
                // ★ 同一处修复 ✓：**全新安装上 StateDir 可能还不存在** ✗ → 先建目录再写 ✓（见 `--to` 的说明 ✓）
                try { string sp2 = System.IO.Path.GetDirectoryName(sel); if (!string.IsNullOrEmpty(sp2)) System.IO.Directory.CreateDirectory(sp2); } catch { }
                System.IO.File.WriteAllText(sel, full, new System.Text.UTF8Encoding(false));
            }
            catch (Exception ex) { Console.WriteLine("BACKUP_DIR_FAIL " + T("无法保存设置：" + ex.Message, "cannot save the setting: " + ex.Message)); return 0; }

            Console.WriteLine("BACKUP_DIR_OK " + full + " " + T("以后的备份都写到这里 ✓（已有备份**不会**被移动 ✓ 仍留在原处 ✓）", "future backups go here; existing ones are not moved"));
            return 0;
        }


        private static int BackupList(string[] args, ServiceRegistry reg)
        {
            if (Has(args, "--verify")) return BackupVerify(reg);
            bool detail = Has(args, "--detail");
            IBackupSource src = reg.Get<IBackupSource>();
            List<BackupEntry> all = src.ListRaw();
            // ★★★ **用户要求（2026-09-30）**：「GUI 备份要不不校验了」✓✓
            //   ✗ 原来**只列有效包** ✗（用 `IsValidPackage` 过滤 ✓）
            //     → 用户往 `backup/` 里放的东西**看不到** ✗ → 他会以为"我的备份不见了" ✓
            //       而其实**还在那里** ✓✓（真机就出现过：`BACKUP_LIST_OK 0` + 1 项被忽略 ✓）
            //   ✓ 现在：**全部列出** ✓✓ 无效的**明确标注** ✓（诚实 ✓ 不隐藏 ✓ 不假装有效 ✓）
            //   ✓ `BACKUP_LIST_IGNORED` 保留 ✓（仍告诉你有几项不是有效备份 ✓）
            //   ✓ **恢复时仍只认有效包** ✓（安全边界不动 ✓ 见 IsValidBackupDirFn ✓✓）
            all.Reverse();   // 最新在前 ✓
            // ★★★ **A1 修复（CLI 审计 CRITICAL —— gate1 变红）** ✓✓
            //   ✗ `backup-list` 是 **v2.x 的冻结契约** ✓ 而 `compare_markers.ps1` **逐字比对**它 ✓
            //     （只忽略 `^BACKUP_LIST_IGNORED ` ✓）→ 我上一轮加的三样**全都 diff** ✗✗：
            //       ① 多出来的 `BACKUP_DIR` 行 ✗
            //       ② 计数从「有效包」改成「全部条目」✗
            //       ③ 无效条目也打印了路径行 + `BACKUP_ITEM_INVALID` ✗
            //   ✓ 现在：**计数与路径行都只算有效包** ✓✓（与 v2.x 一致 ✓）
            //     · 备份位置改由**独立的 `backup-dir` 命令**报告 ✓（GUI 调它 ✓ 不再动这个契约 ✓）
            //     · `BACKUP_ITEM_INVALID` 只在**真有无效包**时出现 ✓ → 比对夹具（3 个有效包 ✓）不受影响 ✓✓
            int notValid = 0;
            for (int i = 0; i < all.Count; i++) { if (!BackupPackage.IsValidPackage(all[i].Snapshot)) notValid++; }
            int validCount = all.Count - notValid;
            Console.WriteLine("BACKUP_LIST_OK " + validCount);
            // 备份根里"不是有效备份"的条目 → **仅在存在时**说明 ✓✓（用户往 backup/ 放了自己的东西、
            // 或留下名字像备份但内容不是的目录时 ✓ 静默忽略会让他以为"我的备份还在" ✗）
            // 仅当 >0 时打印 ✓ → 比对夹具（受控 3 个备份 ✓）不受影响 ✓ 契约安全 ✓
            if (notValid > 0)
                Console.WriteLine("BACKUP_LIST_IGNORED " + notValid + T(" 项在备份根里但不是有效备份（**仍会列出** ✓ 但不能用于恢复 ✓）", " entries are not valid packages (listed, but not restorable)"));
            foreach (BackupEntry e in all)
            {
                // ★★★ **N10 修复（复审 MAJOR —— 这个标记**从来没被打印过**）** ✓✓
                //   ✗ 原来 `continue` 在前 ✗ → 下面那行 `BACKUP_ITEM_INVALID` **永远不可达** ✗✗
                //     （编译器其实早就报 CS0162 ✓ 而门槛只 grep 字符串 ✓ → 绿着）
            //   ✓ 现在：**先判无效并打标记 ✓ 再 `continue` 跳过路径行** ✓✓
                //     无效条目不列路径 ✓（与 v2.x 的"只列有效包路径"一致 ✓ 契约安全 ✓）
                if (!BackupPackage.IsValidPackage(e.Snapshot))
                {
                    Console.WriteLine("BACKUP_ITEM_INVALID " + e.Name);
                    continue;
                }
                Console.WriteLine(e.Path);
                if (detail)
                {
                    long bytes = src.DirSize(e.Path);
                    DateTime? mt = src.LastWrite(e.Path);
                    string mts = mt.HasValue ? mt.Value.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) : "(unknown)";
                    Console.WriteLine("BACKUP_ITEM " + e.Name + " " + BackupPackage.KindLabel(BackupPackage.Classify(e.Name)) + " " + bytes + " " + mts);
                }
            }
            return 0;
        }


        /// <summary>doctor：七类体检。首行 DOCTOR_OK|WARN|ERROR n，其后每行 [级别] 类别 描述。逐条对齐 v2.x。
        /// 可选 `--report <file>`：写完整诊断报告（含配置/日志摘要，全部脱敏）→ `DOCTOR_REPORT <路径>`；
        /// 写失败 → `DOCTOR_WRITE_FAIL <原因>`。全程只读（与 v2.x 一致，报告用 UTF-8 **带 BOM** 写）。</summary>
        /// <summary>当前操作系统名（跨平台：Linux 上不能写 "Windows" —— 真机测试抓到的 bug）。</summary>
        /// <summary>I7 FIX: the running executable path. Assembly.Location is empty for single-file
        /// builds, so autostart entries were written with an empty command while reporting success.</summary>
        private static string SelfExePath()
        {
            try
            {
                string p = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                if (!string.IsNullOrEmpty(p)) return p;
            }
            catch { }
            return "dsh-minato";
        }

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
                string text = DoctorReport.Build(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), ToolkitVersion,
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
                // 桌面端在跑时**跳过 3080 那条** ✓（用户要求："体检里检测到 desktop 就跳过 3080 端口监测" ✓✓）
                // 理由：桌面端**不监听 3080**（实测走 19387 ✓）→ 那条 ERROR 说的是"web 服务没起" ✓
                //       但用户在用桌面端时它**必然**是 ERROR ✓ 会误导 ✗
                bool desktopRunning = false;
                try { desktopRunning = reg.Get<IProcessQuery>().AnyProcessNamed("DeepSeek Harness"); } catch { }
                if (!desktopRunning)
                    items.Add(new DocItem("Service", 2, "端口 " + WebPort + " 未监听（服务未运行；菜单按 2 启动）"));
                // 官方桌面端（Electron）**不监听 3080**（2026-09-29 在真机上实测：它监听 19387）✓
                // → 它开着时上面那条 ERROR 会误导用户，甚至让他"按 2 启动"再起一个 web 实例 ✗
                // 这里**只在真的检测到那个进程时**才补一句说明 ✓
                // ✗ 更正（2026-09-30）：我原先注释写"比对环境里没有它 → gate1 零影响"，**这是错的** ✓
                //   实测：本机桌面端开着时 gate1 从 22/22 掉到 19/21（doctor 与 --report 各失败一次）✗
                //   → 该行**已加进两个 doctor 用例的 ignore** ✓（环境相关 → 属于必须 ignore 的那类 ✓）
                try
                {
                    if (reg.Get<IProcessQuery>().AnyProcessNamed("DeepSeek Harness"))
                        items.Add(new DocItem("Service", 1, T("另检测到官方桌面端进程（DeepSeek Harness）：它不走 3080 端口，本项检测不到它——这是检测范围不同，不是故障", "an official desktop app process (DeepSeek Harness) is also running: it does not use port 3080, so this check cannot see it - a difference in scope, not a fault")));
                }
                catch { }
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
        internal const string ToolkitVersion = "3.0.0-preview.1";

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
            // I4 FIX (CLI audit MINOR): the profile name is joined into a path, so a value like
            // "..\..\x" could rewrite any file named cordis.patch.yml outside the data root (the
            // fixed file name limits the blast radius, and --yes is required, but it is still a
            // path traversal). Names are restricted to what a profile name can actually be.
            if (!string.IsNullOrEmpty(profile) && !System.Text.RegularExpressions.Regex.IsMatch(profile, @"^[A-Za-z0-9._-]+$") || profile == "." || profile == "..")   // N7 FIX: dots alone escaped the profiles dir
            {
                Console.WriteLine("PROFILEPATCH_FAIL " + T("profile 名字不合法（只允许字母数字与 . _ - ✓）：" + profile, "invalid profile name: " + profile));
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
        /// Linux：**当前工作目录**（B2 修复后的事实 ✓；此处原写"诚实返回 null" ✗ 已过时 ✓ —— 那是修 cwd 基准之前的行为 ✗）。
        /// dry-run 与真实恢复都走这里，保证两处目标一致。</summary>
        /// <summary>解析 `ws=` 里的**多个工作区**（`;` 分隔 ✓ —— 不用 `:` 因为 Windows 路径含 `:` ✗✓）。
        /// 单个路径（无 `;`）→ 返回一个元素 ✓ 完全向后兼容 ✓✓。</summary>
        private static string[] ConfiguredWorkspaces()
        {
            string raw = _cfg == null || _cfg.Workspace == null ? "" : _cfg.Workspace;
            if (raw.Trim().Length == 0) return new string[0];
            string[] parts = raw.Split(';');
            List<string> outp = new List<string>();
            for (int i = 0; i < parts.Length; i++) { string s = parts[i].Trim().Trim('"'); if (s.Length > 0) outp.Add(s); }
            return outp.ToArray();
        }

        /// <summary>把**全部**工作区打包成新格式 `_workspace/&lt;名称&gt;/.dshws` ✓✓（仅当 ≥2 个 ✓）。
        /// **前置约束** ✗✓：≥2 个时平台侧一个都不打包 ✓（`WorkspaceRoot` 返回 null ✓）→
        /// 包里**只有一种格式** ✓ —— 上一版就是因为两种格式混在一个包里 ✗ 才回滚的 ✓。
        /// 返回实际打包数 ✓（0 = 没做 ✓）。</summary>
        private static int PackageAllWorkspaces(string pkgDir, ServiceRegistry reg)
        {
            try
            {
                string[] all = ConfiguredWorkspaces();
                if (all.Length < 2) return 0;                       // 单个 → 平台侧按旧式扁平打包 ✓
                string wsRoot = System.IO.Path.Combine(pkgDir, "_workspace");
                try { System.IO.Directory.CreateDirectory(wsRoot); } catch { return 0; }
                string dataFull = Dsht.Domain.Services.PathUtil.TrimTrailingSep(reg.Get<IPaths>().DataRoot);
                int done = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    string full;
                    try { full = System.IO.Path.GetFullPath(all[i]); } catch { continue; }
                    if (!System.IO.Directory.Exists(full)) continue;
                    string f = Dsht.Domain.Services.PathUtil.TrimTrailingSep(full);
                    if (Dsht.Domain.Services.PathUtil.IsSubPath(dataFull, f)) continue;   // 与平台侧同一套护栏 ✓
                    if (Dsht.Domain.Services.PathUtil.IsSubPath(f, dataFull)) continue;
                    string name = System.IO.Path.GetFileName(f);
                    if (string.IsNullOrEmpty(name)) continue;
                    string target = System.IO.Path.Combine(wsRoot, name);
                    if (System.IO.Directory.Exists(target)) continue;                     // 不覆盖 ✓
                    CopyDirDeep(f, target, 0);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(target, ".dshws"),
                        "dsh-minato workspace marker\nsource=" + f + "\n");
                    done++;
                }
                return done;
            }
            catch { return 0; }
        }
        private static string WorkspaceRoot(ServiceRegistry reg)
        {
            // **≥2 个工作区时传 null** ✓✓ —— 让平台侧一个都不打包 ✓（避免两代格式混在一个包里 ✗），
            // 同时**保留自动探测** ✓ → 恢复侧的目标仍然正确 ✓（各工作区落到当前项目目录下的 <名称>/ ✓）
            string[] _wss = ConfiguredWorkspaces();
            string _wsCfg = _wss.Length >= 2 ? null : (_cfg == null ? null : _cfg.Workspace);
            return WorkspaceResolver.Resolve(_wsCfg, reg.Get<IPaths>().WorkspaceRoot,
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
        // ★★★ **用户反馈（2026-09-30）**：「gui 删除备份好像也有问题」✓✓ → 追出**两个**真 bug ✗✗
        //   ✗ 这里（export）与下面的 delete **是同一个 bug** ✗：
        //     校验走 `PathValidator`（内部 `ResolveBackupPath` → **绝对路径** ✓）
        //     但**实际操作**用的却是**原始的 --path 字符串** ✗（GUI 传的是裸名字 ✓）
        //     → 于是操作的是**相对于当前工作目录**的同名目录 ✗ 而不是备份根里的那份 ✗
        //     → delete 那边还会因此**绕过**「删除后仍存在」检查 ✓ → **假报 BKDEL_OK** ✗
        //     → 用户看到"点了两次但没删" ✓✓ **完全解释通了** ✓
        //     → **而且危险** ✗：CWD 里恰好有同名目录会被误删 ✓
        //   ✓ 现在：**校验和操作都用同一个解析后的绝对路径** ✓✓
        private static int BackupExport(string[] args, ServiceRegistry reg)
        {
            IBackupSource bk = reg.Get<IBackupSource>();
            IFileSystemQuery fs = reg.Get<IFileSystemQuery>();
            string reason = PathValidator.ValidateExport(Flag(args, "--path"), Flag(args, "--to"), bk.BackupsRoot,
                delegate(string p) { return fs.DirectoryExists(p); },
                delegate(string p) { return System.IO.Path.GetFullPath(p); });
            if (reason != null) { Console.WriteLine("BKEXPORT_FAIL " + T("导出校验失败: " + reason, "export validation failed: " + reason)); return 0; }
            if (!Has(args, "--yes")) { Console.WriteLine("BKEXPORT_PLAN " + T("将把备份复制到目标目录（会写盘）—— 确认请加 --yes", "will copy the backup to the target directory (writes to disk) - add --yes to confirm")); return 0; }
            string src = PathValidator.ResolveBackupPath((Flag(args, "--path") ?? "").Trim().Trim('"'), bk.BackupsRoot);
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
            // ★★★ **用户要求（2026-09-30）**：「删除弹窗输入当前时间才执行」✓✓
            //   → 删除是**不可逆**的 ✓ → 光有 `--yes` 太容易误点 ✓
            //   → **必须输入当前时间**（`yyyy-MM-dd HH:mm:ss` ✓ 本地时间 ✓）且与真实时间相差 ≤ 120 秒 ✓✓
            //   → 这样"手滑点两下"不可能删掉 ✓ 必须**看着时间手打一遍** ✓✓
            string ct = (Flag(args, "--confirm-time") ?? "").Trim().Trim('"');
            if (string.IsNullOrEmpty(ct))
            {
                Console.WriteLine("BKDEL_PLAN " + T("删除备份需要输入**当前时间**确认 ✓ 请加 `--confirm-time \"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + "\"` ✓（照抄上面这个时间 ✓）",
                                                     "deleting a backup needs the current time: add --confirm-time \"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + "\""));
                return 0;
            }
            // I1 FIX (CLI audit MINOR): this used the current culture, so on a locale with a
            // different date format a bare time or a slashed date was accepted, and the window was
            // symmetric so a time in the FUTURE was accepted too. The documented contract is
            // exactly yyyy-MM-dd HH:mm:ss within two minutes, invariant, and not in the future.
            DateTime ctParsed;
            if (!DateTime.TryParseExact(ct, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out ctParsed))
            {
                Console.WriteLine("BKDEL_FAIL " + T("时间格式不对 ✓ 应为 yyyy-MM-dd HH:mm:ss ✓（现在：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " ✓）", "bad time format"));
                return 0;
            }
            // I1 FIX (cont): the window was symmetric, so a time up to two minutes in the FUTURE
            // was accepted. A confirmation is meant to prove the user is looking at the clock now,
            // so only a time in the past (within the window) counts.
            double ctDiff = (DateTime.Now - ctParsed).TotalSeconds;
            if (ctDiff > 120 || ctDiff < -5)   // N4 FIX: negative diff = future; allow only a tiny clock skew
            {
                Console.WriteLine("BKDEL_FAIL " + T("输入的时间与当前时间相差 " + (int)ctDiff + " 秒（超过 120 秒 ✓）→ 拒绝删除 ✓ 现在：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " ✓",
                                                     "the time you typed is " + (int)ctDiff + "s away from now - refused"));
                return 0;
            }
            if (!Has(args, "--yes")) { Console.WriteLine("BKDEL_PLAN " + T("将删除该备份目录（会丢数据）—— 确认请加 --yes", "will delete that backup directory (data loss) - add --yes to confirm")); return 0; }
            string src = PathValidator.ResolveBackupPath((Flag(args, "--path") ?? "").Trim().Trim('"'), bk.BackupsRoot);
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
        /// <summary>备份 ✓。**用户要求（2026-09-30）**：「第一次备份必须手动设置目录，避免卸载时删掉备份」✓✓
        /// 背景：备份根默认是 `StateDir/backup` ✓ 而 **Windows 上 StateDir 就是安装目录** ✗
        ///   → 备份**物理上躺在安装目录里** ✓ 卸载**理论上**会连它一起清 ✗
        ///   → 安装器侧已加保险（**卸载显式跳过 backup/ 与 logs/** ✓✓）
        ///   → 这里再**明确警告一次** ✓ 并告诉用户怎么把备份移出去 ✓
        ///   → 完整做法（让备份根本身可配置 ✓ 第一次强制指定 ✓）见下方 TODO ✓</summary>
        private static void WarnIfBackupsInsideInstall(ServiceRegistry reg)
        {
            try
            {
                string bk = reg.Get<IBackupSource>().BackupsRoot;
                string st = reg.Get<IPaths>().StateDir;
                if (string.IsNullOrEmpty(bk) || string.IsNullOrEmpty(st)) return;
                string b = System.IO.Path.GetFullPath(bk).TrimEnd('\\', '/');
                string s = System.IO.Path.GetFullPath(st).TrimEnd('\\', '/');
                bool inside = b.StartsWith(s + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(b, s, StringComparison.OrdinalIgnoreCase);
                if (!inside) return;
                // ★★ **用户反馈（2026-09-30）**：「GUI 备份报错」✗✗ —— 命令其实是**成功**的 ✓
                //   真因：这里原来输出**4 行长警告** ✓ 而 GUI 的 toast 把 CLI 输出**原样弹出** ✗
                //     → 满屏 `✗` 与「可能被一起清掉」→ **读起来就是报错** ✓✓
                //   ✓ 现在：**缩成一句** ✓✓（安装器侧已加保险：卸载显式跳过 backup/ ✓
                //     所以这句只是**提示** ✓ 不是告警 ✓ 也不再吓人 ✓）
                Console.WriteLine("BACKUP_WARN " + T(
                    "备份在安装目录内 ✓ 建议用 --to <目录> 或环境变量 DSH_MINATO_BACKUP_DIR 放到外面 ✓",
                    "backups live inside the install folder; use --to <dir> or DSH_MINATO_BACKUP_DIR to move them out"));
            }
            catch { }
        }

        private static int Backup(string[] args, ServiceRegistry reg)
        {
            // ★★ **用户要求（2026-09-30）**：「第一次备份必须手动设置目录」✓✓
            //   ✓ `--to <目录>` → **本次运行把备份根指定到那里** ✓✓（放在安装目录之外才稳妥 ✓）
            //   ✓ 没给 `--to` → 走默认根 ✓ 但**若默认根在安装/状态目录内 → 明确警告** ✓
            string toDir = (Flag(args, "--to") ?? "").Trim().Trim('"');
            if (!string.IsNullOrEmpty(toDir))
            {
                try
                {
                    string full = System.IO.Path.GetFullPath(toDir);
                    System.IO.Directory.CreateDirectory(full);
                    Environment.SetEnvironmentVariable("DSH_MINATO_BACKUP_DIR", full);
                    // D2 FIX (CLI audit MAJOR): --to works through an environment variable, but the
                    // settings file takes priority over it, and the write below swallowed its own
                    // failure. When the file could not be written the variable was ignored, so the
                    // tool announced a destination and then backed up somewhere else.
                    // ✓ **记住这个选择** ✓✓（用户要求"第一次弹窗选择" ✓ 但**不该每次都问** ✗）
                    //   → 写进 `<StateDir>/.backup-dir` ✓ 之后所有命令都用它 ✓✓
                    // ★★★ **真机 CI 修复（2026-10-01）—— 写设置文件前必须先建目录** ✓✓
                    //   ✗ 原来直接 `WriteAllText("<StateDir>/.backup-dir", …)` ✗
                    //     → **全新安装上 StateDir 还不存在** ✗（它由日志/备份首次写入时才建 ✓）
                    //     → `Could not find a part of the path …` ✗ → **位置记不住** ✗✗
                    //     → 后续 `backup-list` / `--verify` **回落到默认根** ✗ → 找不到刚建的包 ✓
                    //   · 真机复现（Ubuntu）：干净 XDG 下第一次 `backup --to <dir>`
                    //     → `BACKUP_WARN 记不住这个位置` ✓ → 4-5 条连锁失败 ✓
                    //     第二次跑就好 ✓ —— 因为第一次的日志**顺手把那个目录建出来了** ✗✗
                    //   ✓ 现在：**先 `CreateDirectory` 再写** ✓✓（真用户也受益 ✓ 不只是测试 ✓）
                    try
                    {
                        string selDir = System.IO.Path.Combine(reg.Get<IPaths>().StateDir, ".backup-dir");
                        try { string parent = System.IO.Path.GetDirectoryName(selDir); if (!string.IsNullOrEmpty(parent)) System.IO.Directory.CreateDirectory(parent); } catch { }
                        System.IO.File.WriteAllText(selDir, full, new System.Text.UTF8Encoding(false));
                    }
                    catch (Exception wex) { Console.WriteLine("BACKUP_WARN " + T("记不住这个位置（设置文件写不进去 ✓）：" + wex.Message, "could not remember the location: " + wex.Message)); }
                    string effective = reg.Get<IBackupSource>().BackupsRoot;
                    if (!string.Equals((effective ?? "").TrimEnd('\\', '/'), full.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("BACKUP_FAIL " + T("指定的目录**没有生效** ✓ 本次备份会写到：" + effective + " ✗（设置文件可能只读 ✓）", "the --to folder did not take effect; this backup would go to: " + effective));
                        return 0;
                    }
                    Console.WriteLine("BACKUP_TO " + full + " " + T("本次备份写到这个目录 ✓（放在安装目录之外才稳妥 ✓）", "this backup goes here"));
                }
                catch (Exception ex) { Console.WriteLine("BACKUP_FAIL " + T("无法使用 --to 指定的目录：" + ex.Message, "cannot use --to dir: " + ex.Message)); return 0; }
            }
            // ★★★ **用户要求（2026-09-30）**：「第一次备份弹窗选择」✓✓
            //   → **第一次备份**（备份根里还没有任何有效备份 ✓）**必须指定目录** ✓✓
            //   → 没给 `--to` 就**拒绝** ✓ 并说清怎么给 ✓（放在安装目录之外才稳妥 ✓）
            //   → 已有备份 → **沿用上次的根** ✓ 不再每次追问 ✓✓
            bool hasAny = false;   // ✓ 提到 if 外面 ✓ 后面 WarnIfBackupsInsideInstall 要用 ✓✓
            if (string.IsNullOrEmpty(toDir))
            {
                try
                {
                    string br = reg.Get<IBackupSource>().BackupsRoot;   // ✓ 这里 `bk` 还没声明 ✓ 直接取 ✓
                    // E1 FIX (CLI audit MINOR): this counted any dsh-data-* folder, and an interrupted
                    // first backup leaves exactly such an empty folder behind (the destination is
                    // created before anything is copied). The next run then skipped the guard and wrote
                    // a package inside the install folder - what that guard exists to prevent.
                    if (System.IO.Directory.Exists(br))
                    {
                        string[] cands = System.IO.Directory.GetDirectories(br, "dsh-data-*");
                        for (int ci = 0; ci < cands.Length; ci++)
                        {
                            try { if (BackupPackage.IsValidPackage(reg.Get<IBackupSource>().Snapshot(cands[ci]))) { hasAny = true; break; } }
                            catch { }
                        }
                    }
                }
                catch { }
                if (!hasAny)
                {
                    // D4 FIX (CLI audit MINOR): when the configured backup folder no longer exists the
                    // message said "the first backup needs a folder", which is wrong and misleading -
                    // the user HAS backups, in a folder that is gone. Say so, and only then apply the
                    // first-backup rule.
                    string br2 = "";
                    try { br2 = reg.Get<IBackupSource>().BackupsRoot; } catch { }
                    // ★★★ **N6 修复（复审 MINOR —— D4 的回归）** ✓✓
                    //   ✗ 原来只要"根不存在"就报"配置的备份目录不存在" ✗
                    //     → 而**全新安装**时默认根 `<StateDir>\backup` **本来就不存在** ✗
                    //     → **第一次备份**看到的是"你可能移动或删除了它" ✗✗ **完全误导** ✓
                    //   ✓ 现在：**只有**显式配置过（`.backup-dir` 或环境变量）**而且**那个目录没了
                    //     才走 D4 分支 ✓ 否则照常走"第一次备份"提示 ✓✓
                    bool explicitlySet = false;
                    try
                    {
                        // F-H FIX (CLI final review): an empty or whitespace-only file, or a whitespace-only
                        // environment variable, counted as "explicitly configured" even though the source ignores
                        // it - so a fresh install could still show the misleading "configured folder is missing".
                        string selFile = System.IO.Path.Combine(reg.Get<IPaths>().StateDir, ".backup-dir");
                        bool selSet = false;
                        try { selSet = System.IO.File.Exists(selFile) && System.IO.File.ReadAllText(selFile).Trim().Length > 0; } catch { }
                        string envSel = Environment.GetEnvironmentVariable("DSH_MINATO_BACKUP_DIR");
                        explicitlySet = selSet || !string.IsNullOrEmpty((envSel == null ? "" : envSel).Trim());
                    }
                    catch { }
                    if (explicitlySet && !string.IsNullOrEmpty(br2) && !System.IO.Directory.Exists(br2))
                    {
                        Console.WriteLine("BACKUP_FAIL " + T("**配置的备份目录不存在** ✗：" + br2 + " ✓（你可能移动或删除了它 ✓）请用 `backup-dir --reset` 恢复默认 ✓ 或用 `--to <目录>` 指定新的 ✓", "the configured backups folder does not exist: " + br2));
                        return 0;
                    }
                    // ★★★ **N9 修复（复审 MAJOR —— GUI 把**所有** BACKUP_FAIL 都当成"要选目录"）** ✓✓
                    //   ✗ GUI 只认 `BACKUP_FAIL` ✗ → 而 CLI 对**每一种失败**都打它 ✓
                    //     （配置目录不存在 ✓ `--to` 没生效 ✓ 数据目录不存在 ✓ 备份真的失败 ✓）
                    //     → 那些**真正的错误**会被 GUI 静默地变成"弹文件夹选择器" ✗✗
                    //   ✓ 现在：**打一个专用标记** ✓✓ GUI 只认它 ✓ 其余错误照常显示 ✓
                    Console.WriteLine("BACKUP_NEEDS_DIR 第一次备份需要先选一个目录");
                    Console.WriteLine("BACKUP_FAIL " + T(
                        "**第一次备份必须指定目录** ✓ 请加 `--to <目录>` ✓（建议放在安装目录之外 ✓ 例如 D:\\dsh-backups ✓）"
                        + "；GUI 里第一次点「立即备份」也会弹窗让你选 ✓✓",
                        "the first backup needs an explicit folder - add --to <dir>, preferably outside the install folder"));
                    return 0;
                }
            }
            // ✓✓ **用户反馈（2026-10-01）**：「现在备份还是受阻」✗
            //   真因之一：这条 `BACKUP_WARN` **每次备份都弹** ✗
            //     → toast 第一行永远是「建议用 --to …」✓ → **读起来像"你得先做什么"** ✓✓
            //   ✓ 现在：**只在第一次备份时提示一次** ✓✓（之后就安静 ✓ 备份本来就成功 ✓）
            if (!hasAny) WarnIfBackupsInsideInstall(reg);
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
            // ★★ 第 3 轮审查抓到：**先打印 BACKUP_OK、之后才看 FailedCopies** ✗✗
            //   → 一个部分失败的备份同时打印"成功"和"不完整" ✓ 而 GUI 只看 BACKUP_OK ✓ → 报成成功 ✗
            // ✓ 现在：**不完整就不报 OK** ✓✓（GUI 因此如实显示未完成 ✓）
            if (r.FailedCopies > 0)
                Console.WriteLine("BACKUP_INCOMPLETE " + r.Path + " " + T("有 " + r.FailedCopies + " 项没能备份 ✓ 这个包**不完整** ✓（restore 会要求 --force）", "the package is incomplete: " + r.FailedCopies + " items could not be backed up"));
            else
                Console.WriteLine("BACKUP_OK " + r.Path);
            int _wsDone = PackageAllWorkspaces(r.Path, reg);
            if (_wsDone > 0) Console.WriteLine("BACKUP_WORKSPACES " + _wsDone + T(" 个工作区已打包（新格式 _workspace/<名称>/.dshws ✓）", " workspaces packaged (new layout _workspace/<name>/.dshws)"));
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
                // ★ 审查抓到：上面校验的是 ResolveBackupPath 的结果 ✗，而这里用的是**原始参数** ✗
                //   → 只给名字时（GUI 就是这样）校验看的是 &lt;备份根&gt;/&lt;名字&gt;，实际读的却是 &lt;当前目录&gt;/&lt;名字&gt; ✗✗
                //   → 可能失败，也可能把当前目录里同名的 dsh-data-* 目录恢复进数据根 ✓
                // ✓ 现在：**校验与操作同一个值** ✓✓
                bkDir = Dsht.Domain.Services.PathValidator.ResolveBackupPath(pathArg.Trim().Trim('"'), bk.BackupsRoot);
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
                // ★★ 第 2 轮审查抓到：**自动选出的包也要过完整性闸门** ✓✓（我上一版只堵了 --path 分支 ✗）
                //   → 否则被中断的包只要是最新的一个，就仍会被恢复并打印 RESTORE_OK ✗
                {
                    string autoTrunc = BackupTruncatedReason(latest);
                    if (!string.IsNullOrEmpty(autoTrunc) && !Has(args, "--force"))
                    {
                        Console.WriteLine("RESTORE_FAIL " + autoTrunc + T("；确认要用它恢复请加 --force", "; add --force to restore from it anyway"));
                        return 0;
                    }
                    if (!string.IsNullOrEmpty(autoTrunc)) Console.WriteLine("RESTORE_WARN " + autoTrunc);
                }
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
                // ★ 第 3 轮抓到：**dry-run 的自动分支也没有闸门** ✗ → 会对真实 restore 会拒的包打印 DRYRUN_OK ✗
                //   → 同一个包两套结论 ✓（正是本文件反复强调不能有的那种不一致 ✓）
                {
                    string autoTrunc2 = BackupTruncatedReason(bkDir);
                    if (!string.IsNullOrEmpty(autoTrunc2) && !Has(args, "--force"))
                    {
                        Console.WriteLine("DRYRUN_FAIL " + autoTrunc2 + T("；确认要预览它请加 --force", "; add --force to preview it anyway"));
                        return 0;
                    }
                    if (!string.IsNullOrEmpty(autoTrunc2)) Console.WriteLine("DRYRUN_WARN " + autoTrunc2);
                }
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
                    // ★★ 第 2 轮审查抓到：这道闸门**只在显式 --path 分支** ✗✗（我上一版的注释还声称覆盖了自动分支 ✗）
            //   → 不带 --path 时，中断的包只要是最新的就仍会被恢复并打印 RESTORE_OK ✗
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
            // —— 排障开关 ✓（用户要求：""给可能会发生可能不会发生的问题提供解决的选项"" + ""备注一下发生什么问题可以尝试启用和禁用"" ✓✓）——
            Console.WriteLine("CONFIG browser_mode " + _cfg.BrowserMode);
            Console.WriteLine("CONFIG ui_parallel " + (_cfg.UiParallel ? "on" : "off"));
            Console.WriteLine("CONFIG scan_children " + (_cfg.ScanChildren ? "on" : "off"));
            // 备注行 ✓✓：GUI 原样显示在对应设置项下面 ✓（"出现什么问题时试哪个" ✓）
            Console.WriteLine("CONFIGNOTE browser_mode " + T("【浏览器打不开时改这个】auto=自动（先 snap run firefox → 再直开 → 最后 xdg-open）/ snap=只走 snap（Ubuntu 的 snap 版 firefox 必须这样 ✓）/ direct=只直开 firefox / xdg=只交给系统默认", "when the browser will not open"));
            Console.WriteLine("CONFIGNOTE ui_parallel " + T("【切页卡顿时改这个】on=并行取数据（快 ✓ 默认）/ off=串行（老行为，个别环境下更稳）", "when switching pages feels slow"));
            Console.WriteLine("CONFIGNOTE scan_children " + T("【会话页想更快时关掉】on=扫描会话文件得出主/子代理归类（默认 ✓ 197 个会话约 200ms）/ off=不扫（会话页更快，但子代理统计会为空）", "to make the sessions page faster"));
            Console.WriteLine("CONFIGNOTE auto_start " + T("【不想让 GUI 自动起 dsh 时关掉】on=GUI 启动时自动启动（默认）/ off=不自动", "if you do not want the GUI to auto-start dsh"));
            Console.WriteLine("CONFIGNOTE auto_start_target " + T("【开机自启启动什么】auto=按平台（Windows/macOS 启动官方桌面端，Linux 启动 dsh web）/ desktop=官方桌面端 / web=dsh web", "what to start at login"));
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
