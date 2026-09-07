using Fichas.Contratos.Modelos;

namespace Fichas.Paquetes;

/// <summary>
/// Los motivos por los que una fila NO entra, escritos aqui y no repartidos por el codigo.
/// </summary>
/// <remarks>
/// Son lo que Miguel lee para decidir, y una lista de motivos redactados cada uno en un
/// sitio distinto acaba diciendo lo mismo de cuatro maneras. Los textos se conservan tal
/// cual del programa en Python (<c>paquete/reconciliacion.py</c>): quien ya los conoce los
/// tiene que reconocer.
/// </remarks>
public static class Motivos
{
    /// <summary>La fila no trae ni caso ni MRN, y sin los dos no se sabe de quien habla.</summary>
    public const string SinClave =
        "la fila no trae el número de caso o no trae el MRN, y sin los dos no se puede "
        + "saber a qué persona se refiere. NUNCA se empareja por nombre";

    /// <summary>La celda de la clave vino vacia: o se la borraron, o la fila se anadio a mano.</summary>
    public const string ClaveBorrada =
        "este renglón no trae nada en la columna «clave», así que no se sabe de quién "
        + "es. O se la borraron, o es una fila añadida a mano. La clave va a la vista "
        + "precisamente para que se note cuando falta, y sin ella la fila NO se busca "
        + "por nombre: nunca se empareja por nombre";

    /// <summary>La clave no tiene la forma esperada. Lleva dentro la clave que venia escrita.</summary>
    public static string ClaveRota(string clave)
        => $"la clave «{clave}» no tiene la forma esperada (caso, dos puntos y cédula de "
           + "miembro), así que no se puede saber a qué persona se refiere";

    /// <summary>Una respuesta que nadie puede interpretar sin inventar. No se aplica media fila.</summary>
    public static string RespuestaIlegible(string rotulo, string detalle)
        => $"en la columna «{rotulo}», {detalle}. Esa persona se quedó sin aplicar entera: "
           + "no se aplica media fila";

    /// <summary>El par no existe en la base: persona anadida a mano, o MRN retecleado.</summary>
    public const string SinPar =
        "el par número de caso + MRN no existe en la base. La fila NO se inserta: "
        + "puede ser una persona añadida a mano en el Excel, o un MRN retecleado";

    /// <summary>El mismo par ya venia en una fila anterior de este mismo archivo.</summary>
    public static string Repetida(int primera) => $"ese mismo par ya venía en la fila {primera} de este archivo";

    /// <summary>
    /// El motivo que nacio con la version 12 del esquema: la clave no lleva el id del caso
    /// —un paquete generado antes del 2026-09-03, o un Excel armado a mano— y el numero de
    /// caso lleva a DOS personas de dos casos distintos.
    /// </summary>
    /// <remarks>
    /// No se escoge una: escribir el trabajo del companero sobre la familia equivocada es
    /// peor que no escribirlo, y esto al menos deja su renglon.
    /// </remarks>
    public static string ClaveAmbigua(int cuantas)
        => $"ese número de caso y ese MRN llevan a {cuantas} personas de {cuantas} casos "
           + "distintos, así que no se sabe a cuál se refiere. El número de caso son cuatro "
           + "letras y el año y el mes: identifica una unidad y un mes, no una familia, y "
           + "dos documentos lo comparten. NO se escoge uno al azar. Vuelva a generar el "
           + "paquete: las claves nuevas llevan dentro el número de caso, el MRN y el id del "
           + "caso, y esa sí distingue";
}

