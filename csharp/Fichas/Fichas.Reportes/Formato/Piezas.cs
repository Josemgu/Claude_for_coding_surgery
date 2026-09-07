namespace Fichas.Reportes.Formato;

/// <summary>
/// Una linea ya colocada: si va en negrita, de que tamano, y sus trozos de texto.
/// </summary>
/// <remarks>
/// Una linea de tabla tiene un trazo por columna; una de parrafo, uno solo. Una linea sin
/// trazos ocupa alto y no pinta: es el hueco entre bloques.
/// </remarks>
/// <param name="Negrita">Si se escribe con Helvetica-Bold.</param>
/// <param name="Tamano">El tamano de la letra en puntos.</param>
/// <param name="Trazos">Los trozos de texto con la x donde empieza cada uno.</param>
/// <param name="Repetir">Si esta linea se vuelve a dibujar arriba de cada pagina nueva.</param>
public sealed record Linea(bool Negrita, int Tamano, IReadOnlyList<Trazo> Trazos, bool Repetir);

/// <summary>Un trozo de texto dentro de una linea, con la x donde empieza.</summary>
/// <remarks>
/// El color va en el TRAZO y no en la linea porque lo que se pinta de rojo en este informe
/// es UNA celda de una fila —la columna «Sin la preparación completa» del caso que salio
/// mal— y no el renglon entero.
/// </remarks>
/// <param name="X">Puntos desde el borde izquierdo de la pagina.</param>
/// <param name="Texto">Lo que se escribe.</param>
/// <param name="Color">Un <c>#RRGGBB</c>, o nulo, que significa negro.</param>
public sealed record Trazo(int X, string Texto, string? Color = null);

/// <summary>Lo que se dibuja DETRAS del texto.</summary>
/// <remarks>
/// Son las dos unicas formas que hace falta pintar: la cinta negra de arriba y la raya fina
/// del pie. No hay curvas, ni imagenes, ni transparencias, ni degradados.
/// </remarks>
public abstract record Adorno;

/// <summary>Un rectangulo relleno; la cinta negra de la cabecera.</summary>
/// <param name="X">Borde izquierdo en puntos.</param>
/// <param name="Y">Borde inferior en puntos, contando desde abajo, como el PDF.</param>
/// <param name="Ancho">Ancho en puntos.</param>
/// <param name="Alto">Alto en puntos.</param>
/// <param name="Color">Un <c>#RRGGBB</c>.</param>
public sealed record Rectangulo(int X, int Y, int Ancho, int Alto, string Color) : Adorno;

/// <summary>Una raya recta horizontal; la del pie.</summary>
/// <param name="X">Donde empieza.</param>
/// <param name="Y">A que altura, contando desde abajo.</param>
/// <param name="HastaX">Donde termina.</param>
/// <param name="Color">Un <c>#RRGGBB</c>.</param>
/// <param name="Grosor">El grosor del trazo en puntos.</param>
public sealed record Raya(int X, int Y, int HastaX, string Color, double Grosor) : Adorno;

/// <summary>
/// Lo que se repite en todas las paginas, dado como funcion porque el pie lleva el numero.
/// </summary>
/// <param name="numero">La pagina que se esta dibujando, base 1.</param>
/// <param name="total">Cuantas paginas tiene el documento entero.</param>
public delegate (IReadOnlyList<Adorno> Adornos, IReadOnlyList<(double Y, Linea Linea)> Lineas) MarcoDePagina(
    int numero, int total);
