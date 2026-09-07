"""El mismo formulario llega en ingles y en espanol, y los dos se tienen que leer.

**El fallo que estas pruebas cierran.** Los cuatro documentos con los que se
construyo el extractor estan en ingles, y las etiquetas quedaron escritas en
ingles dentro del codigo. Los formularios del dueno son la version ESPANOLA del
mismo papel —el pie del formulario en blanco lo dice: «Traduccion de General
Temple Patron Assistance Fund Request and Approval Form. Spanish»—, asi que
ninguna etiqueta casaba, no se localizaba ningun campo, y la pagina entera salia
vacia y marcada para captura manual.

**Los nombres, las cedulas y las unidades de este archivo son INVENTADOS.** La
geometria no lo es: reproduce la que se midio el 2026-09-03 sobre un formulario
espanol real rasterizado a 2705x3500 px —cabeceras de personas cerca de y=795,
filas de persona cada ~62 px desde y=1027, «Fecha de viaje al templo» en y=1503
con su valor en y=1558, «Nombre y numero de unidad del barrio / rama» en y=2413
con su valor en y=2466—, porque es justo lo que el codigo usa para decidir.
"""

import unittest
from collections import namedtuple

from extraccion.bandas import PARECIDO_MINIMO_DEL_ANCLA, localizar_ancla, parecido
from extraccion.campos import ORIGEN_OCR, ORIGEN_VACIO, CampoExtraido
from extraccion.etiquetas import (
    CAMPO_DE_LA_CITA_DEL_TEMPLO,
    CAMPO_DE_LA_ESTACA,
    CAMPO_DE_LA_FECHA_DE_REGRESO,
    CAMPO_DE_LA_FECHA_DE_VIAJE,
    CAMPO_DE_LA_UNIDAD,
    CAMPO_DE_LOS_NOMBRES,
    CAMPO_DEL_MRN,
    CAMPOS_DEL_FORMULARIO,
    FORMAS_DE_CADA_CAMPO,
    formas_de,
    normalizar_para_comparar,
)
from extraccion.formulario import (
    CamposDelCaso,
    campos_del_caso,
    localizar_las_anclas,
    vaciar_el_formulario,
)
from extraccion.geometria import Rectangulo
from extraccion.personas import extraer_personas

LineaFalsa = namedtuple("LineaFalsa", ("texto", "rectangulo", "confianza"))

ALTO_DE_UNA_LINEA = 40.0


def linea(texto, y0, x0, ancho=380.0, alto=ALTO_DE_UNA_LINEA, confianza=0.99):
    """Una linea de OCR fabricada, con la caja que tendria en el escaneo."""
    return LineaFalsa(texto, Rectangulo(x0, y0, x0 + ancho, y0 + alto), confianza)


# Una pagina espanola completa, con datos inventados y la geometria medida.
def pagina_espanola():
    return [
        linea("BARC1234", y0=499.0, x0=2286.0, ancho=300.0),
        linea("Nombre(s) de pila", y0=794.0, x0=495.0),
        linea("Número de cédula de miembro", y0=796.0, x0=1319.0, ancho=560.0),
        linea("Ana Rivas", y0=1027.0, x0=185.0),
        linea("111-2222-3333", y0=1028.0, x0=974.0),
        linea("Luis Rivas", y0=1089.0, x0=185.0),
        linea("111-2222-4444", y0=1090.0, x0=974.0),
        linea("Nombre del templo", y0=1397.0, x0=158.0),
        linea("Fecha de la cita del templo", y0=1397.0, x0=1117.0, ancho=560.0),
        linea("Fecha de viaje al templo", y0=1503.0, x0=157.0),
        linea("Fecha de regreso del templo", y0=1510.0, x0=1559.0, ancho=560.0),
        linea("15-10-2026", y0=1558.0, x0=176.0, ancho=260.0),
        linea("22-10-2026", y0=1554.0, x0=1562.0, ancho=260.0),
        linea("Costos asociados", y0=1625.0, x0=158.0),
        linea(
            "Nombre y número de unidad del barrio / rama",
            y0=2413.0,
            x0=160.0,
            ancho=900.0,
        ),
        linea(
            "Nombre y número de unidad de la estaca / distrito",
            y0=2412.0,
            x0=1511.0,
            ancho=980.0,
        ),
        linea("Rama Ejemplo 123456", y0=2466.0, x0=178.0, ancho=500.0),
    ]


