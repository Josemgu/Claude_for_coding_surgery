"""El documento entero al lado de los campos, con zoom y con el campo resaltado.

Es la mitad izquierda de la pantalla partida, y existe porque el dueno lo pidio
dos veces con estas palabras: «necesito ver el pdf completo a un lado para
confirmar que lo que se leyo es correcto a lo que esta en el documento».

**Sustituye a la tira, no convive con ella,** y es aritmetica y no gusto. La
ventana mide 1100 px de minimo (`interfaz/aplicacion.ANCHO_MINIMO`); la tira mide
380 (`extraccion/recorte.ANCHO_DE_LA_TIRA_PX`), la columna de campos necesita 478
y este visor necesita 450 para ser util. Suman 1308: no caben. Y no se pierde
nada, porque los dos ensenan los mismos pixeles del mismo escaneo; lo que cambia
es cuantos y a que escala. Con el panel a 560 px, la banda del campo enfocado se
dibuja a 536 px contra los 380 de la tira.

**El zoom por defecto es «la hoja a lo ancho», desde el 2026-09-03.** Lo cambio el
dueno: *«¿Para qué me pones el PDF al lado si no puedo moverme dentro de él? Está
muy pequeño… el zoom debe permitirme hacer zoom a cualquier parte del documento y
con el mouse desplazarme y moverme libremente.»* Antes arrancaba ajustado a la
banda del campo enfocado, y eso encuadraba un renglon y escondia el papel. Ahora la
hoja llena el panel de lado a lado, **y la banda del campo enfocado se sigue
resaltando y trayendo a la vista al tabular**: lo que cambio es la escala, no a
donde se mira. `Ctrl+1` ensena la hoja completa para juzgar el papel —si esta
torcida, si hay una segunda firma, si falta un sello—, `Ctrl+2` se acerca a la
banda para leer un dato, y `Ctrl+3` devuelve el ancho.

**El raton se mueve dentro del papel.** Arrastrar con el boton izquierdo desplaza
en cualquier direccion, `Ctrl+rueda` amplia **sobre el cursor** —no sobre el centro
del panel— y el doble clic amplia ahi. Medido con `grep` sobre este archivo el
2026-09-03, antes de tocar nada: habia `<MouseWheel>`, `<Control-MouseWheel>` y
`<Shift-MouseWheel>`, y **ni un solo `<B1-Motion>`**.

**Nada de aqui roba el foco.** El lienzo y los seis botones llevan `takefocus=0`,
igual que la tira que sustituyen. Si hubiera que tabular hasta el visor para
ampliar, cada ampliacion costaria el sitio donde se estaba escribiendo, y volver
son otros tantos Tab. El zoom y el desplazamiento se piden con atajos atados al
toplevel, y por eso funcionan con el cursor dentro de un campo de texto. Medido en
`pruebas/prueba_correccion.py`: el recorrido de Tab tiene que seguir dando **58
paradas**, ni una mas.

**Sin Pillow.** Los bytes viajan en PGM binario crudo hasta `tk.PhotoImage`, que
es lo mismo que ya hacia `interfaz/tira.py` y lo que evita meter una biblioteca de
imagenes de decenas de MB en el ejecutable.
"""

import tkinter as tk
from tkinter import ttk

from extraccion.recorte import ANCHO_DE_LA_TIRA_PX, vista_de_la_region
from interfaz.encuadre import (
    ANCHO_UTIL_MINIMO_DEL_VISOR_PX,
    MODO_ANCHO,
    MODO_BANDA,
    MODO_PAGINA,
    Encuadre,
)
from interfaz.gestos import deltas_del_trackpad
from interfaz.tema import (
    BORDE,
    LETRA_PEQUENA,
    MARINO,
    PAPEL_DEL_ESCANEO,
    TEXTO_SECUNDARIO,
)

# El ancho con el que nace el panel del documento. Con la ventana en su minimo de
# 1100 px deja 532 para la columna de campos, que necesita 478: sobran 54.
ANCHO_POR_DEFECTO_DEL_VISOR_PX = 560

# Lo que el rectangulo de resalte se pinta por encima del escaneo. Dos pixeles,
# como el borde del estado «no valido», para que se distinga en escala de grises.
GROSOR_DEL_RESALTE = 2

SIN_ESCANEO = "no hay imagen de esta hoja"

