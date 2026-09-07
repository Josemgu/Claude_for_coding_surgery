"""Dos familias comparten numero de caso, y el programa deja de perder una.

**El criterio sale de una medicion del dueno, no del codigo.** El 2026-09-03 importo
diez documentos de su carpeta real en la PC del trabajo y el programa contesto
«**6 caso ya existente**». Un renglon, literal: *«El caso BALC2609 ya estaba en la
base con 1 persona(s)… Esta pagina traia 1 persona(s)… MRN en comun: 0… Puede ser
otra familia de la misma unidad y el mismo mes, y entonces esta pagina se quedo
fuera entera.»* **Era otra familia.** Seis de diez documentos se quedaron fuera.

Los criterios que se comprueban aqui, en la forma en que se pueden comprobar:

    DADO dos archivos sueltos con el MISMO numero de caso y MRN distintos,
    CUANDO se importan,
    ENTONCES quedan DOS casos y ninguna pagina se rechaza.

    DADO un documento de seis hojas del mismo numero,
    CUANDO se importa,
    ENTONCES queda UN caso con doce personas.

    DADO un archivo que ya se importo,
    CUANDO se vuelve a importar,
    ENTONCES entra igual, marcado como duplicado del primero,
             y el primero no cambia ni un byte.

    DADO el Excel del agente de dos casos con el mismo numero,
    CUANDO vuelve contestado,
    ENTONCES cada respuesta cae en la persona de SU caso.

⚠️ Ninguna prueba de este archivo toca la base real ni ningun PDF real: los
formularios se construyen a mano con valores inventados, y la base es temporal.
"""

from openpyxl import load_workbook

from datos.asignaciones import asignar_casos
from datos.companeros import alta_de_companero
from datos.ilegibles import ENTRO_COMO_DUPLICADO, documentos_ilegibles
from datos.pendientes import casos_pendientes_de_verificar
from datos.propuestas import resolver_persona_por_par
from datos.repositorio import leer_casos_por_numero, leer_personas_del_caso
from importacion.documento import resumen_de_la_importacion
from importacion.guardado import guardar_las_paginas_del_documento
from paquete.columnas import (
    NOMBRE_DE_LA_HOJA,
    PRIMERA_FILA_DE_DATOS,
    armar_la_clave,
    indice_de,
    partir_la_clave,
)
from paquete.exportacion import exportar_paquete
from paquete.reconciliacion import reconciliar_excel
from pruebas.comun import PruebaConBaseTemporal
from pruebas.prueba_importacion import _formulario, _persona
from pruebas.prueba_paquete import contestar_los_seis_pasos


class _ResultadoParaElResumen:
    """Lo minimo que `resumen_de_la_importacion` mira. Sin ruta ni espejo."""

    def __init__(self, paginas, por_pagina):
        self.ruta_pdf = "inventado.pdf"
        self.paginas = paginas
        self.paginas_por_pagina = por_pagina
        self.espejo = None


def _familia(ruta, numero="BALC2609", nombres_y_mrn=(("ANONIMO, M", "055-1111-3853"),)):
    """Un documento de una hoja con las personas que se le pasen."""
    return _formulario(
        numero,
        pagina=1,
        personas=[
            _persona(fila, nombre, mrn)
            for fila, (nombre, mrn) in enumerate(nombres_y_mrn, start=1)
        ],
    )._replace(ruta_pdf=ruta)


