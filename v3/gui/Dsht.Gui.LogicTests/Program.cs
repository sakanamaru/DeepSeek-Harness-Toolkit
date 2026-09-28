using System;
using System.Collections.Generic;
using Dsht.Gui.Avalonia.Markers;

namespace Dsht.Gui.LogicTests
{
    /// <summary>GUI 呈现层逻辑测试（零第三方断言）：验证标记行解析器 —— 界面能信任数据的前提。
    /// 运行：dotnet run --project v3/gui/Dsht.Gui.LogicTests -c Release</summary>
    internal static class Program
    {
        private static int _pass;
        private static int _fail;

        private static void Check(string name, bool ok)
        {
            if (ok) { _pass++; Console.WriteLine("  [PASS] " + name); }
            else { _fail++; Console.WriteLine("  [FAIL] " + name); }
        }

        private static int Main()
        {
            Console.WriteLine("== GUI 呈现层逻辑测试（标记行解析）==");

            string real =
                "SESSIONS_OK 2\n" +
                "SESSIONS_NONBLANK 1\n" +
                "SESSIONS_LIVE 1\n" +
                "SESSIONS_SOURCE snapshot\n" +
                "SESSIONS_ROOT C:\\Users\\x\\.dsh\\storages\\session_projcache\\sessions\n" +
                "SESSION 0052ed1f-1b9a-4cac-a27a-310abb951edc title=会话%20A%25B created=2026-09-23T01:48:28Z last=2026-09-23T01:48:28Z turns=1 steps=50 in=5161243 out=68886 cacheRead=4964096 hit=96.2 decode=195.7 ttft=187588 ctx=0.0 blank=0 live=1\n" +
                "SESSION abc12345 created=unknown last=unknown turns=0 steps=0 in=0 out=0 cacheRead=0 hit=unknown decode=unknown ttft=unknown ctx=unknown blank=1 live=0\n" +
                "SESSIONS_TOTAL in=5161243 out=68886 cacheRead=4964096 hit=97.1 decode=108.6";

            SessionsSnapshot s = SessionsMarkers.Parse(real);
            Check("解析成功标志与计数", s.Ok && s.Count == 2 && s.NonBlank == 1 && s.Live == 1);
            Check("数据来源与投影根", s.Source == "snapshot" && s.Root.Contains("session_projcache"));
            Check("来源说明文字区分 snapshot/disk", s.SourceText.Contains("桥接插件快照") && SessionsMarkers.Parse("SESSIONS_SOURCE disk").SourceText.Contains("磁盘投影"));
            Check("会话行数", s.Rows.Count == 2);
            SessionRow a = s.Rows[0];
            Check("行：id 与短 id", a.Id == "0052ed1f-1b9a-4cac-a27a-310abb951edc" && a.ShortId == "0052ed1f");
            Check("行：标题解析并解码（%20 空格 / %25 百分号）", a.Title == "会话 A%B" && a.TitleText == "会话 A%B");
            Check("行：无标题 → 人话兜底（不留空白）", s.Rows[1].Title == "" && s.Rows[1].TitleText == "（未命名会话）");
            Check("解码：- 与空 → 空串；非法 % 原样保留", SessionsMarkers.Decode("-") == "" && SessionsMarkers.Decode("") == "" && SessionsMarkers.Decode("50%") == "50%");
            Check("行：turns/steps/in/out/cacheRead", a.Turns == 1 && a.Steps == 50 && a.In == 5161243 && a.Out == 68886 && a.CacheRead == 4964096);
            Check("行：命中率/速度/压力解析为数值", Math.Abs(a.HitPercent - 96.2) < 0.001 && Math.Abs(a.DecodeTps - 195.7) < 0.001 && Math.Abs(a.CtxPercent - 0.0) < 0.001);
            Check("行：ttft 原值", a.TtftMs == 187588);
            Check("行：live/blank 标记", a.Live && !a.Blank && a.LiveText == "运行中");
            Check("行：时间戳保留 ISO 文本", a.Created == "2026-09-23T01:48:28Z" && a.Last == "2026-09-23T01:48:28Z");
            Check("行：token 用人读格式（K/M，InvariantCulture）", a.InText == "5.2M" && a.OutText == "68.9K" && a.CacheReadText == "5.0M");
            SessionRow b = s.Rows[1];
            Check("行：unknown 一律为 -1 / 空串（不假装 0）", b.HitPercent < 0 && b.DecodeTps < 0 && b.CtxPercent < 0 && b.TtftMs == -1 && b.Last == "" && b.LastShort == "unknown");
            Check("行：unknown 的显示文本", b.HitText == "unknown" && b.DecodeText == "unknown" && b.TtftText == "unknown" && b.CtxText == "unknown");
            Check("行：空会话标记与文案", b.Blank && !b.Live && b.LiveText == "空会话");
            Check("行：短时间与状态语义", a.LastShort == "09-23 01:48" && a.StatusKind == 0 && b.StatusKind == 2);
            Check("行：等级与条形（命中 96.2 → 高/96；压力 0 → 低/0）", a.HitLevel == 3 && a.HitBar == 96 && a.CtxLevel == 1 && a.CtxBar == 0);
            Check("行：首 token 超 1 秒按秒显示", a.TtftText == "187.6 s");
            Check("行：unknown 的等级/条形不假装", b.HitLevel == 0 && b.HitBar == 0 && b.CtxLevel == 0);
            List<SessionRow> f1 = SessionsView.Filter(s.Rows, SessionsView.FilterNonBlank);
            List<SessionRow> f2 = SessionsView.Filter(s.Rows, SessionsView.FilterLive);
            Check("过滤：非空 1 条 / 运行中 1 条 / 全部 2 条", f1.Count == 1 && f2.Count == 1 && SessionsView.Filter(s.Rows, SessionsView.FilterAll).Count == 2);
            Check("排序：按输入 token 降序时第一条是量大的那条", SessionsView.Sort(s.Rows, 1)[0].In == 5161243);
            Check("排序：解码速度降序（未知排最后）", SessionsView.Sort(s.Rows, 4)[0].DecodeTps == 195.7);
            Check("排序：缓存命中率升序时未知排最后", SessionsView.Sort(s.Rows, 2)[0].HitPercent == 96.2);
            SessionsView.AttachBars(s.Rows);
            Check("条形：最大者为 100，未知/零不越界", s.Rows[0].TokenBar == 100 && s.Rows[1].TokenBar == 0);
            Check("汇总：总量与加权命中率/速度", s.TotalIn == 5161243 && s.TotalOut == 68886 && s.TotalCacheRead == 4964096 && Math.Abs(s.TotalHitPercent - 97.1) < 0.001 && Math.Abs(s.TotalDecodeTps - 108.6) < 0.001);

            SessionsSnapshot fail = SessionsMarkers.Parse("SESSIONS_FAIL 没有可读的会话投影（dsh 未初始化）");
            Check("失败行：Ok=false 且带原因", !fail.Ok && fail.FailReason.Contains("没有可读的会话投影") && fail.Rows.Count == 0);

            Check("空输入 / null → 空快照（不抛）", SessionsMarkers.Parse("").Rows.Count == 0 && SessionsMarkers.Parse(null).Rows.Count == 0);
            Check("脏数据：无法解析的行被跳过，其它行照常", SessionsMarkers.Parse("随便一行垃圾\nSESSIONS_OK 1\nSESSION onlyid\nSESSION x1 turns=2\nSESSIONS_TOTAL in=oops").Rows.Count == 2);
            Check("脏数据：数值非法 → 回退（不抛、不假装）", SessionsMarkers.Parse("SESSION x1 turns=abc hit=zzz blank=9").Rows[0].Turns == 0 && SessionsMarkers.Parse("SESSION x1 hit=zzz").Rows[0].HitPercent < 0);
            Check("CRLF 与前后空白也能解析", SessionsMarkers.Parse("  SESSIONS_OK 1  \r\nSESSION  a1  turns=3  \r\n").Rows[0].Turns == 3);
            Check("仅 SESSIONS_OK 时 Ok=true 且无行", SessionsMarkers.Parse("SESSIONS_OK 0").Ok && SessionsMarkers.Parse("SESSIONS_OK 0").Rows.Count == 0);

            Console.WriteLine();
            string prof = "PROFILES_OK 2\nPROFILE web form=web bundles=3 thirdparty=1\nBUNDLE web @deepseek-ai/dsh-base official\nBUNDLE web @deepseek-ai/dsh-web-app official\nBUNDLE web dsh-web-search-tavily thirdparty\nPROFILE bare form=unknown bundles=0 thirdparty=0";
            ProfilesSnapshot ps = ProfilesMarkers.Parse(prof);
            Check("profiles：解析成功与计数", ps.Ok && ps.Count == 2 && ps.Profiles.Count == 2);
            Check("profiles：形态文案与色号", ps.Profiles[0].FormText.Contains("Web") && ps.Profiles[0].FormKind == 0 && ps.Profiles[1].FormKind == 3);
            Check("profiles：组合包与第三方计数", ps.Profiles[0].Bundles == 3 && ps.Profiles[0].ThirdParty == 1 && ps.Profiles[0].CountText.Contains("第三方 1"));
            Check("profiles：插件归属（官方 2 / 第三方 1）", ps.Profiles[0].Items.Count == 3 && ps.Profiles[0].Items[2].KindText == "第三方" && ps.Profiles[0].Items[0].Official);
            Check("profiles：失败与空输入不抛", !ProfilesMarkers.Parse("PROFILES_FAIL 找不到 profiles 目录").Ok && ProfilesMarkers.Parse("").Profiles.Count == 0);
            Check("profiles：脏行跳过", ProfilesMarkers.Parse("garbage\nPROFILES_OK 1\nPROFILE x form=web\nBUNDLE nobody a official").Profiles.Count == 1);
            StatusSnapshot st = StatusMarkers.Parse("STATUS_UP\nSTATUS_PID 16748\nSTATUS_START 2026-09-28 11:53:14\nSTATUS_UPTIME 6 小时 3 分");
            Check("status：运行中 + PID/启动/运行时长", st.Ok && st.State == 0 && st.StateText == "运行中" && st.Pid == "16748" && st.Start == "2026-09-28 11:53:14" && st.Uptime == "6 小时 3 分");
            Check("status：启动中 / 未运行", StatusMarkers.Parse("STATUS_STARTING").State == 1 && StatusMarkers.Parse("STATUS_DOWN").State == 2);
            Check("status：未识别标记原样收进 Extras（对未来版本友好）", StatusMarkers.Parse("STATUS_UP\nSTATUS_FUTURE 42").Extras.Count == 1 && StatusMarkers.Parse("STATUS_UP\nSTATUS_FUTURE 42").Extras[0].Value == "42");
            Check("status：空输入不抛且 !Ok", !StatusMarkers.Parse("").Ok && !StatusMarkers.Parse(null).Ok);
            Check("status：含空格的值整行保留（启动时间）", StatusMarkers.Parse("STATUS_START 2026-09-28 11:53:14").Start.Split(' ').Length == 2);
            Console.WriteLine("== " + _pass + "/" + (_pass + _fail) + " passed, " + _fail + " failed ==");
            return _fail == 0 ? 0 : 1;
        }
    }
}
