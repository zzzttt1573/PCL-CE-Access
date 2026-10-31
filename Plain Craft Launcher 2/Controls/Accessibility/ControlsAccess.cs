using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Input;

// 各自绘控件的无障碍实现。全部放在 partial 文件中，尽量不改动上游源码。
// AccessInvoke 的写法都是：先把控件的「鼠标已按下」标记置为 true，再调用原有的鼠标抬起处理，
// 这样键盘和读屏激活走的是和鼠标单击完全相同的逻辑。

namespace PCL;

public partial class MyButton : IAccessibleControl
{
    static MyButton() => AccessHelper.RegisterControl(typeof(MyButton));

    protected override AutomationPeer OnCreateAutomationPeer() => new AccessPeer(this);

    AutomationControlType IAccessibleControl.AccessControlType => AutomationControlType.Button;

    string? IAccessibleControl.AccessName =>
        string.IsNullOrWhiteSpace(Text) ? new System.Windows.Documents.TextRange(
            LabText.ContentStart, LabText.ContentEnd).Text : Text;

    string? IAccessibleControl.AccessHelpText => AccessHelper.ToolTipText(ToolTip);

    void IAccessibleControl.AccessInvoke()
    {
        isMouseDown = true;
        Button_MouseUp(this, AccessHelper.FakeLeftUp(this));
        Button_MouseUp();
    }
}

public partial class MyIconButton : IAccessibleControl
{
    static MyIconButton() => AccessHelper.RegisterControl(typeof(MyIconButton));

    protected override AutomationPeer OnCreateAutomationPeer() => new AccessPeer(this);

    AutomationControlType IAccessibleControl.AccessControlType => AutomationControlType.Button;

    string? IAccessibleControl.AccessName =>
        AccessHelper.ToolTipText(ToolTip) ?? AccessHelper.IconName(SvgIcon) ?? Name;

    void IAccessibleControl.AccessInvoke()
    {
        isMouseDown = true;
        Button_MouseUp(this, AccessHelper.FakeLeftUp(this));
    }
}

public partial class MyIconTextButton : IAccessibleControl
{
    static MyIconTextButton() => AccessHelper.RegisterControl(typeof(MyIconTextButton));

    protected override AutomationPeer OnCreateAutomationPeer() => new AccessPeer(this);

    AutomationControlType IAccessibleControl.AccessControlType => AutomationControlType.Button;

    string? IAccessibleControl.AccessName => Text;

    string? IAccessibleControl.AccessHelpText => AccessHelper.ToolTipText(ToolTip);

    void IAccessibleControl.AccessInvoke()
    {
        isMouseDown = true;
        MyIconTextButton_MouseUp();
    }
}

public partial class MyTextButton : IAccessibleControl
{
    static MyTextButton() => AccessHelper.RegisterControl(typeof(MyTextButton));

    protected override AutomationPeer OnCreateAutomationPeer() => new AccessPeer(this);

    AutomationControlType IAccessibleControl.AccessControlType => AutomationControlType.Hyperlink;

    string? IAccessibleControl.AccessName => Text;

    string? IAccessibleControl.AccessHelpText => AccessHelper.ToolTipText(ToolTip);

    void IAccessibleControl.AccessInvoke()
    {
        isMouseDown = true;
        MyTextButton_MouseLeftButtonUp(this, AccessHelper.FakeLeftUp(this));
    }
}

public partial class MyExtraButton : IAccessibleControl
{
    static MyExtraButton() => AccessHelper.RegisterControl(typeof(MyExtraButton));

    protected override AutomationPeer OnCreateAutomationPeer() => new AccessPeer(this);

    AutomationControlType IAccessibleControl.AccessControlType => AutomationControlType.Button;

    string? IAccessibleControl.AccessName =>
        AccessHelper.ToolTipText(ToolTip) ?? AccessHelper.IconName(SvgIcon) ?? Name;

    void IAccessibleControl.AccessInvoke()
    {
        isLeftMouseHeld = true;
        Button_LeftMouseUp(PanClick, AccessHelper.FakeLeftUp(PanClick));
    }
}

public partial class MyExtraTextButton : IAccessibleControl
{
    static MyExtraTextButton() => AccessHelper.RegisterControl(typeof(MyExtraTextButton));

    protected override AutomationPeer OnCreateAutomationPeer() => new AccessPeer(this);

    AutomationControlType IAccessibleControl.AccessControlType => AutomationControlType.Button;

    string? IAccessibleControl.AccessName => Text;

    string? IAccessibleControl.AccessHelpText => AccessHelper.ToolTipText(ToolTip);

