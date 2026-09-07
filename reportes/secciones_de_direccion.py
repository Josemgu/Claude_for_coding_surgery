"""Las seis secciones del informe a la dirección, y su portada.

Son las del informe del proyecto viejo (`salida/informe.py`), que es el que el
dueño aprobó: quiénes viajaron sin la preparación completa, los viajes, a qué van
al templo, dónde se traban las preparaciones, qué unidades acumulan pendientes y
cómo está repartido el trabajo del equipo. Van **delante** de las tres secciones de
la FASE 8, que siguen intactas en `reportes/documento.py`.

Las dos últimas —«Unidades con preparaciones sin completar» y «El equipo»— entraron
el 2026-09-03: las tiene el informe viejo, los datos ya estaban en la base y no se
estaban usando. «De qué país viajan», la otra que el viejo tiene y aquí no, espera
al ADR-0002: el país no está impreso en el formulario y sale de un catálogo que
nadie ha decidido.

Viven aquí y no allí porque son otra pregunta. `documento.py` arma el reporte de
trabajo —qué hay en la base y en qué estado—; esto arma el documento que lee
alguien que no abre este programa nunca, y que tiene que contarse solo. Juntos, el
archivo pasaba de 500 líneas y había que leerlo entero para cambiar un rótulo.

**Aquí no se cuenta nada.** Las cuentas están en `reportes/preparacion.py`; esto
las escribe en español y las coloca en columnas. Es el mismo reparto que hay entre
`metricas.py` y las secciones de la FASE 8.

**Sin dinero, en ninguna de las seis.** Citado del viejo: el presupuesto lo lleva
otro departamento, y meterlo aquí solo serviría para discutir de lo que no es.

**`SIN_UNIDAD` y `SIN_AGENTE` se importan de `reportes/formato.py`** y no se
escriben aquí: los usan DOS módulos —`preparacion.py` en la columna «Asignado a» de
«Los viajes» y este en «El equipo»—, y dos frases distintas para lo mismo se leen
como dos cosas distintas.
"""

from datos.ordenanzas import contar as contar_ordenanzas
from datos.pasos import estado_de_los_pasos, pasos_sin_completar
from datos.repositorio import CASILLAS_DE_ORDENANZAS
from reportes import preparacion
from reportes.formato import (
    CRUDO,
    SIN_AGENTE,
    SIN_DATO,
    SIN_UNIDAD,
    TEMPORAL,
    TEXTO,
    Cifra,
    Columna,
    Portada,
    Seccion,
    TITULAR_DE_LA_PORTADA,
    TONO_BUENO,
    TONO_MALO,
    TONO_NEUTRO,
    persona_o_sin_nombre,
    unidad_con_su_numero,
)


# ⚠️ Estas tablas dicen «Caso» donde las de la FASE 8 dicen «N.º de caso», y no es
# un descuido: son las columnas del informe del proyecto viejo, que es el que el
# dueño aprobó, y se copian tal como estaban. Unificar el rótulo cambiaría el
# documento que él ya dio por bueno para ganar una coherencia que nadie pidió.
#
# ⚠️ **«Verificada» ya no se dice en ninguna de estas tablas, desde el
# 2026-09-03.** La palabra significaba dos cosas a la vez en el mismo programa: en
# la pantalla de corrección es la FIRMA de Miguel sobre un campo leído, y aquí eran
# los seis pasos que contesta un compañero. Un informe que dice «12 verificadas» al
# lado de una pantalla que dice «36 campos verificados» hace creer que hablan de lo
# mismo. Aquí se dice **«con la preparación completa»** y **«sin la preparación
# completa»**; la firma de Miguel se sigue llamando verificación en las métricas del
# final del informe (`reportes/documento.py`), que es donde de verdad es eso.
_COLUMNAS_DE_QUIEN_VIAJO_SIN_LA_PREPARACION = (
    Columna("Persona", CRUDO, 30),
    Columna("Barrio o rama", CRUDO, 24),
    Columna("Caso", TEXTO, 12),
    Columna("Viajó el", TEMPORAL, 14),
    Columna("Qué pasó", CRUDO, 40),
)

