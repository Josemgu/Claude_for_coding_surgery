"""El informe a la dirección: abre por el número que duele, y no lleva dinero.

Las pruebas de aquí salen del criterio de cierre 3 del pase del 2026-09-03, no de
leer el código:

  3. «El informe abre por «N de M viajaron sin verificar», trae las cuatro cifras,
     las tres tablas y ningún importe de dinero.»

⚠️ **Los rótulos cambiaron el 2026-09-03 y estas pruebas con ellos.** El informe
dejó de decir «verificada» de una persona: la palabra significaba a la vez la FIRMA
de Miguel sobre un campo leído y los seis pasos que contesta un compañero, y las
dos se leían en el mismo documento. Ahora la preparación de una persona se llama
**«con la preparación completa»**; la firma de Miguel se sigue llamando
verificación en las tres métricas del final, que es donde de verdad es eso. El
criterio 3 sigue siendo el mismo hecho medido, escrito con la palabra que no
choca.

⚠️ **De dónde salen los literales.** De haber leído entero
`C:\\Users\\josem\\Desktop\\proyecto\\pdf-a-excel\\salida\\informe.py` el
2026-09-03. Se escriben aquí como literales y no se importan de allí: aquella
carpeta no está en este repositorio, y una prueba que depende de una ruta de otro
proyecto falla el día que alguien la mueve, que no es un fallo de este programa.

**Lo del dinero se comprueba de verdad y no de palabra.** El proyecto viejo dice
que el presupuesto lo lleva otro departamento; aquí se recorre el documento ENTERO
—títulos, notas, columnas, filas y portada— buscando señales de importe. Una regla
que solo vive en un comentario se rompe el día que alguien añade una columna sin
leer el comentario.
"""

import re
import unittest

from datos.asignaciones import asignar_caso
from datos.companeros import alta_de_companero
from datos.pasos import NOMBRES_DE_LOS_PASOS
from datos.propuestas import guardar_propuesta
from datos.repositorio import alta_de_caso, alta_de_persona
from pruebas.comun import PruebaConBaseTemporal
from reportes.documento import (
    TITULAR_DE_LA_PORTADA,
    TONO_BUENO,
    TONO_MALO,
    construir_documento,
)
from reportes.excel import construir_libro
from reportes.pdf import construir_pdf
from reportes.periodo import periodo_del_mes

# El informe se genera el 20 de agosto: el caso del 10 de agosto YA viajó y el del
# 28 todavía no. Las dos mitades de la portada hacen falta para poder medirla.
GENERADO_EN = "2026-08-20 10:00:00"
AGOSTO = periodo_del_mes(2026, 8)


# Los cuatro rótulos del proyecto viejo, en su orden.
ROTULOS_DE_LAS_CUATRO_CIFRAS = (
    "viajaron sin la preparación completa",
    "viajaron con la preparación completa",
    "por viajar todavía",
    "casos en el período",
)

# Las tres tablas por las que pregunta el criterio 3.
LAS_TRES_TABLAS = (
    "Quiénes viajaron sin la preparación completa",
    "Los viajes",
    "A qué van al templo",
)

# Las dos que faltaban frente al informe viejo y entraron el 2026-09-03.
LAS_DOS_QUE_FALTABAN = (
    "Unidades con preparaciones sin completar",
    "El equipo",
)

# La palabra que ninguna sección de la dirección puede volver a decir de una
# persona. Se busca en femenino a propósito: en masculino —«casos verificados»—
# habla de la firma de Miguel, que es otro hecho y sí se llama así.
PALABRA_QUE_CHOCA_CON_LA_FIRMA = "verificada"

# Las secciones que hablan de la firma de Miguel y por eso SÍ pueden decirlo.
SECCIONES_DE_LA_FIRMA = ("Métricas del trabajo del equipo",)

# Señales de que se coló un importe. `\d+[.,]\d{2}` atrapa un «1.234,50» suelto sin
# símbolo delante, que es la forma en la que un importe entra sin que nadie lo note.
SENALES_DE_DINERO = re.compile(
    r"[$€£]|\bRD\$|\bUSD\b|\bEUR\b|\bcost[eo]s?\b|\bimporte\b|\bpresupuesto\b"
    r"|\bmoneda\b|\bmonto\b|\bpago\b|\bfondos?\b|\btarifa\b|\d+[.,]\d{2}\b",
    re.IGNORECASE,
)


