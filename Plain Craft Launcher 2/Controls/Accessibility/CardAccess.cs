using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;

// 卡片的无障碍实现：有标题的卡片暴露为分组，读屏进入卡片时会朗读标题，
// 用来区分不同卡片里同名的设置项；可折叠的卡片可以获得焦点，用空格 / 回车展开或折叠。

namespace PCL;

public partial class MyCard
{
    static MyCard()
    {
        EventManager.RegisterClassHandler(typeof(MyCard), LoadedEvent, new RoutedEventHandler((sender, _) =>
        {
            var card = (MyCard)sender;
            if (card is MySearchBox) return;
            // 卡片自己的 Loaded 处理里才会确定 SwapControl，类处理器比它先执行，所以延后判断
            card.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                () => card.Focusable = card.AccessCanSwap);
        }));
        EventManager.RegisterClassHandler(typeof(MyCard), KeyDownEvent, new KeyEventHandler((sender, e) =>
        {
            if (e.Handled || !ReferenceEquals(e.OriginalSource, sender)) return;
            if (Keyboard.Modifiers != ModifierKeys.None) return;
            var card = (MyCard)sender;
            if (!card.AccessCanSwap) return;
            switch (e.Key)
            {
                case Key.Space or Key.Enter:
                    card.AccessSetSwapped(!card.IsSwapped);
                    break;
                case Key.Right when card.IsSwapped:
                    card.AccessSetSwapped(false);
                    break;
                case Key.Left when !card.IsSwapped:
                    card.AccessSetSwapped(true);
                    break;
                default:
                    return;
            }

            e.Handled = true;
        }));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MyCardAccessPeer(this);

    internal bool AccessCanSwap => SwapControl is not null;

    /// <summary>
    /// 展开或折叠卡片，流程与鼠标点击标题栏一致（会触发 PreviewSwap 和 Swap 事件）。
    /// </summary>
    internal void AccessSetSwapped(bool swapped)
    {
        if (!AccessCanSwap || IsSwapped == swapped) return;
        var e = new ModBase.RouteEventArgs(true);
        PreviewSwap?.Invoke(this, e);
        if (e.handled) return;
        IsSwapped = swapped;
        ModBase.Log("[Control] " + (IsSwapped ? "折叠卡片" : "展开卡片") + (Title is null ? "" : "：" + Title));
        Swap?.Invoke(this, e);
        // 展开时可能动态生成内容，重新扫描里面的文字
        if (!swapped) AccessText.ScanLater(this);

        var peer = UIElementAutomationPeer.FromElement(this);
        peer?.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
            swapped ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
            swapped ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded);
    }
}

public sealed class MyCardAccessPeer(MyCard owner) : FrameworkElementAutomationPeer(owner), IExpandCollapseProvider
{
    private bool HasTitle => !string.IsNullOrWhiteSpace(owner.Title);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetClassNameCore() => owner.GetType().Name;

    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrWhiteSpace(name) ? owner.Title?.Trim() ?? "" : name;
    }

    // 没有标题、也不能折叠的卡片只是装饰，不出现在读屏的控件视图里
    protected override bool IsControlElementCore() => HasTitle || owner.AccessCanSwap;

    protected override bool IsContentElementCore() => HasTitle || owner.AccessCanSwap;

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.ExpandCollapse && owner.AccessCanSwap
            ? this
            : base.GetPattern(patternInterface);

    ExpandCollapseState IExpandCollapseProvider.ExpandCollapseState =>
        owner.IsSwapped ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded;

    void IExpandCollapseProvider.Expand() =>
        owner.Dispatcher.BeginInvoke(() => owner.AccessSetSwapped(false));

    void IExpandCollapseProvider.Collapse() =>
        owner.Dispatcher.BeginInvoke(() => owner.AccessSetSwapped(true));
}
