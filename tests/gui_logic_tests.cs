// DeepSeek Harness Toolkit - GUI 逻辑测试（与 gui_v2.cs 同程序集编译，零第三方依赖）
// 构建（与 CI 一致）：
//   csc /nologo /target:exe /out:guilogictests.exe gui_v2.cs tests\gui_logic_tests.cs /main:GuiLogicTests /warn:4
//   （gui_v2.cs 自带 WinForms 入口 App.Main，用 /main: 指定测试入口；本文件不实例化任何窗体）
// 覆盖：标记行解析（含 v2.8 修复的漏项）/ 信号总线与路由表 / 设置卡片模型 / i18n 强制检查
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

static class GuiLogicTests
{
    static int _pass, _fail;

    static void Check(string name, bool ok)
    {
        if (ok) { _pass++; Console.WriteLine("  [PASS] " + name); }
        else { _fail++; Console.WriteLine("  [FAIL] " + name); }
    }

    // ---------------- 源码扫描（i18n 强制检查用） ----------------

    sealed class Scan
    {
        public List<string> CjkInString = new List<string>();   // L10N 字典之外的中文字符串字面量
        public List<string> UsedKeys = new List<string>();      // L10N._("k")
        public List<string> TagKeys = new List<string>();       // Tag = "k"（语言切换时按 tag 重渲染）
        public List<string> DefinedKeys = new List<string>();   // Add("k", zh, en)
        public int L10NStart = -1, L10NEnd = -1;
    }

    static bool HasCjk(string s)
    {
        for (int i = 0; i < s.Length; i++) { char c = s[i]; if (c >= 0x4E00 && c <= 0x9FFF) return true; }
        return false;
    }

    static void Collect(string code, string marker, List<string> into)
    {
        int at = 0;
        while (true)
        {
            at = code.IndexOf(marker, at, StringComparison.Ordinal);
            if (at < 0) return;
            int s = at + marker.Length;
            int e = code.IndexOf('"', s);
            if (e < 0) return;
            into.Add(code.Substring(s, e - s));
            at = e + 1;
        }
    }

