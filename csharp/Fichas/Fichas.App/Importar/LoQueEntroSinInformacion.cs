using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Importar;

/// <summary>
/// Lo que entro en la tanda sin informacion de ninguna persona, separado en sus dos clases.
/// </summary>
/// <remarks>
/// <para>Del dueno, el 2026-09-07 y dicho dos veces el mismo dia: <i>«no se contempla
/// eliminar PDF o documentos que no tienen informacion; debe poder eliminarlo»</i> y
/// <i>«este documento no tiene informacion de ninguna persona, deberia permitirme
/// eliminarlo»</i>.</para>
///
/// <para>⚠️ <b>«Documento sin informacion» son DOS cosas y no una</b>, y hubo que medirlo
/// antes de escribir nada porque el arreglo de una no es el de la otra:</para>
/// <list type="number">
/// <item><b>Un caso del que no se leyo ninguna persona.</b> Existe en <c>casos</c>, asi que
/// <see cref="IMantenimiento.PlanearDocumentos"/> ya lo alcanza. Lo que faltaba no era el
/// borrado: era poder verlo y borrarlo desde donde se esta mirando.</item>
/// <item><b>Un PDF que no se pudo leer en absoluto.</b> Deja un renglon en
/// <c>documentos_ilegibles</c> con <c>caso_id</c> NULO, no tiene caso al que agarrarse, y
/// hoy ninguna pantalla del programa lo ensena: medido el 2026-09-09, <c>IIlegibles.Listar</c>
/// e <c>IIlegibles.Contar</c> no tienen ni un consumidor fuera de las pruebas, y el texto del
/// resumen de la tanda remite a una pantalla —«Lo que no entro»— que para los ilegibles no
/// existe.</item>
/// </list>
///
/// <para>⛔ El renglon de ilegible que SI tiene caso no se ofrece: es informacion sobre un
/// documento que existe, y se va con el cuando el documento se borre —
/// <c>documentos_ilegibles</c> es uno de los pasos de <c>RepositorioDeMantenimiento</c>—.
/// Ofrecerlo suelto dejaria un caso sin el motivo por el que esta a medias.</para>
///
/// <para>Esta clase NO borra y no sabe que existe una ventana: solo dice que hay. Quien
/// borra es el mismo camino de siempre —copia, cifra delante, cancelable—.</para>
/// </remarks>
public static class LoQueEntroSinInformacion
{
    /// <summary>
    /// Lo que entro sin informacion en ESTA tanda, y solo en esta.
    /// </summary>
    /// <remarks>
    /// Se acota a la tanda a proposito: la pantalla de importar habla de lo que se acaba de
    /// importar, y ofrecer ahi para borrar un documento viejo que el dueno no tiene delante
    /// seria ofrecerle borrar a ciegas.
    /// </remarks>
    /// <param name="casos">Por donde se cuentan las personas de cada caso.</param>
    /// <param name="ilegibles">Por donde se leen los renglones de lo que no se pudo leer.</param>
    /// <param name="casoIds">Los casos que nacieron en la tanda.</param>
    /// <param name="rutasDeLaTanda">Los PDF que se intentaron leer en la tanda.</param>
    public static LoQueNoTraeAnadie Ver(
        ICasos casos,
        IIlegibles ilegibles,
        IReadOnlyList<long> casoIds,
        IReadOnlyList<string> rutasDeLaTanda)
    {
        ArgumentNullException.ThrowIfNull(casos);
        ArgumentNullException.ThrowIfNull(ilegibles);
        ArgumentNullException.ThrowIfNull(casoIds);
        ArgumentNullException.ThrowIfNull(rutasDeLaTanda);

        return new LoQueNoTraeAnadie
        {
            CasosSinPersonas = CasosDeLosQueNoSeLeyoNadie(casos, casoIds),
            RenglonesSinCaso = RenglonesHuerfanosDe(ilegibles, rutasDeLaTanda),
        };
    }

