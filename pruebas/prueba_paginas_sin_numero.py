"""Una pagina leida no se pierde nunca, aunque no se le pueda leer el numero.

El criterio del que salen estas pruebas es el del pase del 2026-09-02, escrito
antes que el codigo y en la forma en que se puede comprobar:

    DADO un formulario del que se leyeron personas pero NO el numero de caso,
    CUANDO se importa,
    ENTONCES el caso queda guardado con esas personas, sin numero inventado,
             con su renglon en la lista de lo que no entro entero,
             y el numero se puede teclear despues.

El fallo que lo motiva esta medido en la maquina del dueno: importo un PDF suyo y
el programa contesto «Se guardaron 0 casos de 1 página, con 0 personas en total».
La pagina se habia leido entera y se tiraba con todo dentro.

⚠️ Ninguna prueba de este archivo toca la base real ni ningun PDF real: los
formularios se construyen a mano con valores inventados, y la base es temporal.
"""

import sqlite3
import unittest

from datos.ilegibles import (
    ENTRO_COMO_DUPLICADO,
    SIN_CAMPOS,
    SIN_NUMERO_DE_CASO,
    SIN_TEXTO,
    contar_por_motivo,
    documentos_ilegibles,
)
from datos.repositorio import (
    asignar_numero_de_caso,
    leer_caso_por_id,
    leer_caso_por_numero,
    leer_personas_del_caso,
)
from datos.validacion import ErrorDeValidacion, validar_numero_caso_si_lo_hay
from extraccion.campos import campo_vacio
from importacion.diagnostico import diagnosticar_la_pagina, hay_algun_dato
from importacion.documento import (
    casos_pendientes_de_identificar,
    resumen_de_la_importacion,
)
from importacion.guardado import guardar_formulario, guardar_las_paginas_del_documento
from pruebas.comun import PruebaConBaseTemporal
from pruebas.prueba_importacion import _campo, _formulario, _persona


def _sin_numero(**resto):
    """Un formulario igual que el de siempre pero sin numero de caso leido."""
    return _formulario(**resto)._replace(numero_caso=campo_vacio())


def _vacio_del_todo(lineas_leidas=0, texto=None):
    """Una pagina de la que no salio absolutamente nada."""
    return _formulario(personas=[])._replace(
        numero_caso=campo_vacio(),
        fecha_viaje=campo_vacio(),
        unidad_numero=campo_vacio(),
        unidad_nombre=campo_vacio(),
        lineas_leidas=lineas_leidas,
        texto_leido=texto,
    )


