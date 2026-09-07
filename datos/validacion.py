"""Las cuatro reglas de formato de `DECISIONES.md`, y el aviso del mes cruzado.

Todo lo que entra se valida ANTES de tocar el motor. Lo que se rechaza, se
rechaza con un mensaje en espanol que nombra el campo y dice que se esperaba.

La separacion que hace este modulo, y que no es cosmetica:

  - `validar_*` levanta `ErrorDeValidacion`: el dato no se guarda.
  - `avisos_*` devuelve texto: el dato SI se guarda y alguien tiene que mirarlo.

La regla del mes cruzado esta en el segundo grupo por decision del dueno
(`DECISIONES.md` 2026-09-02, P-2): como pared haria imposible guardar un viaje
reprogramado a otro mes. Es la regla permanente 5 —el sistema propone, Miguel
confirma— aplicada a una fecha.
"""

import re
from datetime import date

from datos.estados import ESTADOS_RECOMENDACION

_PATRON_NUMERO_CASO = re.compile(r"^[A-Z]{4}[0-9]{4}$")
# El ultimo caracter puede ser un digito o una LETRA (`DECISIONES.md`, 2026-09-04,
# «La cedula PUEDE terminar en letra»). Medido sobre los siete PDF reales del
# dueno: la regla vieja de «11 digitos en patron 3-4-4» dejaba 2 de 7 cedulas sin
# guardar. El mismo error que se cometio con `unidad_numero` y por el mismo
# motivo: la regla se escribio mirando cuatro papeles y no los del dueno.
_PATRON_MRN = re.compile(r"^[0-9]{3}-[0-9]{4}-[0-9]{3}[0-9A-Za-z]$")

# 6 o 7 digitos, no 6 (`DECISIONES.md`, 2026-09-02, «`unidad_numero` admite 6 o 7
# digitos: manda el papel»). Medido en la FASE 2 sobre los PDF reales: 4 de las 9
# paginas traen `7000011`, de siete digitos. La regla de seis rechazaba un dato
# verdadero y dejaba el campo vacio en esas cuatro paginas.
_PATRON_UNIDAD = re.compile(r"^[0-9]{6,7}$")
_PATRON_FECHA = re.compile(r"^([0-9]{4})-([0-9]{2})-([0-9]{2})$")

# Las dos formas que trae el papel, escritas una vez y usadas en el mensaje de
# error. Van las DOS: un ejemplo solo de la forma que ya se aceptaba deja a quien
# lee el error sin saber que la otra tambien vale.
#
# ⚠️ El mensaje que las usa tiene que caber en DOS lineas del panel de correccion
# (532 px de columna, `interfaz/campo.py`). No es cosmetica: hay una prueba que lo
# mide —`prueba_guardar_que_se_ve.test_el_campo_se_queda_en_rojo_con_su_motivo_en_una_linea`—
# y una tercera linea empuja el resto de la pantalla. Por eso el formato se dice
# con los dos ejemplos y una frase corta, y no describiendo grupo por grupo.
EJEMPLO_DE_MRN_CON_DIGITOS = "055-1111-3853"
EJEMPLO_DE_MRN_CON_LETRA = "066-2222-133A"


class ErrorDeValidacion(ValueError):
    """Un campo no cumple su regla de formato y no se va a guardar."""


def validar_numero_caso(numero_caso):
    """4 letras mayusculas + 4 digitos. Obligatorio: un caso sin numero no existe."""
    if not isinstance(numero_caso, str) or not _PATRON_NUMERO_CASO.match(numero_caso):
        raise ErrorDeValidacion(
            f"El campo 'numero_caso' no vale: se recibió {numero_caso!r} y se "
            "esperaban 4 letras mayúsculas seguidas de 4 dígitos, como 'CASP2609'."
        )
    return numero_caso


