# Pendientes

Las diez fases del proyecto Fichas y la deuda abierta. Documento del
planificador. Nada se borra: lo que deja de ser cierto se tacha en el sitio con
su motivo y su fecha.

Escrito el 2026-09-02.
**Puesto al día el 2026-09-04**, sobre `c0a268c`, con los cinco ciclos ya
corridos. Lo que dejó de ser cierto está tachado **en su sitio** con su fecha y
su commit; la deuda que devolvieron los cuatro pases del ciclo 5 entra en
«Deuda del ciclo 5», al final. **Ver «Puesta al día del 2026-09-04» justo debajo
de este párrafo antes de leer las fases**: las diez están codificadas, y los dos
bloqueos que abren este documento llevan dos ciclos siendo falsos.

---

## Puesta al día del 2026-09-04 — las diez fases están codificadas y este documento decía que la 1 estaba bloqueada

Este documento se escribió el 2026-09-02, antes de que existiera una línea de
Python, y **describe el proyecto como si siguiera ahí**. No lo está. Lo que se
mide hoy, y con qué:

```
$ git log --oneline -1
c0a268c Ciclo 5, pase 4: el dibujado — calendario y listas en Canvas, ...

$ .venv/Scripts/python.exe <scratchpad>/medir_esquema.py
    # abrir_conexion(<temporal>) + aplicar_esquema + PRAGMA table_info por tabla
SQLite motor: 3.50.4
VERSION_ACTUAL: 14 | aplicar_esquema devolvio: 14
MIGRACIONES declaradas: [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14]
TABLAS REALES (9): asignaciones, casos, companeros, contactos,
                   documentos_ilegibles, filas_descartadas, personas,
                   procedencia_campo, version_esquema
TOTAL_COLUMNAS 104
INDICES PROPIOS (8)
```

⚠️ **Corregido el 2026-09-04 (segunda vuelta), sobre `02832f4`.** La salida de
arriba es la foto de `c0a268c` y ya no es la de hoy: entró la migración **15**
(`datos/migraciones_de_cedula.py`), que reconstruye `personas` para que el `CHECK`
de `mrn` admita una **letra** en el último carácter. Medido con una base temporal
en el scratchpad, `abrir_conexion` + `aplicar_esquema` + `sqlite_master`:

```
VERSION MAX version_esquema: 15
VERSIONES: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15]
TABLAS: 9 · TOTAL COLUMNAS: 104 · INDICES PROPIOS: 8 · CHECK: 48
```

**Nueve tablas, 104 columnas y ocho índices no se movieron**: la 15 no añade ni
quita columnas.

~~**El esquema existe, tiene 104 columnas y va por la versión 14.**~~
**El esquema existe, tiene 104 columnas, 48 `CHECK` y va por la versión 15.** Su sede es
`docs/ARQUITECTURA.md` §2, que es la opción B que este mismo documento
recomendó en el Bloqueo 1 y que el dueño aceptó el 2026-09-02.

**Lo que eso hace con lo escrito abajo, punto por punto:**

| Qué decía | Qué se mide hoy | Dónde queda tachado |
|---|---|---|
| «FASE 1 ⛔ BLOQUEADA — no hay esquema de datos» | 9 tablas, 104 columnas, ~~13~~ **14** migraciones aplicadas (2 a 15) | encabezado de la FASE 1 y Bloqueo 1 |
| «FASE 2 ⛔ BLOQUEADA para su verificación» | Hay **5 PDF reales** en `pdfs_referencia/` y la extracción corre sobre ellos | encabezado de la FASE 2 y Bloqueo 2 |
| **D-4** «no hay decisión sobre procesar dos veces el mismo PDF» | Decidida por el dueño el 2026-09-03 y construida: migración 13, `casos.duplicado_de` | D-4 |
| **D-8** «los cinco documentos de conocimiento no existen» | Existen **dos** de los cinco, más dos ADR | D-8 |

⚠️ **Lo que este documento sigue sin ser, y hay que decirlo antes de que alguien
lo use mal:** los criterios de aceptación de las diez fases **son los del plan
original y nadie los ha vuelto a cotejar contra lo que el código hace hoy**. El
ciclo 5 cambió reglas de producto que están escritas aquí como criterio —la
identidad del caso ya no es `numero_caso`, un duplicado ya no se rechaza, el
estado de la recomendación ya no lo firma solo Miguel— y esos criterios **no se
han reescrito**. Ver «Qué NO cubre la puesta al día del 2026-09-04», al final.

---

## Cómo se leen los criterios de aceptación de este documento

Un criterio de aceptación aquí **nombra un resultado observable**: un valor
concreto que sale, un archivo que se abre, un comando con su salida esperada, una
acción que se completa. «Funciona bien», «es rápido» y «está correcto» no son
criterios y no aparecen.

Los comandos que se citan son **de comprobación**, no de construcción. Quien
construye es el programador; quien dictamina si el criterio se cumple es QA.

### Regla del control positivo — un cero solo cuenta si el detector ve

*Añadida el 2026-09-02, hallazgo 11 de QA.* QA probó dos de los seis criterios de
este documento cuyo aprobado era «el comando devuelve 0 coincidencias» y **los dos
estaban rotos**: el aprobado se cumplía con el código máximamente vulnerable,
porque el detector no veía nada de lo que decía buscar.

**Regla, obligatoria en todo criterio de este documento cuyo aprobado sea un
cero:**

1. **Control positivo con denominador.** Antes de aceptar el cero sobre el código
   real, el mismo comando se corre sobre un **corpus de mutantes** —un archivo con
   las construcciones malas enumeradas en el propio criterio, una por una— y tiene
   que cazar **todas**. Se anota la fracción: `k de N`. **Si `k < N`, el detector
   no sirve, el criterio no vale y la fase no pasa**, por mucho que el código real
   dé cero.
2. **Denominador del universo.** El criterio dice además sobre cuántos elementos
   se buscó: cuántos `.py`, cuántas llamadas, cuántas líneas. Un cero sobre un
   conjunto vacío no es un aprobado, es un conjunto vacío.
3. **Lista blanca antes que lista negra donde se pueda.** Enumerar *todo* lo que
   hay y comprobar que cada elemento es aceptable es más fuerte que buscar las
   formas malas que a uno se le ocurrieron. Una lista negra solo prueba lo que su
   autor imaginó.

Los seis criterios afectados —FASE 1 crit. 3, FASE 2 crit. 12, FASE 6 crit. 4,
FASE 7 crit. 3, FASE 8 crit. 5, FASE 9 crit. 5— quedan reescritos abajo bajo esta
regla, con el texto anterior tachado en el sitio. Los mutantes son material de
prueba de QA, no código del proyecto, y no se commitean con el producto.

### Marcado `⚠️plan` — las cifras que vienen del plan y no están comprobadas

*Añadido el 2026-09-02, hallazgo 4 de QA.* Cada cifra de este documento que
proceda del plan del dueño y **no** esté medida aquí lleva pegado el marcado
`⚠️plan` en el punto donde se cita, no solo en un aviso al principio de la fase.

`⚠️plan` significa exactamente esto, y resuelve por sí solo qué manda cuando la
medición discrepa:

> Esta cifra es **hipótesis, no aprobado**. La fase se comprueba contra la
> **medición real**. Si la medición contradice la cifra, **manda la medición**
> (`CLAUDE.md` §8), se corrige `DECISIONES.md` con el número nuevo y la fase
> **no** suspende por la discrepancia. Lo que sí suspende la fase es **no medir**,
> o medir y no registrar el número.

Y su límite, que es donde está la trampa: `⚠️plan` cubre **cifras**, no **reglas de
comportamiento**. Que el tachón rojo anule el valor de debajo no es una cifra: es
la regla que la fase existe para implementar. Una regla de comportamiento no se
sustituye por lo que salga; se demuestra o la fase no pasa.

⚠️ **Procedencia de lo que hay aquí dentro.** El contenido de las fases viene del
plan del dueño, repartido entre `CLAUDE.md` y `DECISIONES.md`. Los hallazgos de
sesiones anteriores —las seis reglas de no regresión y todo el bloque de las
anotaciones del PDF— están marcados en `DECISIONES.md` como **«lo dice el plan
del dueño y NO se ha comprobado aquí»**, porque el código donde se descubrieron ya
no existe. Ese marcado se conserva en cada punto de este documento que los cite.
Cuando el código vuelva a existir, cada uno necesita su medición propia y su
número.

### Lo que sí está medido en este documento

Tres cosas, consultadas el **2026-09-02** para que los criterios de las fases 0, 2
y 9 se sostengan:

| Afirmación | Fuente | Valor literal leído |
|---|---|---|
| En modo `--onedir`, `sys._MEIPASS` apunta a la carpeta `_internal` del paquete | <https://pyinstaller.org/en/stable/runtime-information.html> (consultada 2026-09-02) | «When a bundled app starts up, the bootloader sets the `sys.frozen` attribute and stores the absolute path to the bundle folder in `sys._MEIPASS`» · «For a one-folder bundle, this is the path to the `_internal` folder within the bundle» |
| El modelo de reconocimiento latino de PP-OCRv5 se llama `latin_PP-OCRv5_mobile_rec` y cubre español | <http://www.paddleocr.ai/latest/en/version3.x/algorithm/PP-OCRv5/PP-OCRv5_multi_languages.html> (consultada 2026-09-02) | nombre literal `latin_PP-OCRv5_mobile_rec`; lista de idiomas: «French, German, Afrikaans, Italian, **Spanish**, Bosnian, Portuguese…»; tabla de idiomas: «Spanish \| Spanish \| es» |
| RapidOCR selecciona la generación de modelo por etapa con `Det.ocr_version` / `Rec.ocr_version` (`OCRVersion.PPOCRV5`), y ese reparto por etapas existe desde la 3.0.0 | <https://rapidai.github.io/RapidOCRDocs/main/install_usage/rapidocr/usage/> (consultada 2026-09-02) | `"Det.ocr_version": OCRVersion.PPOCRV5`, `"Rec.ocr_version": OCRVersion.PPOCRV5`; también `model_type`, `model_path`, `model_dir`, `engine_type` |

**Reverificadas las tres el 2026-09-02**, en la ronda de FIXES, volviendo a abrir
las tres URLs. Las tres **siguen en pie con su valor literal intacto**:
`sys._MEIPASS` → «For a one-folder bundle, this is the path to the `_internal`
folder within the bundle»; `latin_PP-OCRv5_mobile_rec` aparece con su fila de tabla
y la lista de idiomas incluye `Spanish | Spanish | es`; y `Det.ocr_version` /
`Rec.ocr_version` con `OCRVersion.PPOCRV5`, con la documentación diciendo que el
reparto por etapas es de `rapidocr>=3.0.0`. Un dato nuevo que aparece de paso y no
cambia nada aquí: `ocr_version` ya admite también `PPOCRV6`; este proyecto fija
PP-OCRv5 por la regla de no regresión de los modelos latinos, y cambiar de
generación sería una decisión del dueño con su propia medición.

~~Y una cuarta con **una sola vía de comprobación**, que por eso se anota como
frágil: la API JSON de PyPI (<https://pypi.org/pypi/rapidocr/json>, consultada
2026-09-02) devolvió `info.version` = **3.9.2**, última publicación
**2026-07-21**, **48** entradas en `releases`.~~

**Corregida el 2026-09-02, en la ronda de FIXES.** La cifra frágil se remidió por
dos caminos independientes y **una de las tres partes era falsa**. Las tres piezas,
literales:

| Dato | Camino 1 — API JSON de PyPI | Camino 2 — índice simple / `pip` | Veredicto |
|---|---|---|---|
| `info.version` | `3.9.2` | `pip index versions rapidocr` → `rapidocr (3.9.2)` · `LATEST: 3.9.2` | ✅ **coincide** |
| Última publicación | `upload_time` = `2026-07-21T10:59:01` | — | ✅ **en pie** |
| Entradas en `releases` | `len(d['releases'])` = **31** | `https://pypi.org/simple/rapidocr/` → **31** versiones distintas, de `2.0.0` a `3.9.2` | ❌ **el «48» era falso. Son 31.** |

- URL 1: <https://pypi.org/pypi/rapidocr/json> · consultada **2026-09-02** ·
  valor literal `info.version = 3.9.2`, `len(releases) = 31`,
  `upload_time = 2026-07-21T10:59:01`.
- URL 2: <https://pypi.org/simple/rapidocr/> · consultada **2026-09-02** · valor
  literal: 31 archivos, 31 versiones distintas, la primera `2.0.0` y la última
  `3.9.2`.
- `pip index versions rapidocr` lista **24** versiones, no 31, y no es una
  contradicción: `pip` filtra por las que tienen distribución compatible con esta
  máquina. Es un tercer número que mide otra cosa; se anota para que nadie lo lea
  como un desmentido.

⚠️ **Lo que este error enseña, y se queda escrito:** el «48» no salió de ninguna
parte comprobable — fue una cifra recordada, no leída. Una sola vía de consulta no
distingue haber mirado la fuente de haberla recordado mal. **Ninguna cifra externa
vuelve a entrar en este documento sin URL, fecha y valor literal, por dos caminos
cuando el número importa.**

Nada de esto cambia la consecuencia práctica, que sigue en pie: **no se fija
ninguna versión aquí**. La versión que se instale la congela el programador en
`requirements.txt` en la FASE 0, medida en la máquina.

**Lo que NO pude verificar:** que `openpyxl` exponga la constante `FORMAT_TEXT`
con valor `'@'`. La página de referencia de `openpyxl.styles.numbers` que consulté
no lista las constantes de formato. Por eso el criterio de la FASE 4 no cita la
constante: comprueba el **efecto observable** (`celda.number_format` devuelve `@` y
el MRN conserva sus ceros de delante), que es lo que de verdad importa.

---

# FASE 0 — Reconocimiento y prueba de empaquetado

**Bloqueada por:** nada. Se puede empezar hoy.

Esta fase existe para responder **una sola pregunta**: ¿RapidOCR con los modelos
PP-OCRv5 latinos sobrevive a PyInstaller `--onedir` y arranca en una máquina sin
Python? Descubrir que no cuando haya cinco mil líneas encima es mucho peor que
descubrirlo ahora.

### Qué se hace

- Un **esqueleto desechable**: carga RapidOCR con los modelos latinos, abre una
  ventana, saca texto de un PDF cualquiera con texto latino impreso y lo muestra.
  Existe para responder la pregunta, no para quedarse.
- Empaquetarlo con PyInstaller en `--onedir`.
- Ejecutar el resultado donde no haya Python disponible.
- Congelar las versiones instaladas.

### Qué NO se hace en esta fase

- ⛔ **No se inventaria código previo.** No hay código previo: medido el
  2026-09-02 y registrado en `ESTADO.md`.
- ⛔ No se diseña el esquema de datos, ni se crea la base, ni se escribe Excel.
- ⛔ No se mide precisión de OCR ni se calibra ningún umbral. Aquí solo importa si
  el binario arranca y si el modelo carga.
- ⛔ El esqueleto **no** se convierte en la base del programa. Es material de
  prueba y se marca como tal desde el primer commit.

### El nombre del paquete queda ligado aquí

*Añadido el 2026-09-02, hallazgo 13 de QA.* El documento usaba `<nombre>` sin
ligarlo nunca, así que ningún criterio que lo citara se podía comprobar literalmente.
Queda ligado: **el esqueleto de esta fase se llama `prueba_ocr`**, y su paquete es
`dist\prueba_ocr\prueba_ocr.exe`. El nombre dice que es material de prueba, que es
lo que esta fase entrega. El programa de verdad se llama `Fichas` y aparece a
partir de la FASE 9 (`dist\Fichas`); son dos paquetes distintos y no se confunden.

### Criterio de aceptación

1. `dir dist\prueba_ocr` lista la carpeta del paquete, y dentro existe
   `prueba_ocr.exe` y la carpeta `_internal`. El **tamaño total en MB** de
   `dist\prueba_ocr` queda anotado como número en `ESTADO.md`.
2. ~~En una sesión donde `where python` no devuelve ninguna ruta, el doble clic
   sobre `<nombre>.exe` abre la ventana. Queda anotado, **cronometrado en
   segundos**, el tiempo desde el doble clic hasta que la ventana responde.~~

   **Reescrito el 2026-09-02 — hallazgo 12 de QA.** Motivo: **manipular el `PATH`
   para que `where python` no devuelva nada no es una máquina sin Python.** El
   intérprete sigue instalado, sus DLL siguen en el disco, el runtime de Visual C++
   sigue registrado y la caché de modelos del usuario sigue donde estaba. Ese
   criterio no respondía la única pregunta que esta fase dice existir para
   responder. Queda en tres pasos, del más fuerte al más débil, y **se toma el más
   fuerte que sea posible hoy**:

   - **2a (fuerte).** Copiar `dist\prueba_ocr` entera a **otra máquina Windows que
     nunca haya tenido Python**, o a una máquina virtual limpia, y ejecutarla con
     doble clic: la ventana abre. Se anota **cronometrado en segundos** el tiempo
     desde el doble clic hasta que la ventana responde, y **qué máquina era**.
   - **2b (medio).** Si no hay segunda máquina: una **cuenta de usuario Windows
     recién creada** en esta misma máquina, que no tiene ni perfil, ni variables,
     ni cachés del usuario de desarrollo. Detecta la dependencia del perfil; **no**
     detecta la dependencia de algo instalado para toda la máquina.
   - **2c (débil, y se declara como débil).** Si no hay ninguna de las dos: se
     ejecuta en esta máquina **con el árbol de código fuente renombrado y la caché
     de modelos del usuario renombrada** (la carpeta donde RapidOCR descarga
     modelos, cuya ruta real se anota al medirla). Si arranca así, no depende de
     ninguna de esas dos. Se anota el tiempo igual.

     ⚠️ **Lo que 2c NO prueba, y se escribe en el cierre de la fase:** no prueba
     que arranque sin Python instalado, ni sin el runtime de Visual C++, ni sin
     otras DLL de sistema que el intérprete arrastró. **Cerrar por 2c deja la
     pregunta de la fase respondida solo en parte**, y esa mitad abierta se hereda
     como deuda declarada al criterio 2 de la **FASE 9**, que es donde la prueba en
     otra máquina es obligatoria y no admite sustituto.
3. El programa imprime al arrancar **la ruta desde la que carga cada modelo
   `.onnx`**. Esa ruta cae dentro de `_internal` y el archivo existe ahí
   (comprobable con `dir` sobre la ruta impresa). Base documental: PyInstaller
   documenta que en un paquete de una carpeta `sys._MEIPASS` apunta a `_internal`
   (URL y cita literal en la tabla de arriba, reverificada el 2026-09-02).

   **Añadido el 2026-09-02, hallazgo 12 de QA:** ninguna ruta impresa al arrancar
   cae **fuera** de `dist\prueba_ocr`. Si alguna apunta al árbol de código fuente,
   al perfil del usuario o a una caché, eso **es** la dependencia oculta de la
   máquina de desarrollo, y se anota como hallazgo aunque el programa arranque.
4. ~~El texto que el esqueleto extrae del PDF de prueba se compara **carácter a
   carácter** con lo que se lee en el PDF a simple vista, en al menos una línea con
   tildes o eñes.~~

   **Reescrito el 2026-09-02 — hallazgo 13 de QA:** «a simple vista» no es un
   método reproducible; dos personas leen la misma línea y anotan números distintos.
   Queda así: **antes** de ejecutar el OCR se **transcribe a mano** a un archivo de
   texto la línea de referencia del PDF —al menos una con tildes o eñes—, y esa
   transcripción es la que se guarda. Después se compara la salida del OCR contra
   ella y se anota **cuántos caracteres difieren** y **sobre cuántos caracteres
   tiene la línea** (`k de N`). El número puede ser mayor que cero: esta fase no
   juzga precisión, solo deja el número y su denominador.
5. ~~`requirements.txt` (o `congelados.txt`) contiene la salida literal de
   `pip freeze`~~ → **`requirements.txt`, y solo ese nombre.**

   **Corregido el 2026-09-02 — hallazgo 13 de QA:** `congelados.txt` **choca de
   nombre** con `.claude/congelados.txt`, que es la lista de congelación de
   auditorías de QA y no tiene nada que ver con versiones de bibliotecas. Dos
   archivos con el mismo nombre y significados opuestos es una confusión esperando
   a ocurrir. Se usa **`requirements.txt`**, que también está en la lista blanca de
   `no-crear-documentos.sh`.

   Contiene la salida literal de `pip freeze` de la máquina donde se construyó, con
   la versión exacta de `rapidocr`, `pypdf`, `pypdfium2`, `opencv`, `openpyxl` y
   `pyinstaller`.
6. **Sin `pandas`, y con control positivo** *(añadido el 2026-09-02, hallazgo 9 de
   QA: la regla permanente 3 no tenía criterio en ninguna fase)*. Sobre el
   `requirements.txt` recién congelado, `pandas` no aparece. Y el cero se acompaña
   de su control: el mismo comando de búsqueda corrido contra `openpyxl` —que sí
   tiene que estar— devuelve **1**. Un cero de un comando que no encuentra tampoco
   `openpyxl` es un comando roto, no un aprobado.
7. **Un «no» también cierra esta fase.** Si RapidOCR no sobrevive al empaquetado,
   la fase se cierra igual: con el mensaje de error **literal**, la vía intentada y
   la conclusión escrita en `DECISIONES.md`. La fase entrega una respuesta, no un
   éxito.

---

# FASE 1 — Capa de datos sin interfaz

> ## ~~⛔ BLOQUEADA — no hay esquema de datos~~ ✅ DESBLOQUEADA Y EJECUTADA
>
> ~~`CLAUDE.md` §7 dice que el esquema (nombres de tablas y columnas) **manda sobre
> cualquier plan**, y no existe: medido el 2026-09-02 con `find`, no hay ningún
> `esquema.md` en el disco (registrado en `ESTADO.md`). Empezar esta fase sin él
> significa que el programador inventa los nombres y después hay que renombrarlos
> en todas las fases que vengan.~~
>
> ~~**Vía de cierre y recomendación de dónde debe vivir: ver «Deuda abierta ·
> Bloqueo 1» al final de este documento.** Esta fase no arranca antes.~~
>
> **Tachado el 2026-09-04.** El bloqueo se cerró el 2026-09-02 —el esquema se
> escribió en `docs/ARQUITECTURA.md` §2, que es la opción B recomendada abajo— y
> la fase se ejecutó en el ciclo 3. Medido hoy sobre `c0a268c` construyendo una
> base con `aplicar_esquema`: **9 tablas, 104 columnas, ~~versión 14~~ versión 15
> (remedido el 2026-09-04 sobre `02832f4`), 8 índices propios, 48 `CHECK`**
> (salida completa en «Puesta al día del 2026-09-04»). Este encabezado
> llevaba **dos ciclos** diciendo lo contrario.

### Qué se hace

- Crear la base SQLite en `Documentos\Fichas`, **fuera** de la carpeta del
  programa.
- Las tablas del esquema, con nombres de tabla y columna en español.
- Las funciones de alta, lectura y actualización, y la validación de las cuatro
  reglas de formato de `DECISIONES.md`.

### Qué NO se hace en esta fase

- ⛔ Nada de OCR, nada de PDF, nada de ventanas, nada de Excel.
- ⛔ Nada se marca como verificado por el sistema (regla permanente 5). Si el
  esquema tiene una columna de verificado, su valor por defecto es «no».
- ⛔ No se inventan columnas que ninguna fase pidió.

### Criterio de aceptación

1. ~~Al ejecutar por primera vez, `dir "%USERPROFILE%\Documents\Fichas"` lista el
   archivo de base de datos. Antes de ejecutar, esa carpeta no lo contenía.~~

   **Reescrito el 2026-09-02 — hallazgo 3 de QA. Es el criterio que más falta hacía
   y el que peor estaba.** Motivo: `DECISIONES.md` (2026-09-02, «La carpeta de datos
   se resuelve por API, nunca por nombre») decide que la carpeta de datos se obtiene
   con la **API de carpetas conocidas de Windows** (`FOLDERID_Documents` /
   `SHGetKnownFolderPath`), **nunca componiendo una ruta con la cadena «Documentos»
   ni «Documents»**, porque en esta máquina existen las tres carpetas
   (`Documents` real, `OneDrive\Documentos` y `OneDrive\Documents`) y componer por
   nombre puede aterrizar la base con MRN reales dentro de OneDrive. Este criterio
   **codificaba literalmente el camino prohibido**. Es la primera fase que escribe
   en disco, así que la decisión se comprueba aquí. Cronología justa: la decisión se
   escribió dos minutos después de la primera entrega de este documento; no era un
   descuido, pero hoy era una contradicción.

   - **1a. El programa muestra la ruta que resolvió, antes de escribir nada.** Se
     arranca sobre una máquina sin base creada: **lo primero que aparece** es la
     ruta de la carpeta de datos, y sólo después existe el archivo. Se comprueba
     por el orden observable: si se cierra el programa en cuanto muestra la ruta,
     la carpeta **sigue sin contener** el archivo de base de datos.
   - **1b. Esa ruta es la que devuelve la API, comprobado por un segundo camino.**
     La ruta impresa se compara **carácter a carácter** con la que devuelve
     `[Environment]::GetFolderPath('MyDocuments')` en PowerShell, obtenida aparte.
     Tienen que ser idénticas. Si difieren, el programa no está usando la API:
     la fase no pasa.
   - **1c. Nada compone la ruta por nombre.** Sobre los `.py` de la fase, ninguna
     línea de código ejecutable construye la ruta de datos a partir de las cadenas
     literales `"Documentos"` o `"Documents"`. **Aprobado con cero → exige control
     positivo** (ver «Regla del control positivo»): el mismo comando corrido sobre
     un mutante que contenga `os.path.join(os.environ["USERPROFILE"], "Documents", "Fichas")`
     y su variante con `"Documentos"` tiene que cazar **2 de 2**. Y se anota el
     denominador: sobre cuántos `.py` se buscó.
   - **1d. Sólo entonces, el dato.** En la ruta mostrada en 1a existe el archivo de
     base de datos después de ejecutar, y antes no existía.
   - **1e. Aviso, no aprobado automático.** Si la ruta resuelta cae dentro de una
     carpeta de OneDrive, **eso no suspende la fase** —puede ser la ruta legítima si
     el usuario tiene la copia de seguridad de carpetas conocidas activada— pero el
     programa lo dice en pantalla, con la ruta delante, y queda anotado para que lo
     decida el dueño. Lo que suspende la fase es resolverla por nombre, o no
     mostrarla.
2. `sqlite3 <ruta a la base> ".tables"` devuelve **exactamente** la lista de
   tablas del esquema aprobado, ni una de más ni una de menos. `.schema <tabla>`
   devuelve columnas cuyos nombres están **todos** en español.
3. ~~**Seguridad — SQL parametrizado, y es cazable con un comando.** Ninguna
   consulta se construye concatenando texto: un `grep -n "execute(f\"\|execute('.*%\|execute(\".*+"`
   sobre los `.py` de la fase devuelve **0 coincidencias**, y toda llamada a
   `execute` pasa sus valores como parámetros (`?`).~~

   **Reescrito de raíz el 2026-09-02 — hallazgo 1 de QA, el más grave del
   documento.** Motivo: QA midió ese `grep` contra ocho construcciones inyectables
   reales y cazó **0 de 8** (denominador: `grep -c execute` → 8). Era ciego a la
   comilla simple, a `.format()`, a `%`, a `+`, a `executemany`, a `executescript`
   y a un `execute(` partido en dos líneas. **El aprobado se cumplía con el código
   máximamente vulnerable**: exactamente el criterio que protege la orden de que el
   texto del usuario jamás se concatene en una instrucción SQL.

   **Y no se arregla ensanchando la expresión.** Una expresión más ancha sigue
   siendo una lista negra: solo prueba las formas que a su autor se le ocurrieron, y
   su ceguera sigue sin medirse. Hay una que ninguna lista negra sobre `execute(`
   cazará jamás —`consulta = f"... {dato} ..."` en una línea y `execute(consulta)`
   diez líneas más abajo— y es la más natural de escribir. Se sustituye por **lista
   blanca con denominador, más control positivo**:

   - **3a. Denominador: se cuentan todas las llamadas.** `grep -rnc` sobre los
     `.py` de la fase por `execute`, `executemany` y `executescript` da un número
     **N**. Ese N se anota. **Si N = 0 la fase no pasa**: una capa de datos sin una
     sola llamada al motor no existe, y un cero sobre un conjunto vacío no es un
     aprobado.
   - **3b. Lista blanca: las N se revisan una a una, y las N quedan escritas.** Para
     cada una de las N llamadas se anota archivo y línea, y se comprueba que la
     instrucción SQL es una **cadena literal escrita en el sitio** —sin `f`, sin
     `.format()`, sin `%`, sin `+`, sin venir de una variable armada en otro
     lado— y que **todos** los valores viajan aparte, en la tupla de parámetros,
     con marcadores `?`. **N de N revisadas y N de N conformes, o la fase no pasa.**
     Un solo hallazgo aquí es un defecto que bloquea la fase.
   - **3c. Control positivo con denominador, obligatorio.** Sea cual sea el comando
     que se use para ayudarse en 3b, antes se corre contra un corpus de mutantes con
     estas **nueve** construcciones, una por una, y tiene que cazarlas **todas**
     (`9 de 9`). **Si caza menos de 9, el comando no sirve, el criterio no vale y la
     fase no pasa**, por mucho que el código real dé cero:

     | # | Construcción mala que el detector tiene que cazar |
     |---|---|
     | 1 | `execute(f"SELECT ... {dato}")` — f-string, comilla doble |
     | 2 | `execute(f'SELECT ... {dato}')` — f-string, **comilla simple** |
     | 3 | `execute("SELECT ... " + dato)` — concatenación con `+` |
     | 4 | `execute("SELECT ... %s" % dato)` — operador `%` |
     | 5 | `execute("SELECT ... {}".format(dato))` — `.format()` |
     | 6 | `executemany(f"INSERT ... {dato}", filas)` — **otra función** |
     | 7 | `executescript(f"... {dato}")` — **otra función** |
     | 8 | `execute(` en una línea y la cadena interpolada en la **siguiente** |
     | 9 | `consulta = f"... {dato}"` arriba y `execute(consulta)` más abajo — **la que ninguna lista negra sobre `execute(` caza; la cubre 3b, no el comando** |

     Los mutantes son material de prueba de QA. No son código del proyecto, no se
     commitean con el producto y no los escribe el planificador.
   - **3d. La prueba que no depende de ningún comando.** Dar de alta un caso cuyo
     campo de texto valga `'); DROP TABLE casos; --` y, después, `.tables` sigue
     devolviendo la lista completa de tablas y el valor guardado se relee **literal,
     con sus comillas y su punto y coma**. El motor recibió la lógica y los datos
     por separado: el intento llegó como dato inerte. Si la tabla desaparece, o si
     el valor vuelve recortado, la fase no pasa.

   *Este mismo defecto —aprobado con cero sin control positivo— estaba en los
   `DELETE FROM` de las fases 6, 7 y 8. Corregidos allí, cada uno en su sitio.*
