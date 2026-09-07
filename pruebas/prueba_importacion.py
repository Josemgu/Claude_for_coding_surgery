"""El puente que llevaba lo leido a la base, y que no existia.

Se prueba sin PDF y sin OCR: se construye un `FormularioExtraido` a mano con
valores inventados y se comprueba que al otro lado queda un caso con sus personas
y con la procedencia completa de cada campo. Es lo que hay que defender: **no se
guarda un valor sin guardar de donde salio**.
"""

import unittest

from extraccion.campos import CampoExtraido, campo_vacio
from extraccion.casillas import casillas_no_leidas
from extraccion.formulario import FormularioExtraido
from extraccion.personas import PersonaExtraida
from importacion.documento import resumen_de_la_importacion
from importacion.guardado import (
    CAMPOS_DEL_CASO,
    CAMPOS_DE_LA_PERSONA,
    guardar_formulario,
    guardar_las_paginas_del_documento,
)
from datos.procedencia import banda_de_la_procedencia, procedencia_por_campo
from datos.repositorio import leer_caso_por_numero, leer_personas_del_caso
from pruebas.comun import PruebaConBaseTemporal


def _campo(valor, origen="ocr", confianza=0.9, valor_ocr=None, tachon=False):
    return CampoExtraido(
        valor=valor,
        origen=origen,
        confianza=confianza,
        valor_ocr=valor_ocr if valor_ocr is not None else valor,
        anulado_por_tachon=tachon,
        necesita_revision=False,
    )


def _persona(fila, nombre, mrn):
    return PersonaExtraida(
        fila_formulario=fila,
        nombre=_campo(nombre, confianza=0.94),
        mrn=_campo(mrn, confianza=0.91) if mrn else campo_vacio(valor_ocr="05511113854"),
        banda=None,
    )


def _formulario(numero="CASP2609", pagina=1, personas=None, captura_manual=False):
    personas = personas if personas is not None else [_persona(1, "ANONIMO, M", "055-1111-3853")]
    return FormularioExtraido(
        ruta_pdf=r"C:\Escaneos\inventado.pdf",
        indice_de_pagina=pagina - 1,
        numero_caso=_campo(numero, origen="anotacion", confianza=1.0),
        fecha_viaje=_campo("2026-09-08", origen="anotacion", confianza=1.0,
                           valor_ocr="September 7, 2026"),
        unidad_numero=_campo("7000011", confianza=0.97, valor_ocr="Branch - 7000011"),
        unidad_nombre=_campo("Paramaribo Branch", confianza=0.58, valor_ocr="Branch - 7000011"),
        bandas={
            "numero_caso": None,
            "fecha_viaje": (0.06, 0.20, 0.62, 0.24),
            "unidad_numero": (0.06, 0.28, 0.62, 0.32),
            "unidad_nombre": (0.06, 0.28, 0.62, 0.32),
        },
        personas=personas,
        bandas_por_persona=[(0.06, 0.40, 0.90, 0.44)] * len(personas),
        casillas_por_persona=[casillas_no_leidas() for _ in personas],
        filas_vacias=6 - len(personas),
        filas_descartadas=0,
        captura_manual=captura_manual,
        anclas_no_encontradas=(),
        resumen_de_anotaciones={},
        tamano_de_la_imagen=(2550, 3300),
        segundos=1.0,
    )


