using ClosedXML.Excel;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Paquetes;

namespace Fichas.Pruebas.Paquetes;

/// <summary>
/// El punto 5 de la v16: el Excel del paquete sale con las preguntas que ya estan contestadas,
/// y la vuelta no pisa con blanco lo que ya estaba.
/// </summary>
/// <remarks>
/// <para>
/// Palabras del dueno (PENDIENTES.md, «Para la v16», 5, 2026-09-16): <i>«en el sistema solo le
/// faltan 2 preguntas por llenar, pero cuando le asigné los casos a un gerente el paquete de
/// Excel no marca las preguntas que ya están completas. Eso debería hacerlo el programa:
/// llenar lo que ya está completo en el Excel.»</i>
/// </para>
/// <para>
/// Cada prueba sale del criterio de cierre del pase y no del codigo: (a) persona con 4 de 6
/// en «sí» → el Excel lleva «Sí» en esas 4 y las otras 2 vacias; (b) la vuelta con las 4
/// intactas mas 2 nuevas → 6 en «sí»; (c) con una del sistema BORRADA → sigue en «sí»; (d)
/// con un «No» explicito encima → «no». La regla (c) la decidio el supervisor a falta de
/// respuesta del dueno, y esta escrita en <see cref="Pasos.ConLoQueYaEstabaGuardado"/> para
/// que el la cambie.
/// </para>
/// </remarks>
[TestClass]
public class PruebasDeLoQueYaVieneContestado
{
    /// <summary>Cuando contesto Miguel las cuatro, en ISO-8601 con hora, como lo guarda la base.</summary>
    private const string CuandoContesto = "2026-09-16 10:15:00";

    /// <summary>Por que via las contesto; texto libre, el mismo que escribe la ventana de Revisar.</summary>
    private const string DesdeDonde = "a mano en la pantalla";

    /// <summary>La base en memoria de esta prueba.</summary>
    private BaseInventada _base = null!;

    /// <summary>La carpeta temporal donde se escriben los <c>.xlsx</c>.</summary>
    private string _carpeta = null!;

    /// <summary>El compañero al que se le genera el paquete.</summary>
    private long _sandy;

    /// <summary>Quien contesto las cuatro desde la pantalla; NO es el que recibe el paquete.</summary>
    private long _miguel;

