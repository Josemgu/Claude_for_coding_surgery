"""El trozo de calendario que se reporta, validado en un solo sitio.

Un periodo son dos fechas ISO-8601 y **los dos extremos entran**. Se dice aqui
porque es la clase de detalle que, sin decirlo, hace que un reporte de septiembre
se deje fuera el dia 30 y nadie lo note hasta que los numeros no cuadran.

**El limite de arriba para las marcas de tiempo es otro.** `fecha_viaje` es
'AAAA-MM-DD' pelado, pero `creado_en` y `verificado_en` llevan la hora pegada:
'2026-09-30 14:12:03' es mayor que '2026-09-30' comparado como texto, asi que un
`<= hasta` se dejaria fuera todo lo que paso ese dia despues de medianoche. Por
eso el periodo publica ademas `siguiente_a_hasta`, y las comparaciones contra
columnas con hora son `>= desde AND < siguiente_a_hasta`.

Ninguna funcion de este modulo mira el reloj. La fecha de hoy entra desde fuera,
igual que en `datos/calendario.py` y por el mismo motivo: lo que mira el reloj por
dentro no se puede probar por los bordes.
"""

from collections import namedtuple
from datetime import date, timedelta

from datos.validacion import ErrorDeValidacion, validar_fecha_viaje

Periodo = namedtuple("Periodo", ("desde", "hasta", "siguiente_a_hasta"))

MESES = (
    "enero", "febrero", "marzo", "abril", "mayo", "junio",
    "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
)


class ErrorDePeriodo(ValueError):
    """Al periodo del reporte se le pasaron unas fechas que no forman un periodo."""


def _validar_extremo(valor, nombre_del_parametro):
    """Convierte un extremo a 'AAAA-MM-DD', venga como `date` o como texto."""
    if isinstance(valor, date):
        return valor.isoformat()
    try:
        return validar_fecha_viaje(valor)
    except ErrorDeValidacion as causa:
        raise ErrorDePeriodo(
            f"El extremo {nombre_del_parametro!r} del período no vale: {causa}"
        ) from causa


def periodo(desde, hasta):
    """El periodo entre dos fechas, los dos extremos incluidos.

    Un periodo de un solo dia es legitimo —`desde` igual a `hasta`— y sale con
    `siguiente_a_hasta` en el dia de despues. Lo que no se admite es un periodo
    del reves: un reporte «del 30 al 1» no contaria nada y saldria con todos los
    numeros a cero, que se lee igual que un mes sin trabajo.
    """
    primero = _validar_extremo(desde, "desde")
    ultimo = _validar_extremo(hasta, "hasta")
    if primero > ultimo:
        raise ErrorDePeriodo(
            f"El período va del {primero} al {ultimo}, que está del revés. "
            "Se esperaba que 'desde' fuera anterior o igual a 'hasta'."
        )
    siguiente = (date.fromisoformat(ultimo) + timedelta(days=1)).isoformat()
    return Periodo(primero, ultimo, siguiente)


def _primer_dia_del_mes_siguiente(anio, mes):
    """El limite abierto por arriba del mes, sin sumar dias ni contar bisiestos."""
    return date(anio + 1, 1, 1) if mes == 12 else date(anio, mes + 1, 1)


def periodo_del_mes(anio, mes):
    """El mes entero, del dia 1 al ultimo, sea de 28, 29, 30 o 31 dias."""
    try:
        primero = date(anio, mes, 1)
    except (TypeError, ValueError) as causa:
        raise ErrorDePeriodo(
            f"No hay ningún mes {mes!r} del año {anio!r} ({causa}). Se esperaban dos "
            "enteros, como periodo_del_mes(2026, 9)."
        ) from causa
    ultimo = _primer_dia_del_mes_siguiente(anio, mes) - timedelta(days=1)
    return periodo(primero, ultimo)


def periodo_de_los_ultimos_dias(hoy, dias):
    """Los ultimos `dias` dias contando hoy como el ultimo.

    `dias = 30` da un periodo de 30 dias, no de 31: hoy es uno de los treinta. Es
    la cuenta que espera quien pide «el último mes» y la que hay que escribir para
    que no salga un dia de mas.
    """
    if isinstance(dias, bool) or not isinstance(dias, int) or dias < 1:
        raise ErrorDePeriodo(
            f"El parámetro 'dias' no vale: se recibió {dias!r} y se esperaba un "
            "número entero de días mayor que cero."
        )
    ultimo = date.fromisoformat(_validar_extremo(hoy, "hoy"))
    return periodo(ultimo - timedelta(days=dias - 1), ultimo)


def _fecha_larga(texto_iso):
    """«30 de septiembre de 2026», en espanol y sin depender del idioma del sistema."""
    dia = date.fromisoformat(texto_iso)
    return f"{dia.day} de {MESES[dia.month - 1]} de {dia.year}"


def texto_del_periodo(el_periodo):
    """Como se escribe el periodo en la cabecera del reporte, en espanol."""
    if el_periodo.desde == el_periodo.hasta:
        return f"el {_fecha_larga(el_periodo.desde)}"
    return f"del {_fecha_larga(el_periodo.desde)} al {_fecha_larga(el_periodo.hasta)}"


def nombre_corto_del_periodo(el_periodo):
    """El trozo que va en el nombre del archivo: '2026-09-01_a_2026-09-30'."""
    return f"{el_periodo.desde}_a_{el_periodo.hasta}"
