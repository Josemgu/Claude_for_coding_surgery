"""`fichas.log`: una línea por cosa que tarda, y ni un dato de una persona.

**El criterio sale de una petición del dueño que no se podía cumplir:** *«verifica
los logs, un programa como ese debe ser rápido»* (2026-09-03). Medido antes de
escribir nada: `grep -rln logging` sobre `fichas.py`, `interfaz/`, `datos/` e
`importacion/` no devolvía una sola línea, y en `Documentos\\Fichas` no había
ningún `.log`. No había nada que verificar.

    DADO el programa arrancado,
    CUANDO abre la ventana, abre un caso, lee una página o genera un paquete,
    ENTONCES queda una línea en `fichas.log` con lo que tardó en segundos,
    Y en esa línea NO hay ningún nombre ni ningún MRN.

La segunda mitad no es formalismo: este archivo se va a mandar por correo para
diagnosticar una lentitud, y un `.log` con cédulas de miembro dentro es una fuga
que nadie ve venir.
"""

import shutil
import tempfile
import unittest
from pathlib import Path

from datos.registro import (
    ARRANQUE,
    BYTES_POR_ARCHIVO,
    CASO,
    CUANTOS_ARCHIVOS,
    NOMBRE_DEL_ARCHIVO,
    Cronometro,
    apuntar,
    preparar_el_registro,
)


class PruebaConRegistro(unittest.TestCase):
    """Un `fichas.log` en una carpeta temporal, que se borra al terminar."""

    def setUp(self):
        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_registro_"))
        self.addCleanup(shutil.rmtree, self.carpeta, ignore_errors=True)
        self.ruta = preparar_el_registro(self.carpeta)

    def leer(self):
        """Lo que hay escrito en el registro, ya cerrado para poder leerlo."""
        import logging

        for salida in logging.getLogger("fichas.tiempos").handlers:
            salida.flush()
        return self.ruta.read_text(encoding="utf-8")


class ElRegistroSeCreaDondeEstanLosDatos(PruebaConRegistro):
    """Junto a la base, que es la carpeta que el dueño ya sabe encontrar."""

    def test_el_archivo_se_llama_fichas_log_y_esta_en_la_carpeta_de_datos(self):
        self.assertEqual(self.ruta, self.carpeta / NOMBRE_DEL_ARCHIVO)
        self.assertTrue(self.ruta.is_file())

    def test_una_carpeta_que_no_se_puede_escribir_no_tumba_el_programa(self):
        """Un programa que no arranca porque no pudo escribir su log sería peor."""
        inexistente = self.carpeta / "no" / "existe" / "esta"
        self.assertIsNone(preparar_el_registro(inexistente))
        # Y apuntar despues tampoco levanta: simplemente no escribe.
        apuntar(ARRANQUE, 1.0)

    def test_rota_a_un_mega_por_tres_archivos(self):
        """Sin tope, el registro acaba llenando la carpeta de datos del dueño."""
        self.assertEqual(BYTES_POR_ARCHIVO, 1_000_000)
        self.assertEqual(CUANTOS_ARCHIVOS, 3)


class CadaLineaLlevaSuNumeroDeSegundos(PruebaConRegistro):
    """Un registro sin la cifra no sirve para lo que este existe."""

    def test_la_linea_dice_que_fue_y_cuanto_tardo(self):
        apuntar(ARRANQUE, 1.234)
        texto = self.leer()
        self.assertIn(ARRANQUE, texto)
        self.assertIn("1.234s", texto)

    def test_el_cronometro_deja_su_linea_al_salir(self):
        with Cronometro(CASO, caso=12):
            pass
        self.assertIn("caso=12", self.leer())

    def test_el_cronometro_deja_su_linea_TAMBIEN_si_algo_levanta(self):
        """Lo que tarda en fallar es justo lo que hace falta cuando algo se cuelga."""
        with self.assertRaises(ValueError):
            with Cronometro(CASO, caso=7):
                raise ValueError("lo que sea")
        self.assertIn("caso=7", self.leer())

    def test_los_detalles_que_son_numeros_se_escriben_tal_cual(self):
        apuntar(CASO, 0.5, caso=41, paginas=6)
        texto = self.leer()
        self.assertIn("caso=41", texto)
        self.assertIn("paginas=6", texto)


class EnElRegistroNoEntraNiUnDatoDeUnaPersona(PruebaConRegistro):
    """La mitad que impide que diagnosticar una lentitud sea una fuga de datos."""

    def test_un_nombre_con_espacios_no_se_escribe(self):
        apuntar(CASO, 0.1, quien="Jose Miguel Anonimo")
        texto = self.leer()
        self.assertNotIn("Jose Miguel Anonimo", texto)
        self.assertIn("<str>", texto)

    def test_un_texto_largo_tampoco_se_escribe(self):
        apuntar(CASO, 0.1, algo="x" * 200)
        self.assertNotIn("x" * 200, self.leer())

    def test_el_numero_de_caso_SI_se_escribe(self):
        """Es corto, no lleva espacios, y sin él la línea no lleva a ningún sitio."""
        apuntar(CASO, 0.1, numero="BALC2609")
        self.assertIn("numero=BALC2609", self.leer())

    def test_un_mrn_suelto_pasa_el_filtro_y_por_eso_nadie_lo_pasa(self):
        """⚠️ El filtro NO sabe qué es un MRN: es corto y sin espacios, así que si
        alguien se lo pasara, entraría.

        Lo que impide la fuga es que **ninguna llamada del programa le pase uno**:
        se apuntan el id del caso, su número, cuántas páginas y cuántos segundos.
        Esta prueba deja escrito el límite para que nadie lo descubra tarde.
        """
        apuntar(CASO, 0.1, ojo="055-1111-3853")
        self.assertIn("055-1111-3853", self.leer())

    def test_ninguna_llamada_del_programa_apunta_un_dato_de_persona(self):
        """La comprobación de verdad: se leen las llamadas que hay en el código.

        Se listan los nombres de los detalles que el programa pasa de verdad, y se
        exige que todos estén en la lista blanca de lo que se puede escribir.
        """
        import ast

        permitidos = {"caso", "numero", "paginas", "casos", "archivo"}
        raiz = Path(__file__).resolve().parent.parent
        encontrados = set()
        for archivo in raiz.rglob("*.py"):
            if ".venv" in archivo.parts or "pruebas" in archivo.parts:
                continue
            arbol = ast.parse(archivo.read_text(encoding="utf-8"))
            for nodo in ast.walk(arbol):
                if not isinstance(nodo, ast.Call):
                    continue
                nombre = getattr(nodo.func, "id", None) or getattr(nodo.func, "attr", None)
                if nombre in ("apuntar", "Cronometro"):
                    encontrados.update(
                        clave.arg for clave in nodo.keywords if clave.arg
                    )
        self.assertTrue(encontrados, "no se encontró ninguna llamada al registro")
        self.assertEqual(
            encontrados - permitidos,
            set(),
            "alguien apunta un detalle que no está en la lista blanca del registro",
        )


if __name__ == "__main__":
    unittest.main()
