using System;
using System.Collections.Generic;

namespace Dsht.Platform.Shared
{
    /// <summary>两端备份实现逐字相同的那部分（架构审计 S2）。
    /// 动机：Windows 与 Linux 的实现有 310 行逐行相同（占 65%），一处修 bug 必须记得改两处，
    ///   而这已经出过问题（归一化修复只做在一端）。
    /// 做法：这些方法在两端逐字相同且全是 static，不可能依赖实例状态，
    ///   于是提成基类的 protected static，两端调用点一行都不用改。
    /// 链接：两个 csproj 用 Compile Include + Link 指向同一份源文件（与 MarkerText.cs 同一手法），
    ///   没有新程序集、没有新依赖。
    /// 纪律：每搬一块就编译 + 跑真实往返测试 + 11 项门槛。</summary>
    public abstract class BackupSourceCommon
    {
        /// <summary>复制备份包的同级旁挂文件（.manifest / .version）：存在才复制，尽力而为。
        /// 不这么做的话，export 之后标记就丢了，包到了别处无法核对完整性。</summary>
        protected static void CopySibling(string srcPkg, string dstPkg, string suffix)
        {
            try
            {
                string from = srcPkg.TrimEnd('\\', '/') + suffix;
                if (!System.IO.File.Exists(from)) return;
                System.IO.File.Copy(from, dstPkg.TrimEnd('\\', '/') + suffix, true);
            }
            catch { }
        }
    }
}