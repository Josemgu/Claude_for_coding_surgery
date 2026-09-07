"""El formato de archivo PDF: codificacion, objetos y tabla de referencias.

Este modulo sabe como se escribe un PDF y **no sabe nada del reporte**. Lo que
recibe son lineas ya colocadas —cada una con su `y`, su fuente y sus trazos— y lo
que devuelve son bytes. La otra mitad, decidir que linea va donde, es de
`reportes/pdf.py`.

Van separados porque son dos conocimientos distintos: aqui esta lo que exige el
formato —que la tabla `xref` cuadre al byte, que las fuentes base se declaren con
`/WinAnsiEncoding`— y alli esta lo que exige el reporte —que el motivo se lea
entero, que los titulos de columna se repitan—. Mezclados, cambiar el ancho de una
columna obliga a leer el codigo que numera objetos.

**Sin ninguna biblioteca.** `PENDIENTES.md` (FASE 8) prohibe engordar el
ejecutable, y la FASE 0 dejo demostrado que un PDF de texto se escribe a mano.
`requirements.txt` no cambia por este archivo.

⚠️ **Y desde el 2026-09-03 tampoco lo cambia el informe con color.** El pase de ese
dia autorizaba anadir `reportlab` «si es la via mas corta» a que el informe salga
como el del proyecto viejo, midiendo cuanto engorda el paquete. No hizo falta: lo
que el informe del viejo necesita de un motor de dibujo son **dos operadores**
—rellenar un rectangulo y trazar una raya— mas poder cambiar el color del texto, y
los tres son cuatro lineas de PDF crudo. Estan aqui abajo, en `_flujo_de_los_adornos`
y en `_color_de_relleno`. `reportlab` habria anadido 8,1 MiB de biblioteca
(medidos: 350 archivos en `site-packages/reportlab`) a un paquete que ya pesa
217 MiB, para no escribirlas.

Lo que este modulo pinta y lo que NO: rectangulos rellenos, rayas rectas y texto de
color. No hay curvas, ni imagenes, ni transparencias, ni degradados. Si algun dia
hiciera falta un grafico de verdad, esa si es una conversacion sobre `reportlab`.

Se usan las dos fuentes base que todo visor trae obligatoriamente, Helvetica y
Helvetica-Bold, con `/WinAnsiEncoding`. No se incrusta ninguna: incrustarla es lo
que hace pesados los PDF y aqui no hace falta.
"""

from collections import namedtuple

ANCHO_PAGINA = 792
ALTO_PAGINA = 612

# Una linea colocada: si va en negrita, de que tamano, y los trozos de texto con
# la `x` donde empieza cada uno. Una linea de tabla tiene un trazo por columna;
# una de parrafo, uno solo.
Linea = namedtuple("Linea", ("negrita", "tamano", "trazos", "repetir"))

# `color` es un `#RRGGBB` o `None`, que significa negro. Va en el trazo y no en la
# linea porque lo que se pinta de rojo en este informe es UNA celda de una fila —la
# columna «Sin verificar» del caso que salio mal— y no el renglon entero.
Trazo = namedtuple("Trazo", ("x", "texto", "color"), defaults=(None,))

# Lo que se dibuja detras del texto. Son las dos unicas formas que hace falta
# pintar: la cinta negra de arriba y la raya fina del pie.
Rectangulo = namedtuple("Rectangulo", ("x", "y", "ancho", "alto", "color"))
Raya = namedtuple("Raya", ("x", "y", "hasta_x", "color", "grosor"))

NEGRO = "#151515"


def _componentes(color):
    """Un `#RRGGBB` como los tres numeros de 0 a 1 que espera el PDF.

    El PDF no entiende hexadecimal: sus operadores de color toman tres fracciones.
    Un color que no se puede leer sale negro, que es el que ya tenia el documento
    antes de que existieran los colores: un fallo de formato no puede dejar un
    numero invisible.
    """
    crudo = (color or NEGRO).lstrip("#")
    if len(crudo) != 6:
        crudo = NEGRO.lstrip("#")
    try:
        return tuple(int(crudo[i:i + 2], 16) / 255.0 for i in (0, 2, 4))
    except ValueError:
        return _componentes(NEGRO)


def _color_de_relleno(color):
    """La instruccion que fija el color con el que se rellena a partir de ahi."""
    return b"%.3f %.3f %.3f rg\n" % _componentes(color)


def _color_de_trazo(color):
    """La instruccion que fija el color con el que se dibujan las rayas."""
    return b"%.3f %.3f %.3f RG\n" % _componentes(color)


def a_winansi(texto):
    """Codifica a WinAnsi y devuelve `(bytes, cuantos caracteres no cupieron)`.

    Se recorre caracter a caracter en vez de usar `errors='replace'` para poder
    CONTAR los que se pierden. Sin ese conteo, un nombre alterado saldria igual y
    nadie se enteraria, que es exactamente lo que este proyecto no hace.
    """
    crudo = bytearray()
    perdidos = 0
    for caracter in texto:
        try:
            crudo += caracter.encode("cp1252")
        except UnicodeEncodeError:
            crudo += b"?"
            perdidos += 1
    return bytes(crudo), perdidos


def escapar(crudo):
    """Escapa los tres caracteres que un literal de cadena de PDF no admite."""
    for original, sustituto in ((b"\\", b"\\\\"), (b"(", b"\\("), (b")", b"\\)")):
        crudo = crudo.replace(original, sustituto)
    return crudo


