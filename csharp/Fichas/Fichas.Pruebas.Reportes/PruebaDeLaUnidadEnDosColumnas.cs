using ClosedXML.Excel;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;
using Fichas.Reportes;
using Fichas.Reportes.Armado;
using Fichas.Reportes.Modelo;
using Fichas.Reportes.Reglas;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// La unidad viaja en DOS columnas —su número en una y su nombre en otra— en los cuatro informes.
/// </summary>
/// <remarks>
/// <para><b>Del dueño, 2026-09-08</b>, contestando a que partirla cambia también el PDF:
/// <i>«Sí, pártelo en dos y que conserve lo retirado»</i>. Hasta ese día la unidad salía pegada
/// en una sola celda —«Castries Branch · 0700016», que armaba
/// <c>Vocabulario.UnidadConSuNumero</c>— y por eso en el Excel <b>no se podía filtrar por
/// número de unidad</b>: el filtro veía una sola cadena con las dos cosas dentro.</para>
///
/// <para>Es el mismo cambio que el paquete de los compañeros ya hizo el 2026-09-07
/// (<c>Columnas.ColumnaDelNumeroDeUnidad</c>), y se copia de ahí a propósito, incluido que el
/// número entra como <see cref="ClaseDeColumna.Texto"/>: sin formato de texto Excel se come un
/// cero de delante y <c>0700016</c> deja de ser el número que dice el papel.</para>
///
/// <para>⚠️ <b>Estas pruebas se escribieron ANTES de tocar el armado</b> y salen del criterio
/// del dueño, no del código: por eso comprueban la columna, su clase y el dato leído DE VUELTA
/// desde el <c>.xlsx</c>, y no que tal función devuelva tal cadena.</para>
/// </remarks>
[TestClass]
public class PruebaDeLaUnidadEnDosColumnas
{
    /// <summary>El número de unidad del escenario. Empieza por cero A PROPÓSITO.</summary>
    /// <remarks>
    /// Ninguno de los 7 PDF reales trae un número que empiece por cero (comprobado leyéndolos),
    /// así que el caso que de verdad prueba la defensa hay que inventarlo. Si esta constante
    /// pierde su cero, la prueba sigue verde sin comprobar nada.
    /// </remarks>
    private const string NumeroConCeroDelante = "0700016";

    private const string NombreDeLaUnidad = "Castries Branch";

    /// <summary>Cómo salía pegado hasta el 2026-09-08. No puede volver a aparecer en ninguna celda.</summary>
    private const string ComoSalioPegado = NombreDeLaUnidad + " · " + NumeroConCeroDelante;

    private const string Desde = "2026-09-01";
    private const string Hasta = "2026-09-30";

    /// <summary>El rótulo de la columna nueva; el mismo que ya usa el paquete de los compañeros.</summary>
    private const string RotuloDelNumero = "Número de unidad";

    // ─────────────────────── las dos columnas, en las seis tablas ───────────────────────

    [TestMethod]
    public void LasTablasDelPeriodoTraenElNumeroDeUnidadEnSuPropiaColumna()
    {
        var mundo = Escenario();
        var documento = mundo.Reportes.DocumentoDelPeriodo(Periodo.Leer(Desde, Hasta).Periodo!, Generado);

        // Las tres tablas del informe del período que llevan la unidad de una persona.
        foreach (var titulo in new[]
        {
            Vocabulario.QuienesViajaronSinVerificar,
            "Parte 1 — Personas que viajaron, y en qué estado quedó su recomendación",
            "Parte 2 — Personas que NO pudieron viajar",
        })
        {
            ComprobarLasDosColumnas(Seccion(documento, titulo));
        }
    }

    [TestMethod]
    public void LaTablaDeUnidadesAgrupaPorElParYNoPorLaCadenaPegada()
    {
        var mundo = Escenario();
        var documento = mundo.Reportes.DocumentoDelPeriodo(Periodo.Leer(Desde, Hasta).Periodo!, Generado);

        ComprobarLasDosColumnas(Seccion(documento, "Unidades con preparaciones sin completar"));
    }

