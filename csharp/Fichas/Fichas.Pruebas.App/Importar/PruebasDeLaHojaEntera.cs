using Fichas.App.Cascara;
using Fichas.App.Importar;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// R-4 (plan del 2026-09-15): cada hoja entra entera, en UNA confirmación, o no entra.
/// </summary>
/// <remarks>
/// <para>Sobre SQLite real, como el resto de la importación. Las confirmaciones se cuentan
/// con el «file change counter» de la cabecera del archivo (bytes 24 a 27,
/// <see href="https://www.sqlite.org/fileformat.html#file_change_counter"/>), que en el modo
/// de diario por defecto —el del programa— sube una vez por transacción confirmada que cambió
/// la base. Es el mismo contador de <c>PruebaDelAmbitoDeEscritura</c> en Datos.</para>
///
/// <para>El control positivo es <see cref="SinAmbitoDeGuardado"/>: la misma hoja sin ámbito
/// cuesta las 24 confirmaciones que contó el planificador, así que un guardado que dejara de
/// abrir el ámbito se vería aquí.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaHojaEntera : BaseDeImportacion
{
    /// <summary>Seis personas con cédula: 1 caso + 5 procedencias + 6 personas + 12 procedencias = 24 filas.</summary>
    private static readonly (string Nombre, string? Mrn)[] SeisPersonas =
    [
        ("Ana Prueba", "055-1111-3853"), ("Luis Prueba", "055-2222-3853"), ("Eva Prueba", "055-3333-3853"),
        ("Iván Prueba", "055-4444-3853"), ("Rosa Prueba", "055-5555-3853"), ("Juan Prueba", "055-6666-3853"),
    ];

    /// <summary>
    /// Una hoja con seis personas es UNA confirmación en el archivo; sin ámbito, veinticuatro.
    /// </summary>
    [TestMethod]
    public void UnaHojaDeSeisPersonasEsUnaSolaConfirmacion()
    {
        var antes = ContadorDeCambiosDelArchivo(RutaDeLaBase);

        var resultado = Guardado.GuardarLasHojasDelDocumento([Hoja("C:/pdfs/grupo.pdf", 1, "SURB2609", SeisPersonas)]);

        Assert.IsTrue(resultado[0].Entro);
        Assert.AreEqual(6, resultado[0].Personas);
        Assert.AreEqual(24, Contar("casos") + Contar("personas") + Contar("procedencia_campo"), "las 24 filas que contó el planificador.");
        Assert.AreEqual(1u, ContadorDeCambiosDelArchivo(RutaDeLaBase) - antes, "una hoja, una confirmación.");
    }

    /// <summary>
    /// Control positivo: la misma hoja SIN ámbito cuesta 24 confirmaciones, una por fila.
    /// </summary>
    /// <remarks>
    /// Sin esto, un contador que no contara nada pasaría la prueba de arriba. Y es la cifra
    /// de partida que el pase pedía medir: «hoy 9–24» era del planificador contando líneas;
    /// aquí está contada en el archivo.
    /// </remarks>
    [TestMethod]
    public void LaMismaHojaSinAmbitoSonVeinticuatroConfirmaciones()
    {
        var sinAmbito = new GuardadoDeHojas(
            Datos.Casos, Datos.Personas, Datos.Procedencia, Datos.Ilegibles,
            new RelojDelSistema(), Copias, () => new SinAmbitoDeGuardado());
        var antes = ContadorDeCambiosDelArchivo(RutaDeLaBase);

        sinAmbito.GuardarLasHojasDelDocumento([Hoja("C:/pdfs/grupo.pdf", 1, "SURB2609", SeisPersonas)]);

        Assert.AreEqual(24u, ContadorDeCambiosDelArchivo(RutaDeLaBase) - antes);
    }

    /// <summary>Con una persona son nueve sin ámbito y una con él: el otro extremo del «9–24».</summary>
    [TestMethod]
    public void UnaHojaDeUnaPersonaEsUnaConfirmacionYNueveSinAmbito()
    {
        var antes = ContadorDeCambiosDelArchivo(RutaDeLaBase);
        Guardado.GuardarLasHojasDelDocumento([Hoja("C:/pdfs/uno.pdf", 1, "CASP2609", [SeisPersonas[0]])]);
        Assert.AreEqual(1u, ContadorDeCambiosDelArchivo(RutaDeLaBase) - antes);

        var sinAmbito = new GuardadoDeHojas(
            Datos.Casos, Datos.Personas, Datos.Procedencia, Datos.Ilegibles,
            new RelojDelSistema(), Copias, () => new SinAmbitoDeGuardado());
        antes = ContadorDeCambiosDelArchivo(RutaDeLaBase);
        sinAmbito.GuardarLasHojasDelDocumento([Hoja("C:/pdfs/otro.pdf", 1, "CASP2610", [SeisPersonas[1]])]);
        Assert.AreEqual(9u, ContadorDeCambiosDelArchivo(RutaDeLaBase) - antes);
    }

    /// <summary>
    /// Si la tercera persona revienta, no queda ni el caso ni las dos primeras ni su procedencia.
    /// </summary>
    /// <remarks>
    /// Es el criterio literal de R-4 y el defecto 3 de la deuda de Datos del 2026-09-11
    /// aplicado a la importación. «Revienta» es una excepción que sale del repositorio, no
    /// un rechazo del motor: el rechazo es un aviso y no deshace nada (prueba siguiente).
    /// </remarks>
    [TestMethod]
    public void SiRevientaLaTerceraPersonaNoQuedaNiElCasoNiLasDosPrimeras()
    {
        var personas = new PersonasQueRevientanEnLaTercera(Datos.Personas);
        var guardado = new GuardadoDeHojas(
            Datos.Casos, personas, Datos.Procedencia, Datos.Ilegibles,
            new RelojDelSistema(), Copias, () => new AmbitoDeGuardadoSobreSqlite(Conexion));

        Assert.ThrowsExactly<IOException>(
            () => guardado.GuardarLasHojasDelDocumento([Hoja("C:/pdfs/grupo.pdf", 1, "SURB2609", SeisPersonas)]));

        Assert.AreEqual(2, personas.GuardadasAntesDeReventar, "las dos primeras SÍ se escribieron antes del fallo.");
        Assert.AreEqual(0, Contar("casos"), "y aun así no queda el caso.");
        Assert.AreEqual(0, Contar("personas"), "ni las dos primeras.");
        Assert.AreEqual(0, Contar("procedencia_campo"), "ni la procedencia de ninguno.");
        Assert.AreEqual(0, Contar("documentos_ilegibles"));
    }

    /// <summary>
    /// Control positivo del de arriba: con el ámbito que no abre nada, el fallo en la tercera
    /// deja el caso y las dos primeras a medias, que es lo que pasaba antes de R-4.
    /// </summary>
    [TestMethod]
    public void SinAmbitoElFalloEnLaTerceraDejaElCasoYLasDosPrimerasAMedias()
    {
        var personas = new PersonasQueRevientanEnLaTercera(Datos.Personas);
        var guardado = new GuardadoDeHojas(
            Datos.Casos, personas, Datos.Procedencia, Datos.Ilegibles,
            new RelojDelSistema(), Copias, () => new SinAmbitoDeGuardado());

        Assert.ThrowsExactly<IOException>(
            () => guardado.GuardarLasHojasDelDocumento([Hoja("C:/pdfs/grupo.pdf", 1, "SURB2609", SeisPersonas)]));

        Assert.AreEqual(1, Contar("casos"));
        Assert.AreEqual(2, Contar("personas"));
    }

    /// <summary>
    /// Lo que el motor rechaza fila a fila sigue siendo un aviso: la cédula repetida no entra
    /// y el caso, las demás personas y el renglón sí. «Ninguna hoja se rechaza» no cambia.
    /// </summary>
    [TestMethod]
    public void UnaCedulaRepetidaDentroDeLaHojaNoDeshaceLaHoja()
    {
        (string, string?)[] conRepetida = [SeisPersonas[0], SeisPersonas[1], SeisPersonas[0], SeisPersonas[2]];

        var resultado = Guardado.GuardarLasHojasDelDocumento([Hoja("C:/pdfs/grupo.pdf", 1, "SURB2609", conRepetida)]);

        Assert.IsTrue(resultado[0].Entro);
        Assert.AreEqual(3, resultado[0].Personas, "la repetida no entra; las otras tres sí.");
        Assert.AreEqual(1, Contar("casos"));
        Assert.AreEqual(3, Contar("personas"));
        Assert.IsTrue(resultado[0].Avisos.Any(aviso => aviso.Campo == CamposDeLaHoja.CampoMrn), "y se dice, no se calla.");
    }

    /// <summary>
    /// Después de una hoja que reventó, el guardado sigue vivo: la siguiente entra y se confirma.
    /// </summary>
    /// <remarks>
    /// Un ámbito que se quedara abierto tras el fallo haría que la siguiente hoja fallara con
    /// «ya hay una transacción» o, peor, que entrara dentro del ámbito muerto.
    /// </remarks>
    [TestMethod]
    public void TrasUnaHojaQueReventoLaSiguienteEntraYSeConfirma()
    {
        var personas = new PersonasQueRevientanEnLaTercera(Datos.Personas);
        var guardado = new GuardadoDeHojas(
            Datos.Casos, personas, Datos.Procedencia, Datos.Ilegibles,
            new RelojDelSistema(), Copias, () => new AmbitoDeGuardadoSobreSqlite(Conexion));
        Assert.ThrowsExactly<IOException>(
            () => guardado.GuardarLasHojasDelDocumento([Hoja("C:/pdfs/grupo.pdf", 1, "SURB2609", SeisPersonas)]));

        var antes = ContadorDeCambiosDelArchivo(RutaDeLaBase);
        var resultado = Guardado.GuardarLasHojasDelDocumento([Hoja("C:/pdfs/uno.pdf", 1, "CASP2609", [SeisPersonas[0]])]);

        Assert.IsTrue(resultado[0].Entro);
        Assert.AreEqual(1, Contar("casos"));
        Assert.AreEqual(1u, ContadorDeCambiosDelArchivo(RutaDeLaBase) - antes);
    }

    /// <summary>
    /// Un documento de tres hojas son tres confirmaciones —una por hoja, no una por
    /// documento—: una hoja que reviente no se lleva a las anteriores del mismo PDF.
    /// </summary>
    [TestMethod]
    public void UnDocumentoDeTresHojasSonTresConfirmacionesYLaQueRevientaNoSeLlevaALasAnteriores()
    {
        var antes = ContadorDeCambiosDelArchivo(RutaDeLaBase);
        Guardado.GuardarLasHojasDelDocumento(
        [
            Hoja("C:/pdfs/tres.pdf", 1, "CASP2609", [SeisPersonas[0]]),
            Hoja("C:/pdfs/tres.pdf", 2, "CASP2610", [SeisPersonas[1]]),
            Hoja("C:/pdfs/tres.pdf", 3, "CASP2611", [SeisPersonas[2]]),
        ]);
        Assert.AreEqual(3u, ContadorDeCambiosDelArchivo(RutaDeLaBase) - antes);

        var personas = new PersonasQueRevientanEnLaTercera(Datos.Personas);
        var guardado = new GuardadoDeHojas(
            Datos.Casos, personas, Datos.Procedencia, Datos.Ilegibles,
            new RelojDelSistema(), Copias, () => new AmbitoDeGuardadoSobreSqlite(Conexion));
        Assert.ThrowsExactly<IOException>(() => guardado.GuardarLasHojasDelDocumento(
        [
            Hoja("C:/pdfs/cuatro.pdf", 1, "CASP2612", [SeisPersonas[3]]),
            Hoja("C:/pdfs/cuatro.pdf", 2, "CASP2613", [SeisPersonas[4]]),
            Hoja("C:/pdfs/cuatro.pdf", 3, "CASP2614", SeisPersonas),
        ]));

        Assert.AreEqual(5, Contar("casos"), "los tres del primer PDF y las dos hojas del segundo que se confirmaron antes de la que reventó.");
        Assert.IsNull(Datos.Casos.Listar(FiltroDeCasos.Todo, Pagina.Primera(10)).Elementos.FirstOrDefault(caso => caso.NumeroCaso == "CASP2614"));
    }

    /// <summary>El contador de cambios de la cabecera del archivo: cuántas transacciones lo han cambiado.</summary>
    /// <param name="ruta">El archivo <c>.db</c>, que sigue abierto por la conexión de la prueba.</param>
    private static uint ContadorDeCambiosDelArchivo(string ruta)
    {
        using var flujo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var cabecera = new byte[28];
        flujo.ReadExactly(cabecera);
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(cabecera.AsSpan(24, 4));
    }
}

