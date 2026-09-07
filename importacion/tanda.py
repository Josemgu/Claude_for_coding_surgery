"""Cientos de PDF de una vez: leer, guardar, contar, y que ninguno tumbe al resto.

El dueno lo dijo con un numero: **500 documentos de una vez**. Eso cambia tres
cosas respecto a importar uno, y este modulo existe por las tres.

**1. Un fallo no puede tumbar la tanda.** Con un PDF, que la excepcion suba a la
pantalla es razonable: se ve el error y se vuelve a intentar. Con 500, un archivo
corrupto en el puesto 12 tiraria 488 documentos que estaban bien —y de los 11
anteriores nadie sabria si quedaron guardados—. Aqui cada documento se lee dentro
de su propio `try`, el fallo se convierte en un renglon de `documentos_ilegibles`
con su motivo, y la tanda sigue en el siguiente.

**2. Leer y guardar estan separados a proposito.** `leer_un_pdf` no toca la base y
`guardar_un_pdf` no toca el disco. No es simetria: es lo que permite que la lectura
—que es lo lento, unos 8 s por pagina— corra en un hilo aparte mientras la ventana
sigue respondiendo, y que TODO lo que escribe en SQLite se quede en el hilo que
abrio la conexion. El modulo `sqlite3` de la biblioteca estandar prohibe usar una
conexion desde otro hilo distinto del que la creo, y saltarselo con
`check_same_thread=False` seria cambiar un cuelgue visible por una corrupcion
silenciosa.

**3. El espejo se regenera UNA vez al final de la tanda, no una por documento.**
`espejo.escritura` reescribe el `.xlsx` entero cada vez; con 500 documentos serian
500 reescrituras de un archivo que ademas va creciendo, y el coste crece con el
cuadrado del trabajo. Se hace al terminar —y **tambien al cancelar y al fallar**,
que es lo que impide que quedarse a medias deje el Excel atrasado sin que nadie lo
sepa—. Lo que NO cubre, dicho: si el programa se mata a la fuerza a mitad de una
tanda, la base queda al dia y el Excel atrasado hasta el siguiente guardado. Es el
unico camino que lo deja asi, y no se tapa.

**Lo que este modulo no hace:** no dibuja nada y no sabe que existe Tkinter. Quien
lo mueve, y quien decide cuando parar, es `interfaz/importacion.py`.
"""

from collections import namedtuple
from pathlib import Path

from datos.ilegibles import NO_SE_PUDO_ABRIR, anotar_documento_ilegible
from datos.registro import PAGINA, Cronometro
from espejo.escritura import regenerar_espejo
from importacion.documento import (
    casos_distintos,
    casos_duplicados,
    casos_importados,
    casos_pendientes_de_identificar,
    personas_importadas,
    paginas_no_importadas,
)
from importacion.guardado import guardar_las_paginas_del_documento

EXTENSION_DE_PDF = ".pdf"

LecturaDeUnPdf = namedtuple("LecturaDeUnPdf", ("ruta", "formularios", "error"))
ResultadoDeUnPdf = namedtuple(
    "ResultadoDeUnPdf",
    ("ruta", "paginas", "casos", "personas", "pendientes", "rechazadas", "error",
     # `duplicados` entro el 2026-09-03 y va al final con valor por defecto para
     # que nada de lo que ya construia un `ResultadoDeUnPdf` tenga que cambiar.
     # `rechazadas` se queda en la tupla y vale 0 en todas las tandas desde ese
     # dia: ninguna pagina se rechaza ya. No se retira porque la maquinaria que la
     # rellena sigue en pie y es la que usaria cualquier motivo futuro.
     "duplicados"),
    defaults=(0,),
)


