using ClosedXML.Excel;
using Fichas.Paquetes;

namespace Fichas.Pruebas.Paquetes;

/// <summary>
/// Las propiedades estructurales de la hoja «Por verificar» que se le entrega al companero.
/// </summary>
/// <remarks>
/// El criterio C6-1 de PENDIENTES.md las enumera: hoja, congelado en A7, 16 titulos y
/// anchos, tinta 16233A, fondo FFF6DC en las columnas de respuesta, clave en gris
/// tamano 8, 7 menus Si/No y fecha limite en rojo A62E24. C6-2 anade templo y fecha de
/// salida en la cabecera. Cada una se comprueba abriendo el libro, no leyendo el codigo.
/// </remarks>
[TestClass]
public class PruebasDelLibroDeTrabajo
{
    private static FilaDeTrabajo UnaFila(string caso = "BALC2609", string? mrn = "055-1111-3853", long casoId = 12, string? viaje = "2026-10-15")
        => new()
        {
            NumeroCaso = caso,
            FechaViaje = viaje,
            Templo = "Santo Domingo",
            UnidadNombre = "Cuatricentenaria (7000014)",
            Nombre = "Elena Rosa Muestra",
            Mrn = mrn,
            AQueVa = "Investidura",
            Clave = Columnas.ArmarLaClave(caso, mrn, casoId),
        };

    private static IXLWorksheet Hoja(params FilaDeTrabajo[] filas)
        => LibroDeTrabajo.Construir(filas, "Sandy").Worksheet(Columnas.NombreDeLaHoja);

    [TestMethod]
    public void LaHojaSeLlamaPorVerificarYEsLaUnica()
    {
        using var libro = LibroDeTrabajo.Construir([UnaFila()], "Sandy");
        Assert.AreEqual(1, libro.Worksheets.Count);
        Assert.AreEqual("Por verificar", libro.Worksheets.First().Name);
    }

    [TestMethod]
    public void LosTitulosVanEnLaFilaSeisYLosDatosEnLaSiete()
    {
        var hoja = Hoja(UnaFila());
        Assert.AreEqual("Caso", hoja.Cell(6, 1).GetString());
        Assert.AreEqual("clave", hoja.Cell(6, Columnas.Todas.Count).GetString(), "la clave sigue siendo la última");
        Assert.AreEqual("BALC2609", hoja.Cell(7, 1).GetString());
    }

    [TestMethod]
    public void LaCabeceraCongelaEnLaFilaSeis()
        => Assert.AreEqual(6, Hoja(UnaFila()).SheetView.SplitRow, "congelar en A7 es partir por debajo de la fila 6");

    [TestMethod]
    public void LaCabeceraLlevaTemploYFechaDeSalida()
        => Assert.AreEqual("Templo: Santo Domingo · Sale el 15-10-2026", Hoja(UnaFila()).Cell(2, 1).GetString());

    [TestMethod]
    public void SinTemploGuardadoLaCabeceraNoDejaUnHuecoConSuSeparador()
    {
        var fila = UnaFila() with { Templo = null };
        Assert.AreEqual("Sale el 15-10-2026", Hoja(fila).Cell(2, 1).GetString());
    }

    [TestMethod]
    public void LaFechaLimiteVaUnaSemanaAntesYEnRojo()
    {
        var hoja = Hoja(UnaFila());
        Assert.AreEqual("Todo verificado antes del 08-10-2026", hoja.Cell(3, 1).GetString());
        Assert.AreEqual(XLColor.FromHtml("#A62E24"), hoja.Cell(3, 1).Style.Font.FontColor);
    }

    [TestMethod]
    public void SinFechaDeSalidaNoSeInventaUnaFechaLimite()
    {
        var fila = UnaFila(viaje: null);
        Assert.AreEqual(string.Empty, Hoja(fila).Cell(3, 1).GetString(), "una fecha limite falsa es peor que ninguna: el companero se organiza contra ella");
    }

    [TestMethod]
    public void LaCabeceraDiceQuienEsElAgente()
        => Assert.AreEqual("Agente: Sandy", Hoja(UnaFila()).Cell(4, 1).GetString());

    [TestMethod]
    public void LaFilaDeTitulosVaConFondoDeTinta()
        => Assert.AreEqual(XLColor.FromHtml("#16233A"), Hoja(UnaFila()).Cell(6, 3).Style.Fill.BackgroundColor);

    [TestMethod]
    public void LasSieteColumnasDeRespuestaLlevanFondoYLasDemasNo()
    {
        var hoja = Hoja(UnaFila());
        var piel = XLColor.FromHtml("#FFF6DC");
        foreach (var columna in Columnas.Todas.Where(c => c.EsRespuesta))
            Assert.AreEqual(piel, hoja.Cell(7, Columnas.IndiceDe(columna.Nombre)).Style.Fill.BackgroundColor, columna.Nombre);
        Assert.AreNotEqual(piel, hoja.Cell(7, Columnas.IndiceDe("nombre")).Style.Fill.BackgroundColor,
            "el nombre es editable pero pintarlo pediria teclear un nombre que ya viene puesto");
    }

