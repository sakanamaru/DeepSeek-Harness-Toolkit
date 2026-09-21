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

    /// <summary>profile 目录（~/.dsh/profiles）。</summary>
    static string ProfilesRoot() { return Path.Combine(DataRoot(), "profiles"); }


    /// <summary>需要 maxDepth 的插件包：provider 自己管不了递归预算的那些。</summary>
    static bool NeedsMaxDepthPlugin(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string n = name.Trim().Trim('\'', '"');
        return n == "@deepseek-ai/dsh-tool-subagent" || n == "@deepseek-ai/dsh-subagent-acp";
    }


    /// <summary>YAML 标量清洗：去引号 + 去掉引号外的行尾注释（引号内的 # 不当注释）。纯函数。</summary>
    static string CleanYamlScalar(string v)
    {
        if (v == null) return "";
        string s = v.Trim();
        bool inS = false, inD = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\'' && !inD) inS = !inS;
            else if (c == '"' && !inS) inD = !inD;
            else if (c == '#' && !inS && !inD && i > 0 && s[i - 1] == ' ') { s = s.Substring(0, i).Trim(); break; }
        }
        if (s.Length >= 2 && ((s[0] == '\'' && s[s.Length - 1] == '\'') || (s[0] == '"' && s[s.Length - 1] == '"')))
            s = s.Substring(1, s.Length - 2);
        return s.Trim();
    }


    /// <summary>看着像路径（含分隔符或常见可执行后缀）——用于 mcp command 只报不修的顺带检查。</summary>
    static bool LooksLikeCommandPath(string v)
    {
        if (string.IsNullOrEmpty(v)) return false;
        if (v.IndexOf('\\') >= 0 || v.IndexOf('/') >= 0) return true;
        string l = v.ToLowerInvariant();
        return l.EndsWith(".exe") || l.EndsWith(".cmd") || l.EndsWith(".bat") || l.EndsWith(".ps1");
    }


    /// <summary>路径脱敏 + 相对 ~/.dsh 友好化（pretty=true → ~/.dsh/profiles/...）。</summary>
    static string RelToDataRoot(string path, bool pretty)
    {
        try
        {
            string root = DataRoot().TrimEnd('\\');
            if (!string.IsNullOrEmpty(path) && path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                string rel = path.Substring(root.Length).TrimStart('\\');
                return pretty ? ("~/.dsh/" + rel.Replace('\\', '/')) : rel;
            }
        }
        catch { }
        return pretty ? SanitizeForReport(path) : Path.GetFileName(path);
    }


    /// <summary>一个 profile 风险项（块级扫描结果）。</summary>
    class ProfileFinding
    {
        public string File = "";
        public int Line;
        public string Id = "";
        public string Missing = "";   // 缺失/有问题的键
        public string Hint = "";      // 处方
    }


    /// <summary>是否条目起始行：输出缩进、去缩进正文、是否 `- id:` 形式。</summary>
    static bool TryEntryStart(string line, out int indent, out string body, out bool dashForm)
    {
        indent = 0; body = ""; dashForm = false;
        if (line == null) return false;
        string t = line.TrimEnd();
        if (t.Trim().Length == 0 || t.TrimStart().StartsWith("#")) return false;
        while (indent < t.Length && t[indent] == ' ') indent++;
        body = t.Substring(indent);
        if (Regex.IsMatch(body, @"^-\s+id:\s*\S")) { dashForm = true; return true; }
        if (Regex.IsMatch(body, @"^id:\s*\S")) { dashForm = false; return true; }
        return false;
    }


    /// <summary>条目块结束行下标（不含）。dash 形式按缩进切分；顶层 `id:` 形式没有结构边界，
    /// 退到下一个同级/更浅的 `id:`/`- ` 行为止。</summary>
    static int ProfileBlockEnd(string[] lines, int start, int entryIndent, bool dashForm)
    {
        int j = start + 1;
        while (j < lines.Length)
        {
            string s = (lines[j] ?? "").TrimEnd();
            if (s.Trim().Length == 0) { j++; continue; }
            int ind = 0; while (ind < s.Length && s[ind] == ' ') ind++;
            string b = s.Substring(ind);
            if (b.StartsWith("#")) { j++; continue; }
            if (dashForm)
            {
                if (ind < entryIndent) break;
                if (ind == entryIndent && (b.StartsWith("- ") || b == "-")) break;
            }
            else
            {
                if (ind <= entryIndent && (b.StartsWith("id:") || b.StartsWith("- ") || b == "-")) break;
            }
            j++;
        }
        return j;
    }


    /// <summary>块级扫描（纯函数，单测入口）：找出「需要 maxDepth 却没写」的条目，
    /// 以及 mcp-client 里 failOnStartupError=true 但 command 指向不存在文件的条目（只报不修）。
    /// 不做 YAML 全解析——`- id: x`（insert 条目）或顶层 `id: x`（修改条目）开块。</summary>
    static List<ProfileFinding> ProfileCheckText(string text, string fileLabel)
    {
        var list = new List<ProfileFinding>();
        if (string.IsNullOrEmpty(text)) return list;
        string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        int i = 0;
        while (i < lines.Length)
        {
            int entryIndent; string body; bool dashForm;
            if (!TryEntryStart(lines[i], out entryIndent, out body, out dashForm)) { i++; continue; }
            Match idm = Regex.Match(body, dashForm ? @"^-\s+id:\s*(.*)$" : @"^id:\s*(.*)$");
            string id = CleanYamlScalar(idm.Groups[1].Value);
            int j = ProfileBlockEnd(lines, i, entryIndent, dashForm);

            string name = null;
            bool hasConfig = false, hasMaxDepth = false, hasKeyAnywhere = false;
            int configIndent = -1;
            string mcpCommand = null; bool mcpFailOnStartup = false;
            for (int k = i + 1; k < j; k++)
            {
                string s = (lines[k] ?? "").TrimEnd();
                if (s.Trim().Length == 0 || s.TrimStart().StartsWith("#")) continue;
                int ind = 0; while (ind < s.Length && s[ind] == ' ') ind++;
                string b = s.Substring(ind);
                if (name == null && b.StartsWith("name:")) name = CleanYamlScalar(b.Substring(5));
                if (b.StartsWith("maxDepth:")) { hasKeyAnywhere = true; if (hasConfig && ind > configIndent) hasMaxDepth = true; }
                if (!hasConfig && b.StartsWith("config:")) { hasConfig = true; configIndent = ind; continue; }
                if (b.StartsWith("command:")) mcpCommand = CleanYamlScalar(b.Substring(8));
                if (b.StartsWith("failOnStartupError:")) mcpFailOnStartup = CleanYamlScalar(b.Substring(19)).ToLowerInvariant() == "true";
            }

            if (NeedsMaxDepthPlugin(name) && !hasMaxDepth && !hasKeyAnywhere)
            {
                var f = new ProfileFinding();
                f.File = fileLabel; f.Line = i + 1; f.Id = id; f.Missing = "maxDepth";
                f.Hint = hasConfig ? "set maxDepth: 'provider-managed'" : "set maxDepth: 'provider-managed' (no config: block - manual)";
                list.Add(f);
            }
            else if (!string.IsNullOrEmpty(name) && name.Trim().Trim('\'', '"') == "@deepseek-ai/dsh-mcp-client"
                     && mcpFailOnStartup && LooksLikeCommandPath(mcpCommand) && !File.Exists(mcpCommand))
            {
                var f = new ProfileFinding();
                f.File = fileLabel; f.Line = i + 1; f.Id = id; f.Missing = "command";
                f.Hint = "command path not found: " + mcpCommand;
                list.Add(f);
            }
            i = j;
        }
        return list;
    }


    /// <summary>扫描 profile 目录下所有 yaml/yml（只读）。dir 空 = ~/.dsh/profiles。
    /// 默认跳过 node_modules（那是包自带的 vendor 补丁层，改了会被重装覆盖，不该由工具箱动）；
    /// includeVendor=true 时才一并扫描。skippedVendor 回报被跳过的文件数（透明，不静默吞掉）。
    /// abs=true 时报告里用绝对路径（GUI 要拿它去调 profilepatch；默认是脱敏的 ~/.dsh/... 友好形式）。</summary>
    static List<ProfileFinding> ProfileCheckScan(string dir, bool includeVendor, bool abs, out int fileCount, out int skippedVendor)
    {
        fileCount = 0;
        skippedVendor = 0;
        var all = new List<ProfileFinding>();
        try
        {
            if (string.IsNullOrEmpty(dir)) dir = ProfilesRoot();
            if (!Directory.Exists(dir)) return all;
            var merged = new List<string>();
            merged.AddRange(Directory.GetFiles(dir, "*.yml", SearchOption.AllDirectories));
            merged.AddRange(Directory.GetFiles(dir, "*.yaml", SearchOption.AllDirectories));
            merged.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string f in merged)
            {
                if (!includeVendor && f.IndexOf("\\node_modules\\", StringComparison.OrdinalIgnoreCase) >= 0) { skippedVendor++; continue; }
                string text = null;
                try { text = File.ReadAllText(f, new UTF8Encoding(false)); } catch { continue; }
                fileCount++;
                all.AddRange(ProfileCheckText(text, abs ? f : RelToDataRoot(f, true)));
            }
        }
        catch { }
        return all;
    }


    /// <summary>profilecheck：静态预检（只读，不用等它崩）。
    /// 用法：profilecheck [--dir &lt;目录&gt;] [--file &lt;单个 yaml&gt;] [--vendor] [--abs]
    ///   --abs：报告用绝对路径，并追加机器可解析的 PROFILECHK_FIX &lt;文件&gt;|&lt;行&gt;|&lt;id&gt;|&lt;键&gt; 行（GUI 用）。</summary>
    static void ProfileCheckCli(string[] args)
    {
        string dir = FlagValue(args, "--dir") ?? FlagValue(args, "-dir");
        string one = FlagValue(args, "--file") ?? FlagValue(args, "-file");
        bool vendor = HasFlag(args, "--vendor") || HasFlag(args, "-vendor");
        bool abs = HasFlag(args, "--abs") || HasFlag(args, "-abs");
        int files = 0, skipped = 0;
        List<ProfileFinding> fs;
        if (!string.IsNullOrEmpty(one))
        {
            var list = new List<ProfileFinding>();
            try
            {
                if (File.Exists(one))
                {
                    files = 1;
                    list.AddRange(ProfileCheckText(File.ReadAllText(one, new UTF8Encoding(false)), abs ? one : RelToDataRoot(one, true)));
                }
            }
            catch { }
            fs = list;
        }
        else fs = ProfileCheckScan(dir, vendor, abs, out files, out skipped);

        foreach (ProfileFinding f in fs)
            Console.WriteLine("PROFILECHK_WARN " + f.File + " " + f.Line + " " + f.Id + " " + f.Missing + " " + f.Hint);
        Console.WriteLine("PROFILECHK_TOTAL " + fs.Count + " " + files);
        if (skipped > 0) Console.WriteLine("PROFILECHK_SKIPPED_VENDOR " + skipped);
        if (abs)
        {
            // 只有能自动修的（maxDepth）才给 FIX 行；command 类只报不修
            foreach (ProfileFinding f in fs)
                if (f.Missing == "maxDepth") Console.WriteLine("PROFILECHK_FIX " + f.File + "|" + f.Line + "|" + f.Id + "|" + f.Missing);
        }
        if (fs.Count == 0) Console.WriteLine("PROFILECHK_OK");
    }

    // ---- bootdiag：解析捕获到的启动输出（只读） ----


    class BootDiagResult
    {
        public bool Recognized;
        public string Kind = "unknown";
        public string Plugin = "";
        public string Entry = "";
        public string File = "";
        public int Line;
        public string Hint = "";
        public string FirstError = "";
    }


    /// <summary>file:///C:/a/b#entry → C:\a\b + entry（纯函数，单测覆盖）。</summary>
    static string FileUrlToPath(string url, out string fragment)
    {
        fragment = "";
        if (string.IsNullOrEmpty(url)) return "";
        string u = url.Trim().Trim('\'', '"');
        int hash = u.IndexOf('#');
        if (hash >= 0) { fragment = u.Substring(hash + 1); u = u.Substring(0, hash); }
        if (u.StartsWith("file:///")) u = u.Substring(8);
        else if (u.StartsWith("file://")) u = u.Substring(7);
        else if (u.StartsWith("file:/")) u = u.Substring(6);
        try { u = Uri.UnescapeDataString(u); } catch { }
        // file:///C:/a/b → C:\a\b （盘符形式无需再加前导反斜杠）
        if (Regex.IsMatch(u, @"^/[A-Za-z]:/")) u = u.Substring(1);
        return u.Replace('/', '\\');
    }


    /// <summary>在文件或目录里定位某个条目 id 的行号（1-based）；找不到返回 0。</summary>
    static int LocateEntryLine(string dirOrFile, string entry, out string foundFile)
    {
        foundFile = "";
        if (string.IsNullOrEmpty(entry)) return 0;
        var cands = new List<string>();
        try
        {
            if (File.Exists(dirOrFile)) cands.Add(dirOrFile);
            else if (Directory.Exists(dirOrFile))
            {
                cands.AddRange(Directory.GetFiles(dirOrFile, "*.yml", SearchOption.AllDirectories));
                cands.AddRange(Directory.GetFiles(dirOrFile, "*.yaml", SearchOption.AllDirectories));
            }
            else if (Directory.Exists(ProfilesRoot()))
            {
                cands.AddRange(Directory.GetFiles(ProfilesRoot(), "*.yml", SearchOption.AllDirectories));
                cands.AddRange(Directory.GetFiles(ProfilesRoot(), "*.yaml", SearchOption.AllDirectories));
            }
        }
        catch { return 0; }
        foreach (string f in cands)
        {
            try
            {
                string[] ls = File.ReadAllLines(f, new UTF8Encoding(false));
                for (int k = 0; k < ls.Length; k++)
                {
                    int ind; string body; bool dash;
                    if (!TryEntryStart(ls[k], out ind, out body, out dash)) continue;
                    Match m = Regex.Match(body, dash ? @"^-\s+id:\s*(.*)$" : @"^id:\s*(.*)$");
                    if (CleanYamlScalar(m.Groups[1].Value) == entry) { foundFile = f; return k + 1; }
                }
            }
            catch { }
        }
        return 0;
    }


    /// <summary>解析启动输出（纯函数，单测入口）。识别不到已知签名时 Recognized=false（不做猜测）。</summary>
    static BootDiagResult BootDiagText(string text)
    {
        var r = new BootDiagResult();
        if (string.IsNullOrEmpty(text)) return r;
        string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        foreach (string ln in lines) { string t = (ln ?? "").Trim(); if (t.Length > 0) { r.FirstError = t; break; } }
        foreach (string ln in lines)
        {
            string t = (ln ?? "").Trim();
            if (t.IndexOf("plugin tree failed to load", StringComparison.OrdinalIgnoreCase) >= 0) { r.FirstError = t; r.Recognized = true; break; }
        }
        if (!r.Recognized)
        {
            foreach (string ln in lines)
            {
                string t = (ln ?? "").Trim();
                if (t.StartsWith("Error", StringComparison.OrdinalIgnoreCase) || t.IndexOf("error:", StringComparison.OrdinalIgnoreCase) >= 0) { r.FirstError = t; break; }
            }
            return r;
        }
        r.Kind = (text.IndexOf("cannot enforce maxDepth", StringComparison.OrdinalIgnoreCase) >= 0) ? "maxDepth-missing" : "plugin-tree-load";

        MatchCollection mc = Regex.Matches(text, @"failed to apply loader entry\s+([^\s(]+)\s*\(([^)]+)\)");
        // cause 链是一层层包的：最外层往往是 include (cordis:include)，真正的病灶是最内层那条。
        // 规则：优先取带 @ 的包名匹配（最靠后的那个=最内层）；没有包名时退回最后一个匹配。
        string lastEntry = "", lastPlugin = "", pkgEntry = "", pkgPlugin = "";
        foreach (Match mm in mc)
        {
            string e = mm.Groups[1].Value.Trim();
            string p = mm.Groups[2].Value.Trim();
            lastEntry = e; lastPlugin = p;
            if (p.StartsWith("@")) { pkgEntry = e; pkgPlugin = p; }
        }
        r.Entry = pkgEntry.Length > 0 ? pkgEntry : lastEntry;
        r.Plugin = pkgPlugin.Length > 0 ? pkgPlugin : lastPlugin;
        Match mh = Regex.Match(text, @"set maxDepth:\s*'([^']+)'");
        r.Hint = mh.Success ? ("set maxDepth: '" + mh.Groups[1].Value + "'") : "set maxDepth: 'provider-managed'";
        Match mf = Regex.Match(text, "file:///[^\\s\"']+");
        if (mf.Success)
        {
            string frag;
            string p = FileUrlToPath(mf.Value, out frag);
            if (frag.Length > 0) r.Entry = frag;   // file:///...#entry 里的片段是最可靠的条目 id
            r.File = p;
            string found; int line = LocateEntryLine(p, r.Entry, out found);
            if (line > 0) { r.File = found; r.Line = line; }
            else if (File.Exists(p)) r.File = p;
        }
        return r;
    }


    static void BootDiagCli(string[] args)
    {
        string from = FlagValue(args, "--from") ?? FlagValue(args, "-from");
        if (string.IsNullOrEmpty(from)) { Console.WriteLine("BOOTDIAG_FAIL no-input"); return; }
        string text = null;
        try { if (File.Exists(from)) text = File.ReadAllText(from, new UTF8Encoding(false)); } catch { }
        if (text == null) { Console.WriteLine("BOOTDIAG_FAIL cannot-read " + SanitizeForReport(from)); return; }
        BootDiagResult r = BootDiagText(text);
        if (!r.Recognized)
        {
            Console.WriteLine("BOOTDIAG_FAIL");
            Console.WriteLine("BOOTDIAG_KIND unknown");
            Console.WriteLine("BOOTDIAG_FIRST " + ClipText(r.FirstError, 200));
            return;
        }
        Console.WriteLine("BOOTDIAG_OK");
        Console.WriteLine("BOOTDIAG_KIND " + r.Kind);
        Console.WriteLine("BOOTDIAG_PLUGIN " + r.Plugin);
        Console.WriteLine("BOOTDIAG_ENTRY " + r.Entry);
        Console.WriteLine("BOOTDIAG_FILE " + SanitizeForReport(r.File));
        Console.WriteLine("BOOTDIAG_LINE " + r.Line);
        Console.WriteLine("BOOTDIAG_HINT " + r.Hint);
    }

    // ---- profilepatch：受控单行插入（先备份，后写入，可回滚） ----


    class ProfilePatchPlan
    {
        public bool Noop;
        public string Reason = "";
        public int InsertAt = -1;      // 原始文本的字符偏移（-1 = 无法插入）
        public string InsertText = "";
        public string Indent = "";
        public int Line;               // 插入行的行号（1-based）
        public string NewText = null;
    }


    /// <summary>规划单行插入（纯函数，单测入口）：在该 id 条目的 config: 块内、同级键最后一行之后插一行。
    /// 已有同名键 → Noop；找不到条目 / 没有 config: 块 → Reason。除新增那一行外不改任何字符。</summary>
    static ProfilePatchPlan PlanProfilePatch(string text, string id, string key, string value)
    {
        var plan = new ProfilePatchPlan();
        if (string.IsNullOrEmpty(text)) { plan.Reason = "empty-file"; return plan; }
        if (string.IsNullOrEmpty(id)) { plan.Reason = "no-id"; return plan; }
        if (string.IsNullOrEmpty(key)) { plan.Reason = "no-key"; return plan; }

        string nl = (text.IndexOf("\r\n") >= 0) ? "\r\n" : "\n";
        var lines = new List<string>(text.Split('\n'));
        var offsets = new List<int>();
        int acc = 0;
        for (int i = 0; i < lines.Count; i++) { offsets.Add(acc); acc += lines[i].Length + 1; }

        int entryIdx = -1, entryIndent = 0; bool dashForm = false;
        for (int i = 0; i < lines.Count; i++)
        {
            int ind; string body; bool dash;
            if (!TryEntryStart(lines[i], out ind, out body, out dash)) continue;
            Match m = Regex.Match(body, dash ? @"^-\s+id:\s*(.*)$" : @"^id:\s*(.*)$");
            if (CleanYamlScalar(m.Groups[1].Value) == id) { entryIdx = i; entryIndent = ind; dashForm = dash; break; }
        }
        if (entryIdx < 0) { plan.Reason = "entry-not-found"; return plan; }

        string[] arr = lines.ToArray();
        int end = ProfileBlockEnd(arr, entryIdx, entryIndent, dashForm);
        int configIdx = -1, configIndent = -1;
        int lastKeyIdx = -1, keyIndent = -1;
        for (int k = entryIdx + 1; k < end; k++)
        {
            string s = (arr[k] ?? "").TrimEnd();
            if (s.Trim().Length == 0 || s.TrimStart().StartsWith("#")) continue;
            int ind = 0; while (ind < s.Length && s[ind] == ' ') ind++;
            string b = s.Substring(ind);
            if (configIdx < 0)
            {
                if (b.StartsWith("config:")) { configIdx = k; configIndent = ind; }
                continue;
            }
            if (ind <= configIndent) continue;
            if (b.StartsWith(key + ":")) { plan.Noop = true; return plan; }   // 幂等：已有该键
            if (lastKeyIdx < 0) keyIndent = ind;
            lastKeyIdx = k;
        }
        if (configIdx < 0) { plan.Reason = "no-config-block"; return plan; }

        string insertIndent = new string(' ', lastKeyIdx >= 0 ? keyIndent : (configIndent + 2));
        string val = value.Trim();
        if (val.StartsWith("'") || val.StartsWith("\"")) val = val.Trim('\'', '"');
        string line = insertIndent + key + ": '" + val + "'";

        int afterIdx = lastKeyIdx >= 0 ? lastKeyIdx : configIdx;
        if (afterIdx + 1 < lines.Count)
        {
            plan.InsertAt = offsets[afterIdx + 1];
            plan.InsertText = line + nl;
            plan.Line = afterIdx + 2;
        }
        else
        {
            // 插在文件末尾（末行没有换行符）
            plan.InsertAt = text.Length;
            plan.InsertText = nl + line;
            plan.Line = lines.Count + 1;
        }
        plan.Indent = insertIndent;
        plan.NewText = text.Substring(0, plan.InsertAt) + plan.InsertText + text.Substring(plan.InsertAt);
        return plan;
    }


    /// <summary>完整流程：读（严格 UTF-8，保留 BOM 与否）→ 幂等 → 备份 → 写一行 → 复扫 → 失败回滚。
    /// simulateVerifyFail 仅供单测覆盖回滚路径。返回机器标记行。</summary>
    static string ProfilePatchApply(string file, string id, string key, string value, bool simulateVerifyFail, out string backupPath)
    {
        backupPath = null;
        if (!File.Exists(file)) return "PROFILEPATCH_FAIL file-not-found";
        bool bom;
        string text;
        try
        {
            byte[] raw = File.ReadAllBytes(file);
            bom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
            text = new UTF8Encoding(false, true).GetString(raw, bom ? 3 : 0, raw.Length - (bom ? 3 : 0));
        }
        catch { return "PROFILEPATCH_FAIL not-utf8"; }

        ProfilePatchPlan plan = PlanProfilePatch(text, id, key, value);
        if (plan.Noop) return "PROFILEPATCH_NOOP";
        if (plan.InsertAt < 0) return "PROFILEPATCH_FAIL " + plan.Reason;

        backupPath = BackupProfileFile(file);
        if (backupPath == null) return "PROFILEPATCH_FAIL backup-failed";
        try
        {
            byte[] body = new UTF8Encoding(false).GetBytes(plan.NewText);
            byte[] outBytes = new byte[body.Length + (bom ? 3 : 0)];
            if (bom) { outBytes[0] = 0xEF; outBytes[1] = 0xBB; outBytes[2] = 0xBF; }
            Array.Copy(body, 0, outBytes, bom ? 3 : 0, body.Length);
            File.WriteAllBytes(file, outBytes);
        }
        catch (Exception ex) { return "PROFILEPATCH_FAIL write: " + ClipText(ex.Message, 120); }

        bool still = simulateVerifyFail;
        if (!still)
        {
            foreach (ProfileFinding f in ProfileCheckText(plan.NewText, SanitizeForReport(file)))
                if (f.Id == id && f.Missing == "maxDepth") { still = true; break; }
        }
        if (still)
        {
            try { File.Copy(backupPath, file, true); } catch { }
            return "PROFILEPATCH_ROLLBACK " + SanitizeForReport(backupPath);
        }
        return "PROFILEPATCH_OK " + SanitizeForReport(file) + ":" + plan.Line;
    }

    // ---- v2.7.2：第二条处方「禁用出问题的插件」（顶层补丁项 - id: x / disabled: true） ----
    // 依据（本机 dsh 包内的类型定义 + 实现，不是猜的）：
    //   cordis-plugin-include 的 PatchOptions 有 `disabled?: boolean | null` 这个一等公民字段；
    //   applyEntryPatches 对「非 insert 补丁」做平铺覆盖（target[key] = value），匹配不到只 warn 跳过。
    //   所以 `- id: <条目>` + `disabled: true` 就是「关掉这一行」的正统写法 —— dsh 自己关遥测行也这么干。
    // 注意：同一补丁列表里，`insert:` 插入的行会被索引，后面的 id 补丁才能命中 → 必须「追加在文件末尾」。
    // 与 maxDepth 处方的区别：那条让插件继续能用（外科修复），这条是通用兜底（任何坏插件都能先关掉）。


    /// <summary>条目 id 必须是安全标量：只允许 [A-Za-z0-9._@/-]。
    /// 防 YAML 注入：id 来自文件扫描或 GUI 传参，若带换行/冒号会往补丁文件里注入新键。</summary>
    static bool IsSafePatchId(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        foreach (char c in id)
        {
            bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
            if (!ok && c != '.' && c != '_' && c != '-' && c != '@' && c != '/') return false;
        }
        return true;
    }


    /// <summary>文件里是否存在该 id 的条目（insert 条目或顶层补丁项）。</summary>
    static bool PatchHasEntry(string text, string id)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(id)) return false;
        string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            int ind; string body; bool dash;
            if (!TryEntryStart(lines[i], out ind, out body, out dash)) continue;
            Match m = Regex.Match(body, dash ? @"^-\s+id:\s*(.*)$" : @"^id:\s*(.*)$");
            if (CleanYamlScalar(m.Groups[1].Value) == id) return true;
        }
        return false;
    }


    /// <summary>是否已有该 id 且 disabled: true（幂等判断 + 写后复检共用）。</summary>
    static bool PatchHasDisabled(string text, string id)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(id)) return false;
        string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        int i = 0;
        while (i < lines.Length)
        {
            int entryIndent; string body; bool dash;
            if (!TryEntryStart(lines[i], out entryIndent, out body, out dash)) { i++; continue; }
            Match m = Regex.Match(body, dash ? @"^-\s+id:\s*(.*)$" : @"^id:\s*(.*)$");
            string cur = CleanYamlScalar(m.Groups[1].Value);
            int j = ProfileBlockEnd(lines, i, entryIndent, dash);
            if (cur == id)
            {
                for (int k = i + 1; k < j; k++)
                {
                    string s = (lines[k] ?? "").Trim();
                    if (!s.StartsWith("disabled:")) continue;
                    string v = CleanYamlScalar(s.Substring(9)).ToLowerInvariant();
                    if (v == "true" || v == "yes" || v == "on") return true;
                }
            }
            i = j;
        }
        return false;
    }


    /// <summary>规划「禁用该条目」：在文件末尾追加一个顶层补丁项（只追加，不改动任何已有字符）。
    /// 已有 disabled: true → Noop；id 不存在 → entry-not-found（免得写一条永远匹配不到的补丁）。</summary>
    static ProfilePatchPlan PlanProfileDisable(string text, string id)
    {
        var plan = new ProfilePatchPlan();
        if (string.IsNullOrEmpty(text)) { plan.Reason = "empty-file"; return plan; }
        if (string.IsNullOrEmpty(id)) { plan.Reason = "no-id"; return plan; }
        if (!IsSafePatchId(id)) { plan.Reason = "bad-id"; return plan; }
        if (!PatchHasEntry(text, id)) { plan.Reason = "entry-not-found"; return plan; }
        if (PatchHasDisabled(text, id)) { plan.Noop = true; return plan; }

        string nl = (text.IndexOf("\r\n") >= 0) ? "\r\n" : "\n";
        bool needNl = !text.EndsWith("\n");
        plan.InsertAt = text.Length;
        plan.InsertText = (needNl ? nl : "") + "- id: " + id + nl + "  disabled: true" + nl;
        plan.Indent = "";
        plan.Line = text.Replace("\r\n", "\n").Split('\n').Length + (needNl ? 1 : 0);
        plan.NewText = text + plan.InsertText;
        return plan;
    }


    /// <summary>执行「禁用」：读（严格 UTF-8，保留 BOM）→ 幂等 → 备份 → 追加 → 复检 → 失败回滚。</summary>
    static string ProfilePatchDisableApply(string file, string id, bool simulateVerifyFail, out string backupPath)
    {
        backupPath = null;
        if (!File.Exists(file)) return "PROFILEPATCH_FAIL file-not-found";
        bool bom; string text;
        try
        {
            byte[] raw = File.ReadAllBytes(file);
            bom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
            text = new UTF8Encoding(false, true).GetString(raw, bom ? 3 : 0, raw.Length - (bom ? 3 : 0));
        }
        catch { return "PROFILEPATCH_FAIL not-utf8"; }

        ProfilePatchPlan plan = PlanProfileDisable(text, id);
        if (plan.Noop) return "PROFILEPATCH_NOOP";
        if (plan.InsertAt < 0) return "PROFILEPATCH_FAIL " + plan.Reason;

        backupPath = BackupProfileFile(file);
        if (backupPath == null) return "PROFILEPATCH_FAIL backup-failed";
        try
        {
            byte[] body = new UTF8Encoding(false).GetBytes(plan.NewText);
            byte[] outBytes = new byte[body.Length + (bom ? 3 : 0)];
            if (bom) { outBytes[0] = 0xEF; outBytes[1] = 0xBB; outBytes[2] = 0xBF; }
            Array.Copy(body, 0, outBytes, bom ? 3 : 0, body.Length);
            File.WriteAllBytes(file, outBytes);
        }
        catch (Exception ex) { return "PROFILEPATCH_FAIL write: " + ClipText(ex.Message, 120); }

        bool ok = !simulateVerifyFail && PatchHasDisabled(plan.NewText, id);
        if (!ok)
        {
            try { File.Copy(backupPath, file, true); } catch { }
            return "PROFILEPATCH_ROLLBACK " + SanitizeForReport(backupPath);
        }
        return "PROFILEPATCH_OK " + SanitizeForReport(file) + ":" + plan.Line + " disabled";
    }


    /// <summary>profilepatch：受控写入（必须先预览；--yes 才落盘；白名单只允许 maxDepth 处方）。</summary>
    static void ProfilePatchCli(string[] args)
    {
        string file = FlagValue(args, "--file") ?? FlagValue(args, "-file");
        string id = FlagValue(args, "--id") ?? FlagValue(args, "-id");
        string setPair = FlagValue(args, "--set") ?? FlagValue(args, "-set");
        bool yes = HasFlag(args, "--yes") || HasFlag(args, "-yes");
        // ---- v2.7.2：手动隔离处方（--disable）----
        // 只做「用户显式点/显式敲」的兜底：不自动、不改已有字符、先备份、写后复检、失败回滚。
        if (HasFlag(args, "--disable") || HasFlag(args, "-disable"))
        {
            if (string.IsNullOrEmpty(file) || string.IsNullOrEmpty(id))
            {
                Console.WriteLine("PROFILEPATCH_FAIL usage: profilepatch --file <yaml> --id <entry> --disable [--yes]");
                return;
            }
            if (!File.Exists(file)) { Console.WriteLine("PROFILEPATCH_FAIL file-not-found " + SanitizeForReport(file)); return; }
            bool dbom; string dtext;
            try
            {
                byte[] draw = File.ReadAllBytes(file);
                dbom = draw.Length >= 3 && draw[0] == 0xEF && draw[1] == 0xBB && draw[2] == 0xBF;
                dtext = new UTF8Encoding(false, true).GetString(draw, dbom ? 3 : 0, draw.Length - (dbom ? 3 : 0));
            }
            catch { Console.WriteLine("PROFILEPATCH_FAIL not-utf8"); return; }
            ProfilePatchPlan dplan = PlanProfileDisable(dtext, id);
            if (dplan.Noop) { Console.WriteLine("PROFILEPATCH_NOOP"); return; }
            if (dplan.InsertAt < 0) { Console.WriteLine("PROFILEPATCH_FAIL " + dplan.Reason); return; }
            Console.WriteLine("PROFILEPATCH_PLAN " + SanitizeForReport(file) + ":" + dplan.Line + " - id: " + id + " / disabled: true");
            if (!yes) { Console.WriteLine("PROFILEPATCH_DRYRUN"); return; }
            string dbk;
            string dres = ProfilePatchDisableApply(file, id, false, out dbk);
            if (!string.IsNullOrEmpty(dbk)) Console.WriteLine("PROFILEPATCH_BACKUP " + SanitizeForReport(dbk));
            Console.WriteLine(dres);
            return;
        }
        if (string.IsNullOrEmpty(file) || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(setPair))
        {
            Console.WriteLine("PROFILEPATCH_FAIL usage: profilepatch --file <yaml> --id <entry> --set key=value [--yes] | --disable [--yes]");
            return;
        }
        int eq = setPair.IndexOf('=');
        if (eq <= 0) { Console.WriteLine("PROFILEPATCH_FAIL bad-set"); return; }
        string key = setPair.Substring(0, eq).Trim();
        string value = setPair.Substring(eq + 1).Trim();
        // 白名单：本命令只为这一个处方存在，拒绝变成"通用配置改写器"
        if (key != "maxDepth" || value.Trim('\'', '"') != "provider-managed")
        {
            Console.WriteLine("PROFILEPATCH_FAIL unsupported-set (only maxDepth=provider-managed)");
            return;
        }
        if (!File.Exists(file)) { Console.WriteLine("PROFILEPATCH_FAIL file-not-found " + SanitizeForReport(file)); return; }

        bool bom; string text;
        try
        {
            byte[] raw = File.ReadAllBytes(file);
            bom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
            text = new UTF8Encoding(false, true).GetString(raw, bom ? 3 : 0, raw.Length - (bom ? 3 : 0));
        }
        catch { Console.WriteLine("PROFILEPATCH_FAIL not-utf8"); return; }

        ProfilePatchPlan plan = PlanProfilePatch(text, id, key, value);
        if (plan.Noop) { Console.WriteLine("PROFILEPATCH_NOOP"); return; }
        if (plan.InsertAt < 0) { Console.WriteLine("PROFILEPATCH_FAIL " + plan.Reason); return; }
        Console.WriteLine("PROFILEPATCH_PLAN " + SanitizeForReport(file) + ":" + plan.Line + " " + plan.Indent + key + ": 'provider-managed'");
        if (!yes)
        {
            // 预览模式不落任何文件（备份在 --yes 时先于写入执行）
            Console.WriteLine("PROFILEPATCH_DRYRUN");
            return;
        }
        string bk;
        string res = ProfilePatchApply(file, id, key, value, false, out bk);
        if (!string.IsNullOrEmpty(bk)) Console.WriteLine("PROFILEPATCH_BACKUP " + SanitizeForReport(bk));
        Console.WriteLine(res);
    }

    // ---------------- 配置读写命令（v2.8 Configuration） ----------------

}
