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

    #endregion

    #region 列表项

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
        if (step == 0) return;

        var items = GetItems(container);
        if (items.Count == 0) return;
        var current = FindItem(items, e.OriginalSource as DependencyObject);
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
    /// 用 Tab 从列表外进入时，焦点落在当前选中项上，而不是第一项或上次停留的项。
    /// </summary>
    private static void OnPreviewGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var container = (UIElement)sender;
        if (e.OldFocus is DependencyObject old && FindContainer(old) == container) return;
        if (InputManager.Current.MostRecentInputDevice is not KeyboardDevice) return;
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
        for (var i = 0; i < items.Count; i++)
        {
            AutomationProperties.SetPositionInSet(items[i], i + 1);
            AutomationProperties.SetSizeOfSet(items[i], items.Count);
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

            FocusLaunchButtonOnStartup();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Access] 设置主窗口键盘导航失败");
        }
    }

    /// <summary>
    /// 启动后把焦点放到启动按钮上。有弹窗时先不动，等所有弹窗关闭后再放；
    /// 启动按钮在实例加载完之前是禁用的，也要等它可用。用户已经自己移动了焦点、或离开了启动页，就不再处理。
    /// </summary>
    private void FocusLaunchButtonOnStartup()
    {
        var startTime = DateTime.Now;
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
                if (PanMsg.Children.Count > 0 || ModMain.WaitingMyMsgBox.Any()) return;
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

                if (button is null || !button.IsVisible || !button.IsEnabled || !IsActive) return;
                button.Focus();
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
            var hasNavigation = HasNavigationItem(this);
            if (!hasNavigation) return;
            AccessList.SetIsList(this, true);
            AccessList.SetItemRole(this, System.Windows.Automation.Peers.AutomationControlType.ListItem);
        });
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
