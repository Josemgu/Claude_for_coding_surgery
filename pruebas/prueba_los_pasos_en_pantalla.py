"""Los seis pasos del sistema del lider, vistos en la pantalla de correccion.

**El hueco que estas pruebas cierran, en las palabras del pase.** Los seis pasos y
«¿Llamó al líder?» vuelven del Excel del companero, se guardan en `personas` como
parte de la propuesta que firma `propuesto_por` — «y Miguel no los ve». Medido
antes de escribir nada, en esta maquina:

    $ grep -c "paso_\\|PASOS\\|llamo_al_lider" interfaz/correccion.py   0
    $ grep -c "paso\\|lider"                    interfaz/persona.py     0

Cero en las dos. El dato llegaba entero a la base y moria alli.

**Por que los pasos NO son casillas de ordenanza, y por que eso se prueba.**
`datos/pasos.py` lo dice en su primera linea y el pase lo repite: las seis
ordenanzas dicen a QUE va la persona al templo, y los seis pasos dicen si esta en
condiciones de ir. Son dos preguntas distintas sobre la misma persona. Si en
pantalla se pintaran iguales, Miguel leeria una por la otra — y la que se leeria
mal es justo la que dice si esa persona puede entrar. Por eso hay una prueba que
exige que el rotulo, la palabra y el dibujo de los tres estados sean distintos de
los de la casilla de ordenanza, y no solo que existan.

**Sin contestar no es «No».** Las tres respuestas se comprueban por separado
porque la tercera es la que hace dano si se pierde: una pregunta que nadie miro
convertida en un «No» es una persona reprobada por un blanco, y convertida en un
«Sí» es una persona que va al templo sin que nadie lo mirara.
"""

import shutil
import tempfile
import unittest
from pathlib import Path

from datos.companeros import alta_de_companero
from datos.conexion import abrir_conexion
from datos.esquema import aplicar_esquema
from datos.pasos import (
    COLUMNA_DE_LA_LLAMADA,
    NOMBRES_DE_LOS_PASOS,
    PASOS,
    ROTULO_DE_LA_LLAMADA,
)
from datos.propuestas import guardar_propuesta, propuesta_vigente
from datos.repositorio import alta_de_caso, alta_de_persona

CARPETA_DEL_CODIGO = Path(__file__).resolve().parent.parent


def _hay_ventanas():
    """Si no se puede crear un Tk, estas pruebas se omiten en vez de fallar."""
    try:
        import tkinter

        raiz = tkinter.Tk()
    except Exception:  # pragma: no cover - depende de la maquina, no del codigo
        return False
    raiz.destroy()
    return True


HAY_VENTANAS = _hay_ventanas()


def textos_de(widget):
    """Todos los textos escritos en un widget y en todo lo que cuelga de el.

    Se recorre el arbol de verdad y se lee `cget("text")` de cada pieza, en vez de
    preguntarle al objeto que cree que muestra: lo que se quiere comprobar es lo
    que Miguel leeria en la pantalla, no lo que el codigo se propuso escribir.
    """
    encontrados = []
    try:
        encontrados.append(str(widget.cget("text")))
    except Exception:
        pass
    for hijo in widget.winfo_children():
        encontrados.extend(textos_de(hijo))
    return encontrados


def dibujo_de(lienzo):
    """Lo que hay pintado en un Canvas, como lista comparable.

    Cada figura queda como `(tipo, coordenadas)`. Es una medicion del dibujo y no
    del proposito de quien lo dibujo: dos estados que devuelvan esto mismo se ven
    iguales en pantalla, que es el fallo que se midio con la casilla nativa de Tk.
    """
    return sorted(
        (lienzo.type(figura), tuple(lienzo.coords(figura)))
        for figura in lienzo.find_all()
    )


class LosRotulosSalenDeUnSoloSitio(unittest.TestCase):
    """CA-6: los rotulos de los pasos no se copian a `interfaz/`.

    No hace falta ventana: se lee el codigo. Si alguien pega «1. Preparación» en
    la pantalla, el dia que cambie el rotulo del Excel la pantalla dira otra cosa
    que la hoja, y las dos pareceran bien por separado.
    """

    def test_ningun_rotulo_de_paso_esta_escrito_a_mano_en_interfaz(self):
        copiados = []
        for archivo in sorted((CARPETA_DEL_CODIGO / "interfaz").glob("*.py")):
            texto = archivo.read_text(encoding="utf-8")
            for _, rotulo in PASOS:
                if rotulo in texto:
                    copiados.append(f"{archivo.name}: «{rotulo}»")
        self.assertEqual(
            copiados,
            [],
            "Hay rotulos de paso escritos a mano dentro de interfaz/. Tienen que "
            "venir de datos/pasos.py, que es donde los lee tambien la hoja del "
            f"companero. Copiados: {copiados}",
        )


