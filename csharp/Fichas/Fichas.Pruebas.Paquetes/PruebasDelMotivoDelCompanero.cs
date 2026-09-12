using ClosedXML.Excel;
using Fichas.Contratos.Modelos;
using Fichas.Paquetes;

namespace Fichas.Pruebas.Paquetes;

/// <summary>
/// El compañero tiene dónde decir POR QUÉ no pudo, y eso vuelve hasta la base.
/// </summary>
/// <remarks>
/// Cada prueba sale de una frase del dueño y no del código:
/// <list type="bullet">
/// <item>«en el calendario también puede decir el estado: no completado, no se pudo
/// comunicar con el líder, o el líder no lo hizo» — de ahí salen las tres opciones del
/// menú y de ahí sale que un motivo dicho marque el documento como no completo, porque
/// con «sin marcar» el calendario no enseña ningún motivo.</item>
/// <item>«si el agente no pudo comunicarse con el líder y faltan cambios… debo crear
/// reporte para ellos con los comentarios de los agentes» — de ahí sale que el comentario
/// libre viaje hasta <c>personas.nota_companero</c> con quién y cuándo.</item>
/// </list>
/// <para>
/// Y la regla que no se toca (CLAUDE.md, permanente 5 precisada el 2026-09-03): lo que
/// dice el compañero va a <c>motivo_del_companero</c> y NUNCA a <c>motivo_no_completa</c>,
/// que es de Miguel.
/// </para>
/// </remarks>
[TestClass]
public class PruebasDelMotivoDelCompanero
{
    /// <summary>La base en memoria de esta prueba, montada en <see cref="Preparar"/>.</summary>
    private BaseInventada _base = null!;
    /// <summary>La carpeta temporal donde se escriben los <c>.xlsx</c>; se borra en <see cref="Recoger"/>.</summary>
    private string _carpeta = null!;
    /// <summary>El número interno del compañero de prueba al que se le genera el paquete.</summary>
    private long _sandy;

    /// <summary>Monta la base en memoria, la carpeta temporal y el compañero de cada prueba.</summary>
    [TestInitialize]
    public void Preparar()
    {
        _base = new BaseInventada();
        _carpeta = BaseInventada.CarpetaDePruebas();
        _sandy = _base.Companero("Sandy");
    }

    /// <summary>Borra la carpeta temporal; ningún <c>.xlsx</c> se queda en el disco.</summary>
    [TestCleanup]
    public void Recoger()
    {
        try { Directory.Delete(_carpeta, recursive: true); }
        catch (IOException) { /* si Windows todavia tiene la manija, la limpia el sistema */ }
    }

    /// <summary>La ruta de un archivo dentro de la carpeta temporal de la prueba.</summary>
    /// <param name="nombre">El nombre del archivo; por defecto el del paquete.</param>
    private string Ruta(string nombre = "por_verificar.xlsx") => Path.Combine(_carpeta, nombre);

    /// <summary>Escribe —o vacía, con nulo— una celda cualquiera de una fila, como haría el compañero.</summary>
    /// <param name="ruta">El <c>.xlsx</c> a modificar.</param>
    /// <param name="fila">La fila de Excel, base 1.</param>
    /// <param name="nombreDeColumna">El nombre de la columna en la base, no su título.</param>
    /// <param name="valor">El texto, o nulo para vaciar la celda.</param>
    private static void Escribir(string ruta, int fila, string nombreDeColumna, string? valor)
    {
        using var libro = new XLWorkbook(ruta);
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        var celda = hoja.Cell(fila, Columnas.IndiceDe(nombreDeColumna));
        if (valor is null) celda.Clear(XLClearOptions.Contents); else celda.SetValue(valor);
        libro.Save();
    }

    /// <summary>Rellena los seis pasos y la llamada de una fila, como haría el compañero.</summary>
    private static void Contestar(string ruta, int filaExcel, string respuesta)
    {
        using var libro = new XLWorkbook(ruta);
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        foreach (var columna in Columnas.Todas.Where(c => c.EsSiONo))
            hoja.Cell(filaExcel, Columnas.IndiceDe(columna.Nombre)).SetValue(respuesta);
        libro.Save();
    }

