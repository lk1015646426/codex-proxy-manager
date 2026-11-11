$ErrorActionPreference = 'Stop'
$sourceRoot = $PSScriptRoot
$compiler = Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$outputPath = Join-Path $sourceRoot 'build\CodexProxyDesktop.exe'
New-Item -ItemType Directory -Path (Join-Path $sourceRoot 'build') -Force | Out-Null
$sources = @('Core.cs', 'App.cs', 'Network.cs', 'UI.cs', 'Fixes.cs', 'CountryFallback.cs', 'RuleRouting.cs', 'RuleRepair.cs', 'Enhancements.cs', 'Evidence23.cs', 'ExitInfo.cs', 'FloatWindow.cs', 'Verification21.cs', 'Verification23.cs', 'Diagnostics24.cs', 'DiagnosticsUI24.cs', 'Verification24.cs', 'Automation25.cs', 'Verification25.cs') | ForEach-Object { Join-Path $sourceRoot ('src\' + $_) }
$sources += @('RuleProbe26.cs','Verification26.cs') | ForEach-Object { Join-Path $sourceRoot ('src\' + $_) }
$resources = $sources | ForEach-Object { '/resource:' + $_ + ',Current' + [IO.Path]::GetFileName($_) }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll /reference:System.Web.Extensions.dll /reference:Microsoft.CSharp.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ('/win32icon:' + (Join-Path $sourceRoot 'assets\app.ico')) ('/win32manifest:' + (Join-Path $sourceRoot 'assets\app.manifest')) ('/out:' + $outputPath) @resources @sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
Get-Item -LiteralPath $outputPath | Select-Object FullName, Length
