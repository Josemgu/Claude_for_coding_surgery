namespace Fichas.Reportes.Reglas;

/// <summary>
/// Que una cifra de uno no escriba «1 personas».
/// </summary>
/// <remarks>
/// ⚠️ Es uno de los dos defectos que el pase manda arreglar al portar. En el Python esta en
/// <c>reportes/avisos.py:83</c> —el pase decia la linea 100, y el archivo tiene 97—, y no es
/// el unico sitio: los resumenes de casi todas las secciones tienen el mismo. Se arregla en
/// TODOS, no solo en el que el pase nombra, porque es el mismo defecto y arreglar uno deja
/// los otros pareciendo intencionados.
///
/// No hay reglas de plural aqui dentro: cada llamada dice sus dos formas escritas a mano. Un
/// pluralizador que las adivine acertaria con «persona» y fallaria con «unidad con algo
/// pendiente», y un informe que inventa una palabra en espanol es peor que uno repetitivo.
/// </remarks>
public static class Plural
{
    /// <summary>El numero con la forma que le toca, singular o plural.</summary>
    /// <param name="cuantos">La cifra.</param>
    /// <param name="singular">Como se escribe con uno; sin el numero delante.</param>
    /// <param name="plural">Como se escribe con cualquier otra cantidad; sin el numero delante.</param>
    public static string Con(int cuantos, string singular, string plural)
        => $"{cuantos} {(cuantos == 1 ? singular : plural)}";

    /// <summary>Solo la palabra, sin el numero delante.</summary>
    public static string Palabra(int cuantos, string singular, string plural)
        => cuantos == 1 ? singular : plural;
}
