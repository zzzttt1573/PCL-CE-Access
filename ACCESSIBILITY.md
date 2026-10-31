# PCL CE Access 无障碍版开发说明

本文档记录 PCL CE 无障碍版（PCL CE Access）做过的全部修改、设计决定和测试方法。
用途：上游 PCL CE 发布新版本后重新合并，或者交接给其他开发者 / AI 继续开发。

---

## 1. 项目概况

| 项目 | 内容 |
| --- | --- |
| 上游仓库 | https://github.com/PCL-Community/PCL-CE |
| 基于的上游版本 | **v2.15.0**（tag `v2.15.0`，提交 `12bb0ea2`）。刻意没有用 v2.15.1 |
| 本地仓库 | `E:\vibe coding\projects\PCL-CE-Access` |
| 开发分支 | `access`（所有无障碍修改都在这个分支上） |
| 远程 | `origin` 指向上游 PCL-Community/PCL-CE；作者账号复刻上游时 GitHub 返回 403，暂未推送到个人仓库 |
| 许可证 | Apache 2.0（发布时保留上游版权声明，并注明修改） |
| 目标读屏软件 | 主要测试 **NVDA**；同时兼容任何支持 UIA 的读屏（讲述人、争渡等） |
| 无障碍版作者 | Dravokian，交流 QQ 群：704724210 |

### 编译

- 需要 **.NET 10 SDK**（目前用的是 10.0.403）。
- 命令（在仓库根目录）：

```bash
dotnet build "Plain Craft Launcher 2/Plain Craft Launcher 2.csproj" -c Debug -p:Platform=x64
```

- 输出：`Plain Craft Launcher 2\bin\Debug\net10.0-windows\win-x64\Plain Craft Launcher 2.exe`
- Debug 版每次启动都会弹出「特殊版本提示」对话框，这是上游行为，正好可以用来测试弹窗朗读。正式发布请用 Release 编译。
- PCL **只允许运行一个实例**：编译前必须关闭正在运行的 PCL，否则复制文件失败（MSB3021/MSB3027），新开的实例也会自动退出。

---

## 2. 总体思路（设计决定）

这些决定都和使用者确认过，后续开发请保持一致。

1. **直接改源码**，不做运行时注入。为了方便合并上游，新代码尽量放在 `Accessibility` 文件夹的 **partial 类文件**里，上游文件只做最小改动（多数只是加一个 `partial` 关键字）。
2. **朗读方式**：控件可访问性一律走标准 **UIA**（AutomationPeer）；主动播报优先直接调 **NVDA 控制器接口**，NVDA 没运行时退回 **UIA 通知事件**。
3. **键盘导航按区域划分**：Tab 只在「顶部导航 → 左侧 → 右侧」三个区域之间跳转。
   - 顶部导航（启动 / 下载 / 设置 / 工具）合并成一个横向列表。
   - 含有导航项的左侧页面合并成一个纵向列表。
   - **右侧区域保持原样**，每个控件一个 Tab 停留点（使用者明确要求，不要把右侧的列表合并）。
4. **列表里方向键只移动焦点，回车 / 空格才切换页面**（不做「方向键即选中」）。
5. **纵向列表**：上下键在列表项之间移动；左右键在当前项自带的小按钮（刷新、设置等）之间移动，从第一个按钮按左键回到列表项。**横向列表**：左右 / 上下都是上一项 / 下一项。列表两端不循环。
6. **标题栏的关闭、最小化、帮助、返回按钮不获得焦点**；返回上一级用 Esc（上游已有）。
7. **只有焦点在启动按钮上时回车才启动游戏**；启动后默认焦点放在启动按钮上，有弹窗时等弹窗关闭。
8. **禁用自动更新**：上游更新包不含无障碍功能，安装后会覆盖本程序（曾经实际发生过）。
9. **隐藏设置页的「软件更新」「反馈」**；启动时显示「无障碍版本作者：Dravokian，交流QQ群：704724210」。
10. **不移除「需要先进行一次正版验证才能使用其他验证方式」的限制**（这是正版验证门槛，不属于无障碍范围）。

---

## 3. 新增文件一览

所有新增代码都在下面两个文件夹里，合并上游时一般不会冲突。

### `Plain Craft Launcher 2/Modules/Accessibility/`

