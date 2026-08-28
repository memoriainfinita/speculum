# Generates speculum.ico: an antique oval mirror, with a different drawing per size.
#
# The ornament (beading on the frame, gradients) only shows at the large sizes.
# At 16px nothing survives but the silhouette: ring, glass and handle. That is the
# reason an .ico is a container of several images and not one scaled image.
#
# Run with Windows PowerShell 5.1, not with pwsh 7: System.Drawing is not included
# in PowerShell 7.
#   powershell.exe -ExecutionPolicy Bypass -File tools\make-icon.ps1

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root 'speculum.ico'
$sizes = @(16, 32, 48, 256)

# Mid-tone bronze: it has to read on both a light and a dark taskbar.
$bronzeLight = [System.Drawing.Color]::FromArgb(255, 217, 164,  65)
$bronzeMid   = [System.Drawing.Color]::FromArgb(255, 169, 118,  42)
$bronzeDark  = [System.Drawing.Color]::FromArgb(255, 110,  74,  24)
$outline     = [System.Drawing.Color]::FromArgb(255,  58,  42,  16)
$glassTop    = [System.Drawing.Color]::FromArgb(255,  46,  54,  62)
$glassBottom = [System.Drawing.Color]::FromArgb(255,  14,  19,  25)

function New-Mirror([int]$S) {
    $bmp = New-Object System.Drawing.Bitmap($S, $S, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $detailed = $S -ge 48     # beading on the frame
    $smooth   = $S -ge 32     # gradients

    # Geometry. The oval is taller than it is wide: otherwise it reads as a circle, and
    # with the handle below the whole thing looks like a frying pan or a magnifier.
    if ($smooth) {
        $headW = 0.60 * $S; $headH = 0.74 * $S; $headY = 0.02 * $S
        $thick = [Math]::Max(1.6, 0.075 * $S)
        $handleW = 0.095 * $S; $handleY = 0.70 * $S; $handleH = 0.22 * $S
        $knobR = 0.065 * $S
    } else {
        # At 16px margins are a luxury: the silhouette has to eat the canvas or the
        # mirror ends up a 10 pixel smudge lost in the middle.
        $headW = 0.72 * $S; $headH = 0.74 * $S; $headY = 0.0
        $thick = 2.0
        $handleW = 0.20 * $S; $handleY = 0.70 * $S; $handleH = 0.17 * $S
        $knobR = 0.105 * $S
    }
    $headX = ($S - $headW) / 2.0
    $handleX = ($S - $handleW) / 2.0

    $head = New-Object System.Drawing.RectangleF($headX, $headY, $headW, $headH)
    $glass = New-Object System.Drawing.RectangleF(
        ($headX + $thick), ($headY + $thick),
        ($headW - 2 * $thick), ($headH - 2 * $thick))

    # --- Handle and knob, behind the head ---
    $handle = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = $handleW / 2.0
    $handle.AddArc($handleX, ($handleY + $handleH - 2 * $r), $handleW, (2 * $r), 0, 180)
    $handle.AddArc($handleX, $handleY, $handleW, (2 * $r), 180, 180)
    $handle.CloseFigure()
    $knob = New-Object System.Drawing.Drawing2D.GraphicsPath
    $knob.AddEllipse(($S / 2.0 - $knobR), ($handleY + $handleH - $knobR), (2 * $knobR), (2 * $knobR))

    if ($smooth) {
        $bHandle = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            (New-Object System.Drawing.PointF([float]$handleX, [float]0)),
            (New-Object System.Drawing.PointF([float]($handleX + $handleW), [float]0)),
            $bronzeLight, $bronzeDark)
    } else {
        # At 16px the dark outline eats the ring from both sides: if the bronze is not
        # the light one, all that is left of the frame is a brown line.
        $bHandle = New-Object System.Drawing.SolidBrush($bronzeLight)
    }
    $g.FillPath($bHandle, $handle)
    $g.FillPath($bHandle, $knob)
    $bHandle.Dispose()

    # --- Oval frame ---
    if ($smooth) {
        $bFrame = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            (New-Object System.Drawing.PointF([float]$headX, [float]$headY)),
            (New-Object System.Drawing.PointF([float]($headX + $headW), [float]($headY + $headH))),
            $bronzeLight, $bronzeDark)
    } else {
        $bFrame = New-Object System.Drawing.SolidBrush($bronzeLight)
    }
    $g.FillEllipse($bFrame, $head)
    $bFrame.Dispose()

    # --- Glass ---
    if ($smooth) {
        $bGlass = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            (New-Object System.Drawing.PointF([float]0, [float]$glass.Top)),
            (New-Object System.Drawing.PointF([float]0, [float]$glass.Bottom)),
            $glassTop, $glassBottom)
    } else {
        $bGlass = New-Object System.Drawing.SolidBrush($glassBottom)
    }
    $g.FillEllipse($bGlass, $glass)
    $bGlass.Dispose()

    # --- Diagonal reflection across the glass ---
    $clip = New-Object System.Drawing.Drawing2D.GraphicsPath
    $clip.AddEllipse($glass)
    $state = $g.Save()
    $g.SetClip($clip)
    $cx = $glass.Left + $glass.Width / 2.0
    $cy = $glass.Top + $glass.Height / 2.0
    $g.TranslateTransform([float]$cx, [float]$cy)
    $g.RotateTransform(-38)
    # Two narrow diagonal bands, not one big smear: that is what reads as glass.
    if ($detailed) { $alpha = 66 } elseif ($smooth) { $alpha = 58 } else { $alpha = 50 }
    $bShine = New-Object System.Drawing.SolidBrush(
        [System.Drawing.Color]::FromArgb($alpha, 255, 255, 255))
    $g.FillEllipse($bShine,
        [float](-0.46 * $glass.Width), [float](-0.95 * $glass.Height),
        [float](0.26 * $glass.Width), [float](1.90 * $glass.Height))
    if ($smooth) {
        $bShine2 = New-Object System.Drawing.SolidBrush(
            [System.Drawing.Color]::FromArgb([int]($alpha * 0.55), 255, 255, 255))
        $g.FillEllipse($bShine2,
            [float](-0.10 * $glass.Width), [float](-0.95 * $glass.Height),
            [float](0.12 * $glass.Width), [float](1.90 * $glass.Height))
        $bShine2.Dispose()
    }
    $bShine.Dispose()
    $g.Restore($state)
    $clip.Dispose()

    # --- Beading on the frame, large sizes only ---
    if ($detailed) {
        $a = ($headW - $thick) / 2.0
        $b = ($headH - $thick) / 2.0
        $mcx = $headX + $headW / 2.0
        $mcy = $headY + $headH / 2.0
        $rp = $thick * 0.28
        $bBead = New-Object System.Drawing.SolidBrush($bronzeLight)
        $pBead = New-Object System.Drawing.Pen($outline, [float]($S / 256.0))
        for ($i = 0; $i -lt 20; $i++) {
            $t = 2 * [Math]::PI * $i / 20.0
            $px = $mcx + $a * [Math]::Cos($t)
            $py = $mcy + $b * [Math]::Sin($t)
            $g.FillEllipse($bBead, [float]($px - $rp), [float]($py - $rp), [float](2 * $rp), [float](2 * $rp))
            $g.DrawEllipse($pBead, [float]($px - $rp), [float]($py - $rp), [float](2 * $rp), [float](2 * $rp))
        }
        $bBead.Dispose(); $pBead.Dispose()
    }

    # --- Outlines: they define the silhouette against a light background ---
    $width = [Math]::Max(1.0, $S / 128.0)
    $pOutline = New-Object System.Drawing.Pen($outline, [float]$width)
    $g.DrawEllipse($pOutline, $head)
    # The inner outline only fits once the frame is more than a couple of pixels wide.
    if ($smooth) { $g.DrawEllipse($pOutline, $glass) }
    $g.DrawPath($pOutline, $handle)
    $g.DrawPath($pOutline, $knob)
    $pOutline.Dispose()

    $handle.Dispose(); $knob.Dispose(); $g.Dispose()
    return $bmp
}