_COLUMNAS_DE_LOS_VIAJES = (
    Columna("Caso", TEXTO, 12),
    Columna("País", CRUDO, 16),
    Columna("Templo", CRUDO, 20),
    Columna("Sale", TEMPORAL, 14),
    Columna("Asignado a", CRUDO, 24),
    Columna("Viajan", CRUDO, 9),
    Columna("Con la preparación completa", CRUDO, 18),
    Columna("Sin la preparación completa", CRUDO, 18),
)

_COLUMNAS_DE_LAS_ORDENANZAS = (
    Columna("A qué va al templo", CRUDO, 34),
    Columna("Personas", CRUDO, 12),
)

_COLUMNAS_DE_LOS_PASOS_TRABADOS = (
    Columna("Paso del sistema del líder", CRUDO, 34),
    Columna("Veces sin completar", CRUDO, 20),
)

_COLUMNAS_DE_LAS_UNIDADES = (
    Columna("Barrio o rama", CRUDO, 34),
    Columna("Personas", CRUDO, 12),
    Columna("Sin la preparación completa", CRUDO, 18),
)

_COLUMNAS_DEL_EQUIPO = (
    Columna("Agente", CRUDO, 26),
    Columna("Personas a su cargo", CRUDO, 16),
    Columna("Con la preparación completa", CRUDO, 18),
    Columna("Sin mirar", CRUDO, 12),
)

# Lo que se escribe en «País». La base no lo guarda: el dueno lo pidio el
# 2026-09-03 —«nombre, barrio, cedula, fecha de viaje, numero de caso, mas pais y
# templo»— y de los dos, el templo YA se lee (version 10 del esquema) y el pais no:
# no esta impreso en el formulario y sale de un catalogo que espera al ADR-0002. La
# columna se escribe igual porque es la del informe que el aprobo, y se rellena con
# la palabra que dice que no consta en vez de en blanco: un hueco en un informe a la
# direccion se lee como un dato que se perdio.
_EL_PAIS_NO_SE_GUARDA = SIN_DATO


def seccion_de_quien_viajo_sin_la_preparacion(sin_completar):
    """Con nombre y unidad, porque cada renglon es una persona que no recibio nada."""
    filas = tuple(
        (
            persona_o_sin_nombre(persona),
            unidad_con_su_numero(persona),
            persona["numero_caso"],
            persona["fecha_viaje"],
            preparacion.que_paso(persona),
        )
        for persona in sin_completar
    )
    return Seccion(
        titulo="Quiénes viajaron sin la preparación completa",
        notas=(
            "Con nombre y unidad, porque cada uno de estos renglones es una persona "
            "que fue al templo y no recibió su ordenanza.",
            "«Nadie la miró» y «no está completa» no son lo mismo y piden dos "
            "remedios distintos: la primera es trabajo que no se hizo, la segunda es "
            "trabajo que se hizo y encontró algo.",
        ),
        columnas=_COLUMNAS_DE_QUIEN_VIAJO_SIN_LA_PREPARACION,
        filas=filas,
        resumen=f"{len(filas)} personas viajaron sin la preparación completa",
    )


def seccion_de_los_viajes(renglones):
    """Un renglon por caso, con quien lo lleva en la misma fila."""
    filas = tuple(
        (
            renglon["numero_caso"],
            _EL_PAIS_NO_SE_GUARDA,
            renglon.get("templo") or SIN_DATO,
            renglon["fecha_viaje"],
            renglon["asignado_a"],
            renglon["viajan"],
            renglon["completas"],
            "—" if renglon["sin_completar"] is None else renglon["sin_completar"],
        )
        for renglon in renglones
    )
    return Seccion(
        titulo="Los viajes",
        notas=(
            "«Sin la preparación completa» sale con una raya en los casos que todavía "
            "no han salido: ahí lo que falta no es un fallo, es trabajo por hacer.",
            f"«País» sale como «{_EL_PAIS_NO_SE_GUARDA}» en todas las filas porque no "
            "está impreso en el formulario y su catálogo todavía no se ha decidido. "
            "«Templo» sí se lee del papel desde el 2026-09-03, y sale igual cuando el "
            "caso se importó antes de esa fecha o cuando el lector no lo encontró.",
        ),
        columnas=_COLUMNAS_DE_LOS_VIAJES,
        filas=filas,
        resumen=f"{len(filas)} casos en el período",
    )


