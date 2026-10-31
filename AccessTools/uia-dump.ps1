param([int]$ProcessId, [int]$MaxDepth = 12)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId)
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if (-not $win) { "window not found"; exit 1 }
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
function Dump($el, $depth) {
    if ($depth -gt $MaxDepth) { return }
    $c = $el.Current
    if (-not $c.IsOffscreen -or $depth -lt 3) {
        $type = $c.ControlType.ProgrammaticName -replace 'ControlType.', ''
        $focus = if ($c.IsKeyboardFocusable) { '*' } else { '' }
        $help = if ($c.HelpText) { " | $($c.HelpText)" } else { '' }
        if ($c.Name -or $c.IsKeyboardFocusable -or $depth -lt 3) {
            ('  ' * $depth) + "$type$focus [$($c.ClassName)] '$($c.Name)'$help"
        }
    }
    $child = $walker.GetFirstChild($el)
    while ($child) { Dump $child ($depth + 1); $child = $walker.GetNextSibling($child) }
}
Dump $win 0
