"""La aritmetica del visor del documento: que trozo se ve y a que escala.

**Sin tkinter, a proposito.** Todo lo que decide lo que se ve —la escala, el
recorte visible, donde cae el rectangulo de resalte— es aritmetica sobre el tamano
de la pagina y el del panel. Sacarla a `interfaz/encuadre.py` es lo que permite
medirla sin abrir una ventana, y sin ventana estas pruebas corren en milisegundos
y dan el mismo numero en cualquier maquina. Lo que si necesita ventana se prueba
en `pruebas/prueba_pantalla_partida.py`.

Los criterios de los que salen estas pruebas, en Given/When/Then. Vienen del
pase, de las palabras del dueno del 2026-09-03 y de
`mockups/mockup-correccion-partida.html`, no de leer el codigo:

  DADO un caso recien abierto,
  CUANDO se dibuja el visor,
  ENTONCES la hoja llena el panel A LO ANCHO —«está muy pequeño», dijo el dueno—,
  Y la banda del campo enfocado se sigue resaltando y trayendo a la vista.

  DADO un campo con banda guardada,
  CUANDO se pide «banda» (Ctrl+2),
  ENTONCES la banda se dibuja MAS ancha que los 380 px que medía la tira que
  sustituye —536 px con el panel a 560, que es el numero del mockup—,
  Y el rectangulo de resalte cae sobre esa banda y dentro del panel.

  DADO el visor a cualquier zoom,
  CUANDO se pide «pagina entera» (Ctrl+1),
  ENTONCES la pagina completa cabe en el panel, alto y ancho.

  DADO un zoom que no cabe en el panel,
  CUANDO se desplaza —con el teclado o arrastrando con el raton—,
  ENTONCES la region visible se mueve y NUNCA se sale de la pagina.

  DADO un punto cualquiera de la hoja bajo el cursor,
  CUANDO se amplia con Ctrl+rueda o con doble clic,
  ENTONCES ese punto se queda debajo del cursor.

  DADO un campo SIN banda guardada,
  CUANDO recibe el foco,
  ENTONCES no se resalta nada — un rectangulo que no aparece parece un defecto, y
  esto es lo que deja decirlo con palabras.
"""

import unittest

from interfaz.encuadre import (
    MARGEN_DEL_VISOR_PX,
    MODO_ANCHO,
    MODO_BANDA,
    MODO_LIBRE,
    MODO_PAGINA,
    PASOS_DE_ZOOM,
    Encuadre,
)

# Una pagina vertical del tamano al que este proyecto rasteriza: el tope del lado
# largo son 3500 px (`extraccion/rasterizado.py`), y una carta a esa escala queda
# cerca de 2700 x 3500.
ANCHO_DE_LA_PAGINA = 2700
ALTO_DE_LA_PAGINA = 3500

# El panel izquierdo del mockup: 560 px de ancho dentro de la ventana de 1100.
ANCHO_DEL_PANEL = 560
ALTO_DEL_PANEL = 400

# Una banda como las que guarda la importacion: la fila del numero de unidad,
# ancha y baja. En fracciones de la pagina, que es como viven en
# `procedencia_campo.banda_x0..y1`.
BANDA_DE_LA_UNIDAD = (0.05, 0.20, 0.85, 0.235)


def _encuadre(banda=BANDA_DE_LA_UNIDAD, ancho_panel=ANCHO_DEL_PANEL):
    encuadre = Encuadre(ANCHO_DE_LA_PAGINA, ALTO_DE_LA_PAGINA)
    encuadre.fijar_el_panel(ancho_panel, ALTO_DEL_PANEL)
    encuadre.fijar_la_banda(banda)
    return encuadre


