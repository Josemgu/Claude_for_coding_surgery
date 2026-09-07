# Estado

Dónde quedamos. Nada se borra: lo que deja de ser cierto se tacha en el sitio con
su motivo y su fecha.

---

## 2026-09-02 — Arranque del proyecto

### Lo que hay, medido

`ls -la` sobre `C:\Users\josem\OneDrive\Escritorio\Trabajo`:

```
drwxr-xr-x  .claude
```

Nada más. Después de esta sesión, además: `CLAUDE.md`, `DECISIONES.md`,
`ESTADO.md` y `EN-CURSO.md`.

Dentro de `.claude/`: `agents/` (abogado, diseñador, hacker-seguridad,
ingeniero-seo, lector, planificador, programador, qa, más
`alcance-por-rol.json`), `hooks/` (29 scripts), `congelados.txt` vacío y
`settings.json` con 25 hooks cableados.

### Lo que NO hay

- **Código.** Ni un `.py`. El dueño borró lo anterior.
- **El PDF de referencia** y **el esquema de datos.** ⚠️ La primera versión de
  estas dos líneas afirmaba el resultado de un `find` sin el comando ni su salida
  —falta contra `CLAUDE.md` §8, vista de paso por QA el 2026-09-02—. Con su
  comando y su salida, literales:

  ```
  $ find "C:/Users/josem/OneDrive" "C:/Users/josem/Documents" \
         "C:/Users/josem/Desktop" -maxdepth 5 \
         \( -iname "ESTADO.md" -o -iname "CLAUDE.md" -o -iname "PROCESOS.md" \
            -o -iname "esquema.md" -o -iname "*Zutano_Family*" -o -iname "CASP*" \) \
         2>/dev/null
  C:/Users/josem/OneDrive/Mi pequeno Secreto/Pastepad/pastepad/CLAUDE.md
  ```

  Único resultado, y es de otro proyecto. Ni `CASP2609_Zutano_Family.pdf` ni
  ningún `esquema.md`.

  QA lo remidió por su cuenta el mismo día **con denominador**, que es lo que le
  faltaba a mi medición: `-iname "*CASP2609*"` → 0 y `-iname "esquema*.md"` → 0,
  con control positivo de **57 PDFs y 57 `.md`** encontrados en esas mismas rutas.
  El detector ve; no hay nada que ver.

  El esquema es el bloqueo grande: el plan lo declara autoridad sobre nombres de
  tablas y columnas, así que la FASE 1 no arranca sin él.
- **`PENDIENTES.md`.** Es del planificador y el hilo principal tiene denegada su
  escritura (`propiedad-por-rol.sh`). Se delega.
- **Los cinco documentos de conocimiento** de `CLAUDE.md` §7. Se crean cuando
  haya algo medido que poner dentro.

### Git

~~Rama `master`. El repositorio git que cubre esta carpeta tiene su raíz en
`C:\Users\josem`, no en `Trabajo`: `git status` arrastra miles de archivos del
perfil de usuario. **Sin resolver.** Antes del primer commit del proyecto hay que
decidir si `Trabajo` lleva su propio repositorio.~~
**Resuelto el 2026-09-02:** `git init` en `Trabajo`. `git rev-parse
--show-toplevel` devuelve ahora `C:/Users/josem/OneDrive/Escritorio/Trabajo`.
Rama `master`. Primer commit: los cuatro documentos, `CLAUDE.md`, `.gitignore` y
`.claude/` entero. El motivo y qué se ignora, en `DECISIONES.md`.

### Lo que frenaba el trabajo

~~**Los roles no existen en esta sesión.** Respuesta literal de la herramienta al
lanzar el pase: `Agent type 'planificador' not found. Available agents: claude,
claude-code-guide, Explore, general-purpose, Plan, statusline-setup`. Los ocho
están en `.claude/agents/` con su frontmatter en regla, pero al arrancar esta
sesión la carpeta estaba vacía; `.claude/` apareció a las 13:35, ya en marcha.~~
**Resuelto el 2026-09-02** reiniciando Claude Code. El arranque anunció los ocho
roles. Causa confirmada: el registro se lee al arrancar y no se relee en caliente.

