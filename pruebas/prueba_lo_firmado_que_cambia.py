"""Cambiar el valor de un campo firmado retira su firma. Medido antes de arreglarlo.

**El defecto que estas pruebas cierran, medido por QA sobre la pantalla real el
2026-09-03.** Es el gemelo del que cerró `pruebas/prueba_lo_vacio_no_se_firma.py`,
entrando por la puerta contraria: aquel firmaba lo vacío, este **vacía lo firmado**.

    tras firmar             valor=<fecha leída>  origen=ocr     verificado=1
    tras CAMBIAR la fecha   valor=2027-12-31     origen=manual  verificado=1
    tras BORRAR el valor    valor=None           origen=manual  verificado=1

Y el caso completo, con la fecha de viaje borrada después de firmar:

    casos_pendientes_de_verificar ...... no sale (dice que está completo)
    casos_que_viajan_pronto ............ 0
    casos_en_riesgo .................... 0
    contador de la pantalla ............ 36 de 36

Un caso **firmado por Miguel, con fecha y hora, sin fecha de viaje**, que desaparece
de los tres avisos a la vez. Es «una persona que llega al templo y nadie lo vio a
tiempo», que es la primera línea de `CLAUDE.md`.

**La causa.** `guardar_procedencia_de_campo` actualiza `origen`, `confianza`,
`valor_ocr` y las bandas, y **no toca `verificado`** — declarado a propósito en la
cabecera de `datos/procedencia.py` para el caso del PDF reprocesado (P-5). Pero el
caso de editar un campo **ya firmado** no estaba declarado en ninguna parte, y por
ahí se colaba.

**Qué se eligió, de las dos que QA planteaba.** Que cambiar o borrar el valor de un
campo firmado **retire la firma de ese campo**, y no que el programa impida editarlo.
El argumento largo está en `datos/repositorio.py`; el corto es que bloquear obliga a
tirar las 36 firmas del caso para corregir una letra, y eso castiga corregir.

**Dónde vive la regla, y por qué ahí.** En `datos/repositorio.py`, en las funciones
que **escriben el valor**, no en la pantalla que las llama. Una firma dice «yo di por
bueno ESTE valor»: en cuanto el valor cambia, la firma ya no habla de él, y eso es
cierto venga el cambio de la pantalla de corrección, de la reconciliación del Excel
de los compañeros (FASE 6) o de una pantalla que todavía no existe.
"""

import shutil
import tempfile
import unittest
from datetime import date
from pathlib import Path
from unittest import mock

from datos.calendario import casos_en_riesgo, casos_que_viajan_pronto
from datos.conexion import abrir_conexion
from datos.esquema import aplicar_esquema
from datos.pendientes import casos_pendientes_de_verificar
from datos.procedencia import (
    guardar_procedencia_de_campo,
    leer_procedencia_de_campo,
    marcar_campo_verificado,
)
from datos.repositorio import (
    actualizar_datos_de_persona,
    actualizar_datos_del_caso,
    alta_de_caso,
    alta_de_persona,
)
from importacion.guardado import CAMPOS_DEL_CASO, CAMPOS_DE_LA_PERSONA

PERSONAS_DEL_CASO = 4

# El caso que QA midió: cuatro personas, 36 filas de procedencia —4 campos del caso
# más 2 campos y 6 casillas por persona—, todas con valor.
CAMPOS_DEL_CASO_MEDIDOS = 36

# El mes de la fecha y el de las cuatro cifras del número coinciden a propósito: un
# mes cruzado abre un aviso, y un aviso abierto deja la prueba esperando.
NUMERO_DEL_CASO = "BARC2608"
FECHA_LEIDA = "2026-08-20"
FECHA_CORREGIDA = "2026-08-25"
HOY = date(2026, 8, 18)


def _hay_ventanas():
    """Si no se puede crear un Tk, las pruebas de pantalla se omiten en vez de fallar."""
    try:
        import tkinter

        raiz = tkinter.Tk()
    except Exception:  # pragma: no cover - depende de la maquina, no del codigo
        return False
    raiz.destroy()
    return True


