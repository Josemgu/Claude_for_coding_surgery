namespace Fichas.App.Importar;

/// <summary>
/// Recorre lo que Miguel eligio carpeta a carpeta, sin que una carpeta cerrada tumbe el resto.
/// </summary>
/// <remarks>
/// <para>⛔ <b>Por que a mano y no con <c>SearchOption.AllDirectories</c>.</b> Esa opcion
/// aborta la enumeracion ENTERA en la primera carpeta que el sistema no deja abrir, y se
/// lleva por delante todo lo que faltaba por recorrer, no solo lo de dentro de esa carpeta.
/// Peor todavia: devuelve un enumerable perezoso, asi que la excepcion no salta donde se
/// escribio la llamada sino en el <c>foreach</c> de quien la consume, y un <c>try</c>
/// puesto alrededor de la llamada NO la atrapa. Eso fue exactamente lo que paso el
/// 2026-09-07: elegir la carpeta del perfil de usuario dejaba la tanda en 0 documentos con
/// un solo aviso, este, para toda la tanda:</para>
/// <code>Access to the path 'C:\Users\josem\Application Data' is denied</code>
///
/// <para>No hace falta ningun permiso raro para llegar ahi. Dentro de la carpeta de un
/// usuario de Windows viven uniones heredadas —«Application Data», «Mis documentos»,
/// «Configuración local»— que existen para programas de hace veinte años y que el propio
/// Windows deniega. Le pasa a cualquiera que elija su carpeta de usuario.</para>
///
/// <para>⛔ <b>Y por que lleva memoria de por donde paso.</b> Una union de directorio que
/// apunte hacia atras —<c>mklink /J C:\a\b C:\a</c>, que cualquiera crea sin ser
/// administrador— haria dar vueltas para siempre a un recorrido a mano. Colgar el programa
/// seria peor que el fallo que esto viene a arreglar. La memoria es de destinos RESUELTOS,
/// no de rutas escritas: es lo unico que cierra el circulo cuando el camino que da la vuelta
/// se llama distinto cada vez.</para>
/// </remarks>
internal sealed class RecorridoDeLoElegido
{
    /// <summary>Los PDF encontrados, con ruta completa y sin repetidos aunque se elijan dos veces.</summary>
    private readonly HashSet<string> _pdf = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Los destinos RESUELTOS por los que ya se pasó; es lo que corta los bucles de uniones de directorio.</summary>
    private readonly HashSet<string> _carpetasYaRecorridas = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Lo que el sistema no dejó abrir, con su motivo, en el orden en que se encontró.</summary>
    private readonly List<CarpetaQueNoSeDejoLeer> _noSeDejaronLeer = [];

    /// <summary>Suma un origen suelto: un archivo, o una carpeta con todo su arbol.</summary>
    /// <param name="origen">Ruta de archivo o de carpeta; nula, vacía, inexistente o que no sea PDF no suma nada.</param>
    public void Agregar(string? origen)
    {
        if (string.IsNullOrWhiteSpace(origen)) return;

        try
        {
            if (Directory.Exists(origen)) { RecorrerElArbol(origen); return; }
            if (File.Exists(origen) && RutasDePdf.EsPdf(origen)) _pdf.Add(Path.GetFullPath(origen));
        }
        catch (Exception fallo) when (EsUnFalloDeDisco(fallo))
        {
            // Un origen que ni siquiera se puede mirar se nombra igual: el dueño eligio
            // esto, y que desaparezca de la cuenta sin decir nada es fallar callado.
            _noSeDejaronLeer.Add(new CarpetaQueNoSeDejoLeer(origen, MotivoDe(fallo)));
        }
    }

    /// <summary>Los PDF ordenados y lo que se quedo fuera, ya cerrado.</summary>
    public LoQueSeEncontro LoQueSalio() => new(
        _pdf.OrderBy(ruta => ruta, StringComparer.OrdinalIgnoreCase).ToArray(),
        _noSeDejaronLeer.ToArray());

    /// <summary>
    /// Recorre una carpeta y las de dentro, una a una, apuntando la que no se deje.
    /// </summary>
    /// <remarks>
    /// Con pila y no con recursion: un arbol muy hondo desbordaria la del hilo, y un
    /// desbordamiento de pila no se puede atrapar ni contar —se lleva el proceso entero—.
    /// </remarks>
    /// <param name="carpetaDeArranque">La carpeta elegida; se recorre ella y todo lo de dentro.</param>
    private void RecorrerElArbol(string carpetaDeArranque)
    {
        var porRecorrer = new Stack<string>();
        Apilar(carpetaDeArranque, porRecorrer);

        while (porRecorrer.Count > 0)
        {
            var carpeta = porRecorrer.Pop();
            if (!RecogerLosPdfDe(carpeta)) continue;

            ApilarLasDeDentro(carpeta, porRecorrer);
        }
    }

