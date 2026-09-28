using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using Dsht.Gui.Avalonia.Markers;
using Dsht.Gui.Avalonia.ViewModels;

namespace Dsht.Gui.Avalonia.Shells
{
    /// <summary>五种**布局框架**（外壳），同一个程序里实时切换，方便对比挑选。
    /// ⑤ 混合式 = ① 侧栏（主菜单）+ ② 顶部标签（子菜单）；子菜单不是摆设：会话页里它直接切换排序视角。
    /// 内容构建器（KPI / 会话卡片 / 工具栏 / 说明区）被所有外壳共享 —— 换外壳不动内容，换内容不动外壳。
    /// 所有颜色一律取自 <see cref="Palette"/>（含卡片底），四个风格才不会破。
    /// 设计方向参考了 March7thAssistant（GPL-3.0）的做法，**未复制其任何代码、图标、字体或图片资源**。</summary>
    public static class Shells
    {
        public const int Sidebar = 0;
        public const int TopTabs = 1;
        public const int CardGrid = 2;
        public const int MasterDetail = 3;
        public const int Hybrid = 4;

        private static readonly FontFamily MonoFont = new FontFamily("Cascadia Mono,Consolas,monospace");
        private static readonly Thickness PageMargin = new Thickness(24, 18, 24, 16);

        public static string Name(int id)
        {
            switch (id)
            {
                case Sidebar: return "① 侧栏式";
                case TopTabs: return "② 顶部标签式";
                case CardGrid: return "③ 卡片网格仪表盘";
                case MasterDetail: return "④ 主从式";
                case Hybrid: return "⑤ 混合式（主菜单+子菜单）";
                default: return "未知";
            }
        }

        public static Control Build(int id, MainWindow host)
        {
            switch (id)
            {
                case TopTabs: return BuildTopTabs(host);
                case CardGrid: return BuildCardGrid(host);
                case MasterDetail: return BuildMasterDetail(host);
                case Hybrid: return BuildHybrid(host);
                default: return BuildSidebar(host);
            }
        }

        // ================================================================ 基础构件

        private static TextBlock T(string text, double size, IBrush fg)
        {
            return new TextBlock { Text = text, FontSize = size, Foreground = fg };
        }

        private static TextBlock T(string text, double size, IBrush fg, FontWeight w)
        {
            return new TextBlock { Text = text, FontSize = size, Foreground = fg, FontWeight = w };
        }

        private static TextBlock Mono(string text, double size, IBrush fg)
        {
            return new TextBlock { Text = text, FontSize = size, Foreground = fg, FontFamily = MonoFont };
        }

        private static SymbolIcon Ic(Symbol s, double size, IBrush fg)
        {
            return new SymbolIcon { Symbol = s, IconVariant = IconVariant.Regular, FontSize = size, Foreground = fg };
        }

        /// <summary>统一卡片：12px 圆角 + 发丝描边 + 浅色风格的极浅投影（深色靠描边分层）。</summary>
        private static Border Card(Control child, Thickness margin, Thickness padding)
        {
            Border b = new Border
            {
                Background = Palette.CardBg,
                CornerRadius = new CornerRadius(12),
                BorderBrush = Palette.Border,
                BorderThickness = new Thickness(1),
                Margin = margin,
                Padding = padding,
                Child = child
            };
            if (Palette.CardShadow.Length > 0) b.BoxShadow = BoxShadows.Parse(Palette.CardShadow);
            return b;
        }

        private static Border Chip(string text, IBrush fg, IBrush bg)
        {
            return new Border
            {
                Background = bg,
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(8, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Child = T(text, 10.5, fg, FontWeight.SemiBold)
            };
        }

        /// <summary>圆角小方块里的图标（KPI / 卡片头部用）。</summary>
        private static Border SoftTile(Symbol icon, IBrush fg, IBrush bg, double size, double iconSize)
        {
            return new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(9),
                Background = bg,
                Child = new SymbolIcon
                {
                    Symbol = icon,
                    IconVariant = IconVariant.Regular,
                    FontSize = iconSize,
                    Foreground = fg,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        /// <summary>比例条：自适应容器宽度（星号列），两端圆角。pct &lt; 0 视为未知 → 空条。</summary>
        private static Control Meter(double pct, IBrush brush, double h)
        {
            if (pct < 0) pct = 0;
            if (pct > 100) pct = 100;
            int p = (int)Math.Round(pct);
            Grid g = new Grid { Height = h, ColumnDefinitions = new ColumnDefinitions(p.ToString() + "*," + (100 - p) + "*") };
            Border track = new Border { Background = Palette.BarTrack, CornerRadius = new CornerRadius(h / 2) };
            Grid.SetColumnSpan(track, 2);
            g.Children.Add(track);
            if (p > 0)
            {
                Border fill = new Border { Background = brush, CornerRadius = new CornerRadius(h / 2) };
                Grid.SetColumn(fill, 0);
                g.Children.Add(fill);
            }
            return g;
        }

        private static void Hover(Border b, IBrush normal, IBrush hover)
        {
            b.PointerEntered += delegate { b.Background = hover; };
            b.PointerExited += delegate { b.Background = normal; };
        }

        private static void Hover(Button b, IBrush normal, IBrush hover)
        {
            b.PointerEntered += delegate { b.Background = hover; };
            b.PointerExited += delegate { b.Background = normal; };
        }

        private static Button GhostButton(Control content, Action act, bool bordered)
        {
            Button b = new Button
            {
                Content = content,
                Background = bordered ? Palette.CardBg : Brushes.Transparent,
                BorderBrush = bordered ? Palette.Border : Brushes.Transparent,
                BorderThickness = new Thickness(bordered ? 1 : 0),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 7)
            };
            IBrush normal = bordered ? Palette.CardBg : Brushes.Transparent;
            Hover(b, normal, Palette.CardHover);
            b.Click += delegate { act(); };
            return b;
        }

        private static Button PrimaryButton(string text, Action act)
        {
            Button b = new Button
            {
                Content = T(text, 12.5, Palette.OnAccent, FontWeight.SemiBold),
                Background = Palette.Accent,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 7)
            };
            Hover(b, Palette.Accent, Palette.AccentHover);
            b.Click += delegate { act(); };
            return b;
        }

        /// <summary>segmented 切换器：凹陷底 + 选中项浮起为卡片色。</summary>
        private static Control Segmented(string[] items, int active, Action<int> pick)
        {
            StackPanel inner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            for (int i = 0; i < items.Length; i++)
            {
                int idx = i;
                bool act = idx == active;
                Button b = new Button
                {
                    Content = T(items[i], 12, act ? Palette.Text : Palette.TextDim, act ? FontWeight.SemiBold : FontWeight.Normal),
                    Background = act ? Palette.CardBg : Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(12, 5)
                };
                b.Click += delegate { pick(idx); };
                inner.Children.Add(b);
            }
            return new Border
            {
                Background = Palette.InsetBg,
                BorderBrush = Palette.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = inner
            };
        }

        /// <summary>写操作的两次确认按钮（隔离/恢复）：第一次点击只是变成确认文案，第二次才真执行。</summary>
        private static Button ConfirmButton(string key, string idleText, string confirmText, MainWindow host, Action confirmed)
        {
            bool pending = host.PendingPatch == key;
            Button b = new Button
            {
                Content = T(pending ? confirmText : idleText, 11.5, pending ? Palette.Bad : Palette.TextDim, pending ? FontWeight.SemiBold : FontWeight.Normal),
                Background = pending ? Palette.BadSoft : Brushes.Transparent,
                BorderBrush = pending ? Palette.Bad : Palette.BorderStrong,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(9, 3)
            };
            b.Click += delegate
            {
                if (host.PendingPatch == key)
                {
                    host.PendingPatch = "";
                    confirmed();
                }
                else
                {
                    host.PendingPatch = key;
                    host.Rebuild();
                }
            };
            return b;
        }

        // ================================================================ 外壳

        // ---------------- ① 侧栏式 ----------------

        private static Control BuildSidebar(MainWindow host)
        {
            Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("232,*") };
            Border side = new Border
            {
                Background = Palette.SidebarBg,
                BorderBrush = Palette.Border,
                BorderThickness = new Thickness(0, 0, 1, 0),
                Child = new ScrollViewer { Content = MainMenu(host) }
            };
            Grid.SetColumn(side, 0);

            Grid body = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
            Control title = PageHeader(host);
            Grid.SetRow(title, 0);
            Control content = SectionBody(host);
            Grid.SetRow(content, 1);
            Control foot = Footer(host);
            Grid.SetRow(foot, 2);
            body.Children.Add(title); body.Children.Add(content); body.Children.Add(foot);
            Grid.SetColumn(body, 1);
            g.Children.Add(side); g.Children.Add(body);
            return g;
        }

        // ---------------- ② 顶部标签式 ----------------

        private static Control BuildTopTabs(MainWindow host)
        {
            Grid g = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
            Border tabs = new Border { Margin = new Thickness(24, 14, 24, 0), Child = Segmented(host.SubTabs, host.SubTab, delegate(int i) { host.SetSubTab(i); }) };
            Grid.SetRow(tabs, 0);
            Control title = PageHeader(host);
            Grid.SetRow(title, 1);
            Control content = SectionBody(host);
            Grid.SetRow(content, 2);
            g.Children.Add(tabs); g.Children.Add(title); g.Children.Add(content);
            return g;
        }

        // ---------------- ③ 卡片网格仪表盘 ----------------

