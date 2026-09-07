"""La pantalla partida: el documento entero a un lado y los campos al otro.

Es lo que el dueno pidio dos veces, con estas palabras: «necesito ver el pdf
completo a un lado para confirmar que lo que se leyo es correcto a lo que esta en
el documento», y «al dar clic, de un lado el documento original y del otro lado la
informacion que yo necesito; si esta correcto darle todo correcto, o editarlo yo
mismo».

**El PDF de estas pruebas se fabrica aqui, hoja por hoja.** Los escaneos reales de
`pdfs_referencia/` y del escritorio traen datos de personas de verdad y no entran
en nada que se pueda commitear. El PDF de aqui son seis hojas con una franja
oscura en un sitio distinto de cada una, que es todo lo que hace falta para
comprobar que se ensena LA HOJA que toca: si el visor ensenara otra, la franja
estaria en otro sitio.

Los criterios del pase, en Given/When/Then:

  DADO un caso abierto en una ventana de 1100 x 720,
  CUANDO se dibuja,
  ENTONCES el documento esta a la izquierda y los campos a la derecha, y los dos
  caben sin desbordar los 1100 px.

  DADO el foco en un campo con banda,
  CUANDO pasa al campo siguiente,
  ENTONCES el resalte se mueve a la banda del campo nuevo y esa banda se ve.

  DADO un caso cuyas personas vienen de seis hojas,
  CUANDO el foco entra en una persona de la hoja 4,
  ENTONCES el visor ensena la hoja 4.

  DADO el visor ajustado a una banda,
  CUANDO se pulsa Ctrl+1,
  ENTONCES se ve la pagina entera; y el zoom y el desplazamiento responden al
  teclado y a la rueda.

  DADO todo lo anterior,
  ENTONCES «Todo correcto», «Ninguna ordenanza marcada», «Deshacer» y F2 siguen
  haciendo exactamente lo mismo que antes.
"""

import shutil
import tempfile
import unittest
from pathlib import Path

from datos.conexion import abrir_conexion
from datos.esquema import aplicar_esquema
from datos.procedencia import guardar_procedencia_de_campo
from datos.repositorio import alta_de_caso, alta_de_persona

ANCHO_DE_LA_VENTANA = 1100
ALTO_DE_LA_VENTANA = 720
HOJAS_DEL_GRUPO = 6

ANCHO_DE_LA_HOJA_PT = 612
ALTO_DE_LA_HOJA_PT = 792

# La banda del numero de unidad, en fracciones de la pagina. Ancha y baja, como
# las que guarda `extraccion/bandas.py`.
BANDA_DE_LA_UNIDAD = (0.06, 0.20, 0.86, 0.235)
BANDA_DE_LA_FECHA = (0.06, 0.40, 0.60, 0.435)


def _pdf_de_varias_hojas(hojas):
    """Un PDF fabricado con una franja negra en un sitio distinto de cada hoja.

    Se escribe a mano y sin bibliotecas de generacion por el mismo motivo que
    `crear_pdf_prueba.py`: este proyecto congela su `requirements.txt` y una
    dependencia mas solo para fabricar material de prueba no se paga.
    """
    objetos = []
    contenidos = []
    for numero in range(hojas):
        # La franja baja un poco en cada hoja: asi la hoja se reconoce mirando los
        # pixeles, que es lo que comprueba que el visor ensena la que dice.
        arriba = 700 - numero * 90
        flujo = b"0 0 0 rg\n50 %d 500 40 re f\n" % arriba
        contenidos.append(flujo)

    objetos.append(b"<< /Type /Catalog /Pages 2 0 R >>")
    hijos = b" ".join(b"%d 0 R" % (3 + indice * 2) for indice in range(hojas))
    objetos.append(
        b"<< /Type /Pages /Kids [" + hijos + b"] /Count %d >>" % hojas
    )
    for indice, flujo in enumerate(contenidos):
        objetos.append(
            b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 %d %d] /Contents %d 0 R >>"
            % (ANCHO_DE_LA_HOJA_PT, ALTO_DE_LA_HOJA_PT, 4 + indice * 2)
        )
        objetos.append(b"<< /Length %d >>\nstream\n" % len(flujo) + flujo + b"endstream")

    salida = bytearray(b"%PDF-1.4\n%\xe1\xe9\xf1\n")
    desplazamientos = []
    for numero, cuerpo in enumerate(objetos, start=1):
        desplazamientos.append(len(salida))
        salida += b"%d 0 obj\n" % numero + cuerpo + b"\nendobj\n"
    inicio = len(salida)
    salida += b"xref\n0 %d\n0000000000 65535 f \n" % (len(objetos) + 1)
    for desplazamiento in desplazamientos:
        salida += b"%010d 00000 n \n" % desplazamiento
    salida += b"trailer\n<< /Size %d /Root 1 0 R >>\nstartxref\n%d\n%%%%EOF\n" % (
        len(objetos) + 1,
        inicio,
    )
    return bytes(salida)


