; Fichas.iss — el instalador de Fichas, para Inno Setup 6.
;
; Lo compila publish.ps1 DESPUÉS de publicar la carpeta y de hacer el zip; no se
; compila a mano. Recibe por línea de órdenes lo que no debe vivir aquí duplicado:
;
;   /DOrigen=<carpeta publicada>      lo que sale de «dotnet publish», ya adelgazado
;   /DVersion=<número>                el <Version> de Fichas.App.csproj (hoy «10»)
;   /DSalida=<carpeta>                dónde dejar el .exe del instalador
;   /DNombre=<nombre sin .exe>        Instalar-Fichas-vN, o Instalar-Fichas-vN-ENSAYO
;   /DEnsayo                          solo con publish.ps1 -Ensayo: se ve en el título
;
; Lo que el dueño pidió el 2026-09-11: «Haz un instalador para actualizar». Es decir:
; el instalador de una versión nueva, ejecutado encima de la vieja, la sustituye.
;
; ⛔ Los datos del dueño viven en Documentos\Fichas, FUERA del programa (CLAUDE.md §4).
; Este guion no los nombra en ninguna sección: ni al instalar, ni al actualizar, ni al
; desinstalar se toca nada que no esté en la carpeta del programa.

#ifndef Origen
  #error Falta /DOrigen=<carpeta publicada>. Este guion lo compila publish.ps1, no se compila a mano.
#endif
#ifndef Version
  #error Falta /DVersion=<número>. La versión se lee de Fichas.App.csproj en publish.ps1.
#endif
#ifndef Salida
  #error Falta /DSalida=<carpeta donde dejar el instalador>.
#endif
#ifndef Nombre
  #error Falta /DNombre=<nombre del instalador sin .exe>.
#endif

#ifdef Ensayo
  #define NombreVisible "Fichas v" + Version + " (ENSAYO, no se entrega)"
#else
  #define NombreVisible "Fichas v" + Version
#endif

