"""Guardar se ve, un campo que no vale no tumba el guardado, y la cabecera es una linea.

**Lo que dijo el dueno (2026-09-04):** «El botón Guardar no funciona cuando se
revisa y se coloca la información en un lateral faltante: no hace nada. Y en esa
misma página las letras en amarillo toman todo el espacio, es demasiado texto».

**Lo medido antes de tocar nada**, sobre la copia de una importacion real de dos
PDF (caso 1, cuatro personas, la tercera sin MRN):

    MRN de la persona 3 = '123-4567-8901'
      despues: (3, 'Kevin Fulano', '123-4567-8901')   ← se guardo
      cuadros que salieron: 0
      acuse del pie: (esta version no tiene acuse en el pie)

    MRN de la persona 3 = '123'
      despues: (3, 'Kevin Fulano', None)              ← NO se guardo
      cuadros que salieron: 1
         avisar_de_un_error: No se pudo guardar | El campo 'mrn' no vale...

Y la cabecera, sobre un caso con los cinco avisos: **5 etiquetas, la peor de 5
lineas, 310 px de avisos** y la region central reducida a 241 px de una ventana
de 720.

Las pruebas de aqui salen del criterio de aceptacion del pase, no del codigo:
pulsan **el boton** con `invoke()` —no `guardar()`— porque lo que el dueno dice
que no funciona es el boton, y una prueba que llama al metodo por dentro no
habria visto nunca que el boton no dice nada.
"""

import shutil
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from datos.conexion import abrir_conexion
from datos.esquema import aplicar_esquema
from datos.ilegibles import (
    ANCLAS_PERDIDAS,
    MOTIVOS,
    SIN_NUMERO_DE_CASO,
    anotar_documento_ilegible,
    linea_del_motivo,
)
from datos.repositorio import alta_de_caso, alta_de_persona
from interfaz.acuse_de_guardado import (
    SEGUNDOS_QUE_SE_VE_EL_ACUSE,
    texto_del_acuse,
)
from interfaz.avisos_de_correccion import (
    LARGO_MAXIMO_DE_LA_LINEA,
    agrupados_por_linea,
    aviso_de_lo_que_vio_la_importacion,
)

ANCHO_DE_LA_VENTANA = 1100
ALTO_DE_LA_VENTANA = 720

# El tope de lineas de una etiqueta de aviso, que es el criterio de aceptacion del
# pase escrito como numero: «ninguna Label con mas de 2 lineas de texto de aviso».
LINEAS_MAXIMAS_DE_UNA_ETIQUETA = 2


def _hay_ventanas():
    """Si no se puede crear un Tk, estas pruebas se omiten en vez de fallar."""
    try:
        import tkinter

        raiz = tkinter.Tk()
    except Exception:  # pragma: no cover - depende de la maquina, no del codigo
        return False
    raiz.destroy()
    return True


def _lineas_de(widget):
    """Cuantas lineas de texto ocupa esa etiqueta ya dibujada."""
    import tkinter.font as tkfont

    letra = tkfont.Font(font=widget.cget("font"))
    return max(1, round((widget.winfo_height() - _relleno_de(widget)) / letra.metrics("linespace")))


def _relleno_de(widget):
    """El `pady` de arriba y abajo, o cero: un `ttk.Label` no tiene esa opcion."""
    import tkinter as tk

    try:
        return 2 * int(str(widget.cget("pady")))
    except (tk.TclError, ValueError):
        return 0


def _etiquetas_con_texto(marco):
    """Cada etiqueta con texto de todo el arbol que cuelga de ese marco."""
    etiquetas = []
    pila = list(marco.winfo_children())
    while pila:
        widget = pila.pop(0)
        pila.extend(widget.winfo_children())
        if widget.winfo_class() in ("Label", "TLabel") and (widget.cget("text") or "").strip():
            etiquetas.append(widget)
    return etiquetas


