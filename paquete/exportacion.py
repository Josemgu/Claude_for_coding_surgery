"""La carpeta que se le entrega a un companero: sus PDF y su Excel de trabajo.

**El PDF que se entrega lleva SOLO las hojas del caso, no el documento entero.**
Esta es la decision que mas importa de este modulo y no estaba escrita en ninguna
parte del pase. El motivo: desde que un escaneo de nueve hojas produce varios
casos (`DECISIONES.md`, 2026-09-02), `casos.ruta_pdf` apunta al documento COMPLETO
y no al formulario. Copiarlo tal cual le entregaria a un companero los MRN y los
nombres de personas de casos que no le tocan. Se recortan las hojas con `pypdf`,
que ya es dependencia del proyecto.

Que hojas son: la del caso (`casos.pagina_pdf`) mas la de cada una de sus personas
(`personas.pagina_pdf`), que en un formulario de grupo no son la misma.

⚠️ **Y cuando no se sabe que hojas son, NO se entrega el documento entero.** Un
caso guardado antes de la version 3 del esquema no tiene `pagina_pdf`: entonces se
deja fuera su PDF, se anota el motivo y el companero recibe su fila en el Excel
igual. Entregar de mas por no saber es exactamente el fallo que este recorte
existe para evitar.

Sin `pandas` (regla permanente 3) y sin red (regla permanente 2): esto escribe
archivos en una carpeta que Miguel elige.
"""

import re
import shutil
from collections import namedtuple
from pathlib import Path

from datos.asignaciones import casos_asignados
from datos.companeros import leer_companero
from datos.esquema import marca_de_tiempo
from datos.ordenanzas import resumir as resumir_ordenanzas
from datos.registro import PAQUETE, Cronometro
from datos.repositorio import leer_personas_del_caso
from datos.validacion import ErrorDeValidacion
from paquete.columnas import armar_la_clave
from paquete.trabajo import construir_libro_de_trabajo

# Como se llama el archivo que abre el companero. Lleva el nombre de la hoja que
# hay dentro —«Por verificar»— y no «trabajo», que es lo que decia antes: lo que ve
# el companero en su correo es el nombre del archivo, y «trabajo.xlsx» no dice de
# que trabajo se trata cuando tiene tres en la carpeta de descargas.
NOMBRE_DEL_EXCEL_DE_TRABAJO = "por_verificar.xlsx"
NOMBRE_DE_LA_CARPETA_DE_PDF = "formularios"

# Lo que se le quita al nombre de un companero para poder usarlo como nombre de
# carpeta. Windows rechaza estos nueve caracteres; el resto se deja tal cual,
# tildes incluidas, porque el sistema de archivos las admite y el companero tiene
# que reconocer su carpeta.
_CARACTERES_QUE_WINDOWS_NO_ADMITE = re.compile(r'[<>:"/\\|?*]')

ResultadoDelPaquete = namedtuple(
    "ResultadoDelPaquete",
    ("carpeta", "ruta_del_excel", "casos", "pdf_escritos", "avisos"),
)


class ErrorDeExportacion(RuntimeError):
    """El paquete no se pudo escribir en la carpeta que se le dio."""


def nombre_de_carpeta_para(nombre_del_companero, marca=None):
    """El nombre de la carpeta del paquete, sin caracteres que Windows rechace.

    Lleva la fecha y la hora para que dos paquetes del mismo companero no se pisen:
    el segundo envio de la semana no puede sobrescribir el primero, que puede estar
    todavia sin devolver.
    """
    marca = marca or marca_de_tiempo()
    limpio = _CARACTERES_QUE_WINDOWS_NO_ADMITE.sub("_", nombre_del_companero).strip()
    limpio = limpio.replace(" ", "_") or "companero"
    sello = marca.replace(":", "").replace("-", "").replace(" ", "_")
    return f"Paquete_{limpio}_{sello}"


def _paginas_del_caso(conexion, caso):
    """Las hojas del PDF que pertenecen a ese caso, ordenadas y sin repetir.

    Devuelve `()` cuando no se sabe ninguna. Ese vacio no significa «todas»: lo
    lee `_escribir_el_pdf_del_caso` como «no entregar este PDF», que es el lado
    seguro del error.
    """
    paginas = set()
    if caso.get("pagina_pdf"):
        paginas.add(caso["pagina_pdf"])
    for persona in leer_personas_del_caso(conexion, caso["id"]):
        if persona.get("pagina_pdf"):
            paginas.add(persona["pagina_pdf"])
    return tuple(sorted(paginas))