def _hay_ventanas():
    """Si no se puede crear un Tk, estas pruebas se omiten en vez de fallar."""
    try:
        import tkinter

        raiz = tkinter.Tk()
    except Exception:  # pragma: no cover - depende de la maquina, no del codigo
        return False
    raiz.destroy()
    return True


class PantallaPartidaAbierta(unittest.TestCase):
    """Monta la pantalla de verdad sobre un PDF fabricado de seis hojas.

    La ventana se abre transparente y no retirada por el motivo que ya midio
    `prueba_correccion.py`: con `withdraw()` la ventana no esta dibujada y Tk
    devuelve geometrias falsas, asi que no se podria medir ningun reparto.
    """

    def setUp(self):
        import tkinter as tk

        from interfaz.correccion import PantallaDeCorreccion
        from interfaz.tema import aplicar_tema

        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_partida_"))
        self.ruta_pdf = self.carpeta / "grupo.pdf"
        self.ruta_pdf.write_bytes(_pdf_de_varias_hojas(HOJAS_DEL_GRUPO))

        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        aplicar_esquema(self.conexion)
        caso = alta_de_caso(
            self.conexion,
            numero_caso="SURB2609",
            unidad_numero="7000011",
            fecha_viaje="2026-09-08",
            ruta_pdf=str(self.ruta_pdf),
            pagina_pdf=1,
        )
        self.caso_id = caso.id
        # Dos campos del caso con banda guardada, que es lo que el visor resalta.
        for campo, banda in (
            ("unidad_numero", BANDA_DE_LA_UNIDAD),
            ("fecha_viaje", BANDA_DE_LA_FECHA),
        ):
            guardar_procedencia_de_campo(
                self.conexion, "casos", caso.id, campo,
                origen="ocr", confianza=0.94, banda=banda,
            )
        for hoja in range(1, HOJAS_DEL_GRUPO + 1):
            persona_id = alta_de_persona(
                self.conexion,
                caso.id,
                mrn=f"055-1111-{3850 + hoja:04d}",
                nombre=f"PERSONA DE LA HOJA {hoja}",
                fila_formulario=hoja,
                pagina_pdf=hoja,
            )
            guardar_procedencia_de_campo(
                self.conexion, "personas", persona_id, "mrn",
                origen="ocr", confianza=0.93, banda=BANDA_DE_LA_UNIDAD,
            )
        self.conexion.commit()

        self.raiz = tk.Tk()
        self.raiz.geometry(f"{ANCHO_DE_LA_VENTANA}x{ALTO_DE_LA_VENTANA}")
        self.raiz.attributes("-alpha", 0.0)
        self.raiz.rowconfigure(0, weight=1)
        self.raiz.columnconfigure(0, weight=1)
        aplicar_tema(self.raiz)
        self.pantalla = PantallaDeCorreccion(
            self.raiz, self.conexion, self.caso_id, al_volver=lambda: None,
            carpeta_de_datos=self.carpeta,
        )
        self.pantalla.grid(row=0, column=0, sticky="nsew")
        self.raiz.update()
        # Sin esto `focus_get()` devuelve None y las pruebas del foco medirian que
        # la ventana no tiene el foco del sistema en vez de medir el codigo.
        self.raiz.focus_force()
        self.raiz.update()
        self.visor = self.pantalla._visor

    def tearDown(self):
        self.conexion.close()
        self.raiz.destroy()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def _enfocar(self, control):
        control.focus_set()
        self.raiz.update()


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class ElRepartoCabeEnLaVentanaMinima(PantallaPartidaAbierta):
    """Criterio 1: documento a la izquierda, campos a la derecha, dentro de 1100 px."""

    def test_hay_dos_paneles_y_el_documento_es_el_primero(self):
        paneles = [str(panel) for panel in self.pantalla._partida.panes()]
        self.assertEqual(len(paneles), 2)
        self.assertEqual(paneles[0], str(self.visor))

    def test_el_documento_esta_a_la_izquierda_de_los_campos(self):
        self.assertLess(
            self.visor.winfo_rootx(), self.pantalla._lienzo.winfo_rootx()
        )

    def test_los_dos_paneles_caben_sin_desbordar_la_ventana(self):
        ancho_del_visor = self.visor.winfo_width()
        ancho_de_los_campos = self.pantalla._lienzo.winfo_width()
        self.assertLessEqual(
            ancho_del_visor + ancho_de_los_campos, ANCHO_DE_LA_VENTANA
        )

    def test_al_visor_le_queda_el_ancho_util_que_el_mockup_pide(self):
        """450 px es el ancho por debajo del cual la banda se ve peor que la tira."""
        from interfaz.encuadre import ANCHO_UTIL_MINIMO_DEL_VISOR_PX

        self.assertGreaterEqual(self.visor.winfo_width(), ANCHO_UTIL_MINIMO_DEL_VISOR_PX)

    def test_a_los_campos_les_quedan_los_478_px_que_necesitan(self):
        """La suma medida por el disenador: etiqueta 120 + entrada 210 + palabra 100
        + huecos 24 + margen 24. Por debajo de eso la columna se corta."""
        self.assertGreaterEqual(self.pantalla._lienzo.winfo_width(), 478)

    def test_ninguna_fila_de_campo_desborda_su_columna(self):
        """Criterio 1, la mitad que se olvida: caber no es solaparse."""
        ancho_del_hueco = self.pantalla._lienzo.winfo_width()
        anchos = [
            campo.winfo_width()
            for campo in self.pantalla._campos_del_caso.values()
        ]
        for ancho in anchos:
            self.assertLessEqual(ancho, ancho_del_hueco + 1)

    def test_los_campos_ya_no_llevan_tira_porque_el_documento_ocupa_su_sitio(self):
        for campo in self.pantalla._campos_del_caso.values():
            self.assertIsNone(campo.tira)
        for bloque in self.pantalla._bloques:
            for campo in bloque.campos.values():
                self.assertIsNone(campo.tira)


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class ElResalteSigueAlFoco(PantallaPartidaAbierta):
    """Criterio 2: tabulando, el resalte se mueve y la banda queda a la vista."""

    def test_al_abrir_ya_hay_una_banda_enfocada(self):
        """Abrir en pagina entera ensena una hoja ilegible: no vale como arranque."""
        self.assertIsNotNone(self.visor.encuadre.resalte())

    def test_el_resalte_cambia_al_cambiar_de_campo(self):
        self._enfocar(self.pantalla._campos_del_caso["unidad_numero"].entrada)
        de_la_unidad = self.visor.encuadre.resalte()
        self._enfocar(self.pantalla._campos_del_caso["fecha_viaje"].entrada)
        de_la_fecha = self.visor.encuadre.resalte()
        self.assertIsNotNone(de_la_unidad)
        self.assertIsNotNone(de_la_fecha)
        self.assertNotEqual(de_la_unidad, de_la_fecha)

    def test_la_banda_enfocada_queda_dentro_del_lienzo(self):
        self._enfocar(self.pantalla._campos_del_caso["unidad_numero"].entrada)
        x0, y0, x1, y1 = self.visor.encuadre.resalte()
        self.assertGreaterEqual(x0, 0)
        self.assertGreaterEqual(y0, 0)
        self.assertLessEqual(x1, self.visor._lienzo.winfo_width() + 1)
        self.assertLessEqual(y1, self.visor._lienzo.winfo_height() + 1)

    def test_la_banda_se_dibuja_mas_grande_que_la_tira_que_sustituye(self):
        """Si no, este cambio seria una perdida. Es el numero que lo justifica."""
        from extraccion.recorte import ANCHO_DE_LA_TIRA_PX

        self._enfocar(self.pantalla._campos_del_caso["unidad_numero"].entrada)
        self.assertGreater(
            self.visor.encuadre.ancho_dibujado_de_la_banda(), ANCHO_DE_LA_TIRA_PX
        )

    def test_el_pie_del_visor_nombra_el_campo_y_da_el_numero(self):
        self._enfocar(self.pantalla._campos_del_caso["unidad_numero"].entrada)
        pie = self.visor.texto_del_pie()
        self.assertIn("N.º de unidad", pie)
        self.assertIn("380", pie)

    def test_un_campo_sin_banda_no_resalta_nada_y_lo_dice_con_palabras(self):
        """Un rectangulo que no aparece parece un defecto; una frase, no."""
        campo = self.pantalla._campos_del_caso["unidad_nombre"]
        self.assertIsNone(campo.banda)
        self._enfocar(campo.entrada)
        self.assertIsNone(self.visor.encuadre.resalte())
        self.assertIn("no tiene sitio marcado", self.visor.texto_del_pie())

    def test_mover_el_foco_no_se_lo_lleva_el_visor(self):
        """El visor mira, no teclea. Si robara el foco, Tab dejaria de servir."""
        entrada = self.pantalla._campos_del_caso["unidad_numero"].entrada
        self._enfocar(entrada)
        self.assertEqual(str(self.raiz.focus_get()), str(entrada))


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class ElVisorSigueALaHojaDeLaPersona(PantallaPartidaAbierta):
    """Criterio 3: enfocar a alguien de la hoja 4 ensena la hoja 4."""

    def test_el_caso_se_abre_por_la_hoja_que_lo_abrio(self):
        self.assertEqual(self.visor.hoja, 1)

    def test_enfocar_una_persona_de_la_hoja_4_ensena_la_hoja_4(self):
        self._enfocar(self.pantalla._bloques[3].primer_control())
        self.assertEqual(self.visor.hoja, 4)

    def test_y_volver_a_una_de_la_hoja_2_ensena_la_2(self):
        """Es la prueba de que sigue al foco y no de que avanza sin mas."""
        self._enfocar(self.pantalla._bloques[3].primer_control())
        self._enfocar(self.pantalla._bloques[1].primer_control())
        self.assertEqual(self.visor.hoja, 2)

    def test_cada_hoja_ensena_pixeles_distintos(self):
        """Sin esto, «cambiar de hoja» podria ser un rotulo que cambia y nada mas.

        El PDF fabricado lleva la franja negra en un sitio distinto en cada hoja,
        asi que dos hojas con la misma imagen serian el defecto que
        `prueba_pagina_de_la_persona.py` ya cazo una vez con las tiras.
        """
        imagenes = []
        for indice in (0, 3):
            self._enfocar(self.pantalla._bloques[indice].primer_control())
            imagen = self.pantalla.paginas.pagina(self.visor.hoja).imagen()
            self.assertIsNotNone(imagen)
            imagenes.append(imagen.tobytes())
        self.assertNotEqual(imagenes[0], imagenes[1])

    def test_las_flechas_de_hoja_recorren_el_pdf_y_no_se_pasan(self):
        for _ in range(20):
            self.visor.hoja_siguiente()
        self.assertEqual(self.visor.hoja, HOJAS_DEL_GRUPO)
        for _ in range(20):
            self.visor.hoja_anterior()
        self.assertEqual(self.visor.hoja, 1)

    def test_mirar_otra_hoja_distinta_de_la_del_campo_se_dice_en_el_pie(self):
        """Es el caso incomodo del mockup: se esta mirando otra cosa y hay que saberlo."""
        self._enfocar(self.pantalla._campos_del_caso["unidad_numero"].entrada)
        self.visor.hoja_siguiente()
        pie = self.visor.texto_del_pie()
        self.assertIn("Está viendo la hoja 2", pie)
        self.assertIn("está en la 1", pie)


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class ElZoomYElDesplazamiento(PantallaPartidaAbierta):
    """Criterio 4: Ctrl+1 pagina entera, y zoom y desplazamiento con teclado y rueda."""

    def _pulsar(self, tecla):
        self.raiz.event_generate(tecla)
        self.raiz.update()

    def test_ctrl_1_ensena_la_pagina_entera(self):
        from interfaz.encuadre import MODO_PAGINA

        self._enfocar(self.pantalla._campos_del_caso["unidad_numero"].entrada)
        self._pulsar("<Control-Key-1>")
        self.assertEqual(self.visor.encuadre.modo, MODO_PAGINA)
        region = self.visor.encuadre.region_visible()
        alto, ancho = self.pantalla.paginas.pagina(1).imagen().shape[:2]
        self.assertEqual(region, (0, 0, ancho, alto))

    def test_ctrl_2_vuelve_a_ajustar_a_la_banda(self):
        from interfaz.encuadre import MODO_BANDA

        self._pulsar("<Control-Key-1>")
        self._pulsar("<Control-Key-2>")
        self.assertEqual(self.visor.encuadre.modo, MODO_BANDA)

    def test_ctrl_mas_amplia_y_ctrl_menos_reduce(self):
        self._enfocar(self.pantalla._campos_del_caso["unidad_numero"].entrada)
        antes = self.visor.encuadre.escala
        self._pulsar("<Control-equal>")
        ampliado = self.visor.encuadre.escala
        self.assertGreater(ampliado, antes)
        self._pulsar("<Control-minus>")
        self.assertLess(self.visor.encuadre.escala, ampliado)

    def test_el_zoom_no_mueve_el_foco_del_campo(self):
        """Es la regla que manda en el teclado de esta pantalla."""
        entrada = self.pantalla._campos_del_caso["unidad_numero"].entrada
        self._enfocar(entrada)
        self._pulsar("<Control-equal>")
        self.assertEqual(str(self.raiz.focus_get()), str(entrada))

    def test_ctrl_mayus_flechas_desplazan_sin_mover_el_foco(self):
        entrada = self.pantalla._campos_del_caso["unidad_numero"].entrada
        self._enfocar(entrada)
        for _ in range(4):
            self._pulsar("<Control-equal>")
        antes = self.visor.encuadre.region_visible()
        self._pulsar("<Control-Shift-Down>")
        self.assertNotEqual(self.visor.encuadre.region_visible(), antes)
        self.assertEqual(str(self.raiz.focus_get()), str(entrada))

    def test_la_rueda_desplaza_el_documento(self):
        self._enfocar(self.pantalla._campos_del_caso["unidad_numero"].entrada)
        for _ in range(4):
            self.visor.acercar()
        antes = self.visor.encuadre.region_visible()
        self.visor._lienzo.event_generate("<MouseWheel>", delta=-120)
        self.raiz.update()
        self.assertNotEqual(self.visor.encuadre.region_visible(), antes)

    def test_la_rueda_con_control_amplia(self):
        self._enfocar(self.pantalla._campos_del_caso["unidad_numero"].entrada)
        antes = self.visor.encuadre.escala
        self.visor._lienzo.event_generate("<Control-MouseWheel>", delta=120)
        self.raiz.update()
        self.assertGreater(self.visor.encuadre.escala, antes)

    def test_ctrl_3_devuelve_la_hoja_al_ancho_del_panel(self):
        """Sin esta tecla, el encuadre de arranque seria el unico irrecuperable."""
        from interfaz.encuadre import MODO_ANCHO

        self._pulsar("<Control-Key-1>")
        self._pulsar("<Control-Key-3>")
        self.assertEqual(self.visor.encuadre.modo, MODO_ANCHO)

    def test_arrastrar_con_el_raton_mueve_el_papel(self):
        """Lo que faltaba entero: `grep <B1-Motion> interfaz/visor.py` daba cero.

        Palabras del dueño: «¿para qué me pones el PDF al lado si no puedo moverme
        dentro de él?».
        """
        for _ in range(4):
            self.visor.acercar()
        self.raiz.update()
        antes = self.visor.encuadre.region_visible()
        self._arrastrar_desde((200, 200), (140, 130))
        self.assertNotEqual(self.visor.encuadre.region_visible(), antes)

    def test_arrastrar_no_mueve_el_foco_del_campo(self):
        """Pulsar el papel para moverlo no puede sacar el cursor de donde escribe."""
        entrada = self.pantalla._campos_del_caso["unidad_numero"].entrada
        self._enfocar(entrada)
        for _ in range(4):
            self.visor.acercar()
        self.raiz.update()
        self._arrastrar_desde((200, 200), (140, 130))
        self.assertEqual(str(self.raiz.focus_get()), str(entrada))

    def test_el_doble_clic_amplia(self):
        """Se generan dos clics seguidos y NO un `<Double-Button-1>` a mano.

        Tk se niega: «Double, Triple, or Quadruple modifier not allowed» en
        `event_generate`. El doble clic lo sintetiza él contando dos pulsaciones
        seguidas, que además es lo que hace una mano.
        """
        lienzo = self.visor._lienzo
        antes = self.visor.encuadre.escala
        for _ in range(2):
            lienzo.event_generate("<ButtonPress-1>", x=200, y=200)
            lienzo.event_generate("<ButtonRelease-1>", x=200, y=200)
        self.raiz.update()
        self.assertGreater(self.visor.encuadre.escala, antes)

    def test_la_rueda_con_control_amplia_donde_apunta_el_cursor(self):
        """Ampliar sobre el centro obliga a ampliar, buscar y arrastrar. Y repetir."""
        punto = (150, 320)
        antes = self.visor.encuadre.fraccion_del_punto(*punto)
        self.visor._lienzo.event_generate("<Control-MouseWheel>", delta=120, x=punto[0], y=punto[1])
        self.raiz.update()
        despues = self.visor.encuadre.fraccion_del_punto(*punto)
        self.assertAlmostEqual(antes[0], despues[0], places=2)
        self.assertAlmostEqual(antes[1], despues[1], places=2)

    def _arrastrar_desde(self, desde, hasta):
        """Un arrastre completo: pulsar, mover y soltar, como lo hace una mano."""
        lienzo = self.visor._lienzo
        lienzo.event_generate("<ButtonPress-1>", x=desde[0], y=desde[1])
        lienzo.event_generate("<B1-Motion>", x=hasta[0], y=hasta[1])
        lienzo.event_generate("<ButtonRelease-1>", x=hasta[0], y=hasta[1])
        self.raiz.update()

    def test_ctrl_avpag_y_repag_cambian_de_hoja(self):
        self._pulsar("<Control-Next>")
        self.assertEqual(self.visor.hoja, 2)
        self._pulsar("<Control-Prior>")
        self.assertEqual(self.visor.hoja, 1)

    def test_f4_esconde_el_documento_y_lo_devuelve(self):
        """Colapsado, la columna de campos ocupa la ventana entera."""
        self._pulsar("<F4>")
        self.assertNotIn(str(self.visor), [str(p) for p in self.pantalla._partida.panes()])
        ancho_solo = self.pantalla._lienzo.winfo_width()
        self._pulsar("<F4>")
        self.assertIn(str(self.visor), [str(p) for p in self.pantalla._partida.panes()])
        self.assertGreater(ancho_solo, self.pantalla._lienzo.winfo_width())


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class LoQueYaFuncionabaSigueFuncionando(PantallaPartidaAbierta):
    """Criterio 5: los tres botones del pie y F2, intactos.

    Se comprueban aqui **ademas** de en sus propias pruebas porque lo que cambia
    es la pantalla que los contiene: un reparto nuevo puede dejar un boton fuera
    del hueco o romper el atajo sin que la prueba del boton se entere.
    """

    def test_los_tres_botones_del_pie_siguen_estando(self):
        self.assertEqual(
            self.pantalla._boton_de_las_casillas.cget("text"),
            "Ninguna ordenanza marcada  Ctrl+0",
        )
        self.assertEqual(
            self.pantalla._boton_verificar.cget("text"), "Todo correcto  Ctrl+Intro"
        )
        self.assertEqual(
            self.pantalla._boton_de_deshacer.cget("text"), "Deshacer «Todo correcto»"
        )

    def test_ctrl_0_sigue_siendo_de_las_ordenanzas_y_no_del_zoom(self):
        """El mockup pedia Ctrl+0 para el zoom. Estaba ocupado y manda lo que ya habia."""
        llamadas = []
        self.pantalla.resolver_las_casillas_sin_leer = lambda evento=None: llamadas.append(1)
        self.pantalla.atar_atajos()
        escala = self.visor.encuadre.escala
        self.raiz.event_generate("<Control-Key-0>")
        self.raiz.update()
        self.assertEqual(llamadas, [1])
        self.assertEqual(self.visor.encuadre.escala, escala)

    def test_f2_sigue_anunciando_la_hoja_de_la_persona_enfocada(self):
        self._enfocar(self.pantalla._bloques[3].primer_control())
        self.assertEqual(self.pantalla._pagina_en_foco(), 4)
        self.assertIn("PÁGINA 4", self.pantalla._aviso_de_la_pagina(4))

    def test_el_pie_sigue_diciendo_por_que_no_se_puede_verificar(self):
        self.assertIn("disabled", self.pantalla._boton_verificar.state())
        self.assertNotEqual(self.pantalla._motivo_del_bloqueo.cget("text"), "")

    def test_el_contador_sigue_naciendo_en_cero_verificados(self):
        """Regla permanente 5: nada se marca solo."""
        self.assertIn("Verificados 0 de", self.pantalla._contador.cget("text"))

    def test_intro_sigue_saltando_de_persona_en_persona(self):
        self._enfocar(self.pantalla._bloques[0].primer_control())
        self.pantalla._saltar_a_la_persona_siguiente()
        self.raiz.update()
        self.assertEqual(
            str(self.raiz.focus_get()),
            str(self.pantalla._bloques[1].primer_control()),
        )

    def test_esc_sigue_deshaciendo_lo_tecleado_y_no_cierra(self):
        campo = self.pantalla._campos_del_caso["unidad_numero"]
        self._enfocar(campo.entrada)
        campo.entrada.delete(0, "end")
        campo.entrada.insert(0, "999")
        campo.entrada.event_generate("<Escape>")
        self.raiz.update()
        self.assertEqual(campo.entrada.get(), "7000011")
        self.assertTrue(self.raiz.winfo_exists())


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class ElCasoSinNumeroSeAbreIgual(unittest.TestCase):
    """Una hoja que no se pudo identificar abre la MISMA ventana, con el documento.

    Que sea la misma es la mitad del valor: quien la abre ya sabe usarla. Lo unico
    distinto es que el numero de caso se puede teclear.
    """

    def setUp(self):
        import tkinter as tk

        from interfaz.correccion import PantallaDeCorreccion
        from interfaz.tema import aplicar_tema

        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_sin_numero_"))
        ruta_pdf = self.carpeta / "suelta.pdf"
        ruta_pdf.write_bytes(_pdf_de_varias_hojas(HOJAS_DEL_GRUPO))
        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        aplicar_esquema(self.conexion)
        caso = alta_de_caso(
            self.conexion,
            numero_caso=None,
            unidad_numero="7000011",
            ruta_pdf=str(ruta_pdf),
            pagina_pdf=4,
        )
        alta_de_persona(
            self.conexion, caso.id, mrn="055-1112-1120", nombre="MARTINEZ, LUISA",
            fila_formulario=1, pagina_pdf=4,
        )
        self.conexion.commit()

        self.raiz = tk.Tk()
        self.raiz.geometry(f"{ANCHO_DE_LA_VENTANA}x{ALTO_DE_LA_VENTANA}")
        self.raiz.attributes("-alpha", 0.0)
        aplicar_tema(self.raiz)
        self.pantalla = PantallaDeCorreccion(
            self.raiz, self.conexion, caso.id, al_volver=lambda: None,
            carpeta_de_datos=self.carpeta,
        )
        self.pantalla.grid(row=0, column=0, sticky="nsew")
        self.raiz.update()
        self.raiz.focus_force()
        self.raiz.update()

    def tearDown(self):
        self.conexion.close()
        self.raiz.destroy()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def test_se_abre_con_el_documento_a_la_izquierda(self):
        self.assertEqual(len(self.pantalla._partida.panes()), 2)
        self.assertGreater(self.pantalla._visor.winfo_width(), 0)

    def test_ensena_la_hoja_de_la_que_salio_esa_pagina_y_no_la_primera(self):
        self.assertEqual(self.pantalla._visor.hoja, 4)

    def test_el_numero_de_caso_se_puede_teclear(self):
        entrada = self.pantalla._campos_del_caso["numero_caso"].entrada
        self.assertNotIn("readonly", entrada.state())
        entrada.insert(0, "CASP2609")
        self.raiz.update()
        self.assertEqual(entrada.get(), "CASP2609")

    def test_el_aviso_sigue_diciendo_que_no_se_perdio_nada(self):
        avisos = " ".join(self.pantalla._textos_de_aviso())
        self.assertIn("SIN número de caso", avisos)


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class ElPdfQueNoEstaNoImpideCorregir(unittest.TestCase):
    """Un caso que no se abre porque falta una imagen es un caso que nadie atiende."""

    def setUp(self):
        import tkinter as tk

        from interfaz.correccion import PantallaDeCorreccion
        from interfaz.tema import aplicar_tema

        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_sin_pdf_"))
        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        aplicar_esquema(self.conexion)
        caso = alta_de_caso(
            self.conexion, numero_caso="SURB2609", fecha_viaje="2026-09-08",
            ruta_pdf=str(self.carpeta / "no_existe.pdf"), pagina_pdf=1,
        )
        alta_de_persona(
            self.conexion, caso.id, mrn="055-1111-3853", nombre="UNO",
            fila_formulario=1, pagina_pdf=1,
        )
        self.conexion.commit()
        self.raiz = tk.Tk()
        self.raiz.geometry(f"{ANCHO_DE_LA_VENTANA}x{ALTO_DE_LA_VENTANA}")
        self.raiz.attributes("-alpha", 0.0)
        aplicar_tema(self.raiz)
        self.pantalla = PantallaDeCorreccion(
            self.raiz, self.conexion, caso.id, al_volver=lambda: None,
            carpeta_de_datos=self.carpeta,
        )
        self.pantalla.grid(row=0, column=0, sticky="nsew")
        self.raiz.update()

    def tearDown(self):
        self.conexion.close()
        self.raiz.destroy()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def test_la_pantalla_se_dibuja_igual(self):
        self.assertEqual(len(self.pantalla._partida.panes()), 2)

    def test_los_campos_se_siguen_pudiendo_editar(self):
        entrada = self.pantalla._campos_del_caso["unidad_numero"].entrada
        entrada.insert(0, "7000011")
        self.assertEqual(entrada.get(), "7000011")

    def test_el_visor_dice_por_que_no_hay_imagen_en_vez_de_quedarse_en_blanco(self):
        pie = self.pantalla._visor.texto_del_pie()
        self.assertTrue(pie.strip(), "el visor no dijo nada")
        self.assertNotIn("Banda", pie)

    def test_el_zoom_sobre_una_hoja_que_no_existe_no_revienta(self):
        self.pantalla._visor.acercar()
        self.pantalla._visor.ajustar_a_la_pagina()
        self.pantalla._visor.desplazar(1, 1)
        self.pantalla._visor.hoja_siguiente()
        self.raiz.update()


if __name__ == "__main__":
    unittest.main()
