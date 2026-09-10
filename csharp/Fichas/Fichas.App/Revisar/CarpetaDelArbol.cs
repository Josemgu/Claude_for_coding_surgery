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
/// <param name="Clave">Cómo se la reconoce al rehacer el árbol; la compone <see cref="MemoriaDelSitio"/>.</param>
/// <param name="Carpeta">Cómo se llama, sin la cifra: «Grupo del 17 de septiembre».</param>
/// <param name="Documentos">Los documentos que cuelgan de esta rama, ya ordenados.</param>
/// <param name="Mes">El mes al que pertenece; es lo que se vuelca al disco.</param>
/// <remarks>
/// ⛔ <b><see cref="Clave"/> no es <see cref="Carpeta"/>.</b> El árbol se rehace entero en cada
/// repintado —las cifras de las etiquetas cambian— y los objetos son nuevos, así que para
/// volver a la carpeta donde él estaba hace falta algo que se pueda comparar entre dos
/// árboles. El nombre no sirve: el mes de lo que no tiene fecha y su carpeta de fecha se
/// llaman los dos «Sin fecha de viaje».
/// </remarks>
public sealed record CarpetaDelArbol(
    string Clave,
    string Carpeta,
    IReadOnlyList<TarjetaDeDocumento> Documentos,
    GrupoDeMes Mes);
