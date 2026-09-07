"""De una pagina de PDF a un formulario extraido, con la procedencia de cada campo.

Este modulo solo coordina: cada paso vive en su modulo y aqui se ponen en orden.
No guarda nada en la base y no dibuja nada en pantalla.

Un PDF puede traer VARIOS formularios, uno por pagina —medido sobre un documento
real de seis paginas—, asi que la unidad de trabajo es la PAGINA y no el archivo.
El nombre del archivo no identifica un caso.
"""

import time
from collections import namedtuple

from extraccion.anotaciones import (
    CLASE_TACHON,
    TIPO_TEXTO,
    TIPO_TRAZO,
    contar_por_clase,
    leer_anotaciones,
)
from extraccion.bandas import banda_de_valor, esta_en_la_banda, localizar_ancla, lineas_en_la_banda
from extraccion.campos import campo_vacio, resolver_campo
from extraccion.casillas import casillas_no_leidas
from extraccion.etiquetas import (
    CAMPO_DE_LA_FECHA_DE_VIAJE,
    CAMPO_DE_LA_UNIDAD,
    CAMPO_DE_LOS_NOMBRES,
    CAMPO_DEL_MRN,
    CAMPO_DEL_NOMBRE_DEL_TEMPLO,
    CAMPOS_DEL_FORMULARIO,
    CAMPOS_QUE_CIERRAN_LAS_PERSONAS,
    formas_de,
)
from extraccion.geometria import rectangulo_pdf_a_pixeles
from extraccion.normalizacion import (
    normalizar_fecha,
    normalizar_nombre_de_unidad,
    normalizar_nombre_del_templo,
    normalizar_numero_caso,
    normalizar_numero_de_unidad,
)
from extraccion.ocr import leer_lineas
from extraccion.recorte import banda_en_fracciones
from extraccion.personas import FILAS_DEL_FORMULARIO, extraer_personas
from extraccion.rasterizado import rasterizar_pagina

# Que campos compiten entre si al buscar un ancla: todos contra todos. Cada uno
# aporta sus DOS formas —la inglesa y la espanola—, asi que la lista de rivales
# de un campo son las 16 cadenas de los otros ocho.
#
# «Date traveling home from the temple» esta aqui por una razon medida: en una
# pagina real el OCR la leyo «Date traveling home fror the temple», que se parece
# un 0.77 a la etiqueta de IDA — la fecha que decide si un caso sale avisado en
# el calendario. En espanol el riesgo es el mismo o mayor: «Fecha de regreso del
# templo» se parece un 0.704 a «Fecha de viaje al templo», y «Fecha de la cita
# del templo» un 0.667.
#
# Con el umbral en 0.85 esos casos ya se rechazan por parecido. La lista de
# rivales es la SEGUNDA defensa, y la que no depende de un numero que alguien
# pueda aflojar. `pruebas/prueba_bandas.py` separa las dos y comprueba cada una,
# y `pruebas/prueba_etiquetas_bilingues.py` repite la comprobacion en espanol.

# Umbrales de `DECISIONES.md` para decidir que una pagina esta manuscrita o
# ilegible: si mas del 60% de los campos vuelven por debajo de 0.6 de confianza,
# no se insiste. El caso se marca y se abre a mano con los campos VACIOS.
CONFIANZA_QUE_SE_CONSIDERA_BAJA = 0.6
PROPORCION_DE_CAMPOS_FLOJOS_QUE_MARCA_CAPTURA_MANUAL = 0.6