| 文件 | 作用 |
| --- | --- |
| `AccessSpeech.cs` | 朗读服务。`AccessSpeech.Speak(text, interrupt)`：NVDA 运行时用 `nvdaControllerClient.dll` / `nvdaControllerClient64.dll`（先找程序目录，再找系统路径；NVDA 安装后系统里通常就有），否则发 UIA 通知事件。 |
| `AccessPeer.cs` | `IAccessibleControl` 接口 + 通用节点 `AccessPeer`（支持 Invoke / Toggle / SelectionItem 模式）+ `AccessHelper`（空格回车激活、模拟鼠标抬起、标签查找 `FindLabel`、文本框 / 下拉框命名、图标名映射）。 |
| `AccessList.cs` | 列表导航（附加属性 `AccessList.IsList / IsHorizontal / ItemRole / Label`）；主窗口区域导航、标题栏按钮去焦点、焦点丢失恢复、启动按钮默认焦点（`FormMain` partial）；左侧页面自动变列表、切页后焦点恢复（`MyPageLeft` partial）。 |
| `AccessBuild.cs` | 无障碍版的构建开关和文字：`EnterLaunchesAnywhere`、`HideUpdateAndFeedback`、`AboutHint`、`BlockSelfUpdate()`、`ShowAboutHintOnStartup()`。 |

### `Plain Craft Launcher 2/Controls/Accessibility/`

| 文件 | 作用 |
| --- | --- |
| `ControlsAccess.cs` | 按钮类控件：MyButton、MyIconButton、MyIconTextButton、MyTextButton、MyExtraButton、MyExtraTextButton、MyCheckBox、MyRadioBox、MyRadioButton、MyListItem。名称、状态、空格 / 回车激活；MyListItem 的说明 = Info + 标签（如「26.2」「Fabric 0.19.3」），获得焦点时生成懒加载的操作按钮。 |
| `InputsAccess.cs` | MyTextBox、MyComboBox（自动找标签作名称）；MySlider（RangeValue + Value 模式，朗读带单位的值，方向键 / PageUp / PageDown / Home / End）；MySearchBox 的隐藏清空按钮不作 Tab 停留点。 |
| `CardAccess.cs` | MyCard 暴露为带标题的分组；可折叠卡片可获得焦点，空格 / 回车 / 左右键展开折叠。 |
| `TextAccess.cs` | 独立说明文字变成可 Tab 到达的文本；左侧导航分类标题作为二级标题；弹窗打开时焦点进入弹窗并朗读标题和正文，关闭后焦点回到原控件。 |
| `CompAccess.cs` | 社区资源项 MyCompItem（模组、整合包等）和本地模组项 MyLocalCompItem。朗读格式：`名称，支持 …，下载 …，更新于 …，来源 …，标签 …，简介：…`；获得键盘焦点时显示按钮区。 |
| `MenuAccess.cs` | 右键菜单打开时焦点进入第一个可见菜单项，编号只计可见项。 |
| `AccessItemsControl.cs` | 只负责排列的 ItemsControl，去掉原生 ItemsControl 多包的「数据项目」层（否则会朗读类名，如 `PCL.PageLoginProfile+ProfileItem`）。 |

### 其他

| 路径 | 作用 |
| --- | --- |
| `AccessTools/*.ps1` | UIA 测试脚本，见第 7 节。 |
| `ACCESSIBILITY.md` | 本文档。 |

---

## 4. 对上游文件的修改（合并上游后需要逐条检查）

> 合并新版上游后，**先确认下面每一处还在、还生效**。上游重构了某个文件时，按「目的」重新实现。

### 4.1 只加了 `partial` 关键字

| 文件 | 修改 | 目的 |
| --- | --- | --- |
| `Controls/MyTextButton.cs` | `public class` → `public partial class` | 让无障碍代码写在单独文件里 |
| `Controls/MyTextBox.cs` | 同上 | 同上 |
| `Controls/MyComboBox.cs` | 同上 | 同上 |
| `Controls/MyCard.cs` | 同上 | 同上 |
| `Controls/MyPageLeft.cs` | 同上 | 同上（`AccessList.cs` 里给它加了构造函数，所以上游**不能**再有无参构造函数，否则冲突） |

其余控件（MyButton、MyCheckBox、MyListItem、MyMsgText 等）本来就是 XAML 生成的 partial 类，不需要改。

