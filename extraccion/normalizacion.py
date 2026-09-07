"""Convertir el texto leido en un valor con formato, o en nada.

Todo lo de aqui son expresiones regulares y tablas. Ni una sola de estas
funciones adivina: cuando el texto no encaja con el patron devuelven `None`, y
`None` significa «no se pudo leer», que es una respuesta correcta. La respuesta
incorrecta seria un MRN inventado.

Regla permanente 1 del proyecto, aplicada donde mas tienta romperla.
"""

import re

# `DECISIONES.md` §Reglas de formato de los campos.
PATRON_NUMERO_CASO = re.compile(r"\b([A-Z]{4}[0-9]{4})\b")
# ⚠️ El ultimo caracter puede ser una LETRA (`DECISIONES.md`, 2026-09-04). El
# patron viejo pedia once digitos en 3-4-4 y perdia 2 de las 7 cedulas de los
# escaneos reales del dueno: el OCR las leia bien y esta linea las tiraba. Se
# recorto del PDF la banda de la cedula y se miro la imagen: el papel dice `133A`.
#
# La letra NO se toca: ni se sube a mayuscula, ni se cambia por un digito
# «parecido», ni se recorta. Va tal como venia (regla permanente 1).
PATRON_MRN = re.compile(r"\b([0-9]{3})-([0-9]{4})-([0-9]{3}[0-9A-Za-z])\b")
PATRON_UNIDAD = re.compile(r"\b([0-9]{6,})\b")

# Los espacios de sobra que deja el recorte del OCR. Juntar espacios NO es corregir
# lo leido: «Santo  Domingo» y «Santo Domingo» son el mismo texto escrito con la
# basura del recorte, y la regla permanente 1 habla de inventar datos, no de esto.
_ESPACIOS_SEGUIDOS = re.compile(r"\s+")

# Los largos que `unidad_numero` admite, en un solo sitio. `datos/validacion.py`
# y el `CHECK` de `casos` dicen lo mismo (`DECISIONES.md`, 2026-09-02). El patron
# captura 6 o mas digitos a proposito: asi una cifra de 8 se ve entera, se
# reconoce que NO es un numero de unidad y se devuelve None, en vez de recortarle
# los dos ultimos digitos y dar por bueno un numero que no existe.
LARGOS_DE_UNIDAD = frozenset({6, 7})

# Los meses tal como aparecen en estos formularios. Se admite el nombre completo
# y la abreviatura de tres letras, con o sin punto, en los DOS idiomas en que
# llega el formulario: los cuatro documentos de referencia estan en ingles y los
# del dueno en espanol.
_MESES = {
    "enero": 1, "ene": 1,
    "febrero": 2, "feb": 2,
    "marzo": 3, "mar": 3,
    "abril": 4, "abr": 4,
    "mayo": 5, "may": 5,
    "junio": 6, "jun": 6,
    "julio": 7, "jul": 7,
    "agosto": 8, "ago": 8,
    "septiembre": 9, "setiembre": 9,
    "octubre": 10, "oct": 10,
    "noviembre": 11, "nov": 11,
    "diciembre": 12, "dic": 12,
    "january": 1, "jan": 1,
    "february": 2, "feb": 2,
    "march": 3, "mar": 3,
    "april": 4, "apr": 4,
    "may": 5,
    "june": 6, "jun": 6,
    "july": 7, "jul": 7,
    "august": 8, "aug": 8,
    "september": 9, "sept": 9, "sep": 9,  # coinciden con el espanol y valen 9 igual
    "october": 10, "oct": 10,
    "november": 11, "nov": 11,
    "december": 12, "dec": 12,
}

_NOMBRES_DE_MES = "|".join(sorted(_MESES, key=len, reverse=True))
_SUFIJO_ORDINAL = r"(?:st|nd|rd|th)?"

# «September 8, 2026», «Sept-7, 2026», «September 2nd, 2026».
_MES_DIA_ANIO = re.compile(
    rf"\b({_NOMBRES_DE_MES})\.?\s*[-\s]\s*(\d{{1,2}}){_SUFIJO_ORDINAL}\s*,?\s*(\d{{4}})\b",
    re.IGNORECASE,
)
# «8 Sept 2026», «12 September 2026», «14 de marzo de 2027», «14 marzo 2027».
# Los dos «de» del espanol son opcionales: el OCR se come uno a menudo.
_DIA_MES_ANIO = re.compile(
    rf"\b(\d{{1,2}}){_SUFIJO_ORDINAL}\s+(?:de\s+)?({_NOMBRES_DE_MES})\.?"
    rf"\s*(?:de\s+|,\s*)?(\d{{4}})\b",
    re.IGNORECASE,
)

