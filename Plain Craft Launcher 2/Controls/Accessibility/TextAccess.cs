using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

// 静态文字的无障碍实现。
// 读屏在焦点模式下只读获得焦点的元素，纯文字默认无法用 Tab 到达，所以把「独立的说明文字」变成可获得焦点的文本。
// 以下文字不处理，避免重复朗读或制造多余的 Tab 停留：
//   控件内部的文字（按钮、列表项等已有名称）、控件旁边的标签（已作为控件名称）、卡片标题（已作为分组名称）、左侧导航的分类标题。

namespace PCL;

public static class AccessText
{
    private static readonly DependencyProperty EvaluatedProperty = DependencyProperty.RegisterAttached(
        "AccessTextEvaluated", typeof(bool), typeof(AccessText), new PropertyMetadata(false));

    private static readonly DependencyPropertyDescriptor TextDescriptor =
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));

    [ModuleInitializer]
    internal static void Register()
    {
        // TextBlock 自身通常收不到 Loaded（WPF 只向挂了实例处理器的子树广播），
        // 所以在卡片、提示条、页面、弹窗等容器加载时扫描它们内部的文字
        EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is MyCard or MyHint or MyPageRight or MyMsgText or MyMsgMarkdown or MyMsgInput
                    or MyMsgSelect or UserControl)
                    ScanLater((FrameworkElement)sender);
            }));
    }

    /// <summary>
    /// 稍后扫描容器内部的文字（等容器自己的 Loaded 处理完、内容填好之后）。
    /// </summary>
    public static void ScanLater(FrameworkElement root) =>
        root.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => Scan(root));

    private static bool windowScanScheduled;

    /// <summary>
    /// 有些内容是加载之后才显示或生成的（例如联机大厅的条款卡片），只在加载时扫描会漏掉。
    /// 主窗口布局变化时，节流地重新扫描整个窗口里新出现的文字（已处理过的文字会直接跳过）。
    /// </summary>
    public static void WatchWindow(Window window, params DependencyObject[] roots)
    {
        window.LayoutUpdated += (_, _) =>
        {
            if (windowScanScheduled) return;
            windowScanScheduled = true;
            var timer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                windowScanScheduled = false;
                try
                {
                    foreach (var root in roots) Scan(root);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "[Access] 扫描窗口文字失败");
                }
            };
            timer.Start();
        };
    }

    private static void Scan(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock textBlock) Evaluate(textBlock);
            // 有名称的控件内部不需要再扫描
            if (child is IAccessibleControl or ButtonBase or TextBoxBase or ComboBox or MySlider) continue;
            Scan(child);
        }
    }

    private static void Evaluate(TextBlock textBlock)
    {
        if ((bool)textBlock.GetValue(EvaluatedProperty)) return;
        try
        {
            if (!IsStandaloneText(textBlock)) return;
            textBlock.SetValue(EvaluatedProperty, true);
            RefreshFocusable(textBlock, EventArgs.Empty);
            // 文字之后可能被代码修改（例如状态提示），需要跟着更新；卸载时解除订阅，避免内存泄漏
            TextDescriptor.AddValueChanged(textBlock, RefreshFocusable);
            textBlock.Unloaded += OnTextBlockUnloaded;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Access] 判断文字是否可朗读时出错");
        }
    }

    private static void OnTextBlockUnloaded(object sender, RoutedEventArgs e)
    {
        var textBlock = (TextBlock)sender;
        textBlock.Unloaded -= OnTextBlockUnloaded;
        TextDescriptor.RemoveValueChanged(textBlock, RefreshFocusable);
        textBlock.SetValue(EvaluatedProperty, false);
    }

    private static void RefreshFocusable(object? sender, EventArgs e)
    {
        var textBlock = (TextBlock)sender!;
        textBlock.Focusable = HasWords(textBlock.Text);
    }

    private static bool HasWords(string? text) => !string.IsNullOrWhiteSpace(text) && text.Any(char.IsLetterOrDigit);

    /// <summary>
    /// 判断一个 TextBlock 是否是独立的说明文字。
    /// </summary>
    private static bool IsStandaloneText(TextBlock textBlock)
    {
        if (textBlock.TemplatedParent is not null) return false;
        // 明确设置了不作为 Tab 停留点的文字（例如已作为按钮说明的文字）
        if (!KeyboardNavigation.GetIsTabStop(textBlock)) return false;

        MyCard? card = null;
        DependencyObject? current = textBlock;
        while ((current = VisualTreeHelper.GetParent(current)) is not null)
        {
            switch (current)
            {
                // 已经有名称的控件，内部文字不再单独朗读
                case IAccessibleControl or ButtonBase or TextBoxBase or ComboBox or ComboBoxItem or MySlider:
                    return false;
                // 提示条的文字始终可读（右侧的「不再提示」按钮不代表它是标签）
                case MyHint:
                    return true;
                case MyCard found when card is null:
                    card = found;
                    break;
            }

            if (current is Window) break;
        }

        // 不在窗口里（例如弹出的提示框）
        if (current is null) return false;
        // 卡片标题已作为分组名称朗读
        if (card is not null && !string.IsNullOrWhiteSpace(card.Title) &&
            string.Equals(textBlock.Text?.Trim(), card.Title.Trim(), StringComparison.Ordinal))
            return false;

        return !IsNavigationHeading(textBlock) && !IsControlLabel(textBlock);
    }

    /// <summary>
    /// 左侧导航里的分类标题（例如「游戏」「启动器」）：与导航列表项排在同一个纵向列表里。
    /// </summary>
    private static bool IsNavigationHeading(TextBlock textBlock)
    {
        if (VisualTreeHelper.GetParent(textBlock) is not StackPanel { Orientation: Orientation.Vertical } stack ||
            !stack.Children.OfType<MyListItem>().Any())
            return false;
        // 只在左侧页面里算导航分类标题；右侧卡片里与列表项排在一起的说明文字要保留
        for (DependencyObject? current = stack; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is MyPageLeft)
                return true;
        return false;
    }

    /// <summary>
    /// 是否是某个控件的标签：同一行右侧有非文字元素，或者在横向排列中后面跟着控件。
    /// </summary>
    private static bool IsControlLabel(TextBlock textBlock)
    {
        UIElement unit = textBlock;
        if (VisualTreeHelper.GetParent(textBlock) is StackPanel { Orientation: Orientation.Horizontal } stack)
        {
            var index = stack.Children.IndexOf(textBlock);
            for (var i = index + 1; i < stack.Children.Count; i++)
                if (stack.Children[i] is not TextBlock && stack.Children[i].Visibility == Visibility.Visible &&
                    stack.Children[i] is not IAccessibleControl { AccessControlType: System.Windows.Automation.Peers.AutomationControlType.Button })
                    return true;
            unit = stack;
        }

        if (VisualTreeHelper.GetParent(unit) is not Grid grid) return false;
        var row = Grid.GetRow(unit);
        var column = Grid.GetColumn(unit);
        foreach (UIElement child in grid.Children)
        {
            if (ReferenceEquals(child, unit) || child is TextBlock || child.Visibility == Visibility.Collapsed) continue;
            if (Grid.GetRow(child) == row && Grid.GetColumn(child) > column) return true;
        }

        return false;
    }
}