### 4.2 行为修改

| 文件 | 修改 | 目的 |
| --- | --- | --- |
| `Modules/Updates/UpdateManager.cs` | `UpdateStart` 和 `UpdateRestart` 开头各加一行 `if (AccessBuild.BlockSelfUpdate(...)) return;` | 禁用自动更新，防止上游更新包覆盖无障碍版 |
| `FormMain.xaml.cs`（`FormMain_KeyDown` 里「调用启动游戏」） | 条件前加 `AccessBuild.EnterLaunchesAnywhere &&` | 关闭「启动页任意位置回车启动游戏」 |
| `Modules/UI/HintService.cs`（`Hint` 方法） | 加 `AccessSpeech.Speak(normalized);` | 右下角提示自动朗读 |
| `Controls/MyScrollViewer.cs`（`MyScrollViewer_PreviewGotKeyboardFocus`） | 条件加 `&& InputManager.Current.MostRecentInputDevice is MouseDevice` | 上游（#3854）禁止滑块获得焦点，导致 Tab 卡在滑块前；改为只在鼠标点击时禁止 |
| `Pages/PageSetup/PageSetupUI.xaml.cs`（功能隐藏刷新） | `ItemUpdate`、`ItemFeedback` 的可见性加 `AccessBuild.HideUpdateAndFeedback ||` | 始终隐藏「软件更新」「反馈」 |

### 4.3 XAML 修改

| 文件 | 修改 | 目的 |
| --- | --- | --- |
| `Pages/PageSetup/PageSetupLaunch.xaml` | `SliderRamCustom` 加 `AutomationProperties.Name="自定义游戏内存"` | 内存滑块左边是单选框，找不到文字标签 |
| `Pages/PageInstance/PageInstanceSetup.xaml` | 同上 | 同上 |
| `Pages/PageLaunch/PageLaunchLeft.xaml` | `BtnLaunch` 加 `AutomationProperties.HelpText="{Binding Text, ElementName=LabVersion}"`；`LabVersion` 加 `KeyboardNavigation.IsTabStop="False"` | 启动按钮下方的版本小字作为按钮说明朗读，不单独停留 |
| `Pages/PageLaunch/PageLaunchLeft.xaml` | `PanInput` 设 `TabNavigation="Local"`；`PanLogin` TabIndex 0、启动按钮所在 Grid TabIndex 1、`BtnInstance` 2、`BtnMore` 3 | Tab 顺序与屏幕一致：档案 → 启动 → 实例选择 → 实例设置（上游书写顺序是实例选择在最前） |
| `Pages/PageLaunch/PageLoginProfile.xaml` | `ItemsControl` → `local:AccessItemsControl` | 档案列表不再朗读「数据项目」和类名。**注意不要**加 `AccessList.IsList`，否则档案项里的「更改 UUID」等按钮无法到达 |
| `Pages/PageDownload/Comp/PageDownloadCompDetail.xaml` | `PanInstanceFilter`、`PanModLoaderFilter` 加 `local:AccessList.IsList="True" local:AccessList.IsHorizontal="True"` | 模组详情页的「实例筛选」「模组加载器筛选」变成列表；列表标签自动取行首文字 |
| `Pages/PageSetup/PageSetupLeft.xaml` | `ItemUpdate`、`ItemFeedback` 加 `Visibility="Collapsed"` | 隐藏「软件更新」「反馈」 |

---

## 5. 功能说明（按使用场景）

### 5.1 控件朗读

- 自绘按钮、复选框、单选框、列表项都有 UIA 节点，空格 / 回车激活，复用原来的鼠标点击逻辑（先把控件的「鼠标已按下」标记置为 true，再调用原来的鼠标抬起处理）。
- 输入框、下拉框、滑块的名称：优先 `AutomationProperties.Name`；否则在同一行左侧找文字标签（单独的 TextBlock，或横向 StackPanel 里的第一个 TextBlock）；再退回提示文字和较短（≤20 字）的 ToolTip。
- 卡片是带标题的分组，读屏进入卡片时朗读标题，用来区分同名设置（例如两个「不透明度」）。
- 图标按钮没有名称时，按 SVG 图标名猜一个中文名（`AccessHelper.IconName`），例如 `refresh-cw` → 刷新。

### 5.2 静态文字

