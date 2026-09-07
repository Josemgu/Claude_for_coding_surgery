# ADR-0006 — El ticket es la persona, y su estado son las seis preguntas

- **Fecha:** 2026-09-05
- **Estado:** propuesto — **cinco decisiones son del dueño y están marcadas ⛔**
- **Autor:** el planificador
- **Origen:** `DECISIONES.md`, las tres entradas del 2026-09-05 «⚠️ EL DUEÑO
  EXPLICA SU TRABAJO, Y NO ES EL QUE EL PROGRAMA CREE», «El ticket es por
  persona, y el grupo es cómo viajan» y «Las seis preguntas SON las del sistema
  del obispo, confirmado por él».
- **Se apoya en:** ADR-0005 (el grupo es una consulta, no una tabla). **Corrige:**
  una frase del supervisor en la entrada del dueño — ver §1.3.

---

## 0. Cómo se midió lo que hay aquí dentro

Cada cifra de este documento lleva su comando y su salida. Lo que no pude medir
está en §10.

**La base viva del dueño**, copiada a la carpeta de trabajo y abierta en solo
lectura. La original no se tocó.

```
cp "C:/Users/josem/Documents/Fichas/fichas.db" "$SCRATCH/copia.db"
python -c "... pragma table_info(...) ..."
```

Salida, lo que importa, medido hoy 2026-09-05:

```
version 18
casos 2 (22 columnas)   personas 0 (25)   companeros 1 (7)   asignaciones 1 (6)
TOTAL columnas: 108   (22+25+7+6+12+16+8+9+3)
indices: idx_procedencia_registro, idx_asignaciones_companero, idx_asignacion_viva,
         idx_ilegibles_ruta, idx_descartadas_companero, idx_casos_viaje_activos,
         idx_casos_numero, idx_personas_caso
sqlite 3.50.4
```

**Dos cosas que esto ya deja fijadas y conviene decir antes de seguir:**

1. **La migración 18 del ADR-0005 ya está aplicada en la base del dueño**, y
   trajo cuatro columnas, no tres: `casos.motivo_no_completa`,
   `casos.motivo_del_companero`, `companeros.rol` y `companeros.categoria`. La
   siguiente migración libre es **la 19**.
2. **`docs/ARQUITECTURA.md` §2 dice «104 columnas» y la base tiene 108.** El
   documento está desfasado; el código no. Queda anotado como deuda en
   `PENDIENTES.md` — **no lo corrijo en este pase porque no me lo encargaron**.

**La base de 3 000.** Construida con el DDL exacto de la base viva
(`select sql from sqlite_master`), 3 000 casos repartidos en 30 días de viaje y
de 1 a 10 personas cada uno, con los seis pasos sorteados entre sí, no y vacío:

```
casos: 3000 personas: 16500
```

Es SQLite 3.50.4 desde Python, **no `Microsoft.Data.Sqlite`**. Por eso ningún
criterio de aceptación usa mi número pelado: usa mi número con margen, y lo dice.

---

## 1. El problema, en una frase

El programa cree que el trabajo es **leer documentos**, y el trabajo es
**cerrar, persona por persona, la pregunta de si su recomendación está
confirmada en el sistema del obispo, antes de la fecha en que viaja**.

### 1.1 Lo que ya está construido y sirve — es más de lo que parece

Esto lo leí archivo por archivo. **No ejecuté nada del C#** (§10.2).

| Pieza | Dónde | Qué hace ya |
|---|---|---|
| Las seis preguntas, con su regla de tres valores | `Fichas.Reportes/Reglas/Pasos.cs` (63 líneas) y `Fichas.Paquetes/Pasos.cs` | `Estado(persona)` devuelve **sí / no / no se sabe**, y su propio comentario explica por qué el tercero existe |
| Las seis columnas de cada persona | `personas.paso_preparacion` … `paso_listo_para_el_templo`, más `llamo_al_lider` | Medidas hoy en la base viva: están las 7, desde la migración 9 |
| El Excel del agente pregunta las seis | `Fichas.Paquetes/Columnas.cs:151-153` | Las seis con menú de dos respuestas, más la llamada al líder |
| **La vuelta del agente ya calcula el estado desde las seis** | `Fichas.Paquetes/Paquetes.cs:511-525`, `EstadoDelDocumento` | Si alguna persona tiene un «no», el documento vuelve `NoCompleta`; si **todas** tienen los seis en sí, vuelve `Completa`; si no, `SinMarcar` |
| El grupo del día, y su pantalla por PERSONA | `Fichas.App/Grupo/` (1 634 líneas), `PersonaDelGrupo` | El renglón de la pantalla del grupo **ya es la persona**, no el documento |
| La escalera de categorías y el rol | `companeros.rol`, `companeros.categoria`, migración 18 | El peldaño siguiente ya tiene dónde guardarse |

