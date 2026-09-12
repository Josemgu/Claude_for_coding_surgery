using Fichas.Reportes.Modelo;

namespace Fichas.Reportes.Formato;

/// <summary>
/// Como se llama cada pestana del informe en Excel.
/// </summary>
/// <remarks>
/// <para>Excel pone tres condiciones a un nombre de pestana y las tres se incumplen solas con
/// los titulos de este informe: no mas de <see cref="LargoMaximo"/> caracteres —«Parte 1 —
/// Personas que viajaron, y en qué estado quedó su recomendación» mide 70—, ninguno de los
/// siete signos de <see cref="LoQueExcelNoAdmite"/>, y dos pestanas no se pueden llamar
/// igual. Un libro con un nombre malo no se abre a medias: Excel lo rechaza entero.</para>
///
/// <para><b>El numero de delante no es decoracion.</b> Recortar a 31 caracteres puede dejar
/// dos titulos largos convertidos en el mismo nombre; el numero los separa siempre y ademas
/// dice en que orden se leen, que en el PDF lo dice el propio orden de las paginas y en un
/// libro de Excel no lo diria nada.</para>
///
/// <para>El titulo entero no se pierde: va en la hoja de resumen, en su indice.</para>
/// </remarks>
public static class NombreDeHoja
{
    /// <summary>Lo mas largo que Excel admite en el nombre de una pestana.</summary>
    public const int LargoMaximo = 31;

    /// <summary>La pestana que va delante de todas: de que es el informe y como se lee.</summary>
    public const string DelResumen = "Resumen";

    /// <summary>Como se llama una seccion que llego sin titulo.</summary>
    private const string SinTitulo = "sección";

    /// <summary>Los siete signos que Excel no admite en el nombre de una pestana.</summary>
    private static readonly char[] LoQueExcelNoAdmite = [':', '\\', '/', '?', '*', '[', ']'];

    /// <summary>El nombre de la pestana de cada seccion, en el orden en que llegan.</summary>
    /// <param name="secciones">Las secciones del documento.</param>
    /// <returns>Un nombre por seccion, todos distintos y todos admitidos por Excel.</returns>
    public static IReadOnlyList<string> DeLasSecciones(IReadOnlyList<Seccion> secciones)
    {
        ArgumentNullException.ThrowIfNull(secciones);

        return [.. secciones.Select((seccion, indice) => Una(indice + 1, seccion.Titulo))];
    }

    /// <summary>El nombre de una pestana: su numero, su titulo limpio y recortado.</summary>
    /// <param name="numero">El orden de la sección, base 1; va delante y es lo que garantiza que dos nombres no coincidan.</param>
    /// <param name="titulo">El título de la sección; vacío da «sección N».</param>
    /// <returns>Nunca más de <see cref="LargoMaximo"/> caracteres y sin espacio al final.</returns>
    private static string Una(int numero, string titulo)
    {
        var limpio = Limpiar(titulo);
        if (limpio.Length == 0) limpio = $"{SinTitulo} {numero}";

        var delante = $"{numero}. ";
        var cabe = LargoMaximo - delante.Length;
        return delante + (limpio.Length > cabe ? limpio[..cabe].TrimEnd() : limpio);
    }

    /// <summary>Quita lo que Excel no admite y los espacios de los extremos.</summary>
    /// <param name="titulo">El título tal como llega; nulo cuenta como vacío.</param>
    /// <returns>El título con cada signo prohibido cambiado por un guion, no borrado, para que la longitud no engañe.</returns>
    private static string Limpiar(string? titulo)
        => new string((titulo ?? string.Empty)
            .Select(letra => LoQueExcelNoAdmite.Contains(letra) ? '-' : letra)
            .ToArray())
            .Trim();
}
