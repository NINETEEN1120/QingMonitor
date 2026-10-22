param([string]$AppDirectory=(Join-Path $PSScriptRoot '..\构建\程序'),[string]$OutputDirectory=(Join-Path $PSScriptRoot '..\构建\安装包'))
$ErrorActionPreference='Stop'
$AppDirectory=[IO.Path]::GetFullPath($AppDirectory)
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$driver=Join-Path $AppDirectory '驱动\PawnIO_setup.exe'
$expectedDriver='1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032'
if((Get-FileHash -LiteralPath $driver).Hash -ne $expectedDriver -or (Get-AuthenticodeSignature -LiteralPath $driver).Status -ne 'Valid'){throw '内置 PawnIO 安装器签名或哈希不正确。'}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$stage=Join-Path $OutputDirectory ('暂存-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$payload=Join-Path $stage '程序'
New-Item -ItemType Directory -Force -Path $payload | Out-Null
$required=@('QingMonitor.exe','QingMonitor.exe.config','PresentMon.exe','使用说明.txt','第三方声明.txt')+(Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\验证\required-dlls.txt') -ErrorAction SilentlyContinue)
if($required.Count -lt 14){$required=@('QingMonitor.exe','QingMonitor.exe.config','PresentMon.exe','使用说明.txt','第三方声明.txt','LibreHardwareMonitorLib.dll','BlackSharp.Core.dll','DiskInfoToolkit.dll','RAMSPDToolkit-NDD.dll','HidSharp.dll','System.Memory.dll','System.Buffers.dll','System.Numerics.Vectors.dll','System.Runtime.CompilerServices.Unsafe.dll')}
foreach($name in $required){Copy-Item -LiteralPath (Join-Path $AppDirectory $name) -Destination $payload}
Copy-Item -LiteralPath (Join-Path $AppDirectory 'licenses'),(Join-Path $AppDirectory '驱动') -Destination $payload -Recurse
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=Join-Path $stage 'payload.zip'
[IO.Compression.ZipFile]::CreateFromDirectory($payload,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
$payloadHash=(Get-FileHash -LiteralPath $zip).Hash
$info=Join-Path $stage 'PackageInfo.cs'
[IO.File]::WriteAllText($info,('namespace QingMonitorSetup { internal static class PackageInfo { internal const string PayloadHash="'+$payloadHash+'"; } }'))
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output=Join-Path $OutputDirectory '我在监控你的电脑-0.1.11-离线安装包.exe'
& $compiler /nologo /optimize+ /platform:x64 /target:winexe "/out:$output" "/win32manifest:$PSScriptRoot\Installer\setup.manifest" "/win32icon:$PSScriptRoot\Assets\QingMonitor.ico" "/resource:$PSScriptRoot\Assets\QingMonitor.ico,QingMonitor.Icon" "/resource:$zip,QingMonitor.Payload" /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.ServiceProcess.dll /r:Microsoft.CSharp.dll (Join-Path $PSScriptRoot 'Installer\InstallerCore.cs') (Join-Path $PSScriptRoot 'Installer\SetupProgram.cs') $info
if($LASTEXITCODE -ne 0){throw '离线安装包编译失败'}
[pscustomobject]@{Path=$output;Bytes=(Get-Item -LiteralPath $output).Length;SHA256=(Get-FileHash -LiteralPath $output).Hash;PayloadSHA256=$payloadHash;PawnIOSHA256=$expectedDriver;DriverBundled=$true;RequiresNetwork=$false;CompiledAt=(Get-Date).ToString('O')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory '安装包构建.json') -Encoding utf8
Get-Item -LiteralPath $output | Select-Object FullName,Length
