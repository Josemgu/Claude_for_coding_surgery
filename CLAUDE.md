# Fichas

Programa de escritorio para Windows que lee formularios de recomendación al
templo escaneados en PDF, extrae los datos a una base SQLite, deja que Miguel los
corrija a mano y avisa de los casos que viajan pronto con la recomendación
incompleta.

El daño que evita es concreto: una persona que llega al templo y no puede entrar
porque su recomendación estaba mal y nadie lo vio a tiempo.

---

## 1. Las reglas permanentes

No se negocian en ninguna fase. Si un plan, un pase o un agente las contradice,
mandan estas.

1. **Sin IA generativa para leer campos.** Ni para OCR, ni para "arreglar" texto
   leído, ni para adivinar un dato que falta. Un MRN o una fecha de viaje
   inventados causan daño real. Solo OCR determinista y reglas.
2. **Un solo ejecutable.** El usuario final abre con doble clic. Sin instalación,
   sin Python en la máquina, sin servidor web, sin puertos abiertos.
   *Precisado por el dueño el 2026-09-11 (`DECISIONES.md`, «EL DUEÑO PIDE
   INSTALADORES»):* el programa llega a la máquina con un **instalador por usuario,
   sin administrador** (`Instalar-Fichas-vN.exe`), y actualizar es ejecutar el nuevo
   encima. Lo que la regla sigue prohibiendo es todo lo demás: nada que instalar
   aparte, ni Python, ni runtime, ni servidor, ni puertos. Los datos siguen fuera,
   en `Documentos\Fichas`, y el instalador no los toca.
3. **Sin pandas.** Solo `openpyxl` para Excel. Pandas infla el ejecutable unos
   40 MB sin aportar nada aquí.
   *Desde el C# (2026-09-04):* el Excel lo hace `ClosedXML`; la regla que queda es
   la misma —ninguna biblioteca gorda que no aporte— y no se añade un paquete al
   programa sin decir en `DECISIONES.md` cuánto pesa y para qué.
4. **Español en todo:** variables, funciones, tablas, columnas, comentarios,
   textos de interfaz y mensajes de error.
5. **Nada se marca como verificado automáticamente.** El sistema propone, Miguel
   confirma. Siempre.
   *Precisado por el dueño el 2026-09-03 (`DECISIONES.md`, «Regla definitiva del
   Excel de los compañeros»):* la **firma de campos** («Todo correcto»,
   `verificado_por`) es de Miguel y nunca automática; el **estado de la
   recomendación** (`completa` / `no_completa`) lo escribe el Excel que devuelve el
   compañero, con su nombre — *«el documento que ellos llenan es el que marca, y
   dice completado por Sandy»*. Son dos cosas y no se mezclan.
   *Movido de sitio por el dueño el 2026-09-06 (`DECISIONES.md`, «EL DUEÑO MUEVE
   LA REGLA 5»):* Miguel **no** confirma documento a documento lo que el escaneo
   leyó entero — *«el sistema es para escanear información; si yo tengo que
   verificarla luego, ¿para qué me sirve?»*—. Lo que confirma, sí o sí, son **los
   paquetes que vuelven de los agentes**: *«lo que debo revisar son los paquetes
   de los agentes; solo verificar las informaciones, y ya»*. La regla sigue viva,
   pero se aplica ahí. ⚠️ Y sigue habiendo un suelo que no se toca: un documento
   solo se llama completo si se leyeron **todos** sus campos y cada uno pasó su
   comprobación de formato; lo que falte, lo dudoso y lo que no pase la
   comprobación va a la cola para que él lo complete a mano. No hay «completo por
   defecto».
   *Precisado por el dueño el 2026-09-14 (`DECISIONES.md`, «LAS SEIS EN «SÍ» MARCAN
   COMPLETADO»):* las seis preguntas las marca él, una a una o de un tirón, con su
   firma y su origen; el estado «completa» del documento **se deriva** de esas
   marcas, solo cuando todas sus personas tienen las seis en «sí» y no le falta
   ningún campo; se firma con el administrador (uno activo; con ninguno o dos no se
   marca), y deshacerla es a mano: un «sí» que vuelve a «no» no desmarca solo.
6. **Cada fase termina en commit.** Rama por fase, merge cuando pase el criterio
   de aceptación.