class BaseConDosViajes(PruebaConBaseTemporal):
    """Un caso que ya viajó con dos personas, y otro que todavía no."""

    def setUp(self):
        super().setUp()
        self.companero_id = alta_de_companero(self.conexion, "Ana Pérez")

        self.ya_viajo = alta_de_caso(
            self.conexion, "CASP2608", unidad_numero="123456",
            unidad_nombre="Paramaribo Branch", fecha_viaje="2026-08-10",
        ).id
        self.completa = alta_de_persona(
            self.conexion, self.ya_viajo, mrn="055-1111-3851",
            nombre="Persona Completa", fila_formulario=1, ord_investidura=1,
        )
        self.perdida = alta_de_persona(
            self.conexion, self.ya_viajo, mrn="055-1111-3852",
            nombre="Persona Perdida", fila_formulario=2, ord_recibir_propias=1,
        )
        asignar_caso(self.conexion, self.ya_viajo, self.companero_id)

        self.por_viajar = alta_de_caso(
            self.conexion, "CBSP2608", unidad_numero="123456",
            unidad_nombre="Otro Barrio", fecha_viaje="2026-08-28",
        ).id
        self.futura = alta_de_persona(
            self.conexion, self.por_viajar, mrn="055-1111-3853",
            nombre="Persona Futura", fila_formulario=1,
        )

        self._contestar(self.completa, {nombre: 1 for nombre in NOMBRES_DE_LOS_PASOS})
        self._contestar(
            self.perdida,
            {**{nombre: 1 for nombre in NOMBRES_DE_LOS_PASOS}, "paso_entrevistas": 0},
        )

    def _contestar(self, persona_id, pasos):
        guardar_propuesta(self.conexion, persona_id, None, None, self.companero_id, pasos)

    def documento(self, periodo=AGOSTO, generado_en=GENERADO_EN):
        return construir_documento(self.conexion, periodo, generado_en)

    def seccion(self, documento, titulo):
        for seccion in documento.secciones:
            if seccion.titulo == titulo:
                return seccion
        raise AssertionError(
            f"El informe no trae la sección «{titulo}». Las que trae son "
            f"{[s.titulo for s in documento.secciones]}."
        )


class PruebaDeLaPortada(BaseConDosViajes):
    """Criterio 3, primera mitad: abre por «N de M» y trae las cuatro cifras."""

    def test_abre_por_cuantas_viajaron_sin_la_preparacion_completa(self):
        portada = self.documento().portada
        self.assertEqual(TITULAR_DE_LA_PORTADA, portada.titular)
        self.assertIn("1 de las 2 personas que ya viajaron", portada.frase)
        self.assertIn("SIN la preparación completa", portada.frase)

    def test_la_frase_no_lleva_porcentajes(self):
        """«Sobre ocho personas un porcentaje engaña más de lo que informa»."""
        portada = self.documento().portada
        self.assertNotIn("%", portada.frase)
        self.assertNotIn("por ciento", portada.frase.lower())

    def test_las_cuatro_cifras_estan_en_el_orden_del_viejo(self):
        cifras = self.documento().portada.cifras
        self.assertEqual(4, len(cifras))
        self.assertEqual(
            list(ROTULOS_DE_LAS_CUATRO_CIFRAS), [cifra.rotulo for cifra in cifras]
        )
        self.assertEqual([1, 1, 1, 2], [cifra.numero for cifra in cifras])

    def test_la_primera_cifra_va_en_rojo_cuando_hay_alguien_perdido(self):
        cifras = self.documento().portada.cifras
        self.assertEqual(TONO_MALO, cifras[0].tono)
        self.assertEqual(TONO_BUENO, cifras[1].tono)

    def test_sin_nadie_perdido_la_primera_cifra_deja_de_ir_en_rojo(self):
        """El color sale de la medición y no está escrito a mano."""
        self._contestar(self.perdida, {nombre: 1 for nombre in NOMBRES_DE_LOS_PASOS})
        cifras = self.documento().portada.cifras
        self.assertEqual(0, cifras[0].numero)
        self.assertEqual(TONO_BUENO, cifras[0].tono)
        self.assertIn("No se perdió ninguna ordenanza", self.documento().portada.frase)

    def test_una_persona_de_la_que_nadie_dijo_nada_cuenta_como_perdida(self):
        """El lado seguro: si nadie la miró, nadie puede decir que viajó en regla."""
        alta_de_persona(
            self.conexion, self.ya_viajo, mrn="055-1111-3859",
            nombre="Persona Sin Mirar", fila_formulario=3,
        )
        portada = self.documento().portada
        self.assertIn("2 de las 3 personas que ya viajaron", portada.frase)

    def test_el_dia_del_viaje_todavia_no_cuenta_como_viajado(self):
        """Mientras el día no termina, la preparación se puede arreglar."""
        portada = self.documento(generado_en="2026-08-10 08:00:00").portada
        self.assertIn("Todavía no ha viajado nadie", portada.titular + portada.frase)
        self.assertEqual(3, portada.cifras[2].numero, "las tres siguen por viajar")


