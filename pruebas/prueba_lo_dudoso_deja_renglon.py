"""Dos hojas que se contradicen, y una página con etiquetas perdidas, dejan renglón.

Estas pruebas cierran los dos hallazgos que QA marcó como ALTO y MEDIO el
2026-09-03, y las dos tienen el mismo defecto de fondo: **el programa lo sabía y
no lo decía**.

---

**Hallazgo 3 — la segunda hoja se descarta muda.** Medido por QA sobre un grupo
español de dos hojas:

    hoja 1 fecha_viaje leida: 2026-08-25
    hoja 2 fecha_viaje leida: 2026-08-26
    fecha GUARDADA en el caso: 2026-08-25
    AVISOS: pag 1 -> ()   pag 2 -> ()

Que **mande la hoja que abre el caso está bien decidido y no se toca**
(`importacion/guardado.py`, `_unir_a_un_caso_de_esta_tanda`): si cada hoja pisara,
la última ganaría y una fecha tachada en la página 6 borraría una bien leída en la
1. Lo que falla es el silencio. Mandar a alguien al templo el día equivocado es el
daño que este programa existe para evitar, y la pantalla enseñaba una sola fecha
sin decir que otra hoja del mismo caso decía otra cosa.

---

**Hallazgo 5 — una página mal leída entra sin marcarse.** La página 6 del grupo
inglés: 2 anclas no encontradas, unidad ilegible, un nombre que no parece un
nombre con confianza 0,796 y sin MRN. `documentos_ilegibles` = 0 y
`captura_manual` = 0, porque el umbral es 0,6 y 0,796 lo pasa.

⚠️ **El umbral NO se toca**, y no por prudencia: sale de `DECISIONES.md` («si más
del 60% de los campos vuelven con confianza bajo 0.6…») y moverlo a ojo cambiaría
el comportamiento de todas las páginas para arreglar dos. La puerta se cierra por
otro sitio, y ese sitio está **medido** sobre las 14 páginas reales de
`pdfs_referencia/` (2026-09-03, RapidOCR con los modelos latinos):

    pdf                     pag  anclas_perdidas  n_caso      fecha         uni_num
    59d87aad-CASP2609_Jo      1        0          'CASD2609'  '2026-09-08'  '700001'
    60025df4-CASP2609_Da      1        0          'CASP2609'  '2026-09-08'  '700005'
    aa038ec0-PARB2609_No      1        0          'PARB2609'  '2026-09-02'  '700006'
    cb2f18be-SURB2609_Su    1-4        0          'SURB2609'  '2026-09-05'  '7000011'
    cb2f18be-SURB2609_Su      5        4          'SURB2609'   None          None
    cb2f18be-SURB2609_Su      6        2          'SURB2609'   None          None
    ed633b94-SURB2609_Su    1-4        0          'SURB2609'  '2026-09-05'  '7000011'
    ed633b94-SURB2609_Su      5        4          'SURB2609'   None          None
    ed633b94-SURB2609_Su      6        2          'SURB2609'   None          None

**10 páginas de 14 pierden CERO anclas, y las 4 que pierden alguna son
exactamente las 4 de las que no salió ni fecha ni unidad.** La separación es
limpia: ni un falso positivo ni un falso negativo sobre el material real. Por eso
«se perdió alguna etiqueta impresa» sirve de puerta y no hace falta ningún número
nuevo. Si algún día una página buena empieza a perder anclas, aparecerá en la
lista de renglones y se verá — que es mejor que no verlo, que es lo de hoy.
"""

import unittest

from datos.ilegibles import (
    ANCLAS_PERDIDAS,
    CAMPO_DISCREPANTE,
    HOJA_APARTE,
    contar_por_motivo,
    documentos_ilegibles,
)
from datos.repositorio import leer_caso_por_numero
from extraccion.campos import campo_vacio
from importacion.guardado import guardar_las_paginas_del_documento
from pruebas.comun import PruebaConBaseTemporal
from pruebas.prueba_importacion import _campo, _formulario, _persona

# Las dos fechas que QA midió en las dos hojas del grupo español.
FECHA_DE_LA_HOJA_UNO = "2026-08-25"
FECHA_DE_LA_HOJA_DOS = "2026-08-26"

# Las dos etiquetas que la página 6 del grupo inglés no encontró, medidas arriba.
ANCLAS_QUE_SE_PERDIERON = ("fecha de viaje al templo", "nombre del templo")


