"""«Todo correcto» y las casillas que no se leyeron: el caso se cierra sin teclear.

**El problema, medido antes de escribir nada.** Un caso español del dueño con 4
personas abre la pantalla de corrección así:

    CONTADOR : Verificados 0 de 12 campos · 0 no válidos · 24 casillas no leídas
    MOTIVO   : 24 casillas sin resolver. Hasta que no quede ninguno no se puede verificar.
    BOTON    : ('disabled',)

Y resolverlas a mano, una por una, **no son 24 gestos: son 48**. La barra
espaciadora entra por «marcada» —`interfaz/casilla.py:88`— así que dejar una
casilla en «no marcada» partiendo de «no leída» cuesta dos pulsaciones. Medido en
esta máquina sobre esas mismas 4 personas: 48 gestos para dejar las 24 en «no».

Las ordenanzas de sus PDF **no existen en ninguna capa del archivo** —36 casillas,
tinta 0.0000 en las 36, los 45 botones `/Btn` en `/Off`— así que llegan siempre
las seis de cada persona sin leer. Con cientos de PDF eso es exactamente el
tecleo que el programa existe para quitarle.

**Lo que estas pruebas fijan, y de dónde sale cada una.** Salen del criterio de
aceptación del pase, no del código: un caso suyo se deja verificado sin ir campo
por campo ni casilla por casilla, y en la base queda escrito que lo confirmó
Miguel y no la máquina.

⚠️ **La distinción que no se puede perder.** «No leída» y «no marcada» siguen
siendo dos cosas distintas: el diseñador midió que `tk.Checkbutton` en tri-estado
las dibuja iguales —0 px de diferencia contra 146 de ruido— y por eso la casilla
se dibuja a mano. Resolver 24 de golpe **no** las convierte en «leídas»: las deja
en «no marcada» con `origen='manual'` en `procedencia_campo`, que es la columna
que dice quién lo dijo. Una lectura del papel diría `'ocr'` o `'anotacion'`.
"""

import shutil
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from datos.conexion import abrir_conexion
from datos.esquema import aplicar_esquema
from datos.procedencia import guardar_procedencia_de_campo, procedencia_por_campo
from datos.repositorio import (
    CASILLAS_DE_ORDENANZAS,
    alta_de_caso,
    alta_de_persona,
    leer_personas_del_caso,
)
from importacion.guardado import CAMPOS_DEL_CASO, CAMPOS_DE_LA_PERSONA

PERSONAS_DEL_CASO = 4
CASILLAS_POR_PERSONA = len(CASILLAS_DE_ORDENANZAS)
CASILLAS_SIN_LEER = PERSONAS_DEL_CASO * CASILLAS_POR_PERSONA


