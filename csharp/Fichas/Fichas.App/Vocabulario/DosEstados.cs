namespace Fichas.App.Vocabulario;

/// <summary>Lo que el dueno lee de una cosa: o no le queda nada que hacer, o si.</summary>
/// <remarks>
/// ⚠️ <b>Esto NO es un estado de la base y no hay ninguna columna que lo guarde.</b> Es una
/// LECTURA que se compone al pintar, a partir de las cuatro columnas que siguen siendo cuatro.
/// El motivo esta en <see cref="DosEstados"/>.
/// </remarks>
public enum LoQueSeLee
{
    /// <summary>No queda nada que el dueno tenga que hacer con esto.</summary>
    Resuelto = 0,

    /// <summary>Si queda algo. Que, y a quien le toca, lo dice el detalle.</summary>
    MeFalta = 1,
}

/// <summary>
/// Las DOS palabras de estado que se leen en la pantalla, y no hay una tercera.
/// </summary>
/// <remarks>
/// <para><b>De donde sale.</b> Decision del dueno del 2026-09-07, despues de que el supervisor
/// le ensenara las cuatro palabras que el programa le ponia delante:
/// <i>«Dos estados nada mas: resuelto y me falta»</i>. Resuelto es que no queda nada que el
/// tenga que hacer con eso; me falta es que si.</para>
///
/// <para>⚠️⚠️ <b>EL LIMITE QUE NO SE CRUZA: en la base siguen siendo cuatro.</b> Lo que se
/// colapsa es lo que se le ENSENA, no lo que se guarda, y el motivo no es purismo. Las cuatro
/// las escribe gente distinta:</para>
///
/// <list type="table">
///   <listheader><term>Lo que se guarda</term><description>Quien lo escribe</description></listheader>
///   <item><term><c>procedencia_campo.verificado</c></term>
///     <description>Miguel, campo por campo, y nunca automatico (regla permanente 5).</description></item>
///   <item><term><c>casos.estado_recomendacion</c> + <c>estado_marcado_por</c> + <c>estado_marcado_origen</c></term>
///     <description>El Excel del companero, con su nombre; o la mano de Miguel en Revisar.</description></item>
///   <item><term>Las seis preguntas de <c>Fichas.Reportes.Reglas.Pasos</c></term>
///     <description>Quien las conteste en el sistema del lider.</description></item>
///   <item><term><c>casos.archivado</c> + <c>fecha_archivado</c></term>
///     <description>El dueno, cuando cierra un documento.</description></item>
/// </list>
///
/// <para>Esas columnas dicen <b>quien dijo cada cosa</b>, y esa es la unica defensa del
/// proyecto el dia que alguien llegue al templo y no pueda entrar. Fundirlas en una dejaria de
/// poder saberse si aquello lo dio por bueno Sandy, lo dio por bueno Miguel, o lo dedujo el
/// programa. Por eso <see cref="LoQueSeLeeDeUnDocumento"/> devuelve SIEMPRE la palabra y un
/// <c>Detalle</c> que nombra la fuente, y por eso ninguna escritura pasa por aqui: en este
/// archivo no hay ni una llamada a un puerto.</para>
///
/// <para><b>Y la otra mitad de la decision:</b> «me falta» siempre puede decir que falta y a
/// quien le toca, <b>y lo dice cuando el lo pide</b>, no de entrada. Una cifra a la vista, el
/// detalle a un clic; el ya rechazo por escrito los avisos que ocupan media pantalla.</para>
/// </remarks>
public static class DosEstados
{
    /// <summary>La palabra de lo que no le deja nada que hacer.</summary>
    public const string Resuelto = "resuelto";

    /// <summary>La palabra de lo que si.</summary>
    public const string MeFalta = "me falta";

    /// <summary>La misma palabra con mayuscula, para cuando encabeza un recuadro o una columna.</summary>
    public const string ResueltoEnCabecera = "Resuelto";

    /// <summary>La misma, con mayuscula.</summary>
    public const string MeFaltaEnCabecera = "Me falta";

    /// <summary>Las dos palabras de estado que puede haber en la pantalla, y ninguna mas.</summary>
    /// <remarks>
    /// Se expone como lista para que una prueba pueda recorrer una pantalla y comprobar que no
    /// se lee ninguna otra, sin tener que repetir las dos cadenas en cada sitio.
    /// </remarks>
    public static IReadOnlyList<string> LasDos { get; } = [Resuelto, MeFalta];

    /// <summary>La palabra de una lectura.</summary>
    /// <param name="lo">La lectura que se traduce a una de las dos palabras.</param>
    public static string Palabra(LoQueSeLee lo) => lo == LoQueSeLee.Resuelto ? Resuelto : MeFalta;

    /// <summary>La misma, para una cabecera.</summary>
    /// <param name="lo">La lectura que se traduce a una de las dos palabras.</param>
    public static string PalabraEnCabecera(LoQueSeLee lo)
        => lo == LoQueSeLee.Resuelto ? ResueltoEnCabecera : MeFaltaEnCabecera;

    /// <summary>
    /// Como se dice un CONJUNTO: «resuelto», o «me falta 6 de 10».
    /// </summary>
    /// <remarks>
    /// <para>La palabra sigue siendo una de las dos y la cifra viaja detras, que es lo que el
    /// dueno pidio: una cifra a la vista. Antes esta misma linea decia <i>«4 de 10 personas
    /// confirmadas»</i>, y «confirmadas» es una de las cuatro palabras que se retiran.</para>
    ///
    /// <para>⚠️ <b>Cero de cero NO es resuelto y tampoco lleva cifra.</b> Nadie ha dicho nada
    /// de nadie: no hay a quien confirmar. Decir «resuelto» seria afirmar algo que nadie
    /// afirmo, y «me falta 0 de 0» es la cifra que el dueno vio pintada en rojo y que no dice
    /// nada. Lo que hay que hacer con ese conjunto va en su detalle, y su color deja de ser el
    /// de la alarma.</para>
    /// </remarks>
    /// <param name="resueltos">Cuantos de ellos estan resueltos.</param>
    /// <param name="total">Cuantos son.</param>
    public static string Cuenta(int resueltos, int total)
    {
        if (total <= 0) return MeFalta;
        return resueltos >= total ? Resuelto : $"{MeFalta} {total - resueltos} de {total}";
    }
}
