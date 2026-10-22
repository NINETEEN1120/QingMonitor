param(
 [string]$AppDirectory=(Join-Path $PSScriptRoot '..\artifacts\app'),
 [string]$OutputDirectory=(Join-Path $PSScriptRoot '..\artifacts\checks')
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$AppDirectory=[IO.Path]::GetFullPath($AppDirectory)
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$app=Join-Path $AppDirectory 'QingMonitor.exe'
if(!(Test-Path -LiteralPath $app)){throw '先运行 源码/build.ps1 -OutputDirectory artifacts/app。'}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
Copy-Item -LiteralPath $app -Destination $OutputDirectory -Force
Get-ChildItem -LiteralPath $AppDirectory -Filter '*.dll' | Copy-Item -Destination $OutputDirectory -Force
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$checks=@('AxisResizeMathChecks','SettingsConsistencyChecks','GlassControlsChecks','TemperatureColorChecks','ResponsiveStripChecks')
$results=@()
foreach($name in $checks){
 $exe=Join-Path $OutputDirectory ($name+'.exe')
 $references=@('/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Runtime.Serialization.dll','/r:System.Management.dll')
 $sources=@((Join-Path $root ('tests\'+$name+'.cs')))
 if($name -eq 'AxisResizeMathChecks'){$sources+=Join-Path $root '源码\OverlayResize.cs'}else{$references+='/r:'+$app}
 & $compiler /nologo /platform:x64 /target:exe "/out:$exe" @references @sources
 if($LASTEXITCODE -ne 0){throw ('编译检查失败：'+$name)}
 $arguments=@((Join-Path $OutputDirectory $name))
 if($name -eq 'TemperatureColorChecks'){$arguments+=Join-Path $root 'tests\fixtures\legacy-settings.json'}
 & $exe @arguments | Tee-Object -FilePath (Join-Path $OutputDirectory ($name+'.txt'))
 if($LASTEXITCODE -ne 0){throw ('检查失败：'+$name)}
 $results+=[pscustomobject]@{Name=$name;Passed=$true}
}
$results|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $OutputDirectory 'results.json') -Encoding utf8
Write-Output '离线构建与无硬件检查通过；实际设备、管理员权限及游戏验收需另行测试。'