@unittest.skipUnless(HAY_VENTANAS, "esta maquina no puede abrir ventanas")
class LosTresEstadosSeDistinguen(unittest.TestCase):
    """CA-2 y CA-3: si, no y sin contestar se leen y se ven distintos."""

    def setUp(self):
        import tkinter as tk

        from interfaz.tema import aplicar_tema

        self.raiz = tk.Tk()
        self.raiz.attributes("-alpha", 0.0)
        aplicar_tema(self.raiz)

    def tearDown(self):
        self.raiz.destroy()

    def _respuesta(self, valor):
        from interfaz.pasos import RespuestaDelPaso

        widget = RespuestaDelPaso(self.raiz, "1. Preparación", valor)
        widget.grid()
        self.raiz.update()
        return widget

    def test_las_tres_palabras_son_distintas(self):
        palabras = [self._respuesta(valor).palabra() for valor in (1, 0, None)]
        self.assertEqual(
            len(set(palabras)),
            3,
            f"Los tres estados escriben {palabras}, y dos de ellos dicen lo mismo. "
            "Sin contestar tiene que leerse distinto de «No».",
        )

    def test_sin_contestar_no_dice_ni_si_ni_no(self):
        sin_contestar = self._respuesta(None).palabra().strip().lower()
        self.assertNotIn(
            sin_contestar,
            ("sí", "si", "no"),
            "Una pregunta que nadie miro no puede escribirse como una respuesta.",
        )

    def test_los_tres_dibujos_son_distintos(self):
        dibujos = [dibujo_de(self._respuesta(valor).dibujo) for valor in (1, 0, None)]
        for primero in range(len(dibujos)):
            for segundo in range(primero + 1, len(dibujos)):
                self.assertNotEqual(
                    dibujos[primero],
                    dibujos[segundo],
                    "Dos de los tres estados se pintan igual. Es el fallo que se "
                    "midio en la casilla nativa de Tk: «no leida» y «marcada» "
                    "daban 0 pixeles de diferencia.",
                )

    def test_no_se_pinta_como_una_casilla_de_ordenanza(self):
        """CA-7: los pasos no son las ordenanzas y no se ven como ellas."""
        from interfaz.casilla import CasillaDeOrdenanza

        ordenanza = CasillaDeOrdenanza(self.raiz, "Investidura", valor=1)
        ordenanza.grid()
        self.raiz.update()
        self.assertNotEqual(
            dibujo_de(self._respuesta(1).dibujo),
            dibujo_de(ordenanza._dibujo),
            "Un paso contestado que si se pinta exactamente igual que una "
            "ordenanza marcada. Son dos preguntas distintas sobre la misma "
            "persona y tienen que distinguirse de un vistazo.",
        )