class LaEscalaPorDefectoEsLaHojaALoAncho(unittest.TestCase):
    """El zoom de arranque, cambiado por el dueno el 2026-09-03.

    Sus palabras: *«¿Para qué me pones el PDF al lado si no puedo moverme dentro de
    él? Está muy pequeño»*. Hasta ese dia el visor arrancaba ajustado a la banda del
    campo enfocado, que encuadra un renglon y esconde el papel. El criterio nuevo:

        DADO un caso recien abierto,
        CUANDO se dibuja el visor,
        ENTONCES la hoja llena el panel de lado a lado,
        Y la banda del campo enfocado se sigue resaltando y trayendo a la vista.
    """

    def test_el_modo_de_arranque_es_la_hoja_a_lo_ancho(self):
        self.assertEqual(_encuadre().modo, MODO_ANCHO)

    def test_la_hoja_llena_el_panel_de_lado_a_lado(self):
        """560 de panel − 12 de margen a cada lado = 536 px de hoja dibujados."""
        encuadre = _encuadre()
        self.assertAlmostEqual(
            ANCHO_DE_LA_PAGINA * encuadre.escala,
            ANCHO_DEL_PANEL - 2 * MARGEN_DEL_VISOR_PX,
            places=6,
        )

    def test_a_lo_ancho_la_hoja_se_sale_por_abajo_y_hay_donde_moverse(self):
        """El formulario es mas alto que ancho: a lo ancho no cabe entero.

        Es lo que el dueno pidio —el papel grande y moverse dentro— y lo que
        distingue este modo de «pagina entera».
        """
        encuadre = _encuadre()
        _, y0, _, y1 = encuadre.region_visible()
        self.assertLess(y1 - y0, ALTO_DE_LA_PAGINA)

    def test_la_banda_del_campo_enfocado_se_sigue_resaltando(self):
        x0, y0, x1, y1 = _encuadre().resalte()
        self.assertGreaterEqual(x0, 0)
        self.assertGreaterEqual(y0, 0)
        self.assertLessEqual(x1, ANCHO_DEL_PANEL)
        self.assertLessEqual(y1, ALTO_DEL_PANEL)

    def test_tabular_a_una_banda_de_abajo_la_trae_a_la_vista(self):
        """La escala no cambia, pero la hoja se desplaza hasta el renglon del campo."""
        encuadre = _encuadre()
        arriba_de_la_primera = encuadre.region_visible()[1]
        encuadre.fijar_la_banda((0.05, 0.80, 0.85, 0.835))
        self.assertGreater(encuadre.region_visible()[1], arriba_de_la_primera)
        self.assertEqual(encuadre.modo, MODO_ANCHO)
        _, y0, _, y1 = encuadre.resalte()
        self.assertGreaterEqual(y0, 0)
        self.assertLessEqual(y1, ALTO_DEL_PANEL)

    def test_la_banda_se_ve_mas_grande_que_la_tira_que_sustituye(self):
        """Si la banda saliera mas pequena que los 380 px de `ANCHO_DE_LA_TIRA_PX`,
        este cambio seria una perdida y no una mejora. Tambien a lo ancho."""
        from extraccion.recorte import ANCHO_DE_LA_TIRA_PX

        self.assertGreater(_encuadre().ancho_dibujado_de_la_banda(), ANCHO_DE_LA_TIRA_PX)


class AjustarALaBandaSigueEstandoEnCtrl2(unittest.TestCase):
    """Lo que era el arranque ahora es una tecla: `Ctrl+2` para leer un dato."""

    def test_la_banda_se_dibuja_a_los_536_px_que_dice_el_mockup(self):
        """560 de panel − 12 de margen a cada lado = 536. Es el numero, no un ideal."""
        encuadre = _encuadre()
        encuadre.ajustar_a_la_banda()
        self.assertEqual(
            encuadre.ancho_dibujado_de_la_banda(),
            ANCHO_DEL_PANEL - 2 * MARGEN_DEL_VISOR_PX,
        )

    def test_una_banda_alta_manda_por_el_alto_y_no_se_corta(self):
        """Con una banda mas alta que ancha, la escala la fija el alto del panel.

        Manda el lado que se quedaria corto: la banda tiene que caber ENTERA, que
        es lo mismo que hace `escalar_para_caber` con la tira.
        """
        encuadre = _encuadre(banda=(0.40, 0.10, 0.50, 0.90))
        encuadre.ajustar_a_la_banda()
        _, y0, _, y1 = encuadre.resalte()
        self.assertGreaterEqual(y0, 0)
        self.assertLessEqual(y1, ALTO_DEL_PANEL)

    def test_ajustar_al_ancho_devuelve_el_encuadre_de_arranque(self):
        """Sin esta vuelta, el modo por defecto seria el unico irrecuperable."""
        encuadre = _encuadre()
        de_arranque = encuadre.escala
        encuadre.ajustar_a_la_banda()
        encuadre.ajustar_al_ancho()
        self.assertEqual(encuadre.modo, MODO_ANCHO)
        self.assertEqual(encuadre.escala, de_arranque)