**La petición 9 del dueño —«si Sandy me pasa su paquete con todas las preguntas
llenas… automáticamente debe pasar a listo para viajar»— está construida desde
el 2026-09-04.** Lo que falta de ella es la otra mitad, la que él dijo después:
*«Sandy no pudo verificar todos, por favor verifica qué falta»* (§6).

### 1.2 Lo que falta, y es poco pero es el corazón

| Falta | Medido así |
|---|---|
| **Miguel no puede contestar las seis preguntas dentro del programa** | `grep -rln "PasoPreparacion" csharp/Fichas --include=*.cs --include=*.xaml` da 17 archivos y **ninguno escribe desde una pantalla**: `Fichas.App/Correccion/RespuestaDelCompanero.cs` solo las **lee** para enseñarlas («Lo que el compañero contestó»). El único camino de escritura es el Excel que va y vuelve |
| **El estado que ve la persona en pantalla es el del DOCUMENTO** | `Fichas.App/Grupo/LectorDeGrupos.cs:306` pone `caso.Estado` dentro de cada `PersonaDelGrupo`. Cinco personas del mismo documento salen con el mismo estado, aunque tres estén resueltas y dos no |
| **«Listo» en Inicio mira nuestros campos** | `Fichas.App/Inicio/LectorDelInicio.cs:133` — `renglon.CuantoLeFalta == 0`, que sale de `LoQueLeFalta.cs`: número de caso, unidad, fecha, templo, cédula y nombre. **Ni una de las seis preguntas entra ahí** |
| **Nadie mira las seis para avisar de un viaje encima** | `Pasos.Estado` solo se usa en reportes (`SeccionesDeDireccion.cs`, `Preparacion.cs`) y en el Excel. No en Inicio, ni en el calendario, ni en Revisar |

### 1.3 ⚠️ Una frase del encargo que hay que corregir antes de construir nada

La entrada del supervisor dice: *«"Listo para asignar", tal como se programó hoy,
mira si nuestros campos están llenos. Él dice que eso no es lo que decide. **Hay
que rehacer ese concepto**»*.

**No hay que rehacerlo: hay que dejar de llamar a dos cosas con la misma
palabra.** El mismo día 2026-09-05, y está en `DECISIONES.md` con sus comillas,
el dueño pidió expresamente que existiera:

> *«Si el sistema escanea y verifica todos los campos sin mi intervención, debe
> decir "listo para asignar": el sistema llenó todos los campos y a mí solo me
> debería dejar verificarlo.»*

Y en la misma jornada:

> *«No puede poner los PDF listos para viajar porque no se ha verificado la
> recomendación en el sistema del obispo.»*

**Son dos preguntas distintas sobre el mismo papel, y las dos hacen falta:**

| | Qué pregunta | Quién contesta | De dónde sale |
|---|---|---|---|
| **Listo para asignar** | ¿tenemos los datos suficientes para mandarle este documento a un agente? | **el programa, solo** | `LoQueLeFalta.DeUnDocumento` — ya construido y correcto |
| **Listo para viajar** | ¿está confirmada la recomendación de **esta persona** en el sistema del obispo? | **una persona**: Miguel, un agente, un peldaño superior o el administrador | las seis preguntas de `Pasos` — construido a medias |

**Borrar el primero para poner el segundo le devolvería el trabajo que este
programa le quita:** con 3 000 documentos, firmar campo por campo. Lo que hay
que arreglar es que **la pantalla enseña uno solo y él lee el otro**. Esa es la
frase *«el programa es confuso, muy confuso»* con nombre y apellidos.