class GuardarUnaPagina(PruebaConBaseTemporal):
    def test_la_persona_de_una_pagina_suelta_tambien_guarda_su_hoja(self):
        """Una hoja sola tambien deja la pagina en la persona, no solo en el caso."""
        guardar_formulario(self.conexion, _formulario(pagina=3))
        caso_id = leer_caso_por_numero(self.conexion, "CASP2609")["id"]
        self.assertEqual(
            leer_personas_del_caso(self.conexion, caso_id)[0]["pagina_pdf"], 3
        )

    def test_el_caso_entra_con_su_pagina_y_su_unidad(self):
        resultado = guardar_formulario(self.conexion, _formulario(pagina=3))
        self.assertTrue(resultado.importada)
        caso = leer_caso_por_numero(self.conexion, "CASP2609")
        self.assertEqual(caso["pagina_pdf"], 3)
        self.assertEqual(caso["unidad_nombre"], "Paramaribo Branch")
        self.assertEqual(caso["unidad_numero"], "7000011")

    def test_la_pagina_se_guarda_contando_desde_uno(self):
        """El extractor cuenta desde 0 y las personas desde 1. La resta va en un sitio."""
        resultado = guardar_formulario(self.conexion, _formulario(pagina=6))
        self.assertEqual(resultado.pagina_pdf, 6)
        self.assertEqual(leer_caso_por_numero(self.conexion, "CASP2609")["pagina_pdf"], 6)

    def test_cada_campo_del_caso_deja_su_procedencia(self):
        guardar_formulario(self.conexion, _formulario())
        caso_id = leer_caso_por_numero(self.conexion, "CASP2609")["id"]
        procedencia = procedencia_por_campo(self.conexion, "casos", caso_id)
        for campo in CAMPOS_DEL_CASO:
            self.assertIn(campo, procedencia, f"el campo {campo!r} entro sin procedencia")

    def test_la_banda_llega_hasta_la_base(self):
        """Es el bloqueador 1 comprobado de punta a punta."""
        guardar_formulario(self.conexion, _formulario())
        caso_id = leer_caso_por_numero(self.conexion, "CASP2609")["id"]
        procedencia = procedencia_por_campo(self.conexion, "casos", caso_id)
        self.assertEqual(
            banda_de_la_procedencia(procedencia["fecha_viaje"]), (0.06, 0.20, 0.62, 0.24)
        )
        self.assertIsNone(banda_de_la_procedencia(procedencia["numero_caso"]))

    def test_lo_que_el_lector_leyo_se_conserva_desde_el_primer_dia(self):
        guardar_formulario(self.conexion, _formulario())
        caso_id = leer_caso_por_numero(self.conexion, "CASP2609")["id"]
        procedencia = procedencia_por_campo(self.conexion, "casos", caso_id)
        self.assertEqual(procedencia["fecha_viaje"]["valor_ocr"], "September 7, 2026")

    def test_nada_nace_verificado(self):
        """Regla permanente 5. Es el criterio 3 de la FASE 3 en su origen."""
        guardar_formulario(self.conexion, _formulario())
        verificados = self.conexion.execute(
            "SELECT COUNT(*) FROM procedencia_campo WHERE verificado = 1"
        ).fetchone()[0]
        self.assertEqual(verificados, 0)

    def test_las_personas_entran_con_las_seis_casillas_sin_leer(self):
        guardar_formulario(
            self.conexion,
            _formulario(personas=[
                _persona(1, "ANONIMO, M", "055-1111-3853"),
                _persona(2, "PEREZ, A", None),
            ]),
        )
        caso_id = leer_caso_por_numero(self.conexion, "CASP2609")["id"]
        personas = leer_personas_del_caso(self.conexion, caso_id)
        self.assertEqual(len(personas), 2)
        self.assertIsNone(personas[0]["ord_traductor"])

    def test_una_persona_con_el_mrn_ilegible_entra_igual(self):
        """El MRN vacio es trabajo para la pantalla, no motivo para perder a alguien."""
        resultado = guardar_formulario(
            self.conexion, _formulario(personas=[_persona(1, "PEREZ, A", None)])
        )
        self.assertEqual(resultado.personas, 1)
        caso_id = leer_caso_por_numero(self.conexion, "CASP2609")["id"]
        self.assertIsNone(leer_personas_del_caso(self.conexion, caso_id)[0]["mrn"])


