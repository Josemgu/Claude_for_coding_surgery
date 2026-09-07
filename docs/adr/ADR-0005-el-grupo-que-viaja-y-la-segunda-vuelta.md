# ADR-0005 — El grupo que viaja es la unidad de trabajo, y la segunda vuelta con los gerentes

- **Fecha:** 2026-09-05
- **Estado:** propuesto — **cuatro decisiones son del dueño y están marcadas ⛔**
- **Autor:** el planificador
- **Origen:** `DECISIONES.md`, entradas del 2026-09-05 «Lo que el dueño pidió
  probando el programa: seis cosas» (punto 6) y «El calendario deja de ser
  adorno: es por donde se trabaja».
- **Sustituye a:** nada. **Corrige:** `PENDIENTES.md` DC-17 (ver §9).

---

## 0. Cómo se midió lo que hay aquí dentro

Todo lo que sigue lleva su comando y su salida. Lo que no pude medir está en §10.

**La base sobre la que medí.** La base viva del dueño, copiada a la carpeta de
trabajo y abierta en solo lectura. Nunca se tocó la original.

```
cp "C:/Users/josem/Documents/Fichas/fichas.db" "$SCRATCH/copia.db"
python -c "... select * from version_esquema ..."
```

Salida, lo que importa: **versión 17**, aplicada el 2026-09-05 10:39:20. Nueve
tablas. Y su contenido de hoy: **2 casos, 0 personas, 1 compañero (`Sandy`),
1 asignación, 0 contactos.**

**La base de 3 000.** No existía, así que la construí con el DDL exacto de la
base viva (`select sql from sqlite_master`), 3 000 casos con fecha de viaje
repartida en 30 días y de 1 a 10 personas cada uno:

```
casos: 3000 personas: 16253
```

Es la base sobre la que están medidos todos los milisegundos de este documento.
**Es SQLite 3.50.4 desde Python, no `Microsoft.Data.Sqlite`**, y por eso ningún
criterio de aceptación de §8 usa mi número pelado: usa mi número con margen, y
lo dice.

---

## 1. El problema, en una frase

El programa está construido alrededor del **documento**. El dueño acaba de decir,
usándolo, que su unidad de trabajo es **el grupo que viaja un día**:

> *«así puedo ver los grupos por fechas y saber con qué grupo trabajar»*

Y ha añadido una capa que no existe en ninguna parte del plan: cuando el
compañero no consigue resolver un caso, **el caso pasa a un gerente**, que habla
con los líderes de estaca y de distrito. Es una **segunda vuelta**.

Las dos peticiones no son pantallas. Cambian qué cosa es «una tarea».

---

## 2. Qué es un «grupo» — y la decisión es que no es una tabla

### 2.1 Lo que hay hoy

`casos` tiene `fecha_viaje` (ISO-8601, con su `CHECK` de forma) y
`unidad_numero`. Las personas cuelgan del caso (`personas.caso_id`). Un caso
puede llevar una persona o diez: en los siete escaneos reales del dueño lleva
una; en `pdfs_referencia/` hay dos archivos llamados
`SURB2609_Suriname_Group_Complete.pdf`, que por su nombre llevan varias.

Y ya existe el índice que hace falta:

```sql
CREATE INDEX idx_casos_viaje_activos ON casos (fecha_viaje) WHERE archivado = 0
```

### 2.2 Las dos opciones que aplican de verdad

**Opción A — el grupo es una consulta.** Grupo = todos los casos no archivados
que comparten `fecha_viaje` (y, si se decide, `unidad_numero`). Cero columnas
nuevas, cero tablas nuevas, cero mantenimiento por parte del dueño.

**Opción B — el grupo es una tabla `grupos`** con su nombre, su fecha, su líder,
y `casos.grupo_id`. Permite que un grupo tenga nombre propio, y que dos casos de
la misma fecha estén en grupos distintos a propósito.

### 2.3 Qué cuesta cada una, medido

La opción A, sobre la base de 3 000, con los índices que ya existen:

