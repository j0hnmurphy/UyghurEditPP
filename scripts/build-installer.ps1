#Requires -Version 5.1
# Builds Release, then compiles installer\UyghurEditPP.iss with Inno Setup 6.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sln = Join-Path $root 'UyghurEditPP.sln'

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild not found (vswhere).' }

$iscc = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 (ISCC.exe) not found.' }

& $msbuild $sln -restore -p:RestorePackagesConfig=true -p:Configuration=Release -v:minimal
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed ($LASTEXITCODE)." }

& $iscc (Join-Path $root 'installer\UyghurEditPP.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)." }

$setup = Get-ChildItem -LiteralPath (Join-Path $root 'installer\Output') -Filter '*-setup.exe' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
$hash = (Get-FileHash -LiteralPath $setup.FullName -Algorithm SHA256).Hash
Write-Host "Output: $($setup.FullName)"
Write-Host "Size:   $($setup.Length) bytes"
Write-Host "SHA256: $hash"
