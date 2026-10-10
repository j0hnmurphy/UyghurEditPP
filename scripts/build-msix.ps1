<#
.SYNOPSIS
  Builds the UyghurEdit++ MSIX package (unsigned, or signed with a test PFX).
.DESCRIPTION
  Release build -> stage the files of the release zip + manifest + assets -> makepri -> makeappx pack.
  Output: packaging\msix\Output\UyghurEditPP-<version>.msix
  The default Identity values are for local testing only. Replace them with the values
  from Partner Center (after reserving the name) for the Store submission; see packaging\msix\README.md.
#>
param(
	[string]$IdentityName = 'UyghurEditPP.Test',
	[string]$Publisher = 'CN=UyghurEditPP Test',
	[string]$PublisherDisplayName = 'UyghurEdit++ Test',
	[switch]$SkipBuild,
	[string]$SignWithPfx,
	[securestring]$PfxPassword,
	# Default: the newest Windows 10/11 SDK that has makeappx.exe.
	[string]$SdkBin
)
$ErrorActionPreference = 'Stop'

if(-not $SdkBin){
	$kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
	$SdkBin = Get-ChildItem $kits -Directory -Filter '10.*' -ErrorAction SilentlyContinue |
		Sort-Object { [version]$_.Name } -Descending |
		ForEach-Object { Join-Path $_.FullName 'x64' } |
		Where-Object { Test-Path (Join-Path $_ 'makeappx.exe') } |
		Select-Object -First 1
}
foreach($tool in 'makepri.exe', 'makeappx.exe'){
	if(-not $SdkBin -or -not (Test-Path (Join-Path $SdkBin $tool))){
		throw "$tool not found. Install the Windows SDK or pass -SdkBin <SDK bin\x64 folder>."
	}
}

$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$msixDir = Join-Path $repo 'packaging\msix'
$binDir = Join-Path $repo 'bin\UyghurEditPP'
$staging = Join-Path $msixDir 'staging'
$outDir = Join-Path $msixDir 'Output'

if(-not $SkipBuild){
	$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
	$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
	if(-not $msbuild){ throw 'MSBuild not found (vswhere).' }
	& $msbuild (Join-Path $repo 'UyghurEditPP.sln') -restore -p:RestorePackagesConfig=true -p:Configuration=Release -v:m -nologo
	if($LASTEXITCODE -ne 0){ throw "MSBuild failed ($LASTEXITCODE)." }
}

$exe = Join-Path $binDir 'UyghurEditPP.exe'
if(-not (Test-Path $exe)){ throw "Not built: $exe" }
# MSIX needs a four-part version whose last part is 0 (the Store reserves it).
$fv = [Version][Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion
$version = '{0}.{1}.{2}.0' -f $fv.Major, [Math]::Max($fv.Minor, 0), [Math]::Max($fv.Build, 0)

# Stage: same files as the release zip.
if(Test-Path $staging){ [IO.Directory]::Delete($staging, $true) }
New-Item -ItemType Directory -Force $staging, $outDir | Out-Null
$files = 'UyghurEditPP.exe', 'UyghurEditPP.exe.config', 'DocumentFormat.OpenXml.dll', 'DocumentFormat.OpenXml.Framework.dll',
	'UKIJTuz.ttf', 'UKIJTuzBold.ttf', 'imla_xatatoghra.txt'
foreach($f in $files){ Copy-Item (Join-Path $binDir $f) $staging }
Copy-Item (Join-Path $repo 'LICENSE') (Join-Path $staging 'LICENSE.txt')
Copy-Item (Join-Path $repo 'README.md') $staging
Copy-Item (Join-Path $msixDir 'Assets') (Join-Path $staging 'Assets') -Recurse

$manifest = [IO.File]::ReadAllText((Join-Path $msixDir 'AppxManifest.xml'))
$manifest = $manifest.Replace('__IDENTITY_NAME__', $IdentityName).Replace('__PUBLISHER__', [Security.SecurityElement]::Escape($Publisher)).Replace('__PUBLISHER_DISPLAY_NAME__', [Security.SecurityElement]::Escape($PublisherDisplayName)).Replace('__VERSION__', $version)
if($manifest -match '__[A-Z_]+__'){ throw "Unreplaced placeholder in the manifest: $($Matches[0])" }
[IO.File]::WriteAllText((Join-Path $staging 'AppxManifest.xml'), $manifest, (New-Object Text.UTF8Encoding($false)))

# Resource index (needed for the asset names in the manifest). The config stays outside the package.
$tmp = Join-Path ([IO.Path]::GetTempPath()) ('msixpri-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $tmp | Out-Null
try{
	$cfg = Join-Path $tmp 'priconfig.xml'
	& (Join-Path $SdkBin 'makepri.exe') createconfig /cf $cfg /dq en-US /o
	if($LASTEXITCODE -ne 0){ throw "makepri createconfig failed ($LASTEXITCODE)." }
	& (Join-Path $SdkBin 'makepri.exe') new /pr $staging /cf $cfg /of (Join-Path $staging 'resources.pri') /o
	if($LASTEXITCODE -ne 0){ throw "makepri new failed ($LASTEXITCODE)." }
}
finally{
	[IO.Directory]::Delete($tmp, $true)
}

$msix = Join-Path $outDir "UyghurEditPP-$version.msix"
& (Join-Path $SdkBin 'makeappx.exe') pack /d $staging /p $msix /o
if($LASTEXITCODE -ne 0){ throw "makeappx pack failed ($LASTEXITCODE)." }

if($SignWithPfx){
	if(-not $PfxPassword){ throw '-SignWithPfx needs -PfxPassword.' }
	$plain = [Net.NetworkCredential]::new('', $PfxPassword).Password
	& (Join-Path $SdkBin 'signtool.exe') sign /fd SHA256 /f $SignWithPfx /p $plain $msix
	if($LASTEXITCODE -ne 0){ throw "signtool failed ($LASTEXITCODE)." }
}

$item = Get-Item $msix
Write-Host ('Built {0} ({1:N0} bytes, version {2}, {3})' -f $item.FullName, $item.Length, $version, $(if($SignWithPfx){'signed'}else{'unsigned'}))