class SinBandaNoSeInventaUnResalte(unittest.TestCase):
    """Un campo sin banda guardada no se resalta, y se dice con palabras."""

    def test_sin_banda_no_hay_rectangulo(self):
        self.assertIsNone(_encuadre(banda=None).resalte())

    def test_sin_banda_el_modo_de_arranque_sigue_siendo_el_ancho(self):
        """A lo ancho no hace falta banda: se ve la hoja igual, centrada."""
        self.assertEqual(_encuadre(banda=None).modo, MODO_ANCHO)

    def test_pedir_la_banda_sin_tenerla_cae_a_la_pagina_entera(self):
        """El modo pedido sigue siendo «banda»; el efectivo cae a la pagina."""
        encuadre = _encuadre(banda=None)
        encuadre.ajustar_a_la_banda()
        self.assertEqual(encuadre.modo, MODO_PAGINA)

    def test_sin_banda_no_hay_ancho_dibujado_que_anunciar(self):
        self.assertIsNone(_encuadre(banda=None).ancho_dibujado_de_la_banda())

    def test_una_banda_de_area_cero_se_trata_como_si_no_hubiera(self):
        """`banda_en_fracciones` ya recorta a los bordes; lo que queda puede ser nada."""
        encuadre = _encuadre(banda=(0.5, 0.5, 0.5, 0.5))
        encuadre.ajustar_a_la_banda()
        self.assertEqual(encuadre.modo, MODO_PAGINA)
        self.assertIsNone(encuadre.resalte())


class LaPaginaEnteraCabeEntera(unittest.TestCase):
    """Ctrl+1: la hoja completa, para juzgar el papel y no para leer un dato."""

    def test_la_pagina_entera_cabe_de_alto_y_de_ancho(self):
        encuadre = _encuadre()
        encuadre.ajustar_a_la_pagina()
        x0, y0, x1, y1 = encuadre.region_visible()
        self.assertEqual((x0, y0), (0, 0))
        self.assertEqual((x1, y1), (ANCHO_DE_LA_PAGINA, ALTO_DE_LA_PAGINA))
        self.assertLessEqual(ANCHO_DE_LA_PAGINA * encuadre.escala, ANCHO_DEL_PANEL)
        self.assertLessEqual(ALTO_DE_LA_PAGINA * encuadre.escala, ALTO_DEL_PANEL)

    def test_a_pagina_entera_la_banda_se_ve_mas_pequena_que_la_tira(self):
        """Es justo el motivo por el que «pagina entera» NO es el zoom por defecto."""
        from extraccion.recorte import ANCHO_DE_LA_TIRA_PX

        encuadre = _encuadre()
        encuadre.ajustar_a_la_pagina()
        self.assertLess(encuadre.ancho_dibujado_de_la_banda(), ANCHO_DE_LA_TIRA_PX)

    def test_a_pagina_entera_el_resalte_sigue_marcando_la_banda(self):
        """Se ve pequeno, pero se ve: es lo que dice DONDE mirar en la hoja."""
        encuadre = _encuadre()
        encuadre.ajustar_a_la_pagina()
        self.assertIsNotNone(encuadre.resalte())

    def test_volver_a_la_banda_recupera_la_escala_de_antes(self):
        encuadre = _encuadre()
        encuadre.ajustar_a_la_banda()
        antes = encuadre.escala
        encuadre.ajustar_a_la_pagina()
        encuadre.ajustar_a_la_banda()
        self.assertEqual(encuadre.escala, antes)
        self.assertEqual(encuadre.modo, MODO_BANDA)


class ElZoomVaPorPasosFijos(unittest.TestCase):
    """Con pasos, dos personas que dicen «ponlo al 200» ven lo mismo."""

    def test_acercar_sube_al_paso_siguiente(self):
        encuadre = _encuadre()
        encuadre.ajustar_a_la_pagina()
        encuadre.acercar()
        self.assertIn(encuadre.escala, PASOS_DE_ZOOM)

    def test_acercar_siempre_amplia(self):
        encuadre = _encuadre()
        antes = encuadre.escala
        encuadre.acercar()
        self.assertGreater(encuadre.escala, antes)

    def test_alejar_siempre_reduce(self):
        encuadre = _encuadre()
        encuadre.acercar()
        antes = encuadre.escala
        encuadre.alejar()
        self.assertLess(encuadre.escala, antes)

    def test_el_zoom_tiene_tope_por_arriba(self):
        """Sin tope, veinte pulsaciones piden una imagen que no cabe en memoria."""
        encuadre = _encuadre()
        for _ in range(20):
            encuadre.acercar()
        self.assertEqual(encuadre.escala, max(PASOS_DE_ZOOM))

    def test_el_zoom_tiene_suelo_por_abajo_y_es_la_pagina_entera(self):
        """Alejarse mas que la hoja entera solo ensena marco vacio."""
        encuadre = _encuadre()
        for _ in range(20):
            encuadre.alejar()
        self.assertGreaterEqual(
            ANCHO_DE_LA_PAGINA * encuadre.escala + 1, 0
        )  # no revienta
        self.assertLessEqual(ANCHO_DE_LA_PAGINA * encuadre.escala, ANCHO_DEL_PANEL + 1)

    def test_tocar_el_zoom_saca_del_modo_banda(self):
        """Si siguiera en «banda», el siguiente cambio de foco borraria el zoom."""
        encuadre = _encuadre()
        encuadre.acercar()
        self.assertEqual(encuadre.modo, MODO_LIBRE)


