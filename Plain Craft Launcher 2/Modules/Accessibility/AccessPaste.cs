using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using PCL.Core.UI;

namespace PCL;

/// <summary>
/// 用 Ctrl+V 代替拖拽：在主窗口里按 Ctrl+V，把剪贴板里的文件当作拖入窗口处理（安装整合包、模组等）。
/// 支持资源管理器里「复制」的文件，也支持「复制文件地址」得到的路径文本和 authlib-injector 链接。
/// 焦点在输入框里时不拦截，Ctrl+V 仍然是粘贴文字。
/// </summary>
public partial class FormMain
{
    private void SetupAccessPaste()
    {
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control) return;
            if (Keyboard.FocusedElement is TextBoxBase or PasswordBox) return;
            // 弹窗或菜单打开时不处理，避免在确认框上面再叠一个识别结果
            if (PanMsg.Children.Count > 0 || ModMain.WaitingMyMsgBox.Any() || AccessList.IsFocusInPopup()) return;
            e.Handled = true;
            PasteFilesFromClipboard();
        };
    }

    private void PasteFilesFromClipboard()
    {
        try
        {
            IDataObject? data = null;
            // 剪贴板可能被其他程序占用，打开失败时重试几次
            for (var i = 0; i < 5 && data is null; i++)
            {
                try { data = Clipboard.GetDataObject(); }
                catch (System.Runtime.InteropServices.COMException) { Thread.Sleep(50); }
            }

            if (data is null)
            {
                HintService.Hint("无法读取剪贴板，请稍后再试", HintType.Error);
                return;
            }

            // 资源管理器里复制的文件
            if (data.GetDataPresent(DataFormats.FileDrop))
            {
                if (data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
                {
                    ModBase.Log("[Access] Ctrl+V 粘贴文件：" + string.Join("; ", files));
                    AccessSpeech.Speak(files.Length == 1
                        ? "正在识别 " + Path.GetFileName(files[0])
                        : $"正在识别 {files.Length} 个文件");
                    FileDrag(files);
                    return;
                }
            }

            if (data.GetDataPresent(DataFormats.Text) && data.GetData(DataFormats.Text) is string text)
            {
                // 「复制文件地址」得到的是带引号的路径，多个文件时每行一个
                var paths = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim().Trim('"'))
                    .Where(line => line.Length > 0)
                    .ToList();
                if (paths.Count > 0 && paths.All(File.Exists))
                {
                    ModBase.Log("[Access] Ctrl+V 粘贴文件路径：" + string.Join("; ", paths));
                    AccessSpeech.Speak(paths.Count == 1
                        ? "正在识别 " + Path.GetFileName(paths[0])
                        : $"正在识别 {paths.Count} 个文件");
                    FileDrag(paths);
                    return;
                }

                // 其他文本（authlib-injector 链接、file:/// 地址）交给拖拽的处理逻辑
                if (HandleDropData(new DataObject(DataFormats.Text, text)) != DragDropEffects.None) return;
            }

            HintService.Hint("剪贴板里没有可识别的文件。请先在资源管理器里复制文件，再回到 PCL 按 Ctrl+V", HintType.Info);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Access] 从剪贴板粘贴文件失败", ModBase.LogLevel.Hint);
        }
    }
}
