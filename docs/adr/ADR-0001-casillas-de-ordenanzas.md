# ADR-0001 — Las seis casillas de ordenanzas: seis columnas, no tabla hija

- **Fecha:** 2026-09-02
- **Estado:** aceptada
- **Decide:** planificador, sobre encargo del dueño
- **Afecta a:** `personas` en `docs/ARQUITECTURA.md` §2.3; consultas de las FASES 5 y 8
- **Inmutable** (`CLAUDE.md` §7). Si esta decisión se revierte, se escribe un ADR
  nuevo que la sustituya; este no se edita.

---

## El problema

El formulario de recomendación trae, por cada persona, **seis casillas de
ordenanzas**. `DECISIONES.md` las enumera en orden literal:

> «Las seis columnas, en orden: recibir ordenanzas propias, observar ordenanza de
> sellamiento, traductor, investidura, sellamiento esposa a esposo, sellamiento hijo
> a padres.»

Hay dos formas razonables de guardarlas, y `PENDIENTES.md` (Bloqueo 1) ya señaló que
es **la única decisión del esquema con alternativas de verdad comparables**, porque
«esa elección se paga en todas las consultas de las fases 5 y 8».

**A.** Seis columnas en `personas`.
**B.** Una tabla hija `ordenanzas_persona (persona_id, ordenanza, marcada)`.

## Lo que hace falta saber antes de elegir

Tres exigencias del proyecto, cada una con su fuente. Son las que deciden.

**1. Una casilla tiene tres estados, no dos.** FASE 2 crit. 11b: con menos de tres
formularios reales para calibrar el umbral, *«las seis columnas de ordenanzas
vuelven vacías y marcadas para captura manual»*. Hace falta distinguir **marcada**,
**no marcada** y **no leída**.

**2. El conjunto es fijo y no volátil.** Las seis van impresas en el formulario. No
las define un usuario, no crecen solas, no cambian por caso.

**3. La procedencia se indexa por nombre de campo.** `procedencia_campo` guarda una
fila por `(tabla, registro_id, campo)` (`ARQUITECTURA.md` §2.7), porque cada valor
extraído necesita su origen, su confianza y lo que leyó el OCR antes de corregirlo.

## Lo que dicen las fuentes

**SQLite, documentación oficial** (<https://www.sqlite.org/datatype3.html>,
consultada 2026-09-02), literal:

> «SQLite does not have a separate Boolean storage class. Instead, Boolean values are
> stored as integers 0 (false) and 1 (true).»

Es decir: seis columnas son seis `INTEGER` con `CHECK`, y el tercer estado sale
gratis usando `NULL`.

