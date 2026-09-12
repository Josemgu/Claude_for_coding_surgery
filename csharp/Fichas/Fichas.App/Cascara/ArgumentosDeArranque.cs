namespace Fichas.App.Cascara;

/// <summary>
/// Lo que se le puede decir al programa desde la linea de ordenes.
/// </summary>
/// <remarks>
/// <para>Un argumento que no se entiende NO detiene el arranque (requisito 9): se ignora, se
/// queda el valor por defecto y el motivo sale en la franja de avisos al abrir.</para>
///
/// <para><b>Los argumentos que existen y para qué:</b></para>
/// <list type="bullet">
/// <item><c>--carpeta-de-datos RUTA</c> o <c>--carpeta-de-datos=RUTA</c>: dónde viven la base
/// y el Excel espejo. Sin él, <c>Documentos\Fichas</c>; con <c>--falso</c> y sin él, una
/// carpeta de usar y tirar en la temporal de Windows.</item>
/// <item><c>--falso N</c>: arranca con N casos inventados en memoria en vez de la base del
/// dueño; sirve para medir una pantalla con 3 000 documentos sin tenerlos. <c>--falso 0</c>
/// monta datos vacíos, que es como se comprueba que una pantalla no se cae sin datos.</item>
/// <item><c>--tamano ANCHOxALTO</c>: tamaño de la ventana al abrir (mínimo 400x300); por
/// defecto 1730x770. Existe para retratar la ventana a un tamaño fijo en las mediciones.</item>
/// <item><c>--sin-actualizacion</c>: no se pregunta a GitHub al arrancar si hay versión nueva.
/// Con <c>--falso</c> tampoco se pregunta, sin necesidad de decirlo: los agentes arrancan el
/// programa cien veces al día y no deben salir a internet. El botón «Buscar actualización»
/// pregunta igual.</item>
/// <item><c>/medir-inicio [RUTA]</c> y <c>/cerrar-al-medir</c>: los de medición. NO los lee esta
/// clase sino <c>Inicio.MedicionDeInicio</c>, y empiezan por «/» a propósito para que esta
/// clase no los marque como desconocidos. Escriben cuánto tardó Inicio en pintarse y cuántos
/// elementos quedaron vivos, y el segundo cierra el programa al terminar.</item>
/// </list>
/// <para>Lo que empieza por <c>--</c> y no está en la lista se apunta en <see cref="NoSeEntendio"/>;
/// lo que no empieza por <c>--</c> se ignora en silencio.</para>
/// </remarks>
public sealed class ArgumentosDeArranque
{
    /// <summary>Donde viven la base y el Excel espejo; por defecto Documentos\Fichas.</summary>
    public string CarpetaDeDatos { get; private init; } = CarpetaPorDefecto();

    /// <summary>
    /// Cuantos casos inventados se generan, o nulo para abrir la base de verdad.
    /// </summary>
    /// <remarks>
    /// ⚠️ El nulo y el cero NO son lo mismo, y por eso esto no es un <c>int</c>: sin
    /// <c>--falso</c> se abre la base del dueno; con <c>--falso 0</c> se montan datos
    /// inventados VACIOS, que es como se comprueba que una pantalla no se cae sin datos.
    /// Con un cero por defecto, cualquier arranque normal habria abierto lo falso.
    /// </remarks>
    public int? CasosInventados { get; private init; }

    /// <summary>Si se pidieron datos inventados en vez de la base de verdad.</summary>
    public bool SeUsanDatosInventados => CasosInventados is not null;

    /// <summary>
    /// Si la carpeta es la de usar y tirar que se monta sola con los datos inventados.
    /// </summary>
    /// <remarks>
    /// Existe para poder DECIRLO en la ventana. Una carpeta que el programa elige sola y no
    /// se enseña en ningun sitio es una carpeta que nadie va a buscar cuando haga falta.
    /// </remarks>
    public bool LaCarpetaEsDeUsarYTirar { get; private init; }

    /// <summary>Ancho de la ventana al abrir, en pixeles.</summary>
    public int Ancho { get; private init; } = 1730;