def seccion_de_a_que_van(personas):
    """A que van al templo las personas del periodo, contado por ordenanza.

    Las casillas del formulario son marcas de tilde y el reconocimiento no las lee
    solo: por eso se dice ademas cuantas personas no traen ninguna marcada. Sin ese
    numero, una tabla corta se lee como «casi nadie va a nada».
    """
    sin_marcar = sum(
        1
        for persona in personas
        if not any(persona.get(nombre) for nombre in CASILLAS_DE_ORDENANZAS)
    )
    filas = tuple(contar_ordenanzas(personas))
    notas = [
        "Una persona puede ir a varias ordenanzas, así que estos números suman más "
        "que el total de personas. No es un error de cuenta.",
    ]
    if sin_marcar:
        notas.append(
            f"{sin_marcar} personas no traen ninguna casilla marcada. Esas casillas "
            "del formulario son marcas de tilde y hoy se ponen a mano."
        )
    return Seccion(
        titulo="A qué van al templo",
        notas=tuple(notas),
        columnas=_COLUMNAS_DE_LAS_ORDENANZAS,
        filas=filas,
        resumen=None,
    )


def seccion_de_donde_se_traban(personas):
    """En que paso del sistema del lider se quedan las preparaciones.

    Es la seccion accionable del informe: dice donde hay que acompanar mas a las
    unidades. Sale solo si hay algun paso marcado que no; una tabla de ceros no
    dice nada y ocupa el sitio de lo que si.
    """
    cuentas = {}
    for persona in personas:
        for trabado in pasos_sin_completar(persona):
            cuentas[trabado] = cuentas.get(trabado, 0) + 1
    if not cuentas:
        return None
    filas = tuple(sorted(cuentas.items(), key=lambda par: (-par[1], par[0])))
    return Seccion(
        titulo="Dónde se traban las preparaciones",
        notas=(
            "De los seis pasos del sistema del líder, estos son los que quedaron sin "
            "completar. Es donde hay que acompañar más a las unidades.",
        ),
        columnas=_COLUMNAS_DE_LOS_PASOS_TRABADOS,
        filas=filas,
        resumen=None,
    )


def _cuenta_por_unidad(personas):
    """Cuántas personas tiene cada unidad y cuántas sin la preparación completa."""
    por_unidad = {}
    for persona in personas:
        unidad = unidad_con_su_numero(persona) or SIN_UNIDAD
        cuenta = por_unidad.setdefault(unidad, {"personas": 0, "sin_completar": 0})
        cuenta["personas"] += 1
        if not preparacion.tiene_la_preparacion_completa(persona):
            cuenta["sin_completar"] += 1
    return por_unidad


def seccion_de_las_unidades(personas):
    """Qué unidades acumulan preparaciones sin completar. Es del informe viejo.

    Sale **solo si hay alguna unidad con algo pendiente**, igual que «Dónde se
    traban»: una tabla de ceros no dice nada y ocupa el sitio de lo que sí.

    Es la misma cuenta que «Dónde se traban» mirada por el otro lado, y por eso
    valen las dos: aquella dice QUÉ paso falla y esta dice EN QUÉ UNIDAD. Con las
    dos se sabe a quién llamar y de qué hablarle; con una sola, solo la mitad.

    Ordena por las que más deben, y a igualdad por nombre: sin el segundo criterio,
    dos informes del mismo período podrían listar las mismas unidades en distinto
    orden y leerse como si algo hubiera cambiado.
    """
    con_falta = sorted(
        (
            (unidad, cuenta)
            for unidad, cuenta in _cuenta_por_unidad(personas).items()
            if cuenta["sin_completar"]
        ),
        key=lambda par: (-par[1]["sin_completar"], par[0]),
    )
    if not con_falta:
        return None
    return Seccion(
        titulo="Unidades con preparaciones sin completar",
        notas=(
            "Es «Dónde se traban» mirado por el otro lado: aquella tabla dice qué "
            "paso falla y esta dice en qué unidad. Con las dos se sabe a quién "
            "llamar y de qué hablarle.",
            "Solo salen las unidades que tienen a alguien sin la preparación "
            "completa. Las que están al día no ocupan sitio.",
        ),
        columnas=_COLUMNAS_DE_LAS_UNIDADES,
        filas=tuple(
            (unidad, cuenta["personas"], cuenta["sin_completar"])
            for unidad, cuenta in con_falta
        ),
        resumen=f"{len(con_falta)} unidades con algo pendiente",
    )


