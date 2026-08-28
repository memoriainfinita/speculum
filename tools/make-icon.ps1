# Genera speculum.ico: un espejo ovalado antiguo, con un dibujo distinto por tamano.
#
# El ornamento (perlado del marco, degradados) solo aparece en los tamanos grandes.
# A 16px sobrevive la silueta y nada mas: anillo, cristal y mango. Es la razon de que
# un .ico sea un contenedor de varias imagenes y no una sola imagen escalada.
#
# Ejecutar con Windows PowerShell 5.1, no con pwsh 7: System.Drawing no viene incluido
# en PowerShell 7.
#   powershell.exe -ExecutionPolicy Bypass -File tools\make-icon.ps1

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$destino = Join-Path $raiz 'speculum.ico'
$tamanos = @(16, 32, 48, 256)

# Bronce de tono medio: tiene que leerse sobre barra de tareas clara y oscura.
$bronceClaro  = [System.Drawing.Color]::FromArgb(255, 217, 164,  65)
$bronceMedio  = [System.Drawing.Color]::FromArgb(255, 169, 118,  42)
$bronceOscuro = [System.Drawing.Color]::FromArgb(255, 110,  74,  24)
$borde        = [System.Drawing.Color]::FromArgb(255,  58,  42,  16)
$cristalAlto  = [System.Drawing.Color]::FromArgb(255,  46,  54,  62)
$cristalBajo  = [System.Drawing.Color]::FromArgb(255,  14,  19,  25)