    /// <summary>Alto de la ventana al abrir, en pixeles.</summary>
    public int Alto { get; private init; } = 770;

    /// <summary>Lo que no se entendio, en una linea cada cosa, para ensenarlo en la franja.</summary>
    public IReadOnlyList<string> NoSeEntendio { get; private init; } = Array.Empty<string>();

    /// <summary>Si se pidió <c>--sin-actualizacion</c>: no preguntar a GitHub al arrancar.</summary>
    public bool SinActualizacion { get; private init; }

    /// <summary>
    /// Si al arrancar se pregunta si hay versión nueva: solo en un arranque normal, sin
    /// <c>--falso</c> y sin <c>--sin-actualizacion</c>.
    /// </summary>
    /// <remarks>
    /// Con datos inventados no se sale a internet aunque nadie lo diga: los agentes arrancan
    /// el programa con <c>--falso</c> cien veces al día, y cien consultas a GitHub por
    /// nada es justo lo que no se quiere.
    /// </remarks>
    public bool SeBuscaActualizacionAlArrancar => !SeUsanDatosInventados && !SinActualizacion;

    /// <summary>
    /// Lee los argumentos; lo que no se entiende se ignora y se apunta. Nunca lanza: con una
    /// lista vacía devuelve los valores por defecto, que es el doble clic normal.
    /// </summary>
    /// <param name="argumentos">Los argumentos SIN el ejecutable (quien llama ya saltó la posición 0).</param>
    /// <returns>Lo leído, con la carpeta ya resuelta según se pidieran o no datos inventados.</returns>
    public static ArgumentosDeArranque Leer(string[] argumentos)
    {
        // ⛔ Empieza en NULO y no en la carpeta del dueno. Es la linea del arreglo del
        // 2026-09-05: hasta ese dia se resolvia la de verdad desde el primer momento, y con
        // «--falso N» y sin decir carpeta el programa escribia su cuaderno EN LA DEL DUENO.
        // Medido por el supervisor en su C:\Users\josem\Documents\Fichas\fichas.log, y le
        // habia pasado a tres agentes el mismo dia. Cual toca se decide abajo, cuando ya se
        // sabe si se pidieron datos inventados.
        string? carpetaDicha = null;
        int? casos = null;
        var ancho = 1730;
        var alto = 770;
        var sinActualizacion = false;
        var sobras = new List<string>();

        for (var i = 0; i < argumentos.Length; i++)
        {
            switch (argumentos[i])
            {
                case "--carpeta-de-datos" when i + 1 < argumentos.Length:
                    carpetaDicha = argumentos[++i];
                    break;

                // Las dos formas que la gente teclea, igual que en Fichas.Datos: con
                // espacio y con igual. Admitir solo una convierte un descuido en «el
                // programa abrio la base equivocada y no lo dijo».
                case var conIgual when conIgual.StartsWith("--carpeta-de-datos=", StringComparison.Ordinal):
                    var valor = conIgual["--carpeta-de-datos=".Length..];
                    if (!string.IsNullOrWhiteSpace(valor)) carpetaDicha = valor;
                    else sobras.Add("«--carpeta-de-datos=» vino sin ruta detrás: se ignora.");
                    break;

                case "--falso" when i + 1 < argumentos.Length:
                    if (int.TryParse(argumentos[++i], out var cuantos) && cuantos >= 0) casos = cuantos;
                    else sobras.Add($"«--falso {argumentos[i]}» no es un número de casos: se ignora.");
                    break;

                case "--tamano" when i + 1 < argumentos.Length:
                    if (!LeerTamano(argumentos[++i], ref ancho, ref alto))
                        sobras.Add($"«--tamano {argumentos[i]}» no tiene la forma ANCHOxALTO: se ignora.");
                    break;

                case "--sin-actualizacion":
                    sinActualizacion = true;
                    break;

                default:
                    if (argumentos[i].StartsWith("--", StringComparison.Ordinal))
                        sobras.Add($"«{argumentos[i]}» no es un argumento que este programa conozca: se ignora.");
                    break;
            }
        }

        // Quien dice la carpeta manda. Quien no la dice y pide datos inventados se lleva una
        // de usar y tirar. Quien no dice nada abre la del dueno, que es el doble clic normal.
        var deUsarYTirar = carpetaDicha is null && casos is not null;

        return new ArgumentosDeArranque
        {
            CarpetaDeDatos = carpetaDicha ?? (deUsarYTirar ? CarpetaDeLoInventado() : CarpetaPorDefecto()),
            LaCarpetaEsDeUsarYTirar = deUsarYTirar,
            CasosInventados = casos,
            Ancho = ancho,
            Alto = alto,
            NoSeEntendio = sobras,
            SinActualizacion = sinActualizacion,
        };
    }