def validar_numero_caso_si_lo_hay(numero_caso):
    """Lo mismo, pero admitiendo que todavia no se sepa: None vuelve None.

    Existe porque desde la version 7 del esquema una pagina cuyo numero de caso no
    se pudo leer se guarda igual, pendiente de identificar, en vez de tirarse
    entera con todo lo que si se habia leido.

    Va como funcion aparte y NO aflojando `validar_numero_caso`: esa sigue siendo
    la regla estricta, y quien la llame sigue teniendo la garantia de que un None
    se rechaza. Aflojarla convertiria en silencioso cualquier sitio que hoy depende
    de que un numero vacio no pase, y el motivo por el que este numero importa
    tanto —es la mitad del par que reconcilia el Excel de los companeros— no ha
    cambiado.

    Lo que NO hace, y es la mitad del arreglo: no inventa un numero (regla
    permanente 1). None significa «todavia no se sabe», y se teclea a mano.
    """
    if numero_caso is None:
        return None
    return validar_numero_caso(numero_caso)


def validar_mrn(mrn):
    """3 digitos, 4 digitos y 4 caracteres, con guiones. El ultimo puede ser letra.

    Acepta nulo: el OCR puede no haberlo leido.

    La letra se guarda tal como llega. No se sube a mayuscula: si el papel la trae
    en minuscula, en minuscula queda. Corregir lo leido es lo que la regla
    permanente 1 prohibe, y da igual que la correccion parezca inofensiva.
    """
    if mrn is None:
        return None
    if not isinstance(mrn, str) or not _PATRON_MRN.match(mrn):
        raise ErrorDeValidacion(
            f"El campo 'mrn' no vale: se recibió {mrn!r} y se espera "
            f"'{EJEMPLO_DE_MRN_CON_DIGITOS}' o '{EJEMPLO_DE_MRN_CON_LETRA}': "
            "el último puede ser letra."
        )
    return mrn


def validar_unidad_numero(unidad_numero):
    """6 o 7 digitos. Acepta nulo por la misma razon que el MRN."""
    if unidad_numero is None:
        return None
    if not isinstance(unidad_numero, str) or not _PATRON_UNIDAD.match(unidad_numero):
        raise ErrorDeValidacion(
            f"El campo 'unidad_numero' no vale: se recibió {unidad_numero!r} y se "
            "esperaban 6 o 7 dígitos, como '123456' o '7000011'."
        )
    return unidad_numero


def validar_fecha_viaje(fecha_viaje):
    """Fecha real en ISO-8601 `AAAA-MM-DD`. Acepta nulo.

    La forma no basta: '2026-02-31' pasa cualquier `GLOB` y no existe. Por eso se
    construye la fecha de verdad antes de dar el valor por bueno.
    """
    if fecha_viaje is None:
        return None
    coincidencia = _PATRON_FECHA.match(fecha_viaje) if isinstance(fecha_viaje, str) else None
    if coincidencia is None:
        raise ErrorDeValidacion(
            f"El campo 'fecha_viaje' no vale: se recibió {fecha_viaje!r} y se "
            "esperaba una fecha en formato AAAA-MM-DD, como '2026-09-08'."
        )
    anio, mes, dia = (int(parte) for parte in coincidencia.groups())
    try:
        date(anio, mes, dia)
    except ValueError as causa:
        raise ErrorDeValidacion(
            f"El campo 'fecha_viaje' no vale: {fecha_viaje!r} tiene la forma "
            f"correcta pero no es una fecha que exista ({causa})."
        ) from causa
    return fecha_viaje


def validar_estado_recomendacion(estado_recomendacion):
    """Uno de los valores de `datos.estados`, o nulo mientras nadie lo haya dicho."""
    if estado_recomendacion is None:
        return None
    if estado_recomendacion not in ESTADOS_RECOMENDACION:
        validos = ", ".join(repr(valor) for valor in ESTADOS_RECOMENDACION)
        raise ErrorDeValidacion(
            f"El campo 'estado_recomendacion' no vale: se recibió "
            f"{estado_recomendacion!r} y los valores admitidos hoy son {validos}."
        )
    return estado_recomendacion


def _mes_esperado_del_caso(numero_caso):
    """Los ultimos 4 digitos del numero de caso, leidos como AAMM."""
    return numero_caso[4:8]


