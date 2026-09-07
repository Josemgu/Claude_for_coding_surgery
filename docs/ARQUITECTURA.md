# Arquitectura

El mapa del sistema Fichas. Documento del planificador. Nada se borra: lo que deja
de ser cierto se tacha en el sitio con su motivo y su fecha.

Escrito el 2026-09-02.
**Puesto al día el 2026-09-02** contra el esquema que produce el código. Ver la
sección siguiente.
**Puesto al día otra vez el 2026-09-03**, contra `aa90ad1`: el esquema pasó de la
versión 6 a la **11**. Ver «Puesta al día del 2026-09-03», dos secciones más abajo.
**Y otra vez el 2026-09-04**, contra `c0a268c`: de la **11** a la **14**. Ver
«Puesta al día del 2026-09-04».
**Y una segunda vez el mismo 2026-09-04**, contra `02832f4`: de la **14** a la
**15**. Ver «Puesta al día del 2026-09-04 (segunda) — la versión 15».
⚠️ Las cifras de las puestas al día del 2026-09-02 —siete tablas, 71 columnas,
cinco índices— ~~y del 2026-09-03 —nueve tablas, 96 columnas, siete índices—~~
**son la foto de su día y ya no son las de hoy**. ~~Las de hoy, medidas el
2026-09-04: nueve tablas, 104 columnas, ocho índices propios, versión 14.~~
**Las de hoy, medidas el 2026-09-04 sobre `02832f4`: nueve tablas, 104 columnas,
ocho índices propios, 48 `CHECK`, versión 15.** La 15 **no añade ni quita ninguna
columna** —reconstruye `personas` para cambiar un `CHECK`—, así que lo único que
se movió del renglón anterior es el número de versión.

---

## Puesta al día del 2026-09-02 — el código se adelantó y este documento se quedó

Este documento se escribió cuando no había ni una línea de Python. Después se
codificaron las diez fases y el esquema llegó a la **versión 6**. Lo que sigue es
la lista de todo lo que quedó divergente, **medido**, no recordado. Cada punto está
además tachado y corregido en su sitio, más abajo.

**Cómo se midió.** Se construyó una base en memoria con el propio código del
proyecto —`datos.conexion.abrir_conexion` + `datos.esquema.aplicar_esquema`— y se
leyó el esquema resultante con `PRAGMA table_info`:

```
$ .venv/Scripts/python.exe -c "<abrir_conexion(':memory:') + aplicar_esquema + PRAGMA table_info>"
VERSION_ACTUAL 6
N_MIGRACIONES 5
sqlite_version 3.50.4
asignaciones       6  id,caso_id,companero_id,asignado_en,activa,desactivada_en
casos             12  id,numero_caso,unidad_numero,fecha_viaje,captura_manual,archivado,
                      fecha_archivado,ruta_pdf,creado_en,estado_recomendacion,pagina_pdf,unidad_nombre
companeros         5  id,nombre,activo,desactivado_en,creado_en
contactos         12  id,caso_id,fecha,medio,con_quien,resultado,anulado,motivo_anulacion,
                      anulado_en,registrado_en,contactado_por,respondio
personas          18  id,caso_id,mrn,nombre,fila_formulario,ord_recibir_propias,
                      ord_observar_sellamiento,ord_traductor,ord_investidura,
                      ord_sellamiento_esposos,ord_sellamiento_hijo_padres,pagina_pdf,
                      estado_propuesto,nota_companero,propuesto_por,propuesto_en,
                      motivo_no_viajo,pudo_viajar
procedencia_campo 15  id,tabla,registro_id,campo,origen,confianza,valor_ocr,verificado,
                      verificado_por,verificado_en,banda_x0,banda_y0,banda_x1,banda_y1,
                      anulado_por_tachon
version_esquema    3  version,aplicada_en,descripcion
TOTAL_COLUMNAS 71
INDICE idx_asignacion_viva · idx_asignaciones_companero · idx_casos_viaje_activos
INDICE idx_personas_caso · idx_procedencia_registro
```

**Siete tablas, 71 columnas, 5 índices propios.** `CLAUDE.md` §8 manda: gana el
código.

⚠️ **Una premisa del pase que no se sostiene, dicha antes de construir encima.** El
pase decía «la base va por la versión 6 y hay **seis** migraciones aplicadas».
Medido: `len(datos.migraciones.MIGRACIONES)` = **5**. Son las versiones 2 a 6 sobre
un DDL inicial que es la versión 1. Llegar a la versión 6 cuesta cinco migraciones,
no seis. No cambia ninguna decisión, pero se corrige porque el número se iba a
copiar.

| # | Qué decía este documento | Qué dice el código | Dónde se corrigió |
|---|---|---|---|
| 1 | `unidad_numero`: **6 dígitos** | **6 o 7 dígitos** (migración 2; 4 de 9 páginas reales traen 7) | §2.2 |
| 2 | `casos` no tenía `pagina_pdf` ni `unidad_nombre` | Las tiene (migración 3) | §2.2 |
| 3 | `estado_recomendacion`: ⛔ «PENDIENTE, no se define aquí» | Es columna `TEXT` real, **sin `CHECK`** (`DECISIONES.md` P-1) | §2.2 |
| 4 | La regla del mes cruzado figuraba como `CHECK` del esquema | **No existe en el motor**: es aviso de `datos/validacion.py` (`DECISIONES.md` P-2) | §2.2 |
| 5 | `personas` tenía 11 columnas | Tiene **18**: `pagina_pdf` (v4), las cuatro de la propuesta (v5), `pudo_viajar` y `motivo_no_viajo` (v6) | §2.3 |
| 6 | `contactos` tenía 10 columnas | Tiene **12**: `contactado_por` y `respondio` (v5) | §2.6 |
| 7 | `procedencia_campo` tenía 10 columnas | Tiene **15**: las cuatro de la banda y `anulado_por_tachon` (v3) | §2.7 |
| 8 | P-1, P-2, P-3, P-4 y P-6 figuraban abiertas | Las cinco están resueltas en `DECISIONES.md` (2026-09-02) | §7 |
| 9 | «No he verificado el formulario» (el PDF no estaba) | Hay **4 documentos y 9 páginas** medidas | §8 |

**Lo que NO cambió y sigue siendo cierto:** los siete convenios de §1, ~~las siete
tablas, los cinco índices~~, las relaciones de §3 y la cadena de unicidad de §4.
~~**Corregido el 2026-09-03:** hoy son **nueve tablas y siete índices propios**; los
convenios, las relaciones y la cadena de unicidad sí siguen en pie.~~
**Corregido otra vez el 2026-09-04:** son **nueve tablas y ocho índices propios**;
los convenios y las relaciones siguen en pie, pero **la cadena de unicidad de §4 ya
NO**: la migración 12 retiró `UNIQUE (numero_caso)`, que era su primer eslabón. Ver
la advertencia que abre §4.

---

## Puesta al día del 2026-09-03 — de la versión 6 a la 11

La puesta al día anterior dejó este documento a la altura de la **versión 6**. Entre
esa noche y `aa90ad1` entraron **cinco migraciones más** (7, 8, 9, 10 y 11). Lo
declaró el propio programador al cerrar su pase (`DECISIONES.md`, 2026-09-03,
«Cerrados los hallazgos de la auditoría final», sección «Lo que dejó sin cubrir»),
literal: *«`docs/ARQUITECTURA.md` no declara `casos.templo_nombre` ni
`filas_descartadas`: del planificador»*. Esta sección lo cierra, y encontró más de lo
que ese aviso decía.

**Cómo se midió** (2026-09-03, sobre el árbol en `aa90ad1`). No se leyó el DDL: se
**construyó** una base con el propio código del proyecto y se le preguntó al motor.

```
$ .venv/Scripts/python.exe <script en el scratchpad>
    # abrir_conexion(<temporal>) + aplicar_esquema + PRAGMA table_info por tabla
SQLite motor: 3.50.4
VERSION_ACTUAL: 11 | aplicar_esquema devolvio: 11
MIGRACIONES declaradas: [2, 3, 4, 5, 6, 7, 8, 9, 10, 11]
TABLAS REALES (9): asignaciones, casos, companeros, contactos, documentos_ilegibles,
                   filas_descartadas, personas, procedencia_campo, version_esquema
TABLAS_DEL_ESQUEMA (9): las mismas
Diferencia: set()
```

⚠️ **La conexión hay que abrirla con `datos.conexion.abrir_conexion`, no con
`sqlite3.connect` a secas.** Medido al primer intento: un `sqlite3.connect` por
defecto abre en `isolation_level=''` y la migración 2 revienta con «cannot start a
transaction within a transaction» antes de llegar a la 3. La medición de este
documento se hizo por la puerta del programa, que es la única que dice la verdad.

**El recuento, tabla por tabla, contra lo que este documento declaraba:**

| Tabla | Columnas en el código | Columnas que declaraba este documento | Diferencias | Dónde se corrigió |
|---|---|---|---|---|
| `version_esquema` | 3 | 3 | **0** | — |
| `casos` | 13 | 12 | **2** (falta `templo_nombre`; `numero_caso` figuraba como NO nulo) | §2.2 |
| `personas` | 25 | 18 | **7** (los seis `paso_*` y `llamo_al_lider`) | §2.3 |
| `companeros` | 5 | 5 | **0** | — |
| `asignaciones` | 6 | 6 | **0** | — |
| `contactos` | 12 | 12 | **0** | — |
| `procedencia_campo` | 15 | 15 | **0** | — |
| `documentos_ilegibles` | 8 | **la tabla no existía en este documento** | **8** | §2.8 (nueva) |
| `filas_descartadas` | 9 | **la tabla no existía en este documento** | **9** | §2.9 (nueva) |
| **Total** | **96** | **71** | **26 divergencias en 9 tablas** | |

**96 columnas en el código, 96 en el documento tras esta corrección, 0 diferencias.**
*(Foto del 2026-09-03. Hoy son **104**: ver la puesta al día del 2026-09-04.)*
Las 26 son 25 columnas que faltaban más un nulo mal declarado (`casos.numero_caso`).

**Índices propios: 7 en el código, 5 declaraba este documento.** Faltaban
`idx_ilegibles_ruta` (versión 8) e `idx_descartadas_companero` (versión 11).
Corregido en §6.

⚠️ **Premisas del pase que no se sostuvieron, dichas antes de construir encima.**

| Premisa del pase | Qué se midió | Consecuencia |
|---|---|---|
| «`personas.pagina_pdf` (4, ya declarada o no, compruébalo)» | **Ya estaba declarada** desde la puesta al día del 2026-09-02 (§2.3). Y hay **dos** `pagina_pdf` distintas, que el pase no separa: `casos.pagina_pdf` (versión 3, la hoja que abrió el caso) y `personas.pagina_pdf` (versión 4, la hoja de cada persona) | Nada que corregir ahí. Se deja dicho porque confundirlas es exactamente el fallo que la versión 4 vino a arreglar |
| «La regla “un caso, un compañero” **no existe** en ningún documento» | **Sí existe, como pregunta abierta**: es la **P-11** de §7, escrita el 2026-09-02 | No se inventa ninguna regla. La P-11 se queda abierta y **gana una medición** que antes no tenía: ver §2.5 |
| La lista de divergencias del pase (7 puntos) | **Incompleta**: no nombraba que `documentos_ilegibles` y `filas_descartadas` faltan **enteras** con 8 y 9 columnas, ni los dos índices que faltaban, ni que `casos.numero_caso` seguía declarado como **NO nulo** en la tabla de §2.2 | Se corrigen las 26, no las 7 |

### La tabla de versiones del esquema: qué añadió cada una y por qué

~~De la 1 a la 11 sin saltos.~~ **De la 1 a la 15 sin saltos** — corregido el
2026-09-04 sobre `02832f4`. Medido sobre una base temporal construida con
`abrir_conexion` + `aplicar_esquema` en el scratchpad:

```
$ python -c "... SELECT version FROM version_esquema ORDER BY version"
VERSIONES: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15]
VERSION MAX version_esquema: 15
```

Medido leyendo `version_esquema` de la base construida
arriba y `datos.migraciones.MIGRACIONES`. Las descripciones son las que el propio
código escribe en la base; el motivo va citado de `DECISIONES.md` o del comentario del
módulo que la implementa.

| Ver. | Qué añadió | Por qué | Dónde vive |
|---|---|---|---|
| **1** | El DDL inicial: `version_esquema`, `casos`, `personas`, `companeros`, `asignaciones`, `contactos`, `procedencia_campo` | Es el esquema con el que nació el programa. **No es una migración**: es la constante `_DDL_VERSION_1` | `datos/esquema.py` |
| **2** | `casos.unidad_numero` admite **6 o 7 dígitos**. Reconstruye `casos` entera | El `CHECK` de seis rechazaba un dato verdadero: 4 de las 9 páginas reales traen `7000011` (`DECISIONES.md`, 2026-09-02). SQLite no sabe cambiar un `CHECK` con `ALTER TABLE`: hay que reconstruir | `datos/migraciones.py` |
| **3** | `casos.pagina_pdf`, `casos.unidad_nombre`; `procedencia_campo.banda_x0/y0/x1/y1` y `anulado_por_tachon` (**7 columnas**) | «Los huecos que tapaban la pantalla de corrección». La banda va en **fracciones** de página y no en píxeles para que la tira siga recortándose bien a otra escala; el tachón no es lo mismo que «vacío» | `datos/migraciones.py` |
| **4** | `personas.pagina_pdf` | Desde que las páginas de un grupo se unen en un solo caso, un caso puede tener 12 personas en 6 hojas. Recortar las 12 tiras de la hoja del caso enseñaba **la fila de otra persona** al lado del MRN | `datos/migraciones.py` |
| **5** | `personas.estado_propuesto`, `nota_companero`, `propuesto_por`, `propuesto_en`; `contactos.contactado_por` y `respondio` (**6 columnas**) | Lo que las FASES 6 y 7 no tenían dónde guardar. La propuesta va en columnas propias y **no** en `procedencia_campo`: escribir ahí un `verificado_por` obliga a `verificado = 1`, y eso es marcar como verificado automáticamente — regla permanente 5 | `datos/migraciones.py` |
| **6** | `personas.motivo_no_viajo` y `pudo_viajar` | El reporte de la FASE 8 habla de **personas** que no pudieron viajar, no de casos. En `casos`, una familia de cuatro donde falla uno saldría como cuatro personas con el motivo copiado | `datos/migraciones.py` |
| **7** | `casos.numero_caso` **admite NULL**. Reconstruye `casos` entera | Medido en la máquina del dueño el 2026-09-02: un PDF suyo dio «0 casos de 1 página» porque no se leyó el número, y se tiró la página entera con los nombres, los MRN y la fecha ya leídos. **No se inventa un número** (regla permanente 1): entra a NULL, que significa «todavía no se sabe» | `datos/migraciones_de_importacion.py` |
| **8** | Tabla **`documentos_ilegibles`** + `idx_ilegibles_ruta` (**8 columnas**) | Pedido por el dueño el 2026-09-02: *«si no puede debe ponerlo en un renglón que notifique que tales documentos no son legibles»*. Un cuadro de diálogo se cierra con Aceptar y no deja nada; con 500 documentos no hay forma de leerlo al vuelo | `datos/migraciones_de_importacion.py` |
| **9** | `personas.paso_preparacion`, `paso_informacion`, `paso_cita_del_templo`, `paso_acciones_requeridas`, `paso_entrevistas`, `paso_listo_para_el_templo` y `llamo_al_lider` (**7 columnas**) | El dueño quiere el Excel del proyecto viejo (`DECISIONES.md`, 2026-09-03), y ese Excel no pregunta «¿está completa?»: pregunta los **seis pasos** uno por uno, más si el compañero llamó al líder. Un solo estado dice que no está lista; los seis pasos dicen **en cuál se quedó**, que es lo que hay que decirle al líder por teléfono | `datos/migraciones.py` |
| **10** | `casos.templo_nombre` | El templo está impreso en el formulario y hasta ese día se descartaba; la cabecera del Excel del agente lo pide. **Texto libre, sin catálogo**: la lista de templos con su color es decisión del dueño y no la ha tomado (P-10) | `datos/migraciones.py` |
| **11** | Tabla **`filas_descartadas`** + `idx_descartadas_companero` (**9 columnas**) | Lo que volvió en el Excel de un compañero y **no** entró se perdía al cerrar la ventana, y con ello la única pista de que había trabajo hecho que nadie recogió. Medido en la ida y vuelta de QA: la persona sin MRN volvió con 0 de 7 pasos | `datos/migraciones.py` |
| **12** | `casos.numero_caso` **deja de ser `UNIQUE`** + `idx_casos_numero`. Reconstruye `casos` entera. **Cero columnas nuevas** | Medido por el dueño en la PC del trabajo el 2026-09-03: importó diez documentos y **seis se rechazaron** con «caso ya existente». Un renglón, literal: *«El caso BALC2609 ya estaba en la base con 1 persona(s)… MRN en común: 0»*. **Era otra familia.** El número es `BALC` + `2609` = **una unidad y un mes**, no una familia, y en su carpeta muchos documentos lo comparten por construcción. El `UNIQUE` convertía eso en tirar el documento entero. La identidad del caso pasa a ser **el documento del que salió**. SQLite no sabe quitar un `UNIQUE` de columna con `ALTER TABLE`: hay que reconstruir | `datos/migraciones_de_importacion.py` |
| **13** | `casos.duplicado_de` | Decidido por el dueño el 2026-09-03, literal: *«Si hay documentos duplicados debe decirlo y no rechazarlo.»* El que repite **entra igual** y queda marcado con el caso del que es duplicado; lo que sigue prohibido es **pisar** el que ya estaba, con sus correcciones a mano y sus firmas. **Nada se fusiona solo.** Va como columna y no como tabla aparte porque es un dato *del* caso y se consulta en todas las listas que ya leen `casos`; una tabla de dos columnas obligaría a un `JOIN` en cada una. Con `ALTER TABLE ADD COLUMN`, que la documentación oficial admite para una columna con `REFERENCES` cuando su defecto es NULL — y aquí lo es | `datos/migraciones_de_importacion.py` |
| **14** | `casos.estado_marcado_por`, `estado_marcado_en`, `estado_marcado_origen`, `estado_del_companero`, `estado_del_companero_por`, `estado_del_companero_en`; `procedencia_campo.ausente_en_el_papel` (**7 columnas**) | Los dos grupos de tres están duplicados **a propósito**: uno guarda quién puso el estado que vale **ahora**, y el otro **lo que dijo la hoja del compañero, que no se borra cuando Miguel corrige encima**. Palabras del dueño el 2026-09-03: *«lo que Miguel marque a mano después manda, y queda "Corregida por Miguel" encima de lo del compañero, sin borrar lo que dijo el compañero»*. Con un solo grupo eso no se puede cumplir: la corrección pisaría el nombre de Sandy y nadie sabría que discreparon. `ausente_en_el_papel` es la misma forma que `anulado_por_tachon` de la v3 y por el mismo motivo: sin ella, «no está en el papel» se ve igual que «el OCR no supo leerlo» | `datos/migraciones_de_revision.py` |
| **15** | `personas.mrn` admite que el **último carácter sea una letra**. Reconstruye `personas` entera. **Cero columnas nuevas** | La cédula de miembro puede acabar en letra y nuestra regla la rechazaba. Palabras del dueño, citadas en `DECISIONES.md` («La cédula PUEDE terminar en letra»): *«muchas cédulas de miembro tienen una A u otra letra al final»*. El módulo que la implementa deja medido el daño: sobre los siete PDF reales costaba **2 de 7 cédulas**, y lo leído sobrevivía solo en `procedencia_campo.valor_ocr`, donde el dueño no lo ve. Es el mismo error que la v2 arregló para `unidad_numero`: una regla escrita mirando formularios que no son los del dueño. SQLite no sabe cambiar un `CHECK` con `ALTER TABLE`: hay que reconstruir | `datos/migraciones_de_cedula.py` |

**Cómo se cuenta.** ~~Llegar a la versión 11 cuesta **diez migraciones** (2 a 11)~~
~~**Corregido el 2026-09-04:** llegar a la versión **14** cuesta **trece migraciones**
(2 a 14) sobre un DDL inicial que es la versión 1. **Ocho** viven en
`datos/migraciones.py`, **cuatro —la 7, la 8, la 12 y la 13— en
`datos/migraciones_de_importacion.py`**, y **una —la 14— en
`datos/migraciones_de_revision.py`**; las cinco se importan desde el primero.~~
**Corregido otra vez el 2026-09-04 (segunda puesta al día):** llegar a la versión
**15** cuesta **catorce migraciones** (2 a 15) sobre un DDL inicial que es la
versión 1. Medido con `grep -n "^def migrar_a_version_"` sobre los cuatro módulos:

```
datos/migraciones.py                 8  (2, 3, 4, 5, 6, 9, 10, 11)
datos/migraciones_de_importacion.py  4  (7, 8, 12, 13)
datos/migraciones_de_revision.py     1  (14)
datos/migraciones_de_cedula.py       1  (15)
```

Las seis de los tres últimos módulos se importan desde el primero. La
lista `MIGRACIONES` de `datos/migraciones.py` sigue siendo el **único** orden que
cuenta, y `VERSION_ACTUAL` se deriva de ella (`MIGRACIONES[-1][0]`) en vez de
escribirse a mano, para que nadie añada una migración y se olvide de subir el número.

⚠️ **El orden de la 12, la 13 y la 14 no es indiferente, y está anotado en el
código.** La 12 **reconstruye `casos` entera** y la 13 y la 14 le añaden columnas.
Al revés, la reconstrucción de la 12 tendría que conocer columnas que aún no
existían — o se las llevaría por delante. El comentario de `datos/migraciones.py`
lo dice literal: *«El orden de esta tupla ES el orden en que se aplican.»*
**Y la 15 hereda el mismo cuidado, en la otra tabla:** reconstruye `personas`
entera, así que su `INSERT` tiene que nombrar **todas** las columnas que la tabla
ya tiene, incluidas las que llegaron por `ALTER TABLE ADD COLUMN` en las versiones
4, 5, 6 y 9. Están nombradas una a una en los dos lados del `INSERT`, y **`id` va
la primera y se copia tal cual** porque `procedencia_campo` apunta a la persona
por su `id` y el motor no puede defender ese par con una clave foránea (§2.7).

⚠️ **Y una lección de esta ronda que ya costó 45 errores, para quien enganche la
15:** al registrar la 14 en `MIGRACIONES`, `aplicar_esquema` empezó a aplicarla
sola y el `setUp` de una prueba la volvía a aplicar a mano
(`duplicate column name: estado_marcado_por`). **Una prueba pide el esquema a
`aplicar_esquema`; no llama a `migrar_a_version_N`.** La regla, con su criterio
comprobable, está en `PENDIENTES.md` · «Deuda del ciclo 5 · DC-13».

---

## Puesta al día del 2026-09-04 — de la versión 11 a la 14

La puesta al día anterior dejó este documento a la altura de la **versión 11**.
Entre esa noche y `c0a268c` entraron **tres migraciones más** (12, 13 y 14),
escritas por dos programadores distintos que corrían en paralelo. Esta sección las
declara.

**Cómo se midió** (2026-09-04, sobre el árbol en `c0a268c`, árbol limpio
comprobado con `git status --porcelain` → sin salida). No se leyó el DDL: se
**construyó** una base con el propio código y se le preguntó al motor. La conexión,
otra vez, por `datos.conexion.abrir_conexion` y no con `sqlite3.connect` a secas,
por lo que ya avisaba la sección anterior.

