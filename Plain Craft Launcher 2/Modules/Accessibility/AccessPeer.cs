using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Threading;

namespace PCL;

/// <summary>
/// 自绘控件实现此接口后，即可通过 <see cref="AccessPeer"/> 暴露给读屏软件，并支持空格 / 回车激活。
/// </summary>
public interface IAccessibleControl
{
    /// <summary>UIA 控件类型，例如按钮、复选框。</summary>
    AutomationControlType AccessControlType { get; }

    /// <summary>读屏朗读的名称。AutomationProperties.Name 优先于此值。</summary>
    string? AccessName { get; }

    /// <summary>补充说明，读屏一般在名称之后朗读。</summary>
    string? AccessHelpText => null;

    /// <summary>复选状态；不是复选控件时返回 null。</summary>
    ToggleState? AccessToggleState => null;

    /// <summary>单选状态；不是单选控件时返回 null。</summary>
    bool? AccessIsSelected => null;

    /// <summary>是否把内部子元素也暴露给读屏。纯按钮返回 false，避免重复朗读内部文字。</summary>
    bool AccessExposeChildren => false;

    /// <summary>执行与鼠标左键单击相同的操作。</summary>
    void AccessInvoke();
}

/// <summary>
/// 通用的自绘控件 UIA 节点。
/// </summary>
public sealed class AccessPeer(FrameworkElement owner)
    : FrameworkElementAutomationPeer(owner), IInvokeProvider, IToggleProvider, ISelectionItemProvider
{
    private IAccessibleControl Control => (IAccessibleControl)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => Control.AccessControlType;

    protected override string GetClassNameCore() => Owner.GetType().Name;

    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrWhiteSpace(name) ? Control.AccessName?.Trim() ?? "" : name;
    }

    protected override string GetHelpTextCore()
    {
        var help = base.GetHelpTextCore();
        return string.IsNullOrWhiteSpace(help) ? Control.AccessHelpText?.Trim() ?? "" : help;
    }

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    protected override List<AutomationPeer>? GetChildrenCore() =>
        Control.AccessExposeChildren ? base.GetChildrenCore() : null;

    public override object? GetPattern(PatternInterface patternInterface)
    {
        switch (patternInterface)
        {
            case PatternInterface.Toggle when Control.AccessToggleState is not null:
            case PatternInterface.SelectionItem when Control.AccessIsSelected is not null:
            case PatternInterface.Invoke when Control.AccessToggleState is null && Control.AccessIsSelected is null:
                return this;
            default:
                return base.GetPattern(patternInterface);
        }
    }

    private void InvokeLater()
    {
        if (!IsEnabled()) throw new ElementNotEnabledException();
        // 异步执行，避免点击后弹出模态对话框时卡住读屏的 UIA 调用
        Owner.Dispatcher.BeginInvoke(DispatcherPriority.Input, () => AccessHelper.Activate(Control));
    }

    // IInvokeProvider
    void IInvokeProvider.Invoke() => InvokeLater();

    // IToggleProvider
    ToggleState IToggleProvider.ToggleState => Control.AccessToggleState ?? ToggleState.Off;
    void IToggleProvider.Toggle() => InvokeLater();

    // ISelectionItemProvider
    bool ISelectionItemProvider.IsSelected => Control.AccessIsSelected == true;
    IRawElementProviderSimple? ISelectionItemProvider.SelectionContainer => null;

    void ISelectionItemProvider.Select()
    {
        if (Control.AccessIsSelected != true) InvokeLater();
    }

    void ISelectionItemProvider.AddToSelection() => ((ISelectionItemProvider)this).Select();

    void ISelectionItemProvider.RemoveFromSelection() =>
        throw new InvalidOperationException("单选项不能取消选择");
}

/// <summary>
/// 自绘控件无障碍的公共逻辑。
/// </summary>
public static class AccessHelper
{
    /// <summary>
    /// 为控件类型注册键盘激活（空格 / 回车），并让它可以获得焦点。在控件的静态构造函数里调用。
    /// </summary>
    public static void RegisterControl(Type type, bool focusable = true)
    {
        EventManager.RegisterClassHandler(type, UIElement.KeyDownEvent, new KeyEventHandler(OnKeyDown));
        if (focusable)
            UIElement.FocusableProperty.OverrideMetadata(type, new FrameworkPropertyMetadata(true));
    }

