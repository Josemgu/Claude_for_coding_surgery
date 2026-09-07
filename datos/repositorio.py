"""Alta, lectura y actualizacion de casos y personas.

Cada instruccion SQL de este modulo es una cadena literal escrita dentro de la
llamada, con marcadores `?`, y todos los valores viajan aparte en la tupla de
parametros. El motor recibe la logica y los datos por separado: un intento de
inyeccion llega como dato inerte (`docs/ARQUITECTURA.md` §1.7).

No hay ninguna funcion de borrado, y es a proposito: en este sistema los casos se
archivan y las personas no se borran nunca (FASE 8). Lo que el motor defiende con
`RESTRICT`, este modulo lo defiende no ofreciendo la operacion.

⚠️ **Y una regla que este modulo sostiene entero: un valor que cambia suelta su
firma.** Una firma dice «yo, Miguel, di por bueno ESTE valor»; en cuanto el valor
es otro, la firma ya no habla de el. Vive aqui —en las funciones que ESCRIBEN el
valor— y no en la pantalla que las llama, porque es igual de cierta venga el cambio
de la pantalla de correccion, de la reconciliacion del Excel de los companeros
(FASE 6) o de una pantalla que todavia no existe. Puesta en la interfaz, cada
puerta nueva tendria que acordarse.

El defecto que cierra, medido por QA el 2026-09-03: un caso firmado entero al que
se le borraba despues la fecha de viaje se quedaba **verificado, con fecha y hora,
sin fecha de viaje**, y desaparecia a la vez de `casos_pendientes_de_verificar`, de
`casos_que_viajan_pronto` y de `casos_en_riesgo`. Es el gemelo del defecto de
firmar lo vacio, por la puerta contraria: aquel firmaba lo vacio, este vaciaba lo
firmado.

Se eligio esto y no la otra salida que QA planteaba —que el programa **impida**
editar un campo firmado sin deshacer antes—. El motivo es el coste de corregir:
deshacer es por caso, no por campo, asi que arreglar una letra de una fecha
obligaria a tirar las 36 firmas del caso y volver a ponerlas. Eso castiga corregir,
y un programa que castiga corregir acaba con datos malos sin corregir. Ninguna de
las dos rompe la regla permanente 5 —retirar una firma no marca nada como
verificado, y Miguel sigue firmando solo cuando pulsa—, pero solo una deja el
camino de la correccion abierto.
"""

from collections import namedtuple

from datos.esquema import marca_de_tiempo
from datos.procedencia import retirar_la_firma_de_los_campos
from datos.validacion import (
    ErrorDeValidacion,
    avisos_de_caso,
    validar_estado_recomendacion,
    validar_fecha_viaje,
    validar_mrn,
    validar_numero_caso,
    validar_numero_caso_si_lo_hay,
    validar_pagina_pdf,
    validar_unidad_nombre,
    validar_unidad_numero,
)

ResultadoDeAlta = namedtuple("ResultadoDeAlta", ("id", "avisos"))

# Las seis casillas de ordenanzas, en el orden literal de DECISIONES.md.
CASILLAS_DE_ORDENANZAS = (
    "ord_recibir_propias",
    "ord_observar_sellamiento",
    "ord_traductor",
    "ord_investidura",
    "ord_sellamiento_esposos",
    "ord_sellamiento_hijo_padres",
)

_SIN_CAMBIO = object()


def _campos_cuyo_valor_cambio(actual, nuevos):
    """Los nombres de las columnas cuyo valor nuevo no es el que estaba guardado.

    Se compara **despues de validar**, no con lo que llego: los validadores
    normalizan —`validar_mrn` sobre todo—, y comparar lo crudo contra lo guardado
    marcaria como cambiado un valor que se escribe igual.
    """
    return [nombre for nombre, valor in nuevos.items() if actual[nombre] != valor]