class DosArchivosSueltosConElMismoNumero(PruebaConBaseTemporal):
    """Criterio 1: dos casos, cero rechazos. Es lo que el dueno midio que fallaba."""

    def setUp(self):
        super().setUp()
        self.primeras = guardar_las_paginas_del_documento(
            self.conexion,
            [_familia(r"C:\Escaneos\familia_uno.pdf",
                      nombres_y_mrn=(("ANONIMO, M", "055-1111-3853"),))],
        )
        self.segundas = guardar_las_paginas_del_documento(
            self.conexion,
            [_familia(r"C:\Escaneos\familia_dos.pdf",
                      nombres_y_mrn=(("EJEMPLO, D", "055-1111-3899"),))],
        )

    def test_quedan_dos_casos_con_el_mismo_numero(self):
        casos = leer_casos_por_numero(self.conexion, "BALC2609")
        self.assertEqual(len(casos), 2)

    def test_ninguna_pagina_se_rechaza(self):
        self.assertTrue(all(p.importada for p in self.primeras + self.segundas))
        self.assertIsNone(self.primeras[0].motivo)
        self.assertIsNone(self.segundas[0].motivo)

    def test_ninguno_de_los_dos_se_marca_como_duplicado_del_otro(self):
        """No comparten ni un MRN ni la ruta: son familias distintas, no copias."""
        self.assertIsNone(self.primeras[0].duplicado_de)
        self.assertIsNone(self.segundas[0].duplicado_de)

    def test_cada_caso_conserva_a_su_gente(self):
        uno, dos = leer_casos_por_numero(self.conexion, "BALC2609")
        self.assertEqual(
            [p["nombre"] for p in leer_personas_del_caso(self.conexion, uno["id"])],
            ["ANONIMO, M"],
        )
        self.assertEqual(
            [p["nombre"] for p in leer_personas_del_caso(self.conexion, dos["id"])],
            ["EJEMPLO, D"],
        )

    def test_no_queda_ningun_renglon_de_lo_que_no_entro(self):
        """Nada quedo fuera, asi que no hay nada que anotar."""
        self.assertEqual(documentos_ilegibles(self.conexion), [])


class ElGrupoDeSeisHojasSigueSiendoUnCaso(PruebaConBaseTemporal):
    """Que un numero repetido ya no rechace NO puede romper la union de hojas.

    Es el fallo mas grave que este proyecto tuvo: un PDF de grupo perdia 11 de 12
    personas. Se comprueba aqui otra vez, contra el codigo nuevo.
    """

    def test_seis_hojas_del_mismo_documento_dan_un_caso_con_doce(self):
        paginas = guardar_las_paginas_del_documento(
            self.conexion,
            [
                _formulario(
                    "SURB2609",
                    pagina=hoja,
                    personas=[
                        _persona(1, f"UNO DE LA {hoja}", f"055-1111-{3800 + hoja * 2:04d}"),
                        _persona(2, f"DOS DE LA {hoja}", f"055-1111-{3801 + hoja * 2:04d}"),
                    ],
                )._replace(ruta_pdf=r"C:\Escaneos\grupo.pdf")
                for hoja in range(1, 7)
            ],
        )
        casos = leer_casos_por_numero(self.conexion, "SURB2609")
        self.assertEqual(len(casos), 1)
        self.assertEqual(len(leer_personas_del_caso(self.conexion, casos[0]["id"])), 12)
        self.assertTrue(all(p.importada for p in paginas))
        self.assertTrue(all(p.duplicado_de is None for p in paginas))