4. ~~Dar de alta un caso con `mrn` de 10 dígitos se **rechaza** con un mensaje en
   español que nombra el campo y dice qué se esperaba. Con `055-1111-3853`
   (11 dígitos, patrón 3-4-4) se **acepta**.~~

   ⚠️ **Corregido el 2026-09-04: la regla de «11 dígitos» dejó de ser cierta.** La
   migración 15 (`datos/migraciones_de_cedula.py`) cambió el `CHECK` de
   `personas.mrn` porque el dueño lo mandó — `DECISIONES.md`, «La cédula PUEDE
   terminar en letra»: *«muchas cédulas de miembro tienen una A u otra letra al
   final»*. El `CHECK` vigente, transcrito del `sqlite_master` de una base
   construida hoy con `aplicar_esquema`:

   ```
   CHECK (mrn IS NULL
          OR mrn GLOB '[0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9A-Za-z]')
   ```

   **El criterio vigente es éste:** un `mrn` de 10 caracteres se **rechaza** con un
   mensaje en español que nombra el campo y dice qué se esperaba. `055-1111-3853`
   se **acepta**, y `055-1111-385A` —tres dígitos, guion, cuatro dígitos, guion,
   tres dígitos y una letra— **también se acepta**. Ese segundo valor es el control
   positivo: sin él, un `CHECK` que rechazara todo pasaría la mitad del criterio.

   ⚠️ **`DECISIONES.md` sigue diciendo «11 dígitos, patrón 3-4-4»** en su tabla
   «Reglas de formato de los campos» (línea 2283). Ese documento es del supervisor
   y **no lo toco**; queda dicho aquí para que quien lea aquella tabla sepa que el
   motor ya no dice eso.
5. Un `numero_caso` de 3 letras + 4 dígitos se rechaza; `CASP2609` se acepta.
   `unidad_numero` de 5 dígitos se rechaza; de 6 se acepta.
6. **La regla del mes cruzado.** Con `numero_caso` = `CASP2609`, una
   `fecha_viaje` de octubre de 2026 se rechaza y una de septiembre de 2026 se
   acepta (los últimos 4 dígitos leídos como AAMM = 2609).
7. Borrar por completo la carpeta del programa y volver a ejecutar: la base sigue
   existiendo y `SELECT COUNT(*)` sobre la tabla principal devuelve **el mismo
   número** que antes de borrar.
8. Ejecutar el arranque dos veces seguidas sobre una base ya creada: el conteo de
   filas antes y después es el mismo número, y no aparecen tablas duplicadas.

---

# FASE 2 — Extracción v2 con capa de anotaciones

> ## ~~⛔ BLOQUEADA para su verificación — falta el PDF de referencia~~ ✅ EJECUTADA, con un resto
>
> **Tachado el 2026-09-04.** La fase se escribió y corre sobre documentos reales.
> Medido hoy: `find . -iname "*.pdf"` devuelve **5 archivos en
> `pdfs_referencia/`** —`CASP2609_Jonas_Ficticio`, `CASP2609_Daniel_Jr…`,
> `PARB2609_Nora…` y dos `SURB2609_Suriname_Group`—, y `DECISIONES.md`
> (2026-09-02 y 03) recoge las 9 páginas medidas sobre ellos. **El Bloqueo 1
> también está cerrado** (esquema en la versión ~~14~~ **15**, remedido el
> 2026-09-04 sobre `02832f4`).
>
> ⚠️ **El resto que NO se cierra, y por eso esto es un tachado y no un borrado:**
> `CASP2609_Zutano_Family.pdf` **sigue sin estar en el disco** —los 5 de arriba
> son otros—, así que los criterios 1, 3, 4 y 5 de esta fase, que nombran ese
> archivo y sus coordenadas literales, **nunca se comprobaron contra él**. Se
> comprobaron contra otros documentos, que es distinto y es mejor que nada, pero
> no es lo que estos criterios dicen. Sigue en «Bloqueo 2».
>
> ~~El código se puede escribir; **no se puede dar por buena la fase** sin
> `CASP2609_Zutano_Family.pdf` ni sin formularios reales para calibrar el umbral
> de las casillas. Medido el 2026-09-02 con `find`: el PDF no está en el disco
> (`ESTADO.md`). Ver «Deuda abierta · Bloqueo 2».~~
>
> ~~**Y también por el Bloqueo 1**~~ *(añadido el 2026-09-02, hallazgo 8 de QA: el
> alcance del Bloqueo 1 excluía esta fase y no debía)*. Los criterios de aquí usan
> **seis nombres del esquema que no existe** — `numero_caso`, `mrn`, `fecha_viaje`,
> `origen`, `confianza` y `captura_manual`. El código se puede escribir contra
> nombres provisionales; si el esquema los cambia, hay que renombrarlos aquí y en
> todo lo que venga después.

⚠️ **Todo el contenido técnico de esta fase —las dos capas del PDF, las 22
anotaciones, los colores del tachón y del resaltador, la precedencia de valores,
la fórmula de conversión de coordenadas, el criterio de solapamiento del 50%, las
seis columnas de ordenanzas y el umbral del 60% de manuscritos— está en
`DECISIONES.md` marcado como «lo dice el plan del dueño y NO se ha comprobado
aquí».** Aquí son **hipótesis a verificar**, no hechos. Si un número real
contradice al plan, **manda el número real** y se corrige `DECISIONES.md`
(`CLAUDE.md` §8).

> ### Cómo se resuelve la contradicción que QA encontró en esta fase
>
> *Añadido el 2026-09-02, hallazgo 4 de QA.* El párrafo de arriba decía «manda el
> número real» y el criterio 3 decía «si sale el 7, la fase no pasa». Leídos
> juntos, se contradicen: uno dice que la medición manda y el otro suspende la fase
> por una medición. Dos personas los leen y esperan cosas distintas, que es
> justo lo que un criterio de aceptación no puede permitirse.
>
> **La contradicción era real y se resuelve separando dos cosas que el documento
> mezclaba:**
>
> | | Qué es | Qué manda | Marcado |
> |---|---|---|---|
> | **Cifra** | Un número medido sobre un PDF concreto: cuántas anotaciones tiene, en qué coordenadas está un rectángulo, qué grosor lleva un trazo | **Manda la medición.** Si difiere del plan, se anota el número real, se corrige `DECISIONES.md` y **la fase no suspende por eso** | `⚠️plan` |
> | **Regla de comportamiento** | Lo que el sistema tiene que **hacer**: que un tachón rojo anule el valor de debajo, que un `/FreeText` de la misma banda gane, que un resaltador verde no anule nada | **Manda la regla.** Es lo que la fase existe para construir; no se sustituye por lo que salga. **Si no se demuestra, la fase no pasa** | sin marcado |
>
> La fecha del criterio 3 se leía como cifra —«el 8»— y es una **regla**: lo que se
> exige no es que salga un 8, es que la precedencia funcione. Reescrito abajo con
> esa forma.
>
> Y el tercer caso, que el documento no contemplaba: **si la medición del PDF real
> contradice la descripción del plan** —pongamos que no hay tal tachón rojo—
> entonces lo que estaba mal era la descripción del PDF, no la regla. Se corrige
> `DECISIONES.md`, y la regla **sigue teniendo que demostrarse** sobre el par
> tachón + `/FreeText` que sí exista. Si no existe ninguno en ningún formulario
> real, la fase se cierra con esa parte **explícitamente sin verificar**, anotada
> como deuda, y no con un aprobado.

### Qué se hace

- Leer las anotaciones del PDF con `pypdf`: `/FreeText` (texto directo) e `/Ink`
  (trazos, clasificados por color y grosor).
- Rasterizar la página con `pypdfium2`, con **tope de 3500 px en el lado largo**.
- Filtrar con `cv2.medianBlur(gris, 3)` y **solo** con eso.
- OCR con RapidOCR cargando **explícitamente** los modelos PP-OCRv5 del grupo
  latino. Nombre del modelo de reconocimiento verificado en la documentación de
  PaddleOCR: `latin_PP-OCRv5_mobile_rec`, que cubre español (URL y cita literal en
  la tabla del encabezado).
- Aplicar la precedencia de valores y la conversión de coordenadas.
- Casillas de ordenanzas por conteo de píxel oscuro contra umbral calibrado.
- Descartar filas sin nombre y sin MRN; marcar `captura_manual` los formularios
  manuscritos.

### Qué NO se hace en esta fase

- ⛔ **Ni una llamada a un modelo de lenguaje**, ni para leer, ni para «arreglar»
  texto leído, ni para adivinar un dato que falta (regla permanente 1).
- ⛔ No se corrige a mano: eso es la FASE 3.
- ⛔ No se escribe Excel, no se avisa de viajes próximos.
- ⛔ Nada se marca como verificado.
- ⛔ No se elige el umbral de casillas «a ojo».

### Criterio de aceptación

*Marcado el 2026-09-02, hallazgo 4 de QA: cada cifra que viene del plan lleva ahora
`⚠️plan` pegado en el punto donde se cita, no solo en el aviso de arriba. El
significado de `⚠️plan` y lo que manda cuando la medición discrepa están definidos
en el encabezado del documento.*

1. Sobre `CASP2609_Zutano_Family.pdf`, el lector de anotaciones devuelve un
   conteo. **El plan dice 22** `⚠️plan`; se anota el número real que salga y, si
   difiere, se corrige `DECISIONES.md` con la medición **y la fase no suspende por
   la diferencia**. Lo que sí la suspende es no anotar el número.
2. `numero_caso` sale **`CASP2609`** `⚠️plan` *(el valor concreto sale del plan; lo
   que se exige es que salga **el que de verdad ponga el PDF**)*, con
   `origen='anotacion'` y confianza `1.0` — esto último **no** es cifra del plan,
   es la regla de precedencia: un valor tomado de una anotación se marca como tal.
3. ~~**El caso que prueba todo:** `fecha_viaje` sale **8 de septiembre de 2026**, no
   el 7. Es decir: el tachón rojo sobre `September 7, 2026` anula el valor del OCR
   y el `/FreeText` de la misma banda gana. Si sale el 7, la fase no pasa.~~

   **Reescrito el 2026-09-02 — hallazgo 4 de QA:** tal como estaba, exigía un
   número concreto (`el 8`) como aprobado duro, y ese número venía del plan sin
   comprobar, en contra de lo que el preámbulo de la fase promete. Lo que la fase
   tiene que demostrar **no es el 8: es la precedencia.** Queda en dos pasos:

   - **3a. Primero se mide el PDF y se escribe lo que hay.** Se vuelca la lista de
     anotaciones reales con su tipo, su texto, su color, su grosor y su rectángulo.
     Esa lista es el hecho; el plan era la hipótesis. Si no coincide con lo que dice
     `DECISIONES.md`, se corrige `DECISIONES.md`.
   - **3b. Sobre lo medido, la regla se demuestra o la fase no pasa.** Localizado en
     esa lista un par real de **tachón rojo solapando una banda** + **`/FreeText` en
     la misma banda**, el valor que el sistema entrega para ese campo es **el del
     `/FreeText`**, con `origen='anotacion'`, y **no** el que leyó el OCR debajo del
     tachón. Según el plan `⚠️plan` ese par es la fila «Date traveling to the
     temple» y el resultado sería el **8** de septiembre de 2026 en vez del 7; si el
     PDF real trae otro par, se usa el par real y se exige lo mismo.
   - **3c. Si el PDF real no contiene ningún par así**, la regla de precedencia
     queda **sin verificar**, se anota como deuda con esas palabras, y la fase
     cierra **sin** dar por buena esa parte. No se inventa un PDF para pasar.
4. **La conversión de coordenadas tiene prueba propia.** Se vuelca la imagen
   rasterizada con los rectángulos de las anotaciones dibujados encima: el
   rectángulo de la anotación `[35.9, 437.2, 106.2, 444.6]` `⚠️plan` *(coordenadas
   del plan; se usan las del volcado de 3a)* cae **sobre la fila "Date traveling to
   the temple"** y no sobre la fila espejada respecto al centro de la página.

   **Cómo se comprueba sin «a simple vista»** *(hallazgo 13 de QA)*: la imagen
   volcada se guarda como archivo y se anota, **en píxeles**, la coordenada `y` del
   centro del rectángulo dibujado y la coordenada `y` del centro de la fila
   correspondiente en la imagen. La distancia entre las dos se anota como número y
   tiene que ser menor que la altura de una fila. Una anotación espejada da una
   distancia del orden de media página, y eso se ve en el número, no en la
   impresión de quien mira. La imagen se guarda igualmente, para que se pueda
   volver a mirar.
5. Un tachón que solapa el **49%** de la altura de una banda **no** la anula; uno
   que solapa el **51%** sí. Se prueban los dos lados del borde. *(El umbral del
   **50%** es `⚠️plan`; lo que no es del plan es que **haya** un borde y que se
   pruebe por los dos lados. Si se decide otro umbral con una medición detrás, se
   cambia el número aquí y en `DECISIONES.md`, y el criterio sigue siendo probarlo
   a 1 punto por debajo y 1 por encima.)*
6. Un `/Ink` verde `⚠️plan` `(0.494, 0.765, 0)` de grosor **16.5** `⚠️plan` **no**
   anula nada y el valor de debajo sobrevive. Un `/Ink` de cualquier otro color
   queda **registrado y sin interpretar**: aparece en la salida como anotación
   desconocida. *(Los colores y grosores son del plan; que el resaltador no anule y
   que lo desconocido no se interprete son reglas y no se negocian con la
   medición.)*
7. Ningún lado de la imagen rasterizada supera **3500 px** `⚠️plan` *(regla de no
   regresión del plan, sin medición propia en este repositorio — ver D-2)*: se lee
   el tamaño de la imagen generada y se anota.
8. **Tiempo por página medido en segundos** sobre un escaneo real, anotado en
   `ESTADO.md`. Sin número, la fase no cierra: las dos reglas de no regresión de
   rendimiento (`medianBlur` y el tope de 3500 px) solo se pueden defender con un
   número propio.
9. Un formulario con dos filas de personas llenas y cuatro vacías produce
   **exactamente 2** personas, no 6.
10. Un formulario donde más del **60%** `⚠️plan` de los campos vuelven con confianza
    < **0.6** `⚠️plan` sale marcado `captura_manual`, con los campos vacíos, sin que
    el sistema rellene nada. *(Los dos números son del plan; que exista un umbral y
    que al cruzarlo el sistema **no rellene nada** es regla, y esa parte no la
    cambia ninguna medición: viene de la regla permanente 1.)*
11. ~~El umbral de las casillas queda en **una constante con un comentario que dice
    sobre cuántos formularios reales se calibró**. Si ese número es menor que 3, la
    fase se cierra dejando la deuda anotada en este documento, no se cierra en
    silencio.~~

    **Reescrito el 2026-09-02 — hallazgo 10 de QA.** Motivo: **el criterio permitía
    justo lo que la fase prohíbe.** El apartado «Qué NO se hace» dice «⛔ No se elige
    el umbral de casillas «a ojo»», y este criterio dejaba cerrar la fase con menos
    de 3 formularios con sólo anotar la deuda — es decir, con un umbral elegido a
    ojo corriendo en producción y leyendo casillas de personas reales. Anotar la
    deuda no impide el daño; solo lo documenta.

    Queda así, **y no hay tercera salida**:

    - **11a. Con 3 o más formularios reales con casillas marcadas:** el umbral queda
      en una constante con un comentario que dice **sobre cuántos formularios se
      calibró** y **qué valores de píxel oscuro dieron las casillas marcadas y las
      vacías** en esa muestra. Se anota además cuántas casillas de la muestra se
      clasifican bien con ese umbral (`k de N`). La lectura de casillas se entrega
      **activa**.
    - **11b. Con menos de 3:** la lectura automática de casillas **no se entrega
      activa**. Las seis columnas de ordenanzas vuelven **vacías** y marcadas para
      captura manual, igual que un formulario manuscrito. La fase **puede cerrar**
      —el resto de la extracción funciona sin las casillas— pero cierra con esa
      funcionalidad **explícitamente desactivada** y la deuda anotada, no con un
      número inventado leyendo casillas de verdad. Un campo vacío que Miguel rellena
      es un coste; una casilla mal leída que nadie revisa es el daño que el programa
      existe para evitar.
    - **11c. Se anota el número de formularios que hubo.** Sin ese número no se sabe
      cuál de las dos ramas se aplicó, y la fase no cierra.
12. ~~`grep -rn "requests\|urllib\|http://\|https://\|openai\|anthropic"` sobre los
    `.py` de la fase devuelve **0** coincidencias en código ejecutable.~~

    **Reescrito de raíz el 2026-09-02 — hallazgo 2 de QA, el segundo grave del
    documento.** Motivo: ese `grep` fallaba **en los dos sentidos a la vez**, y es
    el criterio que protege la **regla permanente 1**, la que impide que un MRN o
    una fecha de viaje salgan inventados por un modelo.

    - **Suspendía código limpio.** El patrón `https://` casa con cualquier **URL en
      un comentario** — y este mismo documento manda poner URLs de base documental
      en el código. Un criterio que suspende por cumplir otra regla del proyecto
      enseña a la gente a saltárselo.
    - **Aprobaba código culpable.** No caza Google (`google.generativeai`,
      `gemini`), ni `transformers`, ni `ollama`, ni `llama_cpp`, ni `cohere`, ni
      `mistralai`. Un archivo que le pasa un MRN a Gemini pasaba este criterio.

    **Y no se arregla ensanchando la expresión**: una lista negra más larga sigue
    sin cazar el proveedor que salga el mes que viene, y sigue sin distinguir un
    comentario de una llamada. Se sustituye por **lista blanca de dependencias +
    prueba de comportamiento**, que es lo que de verdad se quiere saber:

    - **12a. Denominador: todas las importaciones, enumeradas.** Se listan **todas**
      las líneas `import` y `from ... import` de los `.py` de la fase; salen **N**
      módulos distintos y **N se anota**. Un comentario no es una importación, así
      que la URL de base documental deja de dar falso positivo por construcción.
    - **12b. Lista blanca: cada uno de los N es de la lista, o es un defecto.** Cada
      módulo importado es **biblioteca estándar de Python** o **uno de los seis de
      `CLAUDE.md` §3** (`pypdf`, `pypdfium2`, `opencv`, `rapidocr`, `sqlite3`,
      `openpyxl`). **Cualquier otro nombre es un hallazgo que bloquea la fase**, se
      llame como se llame y exista o no hoy. Esto sí caza al proveedor que aún no
      se ha inventado.
    - **12c. Control positivo con denominador, obligatorio.** El procedimiento de
      12a-12b se corre antes sobre un corpus de mutantes con estas **ocho**
      importaciones, una por archivo, y tiene que señalarlas **todas** (`8 de 8`).
      **Si señala menos de 8, no vale y la fase no pasa:**

      | # | Importación que la comprobación tiene que señalar |
      |---|---|
      | 1 | `import openai` |
      | 2 | `import anthropic` |
      | 3 | `import google.generativeai as genai` — **Google, el que faltaba** |
      | 4 | `from transformers import pipeline` |
      | 5 | `import ollama` |
      | 6 | `import llama_cpp` |
      | 7 | `import cohere` |
      | 8 | `import mistralai` |

      Y un **control negativo**, que es lo que arregla la otra mitad del fallo: un
      archivo limpio que contenga un comentario con `https://pyinstaller.org/...`
      **no** se señala. Si se señala, la comprobación sigue rota en el sentido de
      los falsos positivos.
    - **12d. La prueba que no depende de leer código: no sale un solo paquete.**
      Con el programa procesando un PDF de principio a fin, **ninguna conexión de
      red sale de su proceso**. Se comprueba en PowerShell sobre el PID del
      programa: `Get-NetTCPConnection -OwningProcess <PID>` no devuelve ninguna
      línea, y `Get-NetUDPEndpoint -OwningProcess <PID>` tampoco.
      **Control positivo:** las mismas dos órdenes **sin** filtrar por PID
      devuelven varias líneas en esta máquina; se anota cuántas. Si sin filtro
      tampoco devuelven nada, la orden no ve y el cero no vale.

      Una importación se puede esconder; un socket abierto, no. Este es el
      criterio duro, y los tres anteriores existen para cazar el problema antes de
      llegar aquí.

---

# FASE 3 — Pantalla de corrección

**Bloqueada por:** FASE 1 (necesita la base) y FASE 2 (necesita algo que corregir).

### Qué se hace

- La ventana donde Miguel ve lo extraído, con **la imagen del formulario al
  lado**, edita los campos y marca a mano lo que da por bueno.

### Qué NO se hace en esta fase

- ⛔ No se cambia el motor de extracción. Si un campo sale mal, se anota como
  hallazgo de la FASE 2; no se parchea desde aquí.
- ⛔ No se genera Excel ni se avisa de viajes próximos.
- ⛔ **Nada se marca solo.** El sistema propone, Miguel confirma (regla permanente
  5).

### Criterio de aceptación

1. Al abrir un caso extraído, cada campo muestra **su valor y su origen**
   (anotación / OCR / vacío) de forma visible sin abrir otra pantalla.
2. ~~Un campo con confianza menor que 0.6 se distingue **a simple vista** de uno con
   confianza alta. Se comprueba abriendo un caso que tenga de los dos.~~

   **Reescrito el 2026-09-02 — hallazgo 13 de QA:** «a simple vista» no es
   reproducible —dos personas dictaminan distinto y ninguna se equivoca— y además
   dejaba pasar que la distinción fuera **sólo un color**, que no la ve quien
   distingue mal el rojo del verde. Queda así:

   - El campo de confianza baja lleva una marca **textual** además de la que sea
     visual: una palabra en español junto al campo (por ejemplo «revisar»). La
     distinción no depende de percibir un color.
   - **Se comprueba con dos listas y se cuentan:** la lista de campos que la pantalla
     marca, y la lista que devuelve un `SELECT` de los campos con confianza < 0.6
     `⚠️plan` de ese mismo caso. **Coinciden elemento a elemento**, y se anota
     `k de N` — cuántos marcados sobre cuántos que deberían estarlo. Se abre un caso
     que tenga campos de los dos tipos, para que N no sea cero ni sean todos.
3. Al abrir un caso recién extraído, el contador de campos verificados es **0**.
   Solo sube cuando Miguel pulsa. Se comprueba con `SELECT` sobre la base antes de
   tocar nada.
4. Editar un campo, guardar, **cerrar el programa por completo**, reabrirlo: el
   valor editado sigue ahí, y un `SELECT` directo sobre la base devuelve ese mismo
   valor.
5. Escribir un `mrn` de 10 dígitos y pulsar guardar: se rechaza con un mensaje en
   español que nombra el campo. El resto de lo editado en esa pantalla **no se
   pierde**.
6. Abrir dos casos seguidos: la imagen que se ve en el segundo es la del segundo
   caso, no la del primero.
7. ~~**Ni una cadena en inglés en la ventana** (regla permanente 4): se recorre la
   interfaz a ojo y se revisan los literales de texto de los `.py` de la fase.~~

   **Reescrito el 2026-09-02 — hallazgo 13 de QA:** «a ojo» tiene el mismo problema
   que «a simple vista», y además no dice cuántas cadenas había, así que un repaso
   que se saltara la mitad daba el mismo resultado que uno completo. Queda con
   denominador:

   - Se **enumeran todas** las cadenas de texto que la interfaz muestra —etiquetas,
     botones, mensajes de error, títulos— y sale un número **N**, que se anota.
   - De esas N, **N están en español**. Se revisan **una a una**; el aprobado se
     escribe `N de N`, no «ninguna en inglés». Si N sale 0, la enumeración está mal
     hecha y el criterio no vale.
   - Los mensajes de error entran en la cuenta. Son los que más se olvidan y los
     únicos que Miguel va a leer con prisa.

---

# FASE 4 — Espejo de Excel

**Bloqueada por:** FASE 1 y FASE 3.

Las decisiones de esta fase están adelantadas en `DECISIONES.md` («Decisiones de
las fases posteriores · Excel»).

### Qué se hace

- Regenerar el `.xlsx` **completo tras cada guardado**, escribiéndolo a un
  temporal y reemplazando al final.

### Qué NO se hace en esta fase

- ⛔ **No hay botón de exportar.** Si hay que acordarse de pulsarlo, algún día no
  se pulsa.
- ⛔ Sin `pandas` (regla permanente 3). Solo `openpyxl`.
- ⛔ Sin celdas combinadas y sin colores que signifiquen algo: un color no es un
  dato.
- ⛔ El Excel **no** es la fuente de verdad; es un espejo. Nada se lee de vuelta
  desde él en esta fase (eso es la FASE 6).

### Criterio de aceptación

1. Guardar un caso en la pantalla de corrección y mirar la **marca de tiempo** del
   `.xlsx` en `Documentos\Fichas`: ha cambiado. No se pulsó ningún botón de
   exportar, porque no existe ninguno en la interfaz.
2. Abrir el `.xlsx` en Excel: la celda del MRN muestra **`055-1111-3853`**, con
   el cero de delante. Releyendo esa celda con `openpyxl`,
   `celda.number_format` devuelve exactamente **`@`** y el valor es la cadena
   completa. Lo mismo para `unidad_numero` de 6 dígitos que empiece por cero.
   <sub>No pude verificar en la documentación de openpyxl la constante
   `FORMAT_TEXT`; por eso el criterio comprueba el efecto, no el nombre de la
   constante.</sub>
3. La celda de `fecha_viaje` releída con `openpyxl` devuelve un objeto `date` de
   Python, no una cadena. En Excel, ordenar esa columna deja las filas en orden
   cronológico real (se comprueba con fechas de distintos meses y años).
4. **Con el `.xlsx` abierto en Excel**, guardar un caso: aparece en pantalla un
   aviso en español que nombra el archivo bloqueado, el programa **no se cierra**, y
   el dato **sí queda guardado en la base** (`SELECT` lo confirma). Nunca falla
   callado.
5. Matar el proceso a mitad de la escritura del Excel: el `.xlsx` anterior sigue
   abriéndose en Excel sin mensaje de archivo dañado, y no queda un archivo a
   medias con el nombre definitivo.
6. Sobre el archivo generado: `0` celdas combinadas, y ninguna celda cuyo color
   sea el único portador de una información. **Se comprueba contando**
   *(hallazgo 13 de QA)*: releído con `openpyxl`, el número de celdas combinadas es
   `0` y el número de celdas con relleno distinto del de por defecto es `0`. Si el
   segundo número no es cero, cada una de esas celdas se justifica por escrito
   diciendo qué información lleva **además** del color.
7. **Sin `pandas`, y con control positivo** *(añadido el 2026-09-02, hallazgo 9 de
   QA)*. La regla permanente 3 —«sin pandas, solo `openpyxl`»— no tenía criterio en
   ninguna fase del documento: aparecía una sola vez, y como viñeta de «qué no se
   hace». Una regla sin criterio es una intención. Esta es la fase donde `pandas`
   entraría, porque es la que escribe Excel:

   - Se enumeran **todas** las importaciones de los `.py` de la fase —el mismo
     procedimiento del criterio 12 de la FASE 2, con su denominador **N**— y
     `pandas` no está entre ellas. Tampoco `numpy` como dependencia traída sólo
     para tabular.
   - **Control positivo con denominador:** la misma comprobación corrida sobre un
     mutante que contenga `import pandas as pd` y otro con
     `from pandas import DataFrame` los señala **2 de 2**. Si señala menos, no vale.
   - **Control negativo:** `openpyxl`, que sí tiene que estar, aparece en la
     enumeración. Un procedimiento que no encuentra ni `openpyxl` no está mirando
     nada.
   - El coste que esta regla protege —unos 40 MB de ejecutable, `CLAUDE.md` §1.3—
     es `⚠️plan`: **no está medido en este repositorio**. Se mide de verdad en la
     FASE 9, donde el tamaño del paquete se anota como número.

---

# FASE 5 — Calendario y pendientes

**Bloqueada por:** FASE 1.

> **Dependencia hacia adelante, declarada** *(añadida el 2026-09-02, hallazgo 7 de
> QA).* El criterio 6 de esta fase hablaba de casos archivados, y **archivar es la
> FASE 8**, que va después. Tal como estaba, esta fase no podía cerrar hasta que
> terminara una posterior, y eso no se decía en ninguna parte: quien la ejecutara
> se encontraría con un criterio imposible sin saber por qué. Resuelto abajo, en el
> propio criterio 6.