        private static Control BuildCardGrid(MainWindow host)
        {
            Grid g = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
            Border tabs = new Border { Margin = new Thickness(24, 14, 24, 0), Child = Segmented(host.SubTabs, host.SubTab, delegate(int i) { host.SetSubTab(i); }) };
            Grid.SetRow(tabs, 0);

            StackPanel body = new StackPanel { Margin = PageMargin, Spacing = 14 };
            body.Children.Add(PageHeader(host, false));
            if (host.IsSessionsSection)
            {
                body.Children.Add(KpiStrip(host));
                body.Children.Add(T("会话明细", 14, Palette.Text, FontWeight.SemiBold));
                body.Children.Add(CardGridBody(host));
            }
            else
            {
                body.Children.Add(SectionInner(host));
            }
            body.Children.Add(Explain());
            Control scroll = new ScrollViewer { Content = body };
            Grid.SetRow(scroll, 1);
            g.Children.Add(tabs); g.Children.Add(scroll);
            return g;
        }

        // ---------------- ④ 主从式 ----------------

        private static Control BuildMasterDetail(MainWindow host)
        {
            Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("300,*") };

            StackPanel left = new StackPanel { Margin = new Thickness(14), Spacing = 10 };
            left.Children.Add(T(host.IsSessionsSection ? "会话列表" : "页面", 13, Palette.Text, FontWeight.SemiBold));
            if (host.IsSessionsSection)
            {
                left.Children.Add(Segmented(new string[] { "全部", "非空", "运行中" }, host.Filter, delegate(int i) { host.SetFilter(i); }));
                left.Children.Add(MasterList(host));
            }
            else
            {
                for (int i = 0; i < MainWindow.NavItems.Length; i++) left.Children.Add(NavItem(host, i));
            }
            Border leftCard = new Border
            {
                Background = Palette.SidebarBg,
                BorderBrush = Palette.Border,
                BorderThickness = new Thickness(0, 0, 1, 0),
                Child = new ScrollViewer { Content = left }
            };
            Grid.SetColumn(leftCard, 0);

            StackPanel right = new StackPanel { Margin = PageMargin, Spacing = 12 };
            right.Children.Add(PageHeader(host, false));
            right.Children.Add(host.IsSessionsSection ? DetailCard(host) : SectionInner(host));
            if (host.IsSessionsSection) right.Children.Add(Explain());
            Control rightScroll = new ScrollViewer { Content = right };
            Grid.SetColumn(rightScroll, 1);
            g.Children.Add(leftCard); g.Children.Add(rightScroll);
            // DetailHost 此时才就位：补一次首行选中，让右侧详情不用等用户点
            if (host.IsSessionsSection && host.Rows.Count > 0) host.ShowDetail(host.Rows[0]);
            return g;
        }

        // ---------------- ⑤ 混合式：侧栏主菜单 + 顶部子菜单 ----------------

        private static Control BuildHybrid(MainWindow host)
        {
            Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("232,*") };
            Border side = new Border
            {
                Background = Palette.SidebarBg,
                BorderBrush = Palette.Border,
                BorderThickness = new Thickness(0, 0, 1, 0),
                Child = new ScrollViewer { Content = MainMenu(host) }
            };
            Grid.SetColumn(side, 0);

