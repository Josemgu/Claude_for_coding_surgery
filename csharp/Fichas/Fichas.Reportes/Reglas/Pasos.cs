using Fichas.Contratos.Modelos;

namespace Fichas.Reportes.Reglas;

/// <summary>
/// Los seis pasos de «Preparación para las ordenanzas» del sistema del lider.
/// </summary>
/// <remarks>
/// Portado de <c>datos/pasos.py</c>, que a su vez viene del proyecto viejo
/// (<c>salida/asignacion.py</c>, <c>PASOS</c>).
///
/// <b>Que son y que NO son.</b> Son los seis pasos que salen en la pantalla de esa persona
/// dentro del sistema del lider. NO son las seis ordenanzas del formulario (las <c>Ord*</c>
/// de <see cref="Persona"/>): aquellas dicen a QUE va la persona al templo, y estas dicen si
/// esta en condiciones de ir.
///
/// ⚠️ <b>Sin contestar NO es «No».</b> Las tres respuestas son si, no y nada, y la tercera es
/// la que importa: una casilla en blanco es una pregunta que nadie miro, y esa persona queda a
/// medias, no reprobada. Por eso <see cref="Estado"/> devuelve tres valores y no dos.
/// </remarks>
public static class Pasos
{
    /// <summary>Los seis pasos con su rotulo, en el orden de la pantalla del lider.</summary>
    /// <remarks>El orden no se altera: el companero los copia de arriba abajo sin ir y venir.</remarks>
    private static readonly (Func<Persona, bool?> Leer, string Rotulo)[] LosSeis =
    [
        (p => p.PasoPreparacion, "1. Preparación"),
        (p => p.PasoInformacion, "2. Información"),
        (p => p.PasoCitaDelTemplo, "3. Cita del templo"),
        (p => p.PasoAccionesRequeridas, "4. Acciones requeridas"),
        (p => p.PasoEntrevistas, "5. Entrevistas"),
        (p => p.PasoListoParaElTemplo, "6. Listo para el templo"),
    ];

    /// <summary>Si esa persona esta lista: si, no, o nulo si todavia no se sabe.</summary>
    /// <remarks>
    /// Lista es tener los seis. Si alguno esta marcado que no, no lo esta. Y si alguno se quedo
    /// en blanco NO SE SABE, que es distinto de las otras dos: dar por lista a una persona de
    /// la que faltan preguntas por mirar es exactamente lo que manda a alguien al templo con la
    /// recomendacion mal.
    /// </remarks>
    /// <param name="persona">La persona con sus seis casillas <c>Paso*</c>.</param>
    /// <returns>Verdadero con los seis en sí; falso con alguno en no; nulo si ninguno es no y alguno está en blanco.</returns>
    public static bool? Estado(Persona persona)
    {
        var valores = LosSeis.Select(paso => paso.Leer(persona)).ToList();
        if (valores.Any(v => v == false)) return false;
        if (valores.All(v => v == true)) return true;
        return null;
    }

    /// <summary>Los rotulos de los pasos marcados que NO, sin el numero de delante.</summary>
    /// <remarks>
    /// Es lo que hay que decirle al lider cuando se le llama: el nombre del paso donde se quedo
    /// vale mas que cualquier nota escrita a mano.
    /// </remarks>
    /// <param name="persona">La persona con sus seis casillas <c>Paso*</c>.</param>
    /// <returns>Los rótulos en el orden de la pantalla del líder; vacía si ninguno está en no. Los que están en blanco no salen.</returns>
    public static IReadOnlyList<string> SinCompletar(Persona persona)
        => LosSeis
            .Where(paso => paso.Leer(persona) == false)
            .Select(paso => SinElNumero(paso.Rotulo))
            .ToList();

    /// <summary>Los rotulos de los pasos que NO dicen que si —en no o en blanco—, sin el numero de delante.</summary>
    /// <remarks>
    /// <para>Entro el 2026-09-16 para la persona A MEDIAS: <i>«las personas que se han completado,
    /// por ejemplo 4 preguntas de las 6, deben pasar a color naranja e indicar que le falta»</i>.
    /// <see cref="Estado"/> contesta si/no/nada y <see cref="SinCompletar"/> solo los que dicen no;
    /// ninguna de las dos dice CUANTAS van en si, y es lo que hace falta para saber si se avanzo.</para>
    ///
    /// <para>⛔ <b>No cambia la regla:</b> <see cref="Estado"/> sigue igual. Esto es una proyeccion
    /// de las mismas seis casillas, y vive aqui porque aqui es donde estan las seis, y no en una
    /// segunda lista que un dia se separe de esta.</para>
    /// </remarks>
    /// <param name="persona">La persona con sus seis casillas <c>Paso*</c>.</param>
    /// <returns>Los rótulos en el orden de la pantalla del líder; vacía con las seis en sí.</returns>
    public static IReadOnlyList<string> LasQueNoDicenSi(Persona persona)
        => LosSeis
            .Where(paso => paso.Leer(persona) != true)
            .Select(paso => SinElNumero(paso.Rotulo))
            .ToList();

    /// <summary>«5. Entrevistas» → «Entrevistas».</summary>
    /// <param name="rotulo">El rótulo con su número delante.</param>
    private static string SinElNumero(string rotulo)
        => rotulo[(rotulo.IndexOf(". ", StringComparison.Ordinal) + 2)..];
}
