using ClosedXML.Excel;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;
using Fichas.Paquetes;

namespace Fichas.Pruebas.Paquetes;

/// <summary>
/// La ida y la vuelta enteras: se genera un paquete, el companero lo rellena, y vuelve.
/// </summary>
/// <remarks>
/// Cada prueba sale de un criterio de PENDIENTES.md, no del codigo: C6-3 (se reconcilia por
/// numero de caso + MRN + id, nunca por nombre; una clave sin id entra por su propio motivo),
/// C6-4 (las filas sin par no se insertan y se ven), C6-5 (se avisa nombrando a quien no tiene
/// MRN), C6-6 (el estado lo escribe el Excel del companero, con su nombre) y C6-7 (Excel
/// bloqueado: dato guardado, aviso en espanol, sin caida).
/// </remarks>
[TestClass]
public class PruebasDeLaVuelta
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
        catch (IOException) { /* si Windows todavia tiene la manija, la carpeta temporal la limpia el sistema */ }
    }

    /// <summary>La ruta de un archivo dentro de la carpeta temporal de la prueba.</summary>
    /// <param name="nombre">El nombre del archivo; por defecto el del paquete.</param>
    private string Ruta(string nombre = "por_verificar.xlsx") => Path.Combine(_carpeta, nombre);

    /// <summary>Rellena las siete columnas de respuesta de una fila, como haria el companero.</summary>
    /// <param name="ruta">El <c>.xlsx</c> a modificar.</param>
    /// <param name="filaExcel">La fila de Excel, base 1; la primera persona va en la 7.</param>
    /// <param name="respuesta">Lo que se escribe en los seis pasos; nulo los deja como están.</param>
    /// <param name="llamo">Lo que se escribe en «¿Llamó al líder?»; nulo la deja como está.</param>
    private static void Contestar(string ruta, int filaExcel, string? respuesta, string? llamo = null)
    {
        using var libro = new XLWorkbook(ruta);
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        foreach (var paso in Pasos.Todos)
            if (respuesta is not null)
                hoja.Cell(filaExcel, Columnas.IndiceDe(paso.Nombre)).SetValue(respuesta);
        if (llamo is not null)
            hoja.Cell(filaExcel, Columnas.IndiceDe(Pasos.ColumnaDeLaLlamada)).SetValue(llamo);
        libro.Save();
    }

    /// <summary>Escribe —o vacía, con nulo— una celda cualquiera de una fila, como si el compañero tocara lo que no debe.</summary>
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

    // ───────────────────────── la ida y la vuelta ─────────────────────────

    /// <summary>
    /// El criterio central del pase: se pone algo, se genera, se lee, y vuelve lo mismo. Con
    /// las dos formas de cedula que Excel estropea si nadie lo impide.
    /// </summary>
    [TestMethod]
    public void LoQueSePuseVuelveIgualIncluidaLaCedulaConLetraYLaDeCerosDelante()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena Rosa Muestra", "055-1111-385A", 1);
        _base.Persona(caso, "Julia Luz Inventada", "055-1111-3853", 2);

        var generado = _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Assert.IsTrue(generado.SeEscribio, string.Join(" | ", generado.Avisos.Select(a => a.Linea)));

        Contestar(Ruta(), 7, "Sí", "No");
        Contestar(Ruta(), 8, "Sí", "No");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.IsEmpty(vuelta.Descartadas, string.Join(" | ", vuelta.Descartadas.Select(d => d.Motivo)));
        Assert.HasCount(2, vuelta.Marcas);
        CollectionAssert.AreEquivalent(
            new[] { "055-1111-385A", "055-1111-3853" },
            vuelta.Marcas.Select(m => m.Mrn).ToArray(),
            "la cédula con letra final y la de ceros delante tienen que volver letra por letra");
        Assert.IsTrue(vuelta.Marcas.All(m => m.NumeroCaso == "BALC2609"));
        Assert.IsTrue(vuelta.Marcas.All(m => m.PasoPreparacion == true && m.PasoListoParaElTemplo == true));
        Assert.IsTrue(vuelta.Marcas.All(m => m.LlamoAlLider == false));
    }

    /// <summary>C6-6: el estado del documento lo escribe el Excel, con el nombre del companero.</summary>
    [TestMethod]
    public void ConLosSeisPasosEnSiElDocumentoVuelveMarcadoCompleta()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena", "055-1111-385A");
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(Ruta(), 7, "Sí");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.AreEqual(EstadoDeRecomendacion.Completa, vuelta.Marcas[0].EstadoDeLaRecomendacion);

        var aplicado = _base.Paquetes.AplicarMarcas(vuelta.Marcas, _sandy, Ruta());
        Assert.IsTrue(aplicado.SeEscribio);
        var guardado = _base.Almacen.Casos[caso];
        Assert.AreEqual("completa", guardado.EstadoRecomendacion);
        Assert.AreEqual(_sandy, guardado.EstadoDelCompaneroPor, "lo firma el compañero, no Miguel");
        Assert.AreEqual(Ruta(), guardado.EstadoMarcadoOrigen, "de dónde salió la marca es el archivo que la trajo");
    }

    /// <summary>
    /// Dado un documento que volvió marcado por Sandy, cuando Miguel lo corrige encima,
    /// entonces manda lo de Miguel Y NO se borra lo que dijo Sandy.
    /// </summary>
    /// <remarks>
    /// Es la partición que hizo la migración 14 y que llevaba desde entonces sin usarse:
    /// `estado_recomendacion` es el vigente y `estado_del_companero` es lo que dijo la
    /// hoja. Sin esta prueba, aplicar el Excel podía dejar las tres columnas del compañero
    /// nulas —como estaban en la base del dueño— sin que nadie se enterara.
    /// </remarks>
    [TestMethod]
    public void CuandoMiguelCorrigeEncimaNoSeBorraLoQueDijoElCompanero()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena", "055-1111-385A");
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Escribir(Ruta(), 7, "paso_entrevistas", "No");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        _base.Paquetes.AplicarMarcas(vuelta.Marcas, _sandy, Ruta());

        // Miguel corrige por el mismo camino que la pantalla de Revisar: `MarcarEstado`.
        var miguel = _base.Companero("Miguel");
        new RepositorioDeCasosFalso(_base.Almacen).MarcarEstado(
            caso, EstadoDeRecomendacion.Completa, miguel, "a mano en la pantalla Revisar");

        var guardado = _base.Almacen.Casos[caso];

        Assert.AreEqual("completa", guardado.EstadoRecomendacion, "la corrección de Miguel manda");
        Assert.AreEqual(miguel, guardado.EstadoMarcadoPor, "la marca vigente la firma Miguel");
        Assert.AreEqual(
            "no_completa",
            guardado.EstadoDelCompanero,
            "la corrección de Miguel se llevó por delante lo que dijo Sandy");
        Assert.AreEqual(
            _sandy, guardado.EstadoDelCompaneroPor, "el nombre de Sandy se perdió al corregir encima");
    }

    /// <summary>Vigila que un solo «No» entre seis marque el documento no completo y la persona «incompleta».</summary>
    [TestMethod]
    public void ConUnPasoEnNoElDocumentoVuelveMarcadoNoCompleta()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena", "055-1111-385A");
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(Ruta(), 7, "Sí");
        Escribir(Ruta(), 7, "paso_entrevistas", "No");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.AreEqual(EstadoDeRecomendacion.NoCompleta, vuelta.Marcas[0].EstadoDeLaRecomendacion);
        Assert.AreEqual("incompleta", vuelta.Marcas[0].EstadoPropuesto);
    }

    /// <summary>Sin contestar NO es «No»: una casilla en blanco es una pregunta que nadie miró.</summary>
    [TestMethod]
    public void ConUnPasoEnBlancoElDocumentoNoSeMarcaNiCompletaNiNoCompleta()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena", "055-1111-385A");
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(Ruta(), 7, "Sí");
        Escribir(Ruta(), 7, "paso_entrevistas", null);

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.AreEqual(EstadoDeRecomendacion.SinMarcar, vuelta.Marcas[0].EstadoDeLaRecomendacion);
    }

    /// <summary>Una persona del caso que no volvió deja el documento sin marcar. No se da por completo lo que falta.</summary>
    [TestMethod]
    public void SiUnaPersonaDelCasoNoVuelveElDocumentoNoSeDaPorCompleto()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena", "055-1111-385A", 1);
        _base.Persona(caso, "Julia", "055-1111-3853", 2);
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(Ruta(), 7, "Sí");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.HasCount(1, vuelta.Marcas);
        Assert.AreEqual(EstadoDeRecomendacion.SinMarcar, vuelta.Marcas[0].EstadoDeLaRecomendacion);
    }

    // ─────────────────────── dos casos, un mismo numero ───────────────────────

    /// <summary>
    /// C6-3, el motivo entero de que la clave lleve el id: dos familias distintas de la misma
    /// unidad y el mismo mes comparten numero de caso.
    /// </summary>
    [TestMethod]
    public void DosCasosConElMismoNumeroNoSeConfundenPorqueLaClaveLlevaElId()
    {
        var primero = _base.Caso("BALC2609");
        var segundo = _base.Caso("BALC2609");
        _base.Persona(primero, "Elena", "055-1111-385A");
        _base.Persona(segundo, "Otra persona", "055-1111-385A");

        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [primero, segundo], Ruta());
        Contestar(Ruta(), 7, "Sí");
        Contestar(Ruta(), 8, "No");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.IsEmpty(vuelta.Descartadas, string.Join(" | ", vuelta.Descartadas.Select(d => d.Motivo)));
        Assert.HasCount(2, vuelta.Marcas);
        Assert.AreEqual(EstadoDeRecomendacion.Completa, vuelta.Marcas[0].EstadoDeLaRecomendacion);
        Assert.AreEqual(EstadoDeRecomendacion.NoCompleta, vuelta.Marcas[1].EstadoDeLaRecomendacion,
            "cada fila tiene que ir a SU familia: la clave lleva el id del caso y ese no se repite");
    }

    /// <summary>
    /// Una clave vieja de dos partes sobre dos casos que comparten numero: se descarta con su
    /// motivo, y NUNCA se escoge una de las dos familias.
    /// </summary>
    [TestMethod]
    public void UnaClaveViejaSinElIdDaSuMotivoYNoSeAplicaANingunaFamilia()
    {
        var primero = _base.Caso("BALC2609");
        var segundo = _base.Caso("BALC2609");
        var lyris = _base.Persona(primero, "Elena", "055-1111-385A");
        var otra = _base.Persona(segundo, "Otra persona", "055-1111-385A");

        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [primero], Ruta());
        Contestar(Ruta(), 7, "Sí");
        // Un paquete generado antes del 2026-09-03 traía la clave con dos partes.
        Escribir(Ruta(), 7, "clave", "BALC2609:055-1111-385A");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.IsEmpty(vuelta.Marcas, "una clave ambigua NO se aplica a ninguna de las dos familias");
        Assert.HasCount(1, vuelta.Descartadas);
        StringAssert.Contains(vuelta.Descartadas[0].Motivo, "no se sabe a cuál se refiere");
        StringAssert.Contains(vuelta.Descartadas[0].Motivo, "Vuelva a generar el paquete");
        Assert.AreEqual(7, vuelta.Descartadas[0].FilaExcel);
        Assert.IsNull(_base.Almacen.Personas[lyris].PropuestoPor, "ni a la primera familia");
        Assert.IsNull(_base.Almacen.Personas[otra].PropuestoPor, "ni a la segunda");
    }

    /// <summary>Una clave vieja que solo apunta a una persona sigue volviendo: un Excel antiguo no se tira.</summary>
    [TestMethod]
    public void UnaClaveViejaQueNoEsAmbiguaSigueVolviendo()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena", "055-1111-385A");
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(Ruta(), 7, "Sí");
        Escribir(Ruta(), 7, "clave", "BALC2609:055-1111-385A");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.IsEmpty(vuelta.Descartadas);
        Assert.HasCount(1, vuelta.Marcas);
    }

    // ─────────────────────────── los descartes ───────────────────────────

    /// <summary>Vigila que con la celda de la clave vacía la fila caiga en descartados con su motivo, con el nombre guardado pero nunca usado para casar.</summary>
    [TestMethod]
    public void UnaClaveBorradaDaSuMotivoYNoSeBuscaPorNombre()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena", "055-1111-385A");
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(Ruta(), 7, "Sí");
        Escribir(Ruta(), 7, "clave", null);

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.IsEmpty(vuelta.Marcas);
        StringAssert.Contains(vuelta.Descartadas[0].Motivo, "no trae nada en la columna «clave»");
        StringAssert.Contains(vuelta.Descartadas[0].Motivo, "nunca se empareja por nombre");
        Assert.AreEqual("Elena", vuelta.Descartadas[0].Nombre, "el nombre se guarda para poder mirarla, no para casarla");
    }

    /// <summary>Vigila que una clave sin la forma se descarte con un motivo que repite lo que venía escrito.</summary>
    [TestMethod]
    public void UnaClaveConFormaRaraDaSuMotivoYLoQueVeniaEscrito()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena", "055-1111-385A");
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(Ruta(), 7, "Sí");
        Escribir(Ruta(), 7, "clave", "esto no es una clave");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.IsEmpty(vuelta.Marcas);
        StringAssert.Contains(vuelta.Descartadas[0].Motivo, "no tiene la forma esperada");
        StringAssert.Contains(vuelta.Descartadas[0].Motivo, "esto no es una clave");
    }

    /// <summary>Vigila que una clave con forma pero sin par en la base se descarte y no cree ninguna persona nueva.</summary>
    [TestMethod]
    public void UnaFilaQueNoCasaConNadieDaSuMotivoYNoSeInserta()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena", "055-1111-385A");
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(Ruta(), 7, "Sí");
        Escribir(Ruta(), 7, "clave", "BALC2609:000-0000-0000:999");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.IsEmpty(vuelta.Marcas);
        StringAssert.Contains(vuelta.Descartadas[0].Motivo, "no existe en la base");
        Assert.HasCount(1, _base.Almacen.Personas, "no se inserta ninguna persona nueva");
    }

    /// <summary>Vigila que «más o menos» en un paso descarte la fila entera, nombrando la columna y el texto.</summary>
    [TestMethod]
    public void UnaRespuestaQueNadiePuedeInterpretarDejaLaFilaEnteraSinAplicar()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena", "055-1111-385A");
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(Ruta(), 7, "Sí");
        Escribir(Ruta(), 7, "paso_entrevistas", "más o menos");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.IsEmpty(vuelta.Marcas, "no se aplica media fila: el paso que no se entendió podría ser el que dice que falta algo");
        StringAssert.Contains(vuelta.Descartadas[0].Motivo, "más o menos");
        StringAssert.Contains(vuelta.Descartadas[0].Motivo, "5. Entrevistas");
    }

    /// <summary>Vigila que «pendiente», que no está en el menú, se lea como «No»: el menú es una ayuda, no una reja.</summary>
    [TestMethod]
    public void UnaRespuestaEscritaAManoQueSeEntiendeSeAceptaAunqueNoSalgaDelMenu()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena", "055-1111-385A");
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(Ruta(), 7, "Sí");
        Escribir(Ruta(), 7, "paso_entrevistas", "pendiente");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.IsEmpty(vuelta.Descartadas);
        Assert.IsFalse(vuelta.Marcas[0].PasoEntrevistas, "«pendiente» es un no escrito a mano, y el menú es una ayuda, no una reja");
    }

    /// <summary>Vigila que la segunda fila con la misma clave se descarte nombrando la fila donde ya venía.</summary>
    [TestMethod]
    public void LaMismaClaveDosVecesEnElMismoArchivoDejaLaSegundaConSuMotivo()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena", "055-1111-385A", 1);
        _base.Persona(caso, "Julia", "055-1111-3853", 2);
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(Ruta(), 7, "Sí");
        Contestar(Ruta(), 8, "No");
        Escribir(Ruta(), 8, "clave", Columnas.ArmarLaClave("BALC2609", "055-1111-385A", caso));

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.HasCount(1, vuelta.Marcas);
        StringAssert.Contains(vuelta.Descartadas[0].Motivo, "ya venía en la fila 7");
    }

    /// <summary>C6-4: las descartadas se ven desde la interfaz, o sea, quedan escritas.</summary>
    [TestMethod]
    public void LasDescartadasQuedanEscritasConSuArchivoSuFilaYSuCompanero()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena", "055-1111-385A");
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(Ruta(), 7, "Sí");
        Escribir(Ruta(), 7, "clave", null);

        _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        var anotada = _base.Almacen.Descartadas.Values.Single();
        Assert.AreEqual(_sandy, anotada.CompaneroId);
        Assert.AreEqual(Ruta(), anotada.RutaExcel);
        Assert.AreEqual(7, anotada.FilaExcel);
        Assert.AreEqual("2026-09-04 10:30:00", anotada.RegistradoEn);
    }

    // ─────────────────────────── el aviso al generar ───────────────────────────

    /// <summary>C6-5: al generar se avisa NOMBRANDO a quien no tiene MRN.</summary>
    [TestMethod]
    public void AlGenerarSeAvisaNombrandoAQuienNoTieneCedula()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena Rosa Muestra", null, 1);
        _base.Persona(caso, "Julia", "055-1111-3853", 2);

        var generado = _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Assert.IsTrue(generado.SeEscribio, "avisar nunca es impedir: el paquete sale igual");
        var aviso = generado.Avisos.Single(a => a.Linea.Contains("SIN cédula"));
        StringAssert.Contains(aviso.Linea, "Elena Rosa Muestra", "«hay 1 persona sin MRN» obliga a abrir el Excel a buscarla, y entonces no se busca");
        Assert.AreEqual(GravedadDeAviso.Advertencia, aviso.Gravedad);
    }

    /// <summary>Y la consecuencia que ese aviso anuncia se cumple: esa fila vuelve descartada.</summary>
    [TestMethod]
    public void LaPersonaSinCedulaVuelveDescartadaYPierdeSusSieteRespuestas()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena Rosa Muestra", null, 1);
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(Ruta(), 7, "Sí");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.IsEmpty(vuelta.Marcas);
        Assert.HasCount(1, vuelta.Descartadas);
    }

    // ─────────────────────── companero y archivo bloqueado ───────────────────────

    /// <summary>Vigila que un compañero inexistente no deje archivo y el aviso diga su número.</summary>
    [TestMethod]
    public void GenerarParaUnCompaneroQueNoExisteNoEscribeNadaYLoDice()
    {
        var resultado = _base.Paquetes.GenerarExcelDeCompanero(999, [], Ruta());
        Assert.IsFalse(resultado.SeEscribio);
        Assert.IsFalse(File.Exists(Ruta()));
        StringAssert.Contains(resultado.Avisos[0].Linea, "999");
    }

    /// <summary>C6-7: Excel abierto y bloqueado — aviso en espanol, sin caida, sin tocar el que ya estaba.</summary>
    [TestMethod]
    public void ConElArchivoAbiertoPorOtroProgramaSeAvisaEnEspanolYNoSeCae()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena", "055-1111-385A");
        Assert.IsTrue(_base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta()).SeEscribio);
        var tamanoAntes = new FileInfo(Ruta()).Length;

        using (File.Open(Ruta(), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var segundo = _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
            Assert.IsFalse(segundo.SeEscribio);
            var aviso = segundo.Avisos.Single(a => a.Gravedad == GravedadDeAviso.Problema);
            StringAssert.Contains(aviso.Linea, "otro programa lo tiene abierto");
            StringAssert.Contains(aviso.Detalle!, "NO se ha tocado");
        }

        Assert.AreEqual(tamanoAntes, new FileInfo(Ruta()).Length, "el .xlsx que ya estaba se queda intacto");
    }

    /// <summary>Vigila que una ruta inexistente vuelva como problema en la franja, no como excepción.</summary>
    [TestMethod]
    public void LeerUnArchivoQueNoExisteDevuelveSuAvisoYNoLevanta()
    {
        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta("no_existe.xlsx"), _sandy);
        Assert.IsEmpty(vuelta.Marcas);
        Assert.AreEqual(GravedadDeAviso.Problema, vuelta.Avisos[0].Gravedad);
        StringAssert.Contains(vuelta.Avisos[0].Detalle!, "No existe el archivo");
    }

    // ─────────── el id del caso, de la lectura a la aplicacion ───────────

    /// <summary>
    /// El id del caso viaja de la clave del Excel a la marca, y de la marca a la base.
    /// </summary>
    /// <remarks>
    /// Hasta el 2026-09-04 <c>MarcaDelCompanero</c> no llevaba el id, y aplicar tenia que
    /// resolver otra vez por el par <c>numero_caso + mrn</c>: con dos casos del mismo numero
    /// eso descartaba por ambiguo lo que la lectura habia resuelto bien. El supervisor anadio
    /// el campo al contrato (commit 5f27756) y esta prueba exige lo contrario de lo que la
    /// anterior documentaba: DOS marcas leidas, DOS aplicadas, cada una a su caso, cero
    /// descartes.
    /// </remarks>
    [TestMethod]
    public void DosCasosConElMismoNumeroSeAplicanCadaUnoASuCaso()
    {
        var primero = _base.Caso("BALC2609");
        var segundo = _base.Caso("BALC2609");
        var lyris = _base.Persona(primero, "Elena", "055-1111-385A");
        var otra = _base.Persona(segundo, "Otra persona", "055-1111-385A");

        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [primero, segundo], Ruta());
        Contestar(Ruta(), 7, "Sí");
        Contestar(Ruta(), 8, "No");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.HasCount(2, vuelta.Marcas);
        CollectionAssert.AreEqual(
            new long?[] { primero, segundo },
            vuelta.Marcas.Select(m => m.CasoId).ToArray(),
            "la clave del Excel es CASO:MRN:ID y el id tiene que llegar entero hasta la marca");

        var aplicado = _base.Paquetes.AplicarMarcas(vuelta.Marcas, _sandy, Ruta());
        Assert.IsTrue(aplicado.SeEscribio);
        Assert.IsEmpty(_base.Almacen.Descartadas, "con el id no hay ambigüedad que descartar");

        Assert.AreEqual(_sandy, _base.Almacen.Personas[lyris].PropuestoPor);
        Assert.AreEqual(_sandy, _base.Almacen.Personas[otra].PropuestoPor);
        Assert.IsTrue(_base.Almacen.Personas[lyris].PasoEntrevistas, "la fila 7 dijo que sí y va a SU familia");
        Assert.IsFalse(_base.Almacen.Personas[otra].PasoEntrevistas, "la fila 8 dijo que no y va a la OTRA");
        Assert.AreEqual("completa", _base.Almacen.Casos[primero].EstadoRecomendacion);
        Assert.AreEqual("no_completa", _base.Almacen.Casos[segundo].EstadoRecomendacion);
    }

    /// <summary>
    /// Una hoja vieja —generada antes del 2026-09-03, sin el id en la clave— sigue dando su
    /// descarte con su motivo cuando el par lleva a dos familias. Ese camino NO cambia.
    /// </summary>
    /// <remarks>
    /// Se construye la marca con el id en nulo a mano, que es como llega una hoja vieja: el
    /// campo tiene valor por defecto justamente para eso.
    /// </remarks>
    [TestMethod]
    public void UnaHojaViejaSinIdSigueDandoSuDescartePorClaveAmbigua()
    {
        var primero = _base.Caso("BALC2609");
        var segundo = _base.Caso("BALC2609");
        var lyris = _base.Persona(primero, "Elena", "055-1111-385A");
        var otra = _base.Persona(segundo, "Otra persona", "055-1111-385A");

        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [primero], Ruta());
        Contestar(Ruta(), 7, "Sí");
        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        var comoUnaHojaVieja = vuelta.Marcas.Select(marca => marca with { CasoId = null }).ToList();

        var aplicado = _base.Paquetes.AplicarMarcas(comoUnaHojaVieja, _sandy, Ruta());

        Assert.IsFalse(aplicado.SeEscribio);
        Assert.IsNull(_base.Almacen.Personas[lyris].PropuestoPor, "ni a la primera familia");
        Assert.IsNull(_base.Almacen.Personas[otra].PropuestoPor, "ni a la segunda");
        var descartada = _base.Almacen.Descartadas.Values.Single();
        StringAssert.Contains(descartada.Motivo, "no se sabe a cuál se refiere");
        StringAssert.Contains(descartada.Motivo, "Vuelva a generar el paquete");
    }

    /// <summary>
    /// Una hoja vieja cuyo par NO es ambiguo se aplica igual: un Excel antiguo no se tira.
    /// </summary>
    [TestMethod]
    public void UnaHojaViejaSinIdQueNoEsAmbiguaSeAplicaIgual()
    {
        var caso = _base.Caso("BALC2609");
        var lyris = _base.Persona(caso, "Elena", "055-1111-385A");

        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(Ruta(), 7, "Sí");
        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        var comoUnaHojaVieja = vuelta.Marcas.Select(marca => marca with { CasoId = null }).ToList();

        Assert.IsTrue(_base.Paquetes.AplicarMarcas(comoUnaHojaVieja, _sandy, Ruta()).SeEscribio);
        Assert.AreEqual(_sandy, _base.Almacen.Personas[lyris].PropuestoPor);
        Assert.IsEmpty(_base.Almacen.Descartadas);
    }

    /// <summary>
    /// Con el id puesto, el numero de caso YA NO SE MIRA: si Miguel lo corrigio despues de
    /// generar el paquete, el Excel del companero sigue diciendo el viejo y exigir que
    /// coincidiera tiraria trabajo bueno.
    /// </summary>
    [TestMethod]
    public void ConElIdPuestoUnNumeroDeCasoCorregidoDespuesNoTiraLaFila()
    {
        var caso = _base.Caso("BALC2609");
        var lyris = _base.Persona(caso, "Elena", "055-1111-385A");

        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(Ruta(), 7, "Sí");
        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);

        // Miguel corrige el número del caso en la base entre la ida y la vuelta.
        _base.Almacen.Casos[caso] = _base.Almacen.Casos[caso] with { NumeroCaso = "CASP2609" };

        Assert.IsTrue(_base.Paquetes.AplicarMarcas(vuelta.Marcas, _sandy, Ruta()).SeEscribio);
        Assert.AreEqual(_sandy, _base.Almacen.Personas[lyris].PropuestoPor);
        Assert.IsEmpty(_base.Almacen.Descartadas);
    }

    /// <summary>Con numeros de caso distintos —el caso normal— aplicar funciona entero.</summary>
    [TestMethod]
    public void ConNumerosDeCasoDistintosAplicarEscribeLaPropuestaYElEstado()
    {
        var uno = _base.Caso("BALC2609");
        var dos = _base.Caso("CASP2610");
        var lyris = _base.Persona(uno, "Elena", "055-1111-385A");
        var julie = _base.Persona(dos, "Julia", "055-1111-3853");

        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [uno, dos], Ruta());
        Contestar(Ruta(), 7, "Sí", "No");
        Contestar(Ruta(), 8, "No", "Sí");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        var aplicado = _base.Paquetes.AplicarMarcas(vuelta.Marcas, _sandy, Ruta());

        Assert.IsTrue(aplicado.SeEscribio);
        Assert.AreEqual(_sandy, _base.Almacen.Personas[lyris].PropuestoPor);
        Assert.IsTrue(_base.Almacen.Personas[lyris].PasoEntrevistas);
        Assert.IsFalse(_base.Almacen.Personas[lyris].LlamoAlLider);
        Assert.IsFalse(_base.Almacen.Personas[julie].PasoEntrevistas);
        Assert.AreEqual("completa", _base.Almacen.Casos[uno].EstadoRecomendacion);
        Assert.AreEqual("no_completa", _base.Almacen.Casos[dos].EstadoRecomendacion);
    }

    /// <summary>La firma por campo NO se toca nunca: es de Miguel y solo de Miguel.</summary>
    [TestMethod]
    public void AplicarUnExcelNoFirmaNingunCampo()
    {
        var caso = _base.Caso("BALC2609");
        _base.Persona(caso, "Elena", "055-1111-385A");
        _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Contestar(Ruta(), 7, "Sí");

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        _base.Paquetes.AplicarMarcas(vuelta.Marcas, _sandy, Ruta());

        Assert.IsEmpty(_base.Almacen.Procedencias, "el Excel escribe el ESTADO del documento, nunca la firma de un campo");
    }
}