    /// <summary>Los casos de la tanda que no tienen ni una persona colgando.</summary>
    /// <remarks>
    /// Las personas se cuentan de una pasada con <see cref="ICasos.ContarPersonasDe"/> y no
    /// caso a caso: una tanda de 500 documentos serian 500 consultas, y esta cuenta se hace
    /// justo cuando la ventana acaba de estar media hora ocupada.
    /// </remarks>
    private static IReadOnlyList<DocumentoSinNadie> CasosDeLosQueNoSeLeyoNadie(
        ICasos casos, IReadOnlyList<long> casoIds)
    {
        if (casoIds.Count == 0) return [];

        var ids = casoIds.Distinct().ToList();
        var cuantas = casos.ContarPersonasDe(ids);

        var vacios = new List<DocumentoSinNadie>();
        foreach (var casoId in ids)
        {
            if (cuantas.TryGetValue(casoId, out var personas) && personas > 0) continue;

            var caso = casos.Obtener(casoId);
            if (caso is null) continue;

            vacios.Add(new DocumentoSinNadie
            {
                CasoId = caso.Id,
                NumeroDeCaso = caso.NumeroCaso ?? string.Empty,
                Archivo = NombreDelArchivo(caso.RutaPdf),
                RutaPdf = caso.RutaPdf ?? string.Empty,
                PaginaPdf = caso.PaginaPdf,
                Personas = 0,
            });
        }

        return vacios;
    }

    /// <summary>Los renglones de la tanda que no apuntan a ningun caso.</summary>
    /// <remarks>
    /// Se pregunta ruta a ruta con el filtro que el puerto ya tiene, en vez de traerse la
    /// tabla entera y filtrarla aqui: la tabla guarda TODO lo que no se ha podido leer desde
    /// que el programa existe, y de eso solo interesa lo de esta tanda.
    /// </remarks>
    private static IReadOnlyList<PdfSinDocumento> RenglonesHuerfanosDe(
        IIlegibles ilegibles, IReadOnlyList<string> rutasDeLaTanda)
    {
        var huerfanos = new List<RenglonIlegible>();
        foreach (var ruta in rutasDeLaTanda.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var pagina = ilegibles.Listar(new FiltroDeIlegibles(RutaPdf: ruta), Pagina.Primera(int.MaxValue));
            huerfanos.AddRange(pagina.Elementos.Where(renglon => renglon.CasoId is null));
        }

        return [.. huerfanos.OrderBy(renglon => renglon.RutaPdf, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(renglon => renglon.Id)
                            .Select(PdfSinDocumento.De)];
    }

    /// <summary>El nombre del archivo sin su ruta; lo que se lee en un renglon.</summary>
    private static string NombreDelArchivo(string? rutaPdf)
    {
        if (string.IsNullOrWhiteSpace(rutaPdf)) return string.Empty;

        try
        {
            return Path.GetFileName(rutaPdf);
        }
        catch (ArgumentException)
        {
            // Una ruta que Windows no admite no puede dejar la lista en blanco: el renglon
            // existe para que el dueno pueda ir a mirar, y algo escrito es mejor que nada.
            return rutaPdf;
        }
    }
}

/// <summary>Las dos listas de lo que entro sin informacion de ninguna persona.</summary>
public sealed record LoQueNoTraeAnadie
{
    /// <summary>Los documentos que existen y de los que no se leyó ni una persona.</summary>
    public IReadOnlyList<DocumentoSinNadie> CasosSinPersonas { get; init; } = [];

    /// <summary>Los PDF que no se pudieron leer y no dejaron ningún documento detrás.</summary>
    public IReadOnlyList<PdfSinDocumento> RenglonesSinCaso { get; init; } = [];

    /// <summary>Cuántas cosas hay en total entre las dos listas.</summary>
    public int Cuantos => CasosSinPersonas.Count + RenglonesSinCaso.Count;

    /// <summary>Si hay algo que enseñar; con esto se decide si la zona aparece siquiera.</summary>
    public bool HayAlgo => Cuantos > 0;

    /// <summary>
    /// Las dos cifras por separado, nunca sumadas.
    /// </summary>
    /// <remarks>
    /// Sumarlas escondería justo la diferencia que importa: de un lado hay documentos que se
    /// pueden abrir y mirar, y del otro archivos de los que no quedó nada. Son dos gestos
    /// distintos y una sola cifra no dejaría elegir.
    /// </remarks>
    public string Linea => Cuantos == 0
        ? "Todo lo que entró trae información de alguna persona."
        : string.Join(" · ",
            Plural.Con(
                CasosSinPersonas.Count,
                "documento del que no se leyó ninguna persona",
                "documentos de los que no se leyó ninguna persona"),
            Plural.Con(
                RenglonesSinCaso.Count,
                "PDF que no se pudieron leer y no dejaron documento",
                "PDF que no se pudieron leer y no dejaron documento"));
}

/// <summary>Un PDF que no se pudo leer y que no dejó ningún documento en la base.</summary>
/// <remarks>
/// <para>Es el hermano de <see cref="DocumentoSinNadie"/>, y existe por lo mismo: la
/// pantalla necesita un renglón que se pueda leer y marcar, no el modelo crudo de la base.
/// Lleva la ruta ENTERA porque es lo que hace falta para ir a abrir el papel y mirarlo
/// antes de decidir; borrar sin poder comprobar es borrar a ciegas.</para>
///
/// <para>⚠️ <b>Lo que se borra de esto es el renglón, no el archivo.</b> El PDF es del
/// dueño y está en su carpeta: este programa lee de ahí y no es quien para tirar sus
/// escaneos. Quien lo enseñe tiene que decirlo —lo dice
/// <see cref="TextosDeBorrarLosPdfIlegibles.LoQueNoSeBorra"/>—, o el dueño creerá que
/// borró un archivo que sigue ahí.</para>
/// </remarks>
public sealed record PdfSinDocumento
{
    /// <summary>El número interno del renglón, que es lo que se le pasa al borrado.</summary>
    public long RenglonId { get; init; }