class PruebaDeLasTresTablas(BaseConDosViajes):
    """Criterio 3, segunda mitad: las tres tablas del proyecto viejo."""

    def test_las_tres_tablas_estan_y_van_delante_de_las_demas(self):
        titulos = [seccion.titulo for seccion in self.documento().secciones]
        for orden, titulo in enumerate(LAS_TRES_TABLAS):
            self.assertEqual(titulo, titulos[orden])

    def test_quienes_viajaron_sin_la_preparacion_sale_con_nombre_y_unidad(self):
        seccion = self.seccion(
            self.documento(), "Quiénes viajaron sin la preparación completa"
        )
        self.assertEqual(
            ["Persona", "Barrio o rama", "Caso", "Viajó el", "Qué pasó"],
            [columna.nombre for columna in seccion.columnas],
        )
        self.assertEqual(1, len(seccion.filas))
        fila = seccion.filas[0]
        self.assertEqual("Persona Perdida", fila[0])
        self.assertIn("Paramaribo Branch", fila[1])
        self.assertEqual("CASP2608", fila[2])
        self.assertEqual("2026-08-10", fila[3])
        self.assertIn("Entrevistas", fila[4], "se dice en qué paso se quedó")

    def test_distingue_que_nadie_la_miro_de_que_no_esta_completa(self):
        """Son dos formas de llegar al mismo sitio y piden dos remedios distintos."""
        alta_de_persona(
            self.conexion, self.ya_viajo, mrn="055-1111-3859",
            nombre="Persona Sin Mirar", fila_formulario=3,
        )
        seccion = self.seccion(
            self.documento(), "Quiénes viajaron sin la preparación completa"
        )
        que_paso = {fila[0]: fila[4] for fila in seccion.filas}
        self.assertIn("no está completa", que_paso["Persona Perdida"])
        self.assertEqual("nadie la miró", que_paso["Persona Sin Mirar"])

    def test_los_viajes_dice_quien_lo_lleva_en_la_misma_fila(self):
        """Sin el nombre del agente al lado, hay que ir a buscarlo a otra parte."""
        seccion = self.seccion(self.documento(), "Los viajes")
        self.assertEqual(
            ["Caso", "País", "Templo", "Sale", "Asignado a", "Viajan",
             "Con la preparación completa", "Sin la preparación completa"],
            [columna.nombre for columna in seccion.columnas],
        )
        por_caso = {fila[0]: fila for fila in seccion.filas}
        self.assertEqual("Ana Pérez", por_caso["CASP2608"][4])
        self.assertEqual("sin asignar", por_caso["CBSP2608"][4])
        self.assertEqual(2, por_caso["CASP2608"][5])
        self.assertEqual(1, por_caso["CASP2608"][6])
        self.assertEqual(1, por_caso["CASP2608"][7])

    def test_un_caso_que_todavia_no_ha_salido_no_cuenta_gente_sin_completar(self):
        """Ahí lo que falta no es un fallo: es trabajo por hacer."""
        seccion = self.seccion(self.documento(), "Los viajes")
        por_caso = {fila[0]: fila for fila in seccion.filas}
        self.assertEqual("—", por_caso["CBSP2608"][7])

    def test_a_que_van_cuenta_por_ordenanza_y_en_espanol(self):
        seccion = self.seccion(self.documento(), "A qué van al templo")
        cuentas = dict(seccion.filas)
        self.assertEqual(1, cuentas["Investidura"])
        self.assertEqual(1, cuentas["Recibir ordenanzas propias"])
        self.assertTrue(
            any("no traen ninguna casilla marcada" in nota for nota in seccion.notas)
        )

    def test_donde_se_traban_dice_en_que_paso_y_cuantas_veces(self):
        seccion = self.seccion(self.documento(), "Dónde se traban las preparaciones")
        self.assertEqual([("Entrevistas", 1)], list(seccion.filas))

    def test_sin_ningun_paso_reprobado_esa_tabla_no_sale(self):
        """Una tabla de ceros ocupa el sitio de lo que sí dice algo."""
        self._contestar(self.perdida, {nombre: 1 for nombre in NOMBRES_DE_LOS_PASOS})
        titulos = [seccion.titulo for seccion in self.documento().secciones]
        self.assertNotIn("Dónde se traban las preparaciones", titulos)