# La fecha en numeros, que es como la escribe el formulario espanol. Medido el
# 2026-09-03: las siete fechas de los dos PDF del dueno son todas `dd-mm-aaaa`,
# y ninguna trae el mes en letras.
#
# El separador puede ser guion, barra o punto. Los tres se ven en escaneos.
_FECHA_ISO = re.compile(r"\b(\d{4})[-/.](\d{1,2})[-/.](\d{1,2})\b")
_FECHA_EN_NUMEROS = re.compile(r"\b(\d{1,2})[-/.](\d{1,2})[-/.](\d{4})\b")

# Un rango —«September 8-11, 2026», «5th till 12th»— NO es una fecha de viaje: no
# se sabe cual de los dos dias vale. Se detecta para poder rechazarlo a proposito
# en vez de quedarse con el primero por casualidad.
_RANGO = re.compile(
    rf"\b\d{{1,2}}{_SUFIJO_ORDINAL}\s*(?:-|–|to|till|until|through)\s*\d{{1,2}}{_SUFIJO_ORDINAL}\b",
    re.IGNORECASE,
)


def _fecha_iso(anio, mes, dia):
    """La fecha en ISO-8601, o None si el dia no existe en ese mes."""
    from datetime import date

    try:
        return date(int(anio), int(mes), int(dia)).isoformat()
    except ValueError:
        return None


def _dia_y_mes_sin_adivinar(primero, segundo):
    """Cual de los dos numeros es el dia y cual el mes, o `(None, None)`.

    Solo responde cuando los DIGITOS lo deciden: un numero mayor que 12 no puede
    ser un mes, asi que el otro lo es. Cuando los dos son 12 o menos la fecha es
    ambigua de verdad —«05-10-2027» es el 5 de octubre o el 10 de mayo— y esta
    funcion se niega a elegir.

    Suponer «dia-mes porque el dueno es de un pais que lo escribe asi» acertaria
    la mayoria de las veces. Las que fallara mueven una fecha de viaje varios
    meses, y es la fecha que decide si un caso sale avisado a tiempo. Regla
    permanente 1: el sistema no adivina.
    """
    if primero > 12 and segundo <= 12:
        return primero, segundo
    if segundo > 12 and primero <= 12:
        return segundo, primero
    return None, None


def _fecha_en_numeros(texto):
    """La fecha escrita en cifras, o None si no hay exactamente UNA sin ambiguedad.

    Se juntan todas las que aparezcan y solo vale si todas dicen lo mismo: dos
    fechas distintas en la misma banda son un rango, y un rango no dice cuando se
    viaja.
    """
    encontradas = set()
    for anio, mes, dia in _FECHA_ISO.findall(texto):
        encontradas.add(_fecha_iso(anio, mes, dia))
    for primero, segundo, anio in _FECHA_EN_NUMEROS.findall(texto):
        dia, mes = _dia_y_mes_sin_adivinar(int(primero), int(segundo))
        if dia is None:
            return None
        encontradas.add(_fecha_iso(anio, mes, dia))
    encontradas.discard(None)
    return encontradas.pop() if len(encontradas) == 1 else None


def normalizar_fecha(texto):
    """«8 Sept 2026» y «08-09-2026» -> «2026-09-08». None si no hay UNA fecha clara.

    Un rango de fechas devuelve None a proposito: en «September 8-11, 2026» no
    hay forma de saber si se viaja el 8 o el 11, y elegir uno seria inventar.

    Las cifras se miran ANTES que el rango, y no es un detalle de orden: el
    detector de rangos casa con «14-03-2027» —dos numeros de dos cifras con un
    guion en medio— y sin esto se comeria todas las fechas del formulario
    espanol dandolas por rangos.
    """
    if not texto:
        return None
    en_numeros = _fecha_en_numeros(texto)
    if en_numeros is not None:
        return en_numeros
    if _RANGO.search(texto):
        return None
    coincidencia = _MES_DIA_ANIO.search(texto)
    if coincidencia is not None:
        mes, dia, anio = coincidencia.groups()
        return _fecha_iso(anio, _MESES[mes.lower()], dia)
    coincidencia = _DIA_MES_ANIO.search(texto)
    if coincidencia is not None:
        dia, mes, anio = coincidencia.groups()
        return _fecha_iso(anio, _MESES[mes.lower()], dia)
    return None


