"""Cada persona sabe de que hoja del PDF salio, y su tira se recorta de ESA hoja.

El defecto que estas pruebas cierran estaba medido y era peor que no ensenar
tira: un caso de grupo reparte doce personas en seis paginas, `casos.pagina_pdf`
es UNA sola, y la pantalla de correccion construia una unica `PaginaDelCaso` con
esa pagina y se la pasaba a los doce bloques. Las once personas de las paginas 2
a 6 ensenaban una tira recortada de la pagina 1.

Una tira equivocada invita a dar por bueno un MRN comparandolo con la imagen de
otra persona. La tira existe justo para no tener que fiarse del OCR, asi que una
tira de otra hoja es peor que un hueco.

El criterio del que salen estas pruebas, en Given/When/Then:

  DADO un caso cuyas personas vienen de seis paginas distintas,
  CUANDO se abre la pantalla de correccion,
  ENTONCES la tira de una persona de la pagina 4 se recorta de la pagina 4,
  Y F2 anuncia la pagina de la persona en la que esta el foco.
"""

import shutil
import sqlite3
import tempfile
import unittest
from pathlib import Path

from datos.conexion import abrir_conexion
from datos.esquema import (
    VERSION_ACTUAL,
    aplicar_esquema,
    crear_tablas_de_la_version_inicial,
    marca_de_tiempo,
    version_de_la_base,
)
from datos.repositorio import alta_de_caso, alta_de_persona, leer_personas_del_caso
from datos.validacion import ErrorDeValidacion
from pruebas.comun import PruebaConBaseTemporal

PAGINAS_DEL_GRUPO = 6


class LaMigracionALaVersion4(unittest.TestCase):
    """Una base que ya existe en disco gana la columna sin perder nada.

    Se construye la base vieja con el DDL literal de la version 1, y no llamando
    a `aplicar_esquema`, por el mismo motivo que `prueba_migraciones.py`: llamar
    a `aplicar_esquema` ya migraria, y entonces la prueba comprobaria el camino
    facil y no el que de verdad se puede romper.
    """

    def setUp(self):
        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_v4_"))
        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        crear_tablas_de_la_version_inicial(self.conexion)
        self.conexion.execute(
            "INSERT INTO version_esquema (version, aplicada_en, descripcion) "
            "VALUES (1, ?, 'inicial')",
            (marca_de_tiempo(),),
        )
        self.conexion.execute(
            "INSERT INTO casos (numero_caso, unidad_numero, fecha_viaje, creado_en) "
            "VALUES ('VIEJ2609', '700001', '2026-09-08', ?)",
            (marca_de_tiempo(),),
        )
        self.conexion.execute(
            "INSERT INTO personas (caso_id, mrn, nombre, fila_formulario) "
            "VALUES (1, '055-1111-3853', 'FILA VIEJA', 1)"
        )

    def tearDown(self):
        self.conexion.close()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def _columnas_de_personas(self):
        return [fila[1] for fila in self.conexion.execute("PRAGMA table_info(personas)")]

    def test_la_base_vieja_de_verdad_no_tenia_la_columna(self):
        """Control positivo: sin esto, todo lo de abajo podria no probar nada."""
        self.assertNotIn("pagina_pdf", self._columnas_de_personas())

    def test_personas_gana_la_pagina_del_pdf(self):
        aplicar_esquema(self.conexion)
        self.assertIn("pagina_pdf", self._columnas_de_personas())

    def test_la_base_llega_a_la_version_actual_y_la_version_actual_es_la_4(self):
        aplicar_esquema(self.conexion)
        self.assertEqual(version_de_la_base(self.conexion), VERSION_ACTUAL)
        self.assertGreaterEqual(VERSION_ACTUAL, 4)

    def test_la_persona_que_ya_estaba_gana_un_hueco_y_no_una_pagina_inventada(self):
        """De una persona guardada antes de esta version nadie sabe de que hoja salio.

        Un `1` por defecto seria una hoja inventada, y su tira se recortaria de un
        papel que no es el suyo — que es exactamente el defecto que se arregla.
        """
        aplicar_esquema(self.conexion)
        fila = self.conexion.execute("SELECT nombre, pagina_pdf FROM personas").fetchone()
        self.assertEqual(fila["nombre"], "FILA VIEJA")
        self.assertIsNone(fila["pagina_pdf"])

    def test_el_motor_rechaza_una_pagina_menor_que_uno(self):
        aplicar_esquema(self.conexion)
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute("UPDATE personas SET pagina_pdf = 0")

    def test_aplicar_el_esquema_dos_veces_no_repite_la_migracion(self):
        aplicar_esquema(self.conexion)
        aplicar_esquema(self.conexion)
        self.assertEqual(
            self.conexion.execute(
                "SELECT COUNT(*) FROM version_esquema WHERE version = ?", (VERSION_ACTUAL,)
            ).fetchone()[0],
            1,
        )
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM personas").fetchone()[0], 1
        )


