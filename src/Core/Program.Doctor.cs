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

    /// <summary>诊断条目：类别 Cat + 级别 Level（0=OK 1=WARN 2=ERROR）+ 描述。</summary>
    public class DocItem
    {
        public string Cat;
        public int Level;
        public string Text;
        public DocItem(string cat, int level, string text) { Cat = cat; Level = level; Text = text; }
    }


    /// <summary>脱敏：报告/日志出口统一消毒（API Key / Token / Cookie / Password 等不可进入报告）。
    /// 规则：URL 查询参数 token=/key=/auth=/session=…；独立密钥键的 key:value / key=value；40+ 位十六进制串。</summary>
    static string SanitizeForReport(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        try
        {
            s = Regex.Replace(s, @"([?&](?:token|key|api[_-]?key|auth|session|password|passwd|secret|cookie)[=])([^&#\s]+)", "$1[REDACTED]", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"(?i)\b(api[_-]?key|password|passwd|secret|cookie|token|session)\b\s*[=:]\s*[^,\s;]+", "$1=[REDACTED]");
            s = Regex.Replace(s, @"\b[0-9a-fA-F]{40,}\b", "[REDACTED]");
        }
        catch { }
        return s;
    }


    /// <summary>汇总串：DOCTOR_OK 0 / DOCTOR_WARN n / DOCTOR_ERROR n（ERROR 优先）。纯函数，可单测。</summary>
    static string DoctorSummary(List<DocItem> items)
    {
        int err = 0, warn = 0;
        foreach (DocItem it in items)
        {
            if (it.Level == 2) err++;
            else if (it.Level == 1) warn++;
        }
        if (err > 0) return "DOCTOR_ERROR " + err;
        if (warn > 0) return "DOCTOR_WARN " + warn;
        return "DOCTOR_OK 0";
    }


    static string DocLevel(int level) { return level == 2 ? "ERROR" : (level == 1 ? "WARN" : "OK"); }


    /// <summary>体检主流程：System / Harness / Service / Workspace / Backup / Network 六类检查。
    /// 输出：首行 DOCTOR_OK|WARN|ERROR n；其后每行 [OK|WARN|ERROR] &lt;类别&gt; &lt;描述&gt;（GUI 按级别着色解析）。
    /// 可选 --report &lt;file&gt;：写完整诊断报告（含配置/日志摘要，全部脱敏）。全程只读。</summary>
    static void Doctor(string[] args)
    {
        var items = new List<DocItem>();
        DoctorCollect(items);
        string summary = DoctorSummary(items);
        Console.WriteLine(summary);
        foreach (DocItem it in items)
            Console.WriteLine("[" + DocLevel(it.Level) + "] " + it.Cat + " " + it.Text);
        string report = null;
        for (int i = 1; i < args.Length - 1; i++)
            if (args[i] == "--report" || args[i] == "-report") report = args[i + 1];
        if (report != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("== DeepSeek Harness Toolkit 诊断报告 ==");
            sb.AppendLine("生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Toolkit : " + CurrentVersion());
            sb.AppendLine("系统    : " + Environment.OSVersion.VersionString);
            sb.AppendLine();
            string lastCat = "";
            foreach (DocItem it in items)
            {
                if (it.Cat != lastCat) { sb.AppendLine("-- " + it.Cat + " --"); lastCat = it.Cat; }
                sb.AppendLine("  [" + DocLevel(it.Level) + "] " + SanitizeForReport(it.Text));
            }
            sb.AppendLine();
            sb.AppendLine("-- 配置摘要（脱敏） --");
            sb.AppendLine("  " + SanitizeForReport(ConfigSummary()));
            sb.AppendLine("-- 日志摘要（脱敏） --");
            sb.AppendLine("  " + SanitizeForReport(LogSummary()));
            sb.AppendLine();
            sb.AppendLine("结果: " + summary);
            try { File.WriteAllText(report, sb.ToString(), new UTF8Encoding(true)); Console.WriteLine("DOCTOR_REPORT " + report); }
            catch (Exception ex) { Console.WriteLine("DOCTOR_WRITE_FAIL " + ex.Message); }
        }
    }


    /// <summary>收集七类检查项（System/Harness/Service/Workspace/Backup/Network/Integrity；独立于输出，便于复用/单测）。全程只读。</summary>
    static void DoctorCollect(List<DocItem> items)
    {
        // ---- System ----
        items.Add(new DocItem("System", 0, "Windows: " + Environment.OSVersion.VersionString + " (" + (Environment.Is64BitOperatingSystem ? "x64" : "x86") + ")"));
        string node = RunCapture("node.exe", "--version");
        if (string.IsNullOrWhiteSpace(node)) items.Add(new DocItem("System", 1, "Node.js 未找到（dsh 依赖 npm 安装）"));
        else items.Add(new DocItem("System", 0, "Node.js: " + node.Trim()));
        string npm = RunCapture("cmd.exe", "/c npm --version 2>nul");
        items.Add(new DocItem("System", string.IsNullOrWhiteSpace(npm) ? 1 : 0, string.IsNullOrWhiteSpace(npm) ? "npm 不可用" : "npm: " + npm.Trim()));

        // ---- Harness ----
        string dsh = LocateDsh();
        if (dsh == null) items.Add(new DocItem("Harness", 2, "dsh 未安装（交互菜单按 1 安装）"));
        else
        {
            items.Add(new DocItem("Harness", 0, "dsh 已安装: " + SanitizeForReport(dsh)));
            string dv = RunDshVersion();
            if (string.IsNullOrWhiteSpace(dv)) items.Add(new DocItem("Harness", 1, "dsh --version 无输出"));
            else items.Add(new DocItem("Harness", 0, "dsh 版本: " + SanitizeForReport(dv.Trim().Replace("\r", " ").Replace("\n", " "))));
        }

        // ---- Service ----
        bool portOpen = IsPortOpen(WEB_PORT, 800);
        if (!portOpen)
        {
            items.Add(new DocItem("Service", 2, "端口 " + WEB_PORT + " 未监听（服务未运行；菜单按 2 启动）"));
        }
        else
        {
            int pid = FindPortPid(WEB_PORT);
            items.Add(new DocItem("Service", 0, "端口 " + WEB_PORT + " 监听中" + (pid > 0 ? "（PID " + pid + "）" : "")));
            bool isDsh = pid > 0 && ListenerIsDsh();
            string who = isDsh ? "监听进程确为 dsh" : (pid > 0 ? "监听进程不是 dsh！命令行: " + SanitizeForReport(GetProcessCommandLine(pid)) : "无法确认监听进程身份");
            items.Add(new DocItem("Service", isDsh ? 0 : 2, who));
            bool http = HttpResponds(WebUrl(), 800);
            items.Add(new DocItem("Service", 0, "HTTP: " + (http ? "有应答（dsh 未授权统一 401 属正常门控）" : "无应答")));
            ServiceState st = ProbeService();
            items.Add(new DocItem("Service", st == ServiceState.Ready ? 0 : 1, "服务状态: " + (st == ServiceState.Ready ? "运行中" : (st == ServiceState.Listening ? "启动中" : "已停止"))));
        }

        // ---- Workspace ----
        string data = DataRoot();
        if (string.IsNullOrEmpty(data) || !Directory.Exists(data))
        {
            items.Add(new DocItem("Workspace", 2, "数据目录不存在: " + data + "（dsh 尚未初始化）"));
        }
        else
        {
            bool enumerable = false;
            try { Directory.GetFiles(data); enumerable = true; } catch { }
            items.Add(new DocItem("Workspace", enumerable ? 0 : 2, "数据目录: " + SanitizeForReport(data) + (enumerable ? "" : "（无读取权限）")));
            long size = DirSize(data);
            items.Add(new DocItem("Workspace", size > 1024L * 1024 * 1024 ? 1 : 0, "数据大小: " + HumanSize(size) + (size > 1024L * 1024 * 1024 ? "（较大，备份耗时会增加）" : "")));
        }

        // ---- Backup ----
        string bkRoot = BackupsRoot();
        if (!Directory.Exists(bkRoot))
        {
            items.Add(new DocItem("Backup", 1, "备份目录不存在（尚未备份过；建议定期备份）"));
        }
        else
        {
            items.Add(new DocItem("Backup", 0, "备份目录: " + SanitizeForReport(bkRoot)));
            string[] dirs;
            try { dirs = Directory.GetDirectories(bkRoot, "dsh-data-*"); } catch { dirs = new string[0]; }
            Array.Sort(dirs);   // 时间戳升序（yyyyMMdd-HHmmssfff 字典序=时间序）
            string latest = null;
            for (int i = dirs.Length - 1; i >= 0; i--) { if (IsValidBackupDir(dirs[i])) { latest = dirs[i]; break; } }
            if (latest == null) items.Add(new DocItem("Backup", 1, "无有效备份（全部无效或为空）"));
            else
            {
                items.Add(new DocItem("Backup", 0, "最新备份: " + SanitizeForReport(Path.GetFileName(latest))));
                try
                {
                    string ts = Path.GetFileName(latest).Substring("dsh-data-".Length);
                    if (ts.Length >= 18) ts = ts.Substring(0, 18);
                    DateTime lt;
                    if (DateTime.TryParseExact(ts, "yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture, DateTimeStyles.None, out lt))
                    {
                        int days = (int)(DateTime.Now - lt).TotalDays;
                        items.Add(new DocItem("Backup", days > 7 ? 1 : 0, "距上次备份: " + days + " 天" + (days > 7 ? "（建议更新备份）" : "")));
                    }
                }
                catch { }
            }
        }

        // ---- Network ----
        string reg = "";
        string cfgReg = RunCapture("cmd.exe", "/c npm config get registry 2>nul");
        if (!string.IsNullOrWhiteSpace(cfgReg)) reg = cfgReg.Trim();
        if (reg.Length == 0) reg = NPM_OFFICIAL;
        bool reach = HttpResponds(reg, 4000);
        items.Add(new DocItem("Network", reach ? 0 : 1, "npm registry " + SanitizeForReport(reg) + (reach ? " 可达" : " 不可达（离线或网络受限；不影响本地功能）")));

        // ---- Integrity（v2.7：自身 exe 与随包 hashes.txt 的一致性）----
        // 三态：true=匹配；false=不匹配（高度可疑，按错误级报出）；null=旁无清单（单独复制 exe / 开发布局，正常，按通过级并说明）
        bool? si = SelfIntegrity();
        if (si == true) items.Add(new DocItem("Integrity", 0, "自身 exe 与随包 hashes.txt 一致（未被改动）"));
        else if (si == false) items.Add(new DocItem("Integrity", 2, "自身 exe 与随包 hashes.txt 不一致！（可能被篡改或替换，请从官方 Release 重新下载）"));
        else items.Add(new DocItem("Integrity", 0, "旁无 hashes.txt，跳过自身校验（单独复制 exe 或源码编译属正常；如需校验请使用官方发布包）"));
    }

    // ---------------- 备份管理 / Dry-Run（v2.6） ----------------

}