function New-Espejo([int]$S) {
    $bmp = New-Object System.Drawing.Bitmap($S, $S, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $detalle = $S -ge 48     # perlado del marco
    $suave   = $S -ge 32     # degradados

    # Geometria. El ovalo va mas alto que ancho: si no, se lee como un circulo, y con
    # el mango debajo el conjunto parece una sarten o una lupa.
    if ($suave) {
        $cabezaW = 0.60 * $S; $cabezaH = 0.74 * $S; $cabezaY = 0.02 * $S
        $grosor  = [Math]::Max(1.6, 0.075 * $S)
        $mangoW  = 0.095 * $S; $mangoY = 0.70 * $S; $mangoH = 0.22 * $S
        $pomoR   = 0.065 * $S
    } else {
        # A 16px los margenes son un lujo: la silueta tiene que comerse el lienzo o
        # el espejo queda como una mancha de 10 pixeles perdida en el centro.
        $cabezaW = 0.72 * $S; $cabezaH = 0.74 * $S; $cabezaY = 0.0
        $grosor  = 2.0
        $mangoW  = 0.20 * $S; $mangoY = 0.70 * $S; $mangoH = 0.17 * $S
        $pomoR   = 0.105 * $S
    }
    $cabezaX = ($S - $cabezaW) / 2.0
    $mangoX  = ($S - $mangoW) / 2.0

    $cabeza = New-Object System.Drawing.RectangleF($cabezaX, $cabezaY, $cabezaW, $cabezaH)
    $cristal = New-Object System.Drawing.RectangleF(
        ($cabezaX + $grosor), ($cabezaY + $grosor),
        ($cabezaW - 2 * $grosor), ($cabezaH - 2 * $grosor))

    # --- Mango y pomo, detras de la cabeza ---
    $mango = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = $mangoW / 2.0
    $mango.AddArc($mangoX, ($mangoY + $mangoH - 2 * $r), $mangoW, (2 * $r), 0, 180)
    $mango.AddArc($mangoX, $mangoY, $mangoW, (2 * $r), 180, 180)
    $mango.CloseFigure()
    $pomo = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pomo.AddEllipse(($S / 2.0 - $pomoR), ($mangoY + $mangoH - $pomoR), (2 * $pomoR), (2 * $pomoR))

    if ($suave) {
        $bm = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            (New-Object System.Drawing.PointF([float]$mangoX, [float]0)),
            (New-Object System.Drawing.PointF([float]($mangoX + $mangoW), [float]0)),
            $bronceClaro, $bronceOscuro)
    } else {
        # A 16px el contorno oscuro se come el anillo por los dos lados: si el bronce
        # no es el claro, del marco solo queda una linea marron.
        $bm = New-Object System.Drawing.SolidBrush($bronceClaro)
    }
    $g.FillPath($bm, $mango)
    $g.FillPath($bm, $pomo)
    $bm.Dispose()

    # --- Marco ovalado ---
    if ($suave) {
        $bmarco = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            (New-Object System.Drawing.PointF([float]$cabezaX, [float]$cabezaY)),
            (New-Object System.Drawing.PointF([float]($cabezaX + $cabezaW), [float]($cabezaY + $cabezaH))),
            $bronceClaro, $bronceOscuro)
    } else {
        $bmarco = New-Object System.Drawing.SolidBrush($bronceClaro)
    }
    $g.FillEllipse($bmarco, $cabeza)
    $bmarco.Dispose()

    # --- Cristal ---
    if ($suave) {
        $bcristal = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            (New-Object System.Drawing.PointF([float]0, [float]$cristal.Top)),
            (New-Object System.Drawing.PointF([float]0, [float]$cristal.Bottom)),
            $cristalAlto, $cristalBajo)
    } else {
        $bcristal = New-Object System.Drawing.SolidBrush($cristalBajo)
    }
    $g.FillEllipse($bcristal, $cristal)
    $bcristal.Dispose()

    # --- Reflejo diagonal sobre el cristal ---
    $recorte = New-Object System.Drawing.Drawing2D.GraphicsPath
    $recorte.AddEllipse($cristal)
    $estado = $g.Save()
    $g.SetClip($recorte)
    $cx = $cristal.Left + $cristal.Width / 2.0
    $cy = $cristal.Top + $cristal.Height / 2.0
    $g.TranslateTransform([float]$cx, [float]$cy)
    $g.RotateTransform(-38)
    # Dos bandas diagonales estrechas, no un manchon: es lo que lee como cristal.
    if ($detalle) { $alfa = 66 } elseif ($suave) { $alfa = 58 } else { $alfa = 50 }
    $bbrillo = New-Object System.Drawing.SolidBrush(
        [System.Drawing.Color]::FromArgb($alfa, 255, 255, 255))
    $g.FillEllipse($bbrillo,
        [float](-0.46 * $cristal.Width), [float](-0.95 * $cristal.Height),
        [float](0.26 * $cristal.Width), [float](1.90 * $cristal.Height))
    if ($suave) {
        $bbrillo2 = New-Object System.Drawing.SolidBrush(
            [System.Drawing.Color]::FromArgb([int]($alfa * 0.55), 255, 255, 255))
        $g.FillEllipse($bbrillo2,
            [float](-0.10 * $cristal.Width), [float](-0.95 * $cristal.Height),
            [float](0.12 * $cristal.Width), [float](1.90 * $cristal.Height))
        $bbrillo2.Dispose()
    }
    $bbrillo.Dispose()
    $g.Restore($estado)
    $recorte.Dispose()

    # --- Perlado del marco, solo en tamanos grandes ---
    if ($detalle) {
        $a = ($cabezaW - $grosor) / 2.0
        $b = ($cabezaH - $grosor) / 2.0
        $mcx = $cabezaX + $cabezaW / 2.0
        $mcy = $cabezaY + $cabezaH / 2.0
        $rp = $grosor * 0.28
        $bperla = New-Object System.Drawing.SolidBrush($bronceClaro)
        $pperla = New-Object System.Drawing.Pen($borde, [float]($S / 256.0))
        for ($i = 0; $i -lt 20; $i++) {
            $t = 2 * [Math]::PI * $i / 20.0
            $px = $mcx + $a * [Math]::Cos($t)
            $py = $mcy + $b * [Math]::Sin($t)
            $g.FillEllipse($bperla, [float]($px - $rp), [float]($py - $rp), [float](2 * $rp), [float](2 * $rp))
            $g.DrawEllipse($pperla, [float]($px - $rp), [float]($py - $rp), [float](2 * $rp), [float](2 * $rp))
        }
        $bperla.Dispose(); $pperla.Dispose()
    }

    # --- Contornos: definen la silueta sobre fondo claro ---
    $ancho = [Math]::Max(1.0, $S / 128.0)
    $pborde = New-Object System.Drawing.Pen($borde, [float]$ancho)
    $g.DrawEllipse($pborde, $cabeza)
    # El contorno interior solo cabe cuando el marco tiene mas de un par de pixeles.
    if ($suave) { $g.DrawEllipse($pborde, $cristal) }
    $g.DrawPath($pborde, $mango)
    $g.DrawPath($pborde, $pomo)
    $pborde.Dispose()

    $mango.Dispose(); $pomo.Dispose(); $g.Dispose()
    return $bmp
}

