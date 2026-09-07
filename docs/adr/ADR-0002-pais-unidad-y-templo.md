# ADR-0002 — País, unidad y templo: tres tablas, y el país sale de la unidad

- **Fecha:** 2026-09-02
- **Estado:** propuesta — ⛔ **espera decisión del dueño** (P-10 y P-12 de
  `docs/ARQUITECTURA.md` §7)
- **Decide:** planificador, sobre encargo del dueño
- **Afecta a:** `docs/ARQUITECTURA.md` §2bis y §6bis; `casos`; una migración nueva
  —**el primer número libre cuando se construya**, que hoy no es el 7: el programador
  ya tiene las versiones 7 y 8 en `datos/migraciones_de_importacion.py`—;
  `espejo/hojas.py`; `paquete/columnas.py`; la pantalla de inicio
- **Inmutable** (`CLAUDE.md` §7). Si esta decisión se revierte, se escribe un ADR
  nuevo que la sustituya; éste no se edita.

---

## El problema

El dueño usó el programa con sus datos reales y pidió tres cosas que el esquema de la
versión 6 no puede sostener. En sus palabras:

> «están varios templos y son varios países del Caribe, y también poder [distinguir
> por] color de qué países son esas personas»

Descompuesto, son tres preguntas que el esquema no sabe responder:

1. ¿De qué **país** es la persona de este caso?
2. ¿A qué **templo** viaja este caso?
3. ¿Qué **color** le corresponde a ese país, de forma que sea siempre el mismo?

Lo que el esquema sí tiene hoy, medido sobre el código de la versión 6:
`casos.unidad_numero` (TEXT, 6 o 7 dígitos) y `casos.unidad_nombre` (TEXT). Ni país,
ni templo, ni color.

## Lo que hace falta saber antes de elegir

Cuatro hechos del proyecto. Son los que deciden, y cada uno con su fuente.

**1. El programa no tiene red, ni la va a tener.** Regla permanente 1 y regla
permanente 2 de `CLAUDE.md`. No puede consultar un servicio que le diga en qué país
está una unidad. Cualquier lista tiene que estar **dentro del binario** o **dentro de
la base**. No hay tercera opción.

**2. El material del proyecto no contiene una lista de países ni de templos.** Ni
`CLAUDE.md`, ni `DECISIONES.md`, ni `ESTADO.md`, ni los cuatro documentos de
referencia. Escribir una es inventar un dato: **regla permanente 1**.

**3. Un color necesita una identidad estable.** El encargo es «distinguir por color de
qué países son esas personas». Si el país fuera texto libre, `Santa Lucía`,
`Saint Lucia` y `St. Lucia` serían tres países y tres colores para las mismas
personas — y nadie se daría cuenta, porque cada fila se vería bien por separado.

**4. El ejecutable pesa 216,9 MiB y se entrega copiándolo a mano** a la PC del
trabajo, que no tiene Python (`ESTADO.md`). Cambiar algo que viva dentro del binario
cuesta reconstruir y volver a copiar 216,9 MiB. Cambiar algo que viva en la base
cuesta teclear.

## Las tres opciones

**A. Dos columnas de texto libre en `casos`:** `pais TEXT`, `templo TEXT`.

**B. Una lista fija en el código**, al estilo de `datos/estados.py`, que ya guarda así
los valores de `estado_recomendacion`.

**C. Tres tablas** —`paises`, `unidades`, `templos`— con `casos.unidad_id` y
`casos.templo_id` apuntando a ellas. *(elegida)*

## La comparación

| | **A. Texto libre en `casos`** | **B. Lista fija en el código** | **C. Tres tablas** *(elegida)* |
|---|---|---|---|
| Coste de construir | Mínimo: 2 `ALTER TABLE` | Bajo: un módulo y 2 `ALTER TABLE` | Alto: 3 tablas, 3 columnas, una pantalla de mantenimiento |
| ¿Hay que inventar la lista? | No | **Sí** ⛔ regla permanente 1 | No: nace vacía |
| Añadir un país nuevo | Teclearlo otra vez, en cada caso | **Reconstruir y recopiar 216,9 MiB** | Teclear una fila, una vez |
| ¿Soporta un color estable por país? | **No.** Tres grafías, tres colores | Sí | **Sí**: una fila, un `id`, un color |
| ¿Hay que teclear el país en cada caso? | **Sí**, uno por uno | Elegirlo de una lista, uno por uno | **No.** Una vez por *unidad*, y todos sus casos futuros lo heredan |
| Faltas de ortografía | Silenciosas y multiplicadoras | Imposibles | Imposibles en el vínculo; posibles al dar de alta, y se corrigen en una fila |
| Contar casos por país | `GROUP BY` sobre texto sucio | Fiable | Fiable, por `id` |
| Coste en disco | ~0 | 0 | **36 KiB medidos** con 20 países, 10 templos y 200 unidades |
| Coste en el ejecutable | 0 | unos bytes | **0** — la base vive en `Documentos\Fichas`, fuera del programa (`CLAUDE.md` §4) |

### La medición del coste en disco

Porque el pase preguntaba por el coste teniendo delante los 216 MB del ejecutable, y
la respuesta merecía un número y no un adjetivo:

```
$ .venv/Scripts/python.exe -c "<aplicar_esquema; crear paises/templos/unidades; poblar; VACUUM>"
base_v6_vacia_bytes                                                     77824
base_v6_mas_3_tablas_con_20_paises_10_templos_200_unidades_bytes       114688
diferencia_bytes                                                        36864
diferencia_KiB                                                           36.0
```

