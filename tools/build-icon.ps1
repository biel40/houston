# Renders ui/src/assets/saturn-v.svg into src/Houston.App/Assets/houston.ico.
# Uses WPF, which ships with Windows, so no extra dependency is needed. Run after editing the SVG:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-icon.ps1
# Supported SVG subset: <path d fill opacity> and <g transform="rotate(angle cx cy)">.
param(
    [string] $Svg = (Join-Path $PSScriptRoot '..\ui\src\assets\saturn-v.svg'),
    [string] $Out = (Join-Path $PSScriptRoot '..\src\Houston.App\Assets\houston.ico'),
    [int[]] $Sizes = @(16, 20, 24, 32, 40, 48, 64, 256)
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase

[xml] $doc = Get-Content -Raw -Path $Svg
$viewBox = ($doc.svg.viewBox -split '\s+') | ForEach-Object { [double]$_ }
$viewSize = $viewBox[2]

function Render-Node($ctx, $node) {
    foreach ($child in $node.ChildNodes) {
        switch ($child.LocalName) {
            'path' {
                $geometry = [System.Windows.Media.Geometry]::Parse($child.d)
                $brush = [System.Windows.Media.SolidColorBrush]([System.Windows.Media.ColorConverter]::ConvertFromString($child.fill))
                $opacity = if ($child.opacity) { [double]$child.opacity } else { 1.0 }
                $ctx.PushOpacity($opacity)
                $ctx.DrawGeometry($brush, $null, $geometry)
                $ctx.Pop()
            }
            'g' {
                $pushed = $false
                if ($child.transform -match 'rotate\(\s*([-\d.]+)[\s,]+([-\d.]+)[\s,]+([-\d.]+)\s*\)') {
                    $ctx.PushTransform([System.Windows.Media.RotateTransform]::new([double]$Matches[1], [double]$Matches[2], [double]$Matches[3]))
                    $pushed = $true
                }
                Render-Node $ctx $child
                if ($pushed) { $ctx.Pop() }
            }
        }
    }
}

$images = foreach ($size in $Sizes) {
    $visual = [System.Windows.Media.DrawingVisual]::new()
    $ctx = $visual.RenderOpen()
    $ctx.PushTransform([System.Windows.Media.ScaleTransform]::new($size / $viewSize, $size / $viewSize))
    Render-Node $ctx $doc.svg
    $ctx.Pop()
    $ctx.Close()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)

    if ($size -ge 256) {
        # 256 px is stored as PNG to keep the file small; every consumer since Vista reads it.
        $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
        $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
        $stream = [System.IO.MemoryStream]::new()
        $encoder.Save($stream)
        [pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() }
        continue
    }

    # Smaller sizes as classic 32-bit DIBs: some readers (e.g. .NET's Icon) only accept PNG at 256 px.
    $straight = [System.Windows.Media.Imaging.FormatConvertedBitmap]::new($bitmap, [System.Windows.Media.PixelFormats]::Bgra32, $null, 0)
    $stride = $size * 4
    $pixels = [byte[]]::new($stride * $size)
    $straight.CopyPixels($pixels, $stride, 0)
    $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
    $stream = [System.IO.MemoryStream]::new()
    $dib = [System.IO.BinaryWriter]::new($stream)
    # BITMAPINFOHEADER; the height counts the XOR bitmap plus the AND mask.
    $dib.Write([uint32]40); $dib.Write([int32]$size); $dib.Write([int32]($size * 2))
    $dib.Write([uint16]1); $dib.Write([uint16]32); $dib.Write([uint32]0)
    $dib.Write([uint32]($stride * $size + $maskStride * $size))
    $dib.Write([int32]0); $dib.Write([int32]0); $dib.Write([uint32]0); $dib.Write([uint32]0)
    for ($row = $size - 1; $row -ge 0; $row--) { $dib.Write($pixels, $row * $stride, $stride) }
    # All-zero AND mask: transparency comes from the alpha channel.
    $dib.Write([byte[]]::new($maskStride * $size))
    $dib.Flush()
    [pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() }
}

# ICO layout: ICONDIR, one 16-byte ICONDIRENTRY per image, then the PNG payloads.
New-Item -ItemType Directory -Force -Path (Split-Path $Out) | Out-Null
$file = [System.IO.BinaryWriter]::new([System.IO.File]::Create($Out))
try {
    $file.Write([uint16]0); $file.Write([uint16]1); $file.Write([uint16]$images.Count)
    $offset = 6 + 16 * $images.Count
    foreach ($image in $images) {
        $dimension = if ($image.Size -ge 256) { 0 } else { $image.Size }
        $file.Write([byte]$dimension); $file.Write([byte]$dimension)
        $file.Write([byte]0); $file.Write([byte]0)
        $file.Write([uint16]1); $file.Write([uint16]32)
        $file.Write([uint32]$image.Bytes.Length); $file.Write([uint32]$offset)
        $offset += $image.Bytes.Length
    }
    foreach ($image in $images) { $file.Write($image.Bytes) }
}
finally { $file.Dispose() }

"Wrote $Out ($($images.Count) sizes: $($Sizes -join ', '))"
