"""Que la pantalla baje con la rueda Y con el trackpad, probado con eventos.

**El criterio del que sale este archivo, literal del dueno:** *«le doy para abajo
con el trackpad de mi laptop y no baja»*. La prueba no mira el codigo: genera los
dos eventos y comprueba que el lienzo se movio.

⚠️ **Lo que estas pruebas NO demuestran.** Aqui no hay trackpad. Los eventos son
sinteticos (`event_generate`), asi que esto prueba que la atadura existe, que `%D`
se desempaqueta bien y que el lienzo se mueve la cantidad correcta — **no** que el
gesto real del portatil del dueno emita `<TouchpadScroll>` con esa magnitud. Eso
solo se puede medir en su maquina.

**Y un detalle que costo encontrar, anotado para el que venga:** un
`event_generate` sobre una ventana con `withdraw()` **no entrega el evento** — la
primera medicion dio una lista vacia por eso, no porque la atadura fallara. La
ventana tiene que estar realizada (`geometry` + `update`) para que los eventos
lleguen. Por eso estas pruebas no ocultan la ventana.
"""

import unittest

import tkinter as tk

from interfaz.desplazamiento import (
    PIXELES_POR_MUESCA_DE_RUEDA,
    UNIDADES_DE_UNA_MUESCA,
    Desplazador,
    MarcoQueSeDesplaza,
)

# Cuantos renglones se meten dentro para que haya de sobra por donde bajar.
RENGLONES_DE_RELLENO = 80


def empaquetar_deltas(delta_x, delta_y):
    """Arma el `%D` de un `<TouchpadScroll>` como lo empaqueta Tk.

    x en los 16 bits altos, y en los bajos. Se escribe aqui —y no se importa del
    modulo— **a proposito**: si la prueba usara la misma funcion que el codigo,
    las dos podrian estar equivocadas igual y la prueba pasaria. El desempaquetado
    de verdad lo hace `tk::PreciseScrollDeltas`, y lo que se comprueba abajo es que
    coincide con esto.
    """
    return (delta_x << 16) | (delta_y & 0xFFFF)


class PruebaConVentana(unittest.TestCase):
    """Una ventana de verdad, realizada, con un marco desplazable dentro."""

    def setUp(self):
        self.raiz = tk.Tk()
        self.raiz.geometry("420x260")
        self.marco = MarcoQueSeDesplaza(self.raiz)
        self.marco.pack(fill="both", expand=True)
        for numero in range(RENGLONES_DE_RELLENO):
            tk.Label(self.marco.interior, text=f"renglón {numero}").pack(anchor="w")
        self.raiz.update_idletasks()
        self.raiz.update()
        self.lienzo = self.marco._lienzo
        self.lienzo.configure(scrollregion=self.lienzo.bbox("all"))
        self.raiz.update()

    def tearDown(self):
        self.marco.soltar()
        self.raiz.destroy()

    def arriba_del_todo(self):
        self.lienzo.yview_moveto(0)
        self.raiz.update()

    def pixel_de_arriba(self):
        """Que pixel del contenido esta pegado al borde de arriba del lienzo."""
        return self.lienzo.canvasy(0)

    def entrar_el_raton(self):
        """Las ataduras se ponen al entrar el raton, que es cuando se usan."""
        self.lienzo.event_generate("<Enter>", when="now")
        self.raiz.update()


class PruebaDeLaRueda(PruebaConVentana):
    """`<MouseWheel>`: lo que ya funcionaba en otras pantallas y aqui no existia."""

    def test_la_rueda_baja_la_pantalla(self):
        self.entrar_el_raton()
        self.arriba_del_todo()

        self.lienzo.event_generate("<MouseWheel>", delta=-120, when="now")
        self.raiz.update()

        self.assertEqual(PIXELES_POR_MUESCA_DE_RUEDA, self.pixel_de_arriba())

    def test_la_rueda_hacia_arriba_sube(self):
        self.entrar_el_raton()
        self.lienzo.yview_moveto(0.5)
        self.raiz.update()
        antes = self.pixel_de_arriba()

        self.lienzo.event_generate("<MouseWheel>", delta=120, when="now")
        self.raiz.update()

        self.assertLess(self.pixel_de_arriba(), antes)


class PruebaDelTrackpad(PruebaConVentana):
    """`<TouchpadScroll>`: el evento que el proyecto no ataba en ninguna parte."""

    def test_el_gesto_del_trackpad_baja_la_pantalla(self):
        """El criterio 4 del pase, y la queja del dueno, en una linea."""
        self.entrar_el_raton()
        self.arriba_del_todo()

        self.lienzo.event_generate(
            "<TouchpadScroll>", delta=empaquetar_deltas(0, -40), when="now"
        )
        self.raiz.update()

        self.assertEqual(40, self.pixel_de_arriba())

    def test_el_gesto_hacia_arriba_sube(self):
        self.entrar_el_raton()
        self.lienzo.yview_moveto(0.5)
        self.raiz.update()
        antes = self.pixel_de_arriba()

        self.lienzo.event_generate(
            "<TouchpadScroll>", delta=empaquetar_deltas(0, 40), when="now"
        )
        self.raiz.update()

        self.assertLess(self.pixel_de_arriba(), antes)

    def test_un_gesto_PEQUENO_mueve_algo(self):
        """La regla del 2026-09-03: no se redondea a muescas de 120.

        Con la aritmetica vieja —`int(-delta / 120)`— un gesto de 3 daria 0 y el
        dedo se movería sin que la pantalla hiciera nada.
        """
        self.entrar_el_raton()
        self.arriba_del_todo()

        self.lienzo.event_generate(
            "<TouchpadScroll>", delta=empaquetar_deltas(0, -3), when="now"
        )
        self.raiz.update()

        self.assertEqual(3, self.pixel_de_arriba())

    def test_el_desempaquetado_de_D_coincide_con_el_de_Tk(self):
        """Que `tk::PreciseScrollDeltas` y el empaquetado de esta prueba concuerdan.

        Si no concordaran, todas las de arriba estarian probando otra cosa.
        """
        for delta_x, delta_y in ((0, -40), (0, 40), (5, 3), (-1, 0), (0, 0)):
            with self.subTest(delta_x=delta_x, delta_y=delta_y):
                empaquetado = empaquetar_deltas(delta_x, delta_y)
                self.assertEqual(
                    (delta_x, delta_y),
                    tuple(
                        int(valor)
                        for valor in self.lienzo.tk.call(
                            "tk::PreciseScrollDeltas", empaquetado
                        )
                    ),
                )


