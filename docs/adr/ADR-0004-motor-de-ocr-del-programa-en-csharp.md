# ADR-0004 — Con qué motor lee los campos el programa en C#

- **Fecha:** 2026-09-04
- **Estado:** propuesta. La recomendación es única (§7) y **la decisión es del dueño**.
- **Decide:** planificador, sobre encargo del supervisor
- **Afecta a:** `PENDIENTES.md` (FASE C0 criterio C0-8; FASE C3 criterios C3-1 y
  C3-2); corrige dos cifras del **ADR-0003 §3.2 y §2.5**, que es inmutable y por
  eso se corrige aquí y no allí (`CLAUDE.md` §7).
- **Inmutable.** Si se revierte, se escribe un ADR nuevo.

---

## 0. Por qué existe este ADR

La FASE C0 midió el OCR nativo de Windows y **no llegó**. `DECISIONES.md`
(«Cerrada la FASE C0», `216f5ab`), comprobado por el supervisor:

| | `Windows.Media.Ocr` es-MX | Python + PP-OCRv5 latinos |
|---|---|---|
| Cédulas completas, dos PDF | **0 de 5** | 4 de 5 |
| Forma del fallo | siempre `###-## #-####` | — |
| Grupo de seis hojas | 10 personas (dos hojas en inglés dan cero) | 12 |

El ADR-0003 §3.3 recomendó la vía A y dejó escrito que, si no llegaba, «entra la
vía B con su coste dicho». Este ADR pone ese coste, y añade dos vías que el
ADR-0003 no contempla.

### 0.1 ⚠️ Las cifras de esos dos PDF no valen, y el motivo es más grave que «son viejos»

El dueño dijo el 2026-09-04, durante la redacción de este ADR, que los dos PDF del
Escritorio son del programa antiguo y no son documentos correctos. Envió siete
reales, y al medirlos apareció **la diferencia que invalida todo lo anterior**.

`DECISIONES.md` («La línea base DE VERDAD», `29cd00c`), comprobado por el
supervisor con `pypdf`:

> **estos NO tienen capa de texto, son escaneos de verdad.** Los anteriores sí la
> tenían, y por eso todo parecía leerse tan bien.

**Eso no es un matiz de versión: es otro problema.** Un PDF con capa de texto se
puede leer sin OCR; los siete reales, no. Toda comparación de motores hecha sobre
`BARC2608_*` y `cb2f18be-SURB2609_*` medía un caso que **no ocurre en la vida
real** de este programa.

> En este documento, cada cifra tomada sobre esos archivos va marcada
> **⚠️doc-no-válido**. **No sirve para fijar un umbral y tampoco para ordenar
> motores**, porque el material no representa el caso real. Se conserva solo
> donde el argumento no depende de ella.

### 0.1bis La línea base real — siete escaneos `CASP2609`, con el Python de hoy

Mismo commit, medido por el supervisor con importación real, base limpia:

| | |
|---|---|
| Documentos leídos | **7 de 7**, 0 ilegibles, 0 rechazados |
| Segundos por hoja | **9,3 a 18,5** (mediana ~12,6) |
| Personas, y con nombre | 7 de 7 · **7 de 7** |
| **Cédulas guardadas** | **5 de 7** |
| Fecha de viaje · unidad | 7 de 7 · 7 de 7 |
| Número de caso | 7 de 7, pero **uno leyó `CASD2609` por `CASP2609`** |
| **Casillas de ordenanza** | **0 de 7** (§6bis) |

**Las dos cédulas que faltan son otra vez el hecho 2 de §0.3:** el OCR leyó
`###-####-###A` y la validación de once dígitos las tiró. **No es fallo de
lectura, y ningún motor de este ADR lo cambia.**

**Ésta es la línea base que hay que batir, y sobre estos siete documentos se
escribe el criterio de aceptación** (§8).

### 0.2 Y el criterio de decisión cambió de orden — lo cambió el dueño

Palabras suyas, en `DECISIONES.md` (`82e9b08`): *«No habría problemas, yo puedo
digitarlo a mano, porque tengo que verificar todas las informaciones de cada
documento siempre.»*

**El orden que este ADR aplica, y no el del encargo original:**

| | Criterio | Por qué |
|---|---|---|
| **1** | **Nunca perder ni inventar en silencio lo que el papel decía** | Es la regla permanente 1 y el requisito 9. Es lo único no negociable |
| **2** | Ahorro de tecleo (precisión) | El dueño verifica documento por documento de todos modos. Es comodidad, **no corrección** |
| **3** | Sin red | Regla permanente 2 |
| **4** | Doble clic sin instalación | Regla permanente 2 |
| **5** | Tamaño y tiempo | |

**Esto degrada el criterio (a) del encargo** —«leer los MRN al menos tan bien como
hoy, 4 de 5»— de bloqueante a deseable. Lo digo explícito porque cambia qué vías
quedan vivas: con el criterio viejo, la vía A' moría; con el nuevo, no.

### 0.3 Dos hechos medidos que reencuadran el problema

**Hecho 1 — la causa del fallo está identificada.** `DECISIONES.md` (`82e9b08`),
mirado por el supervisor rasterizando a 3 500 px y recortando la banda con las
coordenadas que guarda `procedencia_campo`: **una línea vertical de la tabla del
formulario atraviesa el número por el grupo del medio.** No es la resolución ni la
letra. Eso explica el `###-## #-####` y explica por qué **subir la resolución lo
empeora**: agranda la raya igual que el dígito.

**Hecho 2 — el «4 de 5» nunca fue un fallo de OCR.** Mismo commit, comprobado en
la capa de texto del PDF con `pypdf`: **una de las cédulas termina en la letra
`A`**. El OCR la leyó bien; la rechazó **nuestra validación de once dígitos**, y
el campo quedó vacío. **Eso no es criterio de motor: es el requisito 9**, y
ninguna de las cinco vías de este ADR lo arregla ni lo empeora.

---