def normalizar_numero_caso(texto):
    """4 letras mayusculas + 4 digitos, o None.

    No se pasa a mayusculas lo que venia en minusculas: si el OCR leyo `casp`
    puede haber leido mal tambien las cifras, y forzarlo a `CASP` esconderia el
    problema en vez de mandarlo a revision.
    """
    if not texto:
        return None
    coincidencia = PATRON_NUMERO_CASO.search(texto)
    return coincidencia.group(1) if coincidencia is not None else None


def normalizar_mrn(texto):
    """3 digitos, guion, 4 digitos, guion, 3 digitos y un caracter mas. O None.

    Ese ultimo caracter puede ser un digito o una LETRA: `055-1111-3853` y
    `066-2222-133A` son las dos FORMAS, y las dos salen de escaneos reales del
    dueno. Palabras suyas el 2026-09-04: «muchas cedulas de miembro tienen una A u
    otra letra al final».

    ⚠️ Los dos valores de arriba van SUSTITUIDOS desde el 2026-09-07: lo que salio
    del papel fue la forma, y la forma esta entera; el ejemplar concreto ya no vive
    en el repositorio. Ver `EN-CURSO.md`, «Los datos de personas reales salen del
    repositorio».

    ⚠️ **Hasta esa fecha esta funcion devolvia None para la segunda forma**, y esa
    era la razon de que 2 de las 7 cedulas del dueno acabaran con el campo vacio:
    el OCR las leia bien y aqui se tiraban.

    Se exigen los guiones tal como estan y la letra se devuelve tal como venia: no
    se sube a mayuscula, no se cambia por un digito «parecido» y no se recorta.
    Cualquiera de las tres cosas es inventar un dato (regla permanente 1), y un MRN
    inventado manda a una persona al templo con la recomendacion equivocada.
    """
    if not texto:
        return None
    coincidencia = PATRON_MRN.search(texto)
    if coincidencia is None:
        return None
    return "-".join(coincidencia.groups())


def normalizar_unidad(texto):
    """Devuelve `(numero_de_unidad, resto_del_texto)`; el numero puede ser None.

    `unidad_numero` son **6 o 7 digitos** segun `DECISIONES.md` (2026-09-02,
    «manda el papel»), segun `validar_unidad_numero` y segun el `CHECK` de la
    tabla `casos` desde la version 2 del esquema. Una cifra de otro largo NO se
    recorta ni se rellena: vuelve como None y el texto crudo viaja aparte para
    que se vea que habia algo.

    El largo admitido se lee de `LARGOS_DE_UNIDAD` para que este archivo y
    `datos/validacion.py` no puedan volver a discrepar en silencio, que es lo
    que paso hasta hoy: el validador aceptaba siete digitos y aqui se tiraban.
    """
    if not texto:
        return None, None
    coincidencia = PATRON_UNIDAD.search(texto)
    if coincidencia is None:
        return None, texto.strip() or None
    digitos = coincidencia.group(1)
    nombre = (texto[: coincidencia.start()] + texto[coincidencia.end() :]).strip(" -–\t")
    numero = digitos if len(digitos) in LARGOS_DE_UNIDAD else None
    return numero, (nombre or None)


def normalizar_numero_de_unidad(texto):
    """Solo el numero de unidad de un texto, o None. Atajo de `normalizar_unidad`.

    Existe aparte porque el numero y el nombre de la unidad se corrigen por
    separado en los formularios reales: hay anotaciones que reescriben el nombre
    y dejan el numero como estaba.
    """
    numero, _ = normalizar_unidad(texto)
    return numero


def normalizar_nombre_de_unidad(texto):
    """Solo el nombre de la unidad, sin su numero, o None."""
    _, nombre = normalizar_unidad(texto)
    return nombre


def normalizar_nombre_del_templo(texto):
    """El nombre del templo tal como esta escrito, con los espacios juntados.

    **No hay catalogo y no se corrige nada** (regla permanente 1, y
    `DECISIONES.md` 2026-09-03: «se lee del papel a `casos.templo_nombre`, texto,
    sin catalogo»). No se compara contra una lista de templos ni se elige «el mas
    parecido»: el catalogo con sus colores es una decision del dueno que todavia no
    ha tomado, y hasta entonces un templo mal leido tiene que verse mal leido y no
    convertido en otro que si esta en la lista.

    Lo unico que se hace es lo mismo que con cualquier otro campo de texto: recortar
    los espacios de los extremos y juntar los de dentro, porque el OCR devuelve «
    Santo  Domingo » y eso es el mismo templo escrito con la basura del recorte. Un
    texto que se queda vacio vuelve None, que significa «no se leyo».
    """
    if not texto:
        return None
    return _ESPACIOS_SEGUIDOS.sub(" ", str(texto)).strip() or None