# Cuanto se ensancha, a cada lado, el trozo de hoja que se reescala y se guarda.
# Un cuarto de panel a cada lado: el trozo mide vez y media el ancho y vez y media
# el alto de lo que se ve, y un arrastre corto se queda dentro sin reescalar nada.
#
# El numero sale de dos mediciones en esta maquina (Tk 9.0.4), no de un gusto.
#
# **Lo que cuesta cada cosa**, con el panel a 560x458: redibujar y refrescar una
# imagen ya construida cuesta ~1 ms **sea del tamano que sea** (0,26 Mpx: 0,9 ms;
# 2,76 Mpx: 1,1 ms), y construir el `tk.PhotoImage` cuesta en proporcion a los
# pixeles: 5,6 ms a 0,26 Mpx, 13,6 a 0,58, 22,9 a 1,03 y **65 ms a 2,76**. O sea que
# lo caro es construir, y ampliar el trozo guardado abarata el total —se construye
# menos veces— pero encarece cada construccion.
#
# **Por que 0,25 y no 0,5.** Arrastrando una pantalla entera, el coste total sale
# casi igual con cualquiera de los dos —(1+2m)²/m vale 9 con 0,25 y 8 con 0,5—,
# pero el PARON de una construccion suelta es cinco veces mayor: 65 ms con 0,5
# contra 13,6 con 0,25. Un tiron de 65 ms se ve; uno de 14 no. Se elige por el peor
# caso y no por el promedio, porque lo que el dueno noto —«se corta, tosco»— es
# justamente el peor caso.
FRACCION_DE_MARGEN_REESCALADO = 0.25


