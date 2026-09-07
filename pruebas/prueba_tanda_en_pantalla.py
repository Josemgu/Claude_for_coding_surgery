"""La tanda entera con su ventana de verdad, y el caso sin numero en la correccion.

Esto es lo mas cerca que se puede estar de lo que hace Miguel sin poner un dedo en
el raton: se abre una ventana de Tk de verdad, se lanza la tanda con su hilo, se
deja que el bucle de eventos la mueva hasta el final, y se mira que quedo en la
base y que dice el resumen.

**Lo que se sustituye, y por que solo eso.** Se sustituye `leer_un_pdf` por una
funcion que devuelve formularios inventados. Lo que se prueba aqui es el
coordinador —el hilo, la cola, el guardado en el hilo de la ventana, el cierre—, y
meter el OCR de verdad convertiria una prueba de 2 segundos en una de varios
minutos sin comprobar ni una cosa mas de lo que se quiere comprobar. La lectura de
verdad ya la prueba `pruebas/auditoria_extraccion_real.py` sobre PDF reales.

⚠️ **Lo que estas pruebas NO comprueban:** que la ventana se pueda mover y pulsar
mientras corre una tanda de horas. Eso se ve con los ojos y con el raton, y queda
para QA con su nombre puesto. Lo que si se comprueba es la causa de que antes no
se pudiera: que la lectura ya no ocurre en el hilo de la ventana.

Ninguna prueba toca la base real ni ningun PDF real.
"""

import shutil
import tempfile
import unittest
from pathlib import Path

from datos.conexion import abrir_conexion
from datos.esquema import aplicar_esquema
from datos.ilegibles import documentos_ilegibles
from importacion.tanda import LecturaDeUnPdf
from pruebas.prueba_importacion import _formulario


def _hay_ventanas():
    """Si no se puede crear un Tk, estas pruebas se omiten en vez de fallar."""
    try:
        import tkinter

        raiz = tkinter.Tk()
    except Exception:  # pragma: no cover - depende de la maquina, no del codigo
        return False
    raiz.destroy()
    return True


# Cuanto se espera como mucho a que la tanda termine, en segundos. Es un tope de
# seguridad, no una espera: con lecturas de mentira la tanda tarda decimas. Sin
# tope, una prueba que no termina se queda colgada para siempre.
SEGUNDOS_MAXIMOS_DE_ESPERA = 20

