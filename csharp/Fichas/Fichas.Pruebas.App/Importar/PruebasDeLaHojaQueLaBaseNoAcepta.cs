using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Lectura;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// Una hoja NO se pierde porque la base rechace uno de sus campos. Ni una.
/// </summary>
/// <remarks>
/// <para>⚠️ <b>Es pérdida de datos del dueño, medida el 2026-09-05</b> sobre sus dos
/// formularios de grupo (los <c>SURB2609</c>, seis hojas cada uno): <b>4 de sus 20 hojas no
/// entraban</b>, y con cada una se iban sus personas. La salida era «El número de unidad no
/// tiene la forma esperada» seguida de «No se pudo guardar. La base rechazó el dato», dos
/// veces, y <c>Entro=False</c>.</para>
///
/// <para><b>Por qué pasaba.</b> Las hojas 5 y 6 de cada grupo vienen del revés en el escaneo,
/// así que el OCR devuelve texto sin sentido: <c>unidad_numero</c> con <c>«WANLCA Bzeench»</c>
/// dentro y <c>fecha_viaje</c> con <c>«eptomber 2026 Currency:»</c>. La lectura las devuelve
/// tal cual y eso está bien (requisito 9: se enseña lo que el papel decía). Pero el esquema
/// lleva <c>CHECK (unidad_numero GLOB '[0-9]…')</c> y <c>CHECK (fecha_viaje GLOB
/// '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]')</c>, así que SQLite rechazaba la fila entera.
/// El reintento que existía solo sabía retirar <c>numero_caso</c>, que no era el culpable.</para>
///
/// <para><b>La regla que restaura</b>, del dueño y de las primeras del proyecto: <b>un
/// documento NUNCA se rechaza</b>. Lo que no se pueda leer deja su renglón con su motivo y se
/// corrige después; el valor crudo sigue en <c>procedencia_campo</c>, que es de donde la
/// pantalla de corrección lo lee.</para>
///
/// <para>⛔ <b>No hace falta ningún doble aquí.</b> Los dos CHECK que importan están en la
/// base que el propio C# crea, así que quien rechaza es SQLite de verdad. Es la diferencia
/// con <see cref="PruebasDelNumeroQueLaBaseRechaza"/>, que sí necesita un doble porque el
/// CHECK del número de caso ya no existe en una base nueva.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaHojaQueLaBaseNoAcepta : BaseDeImportacion
{
    /// <summary>Lo que el OCR sacó de la hoja 6 del grupo real, letra por letra.</summary>
    private const string NumeroDeUnidadDelReves = "WANLCA Bzeench";

    /// <summary>Lo que el OCR sacó de la fecha en la hoja 5 del mismo grupo.</summary>
    private const string FechaDelReves = "eptomber 2026 Currency:";

    /// <summary>
    /// Un número de unidad que la base rechaza NO se lleva la hoja ni a sus personas.
    /// </summary>
    [TestMethod]
    public void UnNumeroDeUnidadQueLaBaseRechazaNoSeLlevaLaHoja()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
        [
            HojaDelGrupo(pagina: 6, unidadNumero: NumeroDeUnidadDelReves, fechaViaje: "2026-09-05"),
        ]);

        Assert.IsTrue(salida[0].Entro, "la hoja NO se pierde porque la base no acepte el número de unidad");
        Assert.AreEqual(1, salida[0].Personas, "su persona entra con ella");
        Assert.AreEqual(1, Contar("casos"));
        Assert.AreEqual(1, Contar("personas"));
    }

    /// <summary>Una fecha que la base rechaza tampoco se lleva la hoja.</summary>
    /// <remarks>
    /// Va aparte de la del número porque son dos CHECK distintos, y una corrección que
    /// arreglara solo uno pasaría la otra prueba estando media hecha.
    /// </remarks>
    [TestMethod]
    public void UnaFechaDeViajeQueLaBaseRechazaNoSeLlevaLaHoja()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
        [
            HojaDelGrupo(pagina: 5, unidadNumero: "7000011", fechaViaje: FechaDelReves),
        ]);

        Assert.IsTrue(salida[0].Entro, "la hoja NO se pierde porque la base no acepte la fecha");
        Assert.AreEqual(1, salida[0].Personas);
    }

    /// <summary>Las dos cosas mal a la vez, que es lo que traía la hoja 5 de verdad.</summary>
    [TestMethod]
    public void ConLosDosCamposMalLaHojaEntraIgualYConSuPersona()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
        [
            HojaDelGrupo(pagina: 5, unidadNumero: "ceniceq t zcench:", fechaViaje: FechaDelReves),
        ]);

        Assert.IsTrue(salida[0].Entro);
        Assert.AreEqual(1, salida[0].Personas);
    }

    /// <summary>
    /// Lo que se retiró se DICE, y el valor crudo queda donde se puede corregir.
    /// </summary>
    /// <remarks>
    /// Sin esta mitad, «la hoja entra» se podría cumplir tirando el dato en silencio, que es
    /// exactamente lo que este proyecto no hace. El criterio del dueño tiene las dos partes:
    /// entra, <b>y</b> lo que no se pudo leer se dice y se puede corregir después.
    /// </remarks>
    [TestMethod]
    public void LoQueLaBaseNoAceptoSeDiceYSeGuardaSuValorCrudo()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
        [
            HojaDelGrupo(pagina: 6, unidadNumero: NumeroDeUnidadDelReves, fechaViaje: "2026-09-05"),
        ]);

        Assert.IsTrue(
            salida[0].Avisos.Any(aviso => aviso.Linea.Contains(NumeroDeUnidadDelReves, StringComparison.Ordinal)),
            "el aviso tiene que decir QUÉ valor no entró, o no hay nada que teclear");

        var renglones = Datos.Ilegibles.Listar(FiltroDeIlegibles.Todo, Pagina.Primera(50)).Elementos;
        Assert.IsTrue(
            renglones.Any(renglon => renglon.Detalle is not null
                                     && renglon.Detalle.Contains(NumeroDeUnidadDelReves, StringComparison.Ordinal)),
            "tiene que quedar un renglón con el valor crudo dentro");

        var procedencia = Datos.Procedencia.DeRegistro(
            TablaDeProcedencia.Casos, salida[0].CasoId!.Value);
        var delNumero = procedencia.Single(fila => fila.Campo == CamposDeLaHojaUnidadNumero);
        Assert.AreEqual(NumeroDeUnidadDelReves, delNumero.ValorOcr,
            "la procedencia guarda lo que el papel decía: es de donde la corrección lo lee");
    }

    /// <summary>Control positivo: una hoja sana NO pierde ni el número ni la fecha.</summary>
    /// <remarks>
    /// Sin esto, un guardado que retirara SIEMPRE los dos campos pasaría las cuatro pruebas
    /// de arriba y dejaría todos los casos sin unidad y sin fecha.
    /// </remarks>
    [TestMethod]
    public void UnaHojaSanaSeGuardaConSuNumeroDeUnidadYSuFecha()
    {
        var salida = Guardado.GuardarLasHojasDelDocumento(
        [
            HojaDelGrupo(pagina: 1, unidadNumero: "7000011", fechaViaje: "2026-09-05"),
        ]);

        var caso = Datos.Casos.Obtener(salida[0].CasoId!.Value)!;
        Assert.AreEqual("7000011", caso.UnidadNumero);
        Assert.AreEqual("2026-09-05", caso.FechaViaje);
        Assert.AreEqual("SURB2609", caso.NumeroCaso, "el número de caso bueno NO se retira");
    }

    /// <summary>El nombre de la columna del número de unidad, tal como lo escribe la lectura.</summary>
    private const string CamposDeLaHojaUnidadNumero = "unidad_numero";

    /// <summary>Una hoja del grupo real, con los valores que se le digan en la unidad y la fecha.</summary>
    /// <param name="pagina">La página del PDF; también numera a su persona.</param>
    /// <param name="unidadNumero">Lo leído en el número de unidad, bien o mal formado.</param>
    /// <param name="fechaViaje">Lo leído en la fecha de viaje, bien o mal formada.</param>
    private static HojaLeida HojaDelGrupo(int pagina, string unidadNumero, string fechaViaje) => new(
        RutaPdf: @"C:\pdfs\SURB2609_grupo.pdf",
        Pagina: pagina,
        Campos:
        [
            new(TablaDeProcedencia.Casos, "numero_caso", "SURB2609", OrigenDeCampo.Anotacion, 1.0, null),
            new(TablaDeProcedencia.Casos, "fecha_viaje", fechaViaje, OrigenDeCampo.Ocr, 0.7, null),
            new(TablaDeProcedencia.Casos, CamposDeLaHojaUnidadNumero, unidadNumero, OrigenDeCampo.Ocr, 0.6, null),
            new(TablaDeProcedencia.Casos, "unidad_nombre", null, OrigenDeCampo.Vacio, null, null),
            new(TablaDeProcedencia.Casos, "templo_nombre", "Belem Temple Brazil", OrigenDeCampo.Ocr, 0.9, null),
            new(TablaDeProcedencia.Personas, "nombre", $"Persona de la hoja {pagina}", OrigenDeCampo.Ocr, 0.9, null, 1),
            new(TablaDeProcedencia.Personas, "mrn", $"052-1116-04{pagina:D2}", OrigenDeCampo.Ocr, 0.9, null, 1),
        ],
        Avisos: [],
        Ilegible: null,
        CapturaManual: false,
        LineasLeidas: 92,
        TextoLeido: "texto de la hoja",
        Segundos: 11.4);
}
