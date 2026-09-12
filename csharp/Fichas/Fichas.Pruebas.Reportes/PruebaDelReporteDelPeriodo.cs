using System.Text;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Reportes;
using Fichas.Reportes.Reglas;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// El criterio de la C8 de punta a punta: con Fichas.Datos.Falso, el reporte de un mes
/// sale en PDF, ese PDF abre, y sus cifras son las que dan los contratos.
/// </summary>
[TestClass]
public class PruebaDelReporteDelPeriodo
{
    /// <summary>Los casos de la base falsa en estas pruebas; bastan para que todas las secciones tengan filas.</summary>
    private const int CuantosCasos = 300;

    /// <summary>El motor sobre la base falsa con reloj fijo.</summary>
    /// <param name="servicios">Los servicios falsos, para contar aparte sobre los puertos.</param>
    /// <param name="casos">Cuántos casos genera la base.</param>
    private static ReportesEnPdf Montar(out Fichas.Datos.Falso.ServiciosFalsos servicios, int casos = CuantosCasos)
    {
        servicios = BaseDePrueba.Montar(casos);
        return new ReportesEnPdf(
            servicios.Casos, servicios.Personas, servicios.Companeros,
            servicios.Asignaciones, servicios.Procedencia, servicios.Reloj);
    }

    /// <summary>Una ruta única en la carpeta temporal, para que dos pruebas en paralelo no se pisen.</summary>
    /// <param name="nombre">Cómo acaba el archivo.</param>
    private static string RutaTemporal(string nombre)
        => Path.Combine(Path.GetTempPath(), "fichas-pruebas-reportes", $"{Guid.NewGuid():N}-{nombre}");

    // ---- el PDF existe y abre ------------------------------------------------

    /// <summary>Vigila que el reporte del mes se escribe y el archivo tiene cabecera, catálogo, startxref y %%EOF.</summary>
    [TestMethod]
    public void GenerarElReporteDeUnMesEscribeUnPdfQueAbre()
    {
        var reportes = Montar(out _);
        var ruta = RutaTemporal("periodo.pdf");

        var resultado = reportes.GenerarReporteDelPeriodo("2026-09-01", "2026-09-30", ruta);

        Assert.IsTrue(resultado.SeEscribio, string.Join(" · ", resultado.Avisos.Select(a => a.Linea)));
        Assert.IsTrue(File.Exists(ruta));

        var bytes = File.ReadAllBytes(ruta);
        var texto = Encoding.Latin1.GetString(bytes);
        StringAssert.StartsWith(texto, "%PDF-1.4");
        StringAssert.EndsWith(texto, "%%EOF\n");
        Assert.IsGreaterThan(2000, bytes.Length, $"Un PDF de {bytes.Length} bytes no lleva un informe dentro.");
        StringAssert.Contains(texto, "/Type /Catalog");
        StringAssert.Contains(texto, "startxref");

        File.Delete(ruta);
    }

    /// <summary>Vigila que un periodo del revés no deja archivo y devuelve el aviso, sin lanzar.</summary>
    [TestMethod]
    public void UnPeriodoDelRevesNoEscribeNadaYLoDice_NoLanza()
    {
        var reportes = Montar(out _);
        var ruta = RutaTemporal("del-reves.pdf");

        var resultado = reportes.GenerarReporteDelPeriodo("2026-09-30", "2026-09-01", ruta);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.IsTrue(resultado.HayAvisos);
        Assert.IsFalse(File.Exists(ruta), "No se escribe un PDF con todos los numeros a cero.");
    }

    /// <summary>Vigila que una unidad de disco inexistente devuelve un problema, sin lanzar.</summary>
    [TestMethod]
    public void UnaRutaImposibleSeAvisa_NoLanza()
    {
        var reportes = Montar(out _);

        var resultado = reportes.GenerarReporteDelPeriodo(
            "2026-09-01", "2026-09-30", @"Z:\no-existe-esta-unidad\informe.pdf");

        Assert.IsFalse(resultado.SeEscribio);
        Assert.AreEqual(GravedadDeAviso.Problema, resultado.Avisos[0].Gravedad);
    }

    // ---- las cifras son las que dan los contratos ---------------------------