class LaPaginaDeLaPersonaViajaHastaLaBase(PruebaConBaseTemporal):
    """Guardar una persona con su hoja y volver a leerla."""

    def _caso(self):
        return alta_de_caso(
            self.conexion, numero_caso="SURB2609", fecha_viaje="2026-09-08", pagina_pdf=1
        ).id

    def test_se_guarda_y_se_vuelve_a_leer(self):
        caso_id = self._caso()
        alta_de_persona(
            self.conexion, caso_id, mrn="055-1111-3853", nombre="UNO", pagina_pdf=4
        )
        personas = leer_personas_del_caso(self.conexion, caso_id)
        self.assertEqual(personas[0]["pagina_pdf"], 4)

    def test_sin_pagina_se_guarda_el_hueco_y_no_un_uno(self):
        caso_id = self._caso()
        alta_de_persona(self.conexion, caso_id, mrn="055-1111-3853", nombre="UNO")
        self.assertIsNone(leer_personas_del_caso(self.conexion, caso_id)[0]["pagina_pdf"])

    def test_una_pagina_cero_se_rechaza_antes_de_llegar_al_motor(self):
        caso_id = self._caso()
        with self.assertRaises(ErrorDeValidacion) as recogido:
            alta_de_persona(
                self.conexion, caso_id, mrn="055-1111-3853", nombre="UNO", pagina_pdf=0
            )
        self.assertIn("pagina_pdf", str(recogido.exception))


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
class LaTiraSaleDeLaHojaDeCadaPersona(unittest.TestCase):
    """La pantalla de correccion de un grupo de seis hojas, abierta de verdad.

    La ventana se abre transparente y no retirada por el motivo que ya midio
    `prueba_correccion.py`: una ventana con `withdraw()` no esta dibujada y Tk
    devuelve geometrias falsas.
    """

    def setUp(self):
        import tkinter as tk

        from interfaz.correccion import PantallaDeCorreccion
        from interfaz.tema import aplicar_tema

        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_tira_"))
        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        aplicar_esquema(self.conexion)
        # El caso lo abrio la hoja 1, y cada persona viene de una hoja distinta.
        caso = alta_de_caso(
            self.conexion,
            numero_caso="SURB2609",
            fecha_viaje="2026-09-08",
            ruta_pdf=str(self.carpeta / "grupo.pdf"),
            pagina_pdf=1,
        )
        for hoja in range(1, PAGINAS_DEL_GRUPO + 1):
            alta_de_persona(
                self.conexion,
                caso.id,
                mrn=f"055-1111-{3850 + hoja:04d}",
                nombre=f"PERSONA DE LA HOJA {hoja}",
                fila_formulario=hoja,
                pagina_pdf=hoja,
            )

        self.raiz = tk.Tk()
        self.raiz.geometry("1100x720")
        self.raiz.attributes("-alpha", 0.0)
        aplicar_tema(self.raiz)
        self.pantalla = PantallaDeCorreccion(
            self.raiz, self.conexion, caso.id, al_volver=lambda: None,
            carpeta_de_datos=self.carpeta,
        )
        self.pantalla.grid(row=0, column=0, sticky="nsew")
        self.raiz.update()
        # Sin esto, `focus_get()` devuelve None y las pruebas del foco medirian
        # que la ventana no tiene el foco del sistema en vez de medir el codigo.
        # Es la misma correccion que ya lleva `prueba_correccion.py`.
        self.raiz.focus_force()
        self.raiz.update()

    def tearDown(self):
        self.conexion.close()
        self.raiz.destroy()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def test_cada_bloque_recorta_de_la_hoja_de_su_persona(self):
        paginas = [bloque.pagina_pdf for bloque in self.pantalla._bloques]
        self.assertEqual(paginas, list(range(1, PAGINAS_DEL_GRUPO + 1)))

    def test_la_hoja_de_una_persona_no_es_la_del_caso_por_defecto(self):
        """Sin el arreglo, las seis daban 1: la pagina del caso repetida."""
        self.assertEqual(self.pantalla._bloques[3].pagina_pdf, 4)
        self.assertNotEqual(
            self.pantalla._bloques[3].pagina_pdf, self.pantalla.caso["pagina_pdf"]
        )

    def test_cada_hoja_se_rasteriza_una_sola_vez_y_no_una_por_tira(self):
        """Seis hojas son seis objetos de pagina, no doce ni sesenta."""
        self.assertEqual(len(self.pantalla.paginas.rasterizadas()), PAGINAS_DEL_GRUPO)

    def test_f2_anuncia_la_hoja_de_la_persona_enfocada(self):
        self.pantalla._bloques[3].primer_control().focus_set()
        self.raiz.update()
        self.assertEqual(self.pantalla._pagina_en_foco(), 4)
        self.assertIn("PÁGINA 4", self.pantalla._aviso_de_la_pagina(4))

    def test_con_el_foco_en_otra_persona_f2_anuncia_otra_hoja(self):
        """Es la prueba de que anuncia la del foco y no una fija."""
        self.pantalla._bloques[5].primer_control().focus_set()
        self.raiz.update()
        self.assertEqual(self.pantalla._pagina_en_foco(), 6)

    def test_fuera_de_los_bloques_f2_anuncia_la_hoja_del_caso(self):
        """Con el foco en los datos del caso, la hoja que manda es la del caso."""
        self.pantalla._campos_del_caso["fecha_viaje"].entrada.focus_set()
        self.raiz.update()
        self.assertEqual(self.pantalla._pagina_en_foco(), 1)

    def test_con_el_pdf_ausente_el_caso_se_abre_igual_y_se_dice_por_que(self):
        """El PDF de esta prueba no existe: la hoja falla, y el caso se corrige igual.

        Importa porque el motivo dejo de salir de UNA pagina y ahora sale de las
        que se hayan llegado a pedir. Un caso que no se abre porque falta una
        imagen es un caso que nadie atiende, y seis frases identicas en la
        cabecera no dicen mas que una.

        Se pide una hoja a mano porque las hojas se cargan cuando alguien recorta:
        sin ninguna banda guardada no hay nada que recortar y no se intenta abrir
        el PDF. Eso ya era asi antes de este arreglo y no cambia.
        """
        self.pantalla.paginas.pagina(4).imagen()
        motivo = self.pantalla.paginas.motivo_del_fallo
        self.assertIsNotNone(motivo)
        avisos = [
            texto
            for texto in self.pantalla._textos_de_aviso()
            if "No se puede mostrar el escaneo" in texto
        ]
        self.assertEqual(len(avisos), 1)
        self.assertIn(motivo, avisos[0])
        self.assertEqual(len(self.pantalla._bloques), PAGINAS_DEL_GRUPO)


if __name__ == "__main__":
    unittest.main()
