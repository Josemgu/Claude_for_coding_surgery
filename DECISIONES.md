# Decisiones

Qué se decidió y por qué. Nada se borra: si una decisión se revierte, se tacha en
el sitio con su motivo y su fecha.

---

## 2026-09-02 — El plan de construcción se reparte en los cuatro documentos

**Decisión del dueño.** El plan de las diez fases se entregó como un documento
suelto para `docs/plan.md`. El hook `no-crear-documentos.sh` lo denegó: la lista
blanca del proyecto es cerrada y `docs/plan.md` no está en ella.

Se descartaron dos alternativas:

- Abrir `.claude/LLAVE-DEL-DUENO` a mano y añadir `docs/PLAN.md` a la lista
  blanca. Habría creado dos documentos permanentes nuevos por una sola vez.
- Meterlo todo en `docs/ARQUITECTURA.md`. Ese documento es el mapa del sistema,
  no un plan de fases con criterios de aceptación.

**Reparto acordado:**

| Contenido del plan | Dónde vive |
|---|---|
| Reglas permanentes | `CLAUDE.md` §1 |
| Reglas de no regresión | este documento, abajo |
| Tecnología y rutas | `CLAUDE.md` §3 y §4 |
| Las diez fases con sus criterios de aceptación | `PENDIENTES.md` (del planificador) |
| La fase activa, su pase y su QA | `EN-CURSO.md` |

---

## 2026-09-02 — El proyecto arranca de cero

**Medido.** `ls -la` sobre `C:\Users\josem\OneDrive\Escritorio\Trabajo` devolvió
únicamente `.claude/`. No hay `CLAUDE.md` previo, ni `docs/`, ni un solo `.py`.
Una búsqueda con `find` sobre OneDrive, Documentos y Escritorio no encontró
`CASP2609_Zutano_Family.pdf` ni ningún `esquema.md`.

**Confirmado por el dueño:** borró todo lo que había. De la carpeta anterior le
interesan los agentes y el método, no el plan ni el código.

**Consecuencia sobre la FASE 0.** Tal como estaba escrita pedía «inventario del
código actual» y «prueba de PyInstaller sobre el estado actual del proyecto». No
hay código que inventariar ni estado sobre el que probar. La fase se replantea:
se queda solo la prueba de empaquetado, sobre un esqueleto mínimo hecho para eso.
El detalle está en `EN-CURSO.md`.

---

## 2026-09-02 — `Trabajo` lleva su propio repositorio

**Medido.** `git rev-parse --show-toplevel` desde la carpeta del proyecto
devolvía `C:/Users/josem`: el repositorio que la cubría tenía la raíz en el
perfil de usuario entero. Un commit desde aquí habría arrastrado `AppData`,
`.vscode`, las carpetas de juegos y todo lo demás.

**Decisión.** `git init` en `Trabajo`. Comprobado después: `git rev-parse
--show-toplevel` devuelve `C:/Users/josem/OneDrive/Escritorio/Trabajo`. Rama
inicial `master`.

Es condición de la regla permanente 6 —«cada fase termina en commit, rama por
fase»—, que sin repositorio propio no se puede cumplir.

**Qué se ignora y por qué.** `.gitignore` deja fuera `*.pdf`, `*.xlsx`, `*.db`,
`*.sqlite*` y la carpeta `fichas/`. No es higiene: los formularios escaneados y la
base llevan nombres y MRN de personas reales, y no se suben a ningún sitio. La
base y el Excel espejo viven en `Documentos\Fichas` de todos modos; el patrón
está por si alguna vez aparece una copia suelta. También queda fuera
`LLAVE-DEL-DUENO`, que es de una máquina y de un rato.

`.claude/` **sí** se versiona, `settings.json` incluido. Lo pide el propio
`candado-del-dueno.sh` en su comentario: la defensa contra que alguien ablande
los hooks es que el cambio se vea en el diff.

### El repositorio es local y se queda local

**Decisión del dueño, el mismo día.** Dijo que no se trabaja con GitHub y que no
hacía falta commitear. Se le señaló que git y GitHub son cosas distintas —git
funciona entero en local, sin cuenta, sin subir nada y sin internet— y que la
regla permanente 6 es suya. Confirmó: se queda el historial local para poder
volver atrás, y **no se conecta a ningún remoto ni se hace `push` nunca.**

**Medido antes de preguntar:** `git remote -v` salía vacío. Nada había salido de
la máquina en ningún momento.

**Consecuencia práctica:** `docs-al-dia.sh`, que vigila que los cuatro documentos
viajen en cada `push`, no va a disparar jamás en este proyecto. Lo que esa puerta
protege —que el trabajo no avance sin su registro— pasa a depender de que se
cumpla a mano en cada cierre de fase.

---

## 2026-09-02 — La carpeta de datos se resuelve por API, nunca por nombre

Lo levantó el planificador al entregar `PENDIENTES.md`: la base con nombres y MRN
reales va a `Documentos\Fichas`, y si esa carpeta sincroniza con OneDrive, los
datos suben a la nube por una puerta que el `.gitignore` no cubre.

**Medido por el supervisor, 2026-09-02.**

```
HKCU\...\Explorer\User Shell Folders → Personal = C:\Users\josem\Documents
VEREDICTO: Documentos NO esta redirigido a OneDrive
```

Pero además:

```
$ ls -d .../OneDrive/Documentos .../OneDrive/Documents .../Documents
C:/Users/josem/Documents
C:/Users/josem/OneDrive/Documentos
C:/Users/josem/OneDrive/Documents
```

**Las tres existen.** La carpeta real de Documentos no sincroniza; las dos de
OneDrive sí. Así que el riesgo no es el que se temía —no hay redirección— sino
uno de resolución de ruta: un programa que arme `Documentos\Fichas` **pegando el
nombre en español** puede aterrizar en `OneDrive\Documentos\Fichas` y subir a la
nube la base entera sin que nadie lo note.

**Decisión.** La carpeta de datos se obtiene con la API de carpetas conocidas de
Windows (`FOLDERID_Documents` / `SHGetKnownFolderPath`), nunca componiendo una
ruta a partir de la cadena «Documentos» ni de «Documents». Al arrancar, el
programa **muestra la ruta que resolvió** antes de escribir nada, para que se vea
si cayó donde debía. Vinculante para la FASE 9 y para cualquier fase que escriba
en disco antes.

⚠️ Lo que **no** se midió: si el cliente de OneDrive tiene activada la copia de
seguridad de carpetas conocidas y podría redirigir `Documentos` más adelante. La
decisión de arriba protege igual en ese caso, porque la API devuelve la ruta
vigente en el momento, sea cual sea.

---

## 2026-09-02 — Uso personal: el equipo se reduce, y cinco hallazgos pasan a deuda aceptada

**Decisión del dueño.** El programa es de uso personal. El equipo queda en
**diseñador y programador, con QA al final** — no QA en cada entrega. El
planificador ya hizo su trabajo: `PENDIENTES.md` está escrito y auditado dos
veces.

**Qué se acepta sin corregir.** La segunda auditoría de QA devolvió VUELVE AL
AGENTE con cinco puntos. No se corrigen. Están descritos en `EN-CURSO.md`, y los
dos que más pesan son:

1. El criterio que protege la regla permanente 1 **no caza la carga dinámica**:
   un `importlib.import_module("llama_cpp")` que le pase un campo del OCR a un
   modelo local pasa limpio, y el criterio de sockets tampoco lo ve porque un
   modelo local no abre ninguno. QA lo midió: 2 de 11 archivos violan, 0
   señalados. Y el documento afirma dos veces que sí lo tapa. **Esa afirmación
   falsa se queda en el documento.**
2. La FASE 1 exige un `9 de 9` que ningún comando puede dar —QA midió 8 de 9 como
   techo—, así que tal como está esa fase no puede pasar nunca. Se resolverá a
   mano cuando se llegue a ella.

**Lo que cuesta, dicho para que conste.** Los criterios de aceptación son más
flojos de lo que QA aceptaría. El barrido por clase de defecto que QA pedía —«las
correcciones se aplicaron en las coordenadas que le di, no a la clase de
defecto»— no se hizo. Para un programa que va a usar una persona, el dueño juzga
que seguir puliendo el documento cuesta más que tener el programa funcionando. Es
su llamada.

⚠️ El punto 1 no es papeleo: la regla permanente 1 existe porque un MRN inventado
manda a alguien al templo con la recomendación mal. El criterio automático no la
protege. Queda protegida solo por que nadie escriba esa llamada a propósito.

---

## 2026-09-02 — Tres decisiones que salen de la FASE 0

### Los modelos `.onnx` no van al repositorio

13.7 MB en tres archivos. Sus tres SHA256 coinciden con el `default_models.yaml`
de `rapidocr` y `descargar_modelos.py` los reproduce. **El script es la fuente, no
el binario.** Añadido `modelos/` al `.gitignore`, junto con `*.spec`, que
PyInstaller regenera en cada construcción.

### `opencv-python` se cambia por la versión sin vídeo — deuda de la FASE 9

**Medido por el programador en la FASE 0:** de los 244 MB del paquete, **`cv2` pesa
111.76 MB** — 82.30 MB de `cv2.pyd` y **29.45 MB de
`opencv_videoio_ffmpeg500_64.dll`**.

`CLAUDE.md` §3 dice que de OpenCV este proyecto usa **solo `medianBlur`**. Un
códec de vídeo de 29 MB dentro de un programa que lee formularios escaneados no
pinta nada. Cambiar a `opencv-python-headless` o excluir `videoio` son unos
**110 MB de rebaja sobre 244**, casi la mitad del paquete.

No se hizo en la FASE 0 porque cambiar dependencias no era de esa fase. Se hace en
la FASE 9, y el número medido queda aquí para que nadie tenga que volver a
medirlo.

### El tope de 3500 px necesita `math.nextafter`

⚠️ **Esto refina una regla de no regresión, y el motivo es de coma flotante.**
`3500/792` da `3500.0000000000005`, y pypdfium2 redondea hacia arriba: el
rasterizado salía a **3501 px**, uno por encima del tope. Con `math.nextafter` sale
**2705x3500 exacto**, comprobado en la salida del `.exe`.

La regla original —tope de 3500 en el lado largo— no cambia. Lo que se añade es
que **calcular la escala por división directa no la respeta**. Vale para la FASE 2,
que es la que rasteriza de verdad.

---

## 2026-09-02 — Las siete preguntas del esquema, resueltas por el supervisor

El dueño delegó: «te dejo actuar, solo cuando termines me comentas». Estas son
decisiones del supervisor, no suyas, y se marcan como tales para que pueda
revertir cualquiera.

**P-1 — `estado_recomendacion`.** El planificador midió sobre los 65 archivos del
proyecto: `estado_recomendacion` → 0 coincidencias, `no_indicada` → 0, «cuatro
valores» → 0. **El campo no existe en ninguna parte del material.** Yo lo di por
existente citando material de fuera del repositorio; era un error mío.

Pero la FASE 6 sí lo necesita: el compañero rellena una lista desplegable. Y la
FASE 5 lo necesita para pintar en rojo lo que viaja incompleto.

Decisión: **columna guardada**, no derivada, porque la FASE 6 la rellena desde
fuera y un valor derivado no admite que alguien lo escriba. **Sin `CHECK` con una
lista inventada**: la lista de valores válidos vive en **un solo sitio del
código**, y arranca con los dos únicos que constan en el material —`no_indicada` e
`incompleta`—. Añadir los que falten es tocar esa lista y nada más.
⚠️ **Pendiente del dueño:** los otros dos valores. Hasta que los diga, la
desplegable tendrá dos opciones.

**P-2 — La regla del mes cruzado NO es una pared.** El planificador midió que como
`CHECK` haría **imposible guardar un viaje reprogramado a otro mes**, que es una
cosa que pasa en la vida real. Va como **aviso en la pantalla, no como
restricción del motor**. Coincide con la regla permanente 5: el sistema propone,
Miguel confirma.

**P-3 — El origen de un campo corregido a mano se llama `manual`.** Ya estaba así
en el plan del dueño; se confirma.

**P-4 — Las asignaciones se desactivan, no se borran.** Igual que los compañeros y
los casos. Un historial de quién tuvo qué caso vale más que una tabla limpia.

**P-6 — `unidad_numero` y `fecha_viaje` van en `casos`, no en `personas`.** El
formulario tiene una unidad y una fecha de viaje por hoja, y las personas son
filas dentro de esa hoja. ⚠️ Si algún día dos familiares del mismo caso viajan en
fechas distintas, la columna se mueve a `personas` y **hay que revisar la FASE 5
entera**. Queda dicho para que no sorprenda.

**P-5 y P-7** no son de esquema y no bloquean: se resuelven en su fase.

### Y una que el planificador decidió, que confirmo

**Seis columnas de ordenanzas, no tabla hija.** Su argumento cierra el caso: la
FASE 2 exige un tercer estado, «no leída», y con tabla hija «no hay fila» y «no
marcada» se confunden — Miguel dejaría de distinguir lo que el sistema no leyó de
lo que leyó como negativo. Coste declarado: una séptima ordenanza sería
`ALTER TABLE` más subir `version_esquema`.

### Lo que sigue abierto y no es mío decidir

⚠️ **D-5: no hay copia de seguridad de la base.** El planificador lo señala como
«el riesgo más grande que tiene el trabajo de Miguel». Nadie lo ha resuelto y no
cabía en un pase de esquema. **Sigue abierto.**

---

## 2026-09-02 — Llegaron los PDF reales: la premisa de la FASE 2 deja de ser hipótesis

El dueño aportó cinco archivos. **Bloqueo 2 cerrado.** Están en `pdfs_referencia/`
y **fuera del repositorio**: `git check-ignore -v` los atrapa en la línea 29 del
`.gitignore` (`*.pdf`). Llevan nombres y MRN de personas reales.

**Medido con `pypdf` sobre los archivos, no citado de ningún plan:**

| Archivo | Páginas | FreeText | Ink |
|---|---|---|---|
| `CASP2609_Jonas_Ficticio` | 1 | 13 | 6 |
| `CASP2609_Daniel_Jr._Damian_Dorian_Ejemplo` | 1 | 10 | 5 |
| `PARB2609_Nora_Clara_De_Ensayo_Complete` | 1 | 11 | 4 |
| `SURB2609_Suriname_Group_Complete` | **6** | 55 | 31 |

Dos de los cinco archivos son el mismo documento duplicado (2 396 388 bytes
exactos los dos). Quedan **4 documentos y ~~10 páginas~~ 9 páginas** distintas.

⚠️ **Error del supervisor, corregido el 2026-09-02.** Escribí «10 páginas» cuando
la suma de mi propia tabla es 1+1+1+6 = **9**. Lo cazó el programador al empezar
la FASE 2, verificando la premisa del pase antes de construir — que es
exactamente para lo que sirve esa costumbre.

**Las nueve páginas: `mediabox [0, 0, 612, 792]` y `/Rotate 0`.** Coincide con lo
que el plan decía, y ahora está medido. Total: **89 FreeText y 46 Ink**.

### Dos cosas que el plan no contemplaba

1. **Un PDF puede traer varios formularios.** `SURB2609_Suriname_Group_Complete`
   tiene seis páginas, cada una un formulario. El extractor **recorre páginas**;
   no puede asumir una hoja por archivo. El nombre del archivo tampoco identifica
   un caso: `SURB2609` agrupa seis.
2. **El PDF de referencia del plan, `CASP2609_Zutano_Family.pdf`, NO está entre
   los cinco.** Así que el criterio de aceptación de la FASE 2 —que pedía sacar la
   fecha `2026-09-08`, la unidad `Castries Branch` con número `700001` y tres
   personas— **no se puede comprobar tal como está escrito**. Ese criterio hay que
   rederivarlo midiendo estos archivos.

⚠️ ~~Y por lo mismo: los colores y grosores que `DECISIONES.md` recoge para
distinguir **tachón rojo** de **resaltador verde** venían de ese archivo
ausente.~~ **Medidos el 2026-09-02 sobre los cuatro documentos, en la FASE 2:**

```
familias de /Ink (color, grosor) -> cuantas:
  ((0.8902, 0.0941, 0.1765), 1.65) -> 45     <- tachon
  ((0.4941, 0.7686, 0.0),   16.5)  ->  1     <- resaltador
```

**Trazos sin clasificar: 0 de 46.** El tachón coincide con lo que se suponía. El
resaltador **no**: el canal verde medido es **0.7686**, no 0.765. Manda el número
medido.

Y el archivo «ausente» no lo estaba tanto: `CASP2609_Jonas_Ficticio.pdf` trae la
unidad `Castries Branch - 700001` y un tachón rojo sobre `September 7, 2026` con
un `/FreeText` `8 Sept 2026` al lado — los valores exactos que el criterio
original pedía. Casi con seguridad es el mismo documento renombrado. Lo que no
coincide es «tres personas»: tiene una.

---

## 2026-09-02 — `unidad_numero` admite 6 o 7 dígitos: manda el papel

**Medido en la FASE 2 sobre los PDF reales:** 4 de las 9 páginas traen
`unidad_numero = 7000011`, **siete dígitos**, verificado contra la imagen. La
regla del plan decía seis, y el `CHECK` del esquema **rechazaba un dato
verdadero**: esas cuatro páginas salían con el campo vacío.

Decisión del supervisor: **6 o 7 dígitos**. `CLAUDE.md` §8 lo dice — cuando un
documento y la realidad no coinciden, gana lo medido y el documento se corrige.
Una regla que rechaza formularios buenos no protege nada; solo hace que Miguel
teclee a mano lo que el sistema ya había leído bien.

⚠️ Hay que cambiar el `CHECK` de `casos` en `docs/ARQUITECTURA.md` y la validación
en `datos/validacion.py`. Anotado para la próxima fase que toque esa capa.

### Y una regla que NO funciona, medida

La marca de `captura_manual` —«si más del 60% de los campos vuelven con confianza
bajo 0.6, el formulario está escrito a mano»— **no dispara** en las dos páginas
peor escaneadas. RapidOCR devuelve **0.82 a 0.85 de confianza sobre texto basura**
(`'Wcencegb zcench:'`, `'BEM BRASIL'`). La confianza del OCR no distingue leer
bien de leer basura con aplomo.

El programador no inventó una regla nueva para taparlo: expuso
`anclas_no_encontradas` para que la pantalla de corrección lo vea. **La regla del
60% queda como deuda abierta**: hay que sustituirla por algo que sí mida, y ese
algo todavía no existe.

---

## 2026-09-02 — Un PDF de grupo pierde 11 de 12 personas. Se arregla con la opción A

**El fallo más grave que ha tenido este proyecto**, encontrado la primera vez que
alguien ejecutó el camino completo —importar un PDF real, pasarlo por el OCR y
guardarlo— dentro del `.exe`.

`SURB2609_Suriname_Group_Complete.pdf` tiene 6 páginas con **12 personas
distintas** (comparadas por hash, sin exponerlas). El programa importa **la página
1** y rechaza las otras cinco con «ya estaba en la base y NO se ha tocado».

**No es un reimport: es la primera importación.** El extractor sí recorre las
páginas; la capa de guardado nunca se había probado contra un caso repartido en
varias hojas, porque el camino completo nunca se había ejecutado.

Es el daño exacto que `CLAUDE.md` describe en su primera línea: once personas que
llegan al templo sin que nadie mirara su recomendación.

**Decisión del supervisor: opción A.** Mismo `numero_caso` dentro del **mismo
PDF** ⇒ las personas se añaden al caso existente. Eso distingue «páginas del mismo
caso» de «reimportar un archivo ya procesado», que es lo que P-5 protege y sigue
sin resolverse. Las otras dos opciones —dejar que Miguel teclee once personas a
mano, o preguntárselo caso por caso— cargan sobre él un trabajo que la máquina
puede hacer bien.

⚠️ `ed633b94` **es un duplicado real** de `cb2f18be`, así que las dos situaciones
—mismo caso en varias páginas, y el mismo archivo dos veces— conviven en la misma
carpeta y el arreglo tiene que separarlas.

## 2026-09-02 — `opencv-python-headless` NO ahorra los 110 MB: premisa mía, falsa

Escribí en este documento que cambiar a `opencv-python-headless` quitaría unos
110 MB. **Medido en la FASE 9: es falso.**

`opencv-python-headless==5.0.0.93` **trae el mismo `opencv_videoio_ffmpeg500_64.dll`
en su propio RECORD**, y su `cv2.pyd` pesa 81.87 MiB frente a 82.30. El cambio
ahorra **0.42 MiB**, no 110.

Los 29.45 MiB del códec de vídeo hubo que **excluirlos a mano en `fichas.spec`**.
Resultado real: **216.76 MiB** frente a 244.30 — **27.54 MiB menos, un 11.3%**.

La lección, que es la de siempre en este proyecto: el nombre de un paquete no es
una medición.

## 2026-09-02 — El paquete NO se construye ni se entrega desde OneDrive

**Medido en la FASE 9:** OneDrive convirtió **60 carpetas** del `dist` que estaba
dentro del repositorio en marcadores de posición en la nube (`ReparsePoint` +
`ReadOnly`), y eso rompió la reconstrucción. Peor: una carpeta deshidratada
copiada a un pendrive **puede llegar vacía a la otra máquina**.

El paquete se construye fuera de OneDrive. La entrega vive en
`C:\Users\josem\Fichas-entrega\Fichas`.

⚠️ Queda una copia vieja de `dist\Fichas` dentro de OneDrive, medio borrada.
Conviene eliminarla a mano para que nadie la entregue por error.

---

## 2026-09-02 — Lo que queda decidido, y lo que espera al dueño

Cerrada la ronda de arreglos de la auditoría final. **520 pruebas**, medidas por
el supervisor.

### Decidido: un caso lo lleva una persona, pero avisando, no prohibiendo

Dos compañeros sobre el mismo caso borraban la propuesta del primero sin rastro.
Está arreglado por la mitad que no admite discusión: `guardar_propuesta` **rechaza
y dice quién escribió, cuándo y qué**, en vez de pisar en silencio.

La otra mitad —que el motor impida dos asignaciones vivas— **no se hizo, y con
razón**: el programador midió que esa regla no existe en ningún documento del
proyecto. Poner un índice único habría sido decidir por el dueño. Se siguió el
precedente de P-2, la regla del mes cruzado: **aviso en pantalla, no pared del
motor**.

⚠️ **Consecuencia que hay que saber:** hoy no existe forma de sustituir la
propuesta de un compañero por la de otro. Si la buena resulta ser la segunda,
Miguel se queda sin botón. Se dejó así a propósito —quedarse sin botón es mejor
que perder el dato de una persona real sin enterarse— pero **es funcionalidad que
falta**.

**Le toca al dueño responder dos preguntas que van juntas:** ¿un caso puede
llevarlo más de una persona a la vez? Y si sí, ¿cómo elige Miguel entre dos
propuestas? La recomendación del programador es **un caso, un compañero**, porque
es lo que el resto del sistema ya asume: las cuatro columnas de propuesta son una
por persona. Si el dueño dice que sí, la migración es un índice único parcial
sobre `asignaciones(caso_id) WHERE activa = 1`.

### `onnxruntime` es dependencia directa y `CLAUDE.md` §3 no lo lista

Medido: `rapidocr` 3.9.2 **no lo declara** en sus `Requires`, y `fichas.spec` lo
nombra. Es el motor que ejecuta los `.onnx`. Está en la lista blanca del guardián
de la regla permanente 1 por eso.

⚠️ La tabla de tecnología de `CLAUDE.md` §3 no lo menciona. **Corregirlo es del
dueño**, que es quien escribe ese documento.

### Números que se corrigieron al medirlos de nuevo

- Los falsos positivos del guardián de dependencias eran **103**, no 30, y los
  paquetes **ocho**, no siete.
- Los caminos que dejaban el espejo atrasado eran **cinco**, no tres. Y había un
  sexto defecto que no vio nadie: `pudo_viajar` y `motivo_no_viajo` **nunca
  llegaron a la hoja** desde la versión 6 del esquema, así que el Excel no decía
  quién no pudo viajar ni por qué — justo lo que el reporte de la FASE 8 lleva a
  los jefes.
- El código de salida de `discover` sin `-p` es **5**, no 0. El 0 salía de haber
  pasado la orden por una tubería. Lo escribí aquí sin medirlo; ver `ESTADO.md`.

### La deuda que sigue sin cerrarse, dicha sin adornos

⚠️ El guardián de la regla permanente 1 **no caza la carga dinámica**:
`importlib.import_module("openai")`, `__import__("open"+"ai")` y
`exec("import openai")` pasan limpios; QA lo midió, 3 de 5 mutantes sobreviven.
Se pidió expresamente no perseguirlo, y el programador dijo por qué no hay salida
barata: cazarlos exige inspeccionar llamadas con cadenas dentro, y eso empieza a
dar falsos positivos sobre código legítimo.

**Lo que protege esa regla hoy no es el guardián: es que nadie escriba esa llamada
a propósito**, más las mediciones de QA sobre el producto —0 intentos de conexión
con la red cortada, 0 runtimes de modelo de lenguaje entre 147 paquetes, y los
únicos tres `.onnx` son los del OCR.

---

## 2026-09-02 — Para qué existe esto, dicho por el dueño

Se escribe aquí porque es la vara con la que se mide todo lo demás, y hasta hoy
solo estaba implícita:

> «yo debo poder subir al programa **cualquier** PDF, porque ese es el fin.
> Manejaré **cientos** de PDF y **no quiero transcribir a mano**; para eso es el
> programa: para asignar y manejar esos PDF. Y el reporte para los jefes debe ser
> funcional.»

**Consecuencia sobre qué significa «terminado».** No es que el programa importe
sin caerse: es que **le ahorre teclear**. Un documento que entra vacío y hay que
rellenar a mano es un documento que el programa no ha resuelto, aunque no dé
error. La medición que vale es cuántos campos salen bien de **sus** PDF, no de los
de referencia.

⚠️ **Error de juicio del supervisor, reconocido.** Di el proyecto por funcionando
midiendo sobre cuatro documentos que llevaban el número de caso escrito como
anotación `/FreeText` dentro del PDF. Los del dueño son escaneados sin capa de
texto. La diferencia estaba escrita en este mismo documento desde la FASE 2 —el
archivo de referencia del plan ni siquiera existía— y aun así di el conjunto por
representativo. No lo era.

## 2026-09-02 — El OCR SÍ lee el número de caso sin anotaciones. Y el nombre del archivo miente

**Medido por el supervisor**, rasterizando `59d87aad-CASP2609_Jonas_Ficticio.pdf` y
pasándole el OCR **sin tocar la capa de anotaciones**:

```
LINEAS LEIDAS POR EL OCR: 94
LINEAS QUE PASAN EL PATRON DE NUMERO DE CASO: 1
   ACIERTO: 'CASD2609'   conf 0.99995
```

Y recortando esa misma caja de la imagen y mirándola: el papel dice **`CASD2609`**,
nítido, en texto impreso.

**Dos conclusiones, y las dos importan:**

1. **El camino sin anotaciones funciona.** El OCR encuentra el número de caso en
   un escaneo puro, con confianza 1.0. Lo que falla en los PDF del dueño es algo
   propio de esos archivos —calidad, giro, otro formulario, foto en vez de
   escaneo— y **no se puede saber cuál sin verlos**.
2. **~~`CASD2609` contra `CASP2609` queda como duda abierta de la FASE 2~~
   Resuelta:** el OCR acierta y **el nombre del archivo es el que está mal**. La
   entrega de la FASE 2 lo había anotado como «el sistema entrega lo que dice el
   papel y no lo arregla», sin comprobar cuál de los dos decía la verdad. Ahora
   está comprobado.

⚠️ **Y deja a la vista un riesgo que sigue abierto:** una letra mal leída pasa el
patrón de validación —cuatro mayúsculas y cuatro dígitos— sin que nada la frene.
El programa no fallaría: **acertaría mal**, creando un caso que no existe y
rompiendo la reconciliación por `numero_caso` + `mrn` sin que nadie lo note. Aquí
la letra era correcta; el día que no lo sea, no hay quien lo cace.

---

## 2026-09-03 — Por qué el programa no leía nada: el formulario está en español y las anclas en inglés

El dueño entregó dos de sus PDF reales. **Medido por el supervisor**, y es la
explicación completa.

### Sus PDF no son escaneos

```
$ pypdf sobre BARC2608_Fulano_Family_limpio.pdf
paginas: 1 | caja [0,0,612,792] | rot 0 | anotaciones 87
texto extraible 2734 chars | imagenes []
```

**Cero imágenes. 2 734 caracteres de texto extraíble.** Las 87 anotaciones son
todas `/Widget` —campos de formulario rellenable— y **no hay `/AcroForm`** en la
raíz, así que `get_fields()` devuelve 0: el formulario se «limpió» y los campos
quedaron huérfanos. Ni un `/FreeText`, ni un `/Ink`.

⚠️ Toda la hipótesis anterior —«sus PDF son escaneos sin capa de texto»— era
**falsa**. La escribí yo en este documento y era exactamente al revés.

### El OCR funciona. No era el OCR

```
lineas leidas: 87
lineas que pasan el patron de numero de caso: 1 ['BARC2608']
```

Y `'BARC2608' in extract_text()` → **True**. El número está hasta en el texto del
PDF, sin necesidad de rasterizar nada.

### Y aun así, la extracción devuelve nada

```
$ extraer_documento sobre los dos PDF
numero_caso -> None · fecha_viaje -> None · unidad_numero -> None
unidad_nombre -> None · personas: 0 · captura_manual: True
```

### La causa, leída en el código

```
extraccion/formulario.py:41  ETIQUETA_DE_FECHA_DE_VIAJE = "Date traveling to the temple"
extraccion/formulario.py:42  ETIQUETA_DE_UNIDAD = "Ward/Branch Name and Unit Number"
extraccion/personas.py:21    ETIQUETA_DE_NOMBRES = "Full Name(s)"
extraccion/personas.py:22    ETIQUETA_DE_MRN = "Membership Record Number"
```

**Las anclas están en inglés y su formulario está en español.** Su papel dice
«Fecha de viaje al templo», «Nombre(s) de pila», «Número de cédula de miembro»,
«Nombre del templo». No casa ni una.

Sin anclas no se localiza ningún campo; sin campos, `_es_captura_manual` da True
y `_vaciar_el_formulario` **borra hasta el número de caso que el OCR había leído
con confianza 1.0**. Eso es, literalmente, el «Se guardaron 0 casos de 1 página»
que vio el dueño.

Las seis ordenanzas de su formulario coinciden **palabra por palabra** con las que
`CLAUDE.md` enumera en español: recibir ordenanzas propias, presenciar ordenanza
de sellamiento, traductor, investidura, sellamiento de esposa a esposo,
sellamiento de hijo a padres. El proyecto tenía la lista correcta en su documento
de reglas y las anclas en el otro idioma en el código.

### La lección, y es del supervisor

Todo el proyecto se construyó sobre **cuatro formularios en inglés** que llegaron
como «los PDF de referencia». Nadie preguntó nunca si eran los que el dueño usa.
No lo eran: son la versión inglesa del mismo papel. Di el conjunto por
representativo dos veces —primero al cerrar la FASE 2, luego al declarar el
proyecto terminado— y las dos veces me equivoqué por la misma razón: **medir
mucho sobre la muestra equivocada no es medir**.

---

## 2026-09-03 — El proyecto antiguo: qué se trae, qué no, y un hallazgo que bloquea las ordenanzas

