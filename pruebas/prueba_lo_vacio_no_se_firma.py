"""Lo que vino vacío no se firma, y una firma se puede deshacer.

**El defecto que estas pruebas cierran, medido por QA sobre la base real del dueño
el 2026-09-03.** Importando su caso y pulsando los dos botones del pie **sin
teclear ni un carácter**, en la base quedaba esto:

    ('casos',    1, 'unidad_numero', 'vacio', 1, 1)   <<< VACIO FIRMADO
    ('personas', 3, 'mrn',           'vacio', 1, 1)   <<< VACIO FIRMADO
    CAMPOS VACIOS FIRMADOS COMO VERIFICADOS: 2   (de 36 firmados)

Reproducido aquí antes de tocar nada, con los mismos dos números. Una persona con
`mrn = NULL` quedaba **marcada como verificada por Miguel, con fecha y hora**: el
caso *parece* completo, que es peor que no haberlo tocado, y choca de frente con la
regla permanente 5.

**Por qué la regla es «el valor está vacío» y no «el origen es 'vacio'».** QA
proponía las dos y hay que elegir una. `origen='vacio'` es lo que encontró la
importación; el valor de la pantalla es lo que Miguel está mirando cuando firma, y
son cosas distintas en los dos sentidos:

  - Miguel teclea el MRN que faltaba: el origen pasa a `'manual'` y el campo **sí**
    se firma. Con la regla del origen también, así que ahí empatan.
  - Miguel **borra** un valor que estaba mal leído y **después firma**: el origen
    pasa a `'manual'` y con la regla del origen ese vacío **se firmaría**. Con la
    regla del valor, no. Es el mismo daño por otra puerta.

⚠️ **Ese segundo punto vale para ese orden y solo para ese orden**, y la primera
redacción de este archivo no lo decía. QA midió el orden contrario el mismo día
—firmar primero y borrar después— y por ahí el vacío quedaba firmado igual. Lo
cierra `pruebas/prueba_lo_firmado_que_cambia.py`, con la regla puesta donde se
escribe el valor y no donde se firma. Hacen falta las dos.

**Por qué no se bloquea la verificación, que era la otra opción de QA.** Porque un
campo vacío no siempre se puede llenar: si el papel no trae el número de unidad, no
hay nada que teclear y el caso se quedaría sin poder cerrar nunca. La regla
permanente 5 prohíbe las dos direcciones —ni firmar sin que Miguel pulse, ni
impedirle confirmar lo que sí ha revisado—, y bloquear rompe la segunda. Dejando el
campo fuera de la firma, el contador dice **34 de 36** y esa es la verdad: dos
campos que nadie ha dado por buenos porque nadie los pudo leer. El caso sigue
saliendo en `casos_pendientes_de_verificar` (`datos/pendientes.py`), que es
exactamente lo que el programa existe para avisar.

**Y la segunda mitad: deshacer.** `datos/procedencia.py` tenía
`marcar_campo_verificado` y ninguna inversa —13 funciones, ninguna desverificaba—,
así que un error de dos pulsaciones no tenía salida desde el programa. Con el vacío
ya fuera sigue haciendo falta: Miguel puede firmar un caso y darse cuenta después.
"""

import shutil
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from datos.conexion import abrir_conexion
from datos.esquema import aplicar_esquema
from datos.procedencia import (
    desmarcar_campo_verificado,
    guardar_procedencia_de_campo,
    leer_procedencia_de_campo,
    marcar_campo_verificado,
)
from datos.repositorio import (
    CASILLAS_DE_ORDENANZAS,
    alta_de_caso,
    alta_de_persona,
    leer_personas_del_caso,
)
from datos.validacion import ErrorDeValidacion
from importacion.guardado import CAMPOS_DEL_CASO, CAMPOS_DE_LA_PERSONA

PERSONAS_DEL_CASO = 4

# El campo del caso y el de la persona que el caso del dueño trajo vacíos, y la
# fila en la que llegó el segundo. Son los dos que QA vio firmados.
CAMPO_VACIO_DEL_CASO = "unidad_numero"
CAMPO_VACIO_DE_LA_PERSONA = "mrn"
FILA_DE_LA_PERSONA_SIN_MRN = 3


def _hay_ventanas():
    """Si no se puede crear un Tk, estas pruebas se omiten en vez de fallar."""
    try:
        import tkinter

        raiz = tkinter.Tk()
    except Exception:  # pragma: no cover - depende de la maquina, no del codigo
        return False
    raiz.destroy()
    return True