| Consulta | Resultado | Tiempo | Plan |
|---|---|---|---|
| El día `2026-09-08` con sus personas | 98 casos, 517 personas | **1,03 ms** | `SEARCH c USING INDEX idx_casos_viaje_activos (fecha_viaje=?)` |
| El mes entero agrupado por día | 30 días | **5,41 ms** | `SEARCH casos USING COVERING INDEX idx_casos_viaje_activos` |

`explain query plan` confirma que ninguna de las dos hace un barrido de tabla.
**La agrupación por fecha ya está indexada desde la versión 1 del esquema.**

La opción B cuesta: una migración que reconstruye `casos` (la tercera
reconstrucción de esa tabla: ya lo hicieron la 12 y la 16), un repositorio nuevo,
una pantalla de administración de grupos, y **una tarea de mantenimiento
permanente para el dueño** — decir a qué grupo va cada documento que entra. Nada
de eso lo pidió.

### 2.4 Decisión

**Opción A.** El grupo es una consulta, no un registro. Nadie mantiene nada, y el
día que entre un documento con fecha `2026-09-08`, entra solo en su grupo.

**El motivo de fondo, y no es el rendimiento:** un grupo-tabla puede quedarse
desincronizado de la fecha del papel; una consulta no puede. Si el OCR corrige
una fecha, el documento cambia de grupo sin que nadie haga nada. Con la opción B
habría que acordarse.

### 2.5 ⛔ Lo que decide el dueño: ¿la unidad parte el día?

Si el 8 de septiembre viajan la unidad `100014` y la unidad `100027`, ¿es **un
grupo de dos unidades** o **dos grupos el mismo día**?

- En sus datos de hoy **no se nota**: los siete escaneos son todos `CASP2609`, la
  misma unidad y todos con `fecha_viaje = 2026-09-08` (medido por el supervisor
  el 2026-09-04, `DECISIONES.md`).
- Pero llegará: en `pdfs_referencia/` conviven tres prefijos de caso distintos
  —`CASP2609`, `PARB2609`, `SURB2609`—, que son tres unidades.

**Mi recomendación: dos grupos, con el día como cabecera.** Motivo: él habla con
un líder por unidad, no con «el líder del martes». Si prefiere un solo grupo por
día, es cambiar una cláusula `GROUP BY` y ninguna otra cosa.

### 2.6 Lo que la agrupación por fecha arregla sola, y conviene que se sepa

Uno de los siete escaneos **lleva escrito `CASD2609` dentro de su propia
anotación** en vez de `CASP2609` — error de tecleo de quien llenó el formulario,
comprobado por el supervisor con `pypdf` el 2026-09-04. Hoy eso es un caso
suelto y perdido. **Agrupando por fecha, ese documento aparece en el grupo del 8
de septiembre junto a los otros seis**, aunque su número de caso esté mal. La
fecha lo recupera; el número no lo recuperaba.

---

## 3. Los tres estados que pide: son dos ejes, no uno

Sus palabras: *«no completado, no se pudo comunicar con el líder, o el líder no
lo hizo»*.

### 3.1 Por qué no caben en el estado que ya hay

`casos.estado_recomendacion` es texto libre y el programa lo lee con
`EstadoDeRecomendacion { SinMarcar, Completa, NoCompleta }`
(`Fichas.Contratos/Modelos/Enumeraciones.cs`).

De los tres que pide, **el primero ya existe**: «no completado» es `NoCompleta`.
Los otros dos **no son estados alternativos: son el motivo de ese**. Un caso que
no está completo porque no se pudo hablar con el líder **sigue estando no
completo**. Meterlos en el mismo enumerado convertiría «no completa» en un cajón
que a veces dice qué pasó y a veces no.

**Son dos ejes:**

| Eje | Valores | Quién lo escribe |
|---|---|---|
| ¿Está completa? | sin marcar · completa · no completa | el Excel del compañero, o Miguel a mano |
| ¿Por qué no? | *(nulo)* · no se pudo comunicar · el líder no lo hizo · otra razón | el mismo que escribió el eje 1 |