/// <summary>Una fila del Excel que SI resolvio a una persona concreta de la base.</summary>
/// <param name="FilaExcel">De que fila salio, base 1.</param>
/// <param name="NumeroCaso">El numero de caso tal como venia escrito.</param>
/// <param name="Mrn">El MRN tal como venia escrito.</param>
/// <param name="Nombre">El nombre tal como venia escrito; NO se usa para casar y NO se guarda.</param>
/// <param name="PersonaId">La persona de la base a la que resolvio.</param>
/// <param name="CasoId">El caso de esa persona; es lo que decide de que documento es el estado.</param>
/// <param name="Respuestas">Las siete respuestas, ya leidas: verdadero, falso o nulo.</param>
/// <param name="Motivo">Por que no se completo, si el companero lo dijo.</param>
/// <param name="Comentario">Lo que escribio de su puno; texto libre, tal cual.</param>
/// <param name="TraeAlgo">Si la fila venia con algo: una respuesta, el motivo o el comentario.</param>
public sealed record RenglonDeLaVuelta(
    int FilaExcel,
    string? NumeroCaso,
    string? Mrn,
    string? Nombre,
    long PersonaId,
    long CasoId,
    IReadOnlyDictionary<string, bool?> Respuestas,
    MotivoDeNoCompletar Motivo,
    string? Comentario,
    bool TraeAlgo);

/// <summary>Lo que sale de reconciliar un archivo: lo que caso, lo que no y lo que nadie miro.</summary>
/// <param name="Renglones">Las filas que resolvieron a una persona y traian algo contestado.</param>
/// <param name="Descartadas">Las que no entraron, con su motivo escrito y su numero de fila.</param>
/// <param name="SinNadaQueProponer">Las filas que resolvieron pero venian con las siete en blanco.</param>
/// <param name="Avisos">Lo que hay que senalar en la franja.</param>
public sealed record ResultadoDeReconciliar(
    IReadOnlyList<RenglonDeLaVuelta> Renglones,
    IReadOnlyList<FilaDescartada> Descartadas,
    IReadOnlyList<int> SinNadaQueProponer,
    IReadOnlyList<Aviso> Avisos);

/// <summary>
/// El Excel que vuelve: que fila actualiza a quien, y que fila no entra.
/// </summary>
/// <remarks>
/// Las dos reglas que gobiernan este archivo, y ninguna es de quien lo escribio:
/// <list type="number">
/// <item>Se casa por <c>numero_caso</c> + <c>mrn</c> + <c>id</c>, NUNCA por nombre
/// (DECISIONES.md, 2026-09-02 y 2026-09-03). Un acento de mas crea un registro fantasma; el
/// nombre que devuelve el companero no se lee para casar y no se escribe en la base.</item>
/// <item>La fila cuyo par no existe NO se aplica. Va a la lista de descartados con su motivo
/// escrito, para que Miguel decida. No se aplica «a ver si suena la flauta».</item>
/// </list>
/// <para>
/// Lo que entra es una PROPUESTA, no una verificacion (regla permanente 5). Lo que este
/// modulo escribe en el caso es el ESTADO, con el nombre del companero, que es la otra mitad
/// de la regla tal como el dueno la preciso el 2026-09-03. Son dos cosas y no se mezclan.
/// </para>
/// </remarks>
public static class Reconciliacion
{
    /// <summary>
    /// Reconcilia las filas ya leidas contra la base. No escribe nada: devuelve lo que paso.
    /// </summary>
    /// <param name="libro">Lo que devolvio <see cref="LectorDeExcel.Leer"/>.</param>
    /// <param name="personasQueCasan">
    /// Como se resuelve una clave contra la base: recibe <c>(numeroCaso, mrn, casoId)</c> y
    /// devuelve TODAS las personas a las que puede referirse. Cero, una, o varias — y varias
    /// es un descarte, no una eleccion.
    /// </param>
    /// <param name="companeroId">De quien es el Excel; va en cada renglon descartado.</param>
    /// <param name="rutaExcel">El archivo del que vinieron; va en cada renglon descartado.</param>
    /// <param name="ahora">La marca de tiempo con la que se sellan los descartes.</param>
    public static ResultadoDeReconciliar Reconciliar(
        LibroLeido libro,
        Func<string?, string?, long?, IReadOnlyList<Persona>> personasQueCasan,
        long companeroId,
        string rutaExcel,
        string ahora)
    {
        var renglones = new List<RenglonDeLaVuelta>();
        var descartadas = new List<FilaDescartada>();
        var sinNada = new List<int>();
        var yaVistos = new Dictionary<string, int>(StringComparer.Ordinal);
        var avisos = new List<Aviso>();

        AvisarDeLasColumnasQueNoTrae(libro, avisos);
        foreach (var fila in libro.Filas)
        {
            var valores = LectorDeExcel.FilaPorTitulo(libro.Titulos, fila);
            ClasificarUnaFila(valores, fila.Numero, personasQueCasan, yaVistos, renglones, sinNada, avisos,
                motivo => descartadas.Add(Descartar(fila.Numero, valores, motivo, companeroId, rutaExcel, ahora)));
        }

        if (renglones.Count > 0 && descartadas.Count > 0)
        {
            avisos.Add(Aviso.Advierte(
                $"Se pudieron leer {renglones.Count} filas y se descartaron {descartadas.Count}.",
                string.Empty,
                "Las descartadas NO están en la base: revíselas una a una antes de dar la ronda por "
                + "cerrada. Quedan guardadas en la lista de filas descartadas, así que no se pierden "
                + "al cerrar la ventana."));
        }
        return new ResultadoDeReconciliar(renglones, descartadas, sinNada, avisos);
    }

