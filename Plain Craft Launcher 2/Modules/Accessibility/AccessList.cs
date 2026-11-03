using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace PCL;

/// <summary>
/// 把一个容器变成「单个 Tab 停留点」的列表：
/// Tab 进入时落在当前选中项上，方向键 / Home / End 在列表内移动焦点（不切换页面），回车或空格才激活，再按 Tab 离开列表。
/// </summary>
public static class AccessList
{
    #region 附加属性

    /// <summary>容器是否为列表。</summary>
    public static readonly DependencyProperty IsListProperty = DependencyProperty.RegisterAttached(
        "IsList", typeof(bool), typeof(AccessList), new PropertyMetadata(false, OnIsListChanged));

    public static bool GetIsList(DependencyObject d) => (bool)d.GetValue(IsListProperty);
    public static void SetIsList(DependencyObject d, bool value) => d.SetValue(IsListProperty, value);

    /// <summary>是否横向列表（额外响应左右方向键）。</summary>
    public static readonly DependencyProperty IsHorizontalProperty = DependencyProperty.RegisterAttached(
        "IsHorizontal", typeof(bool), typeof(AccessList), new PropertyMetadata(false));

    public static bool GetIsHorizontal(DependencyObject d) => (bool)d.GetValue(IsHorizontalProperty);
    public static void SetIsHorizontal(DependencyObject d, bool value) => d.SetValue(IsHorizontalProperty, value);

    /// <summary>列表项暴露给读屏的控件类型，例如选项卡、列表项。</summary>
    public static readonly DependencyProperty ItemRoleProperty = DependencyProperty.RegisterAttached(
        "ItemRole", typeof(AutomationControlType?), typeof(AccessList), new PropertyMetadata(null));

    public static AutomationControlType? GetItemRole(DependencyObject d) =>
        (AutomationControlType?)d.GetValue(ItemRoleProperty);

    public static void SetItemRole(DependencyObject d, AutomationControlType? value) =>
        d.SetValue(ItemRoleProperty, value);