### 3.2 Los cuatro sitios que ya existen, y por qué ninguno sirve

El pase me pidió mirar lo que ya está antes de inventar columnas. Lo miré. Los
cuatro, uno a uno:

| Candidato | Por qué NO |
|---|---|
| `personas.llamo_al_lider` (0/1) | **No distingue los dos casos que él pide.** «Lo llamé y no contestó» y «lo llamé, contestó y no lo hizo» son los dos `llamo_al_lider = 1`. Además su grano es la persona, y el estado que él quiere ver en el calendario es del documento |
| `personas.estado_propuesto` (texto libre) | Es la propuesta del compañero **sobre una persona**, y ya la escribe la vuelta del Excel. Reutilizarla mezcla dos cosas en una columna sin lista cerrada |
| `personas.motivo_no_viajo` | Está atada por `CHECK` a `pudo_viajar`: solo se puede escribir cuando `pudo_viajar = 0`. Es «esta persona no viajó», que es otra pregunta |
| `contactos.respondio` + `contactos.medio` | Sí distingue respondió/no respondió, pero su grano es **un intento**, no «cómo quedó el caso». Y en el C# `contactos` **no tiene repositorio**: medido, `ls csharp/Fichas/Fichas.Datos/Repositorios/` da 7 archivos y ninguno es de contactos |

### 3.3 Decisión

**Dos columnas nuevas en `casos`, con lista cerrada:**

```sql
ALTER TABLE casos ADD COLUMN motivo_no_completa TEXT
  CHECK (motivo_no_completa IS NULL OR motivo_no_completa IN
         ('no_se_pudo_comunicar','el_lider_no_lo_hizo','otra_razon'));

ALTER TABLE casos ADD COLUMN motivo_del_companero TEXT
  CHECK (motivo_del_companero IS NULL OR motivo_del_companero IN
         ('no_se_pudo_comunicar','el_lider_no_lo_hizo','otra_razon'));
```

**Son dos y no una por el mismo motivo que la migración 14 desdobló el estado:**
`casos` ya guarda por separado `estado_recomendacion` (el vigente, el que se ve)
y `estado_del_companero` (lo que dijo su hoja, que **no se borra** cuando Miguel
corrige encima). El motivo tiene que seguir esa misma partición o el día que
Miguel cambie el motivo se perderá lo que dijo el agente — que es justamente el
dato del que se alimenta el reporte del gerente (§5).

**El texto de la lista se guarda en clave, no en prosa** (`no_se_pudo_comunicar`,
no «No se pudo comunicar con el líder»): la prosa es de la pantalla, y así se
puede cambiar la redacción sin migrar datos.

---

## 4. Quién es un gerente

### 4.1 Lo que hay

`companeros` tiene cinco columnas: `id`, `nombre`, `activo`, `desactivado_en`,
`creado_en`. **No hay ninguna noción de rol.**

Y hay **siete claves foráneas apuntando a `companeros(id)`**, medidas con
`pragma foreign_key_list` sobre las nueve tablas de la base viva:

```
asignaciones.companero_id      -> companeros.id
contactos.contactado_por       -> companeros.id
procedencia_campo.verificado_por -> companeros.id
filas_descartadas.companero_id -> companeros.id
casos.estado_del_companero_por -> companeros.id
casos.estado_marcado_por       -> companeros.id
personas.propuesto_por         -> companeros.id
TOTAL claves foraneas hacia companeros: 7
```

### 4.2 Las dos opciones

**Opción A — una columna `rol` en `companeros`.** Un gerente es un compañero con
`rol = 'gerente'`. Las siete claves foráneas siguen funcionando sin tocar nada:
un gerente puede recibir una asignación, marcar un estado y firmar exactamente
como hoy.

**Opción B — una tabla `gerentes` aparte.** Habría que decidir, para **cada una
de las siete** claves foráneas, si acepta también un gerente; y las que sí,
volverse polimórficas (`quien_id` + `quien_tipo`), que es la construcción que
SQLite no puede sostener con una clave foránea de verdad.

