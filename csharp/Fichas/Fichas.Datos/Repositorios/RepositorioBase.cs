using System.Globalization;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Microsoft.Data.Sqlite;

namespace Fichas.Datos.Repositorios;

/// <summary>
/// Lo que todos los repositorios hacen igual: consultar, contar, paginar y avisar.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ningun texto del usuario se concatena jamas en una instruccion</b>
/// (ARQUITECTURA §1.7). Toda consulta usa marcadores <c>$nombre</c> y pasa los valores
/// como parametros, de forma que el motor reciba la logica y los datos POR SEPARADO.
/// Un intento de inyeccion llega entonces como dato inerte, y hay una prueba que lo
/// guarda y lo relee literal.
/// </para>
/// <para>
/// <b>Requisito 9, «avisar, nunca impedir».</b> <see cref="Escribir"/> atrapa el fallo
/// del motor y lo devuelve como <see cref="Aviso"/> dentro de un
/// <see cref="ResultadoDeEscritura"/>. No lanza. Las excepciones quedan reservadas
/// para lo que ninguna pantalla puede prever —que la carpeta de datos no se resuelva,
/// que las claves foraneas no se enciendan, que una migracion quede a medias—, que es
/// otra cosa que un valor raro en una casilla.
/// </para>
/// </remarks>
public abstract class RepositorioBase
{
    /// <summary>Guarda la conexion sobre la que trabaja este repositorio.</summary>
    /// <param name="conexion">Una conexion ya abierta y con el esquema aplicado.</param>
    protected RepositorioBase(SqliteConnection conexion)
    {
        ArgumentNullException.ThrowIfNull(conexion);
        Conexion = conexion;
    }

    /// <summary>La conexion sobre la que trabaja este repositorio.</summary>
    protected SqliteConnection Conexion { get; }

    /// <summary>
    /// Escribe, y devuelve el resultado con sus avisos. Nunca lanza por el dato.
    /// </summary>
    /// <param name="instruccion">El SQL, con marcadores para todo valor.</param>
    /// <param name="ponerParametros">Lo que rellena esos marcadores.</param>
    /// <param name="avisos">Lo que la revision de formato ya tenia que decir.</param>
    /// <param name="idQueSeDevuelve">
    /// El id a devolver cuando la instruccion no crea fila nueva; 0 pide el
    /// <c>last_insert_rowid()</c>.
    /// </param>
    /// <returns>Bien con el id, o no escrito con el fallo del motor traducido y añadido a los avisos que ya venían.</returns>
    protected ResultadoDeEscritura Escribir(
        string instruccion,
        Action<SqliteCommand> ponerParametros,
        IReadOnlyList<Aviso> avisos,
        long idQueSeDevuelve = 0)
    {
        ArgumentNullException.ThrowIfNull(ponerParametros);
        ArgumentNullException.ThrowIfNull(avisos);

        try
        {
            using var orden = Conexion.CreateCommand();
            orden.CommandText = instruccion;
            ponerParametros(orden);

            if (idQueSeDevuelve != 0)
            {
                var filas = orden.ExecuteNonQuery();
                return filas == 0
                    ? ResultadoDeEscritura.NoSeEscribio(
                        Aviso.Problema(
                            "No se encontro la fila que se queria cambiar.",
                            detalle: $"Ninguna fila con el id {idQueSeDevuelve}. " +
                                     "Puede que otra ventana la haya cambiado."))
                    : new ResultadoDeEscritura(true, idQueSeDevuelve, avisos);
            }

            var nuevo = Convert.ToInt64(orden.ExecuteScalar(), CultureInfo.InvariantCulture);
            return new ResultadoDeEscritura(true, nuevo, avisos);
        }
        catch (SqliteException causa)
        {
            // El motor rechazo el dato. Se DICE, no se lanza: la pantalla tiene que
            // poder ensenarlo en la franja y seguir viva.
            var explicado = new List<Aviso>(avisos)
            {
                Aviso.Problema(
                    "No se pudo guardar. La base rechazo el dato.",
                    detalle: TraducirElFalloDelMotor(causa)),
            };
            return new ResultadoDeEscritura(false, 0, explicado);
        }
    }

