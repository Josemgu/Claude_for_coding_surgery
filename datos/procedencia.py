"""De donde salio cada valor: escribirlo y leerlo.

La tabla `procedencia_campo` existe desde la FASE 1 y hasta ahora no tenia por
donde escribirse: la FASE 2 produce la procedencia completa de cada campo —origen,
confianza y lo que habia leido el OCR antes de que nadie lo corrigiera— y se
quedaba en memoria. Este modulo es ese hueco tapado.

Vive aparte de `datos/repositorio.py` por dos razones, y ninguna es el tamano del
archivo: `procedencia_campo` es la unica tabla polimorfica del esquema —apunta a
`casos` o a `personas` por un par (tabla, registro_id) que el motor NO puede
defender con una clave foranea (`docs/ARQUITECTURA.md` §2.7)— y es la unica que
lleva la firma de quien verifico. Las dos cosas se defienden aqui, en un solo
sitio, y no repartidas por el modulo de casos y personas.

Como en `datos/repositorio.py`, cada instruccion es una cadena literal con
marcadores `?` y los valores viajan aparte.

⚠️ Lo que este modulo NO decide, a proposito: que pasa con la verificacion de un
campo cuando el mismo PDF se vuelve a procesar. `guardar_procedencia_de_campo`
actualiza el origen, la confianza y el texto del OCR, y **no toca** las tres
columnas de verificacion. Borrarlas seria perder quien dio el dato por bueno;
dejarlas puede dejar un «verificado» apuntando a un valor que cambio. Cual de las
dos es la correcta depende de que se decida en «Preguntas abiertas · P-5»
(«un PDF procesado dos veces: reemplazar, rechazar o preguntar»), que sigue
abierta. No se elige aqui por cuenta propia.

⚠️ **Lo que ese aviso NO cubria, y se colaba por ahi.** El caso del PDF
reprocesado estaba declarado; el de **editar a mano un campo ya firmado** no
aparecia en ningun sitio, y por esa puerta QA midio el 2026-09-03 un caso firmado
por Miguel, con fecha y hora, **sin fecha de viaje**, que desaparecia de los tres
avisos a la vez. Ese si esta decidido y esta abajo:
`retirar_la_firma_de_los_campos`, llamada desde `datos/repositorio.py` —que es
donde el valor se escribe—. Los dos casos siguen separados: aqui el UPSERT sigue
sin tocar la verificacion, y P-5 sigue exactamente donde estaba.
"""

from datos.esquema import marca_de_tiempo
from datos.validacion import ErrorDeValidacion

# Las dos tablas que `procedencia_campo.tabla` admite, y los cuatro origenes que
# admite `procedencia_campo.origen`. Se repiten aqui, y no se importan del
# esquema, porque el `CHECK` del motor es la ultima defensa y no la primera: si
# solo estuvieran alli, un valor malo llegaria hasta el motor y volveria con un
# mensaje en ingles que no nombra el campo.
TABLAS_CON_PROCEDENCIA = ("casos", "personas")
ORIGENES_DE_CAMPO = ("anotacion", "ocr", "vacio", "manual")

# Las cuatro columnas que guardan el recorte del escaneo, en el orden en que se
# nombran en todas partes: `(x0, y0, x1, y1)`. Entraron con la version 3 del
# esquema. Nadie arma SQL con esta constante: sirve para leer las cuatro de una
# fila sin escribir sus nombres cuatro veces por sitio.
COLUMNAS_DE_LA_BANDA = ("banda_x0", "banda_y0", "banda_x1", "banda_y1")

# La lista de columnas se repite entera en cada consulta, en vez de guardarse en
# una constante e interpolarse. No es descuido: `pruebas/auditoria_sql.py` lee el
# arbol de sintaxis y dictamina INSEGURA cualquier instruccion armada con una
# f-string, sin excepcion por «esto es una constante mia». Una lista blanca que
# admite excepciones deja de ser una lista blanca.