---

## 2. Qué es un ticket en el esquema — la decisión es que no es una tabla

### 2.1 Lo que dijo el dueño

> *«El ticket es de cada persona o familia, porque muchas veces viajan en familia
> para hacer ordenanzas que están en el PDF. **Es por persona que se revisa la
> información.**»*

Un ticket es **una persona con una fecha de viaje**. Su estado es la respuesta a
una sola pregunta, y esa pregunta ya tiene sus seis columnas en la base.

### 2.2 Las tres opciones que aplican de verdad

**A · Estado derivado.** El estado del ticket se calcula de
`personas.paso_*` cada vez que se pregunta. Ninguna tabla, ninguna columna de
estado. Es lo que ya hace `Pasos.Estado`.

**B · Tabla `tickets`.** Una fila por persona con su estado guardado, más
cerrado_en, cerrado_por y origen. Es el modelo de un gestor de incidencias
clásico.

**C · Tabla de historial `respuestas_de_pasos`.** Append-only, una fila por
pregunta contestada, con quién y cuándo. El estado se sigue derivando; la tabla
guarda **todas** las respuestas de la historia.

### 2.3 Qué cuesta cada una, medido sobre 3 000 casos y 16 500 personas

Con **los índices que ya existen** (`idx_casos_viaje_activos`,
`idx_personas_caso`), **ninguno nuevo**:

| Consulta | A (derivado) | B (tabla `tickets`) |
|---|---|---|
| Las personas de un día con su estado (635 personas) | **3,17 ms** | — |
| Contar el reparto de estados de un día | **1,10 ms** | — |
| El mes entero, por día y por estado (76 renglones) | **36,99 ms** | **39,71 ms** |

```
-- plan de la consulta del día --
SEARCH c USING INDEX idx_casos_viaje_activos (fecha_viaje=?)
SEARCH p USING INDEX idx_personas_caso (caso_id=?)
```

**La tabla `tickets` no es más rápida: es 2,7 ms más lenta en la consulta del
mes**, porque añade una unión a un camino que ya iba por índice. Y cuesta
espacio: la base pasa de **2 207 744 a 2 621 440 bytes**, un **18,7 % más**,
sin contestar ni una pregunta nueva.

**Y hay un coste que no se mide en milisegundos.** Un estado guardado aparte de
su evidencia se separa de ella en silencio. Medido, con este comando:

```
persona elegida 2394 · tickets dice si
UPDATE personas SET paso_entrevistas = 0 WHERE id = 2394
  los seis pasos dicen : no
  la tabla tickets dice: si
```

**Eso es exactamente el daño que este programa existe para evitar**: una persona
cuya quinta pregunta está en «no» y a la que el sistema llama lista para viajar.
Con la opción A ese estado no puede existir, porque no hay dónde escribirlo.

**El coste de la opción C, medido:** una sola ronda de respuestas sobre 16 500
personas son **99 000 filas**, escritas en **1 752 ms**, y la base pasa de
**2,2 MB a 10,3 MB** — **×4,7 por una ronda**. Leer el historial de una persona
cuesta 0,404 ms, o sea que la tabla funciona; lo que no está claro es que el
dueño quiera pagar ese tamaño por algo que nadie ha pedido todavía.

### 2.4 Decisión

**Opción A: el estado del ticket es derivado y no se guarda nunca.** Es el mismo
razonamiento que el ADR-0005 aplicó al grupo, con el mismo resultado: la
consulta gana. Y aquí gana por un motivo más fuerte que la velocidad — **un
veredicto guardado puede mentir, y uno derivado no puede**.

**Lo que sí hace falta guardar, y no es el estado: quién contestó, cuándo y
desde dónde.** Hoy `personas` tiene `propuesto_por` y `propuesto_en`, pero son
del `estado_propuesto` que escribe el Excel; usarlas también para las seis
preguntas mezclaría dos cosas que pueden venir de personas distintas.

**Migración 19 — tres columnas en `personas`, cero reconstrucciones:**

```sql
ALTER TABLE personas ADD COLUMN pasos_por    INTEGER REFERENCES companeros (id) ON DELETE RESTRICT ON UPDATE RESTRICT;
ALTER TABLE personas ADD COLUMN pasos_en     TEXT;
ALTER TABLE personas ADD COLUMN pasos_origen TEXT;
```