FormularioExtraido = namedtuple(
    "FormularioExtraido",
    (
        "ruta_pdf",
        "indice_de_pagina",
        "numero_caso",
        "fecha_viaje",
        "unidad_numero",
        "unidad_nombre",
        # `bandas` es `{nombre_de_campo: (x0, y0, x1, y1)}` en FRACCIONES de la
        # pagina, no en pixeles: el motivo esta en `extraccion/recorte.py`. Un
        # campo cuyo ancla no se encontro tiene None, y eso es un dato — quiere
        # decir «de este no hay trozo de escaneo que ensenar».
        "bandas",
        "personas",
        "bandas_por_persona",
        "casillas_por_persona",
        "filas_vacias",
        "filas_descartadas",
        "captura_manual",
        "anclas_no_encontradas",
        "resumen_de_anotaciones",
        "tamano_de_la_imagen",
        "segundos",
        # Las dos ultimas son de DIAGNOSTICO y por eso van al final con valor por
        # defecto: nada de lo que ya construia un `FormularioExtraido` —las
        # pruebas incluidas— tiene que cambiar para seguir funcionando.
        #
        # Existen porque «no se pudo leer el número de caso» no dice nada util. Sin
        # estas dos no hay forma de distinguir las tres averias que producen ese
        # mismo mensaje, y son averias distintas con arreglos distintos:
        #
        #   - el OCR no devolvio NI UNA linea  -> el escaneo o la rasterizacion;
        #   - devolvio muchas y ninguna encaja -> el papel o las reglas de lectura;
        #   - devolvio el numero y se descarto -> el guardado.
        #
        # `texto_leido` es lo que la maquina leyo en la pagina, tal cual y sin
        # corregir. No se guarda en `casos`: viaja al renglon de
        # `documentos_ilegibles`, recortado, para poder mirar por que fallo.
        "lineas_leidas",
        "texto_leido",
        # `templo_nombre` va detras de las dos de diagnostico y con valor por
        # defecto por lo mismo que ellas: nada de lo que ya construia un
        # `FormularioExtraido` —las pruebas incluidas— tiene que cambiar. Entro el
        # 2026-09-03 y va a `casos.templo_nombre` (version 10 del esquema).
        # Su valor por defecto es un campo VACIO y no `None`: quien lo lea hace
        # `formulario.templo_nombre.valor` como con los otros cuatro, y un None
        # suelto ahi levantaria `AttributeError` en el guardado en vez de
        # significar «de esta pagina no se leyo el templo», que es lo que quiere
        # decir.
        "templo_nombre",
    ),
    defaults=(0, None, campo_vacio()),
)


def _bandas_en_fracciones(bandas_en_pixeles, ancho_px, alto_px):
    """Las bandas del caso, ya en fracciones de la pagina y listas para guardar."""
    return {
        campo: banda_en_fracciones(rectangulo, ancho_px, alto_px)
        for campo, rectangulo in bandas_en_pixeles.items()
    }


def _rivales_de(campo):
    """Las formas de los demas campos del formulario, para desambiguar el ancla."""
    return tuple(formas_de(otro) for otro in CAMPOS_DEL_FORMULARIO if otro != campo)


def _anotaciones_en_pixeles(anotaciones, alto_puntos, escala):
    """Las anotaciones con su rectangulo ya en pixeles, listas para comparar."""
    return [
        (anotacion, rectangulo_pdf_a_pixeles(anotacion.rectangulo_pdf, alto_puntos, escala))
        for anotacion in anotaciones
    ]


def _campo_de_la_banda(lineas, anotaciones_en_px, ancla, normalizar):
    """Aplica la precedencia sobre la banda que cuelga de un ancla."""
    if ancla is None:
        return campo_vacio(), None
    banda = banda_de_valor(ancla.rectangulo)
    correcciones = [
        anotacion
        for anotacion, rectangulo in anotaciones_en_px
        if anotacion.tipo == TIPO_TEXTO and esta_en_la_banda(rectangulo, banda)
    ]
    hay_tachon = any(
        anotacion.tipo == TIPO_TRAZO
        and anotacion.clase == CLASE_TACHON
        and esta_en_la_banda(rectangulo, banda)
        for anotacion, rectangulo in anotaciones_en_px
    )
    return resolver_campo(lineas_en_la_banda(lineas, banda), correcciones, hay_tachon, normalizar), banda


def _numero_de_caso(lineas, anotaciones):
    """El numero de caso, que en estos formularios va siempre en una anotacion.

    Se busca primero en las anotaciones porque ahi el texto es exacto, y solo si
    no aparece se recurre al OCR. No lleva banda: el patron —4 letras mayusculas
    y 4 digitos— es especifico de sobra para encontrarlo en toda la pagina.
    """
    for anotacion in anotaciones:
        if anotacion.tipo != TIPO_TEXTO:
            continue
        valor = normalizar_numero_caso(anotacion.texto)
        if valor is not None:
            return resolver_campo([], [anotacion], hay_tachon=False, normalizar=normalizar_numero_caso)
    for linea in lineas:
        if normalizar_numero_caso(linea.texto) is not None:
            return resolver_campo([linea], [], hay_tachon=False, normalizar=normalizar_numero_caso)
    return campo_vacio()