class VisorDelDocumento(ttk.Frame):
    """La hoja del PDF, con su zoom, su desplazamiento y la banda del foco marcada."""

    def __init__(self, padre, paginas, pagina_inicial=1, total_de_hojas=None):
        super().__init__(padre)
        self._paginas = paginas
        self._hoja = pagina_inicial or 1
        self._total_de_hojas = total_de_hojas
        self._hoja_del_campo = self._hoja
        self._etiqueta_del_campo = None
        self._encuadre = Encuadre(0, 0)
        self._imagen = None
        self._panel_medido = (0, 0)
        # De donde arranco el arrastre en curso, o None si no hay ninguno.
        self._desde = None
        # El trozo de hoja que esta reescalado y guardado, y a que escala y de que
        # hoja es. Mientras sirva, arrastrar solo mueve lo que ya esta dibujado.
        self._imagen_dibujada = None
        self._region_reescalada = None
        self._escala_reescalada = None
        self._hoja_reescalada = None
        # Lo ultimo que se escribio en la barra y en el pie, para no volver a
        # escribir lo mismo. Nace en None y no en una tupla vacia: el primer
        # repintado tiene que escribirlo todo.
        self._rotulos_pintados = None

        self.rowconfigure(1, weight=1)
        self.columnconfigure(0, weight=1)
        self._construir_barra()
        self._construir_lienzo()
        self._pie = ttk.Label(self, style="Secundario.TLabel", anchor="w", wraplength=520)
        self._pie.grid(row=2, column=0, sticky="ew", pady=(4, 0))

    # ---- construccion ----------------------------------------------------

    def _boton(self, padre, texto, orden, ancho=None):
        """Un boton de la barra del visor. SIEMPRE con `takefocus=0`.

        No es un detalle de estilo: seis botones en el recorrido de Tab son seis
        paradas mas por caso en una pantalla cuya promesa es un formulario en menos
        de un minuto. Se alcanzan por atajo, que ademas no mueve el foco del campo.
        """
        boton = ttk.Button(padre, text=texto, command=orden, takefocus=0)
        if ancho is not None:
            boton.configure(width=ancho)
        return boton

    def _construir_barra(self):
        """El zoom, los dos modos y el paso de hoja."""
        barra = ttk.Frame(self)
        barra.grid(row=0, column=0, sticky="ew")
        barra.columnconfigure(6, weight=1)

        self._boton(barra, "−", self.alejar, ancho=3).grid(row=0, column=0)
        self._porcentaje = ttk.Label(barra, width=6, anchor="center", font=LETRA_PEQUENA)
        self._porcentaje.grid(row=0, column=1, padx=2)
        self._boton(barra, "+", self.acercar, ancho=3).grid(row=0, column=2)
        self._boton_de_la_banda = self._boton(
            barra, "Banda  Ctrl+2", self.ajustar_a_la_banda
        )
        self._boton_de_la_banda.grid(row=0, column=3, padx=(8, 2))
        self._boton_de_la_pagina = self._boton(
            barra, "Página  Ctrl+1", self.ajustar_a_la_pagina
        )
        self._boton_de_la_pagina.grid(row=0, column=4, padx=2)
        # El tercero entro el 2026-09-03 con el modo de arranque nuevo: sin el, «la
        # hoja a lo ancho» seria el unico encuadre al que no se puede volver.
        self._boton_del_ancho = self._boton(
            barra, "Ancho  Ctrl+3", self.ajustar_al_ancho
        )
        self._boton_del_ancho.grid(row=0, column=5, padx=2)

        self._boton(barra, "◀", self.hoja_anterior, ancho=3).grid(row=0, column=7)
        self._rotulo_de_la_hoja = ttk.Label(barra, style="Secundario.TLabel")
        self._rotulo_de_la_hoja.grid(row=0, column=8, padx=4)
        self._boton(barra, "▶", self.hoja_siguiente, ancho=3).grid(row=0, column=9)

    def _construir_lienzo(self):
        """El hueco donde se pinta el escaneo. Nunca toma el foco."""
        self._lienzo = tk.Canvas(
            self,
            width=ANCHO_POR_DEFECTO_DEL_VISOR_PX,
            highlightthickness=1,
            highlightbackground=BORDE,
            background=PAPEL_DEL_ESCANEO,
            takefocus=0,
            borderwidth=0,
        )
        self._lienzo.grid(row=1, column=0, sticky="nsew", pady=(4, 0))
        self._lienzo.bind("<Configure>", self._al_cambiar_de_tamano)
        # La rueda: sola desplaza, con Control amplia. Devuelven "break" para que
        # no siga hasta el `bind_all` de la pantalla, que desplazaria la lista de
        # campos de la derecha mientras se mira el papel de la izquierda.
        self._lienzo.bind("<MouseWheel>", self._rueda)
        self._lienzo.bind("<Control-MouseWheel>", self._rueda_con_control)
        self._lienzo.bind("<Shift-MouseWheel>", self._rueda_horizontal)
        # ⚠️ El gesto de dos dedos del trackpad NO llega como `<MouseWheel>` en
        # Tk 9, y `Canvas` no trae binding por defecto de `<TouchpadScroll>`.
        # Medido por el dueno en su portatil: «le doy para abajo con el trackpad de
        # mi laptop y no baja». Ver `interfaz/gestos.py`.
        self._lienzo.bind("<TouchpadScroll>", self._trackpad)
        # El raton dentro del papel, que es lo que faltaba entero. Palabras del
        # dueno el 2026-09-03: «¿para qué me pones el PDF al lado si no puedo
        # moverme dentro de él?». Medido con `grep` sobre este archivo ese dia: no
        # habia ni un `<B1-Motion>`.
        self._lienzo.bind("<ButtonPress-1>", self._empezar_a_arrastrar)
        self._lienzo.bind("<B1-Motion>", self._arrastrar)
        self._lienzo.bind("<ButtonRelease-1>", self._soltar)
        self._lienzo.bind("<Double-Button-1>", self._doble_clic)
        # ⚠️ Ninguno de estos cinco pide el foco, y no es un descuido: `tk.Canvas`
        # no lo toma al pulsar a menos que se lo pidan, y este ademas lleva
        # `takefocus=0`. Pulsar el papel para moverlo NO puede sacar el cursor del
        # campo donde se estaba escribiendo, que es la promesa de esta pantalla.

    # ---- lo que le pide la pantalla --------------------------------------

    def enfocar(self, pagina_pdf, banda, etiqueta_del_campo=None):
        """Ensena la hoja de ese campo y marca su banda. Es lo que llama el foco.

        La hoja se cambia sola, y eso es la mitad del valor: un caso de doce
        personas ocupa seis hojas, y tabular a alguien de la cuarta tiene que
        ensenar la cuarta. `PaginasDelPdf` ya cachea la rasterizacion, asi que ir y
        volver entre hojas no vuelve a pagar el paso caro.
        """
        self._hoja_del_campo = pagina_pdf or self._hoja
        self._etiqueta_del_campo = etiqueta_del_campo
        self._ir_a_la_hoja(self._hoja_del_campo)
        self._encuadre.fijar_la_banda(banda)
        self.repintar()

    def ajustar_a_la_banda(self):
        self._encuadre.ajustar_a_la_banda()
        self.repintar()

    def ajustar_a_la_pagina(self):
        self._encuadre.ajustar_a_la_pagina()
        self.repintar()

    def acercar(self):
        self._encuadre.acercar()
        self.repintar()

    def alejar(self):
        self._encuadre.alejar()
        self.repintar()

    def desplazar(self, cuartos_x, cuartos_y):
        """Mueve el papel. **No mueve el foco del campo**, que es todo el punto."""
        self._encuadre.desplazar(cuartos_x, cuartos_y)
        self.repintar()

    def arrastrar(self, dx, dy):
        """Mueve el papel tantos pixeles de pantalla. Es el arrastre con el raton."""
        self._encuadre.desplazar_en_pixeles(dx, dy)
        self.repintar()

    def acercar_en(self, x, y):
        """Amplia un paso dejando quieto lo que hay bajo ese punto del panel."""
        self._encuadre.acercar_en(x, y)
        self.repintar()

    def alejar_en(self, x, y):
        """Reduce un paso dejando quieto lo que hay bajo ese punto del panel."""
        self._encuadre.alejar_en(x, y)
        self.repintar()

    def ajustar_al_ancho(self):
        """Devuelve la hoja al ancho del panel, que es como nace el visor."""
        self._encuadre.ajustar_al_ancho()
        self.repintar()

    def hoja_anterior(self):
        self._ir_a_la_hoja(self._hoja - 1)
        self.repintar()

    def hoja_siguiente(self):
        self._ir_a_la_hoja(self._hoja + 1)
        self.repintar()

    def fijar_el_total_de_hojas(self, total):
        """Cuantas hojas tiene el PDF, cuando por fin se sabe.

        Nace en None y llega despues a proposito: contarlas cuesta abrir
        `pypdfium2`, que la primera vez son 4,5 s medidos, y ese precio no puede
        estar entre el clic y el caso en pantalla. Hasta que llega, el contador
        dice «hoja 1» sin denominador y las flechas de hoja funcionan igual.
        """
        self._total_de_hojas = total
        self._pintar_los_rotulos()

    @property
    def hoja(self):
        """Que hoja del PDF se esta viendo. La pantalla y las pruebas preguntan."""
        return self._hoja

    @property
    def encuadre(self):
        """El estado del zoom, para poder medirlo sin desmontar el dibujo."""
        return self._encuadre

    # ---- el estado interno ----------------------------------------------

    def _ir_a_la_hoja(self, hoja):
        """Cambia de hoja acotando a lo que el PDF tiene, y reencuadra sobre ella.

        Se acota por abajo a 1 siempre; por arriba solo cuando se sabe cuantas hay.
        Cuando no se sabe —el PDF no se pudo abrir— se deja pasar y la hoja que no
        existe se dibuja como el hueco rayado con su motivo, que es lo cierto.
        """
        hoja = max(1, hoja)
        if self._total_de_hojas:
            hoja = min(hoja, self._total_de_hojas)
        if hoja == self._hoja and self._imagen is not None:
            return
        self._hoja = hoja
        self._imagen = self._paginas.pagina(hoja).imagen()
        alto, ancho = (self._imagen.shape[0], self._imagen.shape[1]) if self._imagen is not None else (0, 0)
        self._encuadre.fijar_el_tamano_de_la_pagina(ancho, alto)

    def _al_cambiar_de_tamano(self, evento):
        """Repinta cuando el hueco cambia de tamano, y solo entonces.

        La guarda no es una optimizacion: repintar dentro del propio `<Configure>`
        puede volver a disparar `<Configure>`, y sin comparar el tamano eso no
        termina.
        """
        medida = (evento.width, evento.height)
        if medida == self._panel_medido:
            return
        self._panel_medido = medida
        self.repintar()

    def _rueda(self, evento):
        self.desplazar(0, -1 if evento.delta > 0 else 1)
        return "break"

    def _rueda_con_control(self, evento):
        """Ctrl+rueda amplia o reduce **sobre el cursor**, no sobre el centro.

        Ampliar sobre el centro obliga a ampliar, buscar lo que se queria, arrastrar
        hasta ahi, y repetir. Sobre el cursor se pone el raton encima de lo que hay
        que leer y se gira la rueda: es la diferencia entre mirar el papel y pelearse
        con el.
        """
        self.acercar_en(evento.x, evento.y) if evento.delta > 0 else self.alejar_en(
            evento.x, evento.y
        )
        return "break"

    def _rueda_horizontal(self, evento):
        self.desplazar(-1 if evento.delta > 0 else 1, 0)
        return "break"

    def _trackpad(self, evento):
        """Dos dedos sobre el papel lo mueven en los dos ejes a la vez.

        A diferencia de la columna de campos, aqui **se atienden TODOS los eventos y
        no uno de cada cinco**: el papel se mueve por pixeles y no por renglones, y
        tirar cuatro de cada cinco deltas se comeria cuatro quintos del gesto. Es
        barato: con la hoja ya reescalada guardada, un movimiento cuesta ~1 ms.
        """
        dx, dy = deltas_del_trackpad(self._lienzo, evento.delta)
        if dx or dy:
            self.arrastrar(dx, dy)
        return "break"

    def _empezar_a_arrastrar(self, evento):
        """Guarda de donde arranca el arrastre. NO pide el foco."""
        self._desde = (evento.x, evento.y)
        self._lienzo.configure(cursor="fleur")
        return "break"

    def _arrastrar(self, evento):
        """Mueve el papel exactamente lo que se ha movido la mano."""
        if self._desde is None:
            return "break"
        desde_x, desde_y = self._desde
        self._desde = (evento.x, evento.y)
        self.arrastrar(evento.x - desde_x, evento.y - desde_y)
        return "break"

    def _soltar(self, evento):
        self._desde = None
        self._lienzo.configure(cursor="")
        return "break"

    def _doble_clic(self, evento):
        """Doble clic amplia AHI, que es el gesto que todo el mundo prueba primero."""
        self.acercar_en(evento.x, evento.y)
        return "break"

    # ---- pintar ----------------------------------------------------------

    def repintar(self):
        """Vuelve a dibujar el escaneo, el resalte y los rotulos. Sin tocar el foco."""
        ancho = self._lienzo.winfo_width()
        alto = self._lienzo.winfo_height()
        self._encuadre.fijar_el_panel(ancho, alto)
        self._lienzo.delete("all")
        if self._imagen is None:
            self._olvidar_lo_reescalado()
            self._dibujar_hueco(self._motivo_del_hueco())
        else:
            self._dibujar_el_escaneo()
            self._dibujar_el_resalte()
        self._pintar_los_rotulos()

    def _olvidar_lo_reescalado(self):
        """Tira la hoja reescalada que estaba guardada. La siguiente se rehace."""
        self._imagen_dibujada = None
        self._region_reescalada = None
        self._escala_reescalada = None
        self._hoja_reescalada = None

    def _sirve_lo_reescalado(self, region):
        """Si lo que ya esta reescalado cubre esa region a esta escala.

        Las tres condiciones son las tres formas de invalidarlo: cambiar de hoja,
        cambiar de zoom, o salirse del trozo que se reescalo. Mientras ninguna
        pase —y arrastrando no pasa casi nunca, porque el trozo se corta con
        margen— dibujar cuesta mover una imagen que ya existe.
        """
        if self._imagen_dibujada is None or self._region_reescalada is None:
            return False
        if self._hoja_reescalada != self._hoja:
            return False
        if self._escala_reescalada != self._encuadre.escala:
            return False
        guardada = self._region_reescalada
        return (
            region[0] >= guardada[0]
            and region[1] >= guardada[1]
            and region[2] <= guardada[2]
            and region[3] <= guardada[3]
        )

    def _region_con_margen(self, region):
        """La region visible ensanchada, sin salirse de la hoja.

        El margen es lo que hace que arrastrar no cueste nada: con medio panel de
        sobra a cada lado, un arrastre normal se queda dentro de lo que ya esta
        reescalado y solo hay que mover la imagen. Se paga en pixeles reescalados
        —el doble de ancho y el doble de alto, o sea cuatro veces el area— y eso es
        barato: el reescalado medido cuesta 0,7 ms, y lo caro es construir el
        `PhotoImage`, que con esto se hace una vez cada muchos arrastres.
        """
        alto_de_la_hoja, ancho_de_la_hoja = self._imagen.shape[0], self._imagen.shape[1]
        escala = self._encuadre.escala or 1
        margen_x = self._lienzo.winfo_width() * FRACCION_DE_MARGEN_REESCALADO / escala
        margen_y = self._lienzo.winfo_height() * FRACCION_DE_MARGEN_REESCALADO / escala
        return (
            int(max(0, region[0] - margen_x)),
            int(max(0, region[1] - margen_y)),
            int(min(ancho_de_la_hoja, region[2] + margen_x)),
            int(min(alto_de_la_hoja, region[3] + margen_y)),
        )

    def _dibujar_el_escaneo(self):
        """Pinta el trozo reescalado que ya hay, o corta uno nuevo si no sirve.

        ⚠️ **Esto existe porque el dueno lo midio a mano: «lento, se corta, tosco al
        moverlo».** Medido en esta maquina antes de tocarlo, con una hoja de
        2700x3500 en un panel de 560x458: **61 ms por arrastre a la escala de
        arranque y 77 ms al 300 %**, y de esos 27 se iban en `_dibujar_el_escaneo`
        —casi todo en construir y destruir un `tk.PhotoImage` nuevo por cada
        movimiento del raton—. A 60 movimientos por segundo no hay presupuesto.

        Lo que **no** se hace, y es lo que `interfaz/encuadre.py` lleva escrito desde
        el primer dia: reescalar la hoja entera. Al 300 % serian 8100x10500 px, 85
        Mpx. Lo que se guarda es el trozo visible **con margen**, que a cualquier
        zoom mide lo mismo: unas dos pantallas de ancho por dos de alto.
        """
        region = self._encuadre.region_visible()
        if region is None:
            self._olvidar_lo_reescalado()
            self._dibujar_hueco(SIN_ESCANEO)
            return
        if self._sirve_lo_reescalado(region):
            self._colocar_lo_reescalado(region)
            return
        self._reescalar_y_dibujar(self._region_con_margen(region), region)

    def _colocar_lo_reescalado(self, region):
        """Pone la imagen que ya existe en el sitio que le toca ahora.

        Es el camino barato del arrastre: no se corta, no se reescala y no se
        construye ningun `PhotoImage`. Solo se calcula el desplazamiento.
        """
        origen_x, origen_y = self._encuadre.origen_del_dibujo()
        escala = self._encuadre.escala
        guardada = self._region_reescalada
        self._lienzo.create_image(
            origen_x - (region[0] - guardada[0]) * escala,
            origen_y - (region[1] - guardada[1]) * escala,
            anchor="nw",
            image=self._imagen_dibujada,
        )

    def _reescalar_y_dibujar(self, region_amplia, region):
        """Corta y reescala el trozo con margen, lo guarda, y lo coloca."""
        bytes_pgm = vista_de_la_region(
            self._imagen, region_amplia, self._encuadre.escala
        )
        if bytes_pgm is None:
            self._olvidar_lo_reescalado()
            self._dibujar_hueco(SIN_ESCANEO)
            return
        try:
            # La referencia se guarda en el objeto: `PhotoImage` no queda
            # referenciada por el lienzo, y sin esto el recolector se la lleva y la
            # hoja aparece en blanco. Es la trampa clasica de Tk, la misma que
            # `interfaz/tira.py` documenta.
            self._imagen_dibujada = tk.PhotoImage(data=bytes_pgm, master=self._lienzo)
        except tk.TclError:
            self._olvidar_lo_reescalado()
            self._dibujar_hueco("la imagen de esta hoja no se pudo dibujar")
            return
        self._region_reescalada = region_amplia
        self._escala_reescalada = self._encuadre.escala
        self._hoja_reescalada = self._hoja
        self._colocar_lo_reescalado(region)

    def _dibujar_el_resalte(self):
        """El rectangulo marino sobre la banda del campo enfocado, si la tiene."""
        resalte = self._encuadre.resalte()
        if resalte is None:
            return
        x0, y0, x1, y1 = resalte
        self._lienzo.create_rectangle(
            x0, y0, x1, y1, outline=MARINO, width=GROSOR_DEL_RESALTE
        )

    def _motivo_del_hueco(self):
        """Por que no hay escaneo que ensenar, con palabras y no en blanco."""
        return self._paginas.motivo_del_fallo or (
            f"no se pudo cargar la hoja {self._hoja} de este PDF"
        )

    def _dibujar_hueco(self, motivo):
        """El rectangulo rayado con el motivo escrito dentro.

        Es el mismo dibujo que `TiraDelEscaneo._dibujar_hueco` ya usaba, y por el
        mismo motivo: un lienzo en blanco parece una hoja escaneada vacia —«el
        papel no tenia nada»— y no lo es. Son dos cosas distintas y se ven
        distintas. Los campos siguen siendo editables: un caso que no se abre
        porque falta una imagen es un caso que nadie atiende.
        """
        ancho = max(1, self._lienzo.winfo_width())
        alto = max(1, self._lienzo.winfo_height())
        for desplazamiento in range(-alto, ancho, 16):
            self._lienzo.create_line(
                desplazamiento, alto, desplazamiento + alto, 0, fill=BORDE, width=1
            )
        self._lienzo.create_rectangle(
            10, alto / 2 - 22, ancho - 10, alto / 2 + 22, fill="#FFFFFF", outline=BORDE
        )
        self._lienzo.create_text(
            ancho / 2, alto / 2, text=motivo, font=LETRA_PEQUENA,
            fill=TEXTO_SECUNDARIO, width=ancho - 30,
        )

    def _pintar_los_rotulos(self):
        """El porcentaje, el contador de hojas, el modo activo y el pie.

        ⚠️ **Solo se escribe lo que ha cambiado**, y no es un pulido: medido con
        `cProfile` sobre 40 arrastres seguidos, estas cuatro llamadas a `configure`
        costaban **2,4 de los 3,1 ms de cada arrastre**, y arrastrando no cambia
        ninguna de las cuatro —ni el zoom, ni la hoja, ni el modo, ni el pie—. Un
        `configure` de Tk cuesta lo mismo escriba algo distinto o lo mismo.
        """
        escala = self._encuadre.escala
        total = f" / {self._total_de_hojas}" if self._total_de_hojas else ""
        modo = self._encuadre.modo
        rotulos = (
            f"{escala * 100:.0f} %" if escala else "—",
            f"hoja {self._hoja}{total}",
            modo,
            self.texto_del_pie(),
        )
        if rotulos == self._rotulos_pintados:
            return
        self._rotulos_pintados = rotulos
        self._porcentaje.configure(text=rotulos[0])
        self._rotulo_de_la_hoja.configure(text=rotulos[1])
        self._boton_de_la_banda.state(["disabled"] if modo == MODO_BANDA else ["!disabled"])
        self._boton_de_la_pagina.state(["disabled"] if modo == MODO_PAGINA else ["!disabled"])
        self._boton_del_ancho.state(["disabled"] if modo == MODO_ANCHO else ["!disabled"])
        self._pie.configure(text=rotulos[3])

    def texto_del_pie(self):
        """Lo que el visor dice de si mismo, en espanol y con numeros.

        Se devuelve el texto en vez de solo pintarlo para poder comprobarlo palabra
        por palabra sin desmontar el dibujo, que es el mismo criterio que ya siguen
        `_aviso_de_la_pagina` y `_pregunta_de_todo_correcto`.

        Las cuatro cosas que tiene que poder decir salen de casos que pasan de
        verdad, no de adornar: que hoja se ve contra la del campo enfocado, a
        cuantos pixeles se dibuja la banda contra los 380 de la tira que se retiro,
        que un campo no tiene sitio marcado en la hoja, y que el divisor se ha
        arrastrado por debajo del ancho en el que esto empeora.
        """
        partes = []
        if self._imagen is None:
            return self._motivo_del_hueco()
        if self._hoja != self._hoja_del_campo:
            partes.append(
                f"Está viendo la hoja {self._hoja}; el campo enfocado está en la "
                f"{self._hoja_del_campo}"
            )
        ancho_de_la_banda = self._encuadre.ancho_dibujado_de_la_banda()
        if ancho_de_la_banda is None:
            partes.append(
                "Este campo no tiene sitio marcado en la hoja, así que no se "
                "resalta nada: mire la página entera"
            )
        else:
            donde = f" de «{self._etiqueta_del_campo}»" if self._etiqueta_del_campo else ""
            partes.append(
                f"Banda{donde} dibujada a {ancho_de_la_banda} px "
                f"(la tira medía {ANCHO_DE_LA_TIRA_PX})"
            )
        if self._encuadre.el_panel_es_demasiado_estrecho():
            partes.append(
                f"Por debajo de {ANCHO_UTIL_MINIMO_DEL_VISOR_PX} px de ancho la "
                "banda se ve más pequeña que la tira que sustituye: arrastre el "
                "divisor a la derecha"
            )
        partes.append(
            "Arrastre con el ratón para moverse · Ctrl+rueda o doble clic amplía "
            "donde apunta · Ctrl+Mayús+flechas desplaza sin salir del campo"
        )
        return " · ".join(partes)