def _hoja(pagina, fecha=FECHA_DE_LA_HOJA_UNO, unidad="7000011", anclas=()):
    """Una hoja del mismo caso, con la fecha y la unidad que se le digan.

    Cada hoja trae una persona con MRN distinto: dos personas con el mismo MRN son
    la misma, y el guardado no la duplica. Con el MRN repetido esta prueba estaría
    midiendo la deduplicación en vez de lo que dice medir.
    """
    return _formulario(
        numero="SURB2609",
        pagina=pagina,
        personas=[_persona(pagina, f"PERSONA {pagina}", f"055-1111-{3850 + pagina:04d}")],
    )._replace(
        fecha_viaje=_campo(fecha, confianza=0.95),
        unidad_numero=_campo(unidad, confianza=0.95) if unidad else campo_vacio(),
        anclas_no_encontradas=anclas,
    )


class DosFamiliasConElMismoNumeroNoSeFunden(PruebaConBaseTemporal):
    """Dado un documento de dos hojas con el mismo número de caso pero fechas de
    viaje distintas, cuando se importa, entonces la segunda NO se une a la primera
    y queda su renglón con todo lo que se le leyó.

    Es el hallazgo CRÍTICO de la auditoría final de QA, medido sobre los dos PDF
    reales del dueño unidos en un documento de dos hojas —que es lo que produce un
    escáner de lote—:

        Se guardó 1 caso de 2 páginas, con 5 personas en total.
        caso num=BARC2608 fecha=2026-08-25   personas por hoja: {1: 4, 2: 1}

    La hoja 2 había leído `2026-08-26`. La persona de la hoja 2 se quedó con la
    fecha de viaje de otra familia y no hay marcha atrás: ninguna función mueve una
    persona de caso.
    """

    def setUp(self):
        super().setUp()
        self.resultados = guardar_las_paginas_del_documento(
            self.conexion,
            [
                _hoja(1, fecha=FECHA_DE_LA_HOJA_UNO),
                _hoja(2, fecha=FECHA_DE_LA_HOJA_DOS),
            ],
        )

    def test_la_hoja_que_contradice_no_se_une_pero_SI_entra(self):
        """El caso se queda con SUS personas, y la otra hoja abre su propio caso.

        ⚠️ Hasta el 2026-09-03 esa hoja se tiraba entera. Lo que evita el daño no
        es tirarla: es **no fundirla**. Entrar aparte cumple las dos cosas.
        """
        self.assertTrue(self.resultados[1].importada)
        self.assertNotEqual(self.resultados[1].caso_id, self.resultados[0].caso_id)
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM personas").fetchone()[0], 2
        )

    def test_no_queda_un_caso_con_las_dos_familias_y_una_sola_fecha(self):
        """Que es exactamente lo que QA midió: 1 caso, 5 personas, 1 fecha."""
        caso = leer_caso_por_numero(self.conexion, "SURB2609")
        self.assertEqual(caso["fecha_viaje"], FECHA_DE_LA_HOJA_UNO)
        personas = self.conexion.execute(
            "SELECT pagina_pdf FROM personas WHERE caso_id = ?", (caso["id"],)
        ).fetchall()
        self.assertEqual([fila["pagina_pdf"] for fila in personas], [1])

    def test_ninguna_persona_se_queda_con_la_fecha_de_la_otra_familia(self):
        """El daño concreto: una persona citada el día equivocado.

        La persona de la hoja 2 tiene que viajar el día que dice SU hoja, y no el
        de la hoja 1. Es la misma comprobación de antes leída al derecho: antes esa
        persona no existía, ahora existe con su fecha.
        """
        fechas = self.conexion.execute(
            "SELECT DISTINCT c.fecha_viaje FROM personas p "
            "JOIN casos c ON c.id = p.caso_id WHERE p.pagina_pdf = 2"
        ).fetchall()
        self.assertEqual([fila["fecha_viaje"] for fila in fechas], [FECHA_DE_LA_HOJA_DOS])

    def test_la_hoja_que_no_se_unio_deja_su_renglon_con_el_motivo(self):
        """Un desvío que solo se dice en un cuadro de diálogo es un desvío perdido."""
        self.assertEqual(contar_por_motivo(self.conexion).get(HOJA_APARTE), 1)

    def test_el_renglon_dice_las_dos_fechas_y_de_que_campo_habla(self):
        """Sin los dos valores, el renglón dice «hay un problema» y no cuál."""
        renglon = self._renglon_de_la_discrepancia()
        self.assertIn(FECHA_DE_LA_HOJA_UNO, renglon["detalle"])
        self.assertIn(FECHA_DE_LA_HOJA_DOS, renglon["detalle"])
        self.assertIn("Fecha de viaje", renglon["detalle"])

    def test_el_renglon_lleva_dentro_lo_que_traia_la_hoja_que_no_entro(self):
        """«Con todo lo leído dentro», como la vía del archivo suelto.

        Sin los nombres no se puede decidir si son dos familias o el mismo papel
        repetido, que es la única pregunta que importa aquí.
        """
        renglon = self._renglon_de_la_discrepancia()
        self.assertIn("PERSONA 2", renglon["detalle"])
        self.assertEqual(renglon["pagina_pdf"], 2)

    def test_la_pagina_que_no_se_unio_lo_dice_en_sus_avisos(self):
        """QA lo midió vacío: `AVISOS: pag 1 -> ()   pag 2 -> ()`.

        Viaja en `avisos` y ya no en `motivo`: `motivo` es de las páginas que NO
        entran, y esta entra. El resumen de la importación pinta los dos.
        """
        aviso = " ".join(self.resultados[1].avisos)
        self.assertIn(FECHA_DE_LA_HOJA_DOS, aviso)
        self.assertIn(FECHA_DE_LA_HOJA_UNO, aviso)
        self.assertIn("CASO APARTE", aviso)

    def _renglon_de_la_discrepancia(self):
        return next(
            fila
            for fila in documentos_ilegibles(self.conexion)
            if fila["motivo"] == HOJA_APARTE
        )