class PruebaDeQueNoHayDinero(BaseConDosViajes):
    """«El presupuesto lo lleva otro departamento», comprobado sobre el documento."""

    def _todo_el_texto(self, documento):
        """Cada cadena del documento, portada y secciones incluidas."""
        textos = [
            documento.titulo,
            documento.subtitulo,
            documento.generado_en,
            documento.portada.titular,
            documento.portada.frase,
        ]
        textos.extend(cifra.rotulo for cifra in documento.portada.cifras)
        textos.extend(documento.avisos)
        for seccion in documento.secciones:
            textos.append(seccion.titulo)
            textos.append(seccion.resumen or "")
            textos.extend(seccion.notas)
            textos.extend(columna.nombre for columna in seccion.columnas)
            for fila in seccion.filas:
                textos.extend("" if valor is None else str(valor) for valor in fila)
        return textos

    def test_ni_una_sola_cadena_del_informe_habla_de_dinero(self):
        sospechosas = [
            texto
            for texto in self._todo_el_texto(self.documento())
            if SENALES_DE_DINERO.search(texto)
        ]
        self.assertEqual(
            [], sospechosas, "el presupuesto lo lleva otro departamento y no entra aquí"
        )

    def test_ninguna_columna_de_ninguna_seccion_es_un_importe(self):
        nombres = [
            columna.nombre
            for seccion in self.documento().secciones
            for columna in seccion.columnas
        ]
        for nombre in nombres:
            self.assertIsNone(SENALES_DE_DINERO.search(nombre), nombre)