    /// <summary>Monta la base, la carpeta y los dos compañeros.</summary>
    [TestInitialize]
    public void Preparar()
    {
        _base = new BaseInventada();
        _carpeta = BaseInventada.CarpetaDePruebas();
        _sandy = _base.Companero("Sandy");
        _miguel = _base.Companero("Jose Muestra");
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

    /// <summary>
    /// Un caso con una persona que ya tiene las cuatro primeras en «sí», firmadas por Miguel
    /// desde la pantalla, y las dos ultimas sin contestar. Es la captura del dueno.
    /// </summary>
    /// <param name="llamo">Lo que ya conste de la llamada al lider; nulo por defecto.</param>
    /// <returns>El id del caso y el de la persona.</returns>
    private (long Caso, long Persona) UnaPersonaConCuatroDeSeis(bool? llamo = null)
    {
        var caso = _base.Caso("PULC2609");
        var persona = _base.Persona(caso, "Elena Rosa Muestra", "055-1111-3853");
        _base.Almacen.Personas[persona] = _base.Almacen.Personas[persona] with
        {
            PasoPreparacion = true,
            PasoInformacion = true,
            PasoCitaDelTemplo = true,
            PasoAccionesRequeridas = true,
            LlamoAlLider = llamo,
        };
        _base.Almacen.FirmasDeLosPasos[persona] = new FirmaDeLosPasos(_miguel, CuandoContesto, DesdeDonde);
        return (caso, persona);
    }

    /// <summary>Genera el paquete y falla con los avisos si no se escribio.</summary>
    /// <param name="caso">El caso que lleva.</param>
    private void Generar(long caso)
    {
        var generado = _base.Paquetes.GenerarExcelDeCompanero(_sandy, [caso], Ruta());
        Assert.IsTrue(generado.SeEscribio, string.Join(" | ", generado.Avisos.Select(a => a.Linea)));
    }

    /// <summary>Lo que dice la celda de esa columna en la fila 7, o la cadena vacia.</summary>
    /// <param name="hoja">La pestaña «Por verificar» ya abierta.</param>
    /// <param name="nombreDeColumna">El nombre de la columna en la base.</param>
    private static string Celda(IXLWorksheet hoja, string nombreDeColumna)
        => hoja.Cell(Columnas.PrimeraFilaDeDatos, Columnas.IndiceDe(nombreDeColumna)).GetString();

    /// <summary>Escribe —o vacia, con nulo— una celda de la fila 7, como haria el companero.</summary>
    /// <param name="nombreDeColumna">El nombre de la columna en la base.</param>
    /// <param name="valor">El texto, o nulo para borrar lo que hubiera.</param>
    private void ElCompaneroDeja(string nombreDeColumna, string? valor)
    {
        using var libro = new XLWorkbook(Ruta());
        var celda = libro.Worksheet(Columnas.NombreDeLaHoja).Cell(Columnas.PrimeraFilaDeDatos, Columnas.IndiceDe(nombreDeColumna));
        if (valor is null) celda.Clear(XLClearOptions.Contents); else celda.SetValue(valor);
        libro.Save();
    }

    /// <summary>Lee el Excel devuelto, lo aplica y devuelve la persona tal como quedo en la base.</summary>
    /// <param name="persona">El id de la persona que se mira.</param>
    private Persona LaVueltaAplicadaSobre(long persona)
    {
        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.IsEmpty(vuelta.Descartadas, string.Join(" | ", vuelta.Descartadas.Select(d => d.Motivo)));
        _base.Paquetes.AplicarMarcas(vuelta.Marcas, _sandy, Ruta());
        return _base.Almacen.Personas[persona];
    }

    /// <summary>Las seis respuestas de una persona, en el orden de la pantalla del lider.</summary>
    /// <param name="persona">La persona leída de la base.</param>
    private static bool?[] LasSeisDe(Persona persona) =>
    [
        persona.PasoPreparacion, persona.PasoInformacion, persona.PasoCitaDelTemplo,
        persona.PasoAccionesRequeridas, persona.PasoEntrevistas, persona.PasoListoParaElTemplo,
    ];

    // ─────────────────────────────── la ida ───────────────────────────────

    /// <summary>
    /// Dada una persona con 4 de 6 en «sí», cuando se genera el paquete, entonces el Excel
    /// lleva «Sí» en esas 4 celdas y las otras 2 y la llamada en blanco.
    /// </summary>
    [TestMethod]
    public void ConCuatroDeSeisEnSiElExcelLlevaCuatroSiYDosCeldasVacias()
    {
        var (caso, _) = UnaPersonaConCuatroDeSeis();
        Generar(caso);

        using var libro = new XLWorkbook(Ruta());
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        Assert.AreEqual("Sí", Celda(hoja, "paso_preparacion"));
        Assert.AreEqual("Sí", Celda(hoja, "paso_informacion"));
        Assert.AreEqual("Sí", Celda(hoja, "paso_cita_del_templo"));
        Assert.AreEqual("Sí", Celda(hoja, "paso_acciones_requeridas"));
        Assert.AreEqual(string.Empty, Celda(hoja, "paso_entrevistas"), "la 5 la contesta el compañero");
        Assert.AreEqual(string.Empty, Celda(hoja, "paso_listo_para_el_templo"), "la 6 la contesta el compañero");
        Assert.AreEqual(string.Empty, Celda(hoja, Pasos.ColumnaDeLaLlamada), "la llamada nadie la ha dicho");
    }

    /// <summary>Un «no» guardado sale como «No», y la llamada al lider guardada sale igual que un paso.</summary>
    [TestMethod]
    public void UnNoGuardadoSaleComoNoYLaLlamadaGuardadaSaleEscrita()
    {
        var (caso, persona) = UnaPersonaConCuatroDeSeis(llamo: true);
        _base.Almacen.Personas[persona] = _base.Almacen.Personas[persona] with { PasoEntrevistas = false };
        Generar(caso);

        using var libro = new XLWorkbook(Ruta());
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        Assert.AreEqual("No", Celda(hoja, "paso_entrevistas"));
        Assert.AreEqual("Sí", Celda(hoja, Pasos.ColumnaDeLaLlamada));
    }

    /// <summary>Una persona de la que nadie contesto nada sale como hasta hoy: las siete en blanco.</summary>
    [TestMethod]
    public void SinNadaContestadoLasSieteSalenEnBlancoComoSiempre()
    {
        var caso = _base.Caso("PULC2609");
        _base.Persona(caso, "Julia Luz Inventada", "055-1111-385A");
        Generar(caso);

        using var libro = new XLWorkbook(Ruta());
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        foreach (var columna in Columnas.Todas.Where(c => c.EsSiONo))
            Assert.AreEqual(string.Empty, Celda(hoja, columna.Nombre), columna.Titulo);
    }

    /// <summary>
    /// La celda que ya viene contestada lleva una nota con quien la contesto, cuando y desde
    /// donde; la celda en blanco no lleva ninguna.
    /// </summary>
    [TestMethod]
    public void LaCeldaContestadaLlevaUnaNotaConQuienCuandoYDesdeDonde()
    {
        var (caso, _) = UnaPersonaConCuatroDeSeis();
        Generar(caso);

        using var libro = new XLWorkbook(Ruta());
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        var contestada = hoja.Cell(Columnas.PrimeraFilaDeDatos, Columnas.IndiceDe("paso_preparacion"));
        Assert.IsTrue(contestada.HasComment, "la celda que viene del sistema dice quién la contestó");
        var nota = contestada.GetComment().Text;
        StringAssert.Contains(nota, "Jose Muestra", "quién");
        StringAssert.Contains(nota, "16-09-2026", "cuándo, como lo lee una persona");
        StringAssert.Contains(nota, DesdeDonde, "desde dónde");

        var enBlanco = hoja.Cell(Columnas.PrimeraFilaDeDatos, Columnas.IndiceDe("paso_entrevistas"));
        Assert.IsFalse(enBlanco.HasComment, "lo que falta por contestar no lleva nota");
    }

    /// <summary>La instruccion de la fila 5 dice que lo que ya viene marcado lo puso el sistema.</summary>
    [TestMethod]
    public void LaInstruccionDiceQueLoMarcadoVieneDelSistema()
    {
        var (caso, _) = UnaPersonaConCuatroDeSeis();
        Generar(caso);

        using var libro = new XLWorkbook(Ruta());
        var instruccion = libro.Worksheet(Columnas.NombreDeLaHoja).Cell(5, 1).GetString();
        StringAssert.StartsWith(instruccion, LibroDeTrabajo.InstruccionDelViejo, "lo del Python no se toca");
        StringAssert.Contains(instruccion, LibroDeTrabajo.InstruccionDeLoQueYaVieneMarcado);
        StringAssert.Contains(LibroDeTrabajo.InstruccionDeLoQueYaVieneMarcado, "sistema");
    }

    // ────────────────────────────── la vuelta ──────────────────────────────

    /// <summary>
    /// Dado el Excel con las 4 del sistema intactas, cuando el companero contesta las 2 que
    /// faltaban y vuelve, entonces la persona queda con las 6 en «sí» y el documento completo.
    /// </summary>
    [TestMethod]
    public void LasCuatroIntactasMasDosNuevasDanSeisEnSi()
    {
        var (caso, persona) = UnaPersonaConCuatroDeSeis();
        Generar(caso);
        ElCompaneroDeja("paso_entrevistas", "Sí");
        ElCompaneroDeja("paso_listo_para_el_templo", "Sí");

        var guardada = LaVueltaAplicadaSobre(persona);

        CollectionAssert.AreEqual(new bool?[] { true, true, true, true, true, true }, LasSeisDe(guardada));
        Assert.AreEqual("completa", _base.Almacen.Casos[caso].EstadoRecomendacion);
    }

    /// <summary>
    /// Dado el Excel con un «Sí» del sistema BORRADO por el companero, cuando vuelve, entonces
    /// ese «sí» se conserva: una celda vacia se lee como «no la toque», no como «no».
    /// </summary>
    /// <remarks>
    /// Regla decidida por el supervisor el 2026-09-16 a falta de respuesta del dueno, y
    /// escrita para que el la cambie: solo un «No» explicito cambia un «sí» del sistema.
    /// </remarks>
    [TestMethod]
    public void UnSiDelSistemaBorradoEnLaHojaSeConserva()
    {
        var (caso, persona) = UnaPersonaConCuatroDeSeis();
        Generar(caso);
        ElCompaneroDeja("paso_cita_del_templo", null);
        ElCompaneroDeja("paso_entrevistas", "Sí");
        ElCompaneroDeja("paso_listo_para_el_templo", "Sí");

        var guardada = LaVueltaAplicadaSobre(persona);

        Assert.IsTrue(guardada.PasoCitaDelTemplo, "el «sí» que el compañero borró sigue siendo «sí»");
        CollectionAssert.AreEqual(new bool?[] { true, true, true, true, true, true }, LasSeisDe(guardada));
        Assert.AreEqual("completa", _base.Almacen.Casos[caso].EstadoRecomendacion,
            "con las seis en «sí» —cuatro del sistema, una conservada, dos nuevas— el documento está completo");
    }

    /// <summary>Un «No» explicito encima de un «sí» del sistema SI lo cambia, y el documento sale no completo.</summary>
    [TestMethod]
    public void UnNoExplicitoSobreUnSiDelSistemaLoCambia()
    {
        var (caso, persona) = UnaPersonaConCuatroDeSeis();
        Generar(caso);
        ElCompaneroDeja("paso_cita_del_templo", "No");

        var guardada = LaVueltaAplicadaSobre(persona);

        Assert.IsFalse(guardada.PasoCitaDelTemplo);
        Assert.IsTrue(guardada.PasoPreparacion, "las que no tocó siguen como estaban");
        Assert.AreEqual("no_completa", _base.Almacen.Casos[caso].EstadoRecomendacion);
    }

    /// <summary>La llamada al lider sigue la misma regla: la que venia del sistema y vuelve vacia se conserva.</summary>
    [TestMethod]
    public void LaLlamadaAlLiderDelSistemaBorradaTambienSeConserva()
    {
        var (caso, persona) = UnaPersonaConCuatroDeSeis(llamo: true);
        Generar(caso);
        ElCompaneroDeja(Pasos.ColumnaDeLaLlamada, null);
        ElCompaneroDeja("paso_entrevistas", "No");

        var guardada = LaVueltaAplicadaSobre(persona);

        Assert.IsTrue(guardada.LlamoAlLider);
    }

    /// <summary>
    /// Una fila que vuelve TAL COMO SALIO —con sus cuatro «Sí» del sistema y nada mas— no es
    /// una propuesta del companero: no se aplica, y la firma de Miguel sobre las cuatro no
    /// pasa a nombre del companero.
    /// </summary>
    /// <remarks>
    /// Antes de este cambio las siete salian en blanco y «no trae nada» era «las siete en
    /// blanco». Ahora «no trae nada» es «nada distinto de lo que el sistema ya tenia»; si no,
    /// devolver el paquete sin tocarlo firmaria al companero como quien contesto lo de Miguel.
    /// </remarks>
    [TestMethod]
    public void UnaFilaQueVuelveTalComoSalioNoSeAplicaNiCambiaLaFirma()
    {
        var (caso, persona) = UnaPersonaConCuatroDeSeis();
        Generar(caso);

        var vuelta = _base.Paquetes.LeerExcelDevuelto(Ruta(), _sandy);
        Assert.IsEmpty(vuelta.Marcas, "no hay nada que proponer");
        Assert.IsEmpty(vuelta.Descartadas);
        Assert.IsTrue(vuelta.Avisos.Any(a => a.Linea.Contains("sin nada nuevo", StringComparison.Ordinal)),
            string.Join(" | ", vuelta.Avisos.Select(a => a.Linea)));

        var firma = _base.Almacen.FirmasDeLosPasos[persona];
        Assert.AreEqual(_miguel, firma.Por, "las cuatro siguen siendo de quien las contestó");
        Assert.AreEqual(CuandoContesto, firma.En);
    }
}