def _texto_de_la_pagina(lineas):
    """Lo que el OCR leyo en la pagina entera, junto y en el orden en que salio.

    Es para diagnosticar, no para extraer: ningun campo se saca de aqui. Se junta
    con espacios y no con saltos de linea porque va a caber en una celda y en un
    renglon de una lista, no en un editor.
    """
    partes = [linea.texto.strip() for linea in lineas if linea.texto and linea.texto.strip()]
    return " ".join(partes) or None


def _es_captura_manual(campos, personas):
    """Cierto cuando la pagina esta demasiado floja para fiarse de ella.

    Cuenta como flojo tanto un campo que volvio con confianza baja como uno que
    no se pudo leer: los dos significan lo mismo para quien tiene que corregir.
    """
    confianzas = [campo.confianza for campo in campos]
    for persona in personas:
        confianzas.extend((persona.nombre.confianza, persona.mrn.confianza))
    if not confianzas:
        return True
    flojos = sum(
        1 for confianza in confianzas if confianza is None or confianza < CONFIANZA_QUE_SE_CONSIDERA_BAJA
    )
    return flojos / len(confianzas) > PROPORCION_DE_CAMPOS_FLOJOS_QUE_MARCA_CAPTURA_MANUAL


def _vaciar(campo):
    """Deja el campo sin valor pero conservando lo que el OCR habia leido."""
    return campo_vacio(valor_ocr=campo.valor_ocr, anulado_por_tachon=campo.anulado_por_tachon)


def localizar_las_anclas(lineas):
    """Cada campo que el extractor necesita, con la linea del OCR que lo rotula.

    Se buscan TODOS los campos del formulario y no solo los que se extraen: los
    que no se extraen —la estaca, la fecha de regreso— hacen falta igual como
    rivales, y saber si aparecieron es lo que permite decir por que fallo una
    pagina. La clave del diccionario es el nombre del campo, no la cadena
    impresa, porque ahora hay dos cadenas impresas para cada cosa.
    """
    return {
        campo: localizar_ancla(lineas, formas_de(campo), _rivales_de(campo))
        for campo in CAMPOS_DEL_FORMULARIO
    }


def _rectangulo_de(anclas, campo):
    """El rectangulo del ancla, o None si ese campo no se encontro."""
    ancla = anclas.get(campo)
    return None if ancla is None else ancla.rectangulo


CamposDelCaso = namedtuple(
    "CamposDelCaso",
    (
        "numero_caso", "fecha_viaje", "unidad_numero", "unidad_nombre", "bandas",
        # `templo_nombre` va DETRAS de `bandas` y con valor por defecto —aunque en
        # el papel esta antes— para que nada de lo que ya construia un
        # `CamposDelCaso` por posicion tenga que cambiar. El orden de un namedtuple
        # es de construccion, no de lectura.
        #
        # Entro el 2026-09-03: el templo esta impreso en el papel («Nombre del
        # templo» / «Temple Name») y su ancla ya se localizaba —hace de cierre del
        # bloque de personas—, pero lo que hubiera debajo se tiraba. La cabecera
        # del Excel del agente lo pide y el informe del proyecto viejo lo tiene.
        "templo_nombre",
    ),
    defaults=(campo_vacio(),),
)