## 1. Lo que hay hoy en el repositorio — medido, no recordado

```
$ for f in modelos/*; do echo "$(stat -c%s "$f") $f"; done
1018508 modelos/ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx
4819576 modelos/ch_PP-OCRv5_det_mobile.onnx
7904513 modelos/latin_PP-OCRv5_rec_mobile.onnx
                                        TOTAL 13 742 597 B = 13,10 MiB
```

### 1.1 El diccionario de caracteres **sí existe**, y el ADR-0003 se equivocó

El ADR-0003 §3.2 escribió: *«el **diccionario de caracteres latino** — que **NO
está en el repositorio hoy**… Sin él, la vía B no arranca.»* Se marcó como no
comprobado. **Lo comprobé, y está**: va embebido en los metadatos del propio
`.onnx`, no en un `.txt`.

```
$ .venv/Scripts/python.exe -c "import onnxruntime as ort; ..."
=== modelos/latin_PP-OCRv5_rec_mobile.onnx
  claves metadata: ['character']
    character -> len 1004 | primeros 120: '!\n"\n#\n$\n%\n&\n'\n(\n)\n*\n+\n,\n-\n.\n/\n0\n1\n2 …'
  salidas: [('fetch_name_0', ['DynamicDimension.0', 'Reshape_524_o0__d2', 504])]

lineas: 503 | no vacias: 502 | no ASCII: 408
ñ True · Ñ True · á True · é True · í True · ó True · ú True · ü True
ç True · à True · è True · ô True · â True
```

**502 caracteres, y las 504 clases de salida cuadran** (502 + `blank` + espacio).
Están la `ñ`, los acentos del español y los del francés — lo que el ADR-0003 §3.1
señaló como la pérdida de la vía A. Los otros dos modelos no traen metadatos, y no
los necesitan.

**Consecuencia:** el impedimento que el ADR-0003 puso a la vía B **no existe**.

### 1.2 El peso real de onnxruntime en `win-x64` — la segunda corrección

El ADR-0003 §2.5 dio **147,83 MB** citando NuGet, y dijo que a `win-x64` le toca
«una fracción que **NO he medido**». La medí, sobre la copia `win-x64` de la
**misma versión 1.29.0** que este repositorio ya tiene instalada:

```
$ .venv/Scripts/python.exe -c "import onnxruntime; print(onnxruntime.__version__)"
1.29.0
$ find .venv/Lib/site-packages/onnxruntime -name "*.dll"
18093408  onnxruntime.dll                    ← 17,25 MiB
   21816  onnxruntime_providers_shared.dll   ←  0,02 MiB
18748728  onnxruntime_pybind11_state.pyd     ← binding de Python, NO va en C#
```

**17,27 MiB, no 147,83 MB.** Los 147,83 MB son el `.nupkg` con los binarios de
**todas** las plataformas; a Windows x64 le toca el 12%.

⚠️ **El matiz honesto:** medí el `onnxruntime.dll` que trae la rueda de Python,
no el que trae el paquete NuGet. Son la misma biblioteca nativa y la misma
versión, pero **no he desempaquetado el `.nupkg` para confirmarlo byte a byte**.
Descargarlo requiere permiso; se confirma en la fase con `dotnet publish` delante.

---

## 2. Vía A' — limpiar las líneas de la tabla antes de leer *(nueva)*

La abre el hecho 1: si lo que rompe el número es una raya de la tabla, quitarla
antes del OCR arregla la causa en vez del síntoma. **Hoy el preproceso es solo
`cv2.medianBlur(gris, 3)`** (regla de no regresión).

**Fuente oficial** —
`https://github.com/opencv/opencv/blob/4.x/doc/tutorials/imgproc/morph_lines_detection/morph_lines_detection.md`,
consultada 2026-09-04 (`docs.opencv.org` devuelve **HTTP 403** a la consulta
automatizada; uso el repositorio del proyecto, que es la misma fuente primaria).
Valor literal:

> In this tutorial you will learn how to: Apply two very common morphology
> operators (i.e. Dilation and Erosion), with the creation of custom kernels, in
> order to extract straight lines on the horizontal and vertical axes.

> You typically choose a structuring element the same size and shape as the
> objects you want to process/extract in the input image. For example, to find
> lines in an image, create a linear structuring element as you will see later.

El tutorial construye un núcleo `1×n` para las horizontales y `n×1` para las
verticales, con `n` derivado del tamaño de la imagen, y aplica `erode` seguido de
`dilate`. **El tutorial oficial extrae las líneas; restarlas de la imagen es el
paso siguiente y NO está en el tutorial** — lo digo para no atribuirle más de lo
que dice.

**Coste:**

| | |
|---|---|
| MB nuevos, en Python | **0.** `cv2` ya está (81,9 MiB medidos, §5) |
| MB nuevos, en C# | Ninguno con la vía B (§3): SkiaSharp puede erosionar y dilatar. Si se quisiera OpenCV: `OpenCvSharp4.runtime.win` **4.13.0.20260627**, **38,02 MB**, **Apache-2.0** (`https://www.nuget.org/packages/OpenCvSharp4.runtime.win`, consultada 2026-09-04) |
| Segundos | **NO medido.** Dos pasadas de morfología sobre 2705×3500 px son baratas comparadas con los 8,6–10,4 s del OCR, pero no lo he cronometrado y no lo estimo |
| Trabajo | Una función y su prueba. Es la vía más barata de todo el ADR |

⚠️ **Riesgo que hay que decir:** un núcleo demasiado agresivo **se come trazos del
propio texto** —el palo de una `1`, la barra de una `t`— y entonces el remedio
inventa un dígito distinto, que es exactamente lo que la regla permanente 1
prohíbe. Por eso A' **no puede ir sin control positivo**: hay que comparar lo
leído con y sin limpieza sobre los mismos documentos, y el umbral del núcleo se
calibra con documentos reales, no a ojo.