class LoQueNoSePuedeGuardar(PruebaConBaseTemporal):
    """⚠️ La primera prueba de esta clase decia lo CONTRARIO hasta el 2026-09-02.

    Decia «sin identidad no hay fila» y exigia `casos = 0`. El dueno lo midio con
    sus PDF y el programa contesto «Se guardaron 0 casos de 1 página, con 0
    personas en total»: la pagina se habia leido entera y se tiraba con todo
    dentro. El criterio se cambio —una pagina leida no se pierde nunca— y esta
    prueba se reescribio contra el criterio nuevo. Lo que NO cambio, y lo dice la
    prueba de abajo, es que el numero no se inventa: entra a NULL.
    """

    def test_sin_numero_de_caso_la_pagina_se_guarda_igual_con_todo_lo_leido(self):
        """Dado un formulario cuyo numero de caso no se pudo leer, cuando se guarda,
        entonces el caso queda en la base con sus personas y sin numero inventado."""
        formulario = _formulario()._replace(numero_caso=campo_vacio())
        resultado = guardar_formulario(self.conexion, formulario)
        self.assertTrue(resultado.importada)
        self.assertTrue(resultado.pendiente_de_identificar)
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0], 1
        )
        self.assertIsNone(
            self.conexion.execute("SELECT numero_caso FROM casos").fetchone()[0]
        )
        self.assertEqual(resultado.personas, 1)

    def test_el_mismo_caso_dos_veces_entra_marcado_y_no_pisa_lo_de_antes(self):
        """Dado un caso ya guardado, cuando se vuelve a guardar la misma pagina,
        entonces entra como caso APARTE marcado como duplicado y el primero no
        cambia.

        Criterio del dueno, 2026-09-03: «Si hay documentos duplicados debe decirlo y
        no rechazarlo». Antes de ese dia esta prueba exigia lo contrario —que el
        segundo se rechazara— y se reescribio contra el criterio nuevo, no contra el
        codigo.
        """
        primero = guardar_formulario(self.conexion, _formulario())
        antes = self.conexion.execute(
            "SELECT * FROM casos WHERE id = ?", (primero.caso_id,)
        ).fetchone()

        segundo = guardar_formulario(self.conexion, _formulario())

        self.assertTrue(segundo.importada)
        self.assertNotEqual(segundo.caso_id, primero.caso_id)
        self.assertEqual(segundo.duplicado_de, primero.caso_id)
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0], 2
        )
        # El caso que ya estaba no se toca: se compara la fila entera, columna por
        # columna, y no solo el campo que uno sospecha.
        despues = self.conexion.execute(
            "SELECT * FROM casos WHERE id = ?", (primero.caso_id,)
        ).fetchone()
        self.assertEqual(tuple(antes), tuple(despues))