Es **la misma forma exacta** que la migración 14 le dio a `casos`
(`estado_marcado_por` / `_en` / `_origen`), y por el mismo motivo: un nombre
escrito a mano se escribe de dos maneras el mismo día y luego no se puede
agrupar por quién. `pasos_origen` es texto libre sin catálogo, igual que
`estado_marcado_origen`, y ahí caben las tres frases que ya se usan: la ruta del
Excel que trajo las respuestas, «a mano en la pantalla», y «el administrador lo
hizo».

**Que la migración es legal está verificado en la documentación oficial** —
<https://sqlite.org/lang_altertable.html>, consultada el 2026-09-05, misma
comprobación que el ADR-0005 §4.4 hizo para la 18: la lista de lo que
`ADD COLUMN` no admite no incluye ni el `REFERENCES` sin `NOT NULL` ni las
columnas de texto sin `DEFAULT`.

### 2.5 ⛔ Decisión del dueño 1 — ¿guardar el historial de respuestas?

Si mañana Sandy contesta que la persona está lista y pasado mañana Miguel
contesta que no, **con la opción A la respuesta de Sandy se pierde**: quedan las
seis columnas con lo último, y `pasos_por` diciendo que fue Miguel.

El dueño ya fijó una regla parecida el 2026-09-03 para el **estado**: *«lo que
Miguel marque a mano después manda… **sin borrar lo que dijo el compañero**»*, y
por eso `casos` tiene dos juegos de tres columnas.

**Mi recomendación: NO copiar aquí ese doble juego, y decir por qué.** Las seis
preguntas no son un veredicto de nadie: son **lo que pone en la pantalla del
obispo**. Si Sandy y Miguel discrepan, casi siempre es porque el obispo hizo algo
entre las dos consultas — y entonces la respuesta buena es la última, no las dos.
El estado sí es opinión y por eso se guardan las dos; esto es un dato del mundo.

**El precio de equivocarme está medido y es reversible:** la opción C cuesta
99 000 filas y ×4,7 de tamaño por ronda, y se puede añadir después sin tocar
nada de lo que este ADR propone, porque el estado seguiría derivándose igual.
**Es la P-15 de `docs/ARQUITECTURA.md` §7, que sigue abierta y es suya.**

### 2.6 ⛔ Decisión del dueño 2 — ¿las seis, o basta la sexta?

Ya está anotada en `DECISIONES.md` y no la reabro: se programa con **las seis**,
y él lo corrige al verlo. Lo dejo aquí porque es un criterio de aceptación
(FASE C18) y tiene que estar donde se pueda cambiar de un sitio: la regla vive en
`Pasos.Estado` y en ningún otro lugar.

---

## 3. Qué significa «listo para viajar» y quién lo dice

### 3.1 La regla, escrita para que dos personas la lean igual

Para **una persona**, con las seis respuestas delante:

| Si… | Entonces el ticket está… |
|---|---|
| alguna de las seis dice **no** | **no listo** — y se dice **en cuál se quedó** |
| las seis dicen **sí** | **listo para viajar** |
| ninguna dice no, pero alguna está **en blanco** | **sin mirar** — que no es lo mismo que «no» |

Es literalmente lo que ya hace `Pasos.Estado`, cuyo propio comentario lo
justifica: *«dar por lista a una persona de la que faltan preguntas por mirar es
exactamente lo que manda a alguien al templo con la recomendación mal»*.

Para **un grupo de un día**, y esto es nuevo: el grupo **no tiene estado propio**.
Dice **cuántas personas de cuántas** están listas, cuántas no y cuántas sin
mirar. Es lo que pidió el dueño con sus palabras: *«faltan 3, 4 o 5 personas que
la recomendación no está confirmada»*. Un grupo no se «completa»: se vacía de
personas sin confirmar.

**Para un documento se conserva la regla que ya está construida** en
`Paquetes.cs:511-525` — un «no» de cualquier persona lo pone `NoCompleta`, los
seis de todas lo ponen `Completa` — porque `casos.estado_recomendacion` sigue
siendo lo que el Excel del compañero escribe con su nombre, que es la regla
permanente 5 tal como el dueño la precisó el 2026-09-03. **No se toca.**