**A' no es una alternativa a las demás vías: es un preproceso que las mejora a
todas.** Y no rescata sola a la vía A: no toca las dos hojas en inglés que dieron
**cero personas** ⚠️doc-no-válido, que es un fallo de otra naturaleza.

---

## 3. Vía B — onnxruntime en C# con los mismos tres modelos

### 3.1 El coste que el ADR-0003 calculó ya no es el coste real

El ADR-0003 §3.2 dijo: *«No es «portar código»: es reimplementar una
biblioteca»*, y listó DB (detección), CTC (reconocimiento) y cls (orientación).
**Eso era cierto suponiendo que no existiera una biblioteca .NET que ya lo
hiciera. Existe.**

**`RapidOcrNet`** — `https://www.nuget.org/packages/RapidOcrNet/`, consultada
2026-09-04:

| | Valor literal |
|---|---|
| Versión | **4.1.0** |
| Publicada | **2026-08-30** |
| Tamaño del paquete | **12.23 MB** |
| Licencia | **Apache-2.0** |
| Marcos | **net8.0, net10.0** |
| Descargas | **32.3K** |
| Dependencias | `Clipper2 (>= 2.0.0)`, `Microsoft.ML.OnnxRuntime (>= 1.29.0)`, `Microsoft.ML.OnnxRuntime.Managed (>= 1.29.0)`, `SkiaSharp (>= 3.119.1)` |

Descripción, literal: *«Cross-platform OCR processing library using PaddleOCR ONNX
models, and based on original code from RapidAI's RapidOCR»*, y **«ships the
PP-OCRv5 models out of the box»**.

**Actividad** — `https://api.github.com/repos/BobLd/RapidOcrNet`, consultada
2026-09-04: `pushed_at` **2026-08-30**, `stargazers_count` **93**,
`open_issues_count` **2**, `archived` **false**, `license.spdx_id` **Apache-2.0**,
creado en 2024-08-05.

### 3.2 Y trae **los mismos modelos latinos**, no los chinos

`https://github.com/BobLd/RapidOcrNet` y su `README.md`, consultados 2026-09-04.
Los archivos que empaqueta:

| RapidOcrNet | Este repositorio |
|---|---|
| `ch_PP-OCRv5_mobile_det.onnx` | `ch_PP-OCRv5_det_mobile.onnx` |
| `ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx` | **idéntico** |
| `latin_PP-OCRv5_rec_mobile_infer.onnx` | `latin_PP-OCRv5_rec_mobile.onnx` |
| `ppocrv5_latin_dict.txt` | embebido en el `.onnx` (§1.1) |

**Es el reconocedor latino, que es justo lo que la regla de no regresión exige**
(*«Cargar explícitamente los modelos PP-OCRv5 del grupo latino… el español sube a
casi 100% solo con los latinos»*). No hay que convencer a la biblioteca de que
use los latinos: son los suyos por defecto.

⚠️ **NO he comprobado que sean el mismo archivo byte a byte** que los de
`modelos/`. Los nombres difieren en el orden de las palabras. **Verificarlo es
criterio de aceptación** (§8), y la comprobación barata ya está diseñada:
comparar el `ppocrv5_latin_dict.txt` de RapidOcrNet contra los **502 caracteres**
que §1.1 leyó de nuestro `.onnx`. Si coinciden, es el mismo reconocedor.

**Y si no coincidieran, no pasa nada:** el README documenta cargar los propios por
ruta, literal:

```csharp
ocr.InitModels(
    detPath:  "models/v5/ch_PP-OCRv5_det_server.onnx",
    clsPath:  "models/v5/ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx",
    recPath:  "models/v5/korean_PP-OCRv5_rec_mobile.onnx",
    keysPath: "models/v5/ppocrv5_korean_dict.txt");
```

Con la advertencia que el propio README da, literal: *«The recognizer's character
set is defined by the `*_dict.txt` file — it **must** match the `*_rec_*.onnx` you
load, otherwise the output will be gibberish.»*

### 3.3 Sin red — comprobado en la documentación, no supuesto

Es la regla permanente 2 y `extraccion/ocr.py` la protege hoy a propósito
(*«Pasar `model_path` tiene un segundo efecto… RapidOCR NO consulta ninguna URL»*).

README, literal: *«When using the NuGet package, these are copied to `models/v5/`
next to your binary automatically.»* Y sobre los únicos que sí se bajan: *«Because
the v6 files are large (~176 MB for all three sizes), they are **not** bundled in
the NuGet — you download them yourself.»*

**Los v5 se copian junto al binario; los v6 son los que se descargan.** Este
proyecto usa **v5**, así que no hay descarga. ⚠️ Que en ejecución no abra ningún
socket **NO lo he comprobado**: se comprueba en la fase, y es criterio de
aceptación (§8).

### 3.4 Qué hay que portar exactamente

**De DB, CTC y cls: nada.** Los tres los pone RapidOcrNet.

Lo que sí hay que escribir en C# es lo que **ya es nuestro y hoy está en Python**,
y que no cambia con esta decisión porque es lógica de negocio:

- `extraccion/bandas.py` (144 líneas) — anclas bilingües por parecido.
- `extraccion/geometria.py` (102) — solapamiento, rectángulos.
- `extraccion/campos.py` (138), `etiquetas.py` (146), `normalizacion.py` (259),
  `personas.py` (228), `recorte.py` (244), `casillas.py` (65),
  `formulario.py` (434), `anotaciones.py` (176), `rasterizado.py` (66).
- `extraccion/ocr.py` (108) — se **reduce**: la resolución de rutas y la
  configuración de versión por etapa las hace la biblioteca.

**Eso es la FASE C3, y hay que portarlo en cualquiera de las cinco vías** salvo la
D. No es coste de la vía B.

### 3.5 Precisión esperable

**El mismo modelo, el mismo diccionario, el mismo pre/posproceso de referencia**
(RapidOcrNet declara estar basado en el código de RapidAI/RapidOCR, que es lo que
usa el Python de hoy). La expectativa razonable es **la misma lectura**.