Sigue en pie, y no es un fallo sino el diseño: con `propiedad-por-rol.sh`, el
hilo principal tiene denegado escribir `PENDIENTES.md`, `docs/ARQUITECTURA.md`,
`docs/RUNBOOK.md`, `docs/INFORMES-QA.md` y cualquier `.py`. Todo eso se delega.

### Lo hecho hasta ahora

- Repositorio propio, local, sin remoto. Dos commits.
- `CLAUDE.md`, `DECISIONES.md`, `ESTADO.md`, `EN-CURSO.md` escritos.
- `PENDIENTES.md` entregado por el planificador y verificado por el supervisor:
  710 líneas, diez fases, los seis puntos del criterio de cierre cumplidos. El
  detalle de la verificación, en `EN-CURSO.md`.
- Medido el riesgo de OneDrive sobre la carpeta de datos. Decisión en
  `DECISIONES.md`.

### FASE 0 — cerrada el 2026-09-02, con un SÍ

RapidOCR con modelos PP-OCRv5 latinos **sobrevive a PyInstaller `--onedir`**.
Números medidos, reverificados por el supervisor ejecutando el `.exe`:

| Qué | Número |
|---|---|
| Tamaño del paquete | **245 MB**, 124 archivos |
| Arranque hasta ventana lista | **1.330 s** en su sitio · **4.914 s** aislado |
| Rutas de carga dentro de `_internal` | **11 de 11**, 0 fuera |
| Caracteres que difieren en la línea con tildes y eñe | **0 de 69** |
| Rasterizado | **2705x3500 px**, exacto en el tope |
| OCR de una página | **3.36 s** |

De esos 245 MB, **111.76 MB son `cv2`**, y 29.45 MB de ellos un códec de vídeo que
este proyecto no usa. Rebaja pendiente para la FASE 9, anotada en `DECISIONES.md`.

**Lo único que queda de la FASE 0 y no puedo hacer yo:** copiar `dist\prueba_ocr`
entera a la PC del trabajo, doble clic, y mirar que abra y saque
`CARACTERES_QUE_DIFIEREN 0 de 69`. Esa es la mitad de la pregunta que esta máquina
no puede responder, porque aquí sí hay Python.

### Las dos máquinas

- **Esta**: Python 3.14.7, pip 26.2.1 (`python --version`, `python -m pip --version`).
  Es la de desarrollo.
- **La del trabajo**: sin Python. Es donde el programa se usa, con doble clic. Lo
  dijo el dueño el 2026-09-02 y **no lo he comprobado** — no tengo acceso a ella.

### Lo que bloquea seguir

1. **No hay esquema de datos.** Bloquea la FASE 1 y, por dependencia, de la 3 a la
   8. El planificador recomienda sede en `docs/ARQUITECTURA.md`; **lo decide el
   dueño.**
2. **No está `CASP2609_Zutano_Family.pdf`.** Bloquea verificar la FASE 2.
3. **La FASE 7 no tiene especificación.** Ninguna decisión escrita sobre qué es un
   contacto con el líder ni qué campos lleva. Necesita un pase aparte.

~~### Siguiente paso

QA audita `PENDIENTES.md` — nada se cierra sin QA, y después el dueño. En
paralelo, el dueño decide dónde vive el esquema y aporta el PDF de referencia; sin
esas dos cosas la FASE 1 no arranca y la FASE 2 no se puede comprobar.

La FASE 0 sí es ejecutable ya: no depende de ninguna de las dos.~~
Superado el mismo día: se hicieron las diez fases. Ver abajo.

---

## 2026-09-02, cierre de la jornada — las diez fases codificadas

⚠️ **Falta del supervisor, señalada por QA:** estos documentos se quedaron en la
FASE 0 mientras el proyecto llegaba a la 9. Se ponen al día ahora, tarde.

### Lo que existe

Un programa completo. `fichas.py` arranca la ventana; siete paquetes de código
—`datos`, `extraccion`, `importacion`, `interfaz`, `espejo`, `paquete`,
`reportes`—; **499 pruebas en verde**, medidas por el supervisor sobre el árbol
mezclado con:

```
.venv/Scripts/python.exe -m unittest discover -s pruebas -t . -p "prueba_*.py"
Ran 499 tests in 92.194s
OK
```

~~⚠️ **El `-p` es obligatorio.** Sin él, `unittest` responde `Ran 0 tests` **con
código de salida 0**: una puerta que use el comando corto pasa en verde para
siempre sin ejecutar nada. Lo midió QA.~~