```
$ .venv/Scripts/python.exe <scratchpad>/medir_esquema.py
    # abrir_conexion(<temporal>) + aplicar_esquema + PRAGMA table_info por tabla
SQLite motor: 3.50.4
VERSION_ACTUAL: 14 | aplicar_esquema devolvio: 14
MIGRACIONES declaradas: [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14]
TABLAS REALES (9): asignaciones, casos, companeros, contactos,
                   documentos_ilegibles, filas_descartadas, personas,
                   procedencia_campo, version_esquema
TABLAS_DEL_ESQUEMA (9): las mismas
Diferencia: set()

asignaciones            6  id,caso_id,companero_id,asignado_en,activa,desactivada_en
casos                  20  id,numero_caso,unidad_numero,fecha_viaje,captura_manual,
                           archivado,fecha_archivado,ruta_pdf,creado_en,
                           estado_recomendacion,pagina_pdf,unidad_nombre,templo_nombre,
                           duplicado_de,estado_marcado_por,estado_marcado_en,
                           estado_marcado_origen,estado_del_companero,
                           estado_del_companero_por,estado_del_companero_en
companeros              5  id,nombre,activo,desactivado_en,creado_en
contactos              12  id,caso_id,fecha,medio,con_quien,resultado,anulado,
                           motivo_anulacion,anulado_en,registrado_en,contactado_por,
                           respondio
documentos_ilegibles    8  id,ruta_pdf,pagina_pdf,motivo,detalle,lineas_leidas,caso_id,
                           registrado_en
filas_descartadas       9  id,companero_id,ruta_excel,fila_excel,numero_caso,mrn,nombre,
                           motivo,registrado_en
personas               25  id,caso_id,mrn,nombre,fila_formulario,ord_recibir_propias,
                           ord_observar_sellamiento,ord_traductor,ord_investidura,
                           ord_sellamiento_esposos,ord_sellamiento_hijo_padres,pagina_pdf,
                           estado_propuesto,nota_companero,propuesto_por,propuesto_en,
                           motivo_no_viajo,pudo_viajar,paso_preparacion,paso_informacion,
                           paso_cita_del_templo,paso_acciones_requeridas,paso_entrevistas,
                           paso_listo_para_el_templo,llamo_al_lider
procedencia_campo      16  id,tabla,registro_id,campo,origen,confianza,valor_ocr,verificado,
                           verificado_por,verificado_en,banda_x0,banda_y0,banda_x1,banda_y1,
                           anulado_por_tachon,ausente_en_el_papel
version_esquema         3  version,aplicada_en,descripcion
TOTAL_COLUMNAS 104

INDICES PROPIOS (8):
    idx_asignacion_viva -> asignaciones          idx_asignaciones_companero -> asignaciones
    idx_casos_numero -> casos                    idx_casos_viaje_activos -> casos
    idx_descartadas_companero -> filas_descartadas
    idx_ilegibles_ruta -> documentos_ilegibles
    idx_personas_caso -> personas                idx_procedencia_registro -> procedencia_campo
```

**El recuento, tabla por tabla, contra lo que este documento declaraba:**

| Tabla | Columnas en el código | Columnas que declaraba este documento | Diferencias | Dónde se corrigió |
|---|---|---|---|---|
| `version_esquema` | 3 | 3 | **0** | — |
| `casos` | 20 | 13 | **7** (`duplicado_de` de la v13; las seis de la marca, v14) | §2.2 |
| `personas` | 25 | 25 | **0** | — |
| `companeros` | 5 | 5 | **0** | — |
| `asignaciones` | 6 | 6 | **0** | — |
| `contactos` | 12 | 12 | **0** | — |
| `procedencia_campo` | 16 | 15 | **1** (`ausente_en_el_papel`, v14) | §2.7 |
| `documentos_ilegibles` | 8 | 8 | **0** | — |
| `filas_descartadas` | 9 | 9 | **0** | — |
| **Total** | **104** | **96** | **8 columnas en 2 tablas** | |

**104 columnas en el código, 104 en el documento tras esta corrección, 0
diferencias.** La divergencia era menor que la de la ronda anterior —26— y por un
motivo que conviene decir: **el programador de identidad y el de Revisar dejaron
sus migraciones documentadas dentro de su propio módulo antes de registrarlas**,
así que lo que faltaba aquí era solo transcribirlo, no descubrirlo.

**Y el «0 diferencias» no es de fiarse a ojo: se cotejó con un comando, y con su
denominador.** Un segundo script lee las tablas markdown de cada §2.x de este
mismo archivo, saca la lista de columnas que **declara**, y la compara **elemento
a elemento** con la que devuelve el motor —lista blanca, no búsqueda de lo que
falta:

```
TABLA                  MOTOR    DOCUMENTO DIFERENCIA
asignaciones           6        6         0
casos                  20       20        0
companeros             5        5         0
contactos              12       12        0
documentos_ilegibles   8        8         0
filas_descartadas      9        9         0
personas               25       28        SOBRA: ['1', '0', 'NULL']
procedencia_campo      16       20        SOBRA: ['anotacion', 'ocr', 'vacio', 'manual']
version_esquema        3        3         0
------------------------------------------------------------
TOTAL motor: 104 | TOTAL declarado: 111
```

⚠️ **Los 7 «sobrantes» son falsos positivos de mi detector, y los identifico uno a
uno en vez de retocar el número:** el extractor confunde con filas de columna las
**tablas auxiliares** que viven dentro de esas dos secciones. `1`, `0` y `NULL`
son «Los tres estados de una casilla» (§2.3, líneas 713-715); `anotacion`, `ocr`,
`vacio` y `manual` son los cuatro valores de `origen` (§2.7, líneas 916-919).
**Ninguno es una columna declarada de más.** Descontados los siete: **104 y 104,
en las nueve tablas, sin una sola diferencia.** Un detector con falsos positivos
conocidos y enumerados sigue sirviendo; uno con falsos **negativos** no, y por eso
la comparación se hace enumerando las dos listas enteras y no buscando ausencias.

**Índices propios: 8 en el código, 7 declaraba este documento.** Faltaba
`idx_casos_numero`, que crea la migración 12. Corregido en §6. **Y ese índice no
es cosmético:** lo que antes daba gratis el `UNIQUE (numero_caso)` —encontrar un
caso por su número sin recorrer la tabla— sigue haciendo falta en cada fila que
vuelve del Excel del compañero y en cada comprobación de duplicado, y al quitar
el `UNIQUE` había que reponerlo a mano. Su programador lo hizo y lo dejó escrito
en `datos/migraciones_de_importacion.py`.

⚠️ **Un cambio de esta ronda que NO es una columna, y es el más importante de las
tres migraciones:** `casos.numero_caso` **deja de ser `UNIQUE`** (versión 12). No
se ve en ningún recuento de columnas y cambia qué es un caso. Ver §2.2 y §4.

### La medición que hay que repetir y no es de esta ronda

Este documento mide **lo que el código produce sobre una base nueva**. La base
**real** del dueño migró de la 11 a la 13 el 2026-09-03 a las 22:28 y
`DECISIONES.md` deja escrito que **ningún agente reconoce haberlo hecho**. No hay
pérdida conocida —8 tablas idénticas al respaldo v11—, pero ~~**nadie ha comprobado
que su base esté hoy en la 14 ni que sus 104 columnas sean éstas**. No lo puedo
comprobar yo: esa base lleva datos de personas reales y está fuera del
repositorio.~~ *Lo mide: el supervisor, o QA con el `.exe` delante.*

⚠️ **Medido el 2026-09-04, en la segunda puesta al día: su base está en la 13, no
en la 14.** Lo abrí **en solo lectura** (`sqlite3.connect(..., mode=ro)`) y conté
filas y versión; **no leí ni un nombre ni un MRN**, que es la única forma en que
esto era mío de medir:

```
$ python -c "sqlite3 file:///C:/Users/josem/Documents/Fichas/fichas.db?mode=ro"
version: 13
casos 2
personas 0
procedencia_campo 8
companeros 1
contactos 0
```

Eso deja **dos** migraciones sin aplicar en la máquina del dueño —la 14 y la 15—,
no una. Que sus columnas sean estas 104 **sigue sin comprobarse**: yo conté filas
y versión, no `PRAGMA table_info`. *Lo mide: QA con el `.exe` delante.*

---

## Puesta al día del 2026-09-04 (segunda) — la versión 15

La sección anterior dejó este documento a la altura de la **14**. Ese mismo día
entró una migración más, la **15**, en un módulo nuevo:
`datos/migraciones_de_cedula.py`.

**Qué cambia, y es una sola cosa.** El `CHECK` de `personas.mrn`. El último de los
cuatro caracteres del tercer grupo pasa a admitir también una letra. El motivo lo
fijó el dueño y está en `DECISIONES.md`, entrada «La cédula PUEDE terminar en
letra»: *«muchas cédulas de miembro tienen una A u otra letra al final»*.

**Cómo se midió** (2026-09-04, sobre `02832f4`, con `git status --porcelain` sin
salida antes de empezar). No se leyó el DDL de `datos/esquema.py`: se **construyó**
una base temporal en el scratchpad con `abrir_conexion` + `aplicar_esquema` y se
le preguntó al motor por `sqlite_master` y `PRAGMA table_info`.

```
VERSION MAX version_esquema: 15
TABLAS: 9  asignaciones, casos, companeros, contactos, documentos_ilegibles,
           filas_descartadas, personas, procedencia_campo, version_esquema
  asignaciones 6 · casos 20 · companeros 5 · contactos 12 ·
  documentos_ilegibles 8 · filas_descartadas 9 · personas 25 ·
  procedencia_campo 16 · version_esquema 3
TOTAL COLUMNAS: 104
INDICES PROPIOS (sql NOT NULL): 8
CHECK totales en el DDL de la base: 48
```

**El recuento contra lo que este documento declaraba: nueve tablas y 104 columnas
en el código, nueve y 104 en el documento, 0 diferencias.** La 15 no toca ninguna
columna: reconstruye la tabla para cambiar una restricción, y el `INSERT` copia las
25 columnas de `personas` nombradas una a una.

### Los `CHECK` son 48, no 28 — y de dónde salía el 28

`PENDIENTES.md`, en el criterio C2-7 de la FASE C2, decía *«28 `CHECK` en total»* y
*«los otros 27»*. Ese número **no era el del esquema**: era el de `grep -c CHECK
datos/esquema.py`, que solo ve el DDL de la **versión 1** y no las catorce
migraciones posteriores. Medido sobre el esquema que el motor produce de verdad:

| Tabla | `CHECK` |
|---|---|
| `asignaciones` | 2 |
| `casos` | 9 |
| `companeros` | 2 |
| `contactos` | 3 |
| `documentos_ilegibles` | 2 |
| `filas_descartadas` | 1 |
| `personas` | 18 |
| `procedencia_campo` | 11 |
| `version_esquema` | 0 |
| **Total** | **48** |

De los 9 de `casos`, **exactamente uno nombra `numero_caso`**, así que quitarlo
dejaría **47**, no 27. Corregido en `PENDIENTES.md` · FASE C2. El hallazgo lo
devolvió el programador del terreno Datos y lo remedí aquí sobre la base construida.

⚠️ **`grep -c CHECK datos/esquema.py` no es una medición del esquema, y este
documento lo deja escrito para que nadie la repita.** Da **28** —lo comprobé—
porque `esquema.py` es un artefacto congelado de la versión 1. El esquema vivo
solo se lee preguntándole al motor.

---

## Qué autoridad tiene este documento

`CLAUDE.md` §7 declara que **el esquema de datos manda sobre cualquier plan**. Esta
es su sede, decidida por el dueño el 2026-09-02 sobre la comparación de tres
opciones de `PENDIENTES.md` («Bloqueo 1 · Dónde debe vivir»).

Y tiene un techo, que es el de `CLAUDE.md` §8:

> **El DDL del código es la autoridad de última instancia cuando exista.** Si
> `sqlite3 base.db ".schema"` y este documento discrepan, **gana el código** y este
> documento se corrige contra él. Hasta que la FASE 1 escriba ese DDL, este
> documento es lo único que hay, y por eso desbloquea la fase.

Cada cambio posterior de esquema se registra en `DECISIONES.md` con su fecha y su
motivo.

---

## Premisas del pase que NO se sostuvieron — medido antes de escribir nada

Dos afirmaciones del pase que abrió este documento no resisten una búsqueda en el
repositorio. Se corrigen aquí porque las dos cambian lo que hay que escribir.

**Barrido, con su denominador y su control positivo** (2026-09-02):

```
$ grep -rniI "estado_recomendacion\|no_indicada\|version_esquema\|schema_version" \
    --exclude-dir=.git --exclude-dir=.venv --exclude-dir=build \
    --exclude-dir=dist --exclude-dir=__pycache__ --exclude-dir=modelos .
(sin salida)

$ grep -rniIc "numero_caso" <mismas exclusiones> . | grep -v ":0"   # control positivo
./DECISIONES.md:2
./PENDIENTES.md:11

$ grep -rlI "" <mismas exclusiones> . | wc -l                        # universo barrido
65
```

El detector ve —caza `numero_caso` en 2 archivos, 13 líneas— y sobre los **65
archivos de texto** del proyecto encuentra **0** de los cuatro términos.

| Premisa del pase | Qué se midió | Consecuencia |
|---|---|---|
| «la tabla `version_esquema` **que la FASE 1 pide** con un entero» | `version_esquema` → **0 coincidencias**. Ni `PENDIENTES.md` ni ningún otro documento la piden, en ninguna fase | La tabla **se entrega igual**, porque el pase la encarga y porque una base que va a crecer en las fases 6, 7 y 8 necesita saber su versión. Pero **no viene de la FASE 1**, y por tanto su forma no está atada por ningún criterio: queda abierta como decisión, con su alternativa nativa comparada abajo |
| «`estado_recomendacion` aparece como “lista desplegable con los cuatro valores”… solo se nombran `no_indicada` e `incompleta`» | `estado_recomendacion` → **0**. `no_indicada` → **0**. «cuatro valores» → **0**. «desplegable» → **1**, y es sobre compañeros (`PENDIENTES.md:905`), no sobre esto. `incompleta` → 4 líneas, **todas en prosa** («la recomendación incompleta»), ninguna como valor de una lista | **El hueco es mayor de lo que el pase describe.** No faltan dos valores de cuatro: **falta el campo entero**. Ni su nombre, ni uno solo de sus valores están escritos en este repositorio. Se trata como pregunta abierta al dueño y **no se rellena** |

El resto de las premisas del pase sí se sostiene: `docs/` no existía (`ls` → *No such
file or directory*, salida 2); `docs/ARQUITECTURA.md` y `docs/adr/ADR-*.md` están en
la lista blanca de `.claude/hooks/no-crear-documentos.sh` y `propiedad-por-rol.sh:66`
asigna ambos al planificador.

---

## 1. Convenios del esquema

Cada convenio, con lo que lo obliga.

### 1.1 Nombres en español y sin tildes ni eñes

Regla permanente 4. Los identificadores van en **ASCII**: `companeros`, no
`compañeros`. No es una concesión estética — `PENDIENTES.md:918` ya escribe el
nombre literal `DELETE FROM companeros` dentro de un criterio de aceptación de QA.
Si la tabla llevara `ñ`, ese criterio no casaría con la tabla que dice vigilar.

### 1.2 Fechas: TEXT en ISO-8601 `AAAA-MM-DD`