class PruebaDeLaAcumulacionDelResto(PruebaConVentana):
    """Un delta que no llega a un pixel no se tira: se guarda y se suma."""

    def test_muchos_gestos_diminutos_acaban_moviendo(self):
        """Con la fraccion tirada, esto se quedaria en 0 para siempre."""
        self.entrar_el_raton()
        self.arriba_del_todo()
        # Un delta de 12 son 5 px por muesca completa; aqui se manda una fraccion
        # tan pequena que cada evento por si solo no llega ni a un pixel.
        por_evento = -1
        pixeles_esperados = abs(por_evento) * PIXELES_POR_MUESCA_DE_RUEDA / UNIDADES_DE_UNA_MUESCA

        for _ in range(UNIDADES_DE_UNA_MUESCA):
            self.lienzo.event_generate("<MouseWheel>", delta=por_evento, when="now")
        self.raiz.update()

        # 120 eventos de 1 unidad son exactamente una muesca entera.
        self.assertEqual(PIXELES_POR_MUESCA_DE_RUEDA, self.pixel_de_arriba())
        self.assertLess(pixeles_esperados, 1, "cada evento suelto no llega a un pixel")


class PruebaDeQueLasAtadurasSeSueltan(PruebaConVentana):
    """Sin soltar, esta pantalla seguiria desplazandose desde otra."""

    def test_al_salir_el_raton_la_rueda_deja_de_mover_esta_pantalla(self):
        self.entrar_el_raton()
        self.arriba_del_todo()

        self.lienzo.event_generate("<Leave>", when="now")
        self.raiz.update()
        self.lienzo.event_generate("<MouseWheel>", delta=-120, when="now")
        self.raiz.update()

        self.assertEqual(0, self.pixel_de_arriba())

    def test_soltar_dos_veces_no_levanta(self):
        """Se llama al cambiar de pantalla y al destruirla: puede repetirse."""
        self.marco.soltar()
        self.marco.soltar()


class PruebaDeLoQueTkNoTraeDeFabrica(PruebaConVentana):
    """La medicion que justifica que este modulo exista.

    Si algun dia `Canvas` trajera estas ataduras de serie, esta prueba se pondria
    en rojo y habria que venir a ver si este modulo sigue haciendo falta.
    """

    def test_el_lienzo_no_trae_ninguna_de_las_dos_ataduras_de_clase(self):
        self.assertEqual("", self.raiz.tk.call("bind", "Canvas", "<MouseWheel>"))
        self.assertEqual("", self.raiz.tk.call("bind", "Canvas", "<TouchpadScroll>"))

    def test_yview_scroll_en_pixeles_NO_existe_en_un_lienzo(self):
        """Por esto se usa `yscrollincrement=1` y no la receta del `Text`."""
        with self.assertRaises(tk.TclError):
            self.lienzo.yview_scroll(30, "pixels")

    def test_una_unidad_del_lienzo_mide_un_pixel(self):
        """Lo que hace que «desplazar en pixeles» sea cierto."""
        self.arriba_del_todo()
        self.lienzo.yview_scroll(37, "units")
        self.raiz.update()
        self.assertEqual(37, self.pixel_de_arriba())


class PruebaDelDesplazadorSuelto(unittest.TestCase):
    """`atar_desplazamiento` sobre un lienzo que ya existia."""

    def test_se_puede_atar_a_un_lienzo_de_fuera(self):
        raiz = tk.Tk()
        raiz.geometry("300x200")
        # `highlightthickness=0` como en `MarcoQueSeDesplaza`. Con el borde de
        # foco por defecto —2 px— `canvasy(0)` devuelve 48 en vez de 50: el
        # lienzo SI se habia desplazado 50, pero la lectura venia corrida por el
        # grosor del borde. Medido, no supuesto.
        lienzo = tk.Canvas(raiz, width=200, height=100, highlightthickness=0)
        lienzo.pack()
        interior = tk.Frame(lienzo)
        lienzo.create_window((0, 0), window=interior, anchor="nw")
        # Relleno de sobra: con 40 renglones el contenido solo daba 48 px de
        # recorrido y el lienzo topaba con el final antes de completar la muesca,
        # asi que la prueba medía el tope y no el desplazamiento.
        for numero in range(RENGLONES_DE_RELLENO):
            tk.Label(interior, text=f"renglón {numero}").pack()
        raiz.update_idletasks()
        lienzo.configure(scrollregion=lienzo.bbox("all"))
        raiz.update()

        desplazador = Desplazador(lienzo)
        lienzo.event_generate("<Enter>", when="now")
        raiz.update()
        lienzo.yview_moveto(0)
        raiz.update()
        lienzo.event_generate("<MouseWheel>", delta=-120, when="now")
        raiz.update()

        self.assertEqual(PIXELES_POR_MUESCA_DE_RUEDA, lienzo.canvasy(0))
        desplazador.soltar()
        raiz.destroy()


if __name__ == "__main__":
    unittest.main()