def _boton_de(pantalla, rotulo):
    """El boton cuyo rotulo empieza por eso, buscado en el arbol entero.

    Se busca por el rotulo y no por un atributo a proposito: es lo que ve quien
    usa el programa, y una prueba que guardara la referencia al construir seguiria
    pasando aunque el boton dejara de dibujarse.
    """
    pila = [pantalla]
    while pila:
        widget = pila.pop()
        pila.extend(widget.winfo_children())
        try:
            texto = widget.cget("text")
        except Exception:  # pragma: no cover - widgets sin `text`
            continue
        if (
            isinstance(texto, str)
            and texto.startswith(rotulo)
            and widget.winfo_class() in ("TButton", "Button")
        ):
            return widget
    raise LookupError(f"No hay ningún botón que empiece por {rotulo!r}")


class ElTextoDelAcuseSeLeeSolo(unittest.TestCase):
    """La linea del pie, sin ventana: se comprueba palabra por palabra."""

    def test_lo_guardado_lleva_la_hora_y_la_cuenta(self):
        self.assertEqual(
            "Guardado a las 09:41 · 2 campos",
            texto_del_acuse("09:41", 2, ()),
        )

    def test_un_solo_campo_va_en_singular(self):
        self.assertEqual(
            "Guardado a las 09:41 · 1 campo",
            texto_del_acuse("09:41", 1, ()),
        )

    def test_guardar_sin_haber_cambiado_nada_lo_dice(self):
        self.assertEqual(
            "Guardado a las 09:41 · sin cambios",
            texto_del_acuse("09:41", 0, ()),
        )

    def test_el_campo_que_no_vale_se_nombra(self):
        """El pase lo pide con esta frase literal."""
        self.assertEqual(
            "Guardado · 1 campo sin guardar: MRN",
            texto_del_acuse("09:41", 3, ("MRN",)),
        )

    def test_dos_campos_que_no_valen_se_nombran_los_dos(self):
        self.assertEqual(
            "Guardado · 2 campos sin guardar: MRN, N.º de unidad",
            texto_del_acuse("09:41", 1, ("MRN", "N.º de unidad")),
        )

    def test_el_acuse_no_se_queda_para_siempre(self):
        """Un «guardado» que sigue en pantalla diez minutos despues miente."""
        self.assertGreaterEqual(SEGUNDOS_QUE_SE_VE_EL_ACUSE, 3)
        self.assertLessEqual(SEGUNDOS_QUE_SE_VE_EL_ACUSE, 20)


class CadaMotivoSabeDecirseEnUnaLinea(unittest.TestCase):
    """La franja de avisos pone la cuenta delante: la frase tiene que caber."""

    def test_los_nueve_motivos_tienen_su_linea(self):
        for motivo in MOTIVOS:
            with self.subTest(motivo=motivo):
                self.assertNotEqual(motivo, linea_del_motivo(motivo))

    def test_ninguna_linea_pasa_del_tope(self):
        for motivo in MOTIVOS:
            with self.subTest(motivo=motivo):
                self.assertLessEqual(len(linea_del_motivo(motivo)), LARGO_MAXIMO_DE_LA_LINEA)

    def test_dos_motivos_distintos_no_dicen_lo_mismo(self):
        lineas = [linea_del_motivo(motivo) for motivo in MOTIVOS]
        self.assertEqual(len(lineas), len(set(lineas)))

    def test_un_motivo_que_no_consta_devuelve_el_codigo(self):
        """Igual que `frase_del_motivo`: se ve, no tumba la pantalla."""
        self.assertEqual("inventado", linea_del_motivo("inventado"))


