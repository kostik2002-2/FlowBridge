param([string]$Browser='chrome')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$project=Split-Path $PSScriptRoot -Parent
[Reflection.Assembly]::LoadFile((Join-Path $project 'package\FlowBridge-Setup.exe'))|Out-Null
[FlowBridge.Native]::SetProcessDPIAware()|Out-Null
$b=[FlowBridge.Engine]::Detect()|Where-Object Id -eq $Browser
if(!$b){throw 'The requested browser is not installed'}
$flags=[Reflection.BindingFlags]'Static,NonPublic'
$engine=[FlowBridge.Engine]
$root=$engine.GetMethod('BrowserRoot',$flags).Invoke($null,@($b.PSObject.BaseObject,5))
$address=$engine.GetMethod('BrowserAddress',$flags).Invoke($null,@($b.PSObject.BaseObject,$root))
$chrome=$engine.GetMethod('IsBrowserControl',$flags)
if(!$chrome.Invoke($null,@($address,$root))){throw 'Native address control rejected'}
$docs=$root.FindAll([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Document)))
$checked=0
foreach($doc in $docs){
$child=$doc.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition)
if($child){
if($chrome.Invoke($null,@($child,$root))){throw 'Web content accepted as browser chrome'}
$checked++
}
}
if(!$checked){throw 'Open a normal webpage in the requested browser before running this check'}
# A stale binding represents a window that was closed between installation attempts.
$b.WindowHandle=[IntPtr]1
$result=[FlowBridge.Engine]::Install($b,[Action[string]]{param($message)})
if($result -ne 'Расширение уже установлено'){throw ('This check expects an existing current installation: '+$result)}
if($b.WindowHandle -eq [IntPtr]1 -or $b.WindowHandle -eq [IntPtr]::Zero){throw 'Stale window binding was not replaced'}
Write-Output ('PASS: '+$Browser+' native controls, web-content exclusion and stale-window recovery')