def campos_del_caso(lineas, anotaciones, anotaciones_en_px, anclas):
    """Los cinco campos que son del caso, y DONDE estaba cada uno en el escaneo.

    El numero y el nombre de la unidad se resuelven por separado, cada uno con su
    normalizador. Juntos no se puede: en un formulario real hay una anotacion que
    corrige el NOMBRE y no dice nada del numero, y tratandola como correccion del
    conjunto se perdia el numero que el OCR habia leido bien.

    Las bandas se devuelven y ya no se tiran. Antes las tres llamadas hacian
    `campo, _ = ...` y el rectangulo se perdia ahi mismo: `_campo_de_la_banda`
    siempre lo calculo, pero no llegaba a ninguna parte. Sin el no hay tira del
    escaneo al lado del campo, que es la mitad de la pantalla de correccion.

    `numero_caso` no lleva banda a proposito: no se busca por ancla sino por
    patron en toda la pagina, asi que no hay una fila del papel que ensenar.
    Su hueco se dibuja con el rectangulo rayado que dice que no la hay.
    """
    fecha_viaje, banda_de_la_fecha = _campo_de_la_banda(
        lineas, anotaciones_en_px, anclas[CAMPO_DE_LA_FECHA_DE_VIAJE], normalizar_fecha
    )
    unidad_numero, banda_de_la_unidad = _campo_de_la_banda(
        lineas, anotaciones_en_px, anclas[CAMPO_DE_LA_UNIDAD], normalizar_numero_de_unidad
    )
    unidad_nombre, _ = _campo_de_la_banda(
        lineas, anotaciones_en_px, anclas[CAMPO_DE_LA_UNIDAD], normalizar_nombre_de_unidad
    )
    # El templo se resuelve como los demas: el ancla, su banda, y la precedencia de
    # tachones y correcciones. Su normalizador NO valida contra ningun catalogo
    # —solo junta espacios—: la lista de templos es del dueno y no existe todavia,
    # y elegir «el mas parecido» de una lista inventada seria inventar un dato.
    templo_nombre, banda_del_templo = _campo_de_la_banda(
        lineas,
        anotaciones_en_px,
        anclas[CAMPO_DEL_NOMBRE_DEL_TEMPLO],
        normalizar_nombre_del_templo,
    )
    return CamposDelCaso(
        numero_caso=_numero_de_caso(lineas, anotaciones),
        fecha_viaje=fecha_viaje,
        unidad_numero=unidad_numero,
        unidad_nombre=unidad_nombre,
        # El nombre y el numero de la unidad salen de la MISMA banda del papel:
        # «Ward/Branch Name and Unit Number» es una sola etiqueta con las dos
        # cosas debajo. Por eso las dos entradas apuntan al mismo rectangulo.
        bandas={
            "fecha_viaje": banda_de_la_fecha,
            "unidad_numero": banda_de_la_unidad,
            "unidad_nombre": banda_de_la_unidad,
            "templo_nombre": banda_del_templo,
        },
        templo_nombre=templo_nombre,
    )


def _se_leyo_con_confianza(campo):
    """Cierto cuando el campo trae valor y su confianza llega al minimo."""
    return (
        campo.valor is not None
        and campo.confianza is not None
        and campo.confianza >= CONFIANZA_QUE_SE_CONSIDERA_BAJA
    )


def vaciar_el_formulario(campos):
    """Todo a vacio menos el numero de caso, conservando lo que el OCR leyo.

    Regla permanente 1 y `DECISIONES.md`: sobre un formulario ilegible no se
    insiste. Se entrega VACIO para que Miguel lo capture mirando la imagen, y no
    medio relleno con lecturas en las que nadie confia.

    **El numero de caso es la excepcion, y esta razonada.** Los demas campos
    salen de una BANDA que cuelga de un ancla: si la pagina se declaro ilegible
    es porque las anclas no se localizaron bien, asi que lo que haya en esas
    bandas es sospechoso por construccion. El numero de caso no: sale de un
    PATRON —cuatro letras mayusculas y cuatro digitos— buscado en la pagina
    entera, sin depender de ninguna etiqueta. Ese patron no aparece por
    casualidad en el ruido de un escaneo malo.

    Vaciarlo tambien no protegia de nada y costaba caro: es lo que el dueno vio
    como «se guardaron 0 casos». El OCR leia el numero con confianza 1.00, la
    pagina se marcaba ilegible porque las demas etiquetas estaban en un idioma
    que el programa no conocia, y el vaciado se llevaba por delante el unico dato
    que permite archivar la pagina en un caso. Sin numero, la pagina no se
    guarda en ninguna parte y desaparece sin que nadie se entere.

    La excepcion NO es «el numero siempre se salva»: es «se salva si es de
    fiar». Un numero leido por debajo del minimo de confianza se vacia como el
    resto, y su lectura viaja en `valor_ocr` para que se vea que se intento.

    Las BANDAS se conservan enteras, y eso no es una excepcion a la regla: la
    banda no es un dato leido, es donde hay que mirar. En un formulario ilegible
    es justo lo que mas falta hace — la pantalla ensena los campos vacios con la
    tira del escaneo al lado para poder teclearlos mirandola.
    """
    numero_de_fiar = _se_leyo_con_confianza(campos.numero_caso)
    return CamposDelCaso(
        numero_caso=campos.numero_caso if numero_de_fiar else _vaciar(campos.numero_caso),
        fecha_viaje=_vaciar(campos.fecha_viaje),
        unidad_numero=_vaciar(campos.unidad_numero),
        unidad_nombre=_vaciar(campos.unidad_nombre),
        bandas=campos.bandas,
        templo_nombre=_vaciar(campos.templo_nombre),
    )