    /// <summary>Da de alta un caso con una persona y le genera el paquete al compañero; falla la prueba si no se escribió.</summary>
    /// <param name="numero">El número de caso.</param>
    /// <param name="mrn">La cédula de la persona.</param>
    /// <returns>El número interno del caso.</returns>
    private long UnCasoConUnaPersona(string numero = "BALC2609", string mrn = "055-1111-385A")
    {
        var caso = _base.Caso(numero);
        _base.Persona(caso, "Elena Rosa Muestra", mrn);
        var generado = _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Assert.IsTrue(generado.SeEscribio, string.Join(" | ", generado.Avisos.Select(a => a.Linea)));
        return caso;
    }

    // ─────────────────────────── la hoja que va ───────────────────────────

    /// <summary>
    /// La hoja trae las dos columnas nuevas: el motivo con las tres opciones del dueño y el
    /// comentario libre, las dos donde el compañero escribe.
    /// </summary>
    [TestMethod]
    public void LaHojaTraeDondeDecirElMotivoYDondeEscribirElComentario()
    {
        UnCasoConUnaPersona();
        using var libro = new XLWorkbook(Ruta());
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);

        var motivo = hoja.Cell(Columnas.FilaDeLaCabecera, Columnas.IndiceDe(MotivosDeLaHoja.ColumnaDelMotivo));
        var comentario = hoja.Cell(Columnas.FilaDeLaCabecera, Columnas.IndiceDe(MotivosDeLaHoja.ColumnaDelComentario));
        Assert.AreEqual(MotivosDeLaHoja.RotuloDelMotivo, motivo.GetString());
        Assert.AreEqual(MotivosDeLaHoja.RotuloDelComentario, comentario.GetString());