def _validar_tabla(tabla):
    """La procedencia solo describe campos de `casos` y de `personas`."""
    if tabla not in TABLAS_CON_PROCEDENCIA:
        validas = ", ".join(repr(nombre) for nombre in TABLAS_CON_PROCEDENCIA)
        raise ErrorDeValidacion(
            f"El campo 'tabla' no vale: se recibió {tabla!r} y la procedencia solo "
            f"describe campos de {validas}."
        )
    return tabla


def _validar_origen(origen):
    """De donde salio el valor: de una anotacion, del OCR, de nada, o de una mano."""
    if origen not in ORIGENES_DE_CAMPO:
        validos = ", ".join(repr(nombre) for nombre in ORIGENES_DE_CAMPO)
        raise ErrorDeValidacion(
            f"El campo 'origen' no vale: se recibió {origen!r} y los orígenes "
            f"admitidos son {validos}."
        )
    return origen


def _validar_confianza(confianza):
    """Una proporcion entre 0 y 1, o nada cuando no hubo lectura que medir."""
    if confianza is None:
        return None
    if isinstance(confianza, bool) or not isinstance(confianza, (int, float)):
        raise ErrorDeValidacion(
            f"El campo 'confianza' no vale: se recibió {confianza!r} y se esperaba "
            "un número entre 0.0 y 1.0, o nada."
        )
    if not 0.0 <= confianza <= 1.0:
        raise ErrorDeValidacion(
            f"El campo 'confianza' no vale: se recibió {confianza!r} y tiene que "
            "estar entre 0.0 y 1.0."
        )
    return float(confianza)


def _validar_nombre_de_campo(campo):
    """El nombre de la columna que se describe. Sin el, la fila no dice nada."""
    if not isinstance(campo, str) or not campo.strip():
        raise ErrorDeValidacion(
            f"El campo 'campo' no vale: se recibió {campo!r} y se esperaba el nombre "
            "de la columna cuya procedencia se guarda, como 'fecha_viaje'."
        )
    return campo


def _validar_tachon(anulado_por_tachon):
    """Si el papel llevaba un tachon sobre ese campo. Vale 1 o 0, nunca nada.

    A diferencia de una casilla de ordenanza, aqui NO hay tercer estado: o el
    extractor encontro un trazo de tachon sobre la banda o no lo encontro. «No se
    sabe» no es una respuesta posible, porque la pregunta se le hizo siempre.
    """
    if anulado_por_tachon in (0, 1, True, False):
        return int(anulado_por_tachon)
    raise ErrorDeValidacion(
        f"El campo 'anulado_por_tachon' no vale: se recibió {anulado_por_tachon!r} "
        "y se esperaba 1 (había un tachón sobre ese campo) o 0 (no lo había)."
    )


def _validar_banda(banda):
    """El rectangulo del recorte del escaneo, en fracciones de la pagina.

    Llega como `(x0, y0, x1, y1)` con cada valor entre 0.0 y 1.0, o como `None`
    cuando no hay banda —el OCR no encontro el ancla de ese campo, y entonces no
    hay nada que recortar—. Se guarda en fracciones y no en pixeles para que la
    tira se pueda volver a cortar a cualquier escala; el motivo largo esta en
    `datos/migraciones.py`.

    Se comprueba que `x0 <= x1` y `y0 <= y1`. Un rectangulo del reves no lo puede
    ver el `CHECK` del motor —que mira cada columna por separado— y produciria un
    recorte de ancho negativo mucho despues, al dibujar.
    """
    if banda is None:
        return (None, None, None, None)
    try:
        x0, y0, x1, y1 = (float(valor) for valor in banda)
    except (TypeError, ValueError) as causa:
        raise ErrorDeValidacion(
            f"El campo 'banda' no vale: se recibió {banda!r} y se esperaban cuatro "
            "fracciones de la página, como (0.1, 0.2, 0.9, 0.24)."
        ) from causa
    for nombre, valor in zip(("x0", "y0", "x1", "y1"), (x0, y0, x1, y1)):
        if not 0.0 <= valor <= 1.0:
            raise ErrorDeValidacion(
                f"El campo 'banda' no vale: su '{nombre}' es {valor!r} y las "
                "coordenadas son fracciones de la página, entre 0.0 y 1.0."
            )
    if x1 < x0 or y1 < y0:
        raise ErrorDeValidacion(
            f"El campo 'banda' no vale: se recibió {banda!r}, que está del revés. "
            "Se esperaba (x0, y0, x1, y1) con x0 <= x1 y y0 <= y1."
        )
    return (x0, y0, x1, y1)