**Coste de añadir una séptima columna después.** Misma casa
(<https://www.sqlite.org/lang_altertable.html>, consultada 2026-09-02), literal:

> «No changes are made to table content for renames or column addition without
> constraints. Because of this, the execution time of such ALTER TABLE commands is
> independent of the amount of data in the table and such commands will run as
> quickly on a table with 10 million rows as on a table with 1 row.»

Y el matiz que sí cuesta, porque las columnas de este esquema **sí llevan `CHECK`**:

> «When adding new columns that have CHECK constraints, or adding generated columns
> with NOT NULL constraints, or when deleting columns, then all existing data in the
> table must be either read (to test new constraints against existing rows) or
> written… In those cases, the ALTER TABLE command takes time that is proportional to
> the amount of content in the table being altered.»

**Sobre cuándo una tabla de atributos genérica está justificada.** El patrón
entidad-atributo-valor está documentado como antipatrón salvo un caso concreto
(<https://cedanet.com.au/antipatterns/eav.php>, consultada 2026-09-02), literal:

> «It is arguable there are valid uses of the pattern. The example sited in Wikipedia
> is for clinical data, where patient records would otherwise need thousands of
> columns in order to support all the possible attributes on a patient.»

> «However, attribute volatility isn't usually present for most database designs, so
> the EAV pattern doesn't offer any particular benefits.»

El discriminador que da esa fuente es la **volatilidad del atributo**. Aquí es cero:
seis ordenanzas impresas en un formulario preexistente.

⚠️ **Lo que NO encontré, y lo digo en vez de rellenarlo:** no localicé un proyecto de
código abierto grande y auditado que resuelva esta misma micro-decisión de forma
citable. Mi ficha me manda comparar en tres fuentes —documentación oficial, código
abierto real, proyectos masivos— y **solo la primera dio material citable con URL,
fecha y valor literal**. La tercera pieza de esta decisión no viene de fuera: viene
de la exigencia interna nº 1, que está medida en `PENDIENTES.md` y es más fuerte que
cualquier precedente ajeno.

## Las dos opciones, comparadas

| | **A. Seis columnas** | **B. Tabla hija** |
|---|---|---|
| Tres estados | `1` / `0` / `NULL`. Nativo | Ambiguo: **«no hay fila» significa a la vez «no marcada» y «no leída»**. Para desambiguar hay que insertar siempre 6 filas por persona con un `marcada` que acepte `NULL` — es decir, reconstruir las seis columnas dentro de una tabla, con más peso |
| Consulta de FASE 5 / 8 | `WHERE ord_investidura = 1` | `JOIN` + filtro por nombre de ordenanza; contar cuántas personas tienen dos ordenanzas concretas exige `GROUP BY … HAVING` |
| Errores de escritura | `ord_investidra` → **error de SQL**, salta al momento | `'investidra'` como valor → **consulta válida que devuelve vacío**, en silencio |
| Procedencia (§2.7) | Seis valores de `campo`, gratis | Hay que inventar una clave de campo por casilla, o una segunda tabla de procedencia para las filas hijas |
| Escribir una persona | Un `INSERT` | Un `INSERT` + hasta seis más |
| Las seis y su orden | **Están en el esquema**; `.schema personas` las enumera | Están en los datos; hay que consultarlos para saber cuáles existen |
| Añadir una séptima ordenanza | `ALTER TABLE ADD COLUMN` + subir `version_esquema` + tocar el escritor de Excel (FASE 4) y los reportes (FASE 8). Con `CHECK`, el `ALTER` recorre la tabla | Insertar filas. **Ningún cambio de esquema** |
| Ordenanzas por persona variables | No las soporta sin `ALTER` | Las soporta de serie |

## Decisión

**Seis columnas en `personas`**, con los nombres y el orden de `DECISIONES.md`:

```
ord_recibir_propias          INTEGER  NULL  CHECK (… IS NULL OR … IN (0,1))
ord_observar_sellamiento     INTEGER  NULL  CHECK (…)
ord_traductor                INTEGER  NULL  CHECK (…)
ord_investidura              INTEGER  NULL  CHECK (…)
ord_sellamiento_esposos      INTEGER  NULL  CHECK (…)
ord_sellamiento_hijo_padres  INTEGER  NULL  CHECK (…)
```

**Los tres motivos, por peso:**

1. **La tabla hija no sabe decir «no leída».** Es la exigencia nº 1, y es la que
   cierra la decisión. La FASE 2 puede entregarse con la lectura de casillas
   **desactivada** —si no hay tres formularios para calibrar—, y en ese estado las
   seis vuelven vacías. Con una tabla hija, «vacía» y «no marcada» son el mismo
   hecho, y Miguel no distinguiría lo que el sistema no leyó de lo que leyó como
   negativo. Eso es exactamente el daño que las reglas permanentes 1 y 5 existen
   para evitar. La única salida sería insertar siempre seis filas con `marcada`
   nulable: la tabla hija dejando de ser una tabla hija.
2. **El error se hace ruidoso.** Un nombre de columna mal escrito revienta la
   consulta; un nombre de ordenanza mal escrito como **dato** devuelve cero filas y
   parece una respuesta legítima. En un programa cuyo daño es «nadie lo vio a
   tiempo», un fallo silencioso pesa más que unas líneas de SQL.
3. **La procedencia encaja sin fabricar nada.** `procedencia_campo` se indexa por
   nombre de campo. Seis columnas son seis nombres de campo. Con tabla hija habría
   que inventar un identificador de campo por casilla, o una segunda tabla de
   procedencia solo para ellas.

La exigencia nº 2 —conjunto fijo y no volátil— es la que quita fuerza al único
argumento serio de la tabla hija: la extensibilidad no vale nada cuando no hay nada
que extender.

## El coste, dicho entero

- **Una séptima ordenanza cuesta una migración.** `ALTER TABLE ADD COLUMN`, subir
  `version_esquema`, y tocar el escritor de Excel (FASE 4) y los reportes (FASE 8).
  Con `CHECK`, el `ALTER` recorre la tabla —proporcional al número de filas, según
  la cita de arriba—, aunque a la escala de este programa (formularios de una
  unidad, no millones de filas) eso es despreciable. **No lo he medido**: no hay
  base. La cifra la dará la FASE 1 el día que exista.
- **`personas` se ensancha en seis columnas.** Sin efecto práctico: SQLite admite
  muy por encima de eso, y en un esquema de siete tablas no cambia nada de leer.
- **Si alguna vez las ordenanzas fueran distintas por persona o por unidad**, esta
  decisión es la equivocada y hay que revertirla con un ADR nuevo. **No hay indicio
  de que sea así** — pero tampoco lo he podido comprobar: el PDF de referencia no
  está en el disco (Bloqueo 2 de `PENDIENTES.md`), así que las seis ordenanzas y su
  orden son lo que dice `DECISIONES.md`, marcado allí como hipótesis del plan **sin
  comprobar**. Si el formulario real trae otra cosa, manda el formulario
  (`CLAUDE.md` §8).

## Qué NO cubre este ADR

- **No decide el umbral de píxel oscuro** que separa una casilla marcada de una
  vacía. Eso se calibra sobre formularios reales en la FASE 2 crit. 11, y hoy no hay
  formularios.
- **No decide cómo se muestran las seis casillas** en la pantalla de corrección
  (FASE 3). Es diseño de interfaz.
- **No se ha ejecutado nada.** Ni un `CREATE TABLE`, ni un `ALTER TABLE`, ni una
  consulta comparada entre las dos opciones. La comparación es de diseño y de
  documentación oficial citada; **no hay un número medido en esta máquina** que
  respalde la ventaja de rendimiento de A sobre B, y no lo presento como si lo
  hubiera.
