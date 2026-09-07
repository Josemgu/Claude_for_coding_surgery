# ADR-0003 — El programa se reescribe en C# con WinUI 3

- **Fecha:** 2026-09-04
- **Estado:** aceptada en lo esencial (la decisión de pasar a C# + WinUI 3 la tomó
  el dueño el 2026-09-04, `DECISIONES.md` «El dueño decide: el programa completo
  pasa a C# con WinUI 3»). Este ADR **no decide el qué**: decide **el cómo**, con
  fuentes. ⛔ Tres puntos quedan esperando al dueño y están marcados 🔴.
- **Decide:** planificador, sobre encargo del supervisor
- **Afecta a:** todo el código; `PENDIENTES.md` (sección «Fases del programa en
  C#»); no afecta a `docs/ARQUITECTURA.md` §2, que se conserva **tal cual**
- **Inmutable** (`CLAUDE.md` §7). Si se revierte, se escribe un ADR nuevo.

---

## 0. Premisas del pase que NO se sostuvieron — medido antes de escribir nada

`CLAUDE.md` §8: un documento no es una medición. Tres cifras del pase se
comprobaron y **dos estaban mal atribuidas**.

**Premisa 1. «Hoy NO hay .NET SDK ni Visual Studio.» — cierto en lo que importa, con un
matiz que cambia una fase.** Medido el 2026-09-04 en esta máquina:

```
$ dotnet --info
Host:
  Version:      6.0.16
  Architecture: x64
.NET SDKs installed:
  No SDKs were found.
.NET runtimes installed:
  Microsoft.NETCore.App 6.0.16 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
$ where msbuild
INFO: Could not find files for the given pattern(s).
$ where dotnet
C:\Program Files\dotnet\dotnet.exe
```

Hay **host y runtime 6.0.16**, no SDK. El runtime 6.0 **está fuera de soporte
desde el 2024-11-12** (ver §7) y no sirve para nada de este plan; lo anoto porque
un `where dotnet` que responde puede hacer creer que la máquina está lista.

**Premisa 2. «18/18 campos rastreados … medido por el supervisor» — falso en las tres
partes.** La cifra existe pero es de otra cosa. Literal de `DECISIONES.md:851-853`:

> **Según el informe del programador, NO repetido por mí:** 861 pruebas OK; **18 de 18**
> propiedades estructurales idénticas entre el Excel nuevo y el del viejo abriendo
> los dos con `openpyxl`

Son **propiedades del Excel**, no campos de OCR; y **no las repitió el supervisor**.

**Premisa 3. «4 de 5 MRN con dos PDF reales» — no existe esa medición.** Los dos «4 de
5» del repositorio son otra cosa: `DECISIONES.md:1004-1005` dice que
`extraccion/etiquetas.py` tiene «las formas españolas de **4 de los 5**» rótulos;
y `DECISIONES.md:991-992` dice «los **cinco** con MRN vuelven con 7/7 pasos; la
que no tiene MRN vuelve con 0/7. Y **su PDF real trae 1 de 4 sin MRN**».

**La línea base honesta del OCR actual, que es la que hay que batir**, es
`DECISIONES.md:1062-1066`, y viene marcada **«Según el informe del programador, NO
repetido por mí»**:

| Material real | Lo que extrae hoy |
|---|---|
| Los dos PDF del dueño unidos en un documento | 1 caso · 4 personas · `2026-08-25` + 1 renglón `campo_discrepante` |
| El grupo real de seis hojas | 1 caso · **12 personas** · 0 discrepancias |
| El PDF del dueño en el **proyecto viejo** (sí medido por el supervisor, salida literal en `DECISIONES.md:722`) | `caso BARC2608 · 23 campos leídos de 26 · 2 por revisar` |

**Premisa 4. «WPF en .NET 8/9» — las dos versiones que el pase nombra mueren en dos
meses.** Ver §7. La comparación se hace contra **.NET 10 LTS**.

**Consecuencia para el pase:** ninguna de las cuatro invalida el encargo. Las premisas 2 y
3 sí invalidan el criterio «igualar 18/18 y 4/5»: **ese criterio no se puede
escribir porque esa medición no existe**. En su lugar la FASE C0 usa la tabla de
arriba, que sí existe.

---

## 1. El problema

El dueño decidió el 2026-09-04 reescribir el programa en C# con WinUI 3. Sus
palabras, dos veces: *«vamos a cambiar el programa completo a C# y WinUI 3, es
súper lento en la interfaz que hiciste; debo poder hacer scroll down en cualquier
parte»* y *«perderemos menos tiempo haciéndolo así»*.

Lo que se tira y lo que no, medido el 2026-09-04:

```
$ for d in datos extraccion importacion interfaz paquete espejo reportes pruebas; do ... done
datos:       29 archivos,  6 279 lineas
extraccion:  13 archivos,  2 122 lineas
importacion:  5 archivos,  1 434 lineas
interfaz:    37 archivos, 11 445 lineas
paquete:      7 archivos,  1 728 lineas
espejo:       5 archivos,    616 lineas
reportes:    14 archivos,  2 579 lineas
pruebas:     68 archivos, 19 534 lineas
TOTAL codigo (sin pruebas): 26 203 lineas
$ grep -rn "def prueba_\|def test_" pruebas/ --include="*.py" | wc -l
1182
```

**11 445 de 26 203 líneas (43,7%) son interfaz Tk.** Ésa es la parte que el dueño
quiere tirar, y es la única que el cambio obliga a tirar. Las otras 14 758 líneas
son reglas de negocio que **se portan, no se reinventan** — y su especificación
son las 1 159 pruebas que pasan hoy (`DECISIONES.md:1764`: «1159 OK, 1 omitida,
553 s»).

De esos 1 182 métodos de prueba declarados:

```
$ ui=$(grep -rln "tkinter\|import interfaz\|from interfaz" pruebas/ --include="*.py")
metodos en pruebas de interfaz: 433
sin interfaz: 749
```

**749 métodos (63%) no tocan la ventana.** Ésos son los que se portan primero y
los que sostienen el núcleo sin dibujar nada.

---

## 2. Un solo ejecutable sin instalación — la regla permanente 2

**Lo que la regla dice**, literal de `CLAUDE.md` §1.2: *«El usuario final abre con
doble clic. Sin instalación, sin Python en la máquina, sin servidor web, sin
puertos abiertos.»* No dice «un archivo»; dice **doble clic y sin instalar**. Hoy
se cumple con una **carpeta** (`--onedir`, 216,9 MiB, 124 archivos, arranque
2,19 / 2,42 s — `DECISIONES.md:1765`, medido por el supervisor).

### 2.1 Lo que dice Microsoft, y una contradicción entre dos de sus páginas

**Fuente 1** — `https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps`
(consultada 2026-09-04; la página se declara `ms.date: 2026-05-28`). Valor
literal:

> Note that `dotnet publish` bundles managed assemblies but **cannot produce a
> single-file EXE** for WinUI 3 apps — the native Windows App SDK runtime
> dependencies must remain as separate files.

**Fuente 2** — `https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app`
(consultada 2026-09-04; `ms.date: 2026-08-29`, tres meses más nueva y específica
del tema). Valor literal:

> Starting with Windows App SDK 1.5, **unpackaged, self-contained** WinUI 3 apps
> support the .NET `PublishSingleFile` deployment model. This produces a single
> distributable EXE file — all dependencies are bundled into the EXE and extracted
> to a temp directory at first launch.

> `PublishSingleFile` is **not** supported for packaged apps (MSIX or packaged with
> external location) or for framework-dependent apps. Both conditions — unpackaged
> **and** self-contained — are required.

**Las dos son de Microsoft y se contradicen.** No lo resuelvo por autoridad: lo
resuelvo midiendo, y por eso publicar de las dos formas y pesarlas es **criterio
de aceptación de la FASE C0**, no una nota al pie.

### 2.2 Las seis propiedades exactas, citadas

De la fuente 2, literal:

```xml
<PropertyGroup>
  <WindowsPackageType>None</WindowsPackageType>
  <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
  <SelfContained>true</SelfContained>
  <EnableMsixTooling>true</EnableMsixTooling>
  <IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>
  <PublishSingleFile>true</PublishSingleFile>
</PropertyGroup>
```

> The build emits **errors** if `EnableMsixTooling`, `WindowsPackageType=None`, or
> `IncludeAllContentForSelfExtract` are missing, and **warnings** if
> `WindowsAppSDKSelfContained` or `SelfContained` are absent.

### 2.3 Y el precio del archivo único, que este proyecto ya pagó una vez

Misma fuente 2, literal:

> **Extraction behavior:** `IncludeAllContentForSelfExtract=true` means dependencies
> are extracted to a temp directory on the user's machine at first launch — the app
> is not a zero-extraction binary.

Y `https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview`
(consultada 2026-09-04), literal:

> If running on Windows, the files are extracted to a directory under `%TEMP%/.net`.

> Specifying `IncludeAllContentForSelfExtract` extracts all files, including the
> managed assemblies, before running the executable. This may be helpful for rare
> application compatibility problems. **This mode is not recommended:** it's a .NET
> Core 3.1 compatibility mode and might be removed in a future release.

**Esto es exactamente lo que este proyecto ya decidió no hacer**, por medición
propia. `DECISIONES.md:1991-1993`, regla de la FASE 9: *«`--onedir`, no
`--onefile`. Con `--onefile` los modelos `.onnx` se descomprimen a una carpeta
temporal en cada arranque y el programa tarda entre 15 y 30 segundos en abrir.»*
Los modelos `.onnx` siguen siendo los mismos si se va por la vía B del OCR (§3).

**Recomendación:** **carpeta autocontenida, no archivo único.** Cumple la regla 2
igual que hoy, evita repetir el error del `--onefile`, y no depende de una
propiedad que la propia documentación de .NET llama *not recommended*. El archivo
único se mide en la C0 y, si arranca rápido, se ofrece al dueño como opción.
🔴 **Es decisión suya**, no mía.

### 2.4 Qué exige de la PC del trabajo

- **Con `WindowsAppSDKSelfContained=true` + `SelfContained=true`: nada.** Fuente 2,
  literal: *«This removes the runtime dependency — users don't need to install
  anything separately.»* Ni .NET, ni el instalador del Windows App SDK.
- **Sin administrador:** la distribución es copiar la carpeta. Fuente 1, literal:
  *«the Windows App SDK dependencies are copied next to the `.exe` in your build
  output. You can xcopy-deploy the resulting files»*. Es lo mismo que hoy.
- **Windows mínimo:** *«Windows 10 version 1809 (build 17763) or later»*
  (`https://learn.microsoft.com/en-us/windows/apps/get-started/start-here`,
  consultada 2026-09-04). La PC del trabajo es Windows 11 — lo dice el dueño y
  **NO lo he comprobado**; esta máquina es `Microsoft Windows 11 Home build 26200`
  (medido).
- ⚠️ **Lo que NO exige el dueño y sí exige el desarrollo:** *«Developer Mode
  enabled»* (misma fuente). Es un interruptor en Ajustes de **esta** máquina.
  Medido hoy: `AllowDevelopmentWithoutDevLicense` está **vacío** → no activado.
  **NO he comprobado si activarlo pide administrador**; esta sesión corre sin
  administrador (`IsInRole(Administrator) = False`, medido).

### 2.5 El tamaño: no lo he medido y no lo estimo

Ninguna página de Microsoft da una cifra. Las dos frases que hay son cualitativas:
*«which significantly increases output size»* y *«your output folder is
significantly larger»* (fuente 2). **No encontré ninguna cifra oficial de tamaño
de una app WinUI 3 autocontenida.** No la invento: **es la primera medición de la
FASE C0**, con la referencia de hoy delante (216,9 MiB / 124 archivos).

Lo único que sí se puede acotar por adelantado, y con fuente: si se va por la vía B
del OCR, `Microsoft.ML.OnnxRuntime` **1.29.0** pesa **147,83 MB** de paquete NuGet
(`https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime/`, consultada
2026-09-04) porque *«includes native libraries for all supported platforms»* — de
los que a `win-x64` le toca una fracción que **NO he medido**.

### 2.6 WPF en .NET 10 — la alternativa, con el mismo criterio

| | **WinUI 3 (Windows App SDK 2.4.0)** | **WPF (.NET 10)** |
|---|---|---|
| Archivo único | Solo si *unpackaged* **y** *self-contained*, con extracción a `%TEMP%\.net`; y una página de MS dice que no se puede (§2.1) | Soportado sin condiciones. Fuente 2, literal: *«WPF and WinForms apps support `PublishSingleFile` with a broader set of configurations»* |
| Carpeta autocontenida | Sí | Sí |
| Runtime en la máquina destino | Ninguno (autocontenido) | Ninguno (autocontenido) |
| `ScrollView` con zoom y arrastre de serie | **Sí** (§6.2) | No: `ScrollViewer` de WPF no trae `ZoomMode`; el zoom se construye |
| Listas virtualizadas | `ItemsView` / `ItemsRepeater`, virtualización de UI y de datos (§6.1) | `VirtualizingStackPanel`, existe desde 2006 |
| Fin de soporte de la plataforma | Windows App SDK 2.0 → **2027-04-29** (medido, tabla oficial) | .NET 10 → **2028-11-14** (medido, tabla oficial) |
| Lo que el dueño pidió | **Es lo que pidió** | No es lo que pidió |

**Recomiendo WinUI 3, que es lo que el dueño eligió, y el argumento con cifras es
el punto 5 de su lista** (`DECISIONES.md:1856`): *«Rápido con 3 000 documentos, no
con 2»*. La queja que originó todo es de desplazamiento y de dibujado, y ahí WinUI
3 trae de serie las dos cosas que hoy no hay: `ScrollView` con `ZoomMode` para el
visor, e `ItemsView`/`ItemsRepeater` con virtualización para la lista. En WPF
habría que construir el zoom a mano.

**El único punto donde WPF gana con cifra es el fin de soporte: 2028-11-14 contra
2027-04-29, diecinueve meses más.** No es suficiente para contradecir al dueño:
el Windows App SDK saca versión mayor cada seis meses como mucho («Major releases
no more than every six months», tabla oficial) y renovarla es cambiar un número
en el `.csproj`. Lo digo para que conste, no para pedir que cambie.

⛔ **Lo que NO he podido comparar:** el **arranque**. El pase pregunta si arranca
más rápido que los ~2 s de PyInstaller. **No lo sé y no lo estimo.** Sin SDK en la
máquina no hay nada que cronometrar, y ninguna fuente oficial publica ese número.
**Es el segundo criterio medible de la FASE C0.**

---

## 3. OCR determinista — la regla permanente 1

*«Sin IA generativa para leer campos… Solo OCR determinista y reglas»*
(`CLAUDE.md` §1.1). Las dos vías cumplen la regla: ninguna es un modelo de
lenguaje. Se comparan por precisión y por peso.

### 3.1 Vía A — `Windows.Media.Ocr`, el motor de Windows

Fuente: `https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine`
(consultada 2026-09-04). Devuelve `OcrResult` → `OcrLine` → `OcrWord`, y literal:

> Each **OcrWord** object specifies the text, size, and position information of the
> word in the image.

**Da caja por palabra**, que es lo que necesita el cruce por bandas
(`franja_y`/`franja_x`) y el solapamiento del 50% con los tachones. Existe desde
Windows 10 10240.

**El idioma.** Fuente:
`https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine.availablerecognizerlanguages`
(consultada 2026-09-04), literal:

> **A language pack must be installed on the device to be used.** A user can install
> new OCR language packs through Windows Settings.

**Medido en esta máquina el 2026-09-04**, PowerShell 5.1 contra WinRT, sin
instalar nada:

```
$ [Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages
TOTAL: 2
  en-US  |  English (United States)
  es-MX  |  Spanish (Mexico)
MaxImageDimension: 10000
Windows: Microsoft Windows 11 Home build 26200
```

Tres cosas que salen de ahí:

1. **El español ya está, sin instalar ni pedir administrador.** `es-MX` sirve para
   un formulario en español; `Language` resuelve por coincidencia
   (`IsLanguageSupported`), y el usuario tiene `es-DO` sin recognizer propio.
2. **`MaxImageDimension` = 10 000 px.** El rasterizado del proyecto tiene tope de
   **3 500 px** en el lado largo (regla de no regresión) y produjo
   **2705×3500 px** (`ESTADO.md:104`). **Cabe con holgura de 2,8×.**
3. **En la PC del trabajo puede haber otros idiomas, y eso es un riesgo real.**
   Si allí no está el español, hay que añadirlo por Ajustes. **NO he comprobado si
   eso exige administrador** y no tengo acceso a esa máquina. Es la única pregunta
   de este ADR que **solo el dueño puede contestar**, y se contesta ejecutando una
   línea (va en la FASE C0).

⚠️ **Lo que la vía A pierde y hay que decir en voz alta:** el francés. El requisito
2 del dueño (`DECISIONES.md:752-753`) es *«hay formulario que estarán en inglés,
en francés… pero el idioma no debe importar»*. Con la vía A, un formulario francés
necesita el paquete `fr` **instalado en la máquina de Miguel**. Con la vía B, el
modelo latino ya lo cubre — es un modelo del grupo latino, no del español.

### 3.2 Vía B — `onnxruntime` en C# con los mismos tres modelos PP-OCRv5

`Microsoft.ML.OnnxRuntime` **1.29.0**, publicado **2026-08-12**, **147,83 MB**,
*«CPU Execution Provider»* (NuGet, consultada 2026-09-04).

Los modelos son los que ya están en el repositorio y ya se verificaron byte a byte
dentro del `.exe` (`ESTADO.md:250`, QA):

- `ch_PP-OCRv5_det_mobile.onnx` — detección
- `latin_PP-OCRv5_rec_mobile.onnx` — reconocimiento **latino**
- `ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx` — orientación

**Lo que hay que portar a mano, y es lo caro:** hoy ese pre/posproceso **no está
en este repositorio** — lo pone RapidOCR. `extraccion/ocr.py` solo carga los tres
modelos por ruta explícita. Portarlo significa escribir en C#:

- **DB (detección):** normalización de la imagen, umbralización del mapa de
  probabilidad, extracción de contornos, *unclip* de cada caja y ordenación.
- **CTC (reconocimiento):** redimensionado a la altura del modelo con relación de
  aspecto, decodificación *greedy* con colapso de repetidos y `blank`, y el
  **diccionario de caracteres latino** — que **NO está en el repositorio hoy**:
  `modelos/` tiene los `.onnx` y **NO he comprobado** que traiga el `.txt` del
  vocabulario. Sin él, la vía B no arranca.
- **cls (orientación):** clasificación 0°/180° y volteo.

**No es «portar código»: es reimplementar una biblioteca.** Y hay una regla del
proyecto que lo encarece más: `DECISIONES.md`, no regresión — *«Cargar
explícitamente los modelos PP-OCRv5 del grupo latino… el español sube a casi 100%
solo con los latinos»*. Esa cifra vale para el modelo, no para una
reimplementación nueva del pre/posproceso: **cada paso mal portado se come esa
precisión sin avisar.**

### 3.3 Recomendación

**Vía A (`Windows.Media.Ocr`) para la FASE C0, y la decisión definitiva se toma
con la medición delante, no ahora.**

Por qué: la vía A es cero dependencias, cero MB, el español ya está medido en esta
máquina, y `MaxImageDimension` da 2,8× de margen. La vía B tiene un techo de
precisión conocido y bueno, pero cuesta reimplementar tres algoritmos y arrastra
147,83 MB de NuGet.

**Y no se elige por elegancia: se elige por el número.** La FASE C0 pasa los dos
PDF reales del dueño por la vía A y compara **contra la tabla de §0, Premisa 3**, que es
lo que hoy funciona:

| Criterio de la C0 | Umbral |
|---|---|
| Los dos PDF unidos | **1 caso · 4 personas · fecha `2026-08-25`** |
| El grupo de seis hojas | **1 caso · 12 personas** |

Si la vía A no llega, entra la vía B con su coste dicho — **una fase entera para
ella sola**, no un rato.

⚠️ **Lo que ninguna de las dos vías cambia, y conviene no perder de vista:** el
mayor salto de lectura del proyecto no vino del motor de OCR sino de las **anclas
bilingües** (`DECISIONES.md:537`, «el formulario está en español y las anclas en
inglés») y de los **perfiles por modelo de formulario** del proyecto viejo. Eso es
lógica, y es la misma en cualquier lenguaje.

---

## 4. PDF: rasterizar y leer anotaciones

Son **dos trabajos distintos** y el proyecto los tiene separados desde el día uno
(`pypdfium2` rasteriza, `pypdf` lee anotaciones). En C# siguen siendo dos.

### 4.1 Rasterizar

| Opción | Licencia | Fuente y fecha | Nota |
|---|---|---|---|
| **`Windows.Data.Pdf`** | Parte de Windows, sin dependencia | `https://learn.microsoft.com/en-us/uwp/api/windows.data.pdf.pdfpage`, consultada 2026-09-04. `RenderToStreamAsync` *«Takes a set of display settings, applies them to the output of a PDF page's contents, and creates a stream with the customized, rendered output»*. Windows 10 10240 | 0 MB. Solo rasteriza: en su lista de métodos **no hay ninguno de texto ni de anotaciones** |
| **PDFtoImage 5.4.0** (2026-08-16) | **MIT**; usa `bblanchon.PDFium >= 152.0.7961` | `https://www.nuget.org/packages/PDFtoImage/`, consultada 2026-09-04 | PDFium es **BSD-3-Clause** (`https://pdfium.googlesource.com/pdfium/+/refs/heads/main/LICENSE`, consultada 2026-09-04) — el mismo motor que hoy usa `pypdfium2` |

**Recomiendo `Windows.Data.Pdf`**: es el mismo argumento que la vía A del OCR —
cero MB y cero licencias de terceros — y rasterizar es justo lo que sabe hacer.
Con PDFium como plan B si el rasterizado nativo no reproduce el
**2705×3500 px en el tope exacto** que hoy se mide.

⚠️ **PDFiumViewer** lo nombra el pase: es un envoltorio **abandonado** (última
versión de hace años, **NO lo he comprobado en NuGet hoy**). No lo recomiendo y no
lo he investigado más: PDFtoImage cubre lo mismo con mantenimiento vivo.

### 4.2 Leer anotaciones y casillas (`/AS != /Off`)

**PdfPig 0.1.16** (2026-08-22), **Apache-2.0**
(`https://www.nuget.org/packages/PdfPig/`, consultada 2026-09-04). Literal de la
descripción: soporta *«Form fields for interactive forms (AcroForms)»*, **read-only**,
y *«hyperlinks»* (anotaciones de enlace).

🔴 **Y aquí hay un hueco que el pase no nombra y que puede costar una fase.** Lo
que este proyecto necesita de las anotaciones **no es AcroForm**. Es, literal de
`DECISIONES.md:1900-1907`:

- `/FreeText` con texto extraíble directo,
- `/Ink` con **color RGB** y `/BS /W` (grosor) para distinguir tachón rojo
  (`0.890, 0.094, 0.176`, grosor 1.65) de resaltador verde (`0.494, 0.765, 0`,
  grosor 16.5),
- y el `/Rect` de cada una en puntos PDF.

**No encontré en la documentación de PdfPig ninguna afirmación de que exponga
`/Ink`, su `/C` (color) ni su `/BS`.** No digo que no pueda; digo que **no lo he
comprobado** y que dar por hecho que sí es exactamente el error que este ADR
existe para evitar. **Comprobarlo es criterio de aceptación de la FASE C1**, con
los PDF reales del dueño delante, y **antes** de escribir una línea de la capa de
anotaciones. Si PdfPig no llega, la alternativa es leer el diccionario del PDF a
mano (son objetos, no imágenes) o quedarse en Python **solo para esa pieza**, que
🔴 el dueño tendría que autorizar.

Esto es lo más frágil del plan entero y por eso va en la fase C1, no en la C7.

---

## 5. Base, Excel y reportes

### 5.1 SQLite — sin cambio de esquema

**`Microsoft.Data.Sqlite` 10.0.11** (2026-08-11), **MIT**, depende de
`SQLitePCLRaw.bundle_e_sqlite3 >= 2.1.12`
(`https://www.nuget.org/packages/Microsoft.Data.Sqlite/`, consultada 2026-09-04).

**El esquema NO se toca.** `docs/ARQUITECTURA.md` es la especificación y manda
sobre este ADR (`CLAUDE.md` §7). Medido allí (§200-259, con el motor, no leyendo
el DDL): **9 tablas, 104 columnas, 8 índices propios, versión 14**, SQLite motor
3.50.4. El programa en C# **abre la misma base del dueño y sigue en la 14**.

Tres condiciones que vienen del documento y que son criterio de aceptación, no
recomendaciones:

1. **Las claves foráneas hay que encenderlas en cada conexión** (§1.5). En
   `Microsoft.Data.Sqlite` eso es `PRAGMA foreign_keys = ON` por conexión, o
   `Foreign Keys=True` en la cadena. **NO he comprobado** cuál de las dos formas
   usa este proveedor por omisión: se mide.
2. **El texto del usuario jamás se concatena en una instrucción** (§1.7). En C#
   eso es `SqliteParameter` siempre, y es cazable con un `grep` — igual que la
   auditoría `pruebas/auditoria_sql.py` que ya existe.
3. **Fechas TEXT ISO-8601, booleanos INTEGER 0/1, números con cero delante TEXT**
   (§1.2, §1.3, §1.4). C# tipa más fuerte que Python y la tentación de meter
   `DateTime` o `bool` en la columna es mayor. Se convierte en el borde, no en la
   tabla.

### 5.2 Excel de ida y vuelta

| Opción | Licencia | Fuente y fecha |
|---|---|---|
| **ClosedXML 0.105.1** (2026-07-25) | **MIT**; depende de `DocumentFormat.OpenXml (>= 3.1.1 && < 4.0.0)` y de `SixLabors.Fonts` | `https://www.nuget.org/packages/ClosedXML/`, consultada 2026-09-04 |
| **DocumentFormat.OpenXml** a secas | MIT | (dependencia de la anterior) |

**Recomiendo ClosedXML.** Motivo con cifra: el Excel del agente tiene **18
propiedades estructurales** que hay que reproducir —hoja, congelado en A7, 16
títulos y anchos, tinta `16233A`, fondo `FFF6DC`, clave en gris 8, 7 menús Sí/No,
fecha límite en rojo `A62E24` (`DECISIONES.md:854-856`)—; ClosedXML expresa cada
una en una línea, OpenXML crudo en varias. El dueño llamó *«perfecto»* a ese
Excel: reproducirlo no es opcional.

⚠️ **`SixLabors.Fonts` entra por la puerta de atrás.** Es una dependencia que este
proyecto no pidió; hay que **pesarla en la C0**, porque la regla 3 de `CLAUDE.md`
(«sin pandas») es en el fondo una regla contra dependencias que inflan sin aportar,
y el dueño la reescribió para C# como *«sin dependencias que inflen sin aportar»*
(`DECISIONES.md:1864`).

### 5.3 Reportes en PDF para los jefes

| Opción | Licencia | Fuente y fecha |
|---|---|---|
| **PDFsharp 6.2.4** (2026-01-06) | **MIT** | `https://www.nuget.org/packages/PDFsharp/`, consultada 2026-09-04 |
| **QuestPDF 2026.8.0** (2026-08-24) | **Doble**. Literal de NuGet: *«The library is free for individuals, non-profits, open-source projects, and organizations under $1M in annual gross revenue.»* | `https://www.nuget.org/packages/QuestPDF/`, consultada 2026-09-04 |

**Recomiendo PDFsharp (MIT).** Dos motivos, y ninguno es de gusto:

1. **La licencia comunitaria de QuestPDF depende de quién use el programa.** El
   dueño lo usa para un trabajo de iglesia; *non-profits* encaja. Pero eso es una
   lectura mía de una frase de marketing en NuGet, y **no he leído el texto legal
   completo**. Un ADR no debería dejar al proyecto dependiendo de mi lectura de una
   licencia. **MIT no plantea la pregunta.** 🔴 Si el dueño prefiere QuestPDF por
   su API, esto lo mira el abogado, no yo.
2. **El precedente ya está escrito, y con número.** `DECISIONES.md:859-862`:
   *«Decisión: `reportlab` NO se añade. Lo que el informe del viejo necesita de un
   motor de dibujo son dos operadores de PDF crudo (`re f`, `l S`) más color de
   texto: cuatro líneas. `reportlab` costaba 8,1 MiB / 350 archivos sobre 217. Se
   escribió a mano en `reportes/formato.py`.»* **Ese informe ya está resuelto con
   cuatro líneas.** Portar `reportes/formato.py` a C# es más barato que cualquiera
   de las dos bibliotecas, y ya se sabe que funciona: 14 de 15 rasgos iguales al
   del viejo (`DECISIONES.md:856-857`, según el programador).

**Recomendación fina:** portar `reportes/formato.py` primero; PDFsharp solo si al
medirlo el porte cuesta más que la biblioteca.

---

## 6. Interfaz: los seis requisitos del dueño contra los controles de WinUI 3

Fuente única de esta sección salvo donde se diga:
`https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/scroll-controls`
y `.../items-repeater`, ambas consultadas **2026-09-04**.

### 6.1 «Rápido con 3 000 documentos» y listas virtualizadas

`ItemsRepeater`, literal:

> `ItemsControl` and ItemsRepeater both enable customizable collection experiences,
> but **ItemsRepeater supports virtualizing UI layouts**, while ItemsControl does not.

> Items shown by the ItemsRepeater are arranged by a `Layout` object… **When used
> with an ItemsRepeater, the Layout object enables UI virtualization.** The layouts
> provided are `StackLayout` and `UniformGridLayout`.

Y **`ItemsView`**, que es el control completo
(`https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/itemsview`,
consultada 2026-09-04): *«Like the list view and grid view controls, the items view
can use UI and data virtualization»*.

**Esto es lo que hoy no existe.** El defecto medido que originó la queja
(`DECISIONES.md:1767-1770`): inicio con **186 widgets**, bajado a 82, **2,6 ms por
widget**; y con 12 casos inventados ya eran 131 widgets contra un techo de 120. La
virtualización rompe esa relación: los widgets dejan de crecer con los datos.

⚠️ **Y una advertencia que la fuente da y que este proyecto ya sufrió:**
*«ItemsRepeater… doesn't contain any built-in scrolling like a ListView… you should
provide scrolling functionality by wrapping it in a ScrollViewer control.»* Un
`ItemsRepeater` sin `ScrollView` alrededor **no baja** — que es literalmente la
queja del dueño. Va como criterio de aceptación, no como consejo.

**Recomiendo `ItemsView`** (trae `ScrollView` en su plantilla, accesible por
`ItemsView.ScrollView`, y da selección y teclado de serie) y bajar a
`ItemsRepeater + ScrollView` solo donde la tarjeta sea muy a medida.

### 6.2 «Debo poder hacer scroll down en cualquier parte»

`ScrollView` frente a `ScrollViewer`, literal:

> The `ScrollView` control is similar in behavior and usage to the `ScrollViewer`
> control, but is based on `InteractionTracker`, has new features such as
> animation-driven view changes, and is **designed to ensure full functionality with
> ItemsRepeater**.

Y la diferencia que decide, literal:

> In the `ScrollViewer` control, the `VerticalScrollBarVisibility` and
> `HorizontalScrollBarVisibility` properties control **both the visibility of the
> scrollbars and whether scrolling in a particular direction is allowed**… In the
> `ScrollView` control… **control only the visibility of the scrollbars.**

> The defaults are: `VerticalScrollBarVisibility="Auto"`,
> `HorizontalScrollBarVisibility="Auto"`

**Esto importa exactamente aquí.** El defecto de Tk medido
(`DECISIONES.md:1813-1817`) es que la fila con `weight=1` *«se queda con 2 px»* y
*«el único lienzo con barra mide 1 px»*: la pantalla no bajaba porque la
disposición no dejaba sitio, no porque faltara una barra. En `ScrollView`, pedir
que **no** se pueda bajar es imposible: `Disabled` no existe para él. La regla
para el programa nuevo se puede escribir en una línea y verificar con un `grep`:

> **Toda pantalla va dentro de un `ScrollView`. `ScrollViewer` con
> `VerticalScrollBarVisibility="Disabled"` está prohibido.**

### 6.3 Visor de PDF con zoom y arrastre

Literal:

> To enable zoom by user interaction, set the `ZoomMode` property to `Enabled` (it's
> `Disabled` by default)… The defaults are: `MinZoomFactor="0.1"`,
> `MaxZoomFactor="10.0"`

Y para que la imagen entre ajustada y se pueda arrastrar después de acercar,
literal:

> To constrain the image to the ScrollView's viewport, set the `ContentOrientation`
> property to `None`. Because the scrollbar visibility is not tied to this
> constraint, **scrollbars appear automatically when the user zooms in.**

**Los `−` `Ajustar` `+` del viejo son tres líneas:** `ZoomTo`, `ZoomBy`,
`ContentOrientation="None"`. Hoy eso es la deuda DC-2 y la queja *«el visor es
lento y tosco»* (`DECISIONES.md:1348`).

### 6.4 «Asignar desde cualquier lugar»

No es un control: es arquitectura. Hoy el desplegable de la tarjeta de Revisar
dice *«todavía no está conectado»* (`DECISIONES.md:1849-1851`). En el programa
nuevo **una sola clase de servicio** `AsignacionDeCompanero` la usan la tarjeta,
la corrección y la lista, y el criterio de aceptación es medible: **una prueba que
llame a la misma operación desde los tres sitios y compruebe que la fila de
`asignaciones` sale idéntica.**

### 6.5 «Ni un párrafo en pantalla»

`InfoBar` es el control de aviso de una línea con cierre. **NO he consultado su
página oficial** en esta ronda y no afirmo sus propiedades; lo que sí es firme es
la regla del dueño y ya está escrita (`DECISIONES.md:1323`, «Regla de interfaz: el
texto no ocupa el programa»). Se verifica como ya se verifica hoy: contando
caracteres del aviso en la ventana montada.

### 6.6 Lo que NO cambia de la interfaz

*«La interfaz de la vieja NO me gusta; me gusta más la nueva»*
(`DECISIONES.md:1413-1416`). **La cara del programa actual se conserva.** Lo que
cambia es el motor de dibujo, no el diseño. Los mockups siguen valiendo.

---

## 7. Herramientas: qué instalar, y que WinUI 3 se compila sin Visual Studio

### 7.1 Sí se compila sin Visual Studio, y está documentado

`https://learn.microsoft.com/en-us/windows/apps/get-started/start-here`
(consultada 2026-09-04, `ms.date: 2026-08-31`) tiene una pestaña **«Command line»**.
Literal:

> **Prerequisites**
> - Windows 10 version 1809 (build 17763) or later
> - Developer Mode enabled (`ms-settings:developers`)
> - **.NET 10 SDK** or later (verify with `dotnet --version`)

```powershell
dotnet new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates
dotnet new winui -n MyWinUIApp
cd MyWinUIApp
dotnet build
dotnet run
```

> The template includes `Microsoft.Windows.SDK.BuildTools.WinApp`, which hooks into
> the .NET CLI `run` target…

**Respuesta al punto 7 del pase: sí. Visual Studio no hace falta.** El Windows SDK
llega **por NuGet** dentro de la plantilla — que es justo lo que el pase
preguntaba. Medido en esta máquina: **no hay `C:\Program Files (x86)\Windows Kits\10`**
y no hace falta que lo haya.

### 7.2 Qué instalar exactamente — **no instalo nada, la descarga la autoriza el dueño**

Medido con `winget search` (solo consulta) el **2026-09-04** en esta máquina,
`winget v1.29.290`:

```
Name                            Id                           Version
Microsoft .NET SDK 10.0         Microsoft.DotNet.SDK.10      10.0.400
Microsoft .NET SDK 9.0          Microsoft.DotNet.SDK.9       9.0.317
Microsoft .NET SDK 8.0          Microsoft.DotNet.SDK.8       8.0.424
Microsoft .NET SDK 11.0 Preview Microsoft.DotNet.SDK.Preview 11.0.100-preview.7.26381.103
```

**Se instala exactamente uno:**

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact
```

Más un interruptor que **no es una descarga**: Modo de desarrollador, en
Ajustes → Sistema → Para programadores (`ms-settings:developers`).

**Y nada más.** No Visual Studio (≈ varios GB), no Windows SDK suelto, no
`winget configure -f https://aka.ms/winui-config` — ese archivo de configuración
que la misma página recomienda **instala Visual Studio 2026 entero**, y aquí no
hace falta.

### 7.3 Por qué .NET 10 y no el 8 ni el 9 que dice el pase

`https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core`
(consultada 2026-09-04). Valores literales de la tabla:

| Versión | Publicada | Tipo | Fin de soporte |
|---|---|---|---|
| **.NET 10** | 2025-11-11 | **LTS** | **2028-11-14** |
| .NET 9 | 2024-11-12 | STS | **2026-11-10** |
| .NET 8 | 2023-11-14 | LTS | **2026-11-10** |
| .NET 6 | 2021-11-08 | — | 2024-11-12 (fuera) |

**Hoy es 2026-09-04: .NET 8 y .NET 9 mueren los dos en 67 días.** Empezar un
programa nuevo sobre cualquiera de ellos sería nacer sin soporte antes de la
tercera fase. **.NET 10 LTS**, y el TFM de ejemplo de la propia documentación es
`net10.0-windows10.0.26100.0`.

### 7.4 Windows App SDK

`https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-channels`
(consultada 2026-09-04, `ms.date: 2026-08-25`). Literal: canal **Stable**,
**2.4.0**, *«Released: 08/13/2026»*. Y de la tabla de ciclo de vida: familia
**2.0**, publicada 2026-04-29, parche 2.4.0, soporte **«Current»**, **fin de
servicio 2027-04-29**.

⚠️ **Ocho meses de servicio.** No es un problema —hay versión mayor cada seis meses
como mucho, y subir es cambiar un número— pero **es deuda con fecha**, y va a
`PENDIENTES.md`.

---

## 8. Pruebas: las 1 159 son la especificación, no se tiran

### 8.1 Qué se puede comprobar sin ventana

Medido (§1): **749 de 1 182 métodos (63%) no importan `tkinter` ni `interfaz`**.
Son esquema, migraciones, extracción, normalización, validación, reconciliación
del Excel, reportes, procedencia. **Ésos se portan uno a uno y son el contrato del
núcleo.** Los otros 433 dependen de una ventana Tk que dejará de existir: se
**reescriben** contra los controles nuevos, no se portan.

### 8.2 El marco

Fuente: `https://learn.microsoft.com/en-us/windows/apps/winui/winui3/testing/`
(consultada 2026-09-04). Lo que dice, en corto:

- La plantilla es **Unit Test App (WinUI in Desktop)** para C#.
- Las pruebas que tocan la UI se marcan con **`[UITestMethod]`**, no `[TestMethod]`,
  para que corran en el hilo de la interfaz.
- El TFM del proyecto de pruebas hay que ajustarlo al del proyecto WinUI (p. ej.
  `net10.0-windows10.0.26100.0`, no `net10.0`).
- **Y la recomendación que decide la arquitectura entera**, literal del resumen de
  esa página: *«It's recommended that you refactor any code to be tested by pulling
  it out of the main app project and placing it into a library project. Both your
  app project and your unit test project can then reference that library project.»*

**Eso es exactamente la separación que este ADR propone:** un proyecto
`Fichas.Nucleo` (biblioteca, sin ventana) donde viven las 14 758 líneas de reglas,
y un `Fichas.App` (WinUI 3) que solo dibuja. Las 749 pruebas del núcleo corren
**sin abrir ventana** y sin `[UITestMethod]`.

**Recomiendo MSTest**, por una razón y no por gusto: es el marco que usa la
plantilla oficial de Microsoft para WinUI y el único donde `[UITestMethod]` está
documentado. xUnit y NUnit sirven para el núcleo, pero tener **dos marcos** en un
proyecto de una persona es peor que tener uno.

### 8.3 Cómo se mide el rendimiento por pantalla

El criterio del dueño es **≤ 0,2 s por clic con 3 000 documentos**. Hoy hay una
herramienta que ya hace justo eso —`medir_pantallas.py`, con la que el supervisor
midió `mostrar_inicio` 1,60 s → 0,20 s (`DECISIONES.md:1758-1763`)— y su
equivalente en C# es lo que hay que escribir **en la FASE C0, antes que ninguna
pantalla**, para que ninguna fase se cierre «rápida» sin número.

Dos condiciones que vienen del precedente medido y no son negociables:

1. **Máquina en silencio.** El supervisor mide con `Get-Process python* = 0`. El
   equivalente aquí es sin compilación ni prueba corriendo.
2. **Base grande, no la de 2 casos.** `DECISIONES.md:1856`: *«Rápido con 3 000
   documentos, no con 2»*. Y el aviso medido de `DECISIONES.md:1785-1786`: con 12
   casos ya eran 131 widgets contra un techo de 120. **Una cifra tomada con 2 casos
   no dice nada**, y ése es el error que la C0 existe para no repetir.

---

## 9. El coste honesto: cuánto se tarda en volver a tener lo que hoy funciona

El dueño dijo *«perderemos menos tiempo así»*. Le debo la cuenta, no el aplauso.

**Lo que se conserva sin tocar** (y es mucho):

| Qué | Por qué se conserva |
|---|---|
| **El esquema: 9 tablas, 104 columnas, versión 14** | `docs/ARQUITECTURA.md` manda sobre cualquier plan (`CLAUDE.md` §7). No se migra ni se renombra |
| **La base real del dueño** | Se abre tal cual. SQLite es SQLite |
| **El Excel del compañero y el informe de los jefes** | Formato ya validado: 18/18 y 14/15 rasgos (según el programador) |
| **Los dos PDF reales y el grupo de seis hojas** | Son el material de prueba, y ya tienen resultado conocido |
| **Las 1 159 pruebas** | Son la especificación escrita de las reglas. Se leen aunque no se ejecuten |
| **Los mockups y el aspecto** | El dueño dijo que la cara del nuevo le gusta |
| **Los tres `.onnx`** | Solo si se va por la vía B del OCR |

**Lo que se pierde por el camino, y hay que decirlo:**

1. **Todo lo entregado en el ciclo 6 en Tk.** El pase 1 (Guardar y la franja) está
   a medio terminar con un programador dentro ahora mismo. Se termina y se entrega
   —el dueño ya lo decidió (`DECISIONES.md:1833-1835`)— pero **su código no viaja**.
2. **La deuda del ciclo 5 (DC-1 a DC-14) se evapora en su mayor parte**, porque 10
   de las 14 son defectos de la interfaz Tk que dejará de existir. Las que sí
   sobreviven son de datos: **DC-4** (archivo copiado sin MRN entra sin marca),
   **DC-13** (una prueba no aplica migraciones a mano), **DC-14** (historial de
   marcas del compañero). Ésas hay que arrastrarlas.
3. **Las preguntas abiertas del esquema siguen abiertas** (P-2, P-3, P-4, P-6,
   P-7, P-8, P-10 a P-16). Cambiar de lenguaje no contesta ninguna.
4. **La calibración de las seis casillas de ordenanzas.** Sigue sin hacerse
   —hacen falta tres formularios con la verdad conocida (`ESTADO.md:236-239`)— y en
   C# habrá que rehacerla contra el nuevo rasterizado, porque el umbral depende del
   contraste de la imagen y la imagen la produce otro motor.
5. **Un año de trampas ya pisadas.** Los seis puntos de «Reglas de no regresión»,
   el `medianBlur`, el tope de 3 500 px, `_suelo()`, el orden de fusión del canvas.
   Están escritos, pero **escrito no es medido**: cada uno vuelve a necesitar su
   número en C#.

**La cuenta de fases.** Diez fases pequeñas (§ `PENDIENTES.md`, «Fases del programa
en C#»). **No doy una cifra en días**, y ésa es una respuesta, no una evasiva: no
tengo ninguna medición de cuánto tarda este equipo por fase en C#, y el proyecto
tiene el precedente de que el planificador se equivocó estimando lo que no midió
(`PENDIENTES.md`, marcado `⚠️plan`). Lo que sí puedo acotar con número:

- **14 758 líneas de lógica** a portar, con **749 métodos de prueba** que dicen si
  el porte está bien. Es trabajo mecánico y verificable.
- **11 445 líneas de interfaz** a **rehacer**, no portar. Es trabajo de diseño y no
  hay prueba automática que diga si quedó bien: lo dice el dueño mirándolo.
- **Tres piezas sin sustituto conocido**, cada una capaz de comerse una fase
  entera: las anotaciones `/Ink` con color (§4.2), el pre/posproceso de PP-OCRv5 si
  hace falta la vía B (§3.2), y la calibración de las casillas.

**Mi lectura, para que el dueño decida con ella delante:** *«perderemos menos
tiempo así»* es probablemente cierto **a partir de la tercera o cuarta fase**, y
**falso en las dos primeras**. Entre hoy y el momento en que el C# haga lo que el
`.exe` de Python ya hace, el dueño va a tener menos programa, no más. **Por eso la
regla del §10 no es un detalle de proceso: es la que hace que ese hueco no duela.**

---

## 10. La regla que gobierna todas las fases

**El `.exe` de Python sigue siendo lo que usa el dueño hasta que el C# lo iguale,
y ninguna fase del C# borra nada del Python.** Lo decidió el dueño
(`DECISIONES.md:1833`) y lo repito aquí porque es la única red que tiene este plan.

Y una consecuencia que nadie ha escrito: **`C:\Users\josem\Fichas-entrega\Fichas`
no se toca.** El programa en C# se entrega en una carpeta **distinta**, y el dueño
decide cuándo cambia de una a otra. Dos programas conviviendo sobre **la misma
base** en `Documentos\Fichas` es un riesgo real y 🔴 **necesita decisión del
dueño**: o se acepta (los dos escriben en la misma base, con el peligro de que uno
migre y el otro no la entienda), o el C# arranca con `--carpeta-de-datos` sobre una
copia hasta que sustituya al viejo. **Recomiendo lo segundo**, y la bandera ya
existe en el Python (`DECISIONES.md:1029-1030`).

---

## 11. Qué NO cubre este ADR, y qué no pude verificar

**Preguntas que me hice y nadie me pidió hacerme:**

- **¿La lista de opciones del pase estaba completa?** En OCR y PDF, no del todo: el
  pase nombra PDFiumViewer (abandonado) y no nombra **PDFtoImage**, que es el
  envoltorio de PDFium vivo hoy. En reportes, el pase nombra QuestPDF y PdfSharp y
  **no considera la opción que este proyecto ya eligió una vez con número**: no usar
  ninguna biblioteca (§5.3). En interfaz, el pase pide «qué controles lo dan de
  serie» y la respuesta honesta es que **`ItemsRepeater` NO da el desplazamiento de
  serie** — hay que envolverlo.
- **¿Y si el problema no era Tk?** El supervisor midió que *«el "no se puede bajar"
  es un defecto de disposición, no de Tk»* y que *«se arreglaría en Tk en horas»*
  (`DECISIONES.md:1813-1819`). El dueño lo sabe y decidió cambiar igual. **No
  reabro su decisión**; lo dejo escrito porque si dentro de tres fases alguien
  pregunta «¿por qué nos fuimos de Tk?», la respuesta honesta es «porque el dueño lo
  decidió», no «porque Tk no podía».

**Lo que NO cubre:**

- **No cubre el ROADMAP.** Es del dueño (`CLAUDE.md`).
- **No cubre el aspecto visual.** Es del diseñador, sobre los mockups que ya existen.
- **No cubre las preguntas abiertas del esquema** (P-2 a P-16). Siguen donde estaban.
- **No cubre la FASE 7 (contactos con el líder)**, que sigue sin especificación.
- **No cubre firma de código ni antivirus.** Un `.exe` nuevo, sin firmar, copiado a
  la PC de un trabajo puede ser bloqueado por SmartScreen o por el antivirus de la
  empresa. **Hoy pasa lo mismo con el de Python y nadie lo ha comprobado**; con un
  binario nuevo el riesgo se renueva. No lo investigué: no me lo pidieron y no
  tengo la máquina.
- **No cubre accesibilidad.** La documentación de `ItemsRepeater` avisa literal:
  *«ItemsRepeater does not provide a default accessibility experience.»* Nadie ha
  dicho si esto importa aquí.

**Lo que NO pude verificar, una por una:**

1. **El tamaño de una app WinUI 3 autocontenida.** No hay cifra oficial. Se mide en
   la C0.
2. **El arranque.** Sin SDK no hay nada que cronometrar. Se mide en la C0.
3. **Si `PublishSingleFile` funciona de verdad para WinUI 3.** Dos páginas de
   Microsoft se contradicen (§2.1). Se mide en la C0.
4. **Si PdfPig expone `/Ink` con su color y grosor.** No lo afirma su documentación
   y no lo he probado. Se mide en la C1. **Es el mayor riesgo del plan.**
5. **Si `modelos/` trae el diccionario de caracteres latino.** Sin él la vía B del
   OCR no arranca. **NO lo he comprobado.**
6. **Qué idiomas de OCR hay en la PC del trabajo, y si añadir uno pide
   administrador.** Solo el dueño puede medirlo, con una línea que va en la C0.
7. **Si activar el Modo de desarrollador en esta máquina pide administrador.** Esta
   sesión corre sin él (medido).
8. **Nada de esta máquina se ha compilado.** No hay SDK y no lo instalo: la
   descarga la autoriza el dueño (`DECISIONES.md:1821`). **Todo lo de este ADR sobre
   compilar es documentación oficial citada, no experiencia propia.**
9. **El texto legal de la licencia comunitaria de QuestPDF.** Cité la frase de
   NuGet; no leí el documento. Es del abogado.
10. **La base real del dueño.** `docs/ARQUITECTURA.md:328-333` deja escrito que
    **nadie ha comprobado que esté en la 14**. Si el C# la abre esperando la 14 y
    está en la 13, falla el primer día. Se comprueba antes de la C2.

---

## Fuentes

Todas consultadas el **2026-09-04**.

| # | URL | Qué se tomó |
|---|---|---|
| 1 | `https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps` | `WindowsAppSDKSelfContained`; xcopy-deploy; «cannot produce a single-file EXE» |
| 2 | `https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app` | Las 6 propiedades de `PublishSingleFile`; extracción a temp; «WPF and WinForms… broader set of configurations» |
| 3 | `https://learn.microsoft.com/en-us/windows/apps/get-started/start-here` | Pestaña «Command line»: .NET 10 SDK, Developer Mode, `dotnet new winui` |
| 4 | `https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-channels` | Stable **2.4.0**, 08/13/2026; fin de servicio 2027-04-29 |
| 5 | `https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core` | .NET 10 LTS → 2028-11-14; .NET 8 y 9 → 2026-11-10 |
| 6 | `https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine` | `OcrWord` con posición; Windows 10 10240 |
| 7 | `https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine.availablerecognizerlanguages` | «A language pack must be installed on the device to be used» |
| 8 | `https://learn.microsoft.com/en-us/uwp/api/windows.data.pdf.pdfpage` | `RenderToStreamAsync`; sin métodos de texto ni anotaciones |
| 9 | `https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/scroll-controls` | `ScrollView` vs `ScrollViewer`; `ZoomMode`; `ContentOrientation="None"` |
| 10 | `https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/items-repeater` | «supports virtualizing UI layouts»; «doesn't contain any built-in scrolling» |
| 11 | `https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/itemsview` | «can use UI and data virtualization» |
| 12 | `https://learn.microsoft.com/en-us/windows/apps/winui/winui3/testing/` | `[UITestMethod]`; sacar el código a una biblioteca |
| 13 | `https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview` | `%TEMP%/.net`; `IncludeAllContentForSelfExtract` «not recommended» |
| 14 | `https://www.nuget.org/packages/Microsoft.Data.Sqlite/` | 10.0.11, 2026-08-11, MIT |
| 15 | `https://www.nuget.org/packages/ClosedXML/` | 0.105.1, 2026-07-25, MIT; depende de OpenXml y SixLabors.Fonts |
| 16 | `https://www.nuget.org/packages/PdfPig/` | 0.1.16, 2026-08-22, Apache-2.0; AcroForms read-only |
| 17 | `https://www.nuget.org/packages/PDFtoImage/` | 5.4.0, 2026-08-16, MIT; `bblanchon.PDFium` |
| 18 | `https://pdfium.googlesource.com/pdfium/+/refs/heads/main/LICENSE` | BSD-3-Clause |
| 19 | `https://www.nuget.org/packages/PDFsharp/` | 6.2.4, 2026-01-06, MIT |
| 20 | `https://www.nuget.org/packages/QuestPDF/` | 2026.8.0, 2026-08-24; «free for… organizations under $1M in annual gross revenue» |
| 21 | `https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime/` | 1.29.0, 2026-08-12, **147,83 MB** |

**Mediciones propias en esta máquina, 2026-09-04** (comandos y salida literal en
§0, §1, §3.1, §7.2): `dotnet --info`, `where msbuild`, `winget --version`,
`winget search`, recuento de líneas y de métodos de prueba,
`[Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages`, Modo de
desarrollador, Windows Kits, disco libre (82,8 GB), privilegios de la sesión.
