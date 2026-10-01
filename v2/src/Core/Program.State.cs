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



    enum Lang { Auto, Zh, En }




    const string NPM_MIRROR   = "https://registry.npmmirror.com";
    const string NPM_OFFICIAL = "https://registry.npmjs.org";
    const string GITHUB_HANDLE = "github.com/sakanamaru";



    const string DATA_DIR     = ".dsh";



    const string ROOT_MARKER  = ".dsh_launcher_root";   // 工具箱根目录标记文件（防误删验证；随包分发）



    const int    WEB_PORT     = 3080;



    static string webHost = "127.0.0.1";



    const int    AUTO_SECONDS = 5;




    static Lang lang = Lang.Auto;



    static string StateDir;



    static bool autoApplied = false;   // 自动倒计时是否已在本程序本次运行中用过



    public enum ServiceState { Down, Listening, Ready }




    // 监听进程身份判定缓存（同一进程内 10 秒内复用）：状态页每 3 秒刷新一次，
    // 不做缓存就会每轮都拉起 netstat + WMI。
    static bool listenerIsDshCached = false;



    static DateTime listenerIsDshAt = DateTime.MinValue;




    static Func<string, int, string> HttpGetImpl = null;   // 单测注入点（为空走真实实现）




    static bool inputEof = false;



    /// <summary>核心（CLI）桌面快捷方式基名。</summary>
    const string SHORTCUT_NAME = "DeepSeek Harness Toolkit";



}
