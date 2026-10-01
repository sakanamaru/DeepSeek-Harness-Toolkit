// ============================================================================
//  DeepSeek Harness Toolkit V2.7.3  ——  DeepSeek Harness(dsh) 安装 / 启动 / 卸载 / 备份恢复工具箱
// ----------------------------------------------------------------------------
//  v1 脚本协助：SOGR-Momono Dango（QwenPaw/DeepseekAPI-V4-Flash-0731）
//  v2 重构封装：DeepSeek DSH（DSH/DeepseekAPI-V4-Flash-0731）
//
//  功能：安装/修复、启动 Web 界面、运行状态监控、卸载（含两步确认清数据）、
//        数据备份/恢复、多语言、自动倒计时选择、彩色输出。
//
//  编译： csc.exe /nologo /optimize+ /target:exe /win32icon:icon.ico /out:"DeepSeek Harness Toolkit.exe" dsh_v2.cs /warn:4
// ============================================================================







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

[assembly: AssemblyTitle("DeepSeek Harness Toolkit V2.7.3")]
[assembly: AssemblyDescription("DeepSeek Harness(dsh) 安装/启动/卸载/备份恢复工具箱。v1: SOGR-Momono Dango(QwenPaw/DeepseekAPI-V4-Flash-0731)；v2: DeepSeek DSH(DSH/DeepseekAPI-V4-Flash-0731)；GitHub @sakanamaru")]
[assembly: AssemblyCompany("SOGR-Momono Dango / DeepSeek DSH / @sakanamaru")]
[assembly: AssemblyProduct("DeepSeek Harness Toolkit")]
[assembly: AssemblyVersion("2.7.3.0")]
[assembly: AssemblyFileVersion("2.7.3.0")]

partial class Program
{

