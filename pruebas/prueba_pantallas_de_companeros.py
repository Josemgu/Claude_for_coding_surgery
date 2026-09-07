"""Que las cuatro pantallas nuevas se DIBUJAN, no solo que importan.

Un modulo de interfaz que importa sin error puede seguir sin dibujarse: `tkinter`
valida la mayoria de las opciones en el momento de crear el control, no al
importar. Ese fallo ya se cobro uno en esta misma fase —un `pady=(5, 0)` en un
`tk.Label`, que Tk rechaza con «expected screen distance but got "5 0"»— y no lo
habria visto ninguna prueba que solo importara el modulo.

Estas pruebas construyen cada pantalla de verdad sobre una ventana real y
comprueban lo que se ve. No sustituyen a QA: comprueban que la pantalla existe y
que dice lo que tiene que decir, no que se use bien.

Si esta maquina no tiene entorno grafico, las pruebas se saltan enteras en vez de
fallar: un fallo de entorno disfrazado de fallo de codigo hace perder mas tiempo
que la prueba que ahorra.
"""

import tkinter as tk
import unittest
from datetime import date

from datos.asignaciones import asignar_caso
from datos.companeros import alta_de_companero, desactivar_companero
from datos.contactos import registrar_contacto
from datos.propuestas import guardar_propuesta, resolver_persona_por_par
from datos.repositorio import alta_de_caso, alta_de_persona
from interfaz.seguimiento import (
    hay_que_volver_a_llamar,
    resumen_del_ultimo_contacto,
    texto_de_los_dias,
)
from interfaz.tema import aplicar_tema
from pruebas.comun import PruebaConBaseTemporal

HOY = date(2026, 9, 12)


def _textos(widget):
    """Todos los textos que se ven en un widget y en todo lo que cuelga de el."""
    textos = []
    try:
        valor = widget.cget("text")
    except (tk.TclError, AttributeError):
        valor = None
    if valor:
        textos.append(str(valor))
    for hijo in widget.winfo_children():
        textos.extend(_textos(hijo))
    return textos


class PruebaConVentana(PruebaConBaseTemporal):
    """Una ventana de verdad, y una base temporal debajo."""

    def setUp(self):
        super().setUp()
        try:
            self.raiz = tk.Tk()
        except tk.TclError as causa:
            self.skipTest(f"Esta máquina no tiene entorno gráfico: {causa}")
        self.raiz.withdraw()
        aplicar_tema(self.raiz)

    def tearDown(self):
        try:
            self.raiz.destroy()
        except tk.TclError:
            pass
        super().tearDown()


class PruebaDeLaPantallaDeCompaneros(PruebaConVentana):
    def test_se_dibuja_vacia_con_una_frase_en_espanol(self):
        from interfaz.companeros import PantallaDeCompaneros

        pantalla = PantallaDeCompaneros(self.raiz, self.conexion, al_volver=lambda: None)
        self.raiz.update_idletasks()

        self.assertTrue(
            any("Todavía no hay ningún compañero" in texto for texto in _textos(pantalla))
        )

    def test_un_desactivado_se_ve_marcado_como_tal(self):
        from interfaz.companeros import PantallaDeCompaneros

        companero_id = alta_de_companero(self.conexion, "Ana Pérez")
        desactivar_companero(self.conexion, companero_id)

        pantalla = PantallaDeCompaneros(self.raiz, self.conexion, al_volver=lambda: None)
        self.raiz.update_idletasks()

        textos = " ".join(_textos(pantalla))
        self.assertIn("Ana Pérez", textos)
        self.assertIn("DESACTIVADO", textos)

    def test_no_hay_ningun_boton_de_borrar(self):
        """La ausencia del botón es la función principal de esta pantalla."""
        from interfaz.companeros import PantallaDeCompaneros

        alta_de_companero(self.conexion, "Ana Pérez")
        pantalla = PantallaDeCompaneros(self.raiz, self.conexion, al_volver=lambda: None)
        self.raiz.update_idletasks()

        for texto in _textos(pantalla):
            self.assertNotIn("Borrar", texto)
            self.assertNotIn("Eliminar", texto)


