$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$destination = Join-Path $PSScriptRoot '../KuzushiBeacon/images/icon.png'
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($destination))) | Out-Null
$bitmap = [Drawing.Bitmap]::new(512, 512)
$g = [Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([Drawing.Color]::FromArgb(19, 24, 34))
$gold = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255, 188, 49))
$white = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(236, 242, 249))
$line = [Drawing.Pen]::new([Drawing.Color]::FromArgb(255, 99, 46), 8)
$ring = [Drawing.Pen]::new([Drawing.Color]::FromArgb(58, 71, 87), 5)
try {
    $g.DrawEllipse($ring, 42, 42, 428, 428)
    $g.DrawLine($line, 126, 356, 345, 252)
    $g.FillEllipse($white, 100, 330, 52, 52)
    $g.DrawEllipse($line, 314, 221, 62, 62)
    $g.FillPolygon($gold, [Drawing.PointF[]]@(
        [Drawing.PointF]::new(281, 121), [Drawing.PointF]::new(409, 121), [Drawing.PointF]::new(345, 200)))
    $bitmap.Save([IO.Path]::GetFullPath($destination), [Drawing.Imaging.ImageFormat]::Png)
} finally {
    $ring.Dispose(); $line.Dispose(); $gold.Dispose(); $white.Dispose(); $g.Dispose(); $bitmap.Dispose()
}