def _validar_casilla(nombre, valor):
    """Una casilla vale 1 (marcada), 0 (no marcada) o None (no leida)."""
    if valor is None or valor in (0, 1):
        return valor
    raise ErrorDeValidacion(
        f"El campo '{nombre}' no vale: se recibió {valor!r} y se esperaba "
        "1 (marcada), 0 (no marcada) o nada (no leída)."
    )


def alta_de_caso(
    conexion,
    numero_caso,
    unidad_numero=None,
    fecha_viaje=None,
    captura_manual=0,
    ruta_pdf=None,
    estado_recomendacion=None,
    pagina_pdf=None,
    unidad_nombre=None,
    templo_nombre=None,
    duplicado_de=None,
):
    """Guarda un caso nuevo y devuelve su id junto con los avisos que produjo.

    Los avisos viajan pegados al resultado a proposito: si `alta_de_caso` solo
    devolviera el id, la pantalla que lo llama podria guardar una fecha de mes
    cruzado sin que nadie se entere.

    `pagina_pdf` y `unidad_nombre` entraron con la version 3 del esquema. Los dos
    los leia ya la extraccion y los dos se perdian: sin la pagina, abrir el PDF de
    un caso que es el sexto formulario del documento lleva a la pagina 1 y hay que
    buscarlo a mano; sin el nombre, la vista de mes solo puede ensenar un numero
    de unidad, que no dice nada a quien lo mira.

    `templo_nombre` entro con la version 10 y es la misma historia una vez mas: el
    templo esta impreso en el papel y se descartaba. Va como texto libre y **sin
    catalogo** (`DECISIONES.md`, 2026-09-03): la lista de templos con sus colores es
    del dueno y todavia no existe, y validar contra una lista inventada rechazaria
    manana un templo verdadero. Por eso no lleva validador, al contrario que los
    otros seis: no hay ninguna regla que aplicarle que no sea inventada.

    `duplicado_de` entro con la version 13 y es el id del caso que este REPITE, o
    None. Lo decidio el dueno el 2026-09-03: «Si hay documentos duplicados debe
    decirlo y no rechazarlo». El duplicado entra como cualquier otro caso y queda
    marcado; el que ya estaba **no se toca**, con sus correcciones a mano y sus
    firmas intactas. No lleva validador propio: el `REFERENCES` del esquema es
    quien rechaza un id que no existe, y hacerlo dos veces solo daria dos mensajes
    distintos para el mismo fallo.
    """
    # `..._si_lo_hay` y no `validar_numero_caso`: desde la version 7 del esquema
    # una pagina cuyo numero no se pudo leer se guarda igual —pendiente de
    # identificar— en vez de tirarse entera con todo lo que si se habia leido.
    # El numero se teclea despues en la pantalla de correccion; NO se inventa.
    numero_caso = validar_numero_caso_si_lo_hay(numero_caso)
    unidad_numero = validar_unidad_numero(unidad_numero)
    fecha_viaje = validar_fecha_viaje(fecha_viaje)
    estado_recomendacion = validar_estado_recomendacion(estado_recomendacion)
    captura_manual = _validar_casilla("captura_manual", captura_manual)
    pagina_pdf = validar_pagina_pdf(pagina_pdf)
    unidad_nombre = validar_unidad_nombre(unidad_nombre)

    cursor = conexion.execute(
        "INSERT INTO casos (numero_caso, unidad_numero, fecha_viaje, captura_manual, "
        "ruta_pdf, creado_en, estado_recomendacion, pagina_pdf, unidad_nombre, "
        "templo_nombre, duplicado_de) "
        "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
        (
            numero_caso,
            unidad_numero,
            fecha_viaje,
            captura_manual,
            ruta_pdf,
            marca_de_tiempo(),
            estado_recomendacion,
            pagina_pdf,
            unidad_nombre,
            templo_nombre,
            duplicado_de,
        ),
    )
    return ResultadoDeAlta(cursor.lastrowid, avisos_de_caso(numero_caso, fecha_viaje))