function Get-PngBytes([System.Drawing.Bitmap]$bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray(); $ms.Dispose(); return $bytes
}

# 32 bit DIB for the .ico: header with a doubled biHeight (XOR + AND mask), pixels
# bottom-up, and the AND mask zeroed because transparency lives in the alpha channel.
function Get-DibBytes([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width; $h = $bmp.Height
    $data = $bmp.LockBits(
        (New-Object System.Drawing.Rectangle(0, 0, $w, $h)),
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $data.Stride
    $raw = New-Object byte[] ($stride * $h)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $raw, 0, $raw.Length)
    $bmp.UnlockBits($data)

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    $bw.Write([uint32]40); $bw.Write([int32]$w); $bw.Write([int32](2 * $h))
    $bw.Write([uint16]1);  $bw.Write([uint16]32); $bw.Write([uint32]0)
    $bw.Write([uint32]($w * $h * 4)); $bw.Write([int32]0); $bw.Write([int32]0)
    $bw.Write([uint32]0);  $bw.Write([uint32]0)
    for ($y = $h - 1; $y -ge 0; $y--) { $bw.Write($raw, ($y * $stride), ($w * 4)) }
    $maskBytes = [int]([Math]::Floor(($w + 31) / 32)) * 4
    $bw.Write((New-Object byte[] ($maskBytes * $h)))
    $bw.Flush(); $bytes = $ms.ToArray(); $bw.Dispose(); $ms.Dispose()
    return $bytes
}

$entries = @()
foreach ($S in $sizes) {
    $bmp = New-Mirror $S
    # The previews are working material and stay out of the repo; the 256 one is also
    # saved into docs/ because the README shows it.
    $preview = Join-Path $PSScriptRoot ("preview-{0}.png" -f $S)
    $bmp.Save($preview, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($S -ge 256) {
        $bmp.Save((Join-Path $root 'docs\icon.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    # PNG for 256 (Vista format, saves 270 KB of bitmap); DIB for the rest.
    if ($S -ge 256) { $data = Get-PngBytes $bmp } else { $data = Get-DibBytes $bmp }
    $entries += [pscustomobject]@{ Size = $S; Data = $data }
    $bmp.Dispose()
    "{0,4}px  {1,7:N0} bytes  ->  {2}" -f $S, $data.Length, (Split-Path -Leaf $preview)
}

$fs = New-Object System.IO.FileStream($target, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$entries.Count)
$offset = 6 + 16 * $entries.Count
foreach ($e in $entries) {
    if ($e.Size -ge 256) { $d = 0 } else { $d = $e.Size }
    $bw.Write([byte]$d); $bw.Write([byte]$d); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$e.Data.Length); $bw.Write([uint32]$offset)
    $offset += $e.Data.Length
}
# With the single argument overload, PowerShell coerces the PSObject-wrapped array and
# writes a single byte. It has to be typed and use the three argument one.
foreach ($e in $entries) {
    $block = [byte[]]$e.Data
    $bw.Write($block, 0, $block.Length)
}
$bw.Flush(); $bw.Dispose(); $fs.Dispose()

"speculum.ico: {0:N0} bytes, {1} sizes" -f (Get-Item $target).Length, $entries.Count