class ElMismoArchivoDosVeces(PruebaConBaseTemporal):
    """Criterio del dueno: «Si hay documentos duplicados debe decirlo y no rechazarlo».

    Las dos mitades se prueban por separado, porque son dos promesas distintas:
    **entra y se marca**, y **el original no cambia**.
    """

    def setUp(self):
        super().setUp()
        self.primeras = guardar_las_paginas_del_documento(
            self.conexion, [_familia(r"C:\Escaneos\uno.pdf")]
        )
        self.original_antes = self._fila_del_caso(self.primeras[0].caso_id)
        self.personas_antes = self._personas_del_caso(self.primeras[0].caso_id)
        self.segundas = guardar_las_paginas_del_documento(
            self.conexion, [_familia(r"C:\Escaneos\uno.pdf")]
        )

    def _fila_del_caso(self, caso_id):
        return tuple(
            self.conexion.execute(
                "SELECT * FROM casos WHERE id = ?", (caso_id,)
            ).fetchone()
        )

    def _personas_del_caso(self, caso_id):
        return [
            tuple(fila)
            for fila in self.conexion.execute(
                "SELECT * FROM personas WHERE caso_id = ? ORDER BY id", (caso_id,)
            ).fetchall()
        ]

    def test_el_duplicado_entra_como_caso_aparte(self):
        self.assertTrue(self.segundas[0].importada)
        self.assertNotEqual(self.segundas[0].caso_id, self.primeras[0].caso_id)
        self.assertEqual(len(leer_casos_por_numero(self.conexion, "BALC2609")), 2)

    def test_el_duplicado_queda_marcado_y_dice_de_cual_lo_es(self):
        self.assertEqual(self.segundas[0].duplicado_de, self.primeras[0].caso_id)
        guardado = self.conexion.execute(
            "SELECT duplicado_de FROM casos WHERE id = ?", (self.segundas[0].caso_id,)
        ).fetchone()[0]
        self.assertEqual(guardado, self.primeras[0].caso_id)

    def test_el_caso_que_ya_estaba_no_cambia_ni_un_byte(self):
        """Se compara la fila entera y sus personas, no el campo que uno sospecha."""
        self.assertEqual(
            self.original_antes, self._fila_del_caso(self.primeras[0].caso_id)
        )
        self.assertEqual(
            self.personas_antes, self._personas_del_caso(self.primeras[0].caso_id)
        )

    def test_nada_se_funde_solo(self):
        """Cada caso se queda con SU gente: el duplicado no se le anade al original."""
        self.assertEqual(
            len(leer_personas_del_caso(self.conexion, self.primeras[0].caso_id)), 1
        )
        self.assertEqual(
            len(leer_personas_del_caso(self.conexion, self.segundas[0].caso_id)), 1
        )

    def test_las_firmas_del_original_siguen_donde_estaban(self):
        """Regla permanente 5: nada se firma solo, ni en el original ni en la copia."""
        firmados = self.conexion.execute(
            "SELECT COUNT(*) FROM procedencia_campo WHERE verificado = 1"
        ).fetchone()[0]
        self.assertEqual(firmados, 0)

    def test_deja_su_renglon_diciendo_que_ENTRO(self):
        filas = documentos_ilegibles(self.conexion)
        self.assertEqual([fila["motivo"] for fila in filas], [ENTRO_COMO_DUPLICADO])
        self.assertEqual(filas[0]["caso_id"], self.segundas[0].caso_id)
        self.assertIn(f"caso n.º {self.primeras[0].caso_id}", filas[0]["detalle"])

    def test_la_marca_se_ve_en_la_lista_de_pendientes(self):
        """Si hay que abrir el caso para enterarse, con cincuenta nadie lo abre."""
        por_id = {caso["id"]: caso for caso in casos_pendientes_de_verificar(self.conexion)}
        self.assertIsNone(por_id[self.primeras[0].caso_id]["duplicado_de"])
        self.assertEqual(
            por_id[self.segundas[0].caso_id]["duplicado_de"], self.primeras[0].caso_id
        )

    def test_el_resumen_de_la_importacion_lo_dice(self):
        texto = resumen_de_la_importacion(
            _ResultadoParaElResumen(1, list(self.segundas))
        )
        self.assertIn("DUPLICADOS: 1 caso repite a otro que ya estaba", texto)
        self.assertIn(f"REPITE al caso n.º {self.primeras[0].caso_id}", texto)


