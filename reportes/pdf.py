"""El reporte colocado sobre el papel: donde va cada linea y donde corta la pagina.

Este modulo decide la **maqueta** —anchos de columna, partido de lineas, saltos de
pagina, titulos de columna repetidos— y no sabe nada del formato de archivo. Los
bytes los pone `reportes/formato_pdf.py`. La separacion se explica alli.

Papel apaisado a proposito: las tablas del reporte tienen hasta ocho columnas y en
vertical no caben sin recortar el motivo, que es justo lo que hay que leer.

⚠️ **Lo que este formato NO puede escribir, y se dice en el propio PDF.**
`/WinAnsiEncoding` cubre el alfabeto latino con tildes y eñes —que es lo que traen
los formularios— pero no cubre todo Unicode. Un carácter que no quepa sale como
`?` y el PDF avisa arriba de cuántos fueron. No se calla: un nombre alterado en
silencio es un dato falso sobre una persona. **El `.xlsx` sí los guarda todos**, y
por eso el aviso remite a él.
"""

from reportes.documento import TONO_BUENO, TONO_MALO
from reportes.formato_pdf import (
    ALTO_PAGINA,
    ANCHO_PAGINA,
    Linea,
    Raya,
    Rectangulo,
    Trazo,
    a_winansi,
    bytes_del_pdf,
)

MARGEN = 36

TAMANO_DEL_TITULO = 14
TAMANO_DEL_SUBTITULO = 10
TAMANO_DE_SECCION = 11
TAMANO_NORMAL = 8

# La portada: el titular, la frase y las cuatro cifras grandes.
TAMANO_DEL_TITULAR = 17
TAMANO_DE_LA_FRASE = 10
TAMANO_DE_UNA_CIFRA = 22
TAMANO_DEL_ROTULO_DE_UNA_CIFRA = 8

# Los colores del informe. Los mismos del proyecto viejo, que es de donde sale este
# documento. El rojo y el verde no son adorno: son la diferencia entre «esto salio
# mal» y «esto salio bien» vista desde el otro lado de una mesa de reuniones.
NEGRO = "#151515"
TINTA = "#16161A"
ROJO = "#D0342C"
VERDE = "#1F8A5F"
GRIS = "#6F6F76"
TENUE = "#A0A0A8"
LINEA_FINA = "#E6E6E6"

COLOR_POR_TONO = {TONO_MALO: ROJO, TONO_BUENO: VERDE}

# La cinta negra de arriba y el pie, iguales en todas las paginas.
ALTO_DE_LA_CINTA = 42
ALTURA_DEL_TEXTO_DE_LA_CINTA = 16
ALTURA_DE_LA_RAYA_DEL_PIE = 34
ALTURA_DEL_TEXTO_DEL_PIE = 22
TAMANO_DE_LA_CINTA = 11
TAMANO_DEL_PIE = 7

ROTULO_DE_LA_CINTA = "Preparación para el templo"
NOTA_DEL_PIE = (
    "La preparación es la que consta en el sistema del líder, no en el formulario."
)

# Donde empieza y donde acaba el texto de una pagina. Arriba manda la cinta: sin
# este hueco, la primera linea se escribiria encima de ella.
TOPE_SUPERIOR = ALTO_PAGINA - ALTO_DE_LA_CINTA - 12
TOPE_INFERIOR = ALTURA_DE_LA_RAYA_DEL_PIE + 12

# Cuanto baja el cursor por cada linea, en proporcion al tamano de la letra.
PROPORCION_DEL_INTERLINEADO = 1.45
# Ancho medio de un caracter de Helvetica en proporcion al tamano. Es una
# aproximacion —Helvetica es de ancho variable— y basta para repartir columnas y
# partir lineas: el reparto es proporcional al ancho de la pagina, asi que una
# columna nunca se sale por la derecha aunque la estimacion se quede corta.
PROPORCION_DEL_ANCHO_DE_CARACTER = 0.5


def _ancho_util():
    """Los puntos de ancho que quedan entre los dos margenes."""
    return ANCHO_PAGINA - 2 * MARGEN


def _capacidad(ancho_en_puntos, tamano):
    """Cuantos caracteres caben en ese ancho con esa letra. Al menos uno.

    Al menos uno y no cero: con capacidad cero el partido de lineas no avanzaria
    nunca y se quedaria dando vueltas sobre la misma palabra.
    """
    return max(1, int(ancho_en_puntos / (tamano * PROPORCION_DEL_ANCHO_DE_CARACTER)))


