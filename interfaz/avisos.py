"""Una sola franja de avisos: una linea, agrupada, y que se puede cerrar.

**Lo que el dueno dijo, literal:** *«Nadie que use ese sistema va a leer tanto.
Ese es el error: el texto ocupa todo el programa.»* Y: *«que se pueda cerrar el
texto.»*

**Lo que ensenaba su captura** (`DECISIONES.md`, 2026-09-03): un caso de cinco
hojas con **seis bloques de aviso** en amarillo, cinco casi identicos, que
empujaban el documento a un sello del 7 % y los campos al borde de abajo.

Las cuatro reglas de esa decision, y donde las cumple este archivo:

  1. **Un aviso es una linea.** La franja mide 30 px cerrada. Lo que haya que
     explicar va detras de «ver», cerrado por defecto.
  2. **Los avisos iguales se agrupan.** Cinco paginas con el mismo problema son
     un aviso con «× 5 páginas», no cinco bloques. Lo hace `agrupar`.
  3. **El documento y los campos mandan sobre el espacio.** Abierta tiene un alto
     **maximo** de 96 px y se desplaza dentro de si; **nunca empuja lo de abajo**.
  4. El texto se escribe corto, sin contar por que el programa hace lo que hace.

**Y una quinta que no estaba escrita pero se deduce de «que se pueda cerrar»:**
cerrar **no puede perder** el aviso. Al cerrarla queda una campana con la cuenta
en la cabecera, y volver a pulsarla la reabre. Un aviso que desaparece del todo
es un aviso que nadie atendera nunca.
"""

import tkinter as tk

from interfaz.desplazamiento import atar_desplazamiento
from interfaz.tema import LETRA_PEQUENA, LETRA_SECCION

# Los tres del ambar, medidos por el disenador en `mockups/mockup-v2-inicio.html`:
# el texto sobre el fondo da 12.34:1, muy por encima del 4.5:1 que pide la WCAG.
FONDO = "#FFF3C4"
MARCA = "#8A5B00"
TINTA = "#3D2A00"
FONDO_AL_PASAR = "#F7E39A"

# El alto maximo de la franja abierta. Es la regla 3 hecha numero: pasado esto se
# desplaza DENTRO, y lo de abajo no se mueve ni un pixel.
ALTO_MAXIMO_ABIERTA = 96


class Aviso:
    """Un aviso ya agrupado: su frase y cuantos son.

    **La frase trae dentro su propio sustantivo** —«documentos duplicados sin
    decidir», «hojas que no se pudieron leer»—, asi que la linea se arma poniendo
    la cuenta delante y ya se lee sola. No hay un campo `unidad` aparte, y eso no
    es solo simplicidad: un literal que sea exactamente «documentos» es lo que
    `pruebas/auditoria_rutas.py` prohibe en todo el codigo, porque asi es como se
    compondria a mano la ruta de la carpeta de datos.
    """

    def __init__(self, texto, cuenta=1, al_ver=None):
        self.texto = texto
        self.cuenta = cuenta
        self.al_ver = al_ver

    def __eq__(self, otro):
        return (
            isinstance(otro, Aviso)
            and (self.texto, self.cuenta) == (otro.texto, otro.cuenta)
        )

    def __repr__(self):
        return f"Aviso({self.texto!r}, {self.cuenta})"

    def en_una_linea(self):
        """«3 documentos duplicados sin decidir»."""
        return f"{self.cuenta} {self.texto}"


def agrupar(avisos):
    """Junta los avisos que dicen lo mismo y les pone la cuenta.

    Es la regla 2 de la decision. El orden de aparicion se conserva: el primero
    que salio es el primero que se lee, y no se reordena por cuenta —un aviso que
    salta de sitio entre dos repintados se lee como un aviso nuevo—.
    """
    agrupados = {}
    for aviso in avisos:
        clave = aviso.texto
        if clave in agrupados:
            agrupados[clave].cuenta += aviso.cuenta
        else:
            agrupados[clave] = Aviso(aviso.texto, aviso.cuenta, aviso.al_ver)
    return list(agrupados.values())


def resumen_en_una_linea(avisos):
    """La linea de la franja cerrada: «3 documentos duplicados · 2 hojas ilegibles».

    Nunca ocupa mas de una linea porque nunca se pintan mas de dos grupos; lo que
    pase de ahi se cuenta en «y N más», que es lo que el «ver» despliega.
    """
    if not avisos:
        return ""
    primeros = [f"{aviso.cuenta} {aviso.texto}" for aviso in avisos[:2]]
    if len(avisos) > 2:
        primeros.append(f"y {len(avisos) - 2} más")
    return "  ·  ".join(primeros)