def _flujo_de_los_adornos(adornos):
    """Lo que se pinta DETRAS del texto: la cinta de arriba y las rayas.

    Va antes del bloque de texto a proposito. Un rectangulo dibujado despues taparia
    lo que hay debajo, y la cinta negra de la cabecera se comeria su propio titulo.
    """
    partes = []
    for adorno in adornos:
        if isinstance(adorno, Rectangulo):
            partes.append(_color_de_relleno(adorno.color))
            partes.append(
                b"%d %d %d %d re f\n"
                % (int(adorno.x), int(adorno.y), int(adorno.ancho), int(adorno.alto))
            )
        elif isinstance(adorno, Raya):
            partes.append(_color_de_trazo(adorno.color))
            partes.append(b"%.2f w\n" % adorno.grosor)
            partes.append(
                b"%d %d m %d %d l S\n"
                % (int(adorno.x), int(adorno.y), int(adorno.hasta_x), int(adorno.y))
            )
    return b"".join(partes)


def _flujo_de_una_pagina(pagina, adornos=()):
    """El flujo de contenido que dibuja una pagina, en el lenguaje del PDF.

    La instruccion de fuente solo se emite cuando cambia, y la de color tambien. No
    es por ahorrar bytes: un `Tf` por cada trazo llenaria el flujo de repeticiones y
    haria ilegible lo unico que se puede inspeccionar de un PDF escrito a mano.

    ⚠️ El color se fija a negro al abrir el bloque de texto **siempre**. El color de
    relleno es de estado, no de operacion: el que dejo puesto el ultimo rectangulo
    de la cinta seguiria vigente dentro del texto, y el informe entero saldria
    escrito del color de la cinta.
    """
    partes = [_flujo_de_los_adornos(adornos), b"BT\n", _color_de_relleno(NEGRO)]
    fuente_puesta = None
    color_puesto = NEGRO
    for y, linea in pagina:
        if not linea.trazos:
            continue
        fuente = (b"/F2" if linea.negrita else b"/F1", linea.tamano)
        if fuente != fuente_puesta:
            partes.append(b"%s %d Tf\n" % fuente)
            fuente_puesta = fuente
        for trazo in linea.trazos:
            color = trazo.color or NEGRO
            if color != color_puesto:
                partes.append(_color_de_relleno(color))
                color_puesto = color
            partes.append(b"1 0 0 1 %d %d Tm\n" % (int(trazo.x), int(y)))
            partes.append(b"(" + escapar(a_winansi(trazo.texto)[0]) + b") Tj\n")
    partes.append(b"ET\n")
    return b"".join(partes)


def _objetos_del_pdf(paginas, marco=None):
    """Los objetos del PDF en el orden en que se numeran, empezando por el 1.

    La numeracion no es libre: cada pagina apunta a su flujo de contenido y a las
    dos fuentes por numero de objeto, asi que el reparto se calcula antes de
    escribir nada. Las paginas ocupan los pares (3, 5, 7...) y sus flujos los
    impares siguientes; las dos fuentes van al final.
    """
    total = len(paginas)
    numero_de_f1 = 3 + 2 * total
    numero_de_f2 = numero_de_f1 + 1
    hijos = b" ".join(b"%d 0 R" % (3 + 2 * indice) for indice in range(total))

    objetos = [
        b"<< /Type /Catalog /Pages 2 0 R >>",
        b"<< /Type /Pages /Kids [" + hijos + b"] /Count %d >>" % total,
    ]
    for indice, pagina in enumerate(paginas):
        adornos, anadidas = ((), ()) if marco is None else marco(indice + 1, total)
        flujo = _flujo_de_una_pagina(list(pagina) + list(anadidas), adornos)
        objetos.append(
            b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 %d %d] "
            b"/Resources << /Font << /F1 %d 0 R /F2 %d 0 R >> >> /Contents %d 0 R >>"
            % (ANCHO_PAGINA, ALTO_PAGINA, numero_de_f1, numero_de_f2, 4 + 2 * indice)
        )
        objetos.append(b"<< /Length %d >>\nstream\n" % len(flujo) + flujo + b"endstream")
    objetos.append(
        b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica "
        b"/Encoding /WinAnsiEncoding >>"
    )
    objetos.append(
        b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold "
        b"/Encoding /WinAnsiEncoding >>"
    )
    return objetos


def _ensamblar(objetos):
    """Pega los objetos con su tabla de referencias cruzadas y su trailer.

    La tabla `xref` no es decorativa: un visor la usa para saltar a un objeto sin
    leer el archivo entero, y si los desplazamientos no cuadran al byte el visor
    dice «archivo dañado». Por eso se anota la posicion real de cada objeto
    mientras se escribe, en vez de calcularla despues.
    """
    salida = bytearray(b"%PDF-1.4\n%\xe1\xe9\xf1\n")
    desplazamientos = []
    for numero, cuerpo in enumerate(objetos, start=1):
        desplazamientos.append(len(salida))
        salida += b"%d 0 obj\n" % numero + cuerpo + b"\nendobj\n"

    inicio_xref = len(salida)
    salida += b"xref\n0 %d\n" % (len(objetos) + 1)
    salida += b"0000000000 65535 f \n"
    for desplazamiento in desplazamientos:
        salida += b"%010d 00000 n \n" % desplazamiento
    salida += b"trailer\n<< /Size %d /Root 1 0 R >>\nstartxref\n%d\n%%%%EOF\n" % (
        len(objetos) + 1,
        inicio_xref,
    )
    return bytes(salida)


def bytes_del_pdf(paginas, marco=None):
    """El archivo PDF entero de esas paginas ya colocadas.

    `marco` es lo que se repite en todas las paginas —la cinta de arriba y el pie—
    y se le pasa como funcion `(numero_de_pagina, total) -> (adornos, lineas)`. Va
    como funcion y no como una lista fija porque el pie lleva el numero de pagina, y
    ese cambia en cada una.
    """
    return _ensamblar(_objetos_del_pdf(paginas, marco))