    /// <summary>
    /// Avisa UNA vez por archivo de las columnas nuevas que la hoja no trae.
    /// </summary>
    /// <remarks>
    /// Un Excel de antes del 2026-09-05 —o uno que Miguel armo por su cuenta— entra igual:
    /// no se rechaza nada. Lo que no se hace es callarlo, porque quien mire la vuelta tiene
    /// que saber por que no hay ningun motivo escrito en ninguna fila.
    /// </remarks>
    private static void AvisarDeLasColumnasQueNoTrae(LibroLeido libro, List<Aviso> avisos)
    {
        var titulos = libro.Titulos.Where(titulo => titulo is not null).ToHashSet(StringComparer.Ordinal)!;
        var faltan = new[] { MotivosDeLaHoja.RotuloDelMotivo, MotivosDeLaHoja.RotuloDelComentario }
            .Where(titulo => !titulos.Contains(titulo))
            .ToArray();
        if (faltan.Length == 0)
            return;

        avisos.Add(Aviso.Informa(
            "La hoja que volvió no trae " + string.Join(" ni ", faltan.Select(t => $"«{t}»")) + ".",
            string.Empty,
            "Se leyó todo lo demás y no se descartó ninguna fila. Es una hoja anterior al "
            + "2026-09-05: en ella el compañero no tenía dónde decir por qué no pudo, así que "
            + "ningún documento saldrá con motivo."));
    }