    static bool IsZh
    {
        get
        {
            if (lang == Lang.Zh) return true;
            if (lang == Lang.En) return false;
            try { return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"; }
            catch { LogErr("IsZh: 读取系统语言异常，默认中文"); return true; }
        }
    }




#if UNIT
    // 单元测试代理（仅 /define:UNIT 构建存在）：嵌套类可访问外层 private 成员，生产构建无此类型
    public static class Test
    {
        public static string PathP(string p) { return Program.P(p); }
        public static string PathTrim(string p) { return Program.TrimP(p); }
        public static bool WorkspaceOk(string p) { return Program.LooksLikeWorkspace(p); }
        public static bool DshData(string p) { return Program.LooksLikeDshData(p); }
        public static bool PortOpen(int port, int ms) { return Program.IsPortOpen(port, ms); }
        public static void SetKeep(int v) { Program.cfgKeep = v; }
        public static void SetStateDir(string d) { Program.StateDir = d; }
        public static bool RotateLog(string file, long max) { return Program.RotateLogIfNeeded(file, max); }
        public static string BkSuffix(BackupKind k) { return Program.BackupSuffix(k); }
        public static bool IsAutoName(string n) { return Program.IsAutoBackupName(n); }
        public static List<string> Retention() { return Program.EnforceBackupRetention(); }
        public static ServiceState JudgeState(bool port, bool http) { return Program.JudgeState(port, http); }
        public static ServiceState JudgeState3(bool port, bool http, Func<bool> listener) { return Program.JudgeState3(port, http, listener); }
        public static int CmpVer(string a, string b) { return Program.CompareVersions(a, b); }
        public static string ParseTag(string body) { return Program.ParseLatestTag(body); }
        public static string Latest() { return Program.LatestVersion(); }
        public static string CurVer() { return Program.CurrentVersion(); }
        public static void SetHttpGet(Func<string, int, string> f) { Program.HttpGetImpl = f; }
        public static string[] ParseVersions(string raw) { return Program.ParseNpmVersions(raw); }
        public static string[] FilterVers(string[] all, int n) { return Program.FilterVersions(all, n); }
        public static bool CleanVer(string v) { return Program.IsCleanVersion(v); }
        public static string SanitizeLatest(string raw) { return Program.SanitizeLatestVersion(raw); }
        public static string DshVersions() { return Program.cfgDshVersions; }
        public static void RecordVer(string v) { Program.RecordDshVersion(v); }
        public static void ResetVersions() { Program.cfgDshVersions = ""; }
        public static string DoBackup(string src) { return Program.DoBackup(src); }
        public static string DoBackupKind(string src, BackupKind k) { return Program.DoBackup(src, null, k); }
        public static bool RootMarker(string dir) { return Program.RootMarkerValid(dir); }
        public static bool ValidBackup(string dir) { return Program.IsValidBackupDir(dir); }
        public static string ResolveBackup(string dir) { return Program.ResolveBackupDir(dir); }
        public static string Shortcut(string desktopDir) { return Program.CreateDesktopShortcut(desktopDir); }
        public static string ShortcutT(string desktopDir, string exe, string name, string desc) { return Program.CreateDesktopShortcut(desktopDir, exe, name, desc); }
        public static string ShortcutNameT(string raw) { return Program.SafeShortcutName(raw); }
        public static string Desktop(string dir) { string old = Environment.GetEnvironmentVariable("DSH_TEST_DESKTOP"); try { Environment.SetEnvironmentVariable("DSH_TEST_DESKTOP", dir); return Program.DesktopDir(); } finally { Environment.SetEnvironmentVariable("DSH_TEST_DESKTOP", old); } }
        public static bool ShortcutExists(string dir) { string old = Environment.GetEnvironmentVariable("DSH_TEST_DESKTOP"); try { Environment.SetEnvironmentVariable("DSH_TEST_DESKTOP", dir); return Program.ShortcutExists(); } finally { Environment.SetEnvironmentVariable("DSH_TEST_DESKTOP", old); } }
        public static int ParsePort(string netstat, int port) { return Program.ParsePortPid(netstat, port); }
        public static string NIValidateRestorePath(string pathArg, string backupsRoot) { return Program.NIValidateRestorePath(pathArg, backupsRoot); }
        public static bool IsDshCmd(string cmdline) { return Program.IsDshCommandLine(cmdline); }
        // ---- v2.5 doctor 体检 ----
        public static string Sand(string s) { return Program.SanitizeForReport(s); }
        public static DocItem DI(string cat, int level, string text) { return new DocItem(cat, level, text); }
        public static string DocSum(List<DocItem> items) { return Program.DoctorSummary(items); }
        public static long DirSizeOf(string d) { return Program.DirSize(d); }
        public static string HumanOf(long b) { return Program.HumanSize(b); }
        public static string DocLvl(int level) { return Program.DocLevel(level); }
        public static string ManHash(string manifest, string name) { return Program.ParseManifestHash(manifest, name); }
        public static bool? SelfInteg() { return Program.SelfIntegrity(); }
        public static long[] PlanMergeT(string src, string dst, string skipDir, string skipFile) { return Program.PlanMerge(src, dst, skipDir, skipFile); }
        public static long[] PlanMergeLegacyT(string src, string dst) { return Program.PlanMergeLegacy(src, dst); }
        public static long[] PlanDeleteT(string root) { return Program.PlanDelete(root); }
        public static string KindName(string n) { return Program.BackupKindName(n); }
        public static string ValExport(string src, string to, string root) { return Program.NIValidateExport(src, to, root); }
        public static string ValBkDel(string src, string root) { return Program.NIValidateBackupDelete(src, root); }
        public static string LatestBk(string suffix) { return Program.LatestBackupWithSuffix(suffix); }
        public static int CountBk() { return Program.CountValidBackups(); }
        public static string ValCfg(string key, string value) { return Program.NIValidateConfigSet(key, value); }
        public static string UptimeT(double seconds) { return Program.FormatUptime(TimeSpan.FromSeconds(seconds)); }
        // ---- v2.7 profile 诊断与修复 ----
        public static string[] ProfChkT(string text)
        {
            List<string> r = new List<string>();
            foreach (Program.ProfileFinding f in Program.ProfileCheckText(text, "T")) r.Add(f.Line + "|" + f.Id + "|" + f.Missing);
            return r.ToArray();
        }
        public static string[] FileUrlT(string url) { string frag; string p = Program.FileUrlToPath(url, out frag); return new string[] { p, frag }; }
        public static string[] BootDiagT(string text)
        {
            Program.BootDiagResult r = Program.BootDiagText(text);
            return new string[] { r.Recognized ? "OK" : "FAIL", r.Kind, r.Plugin, r.Entry, r.File, r.Line.ToString(), r.Hint, r.FirstError };
        }
        public static string[] PatchT(string text, string id, string key, string value)
        {
            Program.ProfilePatchPlan p = Program.PlanProfilePatch(text, id, key, value);
            return new string[] { p.Noop ? "NOOP" : (p.InsertAt < 0 ? "FAIL:" + p.Reason : "PLAN"), p.Line.ToString(), p.Indent, p.NewText == null ? "" : p.NewText };
        }
        public static string PatchApplyT(string file, string id, bool simulateFail, out string bk) { return Program.ProfilePatchApply(file, id, "maxDepth", "provider-managed", simulateFail, out bk); }
        // ---- v2.7.2 隔离处方 ----
        public static string[] PatchDisableT(string text, string id)
        {
            Program.ProfilePatchPlan p = Program.PlanProfileDisable(text, id);
            return new string[] { p.Noop ? "NOOP" : (p.InsertAt < 0 ? "FAIL:" + p.Reason : "PLAN"), p.Line.ToString(), p.NewText == null ? "" : p.NewText };
        }
        public static string PatchDisableApplyT(string file, string id, bool simulateFail, out string bk) { return Program.ProfilePatchDisableApply(file, id, simulateFail, out bk); }
        public static bool PatchHasDisabledT(string text, string id) { return Program.PatchHasDisabled(text, id); }
        public static bool SafePatchIdT(string id) { return Program.IsSafePatchId(id); }
        // ---- v2.8 平台接缝（契约测试用）----
        public static string SeamActivePaths() { return Program.Platform.Paths.GetType().Name; }
        public static string SeamNormalize(string p) { return Program.Platform.Paths.Normalize(p); }
        public static string SeamTrim(string p) { return Program.Platform.Paths.TrimTrailingSep(p); }
        public static string SeamDataRoot() { return Program.Platform.Paths.DataRoot(); }
        public static string SeamBackupsRoot() { return Program.Platform.Paths.BackupsRoot(); }
        public static string SeamCapture(string exe, string args) { return Program.Platform.Shell.Capture(exe, args); }
        public static bool SeamPortOpen(int port, int ms) { return Program.Platform.Probe.PortOpen(port, ms); }
        public static string SeamShortcut(string dir, string exe, string name, string desc) { return Program.Platform.Shortcuts.Create(dir, exe, name, desc); }
        public static string DataRootT() { return Program.DataRoot(); }
        public static string BackupsRootT() { return Program.BackupsRoot(); }
        // ---- v2.8 阶段 3：Linux 实现契约（在 Windows 上靠环境变量驱动验证其逻辑）----
        public static string LinuxDataRootT() { return new LinuxPathService().DataRoot(); }
        public static string LinuxDesktopDirT() { return new LinuxPathService().DesktopDir(); }
        public static string LinuxWorkspaceRootT() { return new LinuxPathService().WorkspaceRoot(); }
        public static string LinuxNormalizeT(string p) { return new LinuxPathService().Normalize(p); }
        public static string LinuxShortcutCreateT(string dir, string exe, string name, string desc) { return new LinuxShortcutService().Create(dir, exe, name, desc); }
        public static string CleanScalarT(string v) { return Program.CleanYamlScalar(v); }
    }

#endif
}