### 3.2 Quién puede contestar las seis

Cuatro orígenes, y los cuatro escriben en el mismo sitio con `pasos_origen`
distinto:

| Quién | Por dónde | Estado hoy |
|---|---|---|
| **Miguel** | la pantalla de la persona, dentro del programa | **no existe** — FASE C18 |
| **Un agente** | el Excel que va y vuelve | **construido** (`Fichas.Paquetes`) |
| **Un peldaño superior** (gerente / categoría 2, 3…) | el mismo Excel, otro filtro | construido a medias (ADR-0005 §5, FASE C15) |
| **El administrador**, sin pasar por la verificación | la acción «el administrador lo hizo» | especificado en la FASE C16, sin construir |

**Ninguno de los cuatro toca `procedencia_campo.verificado`.** La regla
permanente 5 sigue entera: la firma de campos es de Miguel y nunca automática;
esto es otra cosa, y son las palabras del propio dueño las que las separan.

### 3.3 ⛔ Decisión del dueño 3 — ¿puede un agente contestar «sí» sin que Miguel lo mire?

**Mi recomendación: sí, y sin pedirle confirmación**, porque él ya lo dijo con
esas palabras — *«automáticamente debe pasar a "listo para viajar" en base a la
respuesta de Sandy»*—, y porque lo contrario le devuelve las 3 000 pulsaciones.
**Lo que sí propongo, y no es lo mismo:** que la pantalla diga siempre **quién**
lo contestó y **cuándo**, que es para lo que sirve la migración 19. Confiar sin
saber de quién te fías es otra cosa.

---

## 4. Cómo se cierra un ticket, y qué pasa si no se cierra a tiempo

### 4.1 Cerrar

**Un ticket se cierra cuando sus seis preguntas dicen que sí.** No hay un botón
de cerrar aparte, y es deliberado: un botón de cerrar sería un segundo estado que
puede contradecir a las seis respuestas — el mismo fallo que la tabla `tickets`
de §2.3, medido allí.

Lo que sí hace falta y no existe: **que se vea cuándo se cerró y por quién.** Sale
de `pasos_en` y `pasos_por` sin ninguna columna más.

### 4.2 No cerrar a tiempo — esto es el daño que el programa existe para evitar

Hoy el programa **no sabe decirlo**. `personas.pudo_viajar` y
`personas.motivo_no_viajo` existen desde la migración 6 y son otra cosa: son para
después, para el reporte de quién no pudo viajar.

**La regla que propongo, y es una consulta, no una columna:**

> Un ticket está **vencido** si su fecha de viaje ya pasó y su estado no era
> «listo para viajar» el día del viaje.

Y **un ticket vencido no se archiva solo, no se cierra solo y no desaparece.**
Ni siquiera cuando el documento se archiva: el dueño ya decidió el 2026-09-05 que
lo archivado sale de la vista y **sigue en el histórico y en los reportes**.

**Lo que NO propongo, y lo digo por si alguien lo espera:** que el programa
avise por su cuenta, mande correo o suene. No hay servicio de red en este
proyecto (regla permanente 2) y él no lo ha pedido. El aviso es una lista que se
ve al abrir.

### 4.3 ⛔ Decisión del dueño 4 — ¿cuántos días antes empieza a apremiar?

El calendario ya sombrea una ventana de **7 días**
(`LectorDelInicio.DiasDeLaVentana = 7`, y el comentario dice que los pidió él en
el criterio C1-4). **Recomiendo usar esa misma ventana** para lo que apremia y no
inventar una segunda: dos números distintos para «pronto» en la misma pantalla es
otra vez el programa diciendo dos cosas. Si él quiere 14 para los tickets, es un
número en un sitio.

---

## 5. Qué pasa con lo que ya está construido

### 5.1 Se tira

**Nada.** Ni un archivo, ni una columna, ni una migración.

Lo digo con esta seguridad porque lo que parecía sobrar —las seis columnas de
pasos, el Excel que las pregunta, la pantalla del grupo por persona— es
justamente lo que el modelo nuevo necesita. **El programa estaba construyendo la
pieza correcta y enseñándola en el sitio equivocado.**

