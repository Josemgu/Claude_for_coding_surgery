namespace Fichas.Paquetes;

/// <summary>De que clase es una columna, que es lo que decide como entra en la celda.</summary>
/// <remarks>
/// Son las mismas tres del programa en Python (<c>paquete/columnas.py</c> y
/// <c>espejo/hojas.py</c>) y por el mismo motivo. La que importa es
/// <see cref="Texto"/>: sin formato de texto (<c>@</c>), Excel lee <c>055-1111-3853</c>
/// como algo que puede normalizar y se come el cero de delante. Y un MRN sin sus
/// ceros deja de servir para reconciliar, que es todo su trabajo.
/// </remarks>
public enum ClaseDeColumna
{
    /// <summary>Entra tal cual: enteros y texto libre.</summary>
    Crudo = 0,

    /// <summary>Se marca con formato de texto para que Excel no la normalice.</summary>
    Texto = 1,

    /// <summary>Sale de la base como ISO-8601 y entra en la celda como fecha de verdad.</summary>
    Temporal = 2,
}

/// <summary>Que le pide una columna al companero, que es lo que decide como se lee al volver.</summary>
/// <remarks>
/// Las tres no se pueden mezclar: <see cref="SiONo"/> se lee como verdadero, falso o nulo y
/// una respuesta que no se entiende DESCARTA la fila entera; las otras dos no pueden
/// descartar nada, porque una frase escrita a mano no puede tirar seis respuestas buenas.
/// </remarks>
public enum ClaseDeRespuesta
{
    /// <summary>No la rellena el companero: sale escrita y, casi siempre, bloqueada.</summary>
    Ninguna = 0,

    /// <summary>Una de las siete de si o no: los seis pasos y la llamada al lider.</summary>
    SiONo = 1,

    /// <summary>Por que no se completo: menu con las tres frases del dueno.</summary>
    Motivo = 2,

    /// <summary>El comentario del agente: sin menu, porque la realidad no cabe en una lista.</summary>
    TextoLibre = 3,
}

/// <summary>Una columna de la hoja «Por verificar», con todo lo que decide como se pinta.</summary>
/// <remarks>
/// <see cref="EsClave"/>, <see cref="EsEditable"/> y <see cref="EsRespuesta"/> son tres
/// cosas distintas y hacen falta las tres. Solo las <see cref="EsRespuesta"/> llevan
/// fondo, porque el fondo es lo que le dice al companero «esto lo rellenas tu»;
/// <c>nombre</c> es editable pero NO es una respuesta.
/// </remarks>
/// <param name="Nombre">Como se llama la columna en la base.</param>
/// <param name="Titulo">Como sale impresa en la hoja; es tambien lo que se busca al volver.</param>
/// <param name="Clase">Como entra el valor en la celda.</param>
/// <param name="EsClave">Si es parte del par que reconcilia.</param>
/// <param name="EsEditable">Si el companero la puede teclear; las que no, se escriben bloqueadas.</param>
/// <param name="Respuesta">Que le pide al companero, si es que le pide algo.</param>
public sealed record ColumnaDeLaHoja(
    string Nombre,
    string Titulo,
    ClaseDeColumna Clase,
    bool EsClave,
    bool EsEditable,
    ClaseDeRespuesta Respuesta)
{
    /// <summary>Si el companero VIENE a rellenarla; son las que llevan fondo.</summary>
    public bool EsRespuesta => Respuesta != ClaseDeRespuesta.Ninguna;

    /// <summary>Si es una de las siete que se leen como si, no o en blanco.</summary>
    public bool EsSiONo => Respuesta == ClaseDeRespuesta.SiONo;
}

/// <summary>Lo que lleva dentro una clave de fila: <c>CASO:MRN:ID</c>.</summary>
/// <param name="NumeroCaso">El numero de caso, o nulo si la celda venia vacia.</param>
/// <param name="Mrn">La cedula de miembro, o nulo si la persona no la tenia.</param>
/// <param name="CasoId">El id del caso; nulo en una clave vieja de dos partes.</param>
public readonly record struct ClavePartida(string? NumeroCaso, string? Mrn, long? CasoId);

