"""Ningun camino de la interfaz deja la base al dia y el Excel atrasado.

Es la invariante que `espejo/escritura.py` escribe de si misma: «mientras la
pantalla llame aqui en vez de llamar al repositorio directamente, **no existe
ningun camino que deje la base al dia y el Excel atrasado**». La auditoria final de
QA midio que si existia, y esta prueba es la que impide que vuelva a existir.

Lo medido antes del arreglo (esta maquina, 2026-09-02):

    asignaciones  base=1  espejo=0
    contactos     base=1  espejo=0

La forma de comprobarlo no es leer el codigo buscando quien llama a
`guardar_y_regenerar` —eso aprueba a quien lo llama en la rama que no se ejecuta—,
sino **hacer la accion de verdad sobre la pantalla de verdad y contar las filas del
`.xlsx` que queda en disco**. Si la accion escribio en la base y el Excel no lo
refleja, la cuenta no cuadra y esto falla.

Cada prueba cuenta las filas de UNA hoja: base contra espejo, el mismo numero.
"""

import tkinter as tk
from datetime import date
from unittest import mock

from openpyxl import load_workbook

from datos.asignaciones import asignar_caso
from datos.companeros import alta_de_companero
from datos.repositorio import alta_de_caso, alta_de_persona, leer_caso_por_id
from espejo.escritura import regenerar_espejo
from espejo.hojas import HOJAS
from espejo.rutas import ruta_del_espejo
from interfaz.tema import aplicar_tema
from datos.pasos import NOMBRES_DE_LOS_PASOS
from paquete.columnas import (
    NOMBRE_DE_LA_HOJA,
    PRIMERA_FILA_DE_DATOS,
    indice_de,
)
from pruebas.comun import PruebaConBaseTemporal

HOY = date(2026, 9, 12)

# Cada hoja del espejo se llama igual que su tabla, y esto lo aprovecha para poder
# contar las filas de la tabla con el mismo lector que las escribe en la hoja.
POR_TABLA = {definicion.nombre: definicion for definicion in HOJAS}


def _sin_avisar(aviso):
    """El espejo no avisa por consola durante las pruebas."""


