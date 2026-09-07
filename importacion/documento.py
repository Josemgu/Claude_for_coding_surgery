"""Un PDF entero: se lee pagina a pagina y se guarda caso a caso.

La unidad de trabajo es la PAGINA y no el archivo. Esta medido sobre un documento
real de seis paginas: un PDF trae hasta seis formularios y el nombre del archivo
no identifica ningun caso.

**El resumen lleva SIEMPRE las dos cifras** —cuantos casos salieron de cuantas
paginas—. No es cosmetica: un PDF de seis hojas que produce un caso es un fallo, y
con una sola cifra no se ve. Con las dos, salta a la vista.

**El espejo se regenera una vez al final, no una vez por pagina.** Va por
`espejo.escritura.guardar_y_regenerar`, que es el punto unico por el que se
guarda: mientras se pase por ahi no existe ningun camino que deje la base al dia y
el Excel atrasado. Una vez por pagina reescribiria el `.xlsx` entero seis veces
para el mismo resultado.
"""

from collections import namedtuple

from espejo.escritura import guardar_y_regenerar
from importacion.guardado import guardar_las_paginas_del_documento

ResultadoDeImportacion = namedtuple(
    "ResultadoDeImportacion", ("ruta_pdf", "paginas", "paginas_por_pagina", "espejo")
)


def importar_documento(conexion, ruta_pdf, motor_ocr, carpeta_de_datos=None, avisar=print):
    """Lee el PDF entero, guarda lo que se pueda y regenera el espejo una vez.

    Devuelve el resultado de CADA pagina, tambien el de las que no entraron y con
    su motivo escrito. Quien lo llame decide como lo ensena; lo que no puede es no
    enterarse, porque una pagina perdida en silencio es un formulario que nadie
    va a atender.
    """
    from extraccion.formulario import extraer_documento

    formularios = extraer_documento(ruta_pdf, motor_ocr)
    guardado, espejo = guardar_y_regenerar(
        conexion,
        lambda: guardar_las_paginas_del_documento(conexion, formularios),
        carpeta_de_datos=carpeta_de_datos,
        avisar=avisar,
    )
    return ResultadoDeImportacion(
        ruta_pdf=str(ruta_pdf),
        paginas=len(formularios),
        paginas_por_pagina=guardado,
        espejo=espejo,
    )


def casos_importados(resultado):
    """Las paginas que si entraron."""
    return [pagina for pagina in resultado.paginas_por_pagina if pagina.importada]


def paginas_no_importadas(resultado):
    """Las paginas que no entraron, cada una con su motivo."""
    return [pagina for pagina in resultado.paginas_por_pagina if not pagina.importada]


def casos_pendientes_de_identificar(resultado):
    """Las paginas que entraron SIN numero de caso, y que hay que identificar.

    Se cuentan aparte y no se suman al total a secas: una pagina guardada sin
    numero no esta terminada —no se puede cruzar con el Excel de los companeros
    hasta que alguien le teclee el numero—, y un resumen que dijera solo «13
    casos» esconderia justo las que exigen trabajo.
    """
    return [pagina for pagina in casos_importados(resultado) if pagina.pendiente_de_identificar]


def casos_duplicados(resultado):
    """Las paginas que entraron REPITIENDO a un caso que ya estaba.

    Se cuentan aparte por lo mismo que las pendientes de identificar: entraron —el
    dueno lo pidio asi, «si hay documentos duplicados debe decirlo y no
    rechazarlo»— pero no son trabajo terminado. Alguien tiene que mirar los dos y
    decidir, y **el programa no funde nada solo**. Un resumen que las sumara al
    total a secas escondería justo lo que hay que revisar.
    """
    return [pagina for pagina in casos_importados(resultado) if pagina.duplicado_de is not None]


def casos_distintos(resultado):
    """Cuantos CASOS salieron, que no es lo mismo que cuantas paginas entraron.

    Seis paginas de un formulario de grupo se unen en un caso: contarlas como
    seis diria «6 casos de 6 páginas» y esa frase es justo la que tiene que
    delatar cuando algo se pierde. Se cuentan identificadores distintos.
    """
    return len({pagina.caso_id for pagina in casos_importados(resultado)})


def personas_importadas(resultado):
    """Cuantas personas entraron en total, sumando las de todas las paginas."""
    return sum(pagina.personas for pagina in casos_importados(resultado))


def _concordar(cantidad, singular, plural):
    """«1 página» / «6 páginas». Es lo primero que Miguel lee al importar."""
    return f"{cantidad} {singular if cantidad == 1 else plural}"


def resumen_de_la_importacion(resultado):
    """La frase que se le ensena a Miguel cuando termina de importar.

    Lleva las dos cifras siempre, tambien cuando coinciden: «6 casos de 6 páginas»
    es lo que le dice que no se perdio nada, y esa confirmacion vale tanto como el
    aviso del caso contrario.

    Concuerda en numero y lleva sus tildes. No es cosmetica: un mensaje que dice
    «1 casos de 1 paginas» se lee como una plantilla a medio hacer, y lo que se
    duda despues es el numero, que es lo unico que hay que creerse.
    """
    importados = casos_importados(resultado)
    casos = casos_distintos(resultado)
    personas = personas_importadas(resultado)
    sin_identificar = len(casos_pendientes_de_identificar(resultado))
    duplicados = len(casos_duplicados(resultado))
    lineas = [
        f"{'Se guardó' if casos == 1 else 'Se guardaron'} "
        f"{_concordar(casos, 'caso', 'casos')} de "
        f"{_concordar(resultado.paginas, 'página', 'páginas')}, con "
        f"{_concordar(personas, 'persona', 'personas')} en total."
    ]
    if sin_identificar:
        # Va en su propio renglon y con la palabra delante. Pegada a la cifra
        # anterior se leeria como un matiz de «se guardaron 13 casos», y no lo es:
        # son las paginas que todavia exigen que alguien teclee algo.
        lineas.append(
            f"PENDIENTES DE IDENTIFICAR: {_concordar(sin_identificar, 'página', 'páginas')} "
            + ("entró" if sin_identificar == 1 else "entraron")
            + " sin número de caso. Se guardó todo lo demás que traían; el número "
            "hay que teclearlo a mano."
        )
    if duplicados:
        # En su propio renglon y con la palabra delante, por lo mismo que las
        # pendientes de identificar: pegado a la cifra anterior se leeria como un
        # matiz de «se guardaron 13 casos», y no lo es. Son los que hay que mirar.
        lineas.append(
            f"DUPLICADOS: {_concordar(duplicados, 'caso', 'casos')} "
            + ("repite" if duplicados == 1 else "repiten")
            + " a otro que ya estaba. Entró todo igual y nada se ha fundido solo: "
            "el caso que ya estaba NO se ha tocado. Abajo se dice de cuál es cada uno."
        )
    for pagina in paginas_no_importadas(resultado):
        lineas.append(f"Página {pagina.pagina_pdf}: {pagina.motivo}")
    for pagina in importados:
        # `numero_caso` puede ser None: es una pagina que entro sin identificar,
        # y escribir «Caso None» seria peor que no decir nada. Se nombra por su
        # pagina, que es lo unico que la identifica hasta que alguien la abra.
        cabeza = (
            f"Caso {pagina.numero_caso}, página {pagina.pagina_pdf}"
            if pagina.numero_caso is not None
            else f"Página {pagina.pagina_pdf} (sin número de caso)"
        )
        lineas.extend(f"{cabeza}: {aviso}" for aviso in pagina.avisos)
    return "\n".join(lineas)