/// <summary>
/// Las columnas de la hoja «Por verificar»: una sola definicion para la ida y para la vuelta.
/// </summary>
/// <remarks>
/// Existe para que el Excel que SALE y el que VUELVE no se puedan separar. Si el
/// generador escribiera sus cabeceras y el lector buscara las suyas, bastaria renombrar
/// una columna en un sitio para que la vuelta dejara de encontrarla —y una reconciliacion
/// que no encuentra su columna no falla: descarta todo en silencio—.
/// <para>
/// De donde sale la hoja: del programa viejo, que es el que el dueno llama perfecto
/// (DECISIONES.md, 2026-09-03). Una sola hoja, cinco filas de cabecera, la tabla desde la
/// fila 6 y la clave A LA VISTA en la ultima columna — «un dato oculto es un dato que
/// alguien borra sin saber lo que hace».
/// </para>
/// <para>
/// ⚠️ <b>«Estaca o distrito» y «Fecha de solicitud» estuvieron aqui y ya no estan
/// (2026-09-06).</b> Estan en la hoja del viejo y la base de este proyecto NO las guarda, asi
/// que salian siempre con <see cref="SinDato"/> en vez de en blanco —un blanco lo lee el
/// companero como «la estaca esta vacia en el papel», que es un dato falso—. Se dejaban por
/// fidelidad a la hoja que el dueno aprobo. El las quito con un motivo que gana a ese: «son
/// informaciones que no me pide verificar». Una columna que siempre dice «no consta» y que
/// nadie tiene que mirar es ruido en una hoja que el companero rellena a mano. De 18 a 16.
/// </para>
/// <para>
/// Lo que eso deja pendiente y no se toco: si algun dia la base guardara la estaca, volver a
/// ponerla es anadir su <see cref="ColumnaDeLaHoja"/> y su ancho, y nada mas —la vuelta busca
/// por rotulo y no por posicion, que es lo que permite que un paquete de 18 columnas generado
/// antes de este cambio se siga reconciliando entero—.
/// </para>
/// </remarks>
public static class Columnas
{
    /// <summary>El nombre de la pestana. El lector la busca por el y, si no esta, cae a la activa.</summary>
    public const string NombreDeLaHoja = "Por verificar";

    /// <summary>Donde va la fila de titulos. Encima van las cinco lineas de cabecera.</summary>
    public const int FilaDeLaCabecera = 6;

    /// <summary>Donde empieza la primera persona.</summary>
    public const int PrimeraFilaDeDatos = FilaDeLaCabecera + 1;

    /// <summary>La columna que devuelve cada renglon a su persona.</summary>
    public const string ColumnaDeLaClave = "clave";

    /// <summary>Lo que separa las tres partes de la clave.</summary>
    public const char SeparadorDeLaClave = ':';

    /// <summary>Lo que se escribe donde la base no tiene el dato. Misma palabra en todo el programa.</summary>
    public const string SinDato = "no consta";

    /// <summary>Lo que se ensancha una columna que no tiene ancho propio: los seis pasos.</summary>
    public const int AnchoDeUnPaso = 13;