### 4.3 Decisión

**Opción A.**

```sql
ALTER TABLE companeros ADD COLUMN rol TEXT NOT NULL DEFAULT 'companero'
  CHECK (rol IN ('companero','gerente','administrador'));
```

Y **el `administrador` es el tercer rol de la misma lista**, no una tabla ni un
ajuste aparte: es lo que hace que «el administrador lo hizo» (§6) tenga a quién
apuntar sin inventar nada.

**Nadie se crea solo.** Decisión del dueño del 2026-09-04: *«yo debo tener el
control de quién se añade y quién no»*. Un nombre que llegue en una hoja
devuelta y no esté en la lista **se señala y espera**; y cambiar el rol de
alguien es una acción de Miguel, nunca del programa.

### 4.4 Que la migración es legal, verificado en la documentación oficial

**Fuente:** `https://sqlite.org/lang_altertable.html`, consultada el
**2026-09-05**. Las restricciones que la página lista para `ADD COLUMN`, en su
texto literal:

> "The column may not have a PRIMARY KEY or UNIQUE constraint."
> "If a NOT NULL constraint is specified, then the column must have a default
> value other than NULL."

**Ninguna de las restricciones prohíbe un `CHECK`.** La página sí advierte que
*"the added constraints are tested against all preexisting rows"* — que aquí es
inofensivo: `rol` entra con `DEFAULT 'companero'`, que está en la lista, y los
dos motivos entran como `NULL`, que la cláusula admite.

**Y lo medí, no me fié de la página** (SQLite 3.50.4 desde Python):

```
ADD COLUMN con CHECK y DEFAULT: OK
fila por defecto: [(1, 'a', 'companero')]
rechaza rol fuera de lista: CHECK constraint failed: rol IN ('companero','gerente','administrador')
```

Y las tres `ALTER` juntas sobre la base de 3 000:

```
migracion 18 sobre 3000 casos: 49.27 ms
casos count intacto: 3000
integrity: ok
columnas totales tras la 18: 107
rechaza motivo fuera de lista: CHECK constraint failed: motivo_no_completa IS NULL OR ...
```

⚠️ **Medido con el SQLite de Python, no con el que empaqueta
`Microsoft.Data.Sqlite`.** Quien construya la migración lo vuelve a medir con el
suyo; lo que aquí queda demostrado es que la forma de la migración es correcta y
que no reconstruye ninguna tabla.

---

## 5. La segunda vuelta, y el agujero que la deja vacía

### 5.1 El agujero

El dueño quiere *«reporte para ellos con los comentarios de los agentes»*.

**Hoy los comentarios de los agentes no existen como dato.** Medido:

- `personas.nota_companero` existe en la base y `RepositorioDePersonas` sabe
  escribirla (`UPDATE personas SET estado_propuesto = $estado, nota_companero = $nota`).
- Pero `grep -rn "nota_companero" csharp/Fichas/Fichas.Paquetes/` da **0
  coincidencias**. La hoja del compañero tiene **16 columnas**
  (`Fichas.Paquetes/Columnas.cs`, `Todas`) y ninguna es un comentario: son
  8 de contexto, los 6 pasos de preparación, «¿Llamó al líder?» y la clave.

**Conclusión, y es la que ordena las fases:** el reporte del gerente **saldría en
blanco**. Antes de construirlo hay que hacer que el comentario del agente viaje
en el Excel de ida y vuelta. Es una fase previa y estaba sin ver.

### 5.2 Añadir columnas a la hoja es seguro, y por qué

El lector de la vuelta **no lee por posición**: busca la fila de títulos por el
título de la columna `clave` (`LectorDeExcel.BuscarLaFilaDeTitulos`) y mapea cada
valor por su título (`FilaPorTitulo`). Así que insertar columnas antes de `clave`
no rompe la reconciliación, **y una hoja vieja de 16 columnas que vuelva después
del cambio sigue casando**: los títulos que no estén, sencillamente no se
encuentran.