    private static void ClasificarUnaFila(
        IReadOnlyDictionary<string, string?> valores,
        int numeroDeFila,
        Func<string?, string?, long?, IReadOnlyList<Persona>> personasQueCasan,
        Dictionary<string, int> yaVistos,
        List<RenglonDeLaVuelta> renglones,
        List<int> sinNada,
        List<Aviso> avisos,
        Action<string> descartar)
    {
        var nombre = Valor(valores, "nombre");
        var respuestas = LeerLasRespuestas(valores, out var reparo);
        var motivo = LeerElMotivo(valores, numeroDeFila, avisos);
        var comentario = Valor(valores, MotivosDeLaHoja.ColumnaDelComentario);
        var traeAlgo = (respuestas is not null && respuestas.Values.Any(valor => valor is not null))
                       || motivo != MotivoDeNoCompletar.SinMotivo
                       || comentario is not null;

        var par = ParDeLaFila(valores, out var motivoDelPar);
        if (par is null)
        {
            descartar(motivoDelPar!);
            return;
        }

        // El reparo se descarta con la fila ENTERA y no solo su columna: aplicar cinco pasos de
        // seis dejaria a esa persona con un estado a medias que nadie escribio, y el sexto —el
        // que no se entendio— es justo el que podria estar diciendo que falta algo.
        if (reparo is not null)
        {
            descartar(reparo);
            return;
        }

        var (numeroCaso, mrn, casoId) = par.Value;
        if (mrn is null || (casoId is null && numeroCaso is null))
        {
            descartar(Motivos.SinClave);
            return;
        }

        var huella = $"{numeroCaso} {mrn} {casoId}";
        if (yaVistos.TryGetValue(huella, out var primera))
        {
            descartar(Motivos.Repetida(primera));
            return;
        }

        var casan = personasQueCasan(numeroCaso, mrn, casoId);
        if (casan.Count > 1)
        {
            // ⚠️ Dos personas de dos casos distintos, y la clave no distingue. NO se escoge una:
            // el trabajo del companero se escribiria sobre la familia equivocada y nadie se
            // enteraria.
            descartar(Motivos.ClaveAmbigua(casan.Count));
            return;
        }
        if (casan.Count == 0)
        {
            descartar(Motivos.SinPar);
            return;
        }

        yaVistos[huella] = numeroDeFila;
        if (!traeAlgo)
        {
            sinNada.Add(numeroDeFila);
            return;
        }
        renglones.Add(new RenglonDeLaVuelta(
            numeroDeFila, numeroCaso, mrn, nombre, casan[0].Id, casan[0].CasoId, respuestas!,
            motivo, comentario, traeAlgo));
    }

    /// <summary>
    /// El motivo de esa fila; sin motivo, y con su aviso, cuando no se entiende lo escrito.
    /// </summary>
    /// <remarks>
    /// ⚠️ Un motivo que no se entiende NO descarta la fila, al reves que un paso. Un paso
    /// ilegible puede ser justo el que dice que falta algo, asi que aplicar los otros cinco
    /// dejaria un estado que nadie escribio; un motivo no puede empeorar el estado de nadie,
    /// y tirar seis respuestas buenas por una frase escrita a mano seria perder el trabajo
    /// del companero. Se avisa con su fila y con lo que venia escrito, y quien lo lea decide.
    /// </remarks>
    private static MotivoDeNoCompletar LeerElMotivo(
        IReadOnlyDictionary<string, string?> valores, int numeroDeFila, List<Aviso> avisos)
    {
        var crudo = Valor(valores, MotivosDeLaHoja.ColumnaDelMotivo);
        if (MotivosDeLaHoja.SeEntiendeElMotivo(crudo, out var motivo, out var detalle))
            return motivo;

        avisos.Add(Aviso.Advierte(
            $"El motivo de la fila {numeroDeFila} no se entiende y se quedó sin escribir.",
            MotivosDeLaHoja.ColumnaDelMotivo,
            $"En la fila {numeroDeFila}, {detalle}. Lo demás de esa fila SÍ se leyó."));
        return MotivoDeNoCompletar.SinMotivo;
    }

