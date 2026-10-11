# Regenerates every icon from art/xkcdviewer-logo.jpg (the owner's own pencil drawing). Run from the repo root on
# Windows (PowerShell 7):
#   pwsh art/make-icons.ps1
#
# The drawing is pencil on paper. The paper's faint texture would show as a grey box against a white key, so the
# paper is made transparent and the pencil turned into black ink of matching strength, then the logo is put on
# clean white "paper" again in every output (line art alone vanishes on Options+'s black background).
#
#   src/xkcdviewerPlugin/package/metadata/Icon256x256.png
#       Package icon. SDK rules: 256x256 PNG, graphic within the centred 192x192, transparent around it, legible on
#       black and on white.
#   src/xkcdviewerPlugin/Resources/Images/FolderButton116.png
#   src/xkcdviewerPlugin/Resources/Images/FolderButton80.png
#       The key that opens the folder, at the two key sizes seen on hardware (PluginImageSize.Width116 and Width90).
#       Full white keys.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $PSScriptRoot 'xkcdviewer-logo.jpg'

# Pencil darkness (0 = paper white, 255 = black) below this is paper texture; at or above $SolidInk it's full ink.
# Between, the ink fades in, which keeps the pencil's soft edges.
$PaperThreshold = 40
$SolidInk = 150

# Working resolution for the ink conversion: plenty for a 256 px icon, and a quarter of the pixels to process.
$WorkSize = 512

function ConvertTo-Ink([String] $path)
{
    $original = [System.Drawing.Image]::FromFile($path)
    $work = [System.Drawing.Bitmap]::new($WorkSize, $WorkSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($work)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($original, 0, 0, $WorkSize, $WorkSize)
    $g.Dispose()
    $original.Dispose()

    $rect = [System.Drawing.Rectangle]::new(0, 0, $WorkSize, $WorkSize)
    $data = $work.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite, $work.PixelFormat)
    $bytes = [Byte[]]::new($data.Stride * $WorkSize)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)

    $minX = $WorkSize; $minY = $WorkSize; $maxX = -1; $maxY = -1
    for ($y = 0; $y -lt $WorkSize; $y++)
    {
        $row = $y * $data.Stride
        for ($x = 0; $x -lt $WorkSize; $x++)
        {
            $i = $row + 4 * $x
            $darkness = 255 - [Int32](($bytes[$i] + $bytes[$i + 1] + $bytes[$i + 2]) / 3)
            $alpha = if ($darkness -lt $PaperThreshold) { 0 } elseif ($darkness -ge $SolidInk) { 255 } else { [Int32](255 * ($darkness - $PaperThreshold) / ($SolidInk - $PaperThreshold)) }
            $bytes[$i] = 0; $bytes[$i + 1] = 0; $bytes[$i + 2] = 0; $bytes[$i + 3] = [Byte]$alpha
            if ($alpha -gt 0)
            {
                if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }

    [System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
    $work.UnlockBits($data)

    # Crop to the drawing plus a small margin, so it can be drawn as large as each output allows.
    $margin = 4
    $x0 = [Math]::Max(0, $minX - $margin); $y0 = [Math]::Max(0, $minY - $margin)
    $x1 = [Math]::Min($WorkSize - 1, $maxX + $margin); $y1 = [Math]::Min($WorkSize - 1, $maxY + $margin)
    return @{ Image = $work; Crop = [System.Drawing.Rectangle]::new($x0, $y0, $x1 - $x0 + 1, $y1 - $y0 + 1) }
}

function New-Icon($ink, [String] $path, [Int32] $size, [System.Drawing.Rectangle] $paper, [Int32] $cornerRadius, [Int32] $padding)
{
    $crop = $ink.Crop
    $canvas = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($canvas)
    try
    {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)

        $white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
        if ($cornerRadius -gt 0)
        {
            $d = $cornerRadius * 2
            $shape = [System.Drawing.Drawing2D.GraphicsPath]::new()
            $shape.AddArc($paper.X, $paper.Y, $d, $d, 180, 90)
            $shape.AddArc($paper.Right - $d, $paper.Y, $d, $d, 270, 90)
            $shape.AddArc($paper.Right - $d, $paper.Bottom - $d, $d, $d, 0, 90)
            $shape.AddArc($paper.X, $paper.Bottom - $d, $d, $d, 90, 90)
            $shape.CloseFigure()
            $g.FillPath($white, $shape)
        }
        else
        {
            $g.FillRectangle($white, $paper)
        }

        # Fit the cropped drawing inside the paper, keeping its aspect ratio, centred.
        $boxWidth = $paper.Width - 2 * $padding
        $boxHeight = $paper.Height - 2 * $padding
        $scale = [Math]::Min($boxWidth / $crop.Width, $boxHeight / $crop.Height)
        $w = [Int32][Math]::Round($crop.Width * $scale)
        $h = [Int32][Math]::Round($crop.Height * $scale)
        $x = $paper.X + [Int32](($paper.Width - $w) / 2)
        $y = $paper.Y + [Int32](($paper.Height - $h) / 2)

        # TileFlipXY stops the bicubic filter pulling transparent pixels in at the drawing's edges.
        $attributes = [System.Drawing.Imaging.ImageAttributes]::new()
        $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
        $g.DrawImage($ink.Image, [System.Drawing.Rectangle]::new($x, $y, $w, $h), $crop.X, $crop.Y, $crop.Width, $crop.Height, [System.Drawing.GraphicsUnit]::Pixel, $attributes)

        $null = New-Item -ItemType Directory -Force (Split-Path -Parent $path)
        $canvas.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        "Wrote $path ($size x $size, logo $w x $h)"
    }
    finally
    {
        $g.Dispose()
        $canvas.Dispose()
    }
}

$ink = ConvertTo-Ink $source
try
{
    New-Icon $ink (Join-Path $root 'src/xkcdviewerPlugin/package/metadata/Icon256x256.png') 256 ([System.Drawing.Rectangle]::new(32, 32, 192, 192)) 28 10
    New-Icon $ink (Join-Path $root 'src/xkcdviewerPlugin/Resources/Images/FolderButton116.png') 116 ([System.Drawing.Rectangle]::new(0, 0, 116, 116)) 0 4
    New-Icon $ink (Join-Path $root 'src/xkcdviewerPlugin/Resources/Images/FolderButton80.png') 80 ([System.Drawing.Rectangle]::new(0, 0, 80, 80)) 0 3
}
finally
{
    $ink.Image.Dispose()
}