def banda_de_la_procedencia(procedencia):
    """El rectangulo de una fila ya leida, o None si esa fila no lo tiene.

    Existe para que quien dibuja la tira no tenga que saber que la banda vive en
    cuatro columnas sueltas. Devuelve None si falta cualquiera de las cuatro: media
    banda no se puede recortar.
    """
    if procedencia is None:
        return None
    coordenadas = tuple(procedencia.get(nombre) for nombre in COLUMNAS_DE_LA_BANDA)
    return None if any(valor is None for valor in coordenadas) else coordenadas


def _validar_registro_id(registro_id):
    """El `id` de la fila descrita. El motor no puede comprobarlo: la tabla es
    polimorfica y no tiene clave foranea (`docs/ARQUITECTURA.md` §2.7)."""
    if isinstance(registro_id, bool) or not isinstance(registro_id, int) or registro_id < 1:
        raise ErrorDeValidacion(
            f"El campo 'registro_id' no vale: se recibió {registro_id!r} y se "
            "esperaba el id de una fila, que es un entero mayor que cero."
        )
    return registro_id


def guardar_procedencia_de_campo(
    conexion,
    tabla,
    registro_id,
    campo,
    origen,
    confianza=None,
    valor_ocr=None,
    banda=None,
    anulado_por_tachon=0,
):
    """Guarda o actualiza de donde salio un campo. Devuelve el id de la fila.

    Hay una sola fila por (tabla, registro_id, campo) —lo impone el `UNIQUE` del
    esquema—, asi que volver a extraer el mismo campo actualiza la fila en vez de
    anadir otra. Las tres columnas de verificacion no se tocan aqui: ver el aviso
    de la cabecera del modulo.

    ⚠️ La banda **no se borra al corregir a mano**, y es a proposito. Cuando la
    pantalla de correccion escribe `origen='manual'` no pasa ninguna banda, y en
    ese caso la que ya estaba guardada se conserva: la tira del escaneo tiene que
    seguir viendose despues de teclear encima, porque es contra lo que se
    comprueba lo tecleado. Por lo mismo que `valor_ocr` conserva lo que el lector
    habia leido.
    """
    tabla = _validar_tabla(tabla)
    registro_id = _validar_registro_id(registro_id)
    campo = _validar_nombre_de_campo(campo)
    origen = _validar_origen(origen)
    confianza = _validar_confianza(confianza)
    x0, y0, x1, y1 = _validar_banda(banda)
    anulado_por_tachon = _validar_tachon(anulado_por_tachon)

    conexion.execute(
        "INSERT INTO procedencia_campo (tabla, registro_id, campo, origen, confianza, "
        "valor_ocr, banda_x0, banda_y0, banda_x1, banda_y1, anulado_por_tachon) "
        "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?) "
        "ON CONFLICT (tabla, registro_id, campo) DO UPDATE SET "
        "origen = excluded.origen, confianza = excluded.confianza, "
        "valor_ocr = excluded.valor_ocr, "
        "banda_x0 = COALESCE(excluded.banda_x0, procedencia_campo.banda_x0), "
        "banda_y0 = COALESCE(excluded.banda_y0, procedencia_campo.banda_y0), "
        "banda_x1 = COALESCE(excluded.banda_x1, procedencia_campo.banda_x1), "
        "banda_y1 = COALESCE(excluded.banda_y1, procedencia_campo.banda_y1), "
        "anulado_por_tachon = excluded.anulado_por_tachon",
        (tabla, registro_id, campo, origen, confianza, valor_ocr, x0, y0, x1, y1,
         anulado_por_tachon),
    )
    return leer_procedencia_de_campo(conexion, tabla, registro_id, campo)["id"]


