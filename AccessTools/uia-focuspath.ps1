param([int]$ProcessId, [string]$Key = "{DOWN}")
# Sends one key and prints the focused element with its ancestor chain.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms
$shell = New-Object -ComObject WScript.Shell
$null = $shell.AppActivate($ProcessId)
Start-Sleep -Milliseconds 400
$A = [System.Windows.Automation.AutomationElement]
$walker = [System.Windows.Automation.TreeWalker]::RawViewWalker
function Path($el) {
    $parts = @()
    while ($el) {
        $c = $el.Current
        $parts += "$($c.ClassName)'$($c.Name)'"
        $el = $walker.GetParent($el)
        if ($parts.Count -gt 6) { break }
    }
    $parts -join ' < '
}
"before: " + (Path $A::FocusedElement)
[System.Windows.Forms.SendKeys]::SendWait($Key)
Start-Sleep -Milliseconds 700
"after:  " + (Path $A::FocusedElement)
