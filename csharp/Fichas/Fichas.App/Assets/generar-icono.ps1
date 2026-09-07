<#
.SINOPSIS
    Dibuja el logo generico de Fichas y escribe AppIcon.ico y los .png de esta carpeta.

.DESCRIPCION
    El dueno lo pidio el 2026-09-05: «ponle un logo generico al programa». Generico de
    verdad: dos hojas y tres renglones sobre una placa. Nada del templo, nada de la
    Iglesia, ninguna marca.

    ⛔ Sin red y sin bibliotecas nuevas. Dibuja con System.Drawing, que Windows PowerShell
    5.1 ya trae, y arma el contenedor .ico byte a byte aqui dentro. Por eso este guion vive
    en el repositorio: el icono se puede volver a generar sin depender de ninguna descarga.

    El .ico lleva las medidas pequenas como mapa de bits (DIB) y la de 256 como PNG, que es
    como las escriben los editores de iconos: Windows decodifica PNG dentro de un .ico
    desde Vista, pero solo la de 256 esta garantizada en todas las vistas.

.EJEMPLO
    powershell -NoProfile -ExecutionPolicy Bypass -File .\generar-icono.ps1
#>
[CmdletBinding()]
param(
    [string] $Carpeta
)

$ErrorActionPreference = 'Stop'

# Por defecto, la carpeta donde vive el guion. $PSScriptRoot es lo que funciona tanto si se
# ejecuta con -File como si se le hace punto-fuente; $MyInvocation llega nulo en param().
if (-not $Carpeta) { $Carpeta = $PSScriptRoot }
Add-Type -AssemblyName System.Drawing

# --- los colores del logo -------------------------------------------------------
# Placa azul pizarra: se lee sobre fondo claro y sobre fondo oscuro, que es lo que hace
# falta cuando el mismo icono sale en el explorador y en la barra de tareas.
$ColorPlaca     = [System.Drawing.Color]::FromArgb(255, 45, 74, 99)
$ColorHojaAtras = [System.Drawing.Color]::FromArgb(150, 255, 255, 255)
$ColorHoja      = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
$ColorRenglon   = [System.Drawing.Color]::FromArgb(255, 90, 107, 122)

function New-RectanguloRedondo {
    param([single] $X, [single] $Y, [single] $Ancho, [single] $Alto, [single] $Radio)

    $camino = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $Radio * 2
    if ($d -le 0) {
        $camino.AddRectangle((New-Object System.Drawing.RectangleF $X, $Y, $Ancho, $Alto))
        return $camino
    }
    $camino.AddArc($X, $Y, $d, $d, 180, 90)
    $camino.AddArc(($X + $Ancho - $d), $Y, $d, $d, 270, 90)
    $camino.AddArc(($X + $Ancho - $d), ($Y + $Alto - $d), $d, $d, 0, 90)
    $camino.AddArc($X, ($Y + $Alto - $d), $d, $d, 90, 90)
    $camino.CloseFigure()
    return $camino
}