    [TestMethod]
    public void LaClaveVaALaVistaEnGrisPequeno()
    {
        var celda = Hoja(UnaFila()).Cell(7, Columnas.IndiceDe("clave"));
        Assert.AreEqual(8, celda.Style.Font.FontSize);
        Assert.AreEqual(XLColor.FromHtml("#8A93A5"), celda.Style.Font.FontColor);
        Assert.AreEqual("BALC2609:055-1111-3853:12", celda.GetString(), "un dato oculto es un dato que alguien borra sin saber lo que hace");
    }

    [TestMethod]
    public void HayOchoMenus_SieteDeSiONoYElDelMotivo()
    {
        var hoja = Hoja(UnaFila());
        Assert.AreEqual(8, hoja.DataValidations.Count(), "los siete de sí o no más el del motivo");
        var deSiONo = hoja.DataValidations.Where(v => v.Value.Contains("Sí") && v.Value.Contains("No")).ToList();
        Assert.HasCount(7, deSiONo);
        var delMotivo = hoja.DataValidations.Single(v => v.Value.Contains("El líder no lo hizo"));
        foreach (var opcion in MotivosDeLaHoja.Opciones)
            StringAssert.Contains(delMotivo.Value, opcion);
    }

    [TestMethod]
    public void ElMenuEsUnaAyudaYNoUnaRejaAsiQueNoRechazaLoQueSeEscribaAMano()
        => Assert.IsFalse(Hoja(UnaFila()).DataValidations.Any(v => v.ShowErrorMessage),
            "una celda bloqueada obligaria a dejarla vacia cuando la realidad no cabe en dos opciones, y una celda vacia no distingue «no aplica» de «no lo mire»");

    [TestMethod]
    public void SinNingunaFilaNoSePonenMenusSobreLaCabecera()
        => Assert.AreEqual(0, Hoja().DataValidations.Count());

    /// <summary>
    /// ⚠️ Ni la hoja va protegida ni queda una sola celda bloqueada, desde el 2026-09-07.
    /// </summary>
    /// <remarks>
    /// Esta prueba afirmaba lo contrario y la cambió una orden del dueño: «no bloquees las
    /// celdas por favor, de los paquetes». Se comprueban las DOS mitades porque en OOXML hacen
    /// falta las dos: una hoja protegida bloquea todo lo que no diga lo contrario, y una marca
    /// de celda sin protección no impide nada. El detalle está en
    /// <see cref="PruebasDeQueNingunaCeldaVaBloqueada"/>.
    /// </remarks>
    [TestMethod]
    public void NiLaHojaVaProtegidaNiQuedaNingunaCeldaBloqueada()
    {
        var hoja = Hoja(UnaFila());
        Assert.IsFalse(hoja.Protection.IsProtected, "el dueño pidió que no se bloqueen las celdas de los paquetes");
        Assert.IsFalse(hoja.Cell(7, Columnas.IndiceDe("mrn")).Style.Protection.Locked);
        Assert.IsFalse(hoja.Cell(7, Columnas.IndiceDe("clave")).Style.Protection.Locked);
        Assert.IsFalse(hoja.Cell(7, Columnas.IndiceDe("nombre")).Style.Protection.Locked);
        Assert.IsFalse(hoja.Cell(7, Columnas.IndiceDe("paso_entrevistas")).Style.Protection.Locked);
    }

    [TestMethod]
    public void LosAnchosSonLosDeCadaColumna()
    {
        var hoja = Hoja(UnaFila());
        Assert.AreEqual(19d, hoja.Column(Columnas.IndiceDe("mrn")).Width);
        Assert.AreEqual(26d, hoja.Column(Columnas.IndiceDe("clave")).Width);
        Assert.AreEqual(13d, hoja.Column(Columnas.IndiceDe("paso_preparacion")).Width);
    }

    /// <summary>
    /// El motivo entero de que exista <see cref="ClaseDeColumna.Texto"/>: sin formato de
    /// texto Excel se come los ceros de delante y el MRN deja de casar al volver.
    /// </summary>
    [TestMethod]
    public void ElMrnYLaClaveSalenConFormatoDeTexto()
    {
        var hoja = Hoja(UnaFila());
        Assert.AreEqual("@", hoja.Cell(7, Columnas.IndiceDe("mrn")).Style.NumberFormat.Format);
        Assert.AreEqual("@", hoja.Cell(7, Columnas.IndiceDe("clave")).Style.NumberFormat.Format);
        Assert.AreEqual("055-1111-3853", hoja.Cell(7, Columnas.IndiceDe("mrn")).GetString());
    }

    [TestMethod]
    public void UnaCedulaQueTerminaEnLetraSaleTalCualYSinAviso()
    {
        var hoja = Hoja(UnaFila(mrn: "055-1111-385A"));
        Assert.AreEqual("055-1111-385A", hoja.Cell(7, Columnas.IndiceDe("mrn")).GetString());
        Assert.AreEqual(XLDataType.Text, hoja.Cell(7, Columnas.IndiceDe("mrn")).DataType);
    }

