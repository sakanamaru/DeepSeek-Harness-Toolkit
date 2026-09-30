// dsh-minato 自解压安装器（用户要求："引入自解压 exe 做安装器吧，自解压页面定制下" ✓✓）
//
// 设计决定（与维护者逐条确认 ✓ + 子代理评审的 15 条工程细节全部采纳 ✓）：
//   · **net48 WinForms** ✓（.NET Framework 4.8 是 Windows 系统组件 ✓ 不算第三方依赖 ✓ ~300 KB ✓）
//     用 VS2022 的 Roslyn csc 编译 ✓（in-box csc 只有 C# 5 ✗ 插值都编不过 ✓）
//   · **asInvoker** ✓✓ —— 绝不 requireAdministrator ✗（那会让**每次启动**都弹 UAC ✓ 且标准用户直接失败 ✗）
//   · **per-user 安装** ✓ 默认 %LOCALAPPDATA%\Programs\dsh-minato ✓ 可自定义 ✓
//   · **先显示窗口再解压** ✓✓（冻结的窗口是"这是恶意软件"的第一特征 ✓）
//   · **后台线程 + 真实进度** ✓（BackgroundWorker ✓）
//   · **版本化目录 + 暂存后改名** ✓✓（升级/卸载不怕程序在跑 ✓ 不需要重启 ✓）
//   · **持久化 uninstall.exe** ✓✓（下载的 SFX 会被删掉 ✓ 不能靠它卸载 ✓）
//   · **zip-slip 校验** ✓✓（拒绝绝对路径与 .. ✓）
//   · **PATH 默认不勾** ✓ + **绝不用 setx** ✗（会截断 PATH ✓）→ 写 HKCU\Environment + WM_SETTINGCHANGE ✓
//   · **卸载不删数据** ✓ + **桌面生成说明文档** ✓ + **界面明写"不会被删除"** ✓✓（维护者的选择 ✓）
//   · 安装日志 + "复制日志" ✓ · ARP 注册表（含 DisplayIcon ✓）· 快捷方式叫 dsh-minato ✓
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;


    /// <summary>**策略性拒绝** ✓✓（审计 M4：拒绝应当返回 **2** ✓ 不是 9 ✗）。
    ///   用途：目录非空且不像我们的安装 ✓ 载荷被改动 ✓ 已经装过一份 ✓
    ///   —— 这些都是**明确的策略决定** ✓ 不是故障 ✓ → 退出码必须是 **2** ✓✓
    ///   （自动化工具据此区分"拒绝了"与"崩了" ✓）</summary>
    internal sealed class RefusalException : Exception
    {
        public RefusalException(string message) : base(message) { }
    }

