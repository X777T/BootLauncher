# Generates app.ico: blue gradient rounded square with a white ">" glyph,
# matching the logo in the top-left corner of the main window.
# Usage:  .\tools\make-icon.ps1      (output: BootLauncher\app.ico)
# The ico embeds PNG images at several sizes (Windows Vista or later).

Add-Type -AssemblyName System.Drawing

$sizes = 16, 24, 32, 48, 64, 128, 256
$outPath = Join-Path (Split-Path $PSScriptRoot -Parent) "app.ico"

function New-RoundedPath {
    param([single]$x, [single]$y, [single]$w, [single]$h, [single]$r)
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

# Draw at 4x and downscale, so the edges stay smooth at small sizes.
function New-IconBitmap {
    param([int]$size)
    $ss = 4
    $big = $size * $ss
    $src = New-Object System.Drawing.Bitmap($big, $big, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($src)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = $big * 0.04
    $box = $big - 2 * $pad
    $path = New-RoundedPath $pad $pad $box $box ($box * 0.24)
    $rect = New-Object System.Drawing.RectangleF($pad, $pad, $box, $box)
    $c1 = [System.Drawing.Color]::FromArgb(64, 156, 255)
    $c2 = [System.Drawing.Color]::FromArgb(0, 104, 224)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $c1, $c2, [single]45.0)
    $g.FillPath($brush, $path)

    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, [single]($box * 0.085))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $cx = $big / 2.0
    $cy = $big / 2.0
    $u = $box * 0.17 * 1.15
    $x0 = [single]($cx - $u * 0.8)
    $x1 = [single]($cx + $u * 1.1)
    $y0 = [single]($cy - $u * 1.2)
    $y1 = [single]$cy
    $y2 = [single]($cy + $u * 1.2)
    $pa = New-Object System.Drawing.PointF($x0, $y0)
    $pb = New-Object System.Drawing.PointF($x1, $y1)
    $pc = New-Object System.Drawing.PointF($x0, $y2)
    $pts = New-Object 'System.Drawing.PointF[]' 3
    $pts[0] = $pa
    $pts[1] = $pb
    $pts[2] = $pc
    $g.DrawLines($pen, $pts)
    $g.Dispose()
    $pen.Dispose()
    $brush.Dispose()
    $path.Dispose()

    $dst = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g2 = [System.Drawing.Graphics]::FromImage($dst)
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g2.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g2.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g2.DrawImage($src, 0, 0, $size, $size)
    $g2.Dispose()
    $src.Dispose()
    return $dst
}

$pngs = New-Object System.Collections.ArrayList
foreach ($s in $sizes) {
    $bmp = New-IconBitmap -size $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    [void]$pngs.Add($ms.ToArray())
    $bmp.Dispose()
    $ms.Dispose()
}

$fs = [System.IO.File]::Create($outPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    if ($s -ge 256) { $dim = 0 } else { $dim = $s }
    $bw.Write([byte]$dim)
    $bw.Write([byte]$dim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write([byte[]]$p) }
$bw.Dispose()
$fs.Dispose()
Write-Output ("Wrote {0} ({1} bytes, {2} sizes)" -f $outPath, (Get-Item $outPath).Length, $sizes.Count)
