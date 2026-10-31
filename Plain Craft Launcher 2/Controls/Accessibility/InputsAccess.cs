using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;

// 输入类控件（文本框、下拉框、滑块）的无障碍实现。
// 名称优先使用 AutomationProperties.Name，否则自动查找旁边的标签文字。

namespace PCL;

public partial class MyTextBox
{
    protected override AutomationPeer OnCreateAutomationPeer() => new MyTextBoxAccessPeer(this);
}

public sealed class MyTextBoxAccessPeer(MyTextBox owner) : TextBoxAutomationPeer(owner)
{
    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrWhiteSpace(name) ? AccessHelper.TextBoxName(owner) ?? "" : name;
    }

    protected override string GetClassNameCore() => nameof(MyTextBox);
}

public partial class MyComboBox
{
    protected override AutomationPeer OnCreateAutomationPeer() => new MyComboBoxAccessPeer(this);
}

public sealed class MyComboBoxAccessPeer(MyComboBox owner) : ComboBoxAutomationPeer(owner)
{
    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrWhiteSpace(name) ? AccessHelper.ComboBoxName(owner) ?? "" : name;
    }

    protected override string GetClassNameCore() => nameof(MyComboBox);
}

public partial class MySlider
{
    static MySlider()
    {
        EventManager.RegisterClassHandler(typeof(MySlider), KeyDownEvent, new KeyEventHandler((sender, e) =>
        {
            if (e.Handled || Keyboard.Modifiers != ModifierKeys.None) return;
            var slider = (MySlider)sender;
            var step = (int)Math.Max(1, slider.ValueByKey);
            var largeStep = Math.Max(step, slider.MaxValue / 10);
            int? target = e.Key switch
            {
                Key.Left or Key.Down => slider.Value - step,
                Key.Right or Key.Up => slider.Value + step,
                Key.PageDown => slider.Value - largeStep,
                Key.PageUp => slider.Value + largeStep,
                Key.Home => 0,
                Key.End => slider.MaxValue,
                _ => null
            };
            if (target is null) return;
            e.Handled = true;
            slider.AccessSetValue(target.Value);
        }));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MySliderAccessPeer(this);

    /// <summary>当前值的显示文本，与拖动时弹出的提示一致。</summary>
    internal string AccessValueText
    {
        get
        {
            try
            {
                return getHintText?.DynamicInvoke(Value)?.ToString() ?? Value.ToString();
            }
            catch
            {
                return Value.ToString();
            }
        }
    }

    /// <summary>
    /// 用键盘或读屏修改值，行为与原有的左右方向键一致，并通知读屏新值。
    /// </summary>
    internal void AccessSetValue(int value)
    {
        if (ReferenceEquals(this, ModMain.dragControl)) return;
        var oldValue = Value;
        var oldText = AccessValueText;
        changeByKey = true;
        try
        {
            Value = value;
        }
        finally
        {
            changeByKey = false;
        }

        if (getHintText is not null)
        {
            RefreshPopup();
            ModAnimation.AniStop("MySlider KeyPopup " + Uuid);
            ModAnimation.AniStart(
                ModAnimation.AaCode(() => Popup.IsOpen = false, (int)Math.Round(700d * ModAnimation.aniSpeed)),
                "MySlider KeyPopup " + Uuid);
        }

        if (Value == oldValue) return;
        var peer = UIElementAutomationPeer.FromElement(this);
        if (peer is null) return;
        peer.RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, (double)oldValue, (double)Value);
        peer.RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, oldText, AccessValueText);
    }
}

public sealed class MySliderAccessPeer(MySlider owner)
    : FrameworkElementAutomationPeer(owner), IRangeValueProvider, IValueProvider
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

    protected override string GetClassNameCore() => nameof(MySlider);

    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrWhiteSpace(name) ? AccessHelper.FindLabel(owner) ?? "" : name;
    }

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    protected override List<AutomationPeer>? GetChildrenCore() => null;

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface is PatternInterface.RangeValue or PatternInterface.Value
            ? this
            : base.GetPattern(patternInterface);

    // IRangeValueProvider
    double IRangeValueProvider.Value => owner.Value;
    double IRangeValueProvider.Minimum => 0;
    double IRangeValueProvider.Maximum => owner.MaxValue;
    double IRangeValueProvider.SmallChange => Math.Max(1, owner.ValueByKey);
    double IRangeValueProvider.LargeChange => Math.Max(Math.Max(1, owner.ValueByKey), owner.MaxValue / 10);
    bool IRangeValueProvider.IsReadOnly => !owner.IsEnabled;

    void IRangeValueProvider.SetValue(double value)
    {
        if (!IsEnabled()) throw new ElementNotEnabledException();
        owner.AccessSetValue((int)Math.Round(value));
    }

    // IValueProvider：只读，用于让读屏朗读带单位的数值，例如「2.9 GiB」
    string IValueProvider.Value => owner.AccessValueText;
    bool IValueProvider.IsReadOnly => true;
    void IValueProvider.SetValue(string value) => throw new InvalidOperationException("请使用方向键调整");
}

public partial class MySearchBox
{
    static MySearchBox() => EventManager.RegisterClassHandler(typeof(MySearchBox), LoadedEvent,
        new RoutedEventHandler((sender, _) =>
        {
            // 清空按钮平时是隐藏的（透明且不响应鼠标），不应成为 Tab 停留点；清空可以用 Ctrl+A 再删除
            var box = (MySearchBox)sender;
            KeyboardNavigation.SetIsTabStop(box.BtnClear, false);
            AutomationProperties.SetName(box.BtnClear, "清空搜索");
        }));
}
