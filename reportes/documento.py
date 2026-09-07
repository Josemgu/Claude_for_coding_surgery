"""El reporte armado una sola vez: secciones, columnas, filas y avisos.

Excel y PDF salen los dos de aqui y ninguno de los dos vuelve a contar nada. Dos
codigos que cuentan lo mismo por su cuenta acaban dando dos numeros distintos, y
un reporte con dos totales no sirve para lo unico que sirve un reporte.

**Los avisos viven en `reportes/avisos.py`**, y no aqui, porque son otra
responsabilidad: este modulo cuenta, y aquel dice de que no se fia el numero que
se acaba de contar. Ninguno esta escrito a mano: cada uno nace de una medicion y
desaparece solo cuando su causa desaparece.

**Que hay aqui y que no, desde que el informe abrio por la portada del proyecto
viejo (2026-09-03).** Aqui estan las tres secciones de la FASE 8 —quien viajo, quien
no pudo, y las metricas del equipo— y el ensamblaje del documento entero. Lo demas
se reparte en tres modulos vecinos, y cada uno responde a una pregunta distinta:

    formato.py                 con que se describe un reporte: columnas y cifras
    preparacion.py             cuantos viajaron sin la preparacion completa
    secciones_de_direccion.py  las seis secciones que lee la direccion

⚠️ **«Verificado» significa aqui la firma de Miguel, y solo aqui.** Las tres
metricas del final cuentan campos de `procedencia_campo` con `verificado = 1`, o
sea lo que Miguel dio por bueno mirando el papel, y por eso siguen diciendo
«verificados». Las secciones de la direccion hablan de otra cosa —los seis pasos
que contesta un companero— y desde el 2026-09-03 la llaman por su nombre: «con la
preparacion completa». Las dos palabras conviven en el mismo documento a proposito
porque son dos hechos distintos; lo que no puede volver es que se llamen igual.

**Sin dinero, en ninguna seccion.** Citado del viejo: el presupuesto lo lleva otro
departamento. `pruebas/prueba_informe_para_los_jefes.py` lo comprueba recorriendo
cada cadena del documento en vez de fiarse de este parrafo.
"""

from datos.estados import ESTADOS_QUE_RESUELVEN, recomendacion_sin_resolver
from reportes import consultas, metricas, preparacion, secciones_de_direccion
from reportes.avisos import avisos_del_reporte
from reportes.formato import (
    CRUDO,
    NO_SE_PUEDE_SABER,
    SIN_DATO,
    TEMPORAL,
    TEXTO,
    TITULAR_DE_LA_PORTADA,
    TITULO,
    TONO_BUENO,
    TONO_MALO,
    TONO_NEUTRO,
    Cifra,
    Columna,
    Documento,
    Portada,
    Seccion,
    persona_o_sin_nombre,
    unidad_con_su_numero,
)
from reportes.periodo import texto_del_periodo

# El vocabulario —columnas, secciones, cifras y las palabras de lo que no consta—
# vive en `reportes/formato.py` desde que las secciones para la direccion se
# mudaron a su propio modulo: los dos que arman secciones lo comparten sin
# importarse entre ellos. Se reexporta aqui con los mismos nombres para que nada de
# lo que ya lo importaba de `reportes.documento` tenga que cambiar.

_COLUMNAS_DE_QUIEN_VIAJO = (
    Columna("N.º de caso", TEXTO, 12),
    Columna("Persona", CRUDO, 30),
    Columna("MRN", TEXTO, 14),
    Columna("Unidad", CRUDO, 22),
    Columna("Fecha de viaje", TEMPORAL, 14),
    Columna("¿Viajó?", CRUDO, 12),
    Columna("Estado de la recomendación", CRUDO, 22),
    Columna("¿Recomendación completa?", CRUDO, 22),
)

_COLUMNAS_DE_QUIEN_NO_VIAJO = (
    Columna("N.º de caso", TEXTO, 12),
    Columna("Persona", CRUDO, 30),
    Columna("MRN", TEXTO, 14),
    Columna("Unidad", CRUDO, 22),
    Columna("Fecha de viaje", TEMPORAL, 14),
    Columna("Motivo por el que no pudo viajar", CRUDO, 46),
    Columna("¿Se detectó antes del viaje?", CRUDO, 24),
)

_COLUMNAS_DE_LAS_METRICAS = (
    Columna("Métrica", CRUDO, 40),
    Columna("Número", CRUDO, 16),
    Columna("Cómo se cuenta", CRUDO, 60),
)


def _texto_del_estado(estado_recomendacion):
    """El estado tal como esta guardado, o la palabra que dice que no hay ninguno."""
    if estado_recomendacion is None:
        return SIN_DATO
    return estado_recomendacion.replace("_", " ")


