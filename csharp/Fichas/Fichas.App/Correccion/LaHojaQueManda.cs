using Fichas.Contratos.Lectura;

namespace Fichas.App.Correccion;

/// <summary>
/// Qué hoja se enseña y qué banda se ilumina cuando un campo toma el foco: la hoja que el
/// dueño eligió a mano MANDA, y la banda de un campo de otra hoja no se pinta sobre ella.
/// </summary>
/// <remarks>
/// <para><b>Existe por el defecto del 2026-09-15</b>, con las palabras del dueño: <i>«al pasar a
/// la segunda hoja y comenzar a escribir los datos, como el nombre, la cédula y eso, el
/// documento salta de manera automática a la primera hoja de la factura»</i>. Hasta ese día,
/// <c>PaginaDeCorreccion.AlEnfocarUnCampo</c> cambiaba de hoja a la del campo en cada foco, y un
/// campo que la importación dejó en la hoja 1 —o una persona añadida a mano, que heredaba la
/// hoja del caso— devolvía el visor a la factura cada vez que se tocaba. Medido con la ventana
/// abierta sobre master: <c>2 / 2</c> → enfocar «Unidad» → <c>1 / 2</c>.</para>
///
/// <para><b>La regla:</b> enfocar nunca cambia de hoja. Si el campo se leyó en la hoja que está
/// delante —o no se sabe de cuál salió—, se ilumina su banda; si se leyó en otra, no se ilumina
/// nada, porque esa banda señalaría un sitio de otra hoja. Ir a la hoja donde se leyó sigue
/// existiendo como acción explícita —el rótulo del papel en cada ficha—, y nunca sola.</para>
///
/// <para>Se pierde a propósito lo que el planificador trajo de Rossum para los formularios de
/// grupo —tabular a alguien de la cuarta hoja enseñaba la cuarta—: entre «el visor me sigue» y
/// «el visor me obedece», el dueño pidió lo segundo. Queda dicho en la entrega.</para>
/// </remarks>
public static class LaHojaQueManda
{
    /// <summary>La hoja que se enseña al enfocar un campo: la que está delante, siempre.</summary>
    /// <param name="campo">El campo que tomó el foco; de él no se toma nada, y está en la firma para que la regla se lea entera.</param>
    /// <param name="hojaDelante">La hoja que el visor enseña ahora, base 1.</param>
    /// <returns>La misma <paramref name="hojaDelante"/>.</returns>
    /// <exception cref="ArgumentNullException">Si el campo es nulo.</exception>
    public static int HojaQueSeEnsenaAlEnfocar(CampoEnPantalla campo, int hojaDelante)
    {
        ArgumentNullException.ThrowIfNull(campo);
        return hojaDelante;
    }

    /// <summary>La banda que se ilumina al enfocar: la del campo si es de la hoja de delante; ninguna si es de otra.</summary>
    /// <param name="campo">El campo que tomó el foco.</param>
    /// <param name="hojaDelante">La hoja que el visor enseña ahora, base 1.</param>
    /// <returns>Su banda, o nula si no la tiene o si se leyó en otra hoja.</returns>
    /// <exception cref="ArgumentNullException">Si el campo es nulo.</exception>
    public static BandaDeLaPagina? BandaQueSeIlumina(CampoEnPantalla campo, int hojaDelante)
        => SeLeyoEnOtraHoja(campo, hojaDelante) ? null : campo.Banda;

    /// <summary>La hoja a la que lleva la acción explícita de «ver dónde se leyó».</summary>
    /// <param name="campo">El campo del que se quiere ver el papel.</param>
    /// <param name="hojaDelante">La hoja que el visor enseña ahora; es la respuesta cuando el campo no sabe la suya.</param>
    /// <returns>La hoja del campo, o la de delante si no se sabe.</returns>
    /// <exception cref="ArgumentNullException">Si el campo es nulo.</exception>
    public static int HojaDondeSeLeyo(CampoEnPantalla campo, int hojaDelante)
    {
        ArgumentNullException.ThrowIfNull(campo);
        return campo.PaginaPdf ?? hojaDelante;
    }

    /// <summary>Si el campo se leyó en una hoja distinta de la que está delante. Sin hoja conocida, no.</summary>
    /// <param name="campo">El campo que se pregunta.</param>
    /// <param name="hojaDelante">La hoja que el visor enseña ahora, base 1.</param>
    /// <exception cref="ArgumentNullException">Si el campo es nulo.</exception>
    public static bool SeLeyoEnOtraHoja(CampoEnPantalla campo, int hojaDelante)
    {
        ArgumentNullException.ThrowIfNull(campo);
        return campo.PaginaPdf is int suya && suya != hojaDelante;
    }
}
