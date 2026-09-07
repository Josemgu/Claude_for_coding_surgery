"""Que trozo del documento se ve, a que escala, y donde cae el resalte.

Es la aritmetica del visor de la pantalla partida, **sin una sola linea de
tkinter**. Vive aparte del widget por dos motivos, y los dos son medibles:

  - Se puede probar sin abrir una ventana. Las pruebas del encuadre corren en
    milisegundos y dan el mismo numero en cualquier maquina; las de la ventana
    tardan segundos y se omiten donde no hay pantalla.
  - El widget se queda con lo que es de verdad de Tk —pintar, atar teclas— y no
    mezcla el reparto de pixeles con el dibujo, que es donde se esconden los
    fallos de esta clase de pantallas.

**Todo entra en fracciones de la pagina y sale en pixeles.** La banda se guarda en
`procedencia_campo.banda_x0..y1` como cuatro fracciones entre 0 y 1
(`extraccion/recorte.py` lo explica: «una fraccion de la pagina vale a cualquier
escala»). Eso es lo que hace que el rectangulo de resalte **no haya que
recalcularlo al hacer zoom**: se multiplica por la escala del momento y ya esta.
Sin esa decision previa, esta pantalla costaria rehacer la extraccion.

**Solo se pide la region que se ve, nunca la hoja entera.** `region_visible()`
devuelve el recorte en pixeles de la pagina que hay que reescalar, y su tamano lo
manda el panel dividido por la escala. El motivo es un numero: una hoja al tope de
3500 px del lado largo son unos 9,5 MB de PGM, y `tk.PhotoImage` guarda ademas su
copia interna.

⚠️ **Y hay que decir cuanto ahorra de verdad, porque «solo la region visible»
suena a mas de lo que es a poco zoom.** Medido en esta maquina sobre una hoja de
2705 x 3500 px con el panel a 560 x 458:

    zoom por defecto (banda, 25 %)   4 180 589 px   = 44 % de la hoja
    al 300 %                            24 738 px   = 0,3 % de la hoja

A poco zoom la region visible ES casi toda la hoja y no hay mucho que ahorrar; el
ahorro aparece justo donde importa, que es ampliando. Lo que esto garantiza en los
dos casos es que **nunca se reescala mas de lo que cabe en el panel**, y eso es lo
que impide que ampliar al 300 % pida una imagen de 85 Mpx.

Repintar cuesta entre **48 y 105 ms** medidos sobre esa hoja, contando el recorte,
el reescalado, el PGM y el `PhotoImage`.
"""

# Papel que se deja alrededor de lo que se ajusta, a cada lado, en pixeles. Sale
# del mockup: con el panel a 560 px y 12 px de margen, la banda se dibuja a 536.
MARGEN_DEL_VISOR_PX = 12

# Por debajo de este ancho de panel la banda se dibuja mas pequena que los 380 px
# de la tira que este visor sustituye, y entonces el cambio deja de ser una mejora.
# La pantalla lo AVISA en vez de impedirlo: el divisor es de quien lo arrastra.
ANCHO_UTIL_MINIMO_DEL_VISOR_PX = 450

# Los pasos del zoom manual. Fijos y no continuos: con pasos, dos personas que
# dicen «ponlo al 200» ven lo mismo.
PASOS_DE_ZOOM = (0.50, 0.75, 1.00, 1.50, 2.00, 3.00)

# Cuanto se mueve el papel en una pulsacion de desplazamiento: un cuarto del panel.
# Un panel entero pierde la referencia de donde se estaba mirando.
FRACCION_DE_PANEL_POR_DESPLAZAMIENTO = 0.25

MODO_BANDA = "banda"
MODO_PAGINA = "pagina"
MODO_LIBRE = "libre"

# El modo con el que nace el visor desde el 2026-09-03: la hoja llena el panel A LO
# ANCHO y la banda del campo enfocado se trae a la vista sin cambiar la escala.
#
# ⚠️ **El modo de arranque era `banda` y lo cambio el dueno**, con estas palabras:
# «¿Para qué me pones el PDF al lado si no puedo moverme dentro de él? Está muy
# pequeño». Ajustar a la banda encuadra un renglon de tres centimetros y deja fuera
# el resto del papel; a lo ancho se ve la hoja como es, y el renglon del campo sigue
# marcado. `Ctrl+2` sigue llevando a la banda para quien quiera acercarse a leer un
# dato, y `Ctrl+1` a la pagina entera.
MODO_ANCHO = "ancho"