def _hay_ventanas():
    """Si no se puede crear un Tk, estas pruebas se omiten en vez de fallar."""
    try:
        import tkinter

        raiz = tkinter.Tk()
    except Exception:  # pragma: no cover - depende de la maquina, no del codigo
        return False
    raiz.destroy()
    return True


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class PantallaDeUnCasoEspanol(unittest.TestCase):
    """Un caso con 4 personas y las 24 casillas sin leer, como los del dueño.

    El caso se monta con la procedencia que deja la importación de verdad —los
    cuatro campos del caso y los dos de cada persona, y **ninguna** de las
    casillas (`importacion/guardado.py:51-55`)— para que el denominador del pie
    sea el mismo que ve el dueño: 12.
    """

    def setUp(self):
        import tkinter as tk

        from interfaz.correccion import PantallaDeCorreccion
        from interfaz.tema import aplicar_tema

        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_todo_correcto_"))
        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        aplicar_esquema(self.conexion)
        # Mes de la fecha y mes del numero de caso coinciden a proposito: un mes
        # cruzado abre un aviso, y un aviso abierto deja la prueba esperando.
        caso = alta_de_caso(
            self.conexion, numero_caso="BARC2608", fecha_viaje="2026-08-20",
            unidad_numero="7000011", unidad_nombre="Barrio Fulano",
        )
        self.caso_id = caso.id
        for campo in CAMPOS_DEL_CASO:
            guardar_procedencia_de_campo(
                self.conexion, "casos", self.caso_id, campo, origen="ocr", confianza=0.9
            )
        self.personas = []
        for fila in range(1, PERSONAS_DEL_CASO + 1):
            persona_id = alta_de_persona(
                self.conexion, self.caso_id, mrn=f"055-1111-{3850 + fila:04d}",
                nombre=f"PERSONA {fila}", fila_formulario=fila,
            )
            self.personas.append(persona_id)
            for campo in CAMPOS_DE_LA_PERSONA:
                guardar_procedencia_de_campo(
                    self.conexion, "personas", persona_id, campo,
                    origen="ocr", confianza=0.9,
                )

        self.raiz = tk.Tk()
        self.raiz.geometry("1100x720")
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

    def tearDown(self):
        self.pantalla.soltar_atajos()
        self.raiz.destroy()
        self.conexion.close()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    # ---- utilidades de medida -------------------------------------------

    def _casillas_guardadas(self):
        """Los 24 valores de casilla tal como están en la base, en una lista."""
        return [
            persona[nombre]
            for persona in leer_personas_del_caso(self.conexion, self.caso_id)
            for nombre in CASILLAS_DE_ORDENANZAS
        ]

    def _procedencia_de_las_casillas(self):
        """Las filas de `procedencia_campo` que describen casillas de ordenanza."""
        filas = []
        for persona_id in self.personas:
            por_campo = procedencia_por_campo(self.conexion, "personas", persona_id)
            filas.extend(
                por_campo[nombre] for nombre in CASILLAS_DE_ORDENANZAS if nombre in por_campo
            )
        return filas

    def _filas_verificadas(self):
        return self.conexion.execute(
            "SELECT COUNT(*) AS cuantas FROM procedencia_campo WHERE verificado = 1"
        ).fetchone()["cuantas"]

    def _decir(self, respuesta):
        """Contesta esa respuesta a cualquier pregunta de sí o no de la pantalla."""
        return mock.patch("interfaz.correccion.dialogos.preguntar_si_o_no", return_value=respuesta)

    def _sin_avisos(self):
        """Traga los cuadros informativos, que en una prueba se quedan esperando."""
        return mock.patch("interfaz.correccion.dialogos.informar")