class LaInversaDeLaFirma(unittest.TestCase):
    """La capa de datos sabe deshacer lo que sabe hacer.

    Dado un campo firmado, cuando se deshace la firma, entonces la fila vuelve al
    estado en que nació —sin quién y sin cuándo— y el `CHECK` del esquema lo
    admite, que es la parte que un `UPDATE` a medias rompería.
    """

    def setUp(self):
        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_inversa_"))
        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        aplicar_esquema(self.conexion)
        self.caso_id = alta_de_caso(
            self.conexion, numero_caso="BARC2608", fecha_viaje="2026-08-20",
            unidad_numero="7000011", unidad_nombre="Barrio Fulano",
        ).id
        guardar_procedencia_de_campo(
            self.conexion, "casos", self.caso_id, "fecha_viaje", origen="ocr",
            confianza=0.9,
        )
        self.companero_id = self.conexion.execute(
            "INSERT INTO companeros (nombre, activo, creado_en) "
            "VALUES ('Miguel', 1, '2026-09-03T10:00:00')"
        ).lastrowid

    def tearDown(self):
        self.conexion.close()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def _fecha(self):
        return leer_procedencia_de_campo(
            self.conexion, "casos", self.caso_id, "fecha_viaje"
        )

    def test_deshacer_borra_la_firma_entera_y_no_media(self):
        marcar_campo_verificado(
            self.conexion, "casos", self.caso_id, "fecha_viaje", self.companero_id
        )
        firmada = self._fecha()
        self.assertEqual(firmada["verificado"], 1)
        self.assertIsNotNone(firmada["verificado_por"])
        self.assertIsNotNone(firmada["verificado_en"])

        desmarcar_campo_verificado(self.conexion, "casos", self.caso_id, "fecha_viaje")

        suelta = self._fecha()
        self.assertEqual(suelta["verificado"], 0)
        self.assertIsNone(suelta["verificado_por"])
        self.assertIsNone(suelta["verificado_en"])

    def test_deshacer_no_toca_de_donde_salio_el_valor(self):
        """Desverificar dice «nadie lo ha dado por bueno», no «no se leyó»."""
        marcar_campo_verificado(
            self.conexion, "casos", self.caso_id, "fecha_viaje", self.companero_id
        )
        desmarcar_campo_verificado(self.conexion, "casos", self.caso_id, "fecha_viaje")
        suelta = self._fecha()
        self.assertEqual(suelta["origen"], "ocr")
        self.assertEqual(suelta["confianza"], 0.9)

    def test_deshacer_lo_que_nunca_se_firmo_no_revienta(self):
        """Es idempotente: deshacer dos veces deja lo mismo que deshacer una."""
        desmarcar_campo_verificado(self.conexion, "casos", self.caso_id, "fecha_viaje")
        self.assertEqual(self._fecha()["verificado"], 0)

    def test_no_se_puede_deshacer_lo_que_no_tiene_procedencia(self):
        """Mismo criterio que `marcar_campo_verificado`: sin fila no hay nada que decir."""
        with self.assertRaises(ErrorDeValidacion):
            desmarcar_campo_verificado(
                self.conexion, "casos", self.caso_id, "unidad_nombre"
            )


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class LosDosBotonesSinTeclearNada(unittest.TestCase):
    """El caso del dueño tal como QA lo midió: 4 personas, dos campos vacíos.

    Dado un caso importado en el que el número de unidad y el MRN de la tercera
    persona vinieron vacíos, cuando Miguel pulsa «Ninguna ordenanza marcada» y
    «Todo correcto» sin teclear nada, entonces ningún campo vacío queda firmado y
    todos los que sí traían valor, sí.
    """

    def setUp(self):
        import tkinter as tk

        from interfaz.correccion import PantallaDeCorreccion
        from interfaz.tema import aplicar_tema

        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_vacio_"))
        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        aplicar_esquema(self.conexion)
        # El mes de la fecha y el del número de caso coinciden a propósito: un mes
        # cruzado abre un aviso, y un aviso abierto deja la prueba esperando.
        self.caso_id = alta_de_caso(
            self.conexion, numero_caso="BARC2608", fecha_viaje="2026-08-20",
            unidad_numero=None, unidad_nombre="Barrio Fulano",
        ).id
        for campo in CAMPOS_DEL_CASO:
            vacio = campo == CAMPO_VACIO_DEL_CASO
            guardar_procedencia_de_campo(
                self.conexion, "casos", self.caso_id, campo,
                origen="vacio" if vacio else "ocr",
                confianza=None if vacio else 0.9,
            )
        self.personas = []
        for fila in range(1, PERSONAS_DEL_CASO + 1):
            sin_mrn = fila == FILA_DE_LA_PERSONA_SIN_MRN
            persona_id = alta_de_persona(
                self.conexion, self.caso_id,
                mrn=None if sin_mrn else f"055-1111-{3850 + fila:04d}",
                nombre=f"PERSONA {fila}", fila_formulario=fila,
            )
            self.personas.append(persona_id)
            for campo in CAMPOS_DE_LA_PERSONA:
                vacio = sin_mrn and campo == CAMPO_VACIO_DE_LA_PERSONA
                guardar_procedencia_de_campo(
                    self.conexion, "personas", persona_id, campo,
                    origen="vacio" if vacio else "ocr",
                    confianza=None if vacio else 0.9,
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

    def _pulsar_los_dos_botones(self):
        """Los dos gestos del pie, sin teclear ni un carácter."""
        with mock.patch("interfaz.correccion.dialogos.preguntar_si_o_no", return_value=True), \
             mock.patch("interfaz.correccion.dialogos.informar"):
            self.pantalla.resolver_las_casillas_sin_leer()
            self.raiz.update()
            self.pantalla.verificar()
            self.raiz.update()

    def _vacios_firmados(self):
        """La misma consulta con la que QA cazó el defecto."""
        return self.conexion.execute(
            "SELECT COUNT(*) AS cuantos FROM procedencia_campo "
            "WHERE origen = 'vacio' AND verificado = 1"
        ).fetchone()["cuantos"]

    def _firmados(self):
        return self.conexion.execute(
            "SELECT COUNT(*) AS cuantos FROM procedencia_campo WHERE verificado = 1"
        ).fetchone()["cuantos"]

    def _campos_con_valor_vacio(self):
        """Las filas de procedencia cuyo campo está a NULL en su tabla."""
        del_caso = self.conexion.execute(
            "SELECT unidad_numero FROM casos WHERE id = ?", (self.caso_id,)
        ).fetchone()
        vacios = 1 if del_caso["unidad_numero"] is None else 0
        for persona in leer_personas_del_caso(self.conexion, self.caso_id):
            vacios += sum(
                1 for campo in CAMPOS_DE_LA_PERSONA if persona[campo] is None
            )
        return vacios

    # ---- las pruebas ----------------------------------------------------

    def test_la_premisa_el_caso_llega_con_dos_campos_vacios(self):
        """Antes de nada: que el montaje es el que QA midió, y no otro."""
        self.assertEqual(self._campos_con_valor_vacio(), 2)
        self.assertEqual(self._firmados(), 0)

    def test_los_dos_botones_no_dejan_ningun_campo_vacio_firmado(self):
        """Criterio 1 del pase, con la misma consulta que usó QA."""
        self._pulsar_los_dos_botones()
        self.assertEqual(self._vacios_firmados(), 0)

    def test_lo_que_si_traia_valor_si_queda_firmado(self):
        """La otra dirección de la regla 5: no se le impide confirmar lo revisado.

        36 filas de procedencia menos las 2 vacías dan 34. Que el número sea 34 y
        no 0 es la mitad que impide «arreglarlo» dejando de firmar nada.
        """
        self._pulsar_los_dos_botones()
        self.assertEqual(self._firmados(), 34)

    def test_el_contador_dice_treinta_y_cuatro_de_treinta_y_seis(self):
        """Lo que Miguel ve tiene que ser lo que hay en la base, no un redondeo."""
        self._pulsar_los_dos_botones()
        self.assertEqual(self.pantalla._campos_verificados(), (34, 36))

    def test_el_dialogo_avisa_de_cuantos_se_quedan_sin_firmar(self):
        """Un campo que no se firma y no se dice es un campo perdido en silencio."""
        pregunta = self.pantalla._pregunta_de_todo_correcto()
        self.assertIn("2", pregunta)
        self.assertIn("vac", pregunta.lower())

    def test_un_vacio_que_miguel_teclea_si_se_firma(self):
        """Tecleado deja de estar vacío, y entonces sí es suyo para confirmar."""
        bloque = self.pantalla._bloques[FILA_DE_LA_PERSONA_SIN_MRN - 1]
        bloque.campos["mrn"].entrada.insert(0, "055-1111-3999")
        self.pantalla._recontar()
        self._pulsar_los_dos_botones()
        fila = leer_procedencia_de_campo(
            self.conexion, "personas", self.personas[FILA_DE_LA_PERSONA_SIN_MRN - 1], "mrn"
        )
        self.assertEqual(fila["origen"], "manual")
        self.assertEqual(fila["verificado"], 1)

    def test_un_valor_borrado_a_mano_tampoco_se_firma(self):
        """La puerta que la regla del origen dejaba abierta.

        Borrando un valor leído, su origen pasa a `'manual'` y ya no es `'vacio'`:
        una regla escrita sobre el origen lo firmaría. La regla es sobre el VALOR.
        """
        bloque = self.pantalla._bloques[0]
        bloque.campos["mrn"].entrada.delete(0, "end")
        self.pantalla._recontar()
        self._pulsar_los_dos_botones()
        fila = leer_procedencia_de_campo(
            self.conexion, "personas", self.personas[0], "mrn"
        )
        self.assertIsNone(
            leer_personas_del_caso(self.conexion, self.caso_id)[0]["mrn"]
        )
        self.assertEqual(fila["verificado"], 0)

    def test_las_casillas_resueltas_a_mano_si_se_firman(self):
        """Un 0 puesto por Miguel es un valor, no un vacío: se firma.

        Es la diferencia entre «no leída» y «no marcada», que es la distinción que
        `interfaz/casilla.py` existe para sostener. Si al arreglar el vacío se
        dejaran de firmar las 24 casillas, el botón perdería su motivo.
        """
        self._pulsar_los_dos_botones()
        # Se filtra en Python y NO se arma un `IN (...)` con `.format()`:
        # `pruebas/auditoria_sql.py` lee el árbol de sintaxis y dictamina INSEGURA
        # cualquier instrucción construida con una cadena, sin excepción por «los
        # valores son constantes mías». Una lista blanca que admite excepciones
        # deja de ser una lista blanca, y esa auditoría protege la regla que impide
        # una inyección. Medido: con el `.format()` la suite daba 280 conformes de
        # 281 y el veredicto NO PASA.
        filas = self.conexion.execute(
            "SELECT campo FROM procedencia_campo WHERE verificado = 1"
        ).fetchall()
        firmadas = sum(1 for fila in filas if fila["campo"] in CASILLAS_DE_ORDENANZAS)
        self.assertEqual(firmadas, PERSONAS_DEL_CASO * len(CASILLAS_DE_ORDENANZAS))

    # ---- deshacer desde la pantalla -------------------------------------

    def test_hay_forma_de_deshacer_la_verificacion_desde_el_programa(self):
        """Criterio 2 del pase. Un error de dos pulsaciones tiene salida."""
        self._pulsar_los_dos_botones()
        self.assertEqual(self._firmados(), 34)
        with mock.patch("interfaz.correccion.dialogos.preguntar_si_o_no", return_value=True), \
             mock.patch("interfaz.correccion.dialogos.informar"):
            self.pantalla.deshacer_la_verificacion()
            self.raiz.update()
        self.assertEqual(self._firmados(), 0)
        self.assertEqual(self.pantalla._campos_verificados(), (0, 36))

    def test_deshacer_pregunta_antes_y_un_no_no_deshace_nada(self):
        """Mismo freno que al firmar, y por el mismo motivo: es una sola pulsación."""
        self._pulsar_los_dos_botones()
        with mock.patch("interfaz.correccion.dialogos.preguntar_si_o_no", return_value=False), \
             mock.patch("interfaz.correccion.dialogos.informar"):
            self.pantalla.deshacer_la_verificacion()
            self.raiz.update()
        self.assertEqual(self._firmados(), 34)

    def test_deshacer_no_borra_ningun_dato_del_caso(self):
        """Deshacer la firma no deshace las correcciones: son dos cosas distintas."""
        self._pulsar_los_dos_botones()
        antes = [dict(p) for p in leer_personas_del_caso(self.conexion, self.caso_id)]
        with mock.patch("interfaz.correccion.dialogos.preguntar_si_o_no", return_value=True), \
             mock.patch("interfaz.correccion.dialogos.informar"):
            self.pantalla.deshacer_la_verificacion()
        despues = [dict(p) for p in leer_personas_del_caso(self.conexion, self.caso_id)]
        self.assertEqual(antes, despues)