def rutas_de_pdf(origenes):
    """Los PDF que hay en lo que se eligio: archivos sueltos y carpetas enteras.

    Acepta las dos cosas mezcladas porque las dos las puede elegir Miguel: unos
    cuantos archivos con Ctrl, o la carpeta del mes entera. Una carpeta se recorre
    **con sus subcarpetas**: los escaneos llegan repartidos por meses y obligar a
    entrar carpeta por carpeta seria devolverle el problema que venia a resolver.

    Se devuelven ordenados y sin repetidos. Ordenados porque la barra de progreso
    tiene que avanzar por un orden que Miguel pueda seguir en su carpeta, y sin
    repetidos porque elegir un archivo y ademas su carpeta es un descuido normal, y
    procesarlo dos veces produciria un caso rechazado por duplicado que parece un
    fallo del programa.

    Lo que NO se hace: no se abre ningun archivo aqui para comprobar si de verdad
    es un PDF. Eso solo se sabe abriendolo, y abrir 500 archivos dos veces cuesta
    el doble; el que no se pueda abrir deja su renglon cuando le toque.
    """
    encontradas = []
    for origen in origenes:
        ruta = Path(origen)
        if ruta.is_dir():
            encontradas.extend(
                hijo for hijo in ruta.rglob("*")
                if hijo.is_file() and hijo.suffix.lower() == EXTENSION_DE_PDF
            )
        elif ruta.is_file():
            encontradas.append(ruta)
    return sorted({ruta.resolve() for ruta in encontradas})


def leer_un_pdf(ruta_pdf, motor_ocr):
    """Lee un PDF entero y devuelve sus formularios, o el error que lo impidio.

    **No levanta nunca.** Es toda la razon de que exista en vez de llamar a
    `extraer_documento` directamente: en una tanda de 500, la excepcion de uno no
    puede ser la excepcion de la tanda. El error viaja como dato hasta quien sabe
    que hacer con el, que es quien deja el renglon en `documentos_ilegibles`.

    Se atrapa `Exception` a secas y NO una lista de tipos concretos, que es lo
    contrario de lo que este proyecto hace en todos los demas sitios. El motivo:
    aqui pasan por debajo `pypdf`, `pypdfium2`, `opencv` y `onnxruntime`, cuatro
    bibliotecas que levantan cada una lo suyo —y algunas levantan `RuntimeError`
    pelado—. Una lista de tipos seria una lista de los fallos que se me ocurrieron,
    y el primero que no este en ella tumba la tanda entera.

    ⚠️ Y no se silencia: el error se devuelve entero, con su tipo y su texto, y
    acaba escrito en la tabla. Atrapar para callar es lo que esta prohibido;
    atrapar para convertirlo en un renglon que Miguel puede leer es lo contrario.
    """
    from extraccion.formulario import extraer_documento

    # El cronometro envuelve la lectura entera y deja su linea en `fichas.log`
    # **tambien cuando falla**: lo que tarda en fallar es justo lo que hace falta
    # saber cuando el dueno dice que una importacion se quedo colgada. En el
    # registro va la ruta contada en paginas y segundos, nunca lo que se leyo.
    with Cronometro(PAGINA, archivo=Path(ruta_pdf).stem[:24]) as cronometro:
        try:
            lectura = LecturaDeUnPdf(
                Path(ruta_pdf), extraer_documento(ruta_pdf, motor_ocr), None
            )
        except Exception as causa:
            return LecturaDeUnPdf(Path(ruta_pdf), [], f"{type(causa).__name__}: {causa}")
        cronometro.detalles["paginas"] = len(lectura.formularios)
        return lectura


def guardar_un_pdf(conexion, lectura):
    """Guarda lo leido de un PDF y devuelve las cifras de ese documento.

    Cuando la lectura fallo, no hay nada que guardar y lo unico que se escribe es
    el renglon de `documentos_ilegibles` con el motivo `no_se_pudo_abrir` y el
    error tal cual. Ese renglon es la unica prueba de que ese archivo se intento:
    sin el, un PDF corrupto desaparece de la tanda sin dejar rastro, que es
    exactamente lo que no puede pasar.
    """
    if lectura.error is not None:
        anotar_documento_ilegible(
            conexion, lectura.ruta, NO_SE_PUDO_ABRIR, detalle=lectura.error
        )
        return ResultadoDeUnPdf(
            lectura.ruta, paginas=0, casos=0, personas=0, pendientes=0,
            rechazadas=0, error=lectura.error, duplicados=0,
        )

    paginas = guardar_las_paginas_del_documento(conexion, lectura.formularios)
    resultado = _ResultadoParaContar(len(lectura.formularios), paginas)
    return ResultadoDeUnPdf(
        lectura.ruta,
        paginas=len(lectura.formularios),
        casos=casos_distintos(resultado),
        personas=personas_importadas(resultado),
        pendientes=len(casos_pendientes_de_identificar(resultado)),
        rechazadas=len(paginas_no_importadas(resultado)),
        duplicados=len(casos_duplicados(resultado)),
        error=None,
    )