def alta_de_persona(
    conexion, caso_id, mrn=None, nombre=None, fila_formulario=None, pagina_pdf=None,
    **casillas,
):
    """Guarda una persona de un caso y devuelve su id.

    Una fila sin nombre y sin MRN no es una persona: se rechaza aqui, antes de
    que el `CHECK` del motor tenga que hacerlo, para poder decir por que.

    `pagina_pdf` entro con la version 4 del esquema y es la hoja del PDF de la
    que salio ESTA persona, que no siempre es la del caso: un formulario de grupo
    reparte doce personas en seis hojas y el caso guarda solo la que lo abrio. Sin
    esta columna, la tira del escaneo de las once personas de las hojas 2 a 6 se
    recortaba de la hoja 1.
    """
    mrn = validar_mrn(mrn)
    pagina_pdf = validar_pagina_pdf(pagina_pdf)
    if nombre is None and mrn is None:
        raise ErrorDeValidacion(
            "No se puede guardar una persona sin 'nombre' y sin 'mrn': una fila "
            "del formulario sin ninguno de los dos se descarta."
        )
    desconocidas = set(casillas) - set(CASILLAS_DE_ORDENANZAS)
    if desconocidas:
        raise ErrorDeValidacion(
            f"Campos que no existen en 'personas': {sorted(desconocidas)}. "
            f"Las casillas de ordenanzas son {list(CASILLAS_DE_ORDENANZAS)}."
        )
    valores_de_casillas = tuple(
        _validar_casilla(nombre_casilla, casillas.get(nombre_casilla))
        for nombre_casilla in CASILLAS_DE_ORDENANZAS
    )

    cursor = conexion.execute(
        "INSERT INTO personas (caso_id, mrn, nombre, fila_formulario, pagina_pdf, "
        "ord_recibir_propias, ord_observar_sellamiento, ord_traductor, "
        "ord_investidura, ord_sellamiento_esposos, ord_sellamiento_hijo_padres) "
        "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
        (caso_id, mrn, nombre, fila_formulario, pagina_pdf) + valores_de_casillas,
    )
    return cursor.lastrowid


def leer_casos_por_numero(conexion, numero_caso):
    """TODOS los casos que llevan ese numero, del mas antiguo al mas nuevo.

    ⚠️ **Son varios, y esa es la novedad de la version 12 del esquema.** El numero
    de caso son cuatro letras mas el ano y el mes: identifica una UNIDAD Y UN MES,
    no una familia, y en la carpeta real del dueno muchos documentos distintos lo
    comparten. Mientras `numero_caso` fue `UNIQUE`, seis de cada diez documentos de
    una importacion suya se rechazaban enteros (`DECISIONES.md`, 2026-09-03).

    Devuelve una lista, vacia cuando no hay ninguno. Quien pregunte «¿esta este
    numero?» tiene que decidir que hace con dos respuestas, y una funcion que
    devolviera solo la primera se lo ocultaria.
    """
    filas = conexion.execute(
        "SELECT id, numero_caso, unidad_numero, fecha_viaje, captura_manual, "
        "archivado, fecha_archivado, ruta_pdf, creado_en, estado_recomendacion, "
        "pagina_pdf, unidad_nombre, templo_nombre, duplicado_de "
        "FROM casos WHERE numero_caso = ? ORDER BY id",
        (numero_caso,),
    ).fetchall()
    return [dict(fila) for fila in filas]


def leer_caso_por_numero(conexion, numero_caso):
    """El PRIMER caso con ese numero —el mas antiguo—, o None si no hay ninguno.

    ⚠️ Desde la version 12 un numero puede llevar a varios casos, asi que esta
    funcion ya no identifica a nadie: dice si ese numero esta en la base y cual fue
    el primero. Quien necesite saber cuantos hay llama a `leer_casos_por_numero`.
    Se conserva porque las pantallas que solo quieren enseñar «el caso CASP2609»
    siguen queriendo eso, y porque quitarla obligaria a tocar codigo que este pase
    no revisa.
    """
    casos = leer_casos_por_numero(conexion, numero_caso)
    return casos[0] if casos else None


