"""La lista de estados de la recomendacion y su clasificacion.

Lo que se prueba aqui no es una lista de valores —esa la decide el dueno y hoy
esta a medias—, sino que el **mecanismo** que la usa no se pueda romper en
silencio: que el texto que viaja al motor no case a medias, y que un valor mal
escrito levante en vez de apagar la alarma.
"""

import unittest
from unittest import mock

from datos import estados
from datos.estados import (
    ESTADOS_QUE_RESUELVEN,
    ESTADOS_RECOMENDACION,
    ErrorDeEstados,
    recomendacion_sin_resolver,
    texto_de_estados_que_resuelven,
)


class PruebaDeLosEstados(unittest.TestCase):
    def test_completa_es_el_unico_valor_que_resuelve_la_recomendacion(self):
        """~~Hoy ningun valor conocido resuelve~~ — el dueno dijo cual el 2026-09-03.

        La prueba anterior exigia `ESTADOS_QUE_RESUELVEN == ()` y decia de si misma
        «falla el dia que el dueno diga el valor, y eso es lo que se busca». Ese dia
        llego: el dueno dijo «completa» y «no está completa» (DECISIONES.md,
        2026-09-03), asi que la prueba cambia de valor esperado y no de intencion.

        `no_completa` NO resuelve, y es lo que de verdad hay que fijar aqui: un
        documento dado por incompleto tiene que seguir saliendo en el bloque rojo.
        """
        self.assertEqual(
            ("no_indicada", "incompleta", "completa", "no_completa"),
            ESTADOS_RECOMENDACION,
        )
        self.assertEqual(("completa",), ESTADOS_QUE_RESUELVEN)
        self.assertNotIn("no_completa", ESTADOS_QUE_RESUELVEN)

    def test_una_recomendacion_sin_estado_cuenta_como_sin_resolver(self):
        self.assertTrue(recomendacion_sin_resolver(None))
        self.assertTrue(recomendacion_sin_resolver("incompleta"))

    def test_con_la_lista_vacia_el_texto_es_solo_el_separador(self):
        """El caso de la lista vacia sigue probado, ahora con la lista simulada.

        Dejo de ser el estado real del programa el 2026-09-03 —`ESTADOS_QUE_RESUELVEN`
        ya lleva `completa`—, pero la regla que protege no ha cambiado: `,,` contendria
        el hueco `,` + `` + `,` y un estado vacio colado por SQL crudo casaria dentro.
        """
        with mock.patch.object(estados, "ESTADOS_QUE_RESUELVEN", ()):
            self.assertEqual(",", texto_de_estados_que_resuelven())

    def test_con_la_lista_de_hoy_el_texto_rodea_a_completa(self):
        self.assertEqual(",completa,", texto_de_estados_que_resuelven())

    def test_el_texto_lleva_separador_a_los_dos_lados_de_cada_valor(self):
        with mock.patch.object(estados, "ESTADOS_QUE_RESUELVEN", ("no_indicada",)):
            self.assertEqual(",no_indicada,", texto_de_estados_que_resuelven())
        with mock.patch.object(
            estados, "ESTADOS_QUE_RESUELVEN", ("no_indicada", "incompleta")
        ):
            self.assertEqual(",no_indicada,incompleta,", texto_de_estados_que_resuelven())

    def test_los_separadores_impiden_que_un_valor_case_dentro_de_otro(self):
        """La razon de que el texto vaya rodeado de comas y no pelado.

        'completa' es substring de 'incompleta'. Con los separadores puestos, la
        busqueda de ',completa,' dentro de ',incompleta,' no encuentra nada, que es
        justo lo que evita dar por resuelta una recomendacion incompleta.
        """
        self.assertIn("completa", "incompleta")
        self.assertNotIn(",completa,", ",incompleta,")

    def test_un_valor_que_no_se_puede_guardar_no_puede_resolver_nada(self):
        """El valor de ejemplo cambia porque «completa» ya SI se puede guardar.

        La prueba usaba `completa` como «un valor que no esta en
        `ESTADOS_RECOMENDACION`». Desde el 2026-09-03 lo esta, asi que el ejemplo
        pasa a ser una errata verosimil de verdad: `completado`.
        """
        with mock.patch.object(estados, "ESTADOS_QUE_RESUELVEN", ("completado",)):
            with self.assertRaises(ErrorDeEstados) as fallo:
                texto_de_estados_que_resuelven()
        self.assertIn("completado", str(fallo.exception))

    def test_un_estado_con_el_separador_dentro_se_rechaza(self):
        with mock.patch.object(estados, "ESTADOS_RECOMENDACION", ("uno,dos",)):
            with self.assertRaises(ErrorDeEstados):
                texto_de_estados_que_resuelven()


if __name__ == "__main__":
    unittest.main()
