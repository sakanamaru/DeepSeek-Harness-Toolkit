using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

partial class Program
{

    /// <summary>npm 全局安装/更新 dsh 指定版本（version 空=最新）；多源依次尝试，返回 0=成功，-1=全部失败。
    /// 版本号会拼进 cmd 命令行（.NET 4.x ProcessStartInfo 无 ArgumentList，只能字符串拼接），
    /// 故入口做双层防线：① 版本整串白名单复核（IsValidNpmVersion，防注入）② 包名整体加引号、注册表仅取代码内常量。
    /// registry 参数绝不出自用户输入/网络数据（仅 NPM_OFFICIAL / NPM_MIRROR 常量），避免任何注入面。</summary>
    static int NpmInstallDsh(string version, string[] registries)
    {
        if (!string.IsNullOrEmpty(version) && !IsValidNpmVersion(version))
        {
            Error(T("版本号不合法，已拒绝安装：" + version, "Invalid version string; install refused: " + version));
            return -1;
        }
        string pkg = "\"@deepseek-ai/dsh" + (string.IsNullOrEmpty(version) ? "" : "@" + version) + "\"";
        for (int i = 0; i < registries.Length; i++)
        {
            Info(string.Format(T("第 {0}/{1} 次尝试，源：{2}", "Attempt {0}/{1}, registry: {2}"), i + 1, registries.Length, registries[i]));
            int code = RunVisible("cmd.exe", "/c npm install -g --registry=" + registries[i] + " " + pkg);
            if (code == 0) return 0;
            if (code == -2)
                Warn(T("安装/更新超时（10 分钟），已终止。请检查网络或稍后重试。", "Timed out after 10 minutes; terminated. Check the network and retry."));
            else
                Warn(string.Format(T("失败（退出码 {0}）", "Failed (exit code {0})"), code));
        }
        return -1;
    }