def leer_procedencia_de_campo(conexion, tabla, registro_id, campo):
    """La procedencia de un campo concreto, o None si nadie la guardo."""
    fila = conexion.execute(
        "SELECT id, tabla, registro_id, campo, origen, confianza, valor_ocr, "
        "verificado, verificado_por, verificado_en, "
        "banda_x0, banda_y0, banda_x1, banda_y1, anulado_por_tachon "
        "FROM procedencia_campo "
        "WHERE tabla = ? AND registro_id = ? AND campo = ?",
        (_validar_tabla(tabla), registro_id, campo),
    ).fetchone()
    return dict(fila) if fila is not None else None


def leer_procedencia_del_registro(conexion, tabla, registro_id):
    """Toda la procedencia de una fila, por nombre de campo en orden alfabetico."""
    filas = conexion.execute(
        "SELECT id, tabla, registro_id, campo, origen, confianza, valor_ocr, "
        "verificado, verificado_por, verificado_en, "
        "banda_x0, banda_y0, banda_x1, banda_y1, anulado_por_tachon "
        "FROM procedencia_campo "
        "WHERE tabla = ? AND registro_id = ? ORDER BY campo",
        (_validar_tabla(tabla), registro_id),
    ).fetchall()
    return [dict(fila) for fila in filas]


def procedencia_por_campo(conexion, tabla, registro_id):
    """Lo mismo que `leer_procedencia_del_registro`, indexado por nombre de campo.

    La pantalla de correccion pregunta «¿de donde salio la fecha de viaje?» una vez
    por campo dibujado. Con la lista hay que recorrerla entera cada vez; con esto
    se lee una vez y se consulta por nombre.
    """
    return {
        fila["campo"]: fila
        for fila in leer_procedencia_del_registro(conexion, tabla, registro_id)
    }


def marcar_campo_verificado(conexion, tabla, registro_id, campo, companero_id):
    """Deja constancia de que una persona concreta dio un campo por bueno.

    Regla permanente 5: nada se marca como verificado automaticamente. Por eso esta
    funcion existe aparte de `guardar_procedencia_de_campo` y exige el compañero
    que lo confirma: sin quien y sin cuando, el `CHECK` del esquema rechaza la fila.
    """
    tabla = _validar_tabla(tabla)
    if companero_id is None:
        raise ErrorDeValidacion(
            "No se puede marcar un campo como verificado sin decir quién lo "
            "verificó: 'companero_id' llegó vacío."
        )
    if leer_procedencia_de_campo(conexion, tabla, registro_id, campo) is None:
        raise ErrorDeValidacion(
            f"No hay procedencia guardada para el campo {campo!r} de la fila "
            f"{registro_id!r} de {tabla!r}: no se puede verificar lo que no se leyó."
        )

    conexion.execute(
        "UPDATE procedencia_campo SET verificado = 1, verificado_por = ?, verificado_en = ? "
        "WHERE tabla = ? AND registro_id = ? AND campo = ?",
        (companero_id, marca_de_tiempo(), tabla, registro_id, campo),
    )
    return leer_procedencia_de_campo(conexion, tabla, registro_id, campo)