class LaPaginaSinNumeroSeGuarda(PruebaConBaseTemporal):
    """Criterio 1 del pase: una pagina nunca se pierde."""

    def test_el_caso_entra_con_el_numero_a_nulo_y_no_inventado(self):
        guardar_formulario(self.conexion, _sin_numero())
        fila = self.conexion.execute("SELECT id, numero_caso FROM casos").fetchone()
        self.assertIsNotNone(fila)
        self.assertIsNone(fila["numero_caso"])

    def test_las_personas_de_esa_pagina_entran_con_ella(self):
        """Lo que se salva no es la fila del caso: son los nombres y los MRN."""
        resultado = guardar_formulario(
            self.conexion,
            _sin_numero(personas=[
                _persona(1, "ANONIMO, M", "055-1111-3853"),
                _persona(2, "PEREZ, A", "055-1111-3854"),
            ]),
        )
        self.assertEqual(resultado.personas, 2)
        personas = leer_personas_del_caso(self.conexion, resultado.caso_id)
        self.assertEqual([p["nombre"] for p in personas], ["ANONIMO, M", "PEREZ, A"])

    def test_la_procedencia_de_los_cuatro_campos_se_guarda_igual(self):
        """Sin procedencia el caso no aparece en la cola de pendientes."""
        resultado = guardar_formulario(self.conexion, _sin_numero())
        cuantos = self.conexion.execute(
            "SELECT COUNT(*) FROM procedencia_campo WHERE tabla = 'casos' AND registro_id = ?",
            (resultado.caso_id,),
        ).fetchone()[0]
        self.assertEqual(cuantos, 4)

    def test_dos_paginas_sin_numero_son_dos_casos_y_no_uno(self):
        """«No se sabe» no es un numero comun: no unen entre si.

        Si el None entrara en el diccionario de la tanda, dos formularios de dos
        familias distintas quedarian como un caso con las personas revueltas.
        """
        guardar_las_paginas_del_documento(
            self.conexion, [_sin_numero(pagina=1), _sin_numero(pagina=2)]
        )
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0], 2
        )

    def test_el_motor_ya_NO_rechaza_un_numero_repetido(self):
        """El `UNIQUE` se quito en la version 12: dos casos pueden repetir numero.

        Es la vuelta de la prueba que habia aqui hasta el 2026-09-03, y el criterio
        lo puso el dueno con una medicion: con `UNIQUE`, seis de diez documentos de
        su carpeta real se rechazaban enteros porque el numero de caso son cuatro
        letras mas el ano y el mes —una unidad y un mes, no una familia—.
        """
        guardar_formulario(self.conexion, _formulario())
        self.conexion.execute(
            "INSERT INTO casos (numero_caso, creado_en) VALUES ('CASP2609', '2026-09-02 10:00:00')"
        )
        self.assertEqual(
            self.conexion.execute(
                "SELECT COUNT(*) FROM casos WHERE numero_caso = 'CASP2609'"
            ).fetchone()[0],
            2,
        )

    def test_el_motor_sigue_rechazando_un_numero_con_mala_forma(self):
        """El `CHECK` tampoco se aflojo: lo unico que se admite ahora es el nulo."""
        with self.assertRaises(sqlite3.IntegrityError):
            self.conexion.execute(
                "INSERT INTO casos (numero_caso, creado_en) VALUES ('xx', '2026-09-02 10:00:00')"
            )


class ElNumeroSeTecleaDespues(PruebaConBaseTemporal):
    """Criterio 1, segunda mitad: «se puede completar a mano después»."""

    def test_asignar_el_numero_lo_escribe_en_el_caso(self):
        caso_id = guardar_formulario(self.conexion, _sin_numero()).caso_id
        asignar_numero_de_caso(self.conexion, caso_id, "CASP2609")
        self.assertEqual(leer_caso_por_id(self.conexion, caso_id)["numero_caso"], "CASP2609")

    def test_despues_de_teclearlo_el_caso_se_encuentra_por_su_numero(self):
        """Es lo que devuelve al caso al par que reconcilia el Excel del companero."""
        caso_id = guardar_formulario(self.conexion, _sin_numero()).caso_id
        asignar_numero_de_caso(self.conexion, caso_id, "CASP2609")
        self.assertEqual(leer_caso_por_numero(self.conexion, "CASP2609")["id"], caso_id)

    def test_un_numero_con_mala_forma_no_entra(self):
        caso_id = guardar_formulario(self.conexion, _sin_numero()).caso_id
        with self.assertRaises(ErrorDeValidacion):
            asignar_numero_de_caso(self.conexion, caso_id, "casp26")

    def test_un_caso_que_YA_tiene_numero_no_se_puede_cambiar_por_aqui(self):
        """La puerta abre en un solo sentido, y eso es lo que defiende.

        Cambiar el numero de un caso que ya lo tenia rompe el par
        `numero_caso` + `mrn` sin que nadie se entere: el Excel que el companero
        tiene delante sigue diciendo el numero viejo.
        """
        caso_id = guardar_formulario(self.conexion, _formulario()).caso_id
        with self.assertRaises(ErrorDeValidacion):
            asignar_numero_de_caso(self.conexion, caso_id, "CASD2609")

    def test_el_validador_opcional_admite_el_nulo_y_nada_mas(self):
        self.assertIsNone(validar_numero_caso_si_lo_hay(None))
        self.assertEqual(validar_numero_caso_si_lo_hay("CASP2609"), "CASP2609")
        with self.assertRaises(ErrorDeValidacion):
            validar_numero_caso_si_lo_hay("")


