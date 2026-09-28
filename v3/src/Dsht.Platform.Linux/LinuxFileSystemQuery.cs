using System;
using System.Collections.Generic;
using System.IO;
using Dsht.Domain.Abstractions;

namespace Dsht.Platform.Linux
{
    /// <summary>只读文件系统查询（Linux）：与 Windows 实现同语义（DirSize 迭代栈累加）。</summary>
    public sealed class LinuxFileSystemQuery : IFileSystemQuery
    {
        public bool DirectoryExists(string path)
        {
            try { return !string.IsNullOrEmpty(path) && Directory.Exists(path); } catch { return false; }
        }

        public bool CanEnumerate(string path)
        {
            try { Directory.GetFiles(path); return true; } catch { return false; }
        }

        public long DirSize(string path)
        {
            long total = 0;
            try
            {
                Stack<string> stack = new Stack<string>();
                stack.Push(path);
                while (stack.Count > 0)
                {
                    string d = stack.Pop();
                    string[] files;
                    try { files = Directory.GetFiles(d); } catch { files = new string[0]; }
                    foreach (string f in files) { try { total += new FileInfo(f).Length; } catch { } }
                    string[] subs;
                    try { subs = Directory.GetDirectories(d); } catch { subs = new string[0]; }
                    foreach (string sd in subs) stack.Push(sd);
                }
            }
            catch { }
            return total;
        }
    }
}