"""La precedencia de valores y los cuatro casos que la definen.

Estas pruebas se escribieron desde el criterio de aceptacion —«tachon anula el
OCR, correccion escrita gana, si no hay ninguno vale el OCR, y si hubo tachon sin
correccion el campo queda vacio»— y no mirando el codigo. Los datos son
inventados: ni un nombre ni un MRN de los formularios reales entra aqui.
"""

import unittest
from collections import namedtuple

from extraccion.campos import (
    CONFIANZA_DE_UNA_ANOTACION,
    ORIGEN_ANOTACION,
    ORIGEN_OCR,
    ORIGEN_VACIO,
    resolver_campo,
)
from extraccion.geometria import Rectangulo
from extraccion.normalizacion import normalizar_fecha, normalizar_mrn, normalizar_unidad

LineaFalsa = namedtuple("LineaFalsa", ("texto", "rectangulo", "confianza"))
CorreccionFalsa = namedtuple("CorreccionFalsa", ("texto", "rectangulo_pdf"))


def linea(texto, confianza=0.95, x0=100.0):
    return LineaFalsa(texto, Rectangulo(x0, 1000.0, x0 + 200.0, 1050.0), confianza)


def correccion(texto, x0=100.0):
    return CorreccionFalsa(texto, (x0, 400.0, x0 + 50.0, 420.0))


class PruebaDeLaPrecedencia(unittest.TestCase):
    """Los cuatro caminos del arbol, uno por uno."""

    def test_sin_tachon_y_sin_correccion_vale_el_ocr(self):
        campo = resolver_campo(
            [linea("14 March 2027", confianza=0.91)], [], hay_tachon=False, normalizar=normalizar_fecha
        )
        self.assertEqual(campo.valor, "2027-03-14")
        self.assertEqual(campo.origen, ORIGEN_OCR)
        self.assertEqual(campo.confianza, 0.91)
        self.assertFalse(campo.necesita_revision)

    def test_una_correccion_escrita_gana_al_ocr_con_confianza_uno(self):
        """El caso que prueba la fase entera, en su forma minima."""
        campo = resolver_campo(
            [linea("March 13, 2027", confianza=0.99)],
            [correccion("14 March 2027")],
            hay_tachon=True,
            normalizar=normalizar_fecha,
        )
        self.assertEqual(campo.valor, "2027-03-14")
        self.assertEqual(campo.origen, ORIGEN_ANOTACION)
        self.assertEqual(campo.confianza, CONFIANZA_DE_UNA_ANOTACION)
        self.assertTrue(campo.anulado_por_tachon)
        # Lo que leyo el OCR se conserva aunque no se use: Miguel tiene que poder
        # ver que decia el papel antes de la correccion.
        self.assertEqual(campo.valor_ocr, "March 13, 2027")

    def test_la_correccion_gana_aunque_no_haya_habido_tachon(self):
        campo = resolver_campo(
            [linea("March 13, 2027")],
            [correccion("14 March 2027")],
            hay_tachon=False,
            normalizar=normalizar_fecha,
        )
        self.assertEqual(campo.valor, "2027-03-14")
        self.assertEqual(campo.origen, ORIGEN_ANOTACION)

    def test_con_tachon_y_sin_correccion_el_campo_queda_vacio(self):
        """El punto que no se negocia: el valor tachado NO se recupera.

        Alguien lo tacho porque estaba mal. Aprovecharlo seria guardar justo el
        dato que una persona marco como equivocado.
        """
        campo = resolver_campo(
            [linea("March 13, 2027", confianza=0.99)], [], hay_tachon=True, normalizar=normalizar_fecha
        )
        self.assertIsNone(campo.valor)
        self.assertEqual(campo.origen, ORIGEN_VACIO)
        self.assertIsNone(campo.confianza)
        self.assertTrue(campo.anulado_por_tachon)
        self.assertTrue(campo.necesita_revision)
        self.assertEqual(campo.valor_ocr, "March 13, 2027")

    def test_sin_nada_en_la_banda_el_campo_queda_vacio_y_marcado(self):
        campo = resolver_campo([], [], hay_tachon=False, normalizar=normalizar_fecha)
        self.assertIsNone(campo.valor)
        self.assertEqual(campo.origen, ORIGEN_VACIO)
        self.assertTrue(campo.necesita_revision)
        self.assertFalse(campo.anulado_por_tachon)

    def test_texto_que_no_tiene_la_forma_esperada_no_se_fuerza(self):
        """«Wcencegb zcench:» es OCR de verdad, no un caso inventado de laboratorio."""
        campo = resolver_campo(
            [linea("Wcencegb zcench:")], [], hay_tachon=False, normalizar=normalizar_fecha
        )
        self.assertIsNone(campo.valor)
        self.assertEqual(campo.origen, ORIGEN_VACIO)
        self.assertTrue(campo.necesita_revision)
        self.assertEqual(campo.valor_ocr, "Wcencegb zcench:")

    def test_una_correccion_ilegible_no_se_da_por_buena(self):
        campo = resolver_campo(
            [linea("14 March 2027")], [correccion("???")], hay_tachon=True, normalizar=normalizar_fecha
        )
        self.assertIsNone(campo.valor)
        self.assertTrue(campo.necesita_revision)

    def test_la_confianza_del_ocr_es_la_de_la_linea_peor(self):
        """Una banda vale lo que su lectura mas floja, no lo que su media."""
        campo = resolver_campo(
            [linea("September", confianza=0.97, x0=100.0), linea("14, 2027", confianza=0.62, x0=350.0)],
            [],
            hay_tachon=False,
            normalizar=normalizar_fecha,
        )
        self.assertEqual(campo.valor, "2027-09-14")
        self.assertEqual(campo.confianza, 0.62)

    def test_con_dos_correcciones_gana_la_de_mas_a_la_izquierda(self):
        campo = resolver_campo(
            [],
            [correccion("2 April 2027", x0=300.0), correccion("1 April 2027", x0=100.0)],
            hay_tachon=False,
            normalizar=normalizar_fecha,
        )
        self.assertEqual(campo.valor, "2027-04-01")

    def test_sin_normalizador_el_texto_pasa_tal_cual(self):
        campo = resolver_campo([linea("Castries Branch")], [], hay_tachon=False)
        self.assertEqual(campo.valor, "Castries Branch")
        self.assertEqual(campo.origen, ORIGEN_OCR)