def partir_en_lineas(texto, capacidad):
    """Parte un texto en lineas de como mucho `capacidad` caracteres, por palabras.

    Una palabra mas larga que la capacidad se parte por donde toque en vez de
    desbordar la columna: preferimos un MRN partido en dos lineas a un MRN escrito
    encima de la columna de al lado.
    """
    if texto is None or texto == "":
        return [""]
    lineas = []
    for palabra in str(texto).split():
        while len(palabra) > capacidad:
            lineas.append(palabra[:capacidad])
            palabra = palabra[capacidad:]
        if lineas and lineas[-1] and len(lineas[-1]) + 1 + len(palabra) <= capacidad:
            lineas[-1] = f"{lineas[-1]} {palabra}"
        else:
            lineas.append(palabra)
    return lineas or [""]


def _linea(texto, tamano, negrita=False, repetir=False):
    """Una linea suelta que empieza en el margen izquierdo."""
    return Linea(negrita, tamano, (Trazo(MARGEN, texto),), repetir)


def _linea_en_blanco(tamano=TAMANO_NORMAL):
    """El hueco entre bloques. Es una linea sin trazos: ocupa alto y no pinta."""
    return Linea(False, tamano, (), False)


def _lineas_de_parrafo(texto, tamano):
    """Un parrafo largo partido en lineas del ancho de la pagina."""
    capacidad = _capacidad(_ancho_util(), tamano)
    return [_linea(trozo, tamano) for trozo in partir_en_lineas(texto, capacidad)]


def _reparto_de_columnas(columnas):
    """La `x` donde empieza cada columna y cuantos caracteres le caben.

    El reparto es **proporcional** al ancho que declara cada columna, no fijo: asi
    la tabla ocupa siempre el ancho de la pagina y nunca se sale por la derecha,
    tenga las columnas que tenga.
    """
    total = sum(columna.ancho for columna in columnas) or 1
    posiciones = []
    x = MARGEN
    for columna in columnas:
        ancho_en_puntos = _ancho_util() * columna.ancho / total
        posiciones.append((x, _capacidad(ancho_en_puntos, TAMANO_NORMAL) - 1))
        x += ancho_en_puntos
    return posiciones


def _lineas_de_una_fila(valores, reparto, negrita):
    """Una fila de tabla, que ocupa tantas lineas como la celda mas larga.

    Cada celda se parte por su cuenta y las celdas cortas dejan hueco en blanco
    debajo. Es la unica forma de que el motivo de por que alguien no viajo —que es
    texto libre de hasta 300 caracteres— se lea entero sin recortarlo.
    """
    partidas = [
        partir_en_lineas("" if valor is None else valor, capacidad)
        for valor, (_, capacidad) in zip(valores, reparto)
    ]
    alto = max(len(celda) for celda in partidas)
    lineas = []
    for numero in range(alto):
        trazos = tuple(
            Trazo(x, celda[numero])
            for celda, (x, _) in zip(partidas, reparto)
            if numero < len(celda) and celda[numero]
        )
        lineas.append(Linea(negrita, TAMANO_NORMAL, trazos, False))
    return lineas


def _lineas_de_la_tabla(seccion):
    """Los titulos de columna —que se repiten en cada pagina— y todas las filas."""
    reparto = _reparto_de_columnas(seccion.columnas)
    titulos = _lineas_de_una_fila(
        [columna.nombre for columna in seccion.columnas], reparto, negrita=True
    )
    lineas = [linea._replace(repetir=True) for linea in titulos]
    for valores in seccion.filas:
        lineas.extend(_lineas_de_una_fila(valores, reparto, negrita=False))
    return lineas


def _lineas_de_la_seccion(seccion):
    """Titulo, notas, tabla y resumen de una seccion, en ese orden."""
    lineas = [_linea_en_blanco(), _linea(seccion.titulo, TAMANO_DE_SECCION, negrita=True)]
    for nota in seccion.notas:
        lineas.extend(_lineas_de_parrafo(nota, TAMANO_NORMAL))
    lineas.append(_linea_en_blanco())
    lineas.extend(_lineas_de_la_tabla(seccion))
    if seccion.resumen is not None:
        lineas.append(_linea_en_blanco())
        lineas.append(_linea(seccion.resumen, TAMANO_NORMAL, negrita=True))
    return lineas


def _lineas_de_las_cifras(cifras):
    """Las cuatro cifras grandes en una fila, cada una con su rotulo debajo.

    Van repartidas por igual a lo ancho de la pagina, y no una detras de otra: las
    cuatro tienen que verse de un vistazo desde el otro lado de una mesa, que es
    para lo que existen.

    Son dos lineas y no cuatro bloques: los numeros arriba, todos a la misma altura,
    y los rotulos abajo, todos a la misma. Si cada cifra se colocara por su cuenta,
    un numero de cuatro digitos bajaria su rotulo y la fila quedaria escalonada.
    """
    if not cifras:
        return []
    paso = _ancho_util() / len(cifras)
    numeros = tuple(
        Trazo(MARGEN + int(indice * paso), str(cifra.numero), COLOR_POR_TONO.get(cifra.tono, TINTA))
        for indice, cifra in enumerate(cifras)
    )
    rotulos = tuple(
        Trazo(MARGEN + int(indice * paso), cifra.rotulo, GRIS)
        for indice, cifra in enumerate(cifras)
    )
    return [
        Linea(True, TAMANO_DE_UNA_CIFRA, numeros, False),
        Linea(False, TAMANO_DEL_ROTULO_DE_UNA_CIFRA, rotulos, False),
    ]