## 2. Las reglas de no regresión

Están en `DECISIONES.md` con su motivo y con la advertencia de qué se midió y qué
no. Deshacer una de ellas rehace el trabajo.

## 3. Tecnología

Desde el 2026-09-04 el programa es C# (`csharp/Fichas`, ver la memoria
«reescribir Fichas en C# con WinUI 3»). El Python original se retiró del
repositorio el 2026-09-11 por orden del dueño; queda en el historial de git.
Los paquetes, medidos en los `.csproj` el 2026-09-11:

| Para | Se usa |
|---|---|
| Ventanas | WinUI 3 (`Microsoft.WindowsAppSDK` 2.4.0), sin paquete MSIX |
| Leer anotaciones y campos del PDF | `PdfPig` 0.1.11 |
| Rasterizar páginas | `PDFtoImage` 5.4.0 (PDFium, con `WithFormFill`) |
| OCR | `RapidOcrNet` 4.1.0 con tres modelos `.onnx` PP-OCR |
| Base de datos | `Microsoft.Data.Sqlite` 10.0.11 |
| Excel | `ClosedXML` 0.105.1 |
| Publicación | `dotnet publish` autocontenido (`publish.ps1`), zip e instalador Inno Setup 6 |

Sin ningún servicio de red que escuche. Sin llamadas a modelos de lenguaje en
tiempo de ejecución.

## 4. Dónde vive cada cosa

- **La base SQLite y el Excel espejo:** `Documentos\Fichas`, fuera de la carpeta
  del programa. Si van dentro, una actualización se lleva los datos.
- **El código:** en este repositorio, en `csharp/Fichas`.
- **Los modelos `.onnx`:** los trae el paquete `RapidOcrNet` y `publish.ps1` se
  niega a publicar si no salen los tres en la carpeta; un paquete sin ellos
  arranca y no lee nada.
## 5. Los cuatro documentos de trabajo

- `ESTADO.md` — dónde quedamos.
- `DECISIONES.md` — qué se decidió y por qué.
- `PENDIENTES.md` — la deuda abierta y las fases por hacer. **Del planificador.**
- `EN-CURSO.md` — el ciclo activo: el pase, la entrega y los hallazgos de QA,
  como secciones. Se vacía al cerrar el ciclo.

Un pase, un FIXES o un informe de QA no son archivos: son secciones de
`EN-CURSO.md`. Nada se borra de un documento de estado: se tacha en el sitio con
su motivo y su fecha.

## 6. Cómo se pide una fase

Una fase por vez, nunca dos a la vez.

```
Lee CLAUDE.md, DECISIONES.md y PENDIENTES.md completos antes de escribir nada.
Ejecuta la FASE N. Respeta las reglas permanentes y las de no regresion.
No adelantes trabajo de fases posteriores.
Al terminar, muestrame como se cumple cada punto del criterio de aceptacion.
```

Si una fase se pone larga, se corta y se hace commit de lo que funciona antes de
seguir. Es preferible una fase a medias que funciona a una fase completa que no
se puede probar.

## 7. Los documentos de conocimiento

Cada uno con su rol titular. Ninguno existe todavía; se crean cuando haya algo
medido que poner dentro, no antes.

| Documento | Qué es | Titular |
|---|---|---|
| `docs/ARQUITECTURA.md` | el mapa del sistema | planificador |
| `docs/RUNBOOK.md` | fallos y su resolución | programador |
| `docs/BITACORA-SESIONES.md` | el acta de cada sesión | supervisor |
| `docs/GUIA-TECNOLOGIAS-BITACORA.md` | el libro de tecnologías | programador |
| `docs/INFORMES-QA.md` | el único documento de QA de todo el proyecto | QA |

Y `docs/adr/ADR-*.md`: un ADR por decisión investigada, inmutable.

El esquema de datos (nombres de tablas y columnas) manda sobre cualquier plan.
Todavía no existe: ver `PENDIENTES.md`.

## 8. Cómo se afirma algo aquí

Cada afirmación va con su comando y su salida, o con «lo dice X y NO lo he
comprobado». Nunca un hecho a secas. Un documento no es una medición: es lo que
alguien midió o supuso en su momento. Cuando un documento y el código no
coinciden, gana el código y el documento se corrige.