class LasHojasQueNoSeContradicenSeSiguenUniendo(PruebaConBaseTemporal):
    """La otra mitad, y la que no se puede romper: los grupos reales.

    QA midió que las hojas de los dos grupos de `pdfs_referencia/` **no discrepan
    entre sí en ningún campo**, así que tienen que seguir uniéndose. Un arreglo que
    parta el grupo de seis hojas en seis casos sería peor que el fallo que cierra.
    """

    def test_dos_hojas_que_coinciden_se_unen_en_un_solo_caso(self):
        resultados = guardar_las_paginas_del_documento(
            self.conexion, [_hoja(1), _hoja(2)]
        )
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0], 1
        )
        self.assertEqual(sum(resultado.personas for resultado in resultados), 2)
        self.assertEqual(contar_por_motivo(self.conexion).get(CAMPO_DISCREPANTE), None)

    def test_un_grupo_de_seis_hojas_sigue_dando_un_caso_con_sus_seis_personas(self):
        """El grupo real del dueño: seis hojas, un caso. Criterio 1 del pase."""
        resultados = guardar_las_paginas_del_documento(
            self.conexion, [_hoja(pagina) for pagina in range(1, 7)]
        )
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0], 1
        )
        self.assertEqual(sum(resultado.personas for resultado in resultados), 6)

    def test_una_hoja_que_no_leyo_el_campo_no_contradice_a_nadie(self):
        """«No lo leí» no es contradecir: contradecir es decir OTRA cosa.

        Es la mitad que decide si esto rompe los grupos reales: las hojas 5 y 6 del
        grupo del dueño entran SIN fecha y SIN unidad —medido: 4 y 2 anclas
        perdidas—, y tratarlas como discrepancia dejaría fuera del caso a la mitad
        de un grupo de seis.
        """
        resultados = guardar_las_paginas_del_documento(
            self.conexion,
            [_hoja(1), _hoja(2)._replace(fecha_viaje=campo_vacio(), unidad_numero=campo_vacio())],
        )
        self.assertTrue(resultados[1].importada)
        self.assertEqual(contar_por_motivo(self.conexion).get(CAMPO_DISCREPANTE), None)

    def test_una_hoja_que_discrepa_en_la_unidad_tampoco_se_une(self):
        """No solo la fecha: la unidad también identifica a la familia.

        ⚠️ **Lo que cambió el 2026-09-03: no unirse dejó de ser tirarla.** Esa hoja
        abre su propio caso con todo lo que se le leyó y deja su renglón con motivo
        `hoja_aparte`. La mitad que no cambia —y que es la que evita el daño— es que
        NO se funde con la otra: una persona con la fecha de viaje de otra familia
        es mandar a alguien al templo el día equivocado.
        """
        resultados = guardar_las_paginas_del_documento(
            self.conexion, [_hoja(1), _hoja(2, unidad="7000014")]
        )
        self.assertTrue(resultados[1].importada)
        self.assertNotEqual(resultados[1].caso_id, resultados[0].caso_id)
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0], 2
        )
        self.assertEqual(contar_por_motivo(self.conexion).get(HOJA_APARTE), 1)
        self.assertEqual(contar_por_motivo(self.conexion).get(CAMPO_DISCREPANTE), None)