def leer_caso_por_id(conexion, caso_id):
    """El mismo caso, buscado por su `id`, o None si no esta.

    Existe porque las listas de la pantalla de inicio traen el `id` y no el
    numero: entrar a un caso desde ahi no deberia pasar por una segunda busqueda
    por texto sobre una columna que ya se resolvio.
    """
    fila = conexion.execute(
        "SELECT id, numero_caso, unidad_numero, fecha_viaje, captura_manual, "
        "archivado, fecha_archivado, ruta_pdf, creado_en, estado_recomendacion, "
        "pagina_pdf, unidad_nombre, templo_nombre, duplicado_de "
        "FROM casos WHERE id = ?",
        (caso_id,),
    ).fetchone()
    return dict(fila) if fila is not None else None


def leer_personas_del_caso(conexion, caso_id):
    """Las personas de un caso, en el orden en que venian en el formulario."""
    filas = conexion.execute(
        "SELECT id, caso_id, mrn, nombre, fila_formulario, pagina_pdf, "
        "ord_recibir_propias, ord_observar_sellamiento, ord_traductor, "
        "ord_investidura, ord_sellamiento_esposos, ord_sellamiento_hijo_padres "
        "FROM personas WHERE caso_id = ? ORDER BY fila_formulario, id",
        (caso_id,),
    ).fetchall()
    return [dict(fila) for fila in filas]


def _caso_por_id(conexion, caso_id):
    fila = conexion.execute(
        "SELECT numero_caso, unidad_numero, fecha_viaje, estado_recomendacion, "
        "unidad_nombre FROM casos WHERE id = ?",
        (caso_id,),
    ).fetchone()
    if fila is None:
        raise ErrorDeValidacion(f"No hay ningun caso con el id {caso_id!r}.")
    return fila


def actualizar_datos_del_caso(
    conexion,
    caso_id,
    unidad_numero=_SIN_CAMBIO,
    fecha_viaje=_SIN_CAMBIO,
    estado_recomendacion=_SIN_CAMBIO,
    unidad_nombre=_SIN_CAMBIO,
):
    """Corrige a mano los cuatro campos del caso que se corrigen a mano.

    Lo que no se pasa, no se toca. Devuelve los avisos que deja el caso ya
    actualizado, para que quien corrige vea al instante si acaba de cruzar el mes.

    `numero_caso` no esta y no se anade: es la mitad de la clave con la que se
    reconcilia el Excel que vuelve (FASE 6), y cambiarlo aqui la rompe sin que
    nada avise. (Desde la version 12 del esquema ya **no** es `UNIQUE` —el numero
    identifica una unidad y un mes, no una familia—, pero eso no lo hace editable:
    lo que defiende esta puerta cerrada es el Excel que un companero ya tiene
    delante con el numero viejo escrito.)

    **Un campo cuyo valor cambia pierde su firma**, por lo que dice la cabecera del
    modulo. `estado_recomendacion` entra en la comparacion igual que los otros tres
    aunque hoy no lleve fila de procedencia: `retirar_la_firma_de_los_campos` salta
    sola lo que no esta firmado, y asi la regla cubre las cuatro columnas que esta
    funcion escribe y no tres de cuatro.
    """
    actual = _caso_por_id(conexion, caso_id)
    nueva_unidad = actual["unidad_numero"] if unidad_numero is _SIN_CAMBIO else unidad_numero
    nueva_fecha = actual["fecha_viaje"] if fecha_viaje is _SIN_CAMBIO else fecha_viaje
    nuevo_estado = (
        actual["estado_recomendacion"]
        if estado_recomendacion is _SIN_CAMBIO
        else estado_recomendacion
    )
    nuevo_nombre_de_unidad = (
        actual["unidad_nombre"] if unidad_nombre is _SIN_CAMBIO else unidad_nombre
    )

    nueva_unidad = validar_unidad_numero(nueva_unidad)
    nueva_fecha = validar_fecha_viaje(nueva_fecha)
    nuevo_estado = validar_estado_recomendacion(nuevo_estado)
    nuevo_nombre_de_unidad = validar_unidad_nombre(nuevo_nombre_de_unidad)

    cambiados = _campos_cuyo_valor_cambio(
        actual,
        {
            "unidad_numero": nueva_unidad,
            "fecha_viaje": nueva_fecha,
            "estado_recomendacion": nuevo_estado,
            "unidad_nombre": nuevo_nombre_de_unidad,
        },
    )

    conexion.execute(
        "UPDATE casos SET unidad_numero = ?, fecha_viaje = ?, estado_recomendacion = ?, "
        "unidad_nombre = ? WHERE id = ?",
        (nueva_unidad, nueva_fecha, nuevo_estado, nuevo_nombre_de_unidad, caso_id),
    )
    retirar_la_firma_de_los_campos(conexion, "casos", caso_id, cambiados)
    return avisos_de_caso(actual["numero_caso"], nueva_fecha)