⚠️ **Pero rompe el criterio C6-1 tal como está escrito** («16 títulos y anchos»).
Pasa a 18 y hay que corregirlo en el mismo commit, o la fase C6 queda mintiendo.

### 5.3 Qué entra en la segunda vuelta

Un caso entra cuando **las dos cosas**:

1. tuvo asignación y volvió con `estado_recomendacion = no_completa`, y
2. su `motivo_del_companero` es `no_se_pudo_comunicar` o `el_lider_no_lo_hizo`.

Un caso `completa` **no entra nunca**, tenga el comentario que tenga.

### 5.4 ⛔ Dos decisiones del dueño aquí

1. **¿`otra_razon` entra en la segunda vuelta?** Mi recomendación: **sí, pero
   listado aparte**, porque es donde caerá todo lo que el agente no supo
   clasificar, y no verlo es perderlo.
2. **¿Un gerente puede recibir un paquete de primera vuelta?** Mi recomendación:
   **sí, sin prohibirlo**. Prohibirlo obliga a escribir una regla que un día
   estorba, y el rol ya sirve para filtrar la lista sin cerrarla.

---

## 6. «El administrador lo hizo»

### 6.1 No hace falta nada nuevo, y es una buena noticia

`casos.estado_marcado_origen` **ya existe** (migración 14) y ya guarda de dónde
vino la marca. Hoy tiene dos valores en el código:
`"a mano en la pantalla Revisar"` (`AccionesDeRevisar.OrigenAMano`) y la ruta del
Excel que la trajo (`PruebasDeLaVuelta`: *«de dónde salió la marca es el archivo
que la trajo»*).

**Decisión: un tercer valor, con las palabras del dueño, literales:**

```
estado_marcado_origen = "el administrador lo hizo"
```

Sin columna nueva. `MarcarAMano` ya hace el resto.

### 6.2 Que no se mezcla con la firma de Miguel: cómo se comprueba

La regla permanente 5 dice que la firma de campos es de Miguel y nunca
automática. Completar como administrador **no la toca**, y eso se mide, no se
promete:

```sql
-- antes y después de «completar como administrador» sobre un caso:
SELECT count(*) FROM procedencia_campo WHERE verificado = 1;
```

**El número tiene que ser idéntico.** El `CHECK` de la tabla ya impide firmar sin
quién y cuándo; lo que este criterio impide es que alguien *escriba* la firma
desde el atajo.

### 6.3 ⚠️ El defecto que hay que cerrar antes: hoy firmaría como Sandy

`AccionesDeRevisar.QuienFirmaAMano` está documentado en el propio código como
**hueco declarado**: coge el compañero activo llamado «Miguel» y, si no lo hay,
**el primer activo**.

En la base viva del dueño, medido hoy:

```
companeros: [(1, 'Sandy', 1)]
```

**Miguel no está en la lista.** Es decir: si el botón de «completar como
administrador» se construyera hoy tal cual, **la marca quedaría firmada por
Sandy**, y el reporte diría que Sandy completó algo que no tocó.

El dueño ya dio media respuesta el 2026-09-04 (*«él se añade a sí mismo y firma
con su nombre»*). Falta la otra media: **cómo sabe el programa cuál de las filas
soy yo.** Con `companeros.rol` esto se cierra sin preguntar nada más: **el
administrador es el compañero activo con `rol = 'administrador'`**, y si no hay
exactamente uno, **el botón no está y se dice por qué**. Nunca se adivina.

---

## 7. Qué pantallas cambian, y cuáles no

Medido leyendo el código, con el tamaño de cada archivo para que se vea el
volumen real.

### Cambian