function Get-PngBytes([System.Drawing.Bitmap]$bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray(); $ms.Dispose(); return $bytes
}

# DIB de 32 bits para el .ico: cabecera con biHeight doble (XOR + mascara AND),
# pixeles de abajo arriba, y mascara AND a cero porque la transparencia va en el alfa.
function Get-DibBytes([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width; $h = $bmp.Height
    $datos = $bmp.LockBits(
        (New-Object System.Drawing.Rectangle(0, 0, $w, $h)),
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $paso = $datos.Stride
    $crudo = New-Object byte[] ($paso * $h)
    [System.Runtime.InteropServices.Marshal]::Copy($datos.Scan0, $crudo, 0, $crudo.Length)
    $bmp.UnlockBits($datos)

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    $bw.Write([uint32]40); $bw.Write([int32]$w); $bw.Write([int32](2 * $h))
    $bw.Write([uint16]1);  $bw.Write([uint16]32); $bw.Write([uint32]0)
    $bw.Write([uint32]($w * $h * 4)); $bw.Write([int32]0); $bw.Write([int32]0)
    $bw.Write([uint32]0);  $bw.Write([uint32]0)
    for ($y = $h - 1; $y -ge 0; $y--) { $bw.Write($crudo, ($y * $paso), ($w * 4)) }
    $bytesMascara = [int]([Math]::Floor(($w + 31) / 32)) * 4
    $bw.Write((New-Object byte[] ($bytesMascara * $h)))
    $bw.Flush(); $bytes = $ms.ToArray(); $bw.Dispose(); $ms.Dispose()
    return $bytes
}

$entradas = @()
foreach ($S in $tamanos) {
    $bmp = New-Espejo $S
    # Las vistas previas son material de trabajo y quedan fuera del repo; la de 256
    # se guarda ademas en docs/ porque el README la muestra.
    $vista = Join-Path $PSScriptRoot ("preview-{0}.png" -f $S)
    $bmp.Save($vista, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($S -ge 256) {
        $bmp.Save((Join-Path $raiz 'docs\icon.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    # PNG para 256 (formato Vista, evita 270 KB de mapa de bits); DIB para el resto.
    if ($S -ge 256) { $datos = Get-PngBytes $bmp } else { $datos = Get-DibBytes $bmp }
    $entradas += [pscustomobject]@{ Tamano = $S; Datos = $datos }
    $bmp.Dispose()
    "{0,4}px  {1,7:N0} bytes  ->  {2}" -f $S, $datos.Length, (Split-Path -Leaf $vista)
}

$fs = New-Object System.IO.FileStream($destino, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$entradas.Count)
$desplazamiento = 6 + 16 * $entradas.Count
foreach ($e in $entradas) {
    if ($e.Tamano -ge 256) { $d = 0 } else { $d = $e.Tamano }
    $bw.Write([byte]$d); $bw.Write([byte]$d); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$e.Datos.Length); $bw.Write([uint32]$desplazamiento)
    $desplazamiento += $e.Datos.Length
}
# Con la sobrecarga de un solo argumento, PowerShell coacciona el array envuelto en
# PSObject y escribe un unico byte. Hay que tipar y usar la de tres argumentos.
foreach ($e in $entradas) {
    $bloque = [byte[]]$e.Datos
    $bw.Write($bloque, 0, $bloque.Length)
}
$bw.Flush(); $bw.Dispose(); $fs.Dispose()

"speculum.ico: {0:N0} bytes, {1} tamanos" -f (Get-Item $destino).Length, $entradas.Count
