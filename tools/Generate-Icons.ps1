Add-Type -AssemblyName System.Drawing

$repository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$androidIconPath = Join-Path $repository 'Timer.Android\Icon.png'
$desktopIconPath = Join-Path $repository 'Timer\Assets\stilltime.ico'

function New-RoundedRectangle([int]$size, [int]$radius) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $radius * 2
    $path.AddArc(0, 0, $diameter, $diameter, 180, 90)
    $path.AddArc($size - $diameter, 0, $diameter, $diameter, 270, 90)
    $path.AddArc($size - $diameter, $size - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc(0, $size - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconPng([int]$size) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.ScaleTransform($size / 512.0, $size / 512.0)

    $background = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(185, 107, 80))
    $cream = [System.Drawing.Color]::FromArgb(255, 248, 239)
    $ring = [System.Drawing.Pen]::new($cream, 20)
    $ring.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $ring.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $hands = [System.Drawing.Pen]::new($cream, 22)
    $hands.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $hands.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $center = [System.Drawing.SolidBrush]::new($cream)
    $shape = New-RoundedRectangle 512 112

    $graphics.FillPath($background, $shape)
    $graphics.DrawEllipse($ring, 113, 113, 286, 286)
    $graphics.DrawLine($hands, 256, 256, 256, 171)
    $graphics.DrawLine($hands, 256, 256, 319, 292)
    $graphics.FillEllipse($center, 244, 244, 24, 24)

    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $stream.ToArray()

    $stream.Dispose()
    $shape.Dispose()
    $center.Dispose()
    $hands.Dispose()
    $ring.Dispose()
    $background.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
    return ,$bytes
}

[System.IO.File]::WriteAllBytes($androidIconPath, (New-IconPng 512))

$sizes = @(16, 32, 48, 64, 256)
$images = @($sizes | ForEach-Object { New-IconPng $_ })
$stream = [System.IO.File]::Create($desktopIconPath)
$writer = [System.IO.BinaryWriter]::new($stream)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)

$offset = 6 + 16 * $sizes.Count
for ($index = 0; $index -lt $sizes.Count; $index++) {
    $size = $sizes[$index]
    $writer.Write([byte]($size % 256))
    $writer.Write([byte]($size % 256))
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$images[$index].Length)
    $writer.Write([uint32]$offset)
    $offset += $images[$index].Length
}

foreach ($bytes in $images) {
    $writer.Write([byte[]]$bytes)
}

$writer.Dispose()
