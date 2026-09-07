namespace Fichas.App.Revisar;

/// <summary>
/// Una rama del árbol de carpetas de Revisar, sea de mes, de fecha de viaje o de unidad.
/// </summary>
/// <remarks>
/// Las tres ramas se comportan igual al pulsarlas —enseñan sus documentos a la derecha— y
/// las tres se pueden volcar, porque volcar es siempre de un mes entero: de una rama de
/// fecha o de unidad se vuelca el mes al que pertenece. Por eso <see cref="Mes"/> va en
/// todas y no solo en la de mes.
/// </remarks>
/// <param name="Carpeta">Cómo se llama, sin la cifra: «Grupo del 17 de septiembre».</param>
/// <param name="Documentos">Los documentos que cuelgan de esta rama, ya ordenados.</param>
/// <param name="Mes">El mes al que pertenece; es lo que se vuelca al disco.</param>
public sealed record CarpetaDelArbol(
    string Carpeta,
    IReadOnlyList<TarjetaDeDocumento> Documentos,
    GrupoDeMes Mes);