    private static void OnIsListChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement container) return;
        if ((bool)e.NewValue)
        {
            KeyboardNavigation.SetTabNavigation(container, KeyboardNavigationMode.Once);
            KeyboardNavigation.SetDirectionalNavigation(container, KeyboardNavigationMode.None);
            // 用预览事件：列表外层的滚动区域会先处理方向键并把焦点挪进列表项内部
            container.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnKeyDown));
            container.AddHandler(UIElement.PreviewGotKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler(OnPreviewGotFocus));
            container.AddHandler(UIElement.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnGotFocus));
        }
        else
        {
            container.ClearValue(KeyboardNavigation.TabNavigationProperty);
            container.ClearValue(KeyboardNavigation.DirectionalNavigationProperty);
            container.RemoveHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnKeyDown));
            container.RemoveHandler(UIElement.PreviewGotKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler(OnPreviewGotFocus));
            container.RemoveHandler(UIElement.GotKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler(OnGotFocus));
        }
    }

    /// <summary>列表的标签，例如「实例筛选：」。不设置时使用容器里第一个文字。读屏会在列表项后面作为说明朗读。</summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.RegisterAttached(
        "Label", typeof(string), typeof(AccessList), new PropertyMetadata(null));

    public static string? GetLabel(DependencyObject d) => (string?)d.GetValue(LabelProperty);
    public static void SetLabel(DependencyObject d, string? value) => d.SetValue(LabelProperty, value);

    #endregion

    #region 列表项

    /// <summary>
    /// 列表项所在列表的标签；不在列表中或列表没有标签时返回 null。
    /// </summary>
    public static string? GetLabelFor(UIElement item)
    {
        var container = FindContainer(VisualTreeHelper.GetParent(item));
        if (container is null) return null;
        if (GetLabel(container) is { } label) return label;
        if (container is not Panel panel) return null;
        var text = panel.Children.OfType<TextBlock>()
            .FirstOrDefault(t => t.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(t.Text))?.Text;
        return text?.Trim().TrimEnd('：', ':');
    }

    /// <summary>
    /// 按界面顺序取出容器内所有可获得焦点的列表项（不进入列表项内部）。
    /// </summary>
    public static List<UIElement> GetItems(DependencyObject container)
    {
        var result = new List<UIElement>();
        Collect(container, result);
        return result;
    }

    private static void Collect(DependencyObject parent, List<UIElement> result)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is UIElement { Visibility: not Visibility.Visible }) continue;
            if (child is UIElement { Focusable: true, IsEnabled: true, IsVisible: true } element &&
                KeyboardNavigation.GetIsTabStop(element))
            {
                result.Add(element);
                continue;
            }

            Collect(child, result);
        }
    }

    private static UIElement? FindContainer(DependencyObject? element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
            if (GetIsList(current))
                return current as UIElement;
        return null;
    }

    /// <summary>找到包含指定元素的列表项。</summary>
    private static UIElement? FindItem(List<UIElement> items, DependencyObject? element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement uiElement && items.Contains(uiElement))
                return uiElement;
        return null;
    }

    /// <summary>
    /// 把焦点放进一个区域：有选中项时放到选中项，否则按 Tab 顺序放到第一个控件。
    /// </summary>
    public static bool FocusEntry(UIElement region)
    {
        var items = GetItems(region);
        var selected = items.FirstOrDefault(IsSelected);
        if (selected is not null)
        {
            MoveFocus(selected);
            return true;
        }

        if (region.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)) &&
            Keyboard.FocusedElement is DependencyObject focused && region.IsAncestorOf(focused))
        {
            (focused as FrameworkElement)?.BringIntoView();
            return true;
        }

        var first = items.FirstOrDefault();
        if (first is null) return false;
        MoveFocus(first);
        return true;
    }

    /// <summary>
    /// 焦点是否在弹出层里（右键菜单、下拉框列表等）。弹出层不在主窗口的界面树里，不能当成焦点丢失。
    /// </summary>
    public static bool IsFocusInPopup()
    {
        if (Keyboard.FocusedElement is not Visual focused || ModMain.frmMain is not { } window) return false;
        var source = PresentationSource.FromVisual(focused);
        return source is not null && !ReferenceEquals(source, PresentationSource.FromVisual(window));
    }

    private static bool IsSelected(UIElement item) => item is IAccessibleControl { AccessIsSelected: true };

    /// <summary>
    /// 列表项在读屏中的控件类型；不在列表中时返回 null。
    /// </summary>
    public static AutomationControlType? GetRoleFor(UIElement item)
    {
        if (GetItemRole(item) is { } role) return role;
        // 只有可选中的项（导航项）才改变类型，列表区域里的普通按钮保持按钮
        if (item is not IAccessibleControl { AccessIsSelected: not null }) return null;
        var container = FindContainer(VisualTreeHelper.GetParent(item));
        return container is null ? null : GetItemRole(container);
    }

    #endregion

    #region 键盘与焦点

    private static void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || Keyboard.Modifiers != ModifierKeys.None) return;
        var container = (UIElement)sender;
        // 文本框、下拉框、滑块自己需要方向键
        if (e.OriginalSource is TextBoxBase or ComboBox or MySlider) return;
        // 四个方向键都要接管，否则 WPF 自带的方向导航会把焦点挪到别处而且回不来。
        // 横向列表：左 / 上 = 上一项，右 / 下 = 下一项。
        // 纵向列表：上下在列表项之间移动；左右在当前项自带的按钮（例如「刷新」「设置」）之间移动。
        var horizontal = GetIsHorizontal(container);
        var step = e.Key switch
        {
            Key.Up => -1,
            Key.Down => 1,
            Key.Left when horizontal => -1,
            Key.Right when horizontal => 1,
            Key.Home => int.MinValue,
            Key.End => int.MaxValue,
            _ => 0
        };

        var items = GetItems(container);
        if (items.Count == 0) return;
        var current = FindItem(items, e.OriginalSource as DependencyObject);

        if (step == 0)
        {
            if (horizontal || e.Key is not (Key.Left or Key.Right)) return;
            e.Handled = true;
            if (current is not null) MoveWithinItem(current, e.OriginalSource as DependencyObject, e.Key == Key.Right);
            return;
        }

        var index = current is null ? -1 : items.IndexOf(current);
        var target = step switch
        {
            int.MinValue => 0,
            int.MaxValue => items.Count - 1,
            _ => Math.Clamp(index + step, 0, items.Count - 1)
        };
        e.Handled = true;
        if (target == index) return; // 已到列表两端，不循环
        MoveFocus(items[target]);
    }

    private static void MoveFocus(UIElement item)
    {
        (item as FrameworkElement)?.BringIntoView();
        item.Focus();
    }

    /// <summary>
    /// 纵向列表里用左右方向键在列表项和它自带的按钮之间移动：
    /// 列表项 → 第一个按钮 → 第二个按钮……；左键反向，从第一个按钮回到列表项本身。
    /// </summary>
    private static void MoveWithinItem(UIElement item, DependencyObject? focused, bool forward)
    {
        // 按钮是鼠标悬停时才生成的，焦点进入列表项时已生成；这里只取已经存在的
        var buttons = new List<UIElement>();
        Collect(item, buttons);
        if (buttons.Count == 0) return;
        var position = -1;
        for (var current = focused; current is not null && !ReferenceEquals(current, item);
             current = VisualTreeHelper.GetParent(current))
            if (current is UIElement element && buttons.Contains(element))
            {
                position = buttons.IndexOf(element);
                break;
            }

        if (forward)
        {
            if (position < buttons.Count - 1) MoveFocus(buttons[position + 1]);
        }
        else if (position > 0)
            MoveFocus(buttons[position - 1]);
        else if (position == 0)
            MoveFocus(item);
    }

    /// <summary>
    /// 用 Tab 从列表外进入时，焦点落在当前选中项上，而不是第一项或上次停留的项。
    /// </summary>
    private static void OnPreviewGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var container = (UIElement)sender;
        if (e.OldFocus is DependencyObject old && FindContainer(old) == container) return;
        // 只在按 Tab 进入时跳到选中项；关闭菜单后焦点回到列表里的按钮等情况不处理
        if (!Keyboard.IsKeyDown(Key.Tab)) return;
        var items = GetItems(container);
        var selected = items.FirstOrDefault(IsSelected);
        if (selected is null || ReferenceEquals(selected, e.NewFocus)) return;
        e.Handled = true;
        container.Dispatcher.BeginInvoke(() => MoveFocus(selected));
    }

    /// <summary>
    /// 焦点落到某一项时，告诉读屏这是第几项、共几项。
    /// </summary>
    private static void OnGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var container = (UIElement)sender;
        var items = GetItems(container);
        var item = FindItem(items, e.NewFocus as DependencyObject);
        if (item is null) return;
        // 分类标题不计入「第几项 / 共几项」
        var realItems = items.Where(i => i is not TextBlock).ToList();
        for (var i = 0; i < realItems.Count; i++)
        {
            AutomationProperties.SetPositionInSet(realItems[i], i + 1);
            AutomationProperties.SetSizeOfSet(realItems[i], realItems.Count);
        }
    }

    #endregion
}

