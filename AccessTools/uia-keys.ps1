param([int]$ProcessId, [string]$KeyList)
$Keys = $KeyList -split " "
# Sends keys to the app window one by one and prints the focused element after each key.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms
$shell = New-Object -ComObject WScript.Shell
$null = $shell.AppActivate($ProcessId)
Start-Sleep -Milliseconds 500
$A = [System.Windows.Automation.AutomationElement]
function Show($label) {
    $f = $A::FocusedElement
    if (-not $f) { "$label -> (none)"; return }
    $c = $f.Current
    $type = $c.ControlType.ProgrammaticName -replace 'ControlType.', ''
    $pos = ''
    try {
        $p = $f.GetCurrentPropertyValue($A::PositionInSetProperty)
        $s = $f.GetCurrentPropertyValue($A::SizeOfSetProperty)
        if ($p -gt 0) { $pos = " ($p/$s)" }
    } catch {}
    $name = ($c.Name -replace '\s+', ' ')
    if ($name.Length -gt 40) { $name = $name.Substring(0, 40) + '...' }
    "$label -> $type [$($c.ClassName)] '$name'$pos"
}
foreach ($k in $Keys) {
    [System.Windows.Forms.SendKeys]::SendWait($k)
    Start-Sleep -Milliseconds 700
    Show $k
}
