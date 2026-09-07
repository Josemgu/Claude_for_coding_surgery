"""Que la cifra que se ensena y la lista con la que se arma el paquete son la misma.

⚠️ **Esta prueba nace de una premisa que NO se pudo reproducir.** El aviso que
llego decia que la tarjeta del companero diria «2 documentos» y el boton de generar
paquete escribiria 3 carpetas de PDF. Se midio leyendo el codigo y no es asi: los
tres sitios que ensenan la cifra —`interfaz/companeros.py:207`,
`interfaz/asignacion.py:264` y `:323`— y el que arma el paquete
—`paquete/exportacion.py:192`— llaman los cuatro a la MISMA funcion,
`datos.asignaciones.casos_asignados`, con los mismos argumentos. No hay dos
consultas que puedan discrepar porque no hay dos consultas.

Lo que si es cierto, y queda pinchado aqui para que se vea: **`casos_asignados` no
filtra `archivado`**, y archivar un caso NO desactiva su asignacion. O sea que un
caso archivado sigue contando en la tarjeta y sigue entrando en el paquete. Las dos
cosas a la vez, que es lo coherente.

Si eso esta bien o esta mal es una decision del dueno y no se toma aqui: filtrar
`archivado = 0` cambiaria lo que un companero recibe en su carpeta, y hacerlo por
iniciativa propia seria cambiar en silencio el trabajo que se le manda a otra
persona. Esta prueba deja escrito lo que el programa hace HOY, para que el dia que
se decida se vea que cambia.
"""

import unittest

from datos.asignaciones import asignar_caso, casos_asignados
from datos.companeros import alta_de_companero
from datos.archivo import archivar_caso
from datos.repositorio import alta_de_caso
from pruebas.comun import PruebaConBaseTemporal


class UnCasoArchivadoSigueAsignado(PruebaConBaseTemporal):
    def setUp(self):
        super().setUp()
        self.companero_id = alta_de_companero(self.conexion, "Miguel")
        self.vivo = alta_de_caso(
            self.conexion, numero_caso="CASP2609", fecha_viaje="2026-09-08"
        ).id
        self.archivado = alta_de_caso(
            self.conexion, numero_caso="CASD2609", fecha_viaje="2026-09-08"
        ).id
        asignar_caso(self.conexion, self.vivo, self.companero_id)
        asignar_caso(self.conexion, self.archivado, self.companero_id)
        archivar_caso(self.conexion, self.archivado)

    def test_archivar_no_desactiva_la_asignacion(self):
        """Es el hecho del que sale todo lo demas de este archivo."""
        viva = self.conexion.execute(
            "SELECT activa FROM asignaciones WHERE caso_id = ?", (self.archivado,)
        ).fetchone()
        self.assertEqual(viva["activa"], 1)

    def test_casos_asignados_devuelve_tambien_el_archivado(self):
        """Comportamiento de HOY. No se dictamina si esta bien: es del dueno."""
        casos = casos_asignados(self.conexion, self.companero_id)
        self.assertEqual(len(casos), 2)
        self.assertIn(1, [caso["archivado"] for caso in casos])

    def test_la_fila_trae_su_marca_de_archivado_para_poder_decidir(self):
        """La consulta ya devuelve `archivado`, asi que la decision no cuesta nada.

        Quien quiera separarlos —la pantalla, el paquete, o los dos— tiene el dato
        en la mano y no hace falta otra consulta. Lo que falta es la decision, no
        el dato.
        """
        casos = {caso["id"]: caso for caso in casos_asignados(self.conexion, self.companero_id)}
        self.assertEqual(casos[self.vivo]["archivado"], 0)
        self.assertEqual(casos[self.archivado]["archivado"], 1)


if __name__ == "__main__":
    unittest.main()
