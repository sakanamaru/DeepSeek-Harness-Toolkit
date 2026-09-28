using System;
using Avalonia.Media;
using Dsht.Gui.Avalonia.Markers;

namespace Dsht.Gui.Avalonia.ViewModels
{
    /// <summary>配色与风格（一处定义，全界面统一）。四个 demo 通过 Apply 切换：
    /// 0=浅色卡片 1=深色卡片 2=深色紧凑 3=浅色仪表盘。
    /// 做法参考 AuroraZiling/Hollow（MIT，同为 Avalonia）的"颜色集中在资源里"思路，颜色值是我们自己定的。</summary>
    public static class Palette
    {
        public static int StyleKind = 0;
        public static bool Dark;
        public static bool Compact;

        public static IBrush Accent = Brushes.Transparent;
        public static IBrush AccentSoft = Brushes.Transparent;
        public static IBrush PageBg = Brushes.Transparent;
        public static IBrush CardBg = Brushes.Transparent;
        public static IBrush Border = Brushes.Transparent;
        public static IBrush Text = Brushes.Transparent;
        public static IBrush TextDim = Brushes.Transparent;
        public static IBrush TextFaint = Brushes.Transparent;
        public static IBrush Good = new SolidColorBrush(Color.Parse("#10B981"));
        public static IBrush Warn = new SolidColorBrush(Color.Parse("#F59E0B"));
        public static IBrush Bad = new SolidColorBrush(Color.Parse("#EF4444"));
        public static IBrush Idle = new SolidColorBrush(Color.Parse("#9CA3AF"));
        public static IBrush Muted = new SolidColorBrush(Color.Parse("#D1D5DB"));
        public static IBrush BarTrack = Brushes.Transparent;
        public static IBrush WarnSoft = new SolidColorBrush(Color.Parse("#FEF3C7"));
        public static IBrush FormAcp = new SolidColorBrush(Color.Parse("#8B5CF6"));

        static Palette() { Apply(0); }

        public static void Apply(int kind)
        {
            StyleKind = kind;
            Dark = kind == 1 || kind == 2;
            Compact = kind == 2;
            Accent = new SolidColorBrush(Color.Parse("#4D6BFE"));
            if (Dark)
            {
                AccentSoft = new SolidColorBrush(Color.Parse("#23305E"));
                PageBg = new SolidColorBrush(Color.Parse("#1B1B1F"));
                CardBg = new SolidColorBrush(Color.Parse("#27272C"));
                Border = new SolidColorBrush(Color.Parse("#3A3A42"));
                Text = new SolidColorBrush(Color.Parse("#F3F4F6"));
                TextDim = new SolidColorBrush(Color.Parse("#C3C7CF"));
                TextFaint = new SolidColorBrush(Color.Parse("#8A8F98"));
                BarTrack = new SolidColorBrush(Color.Parse("#3A3A42"));
                Muted = new SolidColorBrush(Color.Parse("#4B5563"));
            }
            else
            {
                AccentSoft = new SolidColorBrush(Color.Parse("#E8EDFF"));
                PageBg = new SolidColorBrush(Color.Parse("#F3F4F6"));
                CardBg = Brushes.White;
                Border = new SolidColorBrush(Color.Parse("#E5E7EB"));
                Text = new SolidColorBrush(Color.Parse("#111827"));
                TextDim = new SolidColorBrush(Color.Parse("#6B7280"));
                TextFaint = new SolidColorBrush(Color.Parse("#9CA3AF"));
                BarTrack = new SolidColorBrush(Color.Parse("#EEF0F3"));
                Muted = new SolidColorBrush(Color.Parse("#D1D5DB"));
            }
        }

        public static string StyleName(int kind)
        {
            if (kind == 1) return "B · 深色卡片";
            if (kind == 2) return "C · 深色紧凑";
            if (kind == 3) return "D · 浅色仪表盘";
            return "A · 浅色卡片";
        }

        public static IBrush HitBrush(int level) { return level >= 3 ? Good : (level == 2 ? Warn : (level == 1 ? Bad : Muted)); }
        public static IBrush CtxBrush(int level) { return level == 1 ? Good : (level == 2 ? Warn : (level >= 3 ? Bad : Muted)); }
        public static IBrush StatusBrush(int kind) { return kind == 0 ? Good : (kind == 2 ? Muted : Idle); }
        public static IBrush FormBrush(int kind) { return kind == 0 ? Accent : (kind == 1 ? Idle : (kind == 2 ? FormAcp : Warn)); }
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
