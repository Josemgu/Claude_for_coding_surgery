"""Las cuatro auditorias corren con la suite, y no cuando alguien se acuerda.

Hasta la auditoria final de QA, los cuatro `pruebas/auditoria_*.py` eran archivos
que habia que correr a mano. El patron con el que se descubren las pruebas
—`prueba_*.py`— no los recoge, asi que **los criterios que protegen las reglas
permanentes 1 y 3, la de SQL y la de las rutas no los ejecutaba nadie**. Un
criterio que hay que acordarse de comprobar es un criterio que algun dia no se
comprueba, y es el mismo motivo por el que el espejo se regenera solo en vez de
tener un boton de exportar (`PENDIENTES.md`, FASE 4).

Este archivo SI casa con el patron, asi que las cuatro entran en la suite por la
puerta de siempre y sin ningun invento: cada prueba llama a la auditoria y exige
su veredicto.

**Se llama al `main` de cada una y se mira su codigo de salida**, que es lo mismo
que mira la persona que las corre a mano, y no a sus funciones internas: una
prueba que llama a `auditar()` y se salta `main()` aprueba una auditoria cuyo
veredicto podria estar escrito al reves.

⚠️ **`auditoria_extraccion_real` se salta cuando no hay PDF que leer.** Ese archivo
lo dice de si mismo: depende de `pdfs_referencia/`, que lleva nombres y MRN de
personas reales y esta fuera del repositorio por `.gitignore`. En otra maquina no
habria nada que leer, y un fallo de entorno disfrazado de fallo de codigo hace
perder mas tiempo que la prueba que ahorra. Cuando SI hay PDF, corre: son unos 75
segundos, y es la unica de las cuatro que cuesta algo.
"""

import io
import unittest
from contextlib import redirect_stdout

from pruebas import auditoria_dependencias, auditoria_rutas, auditoria_sql
from pruebas.auditoria_dependencias import carpetas_por_defecto
from pruebas.auditoria_extraccion_real import CARPETA_DE_LOS_PDF

# Las dos listas de alcance, derivadas del disco y no escritas a mano: es lo que
# impide que un paquete nuevo se quede sin auditar porque nadie se acordo. La de
# `auditoria_dependencias` ya hace ese trabajo, asi que se reutiliza.
TODO_EL_CODIGO = carpetas_por_defecto()
CODIGO_DEL_PROGRAMA = [
    ruta for ruta in TODO_EL_CODIGO if not ruta.endswith(("pruebas", "prueba_ocr.py"))
]


def _correr(auditoria, argumentos=None):
    """Ejecuta el `main` de una auditoria en silencio. Devuelve (codigo, salida).

    La salida se recoge en vez de dejarla ir a la consola porque estas cuatro
    imprimen su informe entero —decenas de lineas cada una— y la suite tiene 500
    pruebas: sin esto, el informe de las auditorias tapa el resultado de todo lo
    demas. Se devuelve para poder ensenarla cuando el veredicto no es el esperado,
    que es cuando hace falta leerla.
    """
    recogida = io.StringIO()
    with redirect_stdout(recogida):
        codigo = auditoria.main(argumentos if argumentos is not None else [])
    return codigo, recogida.getvalue()


class PruebaDeLasAuditorias(unittest.TestCase):
    """Las tres que no dependen de datos reales, sobre el codigo del proyecto."""

    def _exigir_que_pase(self, auditoria, argumentos=None):
        codigo, salida = _correr(auditoria, argumentos)
        self.assertEqual(0, codigo, f"la auditoría no pasó:\n{salida}")
        return salida

    def test_ninguna_dependencia_es_un_modelo_de_lenguaje(self):
        """Regla permanente 1, sobre TODO el codigo del proyecto.

        Se llama sin argumentos a proposito: el valor por defecto de esa auditoria
        son los ocho paquetes mas los `.py` sueltos de la raiz, derivados del disco.
        Pasarle aqui una lista escrita a mano la dejaria desfasada el dia que
        aparezca un paquete nuevo, que es exactamente el fallo que se acaba de
        arreglar.
        """
        salida = self._exigir_que_pase(auditoria_dependencias)
        self.assertIn("hallazgos (modulo fuera de la lista blanca): 0", salida)

    def test_ninguna_llamada_al_motor_arma_su_sql_con_datos(self):
        """La lista blanca de SQL: N llamadas encontradas, N conformes.

        Su invocacion documentada cubria «datos pruebas», que son 2 de los 8
        paquetes. Se le pasa TODO el codigo: medido, sube de 205 llamadas
        dictaminadas a 214, y las 214 son conformes. Nueve llamadas al motor que
        antes no revisaba nadie.
        """
        self._exigir_que_pase(auditoria_sql, TODO_EL_CODIGO)

    def test_ningun_literal_compone_la_ruta_de_datos_por_nombre(self):
        """La carpeta de datos sale de la API de Windows, no de «Documentos».

        ⚠️ Esta es la unica de las tres que NO se corre sobre el codigo entero, y
        el motivo es que no puede: `pruebas/auditoria_rutas.py` lleva escritos los
        dos nombres prohibidos —son su lista— y `pruebas/prueba_arranque.py` los
        escribe para comprobar que no se usan. Correrla sobre `pruebas/` la hace
        suspenderse a si misma, que es un falso positivo, no un hallazgo. Se corre
        sobre el codigo del programa, que es donde una ruta compuesta por nombre
        haria dano: 74 archivos y 3100 literales examinados.
        """
        self._exigir_que_pase(auditoria_rutas, CODIGO_DEL_PROGRAMA)

    def test_el_guardian_de_dependencias_sigue_cazando_a_un_proveedor(self):
        """Que dé 0 sobre el código bueno no sirve si ya no caza nada malo.

        Sin esto, vaciar la comprobación dejaría las tres pruebas de arriba en
        verde: el resultado «0 hallazgos» lo da igual un guardián que funciona que
        uno que no mira. Se comprueba sobre nombres, no sobre archivos, para no
        escribir en disco un archivo que importe a un proveedor.
        """
        propios = auditoria_dependencias.modulos_del_proyecto()
        for proveedor in ("openai", "anthropic", "google", "transformers", "ollama"):
            with self.subTest(proveedor=proveedor):
                self.assertFalse(
                    auditoria_dependencias.es_admitido(proveedor, propios),
                    f"{proveedor} tendría que ser un hallazgo",
                )

    def test_una_carpeta_sin_codigo_no_aprueba_en_verde(self):
        """Cero importaciones examinadas es un denominador vacío, no un aprobado."""
        codigo, _ = _correr(auditoria_dependencias, ["pdfs_referencia"])
        self.assertEqual(2, codigo)


class PruebaDeLaExtraccionSobreLosPdfReales(unittest.TestCase):
    """La cuarta auditoría: el extractor sobre los formularios de verdad."""

    def test_la_extraccion_sigue_dando_lo_mismo_sobre_los_pdf_reales(self):
        from pruebas import auditoria_extraccion_real

        if not sorted(CARPETA_DE_LOS_PDF.glob("*.pdf")):
            self.skipTest(
                f"No hay PDF en «{CARPETA_DE_LOS_PDF}»: llevan datos de personas "
                "reales y están fuera del repositorio por .gitignore."
            )
        recogida = io.StringIO()
        with redirect_stdout(recogida):
            codigo = auditoria_extraccion_real.main()
        self.assertEqual(0, codigo, f"la extracción cambió:\n{recogida.getvalue()}")