def _cuenta_por_agente(personas, companeros_por_caso):
    """Cuántas personas lleva cada agente, cuántas completas y cuántas sin mirar.

    Un caso puede estar asignado a varios compañeros, y entonces esa persona cuenta
    para los dos: el informe contesta «¿a quién le pregunto por esta persona?», y la
    respuesta son los dos. Por eso los números de esta tabla pueden sumar más que el
    total de personas, y la nota de la sección lo dice.
    """
    por_agente = {}
    for persona in personas:
        asignados = companeros_por_caso.get(persona["caso_id"], ()) or (SIN_AGENTE,)
        for agente in asignados:
            cuenta = por_agente.setdefault(
                agente, {"personas": 0, "completas": 0, "sin_mirar": 0}
            )
            cuenta["personas"] += 1
            estado = estado_de_los_pasos(persona)
            if estado is True:
                cuenta["completas"] += 1
            elif estado is None:
                # Ni completa ni incompleta: **nadie contestó ni un paso**. Es la
                # columna que de verdad informa a la dirección, porque no habla de
                # las unidades sino del reparto de trabajo del equipo.
                cuenta["sin_mirar"] += 1
    return por_agente


def seccion_del_equipo(personas, companeros_por_caso):
    """Cuánto lleva cada agente y cuánto de eso está mirado. Es del informe viejo.

    Sale SIEMPRE, incluso con una sola fila de «sin asignar», y ahí se separa de
    «Unidades» y de «Dónde se traban»: que nadie tenga nada asignado es justamente
    lo que la dirección tiene que ver, y una tabla que desaparece cuando el trabajo
    no está repartido esconde el peor de los casos.
    """
    cuentas = _cuenta_por_agente(personas, companeros_por_caso)
    filas = tuple(
        (agente, cuenta["personas"], cuenta["completas"], cuenta["sin_mirar"])
        for agente, cuenta in sorted(
            cuentas.items(), key=lambda par: (-par[1]["personas"], par[0])
        )
    )
    return Seccion(
        titulo="El equipo",
        notas=(
            "«Sin mirar» no es lo mismo que «sin completar»: son las personas de las "
            "que no consta NI UN paso contestado, o sea trabajo que no se ha "
            "empezado.",
            "Un caso asignado a dos compañeros cuenta para los dos, así que estas "
            "cifras pueden sumar más que el total de personas. No es un error de "
            "cuenta: la pregunta que contesta la tabla es a quién preguntarle por "
            "cada persona.",
        ),
        columnas=_COLUMNAS_DEL_EQUIPO,
        filas=filas,
        resumen=f"{len(filas)} agentes con trabajo en el período",
    )


def portada(recuento):
    """El titular y las cuatro cifras grandes. Sin porcentajes y sin dinero.

    Las cuatro son las del proyecto viejo y en su orden: las dos primeras parten a
    las que ya viajaron, la tercera dice cuántas quedan por delante —que es sobre
    las que todavía se puede hacer algo— y la cuarta da el tamaño del período.
    """
    return Portada(
        titular=TITULAR_DE_LA_PORTADA,
        frase=preparacion.titular(recuento),
        cifras=(
            Cifra(
                len(recuento.sin_completar),
                "viajaron sin la preparación completa",
                TONO_MALO if recuento.sin_completar else TONO_BUENO,
            ),
            Cifra(
                len(recuento.completas),
                "viajaron con la preparación completa",
                TONO_BUENO,
            ),
            Cifra(len(recuento.por_viajar), "por viajar todavía", TONO_NEUTRO),
            Cifra(len(recuento.casos), "casos en el período", TONO_NEUTRO),
        ),
    )