    private static void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || sender is not IAccessibleControl control) return;
        // 只处理控件自身获得焦点时的按键，避免内部文本框里按回车误触发
        if (!ReferenceEquals(e.OriginalSource, sender)) return;
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        if (e.Key is not (Key.Space or Key.Enter)) return;
        e.Handled = true;
        Activate(control);
    }

    /// <summary>
    /// 激活控件，并把状态变化通知读屏。
    /// </summary>
    public static void Activate(IAccessibleControl control)
    {
        var element = (UIElement)control;
        if (!element.IsEnabled || !element.IsVisible) return;
        var oldToggle = control.AccessToggleState;
        var oldSelected = control.AccessIsSelected;
        try
        {
            control.AccessInvoke();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Access] 激活控件失败：" + control.AccessName);
            return;
        }

        var peer = UIElementAutomationPeer.FromElement(element);
        if (peer is null) return;
        var newToggle = control.AccessToggleState;
        if (oldToggle is not null && newToggle is not null && oldToggle != newToggle)
            peer.RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, oldToggle, newToggle);
        var newSelected = control.AccessIsSelected;
        if (oldSelected is not null && newSelected is not null && oldSelected != newSelected)
        {
            peer.RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, oldSelected,
                newSelected);
            if (newSelected == true)
                peer.RaiseAutomationEvent(AutomationEvents.SelectionItemPatternOnElementSelected);
        }
    }

    /// <summary>
    /// 构造一个模拟的鼠标左键抬起事件参数，用于复用控件原有的点击逻辑。
    /// </summary>
    public static MouseButtonEventArgs FakeLeftUp(UIElement source) =>
        new(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonUpEvent,
            Source = source
        };

    /// <summary>
    /// 把复选状态转换为 UIA 的 ToggleState。
    /// </summary>
    public static ToggleState ToToggleState(bool? value) => value switch
    {
        true => ToggleState.On,
        false => ToggleState.Off,
        null => ToggleState.Indeterminate
    };

    private static readonly Dictionary<string, string> iconNames = new()
    {
        ["refresh-cw"] = "刷新", ["info"] = "详情", ["x"] = "关闭", ["trash-2"] = "删除",
        ["rotate-ccw"] = "重置", ["save"] = "保存", ["folder-open"] = "打开文件夹", ["heart"] = "收藏",
        ["heart-filled"] = "取消收藏", ["download"] = "下载", ["copy"] = "复制", ["settings"] = "设置",
        ["pencil"] = "编辑", ["share-2"] = "分享", ["play"] = "启动", ["globe"] = "网页",
        ["earth"] = "网页", ["external-link"] = "在浏览器中打开", ["circle-minus"] = "移除",
        ["circle-plus"] = "添加", ["circle-check"] = "确认", ["upload"] = "上传", ["folder-plus"] = "新建文件夹",
        ["arrow-left"] = "上一页", ["arrow-right"] = "下一页", ["chevron-left"] = "上一页",
        ["chevron-right"] = "下一页", ["chevrons-left"] = "第一页", ["user-plus"] = "添加账号",
        ["log-out"] = "退出登录", ["minus"] = "最小化", ["circle-help"] = "帮助", ["list"] = "列表",
        ["arrow-up-down"] = "排序", ["power"] = "关闭", ["link-2"] = "链接", ["link-2-off"] = "取消链接",
        ["arrow-up-to-line"] = "回到顶部"
    };

    /// <summary>
    /// 根据 SVG 图标名猜测一个可读名称，仅在控件没有其他名称时使用。
    /// </summary>
    public static string? IconName(string? svgIcon)
    {
        if (string.IsNullOrEmpty(svgIcon)) return null;
        var key = svgIcon[(svgIcon.LastIndexOf('/') + 1)..];
        return iconNames.GetValueOrDefault(key);
    }

    /// <summary>
    /// 从 ToolTip 中取出文本。
    /// </summary>
    public static string? ToolTipText(object? toolTip) => toolTip switch
    {
        string s => s,
        System.Windows.Controls.ToolTip { Content: string s } => s,
        _ => null
    };
}