Decisión adelantada en `DECISIONES.md`: los viajes próximos van **en la pantalla
de inicio, arriba de todo, en rojo**. No en una pestaña.

### Qué se hace

- El aviso de los casos que viajan pronto con la recomendación incompleta. Es el
  daño que el programa entero existe para evitar.

### Qué NO se hace en esta fase

- ⛔ **No se pone en una pestaña ni detrás de un clic.** Si hay que dar un clic
  para enterarse, algún día no se da ese clic.
- ⛔ No se manda ningún correo, ni notificación, ni nada que salga de la máquina
  (regla permanente 2: sin servicios de red).
- ⛔ El sistema no decide que un caso está completo: lo que cuenta es lo que Miguel
  marcó.

### Criterio de aceptación

1. Con un caso cuya `fecha_viaje` es dentro de 3 días y la recomendación
   incompleta, abrir el programa: el caso aparece **en la primera pantalla, en el
   bloque de arriba, en rojo**, sin dar ningún clic.
2. Un caso a 30 días **no** aparece en ese bloque.
3. **El borde exacto se prueba por los dos lados:** un caso a 7 días entra; uno a
   8 días no. Un caso cuya fecha ya pasó aparece también, y distinguido de los
   futuros.
4. ~~Un caso a 3 días **con la recomendación completa** se distingue a simple vista
   de uno a 3 días incompleto.~~

   **Reescrito el 2026-09-02 — hallazgo 13 de QA**, igual que el criterio 2 de la
   FASE 3 y por el mismo motivo: la distinción lleva una marca **textual** en
   español además de la visual, y se comprueba **contando** — la lista de casos que
   la pantalla marca como incompletos coincide, elemento a elemento, con la que
   devuelve el `SELECT` equivalente. Se anota `k de N`. Se prueba con casos de los
   dos tipos a la vez, para que N no sea cero ni sean todos.
5. Con la base vacía, la pantalla de inicio abre sin ningún error y muestra una
   frase en español que dice que no hay viajes próximos.
6. ~~Un caso archivado (FASE 8) no aparece en este bloque aunque su fecha caiga
   dentro de los 7 días.~~

   **Reescrito el 2026-09-02 — hallazgo 7 de QA:** exigía comprobar en la FASE 5
   algo que sólo existe en la FASE 8. La obligación se parte en dos, cada mitad en
   la fase que sí la puede cumplir:

   - **Obligación de diseño, y esta fase sí la cumple:** la consulta que alimenta el
     bloque de viajes próximos **filtra por la marca de archivado desde el primer
     día**, aunque todavía no haya forma de archivar nada. Se comprueba leyendo la
     consulta: la marca de archivado aparece en su filtro. Añadirla después obliga a
     revisar todas las consultas de la fase, que es el retrabajo que esto evita.
   - **Verificación de comportamiento: se hace en la FASE 8, criterio 1**, que ya
     dice que un caso archivado desaparece del bloque de viajes próximos. Aquí no se
     puede verificar y **por eso ya no se pide aquí**. Queda declarado en la cabecera
     de la fase para que nadie se encuentre el hueco sin explicación.
   - Si el esquema todavía no tiene columna de archivado cuando se ejecute esta
     fase, eso **no** es motivo para omitir el filtro: es una entrada más para el
     Bloqueo 1, y se anota ahí.

---

# FASE 6 — Compañeros, asignaciones y paquete

**Bloqueada por:** FASE 1 y FASE 4.

Decisión adelantada en `DECISIONES.md`: reconciliación por **`numero_caso` +
`mrn`**, nunca por nombre; las filas sin par **no se insertan**; los compañeros
**se desactivan, no se borran**.

### Qué se hace

- Registrar compañeros, asignarles casos, generar el paquete que se les entrega, y
  reconciliar el Excel que devuelven.

### Qué NO se hace en esta fase

- ⛔ **Nunca se empareja por nombre.** Un acento de más crea un registro fantasma.
- ⛔ No se borra ningún compañero de la base.
- ⛔ Las filas del Excel devuelto que no casan **no se insertan a ver si suena la
  flauta**.

### Criterio de aceptación

1. Devolver un Excel con una fila cuyo nombre lleva un acento distinto pero el
   mismo `numero_caso` + `mrn`: la fila **actualiza** el registro existente. El
   conteo de filas de la tabla antes y después es **el mismo número**.
2. Devolver un Excel con una fila cuyo par `numero_caso` + `mrn` no existe en la
   base: **no se inserta**, y aparece en la lista de descartados con el motivo
   escrito. Conteo de la tabla igual antes y después; conteo de descartados = 1.
3. Desactivar un compañero: deja de aparecer en el desplegable de asignación, y
   **las verificaciones que hizo siguen mostrando su nombre** al abrir esos casos.
4. ~~`grep -rn "DELETE FROM"` sobre los `.py` de la fase devuelve **0** coincidencias
   sobre la tabla de compañeros.~~

   **Reescrito el 2026-09-02 — hallazgos 1 y 11 de QA.** Mismo defecto que el grep
   antiinyección de la FASE 1: **aprobado con cero, sin control positivo**, y con
   una lista negra que no ve `delete from` en minúscula, ni `DELETE` y `FROM` en
   líneas distintas, ni una instrucción armada en una variable. Queda así:

   - **4a. Control positivo con denominador, obligatorio.** El comando que se use se
     corre antes sobre un corpus de mutantes con estas **cinco** formas y tiene que
     cazarlas **todas** (`5 de 5`); si caza menos, no vale y la fase no pasa:
     `DELETE FROM companeros` · `delete from companeros` (minúscula) ·
     `DELETE\nFROM companeros` (partido en dos líneas) ·
     `DELETE   FROM companeros` (espacios de más) ·
     `sql = "DELETE FROM " + tabla` seguido de `execute(sql)` (armado en variable).
   - **4b. Lista blanca con denominador, que es lo que de verdad cierra la puerta.**
     Se enumeran **todas** las instrucciones SQL de escritura de la fase —salen **N**,
     y N se anota— y **ninguna de las N** borra filas de la tabla de compañeros.
     N de N revisadas.
   - **4c. La prueba de comportamiento, que no depende de leer código.** Desactivar
     un compañero y contar: `SELECT COUNT(*)` sobre la tabla de compañeros devuelve
     **el mismo número** antes y después. La fila sigue ahí; lo que cambió es su
     marca.
5. Generar el paquete de un compañero que tiene 3 casos asignados: el paquete abre
   y contiene **exactamente esos 3**, ninguno más.

   **Formato del paquete, definido el 2026-09-02** *(hallazgo 13 de QA: no estaba
   definido en ninguna parte, y «el paquete abre» no se puede comprobar sin saber
   qué se abre ni con qué)*. **Es un `.xlsx` escrito con `openpyxl`.** No es una
   elección libre: los criterios 1 y 2 de esta misma fase dicen «devolver un
   Excel», así que el paquete que sale y el que vuelve tienen que ser el mismo
   formato; y `CLAUDE.md` §3 no admite ninguna otra biblioteca de escritura sin
   engordar el ejecutable. Comprobable: el archivo abre en Excel sin mensaje de
   archivo dañado, y releído con `openpyxl` tiene **exactamente 3** filas de caso.

   ⚠️ **Decisión del dueño, no mía, si quiere otra cosa:** un paquete en PDF —más
   difícil de estropear al rellenarlo— **exigiría una biblioteca nueva** y
   contradiría la ronda de vuelta por Excel. Se anota como alternativa descartada
   por coste, no por mala.
6. Reconciliar dos veces seguidas el mismo Excel devuelto no duplica nada: el
   conteo de filas es el mismo tras la segunda pasada.

---

# FASE 7 — Contactos con el líder

**Bloqueada por:** FASE 1, **y por falta de especificación** (ver «Deuda abierta ·
Deuda menor · D-1»).

> ⚠️ **Esta es la fase con el criterio más flojo del documento, y lo digo aquí en
> vez de disimularlo.** `CLAUDE.md` y `DECISIONES.md` **no contienen ni una sola
> decisión sobre esta fase**: no dicen qué es un «contacto con el líder», qué
> campos lleva, quién es el líder respecto de un caso, ni qué se hace con un
> contacto que no da resultado. Lo que sigue es lo que el nombre de la fase
> permite deducir, y **necesita un pase de especificación propio antes de
> ejecutarse**. Los criterios de abajo son un punto de partida, no un contrato.

### Qué se hace (a falta de especificación)

- Registrar sobre un caso los contactos hechos con el líder de la unidad: cuándo,
  por qué medio, con quién y qué resultó.

### Qué NO se hace en esta fase

- ⛔ No se manda ningún mensaje desde el programa: se **registra** un contacto que
  ocurrió fuera (regla permanente 2, sin red).
- ⛔ Un contacto no marca nada como verificado.
- ⛔ No se borra un contacto registrado.

### Criterio de aceptación (provisional)

1. Registrar un contacto sobre un caso, cerrar el programa y reabrirlo: el
   contacto aparece en la ficha del caso con su fecha y su resultado, y un `SELECT`
   sobre la base lo confirma.
2. Un caso sin contactos muestra una frase en español que lo dice, no una lista
   vacía muda.
3. Anular un contacto registrado por error deja el registro visible con su motivo
   de anulación y su fecha. ~~`grep -rn "DELETE FROM"` sobre los `.py` de la fase
   devuelve **0**.~~

   **Reescrito el 2026-09-02 — hallazgos 1 y 11 de QA**, mismo defecto y misma cura
   que el criterio 4 de la FASE 6: **control positivo con las cinco formas
   (`5 de 5` o no vale)**, **lista blanca con denominador** sobre todas las
   instrucciones de escritura de la fase, y la **prueba de comportamiento** que no
   depende de leer código — `SELECT COUNT(*)` sobre la tabla de contactos devuelve
   el mismo número antes y después de anular uno.
4. **Criterio de cierre de la propia fase:** antes de darla por buena, existe en
   `DECISIONES.md` una entrada del dueño que diga qué campos lleva un contacto y
   qué es un líder aquí. Sin esa entrada la fase no cierra, aunque el código
   funcione.

---

# FASE 8 — Reportes e histórico

**Bloqueada por:** FASE 1.

Decisión adelantada en `DECISIONES.md`: un caso **se archiva** (`archivado = 1`
con fecha), **nunca se borra**; los archivados salen de las listas de trabajo pero
**siguen contando en los reportes**.

### Qué se hace

- Archivar casos y sacar los reportes del trabajo hecho.

### Qué NO se hace en esta fase

- ⛔ **No existe ninguna operación de borrado de casos ni de personas.**
- ⛔ Los reportes no reinterpretan nada: cuentan lo que hay.
- ⛔ Nada de gráficos que necesiten una biblioteca nueva y engorden el ejecutable.

### Criterio de aceptación

1. Archivar un caso: `SELECT archivado, fecha_archivado FROM ...` devuelve `1` y
   una fecha. El caso **desaparece** de las listas de trabajo y del bloque de
   viajes próximos.
2. `SELECT COUNT(*)` sobre la tabla de casos devuelve **el mismo número** antes y
   después de archivar.
3. El total del reporte del periodo es **el mismo número** antes y después de
   archivar un caso de ese periodo. Los archivados siguen contando.
4. El total que muestra el reporte coincide con el que devuelve la consulta SQL
   equivalente hecha a mano: **el mismo número por dos caminos**. Si no coinciden,
   la fase no pasa.
5. ~~`grep -rn "DELETE FROM"` sobre **todos** los `.py` del proyecto devuelve **0**
   coincidencias.~~

   **Reescrito el 2026-09-02 — hallazgos 1 y 11 de QA.** Es el más importante de
   los tres `DELETE FROM` del documento, porque este barre **el proyecto entero** y
   es el que sostiene la decisión de que un caso se archiva y nunca se borra. Misma
   cura, con una exigencia más:

   - **5a. Control positivo con denominador:** las **cinco** formas del criterio 4
     de la FASE 6, cazadas **5 de 5**, o el comando no vale y la fase no pasa.
   - **5b. Denominador del universo, que aquí no es opcional:** se anota **sobre
     cuántos `.py` y cuántas líneas** se buscó. Un cero sobre «todos los `.py` del
     proyecto» no significa nada si nadie dijo cuántos son. Ese número tiene que
     cuadrar con el total de `.py` del proyecto.
   - **5c. Lista blanca con denominador:** se enumeran **todas** las instrucciones
     SQL de escritura del proyecto —salen **N**— y ninguna de las N borra filas de
     casos ni de personas. N de N revisadas.
   - **5d. Comportamiento:** ya está en el criterio 2 de esta fase — el conteo de
     casos es el mismo antes y después de archivar. Se cita aquí porque es la
     prueba que sobrevive aunque los comandos fallen.
6. ~~El reporte generado se abre en el programa con el que se dice que se abre, sin
   mensaje de archivo dañado.~~

   **Reescrito el 2026-09-02 — hallazgo 13 de QA:** el criterio decía «el programa
   con el que se dice que se abre» y **en ningún sitio del documento se decía cuál
   era**. Un criterio que remite a algo no escrito no se puede comprobar.

   **El reporte es un `.xlsx` escrito con `openpyxl`.** Motivo: `CLAUDE.md` §3 no
   trae ninguna otra biblioteca de escritura, esta fase prohíbe expresamente
   «gráficos que necesiten una biblioteca nueva y engorden el ejecutable», y Miguel
   ya trabaja con el Excel espejo de la FASE 4. Comprobable: el archivo abre en
   Excel sin mensaje de archivo dañado, y releído con `openpyxl` el total que
   muestra coincide con el del criterio 4.

   ⚠️ **Alternativas y por qué no:** **CSV** no necesita nada y se abre en cualquier
   sitio, pero se lleva por delante los ceros de delante del MRN al abrirlo en
   Excel, que es justo el defecto que la FASE 4 se molesta en evitar. **PDF**
   necesita una biblioteca nueva. Si el dueño prefiere PDF, es **su decisión** y
   trae consigo un peso extra en el ejecutable que hoy nadie ha medido.

---

# FASE 9 — Empaquetado final

**Bloqueada por:** todas las anteriores.

Decisión adelantada en `DECISIONES.md`: **`--onedir`, no `--onefile`**. Con
`--onefile` los `.onnx` se descomprimen a una carpeta temporal en cada arranque y
el arranque tarda «entre 15 y 30 segundos» — cifra del plan del dueño, **sin
comprobar aquí**; en esta fase se mide de verdad.

### Qué se hace

- El paquete que Miguel abre con doble clic, probado en una máquina que no es la
  de desarrollo.

### Qué NO se hace en esta fase

- ⛔ **No se cambia a `--onefile`** por ganar un archivo suelto.
- ⛔ No se añaden funcionalidades. Si algo falta, es deuda para un ciclo nuevo.
- ~~⛔ No se instala nada en la máquina de destino: sin instalador, sin Python, sin~~
  ~~servicio, sin puerto (regla permanente 2).~~ Tachado el 2026-09-11: el dueño pidió instaladores; la regla 2 se precisó (DECISIONES.md, «EL DUEÑO PIDE INSTALADORES»). Lo demás —sin Python, sin servicio, sin puerto— sigue.

### Criterio de aceptación

1. El `.spec` **no** contiene configuración de un solo archivo, y `dist\Fichas`
   contiene la carpeta `_internal` (comprobable con `dir`).
2. **Copiar `dist\Fichas` entera a otra máquina Windows sin Python** y ejecutarla
   con doble clic: la ventana abre, se procesa un PDF de principio a fin y el caso
   queda guardado. Es la prueba de que no hay una dependencia oculta de la máquina
   de desarrollo.

   **Aquí no hay sustituto** *(añadido el 2026-09-02, hallazgo 12 de QA)*. La FASE 0
   admite tres niveles de prueba porque su respuesta es provisional; **esta fase no
   admite ninguno**: si la FASE 0 se cerró por su rama débil (2c), esa mitad de
   pregunta llega aquí sin responder y **es aquí donde se responde**. Se anota qué
   máquina fue y qué tenía instalado. Sin otra máquina —o una máquina virtual
   limpia— este criterio **no se da por cumplido**, y la fase no cierra: cerrarla
   sería entregarle a Miguel un ejecutable que sólo se ha visto arrancar donde se
   construyó.
3. **Tiempo de arranque cronometrado en segundos** en esa máquina, anotado como
   número. Se compara con el número medido en la FASE 0: si se ha más que
   duplicado, se investiga antes de cerrar la fase.
4. La ruta de carga de los `.onnx` que el programa imprime al arrancar cae dentro
   de `_internal` y el archivo existe ahí (PyInstaller documenta que en el paquete
   de una carpeta `sys._MEIPASS` es `_internal`; URL y cita en el encabezado).
5. ~~Con el programa corriendo, `netstat -ano | findstr <PID del programa>` **no
   devuelve ninguna línea en estado LISTENING**. Ningún puerto abierto.~~

   **Reescrito el 2026-09-02 — hallazgos 13 y 11 de QA.** Dos defectos: `findstr`
   casa el número **en cualquier columna**, así que un PID de `4820` casaba con una
   línea cuyo **puerto local** fuera 4820 y con cualquier dirección que contuviera
   esos dígitos — el filtro no filtraba lo que decía filtrar, en ninguno de los dos
   sentidos. Y el aprobado era un cero sin control positivo. Queda así:

   - **5a. Filtro exacto por PID, sin ambigüedad de columna.** En PowerShell:
     `Get-NetTCPConnection -State Listen -OwningProcess <PID>` **no devuelve
     ninguna línea**, y `Get-NetUDPEndpoint -OwningProcess <PID>` tampoco. El PID es
     un parámetro con nombre, no un número suelto que se busca en un texto.
   - **5b. Control positivo con denominador, obligatorio.** Las mismas órdenes **sin
     filtrar por PID** —`Get-NetTCPConnection -State Listen`— devuelven **N** líneas
     en esa máquina, y **N se anota**. Si sin filtro también salen cero, la orden no
     está viendo nada y el cero filtrado no vale: el criterio no se cumple.
   - **5c. El PID es el del programa, y se comprueba.** Se obtiene del proceso
     `Fichas.exe` en marcha, no se escribe de memoria. Se anota cuál fue.
   - **5d. También hacia fuera, no sólo escuchando.** Con el programa procesando un
     PDF, `Get-NetTCPConnection -OwningProcess <PID>` —sin `-State Listen`— tampoco
     devuelve nada. Un puerto cerrado no impide una llamada saliente, y la regla
     permanente 1 la prohíbe igual. Es el mismo criterio 12d de la FASE 2, aquí
     sobre el ejecutable final.
6. La base y el `.xlsx` están en la carpeta de datos y **no** dentro de
   `dist\Fichas`: se comprueba con `dir` sobre las dos rutas. Copiar encima una
   versión nueva de `dist\Fichas` **no** toca los datos: el conteo de filas es el
   mismo después.

   **La ruta de datos se resuelve por API, también aquí** *(añadido el 2026-09-02,
   hallazgo 3 de QA; `DECISIONES.md`, 2026-09-02, declara la decisión **vinculante
   para esta fase**)*. Este criterio decía `Documentos\Fichas` como si fuera una
   ruta fija, y no lo es — en esta máquina existen `Documents`,
   `OneDrive\Documentos` y `OneDrive\Documents`, y la que vale es la que devuelva la
   API. Se comprueba, en la máquina de destino y no en la de desarrollo:

   - **6a.** El programa **muestra la ruta de datos que resolvió antes de escribir
     nada**, igual que en la FASE 1. Se anota la ruta literal que mostró **en esa
     máquina**, que es el punto: la máquina de destino puede tener el perfil en otro
     sitio, otro idioma de Windows o OneDrive configurado distinto, y es
     exactamente el caso que componer la ruta por nombre rompe.
   - **6b.** Esa ruta coincide **carácter a carácter** con la que devuelve
     `[Environment]::GetFolderPath('MyDocuments')` **en la máquina de destino**.
   - **6c.** En el `.spec` y en los `.py` empaquetados, **nada** compone la ruta de
     datos a partir de las cadenas `"Documentos"` o `"Documents"`. **Aprobado con
     cero → control positivo:** el mismo comando sobre los dos mutantes del
     criterio 1c de la FASE 1 caza **2 de 2**, con el denominador de archivos
     buscados anotado.
   - **6d.** Si la ruta resuelta cae dentro de OneDrive, el programa lo dice en
     pantalla y queda anotado para el dueño. No suspende la fase; ocultarlo sí.
7. **Tamaño total de `dist\Fichas` en MB**, anotado como número. Se compara con el
   de `dist\prueba_ocr` de la FASE 0 y se anota la diferencia.
8. **Sin `pandas` en el paquete entregado** *(añadido el 2026-09-02, hallazgo 9 de
   QA)*. Es donde la regla permanente 3 se puede comprobar sobre el producto y no
   sobre el código: `pandas` no aparece en `dist\Fichas\_internal`, ni como carpeta
   ni como `.pyd`. **Control positivo:** la misma búsqueda sobre `openpyxl` —que
   tiene que estar— devuelve al menos **1**. Un cero de una búsqueda que tampoco
   encuentra `openpyxl` es una búsqueda rota, no un aprobado.

---

# Deuda abierta

## ~~Bloqueo 1 — No hay esquema de datos ⛔~~ ✅ CERRADO el 2026-09-02

**Tachado el 2026-09-04.** El dueño eligió la **opción B** de la comparación de
abajo —el esquema vive en `docs/ARQUITECTURA.md` §2, con el DDL del código como
autoridad de última instancia—, y desde entonces el esquema no solo existe: ha
crecido trece veces. Medido hoy sobre `c0a268c`, construyendo una base con
`abrir_conexion` + `aplicar_esquema` y preguntando al motor con
`PRAGMA table_info`:

```
VERSION_ACTUAL: 14 | aplicar_esquema devolvio: 14
TABLAS REALES (9): asignaciones, casos, companeros, contactos,
                   documentos_ilegibles, filas_descartadas, personas,
                   procedencia_campo, version_esquema
TOTAL_COLUMNAS 104          INDICES PROPIOS (8)
```

La recomendación que sigue abajo **se conserva entera y sin tocar**, porque es el
razonamiento con el que se tomó la decisión y explica por qué la sede es esa y no
un ADR. Lo que dejó de ser cierto es el encabezado: esto ya no bloquea nada.

> **Alcance corregido el 2026-09-02 — hallazgo 8 de QA.** Decía ~~«la FASE 1 y, por
> dependencia, de la 3 a la 8»~~ y **dejaba fuera dos fases que sí bloquea**:
>
> - **La FASE 2.** Sus criterios usan **cinco nombres del esquema que no existe**
>   —`numero_caso`, `mrn`, `fecha_viaje`, `origen`, `captura_manual`— y ahora también
>   `confianza`. El código de la fase se puede escribir contra nombres provisionales,
>   pero si el esquema los cambia hay que renombrarlos aquí, que es el retrabajo que
>   este bloqueo existe para evitar.
> - **La FASE 9**, que este documento declara «bloqueada por todas las anteriores».
>   Si la 1 está bloqueada, la 9 lo está por transitividad. Excluirla era una
>   inconsistencia del documento consigo mismo.
>
> **La única fase que este bloqueo NO detiene es la FASE 0**, que no toca la base de
> datos. Es la que se puede ejecutar hoy.

**Qué falta.** `CLAUDE.md` §7 declara que el esquema —nombres de tablas y
columnas— **manda sobre cualquier plan**, y no existe. Medido el 2026-09-02 con
`find` sobre OneDrive, Documentos y Escritorio: no hay ningún `esquema.md`
(registrado en `ESTADO.md`).

**Quién puede cerrarlo.** El **dueño** decide qué datos se guardan; el
**planificador** lo redacta; el **programador** lo materializa en la FASE 1.
Ningún agente puede inventarlo por su cuenta: el esquema es una decisión de
producto, no una preferencia técnica.

**Qué debe contener, a grandes rasgos** (no columna por columna — eso es la
decisión que falta): los casos con su `numero_caso`, `mrn`, `unidad_numero` y
`fecha_viaje`; las personas de cada formulario y su relación con el caso; las seis
casillas de ordenanzas; el origen y la confianza de cada valor extraído; la marca
manual de verificado con quién y cuándo; los compañeros y sus asignaciones (FASE
6); los contactos (FASE 7); y la marca de archivado con su fecha (FASE 8).
**Al menos una decisión de ahí dentro merece investigación comparada y su propio
ADR:** si las seis casillas de ordenanzas van como seis columnas o como tabla
hija. Esa elección se paga en todas las consultas de las fases 5 y 8.

### Dónde debe vivir — recomendación argumentada

Primero lo medido. La lista blanca de `.claude/hooks/no-crear-documentos.sh`
(leída el 2026-09-02, líneas 73-80) es de **nombres exactos**, más un patrón:

- Cualquier ubicación: `ESTADO.md` · `DECISIONES.md` · `PENDIENTES.md` ·
  `EN-CURSO.md` · `CLAUDE.md` · `PROCESOS.md` · `requirements.txt` ·
  `congelados.txt`
- Solo en `docs/`: `ARQUITECTURA.md` · `RUNBOOK.md` · `BITACORA-SESIONES.md` ·
  `GUIA-TECNOLOGIAS-BITACORA.md` · `INFORMES-QA.md`
- El patrón: `docs/adr/ADR-*.md`

Dos detalles medidos que cambian la decisión: el hook **solo mira `.md` y `.txt`**
(línea 33), así que un `.sql` o un `.py` no pasa por esta puerta; y **`docs/esquema.md`
está descartado**, no está en la lista. Además, `propiedad-por-rol.sh` (línea 66)
asigna al **planificador** tanto `docs/ARQUITECTURA.md` como `docs/adr/*`.

| Opción | A favor | En contra |
|---|---|---|
| **A. `docs/adr/ADR-0001-esquema-de-datos.md`** | Permitida por el patrón; titular el planificador; formato pensado para decisiones con alternativas | Los ADR son **inmutables** (`CLAUDE.md` §7). El esquema **va a crecer** en las fases 6, 7 y 8. Un documento inmutable no puede ser la autoridad viva sobre nombres de columnas: obligaría a un ADR nuevo por cada columna añadida y a adivinar cuál es el vigente |
| **B. `docs/ARQUITECTURA.md`, sección «Esquema de datos»** | Permitida; titular el planificador; **editable en el sitio**, que es lo que el método pide; un esquema es parte legítima del «mapa del sistema» | `CLAUDE.md` §7 dice que los documentos de conocimiento se crean «cuando haya algo medido que poner dentro»; un esquema acordado es una decisión, no una medición. Es la única objeción, y es de forma |
| **C. Solo el DDL en el código de la FASE 1** | `CLAUDE.md` §8: cuando documento y código no coinciden, **gana el código**. Si el esquema vive en el código no puede desincronizarse, y `sqlite3 base.db ".schema"` lo verifica en un comando | **Circular**: el esquema tiene que existir *antes* de la FASE 1, y el código de la FASE 1 es lo que está bloqueado por su ausencia. Además lo escribiría el programador, y la decisión no es suya |

**Recomendación: B como sede, con C como autoridad de última instancia.**

1. El esquema acordado se escribe en **`docs/ARQUITECTURA.md`, sección «Esquema de
   datos»**, por el planificador, con la decisión del dueño detrás. Eso desbloquea
   la FASE 1.
2. El **DDL real del código** es la autoridad cuando ambos difieran, por
   `CLAUDE.md` §8, y el documento se corrige contra él.
3. Cada cambio posterior de esquema se registra en **`DECISIONES.md`** con su
   fecha y su motivo.
4. Un **ADR** solo para la decisión que de verdad tiene alternativas comparables
   (las seis casillas: seis columnas o tabla hija).

**Alternativa por si el dueño prefiere no abrir `docs/` todavía:** meter la
sección de esquema en **`DECISIONES.md`**, que ya existe y está en la lista blanca.
Funciona, y tiene un coste real: `DECISIONES.md` es cronológico, así que el esquema
vigente habría que reconstruirlo leyendo entradas por fecha — justo lo que un
documento-autoridad no debe obligar a hacer. **La decisión es del dueño.**

## Bloqueo 2 — No está el PDF de referencia ⚠️ *degradado el 2026-09-04: ya no bloquea la fase, sí sus cuatro criterios literales*

**Puesto al día el 2026-09-04.** Llegaron **otros** documentos reales y la FASE 2
se ejecutó sobre ellos. Medido hoy:

```
$ find . -iname "*.pdf" -not -path "./.venv/*" -not -path "./.claude/*"
./dist/prueba_ocr/_internal/prueba_latina.pdf
./pdfs_referencia/59d87aad-CASP2609_Jonas_Ficticio.pdf
./pdfs_referencia/60025df4-CASP2609_Daniel_Jr._Damian_Dorian_Ejemplo.pdf
./pdfs_referencia/aa038ec0-PARB2609_Nora_Clara_De_Ensayo_Complete.pdf
./pdfs_referencia/cb2f18be-SURB2609_Suriname_Group_Complete.pdf
./pdfs_referencia/ed633b94-SURB2609_Suriname_Group_Complete.pdf
./prueba_latina.pdf
```

**Cinco documentos reales, ninguno es `Zutano_Family`.** Lo que sigue abierto es
más estrecho que lo que decía este bloqueo, y por eso se degrada en vez de
cerrarse: los criterios 1, 3, 4 y 5 de la FASE 2 **nombran ese archivo y sus
coordenadas literales** (`[35.9, 437.2, 106.2, 444.6]`, las 22 anotaciones, el
par tachón + `/FreeText`), y nada de eso se ha comprobado contra él. Se comprobó
otra cosa sobre otros papeles. **La regla de precedencia del tachón rojo sigue
sin tener su demostración**, que era lo que el criterio 3c anticipaba: «si el PDF
real no contiene ningún par así, la regla queda sin verificar».