def _recortar_las_paginas(ruta_origen, paginas, ruta_destino):
    """Escribe un PDF nuevo con solo esas hojas. Devuelve cuantas escribio.

    `paginas` viene en base 1 —«pagina 2 de 6», que es como lo cuentan las personas
    y el visor del sistema— y `pypdf` indexa desde 0: la resta se hace aqui, en un
    solo sitio.

    Una hoja que no existe en el documento se salta con su aviso en vez de tumbar
    el paquete entero: si el PDF se recorto por fuera despues de importarlo, el
    resto de las hojas sigue siendo util.
    """
    from pypdf import PdfReader, PdfWriter

    lector = PdfReader(str(ruta_origen))
    escritor = PdfWriter()
    avisos = []
    for pagina in paginas:
        if 1 <= pagina <= len(lector.pages):
            escritor.add_page(lector.pages[pagina - 1])
        else:
            avisos.append(
                f"AVISO: el caso pedía la página {pagina} de «{ruta_origen.name}», "
                f"que solo tiene {len(lector.pages)}. Esa hoja no va en el paquete."
            )
    if not escritor.pages:
        return 0, avisos
    with open(ruta_destino, "wb") as archivo:
        escritor.write(archivo)
    return len(escritor.pages), avisos


def _nombre_del_pdf(caso):
    """Como se llama el PDF recortado de un caso dentro del paquete.

    ⚠️ **Lleva el id del caso pegado desde el 2026-09-03, y no es adorno.** Hasta
    ese dia se llamaba `NUMERO.pdf` a secas, y eso valia mientras `numero_caso` era
    `UNIQUE`. Desde la version 12 no lo es —dos familias de la misma unidad y el
    mismo mes comparten numero por construccion— y dos casos del mismo paquete
    escribirian el mismo archivo: el segundo pisaria al primero y el companero
    recibiria DOS veces el formulario de una familia y NINGUNA vez el de la otra,
    sin que nada avisara.

    Un caso sin numero sale como `caso-12.pdf` y no como `None.pdf`: el numero no
    se inventa (regla permanente 1) y `None` no es una etiqueta en espanol.
    """
    numero = caso.get("numero_caso")
    return f"{numero}-{caso['id']}.pdf" if numero else f"caso-{caso['id']}.pdf"


def _escribir_el_pdf_del_caso(conexion, caso, carpeta_de_pdf):
    """El PDF recortado de un caso. Devuelve (ruta o None, avisos)."""
    ruta_pdf = caso.get("ruta_pdf")
    if not ruta_pdf:
        return None, (
            f"AVISO: el caso {caso['numero_caso']} no tiene PDF guardado, así que "
            "va en el Excel pero sin su formulario.",
        )
    origen = Path(ruta_pdf)
    if not origen.is_file():
        return None, (
            f"AVISO: el PDF del caso {caso['numero_caso']} ya no está en «{origen}». "
            "El caso va en el Excel pero sin su formulario.",
        )

    paginas = _paginas_del_caso(conexion, caso)
    if not paginas:
        return None, (
            f"AVISO: del caso {caso['numero_caso']} no se sabe qué hojas del PDF le "
            "corresponden (se importó antes de que eso se guardara). NO se entrega "
            "el documento entero para no enseñar casos ajenos: el caso va en el "
            "Excel sin su formulario.",
        )

    destino = carpeta_de_pdf / _nombre_del_pdf(caso)
    try:
        escritas, avisos = _recortar_las_paginas(origen, paginas, destino)
    except Exception as causa:
        return None, (
            f"AVISO: no se pudo recortar el PDF del caso {caso['numero_caso']} "
            f"({causa}). El caso va en el Excel sin su formulario.",
        )
    if not escritas:
        return None, tuple(avisos)
    return destino, tuple(avisos)


def _unidad_con_su_numero(caso):
    """El barrio con su numero entre parentesis, o lo que haya de los dos.

    Si el numero ya esta dentro del nombre no se vuelve a pegar. Es el mismo cuidado
    que tiene el proyecto viejo, y alli nacio de un caso real: en unos escaneos el
    nombre sale ya con el numero y en otros suelto, y pegarlo siempre dejaba
    «Cuatricentenaria (7000014) (7000014)», que no es lo que dice el papel.
    """
    nombre = caso.get("unidad_nombre") or ""
    numero = caso.get("unidad_numero") or ""
    if not numero or numero in nombre:
        return nombre or numero or None
    return f"{nombre} ({numero})".strip() if nombre else numero