class PruebaDeLaPantallaDeAsignacion(PruebaConVentana):
    def test_se_dibuja_sin_companeros_y_dice_que_hacer(self):
        from interfaz.asignacion import PantallaDeAsignacion

        pantalla = PantallaDeAsignacion(
            self.raiz, self.conexion, al_volver=lambda: None, hoy=HOY
        )
        self.raiz.update_idletasks()

        self.assertTrue(
            any("No hay ningún compañero activo" in texto for texto in _textos(pantalla))
        )

    def test_ensena_los_casos_asignados_del_companero_elegido(self):
        from interfaz.asignacion import PantallaDeAsignacion

        companero_id = alta_de_companero(self.conexion, "Ana Pérez")
        caso_id = alta_de_caso(self.conexion, "CASP2609", fecha_viaje="2026-09-16").id
        alta_de_persona(self.conexion, caso_id, mrn="055-1111-3853", nombre="Quien sea")
        asignar_caso(self.conexion, caso_id, companero_id)

        pantalla = PantallaDeAsignacion(
            self.raiz, self.conexion, al_volver=lambda: None, hoy=HOY
        )
        self.raiz.update_idletasks()

        textos = " ".join(_textos(pantalla))
        self.assertIn("CASP2609", textos)
        self.assertIn("Ana Pérez", textos)

    def test_un_companero_desactivado_no_sale_en_el_desplegable(self):
        """FASE 6, criterio 3, comprobado sobre el desplegable de verdad."""
        from interfaz.asignacion import PantallaDeAsignacion

        alta_de_companero(self.conexion, "Sigue Aquí")
        companero_id = alta_de_companero(self.conexion, "Ya No Está")
        desactivar_companero(self.conexion, companero_id)

        pantalla = PantallaDeAsignacion(
            self.raiz, self.conexion, al_volver=lambda: None, hoy=HOY
        )
        self.raiz.update_idletasks()

        opciones = " ".join(pantalla._desplegable["values"])
        self.assertIn("Sigue Aquí", opciones)
        self.assertNotIn("Ya No Está", opciones)