⚠️ **Y no la doy por hecha, por dos motivos concretos:**
1. RapidOcrNet **quitó OpenCV**: hace el proceso de imagen con **SkiaSharp y
   PContourNet**. El remuestreo y la extracción de contornos no son bit a bit los
   de OpenCV, y el posproceso de DB depende de los contornos.
2. El README **no publica ninguna cifra de precisión**: *«No benchmark or accuracy
   metrics are provided in the documentation.»* No encontré medición de precisión
   en formularios de esta biblioteca, ni propia ni de terceros.

**Por eso la primera tarea de la fase es medir, no cablear** (§8).

### 3.6 Tamaño de la vía B

| Pieza | MiB | Cómo lo sé |
|---|---|---|
| `onnxruntime.dll` + providers, win-x64 | **17,27** | medido (§1.2) |
| Los tres `.onnx` | **13,10** | medido (§1) |
| SkiaSharp nativo win-x64 | **NO medido** | el paquete `SkiaSharp` **4.151.2** (2026-09-03, **MIT**, **8.9 MB**) es multiplataforma; win-x64 es una fracción, como en §1.2 (`https://www.nuget.org/packages/SkiaSharp/`, consultada 2026-09-04) |
| Clipper2, RapidOcrNet gestionado | **NO medido** | código gestionado, del orden de 1 MB |
| **Suma de lo medido** | **30,4** | **el resto se cierra con `dotnet publish` en la fase** |

**Frente a los 147,83 MB del ADR-0003, y frente a los 189,0 MiB de la vía D.**

---

## 4. Vía C — Windows para el formulario, PP-OCRv5 solo para la banda del MRN

**La descarto, y no por tamaño ni por tiempo: por arquitectura.** La premisa que
la sostiene no se sostiene.

### 4.1 La banda del MRN **no se conoce antes de leer**

El encargo dice: *«el sistema ya guarda las bandas (`procedencia_campo.banda_x0..y1`),
así que la banda del MRN se conoce»*. Es cierto **para un caso ya importado**, y
esas coordenadas son justamente las que el supervisor usó para ver la raya. Pero
para **un PDF nuevo**, que es el caso de uso, no existen todavía.

Lo dice el propio módulo. `extraccion/bandas.py:1-8`, literal:

> Localizar una banda del formulario por su ANCLA de texto, nunca por coordenada.
>
> Un escaneo nunca cae dos veces en el mismo sitio: los margenes del escaner mueven
> todo unos milimetros, y una coordenada fija acaba capturando el encabezado en vez
> del dato.

Y es una **regla de no regresión** del proyecto (*«Medir las bandas sobre escaneos
reales, nunca sobre el PDF en blanco… los márgenes del escáner mueven las
proporciones»*).

**Consecuencia:** para saber dónde está la banda del MRN hay que **haber leído ya
las etiquetas impresas de la página**. La vía C no ahorra la lectura completa: la
exige antes.

### 4.2 Y el modo de fallo es silencioso

Si quien localiza el ancla es el OCR de Windows y el ancla no aparece, **no hay
banda, no hay segundo pase, y el campo sale vacío sin que nada avise**. Eso choca
de frente con el criterio 1 de §0.2.

Medido sobre los volcados de la espiga ⚠️**doc-no-válido**, a 3 500 px, comparando
cada línea del volcado con las etiquetas de `extraccion/etiquetas.py` con el mismo
parecido y el mismo corte de **0,85** que usa el sistema:

| Volcado | Líneas | Mejor parecido a `Membership Record Number` | ¿Llega a 0,85? |
|---|---|---|---|
| Hoja suelta 1 (español) | 87 | 0,17 | **no** |
| Hoja suelta 2 (español) | 80 | 0,18 | **no** |
| Grupo de seis hojas (inglés) | 462 | 0,45 | **no** |

Ninguna de las seis etiquetas ancla probadas llegó al corte en ninguno de los tres
volcados.

⚠️ **Lo que esta medición NO demuestra, y hay que decirlo:** el volcado trae una
caja por renglón (`[x= y= ancho= alto=] texto`, longitud media 69,9 caracteres),
así que el formato no está partiendo palabras y la comparación es justa. Pero en
las dos hojas en español el ancla **está en español en el papel** y el conteo
crudo lo confirma (`Nombre` 4 veces, `miembro` 4 veces, `Membership` 0). **El
sistema busca las dos formas**, así que el 0,17 no significa que Windows no lea
nada: significa que **ninguna línea suya reproduce la etiqueta completa**. La
causa exacta —renglón partido, etiqueta a dos líneas, o lectura mala— **no la he
aislado**, y sobre documentos que el dueño no reconoce no vale la pena aislarla.

**Aun así la vía C cae por §4.1, que no depende de ninguna medición.**

### 4.3 Y ni siquiera ahorraría lo que promete

Necesitaría igualmente cargar `onnxruntime` y al menos el reconocedor: **paga los
MB de la vía B y añade la complejidad de dos motores**, dos juegos de coordenadas
y dos modos de fallo. Ahorraría segundos de reconocimiento sobre una banda
pequeña —**NO medido**— a cambio de sumar el tiempo del OCR de Windows sobre la
página entera.

---

## 5. Vía D — la lectura se queda en Python, invocada desde C#

**Es la única vía con riesgo cero de perder precisión**, porque es literalmente el
código que hoy lee, con sus 1 203 pruebas. Eso hay que reconocerlo antes de
cualquier objeción.

### 5.1 Tamaño — medido

```
$ du/os.walk sobre dist/Fichas/_internal
dist/Fichas/_internal TOTAL      :    206.4 MiB
subconjunto de LECTURA           :    189.0 MiB
  de eso, cv2                    :     81.9 MiB
  de eso, onnxruntime            :     35.2 MiB
  de eso, modelos .onnx          :     13.1 MiB
Tcl/Tk (se cae en la via D)      :      5.3 MiB
```