**Corregido el 2026-09-02, dos veces y por dos motivos.**

Primero, el número estaba mal: el código de salida de «no tests ran» en Python
3.12 y posteriores es **5**, no 0. QA midió 0 porque pasó la orden **por una
tubería**, y una tubería entrega el código del último mandato, no del primero.
El programador cayó en la misma trampa y la vio al repetirlo sin tubería. Yo di
el 0 por bueno y lo escribí aquí sin medirlo: falta mía.

Y segundo, **ya no es cierto de este repositorio.** El `load_tests` de
`pruebas/__init__.py` hace que un patrón que no casa caiga al bueno. Medido por
el supervisor, sin tubería:

```
$ .venv/Scripts/python.exe -m unittest discover -s pruebas -t .
Ran 520 tests in 160.822s
OK
EXIT=0
```

Ese `0` es un verde de verdad: ejecutó las 520. La suite pasa de ~90 s a ~160 s
porque ahora incluye las cuatro auditorías, y **62 de esos segundos son
`auditoria_extraccion_real` sobre los PDF de verdad**, que se salta sola donde no
los hay.

### El ejecutable

`C:\Users\josem\Fichas-entrega\Fichas` — **216,9 MiB**, 124 archivos, **0
deshidratados**. Arranca en **1,31 s** en caliente, **3,94 s** desde copia en
frío. Sin consola detrás. Se copia entera, `_internal` incluido, y se abre con
doble clic. No hace falta Python ni instalar nada.

Construido **fuera de OneDrive**, porque OneDrive convirtió 60 carpetas de un
`dist` anterior en marcadores de la nube y una carpeta deshidratada copiada a un
pendrive puede llegar vacía.

⚠️ En el repositorio queda `dist\Fichas-ROTA-NO-COPIAR`: un paquete al que le
faltan 10 archivos de verdad. Renombrado, no borrado, para que nadie lo copie
creyendo que es la entrega.

### Las fases, una por una

| Fase | Qué quedó |
|---|---|
| 0 | RapidOCR **sí** sobrevive a PyInstaller `--onedir`. `onnxruntime` publica rueda nativa para Python 3.14 |
| 1 | 7 tablas, 42 columnas. Ruta de datos por API de Windows. Todo parametrizado |
| 2 | Extracción con capa de anotaciones, medida sobre 4 documentos y 9 páginas reales |
| 3 | Pantalla de corrección con la tira del escaneo, color por origen y teclado completo |
| 4 | Espejo de Excel que se regenera tras cada guardado |
| 5 | Pantalla de inicio con lo que viaja en 7 días, calendario y pendientes |
| 6 | Compañeros, asignaciones, paquete de trabajo y reconciliación por `numero_caso`+`mrn` |
| 7 | Contactos con el líder, con historial y días transcurridos |
| 8 | Archivar, reportes en Excel y PDF, y las tres métricas |
| 9 | El ejecutable |

Esquema en la **versión 6**, con migraciones 2 a 6 aplicables en orden.

### Lo que QA encontró en la auditoría final

**Veredicto: VUELVE AL AGENTE.** El detalle está en `EN-CURSO.md`. Lo que bloquea:

1. **Dos compañeros sobre el mismo caso borraban la propuesta del primero sin
   rastro ni aviso.** En arreglo.
2. **La FASE 9 no cierra por su propio criterio:** exige probar el `.exe` en otra
   máquina sin Python, y ahí «no hay sustituto». **Solo lo puede hacer el dueño.**

### Lo que sigue abierto y necesita al dueño

- ⚠️ **Faltan dos valores de `estado_recomendacion`.** Solo constan `no_indicada`
  e `incompleta`. Sin el que signifique «resuelta»: `casos_en_riesgo` devuelve
  todo lo que viaja, y el reporte a los jefes escribe «no se puede saber» en vez
  de «viajó con la recomendación completa». Los dos avisos **se apagan solos** el
  día que se añadan a `datos/estados.py`.
- ⚠️ **Las casillas de ordenanzas se entregan desactivadas.** Hacen falta al menos
  tres formularios con la verdad conocida para calibrar el umbral. Hoy salen como
  «no leída», que es distinto de «no marcada» — QA lo verificó: 12 de 12 personas
  con las seis columnas en nulo.