def _mes_de_la_fecha(fecha_viaje):
    """La fecha de viaje comprimida a AAMM, para poder compararla."""
    return fecha_viaje[2:4] + fecha_viaje[5:7]


def avisos_de_caso(numero_caso, fecha_viaje):
    """Lo que hay que mirar de un caso, sin impedir que se guarde.

    Hoy solo produce el aviso del mes cruzado. Devuelve una tupla vacia cuando no
    hay nada que decir, para que quien lo llame no tenga que distinguir None.
    """
    if fecha_viaje is None or not _PATRON_NUMERO_CASO.match(numero_caso or ""):
        return ()
    if not _PATRON_FECHA.match(fecha_viaje):
        return ()

    esperado = _mes_esperado_del_caso(numero_caso)
    encontrado = _mes_de_la_fecha(fecha_viaje)
    if esperado == encontrado:
        return ()
    return (
        f"AVISO: la 'fecha_viaje' {fecha_viaje} cae en el periodo {encontrado}, "
        f"pero el 'numero_caso' {numero_caso} dice {esperado}. Puede ser un viaje "
        "reprogramado: el dato se guarda igual y hay que confirmarlo a mano.",
    )


# Tope de longitud del nombre de la unidad. No es una regla del papel: es el
# limite que impide que una linea de OCR desbocada —o un pegado accidental de
# media pagina— entre entera en la base y luego desborde la celda del Excel y la
# fila de la pantalla. El nombre mas largo de los formularios de referencia mide
# 17 caracteres («Paramaribo Branch»); 120 deja sitio de sobra sin dejar la puerta
# abierta.
LARGO_MAXIMO_DEL_NOMBRE_DE_UNIDAD = 120


def validar_unidad_nombre(unidad_nombre):
    """El nombre de la unidad tal como viene del papel, sin espacios de sobra.

    Acepta nulo: el OCR puede no haberlo leido, y un nombre no es obligatorio para
    que un caso exista. Lo que queda vacio despues de quitar los espacios se
    guarda como nulo y NO como cadena vacia: dos formas de decir «no hay nombre»
    son dos formas de que una consulta se olvide de una.

    No se corrige la ortografia ni se cambian mayusculas. Es un dato leido del
    papel, y arreglarlo aqui seria inventar (regla permanente 1).
    """
    if unidad_nombre is None:
        return None
    if not isinstance(unidad_nombre, str):
        raise ErrorDeValidacion(
            f"El campo 'unidad_nombre' no vale: se recibió {unidad_nombre!r} y se "
            "esperaba texto, como 'Paramaribo Branch'."
        )
    limpio = unidad_nombre.strip()
    if not limpio:
        return None
    if len(limpio) > LARGO_MAXIMO_DEL_NOMBRE_DE_UNIDAD:
        raise ErrorDeValidacion(
            f"El campo 'unidad_nombre' no vale: se recibieron {len(limpio)} "
            f"caracteres y el máximo son {LARGO_MAXIMO_DEL_NOMBRE_DE_UNIDAD}. "
            "Un nombre de unidad más largo que eso casi siempre es una línea del "
            "OCR que se coló entera."
        )
    return limpio


def validar_pagina_pdf(pagina_pdf):
    """La pagina del PDF donde vive este caso, contada desde 1. Acepta nulo.

    Desde 1 y no desde 0 porque es lo que se le ensena a una persona —«pagina 2 de
    6»— y lo que entiende el visor de PDF del sistema. El indice desde 0 de la
    extraccion se convierte una sola vez, al guardar.
    """
    if pagina_pdf is None:
        return None
    if isinstance(pagina_pdf, bool) or not isinstance(pagina_pdf, int):
        raise ErrorDeValidacion(
            f"El campo 'pagina_pdf' no vale: se recibió {pagina_pdf!r} y se "
            "esperaba un número entero de página, contando desde 1."
        )
    if pagina_pdf < 1:
        raise ErrorDeValidacion(
            f"El campo 'pagina_pdf' no vale: se recibió {pagina_pdf!r} y las "
            "páginas se cuentan desde 1, no desde 0."
        )
    return pagina_pdf
