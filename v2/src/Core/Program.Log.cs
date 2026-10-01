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

    /// <summary>把错误追加写入 StateDir\logs\launcher.log（带时间戳；超过 1MB 归档为 launcher.log.1，不再直接丢弃）。</summary>
    static void LogErr(string msg)
    {
        try
        {
            string dir = Path.Combine(StateDir, "logs");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "launcher.log");
            RotateLogIfNeeded(file, 1024 * 1024);
            File.AppendAllText(file, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + msg + Environment.NewLine, new UTF8Encoding(false));
        }
        catch { }
    }



    /// <summary>日志轮转：现有文件超过 maxBytes 时归档为 file+".1"（覆盖旧归档）并留下新的空文件，返回是否发生轮转。（单测可直接调用）</summary>
    static bool RotateLogIfNeeded(string file, long maxBytes)
    {
        try
        {
            if (File.Exists(file) && new FileInfo(file).Length > maxBytes)
            {
                if (File.Exists(file + ".1")) File.Delete(file + ".1");
                File.Move(file, file + ".1");
                File.WriteAllText(file, "");   // 轮转后留下新的空日志
                return true;
            }
        }
        catch { }
        return false;
    }



    /// <summary>日志摘要：launcher.log 行数 + 最近 3 行。</summary>
    static string LogSummary()
    {
        string file = Path.Combine(StateDir, "logs", "launcher.log");
        try
        {
            if (!File.Exists(file)) return "(无日志)";
            string[] lines = File.ReadAllLines(file);
            string tail = "";
            for (int i = Math.Max(0, lines.Length - 3); i < lines.Length; i++)
                tail += (tail.Length == 0 ? "" : " | ") + lines[i];
            return "共 " + lines.Length + " 行；最近: " + tail;
        }
        catch (Exception ex) { return "读取失败: " + ex.Message; }
    }


}