class ElDesplazamientoNoSeSaleDeLaHoja(unittest.TestCase):
    """Ctrl+Mayus+flechas mueve el papel un cuarto de panel, sin pasarse."""

    def _ampliado(self):
        encuadre = _encuadre()
        for _ in range(6):
            encuadre.acercar()
        return encuadre

    def test_desplazar_a_la_derecha_mueve_la_region(self):
        encuadre = self._ampliado()
        antes = encuadre.region_visible()[0]
        encuadre.desplazar(1, 0)
        self.assertGreater(encuadre.region_visible()[0], antes)

    def test_desplazar_hacia_abajo_mueve_la_region(self):
        encuadre = self._ampliado()
        antes = encuadre.region_visible()[1]
        encuadre.desplazar(0, 1)
        self.assertGreater(encuadre.region_visible()[1], antes)

    def test_por_mucho_que_se_desplace_nunca_se_sale_de_la_pagina(self):
        encuadre = self._ampliado()
        for _ in range(60):
            encuadre.desplazar(1, 1)
        x0, y0, x1, y1 = encuadre.region_visible()
        self.assertGreaterEqual(x0, 0)
        self.assertGreaterEqual(y0, 0)
        self.assertLessEqual(x1, ANCHO_DE_LA_PAGINA)
        self.assertLessEqual(y1, ALTO_DE_LA_PAGINA)

    def test_ni_hacia_atras(self):
        encuadre = self._ampliado()
        for _ in range(60):
            encuadre.desplazar(-1, -1)
        x0, y0, _, _ = encuadre.region_visible()
        self.assertEqual((x0, y0), (0, 0))

    def test_con_la_pagina_entera_a_la_vista_desplazar_no_hace_nada(self):
        """No hay a donde ir: la hoja ya se ve completa."""
        encuadre = _encuadre()
        encuadre.ajustar_a_la_pagina()
        antes = encuadre.region_visible()
        encuadre.desplazar(1, 1)
        self.assertEqual(encuadre.region_visible(), antes)


class SoloSeReescalaLoQueSeVe(unittest.TestCase):
    """Reescalar la hoja entera a cada paso de zoom es la via a una pantalla lenta.

    Una hoja al tope de 3500 px del lado largo son ~9,5 MB de PGM y `PhotoImage`
    guarda ademas su copia interna. La region visible a 560x400 son 0,2 MB.
    """

    def test_al_300_por_ciento_la_region_pedida_es_mucho_menor_que_la_hoja(self):
        encuadre = _encuadre()
        for _ in range(6):
            encuadre.acercar()
        x0, y0, x1, y1 = encuadre.region_visible()
        pixeles_de_la_region = (x1 - x0) * (y1 - y0)
        pixeles_de_la_hoja = ANCHO_DE_LA_PAGINA * ALTO_DE_LA_PAGINA
        self.assertLess(pixeles_de_la_region * 20, pixeles_de_la_hoja)

    def test_la_region_pedida_nunca_es_mas_grande_que_el_panel_dividido_por_la_escala(self):
        encuadre = _encuadre()
        encuadre.acercar()
        x0, y0, x1, y1 = encuadre.region_visible()
        self.assertLessEqual((x1 - x0) * encuadre.escala, ANCHO_DEL_PANEL + 1)
        self.assertLessEqual((y1 - y0) * encuadre.escala, ALTO_DEL_PANEL + 1)