class FranjaDeAvisos(tk.Frame):
    """La franja. **Nunca hay mas de una en la pantalla**, y esa es su razon de ser.

    `al_cambiar` se llama cada vez que cambia lo que hay que ensenar o si esta
    cerrada, y es como la cabecera sabe si tiene que dibujar la campana con la
    cuenta. La franja no toca la cabecera: avisa y quien la puso decide.
    """

    def __init__(self, padre, al_cambiar=None):
        super().__init__(padre, background=FONDO)
        self._al_cambiar = al_cambiar
        self._avisos = []
        self._abierta = False
        self._cerrada_por_miguel = False
        self._detalle = None
        # Como estaba colocada la ultima vez que se quito de la vista. Ver
        # `_quitarse_de_la_vista`: sin esto una franja que se queda sin avisos no
        # vuelve a verse nunca.
        self._como_estaba_puesta = None
        self._construir_la_linea()
        self._aplicar()

    # ---- construccion ----------------------------------------------------

    def _construir_la_linea(self):
        """La linea de 30 px: el signo, el texto, «ver» y la equis."""
        self._linea = tk.Frame(self, background=FONDO)
        self._linea.pack(fill="x")
        tk.Label(
            self._linea, text="⚠", background=FONDO, foreground=MARCA,
            font=LETRA_SECCION, padx=8, takefocus=0,
        ).pack(side="left")
        self._texto = tk.Label(
            self._linea, background=FONDO, foreground=TINTA, font=LETRA_PEQUENA,
            anchor="w", takefocus=0,
        )
        self._texto.pack(side="left", fill="x", expand=True)
        self._boton_cerrar = self._boton("×", self.cerrar, "Cerrar los avisos")
        self._boton_cerrar.pack(side="right", padx=(2, 8), pady=4)
        self._boton_ver = self._boton("ver ▾", self.alternar, "Ver el detalle")
        self._boton_ver.pack(side="right", padx=2, pady=4)

    def _boton(self, texto, al_pulsar, descripcion):
        """Un boton plano de la franja, con su anillo de foco y su atajo."""
        boton = tk.Button(
            self._linea, text=texto, command=al_pulsar, font=LETRA_PEQUENA,
            background=FONDO, foreground=TINTA, activebackground=FONDO_AL_PASAR,
            highlightbackground=MARCA, relief="solid", borderwidth=1,
            padx=6, pady=0, takefocus=1, cursor="hand2",
        )
        # Sin esto un lector de pantalla lee «×» y nada mas.
        boton.descripcion = descripcion
        return boton

    # ---- lo que se ensena ------------------------------------------------

    def poner(self, avisos):
        """Cambia los avisos. Se agrupan aqui: quien llama no tiene que saberlo.

        ⚠️ **Volver a poner LOS MISMOS avisos no reabre una franja cerrada, y esa
        distincion se midio el 2026-09-04.** Hasta ese dia cualquier llamada a
        `poner` borraba el cierre. En Inicio no se notaba —los avisos se ponen al
        refrescar la pantalla— pero en Correccion `_pintar_avisos` corre en cada
        recuento, o sea **con cada tecla y con cada cambio de foco**: medido, la
        franja cerrada volvia a aparecer al pulsar Tab una vez. «Que se pueda
        cerrar el texto» dejaba de cumplirse en el mismo segundo.

        Lo que la regla queria decir sigue en pie, y ahora lo dice el codigo: unos
        avisos **nuevos** vuelven a merecer una mirada; los mismos de antes, no.
        La comparacion es por texto y cuenta (`Aviso.__eq__`).
        """
        nuevos = agrupar(list(avisos))
        if nuevos != self._avisos:
            self._cerrada_por_miguel = False
            self._abierta = False
        self._avisos = nuevos
        self._aplicar()

    def cuantos(self):
        """Cuantos avisos hay, ya agrupados. Es el numero de la campana."""
        return sum(aviso.cuenta for aviso in self._avisos)

    def esta_cerrada(self):
        return self._cerrada_por_miguel

    # ---- abrir, cerrar, alternar ----------------------------------------

    def alternar(self, evento=None):
        """«ver» / «ocultar». No cambia el alto de nada que este debajo."""
        self._abierta = not self._abierta
        self._aplicar()
        return "break"

    def cerrar(self, evento=None):
        """La equis y `Esc`. La franja se va; la cuenta se queda en la campana."""
        self._cerrada_por_miguel = True
        self._abierta = False
        self._aplicar()
        return "break"

    def reabrir(self, evento=None):
        """Lo que hace la campana de la cabecera al pulsarla."""
        self._cerrada_por_miguel = False
        self._aplicar()
        return "break"

    # ---- pintado ---------------------------------------------------------

    def _aplicar(self):
        """Deja la franja como toca y avisa a quien la puso. Un solo sitio."""
        self._quitar_el_detalle()
        if not self._avisos or self._cerrada_por_miguel:
            self._quitarse_de_la_vista()
        else:
            self._volver_a_la_vista()
            self._texto.configure(text=resumen_en_una_linea(self._avisos))
            self._boton_ver.configure(text="ocultar ▴" if self._abierta else "ver ▾")
            if self._abierta:
                self._pintar_el_detalle()
        if self._al_cambiar is not None:
            self._al_cambiar(self.cuantos(), self._cerrada_por_miguel)

    def _quitarse_de_la_vista(self):
        """Se retira recordando como estaba puesta, para poder volver.

        ⚠️ **Medido el 2026-09-04, y era un fallo de verdad:** con `pack_forget()`
        a secas, una franja que se queda sin avisos y luego vuelve a tenerlos **no
        se vuelve a ver nunca** —`pack_forget()` no se deshace solo, al reves que
        `grid_remove()`—. Comprobado en esta maquina:

            nace vacia    -> mapeada: True
            con un aviso  -> mapeada: True
            vaciada       -> mapeada: False
            vuelve a haber-> mapeada: False      ← el aviso existia y no se veia

        En Inicio no saltaba porque sus avisos no van y vienen mientras se teclea.
        En Correccion si: el aviso del mes cruzado aparece y desaparece con cada
        tecla de la fecha de viaje, y con esto puesto vuelve cuando vuelve.
        """
        manera = self.winfo_manager()
        if manera == "pack":
            self._como_estaba_puesta = self.pack_info()
            self.pack_forget()
        elif manera == "grid":
            # `grid_remove` ya recuerda la celda y las opciones: basta con anotar
            # que fue esa la manera para saber por donde devolverla.
            self._como_estaba_puesta = "grid"
            self.grid_remove()

    def _volver_a_la_vista(self):
        """La devuelve donde estaba. No hace nada si nunca se la quito de ahi."""
        if self.winfo_manager() or self._como_estaba_puesta is None:
            return
        if self._como_estaba_puesta == "grid":
            self.grid()
        else:
            self.pack(self._como_estaba_puesta)

    def _quitar_el_detalle(self):
        if self._detalle is not None:
            self._detalle.destroy()
            self._detalle = None

    def _pintar_el_detalle(self):
        """El desplegable, con alto MAXIMO y desplazamiento dentro de si.

        El alto va fijo a `ALTO_MAXIMO_ABIERTA` y el contenido se desplaza dentro.
        Es la regla 3: lo de abajo no se mueve por muchos avisos que haya.
        """
        self._detalle = tk.Frame(self, background=FONDO, height=ALTO_MAXIMO_ABIERTA)
        self._detalle.pack(fill="x")
        self._detalle.pack_propagate(False)

        lienzo = tk.Canvas(
            self._detalle, background=FONDO, highlightthickness=0, takefocus=0,
            height=ALTO_MAXIMO_ABIERTA,
        )
        lienzo.pack(side="left", fill="both", expand=True)
        barra = tk.Scrollbar(self._detalle, orient="vertical", command=lienzo.yview)
        barra.pack(side="right", fill="y")
        lienzo.configure(yscrollcommand=barra.set)

        dentro = tk.Frame(lienzo, background=FONDO)
        lienzo.create_window((0, 0), window=dentro, anchor="nw")
        for aviso in self._avisos:
            self._pintar_un_aviso(dentro, aviso)
        dentro.bind(
            "<Configure>",
            lambda evento: lienzo.configure(scrollregion=lienzo.bbox("all")),
        )
        atar_desplazamiento(lienzo)

    def _pintar_un_aviso(self, padre, aviso):
        """Un renglon del detalle: la frase con su cuenta, y «ver cuáles» si lo hay."""
        fila = tk.Frame(padre, background=FONDO)
        fila.pack(fill="x", padx=8, pady=1)
        tk.Label(
            fila, text=f"• {aviso.en_una_linea()}", background=FONDO,
            foreground=TINTA, font=LETRA_PEQUENA, anchor="w", takefocus=0,
        ).pack(side="left")
        if aviso.al_ver is not None:
            tk.Button(
                fila, text="ver cuáles", command=aviso.al_ver, font=LETRA_PEQUENA,
                background=FONDO, foreground=TINTA, activebackground=FONDO_AL_PASAR,
                relief="solid", borderwidth=1, padx=6, pady=0, cursor="hand2",
            ).pack(side="left", padx=6)