class UnaPaginaConEtiquetasPerdidasSeMarca(PruebaConBaseTemporal):
    """Dada una página que perdió etiquetas impresas, cuando entra, entonces deja
    renglón aunque sus datos hayan pasado el umbral de confianza."""

    def test_la_pagina_con_anclas_perdidas_deja_renglon(self):
        """Criterio 5 del pase, sin tocar el umbral de `DECISIONES.md`."""
        guardar_las_paginas_del_documento(
            self.conexion, [_hoja(1, anclas=ANCLAS_QUE_SE_PERDIERON)]
        )
        self.assertEqual(contar_por_motivo(self.conexion).get(ANCLAS_PERDIDAS), 1)

    def test_el_renglon_nombra_las_etiquetas_que_faltaron_en_espanol(self):
        """Los nombres se le leen a Miguel: «Full Name(s)» no le dice nada."""
        guardar_las_paginas_del_documento(
            self.conexion, [_hoja(1, anclas=ANCLAS_QUE_SE_PERDIERON)]
        )
        renglon = next(
            fila
            for fila in documentos_ilegibles(self.conexion)
            if fila["motivo"] == ANCLAS_PERDIDAS
        )
        for ancla in ANCLAS_QUE_SE_PERDIERON:
            self.assertIn(ancla, renglon["detalle"])

    def test_la_pagina_entra_igual_y_no_se_pierde_nada(self):
        """Marcarla no es tirarla: lo leído se guarda como siempre."""
        resultado = guardar_las_paginas_del_documento(
            self.conexion, [_hoja(1, anclas=ANCLAS_QUE_SE_PERDIERON)]
        )[0]
        self.assertTrue(resultado.importada)
        self.assertEqual(resultado.personas, 1)

    def test_una_pagina_sin_anclas_perdidas_no_deja_renglon(self):
        """Las 10 páginas de 14 que pierden cero: no pueden llenar la lista."""
        guardar_las_paginas_del_documento(self.conexion, [_hoja(1)])
        self.assertEqual(contar_por_motivo(self.conexion).get(ANCLAS_PERDIDAS), None)

    def test_la_pagina_lo_dice_tambien_en_su_aviso(self):
        resultado = guardar_las_paginas_del_documento(
            self.conexion, [_hoja(1, anclas=ANCLAS_QUE_SE_PERDIERON)]
        )[0]
        self.assertTrue(
            any("etiqueta" in aviso.lower() for aviso in resultado.avisos),
            f"la página entró sin decir que perdió etiquetas: {resultado.avisos}",
        )

    def test_una_hoja_que_se_une_a_otra_tambien_se_marca(self):
        """La página 6 del grupo real es una hoja que se une, no la que abre.

        Es justo el caso que QA midió, y el que se escapaba: el diagnóstico de
        `_unir_a_un_caso_de_esta_tanda` no miraba nada de esto.
        """
        guardar_las_paginas_del_documento(
            self.conexion,
            [_hoja(1), _hoja(6, fecha=FECHA_DE_LA_HOJA_UNO, anclas=ANCLAS_QUE_SE_PERDIERON)],
        )
        self.assertEqual(contar_por_motivo(self.conexion).get(ANCLAS_PERDIDAS), 1)
        renglon = next(
            fila
            for fila in documentos_ilegibles(self.conexion)
            if fila["motivo"] == ANCLAS_PERDIDAS
        )
        self.assertEqual(renglon["pagina_pdf"], 6)


def _hay_ventanas():
    try:
        import tkinter

        raiz = tkinter.Tk()
    except Exception:  # pragma: no cover - depende de la maquina, no del codigo
        return False
    raiz.destroy()
    return True