- ⚠️ **No hay copia de seguridad de la base.** Deuda D-5, abierta desde el
  esquema. Es el riesgo más grande que tiene el trabajo de Miguel.
- **Probar el `.exe` en la PC del trabajo.** Cierra a la vez la FASE 0 y la 9.

### Lo que QA verificó que aguanta

- **Regla permanente 1:** extracción completa con la red cortada a nivel de
  socket — 66 módulos, 0 intentos de conexión. 0 peticiones HTTP. 0 puertos
  abiertos, con control positivo de 41 líneas TCP en la máquina. **0 runtimes de
  modelo de lenguaje** entre los 147 paquetes empaquetados; los únicos `.onnx`
  son los 3 de OCR, byte a byte idénticos a los del repositorio.
- **Regla permanente 5:** 28 de 28 filas de procedencia con `verificado=0` tras
  importar. 4 de 4 intentos de marcar verificado sin quién y cuándo rechazados
  por el `CHECK` del motor, no por un `if`.
- **No se pierde ninguna persona:** 6 páginas → 12 de 12.
- **El `.exe` lleva este código:** 73 de 73 módulos byte a byte idénticos al
  fuente.
- **El Excel bloqueado:** probado con Excel 16.0 de verdad. Dato guardado, aviso
  en español, `.xlsx` intacto byte a byte, sin caída.

---

## 2026-09-03 — Ya lee los formularios del dueño; el ciclo de QA sigue abierto

### Lo que cambió desde el cierre anterior

- **El programa lee los formularios españoles del dueño.** Medido por el
  supervisor con `extraer_documento` sobre sus dos PDF: `BARC2608`,
  `2026-08-25` / `2026-08-26`, `Cuatricentenaria`, 4 y 1 personas,
  `captura_manual: False`. Antes: todo `None` y `personas: 0`. Causa y arreglo
  en `DECISIONES.md`.
- **Verificar un caso suyo cuesta 2 gestos y 0 caracteres tecleados.** Lo midió
  QA; el supervisor no lo ha repetido.
- **Nada vacío se firma, y la firma cae si el valor cambia.** Dos hallazgos
  críticos de QA, cerrados en `f1dfb76` y `835823f`. El segundo verificado por el
  supervisor con `prueba_lo_firmado_que_cambia` → `Ran 18 tests, OK`.
- **Diálogos enteros en español** (`interfaz/dialogos.py`); `messagebox` fuera del
  código ejecutable, comprobado por el supervisor en el bytecode del `.exe`.
- **El proyecto viejo está medido:** su suite pasa (`Ran 93 tests, OK`) y lee el
  PDF del dueño (`23 campos leídos de 26`). Se trae su forma de leer y sus dos
  salidas —Excel del agente e informe—, no sus 26 campos: el dueño ordenó «solo
  los campos que yo necesito».

### El ejecutable

`C:/Users/josem/Fichas-entrega/Fichas`, 218 MB, según el informe del último
programador (`Fichas.exe` 10 526 276 bytes). Lleva las firmas que caen; **no
lleva** el Excel del agente nuevo ni el visor del documento entero, que están
en construcción. Se reconstruye una sola vez cuando cierren los dos.

### Corriendo ahora

- Excel del agente e informe para los jefes, como los del viejo (`paquete/`,
  `reportes/`, `datos/`).
- El PDF entero al lado de los campos (`interfaz/`, `extraccion/recorte.py`).

### Lo que sigue abierto

- ⚠️ **Las seis casillas de ordenanzas vienen borradas** en los PDF `_limpio` del
  dueño (tinta 0.0000 en las 36, botones en estado reiniciado). Pregunta pendiente
  al dueño: ¿existe el PDF **antes** de limpiar? Si sí, se leen exactas con
  `/AS != /Off`, unas 30 líneas, sin calibrar nada.
- ⚠️ Formularios en **francés**: nadie tiene sus etiquetas. El viejo resuelve la
  forma —un perfil por modelo de formulario—; el perfil francés es un hueco
  declarado, no se inventa.
- Faltan los dos valores de `estado_recomendacion`, la lista de países y la de
  templos. Todo del dueño.
- Un caso firmado que pierde la fecha vuelve a pendientes pero **no** a la franja
  roja de inicio; si el dueño lo quiere ahí, es una consulta nueva.