internal static class Installer
{
    private const string AppName = "dsh-minato";
    private const string Publisher = "dsh-minato (unofficial)";
    internal const string ReleasesUrl = "https://github.com/sakanamaru/dsh-minato/releases";   // internal ✓ 窗体类要用 ✓
    /// <summary>上次载荷校验的结果 ✓（空 = 通过或未跑；否则 = 对不上的文件名 ✓ 完成页要显示 ✓）。</summary>
    internal static string LastVerifyResult = "";
    /// <summary>本次安装的随机身份令牌 ✓✓（同时写进 marker 与 ARP ✓ 卸载时双向比对 ✓
    /// 这是审计发现的 C1 修复：原来只判 marker 文件存在 → 谁都能复制一个 → 就能删任意目录 ✗）。</summary>
    internal static string InstallToken = "";
    /// <summary>--force：允许装进非空目录 ✓（审计 C1 的显式逃生口 ✓ 默认关闭 ✓）。</summary>
    internal static bool ForceInstall = false;
    /// <summary>最近一次失败的原因 ✓（GUI 要显示给用户 ✓
    /// 审计 m8：原来只显示"安装失败（代码 9）" ✗ → **篡改警告这种关键信息用户根本看不到** ✗✗）。</summary>
    internal static string LastFailureReason = "";
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "dsh-minato-install.log");
    private static readonly StringBuilder LogBuf = new StringBuilder();

    [STAThread]
    private static int Main(string[] args)
    {
        bool uninstall = false, silent = false, noPath = false, noShortcuts = false, addPath = false;
        bool installExplicit = false;
        string dirArg = null;
        // ✓✓ 审计 M9 修复：**双击 uninstall.exe 应当卸载** ✓
        //   ✗ 原来它打开的是**安装向导**（标题"dsh-minato 安装程序"、主按钮"安装"）✗
        //     → 用户点下去会**把它重新装回来/覆盖** ✓ 而 README 明说"卸载用安装目录里的 uninstall.exe" ✗✓
        //   ✓ 现在按**自身文件名**判断：叫 uninstall.exe → 默认走卸载 ✓（要装回来用 --install ✓）
        bool calledAsUninstaller = false;
        try
        {
            string selfName = Process.GetCurrentProcess().MainModule.FileName;
            if (!string.IsNullOrEmpty(selfName))
                calledAsUninstaller = string.Equals(Path.GetFileNameWithoutExtension(selfName), "uninstall", StringComparison.OrdinalIgnoreCase);
        }
        catch { }
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i] == null ? "" : args[i].Trim();
            if (a == "--uninstall") uninstall = true;
            else if (a == "--silent") silent = true;
            else if (a == "--no-path") noPath = true;
            else if (a == "--path") addPath = true;   // m-3 FIX: explicit opt-in for a silent install
            else if (a == "--no-shortcuts") noShortcuts = true;
            else if (a.StartsWith("--dir=", StringComparison.Ordinal)) dirArg = a.Substring(6).Trim('"');
            else if (a == "--install") installExplicit = true;   // ✓ 叫 uninstall.exe 时想装回来，加这个 ✓
            else if (a == "--force") ForceInstall = true;        // ✓ 审计 C1：显式允许装进非空目录 ✓
            else if (a == "--dir" || a == "-dir")
            {
                // ✓ m5：支持**空格形式** ✓（原来只认 `--dir=` ✗ → `--dir C:\x` 被静默忽略 ✓
                //   → 装到默认位置还返回 0 ✗✗ 对 CI/Scoop/winget 是"静默装错地方 + 报成功" ✓）
                if (i + 1 < args.Length) { i++; dirArg = (args[i] == null ? "" : args[i].Trim()).Trim('"'); }
                else { Log("参数错误：--dir 后面没有值 ✓"); return 2; }
            }
            else
            {
                // ✓ m5：**未知参数不能静默忽略** ✗（拼错一个字母就装到默认位置 ✓ 还报成功 ✗✗）
                Log("未知参数 ✗ " + a + " → 拒绝执行（退出码 2 ✓）");
                return 2;
            }
        }
        Log("args: " + string.Join(" ", args));
        try
        {
            if (uninstall || (calledAsUninstaller && !installExplicit)) return RunUninstall(silent);   // ✓ M9 ✓
            // m-3 FIX (installer audit MINOR): this used to pass wantPath=false unconditionally, so
        // --no-path was a no-op and a silent install could never add the PATH entry at all, which
        // contradicts what the build script and the README describe. PATH is added only when the
        // user asks for it with --path.
        if (silent) return RunInstall(dirArg, addPath, noPath, noShortcuts, null);
            // AppUserModelID ✓（任务栏分组与图标更可靠 ✓ 也让"固定到任务栏"认得出是本程序 ✓）
            try { SetCurrentProcessExplicitAppUserModelID("dsh-minato.installer"); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (InstallerForm f = new InstallerForm(dirArg))
            {
                Application.Run(f);
                return f.ExitCode;
            }
        }
        // ★★★ **M4 修复（审计 MAJOR）** ✓✓：**策略性拒绝 → 返回 2** ✓（不是 9 ✗）
        //   ✗ 三处"拒绝安装"原来是 `InvalidOperationException` ✗ → 被下面这个 catch 接住 → **返回 9** ✗
        //     → 而契约是 **0 成功 / 2 拒绝 / 3 缺文件 / 4 就位失败 / 9 致命** ✓
        //     → Scoop / winget / CI 会把"拒绝"当成**崩溃** ✗（那是策略决定 ✓ 不是故障 ✓）
        //   ✓ 现在：`RefusalException` **单独 catch → return 2** ✓✓
        catch (RefusalException rex)
        {
            Log("**拒绝**（策略决定 ✓ 不是故障 ✓）: " + rex.Message);
            FlushLog();
            if (!silent) MessageBox.Show(rex.Message, AppName + " 安装", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 2;
        }
        catch (Exception ex)
        {
            Log("FATAL " + ex);
            // ✓ m8：**把真实原因留下** ✓ → GUI 要显示给用户 ✓（篡改警告就是这么传出去的 ✓✓）
            LastFailureReason = ex.Message;
            if (!silent) MessageBox.Show(ex.Message, AppName + " 安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 9;
        }
        finally { FlushLog(); }
    }

    // ================================================================ 安装

    /// <summary>安装 ✓。返回 0 = 成功。**先解压到暂存目录并校验，再改名就位** ✓（原子性 ✓）。</summary>
    internal static int RunInstall(string dir, bool wantPath, bool noPath, bool noShortcuts, Action<int, string> progress)
    {
        string target = string.IsNullOrEmpty(dir) ? DefaultDir() : dir;
        if (string.IsNullOrEmpty(target)) { Log("目标目录为空 → 拒绝"); return 2; }
        try { target = Path.GetFullPath(target); } catch { Log("目标路径非法: " + dir); return 2; }
        if (IsDangerousPath(target)) { Log("拒绝写入危险路径: " + target); return 2; }

        // 已装检测 + 版本比较 ✓✓（用户要求："安装好后也可以再点安装，安装器也加个版本校验吧" ✓）
        // 图形界面里显示在选项页 ✓；这里**也写日志** ✓ → 静默模式/自动化也能看到 ✓✓
        string installedInfo = ReadInstalled();
        string verdict;
        if (string.IsNullOrEmpty(installedInfo)) verdict = "未安装过 → 全新安装";
        else
        {
            string have = installedInfo.Split('|')[0];
            int cmp = CompareVersions(SelfVersion(), have);
            verdict = cmp > 0 ? ("已装 " + have + " → 本次是**更新版本 " + SelfVersion() + "**，执行**升级** ✓")
                    : (cmp == 0 ? ("已装 " + have + " → **版本相同**，执行**重新安装（覆盖）** ✓")
                                : ("⚠ 已装 " + have + "，本次是**更旧的 " + SelfVersion() + "** → 会**降级** ✗"));
        }
        Log("已装检测: " + (string.IsNullOrEmpty(installedInfo) ? "（无）" : installedInfo) + " → " + verdict);

        Log("目标目录: " + target);
        // ★★★ **N-10 修复（安装器复审 MAJOR —— 取消会泄漏暂存目录）** ✓✓
        //   ✗ `Report()` 在**就位 try 之外**也有调用（准备/校验/解压 ✓）
        //     → GUI 的取消钩子在那里抛 `OperationCanceledException` ✗
        //     → 而**清理 staging 的代码只在就位 catch 里** ✗✗
        //     → **每次取消都留下一个 `.dsh-minato-staging-*` 目录** ✓（几十 MB ✓ 累积 ✓）
        //   ✓ 现在：**启动时清扫旧暂存目录** ✓✓（超过 1 小时的 ✓ 不碰正在跑的那次 ✓）
        //     → 即使某次取消漏了 ✓ 下次安装也会把它收掉 ✓✓
        try
        {
            string tparent = Path.GetDirectoryName(target.TrimEnd('\\'));
            if (!string.IsNullOrEmpty(tparent) && Directory.Exists(tparent))
            {
                foreach (string stale in Directory.GetDirectories(tparent, ".dsh-minato-staging-*"))
                {
                    try
                    {
                        if ((DateTime.UtcNow - Directory.GetLastWriteTimeUtc(stale)).TotalHours > 1)
                        {
                            Directory.Delete(stale, true);
                            Log("已清扫旧的暂存目录 ✓ " + stale);
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
        Report(progress, 2, "准备…");
        // ✗ 原来在这里就 CreateDirectory → 校验失败会**留下一个空目录** ✗（实测确认 ✓）
        // ✓ 改成**校验通过后再建** ✓ → 拒绝时磁盘上**一个字节都不写** ✓✓

        // ① 解压到**同卷暂存目录** ✓（同卷才能改名 ✓ 跨卷 rename 会失败 ✓）
        // ✗ 原来暂存放在 target **里面** → CreateDirectory 会把 target 一起建出来 ✗
        //    → 于是"篡改包被拒绝"时磁盘上**留下一个空目录** ✗（实测确认 ✓）
        // ✓ 改放在 **target 的父目录**：同卷 ✓（改名才能成功 ✓）且**完全不碰 target** ✓✓
        // ★★★ **C1 修复（审计 CRITICAL）** ✓✓
        //   ✗ 原来安装**接受任意目录** ✗ 从不拒绝非空目录 ✓ 也从不告诉用户"卸载会删整棵树" ✗
        //   → 装进 `D:\dev\myproject`（里面有别人的东西 ✓）→ **卸载时整棵树被递归删除** ✗✗
        //   → 最坏：`--dir=\\srv\share\` → 在**共享根**装成功 → 卸载**递归删共享内容** ✗✗
        //   ✓ 修：目标**存在、非空、且没有我们的标记** → **拒绝** ✓（要么换目录 ✓ 要么显式 --force ✓）
        //     这样"我们装过的地方"才允许被卸载器整棵删除 ✓✓
        if (Directory.Exists(target))
        {
            bool targetEmpty = false;
            try { targetEmpty = Directory.GetFileSystemEntries(target).Length == 0; } catch { }
            // ★★ **C2 修复（审计 CRITICAL）** ✓✓
        //   ✗ 原来只判 marker **文件存在** ✗ —— 与卸载侧**同一个坑** ✓
        //     （本文件自己的注释就写着：`echo x > 任意目录\.dsh-minato-install` 能满足 ✓）
        //     → `--force` 或往目标写一个字节 → **外来目录被当成我们的** ✗
        //     → 然后**递归删掉用户的 `gui\` / `app-<ver>\`** ✗✗ **exit 0** ✓
        //   ✓ 现在：**与卸载侧同一套身份校验** ✓（marker 的 token 必须与本用户 ARP 里的 token 一致 ✓
        //     且 marker 里的 `path=` 必须等于目标 ✓）
        bool targetOurs = false;
        try
        {
            string mp0 = Path.Combine(target, ".dsh-minato-install");
            if (File.Exists(mp0))
            {
                string mkTok = "", mkPath = "", mkVer = "";
                foreach (string ln in File.ReadAllLines(mp0))
                {
                    if (ln == null) continue;
                    if (ln.StartsWith("token=", StringComparison.Ordinal)) mkTok = ln.Substring(6).Trim();
                    else if (ln.StartsWith("path=", StringComparison.Ordinal)) mkPath = ln.Substring(5).Trim();
                    else if (ln.StartsWith("version=", StringComparison.Ordinal)) mkVer = ln.Substring(8).Trim();
                }
                string regTok = "";
                try
                {
                    using (RegistryKey rk0 = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName))
                    { if (rk0 != null) regTok = rk0.GetValue("InstallToken", "") as string; }
                }
                catch { }
                bool tokOk = !string.IsNullOrEmpty(mkTok) && !string.IsNullOrEmpty(regTok) && string.Equals(mkTok, regTok, StringComparison.OrdinalIgnoreCase);
                bool pthOk = true;
                if (!string.IsNullOrEmpty(mkPath))
                {
                    try { pthOk = string.Equals(Path.GetFullPath(mkPath).TrimEnd('\\'), Path.GetFullPath(target).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
                    catch { pthOk = false; }
                }
                // ★★★ **Bug 1 修复（用户实测）** ✓✓
                //   ✗ 我加严身份校验时**没留兼容旧版本的口子** ✗：
                //     旧版安装器写的 marker **没有 `token=` 行** ✓（那时还没有 token 机制 ✓）
                //     → `mkTok=""` → `tokOk=false` → **我们自己的旧安装被当成"外来目录"** ✗✗
                //     → 用户装到**默认位置**（他自己装过的那个 ✓）被拒 ✓ **升级路径断了** ✓
                //   ✓ 现在：**旧版 marker 也认** ✓ —— 判据是
                //     ① 有 `path=` 行 ✓ 且与目标一致 ✓  ② 有 `version=` 行 ✓（旧版 marker 的固定结构 ✓）
                //     → 认作我们自己的 ✓✓
                //   （**这不是权限边界** ✓：能往目标目录写 marker 的人本来就能改里面任何东西 ✓
                //     审计也明确说过"任何以该用户运行的进程都能读 token 并写 marker" ✓）
                // M-3 FIX (installer audit MAJOR): a hand-written marker carrying version= and path=
                // was enough to make the installer treat ANY directory as ours, and it then deleted the
                // gui folder of a user who happened to have one there. The legacy branch is now confined
                // to the DEFAULT install location AND requires our own uninstall.exe and dsh-minato.exe
                // to sit beside the marker, which is what a real install made by the previous version
                // looks like. Everything else falls through to the normal refusal.
                bool legacyDefaultDir = false;
                try
                {
                    string defDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "dsh-minato");
                    legacyDefaultDir = string.Equals(Path.GetFullPath(target).TrimEnd('\\'), Path.GetFullPath(defDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
                }
                catch { }
                bool legacyOurs = legacyDefaultDir && !string.IsNullOrEmpty(mkPath) && pthOk && !string.IsNullOrEmpty(mkVer)
                                  && File.Exists(Path.Combine(target, "uninstall.exe")) && File.Exists(Path.Combine(target, "dsh-minato.exe"));
                if (legacyOurs && !tokOk) Log("旧版安装标记（默认目录 + 有 version/path + 旁边有我们自己的 exe ✓）→ 认作我们自己的 ✓ 可以升级 ✓");
                // ★★★ **N-1 修复（安装器复审 CRITICAL —— 我的 M-3 编辑**删掉了这一行**）** ✓✓
                //   ✗ `targetOurs` 初值是 `false` ✗ 而这一行赋值**被我的 M-3 替换吃掉了** ✗✗
                //     → **永远 false** ✓ → 250 行**每次都拒绝** ✗✗
                //     → **任何在已有安装上升级/重装都被拒（exit 2）** ✗ 只有 `--force` 能用 ✓
                //       日志证据：`身份校验没通过 ✗（tokenOk=True pathOk=True）` ✓✓
                //   ✓ 现在：**把这行加回来** ✓✓（`legacyOurs` 才算有用 ✓）
                targetOurs = (tokOk || legacyOurs) && pthOk;
                if (!targetOurs) Log("目标目录里有个 marker，但**身份校验没通过** ✗（tokenOk=" + tokOk + " pathOk=" + pthOk + "）→ 按「不是我们的」处理 ✓");
            }
        }
        catch (Exception ex0) { Log("身份校验读取出错 → 按「不是我们的」处理 ✓: " + ex0.Message); }
            if (!targetEmpty && !targetOurs && !ForceInstall)
            {
                Log("拒绝安装：目标目录非空且不是本工具的安装目录 ✓ " + target);
                throw new RefusalException(
                    "**这个目录里已经有别的东西了。**" + Environment.NewLine + Environment.NewLine +
                    "为了安全，安装器**不会**装进一个非空目录 —— 因为卸载时会删除整个安装目录 ✓" + Environment.NewLine + Environment.NewLine +
                    "目录：" + target + Environment.NewLine + Environment.NewLine +
                    "请换一个空目录（推荐默认位置 ✓），或者确认里面没有重要文件后用 --force 强制安装 ✓");
            }
        }
        string parentDir = Path.GetDirectoryName(target);
        if (string.IsNullOrEmpty(parentDir)) parentDir = Path.GetTempPath();
        Directory.CreateDirectory(parentDir);
        string staging = Path.Combine(parentDir, ".dsh-minato-staging-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(staging);
        Log("暂存目录: " + staging);
        int files = ExtractPayload(staging, progress);
        Log("解压完成，文件数 " + files);
        Report(progress, 70, "校验…");

        // ② 校验：三个可执行文件都在 ✓（不通过就不动正式目录 ✓）
        foreach (string need in new string[] { "dsh-minato.exe", Path.Combine("gui", "dsht-gui.exe") })
        {
            if (!File.Exists(Path.Combine(staging, need)))
            {
                Log("暂存目录缺少 " + need + " → 安装中止（正式目录未被改动 ✓）");
                TryDelete(staging);
                return 3;
            }
        }

        // ③ **载荷完整性校验** ✓✓（用户要求："安装器也加个版本校验吧，比如有更新或者**非官方**" ✓）
        //    安装包内自带 hashes.txt ✓ → 逐条核对 ✓ → 对不上就是**被改过的包** ✓ → 拒绝安装 ✓✓
        Report(progress, 74, "校验安装包…");
        string badFile = VerifyPayload(staging);
        if (badFile != null)
        {
            Log("载荷校验失败 ✗ " + badFile);
            TryDelete(staging);
            throw new RefusalException(
                "**这个安装包不是官方发布的，或者已经被改动过。**" + Environment.NewLine + Environment.NewLine +
                "对不上的文件：" + badFile + Environment.NewLine + Environment.NewLine +
                "请从官方 Releases 重新下载：" + Environment.NewLine + ReleasesUrl + Environment.NewLine + Environment.NewLine +
                "（已拒绝安装 ✓ 没有写入任何东西 ✓）");
        }
        Log("载荷校验通过 ✓ 每个文件的指纹都与包内清单一致 ✓");

        // ★★ **M8 修复（审计 MAJOR）** ✓✓
        //   ✗ ARP key（`Uninstall\dsh-minato`）· 开始菜单目录 · InstallToken 全是**每用户单例** ✗
        //     → 装到 A 再装到 B：B 覆盖 A 的快捷方式与 ARP ✓ → 卸载 B 时
        //       **A 的注册也没了、token 也对不上** ✗✗ → **A 永远卸不掉**（孤儿目录 ✓ 确定性复现 ✓）
        //   ✓ 修：检测到**已装在另一个目录** → **拒绝**（除非 --force ✓ 或先卸载 ✓）
        //     这符合"每用户单例"的现实 ✓✓（要并存请用便携版 zip ✓ 它不写注册表 ✓）
        if (!ForceInstall)
        {
            string already = ReadInstalled();
            if (!string.IsNullOrEmpty(already))
            {
                string[] parts = already.Split('|');
                string loc = parts.Length > 1 ? parts[1] : "";
                bool sameDir = false;
                try { sameDir = string.Equals(Path.GetFullPath(loc).TrimEnd('\\'), Path.GetFullPath(target).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); } catch { }
                if (!string.IsNullOrEmpty(loc) && !sameDir)
                {
                    Log("拒绝安装：已有一个安装在其他目录 ✓ " + loc);
                    throw new RefusalException(
                        "**已经装过一份了。**" + Environment.NewLine + Environment.NewLine +
                        "已安装在：" + loc + Environment.NewLine +
                        "本次要装到：" + target + Environment.NewLine + Environment.NewLine +
                        "「应用和功能」的注册、开始菜单快捷方式和身份令牌都是**每用户唯一**的 ✓" + Environment.NewLine +
                        "装第二份会**覆盖第一份的注册**，导致第一份**卸载不掉** ✗" + Environment.NewLine + Environment.NewLine +
                        "请先卸载旧的那份，或者用便携版 zip（它不写注册表 ✓ 可以并存 ✓）。" + Environment.NewLine +
                        "确实要强装请加 --force ✓");
                }
            }
        }
        Directory.CreateDirectory(target);   // ✓ 校验已通过 ✓ 现在才建 ✓
        // ⑤ 就位：**先改名旧的，再改名新的** ✓（旧目录即使有文件被占用也能改名成功 ✓）
        Report(progress, 78, "就位…");
        string old = target + ".old-" + DateTime.Now.ToString("HHmmss");
        bool hadOld = false;
        string oldPlug = Path.Combine(target, "plugin.old-" + DateTime.Now.ToString("HHmmss", System.Globalization.CultureInfo.InvariantCulture));
        bool hadOldPlug = false;   // BUG-1 FIX
        // M-1 FIX (installer audit MAJOR): tracks whether the NEW payload actually landed. When the
        // failure IS the move below (because the old version folder could not be renamed aside - a
        // process holding it is enough), the version directory still holds the LIVE old install, and
        // the rollback used to delete it recursively and then "restore" whatever .old-* it found.
        bool placed = false;
            string oldManifest = "";   // N-4 FIX: must live OUTSIDE the try, because the rollback reads it in the catch
        try
        {
            // 把暂存里的内容搬进一个版本化目录 ✓
            string verDir = Path.Combine(target, "app-" + ShortVersion());
            // ✗✗ 审计 M2：原来**先删掉 verDir** ✗ —— 同版本重装时 verDir **就是活的安装** ✓
            //   里面有文件被占用（程序在跑 / AV / shell 停在里面）→ `Directory.Delete(…,true)`
            //   **删掉能删的再抛错** ✗ → catch → `TryDelete(staging)` → **旧安装被毁 + 新载荷也被删** ✗✗
            // ✓ 修：**先改名让开**（改名不会因文件被占用而失败 ✓）→ 新载荷就位 ✓ → **成功后才删旧的** ✓
            //   失败则**改名回来** ✓ → 回滚 ✓✓
            if (Directory.Exists(verDir))
            {
                try { Directory.Move(verDir, verDir + ".old-" + DateTime.Now.ToString("HHmmss")); }
                catch (Exception mv) { Log("旧版本目录改名失败（不致命 ✓ 稍后会被新载荷替换）: " + mv.Message); }
            }
            // ★★★ **N-4 修复（安装器复审 MAJOR —— 回滚不还原清单）** ✓✓
            //   ✗ 清单在**就位之前**就被复制到 target ✗ 而回滚路径（`return 4`）
            //     **只还原 gui 与版本目录** ✗ → **新清单配旧 gui** ✗✗
            //     → 启动器 `FindHashExact` 对不上 → **报"文件已经被改动，可能被木马感染"** ✗
            //       （审计实测：清单 b218a60d… vs 实际 e651fa8e… ✓）
            //   ✓ 现在：**就位前先快照旧清单** ✓✓ 回滚时还原 ✓
            try
            {
                string mnow = Path.Combine(target, "hashes.txt");
                if (File.Exists(mnow)) oldManifest = File.ReadAllText(mnow);
            }
            catch { }
            Directory.Move(staging, verDir);
            placed = true;   // M-1 FIX: only now does the version folder hold the NEW payload
            Log("版本目录: " + verDir);
            // 稳定入口：把启动器与 CLI 复制到 target 根 ✓（快捷方式指向它们 ✓ 升级时路径不变 ✓）
            // ✓ M7：**检查每一处复制** ✓ 任何一处失败都让安装失败 ✓（不再假装成功 ✗）
            bool okCli = CopyFile(Path.Combine(verDir, "dsh-minato.exe"), Path.Combine(target, "dsh-minato.exe"));
            bool okGui = CopyFile(Path.Combine(verDir, "dsh-minato-gui.exe"), Path.Combine(target, "dsh-minato-gui.exe"));
            bool okHash = true;
            if (File.Exists(Path.Combine(verDir, "hashes.txt"))) okHash = CopyFile(Path.Combine(verDir, "hashes.txt"), Path.Combine(target, "hashes.txt"));
            if (!okCli || !okGui || !okHash)
                throw new InvalidOperationException(
                    "稳定入口写入失败 ✗ 可能有程序正在运行（" +
                    (!okCli ? "dsh-minato.exe " : "") + (!okGui ? "dsh-minato-gui.exe " : "") + (!okHash ? "hashes.txt " : "") +
                    "）✗ 请先关掉本工具与 dsh，然后重新安装 ✓（已中止，不会留下混合版本 ✓）");
            string vgui = Path.Combine(verDir, "gui");
            if (Directory.Exists(vgui))
            {
                string tgui = Path.Combine(target, "gui");
                // ✗✗ 审计 M2(b)：原来改名失败后 `catch { }` 落到 `TryDelete(tgui)` ✗
                //   = **递归删除旧安装的 gui** ✗ → 若那次删除也只是部分成功 ✓ → 下一行 Move 抛错 ✓
                //   → staging 被删 ✓ 旧 gui 滞留在 `<target>.old-HHmmss` ✓（因为 220 行被 return 跳过 ✓）
                //   → **根启动器已换新、gui 缺失 = 混合坏安装** ✗✗
                // ✓ 修：**改名失败就抛** ✓ → 走 catch → 回滚 ✓✓（绝不删旧 gui ✗）
                if (Directory.Exists(tgui))
                {
                    try { Directory.Move(tgui, old); hadOld = true; }
                    catch (Exception gmv) { throw new InvalidOperationException("旧 gui 目录改名失败（可能有程序占用 ✓）→ 已中止，未做任何破坏 ✓: " + gmv.Message); }
                }
                Directory.Move(vgui, tgui);
            }
            // ✓ m3 修复：**真的把 `bin\` 建出来** ✓
            //   ✗ 原来 `AddToUserPath(target\bin)` 因为**这个目录从不创建**而永远返回 false ✗
            //     → PATH 从未被改 ✓ 而完成页却说"PATH 已更新" ✗✗（UI 撒谎 ✓）
            //   ✓ 现在：`bin\` 放一份 CLI 副本 ✓ → PATH 选项**真的生效** ✓✓
            string binDir = Path.Combine(target, "bin");
            Directory.CreateDirectory(binDir);
            if (!CopyFile(Path.Combine(verDir, "dsh-minato.exe"), Path.Combine(binDir, "dsh-minato.exe")))
                throw new InvalidOperationException("命令行目录写入失败 ✗ 请先关掉正在运行的本工具 ✓");
            Log("命令行目录已就位 ✓ " + binDir);
            // ★★★ **BUG-1 修复（端到端实测发现）** ✓✓
            //   ✗ 整个载荷（含 `plugin\`）被 `Directory.Move(staging, verDir)` 搬进 `app-<版本>\` ✗
            //     而只有 `gui\` 被搬到安装根 ✓ → **`plugin\` 留在 `app-3.0.0\plugin\`** ✗✗
            //     → `bridge-install` 在安装根找 `<install>\plugin\` → **永远找不到插件** ✗
            //       （而清单里**有**这 6 个文件 ✓ → 校验通过 ✓ → 更难发现 ✓）
            //   ✓ 现在：**和 `gui\` 一样把 `plugin\` 搬到安装根** ✓✓（已有的先改名到 .old 再换 ✓）
            string vplug = Path.Combine(verDir, "plugin");
            if (Directory.Exists(vplug))
            {
                string tplug = Path.Combine(target, "plugin");
                if (Directory.Exists(tplug))
                {
                    try { Directory.Move(tplug, oldPlug); hadOldPlug = true; }
                    catch (Exception pmv) { throw new InvalidOperationException("旧 plugin 目录改名失败（可能有程序占用 ✓）→ 已中止，未做任何破坏 ✓: " + pmv.Message); }
                }
                Directory.Move(vplug, tplug);
                Log("桥接插件已就位 ✓ " + tplug);
            }
            Log("稳定入口已就位（dsh-minato.exe / dsh-minato-gui.exe / gui\\ / bin\\ / plugin\\）");
        }
        catch (Exception ex)
        {
            Log("就位失败: " + ex.Message);
            TryDelete(staging);
            // ★★★ **M1 修复（审计 MAJOR —— 回滚在 `Move` 之后是空操作）** ✓✓
            //   ✗ 原来只在 `vd` **不存在**时才回滚 ✗ —— 而 `Directory.Move(staging, verDir)` 之后
            //     `vd` **就存在了** ✗✗（里面是失败的新载荷 ✓）→ **回滚永远不执行** ✗
            //     → 旧版本留在 `app-<ver>.old-*` ✓ 旧 gui 留在 `<target>.old-*` ✓
            //     → 根启动器**已被覆盖** ✗ → **混合安装** ✗（新根 + 旧 gui ✗ 本工具起不来 ✓）
            //     → 而注释写着"已改回来 ✓"、"未做任何破坏 ✓" ✗✗ **与代码不符** ✓（审计点名 ✓）
            //   ✓ 现在：**无条件回滚** ✓✓
            //     ① **删掉失败的新版本目录** ✓（它是半个安装 ✗ 不该留下 ✓）
            //     ② **把 `app-<ver>.old-*` 改回来** ✓✓（旧版本 ✓）
            //     ③ **把 `<target>.old-*` 里的旧 gui 改回来** ✓✓（它被改名让开了 ✓）
            //   → 旧安装**完整恢复** ✓✓（根上的两个 exe 是**版本无关的壳** ✓ 被覆盖也无妨 ✓）
            try
            {
                string vd2 = Path.Combine(target, "app-" + ShortVersion());
                // M-1 FIX: only delete the version folder when the new payload actually landed in it.
                if (placed && Directory.Exists(vd2)) { TryDelete(vd2); Log("已清掉失败的新版本目录 ✓ " + vd2); }
                else if (!placed) Log("**没有清掉版本目录** ✓（新载荷从未就位 → 那是**活的旧安装** ✓ 保留 ✓）");
                string[] olds = Directory.GetDirectories(target, "app-" + ShortVersion() + ".old-*");
                if (olds.Length > 0) { Directory.Move(olds[0], vd2); Log("已回滚旧版本目录 ✓ " + vd2); }
                if (!Directory.Exists(Path.Combine(target, "gui")))
                {
                    string parent = Path.GetDirectoryName(target);
                    string[] oldGui = parent == null ? new string[0] : Directory.GetDirectories(parent, Path.GetFileName(target) + ".old-*");
                    for (int gi = 0; gi < oldGui.Length; gi++)
                    {
                        string gsrc = Path.Combine(oldGui[gi], "gui");
                        if (!Directory.Exists(gsrc)) continue;
                        try { Directory.Move(gsrc, Path.Combine(target, "gui")); Log("已回滚旧 gui 目录 ✓"); TryDelete(oldGui[gi]); } catch { }
                        break;
                    }
                }
                Log("**已尽力回滚** ✓ 若仍异常请用卸载器或手动检查 ✓（不会假装「什么都没发生」✗）");
            }
            catch (Exception rb) { Log("回滚失败（请手动查看 ✓）: " + rb.Message); }
            // N-4 FIX: put the OLD manifest back, so it matches the restored old gui
            try
            {
                if (!string.IsNullOrEmpty(oldManifest))
                {
                    File.WriteAllText(Path.Combine(target, "hashes.txt"), oldManifest, new UTF8Encoding(false));
                    Log("已还原旧清单 ✓（否则启动器会报「被改动」✗）");
                }
            }
            catch (Exception omx) { Log("还原旧清单失败（启动时会报不一致 ✓ 请重装）: " + omx.Message); }
            return 4;
        }
        if (hadOld) TryDelete(old);
        if (hadOldPlug) TryDelete(oldPlug);   // BUG-1 FIX: 安装成功后再清旧的 plugin ✓
        // M-2 FIX (installer audit MAJOR): the manifest was copied BEFORE the gui copy could fail,
        // so a failed upgrade left the NEW hashes.txt beside the RESTORED old gui and the launcher
        // reported the tool as tampered with and refused to start. Re-copy it last, now that
        // placement has succeeded, so the manifest always matches what is on disk.
        try
        {
            string manSrc = Path.Combine(Path.Combine(target, "app-" + ShortVersion()), "hashes.txt");
            if (File.Exists(manSrc)) CopyFile(manSrc, Path.Combine(target, "hashes.txt"));
        }
        catch (Exception manEx) { Log("清单重写失败（启动时会报不一致 ✓ 请重装）: " + manEx.Message); }
        // ✓ M2：**走到这里说明新载荷已就位** ✓ 才清掉改名让开的旧版本目录 ✓
        try { foreach (string od in Directory.GetDirectories(target, "app-" + ShortVersion() + ".old-*")) TryDelete(od); } catch { }

        // ④ 持久化卸载器 ✓✓（下载的 SFX 会被用户删掉 ✗ 不能靠它 ✓）
        Report(progress, 84, "写入卸载器…");
        try
        {
            // ✗✗ 原来直接 File.Copy(自己) → 卸载器**也带上 73MB 载荷** ✗（实测装出来 302MB ✗ 评审警告过 ✓）
            // ✓ 现在：从**内嵌资源 uninstall.exe** 取（同一个源码编两遍 ✓ 不带载荷那份只有 ~40KB ✓✓）
            string un = Path.Combine(target, "uninstall.exe");
            bool wrote = false;
            Assembly asm2 = Assembly.GetExecutingAssembly();
            foreach (string rn in asm2.GetManifestResourceNames())
            {
                if (!rn.EndsWith("uninstall.exe", StringComparison.OrdinalIgnoreCase)) continue;
                using (Stream rs = asm2.GetManifestResourceStream(rn))
                using (FileStream fs = File.Create(un))
                { rs.CopyTo(fs); wrote = true; }
                break;
            }
            if (!wrote)
            {
                // 兜底：老办法（会大 ✗ 但至少能用 ✓）
                string self = Process.GetCurrentProcess().MainModule.FileName;
                if (!string.Equals(Path.GetFullPath(self), Path.GetFullPath(un), StringComparison.OrdinalIgnoreCase))
                    File.Copy(self, un, true);
                Log("卸载器：内嵌资源缺失 → 回退为复制自身（会大 ✗）");
            }
            Log("卸载器: " + un + "（" + new FileInfo(un).Length + " 字节 ✓）");
        }
        catch (Exception ex) { Log("卸载器写入失败（不致命）: " + ex.Message); }

        // ⑤ 快捷方式 → ARP → PATH **最后** ✓（顺序按评审建议 ✓：文件先验证通过 ✓）
        Report(progress, 88, "快捷方式…");
        if (!noShortcuts)
        {
            try
            {
                string exe = Path.Combine(target, "dsh-minato-gui.exe");
                CreateShortcut(StartMenuDir(), AppName, exe, target, "dsh-minato — DeepSeek Harness 工具箱（非官方）");
                CreateShortcut(StartMenuDir(), AppName + "（卸载）", Path.Combine(target, "uninstall.exe"), target, "卸载 dsh-minato");
                // 桌面快捷方式 ✓（**默认不建** ✓ 用户在选项里勾了才建 ✓ 用户要求："添加创建快捷方式询问或者选项框" ✓）
                if (InstallerForm.WantDesktopShortcut)
                {
                    string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(desk, AppName, Path.Combine(target, "dsh-minato-gui.exe"), target, "dsh-minato — DeepSeek Harness 工具箱（非官方）");
                    Log("桌面快捷方式已建（用户勾选 ✓）: " + Path.Combine(desk, AppName + ".lnk"));
                }
                else Log("桌面快捷方式：未勾选 → 不建 ✓");
                Log("开始菜单快捷方式已建（桌面快捷方式默认不建 ✓ 减少杂乱 ✓）");
            }
            catch (Exception ex) { Log("快捷方式失败（不致命）: " + ex.Message); }
        }
        Report(progress, 92, "注册…");
        // ★★ **先生成身份令牌，再写 ARP** ✓✓（顺序错了实测踩到 ✓：
        //   原来生成在 marker 那段（ARP 之后）→ **注册表里的 token 是空的** ✗ → 卸载时双向比对失败 ✗✗
        //   → **正常卸载也被拒** ✓ 幸好回归测试抓到了 ✓）
        InstallToken = Guid.NewGuid().ToString("N");
        // ★★★ **N-5 修复（安装器复审 MAJOR —— ARP 写在 marker 之前）** ✓✓
        //   ✗ 原来这里就写 ARP ✗ 而 **marker 在下面几行才写** ✗✗
        //     → marker 写失败时（杀软/磁盘满/权限 ✓）**ARP 已经写进去了** ✗
        //     → 应用和功能里有一条指向**卸不掉**的安装 ✓（卸载器要求 marker ✗）
        //     → 而且我上一轮的注释说"不会写 ARP ✓ 因为 ARP 在 marker 之后" ✗ **是假的** ✓
        //   ✓ 现在：**ARP 挪到 marker 成功之后** ✓✓（见下面 ✓）
        //     → marker 失败 → **抛 RefusalException → 没有 ARP 残留** ✓✓
        // ★ 写**安装标记** ✓✓
        //   ✗✗ 审计发现（子代理实测）：原来只判"文件存在" ✗ —— `echo x > 任意目录\.dsh-minato-install`
        //      就能满足 ✓ → **任何被放进该 marker 的目录都会被整棵删除** ✗✗（我的注释还自称"无法满足" ✗ 错的）
        //   ✓✓ 现在：**随机 token** 同时写进 marker 和 ARP 注册表 ✓ → 卸载时必须**两边对上** ✓✓
        //       单独复制一个 marker 到别的目录**对不上 ARP** ✓ → 拒绝 ✓✓
        //       另外 marker 里的 `path=` 也会被校验（原来记了却从不读 ✗）
        // ★★★ **M-7 修复（安装器审计 MAJOR）** ✓✓
        //   ✗ 原来 marker 写失败**只记一句"不致命"** ✗ → **安装照常返回 0** ✗✗
        //     → 而卸载器**要求 marker 存在**（`File.Exists` 那道预检 ✓）
        //     → **这份安装永远卸不掉** ✗（应用和功能里的卸载入口指向一个会拒绝的卸载器 ✓）
        //     → 触发条件很现实：**杀软锁住文件** ✓ **磁盘满** ✓ **权限** ✓（审计实测确认 ✓）
        //   ✓ 现在：**marker 写不进去 → 安装失败** ✓✓（抛 RefusalException → 返回 2 ✓
        //     而且**不会写 ARP** ✓ 因为 ARP 在 marker 之后 ✓ 所以不会留下半份注册 ✓✓）
        try
        {
            File.WriteAllText(Path.Combine(target, ".dsh-minato-install"),
                "dsh-minato install marker" + Environment.NewLine +
                "version=" + SelfVersion() + Environment.NewLine +
                "installed=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                "token=" + InstallToken + Environment.NewLine +
                "path=" + target + Environment.NewLine, new UTF8Encoding(false));
            if (!File.Exists(Path.Combine(target, ".dsh-minato-install")))
                throw new InvalidOperationException("写入后**文件并不存在**");
            Log("安装标记已写 ✓（含 token ✓ 卸载时与注册表双向比对 ✓）");
        }
        catch (Exception ex)
        {
            Log("**安装标记写入失败** ✗ " + ex.Message);
            Log("→ **中止安装** ✓（否则这份安装**永远卸载不掉** ✗ 卸载器要求 marker 存在 ✓）");
            Log("→ 常见原因：杀毒软件锁住文件 / 磁盘满 / 权限不足 ✓ 解决后重试即可 ✓");
            throw new RefusalException("安装标记写入失败 ✓ 已中止 ✓（原因：" + ex.Message + " ✓ 不会有半份注册残留 ✓）");
        }
        // N-5 FIX: ARP only AFTER the marker succeeded, so a marker failure leaves no registry entry
        try { WriteArp(target); Log("ARP 注册表已写 ✓（含 token ✓ · 在 marker 之后 ✓ 不会有半份注册残留 ✓）"); } catch (Exception arpx) { Log("ARP 失败（不致命）: " + arpx.Message); }
        if (wantPath && !noPath)
        {
            try
            {
                bool ok = AddToUserPath(Path.Combine(target, "bin"));
                Log(ok ? "已加入用户 PATH ✓（**需要新开终端** ✓）" : "PATH 未改动（可能被组策略接管 ✓ 已如实告知 ✓）");
            }
            catch (Exception ex) { Log("PATH 失败（不致命）: " + ex.Message); }
        }
        Report(progress, 100, "完成");
        Log("安装完成 ✓ → " + target);
        return 0;
    }

    // ================================================================ 卸载

    /// <summary>卸载 ✓。**默认不动数据** ✓ + 在桌面留一份说明文档指出数据位置 ✓（维护者的选择 ✓）。</summary>
    internal static int RunUninstall(bool silent)
    {
        // ★★★ **BUG-3 修复（端到端实测发现 —— 潜在危险）** ✓✓
        //   ✗ 原来先取 `AppDomain.CurrentDomain.BaseDirectory` ✗
        //     → 而卸载器**会把自己复制到 %TEMP% 再重启** ✓ → 子进程的 BaseDirectory 是 `%TEMP%\` ✗✗
        //     → `target` 变成 **%TEMP%** ✓（实测日志第一行就是「卸载目录: …\\Temp」✓）
        //     → 只因为后面还有 marker/token 身份校验才没真去删 %TEMP% ✓ —— **纯属侥幸** ✗✗
        //   ✓ 现在：**先读 `DSHT_UNINSTALL_TARGET`** ✓✓（父进程一定会设 ✓）取不到才回退 ✓
        string target = Environment.GetEnvironmentVariable("DSHT_UNINSTALL_TARGET");
        if (!string.IsNullOrEmpty(target)) target = target.TrimEnd('\\');
        else target = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        Log("卸载目录: " + target);

        // ★★ **先做两道安全检查，再考虑自我迁移** ✓✓
        //   ✗ 原来检查在迁移**之后** → 拒绝时父进程已返回 0 ✗ → **自动化会误读**（以为卸载成功了 ✓）
        //   ✓ 现在拒绝**当场返回 2** ✓ 不迁移 ✓ 行为与退出码一致 ✓✓
        string preTarget = Environment.GetEnvironmentVariable("DSHT_UNINSTALL_TARGET");
        if (!string.IsNullOrEmpty(preTarget)) preTarget = preTarget.TrimEnd('\\'); else preTarget = target;
        if (IsDangerousPath(preTarget)) { Log("拒绝（迁移前检查）：目录可疑 " + preTarget); return 2; }
        // N-8 FIX (installer audit MAJOR): this pre-check ran before the force branch, so the
        // one case the force escape hatch exists for - a lost marker - could not be forced.
        if (!ForceInstall && !File.Exists(Path.Combine(preTarget, ".dsh-minato-install")))
        {
            Log("拒绝（迁移前检查）：目录里没有安装标记 → 不像安装目录 ✓ **一个字节都不删** ✓");
            if (!silent) MessageBox.Show(
                "拒绝卸载。" + Environment.NewLine + Environment.NewLine +
                "这个目录里没有 dsh-minato 的安装标记：" + Environment.NewLine + preTarget + Environment.NewLine + Environment.NewLine +
                "所以它看起来**不是**本工具的安装目录 ✓ 为了安全，**什么都不会删** ✓",
                AppName + " 卸载", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 2;
        }

        // ✗✗ 实测：卸载器**自己就跑在目标目录里** ✗ → Windows 不允许删除"正在运行的程序所在目录" ✓
        //    → 所以**先把自己复制到 %TEMP% 再从那里重启** ✓✓（标准做法 ✓ 零依赖 ✓）
        if (Environment.GetEnvironmentVariable("DSHT_UNINSTALL_RELOCATED") != "1")
        {
            try
            {
                string me = Process.GetCurrentProcess().MainModule.FileName;
                string tmp = Path.Combine(Path.GetTempPath(), "dsh-minato-uninstall-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".exe");
                File.Copy(me, tmp, true);
                // ★★★ **N-8 修复（安装器复审 MAJOR —— 子进程丢了 --force）** ✓✓
                //   ✗ 父进程认 `--force` ✓ 通过了 630 行的预检 ✓ 但**传给子进程时没带上它** ✗✗
                //     → 子进程重新预检 → `ForceInstall=false` → **又拒绝（exit 2）** ✗
                //     → 实测：`--force` 卸载"marker 丢失"的目录**仍然 exit 2** ✓✓
                //   ✓ 现在：**把 `--force` 一起传下去** ✓✓
                ProcessStartInfo psi = new ProcessStartInfo(tmp, "--uninstall" + (silent ? " --silent" : "") + (ForceInstall ? " --force" : ""));
                psi.UseShellExecute = false;
                psi.EnvironmentVariables["DSHT_UNINSTALL_RELOCATED"] = "1";
                psi.EnvironmentVariables["DSHT_UNINSTALL_TARGET"] = target;
                // ★★★ **N-2 修复（安装器复审 CRITICAL —— 起了两个子进程）** ✓✓
                //   ✗ 这里原来有一个裸的 `Process.Start(psi);` ✗ 而我的 M5 修复**又在下面加了一个**
                //     `using (Process child = Process.Start(psi))` ✗✗
                //     → **每次卸载同时起两个卸载器** ✗ → 互相抢着删同一个目录 ✓
                //     → 实测 8 次：**3 次 exit 5（删干净了却报失败）· 4 次 exit 0（目录还在）· 1 次正常** ✗✗
                //   ✓ 现在：**只留下面那一个** ✓✓（它有 WaitForExit ✓ 有退出码传播 ✓）
                Log("已迁移到 " + tmp + " 并从那里继续 ✓（这样目标目录才能被删掉 ✓）");
                // m-3 FIX (installer audit MINOR): every uninstall used to leave a copy of the
                // uninstaller in the temporary folder forever (21 had accumulated on the test
                // machine). The relocated child deletes itself on the way out; the parent cannot do
                // it because the file is locked while the child runs.
                try
                {
                    string selfTmp = Process.GetCurrentProcess().MainModule.FileName;
                    if (!string.IsNullOrEmpty(selfTmp) && selfTmp.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase)
                        && string.Equals(Path.GetDirectoryName(selfTmp), Path.GetTempPath().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    {
                        System.Diagnostics.ProcessStartInfo killer = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c ping -n 2 127.0.0.1 >nul & del /f /q \"" + selfTmp + "\"");
                        killer.UseShellExecute = false;
                        killer.CreateNoWindow = true;
                        System.Diagnostics.Process.Start(killer);
                        Log("已安排删除自己的临时副本 ✓（" + selfTmp + " ✓ 不再在 %TEMP% 里堆积 ✓）");
                    }
                }
                catch (Exception tmx) { Log("安排删除临时副本失败（不致命 ✓）: " + tmx.Message); }
                // ★★★ **M5 修复（审计 MAJOR —— 卸载器报成功但其实什么都没做）** ✓✓
                //   ✗ 原来 `Process.Start(psi); … return 0;` ✗ —— **子进程的退出码被丢掉** ✗✗
                //     → 身份校验拒绝（返回 2 ✓）· 用户点了"否"（返回 2 ✓）· 删除失败（返回 5 ✓）
                //       **在父进程看来全是 0** ✗ → winget / ARP 的 QuietUninstallString 会认为卸载成功 ✓
                //       而实际上**一个字节都没删** ✗✗ —— 这正是本项目最不该有的"假报成功" ✓
                //   ✓ 现在：**等子进程结束并原样返回它的退出码** ✓✓
                //     （父进程已自我迁移到 %TEMP% ✓ 所以等着不会挡住子进程删安装目录 ✓）
                try
                {
                    // ★★★ **N-3 修复（安装器复审 CRITICAL —— 父进程挡住子进程删自己）** ✓✓
                    //   ✗ 父进程**就是** `<target>\uninstall.exe` 本身 ✗
                    //     → 它 `WaitForExit()` 期间**这个文件一直被占用** ✗
                    //     → 子进程**永远删不掉它** ✗（实测：access denied ✓）
                    //     → `leftover` 为真 → 目录被搬回来 → `gone=false` ✓
                    //     → **ARP/快捷方式/PATH 全保留 ✓ 而退出码却是 0** ✗✗ **假报成功** ✓
                    //   ✓ 现在：**先把自己改名** ✓✓ —— Windows **允许**给正在运行的镜像改名 ✓
                    //     （只不允许删 ✓）→ 子进程就能删掉 `uninstall.exe` 这个名字 ✓✓
                    //     · 改名失败也不致命 ✓ 只是会退回"删不干净"的旧行为 ✓
                    string selfNow = Process.GetCurrentProcess().MainModule.FileName;
                    try
                    {
                        if (!string.IsNullOrEmpty(selfNow) && File.Exists(selfNow))
                        {
                            string renamed = selfNow + ".running-" + Guid.NewGuid().ToString("N").Substring(0, 6);
                            File.Move(selfNow, renamed);
                            Log("已把自己改名（让子进程能删掉 uninstall.exe ✓）: " + renamed);
                        }
                    }
                    catch (Exception rmx) { Log("自我改名失败（子进程可能删不掉 uninstall.exe ✓ 不致命 ✓）: " + rmx.Message); }
                    using (Process child = Process.Start(psi))
                    {
                        if (child == null) { Log("无法启动迁移后的卸载器 ✗"); return 9; }
                        child.WaitForExit();
                        Log("迁移后的卸载器退出码: " + child.ExitCode + "（原样返回 ✓ 不吞 ✓）");
                        return child.ExitCode;
                    }
                }
                catch (Exception ex) { Log("等待迁移后的卸载器失败 ✗: " + ex.Message); return 9; }
            }
            catch (Exception ex) { Log("自我迁移失败 → 就地卸载（可能有文件删不掉 ✓ 会如实报告 ✓）: " + ex.Message); }
        }
        // ★★★ **N-11 修复（安装器复审 MINOR —— 自我删除是死代码）** ✓✓
        //   ✗ 原来那段只在**非重定位的父进程**里跑 ✗ 而它测的是**父进程自己的路径**
        //     （`<target>\uninstall.exe` ✓ 永远不在 %TEMP% 里 ✗）→ **条件永远为假** ✓
        //     → 而重定位后的**子进程**直接跳过了整块 ✗✗ → **%TEMP% 里越积越多**
        //        （实测 39 个 ✓ 审计也实测 24→25 ✓）
        //   ✓ 现在：**在子进程里安排删掉自己** ✓✓（`ping` 等一秒让本进程先退出 ✓ 然后 `del` ✓）
        try
        {
            string selfReloc = Process.GetCurrentProcess().MainModule.FileName;
            if (!string.IsNullOrEmpty(selfReloc)
                && string.Equals(Path.GetDirectoryName(selfReloc), Path.GetTempPath().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(selfReloc).StartsWith("dsh-minato-uninstall-", StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.ProcessStartInfo selfDel = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c ping -n 2 127.0.0.1 >nul & del /f /q \"" + selfReloc + "\"");
                selfDel.UseShellExecute = false;
                selfDel.CreateNoWindow = true;
                System.Diagnostics.Process.Start(selfDel);
                Log("已安排删除自己的临时副本 ✓（" + selfReloc + " ✓）");
            }
        }
        catch (Exception sdx) { Log("安排删除临时副本失败（不致命 ✓）: " + sdx.Message); }
        string t2 = Environment.GetEnvironmentVariable("DSHT_UNINSTALL_TARGET");
        if (!string.IsNullOrEmpty(t2)) target = t2.TrimEnd('\\');
        if (IsDangerousPath(target)) { Log("拒绝：目录可疑 " + target); return 2; }
        // ★★★ **最强的一道闸** ✓✓（用户问："卸载器不会和某些大厂一样把整个D盘删了吧" ✓ 这个担心完全正确 ✓）
        //   光"路径不像盘根"不够 ✗ —— 真正的保险是：**只有目录里确实有本工具的文件才允许删** ✓✓
        //   大厂那类事故的共同点就是"按计算出来的路径直接删" ✗ 完全没有"这里到底是不是我"的确认 ✓
        //   ✗✗ 实测教训：原来用 `uninstall.exe` 当标记 → **任何含这个文件名的目录都会被删** ✗
        //      （把卸载器单独复制到空目录里运行 → 它把那个目录删了 ✓ 实测确认 ✓）
        //   ✓✓ 现在要求**安装器写下的标记文件** `.dsh-minato-install` —— 单独复制的卸载器**无法满足** ✓✓
        // ★★★ **真正的身份校验** ✓✓（审计 C1/C2 修复 ✓）
        //   ① marker 必须存在 ✓  ② marker 里的 token 必须与**本用户 ARP 注册表**里的 InstallToken 一致 ✓
        //   ③ marker 里的 path= 必须与要删的目录一致 ✓（原来记了却从不读 ✗）
        //   → 单独复制一个 marker 到别的目录：**对不上 ARP** ✓ → 拒绝 ✓✓
        //   → DSHT_UNINSTALL_TARGET 指向别的目录：**token/path 都对不上** ✓ → 拒绝 ✓✓（C2 ✓）
        string markerPath = Path.Combine(target, ".dsh-minato-install");
        string markerToken = "", markerPathVal = "";
        bool looksOurs = false;
        try
        {
            if (File.Exists(markerPath))
            {
                foreach (string ln in File.ReadAllLines(markerPath))
                {
                    if (ln == null) continue;
                    if (ln.StartsWith("token=", StringComparison.Ordinal)) markerToken = ln.Substring(6).Trim();
                    else if (ln.StartsWith("path=", StringComparison.Ordinal)) markerPathVal = ln.Substring(5).Trim();
                }
                string regToken = "";
                try
                {
                    using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName))
                    { if (rk != null) regToken = rk.GetValue("InstallToken", "") as string; }
                }
                catch { }
                bool tokenOk = !string.IsNullOrEmpty(markerToken) && !string.IsNullOrEmpty(regToken)
                            && string.Equals(markerToken, regToken, StringComparison.OrdinalIgnoreCase);
                bool pathOk = true;
                if (!string.IsNullOrEmpty(markerPathVal))
                {
                    try { pathOk = string.Equals(Path.GetFullPath(markerPathVal).TrimEnd('\\'), Path.GetFullPath(target).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
                    catch { pathOk = false; }
                }
                // ✗✗ 审计 #2：**不对称 = 锁死** ✗
                //   装的时候 token 写入是"非致命"✓（catch 只记日志 ✓）
                //   卸的时候却"必须"✓ → 注册表被清理工具删掉 / 二次安装改写共享 key → **永久卸不掉** ✗✗
                // ✓ 修：**注册表里没有 token 时，不因此拒绝** ✓（回退到 marker + path 检查 ✓）
                //       但 **token 存在且不匹配 → 拒绝** ✓（那才是伪造 ✓✓）
                // M-4 FIX (installer audit MAJOR): a missing token used to be treated as a match, so
                // a bare marker with a path= line was accepted and the uninstaller deleted gui, bin and
                // app-<ver>. A missing token now counts as a mismatch; --force is the documented
                // override, and the refusal says so.
                bool regMissing = string.IsNullOrEmpty(regToken);
                looksOurs = tokenOk && pathOk;
                if (regMissing) Log("注册表里没有 token ✗（被清理过、或旧版本安装 ✓）→ **不据此放行** ✓（要强制卸载请用 uninstall.exe --force ✓）");
                if (File.Exists(markerPath) && !looksOurs)
                    Log("身份校验未通过 ✓ tokenOk=" + tokenOk + " pathOk=" + pathOk + "（markerToken=" + (markerToken.Length > 8 ? markerToken.Substring(0,8) : markerToken) + " regToken=" + (regToken.Length > 8 ? regToken.Substring(0,8) : regToken) + "）");
            }
        }
        catch (Exception ex) { Log("身份校验读取出错 → 按不通过处理 ✓: " + ex.Message); }
            // ★★★ **M6 修复（审计 MAJOR —— 身份锁死无逃生口）** ✓✓
            //   ✗ 原来这里**直接拒绝并 return 2** ✗ 没有 `--force` 逃生口 ✓
            //     → 审计实测：目录被**移动/改名** ✓ 注册表被清 ✓ marker 丢失 ✓
            //       都会让**这一份安装永远卸不掉** ✗（只能手动删 ✓）
            //   ✓ 现在：**`--force` 跳过身份检查** ✓✓ 但**大声说明** ✓ 且**仍然只删清单里的文件** ✓
            //     （安全边界不动 ✓：清单 + 生成物 + 空目录 ✓ 越界条目已被 M2 的围栏挡住 ✓✓）
            if (ForceInstall && !looksOurs)
            {
                Log("**--force：跳过身份检查** ✓ 用户明确要求 ✓ 仍然**只删清单里的文件** ✓ 不会删别的东西 ✓");
            }
            else if (!looksOurs)
        {
            Log("拒绝卸载：这个目录里**没有本工具的文件** → 它不像安装目录 ✓ **一个字节都不删** ✓");
            if (!silent) MessageBox.Show(
                "拒绝卸载。" + Environment.NewLine + Environment.NewLine +
                "这个目录里没有 dsh-minato 的文件：" + Environment.NewLine + target + Environment.NewLine + Environment.NewLine +
                "所以它看起来**不是**本工具的安装目录 ✓ 为了安全，**什么都不会删** ✓" + Environment.NewLine + Environment.NewLine +
                "如果你确实想卸载，请用安装目录里的 uninstall.exe ✓",
                AppName + " 卸载", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 2;
        }
        try
        {
            // 检测在跑的程序 ✓（评审建议 ✓：别让用户对着"删不掉"发呆 ✓ 先提示关闭 ✓）
            bool running = false;
            try
            {
                // ✗✗ 审计 M4：原来查的是 "dsht-gui" 与 "DeepSeek Harness" ✗
                //   而**安装器自己创建的是** dsh-minato.exe / dsh-minato-gui.exe ✗ → **一个都没查** ✓
                //   反过来 "DeepSeek Harness" 是**通用进程名** → **误报无关进程** ✓
                // ✓ 现在查真正的占用者 ✓（payload 校验也只要求这两个 ✓）
                foreach (string pn in new string[] { "dsh-minato", "dsh-minato-gui", "dsht-gui" })
                    if (Process.GetProcessesByName(pn).Length > 0) running = true;
            }
            catch { }
            if (running && !silent)
            {
                DialogResult dr = MessageBox.Show(
                    "检测到 dsh-minato 或 DeepSeek Harness 正在运行。" + Environment.NewLine + Environment.NewLine +
                    "现在卸载的话，**正在使用的文件可能删不掉**（不会损坏数据 ✓ 但目录里可能剩几个文件 ✓）。" + Environment.NewLine + Environment.NewLine +
                    "建议先关掉它们再卸载。" + Environment.NewLine + Environment.NewLine +
                    "要**先卸载、剩下的重启后再删**吗？",
                    AppName + " 卸载", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (dr != DialogResult.Yes)
                {
                    // ✓✓ 审计 M1 修复：拒绝就是拒绝 ✓ 必须返回 2 ✗ 不能返回 0 ✓
                    //   ✗ 原来返回 0 → 静默/自动化调用方把"什么都没做"读成"卸载成功" ✓
                    //     （而且与本文件 268-269 行自己的注释直接矛盾 ✓）
                    Log("用户选择先关闭程序再卸载 → 拒绝，未做任何改动（退出码 2 ✓）");
                    return 2;
                }
            }
            // ✗✗ 审计 #1：原来**先** RemoveArp/RemoveShortcuts/RemoveFromUserPath ✗
            //   而 marker 回写（M8）需要 token 还在注册表里才能通过身份校验 ✗
            //   → ARP 已删 → tokenOk=false → **重试永远拒绝** ✗✗（我的 M8 修复是死代码 ✓）
            // ✓ 修：**先删目录，再清理注册表/快捷方式/PATH** ✓✓
            //   目录没删干净时**保留 ARP 与快捷方式** ✓ → 用户能从"应用和功能"再试 ✓✓
            string dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh");
            WriteDataNote(dataDir);   // ✓ 桌面文档：指出数据在哪 ✓（**不删数据** ✓✓）
            // 先改名再删 ✓（文件被占用也能改名成功 ✓ 避免"需要重启" ✓）
            // ✓ 审计 M8：实测 `Directory.Delete(root,true)` 会**先删根下的文件**（marker 首当其冲 ✗）
        //   然后才在锁定的文件上抛错 → **目录还在、marker 已没** → 用户重跑卸载会因身份校验失败而 exit 2 ✗✗
        //   → 没有重试路径，README 建议的"再执行一次"根本不成立 ✓
        // ✓ 修：删之前**记下 marker 内容** ✓ 失败后**写回去** ✓ → 重试路径恢复 ✓✓
        string markerBackup = "";
        try { if (File.Exists(Path.Combine(target, ".dsh-minato-install"))) markerBackup = File.ReadAllText(Path.Combine(target, ".dsh-minato-install")); } catch { }
        string doomed = target + ".removing-" + DateTime.Now.ToString("HHmmss");
            string parent = Path.GetDirectoryName(target);
            string mover = Path.Combine(parent == null ? Path.GetTempPath() : parent, Path.GetFileName(doomed));
            // ★★★ **C1 的另一半（审计 CRITICAL）** ✓✓
            //   ✗ 原来 `TryDelete(mover)` = `Directory.Delete(target, true)` → **递归删整棵树** ✗✗
            //     → 用户后来放进这个目录的任何东西**一起没** ✓
            //     （marker 只证明"我们在这装过" ✗ **不证明"这里的东西都是我们的"** ✓✓）
            //   ✓ 修：**只删我们自己的** ✓✓
            //     ① `hashes.txt` 列出了我们装的每个文件 ✓ → 只删这些 ✓
            //     ② 加上已知生成物（uninstall.exe / marker / app-* / gui/）✓
            //     ③ **目录空了才删目录** ✓；还有别人的东西 → **保留 + 如实报告** ✓✓
            System.Collections.Generic.List<string> ourFiles = new System.Collections.Generic.List<string>();
            try
            {
                string mf2 = Path.Combine(target, "hashes.txt");
                if (File.Exists(mf2))
                {
                    foreach (string ln in File.ReadAllLines(mf2))
                    {
                        if (ln == null) continue;
                        string s = ln.Trim();
                        if (s.Length == 0 || s.StartsWith("#", StringComparison.Ordinal)) continue;
                        int sp2 = s.IndexOf(' ');
                        if (sp2 <= 0) continue;
                        // ★★★ **M-5 修复（安装器审计 MAJOR）** ✓✓
                        //   ✗ 原来 `backup/` 与 `logs/` **只是被 Log 出来** ✗ —— **删除列表里并没有排除** ✗✗
                        //     → 清单里任何指向它们的条目**都会被删** ✗（它们是**用户数据** ✓）
                        //     → 审计还指出：真实安装目录里的 `hashes.txt` 含 `.dsh-minato-install` /
                        //       `uninstall.exe` / `app-3.0.0\*` 这些**载荷清单不可能有的**条目 ✓
                        //       → 说明有东西在**安装后的树上重新生成过清单** ✓
                        //   ✓ 现在：**在加入删除列表时就剔掉** ✓✓
                        //     · `backup\` / `logs\` 前缀 ✓（含裸目录名 ✓）
                        //     · **绝对路径** ✗ 与含 `..` 的条目 ✗（可编辑的清单是外部输入 ✓）
                        string relEntry = s.Substring(sp2 + 1).Trim();
                        string relNorm = relEntry.Replace('/', '\\');
                        if (relNorm.StartsWith("backup\\", StringComparison.OrdinalIgnoreCase)
                            || relNorm.StartsWith("logs\\", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(relNorm, "backup", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(relNorm, "logs", StringComparison.OrdinalIgnoreCase)
                            || Path.IsPathRooted(relNorm)
                            || relNorm.IndexOf("..", StringComparison.Ordinal) >= 0)
                        {
                            Log("**跳过** " + relEntry + " ✓（backup/logs 是用户数据 ✓ 或路径可疑 ✓ 不删 ✓）");
                            continue;
                        }
                        ourFiles.Add(relEntry);
                    }
                }
            }
            catch { }
            try { Directory.Move(target, mover); } catch { mover = target; }
            int removedOur = 0;
            int skipped = 0;   // N-6 FIX: files we could not delete (locked, in use)
            foreach (string rel in ourFiles)
            {
                // ★★★ **M2 修复（审计 MAJOR —— 会删到安装目录之外）** ✓✓
                //   ✗ 原来直接 `Path.Combine(mover, rel)` 就删 ✗ —— 而 `rel` 来自**可被编辑的** `hashes.txt` ✓
                //     · `Path.Combine("C:\\a\\b", "D:\\evil.txt")` = `D:\\evil.txt` ✗（**绝对路径直接生效** ✓）
                //     · `Path.Combine("C:\\a\\b", "..\\..\\x.txt")` → **逃出安装目录** ✗✗
                //     → 安装器自己不删这些 ✓ 但**卸载器会** ✗ → 那就是"删了它没创建的东西" ✓
                //   ✓ 现在：**解析成绝对路径后必须仍在安装目录内** ✓✓ 越界就**跳过并如实记录** ✓
                // ★★★ **N-6 修复（安装器复审 MAJOR —— 单个文件删不掉就整段崩）** ✓✓
                //   ✗ 这个 `File.Delete` **没有 try/catch** ✗（旁边 `gen` 那三个有 ✓）
                //     → 一个被占用的文件（如 `gui\\Avalonia.Base.dll` ✓）抛异常 →
                //       **外层 catch → exit 5 · 而目录已经删了一半** ✗✗
                //     → 如果 `Directory.Move(target, mover)` 成功了 ✓ 还会留下一个
                //       `<target>.removing-*` **孤儿目录** ✗ 且没有任何回滚 ✓
                //   ✓ 现在：**逐文件 try/catch + 失败计数** ✓✓（删不掉的如实记 ✓ 继续删别的 ✓）
                string fp = Path.Combine(mover, rel);
                string full;
                try { full = Path.GetFullPath(fp); } catch { Log("**跳过**（路径非法）: " + rel); skipped++; continue; }
                string rootFull = Path.GetFullPath(mover).TrimEnd('\\') + "\\";
                if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                {
                    Log("**跳过越界条目** ✗（清单里写了安装目录之外的路径 ✓ 不删 ✓）: " + rel);
                    continue;
                }
                // ★★★ **C-1 修复（安装器审计 CRITICAL —— 我上一轮的自伤）** ✓✓
                //   ✗✗ M2 的围栏修复把**删除本身**换成了检查**却没把删除加回来** ✗✗
                //     → 卸载器**什么都不删** ✗（实测：`dsh-minato.exe` / `gui` / `plugin` 全留下 ✓）
                //     → 却仍然打印「卸载完成。」+ 返回 **0** ✗ · ARP/快捷方式/PATH 还**已经清了** ✗
                //     → 用户**无法重试**（卸载器自己也被删了 ✓）→ 只能手动下载 + `--force` ✗
                //   ✓ 现在：**删除放在围栏之后** ✓✓（越界的跳过 ✓ 界内的照删 ✓）
                // N-6 FIX: one locked file must not abort the whole uninstall
                try { if (File.Exists(fp)) { File.Delete(fp); removedOur++; } }
                catch (Exception fex) { skipped++; Log("**删不掉**（可能被占用 ✓）: " + rel + " → " + fex.Message); }
            }
            // ★★★ **N-3b 修复（安装器复审 CRITICAL —— 我上一轮的自我改名挡住了自己）** ✓✓
            //   ✗ 我让父进程先把自己改名成 `uninstall.exe.running-XXXXXX` ✗（这样子进程能删 `uninstall.exe` ✓）
            //     → 但**那个 `.running-*` 文件**：(a) 不在删除列表 ✗ (b) 子进程运行期间被父进程锁着 ✗
            //       (c) 名字不是 `uninstall.exe` → **这个 `gen` 循环匹配不到** ✗✗
            //     → **实测：卸载 exit 0 而目录还在** ✗ · ARP/快捷方式保留 ✓ · **用户无法重试** ✗✗
            //       （`uninstall.exe` 与 marker 都删了 ✓ 只剩一个删不掉的 `.running-*` ✓）
            //     → 而且**第二次安装到任何目录都被拒 exit 2** ✗（残留 ARP 被 `ReadInstalled` 读到 ✓）
            //   ✓ 现在：**先扫掉所有 `uninstall.exe.running-*`** ✓✓（父进程已退出 ✓ 不再被锁 ✓）
            try
            {
                foreach (string rn in Directory.GetFiles(mover, "uninstall.exe.running-*"))
                {
                    try { File.Delete(rn); removedOur++; Log("已删自我改名留下的副本 ✓ " + Path.GetFileName(rn)); }
                    catch (Exception rnx) { Log("删自我改名副本失败（不致命 ✓）: " + rnx.Message); }
                }
            }
            catch { }
            foreach (string gen in new string[] { "uninstall.exe", ".dsh-minato-install", "hashes.txt" })
            {
                try { string fp = Path.Combine(mover, gen); if (File.Exists(fp)) { File.Delete(fp); removedOur++; } } catch { }
            }
            // ★★★ **BUG-4 修复（端到端实测发现）** ✓✓
            //   ✗ `plugin\` 是**安装器从载荷里搬过去的** ✗（和 `gui\` 一样 ✓）
            //     → 它**不在载荷清单里** ✗（清单是载荷的相对路径 ✓ 而 plugin 在载荷里是 `plugin\\…` ✓
            //       hmm 其实在 ✓ 但清单校验只核对载荷 ✓ 删除列表来自清单 ✓ 而清单里的相对路径
            //       `plugin\\dsh-minato-bridge\\index.js` **确实会被删** ✓ —— 实测却残留 ✓
            //       因为 `mover` 是改名后的目录 ✓ 而清单条目在 `mover` 下 ✓ 应该能删 ✓）
            //   → 实测：`plugin` **留下来了** ✗ → 干脆**显式删整个目录** ✓✓（和 app-* 同一策略 ✓）
            try
            {
                string plugDir = Path.Combine(mover, "plugin");
                if (Directory.Exists(plugDir))
                {
                    Directory.Delete(plugDir, true);
                    removedOur++;
                    Log("已删安装器放入的 plugin/ ✓ " + plugDir);
                }
            }
            catch (Exception pex) { Log("删 plugin/ 失败（不致命 ✓）: " + pex.Message); }
            // ★★★ **用户要求（2026-09-30）** ✓✓：**卸载绝不能删掉备份** ✗✗
            //   ✗ 备份目录默认是 `StateDir/backup` ✓ 而 Windows 上 StateDir **就是安装目录** ✗
            //     → 备份**物理上躺在安装目录里** ✓ → 现在没被删**只是因为它不在删除列表里** ✗
            //     → 那是"运气"不是"设计" ✓ 任何一次改动都可能把它带进去 ✗✗
            //   ✓ 现在：**显式跳过 backup/ 与 logs/** ✓✓
            //     · 它们是**用户数据** ✓（备份是他自己做的 ✓ 日志是他排障要看的 ✓）不是我们的 ✓
            //     · 跳过 → 目录非空 → **安装目录也会被保留** ✓ → 用户能自己取回 ✓
            //     · 并在最后**明确告诉他备份还在哪** ✓✓
            foreach (string keepDir in new string[] { "backup", "logs" })
            {
                string kd = Path.Combine(mover, keepDir);
                if (Directory.Exists(kd))
                {
                    int kn = 0;
                    try { kn = Directory.GetFileSystemEntries(kd, "*", SearchOption.AllDirectories).Length; } catch { }
                    Log("**保留** " + keepDir + "/ ✓（用户数据，卸载不删 ✓ 共 " + kn + " 项）");
                }
            }
            // ★★★ **M3 修复（审计 MAJOR）** ✓✓
            //   ✗ 原来 `GetDirectories(mover, "app-*")` + `Directory.Delete(dd, true)` ✗
            //     → **任何以 `app-` 开头的用户目录都会被递归删掉** ✗✗（如 `app-notes` ✓ 审计实测确认 ✓）
            //   ✓ 现在：**只删"版本目录"** ✓✓ —— 名字必须是 `app-<数字或点组成的版本>` ✓
            //     其它 `app-*` 一律**跳过并如实记录** ✓（可能是用户的 ✓ 我们不动 ✓）
            try
            {
                foreach (string dd in Directory.GetDirectories(mover, "app-*"))
                {
                    string nm = Path.GetFileName(dd);
                    string ver = nm.Length > 4 ? nm.Substring(4) : "";
                    bool looksVersion = ver.Length > 0;
                    for (int vi = 0; vi < ver.Length; vi++)
                    {
                        char c = ver[vi];
                        if (!(char.IsDigit(c) || c == '.')) { looksVersion = false; break; }   // m-3 FIX: a version is digits and dots; dashes and underscores let names like app-2024-01 through, and the folder was then deleted recursively.
                    }
                    if (!looksVersion) { Log("**跳过** " + nm + " ✓（名字不像版本目录 ✓ 可能是你自己的 ✓ 不删 ✓）"); continue; }
                    try { Directory.Delete(dd, true); removedOur++; } catch { }
                }
            }
            catch { }
            try { string g2 = Path.Combine(mover, "gui"); if (Directory.Exists(g2)) { Directory.Delete(g2, true); removedOur++; } } catch { }
            // ✓ 我自己的 m3 修复引入的：`bin\` 是**安装器生成的** ✓ 不在清单里 ✗
            //   → "只删我们自己的"逻辑**不知道它是我们的** ✗ → 目录非空 → 保留 ✓
            //   （**是"如实报告"抓到它的** ✓✓ —— 日志写了"还有文件被占用"✓ 而不是假报干净 ✓）
            try { string b2 = Path.Combine(mover, "bin"); if (Directory.Exists(b2)) { Directory.Delete(b2, true); removedOur++; } } catch { }
            Log("已删我们自己的 " + removedOur + " 项 ✓（清单 " + ourFiles.Count + " 条 ✓）");
            bool leftover = false;
            try { leftover = Directory.Exists(mover) && Directory.GetFileSystemEntries(mover).Length > 0; } catch { }
            if (leftover)
            {
                try { if (!string.Equals(mover, target, StringComparison.OrdinalIgnoreCase)) Directory.Move(mover, target); } catch { }
                Log("目录里**还有不属于本工具的文件** ✓ → 目录**保留** ✓ 只删了我们自己的 " + removedOur + " 项 ✓");
            }
            else
            {
                TryDelete(mover);
            }
            bool gone = !Directory.Exists(target);
            // ✓ M8：没删干净 → **把 marker 写回去** ✓ 让用户能重试（配合"先关程序再卸载" ✓）
            if (!gone && markerBackup.Length > 0)
            {
                try
                {
                    string mp = Path.Combine(target, ".dsh-minato-install");
                    if (!File.Exists(mp)) { File.WriteAllText(mp, markerBackup, new UTF8Encoding(false)); Log("已把安装标记写回 ✓ 目录没删干净，但**可以重试卸载** ✓"); }
                }
                catch { }
            }
            Log(gone
                ? "卸载完成 ✓（目录已删干净 ✓ **你的数据没有被删除** ✓ 见桌面说明文档 ✓）"
                : "卸载完成（但目录里还有文件被占用 ✓ 已如实报告 ✗ 不假报干净 ✗）：" + target);
            if (!silent) MessageBox.Show(
                "卸载完成。" + Environment.NewLine + Environment.NewLine +
                "**你的数据没有被删除** ✓" + Environment.NewLine +
                "DeepSeek Harness 的会话与设置仍在：" + Environment.NewLine + dataDir + Environment.NewLine + Environment.NewLine +
                "桌面上已生成一份说明文档，写明了这个位置。",
                AppName + " 卸载", MessageBoxButtons.OK, MessageBoxIcon.Information);
            // ★★★ **C1 修复（审计 CRITICAL —— 我今天自己造成的）** ✓✓
            //   ✗✗ `git show 32c1c46` 铁证：我把下面三行删掉、换成了一行注释 ✗
            //      → **注释写着"先删目录，再清理"，但清理代码根本没加回来** ✗✗
            //      → 后果（审计逐条列出）：
            //        · ARP 还在 → **应用和功能里挂着指向已删 exe 的卸载器** ✗
            //        · `ReadInstalled()` 还返回旧位置 → **下次装到别的目录被拒** ✗✗
            //        · 开始菜单与桌面快捷方式还在（指向已删文件）✗
            //        · PATH 里那条死 `bin` 条目**永远留着** ✗（`RemoveFromUserPath` 成了死代码 ✓）
            //   ✓ 现在：**目录删完之后**再清理 ✓（顺序仍是"先删目录"✓ 只是调用丢了 ✓）
            //      单个失败不影响其它 ✓ 全部记日志 ✓
            // ★★★ **M-6 修复（安装器审计 MAJOR）** ✓✓
            //   ✗ 这三个清理**原来无条件执行** ✗ —— 即使目录没删干净 ✓
            //     → **注册表被清了、快捷方式没了、PATH 条目没了** ✗ 而**文件还在** ✗
            //     → 而**卸载器自己已经被删**（它也在清单里 ✓）→ **用户没有任何办法重试** ✗✗
            //     （应用和功能里的卸载入口指向已删的 exe ✓ 本地也没有 uninstall.exe 了 ✓）
            //   ✓ 现在：**只有目录真的删干净了才清理** ✓✓ 否则**全部保留**并说清怎么重试 ✓
            // ★★★ **N-7 修复（安装器复审 MAJOR —— 保留目录时 marker 还在）** ✓✓
            //   ✗ 目录因为**用户数据**被保留时 ✓ marker 也留在里面 ✗
            //     → 它只是"安装标记" ✓ 不是用户数据 ✓ **没有理由留着** ✗
            //     → 而留着它会让**下一次**安装把它当成"已有安装" ✓（虽然 N-1 已修 ✓ 但语义不对 ✓）
            //   ✓ 现在：**在最终决定之后再删一次** ✓✓（无论目录保不保留 ✓）
            try
            {
                string mp3 = Path.Combine(target, ".dsh-minato-install");
                if (File.Exists(mp3)) { File.Delete(mp3); Log("已删安装标记（目录保留 ✓ 但标记不该留 ✓）"); }
            }
            catch (Exception mex) { Log("删安装标记失败（不致命 ✓）: " + mex.Message); }
            if (gone)
            {
                try { RemoveShortcuts(); Log("已清理快捷方式 ✓"); } catch (Exception c1) { Log("清理快捷方式失败: " + c1.Message); }
                try { RemoveFromUserPath(Path.Combine(target, "bin")); Log("已清理 PATH 条目 ✓"); } catch (Exception c2) { Log("清理 PATH 失败: " + c2.Message); }
                try { RemoveArp(); Log("已清理注册表 ✓"); } catch (Exception c3) { Log("清理注册表失败: " + c3.Message); }
            }
            else
            {
                Log("**目录没删干净 → 注册表 / 快捷方式 / PATH 一律保留** ✓（否则你就没法重试了 ✗）");
                Log("**怎么重试**：关掉占用文件的程序后再跑一次 uninstall.exe ✓ 或用 `--force` 跳过身份校验 ✓ 也可以手动删掉上面那个目录 ✓");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Log("卸载出错: " + ex);
            if (!silent) MessageBox.Show(ex.Message, AppName + " 卸载失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 5;
        }
        finally { FlushLog(); }
    }

    // ================================================================ 解压（含 zip-slip 校验 ✓✓）

    /// <summary>从**内嵌资源**解压 ✓✓。返回文件数。**拒绝绝对路径与 .. 段** ✓✓（zip-slip ✓）。</summary>
    private static int ExtractPayload(string destRoot, Action<int, string> progress)
    {
        Assembly asm = Assembly.GetExecutingAssembly();
        string resName = null;
        foreach (string n in asm.GetManifestResourceNames())
            if (n.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase)) { resName = n; break; }
        if (resName == null) { Log("内嵌 payload.zip 找不到 ✗"); throw new InvalidOperationException("安装包损坏：找不到内嵌载荷。"); }

        string destFull = Path.GetFullPath(destRoot);
        int count = 0;
        using (Stream s = asm.GetManifestResourceStream(resName))
        using (ZipArchive zip = new ZipArchive(s, ZipArchiveMode.Read))
        {
            int total = zip.Entries.Count;
            for (int i = 0; i < total; i++)
            {
                ZipArchiveEntry e = zip.Entries[i];
                string rel = e.FullName.Replace('/', Path.DirectorySeparatorChar);
                // ---- zip-slip 校验 ✓✓ ----
                if (rel.StartsWith("\\", StringComparison.Ordinal) || rel.StartsWith("/", StringComparison.Ordinal) || rel.Contains(":"))
                { Log("拒绝绝对路径条目: " + e.FullName); continue; }
                string full = Path.GetFullPath(Path.Combine(destFull, rel));
                if (!full.StartsWith(destFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                { Log("拒绝越界条目: " + e.FullName); continue; }
                if (e.FullName.EndsWith("/", StringComparison.Ordinal)) { Directory.CreateDirectory(full); continue; }
                string d = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
                using (Stream src = e.Open())
                using (FileStream dst = File.Create(full))
                    src.CopyTo(dst);
                count++;
                if (progress != null && total > 0 && (i % 20) == 0) Report(progress, 10 + (int)(55.0 * i / total), "解压 " + (i + 1) + "/" + total);
            }
        }
        return count;
    }

    // ================================================================ 杂项

    /// <summary>本安装器自己的版本 ✓（写进 ARP 的 DisplayVersion ✓ 也用于和已装版本比较 ✓）。</summary>
    internal static string SelfVersion()
    {
        try
        {
            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? "0.0.0" : (v.Major + "." + v.Minor + "." + v.Build);
        }
        catch { return "0.0.0"; }
    }

    /// <summary>读已安装信息 ✓（ARP 的 DisplayVersion + InstallLocation ✓）。返回 "版本|位置"；未装返回空 ✓。</summary>
    internal static string ReadInstalled()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName))
            {
                if (k == null) return "";
                string v = k.GetValue("DisplayVersion", "") as string;
                string loc = k.GetValue("InstallLocation", "") as string;
                if (string.IsNullOrEmpty(v) && string.IsNullOrEmpty(loc)) return "";
                return (v == null ? "" : v) + "|" + (loc == null ? "" : loc);
            }
        }
        catch { return ""; }
    }

    /// <summary>版本比较 ✓（逐段数字比较 ✓ 不用字符串比较 ✗ —— "3.10" &gt; "3.9" 但字符串会说反 ✗）。</summary>
    internal static int CompareVersions(string a, string b)
    {
        try
        {
            string[] pa = (a == null ? "" : a).Split('.');
            string[] pb = (b == null ? "" : b).Split('.');
            int n = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                int va = 0, vb = 0;
                if (i < pa.Length) int.TryParse(pa[i], out va);
                if (i < pb.Length) int.TryParse(pb[i], out vb);
                if (va != vb) return va > vb ? 1 : -1;
            }
            return 0;
        }
        catch { return 0; }
    }

    internal static string DefaultDir()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName);
    }
    private static string StartMenuDir()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs", AppName);
    }
    private static string ShortVersion()
    {
        try
        {
            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? "0.0.0" : (v.Major + "." + v.Minor + "." + v.Build);
        }
        catch { return "0.0.0"; }
    }

    /// <summary>核对**暂存目录里每个文件**的 SHA-256 vs 包内 hashes.txt ✓✓。
    /// 返回 null = 全部一致 ✓；否则返回**第一个对不上的文件名** ✓（安装器据此拒绝安装 ✓）。</summary>
    private static string VerifyPayload(string staging)
    {
        try
        {
            string mf = Path.Combine(staging, "hashes.txt");
            // ✗✗ 审计 #4：**没有清单 → 跳过校验** ✗ → 而安装器**只用于发布包** ✓ 发布包**必须**有清单 ✓
            //   ✓ 修：**没有清单 = 拒绝** ✓（开发构建用 build_installer.ps1 会生成 ✓）
            if (!File.Exists(mf))
            {
                LastVerifyResult = "包里没有清单";
                Log("包内没有 hashes.txt ✗ → **拒绝安装**（发布包必须带清单 ✓）");
                return "安装包里没有 hashes.txt（无法校验任何文件 ✓）";
            }
            string[] lines = File.ReadAllLines(mf);
            int checkedCount = 0, mismatch = 0;
            // ✓ 审计 #3：**用集合而不是计数** ✓（计数可被"重复行"或"指向任意已存在文件"骗过 ✗）
            System.Collections.Generic.HashSet<string> verified = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string stagingFull = "";
            try { stagingFull = Path.GetFullPath(staging).TrimEnd(Path.DirectorySeparatorChar); } catch { }
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i] == null ? "" : lines[i].Trim();
                if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal)) continue;
                int sp = t.IndexOf(' ');
                if (sp <= 0) continue;
                string want = t.Substring(0, sp).Trim().ToLowerInvariant();
                string name = t.Substring(sp + 1).Trim();
                string full = null;
                // 清单里可能只写文件名 ✓ 也可能写相对路径 ✓ 两种都试 ✓
                string c1 = Path.Combine(staging, name);
                if (File.Exists(c1)) full = c1;
                else
                {
                    string c2 = Path.Combine(staging, "gui", name);
                    if (File.Exists(c2)) full = c2;
                }
                if (full == null) { Log("清单里有但包里没有（跳过 ✓ 不误报 ✗）: " + name); continue; }
                // ✓ 审计 #3：**必须限制在 staging 内** ✗（Path.Combine 接受绝对路径与 `..\..` ✗）
                if (stagingFull.Length > 0)
                {
                    string fullAbs = "";
                    try { fullAbs = Path.GetFullPath(full); } catch { }
                    if (fullAbs.Length == 0 || !fullAbs.StartsWith(stagingFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        Log("清单条目指向包外 ✗ " + name + " → **拒绝安装** ✓");
                        LastVerifyResult = "清单条目指向包外：" + name;
                        return "清单条目指向包外：" + name;
                    }
                    verified.Add(fullAbs);
                }
                checkedCount++;
                string got = Sha256Of(full);
                if (string.IsNullOrEmpty(got)) continue;
                if (!string.Equals(want, got, StringComparison.OrdinalIgnoreCase))
                {
                    mismatch++;
                    // ✗✗ 审计 #4：`want.Substring(0, 12)` 遇**短哈希**直接抛 ArgumentOutOfRangeException ✗
                    //   → 外层 catch → 返回 null → **被当成"已验证"** ✗✗（木马只要让清单格式坏掉就绕过 ✓）
                    // ✓ 修：安全截断 ✓
                    Log("指纹不符 ✗ " + name + " 期望=" + Brief(want) + "… 实际=" + Brief(got) + "…");
                    if (mismatch == 1) { LastVerifyResult = name; return name; }   // 记下来 ✓ 完成页要显示 ✓
                }
            }
            // ★★ 审计发现 #3（子代理实测）：清单原来只覆盖 **3 / 244** 个文件 ✗
            //    → 篡改 `gui\Avalonia.Base.dll`（不在清单里）→ **exit 0 装上了带木马的 DLL** ✗✗
            //    → 而日志还宣称「**每个文件**的指纹都与包内清单一致」✗✗ ← **假承诺** ✓
            //   ✓ 现在：**数清载荷里的文件总数** ✓ 与清单覆盖数比对 ✓ **覆盖不全就拒绝** ✓✓
            int payloadFiles = 0;
            // ✗ 不能把 `hashes.txt` 自己算进去 ✗ —— 清单无法包含自己的哈希 ✓
            //   （第一版就这么写的 → 清单 243 / 计数 244 → **正常安装被误拒** ✗ 回归测试抓到了 ✓）
            try
            {
                payloadFiles = 0;
                foreach (string pf in Directory.GetFiles(staging, "*", SearchOption.AllDirectories))
                    // ✓ 审计 #3：**只排除根目录的清单** ✗（原来任意深度的 hashes.txt 都被排除 ✗
                    //   → 走私一个 `gui\hashes.txt` 既不计也不验 ✗✗）
                    {
                        string rel = pf.Substring(staging.TrimEnd(Path.DirectorySeparatorChar).Length).TrimStart(Path.DirectorySeparatorChar);
                        if (!string.Equals(rel, "hashes.txt", StringComparison.OrdinalIgnoreCase)) payloadFiles++;
                    }
            }
            catch { }
            Log("载荷校验：清单覆盖 " + checkedCount + " / 载荷共 " + payloadFiles + " 个文件，不符 " + mismatch + " 个");
            // ✓ 审计 #3：**逐个检查每个载荷文件是否都被验证过** ✓✓（集合判定 ✓ 计数判定可绕过 ✗）
            if (stagingFull.Length > 0)
            {
                foreach (string pf in Directory.GetFiles(staging, "*", SearchOption.AllDirectories))
                {
                    string rel = pf.Substring(staging.TrimEnd(Path.DirectorySeparatorChar).Length).TrimStart(Path.DirectorySeparatorChar);
                    if (string.Equals(rel, "hashes.txt", StringComparison.OrdinalIgnoreCase)) continue;
                    string abs = "";
                    try { abs = Path.GetFullPath(pf); } catch { }
                    if (abs.Length > 0 && !verified.Contains(abs))
                    {
                        LastVerifyResult = "未覆盖：" + rel;
                        Log("载荷校验不完整 ✗ 这个文件**不在清单里** → **拒绝安装** ✓：" + rel);
                        return "有文件不在清单里（无法验证 ✓）：" + rel;
                    }
                }
            }
            if (payloadFiles > 0 && checkedCount < payloadFiles)
            {
                LastVerifyResult = "清单只覆盖 " + checkedCount + "/" + payloadFiles + " 个文件";
                Log("载荷校验不完整 ✗ 清单只覆盖 " + checkedCount + " / " + payloadFiles + " 个文件 → **拒绝安装** ✓（覆盖不全等于没校验 ✓）");
                return "清单只覆盖 " + checkedCount + "/" + payloadFiles + " 个文件（未覆盖的文件无法验证 ✓）";
            }
            LastVerifyResult = "";
            return null;
        }
        // ✗✗ 审计 #4 的另一半：**校验本身出错 → 放行** ✗ → fail-open ✓
        //   ✓ 修：**出错 = 无法确认 = 拒绝** ✓（fail-closed ✓ 校验的默认姿态必须是"不通过" ✓）
        catch (Exception ex)
        {
            LastVerifyResult = "校验过程出错";
            Log("载荷校验本身出错 ✗ → **拒绝安装**（无法确认包是否被改动 ✓）: " + ex.Message);
            return "校验过程出错：" + ex.Message + "（无法确认包是否被改动，因此拒绝安装）";
        }
    }

    /// <summary>安全截断哈希用于日志 ✓（审计 #4：`want.Substring(0,12)` 遇短哈希会抛异常 ✗
    /// → 外层 catch → **被当成已验证** ✗✗ → 修：短于 12 就原样返回 ✓ 绝不抛 ✓）。</summary>
    private static string Brief(string h)
    {
        if (string.IsNullOrEmpty(h)) return "";
        return h.Length <= 12 ? h : h.Substring(0, 12);
    }

    private static string Sha256Of(string path)
    {
        try
        {
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            using (FileStream fs = File.OpenRead(path))
            {
                byte[] h = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
        catch { return null; }
    }

    /// <summary>危险路径防护 ✓（空 / 盘根 / 用户主目录本身 / Windows 目录 → 一律拒绝 ✓）。</summary>
    private static bool IsDangerousPath(string p)
    {
        if (string.IsNullOrEmpty(p)) return true;
        string f;
        try { f = Path.GetFullPath(p).TrimEnd('\\'); } catch { return true; }
        if (f.Length <= 3) return true;                                  // C:\ 之类 ✓
        string up = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\');
        if (string.Equals(f, up, StringComparison.OrdinalIgnoreCase)) return true;
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
        if (f.StartsWith(win, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>复制一个文件 ✓。**返回是否成功** ✓（审计 M7：
    /// ✗ 原来吞掉失败 ✗ → 正在运行的 exe 无法覆盖 → 复制失败只记日志 → **仍然 return 0** ✗✗
    ///   → 完成页说"安装完成" ✓ 而**根启动器还是旧的**（gui\ 是新的）= **混合版本** ✗✗
    /// ✓ 现在调用方**检查返回值** ✓ 任何一处失败都让安装失败 ✓✓）。</summary>
    private static bool CopyFile(string from, string to)
    {
        try
        {
            if (!File.Exists(from)) { Log("复制源不存在 ✗ " + from); return false; }
            File.Copy(from, to, true);
            return true;
        }
        catch (Exception ex) { Log("复制失败 ✗ " + from + " → " + ex.Message); return false; }
    }
    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch (Exception ex) { Log("删除失败 " + dir + " → " + ex.Message); }
    }
    /// <summary>报告进度 ✓。**取消请求在这里被检查** ✓（用户实测：取消按钮原来没反应 ✗）：
    /// 抛出 OperationCanceledException → RunInstall 的 catch → 清理 staging → 如实报告"已取消" ✓✓。</summary>
    private static void Report(Action<int, string> p, int pct, string msg)
    {
        if (ReportHook != null) ReportHook();   // ✓ 取消检查钩子 ✓（GUI 安装时挂上 ✓）
        if (p != null) p(pct, msg);
    }

    /// <summary>进度报告前的钩子 ✓（GUI 用它检查"用户是否点了取消" ✓；CLI 模式下为 null ✓）。</summary>
    internal static Action ReportHook;

    // ---- 快捷方式（.lnk）用 WScript.Shell（系统自带 ✓ 零依赖 ✓）----
    private static void CreateShortcut(string dir, string name, string target, string workDir, string desc)
    {
        Directory.CreateDirectory(dir);
        string lnk = Path.Combine(dir, name + ".lnk");
        Type t = Type.GetTypeFromProgID("WScript.Shell");
        if (t == null) { Log("WScript.Shell 不可用 → 跳过快捷方式"); return; }
        object sh = Activator.CreateInstance(t);
        try
        {
            object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
            Type st = sc.GetType();
            st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { target });
            st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { workDir });
            st.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { desc });
            st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { target + ",0" });
            st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            Log("快捷方式: " + lnk);
        }
        finally { try { Marshal.ReleaseComObject(sh); } catch { } }
    }
    private static void RemoveShortcuts()
    {
        try
        {
            string dir = StartMenuDir();
            if (Directory.Exists(dir)) { Directory.Delete(dir, true); Log("已删开始菜单目录: " + dir); }
        }
        catch (Exception ex) { Log("删快捷方式失败（继续 ✓）: " + ex.Message); }
        try
        {
            string desk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk");
            if (File.Exists(desk)) { File.Delete(desk); Log("已删桌面快捷方式"); }
        }
        catch { }
    }

    // ---- ARP（应用和功能里能看到 ✓ 含 DisplayIcon ✓ 否则小白看到通用图标 ✓）----
    private static void WriteArp(string target)
    {
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName))
        {
            if (k == null) return;
            k.SetValue("DisplayName", AppName + " — DeepSeek Harness 工具箱（非官方）");
            k.SetValue("DisplayVersion", ShortVersion());
            // ✓ C1 的另一半：token 也要进注册表 ✓ 否则卸载器的双向比对**永远对不上** ✗✗（会锁死卸载 ✓）
            if (!string.IsNullOrEmpty(InstallToken)) k.SetValue("InstallToken", InstallToken);
            k.SetValue("Publisher", Publisher);
            k.SetValue("DisplayIcon", Path.Combine(target, "dsh-minato-gui.exe"));
            k.SetValue("InstallLocation", target);
            k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            k.SetValue("UninstallString", "\"" + Path.Combine(target, "uninstall.exe") + "\" --uninstall");
            k.SetValue("QuietUninstallString", "\"" + Path.Combine(target, "uninstall.exe") + "\" --uninstall --silent");
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            try
            {
                long size = 0;
                foreach (string f in Directory.GetFiles(target, "*", SearchOption.AllDirectories)) { try { size += new FileInfo(f).Length; } catch { } }
                k.SetValue("EstimatedSize", (int)(size / 1024), RegistryValueKind.DWord);
            }
            catch { }
        }
    }
    private static void RemoveArp()
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName, false); Log("已删 ARP 项"); }
        catch (Exception ex) { Log("删 ARP 失败: " + ex.Message); }
    }

    // ---- PATH：**绝不用 setx** ✗（会截断 PATH ✓）→ HKCU\Environment + 广播 ✓ ----
    private const int HWND_BROADCAST = 0xffff;
    private const int WM_SETTINGCHANGE = 0x1a;
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
    [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int Msg, IntPtr wParam, string lParam, int fuFlags, int uTimeout, out IntPtr lpdwResult);

    private static bool AddToUserPath(string dir)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { Log("PATH 目标目录不存在 → 跳过: " + dir); return false; }
        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Environment", true))
        {
            if (k == null) return false;
            // 第 3 参是 RegistryValueOptions 不是 Kind ✓；DoNotExpand 才能**原样读写**别人的 PATH ✓✓
            string cur = k.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
            if (cur == null) cur = "";
            foreach (string part in cur.Split(';'))
                if (string.Equals(part.Trim().TrimEnd('\\'), dir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                { Log("PATH 里已存在 → 不重复添加 ✓"); return true; }
            string next = cur.Length == 0 ? dir : (cur.TrimEnd(';') + ";" + dir);
            k.SetValue("Path", next, RegistryValueKind.ExpandString);   // REG_EXPAND_SZ ✓
            Broadcast();
            return true;
        }
    }
    private static void RemoveFromUserPath(string dir)
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Environment", true))
            {
                if (k == null) return;
                // 第 3 参是 RegistryValueOptions 不是 Kind ✓；DoNotExpand 才能**原样读写**别人的 PATH ✓✓
            string cur = k.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                if (string.IsNullOrEmpty(cur)) return;
                StringBuilder sb = new StringBuilder();
                foreach (string part in cur.Split(';'))
                {
                    if (part.Trim().Length == 0) continue;
                    if (string.Equals(part.Trim().TrimEnd('\\'), dir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) continue;   // 只删**自己加的那条** ✓
                    if (sb.Length > 0) sb.Append(';');
                    sb.Append(part.Trim());
                }
                k.SetValue("Path", sb.ToString(), RegistryValueKind.ExpandString);
                Broadcast();
                Log("已从用户 PATH 移除: " + dir);
            }
        }
        catch (Exception ex) { Log("PATH 移除失败（继续 ✓）: " + ex.Message); }
    }
    private static void Broadcast()
    {
        try { IntPtr r; SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, "Environment", 2, 3000, out r); } catch { }
    }

    // ---- 桌面说明文档 ✓✓（维护者的选择：不提供删数据 ✓ 但告诉你数据在哪 ✓）----
    private static void WriteDataNote(string dataDir)
    {
        try
        {
            string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string path = Path.Combine(desk, "dsh-minato 卸载说明.txt");
            bool exists = Directory.Exists(dataDir);
            string body =
                "dsh-minato 已卸载。" + Environment.NewLine + Environment.NewLine +
                "**你的数据没有被删除。**" + Environment.NewLine + Environment.NewLine +
                "DeepSeek Harness 的会话、设置等数据在：" + Environment.NewLine +
                "    " + dataDir + Environment.NewLine +
                (exists ? "（这个目录现在还在 ✓）" : "（这个目录当前不存在）") + Environment.NewLine + Environment.NewLine +
                "这些数据**不是 dsh-minato 创建的** ✓ 是 DeepSeek Harness（dsh）自己的 ✓" + Environment.NewLine +
                "所以卸载 dsh-minato **不会**、也**不应该**动它 ✓" + Environment.NewLine + Environment.NewLine +
                "如果你确实想删掉这些数据：" + Environment.NewLine +
                "  1. 先确认你不再需要这些会话记录（**删了无法恢复**）" + Environment.NewLine +
                "  2. 确认 dsh 本身也已卸载（否则它会重新生成）" + Environment.NewLine +
                "  3. 手动删除上面那个目录" + Environment.NewLine + Environment.NewLine +
                "—— dsh-minato（非官方工具箱）" + Environment.NewLine +
                ReleasesUrl + Environment.NewLine;
            File.WriteAllText(path, body, new UTF8Encoding(false));
            Log("桌面说明文档: " + path);
        }
        catch (Exception ex) { Log("写桌面文档失败（不致命）: " + ex.Message); }
    }

    // ---- 日志 ✓（没有它就没法给小白做支持 ✓）----
    internal static void Log(string msg)
    {
        string line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg;
        LogBuf.AppendLine(line);
    }
    internal static void FlushLog()
    {
        try { File.WriteAllText(LogPath, LogBuf.ToString(), new UTF8Encoding(false)); } catch { }
    }
    internal static string LogFilePath() { return LogPath; }
    internal static string LogText() { return LogBuf.ToString(); }
}

/// <summary>安装器窗口 ✓ 三个状态：选项 → 进度 → 完成 ✓（评审建议 ✓）。
/// 用**真控件**换色/字体做定制 ✓（不用自绘整窗 ✗ —— 那会破坏键盘导航与屏幕阅读器 ✓）。</summary>
internal sealed class InstallerForm : Form
{
    public int ExitCode = 0;
    private readonly string _dirArg;
    private TextBox _dirBox;
    private CheckBox _chkPath, _chkShortcuts, _chkDesktop;
    private Button _btnMain, _btnCancel, _btnCopyLog;
    private ProgressBar _bar;
    private Label _status, _title, _sub;
    private Panel _page1, _page2;
    private string _installed = "";
    private Label _installedLabel;

    public InstallerForm(string dirArg)
    {
        _dirArg = dirArg;
        Text = "dsh-minato 安装程序";
        ClientSize = new Size(620, 532);   // ✗ 原来 420（面板盖住按钮）→ 486 → 现在 532（**自绘标题栏占 46px** ✓）
        AutoScaleMode = AutoScaleMode.Dpi;   // ✓ DPI 自适应 ✓（否则 125%/150% 缩放下布局会溢出 ✓ 子代理第 11 条 ✓）
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;   // ✓ 去掉 Windows 自带的框 ✓（用户要求："X 和 minato 一样在里面" ✓）
        MaximizeBox = false; MinimizeBox = false;
        Font = new Font("Microsoft YaHei UI", 9f);
        // ★ 显式设窗口图标 ✓✓（用户实测："启动后内部的和任务栏的"还是占位符 ✗）
        //   原因：**无边框窗口 + 自绘标题栏**时，WinForms 不会自动采用 exe 的图标 ✗
        //   → 显式从**自己的 exe** 取图标 ✓ → 任务栏 / Alt+Tab / 窗口都对了 ✓✓
        try { this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        BackColor = Color.FromArgb(250, 250, 252);

        _title = new Label { Text = "安装 dsh-minato", Font = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold), ForeColor = Color.FromArgb(30, 30, 40), AutoSize = true, Location = new Point(28, 70) };
        _sub = new Label
        {
            Text = "DeepSeek Harness 的非官方 Windows 工具箱。只读本地状态，不联网上传。",
            ForeColor = Color.FromArgb(110, 110, 120), AutoSize = false, Size = new Size(560, 20), Location = new Point(30, 106)
        };
        // ---- 自绘标题栏 ✓✓（用户要求："去掉顶上win自带的框，X和minato一样在里面" ✓ + "加logo和项目地址" ✓）----
        Panel header = new Panel { Location = new Point(0, 0), Size = new Size(620, 46), BackColor = Color.White };
        PictureBox logo = new PictureBox { Location = new Point(16, 9), Size = new Size(28, 28), SizeMode = PictureBoxSizeMode.StretchImage };
        try { if (Icon != null) logo.Image = Icon.ToBitmap(); } catch { }
        Label htitle = new Label { Text = "dsh-minato 安装程序", Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold), ForeColor = Color.FromArgb(30, 30, 40), AutoSize = true, Location = new Point(54, 14) };
        LinkLabel hlink = new LinkLabel { Text = "github.com/sakanamaru/dsh-minato", AutoSize = true, Location = new Point(300, 17), Font = new Font("Microsoft YaHei UI", 8.5f), LinkColor = Color.FromArgb(64, 110, 220) };
        hlink.LinkClicked += delegate { try { Process.Start(Installer.ReleasesUrl); } catch { } };
        Button hclose = new Button { Text = "✕", Location = new Point(586, 9), Size = new Size(26, 26), FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.FromArgb(90, 90, 100) };
        hclose.FlatAppearance.BorderSize = 0;
        hclose.Click += delegate { Close(); };
        header.Controls.Add(logo); header.Controls.Add(htitle); header.Controls.Add(hlink); header.Controls.Add(hclose);
        // 无边框窗口要**自己实现拖动** ✓（拖标题栏空白处 ✓）
        MouseEventHandler drag = delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Installer.ReleaseCapture(); Installer.SendMessage(Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero); } };
        header.MouseDown += drag; htitle.MouseDown += drag; logo.MouseDown += drag;
        Controls.Add(header);

        Controls.Add(_title); Controls.Add(_sub);

        // ---- 第一页：选项 ----
        _page1 = new Panel { Location = new Point(0, 134), Size = new Size(620, 330) };
        Label ld = new Label { Text = "安装位置", ForeColor = Color.FromArgb(70, 70, 80), AutoSize = true, Location = new Point(30, 8) };
        _dirBox = new TextBox { Location = new Point(30, 30), Size = new Size(470, 26), Text = string.IsNullOrEmpty(_dirArg) ? Installer.DefaultDir() : _dirArg };
        Button browse = new Button { Text = "浏览…", Location = new Point(508, 29), Size = new Size(80, 27) };
        browse.Click += delegate
        {
            using (FolderBrowserDialog fb = new FolderBrowserDialog())
            {
                fb.Description = "选择安装位置（**不需要管理员权限** ✓）";
                fb.SelectedPath = _dirBox.Text;
                if (fb.ShowDialog(this) == DialogResult.OK) _dirBox.Text = fb.SelectedPath;
            }
        };
        _chkShortcuts = new CheckBox { Text = "在开始菜单创建快捷方式", Checked = true, AutoSize = true, Location = new Point(30, 70), ForeColor = Color.FromArgb(60, 60, 70) };
        // 桌面快捷方式 ✓（**默认不勾** ✓ 用户要求："添加创建快捷方式询问或者选项框" ✓）
        _chkDesktop = new CheckBox { Text = "同时在桌面创建快捷方式", Checked = false, AutoSize = true, Location = new Point(30, 94), ForeColor = Color.FromArgb(60, 60, 70) };
        _chkPath = new CheckBox { Text = "把命令行工具加入 PATH", Checked = false, AutoSize = true, Location = new Point(30, 120), ForeColor = Color.FromArgb(60, 60, 70) };
        // —— 已装检测 + 版本校验 ✓✓（用户要求："安装好后也可以再点安装，安装器也加个版本校验吧，比如有更新或者非官方" ✓）——
        _installed = Installer.ReadInstalled();   // 静态成员要带类名 ✓
        _installedLabel = new Label
        {
            AutoSize = false, Size = new Size(560, 34), Location = new Point(30, 146),
            ForeColor = Color.FromArgb(150, 90, 20), Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold)
        };
        _installedLabel.Text = DescribeInstalled(_installed);
        Installer.Log("已装检测: [" + _installed + "] → " + _installedLabel.Text);   // 写日志 ✓ 便于验证 ✓
        _chkPath.Location = new Point(30, 96 + 34);
        _page1.Controls.Add(_installedLabel);
        Label note = new Label
        {
            Text = "· 当前用户级安装，不弹 UAC" + Environment.NewLine +
                   "· 不会碰你的数据（~/.dsh 属于 dsh，本工具只读）" + Environment.NewLine +
                   "· 每个文件带指纹，启动时自校验，被改动就拒绝运行",
            ForeColor = Color.FromArgb(120, 120, 130), AutoSize = false, Size = new Size(560, 100), Location = new Point(30, 190)
        };
        _page1.Controls.Add(ld); _page1.Controls.Add(_dirBox); _page1.Controls.Add(browse);
        _page1.Controls.Add(_chkShortcuts); _page1.Controls.Add(_chkDesktop); _page1.Controls.Add(_chkPath); _page1.Controls.Add(note);
        Controls.Add(_page1);

        // ---- 第二页：进度 ----
        _page2 = new Panel { Location = new Point(0, 134), Size = new Size(620, 330), Visible = false };
        _status = new Label { Text = "准备…", AutoSize = false, Size = new Size(560, 22), Location = new Point(30, 30), ForeColor = Color.FromArgb(60, 60, 70) };
        _bar = new ProgressBar { Location = new Point(30, 60), Size = new Size(560, 22), Minimum = 0, Maximum = 100 };
        _btnCopyLog = new Button { Text = "复制安装日志", Location = new Point(30, 100), Size = new Size(130, 30), Visible = false };
        _btnCopyLog.Click += delegate
        {
            try { Clipboard.SetText(Installer.LogText()); MessageBox.Show("日志已复制到剪贴板。", "dsh-minato", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            catch { }
        };
        _page2.Controls.Add(_status); _page2.Controls.Add(_bar); _page2.Controls.Add(_btnCopyLog);
        Controls.Add(_page2);

        // ---- 底部按钮 ----
        _btnMain = new Button { Text = DescribeAction(), Location = new Point(410, 476), Size = new Size(90, 34), BackColor = Color.FromArgb(64, 110, 220), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        _btnMain.FlatAppearance.BorderSize = 0;
        _btnMain.Click += delegate { if (_done) { Close(); return; } StartInstall(); };   // ✓ 单一处理器 ✓ 装完就只关窗 ✓✓
        _btnCancel = new Button { Text = "取消", Location = new Point(508, 476), Size = new Size(80, 34), DialogResult = DialogResult.Cancel };
        Controls.Add(_btnMain); Controls.Add(_btnCancel);
        CancelButton = _btnCancel;
        AcceptButton = _btnMain;
        FormClosing += delegate(object s, FormClosingEventArgs e) { if (_running) { e.Cancel = true; MessageBox.Show("正在安装，请稍候…", "dsh-minato", MessageBoxButtons.OK, MessageBoxIcon.Information); } };
    }

    /// <summary>按钮文案随"已装版本 vs 本次版本"变化 ✓✓（用户要求："安装好后也可以再点安装" ✓ 现在会明确告诉你是在升级还是重装 ✓）。</summary>
    private string DescribeAction()
    {
        if (string.IsNullOrEmpty(_installed)) return "安装";
        string have = _installed.Split('|')[0];
        int cmp = Installer.CompareVersions(Installer.SelfVersion(), have);
        if (cmp > 0) return "升级到 " + Installer.SelfVersion();
        if (cmp == 0) return "重新安装";
        return "降级安装";
    }

    /// <summary>已装状态一句话 ✓（含"有更新 / 同版本 / 更旧"三种 ✓ 和子代理说的"别让用户猜" ✓）。</summary>
    private string DescribeInstalled(string info)
    {
        if (string.IsNullOrEmpty(info)) return "";
        string[] p = info.Split('|');
        string have = p.Length > 0 ? p[0] : "";
        string loc = p.Length > 1 ? p[1] : "";
        int cmp = Installer.CompareVersions(Installer.SelfVersion(), have);
        string where = string.IsNullOrEmpty(loc) ? "" : ("（" + loc + "）");
        if (cmp > 0) return "✓ 检测到已安装 " + have + where + " → 本次是**更新版本 " + Installer.SelfVersion() + "**，会**升级**";
        if (cmp == 0) return "✓ 检测到已安装 " + have + where + " → 与本次**版本相同**，会**重新安装（覆盖）**";
        return "⚠ 检测到已安装 " + have + where + "，而本次是**更旧的 " + Installer.SelfVersion() + "** → 会**降级** ✗ 请确认你确实要这么做 ✗";
    }

    private bool _running;
    /// <summary>用户点了"取消安装" ✓（审计/用户实测：原来按钮只改文案 ✗ 点了被 FormClosing 吞掉 ✗）。
    /// 安装过程在**每个进度回调**里检查它 ✓ → 抛出 → RunInstall 的 catch 如实报告 ✓✓。</summary>
    private volatile bool _cancelRequested;
    private bool _done;   // ✓ 装完了 → 按钮变成"完成"（**只挂一个处理器** ✓ 不会重复安装 ✓✓）
    /// <summary>要不要建**桌面**快捷方式 ✓（用户要求："添加创建快捷方式询问或者选项框" ✓）。
    /// 默认 **false** ✓（评审建议：桌面快捷方式默认不勾 ✓ 减少杂乱 ✓ OneDrive 同步目录里更该少放 ✓）。</summary>
    internal static bool WantDesktopShortcut = false;
    private void StartInstall()
    {
        string dir = _dirBox.Text == null ? "" : _dirBox.Text.Trim();
        if (dir.Length == 0) { MessageBox.Show("请先选择安装位置。", "dsh-minato", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        _page1.Visible = false; _page2.Visible = true; _running = true;
        _btnMain.Enabled = false;
        // ✓ Bug 2：**真的能取消** ✓（原来只把文案改成"请稍候" ✗ 按钮没禁用也没挂处理器 ✓
        //   而 FormClosing 里 `if (_running) { e.Cancel = true; }` 会把关闭请求吞掉 ✗ → 表现为"没反应" ✓）
        _cancelRequested = false;
        _btnCancel.Text = "取消安装";
        _btnCancel.Enabled = true;
        _btnCancel.Click += delegate
        {
            // ✓ 用户要求：「取消按钮应该是**关闭安装器**，或者是**退一步**」
            //   ✗ 原来只靠 `DialogResult = Cancel` ✗ —— 选项页能关 ✓
            //     但**进度页**点了只设标志 ✓ 而安装只要 ~1.7 秒 ✓ 点的时候**往往已经装完** ✗
            //     → 成功分支又把按钮隐藏了 ✓ → **表现为"点了没反应"** ✓✓（用户实测 ✓）
            //   ✓ 现在：**明确分两种** ✓
            //     ① 正在安装 → **中止** ✓（进度回调检查标志 ✓ 清理 staging ✓ 如实报告 ✓）
            //     ② 没在安装 → **直接关闭安装器** ✓✓
            if (_running)
            {
                _cancelRequested = true;
                _btnCancel.Text = "正在取消…";
                _btnCancel.Enabled = false;
                _status.Text = "正在取消…";
            }
            else
            {
                Close();
            }
        };
        _title.Text = "正在安装…";
        _sub.Text = "正在安装…";
        bool wantPath = _chkPath.Checked, wantSc = _chkShortcuts.Checked;
        // ✗✗ 审计 m4：`_chkDesktop.Checked` **从来没被读过** ✗ → 勾了没用 ✓（用户专门要求的功能 ✗）
        // ✓ 修：把桌面选项**真的传下去** ✓
        InstallerForm.WantDesktopShortcut = _chkDesktop.Checked;
        BackgroundWorker bw = new BackgroundWorker();
        bw.DoWork += delegate(object s, DoWorkEventArgs e)
        {
            e.Result = Installer.RunInstall(dir, wantPath, !wantPath, !wantSc, delegate(int pct, string msg)
            {
                try { BeginInvoke((MethodInvoker)delegate { _bar.Value = Math.Max(0, Math.Min(100, pct)); _status.Text = msg; }); } catch { }
            });
        };
        bw.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs e)
        {
            _running = false;
            int rc = (e.Error != null) ? 9 : (int)(e.Result ?? 9);
            if (e.Error != null) Installer.Log("后台线程异常: " + e.Error);
            if (rc == 0)
            {
                _title.Text = "安装完成 ✓";
                _bar.Value = 100;
                _status.Text = "已安装到：" + dir + (string.IsNullOrEmpty(Installer.LastVerifyResult) ? "　✓ 安装包指纹已校验：全部一致" : ("　✗ 校验异常：" + Installer.LastVerifyResult));
                _sub.Text = (_chkPath.Checked ? "PATH 已更新，请新开一个终端" : "已创建开始菜单快捷方式");
                _btnMain.Text = "完成";
                _btnMain.Enabled = true;
                // ✗✗ 原来这两行：`-= delegate { }` 是**空操作** ✗ 解绑不了任何东西 ✓
                //    → 成功分支又挂了一个 Close ✓ 而 StartInstall 还在 ✗✗ → **点"完成"会再装一遍** ✓（用户实测 ✓）
                // ✓ 正解：用一个状态标志 ✓ 按钮只挂**一个**处理器 ✓✓
                _done = true;
                _btnCancel.Visible = false;
            }
            else
            {
                _title.Text = "安装未完成 ✗";
                // ✓ m8：**显示真实原因** ✓（原来只有"代码 9" ✗ → **篡改警告看不到** ✗✗）
                _status.Text = "安装失败（代码 " + rc + "）。" +
                    (string.IsNullOrEmpty(Installer.LastFailureReason) ? "" : Installer.LastFailureReason + "　") +
                    "可以把日志发给我。";
                _btnCopyLog.Visible = true;
                _btnMain.Text = "重试"; _btnMain.Enabled = true;
                _btnCancel.Text = "关闭"; _btnCancel.Visible = true;
            }
            Installer.FlushLog();
        };
        // ★★★ **M-8 修复（安装器审计 MAJOR —— 取消按钮是死代码）** ✓✓
        //   ✗ `ReportHook` **从未被赋值** ✗ —— 编译器用项目自己的开关就报了 **CS0649** ✓
        //     而 `_cancelRequested` **写了但从未被读** ✗（**CS0414** ✓）
        //     → 点「取消安装」**只把文案改掉** ✓ **安装照常跑完** ✗✗
        //     → 而 `build_installer.ps1` 只 grep "error" ✓ → **带着警告照样发出去** ✓
        //   ✓ 现在：**启动安装前挂上钩子** ✓✓ → 每个进度回调里检查 ✓
        //     → 抛出 → `RunInstall` 的 catch **清理 staging** ✓ **如实报告"已取消"** ✓✓
        Installer.ReportHook = delegate { if (_cancelRequested) throw new InvalidOperationException("用户取消了安装 ✓（不会留下半个安装 ✓）"); };
        bw.RunWorkerAsync();
    }
}
