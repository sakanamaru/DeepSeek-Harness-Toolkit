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

    static string T(string zh, string en) { return IsZh ? zh : en; }

    // ---------------- 彩色输出 ----------------


    static void Info(string s)    { C(ConsoleColor.Cyan, "  " + s + Environment.NewLine); }

    static void Success(string s) { C(ConsoleColor.Green, "  ✓ " + s + Environment.NewLine); }

    static void Warn(string s)    { C(ConsoleColor.Yellow, "  [!] " + s + Environment.NewLine); }

    static void Error(string s)   { C(ConsoleColor.Red, "  [x] " + s + Environment.NewLine); LogErr(s); }

    // ---------------- 错误日志 ----------------


    /// <summary>加 \\?\ 前缀绕过 260 字符限制（配合 Main 里的长路径开关，兼容无注册表策略的机器）。</summary>
    static string P(string p)   // 统一转 \\?\ 前缀（UNC 走 \\?\UNC\，否则 System.IO 对长路径会失败）
    {
        if (string.IsNullOrEmpty(p)) return p;
        if (p.StartsWith(@"\\?\")) return p;
        if (p.StartsWith(@"\\")) return @"\\?\UNC\" + p.Substring(2);   // \\server\share → \\?\UNC\server\share
        return @"\\?\" + p;
    }


    static string TrimP(string p)   // \\?\ 前缀还原（UNC 还原为 \\server\share）
    {
        if (string.IsNullOrEmpty(p)) return p;
        if (p.StartsWith(@"\\?\UNC")) return @"\\" + p.Substring(8);
        if (p.StartsWith(@"\\?\")) return p.Substring(4);
        return p;
    }


    static string ReadLineTrim()
    {
        if (inputEof) return "";
        string s = null;
        try { s = Console.ReadLine(); } catch { }
        if (s == null) { inputEof = true; return ""; }
        return s.Trim();
    }

    // ---------------- 桌面快捷方式 ----------------


    /// <summary>快捷方式基名净化：只取文件名部分、剔除非法字符（含路径分隔符，防目录穿越），最长 80 字符。
    /// 纯函数，可单测。返回空串表示名字非法（调用方应拒绝创建，而不是写一个怪名字）。</summary>
    static string SafeShortcutName(string raw)
    {
        string s = (raw ?? "").Trim();
        if (s.Length == 0) return "";
        // 手工取最后一段，避免 Path.GetFileName 对非法字符抛异常
        int cut = s.LastIndexOfAny(new char[] { '\\', '/' });
        if (cut >= 0 && cut + 1 < s.Length) s = s.Substring(cut + 1);
        StringBuilder sb = new StringBuilder();
        foreach (char c in s)
        {
            if (c < 32) continue;
            if ("\\/:*?\"<>|".IndexOf(c) >= 0) continue;
            sb.Append(c);
        }
        string n = sb.ToString().Trim().Trim('.');
        return n.Length > 80 ? n.Substring(0, 80) : n;
    }


    /// <summary>运行时长格式化（单测覆盖）：&lt;60 秒报秒；&lt;60 分报分；&lt;24 小时报"时 分"；再长报"天 时"。</summary>
    static string FormatUptime(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;                       // 时钟回拨保护：不显示负数时长
        if (t.TotalSeconds < 60) return ((int)t.TotalSeconds) + " 秒";
        if (t.TotalMinutes < 60) return ((int)t.TotalMinutes) + " 分";
        if (t.TotalHours < 24) return ((int)t.TotalHours) + " 小时 " + t.Minutes + " 分";
        return ((int)t.TotalDays) + " 天 " + (t.Hours) + " 小时";
    }

    // ---------------- dsh 更新管理 ----------------


    /// <summary>递归收集 相对路径→大小。copyRules=true 复现 CopyTree 跳过规则（node_modules/backup/dsh-data-*）；
    /// reparse point 一律跳过且不进入（与备份/恢复执行侧一致）。</summary>
    static bool IsReparse(string path) { try { return (File.GetAttributes(P(path)) & FileAttributes.ReparsePoint) != 0; } catch { return false; } }


    /// <summary>剪切过长文本（诊断输出用）。</summary>
    static string ClipText(string s, int n)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= n ? s : s.Substring(0, n) + "…";
    }


    static void Pause()
    {
        Console.WriteLine();
        C(ConsoleColor.DarkGray, T("  按任意键继续...", "  Press any key to continue..."));
        try { Console.ReadKey(true); } catch { }
        Console.WriteLine();
    }

}
