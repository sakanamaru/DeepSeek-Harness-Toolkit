using System;
using Dsht.Domain.Abstractions;
using Dsht.Domain.Model;
using Dsht.Domain.Services;
using Dsht.Domain.Targets;

// 契约测试：先用假观测跑通判定与形态语义；V3-4 会把同一套断言跑在真实平台实现上。
sealed class FakePort : IPortProbe
{
    public bool Open;
    public bool Throw;
    public bool IsOpen(int port, int timeoutMs) { if (Throw) throw new Exception("boom"); return Open; }
}
sealed class FakeHttp : IHttpProbe
{
    public bool Ready;
    public bool Throw;
    public bool IsReady(string url, int timeoutMs) { if (Throw) throw new Exception("boom"); return Ready; }
}
sealed class FakeProc : IProcessQuery
{
    public int Pid;
    public bool IsDsh;
    public bool Throw;
    public int PidListeningOn(int port) { if (Throw) throw new Exception("boom"); return Pid; }
    public bool IsDshCommandLine(int pid) { return IsDsh; }
}

static class ContractTests
{
    static int _pass, _fail;
    static void Check(string name, bool ok)
    {
        if (ok) { _pass++; Console.WriteLine("  [PASS] " + name); }
        else { _fail++; Console.WriteLine("  [FAIL] " + name); }
    }

    static WebTarget Make(FakePort p, FakeHttp h, FakeProc q)
    {
        return new WebTarget(p, h, q, new WebTargetOptions(3080, "http://127.0.0.1:3080/", 800, 800));
    }

    static int Main()
    {
        Console.WriteLine("== V3 契约测试（领域判定 + 形态语义）==");

        Console.WriteLine("[1] ServiceJudge 真值表（逐条对齐 v2.x JudgeState3）");
        Check("端口关 → Down", ServiceJudge.Judge(false, false, null) == ServiceState.Down);
        Check("端口关+HTTP就绪 → Down（端口优先）", ServiceJudge.Judge(false, true, null) == ServiceState.Down);
        Check("端口开+HTTP就绪 → Ready", ServiceJudge.Judge(true, true, null) == ServiceState.Ready);
        Check("端口开+HTTP未就绪+监听是dsh → Ready", ServiceJudge.Judge(true, false, delegate { return true; }) == ServiceState.Ready);
        Check("端口开+HTTP未就绪+监听非dsh → Listening", ServiceJudge.Judge(true, false, delegate { return false; }) == ServiceState.Listening);
        Check("端口开+HTTP未就绪+委托为null → Listening", ServiceJudge.Judge(true, false, null) == ServiceState.Listening);
        Check("监听判定抛异常 → 按false（Listening）", ServiceJudge.Judge(true, false, delegate { throw new Exception("boom"); }) == ServiceState.Listening);
        Check("两信号版：端口开+HTTP未就绪 → Listening", ServiceJudge.Judge(true, false) == ServiceState.Listening);

        Console.WriteLine("[2] WebTarget 形态语义");
        FakePort p = new FakePort(); FakeHttp h = new FakeHttp(); FakeProc q = new FakeProc();
        WebTarget t = Make(p, h, q);
        Check("Kind=Web", t.Kind == AppKind.Web);
        Check("Describe 提到端口", t.Describe().IndexOf("3080") >= 0);

        ServiceReport r1 = t.Probe();
        Check("端口关 → Down/无PID", r1.State == ServiceState.Down && r1.Pid == 0 && r1.StatusMarker == "STATUS_DOWN");
        Check("端口关 → Basis 说明无监听", r1.Basis.IndexOf("无监听") >= 0);

        p.Open = true; h.Ready = true;
        ServiceReport r2 = t.Probe();
        Check("端口开+HTTP就绪 → Ready/STATUS_UP", r2.State == ServiceState.Ready && r2.StatusMarker == "STATUS_UP");

        h.Ready = false; q.Pid = 4242; q.IsDsh = true;
        ServiceReport r3 = t.Probe();
        Check("端口开+HTTP未就绪+监听是dsh → Ready", r3.State == ServiceState.Ready);
        Check("Ready 时带出 PID", r3.Pid == 4242);

        q.IsDsh = false;
        ServiceReport r4 = t.Probe();
        Check("端口开+HTTP未就绪+监听非dsh → Listening/STATUS_STARTING", r4.State == ServiceState.Listening && r4.StatusMarker == "STATUS_STARTING");

        FakePort pt = new FakePort(); pt.Throw = true;
        Check("端口探测抛异常 → 按 Down 处理", Make(pt, h, q).Probe().State == ServiceState.Down);

        Console.WriteLine("[2b] 观测抛异常时的降级（v2.x 的 try/catch 语义）");
        FakePort p2 = new FakePort(); p2.Open = true;
        FakeHttp h2 = new FakeHttp(); h2.Throw = true;
        FakeProc q2 = new FakeProc(); q2.Pid = 7; q2.IsDsh = true;
        Check("HTTP 探测抛异常 → 按未就绪，但监听是 dsh 仍判 Ready", Make(p2, h2, q2).Probe().State == ServiceState.Ready);
        q2.IsDsh = false;
        Check("HTTP 抛异常 + 监听非 dsh → Listening", Make(p2, h2, q2).Probe().State == ServiceState.Listening);
        FakeProc q3 = new FakeProc(); q3.Throw = true;
        ServiceReport r5 = Make(p2, new FakeHttp(), q3).Probe();
        Check("进程查询抛异常 → 非 dsh → Listening 且 PID=0", r5.State == ServiceState.Listening && r5.Pid == 0);
        Console.WriteLine("[3] UnknownTarget 诚实性");
        UnknownTarget u = new UnknownTarget("测试注入");
        ServiceReport ru = u.Probe();
        Check("Kind=Unknown", u.Kind == AppKind.Unknown);
        Check("State=Down（不假装 Ready）", ru.State == ServiceState.Down && ru.StatusMarker == "STATUS_DOWN");
        Check("IsAvailable=false", !u.IsAvailable());
        Check("Describe 明确说明未识别", u.Describe().IndexOf("未识别") >= 0);
        Check("Basis 明确说明未识别", ru.Basis.IndexOf("未识别") >= 0);

        Console.WriteLine();
        Console.WriteLine("== " + _pass + "/" + (_pass + _fail) + " passed, " + _fail + " failed ==");
        return _fail == 0 ? 0 : 1;
    }
}