def desmarcar_campo_verificado(conexion, tabla, registro_id, campo):
    """Deshace la firma de un campo: vuelve a «nadie lo ha dado por bueno».

    **Por que hacia falta.** Hasta el 2026-09-03 este modulo tenia
    `marcar_campo_verificado` y ninguna inversa —13 funciones, ninguna
    desverificaba—, y la pantalla de correccion lo decia en su propio dialogo:
    «esto no se puede deshacer desde el programa». Era cierto, y convertia un error
    de dos pulsaciones en algo que solo se arreglaba abriendo la base con otra
    herramienta. Un freno sin marcha atras no es un freno: es una trampa.

    **Las tres columnas se borran juntas y no una.** El `CHECK` del esquema exige
    que o esten las tres puestas o esten las tres vacias
    (`datos/esquema.py:177`), asi que un `UPDATE` que solo bajara `verificado` a 0
    lo rechazaria el motor. Se escriben las tres en la misma instruccion.

    **Lo que NO se toca, y es a proposito:** `origen`, `confianza` y `valor_ocr`.
    Desverificar significa «nadie ha dado esto por bueno», no «esto no se leyo».
    Perder de donde salio el valor al retirar una firma seria borrar la lectura por
    corregir la firma.

    ⚠️ **Lo que este proyecto NO guarda todavia, dicho:** quien deshizo la firma y
    cuando. La tabla no tiene columnas para eso y anadirlas es una migracion del
    esquema, que es decision del planificador y no de esta funcion. Mientras tanto,
    lo que queda escrito es el estado —sin verificar— y no la historia.

    Es idempotente: deshacer un campo que nunca se firmo lo deja igual, y eso es un
    resultado normal. Lo que si rechaza es un campo sin procedencia, por el mismo
    motivo que `marcar_campo_verificado`: no se dice nada de lo que no se leyo.
    """
    tabla = _validar_tabla(tabla)
    if leer_procedencia_de_campo(conexion, tabla, registro_id, campo) is None:
        raise ErrorDeValidacion(
            f"No hay procedencia guardada para el campo {campo!r} de la fila "
            f"{registro_id!r} de {tabla!r}: no se puede deshacer la verificación de "
            "lo que no se leyó."
        )

    conexion.execute(
        "UPDATE procedencia_campo "
        "SET verificado = 0, verificado_por = NULL, verificado_en = NULL "
        "WHERE tabla = ? AND registro_id = ? AND campo = ?",
        (tabla, registro_id, campo),
    )
    return leer_procedencia_de_campo(conexion, tabla, registro_id, campo)


def retirar_la_firma_de_los_campos(conexion, tabla, registro_id, campos):
    """Suelta la firma de los campos que se le nombren. Devuelve cuales solto.

    **Para que existe, y no es lo mismo que `desmarcar_campo_verificado`.** Aquella
    es el gesto de Miguel —«retiro lo que di por bueno»— y por eso levanta si el
    campo no tiene procedencia: se le esta diciendo algo de un campo que no se leyo.
    Esta es la **consecuencia** de haber cambiado un valor, y ahi un campo sin
    procedencia, o sin firmar, no es un error: es que no habia ninguna firma que
    retirar. Por eso los salta en silencio en vez de levantar.

    Se le pasan los campos que cambiaron y **ella decide cuales tenian firma**. Quien
    la llama sabe que valores escribio; saber cuales de ellos estaban firmados es de
    aqui, que es donde vive esa columna.

    Devuelve la lista de los que solto, y no un numero, para que quien la llame
    pueda decir en pantalla **cuales** dejaron de estar firmados. Una firma que se
    cae sin decirlo es el mismo silencio que hacia peligroso el defecto de firmar
    un campo vacio.
    """
    tabla = _validar_tabla(tabla)
    retiradas = []
    for campo in campos:
        fila = leer_procedencia_de_campo(conexion, tabla, registro_id, campo)
        if fila is None or not fila["verificado"]:
            continue
        desmarcar_campo_verificado(conexion, tabla, registro_id, campo)
        retiradas.append(campo)
    return retiradas
