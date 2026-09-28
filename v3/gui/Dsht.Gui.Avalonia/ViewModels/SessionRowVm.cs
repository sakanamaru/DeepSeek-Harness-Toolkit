using System;
using Avalonia.Media;
using Dsht.Gui.Avalonia.Markers;

namespace Dsht.Gui.Avalonia.ViewModels
{
    /// <summary>配色（一处定义，全界面统一）。深色适配留到后面；这里先定浅色体系。</summary>
    public static class Palette
    {
        public static readonly IBrush Accent = new SolidColorBrush(Color.Parse("#4D6BFE"));      // DeepSeek 蓝
        public static readonly IBrush AccentSoft = new SolidColorBrush(Color.Parse("#E8EDFF"));
        public static readonly IBrush PageBg = new SolidColorBrush(Color.Parse("#F3F4F6"));
        public static readonly IBrush CardBg = Brushes.White;
        public static readonly IBrush Border = new SolidColorBrush(Color.Parse("#E5E7EB"));
        public static readonly IBrush Text = new SolidColorBrush(Color.Parse("#111827"));
        public static readonly IBrush TextDim = new SolidColorBrush(Color.Parse("#6B7280"));
        public static readonly IBrush TextFaint = new SolidColorBrush(Color.Parse("#9CA3AF"));

        public static readonly IBrush Good = new SolidColorBrush(Color.Parse("#10B981"));        // 运行中 / 命中高
        public static readonly IBrush Warn = new SolidColorBrush(Color.Parse("#F59E0B"));
        public static readonly IBrush Bad = new SolidColorBrush(Color.Parse("#EF4444"));
        public static readonly IBrush Idle = new SolidColorBrush(Color.Parse("#9CA3AF"));        // 已结束
        public static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#D1D5DB"));       // 空会话
        public static readonly IBrush BarTrack = new SolidColorBrush(Color.Parse("#EEF0F3"));
        public static readonly IBrush WarnSoft = new SolidColorBrush(Color.Parse("#FEF3C7"));
        public static readonly IBrush FormAcp = new SolidColorBrush(Color.Parse("#8B5CF6"));

        /// <summary>形态徽章颜色：web=蓝 headless=灰 acp=紫 未知/无法解析=琥珀。</summary>
        public static IBrush FormBrush(int kind) { return kind == 0 ? Accent : (kind == 1 ? Idle : (kind == 2 ? FormAcp : Warn)); }

        /// <summary>缓存命中率（越高越好）：高=绿 中=琥珀 低=红 未知=灰。</summary>
        public static IBrush HitBrush(int level) { return level >= 3 ? Good : (level == 2 ? Warn : (level == 1 ? Bad : Muted)); }

        /// <summary>上下文压力（越低越好）：低=绿 中=琥珀 高=红 未知=灰。</summary>
        public static IBrush CtxBrush(int level) { return level == 1 ? Good : (level == 2 ? Warn : (level >= 3 ? Bad : Muted)); }

        /// <summary>会话状态：运行中=绿 已结束=灰 空会话=浅灰。</summary>
        public static IBrush StatusBrush(int kind) { return kind == 0 ? Good : (kind == 2 ? Muted : Idle); }
    }

    /// <summary>会话列表的一行（界面视图模型）。纯数据/格式化仍在 Markers 里（可无图形环境单测），
    /// 这里只负责把语义等级翻译成画刷，供 XAML 直接绑定。</summary>
    public sealed class SessionRowVm
    {
        public readonly SessionRow Row;

        public SessionRowVm(SessionRow row) { Row = row; }

        public string ShortId { get { return Row.ShortId; } }
        public string TitleText { get { return Row.TitleText; } }
        public string StatusText { get { return Row.LiveText; } }
        public IBrush StatusBrush { get { return Palette.StatusBrush(Row.StatusKind); } }
        public string MetaText
        {
            get
            {
                return Row.TurnsText + "　最后活动 " + Row.LastShort
                    + (string.IsNullOrEmpty(Row.CreatedShort) ? "" : "　创建 " + Row.CreatedShort);
            }
        }

        public string InText { get { return Row.InText; } }
        public string OutText { get { return Row.OutText; } }
        public string CacheReadText { get { return Row.CacheReadText; } }

        public string HitText { get { return Row.HitText; } }
        public double HitBar { get { return Row.HitBar; } }
        public IBrush HitBrush { get { return Palette.HitBrush(Row.HitLevel); } }

        public string CtxText { get { return Row.CtxText; } }
        public double CtxBar { get { return Row.CtxBar; } }
        public IBrush CtxBrush { get { return Palette.CtxBrush(Row.CtxLevel); } }

        public string DecodeText { get { return Row.DecodeText; } }

        /// <summary>解码速度与首 token 合成一行（避免 XAML 里用 Run 绑定，减少解析风险）。</summary>
        public string DecodeLine { get { return "解码 " + Row.DecodeText + "　首 token " + Row.TtftText; } }
        public string TtftText { get { return Row.TtftText; } }

        public double TokenBar { get { return Row.TokenBar; } }
        public IBrush TokenBrush { get { return Palette.Accent; } }
    }
}