function New-Logo {
    <#
        Dibuja el logo centrado en un lienzo de $Ancho x $Alto. El dibujo se escala con
        $Lado, que es el lado del cuadrado del logo: asi el mismo codigo sirve para el
        icono cuadrado y para las imagenes anchas.

        A 16 pixeles los renglones se convierten en manchas, asi que por debajo de 24 no
        se dibujan y por debajo de 32 se dibujan dos en vez de tres. Un icono que a tamano
        pequeno es un borron no es un icono.
    #>
    param([int] $Ancho, [int] $Alto, [int] $Lado = 0)

    if ($Lado -le 0) { $Lado = [Math]::Min($Ancho, $Alto) }
    $mapa = New-Object System.Drawing.Bitmap $Ancho, $Alto, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($mapa)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $x0 = ($Ancho - $Lado) / 2.0
    $y0 = ($Alto - $Lado) / 2.0
    $s = [single] $Lado

    # La placa.
    $placa = New-RectanguloRedondo ($x0 + 0.03 * $s) ($y0 + 0.03 * $s) (0.94 * $s) (0.94 * $s) (0.20 * $s)
    $brocha = New-Object System.Drawing.SolidBrush $ColorPlaca
    $g.FillPath($brocha, $placa)
    $brocha.Dispose(); $placa.Dispose()

    # La hoja de atras, la que asoma: es lo que dice «fichas» en plural. Por debajo de 20
    # pixeles NO se dibuja: a esa medida las dos hojas se funden en una mancha blanca y se
    # come el borde de la placa, o sea que el icono deja de leerse en la barra de tareas.
    if ($Lado -ge 20) {
        $atras = New-RectanguloRedondo ($x0 + 0.32 * $s) ($y0 + 0.19 * $s) (0.44 * $s) (0.52 * $s) (0.05 * $s)
        $brocha = New-Object System.Drawing.SolidBrush $ColorHojaAtras
        $g.FillPath($brocha, $atras)
        $brocha.Dispose(); $atras.Dispose()

        $izquierda = 0.22; $arriba = 0.29; $anchoHoja = 0.44; $altoHoja = 0.52
    }
    else {
        $izquierda = 0.30; $arriba = 0.27; $anchoHoja = 0.40; $altoHoja = 0.46
    }

    # La hoja de delante.
    $delante = New-RectanguloRedondo ($x0 + $izquierda * $s) ($y0 + $arriba * $s) ($anchoHoja * $s) ($altoHoja * $s) (0.05 * $s)
    $brocha = New-Object System.Drawing.SolidBrush $ColorHoja
    $g.FillPath($brocha, $delante)
    $brocha.Dispose(); $delante.Dispose()

    # Los renglones, solo cuando se ven.
    $cuantos = if ($Lado -ge 32) { 3 } elseif ($Lado -ge 24) { 2 } else { 0 }
    if ($cuantos -gt 0) {
        $grosor = [Math]::Max(1.0, 0.045 * $s)
        $brocha = New-Object System.Drawing.SolidBrush $ColorRenglon
        for ($i = 0; $i -lt $cuantos; $i++) {
            $y = $y0 + (0.40 + (0.13 * $i)) * $s
            $largo = if ($i -eq ($cuantos - 1)) { 0.22 * $s } else { 0.30 * $s }
            $g.FillRectangle($brocha, ($x0 + 0.29 * $s), $y, $largo, $grosor)
        }
        $brocha.Dispose()
    }

    $g.Dispose()
    return $mapa
}