# Lo que se duerme entre vueltas del bucle de eventos. Hace falta que pase tiempo
# de RELOJ y no solo vueltas: el coordinador se vuelve a programar con `after(120)`,
# y girar mil veces sin dejar pasar el reloj no hace avanzar ni una sola vuelta.
SEGUNDOS_ENTRE_VUELTAS = 0.01


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class LaTandaEnteraConSuVentana(unittest.TestCase):
    """Criterios 3, 4 y 5 del pase, con la ventana de verdad por medio."""

    def setUp(self):
        import tkinter as tk

        from interfaz.tema import aplicar_tema

        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_tanda_ventana_"))
        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        aplicar_esquema(self.conexion)
        self.raiz = tk.Tk()
        # Transparente y no retirada, por lo mismo que en `prueba_correccion.py`:
        # una ventana retirada no esta dibujada y Tk no da geometrias reales.
        self.raiz.attributes("-alpha", 0.0)
        aplicar_tema(self.raiz)
        self.resumen = None

    def tearDown(self):
        self.conexion.close()
        self.raiz.destroy()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def _correr(self, lecturas, cancelar_tras=None):
        """Lanza la tanda con lecturas ya preparadas y espera a que termine.

        `cancelar_tras` pide cancelar en cuanto el hilo lector ha leido esos
        documentos. Se pide **desde dentro de la lectura de mentira** y no desde el
        bucle de la prueba, y no es comodidad: pedirlo desde fuera depende de que
        el reloj caiga donde uno espera, y una prueba que a veces cancela y a veces
        no es peor que ninguna. Desde dentro, el instante es exacto.
        """
        import interfaz.importacion as modulo
        from interfaz.importacion import TandaEnMarcha

        pendientes = list(lecturas)
        leidos = []
        original = modulo.leer_un_pdf
        tanda = None

        def leer_de_mentira(ruta, motor):
            leidos.append(ruta)
            if cancelar_tras is not None and len(leidos) >= cancelar_tras:
                tanda.cancelar()
            return pendientes.pop(0)

        modulo.leer_un_pdf = leer_de_mentira
        try:
            tanda = TandaEnMarcha(
                self.raiz,
                self.conexion,
                [Path(f"documento_{numero}.pdf") for numero in range(len(lecturas))],
                dar_motor=lambda: "un motor de mentira",
                carpeta_de_datos=self.carpeta,
                al_terminar=self._anotar,
            )
            tanda.arrancar()
            self._esperar()
            self.assertIsNotNone(self.resumen, "la tanda no terminó")
            self.leidos = leidos
            return self.resumen
        finally:
            modulo.leer_un_pdf = original

    def _esperar(self):
        """Mueve el bucle de eventos hasta que la tanda avise de que termino."""
        import time

        limite = time.monotonic() + SEGUNDOS_MAXIMOS_DE_ESPERA
        while self.resumen is None and time.monotonic() < limite:
            self.raiz.update()
            time.sleep(SEGUNDOS_ENTRE_VUELTAS)

    def _anotar(self, resumen):
        self.resumen = resumen

    def _cuantos_casos(self):
        return self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0]

    # ---- criterio 5: el resumen ------------------------------------------

    def test_una_tanda_de_tres_documentos_los_guarda_los_tres(self):
        resumen = self._correr([
            LecturaDeUnPdf(Path("uno.pdf"), [_formulario(numero="CASP2609")], None),
            LecturaDeUnPdf(Path("dos.pdf"), [_formulario(numero="CASD2609")], None),
            LecturaDeUnPdf(Path("tres.pdf"), [_formulario(numero="PARB2609")], None),
        ])
        self.assertEqual(resumen.documentos, 3)
        self.assertEqual(resumen.casos, 3)
        self.assertEqual(self._cuantos_casos(), 3)

    def test_el_resumen_dice_cuantos_entraron_y_cuantos_a_medias(self):
        from extraccion.campos import campo_vacio

        sin_numero = _formulario()._replace(numero_caso=campo_vacio())
        resumen = self._correr([
            LecturaDeUnPdf(Path("uno.pdf"), [_formulario(numero="CASD2609")], None),
            LecturaDeUnPdf(Path("dos.pdf"), [sin_numero], None),
        ])
        self.assertEqual(resumen.casos, 2)
        self.assertEqual(resumen.pendientes, 1)
        self.assertIn("Pendientes de identificar", resumen.texto())

    # ---- criterio 4: uno roto no tumba el resto --------------------------

    def test_un_documento_roto_en_medio_no_impide_los_otros_dos(self):
        resumen = self._correr([
            LecturaDeUnPdf(Path("uno.pdf"), [_formulario(numero="CASP2609")], None),
            LecturaDeUnPdf(Path("roto.pdf"), [], "PdfReadError: fin inesperado"),
            LecturaDeUnPdf(Path("tres.pdf"), [_formulario(numero="CASD2609")], None),
        ])
        self.assertEqual(resumen.documentos, 3)
        self.assertEqual(resumen.casos, 2)
        self.assertEqual(len(resumen.fallidos), 1)
        self.assertEqual(self._cuantos_casos(), 2)

    def test_el_documento_roto_deja_su_renglon_para_despues(self):
        self._correr([LecturaDeUnPdf(Path("roto.pdf"), [], "PdfReadError: x")])
        self.assertEqual(len(documentos_ilegibles(self.conexion)), 1)

    # ---- criterio 3: cancelar --------------------------------------------

    def test_al_cancelar_lo_ya_procesado_se_queda_guardado_y_lo_demas_no_entra(self):
        """Criterio 3: cancelar para de verdad, y no deshace nada de lo hecho.

        Se lanzan DIEZ documentos y se cancela en cuanto el lector termina el
        primero. La bandera se mira ENTRE documento y documento y nunca a mitad de
        uno —parar a mitad de un PDF dejaria unas paginas suyas dentro y otras
        fuera, sin forma de saber cuales—, asi que el que estaba en marcha se
        termina y los ocho de detras no se leen siquiera.
        """
        letras = "ABCDEFGHIJ"
        resumen = self._correr(
            [
                LecturaDeUnPdf(
                    Path(f"{letra}.pdf"), [_formulario(numero=f"CAS{letra}2609")], None
                )
                for letra in letras
            ],
            cancelar_tras=1,
        )
        self.assertTrue(resumen.cancelada)
        # Se paro de verdad: no se leyeron los diez.
        self.assertEqual(len(self.leidos), 1)
        self.assertLess(resumen.documentos, len(letras))
        # Y lo que ya habia entrado sigue en la base.
        self.assertEqual(self._cuantos_casos(), resumen.casos)
        self.assertGreaterEqual(self._cuantos_casos(), 1)
        self.assertIn("CANCELADA", resumen.texto())

    # ---- el espejo se cierra una sola vez --------------------------------

    def test_al_terminar_la_tanda_el_excel_espejo_existe_y_esta_al_dia(self):
        """Se regenera UNA vez al final, no una por documento, pero se regenera."""
        from espejo.rutas import ruta_del_espejo

        self._correr([
            LecturaDeUnPdf(Path("uno.pdf"), [_formulario(numero="CASP2609")], None),
        ])
        self.assertTrue(ruta_del_espejo(self.carpeta).is_file())

    # ---- lo que arregla el «se para» -------------------------------------

    def test_la_lectura_no_ocurre_en_el_hilo_de_la_ventana(self):
        """La causa exacta del cuelgue: leer dentro del bucle de eventos de Tk.

        Se comprueba anotando en que hilo se llamo a `leer_un_pdf` y comparandolo
        con el hilo principal. Es lo unico que se puede medir sin un raton, y es
        justo la causa: mientras una funcion no devuelve, Tk no repinta.
        """
        import threading

        import interfaz.importacion as modulo
        from interfaz.importacion import TandaEnMarcha

        hilos = []
        original = modulo.leer_un_pdf

        def espiar(ruta, motor):
            hilos.append(threading.current_thread())
            return LecturaDeUnPdf(ruta, [_formulario()], None)

        modulo.leer_un_pdf = espiar
        try:
            tanda = TandaEnMarcha(
                self.raiz, self.conexion, [Path("uno.pdf")],
                dar_motor=lambda: None, carpeta_de_datos=self.carpeta,
                al_terminar=self._anotar,
            )
            tanda.arrancar()
            self._esperar()
        finally:
            modulo.leer_un_pdf = original
        self.assertEqual(len(hilos), 1)
        self.assertIsNot(hilos[0], threading.main_thread())


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class LaCorreccionDeUnCasoSinNumero(unittest.TestCase):
    """Criterio 1, segunda mitad, en la pantalla: «se puede completar a mano»."""

    def setUp(self):
        import tkinter as tk

        from datos.repositorio import alta_de_caso, alta_de_persona
        from interfaz.correccion import PantallaDeCorreccion
        from interfaz.tema import aplicar_tema

        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_sin_numero_"))
        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        aplicar_esquema(self.conexion)
        alta = alta_de_caso(self.conexion, numero_caso=None, fecha_viaje="2026-09-08")
        alta_de_persona(
            self.conexion, alta.id, mrn="055-1111-3853", nombre="ANONIMO, M", fila_formulario=1
        )
        self.caso_id = alta.id
        self.raiz = tk.Tk()
        self.raiz.attributes("-alpha", 0.0)
        aplicar_tema(self.raiz)
        self.pantalla = PantallaDeCorreccion(
            self.raiz, self.conexion, self.caso_id, al_volver=lambda: None,
            carpeta_de_datos=self.carpeta,
        )
        self.pantalla.pack(fill="both", expand=True)
        self.raiz.update()

    def tearDown(self):
        self.conexion.close()
        self.raiz.destroy()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def _campo(self):
        return self.pantalla._campos_del_caso["numero_caso"]

    def test_la_pantalla_se_abre_sin_reventar_por_el_numero_vacio(self):
        self.assertIsNotNone(self.pantalla)

    def test_el_campo_del_numero_se_puede_escribir(self):
        self.assertNotIn("readonly", self._campo().entrada.state())

    def test_se_avisa_arriba_de_que_falta_el_numero(self):
        avisos = self.pantalla._textos_de_aviso()
        self.assertTrue(any("SIN número de caso" in aviso for aviso in avisos))

    def test_no_se_puede_verificar_mientras_falte(self):
        motivo = self.pantalla._motivo_por_el_que_no_se_puede_verificar()
        self.assertIsNotNone(motivo)
        self.assertIn("número de caso", motivo)

    def test_al_teclearlo_y_guardar_el_caso_queda_identificado(self):
        from datos.repositorio import leer_caso_por_numero

        self._campo().entrada.insert(0, "CASP2609")
        self.assertTrue(self.pantalla.guardar())
        self.assertEqual(leer_caso_por_numero(self.conexion, "CASP2609")["id"], self.caso_id)

    def test_guardar_sin_teclearlo_no_tumba_nada_y_no_inventa_numero(self):
        """Lo demas se guarda igual: negarse volveria a perder trabajo hecho."""
        self.pantalla._campos_del_caso["unidad_nombre"].entrada.insert(0, "Paramaribo Branch")
        self.assertTrue(self.pantalla.guardar())
        caso = self.conexion.execute(
            "SELECT numero_caso, unidad_nombre FROM casos WHERE id = ?", (self.caso_id,)
        ).fetchone()
        self.assertIsNone(caso["numero_caso"])
        self.assertEqual(caso["unidad_nombre"], "Paramaribo Branch")


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class LaVentanaDeLoQueNoEntro(unittest.TestCase):
    """La lista que se queda: se abre con renglones y sin ellos.

    Vacia es el caso que mas se da al principio y el que mas facil se rompe: una
    ventana que revienta cuando no hay nada que ensenar deja a Miguel pensando que
    el programa esta roto justo el dia que todo fue bien.
    """

    RUTA_ROTA = "C:/Escaneos/roto.pdf"
    RUTA_EN_BLANCO = "C:/Escaneos/septiembre/blanco.pdf"

    def setUp(self):
        import tkinter as tk

        from interfaz.tema import aplicar_tema

        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_lista_"))
        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        aplicar_esquema(self.conexion)
        self.raiz = tk.Tk()
        self.raiz.attributes("-alpha", 0.0)
        aplicar_tema(self.raiz)

    def tearDown(self):
        self.conexion.close()
        self.raiz.destroy()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def _abrir(self):
        from interfaz.ilegibles import VentanaDeIlegibles

        ventana = VentanaDeIlegibles(self.raiz, self.conexion)
        self.raiz.update()
        # No se destruye aparte: destruir la raiz en tearDown se lleva a sus hijas,
        # y hacerlo despues es pedirle a Tk que destruya algo que ya no existe.
        return ventana

    def _anotar_dos(self):
        from datos.ilegibles import (
            NO_SE_PUDO_ABRIR,
            SIN_TEXTO,
            anotar_documento_ilegible,
        )

        anotar_documento_ilegible(
            self.conexion, self.RUTA_ROTA, NO_SE_PUDO_ABRIR, detalle="PdfReadError: x"
        )
        anotar_documento_ilegible(
            self.conexion, self.RUTA_EN_BLANCO, SIN_TEXTO, pagina_pdf=2, lineas_leidas=0
        )

    def test_se_abre_sin_ningun_renglon(self):
        ventana = self._abrir()
        self.assertEqual(ventana.filas, [])
        self.assertIn("entró entero", ventana._desglose())

    def test_se_abre_con_los_renglones_de_una_tanda(self):
        self._anotar_dos()
        self.assertEqual(len(self._abrir().filas), 2)

    def test_el_desglose_separa_los_dos_motivos(self):
        """Es lo que contesta «¿el problema son mis archivos o mis escaneos?»."""
        self._anotar_dos()
        desglose = self._abrir()._desglose()
        self.assertIn("no se pudo abrir", desglose)
        self.assertIn("sin texto", desglose)

    def test_el_texto_para_copiar_lleva_la_ruta_entera_y_el_motivo(self):
        """Pegado en un correo, un nombre suelto no sirve para encontrar nada."""
        from interfaz.ilegibles import texto_de_los_ilegibles

        self._anotar_dos()
        texto = texto_de_los_ilegibles(self._abrir().filas)
        self.assertIn("septiembre", texto)
        self.assertIn("página 2", texto)
        self.assertIn("NI UNA línea", texto)


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class LaVentanaDeProgresoSePintaEnteraDesdeElPrincipio(unittest.TestCase):
    """Que la ventana se vea acabada ANTES de la primera lectura, no despues.

    El criterio no es de adorno y sale de lo que el dueno reporto con sus palabras:
    «ese programa se para al leer un pdf». Los segundos mas largos de una tanda son
    los primeros —cargar el motor de OCR tarda, y hasta que no termina no llega
    ninguna lectura, asi que `poner` todavia no se ha llamado ni una vez—. Si en
    esos segundos la ventana aparece con el contenido en una franja y el resto en
    gris sin pintar, lo que se ve es exactamente un cuelgue.

        Dado que se abre la ventana de progreso de una tanda,
        Cuando todavia no ha llegado ninguna lectura,
        Entonces el marco de contenido ocupa el ancho entero de la ventana.

    Se mide con `winfo_width`, que es el ancho que Tk ha REPARTIDO de verdad, y no
    con `winfo_reqwidth`, que es el que el marco pediria. La diferencia entre los
    dos es justo el defecto: el marco pide ~140 px y la ventana mide 560, y sin
    peso en la fila y la columna del `Toplevel` los 420 restantes no son de nadie.
    """

    ANCHO_MINIMO_DE_LA_VENTANA = 560

    def setUp(self):
        import tkinter as tk

        from interfaz.tema import aplicar_tema

        self.raiz = tk.Tk()
        self.raiz.attributes("-alpha", 0.0)
        aplicar_tema(self.raiz)

    def tearDown(self):
        self.raiz.destroy()

    def _abrir_sin_ninguna_lectura(self, total_de_documentos=15):
        """La ventana recien nacida, sin una sola llamada a `poner`."""
        from interfaz.importacion import VentanaDeProgreso

        ventana = VentanaDeProgreso(self.raiz, total_de_documentos, lambda: None)
        # `update` y no `update_idletasks`: hace falta que Tk atienda tambien los
        # eventos de configuracion de la ventana, que son los que fijan su tamano
        # real. Con solo las tareas ociosas el ancho todavia puede ser 1.
        self.raiz.update()
        return ventana

    def _marco_de_contenido(self, ventana):
        """El unico hijo de la ventana: el marco donde vive todo lo que se ve."""
        hijos = ventana.winfo_children()
        self.assertEqual(
            len(hijos), 1, "la ventana de progreso deberia tener un solo marco dentro"
        )
        return hijos[0]

    def test_el_marco_ocupa_el_ancho_entero_antes_de_la_primera_lectura(self):
        ventana = self._abrir_sin_ninguna_lectura()
        marco = self._marco_de_contenido(ventana)
        self.assertEqual(
            marco.winfo_width(),
            ventana.winfo_width(),
            "el marco no llega al borde: queda una franja gris sin pintar",
        )

    def test_el_marco_ocupa_el_alto_entero_antes_de_la_primera_lectura(self):
        ventana = self._abrir_sin_ninguna_lectura()
        marco = self._marco_de_contenido(ventana)
        self.assertEqual(
            marco.winfo_height(),
            ventana.winfo_height(),
            "el marco no llega al borde de abajo",
        )

    def test_la_ventana_respeta_su_ancho_minimo(self):
        """El defecto se ve porque la ventana es ancha y el contenido no. Si algun
        dia se quitara el ancho minimo, estas dos pruebas pasarian sin arreglar
        nada: el marco llenaria una ventana estrecha. Esta lo impide."""
        ventana = self._abrir_sin_ninguna_lectura()
        self.assertGreaterEqual(ventana.winfo_width(), self.ANCHO_MINIMO_DE_LA_VENTANA)

    def test_sigue_entero_despues_de_la_primera_lectura(self):
        """Antes del arreglo esto ya pasaba: el defecto solo se veia al principio."""
        from importacion.tanda import ResumenDeLaTanda

        ventana = self._abrir_sin_ninguna_lectura()
        resumen = ResumenDeLaTanda(15)
        resumen.documentos = 1
        ventana.poner(resumen, "copia-a-1.pdf")
        self.raiz.update()
        marco = self._marco_de_contenido(ventana)
        self.assertEqual(marco.winfo_width(), ventana.winfo_width())


if __name__ == "__main__":
    unittest.main()