| Qué | Archivo | Líneas hoy | Alcance |
|---|---|---|---|
| La celda del calendario pasa de casos a grupos | `Inicio/CalendarioDelMes.cs` | 109 | `AgruparPorDia` se reescribe; `PastillasPorDia = 3` y las 42 celdas **se quedan** |
| `PastillaDeDia` cambia de significado | `Inicio/ModelosDeInicio.cs` | 172 | Un `record` de 5 campos; alrededor no se toca |
| Inicio responde al clic | `Inicio/PaginaDeInicio.xaml` (+ `.cs`) | 627 | Hoy **cero** manejadores: `grep` de `Click`, `Tapped`, `AlPulsar` y `Seleccion` sobre el calendario da **0 coincidencias** (medido por el supervisor) |
| Navegar llevando «qué grupo» | `Cascara/VentanaPrincipal.xaml.cs` | 108 | Hoy `Navigate(tipo, _servicios)` **solo sabe pasar los servicios**. Hace falta un parámetro |
| Filtrar por una fecha exacta | `Contratos/Consultas/Filtros.cs` | 81 | `FiltroDeCasos` tiene `SoloDeHoy` y `VentanaDeDias`; le falta la fecha concreta |
| Dos columnas más en la hoja | `Paquetes/Columnas.cs` | 238 | 16 → 18. Ver §5.2 |
| Un origen más de marca | `Revisar/AccionesDeRevisar.cs` | — | Una constante |
| **Pantalla nueva: el grupo del día** | *(no existe)* | 0 | Es la única pieza que se escribe desde cero |

### No se tocan

`Fichas.Lectura` entera (OCR, anotaciones, casillas de ordenanza), la pantalla de
Importar, el interior de la pantalla de Corrección, `procedencia_campo` y toda la
maquinaria de la firma de campos, `RepositorioDeProcedencia`, el PDF de los
jefes, y **la tabla `personas`: ni una columna**.

### Qué se tira

**Ningún archivo.** Se reescriben un método (`AgruparPorDia`, ~25 líneas) y un
`record` (`PastillaDeDia`, 5 campos). Nada de lo construido se pierde.

### Qué se reordena

La **FASE C7** («Calendario, pendientes y lo que viaja pronto») deja de ser una
pantalla de consulta y pasa a ser la entrada al trabajo. Sus cinco criterios
C7-1 a C7-5 **siguen vigentes tal cual** — incluido C7-5, el pintado en ≤ 0,2 s
con 3 000 documentos, que ahora hay que volver a demostrar con pastillas de grupo
en vez de pastillas de caso.

⚠️ **Y hay un solape que el dueño tiene que ver:** la pantalla del grupo y la
pantalla de **Revisar** hacen casi lo mismo — listar documentos, ver si están
completos, marcar y archivar. **Mi recomendación es que no se duplique la
máquina:** el grupo es Revisar filtrado por fecha y unidad, con la misma
`OperacionDeAsignar` (criterio C5-1: una sola para todo el programa) y las mismas
`AccionesDeRevisar`. Revisar se queda como la vista sin fecha, que es la que
resuelve la petición 5 del mismo día.

---

## 8. El coste, dicho con la palabra correcta

**⚠️ Esto es una estimación, NO una medición.** No he ejecutado nada del C#, y no
podía: hay tres programadores trabajando a la vez en `csharp/Fichas`.

| Fase | Estimación | Depende de |
|---|---|---|
| C10 — migración 18 | medio día | — |
| C11 — el grupo como consulta | 1 día | C10 |
| C12 — el calendario que responde | 1 día | C11 |
| C13 — la pantalla del grupo | 1,5 a 2 días | C11, C12 |
| C14 — el comentario del agente viaja | medio día | C10 |
| C15 — gerentes y segunda vuelta | 1,5 a 2 días | C10, C14 |
| C16 — «el administrador lo hizo» | medio día | C10 |

**Suma de trabajo: 6 a 7,5 días-programador.** Con tres a la vez y estas
dependencias —C10 bloquea a todos; C14 y C16 pueden ir en paralelo con C11 y
C12— **son 3 a 4 días de calendario.** No son horas y no es una semana larga.

**Lo que el dueño puede ver primero, si quiere algo mañana:** C10 + C11 + C12 le
dan el calendario que se pulsa y enseña el grupo. Eso es **día y medio a dos
días** y no depende de nada del resto.

