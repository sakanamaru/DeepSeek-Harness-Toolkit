using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Dsht.Gui.Avalonia.Markers;
using Dsht.Gui.Avalonia.ViewModels;

namespace Dsht.Gui.Avalonia.Shells
{
    /// <summary>五种**布局框架**（外壳），同一个程序里实时切换，方便对比挑选。
    /// ⑤ 混合式 = ① 侧栏（主菜单）+ ② 顶部标签（子菜单）；子菜单不是摆设：会话页里它直接切换排序视角。
    /// 内容构建器（KPI / 会话卡片 / 工具栏 / 说明区）被所有外壳共享 —— 换外壳不动内容，换内容不动外壳。
    /// 设计方向参考了 March7thAssistant（GPL-3.0）的做法，**未复制其任何代码、图标、字体或图片资源**。</summary>
    public static class Shells
    {
        public const int Sidebar = 0;
        public const int TopTabs = 1;
        public const int CardGrid = 2;
        public const int MasterDetail = 3;
        public const int Hybrid = 4;

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

        // ---------------- ⑤ 混合式：侧栏主菜单 + 顶部子菜单 ----------------

        private static Control BuildHybrid(MainWindow host)
        {
            Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("216,*") };
            Border side = new Border
            {
                Background = Brushes.White,
                BorderBrush = Palette.Border,
                BorderThickness = new Thickness(0, 0, 1, 0),
                Child = new ScrollViewer { Content = MainMenu(host) }
            };
            Grid.SetColumn(side, 0);

            Grid right = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
            Control subs = SubTabs(host);
            Grid.SetRow(subs, 0);
            Control head = Header(host);
            Grid.SetRow(head, 1);
            Control body = host.IsSessionsSection ? (Control)new ScrollViewer { Content = ContentColumn(host) } : TextPane(host);
            Grid.SetRow(body, 2);
            Control foot = Footer(host);
            Grid.SetRow(foot, 3);
            right.Children.Add(subs); right.Children.Add(head); right.Children.Add(body); right.Children.Add(foot);
            Grid.SetColumn(right, 1);

            g.Children.Add(side); g.Children.Add(right);
            return g;
        }