def _texto_de_recomendacion_completa(estado_recomendacion):
    """«sí», «no» o «no se puede saber». Nunca se rellena a ojo.

    Mientras `ESTADOS_QUE_RESUELVEN` este vacia no hay ningun valor que signifique
    «resuelta», asi que ni un «sí» ni un «no» serian ciertos: los dos afirmarian
    algo que nadie ha decidido. La tercera respuesta es la unica honesta.
    """
    if not ESTADOS_QUE_RESUELVEN:
        return NO_SE_PUEDE_SABER
    if estado_recomendacion is None:
        return SIN_DATO
    return "no" if recomendacion_sin_resolver(estado_recomendacion) else "sí"


def _texto_de_si_viajo(pudo_viajar):
    """«sí», «no» o «no consta». `None` es una respuesta, no un hueco."""
    if pudo_viajar is None:
        return SIN_DATO
    return "sí" if pudo_viajar == 1 else "no"


def _fila_de_quien_viajo(persona):
    """Una fila de la primera parte del reporte, ya escrita en español."""
    return (
        persona["numero_caso"],
        persona_o_sin_nombre(persona),
        persona["mrn"] or SIN_DATO,
        unidad_con_su_numero(persona),
        persona["fecha_viaje"],
        _texto_de_si_viajo(persona["pudo_viajar"]),
        _texto_del_estado(persona["estado_recomendacion"]),
        _texto_de_recomendacion_completa(persona["estado_recomendacion"]),
    )


def _texto_de_la_deteccion(caso, fecha_viaje):
    """Si el problema de ese caso estaba visto antes del dia del viaje."""
    if caso is None or caso["verificado_en"] is None:
        return "no se verificó ningún campo"
    dia = caso["verificado_en"][:10]
    return f"sí, el {dia}" if dia < fecha_viaje else f"no, se vio el {dia}"


def _fila_de_quien_no_viajo(persona, casos_por_id):
    """Una fila de la segunda parte, con el motivo tal como lo escribio Miguel."""
    return (
        persona["numero_caso"],
        persona_o_sin_nombre(persona),
        persona["mrn"] or SIN_DATO,
        unidad_con_su_numero(persona),
        persona["fecha_viaje"],
        persona["motivo_no_viajo"],
        _texto_de_la_deteccion(casos_por_id.get(persona["caso_id"]), persona["fecha_viaje"]),
    )


def _seccion_de_quien_viajo(personas):
    """Parte 1: las personas cuyo caso viajaba en el periodo, y como quedo."""
    constan_como_que_viajaron = sum(1 for p in personas if p["pudo_viajar"] == 1)
    completas = sum(
        1
        for p in personas
        if _texto_de_recomendacion_completa(p["estado_recomendacion"]) == "sí"
    )
    return Seccion(
        titulo="Parte 1 — Personas que viajaron, y en qué estado quedó su recomendación",
        notas=(
            "Entran todas las personas cuyo caso tenía fecha de viaje dentro del "
            "período, estén archivadas o no. Archivar un caso no cambia este total.",
        ),
        columnas=_COLUMNAS_DE_QUIEN_VIAJO,
        filas=tuple(_fila_de_quien_viajo(persona) for persona in personas),
        resumen=(
            f"{len(personas)} personas · {constan_como_que_viajaron} constan como "
            f"que sí viajaron · {completas} con la recomendación completa"
        ),
    )


def _seccion_de_quien_no_viajo(personas, casos_por_id):
    """Parte 2: quien no pudo viajar, con su motivo y su numero de caso."""
    return Seccion(
        titulo="Parte 2 — Personas que NO pudieron viajar",
        notas=(
            "Solo aparece quien está anotado a mano como que no pudo viajar. El "
            "programa no lo deduce de ningún dato: alguien tuvo que escribirlo, y "
            "el motivo es lo que esa persona escribió, sin retocar.",
        ),
        columnas=_COLUMNAS_DE_QUIEN_NO_VIAJO,
        filas=tuple(_fila_de_quien_no_viajo(p, casos_por_id) for p in personas),
        resumen=f"{len(personas)} personas no pudieron viajar en este período",
    )


def _numero_de_horas(horas):
    """Las horas con un decimal y su equivalente en dias, en español."""
    if horas is None:
        return NO_SE_PUEDE_SABER
    return f"{horas:.1f} h ({horas / 24:.1f} días)".replace(".", ",")


def _filas_de_las_metricas(verificados, demora, deteccion):
    """Las tres metricas, cada una con su numero y con como se cuenta."""
    return (
        (
            "1. Casos verificados en el período",
            len(verificados),
            "Un caso cuenta cuando tiene campos leídos y TODOS quedaron "
            "verificados. Se sitúa en el período por la fecha del último campo "
            "verificado.",
        ),
        (
            "2. Tiempo promedio de importar a verificar",
            _numero_de_horas(demora.promedio_en_horas),
            f"Promedio sobre {demora.casos_medidos} casos de los de arriba. "
            f"Mínimo {_numero_de_horas(demora.minimo_en_horas)}, máximo "
            f"{_numero_de_horas(demora.maximo_en_horas)}. "
            f"Descartados por fechas incoherentes: "
            f"{demora.descartados_por_fechas_incoherentes}.",
        ),
        (
            "3. Casos con el problema detectado ANTES de la fecha de viaje",
            deteccion.detectados_a_tiempo,
            f"De los {deteccion.casos_del_periodo} casos que viajaban en el "
            f"período, {deteccion.con_problema_registrado} tienen un problema "
            f"escrito. De esos: {deteccion.detectados_a_tiempo} vistos antes del "
            f"día del viaje, {deteccion.detectados_despues_del_viaje} el mismo día "
            f"o después, {deteccion.con_problema_sin_fecha_de_deteccion} sin ningún "
            f"campo verificado con el que fechar cuándo se vio.",
        ),
    )


