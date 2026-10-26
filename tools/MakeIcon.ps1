# Einmalig: erzeugt assets\arbeitsplan.ico (Blatt mit Zeilen + Haken), PNG-in-ICO. Aufruf: powershell -File tools\MakeIcon.ps1
Add-Type -AssemblyName System.Drawing
$out = Join-Path (Split-Path $PSScriptRoot) 'assets\arbeitsplan.ico'
function Draw([int]$s) {
  $b = New-Object System.Drawing.Bitmap $s,$s,([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b); $g.SmoothingMode = 'AntiAlias'
  $u = $s / 32.0
  $x=5*$u; $y=2*$u; $w=20*$u; $h=27*$u
  $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(40,0,0,0))), ($x+1.2*$u), ($y+1.2*$u), $w, $h)
  $g.FillRectangle([System.Drawing.Brushes]::White, $x,$y,$w,$h)
  $g.DrawRectangle((New-Object System.Drawing.Pen -ArgumentList ([System.Drawing.Color]::FromArgb(110,120,135)),([single][Math]::Max(1,1.1*$u))), $x,$y,$w,$h)
  $lp = New-Object System.Drawing.Pen -ArgumentList ([System.Drawing.Color]::FromArgb(150,155,165)),([single][Math]::Max(1,1.6*$u))
  foreach ($i in 0..4) { $ly=(7+$i*4.2)*$u; $g.DrawLine($lp, (8*$u), $ly, (22*$u), $ly) }
  $cp = New-Object System.Drawing.Pen -ArgumentList ([System.Drawing.Color]::FromArgb(30,140,70)),([single][Math]::Max(2,4*$u))
  $cp.StartCap='Round'; $cp.EndCap='Round'; $cp.LineJoin='Round'
  $g.DrawLines($cp, @((New-Object System.Drawing.PointF (15*$u),(23*$u)), (New-Object System.Drawing.PointF (19*$u),(27.5*$u)), (New-Object System.Drawing.PointF (28*$u),(15*$u))))
  $g.Dispose(); $b
}
$sizes = 16,32,48,256; $png = @()
foreach ($s in $sizes) { $b = Draw $s; $m = New-Object System.IO.MemoryStream; $b.Save($m,[System.Drawing.Imaging.ImageFormat]::Png); $png += ,$m.ToArray() }
$fs = [System.IO.File]::Create($out); $w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$off = 6 + 16*$sizes.Count
for ($i=0;$i -lt $sizes.Count;$i++) { $s=$sizes[$i]; $d = if ($s -ge 256) {0} else {$s}
  $w.Write([byte]$d); $w.Write([byte]$d); $w.Write([byte]0); $w.Write([byte]0); $w.Write([uint16]1); $w.Write([uint16]32)
  $w.Write([uint32]$png[$i].Length); $w.Write([uint32]$off); $off += $png[$i].Length }
foreach ($p in $png) { $w.Write($p) }
$w.Close()