class LaFranjaVuelveCuandoVuelveElAviso(unittest.TestCase):
    """Un aviso que va y viene tiene que volver a verse. Medido: no volvia.

    En Correccion el aviso del mes cruzado aparece y desaparece con cada tecla de
    la fecha de viaje, asi que la franja se queda sin avisos varias veces por
    segundo. Con `pack_forget()` a secas no volvia a verse nunca.
    """

    @classmethod
    def setUpClass(cls):
        if not _hay_ventanas():  # pragma: no cover - depende de la maquina
            raise unittest.SkipTest("esta maquina no puede abrir ventanas")

    def setUp(self):
        import tkinter as tk
        from tkinter import ttk

        from interfaz.avisos import FranjaDeAvisos
        from interfaz.tema import aplicar_tema

        self.raiz = tk.Tk()
        self.raiz.geometry(f"{ANCHO_DE_LA_VENTANA}x400")
        self.raiz.attributes("-alpha", 0.0)
        aplicar_tema(self.raiz)
        marco = ttk.Frame(self.raiz)
        marco.pack(fill="x")
        self.franja = FranjaDeAvisos(marco)
        self.franja.pack(fill="x")
        self.raiz.update()

    def tearDown(self):
        self.raiz.destroy()

    def _poner(self, cuantos):
        from interfaz.avisos import Aviso

        self.franja.poner([Aviso("hojas que no se pudieron leer", 3)] * cuantos)
        self.raiz.update()

    def test_con_avisos_se_ve(self):
        self._poner(1)
        self.assertTrue(self.franja.winfo_ismapped())

    def test_sin_avisos_no_ocupa_sitio(self):
        self._poner(1)
        self._poner(0)
        self.assertFalse(self.franja.winfo_ismapped())

    def test_y_cuando_el_aviso_vuelve_la_franja_vuelve(self):
        self._poner(1)
        self._poner(0)
        self._poner(1)
        self.assertTrue(self.franja.winfo_ismapped())

    def test_volver_a_poner_LOS_MISMOS_avisos_no_reabre_lo_cerrado(self):
        """En Correccion esto corre con cada tecla: sin ello, cerrar no dura nada."""
        self._poner(1)
        self.franja.cerrar()
        self._poner(1)
        self.assertTrue(self.franja.esta_cerrada())
        self.assertFalse(self.franja.winfo_ismapped())

    def test_un_aviso_NUEVO_si_la_reabre(self):
        from interfaz.avisos import Aviso

        self._poner(1)
        self.franja.cerrar()
        self.franja.poner([Aviso("documentos duplicados sin decidir", 1)])
        self.raiz.update()
        self.assertFalse(self.franja.esta_cerrada())
        self.assertTrue(self.franja.winfo_ismapped())

    def test_cerrarla_y_reabrirla_tambien_la_devuelve(self):
        self._poner(1)
        self.franja.cerrar()
        self.raiz.update()
        self.assertFalse(self.franja.winfo_ismapped())
        self.franja.reabrir()
        self.raiz.update()
        self.assertTrue(self.franja.winfo_ismapped())


class DosAvisosIgualesNoSeComenUnoAlOtro(unittest.TestCase):
    """Agrupar no puede perder el detalle del segundo. Sin ventana."""

    def _dos_del_mismo_motivo(self):
        return [
            aviso_de_lo_que_vio_la_importacion(ANCLAS_PERDIDAS, 1, "Nombre completo"),
            aviso_de_lo_que_vio_la_importacion(ANCLAS_PERDIDAS, 2, "Unidad"),
        ]

    def test_los_dos_se_cuentan_en_una_sola_linea(self):
        agrupados = agrupados_por_linea(self._dos_del_mismo_motivo())
        self.assertEqual(1, len(agrupados))
        self.assertEqual(2, agrupados[0][1])

    def test_el_detalle_trae_lo_que_se_vio_en_LAS_DOS_paginas(self):
        detalle = agrupados_por_linea(self._dos_del_mismo_motivo())[0][2]
        self.assertIn("página 1 del PDF: Nombre completo", detalle)
        self.assertIn("página 2 del PDF: Unidad", detalle)

    def test_dos_lineas_distintas_siguen_siendo_dos(self):
        agrupados = agrupados_por_linea(
            [
                aviso_de_lo_que_vio_la_importacion(ANCLAS_PERDIDAS, 1, "Unidad"),
                aviso_de_lo_que_vio_la_importacion(SIN_NUMERO_DE_CASO, 2, None),
            ]
        )
        self.assertEqual(2, len(agrupados))