class PruebaDeLasDosTablasQueFaltaban(BaseConDosViajes):
    """Dado el mismo período, cuando se arma el informe, entonces trae las dos
    tablas que tiene el informe viejo y a este le faltaban.

    Las dos salen de datos que YA estaban en la base y que nadie estaba usando: la
    unidad viene del formulario y el agente de `asignaciones`. La medición del pase
    es literal: «El viejo las tiene; los datos existen».
    """

    def test_las_dos_tablas_estan_en_el_informe(self):
        titulos = [seccion.titulo for seccion in self.documento().secciones]
        for titulo in LAS_DOS_QUE_FALTABAN:
            self.assertIn(titulo, titulos)

    def test_las_unidades_dicen_cuantas_personas_y_cuantas_sin_completar(self):
        seccion = self.seccion(self.documento(), LAS_DOS_QUE_FALTABAN[0])
        self.assertEqual(
            ["Barrio o rama", "Personas", "Sin la preparación completa"],
            [columna.nombre for columna in seccion.columnas],
        )
        por_unidad = {fila[0]: fila for fila in seccion.filas}
        paramaribo = next(u for u in por_unidad if u.startswith("Paramaribo"))
        self.assertEqual(2, por_unidad[paramaribo][1])
        self.assertEqual(1, por_unidad[paramaribo][2])

    def test_las_unidades_salen_ordenadas_por_las_que_mas_deben(self):
        """Quien lee un informe lee la primera fila; ahí va la que más duele."""
        alta_de_persona(
            self.conexion, self.ya_viajo, mrn="055-1111-3861",
            nombre="Otra Perdida", fila_formulario=4,
        )
        seccion = self.seccion(self.documento(), LAS_DOS_QUE_FALTABAN[0])
        sin_completar = [fila[2] for fila in seccion.filas]
        self.assertEqual(sorted(sin_completar, reverse=True), sin_completar)

    def test_una_unidad_al_dia_no_ocupa_una_fila_en_la_tabla(self):
        """Solo salen las que deben algo. Una fila de ceros tapa lo que sí dice algo."""
        self._contestar(self.perdida, {nombre: 1 for nombre in NOMBRES_DE_LOS_PASOS})
        seccion = self.seccion(self.documento(), LAS_DOS_QUE_FALTABAN[0])
        unidades = [fila[0] for fila in seccion.filas]
        self.assertFalse(
            [u for u in unidades if u.startswith("Paramaribo")],
            f"Paramaribo está al día y sigue en la tabla: {unidades}",
        )

    def test_con_todas_las_unidades_al_dia_la_tabla_no_sale(self):
        """La otra mitad: sin nada pendiente, la sección entera desaparece."""
        for persona_id in (self.perdida, self.futura):
            self._contestar(persona_id, {n: 1 for n in NOMBRES_DE_LOS_PASOS})
        titulos = [seccion.titulo for seccion in self.documento().secciones]
        self.assertNotIn(LAS_DOS_QUE_FALTABAN[0], titulos)

    def test_el_equipo_dice_cuanto_lleva_cada_agente_y_cuanto_esta_mirado(self):
        seccion = self.seccion(self.documento(), LAS_DOS_QUE_FALTABAN[1])
        self.assertEqual(
            ["Agente", "Personas a su cargo", "Con la preparación completa",
             "Sin mirar"],
            [columna.nombre for columna in seccion.columnas],
        )
        por_agente = {fila[0]: fila for fila in seccion.filas}
        self.assertEqual((2, 1, 0), tuple(por_agente["Ana Pérez"][1:]))

    def test_lo_que_nadie_lleva_sale_como_sin_asignar_y_no_desaparece(self):
        """El peor de los casos —trabajo sin repartir— es el que hay que ver."""
        seccion = self.seccion(self.documento(), LAS_DOS_QUE_FALTABAN[1])
        por_agente = {fila[0]: fila for fila in seccion.filas}
        self.assertIn("sin asignar", por_agente)
        self.assertEqual(1, por_agente["sin asignar"][1])

    def test_sin_mirar_no_es_lo_mismo_que_sin_completar(self):
        """Son dos cifras distintas y la tabla las tiene que separar.

        «Persona Futura» no tiene ni un paso contestado: es trabajo sin empezar.
        «Persona Perdida» tiene cinco de seis: es trabajo hecho que encontró algo.
        """
        seccion = self.seccion(self.documento(), LAS_DOS_QUE_FALTABAN[1])
        por_agente = {fila[0]: fila for fila in seccion.filas}
        self.assertEqual(1, por_agente["sin asignar"][3], "Persona Futura, sin mirar")
        self.assertEqual(0, por_agente["Ana Pérez"][3], "las dos suyas están miradas")