    /// <summary>
    /// Lo que identifica la fila: la terna <c>(numeroCaso, mrn, casoId)</c>, o nulo con su motivo.
    /// </summary>
    /// <remarks>
    /// La clave MANDA sobre las dos columnas sueltas, y ese orden importa: la clave es UNA
    /// celda, se ve de un vistazo si se borro, y lleva el par entero. Un digito cambiado a
    /// mano en la columna del MRN no se nota, y esa fila se iria a descartados sin que nadie
    /// entendiera por que.
    /// <para>
    /// Cuando la hoja NO trae columna «clave» —un Excel que Miguel armo por su cuenta— se cae
    /// a las dos columnas sueltas. Es el mismo par leido de otro sitio: la regla de casar por
    /// <c>numero_caso</c> + <c>mrn</c> no cambia.
    /// </para>
    /// </remarks>
    private static (string? NumeroCaso, string? Mrn, long? CasoId)? ParDeLaFila(
        IReadOnlyDictionary<string, string?> valores, out string? motivo)
    {
        motivo = null;
        var tituloDeLaClave = Columnas.Por(Columnas.ColumnaDeLaClave).Titulo;
        if (valores.ContainsKey(tituloDeLaClave))
        {
            var clave = valores[tituloDeLaClave];
            if (string.IsNullOrWhiteSpace(clave))
            {
                motivo = Motivos.ClaveBorrada;
                return null;
            }
            var partida = Columnas.PartirLaClave(clave);
            if (partida is null)
            {
                motivo = Motivos.ClaveRota(clave);
                return null;
            }
            return (NormalizarNumeroDeCaso(partida.Value.NumeroCaso), partida.Value.Mrn, partida.Value.CasoId);
        }

        var numeroCaso = NormalizarNumeroDeCaso(Valor(valores, "numero_caso"));
        var mrn = Valor(valores, "mrn");
        if (numeroCaso is null || mrn is null)
        {
            motivo = Motivos.SinClave;
            return null;
        }
        return (numeroCaso, mrn, null);
    }

    /// <summary>
    /// El numero de caso tal como se guarda: sin espacios y en mayusculas.
    /// </summary>
    /// <remarks>
    /// Las mayusculas se fuerzan porque la base las exige y porque Excel no las conserva por
    /// su cuenta: un <c>casp2609</c> tecleado en minuscula es el MISMO caso, y descartarlo por
    /// eso seria perder trabajo bueno. NO es inventar un dato: cambiar la caja de una letra no
    /// cambia que caso es. Eso lo distingue del MRN, donde rellenar un cero SI inventaria.
    /// </remarks>
    private static string? NormalizarNumeroDeCaso(string? valor)
        => valor is null ? null : valor.Trim().ToUpperInvariant();

    /// <summary>Las siete respuestas de la hoja; nulo con su reparo si alguna no se entiende.</summary>
    private static Dictionary<string, bool?>? LeerLasRespuestas(
        IReadOnlyDictionary<string, string?> valores, out string? reparo)
    {
        reparo = null;
        var respuestas = new Dictionary<string, bool?>();
        // Solo las de si o no. El motivo y el comentario los rellena el companero igual,
        // pero NO se leen aqui: no son verdadero ni falso, y una frase libre metida en este
        // bucle seria una «respuesta ilegible» que descarta la fila entera.
        foreach (var columna in Columnas.Todas.Where(c => c.EsSiONo))
        {
            valores.TryGetValue(columna.Titulo, out var crudo);
            if (!Pasos.SeEntiendeLaRespuesta(crudo, out var valor, out var detalle))
            {
                reparo = Motivos.RespuestaIlegible(columna.Titulo, detalle!);
                return null;
            }
            respuestas[columna.Nombre] = valor;
        }
        return respuestas;
    }

    private static string? Valor(IReadOnlyDictionary<string, string?> valores, string nombreDeColumna)
        => valores.TryGetValue(Columnas.Por(nombreDeColumna).Titulo, out var valor) ? valor : null;

    /// <summary>
    /// El renglon de un descarte, tal como va a su tabla. Ni el caso ni el MRN se validan:
    /// lo que venia escrito puede ser justo lo que estaba mal.
    /// </summary>
    private static FilaDescartada Descartar(
        int numeroDeFila,
        IReadOnlyDictionary<string, string?> valores,
        string motivo,
        long companeroId,
        string rutaExcel,
        string ahora)
        => new()
        {
            CompaneroId = companeroId,
            RutaExcel = rutaExcel,
            FilaExcel = numeroDeFila,
            NumeroCaso = Valor(valores, "numero_caso"),
            Mrn = Valor(valores, "mrn"),
            Nombre = Valor(valores, "nombre"),
            Motivo = $"Fila {numeroDeFila}: {motivo}.",
            RegistradoEn = ahora,
        };
}