    [TestMethod]
    public void LaTablaDelHistoricoTraeElNumeroDeUnidadEnSuPropiaColumna()
    {
        var mundo = Escenario();
        ComprobarLasDosColumnas(
            Seccion(mundo.Reportes.DocumentoDelHistorico(Generado), "Histórico — casos archivados"));
    }

    [TestMethod]
    public void LaTablaDeLaSegundaVueltaTraeElNumeroDeUnidadEnSuPropiaColumna()
    {
        var mundo = Escenario();
        var documento = mundo.Reportes.DocumentoDeLaSegundaVuelta(2, mundo.Intentos, Generado);

        ComprobarLasDosColumnas(documento.Secciones.Single(s => s.Filas.Count > 0 && TieneLaColumna(s, RotuloDelNumero)));
    }

    // ─────────────────────── el cero de delante, leído de vuelta ───────────────────────

    [TestMethod]
    public void ElExcelDelPeriodoDevuelveElNumeroDeUnidadConSuCeroDeDelante()
    {
        var mundo = Escenario();
        var ruta = RutaTemporal("periodo-unidad.xlsx");
        try
        {
            var resultado = mundo.Reportes.GenerarReporteDelPeriodoEnExcel(Desde, Hasta, ruta);
            Assert.IsTrue(resultado.SeEscribio, string.Join(" · ", resultado.Avisos.Select(a => a.Linea)));

            var leidos = NumerosDeUnidadDelLibro(ruta);

            Assert.IsNotEmpty(leidos, "Ninguna hoja del .xlsx traía la columna del número de unidad.");
            foreach (var leido in leidos)
            {
                Assert.AreEqual(
                    NumeroConCeroDelante,
                    leido,
                    $"Excel devolvió «{leido}»: el cero de delante se perdió por el camino.");
            }

            Console.WriteLine($"MEDIDO · {leidos.Count} celdas del número de unidad, todas «{NumeroConCeroDelante}»");
        }
        finally
        {
            if (File.Exists(ruta)) File.Delete(ruta);
        }
    }

    [TestMethod]
    public void LaColumnaDelNumeroDeUnidadEsDeTextoYNoDeRecuento()
    {
        var mundo = Escenario();
        var documento = mundo.Reportes.DocumentoDelPeriodo(Periodo.Leer(Desde, Hasta).Periodo!, Generado);

        foreach (var seccion in documento.Secciones.Where(s => TieneLaColumna(s, RotuloDelNumero)))
        {
            var columna = seccion.Columnas.Single(c => c.Nombre == RotuloDelNumero);
            Assert.AreEqual(
                ClaseDeColumna.Texto,
                columna.Clase,
                $"«{seccion.Titulo}» declara el número de unidad como {columna.Clase}. Como Crudo, un "
                + "número de siete dígitos sin cero delante entraría en Excel como NÚMERO y dejaría "
                + "de poder compararse con el del papel.");
        }
    }

    // ─────────────────────── lo pegado no vuelve ───────────────────────

    [TestMethod]
    public void NingunaCeldaDeNingunInformeVuelveATraerLosDosDatosPegados()
    {
        var mundo = Escenario();
        var periodo = Periodo.Leer(Desde, Hasta).Periodo!;

        var documentos = new[]
        {
            mundo.Reportes.DocumentoDelPeriodo(periodo, Generado),
            mundo.Reportes.DocumentoDelHistorico(Generado),
            mundo.Reportes.DocumentoDeLaSegundaVuelta(2, mundo.Intentos, Generado),
            mundo.Reportes.DocumentoDeCompanero(mundo.Sandy.Id, periodo, Generado),
        };

        foreach (var documento in documentos)
        {
            foreach (var seccion in documento.Secciones)
            {
                foreach (var celda in seccion.Filas.SelectMany(f => f).Where(c => c is not null))
                {
                    Assert.AreNotEqual(
                        ComoSalioPegado,
                        celda,
                        $"«{documento.Subtitulo}» → «{seccion.Titulo}» volvió a pegar los dos datos "
                        + "en una celda; con eso el filtro del Excel deja de poder buscar por número.");
                }
            }
        }
    }