        private static Control MainMenu(MainWindow host)
        {
            StackPanel s = new StackPanel { Margin = new Thickness(10, 14, 10, 14), Spacing = 2 };
            s.Children.Add(new TextBlock { Text = "主菜单", Foreground = Palette.TextFaint, FontSize = 11, Margin = new Thickness(10, 4, 0, 6) });
            for (int i = 0; i < MainWindow.NavItems.Length; i++)
            {
                int idx = i;
                bool active = host.MainSection == i;
                Button b = new Button
                {
                    Content = MainWindow.NavItems[i],
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = active ? Palette.AccentSoft : Brushes.Transparent,
                    Foreground = active ? Palette.Accent : Palette.Text,
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8),
                    FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal
                };
                b.Click += delegate { host.SetMainSection(idx); };
                s.Children.Add(b);
            }
            return s;
        }

        private static Control SubTabs(MainWindow host)
        {
            StackPanel s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(16, 10, 16, 0) };
            string[] subs = host.SubTabs;
            for (int i = 0; i < subs.Length; i++)
            {
                int idx = i;
                bool active = host.SubTab == i;
                StackPanel inner = new StackPanel { Spacing = 6 };
                inner.Children.Add(new TextBlock
                {
                    Text = subs[i],
                    FontSize = 13,
                    Foreground = active ? Palette.Accent : Palette.TextDim,
                    FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal
                });
                inner.Children.Add(new Border { Height = 2, Background = active ? Palette.Accent : Brushes.Transparent, CornerRadius = new CornerRadius(1) });
                Button b = new Button
                {
                    Content = inner,
                    Background = Brushes.White,
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(0),
                    Padding = new Thickness(10, 6, 10, 0)
                };
                b.Click += delegate { host.SetSubTab(idx); };
                s.Children.Add(b);
            }
            return new Border
            {
                Background = Brushes.White,
                BorderBrush = Palette.Border,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = s
            };
        }

        // ---------------- ① 侧栏式 ----------------

        private static Control BuildSidebar(MainWindow host)
        {
            Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("216,*") };
            Border side = new Border
            {
                Background = Brushes.White,
                BorderBrush = Palette.Border,
                BorderThickness = new Thickness(0, 0, 1, 0),
                Child = MainMenu(host)
            };
            Grid.SetColumn(side, 0);
            Grid body = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
            Control title = Header(host);
            Grid.SetRow(title, 0);
            Control content = host.IsSessionsSection ? (Control)new ScrollViewer { Content = ContentColumn(host) } : TextPane(host);
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
            Control tabs = SubTabs(host);
            Grid.SetRow(tabs, 0);
            Control title = Header(host);
            Grid.SetRow(title, 1);
            Control content = host.IsSessionsSection
                ? (Control)new ScrollViewer { Content = ContentColumn(host), Margin = new Thickness(20, 0, 20, 12) }
                : TextPane(host);
            Grid.SetRow(content, 2);
            g.Children.Add(tabs); g.Children.Add(title); g.Children.Add(content);
            return g;
        }

        // ---------------- ③ 卡片网格仪表盘 ----------------

        private static Control BuildCardGrid(MainWindow host)
        {
            Grid g = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
            Control nav = SubTabs(host);
            Grid.SetRow(nav, 0);
            StackPanel head = new StackPanel { Margin = new Thickness(20, 14, 20, 6), Spacing = 4 };
            head.Children.Add(new TextBlock { Text = host.PageTitle, FontSize = 24, FontWeight = FontWeight.Bold, Foreground = Palette.Text });
            head.Children.Add(new TextBlock { Text = host.SubtitleText, Foreground = Palette.TextDim, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            Grid.SetRow(head, 1);
            StackPanel body = new StackPanel { Margin = new Thickness(20, 0, 20, 12), Spacing = 14 };
            if (host.IsSessionsSection)
            {
                body.Children.Add(KpiStrip(host));
                body.Children.Add(SectionTitle("会话明细（卡片网格）"));
                body.Children.Add(CardGridBody(host));
            }
            else
            {
                body.Children.Add(TextPane(host));
            }
            body.Children.Add(Explain());
            Control scroll = new ScrollViewer { Content = body };
            Grid.SetRow(scroll, 2);
            g.Children.Add(nav); g.Children.Add(head); g.Children.Add(scroll);
            return g;
        }

        // ---------------- ④ 主从式 ----------------

        private static Control BuildMasterDetail(MainWindow host)
        {
            Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("300,*") };
            StackPanel left = new StackPanel { Margin = new Thickness(14), Spacing = 8 };
            left.Children.Add(SectionTitle("会话列表"));
            left.Children.Add(MiniFilter(host));
            left.Children.Add(host.IsSessionsSection ? MasterList(host) : TextPane(host));
            Border leftCard = new Border
            {
                Background = Brushes.White,
                BorderBrush = Palette.Border,
                BorderThickness = new Thickness(0, 0, 1, 0),
                Child = new ScrollViewer { Content = left }
            };
            Grid.SetColumn(leftCard, 0);

            StackPanel right = new StackPanel { Margin = new Thickness(20, 16, 20, 12), Spacing = 12 };
            right.Children.Add(new TextBlock { Text = host.PageTitle, FontSize = 24, FontWeight = FontWeight.Bold, Foreground = Palette.Text });
            right.Children.Add(host.IsSessionsSection ? DetailCard(host) : TextPane(host));
            right.Children.Add(Explain());
            Control rightScroll = new ScrollViewer { Content = right };
            Grid.SetColumn(rightScroll, 1);
            g.Children.Add(leftCard); g.Children.Add(rightScroll);
            return g;
        }

        // ---------------- 共享片段 ----------------

        private static Control Header(MainWindow host)
        {
            StackPanel s = new StackPanel { Margin = new Thickness(20, 16, 20, 8), Spacing = 4 };
            s.Children.Add(new TextBlock { Text = host.PageTitle, FontSize = 24, FontWeight = FontWeight.Bold, Foreground = Palette.Text });
            s.Children.Add(new TextBlock { Text = host.SubtitleText, Foreground = Palette.TextDim, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            return s;
        }

        private static Control Footer(MainWindow host)
        {
            return new Border
            {
                Background = Brushes.White,
                BorderBrush = Palette.Border,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(20, 6),
                Child = new TextBlock { Text = host.SourceText, Foreground = Palette.TextFaint, FontSize = 11, TextWrapping = TextWrapping.Wrap }
            };
        }

        private static Control TextPane(MainWindow host)
        {
            TextBox box = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                Text = host.RawOutput,
                FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(16, 10, 16, 10)
            };
            return new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                BorderBrush = Palette.Border,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(20, 0, 20, 12),
                Child = new ScrollViewer { Content = box }
            };
        }

        private static Control ContentColumn(MainWindow host)
        {
            StackPanel s = new StackPanel { Margin = new Thickness(20, 0, 20, 12), Spacing = 12 };
            s.Children.Add(KpiStrip(host));
            s.Children.Add(Toolbar(host));
            s.Children.Add(FocusLine(host));
            s.Children.Add(SessionCardList(host));
            s.Children.Add(Explain());
            return s;
        }

        private static Control FocusLine(MainWindow host)
        {
            return new TextBlock { Text = host.FocusText, Foreground = Palette.TextDim, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        }

        private static Control SectionTitle(string text)
        {
            return new TextBlock { Text = text, FontSize = 15, FontWeight = FontWeight.Bold, Foreground = Palette.Text, Margin = new Thickness(0, 2, 0, 2) };
        }

        private static Border Card(Control child, Thickness margin, Thickness padding)
        {
            return new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                BorderBrush = Palette.Border,
                BorderThickness = new Thickness(1),
                Padding = padding,
                Margin = margin,
                Child = child
            };
        }

        private static Control KpiStrip(MainWindow host)
        {
            SessionsSnapshot d = host.Data;
            Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
            g.Children.Add(KpiCard("会话总数", d == null ? "—" : d.Count.ToString(), "非空 " + (d == null ? "—" : d.NonBlank.ToString()) + "　运行中 " + (d == null ? "—" : d.Live.ToString()), Palette.Text, 0, -1));
            g.Children.Add(KpiCard("缓存命中率（越高越省钱）", PctText(d == null ? -1 : d.TotalHitPercent), "缓存读 " + (d == null ? "—" : SessionRow.Human(d.TotalCacheRead)), Palette.Good, 1, d == null ? -1 : d.TotalHitPercent));
            g.Children.Add(KpiCard("解码速度（生成 token 的速度）", TpsText(d == null ? -1 : d.TotalDecodeTps), "tok/s，按合计加权", Palette.Accent, 2, -1));
            g.Children.Add(KpiCard("累计 token", d == null ? "—" : SessionRow.Human(d.TotalIn), "输出 " + (d == null ? "—" : SessionRow.Human(d.TotalOut)) + "　输入含缓存读", Palette.Text, 3, -1));
            return g;
        }

        private static Control KpiCard(string title, string value, string sub, IBrush valueBrush, int col, double barPercent)
        {
            StackPanel s = new StackPanel { Spacing = 2 };
            s.Children.Add(new TextBlock { Text = title, Foreground = Palette.TextDim, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            s.Children.Add(new TextBlock { Text = value, FontSize = 26, FontWeight = FontWeight.Bold, Foreground = valueBrush });
            s.Children.Add(new TextBlock { Text = sub, Foreground = Palette.TextFaint, FontSize = 11, TextWrapping = TextWrapping.Wrap });
            if (barPercent >= 0)
            {
                double w = barPercent > 100 ? 100 : barPercent;
                int level = barPercent >= 90 ? 3 : (barPercent >= 70 ? 2 : 1);
                Border fill = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = Palette.HitBrush(level), HorizontalAlignment = HorizontalAlignment.Left, Width = w * 1.5 };
                s.Children.Add(new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = Palette.BarTrack, Margin = new Thickness(0, 6, 0, 0), Child = fill });
            }
            Border card = Card(s, new Thickness(0, 0, col == 3 ? 0 : 12, 0), new Thickness(14, 12));
            Grid.SetColumn(card, col);
            return card;
        }

        private static Control Toolbar(MainWindow host)
        {
            Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            StackPanel filters = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            filters.Children.Add(FilterButton("全部", 0, host));
            filters.Children.Add(FilterButton("非空", 1, host));
            filters.Children.Add(FilterButton("运行中", 2, host));
            Grid.SetColumn(filters, 0);
            StackPanel sorts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(14, 0, 0, 0) };
            sorts.Children.Add(new TextBlock { Text = "排序", Foreground = Palette.TextDim, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            ComboBox box = new ComboBox { MinWidth = 170, SelectedIndex = host.SortMode };
            box.Items.Add("最后活动（新→旧）");
            box.Items.Add("输入 token（多→少）");
            box.Items.Add("缓存命中率（低→高）");
            box.Items.Add("上下文压力（高→低）");
            box.Items.Add("解码速度（快→慢）");
            box.SelectionChanged += delegate { host.SortMode = box.SelectedIndex; host.Rerender(); };
            sorts.Children.Add(box);
            Grid.SetColumn(sorts, 1);
            Button refresh = new Button { Content = "⟳ 刷新" };
            refresh.Click += delegate { host.Refresh(); };
            Grid.SetColumn(refresh, 2);
            g.Children.Add(filters); g.Children.Add(sorts); g.Children.Add(refresh);
            return g;
        }

        private static Button FilterButton(string text, int mode, MainWindow host)
        {
            Button b = new Button { Content = text };
            b.Click += delegate { host.SetFilter(mode); };
            return b;
        }

        private static Control MiniFilter(MainWindow host)
        {
            StackPanel s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            s.Children.Add(FilterButton("全部", 0, host));
            s.Children.Add(FilterButton("非空", 1, host));
            s.Children.Add(FilterButton("运行中", 2, host));
            return s;
        }

        private static Control SessionCardList(MainWindow host)
        {
            ItemsControl list = new ItemsControl();
            list.ItemsSource = host.Rows;
            list.ItemTemplate = new FuncDataTemplate<SessionRowVm>(delegate(SessionRowVm vm, INameScope ns) { return SessionCard(vm); });
            return list;
        }

        private static Control CardGridBody(MainWindow host)
        {
            WrapPanel wrap = new WrapPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < host.Rows.Count; i++)
            {
                Border card = (Border)SessionCard(host.Rows[i]);
                card.Margin = new Thickness(0, 0, 12, 12);
                card.Width = 380;
                wrap.Children.Add(card);
            }
            return wrap;
        }

        private static Control SessionCard(SessionRowVm vm)
        {
            Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("90,*,150,150,120"), VerticalAlignment = VerticalAlignment.Center };
            StackPanel id = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            id.Children.Add(new TextBlock { Text = vm.ShortId, FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace"), Foreground = Palette.Text, FontSize = 12 });
            StackPanel st = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            st.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = vm.StatusBrush, VerticalAlignment = VerticalAlignment.Center });
            st.Children.Add(new TextBlock { Text = vm.StatusText, Foreground = Palette.TextDim, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
            id.Children.Add(st);
            Grid.SetColumn(id, 0);
            StackPanel mid = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
            mid.Children.Add(new TextBlock { Text = vm.MetaText, Foreground = Palette.TextDim, FontSize = 12 });
            mid.Children.Add(new TextBlock { Text = vm.DecodeLine, Foreground = Palette.TextFaint, FontSize = 11 });
            Grid.SetColumn(mid, 1);
            Control b1 = Bar("输入 token", vm.InText, vm.TokenBar, vm.TokenBrush);
            Control b2 = Bar("缓存命中", vm.HitText, vm.HitBar, vm.HitBrush);
            Control b3 = Bar("上下文压力", vm.CtxText, vm.CtxBar, vm.CtxBrush);
            Grid.SetColumn(b1, 2); Grid.SetColumn(b2, 3); Grid.SetColumn(b3, 4);
            g.Children.Add(id); g.Children.Add(mid); g.Children.Add(b1); g.Children.Add(b2); g.Children.Add(b3);
            return Card(g, new Thickness(0, 0, 0, 6), new Thickness(14, 10));
        }

        private static Control Bar(string label, string value, double percent, IBrush brush)
        {
            StackPanel s = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            s.Children.Add(new TextBlock { Text = label, Foreground = Palette.TextFaint, FontSize = 10 });
            s.Children.Add(new TextBlock { Text = value, Foreground = Palette.Text, FontSize = 12 });
            double w = percent < 0 ? 0 : (percent > 100 ? 100 : percent);
            Border fill = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = brush, HorizontalAlignment = HorizontalAlignment.Left, Width = w * 1.2 };
            s.Children.Add(new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = Palette.BarTrack, Child = fill });
            return s;
        }

        private static Control MasterList(MainWindow host)
        {
            ListBox box = new ListBox { ItemsSource = host.Rows, SelectedIndex = host.Rows.Count > 0 ? 0 : -1, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            box.ItemTemplate = new FuncDataTemplate<SessionRowVm>(delegate(SessionRowVm vm, INameScope ns)
            {
                StackPanel s = new StackPanel { Spacing = 2 };
                s.Children.Add(new TextBlock { Text = vm.ShortId, FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace"), FontSize = 12, Foreground = Palette.Text });
                s.Children.Add(new TextBlock { Text = vm.StatusText + "　" + vm.HitText + " 命中", FontSize = 11, Foreground = Palette.TextDim });
                return s;
            });
            box.SelectionChanged += delegate { host.ShowDetail(box.SelectedItem as SessionRowVm); };
            host.ShowDetail(host.Rows.Count > 0 ? host.Rows[0] : null);
            return box;
        }

        private static Control DetailCard(MainWindow host)
        {
            StackPanel s = new StackPanel { Spacing = 10 };
            host.DetailHost = s;
            s.Children.Add(new TextBlock { Text = "（左侧选一个会话）", Foreground = Palette.TextFaint, FontSize = 12 });
            return Card(s, new Thickness(0), new Thickness(16, 14));
        }

        public static void FillDetail(StackPanel host, SessionRowVm vm)
        {
            if (host == null) return;
            host.Children.Clear();
            if (vm == null)
            {
                host.Children.Add(new TextBlock { Text = "（左侧选一个会话）", Foreground = Palette.TextFaint, FontSize = 12 });
                return;
            }
            host.Children.Add(new TextBlock { Text = vm.ShortId, FontSize = 20, FontWeight = FontWeight.Bold, FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace"), Foreground = Palette.Text });
            host.Children.Add(new TextBlock { Text = vm.StatusText + "　" + vm.MetaText, Foreground = Palette.TextDim, FontSize = 12 });
            host.Children.Add(KpiRow("输入 token", vm.InText, vm.TokenBar, vm.TokenBrush));
            host.Children.Add(KpiRow("缓存命中率", vm.HitText, vm.HitBar, vm.HitBrush));
            host.Children.Add(KpiRow("上下文压力", vm.CtxText, vm.CtxBar, vm.CtxBrush));
            host.Children.Add(new TextBlock { Text = "解码速度 " + vm.DecodeText + "　首 token " + vm.TtftText, Foreground = Palette.TextDim, FontSize = 12 });
            host.Children.Add(new TextBlock { Text = "缓存读 " + vm.CacheReadText, Foreground = Palette.TextFaint, FontSize = 11 });
        }

        private static Control KpiRow(string label, string value, double percent, IBrush brush)
        {
            Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("120,120,*") };
            TextBlock l = new TextBlock { Text = label, Foreground = Palette.TextDim, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(l, 0);
            TextBlock v = new TextBlock { Text = value, Foreground = Palette.Text, FontSize = 14, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(v, 1);
            double w = percent < 0 ? 0 : (percent > 100 ? 100 : percent);
            Border fill = new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = brush, HorizontalAlignment = HorizontalAlignment.Left, Width = w * 2.2 };
            Border track = new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = Palette.BarTrack, Child = fill, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(track, 2);
            g.Children.Add(l); g.Children.Add(v); g.Children.Add(track);
            return g;
        }

        private static Control Explain()
        {
            StackPanel s = new StackPanel { Spacing = 6 };
            s.Children.Add(new TextBlock { Text = "这些数字怎么读？", FontSize = 14, FontWeight = FontWeight.Bold, Foreground = Palette.Text });
            s.Children.Add(Line("• 缓存命中率 = 命中缓存的输入 token ÷ 全部输入 token。命中缓存的部分计费更低，所以这个数字越高越省钱；低于 70% 会标成琥珀/红色。"));
            s.Children.Add(Line("• 解码速度 = dsh 投影里的 decodeTokens ÷ decodeMs，即模型生成 token 的速率（tok/s）。它反映生成快慢，不含排队与工具耗时。"));
            s.Children.Add(Line("• 首 token = dsh 投影里的 ttftMs 原值（多步累计），超过 1 秒按秒显示；它是等待第一个字输出的累计时间。"));
            s.Children.Add(Line("• 上下文压力 = 已占用上下文 ÷ 模型窗口。越接近 100% 越可能触发压缩，80% 以上标红提醒。"));
            s.Children.Add(Line("• 输入 token 的条形是相对最长的那条会话画的，用来横向对比，不是绝对刻度。"));
            s.Children.Add(Line("• 显示 unknown 表示 dsh 投影里没有这个字段（例如空会话没有命中率）—— 我们不会用 0 冒充它。"));
            return Card(s, new Thickness(0), new Thickness(16, 14));
        }

        private static Control Line(string text)
        {
            return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.Parse("#374151")), FontSize = 12 };
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