def lineas_de_la_portada(portada):
    """Lo primero que se lee: el titular, la frase y las cuatro cifras.

    Va antes que los avisos y que cualquier tabla. El informe abre por el numero que
    se mide y no por lo que luce, que es la decision del proyecto viejo y la del
    dueno: una persona que viaja sin la preparacion completa no hace la ordenanza.
    """
    lineas = [
        _linea_en_blanco(),
        _linea(portada.titular, TAMANO_DEL_TITULAR, negrita=True),
    ]
    lineas.extend(_lineas_de_parrafo(portada.frase, TAMANO_DE_LA_FRASE))
    lineas.append(_linea_en_blanco())
    lineas.extend(_lineas_de_las_cifras(portada.cifras))
    return lineas


def marco_de_la_pagina(subtitulo):
    """La cinta de arriba y el pie, iguales en todas las paginas.

    Devuelve una funcion porque el pie lleva el numero de pagina, y ese cambia. Lo
    que dibuja: una cinta negra con el nombre del documento y la fecha, una raya
    fina abajo, la nota de que la preparacion es la del sistema del lider, y
    «Pagina N de M».

    El «de M» no lo tenia el viejo y se anade aqui: un informe que se imprime y se
    reparte tiene que decir si esta entero, y una pagina 3 suelta sin el total no
    dice si faltan dos.
    """
    def marco(numero, total):
        adornos = (
            Rectangulo(0, ALTO_PAGINA - ALTO_DE_LA_CINTA, ANCHO_PAGINA, ALTO_DE_LA_CINTA, NEGRO),
            Raya(MARGEN, ALTURA_DE_LA_RAYA_DEL_PIE, ANCHO_PAGINA - MARGEN, LINEA_FINA, 0.5),
        )
        y_de_la_cinta = ALTO_PAGINA - ALTO_DE_LA_CINTA + ALTURA_DEL_TEXTO_DE_LA_CINTA
        pagina_de = f"Página {numero} de {total}"
        lineas = (
            (
                y_de_la_cinta,
                Linea(True, TAMANO_DE_LA_CINTA,
                      (Trazo(MARGEN, ROTULO_DE_LA_CINTA, "#FFFFFF"),), False),
            ),
            (
                y_de_la_cinta,
                Linea(False, TAMANO_DEL_PIE,
                      (Trazo(_x_para_terminar_en_el_margen(subtitulo, TAMANO_DEL_PIE),
                             subtitulo, "#B9B9BE"),), False),
            ),
            (
                ALTURA_DEL_TEXTO_DEL_PIE,
                Linea(False, TAMANO_DEL_PIE, (Trazo(MARGEN, NOTA_DEL_PIE, TENUE),), False),
            ),
            (
                ALTURA_DEL_TEXTO_DEL_PIE,
                Linea(False, TAMANO_DEL_PIE,
                      (Trazo(_x_para_terminar_en_el_margen(pagina_de, TAMANO_DEL_PIE),
                             pagina_de, TENUE),), False),
            ),
        )
        return adornos, lineas

    return marco


def _x_para_terminar_en_el_margen(texto, tamano):
    """Donde empezar un texto para que acabe en el margen derecho.

    Es una estimacion: Helvetica es de ancho variable y aqui se usa el ancho medio,
    el mismo con el que se reparten las columnas. Basta para el pie —si sobra o
    falta un punto no se nota— y evita tener que incrustar las metricas de la fuente
    solo para colocar un numero de pagina.
    """
    ancho = len(str(texto)) * tamano * PROPORCION_DEL_ANCHO_DE_CARACTER
    return max(MARGEN, int(ANCHO_PAGINA - MARGEN - ancho))


def lineas_del_documento(documento, avisos_extra=()):
    """El documento entero convertido en lineas, todavia sin repartir en paginas."""
    lineas = [
        _linea(documento.titulo, TAMANO_DEL_TITULO, negrita=True),
        _linea(documento.subtitulo, TAMANO_DEL_SUBTITULO),
        _linea(f"Generado el {documento.generado_en}", TAMANO_NORMAL),
    ]
    lineas.extend(lineas_de_la_portada(documento.portada))
    for aviso in tuple(avisos_extra) + tuple(documento.avisos):
        lineas.append(_linea_en_blanco())
        lineas.extend(_lineas_de_parrafo(aviso, TAMANO_NORMAL))
    for seccion in documento.secciones:
        lineas.extend(_lineas_de_la_seccion(seccion))
    return lineas