def _seccion_de_las_metricas(verificados, demora, deteccion):
    """Las metricas de trabajo del equipo, con sus denominadores a la vista."""
    return Seccion(
        titulo="Métricas del trabajo del equipo",
        notas=(
            "Cada métrica usa una columna de fecha distinta y por eso los tres "
            "números no son comparables entre sí: la columna «Cómo se cuenta» dice "
            "cuál usa cada una.",
            f"Casos del período por fecha de viaje: {deteccion.casos_del_periodo}. "
            f"Sin estado de recomendación escrito: {deteccion.sin_estado_registrado}. "
            f"Con la recomendación resuelta: {deteccion.con_recomendacion_resuelta}.",
        ),
        columnas=_COLUMNAS_DE_LAS_METRICAS,
        filas=_filas_de_las_metricas(verificados, demora, deteccion),
        resumen=None,
    )


def _dia_de(generado_en):
    """El día de la marca de tiempo con la que se genera el informe.

    Se saca de `generado_en` y no del reloj, por lo mismo que en
    `datos/calendario.py`: una función que mira el reloj por dentro no se puede
    probar, y de qué día se considera «ya viajó» depende la cifra de la portada.
    """
    return str(generado_en)[:10]


def construir_documento(conexion, periodo, generado_en):
    """Arma el reporte entero de ese periodo. Devuelve el documento, sin escribirlo.

    `generado_en` entra desde fuera y no se lee del reloj aqui, por lo mismo que en
    `datos/calendario.py`: una funcion que mira el reloj por dentro no se puede
    probar.
    """
    personas = consultas.personas_del_periodo(conexion, periodo)
    casos = consultas.casos_con_su_verificacion(conexion)
    casos_por_id = {caso["id"]: caso for caso in casos}
    sin_fecha = consultas.personas_que_no_viajaron_sin_fecha(conexion)
    companeros = consultas.companeros_por_caso(conexion)

    viajaron = [persona for persona in personas if persona["pudo_viajar"] != 0]
    no_viajaron = [persona for persona in personas if persona["pudo_viajar"] == 0]
    deteccion = metricas.deteccion_antes_del_viaje(casos, periodo)

    dia_de_hoy = _dia_de(generado_en)
    recuento = preparacion.recontar(personas, dia_de_hoy)

    # Las tres primeras son las del informe del proyecto viejo, en su orden, y van
    # delante: quien lo lee tiene que tropezarse con el numero que duele antes que
    # con nada mas. Las tres de la FASE 8 se quedan detras, que es donde las busca
    # quien ya sabe que quiere mirar.
    secciones = [
        secciones_de_direccion.seccion_de_quien_viajo_sin_la_preparacion(
            recuento.sin_completar
        ),
        secciones_de_direccion.seccion_de_los_viajes(
            preparacion.resumen_por_caso(personas, dia_de_hoy, companeros)
        ),
        secciones_de_direccion.seccion_de_a_que_van(personas),
    ]
    # Las tres que pueden no salir van detrás de las tres fijas y en el orden del
    # informe viejo. «Dónde se traban» y «Unidades» devuelven None cuando no hay
    # nada pendiente: una tabla de ceros ocupa el sitio de lo que sí dice algo. «El
    # equipo» sale siempre, y eso es a propósito: que nadie tenga nada asignado es
    # justamente lo que la dirección tiene que ver.
    for seccion in (
        secciones_de_direccion.seccion_de_donde_se_traban(personas),
        secciones_de_direccion.seccion_de_las_unidades(personas),
        secciones_de_direccion.seccion_del_equipo(personas, companeros),
    ):
        if seccion is not None:
            secciones.append(seccion)
    secciones.extend(
        [
            _seccion_de_quien_viajo(viajaron),
            _seccion_de_quien_no_viajo(no_viajaron, casos_por_id),
            _seccion_de_las_metricas(
                metricas.casos_verificados_en_el_periodo(casos, periodo),
                metricas.demora_de_importar_a_verificar(casos, periodo),
                deteccion,
            ),
        ]
    )

    return Documento(
        titulo=TITULO,
        subtitulo=f"Período: {texto_del_periodo(periodo)}",
        generado_en=generado_en,
        portada=secciones_de_direccion.portada(recuento),
        avisos=avisos_del_reporte(deteccion, sin_fecha),
        secciones=tuple(secciones),
    )