    /// <summary>
    /// Un hueco lo lee el companero como «esto lo relleno yo», y las unicas celdas que rellena
    /// el son las de fondo amarillo. Lo que la base no sabe se dice con palabras.
    /// </summary>
    /// <remarks>
    /// Hasta el 2026-09-06 esto se medía sobre «Estaca o distrito» y «Fecha de solicitud», que
    /// salian SIEMPRE con «no consta» porque la base no las guarda. El dueno las quito ese dia
    /// —«son informaciones que no me pide verificar»—, asi que la regla se mide donde todavia
    /// pasa de verdad: «A qué va» sale vacio cuando ninguna casilla de ordenanzas se leyo, y el
    /// barrio sale vacio cuando el escaneo no lo dio. La regla no cambio; cambio donde se ve.
    /// </remarks>
    [TestMethod]
    public void LoQueLaBaseNoSabeSaleConLaPalabraQueLoDiceYNoEnBlanco()
    {
        var fila = UnaFila() with { AQueVa = null, UnidadNombre = null };
        var hoja = Hoja(fila);
        Assert.AreEqual("no consta", hoja.Cell(7, Columnas.IndiceDe("a_que_va")).GetString());
        Assert.AreEqual("no consta", hoja.Cell(7, Columnas.IndiceDe("unidad_nombre")).GetString());
    }

    /// <summary>
    /// Lo que el dueno pidio el 2026-09-06, medido sobre la hoja que SALE y no sobre la lista.
    /// </summary>
    /// <remarks>
    /// Se cuentan las columnas escritas de verdad y se leen sus rotulos de la fila 6: es lo
    /// unico que prueba que la hoja que recibe el companero ya no las trae. Comprobarlo solo
    /// en <see cref="Columnas.Todas"/> dejaria pasar que alguien las siguiera pintando aparte.
    /// </remarks>
    [TestMethod]
    public void LaHojaSaleConDiecisieteColumnasYSinLaFechaDeSolicitudNiLaEstaca()
    {
        var hoja = Hoja(UnaFila());
        Assert.AreEqual(17, hoja.LastColumnUsed()!.ColumnNumber(),
            "18 → 16 el 2026-09-06 → 17 el 2026-09-07, cuando entró «Número de unidad»");

        var rotulos = Enumerable.Range(1, 17)
            .Select(columna => hoja.Cell(Columnas.FilaDeLaCabecera, columna).GetString())
            .ToArray();
        CollectionAssert.DoesNotContain(rotulos, "Fecha de solicitud");
        CollectionAssert.DoesNotContain(rotulos, "Estaca o distrito");
        Assert.AreEqual("Caso", rotulos[0]);
        Assert.AreEqual("Fecha de viaje", rotulos[1], "la fecha de viaje sube al puesto de la de solicitud");
        Assert.AreEqual("Número de unidad", rotulos[2], "el número de la unidad, en su propia columna");
        Assert.AreEqual("Barrio o rama", rotulos[3], "y al lado el nombre de la unidad");
        Assert.AreEqual("clave", rotulos[16], "la clave sigue siendo la última y a la vista");
    }

    [TestMethod]
    public void LasSieteRespuestasSalenVaciasAunqueLaPersonaTraigaUnaRondaAnterior()
    {
        var hoja = Hoja(UnaFila());
        foreach (var columna in Columnas.Todas.Where(c => c.EsRespuesta))
            Assert.IsTrue(hoja.Cell(7, Columnas.IndiceDe(columna.Nombre)).IsEmpty(),
                $"«{columna.Titulo}» tiene que salir vacia: rellenarla de antemano invita a confirmarla sin comprobarla");
    }

    /// <summary>Un nombre leido por OCR que empieza por «=» no es una formula.</summary>
    [TestMethod]
    public void UnTextoQueEmpiezaPorIgualNoSeEscribeComoFormula()
    {
        var fila = UnaFila() with { Nombre = "=Elena" };
        var celda = Hoja(fila).Cell(7, Columnas.IndiceDe("nombre"));
        Assert.AreEqual(XLDataType.Text, celda.DataType);
        Assert.AreEqual("=Elena", celda.GetString());
    }

    [TestMethod]
    public void ConVariosCasosElTituloNoNombraNingunoYLaFechaLimiteEsLaDelPrimeroQueViaja()
    {
        var hoja = Hoja(
            UnaFila("BALC2609", viaje: "2026-11-20"),
            UnaFila("CASP2609", mrn: "055-1111-3854", casoId: 13, viaje: "2026-10-15"));
        Assert.AreEqual("Preparación para las ordenanzas", hoja.Cell(1, 1).GetString());
        Assert.AreEqual("Todo verificado antes del 08-10-2026", hoja.Cell(3, 1).GetString(),
            "una fecha limite calculada sobre el ultimo dejaria pasar sin aviso al grupo que sale antes");
    }

    [TestMethod]
    public void ConUnSoloCasoElTituloLoNombra()
        => Assert.AreEqual("Preparación para las ordenanzas · BALC2609", Hoja(UnaFila()).Cell(1, 1).GetString());
}