class ElInformeNoDiceVerificadaDeUnaPersona(BaseConDosViajes):
    """Dado el informe entero, cuando se recorre cada cadena de las secciones de la
    dirección, entonces ninguna llama «verificada» a una persona.

    Es el hallazgo MEDIO de la auditoría final de QA: «verificado» significaba dos
    cosas en el mismo programa —la firma de Miguel en la pantalla y los seis pasos
    en «Sí»— y el informe usaba la misma palabra para las dos. La firma SÍ se sigue
    llamando verificación en las métricas del final, y por eso esa sección queda
    fuera de esta prueba en vez de quedar fuera del informe.
    """

    def _cadenas_de(self, seccion):
        textos = [seccion.titulo, seccion.resumen or ""]
        textos.extend(seccion.notas)
        textos.extend(columna.nombre for columna in seccion.columnas)
        for fila in seccion.filas:
            textos.extend("" if valor is None else str(valor) for valor in fila)
        return textos

    def test_ninguna_seccion_de_la_direccion_dice_verificada(self):
        culpables = []
        for seccion in self.documento().secciones:
            if seccion.titulo in SECCIONES_DE_LA_FIRMA:
                continue
            culpables.extend(
                texto
                for texto in self._cadenas_de(seccion)
                if PALABRA_QUE_CHOCA_CON_LA_FIRMA in texto.lower()
            )
        self.assertEqual([], culpables)

    def test_la_portada_tampoco_lo_dice(self):
        portada = self.documento().portada
        textos = [portada.titular, portada.frase]
        textos.extend(cifra.rotulo for cifra in portada.cifras)
        for texto in textos:
            self.assertNotIn(PALABRA_QUE_CHOCA_CON_LA_FIRMA, texto.lower(), texto)

    def test_las_metricas_del_final_SI_siguen_hablando_de_verificados(self):
        """La otra mitad: ahí la palabra es la correcta y quitarla sería peor.

        Esa métrica cuenta campos de `procedencia_campo` con `verificado = 1`, o
        sea lo que Miguel dio por bueno mirando el papel. Llamarlo «preparación
        completa» sería el mismo error de nombre al revés.
        """
        seccion = self.seccion(self.documento(), SECCIONES_DE_LA_FIRMA[0])
        juntas = " ".join(str(valor) for fila in seccion.filas for valor in fila)
        self.assertIn("verificad", juntas.lower())


class PruebaDelTemploEnElInforme(BaseConDosViajes):
    """El templo se lee del papel y llega a la columna «Templo» de «Los viajes»."""

    def test_un_caso_con_templo_guardado_lo_ensena_en_la_tabla(self):
        self.conexion.execute(
            "UPDATE casos SET templo_nombre = ? WHERE id = ?",
            ("Santo Domingo República Dominicana", self.ya_viajo),
        )
        seccion = self.seccion(self.documento(), "Los viajes")
        por_caso = {fila[0]: fila for fila in seccion.filas}
        self.assertEqual("Santo Domingo República Dominicana", por_caso["CASP2608"][2])

    def test_un_caso_sin_templo_lo_dice_y_no_lo_inventa(self):
        """Un caso importado antes de la versión 10 no tiene templo. No se rellena."""
        seccion = self.seccion(self.documento(), "Los viajes")
        por_caso = {fila[0]: fila for fila in seccion.filas}
        self.assertEqual("no consta", por_caso["CBSP2608"][2])


class PruebaDeLosDosArchivos(BaseConDosViajes):
    """La portada tiene que llegar a los dos formatos, no solo al objeto."""

    def test_el_pdf_se_abre_y_abre_por_la_frase_de_la_portada(self):
        from pypdf import PdfReader
        import io

        documento = self.documento()
        lector = PdfReader(io.BytesIO(construir_pdf(documento)))
        texto = "\n".join(pagina.extract_text() for pagina in lector.pages)

        self.assertGreaterEqual(len(lector.pages), 1)
        self.assertIn(TITULAR_DE_LA_PORTADA, texto)
        self.assertIn("1 de las 2 personas que ya viajaron", texto)
        for rotulo in ROTULOS_DE_LAS_CUATRO_CIFRAS:
            self.assertIn(rotulo, texto)

    def test_el_pie_y_la_cinta_salen_en_todas_las_paginas(self):
        from pypdf import PdfReader
        import io

        lector = PdfReader(io.BytesIO(construir_pdf(self.documento())))
        for numero, pagina in enumerate(lector.pages, start=1):
            texto = pagina.extract_text()
            self.assertIn("Preparación para el templo", texto, f"página {numero}")
            self.assertIn(f"Página {numero} de {len(lector.pages)}", texto)

    def test_el_xlsx_lleva_la_portada_en_todas_sus_pestanas(self):
        """Una hoja de Excel se manda suelta por correo, y el número tiene que ir con ella."""
        libro = construir_libro(self.documento())
        for nombre in libro.sheetnames:
            hoja = libro[nombre]
            arriba = [
                hoja.cell(row=fila, column=1).value or "" for fila in range(1, 12)
            ]
            self.assertTrue(
                any(TITULAR_DE_LA_PORTADA in texto for texto in arriba),
                f"a la pestaña «{nombre}» le falta la portada",
            )


if __name__ == "__main__":
    unittest.main()
