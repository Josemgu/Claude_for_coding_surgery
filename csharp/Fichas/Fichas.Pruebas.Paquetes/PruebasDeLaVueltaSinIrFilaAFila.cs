using System.Diagnostics;
using ClosedXML.Excel;
using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;
using Fichas.Paquetes;

namespace Fichas.Pruebas.Paquetes;

/// <summary>
/// R-7 (PENDIENTES.md, plan del 2026-09-15): una hoja que vuelve SIN id de caso no lanza una
/// lista con filtro de texto por cada fila.
/// </summary>
/// <remarks>
/// <para>El camino con id no cambia y no se mide aquí: con el id, <c>PersonasQueCasan</c> va
/// directo a las personas del caso. El que se mide es el viejo —hojas generadas antes del
/// 2026-09-03— donde cada fila resolvía su número de caso con <c>Listar(Texto: número)</c>.
/// Con 300 filas eran 300 listas (medido sobre <c>master</c> el 2026-09-15); contra SQLite cada
/// una son dos consultas con <c>LIKE</c> sobre casos y personas.</para>
///
/// <para>Las dos pruebas de abajo que no cuentan llamadas fijan lo que NO puede cambiar al
/// dejar de preguntar fila a fila: el archivado se sigue encontrando y el número se sigue
/// comparando sin distinguir mayúsculas.</para>
/// </remarks>
[TestClass]
public class PruebasDeLaVueltaSinIrFilaAFila
{
    /// <summary>Los casos del requisito 5 del dueño.</summary>
    private const int TresMil = 3_000;

    /// <summary>«Me enviaron 300»: las filas que vuelven en la hoja vieja.</summary>
    private const int Trescientas = 300;

    /// <summary>Con una sola lista por vuelta basta: los casos se leen una vez y se buscan por número en memoria.</summary>
    private const int ListasQueBastan = 1;

    /// <summary>La base en memoria, el contador delante de los casos y los paquetes montados encima.</summary>
    private BaseInventada _base = null!;
    /// <summary>El contador que envuelve al repositorio de casos de <see cref="_base"/>.</summary>
    private CasosQueSeCuentan _casos = null!;
    /// <summary>Los paquetes montados sobre el contador; los de <see cref="_base"/> no cuentan nada.</summary>
    private Fichas.Paquetes.Paquetes _paquetes = null!;
    /// <summary>La carpeta temporal de los <c>.xlsx</c>; se borra al terminar.</summary>
    private string _carpeta = null!;
    /// <summary>El compañero que devuelve la hoja.</summary>
    private long _sandy;

    /// <summary>Monta la base, el contador y los paquetes de cada prueba.</summary>
    [TestInitialize]
    public void Preparar()
    {
        _base = new BaseInventada();
        _casos = new CasosQueSeCuentan(new RepositorioDeCasosFalso(_base.Almacen));
        _paquetes = new Fichas.Paquetes.Paquetes(
            _casos,
            new RepositorioDePersonasFalso(_base.Almacen),
            new RepositorioDeCompanerosFalso(_base.Almacen),
            new RepositorioDeIlegiblesFalso(_base.Almacen),
            _base.Reloj);
        _carpeta = BaseInventada.CarpetaDePruebas();
        _sandy = _base.Companero("Sandy");
    }

    /// <summary>Borra la carpeta temporal.</summary>
    [TestCleanup]
    public void Recoger()
    {
        try { Directory.Delete(_carpeta, recursive: true); }
        catch (IOException) { /* si Windows todavía tiene la manija, la carpeta temporal la limpia el sistema */ }
    }