def pagina_inglesa():
    """La misma pagina, rotulada en ingles. Misma geometria, mismos datos."""
    traduccion = {
        "Nombre(s) de pila": "Full Name(s)",
        "Número de cédula de miembro": "Membership Record Number",
        "Nombre del templo": "Temple Name",
        "Fecha de la cita del templo": "Temple Appointment Date",
        "Fecha de viaje al templo": "Date traveling to the temple",
        "Fecha de regreso del templo": "Date traveling home from the temple",
        "Costos asociados": "Associated Costs",
        "Nombre y número de unidad del barrio / rama": (
            "Ward/Branch Name and Unit Number"
        ),
        "Nombre y número de unidad de la estaca / distrito": (
            "Stake/District Name and Unit Number"
        ),
    }
    return [
        item._replace(texto=traduccion.get(item.texto, item.texto))
        for item in pagina_espanola()
    ]


class PruebaDeLaNormalizacionParaComparar(unittest.TestCase):
    """Las tildes se van SOLO para comparar. El valor guardado las conserva."""

    def test_quita_las_tildes(self):
        self.assertEqual(
            normalizar_para_comparar("Número de cédula"), "numero de cedula"
        )

    def test_junta_los_espacios_de_sobra(self):
        self.assertEqual(normalizar_para_comparar("  Nombre   del  templo "), "nombre del templo")

    def test_una_etiqueta_leida_sin_tildes_se_parece_del_todo(self):
        """Medido: sin normalizar da 0.926, y una segunda falta del OCR la hunde."""
        self.assertEqual(
            parecido("Número de cédula de miembro", "Numero de cedula de miembro"),
            1.0,
        )


class PruebaDeLasFamiliasDeEtiquetas(unittest.TestCase):
    def test_cada_campo_tiene_las_dos_formas(self):
        for campo in CAMPOS_DEL_FORMULARIO:
            with self.subTest(campo=campo):
                self.assertEqual(len(formas_de(campo)), 2, "falta un idioma")

    def test_ninguna_forma_espanola_se_parece_a_una_inglesa(self):
        """La razon por la que los dos idiomas pueden convivir sin separarlos.

        Medido el 2026-09-03: el maximo de todos los cruces es 0.357, muy por
        debajo del 0.85 que exige el ancla. Si esta prueba se pone roja, alguien
        metio una etiqueta que SI se confunde entre idiomas, y entonces hace
        falta separar los dos juegos en vez de mezclarlos.
        """
        peor = 0.0
        for campo in CAMPOS_DEL_FORMULARIO:
            en_ingles, en_espanol = formas_de(campo)
            for otro in CAMPOS_DEL_FORMULARIO:
                for forma_espanola in (formas_de(otro)[1],):
                    peor = max(peor, parecido(en_ingles, forma_espanola))
        self.assertLess(peor, PARECIDO_MINIMO_DEL_ANCLA)
        self.assertLess(peor, 0.60, f"el maximo cruce entre idiomas subio a {peor:.3f}")