class LasCasillasSinLeerDejanDeSerUnaPared(PantallaDeUnCasoEspanol):
    """Dado un caso con 24 casillas sin leer, cuando Miguel dice que en ese
    documento no hay ninguna marcada, entonces quedan resueltas de una vez."""

    def test_el_caso_arranca_con_las_veinticuatro_sin_leer(self):
        """La premisa medida: 4 personas por 6 casillas, ninguna leída."""
        self.assertEqual(self.pantalla._casillas_sin_leer(), CASILLAS_SIN_LEER)
        self.assertEqual(self._casillas_guardadas(), [None] * CASILLAS_SIN_LEER)

    def test_las_veinticuatro_se_resuelven_con_un_gesto(self):
        with self._decir(True), self._sin_avisos():
            self.pantalla.resolver_las_casillas_sin_leer()
        self.assertEqual(self._casillas_guardadas(), [0] * CASILLAS_SIN_LEER)
        self.assertEqual(self.pantalla._casillas_sin_leer(), 0)

    def test_la_que_miguel_ya_habia_marcado_no_se_toca(self):
        """«Las que ya marcó» son suyas: el botón solo resuelve lo que sigue sin leer."""
        primera = self.pantalla._bloques[0].casillas["ord_investidura"]
        primera.fijar(1)
        with self._decir(True), self._sin_avisos():
            self.pantalla.resolver_las_casillas_sin_leer()
        persona = leer_personas_del_caso(self.conexion, self.caso_id)[0]
        self.assertEqual(persona["ord_investidura"], 1)
        self.assertEqual(persona["ord_traductor"], 0)

    def test_queda_escrito_que_lo_dijo_miguel_y_no_que_se_leyeron(self):
        """La distinción que no se puede perder, hecha dato: `origen='manual'`."""
        with self._decir(True), self._sin_avisos():
            self.pantalla.resolver_las_casillas_sin_leer()
        filas = self._procedencia_de_las_casillas()
        self.assertEqual(len(filas), CASILLAS_SIN_LEER)
        self.assertEqual({fila["origen"] for fila in filas}, {"manual"})
        self.assertEqual({fila["valor_ocr"] for fila in filas}, {None})

    def test_resolver_las_casillas_no_marca_nada_como_verificado(self):
        """Regla permanente 5. Resolver no es confirmar: son dos actos distintos."""
        with self._decir(True), self._sin_avisos():
            self.pantalla.resolver_las_casillas_sin_leer()
        self.assertEqual(self._filas_verificadas(), 0)
        self.assertEqual(
            {fila["verificado"] for fila in self._procedencia_de_las_casillas()}, {0}
        )

    def test_si_dice_que_no_no_cambia_absolutamente_nada(self):
        with self._decir(False), self._sin_avisos():
            self.pantalla.resolver_las_casillas_sin_leer()
        self.assertEqual(self._casillas_guardadas(), [None] * CASILLAS_SIN_LEER)
        self.assertEqual(self._procedencia_de_las_casillas(), [])
        self.assertEqual(self.pantalla._casillas_sin_leer(), CASILLAS_SIN_LEER)

    def test_la_pregunta_dice_cuantas_de_cuantas_personas_y_que_no_es_una_lectura(self):
        """«Que quede claro en pantalla qué está confirmando de una vez»."""
        pregunta = self.pantalla._pregunta_de_las_casillas_sin_leer()
        self.assertIn("24", pregunta)
        self.assertIn("4 personas", pregunta)
        self.assertIn("BARC2608", pregunta)
        self.assertIn("no marcada", pregunta)
        self.assertIn("usted", pregunta)

    def test_sin_casillas_sin_leer_el_boton_esta_apagado_y_no_hace_nada(self):
        with self._decir(True), self._sin_avisos():
            self.pantalla.resolver_las_casillas_sin_leer()
        self.assertIn("disabled", self.pantalla._boton_de_las_casillas.state())
        with self._decir(True), self._sin_avisos() as avisado:
            self.pantalla.resolver_las_casillas_sin_leer()
        self.assertEqual(self._casillas_guardadas(), [0] * CASILLAS_SIN_LEER)
        self.assertTrue(avisado.called)

    def test_al_resolverlas_el_boton_de_todo_correcto_se_enciende(self):
        """La pared cae: el motivo del bloqueo desaparece y el botón se enciende."""
        self.assertIn("disabled", self.pantalla._boton_verificar.state())
        with self._decir(True), self._sin_avisos():
            self.pantalla.resolver_las_casillas_sin_leer()
        self.assertIsNone(self.pantalla._motivo_por_el_que_no_se_puede_verificar())
        self.assertNotIn("disabled", self.pantalla._boton_verificar.state())

    def test_el_motivo_del_bloqueo_nombra_la_salida(self):
        """Un bloqueo que no dice cómo salir es un callejón."""
        self.assertIn(
            "Ninguna ordenanza marcada",
            self.pantalla._motivo_por_el_que_no_se_puede_verificar(),
        )


