# Draws the OpenPW application icon (icon_<size>x<size>.png: the Windows launcher, the portable folder's OpenPW.exe and
# the installer) in the style of the main menu: weathered stone lettering, carved with a dark outline and a lit upper
# edge, on a dark leather-brown tile with a bronze rim. Small sizes show only "P", as two letters would not be readable.
#   powershell -ExecutionPolicy Bypass -File packaging\artwork\make-icons.ps1 [-Font NAME] [-OutDir PATH]
param(
	[string]$Font = 'Bernard MT Condensed',
	[string]$OutDir = $PSScriptRoot
)

Add-Type -AssemblyName System.Drawing

$sizes = 16, 24, 32, 48, 64, 128, 256, 512, 1024
if (-not ((New-Object System.Drawing.Text.InstalledFontCollection).Families | Where-Object Name -eq $Font)) {
	Write-Warning "Font $Font is not installed; using Trebuchet MS."
	$Font = 'Trebuchet MS'
}

function Color([int]$r, [int]$g, [int]$b, [int]$a = 255) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
	$path = New-Object System.Drawing.Drawing2D.GraphicsPath
	$d = 2 * $r
	$path.AddArc($x, $y, $d, $d, 180, 90)
	$path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
	$path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
	$path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
	$path.CloseFigure()
	return $path
}

function Shifted($path, [float]$dx, [float]$dy) {
	$copy = $path.Clone()
	$m = New-Object System.Drawing.Drawing2D.Matrix
	$m.Translate($dx, $dy)
	$copy.Transform($m)
	return $copy
}

foreach ($size in $sizes) {
	$bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
	$g = [System.Drawing.Graphics]::FromImage($bitmap)
	$g.SmoothingMode = 'AntiAlias'
	$g.InterpolationMode = 'HighQualityBicubic'
	$g.Clear([System.Drawing.Color]::Transparent)

	# Tile: dark leather brown, lighter towards the top, with a bronze rim.
	$inset = [Math]::Max(0.5, $size * 0.03)
	$tile = New-RoundedRect $inset $inset ($size - 2 * $inset) ($size - 2 * $inset) ($size * 0.18)
	$leather = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
		(New-Object System.Drawing.PointF(0, 0)), (New-Object System.Drawing.PointF(0, $size)), (Color 92 66 40), (Color 34 23 13))
	$g.FillPath($leather, $tile)
	$rim = New-Object System.Drawing.Pen((Color 150 112 62), [Math]::Max(1, $size * 0.03))
	$g.DrawPath($rim, $tile)

	# Lettering as a path, so it can be outlined, lit and filled with a gradient.
	$text = if ($size -le 24) { 'P' } else { 'PW' }
	$family = New-Object System.Drawing.FontFamily($Font)
	$glyphs = New-Object System.Drawing.Drawing2D.GraphicsPath
	$glyphs.AddString($text, $family, [int][System.Drawing.FontStyle]::Bold, 100, (New-Object System.Drawing.PointF(0, 0)),
		[System.Drawing.StringFormat]::GenericTypographic)

	# Scale the glyphs to fill the tile (font metrics differ a lot) and centre them exactly.
	$bounds = $glyphs.GetBounds()
	$fill = if ($text -eq 'P') { 0.62 } else { 0.68 }
	$scale = [Math]::Min($size * $fill / $bounds.Width, $size * 0.56 / $bounds.Height)
	$matrix = New-Object System.Drawing.Drawing2D.Matrix
	$matrix.Translate($size / 2, $size / 2)
	$matrix.Scale($scale, $scale)
	$matrix.Translate(-($bounds.X + $bounds.Width / 2), -($bounds.Y + $bounds.Height / 2))
	$glyphs.Transform($matrix)
	$bounds = $glyphs.GetBounds()

	# Carved: a dark outline and drop shadow, a lit edge above, then the stone face.
	$depth = [Math]::Max(1, $size * 0.025)
	$shadow = New-Object System.Drawing.SolidBrush((Color 10 6 3 170))
	$g.FillPath($shadow, (Shifted $glyphs $depth $depth))
	$outline = New-Object System.Drawing.Pen((Color 22 14 8), [Math]::Max(1, $size * 0.05))
	$outline.LineJoin = 'Round'
	$g.DrawPath($outline, $glyphs)
	$light = New-Object System.Drawing.SolidBrush((Color 236 226 200))
	$g.FillPath($light, (Shifted $glyphs (-$depth / 2) (-$depth / 2)))

	$stone = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
		[System.Drawing.PointF]::new(0, $bounds.Top), [System.Drawing.PointF]::new(0, $bounds.Bottom + 1), (Color 0 0 0), (Color 0 0 0))
	$blend = New-Object System.Drawing.Drawing2D.ColorBlend(3)
	$blend.Colors = @((Color 214 202 172), (Color 172 156 122), (Color 112 96 70))
	$blend.Positions = @(0.0, 0.55, 1.0)
	$stone.InterpolationColors = $blend
	$g.FillPath($stone, $glyphs)

	$file = Join-Path $OutDir "icon_${size}x${size}.png"
	$bitmap.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
	$g.Dispose()
	$bitmap.Dispose()
	Write-Output "wrote $file"
}
