# Generates the MSIX visual assets in packaging\msix\Assets from uyghur.ico.
# Scale 100 only. Square logos are drawn from the largest icon frame (256x256 or smaller);
# the wide logo is the 150 px icon centered on a transparent 310x150 canvas.
param(
	[string]$Icon = (Join-Path $PSScriptRoot '..\uyghur.ico'),
	[string]$OutDir = (Join-Path $PSScriptRoot '..\packaging\msix\Assets')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$Icon = [IO.Path]::GetFullPath($Icon)
$OutDir = [IO.Path]::GetFullPath($OutDir)
[IO.Directory]::CreateDirectory($OutDir) | Out-Null

# Read the ICO directory and take the PNG/BMP frame with the largest size.
$bytes = [IO.File]::ReadAllBytes($Icon)
$count = [BitConverter]::ToUInt16($bytes, 4)
$best = $null
for($i = 0; $i -lt $count; $i++){
	$o = 6 + 16 * $i
	$w = [int]$bytes[$o]; if($w -eq 0){ $w = 256 }
	if($best -eq $null -or $w -gt $best.W){
		$best = @{ W = $w; Size = [BitConverter]::ToUInt32($bytes, $o + 8); Off = [BitConverter]::ToUInt32($bytes, $o + 12) }
	}
}
if($best.W -lt 256){ Write-Warning "Largest icon frame is $($best.W) px; larger assets would be upscaled." }
$ms = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter($ms)
# Re-wrap the chosen frame as a single-image ICO so System.Drawing decodes exactly that frame.
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]1)
$w.Write($bytes, 6, 8); $w.Write([UInt32]$best.Size); $w.Write([UInt32]22)
$w.Write($bytes, [int]$best.Off, [int]$best.Size); $w.Flush()
$ms.Position = 0
$src = (New-Object System.Drawing.Icon($ms)).ToBitmap()
Write-Host "Source frame: $($src.Width)x$($src.Height)"

function New-Canvas([int]$cw, [int]$ch, [int]$iconSize, [string]$name)
{
	$bmp = New-Object System.Drawing.Bitmap($cw, $ch, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
	$g = [System.Drawing.Graphics]::FromImage($bmp)
	$g.Clear([System.Drawing.Color]::Transparent)
	$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
	$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
	$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
	$x = [int](($cw - $iconSize) / 2); $y = [int](($ch - $iconSize) / 2)
	$g.DrawImage($src, (New-Object System.Drawing.Rectangle($x, $y, $iconSize, $iconSize)))
	$g.Dispose()
	$path = Join-Path $OutDir $name
	$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
	$bmp.Dispose()
	Write-Host ("{0,-50} {1}x{2}" -f $name, $cw, $ch)
}

New-Canvas 150 150 150 'Square150x150Logo.png'
New-Canvas 44 44 44 'Square44x44Logo.png'
# Unplated copy for the taskbar, Alt+Tab etc.: https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-manual-conversion
New-Canvas 44 44 44 'Square44x44Logo.targetsize-44_altform-unplated.png'
New-Canvas 50 50 50 'StoreLogo.png'
New-Canvas 71 71 71 'Square71x71Logo.png'
New-Canvas 310 150 150 'Wide310x150Logo.png'
$src.Dispose()