### 5.2 Se reordena, y cuánto de eso es la consulta y cuánto la pantalla

Medido leyendo los archivos, con sus líneas. **La cuenta de líneas es de `wc -l`;
cuáles hay que tocar es mi lectura, no una medición.**

| Pantalla | Qué cuenta hoy | Qué hay que cambiar | ¿Consulta o pantalla? |
|---|---|---|---|
| **Grupo** (`Fichas.App/Grupo/`, 1 634 líneas) | el renglón **ya es la persona**; su estado viene del caso (`LectorDeGrupos.cs:306`) | que `PersonaDelGrupo.Estado` salga de las seis de esa persona | **Consulta.** El `record` gana un campo y el lector cambia donde hoy pone `caso.Estado`. La pantalla no se rehace |
| **Inicio** (`Fichas.App/Inicio/`, 1 582 líneas) | documentos listos y asignados; **ya suma personas** (`ContadoresDeInicio.PersonasListas`) | una cifra más: personas del próximo grupo **sin confirmar** | **Consulta**, más un recuadro. `LectorDelInicio` ya lee las personas de cada caso (`AgruparLasPersonas`) |
| **Calendario** (`CalendarioDelMes.cs`, 88 líneas) | pastillas de grupo por día (ADR-0005) | que la pastilla diga «N de M personas confirmadas» | **Consulta.** Las 42 celdas fijas y las 3 pastillas por día no se tocan |
| **Revisar** (`Fichas.App/Revisar/`, 2 017 líneas) | tarjeta por documento; ya pide `ContarPersonasDe` (`TableroDeRevisar.cs:88`) | la agrupación por fecha y unidad, ya especificada en el bloque C10–C16 | **Pantalla**, y ya estaba encargada. No la reabro |
| **Corrección** (`RespuestaDelCompanero.cs`) | **enseña** las seis, no deja contestarlas | una ventana por persona con las seis, contestables | **Pantalla nueva.** Es la única pieza que se escribe desde cero |

**El reparto, en una frase:** de las cinco pantallas, **cuatro se arreglan
cambiando de dónde sale un dato**, y **una hay que escribirla**. Es la respuesta
al punto 4 del encargo, y es una buena noticia que conviene decirle al dueño: la
sensación de «hay que rehacerlo todo» no se sostiene contra los archivos.

### 5.3 No se toca

`Fichas.Lectura` entera · Importar · `procedencia_campo` y la firma de campos ·
el PDF de los jefes · `LoQueLeFalta` (§1.3) · las seis columnas `ord_*` de
`personas` · las migraciones 2 a 18.

---

## 6. La vuelta del agente: lo que falta de la petición 9

Su petición tiene dos mitades y solo una está construida:

> *«Si Sandy me pasa su paquete con todas las preguntas llenas… automáticamente
> debe pasar a "listo para viajar". **Si no lo está, la respuesta de Sandy debe
> decirme: Sandy no pudo verificar todos, por favor verifica qué falta.**»*

**La primera mitad está construida** (`Paquetes.cs:511-525`, §1.1).

**La segunda no.** Lo que hay hoy es un aviso suelto para un caso raro —un
documento que vuelve completo y con un motivo escrito a la vez
(`Paquetes.cs:495-508`)—, y no un resumen de la ronda. Lo que hace falta, y no
necesita ni una columna:

- cuántas personas trajo el paquete,
- cuántas volvieron **listas**, cuántas **no listas** y cuántas **sin mirar**,
- **y de las que no, en qué paso se quedó cada una** — que sale de
  `Pasos.SinCompletar(persona)`, ya construido, y es lo que él le dice al obispo
  por teléfono.

**Ninguna de las tres cifras se puede calcular sobre el documento**: una familia
de cinco donde falla uno se cuenta como «un documento no completo» y él necesita
saber que **es una persona de cinco**. Es el modelo nuevo aplicado al informe.

---

## 7. El número de caso repetido: es una pista, no una identidad

El dueño dijo: *«Documentos que tienen el número de caso iguales puede significar
que viajarán en el mismo grupo. Es importante saber esto»*.

