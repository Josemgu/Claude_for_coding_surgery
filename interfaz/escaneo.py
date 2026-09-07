"""La pagina del escaneo de un caso, cargada una vez y recortada muchas.

Un caso pide entre nueve y catorce tiras —cuatro campos suyos y dos por cada
persona—, y las catorce salen de LA MISMA imagen. Rasterizarla catorce veces
seria repetir catorce veces el paso caro de la extraccion.

**No se vuelve a pasar el OCR**, y eso es lo que hace esto barato: los
rectangulos ya estan guardados en `procedencia_campo` desde la importacion. Aqui
solo se rasteriza y se corta.

**Lo que falla, falla en silencio pero no invisible.** Si el PDF ya no esta en su
sitio —se movio, se renombro, el disco externo no esta puesto—, no se levanta:
`imagen()` devuelve None, cada tira dibuja su hueco rayado con el motivo escrito,
y el caso se sigue pudiendo corregir a mano. Un caso que no se abre porque falta
una imagen es un caso que nadie atiende.
"""

from extraccion.recorte import tira_de_la_banda


class PaginaDelCaso:
    """El escaneo de la pagina de un caso, rasterizado como mucho una vez."""

    def __init__(self, ruta_pdf, pagina_pdf):
        self.ruta_pdf = ruta_pdf
        self.pagina_pdf = pagina_pdf
        self.motivo_del_fallo = None
        self._imagen = None
        self._ya_se_intento = False

    def _indice_de_pagina(self):
        """La pagina contada desde 0, que es lo que pide el rasterizador.

        En la base se guarda desde 1 —«pagina 2 de 6»— porque es como la cuentan
        las personas y el visor del sistema. La resta vive aqui y en un solo sitio.
        """
        return (self.pagina_pdf or 1) - 1

    def imagen(self):
        """La pagina en escala de grises, o None si no se pudo cargar.

        Se intenta UNA sola vez. Si fallo, los catorce recortes siguientes no
        vuelven a intentarlo: reintentar catorce veces sobre un archivo que no
        esta solo hace que la pantalla tarde catorce veces mas en decir lo mismo.
        """
        if self._ya_se_intento:
            return self._imagen
        self._ya_se_intento = True
        if not self.ruta_pdf:
            self.motivo_del_fallo = "este caso no guarda de qué PDF salió"
            return None
        try:
            from extraccion.rasterizado import rasterizar_pagina

            self._imagen = rasterizar_pagina(self.ruta_pdf, self._indice_de_pagina()).imagen
        except Exception as causa:
            # Se atrapa ancho a proposito, y NO se silencia: el motivo se guarda y
            # se escribe en la pantalla. Las formas de que esto falle son muchas
            # —archivo movido, PDF corrupto, pagina que ya no existe, permisos— y
            # ninguna justifica que el caso se vuelva inaccesible.
            self.motivo_del_fallo = f"no se pudo abrir el escaneo ({causa})"
            self._imagen = None
        return self._imagen

    def tira(self, banda):
        """Los bytes PGM de la tira de esa banda, o None si no hay con que."""
        if banda is None:
            return None
        return tira_de_la_banda(self.imagen(), banda)

    def motivo_para_la_tira(self):
        """Que escribir en el hueco de una tira que no se pudo dibujar."""
        return self.motivo_del_fallo or "el lector no encontró esta fila en el escaneo"


class PaginasDelPdf:
    """Las hojas de UN PDF, cada una rasterizada como mucho una vez.

    Existe porque un caso ya no vive en una sola hoja. Desde que las paginas de un
    grupo se unen en un solo caso, un formulario de doce personas ocupa seis hojas
    del PDF, y la tira de cada persona hay que recortarla de la SUYA: recortarlas
    todas de la hoja que abrio el caso ensena a once personas la imagen de otra, y
    esa imagen es justo lo que existe para no tener que fiarse del OCR.

    Rasterizar es el paso caro, asi que se guarda una `PaginaDelCaso` por hoja y se
    reutiliza para todas las tiras de esa hoja. Un caso de seis hojas rasteriza
    seis veces, no una por tira.
    """

    def __init__(self, ruta_pdf):
        self.ruta_pdf = ruta_pdf
        self._por_hoja = {}

    def pagina(self, pagina_pdf):
        """La hoja pedida, contada desde 1. Sin numero se entiende la primera.

        Un `None` no es un error: una persona guardada antes de la version 4 del
        esquema no sabe de que hoja salio, y quien llama decide con que hoja
        sustituirlo. Aqui se resuelve a la primera, que es lo que hacia el
        programa entero antes de que existiera esta clase.
        """
        hoja = pagina_pdf or 1
        if hoja not in self._por_hoja:
            self._por_hoja[hoja] = PaginaDelCaso(self.ruta_pdf, hoja)
        return self._por_hoja[hoja]

    def rasterizadas(self):
        """Las hojas que se han llegado a pedir. Sirve para contarlas y medirlas."""
        return tuple(sorted(self._por_hoja))

    @property
    def motivo_del_fallo(self):
        """El primer motivo por el que alguna hoja no se pudo cargar, o None.

        Se devuelve uno y no la lista: los motivos por los que esto falla —el PDF
        se movio, se renombro, el disco no esta— afectan al archivo entero, y
        repetir seis veces la misma frase en la cabecera no anade nada.
        """
        for hoja in sorted(self._por_hoja):
            motivo = self._por_hoja[hoja].motivo_del_fallo
            if motivo:
                return motivo
        return None