function Get-BytesPng {
    param([System.Drawing.Bitmap] $Mapa)

    $flujo = New-Object System.IO.MemoryStream
    $Mapa.Save($flujo, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $flujo.ToArray()
    $flujo.Dispose()
    return ,$bytes
}

function Get-BytesDib {
    <#
        El mapa de bits de 32 bits que va dentro de un .ico: cabecera BITMAPINFOHEADER con
        el alto DOBLE (imagen + mascara), las filas de abajo arriba en BGRA, y la mascara
        de 1 bit a cero, que con 32 bits la transparencia ya la lleva el canal alfa.
    #>
    param([System.Drawing.Bitmap] $Mapa)

    $ancho = $Mapa.Width
    $alto = $Mapa.Height
    $datos = $Mapa.LockBits(
        (New-Object System.Drawing.Rectangle 0, 0, $ancho, $alto),
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $pixeles = New-Object byte[] ($datos.Stride * $alto)
    [System.Runtime.InteropServices.Marshal]::Copy($datos.Scan0, $pixeles, 0, $pixeles.Length)
    $Mapa.UnlockBits($datos)

    $filasMascara = [Math]::Floor(($ancho + 31) / 32) * 4
    $flujo = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $flujo

    $w.Write([uint32] 40)            # biSize
    $w.Write([int32] $ancho)         # biWidth
    $w.Write([int32] ($alto * 2))    # biHeight: imagen + mascara
    $w.Write([uint16] 1)             # biPlanes
    $w.Write([uint16] 32)            # biBitCount
    $w.Write([uint32] 0)             # biCompression: sin comprimir
    $w.Write([uint32] (($ancho * $alto * 4) + ($filasMascara * $alto)))
    $w.Write([int32] 0); $w.Write([int32] 0)   # resolucion, que aqui no importa
    $w.Write([uint32] 0); $w.Write([uint32] 0) # paleta: ninguna

    for ($y = $alto - 1; $y -ge 0; $y--) {
        $w.Write($pixeles, ($y * $datos.Stride), ($ancho * 4))
    }
    $w.Write((New-Object byte[] ($filasMascara * $alto)))

    $w.Flush()
    $bytes = $flujo.ToArray()
    $w.Dispose(); $flujo.Dispose()
    return ,$bytes
}

function Write-Ico {
    param([string] $Ruta, [int[]] $Medidas)

    $imagenes = @()
    foreach ($m in $Medidas) {
        $mapa = New-Logo -Ancho $m -Alto $m
        $bytes = if ($m -ge 256) { Get-BytesPng $mapa } else { Get-BytesDib $mapa }
        $imagenes += [pscustomobject]@{ Medida = $m; Bytes = [byte[]] $bytes }
        $mapa.Dispose()
    }

    $flujo = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $flujo
    $w.Write([uint16] 0)                    # reservado
    $w.Write([uint16] 1)                    # tipo 1: icono
    $w.Write([uint16] $imagenes.Count)

    $desplazamiento = 6 + (16 * $imagenes.Count)
    foreach ($i in $imagenes) {
        $w.Write([byte] $(if ($i.Medida -ge 256) { 0 } else { $i.Medida }))
        $w.Write([byte] $(if ($i.Medida -ge 256) { 0 } else { $i.Medida }))
        $w.Write([byte] 0)                  # colores de la paleta: ninguna
        $w.Write([byte] 0)                  # reservado
        $w.Write([uint16] 1)                # planos
        $w.Write([uint16] 32)               # bits por pixel
        $w.Write([uint32] $i.Bytes.Length)
        $w.Write([uint32] $desplazamiento)
        $desplazamiento += $i.Bytes.Length
    }
    foreach ($i in $imagenes) { $w.Write([byte[]] $i.Bytes) }

    $w.Flush()
    [System.IO.File]::WriteAllBytes($Ruta, $flujo.ToArray())
    $w.Dispose(); $flujo.Dispose()

    Write-Output ("{0} ... {1} medidas, {2} bytes" -f (Split-Path -Leaf $Ruta), $imagenes.Count, (Get-Item $Ruta).Length)
}

function Write-Png {
    param([string] $Ruta, [int] $Ancho, [int] $Alto, [int] $Lado = 0)

    $mapa = New-Logo -Ancho $Ancho -Alto $Alto -Lado $Lado
    $mapa.Save($Ruta, [System.Drawing.Imaging.ImageFormat]::Png)
    $mapa.Dispose()
    Write-Output ("{0} ... {1}x{2}" -f (Split-Path -Leaf $Ruta), $Ancho, $Alto)
}

$destino = [System.IO.Path]::GetFullPath($Carpeta)
Write-Output "Escribiendo el logo en $destino"

# El .ico del ejecutable y de la ventana. Las medidas son las que Windows pide: 16 y 32
# para la ventana y la barra de tareas, 48 para la vista mediana del explorador, 256 para
# la vista grande. Las demas evitan que Windows escale una que no toca.
Write-Ico (Join-Path $destino 'AppIcon.ico') @(16, 20, 24, 32, 40, 48, 64, 256)

# Los .png son los de la plantilla MSIX. Con WindowsPackageType=None NO los usa nadie, y
# se regeneran solo para que no quede arte de la plantilla de Microsoft en el repositorio.
Write-Png (Join-Path $destino 'Square44x44Logo.scale-200.png') 88 88
Write-Png (Join-Path $destino 'Square44x44Logo.targetsize-24_altform-unplated.png') 24 24
Write-Png (Join-Path $destino 'Square44x44Logo.targetsize-48_altform-lightunplated.png') 48 48
Write-Png (Join-Path $destino 'Square150x150Logo.scale-200.png') 300 300
Write-Png (Join-Path $destino 'StoreLogo.png') 50 50
Write-Png (Join-Path $destino 'LockScreenLogo.scale-200.png') 48 48
Write-Png (Join-Path $destino 'Wide310x150Logo.scale-200.png') 620 300 300
Write-Png (Join-Path $destino 'SplashScreen.scale-200.png') 1240 600 320

Write-Output 'Listo.'