- No hay copia de seguridad de la base. Ni un escaneo español de papel con
  tachones ha pasado por aquí. Otra máquina sin Python: sin evidencia.

### 2026-09-03, tarde — el `.exe` de `Fichas-entrega` NO lleva todavía los cierres de la auditoría final

QA devolvió VUELVE AL AGENTE sobre `807bac3` (detalle en `EN-CURSO.md` y
`DECISIONES.md`). El ejecutable entregado **funde dos familias con el mismo
número de caso** si llegan en un solo documento escaneado, y enseña rótulos en
inglés. Está en arreglo, con reconstrucción del paquete al final. Hasta que QA
lo mida, el estado es **no listo**.

### 2026-09-03, noche — QA: APTA sobre `aa90ad1`

El `.exe` de `C:/Users/josem/Fichas-entrega/Fichas` (218 MB, `Fichas.exe`
10 611 446 bytes) es el primero con veredicto **APTA** de QA. Lo que queda son
dos medios y un bajo no bloqueantes (detalle en `EN-CURSO.md`) y las ocho
preguntas del dueño de `docs/ARQUITECTURA.md` §8. **Estado: listo para que el
dueño lo pruebe en la PC del trabajo**, que es la única prueba que nadie aquí
puede hacer.

### 2026-09-03, noche — Veredicto del dueño: «No me gustó para nada»

Lo probó con sus diez PDF reales en la PC del trabajo. Resultado, en sus
palabras y sus capturas: **ninguno quedó usable** —seis rechazados por «caso ya
existente», los cinco de `PAPH2608` con los campos vacíos—; el visor «lento, se
corta, tosco»; el trackpad no baja la pantalla; los avisos «ocupan todo el
programa»; y el proyecto viejo «duró menos y funcionaba mejor», lo que sobre su
PDF está medido: 23 de 26 campos contra 10 de 12.

QA había dado APTA sobre una muestra que no era la suya. **El veredicto del
dueño pesa más que el de QA, y es este.** El estado del proyecto es **no
aceptado**. Lo que se está construyendo es la respuesta a lo que él vio, y lo
prueba él.

### 2026-09-03, noche — dos de los tres pases del ciclo 5 cerrados

- **Revisar** (`4572693`): tarjeta por documento, «Sí, completa / No está
  completa», tablero de completados. Migración 14 sin registrar hasta que cierre
  el pase de identidad.
- **Inicio** (`657a5cc`): funciones añadidas sin rehacer; abrir el inicio pasa
  de 7 325 ms a 1 386–1 587 ms según su informe (el calendario se pintaba dos
  veces). Panel de 1 237 px en ventana de 1 100: se recorta, con prueba que lo
  fija.
- **Identidad por documento + duplicados + visor + trackpad + enganches +
  `.exe`**: corriendo. Al cerrar: suite entera medida por el supervisor, commit,
  y el `.exe` para el dueño.



## 2026-09-03, 23:40 — «Aún el dibujado es lento»: pase 4 en marcha

- Los tres pases del ciclo 5 están entregados. Inicio y Revisar commiteados;
  el de identidad por documento **sin commitear todavía**: espera a la suite
  entera medida por el supervisor con la máquina en silencio (dos suites
  concurrentes contaminaron dos intentos; la tercera corre sola).
- El dueño dice que el dibujado sigue lento. Sin cifra suya: su carpeta de
  datos no tiene `fichas.log`. Medido por el supervisor aquí: ~5 ms por widget
  de Tk, 464 widgets al abrir inicio, 1,2–1,6 s por clic. Remedio encargado en
  el **pase 4** (EN-CURSO.md): calendario y listas en un `Canvas`, pantallas
  que no se destruyen, un solo ayudante de trackpad, `avisos.py:64`, el `after`
  huérfano de corrección. Corre en un worktree; su `.exe` irá a
  `Fichas-entrega\Fichas-pase4` y no pisa el entregado.
- Pendiente del dueño: autorizar la descarga de Python 3.13 para medir Tk 8.6
  frente a Tk 9.0.4; el PDF de PAPH2608; su cifra de arranque.

## 2026-09-04, 00:15 — Los tres pases del ciclo 5 en `master`, suite OK

- `2b6e92a` cierra el tercer pase. Suite entera medida por el supervisor en
  silencio: **1139 pruebas, OK, 1 omitida (601 s)**.
