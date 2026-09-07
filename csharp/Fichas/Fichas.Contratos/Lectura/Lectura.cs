using Fichas.Contratos.Modelos;

namespace Fichas.Contratos.Lectura;

/// <summary>
/// Un rectangulo sobre la pagina, en FRACCIONES de 0,0 a 1,0, nunca en pixeles.
/// </summary>
/// <remarks>
/// El motivo esta medido en ARQUITECTURA §2.7: un pixel depende de la escala con la que
/// se rasterizo ese dia; una fraccion sigue valiendo a cualquier escala.
/// </remarks>
/// <param name="X0">Borde izquierdo.</param>
/// <param name="Y0">Borde superior.</param>
/// <param name="X1">Borde derecho.</param>
/// <param name="Y1">Borde inferior.</param>
public readonly record struct BandaDeLaPagina(double X0, double Y0, double X1, double Y1)
{
    /// <summary>Lo ancha que es, en fraccion de pagina.</summary>
    public double Ancho => X1 - X0;

    /// <summary>Lo alta que es, en fraccion de pagina.</summary>
    public double Alto => Y1 - Y0;
}

/// <summary>Una pagina del PDF ya convertida en imagen, lista para el OCR o para pintarla.</summary>
/// <param name="Pagina">Que hoja del PDF es, base 1.</param>
/// <param name="Ancho">Ancho en pixeles de esta rasterizacion.</param>
/// <param name="Alto">Alto en pixeles de esta rasterizacion.</param>
/// <param name="Png">La imagen codificada en PNG; el formato se fija aqui para no discutirlo por proyecto.</param>
public sealed record ImagenDePagina(int Pagina, int Ancho, int Alto, byte[] Png);

/// <summary>Una linea que devolvio el OCR, con lo que leyo, cuanto se fia y donde estaba.</summary>
/// <param name="Texto">Lo que leyo, tal cual, sin arreglar (regla permanente 1).</param>
/// <param name="Confianza">De 0,0 a 1,0; nula si el motor no la da.</param>
/// <param name="Banda">Donde estaba en la pagina, en fracciones.</param>
public sealed record LineaDeOcr(string Texto, double? Confianza, BandaDeLaPagina Banda);

/// <summary>Una anotacion del propio PDF: lo que alguien escribio o dibujo encima del papel.</summary>
/// <param name="Subtipo">Que clase de anotacion es segun el PDF: <c>FreeText</c>, <c>Ink</c>, etc.</param>
/// <param name="Texto">Su texto, cuando lo tiene.</param>
/// <param name="Banda">Donde esta en la pagina, en fracciones.</param>
/// <param name="Rojo">Componente roja del color del trazo, 0,0 a 1,0; nula si no se sabe.</param>
/// <param name="Verde">Componente verde del color del trazo, 0,0 a 1,0; nula si no se sabe.</param>
/// <param name="Azul">Componente azul del color del trazo, 0,0 a 1,0; nula si no se sabe.</param>
/// <param name="Grosor">Grosor del trazo; nulo si no se sabe. Es lo que separa un tachon de un subrayado.</param>
public sealed record AnotacionDelPdf(
    string Subtipo,
    string? Texto,
    BandaDeLaPagina Banda,
    double? Rojo,
    double? Verde,
    double? Azul,
    double? Grosor);

/// <summary>Un campo que la extraccion propone, con su confianza y su banda.</summary>
/// <remarks>Propone; no firma. Nada se marca verificado aqui (regla permanente 5).</remarks>
/// <param name="Tabla">A que tabla ira: casos o personas.</param>
/// <param name="Campo">Nombre de la columna que se propone llenar.</param>
/// <param name="Valor">Lo propuesto, tal como se leyo; nulo si no se leyo nada.</param>
/// <param name="Origen">De donde salio: anotacion, ocr o vacio.</param>
/// <param name="Confianza">De 0,0 a 1,0; nula si no aplica.</param>
/// <param name="Banda">Donde estaba en la pagina, para poder iluminarlo; nula si no se sabe.</param>
/// <param name="FilaFormulario">Si es de una persona, en que fila del formulario venia.</param>
public sealed record CampoPropuesto(
    TablaDeProcedencia Tabla,
    string Campo,
    string? Valor,
    OrigenDeCampo Origen,
    double? Confianza,
    BandaDeLaPagina? Banda,
    int? FilaFormulario = null,
    // Las tres de abajo las pidio el terreno de Lectura el 2026-09-04 y las anade el
    // supervisor, que es el titular de lo congelado. Sin ellas lo leido viajaba dentro
    // de `Valor` con un aviso al lado, y el guardado no tenia como llenar las columnas
    // `valor_ocr`, `anulado_por_tachon` y la marca de revision que la base SI tiene.
    // Van al final y con valor por defecto: quien ya construia un CampoPropuesto sigue
    // compilando igual.

    /// <summary>Lo que se leyo TAL CUAL, aunque `Valor` no lo haya podido aceptar.</summary>
    string? ValorOcr = null,

    /// <summary>El papel lo tachó: el valor no vale y nadie debe resucitarlo solo.</summary>
    bool AnuladoPorTachon = false,

    /// <summary>No encaja con lo esperado: se guarda igual y se señala junto al campo.</summary>
    bool NecesitaRevision = false);

