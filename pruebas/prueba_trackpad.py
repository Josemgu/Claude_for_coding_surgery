"""El gesto de dos dedos del trackpad desplaza las dos superficies.

**El criterio sale de una queja del dueno, no del codigo:** *«le doy para abajo con
el trackpad de mi laptop y no baja»* (2026-09-03).

    DADO la pantalla de correccion abierta,
    CUANDO llega un gesto de dos dedos hacia abajo,
    ENTONCES la columna de campos se desplaza hacia abajo.

    DADO el visor con la hoja mas grande que el panel,
    CUANDO llega un gesto de dos dedos,
    ENTONCES el papel se mueve en los dos ejes.

⚠️ **Lo que estas pruebas NO cubren, dicho aqui y no escondido.** La maquina donde
se escribieron **no tiene trackpad**: el evento se genera con `event_generate`, que
es un evento sintetico. Lo que se comprueba es que el binding existe, que el
desempaquetado del delta es el que hace el propio Tk y que la superficie se mueve
en el sentido correcto. Lo que **no** se comprueba es que el sistema operativo del
dueno entregue de verdad `<TouchpadScroll>` con su trackpad: eso solo lo puede
medir el, en su portatil.
"""

import tkinter as tk
import unittest

from interfaz.gestos import UNO_DE_CADA, deltas_del_trackpad, toca_atender


def _empaquetar(dx, dy):
    """Los dos ejes metidos en un entero como los mete Tk 9: dx arriba, dy abajo.

    Se escribe aqui **al reves** de como los saca `deltas_del_trackpad`, que llama
    a la funcion del propio Tk. Que las dos cuentas sean independientes es lo que
    hace que la prueba signifique algo: si aqui se llamara a la misma funcion de
    Tk, la comparacion seria de una cosa consigo misma.
    """
    return (dx << 16) | (dy & 0xFFFF)


class PruebaConVentana(unittest.TestCase):
    """Una ventana de verdad, escondida. Sin ventana no hay evento que generar."""

    def setUp(self):
        try:
            self.raiz = tk.Tk()
        except tk.TclError as causa:
            self.skipTest(f"Esta máquina no tiene entorno gráfico: {causa}")
        self.raiz.withdraw()

    def tearDown(self):
        try:
            self.raiz.destroy()
        except tk.TclError:
            pass


class ElDeltaSeDesempaquetaComoLoHaceTk(PruebaConVentana):
    """Los 16 bits altos son el eje X y los 16 bajos el eje Y, con signo."""

    def test_un_gesto_hacia_abajo_da_un_dy_negativo(self):
        self.assertEqual(deltas_del_trackpad(self.raiz, _empaquetar(0, -3)), (0, -3))

    def test_un_gesto_hacia_arriba_da_un_dy_positivo(self):
        self.assertEqual(deltas_del_trackpad(self.raiz, _empaquetar(0, 5)), (0, 5))

    def test_un_gesto_lateral_da_un_dx_y_no_toca_el_dy(self):
        self.assertEqual(deltas_del_trackpad(self.raiz, _empaquetar(7, 0)), (7, 0))

    def test_los_dos_ejes_a_la_vez_no_se_pisan(self):
        self.assertEqual(deltas_del_trackpad(self.raiz, _empaquetar(4, -6)), (4, -6))

    def test_el_cero_es_cero_y_no_desplaza_nada(self):
        self.assertEqual(deltas_del_trackpad(self.raiz, _empaquetar(0, 0)), (0, 0))


class SoloSeAtiendeUnoDeCadaCinco(unittest.TestCase):
    """El mismo 5 que usa Tk en su binding de `Listbox`. No es un numero de aqui."""

    class _Evento:
        def __init__(self, serial):
            self.serial = serial

    def test_le_toca_al_multiplo_de_cinco(self):
        self.assertTrue(toca_atender(self._Evento(UNO_DE_CADA)))
        self.assertTrue(toca_atender(self._Evento(0)))

    def test_no_le_toca_a_los_de_en_medio(self):
        for serial in range(1, UNO_DE_CADA):
            self.assertFalse(toca_atender(self._Evento(serial)), f"serial {serial}")

    def test_un_evento_sin_numero_de_serie_no_revienta(self):
        self.assertTrue(toca_atender(object()))


class ElVisorSeMueveConElTrackpad(PruebaConVentana):
    """El papel se mueve en los dos ejes, y por pixeles y no por renglones."""

    def setUp(self):
        super().setUp()
        import numpy

        from interfaz.tema import aplicar_tema
        from interfaz.visor import VisorDelDocumento

        aplicar_tema(self.raiz)
        self.raiz.deiconify()
        self.raiz.geometry("900x600")
        hoja = numpy.zeros((3500, 2700), dtype=numpy.uint8)

        class _Pagina:
            def imagen(self):
                return hoja

        class _Paginas:
            motivo_del_fallo = None

            def pagina(self, numero):
                return _Pagina()

        self.visor = VisorDelDocumento(self.raiz, _Paginas(), 1, 6)
        self.visor.pack(fill="both", expand=True)
        self.raiz.update()
        self.visor.enfocar(1, (0.05, 0.20, 0.85, 0.235), "N.º de unidad")
        # Ampliado, que es donde hay sitio para moverse en los dos ejes.
        for _ in range(4):
            self.visor.acercar()
        self.raiz.update()

    def _gesto(self, dx, dy):
        self.visor._lienzo.event_generate("<TouchpadScroll>", delta=_empaquetar(dx, dy))
        self.raiz.update()

    def test_el_lienzo_tiene_atado_el_gesto_del_trackpad(self):
        """Era exactamente lo que faltaba: `Canvas` no lo trae de fabrica."""
        self.assertIn("<TouchpadScroll>", self.visor._lienzo.bind())

    def test_un_gesto_hacia_arriba_ensena_lo_que_hay_mas_abajo(self):
        antes = self.visor.encuadre.region_visible()[1]
        for _ in range(20):
            self._gesto(0, -10)
        self.assertGreater(self.visor.encuadre.region_visible()[1], antes)

    def test_un_gesto_hacia_abajo_devuelve_la_hoja_hacia_arriba(self):
        for _ in range(20):
            self._gesto(0, -10)
        despues_de_bajar = self.visor.encuadre.region_visible()[1]
        for _ in range(20):
            self._gesto(0, 10)
        self.assertLess(self.visor.encuadre.region_visible()[1], despues_de_bajar)

    def test_un_gesto_lateral_mueve_el_papel_de_lado(self):
        antes = self.visor.encuadre.region_visible()[0]
        for _ in range(20):
            self._gesto(-10, 0)
        self.assertGreater(self.visor.encuadre.region_visible()[0], antes)

    def test_un_gesto_no_cambia_el_zoom(self):
        """Desplazarse no es ampliar: el trackpad no puede cambiar la escala sola."""
        antes = self.visor.encuadre.escala
        self._gesto(0, -10)
        self.assertEqual(self.visor.encuadre.escala, antes)


if __name__ == "__main__":
    unittest.main()
