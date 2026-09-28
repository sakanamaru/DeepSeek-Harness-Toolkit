namespace Dsht.Domain.Services
{
    /// <summary>start 的判定。</summary>
    public enum StartDecision { AlreadyRunning, ShouldLaunch }

    /// <summary>启动后的判定：只有**观测到**就绪才算 Started。</summary>
    public enum StartOutcome { Started, NotObserved }

    /// <summary>stop 的判定。</summary>
    public enum StopDecision { NothingToStop, ShouldStop }

    /// <summary>停止后的判定：只有**观测到** Down 才算 Stopped。</summary>
    public enum StopOutcome { Stopped, StillListening }

    /// <summary>start/stop 的判定逻辑（**纯函数，可单测**）。
    /// 平台层只负责"把进程起起来/停下来"；**该不该起、算不算成功**全在这里。
    /// 纪律（V3 的核心）：只有**可观测事实**（端口监听 / HTTP 就绪）能判定成功 ——
    /// "命令发出去了"、"进程还在"都不算成功；拿不准时返回"未观测到"，绝不假装成功。
    /// 状态以字符串传入（调用方传 ServiceReport.State.ToString()），这样判定逻辑不依赖模型类型、便于单测。</summary>
    public static class ServiceControlPolicy
    {
        /// <summary>该状态是否表示"在运行"（Ready 或 Listening）。</summary>
        public static bool IsRunning(string state)
        {
            return state == "Ready" || state == "Listening";
        }

        /// <summary>启动前：已在运行就别再起一个（否则会撞端口/起出第二个实例）。</summary>
        public static StartDecision BeforeStart(string observedState, int observedPid)
        {
            if (IsRunning(observedState)) return StartDecision.AlreadyRunning;
            // 端口没在监听但有 PID（半死状态）也算"不该直接再起"——交给用户/诊断处理，不猜
            return StartDecision.ShouldLaunch;
        }

        /// <summary>启动后：观测到就绪 → Started；否则 NotObserved（可能仍在启动，也可能失败）。</summary>
        public static StartOutcome AfterLaunch(string observedState, int observedPid, int launchedPid)
        {
            return IsRunning(observedState) ? StartOutcome.Started : StartOutcome.NotObserved;
        }

        /// <summary>停止前：没有观测到在运行（无 PID 或状态 Down）→ 没什么可停的。</summary>
        public static StopDecision BeforeStop(string observedState, int observedPid)
        {
            if (observedPid <= 0) return StopDecision.NothingToStop;
            if (observedState == "Down") return StopDecision.NothingToStop;
            return StopDecision.ShouldStop;
        }

        /// <summary>停止后：观测到 Down 才算 Stopped；仍能观测到 → StillListening（不谎报已停）。</summary>
        public static StopOutcome AfterStop(string observedState)
        {
            return observedState == "Down" ? StopOutcome.Stopped : StopOutcome.StillListening;
        }
    }
}