/// <summary>Lo que devuelve una extraccion: lo propuesto y lo que hay que decir en la franja.</summary>
/// <param name="Campos">Los campos propuestos.</param>
/// <param name="Avisos">Lo que hay que senalar; una extraccion rara avisa, no se detiene.</param>
public sealed record ResultadoDeExtraccion(
    IReadOnlyList<CampoPropuesto> Campos,
    IReadOnlyList<Aviso> Avisos)
{
    /// <summary>Una extraccion que no propuso nada, con su motivo en una linea.</summary>
    public static ResultadoDeExtraccion Nada(params Aviso[] avisos)
        => new(Array.Empty<CampoPropuesto>(), avisos);
}

/// <summary>Lo que trae de vuelta el Excel de un companero sobre UNA persona.</summary>
/// <param name="NumeroCaso">El numero de caso tal como venia escrito en la hoja.</param>
/// <param name="Mrn">El MRN tal como venia escrito; es la mitad de la clave de reconciliacion.</param>
/// <param name="Nombre">El nombre tal como venia escrito.</param>
/// <param name="EstadoDeLaRecomendacion">Lo que marco el companero sobre el caso.</param>
/// <param name="EstadoPropuesto">Lo que propuso sobre esta persona; texto libre.</param>
/// <param name="NotaCompanero">Su nota libre.</param>
/// <param name="PasoPreparacion">Paso 1, tal como lo devolvio.</param>
/// <param name="PasoInformacion">Paso 2, tal como lo devolvio.</param>
/// <param name="PasoCitaDelTemplo">Paso 3, tal como lo devolvio.</param>
/// <param name="PasoAccionesRequeridas">Paso 4, tal como lo devolvio.</param>
/// <param name="PasoEntrevistas">Paso 5, tal como lo devolvio.</param>
/// <param name="PasoListoParaElTemplo">Paso 6, tal como lo devolvio.</param>
/// <param name="LlamoAlLider">Si llamo al lider; no es un septimo paso.</param>
/// <param name="FilaExcel">De que fila del Excel salio, base 1, para poder ir a mirarla.</param>
public sealed record MarcaDelCompanero(
    string? NumeroCaso,
    string? Mrn,
    string? Nombre,
    EstadoDeRecomendacion EstadoDeLaRecomendacion,
    string? EstadoPropuesto,
    string? NotaCompanero,
    bool? PasoPreparacion,
    bool? PasoInformacion,
    bool? PasoCitaDelTemplo,
    bool? PasoAccionesRequeridas,
    bool? PasoEntrevistas,
    bool? PasoListoParaElTemplo,
    bool? LlamoAlLider,
    int? FilaExcel,
    // Lo pidio el terreno de Paquetes el 2026-09-04 y lo anade el supervisor, que es
    // el titular de lo congelado. Sin este campo, aplicar las marcas tenia que
    // resolver otra vez por el par numero_caso + mrn, y ese par NO identifica: el
    // numero de caso son cuatro letras y el ano y el mes, y el dueno tiene siete
    // documentos que comparten CASP2609. Al leer la hoja el id SI se conoce, porque
    // la clave del Excel es CASO:MRN:ID; perderlo por el camino y volver a
    // adivinarlo era tirar el trabajo del companero con un motivo de ambiguo.
    // Va al final y con valor por defecto: quien ya construia una marca sigue
    // compilando, y una hoja vieja sin id sigue llegando con este campo nulo.

    /// <summary>El id del caso que traia la clave del Excel, o nulo si la hoja es vieja.</summary>
    long? CasoId = null,

    // Lo pidio el terreno de Paquetes el 2026-09-05 y lo anade el supervisor, que es el
    // titular de lo congelado. Sin este campo, `AplicarMarcas` escribia siempre
    // `MotivoDeNoCompletar.SinMotivo` en `casos.motivo_del_companero` —y esa columna
    // llevaba desde la migracion 18 nula en toda la base—, porque la marca no tenia por
    // donde traer lo que el companero habia elegido en su hoja. Es la mitad que faltaba de
    // la frase del dueno: «en el calendario tambien puede decir el estado: no completado,
    // no se pudo comunicar con el lider, o el lider no lo hizo».
    // Va al final y con valor por defecto: quien ya construia una marca sigue compilando, y
    // una hoja sin la columna del motivo llega con este campo en SinMotivo.
    //
    // ⚠️ Es lo que dice EL COMPANERO, y va a `casos.motivo_del_companero`. El motivo
    // vigente —`casos.motivo_no_completa`— es de Miguel y esta hoja no lo escribe nunca.

    /// <summary>Por que dijo el companero que no se completo; sin motivo si no dijo nada.</summary>
    MotivoDeNoCompletar Motivo = MotivoDeNoCompletar.SinMotivo);

/// <summary>Lo que devuelve leer el Excel de vuelta: lo que casa, lo que no, y lo que hay que decir.</summary>
/// <param name="Marcas">Las filas que se pudieron leer.</param>
/// <param name="Descartadas">Las que no casaron con nadie, listas para su tabla.</param>
/// <param name="Avisos">Lo que hay que senalar en la franja.</param>
public sealed record ResultadoDelExcelDevuelto(
    IReadOnlyList<MarcaDelCompanero> Marcas,
    IReadOnlyList<FilaDescartada> Descartadas,
    IReadOnlyList<Aviso> Avisos);