    static string CurrentVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v.Major + "." + v.Minor + "." + v.Build;
    }


    /// <summary>查询最新版：无更新或网络失败返回 null（静默），有更新返回新版本号。</summary>
    static string LatestVersion()
    {
        string body = HttpGet("https://api.github.com/repos/sakanamaru/DeepSeek-Harness-Toolkit/releases/latest", 4000);
        if (body == null) return null;
        string tag = ParseLatestTag(body);
        if (tag == null) return null;
        tag = SanitizeLatestVersion(tag);   // L-8：tag 展示/比较前过严格白名单，拒绝控制字符污染控制台
        if (tag == null) return null;
        return CompareVersions(CurrentVersion(), tag) < 0 ? tag : null;
    }


    /// <summary>校验并清洗 npm view 返回的最新版本：整串必须通过严格白名单（数字核心段 + 可选预发布段），
    /// 防止恶意 registry 返回带命令字符（空格/&/;/|/&gt;/&lt; 等）的版本串导致注入；非法/垃圾返回 null。</summary>
    static string SanitizeLatestVersion(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string v = raw.Trim();
        if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase)) v = v.Substring(1);
        return IsValidNpmVersion(v) ? v : null;
    }


    /// <summary>查询 npm 上 @deepseek-ai/dsh 的最新版本；失败/离线/版本非法返回 null。</summary>
    static string GetLatestDshVersion()
    {
        return SanitizeLatestVersion(Platform.Shell.NpmViewLatest());
    }


    /// <summary>解析 npm 版本输出为字符串数组（容错单行数组 / JSON 多行格式）。</summary>
    static string[] ParseNpmVersions(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new string[0];
        string s = raw.Trim();
        if (s.StartsWith("[")) s = s.Substring(1);
        if (s.EndsWith("]")) s = s.Substring(0, s.Length - 1);
        string[] parts = s.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        var list = new List<string>();
        foreach (string p in parts)
        {
            string v = p.Trim().Trim('\'', '"');
            if (v.Length > 0) list.Add(v);
        }
        return list.ToArray();
    }


    /// <summary>过滤干净的版本串（严格白名单：数字核心段 + 可选预发布段，防注入），升序后取最近 n 个并倒序（最新在前）。</summary>
    static string[] FilterVersions(string[] all, int n)
    {
        var clean = new List<string>();
        foreach (string v in all)
        {
            if (!IsValidNpmVersion(v)) continue;   // 严格整串校验（预发布段仅字母数字与 .-，拒绝任何命令字符）
            clean.Add(v);
        }
        clean.Sort(CompareVersions);
        var recent = new List<string>();
        for (int i = Math.Max(0, clean.Count - n); i < clean.Count; i++) recent.Add(clean[i]);
        recent.Reverse();
        return recent.ToArray();
    }


    /// <summary>严格 npm 版本白名单：核心段数字 . 分隔（1-3 段），允许一个 - 预发布段，其字符仅限 [0-9A-Za-z.-]。
    /// 任何空格/&amp; /; /| /&gt; /&lt; /$ /引号等命令注入字符一律拒绝；调用方可放心的用它拼进命令行。</summary>
    static bool IsValidNpmVersion(string v)
    {
        if (string.IsNullOrWhiteSpace(v)) return false;
        if (!IsCleanVersion(CoreVersion(v))) return false;
        int d = v.IndexOf('-');
        if (d < 0) return true;                       // 无预发布段：核心段已是白名单（IsCleanVersion 只含数字与点）
        if (v.IndexOf('-', d + 1) >= 0) return false; // 只允许一个 -
        string pre = v.Substring(d + 1);
        if (pre.Length == 0) return false;
        foreach (char c in pre)
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '.' || c == '-'))
                return false;
        return true;
    }


    /// <summary>更新/换版本 dsh（菜单 8）。守卫 → 检测 → 选版本 → 双确认 → pre-update 备份 → 安装 → 记忆。</summary>
    static void UpdateDsh()
    {
        if (!IntegrityGate("更新 dsh", "update")) return;
        Banner();
        if (ProbeService() != ServiceState.Down)   // 守卫与备份/恢复/导入一致：启动中（Listening）也拒绝，避免半启动状态文件占用竞态
        {
            Error(T("dsh Web 服务正在运行，请先停止再更新（菜单 2 启动界面中可停止）。", "dsh web is running. Stop it first (from the Start UI screen)."));
            Pause(); return;
        }
        string cur = Platform.Shell.DshVersion();
        if (string.IsNullOrWhiteSpace(cur))
        {
            Warn(T("未检测到已安装的 dsh。请先通过菜单 1 安装。", "No installed dsh detected. Install via menu 1 first."));
            Pause(); return;
        }
        string latest = GetLatestDshVersion();
        if (latest != null)
        {
            if (CompareVersions(cur, latest) >= 0)
                Info(T("当前已是最新版本 v" + cur + "。输入 L 可重装或切换历史版本。", "Already up to date (v" + cur + "). Enter L to reinstall or switch versions."));
            else
                Info(T("发现新版本：当前 v" + cur + " → 最新 v" + latest, "Update available: v" + cur + " -> v" + latest));
        }
        else
        {
            Warn(T("无法获取最新版本（离线或源不可用）。输入 L 可尝试历史版本列表。", "Cannot reach the registry. Enter L to list versions."));
        }
        Console.WriteLine();
        CL(ConsoleColor.White, T("  回车=安装最新版，L=列出历史版本，其他=取消：", "  Enter=install latest, L=list versions, anything else=cancel:"));
        Console.Write("  > ");
        string sel = ReadLineTrim().Trim();
        if (inputEof) { Info(T("已取消。", "Cancelled.")); Pause(); return; }
        string target = null;
        if (sel == "L" || sel == "l")
        {
            target = ListDshVersions();
            if (target == null) { Info(T("已取消。", "Cancelled.")); Pause(); return; }
        }
        else if (sel.Length == 0)
        {
            if (latest == null) { Error(T("无法确定目标版本，请改用 L 手动选择。", "Cannot determine target version; use L to pick one.")); Pause(); return; }
            target = latest;
        }
        else
        {
            Info(T("已取消。", "Cancelled.")); Pause(); return;
        }
        // 双确认：① y/Y ② 输入 update
        Console.WriteLine();
        CL(ConsoleColor.Yellow, T("  ⚠️ 警告：更新可能存在破坏性变更（覆盖当前 dsh 安装；配置/插件可能不兼容）。", "  ⚠️ Warning: this update may be destructive (overwrites the dsh install; config/plugins may be incompatible)."));
        Info(T("  当前 v" + cur + " → 目标 v" + target + "。确认后将先自动备份 ~/.dsh 数据，再执行安装。", "  v" + cur + " -> v" + target + ". Your ~/.dsh data will be backed up first, then install runs."));
        CL(ConsoleColor.White, T("  是否继续？(y/N)：", "  Continue? (y/N): "));
        string a1 = ReadLineTrim().Trim();
        if (a1 != "y" && a1 != "Y") { Info(T("已取消，未做任何更改。", "Cancelled; nothing changed.")); Pause(); return; }
        CL(ConsoleColor.White, T("  请再次确认：输入 update 执行更新（其他键取消）：", "  Confirm again: type update to proceed (anything else cancels): "));
        string a2 = ReadLineTrim().Trim();
        if (a2 != "update") { Info(T("已取消，未做任何更改。", "Cancelled; nothing changed.")); Pause(); return; }
        // pre-update 备份（失败即中止，沿用 v2.1 安全逻辑；数据目录尚未生成时跳过备份——与 wipe 的 L606 判断同构）
        string preBk = null;
        if (Directory.Exists(DataRoot()))
        {
            preBk = DoBackup(DataRoot(), null, BackupKind.PreUpdate);
            if (preBk == null)
            {
                Error(T("更新前自动备份失败，已中止更新（请先手动备份或检查磁盘空间）。", "Pre-update backup failed; update aborted (back up manually or check disk space first)."));
                Pause(); return;
            }
            try { File.WriteAllText(Path.Combine(preBk, "version.txt"), cur, new UTF8Encoding(false)); } catch { }   // 记录旧版本号供回滚
            Info(T("已自动备份：" + preBk, "Auto backup: " + preBk));
        }
        else Info(T("无数据目录，跳过更新前备份。", "No data directory; skipping pre-update backup."));
        // 执行安装
        string[] regs = new string[] { NPM_OFFICIAL, NPM_MIRROR };
        int code = NpmInstallDsh(target, regs);
        if (code == 0)
        {
            string nv = Platform.Shell.DshVersion();
            // M-2：安装退出码 0 不算完，必须验证实际版本 == 目标版本（nv 为空或不等都判失败并走回滚）
            string nvClean = SanitizeLatestVersion(nv);   // 去掉 v 前缀/脏字符，非法返回 null
            bool ok = !string.IsNullOrWhiteSpace(nvClean) && CompareVersions(nvClean, target) == 0;
            if (ok)
            {
                Success(T("更新成功！已安装 v" + nvClean, "Updated! Now on v" + nvClean));
                RecordDshVersion(nvClean);
                Info(T("数据已备份于 " + (preBk ?? T("（本次无数据目录，未备份）", "(no data dir this run, not backed up)")), "Data backed up at " + (preBk ?? "(no data dir this run, not backed up)")));
            }
            else
            {
                string reason = string.IsNullOrWhiteSpace(nv)
                    ? T("安装返回成功但无法读取新版本号", "install returned success but the new version could not be read")
                    : T("安装返回成功但版本不符（期望 v" + target + "，实际 " + (nvClean ?? nv) + "）", "install returned success but version mismatch (expected v" + target + ", got " + (nvClean ?? nv) + ")");
                Error(T("更新验证失败：" + reason, "Update verification failed: " + reason));
                RollbackUpdate(cur, preBk);
            }
        }
        else
        {
            Error(T("更新失败（退出码 " + code + "）。", "Update failed (exit code " + code + ")."));
            RollbackUpdate(cur, preBk);
        }
        Pause();
    }


    /// <summary>update-info：Update Center 只读数据源（网络失败降级 unknown，不阻断）。输出 UPDATEINFO_* 机器行。</summary>
    static void UpdateInfo()
    {
        Console.WriteLine("UPDATEINFO_OK");
        string cur = Platform.Shell.DshVersion();
        Console.WriteLine("UPDATEINFO_CURRENT " + (string.IsNullOrWhiteSpace(cur) ? "none" : cur.Trim().Replace("\r", " ").Replace("\n", " ")));
        string stable = GetLatestDshVersion();
        Console.WriteLine("UPDATEINFO_LATEST_STABLE " + (stable ?? "unknown"));
        string rc = GetLatestRcVersion();
        Console.WriteLine("UPDATEINFO_LATEST_RC " + (rc ?? "none"));
        Console.WriteLine("UPDATEINFO_CHANNEL " + cfgChannel);
        string pre = LatestBackupWithSuffix("-pre-update");
        Console.WriteLine("UPDATEINFO_PREBACKUP " + (pre == null ? "none" : Path.GetFileName(pre)));
        Console.WriteLine("UPDATEINFO_ROLLBACK " + CountValidBackups());
        Console.WriteLine("UPDATEINFO_NOTES_URL https://github.com/deepseek-ai/dsh/releases");
    }

    // ---------------- 自身完整性闸门（v2.5 安全批：高风险操作保护） ----------------


    /// <summary>GET 指定 URL，成功返回正文，失败/超时返回 null。GitHub API 要求 User-Agent。</summary>
    static string HttpGet(string url, int ms)
    {
        if (HttpGetImpl != null) return HttpGetImpl(url, ms);
        try
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = ms;
            req.UserAgent = "DeepSeek-Harness-Toolkit";
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }
        catch { return null; }
    }




    /// <summary>从 GitHub Releases API JSON 提取 tag_name（"v2.1.0" → "2.1.0"），失败返回 null。（单测可直接调用）</summary>
    static string ParseLatestTag(string body)
    {
        try
        {
            int i = body.IndexOf("\"tag_name\"", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;
            int q1 = body.IndexOf('"', i + 10);
            int q2 = body.IndexOf('"', q1 + 1);
            if (q1 < 0 || q2 < 0) return null;
            return body.Substring(q1 + 1, q2 - q1 - 1).TrimStart('v', 'V');
        }
        catch { return null; }
    }




    /// <summary>取版本核心段（去掉 -rc/-beta 等 pre-release 后缀）："0.1.1-rc.2" → "0.1.1"。</summary>
    static string CoreVersion(string v)
    {
        if (string.IsNullOrEmpty(v)) return v ?? "";
        int d = v.IndexOf('-');
        return d >= 0 ? v.Substring(0, d) : v;
    }




    /// <summary>版本号比较（semver 语义）：核心段数字比较；核心段相同再比较 pre-release 后缀——
    /// ① 正式版（无后缀）高于 rc/beta（2.1.2 > 2.1.2-rc）；② 同带后缀按 `.` 分段逐段比较，数字段按数值序、
    /// 字母段按字典序（rc.1 &lt; rc.2 &lt; rc.10），缺段视为更低。
    /// "a 低于 b" 返回负数，"相等" 0，"a 高于 b" 正数。</summary>
    static int CompareVersions(string a, string b)
    {
        string[] pa = CoreVersion(a).Split('.');
        string[] pb = CoreVersion(b).Split('.');
        for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            int x = 0, y = 0;
            int.TryParse(i < pa.Length ? pa[i] : "0", out x);
            int.TryParse(i < pb.Length ? pb[i] : "0", out y);
            if (x != y) return x < y ? -1 : 1;
        }
        return ComparePreRelease(a, b);
    }




    /// <summary>比较 pre-release 后缀（仅当核心段已相等时调用）：无后缀（正式版）> 有后缀；
    /// 同带后缀按 `.` 分段逐段比（数字段数值序、字母/混合段字典序，ASCII 序下数字段天然低于字母段），缺段更低。</summary>
    static int ComparePreRelease(string a, string b)
    {
        int da = a.IndexOf('-');
        int db = b.IndexOf('-');
        string pa = da >= 0 ? a.Substring(da + 1) : "";
        string pb = db >= 0 ? b.Substring(db + 1) : "";
        if (pa.Length == 0 && pb.Length == 0) return 0;
        if (pa.Length == 0) return 1;    // 正式版（无后缀）高于预发布
        if (pb.Length == 0) return -1;
        string[] sa = pa.Split('.');
        string[] sb = pb.Split('.');
        for (int i = 0; i < Math.Max(sa.Length, sb.Length); i++)
        {
            string x = i < sa.Length ? sa[i] : null;
            string y = i < sb.Length ? sb[i] : null;
            if (x == null && y == null) return 0;
            if (x == null) return -1;    // 较短后缀更低：rc < rc.1
            if (y == null) return 1;
            int nx, ny;
            bool xn = int.TryParse(x, out nx);
            bool yn = int.TryParse(y, out ny);
            if (xn && yn)
            {
                if (nx != ny) return nx < ny ? -1 : 1;
            }
            else
            {
                int c = string.CompareOrdinal(x, y);
                if (c != 0) return c < 0 ? -1 : 1;
            }
        }
        return 0;
    }




    /// <summary>是否为 数字[.数字[.数字]] 的干净版本串。</summary>
    static bool IsCleanVersion(string v)
    {
        if (v.Length == 0) return false;
        string[] seg = v.Split('.');
        if (seg.Length < 1 || seg.Length > 3) return false;
        foreach (string s in seg)
        {
            if (s.Length == 0) return false;
            for (int i = 0; i < s.Length; i++)
                if (s[i] < '0' || s[i] > '9') return false;
        }
        return true;
    }




    /// <summary>显示历史版本列表（0/回车=取消），返回用户选中的版本；取消返回 null。本机装过的版本带 * 标记。</summary>
    static string ListDshVersions()
    {
        string raw = Platform.Shell.NpmViewVersions();
        string[] recent = FilterVersions(ParseNpmVersions(raw), 10);
        if (recent.Length == 0)
        {
            Warn(T("无法获取版本列表（离线或源不可用）。", "Cannot get version list (offline or registry unavailable)."));
            return null;
        }
        Console.WriteLine();
        CL(ConsoleColor.White, T("  可选版本（* = 本机安装过）：", "  Available versions (* = installed before):"));
        for (int i = 0; i < recent.Length; i++)
            CL(ConsoleColor.White, "  " + (i + 1) + ") v" + recent[i] + (HasDshVersion(recent[i]) ? " *" : ""));
        CL(ConsoleColor.Gray, "  0) " + T("取消", "Cancel"));
        Console.Write("  > ");
        string sel = ReadLineTrim().Trim();
        int idx;
        if (int.TryParse(sel, out idx) && idx >= 1 && idx <= recent.Length) return recent[idx - 1];
        return null;
    }




    /// <summary>记录本机装过的 dsh 版本（去重、最新在前、最多 10 个）。</summary>
    static void RecordDshVersion(string ver)
    {
        ver = ver.Trim().TrimStart('v', 'V');
        // L-8：过滤逗号/控制符——逗号会污染历史列表的逗号分隔解析，控制符会污染 config 与后续展示
        ver = ver.Replace(",", "");
        var sb = new StringBuilder();
        foreach (char c in ver)
            if (c >= ' ' && c != '\x7f') sb.Append(c);   // 仅保留可打印非控制字符
        ver = sb.ToString();
        if (ver.Length == 0) return;
        var list = new List<string>();
        if (cfgDshVersions.Length > 0)
            list.AddRange(cfgDshVersions.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        list.RemoveAll(x => x.Trim().Equals(ver, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, ver);
        while (list.Count > 10) list.RemoveAt(list.Count - 1);
        cfgDshVersions = string.Join(",", list.ToArray());
        SaveConfig();
    }




    /// <summary>历史列表中是否含指定版本。</summary>
    static bool HasDshVersion(string ver)
    {
        if (cfgDshVersions.Length == 0) return false;
        foreach (string x in cfgDshVersions.Split(','))
            if (x.Trim().Equals(ver, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }




    /// <summary>更新失败后自动回滚到旧版本 cur 并验证；回滚失败给出明确手动指引 + 备份位置（M-1）。</summary>
    static void RollbackUpdate(string cur, string preBk)
    {
        Info(T("正在自动回滚到 v" + cur + " ...", "Auto-rolling back to v" + cur + " ..."));
        string[] regs = new string[] { NPM_OFFICIAL, NPM_MIRROR };
        int rc = NpmInstallDsh(cur, regs);
        string rv = rc == 0 ? Platform.Shell.DshVersion() : null;
        string rvClean = SanitizeLatestVersion(rv);
        bool rolledBack = rc == 0 && !string.IsNullOrWhiteSpace(rvClean) && CompareVersions(rvClean, cur) == 0;
        if (rolledBack)
        {
            Success(T("已回滚到 v" + cur, "Rolled back to v" + cur));
        }
        else
        {
            Error(T("自动回滚失败。请手动执行：npm install -g @deepseek-ai/dsh@" + cur,
                    "Auto-rollback failed. Manually run: npm install -g @deepseek-ai/dsh@" + cur));
        }
        if (!string.IsNullOrWhiteSpace(preBk))
            Info(T("数据已备份于：" + preBk, "Data backed up at: " + preBk));
    }




    /// <summary>npm versions 列表里最后一个 -rc 版本号（发布序）；无/离线返回 null。</summary>
    static string GetLatestRcVersion()
    {
        string raw = Platform.Shell.NpmViewVersions();
        string[] all = ParseNpmVersions(raw);
        string last = null;
        foreach (string v in all) if (v.IndexOf("-rc", StringComparison.OrdinalIgnoreCase) >= 0) last = v;
        return last;
    }



}
