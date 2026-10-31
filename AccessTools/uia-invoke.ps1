param([int]$ProcessId, [string]$Name, [string]$Action = "invoke", [string]$Class = "")
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]
$win = $A::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,
    (New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $ProcessId)))
$el = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
    $(if ($Class) { New-Object System.Windows.Automation.AndCondition((New-Object System.Windows.Automation.PropertyCondition($A::NameProperty, $Name)), (New-Object System.Windows.Automation.PropertyCondition($A::ClassNameProperty, $Class))) } else { New-Object System.Windows.Automation.PropertyCondition($A::NameProperty, $Name) }))
if (-not $el) { "not found: $Name"; exit 1 }
switch ($Action) {
    'invoke' { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
    'select' { $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
    'toggle' { $el.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle() }
    'focus'  { $el.SetFocus() }
    'expand' { $el.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand() }
}
"done $Action $Name"