class PantallaConUnaPersonaSinMrn(unittest.TestCase):
    """Un caso de cuatro personas con la tercera sin MRN, como el del dueno."""

    @classmethod
    def setUpClass(cls):
        if not _hay_ventanas():  # pragma: no cover - depende de la maquina
            raise unittest.SkipTest("esta maquina no puede abrir ventanas")

    def setUp(self):
        import tkinter as tk

        from interfaz.correccion import PantallaDeCorreccion
        from interfaz.tema import aplicar_tema

        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_acuse_"))
        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        aplicar_esquema(self.conexion)
        caso = alta_de_caso(
            self.conexion,
            numero_caso="BARC2608",
            fecha_viaje="2026-08-25",
            unidad_numero="123456",
            unidad_nombre="Barcelona",
        )
        self.caso_id = caso.id
        self.personas = [
            alta_de_persona(
                self.conexion, caso.id,
                mrn=None if fila == 3 else f"077-3333-{3330 + fila:04d}",
                nombre=f"PERSONA {fila}", fila_formulario=fila,
            )
            for fila in (1, 2, 3, 4)
        ]

        self.raiz = tk.Tk()
        self.raiz.geometry(f"{ANCHO_DE_LA_VENTANA}x{ALTO_DE_LA_VENTANA}")
        self.raiz.attributes("-alpha", 0.0)
        self.raiz.rowconfigure(0, weight=1)
        self.raiz.columnconfigure(0, weight=1)
        aplicar_tema(self.raiz)
        self.pantalla = PantallaDeCorreccion(
            self.raiz, self.conexion, caso.id, al_volver=lambda: None,
            carpeta_de_datos=self.carpeta, al_archivar=lambda: None,
        )
        self.pantalla.grid(row=0, column=0, sticky="nsew")
        self.raiz.update()
        self.raiz.focus_force()
        self.raiz.update()

    def tearDown(self):
        self.pantalla.soltar_atajos()
        self.conexion.close()
        self.raiz.destroy()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    # ---- utilidades de la prueba ----------------------------------------

    def _teclear(self, entrada, texto):
        """Escribe eso como si se hubiera tecleado, con su revalidacion.

        Sin `focus_force` y sin `when="now"` Tk **no entrega** el `<KeyRelease>` y
        el campo no se revalida: medido en esta maquina, el campo se quedaba en
        «revisar» con un MRN de tres digitos dentro.
        """
        entrada.focus_force()
        self.raiz.update()
        entrada.delete(0, "end")
        entrada.insert(0, texto)
        entrada.event_generate("<KeyRelease>", when="now")
        self.raiz.update()

    def _mrn_de(self, fila):
        return self.pantalla._bloques[fila - 1].campos["mrn"]

    def _mrn_guardado(self, fila):
        return self.conexion.execute(
            "SELECT mrn FROM personas WHERE caso_id = ? AND fila_formulario = ?",
            (self.caso_id, fila),
        ).fetchone()["mrn"]

    def _sin_cuadros(self):
        """Anota los cuadros que habrian salido, en vez de abrirlos."""
        return mock.patch.multiple(
            "interfaz.correccion.dialogos",
            informar=mock.DEFAULT, advertir=mock.DEFAULT,
            avisar_de_un_error=mock.DEFAULT,
        )