    static Scan ScanSource(string[] lines)
    {
        Scan sc = new Scan();
        // 定位 L10N 类块（顶层类型以列 0 的 "}" 结束）——结构变了就让测试失败，而不是静默放过
        for (int i = 0; i < lines.Length; i++)
        {
            if (sc.L10NStart < 0) { if (lines[i].IndexOf("static class L10N", StringComparison.Ordinal) >= 0) sc.L10NStart = i; }
            else if (sc.L10NEnd < 0 && lines[i].Length > 0 && lines[i][0] == '}' && lines[i].TrimEnd() == "}") { sc.L10NEnd = i; break; }
        }
        bool inBlockComment = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.TrimStart().StartsWith("[assembly:", StringComparison.Ordinal)) continue;   // 产品元数据不是 UI 文案
            StringBuilder code = new StringBuilder();
            int j = 0;
            while (j < line.Length)
            {
                if (inBlockComment)
                {
                    int e = line.IndexOf("*/", j, StringComparison.Ordinal);
                    if (e < 0) { j = line.Length; continue; }
                    inBlockComment = false; j = e + 2; continue;
                }
                if (j + 1 < line.Length && line[j] == '/' && line[j + 1] == '/') break;              // 行注释
                if (j + 1 < line.Length && line[j] == '/' && line[j + 1] == '*') { inBlockComment = true; j += 2; continue; }
                if (line[j] == '@' && j + 1 < line.Length && line[j + 1] == '"')                     // 逐字字符串：本行剩余按字面量处理
                {
                    int e = j + 1;
                    while (e < line.Length && line[e] != '"') e++;
                    string vlit = line.Substring(j, Math.Min(e, line.Length - 1) - j + 1);
                    code.Append(vlit);
                    if (HasCjk(vlit) && !(i >= sc.L10NStart && i <= sc.L10NEnd) && line.IndexOf("L10N.IsZh ?", StringComparison.Ordinal) < 0)
                        sc.CjkInString.Add((i + 1) + ": " + line.Trim());
                    j = Math.Min(e, line.Length - 1) + 1; continue;
                }
                if (line[j] == '"')
                {
                    int e = j + 1;
                    while (e < line.Length && line[e] != '"') { if (line[e] == '\\') e++; e++; }
                    int end = Math.Min(e, line.Length - 1);
                    string lit = line.Substring(j, end - j + 1);
                    code.Append(lit);
                    if (HasCjk(lit) && !(i >= sc.L10NStart && i <= sc.L10NEnd) && line.IndexOf("L10N.IsZh ?", StringComparison.Ordinal) < 0)
                        sc.CjkInString.Add((i + 1) + ": " + line.Trim());
                    j = end + 1; continue;
                }
                code.Append(line[j]); j++;
            }
            string c = code.ToString();
            Collect(c, "L10N._(\"", sc.UsedKeys);
            Collect(c, "Tag = \"", sc.TagKeys);
            if (i >= sc.L10NStart && i <= sc.L10NEnd) Collect(c, "Add(\"", sc.DefinedKeys);
        }
        return sc;
    }

    static string SrcPath()
    {
        string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gui_v2.cs");
        if (File.Exists(p)) return p;
        string d = Environment.CurrentDirectory;
        for (int i = 0; i < 6 && !string.IsNullOrEmpty(d); i++)
        {
            p = Path.Combine(d, "gui_v2.cs");
            if (File.Exists(p)) return p;
            try { d = Path.GetDirectoryName(d); } catch { d = null; }
        }
        return null;
    }

    public static int Main()
    {
        Console.WriteLine("== GUI 逻辑测试（呈现层基座：标记行 / 信号总线 / 设置卡片 / i18n）==");

        // ---------------- [1] 标记行解析 ----------------
        Console.WriteLine("[1] 标记行解析（核心输出的唯一契约）");
        string[] brokenBefore = new string[] { "BKEXPORT_OK", "BKDEL_OK", "CONFIGSET_OK", "DRYRUN_OK", "DOCTOR_WARN", "PROFILECHK_TOTAL", "BOOTDIAG_OK", "IMPORT_OK" };
        bool allKnown = true;
        for (int i = 0; i < brokenBefore.Length; i++) if (!Markers.IsKnown(brokenBefore[i])) allKnown = false;
        Check("v2.8 修复的漏项全部被识别（导出/删除/保存设置/dry-run/体检…）", allKnown);
        string[] oldList = new string[] { "BACKUP_OK", "BACKUP_FAIL", "BACKUP_LIST_OK", "RESTORE_OK", "RESTORE_FAIL", "STATUS_UP", "STATUS_STARTING", "STATUS_DOWN", "START_OK", "START_FAIL", "STOP_OK", "STOP_FAIL", "SHORTCUT_OK", "SHORTCUT_FAIL" };
        bool oldKept = true;
        for (int i = 0; i < oldList.Length; i++) if (!Markers.IsKnown(oldList[i])) oldKept = false;
        Check("旧名单里的每一项仍然被识别（零回归）", oldKept);
        Check("带参数的行按前缀识别", Markers.IsKnown("RESTORE_FAIL dsh 正在运行，无法恢复") && Markers.IsKnown("STATUS_PID 16748"));
        Check("进度文案不是标记行", !Markers.IsKnown("正在恢复数据...") && !Markers.IsKnown("Backing up (skipping node_modules)..."));
        Check("空/null 不是标记行", !Markers.IsKnown("") && !Markers.IsKnown(null));
        Check("Find：从混杂输出里挑出标记行", Markers.Find("正在恢复数据...\nRESTORE_OK D:\\bk\\x\n完成") == "RESTORE_OK D:\\bk\\x");
        Check("Find：CRLF + 前后空白也认", Markers.Find("a\r\n   BACKUP_OK C:\\bk  \r\n") == "BACKUP_OK C:\\bk");
        Check("Find：纯进度文案 → 空串", Markers.Find("line1\nline2\n") == "");
        Check("Find：null/空 → 空串", Markers.Find(null) == "" && Markers.Find("") == "");
        Check("IsOk：STATUS_UP/STARTING 算成功", Markers.IsOk("STATUS_UP") && Markers.IsOk("STATUS_STARTING"));
        Check("IsOk：*_OK 算成功，*_FAIL 不算", Markers.IsOk("BKEXPORT_OK") && !Markers.IsOk("BKEXPORT_FAIL") && !Markers.IsOk("RESTORE_FAIL x"));
        Check("IsOk：STATUS_DOWN/空 → 不算成功", !Markers.IsOk("STATUS_DOWN") && !Markers.IsOk(""));
        Check("已知标记行表够大（防止误删）", Markers.KnownCount >= 40);

        // ---------------- [2] 信号总线 ----------------
        Console.WriteLine("[2] 信号总线（任务即信号）");
        SignalBus.Clear();
        int got = 0; string lastMark = "";
        Action<Sig> h1 = delegate(Sig s) { got++; lastMark = s.Mark; };
        SignalBus.Subscribe(h1);
        Check("订阅后计数为 1", SignalBus.SubscriberCount == 1);
        SignalBus.Publish(new Sig(SigKind.TaskOk, "act.backup", "BACKUP_OK", ""));
        Check("发布后订阅者收到（且带 Mark）", got == 1 && lastMark == "BACKUP_OK");
        int got2 = 0;
        Action<Sig> h2 = delegate(Sig s) { got2++; };
        SignalBus.Subscribe(h2);
        SignalBus.Publish(new Sig(SigKind.StatusChanged, "", "", ""));
        Check("多订阅者都收到", got == 2 && got2 == 1);
        SignalBus.Unsubscribe(h1);
        SignalBus.Publish(new Sig(SigKind.StatusChanged, "", "", ""));
        Check("退订后不再收到", got == 2 && got2 == 2);
        SignalBus.Clear();
        Check("Clear 清空订阅者与错误", SignalBus.SubscriberCount == 0 && SignalBus.Errors.Count == 0);
        int safeGot = 0;
        SignalBus.Subscribe(delegate(Sig s) { throw new Exception("boom"); });
        SignalBus.Subscribe(delegate(Sig s) { safeGot++; });
        SignalBus.Publish(new Sig(SigKind.TaskFail, "k", "", ""));
        Check("一个订阅者抛异常不影响其他订阅者", safeGot == 1);
        Check("异常被记录（可诊断）", SignalBus.Errors.Count == 1 && SignalBus.Errors[0].IndexOf("boom") >= 0);
        int added = 0;
        SignalBus.Clear();
        SignalBus.Subscribe(delegate(Sig s) { added++; SignalBus.Subscribe(delegate(Sig x) { }); });
        SignalBus.Publish(new Sig(SigKind.TaskStarted, "k", "", ""));
        Check("派发期间的订阅不影响本轮（快照语义）", added == 1 && SignalBus.SubscriberCount == 2);
        SignalBus.Clear();
        Check("Publish(null) 是安全的空操作", true);
        try { SignalBus.Publish(null); Check("Publish(null) 不抛异常", true); } catch { Check("Publish(null) 不抛异常", false); }
        Check("Sig.ToString 含种类/键/标记", new Sig(SigKind.TaskOk, "act.backup", "BACKUP_OK", "").ToString() == "TaskOk:act.backup:BACKUP_OK");
        Check("Sig 的 null 参数被规整为空串", new Sig(SigKind.TaskFail, null, null, null).Key == "" && new Sig(SigKind.TaskFail, null, null, null).Mark == "");
        SignalBus.Clear();

        // ---------------- [3] 路由表 ----------------
        Console.WriteLine("[3] 信号 → 界面动作路由（数据化，不再散落在各处直接调用）");
        Check("TaskStarted → 更新按钮", SigRouting.ActionsFor(new Sig(SigKind.TaskStarted, "act.backup", "", "")).Length == 1 && SigRouting.ActionsFor(new Sig(SigKind.TaskStarted, "act.backup", "", ""))[0] == SigRouting.UpdateButtons);
        string[] ab = SigRouting.ActionsFor(new Sig(SigKind.TaskOk, "act.backup", "BACKUP_OK", ""));
        Check("TaskOk(act.backup) → 更新按钮 + 刷新备份列表 + 托盘提示", ab.Length == 3 && ab[0] == SigRouting.UpdateButtons && ab[1] == SigRouting.ReloadBackups && ab[2] == SigRouting.ToastOk);
        string[] dl = SigRouting.ActionsFor(new Sig(SigKind.TaskOk, "bk.delete", "BKDEL_OK", ""));
        Check("TaskOk(bk.delete) → 同上", dl.Length == 3 && dl[1] == SigRouting.ReloadBackups && dl[2] == SigRouting.ToastOk);
        string[] ex = SigRouting.ActionsFor(new Sig(SigKind.TaskOk, "bk.export", "BKEXPORT_OK", ""));
        Check("TaskOk(bk.export) → 提示但不刷新列表", ex.Length == 2 && ex[1] == SigRouting.ToastOk);
        string[] st = SigRouting.ActionsFor(new Sig(SigKind.TaskOk, "act.start", "START_OK", ""));
        Check("TaskOk(act.start) → 只更新按钮", st.Length == 1 && st[0] == SigRouting.UpdateButtons);
        Check("TaskFail → 更新按钮", SigRouting.ActionsFor(new Sig(SigKind.TaskFail, "act.backup", "", "r"))[0] == SigRouting.UpdateButtons);
        Check("TaskTimeout → 更新按钮", SigRouting.ActionsFor(new Sig(SigKind.TaskTimeout, "act.backup", "", ""))[0] == SigRouting.UpdateButtons);
        Check("StatusChanged → 更新按钮（按钮可用性由状态信号驱动）", SigRouting.ActionsFor(new Sig(SigKind.StatusChanged, "", "", ""))[0] == SigRouting.UpdateButtons);
        Check("LogAppended → 无动作", SigRouting.ActionsFor(new Sig(SigKind.LogAppended, "INFO", "", "x")).Length == 0);
        Check("null 信号 → 无动作", SigRouting.ActionsFor(null).Length == 0);

        // ---------------- [4] 设置卡片模型 ----------------
        Console.WriteLine("[4] 设置卡片模型（设置页由数据渲染）");
        SetCard[] cards = SettingsCards.Default();
        Check("4 张卡片", cards.Length == 4);
        Check("卡片标题都是 L10N 键且能取到文案", L10N._(cards[0].TitleKey) != cards[0].TitleKey && L10N._(cards[3].TitleKey) != cards[3].TitleKey);
        string[] keys = SettingsCards.AllConfigKeys();
        Check("共 9 个设置项", keys.Length == 9);
        string[] expected = new string[] { "host", "ws", "keep_backups", "check_update", "check_dsh_update", "update_channel", "lang", "auto_start", "close_action" };
        bool sameSet = keys.Length == expected.Length;
        for (int i = 0; i < expected.Length && sameSet; i++)
        {
            bool found = false;
            for (int j = 0; j < keys.Length; j++) if (keys[j] == expected[i]) found = true;
            if (!found) sameSet = false;
        }
        Check("配置键集合与核心 config-get 的键一致", sameSet);
        bool noDup = true;
        for (int i = 0; i < keys.Length && noDup; i++)
            for (int j = i + 1; j < keys.Length; j++) if (keys[i] == keys[j]) { noDup = false; break; }
        Check("配置键不重复", noDup);
        bool comboOk = true, textOk = true, labelOk = true;
        for (int i = 0; i < cards.Length; i++)
            for (int j = 0; j < cards[i].Items.Length; j++)
            {
                SetItem it = cards[i].Items[j];
                if (it.Kind == SetKind.Combo && (it.Options == null || it.Options.Length < 2)) comboOk = false;
                if (it.Kind == SetKind.Text && it.Width <= 0) textOk = false;
                if (string.IsNullOrEmpty(it.LabelKey) || L10N._(it.LabelKey) == it.LabelKey) labelOk = false;
            }
        Check("Combo 项都有 ≥2 个选项", comboOk);
        Check("Text 项都有宽度", textOk);
        Check("每个设置项的文案键都能取到文案", labelOk);
        Check("顺序：harness → backup → update → toolkit（与旧界面一致）",
            cards[0].TitleKey == "settings.g.harness" && cards[1].TitleKey == "settings.g.backup" &&
            cards[2].TitleKey == "settings.g.update" && cards[3].TitleKey == "settings.g.toolkit");
        Check("选项内容与旧界面一致（host/channel/lang/close_action）",
            cards[0].Items[0].Options[0] == "127.0.0.1" && cards[2].Items[2].Options[0] == "stable" &&
            cards[3].Items[0].Options[0] == "auto" && cards[3].Items[2].Options[2] == "exit");

        // ---------------- [5] i18n 强制检查 ----------------
        Console.WriteLine("[5] i18n 强制检查（源码级）");
        string src = SrcPath();
        if (src == null) { Check("能找到 gui_v2.cs（i18n 检查的前提）", false); }
        else
        {
            string[] lines = File.ReadAllLines(src, new UTF8Encoding(false));
            Scan sc = ScanSource(lines);
            string srcText = string.Join("\n", lines);
            Check("定位到 L10N 字典块", sc.L10NStart > 0 && sc.L10NEnd > sc.L10NStart + 50);
            Check("字典条目 ≥ 120 条", sc.DefinedKeys.Count >= 120);
            Check("界面用到 ≥ 60 个 L10N 键", sc.UsedKeys.Count >= 60);
            // A) 每个 L10N._("k") 用到的键都必须有定义
            List<string> missing = new List<string>();
            for (int i = 0; i < sc.UsedKeys.Count; i++) if (!sc.DefinedKeys.Contains(sc.UsedKeys[i]) && !missing.Contains(sc.UsedKeys[i])) missing.Add(sc.UsedKeys[i]);
            string missList = string.Join(", ", missing.ToArray());
            Check("L10N._() 用到的键都有定义" + (missing.Count > 0 ? "（缺: " + missList + "）" : ""), missing.Count == 0);
            // B) 死键检测：定义过的键必须作为字符串字面量在字典之外出现过
            //    （动态拼接的键如 act.start / settings.autostart 也以字面量形式出现在调用点，所以这条能抓真死键）
            List<string> dead = new List<string>();
            for (int i = 0; i < sc.DefinedKeys.Count; i++)
            {
                string k = sc.DefinedKeys[i];
                if (sc.UsedKeys.Contains(k) || sc.TagKeys.Contains(k)) continue;
                if (srcText.IndexOf("\"" + k + "\"", StringComparison.Ordinal) >= 0) continue;   // 作为字面量出现在别处（动态用）
                if (!dead.Contains(k)) dead.Add(k);
            }
            string deadList = string.Join(", ", dead.ToArray());
            Check("没有定义但从未使用的死键" + (dead.Count > 0 ? "（" + dead.Count + " 个: " + deadList + "）" : ""), dead.Count == 0);
            // C) 中英文都非空
            int emptyZh = 0, emptyEn = 0;
            for (int i = sc.L10NStart; i <= sc.L10NEnd && i < lines.Length; i++)
            {
                string l = lines[i].Trim();
                if (!l.StartsWith("Add(\"", StringComparison.Ordinal)) continue;
                if (l.IndexOf("\"\",") >= 0) emptyZh++;
                if (l.EndsWith("\"\");", StringComparison.Ordinal)) emptyEn++;
            }
            Check("没有空的中文/英文翻译", emptyZh == 0 && emptyEn == 0);
            // D) 核心强制：L10N 字典之外不允许出现中文字符串字面量（产品元数据行除外）
            string cjk = string.Join(" | ", sc.CjkInString.ToArray());
            Check("L10N 之外没有硬编码中文文案" + (sc.CjkInString.Count > 0 ? "（" + cjk + "）" : ""), sc.CjkInString.Count == 0);
            // E) 诊断（不算失败）：Tag 既不是字典键、也没有 ApplyLang 处理分支的控件
            List<string> orphanTags = new List<string>();
            for (int i = 0; i < sc.TagKeys.Count; i++)
            {
                string t = sc.TagKeys[i];
                if (sc.DefinedKeys.Contains(t)) continue;
                if (srcText.IndexOf("tag == \"" + t + "\"", StringComparison.Ordinal) >= 0) continue;
                if (!orphanTags.Contains(t)) orphanTags.Add(t);
            }
            Console.WriteLine("      [info] 语言切换不重渲染的 Tag（仅诊断）：" + (orphanTags.Count == 0 ? "无" : string.Join(", ", orphanTags.ToArray())));
        }

        Console.WriteLine();
        Console.WriteLine("== " + _pass + "/" + (_pass + _fail) + " passed, " + _fail + " failed ==");
        return _fail == 0 ? 0 : 1;
    }
}
