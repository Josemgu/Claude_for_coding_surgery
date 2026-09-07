"""La pantalla de correccion se recorre entera con el teclado, sin perder el foco.

El objetivo del pase de la FASE 3 es medible y manda: **un formulario de tres
personas en menos de un minuto sin tocar el raton**. Eso solo se sostiene si la
vista va detras del foco.

Medido en la FASE 9 antes de este arreglo: **22 tabulaciones y la region central
no se movio ni un pixel**. A partir de la tercera persona las casillas quedaban
fuera de pantalla y se estaria tecleando a ciegas — que en esta pantalla significa
marcar una ordenanza que no se ha mirado.

**Por que la ventana se abre transparente y no oculta.** Una ventana retirada con
`withdraw()` no esta dibujada, y Tk devuelve 1 como alto de todo lo que hay
dentro: la prueba no podria saber donde esta ningun control. Medido en esta
maquina (Tk 9.0.4): con `-alpha 0.0` la ventana SI se dibuja y da geometrias
reales, y no se ve en la pantalla mientras corren las pruebas.
"""

import shutil
import tempfile
import unittest
from pathlib import Path

from datos.conexion import abrir_conexion
from datos.esquema import aplicar_esquema
from datos.repositorio import alta_de_caso, alta_de_persona

ALTO_DE_LA_VENTANA = 720
ANCHO_DE_LA_VENTANA = 1100
PERSONAS_DEL_FORMULARIO = 6


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
class ElFocoArrastraLaVista(unittest.TestCase):
    """Se abre la pantalla de verdad, con las seis personas que caben en la hoja."""

    def setUp(self):
        import tkinter as tk

        from interfaz.correccion import PantallaDeCorreccion
        from interfaz.tema import aplicar_tema

        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_correccion_"))
        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        aplicar_esquema(self.conexion)
        caso = alta_de_caso(self.conexion, numero_caso="SURB2609", fecha_viaje="2026-09-08")
        for fila in range(1, PERSONAS_DEL_FORMULARIO + 1):
            alta_de_persona(
                self.conexion,
                caso.id,
                mrn=f"055-1111-{3850 + fila:04d}",
                nombre=f"PERSONA {fila}",
                fila_formulario=fila,
            )

        self.raiz = tk.Tk()
        self.raiz.geometry(f"{ANCHO_DE_LA_VENTANA}x{ALTO_DE_LA_VENTANA}")
        self.raiz.attributes("-alpha", 0.0)
        self.raiz.rowconfigure(0, weight=1)
        self.raiz.columnconfigure(0, weight=1)
        aplicar_tema(self.raiz)
        self.pantalla = PantallaDeCorreccion(
            self.raiz,
            self.conexion,
            caso.id,
            al_volver=lambda: None,
            carpeta_de_datos=self.carpeta,
        )
        self.pantalla.grid(row=0, column=0, sticky="nsew")
        self.raiz.update()
        # Sin esto la prueba se vuelve mentirosa a partir de la SEGUNDA ventana
        # del proceso, y esta medido: la primera daba 0 controles fuera del hueco
        # y la segunda y la tercera daban 46. Tk solo genera `<FocusIn>` en la
        # ventana que tiene el foco del sistema, y las ventanas siguientes no lo
        # reciben. Sin `focus_force` lo que se estaria comprobando es cuantas
        # ventanas se abrieron antes.
        self.raiz.focus_force()
        self.raiz.update()

    def tearDown(self):
        self.conexion.close()
        self.raiz.destroy()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def _hueco(self):
        """Los dos bordes del hueco visible, en coordenadas de lo que se desplaza."""
        lienzo = self.pantalla._lienzo
        arriba = lienzo.canvasy(0)
        return arriba, arriba + lienzo.winfo_height()

    def _sitio_del_control(self, control):
        """Los dos bordes del control, en esas mismas coordenadas."""
        arriba = control.winfo_rooty() - self.pantalla._dentro.winfo_rooty()
        return arriba, arriba + control.winfo_height()

    def _esta_dentro_del_hueco(self, control):
        arriba_del_hueco, abajo_del_hueco = self._hueco()
        arriba, abajo = self._sitio_del_control(control)
        return arriba >= arriba_del_hueco - 1 and abajo <= abajo_del_hueco + 1

    def _esta_en_la_region_que_se_desplaza(self, control):
        """Si el control vive dentro de lo que rueda.

        La cabecera y el pie NO se desplazan a proposito —«Verificar caso» con su
        motivo tiene que leerse siempre—, asi que sus botones se ven siempre y
        medirlos contra el hueco no significa nada.
        """
        while control is not None:
            if control is self.pantalla._dentro:
                return True
            control = getattr(control, "master", None)
        return False

    def _recorrido_con_tabulador(self, tope=200):
        """La vuelta ENTERA de Tab, con lo que se veia EN EL MOMENTO de cada salto.

        La visibilidad se anota control a control y no al final: al terminar el
        recorrido la vista esta abajo del todo, y medir ahi diria que todo lo de
        arriba quedo fuera cuando en realidad se vio en su turno. Es el error que
        esta prueba cometio antes de medirlo.
        """
        inicio = control = self.pantalla._campos_del_caso["fecha_viaje"].entrada
        control.focus_set()
        self.raiz.update()
        recorrido = [(control, self._esta_dentro_del_hueco(control))]
        for _ in range(tope):
            control = control.tk_focusNext()
            if control is None or control is inicio:
                break
            control.focus_set()
            self.raiz.update()
            recorrido.append((control, self._esta_dentro_del_hueco(control)))
        return recorrido

    def test_la_pantalla_es_mas_larga_que_su_hueco(self):
        """Sin esto la prueba no probaria nada: con todo a la vista siempre pasa."""
        self.assertGreater(
            self.pantalla._dentro.winfo_height(), self.pantalla._lienzo.winfo_height()
        )

    def test_tabulando_por_toda_la_pantalla_el_foco_nunca_se_sale_de_la_vista(self):
        fuera = [
            str(control)
            for control, se_veia in self._recorrido_con_tabulador()
            if self._esta_en_la_region_que_se_desplaza(control) and not se_veia
        ]
        self.assertEqual(fuera, [], f"{len(fuera)} controles quedaron fuera del hueco")

    def test_el_recorrido_llega_de_verdad_hasta_abajo(self):
        """22 tabulaciones fue la medicion de la FASE 9: hay que pasar de ahi."""
        self._recorrido_con_tabulador()
        self.assertGreater(self.pantalla._lienzo.canvasy(0), 0)

    def test_tab_no_para_en_nada_que_no_se_teclee(self):
        """El arreglo de la vista NO puede alargar el recorrido, y casi lo hace.

        `tk::FocusOK` mete en el recorrido a cualquier widget con una atadura de
        foco. Atando `<FocusIn>` a todo lo que hay dentro —etiquetas, marcos, la
        tira del escaneo— el recorrido pasaba de 53 paradas a mas de 130. Es la
        promesa del minuto por formulario tirada a la basura por una atadura de
        mas, y por eso se comprueba aqui.
        """
        paradas = [
            str(control)
            for control, _ in self._recorrido_con_tabulador()
            if str(control.cget("takefocus")) in ("", "0")
        ]
        self.assertEqual(paradas, [], "Tab para en controles donde no se teclea nada")

    # Las dos paradas del panel de seguimiento, que entro con la FASE 6/7: el
    # desplegable del estado de la recomendacion y el boton «Registrar contacto…».
    #
    # ⚠️ **El recorrido paso de 54 paradas a 56, y esta escrito aparte para que se
    # vea.** No es un descuido colado en un numero: las dos son controles donde SE
    # HACE algo —elegir el estado que Miguel confirma, y anotar una llamada—, y
    # dejarlas fuera del recorrido las volveria inalcanzables sin raton, que es lo
    # contrario de la promesa de esta pantalla. Si el dueno decide que el minuto
    # por formulario no admite dos paradas mas, lo que se mueve es el panel a otro
    # sitio, no este numero.
    PARADAS_DEL_PANEL_DE_SEGUIMIENTO = 2

    # ⚠️ **La parada 57, y por que no es una regresion del minuto por formulario.**
    # El boton «Ninguna ordenanza marcada» entro para que las 36 casillas sin leer
    # de estas seis personas no cuesten 72 pulsaciones. Se gana una parada de Tab y
    # se ahorran hasta 71: cambiar una por otra es el objeto entero del cambio.
    #
    # Y es un cambio NETO de una sola parada, no de dos, porque los dos botones no
    # estan encendidos a la vez mientras las casillas sean el unico bloqueo:
    # mientras quede alguna sin leer manda este y «Todo correcto» esta apagado, y
    # al resolverlas se cambian los papeles.
    PARADA_DE_LAS_CASILLAS_SIN_LEER = 1

    # ⚠️ **La parada 58: «Deshacer "Todo correcto"», que entro el 2026-09-03.**
    # Medido en esta maquina antes y despues: el recorrido pasa de 57 a 58.
    #
    # Se paga y no se discute, porque lo que compra es la salida de un callejon:
    # hasta ese dia una verificacion firmada por error **no se podia deshacer desde
    # el programa** —`datos/procedencia.py` tenia `marcar_campo_verificado` y
    # ninguna inversa—, y el unico arreglo era abrir la base con otra herramienta.
    # Una parada de Tab contra eso no es una discusion.
    #
    # A diferencia de los otros dos botones del pie, este esta SIEMPRE encendido y
    # por eso suma siempre: no tiene bloqueo que lo apague, porque la situacion de
    # la que saca puede darse en cualquier momento. Y no lleva atajo de teclado a
    # proposito: una tecla que retira 36 firmas no puede estar a un dedo de la que
    # las pone.
    PARADA_DE_DESHACER = 1

    # ⚠️ **Las paradas 59 y 60: «ver ▾» y «×» de la franja de avisos, 2026-09-04.**
    # Medido en esta maquina antes y despues del cambio: el recorrido pasa de 58 a
    # 60, y las dos paradas nuevas estan las dos dentro de `self.pantalla.franja`.
    #
    # **Solo existen cuando hay algo que avisar.** Este caso de prueba no guarda
    # ruta de PDF, asi que sale el aviso «escaneo que no se puede mostrar» y la
    # franja se dibuja; un caso sin ningun aviso vuelve a las 58, y eso lo mide
    # `test_sin_avisos_la_franja_no_cuesta_ninguna_parada`.
    #
    # Se pagan, y el motivo es el mismo que el del panel de seguimiento: son los
    # dos controles donde SE HACE algo con el aviso —cerrarlo y leer el detalle—, y
    # dejarlos fuera del recorrido los volveria inalcanzables sin raton. Lo que
    # sustituyeron —un parrafo ambar de hasta cinco lineas por aviso— no costaba
    # ninguna parada porque no se podia hacer nada con el: ni cerrarlo, ni
    # desplegarlo. Y costaba 310 px de pantalla, medidos.
    #
    # Cerrando la franja el recorrido baja a 59 y no a 58: la campana con la cuenta
    # ocupa una parada, porque un aviso cerrado que no se puede volver a abrir sin
    # raton es un aviso perdido. Lo mide
    # `test_cerrar_la_franja_cambia_sus_dos_paradas_por_una`.
    PARADAS_DE_LA_FRANJA_DE_AVISOS = 2

    def test_el_recorrido_tiene_las_paradas_que_hacen_falta_y_ni_una_mas(self):
        """Medido en esta maquina: 3 campos del caso, 48 de las personas, 4 botones.

        `numero_caso` no cuenta —es de solo lectura y sale del recorrido a
        proposito— y «Todo correcto» tampoco mientras esta apagado: con las 36
        casillas sin resolver, lo esta.

        ⚠️ Y eso ultimo es una **medicion, no lo que dice el comentario del codigo**:
        `interfaz/correccion.py` afirma que el boton de verificar «sigue tomando el
        foco con Tab a proposito aunque este apagado». Si fuera cierto, aqui
        saldria una parada mas de las que salen. No sale, asi que un `ttk.Button` en
        `disabled` **no** para el Tab en esta maquina (Tk 9.0.4, Windows 11). El
        comentario describe una intencion que no se cumple; el numero manda
        (`CLAUDE.md` §8).

        El total medido hoy es **60**, y era 58 hasta el 2026-09-04: la diferencia
        son «ver ▾» y «×» de la franja de avisos, con su motivo escrito en
        `PARADAS_DE_LA_FRANJA_DE_AVISOS`. Antes fue 57 → 58 con el boton de
        deshacer, y 54 → 56 con el panel de seguimiento.
        """
        esperadas = (
            3
            + PERSONAS_DEL_FORMULARIO * (2 + 6)
            + 3
            + self.PARADA_DE_LAS_CASILLAS_SIN_LEER
            + self.PARADA_DE_DESHACER
            + self.PARADAS_DEL_PANEL_DE_SEGUIMIENTO
            + self.PARADAS_DE_LA_FRANJA_DE_AVISOS
        )
        self.assertEqual(esperadas, 60, "el desglose y el total dejaron de cuadrar")
        self.assertEqual(len(self._recorrido_con_tabulador()), esperadas)

    def test_cerrar_la_franja_cambia_sus_dos_paradas_por_una(self):
        """Cerrada, el recorrido baja de 60 a 59: se van «ver» y «×», llega la campana.

        Medido, y es el numero que hay que saber: cerrar la franja **no la sale
        gratis del recorrido**, porque la cuenta tiene que seguir alcanzable sin
        raton —si no, un aviso cerrado seria un aviso perdido, que es justo lo que
        la campana existe para impedir—. Lo que se ahorra es una parada de dos.

        El caso de esta prueba SI trae aviso —no guarda ruta de PDF—; un caso sin
        ninguno no dibuja ni franja ni campana y se queda en las 58 de antes del
        2026-09-04.
        """
        abierta = len(self._recorrido_con_tabulador())
        self.pantalla.franja.cerrar()
        self.raiz.update()
        cerrada = len(self._recorrido_con_tabulador())
        self.assertEqual(abierta, 60)
        self.assertEqual(cerrada, 59)
        self.assertTrue(self.pantalla._campana.winfo_ismapped())

    def test_cerrar_la_franja_dura_mas_de_una_tecla(self):
        """«Que se pueda cerrar el texto», y que siga cerrado al seguir tecleando.

        `_pintar_avisos` corre en CADA recuento —cada tecla, cada cambio de foco—,
        y hasta el 2026-09-04 volver a poner los mismos avisos borraba el cierre:
        medido, la franja cerrada reaparecia con el primer Tab.
        """
        self.pantalla.franja.cerrar()
        self.raiz.update()
        self.pantalla._recontar()
        self.raiz.update()
        self.assertTrue(self.pantalla.franja.esta_cerrada())

    def test_un_boton_apagado_no_para_el_tab_y_por_eso_su_motivo_va_escrito(self):
        """La consecuencia de la medicion de arriba, aislada para que no se pierda.

        Si el boton apagado no se alcanza con Tab, quien trabaja sin raton **no
        puede leer su motivo pulsandolo**: el motivo tiene que estar escrito en el
        pie, siempre visible. Que lo este es lo que salva la promesa de la
        pantalla, y esta prueba es lo que avisaria si alguien lo quitara.
        """
        self.assertIn("disabled", self.pantalla._boton_verificar.state())
        alcanzados = [str(control) for control, _ in self._recorrido_con_tabulador()]
        self.assertNotIn(str(self.pantalla._boton_verificar), alcanzados)
        self.assertNotEqual(self.pantalla._motivo_del_bloqueo.cget("text"), "")

    def test_saltando_de_persona_en_persona_el_foco_tampoco_se_sale(self):
        """Intro salta al primer campo de la persona siguiente. Ese es el camino."""
        for bloque in self.pantalla._bloques:
            control = bloque.primer_control()
            control.focus_set()
            self.raiz.update()
            self.assertTrue(
                self._esta_dentro_del_hueco(control),
                f"la persona {self.pantalla._bloques.index(bloque) + 1} quedó "
                "fuera del hueco al saltar a ella",
            )

    def test_lo_que_ya_se_ve_no_hace_saltar_la_vista(self):
        """Una vista que salta en cada Tab marea y pierde el campo de referencia."""
        primero = self.pantalla._campos_del_caso["fecha_viaje"].entrada
        primero.focus_set()
        self.raiz.update()
        antes = self.pantalla._lienzo.canvasy(0)
        self.pantalla._campos_del_caso["unidad_numero"].entrada.focus_set()
        self.raiz.update()
        self.assertEqual(self.pantalla._lienzo.canvasy(0), antes)


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class ElTemporizadorDeLasHojasSeCancelaAlSalir(unittest.TestCase):
    """Salir del caso no puede dejar vivo el temporizador que cuenta las hojas.

    Es el punto 5 del pase, y el sintoma que se ve hoy es «invalid command name»
    por consola durante la suite: la pantalla se destruye, el temporizador dispara
    despues sobre lo que ya no existe y Tk se queja. No rompe nada visible, pero
    es un temporizador vivo por cada caso que se abre y se cierra, y una sesion de
    Miguel abre decenas.

    Se comprueba contra `after info`, que es la lista real de temporizadores
    pendientes del interprete, y no contra un atributo de la pantalla: un atributo
    puesto a None no demuestra que Tk lo haya cancelado.
    """

    def setUp(self):
        import tkinter as tk

        from interfaz.correccion import PantallaDeCorreccion
        from interfaz.tema import aplicar_tema

        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_temporizador_"))
        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        aplicar_esquema(self.conexion)
        caso = alta_de_caso(self.conexion, numero_caso="TEMP2609", fecha_viaje="2026-09-08")
        alta_de_persona(self.conexion, caso.id, nombre="PERSONA 1", fila_formulario=1)

        self.raiz = tk.Tk()
        self.raiz.attributes("-alpha", 0.0)
        aplicar_tema(self.raiz)
        self.pantalla = PantallaDeCorreccion(
            self.raiz,
            self.conexion,
            caso.id,
            al_volver=lambda: None,
            carpeta_de_datos=self.carpeta,
        )
        self.pantalla.grid(row=0, column=0, sticky="nsew")

    def tearDown(self):
        self.conexion.close()
        self.raiz.destroy()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def _temporizadores_pendientes(self):
        """Los identificadores que el interprete tiene programados ahora mismo."""
        return set(self.raiz.tk.call("after", "info"))

    def test_al_construirse_deja_un_temporizador_programado(self):
        """Sin esto la prueba de abajo pasaria aunque no hubiera nada que cancelar."""
        self.assertTrue(
            self._temporizadores_pendientes(),
            "la pantalla no programó ningún temporizador, así que la prueba de que "
            "se cancela no estaría midiendo nada",
        )

    def test_soltar_atajos_cancela_el_conteo_de_hojas(self):
        """Es lo que hace la ventana al cambiar de pantalla, antes de destruirla."""
        antes = self._temporizadores_pendientes()
        self.pantalla.soltar_atajos()
        despues = self._temporizadores_pendientes()
        self.assertTrue(
            antes - despues,
            f"soltar_atajos() no canceló ningún temporizador: seguían {despues}",
        )

    def test_al_salir_no_queda_atada_la_rueda_en_toda_la_ventana(self):
        """`bind_all` sin soltar es lo que hace que dos pantallas se pisen.

        La rueda y el trackpad se atan con `bind_all` —hace falta: encima del
        lienzo hay decenas de controles que se comerían el evento—, y una atadura
        de toda la ventana que nadie suelta sobrevive a la pantalla que la puso.
        La siguiente pantalla la reemplaza al atar la suya, y al soltarla deja la
        ventana entera sin nadie que oiga la rueda.
        """
        self.raiz.update()
        self.pantalla.soltar_atajos()
        for evento in ("<MouseWheel>", "<TouchpadScroll>"):
            self.assertEqual(
                "",
                self.raiz.bind_all(evento),
                f"{evento} siguió atado a toda la ventana después de salir del caso",
            )


if __name__ == "__main__":
    unittest.main()