class ElBotonGuardarDiceQueGuardo(PantallaConUnaPersonaSinMrn):
    """Dado un MRN vacio, cuando se rellena con uno bueno y se pulsa el boton,
    entonces la base cambia y el pie lo dice donde se esta mirando."""

    def test_el_mrn_llega_a_la_base(self):
        self._teclear(self._mrn_de(3).entrada, "123-4567-8901")
        with self._sin_cuadros():
            _boton_de(self.pantalla, "Guardar").invoke()
        self.assertEqual("123-4567-8901", self._mrn_guardado(3))

    def test_el_pie_dice_que_se_guardo_y_cuantos_campos(self):
        self._teclear(self._mrn_de(3).entrada, "123-4567-8901")
        with self._sin_cuadros():
            _boton_de(self.pantalla, "Guardar").invoke()
        acuse = self.pantalla._acuse.cget("text")
        self.assertRegex(acuse, r"^Guardado a las \d\d:\d\d · 1 campo$")

    def test_el_boton_no_cambia_de_sitio(self):
        """Un boton que se mueve al pulsarlo es un boton que se falla al repetir."""
        boton = _boton_de(self.pantalla, "Guardar")
        antes = (boton.winfo_rootx(), boton.winfo_rooty())
        self._teclear(self._mrn_de(3).entrada, "123-4567-8901")
        with self._sin_cuadros():
            boton.invoke()
        self.raiz.update()
        self.assertEqual(antes, (boton.winfo_rootx(), boton.winfo_rooty()))

    def test_con_el_atajo_de_teclado_dice_lo_mismo(self):
        self._teclear(self._mrn_de(3).entrada, "123-4567-8901")
        with self._sin_cuadros():
            self.raiz.event_generate("<Control-g>", when="now")
        self.raiz.update()
        self.assertEqual("123-4567-8901", self._mrn_guardado(3))
        self.assertRegex(self.pantalla._acuse.cget("text"), r"^Guardado a las \d\d:\d\d · 1 campo$")

    def test_guardar_sin_tocar_nada_tambien_acusa_recibo(self):
        with self._sin_cuadros():
            _boton_de(self.pantalla, "Guardar").invoke()
        self.assertRegex(
            self.pantalla._acuse.cget("text"), r"^Guardado a las \d\d:\d\d · sin cambios$"
        )

    def test_cambiar_solo_el_estado_no_se_cuenta_como_sin_cambios(self):
        """El estado se escribe con el caso: si cambia, el pie no puede decir «sin cambios»."""
        from datos.estados import COMPLETA

        self.pantalla._seguimiento._estado.set(COMPLETA)
        with self._sin_cuadros():
            _boton_de(self.pantalla, "Guardar").invoke()
        self.assertRegex(
            self.pantalla._acuse.cget("text"), r"^Guardado a las \d\d:\d\d · 1 campo$"
        )
        self.assertEqual(
            COMPLETA,
            self.conexion.execute(
                "SELECT estado_recomendacion FROM casos WHERE id = ?", (self.caso_id,)
            ).fetchone()["estado_recomendacion"],
        )

    def test_el_acuse_se_borra_solo(self):
        self._teclear(self._mrn_de(3).entrada, "123-4567-8901")
        with self._sin_cuadros():
            _boton_de(self.pantalla, "Guardar").invoke()
        self.assertNotEqual("", self.pantalla._acuse.cget("text"))
        self.pantalla._acuse.limpiar()
        self.assertEqual("", self.pantalla._acuse.cget("text"))