@unittest.skipUnless(_hay_ventanas(), "esta maquina no puede abrir ventanas")
class LaPantallaDelCasoLoDice(PruebaConBaseTemporal):
    """Dado un caso al que la importación le vio algo raro, cuando se abre su
    pantalla de corrección, entonces el aviso se lee arriba y no solo en la lista
    de renglones.

    Es la mitad del criterio que la base no cubre: «que quede registrado **y se
    vea**». Quien abre este caso tres días después no vio el resumen de aquella
    importación.

    ⚠️ Este caso se monta con `anclas_perdidas` y ya no con `campo_discrepante`, y
    el cambio es de la decisión, no de la prueba: desde el 2026-09-03 la hoja que
    contradice **no entra**, así que su renglón no cuelga de ningún caso y no hay
    pantalla de caso donde enseñarlo. Se enseña en la lista de lo que no entró. Lo
    que esta prueba vigila —que la cabecera del caso diga lo que la importación
    vio— sigue siendo lo mismo.
    """

    def setUp(self):
        super().setUp()
        guardar_las_paginas_del_documento(
            self.conexion,
            [
                _hoja(1, anclas=ANCLAS_QUE_SE_PERDIERON),
                _hoja(2),
            ],
        )
        self.caso_id = leer_caso_por_numero(self.conexion, "SURB2609")["id"]

    def _avisos_de_la_pantalla(self):
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
            return pantalla._textos_de_aviso()
        finally:
            pantalla.soltar_atajos()
            raiz.destroy()

    def test_la_cabecera_dice_que_la_importacion_perdio_etiquetas(self):
        juntos = " ".join(self._avisos_de_la_pantalla())
        for ancla in ANCLAS_QUE_SE_PERDIERON:
            self.assertIn(ancla, juntos)

    def test_el_aviso_dice_ademas_que_hacer_con_eso(self):
        """Un aviso que dice «hay un problema» y no qué hacer no sirve de nada."""
        juntos = " ".join(self._avisos_de_la_pantalla())
        self.assertIn("compare con el PDF", juntos)


class LosMotivosNuevosSeSabenDecirEnEspanol(unittest.TestCase):
    """Un motivo sin frase sale en la lista como un código, y eso no informa."""

    def test_los_dos_motivos_nuevos_tienen_su_frase(self):
        from datos.ilegibles import FRASES_DE_LOS_MOTIVOS, MOTIVOS

        for motivo in (ANCLAS_PERDIDAS, CAMPO_DISCREPANTE):
            self.assertIn(motivo, MOTIVOS)
            self.assertIn(motivo, FRASES_DE_LOS_MOTIVOS)
            self.assertGreater(len(FRASES_DE_LOS_MOTIVOS[motivo]), 40)

    def test_anclas_perdidas_dice_que_la_pagina_SI_entro(self):
        """El renglón sale en ámbar y no en rojo, porque no se perdió nada.

        La lista pintaba de rojo —«de esta página no quedó nada»— todo lo que no
        fuera `sin_numero_de_caso`. Con este motivo eso habría mandado a Miguel a
        buscar datos que están guardados. Ahora la decisión vive en
        `datos/ilegibles.py`, al lado del motivo, y no en el archivo que lo dibuja:
        un motivo nuevo tiene que decidir de qué color sale donde se define.
        """
        from datos.ilegibles import MOTIVOS_EN_QUE_LA_PAGINA_SI_ENTRO

        self.assertIn(ANCLAS_PERDIDAS, MOTIVOS_EN_QUE_LA_PAGINA_SI_ENTRO)

    def test_ningun_motivo_del_que_no_quedo_nada_se_cuela_en_esa_lista(self):
        """La otra dirección: un ámbar sobre una página perdida es peor que un rojo.

        `campo_discrepante` entra en esta lista desde el 2026-09-03: la hoja que
        contradice ya no se une al caso, así que de ella no quedó nada guardado y
        pintarla en ámbar diría que hay algo que buscar y no lo hay.
        """
        from datos.ilegibles import (
            MOTIVOS_EN_QUE_LA_PAGINA_SI_ENTRO,
            NO_SE_PUDO_ABRIR,
            SIN_CAMPOS,
            SIN_TEXTO,
        )

        for motivo in (NO_SE_PUDO_ABRIR, SIN_TEXTO, SIN_CAMPOS, CAMPO_DISCREPANTE):
            self.assertNotIn(motivo, MOTIVOS_EN_QUE_LA_PAGINA_SI_ENTRO)
