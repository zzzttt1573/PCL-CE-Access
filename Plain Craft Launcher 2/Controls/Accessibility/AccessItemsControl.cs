using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace PCL;

/// <summary>
/// 只用来排列子控件的 ItemsControl。
/// 原生 ItemsControl 会给每个数据再包一层「数据项目」，名称是数据对象的类名（例如 PCL.PageLoginProfile+ProfileItem），
/// 而且自己也会成为一个空的焦点停留点。这里去掉这层包装，让读屏直接看到模板里的控件。
/// </summary>
public class AccessItemsControl : ItemsControl
{
    public AccessItemsControl()
    {
        Focusable = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LayoutOnlyPeer(this);

    private sealed class LayoutOnlyPeer(AccessItemsControl owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;

        protected override string GetClassNameCore() => nameof(AccessItemsControl);
    }
}