SQLite no tiene tipo de fecha. Documentación oficial
(<https://www.sqlite.org/datatype3.html>, consultada 2026-09-02), literal:

> «SQLite does not have a storage class set aside for storing dates and/or times.
> Instead, the built-in Date And Time Functions of SQLite are capable of storing
> dates and times as TEXT, REAL, or INTEGER values»

Se elige **TEXT ISO-8601** de las tres, por tres motivos medibles:

1. Ordena cronológicamente **como texto**, así que la ventana de 7 días de la FASE 5
   sale con una comparación de cadenas, sin funciones ni conversiones.
2. Es legible en un volcado, que es lo que se va a mirar cuando algo falle.
3. La regla del mes cruzado de `DECISIONES.md` se comprueba con `substr` sobre la
   propia cadena, sin desempaquetar nada.

Las marcas de tiempo (`creado_en`, `verificado_en`…) van en el mismo formato
extendido `AAAA-MM-DD HH:MM:SS`.

⚠️ **Lo que esto NO da:** un `GLOB` valida la **forma**, no que la fecha exista.
`2026-02-31` pasa cualquier `CHECK` de forma. `DECISIONES.md` exige «fecha real», y
eso **se valida en Python antes del `INSERT`**. La restricción de la base es el
respaldo, no el validador.

### 1.3 Booleanos: INTEGER 0/1

Misma fuente, literal:

> «SQLite does not have a separate Boolean storage class. Instead, Boolean values
> are stored as integers 0 (false) and 1 (true).»

Todo booleano lleva su `CHECK (columna IN (0,1))`. Los que admiten «no se sabe»
llevan además `NULL` como tercer estado, y se dice en cada caso qué significa.

### 1.4 Números que empiezan por cero: TEXT, nunca INTEGER

`mrn` y `unidad_numero` son **TEXT**. Un `INTEGER` se come los ceros de delante y
`055-1111-3853` deja de existir. Lo obliga la FASE 4 crit. 2, que exige releer la
celda y ver `055-1111-3853` entero, y la FASE 6, que reconcilia por ese valor: un
cero perdido rompe el emparejamiento en silencio.

### 1.5 Las claves foráneas hay que encenderlas — en cada conexión

Documentación oficial (<https://www.sqlite.org/foreignkeys.html>, consultada
2026-09-02), literal:

> «Foreign key constraints are disabled by default (for backwards compatibility),
> so must be enabled separately for each database connection.»

**Obligación vinculante para la FASE 1:** toda conexión ejecuta
`PRAGMA foreign_keys = ON` **antes de la primera consulta**. Sin eso, cada
`REFERENCES` de este documento es un comentario decorativo y el motor deja pasar
huérfanos sin decir nada.

Y la acción al borrar se escribe **siempre explícita**. Misma fuente, literal:

> «If an action is not explicitly specified, it defaults to "NO ACTION".»

### 1.6 Claves primarias: `INTEGER PRIMARY KEY`

Cada tabla lleva `id INTEGER PRIMARY KEY`, que en SQLite es alias del `rowid`. Las
claves de negocio (`numero_caso`, el par `caso_id`+`mrn`) se protegen con
restricciones `UNIQUE`, no se usan como clave primaria: un `numero_caso` mal leído
por el OCR y corregido después obligaría a reescribir todas las filas hijas.

### 1.7 El texto del usuario jamás se concatena en una instrucción

Línea base de seguridad, y no es recomendación: **es criterio de aceptación**. Toda
consulta usa marcadores `?` y pasa los valores en la tupla de parámetros, de forma
que el motor reciba la lógica y los datos **por separado**. Un intento de inyección
llega entonces como dato inerte.

Ya tiene criterio comprobable escrito —FASE 1 crit. 3, apartados 3a a 3d de
`PENDIENTES.md`— con lista blanca, denominador y la prueba de comportamiento
(`'); DROP TABLE casos; --` guardado y releído literal). Este documento no lo
reabre; lo hereda.

**Lo que la línea base de seguridad NO aporta aquí, dicho para que no se busque:**
no hay hash de contraseñas —**Argon2id no aplica**— porque el programa no tiene
autenticación, ni usuarios, ni sesión: es un ejecutable local de un solo usuario,
sin red y sin puertos (reglas permanentes 1 y 2). Meter un derivador de claves
donde no hay credencial que proteger sería peso muerto en el ejecutable. Lo que sí
aplica de esa línea base es lo de arriba —consultas parametrizadas— más la
integridad referencial de §1.5 y las restricciones de coherencia de §2, que es
donde este esquema se defiende de verdad.

### 1.8 Modo de diario: se queda el de por defecto

**No se activa WAL.** Documentación oficial (<https://www.sqlite.org/wal.html>,
consultada 2026-09-02), literal:

> «There is an additional quasi-persistent "-wal" file and "-shm" shared memory file
> associated with each database, which can make SQLite less appealing for use as an
> application file-format.»

> «All processes using a database must be on the same host computer; WAL does not
> work over a network filesystem.»

Motivo de la decisión: la carpeta de datos se resuelve por API y **puede caer bajo
OneDrive** (`DECISIONES.md`, 2026-09-02: la ruta la devuelve el sistema, sea cual
sea, y los criterios 1e/6d solo **avisan**). Dos archivos satélite que un cliente de
sincronización puede copiar desacompasados respecto del `.db` son una vía de
corrupción que el modo por defecto no tiene. Un solo proceso local no gana nada con
WAL a cambio.

---

## 2. Esquema de datos

~~Siete tablas.~~ ~~**Nueve tablas y 96 columnas** — corregido el 2026-09-03: entraron
`documentos_ilegibles` (versión 8, §2.8) y `filas_descartadas` (versión 11, §2.9).~~
**Nueve tablas y 104 columnas** — corregido otra vez el 2026-09-04: **no entró
ninguna tabla nueva**, entraron ocho columnas en dos tablas (siete en `casos`, una
en `procedencia_campo`) con las versiones 13 y 14, y `casos.numero_caso` **dejó de
ser `UNIQUE`** con la 12.
**Y una segunda vez el 2026-09-04: siguen siendo nueve tablas y 104 columnas en la
versión 15.** La 15 no añade ni quita columnas — cambia un `CHECK` de
`personas.mrn` reconstruyendo la tabla. **48 `CHECK` en total**, medidos sobre el
motor.
Para cada columna: tipo, si acepta nulo, y qué la obliga.

### 2.1 `version_esquema`

Qué versión de esquema tiene la base y cuándo se aplicó cada una.

| Columna | Tipo | Nulo | Restricción / motivo |
|---|---|---|---|
| `version` | INTEGER | NO | `PRIMARY KEY`. El entero que identifica la versión |
| `aplicada_en` | TEXT | NO | Marca de tiempo ISO-8601 |
| `descripcion` | TEXT | NO | Qué cambió esa versión, en español |

Una fila por versión aplicada. La versión vigente es `MAX(version)`.

**Idempotencia (FASE 1 crit. 8):** el alta de la versión inicial se hace con
`INSERT OR IGNORE`, de modo que arrancar dos veces sobre una base ya creada deja el
conteo igual y no duplica nada.

> **Alternativa nativa, y es del dueño decidir.** SQLite ya trae un entero de
> versión para la aplicación, sin tabla ninguna. Documentación oficial
> (<https://www.sqlite.org/pragma.html>, consultada 2026-09-02), literal:
>
> > «The user_version pragma will get or set the value of the user-version integer
> > at offset 60 in the database header. The user-version is an integer that is
> > available to applications to use however they want. SQLite makes no use of the
> > user-version itself.»
>
> | | `version_esquema` (tabla) | `PRAGMA user_version` |
> |---|---|---|
> | Coste | Una tabla más | Cero: ya está en la cabecera del archivo |
> | Qué guarda | **Historial**: qué migración, cuándo y por qué | **Un solo entero**, sin fecha ni motivo |
> | Cómo se lee | `SELECT MAX(version)` | `PRAGMA user_version` |
> | Riesgo | Dos autoridades si además se usa el pragma | Ninguna traza de qué se aplicó ni cuándo |
>
> **Recomiendo la tabla**, y por un motivo concreto de este proyecto: cuando algo
> salga mal en una base que ya lleva meses de datos de Miguel, la pregunta va a ser
> *«¿qué migración tocó esto y qué día?»*, y el pragma no la responde. **Y se usa
> una sola**: no se escribe también `user_version`, porque dos sitios que dicen la
> versión acaban discrepando. La decisión es del dueño.

### 2.2 `casos`

Un caso es **un formulario de recomendación**, con su número de caso.

| Columna | Tipo | Nulo | Restricción / motivo |
|---|---|---|---|
| `id` | INTEGER | NO | `PRIMARY KEY` |
| ~~`numero_caso`~~ | ~~TEXT~~ | ~~NO~~ | ~~**`UNIQUE`**. `CHECK (numero_caso GLOB '[A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9]')` — 4 letras mayúsculas + 4 dígitos (`DECISIONES.md`, reglas de formato)~~ |
| `numero_caso` | TEXT | **SÍ** | **Corregido el 2026-09-03 (migración 7).** Deja de ser obligatorio: una página cuyo número no se pudo leer se guarda igual, pendiente de identificar, en vez de tirarse entera. El daño está medido en la máquina del dueño el 2026-09-02 — un PDF suyo dio «0 casos de 1 página» con los nombres, los MRN y la fecha **ya leídos**. **No se inventa un número** (regla permanente 1): entra a `NULL`, que significa «todavía no se sabe», y la pantalla de corrección deja teclearlo. Lo que **no** cambió: sigue `UNIQUE` y sigue con su `CHECK`, ahora precedido de `numero_caso IS NULL OR`. Medido en esta máquina el 2026-09-03 (SQLite 3.50.4): dos filas con `numero_caso` NULL entran las dos, un `'CASP2609'` repetido se rechaza con «UNIQUE constraint failed», y un `'xx'` se rechaza con «CHECK constraint failed» · ⚠️ **La mitad de esto dejó de ser cierta el 2026-09-04: ver la fila siguiente** |
| `numero_caso` | TEXT | SÍ | **Corregido el 2026-09-04 (migración 12): DEJA DE SER `UNIQUE`.** El `CHECK` de formato se queda igual —que el número no sea único no significa que valga cualquier cosa— y el nulo de la migración 7 también. **Lo que cambia es qué identifica.** El número son cuatro letras más `AAMM`: identifica **una unidad y un mes**, no una familia, y en la carpeta real del dueño muchos documentos distintos lo comparten por construcción. Medido por él en la PC del trabajo el 2026-09-03: de diez documentos importados, **seis se rechazaron** con «caso ya existente» y eran **otras familias**. La identidad de un caso pasa a ser **el documento del que salió** (archivo más hoja o hojas), y mismo número con MRN distintos = otro caso. Como el `UNIQUE` daba gratis la búsqueda por número, la migración crea `idx_casos_numero` en su lugar (§6). ⚠️ **Lo que esto le hace a §4:** la cadena de unicidad de este documento se apoyaba en `numero_caso` y hay que leerla con esta corrección delante — ver la nota al final de §4 |
| `duplicado_de` | INTEGER | SÍ | **Añadida el 2026-09-04** (migración 13). El `id` del caso del que este documento es duplicado; `NULL` si no repite a ninguno. `REFERENCES casos (id) ON DELETE RESTRICT ON UPDATE RESTRICT` — un caso al que apunta un duplicado no se puede borrar sin decidir antes qué pasa con él. **Decidido por el dueño:** *«Si hay documentos duplicados debe decirlo y no rechazarlo.»* El duplicado es un caso aparte con su marca y **nada se fusiona solo**. ⚠️ **Lo que NO detecta, y está medido:** la detección va por la ruta del archivo y por los MRN dentro del número; `importacion/guardado.py:501-504` dice literal *«No es una huella del contenido: es la ruta»*, así que **un archivo copiado a otra ruta y sin MRN legible entra sin marca**. Es DC-4 de `PENDIENTES.md` y la P-16 de §7 |
| `estado_marcado_por` | INTEGER | SÍ | **Añadida el 2026-09-04** (migración 14). Quién puso el estado que vale **ahora**. `REFERENCES companeros (id) … RESTRICT`, y no un texto libre con el nombre, por lo mismo que `procedencia_campo.verificado_por`: un nombre escrito a mano se escribe de dos formas el mismo día y entonces no se puede agrupar por quién. **Miguel es una fila de `companeros`** como cualquier otro — ya tenía que serlo para poder firmar campos |
| `estado_marcado_en` | TEXT | SÍ | Migración 14. ISO-8601. `CHECK` que la ata con `estado_marcado_por`: o las dos o ninguna. ⚠️ **El `CHECK` ata quién con cuándo y NADA MÁS — no ata el estado con la firma**, y es deliberado: hay casos guardados antes de que estas columnas existieran, con `estado_recomendacion` puesto y sin firma, y un `CHECK` que los exigiera juntos los convertiría en filas que ya no se pueden actualizar nunca más, sin avisar y desde otro módulo |
| `estado_marcado_origen` | TEXT | SÍ | Migración 14. De dónde salió la marca: la ruta del Excel que la trajo, o la frase que dice que se puso a mano. **Texto libre, sin catálogo**, por el mismo criterio que `estado_recomendacion` y `templo_nombre` |
| `estado_del_companero` | TEXT | SÍ | Migración 14. Lo que dijo la hoja del compañero. **Sin `CHECK` de lista**: la lista vive en `datos/estados.py` |
| `estado_del_companero_por` | INTEGER | SÍ | Migración 14. `REFERENCES companeros (id) … RESTRICT` |
| `estado_del_companero_en` | TEXT | SÍ | Migración 14. ISO-8601, con su `CHECK` cruzado con `estado_del_companero_por` |
| `unidad_numero` | TEXT | SÍ | ~~6 dígitos~~ **6 o 7 dígitos.** `CHECK (unidad_numero IS NULL OR unidad_numero GLOB '[0-9]×6' OR unidad_numero GLOB '[0-9]×7')`. TEXT por §1.4. **Corregido el 2026-09-02:** el `CHECK` de seis rechazaba un dato verdadero — 4 de las 9 páginas reales traen `7000011`, siete dígitos (`DECISIONES.md`, «`unidad_numero` admite 6 o 7 dígitos»). Lo aplica la migración 2, que reconstruye la tabla entera porque SQLite no sabe cambiar un `CHECK` con `ALTER TABLE` |
| `fecha_viaje` | TEXT | SÍ | ISO-8601. `CHECK` de forma **más** la regla del mes cruzado, abajo |
| `captura_manual` | INTEGER | NO | `DEFAULT 0`, `CHECK IN (0,1)`. FASE 2 crit. 10: formulario manuscrito, campos vacíos, sin rellenar nada |
| `archivado` | INTEGER | NO | `DEFAULT 0`, `CHECK IN (0,1)`. FASE 8 |
| `fecha_archivado` | TEXT | SÍ | ISO-8601. Coherente con `archivado`, abajo |
| `ruta_pdf` | TEXT | SÍ | Ruta del PDF de origen. **Derivada, no citada literalmente**: la exige FASE 3 crit. 6 —«la imagen que se ve en el segundo es la del segundo caso»—, que no se puede cumplir si el caso no sabe de qué archivo salió |
| `creado_en` | TEXT | NO | Marca de tiempo del alta. **Derivada**: la exige FASE 8 crit. 3, un reporte «del periodo», que necesita una fecha por la que agrupar |
| ~~`estado_recomendacion`~~ | ~~—~~ | ~~—~~ | ~~⛔ **PENDIENTE. Ver «Preguntas abiertas · P-1». No se define aquí.**~~ |
| `estado_recomendacion` | TEXT | SÍ | **Resuelto el 2026-09-02** (`DECISIONES.md`, P-1): columna **guardada**, no derivada, porque la FASE 6 la rellena desde fuera. **Sin `CHECK`**: la lista de valores válidos vive en `datos/estados.py` y hoy solo tiene los dos que constan en el material (`no_indicada`, `incompleta`). ⚠️ Falta el valor que significa «resuelta» — hasta que el dueño lo diga, `ESTADOS_QUE_RESUELVEN` está vacía y **todo caso cuenta como sin resolver** |
| `pagina_pdf` | INTEGER | SÍ | **Añadida el 2026-09-02** (migración 3). La hoja del PDF que **abrió** el caso, en base 1. `CHECK (pagina_pdf IS NULL OR pagina_pdf >= 1)`. No es la hoja de cada persona: eso es `personas.pagina_pdf` (migración 4) |
| `unidad_nombre` | TEXT | SÍ | **Añadida el 2026-09-02** (migración 3). El nombre de la unidad tal como el extractor lo leyó de la etiqueta `Ward/Branch Name and Unit Number`. Valores reales medidos: `Castries Branch`, `Paramaribo Branch` |
| `templo_nombre` | TEXT | SÍ | **Añadida el 2026-09-03** (migración 10). El nombre del templo, leído del papel de la etiqueta `Temple Name` y guardado tal cual. **Sin `CHECK` y sin catálogo**, por el mismo criterio con el que `estado_recomendacion` tampoco lo lleva (P-1): la lista de templos con su color es una decisión del dueño que sigue sin tomar (P-10), y un `CHECK` con una lista inventada rechazaría mañana un templo verdadero. Nace a `NULL` en los casos anteriores: de ésos nadie leyó el templo. ⚠️ **Y va sin fila en `procedencia_campo` y sin campo en la pantalla**, decidido por el programador y aceptado (`DECISIONES.md`, 2026-09-03): con procedencia habría que poder firmarlo, y un campo firmable que no se dibuja dejaría «Todo correcto» bloqueado en todos los casos. **El coste, medido y dicho:** un templo mal leído no se corrige a mano — una hoja leyó `Caracas-Venezueia` y se queda así. Correcto por la regla 1, pero deja el dato malo sin arreglo. Pendiente: campo de templo editable cuando exista el catálogo. Esto **contradice** lo que §2bis.4 propuso para esta columna; ver la nota de allí |

> **Por qué DOS grupos de tres y no uno** *(añadido el 2026-09-04)*. La
> duplicación es a propósito y la obliga una frase del dueño del 2026-09-03:
> *«lo que Miguel marque a mano después manda, y queda "Corregida por Miguel"
> encima de lo del compañero, **sin borrar lo que dijo el compañero**»*. Con un
> solo juego de columnas, la corrección de Miguel pisaría el nombre de Sandy y
> **nadie sabría que discreparon** — que es justo el caso que hay que poder mirar.
> Con dos, la tarjeta dice las dos cosas: «Corregida por Miguel» arriba y «Sandy
> dijo: completa» debajo.
>
> **Y cómo encaja con la regla permanente 5, que el dueño precisó ese mismo día:**
> la **firma de campos** («Todo correcto», `verificado_por` de §2.7) sigue siendo
> de Miguel y **nunca automática**; el **estado de la recomendación** lo escribe
> el Excel del compañero con su nombre. Son dos cosas y **no se mezclan**: por eso
> el estado vive en `casos` con su propia firma y no toca `procedencia_campo.
> verificado`, que sigue en 0 hasta que Miguel pulse.
>
> ⚠️ **Lo que estas seis columnas NO guardan, y lo devolvió su propio
> programador:** guardan **una** marca, no un historial. *«Si la misma hoja vuelve
> dos veces, la segunda pisa a la primera. Guardar TODAS las marcas de la historia
> es una tabla nueva y una decisión de arquitectura — no la tomo yo, va al
> planificador.»* Recogida como **P-15** en §7; **no la decido yo tampoco**,
> porque cambia el esquema y el coste lo paga el dueño.
>
> **Por qué columnas planas y no una tabla de historial con `JOIN`:** el motivo
> está medido en el propio pase — *«imagina que tenga 3 000 formularios»*. La
> lista de «Revisar» lee **una fila por documento** y no puede pagar un `JOIN`
> correlacionado por tarjeta. Con 3 000 documentos, aplicar la hoja devuelta costó
> 0,071 s y leer las tarjetas 0,141 s (informe de su programador, **no repetido
> por mí**). Lo que se paga a cambio es P-15.

**Nulos, y por qué.** `unidad_numero` y `fecha_viaje` aceptan nulo porque la
precedencia de valores de `DECISIONES.md` lo produce de verdad: *«si hubo tachón y
no hay corrección, el campo queda vacío y marcado para revisión»*. Una columna
obligatoria haría imposible guardar ese caso, que es justo el caso que el programa
existe para enseñarle a Miguel.

**Las dos restricciones de coherencia**, que son las que impiden una fila a medias:

```
CHECK ( fecha_viaje IS NULL
        OR fecha_viaje GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]' )

-- ~~La regla del mes cruzado (DECISIONES.md): el mes de fecha_viaje coincide con
-- los ultimos 4 digitos de numero_caso leidos como AAMM.~~
-- ~~CHECK ( fecha_viaje IS NULL
--          OR substr(fecha_viaje,3,2) || substr(fecha_viaje,6,2)
--             = substr(numero_caso,5,4) )~~
-- RETIRADA el 2026-09-02: NO existe en el motor. Ver la nota de abajo.

-- Un caso archivado tiene fecha; uno no archivado, no. No hay medio archivado.
CHECK ( (archivado = 0 AND fecha_archivado IS NULL)
        OR (archivado = 1 AND fecha_archivado IS NOT NULL) )
```

⚠️ ~~La regla del mes cruzado como `CHECK` tiene un efecto que hay que ver antes de
firmarlo: **hace imposible guardar un viaje reprogramado a otro mes.** Ver
«Preguntas abiertas · P-2».~~

**Corregido el 2026-09-02.** La regla del mes cruzado **no es una restricción del
motor**: `DECISIONES.md` (P-2) la resolvió como **aviso**, por el motivo que este
mismo documento anticipaba —haría imposible guardar un viaje reprogramado a otro
mes, que es una cosa que pasa—. Vive en `datos/validacion.py` y la pantalla la
enseña. Este documento la describía como `CHECK`; el código no la tiene, y **gana
el código** (`CLAUDE.md` §8). Las dos restricciones que `casos` sí lleva en el motor
son la de forma de `fecha_viaje` y la de coherencia de `archivado`.

### 2.3 `personas`

Las personas de cada formulario. El formulario trae varias filas; cada fila llena
es una persona.

| Columna | Tipo | Nulo | Restricción / motivo |
|---|---|---|---|
| `id` | INTEGER | NO | `PRIMARY KEY` |
| `caso_id` | INTEGER | NO | `REFERENCES casos(id) ON DELETE RESTRICT ON UPDATE RESTRICT` |
| ~~`mrn`~~ | ~~TEXT~~ | ~~SÍ~~ | ~~Patrón 3-4-4 con guiones. `CHECK (mrn IS NULL OR mrn GLOB '[0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]')`~~ |
| `mrn` | TEXT | SÍ | **Corregido el 2026-09-04 (migración 15).** Patrón 3-4-4 con guiones, y **el último carácter puede ser una letra**: `CHECK (mrn IS NULL OR mrn GLOB '[0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9A-Za-z]')` — transcrito del `sqlite_master` de la base construida el 2026-09-04, no del código. Lo decidió el dueño (`DECISIONES.md`, «La cédula PUEDE terminar en letra»): *«muchas cédulas de miembro tienen una A u otra letra al final»*. Es el mismo error que la v2 corrigió en `unidad_numero`: una regla de forma escrita sin mirar los papeles del dueño rechazaba un dato verdadero, y el campo quedaba vacío mientras lo leído seguía vivo solo en `procedencia_campo.valor_ocr`. ⚠️ **Sigue siendo un `CHECK` bloqueante**: un MRN de otra forma **no entra en la base**, y eso choca con el requisito 9 del dueño («un valor raro se guarda y se señala en su sitio») igual que el de `numero_caso` — ver `PENDIENTES.md` · FASE C2 |
| `nombre` | TEXT | SÍ | El nombre tal como se leyó o se corrigió |
| `fila_formulario` | INTEGER | SÍ | En qué fila del formulario venía. `CHECK (fila_formulario IS NULL OR fila_formulario >= 1)` |
| `ord_recibir_propias` | INTEGER | SÍ | Casilla 1. `CHECK (… IS NULL OR … IN (0,1))` |
| `ord_observar_sellamiento` | INTEGER | SÍ | Casilla 2 |
| `ord_traductor` | INTEGER | SÍ | Casilla 3 |
| `ord_investidura` | INTEGER | SÍ | Casilla 4 |
| `ord_sellamiento_esposos` | INTEGER | SÍ | Casilla 5 |
| `ord_sellamiento_hijo_padres` | INTEGER | SÍ | Casilla 6 |
| `pagina_pdf` | INTEGER | SÍ | **Añadida el 2026-09-02** (migración 4). De qué hoja del PDF salió **esta persona**, base 1. `CHECK (… IS NULL OR … >= 1)`. No basta con la del caso: desde que las páginas de un grupo se unen en un solo caso, un caso puede tener 12 personas en 6 hojas, y recortar las 12 tiras de la hoja del caso enseñaba **la fila de otra persona** al lado del MRN. Nace a `NULL` en las personas anteriores: de ésas nadie sabe de qué hoja salieron |
| `estado_propuesto` | TEXT | SÍ | **Añadida el 2026-09-02** (migración 5). Lo que el compañero propone en su Excel. **Sin `CHECK`**, por el mismo motivo que `casos.estado_recomendacion` |
| `nota_companero` | TEXT | SÍ | Migración 5. El texto libre que escribe el compañero |
| `propuesto_por` | INTEGER | SÍ | Migración 5. `REFERENCES companeros(id) ON DELETE RESTRICT ON UPDATE RESTRICT` |
| `propuesto_en` | TEXT | SÍ | Migración 5. ISO-8601 |
| `motivo_no_viajo` | TEXT | SÍ | **Añadida el 2026-09-02** (migración 6). Texto libre, **no** lista cerrada: la lista no la ha dicho nadie |
| `pudo_viajar` | INTEGER | SÍ | Migración 6. `1` viajó · `0` no viajó · `NULL` nadie lo ha dicho. `CHECK` cruzado: exige `motivo_no_viajo` cuando es `0` y lo prohíbe en los otros dos casos |
| `paso_preparacion` | INTEGER | SÍ | **Añadida el 2026-09-03** (migración 9). Paso 1 de «Preparación para las ordenanzas». `CHECK (… IS NULL OR … IN (0,1))` |
| `paso_informacion` | INTEGER | SÍ | Migración 9. Paso 2, mismo `CHECK` |
| `paso_cita_del_templo` | INTEGER | SÍ | Migración 9. Paso 3, mismo `CHECK` |
| `paso_acciones_requeridas` | INTEGER | SÍ | Migración 9. Paso 4, mismo `CHECK` |
| `paso_entrevistas` | INTEGER | SÍ | Migración 9. Paso 5, mismo `CHECK` |
| `paso_listo_para_el_templo` | INTEGER | SÍ | Migración 9. Paso 6, mismo `CHECK` |
| `llamo_al_lider` | INTEGER | SÍ | Migración 9. **No es un séptimo paso**: los seis salen en la pantalla del líder, y éste dice lo que hizo el compañero cuando vio que faltaba alguno. Mezclarlo haría que el conteo diera siete y una persona con los seis pasos completos a la que nadie llamó saldría como no lista. Mismo `CHECK` |

> ⚠️ **La propuesta del compañero NO va en `procedencia_campo`, y es una decisión,
> no un descuido.** El pase de la FASE 6 pedía guardarla «con su nombre en
> `verificado_por`». El `CHECK` de §2.7 no lo admite: no hay forma de escribir un
> `verificado_por` sin poner `verificado = 1`, y poner `verificado = 1` porque llegó
> un Excel **es marcar algo como verificado automáticamente** — la regla permanente 5
> exactamente. Se cumplió la regla permanente: la propuesta vive en columnas propias,
> `verificado` sigue en 0, y `verificado_por` lo sigue rellenando solo el botón que
> Miguel pulsa. Registrado en `DECISIONES.md` y en `datos/migraciones.py`.

> **Por qué hacen falta las SIETE de la migración 9 y no basta con
> `estado_propuesto`** *(añadido el 2026-09-03)*. El dueño dijo que el Excel del
> proyecto viejo es el que quiere (`DECISIONES.md`, 2026-09-03). Ese Excel **no**
> pregunta «¿está completa?»: pregunta los seis pasos uno por uno, y además si el
> compañero llamó al líder. La diferencia no es de forma. Un solo estado dice que la
> persona no está lista; **los seis pasos dicen en cuál se quedó**, y eso es lo que
> hay que decirle al líder cuando se le llama por teléfono, que es el trabajo entero.
>
> Van en `personas` y no en `casos` por lo mismo que `pudo_viajar`: un formulario de
> grupo lleva hasta doce personas y la preparación es de cada una. Una familia de
> cuatro donde a uno le falta la entrevista no son cuatro personas con la entrevista
> pendiente.
>
> Las siete son parte de la **propuesta** del compañero, firmada por `propuesto_por`
> y `propuesto_en` (migración 5). **No son una verificación:** `procedencia_campo.
> verificado` sigue en 0 y lo confirma Miguel (regla permanente 5).
>
> ⚠️ **Y los pasos se ven, no se editan** (`DECISIONES.md`, 2026-09-03, aceptado): el
> esquema guarda **una** propuesta por persona, y dejarlos cambiar desde la pantalla
> borraría el trabajo de otro sin avisar. **Pregunta abierta que esto deja:** si
> Miguel debe poder corregirlos, hay que decidir **quién firma esa corrección** —
> ver §7, P-13.

**Los tres valores de los siete, y por qué tres y no dos:** `NULL` nadie miró ese
paso · `0` se miró y no está · `1` se miró y está. Un `NOT NULL DEFAULT 0` habría
escrito «no está preparada» sobre todas las personas ya guardadas, de las que nadie
ha dicho nada: un dato inventado sobre personas reales (regla permanente 1). Es el
mismo razonamiento de las seis casillas de ordenanzas, y no hay que confundirlos:
**los seis `paso_*` no son las seis `ord_*`.** Las `ord_*` son a qué va la persona al
templo, leídas del papel; los `paso_*` son los pasos del sistema del líder, que
devuelve el compañero en su hoja.

**`pudo_viajar` y `motivo_no_viajo` están en `personas` y no en `casos` a propósito.**
El reporte de la FASE 8 habla de **personas** que no pudieron viajar. Puestas en
`casos`, una familia de cuatro donde solo falla uno saldría como cuatro personas que
no viajaron con el mismo motivo copiado cuatro veces: un dato falso sobre tres
personas reales. El coste, dicho: **archivar es del caso y el resultado del viaje es
de cada persona**. Son dos preguntas distintas y se responden por separado.

Las seis casillas van **en el orden literal de `DECISIONES.md`**: «recibir ordenanzas
propias, observar ordenanza de sellamiento, traductor, investidura, sellamiento
esposa a esposo, sellamiento hijo a padres». El porqué de que sean seis columnas y
no una tabla hija está en `docs/adr/ADR-0001-casillas-de-ordenanzas.md`.

**Los tres estados de una casilla, y por qué hacen falta tres:**

| Valor | Significa |
|---|---|
| `1` | marcada |
| `0` | no marcada |
| `NULL` | **no leída** — pendiente de captura manual |

El tercero no es un lujo: lo exige la FASE 2 crit. 11b. Con menos de 3 formularios
para calibrar el umbral, *«las seis columnas de ordenanzas vuelven vacías y marcadas
para captura manual»*. Sin `NULL`, «vacía» y «no marcada» serían el mismo valor y
Miguel no podría distinguir lo que el sistema no leyó de lo que leyó como negativo.

**Por qué `fila_formulario` NO lleva `CHECK … <= 6`.** `DECISIONES.md` dice que el
formulario tiene seis filas, y ese seis está marcado ahí mismo como **hipótesis del
plan sin comprobar** —el PDF de referencia no está en el disco—. Un `CHECK` sobre un
número no medido convierte una suposición en una pared: el día que llegue un
formulario de ocho filas, el programa se negaría a guardar datos reales por una cifra
que nadie verificó. Se acota por abajo, que sí es seguro, y nada más.

**Las dos restricciones de coherencia:**

```
-- DECISIONES.md: "Una fila sin nombre y sin MRN se descarta; no se guarda una
-- persona en blanco." Aqui deja de depender de que el codigo se acuerde.
CHECK ( nombre IS NOT NULL OR mrn IS NOT NULL )

-- La mitad de la clave de reconciliacion. Ver seccion 4.
UNIQUE ( caso_id, mrn )
```

### 2.4 `companeros`

Quien verifica. **Se desactivan, no se borran** (`DECISIONES.md`, FASE 6).

| Columna | Tipo | Nulo | Restricción / motivo |
|---|---|---|---|
| `id` | INTEGER | NO | `PRIMARY KEY` |
| `nombre` | TEXT | NO | |
| `activo` | INTEGER | NO | `DEFAULT 1`, `CHECK IN (0,1)`. FASE 6 crit. 3 |
| `desactivado_en` | TEXT | SÍ | ISO-8601 |
| `creado_en` | TEXT | NO | |

```
CHECK ( (activo = 1 AND desactivado_en IS NULL)
        OR (activo = 0 AND desactivado_en IS NOT NULL) )
```

**`nombre` no es `UNIQUE`, a propósito.** Dos personas pueden llamarse igual, y una
restricción que lo impida obligaría a inventar un desempate al dar de alta a la
segunda. Lo que se pierde: el desplegable de asignación puede mostrar dos entradas
idénticas. Es un problema de interfaz, no de datos, y se resuelve en la pantalla.

### 2.5 `asignaciones`

Qué casos lleva cada compañero (FASE 6).

| Columna | Tipo | Nulo | Restricción / motivo |
|---|---|---|---|
| `id` | INTEGER | NO | `PRIMARY KEY` |
| `caso_id` | INTEGER | NO | `REFERENCES casos(id) ON DELETE RESTRICT ON UPDATE RESTRICT` |
| `companero_id` | INTEGER | NO | `REFERENCES companeros(id) ON DELETE RESTRICT ON UPDATE RESTRICT` |
| `asignado_en` | TEXT | NO | ISO-8601 |
| `activa` | INTEGER | NO | `DEFAULT 1`, `CHECK IN (0,1)` |
| `desactivada_en` | TEXT | SÍ | ISO-8601, coherente con `activa` |

**Un mismo caso no se asigna dos veces al mismo compañero — pero sí se puede
reasignar después de retirarlo.** Eso no lo da un `UNIQUE` normal: en cuanto se
desactiva una asignación y se vuelve a crear, el `UNIQUE` la rechazaría. Se resuelve
con un **índice único parcial**, que SQLite soporta. Documentación oficial
(<https://www.sqlite.org/partialindex.html>, consultada 2026-09-02), literal:

> «A partial index definition may include the UNIQUE keyword. If it does, then
> SQLite requires every entry in the index to be unique. This provides a mechanism
> for enforcing uniqueness across some subset of the rows in a table.»

> «Partial indexes have been supported in SQLite since version 3.8.0 (2013-08-26).»

```
CREATE UNIQUE INDEX idx_asignacion_viva
  ON asignaciones (caso_id, companero_id)
  WHERE activa = 1;
```

⚠️ **Lo que ese índice NO defiende, medido el 2026-09-03.** Es único sobre el par
`(caso_id, companero_id)`, no sobre `caso_id` solo. Comprobado contra el motor
(SQLite 3.50.4) sobre una base construida por el propio código:

```
mismo compañero dos veces vivo sobre el caso 1  -> rechazado:
    UNIQUE constraint failed: asignaciones.caso_id, asignaciones.companero_id
dos compañeros DISTINTOS vivos sobre el caso 1  -> ACEPTADO
    SELECT COUNT(*) FROM asignaciones WHERE activa=1 AND caso_id=1  ->  2
```

Es decir: **el motor permite hoy dos asignaciones vivas sobre el mismo caso**, con
compañeros distintos. Eso **no es un fallo del índice** —hace justo lo que dice— sino
una regla que **nadie ha escrito**: ni `CLAUDE.md`, ni `DECISIONES.md`, ni este
documento dicen si un caso puede llevarlo más de un compañero a la vez. Es la **P-11**
de §7, abierta desde el 2026-09-02 y **devuelta al dueño**. Aquí solo se deja la
medición; **no se inventa la regla**. Si el dueño dice «un caso, un compañero», la
migración es un índice único parcial sobre `asignaciones (caso_id) WHERE activa = 1`,
y hasta entonces `interfaz/asignacion.py` puede crear las dos y el resumen de §6ter
cuenta ese caso dos veces.

⚠️ **`activa` es inferencia mía, no una decisión escrita.** «Los compañeros se
desactivan, no se borran» está en `DECISIONES.md`; **de las asignaciones no dice
nada**, y la FASE 8 crit. 5c solo prohíbe borrar filas de casos y de personas. Borrar
la fila de asignación al retirar un caso sería legal según la letra. Elijo la
desactivación por coherencia con el resto del sistema y porque conserva quién llevó
qué. Ver «Preguntas abiertas · P-4».

### 2.6 `contactos` — ⚠️ PROVISIONAL

Contactos con el líder sobre un caso (FASE 7).

> ⛔ **Esta tabla es un punto de partida, no un contrato.** `PENDIENTES.md` lo dice
> de su propia fase: *«`CLAUDE.md` y `DECISIONES.md` no contienen ni una sola
> decisión sobre esta fase: no dicen qué es un “contacto con el líder”, qué campos
> lleva, quién es el líder respecto de un caso»*. Y su crit. 4 exige, **antes de
> cerrar la fase**, una entrada del dueño en `DECISIONES.md` que lo diga.
>
> Las columnas de abajo salen de la única frase disponible —«cuándo, por qué medio,
> con quién y qué resultó»— más la obligación de anular sin borrar. **Construir
> encima antes de esa entrada es asumir retrabajo conocido.**
>
> ⚠️ **Confirmado y decidido el 2026-09-04.** Sigue PROVISIONAL: `grep -in
> "contactos"` sobre `DECISIONES.md` devuelve **0 líneas**, así que la entrada que
> la FASE 7 exige **no existe**. Y **ninguna de las diez fases del programa en C#
> (C0 a C9) la cubre**. La decisión, escrita entera en `PENDIENTES.md` · «DC-17»:
> **la tabla se queda en el esquema** —sus 12 columnas cuentan dentro de las 104 y
> el criterio C2-1 exige 0 diferencias— **y se queda sin repositorio, sin pantalla
> y fuera del programa nuevo**. En la base viva del dueño tiene **0 filas** (medido
> el 2026-09-04 en solo lectura): no hay nada que portar.

| Columna | Tipo | Nulo | Restricción / motivo |
|---|---|---|---|
| `id` | INTEGER | NO | `PRIMARY KEY` |
| `caso_id` | INTEGER | NO | `REFERENCES casos(id) ON DELETE RESTRICT ON UPDATE RESTRICT` |
| `fecha` | TEXT | NO | Cuándo ocurrió el contacto, ISO-8601 |
| `medio` | TEXT | SÍ | Por qué medio |
| `con_quien` | TEXT | SÍ | Con quién se habló |
| `resultado` | TEXT | SÍ | Qué resultó |
| `anulado` | INTEGER | NO | `DEFAULT 0`, `CHECK IN (0,1)`. FASE 7 crit. 3 |
| `motivo_anulacion` | TEXT | SÍ | Coherente con `anulado` |
| `anulado_en` | TEXT | SÍ | Coherente con `anulado` |
| `registrado_en` | TEXT | NO | Cuándo se registró en el programa (distinto de `fecha`) |
| `contactado_por` | INTEGER | SÍ | **Añadida el 2026-09-02** (migración 5). `REFERENCES companeros(id) … RESTRICT`. **Quién llamó**, que es otra pregunta que `con_quien` —con quién se habló, el líder—. Sin ella, un caso con seis contactos no dice quién de los compañeros los hizo y el historial deja de servir para repartir trabajo |
| `respondio` | INTEGER | SÍ | Migración 5. `1` respondió · `0` no respondió · `NULL` todavía no se sabe. `CHECK (respondio IS NULL OR respondio IN (0,1))`. Un `DEFAULT 0` convertiría «aún no contesta» en «no contestó», que es un dato inventado sobre una persona |

```
CHECK ( (anulado = 0 AND motivo_anulacion IS NULL AND anulado_en IS NULL)
        OR (anulado = 1 AND motivo_anulacion IS NOT NULL AND anulado_en IS NOT NULL) )
```

Esa restricción es la que hace cumplir «deja el registro visible **con su motivo** de
anulación y su fecha»: no se puede anular sin escribir por qué.

### 2.7 `procedencia_campo`

**De dónde salió cada valor extraído, con qué confianza, qué había leído el OCR
antes de que alguien lo corrigiera, y quién lo dio por bueno.** Una fila por campo.

| Columna | Tipo | Nulo | Restricción / motivo |
|---|---|---|---|
| `id` | INTEGER | NO | `PRIMARY KEY` |
| `tabla` | TEXT | NO | `CHECK (tabla IN ('casos','personas'))` |
| `registro_id` | INTEGER | NO | El `id` de la fila en esa tabla |
| `campo` | TEXT | NO | Nombre de la columna cuya procedencia se describe |
| `origen` | TEXT | NO | `CHECK (origen IN ('anotacion','ocr','vacio','manual'))` |
| `confianza` | REAL | SÍ | `CHECK (confianza IS NULL OR (confianza >= 0.0 AND confianza <= 1.0))` |
| `valor_ocr` | TEXT | SÍ | Lo que leyó el OCR **antes** de cualquier corrección. `NULL` si el OCR nunca lo leyó |
| `verificado` | INTEGER | NO | `DEFAULT 0`, `CHECK IN (0,1)` |
| `verificado_por` | INTEGER | SÍ | `REFERENCES companeros(id) ON DELETE RESTRICT ON UPDATE RESTRICT` |
| `verificado_en` | TEXT | SÍ | ISO-8601 |
| `banda_x0` | REAL | SÍ | **Añadida el 2026-09-02** (migración 3). `CHECK (… IS NULL OR (… >= 0.0 AND … <= 1.0))` |
| `banda_y0` | REAL | SÍ | Migración 3, mismo `CHECK` |
| `banda_x1` | REAL | SÍ | Migración 3, mismo `CHECK` |
| `banda_y1` | REAL | SÍ | Migración 3, mismo `CHECK` |
| `anulado_por_tachon` | INTEGER | NO | **Añadida el 2026-09-02** (migración 3). `DEFAULT 0`, `CHECK IN (0,1)`. El papel llevaba un tachón sobre ese campo y nadie escribió la corrección. **No es lo mismo que «vacío»**: es un dato que UNA PERSONA marcó como equivocado a propósito. Sin esta columna la pantalla de corrección enseña ese campo igual que uno que el OCR no supo leer, y se pierde la única pista de que el papel ya decía que el dato estaba mal |
| `ausente_en_el_papel` | INTEGER | NO | **Añadida el 2026-09-04** (migración 14). `DEFAULT 0`, `CHECK IN (0,1)`. **El papel no trae ese campo**, y eso no es un fallo de lectura ni un borrado (`DECISIONES.md`, 2026-09-03, decisión 2 de los mockups v2). Misma forma que `anulado_por_tachon` y por el mismo motivo: sin ella, «no está en el papel» se ve **exactamente igual** que «el OCR no supo leerlo», y Miguel pierde tiempo buscando en la hoja un dato que la hoja nunca tuvo. El `0` por defecto no inventa nada sobre las filas que ya estaban: dice «nadie ha marcado que este campo falte del papel», que es literalmente cierto |

**Los tres estados de un campo que no tiene valor, y por qué hacen falta los tres**
*(añadido el 2026-09-04)*. `procedencia_campo` distingue ahora tres cosas que antes
se confundían en dos, y ninguna es intercambiable:

| Marca | Qué significa | Qué tiene que hacer Miguel |
|---|---|---|
| `origen = 'vacio'` | el OCR miró y no leyó nada | mirar la hoja y teclearlo |
| `anulado_por_tachon = 1` | el papel llevaba un tachón encima | mirar la hoja: alguien dijo que el dato de debajo estaba mal |
| `ausente_en_el_papel = 1` | **la hoja no trae ese campo** | **nada** — no hay dónde mirar |

Las tres se ven igual en pantalla si el esquema no las separa, y la tercera es la
única en la que buscar es perder el tiempo.

**Las cuatro coordenadas de la banda se guardan en FRACCIONES de la página (0.0 a
1.0), no en píxeles**, y esa es la diferencia entre poder volver a recortar la tira
del escaneo y no poder. Los píxeles que calcula la extracción son píxeles **de esa
rasterización, a la escala que salió ese día**; el día que el tope de 3500 px cambie
—o que la página se rasterice más pequeña para caber en la pantalla— el rectángulo
apuntaría a otro sitio del papel. Una fracción de la página sigue valiendo a
cualquier escala.

```
UNIQUE ( tabla, registro_id, campo )

-- Regla permanente 5, hecha estructura: no se puede marcar verificado sin dejar
-- QUIEN y CUANDO. El sistema propone; Miguel confirma, y la confirmacion tiene firma.
CHECK ( (verificado = 0 AND verificado_por IS NULL AND verificado_en IS NULL)
        OR (verificado = 1 AND verificado_por IS NOT NULL AND verificado_en IS NOT NULL) )
```

**Los valores de `origen`, y cuáles están citados y cuáles no:**

| Valor | Procedencia del valor |
|---|---|
| `anotacion` | **Citado.** FASE 2 crit. 2: `origen='anotacion'`, confianza `1.0` |
| `ocr` | **Citado.** FASE 3 crit. 1: «su valor y su origen (anotación / OCR / vacío)» |
| `vacio` | **Citado.** Misma línea |
| `manual` | ⚠️ **Inferido, no citado.** Ningún documento nombra el estado de un campo que Miguel corrigió a mano, y hace falta uno: el pase pide guardar «qué había leído el OCR **antes de que alguien lo corrigiera**», lo que presupone que existe el después. Ver «Preguntas abiertas · P-3» |

**`DEFAULT 0` en `verificado` es obligación, no comodidad.** `PENDIENTES.md`, FASE 1:
*«Si el esquema tiene una columna de verificado, su valor por defecto es “no”»*. Y la
FASE 3 crit. 3 lo mide: al abrir un caso recién extraído, el contador de campos
verificados es **0** antes de tocar nada.

> ### La alternativa que descarté aquí, con su coste
>
> `tabla` + `registro_id` es una **referencia polimórfica**, y SQLite no puede
> imponerla con un `REFERENCES`: un `registro_id` equivocado produce una fila
> huérfana que el motor no caza.
>
> | | **Una tabla polimórfica** (elegida) | **Dos tablas**: `procedencia_caso` + `procedencia_persona` |
> |---|---|---|
> | Integridad | El motor **no** la protege | Clave foránea real, protegida por el motor |
> | Consulta de FASE 3 crit. 2 (campos con confianza < 0.6 de un caso) | Una tabla, dos condiciones | `UNION` de dos consultas, y hay que acordarse de las dos |
> | Añadir una entidad después | Nada | Una tabla de procedencia más |
> | Dónde vive la procedencia | Un solo sitio | Repartida |
>
> **Elijo la tabla única**, y el motivo es que en este sistema **nada se borra
> nunca**: los casos se archivan (FASE 8), las personas no tienen operación de
> borrado (FASE 8 crit. 5c) y todas las claves foráneas van con `RESTRICT`. La
> protección que aporta la clave foránea —evitar apuntar a algo que ya no está— cubre
> un suceso que este esquema hace imposible por otra vía. Lo que sí queda es el
> riesgo de un `registro_id` mal escrito por un fallo de programación; se acota
> haciendo que **solo la capa de extracción escriba esta tabla**, y es comprobable
> con la lista blanca de instrucciones de escritura que ya pide la FASE 8 crit. 5c.
>
> **No es una decisión cerrada.** Si el dueño prefiere integridad garantizada por el
> motor sobre simplicidad de consulta, las dos tablas son legítimas y el coste es el
> `UNION`. ~~Puede convertirse en `ADR-0002`.~~ **Nota del 2026-09-02:** el número
> `ADR-0002` lo tomó la decisión de país, unidad y templo. Si esta alternativa se
> reabre algún día, será con el número siguiente que esté libre.

### 2.8 `documentos_ilegibles` — *añadida el 2026-09-03 (migración 8)*

**Cada PDF o página que no se pudo leer deja su renglón**, con la ruta, la página y
el motivo, y se puede consultar después. Lo pidió el dueño el 2026-09-02, literal:
*«debe poder leer letra dentro de PDF escaneados, y si no puede debe ponerlo en un
renglón que notifique que tales documentos no son legibles»*.

**Por qué una tabla y no un cuadro de diálogo.** Un diálogo con el resumen se cierra
con Aceptar y no deja nada; con 500 documentos no hay forma de leerlo al vuelo ni de
recordarlo. Es el mismo criterio que `interfaz/descartados.py` se aplicó a sí mismo y
que acabó siendo §2.9.

| Columna | Tipo | Nulo | Restricción / motivo |
|---|---|---|---|
| `id` | INTEGER | NO | `PRIMARY KEY` |
| `ruta_pdf` | TEXT | NO | El archivo del que se habla. Sin él el renglón no sirve para ir a mirar nada |
| `pagina_pdf` | INTEGER | SÍ | Base 1. `CHECK (pagina_pdf IS NULL OR pagina_pdf >= 1)`. `NULL` cuando el problema fue del archivo entero y no de una hoja |
| `motivo` | TEXT | NO | **Un código, no una frase.** Una frase se escribe distinta cada vez y no se puede contar; un código se agrupa y contesta la pregunta que si no nadie puede contestar: de 500 documentos, ¿cuántos no se abrieron, cuántos no tenían ni una letra, y cuántos tenían letra pero ningún campo? La frase en español se compone al mostrarla, en `datos/ilegibles.py`. **Sin `CHECK` con la lista de motivos**, por el criterio de P-1: la lista vive en `datos/ilegibles.py` y le pueden faltar motivos que todavía no se han visto — un `CHECK` incompleto perdería mañana justo el documento que esta tabla existe para no perder |
| `detalle` | TEXT | SÍ | El texto libre que acompaña al código, cuando lo hay |
| `lineas_leidas` | INTEGER | SÍ | `CHECK (lineas_leidas IS NULL OR lineas_leidas >= 0)`. Cuántas líneas llegó a leer el OCR antes de rendirse. **Cero es un dato**, distinto de `NULL` = no se contó |
| `caso_id` | INTEGER | SÍ | `REFERENCES casos(id) ON DELETE RESTRICT ON UPDATE RESTRICT`. Apunta al caso que **sí** se llegó a guardar de esa página, cuando lo hay. Es lo que convierte el renglón en accionable: desde la lista se sabe si hay adónde ir o si de esa página no quedó nada |
| `registrado_en` | TEXT | NO | ISO-8601 |

**8 columnas.** Índice propio: `idx_ilegibles_ruta ON documentos_ilegibles (ruta_pdf)`
— la pregunta natural sobre esta tabla es «¿qué pasó con *este* archivo?».

⚠️ Esta tabla **no tiene restricción de unicidad**: el mismo archivo puede dejar
varios renglones (uno por página, o uno por reintento). Es a propósito para no perder
el historial, y el coste es que reprocesar un PDF que sigue ilegible deja renglones
repetidos. Nadie ha dicho qué debe pasar ahí: ver §7, P-14.

### 2.9 `filas_descartadas` — *añadida el 2026-09-03 (migración 11)*

**Lo que volvió en el Excel de un compañero y NO entró en la base deja su renglón**,
y el renglón se queda al cerrar el programa.

El daño que cierra está medido en la ida y vuelta de QA: una persona sin MRN volvió
con 0 de 7 pasos. El trabajo del compañero se descarta —lo cual **es correcto**: casar
por nombre crearía registros fantasma—, pero hasta esa migración la lista de lo
descartado se perdía al cerrar la ventana, y con ella la única pista de que había
trabajo hecho que nadie recogió. `interfaz/descartados.py` lo llevaba anunciado de sí
mismo desde que se escribió: *«si quiere que los descartes sobrevivan al cierre del
programa, es una tabla nueva y su migración»*. El dueño lo decidió tras la auditoría
final de QA, y ésta es la tabla.

| Columna | Tipo | Nulo | Restricción / motivo |
|---|---|---|---|
| `id` | INTEGER | NO | `PRIMARY KEY` |
| `companero_id` | INTEGER | NO | `REFERENCES companeros(id) ON DELETE RESTRICT ON UPDATE RESTRICT`. De quién era el Excel. Un compañero con descartes apuntando no se puede borrar sin decidir antes qué pasa con ellos — y de todos modos los compañeros se desactivan, no se borran (§2.4) |
| `ruta_excel` | TEXT | SÍ | El archivo del que volvió la fila |
| `fila_excel` | INTEGER | SÍ | `CHECK (fila_excel IS NULL OR fila_excel >= 1)`. El número de fila con el que chocó, que es lo que hace falta para ir a mirar **esa** fila |
| `numero_caso` | TEXT | SÍ | **Sin `CHECK` de forma y sin `REFERENCES`**, al revés que `casos.numero_caso`. Es lo que venía escrito en el Excel, y lo que venía escrito puede ser justo lo que estaba mal: validarlo aquí haría que la tabla que existe para guardar lo que no entró rechazara lo que no entró |
| `mrn` | TEXT | SÍ | Igual: el MRN tal como venía, sin validar |
| `nombre` | TEXT | SÍ | Igual |
| `motivo` | TEXT | NO | **Una frase, no un código** — al revés que `documentos_ilegibles`, y la diferencia está razonada: allí hay motivos cerrados que se pueden contar; aquí el motivo lleva dentro el número de la fila del Excel con la que chocó y la causa concreta («la clave "X" no tiene la forma esperada»), que es lo que hace falta para ir a mirar esa fila. Un código perdería justo eso |
| `registrado_en` | TEXT | NO | ISO-8601 |

**9 columnas.** Índice propio: `idx_descartadas_companero ON filas_descartadas
(companero_id)`.

⚠️ **Esta tabla no tiene hoja en el Excel espejo, y `documentos_ilegibles` sí**
(dicho por el programador en `DECISIONES.md`, 2026-09-03, «lo que dejó sin cubrir»).
Un espejo al que le falta una tabla ya no es un espejo entero. **No lo arreglo yo**:
es un hueco declarado, no una propuesta — ver §8.

---

## 2bis. País, unidad y templo — ⚠️ PROPUESTO, NO CONSTRUIDO

> ⛔ ~~**Nada de esta sección existe todavía en el motor.** El esquema medido va por la
> versión 6 y no tiene ninguna de estas tres tablas.~~ Lo que sigue es la
> **especificación** de lo que tendría que hacer una migración nueva; la escribe el
> programador, no este documento.
>
> ✅ **Corregido el 2026-09-03.** Sigue sin existir en el motor **todo menos una
> pieza**: las tres tablas (`paises`, `unidades`, `templos`) y las columnas
> `casos.unidad_id` y `casos.templo_id` **no existen** — medido: la base construida
> por el código tiene 9 tablas y ninguna de ésas. Pero **`casos.templo_nombre` sí se
> construyó**, en la migración 10, y **no como esta sección lo propuso**:
>
> | | Lo que §2bis.4 propuso | Lo que la migración 10 hizo |
> |---|---|---|
> | La columna | `templo_nombre` TEXT, nulo | **igual** |
> | Procedencia | «con su fila en `procedencia_campo`, igual que `unidad_nombre`» | **sin fila de procedencia** |
> | En la pantalla | campo corregible como los demás | **no se dibuja**, no se corrige a mano |
>
> El motivo de la diferencia está en `DECISIONES.md` (2026-09-03) y es de peso: con
> procedencia habría que poder firmarlo, y **un campo firmable que no se dibuja
> dejaría «Todo correcto» bloqueado en todos los casos**. El coste, medido, es que un
> templo mal leído (`Caracas-Venezueia`) se queda así. Ver la fila de `templo_nombre`
> en §2.2. Si el catálogo de templos llega algún día, el campo editable vuelve a la
> mesa.
>
> ⚠️ **Qué número le toca: NO el 7.** Mientras se escribía esto, el programador ya
> tenía en marcha `datos/migraciones_de_importacion.py` con las versiones **7 y 8**
> (medido: `DESCRIPCION_DE_LA_VERSION_7` y `DESCRIPCION_DE_LA_VERSION_8` en ese
> archivo, y las dos ya importadas desde `datos/migraciones.py`:45-46). Esta
> migración toma **el primer número libre cuando se construya**, que hoy sería el 9.
> No se fija aquí: se mira `MIGRACIONES` en ese momento. Un número escrito a mano en
> un documento es exactamente cómo se pisan dos migraciones.

El encargo del dueño, en sus palabras: *«están varios templos y son varios países del
Caribe, y también poder [distinguir por] color de qué países son esas personas»*.

La decisión, su alternativa descartada y el coste de cada una están en
**`docs/adr/ADR-0002-pais-unidad-y-templo.md`**. Aquí va solo la forma.

### 2bis.1 `paises`

| Columna | Tipo | Nulo | Restricción / motivo |
|---|---|---|---|
| `id` | INTEGER | NO | `PRIMARY KEY` |
| `nombre` | TEXT | NO | `UNIQUE`. El nombre tal como Miguel lo escribe |
| `color` | TEXT | NO | `CHECK (color GLOB '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]')` — exactamente `#RRGGBB`, seis dígitos hexadecimales |
| `activo` | INTEGER | NO | `DEFAULT 1`, `CHECK IN (0,1)` |
| `desactivado_en` | TEXT | SÍ | Coherente con `activo`, como `companeros` |
| `creado_en` | TEXT | NO | ISO-8601 |

⛔ **La tabla nace VACÍA. No lleva ni un país precargado.** Ni `CLAUDE.md`, ni
`DECISIONES.md`, ni ninguno de los cuatro documentos de referencia contiene una lista
de países. Escribir una sería inventar un dato (**regla permanente 1**), y además una
lista incompleta obliga a meter en «otros» justo el país real que llegue. Los añade
Miguel desde la pantalla de países, uno a uno, cuando aparece el primer caso de ese
país.

**Por qué `color` es una columna de esta tabla y no un diccionario en el código.** El
color es el encargo literal del dueño, y **un color necesita una identidad estable**.
Si el país fuera texto libre en `casos`, `Santa Lucía`, `Saint Lucia` y `St. Lucia`
serían tres países y tres colores para las mismas personas. Con una fila y un `id`,
el color se escribe una vez y no puede desdoblarse.

**Por qué el `CHECK` de `color` es estricto y no texto libre.** El valor va a parar a
un widget de Tk, que interpreta la cadena de color; un valor que Tk no entienda
levanta en el momento de pintar, que es el peor sitio para enterarse. Un `GLOB` de
`#RRGGBB` lo caza en el `INSERT`, que es donde se puede explicar. Es criterio de
aceptación verificable, no una recomendación: **QA lo comprueba insertando
`rojo`, `#FFF` y `#GGGGGG` y viendo que los tres los rechaza el motor.**

### 2bis.2 `unidades`

**Es aquí donde el programa sabe de qué país es una persona.** Ver §2bis.4.

| Columna | Tipo | Nulo | Restricción / motivo |
|---|---|---|---|
| `id` | INTEGER | NO | `PRIMARY KEY` |
| `unidad_numero` | TEXT | NO | `UNIQUE`. Mismo `CHECK` de 6 o 7 dígitos que `casos.unidad_numero`, y TEXT por §1.4 |
| `nombre` | TEXT | SÍ | El nombre con el que Miguel la reconoce (`Castries Branch`) |
| `pais_id` | INTEGER | NO | `REFERENCES paises(id) ON DELETE RESTRICT ON UPDATE RESTRICT`. **NO nulo**: una unidad sin país no sirve para nada, y admitirlo crearía un segundo estado de «no se sabe» además del que ya da `casos.unidad_id IS NULL` |
| `activa` | INTEGER | NO | `DEFAULT 1`, `CHECK IN (0,1)` |
| `desactivada_en` | TEXT | SÍ | Coherente con `activa` |
| `creado_en` | TEXT | NO | ISO-8601 |

`unidad_numero` es `UNIQUE` porque es la **clave de negocio medible**: tiene forma
estricta (`CHECK` de 6 o 7 dígitos) y sale del papel. El nombre no es único ni
fiable: es texto de OCR.

### 2bis.3 `templos`

| Columna | Tipo | Nulo | Restricción / motivo |
|---|---|---|---|
| `id` | INTEGER | NO | `PRIMARY KEY` |
| `nombre` | TEXT | NO | `UNIQUE` |
| `pais_id` | INTEGER | SÍ | `REFERENCES paises(id) … RESTRICT`. **Nulo permitido**: un templo está en un país, pero puede ser un país del que no hay ninguna unidad, y obligar a darlo de alta antes de poder registrar el templo pondría una pared donde no hace falta |
| `activo` | INTEGER | NO | `DEFAULT 1`, `CHECK IN (0,1)` |
| `desactivado_en` | TEXT | SÍ | Coherente con `activo` |
| `creado_en` | TEXT | NO | ISO-8601 |

⛔ **También nace VACÍA.** Ningún documento del proyecto nombra un solo templo. La
misma regla permanente 1.

### 2bis.4 Las dos columnas nuevas de `casos`, y la pareja crudo / resuelto

| Columna | Tipo | Nulo | Restricción / motivo |
|---|---|---|---|
| `unidad_id` | INTEGER | SÍ | `REFERENCES unidades(id) … RESTRICT`. **El vínculo que Miguel confirma.** Nulo = todavía nadie ha dicho de qué unidad es este caso |
| `templo_nombre` | TEXT | SÍ | **Lo que dice el papel**, extraído de la etiqueta `Temple Name` con su fila en `procedencia_campo`, igual que `unidad_nombre` |
| `templo_id` | INTEGER | SÍ | `REFERENCES templos(id) … RESTRICT`. **El vínculo que Miguel confirma.** Nulo = todavía nadie lo ha dicho |

**El patrón, que es el mismo del resto del programa: crudo al lado de resuelto.**

| | Qué es | Quién lo escribe | Se puede sobrescribir |
|---|---|---|---|
| `unidad_numero`, `unidad_nombre`, `templo_nombre` | **lo que dice el papel** | el extractor, con su fila de `procedencia_campo` | solo corrigiendo el campo, y queda el `valor_ocr` |
| `unidad_id`, `templo_id` | **el juicio de una persona** | Miguel, pulsando | sí, y sin tocar lo de arriba |

⚠️ **No se fusionan en una sola columna, y es la decisión que sostiene todo lo
demás.** Si `unidad_id` sustituyera a `unidad_numero`, resolver el vínculo borraría lo
que el papel decía y `procedencia_campo` se quedaría sin nada con qué contrastar el
`valor_ocr`. Eso es la **regla permanente 5** por la puerta de atrás: el sistema
propone y Miguel confirma, pero la confirmación **no puede destruir la propuesta**.

**El templo ya está en el papel y hoy se tira.** Medido en el código: la etiqueta
`"Temple Name"` figura en `extraccion/formulario.py:49` dentro de
`ETIQUETAS_QUE_CIERRAN_LAS_PERSONAS`, y el comentario de las líneas 43-46 dice que
faltó en **2 de las 9 páginas** (escaneos malos). Es decir: el extractor **ya la
localiza**, la usa como tope inferior del bloque de personas y **no guarda su valor**.
Sacar `templo_nombre` es aplicar a esa ancla el mismo `banda_de_valor` +
`resolver_campo` que ya se aplica a `unidad_nombre` — no es maquinaria nueva.
⚠️ **No lo he medido yo:** cito el comentario del programador; no abrí los PDF
(llevan datos de personas reales, y el pase me lo prohíbe).

### 2bis.5 Índices nuevos

| Índice | Sobre | Para qué |
|---|---|---|
| `idx_casos_unidad` | `casos (unidad_id)` | Pintar el color del país en cada fila de las listas de la FASE 5 sin recorrer `unidades` entera |
| `idx_unidades_pais` | `unidades (pais_id)` | Contar casos por país en el reporte |

⚠️ **Ninguno de los dos está justificado por una medición**, igual que los cinco de
§6. Con 20 países y 200 unidades, SQLite resuelve los dos con un barrido y no se nota.
**Se proponen, y si la FASE que los use mide y no hacen falta, sobran.**

### 2bis.6 Lo que esto cuesta en disco — medido, no estimado

El pase preguntaba por el coste teniendo en cuenta que el ejecutable pesa 216 MB.
**El coste no cae sobre el ejecutable en absoluto**: las tablas viven en el `.db`, y
el `.db` está en `Documentos\Fichas`, **fuera de la carpeta del programa**
(`CLAUDE.md` §4). Añadir tablas a SQLite añade **cero bytes** al `.exe`.

Lo que sí cuesta es espacio en la base, y se midió:

```
$ .venv/Scripts/python.exe -c "<aplicar_esquema; crear las 3 tablas; poblarlas; VACUUM>"
base_v6_vacia_bytes                                                     77824
base_v6_mas_3_tablas_con_20_paises_10_templos_200_unidades_bytes       114688
diferencia_bytes                                                        36864
diferencia_KiB                                                           36.0
```

**36 KiB** con 20 países, 10 templos y 200 unidades dentro. Frente a los **216,9 MiB**
del paquete entregado (`ESTADO.md`), es el **0,016 %** — y ni siquiera vive ahí.
**El tamaño del ejecutable no es un argumento en esta decisión, en ninguna
dirección.** El argumento que sí manda es otro, y está en el ADR: una lista escrita
en el código obliga a **reconstruir y volver a copiar 216 MiB a la PC del trabajo cada
vez que aparece un país nuevo**, mientras que una tabla se rellena tecleando.

---

## 3. Relaciones, y qué pasa al borrar o desactivar

```
   version_esquema        (suelta, sin relaciones)

   casos 1 ──< personas                        (RESTRICT)
   casos 1 ──< asignaciones >── 1 companeros   (RESTRICT en ambos lados)
   casos 1 ──< contactos                       (RESTRICT)

   procedencia_campo ──> (casos | personas)    por (tabla, registro_id): sin FK
   procedencia_campo ──> companeros            (RESTRICT) via verificado_por

   -- PROPUESTO, no construido (§2bis). Version 7:
   casos ──> unidades ──> paises               (RESTRICT) via unidad_id, pais_id
   casos ──> templos  ──> paises               (RESTRICT) via templo_id, pais_id
```

| Relación | Cardinalidad | Al borrar el padre | Al desactivar / archivar |
|---|---|---|---|
| `casos` → `personas` | 1 a N (0..N) | **`RESTRICT`**: el motor **rechaza** borrar un caso que tenga personas | Archivar el caso (`archivado=1`) **no toca** las personas: siguen ahí y siguen contando en los reportes (FASE 8 crit. 3) |
| `casos` → `asignaciones` | 1 a N | **`RESTRICT`** | Un caso archivado sale de las listas de trabajo; sus asignaciones quedan como registro |
| `companeros` → `asignaciones` | 1 a N | **`RESTRICT`** | Desactivar un compañero (`activo=0`) **no toca** sus asignaciones ni sus verificaciones: FASE 6 crit. 3 exige que «las verificaciones que hizo siguen mostrando su nombre» |
| `casos` → `contactos` | 1 a N | **`RESTRICT`** | Anular un contacto (`anulado=1`) lo deja visible con su motivo; no se borra |
| `companeros` → `procedencia_campo` | 1 a N | **`RESTRICT`** | Ídem: la firma de quién verificó sobrevive a la desactivación |
| `casos`/`personas` → `procedencia_campo` | 1 a N | **sin clave foránea** (polimórfica, §2.7) | — |
| `unidades` → `casos` ⚠️ *propuesto* | 1 a N (0..N) | **`RESTRICT`**: no se borra una unidad que tenga casos | Desactivar una unidad (`activa=0`) **no toca** sus casos: el país que ya se resolvió sigue resolviéndose. Solo desaparece de la lista al enlazar casos nuevos |
| `paises` → `unidades` ⚠️ *propuesto* | 1 a N | **`RESTRICT`** | Desactivar un país no toca sus unidades ni sus casos |
| `paises` → `templos` ⚠️ *propuesto* | 1 a N (0..N) | **`RESTRICT`** | Ídem |
| `templos` → `casos` ⚠️ *propuesto* | 1 a N (0..N) | **`RESTRICT`** | Ídem |

⚠️ **Las cuatro relaciones propuestas siguen la misma regla que las cinco de arriba:
`RESTRICT` en todas, y desactivar en vez de borrar.** Un país o una unidad que se
borrase dejaría casos apuntando a nada, y en este sistema **nada se borra nunca**.

**Por qué `RESTRICT` en todas y no `CASCADE`.** `CASCADE` significa «si borras el
padre, me llevo los hijos en silencio». Este proyecto decidió lo contrario: los casos
**se archivan, nunca se borran**, y no existe operación de borrado de casos ni de
personas (FASE 8). Con `RESTRICT`, esa decisión deja de depender de que nadie escriba
un `DELETE`: **el motor rechaza el borrado**. Es la misma regla, defendida por la
base en vez de por la disciplina.

⚠️ **Y solo funciona con `PRAGMA foreign_keys = ON`** (§1.5). Sin ese pragma, un
`DELETE FROM casos` pasa limpio y deja personas huérfanas. Ese pragma es, en la
práctica, parte de la regla de «no borrar».

---

## 4. Lo único que es único — y cómo sostiene la reconciliación

> ## ⚠️ TODA ESTA SECCIÓN SE LEE CON LA CORRECCIÓN DEL 2026-09-04 DELANTE
>
> **La primera de las dos restricciones encadenadas ya no existe.** La migración
> 12 quitó `UNIQUE (numero_caso)`, medido hoy: `sqlite_autoindex_casos_1` no
> aparece entre los índices de la base (§6). El razonamiento de abajo **se
> conserva entero y sin tocar**, porque explica por qué se creyó que hacía falta;
> lo que dejó de ser cierto es su conclusión.
>
> **Qué sostiene la reconciliación hoy, según `DECISIONES.md` (2026-09-03,
> decisión 4) y NO comprobado por mí en el código de `paquete/reconciliacion.py`:**
> la fila devuelta se resuelve **por `mrn` dentro del caso al que pertenece la
> fila** —la clave visible del Excel ya lleva caso y MRN—, **no por `numero_caso`
> como único, porque ya no lo es**. La restricción que queda en pie y sí sostiene
> eso es la segunda: `UNIQUE (caso_id, mrn)` en `personas`.
>
> ⚠️ **Lo que esto abre, y lo digo porque nadie lo ha medido:** si el Excel que
> vuelve trae **solo** `numero_caso` + `mrn` y dos casos comparten número, ese par
> puede devolver **dos filas** — exactamente el fallo que el punto 1 de abajo
> temía. La decisión del dueño dice que se resuelve «dentro del caso al que
> pertenece la fila», lo que supone que el Excel identifica el **caso**, no solo su
> número. **No he verificado qué lleva de verdad la clave visible del Excel**: es
> `paquete/exportacion.py` y `paquete/reconciliacion.py`, y comprobarlo es de QA.
> **Hasta que alguien lo mida, esto es un riesgo abierto, no un hecho.**

La reconciliación del Excel que vuelve es **por `numero_caso` + `mrn`, nunca por
nombre** (`DECISIONES.md`, FASE 6). Ese par tiene que identificar **como mucho una
fila**, o la fila devuelta actualizaría la equivocada.

En la base, el par no vive en una sola tabla. Lo sostienen **dos restricciones
encadenadas**:

| # | Restricción | Dónde | Qué garantiza |
|---|---|---|---|
| 1 | ~~`UNIQUE (numero_caso)`~~ **RETIRADA el 2026-09-04, migración 12** | `casos` | ~~Un `numero_caso` resuelve a **un solo** `casos.id`~~ **Ya no. Un número resuelve a tantos casos como documentos lo compartan, y en la carpeta real del dueño eso es lo normal** |
| 2 | `UNIQUE (caso_id, mrn)` | `personas` | Dentro de ese caso, un `mrn` resuelve a **una sola** persona |

Encadenadas: `numero_caso` → un `caso_id` → con `mrn`, una `persona`. **El par
identifica una fila o ninguna, nunca dos.** Ese es el requisito, y está cubierto.

**Tres consecuencias que conviene ver antes de firmar:**

1. ~~**`numero_caso` tiene que ser `UNIQUE`, y eso resuelve media D-4.**~~
   ⚠️ **REFUTADO POR EL PAPEL el 2026-09-03, y es el error de razonamiento más
   caro de este documento.** Lo que sigue deducía una propiedad del **mundo** —«el
   número identifica un formulario»— a partir de lo que una decisión de diseño
   **necesitaba** que fuera cierto. No lo era. El número es unidad + `AAMM`, y de
   los diez documentos que el dueño importó en la PC del trabajo, **seis se
   rechazaron** por compartirlo con otra familia. La restricción no protegía la
   reconciliación: **tiraba documentos enteros**. Se conserva el texto porque el
   fallo es la lección — *una restricción del motor no puede fundarse en lo que le
   conviene al código, solo en lo que el papel garantiza*:

   `PENDIENTES.md`
   deja abierto *«¿es `numero_caso` único?»* como decisión del dueño. **La
   reconciliación ya la contestó**: si dos casos pudieran compartir número, el par
   `numero_caso`+`mrn` devolvería dos filas y la regla «actualiza el registro
   existente» no tendría a cuál aplicarse. No es una preferencia técnica, es lo que
   una decisión ya tomada exige. Lo que sigue abierto de D-4 es **qué hacer** cuando
   llega un PDF repetido —reemplazar, rechazar, o preguntar—, que es comportamiento y
   no esquema: **el esquema sostiene las tres** (`INSERT` falla por `UNIQUE`, y el
   programa decide qué hacer con ese fallo). Ver «Preguntas abiertas · P-5».
2. **`mrn` acepta nulo, y el `UNIQUE` sigue siendo correcto.** En SQLite dos `NULL`
   no son iguales entre sí, así que un caso puede tener varias personas con `mrn`
   sin leer sin violar `UNIQUE (caso_id, mrn)`. Es el comportamiento que hace falta:
   esas filas **no pueden reconciliarse** por definición, y la FASE 6 crit. 2 ya dice
   qué hacer con lo que no casa —a la lista de descartados, no se inserta—.
3. **Nada más es único.** Ni `nombre` de persona (dos hermanos pueden repetir), ni
   `nombre` de compañero (§2.4), ni `unidad_numero` (muchos casos comparten unidad).
   El único índice único adicional es el **parcial** de `asignaciones` (§2.5), que no
   es clave de negocio sino una regla de no duplicar asignaciones vivas.

---

## 5. La decisión de las seis casillas

**Seis columnas en `personas`, no tabla hija.** Decidida, argumentada y con su coste
en **`docs/adr/ADR-0001-casillas-de-ordenanzas.md`**.

En una línea: las seis ordenanzas son un conjunto **fijo, conocido y no volátil** —van
impresas en el formulario—, la FASE 2 crit. 11b exige un tercer estado *«no leída»*
que una tabla hija confunde con *«no marcada»*, y la tabla de procedencia se indexa
por **nombre de campo**, que las seis columnas dan gratis y una tabla hija obligaría a
fabricar.

---

## 5bis. Las columnas del Excel del paquete — comprobadas contra el esquema

El dueño pidió que el Excel del paquete lleve *«su nombre, todos los nombres y
barrios, cédula de miembro y fecha de viaje»*. **Las cinco ya están.** Medido en
`paquete/columnas.py`, que es la única definición y sirve a la vez para el Excel que
sale y para el que vuelve:

| Lo que pidió el dueño | Columna del esquema | Título en la hoja | ¿Existe hoy? |
|---|---|---|---|
| «su nombre» / «todos los nombres» | `personas.nombre` | Nombre | **Sí**, editable |
| «barrios» | `casos.unidad_nombre` | Unidad | **Sí**, bloqueada |
| «cédula de miembro» | `personas.mrn` | MRN | **Sí**, bloqueada (es media clave de reconciliación) |
| «fecha de viaje» | `casos.fecha_viaje` | Fecha de viaje | **Sí**, bloqueada |
| — | `casos.numero_caso` | N.º de caso | **Sí**, bloqueada (la otra media clave) |

Además van `personas.fila_formulario`, `estado_recomendacion` y `nota`, que son las
dos que el compañero rellena.

### Las dos equivalencias que el pase me mandó comprobar, no dar por buenas

**«cédula de miembro» = `mrn`. ✅ Comprobado.** No por parecido: el extractor busca en
el papel la etiqueta literal **`"Membership Record Number"`**
(`extraccion/personas.py`:22), y el número que sale de esa columna es el que se guarda
en `personas.mrn`. Y por fuera del repositorio, la ayuda de FamilySearch
(<https://www.familysearch.org/en/help/helpcenter/article/where-can-i-find-my-church-membership-record-number-mrn>,
consultada **2026-09-02**) describe el *membership record number* como el número que
identifica al miembro y que aparece en su recomendación al templo. Las dos cosas
concuerdan: es la cédula de miembro.

**«barrio» = `unidad_nombre`. ✅ La columna es ésa, ⚠️ pero la palabra se queda
corta.** La etiqueta del papel es **`"Ward/Branch Name and Unit Number"`**
(`extraccion/formulario.py`:42) — **una sola etiqueta para dos clases de unidad**. Y
los dos valores reales que hay medidos en este proyecto son **`Castries Branch`** y
**`Paramaribo Branch`** (`DECISIONES.md`): los dos son *Branch*, **ninguno es un
Ward**. Así que la columna correcta es `unidad_nombre`, pero llamarla «barrio» en la
interfaz describiría mal justo los dos casos que tenemos delante.

**Recomiendo que la cabecera siga diciendo «Unidad»**, que es la palabra que cubre las
dos. ⚠️ **Lo que NO encontré:** una definición oficial y citable que fije *Ward* =
barrio y *Branch* = rama en español. Busqué en el índice del Manual General
(secciones de abreviaturas y términos, en inglés y en español, consultadas
**2026-09-02**) y **no trae definiciones de esos términos**, solo los usa. Lo digo
como hueco, no lo relleno: la recomendación de arriba se sostiene sola sobre los dos
valores medidos, sin necesitar esa cita.

### Lo que le falta al Excel del paquete si entra el país

Si el dueño acepta §2bis, la hoja gana **una columna más, bloqueada**: el país,
resuelto vía `casos.unidad_id → unidades.pais_id → paises.nombre`. Bloqueada por lo
mismo que `unidad_nombre`: el compañero la lee para ordenar su trabajo, no la corrige.
⚠️ Y con ella hay que tocar `espejo/hojas.py`, que hoy escribe las ~~12~~ **13**
columnas de `casos` una a una — **si se añaden columnas a `casos` y nadie las añade
allí, el Excel espejo se queda callado sobre ellas**, que es exactamente lo que ya
pasó con `pudo_viajar` y `motivo_no_viajo` desde la versión 6 (`DECISIONES.md`, cierre
del 2026-09-02). **Corregido el 2026-09-03:** medido en `espejo/hojas.py:112-130`, la
hoja de `casos` lista hoy **13** columnas e incluye `templo_nombre`, así que por esa
puerta la versión 10 sí entró. Por la de §2.9 **no**: `filas_descartadas` no tiene
hoja en el espejo.

---

## 6. Índices

Además de los que crean `PRIMARY KEY` y `UNIQUE`:

| Índice | Sobre | Para qué |
|---|---|---|
| `idx_casos_viaje_activos` | `casos (fecha_viaje) WHERE archivado = 0` | La ventana de 7 días de la FASE 5. **Parcial**: los archivados no salen en ese bloque (FASE 8 crit. 1), así que no tienen por qué ocupar el índice |
| `idx_personas_caso` | `personas (caso_id)` | Traer las personas de un caso, que es toda la FASE 3 |
| `idx_procedencia_registro` | `procedencia_campo (tabla, registro_id)` | Traer la procedencia de todos los campos de un registro (FASE 3 crit. 1 y 2) |
| `idx_asignaciones_companero` | `asignaciones (companero_id) WHERE activa = 1` | El paquete de un compañero (FASE 6 crit. 5) |
| `idx_ilegibles_ruta` | `documentos_ilegibles (ruta_pdf)` | **Añadido el 2026-09-03** (migración 8). La pregunta natural sobre esa tabla es «¿qué pasó con *este* archivo?» (§2.8) |
| `idx_descartadas_companero` | `filas_descartadas (companero_id)` | **Añadido el 2026-09-03** (migración 11). Los descartes de la vuelta de **un** compañero (§2.9) |
| `idx_casos_numero` | `casos (numero_caso)` | **Añadido el 2026-09-04** (migración 12), y **es el único de esta tabla que sí tiene un motivo medido**. No es una optimización nueva: **repone lo que el `UNIQUE` daba gratis**. Al dejar `numero_caso` de ser único, buscar un caso por su número pasaría a recorrer la tabla entera, y esa búsqueda ocurre **una vez por fila** en la vuelta del Excel del compañero y en cada comprobación de duplicado. Su programador lo dejó escrito en `datos/migraciones_de_importacion.py`. **NO es único**: dos casos pueden repetir número, que es el punto entero de la migración 12 |

**El recuento, ~~corregido el 2026-09-03~~ corregido otra vez el 2026-09-04 y medido
sobre `sqlite_master`.** El código crea **ocho** índices propios: los **siete** de la
tabla de arriba más `idx_asignacion_viva`, que no está aquí porque no es un índice de
consulta sino la restricción de §2.5. No se cuentan los automáticos de
`UNIQUE`/`PRIMARY KEY`. ~~Este documento declaraba cinco: faltaban los dos de las
versiones 8 y 11.~~ **El 2026-09-03 declaraba cinco y faltaban los de las versiones 8
y 11; el 2026-09-04 declaraba siete y faltaba el de la versión 12.**

⚠️ **Y un índice automático que este documento nombraba ha dejado de existir.**
`sqlite_autoindex_casos_1` era el del `UNIQUE (numero_caso)` que la migración 12
quitó. Medido el 2026-09-04 preguntando por **todos** los índices, sin filtrar los
automáticos:

```
idx_asignacion_viva -> asignaciones        idx_asignaciones_companero -> asignaciones
idx_casos_numero -> casos                  idx_casos_viaje_activos -> casos
idx_descartadas_companero -> filas_descartadas
idx_ilegibles_ruta -> documentos_ilegibles
idx_personas_caso -> personas              idx_procedencia_registro -> procedencia_campo
sqlite_autoindex_personas_1 -> personas
sqlite_autoindex_procedencia_campo_1 -> procedencia_campo
```

**Diez en total: ocho propios y dos automáticos.** El de `casos` no está.

⚠️ **Ninguno de estos índices está justificado por una medición.** No hay base, no
hay datos y no hay consulta que cronometrar. Se proponen por la forma de las
consultas que las fases describen, no por un plan de ejecución observado. **Si la
FASE 5 mide y no los necesita, sobran y se quitan**: un índice que nadie usa es
escritura más lenta a cambio de nada. Ver «Qué NO cubre esto».

---

## 6bis. De dónde sale el país de un caso — y qué pasa cuando no se sabe

**Sale de la unidad. Nunca del número de caso.** Y el vínculo unidad → país lo
establece Miguel **una sola vez por unidad**, no una vez por caso.

```
casos.unidad_numero   ──(coincidencia exacta)──>   unidades.unidad_numero
                                                   unidades.pais_id  ──>  paises
      (lo que dice el papel)                              (lo que dijo Miguel, una vez)
```

### 6bis.1 Por qué NO se deduce del número de caso

El pase avisaba: *«No des por hecho que el prefijo codifica el país — parece que sí,
pero eso hay que comprobarlo, y con cuatro documentos no alcanza.»* Se comprobó hasta
donde se pudo, y el resultado es **no**.

| # | Lo que se midió o consultó | Qué dice |
|---|---|---|
| 1 | `DECISIONES.md`:275-278 — los prefijos de los cuatro documentos | `CASP`, `PARB`, `SURB`. **Son nombres de archivo.** No he verificado que el `numero_caso` de dentro del PDF sea el mismo (no puedo abrirlos) |
| 2 | `DECISIONES.md`:317-318 — la unidad medida del documento `CASP…` | `Castries Branch - 700001`. `CASP` empareja con **Castries**, que es una unidad, no un país |
| 3 | Wikipedia, «Castries», consultada **2026-09-02**, primera frase literal: «Castries is the capital and largest city of Saint Lucia, an island country in the Caribbean.» <https://en.wikipedia.org/wiki/Castries> | Castries está en **Santa Lucía** |
| 4 | Wikipedia, «Paramaribo», consultada **2026-09-02**, primera frase literal: «Paramaribo, locally nicknamed Foto, is the capital and largest city of Suriname, located on the banks of the Suriname River in the Paramaribo District.» <https://en.wikipedia.org/wiki/Paramaribo> | Paramaribo está en **Surinam** |
| 5 | Búsqueda web del 2026-09-02 por el formato del número de caso de este formulario | **No encontré evidencia consultable** de que el prefijo de 4 letras sea un código estandarizado de país ni de unidad. Lo que sí devolvió son páginas sobre recomendaciones al templo y sobre el MRN; ninguna describe un «case number» |

**El razonamiento que cierra el punto 3+4:** `PARB` apunta a Paramaribo y `SURB` a
Surinam, y **Paramaribo está en Surinam**. Si el prefijo fuera el país, dos prefijos
distintos darían el mismo país — así que el prefijo, en el mejor de los casos,
identifica **la unidad o el lugar**, no el país. Y aun eso es una hipótesis sobre
**tres prefijos distintos**: una regla inferida de tres puntos que decide de qué color
se pinta la nacionalidad de una persona es exactamente la clase de adivinanza que
prohíbe la **regla permanente 1**.

⚠️ ~~**Lo que NO pude comprobar y hay que decir:** si el `numero_caso` extraído coincide
con el prefijo del nombre del archivo. Es una pregunta al dueño; ver P-9.~~

✅ **Comprobado, y la respuesta refuerza esta sección** *(corregido el 2026-09-03; lo
midió el supervisor el 2026-09-02, no yo)*: **NO coincide** en 1 de los 2 documentos
comprobados — el archivo llamado `CASP2609_Jonas_Ficticio.pdf` lleva **`CASD2609`** en el
papel, leído por OCR con confianza 0.99995 (`DECISIONES.md`:503-514). Es un argumento
más, y del bueno, para **no deducir nada del nombre del archivo**: la única fuente es
lo que dice el papel. Ver P-9, ya cerrada.

### 6bis.2 Por qué tampoco lo teclea Miguel caso por caso

Podría. Es la alternativa más barata de construir y está descartada en el ADR-0002 con
su motivo: el país de un caso **no es un dato del caso**, es un dato de la unidad. Un
dato que se teclea N veces se escribe distinto en alguna de esas N, y cada variante es
un color más.

### 6bis.3 Cómo se propone el vínculo — y qué NO hace el programa

Al importar, y también al abrir un caso sin `unidad_id`:

1. Si existe una fila en `unidades` con **exactamente** el mismo `unidad_numero`, el
   programa la **propone**. Miguel confirma con un botón. `unidad_id` no se escribe
   solo: **regla permanente 5**, el sistema propone y Miguel confirma.
2. Si no existe ninguna, el programa **no propone nada** y ofrece la lista completa
   más «dar de alta una unidad nueva».
3. **La coincidencia es por `unidad_numero`, nunca por `unidad_nombre`.** Es el mismo
   criterio que ya rige la reconciliación del Excel (`DECISIONES.md`, FASE 6: «nunca
   por nombre: un acento de más crea un registro fantasma»). El nombre es texto de
   OCR: `Castries Branch` y `Castries Bnanch` son la misma unidad y cadenas distintas.
4. ⛔ **No hay emparejamiento por parecido de nombre.** Es donde se cuela el dato
   inventado: un `0,87` de similitud entre dos nombres de unidad no es una unidad.

### 6bis.4 Qué pasa cuando la deducción falla — porque va a fallar

Los cuatro modos de fallo, y qué hace el programa en cada uno. **En los cuatro la
respuesta es la misma: se queda en `NULL`, se ve, y nadie inventa nada.**

| Modo de fallo | Con qué frecuencia se espera | Qué hace el programa |
|---|---|---|
| **La unidad no está todavía en `unidades`** (primer caso de una unidad nueva) | Siempre, la primera vez de cada unidad | `unidad_id = NULL`. El caso se guarda entero. Sale con el **color neutro de «país sin asignar»** y entra en la lista «Casos sin país» de la pantalla de inicio |
| **El OCR no leyó `unidad_numero`, o lo leyó mal** | Medido que pasa: 4 de 9 páginas traían 7 dígitos y el `CHECK` viejo las dejaba con el campo **vacío** | Igual: `unidad_id = NULL` y a la lista. El campo `unidad_numero` ya está marcado en `procedencia_campo` para revisión |
| **Miguel enlaza la unidad equivocada** | Ocurrirá | Se puede reenlazar. `unidad_numero` y `unidad_nombre` **nunca se tocaron**, así que el error es visible y reversible. Ésta es la razón de la pareja crudo/resuelto de §2bis.4 |
| **Dos unidades del mismo país** | Normal | No es un fallo: las dos filas apuntan al mismo `pais_id` |

⛔ **Lo que el programa NO hace en ninguno de los cuatro casos:** adivinar el país por
el prefijo, por el nombre parecido, ni por «el país más frecuente». Un caso sin país
**se ve**; un caso con el país equivocado, no.

**Y una consecuencia que hay que aceptar antes de firmar:** el día 1, con `paises` y
`unidades` vacías, **todos** los casos salen sin país. El color por país no aparece
hasta que Miguel da de alta los países y enlaza las unidades. Eso no es un defecto del
diseño: es que el dato no existía y nadie lo va a inventar por él.

---

## 6ter. El resumen por agente de la pantalla de inicio

El encargo, literal: *«en inicio debe decirme qué agente tiene cuántas personas para
verificar y documentos también»*. Y concretado después por el dueño con nombres:

> *«ejemplo en inicio, Sandy y Junier: si yo le asigno 8 documentos a Junier y 5 a
> Sandy, debe decirme cuántos documentos tiene Sandy y cuántas personas hay en los
> documentos»*

**Son dos números por compañero, y no se derivan el uno del otro.** 8 documentos no
son 8 personas ni 16: un formulario de grupo trae hasta doce personas en una sola
hoja. Medido, sobre un PDF real: `SURB2609_Suriname_Group_Complete.pdf` tiene **6
páginas con 12 personas distintas** (`DECISIONES.md`, 2026-09-02).

> ⚠️ **Una premisa del pase ampliado que NO se sostiene, medida antes de construir
> encima.** El pase daba por medido que
> `grep -n "COUNT" datos/companeros.py datos/asignaciones.py` **no devuelve nada**.
> Devuelve una línea:
>
> ```
> $ grep -n "COUNT" datos/companeros.py datos/asignaciones.py
> datos/asignaciones.py:132:        "       (SELECT COUNT(*) FROM personas p WHERE p.caso_id = c.id) AS personas "
> ```
>
> Es decir: **la cuenta de personas por caso ya existe**, dentro de `casos_asignados`,
> y ya viaja al paquete. Lo que no existe es el **agregado por compañero**. La
> conclusión del pase —«esa consulta no existe todavía»— sigue siendo cierta para el
> agregado; el camino es más corto de lo que parecía, porque la subconsulta que cuenta
> personas ya está escrita y probada en otro sitio.

### 6ter.1 ¿Bastan `companeros` y `asignaciones`? Sí. Cero columnas nuevas

Medido: `asignaciones (caso_id, companero_id, activa)` + `personas (caso_id)` +
`casos (archivado)` responden las dos cuentas. **No hace falta ni una columna ni un
índice nuevos.**

- **«documentos»** = casos vivos asignados a ese compañero, sin archivar.
  Un caso **es** un formulario, y el paquete que se le entrega escribe exactamente un
  PDF por caso (`{numero_caso}.pdf`, en `paquete/exportacion.py`). Así que «documento»
  y «caso» son la misma cosa contada desde el lado del compañero.
  ⚠️ Es **mi lectura**, no una definición del dueño. Ver P-8.
- **«personas para verificar»** = las personas de esos casos.

### 6ter.2 La consulta

Como las expresiones `CHECK` de §2: esto es **la especificación de la consulta**, no
un archivo `.sql`. Lo escribe el programador.

```
SELECT co.id, co.nombre, co.activo,
       COUNT(DISTINCT c.id)                          AS documentos,
       COUNT(p.id)                                   AS personas,
       COUNT(CASE WHEN p.id IS NOT NULL
                   AND p.estado_propuesto IS NULL
                  THEN 1 END)                        AS personas_sin_propuesta
FROM companeros co
LEFT JOIN asignaciones a ON a.companero_id = co.id AND a.activa = 1
LEFT JOIN casos       c ON c.id = a.caso_id        AND c.archivado = 0
LEFT JOIN personas    p ON p.caso_id = c.id
WHERE co.activo = 1 OR c.id IS NOT NULL
GROUP BY co.id, co.nombre, co.activo
ORDER BY personas DESC, co.nombre, co.id
```

**Las seis decisiones que lleva dentro, cada una con su motivo:**

1. **`LEFT JOIN` y no `JOIN`.** Un compañero con cero casos tiene que salir con un
   `0`. Es la mitad del valor de esta pantalla: Miguel mira esto para saber **a quién
   le puede dar trabajo**, y quien no aparece no recibe nada.
2. **`a.activa = 1` en el `ON`, no en el `WHERE`.** Puesto en el `WHERE` anularía el
   `LEFT JOIN` y el compañero sin casos desaparecería de la lista. Es el error clásico
   de esta consulta.
3. **`c.archivado = 0`, también en el `ON`.** Un caso archivado ya no es trabajo. Es
   el mismo filtro que llevan las cuatro consultas de la FASE 5 y `datos/pendientes.py`
   (`PENDIENTES.md`, FASE 5 crit. 6).
4. **`COUNT(DISTINCT c.id)` para los documentos.** El `JOIN` con `personas` multiplica
   cada caso por sus personas; un `COUNT(c.id)` a secas diría que un caso de cinco
   personas son cinco documentos.
5. **`p.id IS NOT NULL` dentro del `CASE`.** ⚠️ **Esto es un defecto medido, no una
   precaución teórica.** Sin ese guardia, un compañero con cero casos cuenta **1**
   persona sin propuesta, porque el `LEFT JOIN` le fabrica una fila con `p.*` a `NULL`
   y `p.estado_propuesto IS NULL` es cierta sobre ella. Salida real de las dos
   versiones, sobre el mismo juego de datos:

   ```
   sin el guardia:   {'nombre':'Ana',...,'personas':5,'personas_sin_propuesta':5}   <- mal
                     {'nombre':'Beto',...,'personas':0,'personas_sin_propuesta':1}  <- mal
   con el guardia:   {'nombre':'Ana',...,'personas':5,'personas_sin_propuesta':4}   <- bien
                     {'nombre':'Beto',...,'personas':0,'personas_sin_propuesta':0}  <- bien
   ```

6. **`WHERE co.activo = 1 OR c.id IS NOT NULL`.** Un compañero desactivado que
   **todavía tiene casos vivos** sigue apareciendo, marcado como desactivado. Un
   `WHERE co.activo = 1` a secas haría desaparecer de la pantalla su trabajo sin
   terminar, y ese trabajo es de personas reales que viajan. La columna `co.activo`
   sale en el resultado justamente para que la pantalla lo pinte distinto.

### 6ter.3 Medido contra el motor

Juego de datos: 4 casos (uno archivado, con 5 personas), personas 3/2/1/5,
3 compañeros, una asignación viva a un caso archivado y una asignación retirada.

```
{'id': 1, 'nombre': 'Ana',  'activo': 1, 'documentos': 2, 'personas': 5, 'personas_sin_propuesta': 4}
{'id': 2, 'nombre': 'Beto', 'activo': 1, 'documentos': 0, 'personas': 0, 'personas_sin_propuesta': 0}
{'id': 3, 'nombre': 'Caro', 'activo': 1, 'documentos': 0, 'personas': 0, 'personas_sin_propuesta': 0}
```

Los tres comportamientos que había que comprobar salen bien: **el caso archivado no
cuenta** (Ana tiene 2 documentos y no 3, y 5 personas y no 10), **la asignación
retirada no cuenta** (Beto sale a 0) y **el compañero sin nada aparece igual**.

`EXPLAIN QUERY PLAN` sobre la misma consulta:

```
SCAN co
SEARCH a USING INDEX idx_asignaciones_companero (companero_id=?) LEFT-JOIN
SEARCH c USING INTEGER PRIMARY KEY (rowid=?) LEFT-JOIN
SEARCH p USING INDEX idx_personas_caso (caso_id=?) LEFT-JOIN
USE TEMP B-TREE FOR count(DISTINCT)
USE TEMP B-TREE FOR ORDER BY
```

**Usa los dos índices que ya existen** —`idx_asignaciones_companero` y
`idx_personas_caso`— y solo barre `companeros`, que tiene tantas filas como personas
hay en el equipo. **Confirmado: ningún índice nuevo.**

### 6ter.4 Las tres cuentas que NO son la misma, y cuál va en la tarjeta

Esto es un hueco del encargo, no una elección mía. «Para verificar» admite tres
lecturas y **cada una cuenta una cosa distinta**:

| Cuenta | Qué mide | De quién es el trabajo | Ya existe |
|---|---|---|---|
| `personas` | el **tamaño** de lo que se le encargó | — | esta consulta |
| `personas_sin_propuesta` | lo que **le falta al compañero** por contestar | del compañero | esta consulta |
| campos sin verificar | lo que **le falta a Miguel** por confirmar | de Miguel | `datos/pendientes.py`, ya construido |

Las dos primeras salen de esta consulta sin coste extra. **Cuál de las dos es el
número grande de la tarjeta lo decide el dueño:** ver P-8.

### 6ter.5 «Documento»: ¿el archivo, el caso, o la hoja? — las tres son cosas distintas

El dueño dice «documentos». El esquema no tiene esa palabra: lo que se asigna es un
**caso**. Las tres lecturas posibles, con lo que cada una vale hoy:

| Lectura | En el esquema | ¿Coincide con lo que se asigna? |
|---|---|---|
| **El caso** — un formulario de recomendación | `casos.id` — es lo que lleva `asignaciones.caso_id` | **Sí, exactamente** |
| **El archivo PDF** de origen | `casos.ruta_pdf` | No siempre. Un archivo puede contener varios casos |
| **La hoja** del PDF | `casos.pagina_pdf` (la que abrió el caso) y `personas.pagina_pdf` | No. Un caso puede repartirse en varias hojas |

**Recomiendo «documento» = caso**, y por tres motivos concretos:

1. **Es lo que se asigna.** `asignaciones` apunta a `caso_id`. Contar otra cosa
   obligaría a que el número de la pantalla no case con lo que Miguel acaba de repartir.
2. **Es lo que se entrega.** El paquete del compañero escribe **un PDF por caso** —
   `{numero_caso}.pdf`, en `paquete/exportacion.py` — así que «8 documentos» en la
   pantalla se convierten en 8 archivos dentro de su carpeta. Los dos números casan.
3. **Es lo que el compañero abre y contesta.** El Excel de trabajo lleva una fila por
   persona agrupada por `numero_caso`.

Sobre el ejemplo del dueño: si a Junier se le asignan 8 casos y a Sandy 5, la pantalla
dice **8** y **5**, y al lado las personas que suman —que pueden ser 9 y 41 sin que
nada esté mal.

> ⚠️ **Y una premisa del pase ampliado que no puedo confirmar.** El pase afirma que
> «un PDF puede traer varios casos». Es **posible por diseño** —el extractor trabaja
> por página y agrupa por `numero_caso`—, pero **entre los cuatro documentos de
> referencia no hay ninguno que lo demuestre**: los cuatro son un archivo con un solo
> prefijo, y el de seis páginas se une en **un** caso por la opción A de
> `DECISIONES.md`. Lo dejo como posible-y-no-observado, no como medido. Si resultara
> que **sí** ocurre a menudo, la lectura «documento = archivo» ganaría peso y esto
> vuelve a preguntarse. Ver P-8.

### 6ter.6 Lo que el botón de «generar paquete» necesita de esta consulta

El botón va al lado del renglón de cada compañero; **la pantalla es del diseñador** y
aquí solo se dice qué datos le tiene que dar la consulta.

| Lo que el botón necesita | De dónde sale |
|---|---|
| A quién se le genera | `co.id` — es el único argumento de `exportar_paquete(conexion, companero_id, carpeta_destino)` |
| Si se puede pulsar | `documentos > 0`. `exportar_paquete` **levanta** `ErrorDeValidacion` cuando el compañero no tiene ningún caso asignado; un botón activo que solo sabe dar un error no es un botón |
| Qué avisar antes de pulsar | `co.activo`. A un compañero desactivado no se le puede asignar trabajo nuevo, pero **sí** se le puede generar el paquete de lo que ya llevaba |

⛔ **Y un desajuste que hay que arreglar antes de poner el botón, o el número mentirá.**
La consulta de §6ter.2 excluye los casos archivados (`c.archivado = 0`). **El paquete
no los excluye.** Medido leyendo la única instrucción de `casos_asignados`
(`datos/asignaciones.py`:128-137): su `WHERE` es

```
WHERE a.companero_id = ? AND a.activa = 1
```

`c.archivado` aparece en la lista del `SELECT` (línea 131) y **en ninguna condición**.
Consecuencia: la tarjeta diría «2 documentos» y el botón escribiría **3 carpetas de
PDF**, una de ellas de un caso ya archivado. Las dos consultas tienen que estar de
acuerdo sobre qué es trabajo vivo.

**Recomiendo que `casos_asignados` filtre `c.archivado = 0`**, por coherencia con las
cuatro consultas de la FASE 5 y con `datos/pendientes.py`, que ya lo llevan escrito
desde el primer día. **No lo cambio yo: no escribo código.** Queda como defecto
señalado para el programador, y es suyo decidir si el paquete debe poder incluir un
caso archivado a propósito.

---

## 7. Preguntas abiertas para el dueño

Ninguna se rellena a ojo. Cada una dice qué parte del esquema queda pendiente.

> ### Revisión del 2026-09-04 — las ocho abiertas, una por una, contra lo decidido el 03 y el 04
>
> La ronda anterior dejó **ocho** abiertas: P-5, P-7, P-8, P-10, P-11, P-12, P-13
> y P-14. Las he cotejado **una a una** contra todas las entradas de
> `DECISIONES.md` de los días 3 y 4 y contra el esquema medido hoy. El resultado
> **no es que sigan ocho**:
>
> | | Veredicto del 2026-09-04 | Con qué se coteja |
> |---|---|---|
> | **P-5** | ✅ **CERRADA por el dueño el 2026-09-03, y ya construida.** *«Si hay documentos duplicados debe decirlo y no rechazarlo.»* Ninguna de las tres opciones que planteaba: **entra y se marca**. La columna se llama `casos.duplicado_de` (migración 13), verificada hoy en el volcado | `DECISIONES.md`, «P-5, decidida por el dueño»; §2.2 |
> | **P-7** | ⚠️ **Sigue abierta, sin tocar.** Nadie ha dicho si «el periodo» del reporte va sobre `fecha_viaje` o sobre `creado_en`. Ninguna entrada del 3 ni del 4 lo menciona | — |
> | **P-8** | ⚠️ **Se estrecha, no se cierra.** La **segunda** mitad —«¿documento quiere decir caso?»— la contestó el código de hecho: `datos/equipo.py` cuenta `COUNT(c.id)` sobre `casos`, que es lo que este documento recomendaba. La **primera** —qué número va grande en la tarjeta— **sigue sin respuesta**, pero **ya no bloquea nada**: el resumen entrega `documentos`, `sin_devolver` y `personas`, así que la pantalla puede enseñar la que el dueño prefiera sin tocar el esquema. Baja de pregunta de esquema a preferencia de interfaz | `datos/equipo.py`; `DECISIONES.md`, mockups v2, decisión 3 |
> | **P-10** | ⚠️ **Abierta, y ahora bloquea algo más.** Sigue siendo del dueño, y de ella cuelga que **un templo mal leído no se pueda corregir a mano** — el `Caracas-Venezueia` de §2.2. Ver `PENDIENTES.md` · DC-6 | §2.2 |
> | **P-11** | ⚠️ **Abierta, medida y confirmada.** Dos compañeros distintos con asignación viva sobre el mismo caso → **aceptado, `COUNT = 2`**. La regla «un caso, un compañero» **sigue sin estar escrita en ningún documento del proyecto** | §2.5; `DECISIONES.md`, 2026-09-03 |
> | **P-12** | ⚠️ **Abierta, sin tocar.** ¿La unidad es del caso o de la persona? Ninguna entrada del 3 ni del 4 la roza | — |
> | **P-13** | ⚠️ **Abierta, sin tocar**, y con la misma forma que P-15: las dos preguntan **quién firma** cuando dos personas dicen cosas distintas sobre el mismo dato. **Conviene contestarlas juntas** | §2.3 |
> | **P-14** | ⚠️ **Abierta, y P-5 ya no la bloquea.** Decía «no se decide sin P-5, y si P-5 dice rechazar, ésta no llega a darse». **P-5 dijo lo contrario —entrar y marcar—**, así que este caso **sí se da**: un ilegible reprocesado deja hoy un renglón nuevo. La pregunta pasa de condicional a real | §2.8 |
>
> **Y dos nuevas de esta ronda: P-15 y P-16**, las dos de la deuda que devolvieron
> los programadores del ciclo 5.
>
> **La cuenta correcta al 2026-09-04 es: siete abiertas** —P-7, P-8 (estrechada a
> interfaz), P-10, P-11, P-12, P-13, P-14— **más dos nuevas, P-15 y P-16. Nueve
> en total, y P-5 cae.** Ninguna se rellena aquí.

> **Estado de las siete primeras, al 2026-09-02.** Cinco se cerraron en
> `DECISIONES.md` («Las siete preguntas del esquema», 2026-09-02) y se dejan aquí
> tachadas con su respuesta, no borradas. Las preguntas **nuevas** de esta puesta al
> día empiezan en P-8.
>
> | | Estado | Dónde se resolvió |
> |---|---|---|
> | P-1 | ✅ **Resuelta** — columna guardada, sin `CHECK`; la lista vive en `datos/estados.py`. ⚠️ **Falta el valor que significa «resuelta»** | `DECISIONES.md`, P-1 |
> | P-2 | ✅ **Resuelta** — es **aviso**, no pared del motor | `DECISIONES.md`, P-2 |
> | P-3 | ✅ **Resuelta** — se llama `manual` | `DECISIONES.md`, P-3 |
> | P-4 | ✅ **Resuelta** — se **desactivan**, no se borran | `DECISIONES.md`, P-4 |
> | P-5 | ⚠️ **Abierta.** ~~Y más grave de lo que decía. Ver P-9~~ **Corregido el 2026-09-03:** P-9 quedó cerrada y no la agrava. P-5 sigue abierta **por sí sola** —qué pasa al reprocesar un PDF ya importado— y ahora tiene una hermana menor, P-14 | — |
> | P-6 | ✅ **Resuelta** — van en `casos` | `DECISIONES.md`, P-6 |
> | P-7 | ⚠️ **Abierta.** Nadie ha dicho de qué fecha es «el periodo» | — |

### ~~P-1.~~ `estado_recomendacion`: ¿existe, y con qué valores? — ✅ RESUELTA, con un resto

**Lo medido (arriba):** ni el nombre del campo ni uno solo de sus valores aparecen en
los 65 archivos del repositorio. `no_indicada` → 0 coincidencias. La descripción del
pase —«lista desplegable con los cuatro valores»— no tiene respaldo aquí.

**Lo único que el proyecto sí exige** es la FASE 5 crit. 4: distinguir un caso con la
recomendación **completa** de uno **incompleta**. Eso admite tres formas muy
distintas, y la elección cambia el esquema:

| Forma | Qué haría falta | Coste |
|---|---|---|
| **A. Columna con lista cerrada** | `estado_recomendacion TEXT` con `CHECK (… IN (…))` | Hay que enumerar los valores. **Hoy no se puede: no hay ninguno escrito** |
| **B. Booleano `completa`** | Una columna 0/1 que Miguel marca | Simple, pero pierde el matiz de *por qué* está incompleta |
| **C. Derivado, sin columna** | «Incompleta» = le falta algún campo obligatorio o algún campo sin verificar; se calcula con un `SELECT` | **Cero columnas nuevas.** No puede desincronizarse del dato. A cambio, «obligatorio» hay que definirlo |

**Recomiendo C si la distinción es puramente «le falta algo»**, porque un estado
guardado a mano se queda viejo en cuanto alguien rellena el campo que faltaba, y
entonces la pantalla roja de la FASE 5 miente. **Recomiendo A si «estado» significa
algo que el formulario trae impreso** y que el programa solo transcribe — en cuyo
caso hacen falta los valores, y solo el dueño los tiene.

**Qué queda pendiente:** una columna de `casos`. Todo lo demás del esquema está
cerrado sin ella, y la FASE 1 puede arrancar hoy.

### P-2. La regla del mes cruzado, ¿es una pared o un aviso? ⛔ *cambia un `CHECK`*

`DECISIONES.md` la da como regla de formato y la FASE 1 crit. 6 la mide como
rechazo. Implementada como `CHECK` (§2.2), hace **imposible guardar** un caso
`CASP2609` cuyo viaje se reprogramó a octubre.

Ese caso no es hipotético: el ejemplo del propio plan es una fecha **corregida a
mano sobre el formulario** (7 → 8 de septiembre). Corregir dentro del mes cabe;
corregir al mes siguiente, no.

- **Si es pared** (lo escrito hoy): se queda el `CHECK`. Un formulario reprogramado
  a otro mes **no se puede guardar** y hay que decidir a mano qué se hace con él.
- **Si es aviso**: se quita el `CHECK`, la validación se queda solo en Python y el
  caso se guarda **marcado para revisión**, que es como el programa trata todo lo
  demás dudoso.

**Recomiendo aviso**, por coherencia con el resto del diseño: este programa existe
para **enseñarle a Miguel lo que está raro**, no para negarse a registrarlo. Un dato
que el sistema rechaza es un dato que nadie vuelve a mirar. **Pero es del dueño**,
porque la regla es suya y la escribió como formato.

### P-3. ¿Cómo se llama el origen de un campo corregido a mano? *(menor)*

Ningún documento lo nombra. He puesto `manual` en la lista de `origen` (§2.7) porque
el sistema **tiene** que distinguir un valor que puso Miguel de uno que leyó el OCR
—si no, `valor_ocr` no tiene con qué contrastarse—. Si el dueño prefiere otra
palabra, es cambiar una cadena en un `CHECK`. **Lo digo porque es mío, no del
material.**

### P-4. Al retirarle un caso a un compañero, ¿se borra la asignación o se desactiva? *(menor)*

`DECISIONES.md` dice que **los compañeros** se desactivan; de las **asignaciones** no
dice nada, y la FASE 8 solo protege casos y personas. He elegido desactivar (§2.5)
por coherencia. Si se prefiere borrar, se quita `activa`/`desactivada_en` y el índice
único parcial pasa a ser un `UNIQUE` normal.

### ~~P-5.~~ Un PDF procesado dos veces: ¿reemplazar, rechazar o preguntar? — ✅ RESUELTA por el dueño el 2026-09-03, con un resto

> ✅ **CERRADA el 2026-09-04 en este documento.** `DECISIONES.md` la resolvió el
> 2026-09-03 y este documento seguía listándola abierta — el propio supervisor lo
> anotó entonces: *«`docs/ARQUITECTURA.md` §7 sigue listando P-5 como abierta: lo
> corrige el planificador cuando el código aterrice y se sepa el nombre real de la
> columna»*. **El código aterrizó en `2b6e92a` y la columna se llama
> `casos.duplicado_de`**, verificada hoy en el volcado de §2.2.
>
> **La respuesta del dueño, literal:** *«Si hay documentos duplicados debe decirlo
> y no rechazarlo.»* **Ninguna de las tres opciones que esta pregunta ofrecía.**
> Lo que se construyó:
>
> - Un archivo ya importado —misma ruta, o mismas personas por MRN dentro del
>   mismo número— **entra igual** y crea su caso, marcado con el caso del que es
>   duplicado.
> - **Pisar sigue prohibido.** El caso que ya estaba, con sus correcciones a mano y
>   sus firmas, no se toca. **Nada se fusiona solo**: Miguel decide.
> - Regla permanente 5 intacta en los dos: nada se firma solo.
>
> ⚠️ **El resto que NO cierra, y por eso esto es un tachado y no un borrado.** La
> detección va por la **ruta** del archivo y por los **MRN**. Medido hoy en
> `importacion/guardado.py:501-504`, literal: *«No es una huella del contenido: es
> la ruta … la base no guarda ninguna huella del archivo y este pase no la
> anade.»* Un archivo **copiado a otra ruta y sin MRN legible** entra **sin marca
> de duplicado**. Eso es la pregunta nueva **P-16**.

~~El esquema ya no bloquea: `UNIQUE (numero_caso)` hace que el segundo `INSERT` falle,
y el programa decide qué hacer con ese fallo. **Es comportamiento de la FASE 2/3, no
esquema.** Se anota aquí porque `PENDIENTES.md` D-4 lo listaba como pregunta de
esquema y **ya no lo es**: la reconciliación la contestó (§4).~~

⚠️ **Y el razonamiento de arriba era doblemente falso, dicho para que no se repita:**
`UNIQUE (numero_caso)` **ya no existe** (migración 12), así que el segundo `INSERT`
no falla por ahí; y cuando existía, ese fallo **no era el de un PDF repetido** sino
casi siempre el de **otra familia de la misma unidad y el mismo mes**. Este documento
llamó «duplicado» a lo que era un caso nuevo, y esa confusión costó seis de diez
documentos del dueño.

### P-6. ¿`unidad_numero` y `fecha_viaje` son del caso o de cada persona? *(nadie lo ha preguntado)*

Las he puesto **en `casos`**, porque el formulario es de una familia con un solo
número de caso y `DECISIONES.md` describe *una* fila «Date traveling to the temple»
para todo el formulario, y porque la FASE 5 crit. 1 dice «un caso cuya `fecha_viaje`».

**Lo que eso hace imposible:** que dos miembros de la misma familia viajen en fechas
distintas, o pertenezcan a unidades distintas. Si eso ocurre en la realidad, la
columna se mueve a `personas` y hay que revisar la FASE 5 entera. **Nadie lo ha
preguntado y no tengo el formulario para mirarlo** — el PDF de referencia no está en
el disco (Bloqueo 2). Basta con que el dueño diga sí o no.

### P-7. «El periodo» del reporte de la FASE 8, ¿medido sobre qué fecha? *(menor, afecta a un índice)*

FASE 8 crit. 3 habla del «reporte del periodo» sin decir de qué fecha se trata:
¿la del viaje, o la de cuándo se procesó el caso? He añadido `creado_en` a `casos`
para que la segunda lectura sea posible. Si el periodo es el de viaje, `creado_en`
sigue siendo útil como traza pero no lo usa ningún reporte.

---

## Preguntas nuevas de la puesta al día del 2026-09-02

Las cinco salen del encargo de país, templo y resumen por agente. **Ninguna se ha
rellenado a ojo.**

### P-8. En «cuántas personas para verificar», ¿el número grande es cuántas le tocan o cuántas le faltan? *(decide un número en pantalla, no el esquema)*

La consulta de §6ter devuelve las dos sin coste extra: `personas` (lo que se le
encargó) y `personas_sin_propuesta` (lo que aún no ha contestado). Son distintas: un
compañero con 20 personas de las que ya contestó 18 **no está igual de cargado** que
uno con 20 sin tocar.

- **Recomiendo `personas_sin_propuesta` como número grande**, con `personas` al lado
  en pequeño («faltan 4 de 20»), porque «para verificar» significa lo que queda.
- **Y una segunda pregunta que va con ésta:** ¿«documentos» quiere decir **casos**?
  §6ter.5 compara las tres lecturas —archivo, caso, hoja— y recomienda **caso**,
  porque es lo que se asigna, lo que se entrega como PDF y lo que el compañero
  contesta. **Basta con que el dueño diga sí.** Si «documento» fuera el archivo PDF de
  origen, la cuenta se agrupa por `casos.ruta_pdf` y deja de casar con lo que Miguel
  reparte.

### ~~P-9.~~ ¿Puede haber dos casos distintos con el mismo `numero_caso`? — ✅ RESUELTA en la muestra, con un resto por construcción

> ✅ **CERRADA el 2026-09-03. La midió el supervisor el 2026-09-02 y yo no la vi.**
> Lo digo así porque es un fallo mío de lectura, no un dato que no existiera: la
> entrada estaba en `DECISIONES.md` desde el día anterior a mi entrega, y la busqué
> solo entre las entradas del 03.
>
> **La medición, literal** (`DECISIONES.md`:503-514, «El OCR SÍ lee el número de caso
> sin anotaciones. Y el nombre del archivo miente»), rasterizando el PDF y pasándole
> el OCR **sin tocar la capa de anotaciones**:
>
> ```
> LINEAS LEIDAS POR EL OCR: 94
> LINEAS QUE PASAN EL PATRON DE NUMERO DE CASO: 1
>    ACIERTO: 'CASD2609'   conf 0.99995
> ```
>
> Y recortando esa caja de la imagen y mirándola: el papel de Jonas Ficticio dice
> **`CASD2609`**, nítido, en texto impreso. El otro documento dice **`CASP2609`**
> (`DECISIONES.md`:1117-1122).
>
> | | Nombre del archivo | Lo que dice el papel |
> |---|---|---|
> | `CASP2609_Jonas_Ficticio.pdf` | `CASP2609` | **`CASD2609`** |
> | `CASP2609_Daniel_Jr._Damian_...` | `CASP2609` | `CASP2609` |
>
> **La respuesta: son dos casos distintos, y el que miente es el nombre del archivo,
> no el papel.** En el material de referencia **no hay dos casos con el mismo
> `numero_caso`**, y `importacion/guardado.py` **no descarta ninguna familia** por esa
> vía con estos cuatro documentos. La premisa entera de esta pregunta —«mismo prefijo
> `CASP2609` en los dos»— era del **nombre de archivo**, que es justo lo que no vale
> como fuente.
>
> ⚠️ **Lo que NO cierra, y por eso esto no es un borrado sino un tachado.** El número
> sigue siendo **unidad + AAMM** y por tanto **no identifica una familia**: la colisión
> es **posible por construcción**, solo que **no observada en la muestra de cuatro
> documentos**. Con cientos de PDF puede darse. La puerta quedó cerrada por otra vía
> el 2026-09-03, en la auditoría final: **una hoja que contradice la fecha o la unidad
> ya no se une al caso** — sale de `MOTIVOS_EN_QUE_LA_PAGINA_SI_ENTRO` y va a «Lo que
> no entró…» con su renglón (`DECISIONES.md`, 2026-09-03, decisión 2 del programador;
> §2.9 de este documento). Así que el daño que esta pregunta temía —descartar una
> familia en silencio— hoy deja rastro en vez de callarse.
>
> **Y sigue en pie el riesgo que esa misma entrada dejó a la vista, que es otro:** una
> letra mal leída pasa el patrón de cuatro mayúsculas + cuatro dígitos sin que nada la
> frene. El programa no fallaría; **acertaría mal**, creando un caso que no existe y
> rompiendo la reconciliación por `numero_caso` + `mrn`. Aquí la letra era correcta;
> el día que no lo sea, no hay quien lo cace. **Eso no es P-9 y no se cierra con
> ella.**

~~**Esto lo levanté al mirar el material y no lo pude cerrar. Es lo más serio que traigo.**~~

Lo que sigue queda **como estaba, tachado en lo que dejó de ser cierto**, porque el
razonamiento explica por qué la pregunta se hizo:

Lo medido, y lo que no:

| | |
|---|---|
| **Medido** (`DECISIONES.md`:275-276) | Dos de los cuatro documentos de referencia se llaman `CASP2609_Jonas_Ficticio` y `CASP2609_Daniel_Jr._Damian_Dorian_Ejemplo`. ~~**Mismo prefijo `CASP2609`, familias distintas, archivos distintos**~~ **Corregido el 2026-09-03: mismo prefijo en el NOMBRE DEL ARCHIVO. Por dentro son `CASD2609` y `CASP2609`** — no son el par duplicado, que es otro |
| **Medido** (`DECISIONES.md`, reglas de formato) | `numero_caso` = 4 letras + 4 dígitos, y los 4 dígitos son **AAMM**: `2609` es septiembre de 2026. **Sigue siendo cierto**, y es lo que deja la colisión posible por construcción |
| **Medido** (`importacion/guardado.py`:279-285) | Si `leer_caso_por_numero` encuentra el número, el formulario **se rechaza entero**: «El caso {numero} ya estaba en la base y NO se ha tocado». **Sigue siendo cierto**; lo que cambió el 2026-09-03 es que la hoja que contradice fecha o unidad ya no llega a unirse, y deja renglón |
| ~~**NO medido, y no puedo**~~ | ~~Si el `numero_caso` **de dentro** de esos dos PDF es realmente `CASP2609` en los dos. No abro los PDF: llevan datos de personas reales y el pase me lo prohíbe~~ **Medido por el supervisor el 2026-09-02 con el OCR, no por mí: `CASD2609` y `CASP2609`. Son distintos.** Sigue siendo cierto que yo no abro los PDF; lo que era falso es que nadie lo hubiera medido |

**Las dos lecturas, y por qué importa cuál sea:**

- **Si `CASP2609` identifica un formulario**, todo está bien: `UNIQUE (numero_caso)`
  es correcto y los dos archivos traerán números distintos dentro.
  ✅ **Ésta es la que salió**, y de la forma más literal: los dos archivos traen
  números distintos dentro (`CASD2609` y `CASP2609`).
- ~~**Si `CASP2609` identifica «los casos de la unidad Castries de septiembre de 2026»**
  —que es lo que un código de unidad + AAMM sugiere—, entonces **importar el segundo
  archivo descarta una familia entera en silencio**, con el mensaje «ya estaba en la
  base».~~ **Descartada para esta muestra el 2026-09-03**: no hubo tal colisión. El
  temor no era infundado —el número sigue siendo unidad + AAMM— pero **no se observó**,
  y desde la auditoría final la hoja que contradice fecha o unidad **deja renglón en
  vez de desaparecer**, así que el «en silencio» ya no aplica.

⚠️ ~~Y afecta a este encargo directamente: si el número de caso es por unidad y mes,
entonces **el prefijo sí identifica la unidad** y hay una tentación fuerte de deducir
el país de ahí.~~ **Matizado el 2026-09-03:** el número **sí** es unidad + AAMM, pero
la medición añade un motivo más para no deducir nada del prefijo — **el prefijo del
nombre del archivo no coincide con el del papel** en 1 de 2 documentos comprobados.
§6bis explica por qué deducir el país del prefijo sigue sin ser buena idea; esto lo
refuerza en vez de cambiarlo.

~~**Se cierra en treinta segundos y no lo puede hacer un agente:** Miguel abre los dos
PDF `CASP2609_*` y mira si el número de caso impreso es el mismo en los dos. **Es del
dueño.**~~ **No hizo falta el dueño: lo cerró el supervisor con el OCR el 2026-09-02**,
sin abrir el PDF a ojo y sin sacar de ahí ningún dato de una persona.

### P-10. Los países y los templos: ¿quién los da de alta, y hay alguno hoy? *(no bloquea el esquema; bloquea que se vea el color)*

Las tablas nacen vacías a propósito (§2bis). Para que el color aparezca hacen falta
dos cosas que **solo el dueño tiene**:

1. **Los países del Caribe con los que se trabaja de verdad**, y el color de cada uno.
2. **Los templos a los que viajan estos casos.**

⛔ **No los escribo.** El pase lo prohíbe expresamente y la regla permanente 1
también. Si el dueño los dicta, entran como filas —tecleadas o cargadas de una vez—,
y en ningún caso como una lista dentro del código: eso obliga a reconstruir 216 MiB
para añadir un país.

### P-11. ¿Un caso puede llevarlo más de un compañero a la vez? *(heredada, y ahora se ve en la pantalla de inicio)*

`DECISIONES.md` (2026-09-02, cierre) la deja abierta y el motor hoy lo permite: el
índice único es parcial sobre `(caso_id, companero_id)`, así que dos compañeros
distintos pueden llevar el mismo caso.

✅ **Medido el 2026-09-03, que antes era solo lectura del DDL.** Sobre una base
construida por el propio código (SQLite 3.50.4): el mismo compañero dos veces vivo
sobre un caso se **rechaza** con «UNIQUE constraint failed»; dos compañeros distintos
vivos sobre el mismo caso se **aceptan**, y el conteo de asignaciones vivas de ese
caso queda en **2**. La salida literal está en §2.5. **Sigue abierta y sigue siendo
del dueño**: la regla «un caso, un compañero» **no está escrita en ningún documento
del proyecto**, y no se inventa aquí.

**Por qué reaparece aquí:** el resumen de §6ter **cuenta ese caso dos veces**, una en
cada compañero. La suma de la columna «documentos» de todos los compañeros no dará el
total de casos asignados, y eso se ve en pantalla. No es un fallo de la consulta —está
contando lo que hay— pero conviene saberlo antes de que alguien sume a mano.

Si el dueño dice «un caso, un compañero», la migración es un índice único parcial
sobre `asignaciones (caso_id) WHERE activa = 1`, y esta cuenta deja de poder duplicar.

### P-12. ¿La unidad es del caso o de la persona? *(P-6 vuelve, con un dato nuevo)*

P-6 puso `unidad_numero` en `casos` y avisó del coste: dos familiares del mismo caso
**no pueden** estar en unidades distintas. El encargo del dueño dice que una persona
pertenece a una unidad y una unidad está en un país, lo que —leído literalmente—
pondría la unidad en `personas`.

**Recomiendo dejarlo en `casos`**, porque el formulario tiene **una** fila
`Ward/Branch Name and Unit Number` para toda la hoja (medido:
`extraccion/formulario.py:42`), así que el papel no da más de una unidad por
formulario. Si el dueño dice que en la vida real un caso mezcla unidades, la columna
se mueve y **hay que revisar la FASE 5 entera** — el aviso de P-6 sigue en pie.

## Preguntas nuevas de la puesta al día del 2026-09-03

Las dos salen del esquema que entró entre la versión 7 y la 11, y ninguna se rellena
aquí.

### P-13. Si Miguel corrige uno de los seis pasos, ¿quién firma esa corrección? *(la abrió el programador, no yo)*

Los siete campos de la migración 9 (§2.3) son **la propuesta de un compañero**,
firmada por `propuesto_por` y `propuesto_en`. Se decidió que **se ven pero no se
editan** (`DECISIONES.md`, 2026-09-03, aceptado), porque el esquema guarda **una**
propuesta por persona y dejarlos cambiar desde la pantalla borraría el trabajo de otro
sin avisar.

El propio programador dejó la pregunta escrita, literal: *«si Miguel debe poder
corregirlos, hay que decidir quién firma esa corrección»*. **La recojo aquí porque es
del esquema y no de la pantalla**: hoy no hay dónde guardar una corrección de Miguel
sobre un paso sin pisar la firma del compañero. Las dos formas que veo, sin elegir por
él:

| | Columnas nuevas | Qué se gana | Qué cuesta |
|---|---|---|---|
| **A. Un segundo juego firmado por Miguel** | 7 columnas más, o una tabla hija `propuestas` | Se ve quién dijo qué y cuándo, sin destruir nada | Duplica siete columnas y hay que decidir cuál manda al mostrar |
| **B. Miguel sobrescribe y se le pone a él en `propuesto_por`** | ninguna | Barato | **Borra el trabajo del compañero sin avisar**, que es el hallazgo ALTO que ya se cerró una vez |

⚠️ **Ninguna de las dos se construye hasta que el dueño diga si Miguel debe poder
corregirlos.** Si la respuesta es «no», no hace falta ninguna.

### P-14. Un PDF ilegible reprocesado, ¿deja un renglón nuevo o actualiza el suyo? *(menor; hoy duplica)*

`documentos_ilegibles` (§2.8) no tiene ninguna restricción de unicidad: medido, el
mismo `(ruta_pdf, pagina_pdf)` puede entrar tantas veces como se reintente. Es a
propósito para no perder el historial —un intento que falló hoy y otro que falló
mañana son dos hechos—, pero nadie ha dicho qué debe ver Miguel en la lista: si los
dos renglones o solo el último.

~~Es hermana de **P-5** (qué pasa con un PDF procesado dos veces), que sigue abierta, y
**no se decide sin ella**: si P-5 dice «rechazar», esta no llega a darse.~~

**Corregido el 2026-09-04: P-5 se resolvió al revés de esa suposición.** El dueño
dijo **entrar y marcar**, no rechazar, así que **este caso sí se da**: un PDF que
falló ayer y se reintenta hoy deja **dos renglones**. La pregunta pasa de
condicional a real, y sigue siendo pequeña: *¿Miguel debe ver los dos intentos o
solo el último?* Sin unicidad no se puede colapsar; con ella se pierde el
historial. **Es la única de las nueve que se puede contestar con un sí o un no y
sin coste**, y por eso conviene preguntarla junto a las demás en vez de dejarla
otro ciclo.

## Preguntas nuevas de la puesta al día del 2026-09-04

Las dos salen del esquema que entró entre la versión 12 y la 14, y **las dos las
devolvieron los programadores**, no las levanté yo.

### P-15. Si la misma hoja del compañero vuelve dos veces, ¿se guarda la primera? *(la devolvió expresamente al planificador el programador de Revisar)*

Las seis columnas de la marca (migración 14, §2.2) guardan **una** marca por
documento, no un historial. Su propio autor lo dejó escrito en
`datos/migraciones_de_revision.py`, literal: *«si la misma hoja vuelve dos veces,
la segunda pisa a la primera. Guardar TODAS las marcas de la historia es una tabla
nueva y una decisión de arquitectura — **no la tomo yo, va al planificador**»*.

**El caso concreto, para que se vea qué se pierde:** Sandy manda su hoja el lunes
diciendo «completa». El martes manda una corregida diciendo «no completa». Hoy
**la del lunes desaparece sin dejar rastro**, y con ella la única prueba de que
cambió de opinión — que es justo el dato que hace falta el día que alguien
pregunte por qué un documento pasó al tablero de completados y volvió.

**Las dos formas, sin elegir por él:**

| | Qué hace falta | Qué se gana | Qué cuesta |
|---|---|---|---|
| **A. Se queda como está: una marca, la última manda** | nada | Cero coste. La lista de «Revisar» lee **una fila por documento**, que es lo que sostiene los 3 000 documentos en 0,141 s | Una hoja corregida borra la anterior. **No hay forma de saber que hubo dos** |
| **B. Tabla hija `marcas_de_estado`** | Una tabla nueva y su migración (la 15) | El historial completo: quién dijo qué, cuándo, desde qué archivo. Se puede auditar una discrepancia | Un `JOIN` correlacionado —o una columna de «la última», que es duplicar el dato— en la pantalla que **más filas lee del programa** |

**Recomiendo A mientras el dueño no vea el problema en su trabajo real**, y lo
digo con su motivo: el coste de B es visible cada día en la pantalla que más se
usa, y el beneficio solo aparece el día que dos hojas discrepan — **que nadie ha
observado todavía**. Si dice que sus compañeros corrigen hojas a menudo, B.
**La decisión es del dueño**, porque es él quien sabe si eso pasa.

### P-16. ¿La identidad de un documento es su ruta o su contenido? *(la abrió el cierre del pase de identidad; hoy un archivo copiado entra sin marca)*

La migración 13 marca los duplicados, y los detecta por **la ruta del archivo**
(`ruta_pdf` + `pagina_pdf`) y por los **MRN** dentro del mismo número de caso.
Medido hoy en `importacion/guardado.py`, literal en las líneas 501-504: *«**No es
una huella del contenido: es la ruta.** El mismo archivo copiado a … la base no
guarda ninguna huella del archivo y este pase no la anade»*; y en la 37: *«No hay
huella criptografica»*.

**El hueco, en una frase:** un archivo **copiado a otra carpeta** cuyas personas
**no traigan MRN legible** entra como caso nuevo **sin decir de cuál repite**, que
es exactamente lo que el dueño pidió que se dijera. No es hipotético: los MRN
ilegibles son la razón por la que existe `documentos_ilegibles`, y copiar un PDF
de una carpeta a otra es lo que hace cualquiera que organice su trabajo por meses.

**Las dos formas:**

| | Qué hace falta | Qué se gana | Qué cuesta |
|---|---|---|---|
| **A. Se queda la ruta** | nada | Cero coste, y funciona en el caso normal —reimportar el mismo archivo desde el mismo sitio— | El archivo copiado o renombrado no se detecta |
| **B. Huella del contenido** | Una columna `huella` en `casos` y su migración (la 15). `hashlib` es **biblioteca estándar**: no engorda el `.exe` ni añade dependencia (regla permanente 2 intacta) | Detecta el mismo documento **venga de donde venga**, aunque cambie de nombre y de carpeta | Leer el archivo entero una vez por importación. **No está medido cuánto tarda sobre un PDF de seis hojas**, y con cientos de documentos ese número importa |

**Recomiendo B, y con una condición: que se mida primero.** El motivo es que el
dueño **ya vio este daño con otra cara** —seis documentos rechazados por confiar
en una identidad equivocada— y la ruta es una identidad igual de frágil: cambia
sola cuando alguien ordena su carpeta. Pero **no lo propongo sin el número**: si
la huella añade un segundo por documento sobre 300 documentos, son cinco minutos
por importación y entonces hay que hablarlo. *Lo mide: el programador, antes de
que el dueño decida.*

---

## 8. Qué NO cubre esto y por qué

**Lo que no está aquí, dicho antes de que lo encuentre otro.**

- **No hay DDL.** No escribo código: `CLAUDE.md` y el pase me lo prohíben. Este
  documento **especifica qué tiene que contener** el `CREATE TABLE`; lo escribe el
  programador en la FASE 1. Las expresiones `CHECK` que aparecen son **la
  especificación de la restricción**, no el archivo `.sql` — y **ninguna se ha
  ejecutado contra un motor SQLite**. Un paréntesis mal puesto en `substr` no lo
  caza este documento: lo caza el primer `CREATE TABLE`.
- **Lo que SÍ probé contra un motor real, y lo que no.** Ejecuté las tres expresiones
  que más fácil se escriben mal, sobre `sqlite3` en memoria (versión **3.50.4** en
  esta máquina, base desechable, no se creó ningún archivo del proyecto):

  | Expresión | Entrada | Salida |
  |---|---|---|
  | Regla del mes cruzado | `2026-09-08` + `CASP2609` | `2609` = `2609` → **acepta** |
  | | `2026-10-08` + `CASP2609` | `2610` ≠ `2609` → **rechaza** |
  | `GLOB` de `numero_caso` | `CASP2609` → `1` · `CAS2609` → `0` · `casp2609` → `0` · `CASP26099` → `0` |
  | `GLOB` de `mrn` | `055-1111-3853` → `1` · `05511113853` → `0` · `05-1111-3853` → `0` |

  Coincide exactamente con lo que piden los criterios 4, 5 y 6 de la FASE 1,
  **incluido que `GLOB` distingue mayúsculas** —`casp2609` se rechaza—, que era el
  supuesto silencioso del `CHECK` de `numero_caso`.

  **Lo que NO probé:** las restricciones de coherencia (`archivado`/`fecha_archivado`,
  `verificado`/`verificado_por`, `anulado`/`motivo_anulacion`), el índice único
  parcial y las claves foráneas con `RESTRICT`. Están **razonadas, no medidas**: eso
  exige crear tablas e insertar filas, que es la FASE 1 y no es mío.
- **Los índices de §6 no tienen ni una medición detrás.** Se proponen por la forma de
  las consultas descritas. Ninguno se ha justificado con un `EXPLAIN QUERY PLAN` ni
  con un tiempo, porque no hay contra qué. Si la FASE 5 mide y no hacen falta, sobran.
- **No hay copia de seguridad, y este documento no la resuelve.** `PENDIENTES.md` D-5
  la deja abierta. Es una decisión de producto —cada cuánto, cuántas copias, dónde—
  y no cabía en un pase de esquema. Sigue abierta y sigue siendo el riesgo más
  grande que tiene la base de Miguel.
- ~~**No he verificado el formulario.** El PDF de referencia no está en el disco
  (Bloqueo 2, medido por otros el 2026-09-02).~~ **Superado el 2026-09-02:** llegaron
  cuatro documentos reales y se midieron **9 páginas**. Se conserva el texto de abajo
  porque el matiz sigue valiendo para lo que esos nueve no cubren: las **seis filas**
  del formulario y las **seis casillas** siguen sin verse contradichas, pero nueve
  páginas de tres unidades y un solo mes no son una muestra de nada. Lo que sigue
  entre las líneas de abajo se lee con esa fecha delante.

  Todo lo que este esquema asume sobre
  la **forma del formulario** —seis filas, seis casillas, una fecha de viaje por
  formulario, un número de unidad por formulario— viene de `DECISIONES.md`, donde
  está marcado como **hipótesis del plan sin comprobar**. Ese marcado se conserva:
  las columnas existen, pero si el formulario real dice otra cosa, **manda el
  formulario** (`CLAUDE.md` §8) y este documento se corrige.
- **La tabla `contactos` es un marcador de posición.** Está declarada como tal en
  §2.6. La FASE 7 no tiene ni una decisión escrita y su propio criterio 4 lo exige
  antes de cerrar.
- **No cubre nada que no sea el esquema de datos.** El «mapa del sistema» que
  `CLAUDE.md` §7 asigna a este documento incluiría también el flujo de módulos, el
  reparto de responsabilidades del código y el diagrama de la interfaz. **Nada de eso
  está aquí**, porque el pase pedía el esquema y porque no hay código que mapear. Se
  añade cuando lo haya.
- **No he medido el estado del repositorio.** No me corresponde, es del supervisor
  (`CLAUDE.md`, reparto de roles). Lo único que ejecuté fueron **búsquedas de
  términos concretos** para comprobar dos premisas del pase antes de construir
  encima, y cuatro consultas a la documentación oficial de SQLite.
- **De las cinco URL que cito, las cinco las abrí el 2026-09-02** y cada cita es
  literal. No cité de memoria ninguna.
- **Los índices parciales están disponibles aquí: medido.** `sqlite3.sqlite_version`
  en esta máquina devuelve **3.50.4**, muy por encima del **3.8.0 (2013-08-26)** que
  la documentación oficial da como mínimo. ⚠️ Es la versión de **esta** máquina, la
  de desarrollo; la que acabe dentro del `.exe` la fija PyInstaller al empaquetar y
  **no la he comprobado**. Se comprueba en la FASE 1 imprimiendo
  `sqlite3.sqlite_version` desde el programa, no desde el intérprete.

### Qué NO cubre la puesta al día del 2026-09-02

Añadido por el planificador al actualizar este documento con país, templo y el
resumen por agente. **Lo que no está, dicho antes de que lo encuentre otro.**

- **Nada de la sección 2bis existe en el motor.** Es especificación de una migración a
  la versión 7 que todavía no se ha escrito. Ninguna de las tres tablas nuevas se ha
  creado en una base del proyecto, ninguna de sus restricciones se ha ejecutado contra
  SQLite, y **el `CHECK` del color no lo he probado**. Lo único que sí medí de ellas es
  **cuánto ocupan** (§2bis.6), y para eso las creé en una base desechable del
  cuaderno de trabajo, no en ninguna del proyecto.
- **La consulta del resumen por agente SÍ la ejecuté** contra el motor, sobre datos
  inventados por mí en memoria (§6ter.3), y encontró un defecto real —el `COUNT` que
  daba `1` sobre cero personas—. **No la he ejecutado sobre datos de verdad**, porque
  no hay base de Miguel a la que yo deba entrar.
- **No abrí ni un PDF de `pdfs_referencia/`.** Llevan nombres y MRN de personas
  reales y el pase me lo prohíbe. Todo lo que digo del papel viene de `DECISIONES.md`
  o de los comentarios del código, y en cada punto está dicho de cuál de los dos.
  ~~**La consecuencia grande es P-9**, que no se puede cerrar sin abrirlos.~~
  **Corregido el 2026-09-03:** P-9 **sí se cerró sin abrirlos a ojo** — el supervisor
  la midió el 2026-09-02 pasando el OCR sobre la página rasterizada, que saca el
  número de caso sin exponer ningún dato de una persona. Sigue siendo cierto que yo no
  abro los PDF.
- ~~**No sé qué prefijo lleva dentro cada documento.** `CASP`, `PARB` y `SURB` son
  prefijos de **nombre de archivo**. Que el `numero_caso` extraído coincida con ellos
  es plausible y **no está medido**.~~ **Corregido el 2026-09-03: ya no es plausible,
  es falso en 1 de 2.** De los cuatro documentos se han medido **dos** por dentro:
  `CASD2609` y `CASP2609`. El nombre del archivo **no** es fuente del número de caso.
  De `PARB` y `SURB` sigue sin medirse qué llevan dentro.
- **No hay lista de países ni de templos, y no la he escrito.** Es P-10. Un hueco
  declarado; rellenarlo a ojo haría daño de verdad, porque decide de qué color se
  pinta la nacionalidad de una persona.
- **No busqué en vídeo.** Si algo del formato de este formulario solo estuviera
  explicado en un vídeo, no lo he visto: solo leo páginas.
- **Los dos índices nuevos de §2bis.5 no tienen medición.** Igual que los cinco de
  §6, y por lo mismo: no hay datos que cronometrar.
- **`datos/migraciones.py` estaba a medio editar mientras yo medía.** Un segundo
  intento de construir el esquema levantó
  `NameError: name '_rehacer_la_tabla_casos' is not defined`. **Mis mediciones del
  esquema (§ puesta al día) se tomaron antes, y salieron limpias**; las anoto igual
  porque quien las repita hoy puede no poder. Es trabajo del programador en curso y no
  lo he tocado.
- **No he medido el estado del repositorio.** No me corresponde: es del supervisor. Lo
  que ejecuté fueron construcciones del esquema en memoria, una consulta con
  `EXPLAIN QUERY PLAN`, dos búsquedas de términos y cuatro consultas a fuentes de
  fuera, todas citadas con su URL y su fecha.
- **De las fuentes externas nuevas, las cuatro las abrí el 2026-09-02** y cada cita es
  literal. Dos dieron respuesta (Castries, Paramaribo), una dio la confirmación del
  MRN, y **dos no tenían lo que buscaba** —las secciones de términos del Manual
  General— y así está dicho en §5bis. La del *World Factbook* que intenté primero
  respondió que **el recurso está descontinuado desde febrero de 2026**; por eso la
  cita de los dos países no viene de allí.

### Qué NO cubre la puesta al día del 2026-09-03

**El encargo era acotado y lo digo antes de que lo encuentre otro:** poner §2 al día
con el esquema que produce el código en `aa90ad1`. Eso está hecho y medido. Lo que
queda fuera:

- **No auditó el código, ni ejecutó la suite.** Eso es de QA, que estaba con el
  árbol congelado mientras yo escribía. Lo único que ejecuté fue **construir una base
  temporal con `aplicar_esquema` y preguntarle al motor** (`PRAGMA table_info`,
  `PRAGMA foreign_key_list`, `sqlite_master`) más seis inserciones de prueba para
  medir tres `CHECK`, un `UNIQUE` y el índice de asignaciones. **Medir no es probar.**
- **No verifiqué que el código USE cada columna que declara.** Comprobé que el motor
  las tiene. Que `interfaz/` o `paquete/` las lean y escriban bien es otra pregunta, y
  es de QA. La única excepción es `espejo/hojas.py`, que miré porque este documento
  tenía un número suyo desfasado.
- **No verifiqué el esquema de la base REAL del dueño.** Lo medido es lo que el
  código produce sobre una base nueva. `DECISIONES.md` (2026-09-03) dice que su base
  llegó a estar en la versión 8 y que la 9 corrió sobre ella; **no lo he comprobado
  yo** y no puedo: esa base lleva datos de personas reales y está fuera del
  repositorio.
- **No abrí ningún PDF.** El pase lo prohíbe y llevan datos de personas reales. Todo
  lo que este documento dice de lo que traen las páginas —los siete dígitos de
  `7000011`, el `Caracas-Venezueia`, las 2 de 9 páginas sin `Temple Name`— es **cita
  de lo que midió otro**, y va dicho como cita, no como medición mía.
- **No comparé contra el esquema del proyecto viejo.** El Excel del agente sí se
  comparó (lo hizo QA, 16/16); qué tablas tenía el viejo y si guardaba algo que el
  nuevo no guarda es una pregunta que **nadie ha hecho** y que podría valer la pena.
  La dejo dicha, no la respondo.
- **Tres cosas que el esquema hoy NO tiene y nadie ha pedido, y no las propongo:**
  ninguna tabla registra **quién ejecutó una migración**, `documentos_ilegibles` no
  distingue un reintento de un hecho nuevo (P-14), y `filas_descartadas` no tiene
  forma de marcarse como «ya la miré». Las tres son huecos declarados, no propuestas.
- **`filas_descartadas` no tiene hoja en el Excel espejo** (§2.9). Lo declaró el
  programador y lo confirmo leyendo `espejo/hojas.py`; **arreglarlo no es mío**.
- **Las preguntas abiertas de esquema que siguen sin respuesta del dueño**, sin
  rellenar: ~~**P-2** (mes cruzado, hoy aviso y no `CHECK`), **P-3** (cómo se llama el
  origen `manual`), **P-4** (borrar o desactivar una asignación),~~ **P-5** (PDF
  procesado dos veces), ~~**P-6**~~**/P-12** (unidad del caso o de la persona), **P-7**
  (sobre qué fecha va «el periodo»), **P-8** (qué número va en la tarjeta), ~~**P-9**
  (dos casos con el mismo `numero_caso` — la más grave, y se cierra en treinta segundos
  abriendo dos PDF, cosa que solo puede hacer el dueño),~~ **P-10** (países y templos),
  **P-11** (un caso, ¿más de un compañero?), **P-13** (quién firma la corrección de un
  paso) y **P-14** (renglón nuevo o actualizado al reprocesar un ilegible). ~~Solo
  **P-1** está resuelta, y con un resto: falta el valor que significa «resuelta».~~

  ⚠️ **Corregido el 2026-09-03, y el error era mío.** Esa lista daba por abiertas seis
  preguntas que ya estaban cerradas en `DECISIONES.md`: **P-1** (con un resto: falta el
  valor que significa «resuelta»), **P-2**, **P-3**, **P-4**, **P-6** y **P-9**. La
  tabla de §7 ya marcaba resueltas las cinco primeras y yo copié la lista sin cotejarla
  contra ella. **P-9 la cerró el supervisor el 2026-09-02** con el OCR sobre la página
  rasterizada —el papel dice `CASD2609` donde el nombre del archivo dice `CASP2609`—, y
  **yo no la vi porque busqué solo entre las entradas del 2026-09-03**. La lista
  correcta de lo que sigue **abierto y sin respuesta del dueño** es: **P-5, P-7, P-8,
  P-10, P-11, P-12, P-13 y P-14** — ocho, no doce. **P-12 no es P-6 reabierta por el
  dueño: la reabrí yo** al ver el encargo de país y unidad, y sigue en pie.

  Y lo que **no** cierra P-9, dicho aparte para que nadie lo dé por cerrado con ella:
  el número de caso es **unidad + AAMM** y **no identifica una familia**, así que la
  colisión es **posible por construcción, no observada en esta muestra de cuatro
  documentos**; y una letra mal leída sigue pasando el patrón de cuatro mayúsculas +
  cuatro dígitos sin que nada la frene — el programa **acertaría mal** en vez de
  fallar. Ninguna de las dos es una pregunta al dueño hoy; son riesgos anotados.
- **No busqué en vídeo, y sigo sin poder.** Si algo del esquema estuviera explicado
  solo en un vídeo, no lo he visto.
- **No consulté ninguna fuente externa nueva.** Este encargo se contesta con el
  código del repositorio y el motor de esta máquina; la única cita técnica de fuera
  que se usa es la de `ALTER TABLE ADD COLUMN` y la de los índices parciales, ya
  citadas con su URL y su fecha (2026-09-02) en §2.5 y en `datos/migraciones.py`. **No
  las he vuelto a abrir hoy**, así que su fecha de consulta sigue siendo el 2026-09-02
  y no el 03.
- **No medí el estado del repositorio.** No me corresponde: es del supervisor.

  ⚠️ **Corregido el 2026-09-04:** la lista de arriba —«P-5, P-7, P-8, P-10, P-11,
  P-12, P-13 y P-14, ocho, no doce»— **ya no es la de hoy**. P-5 se cerró y
  entraron P-15 y P-16. La cuenta vigente está en la revisión que abre §7: **nueve
  abiertas**, una de ellas (P-8) degradada a preferencia de interfaz.

### Qué NO cubre la puesta al día del 2026-09-04

**El encargo era acotado y lo digo antes de que lo encuentre otro:** declarar las
versiones 12, 13 y 14 en §2 y en la tabla de versiones, recontar las columnas, y
revisar las preguntas abiertas contra lo decidido los días 3 y 4. Eso está hecho y
medido. Lo que queda fuera:

- **No auditó el código ni ejecutó la suite.** Eso es de QA, y además hay una suite
  corriendo en esta máquina mientras escribo: el pase me prohíbe expresamente
  lanzar nada pesado. Lo único que ejecuté fue **construir una base temporal con
  `abrir_conexion` + `aplicar_esquema`** y preguntarle al motor
  (`PRAGMA table_info`, `sqlite_master`), más búsquedas de texto y `wc -l`.
  **Medir no es probar.**
- **No verifiqué que el código USE las ocho columnas nuevas.** Comprobé que el
  motor las tiene y leí el módulo que las crea. Que `interfaz/revisar.py` o
  `paquete/reconciliacion.py` las lean y escriban bien es otra pregunta y es de QA.
- **No ejecuté ni un `CHECK` de los que declaro para las columnas nuevas.** Los
  `CHECK` cruzados de la migración 14 —quién con cuándo, en los dos grupos— están
  **transcritos del código, no probados contra el motor con inserciones**. La ronda
  anterior sí midió tres `CHECK`, un `UNIQUE` y el índice de asignaciones; **ésta
  no ha medido ninguno**, y eso es menos de lo que hizo la anterior.
- **No verifiqué el esquema de la base REAL del dueño**, y esta vez pesa más que
  las otras veces: su base migró sola de la 11 a la 13 el 2026-09-03 a las 22:28 y
  **ningún agente reconoce haberlo hecho** (`DECISIONES.md`). Lo medido aquí es lo
  que el código produce sobre una base **nueva**. Que la suya esté en la 14 y con
  estas 104 columnas **no lo ha comprobado nadie**.
- **No comprobé qué lleva la clave visible del Excel del paquete**, y de eso
  depende que la reconciliación siga siendo correcta ahora que `numero_caso` no es
  único. Está dicho como riesgo abierto al principio de §4, **no como un fallo**:
  puede estar perfectamente resuelto y yo no lo sé. `paquete/exportacion.py` y
  `paquete/reconciliacion.py` son los archivos; **medirlo es de QA**.
- **No abrí ningún PDF.** Llevan datos de personas reales. Todo lo que este
  documento dice de lo que traen las páginas —los seis rechazos de la PC del
  trabajo, el `BALC2609`, el `Caracas-Venezueia`— es **cita de lo que midió otro**,
  y va dicho como cita.
- **No repetí ninguna cifra de rendimiento del ciclo 5.** Los 82 widgets, los
  0,190 s, los 0,071 s y los 0,141 s de las 3 000 tarjetas son de los informes de
  sus programadores. Van citados como suyos y **no los he verificado**; medir
  rendimiento no es mío.
- **No comprobé si el esquema tiene columnas que ya nadie usa.** Con 104 columnas
  en 9 tablas y catorce versiones en tres días, la pregunta *«¿sobra alguna?»* es
  razonable y **nadie la ha hecho**. La dejo dicha, no la respondo: exige leer todo
  el código que lee la base, que es una auditoría y no una puesta al día.
- **No consulté ninguna fuente externa.** Ni una URL nueva. Las citas de SQLite que
  este documento arrastra son del 2026-09-02 y **no las he vuelto a abrir**, así
  que su fecha de consulta sigue siendo aquélla.
- **No busqué en vídeo, y sigo sin poder.** Solo leo páginas.
- **No medí el estado del repositorio** más allá de `git log -1` y
  `git status --porcelain` para comprobar la premisa del pase antes de escribir
  nada. Levantar el estado es del supervisor.

### Qué NO cubre la segunda puesta al día del 2026-09-04 (la versión 15)

**El encargo era todavía más acotado: declarar la 15, contar los `CHECK` y quitar
los restos de la regla vieja del MRN.** Eso está hecho y medido. Lo que queda
fuera, y lo digo antes de que lo encuentre otro:

- **No ejercité la migración 15 sobre datos.** Construí una base **nueva** —donde
  `aplicar_esquema` la aplica sobre `personas` vacía— y le pregunté al motor. **La
  reconstrucción de `personas` con filas dentro no la ha corrido nadie sobre datos
  del dueño, y no por descuido: no existen esos datos.** Medido el 2026-09-04 en
  solo lectura sobre los **ocho** respaldos de `C:\Users\josem\Fichas-entrega\` y
  `C:\Users\josem\Fichas-work\` más la base viva de `Documents\Fichas`: **nueve
  bases reales, `personas` con 0 filas en las nueve, 0 con MRN**. Queda anotado
  como riesgo en `PENDIENTES.md` · «DC-15».
- **No probé el `CHECK` nuevo con inserciones.** Lo transcribí del
  `sqlite_master` de la base construida; **no metí un `055-1111-385A` para ver si
  entra ni un `055-1111-385$` para ver si se rechaza.** Eso es una prueba y las
  pruebas son de QA. **Medir no es probar.**
- **No conté los `CHECK` del C# ni comparé el DDL de C# contra el de Python.** Los
  48 son del esquema que produce el **Python**. Que el `Fichas.Datos` de C#
  produzca los mismos 48 es exactamente lo que pide el criterio C2-1, y **hoy no
  está comprobado por nadie**.
- **No ejecuté `dotnet` de ninguna forma**, ni `build`, ni `restore`, ni
  `list package --vulnerable`: hay seis programadores compilando en worktrees y el
  pase me lo prohíbe. Todo lo que digo de `Microsoft.Data.Sqlite` sale de leer
  `Fichas.Datos.csproj` y `obj/project.assets.json`, más el aviso de GitHub. Ver
  `PENDIENTES.md` · «DC-16».
- **No abrí ningún dato personal.** De la base del dueño leí **solo** `MAX(version)`
  y `COUNT(*)`, en `mode=ro`. Ni un nombre, ni un MRN.
- **No revisé si el resto del código Python valida el MRN con la regla vieja.**
  Este documento describe el **esquema**; que `datos/validacion.py`,
  `extraccion/` o la pantalla de corrección sigan exigiendo el último dígito es
  otra pregunta, **y no la he mirado**. Si alguna lo hace, el dato pasa el motor y
  lo tira la capa de arriba, que es el mismo daño con otro culpable. *Lo mide: QA.*
- **No repetí las mediciones de la ronda anterior.** Las 104 columnas y los 8
  índices los volví a contar; el cotejo columna a columna contra las tablas §2.x
  con el script extractor **no lo repetí** — me apoyo en que la 15 no toca
  ninguna columna, que sí medí.
- **No consulté fuentes externas para la 15.** La cita de SQLite sobre reconstruir
  una tabla (<https://www.sqlite.org/lang_altertable.html>, «Making Other Kinds Of
  Table Schema Changes») está **tomada del módulo que la implementa**, con su fecha
  de consulta del 2026-09-04; **yo no volví a abrir esa página**.
- **No busqué en vídeo. Sigo sin poder.**