class PruebaDeLosCaminosQueEscriben(PruebaConBaseTemporal):
    """Una ventana de verdad, una base temporal y el espejo en la misma carpeta."""

    def setUp(self):
        super().setUp()
        try:
            self.raiz = tk.Tk()
        except tk.TclError as causa:
            self.skipTest(f"Esta máquina no tiene entorno gráfico: {causa}")
        self.raiz.withdraw()
        aplicar_tema(self.raiz)

        self.companero_id = alta_de_companero(self.conexion, "Ana Pérez")
        self.otro_id = alta_de_companero(self.conexion, "Luis Gómez")
        self.caso_id = alta_de_caso(
            self.conexion, "CASP2609", fecha_viaje="2026-09-16"
        ).id
        alta_de_persona(
            self.conexion, self.caso_id, mrn="055-1111-3853", nombre="Quien sea"
        )
        # El espejo se deja ya escrito y al dia: asi lo que la prueba mide es el
        # efecto de LA ACCION, y no que el archivo no existiera todavia.
        regenerar_espejo(self.conexion, self.carpeta_temporal, avisar=_sin_avisar)

    def tearDown(self):
        try:
            self.raiz.destroy()
        except tk.TclError:
            pass
        super().tearDown()

    # ---- las dos cuentas que se comparan ---------------------------------

    def _filas_en_la_base(self, tabla):
        """Cuantas filas tiene hoy la tabla que alimenta esa hoja del espejo.

        Se cuenta llamando al lector de la propia hoja (`espejo/hojas.py`) y no con
        un `SELECT COUNT(*) FROM {tabla}`: pegar el nombre de la tabla con una
        f-string lo dictamina INSEGURA `pruebas/auditoria_sql.py`, con razon —no
        puede seguir la variable hasta su valor—, y el nombre de la tabla no se
        puede pasar como parametro. El lector ya tiene su SQL escrito como cadena
        literal en su sitio.
        """
        return len(POR_TABLA[tabla].leer_filas(self.conexion))

    def _filas_en_el_espejo(self, hoja):
        """Las filas de datos de una hoja del `.xlsx` que hay ahora en disco."""
        libro = load_workbook(ruta_del_espejo(self.carpeta_temporal), read_only=True)
        try:
            return max(0, libro[hoja].max_row - 1)
        finally:
            libro.close()

    def _exigir_que_cuadren(self, tabla, hoja=None):
        hoja = hoja or tabla
        base = self._filas_en_la_base(tabla)
        espejo = self._filas_en_el_espejo(hoja)
        self.assertGreater(base, 0, f"la acción no escribió nada en {tabla}")
        self.assertEqual(base, espejo, f"{tabla} base={base} espejo={espejo}")

    def _pantalla_de_asignacion(self):
        from interfaz.asignacion import PantallaDeAsignacion

        pantalla = PantallaDeAsignacion(
            self.raiz,
            self.conexion,
            al_volver=lambda: None,
            hoy=HOY,
            carpeta_de_datos=self.carpeta_temporal,
        )
        pantalla._desplegable.current(0)
        pantalla.refrescar()
        self.raiz.update_idletasks()
        return pantalla

    # ---- asignaciones ----------------------------------------------------

    def test_asignar_un_caso_deja_el_espejo_al_dia(self):
        pantalla = self._pantalla_de_asignacion()
        pantalla._marcados[self.caso_id].set(True)

        with mock.patch("interfaz.asignacion.dialogos"):
            pantalla._asignar_los_marcados()

        self._exigir_que_cuadren("asignaciones")

    def test_retirar_un_caso_deja_el_espejo_al_dia(self):
        """Retirar no borra la fila: la desactiva, y esa columna es del espejo."""
        asignar_caso(self.conexion, self.caso_id, self.companero_id)
        regenerar_espejo(self.conexion, self.carpeta_temporal, avisar=_sin_avisar)
        pantalla = self._pantalla_de_asignacion()
        companero = pantalla._companero_elegido()
        caso = leer_caso_por_id(self.conexion, self.caso_id)

        with mock.patch("interfaz.asignacion.dialogos") as caja:
            caja.preguntar_si_o_no.return_value = True
            pantalla._retirar(companero, caso)

        self._exigir_que_cuadren("asignaciones")
        activas = self.conexion.execute(
            "SELECT COUNT(*) FROM asignaciones WHERE activa = 1"
        ).fetchone()[0]
        self.assertEqual(0, activas, "retirar tenía que desactivar la asignación")
        libro = load_workbook(ruta_del_espejo(self.carpeta_temporal), read_only=True)
        try:
            encabezados = [celda.value for celda in next(libro["asignaciones"].rows)]
            fila = [celda.value for celda in list(libro["asignaciones"].rows)[1]]
        finally:
            libro.close()
        self.assertEqual(0, fila[encabezados.index("activa")])

    # ---- contactos -------------------------------------------------------

    def _ventana_de_contacto(self):
        from interfaz.seguimiento import VentanaDeContacto

        ventana = VentanaDeContacto(
            self.raiz,
            self.conexion,
            leer_caso_por_id(self.conexion, self.caso_id),
            carpeta_de_datos=self.carpeta_temporal,
        )
        self.raiz.update_idletasks()
        return ventana

    def test_registrar_un_contacto_deja_el_espejo_al_dia(self):
        ventana = self._ventana_de_contacto()
        ventana._con_quien.insert(0, "El líder")

        with mock.patch("interfaz.seguimiento.dialogos"):
            ventana._guardar()

        self._exigir_que_cuadren("contactos")

    def test_anular_un_contacto_deja_el_espejo_al_dia(self):
        """Anular tampoco borra: escribe el motivo, y el motivo es del espejo."""
        from datos.contactos import contactos_del_caso, registrar_contacto
        from interfaz.seguimiento import PanelDeSeguimiento

        registrar_contacto(self.conexion, self.caso_id, "2026-09-10", medio="llamada")
        regenerar_espejo(self.conexion, self.carpeta_temporal, avisar=_sin_avisar)
        panel = PanelDeSeguimiento(
            self.raiz,
            self.conexion,
            leer_caso_por_id(self.conexion, self.caso_id),
            carpeta_de_datos=self.carpeta_temporal,
        )
        contacto = contactos_del_caso(self.conexion, self.caso_id)[0]

        with mock.patch("interfaz.seguimiento._pedir_texto", return_value="me equivoqué"):
            with mock.patch("interfaz.seguimiento.dialogos"):
                panel._anular(contacto)

        self._exigir_que_cuadren("contactos")
        libro = load_workbook(ruta_del_espejo(self.carpeta_temporal), read_only=True)
        try:
            encabezados = [celda.value for celda in next(libro["contactos"].rows)]
            fila = [celda.value for celda in list(libro["contactos"].rows)[1]]
        finally:
            libro.close()
        self.assertEqual("me equivoqué", fila[encabezados.index("motivo_anulacion")])

    # ---- el Excel que devuelve el companero ------------------------------

    def test_cargar_el_excel_devuelto_deja_el_espejo_al_dia(self):
        """QA no lo listo, y `personas` tambien es una hoja del espejo.

        La reconciliacion escribe las cuatro columnas de la propuesta sobre
        `personas`, y esas cuatro estan en la hoja `personas` del espejo desde la
        version 5 del esquema. Sin regenerar, el Excel se queda sin la propuesta.
        """
        from paquete.exportacion import exportar_paquete

        asignar_caso(self.conexion, self.caso_id, self.companero_id)
        paquete = exportar_paquete(
            self.conexion, self.companero_id, self.carpeta_temporal
        )
        ruta = paquete.ruta_del_excel
        libro = load_workbook(ruta)
        hoja = libro[NOMBRE_DE_LA_HOJA]
        for nombre in NOMBRES_DE_LOS_PASOS:
            hoja.cell(row=PRIMERA_FILA_DE_DATOS, column=indice_de(nombre), value="No")
        libro.save(str(ruta))
        libro.close()
        regenerar_espejo(self.conexion, self.carpeta_temporal, avisar=_sin_avisar)

        pantalla = self._pantalla_de_asignacion()
        with mock.patch("interfaz.asignacion.filedialog") as dialogo:
            dialogo.askopenfilename.return_value = str(ruta)
            with mock.patch("interfaz.asignacion.dialogos"):
                pantalla._cargar_devuelto()

        libro = load_workbook(ruta_del_espejo(self.carpeta_temporal), read_only=True)
        try:
            cabeceras = [celda.value for celda in next(libro["personas"].rows)]
            fila = [celda.value for celda in list(libro["personas"].rows)[1]]
        finally:
            libro.close()
        self.assertEqual("incompleta", fila[cabeceras.index("estado_propuesto")])