class _RepartoEnPaginas:
    """Lleva la cuenta de por donde va la pagina y cual es el encabezado vigente.

    Va como clase y no como una funcion con cinco variables sueltas porque son
    justo eso: cinco cosas que cambian a la vez y que hay que dejar coherentes en
    cada salto de pagina.
    """

    def __init__(self):
        self._paginas = []
        self._pagina = []
        self._y = TOPE_SUPERIOR
        self._encabezado = []
        self._la_anterior_era_encabezado = False

    def anotar_el_encabezado(self, linea):
        """Guarda las lineas que hay que repetir arriba de cada pagina nueva.

        Un titulo de seccion borra el encabezado vigente: si la pagina se corta
        entre el titulo de la seccion nueva y su tabla, repetir arriba los titulos
        de columna de la tabla ANTERIOR seria peor que no repetir ninguno.
        """
        if linea.repetir and not self._la_anterior_era_encabezado:
            self._encabezado = []
        if linea.repetir:
            self._encabezado.append(linea)
        elif linea.tamano == TAMANO_DE_SECCION:
            self._encabezado = []
        self._la_anterior_era_encabezado = linea.repetir

    def _bajar_y_colocar(self, linea):
        """Baja el cursor el alto de la linea y la coloca en esa base."""
        self._y -= linea.tamano * PROPORCION_DEL_INTERLINEADO
        self._pagina.append((self._y, linea))

    def _cambiar_de_pagina(self):
        """Cierra la pagina y abre otra con el encabezado vigente repetido."""
        self._paginas.append(self._pagina)
        self._pagina = []
        self._y = TOPE_SUPERIOR
        for repetida in self._encabezado:
            self._bajar_y_colocar(repetida)

    def colocar(self, linea):
        """Sitúa una linea, cambiando de pagina si ya no cabe."""
        if (
            self._y - linea.tamano * PROPORCION_DEL_INTERLINEADO < TOPE_INFERIOR
            and self._pagina
        ):
            self._cambiar_de_pagina()
        self._bajar_y_colocar(linea)

    def paginas(self):
        """Las paginas repartidas. Siempre al menos una, aunque este vacia."""
        if self._pagina:
            self._paginas.append(self._pagina)
        return self._paginas or [[]]


def repartir_en_paginas(lineas):
    """Reparte las lineas en paginas y devuelve cada una con su `y` ya calculada.

    Los titulos de columna llevan `repetir=True` y se vuelven a dibujar arriba de
    cada pagina nueva: una tabla que sigue en la pagina 3 sin sus titulos es una
    rejilla de numeros sin nombre.
    """
    reparto = _RepartoEnPaginas()
    for linea in lineas:
        reparto.anotar_el_encabezado(linea)
        reparto.colocar(linea)
    return reparto.paginas()


def contar_caracteres_que_no_caben(documento):
    """Cuantos caracteres del documento entero no existen en WinAnsi."""
    textos = [documento.titulo, documento.subtitulo, documento.generado_en]
    textos.append(documento.portada.titular)
    textos.append(documento.portada.frase)
    textos.extend(cifra.rotulo for cifra in documento.portada.cifras)
    textos.extend(documento.avisos)
    for seccion in documento.secciones:
        textos.extend([seccion.titulo, seccion.resumen or ""])
        textos.extend(seccion.notas)
        textos.extend(columna.nombre for columna in seccion.columnas)
        for fila in seccion.filas:
            textos.extend("" if valor is None else str(valor) for valor in fila)
    return sum(a_winansi(texto)[1] for texto in textos)


def aviso_de_caracteres_perdidos(cuantos):
    """El aviso que encabeza el PDF cuando algun caracter no cupo en la fuente."""
    return (
        f"AVISO: {cuantos} caracteres de este reporte no existen en la codificación "
        "que usan las fuentes básicas de PDF y salen escritos como «?». Casi "
        "siempre es un nombre con un signo poco corriente. El archivo .xlsx del "
        "mismo período los guarda todos sin alterar: para comprobar un nombre o un "
        "MRN, mírelo allí."
    )


def construir_pdf(documento):
    """El PDF entero del documento, como bytes. No toca el disco."""
    perdidos = contar_caracteres_que_no_caben(documento)
    avisos_extra = (aviso_de_caracteres_perdidos(perdidos),) if perdidos else ()
    paginas = repartir_en_paginas(lineas_del_documento(documento, avisos_extra))
    return bytes_del_pdf(paginas, marco_de_la_pagina(str(documento.generado_en)))


def escribir_pdf(documento, ruta_parcial):
    """Escribe el PDF en esa ruta. Quien la asciende a definitiva es `rutas`."""
    ruta_parcial.write_bytes(construir_pdf(documento))
