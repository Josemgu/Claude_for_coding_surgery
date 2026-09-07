"""La tira del escaneo: guardar donde estaba una banda y volver a cortarla.

Este modulo es la mitad de la pantalla de correccion. Sin el, corregir un campo
obliga a abrir el PDF entero, buscar la pagina, buscar la fila y volver: minutos
por campo, en una tarea que tiene que costar segundos.

**Por que se guardan FRACCIONES de la pagina y no pixeles.** La banda la calcula
`extraccion/bandas.py` en pixeles de la rasterizacion de ese momento, que se hizo
a la escala que salio de `escala_de_rasterizado` para esa pagina concreta. Guardar
esos pixeles ata el recorte a esa escala para siempre: el dia que el tope de
3500 px cambie, o que la pagina se rasterice mas pequena para caber en la
pantalla, el rectangulo guardado apuntaria a otro sitio del papel y la tira
ensenaria un campo que no es. Una fraccion de la pagina vale a cualquier escala.

**Lo que este modulo NO hace:** no lee, no interpreta y no arregla la imagen. Solo
recorta y cambia de tamano. Todo lo que sale de aqui es un trozo del escaneo tal
como estaba (regla permanente 1).
"""

import numpy

# Alto de la tira que se dibuja al lado del campo, en pixeles. El pase lo fija
# entre 40 y 50.
ALTO_DE_LA_TIRA_PX = 46

# Ancho del hueco donde la pantalla dibuja la tira, en pixeles. Esta aqui, junto
# al alto, y no en `interfaz/tira.py`, porque los dos numeros tienen que salir del
# MISMO sitio.
#
# ⚠️ Que estuvieran separados es el fallo que esto arregla, medido en la FASE 9:
# el hueco media 380 px y la imagen salia de 610, asi que se veian los cinco
# primeros digitos de un numero de unidad de seis y **el ultimo no habia forma de
# comprobarlo**. Un numero se valida por el ultimo digito tanto como por el
# primero, y la tira existe justo para eso. Mientras `escalar_para_caber` reciba
# este mismo numero como tope, la imagen no puede volver a salirse del hueco.
ANCHO_DE_LA_TIRA_PX = 380

# Cuanto se ensancha el recorte por arriba y por abajo respecto a la banda, en
# multiplos de su alto. La banda que calcula `banda_de_valor` esta ajustada a la
# fila del valor y nada mas; un poco de papel alrededor es lo que permite ver que
# la tira esta bien situada y no cortando un renglon por la mitad.
MARGEN_VERTICAL_DE_LA_TIRA = 0.25


def banda_en_fracciones(rectangulo, ancho_px, alto_px):
    """Pasa un rectangulo en pixeles de la imagen a fracciones de la pagina.

    Devuelve `(x0, y0, x1, y1)` con cada valor entre 0.0 y 1.0, o None si no hay
    rectangulo —el OCR no encontro el ancla de ese campo— o si la imagen no tiene
    tamano. Los valores se recortan a los bordes: una banda que se sale de la
    pagina por la derecha se queda en el borde en vez de guardarse fuera de rango
    y que la rechace el `CHECK` del motor.
    """
    if rectangulo is None or not ancho_px or not alto_px:
        return None
    x0 = min(max(rectangulo.x0 / ancho_px, 0.0), 1.0)
    y0 = min(max(rectangulo.y0 / alto_px, 0.0), 1.0)
    x1 = min(max(rectangulo.x1 / ancho_px, 0.0), 1.0)
    y1 = min(max(rectangulo.y1 / alto_px, 0.0), 1.0)
    return (min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1))


def _limites_en_pixeles(banda, ancho_px, alto_px):
    """Los cuatro bordes del recorte ya en pixeles enteros, con su margen.

    El margen se anade solo arriba y abajo. A los lados no: la banda ya nace
    ensanchada hacia la derecha —seis veces el alto del ancla— porque el valor
    suele ser mas ancho que su etiqueta.
    """
    x0, y0, x1, y1 = banda
    alto_de_la_banda = (y1 - y0) * alto_px
    margen = alto_de_la_banda * MARGEN_VERTICAL_DE_LA_TIRA
    return (
        int(max(0, round(x0 * ancho_px))),
        int(max(0, round(y0 * alto_px - margen))),
        int(min(ancho_px, round(x1 * ancho_px))),
        int(min(alto_px, round(y1 * alto_px + margen))),
    )