def _casillas_ya_guardadas(conexion, persona_id):
    """Las seis casillas tal como estan hoy en la base, para la mezcla."""
    return conexion.execute(
        "SELECT nombre, mrn, ord_recibir_propias, ord_observar_sellamiento, "
        "ord_traductor, ord_investidura, ord_sellamiento_esposos, "
        "ord_sellamiento_hijo_padres FROM personas WHERE id = ?",
        (persona_id,),
    ).fetchone()


def _casillas_mezcladas(actual, casillas):
    """Las seis casillas con los cambios pedidos encima de las que ya estaban.

    Una casilla que no se nombra conserva su valor; una que se nombra con `None`
    vuelve a «no leida», que es una marcha atras legitima y por eso se distingue
    de no nombrarla. Ese `None` es el motivo por el que no vale el truco de usar
    `None` como «sin cambio»: aqui `None` significa algo.
    """
    desconocidas = set(casillas) - set(CASILLAS_DE_ORDENANZAS)
    if desconocidas:
        raise ErrorDeValidacion(
            f"Campos que no existen en 'personas': {sorted(desconocidas)}. "
            f"Las casillas de ordenanzas son {list(CASILLAS_DE_ORDENANZAS)}."
        )
    return tuple(
        _validar_casilla(nombre, casillas[nombre])
        if nombre in casillas
        else actual[nombre]
        for nombre in CASILLAS_DE_ORDENANZAS
    )