**36 KiB.** El tamaño del ejecutable no es un argumento en esta decisión, en ninguna
dirección: las tablas no viven ahí. Donde los 216,9 MiB **sí** pesan es en la opción
B, y en su contra: cada país nuevo obliga a reconstruir el paquete entero y volver a
llevarlo a la otra máquina.

## La decisión

**Se elige C: tres tablas.** Y con ella, dos decisiones que van pegadas:

### C.1 El país sale de la **unidad**, no del caso ni del número de caso

`unidades` lleva `pais_id NOT NULL`. `casos` gana `unidad_id`, que es un vínculo que
**Miguel confirma**. El país de un caso se lee recorriendo
`casos.unidad_id → unidades.pais_id → paises`.

Esto es lo que hace que la opción C no sea solo «más ordenada» sino **menos trabajo**:
el país no es un dato del caso, es un dato de la unidad. Se dice una vez por unidad y
sirve para todos sus casos, los de hoy y los del año que viene. En A y en B hay que
decirlo en cada caso, para siempre.

### C.2 Lo crudo y lo resuelto conviven; el vínculo **no** pisa lo que dijo el papel

`casos.unidad_numero` y `casos.unidad_nombre` se quedan **exactamente como están**, con
su fila en `procedencia_campo`. `unidad_id` es una columna **aparte**.

Es la **regla permanente 5** llevada al esquema: el sistema propone y Miguel confirma,
pero **la confirmación no puede destruir la propuesta**. Si `unidad_id` sustituyera a
`unidad_numero`, resolver el vínculo borraría lo que el papel decía y `valor_ocr` se
quedaría sin nada con qué contrastarse. Además hace el error reversible: si Miguel
enlaza la unidad equivocada, el papel sigue ahí y se ve.

### C.3 El templo ya está en el papel y hoy se tira

Medido en el código: `"Temple Name"` figura en `extraccion/formulario.py`:49 dentro de
`ETIQUETAS_QUE_CIERRAN_LAS_PERSONAS`, y el comentario de las líneas 43-46 dice que
faltó en **2 de las 9 páginas** por escaneos malos. El extractor **ya localiza esa
etiqueta** y la usa como tope inferior del bloque de personas — y **descarta su
valor**. Sacar `templo_nombre` es aplicarle el mismo `banda_de_valor` +
`resolver_campo` que ya se le aplica a `unidad_nombre`. No es maquinaria nueva.

⚠️ Ese «2 de 9» lo mide el comentario del programador; **yo no abrí los PDF** — llevan
datos de personas reales.

## Lo que esta decisión cuesta, dicho para que conste

1. **Una pantalla de mantenimiento que hoy no existe:** dar de alta países con su
   color, templos, y unidades con su país. Es trabajo de interfaz que las opciones A y
   B no necesitan.
2. **El día 1 no se ve ningún color.** Con `paises` y `unidades` vacías, todos los
   casos salen «sin país». El color aparece a medida que Miguel rellena. **No es un
   defecto: es que el dato no existía y nadie lo va a inventar por él.**
3. **Un paso manual por unidad nueva.** La primera vez que llega un caso de una unidad
   desconocida, alguien tiene que decir de qué país es. Se hace una vez.
4. **Tres tablas más que mantener** en `espejo/hojas.py` si se quieren en el Excel
   espejo, y una columna más en `paquete/columnas.py` si el país entra en el paquete.
5. **Tres tablas más en la copia de seguridad que no existe.** La deuda D-5 sigue
   abierta y ahora tiene más que perder.

## Lo que se descarta explícitamente, y por qué

⛔ **Deducir el país del prefijo de 4 letras del número de caso.** Es lo primero que
se le ocurre a cualquiera que mire `CASP`, `PARB`, `SURB`, y **no se sostiene**:

- `CASP` empareja con `Castries Branch - 700001` (`DECISIONES.md`) — una **unidad**,
  no un país.
- Castries es la capital de **Santa Lucía**; Paramaribo es la capital de **Surinam**
  (Wikipedia, consultadas 2026-09-02, primeras frases citadas literalmente en
  `docs/ARQUITECTURA.md` §6bis.1). `PARB` apunta a Paramaribo y `SURB` a Surinam:
  **dos prefijos, un solo país.** Si el prefijo fuera el país, eso no podría pasar.
- **No encontré evidencia consultable** de que ese prefijo sea un código
  estandarizado (búsqueda del 2026-09-02).
- Y el fondo: sería una regla inferida de **tres prefijos** que decide de qué color se
  pinta la nacionalidad de una persona. Eso es adivinar, y `CLAUDE.md` §1 lo prohíbe.

⛔ **Emparejar unidades por parecido de nombre.** Un `0,87` de similitud entre dos
nombres salidos del OCR no es una unidad. El emparejamiento es por `unidad_numero`
**exacto**, o no hay emparejamiento y Miguel elige de la lista. Es el mismo criterio
que ya rige la reconciliación del Excel (`DECISIONES.md`, FASE 6: «nunca por nombre:
un acento de más crea un registro fantasma»).

## Lo que esta decisión NO resuelve

- **No dice qué países ni qué templos hay.** Es P-10, y es del dueño. Las tablas nacen
  vacías a propósito.
- **No resuelve si la unidad es del caso o de la persona.** Es P-12; se recomienda
  dejarla en `casos` porque el papel trae una sola fila de unidad por formulario.
- **No resuelve P-9** —si dos casos distintos pueden compartir `numero_caso`—, que es
  independiente pero que, si se resolviera en el sentido «el número identifica la
  unidad y el mes», haría reaparecer la tentación de deducir el país del prefijo. La
  respuesta seguiría siendo la de arriba.
