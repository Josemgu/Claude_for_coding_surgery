"""Que la pantalla «Revisar» se DIBUJA, no solo que importa.

Un módulo de interfaz que importa sin error puede seguir sin dibujarse: `tkinter`
valida la mayoría de las opciones al crear el control, no al importar. Estas
pruebas construyen la pantalla de verdad sobre una ventana real y comprueban lo que
se ve, con el mismo patrón —y el mismo salto cuando no hay entorno gráfico— que
`pruebas/prueba_pantallas_de_companeros.py`.

Criterio 2 del pase: las tarjetas con sus filtros y su búsqueda, **sin párrafos** y
con **una franja de aviso como máximo**. Las dos últimas se cuentan, no se opinan.
"""

import tkinter as tk
import unittest
from datetime import date

from datos.companeros import alta_de_companero
from datos.esquema import version_de_la_base
from datos.estados import COMPLETA, NO_COMPLETA
from datos.marcas_de_revision import marcar_a_mano
from datos.migraciones_de_revision import VERSION_DE_LA_REVISION
from interfaz.revisar import (
    TABLERO_DE_COMPLETADOS,
    TABLERO_DE_TRABAJO,
    TARJETAS_POR_PAGINA,
    PantallaDeRevisar,
)
from interfaz.tema import aplicar_tema
from pruebas.comun import PruebaConBaseTemporal

HOY = date(2026, 9, 8)

# Lo que separa una etiqueta de un párrafo. El mockup mide 0 párrafos dentro de la
# ventana; aquí se fija el umbral en caracteres de un solo rótulo, que es lo que se
# puede contar sobre la ventana montada.
LARGO_MAXIMO_DE_UN_ROTULO = 90


def _rotulos(widget):
    """Todos los textos visibles del widget y de todo lo que cuelga de él.

    **Mira también dentro de los `Canvas`.** Desde el pase del dibujado, el texto
    de una tarjeta no son etiquetas sino elementos pintados en un lienzo: lo que
    Miguel lee es lo mismo, y estas pruebas siguen midiendo eso.
    """
    textos = []
    try:
        valor = widget.cget("text")
    except (tk.TclError, AttributeError):
        valor = None
    if valor:
        textos.append(str(valor))
    if isinstance(widget, tk.Canvas):
        for elemento in widget.find_all():
            if widget.type(elemento) == "text":
                textos.append(str(widget.itemcget(elemento, "text")))
    for hijo in widget.winfo_children():
        textos.extend(_rotulos(hijo))
    return textos


def _contar_widgets(widget):
    """Cuantos widgets cuelgan de ese, el incluido. Es la cifra que gobierna el pase."""
    return 1 + sum(_contar_widgets(hijo) for hijo in widget.winfo_children())


