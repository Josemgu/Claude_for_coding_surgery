<#
.SINOPSIS
    Corre la suite entera de csharp/Fichas y deja el numero de pruebas a la vista.

.DESCRIPCION
    Es el equivalente de «python -m pytest tests/» del lado de Python: una sola orden que
    cualquiera puede repetir, con el SDK resuelto dentro para que no dependa del PATH.

.PARAMETER Configuracion
    Release por defecto, que es como se mide.

.PARAMETER Detalle
    Nivel de detalle de la salida. «normal» por defecto; «detailed» para ver prueba a prueba.

.EJEMPLO
    .\pruebas.ps1
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string] $Configuracion = 'Release',

    [ValidateSet('quiet', 'minimal', 'normal', 'detailed')]
    [string] $Detalle = 'normal'
)

$ErrorActionPreference = 'Stop'

# El SDK no esta en el PATH de esta maquina: se resuelve aqui dentro.
$env:DOTNET_ROOT = 'C:\Users\josem\.dotnet'
$dotnet = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
if (-not (Test-Path $dotnet)) {
    throw "No encuentro el SDK de .NET en $env:DOTNET_ROOT. Sin el no se pueden correr las pruebas."
}

$raiz = Split-Path -Parent $MyInvocation.MyCommand.Path
$solucion = Join-Path $raiz 'Fichas.sln'

Write-Output "Corriendo la suite de $solucion en $Configuracion"
& $dotnet test $solucion -c $Configuracion --nologo --verbosity $Detalle

if ($LASTEXITCODE -ne 0) {
    Write-Output ''
    Write-Output "LA SUITE NO PASO (codigo $LASTEXITCODE). No se cierra nada con esto en rojo."
    exit $LASTEXITCODE
}

Write-Output ''
Write-Output 'La suite paso entera.'
