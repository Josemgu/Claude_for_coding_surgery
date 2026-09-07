"""La vista de mes de la pantalla de inicio, dibujada en un solo lienzo.

Es **contexto, no alarma**: contesta «¿cómo viene el mes?», que es una pregunta
que se hace sentado. Lo urgente vive arriba, en el bloque rojo, y este control no
lo toca: cambiar de mes con «‹ ›» NO cambia el bloque rojo, que siempre son los
proximos siete dias desde hoy, se mire el mes que se mire.

Vive aparte de `interfaz/inicio.py` porque es una pieza cerrada —una rejilla que
sabe pintar un mes y avisar cuando se pulsa un caso— y porque la pantalla de
inicio ya tiene bastante con los otros tres bloques.

**Un dia con mas casos de los que caben lo dice**: «+2 casos más». Ocultarlos en
silencio seria esconder justo los dias mas cargados, que son los que hay que
mirar.

---

**Por que un `Canvas` y no un `Frame`+`Label` por dia.** Lo midio el supervisor en
esta maquina, y no es una preferencia de estilo:

    186 widgets ttk creados y pintados ..... 0,87–1,40 s   destruirlos 0,32–0,68 s
    un solo Canvas con 186 textos y rects ... 0,52 s        destruirlo  0,01 s
    2000 llamadas Tk triviales ............. 0,006 s

O sea: ni las consultas ni las llamadas a Tk cuestan nada. **Lo que cuesta es
mapear y destruir cada widget**, porque en Windows cada widget de Tk es una
ventana del sistema y crearla pasa por el escritorio. Con la version anterior de
este modulo, un mes normal creaba mas de cien controles y los destruia enteros
cada vez que se volvia de un caso.

Aqui el mes entero son **un lienzo y nada mas**: los recuadros, los numeros, las
pastillas, la palabra ARCHIVADO y la leyenda son elementos dibujados dentro. El
clic no lo recibe un `Label`, lo recibe el elemento por su etiqueta —`tag_bind`—,
que es lo que hace que no haga falta un control por caso para poder pulsarlo.

**Lo que NO cambia es lo que se ve.** Los mismos textos, los mismos colores, la
misma leyenda y el mismo «+N casos más». Si algo de eso desapareciera, el ahorro
no valdria nada: esta pantalla existe para que Miguel vea el mes.
"""

import calendar
from datetime import date

import tkinter as tk
from tkinter import ttk

from datos.calendario import DIAS_DE_LA_VENTANA, vista_de_mes
from interfaz.tema import (
    BORDE,
    LETRA_PEQUENA,
    ROJO_DE_FILA,
    ROJO_SOLIDO,
    TEXTO_SECUNDARIO,
    VERDE_RESUELTO,
)

DIAS_DE_LA_SEMANA = ("lun", "mar", "mié", "jue", "vie", "sáb", "dom")
MESES = (
    "enero", "febrero", "marzo", "abril", "mayo", "junio",
    "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
)

# Cuantos casos caben en la casilla de un dia. La celda mide unos 80 px y tres
# pastillas no caben sin encoger la letra por debajo de lo legible.
CASOS_VISIBLES_POR_DIA = 2

# El gris de lo archivado. No es ninguno de los colores de estado a proposito: un
# caso cerrado no es un estado de la recomendacion, es una propiedad del documento,
# y darle el verde de «resuelta» diria algo que nadie ha comprobado.
PASTILLA_ARCHIVADA = "#E9EBEF"
GRIS_DE_LA_MARCA = "#46505C"
LETRA_DE_LA_MARCA = ("Segoe UI", 7, "bold")

# El fondo del lienzo: el de la ventana, para que la rejilla no se recorte contra
# un rectangulo gris.
FONDO_DEL_MES = "#F7F7F8"

# Las medidas del dibujo, en pixeles. Salen del control anterior: la fila de
# nombres de dia media una linea de `LETRA_PEQUENA`, la celda unos 80 px de alto y
# la leyenda una linea. Se escriben aqui porque en un lienzo la geometria hay que
# calcularla, no la reparte nadie.
ALTO_DE_LOS_NOMBRES = 16
ALTO_DE_LA_LEYENDA = 18
ALTO_MINIMO_DE_CELDA = 44
ALTO_DE_UNA_PASTILLA = 13
ALTO_DE_LA_MARCA = 10
MARGEN = 2

# Debajo de esto no se dibuja: el lienzo todavia no tiene tamano real y cualquier
# reparto saldria negativo. Tk da 1x1 a un widget que aun no se ha realizado.
ANCHO_MINIMO_PARA_DIBUJAR = 60