class PruebaDelPanelDeSeguimiento(PruebaConVentana):
    def setUp(self):
        super().setUp()
        self.companero_id = alta_de_companero(self.conexion, "Ana Pérez")
        self.caso_id = alta_de_caso(
            self.conexion, "CASP2609", fecha_viaje="2026-09-16"
        ).id
        alta_de_persona(
            self.conexion, self.caso_id, mrn="055-1111-3853", nombre="Quien sea"
        )
        from datos.repositorio import leer_caso_por_id

        self.caso = leer_caso_por_id(self.conexion, self.caso_id)

    def _panel(self):
        from interfaz.seguimiento import PanelDeSeguimiento

        panel = PanelDeSeguimiento(self.raiz, self.conexion, self.caso)
        self.raiz.update_idletasks()
        return panel

    def test_un_caso_sin_contactos_dice_una_frase_no_una_lista_vacia(self):
        """FASE 7, criterio 2."""
        panel = self._panel()
        self.assertTrue(
            any(
                "Todavía no se ha registrado ningún contacto" in texto
                for texto in _textos(panel)
            )
        )

    def test_el_historial_ensena_el_contacto_con_sus_dias(self):
        registrar_contacto(
            self.conexion,
            self.caso_id,
            date.today().isoformat(),
            medio="whatsapp",
            con_quien="Obispo Rodríguez",
            resultado="Queda en revisarlo",
            contactado_por=self.companero_id,
            respondio=1,
        )

        panel = self._panel()

        textos = " ".join(_textos(panel))
        self.assertIn("whatsapp", textos)
        self.assertIn("hoy", textos)
        self.assertIn("RESPONDIÓ", textos)
        self.assertIn("Queda en revisarlo", textos)

    def test_un_contacto_anulado_sigue_visible_con_su_motivo(self):
        """FASE 7, criterio 3: anular deja el registro visible."""
        from datos.contactos import anular_contacto

        contacto_id = registrar_contacto(
            self.conexion, self.caso_id, date.today().isoformat(), medio="llamada"
        )
        anular_contacto(self.conexion, contacto_id, "Era de otro caso")

        panel = self._panel()

        textos = " ".join(_textos(panel))
        self.assertIn("ANULADO", textos)
        self.assertIn("Era de otro caso", textos)

    def test_la_propuesta_se_ensena_pero_NO_se_copia_sola(self):
        """Regla permanente 5 en el sitio donde más se nota."""
        persona = resolver_persona_por_par(self.conexion, "CASP2609", "055-1111-3853")
        guardar_propuesta(
            self.conexion, persona["id"], "incompleta", "falta la entrevista",
            self.companero_id,
        )

        panel = self._panel()

        textos = " ".join(_textos(panel))
        self.assertIn("incompleta", textos)
        self.assertIn("falta la entrevista", textos)
        self.assertIn("Ana Pérez", textos)
        self.assertIsNone(
            panel.valor_del_estado(),
            "la propuesta NO se puede haber copiado sola al desplegable",
        )

    def test_usar_la_propuesta_la_copia_al_desplegable_sin_guardarla(self):
        persona = resolver_persona_por_par(self.conexion, "CASP2609", "055-1111-3853")
        guardar_propuesta(
            self.conexion, persona["id"], "incompleta", None, self.companero_id
        )
        panel = self._panel()

        panel._usar_la_propuesta("incompleta")

        self.assertEqual("incompleta", panel.valor_del_estado())
        estado_en_la_base = self.conexion.execute(
            "SELECT estado_recomendacion FROM casos WHERE id = ?", (self.caso_id,)
        ).fetchone()[0]
        self.assertIsNone(estado_en_la_base, "copiar al desplegable NO es guardar")


class PruebaDeLaVentanaDeDescartados(PruebaConVentana):
    """La ventana que enseña lo que NO entró, con su motivo escrito."""

    def _resultado_con_descartes(self):
        from openpyxl import Workbook

        from paquete.columnas import COLUMNAS_POR_NOMBRE
        from paquete.reconciliacion import reconciliar_excel

        companero_id = alta_de_companero(self.conexion, "Ana Pérez")
        ruta = self.carpeta_temporal / "devuelto.xlsx"
        libro = Workbook()
        hoja = libro.active
        hoja.title = "trabajo"
        hoja.append([columna.titulo for columna in COLUMNAS_POR_NOMBRE.values()])
        fila = [None] * len(COLUMNAS_POR_NOMBRE)
        nombres = list(COLUMNAS_POR_NOMBRE)
        fila[nombres.index("numero_caso")] = "CXSP2609"
        fila[nombres.index("mrn")] = "007-7777-7777"
        fila[nombres.index("clave")] = "CXSP2609:007-7777-7777"
        fila[nombres.index("paso_preparacion")] = "No"
        hoja.append(fila)
        libro.save(str(ruta))
        libro.close()
        return reconciliar_excel(self.conexion, ruta, companero_id)

    def test_se_dibuja_y_ensena_el_par_y_el_motivo(self):
        from interfaz.descartados import VentanaDeDescartados, texto_de_los_descartes

        resultado = self._resultado_con_descartes()
        self.assertEqual(1, len(resultado.descartadas))

        ventana = VentanaDeDescartados(self.raiz, resultado)
        self.raiz.update_idletasks()

        textos = " ".join(_textos(ventana))
        self.assertIn("CXSP2609", textos)
        self.assertIn("007-7777-7777", textos)
        self.assertIn("no existe en la base", textos)
        self.assertIn("Fila 2", texto_de_los_descartes(resultado))
        ventana.destroy()