    /// <summary>
    /// Las 16 columnas en el orden en que salen impresas.
    /// </summary>
    /// <remarks>
    /// ⚠️ <c>numero_caso</c> y <c>mrn</c> van BLOQUEADAS. Son las dos mitades del par que
    /// reconcilia: si el companero corrige un MRN «que estaba mal», su fila deja de casar
    /// y su trabajo entero se va a la lista de descartados. <c>nombre</c> SI se deja
    /// editable, y no es un descuido: la reconciliacion NUNCA mira el nombre, asi que un
    /// acento cambiado no puede hacer dano.
    /// <para>
    /// Las dos ultimas antes de la clave —el motivo y el comentario— las pidio el dueno el
    /// 2026-09-05: «en el calendario tambien puede decir el estado: no completado, no se
    /// pudo comunicar con el lider, o el lider no lo hizo». Hasta entonces el companero no
    /// tenia DONDE decir por que no pudo, y la hoja solo admitia trabajo hecho. Van
    /// pegadas a las otras siete para que todo lo que rellena el agente quede junto, y
    /// ANTES de la clave, que sigue siendo la ultima y a la vista.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<ColumnaDeLaHoja> Todas { get; } =
    [
        new("numero_caso", "Caso", ClaseDeColumna.Texto, EsClave: true, EsEditable: false, ClaseDeRespuesta.Ninguna),
        new("fecha_viaje", "Fecha de viaje", ClaseDeColumna.Temporal, false, false, ClaseDeRespuesta.Ninguna),
        new("unidad_nombre", "Barrio o rama", ClaseDeColumna.Crudo, false, false, ClaseDeRespuesta.Ninguna),
        new("nombre", "Hermano(a) que viaja", ClaseDeColumna.Crudo, false, true, ClaseDeRespuesta.Ninguna),
        new("mrn", "Cédula de miembro", ClaseDeColumna.Texto, EsClave: true, EsEditable: false, ClaseDeRespuesta.Ninguna),
        new("a_que_va", "A qué va", ClaseDeColumna.Crudo, false, false, ClaseDeRespuesta.Ninguna),
        .. Pasos.Todos.Select(paso => new ColumnaDeLaHoja(
            paso.Nombre, paso.Rotulo, ClaseDeColumna.Crudo, false, true, ClaseDeRespuesta.SiONo)),
        new(Pasos.ColumnaDeLaLlamada, Pasos.RotuloDeLaLlamada, ClaseDeColumna.Crudo, false, true, ClaseDeRespuesta.SiONo),
        new(MotivosDeLaHoja.ColumnaDelMotivo, MotivosDeLaHoja.RotuloDelMotivo,
            ClaseDeColumna.Crudo, false, true, ClaseDeRespuesta.Motivo),
        new(MotivosDeLaHoja.ColumnaDelComentario, MotivosDeLaHoja.RotuloDelComentario,
            ClaseDeColumna.Crudo, false, true, ClaseDeRespuesta.TextoLibre),
        new(ColumnaDeLaClave, "clave", ClaseDeColumna.Texto, false, false, ClaseDeRespuesta.Ninguna),
    ];

    private static readonly Dictionary<string, ColumnaDeLaHoja> PorNombre =
        Todas.ToDictionary(columna => columna.Nombre);

    /// <summary>
    /// Cuanto se ensancha cada columna, en caracteres. No es decoracion: con el ancho por
    /// defecto un MRN de once digitos sale como <c>#####</c> y el companero no puede
    /// comprobar contra que trabaja. Los numeros son los del programa viejo.
    /// </summary>
    private static readonly Dictionary<string, int> Anchos = new()
    {
        ["numero_caso"] = 11,
        ["fecha_viaje"] = 14,
        ["unidad_nombre"] = 24,
        ["nombre"] = 26,
        ["mrn"] = 19,
        ["a_que_va"] = 26,
        [Pasos.ColumnaDeLaLlamada] = 16,
        // Las dos del 2026-09-05: la opcion mas larga del menu son 32 caracteres y a 13
        // —el ancho de un paso— saldria cortada, que es lo mismo que no ofrecerla.
        [MotivosDeLaHoja.ColumnaDelMotivo] = MotivosDeLaHoja.AnchoDelMotivo,
        [MotivosDeLaHoja.ColumnaDelComentario] = MotivosDeLaHoja.AnchoDelComentario,
        // 26 y no los 16 del programa viejo: la clave lleva ahora tres partes
        // —«BALC2609:055-1111-3853:12» son 25 caracteres— y a 16 sale cortada.
        [ColumnaDeLaClave] = 26,
    };

    /// <summary>La columna que se llama asi. Levanta diciendo cuales hay si no existe.</summary>
    public static ColumnaDeLaHoja Por(string nombre)
        => PorNombre.TryGetValue(nombre, out var columna)
            ? columna
            : throw new KeyNotFoundException(
                $"La columna «{nombre}» no existe en la hoja «{NombreDeLaHoja}». Las que hay son: "
                + string.Join(", ", Todas.Select(c => c.Nombre)) + ".");

    /// <summary>Los caracteres de ancho de esa columna. Los seis pasos miden todos igual.</summary>
    public static int AnchoDe(string nombre) => Anchos.TryGetValue(nombre, out var ancho) ? ancho : AnchoDeUnPaso;