    void IAccessibleControl.AccessInvoke()
    {
        isLeftMouseHeld = true;
        Button_LeftMouseUp(PanClick, AccessHelper.FakeLeftUp(PanClick));
    }
}

public partial class MyCheckBox : IAccessibleControl
{
    static MyCheckBox() => AccessHelper.RegisterControl(typeof(MyCheckBox));

    protected override AutomationPeer OnCreateAutomationPeer() => new AccessPeer(this);

    AutomationControlType IAccessibleControl.AccessControlType => AutomationControlType.CheckBox;

    string? IAccessibleControl.AccessName => Text;

    string? IAccessibleControl.AccessHelpText => AccessHelper.ToolTipText(ToolTip);

    ToggleState? IAccessibleControl.AccessToggleState => AccessHelper.ToToggleState(Checked);

    void IAccessibleControl.AccessInvoke()
    {
        if (!allowMouseDown) return;
        mouseDowned = true;
        Checkbox_MouseUp();
    }
}

public partial class MyRadioBox : IAccessibleControl
{
    static MyRadioBox() => AccessHelper.RegisterControl(typeof(MyRadioBox));

    protected override AutomationPeer OnCreateAutomationPeer() => new AccessPeer(this);

    AutomationControlType IAccessibleControl.AccessControlType => AutomationControlType.RadioButton;

    string? IAccessibleControl.AccessName => Text;

    string? IAccessibleControl.AccessHelpText => AccessHelper.ToolTipText(ToolTip);

    bool? IAccessibleControl.AccessIsSelected => Checked;

    void IAccessibleControl.AccessInvoke()
    {
        if (!allowMouseDown) return;
        mouseDowned = true;
        Radiobox_MouseUp();
    }
}

public partial class MyRadioButton : IAccessibleControl
{
    static MyRadioButton() => AccessHelper.RegisterControl(typeof(MyRadioButton));

    protected override AutomationPeer OnCreateAutomationPeer() => new AccessPeer(this);

    AutomationControlType IAccessibleControl.AccessControlType => AutomationControlType.RadioButton;

    string? IAccessibleControl.AccessName => Text;

    string? IAccessibleControl.AccessHelpText => AccessHelper.ToolTipText(ToolTip);

    bool? IAccessibleControl.AccessIsSelected => Checked;

    void IAccessibleControl.AccessInvoke()
    {
        if (Checked) return;
        isMouseDown = true;
        Radiobox_MouseUp();
    }
}

public partial class MyListItem : IAccessibleControl
{
    static MyListItem()
    {
        // 列表项是否可获得焦点取决于 Type，在 Loaded 时设置
        AccessHelper.RegisterControl(typeof(MyListItem), focusable: false);
        EventManager.RegisterClassHandler(typeof(MyListItem), LoadedEvent, new RoutedEventHandler((sender, _) =>
        {
            var item = (MyListItem)sender;
            item.Focusable = item.Type != CheckType.None;
        }));
        // 列表项内部的操作按钮（例如档案的「更改 UUID」）是鼠标第一次悬停时才生成的，
        // 键盘用户不会悬停，所以获得焦点时也要生成
        EventManager.RegisterClassHandler(typeof(MyListItem), GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler((sender, e) =>
            {
                var item = (MyListItem)sender;
                if (item.ContentHandler is null) return;
                item.ContentHandler.Invoke(item, e);
                item.ContentHandler = null;
            }));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AccessPeer(this);

    AutomationControlType IAccessibleControl.AccessControlType => Type switch
    {
        CheckType.RadioBox => AutomationControlType.RadioButton,
        CheckType.CheckBox => AutomationControlType.CheckBox,
        CheckType.Clickable => AutomationControlType.Button,
        _ => AutomationControlType.ListItem
    };

    string? IAccessibleControl.AccessName => Title;

    string? IAccessibleControl.AccessHelpText => Info;

    ToggleState? IAccessibleControl.AccessToggleState =>
        Type == CheckType.CheckBox ? AccessHelper.ToToggleState(Checked) : null;

    bool? IAccessibleControl.AccessIsSelected => Type == CheckType.RadioBox ? Checked : null;

    // 列表项内部可能有悬停才显示的操作按钮，需要让读屏能找到
    bool IAccessibleControl.AccessExposeChildren => buttonStack is not null;

    void IAccessibleControl.AccessInvoke()
    {
        if (Type == CheckType.None) return;
        var e = AccessHelper.FakeLeftUp(this);
        isMouseDown = true;
        Button_MouseUp(this, e);
        Button_MouseLeave(this, e);
    }
}