---

## 9. Lo que este ADR corrige de mis propios documentos

**`PENDIENTES.md` DC-17 está superada y hay que tacharla.** Yo decidí el
2026-09-04 que `contactos` *«se queda fuera del programa nuevo»*. **El dueño
decidió lo contrario ese mismo día**, con sus palabras: *«No debe haber teléfono
ni teléfono, solo el canal que el agente se comunicó y ya»* — y el supervisor lo
recogió en `DECISIONES.md` diciéndolo explícitamente: *«Esto cierra la DC-17 del
planificador: entra, como registro de canal, y sin agenda»*.

**Y lo digo porque el pase me la ofrecía como candidata:** `contactos.respondio`
sigue sin servir para los tres estados (§3.2), pero **no por estar fuera del
programa** —no lo está— sino porque su grano es un intento y no un caso.

---

## 10. Qué NO cubre este ADR, y qué no pude verificar

### Fuera de alcance a propósito

- **La petición 5 del dueño** («la pestaña de revisar todos los PDF antes de
  generar el paquete»). Toca la misma pantalla y por eso está nombrada en §7,
  pero es un encargo aparte y no lo especifico aquí.
- **Las cuatro primeras peticiones del 2026-09-05** (firmar lo que él escribe,
  «no está en el papel», borrar, el Excel que no cambia nada visible). Son
  defectos y ya están encargadas.
- **El ROADMAP.** No es mío.
- **Qué canales de contacto se pintan.** Es la DC-17 reabierta por el dueño, y
  merece su propia especificación.

### Preguntas que me hice y nadie me pidió

- *¿Y si dos personas del mismo grupo viajan en fechas distintas?* Hoy no puede
  pasar: la fecha vive en el caso, no en la persona. Si algún día hiciera falta,
  sería una columna en `personas` y **no lo propongo**, porque el dueño no lo ha
  pedido nunca y añadiría una fecha que contradice a otra.
- *¿Qué estado enseña la pastilla de un grupo mixto?* Lo resuelvo en el criterio
  C11-4 con una regla determinista, porque «el más grave» es una opinión y dos
  personas la leerían distinto.
- *¿Puede un caso archivado estar en un grupo?* Sí, y se ve marcado: es la
  decisión del dueño del 2026-09-03 y el criterio C7-2. No la toco.

### Lo que NO pude verificar, y es honesto decirlo

1. **No encontré los siete escaneos reales en el repositorio.** `find . -name
   "*.pdf"` da 7 archivos, 5 de ellos en `pdfs_referencia/` y con **tres
   prefijos distintos** (`CASP2609`, `PARB2609`, `SURB2609`). Los siete
   `CASP2609` con los que el supervisor midió la línea base **no están aquí** —
   y `.gitignore` excluye `*.pdf`, así que nunca lo estarán. Todo criterio de §8
   que los nombra depende de que el supervisor o el dueño los pongan sobre la
   mesa. **Los números que cito sobre ellos son del supervisor, del 2026-09-04,
   y no los he vuelto a medir.**
2. **No ejecuté nada del C#.** Ni `dotnet build`, ni la suite, ni el `.exe`. Es
   lo que el pase me mandó y también lo que me corresponde. Todo lo que digo del
   C# sale de **leer** los archivos que cito.
3. **Los milisegundos son de SQLite 3.50.4 desde Python**, no de
   `Microsoft.Data.Sqlite`. Sirven para descartar la opción B de §2 —tres órdenes
   de magnitud de margen— y **no sirven como umbral de aceptación**: por eso los
   criterios de §8 llevan margen y lo declaran.
4. **No sé cuántos documentos tiene un grupo real del dueño.** Los siete
   escaneos son un grupo de 7. Mi base sintética da un día de 98. **No he medido
   ningún grupo real de más de 7**, y si sus grupos son de 200, la pantalla del
   grupo necesita el mismo tratamiento de tope que ya tiene el calendario.
