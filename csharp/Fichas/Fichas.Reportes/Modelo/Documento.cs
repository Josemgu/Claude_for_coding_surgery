namespace Fichas.Reportes.Modelo;

/// <summary>
/// Las tres clases de columna. <see cref="Texto"/> no es cosmetica.
/// </summary>
/// <remarks>
/// Es la unica defensa contra que Excel se coma el cero de delante de un MRN como
/// '055-1111-3853', y un MRN sin sus ceros deja de identificar a nadie. El PDF no la
/// necesita —escribe cadenas—, pero el vocabulario es el mismo que usara el Excel del
/// mismo periodo, y dos vocabularios para lo mismo se separan solos.
/// </remarks>
public enum ClaseDeColumna
{
    /// <summary>Se escribe como texto pase lo que pase: MRN, numero de caso, unidad.</summary>
    Texto = 0,

    /// <summary>Una fecha o una marca de tiempo.</summary>
    Temporal = 1,

    /// <summary>Cualquier otra cosa: nombres, motivos, rotulos.</summary>
    Crudo = 2,
}

/// <summary>
/// Cuanto pesa una cifra. Es un TONO, no un color.
/// </summary>
/// <remarks>
/// <see cref="Malo"/> no es «rojo»: es «esto salio mal», y cada formato decide como se ve.
/// El PDF lo pinta de rojo; un <c>.xlsx</c> del mismo periodo sale sin colores a proposito,
/// porque alguien lo va a imprimir en blanco y negro y un color no es un dato.
/// </remarks>
public enum TonoDeCifra
{
    /// <summary>Ni bueno ni malo: un tamano, un total.</summary>
    Neutro = 0,

    /// <summary>Esto salio bien.</summary>
    Bueno = 1,

    /// <summary>Esto salio mal.</summary>
    Malo = 2,
}

/// <summary>Una columna de una tabla del informe.</summary>
/// <param name="Nombre">El rotulo impreso, en espanol.</param>
/// <param name="Clase">Como se escribe lo que lleva dentro.</param>
/// <param name="Ancho">El peso relativo de la columna; el reparto es proporcional, no fijo.</param>
public sealed record Columna(string Nombre, ClaseDeColumna Clase, int Ancho);

/// <summary>Una seccion del informe: su titulo, sus notas, su tabla y su resumen.</summary>
/// <param name="Titulo">Como se llama la seccion.</param>
/// <param name="Notas">Los parrafos cortos que explican como se cuenta lo de la tabla.</param>
/// <param name="Columnas">Los titulos de columna, que se repiten en cada pagina nueva.</param>
/// <param name="Filas">Las filas; un nulo en una celda se escribe como hueco, no como «None».</param>
/// <param name="Resumen">La linea en negrita de debajo de la tabla; nula si no lleva.</param>
public sealed record Seccion(
    string Titulo,
    IReadOnlyList<string> Notas,
    IReadOnlyList<Columna> Columnas,
    IReadOnlyList<IReadOnlyList<string?>> Filas,
    string? Resumen);

/// <summary>Una de las cifras grandes de la portada.</summary>
/// <param name="Numero">El numero, que se ve desde el otro lado de una mesa.</param>
/// <param name="Rotulo">Que cuenta ese numero, debajo y en pequeno.</param>
/// <param name="Tono">Si eso salio bien, mal, o ninguna de las dos.</param>
public sealed record Cifra(int Numero, string Rotulo, TonoDeCifra Tono);

/// <summary>Lo primero que se lee del informe.</summary>
/// <param name="Titular">La pregunta que se le hace al programa, escrita como pregunta.</param>
/// <param name="Frase">La respuesta en una frase. Nunca lleva porcentajes.</param>
/// <param name="Cifras">Las cuatro cifras grandes, en su orden.</param>
public sealed record Portada(string Titular, string Frase, IReadOnlyList<Cifra> Cifras);

/// <summary>El informe entero armado, todavia sin repartir en paginas ni escribir.</summary>
/// <param name="Titulo">El titulo del documento.</param>
/// <param name="Subtitulo">El periodo o el companero del que habla.</param>
/// <param name="GeneradoEn">La marca de tiempo con la que se genero; entra desde fuera.</param>
/// <param name="Portada">La portada.</param>
/// <param name="Avisos">De que no se fian los numeros de este informe.</param>
/// <param name="Secciones">Las secciones, en el orden en que se leen.</param>
public sealed record Documento(
    string Titulo,
    string Subtitulo,
    string GeneradoEn,
    Portada Portada,
    IReadOnlyList<string> Avisos,
    IReadOnlyList<Seccion> Secciones);