class UnCampoQueNoValeNoTumbaElGuardado(PantallaConUnaPersonaSinMrn):
    """Dado un MRN mal escrito, cuando se pulsa Guardar, entonces se guarda todo
    lo demas, no sale ningun cuadro, y el pie nombra el campo que se quedo."""

    def _con_un_cambio_bueno_y_uno_malo(self, malo):
        """Cambia el nombre de la unidad —que si vale— y el MRN de la 3 —que no—."""
        self._teclear(self.pantalla._campos_del_caso["unidad_nombre"].entrada, "Barcelona Sur")
        self._teclear(self._mrn_de(3).entrada, malo)

    def _unidad_guardada(self):
        return self.conexion.execute(
            "SELECT unidad_nombre FROM casos WHERE id = ?", (self.caso_id,)
        ).fetchone()["unidad_nombre"]

    def test_lo_valido_se_guarda(self):
        self._con_un_cambio_bueno_y_uno_malo("123")
        with self._sin_cuadros():
            _boton_de(self.pantalla, "Guardar").invoke()
        self.assertEqual("Barcelona Sur", self._unidad_guardada())

    def test_el_campo_que_no_vale_no_se_escribe(self):
        self._con_un_cambio_bueno_y_uno_malo("123")
        with self._sin_cuadros():
            _boton_de(self.pantalla, "Guardar").invoke()
        self.assertIsNone(self._mrn_guardado(3))

    def test_las_otras_personas_se_guardan_igual(self):
        """El MRN malo esta en la 3 de 4: sin esto la 4 se perdia."""
        self._teclear(self._mrn_de(4).entrada, "077-3333-9999")
        self._teclear(self._mrn_de(3).entrada, "123")
        with self._sin_cuadros():
            _boton_de(self.pantalla, "Guardar").invoke()
        self.assertEqual("077-3333-9999", self._mrn_guardado(4))

    def test_no_sale_ningun_cuadro(self):
        self._con_un_cambio_bueno_y_uno_malo("123")
        with self._sin_cuadros() as cuadros:
            _boton_de(self.pantalla, "Guardar").invoke()
        for nombre, cuadro in cuadros.items():
            with self.subTest(cuadro=nombre):
                cuadro.assert_not_called()

    def test_el_pie_nombra_el_campo_que_se_quedo(self):
        self._con_un_cambio_bueno_y_uno_malo("123")
        with self._sin_cuadros():
            _boton_de(self.pantalla, "Guardar").invoke()
        self.assertEqual(
            "Guardado · 1 campo sin guardar: MRN", self.pantalla._acuse.cget("text")
        )

    def test_el_campo_se_queda_en_rojo_con_su_motivo_en_una_linea(self):
        self._con_un_cambio_bueno_y_uno_malo("123")
        with self._sin_cuadros():
            _boton_de(self.pantalla, "Guardar").invoke()
        self.raiz.update()
        campo = self._mrn_de(3)
        self.assertEqual("no_valido", campo.estado())
        self.assertEqual("123", campo.entrada.get())
        self.assertLessEqual(_lineas_de(campo._aviso), LINEAS_MAXIMAS_DE_UNA_ETIQUETA)

    def test_una_unidad_de_once_digitos_hace_lo_mismo(self):
        """El otro caso que reprodujo el supervisor, y por el mismo camino."""
        self._teclear(self._mrn_de(4).entrada, "077-3333-9999")
        self._teclear(self.pantalla._campos_del_caso["unidad_numero"].entrada, "12345678901")
        with self._sin_cuadros() as cuadros:
            _boton_de(self.pantalla, "Guardar").invoke()
        self.assertEqual("077-3333-9999", self._mrn_guardado(4))
        self.assertEqual(
            "Guardado · 1 campo sin guardar: N.º de unidad",
            self.pantalla._acuse.cget("text"),
        )
        cuadros["avisar_de_un_error"].assert_not_called()

    def test_el_numero_de_unidad_malo_no_llega_a_la_base(self):
        self._teclear(self.pantalla._campos_del_caso["unidad_numero"].entrada, "12345678901")
        with self._sin_cuadros():
            _boton_de(self.pantalla, "Guardar").invoke()
        self.assertEqual(
            "123456",
            self.conexion.execute(
                "SELECT unidad_numero FROM casos WHERE id = ?", (self.caso_id,)
            ).fetchone()["unidad_numero"],
        )