def extraer_pagina(ruta_pdf, indice_de_pagina, pagina_pypdf, motor_ocr):
    """Lee una pagina entera y devuelve el formulario con toda su procedencia.

    Son 44 lineas y no se parte mas: 24 de ellas son la construccion del
    `FormularioExtraido`, que es UNA sentencia con quince campos nombrados.
    Partirla obligaria a devolver el resultado a medio montar desde otra funcion,
    que es peor. El trabajo de verdad —anclas, campos, personas, decision de
    captura manual— ya esta cada uno en su funcion, y aqui solo se llaman.
    """
    comenzo = time.perf_counter()
    rasterizada = rasterizar_pagina(ruta_pdf, indice_de_pagina)
    lineas = leer_lineas(motor_ocr, rasterizada.imagen)
    anotaciones = leer_anotaciones(pagina_pypdf)
    anotaciones_en_px = _anotaciones_en_pixeles(
        anotaciones, rasterizada.alto_puntos, rasterizada.escala
    )

    anclas = localizar_las_anclas(lineas)
    campos = campos_del_caso(lineas, anotaciones, anotaciones_en_px, anclas)
    personas, filas_descartadas = extraer_personas(
        lineas,
        _rectangulo_de(anclas, CAMPO_DE_LOS_NOMBRES),
        _rectangulo_de(anclas, CAMPO_DEL_MRN),
        tuple(_rectangulo_de(anclas, campo) for campo in CAMPOS_QUE_CIERRAN_LAS_PERSONAS),
    )

    captura_manual = _es_captura_manual(
        [campos.numero_caso, campos.fecha_viaje, campos.unidad_numero], personas
    )
    if captura_manual:
        campos = vaciar_el_formulario(campos)
        personas = []

    ancho_px, alto_px = rasterizada.imagen.shape[1], rasterizada.imagen.shape[0]
    return FormularioExtraido(
        ruta_pdf=str(ruta_pdf),
        indice_de_pagina=indice_de_pagina,
        numero_caso=campos.numero_caso,
        fecha_viaje=campos.fecha_viaje,
        unidad_numero=campos.unidad_numero,
        unidad_nombre=campos.unidad_nombre,
        bandas=_bandas_en_fracciones(campos.bandas, ancho_px, alto_px),
        personas=personas,
        bandas_por_persona=[
            banda_en_fracciones(persona.banda, ancho_px, alto_px) for persona in personas
        ],
        casillas_por_persona=[casillas_no_leidas() for _ in personas],
        filas_vacias=max(0, FILAS_DEL_FORMULARIO - len(personas)),
        filas_descartadas=filas_descartadas,
        captura_manual=captura_manual,
        # Los NOMBRES de los campos que no se localizaron, en espanol: esta
        # tupla se le ensena a Miguel tal cual, en el aviso que explica por que
        # fallo la pagina. Antes llevaba las cadenas inglesas del papel, que en
        # un formulario espanol no le dicen nada a nadie.
        anclas_no_encontradas=tuple(campo for campo, ancla in anclas.items() if ancla is None),
        resumen_de_anotaciones=contar_por_clase(anotaciones),
        tamano_de_la_imagen=(ancho_px, alto_px),
        segundos=time.perf_counter() - comenzo,
        lineas_leidas=len(lineas),
        texto_leido=_texto_de_la_pagina(lineas),
        templo_nombre=campos.templo_nombre,
    )


def extraer_documento(ruta_pdf, motor_ocr):
    """Todas las paginas del PDF, cada una como un formulario propio."""
    from pypdf import PdfReader

    lector = PdfReader(str(ruta_pdf))
    return [
        extraer_pagina(ruta_pdf, indice, pagina, motor_ocr)
        for indice, pagina in enumerate(lector.pages)
    ]