class PruebaDelAnclaEnLosDosIdiomas(unittest.TestCase):
    def _rivales_de(self, campo):
        return tuple(otro for otro in CAMPOS_DEL_FORMULARIO if otro != campo)

    def _localizar(self, lineas, campo):
        return localizar_ancla(
            lineas,
            formas_de(campo),
            tuple(formas_de(rival) for rival in self._rivales_de(campo)),
        )

    def test_encuentra_la_etiqueta_espanola(self):
        encontrada = self._localizar(pagina_espanola(), CAMPO_DE_LA_FECHA_DE_VIAJE)
        self.assertIsNotNone(encontrada)
        self.assertEqual(encontrada.texto, "Fecha de viaje al templo")

    def test_sigue_encontrando_la_etiqueta_inglesa(self):
        encontrada = self._localizar(pagina_inglesa(), CAMPO_DE_LA_FECHA_DE_VIAJE)
        self.assertIsNotNone(encontrada)
        self.assertEqual(encontrada.texto, "Date traveling to the temple")

    def test_la_unidad_del_barrio_no_es_la_de_la_estaca(self):
        """0.694 de parecido entre las dos. Sin la regla rival, se confunden."""
        encontrada = self._localizar(pagina_espanola(), CAMPO_DE_LA_UNIDAD)
        self.assertEqual(encontrada.texto, "Nombre y número de unidad del barrio / rama")
        otra = self._localizar(pagina_espanola(), CAMPO_DE_LA_ESTACA)
        self.assertEqual(otra.texto, "Nombre y número de unidad de la estaca / distrito")

    def test_sin_la_etiqueta_de_ida_no_se_toma_la_de_vuelta(self):
        """El error que manda a alguien al templo el dia equivocado.

        Medido el 2026-09-03: «Fecha de regreso del templo» se parece un 0.704 a
        «Fecha de viaje al templo» por Levenshtein y un 0.745 por difflib. Con el
        umbral 0.60 que usa el proyecto antiguo, esta linea se ACEPTA como fecha
        de ida. Con 0.85 y la regla rival, no.
        """
        solo_la_vuelta = [
            item for item in pagina_espanola() if item.texto != "Fecha de viaje al templo"
        ]
        self.assertIsNone(self._localizar(solo_la_vuelta, CAMPO_DE_LA_FECHA_DE_VIAJE))

    def test_sin_la_etiqueta_de_ida_tampoco_se_toma_la_de_la_cita(self):
        """0.667 de parecido. Es la otra fecha que se le puede colar."""
        sin_ida_ni_vuelta = [
            item
            for item in pagina_espanola()
            if item.texto not in ("Fecha de viaje al templo", "Fecha de regreso del templo")
        ]
        self.assertIsNone(self._localizar(sin_ida_ni_vuelta, CAMPO_DE_LA_FECHA_DE_VIAJE))

    def test_una_pagina_inglesa_no_activa_ningun_ancla_espanola(self):
        """Cada campo se localiza una sola vez, y en el idioma de la pagina."""
        anclas = localizar_las_anclas(pagina_inglesa())
        for campo, ancla in anclas.items():
            if ancla is not None:
                with self.subTest(campo=campo):
                    self.assertIn(ancla.texto, formas_de(campo))


class PruebaDeLaPaginaEspanolaCompleta(unittest.TestCase):
    """El criterio de cierre: una pagina espanola entrega sus campos."""

    def setUp(self):
        self.lineas = pagina_espanola()
        self.anclas = localizar_las_anclas(self.lineas)

    def test_se_localizan_todas_las_anclas(self):
        sin_encontrar = [campo for campo, ancla in self.anclas.items() if ancla is None]
        self.assertEqual(sin_encontrar, [])

    def test_salen_los_campos_del_caso(self):
        campos = campos_del_caso(self.lineas, [], [], self.anclas)
        self.assertEqual(campos.numero_caso.valor, "BARC1234")
        self.assertEqual(campos.fecha_viaje.valor, "2026-10-15")
        self.assertEqual(campos.unidad_numero.valor, "123456")
        self.assertEqual(campos.unidad_nombre.valor, "Rama Ejemplo")

    def test_salen_las_dos_personas(self):
        personas, descartadas = extraer_personas(
            self.lineas,
            self.anclas[CAMPO_DE_LOS_NOMBRES].rectangulo,
            self.anclas[CAMPO_DEL_MRN].rectangulo,
            tuple(
                self.anclas[campo].rectangulo
                for campo in (CAMPO_DE_LA_CITA_DEL_TEMPLO,)
                if self.anclas[campo] is not None
            ),
        )
        self.assertEqual(len(personas), 2)
        self.assertEqual([persona.nombre.valor for persona in personas], ["Ana Rivas", "Luis Rivas"])
        self.assertEqual(
            [persona.mrn.valor for persona in personas], ["111-2222-3333", "111-2222-4444"]
        )
        self.assertEqual(descartadas, 0)

    def test_la_fecha_de_regreso_no_se_guarda_como_la_de_ida(self):
        campos = campos_del_caso(self.lineas, [], [], self.anclas)
        self.assertNotEqual(campos.fecha_viaje.valor, "2026-10-22")