    /// <summary>La ruta entera del PDF, para poder ir a abrirlo.</summary>
    public string RutaPdf { get; init; } = string.Empty;

    /// <summary>El nombre del archivo sin su ruta.</summary>
    public string Archivo { get; init; } = string.Empty;

    /// <summary>La hoja del PDF, base 1, o nula si el fallo fue del archivo entero.</summary>
    public int? PaginaPdf { get; init; }

    /// <summary>El código con el que se archivó el motivo; se guarda para poder contarlo.</summary>
    public string Motivo { get; init; } = string.Empty;

    /// <summary>Ese mismo motivo dicho en español, que es lo que se lee en pantalla.</summary>
    public string MotivoEnEspanol => MotivosDeIlegible.EnEspanol(Motivo);

    /// <summary>Lo que se lee en su renglón: la ruta entera, su hoja y por qué falló.</summary>
    public string Etiqueta
    {
        get
        {
            var hoja = PaginaPdf is int pagina ? $", hoja {pagina}" : string.Empty;
            return $"{RutaPdf}{hoja} — {MotivoEnEspanol}";
        }
    }

    /// <summary>El renglón de la base, convertido en lo que la pantalla enseña.</summary>
    public static PdfSinDocumento De(RenglonIlegible renglon)
    {
        ArgumentNullException.ThrowIfNull(renglon);

        return new PdfSinDocumento
        {
            RenglonId = renglon.Id,
            RutaPdf = renglon.RutaPdf,
            Archivo = NombreDelArchivoDe(renglon.RutaPdf),
            PaginaPdf = renglon.PaginaPdf,
            Motivo = renglon.Motivo,
        };
    }

    /// <summary>El nombre sin la ruta; si Windows no admite la ruta, la ruta entera.</summary>
    private static string NombreDelArchivoDe(string? rutaPdf)
    {
        if (string.IsNullOrWhiteSpace(rutaPdf)) return string.Empty;

        try
        {
            return Path.GetFileName(rutaPdf);
        }
        catch (ArgumentException)
        {
            return rutaPdf;
        }
    }
}

/// <summary>Un documento que existe en la base y del que no se leyó ninguna persona.</summary>
/// <remarks>
/// Lleva la ruta y la hoja además del nombre del archivo porque son lo que hace falta para
/// ir a mirar el papel antes de borrarlo: borrar sin poder comprobar es borrar a ciegas.
/// </remarks>
public sealed record DocumentoSinNadie
{
    /// <summary>El número interno del caso, que es lo que se le pasa al borrado.</summary>
    public long CasoId { get; init; }

    /// <summary>El número del papel, o vacío si no se pudo leer.</summary>
    public string NumeroDeCaso { get; init; } = string.Empty;

    /// <summary>El nombre del archivo del que salió, sin la ruta.</summary>
    public string Archivo { get; init; } = string.Empty;

    /// <summary>La ruta entera, para poder ir a abrirlo.</summary>
    public string RutaPdf { get; init; } = string.Empty;

    /// <summary>La hoja del PDF que lo abrió, base 1, o nula.</summary>
    public int? PaginaPdf { get; init; }

    /// <summary>Cuántas personas tiene; es cero, y se guarda para que la lista lo diga.</summary>
    public int Personas { get; init; }

    /// <summary>Lo que se lee en su renglón: el archivo, su hoja y su número.</summary>
    public string Etiqueta
    {
        get
        {
            var hoja = PaginaPdf is int pagina ? $", hoja {pagina}" : string.Empty;
            var numero = NumeroDeCaso.Length == 0 ? "sin número de caso" : NumeroDeCaso;
            return $"{Archivo}{hoja} · {numero} · ninguna persona";
        }
    }
}