**Qué falta.** ~~`CASP2609_Zutano_Family.pdf`. Medido el 2026-09-02 con `find` sobre
OneDrive, Documentos y Escritorio: no está en el disco (`ESTADO.md`).~~
`CASP2609_Zutano_Family.pdf` — **sigue sin estar, remedido el 2026-09-04 con el
`find` de arriba**. Y se le suma el **`PAPH2608`** que el dueño no ha enviado
(ver «Lo que solo el dueño puede dar»).

**Qué impide, en concreto.** Sin él no se puede comprobar ninguno de los puntos 1
a 6 del criterio de la FASE 2 —el conteo de anotaciones, el caso de la fecha
tachada, la conversión de coordenadas, el borde del 50% de solapamiento— y **todo
el bloque técnico de `DECISIONES.md` que gobierna esa fase se queda como hipótesis
sin comprobar**. Y con un solo PDF tampoco se puede **calibrar el umbral de las
casillas de ordenanzas**: para eso hacen falta varios formularios reales.

**Quién puede cerrarlo.** Solo el **dueño**. No se puede fabricar: un PDF
inventado por un agente no probaría nada, y fabricar datos de personas para
probar contradice el motivo entero del `.gitignore`.

**Qué hace falta exactamente:**
- `CASP2609_Zutano_Family.pdf`, para los criterios 1 a 6 de la FASE 2.
- **Al menos 3 formularios escaneados reales más**, con casillas marcadas, para
  calibrar el umbral (criterio 11 de la FASE 2). Cuantos más, mejor el número.
- Al menos **1 formulario manuscrito**, para probar el marcado `captura_manual`
  (criterio 10).
- Ninguno de ellos entra en el repositorio: `.gitignore` deja fuera `*.pdf` por
  decisión del 2026-09-02.

**Mientras tanto:** la FASE 2 se puede **escribir**, pero **no se puede dar por
buena**. Si se ejecuta antes de que llegue el PDF, se cierra con la verificación
explícitamente pendiente, no con un «pasa».

---

## Deuda menor — lo que nadie ha preguntado todavía

Ninguna de estas bloquea una fase hoy. Todas se convierten en retrabajo si se
descubren tarde.

**D-1. La FASE 7 no tiene ni una decisión escrita.** Ni en `CLAUDE.md` ni en
`DECISIONES.md`. Las fases 4, 5, 6, 8 y 9 tienen sus decisiones adelantadas; la 7
no. Necesita un pase de especificación antes de ejecutarse. *Lo cierra: el dueño.*

**D-2. Los seis hallazgos de no regresión no están medidos en este repositorio.**
`DECISIONES.md` lo dice expresamente. Se están respetando por prudencia, que es
correcto, pero **una regla sin número propio no se puede defender** cuando alguien
proponga cambiarla. Cada una necesita su medición en cuanto el código exista: el
tiempo con `medianBlur` frente a `fastNlMeansDenoising`, el tiempo con y sin tope
de 3500 px, y la precisión con modelos latinos frente a los de por defecto.
*Lo cierran: programador y QA, durante la FASE 2.*

**D-3. ~~⚠️ La base de datos con nombres y MRN reales va a vivir en
`Documentos\Fichas`, y `Documentos` en esta máquina puede estar sincronizado con
OneDrive.~~ MEDIDO Y DECIDIDO el 2026-09-02** — actualizado aquí el mismo día,
tras el hallazgo 3 de QA, porque el texto de abajo pedía medir algo que ya se
midió y eso deja de ser cierto.

**Lo que midió el supervisor:** `Documentos` **no** está redirigido a OneDrive
(`User Shell Folders → Personal = C:\Users\josem\Documents`). Pero existen **las
tres** carpetas: `C:\Users\josem\Documents`, `OneDrive\Documentos` y
`OneDrive\Documents`. **El riesgo cambió de forma:** no es redirección, es
**resolución de ruta por nombre** — un programa que arme `Documentos\Fichas`
pegando la cadena puede aterrizar en OneDrive y subir la base entera sin que nadie
lo note.

**La decisión está en `DECISIONES.md` (2026-09-02) y es vinculante:** la carpeta se
obtiene con la API de carpetas conocidas de Windows, nunca por nombre, y el
programa muestra la ruta que resolvió antes de escribir nada. **Ya tiene criterios
de aceptación**, que es lo que faltaba: FASE 1 crit. 1 (1a-1e) y FASE 9 crit. 6
(6a-6d). Deja de ser deuda abierta.

⚠️ **Lo que sigue sin medir**, y se hereda: si el cliente de OneDrive tiene activada
la copia de seguridad de carpetas conocidas y podría redirigir `Documentos` más
adelante. La decisión protege igual en ese caso —la API devuelve la ruta vigente,
sea cual sea— y por eso los criterios 1e y 6d **avisan en pantalla** en vez de
suspender. *Lo decide: el dueño, si la ruta resuelta aparece bajo OneDrive.*

**El texto original de esta deuda, conservado y tachado en el sitio** porque su
petición ya se cumplió:

> ~~El `.gitignore` deja fuera `*.pdf`, `*.db` y `*.sqlite*` con un motivo
> explícito en `DECISIONES.md` — «llevan nombres y MRN de personas reales, y no se
> suben a ningún sitio». Si `Documentos` sincroniza, **la base sube a la nube por
> una puerta distinta**, y la protección del `.gitignore` queda a medias. Que el
> proyecto entero viva bajo `OneDrive\Escritorio\Trabajo` hace la pregunta más
> urgente, no menos. Hay que **medir si esa carpeta sincroniza** y, si sincroniza,
> que el dueño decida: excluir `Documentos\Fichas` de la sincronización, moverla, o
> aceptarlo a sabiendas. *Lo mide: el supervisor. Lo decide: el dueño.*~~

El razonamiento del `.gitignore` sigue siendo válido y por eso se conserva legible:
es el motivo por el que la decisión de la API importa. Lo que ya no es cierto es la
petición —«hay que medir»—, porque está medido, y el veredicto está arriba.

**~~D-4. No hay decisión sobre qué pasa al procesar dos veces el mismo PDF.~~**
~~¿Se detecta que ese `numero_caso` ya está y se ofrece reemplazar, se crea un
duplicado, o se rechaza? Afecta al esquema (¿es `numero_caso` único?), así que
conviene decidirlo **antes** de cerrar el Bloqueo 1. *Lo decide: el dueño.*~~

✅ **CERRADA el 2026-09-03 por el dueño, y construida en `2b6e92a`.** Su
respuesta, literal (`DECISIONES.md`, «P-5, decidida por el dueño»): *«Si hay
documentos duplicados debe decirlo y no rechazarlo.»* **Ninguna de las tres
opciones que esta deuda planteaba**: el documento **entra y se marca**. Y la
pregunta de debajo —«¿es `numero_caso` único?»— se contestó al revés de lo que
estaba escrito: **deja de serlo** (migración 12), porque el número es unidad +
AAMM e identifica un mes de una unidad, no una familia. La marca es
`casos.duplicado_de` (migración 13), verificada hoy en el volcado de columnas.

⚠️ **El resto que NO cierra, medido el 2026-09-04 en
`importacion/guardado.py:501-504`**, literal: *«No es una huella del contenido:
es la ruta. El mismo archivo copiado a … la base no guarda ninguna huella del
archivo y este pase no la anade.»* Consecuencia: **un archivo copiado a otra ruta
y sin MRN legible entra sin marca de duplicado.** Ver «Deuda del ciclo 5 · DC-4».

**D-5. No hay copia de seguridad de la base.** Un `.db` corrupto o borrado por
error se lleva todo el trabajo de Miguel, y la única red hoy es la sincronización
de OneDrive, que es precisamente lo que D-3 pone en duda. *Lo decide: el dueño;
lo especifica el planificador.*

**D-6. Ninguna fase tiene objetivo de rendimiento fijado por el dueño.** Los
criterios de este documento piden **anotar** tiempos y tamaños, que es lo correcto
mientras no haya con qué compararlos. Pero nadie ha dicho cuántos formularios
procesa Miguel en una sesión ni cuánto puede esperar por uno. Sin ese número, «el
tiempo por página es 40 segundos» no se puede juzgar. *Lo decide: el dueño.*

**D-7. `docs-al-dia.sh` no va a disparar jamás en este proyecto**, porque no hay
remoto y nunca se hace `push` (`DECISIONES.md`, 2026-09-02). Lo que esa puerta
protegía —que el trabajo no avance sin su registro— **hoy depende de que alguien lo
cumpla a mano en cada cierre de fase**. Es una protección que se cree activa y no
lo está; conviene que el cierre de cada fase la sustituya explícitamente.
*Lo asume: el supervisor.*

**D-8. ~~Los cinco documentos de conocimiento de `CLAUDE.md` §7 no existen.~~
Existen dos de los cinco.** ~~Es correcto: se crean cuando haya algo medido que
poner dentro. Queda anotado aquí para que su ausencia sea una decisión y no un
olvido. El primero que va a hacer falta es `docs/ARQUITECTURA.md`, por el
Bloqueo 1.~~

**Puesto al día el 2026-09-04**, medido con `ls docs/` y `ls docs/adr/`:

| Documento | Titular | Estado hoy |
|---|---|---|
| `docs/ARQUITECTURA.md` | planificador | ✅ existe (130 325 bytes) |
| `docs/GUIA-TECNOLOGIAS-BITACORA.md` | programador | ✅ existe (16 756 bytes) |
| `docs/RUNBOOK.md` | programador | ❌ no existe |
| `docs/BITACORA-SESIONES.md` | supervisor | ❌ no existe |
| `docs/INFORMES-QA.md` | QA | ❌ no existe |
| `docs/adr/ADR-*.md` | planificador | ✅ dos: `ADR-0001-casillas-de-ordenanzas.md`, `ADR-0002-pais-unidad-y-templo.md` |

⚠️ **De los tres que faltan, el que más pesa es `docs/RUNBOOK.md`**, y hoy hay
material medido de sobra para llenarlo: el `sqlite3.connect` a secas que revienta
la migración 2, la migración registrada que rompe la prueba que la aplicaba a
mano, el `after` huérfano que dispara sobre una pantalla destruida, los 5 ms por
widget de Tk en Windows. **Cada uno de esos cuatro le costó horas a alguien y
ninguno está donde se busca cuando vuelva a pasar.** *Lo cierra: el programador.*
`docs/INFORMES-QA.md` es de QA y `docs/BITACORA-SESIONES.md` del supervisor; no
son míos y no los propongo aquí más allá de decir que no están.

---

# Deuda del ciclo 5 — lo que devolvieron los cuatro pases

*Añadida el 2026-09-04, sobre `c0a268c`.* Ninguna de estas la levanté yo: **las
devolvieron los propios programadores al cerrar su pase**, o salen de una
decisión del dueño de los días 3 y 4. Cada una lleva **de dónde viene**, porque
una deuda sin origen se discute dos veces. Las cifras que van sin comando son
**del informe de su programador y no las he repetido**; las que llevan comando
las medí yo hoy.

## Lo entregado, para que se lea contra qué se debe

| Pase | Commit | Qué cerró |
|---|---|---|
| Revisar + «completa» / «no está completa» | `4572693` | Tarjeta por documento, los dos botones, tablero de completados, migración 14 |
| Panel de inicio v2 | `657a5cc`, `62c5e1f` | Funciones del viejo en la cara del nuevo, menú de iconos, calendario con archivados |
| Identidad por documento | `2b6e92a` | Ninguna página se rechaza, `duplicado_de`, visor libre, trackpad, `fichas.log`, migraciones 12-14 |
| El dibujado | `c0a268c` | Calendario y listas en `Canvas`, pantallas que no se reconstruyen, un solo ayudante de trackpad |

## DC-1. El menú de iconos sigue en widgets: 26 de los 82 de inicio

*Origen: entrega del pase 4 (`c0a268c`), devuelta por su programador.*

El pase 4 llevó el calendario y las listas a un `Canvas` y dejó inicio en **82
widgets** (venía de 186). **26 de esos 82 son el menú de iconos**, que sigue
siendo `Frame` + `Canvas` + dos `Label` por sección. Leído hoy en
`interfaz/menu_de_iconos.py:181-194`: esa es la forma de cada fila, y las
secciones son cinco.

**Por qué importa el número y no es cosmético:** el criterio del pase era
**volver a inicio en ≤ 0,2 s** y quedó en **0,218 s** — a **18 ms**. A los ~5 ms
por widget que midió el supervisor en esta máquina, el menú es del orden de
0,13 s de esos 0,218. **Es el único bloque que queda por convertir y es el que
sobra para cumplir el criterio.** *Lo cierra: el programador. No urgente: el
criterio se incumple por 18 ms, no por un segundo.*

## DC-2. El visor de corrección no se reutiliza — y el pase lo pedía

*Origen: criterio 4 del pase 4, declarado no cumplido por su programador.*

El encargo decía literal: *«Corrección se construye por caso pero **reutiliza el
visor** si ya existe»*. No se hizo. Medido hoy en
`interfaz/aplicacion.py:360-384`: `abrir_caso` construye una `PantallaDeCorreccion`
nueva en cada llamada, con su visor dentro.

**El motivo que dio su programador, y lo doy como cita suya, NO como medición
mía:** un widget de Tk no puede cambiar de padre después de creado, así que un
visor que vive dentro de una pantalla no se puede mover a la siguiente. Si eso es
cierto, la reutilización exige **sacar el visor de la pantalla** y colgarlo de la
ventana, que es un cambio de estructura y no un retoque.

**Lo que costó:** `abrir_caso` en el segundo caso quedó en **0,597 s** contra un
criterio de ≤ 0,8 s, así que **el criterio se cumple igualmente**. La deuda no es
el número: es que el camino barato para el caso de seis hojas sigue sin existir,
y el dueño abre casos todo el día. *Lo especifica el planificador si se decide
sacar el visor; lo cierra el programador.*

## DC-3. `toca_atender` y `UNO_DE_CADA` viven en `interfaz/gestos.py` y no los usa nadie

*Origen: punto 6 del pase 4 —«`interfaz/gestos.py` desaparece o queda reducido a
lo que desplazamiento no cubra»—, cumplido a medias.*

Medido hoy, sobre todo el árbol y excluyendo los worktrees de `.claude/`:

```
$ grep -rn "toca_atender\|UNO_DE_CADA" --include=*.py .
./pruebas/prueba_trackpad.py:26,84,85,88,89,92      (6 lineas, todas de prueba)
./interfaz/gestos.py:41,53,60                        (la definicion)

$ grep -rn "from interfaz.gestos" --include=*.py .
./interfaz/visor.py:56:from interfaz.gestos import deltas_del_trackpad
```

**Denominador dicho:** de los tres nombres públicos de `gestos.py`, **uno**
(`deltas_del_trackpad`) lo usa el programa; **dos** (`toca_atender`,
`UNO_DE_CADA`) solo los ejercita su propia prueba. Código que solo mantiene viva
su prueba parece cubierto y no lo está. *Lo cierra: el programador — borrarlos
con su prueba, o dejar escrito para qué se guardan.*

## DC-4. Un archivo copiado a otra ruta y sin MRN legible entra sin marca de duplicado

*Origen: devuelta por el programador de identidad al cerrar `2b6e92a`, y no
entraba en el pase 4.*

Leído hoy en `importacion/guardado.py:501-504`, literal: *«**No es una huella del
contenido: es la ruta.** El mismo archivo copiado a … la base no guarda ninguna
huella del archivo y este pase no la anade.»* Y en la línea 37 del mismo módulo:
*«No hay huella criptografica»*.

Los dos caminos de detección son la **ruta** (`ruta_pdf` + `pagina_pdf`) y los
**MRN dentro del número de caso**. Si el archivo cambia de ruta **y** ninguna
persona trae MRN legible, no queda ninguno: el documento entra como caso nuevo
sin decir de cuál repite, que es exactamente lo que el dueño pidió que se dijera.

**Lo que costaría cerrarlo:** una huella del contenido del archivo (`hashlib` es
biblioteca estándar, no añade peso al `.exe`) y una columna donde guardarla —o
sea, **migración 15 y una decisión de esquema**. No la tomo aquí: va a
`docs/ARQUITECTURA.md` como pregunta, porque decide si la identidad del documento
es la ruta o el contenido. *Lo decide: el dueño, sobre la comparación que le
prepare el planificador.*

## DC-5. «Revisar» tiene tope de página, no dibujado por ventana visible

*Origen: declarado por el propio código del pase 4.*

Medido hoy en `interfaz/revisar.py`: `TARJETAS_POR_PAGINA = 24` (línea 51), y el
comentario de la línea 224 lo dice él mismo, literal: *«Esto es un tope por
pagina, **no** el dibujado por ventana visible que pide…»*.

**Qué significa para el dueño:** con 3 000 documentos ve 24 y un botón de «ver
más»; cada pulsación **suma** otra página a lo dibujado, no la sustituye. Es
correcto y es barato hoy; deja de serlo cuando alguien pulse «ver más» veinte
veces. *Lo cierra: el programador, cuando haya un número que lo justifique. No
hoy: nadie ha medido a partir de qué página duele.*

## DC-6. Un templo mal leído no se corrige a mano

*Origen: decisión del programador aceptada el 2026-09-03, con su coste dicho.*

`casos.templo_nombre` (migración 10) **va sin fila en `procedencia_campo` y sin
campo en la pantalla**, para no dejar «Todo correcto» bloqueado en todos los
casos por un campo firmable que no se dibuja. El coste, ya escrito en
`docs/ARQUITECTURA.md` §2.2: una hoja leyó **`Caracas-Venezueia`** y se queda
así. Correcto por la regla permanente 1 —no se inventa nada— pero **el dato malo
no tiene arreglo**. Se cierra cuando exista el catálogo de templos, que es P-10 y
es del dueño. *Encadenada a «Lo que solo el dueño puede dar».*

## DC-7. Módulos largos — y son 27, no dos

*Origen: el pase que me encargó esto nombra `guardado.py` y `visor.py`.* Los dos
están, y **la lista es mucho más larga**. Medido hoy sobre los `.py` de
producción (sin `pruebas/`, `.venv/`, `build/`, `dist/`, `.claude/`):

```
$ find . -name "*.py" <exclusiones> -exec wc -l {} + | sort -rn | awk '$1>300'
1615 interfaz/correccion.py      983 interfaz/inicio.py
 839 importacion/guardado.py     728 datos/migraciones.py
 629 interfaz/visor.py           569 interfaz/asignacion.py
 530 interfaz/aplicacion.py      526 interfaz/seguimiento.py
 ... 27 archivos en total por encima de 300 lineas
```

**Corrijo la premisa del encargo:** si el umbral es 300 líneas, la deuda son
**27 módulos**, y el que más pesa **no** es ninguno de los dos nombrados sino
`interfaz/correccion.py` con **1 615**, casi el doble que el siguiente. Partir
módulos **no entraba en el pase 4** por decisión escrita («No entra en este
pase: … partir módulos largos»). *Lo decide: el dueño, porque cuesta un pase
entero y no añade ninguna función. Lo especifica el planificador si dice que sí.*

## DC-8. `interfaz/tarjetas.py` no lo importa nadie

*Origen: devuelta por el programador de inicio (`657a5cc`), literal en
`DECISIONES.md`: «El cajón de soltar no recibe arrastre: Tk no lo trae sin
dependencia nueva. Es un botón y su rótulo lo dice. `interfaz/tarjetas.py` quedó
sin uso.»*

Confirmado hoy: `grep -rn "tarjetas" --include=*.py . | grep -i import` → **sin
salida**. Ningún módulo lo importa. Es un archivo entero muerto en el árbol.
⚠️ **Y no se confunde con `interfaz/tarjeta_de_documento.py` (315 líneas), que sí
se usa**: los nombres se parecen y borrar el equivocado rompe «Revisar». *Lo
cierra: el programador.*

## DC-9. `Ctrl+I` cambió de significado — y es decisión del dueño, no defecto

*Origen: `DECISIONES.md`, 2026-09-03, devuelto por el programador de inicio.*

`Ctrl+I` pasó a ser **el informe** (como el mockup) y importar PDF se fue a
`Ctrl+O`. **Es memoria muscular de Miguel**, que lleva meses con el atajo
anterior. Su programador lo marcó como decisión del dueño y **el dueño no la ha
contestado**. Se anota aquí para que no se dé por aceptada por silencio: un atajo
que hace otra cosa de la que hacía ayer es un dato importado por error, no una
molestia. *Lo decide: el dueño, con una frase.*

## DC-10. «1 personas» en un aviso del reporte — y la línea es la 83, no la 100

*Origen: el encargo lo sitúa en `reportes/avisos.py:100`.* **Corrijo la cita**,
medido hoy:

```
$ grep -rn "personas\b" reportes/avisos.py
83:        f"{len(personas_sin_fecha)} personas anotadas como que no pudieron viajar "
```

La línea 100 es `avisos_del_reporte`, que no compone ningún plural. **El defecto
existe y está en la 83:** con una sola persona el reporte escribe «1 personas».
Es lo más barato de esta lista y lo ve el dueño en un documento que enseña a la
dirección. *Lo cierra: el programador.*

⚠️ **Y su hermano ya cerrado, para que nadie lo reabra:** `avisos.py:64`
—`sorted` reventando con un caso sin número— **sí se arregló en el pase 4**;
leído hoy en el comentario de la línea 69. Lo que queda es solo el plural.

## DC-11. La importación conducida dentro del `.exe` no se ha hecho nunca

*Origen: `DECISIONES.md`, 2026-09-03, escrito por el supervisor: «La próxima
auditoría de QA conduce Reportes y Archivar **dentro del `.exe`** con casos
importados; hasta hoy solo se verificó desde el código.»*

Es la deuda de verificación más grande que queda abierta, y no es de esquema ni
de interfaz: **nadie ha importado un PDF dentro del ejecutable y seguido el
camino entero** —importar, corregir, marcar, reportar, archivar— con el binario
que se entrega. Todo lo medido hasta hoy o corre desde el código fuente, o es el
arranque del `.exe` cronometrado. El único que lo ha intentado con el paquete
real es el dueño, y su veredicto fue *«no me gustó para nada»*.

**Esto es de QA y no es mío**, y lo escribo aquí porque es deuda del proyecto,
no porque vaya a auditarla. *Lo cierra: QA.*

## DC-12. Tk 8.6 frente a Tk 9.0.4 sigue sin medir — y necesita permiso del dueño

*Origen: `DECISIONES.md`, 2026-09-03, punto 3 de «Aún el dibujado del sistema es
lento», literal: «solo hay un Python en la máquina (3.14, Tk 9.0.4). Probarlo
exige descargar otro Python; lo autoriza el dueño.»*

Todo el diagnóstico del dibujado —5 ms por widget porque en Windows cada widget
de Tk es una ventana del sistema— se midió **sobre una sola versión de Tk**. Que
Tk 8.6 se comporte igual, mejor o peor **es una hipótesis sin un solo número**.
Si resultara mejor, parte del trabajo del pase 4 habría tenido una alternativa
más barata. *Lo autoriza: el dueño (descargar Python 3.13). Lo mide: el
supervisor.*

## DC-13. Regla nueva, ya en vigor: una prueba no aplica una migración a mano

*Origen: `DECISIONES.md`, 2026-09-03, «Precedente: registrar una migración rompe
la prueba que la aplicaba a mano».*

Al registrar la migración 14 en `MIGRACIONES`, `aplicar_esquema` empezó a
aplicarla sola y el `setUp` de la prueba de revisión la volvía a aplicar:
`duplicate column name: estado_marcado_por`, **45 errores, los mismos 45 en dos
pasadas** — no era ruido de suites concurrentes, era real.

**La regla, que es de este documento a partir de hoy y vale para toda prueba que
se escriba:**

> Una prueba **pide el esquema a `aplicar_esquema`** y comprueba la versión. **No
> llama a `migrar_a_version_N` a mano.** Una prueba que aplica su propia
> migración pasa mientras esa migración no está registrada y **rompe el día que
> se registra**, que es justo el día en que menos ruido hace falta.

**Cómo se comprueba, con denominador y control positivo** (regla del control
positivo de este documento): se enumeran **todas** las llamadas a
`migrar_a_version_*` en `pruebas/` —sale un número **N**, que se anota— y **N de
N** están en las pruebas de la propia migración, no en el `setUp` de una pantalla
o de un repositorio. **Si N sale 0, la enumeración está mal hecha**: hay al menos
las de los módulos de migración. *La aplica: el programador. La verifica: QA.*
**El día que alguien enganche la 15 no debe volver a pasar.**

## DC-14. Nadie decide si el historial de marcas del compañero se guarda

*Origen: **devuelta expresamente al planificador** por el programador de Revisar,
literal en `datos/migraciones_de_revision.py`: «si la misma hoja vuelve dos
veces, la segunda pisa a la primera. Guardar TODAS las marcas de la historia es
una tabla nueva y una decisión de arquitectura — **no la tomo yo, va al
planificador**.»*

Hoy `casos` guarda **una** marca del compañero en tres columnas
(`estado_del_companero`, `_por`, `_en`, migración 14). Si Sandy manda su hoja el
lunes y una corregida el martes, **la del lunes desaparece sin dejar rastro**.

**No la decido yo tampoco**, porque cambia el esquema y el dueño paga el coste:
queda escrita como pregunta abierta **P-15** en `docs/ARQUITECTURA.md` §7, con
las dos formas comparadas. *Lo decide: el dueño.*

## DC-15. La migración 15 reconstruye `personas` y nunca ha corrido sobre una persona

*Anotada el 2026-09-04 por el planificador. **Nadie la había escrito**: no la
devolvió ningún pase, salió de medir.*

La migración 15 (`datos/migraciones_de_cedula.py`) **no es un `ALTER TABLE`**: crea
`personas_version_15`, copia las 25 columnas con un `INSERT … SELECT`, hace `DROP
TABLE personas`, renombra y recrea `idx_personas_caso`. Es el procedimiento oficial
de SQLite y está bien escrito. **El problema no es el código: es que la parte que
puede perder datos —la copia— no se ha ejercitado nunca con datos dentro.**

**El tamaño, medido el 2026-09-04 en solo lectura sobre todas las bases reales que
existen en esta máquina** (`sqlite3` con `mode=ro`, contando **solo** `MAX(version)`
y `COUNT(*)`; ni un nombre ni un MRN leído):

| Base | Versión | casos | personas | personas con MRN |
|---|---|---|---|---|
| `Documents\Fichas\fichas.db` (la viva) | 13 | 2 | **0** | 0 |
| `Fichas-entrega\respaldo-base-20260903` | 8 | 2 | **0** | 0 |
| `…-20260903-0815` | 8 | 2 | **0** | 0 |
| `…-20260903-1251` | 8 | 2 | **0** | 0 |
| `…-20260903-1420` | 8 | 2 | **0** | 0 |
| `…-20260903-1630` | 8 | 2 | **0** | 0 |
| `…-20260903-1815` | 8 | 2 | **0** | 0 |
| `…-20260903-2100` | 11 | 2 | **0** | 0 |
| `Fichas-work\respaldo-base-20260903` | 8 | 2 | **0** | 0 |

⚠️ **El pase decía «el único respaldo real disponible». Son ocho respaldos más la
base viva: nueve bases. El número cambia, la conclusión no la cambia — y la
refuerza:** `personas` tiene **0 filas en las nueve**. No existe hoy ningún dato
del dueño sobre el que esa copia se pueda ensayar. Sobre una base nueva la
migración corre con la tabla vacía, así que **la suite en verde no dice nada de
esto**: prueba el `DROP`/`RENAME`, no la copia.

**Y hay un segundo filo, que es el que de verdad puede doler.** `procedencia_campo`
apunta a la persona por el par `(tabla, registro_id)` y el motor **no puede
defender ese par con una clave foránea** (`docs/ARQUITECTURA.md` §2.7). Si la copia
no conservase los `id` —los conserva: van nombrados los primeros en los dos lados
del `INSERT`, lo leí— **la procedencia de cada campo quedaría apuntando a la
persona equivocada, en silencio y sin que ningún `CHECK` se queje**. La base viva
tiene **8 filas de procedencia y 0 personas**, así que hoy ese daño no se puede ni
provocar ni observar.

**Qué hace falta, y de quién es:**

1. **Una prueba que aplique la 15 sobre una base con personas dentro** y compruebe,
   además del recuento, que `procedencia_campo.registro_id` sigue apuntando a la
   misma persona antes y después. *Es de QA, y es barato: no necesita datos del
   dueño, los puede fabricar.*
2. **La comprobación con datos reales solo la puede hacer el dueño**, y hasta que
   su base tenga personas dentro **no se puede hacer**. Hoy está en la versión 13:
   la primera vez que abra el programa nuevo se le aplicarán **dos** migraciones
   seguidas, la 14 y la 15, y la 15 es la que reconstruye. *Antes de eso: respaldo
   con fecha, y comparar `COUNT(*)` de `personas` y de `procedencia_campo` antes y
   después.*

**El riesgo que se acepta mientras tanto, dicho sin adornos:** hoy es **cero
personas en juego**, y por eso esto es una anotación y no una alarma. El día que su
base tenga cien personas dentro, deja de serlo — y la migración ya estará escrita y
dada por buena desde hace semanas.

## DC-16. `Microsoft.Data.Sqlite`: la versión es la 10.0.11, y vale para los seis terrenos