def _campo_leido(valor, confianza):
    return CampoExtraido(
        valor=valor,
        origen=ORIGEN_OCR,
        confianza=confianza,
        valor_ocr=valor,
        anulado_por_tachon=False,
        necesita_revision=False,
    )


class PruebaDelNumeroDeCasoEnUnaPaginaIlegible(unittest.TestCase):
    """Una pagina que se manda a captura manual NO pierde su numero de caso.

    Es lo que el dueno vio como «se guardaron 0 casos»: el OCR leia «BARC1234»
    con confianza 1.00, la pagina se marcaba ilegible porque los demas campos no
    se localizaban, y el vaciado se llevaba tambien el numero. Sin numero, la
    pagina no se puede archivar en ningun caso y desaparece.

    El numero de caso es distinto del resto y por eso se trata distinto: no sale
    de una banda que dependa de un ancla, sale de un PATRON de cuatro letras
    mayusculas y cuatro digitos buscado en la pagina entera. Ese patron no
    aparece por casualidad en el ruido de un escaneo malo, asi que un numero
    leido con confianza suficiente sigue valiendo aunque la pagina sea ilegible.
    """

    def _campos(self, numero_caso):
        return CamposDelCaso(
            numero_caso=numero_caso,
            fecha_viaje=_campo_leido("2026-10-15", 0.30),
            unidad_numero=_campo_leido("123456", 0.20),
            unidad_nombre=_campo_leido("Rama Ejemplo", 0.20),
            bandas={},
        )

    def test_se_conserva_el_numero_leido_con_confianza_alta(self):
        vaciados = vaciar_el_formulario(self._campos(_campo_leido("BARC1234", 1.00)))
        self.assertEqual(vaciados.numero_caso.valor, "BARC1234")
        self.assertEqual(vaciados.numero_caso.origen, ORIGEN_OCR)

    def test_los_demas_campos_si_se_vacian(self):
        vaciados = vaciar_el_formulario(self._campos(_campo_leido("BARC1234", 1.00)))
        for campo in (vaciados.fecha_viaje, vaciados.unidad_numero, vaciados.unidad_nombre):
            with self.subTest(campo=campo):
                self.assertIsNone(campo.valor)
                self.assertEqual(campo.origen, ORIGEN_VACIO)
                self.assertTrue(campo.necesita_revision)

    def test_se_conserva_lo_que_el_ocr_habia_leido_en_los_vaciados(self):
        vaciados = vaciar_el_formulario(self._campos(_campo_leido("BARC1234", 1.00)))
        self.assertEqual(vaciados.fecha_viaje.valor_ocr, "2026-10-15")

    def test_un_numero_de_confianza_baja_si_se_vacia(self):
        """La excepcion no es «el numero siempre se salva», es «si es de fiar»."""
        vaciados = vaciar_el_formulario(self._campos(_campo_leido("BARC1234", 0.31)))
        self.assertIsNone(vaciados.numero_caso.valor)
        self.assertEqual(vaciados.numero_caso.valor_ocr, "BARC1234")

    def test_un_numero_que_no_se_leyo_sigue_vacio(self):
        sin_numero = CampoExtraido(
            valor=None,
            origen=ORIGEN_VACIO,
            confianza=None,
            valor_ocr=None,
            anulado_por_tachon=False,
            necesita_revision=True,
        )
        vaciados = vaciar_el_formulario(self._campos(sin_numero))
        self.assertIsNone(vaciados.numero_caso.valor)


class PruebaDeQueLasFormasNoSeSolapan(unittest.TestCase):
    """Ningun campo puede compartir una forma con otro: seria ambiguo."""

    def test_cada_forma_pertenece_a_un_solo_campo(self):
        vistas = {}
        for campo, formas in FORMAS_DE_CADA_CAMPO.items():
            for forma in formas:
                self.assertNotIn(forma, vistas, f"«{forma}» esta en {campo} y en {vistas.get(forma)}")
                vistas[forma] = campo


if __name__ == "__main__":
    unittest.main()