    /// <summary>Vigila que aplicar 300 marcas sin id sobre 3 000 casos pide UNA lista y no 300, e imprime listas y ms.</summary>
    [TestMethod]
    public void TrescientasMarcasSinIdSobreTresMilCasosNoLanzanUnaListaPorFila()
    {
        var personas = DarDeAltaTresMil();
        var marcas = personas.Take(Trescientas)
            .Select((p, i) => MarcaSinId(p.Numero, p.Mrn, filaExcel: Columnas.PrimeraFilaDeDatos + i))
            .ToList();

        var reloj = Stopwatch.StartNew();
        var aplicado = _paquetes.AplicarMarcas(marcas, _sandy, Path.Combine(_carpeta, "vieja.xlsx"));
        reloj.Stop();

        Console.WriteLine(
            $"MEDIDO · aplicar {Trescientas} marcas sin id con {TresMil} casos: {_casos.Listas} listas "
            + $"({_casos.ListasConTexto} con filtro de texto) · {reloj.Elapsed.TotalMilliseconds:F0} ms");

        Assert.IsTrue(aplicado.SeEscribio);
        StringAssert.StartsWith(aplicado.Avisos[0].Linea, $"Aplicadas {Trescientas} fila(s); descartadas 0;");
        Assert.IsEmpty(_base.Almacen.Descartadas);
        Assert.IsLessThanOrEqualTo(ListasQueBastan, _casos.Listas, "los casos se leen una vez por vuelta, no una por fila");
        Assert.AreEqual(0, _casos.ListasConTexto, "y sin filtro de texto: el número se compara entero, no con LIKE");
    }

    /// <summary>Vigila que leer una hoja vieja de 300 filas —sin id en la clave— también pide una sola lista.</summary>
    [TestMethod]
    public void LeerUnaHojaViejaDeTrescientasFilasTampocoLanzaUnaListaPorFila()
    {
        var personas = DarDeAltaTresMil();
        var ruta = Path.Combine(_carpeta, "vieja.xlsx");
        var casoIds = personas.Take(Trescientas).Select(p => p.CasoId).Distinct().ToList();
        Assert.IsTrue(_paquetes.GenerarExcelDeCompanero(_sandy, casoIds, ruta).SeEscribio);
        QuitarElIdDeLaClaveYContestar(ruta);
        var listasAntesDeLeer = _casos.Listas;

        var reloj = Stopwatch.StartNew();
        var vuelta = _paquetes.LeerExcelDevuelto(ruta, _sandy);
        reloj.Stop();

        var listasAlLeer = _casos.Listas - listasAntesDeLeer;
        Console.WriteLine($"MEDIDO · leer una hoja vieja de {vuelta.Marcas.Count} filas: {listasAlLeer} listas · {reloj.Elapsed.TotalMilliseconds:F0} ms");

        Assert.HasCount(Trescientas, vuelta.Marcas);
        Assert.HasCount(Trescientas, vuelta.Marcas.Select(m => m.CasoId).Distinct().ToList(), "cada fila casó con su caso, resuelto por número y MRN");
        Assert.IsEmpty(vuelta.Descartadas);
        Assert.IsLessThanOrEqualTo(ListasQueBastan, listasAlLeer);
    }

    /// <summary>Vigila que, sin id, el caso archivado se sigue encontrando: el compañero pudo recibirlo antes de archivarse.</summary>
    [TestMethod]
    public void SinIdElCasoArchivadoSeSigueEncontrando()
    {
        var caso = _base.Caso("BALC2609");
        var persona = _base.Persona(caso, "Elena", "055-1111-385A");
        _base.Almacen.Casos[caso] = _base.Almacen.Casos[caso] with { Archivado = true, FechaArchivado = "2026-09-01" };

        var aplicado = _paquetes.AplicarMarcas([MarcaSinId("BALC2609", "055-1111-385A", filaExcel: 7)], _sandy, "vieja.xlsx");

        Assert.IsTrue(aplicado.SeEscribio);
        Assert.AreEqual(_sandy, _base.Almacen.Personas[persona].PropuestoPor);
        Assert.IsEmpty(_base.Almacen.Descartadas);
    }

    /// <summary>Vigila que, sin id, el número se compara sin distinguir mayúsculas, como hacía el filtro de texto.</summary>
    [TestMethod]
    public void SinIdElNumeroSeComparaSinDistinguirMayusculas()
    {
        var caso = _base.Caso("BALC2609");
        var persona = _base.Persona(caso, "Elena", "055-1111-385A");

        var aplicado = _paquetes.AplicarMarcas([MarcaSinId("balc2609", "055-1111-385A", filaExcel: 7)], _sandy, "vieja.xlsx");

        Assert.IsTrue(aplicado.SeEscribio);
        Assert.AreEqual(_sandy, _base.Almacen.Personas[persona].PropuestoPor, "«balc2609» es BALC2609");
        Assert.IsEmpty(_base.Almacen.Descartadas);
    }