def actualizar_datos_de_persona(
    conexion, persona_id, nombre=_SIN_CAMBIO, mrn=_SIN_CAMBIO, **casillas
):
    """Corrige a mano el nombre, el MRN y las seis casillas. Lo que no se pasa, no se toca.

    Las casillas entraron aqui con la FASE 3, y hasta entonces no habia ninguna
    forma de resolverlas: solo `alta_de_persona` las escribia, y solo al importar.
    Como la lectura automatica de casillas viene desactivada a falta de calibrar
    (`extraccion/casillas.py`), TODAS llegan en «no leida», y sin esta puerta las
    seis de cada persona se quedaban asi para siempre.

    **Un campo cuyo valor cambia pierde su firma**, por lo que dice la cabecera del
    modulo, y las seis casillas cuentan igual que el nombre y el MRN: una ordenanza
    que Miguel firmo como «sí» y luego pasa a «no» tampoco sigue firmada.
    """
    actual = _casillas_ya_guardadas(conexion, persona_id)
    if actual is None:
        raise ErrorDeValidacion(f"No hay ninguna persona con el id {persona_id!r}.")

    nuevo_nombre = actual["nombre"] if nombre is _SIN_CAMBIO else nombre
    nuevo_mrn = validar_mrn(actual["mrn"] if mrn is _SIN_CAMBIO else mrn)
    if nuevo_nombre is None and nuevo_mrn is None:
        raise ErrorDeValidacion(
            "No se puede dejar una persona sin 'nombre' y sin 'mrn': quedaria una "
            "fila en blanco."
        )
    valores_de_casillas = _casillas_mezcladas(actual, casillas)
    cambiados = _campos_cuyo_valor_cambio(
        actual,
        {"nombre": nuevo_nombre, "mrn": nuevo_mrn}
        | dict(zip(CASILLAS_DE_ORDENANZAS, valores_de_casillas)),
    )

    conexion.execute(
        "UPDATE personas SET nombre = ?, mrn = ?, ord_recibir_propias = ?, "
        "ord_observar_sellamiento = ?, ord_traductor = ?, ord_investidura = ?, "
        "ord_sellamiento_esposos = ?, ord_sellamiento_hijo_padres = ? WHERE id = ?",
        (nuevo_nombre, nuevo_mrn) + valores_de_casillas + (persona_id,),
    )
    retirar_la_firma_de_los_campos(conexion, "personas", persona_id, cambiados)
    return persona_id


def asignar_numero_de_caso(conexion, caso_id, numero_caso):
    """Pone el numero a un caso que entro sin el. Devuelve los avisos del caso.

    Es la unica puerta por la que `numero_caso` se escribe despues del alta, y solo
    abre en un sentido: **de «no se sabe» a un numero**. Un caso que YA tiene
    numero no se puede cambiar por aqui, y esa restriccion es el motivo entero de
    que esta funcion exista aparte de `actualizar_datos_del_caso`.

    Lo que defiende: `numero_caso` es la mitad del par que reconcilia el Excel que
    devuelven los companeros (`paquete/reconciliacion.py`). Cambiar el numero de un
    caso que ya lo tenia rompe ese par en silencio —el Excel que un companero tiene
    delante sigue diciendo el numero viejo— y no hay forma de que nadie se entere.
    Rellenar uno vacio no rompe nada: no habia par que romper.

    El aviso del mes cruzado se devuelve ya recalculado. Es el momento exacto en
    que se puede calcular por primera vez: ese aviso compara la fecha de viaje con
    el mes que dice el numero de caso, y hasta ahora no habia numero con el que
    comparar.
    """
    actual = _caso_por_id(conexion, caso_id)
    if actual["numero_caso"] is not None:
        raise ErrorDeValidacion(
            f"El caso {caso_id} ya tiene el número {actual['numero_caso']!r} y no se "
            "puede cambiar por aquí. El número de caso es la mitad de la clave con "
            "la que se reconcilia el Excel que devuelven los compañeros: cambiarlo "
            "rompería ese par sin que nadie se entere."
        )
    numero_caso = validar_numero_caso(numero_caso)
    conexion.execute(
        "UPDATE casos SET numero_caso = ? WHERE id = ?", (numero_caso, caso_id)
    )
    # La misma regla que en las otras dos: esta funcion escribe un valor, asi que
    # suelta su firma. ⚠️ Por la pantalla no puede disparar nunca —el numero solo
    # se escribe cuando estaba vacio, y un campo vacio no se llega a firmar
    # (`interfaz/correccion.py`, `_campos_que_se_confirman`)—, y se pone igual para
    # que la regla sea «ninguna funcion de este modulo cambia un valor dejando su
    # firma» y no «dos de las tres». La excepcion es lo que se olvida.
    retirar_la_firma_de_los_campos(conexion, "casos", caso_id, ("numero_caso",))
    return avisos_de_caso(numero_caso, actual["fecha_viaje"])
