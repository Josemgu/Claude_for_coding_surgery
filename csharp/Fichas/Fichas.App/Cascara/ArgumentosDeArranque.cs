namespace Fichas.App.Cascara;

/// <summary>
/// Lo que se le puede decir al programa desde la linea de ordenes.
/// </summary>
/// <remarks>
/// Un argumento que no se entiende NO detiene el arranque (requisito 9): se ignora, se
/// queda el valor por defecto y el motivo sale en la franja de avisos al abrir.
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

    /// <summary>Lee los argumentos; lo que no se entiende se ignora y se apunta.</summary>
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
        };
    }

    /// <summary>Lee un «1100x700»; devuelve falso si no tiene esa forma y no toca nada.</summary>
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
    /// <para>Si Windows no la devuelve, el programa NO se queda sin carpeta: cae en la
    /// carpeta del usuario y lo dice al arrancar. Preferir no abrir seria dejar al dueno
    /// con un icono que no hace nada.</para>
    /// </remarks>
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