class ElBotonTodoCorrecto(PantallaDeUnCasoEspanol):
    """Dado un caso sin nada que corregir, cuando Miguel pulsa «Todo correcto» y
    acepta, entonces el caso queda firmado por él, con la fecha y la hora."""

    def _dejar_el_caso_listo(self):
        with self._decir(True), self._sin_avisos():
            self.pantalla.resolver_las_casillas_sin_leer()

    def test_el_boton_se_llama_todo_correcto(self):
        """Lo pidió el dueño literal y el diseñador lo especificó: no es «Guardar»."""
        self.assertIn("Todo correcto", self.pantalla._boton_verificar.cget("text"))

    def test_pregunta_antes_y_si_dice_que_no_no_firma_nada(self):
        self._dejar_el_caso_listo()
        with self._decir(False) as preguntado, self._sin_avisos():
            self.pantalla.verificar()
        self.assertTrue(preguntado.called)
        self.assertEqual(self._filas_verificadas(), 0)

    def test_al_aceptar_firma_todo_lo_de_la_pantalla_con_quien_y_cuando(self):
        """4 campos del caso + 4 personas x (2 campos + 6 casillas) = 36 filas."""
        self._dejar_el_caso_listo()
        with self._decir(True), self._sin_avisos():
            self.pantalla.verificar()
        esperadas = len(CAMPOS_DEL_CASO) + PERSONAS_DEL_CASO * (
            len(CAMPOS_DE_LA_PERSONA) + CASILLAS_POR_PERSONA
        )
        self.assertEqual(self._filas_verificadas(), esperadas)
        filas = self.conexion.execute(
            "SELECT verificado_por, verificado_en FROM procedencia_campo "
            "WHERE verificado = 1"
        ).fetchall()
        self.assertTrue(all(fila["verificado_por"] is not None for fila in filas))
        self.assertTrue(all(fila["verificado_en"] is not None for fila in filas))

    def test_las_casillas_que_miguel_resolvio_tambien_quedan_firmadas(self):
        self._dejar_el_caso_listo()
        with self._decir(True), self._sin_avisos():
            self.pantalla.verificar()
        filas = self._procedencia_de_las_casillas()
        self.assertEqual(len(filas), CASILLAS_SIN_LEER)
        self.assertEqual({fila["verificado"] for fila in filas}, {1})
        # Y siguen diciendo que lo dijo el, no que se leyeran del papel.
        self.assertEqual({fila["origen"] for fila in filas}, {"manual"})

    def test_la_pregunta_dice_cuantos_campos_cuantas_personas_y_que_caso(self):
        self._dejar_el_caso_listo()
        pregunta = self.pantalla._pregunta_de_todo_correcto()
        self.assertIn("36", pregunta)
        self.assertIn("4 personas", pregunta)
        self.assertIn("BARC2608", pregunta)
        self.assertIn("Miguel", pregunta)

    def test_la_pregunta_ya_no_dice_que_no_se_puede_deshacer(self):
        """~~Hueco 6 del mockup: no existe la inversa de `marcar_campo_verificado`.~~

        **Dejó de ser cierto el 2026-09-03** y por eso esta prueba se dio la vuelta
        en vez de borrarse. `datos/procedencia.py` ya tiene
        `desmarcar_campo_verificado` y el pie de la pantalla tiene el botón
        «Deshacer "Todo correcto"» (`pruebas/prueba_lo_vacio_no_se_firma.py`).
        Dejar la frase habría sido mentir en la otra dirección: quien la leyera no
        buscaría el botón que sí está.
        """
        self._dejar_el_caso_listo()
        pregunta = self.pantalla._pregunta_de_todo_correcto()
        self.assertNotIn("no se puede deshacer", pregunta.lower())

    def test_el_numero_de_la_pregunta_es_el_que_se_firma_de_verdad(self):
        """Si el diálogo dice 12 y firma 36, la confirmación no confirma nada.

        El caso que lo destapa: Miguel resuelve las casillas **a mano** y pulsa
        «Todo correcto» sin guardar antes. Las filas de procedencia de esas
        casillas todavía no existen al preguntar, y sí existen al firmar. Contar
        lo que hay en ese momento diría 12 y se firmarían 36.
        """
        for bloque in self.pantalla._bloques:
            for casilla in bloque.casillas.values():
                casilla.fijar(0)
        self.pantalla._recontar()
        # Se lee el texto que RECIBE el cuadro de diálogo, no el que devuelve una
        # llamada suelta: lo que importa es lo que Miguel tiene delante en el
        # momento de decir que sí, y ese momento lo elige la pantalla.
        with self._decir(True) as preguntado, self._sin_avisos():
            self.pantalla.verificar()
        dicho = preguntado.call_args.args[1]
        self.assertEqual(self._filas_verificadas(), 36)
        self.assertIn(str(self._filas_verificadas()), dicho)

    def test_con_la_pared_puesta_no_pregunta_siquiera(self):
        """Bloqueado, el botón no abre la pregunta: dice el motivo y mueve el foco."""
        with self._decir(True) as preguntado, self._sin_avisos() as avisado:
            self.pantalla.verificar()
        self.assertFalse(preguntado.called)
        self.assertTrue(avisado.called)
        self.assertEqual(self._filas_verificadas(), 0)

    def test_un_campo_que_no_vale_sigue_siendo_pared(self):
        """«Todo correcto» no puede saltarse la validación: eso firmaría un error."""
        self._dejar_el_caso_listo()
        mrn = self.pantalla._bloques[0].campos["mrn"]
        mrn.entrada.delete(0, "end")
        mrn.entrada.insert(0, "no-es-un-mrn")
        mrn.refrescar()
        self.pantalla._recontar()
        with self._decir(True) as preguntado, self._sin_avisos():
            self.pantalla.verificar()
        self.assertFalse(preguntado.called)
        self.assertEqual(self._filas_verificadas(), 0)


