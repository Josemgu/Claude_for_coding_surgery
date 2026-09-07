"""Ningún rótulo de la pantalla de corrección sale en inglés.

**El defecto, sondeado por QA sobre la ventana real el 2026-09-03.** Los
formularios del dueño son la versión ESPAÑOLA del papel, y la pantalla de
corrección rotulaba cada campo con la etiqueta INGLESA escrita a mano en
`interfaz/correccion.py` y en `interfaz/persona.py`:

    Case Number
    Date traveling to the temple
    Full Name(s)
    Membership Record Number
    Ward/Branch Name and Unit Number

**0 de 5 aparecen en su papel.** Una etiqueta del papel existe para ayudar a
localizar el campo en la hoja; una que no está en la hoja no ayuda, estorba. Y
choca de frente con la regla permanente 4, que no admite excepción por el idioma
del original: la interfaz habla español **siempre**.

**Lo que estas pruebas fijan** sale del criterio 3 del pase y no del código:

  1. ninguna de las cinco cadenas inglesas aparece en ningún rótulo visible de la
     pantalla de corrección, recorrida entera con la ventana montada de verdad;
  2. la etiqueta del papel que sí se escribe es la forma española que declara
     `extraccion/etiquetas.py`, y no una copia escrita a mano en la interfaz.

La primera se hace sobre la ventana **construida**, no leyendo el archivo: el
defecto era exactamente que el archivo declaraba una cosa y nadie miraba la
ventana. Si algún día un rótulo inglés entra por otro archivo, esta prueba lo ve
igual.
"""

import unittest

from extraccion.etiquetas import (
    CAMPO_DE_LA_FECHA_DE_VIAJE,
    CAMPO_DE_LA_UNIDAD,
    CAMPO_DE_LOS_NOMBRES,
    CAMPO_DEL_MRN,
    forma_espanola_de,
)
from importacion.guardado import guardar_las_paginas_del_documento
from datos.repositorio import leer_caso_por_numero
from pruebas.comun import PruebaConBaseTemporal
from pruebas.prueba_importacion import _formulario, _persona

# Las cinco que QA leyó en la ventana. Van escritas aquí, en la prueba, y no
# importadas de ningún sitio: si mañana alguien las vuelve a poner en la interfaz
# tomándolas de `FORMAS_DE_CADA_CAMPO`, una prueba que las importara de allí
# seguiría en verde. Lo que se prohíbe es la CADENA, venga de donde venga.
ROTULOS_INGLESES_QUE_NO_PUEDEN_SALIR = (
    "Case Number",
    "Date traveling to the temple",
    "Full Name(s)",
    "Membership Record Number",
    "Ward/Branch Name and Unit Number",
)


def _hay_ventanas():
    try:
        import tkinter

        raiz = tkinter.Tk()
    except Exception:  # pragma: no cover - depende de la maquina, no del codigo
        return False
    raiz.destroy()
    return True


def _textos_visibles(widget):
    """Todo lo que se lee en ese widget y en los que cuelgan de él.

    Se pregunta por la opción `text` y se ignora el que no la tenga: un `Frame` no
    rotula nada, y un `Entry` lleva el DATO —el nombre de una persona real—, no un
    rótulo. Lo que esta prueba mira son los rótulos.
    """
    textos = []
    try:
        valor = widget.cget("text")
    except Exception:
        valor = None
    if isinstance(valor, str) and valor:
        textos.append(valor)
    for hijo in widget.winfo_children():
        textos.extend(_textos_visibles(hijo))
    return textos


class LaEtiquetaDelPapelSeDiceEnEspanol(unittest.TestCase):
    """Sin ventana: los cuatro campos del papel tienen su forma española."""

    def test_las_cuatro_formas_espanolas_no_llevan_ni_una_palabra_inglesa(self):
        for campo in (
            CAMPO_DE_LA_FECHA_DE_VIAJE,
            CAMPO_DE_LA_UNIDAD,
            CAMPO_DE_LOS_NOMBRES,
            CAMPO_DEL_MRN,
        ):
            forma = forma_espanola_de(campo)
            self.assertNotIn(forma, ROTULOS_INGLESES_QUE_NO_PUEDEN_SALIR)
            for ingles in ROTULOS_INGLESES_QUE_NO_PUEDEN_SALIR:
                self.assertNotIn(ingles, forma)

    def test_la_interfaz_no_escribe_su_propia_copia_de_la_etiqueta(self):
        """La forma que pinta la pantalla es la que declara la extracción.

        Escrita a mano en la interfaz, el día que se corrija una forma se corrige
        en un sitio y no en el otro — que es justo como llegó este defecto.
        """
        from interfaz.correccion import CAMPOS_DEL_CASO

        del_papel = {campo[2] for campo in CAMPOS_DEL_CASO if campo[2]}
        self.assertEqual(
            del_papel,
            {
                forma_espanola_de(CAMPO_DE_LA_FECHA_DE_VIAJE),
                forma_espanola_de(CAMPO_DE_LA_UNIDAD),
            },
        )


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class LaPantallaDeCorreccionNoTieneNiUnRotuloIngles(PruebaConBaseTemporal):
    """Dada la pantalla de corrección de un caso con personas, cuando se recorren
    todos sus rótulos visibles, entonces ninguno lleva una de las cinco cadenas
    inglesas que QA sondeó."""

    def setUp(self):
        super().setUp()
        guardar_las_paginas_del_documento(
            self.conexion,
            [
                _formulario(
                    personas=[
                        _persona(1, "ANONIMO, JOSE MIGUEL", "055-1111-3853"),
                        _persona(2, "PEREZ, MARIA", "055-1111-3854"),
                    ]
                )
            ],
        )
        self.caso_id = leer_caso_por_numero(self.conexion, "CASP2609")["id"]

    def _rotulos_de_la_pantalla(self):
        import tkinter as tk

        from interfaz.correccion import PantallaDeCorreccion
        from interfaz.tema import aplicar_tema

        raiz = tk.Tk()
        raiz.geometry("1100x720")
        raiz.attributes("-alpha", 0.0)
        aplicar_tema(raiz)
        pantalla = PantallaDeCorreccion(
            raiz, self.conexion, self.caso_id, al_volver=lambda: None,
            carpeta_de_datos=self.carpeta_temporal,
        )
        try:
            raiz.update_idletasks()
            return _textos_visibles(pantalla)
        finally:
            pantalla.soltar_atajos()
            raiz.destroy()

    def test_ningun_rotulo_visible_lleva_una_de_las_cinco_cadenas_inglesas(self):
        rotulos = self._rotulos_de_la_pantalla()
        self.assertTrue(rotulos, "la pantalla no devolvió ni un rótulo que mirar")
        for ingles in ROTULOS_INGLESES_QUE_NO_PUEDEN_SALIR:
            culpables = [rotulo for rotulo in rotulos if ingles in rotulo]
            self.assertEqual(
                culpables, [], f"«{ingles}» sigue saliendo en la pantalla: {culpables}"
            )

    def test_la_pista_espanola_del_papel_si_se_lee_en_la_pantalla(self):
        """La otra mitad: quitar el inglés no puede dejar el campo sin pista."""
        rotulos = self._rotulos_de_la_pantalla()
        for campo in (
            CAMPO_DE_LA_FECHA_DE_VIAJE,
            CAMPO_DE_LA_UNIDAD,
            CAMPO_DE_LOS_NOMBRES,
            CAMPO_DEL_MRN,
        ):
            forma = forma_espanola_de(campo)
            self.assertIn(
                forma, rotulos, f"la pista «{forma}» no se lee en la pantalla"
            )