# Las funciones de conteo de `importacion.documento` reciben el resultado entero de
# una importacion y solo miran dos de sus campos. Esta tupla les da esos dos sin
# tener que montar el resultado completo —que ademas exige la ruta y el espejo, que
# aqui no vienen al caso— ni duplicar las cuatro cuentas en este modulo.
_ResultadoParaContar = namedtuple("_ResultadoParaContar", ("paginas", "paginas_por_pagina"))


class ResumenDeLaTanda:
    """Las cifras de toda la tanda, sumadas segun van llegando.

    Es una clase y no una tupla porque va creciendo mientras la tanda corre: la
    ventana de progreso ensena estas mismas cifras al vuelo, y al terminar son las
    del resumen final. Dos cuentas separadas —una para la pantalla y otra para el
    resumen— serian dos cuentas que pueden no coincidir.
    """

    def __init__(self, total_de_documentos):
        self.total_de_documentos = total_de_documentos
        self.documentos = 0
        self.paginas = 0
        self.casos = 0
        self.personas = 0
        self.pendientes = 0
        self.rechazadas = 0
        self.duplicados = 0
        self.fallidos = []
        self.cancelada = False

    def anotar(self, resultado):
        """Suma lo de un documento. Devuelve el mismo resultado, para encadenar."""
        self.documentos += 1
        self.paginas += resultado.paginas
        self.casos += resultado.casos
        self.personas += resultado.personas
        self.pendientes += resultado.pendientes
        self.rechazadas += resultado.rechazadas
        self.duplicados += resultado.duplicados
        if resultado.error is not None:
            self.fallidos.append((resultado.ruta, resultado.error))
        return resultado

    def texto(self):
        """El resumen final, en espanol y con las cifras que cambian una decision.

        Lleva SIEMPRE todas las cifras, tambien cuando alguna es cero. Un resumen
        que se calla los ceros obliga a preguntarse si es que no hubo o es que no
        se cuenta, y esa duda recae justo sobre los numeros que hay que creerse.
        """
        lineas = [
            "IMPORTACIÓN CANCELADA. Lo que ya se había procesado quedó guardado."
            if self.cancelada
            else "Importación terminada.",
            f"Documentos procesados: {self.documentos} de {self.total_de_documentos}.",
            f"Páginas leídas: {self.paginas}.",
            f"Casos guardados: {self.casos}, con {self.personas} personas.",
            f"Pendientes de identificar (entraron sin número de caso): {self.pendientes}.",
            f"Duplicados (entraron, y repiten a un caso que ya estaba): {self.duplicados}.",
            f"Páginas que no entraron: {self.rechazadas}.",
            f"Documentos que fallaron: {len(self.fallidos)}.",
        ]
        for ruta, error in self.fallidos:
            lineas.append(f"    · {ruta}: {error}")
        lineas.append("")
        lineas.append(
            "Ninguna página se rechaza por repetir el número de caso: el número son "
            "cuatro letras y el año y el mes, así que dos familias distintas de la "
            "misma unidad lo comparten. Un documento que repite a otro ENTRA igual, "
            "aparte y marcado como duplicado, y el que ya estaba NO se toca: nada se "
            "funde solo."
        )
        lineas.append(
            "Todo lo que no se pudo leer entero, y cada duplicado con el caso del que "
            "lo es, queda anotado renglón a renglón: se consulta con «Lo que no "
            "entró…» en la pantalla de inicio."
        )
        return "\n".join(lineas)


def cerrar_la_tanda(conexion, carpeta_de_datos=None, avisar=print):
    """Regenera el Excel espejo una sola vez, cuando la tanda ya no escribe mas.

    Se llama al terminar, al cancelar y cuando algo levanta. Es lo que impide que
    una tanda a medias deje la base al dia y el Excel atrasado, que es la averia
    que `espejo/escritura.py` existe para que no pueda pasar.
    """
    return regenerar_espejo(conexion, carpeta_de_datos=carpeta_de_datos, avisar=avisar)