/// <summary>
/// Un <see cref="IPersonas"/> que deja pasar todo al de verdad y revienta al guardar la
/// tercera persona, como lo haría un disco que falla a mitad de hoja.
/// </summary>
internal sealed class PersonasQueRevientanEnLaTercera : IPersonas
{
    /// <summary>El repositorio real; todo se le pasa tal cual salvo el tercer <see cref="Guardar"/>.</summary>
    private readonly IPersonas _deVerdad;

    /// <summary>Envuelve al repositorio real.</summary>
    /// <param name="deVerdad">El repositorio sobre SQLite.</param>
    public PersonasQueRevientanEnLaTercera(IPersonas deVerdad) => _deVerdad = deVerdad;

    /// <summary>Cuántas personas llegaron a guardarse antes del fallo: dos, si el fallo es en la tercera.</summary>
    public int GuardadasAntesDeReventar { get; private set; }

    /// <inheritdoc />
    public ResultadoDeEscritura Guardar(Persona persona)
    {
        if (GuardadasAntesDeReventar == 2)
        {
            throw new IOException("El disco falló a mitad de la hoja (fallo forzado por la prueba).");
        }

        var resultado = _deVerdad.Guardar(persona);
        GuardadasAntesDeReventar++;
        return resultado;
    }

    /// <inheritdoc />
    public PaginaDe<Persona> Listar(FiltroDePersonas filtro, Pagina trozo) => _deVerdad.Listar(filtro, trozo);

    /// <inheritdoc />
    public int Contar(FiltroDePersonas filtro) => _deVerdad.Contar(filtro);

    /// <inheritdoc />
    public Persona? Obtener(long id) => _deVerdad.Obtener(id);

    /// <inheritdoc />
    public IReadOnlyList<Persona> DeCaso(long casoId) => _deVerdad.DeCaso(casoId);

    /// <inheritdoc />
    public ResultadoDeEscritura AnotarPropuesta(long personaId, Persona propuesta, long companeroId)
        => _deVerdad.AnotarPropuesta(personaId, propuesta, companeroId);

    /// <inheritdoc />
    public ResultadoDeEscritura ResponderLosPasos(long personaId, RespuestaALosPasos respuesta, long companeroId, string origen)
        => _deVerdad.ResponderLosPasos(personaId, respuesta, companeroId, origen);

    /// <inheritdoc />
    public IReadOnlyDictionary<long, FirmaDeLosPasos> FirmasDeLosPasosDelCaso(long casoId) => _deVerdad.FirmasDeLosPasosDelCaso(casoId);

    /// <inheritdoc />
    public ResultadoDeBorrado Borrar(long personaId) => _deVerdad.Borrar(personaId);
}