class VistaDeMes(ttk.Frame):
    """La rejilla de un mes en un lienzo, con los casos de cada dia dentro."""

    def __init__(self, padre, conexion, al_abrir_caso, hoy):
        super().__init__(padre)
        self.conexion = conexion
        self._al_abrir_caso = al_abrir_caso
        self.hoy = hoy
        self._mes = (hoy.year, hoy.month)
        # Los casos que se pueden pulsar, por la etiqueta con la que se dibujaron.
        # Es lo que sustituye a «un widget por caso que se acuerda de su caso».
        self._pulsables = {}
        self._medida_dibujada = None
        self.rowconfigure(1, weight=1)
        self.columnconfigure(0, weight=1)
        self._construir_cabecera()
        self.lienzo = tk.Canvas(
            self, background=FONDO_DEL_MES, highlightthickness=0, takefocus=0
        )
        self.lienzo.grid(row=1, column=0, sticky="nsew")
        self.lienzo.bind("<Configure>", self._al_cambiar_de_tamano)
        self.pintar()

    def _construir_cabecera(self):
        """El mes con su año y las dos flechas. Son los únicos controles del bloque."""
        cabecera = ttk.Frame(self)
        cabecera.grid(row=0, column=0, sticky="ew")
        self._titulo = ttk.Label(cabecera, style="Seccion.TLabel")
        self._titulo.pack(side="left")
        ttk.Button(cabecera, text="›", width=3, command=lambda: self._cambiar(1)).pack(
            side="right"
        )
        ttk.Button(cabecera, text="‹", width=3, command=lambda: self._cambiar(-1)).pack(
            side="right"
        )

    def poner_hoy(self, hoy):
        """Cambia el dia de referencia y vuelve al mes de ese dia.

        **No repinta si no ha cambiado nada, y eso es la mitad de lo que tardaba
        en abrir el programa.** Medido con `cProfile` sobre una base de 300
        documentos inventados, antes de tocar esto:

            interfaz/mes.py:pintar ....... 2 llamadas, 3 647 ms acumulados
            destruir 230 widgets ......... 2 885 ms

        Dos llamadas, no una: `__init__` pinta el mes, y acto seguido `refrescar()`
        llamaba aqui y lo **volvia a pintar entero**. Nadie lo veia porque el
        resultado era correcto; solo costaba unos segundos cada vez que se abria o
        se volvia de un caso.

        Sigue valiendo con el lienzo, aunque ahora el repintado sea mucho mas
        barato: leer el mes de la base tambien cuesta, y no hace falta leerlo para
        volver a dibujar lo mismo.
        """
        mes_nuevo = (hoy.year, hoy.month)
        if self.hoy == hoy and self._mes == mes_nuevo:
            return
        self.hoy = hoy
        self._mes = mes_nuevo
        self.pintar()

    def _al_cambiar_de_tamano(self, evento):
        """Repinta cuando el lienzo cambia de tamaño de verdad.

        Se compara la medida con la ultima dibujada porque `<Configure>` llega
        tambien por cosas que no cambian el reparto, y repintar en cada uno haria
        justo lo que este modulo existe para no hacer.
        """
        if (evento.width, evento.height) == self._medida_dibujada:
            return
        self._dibujar(evento.width, evento.height)

    def pintar(self):
        """Vuelve a leer el mes de la base y a dibujarlo con la medida que haya."""
        self._medida_dibujada = None
        anio, mes = self._mes
        self._titulo.configure(text=f"{MESES[mes - 1].capitalize()} de {anio}")
        self._por_dia = vista_de_mes(self.conexion, anio, mes)
        self._dibujar(self.lienzo.winfo_width(), self.lienzo.winfo_height())

    # ---- el dibujo -------------------------------------------------------

    def _dibujar(self, ancho, alto):
        """Pinta el mes entero de una vez sobre el lienzo, ya vacío.

        Borrar y volver a dibujar el lienzo entero cuesta 0,01 s de destruccion
        frente a los 0,32–0,68 s que costaba destruir los widgets equivalentes, asi
        que no hace falta dibujar por partes ni llevar cuenta de lo que cambio.
        """
        self.lienzo.delete("all")
        self._pulsables = {}
        if ancho < ANCHO_MINIMO_PARA_DIBUJAR:
            # Todavia no hay sitio: el `<Configure>` que llegue con el tamano real
            # volvera a llamar aqui.
            return
        self._medida_dibujada = (ancho, alto)
        anio, mes = self._mes
        semanas = calendar.Calendar().monthdayscalendar(anio, mes)

        ancho_de_celda = ancho / 7
        alto_util = max(alto - ALTO_DE_LOS_NOMBRES - ALTO_DE_LA_LEYENDA, 0)
        alto_de_celda = max(alto_util / max(len(semanas), 1), ALTO_MINIMO_DE_CELDA)

        self._dibujar_los_nombres(ancho_de_celda)
        for numero_de_semana, semana in enumerate(semanas):
            for columna, dia in enumerate(semana):
                if dia:
                    self._dibujar_dia(
                        anio, mes, dia, columna, numero_de_semana,
                        ancho_de_celda, alto_de_celda,
                    )
        self._dibujar_la_leyenda(
            ALTO_DE_LOS_NOMBRES + alto_de_celda * len(semanas) + 2
        )
        if not self._por_dia:
            self.lienzo.create_text(
                MARGEN, ALTO_DE_LOS_NOMBRES // 2, anchor="w", text="Ningún caso en este mes.",
                fill=TEXTO_SECUNDARIO, font=LETRA_PEQUENA,
            )
        self.lienzo.configure(scrollregion=self.lienzo.bbox("all"))

    def _dibujar_los_nombres(self, ancho_de_celda):
        """La fila «lun mar mié…» de arriba."""
        for columna, nombre in enumerate(DIAS_DE_LA_SEMANA):
            self.lienzo.create_text(
                columna * ancho_de_celda + MARGEN, ALTO_DE_LOS_NOMBRES // 2,
                anchor="w", text=nombre, fill=TEXTO_SECUNDARIO, font=LETRA_PEQUENA,
            )

    def _dibujar_dia(self, anio, mes, dia, columna, fila, ancho_de_celda, alto_de_celda):
        """Una casilla del calendario, con hasta dos casos y «+N más» si hay más.

        Los dias de la ventana de siete dias van con fondo rojo claro: es el limite
        DIBUJADO, para que se pueda comprobar mirando que el dia 7 entra y el 8 no.

        ⚠️ **Un dia cuyos unicos casos esten archivados NO se pinta de rojo.** El
        dueno metio los archivados en el calendario y dejo fuera la franja de siete
        dias: *«La franja roja de los 7 días y la lista de pendientes siguen sin
        archivados»* (`DECISIONES.md`, 2026-09-03). Sin este filtro el fondo rojo
        volveria a entrar por la puerta de atras y avisaria de trabajo que ya se
        cerro.
        """
        casos = self._por_dia.get(date(anio, mes, dia).isoformat(), [])
        vivos = [caso for caso in casos if not caso.get("esta_archivado")]
        en_la_ventana = 0 <= (date(anio, mes, dia) - self.hoy).days <= DIAS_DE_LA_VENTANA

        izquierda = columna * ancho_de_celda
        arriba = ALTO_DE_LOS_NOMBRES + fila * alto_de_celda
        derecha = izquierda + ancho_de_celda - 1
        abajo = arriba + alto_de_celda - 1
        self.lienzo.create_rectangle(
            izquierda, arriba, derecha, abajo,
            fill=ROJO_DE_FILA if en_la_ventana and vivos else "#FFFFFF",
            outline=BORDE,
        )
        self.lienzo.create_text(
            izquierda + MARGEN + 2, arriba + MARGEN + 4, anchor="w", text=str(dia),
            fill=TEXTO_SECUNDARIO, font=LETRA_PEQUENA,
        )

        cursor = arriba + ALTO_DE_UNA_PASTILLA + MARGEN
        for caso in casos[:CASOS_VISIBLES_POR_DIA]:
            cursor = self._dibujar_pastilla(caso, izquierda, derecha, cursor)
        self._dibujar_los_que_no_caben(casos, izquierda, cursor)

    def _dibujar_pastilla(self, caso, izquierda, derecha, arriba):
        """Un caso dentro de su dia. **El archivado dice la palabra, no solo el gris.**

        Lo pidio el dueno con esas palabras: *«Lo que archivo debe verse en el
        calendario, debe decir archivado.»* La palabra va en SU PROPIA LINEA, y no
        pegada al numero de caso: medido por el disenador, en la misma linea la
        casilla la cortaba en «ARC…», y una palabra a medias no dice nada.

        El gris solo no bastaria aunque cupiera: es la misma regla que gobierna
        todos los estados de este programa —el color nunca va solo—, porque un
        color sin palabra no se lee si no se distinguen los colores.

        Devuelve donde termina, que es donde empieza lo siguiente.
        """
        archivado = caso.get("esta_archivado")
        if archivado:
            fondo, texto = PASTILLA_ARCHIVADA, TEXTO_SECUNDARIO
        elif caso["recomendacion_sin_resolver"]:
            fondo, texto = ROJO_SOLIDO, "#FFFFFF"
        else:
            fondo, texto = VERDE_RESUELTO, "#FFFFFF"

        alto = ALTO_DE_UNA_PASTILLA + (ALTO_DE_LA_MARCA if archivado else 0)
        # La etiqueta es lo que hace pulsable el caso sin un control por caso: el
        # clic lo recibe el elemento, y `tag_bind` lo traduce a este caso.
        etiqueta = f"caso:{caso['id']}"
        self._pulsables[etiqueta] = caso
        self.lienzo.create_rectangle(
            izquierda + MARGEN, arriba, derecha - MARGEN, arriba + alto,
            fill=fondo, outline=BORDE, tags=(etiqueta,),
        )
        ancho_del_texto = max(int(derecha - izquierda - 4 * MARGEN), 1)
        self.lienzo.create_text(
            izquierda + 2 * MARGEN, arriba + ALTO_DE_UNA_PASTILLA // 2, anchor="w",
            text=f"{caso['numero_caso'] or 'sin número'} · {caso['personas']}",
            fill=texto, font=LETRA_PEQUENA, width=ancho_del_texto, tags=(etiqueta,),
        )
        if archivado:
            self.lienzo.create_text(
                izquierda + 2 * MARGEN,
                arriba + ALTO_DE_UNA_PASTILLA + ALTO_DE_LA_MARCA // 2,
                anchor="w", text="ARCHIVADO", fill=GRIS_DE_LA_MARCA,
                font=LETRA_DE_LA_MARCA, width=ancho_del_texto, tags=(etiqueta,),
            )
        self.lienzo.tag_bind(
            etiqueta, "<Button-1>", lambda evento, e=etiqueta: self._al_pulsar(e)
        )
        return arriba + alto + MARGEN

    def _dibujar_los_que_no_caben(self, casos, izquierda, arriba):
        """«+2 casos más». Ocultarlos en silencio esconde los dias mas cargados."""
        de_mas = len(casos) - CASOS_VISIBLES_POR_DIA
        if de_mas <= 0:
            return
        self.lienzo.create_text(
            izquierda + 2 * MARGEN, arriba + ALTO_DE_UNA_PASTILLA // 2, anchor="w",
            text=f"+{de_mas} caso{'s' if de_mas != 1 else ''} más",
            fill=TEXTO_SECUNDARIO, font=LETRA_PEQUENA,
        )

    def _dibujar_la_leyenda(self, arriba):
        """Que significa cada color, con su palabra. Cuatro entradas, una linea.

        Va porque el calendario es lo unico de esta pantalla donde el estado se
        dice **solo** con color: en una casilla de 74 px no cabe la palabra de cada
        caso. La leyenda es lo que evita que el color vaya solo.
        """
        cursor = MARGEN
        for color, palabra in (
            (ROJO_SOLIDO, "sin completar"),
            (VERDE_RESUELTO, "completa"),
            (GRIS_DE_LA_MARCA, "archivado"),
        ):
            self.lienzo.create_rectangle(
                cursor, arriba + 4, cursor + 8, arriba + 12, fill=color, outline=color
            )
            cursor += 12
            elemento = self.lienzo.create_text(
                cursor, arriba + 8, anchor="w", text=palabra,
                fill=TEXTO_SECUNDARIO, font=LETRA_PEQUENA,
            )
            cursor = self.lienzo.bbox(elemento)[2] + 8
        self.lienzo.create_text(
            cursor, arriba + 8, anchor="w", text="· el número es cuántas personas",
            fill=TEXTO_SECUNDARIO, font=LETRA_PEQUENA,
        )

    # ---- lo que hace el raton --------------------------------------------

    def _al_pulsar(self, etiqueta):
        """Abre el caso de esa etiqueta. Es el clic que antes recibia un `Label`."""
        caso = self._pulsables.get(etiqueta)
        if caso is not None:
            self._al_abrir_caso(caso)

    def _cambiar(self, paso):
        """Mes anterior o siguiente. El bloque rojo de arriba NO se entera."""
        anio, mes = self._mes
        mes += paso
        if mes < 1:
            anio, mes = anio - 1, 12
        elif mes > 12:
            anio, mes = anio + 1, 1
        self._mes = (anio, mes)
        self.pintar()