    /// <summary>Los PDF de esta carpeta, sin bajar. Falso si no se dejo leer.</summary>
    /// <param name="carpeta">La carpeta con su ruta completa.</param>
    private bool RecogerLosPdfDe(string carpeta)
    {
        try
        {
            foreach (var archivo in Directory.EnumerateFiles(carpeta))
            {
                if (RutasDePdf.EsPdf(archivo)) _pdf.Add(archivo);
            }

            return true;
        }
        catch (Exception fallo) when (EsUnFalloDeDisco(fallo))
        {
            _noSeDejaronLeer.Add(new CarpetaQueNoSeDejoLeer(carpeta, MotivoDe(fallo)));
            return false;
        }
    }

    /// <summary>Apila las subcarpetas para seguir bajando.</summary>
    /// <remarks>
    /// Va aparte de los archivos porque son dos permisos distintos y pueden fallar por
    /// separado; quien no se deje se apunta UNA sola vez, que es lo que hace el
    /// <c>return</c> de arriba cuando ya fallo al leer los archivos.
    /// </remarks>
    /// <param name="carpeta">La carpeta cuyas subcarpetas se apilan.</param>
    /// <param name="porRecorrer">La pila del recorrido.</param>
    private void ApilarLasDeDentro(string carpeta, Stack<string> porRecorrer)
    {
        try
        {
            foreach (var dentro in Directory.EnumerateDirectories(carpeta)) Apilar(dentro, porRecorrer);
        }
        catch (Exception fallo) when (EsUnFalloDeDisco(fallo))
        {
            _noSeDejaronLeer.Add(new CarpetaQueNoSeDejoLeer(carpeta, MotivoDe(fallo)));
        }
    }

    /// <summary>
    /// Deja una carpeta para recorrer, si no se recorrio ya por otro camino.
    /// </summary>
    /// <remarks>
    /// La que ya se recorrio NO se apunta como perdida: sus archivos entraron por el otro
    /// camino, y nombrarla haria pensar que falta algo cuando no falta nada.
    /// </remarks>
    /// <param name="carpeta">La carpeta tal como la devolvió el sistema.</param>
    /// <param name="porRecorrer">La pila del recorrido.</param>
    private void Apilar(string carpeta, Stack<string> porRecorrer)
    {
        string completa, aQueApunta;
        try
        {
            var laCarpeta = new DirectoryInfo(carpeta);
            completa = laCarpeta.FullName;
            aQueApunta = ADondeLleva(laCarpeta);
        }
        catch (Exception fallo) when (EsUnFalloDeDisco(fallo))
        {
            _noSeDejaronLeer.Add(new CarpetaQueNoSeDejoLeer(carpeta, MotivoDe(fallo)));
            return;
        }

        if (_carpetasYaRecorridas.Add(aQueApunta)) porRecorrer.Push(completa);
    }

    /// <summary>
    /// La carpeta a la que se llega de verdad: si es una union o un enlace, su destino final.
    /// </summary>
    /// <param name="laCarpeta">La carpeta, ya abierta como <see cref="DirectoryInfo"/>.</param>
    /// <returns>La ruta completa del destino, sin barra final; la propia carpeta si no es enlace.</returns>
    private static string ADondeLleva(DirectoryInfo laCarpeta)
    {
        var destino = laCarpeta.ResolveLinkTarget(returnFinalTarget: true);
        return Path.GetFullPath(destino?.FullName ?? laCarpeta.FullName)
            .TrimEnd(Path.DirectorySeparatorChar);
    }

    /// <summary>Lo que el disco puede contestar mal sin que sea un defecto del programa.</summary>
    /// <remarks>
    /// Es una lista cerrada a proposito. Atrapar <c>Exception</c> aqui convertiria un defecto
    /// del programa en «una carpeta que no se dejo leer», y ese defecto no volveria a verse.
    /// </remarks>
    /// <param name="fallo">Lo que levantó el sistema de archivos.</param>
    private static bool EsUnFalloDeDisco(Exception fallo)
        => fallo is UnauthorizedAccessException
                 or IOException
                 or System.Security.SecurityException
                 or ArgumentException
                 or NotSupportedException;

    /// <summary>Por que no se pudo, en español y para leerlo.</summary>
    /// <remarks>
    /// El mensaje que trae Windows viene en ingles —«Access to the path … is denied»— y la
    /// regla permanente 4 dice español en los mensajes de error. Lo que se escribe es lo que
    /// el tipo de fallo significa, que ademas es mas util: al dueño le sirve saber que es un
    /// permiso, no la frase literal del sistema.
    /// </remarks>
    /// <param name="fallo">Uno de los que <see cref="EsUnFalloDeDisco"/> admite.</param>
    private static string MotivoDe(Exception fallo) => fallo switch
    {
        UnauthorizedAccessException or System.Security.SecurityException
            => "el sistema no da permiso para leerla.",
        DirectoryNotFoundException => "ya no está donde estaba.",
        PathTooLongException => "su ruta es demasiado larga para Windows.",
        IOException => "el sistema de archivos no la pudo leer.",
        _ => "su ruta no es una ruta que Windows admita.",
    };
}