        var piel = XLColor.FromHtml(LibroDeTrabajo.Piel);
        foreach (var nombre in new[] { MotivosDeLaHoja.ColumnaDelMotivo, MotivosDeLaHoja.ColumnaDelComentario })
        {
            var celda = hoja.Cell(Columnas.PrimeraFilaDeDatos, Columnas.IndiceDe(nombre));
            Assert.AreEqual(piel, celda.Style.Fill.BackgroundColor, $"«{nombre}» la rellena el compañero");
            Assert.IsFalse(celda.Style.Protection.Locked, $"«{nombre}» tiene que poder teclearse");
            Assert.IsTrue(celda.IsEmpty(), $"«{nombre}» sale vacía: se pregunta, no se propone");
        }
    }

    /// <summary>Las tres opciones del menú son las tres frases del dueño, y no hay una cuarta.</summary>
    [TestMethod]
    public void ElMenuDelMotivoTraeLasTresOpcionesQueElDuenoNombro()
    {
        UnCasoConUnaPersona();
        using var libro = new XLWorkbook(Ruta());
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);

        var columna = Columnas.IndiceDe(MotivosDeLaHoja.ColumnaDelMotivo);
        var menu = hoja.DataValidations.Single(v => v.Ranges.Any(r => r.RangeAddress.FirstAddress.ColumnNumber == columna));
        foreach (var opcion in MotivosDeLaHoja.Opciones)
            StringAssert.Contains(menu.Value, opcion, $"falta «{opcion}» en el menú");
        Assert.HasCount(3, MotivosDeLaHoja.Opciones);
        Assert.IsFalse(menu.ShowErrorMessage, "el menú es una ayuda, no una reja: se puede escribir a mano");

        var comentario = Columnas.IndiceDe(MotivosDeLaHoja.ColumnaDelComentario);
        Assert.IsFalse(
            hoja.DataValidations.Any(v => v.Ranges.Any(r => r.RangeAddress.FirstAddress.ColumnNumber == comentario)),
            "el comentario es libre: un menú ahí sería decirle al compañero qué puede pensar");
    }

    // ─────────────────────────── la hoja que vuelve ───────────────────────────

    /// <summary>
    /// Los tres motivos, uno por uno: lo que elige el compañero llega a
    /// <c>casos.motivo_del_companero</c> con su nombre y su hora.
    /// </summary>
    [TestMethod]
    [DataRow("No se pudo comunicar con el líder", "no_se_pudo_comunicar")]
    [DataRow("El líder no lo hizo", "el_lider_no_lo_hizo")]
    [DataRow("Otra razón", "otra_razon")]
    public void ElMotivoQueEligeElCompaneroLlegaHastaElCasoConQuienYCuando(string elegido, string guardado)
    {
        var caso = UnCasoConUnaPersona();
        Contestar(Ruta(), 7, "No");
        Escribir(Ruta(), 7, MotivosDeLaHoja.ColumnaDelMotivo, elegido);
        Escribir(Ruta(), 7, MotivosDeLaHoja.ColumnaDelComentario, "Llamé tres veces y no contestó.");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.IsEmpty(vuelta.Descartadas, string.Join(" | ", vuelta.Descartadas.Select(d => d.Motivo)));
        Assert.AreEqual(Caso.LeerMotivo(guardado), vuelta.Marcas[0].Motivo, "el motivo tiene que llegar a la marca");

        Assert.IsTrue(_base.Paquetes.AplicarMarcas(vuelta.Marcas, _sandy, Ruta()).SeEscribio);
        var anotado = _base.Almacen.Casos[caso];
        Assert.AreEqual(guardado, anotado.MotivoDelCompanero);
        Assert.AreEqual(_sandy, anotado.EstadoDelCompaneroPor, "con quién");
        Assert.AreEqual("2026-09-04 10:30:00", anotado.EstadoDelCompaneroEn, "y cuándo");
    }

    /// <summary>El comentario libre llega a <c>personas.nota_companero</c>, con quién y cuándo.</summary>
    [TestMethod]
    public void ElComentarioLlegaHastaLaPersonaConQuienYCuando()
    {
        var caso = _base.Caso("BALC2609");
        var lyris = _base.Persona(caso, "Elena Rosa Muestra", "055-1111-385A");
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(Ruta(), 7, "No");
        Escribir(Ruta(), 7, MotivosDeLaHoja.ColumnaDelComentario, "El líder está de viaje hasta el día 20.");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.AreEqual("El líder está de viaje hasta el día 20.", vuelta.Marcas[0].NotaCompanero);

        _base.Paquetes.AplicarMarcas(vuelta.Marcas, _sandy, Ruta());
        var anotada = _base.Almacen.Personas[lyris];
        Assert.AreEqual("El líder está de viaje hasta el día 20.", anotada.NotaCompanero);
        Assert.AreEqual(_sandy, anotada.PropuestoPor, "con quién");
        Assert.AreEqual("2026-09-04 10:30:00", anotada.PropuestoEn, "y cuándo");
    }

    /// <summary>
    /// El caso entero del dueño: no se pudo hablar con el líder, así que NADA se miró y las
    /// siete casillas vuelven en blanco. Esa fila no se puede perder, que es lo que pasaba.
    /// </summary>
    [TestMethod]
    public void ConLasSieteEnBlancoPeroConMotivoLaFilaNoSePierde()
    {
        var caso = _base.Caso("BALC2609");
        var lyris = _base.Persona(caso, "Elena Rosa Muestra", "055-1111-385A");
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Escribir(Ruta(), 7, MotivosDeLaHoja.ColumnaDelMotivo, "No se pudo comunicar con el líder");
        Escribir(Ruta(), 7, MotivosDeLaHoja.ColumnaDelComentario, "Teléfono apagado toda la semana.");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.HasCount(1, vuelta.Marcas, "una fila que trae el motivo NO viene «con las siete en blanco»");
        Assert.AreEqual(MotivoDeNoCompletar.NoSePudoComunicar, vuelta.Marcas[0].Motivo);
        Assert.AreEqual(
            EstadoDeRecomendacion.NoCompleta,
            vuelta.Marcas[0].EstadoDeLaRecomendacion,
            "decir POR QUÉ no se completó es decir que no está completa; con «sin marcar» el calendario no enseñaría el motivo");

        _base.Paquetes.AplicarMarcas(vuelta.Marcas, _sandy, Ruta());
        Assert.AreEqual("no_se_pudo_comunicar", _base.Almacen.Casos[caso].MotivoDelCompanero);
        Assert.AreEqual("no_completa", _base.Almacen.Casos[caso].EstadoRecomendacion);
        Assert.AreEqual("Teléfono apagado toda la semana.", _base.Almacen.Personas[lyris].NotaCompanero);
    }

    /// <summary>Sin motivo y con las siete en blanco NO cambia nada: eso sigue siendo «nadie la miró».</summary>
    [TestMethod]
    public void SinMotivoYConLasSieteEnBlancoElDocumentoSigueSinMarcar()
    {
        var caso = UnCasoConUnaPersona();

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.IsEmpty(vuelta.Marcas);
        _base.Paquetes.AplicarMarcas(vuelta.Marcas, _sandy, Ruta());
        Assert.IsNull(_base.Almacen.Casos[caso].EstadoRecomendacion);
        Assert.IsNull(_base.Almacen.Casos[caso].MotivoDelCompanero);
    }

    // ─────────────────────────── lo que no se rompe ───────────────────────────

    /// <summary>
    /// Lo que dice el compañero es suyo y no firma nada de Miguel: ni
    /// <c>motivo_no_completa</c> ni ninguna procedencia.
    /// </summary>
    [TestMethod]
    public void LoQueDiceElCompaneroNoEscribeElMotivoDeMiguelNiFirmaNingunCampo()
    {
        var caso = UnCasoConUnaPersona();
        Contestar(Ruta(), 7, "No");
        Escribir(Ruta(), 7, MotivosDeLaHoja.ColumnaDelMotivo, "El líder no lo hizo");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        _base.Paquetes.AplicarMarcas(vuelta.Marcas, _sandy, Ruta());

        Assert.AreEqual("el_lider_no_lo_hizo", _base.Almacen.Casos[caso].MotivoDelCompanero);
        Assert.IsNull(_base.Almacen.Casos[caso].MotivoNoCompleta, "el motivo vigente es de Miguel y esta hoja no lo escribe");
        Assert.IsEmpty(_base.Almacen.Procedencias, "el Excel escribe el estado del documento, nunca la firma de un campo");
    }

    /// <summary>
    /// Un Excel viejo —sin las dos columnas nuevas— entra igual. Nada se rechaza: se lee lo
    /// que traiga y se dice qué faltaba.
    /// </summary>
    [TestMethod]
    public void UnExcelSinLasDosColumnasEntraIgualYSeDiceQueFaltaba()
    {
        var caso = _base.Caso("BALC2609");
        var lyris = _base.Persona(caso, "Elena Rosa Muestra", "055-1111-385A");
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(Ruta(), 7, "Sí");

        // La hoja de antes del 2026-09-05: se le quitan las dos columnas nuevas.
        using (var libro = new XLWorkbook(Ruta()))
        {
            var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
            hoja.Column(Columnas.IndiceDe(MotivosDeLaHoja.ColumnaDelComentario)).Delete();
            hoja.Column(Columnas.IndiceDe(MotivosDeLaHoja.ColumnaDelMotivo)).Delete();
            libro.Save();
        }

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.IsEmpty(vuelta.Descartadas, "un Excel viejo no se rechaza");
        Assert.HasCount(1, vuelta.Marcas);
        Assert.AreEqual(MotivoDeNoCompletar.SinMotivo, vuelta.Marcas[0].Motivo);
        Assert.IsNull(vuelta.Marcas[0].NotaCompanero);
        var aviso = vuelta.Avisos.Single(a => a.Linea.Contains("no trae"));
        StringAssert.Contains(aviso.Linea, MotivosDeLaHoja.RotuloDelMotivo);
        StringAssert.Contains(aviso.Linea, MotivosDeLaHoja.RotuloDelComentario);

        Assert.IsTrue(_base.Paquetes.AplicarMarcas(vuelta.Marcas, _sandy, Ruta()).SeEscribio);
        Assert.AreEqual("completa", _base.Almacen.Casos[caso].EstadoRecomendacion, "los seis pasos siguen valiendo");
        Assert.AreEqual(_sandy, _base.Almacen.Personas[lyris].PropuestoPor);
    }

    /// <summary>
    /// Un motivo escrito a mano que no está en la lista NO tira la fila: se avisa con la
    /// fila y con el texto tal cual, y los seis pasos se aplican igual.
    /// </summary>
    /// <remarks>
    /// Al revés que un paso ilegible, que sí descarta la fila entera. La diferencia está
    /// medida: un paso que no se entiende puede ser justo el que dice que falta algo, y el
    /// motivo no puede empeorar el estado de nadie. Tirar seis respuestas buenas por una
    /// frase escrita a mano sería perder el trabajo del compañero.
    /// </remarks>
    [TestMethod]
    public void UnMotivoQueNoEstaEnLaListaNoTiraLaFilaYSeAvisaConSuTexto()
    {
        var caso = UnCasoConUnaPersona();
        Contestar(Ruta(), 7, "No");
        Escribir(Ruta(), 7, MotivosDeLaHoja.ColumnaDelMotivo, "se mudó de barrio");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.IsEmpty(vuelta.Descartadas, "seis respuestas buenas no se tiran por una frase escrita a mano");
        Assert.HasCount(1, vuelta.Marcas);
        Assert.AreEqual(MotivoDeNoCompletar.SinMotivo, vuelta.Marcas[0].Motivo, "no se adivina a cuál de los tres se parece");
        var aviso = vuelta.Avisos.Single(a => a.Detalle is not null && a.Detalle.Contains("se mudó de barrio"));
        StringAssert.Contains(aviso.Detalle!, "fila 7");

        _base.Paquetes.AplicarMarcas(vuelta.Marcas, _sandy, Ruta());
        Assert.AreEqual("no_completa", _base.Almacen.Casos[caso].EstadoRecomendacion, "los pasos sí se aplicaron");
        Assert.IsNull(_base.Almacen.Casos[caso].MotivoDelCompanero);
    }

    /// <summary>
    /// Dos personas del mismo documento con motivos distintos: la columna del caso solo
    /// admite uno, así que se escribe el de la primera fila y se dice en voz alta.
    /// </summary>
    [TestMethod]
    public void DosFilasDelMismoCasoConMotivosDistintosEscribenElDeLaPrimeraYLoAvisan()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena", "055-1111-385A", 1);
        _base.Persona(caso, "Julia", "055-1111-3853", 2);
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(Ruta(), 7, "No");
        Contestar(Ruta(), 8, "No");
        Escribir(Ruta(), 7, MotivosDeLaHoja.ColumnaDelMotivo, "No se pudo comunicar con el líder");
        Escribir(Ruta(), 8, MotivosDeLaHoja.ColumnaDelMotivo, "El líder no lo hizo");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        _base.Paquetes.AplicarMarcas(vuelta.Marcas, _sandy, Ruta());

        Assert.AreEqual("no_se_pudo_comunicar", _base.Almacen.Casos[caso].MotivoDelCompanero);
        // El aviso sale al LEER, que es cuando se decide y antes de escribir nada.
        var aviso = vuelta.Avisos.Single(a => a.Linea.Contains("dos motivos distintos"));
        StringAssert.Contains(aviso.Detalle!, MotivosDeLaHoja.Opciones[1], "el motivo que NO se guardó se dice con todas sus letras");
        StringAssert.Contains(aviso.Detalle!, "fila 8");
    }

    /// <summary>Con los seis pasos en «Sí» manda lo que dicen los pasos, y la contradicción se avisa.</summary>
    [TestMethod]
    public void ConLosSeisEnSiYUnMotivoPuestoMandanLosPasosYSeAvisaDeLaContradiccion()
    {
        var caso = UnCasoConUnaPersona();
        Contestar(Ruta(), 7, "Sí");
        Escribir(Ruta(), 7, MotivosDeLaHoja.ColumnaDelMotivo, "Otra razón");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.AreEqual(EstadoDeRecomendacion.Completa, vuelta.Marcas[0].EstadoDeLaRecomendacion);
        var aviso = vuelta.Avisos.Single(a => a.Linea.Contains("completo") && a.Linea.Contains("motivo"));
        StringAssert.Contains(aviso.Detalle!, "7");

        _base.Paquetes.AplicarMarcas(vuelta.Marcas, _sandy, Ruta());
        Assert.AreEqual("completa", _base.Almacen.Casos[caso].EstadoRecomendacion);
        Assert.AreEqual("otra_razon", _base.Almacen.Casos[caso].MotivoDelCompanero,
            "lo que dijo el compañero se guarda igual: quien lo lea decide");
    }
}