def recortar(imagen, banda):
    """El trozo de la imagen que corresponde a esa banda, o None si no hay nada.

    `imagen` es la pagina rasterizada en escala de grises, tal como la devuelve
    `extraccion.rasterizado`. `banda` son las cuatro fracciones. Devuelve None
    cuando no hay banda o cuando el recorte sale sin alto o sin ancho, que es lo
    que pasa con una banda pegada a un borde.
    """
    if imagen is None or banda is None:
        return None
    alto_px, ancho_px = imagen.shape[0], imagen.shape[1]
    izquierda, arriba, derecha, abajo = _limites_en_pixeles(banda, ancho_px, alto_px)
    if derecha <= izquierda or abajo <= arriba:
        return None
    return numpy.ascontiguousarray(imagen[arriba:abajo, izquierda:derecha])


def escalar_para_caber(recorte, alto_destino=ALTO_DE_LA_TIRA_PX, ancho_maximo=None):
    """Cambia el tamano del recorte para que quepa ENTERO en el hueco de la tira.

    Manda el alto —`alto_destino`— salvo cuando a esa escala la imagen no cabria
    de ancho; entonces manda el ancho y la tira sale mas baja. **La imagen nunca
    se corta**, y ese es el punto: recortar por la derecha se lleva el ultimo
    caracter del valor, que es la mitad de lo que hay que comprobar.

    Se hace por indices —cada pixel de destino toma el pixel de origen que le
    toca— y no promediando vecinos. Es la operacion mas simple que hay y aqui
    basta, porque el factor de escala es casi 1: la banda mide entre 35 y 53
    pixeles de alto en las paginas de referencia —lo fija `FACTOR_DE_ALTO_DE_LA_BANDA`
    sobre anclas de ese tamano— y el destino son 46. Promediar tendria sentido si
    se estuviera reduciendo a la mitad; a esta escala solo anadiria un desenfoque
    sobre un texto que ya viene de un escaneo.

    ⚠️ Lo que esto NO es: un filtro. No se aclara, no se endereza y no se limpia
    nada. Lo que se ve en la tira es lo que hay en el papel.
    """
    if recorte is None or recorte.size == 0:
        return None
    alto_origen, ancho_origen = recorte.shape[0], recorte.shape[1]
    if alto_origen <= 0 or ancho_origen <= 0 or alto_destino <= 0:
        return None
    factor = alto_destino / alto_origen
    if ancho_maximo is not None and ancho_origen * factor > ancho_maximo:
        factor = ancho_maximo / ancho_origen
    alto_destino = max(1, int(round(alto_origen * factor)))
    ancho_destino = max(1, int(round(ancho_origen * factor)))

    filas = numpy.minimum(
        (numpy.arange(alto_destino) / factor).astype(numpy.int64), alto_origen - 1
    )
    columnas = numpy.minimum(
        (numpy.arange(ancho_destino) / factor).astype(numpy.int64), ancho_origen - 1
    )
    return numpy.ascontiguousarray(recorte[filas][:, columnas])


def region_de_la_pagina(imagen, region):
    """El rectangulo pedido de la pagina, en pixeles, o None si no queda nada.

    `region` son `(x0, y0, x1, y1)` en pixeles de la imagen rasterizada, no
    fracciones: quien la pide es el visor, que ya trabaja en pixeles porque el
    zoom vive ahi. Los bordes se recortan a la hoja, asi que una region mal pasada
    devuelve menos papel en vez de reventar.

    Es la hermana de `recortar()` y no la sustituye: aquella toma la banda en
    fracciones y le anade su margen vertical; esta toma pixeles y no anade nada,
    porque el margen del visor lo pone `interfaz/encuadre.py` al calcular que
    trozo se ve.
    """
    if imagen is None or region is None:
        return None
    alto_px, ancho_px = imagen.shape[0], imagen.shape[1]
    izquierda = int(max(0, min(region[0], ancho_px)))
    arriba = int(max(0, min(region[1], alto_px)))
    derecha = int(max(0, min(region[2], ancho_px)))
    abajo = int(max(0, min(region[3], alto_px)))
    if derecha <= izquierda or abajo <= arriba:
        return None
    return numpy.ascontiguousarray(imagen[arriba:abajo, izquierda:derecha])