**Esto NO reabre la decisión del 2026-09-03**, que quitó el `UNIQUE` de
`numero_caso` porque el número son cuatro letras más `AAMM` e identifica **una
unidad y un mes**, no una familia — y que se tomó porque en la máquina del dueño
**6 de 10 documentos se rechazaron como «caso ya existente» siendo otras
familias**. Esa medición sigue en pie y la identidad sigue siendo el documento.

**La lectura correcta de las dos cosas a la vez:** el número no dice *quiénes
son*, pero sí dice *de qué unidad y de qué mes vienen*, y por eso es una **pista
de grupo** — que es exactamente la palabra que usó él, «puede significar».

**Cómo se instrumenta, y cuesta 0,69 ms:** dentro del grupo de un día, se
agrupan los documentos por número de caso y se enseña el que se repite, con
cuántos documentos y cuántas personas.

```
pista de grupo por numero de caso: 0.69 ms, 1 numeros repetidos
[('CASP2609', 109, 635)]
plan: SEARCH c USING INDEX idx_casos_viaje_activos (fecha_viaje=?)
      SEARCH p USING COVERING INDEX idx_personas_caso (caso_id=?)
```

**Y una advertencia que hay que pintar con la pista, no esconder:** en los siete
escaneos reales del dueño **uno de los siete lleva `CASD2609` escrito en su
propia anotación**, una D por una P, y **no lo leyó mal ningún motor** — lo tecleó
así quien llenó el formulario (medido por el supervisor el 2026-09-04). Si la
pista se pinta sin decir que se apoya en un dato escrito a mano, el día que falle
parecerá un fallo del programa. **Una pista que no se puede desobedecer no es una
pista: es una identidad con otro nombre**, y esa ya se descartó con datos.

⛔ **Decisión del dueño 5:** si además de enseñar la pista quiere **poder unir dos
documentos** en un mismo caso —lo que arreglaría el `CASD2609`—, eso es otra
fase y no está aquí. Sigue pedida desde el 2026-09-04 y sin hacer.

---

## 8. Las once peticiones, ordenadas

| # | Petición, en corto | Qué es | Dónde va |
|---|---|---|---|
| 1 | No puede decir «listo para viajar» sin verificar la recomendación | **modelo** | FASE C17 (las palabras) + C18 (el estado) |
| 2 | En Corrección, solo lo que falta | **defecto suelto** | fuera de este ADR — encargo aparte |
| 3 | Abrir el documento desde Revisar y ver su información y comentarios | **modelo** | FASE C19, es la misma ventana que la 4 |
| 4 | Entrar en un caso y **completar las preguntas** | **modelo — la pieza que falta** | FASE C19 |
| 5 | Muchos no tenían unidad y sí la tenían | defecto — **ya encargado a un programador** | no lo planifico |
| 6 | Revisar agrupado por fecha, y una sección para la fecha ilegible | **modelo**, ya especificado | bloque C10–C16; falta solo la sección de fecha ilegible |
| 7 | El Home dice cuántas **personas** del grupo del 17 faltan | **modelo** | FASE C20 |
| 8 | Nombrar las carpetas y cargar un grupo de carpetas | defecto/función — **ya encargado** | no lo planifico |
| 9 | El paquete de Sandy cierra solo, y dice qué falta | **modelo**, media construida | FASE C22 |
| 10 | Si falta algo, pasarlo a un gerente con lo que falta señalado | **modelo**, ya especificado | FASE C15 del bloque C10–C16 |
| 11 | Números de caso iguales = pista de grupo | **modelo** | FASE C23 |

**Cuatro de las once no las planifico** y digo por qué: la 5 y la 8 están
encargadas mientras escribo esto; la 2 es un defecto de una pantalla que no toca
el modelo; la 10 ya tiene fase escrita.

---

## 9. Lo que este ADR corrige de mis propios documentos

1. **ADR-0005 §2** dijo que la unidad de trabajo es **el grupo que viaja un día**.
   **Es media verdad, y la mitad que falta importa:** el grupo es cómo se
   **reparte** y cómo se **mira**; la unidad que se **resuelve** es la persona. Las
   dos conviven sin contradecirse — el grupo sigue siendo una consulta y ahora
   cuenta personas en vez de documentos.