- 读屏焦点模式只读获得焦点的元素，所以把「独立的说明文字」设为可获得焦点。
- 不处理：控件内部文字、控件标签、卡片标题。左侧导航分类标题例外：作为二级标题，可用方向键停留，但不计入项数。
- **TextBlock 自身通常收不到 Loaded 事件**（WPF 只向挂了实例处理器的子树广播 Loaded），所以改为：卡片 / 提示条 / 页面 / 弹窗加载时扫描，并且主窗口布局变化时节流（400 毫秒）重扫 `PanMain` 和 `PanMsg`。

### 5.3 弹窗

- 弹窗（MyMsgText / Markdown / Input / Select）打开时：焦点进入弹窗；约 600 毫秒后朗读「标题。正文」；`PanMsg` 设为 Tab 循环，焦点不会跑出弹窗；关闭后焦点回到打开前的控件。
- 上游已有：弹窗中回车 = 第一个按钮，Esc = 最后一个按钮。

### 5.4 焦点管理

- 切换大页面后，焦点落到新左侧页面的选中项（启动页落到启动按钮）。
- 焦点控件被移除或隐藏时（例如关掉一张卡片），焦点回到原来所在的区域：列表区域放到选中项，其他区域按 Tab 顺序放到第一个控件。
- 焦点在弹出层（右键菜单、下拉框列表）里时不算焦点丢失（`AccessList.IsFocusInPopup()`）。

### 5.5 已知坑（以后改代码时注意）

| 现象 | 原因 | 处理 |
| --- | --- | --- |
| 方向键把焦点挪进列表项内部的小按钮且回不来 | 外层 ScrollViewer 先处理方向键 / WPF 自带方向导航 | 列表用 **PreviewKeyDown** 拦截，并接管四个方向键 |
| Tab 卡在滑块前 | 上游 MyScrollViewer 取消滑块获得焦点 | 见 4.2 |
| 「更改 UUID」等按钮键盘到不了 | 列表项按钮是鼠标首次悬停时才生成（`ContentHandler`） | MyListItem 获得键盘焦点时也生成 |
| 模组项「快速下载」「收藏」第一次按没反应 | 点击处理要求按钮区透明度 > 0，按钮区只在悬停时淡入 | 获得键盘焦点时显示按钮区 |
| 用代码打开的右键菜单没有焦点 | WPF 不会自动给它焦点 | `MenuAccess.cs` |
| 在启动页按回车直接启动游戏 / 跳到下载页 | 上游全局回车启动 | 见 4.2 |
| exe 被替换成 23 MB 的上游版本，无障碍全部失效 | 上游自动更新 | 见 4.2；若再发生，删掉 `bin\...\Plain Craft Launcher 2.exe` 和 `PCL\Plain Craft Launcher Community Edition.exe` 后重新编译 |
| 文字被当成导航分类标题跳过 | 早期规则只看「和 MyListItem 在同一纵向 StackPanel」 | 现在只在 MyPageLeft 里生效 |

---

## 6. 合并上游新版本的步骤

1. 备份当前分支：`git branch access-backup-<日期>`。
2. 获取上游 tag：`git fetch origin --tags`。
3. 在新 tag 上建分支并合并（或者 rebase）：

```bash
git switch -c access-<新版本> <新版本tag>
```

```bash
git merge access
```

4. 解决冲突。`Accessibility` 文件夹里的新文件一般不冲突；冲突通常出在第 4 节列出的上游文件里。
5. 按第 4 节**逐条核对**，尤其是：
   - `UpdateManager` 的更新拦截还在（**最重要**，否则自动更新会覆盖无障碍版）。
   - 第 4.1 节的控件仍是 partial；`MyPageLeft` 没有新增无参构造函数。
   - 上游是否新增了自绘控件（`Controls/` 下新的 `My*` 类），需要照 `ControlsAccess.cs` 的写法接入。
   - 上游是否改了用到的私有字段名（例如 `isMouseDown`、`mouseDowned`、`isLeftMouseHeld`、`Button_MouseUp`、`PanButtons`、`buttonStack`、`ContentHandler`），改了的话编译会报错，照新名字改即可。
6. 编译，按第 7 节跑一遍测试，再请读屏用户实测。

---

## 7. 测试方法

### 7.1 UIA 脚本（`AccessTools/`）

