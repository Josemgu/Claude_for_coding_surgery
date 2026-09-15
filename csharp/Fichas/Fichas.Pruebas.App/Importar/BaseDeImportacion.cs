using Fichas.App.Cascara;
using Fichas.App.Importar;
using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Datos;
using Fichas.Datos.Repositorios;
using Fichas.Lectura;
using Microsoft.Data.Sqlite;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// Una base de verdad en una carpeta temporal y las hojas de mentira para llenarla.
/// </summary>
/// <remarks>
/// Las pruebas de la importacion corren contra SQLite REAL y no contra un doble: lo que
/// hay que comprobar es que una hoja repetida marca duplicado y que un numero que el
/// esquema no acepta no tira el documento, y las dos cosas las decide el motor. Un doble
/// en memoria contestaria lo que yo le diga y la prueba pasaria estando mal.
///
/// <para>El OCR SI es de mentira, y por eso existe <see cref="Hoja"/>: leer siete PDF de
/// verdad tarda un minuto por prueba y lo que se prueba aqui no es el OCR —eso lo miden
/// las 41 pruebas de <c>Fichas.Pruebas.Lectura</c>— sino que hacer con lo que devuelva.</para>
/// </remarks>
public abstract class BaseDeImportacion
{
    /// <summary>La carpeta temporal de esta prueba; se borra al recoger.</summary>
    private string _carpeta = string.Empty;
    /// <summary>La conexión abierta sobre la base de la prueba; se cierra al recoger.</summary>
    private SqliteConnection? _conexion;

    /// <summary>La carpeta temporal donde vive la base de esta prueba.</summary>
    protected string Carpeta => _carpeta;

    /// <summary>Los repositorios reales montados sobre esa base.</summary>
    protected RepositoriosDePrueba Datos { get; private set; } = null!;

    /// <summary>El guardado que se esta probando.</summary>
    protected GuardadoDeHojas Guardado { get; private set; } = null!;

    /// <summary>
    /// Donde el guardado deja su copia de cada escaneo: dentro de la carpeta de datos de la
    /// prueba, como en el programa dentro de la del dueño.
    /// </summary>
    /// <remarks>
    /// Entró el 2026-09-15 con el defecto del papel pegado: desde entonces un caso no guarda
    /// la ruta del escáner sino la de su copia, y las pruebas necesitan saber dónde mirar.
    /// </remarks>
    protected CopiaDelEscaneo Copias { get; private set; } = null!;

