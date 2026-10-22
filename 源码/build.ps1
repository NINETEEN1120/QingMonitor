param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\构建\程序'))
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(!(Test-Path -LiteralPath $compiler)){throw '需要 Windows x64 和 .NET Framework 4.8 或更新版本。'}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$vendor=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\vendor\runtime'))
$deps=if(Test-Path -LiteralPath $vendor){$vendor}else{[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\构建\程序'))}
if(!(Test-Path -LiteralPath (Join-Path $deps 'LibreHardwareMonitorLib.dll'))){throw '缺少依赖。请完整克隆仓库（含 vendor/runtime），或解压完整源码包。'}
if(Test-Path -LiteralPath $vendor){
 $manifestPath=Join-Path $PSScriptRoot '..\vendor\manifest.json'
 if(!(Test-Path -LiteralPath $manifestPath)){throw '缺少依赖清单 vendor/manifest.json。'}
 $manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
 foreach($entry in $manifest.Files){
  $dependency=Join-Path $vendor $entry.Path
  if(!(Test-Path -LiteralPath $dependency) -or (Get-FileHash -LiteralPath $dependency -Algorithm SHA256).Hash -ne $entry.SHA256){throw ('依赖文件校验失败：'+$entry.Path)}
 }
}
if($deps -ne $OutputDirectory){Get-ChildItem -LiteralPath $deps -File | Where-Object {$_.Extension -eq '.dll' -or $_.Name -eq 'PresentMon.exe'} | Copy-Item -Destination $OutputDirectory}
if($deps -ne $OutputDirectory){foreach($folder in @('licenses','驱动')){if(Test-Path -LiteralPath (Join-Path $deps $folder)){Copy-Item -LiteralPath (Join-Path $deps $folder) -Destination $OutputDirectory -Recurse -Force}}}
foreach($document in @(@('..\docs\USER_GUIDE.txt','使用说明.txt'),@('..\docs\THIRD_PARTY_NOTICES.md','第三方声明.txt'))){$path=Join-Path $PSScriptRoot $document[0];if(Test-Path -LiteralPath $path){Copy-Item -LiteralPath $path -Destination (Join-Path $OutputDirectory $document[1]) -Force}}
$sources=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | Select-Object -ExpandProperty FullName
& $compiler /nologo /optimize+ /platform:x64 /target:winexe "/out:$OutputDirectory\QingMonitor.exe" "/win32manifest:$PSScriptRoot\app.manifest" "/win32icon:$PSScriptRoot\Assets\QingMonitor.ico" "/resource:$PSScriptRoot\Assets\QingMonitor.ico,QingMonitor.Icon" /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Management.dll /r:System.Runtime.Serialization.dll "/r:$deps\LibreHardwareMonitorLib.dll" $sources
if($LASTEXITCODE -ne 0){throw '编译失败'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'QingMonitor.exe.config') -Destination $OutputDirectory
Get-Item -LiteralPath (Join-Path $OutputDirectory 'QingMonitor.exe') | Select-Object FullName,Length,LastWriteTime