/// <summary>
/// 主窗口的键盘导航：Tab 只在「顶部导航、左侧、右侧」三个区域之间切换。
/// </summary>
public partial class FormMain
{
    static FormMain() => EventManager.RegisterClassHandler(typeof(FormMain), LoadedEvent,
        new RoutedEventHandler((sender, _) => ((FormMain)sender).SetupAccessNavigation()));

    private void SetupAccessNavigation()
    {
        try
        {
            // 顶部导航：启动、下载、设置、工具 合并为一个横向选项卡列表
            AccessList.SetIsList(PanTitleSelect, true);
            AccessList.SetIsHorizontal(PanTitleSelect, true);
            AccessList.SetItemRole(PanTitleSelect, System.Windows.Automation.Peers.AutomationControlType.TabItem);

            // 标题栏按钮不获得焦点（返回上一级可按 Esc）
            foreach (var button in new UIElement[] { BtnTitleClose, BtnTitleMin, BtnTitleHelp, BtnTitleInner })
                button.Focusable = false;

            // 界面里右侧页面写在左侧页面前面，默认 Tab 会先到右侧，这里调整为先左后右
            KeyboardNavigation.SetTabNavigation(PanMain, KeyboardNavigationMode.Local);
            foreach (var (border, index) in new[] { (PanMainLeft, 0), (PanMainRight, 1) })
            {
                var region = (UIElement)VisualTreeHelper.GetParent(border);
                KeyboardNavigation.SetTabNavigation(region, KeyboardNavigationMode.Local);
                KeyboardNavigation.SetTabIndex(region, index);
            }

            // 弹窗打开期间，Tab 只在弹窗内循环
            KeyboardNavigation.SetTabNavigation(PanMsg, KeyboardNavigationMode.Cycle);
            KeyboardNavigation.SetDirectionalNavigation(PanMsg, KeyboardNavigationMode.Cycle);

            // 标题栏不扫描，避免标题文字变成多余的 Tab 停留点
            AccessText.WatchWindow(this, PanMain, PanMsg);
            WatchFocusLoss();
            SetupAccessPaste();
            FocusLaunchButtonOnStartup();
            AccessBuild.ShowAboutHintOnStartup();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Access] 设置主窗口键盘导航失败");
        }
    }

    private UIElement? accessLastFocusRegion;
    private bool accessFocusCheckScheduled;

    /// <summary>
    /// 获得焦点的控件被移除或隐藏（例如关闭了一张卡片）时，焦点会落到窗口本身，读屏只能读出窗口标题。
    /// 记住焦点最后所在的区域（顶部导航、左侧、右侧），焦点丢失时放回同一区域：列表区域放到选中项，其他区域放到第一个控件。
    /// </summary>
    private void WatchFocusLoss()
    {
        var regions = new UIElement[]
        {
            PanTitleSelect,
            (UIElement)VisualTreeHelper.GetParent(PanMainLeft),
            (UIElement)VisualTreeHelper.GetParent(PanMainRight)
        };
        AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, e) =>
        {
            if (e.NewFocus is not DependencyObject focused) return;
            var region = regions.FirstOrDefault(r => r.IsAncestorOf(focused));
            if (region is not null) accessLastFocusRegion = region;
        }), true);

        LayoutUpdated += (_, _) =>
        {
            if (accessFocusCheckScheduled) return;
            accessFocusCheckScheduled = true;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () =>
            {
                accessFocusCheckScheduled = false;
                try
                {
                    RestoreLostFocus();
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "[Access] 恢复丢失的焦点失败");
                }
            });
        };
    }

    private void RestoreLostFocus()
    {
        if (!IsActive || accessLastFocusRegion is null) return;
        if (PanMsg.Children.Count > 0 || ModMain.WaitingMyMsgBox.Any() || AccessList.IsFocusInPopup()) return;
        if (Keyboard.FocusedElement is UIElement focused && !ReferenceEquals(focused, this) && focused.IsVisible &&
            IsAncestorOf(focused))
            return;
        // 回到启动页时，焦点优先放在启动按钮上
        if (ModMain.frmLaunchLeft is { IsVisible: true } launch && launch.BtnLaunch.IsEnabled &&
            accessLastFocusRegion.IsAncestorOf(launch))
        {
            launch.BtnLaunch.Focus();
            return;
        }

        AccessList.FocusEntry(accessLastFocusRegion);
    }

    /// <summary>
    /// 启动后把焦点放到启动按钮上。有弹窗时先不动，等所有弹窗关闭后再放；
    /// 启动按钮在实例加载完之前是禁用的，也要等它可用。用户已经自己移动了焦点、或离开了启动页，就不再处理。
    /// </summary>
    private void FocusLaunchButtonOnStartup()
    {
        var startTime = DateTime.Now;
        DateTime? noDialogSince = null;
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        timer.Tick += (_, _) =>
        {
            try
            {
                if (DateTime.Now - startTime > TimeSpan.FromSeconds(60) || pageCurrent != PageType.Launch)
                {
                    timer.Stop();
                    return;
                }

                // 弹窗中，焦点交给弹窗
                if (PanMsg.Children.Count > 0 || ModMain.WaitingMyMsgBox.Any() || AccessList.IsFocusInPopup())
                {
                    noDialogSince = null;
                    return;
                }

                noDialogSince ??= DateTime.Now;
                var button = ModMain.frmLaunchLeft?.BtnLaunch;
                // 焦点已经在启动按钮上：继续观察，防止随后弹出的弹窗关闭后焦点丢失
                if (button is not null && button.IsKeyboardFocused) return;
                // 用户已经把焦点放到别的控件上了
                if (Keyboard.FocusedElement is UIElement focused && !ReferenceEquals(focused, this) &&
                    focused.IsVisible && !PanMsg.IsAncestorOf(focused))
                {
                    timer.Stop();
                    return;
                }

                if (!IsActive) return;
                if (button is { IsVisible: true, IsEnabled: true })
                {
                    button.Focus();
                    return;
                }

                // 启动按钮不可用（例如还没有选择档案）：弹窗都关闭 2 秒后仍不可用，就把焦点放到启动页左侧第一个控件（档案区）
                if (DateTime.Now - noDialogSince < TimeSpan.FromSeconds(2) || ModMain.frmLaunchLeft is not { IsVisible: true } launch)
                    return;
                if (AccessList.FocusEntry(launch)) timer.Stop();
            }
            catch (Exception ex)
            {
                timer.Stop();
                ModBase.Log(ex, "[Access] 设置启动按钮焦点失败");
            }
        };
        timer.Start();
    }
}

