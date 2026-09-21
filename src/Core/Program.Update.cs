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
        return SanitizeLatestVersion(RunCapture("cmd.exe", "/c npm view @deepseek-ai/dsh version 2>nul"));
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
        string cur = RunDshVersion();
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
            string nv = RunDshVersion();
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
        string cur = RunDshVersion();
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

}