    /// <summary>
    /// Trae un trozo de una lista con el total detras, para poder decir «N de M».
    /// </summary>
    /// <param name="consulta">El SELECT, SIN el LIMIT ni el OFFSET.</param>
    /// <param name="conteo">El SELECT COUNT(*) con el mismo filtro.</param>
    /// <param name="ponerParametros">Lo que rellena los marcadores de las dos.</param>
    /// <param name="leer">Como se convierte una fila en el objeto.</param>
    /// <param name="trozo">Que trozo se pide.</param>
    /// <typeparam name="T">El tipo del objeto que se lee de cada fila.</typeparam>
    /// <returns>El trozo pedido, ya saneado, con el total; vacío sin ejecutar el SELECT si el conteo da cero.</returns>
    protected PaginaDe<T> Paginar<T>(
        string consulta,
        string conteo,
        Action<SqliteCommand> ponerParametros,
        Func<SqliteDataReader, T> leer,
        Pagina trozo)
    {
        ArgumentNullException.ThrowIfNull(ponerParametros);
        ArgumentNullException.ThrowIfNull(leer);

        var trozoSano = Sanear(trozo);
        var total = ContarCon(conteo, ponerParametros);
        if (total == 0)
        {
            return PaginaDe<T>.Vacia(trozoSano);
        }

        using var orden = Conexion.CreateCommand();
        orden.CommandText = consulta + " LIMIT $tamano OFFSET $desde";
        ponerParametros(orden);
        orden.Parameters.AddWithValue("$tamano", trozoSano.Tamano);
        orden.Parameters.AddWithValue("$desde", trozoSano.Desde);

        using var lector = orden.ExecuteReader();
        var elementos = new List<T>();
        while (lector.Read())
        {
            elementos.Add(leer(lector));
        }

        return new PaginaDe<T>(elementos, trozoSano, total);
    }

