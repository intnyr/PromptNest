#requires -Version 5.1
<#
Generates Assets\AppIcon.ico with multi-resolution PromptNest brand mark.
Background: indigo accent #5865F2. Glyph: white "PN" monogram.
Sizes: 16, 32, 48, 64, 128, 256. PNG-compressed entries (Vista+ ICO).
#>
[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\..\src\PromptNest.App\Assets\AppIcon.ico')
)

Add-Type -AssemblyName System.Drawing

$sizes = 16, 32, 48, 64, 128, 256
$bgColor = [System.Drawing.ColorTranslator]::FromHtml('#5865F2')
$fgColor = [System.Drawing.Color]::White

function New-IconPng {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    # rounded square
    $radius = [Math]::Max(2, [int]($Size * 0.22))
    $rect = New-Object System.Drawing.RectangleF(0, 0, $Size, $Size)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $bgBrush = New-Object System.Drawing.SolidBrush($bgColor)
    $g.FillPath($bgBrush, $path)
    $bgBrush.Dispose()

    # subtle inner highlight on bigger sizes
    if ($Size -ge 48) {
        $hi = [System.Drawing.Color]::FromArgb(28, 255, 255, 255)
        $hiBrush = New-Object System.Drawing.SolidBrush($hi)
        $hiRect = New-Object System.Drawing.RectangleF(0, 0, $Size, [int]($Size * 0.5))
        $hiPath = New-Object System.Drawing.Drawing2D.GraphicsPath
        $hiPath.AddArc($hiRect.X, $hiRect.Y, $d, $d, 180, 90)
        $hiPath.AddArc($hiRect.Right - $d, $hiRect.Y, $d, $d, 270, 90)
        $hiPath.AddLine($hiRect.Right, $hiRect.Bottom, $hiRect.X, $hiRect.Bottom)
        $hiPath.CloseFigure()
        $g.FillPath($hiBrush, $hiPath)
        $hiBrush.Dispose()
        $hiPath.Dispose()
    }

    # "PN" monogram
    $glyph = 'PN'
    $fontSize = [single]($Size * 0.46)
    $family = $null
    foreach ($name in 'Segoe UI Variable Display Semibold','Segoe UI Semibold','Segoe UI','Arial') {
        try {
            $f = New-Object System.Drawing.FontFamily($name)
            $family = $f
            break
        } catch { }
    }
    if (-not $family) { $family = [System.Drawing.FontFamily]::GenericSansSerif }

    $font = New-Object System.Drawing.Font($family, $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center

    # measure & nudge baseline up slightly so visual center matches geometric center
    $rectF = New-Object System.Drawing.RectangleF(0, [single](-$Size * 0.04), [single]$Size, [single]$Size)
    $fgBrush = New-Object System.Drawing.SolidBrush($fgColor)
    $g.DrawString($glyph, $font, $fgBrush, $rectF, $sf)
    $fgBrush.Dispose()
    $font.Dispose()
    $sf.Dispose()
    $path.Dispose()
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()
}

$pngs = @{}
foreach ($s in $sizes) { $pngs[$s] = New-IconPng -Size $s }

# Build ICO container
$icoDir = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($icoDir)
$bw.Write([uint16]0)              # reserved
$bw.Write([uint16]1)              # type = icon
$bw.Write([uint16]$sizes.Count)   # image count

$headerSize = 6 + (16 * $sizes.Count)
$offset = $headerSize
foreach ($s in $sizes) {
    $bytes = $pngs[$s]
    $w = if ($s -ge 256) { 0 } else { $s }
    $h = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$w)            # width
    $bw.Write([byte]$h)            # height
    $bw.Write([byte]0)             # palette colors
    $bw.Write([byte]0)             # reserved
    $bw.Write([uint16]1)           # color planes
    $bw.Write([uint16]32)          # bits per pixel
    $bw.Write([uint32]$bytes.Length)
    $bw.Write([uint32]$offset)
    $offset += $bytes.Length
}
foreach ($s in $sizes) { $bw.Write($pngs[$s]) }
$bw.Flush()

$dir = Split-Path -Parent $OutputPath
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
[System.IO.File]::WriteAllBytes($OutputPath, $icoDir.ToArray())
$bw.Dispose()

Write-Host "Wrote $OutputPath ($([Math]::Round((Get-Item $OutputPath).Length/1KB,1)) KB)"