def escalar_por_factor(recorte, factor):
    """Cambia el tamano del recorte multiplicando por `factor`. Nada de topes.

    Es lo que `escalar_para_caber` no puede dar al visor: aquella escala a un ALTO
    fijo —los 46 px de la tira— y aqui el alto lo manda el zoom, que puede estar en
    cualquiera de los seis pasos.

    ⚠️ **Se hace por indices, igual que `escalar_para_caber`, y no promediando.**
    Promediar vecinos sobre un escaneo es un filtro, y la regla permanente 1 dice
    que lo que se ve es lo que hay en el papel. Un promediado al reducir suavizaria
    el ruido del escaneo y haria parecer limpio un documento que no lo esta — que
    es exactamente la clase de ayuda que aqui hace dano.

    Nunca devuelve una imagen sin lado: `tk.PhotoImage` no acepta un alto de 0 px.
    """
    if recorte is None or recorte.size == 0 or factor is None or factor <= 0:
        return None
    alto_origen, ancho_origen = recorte.shape[0], recorte.shape[1]
    if alto_origen <= 0 or ancho_origen <= 0:
        return None
    alto_destino = max(1, int(round(alto_origen * factor)))
    ancho_destino = max(1, int(round(ancho_origen * factor)))
    filas = numpy.minimum(
        (numpy.arange(alto_destino) / factor).astype(numpy.int64), alto_origen - 1
    )
    columnas = numpy.minimum(
        (numpy.arange(ancho_destino) / factor).astype(numpy.int64), ancho_origen - 1
    )
    return numpy.ascontiguousarray(recorte[filas][:, columnas])


def vista_de_la_region(imagen, region, factor):
    """De la pagina rasterizada a los bytes PGM de lo que el visor tiene que pintar.

    Es el camino entero en una llamada, igual que `tira_de_la_banda` lo es para la
    tira. Devuelve None en cuanto un paso no tenga con que seguir, para que el
    visor dibuje su rectangulo rayado con el motivo escrito en vez de un hueco
    blanco, que pareceria una hoja en blanco y no lo es.

    **Solo se reescala lo que se ve.** El motivo es un numero: una hoja al tope de
    3500 px del lado largo son unos 9,5 MB de PGM y `tk.PhotoImage` guarda ademas
    su copia interna, asi que reescalar la hoja entera a cada paso de zoom es la
    via directa a una pantalla que tarda segundos en responder.
    """
    return a_pgm(escalar_por_factor(region_de_la_pagina(imagen, region), factor))


def a_pgm(imagen_en_gris):
    """La imagen como bytes en formato PGM binario, que Tk sabe leer sin ayuda.

    Medido en esta maquina (Tk 9.0.4, Python 3.14.7): `tkinter.PhotoImage(data=...)`
    con estos bytes CRUDOS los acepta y construye la imagen. Con los mismos bytes
    en base64 falla con «data stream does not have a PNG signature» — Tk 9 trata
    el texto base64 como PNG y solo el binario como PGM. Por eso se devuelven
    bytes y no texto.

    Esto es lo que evita meter Pillow en el ejecutable solo para ensenar una tira
    de escaneo. PGM binario son doce bytes de cabecera y la matriz detras.
    """
    if imagen_en_gris is None or imagen_en_gris.size == 0:
        return None
    matriz = numpy.ascontiguousarray(imagen_en_gris.astype(numpy.uint8))
    alto, ancho = matriz.shape[0], matriz.shape[1]
    cabecera = f"P5\n{ancho} {alto}\n255\n".encode("ascii")
    return cabecera + matriz.tobytes()


def tira_de_la_banda(
    imagen, banda, alto_destino=ALTO_DE_LA_TIRA_PX, ancho_maximo=ANCHO_DE_LA_TIRA_PX
):
    """De la pagina rasterizada y la banda guardada, a los bytes de la tira.

    Es el camino entero en una llamada, que es como lo usa la pantalla. Devuelve
    None en cuanto cualquier paso no tenga con que seguir, para que quien dibuja
    pueda poner en su hueco el rectangulo rayado que dice «aqui no hubo banda» en
    vez de una tira en blanco, que parece un escaneo vacio y no lo es.

    El tope de ancho viene puesto por defecto y no hay que acordarse de pasarlo:
    olvidarlo era el fallo, no la solucion.
    """
    return a_pgm(
        escalar_para_caber(recortar(imagen, banda), alto_destino, ancho_maximo)
    )