El dueño entregó `C:\Users\josem\Desktop\proyecto\pdf-a-excel\`, una implementación
anterior suya que **sí le leía los documentos**. Analizada por el planificador.

### Es el mismo formulario, no otro

Del pie del formulario oficial en blanco que ese proyecto guarda, **citado literal**:

> «Versión: 2/23. Traducción de General Temple Patron Assistance Fund Request and
> Approval Form. Spanish. PD80014016 002»

Es la **traducción española de la misma hoja inglesa**. Sus seis columnas de
ordenanza coinciden una por una y en el mismo orden con las del proyecto nuevo.
Nadie preguntó nunca si los cuatro «PDF de referencia» eran el papel que el dueño
usa; son su versión inglesa.

### Lo que se trae, por orden de valor

1. **Las 14 anclas españolas** de `perfiles/barc2608.json`, con su dirección de
   lectura. Ahí está el equivalente que faltaba de `Ward/Branch Name and Unit
   Number`: **«Nombre y número de unidad del barrio / rama»**.
2. **El buscador de ancla tolerante** (`nucleo/extraccion.py:76-99`): normaliza sin
   acentos, prueba subcadena, y cae a `difflib.SequenceMatcher` con umbral 0.60.
   Su comentario trae el caso real que lo justifica — *«"Nombre del templo" se lee
   "Norrore del tempio"»*. Solo biblioteca estándar.
3. **Su orden de estrategias:** patrón → ancla → zona fija, y la zona solo como
   último recurso. Citado de su cabecera: *«las zonas fijas se rompen en silencio
   cuando el papel se corre»*.
4. **`nucleo/grupos.py`**: coteja el papel contra un catálogo de casos y **avisa sin
   corregir**. Biblioteca estándar pura.
5. **`salida/asignacion.py`**: el ciclo de ida y vuelta con el agente, una fila por
   persona, columna clave visible, y «blanco ≠ no».

### Lo que NO se trae, y por qué

- **`servidor.py` + `web/`, 2 797 líneas.** Es una aplicación web con
  `ThreadingHTTPServer` en el puerto 8760. Rompe la regla permanente 2. Y su
  propio `LEEME.md` dice: *«No hay contraseña. Quien alcance esa dirección entra y
  ve los nombres y las cédulas.»* Eso no entra aquí.
- **`construir_exe.py`**, que usa `--onefile` y `--console`. El nuevo es `--onedir`
  por los tres `.onnx`, y sin consola.
- **`almacen.py`**: sería un segundo esquema compitiendo con `datos/`.
- **`nucleo/saneo.py` entero**: usa `warpAffine`, `minAreaRect`, `threshold`,
  `divide`. La tabla de tecnología dice «`opencv` (solo `medianBlur`)», así que
  traerlo **amplía una regla** y eso lo decide el dueño.
- **`informe.py` y `rellenar.py`**: valen, pero cuestan `reportlab` y `pdfplumber`,
  que no están en el `requirements.txt` del nuevo.

### ⚠️ El hallazgo que bloquea las ordenanzas, y es del dueño resolverlo

El planificador midió las 36 casillas de los dos PDF del dueño con el lector del
proyecto antiguo:

```
rejilla: cols=7 rens=7   casillas leidas: 36
marcadas=0  vacias=36  dudosas=0
tinta: min=0.0000  max=0.0000   en las 36, en los dos archivos
```

Y contrastó contra la verdad estructural del PDF: **45 botones `/Btn` con `/AS`
= `/Off`**, que es el estado *reiniciado*; **2 de 87 widgets con valor**; ni un
píxel dibujado dentro de las celdas.

> **La información de ordenanzas no existe en ninguna capa de esos archivos.** El
> proceso que produce el sufijo `_limpio` **se llevó por delante el estado de las
> casillas**.

**Pregunta para el dueño, y es la más rentable del proyecto:** ¿existe el PDF
**antes** de «limpiar»? Si existe, las seis ordenanzas se leen **exactas** con
`/AS != /Off` — sin umbral, sin calibrar, deterministas. Eso resuelve del todo lo
que hoy está desactivado.

De paso, una validación que sí sirve: el localizador de rejilla del proyecto
antiguo **acierta sobre el papel real** con un error de **0.002 del ancho de
página** frente a los rectángulos verdaderos de los `/Widget`.

### Lo que el análisis NO pudo verificar

⚠️ **Los umbrales `LLENO=0.030` y `VACIO=0.008` del proyecto antiguo NO están
calibrados.** El comentario del código dice «entre el 4 y el 15%», su `LEEME.md` y
sus pruebas dicen **20%**, se contradicen, y no hay ningún registro de la medición
que los produjo. Sus tres pruebas se saltan solas si no hay un PDF real al lado.
**Traer esos números como si estuvieran medidos sería importar una suposición con
aspecto de dato.** El proyecto nuevo tenía razón en no elegir un umbral a ojo.

⚠️ Y su aviso final, que suscribo: **los dos PDF del dueño son del mismo caso y
los dos están `_limpio`**. No se sabe si son representativos. Es exactamente la
trampa en la que caí yo con los cuatro formularios en inglés.

---

## 2026-09-03 — El proyecto viejo, medido por fin por el supervisor: lee más que el nuevo

El dueño preguntó «¿verificaste el proyecto pasado?». La respuesta honesta era
**no**: solo lo había inventariado, y lo único medido sobre él era del
planificador. Se mide ahora.

**Su suite pasa.** `python -m unittest pruebas` con el Python del sistema (que sí
tiene `pdfplumber` 0.11.10; el venv del nuevo no lo tiene y por eso ahí no
arranca):

```
Ran 93 tests in 80.115s
OK (skipped=1)
```

**Lee el PDF del dueño, y lee más que el nuevo.** `python servidor.py --comprobar
C:/Users/josem/Desktop/BARC2608_Fulano_Family_limpio.pdf`, salida literal:

```
[ok ] El PDF se lee de punta a punta — caso BARC2608 · 23 campos leídos de 26 · 2 por revisar
[ok ] El escaneo se ve en pantalla — 338 KB de imagen
TODO BIEN
```

El nuevo lee 10 de 12 sobre el mismo PDF. La diferencia no es de calidad de
lectura: **es de modelo**. Su perfil `perfiles/barc2608.json` declara **26
campos** por formulario, medido leyendo el JSON:

> Caso, Solicitud, Fecha de la solicitud, Solicitante, Participantes, Cédulas,
> Templo, Fecha de la cita, Fecha de viaje, Fecha de regreso, Moneda, Transporte,
> Alimentos, Contribución del miembro, Garments hombre, Garments mujer, Ropa
> hombre, Ropa mujer, Total costs, Amount to unit, Barrio o rama, No. barrio,
> Estaca, No. estaca, Aprobación de área, Número de referencia.

El nuevo modela 12: número de caso, fecha de viaje, unidad y su número, nombre,
MRN y las seis ordenanzas. ~~**Los otros 14 no existen en su esquema.** Ese es el
hueco que el dueño llama «las funciones del viejo».~~

**Corregido el mismo día, por orden del dueño:** «solo debe leer los campos que yo
necesito». **No se construyen los 26.** Los campos que necesita son los que ya
nombró: nombre, barrio (unidad), cédula de miembro (MRN), fecha de viaje, número
de caso, más país y templo. Lo que se trae del viejo es la **forma de leer**
—patrón antes que ancla, ancla tolerante, un perfil por modelo de formulario—,
no su lista de campos.

### Tres requisitos nuevos del dueño, en sus palabras

1. «La funciones que tiene ese es la funciones que quiero que se le agreguen al
   nuevo sistema.»
2. «Hay formulario que estarán en inglés, en francés... pero **el idioma no debe
   importar**.»
3. «Quiero poder ver el documento completo.»

### Lo medido sobre el 3: el visor del documento completo NO está en el código

`grep -nE 'zoom|Control-1|VisorDelDocumento|pagina_entera' interfaz/*.py` → 0
coincidencias. `PaginasDelPdf` existe (`interfaz/escaneo.py:76`) pero solo sirve
las tiras por campo. La pantalla partida con el documento entero está
**especificada** en `mockups/mockup-correccion-partida.html` y **no construida**.
Cuando el programador anterior dijo «punto 4 ya cubierto», se refería al mockup,
no al código; yo lo di por hecho y no lo era.

### Sobre el 2: el viejo ya resolvió la forma, no el francés

El viejo localiza campos por **perfiles** (`perfiles/*.json`): un archivo de
anclas por modelo de formulario. Eso es exactamente la estructura que hace que
el idioma no importe — un perfil por idioma, mismo motor. Hoy solo existe el
perfil español. **Nadie tiene las etiquetas francesas**; el formulario oficial
francés (misma familia PD80014016) no está en ningún sitio del material. Es un
hueco declarado, no se inventa.

---

## 2026-09-03 — El Excel del agente y el informe se hacen como los del viejo

**Dicho por el dueño, literal:** «El Excel que crea el viejo para los agentes es
**perfecto**, ese es el que quiero para este, cómo lo crea, y el reporte para los
jefes también.»

**Abierto por el supervisor antes de encargarlo** (`sed` sobre
`salida/asignacion.py` y `salida/informe.py` del viejo), no dado por sabido:

- El Excel del agente es **una sola hoja**, «Por verificar», con cinco filas de
  cabecera antes de la tabla —título, templo y salida, **la fecha límite sola y
  en rojo**, el agente, y una instrucción— y luego: Caso · Fecha de solicitud ·
  Fecha de viaje · Barrio o rama · Estaca o distrito · Hermano(a) que viaja ·
  Cédula de miembro · A qué va · **seis pasos** con menú Sí/No · «¿Llamó al
  líder?» · **clave visible**. Su comentario, citado: *«un dato oculto es un dato
  que alguien borra sin saber lo que hace»*.
- Los seis **pasos** («1. Preparación» … «6. Listo para el templo») **no son las
  seis ordenanzas**: son los pasos del sistema del líder. El nuevo hoy devuelve un
  solo `estado_recomendacion` con dos valores. Replicar el viejo cambia el modelo
  de lo que el agente devuelve; se hace con migración.
- El informe abre por «**N** de las M personas que ya viajaron lo hicieron sin
  que se verificara su preparación», cuatro cifras grandes **sin porcentajes**, y
  **sin dinero** a propósito: *«el presupuesto lo lleva otro departamento»*.

**Decisión del supervisor sobre `reportlab`.** El viejo dibuja el informe con
`reportlab`, que no está en el nuevo, y el escritor de PDF a mano del nuevo **no
sabe pintar** (medido por el diseñador sobre `reportes/formato_pdf.py`). Se
autoriza añadirlo **si es la vía más corta** a que el informe salga como el
viejo, con la condición de medir cuánto engorda el paquete y decirlo. Lo que no
vale es un informe distinto por ahorrarse la dependencia.

⚠️ Corre en paralelo con el pase de las firmas, sobre archivos distintos
(`paquete/`, `reportes/` frente a `datos/procedencia.py`, `interfaz/correccion.py`).
El `.exe` se reconstruye **una sola vez** cuando cierren los dos.

---

## 2026-09-03 — Cambiar o borrar un campo firmado le retira la firma

**El hallazgo, de la segunda auditoría de QA, medido por él:** un caso a 36 de 36
al que se le borraba la fecha de viaje seguía en «36 de 36», dejaba de salir en
pendientes, y `casos_que_viajan_pronto` pasaba de 1 a 0. Firmado, sin fecha, e
invisible en los tres avisos a la vez.

**Decisión: la firma cae con el cambio**, no se impide editar lo firmado. Deshacer
es por caso, no por campo: con la otra opción, arreglar una letra de una fecha
obligaría a tirar las 36 firmas y volver a ponerlas, y un programa que castiga
corregir acaba con datos malos sin corregir. Ninguna de las dos rompe la regla
permanente 5; solo una deja abierto el camino de la corrección.

**Dónde vive, y por qué ahí.** No en `guardar_procedencia_de_campo`, que es donde
apuntaba el hallazgo: hacerlo ahí habría decidido de paso **P-5** (qué pasa con
la verificación al reprocesar un PDF), que sigue abierta y es del dueño. Vive en
`datos/repositorio.py`, en las tres funciones que **escriben el valor**, así vale
igual venga el cambio de la pantalla de corrección, del Excel de los compañeros
o de una pantalla futura. P-5 queda intacta: el UPSERT sigue sin tocar
`verificado`.

**Medido por el supervisor, no solo por su informe:** `prueba_lo_firmado_que_cambia`
→ `Ran 18 tests, OK`. Según su informe —y NO lo he repetido yo—: 736 pruebas en
verde, contador 36→35 al borrar la fecha, `verificado/verificado_por/verificado_en`
= `0/None/None`, y el caso vuelve a pendientes; comprobado además cargando los
`datos.*` del PYZ del `.exe`.

⚠️ **Lo que sigue abierto, dicho por él:** `casos_que_viajan_pronto` y
`casos_en_riesgo` siguen dando 0 cuando falta la fecha, porque un caso sin fecha
no se puede situar en el calendario. Quien lo recupera es la lista de pendientes.
Si el dueño quiere que además salga en la franja roja de inicio, es una consulta
nueva y una decisión suya.

---

## 2026-09-03 — El Excel del agente y el informe, hechos; `reportlab` no entra

**Medido por el supervisor:** `prueba_hoja_del_agente` y
`prueba_informe_para_los_jefes` en verde (salida en el commit). **Según el
informe del programador, NO repetido por mí:** 861 pruebas OK; **18 de 18**
propiedades estructurales idénticas entre el Excel nuevo y el del viejo abriendo
los dos con `openpyxl` —hoja, congelado en A7, 16 títulos y anchos, tinta
`16233A`, fondo `FFF6DC` en las 7 columnas del agente, clave visible en gris 8,
7 menús Sí/No, fecha límite en rojo `A62E24`—; y el informe con **14 de 15**
rasgos iguales al del viejo.

**Decisión: `reportlab` NO se añade.** Lo que el informe del viejo necesita de un
motor de dibujo son dos operadores de PDF crudo (`re f`, `l S`) más color de
texto: cuatro líneas. `reportlab` costaba **8,1 MiB / 350 archivos** sobre 217.
Se escribió a mano en `reportes/formato.py`.

**Los seis pasos van a la base: migración 9.** Siete columnas en `personas`
(`paso_*` ×6 y `llamo_al_lider`), NULL/0/1, parte de la propuesta que firma
`propuesto_por`. Medida sobre una base v1 con datos: migra a 9, la fila queda
intacta, idempotente.

### Lo que devolvió sin decidir, y queda para el dueño

- **«Completa» no existe como valor.** Seis pasos en «Sí» dejan
  `estado_propuesto = NULL`; inventar la palabra violaría P-1. Es una línea de
  `paquete/reconciliacion.py` el día que el dueño la diga.
- **La hoja del viejo no tiene columna de nota**, así que `nota_companero` ya no
  llega del agente. Es lo que el dueño llamó perfecto, y se pierde ese canal.
- Dos columnas del viejo —«Estaca o distrito» y «Fecha de solicitud»— salen
  «no consta»: el nuevo no las extrae, por la orden del dueño de leer solo lo que
  necesita. «País» y «Templo» también, hasta que exista lo que el planificador
  especificó.

⚠️ **A medias, y coordinado:** los seis pasos llegan a la base y la pantalla de
corrección **no los enseña**. Ese archivo lo tiene el pase del visor, y se le
manda ahí. Y `interfaz/persona.py` la tocaron **los dos** programadores; se
commitea cuando cierre el segundo.

---

## 2026-09-03 — El PDF entero al lado de los campos, construido

Pedido dos veces por el dueño y medido antes como inexistente (`grep` → 0).
Construido según `mockups/mockup-correccion-partida.html`: `interfaz/visor.py`,
`interfaz/encuadre.py` (la aritmética del visor sin tkinter, 32 pruebas en 3 ms),
y `ttk.PanedWindow` en `interfaz/correccion.py` sin reescribirlo.

**Según el informe del programador, NO repetido por mí salvo la suite:** reparto
real en 1100×720 = visor **560** + campos **495**, no desborda; la banda enfocada
se ve a **536 px** —el número exacto del mockup— contra 380 de la tira; 58
paradas de Tab, ni una más; **6 → 1** rasterizaciones al abrir un caso de seis
hojas. Cada prueba nueva pasó su control negativo.

**Decisión suya que se acepta:** `Ctrl+0` ya era «Ninguna ordenanza marcada»,
así que el zoom va en **`Ctrl+1` página entera** y **`Ctrl+2` banda**. Si el
dueño quiere el mockup literal, lo que se mueve es el atajo de las ordenanzas.

**Fuera, y dicho:** F6 entre regiones (contradice el `takefocus=0` que el propio
mockup exige), Alt+←/→ entre casos (no existe esa navegación), el divisor no
recuerda su posición, y países/templos, que necesitan esquema. La columna de
campos mide 495 px y no los 532 que calculaba el mockup: sobran 17 px, no 54.

**No verificado:** con un escaneo real torcido; dentro del `.exe`; en pantallas
con escalado de Windows.

---

## 2026-09-03 — Los seis pasos se ven; y el `.exe` único con todo lo del día

**Medido por el supervisor:** la suite entera con el árbol quieto; la salida
literal va en el commit. **Según el informe del programador, NO repetido por
mí:** 874 pruebas, 0 omitidas; 92 de 92 módulos dentro del `.exe` con 0 avisos;
218 MB; arranque medio 1,49 s; y la vuelta entera **dentro del ejecutable** con
un PDF real del dueño —migración 8→9 sobre su base, importar, documento entero
con la banda a 536 px, generar paquete con los seis pasos, cargarlo devuelto,
verlos firmados en pantalla, informe en `.xlsx` de 7 hojas y `.pdf` de 3—.

Hallazgo suyo de paso: **la base real del dueño estaba en esquema 8**; la
migración 9 nunca había corrido sobre datos reales hasta esta comprobación. Se
respaldó y se devolvió byte a byte, esquema de vuelta en 8.

### Dos decisiones suyas que se aceptan

1. **Los pasos se ven, no se editan.** Los firma un compañero y el esquema
   guarda **una** propuesta por persona: dejarlos cambiar desde la pantalla
   borraría el trabajo de otro sin avisar, que es el hallazgo ALTO ya cerrado.
   ⚠️ **Para el dueño:** si Miguel debe poder corregirlos, hay que decidir quién
   firma esa corrección.
2. **Sin propuesta no se dibuja la sección.** Siete «sin contestar» por persona
   esconderían las dos que sí traen algo.

Y dejó fuera a propósito un veredicto calculado de «lista para el templo»: un
dictamen en pantalla se lee como que el sistema decidió, y roza la regla 5.

### Anotado y no tocado

- Una persona **sin MRN nunca podrá recibir propuesta**: el par `caso+MRN` no
  resuelve. Es previo, no lo introdujo él.
- ⚠️ **Quedan en disco datos de personas reales fuera del repositorio:**
  `C:\Users\josem\Fichas-work\paquete-prueba\` y capturas del scratchpad. No se
  commitean; borrarlos es del dueño.

---

## 2026-09-03 — Auditoría final de QA sobre `807bac3`: VUELVE AL AGENTE, y las decisiones que salen de ella

**Lo que aguanta, medido por QA (no repetido por el supervisor):** 874/874
pruebas; el hallazgo anterior cerrado con **0 fallos de 11 escenarios** y prueba
destructiva del detector (11 de 11 al neutralizar el arreglo); regla 5: 0
verificados de 12 tras importar sus PDF y 0 de 15 tras la vuelta del Excel;
regla 1: 0 imports de red o LLM en los 7 paquetes; Excel del agente **16/16**
idéntico al viejo en títulos, anchos y menús, y con hoja protegida y MRN en
texto, que el viejo no tenía; pantalla partida 560/510 px con la hoja correcta
13 de 13.

### CRÍTICO — Dos familias se funden en un caso con una sola fecha de viaje

QA unió los dos PDF reales del dueño en **un documento de dos hojas** —que es lo
que produce el escáner de lote— y lo importó:

```
Se guardó 1 caso de 2 páginas, con 5 personas en total.
caso num=BARC2608 fecha=2026-08-25   personas por hoja: {1: 4, 2: 1}
```

La hoja 2 leyó `2026-08-26`; el caso se quedó con `2026-08-25`. **La persona de
la hoja 2 queda con la fecha de viaje de otra familia**, y no hay marcha atrás:
ninguna función mueve una persona de caso ni borra. Y es **asimétrico**: como dos
archivos sueltos el segundo se rechaza entero; como un documento, se funden en
silencio. El mismo papel, dos comportamientos contrarios según cómo se metió en
el escáner.

**Decisión del supervisor, sin abrir P-5.** Una hoja se une a un caso de la
misma tanda **solo si no contradice** sus campos de caso: misma fecha de viaje y
misma unidad, o sin leer. Si contradice, **no se une**: va a «Lo que no entró…»
con motivo `campo_discrepante`, igual que la vía del archivo suelto, con todo lo
leído dentro. Los grupos reales siguen uniéndose —QA midió que las hojas de los
dos grupos de referencia no discrepan en ninguna— y dos familias con el mismo
número dejan de fundirse. Las dos vías pasan a hacer lo mismo. P-5 (reimportar
el mismo archivo) sigue exactamente donde estaba.

### ALTO — Una persona sin MRN pierde el trabajo del compañero

Ida y vuelta con seis perfiles: los cinco con MRN vuelven con 7/7 pasos; **la que
no tiene MRN vuelve con 0/7**. Y **su PDF real trae 1 de 4 sin MRN**. El compañero
contesta y al volver se descarta, sin aviso al generar y sin lista persistente.

**Decisión:** al generar el paquete se **avisa** nombrando a quien no tiene MRN;
la lista de descartados de la vuelta **se guarda** (renglón, como los ilegibles),
no se pierde al cerrar. Teclear los pasos a mano exige decidir quién firma esa
corrección: **sigue siendo del dueño**, como ya estaba dicho.

### ALTO — Rótulos en inglés sobre formularios en español

Sondeada la ventana real: `Case Number`, `Date traveling to the temple`,
`Full Name(s)`, `Membership Record Number`, `Ward/Branch Name and Unit Number`.
**0 de 5 aparecen en su papel.** `extraccion/etiquetas.py` ya tiene las formas
españolas de 4 de los 5 e `interfaz/correccion.py:88-91` codifica solo la
inglesa. Es el mismo error de juicio del día, sin corregir en la interfaz.

**Decisión:** la interfaz habla español **siempre**, sea cual sea el idioma del
papel (regla 4). El rótulo de cada campo es el nombre español del proyecto; la
etiqueta que se encontró en el papel puede ir debajo, en pequeño, como pista.

### MEDIO — Lo que le falta al informe y al Excel frente al viejo

- Informe: faltan **«Unidades con preparaciones sin completar»** y **«El equipo»**
  (agente · personas a su cargo · verificadas · sin mirar). Los datos existen.
  Se añaden. «De qué país viajan» espera ADR-0002.
- Excel del agente: la cabecera A1/A2 pierde el nombre del grupo y **el templo**.
  El templo **está impreso en el papel** («Nombre del templo») y hoy se descarta.
  **Decisión:** se lee del papel a `casos.templo_nombre`, texto, sin catálogo, con
  migración; la cabecera lo pinta. El catálogo con color espera al dueño.
- «Verificado» significa dos cosas: en la pantalla, la firma de Miguel; en el
  informe, los seis pasos en «Sí». **Decisión:** el informe deja de decir
  «verificada» y dice **«con la preparación completa»** / «sin la preparación
  completa».

### BAJO, y uno se hace

- El `.exe` **no admite otra carpeta de datos**, y por eso QA no pudo conducirlo
  de punta a punta sin escribir en la base real. **Se añade
  `--carpeta-de-datos`**: es lo que convierte «no lo pude probar» en «probado».
- `generar_reporte` revienta con `TypeError` si `generado_en` no es texto: se
  valida con mensaje.
- `requests`/`urllib3`/`certifi` viajan en el paquete arrastrados por rapidocr,
  con 0 imports en el código. Se queda declarado.

---

## 2026-09-03 — Del viejo se conservan las FUNCIONES, no el aspecto

**Dicho por el dueño, literal:** «es perfecto en lo que quería, las funciones;
no perfecto de hecho, el que estás haciendo es mejor que el otro, pero las
funciones eran lo que me gustaba».

**Consecuencia:** el criterio «idéntico al viejo» se relee. Lo que se conserva es
**lo que hace** el Excel del agente y el informe —una fila por persona, los seis
pasos con menú Sí/No, «¿Llamó al líder?», la clave visible, la fecha límite sola
y en rojo, el templo y la salida en la cabecera, la vuelta que cae en la persona
correcta y avisa de lo que no entiende; el informe que abre por cuántas personas
viajaron sin la preparación completa, sin dinero y sin porcentajes, con las
unidades y el equipo—. **No** se conserva el aspecto byte a byte.

Las dos mejoras que QA midió en el nuevo y el viejo no tenía **se quedan**: hoja
protegida (viejo `False` → nuevo `True`) y MRN y clave con formato texto, que
evita que Excel se coma los ceros de delante. Que nadie las quite para «parecerse
más» al viejo.

---

## 2026-09-03 — Cerrados los hallazgos de la auditoría final; lo que decidió el programador

**Medido por el supervisor:** la suite entera con el árbol quieto; la salida
literal va en el commit. **Según el informe del programador, NO repetido por
mí:** los dos PDF reales unidos en un documento dan ahora **1 caso · 4 personas
· 2026-08-25** más **un renglón `campo_discrepante`** por la hoja 2 — antes, 1
caso con 5 personas y una fecha ajena—; el grupo real de seis hojas sigue dando
**1 caso · 12 personas · 0 discrepancias**; el aviso nombra a cada persona sin
MRN y los descartes de la vuelta van a la tabla `filas_descartadas` (migración
11), con prueba destructiva —7 de 11 en rojo al neutralizar los arreglos—;
ningún rótulo inglés, con una prueba que recorre la ventana montada; «Unidades»
y «El equipo» en el informe; `Templo: … · Sale el …` en la cabecera del Excel;
`Fichas.exe --carpeta-de-datos X` escribe en X con esquema 11 y la base real
intacta. 932 pruebas, 218 MB.

### Decisiones suyas que se aceptan, con su coste dicho

1. **`casos.templo_nombre` se guarda sin fila de procedencia y sin campo en la
   pantalla.** Con procedencia habría que poder firmarlo, y un campo firmable que
   no se dibuja dejaría «Todo correcto» bloqueado en todos los casos. **Coste:**
   un templo mal leído no se corrige a mano; medido que pasa — una hoja leyó
   `Caracas-Venezueia` y se queda así, que es lo correcto por la regla 1 pero
   deja el dato malo sin arreglo. ⚠️ Pendiente: campo de templo editable, cuando
   exista el catálogo (ADR-0002).
2. **La hoja que contradice no entra** (sale de `MOTIVOS_EN_QUE_LA_PAGINA_SI_ENTRO`)
   y su renglón va con `caso_id` nulo, igual que la vía del archivo suelto.
3. En el informe cambia «verificada» solo en lo de una persona (preparación); las
   métricas del final siguen diciendo «verificados» porque ahí cuentan la firma
   de Miguel. Una prueba por cada mitad.
4. Botón «Descartes guardados…» en la pantalla de asignación; sin él la tabla
   existiría y nadie la vería.
5. `--carpeta-de-datos` sin ruta detrás **levanta**, no cae a la carpeta de
   siempre.

### Lo que dejó sin cubrir, dicho por él

- **No condujo la importación dentro del `.exe`**: el empaquetado solo importa por
  un diálogo de archivo. Midió que el mismo código está en el PYZ y que las
  migraciones corren hasta la 11. **La punta a punta dentro del `.exe` queda
  para QA**, que ahora sí puede con `--carpeta-de-datos`.
- `filas_descartadas` no tiene hoja en el espejo; `documentos_ilegibles` sí.
- `docs/ARQUITECTURA.md` no declara `casos.templo_nombre` ni `filas_descartadas`:
  del planificador.

---

## 2026-09-03 — `docs/ARQUITECTURA.md` al día con el código, y P-9 ya estaba contestada

**Según el informe del planificador, NO repetido por el supervisor:** el esquema
se construyó con `aplicar_esquema` + migraciones sobre una base temporal y se
leyó con `PRAGMA table_info`: **9 tablas, 96 columnas en el código, 96 en el
documento, 0 diferencias**, orden incluido; tabla de versiones 1 a 11 sin saltos.
Corrigió **26 divergencias**, no las 7 que yo le había listado: faltaban enteras
`documentos_ilegibles` y `filas_descartadas`, dos índices, y `casos.numero_caso`
seguía declarado NO nulo. Un tropiezo suyo que conviene saber: con
`sqlite3.connect` a secas la migración 2 revienta; hay que entrar por
`datos.conexion.abrir_conexion`.

**P-9 la da por abierta y no lo está.** Pedía comprobar si los dos PDF llamados
`CASP2609` llevan el mismo número por dentro. **El supervisor lo midió el
2026-09-02 con el OCR**: `CASP2609_Jonas_Ficticio.pdf` dice **`CASD2609`** en el
papel y `CASP2609_Daniel_Jr...` dice **`CASP2609`**. Son casos distintos; el que
miente es el nombre del archivo. Queda cerrada aquí; corregir §8 del documento es
del planificador.

**P-11, medida por él y devuelta:** dos compañeros distintos con asignación viva
sobre el mismo caso → **aceptado**, `COUNT = 2`. El índice es único sobre el par,
no sobre el caso. La regla «un caso, un compañero» sigue sin escribir: es del
dueño.

**Dos preguntas nuevas suyas, sin rellenar:** P-13 (los siete pasos se ven y no se
editan; si Miguel debe corregirlos, ¿quién firma?) y P-14 (`documentos_ilegibles`
no tiene unicidad: reprocesar duplica renglones, hermana de P-5).

---

## 2026-09-03 — El número de caso deja de ser la identidad del caso

**Lo que enseñó el dueño, en dos capturas de la PC del trabajo** (ruta
`C:\Users\migueljoseortiz\Desktop\Proyecto\Documentos GTPD\Septiembre\...`):
importó diez documentos y el programa contestó «**6 caso ya existente**». Un
renglón, literal: *«El caso BALC2609 ya estaba en la base con 1 persona(s)…
Esta página traía 1 persona(s)… MRN en común: 0… Puede ser otra familia de la
misma unidad y el mismo mes, y entonces esta página se quedó fuera entera.»*

**Era otra familia.** En su carpeta real, **muchos documentos distintos
comparten el número de caso**, porque el número es unidad + año + mes. Lo
llevaba escrito como «posible por construcción, no observado en la muestra»
desde el día 2; con diez PDF suyos ya es lo normal. **El diseño estaba mal:**
`UNIQUE(numero_caso)` (`datos/esquema.py:65`) y el rechazo de
`importacion/guardado.py:468` tratan el número como identidad, y no lo es.

Dicho por el dueño: *«los casos muchas veces tendrán el mismo documento, y
todos esos documentos tienen MRN… cuando un documento no tenga información debe
presentarse completo al lado para yo rellenar lo que el algoritmo no pudo.»*

### Decisión

1. **`numero_caso` pasa a ser un atributo, no la identidad.** Se quita
   `UNIQUE(numero_caso)` con migración (reconstrucción de `casos`; SQLite no
   sabe quitar un `UNIQUE` con `ALTER TABLE`). La identidad de un caso es **el
   documento del que salió**: archivo más hoja(s).
2. **Ninguna página se rechaza.** Cada documento importado crea su caso, con lo
   que se leyó y con lo que no; lo que falta se rellena al lado del PDF entero.
   Mismo número y MRN distintos = **otra familia = otro caso**. Las hojas de un
   mismo documento que concuerdan siguen uniéndose (grupos).
3. **Reimportar el mismo archivo** (misma huella) sigue sin pisar nada: deja su
   renglón y no toca el caso. P-5 queda donde estaba.
4. La reconciliación del Excel del agente pasa a resolverse por **MRN** dentro
   del caso al que pertenece la fila (la clave visible ya lleva caso y MRN), no
   por `numero_caso` como único, porque ya no lo es.

**Evidencia de la PC del trabajo, no medida por el supervisor:** las capturas
vienen de una máquina distinta, sin Python según el dueño, y el programa
arrancó, importó y escribió sus renglones. Es la primera prueba del criterio 2
de la FASE 9 — la aporta él, no yo.

### El visor, en sus palabras

*«¿Para qué me pones el PDF al lado si no puedo moverme dentro de él? Está muy
pequeño… el zoom debe permitirme hacer zoom a cualquier parte del documento y
con el mouse desplazarme y moverme libremente.»* Medido con `grep` sobre
`interfaz/visor.py`: hay rueda, `Control-MouseWheel` y `Shift-MouseWheel`, y
**no hay `<B1-Motion>`** — no se puede arrastrar con el ratón. Decisión: zoom
con Ctrl+rueda **centrado en el cursor**, arrastre con el botón izquierdo,
doble clic para ampliar ahí, y el documento más grande por defecto.

Y una queja suya que no tiene medición y se apunta igual: *«el programa está
complicado»*.

---

## 2026-09-03 — P-5, decidida por el dueño: un duplicado se avisa, no se rechaza

**Dicho por el dueño, literal:** «Si hay documentos duplicados debe decirlo y no
rechazarlo.»

Cierra la mitad de P-5 que llevaba abierta desde el esquema («PDF procesado dos
veces: reemplazar, rechazar o preguntar»). La respuesta es **ninguna de las
tres**: **entra y se marca**.

- Un archivo ya importado —misma huella, o mismas personas por MRN dentro del
  mismo `numero_caso`— **entra igual**, crea su caso, y queda marcado como
  duplicado de forma visible: en pendientes, en la pantalla del caso y en el
  resumen de la importación, diciendo de qué caso es duplicado.
- **Pisar sigue prohibido.** El caso que ya estaba, con sus correcciones a mano y
  sus firmas, no se toca. El duplicado es un caso aparte con su marca, y Miguel
  decide. **Nada se fusiona solo.**
- Regla permanente 5 intacta en los dos: nada se firma solo.

Va dentro del pase que ya corre (identidad por documento + visor). El renglón
en «Lo que no entró…» pierde sentido para este motivo, porque el documento sí
entró. ⚠️ `docs/ARQUITECTURA.md` §7 sigue listando P-5 como abierta: lo corrige
el planificador cuando el código aterrice y se sepa el nombre real de la
columna.

---

## 2026-09-03 — «Resolver» un caso existe por fin, y «Reportes no hace nada» medido

### Lo que dijo el dueño

*«Yo con el programa debo también resolver casos. No sé cómo se resuelve, cómo
ve los que están resueltos. Debe ver eso.»* Y sobre el botón de reportes: *«no
hace nada, le das y no hace nada».*

### Reportes, medido por el supervisor

Sobre una copia exacta de `aa90ad1` (el `.exe` entregado), base vacía, y la
excepción de Tk capturada: `mostrar_reportes()` → **sin excepción**, título
«Fichas — Reportes», **0 errores de callback**. La pantalla se abre. Lo que el
dueño ve puede ser (a) la pantalla abierta pero **vacía**, porque su base tiene
0 casos —los diez se rechazaron—, o (b) **una copia vieja** del paquete: hay diez
`Fichas-anterior-*` al lado de la buena y su primera captura era de una versión
anterior. **No está medido cuál.** La próxima auditoría de QA conduce Reportes y
Archivar **dentro del `.exe`** con casos importados; hasta hoy solo se verificó
desde el código.

### Decisión: el estado «resuelta»

`estado_recomendacion` tenía dos valores en todo el material —`no_indicada`,
`incompleta`— y ninguno significaba «lista». El dueño acaba de nombrar lo que
faltaba: **resolver**. Se añade el valor **`resuelta`**, y con él:

- **Lo marca Miguel**, con un botón en la pantalla del caso, un gesto. Regla
  permanente 5: el sistema no lo pone solo. Cuando el Excel del agente vuelve con
  los seis pasos en «Sí», el programa lo **propone** —lo enseña como «el
  compañero dice que está lista»— y Miguel confirma o no.
- `ESTADOS_QUE_RESUELVEN = ("resuelta",)`. Con eso `casos_en_riesgo` deja de ser
  igual a `casos_que_viajan_pronto`, y el informe deja de escribir «no se puede
  saber»: las dos deudas que llevaban abiertas desde la FASE 5 y la FASE 8.
- El inicio enseña **dos listas separadas: por resolver y resueltos**, y el
  reporte cuenta con ese valor.
- Los otros dos valores que el plan original suponía siguen sin existir; con tres
  —no indicada, incompleta, resuelta— el dueño puede trabajar. Si hace falta un
  cuarto, lo dirá él.

Va como pase propio en cuanto cierre el que corre (identidad por documento +
visor), porque toca la misma pantalla.

---

## 2026-09-03 — Lo archivado se sigue viendo en el calendario, marcado «archivado»

**Dicho por el dueño, literal:** *«No está eso, solo archivar. Lo que archivo
debe verse en el calendario, debe decir archivado.»*

**Medido con `grep -c 'archivado = 0'`:** `datos/calendario.py` y
`datos/pendientes.py` excluyen lo archivado en todas sus consultas desde la
FASE 5 — «un caso archivado no aparece en ninguna de estas listas» era la
decisión del plan original. **El dueño la revierte para el calendario.**

**Decisión:**
- La **vista de mes** enseña también los casos archivados, con la marca
  **«archivado»** visible en cada uno, para que se vea qué viajó y qué se cerró.
- La **franja roja de los 7 días** y la **lista de pendientes** siguen sin
  archivados: un caso archivado ya no es trabajo por hacer, y meterlo ahí
  taparía los que sí lo son.
- El reporte ya los contaba; no cambia.

Va en el mismo pase que el estado «resuelta», porque tocan la misma pantalla
de inicio, en cuanto cierre el que corre.

---

## 2026-09-03 — El trackpad no baja la pantalla de corrección; la causa está en el código

**Dicho por el dueño:** *«le doy para abajo con el trackpad de mi laptop y no
baja. Eso es súper incómodo.»*

~~**Medido con `grep` y `sed` sobre `interfaz/correccion.py`:** la rueda se ata en
la línea 373 con `bind_all("<MouseWheel>", … yview_scroll(int(-evento.delta /
120), "units"))`. Un ratón con rueda manda `delta = ±120` por muesca; **un
trackpad de portátil en Windows manda deltas pequeños** —±30, ±10, incluso
menores— y `int(-30 / 120)` es **0**: el gesto llega y desplaza cero líneas.
Es un fallo conocido de Tk en Windows, no del trackpad.~~

**Corregido el mismo día, minutos después: esa causa era falsa.** Lo escribí de
memoria sobre un patrón conocido, sin leer la línea. Leída con `sed`, la atadura
literal es `yview_scroll(-1 if evento.delta > 0 else 1, "units")`: cualquier
delta, grande o pequeño, desplaza una línea. Por esa vía un trackpad **sí**
desplazaría. La causa es otra.

**Candidata con nombre, NO verificada con un trackpad aquí:** el proyecto corre
sobre **Tk 9.0** (medido: `tkinter.TkVersion`), y Tk 9 introdujo el evento
**`<TouchpadScroll>`** para el desplazamiento de dos dedos en Windows y macOS,
separado de `<MouseWheel>`. `grep -rn TouchpadScroll interfaz/` → **0 líneas**:
el programa solo ata `<MouseWheel>`, así que el gesto del trackpad no tiene a
quién llegar. Lo dice la documentación de Tk 9 y **no lo he comprobado**: no
tengo trackpad en esta máquina. Se comprueba en el pase, atando los dos eventos
y probando con el portátil del dueño.

**Y en `interfaz/inicio.py` no hay ninguna atadura de rueda:** `grep` de
`MouseWheel`, `bind_all` y `yview_scroll` → 0 líneas. La pantalla de inicio no
se desplaza con la rueda ni con el trackpad.

**Decisión:** el desplazamiento acumula el delta y desplaza en píxeles
proporcionales (`delta` positivo o negativo de cualquier tamaño → movimiento),
en vez de redondear a muescas de 120; y la pantalla de inicio recibe la misma
atadura. Va en el siguiente pase de interfaz, con «resuelta», el calendario con
archivados, los avisos repetidos y lo leído que no se tira.

---

## 2026-09-03 — Regla de interfaz: el texto no ocupa el programa

**Dicho por el dueño, literal:** *«Nadie que use ese sistema va a leer tanto.
Ese es el error: el texto ocupa todo el programa.»*

**Lo que enseña su captura** (no medido en código): un caso de cinco hojas con
**seis bloques de aviso** en amarillo, cinco de ellos casi idénticos, que empujan
el documento a un sello del 7 % y los campos al borde inferior.

**Decisión, y vale para todas las pantallas:**
1. **Un aviso es una línea.** Lo que haga falta explicar va detrás de un
   «ver más», cerrado por defecto. Las explicaciones largas que hoy se pintan en
   pantalla se quedan en los documentos y en el detalle de «Lo que no entró…».
2. **Los avisos iguales se agrupan**: cinco páginas con el mismo problema son un
   aviso con «× 5 páginas», no cinco bloques.
3. **El documento y los campos mandan sobre el espacio.** Los avisos tienen un
   alto máximo; si hay más, se desplazan dentro de su caja, no empujan el resto.
4. El texto de la interfaz se escribe para quien lo usa cada día: corto, y sin
   contar por qué el programa hace lo que hace.

Va en el mismo pase de interfaz que «resuelta», el calendario con archivados, el
trackpad, y lo leído que no se tira.

---

## 2026-09-03 — El visor es lento y tosco en el portátil del dueño

**Dicho por el dueño:** *«Conmigo es lento, se corta, se siente tosco al
moverlo.»* Es en su portátil del trabajo, sin Python.

**Lo que hay medido, y por quién:** el programador del visor midió en la máquina
de desarrollo **48–105 ms por repintado** y una región reescalada del 44 % de la
hoja al zoom por defecto; el diseñador estimó ~9,5 MB por hoja rasterizada a
2705×3500. En esta máquina nadie lo notó; en la suya sí. **No hay ninguna
medición en su portátil.**

**Decisión:** el visor no reescala la hoja entera en cada movimiento. Se guarda
la hoja ya escalada al zoom actual y el arrastre solo mueve lo dibujado; el
reescalado ocurre al cambiar el zoom, una vez, y con un límite de repintados por
segundo. **Criterio:** arrastrar y hacer zoom sin saltos perceptibles **en su
portátil**, medido allí — no aquí. Va en el pase de interfaz.

---

## 2026-09-03 — Lo que hace el viejo, visto funcionando, y lo que el dueño quiere de verdad

**Dicho por el dueño, literal:** *«Lo que necesito es un gestor de PDF que
convierta la información en documento Excel, que yo mismo pueda verificar, que yo
mismo pueda decir: está completa la recomendación para el templo de la
hermana.»* Y sobre la interfaz: *«no pongas tanto texto», «que se pueda cerrar el
texto», «era mejor en movimiento».*

**Visto por el supervisor, arrancando el proyecto viejo en local
(`servidor.py --puerto 8761 --sin-abrir`) y usándolo en el navegador:**

- **Panel:** una pantalla. Tres tarjetas arriba («Por verificar antes de que
  salgan», «Próximas salidas», «El equipo»), cuatro contadores (personas por
  viajar · con la recomendación verificada · hojas sin devolver · viajaron sin
  verificar), el calendario del mes con el caso en su día, y a la derecha un
  cajón **«Suelta los archivos aquí»** con cuatro botones: Cargar formularios ·
  Recibir hoja del agente · Informe para la dirección (PDF) · Bajar la base en
  Excel. **Sin párrafos.**
- **Revisar:** **una tarjeta por documento** — nombre del archivo, caso, número de
  personas, «N por comprobar» — con dos botones grandes, **«Sí, completa» / «No
  está completa»**, y un desplegable «Sin asignar». Filtros arriba: Todo ·
  Incompletas · Sin revisar · Completas · Sin asignar.
- **Al abrir un documento:** el PDF entero a la izquierda con `−` `Ajustar` `+`
  y desplazamiento; a la derecha **«A qué va cada quien»**: cada persona con su
  cédula y sus **seis ordenanzas como fichas que se pulsan**, y abajo «¿Hará el
  miembro una contribución? Sí / No». Arriba, otra vez, **«Sí, completa» / «No
  está completa»**. Ni un aviso de texto.
- **Tabla:** qué columnas salen al Excel, con casillas para apagar y flechas para
  ordenar. **Historial:** lotes procesados.

### Lo que esto decide

1. **Los dos estados que faltaban son los suyos:** **«completa»** y **«no está
   completa»**, los marca Miguel por documento con un botón cada uno. Sustituye
   el nombre `resuelta` que puse yo horas antes: se usa la palabra del dueño.
   `ESTADOS_QUE_RESUELVEN = ("completa",)`.
2. **La unidad de trabajo es el documento**, con su tarjeta, no «el caso» por
   número. Coincide con la identidad por documento que ya está en construcción.
3. ~~**La pantalla de corrección adopta esa forma:**~~ **La pantalla de corrección hace lo mismo que la del viejo, con la cara del nuevo:** PDF entero a la izquierda con
   zoom y arrastre; a la derecha las personas con sus ordenanzas como fichas
   pulsables; arriba los dos botones. Los avisos, si los hay, en una línea que se
   cierra. Se conserva lo que el nuevo hace mejor y el viejo no: la firma de
   Miguel por campo, la tira por origen, los diálogos en español, nada firmado
   solo.
4. ~~**El panel de inicio adopta la forma del viejo:** tarjetas, contadores,
   calendario, y el cajón de soltar archivos con los cuatro botones.~~
   **Corregido minutos después por el dueño, literal:** *«La interfaz de la vieja
   NO me gusta; me gusta más la nueva. Pero la función de la vieja me gusta: los
   botones que funcionan, el menú de iconos que funciona. No debe ser así en la
   nueva interfaz, pero debe hacer lo mismo.»* **Se conserva la cara del nuevo y
   se le meten las funciones del viejo**: tarjeta por documento con «Sí, completa /
   No está completa» y asignar; el panel con lo que viaja pronto, el equipo, el
   calendario con archivados y las cuatro acciones desde un menú de iconos que
   funcione. Nada del aspecto del viejo se copia.

Va al diseñador primero, con el viejo corriendo delante, y después al
programador en cuanto cierre el pase de la identidad.

---

## 2026-09-03 — Los tres mockups v2 y las cinco decisiones de esquema que pedían

**Diseño entregado** (`mockups/mockup-v2-inicio.html`, `-revisar.html`,
`-documento.html`): la cara del nuevo con las funciones del viejo. Según su
informe, no repetido por mí: 0 párrafos en las tres pantallas, una sola franja
de aviso de 96 px máximo, visor al 61 % del cuerpo, y las fichas de ordenanza con
tres estados distinguibles por trama, fondo y palabra — el viejo, medido por él
con `aria-pressed`, no distingue «no leída» de «no marcada».

**Decisiones del supervisor sobre lo que devolvió, para no parar al programador:**

1. **Estados del documento: `completa` y `no_completa`**, palabras del dueño, más
   el vacío «sin revisar». Se guardan en `casos` con **quién y cuándo**
   (`estado_marcado_por`, `estado_marcado_en`). Solo los pone Miguel con los dos
   botones. `ESTADOS_QUE_RESUELVEN = ("completa",)`. Sustituye a `resuelta`.
2. **«No está en el papel»** existe: una marca `ausente_en_el_papel` en
   `procedencia_campo`, distinta de «vacío» y de «no leído». Un campo que el papel
   no trae no es un fallo de lectura ni un borrado.
3. **«Hojas sin devolver»** = documentos asignados en los que **ninguna** persona
   trae propuesta. Un solo número, el simple.
4. **«¿Hará el miembro una contribución?»**, «Aplicar reglas» y «Copias limpias»
   **no entran**: el dueño pidió solo su información, y ninguna es de la suya.
5. Tabla, Historial y Equipo no están diseñadas: el menú las nombra; se
   construyen con la forma de las que sí lo están, sin pantalla propia nueva.

Todo con migración. Nada de esto se toca en `docs/ARQUITECTURA.md` hasta que el
código aterrice; entonces el planificador lo pone al día.

---

## 2026-09-03 — Los compañeros nunca tocan el programa: su Excel vuelve y Miguel marca

**Dicho por el dueño, literal:** *«Si ya se recibió, yo puedo marcar como completo
el documento que creamos para los agentes; se sube y puedo marcar como completo
ese documento que lee el sistema. Es como si los agentes estuvieran usando el
programa, pero yo soy el que manipula el sistema.»*

~~**Regla:** los compañeros solo devuelven su Excel. Miguel lo carga, y lo que
trae es una **propuesta** que la tarjeta enseña —«Sandy dice: lista», con
fecha— con «Sí, completa» a un clic. **La carga nunca cambia
`estado_recomendacion`**; lo cambia el clic de Miguel (regla permanente 5).~~

**Corregido minutos después por el dueño, literal:** *«El documento es el que
marca.» «Imagina que tenga 3 000 formularios.» «Me enviaron 300, yo no me
puedo poner a ver uno a uno a ver cuál se completó.»*

**Regla definitiva:** los compañeros solo devuelven su Excel. **Cargarlo marca en
lote**: el programa lee lo contestado, enseña **una** confirmación con las cuentas
—«se marcarán N como completas y M como no completas»— y al aceptar escribe el
estado en todos con `estado_marcado_por = Miguel`, la fecha y el archivo de
origen. Ese clic es el acto de Miguel: la regla permanente 5 se cumple una vez
por carga, no una vez por documento. Si cancela, no se escribe ningún estado.
Después de aplicar, la lista de lo que cambió y los filtros «completas» / «no
completas» / «sin devolver», para no mirar uno a uno. Lo que Miguel marque a
mano después manda. Hay
acción de lote —«marcar como completas las que el compañero dio por listas»— con
confirmación que dice cuántas y cuáles; es un clic suyo y escribe
`estado_marcado_por = Miguel`. Si discrepan, gana Miguel y la propuesta queda
visible. Filtro «el compañero dice lista». Mandado al pase de «Revisar».

---

## 2026-09-03 — Regla definitiva del Excel de los compañeros, en cuatro frases del dueño

*«El documento que ellos llenan de Excel es el que marca.» «Y dice completado
por Sandy.» «Me enviaron 300, no me puedo poner a ver uno a uno a ver cuál se
completó, y cuál no.» «Y pasa al tablero de completados.»*

**Manda sobre las dos entradas anteriores** (que quedan tachadas de hecho por
esta):

1. Cargar el Excel devuelto **escribe el estado directamente**, sin confirmación:
   `completa` con los seis pasos en «Sí», `no_completa` si alguno es «No».
2. `estado_marcado_por` = **el compañero** cuyo nombre trae la hoja; fecha de la
   carga; archivo de origen. En pantalla: **«Completada por Sandy · fecha»**.
3. Al terminar, un resumen informativo con las cuentas, y filtros para ver cuál
   sí y cuál no sin mirar uno a uno: completas · no completas · sin devolver ·
   sin casar.
4. Lo completo **pasa al tablero de completados**, agrupable por compañero; lo
   demás se queda en el tablero de trabajo. **Y desde ese tablero Miguel selecciona
   varios y los archiva** —dicho por él: *«del tablero de completados es que yo
   puedo seleccionar completo y archivarlo»*— con la función de archivar que ya
   existe; lo archivado va al histórico y al calendario marcado «archivado». Lo que Miguel marque a mano después
   manda y queda «Corregida por Miguel» sin borrar lo del compañero.

**La regla permanente 5 queda repartida por decisión del dueño:** la **firma de
campos** («Todo correcto», `verificado_por`) sigue siendo de Miguel y nunca
automática; el **estado de la recomendación** lo pone el Excel del compañero con
su nombre. Son dos cosas y no se mezclan.

---

## 2026-09-03 — Fecha ya pasada al subir, y los reportes en PDF

**Dicho por el dueño, literal:** *«Si hay documento que se sube y ya pasó la
fecha, debe decir: esta fecha ya pasó, revisar. ¿Quieres archivar o completar?
Para que salga en los reportes.»* Y: *«Me gustaba mucho los reportes en PDF.»*

- Un documento cuya fecha de viaje ya pasó lleva la etiqueta **«Fecha ya
  pasada»** en su tarjeta, con **«Completar»** o **«Archivar»** a un clic; las dos
  lo dejan contando en los reportes. Filtro «fecha pasada»; el resumen de la
  importación dice cuántos llegaron así. Mandado al pase de «Revisar».
- Los reportes en PDF del viejo ya están replicados en el nuevo, según informes
  de programador y QA no repetidos por mí: 6 de las 7 secciones del viejo más
  «Unidades» y «El equipo» añadidas después, escritos a mano sin `reportlab`. Se
  conservan tal cual, y se enseñan al dueño en el paquete para que diga si son
  los que le gustaban.

---

## 2026-09-03 — Añadir funciones, no rehacer; y la rapidez se mide, no se promete

**Dicho por el dueño, literal:** *«Ojo, no te envié a reconstruir todo, te envié
a agregar funciones.» «Se está poniendo súper lento al abrir toda la interfaz.»
«Debe ser rápido.» «Mejora la rapidez.» «Pero como está yendo está bien.»*

**Regla:** las pantallas que existen no se reescriben; se les añade lo que falta
con cambios localizados. **Criterio de rapidez, medible:** arranque hasta
ventana pintada, abrir un caso de seis hojas hasta ver el documento, y un
arrastre y un zoom en el visor — medidos **antes y después** de cada pase con el
mismo método y una base de cientos de casos. Ninguno puede empeorar; abrir un
caso y mover el visor tienen que mejorar con número. Lo que no hace falta para
pintar se carga después de pintar: solo la hoja visible se rasteriza, el resto
al pedirla, y cacheado. Se cuentan las consultas SQL por pantalla. Mandado a los
dos pases que tocan pantallas.

---

## 2026-09-03 — «Revisar» entregada; la migración es la 14; regla 5 precisada en `CLAUDE.md`

**Según el informe del programador de Revisar, NO repetido por el supervisor:**
194 pruebas OK en sus módulos; con 3 000 documentos, aplicar la hoja devuelta
**0,071 s** y leer las 3 000 tarjetas **0,141 s**; y una medición que cambió su
diseño: la rejilla de tarjetas era **n²** por el `<Configure>` de cada control
(500 tarjetas → 65,75 s), así que va paginada de 24 en 24 a ~5 s constante.

**Migración:** el pase decía 13 y ya estaban registradas 12 y 13 por el pase de
identidad (`numero_caso` deja de ser único; `casos.duplicado_de`). **La suya es
la 14**, en `datos/migraciones_de_revision.py`, **sin registrar** hasta que el
pase de identidad cierre y la registre él.

**Regla permanente 5 precisada en `CLAUDE.md` §1** con las palabras del dueño:
firma de campos = Miguel; estado de la recomendación = el Excel del compañero
con su nombre. El programador lo pidió: el código y las reglas tienen que decir
lo mismo.

**Sus decisiones, aceptadas:** lo devuelto a medias no se marca (marcar
`no_completa` lo que nadie miró sería inventar); seis columnas en `casos` y no
tabla de historial, con el coste de que la misma hoja dos veces pisa la
anterior; lo archivado sale de los dos tableros. **Enganches en archivos ajenos**,
mandados al pase de identidad: registrar la 14, `mostrar_revisar`, quién es
Miguel en `datos/companeros.py`, aplicar las marcas al cargar en
`paquete/reconciliacion.py` con `propuesto_desde`, y que el desplegable de
seguimiento no escriba `completa` sin firma.

---

## 2026-09-03 — «Verifica los logs»: no hay ninguno. Se añade un registro de tiempos

**Dicho por el dueño:** *«Verifica los logs, un programa como ese debe ser
rápido.»* Y antes: *«el problema que estoy viendo es en cargar la interfaz de
usuario»*.

**Medido por el supervisor:** `grep -rln "logging"` sobre `fichas.py`,
`interfaz/`, `datos/` e `importacion/` → **nada**; ningún `.log` en
`Documentos\Fichas`. **El programa no escribe ningún registro**, así que no hay
qué verificar. Arranque del `.exe` entregado en esta máquina, proceso → ventana
visible, tres veces: **2,07 · 1,99 · 1,97 s**. De su portátil no hay ningún
número.

**Decisión:** un registro de tiempos mínimo en `<carpeta de datos>\fichas.log`
(rotación 1 MB × 3): arranque hasta ventana pintada, abrir caso hasta documento
visible, OCR por página, generación de paquete e informe. **Sin datos de
personas**: números de caso y segundos. Con eso, «lento» pasa a ser un número
de su máquina. Mandado al pase de identidad, que tiene `interfaz/aplicacion.py`.

---

## 2026-09-03 — La causa real de «lento al abrir»: el calendario se pintaba dos veces

**Según el informe del programador del inicio, NO repetido por el supervisor:**
abrir el inicio costaba **7 325 ms** con solo 7 consultas SQL; el perfil señaló
que `VistaDeMes.pintar` corría **dos veces** por apertura —`__init__` pintaba y
`refrescar()` volvía a pintar— y destruir esos 231 controles costaba **2 885
ms**. Venía de antes y nadie lo veía porque el resultado era correcto. Tres
arreglos —no repintar el mes si no cambió, tope de 8 filas por lista con «y N
más», leer pendientes una vez— y queda en **1 386–1 587 ms**, 4,7× más rápido;
con 1 000 casos, 2 040 ms.

**Lo que devolvió:**
- El panel pide **1 237 px de ancho** y la ventana mide 1 100: se recorta. Venía
  de 1 536 × 3 413; lo redujo y dejó una prueba que fija lo medido para que
  empeorar salte en rojo. **No cumple el criterio 1 y lo dice.**
- `Ctrl+I` pasa a ser el informe (como el mockup) y el importar PDF a `Ctrl+O`.
  Es memoria muscular de Miguel: **decisión del dueño**.
- Dos ayudantes para el mismo `<TouchpadScroll>`: `interfaz/gestos.py` (pase de
  identidad) e `interfaz/desplazamiento.py` (inicio). Defecto del supervisor:
  dio el mismo requisito a dos terrenos sin nombrar dueño. Se consolida en el
  siguiente pase.
- «Revisar» quedó sin enganchar a propósito: no existe en el código quién es
  Miguel, y elegir uno pondría la firma de una persona real bajo una marca que
  no hizo. Lo resuelve el pase de identidad (`datos/companeros.py`).
- El cajón de soltar no recibe arrastre: Tk no lo trae sin dependencia nueva. Es
  un botón y su rótulo lo dice. `interfaz/tarjetas.py` quedó sin uso.

---

## 2026-09-03 — Cerrado el pase de identidad por documento; lo que devolvió

**Según su informe, NO repetido por el supervisor** (la suite entera se está
midiendo aparte; el número válido es el del supervisor):

| Escenario, con OCR real sobre los PDF del Escritorio | Resultado |
|---|---|
| Dos archivos con el mismo `BARC2608`, familias distintas | **2 casos, 5 personas, 0 rechazos** |
| Grupo de seis hojas | **1 caso, 12 personas** |
| El mismo archivo dos veces | **2 casos**, el segundo `duplicado_de = 1`, el primero intacto |

Migraciones 12 (`numero_caso` deja de ser único), 13 (`duplicado_de`) y 14
(Revisar) registradas en orden. Visor: arrastre **26,8 → 1,4 ms**. `fichas.log`
con rotación y la cifra de arranque en el pie: **1,88 / 1,35 / 1,34 s** por su
propio log. `<TouchpadScroll>` decodificado con `::tk::PreciseScrollDeltas`.

**Decisión suya aceptada:** el desplegable de estado de `interfaz/seguimiento.py`
**firma** con `marcar_a_mano` en vez de recortarse — «ese desplegable es la mano
de Miguel».

**Devuelto, y va al pase siguiente (número de migración 15):**
- ~~`reportes/avisos.py:64` hace `sorted({numero_caso})`~~ **La cita del supervisor era falsa, comprobado el 2026-09-04 con `sed -n 64p`: la 64 es una línea de docstring, el `sorted` está en la 80 y ya lleva `or SIN_NUMERO_DE_CASO`, o sea que ya estaba arreglado.** Decía que **revienta con un caso
  sin número** — y ahora los casos sin número entran. Anterior al pase.
- `interfaz/desplazamiento.py` (inicio) y `bind_all("<TouchpadScroll>")` en
  `correccion.py` **se pisan**: `bind_all` reemplaza. Se elige uno.
- Un archivo copiado a otra ruta y **sin MRN legible** entra sin marca de
  duplicado: no hay huella de contenido en la base.
- **La base real del dueño migró a la versión 13 a las 22:28 y ningún agente
  reconoce haberlo hecho.** Sin pérdida (8 tablas idénticas al respaldo v11 en
  `Fichas-entrega/respaldo-base-20260903-2100/`). Probablemente la medición de
  arranque del pase de inicio; **no está medido quién**, y en la base real eso
  no es un detalle.
- Su premisa «árbol limpio, eres el único que escribe» no se sostuvo: HEAD
  avanzó 18 commits durante su trabajo. Cierto: el supervisor commiteó los otros
  dos pases mientras corría. Ninguna de sus mediciones es del árbol descrito.

---

## 2026-09-03 — «Aún el dibujado del sistema es lento»: lo medido por el supervisor

**Sin cifra del dueño**: su carpeta `Documents\Fichas` no tiene `fichas.log`, así
que el `.exe` nuevo no ha arrancado contra su base en esta máquina. Lo que sigue
lo medí yo, en esta máquina, **con la suite entera corriendo al mismo tiempo**,
y por eso sirve para comparar entre sí y NO como cifra absoluta:

| Medición (copia de su base, 2 casos) | Resultado |
|---|---|
| `Fichas.exe` arranque ×3 | 1,75 / 1,81 / 1,63 s |
| `mostrar_inicio` desde el código | 9,3 s, de ellos 5,8 s dentro de `update()` de Tk |
| `abrir_caso` / volver a inicio | 7,6 s / 7,0 s |
| **Control, Tk pelado, 186 widgets ttk** | crear+pintar 1,5–2,0 s, destruir 0,7–0,9 s |
| 2000 llamadas Tk triviales | 0,006 s |
| 200 `Label` creados sin mapear | 0,076 s |
| Construir a oscuras y mapear el marco de golpe | igual o peor (3,3–4,3 s) |
| `grid_remove` / volver a `grid` el mismo marco | 0,08 s / 1,4–1,9 s |
| **Un solo `Canvas` con 186 textos y rectángulos** | **0,52 s; destruirlo 0,01 s** |
| Tema `ttk`: vista, clam, alt, default, winnative, xpnative; `tk` pelado | ninguno baja de 3,9 s; `tk` pelado peor (7,8 s) |
| `ttk.Frame` vacío ×186 | 3–5 s |

**Lectura:** el coste no está en nuestro código ni en las llamadas Tk ni en el
tema: está en **mapear y destruir cada widget con la ventana visible**. En
Windows cada widget de Tk es una ventana hija del sistema (`HWND`), y crearlas
y destruirlas pasa por el escritorio de Windows, que la suite —que abre y cierra
ventanas Tk sin parar— tiene ocupado. Por eso las cifras crecen mientras corre.

**Lo que se decide a partir de esto, pendiente de repetir la medición con la
máquina en silencio:**
1. **Menos widgets por pantalla.** El calendario, la cola de pendientes y las
   tarjetas del equipo se dibujan en un `Canvas` con detección de clic, no con
   un `Frame`+`Label` por día o por fila. Seis veces más barato aquí.
2. **Las pantallas no se reconstruyen al volver.** Hoy `_sustituir` destruye la
   pantalla entera (186 `destroy`, 1,35 s en el perfil) y la vuelve a crear.
3. **Tk 8.6 frente a Tk 9.0.4 no está medido**: solo hay un Python en la máquina
   (3.14, Tk 9.0.4). Probarlo exige descargar otro Python; lo autoriza el dueño.
4. Un `after` de `correccion.py:373` (`_contar_las_hojas_cuando_ya_se_ve`)
   dispara después de destruida la pantalla: «invalid command name» en la suite.
   Se cancela al salir, como ya hace `inicio.py`.

---

## 2026-09-03 — Corrección del propio programador de inicio: 1,9×, no 4,7×

El mensaje del commit `657a5cc` dice «4,7 veces más rápido al abrir». **Ese
número lo dio el programador y él mismo lo retiró** al descubrir que había
comparado el panel viejo y el nuevo en momentos distintos con cargas distintas.
Repetido por él alternando las dos variantes, 9 vueltas (según su informe, NO
repetido por el supervisor):

| | Mediana | Mín / máx |
|---|---|---|
| Original (`657a5cc^`) | 6 400 ms | 2 143 / 18 353 |
| Nuevo | 3 424 ms | 1 767 / 7 397 |

**1,87×, el nuevo gana 9 de 9.** Lo que no depende de la carga: widgets al
abrir **1 635 → 464**; el mes se pintaba dos veces, ahora una. El mensaje del
commit no se reescribe (la historia no se edita): queda corregido aquí.

Los 464 widgets son la cifra que gobierna el pase 4: a ~5 ms por widget en esta
máquina son más de dos segundos por clic.

---

## 2026-09-03 — Precedente: registrar una migración rompe la prueba que la aplicaba a mano

Dicho por el programador de identidad, con su traza (NO repetido por el
supervisor): al registrar la migración 14 en `MIGRACIONES`, `aplicar_esquema`
empezó a aplicarla sola, y el `setUp` de `pruebas/prueba_revision.py` la volvía
a aplicar a mano: `duplicate column name: estado_marcado_por`, 45 errores,
**los mismos 45 en dos pasadas**. No era ruido de suites concurrentes: era
real, y lo cerró él (30 pruebas OK). El supervisor le escribió «45 errores
falsos» en un mensaje y era un hecho equivocado; queda corregido aquí.

**Regla que sale de esto:** una prueba no aplica una migración a mano. Pide el
esquema a `aplicar_esquema` y comprueba la versión. El día que alguien
enganche la 15 no debe volver a pasar.

---

## 2026-09-04 — Cerrado el pase 4, el dibujado (`c0a268c`)

**Medido por el supervisor** sobre `master` fusionado, máquina en silencio
(`Get-Process python*` = 0), copia de la base real (2 casos), con
`medir_pantallas.py`:

| | antes (03-09, en silencio) | después |
|---|---|---|
| `mostrar_inicio` | 1,60 s | **0,20 s** |
| volver a inicio desde un caso | 1,33 s | **0,24 s** |
| `abrir_caso` (primero) | 1,54 s | 0,93 s |
| `mostrar_revisar` | 0,57 s | 0,29 s |
| Suite entera | 1139 OK | **1159 OK, 1 omitida, 553 s, 0 «invalid command name»** |
| `Fichas.exe` arranque ×2 | 1,22 / 1,26 s | 2,19 / 2,42 s (mismo orden; el arranque es cargar Python, no dibujar) |

**Según el programador, NO repetido por el supervisor:** inicio 186 → 82
widgets; una tarjeta de Revisar 14 → 6; volver a inicio 0,218 s frente al
criterio de 0,200 (26 de los 82 widgets son el menú de iconos, que queda en
PENDIENTES). Y un fallo real cerrado que nadie había visto: **la rejilla de
Revisar leía el eje X del `<TouchpadScroll>` como vertical**, así que un
deslizamiento vertical movía 0 px. Justo la queja del dueño, en la pantalla
hecha para resolverla.

**Correcciones a lo que decía el pase:** los «464 widgets» eran de otra base;
sobre `2b6e92a` con la base de 2 casos son 186. El diagnóstico (coste por
widget mapeado) se sostiene: el programador lo repitió en silencio, 2,6 ms por
widget.

**Entrega:** `C:\Users\josem\Fichas-entrega\Fichas` es ahora el `.exe` del pase
4; el anterior queda en `Fichas-anterior-2b6e92a`. Ninguno se borra sin el
dueño.

**Sigue sin medir:** el trackpad real del dueño; su portátil; una base con
cientos de documentos (con 12 casos inventados el programador contó 131
widgets: el techo de 120 se cumple con 2 casos, no con muchos).

---

## 2026-09-04 — El riesgo que dejó abierto el planificador está cerrado en el código

El planificador (`46b22a0`) dejó como riesgo sin verificar que, si el Excel del
compañero identifica la fila solo por número y MRN, dos casos con el mismo
número devolverían dos filas. **Leído por el supervisor en
`paquete/reconciliacion.py:106-116`:** desde la versión 12 del esquema la clave
de cada fila lleva número de caso, MRN **y el id del caso**; una clave sin el id
sale con su propio motivo. El riesgo no existe con paquetes nuevos; con un
paquete generado antes de la 12, la fila entra por el motivo de «clave vieja».
NO probado con un Excel real de dos casos del mismo número: queda para QA.

---

## 2026-09-04 — El dueño decide: el programa completo pasa a C# con WinUI 3

**Sus palabras, dos veces:** «vamos a cambiar el programa completo a C# y WinUI
3, es súper lento en la interfaz que hiciste; debo poder hacer scroll down en
cualquier parte» y «perderemos menos tiempo haciéndolo así». Es su decisión y
manda. Lo que el supervisor midió antes de acatarla, para que quede:

- **Con 40 casos y 58 personas** (base sintética del tamaño de su captura), en
  esta máquina Inicio se pinta en **0,49 s** con **116 widgets**. En su PC del
  trabajo él lo llama «súper lento»; esa cifra no la tengo.
- **El «no se puede bajar» es un defecto de disposición, no de Tk:** el bloque
  rojo con las listas va en la fila 3 con `sticky="ew"` y **580 px** de alto sin
  desplazamiento; la fila 4 (calendario, cajón, equipo, pendientes) es la que
  tiene `weight=1` y a 770 px de ventana **se queda con 2 px**. El único lienzo
  con barra mide 1 px. Medido con `winfo_height` a 1730×770 y 1100×700. Se
  arreglaría en Tk en horas; **no se arregla en Tk porque el dueño ha decidido
  no invertir más ahí.**
- **La máquina:** sin .NET SDK, sin Visual Studio; `winget` 1.29 disponible;
  Windows 11 26200. Instalar el SDK es una descarga: **la autoriza el dueño**.

**Lo que el cambio implica frente a las reglas permanentes (a resolver por el
planificador con fuentes, no aquí):** un solo ejecutable → WinUI 3 sin
paquete y autocontenido (`WindowsAppSDKSelfContained`); OCR determinista → o
`Windows.Media.Ocr` (nativo, sin IA generativa) o onnxruntime en C# portando el
pre/posproceso de PP-OCRv5; PDF → `Windows.Data.Pdf` o PDFium; SQLite →
`Microsoft.Data.Sqlite`; Excel → OpenXML; reportes PDF → por decidir. **Las
reglas de negocio son 1 159 pruebas en Python**: anclas bilingües, bandas,
validaciones, identidad por documento, reconciliación del Excel, reportes.
Eso se porta, no se reinventa.

**Mientras no exista el C#, el `.exe` de Python sigue siendo lo que usa el
dueño.** El pase 1 del ciclo 6 (Guardar y la franja) se termina y se entrega;
no se abre ningún pase nuevo en Tk.

---

## 2026-09-04 — Requisitos del dueño para el programa nuevo, dichos hoy con capturas

Se recogen aquí para que el plan del C# los lleve desde el primer día. Cada
uno con su origen:

1. **Desplazamiento en cualquier parte.** «Debo poder hacer scroll down en
   cualquier parte.» Con la rueda, con el trackpad y con barra visible; ninguna
   pantalla recorta contenido sin forma de bajar. (Captura de Inicio con 31
   casos: las listas cortadas y el calendario fuera de la vista.)
2. **Asignar desde cualquier lugar.** «Debe poder asignarse desde cualquier
   lugar.» Hoy el desplegable «Sin asignar» de la tarjeta de Revisar dice
   «todavía no está conectado, se asigna en la pantalla Asignar casos». En el
   programa nuevo el compañero se asigna desde la tarjeta, desde la corrección
   y desde la lista, y es la misma acción.
3. **Guardar acusa recibo y no tumba lo válido por un campo inválido.** (Del
   ciclo 6; ver arriba.)
4. **Ni un párrafo en pantalla:** avisos de una línea, cerrables, con «ver».
5. **Rápido con 3 000 documentos**, no con 2: la cifra de cada pantalla se
   mide con una base grande.
6. **Duplicados avisados, nunca rechazados**; identidad por documento; el
   Excel del compañero marca `completa`/`no_completa` con su nombre; tablero de
   completados y archivo en lote; fecha pasada pregunta archivar o completar;
   archivados visibles en el calendario; reportes PDF para los jefes como los
   del programa viejo. (Todo decidido el 2026-09-03; sigue vigente.)
8. **Libre para asignar.** «El programa debe ser libre, no me deja asignar a
   los agentes.» Causa leída en el código de hoy (`interfaz/asignacion.py`,
   `_casos_disponibles`): la pantalla solo ofrece los casos **pendientes de
   verificar** y los que **viajan pronto**; cualquier otro caso no aparece, y
   sin compañero elegido no ofrece ninguno. En el programa nuevo **cualquier
   caso se asigna a cualquier compañero activo, desde cualquier sitio**, sin
   filtro de estado. Lo único que sigue: no se asigna a un compañero
   desactivado.
9. **Sin restricciones que bloqueen.** «No me gusta que me ponga tantas
   restricciones.» Contado hoy: 84 puntos en `interfaz/` y `datos/` donde una
   validación levanta un error o abre un cuadro. En el programa nuevo la regla
   es **avisar, nunca impedir**: un valor raro se guarda y se señala en su
   sitio; un formato inesperado no detiene nada. Las dos excepciones, y son
   las únicas: nada se firma como verificado sin Miguel (regla permanente 5)
   y nada se borra sin preguntar.
7. **Las cinco reglas permanentes siguen:** sin IA generativa para leer, un
   solo ejecutable sin instalación, sin pandas (en C#: sin dependencias que
   inflen sin aportar), español en todo, nada se firma solo.

---

## 2026-09-04 — SDK de .NET 10.0.400 instalado en la carpeta de usuario

El dueño respondió «Intentar nuevamente» a la petición de autorizar la
descarga; el supervisor lo tomó como el sí y lo dijo. `winget install
Microsoft.DotNet.SDK.10` se canceló dos veces en la ventana de permisos de
Windows (exit 1602, «cancelado por el usuario»). Se instaló entonces **sin
administrador** con el guion oficial `dot.net/v1/dotnet-install.ps1`:

| | |
|---|---|
| Versión | 10.0.400 (`dotnet --version`) |
| Carpeta | `C:\Users\josem\.dotnet` (no está en el PATH: se usa con `DOTNET_ROOT` y la ruta completa) |
| Origen | download.microsoft.com, hash verificado por winget en el primer intento |
| Deshacer | borrar la carpeta `C:\Users\josem\.dotnet` |

Un programa de consola `net10.0` compila y corre (salida abajo, medida por el
supervisor). Compilar WinUI 3 exigirá además paquetes NuGet de Microsoft
(Windows App SDK, Windows SDK BuildTools): son descargas de nuget.org que el
plan del planificador nombra con versión antes de traerlas.

---

## 2026-09-04 — La línea base de lectura con los dos PDF reales, medida por el supervisor

El planificador (ADR-0003) dijo que la medida «18/18 campos, 4 de 5 MRN» no
existía. Existía, pero solo en la conversación: no estaba escrita. Va aquí.
Comando: importación desde el código (`leer_un_pdf` + `guardar_un_pdf` +
`cerrar_la_tanda`, OCR real) sobre base limpia, en esta máquina, con
`C:\Users\josem\Desktop\BARC2608_*_limpio.pdf`:

| | |
|---|---|
| Motor OCR listo | 2,2 s |
| Fulano_Family (1 hoja) | 10,4 s → 1 caso, 4 personas, 0 rechazos, 0 duplicados |
| Elisa_Mengano (1 hoja) | 8,6 s → 1 caso, 1 persona |
| Casos / personas / ilegibles | 2 (los dos `BARC2608`, sin marca de duplicado) / 5 / 0 |
| Renglones de `procedencia_campo` con `valor_ocr` | casos 8 de 8; personas 10 de 10 |
| Personas con nombre / con MRN | 5 de 5 / **4 de 5** |

Esta es la línea base que la FASE C0 tiene que igualar con el OCR de Windows
sobre los **mismos dos archivos, por separado**, además de la que puso el
planificador (los dos unidos → 1 caso · 4 personas · `2026-08-25`).

---

## 2026-09-04 — El `CHECK` del número de caso se quita en el programa nuevo

El planificador (`3a746c9`) devolvió la decisión: `casos.numero_caso` lleva
`CHECK (numero_caso GLOB '[A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9]')`, y con
él ningún aviso puede sustituir al rechazo porque la fila no entra en SQLite.

**Comprobado por el supervisor con `GLOB` en sqlite3:** su ejemplo
`CASD2609` **sí entra** (cuatro letras y cuatro dígitos); los que NO entran son
`CASP26O9` (una O por un cero, error típico del OCR), `CAS P2609` (un espacio)
y `casp2609` (minúsculas). El argumento se sostiene con esos casos, no con el
suyo.

**Decisión del supervisor, dentro de lo que el dueño ya fijó («sin
restricciones que bloqueen», requisito 9):** en el programa en C# el número de
caso se guarda tal como se lee o se teclea y, si no tiene la forma esperada, se
**señala en el campo** en una línea; no se rechaza. Los otros 27 `CHECK` de
coherencia se quedan. En el `.exe` de Python no se toca nada: está congelado.

**Nota sobre el ADR-0003 §0:** dice que la medida «4 de 5 MRN» no existía; desde
`3b791bc` está escrita en este documento. El ADR es inmutable y la corrección
vive aquí.

---

## 2026-09-04 — Cerrado el pase 1 del ciclo 6 (`24a9699`): Guardar se ve, un campo malo no tumba lo demás

**Repetido por el supervisor** pulsando el botón con `invoke()` sobre una copia
de la importación real (caso 1, persona 3 con `mrn = NULL`), tecleando con
`<KeyRelease>` como en la vida real:

| Escenario | Cuadros | Pie | Base |
|---|---|---|---|
| MRN 3 = `123-4567-8901` | 0 | «Guardado a las 11:07 · 1 campo» | persona 3 con el MRN |
| MRN 3 = `123` y MRN 1 = `999-8888-7777` a la vez | **0** | «Guardado · 1 campo sin guardar: MRN» | persona 1 guardada, persona 3 sigue `NULL` |
| Etiquetas con más de 2 líneas en toda la pantalla | 0 | | |

**Una trampa para quien lo repita:** si se escribe en la `Entry` con `insert`
sin disparar `<KeyRelease>` ni `<FocusOut>`, el campo no sabe que es inválido y
el repositorio sigue levantando: sale el cuadro viejo. El supervisor cayó en
ella la primera vez; con teclado real no pasa.

**Según el programador, NO repetido:** suite 1203 OK (44 pruebas nuevas), pie
del `.exe` 2,79 s frío / 1,18–1,19 s caliente, cabecera de 310 px a 30 px.
La suite la mide el supervisor sobre `24a9699`; el número se anota en
EN-CURSO.

**Decisiones que devolvió y que se toman aquí:** el detalle largo de un aviso
sale en un cuadro al pulsar «ver cuáles», no dentro del desplegable: vale, es
lo que cumple «ni un párrafo en pantalla». Los avisos que devuelve `guardar`
(mes cruzado, firmas retiradas) siguen en cuadros: **queda para el C#** (requisito
9), no se abre otro pase en Tk.

**Entrega:** `C:\Users\josem\Fichas-entrega\Fichas` es ahora el `.exe` del ciclo
6; el anterior, `Fichas-anterior-c0a268c`.

---

## 2026-09-04 — Cerrada la FASE C0, la espiga medida (`df22801`)

**Lo que decide a favor de WinUI 3**, según el programador y con sus comandos
en la entrega (NO repetido por el supervisor salvo lo que se dice abajo):

| | C# WinUI 3 | Python/Tk hoy |
|---|---|---|
| Arranque, carpeta | **0,96 – 1,04 s** | 2,19 / 2,42 s |
| Pintar 3 000 filas | **0,181 s** de mediana (una de cinco a 0,205) | no se midió con 3 000 |
| Elementos vivos con 3 000 filas | **193 – 202**, no crece con los datos | 131 widgets con 12 casos |
| Carpeta autocontenida | 270,3 MiB / 516 archivos | 217,2 MiB / 124 |
| Archivo único | funciona: 285,7 MiB, pero extrae 276 MiB a `%TEMP%` y tarda **4,7 s** la primera vez | — |

**Dos correcciones que trajo la medición:**
- **No hace falta el Modo de desarrollador**: con `WindowsPackageType=None` el
  programa compila, publica y arranca sin él. Solo lo exige el flujo empaquetado.
- **Esta máquina SÍ tiene trackpad** (`ASUS Precision Touchpad`, por
  `Get-PnpDevice`). Lo que no se puede es accionarlo por software. Todo lo que
  este proyecto dio por «aquí no hay trackpad» estaba mal dicho.
- `PublishSingleFile` **sí funciona** en WinUI 3: de las dos páginas de
  Microsoft que se contradicen, gana la que dice que sí (ADR-0003 §2.1, fuente
  2). En archivo único hay que buscar los datos con `Environment.ProcessPath`,
  no con `AppContext.BaseDirectory`.

### **Decisión: se publica en carpeta, no en archivo único**

La toma el supervisor con las cifras delante y es reversible. El archivo único
cuesta 4,7 s de primera apertura y deja 276 MiB en la carpeta temporal cada
vez; el `.exe` de Python ya es una carpeta con doble clic y el dueño la abre
sin problema. Si él prefiere el archivo suelto, se cambia una propiedad.

### ⚠️ El hallazgo que cambia el plan: el OCR de Windows no lee los MRN

**Comprobado por el supervisor** sobre los volcados de la espiga
(`grep -oE "[0-9]{3}-[0-9]{4}-[0-9]{4}"` a 3 500 px):

| | `Windows.Media.Ocr` | Línea base Python (medida el 2026-09-04) |
|---|---|---|
| **MRN completos** en los dos PDF del dueño | **0 de 5** | **4 de 5** |
| Forma del fallo | siempre `###-## #-####`: pierde un dígito del grupo del medio | — |
| Nombres | 5 de 5 presentes (uno con una letra mal) | 5 de 5 |
| Grupo de seis hojas | **10 personas** (las hojas 5 y 6, en inglés y más degradadas, dan cero) | 12 |

Subir la resolución lo empeora: a 7 000 px lee 1 de 4 y a 10 000 px, 0 de 4.

**El MRN es la identidad de este sistema.** La vía A del ADR-0003 —OCR nativo,
0 MB— **no iguala lo que hoy funciona** y no puede sostener sola el programa.
La comparación entre la vía B (onnxruntime con los mismos modelos PP-OCRv5,
148 MB, portar DB + CTC + cls) y una vía C que el ADR no contempla (Windows
para el grueso del formulario y PP-OCRv5 solo para la banda de los MRN) es
**del planificador**, con coste y fuentes. Hasta que la resuelva, ninguna fase
que dependa del OCR se lanza.

---

## 2026-09-04 — Por qué cuesta leer la cédula de miembro: una raya de la tabla la parte

El dueño preguntó por qué el OCR falla justo ahí, «si son solo números».
**Mirado por el supervisor**: se rasterizó la página a 3 500 px, se recortó la
banda del MRN con las coordenadas que el propio sistema guarda
(`procedencia_campo.banda_x0..y1`) y se miró la imagen.

**La causa: una línea vertical de la tabla del formulario atraviesa el número**,
justo en el grupo del medio. No es la letra ni la resolución: es que el número
está impreso montado sobre el borde de una columna. Por eso el OCR de Windows
devuelve siempre `###-## #-####`: la raya le parte el renglón en dos trozos y
entre ellos se pierde un dígito. RapidOCR con PP-OCRv5 no se traga la raya y
por eso lee bien.

**Lo que esto abre, y va al ADR-0004:** una limpieza de líneas antes del OCR
(quitar los trazos largos y finos, horizontales y verticales, con morfología de
OpenCV) puede arreglar el problema **para cualquier motor**, incluido el nativo
de Windows. Hoy el preproceso es solo `medianBlur`.

### Y una segunda cosa, que no es OCR: el «4 de 5» no era un fallo de lectura

**Comprobado en la capa de texto del propio PDF, sin OCR de por medio**
(`pypdf`, patrón `\d{3}-\d{4}-\d{3}[0-9A-Za-z]`): de las cuatro cédulas del
documento, **una termina en la letra `A`**. El OCR la leyó entera y bien; quien
la rechazó fue **nuestra validación**, que exige once dígitos. El sistema se
quedó con el campo **vacío** y guardó lo leído solo en `valor_ocr`.

**El dueño dice que esa `A` es un error del papel.** Da igual de quién sea el
error: **el programa no puede dejar el campo vacío en silencio**. Tiene que
enseñar lo que decía el papel, señalar que no cuadra, y dejar que él lo
escriba. Es el requisito 9 con un caso real detrás.

### Lo que el dueño precisó sobre la precisión

*«No habría problemas, yo puedo digitarlo a mano, porque tengo que verificar
todas las informaciones de cada documento siempre.»* Entonces la precisión del
OCR en el MRN **no es una condición de corrección, es ahorro de tecleo**: él
verifica documento por documento de todos modos. Lo que sí es condición: que
nunca se pierda en silencio lo que el papel decía.

---

## 2026-09-04 — La línea base DE VERDAD: siete escaneos reales del dueño (CASP2609)

El dueño dijo que los dos PDF anteriores eran del programa antiguo y no valían,
y envió **siete documentos reales**. **Diferencia capital, comprobada por el
supervisor con `pypdf`: estos NO tienen capa de texto, son escaneos de verdad.**
Los anteriores sí la tenían, y por eso todo parecía leerse tan bien. **Todas las
cifras anteriores de lectura quedan anuladas como criterio.**

**Medido por el supervisor**, importación real con OCR (`leer_un_pdf` +
`guardar_un_pdf`), base limpia, esta máquina:

| | |
|---|---|
| Documentos leídos | **7 de 7**, 0 ilegibles, 0 rechazados |
| Segundos por hoja | 9,3 a 18,5 (mediana ~12,6) |
| Personas | 7 de 7, **7 de 7 con nombre** |
| **Cédulas guardadas** | **5 de 7** |
| Fecha de viaje | 7 de 7, todas `2026-09-08` |
| Unidad (número y nombre) | 7 de 7 |
| Número de caso | 7 de 7, pero **uno leyó `CASD2609` en vez de `CASP2609`**: una P por una D, y entra como caso aparte |
| Templo | 6 dicen «Panama City, Panama» y 1 «Panama City» |
| **Casillas de ordenanza** | **0 de 7** ~~porque en un escaneo no hay anotaciones~~ — **eso era falso, comprobado el 2026-09-04**: están apagadas a propósito (`extraccion/casillas.py`, `LECTURA_DE_CASILLAS_ACTIVA = False`) por falta de formularios con la verdad anotada |

**Las dos cédulas que faltan tienen la misma causa que ya se documentó:** el
OCR leyó `###-####-###A`, con una letra al final, y **nuestra validación de once
dígitos las tiró**. El campo quedó vacío y lo leído solo vive en `valor_ocr`.
No es un fallo de lectura: es la restricción. Con los dos documentos de antes
pasó exactamente lo mismo.

**Lo que esto fija para el programa nuevo:**
1. **El criterio de aceptación de la fase de lectura son estos siete
   documentos**, no los anteriores: 7 de 7 leídos, 7 de 7 nombres, 7 de 7
   fechas y unidades, y **7 de 7 cédulas enseñadas** (guardadas o señaladas,
   nunca vacías en silencio).
2. **Las casillas de ordenanza hay que leerlas de la imagen.** ~~En los
   escaneos reales del dueño no hay anotaciones.~~ **Eso era falso y lo
   comprobó el supervisor con `pypdf` el 2026-09-04: cada hoja trae entre 10 y
   13 anotaciones `/FreeText` y entre 3 y 6 `/Ink`**, y de ahí salen el número
   de caso y la corrección de la fecha. Lo que no hay es capa de texto. Las
   casillas siguen sin leerse por otra razón: están apagadas a propósito hasta
   que el dueño anote la verdad de tres formularios.
3. ~~Un número de caso mal leído por una letra crea un caso duplicado.~~
   **Corregido el 2026-09-04, comprobado por el supervisor con `pypdf`: no lo
   leyó mal nadie.** El documento de Jonas Ficticio **lleva escrito `CASD2609`
   dentro de su propia anotación**; el único sitio donde pone `CASP` es el
   nombre del archivo. Es un error de tecleo de quien llenó el formulario y
   **ningún motor de lectura lo arregla**. Sigue haciendo falta poder corregir
   el número a mano y unir los dos casos, que estaba pedido y sin hacer.

---

## 2026-09-04 — La cédula PUEDE terminar en letra: la validación estaba mal, no el OCR

**Mirado por el supervisor** en los escaneos reales del dueño: se recortó del
PDF la banda de la cédula de las dos personas cuyo MRN no se guardó
(`Elena Rosa Muestra`, `Julia Luz Inventada`) y **se miró la imagen del
papel**. Las dos terminan en la letra `A`, impresa, nítida, en un escaneo de
verdad. **El OCR las leyó exactamente bien.** Quien las tiró fue
`datos/validacion.py:69`, que exige «11 dígitos en patrón 3-4-4».

Con esto son **tres documentos distintos** (uno de los BARC, comprobado en su
capa de texto, y estos dos escaneos) donde la cédula acaba en letra. El dueño
dijo del primero «ese es un error»; **aparece en 2 de sus 7 documentos reales**,
así que como formato hay que aceptarlo. Y aunque fuera un error del papel, la
regla del proyecto ya lo resuelve: **se guarda lo que dice el papel, se
señala, y Miguel confirma** (requisito 9). Perderlo en silencio es lo único
que no vale.

**Formato real observado: 3 dígitos, 4 dígitos, y 4 caracteres de los que el
último puede ser letra.** No se normaliza, no se corrige, no se inventa: se
guarda tal cual.

**Confirmado por el dueño el 2026-09-04, con sus palabras:** *«Muchas cédulas
de miembro tienen una A u otra letra al final».* No es un error del papel ni un
caso raro: **es el formato**, y la letra puede ser cualquiera. Una cédula
terminada en letra se guarda como cualquier otra, **sin aviso y sin marca de
duda**. Queda anulado lo que él dijo antes («ese es un error») sobre el primer
documento.

**Y una respuesta al hueco que dejó el planificador (ADR-0004):** en estos
escaneos reales **la raya vertical de la tabla NO cruza el número** — pasa a la
izquierda, lejos. Era un rasgo de los dos PDF viejos. **La vía A' (limpiar
líneas) vale mucho menos de lo que parecía**, y no puede justificarse con los
documentos del dueño.

---

## 2026-09-04 — Decisión: el C# lee con RapidOcrNet (vía B del ADR-0004)

La toma el supervisor para que la fase de lectura no espere, y **es
reversible**: si en la fase C3a no alcanza la línea base, se cae a la vía D
(llamar al lector de Python empaquetado aparte), que ya lee 7 de 7 nombres y
5 de 7 cédulas sobre los documentos reales.

**Lo que la decide**, según el planificador en el ADR-0004 y NO repetido por el
supervisor: `RapidOcrNet` 4.1.0, Apache-2.0, **trae los modelos v5 latinos y el
diccionario de 502 caracteres embebido en el propio `.onnx`**, y no hay que
reimplementar DB, CTC ni cls. **30,4 MiB frente a los 189,0 MiB de llevarse el
Python entero.** Y tres cifras del ADR-0003 que estaban mal: el diccionario sí
está, `onnxruntime` en win-x64 son 17,27 MiB y no 147,83, y no hay que portar
el pre y posproceso.

**El criterio que tiene que cumplir, y son los documentos reales del dueño:**
7 de 7 leídos, 7 de 7 nombres, 7 de 7 fechas y unidades, **7 de 7 cédulas**
(con la letra final aceptada), y ninguna hoja perdida en silencio. Si no llega,
se cae a la vía D sin discusión: el MRN es la identidad.

La vía C (Windows para el grueso y PP-OCRv5 para la banda del MRN) queda
descartada por el planificador con razón medida: la banda se localiza **por su
ancla de texto**, así que no se conoce hasta haber leído la página entera. Y la
vía A' (limpiar rayas) vale poco: en los escaneos reales la raya no cruza el
número.

---

## 2026-09-04 — Cerrado el terreno de Lectura del C#: lee los siete igual que el Python

**Verificado por el supervisor**: `dotnet test Fichas.Pruebas.Lectura` →
`Failed: 0, Passed: 41`. Y `pypdf` sobre los siete escaneos confirma sus dos
correcciones: traen anotaciones, y el `CASD2609` está escrito dentro del
documento.

**Según su programador, con hashes y comandos pegados, NO repetido por el
supervisor:** los tres `.onnx` de RapidOcrNet son **idénticos byte a byte** a
los del repositorio y el diccionario latino de 502 entradas coincide; **0
conexiones de red** en 82 muestras durante la lectura; **53,52 MiB** el
subsistema frente a los 189 de llevarse el Python entero; **9,4 a 13,3 s** por
hoja frente a 12,9 a 18,5 del Python; y sobre los siete documentos reales,
**7 de 7** en leídos, nombres, fechas, unidades, cédulas y números de caso,
**con los mismos valores que el Python uno a uno**.

**La decisión del motor queda confirmada por medición**: la vía B alcanza la
línea base, así que no se cae a la vía D.

**Bibliotecas que trae, con su licencia:** RapidOcrNet 4.1.0 (Apache-2.0),
PDFtoImage sobre PDFium (MIT / BSD-3-Clause) elegido porque es **el mismo motor
que `pypdfium2`** y así la imagen no cambia, y PdfPig (Apache-2.0) para las
anotaciones, porque PDFium no da el color ni el grosor que separan un tachón de
un resaltador.

**Lo que pidió y le concedió el supervisor:** `CampoPropuesto` no tenía dónde
poner `ValorOcr`, `AnuladoPorTachon` ni `NecesitaRevision`, que la base sí
tiene. Se añaden al final y con valor por defecto, así que nadie deja de
compilar. `Fichas.Contratos` está congelado y su titular es el supervisor.

**Lo que sigue sin probarse, y lo dice él:** un formulario en **español** sobre
papel real (los siete del dueño están en inglés), un PDF de varias hojas, otra
máquina distinta, y un PDF corrupto.

---

## 2026-09-04 — Cerrado el terreno de Reportes, y dos citas del supervisor que eran falsas

**Verificado por el supervisor**: `dotnet test Fichas.Pruebas.Reportes` →
`Failed: 0, Passed: 77`. Y `sed -n 64p reportes/avisos.py` más `wc -l`
confirman sus dos correcciones: la línea 64 es un docstring y el `sorted`
peligroso está en la 80 **ya arreglado**; el archivo tiene 97 líneas, así que la
«línea 100» que citó el supervisor no existe y el «1 personas» está en la 83.
**Las dos citas del supervisor estaban mal y las dio por buenas sin abrir el
archivo.**

**Según su programador, NO repetido:** el PDF lo abre `pypdf` con `strict=True`
y trae 3 páginas y 6 174 caracteres con las tildes intactas; con 3 000 casos y
7 531 personas tarda **1,480 s** y pesa 662 KiB; comparado con el Python sobre
los mismos datos, **68 líneas de volcado por lado y 5 distintas**, y las cinco
son la concordancia de número que venía a arreglar («1 personas» → «1 persona»).

**Cero bibliotecas:** `dotnet list package --include-transitive` no devuelve ni
un paquete. El motor de PDF está portado a mano en 280 líneas, así que **no hay
ninguna licencia de terceros que auditar** y el ejecutable no crece. El proyecto
viejo del dueño sí usaba `reportlab`; no hizo falta.

### Deuda medida que hay que cerrar antes de conectar la pantalla

`IProcedencia` no tiene lectura en bloque, así que para el reporte hay que
llamar **una vez por caso y una por persona**: con 3 000 casos son **10 531
llamadas**, y son **1,171 s de los 1,480 s totales, el 79%**. Contra memoria se
aguanta; **contra SQLite serían 10 531 consultas**. Hace falta un resumen por
caso en el contrato. Y `FiltroDeCasos` no tiene rango de fechas, así que hoy se
traen todos los casos y se filtra en memoria.

### Dos decisiones que van al dueño

1. **La orientación del papel.** El informe del programa viejo era vertical; el
   nuevo lo puso apaisado para que quepan las ocho columnas. Se portó el
   apaisado, pero es una diferencia real con «el que le gustaba».
2. **Los rótulos.** El viejo decía «Verificadas» y «Sin verificar»; el criterio
   nuevo dice «con la preparación completa». Son dos decisiones suyas que
   chocan.

---

## 2026-09-04 — Cerrados Asignar y Revisar: el «no me deja asignar» queda medido al revés

**Verificado por el supervisor**: `dotnet test Fichas.Pruebas.App` →
`Failed: 0, Passed: 56` (18 del esqueleto más 38 nuevas).

**Según su programador, con la ventana abierta y la máquina en silencio, NO
repetido por el supervisor:**

| | |
|---|---|
| Lo que dice la pantalla de Asignar | **«3000 casos en la base · 3000 ofrecidos · 6 compañeros activos»** |
| Con un caso en cada estado | 7 en la base, **7 ofrecidos**, incluidos el ya verificado y el archivado |
| Asignar desde una tarjeta de Revisar | el botón pasa a «Yudelka», acuse en el pie, **1 fila viva** en `asignaciones` |
| Tablero de completados | pastilla «Completados 13», cada tarjeta con **«Completada por Sandy · 2026-08-23»** |
| Ctrl+A y archivar en lote | «13 marcado(s)» → «13 documento(s) archivado(s)», las 13 pasan a «archivado el …» |
| Navegar a Asignar / a Revisar | 132 ms y 19 ms · 82 ms y 20 ms |
| Cuadros modales | **0 en 11 archivos** |

**Un fallo que ninguna prueba veía y solo apareció con la ventana abierta:**
pulsar «Archivar los marcados» **mataba el proceso** sin dejar línea en
`fichas.log`, por cambiar la fuente de una lista con elementos aún marcados.
Cerrado. Es el argumento de por qué QA tiene que abrir la ventana y no leer
código.

### Dos cosas que quedan abiertas y no las decide un programador

1. **P-11: asignar un caso que ya lleva otro compañero.** Hoy **no retira al
   primero**: se guarda, el motor avisa y la tarjeta enseña los dos nombres.
   **Lo decide el dueño.**
2. **Quién es «yo» al firmar a mano.** En el C# no hay ninguna noción de usuario
   actual: se toma el compañero activo llamado Miguel y, si no lo hay, el
   primero activo; sin nadie activo **no firma y avisa**. Es un hueco declarado.
   **Va al planificador.**

---

## 2026-09-04 — Cuatro respuestas del dueño, con sus palabras

**1. Contactos: no como registro de llamadas, sí como canales para localizar al
líder.** Sus palabras: *«No uso contactos, aunque sí debemos comunicarnos con
los líderes: coloca canales como teléfono, correo, WS, o todo completo, sin
contactar»*.

**Comprobado por el supervisor** con `pragma table_info`: la tabla `contactos`
que existe hoy **no es lo que él pide**. Es un registro de intentos (`fecha`,
`medio`, `con_quien`, `resultado`, `respondio`, `anulado`). Y `companeros` solo
tiene `nombre` y `activo`: **ningún teléfono, ningún correo en toda la base**.

~~Lo que pide es guardar los canales del líder: teléfono, correo, WhatsApp.~~
**Lo precisó él mismo acto seguido, y es lo contrario:** *«No debe haber
teléfono ni teléfono, solo el canal que el agente se comunicó y ya»*.

**Nada de agenda. Ni un número, ni un correo, en ninguna tabla.** Lo único que
se guarda es **por qué canal habló el compañero**: WhatsApp, llamada, correo,
presencial u otro. Ni con quién, ni a qué número.

**Comprobado por el supervisor** (`datos/contactos.py:42`): esa lista cerrada de
cinco medios **ya existe** en el Python, en `MEDIOS_DE_CONTACTO`, y la tabla
`contactos` ya guarda el `medio`. Así que **la tabla se queda y no hace falta
inventar nada**; lo que sobra son las columnas que piden datos de una persona.

**Esto cierra la DC-17 del planificador** («¿contactos entra o sale?»): **entra**,
como registro de canal, y **sin agenda**. Y de paso es la opción que menos datos
personales guarda, que en este proyecto pesa.

**2. El informe de los jefes: como el viejo.** Sus palabras: *«si el informe de
los jefes como el viejo está bien»*. Se toma como respuesta a las dos preguntas
que se le hicieron: **papel vertical** (el viejo era Carta vertical, 612×792) y
**los rótulos del viejo** («Verificadas», «Sin verificar», «Quiénes viajaron sin
verificar»), no los del criterio C8-2. Si al verlo prefiere otras palabras, se
cambia.

**3. Quién entra en la lista, lo decide él.** Sus palabras: *«yo debo tener el
control de quién se añade y quién no»*. Ningún compañero se crea solo: ni
importando un Excel, ni al leer un nombre en una hoja devuelta. Un nombre que
llegue de fuera y no esté en la lista **se señala y espera**, no se da de alta.
Esto cierra el hueco de «quién es Miguel»: él se añade a sí mismo y firma con
su nombre.

**4. Dos preguntas que no se entendieron y hay que rehacer**, así que siguen
abiertas: los rótulos del informe (se resuelve con la respuesta 2) y qué pasa
al asignar un caso que ya lleva otro compañero.

---

## 2026-09-04, 18:05 — El programa nuevo migró la base VIVA del dueño solo por abrirse

**Qué pasó, reconstruido por el supervisor desde el propio `fichas.log` de la
carpeta de datos del dueño:**

```
18:05:37  ARRANQUE  ventana lista en 868 ms  datos=la base de verdad
          carpeta=C:\Users\josem\Documents\Fichas
18:05:47  Correccion abre el caso 1
18:06:36  CIERRE
```

Alguien arrancó `Fichas.exe` **sin `--carpeta-de-datos`**, y el programa hizo lo
que está escrito que haga: resolvió la carpeta de Documentos, abrió la base real
y **la migró de la 13 a la 15 sin preguntar y sin copia previa**. Las
migraciones 14 y 15 quedan registradas con fecha `2026-09-04 18:05:37`.

**Ningún dato se perdió ni se cambió.** Comprobado por el supervisor comparando
fila a fila contra el respaldo de anoche (`respaldo-base-20260903-2100`, versión
11): `casos`, `procedencia_campo`, `documentos_ilegibles` y `companeros` **son
idénticos**, con los mismos valores y las mismas firmas. Solo cambió el esquema.
Se hizo respaldo inmediato en `Fichas-entrega\respaldo-base-20260904-1815`.

### Lo que esto enseña, y vale más que el susto

**Que un programa migre la base de trabajo de alguien por el mero hecho de
abrirse, sin avisar y sin copia, es un riesgo para el dueño**, no una comodidad.
Hoy no pasó nada porque su base tiene dos casos; el día que tenga tres mil, una
migración a medias sin respaldo se lleva el trabajo de meses.

**Regla nueva, y va a las fases del C#:** antes de aplicar ninguna migración, el
programa **copia la base** a un archivo con la fecha al lado, y solo entonces
migra. Si la migración falla, la base se queda como estaba y se dice dónde está
la copia. Y la cifra del pie dice en qué versión entró y en cuál salió.

**Y una regla para los agentes, que ya se les había dicho:** nadie arranca el
`.exe` sin `--carpeta-de-datos` apuntando a una carpeta temporal. Se avisó a QA
en cuanto se detectó.

---

## 2026-09-04 — QA auditó con datos inventados, y por eso dos de sus hallazgos no eran del programa

**Lo devolvió el programador de Corrección con una sonda de veinte líneas**, y el
supervisor lo da por bueno porque la sonda mide sobre los siete PDF reales: los
siete rasterizan a 1314×1700 px en 384 a 1 380 ms. El visor **sí pintaba**.

El texto «el escaneo todavía no se puede pintar: la lectura de PDF es la fase
C3» solo aparece cuando el PNG llega **vacío**, y lo único que devuelve un PNG
vacío es `LecturaDePdfFalsa`, la que se usa con `--falso N`. QA declaró en su
propio informe que midió con `--falso 40`.

**La lección, y es del supervisor, no de QA:** el pase de auditoría no le exigió
medir sobre datos reales, y con datos inventados **la mitad de lo que se audita
no es el programa, es el doble**. QA dijo con razón «medí con --falso 40, que no
escribe en la base» en su apartado de no verificado; el fallo fue no habérselo
prohibido en el encargo.

**Lo que sí se sostiene de su auditoría, y sigue en pie:** la pantalla de
Importar no abre el selector —eso no depende de los datos—, el programa migra la
base real sin copia previa, el `CHECK` de `personas.mrn` rechaza en duro, y las
pantallas de Reportes y Paquetes dicen «pendiente». Los cuatro están encargados.

**El defecto real del visor era otro:** un texto que mentía sobre lo que el
programa sabe hacer. Y midiendo se encontró uno peor que nadie había visto:
entrar en un caso **congelaba la ventana 8 256 ms**, de los que 6 600 eran
rasterizar y pasar el OCR en el hilo de la ventana. Ahora 1 674 ms sin
congelar.

**Regla nueva para las auditorías:** QA mide sobre datos reales del dueño y
sobre la base de verdad copiada, nunca con `--falso`. Si algo solo se puede
probar con datos inventados, se dice cuál y por qué.

---

## 2026-09-04, 21:37 — Dos programadores arreglaron el mismo selector de dos formas, y solo una funciona

**Medido por el supervisor sobre el paquete publicado**, arrancando
`Fichas-entrega\Fichas-nuevo\Fichas.exe` con `--carpeta-de-datos` sobre una
carpeta temporal, navegando a Importar por automatización de interfaz y pulsando
sus dos botones:

```
botones en la pantalla de Importar: 4
   'Open Navigation'   ''   ''   'Detener'
pulso el boton de elegir archivos  -> ventanas del proceso tras pulsar: 1
pulso el boton de la carpeta       -> ventanas del proceso tras pulsar: 1
proceso vivo: True
```

**Ningún cuadro se abre. El arreglo por los selectores del Windows App SDK NO
funciona en el paquete publicado**, aunque su programador midió que sí en su
árbol. El otro programador, el de Reportes, llegó a la conclusión contraria
midiendo los dos caminos, y resolvió el suyo con el cuadro de `comdlg32` de
Win32, que es el que sí abre.

**Decisión del supervisor:** sobrevive el de `comdlg32`. Importar pasa a usarlo,
y el ayudante sube a la cáscara para que no haya dos.

**Y un defecto que salió de camino, medido arriba:** los dos botones de Importar
**no tienen nombre**, igual que pasaba en Corrección. Por eso ni mi sonda podía
encontrarlos por su texto. Un lector de pantalla tampoco.

**La lección, y es del supervisor:** dos agentes en paralelo resolvieron el
mismo problema sin saberlo, con dos respuestas incompatibles, y una era falsa.
El reparto por terrenos evita que se pisen los archivos; **no evita que
resuelvan lo mismo dos veces**. Cuando un fallo es de la plataforma y no de una
pantalla, el arreglo se encarga UNA vez y se pone en la cáscara.

---

## 2026-09-05 — Cómo se mide un arrastre, y por qué un hallazgo de QA era medio falso

**Lo devolvió el programador de Corrección y el supervisor lo da por bueno
porque trae las tres mediciones al lado.** QA midió el arrastre del visor
moviendo el cursor con `SetCursorPos`, que **teletransporta el puntero pero no
inyecta un suceso de entrada**. Una aplicación WinUI 3 lee la entrada por
punteros, así que no se entera de que el ratón se movió. Su control negativo
—un clic en «Alejar», que sí bajó el zoom— no lo cazaba porque **un clic sí se
inyecta**.

```
QA (SetCursorPos), código original ....... H=22,34 %  V=10,21 %  → idéntico
él (SetCursorPos), código original ....... H=22,34 %  V=10,21 %  → idéntico (reproducido)
él (mouse_event),  código original ....... H=22,34 % → 51,05 %   V=10,21 % → 19,93 %
```

**Regla para cualquiera que mida una ventana:** el ratón se mueve con
`mouse_event` o `SendInput`, nunca con `SetCursorPos`; y una prueba de arrastre
lleva su control positivo, que es decir cuántos píxeles se pidieron mover y
cuántos se movieron.

**Pero el hallazgo no era del todo falso, y esa es la parte que importa:** al
medirlo bien aparecieron cuatro averías reales que nadie había visto —llegaban
2 de 8 movimientos, se sumaban saltos en vez de ir a una posición absoluta, no
había barras al 200 %, y cada arrastre se anotaba dos veces—. **QA se equivocó
en el método y acertó en que había algo roto.**

## 2026-09-05 — El cursor de esta máquina está congelado, y no lo congeló el proyecto

**Medido por el supervisor**, y hace falta decirlo porque cambia lo que se puede
medir aquí:

```
antes: 726,472   tras pedir 400,400: 726,472
boton izquierdo pulsado: False   boton derecho: False   captura del raton: 0
tras soltar los botones, liberar ClipCursor y desbloquear la entrada: 726,472
```

El cursor **no se mueve ni pidiéndoselo**, no hay ningún botón trabado, nadie
tiene la captura, y no es un `ClipCursor` ni un `BlockInput` de nadie. **No es
del programa ni de los agentes**: ninguna de las tres causas que un automatismo
puede dejar detrás está presente. **Es de la máquina, y el dueño tiene que
saberlo**: si su puntero no responde, se arregla desconectando y volviendo a
conectar el ratón, o reiniciando.

**Consecuencia para el proyecto:** mientras siga así, **nadie puede medir un
arrastre real en esta máquina**. Las cifras del arrastre del visor son del
paquete inmediatamente anterior al último, y quedan como «muy probable, no
medido sobre el final».

## 2026-09-05 — La prueba inestable de Reportes no se reproduce sola

Dos programadores la vieron caer bajo carga (76 de 77 y 79 de 80). **Medida por
el supervisor tres veces seguidas con la máquina tranquila: 80 de 80 las tres.**
Es una prueba de rendimiento que mide reloj de pared y se contamina cuando otro
proyecto corre OCR en paralelo. Queda anotada: **una prueba que mide tiempo real
no puede convivir con una suite que compila y hace OCR a la vez.**

---

## 2026-09-05 — Lo que el dueño pidió probando el programa: seis cosas, y una es una fase entera nueva

Sus palabras, recogidas mientras lo usaba. Las cuatro primeras son defectos o
huecos; las dos últimas cambian el modelo de trabajo y son del planificador.

**1. No puede firmar lo que él mismo escribe.** *«Solo me permite firmar en
comprobar lo que [hay]; cuando está en blanco no me deja firmar si coloco la
información que está pidiéndome».* Un campo que llegó vacío, relleno a mano por
él, no se puede dar por bueno. Encargado.

**2. Falta decir «esto no está en el papel».** *«Falta el botón de "esta
información no es necesaria" para guardar el documento».* La columna ya existe
en la base desde la versión de Python: `procedencia_campo.ausente_en_el_papel`.
Encargado.

**3. No hay forma de borrar.** *«Necesito un botón para dejar todo en limpio y
eliminar todo, porque me dejaste muchos documentos de prueba que no sé cómo
borrar».* Comprobado por el supervisor: no existe ningún puerto de borrado en el
C#, **y en el Python tampoco**; solo se archiva, que no borra. Su base tiene
ahora 2 casos de prueba del 3 de septiembre. Encargado, con copia previa y
confirmación con el número delante: **borrar es la única acción que pregunta**.

**4. El Excel que devuelve el compañero no cambia nada visible.** *«Cuando tomar
el Excel del agente de vuelta no hace ningún cambio en el sistema. Debe hacer un
cambio de si todo está completo de la persona».* Lo que ese Excel trae tiene que
verse **por persona**, no solo en un resumen de la importación.

### 5. La pestaña de revisar, para saber que un paquete sale completo

*«Yo debo tener la pestaña de revisar todos los PDF, de que todas las
informaciones estén colocadas en el sistema, para que se pueda generar los
paquetes de los agentes con toda la información».* Antes de mandar trabajo a un
compañero, él quiere ver de un vistazo qué documentos están completos en el
sistema y cuáles no, y que el paquete no salga cojo.

### 6. La segunda fase: los gerentes, y el administrador que completa

Esto no existe en ninguna parte del programa ni del plan, y es una capa nueva
de trabajo. Sus palabras:

*«Si yo creo un paquete para mí y lo subo, debe hacer cambio de si está completo
o no. Si el agente no pudo comunicarse con el líder y faltan cambios, lo que se
hará es que se sube el documento al sistema y yo creo otro paquete para los
gerentes, para que ellos puedan comunicarse con los líderes de estaca y
distrito, de lo que los agentes no pudieron. Es como la segunda fase. Así que
debo crear reporte para ellos con los comentarios de los agentes.»*

*«Yo solo estoy para verificar que todo esté correcto con el sistema y los PDF.
Yo puedo completarlos también, los paquetes, desde el sistema sin pasar la
verificación, y cuando pase eso debe decir "el administrador lo hizo".»*

**Lo que esto añade al modelo, y lo tiene que instrumentar el planificador:**
- Un segundo nivel de destinatario, el **gerente**, distinto del compañero, que
  se ocupa de lo que el compañero no consiguió y habla con líderes de estaca y
  de distrito.
- Un **paquete de segunda vuelta** que se arma con lo que quedó sin resolver, y
  un **reporte para el gerente que lleva los comentarios de los compañeros**.
- Que **Miguel pueda completar un caso sin pasar por la verificación**, y que
  eso quede escrito con esas palabras: **«el administrador lo hizo»**. Es una
  firma distinta de la suya como verificador, y no se mezcla con ella.

---

## 2026-09-05 — El calendario deja de ser adorno: es por donde se trabaja

Sus palabras: *«El calendario no puede estar de lujo. Cuando el calendario ponga
un grupo de personas en una fecha, yo debo poder darle clic y ver solamente al
grupo de personas que viajará en esa fecha, con su PDF. Y al darle clic me
despliega la opción de verificar la información, también permitirme asignarlos a
los agentes. Al recibirlo, en el calendario también puede decir el estado: no
completado, no se pudo comunicar con el líder, o el líder no lo hizo. Porque la
fecha es súper importante: así puedo ver los grupos por fechas y saber con qué
grupo trabajar.»*

**Comprobado por el supervisor antes de encargar nada:**
- `grep` de `Click`, `Tapped`, `AlPulsar` y `Seleccion` sobre
  `csharp/Fichas/Fichas.App/Inicio/CalendarioDelMes.cs`: **sin una sola
  coincidencia**. El calendario de hoy no responde al clic. Él tiene razón: es
  adorno.
- El calendario **solo conoce casos y una cuenta de personas**
  (`CalendarioDelMes.cs:84-85`), no las personas.
- **Los tres estados que pide NO existen.** Hoy solo hay `completa` y
  `no_completa` (`Fichas.Contratos/Modelos/Enumeraciones.cs`), y
  `grep -i "no se pudo comunicar|no contesto|sin respuesta|lider no"` sobre todo
  el C# no devuelve nada.
- Dónde podrían vivir, y hay que decidirlo, no inventarlo: `personas` ya tiene
  `estado_propuesto`, `nota_companero`, `motivo_no_viajo` y `llamo_al_lider`; y
  la tabla `contactos` guarda por qué canal se habló y si `respondio`.

**Lo que esto significa, y por eso no es un arreglo pequeño:** el dueño está
diciendo cuál es su unidad de trabajo real. No es el documento suelto: **es el
grupo que viaja un día concreto.** Abre el calendario, ve qué grupos salen esta
semana, entra en uno, y desde ahí verifica, asigna y ve cómo va. El resto del
programa está construido alrededor del documento; esta petición reordena la
entrada.

**Va junto a la petición de los gerentes (entrada anterior): las dos son del
planificador y las dos tocan el modelo, no una pantalla.**

---

## 2026-09-05 — «Listo para asignar» NO es «verificado», y el dueño lo precisó a tiempo

El supervisor encargó que, tras firmar Miguel todos los campos y guardar, el pie
dijera «listo para asignar». **El dueño lo corrigió en el acto y tenía razón:**

*«Yo doy ese veredicto cuando el sistema no completa todos los campos. Si el
sistema escanea y verifica todos los campos sin mi intervención, debe decir
"listo para asignar": el sistema llenó todos los campos y a mí solo me debería
dejar verificarlo.»*

**Lo que estaba mal en el encargo:** ponía la firma de Miguel como condición
para poder asignar. Eso le obliga a pulsar campo por campo en documentos que el
programa leyó enteros y bien, que es precisamente el trabajo que este programa
existe para ahorrarle. Con 3 000 documentos, eso son miles de pulsaciones para
confirmar lo que ya estaba bien.

**Las dos cosas, separadas, y no se mezclan nunca:**

| | Qué es | Quién lo pone |
|---|---|---|
| **«Listo para asignar»** | el sistema llenó **todos** los campos, ninguno vacío, ninguno dudoso, ninguno tachado sin corregir | **el programa, solo**, al terminar de leer o al guardar |
| **`verificado = 1`** (la firma «Todo correcto») | Miguel miró ese campo y responde por él | **Miguel, siempre**, nunca automático |

**Esto NO rompe la regla permanente 5, y hay que decir por qué:** la regla dice
que nada se marca como **verificado** automáticamente, y eso sigue igual: ni un
campo pasa a `verificado = 1` sin que él lo firme. «Listo para asignar» es otra
cosa: es una **lectura del estado**, la respuesta a «¿le falta algo a este
documento?», y esa pregunta la puede contestar el programa porque solo mira si
hay huecos.

**Su papel, con sus palabras:** *«a mí solo me debería dejar verificarlo»* y
*«yo solo estoy para verificar que todo esté correcto con el sistema y los
PDF»*. Su firma es para **lo que el sistema no pudo**, y para cuando él quiera
responder por algo. No es un peaje para trabajar.

**Consecuencia:** un documento leído entero se puede asignar sin que él toque
nada. Uno con huecos dice cuántos le faltan y no sale como listo.

---

## 2026-09-05 — Lo que el dueño pide mientras habla, y esta vez el supervisor solo escucha

Él pidió: *«Primero espera todas mis sugerencias y mis informaciones, y luego lo
programas. Sería más fácil para ti.»* Tiene razón, y el supervisor paró de abrir
trabajo. Se deja terminar solo lo que ya estaba en marcha. **Esta sección se va
llenando con sus palabras según las dice; no se encarga nada hasta que él diga
que ha terminado.**

### Organizar por carpetas, y poder bajarlas al escritorio

*«Permíteme también, en la ventana de revisión, organizar todos por carpetas.
Ejemplo: una carpeta con el mes, dentro carpetas con, por ejemplo, "grupo de
septiembre 17", "grupo de septiembre 2", y dentro de las carpetas otras carpetas
con el nombre y número de la unidad, y dentro el paquete de personas de la
unidad que viajará. Y permite descargarlas por mes en esa organización y carpeta
creada, para yo tener todo organizado. Me explico: descargar ese conjunto de
carpetas a mi escritorio en esa organización.»*

La forma que pide, tal cual:

```
Septiembre/
  Grupo del 17 de septiembre/
    700001 · Castries Branch/
      (las personas de esa unidad que viajan ese día, con su documento)
  Grupo del 2 de septiembre/
    ...
```

**Dos cosas en una, y conviene no confundirlas:** que la pantalla de revisión se
vea así, agrupada por mes, luego por fecha de viaje, luego por unidad; y que
**esa misma estructura se pueda volcar a su escritorio como carpetas de verdad**,
un mes entero de una vez.

### Y una precisión suya que reduce el trabajo a la mitad

*«Y de hecho tienes el sistema de revisar documento, solo falta organizarlo por
fecha y grupos.»*

Tiene razón, y conviene decirlo porque cambia el tamaño de lo que viene.
**Comprobado por el supervisor** sobre `Fichas.App/Revisar/`: la pantalla ya
existe y ya trae tarjeta por documento, buscador por caso, unidad, nombre o
cédula, la palabra del estado, la marca de duplicado, los tableros con sus
pastillas, selección múltiple y archivar en lote. Y **ya sabe de la fecha**:
`TableroDeRevisar.cs:172-173` guarda `FechaDeViaje` y `FechaYaPasada`, y
`PaginaDeRevisar.xaml.cs:131` ordena por `FechaViaje`.

**Lo que falta no es la pantalla, es la agrupación**: verla por mes, luego por
fecha de viaje, luego por unidad, como las carpetas que pidió. El dato ya está
donde hace falta.

### Inicio se queda con dos cosas, y lo incompleto se va a su propia ventana

*«Haz una mejor gestión de las notificaciones. Lo único que quiero ver en Home
es lo que está listo para asignar y lo que está asignado a los agentes. Luego
crea una ventana de revisar lo que no está completo, y ponlo por grupo de
fechas.»*

Y acto seguido, la razón, que vale más que la petición:

*«No quiero revisar gente que viaja en noviembre estando en septiembre.»*

**Comprobado por el supervisor** con `grep` sobre
`csharp/Fichas/Fichas.App/Inicio/PaginaDeInicio.xaml`, los rótulos que hay hoy
en Inicio: «Personas por viajar», «Con la recomendación completa», «Hojas sin
devolver», «Viajaron sin verificar», «Por verificar antes de que salgan», «Sin
fecha de viaje» y «El equipo», más el calendario. **Ninguno de esos es lo que él
pide ver.**

**Lo que quiere en Inicio, y solo eso:**
1. Lo que está **listo para asignar**.
2. Lo que está **asignado** a los agentes.

**Y una ventana aparte** para lo que no está completo, **agrupado por fecha de
viaje**.

**Lo que hay detrás, y por qué esto no es un capricho de pantalla:** él trabaja
por tandas de fecha. En septiembre le estorba todo lo de noviembre, aunque esté
incompleto, porque no es su turno todavía. Es la tercera vez que dice lo mismo
desde un ángulo distinto: el calendario como entrada, las carpetas por mes y
fecha, y ahora esto. **La fecha de viaje no es un dato del caso: es el eje del
programa.**

### Archivar: fuera de la vista, dentro del histórico

*«Algo más: cuando yo archive, debe salir del sistema visible, pero se queda
como histórico para los reportes.»*

**Esto cierra una pregunta que el programa ya tenía mal contestada, y en dos
sitios a la vez.** Lo midió el programador que arregló el informe de los jefes:
archivar tres casos dejó el selector de Corrección en **13 de 16** y la pantalla
de Revisar en **16 de 16**. O sea que hoy un caso archivado desaparece de unas
listas y sigue en otras, sin criterio. Su regla lo resuelve: **de todas.**

**Y descubre un error del supervisor.** El criterio que escribí para la pantalla
de Asignar decía «todos los casos ofrecidos, **incluidos los archivados**», y así
se construyó y se midió: «18 casos en la base · 18 ofrecidos». Yo lo saqué de su
requisito 8, «libre para asignar, cualquier caso a cualquier compañero, sin
filtro de estado». **Estiré su regla más de lo que él pedía:** lo que él quería
era que no le filtraran por «verificado» o «pendiente», no que le ofrecieran
trabajo ya terminado. Un caso archivado no se asigna: está cerrado.

**Queda así, y si me equivoco él lo dirá:** archivado sale de Inicio, de Revisar,
de Asignar y del selector de Corrección. Sigue entero en la base, sale en el
histórico y cuenta en los reportes, y en el calendario aparece con la palabra
ARCHIVADO, que es lo que él pidió el 2026-09-03. Desarchivar tiene que ser
posible: hoy no existe y el Python sí lo tenía.

### Categorías de agentes, y la escalera del que no consiguió hablar

*«Es importante tener un informe por agente también: lo que hicieron los agentes
en ese mes y lo que hicieron en esa semana, qué hicieron. Quiero poner los
agentes por categoría. Ejemplo: los agentes categoría 1 no pudieron comunicarse
con los líderes, debo pasarlo a los agentes de categoría 2; si los agentes de
categoría 2 no pudieron, a los agentes de categoría 3. Así puedes agregarle a
los gerentes categoría y a los agentes categorías.»*

**Esto generaliza lo que él mismo había pedido antes y lo mejora.** La entrada
anterior recogía «los gerentes» como una segunda fase especial. Con sus palabras
de ahora, **un gerente no es una cosa aparte: es una categoría más de la misma
escalera.** El modelo es uno solo:

- Cada compañero tiene una **categoría**, un número.
- Un caso empieza en la categoría 1.
- Si en esa categoría **no se consiguió hablar con el líder**, el caso **sube a
  la categoría siguiente**, y se le puede armar su paquete a quien esté ahí.
- La escalera no tiene un final fijo en dos peldaños: él habla de 1, 2 y 3, y
  dice «así puedes agregarle a los gerentes categoría», o sea que el número de
  peldaños lo pone él, no el programa.

**Y el informe por agente, que es lo primero que dice:** qué hizo cada compañero
**en el mes** y **en la semana**. Hoy no existe: `IReportes` tiene un
`GenerarReporteDeCompanero` que el programador de Reportes escribió sin original
que portar y que **nunca llegó a tener botón**, y el programa viejo del dueño no
tenía nada parecido.

**Lo que hay que decidir y no inventar:** qué cuenta como «no se pudo hablar con
el líder» —hoy no existe ese estado, ver la entrada del calendario—, si sube el
caso entero o solo las personas que quedaron sin resolver, y si el nombre del
compañero anterior se conserva cuando el caso sube de categoría. Lo tercero
tiene respuesta ya dicha por él en otro contexto: nunca se pierde el rastro de
quién hizo qué.

### Elegir el tema, claro u oscuro

*«Dame opción también de cambiar el tema del sistema, si es negro o oscuro.»*

Que él elija, y que el programa lo recuerde entre sesiones. Hoy sigue el tema de
Windows, y eso ya causó un defecto: el programador de Inicio midió que con
Windows en oscuro el botón «Hoy» salía blanco sobre fondo claro, ilegible, y lo
tapó fijando esa pantalla en claro. Con la elección hecha por él, ese parche
sobra.

**Encaja con lo que ya dijo del calendario** («ver los grupos por fechas y saber
con qué grupo trabajar»): las dos peticiones dicen lo mismo desde dos sitios.
Su unidad de trabajo es **fecha de viaje → unidad → personas**, y el documento
suelto está por debajo de eso.

---

## 2026-09-05 — ⚠️ EL DUEÑO EXPLICA SU TRABAJO, Y NO ES EL QUE EL PROGRAMA CREE

**Esta es la entrada más importante del proyecto.** Él pidió que no se lanzara
nada y que solo se anotara. Así queda.

**Su primera frase:** *«el programa es confuso, muy confuso»*.

### Lo que hace de verdad, con sus palabras

*«Yo recibo los PDF de otro departamento, ellos ya hicieron su trabajo. Ahora yo
tengo que ir al sistema de la iglesia y verificar que los hermanos que están en
el PDF tengan la recomendación hecha en el sistema, que es las preguntas que
están en el paquete de los agentes, por eso son importantes.»*

*«Si no está completa la recomendación, yo debo llamar al obispo, saber cómo le
puedo ayudar a completar ese caso. Es como un sistema de tickets: literal, yo
debo resolver los tickets de la gente que viajará en las fechas, antes de que
viajen.»*

*«Yo puedo asignar a que otras personas revisen la recomendación del obispo para
que ellos puedan ayudarme, porque pueden ser 100 casos para mí solo. Por eso son
los paquetes.»*

**Lo que esto significa, y es un cambio de modelo, no una lista de arreglos:**

| El programa cree que… | Y de verdad es… |
|---|---|
| el trabajo es **extraer los datos del PDF** | el PDF **ya viene hecho por otro departamento**: sus datos solo sirven para **saber a quién hay que verificar** |
| «listo» = **nuestros campos están llenos** | «listo para viajar» = **la recomendación está confirmada en el sistema de la Iglesia**, que es otro sistema y el programa no lo ve |
| verificar = **Miguel firma campo por campo** | verificar = **Miguel entra al sistema del obispo y comprueba la recomendación de esa persona** |
| la unidad de trabajo es **el documento** | la unidad es **el ticket de una persona que viaja una fecha**, y se cierra antes de que viaje |
| los agentes **rellenan datos** | los agentes **verifican recomendaciones en el sistema del obispo, en lugar de él**, porque son cientos |

**Sus palabras que lo cierran:** *«es un sistema de gestión de tickets y casos»*.

### Los once arreglos que pide, con sus palabras

1. **«No puede poner los PDF listos para viajar porque no se ha verificado la
   recomendación en el sistema del obispo, que es lo que realmente verifico yo».**
   O sea: lo que hoy dice «listo para asignar» está mirando lo que no es.
2. **«En Corrección solo debe estar lo que falta información».** Hoy salen todos
   los campos.
3. **«En Revisar debo poder dar clic al documento y abrir otra ventana donde
   está la información, comentarios o preguntas, para llegar a si se completó o
   no la información en el sistema del obispo».**
4. **«Cada caso que tengo en revisión debe poder entrar y completar las
   preguntas que dicen si está listo para viajar o no».**
5. **«Muchos no tenían unidad; cuando entré al documento, la unidad sí
   estaba».** Un defecto de lectura o de pintado, medible.
6. **«Colocarlo todo en Revisar me hace el trabajo difícil: debes agruparlo por
   fecha, y lo que no se reconozca la fecha, ahí mismo en revisión, en una
   sección que diga "revisar este documento" para que se pueda ir a una fecha».**
7. **«En el Home debe decirme: las personas del grupo del 17 de septiembre,
   faltan 3, 4 o 5 personas que la recomendación para el templo no está
   confirmada. Ahí yo puedo verificarlos y ver en el sistema de la Iglesia».**
8. **«Permíteme nombrar las carpetas; y si cargo un grupo de carpetas, que tome
   la información de las carpetas que cargo, porque muchas veces yo mismo
   organizo la carpeta y lo único que hace falta es cargarla al sistema».**
9. **«Si Sandy me pasa su paquete con todas las preguntas llenas, formularios
   completos, y yo lo subo, automáticamente debe pasar a "listo para viajar" en
   base a la respuesta de Sandy. Si no lo está, la respuesta de Sandy debe
   decirme: Sandy no pudo verificar todos, por favor verifica qué falta».**
10. **«Y si falta algo que debe ir a un gerente, debe poder asignarle el caso o
    el ticket a un gerente para que lo resuelva. Para eso creo un paquete con la
    respuesta de Sandy y señalando lo que le hace falta, para poder cerrar el
    ticket o el caso».**
11. **«Documentos que tienen el número de caso iguales puede significar que
    viajarán en el mismo grupo. Es importante saber esto».** Ojo: esto matiza la
    decisión del 2026-09-03, que dijo que el número de caso NO es identidad de
    familia. Sigue sin serlo, **pero sí es una pista de grupo de viaje**, y él
    quiere verla.

### El ticket es por persona, y el grupo es cómo viajan

Su respuesta, con sus palabras: *«El ticket es de cada persona o familia, porque
muchas veces viajan en familia para hacer ordenanzas que están en el PDF. **Es
por persona que se revisa la información.**»*

**Queda fijado así, y es la pieza que faltaba para ordenar todo lo demás:**

- **La unidad de trabajo es LA PERSONA.** Un ticket por persona: ¿tiene su
  recomendación confirmada en el sistema del obispo, sí o no?
- **La familia y el grupo son cómo viajan, no cómo se revisa.** Un documento con
  diez personas son diez tickets, y pueden estar tres resueltos y siete no.
- **Las ordenanzas son de cada persona** y ya están en la base, una columna por
  ordenanza (`ord_recibir_propias`, `ord_observar_sellamiento`, `ord_traductor`,
  `ord_investidura`, `ord_sellamiento_esposos`, `ord_sellamiento_hijo_padres`),
  medido por el supervisor con `pragma table_info(personas)`. Están **apagadas**
  desde la importación y sin calibrar (ver la entrada del 2026-09-04 sobre
  `LECTURA_DE_CASILLAS_ACTIVA = False`), pero el sitio existe.

**Esto contradice cómo está construido casi todo el programa hoy**, que cuenta y
enseña documentos: Inicio, Revisar, el calendario y los tableros hablan de
documentos, no de personas. Su frase del Home lo dice sin rodeos: *«faltan 3, 4
o 5 PERSONAS que la recomendación no está confirmada»*.

### Las seis preguntas SON las del sistema del obispo, confirmado por él

Se le enseñaron las seis tal como salen del código
(`Fichas.Reportes/Reglas/Pasos.cs`) y contestó: *«Sí, son esas seis las que veo
en el sistema»*.

```
1. Preparación          4. Acciones requeridas
2. Información          5. Entrevistas
3. Cita del templo      6. Listo para el templo
```

**Esto cierra el modelo, y de golpe encaja todo lo que él venía pidiendo:**

- **El paquete del agente ya preguntaba lo correcto.** No hay que inventar
  ninguna pregunta: son literalmente lo que él mira en el sistema del obispo.
  Por eso dijo *«son las preguntas que están en el paquete de los agentes, por
  eso son importantes»*.
- **Un ticket es una persona, y su estado es si esas seis están confirmadas.**
  Ni más ni menos. Lo demás del programa —campos leídos del PDF, firmas de
  Miguel— es material de apoyo para saber a quién verificar, no el veredicto.
- **«Listo para viajar» se calcula de esas seis**, no de si nuestros campos están
  llenos. Eso es exactamente lo que él dijo que el programa hacía mal.
- **Y por eso el paquete que devuelve el agente puede cerrar tickets solo:**
  *«si Sandy me pasa su paquete con todas las preguntas llenas… automáticamente
  debe pasar a listo para viajar en base a la respuesta de Sandy»*. No es una
  excepción a la regla permanente 5: no es una firma de campos, es el veredicto
  de quien verificó, con su nombre, que es lo que ya se decidió el 2026-09-03.

**Falta por preguntarle una sola cosa, y no bloquea:** si «listo para viajar»
exige las seis o basta con la sexta, que se llama «Listo para el templo». Se
programa con las seis y él lo corrige al verlo.

### Lo que el supervisor tiene que revisar antes de programar nada

- **«Listo para asignar»**, tal como se programó hoy, mira si nuestros campos
  están llenos. Él dice que eso no es lo que decide. **Hay que rehacer ese
  concepto**, no ajustarlo.
- **Las preguntas del paquete de los agentes son el corazón del programa**, no un
  anexo: son las preguntas de la recomendación en el sistema del obispo. Hoy son
  seis casillas heredadas del formulario.
- **Un ticket se cierra o no se cierra**, y eso es un estado que hoy no existe
  con ese nombre.

---

## 2026-09-05 — Cómo quiere el dueño que se trabaje a partir de ahora

Sus palabras: *«No me pidas aprobación de agente. Termina todo y dime solo
cuando esté listo.»*

**Qué cambia para el supervisor:**
- **No se le pide permiso para lanzar un agente ni para fusionar una entrega.**
  El reparto de terrenos, el orden y el arranque los decide el supervisor.
- **No se le cuenta cada entrega.** Se trabaja y se le avisa cuando el programa
  está listo para que lo abra.
- **Lo que SÍ sigue llegando a él, sin esperar a que termine nada:** que algo
  suyo esté en riesgo, un hallazgo que cambie lo que él creía, y una decisión que
  solo él puede tomar y que bloquea. Eso no es pedir aprobación: es lo que un
  supervisor calla a su costa.
- **Lo que no cambia:** cada afirmación con su medición o dicha como cita, y la
  suite medida por el supervisor antes de darle nada.

---

## 2026-09-06 — Archivado desaparece de TODAS partes, y eso deshace una decisión suya del 3

Sus palabras: *«Si ya resolví un archivo y lo archivo, no debe aparecer en
notificaciones, debe salir del tablero, no se cuenta ya. Porque si se queda en el
tablero y dice archivado, lo que hace es que me confunda. Debe pasar a archivado
y no aparecer más en ningún lado.»*

**Esto deshace lo que él mismo pidió el 2026-09-03**, y hay que decirlo: aquel
día dijo *«lo que archivo debe verse en el calendario, debe decir archivado»*, y
así se construyó. **Manda lo de hoy**, que además trae el motivo: verlo marcado
no le ayuda, le confunde. Queda tachada la de septiembre 3.

**Medido por el supervisor, dónde sigue apareciendo hoy:**

| Dónde | Qué hace |
|---|---|
| El grupo del día | `LectorDeGrupos.DelDia(fecha, conArchivados = true)` — **por defecto los trae**, y desde el calendario se llega con ellos |
| El renglón de Asignar | `RenglonParaAsignar.cs:51` añade `« · archivado»` al texto |
| El calendario de Inicio | `ElSistemaDelObispo.cs:104`, `MarcaDeArchivado => "ARCHIVADO"`, y su propio comentario dice «se marca y NO desaparece» |
| Inicio y la ventana de incompletos | ya salen sin archivados |
| Asignar, en lo ofrecido | ya no se ofrecen |

**Lo que queda:** archivado no sale en ninguna lista, ni en el calendario, ni en
ningún contador. Sigue en la base y en los reportes y en el histórico, que es lo
que él pidió el mismo día: *«se queda como histórico para los reportes»*. Y
tiene que haber un sitio a propósito para verlos y desarchivarlos, que ya existe
en Revisar con su interruptor.

---

## 2026-09-06 — El dueño pide un flujo, no una pantalla: tres cosas medidas antes de programar

Tres mensajes suyos seguidos, mientras el programador del archivado ya estaba
trabajando. Se anotan con sus palabras porque las tres cambian pantallas que ya
están hechas y en uso.

### 1. Guardar tiene que llevarle al siguiente, y hace falta una pestaña para eso

Sus palabras: «Cuando algo no leído se lee y yo lo reviso y le doy guardar, debe
pasar a otro renglón de "listo para asignar" y me envía a mí a la pantalla donde
está todo lo que no está completo, para yo seguir trabajando. Te lo pongo así: un
flujo de trabajo donde yo vaya resolviendo casos de manera automática, vaya donde
tenga que ir. Y debe haber una tab solo para esto: completar información de
documentos que faltan.»

Lo que hay hoy, medido:

| Qué | Dónde | Qué hace hoy |
|---|---|---|
| El veredicto | `Fichas.App/Correccion/ModeloDeCorreccion.Guardado.cs:390` | `ListoParaAsignar => _caso is not null && _campos.Count > 0 && CamposQueLeFaltan == 0` — **ya existe y ya se dice en el acuse** |
| Qué pasa después de guardar | la misma pantalla | **nada**: se queda en el documento recién guardado |
| Las pestañas | `Fichas.App/Cascara/VentanaPrincipal.xaml:63-102` y `VentanaPrincipal.xaml.cs:116-124` | siete: Inicio, Importar, Corrección, Revisar, Asignar, Paquetes, Reportes. **No hay ninguna de «completar lo que falta»** |

Comando: `grep -n "ListoParaAsignar\|NavigationViewItem" ...` sobre esos archivos.

Lo que falta no es el veredicto: es el **encadenado**. El veredicto ya está y él
lo confirmó el 5 («si el sistema escanea y verifica todos los campos sin mi
intervención, debe decir listo para asignar»). Lo que no existe es que guardar te
saque de ahí y te ponga delante el siguiente.

**Decisión.** Una pestaña nueva, la octava, con la cola de lo que le falta
información. Se entra a un documento desde la cola, se corrige, se guarda; si
quedó completo pasa a «listo para asignar», **sale de la cola** y se abre el
siguiente sin volver atrás. Si no quedó completo, se queda en la cola. La
pestaña de Corrección sigue existiendo para ir a un documento suelto: la cola no
la sustituye, la usa.

⚠️ Esto NO toca la regla permanente 5. La cola encadena; **no firma nada**. La
firma de campos sigue siendo suya y sigue sin ser automática.

### 2. Revisar no puede abrir con todo dentro

Sus palabras: «O solo que me aparezca en "por corregir" los documentos a
corregir, no todos los documentos. Cuando doy click aparecen todos los PDF para
revisar; solo los que necesitan revisión son los que deben ser revisados, no
todos.»

Medido: `Fichas.App/Revisar/PaginaDeRevisar.xaml.cs:32` —
`private FiltroDeTarjeta _tableroALaVista = FiltroDeTarjeta.Todo;`. Los seis
tableros existen desde el 3 (`TableroDeRevisar.cs:9-28`: Todo, SinRevisar,
Incompletas, Completadas, SinAsignar, FechaPasada) y están bien; el defecto es el
que sobra.

**Decisión.** Revisar abre en lo que hay que revisar, no en «Todo». «Todo» sigue
estando, se pulsa; deja de ser lo primero que ve. Es el mismo motivo del
archivado de esta mañana: lo que no requiere trabajo suyo no debe ocupar la
pantalla donde decide qué trabajar.

### 3. Dos columnas fuera del paquete de los agentes

Sus palabras: «En los paquetes para los agentes o gerentes debe eliminar la
columna Fecha de solicitud, Estaca o Distrito a que va, porque son informaciones
que no me pide verificar.»

Medido en `Fichas.Paquetes/Columnas.cs:144` y `:147`, dentro de las 18 columnas.
Y el comentario de `Columnas.cs:93` ya avisaba de que **salen siempre vacías**
porque la base no las guarda; se dejaban por fidelidad a la hoja del programa
viejo, y se escribían como «no consta».

Ese motivo ya no vale: él dice que no las verifica. Una columna que siempre dice
«no consta» y que nadie tiene que mirar es ruido en una hoja que el compañero
rellena a mano.

**Decisión.** Fuera las dos. 18 → 16 columnas. Nadie fuera de
`Fichas.Paquetes/` las lee: comprobado con
`grep -rn "fecha_solicitud|\"estaca\"" --include=*.cs` — solo salen en
`Columnas.cs`, `LibroDeTrabajo.cs:49,52`, `Paquetes.cs:242` y dos pruebas.

⚠️ La hoja que sale y la que vuelve tienen que seguir siendo la misma: el lector
busca las columnas por su nombre, así que un paquete generado ANTES de este
cambio sigue teniendo esas dos columnas. Hay que medir que uno viejo se sigue
leyendo, o decir que no.

---

## 2026-09-06 — ⚠️ EL DUEÑO MUEVE LA REGLA 5: no revisa documentos, revisa paquetes

Esta entrada toca una **regla permanente** de CLAUDE.md, la 5. No se escribe a la
ligera y va con sus palabras enteras, porque quien la lea dentro de un mes tiene
que poder decidir si sigue teniendo sentido.

### Lo que dice hoy, seguido, sin cortar

> «La ventana de corrección no me sirve para nada porque me pone todo junto, y
> cuando corrijo no sale de corrección, se queda todo ahí, debe moverse. Si el
> sistema escaneó toda la información, toda, ¿para qué yo debo revisarlo? Si no
> estaría trabajando doble. Debería estar en una sección que diga preparado para
> asignar.»
>
> «El sistema es para escanear información. Si yo tengo que verificarla luego,
> ¿para qué me sirve el sistema si tengo que hacerlo igual?»
>
> «Lo que yo debo verificar es eso. Algo más: debe haber botones donde aplique
> "todo completo", igual en los paquetes, porque ir uno por uno si está bien pero
> no es suficiente. Lo que sí debo revisar sí o sí son los paquetes que vienen de
> los agentes, para evitar errores.»
>
> «Me equivoqué: lo que debo revisar son los paquetes de los agentes. Solo
> verificar las informaciones. Y ya.»

### Qué decía la regla 5 y qué dice a partir de hoy

CLAUDE.md, regla permanente 5: «Nada se marca como verificado automáticamente. El
sistema propone, Miguel confirma. Siempre.»

El 2026-09-05 él ya la había recortado una vez: «yo doy ese veredicto cuando el
sistema no completa todos los campos; si el sistema escanea y verifica todos los
campos sin mi intervención, debe decir "listo para asignar"». Hoy termina el
movimiento y dice **dónde** sí verifica.

**La regla 5 se queda, pero cambia de sitio.** El dueño no confirma documento a
documento lo que el escaneo leyó entero: confirma **los paquetes que vuelven de
los agentes**. Ahí sí, sí o sí, y él mismo dice por qué: «para evitar errores».

Lo que NO cambia, y esto es lo que impide que esto se convierta en un daño:

- Un documento solo puede llamarse completo si de verdad se leyeron **todos** sus
  campos y cada uno pasó su comprobación de formato (`numero_caso`, `mrn`,
  `unidad_numero`, `fecha_viaje` y la regla del mes contra el AAMM del caso, que
  están más abajo en este mismo documento). Lo que no se leyó, lo que quedó con
  poca confianza y lo que no pasa su comprobación **sigue yendo a la cola** para
  que él lo complete a mano. No hay «completo por defecto».
- Sigue prohibida la IA generativa para leer o adivinar un campo (regla
  permanente 1). Nada de esto la reabre.
- Firmar campos («Todo correcto», `verificado_por`) sigue siendo suyo. Lo que
  desaparece es la **obligación** de pasar por cada documento que el sistema leyó
  entero, no la posibilidad de mirarlo.

### Lo que hay que construir, con sus palabras al lado

| Qué | Sus palabras |
|---|---|
| Lo que el escaneo leyó entero va solo a una sección «preparado para asignar», sin pasar por Corrección | «debería estar en una sección que diga preparado para asignar» |
| Corrección deja de ser una pantalla con todo junto: solo lo que le falta algo | «me pone todo junto», «cuando corrijo no sale de corrección» |
| Un botón que aplique «todo completo» a varios de golpe, no uno por uno | «debe haber botones donde aplique todo completo» |
| El mismo botón en los paquetes | «igual en los paquetes» |
| El paquete que vuelve del agente es lo que él revisa sí o sí | «lo que sí debo revisar sí o sí son los paquetes que vienen de los agentes» |

### Y dos cosas más que dijo en la misma tanda

**En Asignar quiere el nombre de la persona, no el barrio.** Sus palabras: «En
asignado a los agentes necesito ver cuando se completa el nombre de las personas,
no de la rama o barrio». Medido: `Fichas.App/Asignar/RenglonParaAsignar.cs:44`
—`Titulo => $"{NumeroDeCaso} · {Unidad}"`— y el renglón no tiene ningún campo con
el nombre; lleva `Unidad`, `FechaDeViaje`, `Personas`, `PalabraDelEstado` y
`AsignadoA`, y ninguno es quién viaja. Es coherente con lo que él ya explicó el 5:
el ticket es por persona, no por papel.

**En Revisar no hay color de estado.** Sus palabras: «cuando estoy en revisar no
hay nada que me indique color o algo, si este está completo o no». Medido:
`Fichas.App/Revisar/TarjetaDeDocumento.cs:169` tiene `PalabraDelEstado`, que es
texto, y el comentario de `RenglonParaAsignar.cs:65` dice «el color nunca va solo
(mockup v2)» — la palabra está, el color no. La regla que se respeta al añadirlo
es esa misma: color **y** palabra, nunca color solo, porque un daltónico o una
pantalla mala dejarían la tarjeta muda.

### Lo que queda esperando una foto suya

Sus palabras: «lo que necesito son las respuestas, las de si está completa la
recomendación. Hice el ejercicio y solo marca completo cuando se completan las
preguntas que sí aportan, pero que no importan cuando estaba lleno las preguntas
que necesitaba. Ejemplo, te mando una foto ahora.»

**No se programa nada de esto hasta ver la foto.** Lo que se entiende es que no
las seis preguntas pesan igual para dar una recomendación por completa, y cuáles
pesan es exactamente el tipo de cosa que no se adivina: si se elige mal, una
persona viaja con la recomendación incompleta. Queda anotado y a la espera.

---

## 2026-09-06 — Archivar gana a estar incompleto, y lo pasado completado se llama así

Dos frases suyas, seguidas, después de todo lo anterior:

> «Si yo lo archivo debe desaparecer aunque no estén completos, porque a veces lo
> archivo porque son fechas pasadas y ya los completé antes de crear el sistema.»
>
> «Debe decir fecha pasada completada.»

**Lo primero explica lo segundo, y explica algo que el programa entendía al
revés.** El programa trata «incompleto» como una condición que reclama trabajo, y
por eso lo incompleto pesa en las listas y en los avisos. Pero él archiva
precisamente casos que están incompletos **en la base y completos en la vida
real**: gente que viajó antes de que existiera este programa, cuyo trámite él ya
resolvió a mano. Para esos, incompleto no significa «falta trabajo»: significa
«el programa no lo vio».

**Decisión.** Archivar gana. Un caso archivado desaparece de todo aunque le falten
campos; no hay condición de completitud que lo retenga en ninguna lista, en
ningún contador ni en ningún aviso. Y donde un archivado sí se ve a propósito —el
histórico, los reportes, el interruptor de Revisar—, el que tiene la fecha de
viaje pasada se llama **«fecha pasada completada»**, no «incompleto», porque
llamarlo incompleto es decir de él algo que no es verdad.

⚠️ Esto NO deshace la regla de que un caso se archiva y nunca se borra
(`archivado = 1` con su fecha). Sigue entero en la base y sigue contando en los
reportes.

### Lo que está esperando turno, y por qué

Todo lo de abajo lo pidió hoy y NO se ha programado todavía, porque el archivo
que hay que tocar lo tiene otro programador abierto ahora mismo. Se anota aquí
para que no se pierda por eso.

| Qué pidió | Sus palabras | Dónde va |
|---|---|---|
| Archivar gana a incompleto, y «fecha pasada completada» | las dos de arriba | `Fichas.App/Grupo/`, `Fichas.App/Inicio/`, `Fichas.App/Revisar/` |
| En Asignar, el nombre de quien viaja en vez del barrio | «necesito ver cuándo se completa el nombre de las personas, no de la rama o barrio» | `Fichas.App/Asignar/RenglonParaAsignar.cs:44` |
| Corrección solo con lo que le falta algo; lo leído entero va derecho a «preparado para asignar» | «me pone todo junto», «debería estar en una sección que diga preparado para asignar» | `Fichas.App/Correccion/` |
| Color de estado en las tarjetas de Revisar | «no hay nada que me indique color o algo, si este está completo o no» | `Fichas.App/Revisar/TarjetaDeDocumento.cs` |
| Cuáles de las seis preguntas pesan para dar por completa una recomendación | «solo marca completo cuando se completan las preguntas que sí aportan… te mando una foto» | ⛔ **esperando su foto** |

---

## 2026-09-06 — Dos veredictos distintos de «listo para asignar», y un documento que no está en ninguna lista

Lo encontró el programador de la cola al construirla, y **el supervisor lo
verificó abriendo los dos archivos**, no leyendo su informe.

Hay dos sitios que deciden si a un documento le falta algo:

| Dónde | Qué mira |
|---|---|
| `Fichas.App/Correccion/ModeloDeCorreccion.Guardado.cs:364` → `EstadosDeCampo.cs:155-165` | vacío, forma, **confianza del OCR** (`:164`) y **tachón** (`:162`) |
| `Fichas.App/Grupo/LoQueLeFalta.cs:100` → `Mirar(...)` en `:104-113` | vacío y forma. **Nada más.** |

**Consecuencia medida, no supuesta.** Un campo que el OCR leyó con poca confianza
pero cuya forma es válida cuenta como «no falta» para `LoQueLeFalta` y como
**dudoso** para Corrección. Ese documento **no entra en la cola de Completar**
—que se construye con `LoQueLeFalta`— y **tampoco** sale como «listo para
asignar» —que lo decide Corrección—. No está en ninguna de las dos listas.

Es exactamente el daño para el que existe este programa, escrito en la primera
línea de CLAUDE.md: alguien llega al templo y no puede entrar porque su
recomendación estaba mal y nadie lo vio a tiempo. Un MRN leído con poca confianza
que por casualidad sale con la forma correcta es ese caso.

Las dos citan la misma frase del dueño del 2026-09-05 —«ninguno vacío, ninguno
dudoso»— y cada una entiende «dudoso» a su manera.

### Cerrado el 2026-09-06, y lo que salió al medirlo con los papeles del dueño

Hicieron falta **tres pases**. El primero midió el agujero y midió que no se podía
cerrar sin abrir un archivo congelado. El segundo confirmó la medición, encontró
una divergencia más, sembró lo que faltaba en la base inventada y volvió a parar en
el mismo candado —que el supervisor abrió entonces, dejando escrito que **no había
ninguna auditoría** sobre ese archivo, aunque el aviso del hook dijera eso—. El
tercero lo cerró.

**Lo que de verdad importa no son las seis divergencias.** Es lo que apareció al
importar los **siete escaneos reales del dueño** por la ventana:

```
listosAntes=7  listosAhora=5
caso 3  antes=listo  ahora=no  falta=[Cédula de ...]   mrn=###-####-####  confianza=0,52
caso 4  antes=listo  ahora=no  falta=[Cédula de ...]   mrn=###-####-####  confianza=0,57
```

**Dos cédulas de personas de verdad**, con la forma correcta, que la máquina leyó
mal, y que el programa **se estaba ofreciendo a mandar a un compañero** como si
estuvieran bien. Dos de siete. Esa proporción no se puede leer sin escalofrío: es,
literal, la primera línea de CLAUDE.md.

**Cómo queda hecho, y por qué esto no vuelve.** Las dos preguntas llaman ahora a
`EstadosDeCampo.EsDudoso`, **la misma función**. La divergencia deja de ser posible
por construcción, no por disciplina.

**Lo que cuesta**, medido a solas: Inicio 83,7 → 126,9 ms, la ventana de
incompletos 37,3 → 111,8 ms, con techo de 200. De eso, la lectura de procedencias
en bloque son 17,5 ms. La vía que ya existía daba 65,4 ms para 300 documentos
contra 1,9 ms en bloque, y hay prueba permanente que lo fija.

⚠️ **Y una cosa bien hecha que conviene copiar:** la prueba de tiempo **no afirma**
los 200 ms dentro de la suite, y lo dice por escrito, porque con la máquina cargada
pruebas que ese pase no tocó pasan de 12,9 a 95,1 ms. Afirma la **proporción** —leer
la procedencia cuesta menos de la mitad de Inicio— y deja el número absoluto para
la medición a solas. Es la forma honesta de escribir una prueba de tiempo aquí.

Suite entera tras fusionar, medida por el supervisor con `dotnet test Fichas.sln`:
21 + 46 + 103 + 130 + 169 + 724 + 63 = **1 256 pasadas, 0 omitidas, 0 rojas**.

**Decisión.** Un solo veredicto, y la regla es la larga: un campo leído con poca
confianza **es** dudoso. Cambiar esto mueve cinco pantallas, porque `LoQueLeFalta`
lo usan la cola, el calendario, la ventana de incompletos y las cifras de Inicio
(medido con `grep -rln "LoQueLeFalta"`). La cola va a crecer. Eso no es un fallo
nuevo: es el agujero saliendo a la luz.

### Tres cosas que los programadores devolvieron y decide el dueño

1. **Hay dos pantallas que listan lo incompleto**: la ventana de Inicio (agrupada
   por fecha, para mirar) y la pestaña Completar (cola, para trabajar). Si le
   sobra una, la que sobra es la de Inicio. Nadie la ha quitado.
2. **El botón de dar por bueno en bloque firma siete campos**, y dos de ellos
   —`unidad_numero` y `templo_nombre`— no viajan en el Excel del agente: se
   pintan en pantalla para que los vea antes de firmar. Si él quiere que el botón
   abarque solo lo que sale impreso en la hoja, es cambiar una lista.
3. **Cuáles de las seis preguntas pesan** para dar una recomendación por
   completa. ⛔ Sigue esperando su foto y no se programa sin ella.

---

## 2026-09-06 — Un campo tachado en el papel llega a la base como si fuera bueno

Lo encontró el programador que fue a unificar el veredicto, buscando otra cosa. El
supervisor lo verificó abriendo los archivos, no leyendo su informe.

- `Fichas.Lectura/Campos.cs:137` — `if (hayTachon) return CampoVacio(valorOcr, anuladoPorTachon: true);`,
  con su comentario: «Hubo tachón y nadie escribió el valor bueno. El dato de
  debajo está marcado como equivocado y **NO se usa**». Hasta aquí, correcto.
- `Fichas.Lectura/Extraccion.cs:336-338` — ese campo se construye con **siete
  argumentos posicionales**. `AnuladoPorTachon` es el noveno parámetro de
  `CampoPropuesto` (`Fichas.Contratos/Lectura/Lectura.cs:83`) y vale `false` por
  defecto. **No se pasa.** Dos líneas más abajo, en `:339`, el mismo código lee
  `campo.AnuladoPorTachon` para redactar el aviso: la información está ahí, se usa
  para el texto, y no viaja con el dato.
- `Extraccion.cs:307-308` lo explica: «se dice en el aviso, porque
  `CampoPropuesto` no tiene dónde ponerlo». **Era verdad cuando se escribió.** El
  sitio se añadió el 2026-09-04 y el comentario se quedó atrás.

**Todo lo de aguas abajo está construido y esperando una marca que no llega**,
medido con `grep -rn "AnuladoPorTachon"`: la columna en la base
(`RepositorioDeProcedencia.cs:106` y `:262`), `EstadosDeCampo.cs:71,135,162` y
`CamposDeLaHoja.cs:106,126,129`, que devuelve `null` cuando el campo está tachado.

**El daño.** El valor tachado entra como lectura normal de OCR. Si ese texto tiene
por casualidad la forma correcta —una cédula de 11 dígitos, una fecha bien
formada— el programa lo da por bueno y nadie lo mira. Es un dato que alguien tachó
en el papel **porque estaba mal**. Es la primera línea de CLAUDE.md, literal.

⚠️ **Y hay un segundo caso que NO es el mismo y no se puede arreglar igual:**
cuando hay tachón **y** alguien escribió el valor bueno al lado
(`Campos.cs:125-131`), `CampoExtraido` también lleva `AnuladoPorTachon: true`,
pero el valor que viaja es el bueno, el que escribió la persona. Marcarlo como
tachado tiraría un dato correcto, porque `CamposDeLaHoja.cs:106` devuelve `null`
en cuanto ve la marca. Las dos ramas necesitan decisión por separado.

### Arreglado el mismo día, y lo que se aprendió midiendo

El camino entero comprobado, no solo la extracción: importando por la ventana un
escaneo con la fila de la fecha tachada, `procedencia_campo.anulado_por_tachon`
valía **0 antes y vale 1 después**. `Fichas.Pruebas.Lectura`: 43 → 50, 0 rojas,
verificado por el supervisor con `dotnet test`.

**Las dos ramas NO se marcan igual, y el argumento no es de gusto.** La marca
significa «el valor que viaja está anulado», no «hubo un trazo rojo en esta fila»,
y lo fija el código que ya la lee: `CamposDeLaHoja.cs:106` devuelve `null` en
cuanto la ve, así que marcar la rama con corrección **tiraría el valor bueno que
escribió la persona**; y `EstadosDeCampo.cs:71` mira el tachón antes que el
origen, así que ese campo saldría en pantalla como «tachado, sin corrección», que
sería falso. Se marca la rama **sin** corrección. Y apareció una tercera que nadie
había mirado: el tachón sobre una banda sin nada legible, que salía como «no se
pudo leer del papel» siendo mentira.

**Lo que enseñaron los siete escaneos reales, medido con `pypdf`:** los siete
traen tachones —~~30 trazos rojos `/Ink` más un resaltador verde~~ → **29 rojos y
un resaltador verde**, corregido el mismo día por el programador de las personas,
que los contó uno a uno— y los siete se detectan. Pero **ninguno tiene un tachón sin corrección sobre un campo que esta
fase extraiga**: el único que cae en banda extraída es el de la fila de la fecha,
y en los siete lleva al lado la anotación con el día bueno. El documento de prueba
hubo que fabricarlo.

⚠️ **Y una trampa que cuesta cara si no se sabe:** borrar la anotación de
corrección para fabricar el caso **cambia la imagen rasterizada y con ella el
OCR**, así que el tachón deja de detectarse por un motivo ajeno a lo que se prueba.
Lo correcto es vaciar el `/Contents` dejando el `/AP` intacto: la página se dibuja
igual y solo cambia lo que el código lee de la capa de anotaciones.

### ~~Lo que queda abierto de esto~~ → cerrado el mismo día, y el defecto era otro

Se dio por hecho que el fallo estaba en `Personas.cs:150`, que pasa
`hayTachon: false`. **El programador que fue a arreglarlo encontró que estaba un
piso más arriba**, y su versión es mejor: `ProponerCamposDePersonas` recibía las
anotaciones, las validaba con `ArgumentNullException.ThrowIfNull`… y llamaba a
`ExtraerPersonas(lineas)` **sin ellas**. `Personas.Extraer` no tenía forma de saber
que existía un tachón. El puente compartido con los campos del caso ya estaba bien;
lo roto era la entrada.

**El daño, medido en la base por la ventana:** una cédula tachada cuyos dígitos
quedaron intactos daba un `mrn` con la forma correcta `###-####-####` y entraba con
`anulado_por_tachon = 0`, **indistinguible de una lectura limpia**. Ahora entra
con 1. Prueba antes que código, con las dos salidas: contra el código sin arreglar
`Failed: 6, Passed: 5`; después `Failed: 0, Passed: 12`.
`Fichas.Pruebas.Lectura`: 50 → **63**, verificado por el supervisor.

**El nombre** pasa ahora por el mismo camino y recibe el tachón, pero **no**
correcciones: para la cédula hay una regla que decide si una anotación es de verdad
una cédula, y para un nombre no la hay, así que cualquier anotación de la fila lo
sustituiría en silencio.

⚠️ **Dos límites que el propio programador midió y dejó escritos**, y que valen más
que la parte arreglada:
- **El margen es estrecho.** El umbral reusa `FactorDeAltoDeLaBanda` (0,8) en vez
  de inventar uno. Un trazo de 0,0078 sobre una fila de 0,0160 da 0,608 y anula;
  pero **0,0062 sobre una fila de 0,0180 da 0,431 y se escaparía**. Esos números
  salen de trazos medidos en *otras* filas del mismo papel: **no existe un escaneo
  real con la cédula tachada**.
- Un tachón puesto solo sobre la nota «Verified» solapa ~0,02 con la columna de la
  cédula y la marcaría de más. Sin caso real, y el error cae **del lado seguro**:
  va a revisión, no entra callado.

**Y lo que sigue sin poder medirse:** los siete escaneos traen **una persona cada
uno**. La separación por columnas está probada fila a fila, nunca sobre papel real
con dos a seis personas.

---

## 2026-09-07 — Cargar por carpetas no funcionaba, y el comentario decía que sí

Lo encontró de paso el programador del veredicto, importando para medir otra cosa.
Importa porque el dueño pidió expresamente trabajar así (2026-09-05: «permíteme
también en la ventana de revisión organizar todos por carpetas»), y porque el
fallo se lleva la tanda entera sin dejar rastro de qué pasó.

**Eran dos defectos, no uno**, y el segundo explica el síntoma que no cuadraba —un
solo aviso para toda la tanda—:

1. `SearchOption.AllDirectories` **aborta la enumeración entera** en la primera
   carpeta denegada. No se salta esa: se para todo.
2. El `try/catch` que `RutasDePdf.PdfDe` tenía puesto **nunca se disparaba**:
   devolvía un enumerable perezoso desde dentro del `try`, así que la excepción
   saltaba en el `foreach` de `Reunir`, **fuera** del `try`. La traza del paquete
   publicado lo prueba: `FileSystemEnumerator.MoveNext() → RutasDePdf.Reunir`.

⚠️ **Y el comentario del código afirmaba lo contrario de lo que el código hacía:**
«una carpeta que el sistema no deja recorrer se ignora y no detiene nada
(requisito 9)». Era falso desde que se escribió. Un comentario que promete la
conducta contraria es peor que no tener comentario: hace que nadie vaya a mirar.

**No hace falta ningún permiso raro para tropezar con esto.** `C:\Users\josem`
tiene **11 uniones heredadas** —`Application Data`, `My Documents`, `Local
Settings`, `Cookies`, `Recent`, `SendTo`, `Start Menu`, `Templates`, `NetHood`,
`PrintHood`—, medidas con `cmd /c dir /a:l`, y Windows las deniega a propósito.
Cualquiera que elija su carpeta de usuario se lo come.

**Antes y después, medido sobre el paquete publicado:** elegir la carpeta del
perfil daba `System.UnauthorizedAccessException` en inglés y **0 documentos**;
ahora encuentra **395 PDF** y nombra las **11 carpetas una a una con su ruta
entera**. Con una carpeta mixta: antes 0 de 6 legibles; ahora `Importados 6 de 6`
más el aviso que nombra la que no se pudo leer.

**Cuando no se puede leer nada**, la pantalla NO dice que esté vacía —sería
mentira— y lo dice como problema, no como advertencia. Hay una prueba que se pone
roja si alguien mete la palabra «vacía» en esa rama.

**Una decisión del programador que conviene no deshacer:** el recorrido va a mano,
con pila, y lleva memoria de destinos resueltos. Cualquiera crea una unión cíclica
sin ser administrador, y **colgar el programa sería peor que el fallo que esto
arregla**. Hay prueba con unión real y tope de 20 segundos. Esa misma memoria
evita además importar dos veces el mismo PDF por dos caminos.

### Lo que queda abierto de esto

**Enumerar 395 PDF tardó ~57 segundos y la pantalla no dice nada durante la
espera.** Con 3 000 será más. No se encargó y no se tocó.

### Y la prueba de tiempo que se cae sola, otra vez

`ConTresMilDocumentosElTableroDeRevisarCargaEnMenosDeDosDecimas` se midió **roja en
la tanda completa y 733/733 corriendo el proyecto a solas**. Es la tercera vez hoy
que una prueba de tiempo se cae por la máquina y no por el programa.

**Ya existe la forma buena de escribirlas**, y la inventó el pase del veredicto:
afirmar la **proporción** —esto cuesta menos de la mitad que aquello— y dejar el
número absoluto para la medición a solas, diciéndolo por escrito en la propia
prueba. Las de tiempo que quedan en `Fichas.Pruebas.App` y `Fichas.Pruebas.Reportes`
siguen afirmando milisegundos absolutos dentro de la suite. **Nadie las ha
cambiado.**

---

## 2026-09-07 — ⚠️ EL DUEÑO DICTA EL FLUJO ENTERO: el programa se organiza por grupo de fecha

Es el mensaje más denso que ha escrito y **rehace la forma del programa**, no una
pantalla. Va entero y con sus palabras, porque cualquier resumen mío pierde el orden
en que él trabaja. Nada de esto se ha programado todavía.

### 1. Inicio se queda con DOS cosas, y las otras tres se van juntas

> «Listo para asignar debe ser una ventana, no debe estar en el home del sistema.
> Tampoco asignar a los agentes, eso debe estar en otra ventana. Y listo para viajar
> igual: **los tres en una misma pestaña**. Lo único que quiero [en Inicio] es **el
> calendario** y **un cuadro informando cuáles hacen falta por completar**, y
> **cuántos casos tienen los agentes**.»

⚠️ Esto **precisa** lo del 2026-09-05 («lo único que quiero ver en Home es lo que está
listo para asignar y lo que está asignado a los agentes»): entonces esas dos listas se
quedaban en Inicio; ahora se van, y lo que queda es el calendario más un cuadro de
cifras.

### 2. El paquete que vuelve limpia lo que el agente ya completó

> «No quiero que después de subir el paquete de los agentes al sistema y le asigne más
> casos, se queden los casos que él ya ha completado. **Debe quedar limpio.** Cuando él
> sube un paquete que completó, debe quitarle que ese caso está asignado a él, porque
> ya debe pasar a completado en el sistema. Lo único que no se le quitan son los que no
> están completos aún, **pero debe quedar la información que él colocó en el
> paquete**.»

⚠️ Esto **cambia** lo que se decidió hoy mismo, unas horas antes. El programador midió
los seis consumidores de las asignaciones vivas y eligió **filtrar el paquete** en vez
de **cerrar la asignación**, porque cerrarla borraba del informe de cada agente el
trabajo que acababa de hacer. El dueño pide justo lo otro: que se cierre. Manda él.
**Y queda un problema abierto que hay que resolver, no ignorar:** cómo sigue
apareciendo en el informe del agente lo que hizo, si su asignación ya no está viva.

### 3. El lector deja vacío y manda a Corrección

> «Debes afinar el escáner del PDF, porque está tomando información de otro cuadro. **Si
> no reconoce la información, mejor dejarlo vacío y enviarlo a la ventana de
> Corrección.**»

Es la misma regla que se cerró hoy para la cédula y la unidad, dicha para todos los
campos. Lo que añade es la segunda mitad: **lo que queda vacío tiene que ir a Corrección
solo**, no quedarse esperando a que alguien lo busque.

### 4. El flujo, con su ejemplo entero

> «Debe haber un flujo de trabajo. **Lo que está listo para asignar no puede mezclarse
> con lo que se debe organizar.** Por fecha, así trabajamos los grupos a tiempo.
>
> Ejemplo: 17 personas tienen fecha de viaje 12 de septiembre, y el lector de PDF leyó
> todos, le colocó el nombre de la unidad y número de la unidad, nombre, cédula de
> miembro, **pero no leyó la fecha de 2 archivos que tenían 4 personas**. Debe ponérmelo
> en la ventana de Corrección. Y los otros, que sí están correctos, debe ponerle **grupo
> viaja el 12 de septiembre**.
>
> **Cuando yo lo corrija y le ponga la información, debe salir de Corrección y pasar al
> grupo de su fecha.** Y si voy a Corrección no debe estar ahí, porque ya está todo
> listo, toda la información está correcta.»

**El grupo de la fecha va dividido por unidad**, con sus palabras:

> «Rama San Juan No 325535, 10 personas viajarán el 12 de septiembre. Barrio Marito
> 656351, 5 personas viajarán el 12 de septiembre, falta verificar recomendaciones. Y
> ya.»

O sea: **Corrección es un sitio de paso, no un almacén.** Un documento entra cuando le
falta algo y **sale solo** cuando deja de faltarle.

### 5. Lo completo sale de todas partes MENOS del calendario, y ahí va en verde

> «Cuando un paquete entra y marca todo completo, **debe salir de todos lados excepto
> del calendario**. Aunque se archive, debe quedarse en el calendario **marcado en
> verde**, porque están completos, pero se mueve para abrir espacio a otros PDF que
> necesitan ser procesados en el flujo de trabajo.»

⚠️⚠️ **Esto deshace en parte lo que se entregó hoy.** El 2026-09-06 él pidió «archivado
no aparece más en ningún lado», y con ese motivo —«si se queda en el tablero y dice
archivado, lo que hace es que me confunda»— se quitó también del calendario, y el
supervisor quitó además la palabra de los tres contadores.

**Lo que ha cambiado es el motivo, y por eso no es una contradicción:** lo que le
confundía era ver «ARCHIVADO» como una etiqueta en medio del trabajo. Lo que pide ahora
es **verlo en verde en el calendario**, que no es una etiqueta que estorba sino la señal
de que ese día ya está resuelto. **Antes de programarlo hay que preguntárselo con las
dos frases delante**, porque una de las dos se va a quedar sin cumplir y él tiene que
elegir cuál.

### 6. Poder editar las carpetas al importar

> «En la ventana de importar documentos, cuando carga los PDF, debe permitir **editar
> las carpetas que se mostrarán en Revisar**, porque a veces el sistema no pone los
> nombres de manera correcta.»

### 7. Revisar: marcar sin perder el sitio, y «ver todos» solo si él lo pide

> «Cuando estoy trabajando en Revisar y le das clic a un paquete, te envía al inicio
> otra vez de Revisar y te coloca todos juntos. **Debe permitirme marcar un caso sin
> moverse**, para seguir trabajando con los otros sin revisar. **Ver todos debe ser una
> opción mía, no ponerlo como predeterminado.** Ahí es que debe ponerse en su categoría
> y organizado por sus carpetas y fechas, con su nombre de unidad y fecha de viaje de
> cada unidad.»

⚠️ Ojo: el 2026-09-06 se cambió el tablero de arranque de «Todo» a «Sin revisar». Lo que
él describe —«te envía al inicio otra vez y te coloca todos juntos»— es que **la pantalla
se repinta entera y pierde el sitio** al marcar algo. Es otra cosa distinta del tablero
de arranque, y hay que medirla antes de tocar nada.

### 8. Un botón para contestar las seis de un tirón

> «En el botón de las seis preguntas debe haber un botón que me permita **marcar todos
> los pasos en un solo clic**, en el caso de que no quiera crear un paquete para mí, sino
> que si una persona puede trabajar sola con el sistema, que se pueda hacer sin
> problema.»

### 9. Asignar por grupo

> «En asignar debe poder asignarlo por grupo. **Cargar todos los documentos en un solo
> lugar no me conviene para nada.**»

### Y lo que repitió dos veces el mismo día, señal de que estorba de verdad

> «Este documento no tiene información de ninguna persona, debería permitirme
> eliminarlo.»

Medido por el supervisor: el botón **existe** —«Borrar los marcados», en Revisar— y ese
documento **sí** es un caso, así que lo alcanza. Lo que no existe es borrarlo **desde
donde él lo está mirando**, que es Corrección. Y el documento de su captura estaba
marcado «completa», así que en Revisar no sale en el tablero de arranque: hay que ir a
buscarlo a otro tablero. **La falta no es el botón: es el camino.**

### Lo que esto significa, dicho de una vez

El programa está organizado por **pantallas** —importar, corregir, revisar, asignar— y él
trabaja por **grupos que viajan una fecha**. Cada una de las nueve cosas de arriba empuja
en la misma dirección: que un documento **viaje solo** de un sitio al siguiente según lo
que le falte, y que las pantallas sean etapas de ese viaje y no almacenes donde se
acumula todo.

---

## 2026-09-07 — Su palabra gana a la del programa: marcar completo y borrar un nombre

Dos frases suyas, seguidas, y las dos dicen lo mismo por dentro: **el programa
propone, y cuando él decide otra cosa, la suya vale.**

> «Si yo marco algo completo, **debe cambiar a completo, no importa si el sistema
> diga que está mal**.»
>
> «Si quiero eliminar un nombre, puedo hacerlo.»

Es la misma línea que el requisito 9 del proyecto —**avisar, nunca impedir**— y la
misma que él ya había dicho el 2026-09-04 con otras palabras: «no me gusta que me
ponga tantas restricciones», «el programa debe ser libre».

### Lo medido antes de anotarlo

**Marcar completo NO está bloqueado por los campos que falten.** Buscado en
`Fichas.App/Correccion/ModeloDeCorreccion.Administrador.cs` y en
`Fichas.App/Revisar/AccionesDeRevisar.cs`: no hay ninguna condición sobre lo que le
falte al documento.

Lo que sí lo esconde es otra cosa: `ModeloDeCorreccion.Administrador.cs:15` —
`HayAtajoDeAdministrador` exige **un caso abierto y exactamente UN administrador
activo**, y `PaginaDeCorreccion.xaml.cs:427` esconde el botón cuando no lo hay.
Viene del ADR-0005 §6.3: «si no hay exactamente uno, el botón no está y se dice por
qué».

⚠️ **Así que hay dos lecturas de su frase y no se pueden confundir:**
1. Que el botón **no aparezca** porque en su base no hay un administrador dado de
   alta. Entonces lo que falta es el alta, no el permiso.
2. Que marque y **algo se lo deshaga** después. Eso sería un defecto y hay que
   medirlo con la ventana abierta.

**Hay que preguntárselo antes de programar nada**, porque el arreglo de una no es el
de la otra.

**Borrar una persona: hoy es imposible.** `Fichas.Contratos/Puertos/IPersonas.cs`
tiene `Listar`, `Contar`, `Obtener`, `DeCaso` y `Guardar`. **No hay `Borrar`.** Lo
midió el planificador y lo comprobó el supervisor. Consecuencia: una persona
añadida a mano por error —y desde hoy se pueden añadir a mano— se queda para
siempre y cuenta como un ticket abierto que nunca se cierra.

Abrirlo pide tocar `Fichas.Contratos`, que está congelado, y lo autoriza el
supervisor cuando salga el pase.

### Lo que NO cambia, y conviene decirlo aquí

Que él pueda marcar completo por encima del programa **no toca la regla permanente
5**: lo que él firma queda firmado con su nombre, y lo que el compañero dijo sigue
siendo del compañero. Lo que se amplía es su libertad para decidir; lo que no se
toca es de quién es cada firma. Y borrar una persona **no puede borrar en silencio**
lo que esa persona tuviera contestado: eso hay que decirlo antes, como ya hacen los
otros borrados.

---

## 2026-09-07 — ⚠️ DOS ESTADOS Y NO CUATRO: «resuelto» y «me falta»

Es la decisión más simplificadora que ha tomado el dueño, y llega después de que el
supervisor le enseñara las cuatro palabras que el programa le estaba poniendo
delante. Su respuesta, entera:

> «Dos estados nada más: **resuelto** y **me falta**.»

### Lo que había, medido, y por qué le confundía

Cuatro estados a la vista, y **tres los escribe alguien distinto**:

| Palabra | Qué significa | Quién la escribe | Dónde |
|---|---|---|---|
| «listo para asignar» | no falta ningún campo del papel | el programa | `ModeloDeCorreccion.Guardado.cs:390` |
| «completa» / «no completa» | el compañero fue al sistema del obispo | **el Excel del compañero, con su nombre** | `casos.estado_recomendacion` |
| «confirmada» | las **seis preguntas** de esa persona dicen sí | quien las conteste | `Fichas.Reportes/Reglas/Pasos.cs:42` |
| «listo para viajar» | esa persona puede viajar | consecuencia de las seis | `LasDosPreguntas.Decir` |

Por eso un renglón podía decir **«listo para asignar · no completada»** a la vez sin
estar roto, y por eso él marcaba «completa» y el calendario seguía en rojo: el
calendario cuenta **confirmadas**, que es otra columna.

### La decisión, y el límite que NO se cruza

**A la vista, dos: «resuelto» y «me falta».** Resuelto es que no queda nada que él
tenga que hacer con ese documento o esa persona. Me falta es que sí.

⚠️ **En la base siguen siendo cuatro, y eso no es negociable.** Lo que se colapsa es
lo que se le enseña, no lo que se guarda. El motivo no es purismo: las tres
columnas dicen **quién dijo cada cosa**, y esa es la única defensa del proyecto el
día que alguien llegue al templo y no pueda entrar. Si se funden en una, deja de
poder saberse si aquello lo dio por bueno Sandy, lo dio por bueno Miguel, o lo dedujo
el programa. La regla permanente 5 vive precisamente ahí.

**Cómo se cumplen las dos cosas a la vez:** «me falta» **siempre puede decir qué
falta y a quién le toca**, y lo dice cuando él lo pide, no de entrada. La cifra que
ve es una; el detalle está a un clic.

### Lo que esto arrastra, y hay que medirlo antes de tocar

- Los rótulos de `RenglonParaAsignar.PalabraDe`, `TableroDeRevisar` (seis tableros),
  `LasDosPreguntas.Decir`, `PalabrasDelEstado` y la etiqueta del calendario
  (`ModelosDeInicio.cs:176`, «N de N confirmadas») dejan de ser el idioma de la
  pantalla y pasan a ser detalle.
- **Los seis tableros de Revisar** están hechos sobre las cuatro palabras. Con dos
  estados, hay que ver cuántos quedan.
- Los **reportes de los jefes** y el histórico siguen con su vocabulario: no los ve
  él, los ven los jefes, y ahí la distinción sí importa.

### Lo que esta decisión NO contesta, y sigue abierto

1. Si al marcar algo resuelto quiere decir «verifiqué las seis» o «lo doy por bueno»
   — con dos estados pesa menos, pero el calendario tiene que saber cuál de las dos
   pinta.
2. El archivado en el calendario: tres posturas suyas en cinco días.
3. Cerrar la asignación al devolver, y de dónde sale entonces el informe del agente.
4. Si el botón de dar por completo **no aparece** o si algo **lo deshace**.

---

## 2026-09-10 — Lo que el dueño vio probando el v9, seis cosas

Sus palabras, enteras. Dos son defectos de lo que se acaba de entregar; cuatro son
trabajo nuevo. Nada de esto está programado.

### 1. Desde Revisar, entrar al PDF y añadir o quitar personas

> «En la parte de Revisión quiero que me permita **entrar al PDF como si fuera la
> parte de Corrección**, y dentro quiero que me permita **eliminar personas o
> agregar personas** que quizás el escáner no contempló.»

Lo de añadir a mano ya existe en Corrección desde el 09, pero solo cuando el
documento no tiene **ninguna** persona. Lo que pide es más: añadir una más a un
documento que ya tiene, y **quitar** una. Quitar una persona sigue sin existir:
`IPersonas` no tiene `Borrar` (medido el 07, sigue igual). Y desde Revisar no se
llega al PDF: el planificador lo midió el 07 — «Revisar no navega a ninguna parte».

### 2. ⚠️ DEFECTO: en Corrección el grupo de fecha aparece vacío

> «En la parte Corrección debe aparecer el grupo de fecha, pero **tengo un grupo de
> fecha y no me aparece nada, está vacío**. Debes verificar esta parte.»

Corrección pasó ayer a ser sitio de paso: **solo enseña lo que le falta algo**. Si
todos los documentos de ese grupo están resueltos, el grupo sale vacío — y eso es
lo que él pidió el 07 («si voy a Corrección no debe estar ahí, porque ya está todo
listo»). **Pero puede ser otra cosa**, y no se puede decidir sin verlo: hay que
medirlo con su captura o con un caso igual.

### 3. El reporte a los gerentes: nombres, quién lo resolvió y qué se hizo

> «En los reportes a los gerentes **solo aparece el número de la unidad, pero no los
> nombres** y **quién lo resolvió**. Debe dar un **reporte detallado de lo que se
> hizo con el líder**. Es importante hacerlo así.»

### 4. Un documento de varias hojas: cada hoja es su información

> «Si un documento tiene más hojas —ejemplo hoja 1 y hoja 2— **los cambios deben
> cambiar, porque no es la misma información la de la hoja 1 y la hoja 2**. Algo más
> importante: **debe revisarse por grupo y por persona de manera individual**, y
> también tener la opción de agruparlos.»

Esto toca lo del 08: cuando varias hojas se juntan en un caso, los campos salen de
la hoja que lo abrió. Él dice que las hojas **no son la misma información** y que
los cambios deben ser por hoja. Es la decisión que el programador de las hojas
devolvió el 08 y quedó abierta.

### 5. Una sección en Revisar con todos de forma individual

> «Puede abrir una nueva sección en Revisar que estén **todos de manera
> individual**. El número de caso es para saber en qué grupo viajan. En Revisión me
> gustaría más que aparecieran **los nombres, la fecha en que viajará, la cédula de
> miembro y su unidad**.»

Es coherente con «el ticket es por persona» del 05: una vista por **persona**, no
por documento, con esas cuatro cosas.

### 6. ⚠️ DEFECTO: un PDF suyo que el motor no lee

> «El PDF que te envié, ese PDF **no lo lee el motor de lectura, y crea varios que
> nada que ver**. Mira la diferencia.»

**El PDF no llegó**: lo mandó por una sesión remota que estaba fuera de línea. Sin
el archivo no se puede medir nada. Pedido.

### Lo que hay que preguntar antes de programar

- Del 2: **una captura** del grupo vacío, para saber si es lo pedido o un defecto.
- Del 6: **el PDF**.
- Del 4: ¿«los cambios deben cambiar» quiere decir que al corregir la hoja 2 no
  se toque la hoja 1? ¿O que cada hoja sea un caso aparte?

  **Contestado el mismo día:** «**Hay veces que serán diferentes los
  documentos.**» O sea: un PDF de varias hojas **puede** traer documentos
  distintos —personas y unidades distintas— bajo el mismo archivo. El programa
  hoy los junta en un caso por número de caso (`GuardadoDeHojas.cs`, medido el
  08). **Esa suposición se cae.** Cada hoja se lee como lo que es, y agruparlas
  es una opción suya —sus palabras del 10: «también tener la opción de
  agruparlos»—, no un automatismo.

---

### Lo que enseña la captura del PDF, y cambia el diagnóstico del 07 y del 08

Mandó una captura de una hoja de **ELTC2609**. Leída por el supervisor, no por el
programa: es un formulario **rellenado a máquina**, no a mano —el nombre y la
cédula van tecleados dentro de la fila—, la unidad dice **«Barrio Cantaura
(339482)»** con el número entre paréntesis dentro del mismo texto, y la estaca
«Estaca El Tigre Venezuela (429589)». Viaja el 17/09/2026 al templo de Caracas.

**Y el 07 él enseñó OTRA hoja del mismo ELTC2609** que decía «Wanica Branch», con
estaca 453633 y fecha de mayo. **Mismo número de caso, dos hojas, dos unidades
distintas, dos personas distintas.** El visor decía «5 / 6»: son seis hojas.

Eso deshace dos conclusiones anteriores:

- **Del 07:** se concluyó que «Barrio Cantaura ()» venía de una línea de otra fila
  tomada como la banda de la unidad. No: **es la unidad real de una de las hojas**,
  con el paréntesis vacío como huella de quitarle el número. Lo que él veía «mal» era
  que el programa le enseñaba la hoja de Wanica con la unidad de Cantaura — **la
  unidad de otra hoja del mismo caso**, no de otra fila.
- **Del 08:** el programador de las hojas midió sobre `SURB2609` que las seis hojas
  decían lo mismo y concluyó que el mecanismo «vale 0 veces en las 20 hojas reales».
  Era verdad para SURB2609. **ELTC2609 no estaba en la máquina**, y en ELTC2609 las
  hojas NO dicen lo mismo. El agujero que se dejó abierto ese día —«el valor de la
  otra hoja no se mete solo en el caso»— es exactamente lo que él está viendo.

### ⚠️ Y el archivo, medido: NO es lo que parecía. Es un formulario rellenado a máquina

Llegó el PDF. Medido por el supervisor con `pypdf`:

```
hojas: 1
caracteres en la capa de texto: 2 723
anotaciones: 105 → /Widget 93 · /FreeText 7 · /Ink 3 · /StrikeOut 1 · /Popup 1
```

**Una sola hoja, y 93 campos de formulario (`/Widget`).** El nombre, la cédula, las
fechas y el templo **no están en la capa de texto ni en un `/FreeText`: están dentro
de los campos del formulario**, tecleados. La única línea de datos que sí está en
la capa de texto es «Barrio Cantaura (339482) Estaca El Tigre Venezuela (429589)».

Y el lector, medido en `Fichas.Lectura/LecturaDePdf.cs:185`: **«Solo salen las
`/FreeText` y las `/Ink`. Los demás subtipos se ignoran».** `grep` de `Widget`,
`AcroForm` o `FormField` sobre `Fichas.Lectura/`: **cero**.

**Eso lo explica todo, y mejor que lo de arriba:**

- «Sin ninguna persona leída» del 07: el programa **no lee campos de formulario**,
  y al rasterizar sin ellos la tabla sale **en blanco** — que es exactamente lo que
  enseñaba la captura de ese día. La persona está ahí; el programa no la ve.
- «Barrio Cantaura ()»: es la única línea de datos que sí está en la capa de texto,
  partida en número y nombre, con el paréntesis vacío como huella.
- «Crea varios que nada que ver»: sin las personas y con el caso leído del texto,
  lo que entra es un cascarón.

**Corrección a lo escrito más arriba:** este archivo tiene **una** hoja, no seis.
La captura del 07 con «Wanica Branch» y «5 / 6» era de **otro PDF** que el visor
tenía abierto mientras el panel decía ELTC2609, o de un ELTC2609 distinto que no
ha llegado. No se puede afirmar cuál; queda dicho.

**Lo que esto significa para el proyecto:** hay **dos clases de documento** y el
lector solo conoce una. Los siete `CASP2609` son escaneos con correcciones a mano
—`/FreeText` e `/Ink`—; este es un **formulario rellenable rellenado en el
ordenador**, y su información vive en `/Widget`. El dueño recibe de los dos.

**Lo que esto pide, y coincide con su punto 4:** un PDF cuyas hojas llevan personas
y unidades distintas **no es un caso**: son varios. Juntar por número de caso es
la suposición que falla con sus documentos reales. Hace falta el archivo para
medir cuántas hojas, cuántas unidades y cuántas personas trae, y qué hizo el
programa con él.

## 2026-09-11 — EL DUEÑO PIDE INSTALADORES, y la regla 2 se precisa

Sus palabras: *«Haz un instalador para actualizar. Ahora vamos a actualizar el
programa: haremos instaladores.»* Y antes, el 10: *«la versión que subiste es del
código, no del programa»* — el programa tiene que estar en GitHub, no solo el código.

**Lo que contradice.** La regla permanente 2 de CLAUDE.md dice «sin instalación», y
`PENDIENTES.md:1203` lo repite. Lo señaló el programador del instalador, que hizo bien:
lo que la regla protegía —que el usuario no tenga que instalar Python, ni un servidor,
ni un runtime, ni abrir puertos— sigue intacto. Lo que cambia es la forma de llegar
el programa a la máquina: en vez de descomprimir un zip a mano, un instalador. Se
precisa la regla 2 en CLAUDE.md con esta fecha; no se borra.

**Lo decidido, y medido por el programador (yo leí el guion entero y el diff; NO repetí
la instalación de ensayo):**

- `csharp/Fichas/instalador/Fichas.iss`, Inno Setup 6. `AppId` **fijo para siempre**
  (`{943F88A5-F4D4-43CF-ACEC-97D50EE6D45B}`): es lo que le dice a Windows que la v11
  es el mismo programa que la v10 y debe sustituirla. Si se cambia, se instalan dos.
- **Por usuario y sin administrador** (`PrivilegesRequired=lowest`,
  `%LOCALAPPDATA%\Programs\Fichas`). Motivo: en el trabajo del dueño puede no haber
  permisos de administrador, y una instalación en Archivos de programa los exigiría
  en CADA actualización. No se ofrece «para todos los usuarios».
- **Actualizar = ejecutar el instalador nuevo encima.** Cierra Fichas si está abierto
  (Administrador de reinicios de Windows) y **vacía la carpeta del programa** antes de
  copiar —la lista de DLL cambia entre versiones y una vieja que sobre puede romper el
  arranque—, solo si la carpeta ya era una instalación de Fichas. Medido con un
  señuelo `viejo.dll`: desaparece; 0 archivos sobran, 0 faltan.
- **`Documentos\Fichas` no se nombra en ninguna sección del guion.** Ni instalar, ni
  actualizar, ni desinstalar lo tocan. Medido: 1 archivo antes, 1 después de desinstalar.
- `publish.ps1` construye, después de la carpeta, el zip (`Fichas-vN.zip`, antes se
  hacía a mano) y el instalador (`Instalar-Fichas-vN.exe`), **al lado** de la carpeta.
  Con `-Ensayo`, los dos llevan `-ENSAYO`. Si falta Inno Setup, lo dice y el zip sale
  igual. Ambos llevan la misma guarda de versión que la carpeta.
- Nada que instalar aparte: `SelfContained` y `WindowsAppSDKSelfContained` son `true`
  en el csproj (medido por el programador); exige Windows 10 1809 x64.
- Inno Setup se instaló en esta máquina con winget (6.7.3) con permiso del dueño el
  2026-09-11.

**Lo que NO cubre:** actualizar una copia que se descomprimió del zip a mano en otra
carpeta —el instalador no la conoce, instala en la suya y la suelta se queda—. El
dueño tiene que instalar una vez con el instalador y, de ahí en adelante, actualizar
con el siguiente. Tampoco se comprueba solo si hay versión nueva: el repositorio es
privado y haría falta una clave en cada máquina; queda fuera salvo que él lo pida.

**Lo que se sube a cada Release de GitHub a partir de la v11:** el instalador y el zip.

## 2026-09-11 — ARCHIVAR LE QUITA EL DOCUMENTO AL AGENTE

Sus palabras: *«Quiero que cuando los documentos se archiven ya no aparezcan
asignados al agente, porque llegará un punto en que, si no se hace así, un agente
puede tener 1 000 casos pero en la realidad solo tiene 10.»*

**Lo que había, medido por el programador en el código y repetido por el supervisor
en `AccionesDeRevisar.cs:46-47` y `CargaDeUnCompanero.cs:173-181`:** archivar solo
escribía `archivado = 1`; la asignación seguía viva. Un archivado con asignación
entraba en el renglón por agente de Inicio y Flujo, en «sin devolver», en el
desplegable de Paquetes y en el paquete siguiente; y **no** entraba en la cifra grande
de «casos de los agentes». Inicio decía dos números distintos de lo mismo.

**Lo decidido:**

- Archivar retira **todas** las asignaciones vivas del documento: se desactivan con la
  fecha de hoy y **nunca se borran**, el mismo gesto que el paquete que vuelve hace con
  lo completado desde el 2026-09-08. Va por `OperacionDeAsignar.RetirarDelCaso`, la
  misma puerta que el botón de Asignar, para que los dos caminos dejen la base igual.
- El acuse lo dice: «2 documentos archivados; 2 dejan de estar asignados».
- **Desarchivar no devuelve la asignación**: el dueño decide a quién va.
- **El informe del agente sigue trayendo lo que hizo**, porque lee vivas y retiradas.
  Medido por el programador sobre el PDF generado: «Documentos que se le asignaron 8 ·
  Lleva 4 ahora mismo», con los dos archivados en la lista.
- **El caso de los 1 000, los archivados de antes de hoy con asignación viva:** al
  llegar a Inicio, el programa los retira solo y lo dice en una línea («2 documentos
  archivados de antes seguían asignados; ya no. Lo que hizo cada agente con ellos se
  conserva en su informe»). Es idempotente, no marca ni firma nada (regla 5 intacta),
  y con 3 000 casos cuesta 54 ms con trabajo y 18 ms sin él. **Lo eligió el supervisor
  y no el dueño**: la alternativa era un botón. Si él prefiere el botón, se cambia.
- Nada de esto toca la fecha de archivado.

**Medido por el supervisor tras fusionar:** App 991 pruebas (980 + 11 nuevas); una,
`ConTresMilDocumentosLeerLaProcedencia…`, roja con la máquina al 62 % (OneDrive y
otros procesos: 704–786 ms sobre un tope de 700) y verde a solas; `LectorDelInicio.cs`
no cambió en esta fusión (`git diff --stat` vacío). Los 8 archivos de la App que
cambiaron: `Revisar/RetiradaAlArchivar.cs` (nuevo), `AccionesDeRevisar.cs`,
`PaginaDeRevisar.xaml.cs`, `Inicio/PaginaDeInicio.xaml.cs`, `CargaDeUnCompanero.cs`
(solo el comentario que hacía la pregunta).

**Lo que NO cubre:** `PanelDelEquipo` («lleva N cosas a su nombre», para borrar un
compañero) sigue contando vivas y retiradas: es otra pregunta. El coste de archivar
en lote sobre SQLite no se midió (en memoria, 300 documentos: 4 → 45 ms).

## 2026-09-11 — EL DUEÑO PIDE QUE EL PROGRAMA SE ACTUALICE SOLO

Sus palabras: *«Haz un control de versiones que cuando llegue una nueva versión se
actualice todo.»* Y eligió, entre dos opciones que se le pusieron delante, la **A**:
el repositorio sigue privado y él crea en GitHub una clave de solo lectura que pega
una vez por máquina; la B era hacer público el repositorio.

**Lo decidido:**

- Al arrancar, y desde un botón «Buscar actualización» en la cabecera, el programa
  pregunta a GitHub por el último Release del repositorio
  (`GET https://api.github.com/repos/Josemgu/Claude_for_coding_surgery/releases/latest`)
  y compara la etiqueta `vN` con su `<Version>`. Es **una llamada saliente por HTTPS**
  y nada más: no escucha, no abre puertos, no manda datos del dueño (regla 2 y §3 de
  CLAUDE.md, precisados hoy). Sin red o sin clave, calla: una línea en el registro
  de arranque y ninguna ventana.
- Si hay versión nueva, lo dice en una franja con un botón. **No se instala sin que
  él lo pida**: un clic. Con el clic, baja `Instalar-Fichas-vN.exe` del Release a la
  carpeta temporal, comprueba tamaño y huella SHA-256 (el `digest` que GitHub da por
  activo) y, solo si cuadran, lanza el instalador en silencio y cierra el programa.
  El instalador (decisión de hoy, «EL DUEÑO PIDE INSTALADORES») hace el resto y
  vuelve a abrir Fichas.
- **La clave:** la crea el dueño en GitHub (fine-grained, solo «Contents: read»
  sobre ese repositorio) y la pega **él** en `Documentos\Fichas\clave-de-actualizacion.txt`.
  El programa la lee de ahí y la manda como `Authorization: Bearer`. Nadie del
  proyecto la ve ni la escribe: el supervisor y los programadores no manejan claves
  del dueño (regla de trabajo desde el 09-10). Si el archivo no está, el programa
  prueba sin clave (vale si el repositorio se hace público algún día) y, si GitHub
  contesta 404, dice en la franja que falta la clave y dónde va.
- La clave **nunca** va a un registro, a un acuse ni a un informe.
- El código va en `Fichas.App/Actualizacion/`, con la red detrás de una interfaz
  para que las pruebas no salgan a internet (regla: sin red en pruebas).

**Lo que NO cubre:** actualizar una copia descomprimida del zip a mano (el
instalador no la conoce); comprobar si hay versión nueva más de una vez por
arranque sin pulsar el botón.

**Lo medido al fusionar (2026-09-12, supervisor):** App 1 067 pruebas (990 + 77 de
`Actualizacion/`), 0 rojas; toda la solución recompilada con documentación, 0 avisos.
El programador midió con la ventana abierta la franja «pega la clave en …» sin clave y
«La clave de actualización no vale» con una inventada, y que el registro no contiene la
clave. Dos cosas que cambió respecto al diseño de arriba, con motivo: 401/403 **sin**
clave dice «no se pudo (GitHub contestó 403)» y no «la clave no vale», porque no hay
clave que cambiar; y el instalador lanzado desde el programa recibe la carpeta de datos
(`/carpetadedatos=…`) y reabre Fichas con el mismo `--carpeta-de-datos`, porque si no
un programa arrancado sobre otra carpeta volvería sobre la de por defecto. **No
verificado por nadie:** el camino completo con clave válida y un Release más nuevo
(hace falta la clave del dueño y una v13); se verá la primera vez que salga la v13.

## 2026-09-14 — CUATRO PETICIONES DEL DUEÑO, HECHAS EL MISMO DÍA

Cada una con sus palabras, lo que se decidió y lo medido. Todas fusionadas en master;
el supervisor repitió la suite tras cada fusión (App 1 089 → 1 108 → 1 118 → 1 126;
Datos 189 → 205; Contratos 32) y no repitió las mediciones con la ventana abierta, que
son de cada programador.

### 1. El panel de carpetas de Revisar se ajusta con el ratón

*«Que pueda ser ajustado con el mouse, agrandar o reducir, en la ventana de Revisar.»*

Un tirador entre el panel y las tarjetas, hecho a mano con eventos de puntero (**sin
paquete nuevo**: regla 3). Ancho de siempre 290, mínimo 200, techo = marco − 16 − una
tarjeta entera (330); doble clic vuelve a 290; el ancho se guarda en el mismo
`preferencias.txt` del tema (`ancho_del_panel_de_carpetas=`). Medido con la ventana
abierta: 290 → 440 → 290 al arrastrar; reabrir con 490 guardado da 490; a 1100×700 con
700 guardado se recorta a 453 y el archivo sigue diciendo 700. **Solo con ratón:** el
mockup v2 no trae gesto de teclado y no se inventó. Antes de tocar nada se midió que
los nombres largos de carpeta ya se cortaban a 290.

### 2. «Eliminar» en Corrección

*«Agrega un botón en la Corrección de eliminar información también. No lo tengo y es
importante tenerlo.»* Y del 07 y del 10: eliminar un nombre; eliminar o agregar personas.

Un botón «Eliminar» con una opción por persona y «Eliminar el documento entero», cada
una con su pregunta. Nuevo `IPersonas.Borrar(personaId)` (Datos y Falso): copia de la
base antes, transacción procedencia → persona; sin copia no borra. La última persona
pregunta si borrar el documento; el documento entero va por el mismo camino que
Revisar (plan, pregunta, copia). Y **añadir una persona a mano se ofrece siempre**,
aunque el documento ya tenga otras (hasta hoy solo si no tenía ninguna). Medido sobre
SQLite propia: 3 → 2 personas con 0 huérfanos en procedencia; documento entero fuera
con sus personas; «No» deja las cuentas iguales. Decisión del programador, devuelta:
la copia al borrar una persona se hace **después** del «sí» (el puerto no tiene fase
de plan); la pregunta lo avisa.

### 3. Dentro de la fecha, verde lo resuelto y rojo lo que falta

*«Cuando des clic y entres a la fecha, lo que esté completo se marque en verde y lo
que no en rojo, como se muestra en el calendario afuera.»*

Cada renglón del grupo lleva fondo y franja con el mismo par exacto del calendario
(`PinturaDeInicio.VerdeMarca/RojoMarca` y sus fondos), con el mismo veredicto de las
dos palabras del 07; la cabecera de cada unidad y del día llevan su franja. Medido por
píxel en claro y oscuro: 9 rojas + 1 verde en un día con «me falta 9 de 10» afuera.
No se añadió frase nueva: la cabecera ya dice «me falta N de M».

⚠️ **Decisión que queda del dueño (preguntada el 14):** un archivado cuenta en el
calendario (07: «se queda en verde») pero no entra al grupo de la fecha (06: «sale de
todos lados»). Medido: un día decía afuera «me falta 4 de 5» y dentro «4 de 4»; un día
resuelto solo por archivados se abre vacío. Opciones: (A) el archivado entra al grupo
en verde; (B) el calendario deja de contarlo.

### 4. Duplicado en rojo entero y «Unificar con el original»

*«Cuando un caso esté duplicado, ponlo en rojo completo, que diga duplicado, y agrega
la función de unificar el caso duplicado con el caso original, para que se elimine el
duplicado.»*

La tarjeta del duplicado va entera en rojo con la palabra «DUPLICADO» y de quién lo es,
y un botón «Unificar con el original» que pregunta antes. Nuevo
`IMantenimiento.PlanearUnificacion` + `Unificar` (solo añadido), en **una transacción**:

- Pasan al original **solo si él los tiene vacíos**: número de caso, unidad (número y
  nombre), fecha de viaje, templo, y con ellos su procedencia. Nada del original se pisa.
- **No pasan** estado, motivo ni firmas (son la firma de un compañero sobre ese papel:
  regla 5), ni ruta/hoja del PDF, ni archivado, ni captura manual.
- Personas: misma cédula = misma persona (no pasa); sin cédula, nombre exacto; con
  cédula que el original no tiene, pasa aunque haya un nombre igual sin cédula (ante la
  duda se duplica, no se pierde).
- Asignaciones: pasan al original y las vivas se retiran con fecha (como al archivar);
  el original no hereda agente. Los ilegibles del duplicado caen con él.
- El duplicado se borra por el camino de siempre, con copia previa de la base.
- Si el original ya no está, en vez de unificar se ofrece «Quitar la marca de
  duplicado» (`duplicado_de` a NULL).

Medido sobre SQLite propia: original con 2 personas sin fecha + duplicado con 3 (2
iguales por cédula + 1 nueva) con fecha → original con 3, fecha puesta, procedencia de
la nueva apuntando a la hoja del duplicado; `casos` 4 → 3, `personas` 7 → 5; la
asignación viva del duplicado pasa retirada con fecha; «No» deja todo igual.

**Lo que NO queda en la base y se devuelve al dueño:** el nombre del archivo del
duplicado cuando era otro PDF (`casos` no tiene sitio para una segunda ruta; ponerlo
es una migración). Queda en el acuse, en `fichas.log` y en la copia previa.
Recomendación del programador: dejarlo así hasta que se eche en falta. La tarjeta de
`Grupo/` **no** se puso en rojo (otro terreno).

## 2026-09-14 — LAS SEIS EN «SÍ» MARCAN COMPLETADO, y la regla 5 se precisa

Sus palabras: *«Cuando se marcan las 6 preguntas que sí, de manera automática debe
marcarse como completado.»* Deshace los dos clics a propósito del 07 («marcar y firmar»).

**La firma la decidió el supervisor, no el dueño** (contestó «Dale» sin elegir): el
administrador, por la misma regla que `ElAdministrador` usa en Corrección —uno activo
firma; con ninguno o dos no se marca solo y el acuse dice por qué—, y nunca «el primero
activo», que podía firmar como Sandy. Queda escrito para que él lo cambie.

**Lo decidido:** al quedar las seis de una persona en «sí» (una a una o de un tirón),
si **todas** las personas del documento tienen las seis en «sí» y `LoQueLeFalta` no
devuelve nada, el documento pasa a `completa` en el mismo gesto, firmado por el
administrador, con origen «las seis en «sí» desde la pantalla», y el motivo de «no
completa» se vacía. Si no puede, el acuse dice exactamente qué falta («sin las seis en
«sí»: Persona 2», «le falta: Fecha de viaje», «dese de alta como administrador») y los
«sí» se guardan igual. Un «sí» que vuelve a «no» **no** desmarca; una marca de un
compañero no se pisa.

**Medido por el programador con la ventana abierta y base propia:** 1 persona con todo →
`completa`, firma Miguel, origen nuevo; 2 personas, las seis de una sola → no, acuse
nombra a la otra; las de la otra → completa; sin fecha de viaje → no, acuse «le falta:
Fecha de viaje»; sin administrador activo → el programa ya se niega a guardar las seis
(criterio C19-10), así que ahí no hay nada que derivar; con un MRN mal formado el acuse
dice «le falta: Cédula» (el suelo de la regla 5 actúa). Suite App 1 126 → 1 137, medida
por el supervisor tras fusionar.

**Lo que NO cubre, devuelto al dueño:** las seis contestadas por el Excel de un compañero
(Paquetes) no disparan esta derivación, solo la ventana de Revisar; Corrección lee el
origen nuevo como «completa a mano por Miguel» (una línea en `TextoDeLaMarcaDelEstado`,
otro terreno); un «no» posterior no avisa de que el documento sigue completado.

## 2026-09-14 — EL ARCHIVADO ENTRA AL GRUPO DE SU FECHA, EN VERDE (opción A)

La pregunta del apartado 3 de «CUATRO PETICIONES» se le hizo al dueño y contestó «Dale»
sin elegir. **Decide el supervisor, y queda escrito para que él lo cambie:** opción A.
Un archivado con fecha entra al grupo de su fecha, en verde, con la nota «archivado», y
**sin botones** (no se verifica ni se asigna desde ahí); así la pastilla del calendario y
la cabecera del grupo dicen la misma cuenta, y un día resuelto solo por archivados ya no
se abre vacío. Flujo, Corrección y Asignar siguen sin archivados (decisión del 06).

Medido por el programador con `--falso 300`: el 20-09 decía afuera «me falta 4 de 5» y
dentro «4 de 4»; ahora «4 de 5» en los dos sitios, con el archivado en verde; el 09-09
(solo un archivado) se abría vacío y ahora dice «1 documento · 1 persona · resuelto»;
botones del renglón archivado `enabled=False` por UIA; el archivado no aparece en
Corrección, Flujo ni Asignar. Ocho pruebas de Inicio y Corrección fijaban lo contrario
(«el archivado no entra a DelDia») y se cambiaron conservando lo que vigilaban. Suite
App 1 137 → 1 145, medida por el supervisor tras fusionar.

**Lo que NO cubre, devuelto al dueño:** la línea «documentos: me falta N de M» del grupo
cuenta `Estado == Completa` (lo que escribe el Excel): un archivado sin ese estado se
lee «resuelto» en personas y «me falta» en documentos. Decidir si el archivado cuenta
como documento completo es suyo.

## 2026-09-15 — EL PAPEL PEGADO Y EL GIGABYTE: lo que se midió y lo que se decidió

Dos quejas del dueño el mismo día sobre la v13, con su base real de 76 casos: *«un PDF se
queda pegado y se carga a lugares que no le corresponde… está pegado a todos los casos,
se friza y se cierra solo»* y *«Fichas está consumiendo 1 GB de RAM; está lento»*. El
Claude de su PC midió su base y su registro (informe citado en PENDIENTES.md, entrada del
15): ninguna ruta compartida; cada entrada a Corrección abría dos casos; el OCR de bandas
no se cancelaba y se acumulaba (hasta 5 en vuelo, 30–58 s cada uno).

### El papel pegado (programador del papel; el supervisor repitió la suite: App 1 173)

Tres piezas, todas medidas con la ventana abierta: (1) **doble apertura** al entrar a
Corrección (`Grupos.cs` seleccionaba el índice 0 y abría antes de restaurar el elegido);
(2) **el visor pintaba sin turno** (`ComponerYPintar` asíncrono suelto: la imagen que
terminaba la última ganaba) y rasterizaba en el hilo de la ventana (un PDF de 44 MiB la
congelaba 1 883–2 327 ms); (3) **un OCR de bandas por apertura, sin cancelar**. Ahora: una
sola apertura (26 ms); turno de pintado con descarte de lo tardío; rasterizado fuera del
hilo (abre en 20 ms, la ventana viva); una sola lectura de bandas en vuelo, cancelable
(8 cambios seguidos: 5 canceladas, 1 descartada, 1 leída; antes 8 OCR).

Y tres cosas más que salieron midiendo:
- **Duplicado por contenido**: la misma ficha en dos carpetas (mismo SHA) no se marcaba
  porque el índice por hoja no aprendía los casos nacidos en la misma tanda. Ahora sí.
- **Impreso ajeno** (una clase de formulario que el lector no conoce, como el FORD2610 de
  fondos de ayuda): queda como ilegible «formulario desconocido», sin caso.
- **Botón «Comprobar que cada documento tiene su papel»** (Importar): arreglo de datos
  para una base con daño; sobre una dañada a propósito: 3 rutas perdidas recuperadas por
  carpetas hermanas, 1 par idéntico marcado como duplicado, 0 sin decidir; idempotente.

**Decisión del programador, devuelta al dueño y aceptada por el supervisor salvo que él
diga lo contrario:** cada caso guarda **una copia de su escaneo** en
`Documentos\Fichas\escaneos\<nombre>-<huella>.pdf` y apunta a ella. Motivo medido: un
escáner que reutiliza `Scan.pdf` pisaba el papel de casos anteriores, y renombrar carpetas
(las 9 rutas «… Complete» del dueño) dejaba casos sin papel. Coste: dobla el disco de los
escaneos (cientos de MB con miles de papeles). Lo que NO cubre: casos anteriores al 15
siguen con su ruta original hasta que la comprobación los toque; `PlanearLasBandas` sigue
pidiendo banda para el número de caso en cada apertura y nunca la hay (2,5 s de OCR por
apertura que ahora es una sola y cancelable; deuda en PENDIENTES); RapidOcrNet no se
puede abortar a medias.

### El gigabyte (programador de memoria y programador de la arena; el supervisor repitió
la suite: App 1 180, y NO repitió las sondas)

Medido con el programa publicado y los 16 PDF del corpus: el programa vacío pesa 146 MiB;
**la arena de memoria de ONNX Runtime dentro del motor de OCR** sube a 650 MiB con la
primera hoja y a 1 230 con la segunda, y se queda porque el motor vive hasta cerrar. Las
hojas del visor y las tarjetas no retienen nada.

**Lo decidido:** `EnableCpuMemArena = false` en la sesión de ONNX (lo demás igual) y el
motor se suelta solo tras 30 s sin leer (recargarlo cuesta 333–471 ms). Medido: sonda
sobre la hoja mayor, privados tras OCR 1/2/3: **706/1 300/1 321 → 160/177/200 MiB**;
programa publicado importando los 16: pico 1 577 → 1 069, tras importar 1 460 → 447, a los
60 s 1 460 → **293 MiB**. Coste de tiempo por hoja: entre 0 y +10 % (máquina cargada).
**La lectura no cambió**: 16 huellas SHA-256 por documento (26 hojas) idénticas, y por
fin fijadas en la suite (`PruebaDeLaHuellaDeLaLectura`, 17 pruebas), que es la regla de
no regresión de la lectura hecha prueba. Lectura 93 → 115.

**Devuelto al dueño:** el pico transitorio durante la importación sigue en ~1 GB;
`EnableMemoryPattern=false` lo bajaría a ~550 MiB con coste dentro del ruido; no se aplicó.
El umbral de 30 s de reposo lo eligió el programador.

## 2026-09-15 — LA HOJA QUE EL DUEÑO ELIGIÓ MANDA MIENTRAS ESCRIBE

Sus palabras: *«Cuando un PDF tiene dos hojas, a veces la primera es solo una factura y la
segunda es la correcta… al pasar a la segunda hoja y comenzar a escribir los datos, el
documento salta de manera automática a la primera hoja de la factura y no te deja colocar
la información.»*

Medido por el supervisor en el código y reproducido por el programador con la ventana
abierta (PDF sintético: hoja 1 factura con dos rótulos del formulario, hoja 2 el
formulario): al enfocar un campo, `AlEnfocarUnCampo` mostraba la hoja de la que el campo
se leyó; con la hoja 2 delante, teclear en «Unidad» devolvía el visor a la 1.

**Lo decidido:** enfocar o teclear no cambia de hoja. Ir a la hoja del campo queda como
acción explícita: un enlace por campo «Ver dónde se leyó (hoja N)». **Lo tecleado se anota
como salido de la hoja de delante** (`pagina_pdf` del caso y de la persona añadida a
mano), medido en la base: caso 1 → 2, persona nueva en la 2, procedencia `manual`.

**Regla del programador, devuelta al dueño:** un dato que el lector NO trajo sale de la
hoja de delante al teclearlo; un dato que SÍ leyó se queda en su hoja aunque se corrija
mirando otra (evita que un nombre de la hoja 4 de un formulario de grupo pase a la 1 por
arreglarle una letra). Alternativa: siempre la de delante.

**Defecto previo encontrado y arreglado de paso:** teclear la unidad y añadir una persona
perdía la unidad (también en master; `TextBox.TextChanged` es asíncrono según la
documentación oficial y llegaba con la guarda de pintado ya bajada). **Lo que NO cubre:**
importar sigue uniendo hojas por número de caso y no funde la unidad de la hoja 2 en un
caso abierto en la 1 (decisión abierta del 08/10). Suite App 1 180 → 1 203, medida por el
supervisor tras fusionar.

## 2026-09-15 — EL CAUCE DEL DOCUMENTO: siete de las nueve fases del plan, medidas

El dueño pidió *«que el flujo del código sea como un río que corre a un mismo cauce…
eficiente y bien optimizado»*. El planificador escribió el plan (PENDIENTES.md, «Plan del
2026-09-15», 9 fases con fuentes) y el supervisor lo lanzó por fases en programadores
distintos, cada uno en su terreno, repitiendo la suite tras cada fusión. Lo medido por
cada programador (el supervisor NO repitió las sondas, sí las suites):

| Fase | Antes → después | Lo que no cambia |
|---|---|---|
| R-2 motor de OCR sin arena de ONNX + se suelta tras 30 s en reposo | privados tras 3 hojas 1 321 → 200 MiB; programa a los 60 s de importar 1 460 → 293 MiB | 16 huellas de lectura idénticas, ahora fijadas en la suite |
| R-3 una pasada por el PDF, una imagen por hoja | aperturas PdfPig por PDF de 6 hojas 19 → 1; tramo sin OCR 1 435 → 504 ms por hoja | píxeles que ve el OCR idénticos byte a byte (0 de 37 870 000 distintos) |
| R-4 una transacción por hoja importada | confirmaciones por hoja 24 → 1; 77 → 13 ms por hoja (SQLite real) | «ninguna hoja se rechaza» sigue; migraciones sin transacción (fuera del pase) |
| R-7 reportes y paquetes en bloque | consultas del reporte 10 531 → 3; 1,15 → 0,50 s; paquetes 300 → 1 lista (752 → 45 ms) | PDF y Excel idénticos byte a byte en 39 archivos; Contratos no se abre (los puertos ya tenían lecturas en bloque) |
| R-1 el visor no congela y no repite | rasterizado fuera del hilo (ya el 15); volver a una hoja vista 3 rasterizados → 0; ventana quieta ≤ 2,9 ms; `ContarPaginas` 1 por documento; `BitmapImage` atada antes de `SetSourceAsync` (30 MiB menos que `SoftwareBitmapSource`, mismo tiempo) | topes 1 700 / 3 500 px |
| R-5 Corrección lee una vez por guardado | `ICasos.Listar` 2 → 1 (el plan decía 3) | ms sin diferencia medible (la lectura ya era barata) |
| R-0 medidor de memoria en `fichas.log` | cuatro líneas `MEMORIA` por sesión (ventana lista, fin de tanda, abrir documento, cierre) + «OCR soltado»; solo cifras | — |

**Sin hacer, a propósito:** R-6 (una sola fuente compartida por las cinco pantallas: la
que más «río único» da y la de más riesgo —una pantalla enseñando lo viejo—; va cuando
las demás lleven días medidas en la máquina del dueño) y R-8 (afinar el recolector solo
si R-2 no bastó; con 293 MiB no hace falta hoy).

**Devuelto al dueño:** el pico DURANTE la importación sigue en ~1 GB (`EnableMemoryPattern
= false` lo bajaría a ~550 MiB); las migraciones sin transacción; `BaseDePrueba` de
Reportes firma con nombres de campo que no coinciden con el generador (la métrica 1 de
sus pruebas daba siempre 0).

**Lo que el supervisor midió tras fusionar todo:** Lectura 123, Datos 214, Reportes 158,
Paquetes 151, Datos.Falso 46, Contratos 32, App 1 254 (1 253 en verde con la máquina
cargada por OneDrive; el cronómetro caído da 164 ms a solas, techo 200). Un `find` huérfano
de un programador llevaba 4 h buscando un `.onnx` por todo el disco (13 209 s de CPU):
cerrado por el supervisor; explica parte de la lentitud de las suites de la tarde.

## Reglas de no regresión

⚠️ **Procedencia:** estas seis las trae el plan del dueño como hallazgos de
sesiones anteriores. **No están medidas en este repositorio** — el código donde
se descubrieron ya no existe. Se conservan porque el coste de volver a tropezar
es alto, pero cuando el código vuelva a existir cada una necesita su medición
propia y su número.

| Regla | Por qué (según el plan, sin comprobar aquí) |
|---|---|
| `cv2.medianBlur(gris, 3)` para el filtrado | `fastNlMeansDenoising` se comía 56 de 65 segundos por página con la misma precisión |
| Rasterizar con tope de 3500 px en el lado largo | Sin tope, un escaneo grande a 300 DPI tardaba entre 5 y 10 minutos por página |
| Cargar explícitamente los modelos PP-OCRv5 del grupo latino | Los modelos por defecto de RapidOCR apuntan a chino e inglés; el español sube a casi 100% solo con los latinos |
| Medir las bandas (`franja_y`, `franja_x`) sobre escaneos reales, nunca sobre el PDF en blanco | Los márgenes del escáner mueven las proporciones y la extracción acaba capturando el encabezado en vez de los nombres |
| Usar `_suelo()` para el posicionamiento vertical, no un `dy` fijo | Con offset fijo el texto se monta sobre la fila de al lado |
| Al generar el PDF limpio, el canvas de datos se fusiona **sobre** la página base | Al revés se pierden las líneas y casillas impresas del formulario |

---

## Hallazgo técnico que gobierna la FASE 2

⚠️ **Procedencia: lo dice el plan del dueño y NO se ha comprobado aquí.** El PDF
de referencia no está en el disco (medido con `find`, 2026-09-02). Todo lo que
sigue se anota como hipótesis a verificar en cuanto aparezca un PDF real.

Los PDFs traen dos capas. El formulario lleno es imagen sin texto. Encima hay
anotaciones que **sí son objetos reales del PDF** y se leen sin OCR.

En `CASP2609_Zutano_Family.pdf`, página carta (612 × 792 puntos, sin rotación),
había 22 anotaciones:

- `/FreeText` con texto extraíble directo. El número de caso `CASP2609` es una.
  La corrección de fecha `8 Sept 2026` es otra.
- `/Ink`, trazos a mano sin texto, pero con color y grosor:
  - **rojo** `(0.890, 0.094, 0.176)` con `/BS /W` de 1.65 → **tachón**, anula el
    valor que tiene debajo.
  - **verde** `(0.494, 0.765, 0)` con `/BS /W` de 16.5 → **resaltador**, no anula
    nada, se ignora.
  - cualquier otro → se registra y **no se interpreta**.

El caso que lo prueba todo: en la fila "Date traveling to the temple" hay un
tachón rojo en `[35.9, 437.2, 106.2, 444.6]` sobre `September 7, 2026`, y un
FreeText `8 Sept 2026` en `[138.0, 424.8, 188.3, 446.2]`. Misma banda vertical,
uno al lado del otro. El OCR lee las dos fechas y no sabe cuál vale. Con las
anotaciones sí se sabe.

**Precedencia de valores en un campo, en este orden:** tachón que solapa anula el
OCR → FreeText en la misma banda gana con `origen='anotacion'` y confianza 1.0 →
si no hay ninguno de los dos, vale el OCR con su confianza → si hubo tachón y no
hay corrección, el campo queda vacío y marcado para revisión.

**Solapamiento:** un tachón anula una banda si el traslape vertical supera el 50%
de la altura de la banda. Un FreeText pertenece a una banda con el mismo
criterio. Nada de proximidad aproximada ni de «el más cercano».

**Conversión de coordenadas.** Las anotaciones vienen en puntos PDF con origen
abajo a la izquierda; la imagen rasterizada tiene el origen arriba a la
izquierda. Omitir la inversión deja los rectángulos espejados y el sistema anula
campos que estaban buenos. Necesita prueba propia.

```python
escala = alto_imagen_px / 792.0
y_img_arriba = (792 - y2_pdf) * escala
y_img_abajo  = (792 - y1_pdf) * escala
x_img_izq    = x1_pdf * escala
x_img_der    = x2_pdf * escala
```

**Casillas de ordenanzas.** Son marcas dibujadas, no texto; el OCR no las lee de
forma confiable. Se recorta cada celda, se binariza, se cuenta píxel oscuro sobre
área total y se compara contra un umbral. El umbral **se calibra midiendo
formularios reales**, no se elige a ojo, y queda en una constante con un
comentario que diga sobre cuántos formularios se calibró.

Las seis columnas, en orden: recibir ordenanzas propias, observar ordenanza de
sellamiento, traductor, investidura, sellamiento esposa a esposo, sellamiento
hijo a padres.

**Filas vacías.** El formulario tiene seis filas de personas y casi nunca vienen
todas llenas. Una fila sin nombre y sin MRN se descarta; no se guarda una persona
en blanco.

**Manuscritos.** Si más del 60% de los campos vuelven con confianza bajo 0.6, el
formulario probablemente está escrito a mano. No se insiste: el caso se marca
`captura_manual` y se abre directo en la pantalla de corrección, con los campos
vacíos y la imagen al lado.

---

## Reglas de formato de los campos

| Campo | Regla |
|---|---|
| `numero_caso` | 4 letras mayúsculas + 4 dígitos |
| `mrn` | ~~11 dígitos, patrón 3-4-4~~ → **3 dígitos, 4 dígitos y 4 caracteres cuyo último puede ser letra** (2026-09-04, migración 15: el dueño confirmó que «muchas cédulas tienen una A u otra letra al final») |
| `unidad_numero` | ~~6 dígitos~~ **6 o 7 dígitos** (ver abajo) |
| `fecha_viaje` | fecha real, y el mes tiene que coincidir con los últimos 4 dígitos del número de caso interpretados como AAMM |

---

## Decisiones de las fases posteriores

Se anotan aquí para que no se pierdan, aunque su fase no haya empezado.

- **Excel (FASE 4).** Se regenera completo tras **cada** guardado; no hay botón de
  exportar. `mrn` y `unidad_numero` con `cell.number_format = '@'` o Excel se come
  los ceros de delante de `055-1111-3853`. Fechas como objeto `date` de Python, no
  como cadena, o no se puede filtrar ni ordenar por mes. Se escribe a un temporal
  y se reemplaza al final; si el archivo está abierto, Windows lo bloquea y
  `openpyxl` lanza `PermissionError`: se atrapa y se avisa en pantalla. **Nunca
  fallar callado.** Sin celdas combinadas y sin colores que signifiquen algo.
- **Calendario (FASE 5).** Los casos que viajan en los próximos 7 días salen en la
  pantalla de inicio, arriba de todo, en rojo. No en una pestaña: si hay que dar
  un clic para enterarse, algún día no se da ese clic.
- **Compañeros (FASE 6).** La reconciliación del Excel que vuelve es por
  **`numero_caso` + `mrn`**, nunca por nombre: un acento de más crea un registro
  fantasma. Las filas cuyo par no existe en la base **no se insertan**; van a una
  lista de descartados. Los compañeros se desactivan, no se borran, o se pierde el
  historial de quién verificó qué.
- **Histórico (FASE 8).** Un caso se archiva (`archivado = 1` con fecha), nunca se
  borra. Los archivados salen de las listas de trabajo pero siguen contando en los
  reportes.
- **Empaquetado (FASE 9).** `--onedir`, no `--onefile`. Con `--onefile` los
  modelos `.onnx` se descomprimen a una carpeta temporal en cada arranque y el
  programa tarda entre 15 y 30 segundos en abrir.