/// <summary>
/// 弹窗出现时朗读标题和正文。焦点会先落在按钮上，读屏读完按钮后再读正文。
/// </summary>
public static class AccessDialog
{
    public static void AnnounceLater(FrameworkElement dialog, Func<string?> getTitle, Func<string?> getBody)
    {
        // 记住打开弹窗前的焦点，弹窗关闭后焦点如果丢了就放回去
        var previousFocus = Keyboard.FocusedElement as UIElement;
        if (previousFocus is not null && dialog.IsAncestorOf(previousFocus)) previousFocus = null;
        RoutedEventHandler? onUnloaded = null;
        onUnloaded = (_, _) =>
        {
            dialog.Unloaded -= onUnloaded;
            dialog.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                var window = ModMain.frmMain;
                if (window is null || window.PanMsg.Children.Count > 0) return; // 还有下一个弹窗
                if (Keyboard.FocusedElement is UIElement focused && !ReferenceEquals(focused, window) &&
                    focused.IsVisible) return;
                if (previousFocus is { IsVisible: true }) previousFocus.Focus();
            });
        };
        dialog.Unloaded += onUnloaded;

        // 弹窗打开时焦点必须在弹窗里，否则读屏用户不知道有弹窗，Tab 也会落到弹窗后面的页面上
        dialog.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!dialog.IsVisible || dialog.IsKeyboardFocusWithin) return;
            AccessList.GetItems(dialog).FirstOrDefault()?.Focus();
        });

        var timer = new DispatcherTimer(DispatcherPriority.Background, dialog.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(600)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!dialog.IsVisible) return;
            var title = getTitle()?.Trim();
            var body = getBody()?.Trim();
            AccessSpeech.Speak(string.IsNullOrEmpty(title) ? body : $"{title}。{body}");
        };
        timer.Start();
    }

    /// <summary>
    /// 把 Markdown 粗略转成适合朗读的纯文本。
    /// </summary>
    public static string? MarkdownToSpeech(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return markdown;
        var text = System.Text.RegularExpressions.Regex.Replace(markdown, @"!\[[^\]]*\]\([^)]*\)", "");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\[([^\]]*)\]\([^)]*\)", "$1");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"^\s{0,3}(#{1,6}|>|[-*+]\s)\s*", "",
            System.Text.RegularExpressions.RegexOptions.Multiline);
        return text.Replace("**", "").Replace("__", "").Replace("`", "");
    }
}

public partial class MyMsgText
{
    static MyMsgText() => EventManager.RegisterClassHandler(typeof(MyMsgText), LoadedEvent,
        new RoutedEventHandler((sender, _) =>
        {
            var box = (MyMsgText)sender;
            AccessDialog.AnnounceLater(box, () => box.LabTitle.Text, () => box.LabCaption.Text);
        }));
}

public partial class MyMsgMarkdown
{
    static MyMsgMarkdown() => EventManager.RegisterClassHandler(typeof(MyMsgMarkdown), LoadedEvent,
        new RoutedEventHandler((sender, _) =>
        {
            var box = (MyMsgMarkdown)sender;
            AccessDialog.AnnounceLater(box, () => box.LabTitle.Text,
                () => AccessDialog.MarkdownToSpeech(box.LabCaption.Markdown));
        }));
}

public partial class MyMsgInput
{
    static MyMsgInput() => EventManager.RegisterClassHandler(typeof(MyMsgInput), LoadedEvent,
        new RoutedEventHandler((sender, _) =>
        {
            var box = (MyMsgInput)sender;
            AccessDialog.AnnounceLater(box, () => box.LabTitle.Text, () => box.LabText.Text);
        }));
}

public partial class MyMsgSelect
{
    static MyMsgSelect() => EventManager.RegisterClassHandler(typeof(MyMsgSelect), LoadedEvent,
        new RoutedEventHandler((sender, _) =>
        {
            var box = (MyMsgSelect)sender;
            AccessDialog.AnnounceLater(box, () => box.LabTitle.Text, () => null);
        }));
}
