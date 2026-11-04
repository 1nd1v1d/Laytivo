$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\Laytivo.App\Assets'
$sourcePath = Join-Path $assetDirectory 'Laytivo-source.png'
$source = [System.Drawing.Image]::FromFile($sourcePath)
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = [Collections.Generic.List[object]]::new()
try {
    foreach ($size in $sizes) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $png = [IO.MemoryStream]::new()
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.DrawImage($source, [System.Drawing.Rectangle]::new(0, 0, $size, $size))
            $bitmap.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames.Add([pscustomobject]@{ Size = $size; Data = $png.ToArray() })
            if ($size -eq 256) { [IO.File]::WriteAllBytes((Join-Path $assetDirectory 'Laytivo.png'), $png.ToArray()) }
        }
        finally { $png.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
}
finally { $source.Dispose() }

$file = [IO.File]::Create((Join-Path $assetDirectory 'Laytivo.ico'))
$writer = [IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Data.Length)
        $writer.Write([uint32]$offset)
        $offset += $frame.Data.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Data) }
}
finally { $writer.Dispose() }
Write-Output "Laytivo.ico erstellt: $($sizes -join ', ') Pixel; Laytivo.png: 256 Pixel."
