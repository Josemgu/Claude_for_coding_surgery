using System.Globalization;
using System.Text.RegularExpressions;
using Fichas.App.Revisar;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Importar;

/// <summary>
/// Las carpetas que la tanda recien importada va a formar en Revisar, para poder corregirlas
/// alli mismo.
/// </summary>
/// <remarks>
/// <para>Peticion 6 del dueno del 2026-09-07, con sus palabras: <i>«en la ventana de
/// importar documentos, cuando carga los PDF, debe permitir editar las carpetas que se
/// mostraran en Revisar, porque a veces el sistema no pone los nombres de manera
/// correcta»</i>.</para>
///
/// <para>⚠️ <b>Lo primero que hubo que medir es que un «nombre de carpeta» NO existe en
/// ningun sitio.</b> <see cref="ArbolDeRevisar"/> compone los tres niveles —mes, fecha de
/// viaje y unidad— a partir de tres columnas del caso: <c>fecha_viaje</c>,
/// <c>unidad_numero</c> y <c>unidad_nombre</c>. No hay una tabla de carpetas ni un texto
/// guardado que renombrar. Asi que «editar la carpeta» es <b>corregir esas tres columnas en
/// los documentos que cuelgan de ella</b>, y el nombre cambia solo.</para>
///
/// <para>⛔ <b>La agrupacion NO se vuelve a escribir aqui: se le pide a
/// <see cref="ArbolDeRevisar.Agrupar"/>.</b> Dos reglas separadas para el mismo arbol
/// acabarian discrepando, y entonces el dueno corregiria en Importar una carpeta que en
/// Revisar se llama de otra manera —que es exactamente el defecto que esto viene a
/// arreglar—.</para>
///
/// <para>⛔ <b>Nada se adivina</b> (regla permanente 1). Una fecha que no es una fecha o un
/// numero de unidad que el esquema no admite <b>no se escriben</b>, y se dice por que.
/// Escribirlos dejaria el documento donde estaba —el arbol no sabe leerlos— pero con el
/// dueno creyendo que ya lo puso.</para>
/// </remarks>
public static partial class CarpetasDeLaTanda
{
    /// <summary>
    /// Las carpetas que formaran esos documentos, tal como se veran en Revisar.
    /// </summary>
    /// <param name="casos">Los casos que entraron en la tanda; los nulos se saltan.</param>
    /// <returns>Una carpeta por unidad, en el orden en que Revisar las pinta; vacía si no hay casos.</returns>
    public static IReadOnlyList<CarpetaDeLaTanda> Componer(IEnumerable<Caso> casos)
    {
        ArgumentNullException.ThrowIfNull(casos);

        var porCaso = casos.Where(caso => caso is not null).ToDictionary(caso => caso.Id);

        return
        [
            .. ArbolDeRevisar.Agrupar(porCaso.Values.Select(TarjetaMinima))
                .SelectMany(mes => mes.Fechas.SelectMany(fecha => fecha.Unidades.Select(
                    unidad => new CarpetaDeLaTanda
                    {
                        CarpetaDelMes = mes.Carpeta,
                        CarpetaDeLaFecha = fecha.Carpeta,
                        CarpetaDeLaUnidad = unidad.Carpeta,
                        FechaDeViaje = fecha.FechaIso,
                        UnidadNumero = unidad.Numero,
                        UnidadNombre = unidad.Nombre,
                        CasoIds = [.. unidad.Documentos.Select(documento => documento.CasoId)],
                    }))),
        ];
    }

    /// <summary>
    /// Escribe la fecha y la unidad corregidas en todos los documentos de esa carpeta.
    /// </summary>
    /// <remarks>
    /// <para>⚠️ <b>Cada caso se lee entero y se le cambian tres campos con <c>with</c>.</b>
    /// No es un rodeo: <c>RepositorioDeCasos.Guardar</c> de un caso que ya existe hace un
    /// UPDATE de las veintiuna columnas, asi que componer el caso a mano borraria el numero
    /// de caso, el templo, el estado y la firma de quien lo marco.</para>
    ///
    /// <para>Se comprueban las dos entradas ANTES de escribir ninguna, y si una no vale no
    /// se escribe nada: corregir una carpeta es UN gesto del dueno, y dejarla con el nombre
    /// nuevo y la fecha vieja seria peor que no hacer nada.</para>
    /// </remarks>
    /// <param name="casos">Por donde se escribe.</param>
    /// <param name="carpeta">La carpeta que se corrige, con los documentos que cuelgan de ella.</param>
    /// <param name="fechaDeViaje">La fecha de viaje en <c>AAAA-MM-DD</c>, o vacio para dejarla sin poner.</param>
    /// <param name="unidadNumero">El numero de unidad, 6 o 7 digitos, o vacio.</param>
    /// <param name="unidadNombre">El nombre de la unidad, o vacio.</param>
    /// <returns>
    /// Con <c>SeEscribio</c> falso y los reparos como avisos cuando una entrada no vale o la
    /// carpeta no tiene documentos; si no, cuántos documentos quedaron escritos.
    /// </returns>
    public static ResultadoDeLaCorreccion Corregir(
        ICasos casos,
        CarpetaDeLaTanda carpeta,
        string? fechaDeViaje,
        string? unidadNumero,
        string? unidadNombre)
    {
        ArgumentNullException.ThrowIfNull(casos);
        ArgumentNullException.ThrowIfNull(carpeta);

        var fecha = Limpio(fechaDeViaje);
        var numero = Limpio(unidadNumero);
        var nombre = Limpio(unidadNombre);

        var reparos = LoQueNoSePuedeEscribir(fecha, numero);
        if (reparos.Count > 0) return new ResultadoDeLaCorreccion(false, 0, reparos);

        if (carpeta.CasoIds.Count == 0)
        {
            return new ResultadoDeLaCorreccion(false, 0, [Aviso.Informa(
                "Esa carpeta no tiene ningún documento detrás: no hay nada que corregir.")]);
        }

        return Escribir(casos, carpeta, fecha, numero, nombre);
    }

