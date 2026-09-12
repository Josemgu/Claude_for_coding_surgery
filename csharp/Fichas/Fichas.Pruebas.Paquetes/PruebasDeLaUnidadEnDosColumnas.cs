using ClosedXML.Excel;
using Fichas.Paquetes;

namespace Fichas.Pruebas.Paquetes;

/// <summary>
/// El número de la unidad y el nombre de la unidad, cada uno en SU columna.
/// </summary>
/// <remarks>
/// <para><b>Lo pidió el dueño el 2026-09-07:</b> <i>«En el paquete que se prepara para los
/// agentes debe estar el número de unidad en un lado y al otro el nombre de la unidad».</i></para>
///
/// <para>⚠️ <b>Antes de esto el número SÍ salía, pero pegado dentro de la misma celda</b> —
/// «Cuatricentenaria (7000014)», que armaba <c>Paquetes.UnidadConSuNumero</c>—. Por eso estas
/// pruebas no comprueban que el número «aparezca»: comprueban que sale <b>solo</b> en su
/// columna y que la del nombre ya no lo lleva pegado. Una prueba que solo buscara el número
/// habría pasado en verde antes del cambio.</para>
///
/// <para>Los criterios salen de las palabras del dueño, no del código: dos columnas, cada una
/// con su valor, y las dos con el valor que guarda la base sin tocarlo.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaUnidadEnDosColumnas
{
    /// <summary>Dado que la hoja se define, existen las dos columnas y son distintas.</summary>
    [TestMethod]
    public void LaHojaTieneLaColumnaDelNumeroYLaDelNombreYNoSonLaMisma()
    {
        var numero = Columnas.Por("unidad_numero");
        var nombre = Columnas.Por("unidad_nombre");

        Assert.AreEqual("Número de unidad", numero.Titulo);
        Assert.AreEqual("Barrio o rama", nombre.Titulo);
        Assert.AreNotEqual(Columnas.IndiceDe("unidad_numero"), Columnas.IndiceDe("unidad_nombre"));
    }

    /// <summary>El número va al lado del nombre: se leen de un vistazo, no en dos extremos.</summary>
    [TestMethod]
    public void LasDosVanPegadasYElNumeroDelante()
        => Assert.AreEqual(
            Columnas.IndiceDe("unidad_nombre") - 1,
            Columnas.IndiceDe("unidad_numero"),
            "el número va justo antes del nombre: «el número de unidad en un lado y al otro el nombre»");

    /// <summary>
    /// El número entra como TEXTO, como el caso y el MRN: Excel no le puede comer un cero.
    /// </summary>
    [TestMethod]
    public void ElNumeroDeUnidadEntraComoTextoParaQueExcelNoLoNormalice()
        => Assert.AreEqual(ClaseDeColumna.Texto, Columnas.Por("unidad_numero").Clase);

    /// <summary>
    /// Dado un caso con nombre y número, cuando sale la hoja, cada uno está en su celda.
    /// </summary>
    /// <remarks>
    /// La comprobación que de verdad cierra el encargo es la segunda: que la celda del nombre
    /// NO lleve el número pegado. Es lo único que distingue esta hoja de la anterior.
    /// </remarks>
    [TestMethod]
    public void CadaValorSaleEnSuCeldaYElNombreYaNoLlevaElNumeroPegado()
    {
        var hoja = HojaDeUnaFila("Cuatricentenaria", "7000014");

        Assert.AreEqual("7000014", hoja.Cell(7, Columnas.IndiceDe("unidad_numero")).GetString());
        Assert.AreEqual("Cuatricentenaria", hoja.Cell(7, Columnas.IndiceDe("unidad_nombre")).GetString());
    }

    /// <summary>
    /// Sin número guardado, la celda dice «no consta» y no se queda en blanco.
    /// </summary>
    /// <remarks>
    /// Un blanco en una hoja que el compañero rellena a mano se lee como «esto lo pongo yo».
    /// Es la misma regla que ya tenían las demás columnas que no son respuesta.
    /// </remarks>
    [TestMethod]
    public void SinNumeroGuardadoLaCeldaLoDiceConPalabras()
    {
        var hoja = HojaDeUnaFila("Cuatricentenaria", null);

        Assert.AreEqual(Columnas.SinDato, hoja.Cell(7, Columnas.IndiceDe("unidad_numero")).GetString());
    }

    /// <summary>El ancho deja ver los siete dígitos y el rótulo entero.</summary>
    [TestMethod]
    public void LaColumnaDelNumeroEsAnchaComoSuRotulo()
        => Assert.AreEqual(16, Columnas.AnchoDe("unidad_numero"), "«Número de unidad» son 16 caracteres");

    /// <summary>Una hoja de una sola fila con esa unidad, escrita por el motor de verdad.</summary>
    /// <param name="unidadNombre">El nombre del barrio o rama, o nulo.</param>
    /// <param name="unidadNumero">El número de la unidad, o nulo para ver qué sale sin él.</param>
    private static IXLWorksheet HojaDeUnaFila(string? unidadNombre, string? unidadNumero)
        => LibroDeTrabajo.Construir(
            [
                new FilaDeTrabajo
                {
                    NumeroCaso = "BALC2609",
                    FechaViaje = "2026-10-15",
                    Templo = "Santo Domingo",
                    UnidadNombre = unidadNombre,
                    UnidadNumero = unidadNumero,
                    Nombre = "Persona de prueba A",
                    Mrn = "055-1111-3853",
                    AQueVa = "Investidura",
                    Clave = Columnas.ArmarLaClave("BALC2609", "055-1111-3853", 12),
                },
            ],
            "Agente de prueba").Worksheet(Columnas.NombreDeLaHoja);
}