class ElRenglonDeLoQueNoEntro(PruebaConBaseTemporal):
    """La ampliacion del dueno: «un renglón que notifique», y que se queda."""

    def test_la_pagina_sin_numero_deja_su_renglon_apuntando_al_caso(self):
        resultado = guardar_las_paginas_del_documento(self.conexion, [_sin_numero()])[0]
        filas = documentos_ilegibles(self.conexion)
        self.assertEqual(len(filas), 1)
        self.assertEqual(filas[0]["motivo"], SIN_NUMERO_DE_CASO)
        self.assertEqual(filas[0]["caso_id"], resultado.caso_id)
        self.assertEqual(filas[0]["pagina_pdf"], 1)

    def test_una_pagina_de_la_que_no_salio_nada_dice_que_no_habia_ni_letra(self):
        guardar_las_paginas_del_documento(self.conexion, [_vacio_del_todo(lineas_leidas=0)])
        self.assertEqual(documentos_ilegibles(self.conexion)[0]["motivo"], SIN_TEXTO)

    def test_una_pagina_con_letra_pero_sin_campos_se_distingue_de_la_anterior(self):
        """Es la distincion que el supervisor pidio: escaneo malo o formulario otro."""
        guardar_las_paginas_del_documento(
            self.conexion, [_vacio_del_todo(lineas_leidas=94, texto="Temple Recommend")]
        )
        fila = documentos_ilegibles(self.conexion)[0]
        self.assertEqual(fila["motivo"], SIN_CAMPOS)
        self.assertEqual(fila["lineas_leidas"], 94)
        self.assertIn("Temple Recommend", fila["detalle"])

    def test_el_detalle_dice_cuantas_lineas_leyo_el_lector(self):
        """Sin esa cifra, «no se pudo leer» no dice si el problema es el escaneo."""
        guardar_las_paginas_del_documento(
            self.conexion, [_vacio_del_todo(lineas_leidas=94, texto="hola")]
        )
        self.assertIn("94", documentos_ilegibles(self.conexion)[0]["detalle"])

    def test_una_pagina_que_salio_bien_no_deja_ningun_renglon(self):
        guardar_las_paginas_del_documento(self.conexion, [_formulario()])
        self.assertEqual(documentos_ilegibles(self.conexion), [])

    def test_el_duplicado_entra_y_deja_su_renglon_diciendo_de_cual_lo_es(self):
        """El duplicado ya no se rechaza, pero sigue dejando su renglón.

        El renglón es lo que sobrevive al cuadro de diálogo del resumen, que se
        cierra con Aceptar y no deja nada. Ahora dice que la página SÍ entró y de
        qué caso es duplicada.
        """
        primeras = guardar_las_paginas_del_documento(self.conexion, [_formulario()])
        guardar_las_paginas_del_documento(self.conexion, [_formulario(pagina=2)])
        filas = documentos_ilegibles(self.conexion)
        self.assertEqual(len(filas), 1)
        self.assertEqual(filas[0]["motivo"], ENTRO_COMO_DUPLICADO)
        self.assertEqual(filas[0]["pagina_pdf"], 2)
        self.assertIn(f"caso n.º {primeras[0].caso_id}", filas[0]["detalle"])

    def test_el_renglon_del_duplicado_dice_con_quien_choco(self):
        """Con los nombres de las dos se puede decidir si son la misma familia.

        ⚠️ Estas dos paginas NO comparten MRN —son otra familia— pero SI comparten
        ruta y pagina del PDF, que es la segunda via de deteccion: el mismo papel
        del mismo archivo otra vez. Por eso el renglon sale igual.
        """
        guardar_las_paginas_del_documento(
            self.conexion, [_formulario(personas=[_persona(1, "ANONIMO, M", "055-1111-3853")])]
        )
        guardar_las_paginas_del_documento(
            self.conexion,
            [_formulario(personas=[_persona(1, "EJEMPLO, D", "055-1111-3855")])],
        )
        detalle = documentos_ilegibles(self.conexion)[0]["detalle"]
        self.assertIn("ANONIMO, M", detalle)
        self.assertIn("EJEMPLO, D", detalle)

    def test_el_desglose_por_motivo_cuenta_cada_clase_por_separado(self):
        guardar_las_paginas_del_documento(
            self.conexion, [_sin_numero(pagina=1), _sin_numero(pagina=2), _vacio_del_todo()]
        )
        cuentas = contar_por_motivo(self.conexion)
        self.assertEqual(cuentas[SIN_NUMERO_DE_CASO], 2)
        self.assertEqual(cuentas[SIN_TEXTO], 1)