class UnCasoRepartidoEnVariasPaginas(PruebaConBaseTemporal):
    """El fallo mas grave del proyecto: un PDF de grupo perdia 11 de 12 personas.

    Los criterios salen del pase, no del codigo: importar el documento de grupo
    tiene que dejar las DOCE personas, y volver a importar el mismo caso desde
    otro documento tiene que seguir rechazandose. Las dos situaciones conviven en
    la carpeta de referencia y el arreglo tiene que separarlas.
    """

    def _seis_paginas_del_mismo_caso(self, ruta=r"C:\Escaneos\grupo.pdf"):
        """Seis paginas del mismo numero de caso, con dos personas cada una."""
        return [
            _formulario(
                "SURB2609",
                pagina=numero,
                personas=[
                    _persona(1, f"UNO DE LA {numero}", f"055-1111-{3850 + numero * 2:04d}"),
                    _persona(2, f"DOS DE LA {numero}", f"055-1111-{3851 + numero * 2:04d}"),
                ],
            )._replace(ruta_pdf=ruta)
            for numero in range(1, 7)
        ]

    def test_las_seis_paginas_dejan_las_doce_personas_en_un_solo_caso(self):
        paginas = guardar_las_paginas_del_documento(
            self.conexion, self._seis_paginas_del_mismo_caso()
        )
        self.assertTrue(all(pagina.importada for pagina in paginas))
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0], 1
        )
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM personas").fetchone()[0], 12
        )

    def test_solo_la_primera_pagina_crea_el_caso(self):
        """Las otras cinco se unen: si crearan caso, el resumen diria «6 casos»."""
        paginas = guardar_las_paginas_del_documento(
            self.conexion, self._seis_paginas_del_mismo_caso()
        )
        self.assertEqual([pagina.caso_nuevo for pagina in paginas], [True] + [False] * 5)
        self.assertEqual(len({pagina.caso_id for pagina in paginas}), 1)

    def test_las_personas_unidas_se_numeran_seguidas_y_no_se_entremezclan(self):
        """Sin esto, `ORDER BY fila_formulario` intercala las seis paginas."""
        guardar_las_paginas_del_documento(
            self.conexion, self._seis_paginas_del_mismo_caso()
        )
        caso_id = leer_caso_por_numero(self.conexion, "SURB2609")["id"]
        personas = leer_personas_del_caso(self.conexion, caso_id)
        self.assertEqual([persona["fila_formulario"] for persona in personas], list(range(1, 13)))
        self.assertEqual(personas[0]["nombre"], "UNO DE LA 1")
        self.assertEqual(personas[-1]["nombre"], "DOS DE LA 6")

    def test_cada_persona_guarda_la_hoja_de_la_que_salio(self):
        """Sin esto, la tira de las once personas de las hojas 2 a 6 sale de la 1.

        El caso guarda UNA pagina —la que lo abrio—, asi que la pantalla de
        correccion no tiene de donde sacar la hoja de cada persona si la persona
        no la lleva encima.
        """
        guardar_las_paginas_del_documento(
            self.conexion, self._seis_paginas_del_mismo_caso()
        )
        caso_id = leer_caso_por_numero(self.conexion, "SURB2609")["id"]
        personas = leer_personas_del_caso(self.conexion, caso_id)
        self.assertEqual(
            [persona["pagina_pdf"] for persona in personas],
            [hoja for hoja in range(1, 7) for _ in range(2)],
        )

    def test_la_pagina_del_caso_sigue_siendo_la_de_la_hoja_que_lo_abrio(self):
        """Unir hojas no pisa los campos del caso, y eso no cambia con esto."""
        guardar_las_paginas_del_documento(
            self.conexion, self._seis_paginas_del_mismo_caso()
        )
        self.assertEqual(leer_caso_por_numero(self.conexion, "SURB2609")["pagina_pdf"], 1)

    def test_cada_persona_unida_deja_su_procedencia(self):
        """Unirse a un caso no puede ser un atajo que se salte la procedencia."""
        guardar_las_paginas_del_documento(
            self.conexion, self._seis_paginas_del_mismo_caso()
        )
        filas = self.conexion.execute(
            "SELECT COUNT(*) FROM procedencia_campo WHERE tabla = 'personas'"
        ).fetchone()[0]
        self.assertEqual(filas, 12 * len(CAMPOS_DE_LA_PERSONA))

    def test_el_mismo_caso_desde_OTRA_ruta_entra_entero_y_marcado(self):
        """Dado un grupo de seis hojas ya importado, cuando llega el mismo documento
        desde otra ruta, entonces entra entero como un caso aparte marcado como
        duplicado del primero."""
        primeras = guardar_las_paginas_del_documento(
            self.conexion, self._seis_paginas_del_mismo_caso()
        )
        segundas = guardar_las_paginas_del_documento(
            self.conexion, self._seis_paginas_del_mismo_caso(r"C:\Escaneos\copia.pdf")
        )
        self.assertTrue(all(pagina.importada for pagina in segundas))
        self.assertEqual(segundas[0].duplicado_de, primeras[0].caso_id)
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0], 2
        )
        # Las doce de la primera y las doce de la copia: nada se ha fundido.
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM personas").fetchone()[0], 24
        )

    def test_el_mismo_documento_importado_dos_veces_deja_dos_casos_y_lo_dice(self):
        """Unirse vale DENTRO de un documento; entre dos importaciones se marca.

        Las seis hojas de la segunda vuelta se unen entre ellas —son un documento—,
        asi que salen DOS casos de doce y no doce casos de uno.
        """
        primeras = guardar_las_paginas_del_documento(
            self.conexion, self._seis_paginas_del_mismo_caso()
        )
        segundas = guardar_las_paginas_del_documento(
            self.conexion, self._seis_paginas_del_mismo_caso()
        )
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0], 2
        )
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM personas").fetchone()[0], 24
        )
        self.assertEqual(segundas[0].duplicado_de, primeras[0].caso_id)
        self.assertEqual(
            self.conexion.execute(
                "SELECT COUNT(*) FROM personas WHERE caso_id = ?",
                (segundas[0].caso_id,),
            ).fetchone()[0],
            12,
        )

    def test_dos_casos_distintos_en_el_mismo_pdf_siguen_siendo_dos_casos(self):
        """Unir es por numero de caso, no por archivo: no se mezclan dos casos."""
        paginas = guardar_las_paginas_del_documento(
            self.conexion,
            [_formulario("CASP2609", pagina=1), _formulario("PARB2609", pagina=2)],
        )
        self.assertEqual([pagina.caso_nuevo for pagina in paginas], [True, True])
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0], 2
        )

    def test_una_persona_repetida_no_tumba_la_importacion_y_se_dice(self):
        """`UNIQUE (caso_id, mrn)` salta si el lector repite un MRN entre paginas.

        Antes subia como `IntegrityError` y se llevaba por delante el PDF entero.
        Ahora la fila se descarta, las demas entran, y el motivo se escribe.
        """
        repetida = [
            _formulario(
                "SURB2609",
                pagina=numero,
                personas=[_persona(1, f"FILA {numero}", "055-1111-3853")],
            )
            for numero in (1, 2)
        ]
        paginas = guardar_las_paginas_del_documento(self.conexion, repetida)
        self.assertTrue(all(pagina.importada for pagina in paginas))
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM personas").fetchone()[0], 1
        )
        self.assertTrue(any("ya estaba" in aviso for aviso in paginas[1].avisos))