@unittest.skipUnless(HAY_VENTANAS, "esta maquina no puede abrir ventanas")
class LaPantallaEnsenaLosPasos(unittest.TestCase):
    """CA-1, CA-4, CA-5 y CA-8, sobre la pantalla de correccion de verdad."""

    def setUp(self):
        import tkinter as tk

        from interfaz.tema import aplicar_tema

        self.carpeta = Path(tempfile.mkdtemp(prefix="fichas_pasos_"))
        self.conexion = abrir_conexion(self.carpeta / "fichas.db")
        aplicar_esquema(self.conexion)
        self.caso = alta_de_caso(
            self.conexion, numero_caso="PASO2609", fecha_viaje="2026-09-30"
        )
        self.con_propuesta = alta_de_persona(
            self.conexion,
            self.caso.id,
            mrn="055-1111-3853",
            nombre="PERSONA CON PROPUESTA",
            fila_formulario=1,
        )
        self.sin_propuesta = alta_de_persona(
            self.conexion,
            self.caso.id,
            mrn="055-1111-3854",
            nombre="PERSONA SIN PROPUESTA",
            fila_formulario=2,
        )
        self.companero_id = alta_de_companero(self.conexion, "Ana Compañera")
        # Los tres estados a la vez sobre la misma persona: dos contestados que
        # si, uno que no, y tres en blanco. Es el caso real —el companero copia lo
        # que ve y lo que no ve lo deja— y es el unico que prueba las tres cosas.
        guardar_propuesta(
            self.conexion,
            self.con_propuesta,
            estado_propuesto=None,
            nota=None,
            companero_id=self.companero_id,
            pasos={
                "paso_preparacion": 1,
                "paso_informacion": 1,
                "paso_cita_del_templo": 0,
                COLUMNA_DE_LA_LLAMADA: 1,
            },
        )

        self.raiz = tk.Tk()
        self.raiz.geometry("1100x720")
        self.raiz.attributes("-alpha", 0.0)
        self.raiz.rowconfigure(0, weight=1)
        self.raiz.columnconfigure(0, weight=1)
        aplicar_tema(self.raiz)
        self.pantalla = self._abrir_la_pantalla()

    def _abrir_la_pantalla(self):
        from interfaz.correccion import PantallaDeCorreccion

        pantalla = PantallaDeCorreccion(
            self.raiz,
            self.conexion,
            self.caso.id,
            al_volver=lambda: None,
            carpeta_de_datos=self.carpeta,
        )
        pantalla.grid(row=0, column=0, sticky="nsew")
        self.raiz.update()
        return pantalla

    def tearDown(self):
        self.pantalla.soltar_atajos()
        self.raiz.destroy()
        self.conexion.close()
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def _bloque(self, persona_id):
        for bloque in self.pantalla._bloques:
            if bloque.persona_id == persona_id:
                return bloque
        self.fail(f"No hay bloque para la persona {persona_id}")

    def test_los_seis_rotulos_y_la_llamada_salen_en_pantalla(self):
        """CA-1. Los siete, y con el rotulo con el que salen en la hoja."""
        escritos = textos_de(self._bloque(self.con_propuesta))
        faltan = [
            rotulo
            for rotulo in [rotulo for _, rotulo in PASOS] + [ROTULO_DE_LA_LLAMADA]
            if rotulo not in escritos
        ]
        self.assertEqual(
            faltan,
            [],
            f"Estos rotulos no aparecen en el bloque de la persona: {faltan}. "
            f"Lo que hay escrito es {escritos}.",
        )

    def test_cada_paso_ensena_el_valor_que_hay_en_la_base(self):
        """CA-2 sobre datos reales: lo pintado es lo guardado, paso por paso."""
        guardado = propuesta_vigente(self.conexion, self.con_propuesta)
        bloque = self._bloque(self.con_propuesta)
        for nombre in NOMBRES_DE_LOS_PASOS + (COLUMNA_DE_LA_LLAMADA,):
            with self.subTest(paso=nombre):
                self.assertEqual(
                    bloque.pasos.respuestas[nombre].valor,
                    guardado[nombre],
                    f"El paso «{nombre}» vale {guardado[nombre]!r} en la base y la "
                    "pantalla ensena otra cosa.",
                )

    def test_se_dice_quien_lo_propuso(self):
        """CA-4. Un dato firmado sin firma visible es un dato sin dueno."""
        escritos = " ".join(textos_de(self._bloque(self.con_propuesta)))
        self.assertIn(
            "Ana Compañera",
            escritos,
            "El bloque de los pasos no dice quien los propuso. Lo que hay escrito "
            f"es: {escritos}",
        )

    def test_una_persona_sin_propuesta_no_ensena_pasos_vacios(self):
        """CA-5. Siete «sin contestar» en cada persona de cada caso es ruido."""
        bloque = self._bloque(self.sin_propuesta)
        self.assertIsNone(
            bloque.pasos,
            "Se dibujan los pasos de una persona sobre la que ningun companero "
            "ha contestado nada.",
        )
        escritos = textos_de(bloque)
        for _, rotulo in PASOS:
            self.assertNotIn(rotulo, escritos)

    def test_las_ordenanzas_siguen_estando_y_siguen_siendo_las_de_antes(self):
        """CA-7. Anadir los pasos no puede llevarse las seis casillas."""
        from interfaz.casilla import CasillaDeOrdenanza

        bloque = self._bloque(self.con_propuesta)
        self.assertEqual(len(bloque.casillas), 6)
        for casilla in bloque.casillas.values():
            self.assertIsInstance(casilla, CasillaDeOrdenanza)
        self.assertIn("Ordenanzas", textos_de(bloque))

    def test_guardar_el_caso_no_toca_lo_que_firmo_el_companero(self):
        """CA-8. La propuesta se ensena, no se edita.

        Los pasos los firma un companero con su nombre y su fecha, y el esquema
        guarda UNA propuesta por persona. Una pantalla que los dejara cambiar
        estaria borrando el trabajo de otra persona sin decirselo a nadie, que es
        el mismo hallazgo ALTO que ya cerro `_exigir_que_no_pise_a_otro`. Que se
        puedan corregir a mano es una decision del dueno, y no se toma aqui.
        """
        antes = propuesta_vigente(self.conexion, self.con_propuesta)
        self.pantalla.guardar()
        despues = propuesta_vigente(self.conexion, self.con_propuesta)
        for nombre in NOMBRES_DE_LOS_PASOS + (
            COLUMNA_DE_LA_LLAMADA,
            "propuesto_por",
            "propuesto_en",
        ):
            with self.subTest(columna=nombre):
                self.assertEqual(antes[nombre], despues[nombre])

    def test_los_pasos_no_anaden_ni_una_parada_al_recorrido_del_tabulador(self):
        """El minuto por formulario no lo paga una seccion que solo se mira.

        ⚠️ **Esto no lo cubria ninguna prueba de antes, y se comprobo.** La prueba
        que fija el recorrido en 58 paradas
        (`pruebas/prueba_correccion.py`) monta seis personas con
        `alta_de_persona` y ninguna propuesta, asi que en ella esta seccion **no
        se dibuja** y seguiria en verde aunque cada paso fuera una parada de Tab.
        Con siete respuestas por persona y seis personas, eso son 42 paradas
        nuevas en un recorrido de 58.

        El riesgo es real y no teorico: `tk::FocusOK` mete en el recorrido a
        cualquier widget con una atadura de foco, y `interfaz/correccion.py` ata
        `<FocusIn>` a todo lo que traiga `takefocus` distinto de vacio o de cero.
        Medido en esta maquina: el `Canvas` del paso lo trae en «0» y el marco y
        las etiquetas en vacio, asi que ninguno se ata. Lo que fija esta prueba es
        que siga siendo asi.
        """
        recorrido = []
        inicio = control = self.pantalla._campos_del_caso["fecha_viaje"].entrada
        control.focus_set()
        self.raiz.update()
        recorrido.append(control)
        for _ in range(200):
            control = control.tk_focusNext()
            if control is None or control is inicio:
                break
            control.focus_set()
            self.raiz.update()
            recorrido.append(control)

        bloque_de_pasos = self._bloque(self.con_propuesta).pasos
        dentro_de_los_pasos = [
            str(control)
            for control in recorrido
            if self._cuelga_de(control, bloque_de_pasos)
        ]
        self.assertEqual(
            dentro_de_los_pasos,
            [],
            "Tab para dentro de la seccion de los pasos, que es de solo lectura: "
            f"{dentro_de_los_pasos}",
        )

    @staticmethod
    def _cuelga_de(widget, antepasado):
        """Si ese control vive dentro de ese otro, subiendo por sus padres.

        Se sube por la cadena de padres y no se comparan nombres de ruta de Tk,
        por el motivo ya medido en `_bloque_del_widget`: Tk numera los hermanos y
        el nombre del primero es prefijo del de los demas.
        """
        while widget is not None:
            if widget is antepasado:
                return True
            widget = getattr(widget, "master", None)
        return False

    def test_los_pasos_no_se_cuentan_como_ordenanzas_sin_leer(self):
        """El contador del pie cuenta ordenanzas. Tres pasos en blanco no lo son.

        Si los pasos entraran en esa cuenta, «Ninguna ordenanza marcada» dejaria
        de resolver el bloqueo y la verificacion se quedaria trabada sin motivo
        que Miguel pueda ver.
        """
        bloque = self._bloque(self.con_propuesta)
        self.assertEqual(bloque.casillas_sin_leer(), 6)
