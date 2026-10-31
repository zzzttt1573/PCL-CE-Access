param([int]$ProcessId)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]
$win = $A::RootElement.FindFirst('Children', (New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $ProcessId)))
$sliders = $win.FindAll('Descendants', (New-Object System.Windows.Automation.PropertyCondition($A::ClassNameProperty, 'MySlider')))
foreach ($e in $sliders) {
    $v = $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
    $r = $e.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
    "$($e.Current.Name) = $v ($($r.Current.Value) / $($r.Current.Maximum))"
}
# Step the first slider up by one, read it back, then restore it.
$first = $sliders[0]
$rp = $first.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
$rp.SetValue($rp.Current.Value + 1)
Start-Sleep -Milliseconds 500
"after +1: " + $first.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
$rp.SetValue($rp.Current.Value - 1)
