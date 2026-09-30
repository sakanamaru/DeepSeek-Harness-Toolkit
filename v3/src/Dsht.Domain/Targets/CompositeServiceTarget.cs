using System;
using System.Collections.Generic;
using Dsht.Domain.Abstractions;
using Dsht.Domain.Model;

namespace Dsht.Domain.Targets
{
    /// <summary>组合服务目标：把多个形态实现聚合为一个"当前 dsh 在跑哪个形态"的答案。
    /// 选择规则（确定性、只用可观测事实）：
    ///   ① 状态优先级 Ready &gt; Listening &gt; Down
    ///   ② 同状态时，**已知形态优先于 Unknown**（更具体的证据更可信）
    ///   ③ 同状态同具体度时，按传入顺序（稳定）
    /// 全部为 Down 时：若有 Unknown 报告则返回它（诚实说明"未识别形态"），否则返回第一条并注明"均未观测到"。</summary>
    public sealed class CompositeServiceTarget : IServiceTarget
    {
        private readonly IServiceTarget[] _targets;
        private AppKind _lastKind = AppKind.Unknown;

        public CompositeServiceTarget(IServiceTarget[] targets)
        {
            _targets = targets == null ? new IServiceTarget[0] : targets;
        }

        public AppKind Kind { get { return _lastKind; } }

        public bool IsAvailable()
        {
            for (int i = 0; i < _targets.Length; i++) { if (_targets[i] != null && _targets[i].IsAvailable()) return true; }
            return false;
        }

        public ServiceReport Probe()
        {
            List<ServiceReport> reports = new List<ServiceReport>();
            for (int i = 0; i < _targets.Length; i++)
            {
                if (_targets[i] == null) continue;
                ServiceReport r;
                try { r = _targets[i].Probe(); }
                catch { r = null; }
                if (r != null) reports.Add(r);
            }
            if (reports.Count == 0)
            {
                _lastKind = AppKind.Unknown;
                return new ServiceReport(AppKind.Unknown, ServiceState.Down, 0, "没有任何形态实现（组合为空）");
            }

            ServiceReport best = reports[0];
            for (int i = 1; i < reports.Count; i++)
            {
                if (Better(reports[i], best)) best = reports[i];
            }
            _lastKind = best.Kind;

            if (best.State == ServiceState.Down)
            {
                // 全都 Down：优先返回 Unknown 报告（诚实说明"未识别形态"）
                for (int i = 0; i < reports.Count; i++)
                {
                    if (reports[i].Kind == AppKind.Unknown) { _lastKind = AppKind.Unknown; return reports[i]; }
                }
                _lastKind = AppKind.Unknown;
                // 措辞必须与实际发生的事一致 ✓：预留形态（headless/acp/desktop）的 Probe() 是硬编码返回 Down，
                // IsAvailable() 恒为 false —— 它们**根本没有可尝试的观测** ✗，说"已尝试"是过度声称 ✓
                // （2026-09-29 由子代理发现、我复核确认 ✓）
                return new ServiceReport(AppKind.Unknown, ServiceState.Down, 0, DownMessage());
            }
            return best;
        }

        public int FindPid()
        {
            for (int i = 0; i < _targets.Length; i++)
            {
                if (_targets[i] == null) continue;
                try { int pid = _targets[i].FindPid(); if (pid > 0) return pid; }
                catch { }
            }
            return 0;
        }

        public string Describe()
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < _targets.Length; i++)
            {
                if (_targets[i] == null) continue;
                try { parts.Add(_targets[i].Describe()); } catch { }
            }
            return "组合服务目标（按可观测事实择一）：" + string.Join(" | ", parts.ToArray());
        }

        private static bool Better(ServiceReport a, ServiceReport b)
        {
            int sa = Rank(a.State), sb = Rank(b.State);
            if (sa != sb) return sa > sb;
            bool ka = a.Kind != AppKind.Unknown, kb = b.Kind != AppKind.Unknown;
            if (ka != kb) return ka;          // 已知形态优先
            return false;                      // 同状态同具体度 → 保持先者（稳定）
        }

        private static int Rank(ServiceState s)
        {
            if (s == ServiceState.Ready) return 2;
            if (s == ServiceState.Listening) return 1;
            return 0;
        }

        /// <summary>全 Down 时的说明：**区分"尝试过但没观测到"与"根本无法观测"** ✓✓
        /// 预留形态（IsAvailable()==false）的 Probe() 是硬编码 Down，从未真正尝试 ✗ → 必须分开说 ✓</summary>
        private string DownMessage()
        {
            List<string> observed = new List<string>();
            List<string> reserved = new List<string>();
            for (int i = 0; i < _targets.Length; i++)
            {
                if (_targets[i] == null) continue;
                bool avail;
                try { avail = _targets[i].IsAvailable(); } catch { avail = false; }
                if (avail) observed.Add(_targets[i].Kind.ToString());
                else reserved.Add(_targets[i].Kind.ToString());
            }
            string msg = "所有可观测形态均未运行";
            if (observed.Count > 0) msg += "（已尝试：" + string.Join(",", observed.ToArray()) + "）";
            if (reserved.Count > 0) msg += "；预留形态暂无法观测：" + string.Join(",", reserved.ToArray()) + "（形态待观测，不等于未运行）";
            return msg;
        }
        private static string Kinds(List<ServiceReport> reports)
        {
            List<string> ks = new List<string>();
            for (int i = 0; i < reports.Count; i++) ks.Add(reports[i].Kind.ToString());
            return string.Join(",", ks.ToArray());
        }
    }
}