class ElPanelEstrechoNoRompeNada(unittest.TestCase):
    """El divisor se puede arrastrar, y el panel puede quedar en nada."""

    def test_un_panel_de_cero_no_revienta(self):
        encuadre = Encuadre(ANCHO_DE_LA_PAGINA, ALTO_DE_LA_PAGINA)
        encuadre.fijar_el_panel(0, 0)
        encuadre.fijar_la_banda(BANDA_DE_LA_UNIDAD)
        self.assertIsNone(encuadre.region_visible())
        self.assertIsNone(encuadre.resalte())

    def test_una_pagina_sin_tamano_no_revienta(self):
        """Es lo que hay cuando el PDF no se pudo abrir: no hay imagen y no hay px."""
        encuadre = Encuadre(0, 0)
        encuadre.fijar_el_panel(ANCHO_DEL_PANEL, ALTO_DEL_PANEL)
        self.assertIsNone(encuadre.region_visible())
        self.assertIsNone(encuadre.resalte())

    def test_por_debajo_del_ancho_util_la_banda_se_ve_peor_que_la_tira_y_se_puede_saber(self):
        """El mockup fija 450 px como el ancho por debajo del cual esto empeora.

        La pantalla lo tiene que poder DECIR, asi que hace falta poder preguntarlo.
        """
        from extraccion.recorte import ANCHO_DE_LA_TIRA_PX
        from interfaz.encuadre import ANCHO_UTIL_MINIMO_DEL_VISOR_PX

        encuadre = _encuadre(ancho_panel=ANCHO_UTIL_MINIMO_DEL_VISOR_PX - 100)
        self.assertTrue(encuadre.el_panel_es_demasiado_estrecho())
        self.assertLess(encuadre.ancho_dibujado_de_la_banda(), ANCHO_DE_LA_TIRA_PX)

    def test_con_el_ancho_de_diseno_no_avisa_de_nada(self):
        self.assertFalse(_encuadre().el_panel_es_demasiado_estrecho())


class ElRatonSeMueveDentroDelPapel(unittest.TestCase):
    """Lo que faltaba entero, dicho por el dueno el 2026-09-03.

    *«¿Para qué me pones el PDF al lado si no puedo moverme dentro de él?… el zoom
    debe permitirme hacer zoom a cualquier parte del documento y con el mouse
    desplazarme y moverme libremente.»* Medido con `grep` sobre `interfaz/visor.py`
    ese dia: habia `<MouseWheel>`, `<Control-MouseWheel>` y `<Shift-MouseWheel>`, y
    **ni un `<B1-Motion>`**.

        DADO el visor con la hoja mas grande que el panel,
        CUANDO se arrastra con el boton izquierdo,
        ENTONCES el papel se mueve exactamente lo que se movio la mano,
        Y nunca se sale de la hoja.

        DADO un punto cualquiera de la hoja bajo el cursor,
        CUANDO se amplia con Ctrl+rueda o con doble clic,
        ENTONCES ese punto se queda debajo del cursor.
    """

    def _ampliado(self):
        """El visor al 300 %, que es donde arrastrar tiene sentido y sitio."""
        encuadre = _encuadre()
        for _ in PASOS_DE_ZOOM:
            encuadre.acercar()
        return encuadre

    def test_arrastrar_a_la_izquierda_mueve_la_hoja_a_la_izquierda(self):
        """Arrastrar la mano a la izquierda ensena lo que hay a la DERECHA."""
        encuadre = self._ampliado()
        antes = encuadre.region_visible()[0]
        encuadre.desplazar_en_pixeles(-100, 0)
        self.assertGreater(encuadre.region_visible()[0], antes)

    def test_arrastrar_hacia_arriba_ensena_lo_de_abajo(self):
        encuadre = self._ampliado()
        antes = encuadre.region_visible()[1]
        encuadre.desplazar_en_pixeles(0, -100)
        self.assertGreater(encuadre.region_visible()[1], antes)

    def test_el_papel_se_mueve_los_pixeles_que_se_movio_la_mano(self):
        """Un arrastre de 100 px de pantalla mueve 100/escala px de la hoja."""
        encuadre = self._ampliado()
        antes = encuadre.region_visible()[0]
        encuadre.desplazar_en_pixeles(-100, 0)
        self.assertAlmostEqual(
            encuadre.region_visible()[0] - antes, 100 / encuadre.escala, delta=1
        )

    def test_por_mucho_que_se_arrastre_nunca_se_sale_de_la_hoja(self):
        encuadre = self._ampliado()
        for _ in range(200):
            encuadre.desplazar_en_pixeles(-500, -500)
        x0, y0, x1, y1 = encuadre.region_visible()
        self.assertGreaterEqual(x0, 0)
        self.assertGreaterEqual(y0, 0)
        self.assertLessEqual(x1, ANCHO_DE_LA_PAGINA)
        self.assertLessEqual(y1, ALTO_DE_LA_PAGINA)

    def test_arrastrar_sin_nada_dibujado_no_revienta(self):
        """Un caso cuyo PDF se movio se tiene que poder corregir igual."""
        vacio = Encuadre(0, 0)
        vacio.fijar_el_panel(ANCHO_DEL_PANEL, ALTO_DEL_PANEL)
        vacio.desplazar_en_pixeles(-40, -40)
        self.assertIsNone(vacio.region_visible())

    def test_ampliar_sobre_el_cursor_deja_ese_punto_donde_estaba(self):
        """Es la diferencia entre mirar el papel y pelearse con el."""
        encuadre = _encuadre()
        punto = (140, 90)
        antes = encuadre.fraccion_del_punto(*punto)
        encuadre.acercar_en(*punto)
        despues = encuadre.fraccion_del_punto(*punto)
        self.assertAlmostEqual(antes[0], despues[0], places=3)
        self.assertAlmostEqual(antes[1], despues[1], places=3)

    def test_ampliar_en_una_esquina_de_la_hoja_no_pide_pixeles_que_no_existen(self):
        """Contra el borde, la region se acota y el punto se mueve un poco. Es correcto."""
        encuadre = _encuadre()
        encuadre.acercar_en(0, 0)
        x0, y0, _, _ = encuadre.region_visible()
        self.assertGreaterEqual(x0, 0)
        self.assertGreaterEqual(y0, 0)

    def test_reducir_sobre_el_cursor_tambien_deja_ese_punto_donde_estaba(self):
        encuadre = self._ampliado()
        punto = (400, 300)
        antes = encuadre.fraccion_del_punto(*punto)
        encuadre.alejar_en(*punto)
        despues = encuadre.fraccion_del_punto(*punto)
        self.assertAlmostEqual(antes[0], despues[0], places=3)
        self.assertAlmostEqual(antes[1], despues[1], places=3)

    def test_el_zoom_sobre_el_cursor_respeta_los_pasos_fijos(self):
        """No es un zoom continuo: sigue la misma escalera que el boton «+»."""
        encuadre = _encuadre()
        encuadre.acercar_en(100, 100)
        self.assertIn(encuadre.escala, PASOS_DE_ZOOM)

    def test_ampliar_sobre_el_cursor_sin_imagen_no_revienta(self):
        vacio = Encuadre(0, 0)
        vacio.fijar_el_panel(ANCHO_DEL_PANEL, ALTO_DEL_PANEL)
        vacio.acercar_en(10, 10)
        self.assertIsNone(vacio.fraccion_del_punto(10, 10))