    /// <summary>La posicion de una columna en la hoja, contando desde 1 como Excel.</summary>
    public static int IndiceDe(string nombre)
    {
        for (var numero = 0; numero < Todas.Count; numero++)
            if (Todas[numero].Nombre == nombre)
                return numero + 1;
        throw new KeyNotFoundException(
            $"La columna «{nombre}» no existe en la hoja «{NombreDeLaHoja}». Las que hay son: "
            + string.Join(", ", Todas.Select(c => c.Nombre)) + ".");
    }

    /// <summary>La fila de titulos, tal como se escribe y tal como se busca.</summary>
    public static IReadOnlyList<string> Titulos() => [.. Todas.Select(columna => columna.Titulo)];

    /// <summary>
    /// El texto de la columna «clave» de una persona: <c>CASO:MRN:ID</c>.
    /// </summary>
    /// <remarks>
    /// Una persona sin MRN sale con la clave a medias —<c>CASO::ID</c>— y no con una clave
    /// inventada. Al volver, esa fila cae en descartados con su motivo, que es donde Miguel
    /// la ve.
    /// <para>
    /// ⚠️ La tercera parte —el id del caso— es obligatoria para que la vuelta no cruce dos
    /// familias (DECISIONES.md, 2026-09-03). Desde la version 12 del esquema dos casos
    /// distintos pueden llevar el MISMO numero: son cuatro letras mas el ano y el mes, o
    /// sea una unidad y un mes, no una familia. Con <c>CASO:MRN</c> a secas la fila que
    /// vuelve casaria con dos personas y no habria forma de saber con cual.
    /// </para>
    /// <para>
    /// <paramref name="casoId"/> admite nulo para no romper a quien todavia arme una clave
    /// sin el; esa clave se resuelve por el camino viejo, que puede quedar ambiguo. El
    /// generador SIEMPRE lo pasa.
    /// </para>
    /// </remarks>
    public static string ArmarLaClave(string? numeroCaso, string? mrn, long? casoId)
    {
        var cola = casoId is null ? string.Empty : $"{SeparadorDeLaClave}{casoId}";
        return $"{numeroCaso}{SeparadorDeLaClave}{mrn}{cola}";
    }

    /// <summary>
    /// Lo que lleva dentro una clave, o nulo cuando no tiene la forma esperada.
    /// </summary>
    /// <remarks>
    /// Devuelve nulo —y no levanta— cuando la clave no se puede partir: es un resultado
    /// normal del trabajo (a alguien se le borro la celda) y quien llama lo convierte en
    /// una fila descartada con su motivo.
    /// <para>
    /// El id sale nulo cuando la clave viene del formato viejo de dos partes —un paquete
    /// generado antes del 2026-09-03— o cuando la tercera parte no es un numero. Las dos
    /// primeras partes se leen igual que siempre, asi que un Excel antiguo sigue volviendo.
    /// </para>
    /// </remarks>
    public static ClavePartida? PartirLaClave(string? clave)
    {
        if (string.IsNullOrWhiteSpace(clave) || !clave.Contains(SeparadorDeLaClave))
            return null;

        var partes = clave.Split(SeparadorDeLaClave);
        return new ClavePartida(
            VacioComoNulo(partes[0]),
            VacioComoNulo(partes[1]),
            EnteroONada(partes.Length > 2 ? partes[2] : string.Empty));
    }

    private static string? VacioComoNulo(string texto)
    {
        var limpio = texto.Trim();
        return limpio.Length == 0 ? null : limpio;
    }

    /// <summary>
    /// El id del caso que trae la clave, o nulo si no lo trae o no es un numero.
    /// Nulo y no un error: una clave sin tercera parte es la de un paquete anterior a este
    /// cambio, y esas filas tienen que seguir volviendo.
    /// </summary>
    private static long? EnteroONada(string texto)
    {
        var limpio = texto.Trim();
        return limpio.Length > 0 && limpio.All(char.IsAsciiDigit) && long.TryParse(limpio, out var id)
            ? id
            : null;
    }
}