def _area_de_la_banda(banda, ancho_px, alto_px):
    """El ancho y el alto de la banda en pixeles, o None si no hay banda util.

    Una banda de area cero se trata como si no la hubiera. Pasa de verdad:
    `banda_en_fracciones` recorta a los bordes de la pagina, y una banda pegada a
    un margen puede quedar en nada. Media banda no se puede resaltar.
    """
    if banda is None or not ancho_px or not alto_px:
        return None
    ancho = (banda[2] - banda[0]) * ancho_px
    alto = (banda[3] - banda[1]) * alto_px
    return (ancho, alto) if ancho > 0 and alto > 0 else None


class Encuadre:
    """El estado del visor: que hoja se mira, con que zoom y sobre que banda.

    El modo tiene dos capas a proposito: el **pedido** —lo que se eligio con
    Ctrl+1, Ctrl+2 o el zoom— y el **efectivo**, que es el que manda al dibujar.
    Se separan porque un campo sin banda guardada no puede ajustarse a su banda:
    el pedido sigue siendo «banda» y el efectivo cae a «pagina entera». Sin las dos
    capas, tabular a un campo sin banda dejaria el visor en pagina entera para
    siempre, y el campo siguiente —que si tiene banda— ya no se ajustaria.
    """

    def __init__(self, ancho_px, alto_px):
        self.ancho_px = ancho_px or 0
        self.alto_px = alto_px or 0
        self._ancho_panel = 0
        self._alto_panel = 0
        self._banda = None
        self._modo_pedido = MODO_ANCHO
        self._escala_libre = 1.0
        self._centro_libre = (0.5, 0.5)

    # ---- lo que le cuentan desde fuera ----------------------------------

    def fijar_el_tamano_de_la_pagina(self, ancho_px, alto_px):
        """La hoja cambio: otra pagina del PDF, o la primera que se pudo cargar."""
        self.ancho_px = ancho_px or 0
        self.alto_px = alto_px or 0

    def fijar_el_panel(self, ancho, alto):
        """El hueco donde se dibuja. Cambia al arrastrar el divisor y al redimensionar."""
        self._ancho_panel = max(0, int(ancho or 0))
        self._alto_panel = max(0, int(alto or 0))

    def fijar_la_banda(self, banda):
        """La banda del campo que acaba de recibir el foco, o None si no tiene."""
        self._banda = banda if _area_de_la_banda(banda, self.ancho_px, self.alto_px) else None

    # ---- lo que se le pide con el teclado -------------------------------

    def ajustar_a_la_banda(self):
        """Ctrl+2: el zoom por defecto. La banda del campo enfocado llena el panel."""
        self._modo_pedido = MODO_BANDA

    def ajustar_a_la_pagina(self):
        """Ctrl+1: la hoja entera. Sirve para juzgar el papel, no para leer un dato."""
        self._modo_pedido = MODO_PAGINA

    def ajustar_al_ancho(self):
        """El modo de arranque: la hoja llena el panel a lo ancho.

        Se puede volver a el despues de haberse movido, y por eso es un metodo y no
        solo el valor inicial: quien se pierde arrastrando necesita una tecla que le
        devuelva la hoja como estaba.
        """
        self._modo_pedido = MODO_ANCHO

    def acercar(self):
        """Un paso de zoom hacia arriba. Sin nada que dibujar, no hace nada.

        ⚠️ La guarda no es defensiva de adorno: sin ella, ampliar sobre un caso
        cuyo PDF se movio reventaba con `TypeError`, porque `escala` es None
        cuando no hay imagen. Lo cazo
        `prueba_pantalla_partida.ElPdfQueNoEstaNoImpideCorregir`. Un caso que no se
        puede tocar porque falta su papel es un caso que nadie atiende.
        """
        if self.escala is None:
            return
        self._fijar_zoom_libre(self._paso_siguiente(self.escala))

    def alejar(self):
        """Un paso de zoom hacia abajo. El suelo es la pagina entera."""
        if self.escala is None:
            return
        self._fijar_zoom_libre(self._paso_anterior(self.escala))

    def desplazar(self, cuartos_x, cuartos_y):
        """Mueve el papel tantos cuartos de panel. No mueve el foco del campo."""
        escala = self.escala
        if not escala or not self._ancho_panel:
            return
        centro_x, centro_y = self._centro()
        avance_x = self._ancho_panel * FRACCION_DE_PANEL_POR_DESPLAZAMIENTO / escala
        avance_y = self._alto_panel * FRACCION_DE_PANEL_POR_DESPLAZAMIENTO / escala
        self._centro_libre = (
            centro_x + cuartos_x * avance_x / self.ancho_px,
            centro_y + cuartos_y * avance_y / self.alto_px,
        )
        self._escala_libre = escala
        self._modo_pedido = MODO_LIBRE

    def desplazar_en_pixeles(self, dx, dy):
        """Mueve el papel tantos pixeles de PANTALLA. Es el arrastre con el raton.

        Distinto de `desplazar`, que va por cuartos de panel porque lo mueve el
        teclado y una tecla no tiene distancia. El raton si la tiene: el papel se
        mueve exactamente lo que se mueve la mano, que es lo unico que se siente
        como arrastrar y no como empujar.

        Los signos: arrastrar hacia la derecha (`dx` positivo) lleva el papel a la
        derecha, o sea que lo que se ve se desplaza hacia la IZQUIERDA de la hoja.
        Por eso se resta.
        """
        escala = self.escala
        if not escala or not self.ancho_px or not self.alto_px:
            return
        centro_x, centro_y = self._centro()
        self._centro_libre = (
            centro_x - dx / escala / self.ancho_px,
            centro_y - dy / escala / self.alto_px,
        )
        self._escala_libre = escala
        self._modo_pedido = MODO_LIBRE

    def fraccion_del_punto(self, x_panel, y_panel):
        """Que punto de la hoja cae bajo ese pixel del panel, en fracciones.

        Es la cuenta inversa de `region_visible` mas `origen_del_dibujo`, y existe
        para que el zoom pueda hacerse **sobre el cursor** en vez de sobre el centro
        del panel. Devuelve None cuando no hay nada dibujado.
        """
        region = self.region_visible()
        escala = self.escala
        if region is None or not escala or not self.ancho_px or not self.alto_px:
            return None
        origen_x, origen_y = self.origen_del_dibujo()
        return (
            self._acotar((region[0] + (x_panel - origen_x) / escala) / self.ancho_px, 1.0),
            self._acotar((region[1] + (y_panel - origen_y) / escala) / self.alto_px, 1.0),
        )

    def acercar_en(self, x_panel, y_panel):
        """Un paso de zoom hacia arriba dejando quieto lo que hay bajo el cursor."""
        if self.escala is None:
            return
        self._zoom_sobre_el_punto(self._paso_siguiente(self.escala), x_panel, y_panel)

    def alejar_en(self, x_panel, y_panel):
        """Un paso de zoom hacia abajo dejando quieto lo que hay bajo el cursor."""
        if self.escala is None:
            return
        self._zoom_sobre_el_punto(self._paso_anterior(self.escala), x_panel, y_panel)

    def _zoom_sobre_el_punto(self, escala_nueva, x_panel, y_panel):
        """Cambia la escala moviendo el centro para que ese punto no se mueva.

        La cuenta se despeja de `region_visible`, al reves. Alli el borde izquierdo
        sale de `centro * ancho − region / 2`, y el pixel de pantalla de un punto de
        la hoja es `(punto − borde) * escala`. Pidiendo que ese pixel siga siendo el
        mismo y despejando el centro queda:

            centro = (punto + region / 2 − pixel / escala) / ancho

        ⚠️ **Se despeja en vez de acercar el centro «en la misma proporcion», que es
        la formula corta que se ve en todas partes.** Esa vale solo cuando lo
        dibujado llena el panel; con la hoja mas pequena que el hueco —el modo de
        arranque, sin ir mas lejos— `region_visible` acota el borde a cero y
        `origen_del_dibujo` centra lo que sobra, y entonces la formula corta deja el
        punto desplazado. Medido con la pagina de 2700x3500 en el panel de 560x400:
        el punto se iba dos centesimas de hoja, unos 54 px de papel.

        Cuando el punto queda contra un borde, la region se acota y el punto se
        mueve un poco. Eso es correcto: la alternativa seria pedir pixeles que no
        existen.
        """
        punto = self.fraccion_del_punto(x_panel, y_panel)
        if punto is None or not escala_nueva:
            self._fijar_zoom_libre(escala_nueva)
            return
        ancho_region = min(self.ancho_px, self._ancho_panel / escala_nueva)
        alto_region = min(self.alto_px, self._alto_panel / escala_nueva)
        self._centro_libre = (
            (punto[0] * self.ancho_px + ancho_region / 2 - x_panel / escala_nueva)
            / self.ancho_px,
            (punto[1] * self.alto_px + alto_region / 2 - y_panel / escala_nueva)
            / self.alto_px,
        )
        self._escala_libre = escala_nueva
        self._modo_pedido = MODO_LIBRE

    def _fijar_zoom_libre(self, escala):
        """Deja el zoom quieto en esa escala, conservando lo que se estaba mirando.

        Pasa a modo libre a proposito: si siguiera en «banda», el siguiente cambio
        de foco recalcularia la escala y borraria el zoom que se acaba de pedir.
        """
        self._centro_libre = self._centro()
        self._escala_libre = escala
        self._modo_pedido = MODO_LIBRE

    def _paso_siguiente(self, escala):
        for paso in PASOS_DE_ZOOM:
            if paso > escala + 1e-9:
                return paso
        return PASOS_DE_ZOOM[-1]

    def _paso_anterior(self, escala):
        suelo = self._escala_de_la_pagina() or PASOS_DE_ZOOM[0]
        for paso in reversed(PASOS_DE_ZOOM):
            if paso < escala - 1e-9 and paso > suelo:
                return paso
        return suelo

    # ---- lo que sale ----------------------------------------------------

    @property
    def modo(self):
        """El modo EFECTIVO: el que manda al dibujar. Ver la nota de la clase."""
        if self._modo_pedido == MODO_BANDA and self._banda is None:
            return MODO_PAGINA
        return self._modo_pedido

    @property
    def escala(self):
        """Cuantos pixeles de pantalla por pixel de la pagina, ahora mismo."""
        modo = self.modo
        if modo == MODO_LIBRE:
            return self._escala_libre
        if modo == MODO_BANDA:
            return self._escala_de_la_banda()
        if modo == MODO_ANCHO:
            return self._escala_del_ancho()
        return self._escala_de_la_pagina()

    def _escala_del_ancho(self):
        """La que hace caber la hoja de lado a lado. Alta y ancha como el papel.

        Es mayor que la de la pagina entera —el formulario es mas alto que ancho—,
        asi que la hoja se sale por abajo y hay que bajar para ver el resto. Eso es
        exactamente lo que el dueno pidio: el papel grande y moverse dentro.
        """
        if not self._hay_con_que_dibujar():
            return None
        return self._ancho_util() / self.ancho_px

    def _escala_de_la_pagina(self):
        """La que hace caber la hoja ENTERA, alto y ancho. Es tambien el suelo."""
        if not self._hay_con_que_dibujar():
            return None
        return min(
            self._ancho_util() / self.ancho_px, self._alto_util() / self.alto_px
        )

    def _escala_de_la_banda(self):
        """La que hace caber la BANDA entera. Nunca menos que la pagina entera.

        Manda el lado que se quedaria corto —`min` y no `max`— por el mismo motivo
        que `escalar_para_caber` en la tira: la banda tiene que caber entera. Cortar
        por la derecha se lleva el ultimo caracter del valor, que es justo la mitad
        de lo que hay que comprobar.
        """
        de_la_pagina = self._escala_de_la_pagina()
        area = _area_de_la_banda(self._banda, self.ancho_px, self.alto_px)
        if de_la_pagina is None or area is None:
            return de_la_pagina
        ancho_banda, alto_banda = area
        cabe = min(self._ancho_util() / ancho_banda, self._alto_util() / alto_banda)
        return max(de_la_pagina, min(cabe, PASOS_DE_ZOOM[-1]))

    def _hay_con_que_dibujar(self):
        return bool(
            self.ancho_px and self.alto_px and self._ancho_util() and self._alto_util()
        )

    def _ancho_util(self):
        return max(0, self._ancho_panel - 2 * MARGEN_DEL_VISOR_PX)

    def _alto_util(self):
        return max(0, self._alto_panel - 2 * MARGEN_DEL_VISOR_PX)

    def _centro(self):
        """El punto de la pagina que queda en el medio del panel, en fracciones."""
        modo = self.modo
        if modo == MODO_LIBRE:
            return self._centro_libre
        # ⚠️ `MODO_ANCHO` centra en la banda igual que `MODO_BANDA`, y esa es la
        # mitad de lo que el dueno pidio: la hoja se ve grande **y** el renglon del
        # campo enfocado se trae a la vista al tabular. Lo que cambia entre los dos
        # modos es la escala, no a donde se mira.
        if modo in (MODO_BANDA, MODO_ANCHO) and self._banda is not None:
            return (
                (self._banda[0] + self._banda[2]) / 2,
                (self._banda[1] + self._banda[3]) / 2,
            )
        return (0.5, 0.5)

    def region_visible(self):
        """El recorte de la pagina que hay que reescalar, en pixeles enteros.

        Devuelve `(x0, y0, x1, y1)` o None si no hay con que dibujar. **Nunca se
        sale de la hoja**: el centro se acota antes de convertirlo en bordes, asi
        que desplazarse cincuenta veces contra el margen deja la region pegada al
        borde en vez de pedir pixeles que no existen.
        """
        if not self._hay_con_que_dibujar():
            return None
        escala = self.escala
        if not escala:
            return None
        ancho_region = min(self.ancho_px, self._ancho_panel / escala)
        alto_region = min(self.alto_px, self._alto_panel / escala)
        centro_x, centro_y = self._centro()
        x0 = self._acotar(centro_x * self.ancho_px - ancho_region / 2, self.ancho_px - ancho_region)
        y0 = self._acotar(centro_y * self.alto_px - alto_region / 2, self.alto_px - alto_region)
        return (
            int(round(x0)),
            int(round(y0)),
            int(round(x0 + ancho_region)),
            int(round(y0 + alto_region)),
        )

    @staticmethod
    def _acotar(valor, tope):
        return min(max(valor, 0.0), max(tope, 0.0))

    def origen_del_dibujo(self):
        """Donde va la esquina de la imagen dentro del lienzo, en pixeles.

        Cuando lo que se dibuja es mas pequeno que el panel —la hoja entera a poco
        zoom— se centra. Pegado a la esquina quedaria flotando con un hueco al lado
        que parece un defecto, que es el mismo criterio que ya sigue la tira.
        """
        region = self.region_visible()
        if region is None:
            return (0, 0)
        escala = self.escala
        ancho_dibujado = (region[2] - region[0]) * escala
        alto_dibujado = (region[3] - region[1]) * escala
        return (
            max(0, int(round((self._ancho_panel - ancho_dibujado) / 2))),
            max(0, int(round((self._alto_panel - alto_dibujado) / 2))),
        )

    def resalte(self):
        """El rectangulo de la banda en coordenadas del lienzo, o None si no hay banda.

        None NO se dibuja como un rectangulo vacio: quien pinta escribe con
        palabras que ese campo no tiene sitio marcado en la hoja. Un rectangulo que
        no aparece parece un defecto; una frase, no.
        """
        region = self.region_visible()
        if region is None or self._banda is None:
            return None
        escala = self.escala
        origen_x, origen_y = self.origen_del_dibujo()
        return (
            (self._banda[0] * self.ancho_px - region[0]) * escala + origen_x,
            (self._banda[1] * self.alto_px - region[1]) * escala + origen_y,
            (self._banda[2] * self.ancho_px - region[0]) * escala + origen_x,
            (self._banda[3] * self.alto_px - region[1]) * escala + origen_y,
        )

    def ancho_dibujado_de_la_banda(self):
        """A cuantos pixeles se esta viendo la banda. Es lo que el pie anuncia.

        Se anuncia porque es el numero que justifica esta pantalla: la tira que
        sustituye medía 380 px (`extraccion/recorte.ANCHO_DE_LA_TIRA_PX`), y sin
        decirlo no hay forma de saber si el reparto de hoy mejora o empeora.
        """
        area = _area_de_la_banda(self._banda, self.ancho_px, self.alto_px)
        if area is None or not self.escala:
            return None
        return int(round(area[0] * self.escala))

    def el_panel_es_demasiado_estrecho(self):
        """Si a este ancho la banda se ve peor que en la tira que se retiro."""
        return 0 < self._ancho_panel < ANCHO_UTIL_MINIMO_DEL_VISOR_PX