[Setup]
; Fijo para siempre. Es lo que le dice a Windows y a Inno Setup que el instalador de la
; v11 es EL MISMO programa que la v10 y que debe sustituirla, no ponerse al lado. Con él
; se forma la clave del registro que ve «Aplicaciones instaladas»:
;   HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\{943F88A5-F4D4-43CF-ACEC-97D50EE6D45B}_is1
; Si se cambia, la versión nueva se instala como un segundo programa y la vieja se queda.
AppId={{943F88A5-F4D4-43CF-ACEC-97D50EE6D45B}
AppName=Fichas
AppVersion={#Version}
AppVerName={#NombreVisible}

; Por usuario y sin pedir administrador: en el trabajo del dueño puede no haberlo. La
; carpeta es la que Windows reserva para los programas de un solo usuario. No se ofrece
; «para todos los usuarios»: la máquina es de una persona, y una instalación en Archivos
; de programa exigiría administrador también para CADA actualización, que es justo lo
; que no se puede garantizar.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\Fichas
DisableDirPage=yes
DisableProgramGroupPage=yes
UsePreviousAppDir=yes

; Solo x64: el paquete publicado es win-x64 (Directory.Build.props). En un Windows de
; 32 bits el instalador se niega en vez de copiar un programa que no arrancaría.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Windows 10 1809 (compilación 17763) es el TargetPlatformMinVersion del programa.
MinVersion=10.0.17763

; Actualizar = ejecutar el instalador nuevo encima. Si Fichas está abierto, el
; instalador lo cierra (por el Administrador de reinicios de Windows) antes de tocar
; sus archivos. Quien lo vuelve a abrir es la entrada [Run] de abajo, no esto:
; RestartApplications solo reabre programas que se registraron con
; RegisterApplicationRestart, y Fichas no lo hace.
CloseApplications=yes
RestartApplications=no

; La versión que enseña Windows en «Aplicaciones instaladas» y en Detalles del .exe.
VersionInfoVersion={#Version}
VersionInfoProductTextVersion={#Version}
VersionInfoDescription=Instalador de Fichas
VersionInfoProductName=Fichas
UninstallDisplayName=Fichas
UninstallDisplayIcon={app}\Fichas.exe

SetupIconFile={#Origen}\Assets\AppIcon.ico
WizardStyle=modern
ShowLanguageDialog=no

OutputDir={#Salida}
OutputBaseFilename={#Nombre}
Compression=lzma2
SolidCompression=yes

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
; El del escritorio es una casilla, apagada por defecto; el del menú Inicio va siempre.
Name: "escritorio"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
; Antes de copiar la versión nueva se vacía la carpeta de la vieja. La lista de DLL
; cambia entre versiones y un .dll viejo que sobra puede romper el arranque; con solo
; sobreescribir, se quedaría. Se procesa como PRIMER paso de la instalación (dice la
; ayuda de Inno Setup, sección [InstallDelete]), y CloseApplications lo tiene en cuenta.
;
; Con la misma guarda que publish.ps1 usa para vaciar: SOLO si la carpeta ya es una
; instalación de Fichas. Así, si alguien apunta el instalador a una carpeta que no lo
; es, no se le borra nada.
Type: filesandordirs; Name: "{app}\*"; Check: FileExists(ExpandConstant('{app}\Fichas.exe'))

[Files]
; La carpeta publicada entera, con sus subcarpetas (idiomas de WinUI, modelos de OCR).
; ignoreversion: son archivos privados del programa; se sustituyen siempre, sin comparar
; versiones, que es lo que hace falta para que una actualización sea completa.
Source: "{#Origen}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Fichas"; Filename: "{app}\Fichas.exe"; IconFilename: "{app}\Assets\AppIcon.ico"
Name: "{autodesktop}\Fichas"; Filename: "{app}\Fichas.exe"; IconFilename: "{app}\Assets\AppIcon.ico"; Tasks: escritorio

[Run]
; Con doble clic: la ultima pagina ofrece la casilla «Ejecutar Fichas» (postinstall).
; En silencio (/SILENT, que es como lo lanza el propio programa al actualizarse solo desde
; el 2026-09-11): la pagina no se ve, la casilla cuenta como marcada y la entrada se ejecuta
; igual, asi que Fichas vuelve a abrirse solo al terminar. Para eso se quito «skipifsilent»
; ese dia: con esa bandera, la ayuda de Inno Setup dice «Instructs Setup to skip this entry
; if Setup is running (very) silent», y el programa se quedaba cerrado tras actualizarse.
; Medido el 2026-09-12 con Inno Setup 6.7.3 y un guion de ensayo: con /SILENT, la entrada
; «postinstall» se ejecuta y la que lleva «skipifsilent» no.
;
; ⛔ Lo que costó quitar «skipifsilent», medido el 2026-09-12 por el supervisor: un
; instalador ejecutado en silencio A MANO, sin /carpetadedatos (que es como lo prueban los
; agentes: «/SILENT /SUPPRESSMSGBOXES /DIR=…»), abría Fichas SIN argumentos, es decir, sobre
; la carpeta por defecto Documentos\Fichas, que es la base real del dueño. Por eso desde ese
; día la entrada lleva «Check: DebeReabrirFichas» ([Code], abajo): en silencio solo se
; reabre si llegó /carpetadedatos, que es la señal de que quien lanzó el instalador fue el
; propio programa. Sin el parámetro y en silencio, no se abre nada, como hacía la v11.
; Con ventana no cambia: la casilla de la última página sigue mandando.
;
; Los argumentos con los que se reabre Fichas los da ArgumentosParaFichas ([Code], abajo):
; la carpeta de datos que el programa le paso al instalador, o nada.
;
; «nowait» no desacopla el proceso: el Fichas que se abre sigue siendo descendiente del
; instalador (SetupLdr → Setup → Fichas.exe). Inno Setup no tiene bandera que lo haga
; huérfano, y da igual para el uso real: cuando Fichas se actualiza solo, nadie espera al
; instalador. Sí importa para quien lo pruebe desde PowerShell: «Start-Process -Wait» espera
; a TODO el árbol (lo dice su ayuda: «waits for the specified process and all descendants»)
; y se queda colgado hasta que se cierre ese Fichas. Medido el 2026-09-12: con
; «$p = Start-Process -PassThru; $p.WaitForExit()» vuelve en cuanto termina el instalador.
Filename: "{app}\Fichas.exe"; Parameters: "{code:ArgumentosParaFichas}"; Description: "{cm:LaunchProgram,Fichas}"; Flags: nowait postinstall; Check: DebeReabrirFichas

[Code]
// Los argumentos con los que [Run] reabre Fichas.
//
// Cuando Fichas se actualiza solo (Fichas.App/Actualizacion/LanzadorDelInstalador.cs), lanza
// este instalador con /carpetadedatos="<carpeta>" —la carpeta de datos con la que estaba
// abierto—, y aqui se le devuelve como --carpeta-de-datos al Fichas que se reabre, para que
// no vuelva abierto sobre la carpeta por defecto si estaba sobre otra. Con doble clic nadie
// pasa el parametro y Fichas se abre sin argumentos, como siempre.
//
// La constante «param:carpetadedatos» es la forma que Inno Setup da de leer un parametro
// propio de la linea de ordenes; sin el, es la cadena vacia. Medido el 2026-09-12: llega
// con espacios. (Comentarios con «//» y no con llaves: una llave dentro cerraria el comentario.)
function ArgumentosParaFichas(Param: String): String;
var
  Carpeta: String;
begin
  Carpeta := ExpandConstant('{param:carpetadedatos|}');
  if Carpeta = '' then
    Result := ''
  else
    Result := '--carpeta-de-datos "' + Carpeta + '"';
end;

// Si la entrada [Run] que reabre Fichas se procesa o no (es su «Check:»).
//
// Con ventana (doble clic), siempre: la casilla «Ejecutar Fichas» de la ultima pagina es
// la que decide, como desde la v11. En silencio (/SILENT o /VERYSILENT: WizardSilent da
// True en los dos, dice la ayuda), solo si llego /carpetadedatos, que es lo que manda el
// propio Fichas al actualizarse (LanzadorDelInstalador.cs, y esa carpeta nunca es vacia:
// ArgumentosDeArranque resuelve siempre la dicha o la de por defecto). Un instalador
// silencioso lanzado a mano, sin el parametro, no abre nada.
//
// Anadido el 2026-09-12 por lo medido ese dia: sin esta guarda, «/SILENT» a secas abria
// Fichas sin argumentos sobre Documentos\Fichas, la base real del dueño.
function DebeReabrirFichas(): Boolean;
begin
  Result := (not WizardSilent) or (ExpandConstant('{param:carpetadedatos|}') <> '');
end;

[UninstallDelete]
; Solo la carpeta del programa, y solo si quedó vacía después de quitar lo instalado.
; Nada fuera de {app}: Documentos\Fichas no se nombra en ninguna parte de este guion.
Type: dirifempty; Name: "{app}"