    /// <summary>Vigila que las cuatro cifras de la portada coinciden con la cuenta hecha aparte sobre los puertos.</summary>
    [TestMethod]
    public void LasCuatroCifrasDeLaPortadaCuadranConLoQueDicenLosPuertos()
    {
        var reportes = Montar(out var servicios);
        var periodo = Periodo.Leer("2026-09-01", "2026-09-30").Periodo!;

        var documento = reportes.DocumentoDelPeriodo(periodo, $"{BaseDePrueba.Hoy} 10:00:00");

        // Lo que dicen los puertos, contado aparte y sin pasar por Fichas.Reportes.
        var casosDelPeriodo = TodosLosCasos(servicios.Casos)
            .Where(c => c.FechaViaje is not null
                        && string.CompareOrdinal(c.FechaViaje, "2026-09-01") >= 0
                        && string.CompareOrdinal(c.FechaViaje, "2026-09-30") <= 0)
            .ToList();
        var personasDelPeriodo = casosDelPeriodo.SelectMany(c => servicios.Personas.DeCaso(c.Id)).ToList();
        var porCaso = casosDelPeriodo.ToDictionary(c => c.Id, c => c.FechaViaje!);

        var yaViajaron = personasDelPeriodo
            .Where(p => string.CompareOrdinal(porCaso[p.CasoId], BaseDePrueba.Hoy) < 0)
            .ToList();
        var completas = yaViajaron.Count(EsCompleta);

        Assert.HasCount(4, documento.Portada.Cifras);
        Assert.AreEqual(yaViajaron.Count - completas, documento.Portada.Cifras[0].Numero, "viajaron sin la preparación completa");
        Assert.AreEqual(completas, documento.Portada.Cifras[1].Numero, "viajaron con la preparación completa");
        Assert.AreEqual(personasDelPeriodo.Count - yaViajaron.Count, documento.Portada.Cifras[2].Numero, "por viajar todavía");
        Assert.AreEqual(casosDelPeriodo.Count, documento.Portada.Cifras[3].Numero, "casos en el período");
    }

    /// <summary>Vigila que el titular abre con «N de las M personas» y no lleva «%».</summary>
    [TestMethod]
    public void ElTitularDiceElDenominadorYNoLlevaPorcentajes()
    {
        var reportes = Montar(out _);
        var documento = reportes.DocumentoDelPeriodo(
            Periodo.Leer("2026-09-01", "2026-09-30").Periodo!, $"{BaseDePrueba.Hoy} 10:00:00");

        var frase = documento.Portada.Frase;
        var perdidas = documento.Portada.Cifras[0].Numero;
        var viajaron = perdidas + documento.Portada.Cifras[1].Numero;

        StringAssert.StartsWith(frase, $"{perdidas} de las {viajaron} personas");
        Assert.DoesNotContain("%", frase);
    }

    /// <summary>Vigila que la fila de la métrica 3 y la nota de la sección dicen los denominadores.</summary>
    [TestMethod]
    public void LaMetricaTresDiceSuDenominadorYSusCincoCubos()
    {
        var reportes = Montar(out _);
        var documento = reportes.DocumentoDelPeriodo(
            Periodo.Leer("2026-09-01", "2026-09-30").Periodo!, $"{BaseDePrueba.Hoy} 10:00:00");

        var metricas = documento.Secciones.Single(s => s.Titulo == "Métricas del trabajo del equipo");
        var comoSeCuenta = metricas.Filas[2][2]!;

        StringAssert.Contains(comoSeCuenta, "casos que viajaban en el período");
        StringAssert.Contains(comoSeCuenta, "tienen un problema escrito");
        StringAssert.Contains(metricas.Notas[1], "Casos del período por fecha de viaje:");
    }