    /// <summary>Vigila que, sin id, el número se compara ENTERO: un caso cuyo número contiene al buscado no casa.</summary>
    /// <remarks>Es lo que el código de hoy hacía letra por letra después del filtro; al dejar el filtro, no puede perderse.</remarks>
    [TestMethod]
    public void SinIdUnNumeroQueContieneAlBuscadoNoCasa()
    {
        var masLargo = _base.Caso("BALC2609");
        var laOtra = _base.Persona(masLargo, "Otra", "055-1111-385A");
        var exacto = _base.Caso("BALC260");
        var elena = _base.Persona(exacto, "Elena", "055-1111-385A");

        var aplicado = _paquetes.AplicarMarcas([MarcaSinId("BALC260", "055-1111-385A", filaExcel: 7)], _sandy, "vieja.xlsx");

        Assert.IsTrue(aplicado.SeEscribio);
        Assert.AreEqual(_sandy, _base.Almacen.Personas[elena].PropuestoPor, "el exacto casa");
        Assert.IsNull(_base.Almacen.Personas[laOtra].PropuestoPor, "el que solo lo contiene no casa, y por eso no hay ambigüedad");
        Assert.IsEmpty(_base.Almacen.Descartadas);
    }

    /// <summary>Una persona dada de alta para estas pruebas: su caso, su número y su MRN.</summary>
    /// <param name="CasoId">El id del caso.</param>
    /// <param name="Numero">El número del caso, distinto para cada uno.</param>
    /// <param name="Mrn">El MRN de la persona, distinto para cada una.</param>
    private sealed record PersonaDeAlta(long CasoId, string Numero, string Mrn);

    /// <summary>Da de alta 3 000 casos con números distintos y una persona cada uno.</summary>
    /// <returns>Las personas, en el orden de alta.</returns>
    private List<PersonaDeAlta> DarDeAltaTresMil()
    {
        var personas = new List<PersonaDeAlta>(TresMil);
        for (var numero = 0; numero < TresMil; numero++)
        {
            var numeroDeCaso = $"C{numero:D4}2609";
            var mrn = $"055-{numero:D4}-0001";
            var caso = _base.Caso(numeroDeCaso);
            _base.Persona(caso, $"Persona {numero}", mrn);
            personas.Add(new PersonaDeAlta(caso, numeroDeCaso, mrn));
        }

        return personas;
    }

    /// <summary>Una marca como la que deja una hoja vieja: número y MRN, sin id, con los seis pasos en «sí».</summary>
    /// <param name="numeroCaso">El número tal como vino en la clave.</param>
    /// <param name="mrn">El MRN tal como vino en la clave.</param>
    /// <param name="filaExcel">La fila de la hoja de la que sale.</param>
    private static MarcaDelCompanero MarcaSinId(string numeroCaso, string mrn, int filaExcel)
        => new(
            numeroCaso, mrn, null,
            EstadoDeRecomendacion.Completa, null, null,
            true, true, true, true, true, true, true,
            filaExcel);

    /// <summary>Deja la hoja como una generada antes del 2026-09-03: la clave sin el id, y las respuestas en «Sí».</summary>
    /// <param name="ruta">El <c>.xlsx</c> recién generado; se reescribe en sitio.</param>
    private static void QuitarElIdDeLaClaveYContestar(string ruta)
    {
        using var libro = new XLWorkbook(ruta);
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        var columnaDeLaClave = Columnas.IndiceDe(Columnas.ColumnaDeLaClave);
        for (var fila = Columnas.PrimeraFilaDeDatos; fila < Columnas.PrimeraFilaDeDatos + Trescientas; fila++)
        {
            var partes = hoja.Cell(fila, columnaDeLaClave).GetString().Split(Columnas.SeparadorDeLaClave);
            hoja.Cell(fila, columnaDeLaClave).SetValue(Columnas.ArmarLaClave(partes[0], partes[1], null));
            foreach (var columna in Columnas.Todas.Where(c => c.EsRespuesta))
                hoja.Cell(fila, Columnas.IndiceDe(columna.Nombre)).SetValue("Sí");
        }

        libro.Save();
    }
}