脚本用 Windows PowerShell 5（`powershell.exe`）运行，文件里只能有 ASCII 字符（否则 PS5 会读错编码）。参数 `-ProcessId` 是 PCL 的进程号。

| 脚本 | 用途 |
| --- | --- |
| `uia-dump.ps1 -ProcessId <pid> [-MaxDepth 12]` | 打印 UIA 控件树：`类型* [类名] '名称' \| 说明`，`*` 表示可获得键盘焦点 |
| `uia-text.ps1 -ProcessId <pid> -Label <标题>` | 列出页面上的文字节点和所在分组 |
| `uia-invoke.ps1 -ProcessId <pid> -Name <名称> [-Action invoke\|select\|toggle\|focus\|expand] [-Class <类名>]` | 按名称找控件并执行 UIA 操作 |
| `uia-keys.ps1 -ProcessId <pid> -KeyList "{TAB} {DOWN} {ENTER}"` | 依次发送按键（空格分隔，SendKeys 语法），每次打印当前焦点及「第几项 / 共几项」。会把 PCL 窗口切到前台 |
| `uia-focuspath.ps1 -ProcessId <pid> -Key "{DOWN}"` | 发一个键，打印前后焦点元素及其祖先链 |
| `uia-slider.ps1 -ProcessId <pid>` | 读出所有滑块的值，并测试通过 UIA 修改数值 |

常用流程：

```powershell
$p = Start-Process "Plain Craft Launcher 2\bin\Debug\net10.0-windows\win-x64\Plain Craft Launcher 2.exe" -PassThru
Start-Sleep 12
powershell.exe -NoProfile -File AccessTools\uia-keys.ps1 -ProcessId $p.Id -KeyList "{ENTER} {TAB} {TAB}"
```

注意：

- 启动页焦点在「启动游戏」上时**不要随手按回车**，会真的启动游戏；模组项的「快速下载」按回车会真的下载。
- 进入工具页时可能弹出「获取大厅公告失败」的网络错误弹窗，按回车关闭即可。
- 排查焦点被吞的问题时，可以临时加一个类处理器记录 `PreviewGotKeyboardFocus` 和 `GotKeyboardFocus`（看 `e.Handled`），排查完删掉。

### 7.2 人工测试清单（NVDA）

- [ ] 启动：焦点在启动按钮，读出「启动游戏 / 下载游戏 按钮」和版本说明；有弹窗时焦点在弹窗，正文被朗读。
- [ ] Tab 只在顶部、左侧、右侧之间循环；顶部和左侧进入时落在选中项。
- [ ] 下载页左侧：上下键移动，分类标题朗读为标题；左右键到「刷新」按钮再回来。
- [ ] 实例选择左侧：右键到「设置」，回车打开菜单，上下选择，Esc 回到按钮。
- [ ] 模组列表：名称、支持版本、下载量、更新时间、来源、标签、简介都能读到；「快速下载」「收藏」第一次就能用键盘触发。
- [ ] 模组详情页：实例筛选、模组加载器筛选是列表，带筛选名称。
- [ ] 设置各页：输入框 / 下拉框 / 滑块都有名称；滑块可用方向键调整并读出新值；Tab 能经过滑块。
- [ ] 工具页大厅：条款文字可读。
- [ ] 右下角提示被自动朗读；启动时朗读无障碍版作者信息。
- [ ] 设置页没有「软件更新」「反馈」。

---

## 8. 后续可以做的事（待办）

- 下载和启动游戏过程中的进度播报（目前只有右下角提示会被朗读）。
- 正版验证（设备代码流）的无障碍：朗读并一键复制验证码、播报每一步状态。
- 页面切换时主动朗读页面名称。
- 焦点框目前看不见（上游把 FocusVisualStyle 关了），视力正常的协助者看不出焦点位置。
- 图标按钮的名称有一部分是按图标猜的，需要逐个核对；个别按钮会读出内部名字（例如 `BtnExtraMusic`）。
- 右侧区域里由列表项组成的长列表（版本列表、模组列表）目前每项一个 Tab 停留点；如果以后要改成方向键列表，需要先解决列表项内部按钮的到达问题（参考左侧列表的左右键方案）。
- 推送到个人 GitHub 仓库（之前复刻被 GitHub 拒绝，可以新建仓库后添加远程推送）。
- Release 版发布打包流程。
