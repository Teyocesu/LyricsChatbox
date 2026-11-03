param(
    [string]$Svg = "$PSScriptRoot\..\src\LyricsChatbox\Assets\Brand\AppIcon.svg",
    [string]$OutputDirectory = "$PSScriptRoot\..\src\LyricsChatbox\Assets\Brand\Png"
)
# Renders the master SVG to PNG frames with WPF. At 24 px and below the inner line is omitted so the mark stays legible.
$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') { throw 'Run with powershell -STA.' }
Add-Type -AssemblyName PresentationCore, WindowsBase
[xml]$document = Get-Content -LiteralPath $Svg -Raw -Encoding UTF8
$root = $document.DocumentElement
function Get-Brush([string]$Id) {
    $stops = @(($root.defs.linearGradient | Where-Object { $_.id -eq $Id }).stop)
    $brush = [System.Windows.Media.LinearGradientBrush]::new()
    $brush.StartPoint = [System.Windows.Point]::new(0, 0)
    $brush.EndPoint = [System.Windows.Point]::new(1, 1)
    foreach ($stop in $stops) {
        $color = [System.Windows.Media.ColorConverter]::ConvertFromString($stop.'stop-color')
        $brush.GradientStops.Add([System.Windows.Media.GradientStop]::new($color, [double]$stop.offset))
    }
    $brush
}
function Get-Rect($Node) {
    $rect = [System.Windows.Rect]::new([double]$Node.x, [double]$Node.y, [double]$Node.width, [double]$Node.height)
    [System.Windows.Media.RectangleGeometry]::new($rect, [double]$Node.rx, [double]$Node.rx)
}
$tile = Get-Rect $root.rect[0]
$line = Get-Rect $root.rect[1]
$lineBrush = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString($root.rect[1].fill))
$mark = [System.Windows.Media.Geometry]::Parse($root.path.d)
$tileBrush = Get-Brush 't'
$markBrush = Get-Brush 'r'
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $visual = [System.Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    $context.PushTransform([System.Windows.Media.ScaleTransform]::new($size / 48, $size / 48))
    $context.DrawGeometry($tileBrush, $null, $tile)
    $context.DrawGeometry($markBrush, $null, $mark)
    if ($size -gt 24) { $context.DrawGeometry($lineBrush, $null, $line) }
    $context.Pop()
    $context.Close()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [System.IO.File]::Create((Join-Path $OutputDirectory "AppIcon-$size.png"))
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}