def filas_de_trabajo(conexion, casos):
    """Una fila por persona de esos casos, con lo que la hoja «Por verificar» pide.

    Los seis pasos y la llamada al lider salen VACIOS aunque la persona ya traiga
    una propuesta de una ronda anterior. Es a proposito: lo que se le manda a un
    companero es lo que tiene que mirar, no lo que otro contesto. Rellenarlo de
    antemano invita a confirmarlo sin comprobarlo, que es la averia contra la que
    existe la regla permanente 5.

    ⚠️ **`fecha_solicitud` y `estaca` salen a nulo siempre.** La base no las guarda:
    el dueno dijo «solo debe leer los campos que yo necesito» y esos dos no estan en
    su lista (`DECISIONES.md`, 2026-09-03). Las columnas se escriben igual, porque la
    hoja es la que el aprobo, y `paquete/trabajo.py` las rellena con «no consta» en
    vez de dejarlas en blanco. **Aqui no se inventan** (regla permanente 1).
    """
    filas = []
    for caso in casos:
        for persona in leer_personas_del_caso(conexion, caso["id"]):
            filas.append(
                {
                    "numero_caso": caso["numero_caso"],
                    "fecha_solicitud": None,
                    "fecha_viaje": caso.get("fecha_viaje"),
                    # El templo va en la fila para que `paquete/trabajo.py` lo pinte
                    # en la cabecera A2 —«Templo: … · Sale el …»—, que es como lo
                    # hace el proyecto viejo. Ese modulo ya lo leia de las filas
                    # desde que se escribio; lo que faltaba era que alguien lo
                    # pusiera, porque la base no lo guardaba hasta la version 10.
                    # Sale a nulo en los casos importados antes de esa version, y la
                    # cabecera se queda sin esa mitad en vez de inventarla.
                    "templo": caso.get("templo_nombre"),
                    "unidad_nombre": _unidad_con_su_numero(caso),
                    "estaca": None,
                    "nombre": persona["nombre"],
                    "mrn": persona["mrn"],
                    "a_que_va": resumir_ordenanzas(persona),
                    # El id del caso va DENTRO de la clave desde el 2026-09-03. Es
                    # lo unico que no se repite: dos casos pueden llevar el mismo
                    # numero y las mismas personas —numero repetido y documento
                    # duplicado— y sin el id la fila que vuelve no sabe a cual es.
                    "clave": armar_la_clave(
                        caso["numero_caso"], persona["mrn"], caso["id"]
                    ),
                }
            )
    return filas


SIN_NOMBRE = "— sin nombre leído —"

# Lo que se le dice a Miguel cuando en el paquete va alguien sin cedula de miembro.
# La frase nombra a cada uno, y eso es la mitad de lo que sirve: «hay 1 persona sin
# MRN» obliga a abrir el Excel a buscarla, y entonces no se busca.
AVISO_DE_QUIEN_NO_TIENE_MRN = (
    "AVISO: {cuantas} persona{plural} de este paquete {verbo} SIN cédula de miembro "
    "(MRN): {nombres}. El compañero puede contestar por {pronombre}, pero al volver "
    "el Excel esas filas NO se pueden emparejar —la vuelta casa por «número de caso "
    "+ MRN» y NUNCA por nombre— y se descartan enteras: se pierden los seis pasos "
    "que el compañero haya contestado. {sujeto} no podrá{plural_verbo} recibir "
    "respuesta hasta que se le{plural} teclee la cédula en la pantalla de corrección "
    "y se vuelva a generar el paquete."
)


def _avisos_de_quien_no_tiene_mrn(filas):
    """Nombra a quien va en el paquete sin MRN, o nada si no hay nadie.

    ⚠️ **Es el hallazgo ALTO de la auditoría final de QA**, medido con una ida y
    vuelta de seis perfiles: los cinco con MRN volvieron con 7/7 pasos y la que no
    lo tiene volvió con **0/7**. Y no es un caso raro: el PDF real del dueño trae
    **1 de 4** así. El compañero hace el trabajo, lo devuelve, y al reconciliar se
    descarta — hasta hoy sin que nadie lo hubiera avisado al generar, que es el
    único momento en que todavía se puede arreglar sin gastar el trabajo de nadie.

    Lo que NO se hace aquí, y es del dueño: teclear los pasos a mano al volver. Eso
    exige decidir quién firma esa corrección y nadie lo ha decidido.
    """
    sin_mrn = [fila["nombre"] or SIN_NOMBRE for fila in filas if not fila.get("mrn")]
    if not sin_mrn:
        return ()
    varias = len(sin_mrn) != 1
    return (
        AVISO_DE_QUIEN_NO_TIENE_MRN.format(
            cuantas=len(sin_mrn),
            plural="s" if varias else "",
            verbo="van" if varias else "va",
            nombres=", ".join(sin_mrn),
            pronombre="ellas" if varias else "ella",
            sujeto="Esas personas" if varias else "Esa persona",
            plural_verbo="n" if varias else "",
        ),
    )