    /// <summary>Vigila que ninguna cadena del documento nombra transporte, alimentos, alojamiento, costos ni «$».</summary>
    [TestMethod]
    public void NingunaSeccionHablaDeDinero()
    {
        // Citado del viejo: el presupuesto lo lleva otro departamento. Se comprueba
        // recorriendo cada cadena del documento, no fiandose del comentario.
        var reportes = Montar(out _);
        var documento = reportes.DocumentoDelPeriodo(
            Periodo.Leer("2026-09-01", "2026-09-30").Periodo!, $"{BaseDePrueba.Hoy} 10:00:00");

        string[] prohibidas = ["Transporte", "Alimentos", "Alojamiento", "Costos totales", "Contribución del miembro", "$"];
        foreach (var texto in TodosLosTextos(documento))
        {
            foreach (var prohibida in prohibidas)
            {
                Assert.IsFalse(texto.Contains(prohibida, StringComparison.OrdinalIgnoreCase),
                    $"El informe a la dirección no habla de dinero, y dice «{prohibida}» en: {texto}");
            }
        }
    }

    /// <summary>
    /// Las tres secciones del viejo siguen ahi, y con las palabras que el dueno pidio.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Esta prueba comprobaba lo CONTRARIO hasta el 2026-09-05 y se llamaba
    /// <c>NoSeDiceVerificadaDondeSonLosSeisPasos</c>.</b> Exigia el criterio C8-2: que
    /// «verificada» no se dijera en estas tres tablas, para que la palabra no significara dos
    /// cosas en el mismo programa. <b>La invirtio el dueno</b> (<c>DECISIONES.md</c>,
    /// 2026-09-04): <i>«si el informe de los jefes como el viejo está bien»</i>, y el informe
    /// del viejo decia «Verificadas», «Sin verificar» y «Quiénes viajaron sin verificar».
    ///
    /// No se borra ni se deja pasando en verde sin mirar nada: se le da la vuelta, para que la
    /// suite siga vigilando estas tres secciones —que sigan existiendo y que no se les cambien
    /// las palabras otra vez sin que nadie se entere—. Lo que se conserva del C8-2, la
    /// aclaracion de que aqui son los seis pasos y no la firma de Miguel, lo vigila
    /// <c>PruebaDelPapelYLosRotulosDelViejo</c>.
    /// </remarks>
    [TestMethod]
    public void LasTresSeccionesDelViejoLlevanLosRotulosDelViejo()
    {
        var reportes = Montar(out _);
        var documento = reportes.DocumentoDelPeriodo(
            Periodo.Leer("2026-09-01", "2026-09-30").Periodo!, $"{BaseDePrueba.Hoy} 10:00:00");

        var delViejo = documento.Secciones
            .Where(s => s.Titulo is "Quiénes viajaron sin verificar" or "Los viajes" or "El equipo")
            .ToList();

        Assert.HasCount(3, delViejo);

        var rotulos = delViejo.SelectMany(s => s.Columnas).Select(c => c.Nombre).ToList();
        CollectionAssert.Contains(rotulos, "Verificadas");
        CollectionAssert.Contains(rotulos, "Sin verificar");
    }

    // ---- criterio C8-3: los archivados siguen contando ----------------------

    /// <summary>Vigila que archivar un caso del periodo deja iguales la portada y el tamaño de cada sección (C8-3).</summary>
    [TestMethod]
    public void ArchivarUnCasoDelPeriodoNoCambiaNingunTotalDelReporte()
    {
        var reportes = Montar(out var servicios);
        var periodo = Periodo.Leer("2026-09-01", "2026-09-30").Periodo!;
        var antes = reportes.DocumentoDelPeriodo(periodo, $"{BaseDePrueba.Hoy} 10:00:00");

        var victima = TodosLosCasos(servicios.Casos)
            .First(c => !c.Archivado && c.FechaViaje is not null
                        && string.CompareOrdinal(c.FechaViaje, "2026-09-01") >= 0
                        && string.CompareOrdinal(c.FechaViaje, "2026-09-30") <= 0);
        Assert.IsTrue(servicios.Casos.Archivar(victima.Id, true, BaseDePrueba.Hoy).SeEscribio);

        var despues = reportes.DocumentoDelPeriodo(periodo, $"{BaseDePrueba.Hoy} 10:00:00");

        CollectionAssert.AreEqual(
            antes.Portada.Cifras.Select(c => c.Numero).ToArray(),
            despues.Portada.Cifras.Select(c => c.Numero).ToArray(),
            "Archivar no borra nada: los archivados siguen contando en los reportes (C8-3).");

        for (var i = 0; i < antes.Secciones.Count; i++)
        {
            Assert.HasCount(antes.Secciones[i].Filas.Count, despues.Secciones[i].Filas,
                $"La sección «{antes.Secciones[i].Titulo}» cambió de tamaño al archivar.");
        }
    }