    /// <summary>Escribe los tres campos en cada documento de la carpeta y cuenta los que entraron.</summary>
    /// <param name="casos">Por donde se escribe.</param>
    /// <param name="carpeta">La carpeta cuyos documentos se corrigen.</param>
    /// <param name="fecha">La fecha ya limpia y comprobada; vacía deja la columna en nulo.</param>
    /// <param name="numero">El número de unidad ya limpio y comprobado; vacío deja la columna en nulo.</param>
    /// <param name="nombre">El nombre de la unidad ya limpio; vacío deja la columna en nulo.</param>
    private static ResultadoDeLaCorreccion Escribir(
        ICasos casos, CarpetaDeLaTanda carpeta, string fecha, string numero, string nombre)
    {
        var avisos = new List<Aviso>();
        var escritos = 0;

        foreach (var casoId in carpeta.CasoIds)
        {
            var guardado = casos.Obtener(casoId);
            if (guardado is null)
            {
                avisos.Add(Aviso.Advierte(
                    $"El documento {casoId.ToString(CultureInfo.InvariantCulture)} ya no está en la base.",
                    string.Empty,
                    "Puede que otra ventana lo haya borrado mientras esta estaba abierta."));
                continue;
            }

            var escrito = casos.Guardar(guardado with
            {
                FechaViaje = ONulo(fecha),
                UnidadNumero = ONulo(numero),
                UnidadNombre = ONulo(nombre),
            });

            avisos.AddRange(escrito.Avisos);
            if (escrito.SeEscribio) escritos++;
        }

        return new ResultadoDeLaCorreccion(escritos > 0, escritos, avisos);
    }

    /// <summary>Los reparos que impiden escribir; vacio si las dos entradas valen.</summary>
    /// <remarks>
    /// El nombre de la unidad NO se comprueba: es texto de una etiqueta de papel y no hay
    /// forma de saber cual es correcto. Lo que si tiene forma son la fecha y el numero.
    /// </remarks>
    /// <param name="fecha">La fecha ya sin espacios; vacía no es un reparo.</param>
    /// <param name="numero">El número de unidad ya sin espacios; vacío no es un reparo.</param>
    private static IReadOnlyList<Aviso> LoQueNoSePuedeEscribir(string fecha, string numero)
    {
        var reparos = new List<Aviso>();

        if (fecha.Length > 0 && !EsUnaFecha(fecha))
        {
            reparos.Add(Aviso.Problema(
                $"«{fecha}» no es una fecha, así que no se escribió nada.",
                "fecha_viaje",
                "La fecha de viaje se escribe como 2026-09-12: cuatro cifras de año, dos de mes y "
                + "dos de día. El programa NO adivina qué quiso decir: un grupo colocado en el día "
                + "equivocado es gente que viaja sin recomendación revisada."));
        }

        if (numero.Length > 0 && !PatronDeUnidad().IsMatch(numero))
        {
            reparos.Add(Aviso.Problema(
                $"«{numero}» no es un número de unidad, así que no se escribió nada.",
                "unidad_numero",
                "El número de unidad son 6 o 7 cifras seguidas, sin espacios ni guiones."));
        }

        return reparos;
    }