/// <summary>
/// 左侧页面：含有导航列表项时，整个左侧区域作为一个列表。
/// </summary>
public partial class MyPageLeft
{
    public MyPageLeft()
    {
        Loaded += (_, _) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            if (HasNavigationItem(this))
            {
                AccessList.SetIsList(this, true);
                AccessList.SetItemRole(this, System.Windows.Automation.Peers.AutomationControlType.ListItem);
            }

            RestoreFocusIfLost();
        });
    }

    /// <summary>
    /// 切换大页面时，原来获得焦点的控件随旧页面一起消失，焦点会落到窗口本身。
    /// 这种情况下把焦点放到新左侧页面的当前选中项（没有选中项时放到第一个控件）。
    /// 旧页面的退出动画期间焦点控件可能还可见，所以多检查几次。
    /// </summary>
    private void RestoreFocusIfLost()
    {
        var ticks = 0;
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) =>
        {
            try
            {
                var window = ModMain.frmMain;
                if (++ticks > 8 || window is null || !IsLoaded || !window.IsActive)
                {
                    timer.Stop();
                    return;
                }

                // 有弹窗时焦点交给弹窗，弹窗关闭后再看
                if (window.PanMsg.Children.Count > 0 || ModMain.WaitingMyMsgBox.Any() || AccessList.IsFocusInPopup()) return;
                if (Keyboard.FocusedElement is UIElement focused && !ReferenceEquals(focused, window) &&
                    focused.IsVisible && window.IsAncestorOf(focused))
                {
                    timer.Stop();
                    return;
                }

                if (this is PageLaunchLeft launch && launch.BtnLaunch.IsEnabled)
                {
                    timer.Stop();
                    launch.BtnLaunch.Focus();
                    return;
                }

                if (AccessList.FocusEntry(this)) timer.Stop();
            }
            catch (Exception ex)
            {
                timer.Stop();
                ModBase.Log(ex, "[Access] 恢复焦点失败");
            }
        };
        timer.Start();
    }

    private static bool HasNavigationItem(DependencyObject parent)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is MyListItem { Type: MyListItem.CheckType.RadioBox }) return true;
            if (HasNavigationItem(child)) return true;
        }

        return false;
    }
}
