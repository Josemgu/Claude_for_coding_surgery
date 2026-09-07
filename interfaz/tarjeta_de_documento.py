"""La tarjeta de un documento en la pantalla «Revisar».

Una tarjeta por documento, que es la unidad de trabajo del dueno
(`DECISIONES.md`, 2026-09-03). Va en su propio archivo y no dentro de
`interfaz/revisar.py` porque son dos responsabilidades: alli se decide QUE
tarjetas se ven, aqui como se ve UNA.

**El color nunca va solo.** Cada estado lleva ademas su palabra en espanol, por lo
mismo que en `interfaz/tema.py`: un borde de color no se lee si no se distinguen
los colores, y en una rejilla de tarjetas media docena de bordes distintos son
indistinguibles de memoria.

**Sin parrafos.** El dueno lo pidio con esas palabras —«no pongas tanto texto»—, y
aqui se cumple contandolo: cada dato de la tarjeta es una etiqueta corta o una
palabra en una cajita, y no hay ni un `wraplength` que arme un bloque de texto.
`pruebas/prueba_pantalla_de_revisar.py` lo comprueba.
"""

import tkinter as tk
from tkinter import ttk

from datos.estados import COMPLETA, NO_COMPLETA
from datos.revision import SIN_REVISAR
from interfaz.etiquetas import numero_de_caso_visible
from interfaz.tema import BORDE, LETRA_DATO, LETRA_PEQUENA, TEXTO_SECUNDARIO

# El borde izquierdo y el fondo de cada estado, del mockup v2 con su contraste ya
# medido alli. La palabra va al lado SIEMPRE, no como alternativa al color.
CARAS = {
    SIN_REVISAR: ("#5B6470", "#FFFFFF", "sin revisar"),
    COMPLETA: ("#1B6E3C", "#FFFFFF", "completa"),
    NO_COMPLETA: ("#B3261E", "#FDE7E7", "no está completa"),
}
CARA_ARCHIVADA = ("#AAB1BC", "#F4F5F7", "archivado")

AZUL_DUPLICADO = "#1B4F9C"
AMBAR = "#8A5B00"
AMBAR_FONDO = "#FFF3C4"
VERDE = "#1B6E3C"
ROJO = "#B3261E"

ANCHO_DEL_BORDE = 6

# Las medidas del texto dibujado, en pixeles. Salen de lo que ocupaban las
# etiquetas que habia: cinco lineas cortas con `padx=10` y `pady=8`. En un lienzo
# la geometria hay que calcularla, porque no la reparte ningun gestor.
SANGRIA = ANCHO_DEL_BORDE + 8
ALTO_DE_LAS_MARCAS = 64
ALTO_DE_LA_FIRMA = 84
ALTO_DEL_LIENZO = 98


def _texto_de_las_hojas(documento):
    """«hoja 1» o «hojas 1–3», o nada si de ese documento no se guardo la hoja."""
    hojas = documento["hojas"]
    if hojas is None:
        return ""
    primera, ultima = hojas
    return f"hoja {primera}" if primera == ultima else f"hojas {primera}–{ultima}"


def _nombre_del_archivo(documento):
    """El nombre del PDF sin su carpeta. La ruta entera no cabe y no dice mas."""
    ruta = documento["ruta_pdf"]
    if not ruta:
        return "sin archivo"
    return str(ruta).replace("\\", "/").rsplit("/", 1)[-1]


def _dia_de(marca_de_tiempo):
    """'2026-09-03 09:14:00' -> '03-09-2026'. Vacio si no hay marca.

    Dia-mes-ano porque es como lo escribio el dueno —«Completada por Sandy ·
    03-09-2026»— y como se lee una fecha aqui. La base la guarda al reves, en ISO,
    porque asi ordena como cadena; la conversion se hace al pintar y en un solo sitio.
    """
    if not marca_de_tiempo:
        return ""
    partes = str(marca_de_tiempo).split(" ")[0].split("-")
    return "-".join(reversed(partes)) if len(partes) == 3 else str(marca_de_tiempo)


def texto_de_la_firma(documento):
    """Quien puso el estado y cuando, en una linea. Vacio si nadie lo puso.

    Tres formas, y las tres son palabras del dueno del 2026-09-03:

      - «Completada por Sandy · 03-09-2026» cuando la marca la trajo su hoja;
      - «No completa · Sandy · 03-09-2026» cuando la hoja dijo que no;
      - «Corregida por Miguel · 03-09-2026   ·   Sandy dijo: completa» cuando Miguel
        marco encima. **Lo que dijo el companero se sigue leyendo**, que es lo que
        deja ver que discreparon y el dato que se perderia si la correccion pisara
        la marca.
    """
    quien = documento["estado_marcado_por_nombre"]
    if not quien:
        return ""
    dia = _dia_de(documento["estado_marcado_en"])
    del_companero = documento["estado_del_companero_por_nombre"]
    if del_companero and del_companero != quien:
        palabra = CARAS.get(documento["estado_del_companero"], ("", "", "?"))[2]
        return f"Corregida por {quien} · {dia}   ·   {del_companero} dijo: {palabra}"
    if documento["estado_de_la_tarjeta"] == COMPLETA:
        return f"Completada por {quien} · {dia}"
    return f"No completa · {quien} · {dia}"