class PruebaDeLaNormalizacion(unittest.TestCase):
    """Los patrones, y sobre todo lo que se NIEGAN a interpretar."""

    def test_las_formas_de_fecha_que_traen_estos_formularios(self):
        casos = {
            "14 March 2027": "2027-03-14",
            "March 14, 2027": "2027-03-14",
            "Mar-14, 2027": "2027-03-14",
            "March 14th, 2027": "2027-03-14",
            "14th March 2027": "2027-03-14",
            "Sept 1, 2027": "2027-09-01",
            "1 Sept 2027": "2027-09-01",
        }
        for texto, esperado in casos.items():
            with self.subTest(texto=texto):
                self.assertEqual(normalizar_fecha(texto), esperado)

    def test_las_formas_de_fecha_del_formulario_espanol(self):
        """Los formularios espanoles escriben la fecha en numeros, no en letras.

        Medido el 2026-09-03 sobre los dos PDF espanoles del dueno: las siete
        fechas que traen entre los dos son todas `dd-mm-aaaa`. Sin esto, las
        anclas espanolas encuentran la fila y la fecha sigue saliendo vacia.
        """
        casos = {
            "14-03-2027": "2027-03-14",
            "14/03/2027": "2027-03-14",
            "14.03.2027": "2027-03-14",
            "2027-03-14": "2027-03-14",
            "14 de marzo de 2027": "2027-03-14",
            "14 marzo 2027": "2027-03-14",
        }
        for texto, esperado in casos.items():
            with self.subTest(texto=texto):
                self.assertEqual(normalizar_fecha(texto), esperado)

    def test_una_fecha_numerica_ambigua_no_se_adivina(self):
        """«05-10-2027» puede ser el 5 de octubre o el 10 de mayo. No se elige.

        Regla permanente 1 llevada a su caso mas incomodo: aqui SE PODRIA
        acertar el 70% de las veces suponiendo dia-mes, que es lo que usa el
        pais del dueno. El 30% restante mueve una fecha de viaje varios meses,
        y esa es justo la fecha que decide si alguien sale avisado a tiempo.
        Se devuelve None, el campo va a revision, y Miguel la teclea mirando la
        hoja. **Es una decision que le corresponde al dueno, no al programa.**
        """
        for texto in ("05-10-2027", "05/10/2027", "12-11-2027"):
            with self.subTest(texto=texto):
                self.assertIsNone(normalizar_fecha(texto))

    def test_una_fecha_numerica_imposible_no_se_acerca_a_la_de_al_lado(self):
        for texto in ("31-02-2027", "45-03-2027", "14-13-2027"):
            with self.subTest(texto=texto):
                self.assertIsNone(normalizar_fecha(texto))

    def test_un_mrn_no_se_lee_como_fecha(self):
        """«066-2222-1330» tiene la forma numero-numero-numero y NO es una fecha.

        Es el riesgo concreto de admitir fechas numericas: la columna de cedulas
        esta llena de cadenas con dos guiones. Ninguna puede colarse.
        """
        for texto in ("066-2222-1330", "111-2222-3333", "123-4567-8901"):
            with self.subTest(texto=texto):
                self.assertIsNone(normalizar_fecha(texto))

    def test_un_rango_de_fechas_no_es_una_fecha_de_viaje(self):
        """«March 8-11» no dice cuando se viaja. Elegir uno seria inventarlo."""
        for texto in ("March 8-11, 2027", "March 8th till 11th, 2027", "March 8 to 11, 2027"):
            with self.subTest(texto=texto):
                self.assertIsNone(normalizar_fecha(texto))

    def test_una_fecha_que_no_existe_no_se_acerca_a_la_de_al_lado(self):
        self.assertIsNone(normalizar_fecha("February 31, 2027"))

    def test_texto_sin_fecha_devuelve_nada(self):
        for texto in ("", None, "Verified for translator", "Wcencegb zcench:"):
            with self.subTest(texto=texto):
                self.assertIsNone(normalizar_fecha(texto))

    def test_un_mrn_terminado_en_letra_se_lee_entero(self):
        """⚠️ Esta prueba afirmaba lo contrario hasta el 2026-09-04.

        Decia que `066-2222-133A` NO era un MRN, y por eso el campo se quedaba
        vacio. Se recorto del PDF la banda de la cedula en dos escaneos reales del
        dueno y se miro la imagen: el papel dice `133A`. Palabras del dueno:
        «Muchas cedulas de miembro tienen una A u otra letra al final».

        Lo que la prueba vieja defendia de verdad —no cambiar una letra por un
        digito «parecido»— sigue vivo y esta justo debajo: la letra se lee tal
        cual, no se sustituye y no se recorta.
        """
        self.assertEqual("066-2222-133A", normalizar_mrn("066-2222-133A"))

    def test_una_letra_en_medio_del_mrn_sigue_sin_ser_un_mrn(self):
        """La letra solo vale en el ultimo caracter. En cualquier otro sitio, no."""
        for texto in ("066-222A-1330", "06A-2222-1330", "066-2222-1A30"):
            with self.subTest(texto=texto):
                self.assertIsNone(normalizar_mrn(texto))

    def test_un_mrn_bien_formado_se_lee_aunque_lleve_texto_pegado(self):
        self.assertEqual(normalizar_mrn("123-4567-8901 verified for sealing"), "123-4567-8901")

    def test_un_mrn_sin_guiones_no_se_le_ponen(self):
        self.assertIsNone(normalizar_mrn("12345678901"))

    def test_una_unidad_de_seis_digitos_se_separa_de_su_nombre(self):
        numero, nombre = normalizar_unidad("Ejemplo Branch - 123456")
        self.assertEqual(numero, "123456")
        self.assertEqual(nombre, "Ejemplo Branch")

    def test_una_unidad_de_siete_digitos_se_conserva_entera(self):
        """`DECISIONES.md` (2026-09-02): «`unidad_numero` admite 6 o 7 digitos».

        Manda el papel. Hay formularios reales de siete digitos que el OCR lee
        bien y que `validar_unidad_numero` y el `CHECK` de `casos` aceptan. Si el
        normalizador los tira, el campo sale vacio y Miguel teclea a mano un dato
        que el sistema ya habia leido.

        No se recorta el septimo digito: se conserva el numero entero.
        """
        numero, nombre = normalizar_unidad("Ejemplo Branch - 1234567")
        self.assertEqual(numero, "1234567")
        self.assertEqual(nombre, "Ejemplo Branch")

    def test_una_unidad_de_ocho_digitos_no_se_recorta(self):
        """Ocho digitos no es un numero de unidad y no se convierte en uno.

        Recortar el octavo daria un numero que no existe: es exactamente lo que
        prohibe la regla permanente 1. Vuelve None y el texto crudo viaja aparte
        para que se vea que SI habia algo.
        """
        numero, nombre = normalizar_unidad("Ejemplo Branch - 12345678")
        self.assertIsNone(numero)
        self.assertEqual(nombre, "Ejemplo Branch")

    def test_una_unidad_de_cinco_digitos_no_se_rellena(self):
        """Cinco digitos tampoco: ni se rellena con ceros ni se da por bueno."""
        numero, nombre = normalizar_unidad("Ejemplo Branch - 12345")
        self.assertIsNone(numero)
        self.assertEqual(nombre, "Ejemplo Branch - 12345")

    def test_una_unidad_solo_con_numero_no_inventa_nombre(self):
        numero, nombre = normalizar_unidad("123456")
        self.assertEqual(numero, "123456")
        self.assertIsNone(nombre)


if __name__ == "__main__":
    unittest.main()
