using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Threading;

namespace PCL;

/// <summary>
/// 无障碍朗读服务：NVDA 正在运行时直接调用 NVDA 控制器接口，否则通过 UIA 通知事件交给任意读屏软件。
/// </summary>
public static class AccessSpeech
{
    #region NVDA 控制器

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int NvdaNoArg();

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    private delegate int NvdaSpeak([MarshalAs(UnmanagedType.LPWStr)] string text);

    private static bool nvdaLoadTried;
    private static NvdaNoArg? nvdaTestIfRunning;
    private static NvdaNoArg? nvdaCancelSpeech;
    private static NvdaSpeak? nvdaSpeakText;

    private static void EnsureNvdaLoaded()
    {
        if (nvdaLoadTried) return;
        nvdaLoadTried = true;
        // NVDA 2024.1 起所有架构统一叫 nvdaControllerClient.dll，旧版 x64 叫 nvdaControllerClient64.dll
        foreach (var name in new[] { "nvdaControllerClient.dll", "nvdaControllerClient64.dll" })
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, name);
            if (!NativeLibrary.TryLoad(path, out var handle) && !NativeLibrary.TryLoad(name, out handle)) continue;
            try
            {
                nvdaTestIfRunning = Marshal.GetDelegateForFunctionPointer<NvdaNoArg>(
                    NativeLibrary.GetExport(handle, "nvdaController_testIfRunning"));
                nvdaCancelSpeech = Marshal.GetDelegateForFunctionPointer<NvdaNoArg>(
                    NativeLibrary.GetExport(handle, "nvdaController_cancelSpeech"));
                nvdaSpeakText = Marshal.GetDelegateForFunctionPointer<NvdaSpeak>(
                    NativeLibrary.GetExport(handle, "nvdaController_speakText"));
                ModBase.Log("[Access] 已加载 NVDA 控制器：" + name);
                return;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[Access] 加载 NVDA 控制器失败：" + name);
                nvdaTestIfRunning = null;
                nvdaCancelSpeech = null;
                nvdaSpeakText = null;
            }
        }
    }

    private static bool TrySpeakWithNvda(string text, bool interrupt)
    {
        EnsureNvdaLoaded();
        if (nvdaTestIfRunning is null || nvdaSpeakText is null) return false;
        try
        {
            if (nvdaTestIfRunning() != 0) return false;
            if (interrupt) nvdaCancelSpeech?.Invoke();
            return nvdaSpeakText(text) == 0;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Access] NVDA 朗读失败");
            return false;
        }
    }

    #endregion

    #region UIA 通知

    private static void SpeakWithUia(string text, bool interrupt)
    {
        var window = ModMain.frmMain;
        if (window is null) return;
        window.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            try
            {
                var source = (Keyboard.FocusedElement as UIElement) ?? window;
                var peer = UIElementAutomationPeer.FromElement(source)
                           ?? UIElementAutomationPeer.CreatePeerForElement(window);
                peer?.RaiseNotificationEvent(AutomationNotificationKind.Other,
                    interrupt
                        ? AutomationNotificationProcessing.ImportantMostRecent
                        : AutomationNotificationProcessing.All,
                    text, "PCLAccess");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[Access] UIA 通知失败");
            }
        });
    }

    #endregion

    /// <summary>
    /// 朗读一段文本。
    /// </summary>
    /// <param name="text">要朗读的内容。</param>
    /// <param name="interrupt">是否打断读屏正在朗读的内容。</param>
    public static void Speak(string? text, bool interrupt = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        text = text.Trim();
        if (TrySpeakWithNvda(text, interrupt)) return;
        SpeakWithUia(text, interrupt);
    }
}