class ElResalteSigueALaBandaSinRecalcularLaExtraccion(unittest.TestCase):
    """La banda vive en fracciones, asi que el resalte solo se multiplica por la escala.

    Es lo que hace construible esta pantalla hoy: sin las fracciones guardadas
    (`procedencia_campo.banda_x0..y1`, version 3 del esquema) habria que rehacer la
    extraccion para cada zoom.
    """

    def test_al_ampliar_el_resalte_crece_en_la_misma_proporcion(self):
        """Se mide sobre `resalte()`, que devuelve el rectangulo en decimales.

        `ancho_dibujado_de_la_banda()` redondea a pixeles enteros —es lo que se
        escribe en el pie— y ese redondeo se comia el quinto decimal de la
        comparacion sin que la proporcion tuviera nada de malo.
        """
        encuadre = _encuadre()
        x0, _, x1, _ = encuadre.resalte()
        antes = x1 - x0
        escala_antes = encuadre.escala
        encuadre.acercar()
        proporcion = encuadre.escala / escala_antes
        nuevo_x0, _, nuevo_x1, _ = encuadre.resalte()
        self.assertAlmostEqual((nuevo_x1 - nuevo_x0) / antes, proporcion, places=5)

    def test_cambiar_de_banda_reencuadra_sobre_la_nueva(self):
        """Es lo que pasa al tabular de un campo al siguiente."""
        encuadre = _encuadre()
        arriba = encuadre.resalte()[1]
        encuadre.fijar_la_banda((0.05, 0.70, 0.85, 0.735))
        encuadre.ajustar_a_la_banda()
        self.assertIsNotNone(encuadre.resalte())
        # La banda nueva esta mucho mas abajo en la hoja, asi que la region visible
        # tiene que haberse movido con ella.
        self.assertGreater(encuadre.region_visible()[1], 0)
        self.assertIsNotNone(arriba)


if __name__ == "__main__":
    unittest.main()