2. **PENDIENTES.md, FASE C13-4** dice que cada renglón del grupo dice «su estado
   y su motivo» con las palabras del dueño. **Ese criterio se queda corto**: el
   renglón es una persona y el estado que se le pinta es el de su documento. Se
   corrige en la FASE C18.
3. **`docs/ARQUITECTURA.md` §2 dice 104 columnas y la base viva tiene 108**
   (§0). No lo corrijo aquí porque este pase no me lo encarga; queda anotado como
   deuda.

---

## 10. Qué NO cubre este ADR, y qué no pude verificar

### 10.1 Fuera de alcance a propósito

- **El ROADMAP.** Es del dueño.
- **Unir dos documentos en un caso** (el `CASD2609`). Pedido desde el
  2026-09-04, sigue sin fase. Lo nombro en §7 y **no lo especifico**.
- **Las peticiones 2, 5 y 8.** §8.
- **El informe por agente, por mes y por semana**, que él pidió el 2026-09-05.
  Encaja con este modelo —se cuenta por personas, no por documentos— y **no lo
  he especificado**: es un encargo aparte.
- **El tema claro u oscuro.** Nada que ver con el modelo.
- **`contactos`.** Sigue sin especificación, como dejé dicho en el ADR-0005, y
  la pregunta que lo decide —*¿usa Miguel hoy esta función?*— **sigue sin
  hacerse**.

### 10.2 Lo que no pude verificar

1. **No ejecuté nada del C#** — ni compilar, ni la suite, ni el `.exe`. Hay dos
   programadores trabajando en `csharp/Fichas` y además **no me corresponde: eso
   lo dictamina QA.** Todo lo que digo del C# sale de **leer** los archivos que
   cito con su ruta y su línea.
2. **Los milisegundos son de SQLite 3.50.4 desde Python**, no de
   `Microsoft.Data.Sqlite`. Sirven para descartar la tabla `tickets` y para saber
   que no hace falta ningún índice nuevo; **no como umbral**. Por eso cada
   criterio de aceptación lleva margen declarado.
3. **Los siete escaneos reales no están en el repositorio** (`.gitignore` excluye
   `*.pdf`). Las cifras que cito de ellos son del supervisor, del 2026-09-04, y
   **no las he vuelto a medir**.
4. ⚠️ **Los siete escaneos traen UNA persona cada uno** — «Personas: 7 de 7»,
   misma medición. **O sea que en los datos reales de hoy, contar por documentos
   y contar por personas da el mismo número, y la diferencia no se ve.** Todo
   criterio de aceptación de estas fases que quiera demostrar el cambio de unidad
   **necesita además un caso con varias personas**, y eso hay que fabricarlo o
   pedírselo al dueño. Es la comprobación más importante de este documento y es
   la que no puedo cerrar yo.
5. **No sé cuántas personas tiene una familia real del dueño.** El esquema admite
   12 en 6 hojas (`ARQUITECTURA.md`, migración 4). Mi base sintética llega a 10.
   Si las suyas son de 20, ningún criterio de aquí lo descubre.

### 10.3 Preguntas que me hice y nadie me pidió

- **¿La fecha de viaje debería vivir en la persona y no en el caso?** Si el
  ticket es la persona, parece lo suyo. **Decido no proponerlo**, por lo mismo
  que en el ADR-0005: nadie lo ha pedido, y una segunda fecha puede contradecir a
  la primera. Pero **ahora hay un motivo nuevo para volver a mirarlo**: si una
  familia de cinco viaja partida en dos fechas, hoy no se puede guardar. **No sé
  si eso pasa. Es una pregunta para el dueño, y no se la ha hecho nadie.**
- **¿Un ticket puede existir sin documento?** Una persona que hay que verificar y
  cuyo PDF no llegó. Hoy es imposible: `personas.caso_id` es obligatorio. No lo
  cambio, pero si él trabaja alguna vez desde una lista sin papel, esto es lo
  primero que se rompe.
- **¿Qué pasa cuando el obispo arregla algo después de que el agente dijo que
  no?** Con la opción A, la respuesta nueva pisa a la vieja y se ve quién y
  cuándo. Con historial se vería la secuencia entera. Es la decisión ⛔ 1.