- `C:\Users\josem\Fichas-entrega\Fichas` es el `.exe` del tercer pase (esquema
  14); NO se ha comprobado que corresponda exactamente a `2b6e92a`: se
  construyó a las 22:58 y después hubo dos ediciones pequeñas. El pase 4 lo
  reconstruye en `Fichas-pase4`.
- Pase 4 (el dibujado) corriendo en worktree desde `2b6e92a`.

## 2026-09-04, 08:00 — Ciclo 5 completo en `master` (`c0a268c`), a la espera del dueño

- Cuatro pases cerrados. Suite entera medida por el supervisor: **1159 OK**.
- Inicio pasa de 1,6 s a 0,2 s por clic, medido aquí. `Fichas-entrega\Fichas`
  es el `.exe` del pase 4; el anterior, `Fichas-anterior-2b6e92a`.
- Del dueño: su veredicto con sus PDF, la cifra del pie de la ventana, el
  trackpad real, y si autoriza descargar Python 3.13 para medir Tk 8.6.

## 2026-09-04, 10:45 — El dueño decide pasar a C# con WinUI 3; FASE C0 en marcha

- **Decisión del dueño** (DECISIONES.md, «El dueño decide»): el programa completo
  se reescribe en C# con WinUI 3. El `.exe` de Python sigue siendo lo que usa
  hasta que exista el nuevo; ningún pase nuevo en Tk salvo el ciclo 6 ya abierto.
- **Plan:** `docs/adr/ADR-0003-csharp-winui3.md` (21 fuentes) y «Fases del
  programa en C#» en PENDIENTES.md, C0–C9. Recomendación del planificador:
  WinUI 3 en carpeta autocontenida, OCR nativo `Windows.Media.Ocr` (es-MX ya
  presente, 0 MB) frente a onnxruntime (148 MB y portar DB/CTC/cls).
- **Máquina:** .NET SDK 10.0.400 instalado en `C:\Users\josem\.dotnet` sin
  administrador; consola compila. Modo de desarrollador no activado.
- **Corriendo:** FASE C0, la espiga medida (3 000 filas con desplazamiento,
  publicación en carpeta y archivo único, arranque, OCR de Windows sobre los
  PDF reales), entrega en `Fichas-entrega\Fichas-C0`. Y el pase 1 del ciclo 6
  en Python (Guardar y la franja), entrega en `Fichas-entrega\Fichas-ciclo6`.
- **Del dueño:** la lista de idiomas de OCR en la PC del trabajo (línea de
  PowerShell en PENDIENTES, FASE C0); y sus dos decisiones devueltas por el
  planificador: archivo único o carpeta, y trabajar sobre copia de la base.

## 2026-09-04, 17:36 — El programa en C# abre la base real; falta Inicio y la prueba de QA

**Lo que ya hace**, medido por sus programadores y con la suite verificada por el
supervisor (505 pruebas, 0 fallos): abre una copia de la base real y la migra de
la 13 a la 15 sin perder filas; importa los siete escaneos reales con OCR real
en 60,9 s dando 7 casos y 7 personas, 0 rechazados; los mismos siete otra vez
salen como 7 duplicados **avisados**; corregir un campo escribe en SQLite; el
Excel del compañero y el PDF de los jefes se escriben de verdad; el `.exe`
publicado abre la base y deja «ventana lista en 1108 ms».