class ElCasoEnteroSeCierraSinTeclear(PantallaDeUnCasoEspanol):
    """El criterio de cierre del pase, contado en gestos.

    Un gesto es una pulsación: un clic de ratón o una tecla. Se cuentan los de la
    pantalla de corrección, sin contar el clic que la abrió.
    """

    GESTOS_A_MANO_MEDIDOS = 48 + 1 + 1  # 48 en las casillas + «verificar» + aceptar
    GESTOS_QUE_SE_PERMITEN = 4          # resolver + aceptar + todo correcto + aceptar

    def test_cuatro_gestos_dejan_el_caso_verificado_y_firmado(self):
        gestos = 0
        with self._decir(True), self._sin_avisos():
            self.pantalla.resolver_las_casillas_sin_leer()   # 1 pulsación
            gestos += 2                                      # + aceptar el diálogo
            self.pantalla.verificar()                        # 1 pulsación
            gestos += 2                                      # + aceptar el diálogo

        self.assertLessEqual(gestos, self.GESTOS_QUE_SE_PERMITEN)
        esperadas = len(CAMPOS_DEL_CASO) + PERSONAS_DEL_CASO * (
            len(CAMPOS_DE_LA_PERSONA) + CASILLAS_POR_PERSONA
        )
        self.assertEqual(self._filas_verificadas(), esperadas)
        self.assertEqual(self._casillas_guardadas(), [0] * CASILLAS_SIN_LEER)

    def test_a_mano_cuesta_cincuenta_gestos_y_por_eso_existe_el_boton(self):
        """El número contra el que se compara, medido y no citado.

        Son 48 y no 24 porque la barra entra por «marcada»: dejar una casilla en
        «no» partiendo de «no leída» cuesta dos pulsaciones.
        """
        gestos = 0
        for bloque in self.pantalla._bloques:
            for casilla in bloque.casillas.values():
                while casilla.valor != 0:
                    casilla._alternar()
                    gestos += 1
        self.assertEqual(gestos, 48)
        self.assertEqual(self.GESTOS_A_MANO_MEDIDOS, gestos + 2)


class LaCasillaRecuerdaSiLaTocaronAMano(PantallaDeUnCasoEspanol):
    """`cambio()` es lo que distingue una casilla que alguien resolvió de una que
    nadie tocó, y es de donde sale la fila de procedencia con `origen='manual'`."""

    def test_una_casilla_recien_cargada_no_ha_cambiado(self):
        casilla = self.pantalla._bloques[0].casillas["ord_traductor"]
        self.assertFalse(casilla.cambio())

    def test_pasar_de_no_leida_a_no_marcada_cuenta_como_cambio(self):
        """`None` y 0 son valores distintos, y `cambio()` tiene que verlo.

        Es el caso exacto del botón: la casilla queda en «no», que es el mismo
        valor que tendría si la máquina la hubiera leído sin marca, y la única
        prueba de que lo dijo una persona es que aquí se vio el cambio.
        """
        casilla = self.pantalla._bloques[0].casillas["ord_traductor"]
        casilla.fijar(0)
        self.assertTrue(casilla.cambio())

    def test_volver_al_valor_de_partida_deja_de_ser_un_cambio(self):
        casilla = self.pantalla._bloques[0].casillas["ord_traductor"]
        casilla.fijar(1)
        casilla.fijar(None)
        self.assertFalse(casilla.cambio())

    def test_una_casilla_sin_tocar_no_deja_fila_de_procedencia(self):
        """Guardar no inventa una procedencia para lo que nadie resolvió."""
        self.pantalla._bloques[0].casillas["ord_traductor"].fijar(1)
        with self._sin_avisos():
            self.pantalla.guardar()
        filas = self._procedencia_de_las_casillas()
        self.assertEqual(len(filas), 1)
        self.assertEqual(filas[0]["campo"], "ord_traductor")
        self.assertEqual(filas[0]["origen"], "manual")


if __name__ == "__main__":
    unittest.main()
