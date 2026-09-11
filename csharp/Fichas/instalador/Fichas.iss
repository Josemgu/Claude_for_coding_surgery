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
; sus archivos; no lo vuelve a abrir solo: la última página ofrece abrirlo.
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
Filename: "{app}\Fichas.exe"; Description: "{cm:LaunchProgram,Fichas}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Solo la carpeta del programa, y solo si quedó vacía después de quitar lo instalado.
; Nada fuera de {app}: Documentos\Fichas no se nombra en ninguna parte de este guion.
Type: dirifempty; Name: "{app}"
