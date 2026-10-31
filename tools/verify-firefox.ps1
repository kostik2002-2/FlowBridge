$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$project=Split-Path $PSScriptRoot -Parent
$package=Join-Path $project 'package\signing\FlowBridge-0.1.1-signed.xpi'
$expected='7DA9235F189CF8B62D5DC82C5C3204E555112C91F3324A3CAE05F8054C5A5FB1'
if((Get-FileHash $package -Algorithm SHA256).Hash -ne $expected){throw 'Signed Firefox XPI hash mismatch'}
$folder=Join-Path $project 'package\extension\firefox'
$zip=[IO.Compression.ZipFile]::OpenRead($package)
try{
$files=@(Get-ChildItem $folder -File)
foreach($file in $files){
$entry=$zip.GetEntry($file.Name)
if(!$entry){throw ('Missing signed entry: '+$file.Name)}
if($file.Name -eq 'manifest.json'){
$reader=New-Object IO.StreamReader($entry.Open())
try{$signedManifest=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}
$builtManifest=[IO.File]::ReadAllText($file.FullName)|ConvertFrom-Json
$left=$signedManifest|ConvertTo-Json -Depth 20 -Compress
$right=$builtManifest|ConvertTo-Json -Depth 20 -Compress
if($left -ne $right){Write-Output ('Signed manifest: '+$left);Write-Output ('Built manifest: '+$right);throw 'Signed manifest semantic mismatch'}
continue
}
$stream=$entry.Open()
$sha=[Security.Cryptography.SHA256]::Create()
try{$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$sha.Dispose();$stream.Dispose()}
if($hash -ne (Get-FileHash $file.FullName -Algorithm SHA256).Hash){throw ('Source build differs from signed package: '+$file.Name)}
}
$names=@($files|ForEach-Object Name)
foreach($entry in $zip.Entries){
if($entry.FullName -notmatch '^META-INF/' -and $entry.FullName -notin $names){throw ('Unexpected signed payload entry: '+$entry.FullName)}
}
Write-Output ('PASS: signed Firefox XPI hash and '+$files.Count+' source-built files match')
}finally{$zip.Dispose()}