**189,0 MiB.** El subconjunto es `cv2`, `onnxruntime`, `numpy` + `numpy.libs`,
`modelos`, `pypdfium2`, `rapidocr`, `pypdf`, `shapely`, `PIL`, `yaml` y el
intérprete. Quitar la interfaz ahorra **5,3 MiB de 206,4**: la interfaz nunca fue
lo que pesaba.

⚠️ **Es una suma de componentes ya empaquetados, NO un `PyInstaller` ejecutado.**
No he empaquetado nada — es trabajo del programador. La cifra real saldrá algo
distinta (PyInstaller poda dependencias no alcanzadas); **189,0 MiB es el techo
medido, no la promesa.**

Y **35,2 MiB de esos 189,0 son `onnxruntime` con el binding de Python**, frente a
los **17,27 MiB** que la vía B necesita del mismo motor: la vía D paga dos veces
por lo mismo.

### 5.2 Las reglas permanentes

- **Regla 2, doble clic sin instalación: se cumple.** Si el ejecutable de lectura
  vive dentro de la carpeta del programa en C#, el dueño sigue abriendo con doble
  clic un solo icono. No hay Python en la máquina: va empaquetado.
- **Regla 2, sin puertos abiertos: se cumple** si la comunicación es por
  argumentos y salida estándar (ruta del PDF → JSON), como plantea el encargo.
  **No** si alguien lo convierte en un servicio local.
- **Regla 1: se cumple**, es el mismo OCR determinista.

### 5.3 El coste que no es de tamaño

Dos empaquetados, dos lenguajes, dos cadenas de dependencias que actualizar, y un
borde nuevo que hoy no existe: **el proceso hijo puede fallar, colgarse o morir**,
y cada uno de esos tres estados necesita su mensaje y su prueba. La regla 1 pesa
aquí: un proceso que muere a medias **no puede parecer un formulario vacío**.

⚠️ **NO he medido el coste de arranque del proceso hijo.** El motor de OCR tarda
**2,2 s** en levantar (`DECISIONES.md`); si el proceso se lanza una vez por PDF,
esos 2,2 s se pagan por PDF, y hoy se pagan una vez por sesión. Sobre veinte
formularios eso es **44 s** que hoy no se pagan. **La cifra 2,2 s es medida; la
multiplicación es mía y depende de un diseño que no está decidido** — un proceso
persistente lo evitaría, a cambio de más complejidad.

---

## 6. Vía E — lo que encontré investigando

| Motor | Licencia | Estado | Por qué no lo recomiendo |
|---|---|---|---|
| **TesseractOCR 5.5.2** (fork de `charlesw/tesseract` mantenido por Sicos1977) | **Apache-2.0** | Vivo. El original `charlesw/tesseract` (Apache-2.0) **redujo actividad tras 2023** | Es cambiar de familia de modelo entero, tirando la regla de no regresión de los latinos PP-OCRv5. **No encontré ninguna medición de Tesseract 5 contra PP-OCRv5 en formularios escaneados**, ni a favor ni en contra. Adoptarlo sería apostar sin cifra |
| **PaddleOCR.Onnx 1.2.0** | **NO comprobada** | Publicado en NuGet | Mismo enfoque que RapidOcrNet con menos tracción. **No lo investigué a fondo**: RapidOcrNet cubre lo mismo, trae los modelos v5 latinos y tiene la actividad medida de §3.1 |
| **RapidOCR.Net 0.2** | — | **Fork no oficial** de RapidOcrNet, publicado por un tercero | Es el mismo código sin el mantenedor. No hay motivo para preferirlo al original |
| **OpenCvSharp4.runtime.win 4.13.0.20260627** | **Apache-2.0**, 38,02 MB | Vivo | No es un motor de OCR. Lo anoto porque es la opción si la vía A' se quisiera hacer con OpenCV en C# en vez de con SkiaSharp |

⚠️ **Lo que no pude hacer en la vía E:** no encontré **ninguna** comparación
publicada de precisión entre estos motores **sobre formularios**. Las cifras que
circulan son de texto de escena o de documentos limpios. **No encontré evidencia
consultable** para ordenarlos por precisión en este caso de uso, y no la invento.

---

## 6bis. Las casillas de ordenanza: 0 de 7 — y la causa **no** es la que parece

Va aparte de las vías A–E **a propósito**: no depende del motor de OCR. Cualquiera
de las cinco da 0 de 7 hoy.

### 6bis.1 La corrección: no fallan por falta de anotaciones

El pase del supervisor dice: *«Hoy se leen de las anotaciones del PDF y un escaneo
no tiene anotaciones.»* **Medido en el código, y es otra cosa.**
`extraccion/casillas.py:1-8` y `:47-55`, literal:

> Las casillas son marcas dibujadas a mano, no texto: el OCR no las lee de forma
> fiable. El metodo previsto es recortar cada celda, binarizarla, contar el pixel
> oscuro sobre el area total y comparar contra un umbral.

```python
FORMULARIOS_MINIMOS_PARA_CALIBRAR = 3
FORMULARIOS_CON_VERDAD_CONOCIDA = 0
LECTURA_DE_CASILLAS_ACTIVA = FORMULARIOS_CON_VERDAD_CONOCIDA >= FORMULARIOS_MINIMOS_PARA_CALIBRAR
UMBRAL_DE_PIXEL_OSCURO = None
```

**Tres cosas que cambian el coste de esta fase:**

1. **El método previsto ya es por imagen**, no por anotaciones. La idea del pase
   —«hay que leerlas de la imagen»— ya estaba escrita en el proyecto.
2. **La lectura está apagada a propósito**, en cualquier documento, tenga
   anotaciones o no. `LECTURA_DE_CASILLAS_ACTIVA` es **False** porque hay **0**
   formularios con verdad conocida y hacen falta **3**. Con los BARC (que sí
   tenían anotaciones) también habría dado 0.