    [TestMethod]
    public void ElInformeDeUnAgenteTambienLoTraeEnDosColumnas()
    {
        var mundo = Escenario();
        var documento = mundo.Reportes.DocumentoDeCompanero(
            mundo.Sandy.Id, Periodo.Leer(Desde, Hasta).Periodo!, Generado);

        var conUnidad = documento.Secciones.Where(s => TieneLaColumna(s, RotuloDelNumero)).ToList();
        Assert.IsGreaterThanOrEqualTo(
            3,
            conUnidad.Count,
            $"El informe del agente solo trajo {conUnidad.Count} tablas con la columna del número de unidad.");

        foreach (var seccion in conUnidad) ComprobarLasDosColumnas(seccion);
    }

    // ---- las comprobaciones -------------------------------------------------

    /// <summary>Que la tabla trae las dos columnas, contiguas, y cada dato en la suya.</summary>
    private static void ComprobarLasDosColumnas(Seccion seccion)
    {
        var donde = IndiceDe(seccion, RotuloDelNumero);
        Assert.IsGreaterThanOrEqualTo(0, donde, $"«{seccion.Titulo}» no trae la columna «{RotuloDelNumero}».");

        var nombre = seccion.Columnas[donde + 1].Nombre;
        Assert.IsTrue(
            nombre is "Unidad" or "Barrio o rama",
            $"Detrás del número de unidad de «{seccion.Titulo}» hay «{nombre}» y no la del nombre.");

        Assert.IsNotEmpty(seccion.Filas, $"«{seccion.Titulo}» salió sin ninguna fila que mirar.");

        foreach (var fila in seccion.Filas)
        {
            Assert.AreEqual(
                NumeroConCeroDelante, fila[donde],
                $"«{seccion.Titulo}»: la columna del número trae «{fila[donde]}».");
            Assert.AreEqual(
                NombreDeLaUnidad, fila[donde + 1],
                $"«{seccion.Titulo}»: la columna del nombre trae «{fila[donde + 1]}».");
        }
    }

    private static int IndiceDe(Seccion seccion, string rotulo)
    {
        for (var i = 0; i < seccion.Columnas.Count; i++)
        {
            if (seccion.Columnas[i].Nombre == rotulo) return i;
        }
        return -1;
    }

    private static bool TieneLaColumna(Seccion seccion, string rotulo) => IndiceDe(seccion, rotulo) >= 0;

    private static Seccion Seccion(Documento documento, string titulo)
    {
        var seccion = documento.Secciones.FirstOrDefault(s => s.Titulo == titulo);
        Assert.IsNotNull(seccion, $"El informe no trae ninguna sección «{titulo}».");
        return seccion;
    }

    /// <summary>Lo que devuelve Excel en cada celda de la columna del número, hoja por hoja.</summary>
    /// <remarks>
    /// Se lee con <c>GetString()</c> y no con <c>Value</c>: es lo que ve quien abre el archivo y
    /// filtra, que es justamente lo que el dueño pidió poder hacer.
    /// </remarks>
    private static List<string> NumerosDeUnidadDelLibro(string ruta)
    {
        var leidos = new List<string>();
        using var libro = new XLWorkbook(ruta);

        foreach (var hoja in libro.Worksheets)
        {
            var cabecera = hoja.Row(1).CellsUsed().FirstOrDefault(c => c.GetString() == RotuloDelNumero);
            if (cabecera is null) continue;

            var columna = cabecera.Address.ColumnNumber;
            for (var fila = 2; fila <= hoja.LastRowUsed()?.RowNumber(); fila++)
            {
                var texto = hoja.Cell(fila, columna).GetString();
                if (!string.IsNullOrWhiteSpace(texto)) leidos.Add(texto);
            }
        }

        return leidos;
    }

    // ---- el escenario -------------------------------------------------------

