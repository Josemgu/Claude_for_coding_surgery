using Fichas.Contratos.Modelos;

namespace Fichas.App.Importar;

/// <summary>
/// El segundo paso de la comprobación: leer con OCR lo justo para poder decidir. No toca
/// la base, así que puede correr en otro hilo.
/// </summary>
public sealed partial class ComprobacionDeLosPapeles
{
    /// <summary>
    /// Lee el número de caso del archivo de cada disputa y del único candidato de cada papel
    /// perdido, y calcula la huella de cada escaneo para encontrar los pares idénticos.
    /// </summary>
    /// <remarks>
    /// Una lectura por archivo y no una por documento: la disputa de cuatro casos sobre
    /// <c>Scan.pdf</c> es UNA lectura. Y el OCR que falle no tumba nada: el archivo queda
    /// como «no se pudo leer» y se dice. Las huellas leen cada archivo entero, y por eso esto
    /// corre en otro hilo y no al llegar a la pantalla.
    /// </remarks>
    /// <param name="plan">Lo que dejó <see cref="Planear"/>.</param>
    /// <returns>Las lecturas, para <see cref="Aplicar"/>.</returns>
    public LecturasDeLosPapeles Leer(PlanDeLosPapeles plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new LecturasDeLosPapeles(
            [.. plan.Disputas.Select(LeerLaDisputa)],
            [.. plan.Perdidos.Select(BuscarElPapelPerdido)],
            ParesIdenticos(plan.ConEscaneo));
    }

    /// <summary>Qué dice hoy el archivo de una disputa; si no existe o no se lee, lo dice.</summary>
    /// <param name="disputa">La hoja reclamada por varios.</param>
    private LecturaDeUnaDisputa LeerLaDisputa(PapelEnDisputa disputa)
    {
        if (!File.Exists(disputa.RutaPdf)) return new LecturaDeUnaDisputa(disputa, null, ElArchivoExiste: false, Error: null);
        var (numero, error) = LeerSinTumbarNada(disputa.RutaPdf, disputa.PaginaPdf);
        return new LecturaDeUnaDisputa(disputa, numero, ElArchivoExiste: true, error);
    }

    /// <summary>
    /// Busca el archivo perdido por su nombre en las carpetas hermanas de la que decía, y
    /// lee su número solo si hay exactamente uno.
    /// </summary>
    /// <remarks>
    /// <para>Lo que pasó en la máquina del dueño: las carpetas se renombraron a «… Complete» y
    /// los archivos siguen ahí con su nombre. Se mira UN nivel —las hermanas de la carpeta que
    /// decía la ruta— y no el disco entero: buscar por nombre en todo el disco encontraría
    /// «Scan.pdf» en cualquier parte.</para>
    ///
    /// <para>⛔ Con dos candidatos no se lee ninguno: elegir entre dos por el nombre sería
    /// adivinar, y leer los dos para quedarse con el que diga el número seguiría siendo
    /// adivinar cuando los dos lo digan.</para>
    /// </remarks>
    /// <param name="perdido">El caso cuyo archivo no está.</param>
    private LecturaDeUnPerdido BuscarElPapelPerdido(Caso perdido)
    {
        var candidatos = CandidatosPorNombre(perdido.RutaPdf!);
        if (candidatos.Count != 1) return new LecturaDeUnPerdido(perdido, candidatos, null, null);

        var (numero, error) = LeerSinTumbarNada(candidatos[0], perdido.PaginaPdf!.Value);
        return new LecturaDeUnPerdido(perdido, candidatos, numero, error);
    }

    /// <summary>Los archivos con el mismo nombre en las carpetas hermanas de la que decía la ruta.</summary>
    /// <param name="rutaPerdida">La ruta que ya no existe.</param>
    /// <returns>Ordenados por ruta; vacía si la carpeta madre tampoco existe o no se deja leer.</returns>
    private static IReadOnlyList<string> CandidatosPorNombre(string rutaPerdida)
    {
        try
        {
            var nombre = Path.GetFileName(rutaPerdida);
            var carpeta = Path.GetDirectoryName(Path.GetFullPath(rutaPerdida));
            var madre = carpeta is null ? null : Path.GetDirectoryName(carpeta);
            if (madre is null || !Directory.Exists(madre)) return [];

            return
            [
                .. Directory.EnumerateDirectories(madre)
                    .Select(hermana => Path.Combine(hermana, nombre))
                    .Where(File.Exists)
                    .OrderBy(ruta => ruta, StringComparer.OrdinalIgnoreCase),
            ];
        }
        catch (Exception fallo) when (fallo is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return [];
        }
    }

    /// <summary>Lee el número con el lector inyectado; si el lector revienta, el fallo viaja como dato.</summary>
    /// <remarks>
    /// Se atrapa <see cref="Exception"/> a secas por lo mismo que en <c>MotorDeImportacion</c>:
    /// por debajo estan PDFium, PdfPig y onnxruntime, y una lista de tipos seria la lista de
    /// los fallos que se me ocurrieron. ⛔ No se calla: el tipo y el texto del fallo vuelven
    /// enteros y acaban en el aviso que lee el dueño.
    /// </remarks>
    /// <param name="ruta">El archivo.</param>
    /// <param name="pagina">La hoja, base 1.</param>
    /// <returns>El número leído (o nulo) y el fallo si lo hubo.</returns>
    private (string? Numero, string? Error) LeerSinTumbarNada(string ruta, int pagina)
    {
        try
        {
            return (_leerElNumero(ruta, pagina), null);
        }
        catch (Exception causa)
        {
            return (null, $"{causa.GetType().Name}: {causa.Message}");
        }
    }
}

/// <summary>Lo que dice hoy el archivo de una disputa.</summary>
/// <param name="Disputa">La hoja reclamada por varios.</param>
/// <param name="NumeroLeido">El número de caso leído en el archivo, o nulo si no se leyó ninguno.</param>
/// <param name="ElArchivoExiste">Falso si el archivo ya no está: entonces no se decide.</param>
/// <param name="Error">El fallo del lector, con su tipo y su texto, o nulo si leyó.</param>
public sealed record LecturaDeUnaDisputa(PapelEnDisputa Disputa, string? NumeroLeido, bool ElArchivoExiste, string? Error);

/// <summary>Lo que se encontró de un papel perdido.</summary>
/// <param name="Caso">El caso cuyo archivo no está.</param>
/// <param name="Candidatos">Los archivos con su nombre en las carpetas hermanas; solo con uno se decide.</param>
/// <param name="NumeroLeido">El número leído en el único candidato, o nulo.</param>
/// <param name="Error">El fallo del lector, con su tipo y su texto, o nulo si leyó o no hubo que leer.</param>
public sealed record LecturaDeUnPerdido(Caso Caso, IReadOnlyList<string> Candidatos, string? NumeroLeido, string? Error);

/// <summary>Todo lo leído, listo para aplicar.</summary>
/// <param name="Disputas">Una lectura por disputa.</param>
/// <param name="Perdidos">Una búsqueda por papel perdido.</param>
/// <param name="Pares">Los pares idénticos, tal como vinieron del plan.</param>
public sealed record LecturasDeLosPapeles(
    IReadOnlyList<LecturaDeUnaDisputa> Disputas,
    IReadOnlyList<LecturaDeUnPerdido> Perdidos,
    IReadOnlyList<ParDeIguales> Pares);
