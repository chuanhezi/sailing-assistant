param(
    [string]$GameRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$managed = Join-Path $GameRoot 'ThePirate_Data\Managed'
$api = Join-Path $GameRoot 'UserLibs\PirateModAPI.dll'
$source = Join-Path $PSScriptRoot 'SailingAssistant.cs'
$output = Join-Path $GameRoot 'Mods\PirateExtensions\SailingAssistant.dll'

New-Item -ItemType Directory -Force -Path (Split-Path $output) | Out-Null
$references = @(
    (Join-Path $managed 'mscorlib.dll'),
    (Join-Path $managed 'System.dll'),
    (Join-Path $managed 'System.Core.dll'),
    (Join-Path $managed 'netstandard.dll'),
    (Join-Path $managed 'UnityEngine.dll'),
    (Join-Path $managed 'UnityEngine.CoreModule.dll'),
    (Join-Path $managed 'UnityEngine.IMGUIModule.dll'),
    (Join-Path $managed 'UnityEngine.TextRenderingModule.dll'),
    $api
)

& $compiler /noconfig /nostdlib /target:library "/out:$output" `
    ($references | ForEach-Object { "/reference:$_" }) $source
if ($LASTEXITCODE -ne 0) { throw 'Sailing Assistant compilation failed.' }
Write-Host "Built $output"
