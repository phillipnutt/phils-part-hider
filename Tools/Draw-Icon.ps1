# Draws the toolbar icon (an eye with a slash through it), in PIP orange (#ffa62b) on transparent,
# 38 x 38 like the stock toolbar icons. Run from anywhere:
#   powershell -ExecutionPolicy Bypass -File Tools\Draw-Icon.ps1
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot '..\GameData\PhilsPartHider\Textures\icon.png'
$size = 38
$bmp = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)
$ink = [System.Drawing.Color]::FromArgb(255, 255, 166, 43)
$pen = New-Object System.Drawing.Pen $ink, 2.6

# Eye outline: two arcs meeting at the corners.
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$path.AddBezier(4, 19, 11, 8, 27, 8, 34, 19)
$path.AddBezier(34, 19, 27, 30, 11, 30, 4, 19)
$g.DrawPath($pen, $path)

# Pupil.
$brush = New-Object System.Drawing.SolidBrush $ink
$g.FillEllipse($brush, 13.5, 13.5, 11, 11)

# Slash, with a transparent gap on each side so it reads clearly.
$gap = New-Object System.Drawing.Pen ([System.Drawing.Color]::Transparent), 5
$g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
$g.DrawLine($gap, 7, 33, 31, 5)
$g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
$slash = New-Object System.Drawing.Pen $ink, 2.6
$slash.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$slash.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLine($slash, 7, 33, 31, 5)

$g.Dispose()
$bmp.Save([System.IO.Path]::GetFullPath($out), [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
"Wrote $out"