class PruebaDeLaPantallaDeRevisar(PruebaConBaseTemporal):

    def setUp(self):
        super().setUp()
        # La base la deja al día `aplicar_esquema`, que desde el 2026-09-03 aplica
        # sola la 14 porque está registrada en `MIGRACIONES`. Aquí NO se aplica a
        # mano: `ALTER TABLE ADD COLUMN` no es idempotente y reventaba con
        # «duplicate column name: estado_marcado_por» (DECISIONES.md, 2026-09-03).
        # Se comprueba la versión en vez de aplicarla, que es lo que pide la regla.
        self.assertGreaterEqual(version_de_la_base(self.conexion),
                                VERSION_DE_LA_REVISION)
        try:
            self.raiz = tk.Tk()
        except tk.TclError as causa:
            self.skipTest(f"Esta máquina no tiene entorno gráfico: {causa}")
        self.raiz.withdraw()
        aplicar_tema(self.raiz)
        self.miguel = alta_de_companero(self.conexion, "Miguel")
        self.abiertos = []

        self.completo = self._alta("AAAA2609", "2026-09-10")
        self.incompleto = self._alta("BBBB2609", "2026-09-11")
        self.nuevo = self._alta("CCCC2609", "2026-09-12")
        self.vencido = self._alta("DDDD2609", "2026-09-01")
        marcar_a_mano(self.conexion, self.completo, COMPLETA, self.miguel)
        marcar_a_mano(self.conexion, self.incompleto, NO_COMPLETA, self.miguel)

    def tearDown(self):
        try:
            self.raiz.destroy()
        except tk.TclError:
            pass
        super().tearDown()

    def _alta(self, numero, fecha):
        cursor = self.conexion.execute(
            "INSERT INTO casos (numero_caso, fecha_viaje, creado_en, ruta_pdf) "
            "VALUES (?, ?, '2026-09-01 10:00:00', 'C:\\Escaneos\\ejemplo.pdf')",
            (numero, fecha),
        )
        return cursor.lastrowid

    def pantalla(self):
        return PantallaDeRevisar(
            self.raiz, self.conexion, HOY, self.abiertos.append, self.miguel
        )

    # ---- que se dibuja ---------------------------------------------------

    def test_se_construye_y_ensena_una_tarjeta_por_documento_del_trabajo(self):
        pantalla = self.pantalla()
        self.assertEqual(TABLERO_DE_TRABAJO, pantalla.tablero)
        # Tres: el completo se fue al otro tablero.
        self.assertEqual(3, len(pantalla._tarjetas))

    def test_el_tablero_de_completados_ensena_lo_completo_con_su_firma(self):
        pantalla = self.pantalla()
        pantalla._cambiar_de_tablero(TABLERO_DE_COMPLETADOS)

        self.assertEqual(1, len(pantalla._tarjetas))
        rotulos = _rotulos(pantalla)
        self.assertIn("AAAA2609", rotulos)
        self.assertTrue(any("Miguel" in rotulo for rotulo in rotulos))

    def test_las_pastillas_de_filtro_traen_su_cuenta(self):
        rotulos = _rotulos(self.pantalla())
        self.assertTrue(any(rotulo.startswith("Sin revisar") for rotulo in rotulos))
        self.assertTrue(any(rotulo.startswith("Incompletas") for rotulo in rotulos))

    def test_el_filtro_deja_solo_lo_suyo(self):
        pantalla = self.pantalla()
        pantalla._poner_filtro("incompletas")
        self.assertEqual(1, len(pantalla._tarjetas))
        self.assertEqual(
            self.incompleto, pantalla._tarjetas[0].documento["id"]
        )

    def test_la_busqueda_reduce_la_rejilla(self):
        pantalla = self.pantalla()
        pantalla.busqueda.set("CCCC")
        self.assertEqual(1, len(pantalla._tarjetas))
        self.assertEqual(self.nuevo, pantalla._tarjetas[0].documento["id"])

    def test_escape_devuelve_a_todo(self):
        pantalla = self.pantalla()
        pantalla._poner_filtro("incompletas")
        pantalla._poner_filtro("todo")
        self.assertEqual(3, len(pantalla._tarjetas))

    # ---- criterio 2: sin párrafos y una sola franja ----------------------

    def test_ni_un_parrafo_en_la_pantalla(self):
        """«No pongas tanto texto», dicho por el dueño. Contado, no opinado."""
        largos = [
            rotulo
            for rotulo in _rotulos(self.pantalla())
            if len(rotulo) > LARGO_MAXIMO_DE_UN_ROTULO
        ]
        self.assertEqual([], largos, f"hay {len(largos)} rótulo(s) de párrafo")

    def test_una_franja_de_aviso_como_maximo(self):
        pantalla = self.pantalla()
        self.assertLessEqual(
            len(pantalla._avisos.winfo_children()), 1
        )

    def test_la_franja_se_puede_cerrar(self):
        pantalla = self.pantalla()
        self.assertEqual(1, len(pantalla._avisos.winfo_children()))
        pantalla._avisos.cerrar()
        self.assertEqual(0, len(pantalla._avisos.winfo_children()))

    # ---- lo que hacen los controles --------------------------------------

    def test_pulsar_si_completa_marca_y_mueve_la_tarjeta_al_otro_tablero(self):
        pantalla = self.pantalla()
        documento = next(
            t.documento for t in pantalla._tarjetas if t.documento["id"] == self.nuevo
        )
        pantalla.marcar(documento, COMPLETA)

        self.assertEqual(
            COMPLETA,
            self.conexion.execute(
                "SELECT estado_recomendacion FROM casos WHERE id = ?", (self.nuevo,)
            ).fetchone()[0],
        )
        self.assertNotIn(self.nuevo, [t.documento["id"] for t in pantalla._tarjetas])

    def test_pulsar_una_tarjeta_abre_el_documento(self):
        pantalla = self.pantalla()
        documento = pantalla._tarjetas[0].documento
        pantalla.abrir(documento)
        self.assertEqual([documento], self.abiertos)

    def test_control_a_selecciona_todo_en_completados(self):
        pantalla = self.pantalla()
        pantalla._cambiar_de_tablero(TABLERO_DE_COMPLETADOS)
        pantalla._seleccionar_todo()
        self.assertTrue(all(t.seleccionada.get() for t in pantalla._tarjetas))

    def test_archivar_lo_seleccionado_lo_saca_del_tablero(self):
        pantalla = self.pantalla()
        pantalla._cambiar_de_tablero(TABLERO_DE_COMPLETADOS)
        pantalla._seleccionar_todo()
        # Se llama a `archivar` con la lista ya resuelta para no depender de un
        # cuadro de diálogo: lo que se prueba es el efecto, no el `messagebox`.
        for tarjeta in pantalla._tarjetas:
            from datos.archivo import archivar_caso

            archivar_caso(self.conexion, tarjeta.documento["id"])
        pantalla.refrescar()
        self.assertEqual(0, len(pantalla._tarjetas))

    def test_solo_se_dibuja_una_pagina_y_el_resto_entra_con_ver_mas(self):
        """Con 3 000 documentos, dibujarlos todos no es lento: es inservible.

        `interfaz/barra_de_revisar.py` trae la medición. Aquí se fija la consecuencia:
        el número de tarjetas dibujadas no crece con el archivo, y lo que no se
        dibujó se puede pedir.
        """
        for numero in range(30):
            self._alta("EEEE2609", "2026-09-15")
        pantalla = self.pantalla()

        self.assertEqual(TARJETAS_POR_PAGINA, len(pantalla._tarjetas))
        self.assertIsNotNone(pantalla._ver_mas)
        self.assertIn("sin dibujar", pantalla._ver_mas.cget("text"))

        pantalla._pagina_siguiente()
        self.assertEqual(33, len(pantalla._tarjetas))
        self.assertIsNone(pantalla._ver_mas)

    def test_cambiar_de_filtro_vuelve_a_la_primera_pagina(self):
        for numero in range(30):
            self._alta("EEEE2609", "2026-09-15")
        pantalla = self.pantalla()
        pantalla._pagina_siguiente()
        pantalla._poner_filtro("incompletas")
        self.assertEqual(1, pantalla._pagina)

    def test_la_firma_dice_quien_completo_y_cuando_con_las_palabras_del_dueno(self):
        """«Completada por Sandy · 03-09-2026», literal del dueño el 2026-09-03."""
        sandy = alta_de_companero(self.conexion, "Sandy")
        self.conexion.execute(
            "UPDATE casos SET estado_marcado_por = ?, estado_marcado_en = ?, "
            "estado_del_companero = ?, estado_del_companero_por = ?, "
            "estado_del_companero_en = ? WHERE id = ?",
            (sandy, "2026-09-03 09:14:00", COMPLETA, sandy, "2026-09-03 09:14:00",
             self.completo),
        )
        pantalla = self.pantalla()
        pantalla._cambiar_de_tablero(TABLERO_DE_COMPLETADOS)
        self.assertIn("Completada por Sandy · 03-09-2026", _rotulos(pantalla))

    def test_lo_que_corrige_miguel_deja_ver_lo_que_dijo_el_companero(self):
        sandy = alta_de_companero(self.conexion, "Sandy")
        self.conexion.execute(
            "UPDATE casos SET estado_del_companero = ?, estado_del_companero_por = ?, "
            "estado_del_companero_en = ? WHERE id = ?",
            (COMPLETA, sandy, "2026-09-03 09:14:00", self.incompleto),
        )
        firmas = [r for r in _rotulos(self.pantalla()) if "Corregida" in r]
        self.assertEqual(1, len(firmas))
        self.assertIn("Corregida por Miguel", firmas[0])
        self.assertIn("Sandy dijo: completa", firmas[0])

    def test_el_atajo_se_suelta_al_salir(self):
        pantalla = self.pantalla()
        pantalla.soltar_atajos()
        self.assertEqual("", self.raiz.bind("<Control-a>"))

    # ---- cuantos controles cuesta una tarjeta ----------------------------

    def test_una_tarjeta_no_pasa_de_ocho_controles(self):
        """El texto de la tarjeta se dibuja; los botones siguen siendo botones.

        **De dónde sale el techo.** Antes de este pase una tarjeta costaba **14
        widgets** (medido en esta máquina con 30 documentos: 359 widgets para 24
        tarjetas), y de esos solo cuatro eran controles con los que se hace algo:
        los dos botones de marcar, el de asignar y —en «Completados»— la casilla.
        Los otros diez eran marcos y etiquetas, o sea ventanas del escritorio de
        Windows a unos 5 ms cada una para enseñar texto.

        Lo que NO se toca son los botones: en una tarjeta archivada siguen tomando
        el foco a propósito, porque es la única forma de llegar con Tab y leer por
        qué no se pueden pulsar.
        """
        tarjeta = self.pantalla()._tarjetas[0]
        cuantos = _contar_widgets(tarjeta)
        self.assertLessEqual(
            cuantos, 8, f"una tarjeta cuesta {cuantos} widgets y el techo son 8"
        )

    def test_la_tarjeta_sigue_diciendo_lo_mismo(self):
        """El ahorro no vale nada si se lleva por delante lo que había que leer."""
        textos = " ".join(_rotulos(self.pantalla()))
        for esperado in ("BBBB2609", "Sí, completa", "No está completa", "Sin asignar"):
            self.assertIn(esperado, textos)

    # ---- rueda y trackpad ------------------------------------------------

    def test_el_gesto_del_trackpad_baja_la_rejilla(self):
        """El eje que mueve la rejilla es el VERTICAL, y estaba tomando el otro.

        `tk::PreciseScrollDeltas` desempaqueta `%D` con **x en los 16 bits altos e
        y en los bajos**. Esta pantalla leía los altos y los usaba como si fueran
        el vertical: un gesto de dos dedos hacia abajo —que trae x en cero— movía
        cero, y un gesto lateral la hacía saltar. Con un solo ayudante para toda la
        aplicación (`interfaz/desplazamiento.py`) el desempaquetado es el de Tk y
        deja de haber dos versiones de la misma cuenta.
        """
        self.raiz.deiconify()
        self.raiz.attributes("-alpha", 0.0)
        self.raiz.geometry("900x400")
        pantalla = self.pantalla()
        pantalla.grid(row=0, column=0, sticky="nsew")
        self.raiz.update()
        lienzo = pantalla._rueda.lienzo
        lienzo.event_generate("<Enter>", when="now")
        self.raiz.update()
        # La region desplazable se fija DESPUES del ultimo `update`: cada control
        # que entra dispara `<Configure>`, y ese manejador la recalcula a lo que
        # ocupan las cuatro tarjetas de esta prueba, que no da recorrido para
        # medir nada.
        lienzo.configure(scrollregion=(0, 0, 800, 4000))
        lienzo.yview_moveto(0)
        # x en cero, y hacia abajo: es lo que manda un deslizamiento vertical.
        lienzo.event_generate(
            "<TouchpadScroll>", delta=(0 << 16) | (-40 & 0xFFFF), when="now"
        )
        self.assertGreater(
            lienzo.canvasy(0), 0, "el gesto vertical del trackpad no movió la rejilla"
        )

    def test_al_salir_no_queda_atada_la_rueda_en_toda_la_ventana(self):
        """Lo mismo que en corrección: `bind_all` reemplaza, no suma."""
        pantalla = self.pantalla()
        pantalla.grid(row=0, column=0, sticky="nsew")
        self.raiz.update()
        pantalla._rueda.lienzo.event_generate("<Enter>", when="now")
        self.raiz.update()
        pantalla.soltar_atajos()
        for evento in ("<MouseWheel>", "<TouchpadScroll>"):
            self.assertEqual(
                "",
                self.raiz.bind_all(evento),
                f"{evento} siguió atado a toda la ventana al salir de Revisar",
            )


if __name__ == "__main__":
    unittest.main()
