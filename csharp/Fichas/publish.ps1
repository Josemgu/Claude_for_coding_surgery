<#
.SINOPSIS
    Publica Fichas.App en una carpeta autocontenida win-x64 que se abre con doble clic.

.DESCRIPCION
    Carpeta, no archivo unico: es lo que la espiga C0 dejo compilando y publicando en esta
    maquina. Si la FASE C0 mide que el archivo unico sirve, se cambia aqui y en el .csproj.

    ⛔ Nunca publica sobre C:\Users\josem\Fichas-entrega\Fichas: esa carpeta es el .exe de
    Python que el dueno usa hoy, y esta congelada (PENDIENTES.md, regla 2 de las fases C).
    El guion se niega a escribir ahi.

.PARAMETER Destino
    La carpeta donde queda lo publicado. Se crea si no existe.

.PARAMETER Configuracion
    Release por defecto. Debug solo para mirar por dentro.

.EJEMPLO
    .\publish.ps1 -Destino C:\Users\josem\Fichas-entrega\Fichas-esqueleto
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Destino,

    [ValidateSet('Release', 'Debug')]
    [string] $Configuracion = 'Release'
)

$ErrorActionPreference = 'Stop'

# El SDK no esta en el PATH de esta maquina: se resuelve aqui dentro.
$env:DOTNET_ROOT = 'C:\Users\josem\.dotnet'
$dotnet = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
if (-not (Test-Path $dotnet)) {
    throw "No encuentro el SDK de .NET en $env:DOTNET_ROOT. Sin el no se puede publicar."
}

$raiz = Split-Path -Parent $MyInvocation.MyCommand.Path
$proyecto = Join-Path $raiz 'Fichas.App\Fichas.App.csproj'

# La carpeta del programa de Python esta congelada y no se toca.
$congelada = 'C:\Users\josem\Fichas-entrega\Fichas'
$destinoCompleto = [System.IO.Path]::GetFullPath($Destino)
if ($destinoCompleto.TrimEnd('\') -ieq $congelada) {
    throw "Esa carpeta es la del programa de Python que el dueno usa hoy. Elige otra."
}

# Se vacia antes, para que no queden restos de una publicacion anterior mezclados con
# esta. Solo se vacia si la carpeta ya es una publicacion de Fichas o esta vacia: asi el
# guion no puede borrar una carpeta de otra cosa por un parametro mal escrito.
if (Test-Path $destinoCompleto) {
    $tieneAlgo = Get-ChildItem $destinoCompleto -Force | Select-Object -First 1
    $esUnaPublicacion = Test-Path (Join-Path $destinoCompleto 'Fichas.exe')
    if ($tieneAlgo -and -not $esUnaPublicacion) {
        throw "«$destinoCompleto» tiene cosas dentro y no es una publicacion de Fichas. No la toco."
    }
    if ($esUnaPublicacion) {
        Write-Output "Vaciando la publicacion anterior de $destinoCompleto"
        Remove-Item (Join-Path $destinoCompleto '*') -Recurse -Force
    }
}

Write-Output "Publicando $proyecto"
Write-Output "         en $destinoCompleto"

& $dotnet publish $proyecto `
    -c $Configuracion `
    -r win-x64 `
    --self-contained true `
    -p:Platform=x64 `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -o $destinoCompleto `
    --nologo

if ($LASTEXITCODE -ne 0) { throw "La publicacion fallo con codigo $LASTEXITCODE." }

# Los .pdb de terceros no le sirven de nada al dueno y pesan lo que un programa
# entero: medido el 2026-09-04, `libSkiaSharp.pdb` solo eran 84,9 MiB de los
# 419,7 del paquete. Los nuestros se quedan: sin ellos, un fallo en su maquina
# da una traza sin numeros de linea y no hay forma de saber donde se rompio.
$nuestros = @('Fichas.App', 'Fichas.Contratos', 'Fichas.Datos', 'Fichas.Datos.Falso',
              'Fichas.Lectura', 'Fichas.Paquetes', 'Fichas.Reportes')
$ajenos = Get-ChildItem $destinoCompleto -Recurse -File -Filter '*.pdb' |
    Where-Object { $nuestros -notcontains $_.BaseName }
if ($ajenos) {
    $pesoAjeno = [math]::Round((($ajenos | Measure-Object -Property Length -Sum).Sum) / 1MB, 1)
    $ajenos | Remove-Item -Force
    Write-Output "Quitados $($ajenos.Count) .pdb de terceros ($pesoAjeno MiB)."
}

# Un paquete sin los modelos de OCR arranca y no lee NADA, y eso el dueno solo lo
# descubre con sus documentos delante. Mejor que falle aqui.
$modelos = Get-ChildItem $destinoCompleto -Recurse -File -Filter '*.onnx'
if ($modelos.Count -lt 3) {
    throw "El paquete solo trae $($modelos.Count) modelos .onnx y hacen falta al menos 3: sin ellos no lee ningun documento."
}
Write-Output "Modelos de OCR ... $($modelos.Count)"

# Las dos cifras que la entrega tiene que decir, medidas y no estimadas.
$archivos = Get-ChildItem $destinoCompleto -Recurse -File
$bytes = ($archivos | Measure-Object -Property Length -Sum).Sum
$mib = [math]::Round($bytes / 1MB, 1)

Write-Output ''
Write-Output "Carpeta ... $destinoCompleto"
Write-Output "Archivos .. $($archivos.Count)"
Write-Output "Tamano .... $mib MiB ($bytes bytes)"
Write-Output "Se abre con doble clic en Fichas.exe"