    private const string Generado = BaseDePrueba.Hoy + " 10:00:00";

    private static string RutaTemporal(string nombre)
        => Path.Combine(Path.GetTempPath(), "fichas-pruebas-reportes", $"{Guid.NewGuid():N}-{nombre}");

    private sealed record Mundo(
        ReportesEnPdf Reportes, Companero Sandy, IReadOnlyList<IntentoAnterior> Intentos);

    /// <summary>
    /// Cuatro casos de la MISMA unidad, uno archivado, todos de Sandy y ninguno preparado.
    /// </summary>
    /// <remarks>
    /// Todos comparten unidad a propósito: así «Unidades con preparaciones sin completar» sale
    /// con UNA fila, y si el agrupado volviera a hacerse por la cadena pegada seguiría saliendo
    /// con una —lo que la delata es el contenido de las dos columnas, no cuántas filas hay—.
    /// Ninguna persona lleva un paso contestado, que es lo que hace que esa tabla exista.
    ///
    /// Ningún dato es de nadie: los nombres son inventados y el número de unidad también.
    /// </remarks>
    private static Mundo Escenario()
    {
        var servicios = new ServiciosFalsos(0, 11, new BaseDePrueba.RelojFijo(BaseDePrueba.Hoy));
        var almacen = servicios.Almacen;
        almacen.Companeros.Clear();

        var sandy = new Companero
        {
            Id = almacen.SiguienteId(), Nombre = "Sandy", Activo = true, CreadoEn = "2026-09-01 08:00:00",
        };
        almacen.Companeros[sandy.Id] = sandy;

        // Dos que no pudieron viajar y dos que sí: con las cuatro del mismo lado, una de las
        // dos Partes saldría VACÍA y su comprobación pasaría en verde sin mirar ninguna fila.
        var deLaSegundaVuelta = Caso(almacen, sandy, "CASP2001", archivado: false, pudoViajar: false);
        Caso(almacen, sandy, "CASP2002", archivado: false, pudoViajar: false);
        Caso(almacen, sandy, "CASP2003", archivado: false, pudoViajar: true);
        Caso(almacen, sandy, "CASP2004", archivado: true, pudoViajar: true);

        return new Mundo(
            new ReportesEnPdf(
                servicios.Casos, servicios.Personas, servicios.Companeros,
                servicios.Asignaciones, servicios.Procedencia, servicios.Reloj),
            sandy,
            [new IntentoAnterior(deLaSegundaVuelta, "Sandy", 1, MotivoDeNoCompletar.NoSePudoComunicar)]);
    }

    /// <summary>Un caso de la unidad del escenario, con su persona y su asignación viva.</summary>
    private static long Caso(
        AlmacenFalso almacen, Companero quien, string numero, bool archivado, bool pudoViajar)
    {
        var casoId = almacen.SiguienteId();
        almacen.Casos[casoId] = new Caso
        {
            Id = casoId,
            NumeroCaso = numero,
            UnidadNombre = NombreDeLaUnidad,
            UnidadNumero = NumeroConCeroDelante,
            FechaViaje = "2026-09-15",
            CreadoEn = "2026-09-01 09:00:00",
            EstadoRecomendacion = "no_completa",
            Archivado = archivado,
            FechaArchivado = archivado ? "2026-09-18" : null,
        };

        var personaId = almacen.SiguienteId();
        almacen.Personas[personaId] = new Persona
        {
            Id = personaId,
            CasoId = casoId,
            Nombre = $"Persona de {numero}",
            Mrn = $"055-1000-{casoId:0000}",
            PudoViajar = pudoViajar,
            MotivoNoViajo = pudoViajar ? null : "La recomendación venció la semana antes del viaje.",
        };

        var asignacionId = almacen.SiguienteId();
        almacen.Asignaciones[asignacionId] = new Asignacion
        {
            Id = asignacionId,
            CasoId = casoId,
            CompaneroId = quien.Id,
            AsignadoEn = "2026-09-02 09:00:00",
            Activa = true,
        };

        return casoId;
    }
}