    /// <summary>Lee un «1100x700»; devuelve falso si no tiene esa forma y no toca nada.</summary>
    /// <param name="texto">Lo que vino detrás de <c>--tamano</c>; admite «x» o «X» como separador.</param>
    /// <param name="ancho">Recibe el ancho solo si el texto es válido y llega al mínimo de 400.</param>
    /// <param name="alto">Recibe el alto solo si el texto es válido y llega al mínimo de 300.</param>
    /// <returns>Verdadero si se leyeron los dos números y ninguno está por debajo del mínimo.</returns>
    private static bool LeerTamano(string texto, ref int ancho, ref int alto)
    {
        var partes = texto.Split('x', 'X');
        if (partes.Length != 2) return false;
        if (!int.TryParse(partes[0], out var a) || !int.TryParse(partes[1], out var b)) return false;
        if (a < 400 || b < 300) return false;
        ancho = a;
        alto = b;
        return true;
    }

    /// <summary>
    /// La carpeta de usar y tirar de los datos inventados, en la carpeta temporal de Windows.
    /// </summary>
    /// <remarks>
    /// <para>⛔ Con <c>--falso</c> y sin <c>--carpeta-de-datos</c>, el programa NO puede
    /// escribir en <c>Documents\Fichas</c>. No es por el cuaderno: es que esa era la unica
    /// puerta por la que una prueba alcanzaba los datos del dueno sin querer, y el dia que
    /// algo escriba de verdad, escribiria en los suyos.</para>
    ///
    /// <para>El nombre se lee solo: quien encuentre esta carpeta dentro de seis meses sabe de
    /// donde salio sin abrir el codigo. No lleva numero de proceso a proposito, para que
    /// cerrar y volver a abrir con <c>--falso</c> siga encontrando lo que dejo la vez
    /// anterior; lo que hay dentro es de mentira y se puede borrar cuando se quiera.</para>
    /// </remarks>
    private static string CarpetaDeLoInventado()
        => Path.Combine(Path.GetTempPath(), "Fichas-datos-inventados");

    /// <summary>
    /// Documentos\Fichas, fuera de la carpeta del programa para que una actualizacion no se
    /// lleve los datos.
    /// </summary>
    /// <remarks>
    /// ⚠️ La resuelve <c>Fichas.Datos</c> con <c>SHGetKnownFolderPath</c> y NO
    /// <c>Environment.SpecialFolder.MyDocuments</c>, que es lo que habia aqui antes. La
    /// decision es del 2026-09-02 y esta medida: en la maquina del dueno hay tres carpetas
    /// candidatas y dos sincronizan con OneDrive; componer la ruta por otra via puede
    /// aterrizar en la de OneDrive y subir a la nube una base con MRN de personas reales.
    ///
    /// <para>Si Windows no la devuelve (<c>ErrorDeRuta</c>), el programa NO se queda sin
    /// carpeta: cae en la carpeta del usuario y lo dice al arrancar. Preferir no abrir seria
    /// dejar al dueno con un icono que no hace nada.</para>
    /// </remarks>
    private static string CarpetaPorDefecto()
    {
        try
        {
            return Fichas.Datos.Rutas.CarpetaDeDatos.ResolverCarpetaDeDatos();
        }
        catch (Fichas.Datos.Rutas.ErrorDeRuta)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Fichas");
        }
    }
}