**Entrega:** `C:\Users\josem\Fichas-entrega\Fichas-nuevo\`, 560 archivos,
334,6 MiB tras quitar 85 MiB de `.pdb` de terceros. **NO se le ha dado todavía
al dueño**, y por tres razones concretas:

1. **La pantalla de Inicio está a medias.** El primer agente se colgó a las
   13:22 y a las 16:26 seguía sin tocar nada; se rescataron sus 1 077 líneas
   (compilan) y otro agente la está terminando.
2. **La pantalla de importar no se ha visto abierta nunca.** Compila y viaja en
   el paquete; los 7 de 7 están medidos llamando al motor, no pulsando el botón.
   El selector de archivos no se ha abierto jamás.
3. **QA no ha auditado nada del C#.** El dueño pidió QA por fase y aún no se ha
   hecho ni una.

**Dos defectos en arreglo:** Corrección escribía el nombre del campo en
PascalCase y dejaba dos filas por campo, así que la pantalla no veía la banda
del papel; y el `CHECK` del número de caso diverge entre una base migrada desde
la del dueño y una nueva.

**Lo que el dueño respondió hoy** y está en DECISIONES: ni teléfono ni correo,
solo el canal por el que habló el compañero; el informe de los jefes como el
viejo; él controla quién entra en la lista. **Sigue sin contestar**: qué pasa al
asignar un caso que ya lleva otro compañero.

---

## 2026-09-06 — El día que el dueño describió su trabajo y el programa cambió de forma

### Lo que pidió, y está dentro

Dictó doce cosas seguidas mientras probaba el programa. Nueve están hechas y
fusionadas; las tres que faltan están abajo con su motivo.

| Lo que pidió, con sus palabras | Dónde está |
|---|---|
| «debe pasar a archivado y no aparecer más en ningún lado» | seis sitios, no los tres que el supervisor había medido |
| «una tab solo para esto: completar información de documentos que faltan» | pestaña **Completar**, la octava |
| «cuando doy click aparecen todos los PDF… solo los que necesitan revisión» | Revisar abre en «Sin revisar» |
| «eliminar la columna Fecha de solicitud, Estaca o Distrito» | 18 → 16 columnas |
| «lo que debo revisar son los paquetes de los agentes» | el paquete que vuelve se mira entero y se firma en bloque |
| «necesito ver el nombre de las personas, no de la rama o barrio» | Asignar dice quién viaja |
| «no hay nada que me indique color o algo» | ocho colores medidos en píxeles, con la palabra siempre al lado |
| «debe decir fecha pasada completada» | así se llama donde se ven a propósito |
| «si yo lo archivo debe desaparecer aunque no estén completos» | ya lo hacía; se verificó y se dejó dicho dónde |

### Las cifras, medidas por el supervisor con `dotnet test`

`Fichas.Pruebas.App`: **711 pasadas, 6 omitidas, 0 rojas** (717 en total).
`Fichas.Pruebas.Lectura`: **50, 0 rojas** (eran 43).
`Fichas.Pruebas.Paquetes`: **130**. `Datos`: **167**. `Reportes`: **103**.
`Datos.Falso`: **37**. `Contratos`: **21**.

Las 6 omitidas son a propósito: son las divergencias del veredicto, con `[Ignore]`
que nombra su motivo, y se encienden cuando el pase de la unificación cierre.

⚠️ **Con la máquina cargada de agentes, las pruebas de tiempo se caen solas.** La
de Reportes se midió roja en una tanda completa y **103/103 corriendo a solas**.
No es del programa.

### Los tres agujeros que aparecieron sin que nadie los buscara

1. **Un campo tachado llegaba a la base como bueno.** `Extraccion.cs` construía el
   campo con siete argumentos y la marca de tachón, que es el noveno, se quedaba
   en su valor por defecto. **Arreglado y medido de punta a punta**: 0 antes, 1
   después, en la columna de la base. Y se aprendió que **los siete escaneos
   reales traen tachones**.
2. **Una cédula tachada no se marca nunca.** Mismo defecto, en el camino de las
   personas. **En arreglo.**
3. **Seis divergencias entre los dos veredictos de «le falta algo».** Un documento
   podía no estar en ninguna de las dos listas, y otro quedaba atrapado en la cola
   para siempre. **En arreglo**, con el archivo congelado ya abierto.

### Lo que falta, y por qué

- **Cuáles de las seis preguntas pesan** para dar por completa una recomendación:
  ⛔ esperando una foto suya. No se programa sin ella.
- **Corrección solo con lo que le falta**, en vez de todo junto: la cola nueva ya
  hace el trabajo; queda decidir si la ventana de incompletos de Inicio sobra.
- **QA no ha auditado nada de esto.** Nueve fusiones en un día, todas verificadas
  por el supervisor repitiendo las mediciones, ninguna auditada a ciegas.

### Residuos con datos reales, contados y NO borrados

`C:\Users\josem\Fichas-datos-programador-tachon-2`: 2 archivos, 0,1 MiB. Un PDF
derivado de uno suyo en el scratchpad de la sesión. Y las carpetas de entrega de
cada programador. **Se cuentan y se le enseñan al dueño; no se borran sin él.**
