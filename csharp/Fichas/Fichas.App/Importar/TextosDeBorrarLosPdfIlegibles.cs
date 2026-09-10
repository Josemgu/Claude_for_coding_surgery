using Fichas.Contratos.Puertos;

namespace Fichas.App.Importar;

/// <summary>
/// Lo que se lee al borrar los PDF que no se pudieron leer: el título, el botón y la
/// pregunta.
/// </summary>
/// <remarks>
/// <para>Va aparte de la operación —y no dentro de ella— para que se pueda medir sin abrir
/// ninguna ventana. Lo que hay que poder comprobar en una prueba es el TEXTO que el dueño
/// va a leer antes de contestar, y eso no necesita un <c>XamlRoot</c>.</para>
///
/// <para>⚠️ <b>Por qué el título no se le pide al plan.</b>
/// <see cref="PlanDeBorrado.Titulo"/> y <see cref="PlanDeBorrado.TextoDelBoton"/> cuentan
/// la tabla <c>casos</c>, que en este borrado no entra: dirían «Borrar 0 documentos» con
/// tres renglones marcados. No se arregla allí porque <c>IMantenimiento.cs</c> —donde vive
/// ese registro, y también <c>AlcanceDelBorrado</c>— sigue congelado
/// (<c>.claude/congelados.txt</c>); de <c>Fichas.Contratos</c> solo está abierto
/// <c>IIlegibles.cs</c>. Lo que sí se le pide al plan es todo lo demás: los conteos, la
/// ruta de la copia y el cuerpo de la pregunta.</para>
/// </remarks>
public static class TextosDeBorrarLosPdfIlegibles
{
    /// <summary>
    /// Qué se borra y qué NO. Se dice en la pantalla y otra vez dentro de la pregunta.
    /// </summary>
    /// <remarks>
    /// ⛔ Es la frase que evita el malentendido caro: el dueño marca un renglón que nombra
    /// un archivo suyo, pulsa «borrar», y podría creer que le quitaron el escaneo. Lo que
    /// cae es la anotación de que ese archivo no se pudo leer. Va DOS veces —en la zona,
    /// antes de marcar nada, y en el cuadro, antes de contestar— porque quien solo lee una
    /// de las dos tiene que enterarse igual.
    /// </remarks>
    public const string LoQueNoSeBorra =
        "Los archivos PDF NO se borran: siguen en su carpeta, tal como están. Lo que se "
        + "borra es la anotación de que no se pudieron leer. Si vuelve a importar esos "
        + "PDF, volverán a aparecer aquí.";

    /// <summary>El título del cuadro: la acción CON su cifra delante, nunca un «¿seguro?».</summary>
    /// <remarks>
    /// Del pase del dueño: «una confirmación con el número delante». Un «¿seguro?» se
    /// contesta que sí sin leerlo; «Borrar 3 renglones de PDF que no se pudieron leer», no.
    /// </remarks>
    public static string Titulo(PlanDeBorrado plan) => "Borrar " + Cuenta(plan);

    /// <summary>Lo que va en el botón que borra; también con la cifra delante.</summary>
    public static string TextoDelBoton(PlanDeBorrado plan) => "Borrar " + Cuenta(plan);

    /// <summary>
    /// El cuerpo de la pregunta: lo que cae, dónde quedó la copia, que no hay vuelta atrás
    /// y que el PDF del disco se queda.
    /// </summary>
    /// <remarks>
    /// Las tres primeras las escribe el propio plan —son las mismas que en cualquier otro
    /// borrado del programa, y repetirlas aquí sería un segundo sitio donde alguien puede
    /// olvidarse de nombrar la copia—. Esta clase solo añade la cuarta.
    /// </remarks>
    public static string Pregunta(PlanDeBorrado plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return string.Join(
            Environment.NewLine,
            plan.Pregunta,
            string.Empty,
            LoQueNoSeBorra);
    }

    /// <summary>«3 renglones de PDF que no se pudieron leer», tal como los contó el plan.</summary>
    private static string Cuenta(PlanDeBorrado plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var renglones = plan.Conteos.FirstOrDefault(
            conteo => string.Equals(conteo.Tabla, IIlegibles.TablaDeLosRenglones, StringComparison.Ordinal));

        return renglones?.Dicho ?? "0 renglones de PDF que no se pudieron leer";
    }
}