class GuardarUnaPaginaSuelta(PruebaConBaseTemporal):
    """`guardar_formulario` a solas no une nada, pero tampoco rechaza nada."""

    def test_sin_el_contexto_del_documento_el_repetido_entra_marcado(self):
        primero = guardar_formulario(self.conexion, _formulario())
        segundo = guardar_formulario(self.conexion, _formulario(pagina=2))
        self.assertTrue(segundo.importada)
        self.assertEqual(segundo.duplicado_de, primero.caso_id)
        self.assertEqual(
            self.conexion.execute("SELECT COUNT(*) FROM casos").fetchone()[0], 2
        )


class ElResumenLlevaLasDosCifras(PruebaConBaseTemporal):
    """Un PDF de seis hojas que produce un caso es un fallo, y sin las dos no se ve."""

    class _Resultado:
        def __init__(self, paginas, por_pagina):
            self.ruta_pdf = "inventado.pdf"
            self.paginas = paginas
            self.paginas_por_pagina = por_pagina
            self.espejo = None

    def test_dice_cuantos_casos_de_cuantas_paginas(self):
        """Dos paginas sueltas del mismo numero son DOS casos: ninguna se rechaza."""
        primera = guardar_formulario(self.conexion, _formulario("CASP2609", pagina=1))
        segunda = guardar_formulario(
            self.conexion, _formulario("CASP2609", pagina=2)
        )
        texto = resumen_de_la_importacion(self._Resultado(6, [primera, segunda]))
        self.assertIn("2 casos de 6 páginas", texto)

    def test_cuenta_casos_distintos_y_no_paginas_importadas(self):
        """Seis paginas unidas son UN caso. Contar paginas diria «6 casos»."""
        paginas = guardar_las_paginas_del_documento(
            self.conexion,
            [
                _formulario("SURB2609", pagina=numero, personas=[
                    _persona(1, f"FILA {numero}", f"055-1111-{3850 + numero:04d}")
                ])
                for numero in range(1, 7)
            ],
        )
        texto = resumen_de_la_importacion(self._Resultado(6, paginas))
        self.assertIn("Se guardó 1 caso de 6 páginas, con 6 personas en total.", texto)

    def test_el_duplicado_entra_y_el_resumen_dice_de_cual_lo_es(self):
        """Dado un caso repetido, cuando se pinta el resumen, entonces dice que
        entró, que es duplicado y de qué caso lo es."""
        buena = guardar_formulario(self.conexion, _formulario("CASP2609", pagina=1))
        repetida = guardar_formulario(self.conexion, _formulario("CASP2609", pagina=2))
        texto = resumen_de_la_importacion(self._Resultado(2, [buena, repetida]))
        self.assertIn("DUPLICADOS: 1 caso repite a otro que ya estaba", texto)
        self.assertIn(f"REPITE al caso n.º {buena.caso_id}", texto)
        self.assertIn("NO se ha tocado", texto)


