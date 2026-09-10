param([Parameter(Mandatory=$true)][string]$Source, [string]$OutputDirectory = "$PSScriptRoot\..\src\LyricsChatbox\Assets")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$sourceImage = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $Source).Path)
try {
    if ($sourceImage.Width -ne $sourceImage.Height) { throw 'Use a square source image to preserve its proportions.' }
    $frames = @()
    foreach ($size in @(16,24,32,48,64,128,256)) {
        $bitmap = [System.Drawing.Bitmap]::new($size,$size,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $stream = [System.IO.MemoryStream]::new()
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($sourceImage,[System.Drawing.Rectangle]::new(0,0,$size,$size))
            $bitmap.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png)
            $frames += ,($stream.ToArray())
            if ($size -eq 256) { [System.IO.File]::WriteAllBytes((Join-Path $OutputDirectory 'AppIcon.png'),$stream.ToArray()) }
        } finally { $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
    $file = [System.IO.File]::Create((Join-Path $OutputDirectory 'AppIcon.ico'))
    $writer = [System.IO.BinaryWriter]::new($file)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
        $offset = 6 + 16 * $frames.Count
        $sizes = @(16,24,32,48,64,128,256)
        for ($i=0; $i -lt $frames.Count; $i++) {
            $dimension = if ($sizes[$i] -eq 256) {0} else {$sizes[$i]}
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
            $offset += $frames[$i].Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    } finally { $writer.Dispose() }
} finally { $sourceImage.Dispose() }
