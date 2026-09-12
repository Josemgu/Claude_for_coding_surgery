using Fichas.Contratos.Modelos;

namespace Fichas.Reportes.Reglas;

/// <summary>
/// Como se llaman las seis casillas de ordenanzas, y cuantas personas van a cada una.
/// </summary>
/// <remarks>
/// Portado de <c>datos/ordenanzas.py</c>. Las seis <c>Ord*</c> de <see cref="Persona"/> dicen a
/// QUE va cada persona al templo. Los nombres de columna sirven para el motor y no para una
/// persona: «A qué va: OrdSellamientoEsposos» no se puede poner en un informe que leen los jefes.
///
/// ⚠️ <b>Un nulo no es un «no».</b> Una casilla sin leer y una casilla leida y sin marcar son
/// cosas distintas: la primera dice que nadie sabe, la segunda que esa persona no va a eso.
/// <see cref="Contar"/> solo cuenta las MARCADAS.
///
/// El orden es el literal de las columnas <c>ord_*</c>, y no se altera.
/// </remarks>
public static class Ordenanzas
{
    /// <summary>Las seis casillas con su rotulo, en el orden de las columnas.</summary>
    private static readonly (Func<Persona, bool?> Leer, string Rotulo)[] LasSeis =
    [
        (p => p.OrdRecibirPropias, "Recibir ordenanzas propias"),
        (p => p.OrdObservarSellamiento, "Observar ordenanza de sellamiento"),
        (p => p.OrdTraductor, "Traductor"),
        (p => p.OrdInvestidura, "Investidura"),
        (p => p.OrdSellamientoEsposos, "Sellamiento esposa a esposo"),
        (p => p.OrdSellamientoHijoPadres, "Sellamiento hijo a padres"),
    ];

    /// <summary>Si esa persona no trae NINGUNA casilla marcada.</summary>
    /// <remarks>
    /// Las casillas del formulario son marcas de tilde y el reconocimiento no las lee solo: sin
    /// este numero, una tabla corta se lee como «casi nadie va a nada».
    /// </remarks>
    /// <param name="persona">La persona con sus seis casillas <c>Ord*</c>; una nula cuenta como no marcada.</param>
    public static bool SinNingunaMarcada(Persona persona)
        => LasSeis.All(casilla => casilla.Leer(persona) != true);

    /// <summary>Cuantas personas van a cada ordenanza. Solo salen las que tienen alguien.</summary>
    /// <remarks>
    /// Ordenado de mas a menos, y a igualdad por el ORDEN DEL FORMULARIO: dos ordenanzas con el
    /// mismo numero de personas tienen que salir siempre en el mismo sitio, o dos informes del
    /// mismo periodo pareceran distintos.
    /// </remarks>
    /// <param name="personas">Las personas que se cuentan; se recorre una vez por casilla.</param>
    /// <returns>Rótulo y cuántas por cada ordenanza con alguien; vacía si nadie tiene ninguna marcada.</returns>
    public static IReadOnlyList<(string Rotulo, int Personas)> Contar(IEnumerable<Persona> personas)
    {
        var lista = personas as IReadOnlyList<Persona> ?? personas.ToList();

        return LasSeis
            .Select((casilla, orden) => (
                casilla.Rotulo,
                Personas: lista.Count(p => casilla.Leer(p) == true),
                Orden: orden))
            .Where(cuenta => cuenta.Personas > 0)
            .OrderByDescending(cuenta => cuenta.Personas)
            .ThenBy(cuenta => cuenta.Orden)
            .Select(cuenta => (cuenta.Rotulo, cuenta.Personas))
            .ToList();
    }
}