class PruebaDeLaVentanaDeMapeo(PruebaConVentana):
    """La pantalla donde Miguel dice qué columna es cada cosa."""

    def _hoja_con_titulos_raros(self):
        from openpyxl import Workbook

        ruta = self.carpeta_temporal / "lista_de_ana.xlsx"
        libro = Workbook()
        hoja = libro.active
        hoja.append(["Caso", "N. de Registro", "Nombre completo", "Situación"])
        hoja.append(["CASP2609", "055-1111-3853", "Quien sea", "incompleta"])
        libro.save(str(ruta))
        libro.close()
        return ruta

    def test_se_dibuja_con_la_propuesta_ya_elegida_y_su_muestra(self):
        from interfaz.mapeo import SIN_ASIGNAR, VentanaDeMapeo

        companero = {"id": alta_de_companero(self.conexion, "Ana Pérez"), "nombre": "Ana Pérez"}
        ventana = VentanaDeMapeo(
            self.raiz, self.conexion, self._hoja_con_titulos_raros(), companero
        )
        self.raiz.update_idletasks()

        self.assertEqual("Caso", ventana._elegidos["numero_caso"].get())
        self.assertEqual("N. de Registro", ventana._elegidos["mrn"].get())
        # La muestra es lo que hace comprobable el mapeo: sin ella, «MRN → N. de
        # Registro» es una afirmación que nadie puede verificar mirando la pantalla.
        self.assertIn("055-1111-3853", ventana._muestras["mrn"].cget("text"))
        # Una columna que el archivo no trae se queda sin asignar, no se adivina.
        self.assertEqual(SIN_ASIGNAR, ventana._elegidos["fecha_viaje"].get())
        self.assertIsNone(ventana.resultado, "nada se importa hasta pulsar el botón")
        ventana.destroy()


class PruebaDelTextoDeLosDias(unittest.TestCase):
    """Los días transcurridos, que es lo que pidió el pase de la FASE 7."""

    def test_las_cuatro_formas(self):
        self.assertEqual("hoy", texto_de_los_dias(0))
        self.assertEqual("ayer", texto_de_los_dias(1))
        self.assertEqual("hace 10 días", texto_de_los_dias(10))
        self.assertEqual("sin fecha", texto_de_los_dias(None))

    def test_una_fecha_futura_se_dice_en_futuro_y_no_se_tapa(self):
        self.assertEqual("dentro de 3 días", texto_de_los_dias(-3))

    def test_sin_contacto_hay_que_llamar(self):
        self.assertTrue(hay_que_volver_a_llamar(None))
        self.assertEqual("sin contactar", resumen_del_ultimo_contacto(None))

    def test_contactado_ayer_sin_respuesta_todavia_no_caduca(self):
        contacto = {"dias_desde": 1, "respondio": None, "medio": "correo"}
        self.assertFalse(hay_que_volver_a_llamar(contacto))

    def test_contactado_hace_diez_dias_sin_respuesta_si_caduca(self):
        """El caso del pase: 10 días sin respuesta no es «ya se llamó»."""
        contacto = {"dias_desde": 10, "respondio": 0, "medio": "llamada"}
        self.assertTrue(hay_que_volver_a_llamar(contacto))
        resumen = resumen_del_ultimo_contacto(contacto)
        self.assertIn("hace 10 días", resumen)
        self.assertIn("NO RESPONDIÓ", resumen)

    def test_si_respondio_no_caduca_por_mucho_tiempo_que_pase(self):
        contacto = {"dias_desde": 90, "respondio": 1, "medio": "presencial"}
        self.assertFalse(hay_que_volver_a_llamar(contacto))


if __name__ == "__main__":
    unittest.main()
