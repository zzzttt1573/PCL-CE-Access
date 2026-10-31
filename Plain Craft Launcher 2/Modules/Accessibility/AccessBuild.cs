using PCL.Core.UI;

namespace PCL;

/// <summary>
/// 无障碍版（PCL CE Access）的构建级开关。
/// </summary>
public static class AccessBuild
{
    /// <summary>
    /// 上游在启动页任意位置按回车都会启动游戏。无障碍版关闭此行为，只有焦点在启动按钮上时回车才启动。
    /// </summary>
    public const bool EnterLaunchesAnywhere = false;

    /// <summary>
    /// 隐藏设置页里的「软件更新」和「反馈」。
    /// </summary>
    public const bool HideUpdateAndFeedback = true;

    /// <summary>
    /// 无障碍版信息，替代原来的更新提示。
    /// </summary>
    public const string AboutHint = "无障碍版本作者：Dravokian，交流QQ群：704724210";

    /// <summary>
    /// 上游的更新包不包含无障碍功能，安装后会覆盖本程序，因此禁止自我更新。
    /// </summary>
    /// <param name="notifyUser">保留参数，与上游调用方式一致。</param>
    /// <returns>始终返回 true，表示已拦截。</returns>
    public static bool BlockSelfUpdate(bool notifyUser)
    {
        ModBase.Log("[Access] 无障碍版已禁用自动更新，跳过上游更新");
        return true;
    }

    /// <summary>
    /// 启动后显示一次无障碍版信息（右下角提示，读屏会朗读）。
    /// </summary>
    public static void ShowAboutHintOnStartup()
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            HintService.Hint(AboutHint, HintType.Info);
        };
        timer.Start();
    }
}