    // ---- un caso sin numero no revienta el reporte entero -------------------

    /// <summary>Vigila que con todos los casos del mes sin número el PDF se escribe y el aviso nombra al caso sin número.</summary>
    [TestMethod]
    public void UnCasoSinNumeroNoRevientaElReporteNiElPdf()
    {
        var reportes = Montar(out var servicios);

        // Se deja SIN numero a todos los casos que viajan en el mes, que es el peor caso.
        foreach (var caso in TodosLosCasos(servicios.Casos).Where(c => c.FechaViaje?.StartsWith("2026-09") == true))
        {
            servicios.Almacen.Casos[caso.Id] = caso with { NumeroCaso = null };
        }
        // Y ademas alguien sin fecha de viaje que no pudo viajar, que es lo que dispara el aviso.
        var suelto = servicios.Almacen.Casos.Values.First(c => c.FechaViaje is null);
        servicios.Almacen.Casos[suelto.Id] = suelto with { NumeroCaso = null };
        foreach (var persona in servicios.Almacen.PersonasDe(suelto.Id))
        {
            servicios.Almacen.Personas[persona.Id] = persona with { PudoViajar = false, MotivoNoViajo = "Sin fecha." };
        }

        var ruta = RutaTemporal("sin-numero.pdf");
        var resultado = reportes.GenerarReporteDelPeriodo("2026-09-01", "2026-09-30", ruta);

        Assert.IsTrue(resultado.SeEscribio, string.Join(" · ", resultado.Avisos.Select(a => a.Linea)));
        Assert.IsTrue(File.Exists(ruta));

        var documento = reportes.DocumentoDelPeriodo(
            Periodo.Leer("2026-09-01", "2026-09-30").Periodo!, $"{BaseDePrueba.Hoy} 10:00:00");
        Assert.IsTrue(documento.Avisos.Any(a => a.Contains(Avisos.SinNumeroDeCaso)),
            "El aviso tiene que nombrar al caso sin número, que es el que más falta hace mirar.");

        File.Delete(ruta);
    }

    // ---- ayudas -------------------------------------------------------------

    /// <summary>Los seis pasos en sí, escrito aparte para no fiarse de <c>Pasos.Estado</c> al contar contra los puertos.</summary>
    /// <param name="p">La persona.</param>
    private static bool EsCompleta(Persona p)
        => p.PasoPreparacion == true && p.PasoInformacion == true && p.PasoCitaDelTemplo == true
           && p.PasoAccionesRequeridas == true && p.PasoEntrevistas == true && p.PasoListoParaElTemplo == true;

    /// <summary>Todos los casos del puerto, archivados incluidos, en una sola página.</summary>
    /// <param name="casos">El puerto de casos.</param>
    private static List<Caso> TodosLosCasos(ICasos casos)
        => casos.Listar(new FiltroDeCasos(IncluirArchivados: true), new Pagina(0, int.MaxValue)).Elementos.ToList();

    /// <summary>Cada cadena que se imprime en el documento, una a una, para poder buscar palabras prohibidas.</summary>
    /// <param name="documento">El documento armado.</param>
    private static IEnumerable<string> TodosLosTextos(Fichas.Reportes.Modelo.Documento documento)
    {
        yield return documento.Titulo;
        yield return documento.Subtitulo;
        yield return documento.Portada.Titular;
        yield return documento.Portada.Frase;
        foreach (var cifra in documento.Portada.Cifras) yield return cifra.Rotulo;
        foreach (var aviso in documento.Avisos) yield return aviso;
        foreach (var seccion in documento.Secciones)
        {
            yield return seccion.Titulo;
            if (seccion.Resumen is not null) yield return seccion.Resumen;
            foreach (var nota in seccion.Notas) yield return nota;
            foreach (var columna in seccion.Columnas) yield return columna.Nombre;
            foreach (var fila in seccion.Filas)
            {
                foreach (var valor in fila) if (valor is not null) yield return valor;
            }
        }
    }
}
