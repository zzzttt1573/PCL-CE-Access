using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

// 右键菜单的无障碍实现。
// 用代码打开的菜单（例如点击列表项的「设置」按钮）不会自动获得键盘焦点，读屏不知道菜单已打开，
// 方向键也还在操作后面的列表。打开时把焦点放到第一个可用的菜单项上；关闭后 WPF 会把焦点还给打开前的控件。

namespace PCL;

internal static class AccessMenu
{
    [ModuleInitializer]
    internal static void Register() =>
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                var menu = (ContextMenu)sender;
                // 隐藏的菜单项（例如第一个文件夹的「上移」）不计入「第几项 / 共几项」
                var visibleItems = menu.Items.OfType<MenuItem>()
                    .Where(item => item.Visibility == Visibility.Visible).ToList();
                for (var i = 0; i < visibleItems.Count; i++)
                {
                    System.Windows.Automation.AutomationProperties.SetPositionInSet(visibleItems[i], i + 1);
                    System.Windows.Automation.AutomationProperties.SetSizeOfSet(visibleItems[i], visibleItems.Count);
                }

                menu.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
                {
                    if (!menu.IsOpen || menu.IsKeyboardFocusWithin) return;
                    var first = menu.Items.OfType<MenuItem>()
                        .FirstOrDefault(item => item.Visibility == Visibility.Visible && item.IsEnabled);
                    if (first is not null)
                        first.Focus();
                    else
                        menu.Focus();
                });
            }));
}