def exportar_paquete(conexion, companero_id, carpeta_destino, casos=None):
    """Escribe la carpeta del companero y devuelve que quedo dentro.

    `casos` permite entregar solo algunos de los que tiene asignados; si no se
    pasa, se entregan todos los vivos. La lista se pide a `datos/asignaciones.py` y
    no se compone aqui: el paquete refleja lo asignado, y si los dos pudieran
    separarse habria dos verdades sobre quien lleva que.
    """
    companero = leer_companero(conexion, companero_id)
    if companero is None:
        raise ErrorDeValidacion(f"No hay ningún compañero con el id {companero_id!r}.")

    casos = list(casos) if casos is not None else casos_asignados(conexion, companero_id)
    # El cronómetro envuelve solo lo que escribe archivos, que es lo que tarda, y
    # deja su línea **también si algo levanta**: lo que tarda en fallar es lo que
    # hace falta cuando el dueño dice que se quedó colgado. En el registro van
    # cuántos casos y cuántos segundos; ni un nombre, ni el del compañero.
    with Cronometro(PAQUETE, casos=len(casos)):
        return _escribir_el_paquete(conexion, companero, casos, carpeta_destino)


def _escribir_el_paquete(conexion, companero, casos, carpeta_destino):
    """Escribe la carpeta, los PDF recortados y el Excel. Es la parte que tarda."""
    if not casos:
        raise ErrorDeValidacion(
            f"El compañero «{companero['nombre']}» no tiene ningún caso asignado: "
            "no hay paquete que generar. Asígnele casos antes."
        )

    carpeta = Path(carpeta_destino) / nombre_de_carpeta_para(companero["nombre"])
    carpeta_de_pdf = carpeta / NOMBRE_DE_LA_CARPETA_DE_PDF
    try:
        carpeta_de_pdf.mkdir(parents=True, exist_ok=True)
    except OSError as causa:
        raise ErrorDeExportacion(
            f"No se pudo crear la carpeta del paquete «{carpeta}»: {causa}. "
            "Elija otra carpeta de destino."
        ) from causa

    avisos = []
    pdf_escritos = []
    for caso in casos:
        ruta, avisos_del_caso = _escribir_el_pdf_del_caso(conexion, caso, carpeta_de_pdf)
        avisos.extend(avisos_del_caso)
        if ruta is not None:
            pdf_escritos.append(ruta)

    ruta_del_excel = carpeta / NOMBRE_DEL_EXCEL_DE_TRABAJO
    filas = filas_de_trabajo(conexion, casos)
    avisos.extend(_avisos_de_quien_no_tiene_mrn(filas))
    libro = construir_libro_de_trabajo(filas, agente=companero["nombre"])
    try:
        libro.save(str(ruta_del_excel))
    except PermissionError as causa:
        raise ErrorDeExportacion(
            f"No se pudo escribir «{ruta_del_excel}» porque otro programa lo tiene "
            f"abierto (casi siempre es el propio Excel). El sistema dijo: {causa}"
        ) from causa

    if not pdf_escritos:
        avisos.append(
            "AVISO: el paquete salió SIN ningún PDF. El compañero recibe el Excel "
            "con sus filas, pero no tiene contra qué comprobarlas."
        )
    return ResultadoDelPaquete(
        carpeta, ruta_del_excel, tuple(casos), tuple(pdf_escritos), tuple(avisos)
    )


def borrar_carpeta_a_medias(carpeta):
    """Quita una carpeta de paquete que quedo incompleta. Solo para deshacer un fallo.

    No borra datos de nadie: una carpeta de paquete es una COPIA que se acaba de
    escribir, y la base y los PDF de origen no se tocan. Existe para que un fallo a
    mitad no deje media entrega en el escritorio de Miguel, donde alguien la
    mandaria creyendo que esta completa.
    """
    shutil.rmtree(Path(carpeta), ignore_errors=True)