    /// <summary>Cuenta cuantas filas cumplen un filtro, sin traerlas.</summary>
    /// <param name="conteo">Un <c>SELECT COUNT(*)</c> con marcadores.</param>
    /// <param name="ponerParametros">Lo que rellena esos marcadores.</param>
    /// <returns>La cifra que devuelve el motor.</returns>
    protected int ContarCon(string conteo, Action<SqliteCommand> ponerParametros)
    {
        ArgumentNullException.ThrowIfNull(ponerParametros);

        using var orden = Conexion.CreateCommand();
        orden.CommandText = conteo;
        ponerParametros(orden);
        return Convert.ToInt32(orden.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>Trae una fila por su id, o nulo si no esta.</summary>
    /// <typeparam name="T">El tipo del objeto que se lee.</typeparam>
    /// <param name="consulta">Un SELECT con el marcador <c>$id</c>.</param>
    /// <param name="id">El valor de <c>$id</c>.</param>
    /// <param name="leer">Cómo se convierte la fila en el objeto.</param>
    /// <returns>El objeto, o nulo si la consulta no devuelve filas.</returns>
    protected T? ObtenerUno<T>(string consulta, long id, Func<SqliteDataReader, T> leer)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(leer);

        using var orden = Conexion.CreateCommand();
        orden.CommandText = consulta;
        orden.Parameters.AddWithValue("$id", id);

        using var lector = orden.ExecuteReader();
        return lector.Read() ? leer(lector) : null;
    }

    /// <summary>Trae una lista entera, para las que se sabe que nunca son muchas.</summary>
    /// <typeparam name="T">El tipo del objeto que se lee de cada fila.</typeparam>
    /// <param name="consulta">El SELECT completo, sin LIMIT.</param>
    /// <param name="ponerParametros">Lo que rellena sus marcadores.</param>
    /// <param name="leer">Cómo se convierte cada fila en el objeto.</param>
    /// <returns>Todas las filas, en el orden del SELECT; vacía si no hay.</returns>
    protected IReadOnlyList<T> ListarTodo<T>(
        string consulta, Action<SqliteCommand> ponerParametros, Func<SqliteDataReader, T> leer)
    {
        ArgumentNullException.ThrowIfNull(ponerParametros);
        ArgumentNullException.ThrowIfNull(leer);

        using var orden = Conexion.CreateCommand();
        orden.CommandText = consulta;
        ponerParametros(orden);

        using var lector = orden.ExecuteReader();
        var elementos = new List<T>();
        while (lector.Read())
        {
            elementos.Add(leer(lector));
        }

        return elementos;
    }

    /// <summary>
    /// Deja el trozo pedido en algo que se pueda consultar.
    /// </summary>
    /// <remarks>
    /// Un tamano de 0 o negativo, o un «desde» negativo, no rechazan la consulta: se
    /// corrigen y se sigue (requisito 9). Un filtro que no se entiende se ignora, no
    /// tumba la pantalla.
    /// </remarks>
    /// <param name="trozo">Lo que pidió la pantalla, tal cual.</param>
    /// <returns>Desde 0 o más; tamaño 50 si venía 0 o negativo, y nunca más de 5000.</returns>
    protected static Pagina Sanear(Pagina trozo)
    {
        var desde = Math.Max(0, trozo.Desde);
        var tamano = trozo.Tamano <= 0 ? 50 : Math.Min(trozo.Tamano, 5000);
        return new Pagina(desde, tamano);
    }

    /// <summary>Lee un texto que puede venir nulo.</summary>
    /// <param name="lector">El lector posicionado en la fila.</param>
    /// <param name="columna">El índice de la columna.</param>
    protected static string? TextoONulo(SqliteDataReader lector, int columna)
        => lector.IsDBNull(columna) ? null : lector.GetString(columna);

    /// <summary>Lee un entero que puede venir nulo.</summary>
    /// <param name="lector">El lector posicionado en la fila.</param>
    /// <param name="columna">El índice de la columna.</param>
    protected static int? EnteroONulo(SqliteDataReader lector, int columna)
        => lector.IsDBNull(columna) ? null : lector.GetInt32(columna);

    /// <summary>Lee un entero largo que puede venir nulo.</summary>
    /// <param name="lector">El lector posicionado en la fila.</param>
    /// <param name="columna">El índice de la columna.</param>
    protected static long? LargoONulo(SqliteDataReader lector, int columna)
        => lector.IsDBNull(columna) ? null : lector.GetInt64(columna);

    /// <summary>Lee un decimal que puede venir nulo.</summary>
    /// <param name="lector">El lector posicionado en la fila.</param>
    /// <param name="columna">El índice de la columna.</param>
    protected static double? DecimalONulo(SqliteDataReader lector, int columna)
        => lector.IsDBNull(columna) ? null : lector.GetDouble(columna);

    /// <summary>
    /// Lee una casilla de TRES estados: si, no, o nadie lo miro.
    /// </summary>
    /// <remarks>
    /// El nulo es un DATO y no un hueco: «no leida» no es lo mismo que «leida y no
    /// marcada», y confundirlas es lo que hace que un formulario en blanco se vea igual
    /// que uno que dice que no.
    /// </remarks>
    /// <param name="lector">El lector posicionado en la fila.</param>
    /// <param name="columna">El índice de la columna.</param>
    /// <returns>Nulo si la columna es NULL; si no, verdadero cuando el entero no es 0.</returns>
    protected static bool? CasillaDeTresEstados(SqliteDataReader lector, int columna)
        => lector.IsDBNull(columna) ? null : lector.GetInt64(columna) != 0;

    /// <summary>Lee un booleano de dos estados.</summary>
    /// <param name="lector">El lector posicionado en la fila.</param>
    /// <param name="columna">El índice de la columna.</param>
    /// <returns>Falso si es NULL o 0; verdadero en cualquier otro caso.</returns>
    protected static bool Booleano(SqliteDataReader lector, int columna)
        => !lector.IsDBNull(columna) && lector.GetInt64(columna) != 0;

    /// <summary>Convierte un valor que puede ser nulo en lo que espera el parametro.</summary>
    /// <param name="valor">El valor, que puede ser nulo.</param>
    /// <returns>El valor, o <see cref="DBNull.Value"/>: un <c>null</c> de C# no se puede pasar como parámetro.</returns>
    protected static object ONulo(object? valor) => valor ?? DBNull.Value;

    /// <summary>Convierte una casilla de tres estados en lo que espera el parametro.</summary>
    /// <param name="casilla">Sí, no, o nadie lo miró.</param>
    /// <returns>1, 0 o <see cref="DBNull.Value"/>.</returns>
    protected static object DeCasilla(bool? casilla)
        => casilla is null ? DBNull.Value : (casilla.Value ? 1 : 0);

    /// <summary>
    /// Traduce el fallo del motor a una frase que se pueda leer en la franja.
    /// </summary>
    /// <remarks>
    /// El mensaje crudo de SQLite viene en ingles y nombra la restriccion, no el campo.
    /// Aqui se traduce lo que se reconoce y se deja el resto tal cual: inventar una
    /// causa que no se sabe seria peor que ensenar el texto del motor.
    /// </remarks>
    /// <param name="causa">La excepción del motor.</param>
    /// <returns>Una frase en español seguida del mensaje del motor, o el mensaje solo si no se reconoce la restricción.</returns>
    private static string TraducirElFalloDelMotor(SqliteException causa)
    {
        var mensaje = causa.Message;

        if (mensaje.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
        {
            return "La fila apunta a algo que no existe (un caso o un companero que no " +
                   "esta en la base). Detalle del motor: " + mensaje;
        }

        if (mensaje.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
        {
            return "Ya hay una fila con esa misma clave. Detalle del motor: " + mensaje;
        }

        if (mensaje.Contains("CHECK", StringComparison.OrdinalIgnoreCase))
        {
            return "El valor no cumple una regla de coherencia del esquema. " +
                   "Detalle del motor: " + mensaje;
        }

        if (mensaje.Contains("NOT NULL", StringComparison.OrdinalIgnoreCase))
        {
            return "Falta un dato obligatorio. Detalle del motor: " + mensaje;
        }

        return mensaje;
    }
}