class Tarjeta(ttk.Frame):
    """Un documento: su numero, sus datos, sus marcas y sus botones.

    **El texto se dibuja en un lienzo; los botones siguen siendo botones.** Medido
    en esta maquina con 30 documentos, antes de este pase: 24 tarjetas costaban
    **359 widgets**, o sea 14 por tarjeta, y de esos solo cuatro eran controles con
    los que se hace algo —los dos de marcar, el de asignar y la casilla—. Los otros
    diez eran marcos y etiquetas: en Windows, diez ventanas del escritorio a unos
    5 ms cada una para enseñar texto que no se pulsa.

    Lo que **no** se toca son los botones, y el motivo es de uso y no de dibujo: en
    una tarjeta archivada toman el foco a proposito, porque llegar con Tab y leer
    por que no se pueden pulsar es la unica forma de enterarse. Un elemento
    dibujado en un lienzo no se tabula.
    """

    def __init__(self, padre, documento, acciones, con_casilla=False):
        super().__init__(padre)
        self.documento = documento
        self.acciones = acciones
        self.seleccionada = tk.BooleanVar(value=False)
        self._con_casilla = con_casilla
        self._construir()

    # ---- construccion ----------------------------------------------------

    def _cara(self):
        """El borde, el fondo y la palabra que le tocan a este documento."""
        if self.documento["archivado"]:
            return CARA_ARCHIVADA
        return CARAS[self.documento["estado_de_la_tarjeta"]]

    def _construir(self):
        borde, fondo, palabra = self._cara()
        self.configure(padding=0)
        self._fondo = fondo
        self.columnconfigure(0, weight=1)

        self.lienzo = tk.Canvas(
            self, background=fondo, highlightthickness=1, highlightbackground=BORDE,
            takefocus=0, height=ALTO_DEL_LIENZO,
        )
        self.lienzo.grid(row=0, column=0, sticky="ew")
        self.lienzo.bind("<Configure>", lambda evento: self._dibujar(palabra))
        self.lienzo.bind("<Button-1>", lambda evento: self.acciones.abrir(self.documento))

        self._cuerpo = tk.Frame(self, background=fondo, padx=10, pady=6)
        self._cuerpo.grid(row=1, column=0, sticky="ew")
        if self._con_casilla:
            tk.Checkbutton(
                self._cuerpo, variable=self.seleccionada, background=fondo,
                activebackground=fondo, takefocus=1,
            ).pack(side="left")
        self._botones()
        self._pie()
        self._dibujar(palabra)

    # ---- lo que se dibuja ------------------------------------------------

    def _dibujar(self, palabra):
        """Todo el texto de la tarjeta, de una vez, sobre el lienzo ya vacío."""
        self.lienzo.delete("all")
        ancho = max(self.lienzo.winfo_width(), 1)
        self.lienzo.create_rectangle(
            0, 0, ANCHO_DEL_BORDE, ALTO_DEL_LIENZO,
            fill=self._cara()[0], outline=self._cara()[0],
        )
        self._dibujar_la_cabecera(palabra, ancho)
        self._dibujar_los_datos(ancho)
        self._dibujar_las_marcas()
        self._dibujar_la_firma(ancho)

    def _dibujar_la_cabecera(self, palabra, ancho):
        """El numero de caso a la izquierda y la palabra del estado a la derecha."""
        self.lienzo.create_text(
            SANGRIA, 12, anchor="w", text=numero_de_caso_visible(self.documento),
            font=LETRA_DATO if self.documento["numero_caso"] else LETRA_PEQUENA,
            fill="#1A1D21" if self.documento["numero_caso"] else AMBAR,
        )
        self._pastilla_a_la_derecha(palabra, self._cara()[0], "#FFFFFF", ancho, 12)

    def _dibujar_los_datos(self, ancho):
        """Archivo, hojas, personas, fecha y unidad. Todo en dos lineas cortas."""
        hueco = max(ancho - SANGRIA - 8, 40)
        self.lienzo.create_text(
            SANGRIA, 30, anchor="w",
            text=f"{_nombre_del_archivo(self.documento)} · "
            f"{_texto_de_las_hojas(self.documento)}",
            fill=TEXTO_SECUNDARIO, font=LETRA_PEQUENA, width=hueco,
        )
        personas = self.documento["personas"]
        trozos = [f"{personas} persona" if personas == 1 else f"{personas} personas"]
        trozos.append(self.documento["fecha_viaje"] or "sin fecha de viaje")
        trozos.append(self.documento["unidad_nombre"] or "sin unidad")
        self.lienzo.create_text(
            SANGRIA, 46, anchor="w", text="  ·  ".join(trozos),
            fill=TEXTO_SECUNDARIO, font=LETRA_PEQUENA, width=hueco,
        )

    def _dibujar_las_marcas(self):
        """Las cajitas: por comprobar, duplicado, fecha pasada, lo que dijo el companero."""
        cursor = SANGRIA
        if self.documento["por_comprobar"]:
            cursor = self._pastilla(
                f"{self.documento['por_comprobar']} por comprobar",
                AMBAR_FONDO, "#3D2A00", cursor, ALTO_DE_LAS_MARCAS,
            )
        if self.documento["es_duplicado"]:
            original = self.documento["numero_del_original"] or self.documento["duplicado_de"]
            cursor = self._pastilla(
                f"duplicado de {original}", "#E4EEFB", "#0F2C57", cursor, ALTO_DE_LAS_MARCAS
            )
        if self.documento["fecha_pasada"] and not self.documento["archivado"]:
            cursor = self._pastilla(
                "fecha ya pasada", ROJO, "#FFFFFF", cursor, ALTO_DE_LAS_MARCAS
            )
        if (
            self.documento["propuesta_del_companero"] == COMPLETA
            and self.documento["estado_de_la_tarjeta"] != COMPLETA
        ):
            self._pastilla(
                "el compañero dice: lista", "#E7F5EC", "#12351F", cursor, ALTO_DE_LAS_MARCAS
            )

    def _dibujar_la_firma(self, ancho):
        """Quien marco el documento y cuando. Vacio si nadie lo marco todavia."""
        firma = texto_de_la_firma(self.documento)
        if not firma:
            return
        self.lienzo.create_text(
            SANGRIA, ALTO_DE_LA_FIRMA, anchor="w", text=firma,
            fill=TEXTO_SECUNDARIO, font=LETRA_PEQUENA,
            width=max(ancho - SANGRIA - 8, 40),
        )

    def _pastilla(self, texto, fondo, tinta, izquierda, arriba):
        """Una palabra en una cajita de color. Nunca un parrafo.

        Devuelve donde termina, para que la siguiente empiece ahi.
        """
        elemento = self.lienzo.create_text(
            izquierda + 6, arriba, anchor="w", text=texto, fill=tinta, font=LETRA_PEQUENA
        )
        recuadro = self.lienzo.bbox(elemento)
        caja = self.lienzo.create_rectangle(
            recuadro[0] - 6, recuadro[1] - 1, recuadro[2] + 6, recuadro[3] + 1,
            fill=fondo, outline=fondo,
        )
        # El rectangulo se dibuja despues, asi que taparia el texto: se manda detras.
        self.lienzo.tag_lower(caja, elemento)
        return recuadro[2] + 10

    def _pastilla_a_la_derecha(self, texto, fondo, tinta, ancho, arriba):
        """La palabra del estado, pegada al borde derecho de la tarjeta."""
        elemento = self.lienzo.create_text(
            ancho - 10, arriba, anchor="e", text=texto, fill=tinta, font=LETRA_PEQUENA
        )
        recuadro = self.lienzo.bbox(elemento)
        caja = self.lienzo.create_rectangle(
            recuadro[0] - 6, recuadro[1] - 1, recuadro[2] + 4, recuadro[3] + 1,
            fill=fondo, outline=fondo,
        )
        self.lienzo.tag_lower(caja, elemento)

    def _botones(self):
        """«Sí, completa» y «No está completa», y las dos acciones de la fecha pasada.

        En una tarjeta archivada los dos siguen tomando el foco a proposito —es la
        unica forma de llegar con Tab y leer por que no se puede pulsar— y su
        `command` solo ensena el motivo. No se usa `state=['disabled']`, que quita
        el foco.
        """
        archivado = bool(self.documento["archivado"])
        for texto, estado, color in (
            ("Sí, completa", COMPLETA, VERDE),
            ("No está completa", NO_COMPLETA, ROJO),
        ):
            pulsado = self.documento["estado_de_la_tarjeta"] == estado
            tk.Button(
                self._cuerpo, text=texto, font=LETRA_PEQUENA, relief="solid",
                borderwidth=1,
                background=color if pulsado else "#FFFFFF",
                foreground="#FFFFFF" if pulsado else color,
                activebackground=color, activeforeground="#FFFFFF",
                command=(
                    self.acciones.avisar_de_lo_archivado if archivado
                    else lambda e=estado: self.acciones.marcar(self.documento, e)
                ),
            ).pack(side="left", fill="x", expand=True, padx=(0, 4))
        if self.documento["fecha_pasada"] and not archivado:
            tk.Button(
                self._cuerpo, text="Archivar", font=LETRA_PEQUENA, relief="solid",
                borderwidth=1,
                command=lambda: self.acciones.archivar([self.documento]),
            ).pack(side="left", padx=(0, 4))

    def _pie(self):
        """El desplegable de asignar. La firma va dibujada, arriba, con lo demás.

        Los tres botones y la casilla van en la MISMA fila y no cada uno en su
        marco: los marcos eran cuatro widgets más por tarjeta que no se pulsan.
        """
        asignado = self.documento["asignado_a"] or "Sin asignar"
        tk.Button(
            self._cuerpo, text=f"{asignado}  ▾", font=LETRA_PEQUENA, relief="solid",
            borderwidth=1, anchor="w",
            command=lambda: self.acciones.asignar(self.documento),
        ).pack(side="left", fill="x", expand=True)
