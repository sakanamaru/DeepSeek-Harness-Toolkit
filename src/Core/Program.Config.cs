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

    static string cfgWs = null;          // 手动指定的工作区路径（配置 ws=，空=自动探测）   // 访问入口：127.0.0.1 / localhost（浏览器缓存异常时可切换）

    static int cfgKeep = 10;             // 备份保留策略：自动类备份最多保留份数（配置 keep_backups=，最小 3）

    static bool cfgCheckUpdate = true;   // 启动时静默检查更新（配置 check_update=off 关闭）

    static bool cfgCheckDshUpdate = true; // 检测 dsh 本体更新开关（配置 check_dsh_update=off 关闭）

    static string cfgDshVersions = "";    // 本机装过的 dsh 历史版本（逗号分隔，最近 10 个）

    static string cfgChannel = "stable";  // v2.7 更新通道（配置 update_channel=stable|rc）

    static string cfgCloseAction = "";    // v2.7 GUI 关闭行为记忆（配置 close_action=ask|tray|exit；空=还没问过 → 首次关窗询问）

    static bool cfgAutoStart = true;      // v2.7 CLI 菜单倒计时自动启动（配置 auto_start=off 关闭倒计时，只手动选择）

    static string ConfigPath() { return Path.Combine(StateDir, "launcher.config"); }


    static void LoadConfig()
    {
        try
        {
            foreach (string line in File.ReadAllLines(ConfigPath()))
            {
                string t = line.Trim();
                if (t.StartsWith("lang=")) { string v = t.Substring(5).Trim().ToLowerInvariant();
                    if (v == "zh") lang = Lang.Zh; else if (v == "en") lang = Lang.En; else lang = Lang.Auto; }
                if (t.StartsWith("host=")) { string v = t.Substring(5).Trim().ToLowerInvariant();
                    if (v == "localhost" || v == "127.0.0.1") webHost = v; }
                if (t.StartsWith("ws=")) { string v = t.Substring(3).Trim().Trim('"');
                    if (v.Length > 0) { try { cfgWs = Path.GetFullPath(v); } catch { cfgWs = null; } } }
                if (t.StartsWith("keep_backups=")) { int v; if (int.TryParse(t.Substring(13).Trim(), out v)) cfgKeep = (v < 3) ? 3 : v; }   // 备份保留策略：自动类最多保留份数（最小 3，直接夹到 3 而非忽略）
                if (t.StartsWith("check_update=")) { string v = t.Substring(13).Trim().ToLowerInvariant(); if (v.Length > 0) cfgCheckUpdate = v != "off"; }   // 启动更新检查开关
                if (t.StartsWith("check_dsh_update=")) { string v = t.Substring(17).Trim().ToLowerInvariant(); if (v.Length > 0) cfgCheckDshUpdate = v != "off"; }   // dsh 本体更新检测开关
                if (t.StartsWith("dsh_versions=")) { string v = t.Substring(13).Trim(); if (v.Length > 0) cfgDshVersions = v; }   // 本机 dsh 历史版本
                if (t.StartsWith("update_channel=")) { string v = t.Substring(15).Trim().ToLowerInvariant(); cfgChannel = (v == "rc") ? "rc" : "stable"; }   // v2.7 更新通道
                if (t.StartsWith("close_action=")) { string v = t.Substring(13).Trim().ToLowerInvariant(); cfgCloseAction = (v == "tray" || v == "exit") ? v : (v == "ask" ? "ask" : ""); }   // v2.7 GUI 关闭行为记忆（非法值一律退回"未询问"）
                if (t.StartsWith("auto_start=")) { string v = t.Substring(11).Trim().ToLowerInvariant(); if (v.Length > 0) cfgAutoStart = v != "off"; }   // v2.7 CLI 倒计时开关
            }
        }
        catch { }
    }


    static void SaveConfig()
    {
        try
        {
            string v = lang == Lang.Zh ? "zh" : (lang == Lang.En ? "en" : "auto");
            File.WriteAllText(ConfigPath(), "lang=" + v + Environment.NewLine + "host=" + webHost + Environment.NewLine + "ws=" + (cfgWs ?? "") + Environment.NewLine + "keep_backups=" + cfgKeep + Environment.NewLine + "check_update=" + (cfgCheckUpdate ? "on" : "off") + Environment.NewLine + "check_dsh_update=" + (cfgCheckDshUpdate ? "on" : "off") + Environment.NewLine + "dsh_versions=" + cfgDshVersions + Environment.NewLine + "update_channel=" + cfgChannel + Environment.NewLine + "close_action=" + cfgCloseAction + Environment.NewLine + "auto_start=" + (cfgAutoStart ? "on" : "off") + Environment.NewLine, new UTF8Encoding(false));
        }
        catch { }
    }

    // ---------------- 通用工具 ----------------


    /// <summary>config-set 校验（供单测）：白名单键 + 值域；返回 null=通过，否则原因键（no-key/unknown-key/bad-value）。</summary>
    static string NIValidateConfigSet(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key)) return "no-key";
        string k = key.Trim().ToLowerInvariant();
        string v = (value ?? "").Trim();
        if (k == "lang") { if (v == "zh" || v == "en" || v == "auto" || v == "") return null; return "bad-value"; }
        if (k == "host") { if (v == "127.0.0.1" || v == "localhost") return null; return "bad-value"; }
        if (k == "ws") { if (v.Length == 0) return null; try { Path.GetFullPath(v); return null; } catch { return "bad-value"; } }
        if (k == "keep_backups") { int n; if (int.TryParse(v, out n) && n >= 3) return null; return "bad-value"; }
        if (k == "check_update" || k == "check_dsh_update") { if (v == "on" || v == "off") return null; return "bad-value"; }
        if (k == "update_channel") { if (v == "stable" || v == "rc") return null; return "bad-value"; }
        if (k == "close_action") { if (v.Length == 0 || v == "ask" || v == "tray" || v == "exit") return null; return "bad-value"; }   // v2.7：空=清除记忆（下次关窗重新询问）
        if (k == "auto_start") { if (v == "on" || v == "off") return null; return "bad-value"; }
        return "unknown-key";
    }


    /// <summary>config-set &lt;key&gt; &lt;value&gt;：白名单内才写盘（SaveConfig），否则 CONFIGSET_FAIL 原因。</summary>
    static void ConfigSet(string[] args)
    {
        string key = args.Length > 1 ? args[1] : "";
        string val = args.Length > 2 ? args[2] : "";
        string reason = NIValidateConfigSet(key, val);
        if (reason != null) { Console.WriteLine("CONFIGSET_FAIL " + reason); return; }
        string k = key.Trim().ToLowerInvariant();
        string v = val.Trim();
        if (k == "lang") lang = v == "zh" ? Lang.Zh : (v == "en" ? Lang.En : Lang.Auto);
        else if (k == "host") webHost = v;
        else if (k == "ws") { try { cfgWs = v.Length > 0 ? Path.GetFullPath(v) : null; } catch { cfgWs = null; } }
        else if (k == "keep_backups") { int n; int.TryParse(v, out n); cfgKeep = n < 3 ? 3 : n; }
        else if (k == "check_update") cfgCheckUpdate = v != "off";
        else if (k == "check_dsh_update") cfgCheckDshUpdate = v != "off";
        else if (k == "update_channel") cfgChannel = v == "rc" ? "rc" : "stable";
        else if (k == "close_action") cfgCloseAction = (v == "tray" || v == "exit") ? v : (v == "ask" ? "ask" : "");
        else if (k == "auto_start") cfgAutoStart = v != "off";
        SaveConfig();
        Console.WriteLine("CONFIGSET_OK " + k);
    }


    /// <summary>config-get：CONFIGGET_OK + 每行 CONFIG &lt;key&gt; &lt;value&gt;（GUI 设置页数据源，只读）。</summary>
    static void ConfigGet()
    {
        Console.WriteLine("CONFIGGET_OK");
        Console.WriteLine("CONFIG lang " + (lang == Lang.Zh ? "zh" : (lang == Lang.En ? "en" : "auto")));
        Console.WriteLine("CONFIG host " + webHost);
        Console.WriteLine("CONFIG ws " + (cfgWs ?? ""));
        Console.WriteLine("CONFIG keep_backups " + cfgKeep);
        Console.WriteLine("CONFIG check_update " + (cfgCheckUpdate ? "on" : "off"));
        Console.WriteLine("CONFIG check_dsh_update " + (cfgCheckDshUpdate ? "on" : "off"));
        Console.WriteLine("CONFIG update_channel " + cfgChannel);
        Console.WriteLine("CONFIG close_action " + cfgCloseAction);
        Console.WriteLine("CONFIG auto_start " + (cfgAutoStart ? "on" : "off"));
        Console.WriteLine("CONFIG dsh_versions " + cfgDshVersions);
    }

    // ---------------- Update Center 数据源（v2.7） ----------------

}
