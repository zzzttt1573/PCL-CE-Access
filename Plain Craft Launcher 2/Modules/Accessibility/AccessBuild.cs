using PCL.Core.UI;

namespace PCL;

/// <summary>
/// 无障碍版（PCL CE Access）的构建级开关。
/// </summary>
public static class AccessBuild
{
    /// <summary>
    /// 上游的更新包不包含无障碍功能，安装后会覆盖本程序，因此禁止自我更新。
    /// </summary>
    /// <param name="notifyUser">是否提示用户（用户手动点击更新时为 true）。</param>
    /// <returns>始终返回 true，表示已拦截。</returns>
    public static bool BlockSelfUpdate(bool notifyUser)
    {
        ModBase.Log("[Access] 无障碍版已禁用自动更新，跳过上游更新");
        if (notifyUser)
            HintService.Hint("无障碍版不支持自动更新，请手动下载新的无障碍版本。", HintType.Info);
        return true;
    }
}