            Grid right = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
            Border subs = new Border
            {
                BorderBrush = Palette.Border,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 10, 24, 10),
                Child = Segmented(host.SubTabs, host.SubTab, delegate(int i) { host.SetSubTab(i); })
            };
            Grid.SetRow(subs, 0);
            Control head = PageHeader(host);
            Grid.SetRow(head, 1);
            Control body = SectionBody(host);
            Grid.SetRow(body, 2);
            Control foot = Footer(host);
            Grid.SetRow(foot, 3);
            right.Children.Add(subs); right.Children.Add(head); right.Children.Add(body); right.Children.Add(foot);
            Grid.SetColumn(right, 1);

            g.Children.Add(side); g.Children.Add(right);
            return g;
        }

        // ================================================================ 共享片段

        private static Control NavItem(MainWindow host, int idx)
        {
            bool active = host.MainSection == idx;
            Grid row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*") };
            Border bar = new Border
            {
                Width = 3,
                Height = 16,
                CornerRadius = new CornerRadius(2),
                Background = active ? Palette.Accent : Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(bar, 0);
            SymbolIcon ic = Ic(MainWindow.NavIcons[idx], 15, active ? Palette.Accent : Palette.TextDim);
            ic.Margin = new Thickness(9, 0, 0, 0);
            ic.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(ic, 1);
            TextBlock tb = T(MainWindow.NavItems[idx], 13, active ? Palette.Accent : Palette.Text);
            tb.FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal;
            tb.Margin = new Thickness(10, 0, 0, 0);
            tb.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(tb, 2);
            row.Children.Add(bar); row.Children.Add(ic); row.Children.Add(tb);

            Button b = new Button
            {
                Content = row,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = active ? Palette.AccentSoft : Brushes.Transparent,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 9)
            };
            if (!active) Hover(b, Brushes.Transparent, Palette.CardHover);
            b.Click += delegate { host.SetMainSection(idx); };
            return b;
        }

        private static Control MainMenu(MainWindow host)
        {
            Grid g = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
            Control inner = MainMenuInner(host);
            Grid.SetRow(inner, 0);
            g.Children.Add(inner);
            Control start = StartStopButton(host);
            Grid.SetRow(start, 1);
            g.Children.Add(start);
            return g;
        }

        /// <summary>侧栏最底下的一键启动/停止（用户要求放这里，不放看板）。</summary>
        private static Control StartStopButton(MainWindow host)
        {
            StatusSnapshot st = host.Status;
            bool up = st != null && st.Ok && st.State == 0;
            StackPanel s = new StackPanel { Margin = new Thickness(12, 8, 12, 12), Spacing = 6 };
            Button b = new Button
            {
                Content = up ? "■  停止 dsh" : "▶  一键启动 dsh",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(10, 9)
            };
            b.Click += delegate { if (up) host.StopDsh(); else host.StartDsh(); };
            s.Children.Add(b);
            s.Children.Add(T(up ? "当前：运行中" : "当前：未运行", 10.5, up ? Palette.Good : Palette.TextFaint));
            return s;
        }

        private static Control MainMenuInner(MainWindow host)
        {
            StackPanel s = new StackPanel { Margin = new Thickness(12, 16, 12, 12), Spacing = 2 };
            s.Children.Add(new TextBlock { Text = "导航", Foreground = Palette.TextFaint, FontSize = 11, Margin = new Thickness(12, 0, 0, 8) });
            for (int i = 0; i < MainWindow.NavItems.Length; i++) s.Children.Add(NavItem(host, i));
            return s;
        }

        private static Control PageHeader(MainWindow host) { return PageHeader(host, true); }

        private static Control PageHeader(MainWindow host, bool withMargin)
        {
            StackPanel s = new StackPanel { Spacing = 4 };
            if (withMargin) s.Margin = new Thickness(24, 18, 24, 12);
            s.Children.Add(T(host.PageTitle, 20, Palette.Text, FontWeight.SemiBold));
            s.Children.Add(new TextBlock { Text = host.SubtitleText, Foreground = Palette.TextDim, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
            return s;
        }

        private static Control Footer(MainWindow host)
        {
            return new Border
            {
                Background = Palette.SidebarBg,
                BorderBrush = Palette.Border,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(24, 7),
                Child = new TextBlock { Text = host.SourceText, Foreground = Palette.TextFaint, FontSize = 11, TextWrapping = TextWrapping.Wrap }
            };
        }

        /// <summary>原始标记行：头部（图标+标题+说明）+ 凹陷等宽文本区（限高内滚）。</summary>
        private static Control RawCard(MainWindow host, string title, string caption)
        {
            StackPanel s = new StackPanel { Spacing = 10 };
            Grid head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            SymbolIcon ic = Ic(Symbol.Code, 15, Palette.TextDim);
            ic.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(ic, 0);
            TextBlock t = T(title, 13, Palette.Text, FontWeight.SemiBold);
            t.Margin = new Thickness(8, 0, 0, 0);
            t.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(t, 1);
            TextBlock cap = T(caption, 11, Palette.TextFaint);
            cap.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(cap, 2);
            head.Children.Add(ic); head.Children.Add(t); head.Children.Add(cap);
            s.Children.Add(head);

            TextBox box = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                Text = host.RawOutput,
                FontFamily = MonoFont,
                FontSize = 11.5,
                Foreground = Palette.TextDim,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            s.Children.Add(new Border
            {
                Background = Palette.InsetBg,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10),
                MaxHeight = 460,
                Child = new ScrollViewer { Content = box }
            });
            return Card(s, new Thickness(0), new Thickness(16, 14));
        }

        private static Control TextPane(MainWindow host)
        {
            return new Border { Margin = PageMargin, Child = RawCard(host, "原始输出", "CLI 标记行原文") };
        }

        /// <summary>按当前主菜单项选内容（带外层滚动）：会话页=面板，形态页=profile 卡片，状态页=图形化概览，其余=标记行原文。</summary>
        private static Control SectionBody(MainWindow host)
        {
            return new ScrollViewer { Content = SectionInner(host) };
        }

        /// <summary>同 <see cref="SectionBody"/> 但不自带滚动（供已经有滚动容器的外壳用）。</summary>
        private static Control SectionInner(MainWindow host)
        {
            if (host.IsSessionsSection) return SessionsContent(host);
            if (host.MainSection == 3) return ProfilesContent(host);
            if (host.MainSection == 0) return OverviewContent(host);
            if (host.MainSection == 1) return BoardContent(host);
            if (host.MainSection == 4) return BackupContent(host);
            if (host.MainSection == 5) return HealthContent(host);
            if (host.MainSection == 6) return SettingsContent(host);
            return new Border { Margin = PageMargin, Child = RawCard(host, "原始输出", "CLI 标记行原文") };
        }

        // ================================================================ 会话与 Token

        private static Control SessionsContent(MainWindow host)
        {
            StackPanel s = new StackPanel { Margin = PageMargin, Spacing = 14 };
            if (host.Data == null || !host.Data.Ok)
            {
                string msg = host.Data == null
                    ? (string.IsNullOrEmpty(host.RawOutput) ? "读不到会话数据。" : host.RawOutput)
                    : (string.IsNullOrEmpty(host.Data.FailReason) ? "没有可显示的会话。" : host.Data.FailReason);
                StackPanel err = new StackPanel { Spacing = 8 };
                Grid eh = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
                SymbolIcon wic = Ic(Symbol.Warning, 16, Palette.Warn);
                wic.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(wic, 0);
                TextBlock et = T("会话数据不可用", 13.5, Palette.Text, FontWeight.SemiBold);
                et.Margin = new Thickness(8, 0, 0, 0);
                et.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(et, 1);
                eh.Children.Add(wic); eh.Children.Add(et);
                err.Children.Add(eh);
                err.Children.Add(new TextBlock { Text = msg, Foreground = Palette.TextDim, FontSize = 12, TextWrapping = TextWrapping.Wrap });
                s.Children.Add(Card(err, new Thickness(0), new Thickness(18, 16)));
                return s;
            }
            s.Children.Add(KpiStrip(host));
            s.Children.Add(Toolbar(host));
            s.Children.Add(new TextBlock { Text = host.FocusText, Foreground = Palette.TextDim, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            s.Children.Add(host.SubTab == 1 ? StatsBody(host) : SessionList(host));
            s.Children.Add(Explain());
            return s;
        }

        private static Control KpiStrip(MainWindow host)
        {
            SessionsSnapshot d = host.Data;
            Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
            g.Children.Add(KpiCard(Symbol.ChatMultiple, "会话总数", d == null ? "—" : d.Count.ToString(),
                "非空 " + (d == null ? "—" : d.NonBlank.ToString()) + " · 运行中 " + (d == null ? "—" : d.Live.ToString()), Palette.Text, 0, -1));
            g.Children.Add(KpiCard(Symbol.Database, "缓存命中率（越高越省钱）", PctText(d == null ? -1 : d.TotalHitPercent),
                "缓存读 " + (d == null ? "—" : SessionRow.Human(d.TotalCacheRead)), Palette.Good, 1, d == null ? -1 : d.TotalHitPercent));
            g.Children.Add(KpiCard(Symbol.Gauge, "解码速度（生成 token 的速度）", TpsText(d == null ? -1 : d.TotalDecodeTps),
                "tok/s，按合计加权", Palette.Accent, 2, -1));
            g.Children.Add(KpiCard(Symbol.DataUsage, "累计 token", d == null ? "—" : SessionRow.Human(d.TotalIn),
                "输出 " + (d == null ? "—" : SessionRow.Human(d.TotalOut)) + " · 输入含缓存读", Palette.Text, 3, -1));
            return g;
        }

        private static Control KpiCard(Symbol icon, string title, string value, string sub, IBrush valueBrush, int col, double barPercent)
        {
            StackPanel s = new StackPanel();
            Grid head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            Border tile = SoftTile(icon, Palette.Accent, Palette.AccentSoft, 30, 15);
            Grid.SetColumn(tile, 0);
            TextBlock label = new TextBlock { Text = title, Foreground = Palette.TextDim, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            Grid.SetColumn(label, 1);
            head.Children.Add(tile); head.Children.Add(label);
            s.Children.Add(head);
            s.Children.Add(new TextBlock { Text = value, FontSize = 24, FontWeight = FontWeight.SemiBold, Foreground = valueBrush, Margin = new Thickness(0, 8, 0, 0) });
            s.Children.Add(new TextBlock { Text = sub, Foreground = Palette.TextFaint, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            if (barPercent >= 0)
            {
                int level = barPercent >= 90 ? 3 : (barPercent >= 70 ? 2 : 1);
                Border slot = new Border { Margin = new Thickness(0, 8, 0, 0), Child = Meter(barPercent, Palette.HitBrush(level), 4) };
                s.Children.Add(slot);
            }
            Border card = Card(s, new Thickness(0, 0, col == 3 ? 0 : 12, 0), new Thickness(16, 14));
            Grid.SetColumn(card, col);
            return card;
        }

        private static Control Toolbar(MainWindow host)
        {
            Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            Control filters = Segmented(new string[] { "全部", "非空", "运行中" }, host.Filter, delegate(int i) { host.SetFilter(i); });
            Grid.SetColumn(filters, 0);

            StackPanel sorts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(14, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
            TextBlock sl = T("排序", 12, Palette.TextDim);
            sl.VerticalAlignment = VerticalAlignment.Center;
            sorts.Children.Add(sl);
            ComboBox box = new ComboBox { MinWidth = 180, SelectedIndex = host.SortMode, FontSize = 12.5 };
            box.Items.Add("最后活动（新→旧）");
            box.Items.Add("输入 token（多→少）");
            box.Items.Add("缓存命中率（低→高）");
            box.Items.Add("上下文压力（高→低）");
            box.Items.Add("解码速度（快→慢）");
            box.SelectionChanged += delegate { host.SortMode = box.SelectedIndex; host.Rerender(); };
            sorts.Children.Add(box);
            Grid.SetColumn(sorts, 1);

            StackPanel rc = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            SymbolIcon ric = Ic(Symbol.ArrowClockwise, 14, Palette.TextDim);
            ric.VerticalAlignment = VerticalAlignment.Center;
            rc.Children.Add(ric);
            TextBlock rt = T("刷新", 12.5, Palette.Text);
            rt.VerticalAlignment = VerticalAlignment.Center;
            rc.Children.Add(rt);
            Button refresh = GhostButton(rc, delegate { host.Refresh(); }, true);
            Grid.SetColumn(refresh, 2);

            g.Children.Add(filters); g.Children.Add(sorts); g.Children.Add(refresh);
            return g;
        }

        /// <summary>按当前风格选会话列表形态：C=紧凑行（密度优先），D=大条形卡片，其余=标准卡片。</summary>
        private static Control SessionList(MainWindow host)
        {
            if (Palette.Compact)
            {
                ItemsControl list = new ItemsControl();
                list.ItemsSource = host.Rows;
                list.ItemTemplate = new FuncDataTemplate<SessionRowVm>(delegate(SessionRowVm vm, INameScope ns) { return SessionRowCompact(vm); });
                Border card = Card(list, new Thickness(0), new Thickness(0));
                card.ClipToBounds = true;
                return card;
            }
            ItemsControl cards = new ItemsControl();
            cards.ItemsSource = host.Rows;
            cards.ItemTemplate = new FuncDataTemplate<SessionRowVm>(delegate(SessionRowVm vm, INameScope ns)
            {
                return Palette.StyleKind == 3 ? SessionCardDash(vm) : SessionCard(vm);
            });
            return cards;
        }

        /// <summary>标准会话卡片：状态点 + 标题/元信息 + 三个指标块（标签+数值+比例条）。</summary>
        private static Control SessionCard(SessionRowVm vm)
        {
            Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,170,170,170") };

            Ellipse dot = new Ellipse { Width = 9, Height = 9, Fill = vm.StatusBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            ToolTip.SetTip(dot, vm.StatusText);
            Grid.SetColumn(dot, 0);

            StackPanel mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            mid.Children.Add(new TextBlock { Text = vm.TitleText, Foreground = Palette.Text, FontSize = 13.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            mid.Children.Add(new TextBlock { Text = vm.ShortId + " · " + vm.MetaText, Foreground = Palette.TextDim, FontSize = 11.5, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
            mid.Children.Add(new TextBlock { Text = vm.DecodeLine, Foreground = Palette.TextFaint, FontSize = 11, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(mid, 1);

            Control m1 = Metric("输入 token", vm.InText, vm.TokenBar, Palette.Text, vm.TokenBrush);
            Control m2 = Metric("缓存命中", vm.HitText, vm.HitBar, vm.HitBrush, vm.HitBrush);
            Control m3 = Metric("上下文压力", vm.CtxText, vm.CtxBar, vm.CtxBrush, vm.CtxBrush);
            Grid.SetColumn(m1, 2); Grid.SetColumn(m2, 3); Grid.SetColumn(m3, 4);
            g.Children.Add(dot); g.Children.Add(mid); g.Children.Add(m1); g.Children.Add(m2); g.Children.Add(m3);

            Border card = Card(g, new Thickness(0, 0, 0, 8), new Thickness(16, 13));
            Hover(card, Palette.CardBg, Palette.CardHover);
            return card;
        }

        private static Control Metric(string label, string value, double pct, IBrush valueBrush, IBrush meterBrush)
        {
            StackPanel s = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
            Grid top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            TextBlock l = T(label, 10.5, Palette.TextFaint);
            Grid.SetColumn(l, 0);
            TextBlock v = T(value, 12, valueBrush, FontWeight.SemiBold);
            Grid.SetColumn(v, 1);
            top.Children.Add(l); top.Children.Add(v);
            s.Children.Add(top);
            s.Children.Add(new Border { Margin = new Thickness(0, 5, 0, 0), Child = Meter(pct, meterBrush, 4) });
            return s;
        }

        /// <summary>C · 深色紧凑：单行会话（发卡线分隔，信息密度优先，无条形）。</summary>
        private static Control SessionRowCompact(SessionRowVm vm)
        {
            Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("86,*,110,110,120,120"), VerticalAlignment = VerticalAlignment.Center };
            TextBlock id = Mono(vm.ShortId, 11, Palette.TextFaint);
            id.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(id, 0);
            TextBlock title = new TextBlock { Text = vm.TitleText, FontSize = 12, Foreground = Palette.Text, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(title, 1);
            TextBlock turns = new TextBlock { Text = vm.MetaText, FontSize = 11, Foreground = Palette.TextDim, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(turns, 2);
            TextBlock tin = new TextBlock { Text = vm.InText, FontSize = 12, Foreground = Palette.Text, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(tin, 3);
            TextBlock hit = new TextBlock { Text = vm.HitText, FontSize = 12, Foreground = vm.HitBrush, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(hit, 4);
            TextBlock dec = new TextBlock { Text = vm.DecodeText, FontSize = 12, Foreground = Palette.TextDim, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(dec, 5);
            g.Children.Add(id); g.Children.Add(title); g.Children.Add(turns); g.Children.Add(tin); g.Children.Add(hit); g.Children.Add(dec);
            Border row = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = Palette.Border,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(14, 7),
                Child = g
            };
            Hover(row, Brushes.Transparent, Palette.CardHover);
            return row;
        }

        /// <summary>D · 浅色仪表盘：把可视化放大 —— 标题行 + 三行大比例条。</summary>
        private static Control SessionCardDash(SessionRowVm vm)
        {
            StackPanel s = new StackPanel { Spacing = 8 };
            Grid head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            Ellipse dot = new Ellipse { Width = 9, Height = 9, Fill = vm.StatusBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            Grid.SetColumn(dot, 0);
            TextBlock title = new TextBlock { Text = vm.TitleText, FontSize = 13.5, FontWeight = FontWeight.SemiBold, Foreground = Palette.Text, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(title, 1);
            TextBlock st = T(vm.StatusText, 11, Palette.TextDim);
            st.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(st, 2);
            head.Children.Add(dot); head.Children.Add(title); head.Children.Add(st);
            s.Children.Add(head);
            s.Children.Add(new TextBlock { Text = vm.ShortId + " · " + vm.MetaText + " · " + vm.DecodeLine, FontSize = 11, Foreground = Palette.TextFaint, TextTrimming = TextTrimming.CharacterEllipsis });
            s.Children.Add(DashMeter("输入 token", vm.InText, vm.TokenBar, vm.TokenBrush));
            s.Children.Add(DashMeter("缓存命中", vm.HitText, vm.HitBar, vm.HitBrush));
            s.Children.Add(DashMeter("上下文压力", vm.CtxText, vm.CtxBar, vm.CtxBrush));
            Border card = Card(s, new Thickness(0, 0, 0, 10), new Thickness(18, 14));
            Hover(card, Palette.CardBg, Palette.CardHover);
            return card;
        }

        private static Control DashMeter(string label, string value, double pct, IBrush brush)
        {
            Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("92,*,76") };
            TextBlock l = T(label, 11, Palette.TextDim);
            l.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(l, 0);
            Border m = new Border { VerticalAlignment = VerticalAlignment.Center, Child = Meter(pct, brush, 7) };
            Grid.SetColumn(m, 1);
            TextBlock v = T(value, 11.5, brush, FontWeight.SemiBold);
            v.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(v, 2);
            g.Children.Add(l); g.Children.Add(m); g.Children.Add(v);
            return g;
        }

        private static Control CardGridBody(MainWindow host)
        {
            WrapPanel wrap = new WrapPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < host.Rows.Count; i++)
            {
                Border card = (Border)SessionCardDash(host.Rows[i]);
                card.Margin = new Thickness(0, 0, 12, 12);
                card.Width = 340;
                wrap.Children.Add(card);
            }
            return wrap;
        }

        // ================================================================ 主从式明细

        private static Control MasterList(MainWindow host)
        {
            ListBox box = new ListBox { ItemsSource = host.Rows, SelectedIndex = host.Rows.Count > 0 ? 0 : -1, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            box.ItemTemplate = new FuncDataTemplate<SessionRowVm>(delegate(SessionRowVm vm, INameScope ns)
            {
                StackPanel s = new StackPanel { Spacing = 3 };
                Grid row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
                Ellipse dot = new Ellipse { Width = 8, Height = 8, Fill = vm.StatusBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                Grid.SetColumn(dot, 0);
                TextBlock title = new TextBlock { Text = vm.TitleText, FontSize = 12.5, Foreground = Palette.Text, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(title, 1);
                row.Children.Add(dot); row.Children.Add(title);
                s.Children.Add(row);
                s.Children.Add(new TextBlock { Text = vm.ShortId + " · " + vm.HitText + " 命中", FontSize = 11, Foreground = Palette.TextDim, Margin = new Thickness(16, 0, 0, 0) });
                return s;
            });
            box.SelectionChanged += delegate { host.ShowDetail(box.SelectedItem as SessionRowVm); };
            host.ShowDetail(host.Rows.Count > 0 ? host.Rows[0] : null);
            return box;
        }

        private static Control DetailCard(MainWindow host)
        {
            StackPanel s = new StackPanel { Spacing = 12 };
            host.DetailHost = s;
            s.Children.Add(T("（左侧选一个会话）", 12, Palette.TextFaint));
            return Card(s, new Thickness(0), new Thickness(18, 16));
        }

        public static void FillDetail(StackPanel host, SessionRowVm vm)
        {
            if (host == null) return;
            host.Children.Clear();
            if (vm == null)
            {
                host.Children.Add(T("（左侧选一个会话）", 12, Palette.TextFaint));
                return;
            }
            host.Children.Add(Mono(vm.ShortId, 20, Palette.Text));
            StackPanel meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            meta.Children.Add(Chip(vm.StatusText, vm.StatusBrush, Palette.SoftOf(vm.StatusBrush)));
            TextBlock mt = T(vm.MetaText, 12, Palette.TextDim);
            mt.VerticalAlignment = VerticalAlignment.Center;
            meta.Children.Add(mt);
            host.Children.Add(meta);
            host.Children.Add(new Border { Height = 1, Background = Palette.Border, Margin = new Thickness(0, 4, 0, 4) });
            host.Children.Add(KpiRow("输入 token", vm.InText, vm.TokenBar, vm.TokenBrush));
            host.Children.Add(KpiRow("缓存命中率", vm.HitText, vm.HitBar, vm.HitBrush));
            host.Children.Add(KpiRow("上下文压力", vm.CtxText, vm.CtxBar, vm.CtxBrush));
            host.Children.Add(new Border { Height = 1, Background = Palette.Border, Margin = new Thickness(0, 4, 0, 4) });
            host.Children.Add(T("解码速度 " + vm.DecodeText + "　首 token " + vm.TtftText, 12, Palette.TextDim));
            host.Children.Add(T("缓存读 " + vm.CacheReadText, 11, Palette.TextFaint));
        }

        private static Control KpiRow(string label, string value, double percent, IBrush brush)
        {
            Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("96,110,*") };
            TextBlock l = T(label, 12, Palette.TextDim);
            l.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(l, 0);
            TextBlock v = T(value, 14, brush, FontWeight.SemiBold);
            v.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(v, 1);
            Border m = new Border { VerticalAlignment = VerticalAlignment.Center, Child = Meter(percent, brush, 6) };
            Grid.SetColumn(m, 2);
            g.Children.Add(l); g.Children.Add(v); g.Children.Add(m);
            return g;
        }

        // ================================================================ 状态（概览）

        /// <summary>看板（概览子菜单）：一键启动/停止 + token 消耗 / 缓存命中 / 解码速度 / 会话数。</summary>
        private static Control StatusContent(MainWindow host)
        {
            if (host.SubTab == 1) return StatusDetail(host);
            StatusSnapshot st = host.Status;
            StackPanel s = new StackPanel { Margin = new Thickness(20, 0, 20, 12), Spacing = 12 };

            StackPanel hero = new StackPanel { Spacing = 10 };
            StackPanel head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            bool known = st != null && st.Ok;
            bool up = known && st.State == 0;
            IBrush stateBrush = up ? Palette.Good : (known && st.State == 1 ? Palette.Warn : Palette.Bad);
            head.Children.Add(new Ellipse { Width = 14, Height = 14, Fill = stateBrush, VerticalAlignment = VerticalAlignment.Center });
            head.Children.Add(T(known ? st.StateText : "未知", 26, stateBrush, FontWeight.Bold));
            if (known && !string.IsNullOrEmpty(st.Uptime)) head.Children.Add(T("已运行 " + st.Uptime, 12, Palette.TextDim));
            hero.Children.Add(head);

            StackPanel acts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            Button start = new Button { Content = "▶  一键启动 dsh" };
            start.Click += delegate { host.StartDsh(); };
            acts.Children.Add(start);
            Button stop = new Button { Content = "■  停止" };
            stop.Click += delegate { host.StopDsh(); };
            acts.Children.Add(stop);
            acts.Children.Add(T("只有你点的时候才执行；启动/停止都走工具箱核心（非交互命令）", 11, Palette.TextFaint));
            hero.Children.Add(acts);
            s.Children.Add(Card(hero, new Thickness(0), new Thickness(18, 16)));

            s.Children.Add(KpiStrip(host));

            if (!string.IsNullOrEmpty(host.ActionLog))
                s.Children.Add(Card(T(host.ActionLog, 11.5, Palette.TextDim), new Thickness(0), new Thickness(16, 12)));
            return s;
        }

        /// <summary>统计视图（会话页「统计」子菜单）：总量、命中率、速度 + 最耗 token 的会话排行。</summary>
        private static Control StatsBody(MainWindow host)
        {
            SessionsSnapshot d = host.Data;
            StackPanel s = new StackPanel { Spacing = 12 };
            if (d == null || !d.Ok)
            {
                s.Children.Add(Card(T("没有可统计的会话数据。", 12, Palette.TextDim), new Thickness(0), new Thickness(16, 14)));
                return s;
            }
            StackPanel sum = new StackPanel { Spacing = 8 };
            sum.Children.Add(T("累计输入 token", 12, Palette.TextDim));
            sum.Children.Add(T(SessionRow.Human(d.TotalIn), 34, Palette.Text, FontWeight.Bold));
            sum.Children.Add(T("输出 " + SessionRow.Human(d.TotalOut) + " · 缓存读 " + SessionRow.Human(d.TotalCacheRead) + " · 会话 " + d.Count + " 个（非空 " + d.NonBlank + "）", 11.5, Palette.TextFaint));
            sum.Children.Add(Meter(d.TotalHitPercent < 0 ? 0 : d.TotalHitPercent, Palette.HitBrush(d.TotalHitPercent >= 90 ? 3 : (d.TotalHitPercent >= 70 ? 2 : 1)), 8));
            sum.Children.Add(T("缓存命中率 " + PctText(d.TotalHitPercent) + "　解码速度 " + TpsText(d.TotalDecodeTps) + " tok/s", 12.5, Palette.TextDim));
            s.Children.Add(Card(sum, new Thickness(0), new Thickness(18, 16)));

            List<SessionRow> top = SessionsView.Sort(SessionsView.Filter(d.Rows, SessionsView.FilterAll), 1);
            StackPanel list = new StackPanel { Spacing = 6 };
            list.Children.Add(T("输入 token 最多的会话", 13, Palette.Text, FontWeight.Bold));
            for (int i = 0; i < top.Count && i < 10; i++)
            {
                SessionRow r = top[i];
                string name = string.IsNullOrEmpty(r.Title) ? r.ShortId : r.Title;
                list.Children.Add(T((i + 1) + ".　" + name + "　　" + r.InText + "　命中 " + r.HitText + "　" + r.DecodeText, 12, Palette.TextDim));
            }
            s.Children.Add(Card(list, new Thickness(0), new Thickness(16, 14)));
            return s;
        }
        /// <summary>体检页：结论徽章 + 分级条目（错误在前），原始输出另有一页。</summary>
        private static Control HealthContent(MainWindow host)
        {
            DoctorSummary d = host.Doctor;
            StackPanel s = new StackPanel { Margin = PageMargin, Spacing = 14 };
            if (host.SubTab == 1) { s.Children.Add(new Border { Child = RawCard(host, "原始输出", "doctor 的标记行与分级条目原文") }); return s; }
            if (d == null || !d.Ok)
            {
                s.Children.Add(Card(T("还没有体检结果。进这一页会自动跑一次 doctor（会检查网络，可能需要几秒）。", 12, Palette.TextDim), new Thickness(0), new Thickness(16, 14)));
                return s;
            }
            IBrush b = d.Error > 0 ? Palette.Bad : (d.Warn > 0 ? Palette.Warn : Palette.Good);
            StackPanel head = new StackPanel { Spacing = 6 };
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            row.Children.Add(new Ellipse { Width = 14, Height = 14, Fill = b, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(T(d.Headline, 24, b, FontWeight.Bold));
            head.Children.Add(row);
            head.Children.Add(T("错误 " + d.Error + " · 提醒 " + d.Warn + " · 通过 " + d.Pass + "（只依据工具箱自己的检查结果，不替它下别的结论）", 12, Palette.TextDim));
            s.Children.Add(Card(head, new Thickness(0), new Thickness(18, 16)));

            if (d.ErrorLines.Count > 0 || d.WarnLines.Count > 0)
            {
                StackPanel list = new StackPanel { Spacing = 6 };
                for (int i = 0; i < d.ErrorLines.Count; i++) list.Children.Add(T(d.ErrorLines[i], 12, Palette.Bad));
                for (int i = 0; i < d.WarnLines.Count; i++) list.Children.Add(T(d.WarnLines[i], 12, Palette.Warn));
                s.Children.Add(Card(list, new Thickness(0), new Thickness(16, 14)));
            }
            return s;
        }
        /// <summary>备份页：清单（名称/类型/大小/时间）+ 立即备份 / 导出 / 恢复预览 / 应用恢复 / 删除（两次确认）。</summary>
        private static Control BackupContent(MainWindow host)
        {
            StackPanel s = new StackPanel { Margin = PageMargin, Spacing = 14 };
            List<BackupItem> items = host.BackupItems;

            StackPanel bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            Button mk = new Button { Content = "＋ 立即备份" };
            mk.Click += delegate { host.CreateBackup(); };
            bar.Children.Add(mk);
            bar.Children.Add(T("共 " + items.Count + " 份（" + (host.Backups != null && host.Backups.Ok ? "backup-list 有效包" : "未读到清单") + "）", 12, Palette.TextDim));
            s.Children.Add(Card(bar, new Thickness(0), new Thickness(16, 14)));

            if (items.Count == 0)
                s.Children.Add(Card(T("还没有备份。点「立即备份」创建第一份（空数据根不会被算作有效备份，这是刻意的规则）。", 12, Palette.TextDim), new Thickness(0), new Thickness(16, 14)));
            for (int i = 0; i < items.Count; i++)
            {
                BackupItem b = items[i];
                StackPanel row = new StackPanel { Spacing = 8 };
                StackPanel head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                head.Children.Add(Chip(b.KindText, b.Kind == "Manual" ? Palette.Accent : Palette.TextDim, b.Kind == "Manual" ? Palette.AccentSoft : Palette.BarTrack));
                head.Children.Add(T(b.Name, 12.5, Palette.Text, FontWeight.SemiBold));
                head.Children.Add(T(b.SizeText + "　" + b.Time, 11.5, Palette.TextFaint));
                row.Children.Add(head);
                StackPanel acts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                Button ex = new Button { Content = "导出", FontSize = 11.5 };
                ex.Click += delegate { host.ExportBackup(b.Name); };
                acts.Children.Add(ex);
                Button dr = new Button { Content = "恢复预览", FontSize = 11.5 };
                dr.Click += delegate { host.DryRunRestore(b.Name); };
                acts.Children.Add(dr);
                Button ap = new Button { Content = "应用恢复（仅隔离数据根）", FontSize = 11.5 };
                ap.Click += delegate { host.ApplyRestore(b.Name); };
                acts.Children.Add(ap);
                string key = "del:" + b.Name;
                bool armed = host.PendingDelete == key;
                Button del = new Button
                {
                    Content = armed ? "再点一次确认删除" : "删除",
                    FontSize = 11.5,
                    Background = armed ? Palette.Bad : Palette.WarnSoft,
                    Foreground = armed ? Brushes.White : Palette.Warn
                };
                del.Click += delegate
                {
                    if (host.PendingDelete != key) { host.PendingDelete = key; host.Rebuild(); return; }
                    host.PendingDelete = "";
                    host.DeleteBackup(b.Name);
                };
                acts.Children.Add(del);
                row.Children.Add(acts);
                s.Children.Add(Card(row, new Thickness(0), new Thickness(16, 14)));
            }

            if (!string.IsNullOrEmpty(host.ActionLog))
                s.Children.Add(Card(T(host.ActionLog, 11.5, Palette.TextDim), new Thickness(0), new Thickness(16, 12)));
            s.Children.Add(Card(T("恢复是合并语义：只覆盖同名文件，不删除目标端独有的文件；「应用恢复」只允许写入隔离数据根（CLI 的准入闸门会拒绝其它情况并把原因显示在上面）。", 11.5, Palette.TextFaint), new Thickness(0), new Thickness(16, 12)));
            return s;
        }

        /// <summary>设置页：逐键编辑（config-get / config-set），开关型给两个按钮，只读键禁编辑。</summary>
        private static Control SettingsContent(MainWindow host)
        {
            StackPanel s = new StackPanel { Margin = PageMargin, Spacing = 12 };
            List<ConfigItem> items = host.Config;
            if (items.Count == 0)
            {
                s.Children.Add(Card(T("没有读到配置项（CLI 未返回 CONFIG 行）。", 12, Palette.TextDim), new Thickness(0), new Thickness(16, 14)));
                return s;
            }
            s.Children.Add(Card(T("配置写入会立即生效并落盘（CLI 的 config-set）；键名与 v2.x 完全一致，可用文本编辑器对照。", 11.5, Palette.TextFaint), new Thickness(0), new Thickness(16, 12)));
            for (int i = 0; i < items.Count; i++)
            {
                ConfigItem c = items[i];
                StackPanel row = new StackPanel { Spacing = 8 };
                StackPanel head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                head.Children.Add(T(c.Key, 12.5, Palette.Text, FontWeight.SemiBold));
                head.Children.Add(T(c.Desc, 11.5, Palette.TextDim));
                row.Children.Add(head);

                StackPanel edit = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                if (c.ReadOnly)
                {
                    edit.Children.Add(T(c.Value, 12, Palette.TextFaint));
                }
                else if (c.IsSwitch)
                {
                    string[] opts = new string[] { "on", "off" };
                    for (int k = 0; k < opts.Length; k++)
                    {
                        string val = opts[k];
                        bool active = c.Value == val;
                        Button ob = new Button
                        {
                            Content = val,
                            FontSize = 11.5,
                            Background = active ? Palette.AccentSoft : Brushes.Transparent,
                            Foreground = active ? Palette.Accent : Palette.TextDim
                        };
                        ob.Click += delegate { host.SetConfig(c.Key, val); };
                        edit.Children.Add(ob);
                    }
                }
                else
                {
                    TextBox box = new TextBox { Text = c.Value, Width = 260, FontSize = 12 };
                    edit.Children.Add(box);
                    Button save = new Button { Content = "保存", FontSize = 11.5 };
                    save.Click += delegate { host.SetConfig(c.Key, box.Text == null ? "" : box.Text.Trim()); };
                    edit.Children.Add(save);
                    if (c.Key == "ws") edit.Children.Add(T("留空=自动探测；填了必须存在", 11, Palette.TextFaint));
                }
                row.Children.Add(edit);
                s.Children.Add(Card(row, new Thickness(0), new Thickness(16, 12)));
            }
            if (!string.IsNullOrEmpty(host.ActionLog))
                s.Children.Add(Card(T(host.ActionLog, 11.5, Palette.TextDim), new Thickness(0), new Thickness(16, 12)));
            return s;
        }
        /// <summary>看板图表：近 14 天新增会话（柱状）+ 缓存命中率分布（柱状）。
        /// **手绘**（Grid + Border 柱），不引入任何图表依赖；日期用 ISO 字符串前缀比对，不做时区/日历运算（不猜）。</summary>
        private static Control ChartsBody(MainWindow host)
        {
            SessionsSnapshot d = host.Data;
            StackPanel s = new StackPanel { Spacing = 14 };
            if (d == null || !d.Ok)
            {
                s.Children.Add(Card(T("没有可绘制的会话数据。", 12, Palette.TextDim), new Thickness(0), new Thickness(16, 14)));
                return s;
            }

            // ① 近 14 天新增会话
            int days = 14;
            string[] labels = new string[days];
            long[] counts = new long[days];
            System.DateTime today = System.DateTime.UtcNow.Date;
            for (int i = 0; i < days; i++) labels[i] = today.AddDays(i - (days - 1)).ToString("MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            long max = 0;
            for (int i = 0; i < d.Rows.Count; i++)
            {
                string created = d.Rows[i].Created;
                if (string.IsNullOrEmpty(created) || created.Length < 10) continue;
                string day = created.Substring(0, 10);
                for (int k = 0; k < days; k++)
                {
                    if (labels[k].Length == 5 && day.Length == 10 && day.Substring(5, 5) == labels[k]) { counts[k]++; if (counts[k] > max) max = counts[k]; break; }
                }
            }
            StackPanel c1 = new StackPanel { Spacing = 8 };
            c1.Children.Add(T("近 14 天新增会话（按 dsh 记录的创建时间，UTC 日期）", 13, Palette.Text, FontWeight.Bold));
            c1.Children.Add(BarChart(labels, counts, max, Palette.Accent, "个"));
            c1.Children.Add(T("最高 " + max + " 个/天　合计 " + Sum(counts) + " 个（创建时间缺失的会话不计入，不猜）", 11.5, Palette.TextFaint));
            s.Children.Add(Card(c1, new Thickness(0), new Thickness(18, 16)));

            // ② 缓存命中率分布
            long low = 0, mid = 0, high = 0, unknown = 0;
            for (int i = 0; i < d.Rows.Count; i++)
            {
                double h = d.Rows[i].HitPercent;
                if (h < 0) unknown++;
                else if (h < 70) low++;
                else if (h < 90) mid++;
                else high++;
            }
            string[] hl = new string[] { "< 70%", "70–90%", "≥ 90%", "unknown" };
            long[] hc = new long[] { low, mid, high, unknown };
            StackPanel c2 = new StackPanel { Spacing = 8 };
            c2.Children.Add(T("缓存命中率分布（会话数）", 13, Palette.Text, FontWeight.Bold));
            c2.Children.Add(BarChart(hl, hc, Math.Max(Math.Max(low, mid), Math.Max(high, unknown)), Palette.Good, "个"));
            c2.Children.Add(T("命中率越高越省钱；unknown 表示该会话没有这个字段（空会话），我们不会把它算成 0%。", 11.5, Palette.TextFaint));
            s.Children.Add(Card(c2, new Thickness(0), new Thickness(18, 16)));
            return s;
        }

        private static long Sum(long[] a) { long s = 0; for (int i = 0; i < a.Length; i++) s += a[i]; return s; }

        /// <summary>柱状图：等宽柱子 + 底部标签（纯 Grid/Border，零依赖）。</summary>
        private static Control BarChart(string[] labels, long[] values, long max, IBrush brush, string unit)
        {
            Grid g = new Grid { Height = 132 };
            for (int i = 0; i < labels.Length; i++) g.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            for (int i = 0; i < labels.Length; i++)
            {
                long v = values[i];
                double h = max <= 0 ? 2 : 4 + (v * 88.0 / max);
                StackPanel col = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Spacing = 3, Margin = new Thickness(2, 0) };
                col.Children.Add(T(v == 0 ? "" : v.ToString(), 10, Palette.TextDim));
                col.Children.Add(new Border { Height = h, CornerRadius = new CornerRadius(3), Background = v == 0 ? Palette.BarTrack : brush });
                col.Children.Add(T(labels[i], 9.5, Palette.TextFaint));
                Grid.SetColumn(col, i);
                g.Children.Add(col);
            }
            return g;
        }
        private static Control OverviewContent(MainWindow host)
        {
            if (host.SubTab == 1) return new Border { Margin = PageMargin, Child = RawCard(host, "原始输出", "status --detail 的标记行原文") };
            return StatusDetail(host);
        }

        /// <summary>看板：指标（KPI + 操作日志）与图表（近 14 天新增会话、命中率分布）。</summary>
        private static Control BoardContent(MainWindow host)
        {
            StackPanel s = new StackPanel { Margin = PageMargin, Spacing = 14 };
            if (host.SubTab == 1) { s.Children.Add(ChartsBody(host)); return s; }
            s.Children.Add(KpiStrip(host));
            if (!string.IsNullOrEmpty(host.ActionLog)) s.Children.Add(Card(T(host.ActionLog, 11.5, Palette.TextDim), new Thickness(0), new Thickness(16, 12)));
            return s;
        }

        private static Control StatusDetail(MainWindow host)
        {
            StatusSnapshot st = host.Status;
            StackPanel s = new StackPanel { Margin = PageMargin, Spacing = 14 };
            if (st == null || !st.Ok)
            {
                s.Children.Add(Card(new TextBlock { Text = "读不到状态（CLI 未返回 STATUS_* 标记）。", Foreground = Palette.TextDim, FontSize = 12 }, new Thickness(0), new Thickness(18, 16)));
                return s;
            }

            // —— 状态 hero ——
            IBrush stateBrush = st.State == 0 ? Palette.Good : (st.State == 1 ? Palette.Warn : Palette.Bad);
            Grid hero = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            Grid halo = new Grid { Width = 44, Height = 44, VerticalAlignment = VerticalAlignment.Center };
            halo.Children.Add(new Ellipse { Width = 44, Height = 44, Fill = Palette.SoftOf(stateBrush) });
            halo.Children.Add(new Ellipse { Width = 14, Height = 14, Fill = stateBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(halo, 0);
            StackPanel ht = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Spacing = 4 };
            ht.Children.Add(T(st.StateText, 26, stateBrush, FontWeight.SemiBold));
            ht.Children.Add(new TextBlock
            {
                Text = "依据只来自可观测事实：本地端口是否监听 + 进程是否存在。dsh 换了形态（例如 headless 没有端口）时，这里会如实显示未运行，而不是假装就绪。",
                Foreground = Palette.TextDim,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
            Grid.SetColumn(ht, 1);
            hero.Children.Add(halo); hero.Children.Add(ht);
            s.Children.Add(Card(hero, new Thickness(0), new Thickness(18, 16)));

            // —— 运行时事实 ——
            Grid facts = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") };
            facts.Children.Add(StatCard(Symbol.NumberSymbol, "进程 PID", string.IsNullOrEmpty(st.Pid) ? "—" : st.Pid, "运行 dsh 的进程号", Palette.Text, -1, 0, 3));
            facts.Children.Add(StatCard(Symbol.Calendar, "启动时间", string.IsNullOrEmpty(st.Start) ? "—" : st.Start, "dsh 启动的时刻", Palette.Text, -1, 1, 3));
            facts.Children.Add(StatCard(Symbol.Clock, "已运行", string.IsNullOrEmpty(st.Uptime) ? "—" : st.Uptime, "从启动到现在", Palette.Accent, -1, 2, 3));
            s.Children.Add(facts);

            // —— 总览指标（把其它页的要点也摆到这里，省得来回点）——
            ProfilesSnapshot pf = host.Profiles;
            SessionsSnapshot se = host.Data;
            BackupSummary bk = host.Backups;
            DoctorSummary dc = host.Doctor;
            int bundleCount = 0; int thirdCount = 0; string formText = "—";
            if (pf != null && pf.Ok && pf.Profiles.Count > 0)
            {
                for (int i = 0; i < pf.Profiles.Count; i++) { bundleCount += pf.Profiles[i].Bundles; thirdCount += pf.Profiles[i].ThirdParty; }
                formText = pf.Profiles[0].FormText;
            }
            Grid row1 = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
            row1.Children.Add(StatCard(Symbol.Box, "当前形态", formText, "来自 profile 的 dsh.profile.bundles", Palette.Text, -1, 0, 4));
            row1.Children.Add(StatCard(Symbol.PuzzlePiece, "profile / 插件", (pf == null ? "—" : pf.Count.ToString()) + " / " + thirdCount, bundleCount + " 个组合包（含官方）", Palette.Text, -1, 1, 4));
            row1.Children.Add(StatCard(Symbol.ChatMultiple, "会话", se == null ? "—" : se.Count.ToString(), "非空 " + (se == null ? "—" : se.NonBlank.ToString()) + " · 运行中 " + (se == null ? "—" : se.Live.ToString()), Palette.Text, -1, 2, 4));
            row1.Children.Add(StatCard(Symbol.DataUsage, "累计输入 token", se == null ? "—" : SessionRow.Human(se.TotalIn), "输出 " + (se == null ? "—" : SessionRow.Human(se.TotalOut)), Palette.Text, -1, 3, 4));
            s.Children.Add(row1);

            Grid row2 = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
            row2.Children.Add(StatCard(Symbol.Database, "缓存命中率", se == null ? "—" : PctText(se.TotalHitPercent), "越高越省钱", Palette.Good, se == null ? -1 : se.TotalHitPercent, 0, 4));
            row2.Children.Add(StatCard(Symbol.Gauge, "解码速度", se == null ? "—" : TpsText(se.TotalDecodeTps), "tok/s（按合计加权）", Palette.Accent, -1, 1, 4));
            row2.Children.Add(StatCard(Symbol.Archive, "备份", bk == null || !bk.Ok ? "—" : bk.Count.ToString(), "份（backup-list）", Palette.Text, -1, 2, 4));
            row2.Children.Add(StatCard(Symbol.Stethoscope, "体检", dc == null || !dc.Ok ? "未运行" : dc.Headline, "点下面按钮运行 doctor", dc != null && dc.Error > 0 ? Palette.Bad : (dc != null && dc.Warn > 0 ? Palette.Warn : Palette.Good), -1, 3, 4));
            s.Children.Add(row2);

            // —— 快捷入口 ——
            Grid jumps = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*") };
            int[] targets = new int[] { 1, 2, 3, 4, 5 };
            for (int i = 0; i < targets.Length; i++)
            {
                Control jc = JumpCard(host, targets[i], i == targets.Length - 1);
                Grid.SetColumn(jc, i);
                jumps.Children.Add(jc);
            }
            s.Children.Add(jumps);

            if (st.Extras.Count > 0)
            {
                StackPanel ex = new StackPanel { Spacing = 6 };
                ex.Children.Add(T("CLI 还报告了这些（未识别的标记原样展示）", 12, Palette.TextDim));
                for (int i = 0; i < st.Extras.Count; i++)
                    ex.Children.Add(Mono(st.Extras[i].Key + "  " + st.Extras[i].Value, 11.5, Palette.Text));
                s.Children.Add(Card(ex, new Thickness(0), new Thickness(16, 14)));
            }
            s.Children.Add(RawCard(host, "原始标记行", "status --detail 的 CLI 输出原文"));
            return s;
        }

        private static Control StatCard(Symbol icon, string label, string value, string sub, IBrush valueBrush, double pct, int col, int cols)
        {
            StackPanel s = new StackPanel();
            Grid head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            Border tile = SoftTile(icon, Palette.Accent, Palette.AccentSoft, 30, 15);
            Grid.SetColumn(tile, 0);
            TextBlock l = new TextBlock { Text = label, FontSize = 11.5, Foreground = Palette.TextDim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(l, 1);
            head.Children.Add(tile); head.Children.Add(l);
            s.Children.Add(head);
            s.Children.Add(new TextBlock { Text = value, FontSize = 22, FontWeight = FontWeight.SemiBold, Foreground = valueBrush, Margin = new Thickness(0, 8, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
            s.Children.Add(new TextBlock { Text = sub, FontSize = 11, Foreground = Palette.TextFaint, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap });
            if (pct >= 0)
            {
                int level = pct >= 90 ? 3 : (pct >= 70 ? 2 : 1);
                s.Children.Add(new Border { Margin = new Thickness(0, 8, 0, 0), Child = Meter(pct, Palette.HitBrush(level), 4) });
            }
            Border card = Card(s, new Thickness(0, 0, col == cols - 1 ? 0 : 12, 0), new Thickness(16, 14));
            Grid.SetColumn(card, col);
            return card;
        }

        private static Control JumpCard(MainWindow host, int section, bool last)
        {
            Grid row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            Border tile = SoftTile(MainWindow.NavIcons[section], Palette.Accent, Palette.AccentSoft, 32, 16);
            Grid.SetColumn(tile, 0);
            TextBlock label = T(MainWindow.NavItems[section], 13, Palette.Text, FontWeight.SemiBold);
            label.Margin = new Thickness(10, 0, 0, 0);
            label.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(label, 1);
            SymbolIcon chev = Ic(Symbol.ChevronRight, 13, Palette.TextFaint);
            chev.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(chev, 2);
            row.Children.Add(tile); row.Children.Add(label); row.Children.Add(chev);

            Button b = new Button
            {
                Content = row,
                Background = Palette.CardBg,
                BorderBrush = Palette.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 12),
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            Hover(b, Palette.CardBg, Palette.CardHover);
            b.Click += delegate { host.SetMainSection(section); };
            Border wrap = new Border { Margin = new Thickness(0, 0, last ? 0 : 12, 0), CornerRadius = new CornerRadius(12), Child = b };
            if (Palette.CardShadow.Length > 0) wrap.BoxShadow = BoxShadows.Parse(Palette.CardShadow);
            return wrap;
        }

        // ================================================================ 形态与插件

        private static Control ProfilesContent(MainWindow host)
        {
            ProfilesSnapshot d = host.Profiles;
            StackPanel s = new StackPanel { Margin = PageMargin, Spacing = 12 };
            if (d == null || !d.Ok)
            {
                s.Children.Add(Card(new TextBlock
                {
                    Text = d == null ? "正在读取…" : (string.IsNullOrEmpty(d.FailReason) ? "没有可显示的 profile 信息。" : d.FailReason),
                    Foreground = Palette.TextDim,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap
                }, new Thickness(0), new Thickness(18, 16)));
                return s;
            }
            int totalBundles = 0; int totalThird = 0;
            for (int i = 0; i < d.Profiles.Count; i++) { totalBundles += d.Profiles[i].Bundles; totalThird += d.Profiles[i].ThirdParty; }
            s.Children.Add(new TextBlock
            {
                Text = d.Count + " 个 profile · " + totalBundles + " 个组合包 · 其中第三方插件 " + totalThird + " 个（数据来自各 profile 的 package.json 里 dsh.profile.bundles）",
                Foreground = Palette.TextDim, FontSize = 12, TextWrapping = TextWrapping.Wrap
            });

            // —— 工具行：过滤 + 搜索（搜索框就地刷新下面的卡片列表，不重建整页，避免输入框丢焦点）——
            Grid tools = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            Control chips = Segmented(new string[] { "全部", "只看第三方", "只看官方" }, host.ProfilesFilter, delegate(int i) { host.SetProfilesFilter(i); });
            Grid.SetColumn(chips, 0);
            TextBox search = new TextBox
            {
                Watermark = "搜索 profile 或插件 id…",
                Width = 260,
                Text = host.ProfileSearch,
                Background = Palette.InsetBg,
                BorderBrush = Palette.Border,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 6),
                FontSize = 12.5,
                Foreground = Palette.Text
            };
            Grid.SetColumn(search, 2);
            tools.Children.Add(chips); tools.Children.Add(search);
            s.Children.Add(tools);

            s.Children.Add(HealthCard(host));

            StackPanel cardHost = new StackPanel { Spacing = 12 };
            search.TextChanged += delegate
            {
                host.ProfileSearch = search.Text == null ? "" : search.Text;
                RebuildProfileCards(cardHost, host);
            };
            RebuildProfileCards(cardHost, host);
            s.Children.Add(cardHost);

            s.Children.Add(Card(new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    T("这一页怎么读？", 13.5, Palette.Text, FontWeight.SemiBold),
                    Line("• 形态来自每个 profile 的 package.json 里 dsh.profile.bundles：启用 dsh-web-app = Web，dsh-headless = Headless（没有端口），dsh-acp-app = ACP。"),
                    Line("• 这是**配置形态**，不是运行形态 —— 「dsh 在跑」仍然由端口/进程等运行时事实判断（状态页看）。"),
                    Line("• 官方 = @deepseek-ai/* 的组合包；第三方 = 你自己加的插件（例如 dsh-web-search-tavily）。")
                }
            }, new Thickness(0), new Thickness(18, 16)));
            return s;
        }

        private static Control HealthCard(MainWindow host)
        {
            StackPanel s = new StackPanel { Spacing = 10 };
            Grid head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            Border tile = SoftTile(Symbol.Stethoscope, Palette.Accent, Palette.AccentSoft, 34, 17);
            Grid.SetColumn(tile, 0);
            StackPanel tt = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
            tt.Children.Add(T("健康检查 · profilecheck", 13.5, Palette.Text, FontWeight.SemiBold));
            tt.Children.Add(T("检查 profile 的语法 / 重复 id / 缺字段，并给出处方", 11.5, Palette.TextFaint));
            Grid.SetColumn(tt, 1);
            Button run = PrimaryButton("运行检查", delegate { host.LoadHealth(); });
            run.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(run, 2);
            head.Children.Add(tile); head.Children.Add(tt); head.Children.Add(run);
            s.Children.Add(head);
            if (!string.IsNullOrEmpty(host.Health))
            {
                s.Children.Add(new Border
                {
                    Background = Palette.InsetBg,
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 10),
                    MaxHeight = 320,
                    Child = new ScrollViewer
                    {
                        Content = new TextBlock { Text = host.Health, FontFamily = MonoFont, FontSize = 11.5, Foreground = Palette.TextDim, TextWrapping = TextWrapping.Wrap }
                    }
                });
            }
            return Card(s, new Thickness(0), new Thickness(18, 16));
        }

        /// <summary>按过滤 + 搜索条件重画 profile 卡片（只换列表，不动整页）。</summary>
        private static void RebuildProfileCards(StackPanel cardHost, MainWindow host)
        {
            cardHost.Children.Clear();
            ProfilesSnapshot d = host.Profiles;
            if (d == null || !d.Ok) return;
            string q = (host.ProfileSearch ?? "").Trim();
            int shown = 0;
            for (int i = 0; i < d.Profiles.Count; i++)
            {
                ProfileCard p = d.Profiles[i];
                bool nameHit = q.Length > 0 && p.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
                List<BundleItem> items = MatchBundles(p, host.ProfilesFilter, nameHit ? "" : q);
                if (host.ProfilesFilter == 1 && p.ThirdParty == 0) continue;
                if (q.Length > 0 && !nameHit && items.Count == 0) continue;
                cardHost.Children.Add(ProfileCard(host, p, items, q));
                shown++;
            }
            if (shown == 0)
            {
                cardHost.Children.Add(Card(T("没有匹配的 profile —— 换个关键词或过滤条件试试。", 12, Palette.TextDim), new Thickness(0), new Thickness(18, 16)));
            }
        }

        /// <summary>过滤 bundle 列表：1=只看第三方 2=只看官方；搜索词只保留 id 命中的条目。</summary>
        private static List<BundleItem> MatchBundles(ProfileCard p, int filter, string q)
        {
            List<BundleItem> r = new List<BundleItem>();
            for (int i = 0; i < p.Items.Count; i++)
            {
                BundleItem it = p.Items[i];
                if (filter == 1 && it.Official) continue;
                if (filter == 2 && !it.Official) continue;
                if (q.Length > 0 && it.Id.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                r.Add(it);
            }
            return r;
        }

        private static Control ProfileCard(MainWindow host, ProfileCard p, List<BundleItem> items, string q)
        {
            StackPanel card = new StackPanel { Spacing = 10 };

            StackPanel head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            head.Children.Add(new TextBlock { Text = p.Name, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Palette.Text, VerticalAlignment = VerticalAlignment.Center });
            IBrush fb = Palette.FormBrush(p.FormKind);
            head.Children.Add(Chip(p.FormText, Palette.OnAccent, fb));
            TextBlock cnt = T(p.CountText, 12, Palette.TextDim);
            cnt.VerticalAlignment = VerticalAlignment.Center;
            head.Children.Add(cnt);
            card.Children.Add(head);

            if (p.Disabled.Count > 0)
            {
                StackPanel dis = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                SymbolIcon wic = Ic(Symbol.Warning, 13, Palette.Warn);
                wic.VerticalAlignment = VerticalAlignment.Center;
                dis.Children.Add(wic);
                TextBlock dt = T(p.DisabledText, 12, Palette.Warn);
                dt.VerticalAlignment = VerticalAlignment.Center;
                dis.Children.Add(dt);
                card.Children.Add(dis);
            }

            if (items.Count == 0)
            {
                card.Children.Add(T("该条件下没有可显示的条目。", 11.5, Palette.TextFaint));
            }
            for (int b = 0; b < items.Count; b++)
            {
                BundleItem it = items[b];
                bool disabled = p.Disabled.Contains(it.Id);
                Grid row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto") };
                Border kind = Chip(it.KindText, it.Official ? Palette.Accent : Palette.Warn, it.Official ? Palette.AccentSoft : Palette.WarnSoft);
                Grid.SetColumn(kind, 0);
                StackPanel idv = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                idv.Children.Add(Mono(it.Id, 12, disabled ? Palette.TextFaint : Palette.Text));
                if (it.VersionText.Length > 0) idv.Children.Add(T(it.VersionText, 11, Palette.TextFaint));
                if (disabled) idv.Children.Add(Chip("已隔离", Palette.Warn, Palette.WarnSoft));
                Grid.SetColumn(idv, 1);
                row.Children.Add(kind); row.Children.Add(idv);

                string folder = host.BundleFolder(p.Name, it.Id);
                if (folder.Length > 0)
                {
                    Button open = GhostButton(Ic(Symbol.FolderOpen, 13, Palette.TextDim), delegate { host.OpenFolder(folder); }, false);
                    open.Padding = new Thickness(6, 3);
                    ToolTip.SetTip(open, "在文件管理器中打开插件目录");
                    Grid.SetColumn(open, 2);
                    row.Children.Add(open);
                }

                string key = p.Name + "|" + it.Id;
                Button act = disabled
                    ? ConfirmButton(key, "恢复", "再点一次确认恢复", host, delegate { host.PatchEntry(p.Name, it.Id, false); host.Refresh(); })
                    : ConfirmButton(key, "隔离", "再点一次确认隔离", host, delegate { host.PatchEntry(p.Name, it.Id, true); host.Refresh(); });
                act.Margin = new Thickness(6, 0, 0, 0);
                Grid.SetColumn(act, 3);
                row.Children.Add(act);
                card.Children.Add(row);
            }

            // DISABLED 里存在、但 bundle 清单里没有的 id（例如条目已被删除但补丁还在）—— 也要能恢复
            for (int k = 0; k < p.Disabled.Count; k++)
            {
                string id = p.Disabled[k];
                bool listed = false;
                for (int b = 0; b < p.Items.Count; b++) if (p.Items[b].Id == id) { listed = true; break; }
                if (listed) continue;
                if (q.Length > 0 && id.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                row.Children.Add(Chip("已隔离", Palette.Warn, Palette.WarnSoft));
                TextBlock idt = Mono(id, 12, Palette.TextFaint);
                idt.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(idt);
                string rkey = p.Name + "|" + id;
                row.Children.Add(ConfirmButton(rkey, "恢复", "再点一次确认恢复", host, delegate { host.PatchEntry(p.Name, id, false); host.Refresh(); }));
                card.Children.Add(row);
            }

            return Card(card, new Thickness(0), new Thickness(18, 16));
        }

        // ================================================================ 说明区

        private static Control Explain()
        {
            StackPanel s = new StackPanel { Spacing = 8 };
            Grid head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            SymbolIcon ic = Ic(Symbol.Info, 15, Palette.TextDim);
            ic.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(ic, 0);
            TextBlock t = T("这些数字怎么读？", 13.5, Palette.Text, FontWeight.SemiBold);
            t.Margin = new Thickness(8, 0, 0, 0);
            t.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(t, 1);
            head.Children.Add(ic); head.Children.Add(t);
            s.Children.Add(head);
            s.Children.Add(Line("• 缓存命中率 = 命中缓存的输入 token ÷ 全部输入 token。命中缓存的部分计费更低，所以这个数字越高越省钱；低于 70% 会标成琥珀/红色。"));
            s.Children.Add(Line("• 解码速度 = dsh 投影里的 decodeTokens ÷ decodeMs，即模型生成 token 的速率（tok/s）。它反映生成快慢，不含排队与工具耗时。"));
            s.Children.Add(Line("• 首 token = dsh 投影里的 ttftMs 原值（多步累计），超过 1 秒按秒显示；它是等待第一个字输出的累计时间。"));
            s.Children.Add(Line("• 上下文压力 = 已占用上下文 ÷ 模型窗口。越接近 100% 越可能触发压缩，80% 以上标红提醒。"));
            s.Children.Add(Line("• 输入 token 的条形是相对最长的那条会话画的，用来横向对比，不是绝对刻度。"));
            s.Children.Add(Line("• 显示 unknown 表示 dsh 投影里没有这个字段（例如空会话没有命中率）—— 我们不会用 0 冒充它。"));
            return Card(s, new Thickness(0), new Thickness(18, 16));
        }

        private static Control Line(string text)
        {
            return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Palette.TextDim, FontSize = 12 };
        }

        private static string PctText(double v)
        {
            return v < 0 ? "unknown" : v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }

        private static string TpsText(double v)
        {
            return v < 0 ? "unknown" : v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