3. **`extraccion/casillas.py` no lo importa ningún módulo de producción**
   (comprobado con `grep` sobre todo el árbol menos `pruebas/`, `.venv/` y
   `.claude/`): lo único que se usa es `casillas_no_leidas()`, que devuelve `None`
   en las seis.

**Por tanto el 0 de 7 no es un hallazgo nuevo del escaneo: es el estado declarado
del proyecto.** Lo nuevo y valioso del pase es otra cosa: **ahora hay siete
documentos reales con los que calibrar**, y antes había cero.

### 6bis.2 Lo que de verdad falta, y quién puede darlo

No es código. Es **la verdad conocida**: alguien tiene que marcar a mano, casilla
por casilla, qué ordenanzas trae cada uno de los siete papeles.
`extraccion/casillas.py:18-22` ya explica por qué no vale atajarlo:

> El OCR detecta algunas marcas como caracteres sueltos… pero eso no es una
> verdad conocida: es otra lectura automatica, y calibrar un detector contra otro
> detector no mide nada.

🔴 **Eso lo da el dueño, y son 42 casillas (7 × 6).** Sin ese dato la fase no
puede empezar, y con él es barata: recortar, binarizar, contar y comparar.

⚠️ **Y una advertencia que hay que decir antes de que alguien la descubra tarde:**
el umbral se calibra **sobre la imagen que produce el rasterizador**. Si el
programa en C# rasteriza con otro motor (§9.1), **el umbral hay que recalibrarlo**.
Es la misma trampa que la vía A'.

### 6bis.3 Y la regla que no se puede romper aquí

`extraccion/casillas.py:57-65`, literal:

> `None` no es lo mismo que 0 en este esquema… 0 significa «se leyo y no estaba
> marcada»; `None` significa «no se leyo». Si se devolvieran ceros, Miguel veria
> seis ordenanzas negativas en firme y no tendria motivo para mirar el papel.

**Seis ceros en silencio son exactamente el daño del criterio 1 de §0.2.** Si el
detector no está calibrado, devuelve `None` y se pide a mano. **Esto no es
negociable por comodidad.**

---

## 6ter. `CASD2609` por `CASP2609` — una letra que parte un caso en dos

Uno de los siete leyó una `D` donde el papel dice `P`. Consecuencias medidas
(`DECISIONES.md`, `29cd00c`): **entra como caso aparte**, y hoy **no hay forma de
corregir el número y unir los dos**.

**No es un problema de motor** —ninguna vía garantiza no confundir `P` con `D`— y
por eso no cambia la recomendación. Es el requisito 9 otra vez: el programa tiene
que **dejar corregir el número de caso y fusionar**. Ya está reconocido en
`DECISIONES.md` (2026-09-04, «El `CHECK` del número de caso se quita en el
programa nuevo»): el número se guarda tal como se lee y, si no tiene la forma
esperada, **se señala en el campo, no se rechaza**. Lo que falta y no está en
ninguna fase es **unir dos casos que resultaron ser el mismo**.

⚠️ **Y ojo con creer que el `CHECK` lo habría cazado: no.** `CASD2609` tiene
cuatro letras y cuatro dígitos, así que **pasa el `GLOB`** — está comprobado en
`DECISIONES.md` con ese mismo ejemplo. Ninguna restricción de formato detecta este
error; solo un ojo humano o la fusión posterior.

---

## 7. Recomendación — una sola

> ### **Vía B con `RapidOcrNet`, y la vía A' como preproceso de todos.**

**Por qué, contra el orden de criterios de §0.2:**

1. **No perder nada en silencio.** Es el mismo modelo que hoy lee, con el mismo
   diccionario latino de 502 caracteres (§1.1). Es la vía que menos cambia lo que
   ya está verificado, y **lo que está verificado es la línea base de §0.1bis:
   7 de 7 documentos sobre escaneos reales**.
2. **Ahorro de tecleo.** Es el motor del que existe una medición sobre el material
   real. ⚠️ **De la vía A no la hay:** sus cifras (0 de 5) son ⚠️doc-no-válido, y
   sobre escaneos sin capa de texto **podría ir aún peor, o distinto**. No la
   descarto por esos números —**los descarto yo mismo en §0.1**—, sino porque
   adoptarla exigiría volver a medirla y porque no cubre el francés (ADR-0003 §3.1).
3. **Sin red.** Los v5 se copian junto al binario (§3.3).
4. **Doble clic.** Un solo ejecutable, un solo lenguaje, un solo empaquetado.
5. **Tamaño y tiempo.** **30,4 MiB medidos** contra los **189,0 MiB** de la vía D
   y contra los 147,83 MB que el ADR-0003 temía.

**Y el argumento que más pesa no es ninguno de esos cinco: es que el coste que
hacía cara a la vía B ya no existe.** El ADR-0003 la descartó porque había que
reimplementar DB, CTC y cls, y porque faltaba el diccionario. **Las dos cosas eran
ciertas cuando se escribieron y las dos son falsas hoy**: la biblioteca existe,
es Apache-2.0, se actualizó hace cinco días y trae exactamente nuestros modelos.

**La vía A' va aparte y va igual.** Es barata, arregla la causa medida del fallo
y beneficia a cualquier motor. **No la recomiendo como alternativa a B, sino
dentro de B**, con su control positivo (§2).

### 7.1 Lo que hay que decidir y no decido yo

🔴 **La vía D sigue viva y es una decisión del dueño, no mía.** Su argumento —cero
riesgo de perder precisión, es el código que ya funciona con sus pruebas— es real,
y las 1 203 pruebas que lo respaldan tienen más valor que mi expectativa de §3.5.
Lo que pongo enfrente son **189,0 MiB contra 30,4 MiB**, dos empaquetados, y los
2,2 s de arranque del motor por proceso. **Si el dueño prefiere no tocar lo que
lee, la vía D es defendible** y este ADR no la descalifica.

---

## 8. El trabajo que implica, en fases