class LosMensajesConcuerdanYLlevanSusTildes(PruebaConBaseTemporal):
    """Es lo primero que Miguel lee al importar. «1 casos de 1 paginas» no vale."""

    _Resultado = ElResumenLlevaLasDosCifras._Resultado

    def test_en_singular_todo_va_en_singular(self):
        pagina = guardar_formulario(self.conexion, _formulario())
        texto = resumen_de_la_importacion(self._Resultado(1, [pagina]))
        self.assertIn("Se guardó 1 caso de 1 página, con 1 persona en total.", texto)

    def test_en_plural_todo_va_en_plural(self):
        primera = guardar_formulario(self.conexion, _formulario("CASP2609", pagina=1))
        segunda = guardar_formulario(
            self.conexion,
            _formulario("PARB2609", pagina=2, personas=[
                _persona(1, "UNO", "055-1111-3861"),
                _persona(2, "DOS", "055-1111-3862"),
            ]),
        )
        texto = resumen_de_la_importacion(self._Resultado(2, [primera, segunda]))
        self.assertIn("Se guardaron 2 casos de 2 páginas, con 3 personas en total.", texto)

    def test_cuando_no_entra_nada_se_dice_en_plural_y_con_tilde(self):
        texto = resumen_de_la_importacion(self._Resultado(3, []))
        self.assertIn("Se guardaron 0 casos de 3 páginas, con 0 personas en total.", texto)

    def test_ninguna_palabra_visible_se_queda_sin_su_tilde(self):
        """Las tres palabras que este resumen escribia mal."""
        pagina = guardar_formulario(self.conexion, _formulario())
        texto = resumen_de_la_importacion(self._Resultado(1, [pagina]))
        for sin_tilde in ("paginas", "pagina", "Pagina"):
            self.assertNotIn(sin_tilde, texto)

    def test_el_aviso_de_la_pagina_sin_numero_de_caso_lleva_sus_tildes(self):
        """Ya no hay motivo que mirar —la pagina entra—, pero si un aviso."""
        formulario = _formulario()._replace(numero_caso=campo_vacio())
        avisos = guardar_formulario(self.conexion, formulario).avisos
        self.assertTrue(avisos)
        for aviso in avisos:
            self.assertNotIn("numero", aviso)
        self.assertIn("número de caso", " ".join(avisos))


if __name__ == "__main__":
    unittest.main()