class ElExcelDelAgenteNoCruzaDosFamilias(PruebaConBaseTemporal):
    """Criterio 3 del cierre: dos casos con el mismo numero vuelven a su persona.

    Es el riesgo que abre quitar el `UNIQUE`: la vuelta casaba por
    `numero_caso` + `mrn`, y con dos casos que comparten numero eso deja de
    identificar a nadie. La clave lleva ahora tambien el id del caso.
    """

    def setUp(self):
        super().setUp()
        self.companero_id = alta_de_companero(self.conexion, "Ana Pérez")
        # Dos familias distintas con el MISMO numero. La segunda ademas repite el
        # MRN de la primera: es el caso peor, y el que un `UNIQUE` habria tapado.
        self.primera = guardar_las_paginas_del_documento(
            self.conexion,
            [_familia(r"C:\Escaneos\uno.pdf",
                      nombres_y_mrn=(("ANONIMO, M", "055-1111-3853"),))],
        )[0]
        self.segunda = guardar_las_paginas_del_documento(
            self.conexion,
            [_familia(r"C:\Escaneos\dos.pdf",
                      nombres_y_mrn=(("ANONIMO, M", "055-1111-3853"),))],
        )[0]
        asignar_casos(
            self.conexion,
            [self.primera.caso_id, self.segunda.caso_id],
            self.companero_id,
        )

    def _fila_de_la_clave(self, hoja, caso_id):
        """La fila del Excel cuya clave apunta a ESE caso."""
        for fila in range(PRIMERA_FILA_DE_DATOS, hoja.max_row + 1):
            partes = partir_la_clave(hoja.cell(row=fila, column=indice_de("clave")).value)
            if partes is not None and partes[2] == caso_id:
                return fila
        raise AssertionError(f"No hay ninguna fila del caso {caso_id}.")

    def test_la_clave_del_paquete_lleva_el_id_del_caso(self):
        resultado = exportar_paquete(
            self.conexion, self.companero_id, self.carpeta_temporal
        )
        libro = load_workbook(str(resultado.ruta_del_excel))
        hoja = libro[NOMBRE_DE_LA_HOJA]
        claves = {
            hoja.cell(row=fila, column=indice_de("clave")).value
            for fila in range(PRIMERA_FILA_DE_DATOS, hoja.max_row + 1)
        }
        libro.close()
        self.assertEqual(
            claves,
            {
                armar_la_clave("BALC2609", "055-1111-3853", self.primera.caso_id),
                armar_la_clave("BALC2609", "055-1111-3853", self.segunda.caso_id),
            },
        )

    def test_lo_que_el_companero_contesta_cae_en_la_persona_de_SU_caso(self):
        """DADO dos casos con el mismo numero y el mismo MRN, CUANDO el companero
        contesta que NO a los seis pasos de uno solo, ENTONCES ese uno queda
        «incompleta» y el otro se queda sin propuesta."""
        resultado = exportar_paquete(
            self.conexion, self.companero_id, self.carpeta_temporal
        )
        libro = load_workbook(str(resultado.ruta_del_excel))
        hoja = libro[NOMBRE_DE_LA_HOJA]
        contestar_los_seis_pasos(hoja, self._fila_de_la_clave(hoja, self.segunda.caso_id), "No")
        libro.save(str(resultado.ruta_del_excel))
        libro.close()

        vuelta = reconciliar_excel(
            self.conexion, resultado.ruta_del_excel, self.companero_id
        )

        self.assertEqual(len(vuelta.aplicadas), 1)
        self.assertEqual(len(vuelta.descartadas), 0)
        de_la_segunda = resolver_persona_por_par(
            self.conexion, "BALC2609", "055-1111-3853", self.segunda.caso_id
        )
        de_la_primera = resolver_persona_por_par(
            self.conexion, "BALC2609", "055-1111-3853", self.primera.caso_id
        )
        self.assertEqual(de_la_segunda["estado_propuesto"], "incompleta")
        self.assertIsNone(de_la_primera["estado_propuesto"])
        self.assertIsNone(de_la_primera["propuesto_por"])

    def test_una_clave_vieja_sin_id_no_escoge_una_al_azar_y_lo_dice(self):
        """Un paquete generado antes del cambio no puede distinguir, y se descarta.

        Escoger una de las dos escribiria el trabajo del companero sobre la familia
        equivocada y nadie se enteraria. Descartar deja su renglon.
        """
        resultado = exportar_paquete(
            self.conexion, self.companero_id, self.carpeta_temporal
        )
        libro = load_workbook(str(resultado.ruta_del_excel))
        hoja = libro[NOMBRE_DE_LA_HOJA]
        fila = self._fila_de_la_clave(hoja, self.segunda.caso_id)
        hoja.cell(row=fila, column=indice_de("clave")).value = armar_la_clave(
            "BALC2609", "055-1111-3853"
        )
        contestar_los_seis_pasos(hoja, fila, "No")
        libro.save(str(resultado.ruta_del_excel))
        libro.close()

        vuelta = reconciliar_excel(
            self.conexion, resultado.ruta_del_excel, self.companero_id
        )

        self.assertEqual(len(vuelta.aplicadas), 0)
        self.assertEqual(len(vuelta.descartadas), 1)
        self.assertIn("no se sabe a cuál se refiere", vuelta.descartadas[0].motivo)
        # Y lo importante: NINGUNA de las dos personas se toco.
        for caso_id in (self.primera.caso_id, self.segunda.caso_id):
            persona = resolver_persona_por_par(
                self.conexion, "BALC2609", "055-1111-3853", caso_id
            )
            self.assertIsNone(persona["propuesto_por"])

    def _pdf_de_una_pagina(self, nombre):
        """Un PDF de verdad en la carpeta temporal, para que haya algo que recortar."""
        from pypdf import PdfWriter

        ruta = self.carpeta_temporal / nombre
        escritor = PdfWriter()
        escritor.add_blank_page(width=612, height=792)
        with open(ruta, "wb") as archivo:
            escritor.write(archivo)
        return ruta

    def test_los_dos_pdf_del_paquete_no_se_pisan(self):
        """Dos casos con el mismo numero escribian el mismo archivo: uno se perdia."""
        for caso_id, nombre in (
            (self.primera.caso_id, "uno.pdf"),
            (self.segunda.caso_id, "dos.pdf"),
        ):
            self.conexion.execute(
                "UPDATE casos SET ruta_pdf = ? WHERE id = ?",
                (str(self._pdf_de_una_pagina(nombre)), caso_id),
            )
        resultado = exportar_paquete(
            self.conexion, self.companero_id, self.carpeta_temporal
        )
        pdfs = sorted(p.name for p in resultado.carpeta.rglob("*.pdf"))
        self.assertEqual(len(pdfs), len(set(pdfs)))
        self.assertEqual(
            pdfs,
            sorted(
                [
                    f"BALC2609-{self.primera.caso_id}.pdf",
                    f"BALC2609-{self.segunda.caso_id}.pdf",
                ]
            ),
        )