    /// <summary>Abre una base nueva, migrada al dia, en una carpeta que nadie mas usa.</summary>
    [TestInitialize]
    public void Preparar()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "fichas-pruebas-importar", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpeta);
        _conexion = ArranqueDeLaBase.PrepararLaBase(_carpeta, _ => { });

        Datos = new RepositoriosDePrueba(
            new RepositorioDeCasos(_conexion),
            new RepositorioDePersonas(_conexion),
            new RepositorioDeProcedencia(_conexion),
            new RepositorioDeIlegibles(_conexion));

        Copias = new CopiaDelEscaneo(Path.Combine(_carpeta, "datos"));
        Guardado = new GuardadoDeHojas(
            Datos.Casos, Datos.Personas, Datos.Procedencia, Datos.Ilegibles,
            new RelojDelSistema(), Copias);
    }

    /// <summary>Cierra la base y borra la carpeta; una prueba no deja rastro.</summary>
    [TestCleanup]
    public void Recoger()
    {
        _conexion?.Close();
        _conexion?.Dispose();
        try { Directory.Delete(_carpeta, recursive: true); } catch (IOException) { }
    }

    /// <summary>La conexion cruda, para preguntarle al motor y no al programa.</summary>
    protected SqliteConnection Conexion => _conexion!;

    /// <summary>Cuenta filas de una tabla preguntandole al motor.</summary>
    /// <param name="tabla">Una de las cuatro tablas de la importación, escrita como literal en la prueba.</param>
    protected long Contar(string tabla)
    {
        using var orden = _conexion!.CreateCommand();
        // La tabla NUNCA viene de fuera: son literales escritos en las pruebas.
        orden.CommandText = tabla switch
        {
            "casos" => "SELECT COUNT(*) FROM casos",
            "personas" => "SELECT COUNT(*) FROM personas",
            "procedencia_campo" => "SELECT COUNT(*) FROM procedencia_campo",
            "documentos_ilegibles" => "SELECT COUNT(*) FROM documentos_ilegibles",
            _ => throw new ArgumentException($"Tabla no prevista en las pruebas: {tabla}", nameof(tabla)),
        };
        return Convert.ToInt64(orden.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Una hoja leida de mentira, con los campos que se le digan.</summary>
    /// <param name="ruta">El archivo del que sale.</param>
    /// <param name="pagina">Que hoja es, base 1.</param>
    /// <param name="numeroCaso">Lo leido en el numero de caso; nulo para «no se leyo».</param>
    /// <param name="personas">Los pares nombre/MRN de las personas de la hoja.</param>
    /// <param name="fechaViaje">Lo leido en la fecha de viaje.</param>
    /// <param name="unidadNumero">Lo leido en el numero de unidad.</param>
    /// <param name="temploNombre">Lo leido en el nombre del templo.</param>
    /// <param name="unidadNombre">
    /// Lo leido en el nombre de la unidad. Es parametro desde el 2026-09-07 porque sin el no
    /// se podia escribir la hoja que MAS se parece a las de verdad: en los dos formularios de
    /// grupo del dueno, <c>unidad_nombre</c> sale vacio en las SEIS hojas —medido sobre los
    /// diez PDF reales—, y una hoja de mentira que siempre lo trae lleno no puede ver que
    /// pasa cuando una hoja lo lee y otra no.
    /// </param>
    /// <remarks>
    /// ⚠️ <b>La hoja trae los CINCO campos del caso, no cuatro.</b> Hasta el 2026-09-05 esta
    /// hoja de mentira no traia <c>templo_nombre</c>, asi que ninguna prueba podia ver que la
    /// importacion no le dejaba fila de procedencia. Medido con la ventana abierta sobre los
    /// siete escaneos del dueno: <c>templo_nombre</c> con fila en <b>0 de 7</b> casos, y los
    /// otros cuatro en 7 de 7. <see cref="Fichas.Lectura.Extraccion"/> SIEMPRE propone los
    /// cinco —cuando no lee nada, con el valor en nulo—, asi que una hoja de mentira que
    /// traiga cuatro no se parece a ninguna hoja de verdad.
    /// </remarks>
    protected static HojaLeida Hoja(
        string ruta,
        int pagina,
        string? numeroCaso,
        (string Nombre, string? Mrn)[] personas,
        string? fechaViaje = "2026-09-20",
        string? unidadNumero = "123456",
        string? temploNombre = "Panama City, Panama",
        string? unidadNombre = "Barrio de prueba")
    {
        var campos = new List<CampoPropuesto>
        {
            Campo(TablaDeProcedencia.Casos, "numero_caso", numeroCaso),
            Campo(TablaDeProcedencia.Casos, "fecha_viaje", fechaViaje),
            Campo(TablaDeProcedencia.Casos, "unidad_numero", unidadNumero),
            Campo(TablaDeProcedencia.Casos, "unidad_nombre", unidadNombre),
            Campo(TablaDeProcedencia.Casos, "templo_nombre", temploNombre),
        };

        for (var fila = 1; fila <= personas.Length; fila++)
        {
            campos.Add(Campo(TablaDeProcedencia.Personas, "nombre", personas[fila - 1].Nombre, fila));
            campos.Add(Campo(TablaDeProcedencia.Personas, "mrn", personas[fila - 1].Mrn, fila));
        }

        return new HojaLeida(
            RutaPdf: ruta,
            Pagina: pagina,
            Campos: campos,
            Avisos: [],
            Ilegible: null,
            CapturaManual: false,
            LineasLeidas: 40,
            TextoLeido: "texto de prueba",
            Segundos: 0.1);
    }

    /// <summary>Una hoja que no se pudo leer, con su motivo.</summary>
    /// <param name="ruta">El PDF del que sería.</param>
    /// <param name="motivo">El motivo, que va también como aviso.</param>
    protected static HojaLeida HojaIlegible(string ruta, string motivo) => new(
        RutaPdf: ruta,
        Pagina: 0,
        Campos: [],
        Avisos: [Aviso.Problema(motivo)],
        Ilegible: new RenglonIlegible
        {
            RutaPdf = ruta,
            PaginaPdf = null,
            Motivo = motivo,
            LineasLeidas = 0,
            RegistradoEn = "2026-09-04T10:00:00",
        },
        CapturaManual: true,
        LineasLeidas: 0,
        TextoLeido: null,
        Segundos: 0.0);

    /// <summary>Los cuatro repositorios de esta prueba, juntos para no pasarlos de uno en uno.</summary>
    /// <param name="Casos">Los casos.</param>
    /// <param name="Personas">Las personas.</param>
    /// <param name="Procedencia">La procedencia de cada campo.</param>
    /// <param name="Ilegibles">Los renglones de lo que no entro.</param>
    protected sealed record RepositoriosDePrueba(
        Contratos.Puertos.ICasos Casos,
        Contratos.Puertos.IPersonas Personas,
        Contratos.Puertos.IProcedencia Procedencia,
        Contratos.Puertos.IIlegibles Ilegibles);

    /// <summary>Un campo propuesto de mentira, con banda fija y confianza 0,92; vacío si el valor es nulo.</summary>
    /// <param name="tabla">Si es del caso o de una persona.</param>
    /// <param name="nombre">El nombre de la columna.</param>
    /// <param name="valor">Lo leído; nulo es «el papel no lo traía».</param>
    /// <param name="fila">El renglón del papel para una persona; nulo para el caso.</param>
    private static CampoPropuesto Campo(
        TablaDeProcedencia tabla, string nombre, string? valor, int? fila = null)
        => new(
            tabla,
            nombre,
            valor,
            valor is null ? OrigenDeCampo.Vacio : OrigenDeCampo.Ocr,
            valor is null ? null : 0.92,
            new BandaDeLaPagina(0.1, 0.2, 0.5, 0.24),
            fila,
            ValorOcr: valor);
}