class LaCapaDeDatosSueltaLaFirmaDelValorQueCambia(unittest.TestCase):
    """La regla, sin pantalla delante: quien escribe el valor suelta su firma.

    Dado un campo firmado, cuando se le escribe un valor distinto, entonces su fila
    de procedencia vuelve a «nadie lo ha dado por bueno» —las tres columnas—, y
    cuando se le vuelve a escribir el mismo valor, no.
    """

    def setUp(self):
        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_firmado_"))
        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        aplicar_esquema(self.conexion)
        self.caso_id = alta_de_caso(
            self.conexion, numero_caso=NUMERO_DEL_CASO, fecha_viaje=FECHA_LEIDA,
            unidad_numero="7000011", unidad_nombre="Barrio Fulano",
        ).id
        self.persona_id = alta_de_persona(
            self.conexion, self.caso_id, mrn="055-1111-3851", nombre="PERSONA 1",
            fila_formulario=1, ord_traductor=1,
        )
        self.companero_id = self.conexion.execute(
            "INSERT INTO companeros (nombre, activo, creado_en) "
            "VALUES ('Miguel', 1, '2026-09-03T10:00:00')"
        ).lastrowid
        for campo in ("fecha_viaje", "unidad_numero", "unidad_nombre"):
            guardar_procedencia_de_campo(
                self.conexion, "casos", self.caso_id, campo, origen="ocr", confianza=0.9,
            )
            marcar_campo_verificado(
                self.conexion, "casos", self.caso_id, campo, self.companero_id
            )
        for campo in ("nombre", "mrn", "ord_traductor"):
            guardar_procedencia_de_campo(
                self.conexion, "personas", self.persona_id, campo, origen="ocr",
                confianza=0.9,
            )
            marcar_campo_verificado(
                self.conexion, "personas", self.persona_id, campo, self.companero_id
            )

    def tearDown(self):
        self.conexion.close()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def _procedencia(self, tabla, registro_id, campo):
        return leer_procedencia_de_campo(self.conexion, tabla, registro_id, campo)

    def _firmados(self):
        return self.conexion.execute(
            "SELECT COUNT(*) AS cuantos FROM procedencia_campo WHERE verificado = 1"
        ).fetchone()["cuantos"]

    def test_la_premisa_las_seis_filas_nacen_firmadas(self):
        """Antes de nada: que el montaje es el que se dice, y no otro."""
        self.assertEqual(self._firmados(), 6)

    def test_borrar_el_valor_de_un_campo_firmado_retira_su_firma(self):
        actualizar_datos_del_caso(self.conexion, self.caso_id, fecha_viaje=None)
        fila = self._procedencia("casos", self.caso_id, "fecha_viaje")
        self.assertEqual(fila["verificado"], 0)
        self.assertIsNone(fila["verificado_por"])
        self.assertIsNone(fila["verificado_en"])

    def test_cambiar_el_valor_de_un_campo_firmado_retira_su_firma(self):
        actualizar_datos_del_caso(
            self.conexion, self.caso_id, fecha_viaje=FECHA_CORREGIDA
        )
        self.assertEqual(
            self._procedencia("casos", self.caso_id, "fecha_viaje")["verificado"], 0
        )

    def test_solo_se_retira_la_firma_del_campo_que_cambio(self):
        """Las otras cinco no se tocan: la firma es por campo, no por caso."""
        actualizar_datos_del_caso(self.conexion, self.caso_id, fecha_viaje=None)
        self.assertEqual(self._firmados(), 5)
        self.assertEqual(
            self._procedencia("casos", self.caso_id, "unidad_numero")["verificado"], 1
        )

    def test_escribir_el_mismo_valor_no_retira_nada(self):
        """Guardar sin cambiar nada no puede desfirmar un caso entero."""
        actualizar_datos_del_caso(
            self.conexion, self.caso_id, fecha_viaje=FECHA_LEIDA,
            unidad_numero="7000011", unidad_nombre="Barrio Fulano",
        )
        self.assertEqual(self._firmados(), 6)

    def test_retirar_la_firma_no_borra_de_donde_salio_el_valor(self):
        """Desfirmar dice «nadie lo ha dado por bueno», no «esto no se leyó»."""
        actualizar_datos_del_caso(self.conexion, self.caso_id, fecha_viaje=None)
        fila = self._procedencia("casos", self.caso_id, "fecha_viaje")
        self.assertEqual(fila["origen"], "ocr")
        self.assertEqual(fila["confianza"], 0.9)

    def test_un_campo_sin_fila_de_procedencia_no_revienta(self):
        """`estado_recomendacion` no lleva procedencia y se escribe por la misma puerta."""
        actualizar_datos_del_caso(
            self.conexion, self.caso_id, estado_recomendacion="incompleta"
        )
        self.assertEqual(self._firmados(), 6)

    def test_cambiar_el_mrn_de_una_persona_firmada_retira_su_firma(self):
        actualizar_datos_de_persona(
            self.conexion, self.persona_id, mrn="055-1111-3999"
        )
        self.assertEqual(
            self._procedencia("personas", self.persona_id, "mrn")["verificado"], 0
        )
        self.assertEqual(
            self._procedencia("personas", self.persona_id, "nombre")["verificado"], 1
        )

    def test_cambiar_una_casilla_de_ordenanza_firmada_retira_su_firma(self):
        """Una casilla firmada que pasa de «sí» a «no» tampoco sigue firmada."""
        actualizar_datos_de_persona(self.conexion, self.persona_id, ord_traductor=0)
        self.assertEqual(
            self._procedencia("personas", self.persona_id, "ord_traductor")["verificado"],
            0,
        )

    def test_una_casilla_que_no_se_nombra_conserva_su_firma(self):
        """Lo que no se pasa, no se toca: tampoco su firma."""
        actualizar_datos_de_persona(self.conexion, self.persona_id, nombre="PERSONA 1")
        self.assertEqual(
            self._procedencia("personas", self.persona_id, "ord_traductor")["verificado"],
            1,
        )


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class ElCasoQueDesaparecioDeLosTresAvisos(unittest.TestCase):
    """El escenario de QA por la vía real: pantalla, botones, sin trucos.

    Dado un caso de cuatro personas firmado entero —36 de 36—, cuando Miguel borra
    la fecha de viaje y guarda, entonces el caso **vuelve a salir** en la lista de
    pendientes de verificar y el contador deja de decir 36 de 36.
    """

    def setUp(self):
        import tkinter as tk

        from interfaz.correccion import PantallaDeCorreccion
        from interfaz.tema import aplicar_tema

        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_avisos_"))
        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        aplicar_esquema(self.conexion)
        self.caso_id = alta_de_caso(
            self.conexion, numero_caso=NUMERO_DEL_CASO, fecha_viaje=FECHA_LEIDA,
            unidad_numero="7000011", unidad_nombre="Barrio Fulano",
        ).id
        for campo in CAMPOS_DEL_CASO:
            guardar_procedencia_de_campo(
                self.conexion, "casos", self.caso_id, campo, origen="ocr", confianza=0.9,
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
                    self.conexion, "personas", persona_id, campo, origen="ocr",
                    confianza=0.9,
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

    # ---- gestos y medidas -------------------------------------------------

    def _firmar_el_caso_entero(self):
        """Los dos gestos del pie, que es como QA llegó a «36 de 36»."""
        with mock.patch("interfaz.correccion.dialogos.preguntar_si_o_no", return_value=True), \
             mock.patch("interfaz.correccion.dialogos.informar"), \
             mock.patch("interfaz.correccion.dialogos.advertir"):
            self.pantalla.resolver_las_casillas_sin_leer()
            self.raiz.update()
            self.pantalla.verificar()
            self.raiz.update()

    def _escribir_la_fecha(self, texto):
        """Teclea en el campo de la fecha de viaje y guarda. Devuelve los avisos vistos."""
        entrada = self.pantalla._campos_del_caso["fecha_viaje"].entrada
        entrada.delete(0, "end")
        if texto:
            entrada.insert(0, texto)
        self.pantalla._recontar()
        with mock.patch("interfaz.correccion.dialogos.advertir") as advertir, \
             mock.patch("interfaz.correccion.dialogos.informar"):
            self.pantalla.guardar()
            self.raiz.update()
        return [llamada.args[1] for llamada in advertir.call_args_list]

    def _sale_en_pendientes(self):
        return any(
            caso["id"] == self.caso_id
            for caso in casos_pendientes_de_verificar(self.conexion)
        )

    def _procedencia_de_la_fecha(self):
        return leer_procedencia_de_campo(
            self.conexion, "casos", self.caso_id, "fecha_viaje"
        )

    # ---- las pruebas ------------------------------------------------------

    def test_la_premisa_el_caso_firmado_entero_dice_treinta_y_seis_de_treinta_y_seis(self):
        """El punto de partida de QA, reproducido con los mismos números."""
        self._firmar_el_caso_entero()
        self.assertEqual(
            self.pantalla._campos_verificados(),
            (CAMPOS_DEL_CASO_MEDIDOS, CAMPOS_DEL_CASO_MEDIDOS),
        )
        self.assertFalse(self._sale_en_pendientes())
        self.assertEqual(len(casos_que_viajan_pronto(self.conexion, HOY)), 1)
        self.assertEqual(len(casos_en_riesgo(self.conexion, HOY)), 1)

    def test_borrar_la_fecha_de_viaje_ya_firmada_retira_su_firma(self):
        """Criterio 1 del pase: la misma consulta con la que QA cazó el defecto."""
        self._firmar_el_caso_entero()
        self._escribir_la_fecha("")
        fila = self._procedencia_de_la_fecha()
        self.assertEqual(fila["verificado"], 0)
        self.assertIsNone(fila["verificado_por"])
        self.assertIsNone(fila["verificado_en"])

    def test_el_caso_al_que_le_falta_la_fecha_vuelve_a_salir_en_los_pendientes(self):
        """Criterio 2 del pase, en el aviso que le corresponde.

        No en `casos_que_viajan_pronto`: un caso **sin fecha** no se puede situar en
        el calendario, y esa consulta lo dice en su propia cabecera. El aviso que le
        toca a un caso al que le falta un dato es la lista de pendientes de
        verificar, y ahí es donde tiene que reaparecer.
        """
        self._firmar_el_caso_entero()
        self._escribir_la_fecha("")
        self.assertTrue(self._sale_en_pendientes())

    def test_el_contador_deja_de_decir_treinta_y_seis_de_treinta_y_seis(self):
        """Lo que Miguel ve tiene que ser lo que hay en la base."""
        self._firmar_el_caso_entero()
        self._escribir_la_fecha("")
        self.assertEqual(
            self.pantalla._campos_verificados(),
            (CAMPOS_DEL_CASO_MEDIDOS - 1, CAMPOS_DEL_CASO_MEDIDOS),
        )

    def test_cambiar_la_fecha_firmada_por_otra_tambien_retira_la_firma(self):
        """La otra mitad de lo que QA midió: no hace falta borrar para descolgarla."""
        self._firmar_el_caso_entero()
        self._escribir_la_fecha(FECHA_CORREGIDA)
        self.assertEqual(self._procedencia_de_la_fecha()["verificado"], 0)
        self.assertTrue(self._sale_en_pendientes())

    def test_guardar_avisa_de_que_retiro_una_firma(self):
        """Una firma que se cae sin decirlo es el mismo silencio que hacía daño.

        El contador baja de 36 a 35 en un pie que nadie está mirando mientras
        teclea. Si el programa no lo dice con palabras, el caso vuelve a los avisos
        y Miguel no sabe por qué.
        """
        self._firmar_el_caso_entero()
        avisos = self._escribir_la_fecha("")
        self.assertTrue(
            any("firma" in aviso.lower() for aviso in avisos),
            f"ningún aviso del guardado habla de la firma: {avisos!r}",
        )

    def test_guardar_sin_cambiar_nada_no_retira_ninguna_firma(self):
        """El otro lado de la regla: no se puede desfirmar un caso por pulsar Guardar."""
        self._firmar_el_caso_entero()
        with mock.patch("interfaz.correccion.dialogos.advertir"), \
             mock.patch("interfaz.correccion.dialogos.informar"):
            self.pantalla.guardar()
            self.raiz.update()
        self.assertEqual(
            self.pantalla._campos_verificados(),
            (CAMPOS_DEL_CASO_MEDIDOS, CAMPOS_DEL_CASO_MEDIDOS),
        )

    def test_corregido_el_dato_se_puede_volver_a_firmar_el_caso_entero(self):
        """El ciclo se cierra: corregir no deja el caso atrapado sin poder firmarse.

        Es lo que la otra opción —impedir editar un campo firmado— habría costado:
        tirar las 36 firmas para corregir una fecha. Aquí se retira una, se corrige,
        y «Todo correcto» vuelve a dejar 36 de 36 sobre el valor nuevo.
        """
        self._firmar_el_caso_entero()
        self._escribir_la_fecha(FECHA_CORREGIDA)
        self._firmar_el_caso_entero()
        self.assertEqual(
            self.pantalla._campos_verificados(),
            (CAMPOS_DEL_CASO_MEDIDOS, CAMPOS_DEL_CASO_MEDIDOS),
        )
        fila = self._procedencia_de_la_fecha()
        self.assertEqual(fila["verificado"], 1)
        self.assertEqual(fila["origen"], "manual")
