"""Los avisos de la cabecera de Corrección: una línea cada uno, y su detalle aparte.

**Lo que dijo el dueño el 2026-09-04:** «en esa misma página las letras en
amarillo toman todo el espacio, es demasiado texto». Y el 2026-09-03, sobre otra
pantalla: «Nadie que use ese sistema va a leer tanto. Ese es el error: el texto
ocupa todo el programa».

**Lo medido antes de escribir esto**, sobre un caso con los cinco avisos (sin
número, ilegible, dos renglones de la importación y el escaneo que no se abre),
ventana de 1100 × 720:

    5 etiquetas · la peor ocupa 5 líneas · 310 px de avisos
    región central: 241 px

O sea: los avisos ocupaban más que los campos que hay que corregir.

**Lo que este archivo hace, y lo que no.** Parte cada aviso en dos: la **línea**
que va en `interfaz.avisos.FranjaDeAvisos` —una sola, con la cuenta delante— y el
**detalle**, que es el texto entero de antes y se lee al pedirlo. Nada se pierde:
`PantallaDeCorreccion._textos_de_aviso()` sigue devolviendo los detalles enteros,
que es lo que las pruebas de los pases anteriores comprueban palabra por palabra.

No dibuja nada y no toca la base: recibe datos y devuelve pares. Así las frases se
pueden leer en una prueba sin abrir una ventana, que es la única forma de que
alguien las mire de verdad.
"""

from collections import namedtuple

from datos.ilegibles import frase_del_motivo, linea_del_motivo
from datos.validacion import avisos_de_caso

# Lo que cabe en la línea de la franja. La franja pone la cuenta delante —«1 caso
# sin número…»— y el resumen junta dos grupos en una línea de 1100 px; con 90
# caracteres por frase los dos caben sin partir.
LARGO_MAXIMO_DE_LA_LINEA = 90

# La línea que se lee de un vistazo y el texto entero que se lee al pedirlo.
AvisoDeCabecera = namedtuple("AvisoDeCabecera", "linea detalle")


def agrupados_por_linea(avisos):
    """Junta los que dicen la misma linea y **pega sus detalles**, en orden.

    ⚠️ **Sin esto se pierde informacion, y es el riesgo entero de acortar.**
    `interfaz.avisos.agrupar` junta los avisos con el mismo texto y se queda con
    el `al_ver` del PRIMERO: dos hojas del mismo caso con el mismo motivo saldrian
    como «2 páginas con etiquetas del formulario sin encontrar» y «ver cuáles»
    ensenaria solo lo que se vio en la primera. Aqui los detalles se pegan antes,
    asi que el desplegable los tiene los dos.

    Devuelve tripletes `(linea, cuantos, detalle)`. El orden de aparicion se
    conserva: un aviso que salta de sitio se lee como un aviso nuevo.
    """
    juntos = {}
    for aviso in avisos:
        if aviso.linea in juntos:
            cuantos, detalle = juntos[aviso.linea]
            juntos[aviso.linea] = (cuantos + 1, f"{detalle}\n\n———\n\n{aviso.detalle}")
        else:
            juntos[aviso.linea] = (1, aviso.detalle)
    return [(linea, cuantos, detalle) for linea, (cuantos, detalle) in juntos.items()]


def avisos_de_la_cabecera(caso, fecha_en_pantalla, renglones, motivo_del_visor):
    """Todos los avisos de un caso, en el orden en que se leen.

    `fecha_en_pantalla` es lo que hay escrito AHORA en el campo de la fecha, no lo
    guardado: el aviso del mes cruzado tiene que aparecer y desaparecer mientras se
    teclea, sin esperar a Guardar. `renglones` son las filas de
    `documentos_ilegibles` de este caso y `motivo_del_visor` lo que impide ver el
    escaneo, o nulo.

    No consulta la base ni dibuja: recibe lo leido y devuelve pares. Asi las cinco
    frases se pueden comprobar sin abrir una ventana.
    """
    avisos = []
    if caso["numero_caso"] is None:
        avisos.append(aviso_del_numero_que_falta())
    if caso["captura_manual"]:
        avisos.append(aviso_del_formulario_ilegible())
    avisos.extend(
        aviso_de_la_validacion(texto)
        for texto in avisos_de_caso(caso["numero_caso"], fecha_en_pantalla)
    )
    avisos.extend(
        aviso_de_lo_que_vio_la_importacion(
            renglon["motivo"], renglon["pagina_pdf"], renglon["detalle"]
        )
        for renglon in renglones
    )
    if motivo_del_visor:
        avisos.append(aviso_del_escaneo_que_no_se_ve(motivo_del_visor))
    return avisos


def aviso_del_numero_que_falta():
    """El caso entró sin número porque no se pudo leer."""
    return AvisoDeCabecera(
        "caso sin número: mírelo en el PDF (F2) y escríbalo en «N.º de caso»",
        "Este caso entró SIN número de caso porque no se pudo leer, y todo lo demás "
        "que traía la página SÍ se guardó: no se ha perdido nada. Mire el PDF con F2 "
        "y escriba el número arriba, en «N.º de caso». Hasta que lo tenga, este caso "
        "no se puede cruzar con el Excel que devuelven los compañeros.",
    )


def aviso_del_formulario_ilegible():
    """El escaneo no se pudo leer y los campos vienen vacíos a propósito."""
    return AvisoDeCabecera(
        "formulario ilegible: los campos vienen vacíos, teclee mirando el PDF (F2)",
        "Formulario ilegible: los campos vienen VACÍOS a propósito, no se perdieron. "
        "Sobre un escaneo que no se lee el sistema no insiste ni adivina. Teclee "
        "mirando la imagen; la tira del escaneo de cada campo sigue estando al lado, "
        "y F2 abre el PDF en esta página.",
    )


def aviso_de_la_validacion(texto):
    """Uno de los avisos que devuelve `datos.validacion.avisos_de_caso`.

    ⚠️ **La línea nombra el mes cruzado porque hoy ese es el único aviso que esa
    función produce** —lo dice su docstring y lo fija `pruebas/prueba_validacion.py`
    con tres asertos—. El detalle es siempre el texto literal que devolvió, así que
    el día que aparezca un segundo aviso lo único que habrá que corregir es esta
    línea; el texto completo seguirá siendo exacto.
    """
    return AvisoDeCabecera(
        "fecha de viaje que no cuadra con el mes del número de caso", texto
    )


def aviso_de_lo_que_vio_la_importacion(motivo, pagina_pdf, detalle):
    """Un renglón de `documentos_ilegibles` de este caso.

    El «Lo que se vio, página N…» es justo el trozo que hacía de este aviso un
    párrafo de cinco líneas, y es también el que hay que poder mirar cuando se
    duda: por eso baja al detalle en vez de desaparecer.
    """
    completo = frase_del_motivo(motivo)
    if detalle:
        completo += f"\n\nLo que se vio, página {pagina_pdf} del PDF: {detalle}"
    return AvisoDeCabecera(linea_del_motivo(motivo), completo)


def aviso_del_escaneo_que_no_se_ve(motivo_del_fallo):
    """El PDF de este caso no se pudo abrir; los campos se corrigen igual."""
    return AvisoDeCabecera(
        "escaneo que no se puede mostrar: los campos se corrigen igual, sin imagen",
        f"No se puede mostrar el escaneo de este caso: {motivo_del_fallo}. Los campos "
        "se pueden corregir igual, pero sin la imagen al lado.",
    )
