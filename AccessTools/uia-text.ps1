param([int]$ProcessId, [string]$Label)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]
$win = $A::RootElement.FindFirst('Children', (New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $ProcessId)))
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
function Walk($el, $depth, $group) {
    $c = $el.Current
    if ($c.ControlType.ProgrammaticName -eq 'ControlType.Group') { $group = $c.Name }
    if ($c.ControlType.ProgrammaticName -eq 'ControlType.Text' -and $c.Name.Trim() -and -not $c.IsOffscreen) {
        $t = ($c.Name -replace "\s+", ' ')
        if ($t.Length -gt 70) { $t = $t.Substring(0, 70) + '...' }
        "[$group] $t"
    }
    $child = $walker.GetFirstChild($el)
    while ($child) { Walk $child ($depth + 1) $group; $child = $walker.GetNextSibling($child) }
}
"===== $Label"
Walk $win 0 ''
