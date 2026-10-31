$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$runtime=[Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
$csc=Join-Path $runtime 'csc.exe'
$output=Join-Path $project 'package\FlowBridge-Setup.exe'
$references=@('System.Windows.Forms.dll','System.Drawing.dll', (Join-Path $runtime 'WPF\UIAutomationClient.dll'),(Join-Path $runtime 'WPF\UIAutomationTypes.dll'),(Join-Path $runtime 'WPF\WindowsBase.dll'))
$icon=Join-Path $project 'assets\flowbridge.ico'
if(!(Test-Path $icon)){throw 'Missing assets/flowbridge.ico. Restore the tracked icon assets before building.'}
$arguments=@('/nologo','/target:winexe','/platform:anycpu','/optimize+','/warn:4','/warnaserror+',('/out:'+$output),('/win32icon:'+$icon))
foreach($r in $references){$arguments+=('/reference:'+$r)}
$arguments+=(Join-Path $PSScriptRoot 'Setup.cs')
$arguments+=(Join-Path $PSScriptRoot 'FirefoxInstaller.cs')
$arguments+=(Join-Path $PSScriptRoot 'Payload.cs')
$start=New-Object System.Diagnostics.ProcessStartInfo
$start.FileName=$csc
$start.Arguments=($arguments | ForEach-Object {'"'+$_+'"'}) -join ' '
$start.UseShellExecute=$false
$start.RedirectStandardOutput=$true
$start.RedirectStandardError=$true
$compiler=[System.Diagnostics.Process]::Start($start)
Write-Output $compiler.StandardOutput.ReadToEnd()
Write-Output $compiler.StandardError.ReadToEnd()
$compiler.WaitForExit()
Write-Output ('Compiler exit='+$compiler.ExitCode)
if($compiler.ExitCode -ne 0){throw 'Compilation failed'}
Add-Type 'using System;using System.Runtime.InteropServices;public static class FlowBridgeIconRefresh{[DllImport("shell32.dll",CharSet=CharSet.Unicode)]public static extern void SHChangeNotify(int eventId,uint flags,string item1,string item2);}'
[FlowBridgeIconRefresh]::SHChangeNotify(0x2000,5,$output,$null)
Get-Item $output | Select-Object Name,Length