class ElDiagnosticoSeparaLasTresAverias(unittest.TestCase):
    """Sin base: es una decision sobre lo leido, no sobre lo guardado."""

    def test_una_pagina_con_personas_tiene_datos(self):
        self.assertTrue(hay_algun_dato(_sin_numero()))

    def test_una_pagina_sin_nada_no_tiene_datos(self):
        self.assertFalse(hay_algun_dato(_vacio_del_todo()))

    def test_una_pagina_completa_no_necesita_ningun_renglon(self):
        self.assertIsNone(diagnosticar_la_pagina(_formulario()))

    def test_sin_lineas_y_sin_datos_es_el_escaneo(self):
        self.assertEqual(diagnosticar_la_pagina(_vacio_del_todo(0)).motivo, SIN_TEXTO)

    def test_con_lineas_y_sin_datos_es_el_papel_o_las_reglas(self):
        self.assertEqual(diagnosticar_la_pagina(_vacio_del_todo(94)).motivo, SIN_CAMPOS)

    def test_una_pagina_que_solo_trae_la_fecha_ya_cuenta_como_con_datos(self):
        """Un campo bueno es trabajo que Miguel no tiene que rehacer."""
        formulario = _vacio_del_todo(94)._replace(fecha_viaje=_campo("2026-09-08"))
        self.assertEqual(diagnosticar_la_pagina(formulario).motivo, SIN_NUMERO_DE_CASO)

    def test_una_pagina_con_anotaciones_y_cero_lineas_de_ocr_no_es_sin_texto(self):
        """El PDF que trae los datos en su capa de anotaciones da 0 lineas de OCR.

        Llamar a eso «no tiene ni una letra» seria falso, y ademas mandaria a
        Miguel a volver a escanear una pagina que se leyo perfecta.
        """
        self.assertIsNone(diagnosticar_la_pagina(_formulario()._replace(lineas_leidas=0)))


class ElResumenCuentaLasQueQuedaronAMedias(PruebaConBaseTemporal):
    """Criterio 5 del pase, en el resumen de un documento."""

    class _Resultado:
        def __init__(self, paginas, por_pagina):
            self.paginas = paginas
            self.paginas_por_pagina = por_pagina

    def test_las_pendientes_se_cuentan_aparte_de_los_casos(self):
        paginas = guardar_las_paginas_del_documento(
            self.conexion, [_formulario(pagina=1), _sin_numero(pagina=2)]
        )
        resultado = self._Resultado(2, paginas)
        self.assertEqual(len(casos_pendientes_de_identificar(resultado)), 1)

    def test_el_texto_del_resumen_las_nombra(self):
        paginas = guardar_las_paginas_del_documento(self.conexion, [_sin_numero()])
        texto = resumen_de_la_importacion(self._Resultado(1, paginas))
        self.assertIn("PENDIENTES DE IDENTIFICAR", texto)
        self.assertNotIn("Caso None", texto)


if __name__ == "__main__":
    unittest.main()