### 8.1 El criterio de aceptación son los **siete documentos `CASP2609`**

Ya no es un hueco: los ejemplos llegaron. **La prueba de la fase de lectura son
esos siete escaneos**, no los dos anteriores, por el motivo de §0.1 — los viejos
tenían capa de texto y los reales no.

| # | Qué | Umbral |
|---|---|---|
| C3-L1 | Documentos leídos | **7 de 7**, 0 ilegibles, 0 rechazados |
| C3-L2 | Personas con nombre | **7 de 7** |
| C3-L3 | Fecha de viaje | **7 de 7** |
| C3-L4 | Unidad, número y nombre | **7 de 7** |
| C3-L5 | **Cédulas ENSEÑADAS** | **7 de 7** — guardadas **o señaladas en el campo**, y **jamás vacías en silencio**. Es el criterio 1 de §0.2 y hoy se cumple 5 de 7 |
| C3-L6 | Número de caso | **7 de 7 leídos**, y el que salga mal **se puede corregir a mano y unir** (§6ter) |
| C3-L7 | Segundos por hoja | Se anota contra **9,3–18,5 s** del Python de hoy. **No pongo umbral**: el dueño no se ha quejado del tiempo de lectura |
| C3-L8 | Casillas de ordenanza | ⛔ **no entra aquí.** Va en su fase (§8.4) y hoy es 0 de 7 por diseño (§6bis) |

⚠️ **C3-L5 es el único que exige más que hoy**, y no se arregla con OCR: se arregla
enseñando `valor_ocr` cuando la validación rechaza. **Es trabajo de la fase de
extracción, no del motor.**

### 8.2 FASE C3a — la sonda del motor *(nueva, antes de portar nada)*

Un proyecto tirable, como fue la C0. **Ninguna línea de `extraccion/` se porta
hasta que esta sonda dé sus números.**

| # | Qué se mide | Criterio |
|---|---|---|
| C3a-1 | `ppocrv5_latin_dict.txt` de RapidOcrNet contra los **502 caracteres** del metadato `character` de `modelos/latin_PP-OCRv5_rec_mobile.onnx` | Iguales, o se dice en qué difieren. Decide si se usan sus modelos o los nuestros por `InitModels(...)` |
| C3a-2 | Los tres `.onnx` de RapidOcrNet contra los de `modelos/`, por SHA-256 | Iguales o no, por escrito. **Hoy NO lo sé** (§3.2) |
| C3a-3 | RapidOcrNet sobre **los siete escaneos `CASP2609`** | Los ocho criterios de §8.1, y **comparado contra el Python sobre los mismos siete archivos, medido el mismo día**. La línea base a batir es 7/7 leídos, 7/7 nombres, fechas y unidades, 5/7 cédulas guardadas |
| C3a-4 | **Sin red:** el proceso no abre ninguna conexión durante una lectura completa | Cero conexiones salientes. Regla permanente 2 |
| C3a-5 | Tamaño de `dotnet publish` con RapidOcrNet, y cuánto sube sobre la C0 | Se anota. Referencia medida: **30,4 MiB** de lo identificado (§3.6) |
| C3a-6 | Segundos por hoja, frío y caliente | Se anota contra **10,4 s / 8,6 s + 2,2 s de motor** del Python |
| C3a-7 | **Vía A':** la misma lectura con y sin limpieza de líneas, sobre los mismos documentos | **Control positivo obligatorio:** hay que enseñar que la limpieza **no borra** texto bueno. Si empeora un solo campo, no entra |

**Si C3a-3 sale peor que Python, la salida es la vía D**, y la decisión vuelve al
dueño con las dos cifras delante.

### 8.3 FASE C3b — las casillas de ordenanza *(nueva, y no depende del motor)*

Va aparte porque **cuesta lo mismo en las cinco vías** (§6bis). Y no puede
empezar hasta que exista la verdad conocida.

| # | Qué | Criterio |
|---|---|---|
| C3b-0 | 🔴 **La verdad conocida de las 42 casillas** (7 documentos × 6) anotada por una persona | **Bloqueante.** Sin esto la fase no empieza. `extraccion/casillas.py` exige **3** formularios como mínimo y hay **0**. *Lo da: el dueño* |
| C3b-1 | Localizar las seis casillas en la imagen | Por ancla, como las bandas — **nunca por coordenada fija** (§4.1) |
| C3b-2 | Medir el píxel oscuro de las marcadas y de las vacías, y **escribir el umbral con ese número** | El umbral va en el código **con cuántos formularios lo calibraron**. `UMBRAL_DE_PIXEL_OSCURO = None` hoy, y `None` está puesto a propósito |
| C3b-3 | Lectura sobre los siete documentos | **42 de 42 casillas**, contra la verdad de C3b-0, con los fallos nombrados uno a uno |
| C3b-4 | **Control negativo obligatorio** | Una casilla no leída devuelve **`None`, jamás `0`** (§6bis.3). Verificable con un `grep` y con una prueba |
| C3b-5 | Si no se alcanza C3b-3 | **La lectura NO se entrega activa**, como hoy: las seis vuelven a captura manual. Es la rama que el proyecto ya eligió una vez |

**Coste:** pequeño en código —recortar, binarizar, contar, comparar— y **el
grueso es C3b-0, que no es trabajo de programación.** ⚠️ **NO he estimado horas**
y no las estimo.

### 8.4 Lo que cambia en las fases ya escritas

- **FASE C0, criterio C0-8** — ya no es umbral de aprobación: la C0 se cerró y el
  resultado (0 de 5) **es** el hallazgo. Queda anotado como medido, y
  ⚠️doc-no-válido.
- **FASE C3, criterios C3-1 y C3-2** — **se sustituyen** por los ocho de §8.1:
  sus cifras salían de documentos con capa de texto que no representan el caso
  real (§0.1).