    /// <summary>Una tarjeta con lo justo para agrupar: es lo unico que el arbol mira.</summary>
    /// <remarks>
    /// Se construye aqui y no se le pide a <c>TableroDeRevisar</c> porque esa clase necesita
    /// los cinco repositorios y una tanda recien importada solo tiene los casos delante.
    /// Los cuatro campos son los que <see cref="ArbolDeRevisar"/> lee, y no hay mas.
    /// </remarks>
    /// <param name="caso">El caso recién importado.</param>
    private static TarjetaDeDocumento TarjetaMinima(Caso caso) => new()
    {
        CasoId = caso.Id,
        NumeroDeCaso = caso.NumeroCaso ?? string.Empty,
        FechaDeViajeIso = caso.FechaViaje?.Trim() ?? string.Empty,
        UnidadNumero = caso.UnidadNumero?.Trim() ?? string.Empty,
        Unidad = string.IsNullOrWhiteSpace(caso.UnidadNombre)
            ? TarjetaDeDocumento.SinUnidad
            : caso.UnidadNombre,
    };

    /// <summary>Una fecha ISO-8601 estricta y que exista de verdad; el 31 de febrero no.</summary>
    /// <param name="texto">Lo que escribió el dueño, ya sin espacios.</param>
    private static bool EsUnaFecha(string texto)
        => DateOnly.TryParseExact(
            texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    /// <summary>Sin espacios por los lados, y nulo se vuelve vacío para poder comparar largos.</summary>
    /// <param name="texto">Lo que trae la caja de la pantalla.</param>
    private static string Limpio(string? texto) => texto?.Trim() ?? string.Empty;

    /// <summary>Lo contrario de <see cref="Limpio"/>: vacío se guarda como nulo, que es como la base dice «sin poner».</summary>
    /// <param name="texto">El texto ya limpio.</param>
    private static string? ONulo(string texto) => texto.Length == 0 ? null : texto;

    /// <summary>Seis o siete cifras seguidas: lo que admite <c>unidad_numero</c> (DECISIONES.md, 2026-09-02, «manda el papel»).</summary>
    [GeneratedRegex(@"^\d{6,7}$")]
    private static partial Regex PatronDeUnidad();
}

/// <summary>
/// Una carpeta de Revisar vista desde la importacion, con los documentos que cuelgan de ella.
/// </summary>
/// <remarks>
/// Los tres nombres NO se componen aqui: llegan de <see cref="ArbolDeRevisar"/>, que es
/// quien los compone para la pantalla de Revisar. Guardarlos ya compuestos es lo que
/// permite ensenarle al dueno la carpeta con el mismo nombre que va a leer despues.
/// </remarks>
public sealed record CarpetaDeLaTanda
{
    /// <summary>«Septiembre 2026», o la carpeta de lo que no tiene fecha.</summary>
    public string CarpetaDelMes { get; init; } = string.Empty;

    /// <summary>«Grupo del 20 de septiembre», o la carpeta de lo que no tiene fecha.</summary>
    public string CarpetaDeLaFecha { get; init; } = string.Empty;

    /// <summary>«123456 · Barrio de prueba», o la carpeta de lo que no tiene unidad.</summary>
    public string CarpetaDeLaUnidad { get; init; } = string.Empty;

    /// <summary>La fecha de viaje que hay hoy, en <c>AAAA-MM-DD</c>, o vacia.</summary>
    /// <remarks>
    /// Sale de la CLAVE del grupo de fecha, que es la fecha ya leida. Si lo guardado no era
    /// una fecha, aqui llega vacio: es lo honesto, porque el arbol tampoco la supo leer y el
    /// documento esta en «sin fecha de viaje» aunque la columna tenga algo escrito.
    /// </remarks>
    public string FechaDeViaje { get; init; } = string.Empty;

    /// <summary>El número de unidad que hay hoy, o vacío.</summary>
    public string UnidadNumero { get; init; } = string.Empty;

    /// <summary>El nombre de unidad que hay hoy, o vacío.</summary>
    public string UnidadNombre { get; init; } = string.Empty;

    /// <summary>Los documentos que cuelgan de esta carpeta.</summary>
    public IReadOnlyList<long> CasoIds { get; init; } = [];

    /// <summary>Cuántos documentos cuelgan de ella.</summary>
    public int CuantosDocumentos => CasoIds.Count;

    /// <summary>La ruta entera, tal como se lee en Revisar.</summary>
    public string Ruta => string.Join(" / ", CarpetaDelMes, CarpetaDeLaFecha, CarpetaDeLaUnidad);

    /// <summary>Lo que se lee en la lista: la ruta con su cifra detrás, como en Revisar.</summary>
    public string Etiqueta => $"{Ruta} · {Plural.Con(CuantosDocumentos, "documento", "documentos")}";
}

/// <summary>Qué pasó al corregir una carpeta.</summary>
/// <param name="SeEscribio">Si de verdad cambió algo en la base.</param>
/// <param name="Documentos">Cuántos documentos quedaron corregidos.</param>
/// <param name="Avisos">Lo que hay que decir; puede estar vacío.</param>
public sealed record ResultadoDeLaCorreccion(
    bool SeEscribio, int Documentos, IReadOnlyList<Aviso> Avisos);
