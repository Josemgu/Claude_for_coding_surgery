using ClosedXML.Excel;
using Fichas.Paquetes;

namespace Fichas.Pruebas.Paquetes;

/// <summary>
/// El punto 8b de la v16: la columna «Templo» en cada fila del paquete, ademas de la cabecera.
/// </summary>
/// <remarks>
/// Palabras del dueno (PENDIENTES.md, «Para la v16», 8, 2026-09-16): <i>«A dónde viajarán es
/// el templo: eso sí debe ponerse (templo de Panamá, templo de Santo Domingo)»</i>. Hasta ese
/// dia el templo iba solo en la linea 2 de la cabecera, y solo cuando TODAS las filas iban al
/// mismo; un paquete con dos templos no lo decia en ningun sitio.
/// </remarks>
[TestClass]
public class PruebasDelTemploEnCadaFila
{
    /// <summary>La base en memoria de esta prueba.</summary>
    private BaseInventada _base = null!;

    /// <summary>La carpeta temporal donde se escriben los <c>.xlsx</c>.</summary>
    private string _carpeta = null!;

    /// <summary>El compañero al que se le genera el paquete.</summary>
    private long _sandy;

    /// <summary>Monta la base, la carpeta y el compañero.</summary>
    [TestInitialize]
    public void Preparar()
    {
        _base = new BaseInventada();
        _carpeta = BaseInventada.CarpetaDePruebas();
        _sandy = _base.Companero("Sandy");
    }

    /// <summary>Borra la carpeta temporal.</summary>
    [TestCleanup]
    public void Recoger()
    {
        try { Directory.Delete(_carpeta, recursive: true); }
        catch (IOException) { /* si Windows todavia tiene la manija, la carpeta temporal la limpia el sistema */ }
    }

    /// <summary>La ruta del paquete dentro de la carpeta temporal.</summary>
    private string Ruta() => Path.Combine(_carpeta, "por_verificar.xlsx");

    /// <summary>La columna existe, se llama «Templo» y va justo detras de la fecha de viaje: cuando y a donde, juntos.</summary>
    [TestMethod]
    public void HayColumnaTemploJustoDespuesDeLaFechaDeViaje()
    {
        var templo = Columnas.Por(Columnas.ColumnaDelTemplo);
        Assert.AreEqual("Templo", templo.Titulo);
        Assert.IsFalse(templo.EsRespuesta, "el templo lo escribe el sistema, no el compañero");
        Assert.AreEqual(Columnas.IndiceDe("fecha_viaje") + 1, Columnas.IndiceDe(Columnas.ColumnaDelTemplo));
    }

    /// <summary>
    /// Dados dos casos que van a templos distintos, cuando se genera el paquete, entonces cada
    /// fila dice el suyo aunque la cabecera no pueda nombrar ninguno.
    /// </summary>
    [TestMethod]
    public void CadaFilaLlevaSuTemploAunqueLaCabeceraNoPuedaNombrarUno()
    {
        var aSantoDomingo = _base.Caso("BALC2609", templo: "Santo Domingo");
        var aPanama = _base.Caso("CASP2609", templo: "Panamá");
        _base.Persona(aSantoDomingo, "Elena Rosa Muestra", "055-1111-3853");
        _base.Persona(aPanama, "Julia Luz Inventada", "055-1111-385A");

        var generado = _base.Paquetes.GenerarExcelDeCompanero(_sandy, [aSantoDomingo, aPanama], Ruta());
        Assert.IsTrue(generado.SeEscribio, string.Join(" | ", generado.Avisos.Select(a => a.Linea)));

        using var libro = new XLWorkbook(Ruta());
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        var columna = Columnas.IndiceDe(Columnas.ColumnaDelTemplo);
        Assert.AreEqual("Templo", hoja.Cell(Columnas.FilaDeLaCabecera, columna).GetString());
        Assert.AreEqual("Santo Domingo", hoja.Cell(7, columna).GetString());
        Assert.AreEqual("Panamá", hoja.Cell(8, columna).GetString());
        Assert.IsFalse(hoja.Cell(2, 1).GetString().Contains("Templo:", StringComparison.Ordinal),
            "con dos templos la cabecera no nombra ninguno; por eso hace falta la columna");
    }

    /// <summary>Con un solo templo, la cabecera lo sigue diciendo Y la fila tambien: no se quita de arriba.</summary>
    [TestMethod]
    public void ConUnSoloTemploVaEnLaCabeceraYEnLaFila()
    {
        var caso = _base.Caso("BALC2609", templo: "Santo Domingo");
        _base.Persona(caso, "Elena Rosa Muestra", "055-1111-3853");
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());

        using var libro = new XLWorkbook(Ruta());
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        StringAssert.Contains(hoja.Cell(2, 1).GetString(), "Templo: Santo Domingo");
        Assert.AreEqual("Santo Domingo", hoja.Cell(7, Columnas.IndiceDe(Columnas.ColumnaDelTemplo)).GetString());
    }

    /// <summary>
    /// La vuelta casa por rotulo y no por posicion: con la columna nueva en medio, una hoja
    /// generada hoy vuelve entera, y la clave sigue siendo la ultima.
    /// </summary>
    [TestMethod]
    public void LaVueltaSigueCasandoConLaColumnaNuevaEnMedio()
    {
        var caso = _base.Caso("BALC2609", templo: "Panamá");
        _base.Persona(caso, "Elena Rosa Muestra", "055-1111-3853");
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        using (var libro = new XLWorkbook(Ruta()))
        {
            var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
            foreach (var paso in Pasos.Todos)
                hoja.Cell(7, Columnas.IndiceDe(paso.Nombre)).SetValue("Sí");
            libro.Save();
        }

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);

        Assert.IsEmpty(vuelta.Descartadas, string.Join(" | ", vuelta.Descartadas.Select(d => d.Motivo)));
        Assert.HasCount(1, vuelta.Marcas);
        Assert.AreEqual("055-1111-3853", vuelta.Marcas[0].Mrn);
        Assert.AreEqual(Columnas.Todas.Count, Columnas.IndiceDe(Columnas.ColumnaDeLaClave), "la clave sigue la última");
    }

    /// <summary>
    /// Sin templo guardado la celda dice «no consta», como el resto de lo que escribe el sistema
    /// y no el companero. El templo es un dato que el papel SI trae; si falta, falta de verdad.
    /// </summary>
    [TestMethod]
    public void SinTemploGuardadoLaCeldaDiceQueNoConsta()
    {
        var caso = _base.Caso("BALC2609", templo: null);
        _base.Persona(caso, "Elena Rosa Muestra", "055-1111-3853");
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());

        using var libro = new XLWorkbook(Ruta());
        Assert.AreEqual(Columnas.SinDato,
            libro.Worksheet(Columnas.NombreDeLaHoja).Cell(7, Columnas.IndiceDe(Columnas.ColumnaDelTemplo)).GetString());
    }
}