class LoQueMiguelMarcaDesdeElCasoQuedaFirmado(PruebaConBaseTemporal):
    """El desplegable de estado escribe QUIÉN lo marcó, no solo el estado.

        DADO un caso abierto,
        CUANDO Miguel elige «completa» en el desplegable de la recomendación,
        ENTONCES el documento queda marcado Y consta quién lo marcó y cuándo.

    El defecto que cierra: el desplegable podía escribir `completa` dejando las
    tres columnas de la marca en blanco —un documento dado por completo que nadie
    firmó—. Se decidió firmarlo en vez de quitar el valor del desplegable, porque
    ese desplegable ES la mano de Miguel y quitárselo le obligaría a salir a otra
    pantalla para decir lo que ya sabe.
    """

    def test_elegir_completa_deja_quien_y_cuando(self):
        from datos.companeros import companero_que_firma
        from datos.estados import COMPLETA
        from datos.marcas_de_revision import marcar_a_mano

        caso_id = guardar_las_paginas_del_documento(
            self.conexion, [_familia(r"C:\Escaneos\uno.pdf")]
        )[0].caso_id
        antes = self.conexion.execute(
            "SELECT estado_recomendacion, estado_marcado_por FROM casos WHERE id = ?",
            (caso_id,),
        ).fetchone()
        self.assertIsNone(antes["estado_recomendacion"])
        self.assertIsNone(antes["estado_marcado_por"])

        marcar_a_mano(
            self.conexion, caso_id, COMPLETA, companero_que_firma(self.conexion)
        )

        fila = self.conexion.execute(
            "SELECT estado_recomendacion, estado_marcado_por, estado_marcado_en "
            "FROM casos WHERE id = ?",
            (caso_id,),
        ).fetchone()
        self.assertEqual(fila["estado_recomendacion"], COMPLETA)
        self.assertIsNotNone(fila["estado_marcado_por"])
        self.assertIsNotNone(fila["estado_marcado_en"])

    def test_las_dos_pantallas_firman_como_el_MISMO_companero(self):
        """Dos formas de responder «quién es Miguel» darían dos compañeros iguales."""
        from datos.companeros import companero_que_firma

        primero = companero_que_firma(self.conexion)
        segundo = companero_que_firma(self.conexion)
        self.assertEqual(primero, segundo)
        self.assertEqual(
            self.conexion.execute(
                "SELECT COUNT(*) FROM companeros WHERE nombre = 'Miguel'"
            ).fetchone()[0],
            1,
        )
