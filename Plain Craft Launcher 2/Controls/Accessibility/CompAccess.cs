using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

// 社区资源（模组、整合包、资源包等）列表项的无障碍实现。
// 名称为资源标题，说明为简介和版本、下载量、更新时间、来源、标签等信息；回车或空格打开详情。

namespace PCL;

public partial class MyCompItem : IAccessibleControl
{
    static MyCompItem() => AccessHelper.RegisterControl(typeof(MyCompItem));

    protected override AutomationPeer OnCreateAutomationPeer() => new AccessPeer(this);

    AutomationControlType IAccessibleControl.AccessControlType => AutomationControlType.ListItem;

    string? IAccessibleControl.AccessName => AccessCompText.CleanTitle(Title, SubTitle);

    string? IAccessibleControl.AccessHelpText
    {
        get
        {
            var tags = PanTags.Visibility == Visibility.Visible
                ? string.Join("、", PanTags.Children.OfType<FrameworkElement>()
                    .Select(AccessCompText.TextOf).Where(t => !string.IsNullOrWhiteSpace(t)))
                : null;
            // 短的数据在前并带上说明，长简介放最后
            return AccessCompText.Join(
                AccessCompText.Labeled("支持", AccessCompText.Visible(LabVersion)),
                AccessCompText.Labeled("下载", AccessCompText.Visible(LabDownload)),
                AccessCompText.Labeled("更新于", AccessCompText.Visible(LabTime)),
                AccessCompText.Labeled("来源", AccessCompText.Visible(LabSource)),
                AccessCompText.Labeled("标签", tags),
                AccessCompText.Labeled("简介：", Description));
        }
    }

    // 内部的「下载」「收藏」按钮需要让读屏能找到
    bool IAccessibleControl.AccessExposeChildren => true;

    void IAccessibleControl.AccessInvoke()
    {
        if (!CanInteraction) return;
        isMouseDown = true;
        Button_MouseUp(this, AccessHelper.FakeLeftUp(this));
        isMouseDown = false;
    }
}

public partial class MyLocalCompItem : IAccessibleControl
{
    static MyLocalCompItem() => AccessHelper.RegisterControl(typeof(MyLocalCompItem));

    protected override AutomationPeer OnCreateAutomationPeer() => new AccessPeer(this);

    AutomationControlType IAccessibleControl.AccessControlType => AutomationControlType.ListItem;

    string? IAccessibleControl.AccessName => AccessCompText.CleanTitle(Title, SubTitle);

    string? IAccessibleControl.AccessHelpText => AccessCompText.Join(Checked ? "已选中" : null, Description);

    bool IAccessibleControl.AccessExposeChildren => true;

    void IAccessibleControl.AccessInvoke()
    {
        var e = AccessHelper.FakeLeftUp(this);
        isMouseDown = true;
        Button_MouseUp(this, e);
        Button_MouseLeave(this, e);
    }
}

internal static class AccessCompText
{
    public static string? Visible(TextBlock textBlock) =>
        textBlock.Visibility == Visibility.Visible ? textBlock.Text : null;

    public static string? TextOf(FrameworkElement element) => element switch
    {
        TextBlock textBlock => textBlock.Text,
        Border { Child: FrameworkElement child } => TextOf(child),
        Panel panel => string.Join(" ", panel.Children.OfType<FrameworkElement>().Select(TextOf)),
        ContentControl { Content: string s } => s,
        _ => null
    };

    /// <summary>
    /// 去掉标题里的「|」分隔符（例如「钠 | Sodium」），读屏会把它读成「竖线」。
    /// </summary>
    public static string CleanTitle(string? title, string? subTitle)
    {
        var text = string.IsNullOrWhiteSpace(subTitle) ? title ?? "" : $"{title} {subTitle}";
        text = text.Replace('|', ' ');
        return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
    }

    public static string? Labeled(string label, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : label.EndsWith('：') ? label + value.Trim() : $"{label} {value.Trim()}";

    public static string Join(params string?[] parts) =>
        string.Join("，", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
}