*Devuelta por el programador del terreno Datos el 2026-09-04. La anoto aquí porque
**afecta a todos los terrenos**, no solo al suyo.*

**Lo que él midió y NO he comprobado yo** (el pase me prohíbe lanzar `dotnet` con
seis programadores compilando): que la `10.0.0` **ni siquiera restaura** con
`TreatWarningsAsErrors`, porque el aviso de vulnerabilidad se convierte en error.
*Lo dice el programador de Datos y NO lo he comprobado.* Y que el supervisor corrió
`dotnet list package --vulnerable` con la `10.0.11` y respondió **«no vulnerable
packages»**. *Lo dice el supervisor y NO lo he comprobado.*

**Lo que sí verifiqué yo, y con qué:**

- **El aviso existe, y no dice lo que el pase daba a entender.**
  <https://github.com/advisories/GHSA-2m69-gcr7-jv3q>, consultado el **2026-09-04**.
  Es **CVE-2025-6965**, severidad **alta, CVSS 7,2**. Los paquetes afectados
  **no son `Microsoft.Data.Sqlite`**: son `SQLitePCLRaw.lib.e_sqlite3` y sus dos
  variantes de móvil, en el rango literal **«≤ 2.1.11»**. Llegan como dependencia
  transitiva. El fondo es SQLite anterior a **3.50.2**: un recuento de términos de
  agregado podía superar las columnas disponibles y corromper memoria.
- ⚠️ **El aviso declara «Patched Versions: None»** para los tres paquetes. Es decir:
  la base de datos de GitHub **no registra una versión corregida**. Lo que nos pone
  a salvo no es un arreglo declarado, sino **quedar fuera del rango afectado**.
- **Y ahí está la medición que lo cierra**, leída del archivo de bloqueo del
  proyecto, sin ejecutar nada — `csharp/Fichas/Fichas.Datos/obj/project.assets.json`
  y el `deps.json` compilado:

  ```
  SQLitePCLRaw.bundle_e_sqlite3  2.1.12
  SQLitePCLRaw.core              2.1.12
  SQLitePCLRaw.lib.e_sqlite3     2.1.12   ← 2.1.12 > 2.1.11: fuera del rango
  SQLitePCLRaw.provider.e_sqlite3 2.1.12
  ```

  `Microsoft.Data.Sqlite` **10.0.11** arrastra la `2.1.12`. **Ése es el mecanismo**
  por el que `--vulnerable` no dice nada, y conviene tenerlo escrito: quien lea solo
  «actualiza a 10.0.11» no sabrá qué mirar el día que el aviso cambie.
- **La versión existe y es la última estable.**
  <https://api.nuget.org/v3-flatcontainer/microsoft.data.sqlite/index.json>,
  consultado el **2026-09-04**: las últimas del listado son
  `"11.0.0-preview.7.26381.103"` … `"11.0.0-preview.1.26104.118"`, `"10.0.11"`,
  `"10.0.10"`, `"10.0.9"`. La `10.0.11` es **la última no-preview**. La `10.0.0`
  también está publicada.
