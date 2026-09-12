using Fichas.Contratos.Modelos;

namespace Fichas.App.Importar;

/// <summary>
/// Convierte un fallo que se escapo de un manejador en una linea que el dueno ve.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Ningun manejador de esta pantalla se traga una excepcion en silencio.</b> La regla
/// sale de un fallo medido el 2026-09-04 sobre el paquete publicado: se pulsaba «Elegir
/// archivos…» y no pasaba NADA —sin selector, sin error, sin mensaje y sin una linea en
/// <c>fichas.log</c>—. Un boton que no hace nada y no dice nada es lo peor que puede tener
/// este programa: quien lo pulsa no sabe si esta trabajando, si se rompio o si el pulso no
/// llego, y no tiene con que preguntarlo.
/// </para>
/// <para>
/// Esto NO contradice el requisito 9 («avisar, nunca impedir»): justamente lo cumple. El
/// fallo no tumba la pantalla ni abre un cuadro modal; se queda en una linea de la franja,
/// y el programa sigue.
/// </para>
/// </remarks>
public static class AvisoDeUnFalloEnPantalla
{
    /// <summary>Lo que cabe en el renglon de la franja de avisos.</summary>
    /// <remarks>
    /// La franja pinta <see cref="Aviso.Linea"/> en UN renglon (requisito 4 del dueno, «ni
    /// un parrafo en pantalla»). Un mensaje de WinRT o de SQLite puede traer cientos de
    /// caracteres y saltos dentro; lo largo va al detalle, que se abre con «ver».
    /// </remarks>
    public const int LargoMaximoDeLaLinea = 160;

    /// <summary>El aviso de una linea que se deja en la franja cuando algo falla.</summary>
    /// <param name="accion">Que se estaba haciendo, tal como se lee en el boton.</param>
    /// <param name="fallo">Lo que se escapo del manejador.</param>
    public static Aviso Describir(string accion, Exception fallo)
    {
        ArgumentNullException.ThrowIfNull(fallo);

        var causa = fallo.InnerException is null
            ? string.Empty
            : $" (por debajo: {EnUnRenglon(fallo.InnerException.Message)})";

        var linea = Recortar(
            $"«{accion}» no se pudo hacer: {EnUnRenglon(fallo.Message)}{causa}");

        return Aviso.Problema(linea, string.Empty, DetalleDe(accion, fallo));
    }

    /// <summary>La misma cosa para <c>fichas.log</c>, en un solo renglon.</summary>
    /// <remarks>
    /// Va aparte del aviso y sin recortar: el cuaderno es lo que se lee despues, cuando
    /// la franja ya se cerro. Se lee renglon a renglon, asi que una entrada con saltos
    /// dentro se mezclaria con las de al lado y dejaria de poderse buscar.
    /// </remarks>
    /// <param name="accion">Qué se estaba haciendo, tal como se lee en el botón.</param>
    /// <param name="fallo">Lo que se escapó del manejador; va entero, con su traza.</param>
    /// <returns>Un renglón que empieza por «FALLO EN PANTALLA» y no lleva saltos de línea.</returns>
    public static string LineaParaElCuaderno(string accion, Exception fallo)
    {
        ArgumentNullException.ThrowIfNull(fallo);

        return $"FALLO EN PANTALLA  «{accion}»  {EnUnRenglon(fallo.ToString())}";
    }

    /// <summary>Lo largo, que solo se ve al pulsar «ver»: tipo, mensaje y traza enteros.</summary>
    /// <remarks>
    /// Sin la traza no hay forma de saber en que linea se rompio, y entonces el aviso
    /// sirve para enterarse pero no para arreglarlo.
    /// </remarks>
    /// <param name="accion">Qué se estaba haciendo, tal como se lee en el botón.</param>
    /// <param name="fallo">Lo que se escapó del manejador.</param>
    private static string DetalleDe(string accion, Exception fallo)
        => $"Acción: {accion}{Environment.NewLine}{Environment.NewLine}{fallo}";

    /// <summary>Aplana saltos, retornos y tabuladores, y junta los espacios de sobra.</summary>
    /// <param name="texto">El texto tal como vino; nulo o vacío devuelve vacío.</param>
    private static string EnUnRenglon(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;

        var plano = new System.Text.StringBuilder(texto.Length);
        var veniaUnEspacio = false;
        foreach (var letra in texto)
        {
            var esEspacio = char.IsWhiteSpace(letra);
            if (esEspacio)
            {
                if (!veniaUnEspacio && plano.Length > 0) plano.Append(' ');
            }
            else
            {
                plano.Append(letra);
            }

            veniaUnEspacio = esEspacio;
        }

        return plano.ToString().TrimEnd();
    }

    /// <summary>Deja la linea dentro del renglon, marcando con «…» que se corto.</summary>
    /// <remarks>
    /// Publico porque el renglon de la franja es UNO para todo el programa (requisito 4 del
    /// dueño): quien componga otra linea larga —los avisos de la importacion, por ejemplo—
    /// la recorta con este mismo, y no con una copia que se quede vieja.
    /// </remarks>
    /// <param name="linea">La línea entera, ya en un solo renglón.</param>
    /// <returns>La misma línea si cabe; si no, sus primeros 159 caracteres y «…».</returns>
    public static string Recortar(string linea)
        => linea.Length <= LargoMaximoDeLaLinea
            ? linea
            : string.Concat(linea.AsSpan(0, LargoMaximoDeLaLinea - 1), "…");
}
