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

.PARAMETER Ensayo
    Publica AUNQUE la version ya este etiquetada en git, para probar un paquete que NO se
    entrega. Sin este interruptor el guion se niega: dos paquetes distintos con el mismo
    numero es justo lo que el dueno no puede distinguir.

    Para que un ensayo no acabe en manos del dueno, se distingue por fuera y por dentro:
    el nombre de la carpeta de destino tiene que llevar la palabra «ensayo» —si no, el
    guion se niega— y dentro queda un archivo ENSAYO-NO-SE-ENTREGA.txt con la version, la
    fecha y el motivo.

.EJEMPLO
    .\publish.ps1 -Destino C:\Users\josem\Fichas-entrega\Fichas-esqueleto

.EJEMPLO
    .\publish.ps1 -Destino C:\Users\josem\Fichas-ensayo -Ensayo
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Destino,

    [ValidateSet('Release', 'Debug')]
    [string] $Configuracion = 'Release',

    [switch] $Ensayo
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

# ---- la version -------------------------------------------------------------------
# El numero vive en UN solo sitio, <Version> de Fichas.App.csproj, y de ahi MSBuild deriva
# lo que declara el .exe. Se lee ANTES de publicar para negarse a tiempo: un paquete con un
# numero que ya salio —«ya esta etiquetado en git»— seria dos paquetes distintos con el
# mismo nombre, y el dueno no sabria cual tiene abierto (lo pidio el 2026-09-07).
$nodo = ([xml](Get-Content $proyecto -Raw)).SelectSingleNode('/Project/PropertyGroup/Version')
if ($null -eq $nodo -or [string]::IsNullOrWhiteSpace($nodo.InnerText)) {
    throw "Fichas.App.csproj no escribe <Version>. Sin numero no se publica: el dueno no sabria que version tiene."
}
$version = $nodo.InnerText.Trim()
$etiqueta = "v$version"

if ($Ensayo) {
    Write-Output "ENSAYO: no se mira si «$etiqueta» ya esta etiquetada. Este paquete NO se entrega."
} else {
    # git tiene que estar: sin el no se puede saber si el numero ya salio, y «no se» no es
    # «no salio». Se busca la etiqueta por su nombre exacto en el repositorio de este guion.
    $git = Get-Command git -ErrorAction SilentlyContinue
    if ($null -eq $git) {
        throw "No encuentro git, y sin el no puedo saber si la version $version ya salio. Instalalo, o publica con -Ensayo si el paquete no es para entregar."
    }
    $existentes = & $git.Source -C $raiz tag --list $etiqueta
    if ($LASTEXITCODE -ne 0) {
        throw "«git tag» fallo con codigo $LASTEXITCODE en $raiz. Sin poder leer las etiquetas no se publica."
    }
    if ($existentes) {
        throw "La version $version ya salio: la etiqueta «$etiqueta» existe en git. Sube <Version> en Fichas.App.csproj antes de publicar otra vez."
    }
}
Write-Output "Version $version (etiqueta $etiqueta)"

# La carpeta del programa de Python esta congelada y no se toca.
$congelada = 'C:\Users\josem\Fichas-entrega\Fichas'
$destinoCompleto = [System.IO.Path]::GetFullPath($Destino)
if ($destinoCompleto.TrimEnd('\') -ieq $congelada) {
    throw "Esa carpeta es la del programa de Python que el dueno usa hoy. Elige otra."
}

# Un ensayo se tiene que ver desde fuera, en el nombre de la carpeta, antes de abrirla.
$nombreDeLaCarpeta = Split-Path -Leaf $destinoCompleto.TrimEnd('\')
if ($Ensayo -and $nombreDeLaCarpeta -inotmatch 'ensayo') {
    throw "Con -Ensayo la carpeta de destino tiene que llevar «ensayo» en el nombre, y «$nombreDeLaCarpeta» no lo lleva. Asi un paquete de prueba no se confunde con una entrega."
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
#
# «Fichas» a secas es el de la app: su AssemblyName es «Fichas», no «Fichas.App», y el
# .pdb sale con ese nombre. Medido el 2026-09-10: sin esa entrada el guion borraba
# Fichas.pdb (0,4 MiB) como si fuera de terceros, y era el unico que importaba.
$nuestros = @('Fichas', 'Fichas.App', 'Fichas.Contratos', 'Fichas.Datos', 'Fichas.Datos.Falso',
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

# La version que declara el .exe publicado tiene que ser la del codigo. Se lee del archivo,
# no de la variable de arriba: es lo que el dueno ve en clic derecho → Detalles.
$exe = Join-Path $destinoCompleto 'Fichas.exe'
$declarada = (Get-Item $exe).VersionInfo.ProductVersion
if ($declarada -ne $version) {
    throw "Fichas.exe declara la version «$declarada» y el codigo dice «$version». No se entrega un paquete que no sabe que version es."
}

# Un ensayo se tiene que ver tambien desde dentro: si alguien copia la carpeta a otro
# sitio con otro nombre, el archivo sigue ahi diciendo que no se entrega.
$marcaDeEnsayo = Join-Path $destinoCompleto 'ENSAYO-NO-SE-ENTREGA.txt'
if ($Ensayo) {
    $fecha = Get-Date -Format 'yyyy-MM-dd HH:mm'
    $texto = @(
        'ESTE PAQUETE ES UN ENSAYO Y NO SE ENTREGA.',
        '',
        "Version ..... $version (etiqueta $etiqueta)",
        "Publicado ... $fecha",
        "Desde ....... $raiz",
        '',
        'Se publico con el interruptor -Ensayo de publish.ps1, que salta la comprobacion de',
        'que la etiqueta de git no exista. Puede haber otro paquete con este mismo numero, y',
        'el dueno no tendria forma de distinguirlos. Si hay que entregarlo, se publica otra',
        'vez sin -Ensayo.'
    )
    Set-Content -Path $marcaDeEnsayo -Value $texto -Encoding UTF8
}

# Las cifras que la entrega tiene que decir, medidas y no estimadas.
$archivos = Get-ChildItem $destinoCompleto -Recurse -File
$bytes = ($archivos | Measure-Object -Property Length -Sum).Sum
$mib = [math]::Round($bytes / 1MB, 1)

Write-Output ''
Write-Output "Carpeta ... $destinoCompleto"
Write-Output "Version ... $version (etiqueta $etiqueta)"
if ($Ensayo) { Write-Output "            ENSAYO: este paquete NO se entrega. Lo dice dentro $marcaDeEnsayo" }
Write-Output "Archivos .. $($archivos.Count)"
Write-Output "Tamano .... $mib MiB ($bytes bytes)"
Write-Output "Se abre con doble clic en Fichas.exe"