- **FASE C3** — la preceden la **C3a** (motor) y la **C3b** (casillas).
- **El requisito 9 entra en la C3**, no en la C4: la cédula terminada en `A`
  (§0.3) es un campo que el papel traía y el programa **vació en silencio**. Eso
  no es la pantalla: es la extracción. Es el criterio **C3-L5**.
- **Corregir el número de caso y unir dos casos** (§6ter) no tiene fase. Hay que
  darle una, y **no la asigno yo**: toca pantalla y base, no extracción.

---

## 9. Qué NO cubre este ADR, y qué no pude verificar

### 9.1 Fuera de alcance a propósito

- **Qué hace el programa con lo que lee.** El caso de la `A` es el requisito 9 y no
  lo resuelve un motor. Merece su propia decisión y no la tomo aquí.
- **El rasterizado.** Sigue siendo lo que dice el ADR-0003 §4.1. Pero anoto un
  hueco que ese ADR no vio: **`Windows.Data.Pdf` y `pypdfium2` no producen la misma
  imagen**, y la vía A' calibra un núcleo morfológico **sobre esa imagen**. Si el
  rasterizador cambia, **la calibración de A' hay que rehacerla** — el mismo
  problema que `PENDIENTES.md` ya reconoce para las seis casillas de ordenanzas.
- **Las anotaciones (`/Ink`, `/FreeText`).** Es la sonda C0-9 y el riesgo C3-12.
  Ninguna vía de este ADR lo toca: el tachón y el resaltador **no se leen con
  OCR**.
- **Portar `extraccion/`.** Es la FASE C3 y cuesta lo mismo en B, C, A' y E.

### 9.2 Supuestos del encargo que cuestioné, y con qué resultado

| Supuesto | Resultado |
|---|---|
| «Mira si los modelos incluyen el diccionario latino, que sin él no arranca» (ADR-0003 §3.2) | **Falso el impedimento.** Está embebido, 502 caracteres (§1.1) |
| «`Microsoft.ML.OnnxRuntime` 147,83 MB» (ADR-0003 §2.5) | **Es el paquete multiplataforma.** win-x64 son **17,27 MiB** medidos (§1.2) |
| «Portar DB, CTC y cls» (ADR-0003 §3.2) | **No hay que portarlos.** RapidOcrNet los trae, Apache-2.0 (§3.1) |
| «El sistema ya guarda las bandas, así que la banda del MRN se conoce» | **Solo para un caso ya importado.** Para un PDF nuevo la banda se localiza por ancla de texto (§4.1). **Esto tumba la vía C** |
| «Criterio (a): igualar 4 de 5» | **Lo revocó el dueño** (§0.2), y el «4 de 5» no era de OCR (§0.3) |
| «La lista de vías: B, C, D, E» | **Faltaba A'** — la trajo el supervisor a mitad de la redacción, y es la más barata de todas |
| «Los dos PDF del Escritorio son el material de prueba» | **Falso, y por un motivo peor que su antigüedad: tienen capa de texto** y los reales no (§0.1) |
| «Las casillas dan 0 de 7 porque un escaneo no tiene anotaciones» | **Falso.** La lectura está apagada a propósito por falta de calibración, y el método previsto **ya era por imagen** (§6bis.1) |

### 9.3 Preguntas que me hice y nadie me pidió

- **¿Es el `.exe` de Python grande por la interfaz?** No: Tcl/Tk son **5,3 MiB de
  206,4**. Es `cv2` con **81,9 MiB**. Eso importa porque **la vía B no lleva
  OpenCV** —RapidOcrNet usa SkiaSharp— y ahí está el ahorro real, no en el idioma.
- **¿Y si la vía A' se aplica al Python de hoy?** Cuesta cero MB, ataca la causa
  medida y **el `.exe` de Python es lo que el dueño usa hoy**. Puede mejorar la
  lectura actual sin esperar al C#. **No lo propongo como fase** —el `.exe` está
  congelado— pero la pregunta es del dueño, no mía.
- **¿Alguien mide precisión de estos motores en formularios?** **No encontré
  evidencia consultable** (§6).

### 9.4 Lo que NO verifiqué — la lista completa

1. **Que los `.onnx` de RapidOcrNet sean los nuestros byte a byte.** Criterio C3a-2.
2. **Que RapidOcrNet no abra red en ejecución.** La documentación dice que los v5
   van junto al binario; **no lo comprobé corriendo**. Criterio C3a-4.
3. **La precisión de RapidOcrNet.** No hay cifra publicada (§3.5) y no la estimo.
4. **El `onnxruntime.dll` del `.nupkg`.** Medí el de la rueda de Python, misma
   versión 1.29.0 (§1.2).
5. **El tamaño de SkiaSharp y Clipper2 en win-x64.** Solo tengo el multiplataforma.
6. **El coste en segundos de la vía A'.** No lo cronometré.
7. **Que la vía A' no dañe texto bueno.** Es el riesgo de §2 y el criterio C3a-7.
8. **La causa exacta de que Windows no reproduzca las etiquetas ancla** (§4.2).
9. **El coste de arranque del proceso hijo de la vía D** (§5.1).
10. **Nada en C#.** No compilé ni ejecuté: hay un programador trabajando en
    `csharp/Fichas/` y el pase lo prohíbe. **Todo lo de C# viene de documentación
    oficial y de NuGet, con URL y fecha; nada de medición propia.**
11. **No leí los siete `CASP2609`.** La línea base de §0.1bis es del supervisor,
    no mía. **No repetí ninguna de sus cifras**, y hay una que conviene que
    alguien repita: si la **raya de la tabla** (§0.3) también cruza el número en
    estos siete escaneos, o si era un rasgo de los BARC. **De eso depende cuánto
    aporta la vía A'**, y hoy no lo sé.
12. **Que RapidOcrNet lea un escaneo sin capa de texto igual que el Python.**
    Todo lo que se sabe de PP-OCRv5 en este proyecto se midió con el Python;
    §3.5 explica por qué la expectativa es razonable y por qué no es garantía.