- **Ya está aplicada en un terreno, no en los seis.** Medido con `grep` sobre el
  árbol: `csharp/Fichas/Fichas.Datos/Fichas.Datos.csproj` línea 21 dice
  `<PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.11" />`, y
  `csharp/Fichas/Directory.Build.props` línea 28 dice
  `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.

**Lo que se pide, y es una línea:** cualquier proyecto que añada
`Microsoft.Data.Sqlite` lo hace en la **`10.0.11`**. Con `TreatWarningsAsErrors`
puesto en `Directory.Build.props` para toda la solución, una versión anterior **no
compila**, así que esto se cae solo si alguien lo intenta — pero es mejor que no
pierda una tarde averiguando por qué.

⚠️ **Y una recomendación que nadie pidió:** `Directory.Build.props` es el sitio
natural de una **`<PackageReference>` centralizada** (o un `Directory.Packages.props`
con gestión central de versiones), para que la versión se escriba **una vez** y no
seis. **No lo propongo como tarea todavía**: no he comprobado si la solución ya usa
gestión central de paquetes, y proponer refactorizar la construcción con seis
programadores dentro es la peor hora para hacerlo. *Lo decide: el dueño, cuando los
seis terrenos hayan cerrado.*

## ~~DC-17.~~ `contactos` sigue PROVISIONAL y sin repositorio — ~~y mi decisión es que **se queda fuera** del programa nuevo~~

> ⛔ **SUPERADA el 2026-09-05 por el planificador, y la superó el dueño el mismo
> 2026-09-04 en que yo la escribí.** Mi decisión era «se queda fuera». La suya fue
> la contraria: *«No debe haber teléfono ni teléfono, solo el canal que el agente
> se comunicó y ya»*, y el supervisor la recogió en `DECISIONES.md` («Cuatro
> respuestas del dueño») diciéndolo con estas palabras: *«Esto cierra la DC-17 del
> planificador: **entra**, como registro de canal, y **sin agenda**»*.
> **`contactos` ENTRA en el programa nuevo**, como registro del canal por el que
> habló el compañero, sin ningún dato de contacto de nadie. Lo que sigue abajo se
> conserva porque su medición del estado del código era correcta y porque la
> pregunta del final —*¿usa Miguel hoy esta función?*— **sigue sin contestar**.
> Lo que ya NO vale es mi conclusión.
> **Y lo que la sustituye no es una fase de este bloque:** `contactos` necesita su
> propia especificación (qué canales, dónde se pintan, quién los escribe), y no la
> he escrito. *Está en la lista de §Lo que solo el dueño puede dar.*

*Devuelta por el programador del terreno Datos el 2026-09-04: escribió repositorio
para las demás tablas y para `contactos` no, porque `docs/ARQUITECTURA.md` §2.6 la
marca **⚠️ PROVISIONAL**. Hizo bien en preguntar en vez de inventar.*

**El estado, medido:**

- `docs/ARQUITECTURA.md` §2.6 la marca PROVISIONAL desde el 2026-09-02, con el
  motivo escrito: *«`CLAUDE.md` y `DECISIONES.md` no contienen ni una sola decisión
  sobre esta fase: no dicen qué es un “contacto con el líder”, qué campos lleva,
  quién es el líder respecto de un caso»*.
- La FASE 7 del programa Python exige, en su criterio 4 y **antes de cerrar**, una
  entrada del dueño en `DECISIONES.md` que lo diga. **Esa entrada sigue sin
  existir**, medido el 2026-09-04 con `grep` sobre `DECISIONES.md`.
- **Ninguna fase C la cubre.** Medido leyendo la lista entera de fases del C#: van
  de la C0 a la C9 —inicio, base, OCR, casillas, extracción, corrección, asignar,
  Excel, calendario, reportes, paquete— y **ni una nombra los contactos con el
  líder**. La FASE 7 del Python **no tiene equivalente en C#**.
- En la base viva del dueño, `contactos` tiene **0 filas** (medido en solo lectura
  el 2026-09-04). No hay nada que portar.

**Mi decisión, y digo cuál de las dos cosas es:**

1. **La tabla se queda en el esquema.** Sus 12 columnas siguen contando dentro de
   las 104 y el DDL del C# la crea igual. **No es negociable**: el criterio C2-1
   exige 0 diferencias contra el esquema, y quitarla convertiría cualquier base real
   en una base «con una tabla de más» el día que se compare.
2. **Y se queda SIN repositorio, sin pantalla y fuera del programa nuevo**, hasta
   que exista la entrada del dueño. **No sale de PROVISIONAL.** El programador de
   Datos hizo lo correcto y no hay nada que arreglar en su entrega.

**El motivo, y es el mismo que ya está escrito en §2.6:** un repositorio sobre
`contactos` habría que escribirlo contra columnas que salieron de **una sola frase**
—«cuándo, por qué medio, con quién y qué resultó»— y no de una decisión. Construir
encima de eso es **asumir retrabajo conocido**, y con seis terrenos abiertos el
retrabajo se multiplica por seis.

**Lo que esto cuesta, dicho:** el programa nuevo saldrá **sin la función de
contactos con el líder**, que el Python tampoco tiene terminada. Si el dueño la
quiere en la sustitución, **hay que decírselo ahora** y no el día del cambio.

⚠️ **Y la pregunta que nadie ha hecho, que es la que de verdad decide esto:**
*¿usa Miguel hoy esta función, en el programa viejo o en papel?* Si la usa, esto
deja de ser deuda menor y se convierte en una fase C que no existe. Si no la usa,
la tabla puede seguir dormida años sin coste. **No lo sé, y no lo puedo medir desde
aquí.** *Lo contesta: el dueño.*

---

# Lo que solo el dueño puede dar

*Al 2026-09-04. Ninguna de estas cinco la puede cerrar ningún agente, y cuatro
llevan abiertas desde el 2026-09-03.*

1. **El PDF `PAPH2608`** — el que leyó 98 líneas y no encontró ninguna etiqueta,
   y con el que los cinco documentos suyos salieron con los campos vacíos.
   **Remedido hoy: no está en el disco**; `pdfs_referencia/` tiene otros cinco
   archivos y ninguno es ése. Sin él, la causa de ese fallo concreto sigue sin
   poder reproducirse aquí.
2. **Un PDF «sin limpiar»**, tal como sale del escáner. Todo lo que se ha medido
   viene de documentos ya tratados, y `DECISIONES.md` (2026-09-03) dice que sus
   PDF *no son escaneos*. La FASE 2 entera está escrita suponiendo un escaneo.
3. **Las listas de países y templos, con su color** — es **P-10** de
   `docs/ARQUITECTURA.md`. Bloquea que el color se vea, bloquea el catálogo de
   templos, y por ese camino bloquea **DC-6** (el `Caracas-Venezueia` que no se
   puede corregir). ⛔ No se escriben aquí: rellenarlas a ojo decide de qué color
   se pinta la nacionalidad de una persona.
4. **Su cifra de arranque, medida en su portátil.** Todo lo que hay son números
   de esta máquina. `DECISIONES.md` (2026-09-03): su carpeta `Documents\Fichas`
   **no tiene `fichas.log`**, así que el `.exe` con registro de tiempos no ha
   arrancado aún contra su base. **«Lento» no es un número hasta que él abra el
   programa una vez.** Es lo más barato de esta lista y lo que más desbloquea:
   sin ella, cada pase de rendimiento se mide contra un criterio que nadie sabe
   si es el suyo.
5. **Los residuos con datos reales, para borrarlos.** Medido hoy con
   `ls -d /c/Users/josem/Fichas-entrega/*`: **22 carpetas**, de las cuales
   **15 paquetes `Fichas*`** (uno de ellos el bueno) y **7 `respaldo-base-*`**
   con la base real de Miguel dentro. ⚠️ **Son copias de datos de personas
   reales fuera del repositorio y fuera de toda protección del `.gitignore`.**
   Ninguna se borra sin que él lo diga —una es la única red que hubo cuando su
   base migró sola a la versión 13 y ningún agente reconoció haberlo hecho— pero
   **veintidós no hacen falta**. *Lo decide: el dueño, diciendo cuáles se
   quedan.*

---

# Qué NO cubre este documento

- **No contiene el esquema de datos.** Recomienda dónde debe vivir y enumera qué
  debe contener a grandes rasgos; no lo redacta. No lo tengo y no se inventa.
- **No contiene el plan del ciclo de trabajo** (quién lanza qué pase y cuándo).
  Eso vive en `EN-CURSO.md` y es del supervisor.
- **No fija versiones de las bibliotecas.** Las congela el programador en la FASE
  0, medidas en la máquina donde se instalen.
- **No fija el orden de las fases 5, 6 y 7 entre sí.** Las tres dependen de la 1 y
  ninguna depende de las otras dos; el orden lo puede alterar el dueño según qué
  duela más primero.
- **Los criterios de la FASE 7 son provisionales**, y está dicho en su sitio: esa
  fase no tiene ninguna decisión escrita todavía.
- **Los criterios de la FASE 2 no se pueden ejecutar hoy**, porque dependen de un
  PDF que no está. Están redactados para que el día que llegue se ejecuten sin
  reescribirlos.

---

## Qué NO cubre la ronda de correcciones del 2026-09-02

*Añadido tras los once hallazgos de QA. Lo que la corrección deja fuera, dicho por
mí antes de que lo encuentre otro.*

- **Ningún criterio de este documento se ha ejecutado. Ninguno.** No hay `.py`, ni
  PDF, ni esquema, ni entorno. He arreglado **criterios escritos**, y un criterio
  escrito que nadie ha corrido sigue pudiendo estar roto — es exactamente lo que
  pasó con los dos greps de la primera versión. **Los controles positivos que ahora
  exijo no los he probado tampoco**: describo qué mutante tiene que cazar cada
  comprobación y con qué denominador, pero **quién elige el comando concreto y
  demuestra que caza los 9 de 9, los 8 de 8 y los 5 de 5 es el programador al
  construir y QA al auditar.** Ese es el punto de la regla: mi trabajo era quitar
  el «0 coincidencias» que se aprobaba solo, no fabricar el detector.
- **Los corpus de mutantes no existen todavía.** Los enumero; no los escribo, porque
  no escribo código. QA fabricó dos en su scratchpad para probar los greps rotos;
  los otros hay que hacerlos.
- **No he verificado que `[Environment]::GetFolderPath('MyDocuments')` sea un camino
  de comprobación **independiente** de `SHGetKnownFolderPath`.** Sospecho que
  ambos acaban leyendo la misma fuente del sistema, y si es así el criterio 1b de la
  FASE 1 no es una segunda opinión de verdad, sino la misma leída dos veces. Sigue
  cazando el fallo que importa —componer la ruta por nombre da otra cosa— pero **no
  lo presento como dos caminos independientes**. Falta comprobarlo en la
  documentación de la API antes de la FASE 1.
- **No he tocado el orden de las fases ni su número.** Siguen siendo diez, de la 0 a
  la 9. QA declaró expresamente que no juzgó si diez son las que hacen falta; yo
  tampoco lo he replanteado en esta ronda, que era de correcciones.
- **La FASE 7 sigue siendo la más floja**, y su corrección aquí ha sido cosmética
  —el `DELETE FROM`—. Lo de fondo no lo arregla una ronda de fixes: **no hay ni una
  decisión escrita sobre qué es un contacto con el líder**. Necesita el pase de
  especificación de D-1, y hasta entonces sus criterios son un punto de partida.
- **Los formatos que he definido son decisiones que arrastro yo, no del dueño.**
  Que el paquete de la FASE 6 y el reporte de la FASE 8 sean `.xlsx` sale de que
  `CLAUDE.md` §3 no ofrece otra cosa sin engordar el ejecutable. **Están abiertos a
  que el dueño diga otra cosa**, y he dejado escrito en cada sitio qué costaría.
- **`prueba_ocr` como nombre del esqueleto lo he elegido yo.** Hacía falta ligar
  `<nombre>` para que los criterios se pudieran comprobar literalmente; el nombre
  concreto es reemplazable y no cambia ningún criterio.
- **Ningún número de rendimiento se ha medido.** Sigue en pie D-6: nadie ha dicho
  cuántos formularios procesa Miguel ni cuánto puede esperar. Los criterios piden
  **anotar** tiempos, que es lo único honesto sin ese número, pero eso significa que
  **hoy ningún tiempo puede suspender una fase.**
- **De las cuatro afirmaciones con fuente web, reverifiqué las cuatro** (QA no
  pudo: su ficha le deniega `WebFetch`). Tres siguen en pie palabra por palabra;
  **la cuarta era falsa** —«48 entradas en `releases`» cuando son **31**— y está
  corregida arriba con sus dos caminos. **No reverifiqué el descargo sobre
  `FORMAT_TEXT` de `openpyxl`**: el criterio 2 de la FASE 4 sigue comprobando el
  efecto (`number_format` devuelve `@`) y no el nombre de la constante, que es lo
  correcto lo diga la documentación o no.
- **No he medido el estado del repositorio** para escribir nada de esto: no me
  corresponde, es del supervisor. Lo único que ejecuté fueron consultas a PyPI, que
  es la fuente externa que tenía que reverificar.

---

## Qué NO cubre la puesta al día del 2026-09-04

*Lo que esta ronda deja fuera, dicho antes de que lo encuentre otro.*

- **⚠️ Los criterios de aceptación de las diez fases NO se han cotejado contra el
  código de hoy, y es el hueco más grande de esta entrega.** El encargo era poner
  al día la deuda, y eso está hecho. Pero el ciclo 5 cambió reglas de producto que
  siguen escritas aquí como criterio, y **tres al menos están hoy en contradicción
  directa con lo decidido y construido**:
  - **FASE 1 crit. 2** exige que `.tables` devuelva «exactamente la lista de tablas
    del esquema aprobado». El esquema aprobado de entonces eran siete tablas; hoy
    son nueve.
  - **FASE 2 crit. 1, 3, 4 y 5** nombran `CASP2609_Zutano_Family.pdf` y sus
    coordenadas literales. Ese archivo **sigue sin existir** y esos criterios
    nunca se comprobaron contra él.
  - **La regla permanente 5**, citada en las fases 1 y 3 como «nada se marca
    solo», la **precisó el dueño el 2026-09-03**: el estado de la recomendación lo
    escribe el Excel del compañero con su nombre. `CLAUDE.md` está al día; los
    criterios de este documento, no.

  **No los he reescrito porque reescribir un criterio de aceptación sobre código ya
  entregado es cambiar la vara después del examen**, y eso lo decide el dueño, no
  yo. Lo que propongo, sin hacerlo: **un pase de cotejo criterio a criterio, con
  denominador** —cuántos criterios hay, cuántos siguen valiendo, cuántos
  contradicen lo construido— antes de que alguien vuelva a usar este documento
  como vara de medir. *Lo decide: el dueño.*
- **No he verificado ninguna de las cifras de rendimiento del ciclo 5.** Los
  82 widgets, los 0,190 s, los 0,218 s, los 0,597 s, los 26,8 → 1,4 ms del visor y
  los 5 ms por widget **son de los informes de sus programadores y del supervisor,
  y van citados como tales**. No he corrido ninguna medición de interfaz: hay una
  suite corriendo en la máquina y el pase me lo prohíbe. **Medir rendimiento no es
  mío de todos modos.**
- **No he ejecutado la suite ni auditado nada.** Eso es de QA. Lo único que ejecuté
  fue **construir una base temporal con `aplicar_esquema`** y preguntarle al motor,
  más búsquedas de texto y `wc -l`. **Medir no es probar.**
- **No he comprobado que Tk impida cambiar el padre de un widget** (DC-2). Es la
  explicación que dio el programador del pase 4 y va **como cita suya**. No abrí la
  documentación de Tk: el encargo no lo pedía y no cambia lo que hay que hacer.
- **No he consultado ninguna fuente externa en esta ronda.** Todo lo que afirmo
  sale del repositorio, del motor SQLite de esta máquina o de una cita de
  `DECISIONES.md` / `EN-CURSO.md` dicha como cita. **Las cuatro consultas web del
  encabezado de este documento son del 2026-09-02 y NO las he vuelto a abrir**, así
  que su fecha de consulta sigue siendo aquélla.
- **No he buscado en vídeo, y sigo sin poder.** Solo leo páginas.
- **No propongo nada sobre `docs/RUNBOOK.md`, `docs/INFORMES-QA.md` ni
  `docs/BITACORA-SESIONES.md` más allá de decir que no existen** (D-8). Son del
  programador, de QA y del supervisor.
- **No he tocado el ROADMAP ni el orden de los pases.** No es mío.

### Qué NO cubre la segunda vuelta del 2026-09-04 (la versión 15 y DC-15 a DC-17)

- **No he ejecutado `dotnet` de ninguna forma** —ni `build`, ni `restore`, ni
  `list package --vulnerable`—: hay seis programadores compilando en worktrees y el
  pase me lo prohíbe. Todo lo de DC-16 que no sea el aviso de GitHub, la API de
  NuGet o un `grep` sobre archivos del árbol **es cita de otro y va dicho como
  cita**.
- **No he corrido la suite de Python.** Lo único que ejecuté fue construir una base
  temporal en el scratchpad con `abrir_conexion` + `aplicar_esquema` y preguntarle
  al motor, más `grep` y lecturas en `mode=ro`. **Medir no es probar.**
- **No he probado el `CHECK` nuevo del MRN con inserciones.** Está transcrito del
  `sqlite_master`; el criterio C2-1bis que escribí **es trabajo para QA, no algo
  que yo haya verificado**.
- **No he revisado si el código Python valida el MRN con la regla vieja** fuera del
  esquema (`datos/validacion.py`, `extraccion/`, la pantalla de corrección). Si
  alguno la conserva, el dato pasa el motor y lo tira la capa de arriba: **el mismo
  daño con otro culpable**. *Lo mide: QA.*
- **No he tocado `DECISIONES.md`**, que sigue diciendo «11 dígitos» en su tabla de
  reglas de formato (línea 2283). **Es del supervisor.** Queda dicho en el criterio
  4 de la FASE 1 y en la FASE C2, que sí son míos.
- **No he abierto ningún dato personal.** De las nueve bases reales leí **solo**
  `MAX(version)` y `COUNT(*)`, en `mode=ro`. Ni un nombre, ni un MRN.
- **No he reescrito los criterios de las diez fases del Python** contra lo que el
  código hace hoy. Sigue siendo la deuda que este documento ya declaraba arriba;
  **solo toqué el criterio 4 de la FASE 1**, que era falso de forma medible.
- **No sé si Miguel usa hoy la función de contactos con el líder**, y esa es la
  pregunta que decide DC-17. No la puedo medir desde aquí. *La contesta: el dueño.*
- **No he comprobado si la solución de C# usa gestión central de paquetes.** Por eso
  la centralización de la versión en DC-16 va como observación y **no como tarea**.

---

# Fases del programa en C#

**Escrito el 2026-09-04 por el planificador**, sobre la decisión del dueño de ese
día (`DECISIONES.md`, «El dueño decide: el programa completo pasa a C# con WinUI
3») y sus siete requisitos («Requisitos del dueño para el programa nuevo»). La
investigación con fuentes que sostiene cada elección está en
**`docs/adr/ADR-0003-csharp-winui3.md`**; aquí solo van las fases y sus criterios.

**Qué significa la «C» del nombre.** Las diez fases de arriba (FASE 0 a FASE 9)
son las del programa en **Python** y siguen valiendo como historia de lo hecho.
Éstas son las del programa en **C#** y llevan **C** delante para que nadie las
confunda: **FASE C0 es la del C#, FASE 0 es la de Python.** No se renumera nada de
lo anterior.

## Las cinco reglas que gobiernan todas estas fases

1. **Cada fase termina en commit** (`CLAUDE.md` §1.6) **y en una carpeta que el
   dueño pueda abrir con doble clic.** Una fase que no produce algo abrible no se
   cierra.
2. **El `.exe` de Python no se toca.** `C:\Users\josem\Fichas-entrega\Fichas` sigue
   siendo lo que usa el dueño hasta que él diga lo contrario. El C# se entrega en
   carpeta **aparte**.
3. **Sobre una copia de la base, no sobre la real**, hasta que el dueño decida lo
   contrario (ADR-0003 §10). ⛔ *Lo decide: el dueño.*
4. **Ninguna cifra de rendimiento vale si se toma con 2 casos.** El criterio es con
   **3 000 documentos** y con la máquina en silencio. Precedente medido: con 12
   casos ya eran 131 widgets contra un techo de 120 (`DECISIONES.md`, cierre del
   pase 4).
5. **Las 1 159 pruebas de Python son la especificación de las reglas.** Una regla de
   negocio que se porte sin su prueba portada **no está portada**.

## Lo que hay que resolver antes de la FASE C0 — no es trabajo, es un permiso

⛔ **Instalar el .NET SDK 10.** Una sola orden, y **la autoriza el dueño**
(`DECISIONES.md`: «Instalar el SDK es una descarga: la autoriza el dueño»):

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact
```

Medido el 2026-09-04 con `winget search` en esta máquina (winget v1.29.290): el
paquete existe y está en la versión **10.0.400**. **No se instala nada más:** ni
Visual Studio, ni Windows SDK suelto. WinUI 3 se compila desde la línea de órdenes
—documentado por Microsoft, ADR-0003 §7.1— y el Windows SDK llega por NuGet dentro
de la plantilla.

⛔ **Activar el Modo de desarrollador** en Ajustes → Sistema → Para programadores.
Medido: hoy **no está activado** en esta máquina. **NO he comprobado si activarlo
pide administrador.**

---

## FASE C0 — La espiga medida

**Nada se porta hasta que esta fase dé sus números.** Existe porque hay cuatro
cosas que el plan da por buenas y **ninguna está medida**: el tamaño, el arranque,
si el archivo único funciona, y si el OCR nativo lee los papeles del dueño.

### Qué se hace

Un solo proyecto de juguete, tirable, con cuatro cosas dentro: una ventana WinUI 3
con una lista de **3 000 filas** dentro de un `ScrollView`; el OCR de Windows
leyendo **uno de los PDF reales del dueño**; un cronómetro que mide por pantalla; y
la publicación de las dos formas (carpeta y archivo único).

### Qué NO se hace en esta fase

No hay base de datos, ni esquema, ni Excel, ni reportes, ni diseño. **Nada de esto
sobrevive a la fase**: se mide, se anota y se tira.

### Criterio de aceptación — ocho números, cada uno con su comando

| # | Qué se mide | Criterio |
|---|---|---|
| C0-1 | `dotnet --list-sdks` | Sale una línea **10.x**. Sin eso no empieza nada |
| C0-2 | La lista de **3 000 filas** baja con rueda, con trackpad y con barra visible | Las tres. Es el requisito 1 del dueño y se comprueba a mano, con el dueño o con captura |
| C0-3 | Tiempo de pintar esa lista de 3 000, y de un clic en una fila | **≤ 0,2 s** cada uno, máquina en silencio |
| C0-4 | Cuántos elementos vivos hay con 3 000 filas | **No crece con los datos.** Si con 3 000 filas hay del orden de los que caben en pantalla, la virtualización funciona; si hay 3 000, no |
| C0-5 | Tamaño de `dotnet publish` **en carpeta**, autocontenido, y número de archivos | Se anota. La referencia es **216,9 MiB / 124 archivos** del Python. **No hay umbral: no existe cifra oficial y no la invento** |
| C0-6 | ¿Funciona `PublishSingleFile`? | Sí o no, con la salida del compilador. **Dos páginas de Microsoft se contradicen** (ADR-0003 §2.1). Si funciona: su tamaño y su arranque |
| C0-7 | Arranque hasta ventana lista, en frío y en caliente, ×2 cada uno | Se anota contra los **2,19 / 2,42 s** del `.exe` de Python (`DECISIONES.md`, cierre del pase 4). **No prometo que sea menor.** Si el archivo único tarda más de 5 s, se descarta: ya pasó con `--onefile` en Python (15-30 s) |
| C0-8 | ~~`Windows.Media.Ocr` sobre **los dos PDF reales del dueño unidos**~~ | ~~**1 caso · 4 personas · fecha `2026-08-25`**… Y sobre el grupo de seis hojas: **12 personas**~~ **Tachado el 2026-09-04 (ADR-0004 §0.1).** Se midió y dio **0 de 5 cédulas**, pero **el material no valía**: esos PDF tienen capa de texto y los escaneos reales del dueño no. La medición **no ordena motores** y no aprueba ni suspende nada. El criterio de lectura pasa a la C3 (§C3-L1..C3-L8) sobre los siete `CASP2609` |
| C0-9 | **Sonda de riesgo: ¿PdfPig da el color y el grosor de un `/Ink`?** Veinte líneas sobre `CASP2609_Zutano_Family.pdf`, sin construir nada | **Sí o no, por escrito.** Se busca el rojo `(0.890, 0.094, 0.176)` grosor 1.65 y el verde `(0.494, 0.765, 0)` grosor 16.5. **Añadido el 2026-09-04**: el trabajo de anotaciones bajó a la C3, y esta sonda se queda arriba para que un «no» se sepa ahora y no cuatro fases después |

**Y una medición que solo puede hacer el dueño, en la PC del trabajo**, porque es
la única pregunta de todo el plan que esta máquina no puede responder:

```powershell
$null = [Windows.Media.Ocr.OcrEngine,Windows.Foundation,ContentType=WindowsRuntime]
[Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages | Select LanguageTag, DisplayName
```

En **esta** máquina salen dos (`en-US`, `es-MX`) sin instalar nada, medido el
2026-09-04. **Si en la suya no sale ninguno en español, el OCR nativo no sirve y la
FASE C0 se cierra con «vía B»** — que cuesta una fase entera más (ADR-0003 §3.2).

⚠️ **Puesta al día del 2026-09-04 (ADR-0004 §3):** esa frase daba por bueno que la
vía B costaba «una fase entera más» porque había que reimplementar DB, CTC y cls y
porque faltaba el diccionario latino. **Las dos cosas eran ciertas al escribirlas y
las dos son falsas hoy**, medido: el diccionario va **embebido en el `.onnx`**
(502 caracteres, con `ñ` y acentos) y existe **`RapidOcrNet` 4.1.0**, Apache-2.0,
que trae esos tres algoritmos y **los mismos modelos v5 latinos**. Y el peso no es
147,83 MB: el `onnxruntime` de `win-x64` son **17,27 MiB medidos**. **La vía B ya
no es la cara.**

### Lo que esta fase decide

Al cerrarla se sabe, con número y no con opinión: si el OCR nativo sirve o hay que
portar PP-OCRv5; si se entrega en carpeta o en archivo único; y si WinUI 3 cumple
los 0,2 s con 3 000 filas. **Si C0-3 o C0-8 fallan, este plan cambia**, y eso es
mejor descubrirlo aquí que en la fase 6.

---

## FASE C1 — Inicio con la base real, que se abre y se baja

⚠️ **Esta fase la puso el dueño el 2026-09-04, y desplazó a la que estaba aquí.**
Sus palabras: *«comenzamos con el programa lo más rápido posible»*. Lo que había
en la C1 —rasterizar y leer anotaciones— **no se ha borrado: bajó a la C3**
(criterios C3-10 a C3-15), porque es infraestructura que el dueño no ve.

**El principio que esto cambia:** la primera cosa que se construye no es la más
peligrosa, sino **la primera que él puede abrir**.

### Qué se hace

La pantalla de inicio, leyendo **una copia de su base real**, y nada más. Sin
escribir, sin OCR, sin PDF, sin Excel. Un `.exe` que abre y enseña sus casos.

### Qué NO se hace en esta fase

No se escribe una sola fila. No hay migraciones —la base se abre **como esté**—,
ni corrección, ni asignación. Si algo no se puede leer, **se enseña vacío y se
sigue**, nunca se detiene (requisito 9).

### Criterio de aceptación

| # | Criterio |
|---|---|
| C1-1 | Abre **una copia** de la base real del dueño y pinta **sus** casos, con el denominador dicho: *N casos en la base, N en pantalla*. Si el número no cuadra, la fase no cierra |
| C1-2 | **Se baja con rueda, con trackpad y con barra visible**, en toda la pantalla. Requisito 1, y es la queja que originó el cambio de lenguaje |
| C1-3 | **A 1730×770 y a 1100×700 no se recorta nada sin forma de bajar.** Son las dos medidas con las que se diagnosticó el defecto de Tk |
| C1-4 | Lo que viaja en **7 días** sale arriba y en rojo; lo que ya pasó, marcado. Solo lectura |
| C1-5 | **Con 3 000 documentos, pintar inicio ≤ 0,2 s**, máquina en silencio |
| C1-6 | **Cero cuadros modales** en esta pantalla. Requisito 9 |
| C1-7 | **Se entrega en carpeta aparte y el dueño la abre con doble clic.** Si no la puede abrir, la fase no está hecha |

**Lo que esta fase le da al dueño:** por primera vez desde el cambio de lenguaje,
algo suyo en pantalla. Y a nosotros nos da la cifra que ninguna otra fase puede
dar: **si WinUI 3 cumple los 0,2 s con su base de verdad**, no con una inventada.

---

## FASE C2 — La base: el mismo esquema, ~~la misma versión 14~~ **la misma versión 15**

*Renumerada el 2026-09-04: la migración 15 entró ese mismo día
(`datos/migraciones_de_cedula.py`) y esta fase decía la 14.*

### Qué se hace

`Microsoft.Data.Sqlite` sobre el esquema que ya existe. **No se diseña nada:**
`docs/ARQUITECTURA.md` es la especificación y manda (`CLAUDE.md` §7).

**La versión del paquete es la `10.0.11`, y no es indiferente.** El motivo, con su
medición, está en «Deuda del ciclo 5 · DC-16». **Vale para los seis terrenos**, no
solo para Datos.

### Criterio de aceptación

| # | Criterio |
|---|---|
| C2-1 | Sobre una base nueva: **9 tablas, 104 columnas, 8 índices propios, ~~versión 14~~ versión 15, 48 `CHECK`**, preguntado **al motor** con `PRAGMA table_info` y `sqlite_master`, no leyendo el DDL. **0 diferencias** contra `docs/ARQUITECTURA.md` §2 |
| C2-1bis | **El `CHECK` de `mrn` es el de la versión 15**, con su control positivo: `055-1111-3853` entra, `055-1111-385A` **también entra**, `055-1111-385` se rechaza. Sin el segundo valor el criterio se pasa con un `CHECK` que rechace todo |
| C2-2 | Abre una **copia** de la base real del dueño y la lee sin migrar nada. ⚠️ ~~Antes hay que comprobar en qué versión está: `docs/ARQUITECTURA.md` deja escrito que **nadie ha comprobado que esté en la 14**~~ **Medido el 2026-09-04 en solo lectura: su base está en la 13**, con 2 casos, 0 personas, 8 filas de procedencia y 1 compañero. Le faltan **dos** migraciones, la 14 y la 15, no una. Lo que sigue sin comprobarse es que sus **columnas** sean estas 104: se contaron filas y versión, no `PRAGMA table_info` |
| C2-3 | `PRAGMA foreign_keys` devuelve **1 en cada conexión**, con prueba que intente violar una clave foránea y falle |
| C2-4 | **Cero interpolación de cadenas en SQL**, cazado con un `grep` — es el equivalente de `pruebas/auditoria_sql.py`, que ya existe |
| C2-5 | Fechas TEXT ISO-8601, booleanos INTEGER 0/1, `mrn` y `unidad_numero` TEXT: ida y vuelta de `055-1111-3853` sin perder el cero |
| C2-6 | Los `CHECK` del motor siguen rechazando marcar verificado sin quién y cuándo. **Regla permanente 5** |
| C2-7 | **Requisito 9 en la capa de datos.** Guardar un caso con `numero_caso` fuera de formato, o una persona con MRN corto, **no levanta ningún error**: el dato entra y queda marcado. Prueba con los tres valores de C4-10 llamando **directo al repositorio**, sin ventana de por medio |

⛔ **El requisito 9 choca con un `CHECK` del esquema, y no lo resuelvo yo.**
~~Medido hoy sobre `datos/esquema.py` (28 `CHECK` en total)~~ **Corregido el
2026-09-04: eran 48, no 28.** El 28 salía de `grep -c CHECK datos/esquema.py`, que
solo ve el DDL **congelado de la versión 1** y no las catorce migraciones
posteriores. El hallazgo lo devolvió el programador del terreno Datos y lo remedí
sobre una base construida con `aplicar_esquema`, contando por tabla en
`sqlite_master`:

```
asignaciones 2 · casos 9 · companeros 2 · contactos 3 · documentos_ilegibles 2
filas_descartadas 1 · personas 18 · procedencia_campo 11 · version_esquema 0
TOTAL: 48        CHECK que nombran numero_caso: 1
```

Éste bloquea en el motor un formato de campo:

```
CHECK (numero_caso GLOB '[A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9]')
```

El dueño dice *«un valor raro se guarda y se señala en su sitio»*, y este `CHECK`
lo impide **desde SQLite**, donde ningún aviso puede sustituirlo: la fila no entra.
Y hay una segunda pareja en la misma situación: las «Reglas de formato de los
campos» de `DECISIONES.md` exigen ~~MRN de 11 dígitos~~ un MRN de forma fija y que
el mes de `fecha_viaje` case con los últimos 4 dígitos del número de caso.
⚠️ **Lo del MRN cambió el 2026-09-04:** la migración 15 abrió el `CHECK` para que
el último carácter pueda ser una letra, pero **sigue siendo bloqueante** — un MRN
de otra forma no entra en la base. La regla vieja está tachada en el criterio 4 de
la FASE 1; `DECISIONES.md` (línea 2283) todavía dice «11 dígitos» y ese documento
es del supervisor.

Tres salidas, y **la elige el dueño**:

1. **Quitar ese `CHECK`** (los otros ~~27~~ **47** son de coherencia —`IN (0,1)`,
   fechas que acompañan a su bandera, la firma de la regla 5— y **ésos se quedan**;
   medido: 48 en total, uno de ellos el de `numero_caso`). Cuesta una
   migración y cambia `docs/ARQUITECTURA.md`.
2. **Dejarlo y aceptar una tercera excepción** al «avisar, nunca impedir».
3. **Guardar el valor crudo en otra columna** y dejar `numero_caso` nulo hasta que
   Miguel lo corrija.

**Recomiendo la 1**, y el motivo es del propio proyecto: el número de caso ya
**dejó de ser la identidad del caso** (migración 12 quitó su `UNIQUE`,
`DECISIONES.md` del 2026-09-03). Un formato bloqueante sobre un campo que ya no
identifica nada es justo la restricción que el dueño está pidiendo quitar. Además
el OCR ya lo lee mal a veces —medido: `CASP2609` leído como `CASD2609`—, y hoy ese
caso **no puede entrar**. *Lo decide: el dueño.* Hasta que lo diga, la FASE C2 no
puede cerrar C2-7.

---

## FASE C3a — La sonda del motor de OCR ⭐ *(nueva el 2026-09-04, ADR-0004 §8.2)*

**Ninguna línea de `extraccion/` se porta hasta que esta sonda dé sus números.**
Un proyecto tirable, como fue la C0. Existe porque la FASE C0 midió el OCR nativo
de Windows y **no llegó**, y porque la vía que el ADR-0003 daba por cara resultó
no serlo.

**Lo que el ADR-0004 recomienda y esta fase comprueba:** `RapidOcrNet` 4.1.0
(Apache-2.0, publicada 2026-08-30, 12,23 MB) sobre `Microsoft.ML.OnnxRuntime`,
con los mismos modelos PP-OCRv5 latinos que ya están en `modelos/`. 🔴 **La
decisión entre esto y dejar la lectura en Python es del dueño** (ADR-0004 §7.1).

| # | Qué se mide | Criterio |
|---|---|---|
| C3a-1 | El `ppocrv5_latin_dict.txt` de RapidOcrNet contra los **502 caracteres** del metadato `character` de `modelos/latin_PP-OCRv5_rec_mobile.onnx` | Iguales, o se dice en qué difieren |
| C3a-2 | Los tres `.onnx` de RapidOcrNet contra los de `modelos/`, por SHA-256 | Iguales o no, por escrito |
| C3a-3 | RapidOcrNet sobre **los siete escaneos `CASP2609`** | Los ocho criterios C3-L1..C3-L8, y **comparado contra el Python sobre los mismos siete archivos el mismo día** |
| C3a-4 | **Sin red:** ninguna conexión saliente durante una lectura completa | Cero. Regla permanente 2 |
| C3a-5 | Tamaño de `dotnet publish` con RapidOcrNet | Se anota. Lo identificado y medido son **30,4 MiB** (17,27 de onnxruntime + 13,10 de modelos); falta SkiaSharp y Clipper2 |
| C3a-6 | Segundos por hoja, frío y caliente | Contra **9,3–18,5 s** del Python sobre esos mismos siete |
| C3a-7 | **Vía A' (limpiar las rayas de la tabla):** la misma lectura con y sin limpieza | **Control positivo obligatorio:** hay que enseñar que la limpieza **no borra** texto bueno. Si empeora un solo campo, no entra |

⚠️ **Antes de C3a-7 hay que comprobar algo que nadie ha mirado:** si la **raya
vertical de la tabla que parte el número** aparece también en los siete escaneos
reales, o si era un rasgo de los dos PDF viejos. **De eso depende cuánto aporta la
vía A'**, y hoy no se sabe (ADR-0004 §9.4.11).

**Si C3a-3 sale peor que el Python, la salida es dejar la lectura en Python**
(ADR-0004 §5) y **la decisión vuelve al dueño con las dos cifras delante.**

---

## FASE C3b — Las casillas de ordenanza, leídas de la imagen ⭐ *(nueva el 2026-09-04, ADR-0004 §8.3)*

**No depende del motor de OCR: cuesta lo mismo con cualquiera.** Existe porque la
línea base real dio **0 de 7 casillas** (`DECISIONES.md`, `29cd00c`).

⚠️ **Y el diagnóstico correcto no es el que parece.** No fallan porque un escaneo
no tenga anotaciones: **la lectura está apagada a propósito**.
`extraccion/casillas.py` declara `LECTURA_DE_CASILLAS_ACTIVA = False` y
`UMBRAL_DE_PIXEL_OSCURO = None` porque hay **0 formularios con verdad conocida** y
hacen falta **3**. El método previsto —recortar, binarizar, contar el píxel oscuro,
comparar con un umbral— **ya era por imagen**. Lo que faltaba era con qué
calibrarlo, **y ahora hay siete documentos reales**.

| # | Qué | Criterio |
|---|---|---|
| C3b-0 | 🔴 **La verdad conocida de las 42 casillas** (7 documentos × 6), anotada por una persona | **Bloqueante: sin esto la fase no empieza.** *Lo da: el dueño* |
| C3b-1 | Localizar las seis casillas en la imagen | Por ancla, como las bandas. **Nunca por coordenada fija** |
| C3b-2 | Medir el píxel oscuro de las marcadas y de las vacías, y **escribir el umbral con ese número** | El umbral va en el código con **cuántos formularios lo calibraron** |
| C3b-3 | Lectura sobre los siete | **42 de 42**, contra la verdad de C3b-0, con los fallos nombrados uno a uno |
| C3b-4 | **Control negativo obligatorio** | Una casilla no leída devuelve **`None`, jamás `0`**. `0` significa «se leyó y no estaba marcada» y dejaría a Miguel seis ordenanzas negativas en firme sin motivo para mirar el papel |
| C3b-5 | Si no se alcanza C3b-3 | **La lectura NO se entrega activa:** las seis vuelven a captura manual, como hoy |

⚠️ **El umbral se calibra sobre la imagen que produce el rasterizador.** Si el C#
rasteriza con otro motor, **hay que recalibrarlo**.

---

## FASE C3 — Extracción: las reglas del papel

### Qué se hace

Portar `extraccion/` (2 122 líneas) e `importacion/` (1 434): anclas bilingües,
bandas, precedencia tachón → FreeText → OCR, geometría, normalización, identidad
por documento, duplicados.

### Criterio de aceptación

| # | Criterio |
|---|---|
| ~~C3-1~~ | ~~Los dos PDF reales unidos: **1 caso · 4 personas · `2026-08-25`** + 1 renglón `campo_discrepante`~~ **Sustituido el 2026-09-04 por C3-L1..C3-L8** (ADR-0004 §8.1) |
| ~~C3-2~~ | ~~El grupo real de seis hojas: **1 caso · 12 personas · 0 discrepancias**~~ **Sustituido el 2026-09-04.** Motivo, y no es que los documentos fueran viejos: **tienen capa de texto**, y los escaneos reales del dueño **no**. Un PDF con capa de texto se lee sin OCR; medir sobre él no dice nada del caso real |
| **C3-L1** | **Los siete escaneos `CASP2609`: 7 de 7 leídos**, 0 ilegibles, 0 rechazados |
| **C3-L2** | **7 de 7 personas con nombre** |
| **C3-L3** | **7 de 7 fechas de viaje** |
| **C3-L4** | **7 de 7 unidades**, número y nombre |
| **C3-L5** | **7 de 7 cédulas ENSEÑADAS**: guardadas **o señaladas en el campo**, y **jamás vacías en silencio**. ⚠️ Es el único criterio que exige **más que hoy** (hoy son 5 de 7) y **no se arregla con OCR**: las dos que faltan las tiró nuestra validación de once dígitos porque el papel trae una letra al final. Se arregla **enseñando `valor_ocr`** cuando la validación rechaza. Es el requisito 9 |
| **C3-L6** | **7 de 7 números de caso leídos**, y el que salga mal **se puede corregir a mano y unir**. Caso real: uno leyó `CASD2609` por `CASP2609` y entró como caso aparte. ⚠️ **El `CHECK` no lo habría cazado**: `CASD2609` tiene cuatro letras y cuatro dígitos y **pasa el `GLOB`** |
| **C3-L7** | Segundos por hoja: se anota contra **9,3–18,5 s** del Python. **Sin umbral**: el dueño no se ha quejado del tiempo de lectura |
| **C3-L8** | Casillas de ordenanza: ⛔ **no entra aquí**, va en la **FASE C3b** |
| C3-3 | La precedencia se prueba con el caso conocido: tachón rojo sobre `September 7, 2026` + FreeText `8 Sept 2026` en la misma banda → gana `8 Sept 2026` con `origen='anotacion'` y confianza 1.0 |
| C3-4 | Solapamiento del **50%** de la altura de la banda, ni proximidad ni «el más cercano» |
| C3-5 | Un formulario en español se lee con **rótulos españoles**; uno en inglés con los ingleses. **Cero rótulos ingleses en la interfaz** — regla 4 |
| C3-6 | Un archivo reprocesado **avisa y no rechaza**; un duplicado es un caso aparte con su marca, y **nada se fusiona solo** |
| C3-7 | Las **filas vacías** se descartan: no se guarda una persona en blanco |
| C3-8 | Más del 60% de campos bajo confianza 0.6 → `captura_manual` |
| C3-9 | Los **749 métodos de prueba sin interfaz** tienen su equivalente portado y en verde, con el denominador dicho: cuántos se portaron, cuántos no y por qué |
| C3-10 | *(era C1-1)* El rasterizado da **2705×3500 px** sobre el mismo PDF, o se explica con número por qué difiere. Tope de **3 500 px** en el lado largo — regla de no regresión |
| C3-11 | *(era C1-2)* Se leen las **22 anotaciones** de `CASP2609_Zutano_Family.pdf` (`DECISIONES.md`, «Hallazgo técnico que gobierna la FASE 2») |
| C3-12 | *(era C1-3)* De cada `/Ink` se obtienen **color RGB y `/BS /W`**: rojo `(0.890, 0.094, 0.176)` grosor 1.65 → tachón; verde `(0.494, 0.765, 0)` grosor 16.5 → resaltador |
| C3-13 | *(era C1-4)* Se lee el texto de un `/FreeText` y su `/Rect`. Caso que lo prueba: `8 Sept 2026` en `[138.0, 424.8, 188.3, 446.2]` |
| C3-14 | *(era C1-5)* La conversión de coordenadas tiene **prueba propia**: origen abajo-izquierda del PDF a arriba-izquierda de la imagen. Omitirla espeja los rectángulos y anula campos buenos |
| C3-15 | *(era C1-6)* Las casillas `/AS != /Off` se leen, o se declara por escrito que no se pueden leer así |

⚠️ **Estos seis bajaron aquí desde la antigua FASE C1 el 2026-09-04**, por la orden
del dueño de empezar por lo que él puede abrir. **Bajar el trabajo NO baja el
riesgo**, y hay que decirlo: C3-12 es el mayor riesgo del plan —ninguna fuente
dice que PdfPig exponga `/Ink` con su color y su grosor (ADR-0003 §4.2)— y si
falla en la C3, se descubre **cuatro fases más tarde** que en el orden anterior.
**Por eso su sonda se adelanta a la C0** (criterio C0-9): el trabajo baja, la
pregunta no. Si la sonda dice que no, hay tres salidas y **ninguna la elijo yo**:
leer el diccionario del PDF a mano, buscar otra biblioteca, o dejar esa pieza en
Python. *Lo decide: el dueño.*

⚠️ ~~**Las seis casillas de ordenanzas siguen sin calibrar**… Siguen haciendo falta
**tres formularios con la verdad conocida**, que no existen.~~ **Al día el
2026-09-04:** el trabajo se movió a la **FASE C3b**, y la parte que no existía ya
existe — **hay siete documentos reales del dueño**. Lo que sigue faltando es la
**verdad conocida de sus 42 casillas**, y eso lo anota una persona, no un
programa. *Lo da: el dueño.* El aviso del rasterizador sigue vigente.

---

## FASE C4 — La pantalla de corrección

~~**Aquí empieza lo que el dueño ve, y es donde estaba su queja.**~~ **Corregido el
2026-09-04:** lo que el dueño ve empieza en la **C1**, que ahora es la pantalla de
inicio. Esta fase se queda con **la corrección**: el papel a un lado, los campos al
otro, y la firma de Miguel. El inicio solo vuelve aquí para lo que en la C1 no
existía: escribir.

### Criterio de aceptación

| # | Criterio |
|---|---|
| C4-1 | **Toda pantalla dentro de un `ScrollView`.** Cazable con `grep`: cero `ScrollViewer` con `VerticalScrollBarVisibility="Disabled"`. Requisito 1 del dueño |
| C4-2 | Ninguna pantalla recorta contenido sin forma de bajar, a **1730×770 y a 1100×700** — las dos medidas con las que se diagnosticó el defecto de Tk |
| C4-3 | La lista de casos usa `ItemsView` o `ItemsRepeater` **dentro de** un `ScrollView`. Un `ItemsRepeater` suelto no baja: lo dice su documentación |
| C4-4 | Visor del PDF entero con `ZoomMode="Enabled"` y `ContentOrientation="None"`: entra ajustado, se acerca, se arrastra. Requisito «quiero poder ver el documento completo» |
| C4-5 | Cada aviso cabe en **una línea**, se cierra, y tiene «ver». Requisito 4. Se cuenta el texto en la ventana montada, como ya se hace hoy |
| C4-6 | **Con 3 000 documentos:** pintar inicio, abrir un caso y volver, **≤ 0,2 s** cada uno |
| C4-7 | Guardar **acusa recibo** y **un campo inválido no tumba lo válido**. Requisito 3 |
| C4-8 | **Nada se firma solo:** tras importar, todas las filas de procedencia con `verificado=0`. Regla permanente 5 |
| C4-9 | **Requisito 9 — ningún cuadro modal, salvo dos.** Contados en todo el proyecto de interfaz, los diálogos que detienen al usuario son **exactamente 2**: firmar como verificado y borrar. Cazable con `grep` sobre el XAML y el C#, con el denominador dicho. Cualquier tercero es un defecto |
| C4-10 | **Un valor con formato inesperado se guarda y se señala en su sitio.** Prueba con tres a la vez: MRN de 9 dígitos, unidad de 5 dígitos y fecha de viaje cuyo mes no casa con el número de caso. Los tres **quedan en la base**, los tres salen marcados en su campo, y **no se abre ningún cuadro ni se interrumpe nada** |
| C4-11 | **Guardar nunca es todo o nada.** El formulario de C4-10 guarda **los tres campos raros más todos los correctos**: cero campos perdidos por culpa de otro |
| C4-12 | **La señal va en el campo, no en un párrafo.** El aviso de un valor raro cabe en la línea del propio campo; no añade texto al cuerpo de la pantalla. Requisito 4 del dueño |

---

## FASE C5 — Asignar desde cualquier lugar

Va sola porque es un requisito del dueño que hoy está **roto** («todavía no está
conectado, se asigna en la pantalla Asignar casos») y porque es arquitectura, no
pantalla.

| # | Criterio |
|---|---|
| C5-1 | **Una sola operación** de asignar, usada desde la tarjeta, desde la corrección y desde la lista |
| C5-2 | Una prueba que la llame desde los tres sitios y compruebe que la fila de `asignaciones` sale **idéntica** |
| C5-3 | Retirar un caso **desactiva**, no borra: el historial de quién verificó qué no se pierde |
| C5-4 | **Requisito 8 — libre para asignar.** Base de prueba con **un caso en cada estado**: sin verificar, verificado, `completa`, `no_completa`, sin fecha de viaje, con fecha pasada y **archivado**. La pantalla ofrece **todos**. Cero casos ocultos por estado, con el denominador dicho: *N casos en la base, N ofrecidos* |
| C5-5 | **Sin compañero elegido, la lista de casos NO sale vacía.** Es el defecto medido hoy en `interfaz/asignacion.py::_casos_disponibles`, que solo sirve los pendientes de verificar y los que viajan pronto |
| C5-6 | **Lo único que sigue filtrando es el compañero desactivado.** Prueba: con 3 compañeros, desactivar uno → quedan 2 como destino y los casos siguen siendo todos. Es la única excepción que el dueño dejó en pie |

---

## FASE C6 — Excel de ida y vuelta con los compañeros

| # | Criterio |
|---|---|
| C6-1 | El Excel del agente reproduce las **18 propiedades estructurales** ya validadas: hoja, congelado en **A7**, 16 títulos y anchos, tinta `16233A`, fondo `FFF6DC` en las 7 columnas, clave en gris 8, 7 menús Sí/No, fecha límite en rojo `A62E24` |
| C6-2 | La cabecera lleva **templo y fecha de salida** |
| C6-3 | La vuelta reconcilia por **`numero_caso` + `mrn` + `id` del caso**, nunca por nombre. Una clave sin `id` entra por su propio motivo |
| C6-4 | Las filas sin par **no se insertan**: van a `filas_descartadas`, y **se ven** desde la interfaz |
| C6-5 | Al generar, **se avisa nombrando a quien no tiene MRN** — sin MRN, su trabajo se pierde en la vuelta |
| C6-6 | El estado `completa`/`no_completa` lo escribe **el Excel del compañero, con su nombre**; la firma de campos sigue siendo solo de Miguel. `CLAUDE.md` §1.5: **son dos cosas y no se mezclan** |
| C6-7 | Excel abierto y bloqueado: **dato guardado, aviso en español, `.xlsx` intacto, sin caída.** Nunca fallar callado |
| C6-8 | El espejo se regenera tras **cada** guardado. Sin botón de exportar |

---

## FASE C7 — Calendario, pendientes y lo que viaja pronto

| # | Criterio |
|---|---|
| C7-1 | Lo que viaja en **7 días** sale en inicio, arriba, en rojo. **No en una pestaña** |
| C7-2 | Los **archivados se ven en el calendario**, marcados «archivado» |
| C7-3 | Fecha ya pasada al subir: **pregunta** archivar o completar |
| C7-4 | Un caso **sin fecha** no desaparece: lo recoge la lista de pendientes |
| C7-5 | Con 3 000 documentos, el calendario se pinta en **≤ 0,2 s** |

---

## FASE C8 — Reportes para los jefes y el histórico

| # | Criterio |
|---|---|
| C8-1 | El informe en PDF tiene los **14 de 15 rasgos** ya validados contra el del viejo, más «Unidades con preparaciones sin completar» y «El equipo» |
| C8-2 | Dice **«con la preparación completa»**, no «verificada»: en el informe son los seis pasos, en la pantalla es la firma de Miguel |
| C8-3 | Archivar es `archivado = 1` con fecha. **Nunca se borra.** Los archivados salen de las listas de trabajo y **siguen contando** en los reportes |
| C8-4 | El motor de PDF: primero portar `reportes/formato.py` (dos operadores crudos, `re f` y `l S`); PDFsharp **solo si** el porte cuesta más, con la cifra que lo diga |

---

## FASE C9 — El paquete final, y la sustitución

| # | Criterio |
|---|---|
| C9-1 | Publicado autocontenido y sin paquete, con las propiedades del ADR-0003 §2.2. Tamaño y número de archivos anotados |
| C9-2 | **Se abre con doble clic en la PC del trabajo, sin instalar nada y sin administrador.** Esto **solo lo puede probar el dueño** — es la misma mitad de pregunta que la FASE 0 del Python dejó abierta desde el 2026-09-02 |
| C9-3 | Construido **fuera de OneDrive**. Precedente medido: OneDrive convirtió 60 carpetas de un `dist` en marcadores de la nube |
| C9-4 | `--carpeta-de-datos` existe, y **sin ruta detrás levanta**, no cae a la carpeta de siempre |
| C9-5 | **Regla permanente 1, con control positivo:** cero conexiones de red a nivel de socket, cero runtimes de modelo de lenguaje en el paquete |
| C9-6 | La carpeta del Python **sigue donde está**. La sustitución **la decide el dueño**, no esta fase |

---

## El orden de las fases cambió el 2026-09-04, y por qué

**Orden del dueño:** *«comenzamos con el programa lo más rápido posible»*. Antes,
la C1 era rasterizar PDF y leer anotaciones: infraestructura que él no ve. Ahora la
C1 es **la pantalla de inicio con su base real y desplazamiento**.

**Qué se movió, exactamente una cosa:**

| Antes | Ahora | Por qué |
|---|---|---|
| C1 — Leer un PDF: rasterizar y anotaciones | **C3-10 a C3-15**, dentro de Extracción | Es infraestructura invisible. Va donde se usa |
| *(no existía)* | **C1 — Inicio con la base real, que se abre y se baja** | Es lo primero que el dueño puede abrir |
| C0-1 a C0-8 | **C0-9 añadido** | La sonda del `/Ink` se queda arriba aunque su trabajo bajara |

**C2 y de la C4 a la C9 no se tocan**, y sus criterios conservan su etiqueta: lo
comprometido en los requisitos 8 y 9 (C2-7, C4-9 a C4-12, C5-4 a C5-6) sigue
diciendo lo mismo y en el mismo sitio.

⚠️ **Lo que este reordenamiento cuesta, dicho sin adornos.** El orden anterior
ponía el mayor riesgo el segundo a propósito. Ahora el trabajo de anotaciones
ocurre en la C3, **cuatro fases más tarde**, y si PdfPig no sirve se descubre con
mucho más construido encima. **La sonda C0-9 tapa la parte barata de ese agujero
—saber la respuesta— pero no la cara:** si la respuesta es «no», habrá que decidir
la alternativa con la C1 y la C2 ya hechas. Lo acepto porque lo mandó el dueño y
porque tener algo abrible tiene valor propio, pero **no digo que salga gratis**.

---

## Funciones tomadas de programas similares

**Encargo del dueño, 2026-09-04:** *«Planificador que busque programas similares y
agregue las funciones»*. Se miraron **siete productos reales**, todos consultados
el **2026-09-04**. Ninguna función de esta sección está inventada: cada una lleva
el programa donde se vio y su fuente.

**Los siete, y qué es cada uno:**

| Programa | Qué es | Fuente |
|---|---|---|
| **Paperless-ngx** | Gestor documental libre: escanea, indexa y archiva | `https://docs.paperless-ngx.com/usage/` |
| **Kanboard** | Tablero kanban libre con fechas límite y calendario | `https://docs.kanboard.org/v1/user/tasks/` · `https://docs.kanboard.org/v1/user/boards/` |
| **Redmine** | Gestor de expedientes/incidencias libre, con asignación | `https://www.redmine.org/projects/redmine/wiki/Features` |
| **Rossum** | Captura de datos de formularios, con pantalla de validación | `https://knowledge-base.rossum.ai/docs/guide-to-automation-process-in-rossum` |
| **Docsumo** | Captura de formularios con pantalla de revisión | `https://support.docsumo.com/docs/review-screen` · `https://support.docsumo.com/docs/confidence-score` |
| **ABBYY FlexiCapture 12** | Estación de verificación de datos reconocidos | `https://help.abbyy.com/en-us/flexicapture/12/standalone_operator/operator_verification/` |
| **Label Studio** | Herramienta de anotación, con atajos y deshacer | `https://labelstud.io/guide/hotkeys` |

---

### (a) Lo que el dueño ya pidió, y estos programas confirman

Que siete productos independientes hagan lo mismo no lo convierte en obligatorio,
pero **sí quita la duda de si es una manía suya**. No lo es.

| Función | Dónde se vio | Qué problema del dueño resuelve | Fase | Criterio de aceptación |
|---|---|---|---|---|
| **Documento a un lado, campos al otro** | Docsumo: *«Reviewers see extracted data side-by-side with the original document image»* | *«Quiero poder ver el documento completo»* | C4 | Ya es **C4-4** |
| **Confianza marcada, y el humano confirma** | Rossum: *«A grey tick means AI has validated the field, while a green tick means that a human validated/confirmed the field»* | Regla permanente 5: nada se firma solo | C4 | Ya es **C4-8**. El par gris/verde es **exactamente** la distinción del proyecto entre propuesto y firmado |
| **Lo dudoso se enseña primero** | Rossum: *«prompts the user to inspect empty fields and review data with low confidence scores»*; ABBYY: revisar *«uncertain characters»* antes que el resto | Que Miguel no lea 26 campos buenos para encontrar el malo | C4 | **Nuevo C4-13** (abajo) |
| **Fecha límite en rojo cuando se pasa** | Kanboard: *«Overdue tasks will have a red due date»* | Lo que viaja en 7 días, arriba y en rojo | C1 y C7 | Ya es **C1-4** y **C7-1** |
| **Calendario alimentado por la fecha** | Kanboard: *«The calendar shows tasks with a defined due date»*; Redmine: *«Automatic gantt and calendar based on issues start and due dates»* | El calendario del mes con el caso en su día | C7 | Ya es **C7-1** a **C7-5** |
| **Asignar a una persona desde el tablero** | Kanboard: *«change the color, the category, the assignee, the due date»* | Requisito 8, libre para asignar | C5 | Ya es **C5-1** a **C5-6** |
| **Edición en lote** | Paperless-ngx: *«bulk editing of tags, correspondents, types and more»* | «Tablero de completados y archivo en lote» | C7 | **Nuevo C7-6** (abajo) |

---

### (b) Lo que el dueño NO pidió y encaja con «libre, rápido, sin texto, sin restricciones»

**Ninguna de estas se construye porque yo lo diga.** Van con su fase y su criterio
para que él las acepte o las tire una por una. 🔴 *Lo decide: el dueño.*

| Función | Dónde se vio | Por qué encaja | Fase | Criterio de aceptación |
|---|---|---|---|---|
| **Búsqueda instantánea global** | Paperless-ngx: *«search bar that allows you to search for documents by title, ASN»*, con *«Auto completion»* y *«Results are sorted by relevance»* | Con 3 000 documentos, encontrar uno navegando es imposible. Es «libre» en estado puro | **C1** | Una caja arriba: se teclean 3 letras y salen los casos que casan por número, nombre o MRN, **≤ 0,2 s** con 3 000 documentos, sin pulsar Intro |
| **Atajos de teclado en todo** | Rossum: *«[Tab] and shift + [Tab] for navigating between the previous and next fields»*, *«[Enter] for going to the next field that requires validation»*; Label Studio: *«customize global hotkeys»* | El dueño corrige con las dos manos en el papel. Ya hay teclado en la corrección de hoy | **C4** | Tab y Mayús+Tab recorren campos; Intro salta **al siguiente campo dudoso**, no al siguiente a secas. Prueba: 10 campos, 3 dudosos → 3 pulsaciones de Intro |
| **Deshacer** | Label Studio: *«a navigation panel for the current task with buttons for undo, redo and reset»* | Es la otra cara del requisito 9: si nada te impide equivocarte, algo tiene que dejarte volver | **C4** | Ctrl+Z deshace la última corrección de campo, y lo deshecho **no queda firmado**. Al menos 10 pasos de historia |
| **Arrastrar y soltar para asignar** | Kanboard: *«drag and drop tasks between columns»* | «Debe poder asignarse desde cualquier lugar» llevado al gesto | **C5** | Arrastrar una tarjeta sobre un compañero crea la misma fila de `asignaciones` que el menú. **Misma prueba que C5-2**, con un tercer camino |
| **Filtros guardados** | Kanboard: *«Custom filters are stored by project»*; Paperless-ngx: *«Customizable views can be saved and displayed on the dashboard»*; Redmine: *«Custom fields can be displayed on the issue list and used as filters»* | Miguel repite las mismas preguntas cada semana | **C7** | Se guarda un filtro con nombre, sobrevive a cerrar el programa, y sale en inicio con su cuenta al lado |
| **Historial por caso** | Kanboard, «Task Transitions»: *«Date of the action, Source column, Destination column, Executor, Time spent in the origin column»* | Hoy `procedencia_campo` guarda quién firmó qué, pero **no hay pantalla que lo enseñe** | **C4** | Un panel por caso con quién tocó qué y cuándo, leído de `procedencia_campo` y `asignaciones`. **Cero columnas nuevas** |
| **Pinchar un campo y que se ilumine en el papel** | Docsumo: *«Click a field and its exact spot lights up on the page»* | El proyecto ya guarda `banda_x0/y0/x1/y1` por campo: **el dato ya está y nadie lo usa así** | **C4** | Pinchar un campo desplaza el visor a su banda y la resalta. Se prueba con el `/FreeText` conocido en `[138.0, 424.8, 188.3, 446.2]` |
| **Umbral de confianza configurable** | ABBYY: *«highlight characters if confidence level is less than a threshold value (with possible values of 40%, 60%, and 80%)»* | Hoy el 0,6 está clavado en el código y nadie lo ha calibrado | **C4** | El umbral vive en un ajuste, no en el código. Cambiarlo cambia cuántos campos salen marcados, con la cuenta antes y después |
| **Exportar cualquier lista a Excel** | ⚠️ **NO verificado.** Redmine y Kanboard lo tienen fama de tener; **no encontré la frase en las páginas que leí** | El espejo de Excel ya existe, pero es del programa entero, no de la lista que estás mirando | **C6** | *(sin criterio: no lo escribo sobre una fuente que no vi)* |
| **Recordatorios** | ⚠️ **NO encontré evidencia** en ninguno de los siete | — | — | *(no entra)* |
| **Calendario con arrastre de fechas** | ⚠️ **Media.** Existe en Kanboard —hay un fallo abierto que lo nombra, `github.com/kanboard/kanboard/issues/4160`, «Calendar drag and drop not always working»— pero **no lo vi documentado como función**, y que su propio proyecto lo tenga por inestable es un dato en contra | Cambiar la fecha de viaje arrastrando | **C7** | 🔴 **No lo recomiendo todavía.** Arrastrar una fecha de viaje es cambiar el dato que decide si alguien entra al templo. Con el requisito 9 («avisar, nunca impedir») un arrastre torpe cambia la fecha **sin que nada lo detenga**. *Lo decide: el dueño* |

---

### (c) Lo que se descarta, y por qué

**Todo lo de esta lista existe en los siete programas y aquí no entra.** No es
desdén: son las reglas permanentes de `CLAUDE.md`, y ninguna se negocia.

| Función descartada | Dónde se vio | Regla que lo prohíbe |
|---|---|---|
| **Extracción por modelo entrenado / «AI validated»** | Rossum, Docsumo | **Regla 1.** Un MRN o una fecha inventados causan daño real. Solo OCR determinista y reglas |
| **Clasificación automática del tipo de documento** | Docsumo, Paperless-ngx | **Regla 1.** Es adivinar qué papel es |
| **Sugerir el valor de un campo vacío** | Rossum (*«prompts… inspect empty fields»* está bien; rellenarlos, no) | **Regla 1.** Se avisa del hueco; **no se rellena** |
| **Servidor web, navegador y puertos** | Paperless-ngx, Kanboard, Redmine — los tres son aplicaciones web | **Regla 2.** Sin servidor web, sin puertos abiertos. Es la misma razón por la que el proyecto viejo se abandonó |
| **Cuenta de usuario, inicio de sesión, roles** | Redmine, Rossum, Docsumo | **Regla 2** y el uso real: **el programa es de una persona**. Los compañeros nunca lo tocan; su canal es el Excel |
| **Nube, API REST, `Atom feeds`, webhooks** | Redmine (*«available as Atom feeds»*), Rossum, Docsumo | **Regla 2.** Cero red. QA ya lo verificó con la red cortada a nivel de socket |
| **Entrada por correo / IMAP** | Paperless-ngx | **Regla 2.** Red |
| **Instalación (Docker, servidor, base externa)** | Paperless-ngx, Redmine | **Regla 2.** Doble clic, sin instalar |
| **Precio por documento / licencia por puesto** | Rossum, Docsumo, ABBYY | No es una regla escrita, pero **el programa es para el trabajo de iglesia de una persona**. Un coste por documento con 3 000 documentos no tiene sentido aquí |

⚠️ **Y lo que esto significa, dicho entero:** los tres programas que mejor resuelven
la lectura de formularios —Rossum, Docsumo, ABBYY— **están descartados como
producto**. De ellos se toma **la forma de la pantalla**, que es lo que se puede
copiar sin romper ninguna regla: documento a un lado, campos al otro, lo dudoso
marcado, el humano confirmando. **Su motor no se puede copiar y no se copia.**

---

### Los tres criterios nuevos que salen de esta sección

Van aquí para que se lean junto a las fases donde entran.

| # | Fase | Criterio |
|---|---|---|
| C4-13 | C4 | **Lo dudoso primero.** Al abrir un caso, los campos por debajo del umbral y los vacíos salen agrupados arriba, con su cuenta. Prueba: 18 campos, 4 dudosos → los 4 arriba y el rótulo dice «4» |
| C7-6 | C7 | **Archivo en lote.** Seleccionar N casos y archivarlos deja **N filas con `archivado = 1` y su fecha**, en una sola acción y sin cuadro modal salvo la pregunta de borrar —que aquí no aplica, porque archivar no borra |
| C1-8 | C1 | **Búsqueda instantánea.** Tres letras devuelven los casos que casan por número, nombre o MRN en **≤ 0,2 s** con 3 000 documentos, sin pulsar Intro |

---

### Qué NO cubre esta investigación

- **No miré ningún programa de gestión de recomendaciones al templo.** Busqué por
  función —expedientes, captura de formularios, tableros con fechas—, no por
  dominio. **Si existe uno específico de esto, no lo encontré y no lo busqué a
  fondo.**
- **No instalé ni ejecuté ninguno de los siete.** Todo sale de su documentación
  oficial. **No he visto ninguna de estas pantallas funcionando**, salvo las que ya
  se vieron del programa viejo de Miguel.
- **No comparé rendimiento con ninguno.** Ninguno publica cifras que sirvan aquí.
- **Tres funciones se quedaron sin fuente y van marcadas como tales**, no
  escondidas: exportar listas a Excel, recordatorios y el arrastre de fechas en el
  calendario.
- **No busqué en vídeo, y sigo sin poder.** Solo leo páginas. Varias de estas
  herramientas enseñan su pantalla en vídeos de demostración que **no puedo ver**;
  lo que hay aquí sale de texto.

## Deuda que este cambio NO borra

Cambiar de lenguaje no contesta ninguna pregunta abierta. Lo que sigue vivo:

- **Las preguntas del esquema P-2, P-3, P-4, P-6, P-7, P-8 y P-10 a P-16.** Todas
  del dueño, todas donde estaban.
- **DC-4** (un archivo copiado a otra ruta y sin MRN legible entra sin marca de
  duplicado), **DC-13** (una prueba no aplica una migración a mano) y **DC-14**
  (nadie decide si el historial de marcas del compañero se guarda). Son de datos, no
  de interfaz: **viajan enteras**.
- **La copia de seguridad de la base (D-5).** Sigue sin existir y sigue siendo *«el
  riesgo más grande que tiene el trabajo de Miguel»* (`ESTADO.md`). Con **dos**
  programas capaces de escribir en la misma base, es mayor que ayer.
- **La calibración de las seis casillas.** Faltan los tres formularios.
- **La FASE 7 del Python (contactos con el líder) nunca tuvo especificación.** Aquí
  no tiene fase: **no se puede especificar lo que nadie ha definido.** *Lo da: el
  dueño.* ⚠️ **Ampliado el 2026-09-04 en «DC-17»**, con lo que eso significa para
  la tabla `contactos`: se queda **en el esquema** y **fuera del programa**, y la
  pregunta que lo decide de verdad —si Miguel usa hoy esa función— sigue sin
  hacerse.
- ⚠️ **Deuda nueva con fecha:** el Windows App SDK **2.4.0** (canal Stable,
  publicado 2026-08-13) tiene **fin de servicio el 2027-04-29**. Subir de versión es
  cambiar un número, pero hay que acordarse. *Lo mide: quien empaquete.*

## Lo que NO cubren estas fases

- **No cubren el ROADMAP ni el orden de los pases.** Es del dueño.
- **No cubren el aspecto.** Es del diseñador, sobre los mockups que ya existen. La
  cara del programa **no cambia**: el dueño dijo que la del nuevo le gusta.
- **No cubren firma de código ni antivirus.** Un binario nuevo sin firmar, copiado a
  la PC de un trabajo, puede ser bloqueado por SmartScreen o por el antivirus de la
  empresa. **Hoy pasa lo mismo con el de Python y nadie lo ha comprobado.** No lo
  investigué: no me lo pidieron y no tengo esa máquina.
- **No cubren accesibilidad.** La documentación de `ItemsRepeater` avisa literal que
  *«does not provide a default accessibility experience»*. Nadie ha dicho si importa
  aquí.
- **No dan una cifra en días para nada.** No tengo ninguna medición de cuánto tarda
  este equipo por fase en C#, y este documento ya tiene el precedente de las cifras
  marcadas `⚠️plan` que vinieron de una suposición. **Lo que sí está medido está en
  el ADR-0003 §9:** 14 758 líneas de lógica a portar con 749 métodos de prueba que
  dicen si el porte está bien, y 11 445 líneas de interfaz a **rehacer**, para las
  que no hay prueba automática que diga si quedó bien.
- **No he compilado nada.** No hay SDK en esta máquina y no lo instalo: la descarga
  la autoriza el dueño. Todo lo que estas fases afirman sobre compilar sale de
  documentación oficial citada en el ADR-0003, **no de experiencia propia**.

---

# Fases C10 a C16 — El grupo que viaja, y la segunda vuelta con los gerentes

**Escritas el 2026-09-05 por el planificador**, sobre las dos peticiones del dueño
recogidas ese día en `DECISIONES.md` («Lo que el dueño pidió probando el programa»,
punto 6, y «El calendario deja de ser adorno»). **La investigación que las sostiene
—las opciones comparadas, lo medido y las cuatro decisiones que son suyas— está en
`docs/adr/ADR-0005-el-grupo-que-viaja-y-la-segunda-vuelta.md`.** Aquí van las fases
y sus criterios, nada más.

**Siguen numerando la serie C**, que es la del programa en C#. No renumeran ni
tocan de la C0 a la C9.

## Lo que cambia del modelo, en tres frases

1. **La unidad de trabajo deja de ser el documento y pasa a ser el grupo que viaja
   un día.** El grupo **no es una tabla**: es una consulta sobre `fecha_viaje`, con
   el índice `idx_casos_viaje_activos` que existe desde la versión 1 del esquema.
   Medido sobre 3 000 casos: el día tarda **1,03 ms** y el mes entero **5,41 ms**,
   sin barrido de tabla (ADR-0005 §2.3).
2. **«No completa» gana un segundo eje: por qué.** Los tres estados que pidió no
   son tres valores del mismo campo: uno es el estado y los otros dos son el motivo
   (ADR-0005 §3).
3. **Aparece un segundo destinatario, el gerente, y un tercer origen de marca, el
   administrador.** Ninguno de los dos necesita tabla propia: son roles de
   `companeros` (ADR-0005 §4).

## Qué se tira, qué se reordena, qué no se toca

- **Se tira: ningún archivo.** Se reescriben un método (`CalendarioDelMes.AgruparPorDia`,
  ~25 de sus 109 líneas) y un `record` de 5 campos (`PastillaDeDia`).
- **Se reordena: la FASE C7.** Deja de ser una pantalla de consulta y pasa a ser la
  entrada al trabajo. **Sus cinco criterios C7-1 a C7-5 siguen vigentes tal cual**, y
  el C7-5 (pintar el mes en ≤ 0,2 s con 3 000 documentos) hay que volver a
  demostrarlo con pastillas de grupo.
- **No se toca:** `Fichas.Lectura` entera, Importar, el interior de Corrección,
  `procedencia_campo` y la firma de campos, el PDF de los jefes, y la tabla
  `personas` — **ni una columna**.

## Coste — ⚠️ estimación, NO medición

No he ejecutado nada del C#: hay tres programadores a la vez en `csharp/Fichas` y
además no me corresponde. **6 a 7,5 días-programador de trabajo; con tres a la vez
y estas dependencias, 3 a 4 días de calendario.** Lo que se puede enseñar primero
—el calendario que se pulsa y abre el grupo— son **C10 + C11 + C12: día y medio a
dos días**, y no depende del resto.

---

## FASE C10 — La migración 18: el vocabulario del trabajo nuevo

Tres columnas y ninguna reconstrucción de tabla. **Bloquea a todas las demás.**

```sql
ALTER TABLE casos      ADD COLUMN motivo_no_completa   TEXT CHECK (... IS NULL OR ... IN ('no_se_pudo_comunicar','el_lider_no_lo_hizo','otra_razon'));
ALTER TABLE casos      ADD COLUMN motivo_del_companero TEXT CHECK (... IS NULL OR ... IN ('no_se_pudo_comunicar','el_lider_no_lo_hizo','otra_razon'));
ALTER TABLE companeros ADD COLUMN rol TEXT NOT NULL DEFAULT 'companero' CHECK (rol IN ('companero','gerente','administrador'));
```

| # | Criterio |
|---|---|
| C10-1 | La 18 se engancha **al final** de `CatalogoDeMigraciones.Todas` y `VersionAlDia` sube sola. **No se escribe ningún número a mano**: lo dice el propio comentario del catálogo |
| C10-2 | Son **tres `ADD COLUMN`, cero reconstrucciones.** Verificado contra la documentación oficial (`https://sqlite.org/lang_altertable.html`, consultada el 2026-09-05): las restricciones que lista no incluyen el `CHECK`. Y medido en SQLite 3.50.4: `ADD COLUMN con CHECK y DEFAULT: OK` |
| C10-3 | La base viva del dueño —**2 casos, 0 personas, 1 compañero (`Sandy`), versión 17**, medido el 2026-09-05— sube a la 18 **sin perder una fila**, y `Sandy` queda con `rol = 'companero'` |
| C10-4 | Sobre una base de **3 000 casos y 16 253 personas**, la migración entera tarda **≤ 500 ms** y `pragma integrity_check` devuelve `ok`. *(Referencia medida con el SQLite de Python: **49,27 ms**. El margen ×10 es a propósito: `Microsoft.Data.Sqlite` empaqueta otro motor y **no lo he medido**.)* |
| C10-5 | Un motivo o un rol fuera de su lista **lo rechaza el `CHECK`**, con prueba que lo demuestre. Medido en Python: `CHECK constraint failed: rol IN ('companero','gerente','administrador')` |
| C10-6 | **El conteo de columnas pasa de 104 a 107, y hay CINCO sitios que lo llevan escrito a mano.** Se corrigen los cinco **en el mismo commit** o la suite se pone roja: `Fichas.Pruebas.Datos/PruebaDeLaBaseReal.cs:128`, `PruebaDeLasColumnasUnaAUna.cs:99`, `PruebaDelEsquema.cs:96` (y su desglose de la línea 29, que pasa a `22+25+6+6+12+16+8+9+3`), y `docs/ARQUITECTURA.md` §2 |
| C10-7 | El respaldo previo (`RespaldoAntesDeMigrar`) sigue haciéndose. Precedente del 2026-09-04: el programa nuevo migró la base VIVA solo por abrirse |

---

## FASE C11 — El grupo, como consulta y sin pantalla

*Depende de C10.* Todo el dominio del grupo, probable **sin abrir ventana**
(ADR-0003 §8.1).

| # | Criterio |
|---|---|
| C11-1 | `FiltroDeCasos` gana **la fecha de viaje exacta**. Hoy tiene `SoloDeHoy` y `VentanaDeDias` y **no hay forma de pedir «el 8 de septiembre»** (medido: `Fichas.Contratos/Consultas/Filtros.cs`, 81 líneas) |
| C11-2 | Una consulta devuelve, para un mes, **los grupos de cada día** con: fecha, unidad, cuántos documentos, cuántas personas y el desglose de estados. Otra devuelve **el grupo entero** con sus casos y sus personas |
| C11-3 | **Sobre los siete escaneos reales del dueño**: la consulta del `2026-09-08` devuelve **7 documentos y 7 personas**, y **`CASP2609` y `CASD2609` caen en el MISMO grupo** — el segundo lleva el número mal escrito en su propia anotación y hoy queda suelto. *(La línea base de los siete es del supervisor, 2026-09-04. **Los siete archivos NO están en el repositorio**: `.gitignore` excluye `*.pdf`. Los pone el dueño o el supervisor)* |
| C11-4 | **La regla del estado de un grupo mixto es determinista y está escrita**: la pastilla dice «N de M completas» y, si queda alguno sin completar, el motivo **que más se repite**; en empate, el primero de este orden declarado: `el_lider_no_lo_hizo` · `no_se_pudo_comunicar` · `otra_razon` · sin marcar. **Nada de «el más grave»**: dos personas lo leerían distinto |
| C11-5 | Con 3 000 documentos: la consulta de un día **≤ 20 ms** y la del mes **≤ 60 ms**. *(Referencia medida en Python: 1,03 ms y 5,41 ms, con `explain query plan` confirmando `SEARCH ... USING INDEX idx_casos_viaje_activos` en las dos. Margen ×4 declarado: **otro motor, sin medir**)* |
| C11-6 | **Ningún índice nuevo.** Si hace falta uno, es que la consulta está mal escrita |
| C11-7 | Un caso **sin fecha de viaje** no cae en ningún grupo **y no desaparece**: sigue en la lista de pendientes (criterio C7-4, que no se toca) |

---

## FASE C12 — El calendario que responde al clic

*Depende de C11.*

| # | Criterio |
|---|---|
| C12-1 | `grep` de `Click`, `Tapped`, `AlPulsar` y `Seleccion` sobre `Inicio/` **deja de dar 0 coincidencias**. Hoy da 0 sobre `CalendarioDelMes.cs` (medido por el supervisor el 2026-09-05): el calendario es adorno, y el dueño tiene razón |
| C12-2 | La celda enseña **grupos, no documentos sueltos**. `PastillasPorDia = 3` y las **42 celdas fijas se quedan**: el número de elementos visuales del calendario sigue **sin depender de cuántos casos haya** (≤ 42 × 3 pastillas más los «+N más») |
| C12-3 | **C7-5 se vuelve a demostrar, no se hereda:** con 3 000 documentos el mes se pinta en **≤ 0,2 s** con las pastillas nuevas |
| C12-4 | **C7-2 sigue:** los archivados se ven en el calendario, con la palabra ARCHIVADO —el dueño pidió la palabra, no un color— |
| C12-5 | Pulsar un día **abre su grupo**. Navegar hoy solo sabe pasar los servicios (`VentanaPrincipal.xaml.cs:91`, `Navigate(PantallaDe(nombre), _servicios, ...)`): hace falta llevar **qué grupo**, y el cambio está en ese archivo de 108 líneas |
| C12-6 | Un día **sin grupos** no es pulsable y no da error: no hace nada y se ve que no hace nada |

---

## FASE C13 — La pantalla del grupo: verificar, asignar y ver el PDF

*Depende de C11 y C12.* **Es la única pieza que se escribe desde cero.**

| # | Criterio |
|---|---|
| C13-1 | Desde el calendario, **dos pulsaciones** hasta el PDF de una persona del grupo. Es lo que pidió: *«darle clic y ver solamente al grupo de personas que viajará en esa fecha, con su PDF»* |
| C13-2 | La pantalla ofrece **verificar** (abre Corrección en ese caso) y **asignar**, y asignar usa **`OperacionDeAsignar`, la única del programa** (criterio C5-1). **No se duplica la máquina de Revisar**: el grupo es Revisar filtrado por fecha y unidad |
| C13-3 | Se puede **asignar el grupo entero de una vez**. Sobre el grupo del `2026-09-08`: asignar los 7 documentos a un compañero crea **7 filas** en `asignaciones` y **0 duplicadas** — `idx_asignacion_viva` ya lo impide |
| C13-4 | Cada renglón dice **su estado y su motivo** con las palabras del dueño: «no completado», «no se pudo comunicar con el líder», «el líder no lo hizo» |
| C13-5 | Con 3 000 documentos, abrir el día más cargado de la base (en mi base de prueba, **98 documentos y 517 personas**) tarda **≤ 0,5 s** y **el número de elementos visuales tiene tope**, como el calendario. ⚠️ **No he medido ningún grupo real de más de 7 documentos**: si los suyos son de 200, este criterio es el que avisa |
| C13-6 | Un documento del grupo **sin PDF en su ruta** se ve y lo dice; no rompe la pantalla ni desaparece del grupo |

---

## FASE C14 — El comentario del agente viaja en el Excel

*Depende de C10.* **Va ANTES que los gerentes, y ése es el hallazgo que ordena
este bloque.**

**El agujero, medido:** el dueño quiere *«reporte para ellos con los comentarios
de los agentes»*. `personas.nota_companero` existe en la base y
`RepositorioDePersonas` sabe escribirla, pero `grep -rn "nota_companero"
csharp/Fichas/Fichas.Paquetes/` da **0 coincidencias**: la hoja tiene 16 columnas y
**ninguna es un comentario**. **Sin esta fase, el reporte del gerente sale en
blanco.**

| # | Criterio |
|---|---|
| C14-1 | La hoja gana **dos columnas de respuesta**: «Comentario del agente» → `personas.nota_companero`, y «¿Por qué no está completa?» (menú de tres) → `casos.motivo_del_companero` |
| C14-2 | **El C6-1 se corrige en el mismo commit**: dice «16 títulos y anchos» y pasan a ser **18**. Un criterio que se queda desfasado es un criterio que miente |
| C14-3 | **Una hoja VIEJA de 16 columnas que vuelva después del cambio sigue reconciliando.** El lector mapea **por título**, no por posición (`LectorDeExcel.BuscarLaFilaDeTitulos` busca la fila por el título de `clave`; `FilaPorTitulo` mapea el resto), así que los títulos que falten sencillamente no se encuentran. **Con prueba que lo demuestre**, no con el razonamiento |
| C14-4 | Lo que escribe la hoja va a **`motivo_del_companero`** y **NUNCA** a `motivo_no_completa`. Es la misma partición que hizo la migración 14 con el estado, y por el mismo motivo: **lo que dijo el agente no se borra cuando Miguel corrige encima** |
| C14-5 | Un comentario largo (500 caracteres) entra **entero** en la base y sale entero en el reporte |
| C14-6 | El resto del C6 no se mueve: la clave sigue siendo `caso:MRN:id` y sigue **a la vista en la última columna** |

---

## FASE C15 — Los gerentes y el paquete de segunda vuelta

*Depende de C10 y C14.*

| # | Criterio |
|---|---|
| C15-1 | El rol se **ve y se cambia** en la pantalla del equipo. **Nadie se crea solo** ni cambia de rol solo: decisión del dueño del 2026-09-04, *«yo debo tener el control de quién se añade y quién no»* |
| C15-2 | **Criterio de entrada a la segunda vuelta, escrito y comprobable:** entra el caso que **(a)** volvió con `estado_recomendacion = no_completa` **y (b)** tiene `motivo_del_companero` = `no_se_pudo_comunicar` o `el_lider_no_lo_hizo`. Un caso `completa` **no entra nunca**, tenga el comentario que tenga |
| C15-3 | El Excel del gerente es **el mismo generador** con otro filtro. La columna «Comentario del agente» va **BLOQUEADA y con el texto del agente dentro**: el gerente viene a leerla, no a escribirla |
| C15-4 | El reporte del gerente en PDF usa **el mismo motor** de `IReportes` (un método más, no un motor nuevo) y **lleva los comentarios de los agentes**, que es lo que él pidió |
| C15-5 | Sobre 3 000 documentos con 98 elegibles, el Excel y el PDF del gerente se escriben en **≤ 5 s** cada uno, y con Excel abierto y bloqueado **avisan en español sin caída** (criterio C6-7, que sigue mandando) |
| C15-6 | La vuelta del gerente escribe **por el mismo camino** que la del compañero: estado y motivo, **nunca** `procedencia_campo.verificado` |

---

## FASE C16 — «El administrador lo hizo»

*Depende de C10.* Sus palabras: *«yo puedo completarlos también, los paquetes,
desde el sistema sin pasar la verificación, y cuando pase eso debe decir "el
administrador lo hizo"»*.

**No hace falta ninguna columna nueva:** `casos.estado_marcado_origen` existe desde
la migración 14 y ya guarda dos valores distintos (`"a mano en la pantalla Revisar"`
y la ruta del Excel que trajo la marca). Éste es **el tercero**.

| # | Criterio |
|---|---|
| C16-1 | Completar como administrador escribe `estado_marcado_origen = "el administrador lo hizo"` — **las palabras del dueño, literales** — y se ven así en la tarjeta y en el reporte |
| C16-2 | **NO toca la firma de campos.** Se mide antes y después: `SELECT count(*) FROM procedencia_campo WHERE verificado = 1` **da el mismo número**. Regla permanente 5: son dos cosas y no se mezclan |
| C16-3 | ⚠️ **Se cierra primero el defecto de la firma, o esto hace daño.** `AccionesDeRevisar.QuienFirmaAMano` está documentado en su propio código como hueco declarado: coge el compañero llamado «Miguel» y, si no lo hay, **el primer activo**. En la base viva del dueño, medido el 2026-09-05, `companeros` tiene **una sola fila: `Sandy`**. **Hoy el administrador firmaría como Sandy.** El criterio: el administrador es **el compañero activo con `rol = 'administrador'`**; si no hay **exactamente uno**, el botón **no está** y se dice por qué. **Nunca se adivina** |
| C16-4 | La acción **pregunta antes**, con el número de documentos delante. Es el mismo trato que se le da a borrar |
| C16-5 | Un caso completado por el administrador **sigue en el calendario, en el grupo y en los reportes**, marcado con su origen. No se esconde |

---

## ⛔ Las cuatro decisiones de estas fases que son del dueño

Ninguna la puede cerrar un agente. **Están razonadas, con mi recomendación, en el
ADR-0005; aquí van en una línea cada una.**

| # | La pregunta | Mi recomendación |
|---|---|---|
| 1 | Si el mismo día viajan dos unidades, **¿es un grupo o son dos?** | **Dos**, con el día como cabecera: él habla con un líder por unidad. En sus datos de hoy no se nota (los siete escaneos son la misma unidad y la misma fecha), pero en `pdfs_referencia/` ya conviven tres prefijos |
| 2 | **¿`otra_razon` entra en la segunda vuelta?** | **Sí, pero listado aparte**: es donde caerá lo que el agente no supo clasificar |
| 3 | **¿Un gerente puede recibir un paquete de primera vuelta?** | **Sí, sin prohibirlo.** El rol sirve para filtrar la lista, no para cerrarla |
| 4 | Un caso completado por el administrador, **¿cuenta como «completo» en el informe de los jefes, o va aparte?** | **Aparte, y con su nombre.** El 2026-09-03 él decidió que el estado lo escribe *«el documento que ellos llenan… y dice completado por Sandy»*; aquí no hay documento de nadie, y mezclarlos borraría esa distinción que él mismo pidió |

## Qué NO cubren las fases C10 a C16

- **La petición 5 del 2026-09-05** («la pestaña de revisar todos los PDF antes de
  generar el paquete»). Toca la misma pantalla y por eso está nombrada arriba, pero
  **no la he especificado**: es un encargo aparte.
- **Las cuatro primeras peticiones del 2026-09-05** (firmar lo que él mismo escribe,
  «esto no está en el papel», borrar, y que el Excel devuelto se vea por persona).
  Son defectos y ya están encargadas.
- **`contactos`.** El dueño reabrió la DC-17 el 2026-09-04 y **entra** en el
  programa como registro de canal. **No tiene fase aquí y necesita su propia
  especificación** — qué canales, dónde se pintan, quién los escribe. No la he
  escrito. Y la pregunta que de verdad la decide, *¿usa Miguel hoy esta función?*,
  **sigue sin hacerse**.
- **El ROADMAP.** Es del dueño.
- **Fechas por persona.** Hoy la fecha vive en el caso. Me pregunté si hacía falta
  moverla a `personas` y **decido que no lo propongo**: nadie lo ha pedido y
  añadiría una fecha que puede contradecir a otra.

## Qué no pude verificar

1. **Los siete escaneos reales no están en el repositorio.** `find . -name "*.pdf"`
   da 7 archivos, 5 de ellos en `pdfs_referencia/` y con **tres prefijos distintos**
   (`CASP2609`, `PARB2609`, `SURB2609`); `.gitignore` excluye `*.pdf`. **Los números
   que cito sobre los siete son del supervisor, del 2026-09-04, y no los he vuelto a
   medir.**
2. **No ejecuté nada del C#** — ni compilar, ni la suite, ni el `.exe`. Hay tres
   programadores a la vez en `csharp/Fichas` y además no me corresponde: **eso lo
   dictamina QA.** Todo lo que digo del C# sale de **leer** los archivos que cito
   con su ruta.
3. **Los milisegundos son de SQLite 3.50.4 desde Python**, no de
   `Microsoft.Data.Sqlite`. Sirven para descartar la tabla `grupos` —tres órdenes de
   magnitud de margen— y **no como umbral**: por eso cada criterio lleva margen y lo
   declara.
4. **No sé cuántos documentos tiene un grupo real del dueño.** El único que conozco
   es de 7. Mi base sintética da un día de 98. **Si los suyos son de 200, el C13-5 es
   el criterio que lo descubre antes de que le duela.**

---

# Fases C17 a C23 — El ticket es la persona

**Escritas el 2026-09-05 por el planificador**, sobre las tres entradas del dueño de
ese día en `DECISIONES.md` («⚠️ EL DUEÑO EXPLICA SU TRABAJO, Y NO ES EL QUE EL
PROGRAMA CREE», «El ticket es por persona» y «Las seis preguntas SON las del sistema
del obispo»). **La investigación que las sostiene —las opciones comparadas, lo medido
y las cinco decisiones que son suyas— está en
`docs/adr/ADR-0006-el-ticket-es-la-persona.md`.** Aquí van las fases y sus criterios.

**Siguen numerando la serie C.** No renumeran ni tocan de la C0 a la C16.

## Lo que cambia del modelo, en cuatro frases

1. **La unidad que se resuelve es LA PERSONA.** Un documento con cinco personas son
   cinco tickets, y pueden estar tres cerrados y dos no. El grupo del día sigue siendo
   la unidad que se **reparte y se mira** (ADR-0005), y ahora cuenta **personas**.
2. **El estado del ticket no se guarda: se deriva de las seis preguntas** que ya
   viven en `personas.paso_*` desde la migración 9. Ninguna tabla `tickets`. Medido:
   la tabla no es más rápida (39,71 ms contra 36,99 ms en la consulta del mes), ocupa
   un 18,7 % más, y **puede mentir** — la demostración está en el ADR-0006 §2.3.
3. **«Listo para asignar» y «listo para viajar» son DOS preguntas distintas y las dos
   se quedan.** La primera la contesta el programa mirando nuestros campos; la segunda
   la contesta una persona mirando el sistema del obispo. El defecto de hoy no es que
   sobre una: es que **la pantalla enseña una y él lee la otra**.
4. **Se añade un origen de respuesta que no existía: Miguel, dentro del programa.**
   Hoy las seis preguntas solo se pueden contestar por el Excel que va y vuelve.

## Qué se tira, qué se reordena, qué no se toca

- **Se tira: nada.** Ni un archivo, ni una columna, ni una migración. Lo que parecía
  sobrar es justo lo que el modelo nuevo necesita.
- **Se reordena:** de dónde sale un dato en cuatro pantallas —Grupo, Inicio, el
  calendario y el reporte de la vuelta— y **una pantalla se escribe desde cero**, la
  de contestar las seis preguntas de una persona. El desglose por archivo y línea está
  en el ADR-0006 §5.2.
- **No se toca:** `Fichas.Lectura` entera · Importar · `procedencia_campo` y la firma
  de campos · `LoQueLeFalta` · las seis columnas `ord_*` · las migraciones 2 a 18 ·
  la regla que el Excel del compañero usa para el estado del documento.

## Coste — ⚠️ estimación, NO medición

No ejecuté nada del C#: hay dos programadores a la vez en `csharp/Fichas` y además no
me corresponde. **4 a 5,5 días-programador.** Lo que él ve primero —las dos palabras
separadas y el estado que deja de mentir— son **C17 + C18: menos de un día**, y no
dependen de ninguna migración.

## El orden, y por qué es ese

Él lleva dos días abriendo el programa y diciendo *«es confuso, muy confuso»*. Las dos
primeras fases no añaden ninguna función: **quitan la confusión concreta que él
nombró**, cuestan poco y no tocan la base. La tercera es la pieza que le falta para
hacer su trabajo dentro del programa. Las cuatro últimas van detrás porque **ninguna
sirve hasta que las seis preguntas se puedan contestar**.

---

## FASE C17 — Las dos palabras dejan de confundirse

*No depende de nada. Sin migración, sin consulta nueva.* Es el arreglo más barato del
bloque y ataca la frase textual del dueño.

| # | Criterio |
|---|---|
| C17-1 | Donde hoy pone «Listo para asignar» sigue poniendo eso, **y se le añade lo que significa**: que el sistema llenó todos los campos. Es lo que él pidió el 2026-09-05 y **no se quita** |
| C17-2 | En ningún sitio del programa aparece la palabra «listo» a secas sobre un documento o una persona. `grep -rn "Listo" csharp/Fichas/Fichas.App --include=*.xaml --include=*.cs` (fuera de `obj/`) devuelve **solo rótulos que dicen listo PARA QUÉ** |
| C17-3 | La pantalla del grupo y la de Corrección dicen, para cada persona, **«recomendación sin confirmar»** mientras sus seis preguntas no estén todas en sí. La frase es del dueño: *«la recomendación para el templo no está confirmada»* |
| C17-4 | **Nada cambia de comportamiento.** La suite pasa sin tocar ni una prueba de `PruebasDeListoParaAsignar.cs`. Si una se pone roja, es que se cambió algo que esta fase no debía cambiar |

---

## FASE C18 — El estado de una persona sale de SUS seis preguntas

*Depende de C17.* Sin migración. **Es donde el programa deja de mentir.**

| # | Criterio |
|---|---|
| C18-1 | `PersonaDelGrupo` gana su propio estado, calculado con `Pasos.Estado(persona)` —la función que ya existe en `Fichas.Reportes/Reglas/Pasos.cs` y devuelve **sí, no o no se sabe**—, y **deja de copiar `caso.Estado`**, que es lo que hace hoy `LectorDeGrupos.cs:306` |
| C18-2 | **Tres estados en pantalla y no dos:** «lista para viajar», «no lista», y **«sin mirar»** para la que tiene alguna pregunta en blanco. Sin mirar **no** se pinta como no lista: son cosas distintas y la propia función lo dice en su comentario |
| C18-3 | Cuando una persona no está lista, la pantalla dice **en qué paso se quedó**, con `Pasos.SinCompletar(persona)`, ya construido. Es lo que él le dice al obispo por teléfono |
| C18-4 | ⚠️ **La prueba que demuestra el cambio de unidad necesita un caso con VARIAS personas, y hay que fabricarlo:** un documento con **5 personas, 2 listas y 3 no**, pinta **5 renglones con 2 estados distintos**. Hoy pintaría 5 iguales. **Los siete escaneos reales del dueño traen UNA persona cada uno** (medición del supervisor, 2026-09-04), así que **sobre sus datos de hoy este cambio no se ve**: los siete siguen dando 7 documentos y 7 personas, y ése es el criterio de no regresión |
| C18-5 | `casos.estado_recomendacion` **no se toca ni se recalcula aquí.** Sigue siendo lo que escribe el Excel del compañero con su nombre — regla permanente 5 tal como el dueño la precisó el 2026-09-03. Se mide antes y después: `SELECT estado_recomendacion, count(*) FROM casos GROUP BY 1` da lo mismo |
| C18-6 | Con 3 000 documentos, la pantalla del grupo del día más cargado **no tarda más que hoy**. *(Referencia medida en Python sobre 3 000 casos y 16 500 personas: la consulta del día con el estado derivado de las seis columnas tarda **3,17 ms** para 635 personas, por `idx_casos_viaje_activos` + `idx_personas_caso`. Margen ×6 declarado: **otro motor, sin medir**)* |
| C18-7 | **Ningún índice nuevo.** El plan de la consulta no lleva ni un `SCAN`: si aparece uno, la consulta está mal escrita |

---

## FASE C19 — Contestar las seis preguntas dentro del programa

*Depende de C18.* **Es la única pantalla que se escribe desde cero, y es la pieza que
le falta para hacer su trabajo.** Cubre sus peticiones 3 y 4.

**Migración 19 — tres columnas en `personas`, cero reconstrucciones:**

```sql
ALTER TABLE personas ADD COLUMN pasos_por    INTEGER REFERENCES companeros (id) ON DELETE RESTRICT ON UPDATE RESTRICT;
ALTER TABLE personas ADD COLUMN pasos_en     TEXT;
ALTER TABLE personas ADD COLUMN pasos_origen TEXT;
```

| # | Criterio |
|---|---|
| C19-1 | La 19 se engancha **al final** de `CatalogoDeMigraciones.Todas` y `VersionAlDia` sube sola. **Ningún número escrito a mano**: lo dice el propio comentario del catálogo |
| C19-2 | Son **tres `ADD COLUMN`, cero reconstrucciones**, y es la misma forma que la migración 14 le dio a `casos`. Verificado contra la documentación oficial (<https://sqlite.org/lang_altertable.html>, consultada el 2026-09-05): lo que `ADD COLUMN` no admite no incluye ni un `REFERENCES` sin `NOT NULL` ni una columna de texto sin `DEFAULT` |
| C19-3 | **El conteo de columnas pasa de 108 a 111 y hay CUATRO sitios que lo llevan escrito a mano.** Se corrigen los cuatro en el mismo commit o la suite se pone roja: `Fichas.Pruebas.Datos/PruebaDeLaBaseReal.cs:128`, `PruebaDeLasColumnasUnaAUna.cs:105`, `PruebaDelEsquema.cs:100` (y su desglose de la línea 33, que pasa a `22+28+7+6+12+16+8+9+3`), y `docs/ARQUITECTURA.md` §2 — **que además arrastra un error previo: dice 104 y la base viva tiene 108**, medido hoy con `pragma table_info` sobre una copia |
| C19-4 | La base viva del dueño —**2 casos, 0 personas, 1 compañero (`Sandy`), versión 18**, medido el 2026-09-05— sube a la 19 **sin perder una fila**, con su respaldo previo (`RespaldoAntesDeMigrar`), y `pragma integrity_check` devuelve `ok` |
| C19-5 | Desde la pantalla del grupo o desde Revisar, **una pulsación** abre la ventana de una persona con **sus seis preguntas contestables**, su nombre, su cédula y su documento a mano. Es su petición 3 |
| C19-6 | Cada pregunta tiene **tres respuestas y no dos**: sí, no y **dejarla en blanco**. Y se puede **volver a dejar en blanco** una ya contestada: si contestar fuera irreversible, nadie se atrevería a contestar |
| C19-7 | Al guardar, se escriben las seis columnas **y las tres de la 19**: `pasos_por` = el compañero que es Miguel, `pasos_en` = ahora en ISO-8601, `pasos_origen` = `"a mano en la pantalla"` |
| C19-8 | ⛔ **NO toca la firma de campos.** Se mide antes y después: `SELECT count(*) FROM procedencia_campo WHERE verificado = 1` **da el mismo número**. Regla permanente 5: contestar las seis y firmar un campo son dos cosas y no se mezclan |
| C19-9 | La ventana dice **quién contestó y cuándo** lo que ya estaba contestado, y si lo contestó un agente **lo dice con su nombre** antes de dejar cambiarlo. Sobrescribir la respuesta de Sandy sin que se vea que era suya es la forma de que nadie sepa nunca de quién se fía |
| C19-10 | ⚠️ **Se cierra primero el defecto de quién firma, o esto escribe el nombre equivocado.** `AccionesDeRevisar.QuienFirmaAMano` coge el compañero llamado «Miguel» y, si no lo hay, **el primer activo**; en la base viva del dueño `companeros` tiene **una sola fila, `Sandy`** (medido el 2026-09-05). **Hoy Miguel contestaría como Sandy.** Es el mismo defecto que ya bloquea la FASE C16 y se arregla una vez para las dos |

---

## FASE C20 — El Home cuenta PERSONAS del grupo que viene

*Depende de C18.* Sin migración. Es su petición 7, con sus palabras: *«las personas
del grupo del 17 de septiembre, faltan 3, 4 o 5 personas que la recomendación no está
confirmada»*.

| # | Criterio |
|---|---|
| C20-1 | Inicio enseña, para el **próximo grupo con fecha**, la frase con su forma: «Del grupo del 17 de septiembre faltan **N personas** por confirmar, de M». Con `N = 1` dice «1 persona» —el plural ya está resuelto en `Plural.Con`— |
| C20-2 | Las **dos cifras que él pidió el 2026-09-05** —lo listo para asignar y lo asignado— **siguen donde están y no se tocan.** Esto es una tercera, no un cambio de las otras dos |
| C20-3 | La pastilla del calendario dice **«N de M confirmadas»** en personas, no en documentos. Las **42 celdas fijas y las 3 pastillas por día no cambian**: el número de elementos visuales sigue sin depender de cuántos casos haya (criterio C12-2, que sigue mandando) |
| C20-4 | El denominador se ve siempre, como exige el C1-1: una cifra sin su denominador no se puede comprobar |
| C20-5 | Con 3 000 documentos, Inicio se pinta en **≤ 0,2 s**, el mismo tope de la C7-5, que **se vuelve a demostrar y no se hereda**. *(Referencia medida en Python: contar el reparto de estados de un día cuesta **1,10 ms**, y el mes entero por día y estado **36,99 ms**. Margen ×5 declarado: otro motor)* |
| C20-6 | Un día **sin ninguna persona sin confirmar** lo dice con esas palabras, y no enseña un hueco ni un cero mudo |

---

## FASE C21 — El ticket vencido: quien viajó sin la recomendación confirmada

*Depende de C18.* Sin migración. **Es el daño que este programa existe para evitar.**

| # | Criterio |
|---|---|
| C21-1 | La regla está escrita y es determinista: **un ticket está vencido si su fecha de viaje ya pasó y su estado no era «lista para viajar»**. Dos personas la leen igual |
| C21-2 | Un ticket vencido **no se cierra solo, no se archiva solo y no desaparece**. Tampoco cuando el documento se archiva: el dueño decidió el 2026-09-05 que lo archivado sale de la vista **y sigue en el histórico y en los reportes** |
| C21-3 | Se ve **cuántas personas** viajaron sin confirmar, no cuántos documentos. Una familia de cinco donde falló uno es **una persona**, no un documento |
| C21-4 | Lo que **apremia** usa la ventana de **7 días** que ya existe (`LectorDelInicio.DiasDeLaVentana`, pedida por él en el C1-4). **No se inventa un segundo número** para «pronto»: dos números distintos en la misma pantalla es otra vez el programa diciendo dos cosas. ⛔ Si él quiere otro, es un número en un sitio |
| C21-5 | **No hay aviso que salga solo del programa**: ni correo, ni sonido, ni servicio. Regla permanente 2 y nadie lo ha pedido. El aviso es una lista que se ve al abrir |
| C21-6 | `personas.pudo_viajar` y `personas.motivo_no_viajo` **no se tocan**: son otra pregunta —qué pasó después— y confundirlas haría que el programa diera por explicado lo que solo está avisado |

---

## FASE C22 — La vuelta del agente dice qué falta, y lo dice por persona

*Depende de C18.* Sin migración. Es la **segunda mitad** de su petición 9; la primera
—que el paquete lleno cierre solo— **ya está construida** en
`Fichas.Paquetes/Paquetes.cs:511-525`, y esta fase no la toca.

| # | Criterio |
|---|---|
| C22-1 | Al aplicar un paquete devuelto, sale un resumen con **cuántas personas** trajo, cuántas volvieron **listas**, cuántas **no listas** y cuántas **sin mirar**. Las cuatro cifras en personas; ninguna en documentos |
| C22-2 | De las que no volvieron listas, dice **en qué paso se quedó cada una**, con `Pasos.SinCompletar`, ya construido |
| C22-3 | La frase de cabecera es la suya: *«Sandy no pudo verificar todos, por favor verifica qué falta»*, con el nombre del compañero que devolvió la hoja, **no un nombre inventado ni «el compañero»** |
| C22-4 | Al escribir las seis columnas desde el Excel se escriben **también las tres de la 19**, con `pasos_origen` = la ruta del Excel. Es lo mismo que ya hace `estado_marcado_origen` desde la migración 14 |
| C22-5 | **Una hoja vieja sigue reconciliando.** El lector mapea por título y no por posición (`LectorDeExcel.BuscarLaFilaDeTitulos`), y hay que demostrarlo con una prueba, no razonarlo |
| C22-6 | El aviso que ya existe para el caso raro —volver completo y con un motivo escrito a la vez, `Paquetes.cs:495-508`— **se queda tal cual** |

---

## FASE C23 — La pista de grupo por número de caso

*Depende de C18.* Sin migración. Es su petición 11.

| # | Criterio |
|---|---|
| C23-1 | Dentro del grupo de un día, los documentos que **comparten número de caso** se enseñan juntos, con cuántos documentos y cuántas personas suman |
| C23-2 | ⛔ **Es una PISTA y no una identidad, y la pantalla lo dice.** No fusiona nada, no agrupa a la fuerza y se puede ignorar. La decisión del 2026-09-03 sigue entera: el número son cuatro letras más `AAMM` e identifica **una unidad y un mes**, no una familia — medido por el dueño, **6 de 10 documentos rechazados como «caso ya existente» siendo otras familias** |
| C23-3 | **Sobre los siete escaneos reales**, la pista del `2026-09-08` agrupa **6 documentos bajo `CASP2609`** y deja **`CASD2609` aparte**, y **dice por qué**: ese documento lleva la D escrita en su propia anotación, no lo leyó mal ningún motor (medición del supervisor, 2026-09-04). Una pista que falla sin explicarse parece un fallo del programa |
| C23-4 | Un número de caso **nulo** —una hoja cuyo número no se pudo leer— no agrupa con nada y **no desaparece del grupo** |
| C23-5 | Con 3 000 documentos, la pista **no añade más de 5 ms** a la pantalla del grupo. *(Referencia medida en Python: **0,69 ms**, por los índices que ya existen, sin ninguno nuevo. Margen ×7 declarado: otro motor)* |
| C23-6 | **Unir dos documentos en un mismo caso NO entra aquí.** Sigue pedido desde el 2026-09-04 y sin fase; ver «Qué NO cubren» |

---

## ⛔ Las cinco decisiones de estas fases que son del dueño

Ninguna la puede cerrar un agente. **Están razonadas, con mi recomendación, en el
ADR-0006; aquí van en una línea cada una.**

| # | La pregunta | Mi recomendación |
|---|---|---|
| 1 | Si Miguel contesta encima de lo que contestó Sandy, **¿se guarda lo que dijo Sandy?** | **No, y se ve quién contestó lo último.** Las seis preguntas no son opinión de nadie: son lo que pone en la pantalla del obispo, y si discrepan suele ser porque el obispo lo arregló en medio. Guardar el historial cuesta **99 000 filas y ×4,7 de tamaño por ronda**, medido, y se puede añadir después sin deshacer nada |
| 2 | **¿«Listo para viajar» exige las seis, o basta la sexta?** | **Las seis**, y él lo corrige al verlo. La regla vive en un solo sitio (`Pasos.Estado`) para poder cambiarla de un sitio |
| 3 | **¿Un agente puede dejar una persona lista sin que Miguel la mire?** | **Sí**, porque él lo dijo con esas palabras y lo contrario le devuelve las 3 000 pulsaciones. Con **quién y cuándo siempre a la vista** |
| 4 | **¿Cuántos días antes apremia un viaje?** | **Los 7 que ya usa el calendario.** Un segundo número para «pronto» vuelve a hacer que el programa diga dos cosas |
| 5 | **¿Quiere poder UNIR dos documentos** en un mismo caso, que es lo que arreglaría el `CASD2609`? | **Sí, y es una fase aparte que no está escrita.** Está pedida desde el 2026-09-04 |

## Qué NO cubren las fases C17 a C23

- **Las peticiones 5 y 8** (la unidad que no se ve, y nombrar y cargar carpetas).
  **Encargadas a programadores mientras se escribía esto**; no las planifico.
- **La petición 2** (en Corrección, solo lo que falta). Es un defecto de una pantalla
  y no toca el modelo: encargo aparte.
- **La petición 10** (subir el caso al gerente con lo que falta señalado). **Ya tiene
  fase**: la C15 del bloque C10–C16.
- **La sección «revisar este documento»** para lo que no tiene fecha legible, dentro
  de la petición 6. Toca la misma pantalla que la agrupación por fecha y **no la he
  especificado**.
- **Unir dos documentos en un caso.** Decisión ⛔ 5.
- **El informe por agente, por mes y por semana**, pedido el 2026-09-05. Encaja con
  este modelo —se cuenta por personas— y **no lo he especificado**.
- **El tema claro u oscuro.** No toca el modelo.
- **`contactos`.** Sigue sin especificación desde el ADR-0005, y la pregunta que lo
  decide —*¿usa Miguel hoy esta función?*— **sigue sin hacerse**.
- **El ROADMAP.** Es del dueño.

## Qué no pude verificar

1. ⚠️ **Los siete escaneos reales traen UNA persona cada uno** (medición del
   supervisor, 2026-09-04: «Personas: 7 de 7»). **O sea que sobre los datos reales de
   hoy, contar por documentos y contar por personas da el mismo número, y todo este
   cambio de modelo no se ve.** Cualquier criterio que quiera demostrarlo necesita
   **además** un caso con varias personas, fabricado o pedido al dueño. Es la
   comprobación más importante de este bloque y **es la que no puedo cerrar yo**.
2. **No sé cuántas personas tiene una familia real suya.** El esquema admite 12 en 6
   hojas; mi base sintética llega a 10. Si las suyas son de 20, ningún criterio de
   aquí lo descubre.
3. **No ejecuté nada del C#** — ni compilar, ni la suite, ni el `.exe`. Hay dos
   programadores en `csharp/Fichas` y además **no me corresponde: eso lo dictamina
   QA.** Todo lo que digo del C# sale de leer los archivos que cito con su línea.
4. **Los milisegundos son de SQLite 3.50.4 desde Python**, no de
   `Microsoft.Data.Sqlite`. Sirven para descartar la tabla `tickets` y para saber que
   no hace falta ningún índice nuevo; **no como umbral**. Por eso cada criterio lleva
   su margen y lo declara.
5. **`docs/ARQUITECTURA.md` §2 dice 104 columnas y la base viva tiene 108** (medido
   hoy sobre una copia). **Deuda nueva**, anotada aquí porque este pase no me encarga
   corregir ese documento.

---

## Deuda abierta del 2026-09-06 — la frase del histórico quedó falsa

Al hacer que **lo archivado desaparezca de todas partes** (`DECISIONES.md`,
2026-09-06) quedó **mintiendo una frase que se IMPRIME en el PDF que leen los
jefes**:

- **`csharp/Fichas/Fichas.Reportes/Armado/ArmadoDelHistorico.cs:69-74`**,
  `QueLePasaAUnArchivado`, sigue diciendo *«Sigue viéndose, marcado como
  archivado, en el calendario del Inicio»*. **Desde hoy es falso**: el calendario
  ya no lo enseña. Medido con la ventana abierta sobre el paquete publicado: el
  día del viaje pasó de «martes 8 de septiembre de 2026, **1 grupo**» a «martes 8
  de septiembre de 2026, **sin grupos**».
- **Por qué no lo corregí:** `Fichas.Reportes` **queda fuera del terreno de mi
  pase**, que era `Fichas.App` y sus pruebas. La decisión de tocarlo es del
  supervisor.
- **El texto que propongo**, si se acepta: quitar «Sigue viéndose, marcado como
  archivado, en el calendario del Inicio, y» y dejar *«…deja de salir en el
  selector de Corrección, en Asignar, en Revisar, en el calendario y en las
  cifras del Inicio. Sigue viéndose en Revisar cuando se marca «Ver los
  archivados», que es desde donde se desarchiva. Y sigue contando entero en los
  reportes del período y en este histórico.»*
- **Quién avisa si se olvida:** la prueba
  `Fichas.Pruebas.App/Reportes/PruebasDeLaFraseDelArchivadoContraLaApp.cs`,
  `ElInicioNoLoCuentaEnLasCifrasNiLoEnsenaEnElCalendario`, ya mide la conducta
  nueva y lleva el hallazgo escrito en su propio comentario. **Lo que esa prueba
  NO hace hoy es comparar la frase carácter a carácter con la conducta**: si lo
  hiciera, la suite quedaría en rojo hasta corregir `Fichas.Reportes`.

### Lo que se decidió conservar, y no es olvido

Medido en el paquete publicado, con 12 casos y 3 archivados:

| Sitio | Qué dice | Por qué se queda |
|---|---|---|
| Inicio | «12 casos en la base · **9 sin archivar**» | dice cuánto se recorta, sin enseñar ningún caso |
| Asignar | «9 sin archivar · 9 ofrecidos · **3 archivados fuera de la lista**» | igual; sin él, una lista que encoge no se distingue de una que perdió casos |
| Incompletos | «4 sin completar · 10 personas · **de 9 sin archivar, de 12 en la base**» | igual |
| `Fichas.App/Importar/BuscadorDeDuplicados.cs:93,114` | busca duplicados **con** los archivados | si no los mirara, reimportar un PDF ya archivado crearía un duplicado |

Son **contadores agregados, no apariciones de un caso**. Si el dueño los lee como
«sigue apareciendo», se quitan: es decisión suya y **se le devuelve**.
