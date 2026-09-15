using Fichas.App.Cascara;
using Fichas.App.Importar;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos;
using Fichas.Datos.Repositorios;
using Microsoft.Data.Sqlite;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// Que pasa cuando la base RECHAZA el numero de caso leido.
/// </summary>
/// <remarks>
/// <para>⚠️ Esto no es hipotetico y la medicion esta pegada en la entrega del pase: la
/// base del dueno, en la version 13, lleva
/// <c>CHECK (numero_caso IS NULL OR numero_caso GLOB '[A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9]')</c>,
/// porque su migracion 12 la escribio el Python y ese CHECK lo conservo. La migracion 12
/// del C# lo quita, pero una base que YA paso por la 12 no la vuelve a aplicar: la del
/// dueno llega a la 15 con el CHECK dentro.</para>
///
/// <para>Con ese CHECK, un `CASP26O9` —una letra O por un cero, que es como el OCR lee a
/// veces el numero— NO entra en SQLite, y sin el reintento se perderia el caso ENTERO:
/// sus personas, su fecha y su procedencia. La prueba usa un doble que rechaza igual que
/// ese CHECK, porque el motor de una base recien creada por el C# ya no lo tiene.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelNumeroQueLaBaseRechaza
{
    /// <summary>La carpeta temporal de esta prueba; se borra al recoger.</summary>
    private string _carpeta = string.Empty;
    /// <summary>La conexión abierta sobre la base de la prueba; se cierra al recoger.</summary>
    private SqliteConnection? _conexion;

    /// <summary>Abre una base nueva en una carpeta que nadie mas usa.</summary>
    [TestInitialize]
    public void Preparar()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "fichas-pruebas-numero", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpeta);
        _conexion = ArranqueDeLaBase.PrepararLaBase(_carpeta, _ => { });
    }

    /// <summary>Cierra y borra.</summary>
    [TestCleanup]
    public void Recoger()
    {
        _conexion?.Close();
        _conexion?.Dispose();
        try { Directory.Delete(_carpeta, recursive: true); } catch (IOException) { }
    }

    /// <summary>
    /// Si la base rechaza el numero, el caso entra SIN el y con todo lo demas dentro.
    /// </summary>
    [TestMethod]
    public void SiLaBaseRechazaElNumeroElCasoEntraSinElYNoSePierdeNada()
    {
        var casos = new CasosQueRechazanUnNumeroMalFormado(new RepositorioDeCasos(_conexion!));
        var personas = new RepositorioDePersonas(_conexion!);
        var ilegibles = new RepositorioDeIlegibles(_conexion!);
        var guardado = new GuardadoDeHojas(
            casos, personas, new RepositorioDeProcedencia(_conexion!), ilegibles, new RelojDelSistema(), new CopiaDelEscaneo(_carpeta),
            () => new AmbitoDeGuardadoSobreSqlite(_conexion!));

        var salida = guardado.GuardarLasHojasDelDocumento([HojaConNumero("CASP26O9")]);

        Assert.IsTrue(salida[0].Entro, "el documento NO se pierde por un numero que la base no acepta.");
        Assert.IsTrue(salida[0].PendienteDeIdentificar);
        Assert.IsNull(casos.Obtener(salida[0].CasoId!.Value)!.NumeroCaso);
        Assert.AreEqual(1, salida[0].Personas, "las personas del caso entran igual.");

        var renglones = ilegibles.Listar(FiltroDeIlegibles.Todo, Pagina.Primera(10)).Elementos;
        var elNuestro = renglones.Single(renglon => renglon.Motivo == MotivosDeIlegible.NumeroNoAceptado);
        Assert.Contains("CASP26O9", elNuestro.Detalle!,
            "el valor crudo tiene que quedar escrito, o no hay nada que teclear.");
    }

    /// <summary>Control positivo: un numero bien formado SI se guarda con su numero.</summary>
    /// <remarks>
    /// Sin esta mitad, un guardado que metiera SIEMPRE el numero a nulo pasaria la prueba
    /// de arriba.
    /// </remarks>
    [TestMethod]
    public void UnNumeroBienFormadoSeGuardaConSuNumero()
    {
        var casos = new CasosQueRechazanUnNumeroMalFormado(new RepositorioDeCasos(_conexion!));
        var guardado = new GuardadoDeHojas(
            casos, new RepositorioDePersonas(_conexion!), new RepositorioDeProcedencia(_conexion!),
            new RepositorioDeIlegibles(_conexion!), new RelojDelSistema(), new CopiaDelEscaneo(_carpeta),
            () => new AmbitoDeGuardadoSobreSqlite(_conexion!));

        var salida = guardado.GuardarLasHojasDelDocumento([HojaConNumero("CASP2609")]);

        Assert.AreEqual("CASP2609", casos.Obtener(salida[0].CasoId!.Value)!.NumeroCaso);
        Assert.IsFalse(salida[0].PendienteDeIdentificar);
    }

    /// <summary>Una hoja de mentira con una persona y con el número de caso que se le diga, bien o mal leído.</summary>
    /// <param name="numeroCaso">Lo que «leyó» el OCR en el número de caso; va también como valor crudo.</param>
    private static Fichas.Lectura.HojaLeida HojaConNumero(string numeroCaso) => new(
        RutaPdf: "C:/pdfs/malo.pdf",
        Pagina: 1,
        Campos:
        [
            new(TablaDeProcedencia.Casos, "numero_caso", numeroCaso, OrigenDeCampo.Ocr, 0.71, null, ValorOcr: numeroCaso),
            new(TablaDeProcedencia.Casos, "fecha_viaje", "2026-09-20", OrigenDeCampo.Ocr, 0.9, null),
            new(TablaDeProcedencia.Casos, "unidad_numero", "123456", OrigenDeCampo.Ocr, 0.9, null),
            new(TablaDeProcedencia.Casos, "unidad_nombre", "Barrio", OrigenDeCampo.Ocr, 0.9, null),
            new(TablaDeProcedencia.Personas, "nombre", "Ana", OrigenDeCampo.Ocr, 0.9, null, 1),
            new(TablaDeProcedencia.Personas, "mrn", "055-1111-3853", OrigenDeCampo.Ocr, 0.9, null, 1),
        ],
        Avisos: [],
        Ilegible: null,
        CapturaManual: false,
        LineasLeidas: 40,
        TextoLeido: "texto",
        Segundos: 0.1);

    /// <summary>
    /// Los casos de verdad, pero rechazando el numero igual que lo hace la base del dueno.
    /// </summary>
    /// <remarks>
    /// Es un doble ESTRECHO a proposito: solo cambia una cosa —el <c>CHECK</c> que la base
    /// del dueno tiene y la nueva no— y todo lo demas es el repositorio de verdad
    /// escribiendo en SQLite de verdad. Un doble que contestara a todo dejaria de probar
    /// que las personas y la procedencia entran.
    /// </remarks>
    private sealed class CasosQueRechazanUnNumeroMalFormado : ICasos
    {
        /// <summary>El repositorio de verdad al que se le pasa todo menos el alta de un número mal formado.</summary>
        private readonly ICasos _deVerdad;

        /// <summary>Envuelve al repositorio de verdad.</summary>
        /// <param name="deVerdad">El repositorio de casos sobre SQLite.</param>
        internal CasosQueRechazanUnNumeroMalFormado(ICasos deVerdad) => _deVerdad = deVerdad;

        /// <inheritdoc />
        public PaginaDe<Caso> Listar(FiltroDeCasos filtro, Pagina trozo) => _deVerdad.Listar(filtro, trozo);

        /// <inheritdoc />
        public int Contar(FiltroDeCasos filtro) => _deVerdad.Contar(filtro);

        /// <inheritdoc />
        public Caso? Obtener(long id) => _deVerdad.Obtener(id);

        /// <inheritdoc />
        public IReadOnlyDictionary<long, int> ContarPersonasDe(IReadOnlyList<long> casoIds)
            => _deVerdad.ContarPersonasDe(casoIds);

        /// <summary>Lo único que cambia: un número que el <c>CHECK</c> del esquema viejo no admite se rechaza con el mismo texto que SQLite.</summary>
        /// <param name="caso">El caso que se intenta guardar.</param>
        public ResultadoDeEscritura Guardar(Caso caso)
            => TieneLaFormaQueLaBaseAcepta(caso.NumeroCaso)
                ? _deVerdad.Guardar(caso)
                : ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                    "CHECK constraint failed: numero_caso", "numero_caso"));

        /// <inheritdoc />
        public ResultadoDeEscritura MarcarEstado(long casoId, EstadoDeRecomendacion estado, long companeroId, string origen)
            => _deVerdad.MarcarEstado(casoId, estado, companeroId, origen);

        /// <inheritdoc />
        public ResultadoDeEscritura MarcarEstadoDelCompanero(
            long casoId,
            EstadoDeRecomendacion estado,
            MotivoDeNoCompletar motivo,
            long companeroId,
            string origen)
            => _deVerdad.MarcarEstadoDelCompanero(casoId, estado, motivo, companeroId, origen);

        /// <inheritdoc />
        public ResultadoDeEscritura Archivar(long casoId, bool archivado, string fechaDeArchivado)
            => _deVerdad.Archivar(casoId, archivado, fechaDeArchivado);

        /// <summary>Cuatro letras mayusculas y cuatro digitos, o nulo. El GLOB del esquema.</summary>
        /// <param name="numeroCaso">El número que se quiere guardar; nulo siempre pasa.</param>
        private static bool TieneLaFormaQueLaBaseAcepta(string? numeroCaso)
            => numeroCaso is null
               || (numeroCaso.Length == 8
                   && numeroCaso[..4].All(letra => letra is >= 'A' and <= 'Z')
                   && numeroCaso[4..].All(char.IsAsciiDigit));
    }
}