class LaCabeceraDeCorreccionEsUnaLinea(unittest.TestCase):
    """Dado un caso con los cinco avisos, entonces la cabecera ocupa una franja.

    Medido antes de este pase sobre este mismo caso: **5 etiquetas, la peor de 5
    lineas, 310 px de avisos**, y la region central en 241 px de 720.
    """

    @classmethod
    def setUpClass(cls):
        if not _hay_ventanas():  # pragma: no cover - depende de la maquina
            raise unittest.SkipTest("esta maquina no puede abrir ventanas")

    def setUp(self):
        import tkinter as tk

        from interfaz.correccion import PantallaDeCorreccion
        from interfaz.tema import aplicar_tema

        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_cabecera_"))
        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        aplicar_esquema(self.conexion)
        caso = alta_de_caso(
            self.conexion, numero_caso=None, fecha_viaje="2026-10-08", captura_manual=1,
            ruta_pdf=str(self.carpeta / "no_esta.pdf"), pagina_pdf=1,
        )
        alta_de_persona(
            self.conexion, caso.id, mrn="055-1111-3853", nombre="PERSONA 1",
            fila_formulario=1,
        )
        anotar_documento_ilegible(
            self.conexion, str(self.carpeta / "no_esta.pdf"), SIN_NUMERO_DE_CASO,
            pagina_pdf=1, detalle="BARC2608 · Fulano · fila 1 de la hoja",
            caso_id=caso.id,
        )
        anotar_documento_ilegible(
            self.conexion, str(self.carpeta / "no_esta.pdf"), ANCLAS_PERDIDAS,
            pagina_pdf=2, detalle="Nombre completo, Número de registro de miembro",
            caso_id=caso.id,
        )

        self.raiz = tk.Tk()
        self.raiz.geometry(f"{ANCHO_DE_LA_VENTANA}x{ALTO_DE_LA_VENTANA}")
        self.raiz.attributes("-alpha", 0.0)
        self.raiz.rowconfigure(0, weight=1)
        self.raiz.columnconfigure(0, weight=1)
        aplicar_tema(self.raiz)
        self.pantalla = PantallaDeCorreccion(
            self.raiz, self.conexion, caso.id, al_volver=lambda: None,
            carpeta_de_datos=self.carpeta,
        )
        self.pantalla.grid(row=0, column=0, sticky="nsew")
        self.raiz.update()

    def tearDown(self):
        self.pantalla.soltar_atajos()
        self.conexion.close()
        self.raiz.destroy()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def test_los_cinco_avisos_siguen_estando(self):
        """Acortar no es perder: los cinco textos completos siguen ahi."""
        self.assertEqual(5, len(self.pantalla._textos_de_aviso()))
        self.assertEqual(5, self.pantalla.franja.cuantos())

    def test_ninguna_etiqueta_de_aviso_es_un_parrafo(self):
        for etiqueta in _etiquetas_con_texto(self.pantalla._marco_de_avisos):
            with self.subTest(texto=etiqueta.cget("text")[:40]):
                self.assertLessEqual(_lineas_de(etiqueta), LINEAS_MAXIMAS_DE_UNA_ETIQUETA)

    def test_cada_linea_cabe_en_el_tope(self):
        for aviso in self.pantalla.franja._avisos:
            with self.subTest(linea=aviso.texto):
                self.assertLessEqual(len(aviso.texto), LARGO_MAXIMO_DE_LA_LINEA)

    def test_la_franja_se_puede_cerrar_y_la_cuenta_no_se_pierde(self):
        self.pantalla.franja.cerrar()
        self.raiz.update()
        self.assertTrue(self.pantalla.franja.esta_cerrada())
        self.assertEqual("🔔 5", self.pantalla._campana.cget("text"))

    def test_el_detalle_largo_no_esta_en_la_cabecera(self):
        """Lo que se vio en cada pagina se lee al pedirlo, no ocupando la pantalla."""
        pintado = " ".join(
            etiqueta.cget("text")
            for etiqueta in _etiquetas_con_texto(self.pantalla._marco_de_avisos)
        )
        self.assertNotIn("Lo que se vio", pintado)
        self.assertIn(
            "Lo que se vio", " ".join(self.pantalla._textos_de_aviso())
        )

    def test_la_region_central_gana_el_sitio_que_dejan_los_avisos(self):
        """El numero que se movio: 241 px de region central antes de este pase."""
        self.assertGreater(self.pantalla._lienzo.winfo_height(), 400)

    def test_el_marco_de_avisos_cabe_en_una_linea(self):
        self.assertLessEqual(self.pantalla._marco_de_avisos.winfo_height(), 40)

    def test_desplegar_el_detalle_cuesta_lo_mismo_haya_los_avisos_que_haya(self):
        """La regla 3 del dueno, medida: el desplegable tiene alto MAXIMO.

        Abrir «ver» si mueve los campos hacia abajo —96 px medidos aqui, igual que
        en Inicio, y solo mientras esta abierto y porque alguien lo pidio—. Lo que
        la regla prohibe es que ese coste **crezca con el numero de avisos**: cinco
        avisos empujan lo mismo que uno, porque el detalle se desplaza dentro de su
        caja. Eso es lo que se comprueba.
        """
        from interfaz.avisos import ALTO_MAXIMO_ABIERTA

        antes = self.pantalla._lienzo.winfo_rooty()
        self.pantalla.franja.alternar()
        self.raiz.update()
        empujon = self.pantalla._lienzo.winfo_rooty() - antes
        self.assertEqual(5, self.pantalla.franja.cuantos())
        self.assertLessEqual(empujon, ALTO_MAXIMO_ABIERTA)


if __name__ == "__main__":
    unittest.main()
