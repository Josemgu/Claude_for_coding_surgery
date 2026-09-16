using System.Globalization;
using ClosedXML.Excel;

namespace Fichas.Paquetes;

/// <summary>Una fila de la hoja «Por verificar»: una persona con lo que la hoja pide.</summary>
/// <remarks>
/// ⚠️ Aqui vivian <c>FechaSolicitud</c> y <c>Estaca</c>, y salian a nulo SIEMPRE porque la base
/// no las guarda —el dueno dijo «solo debe leer los campos que yo necesito» y esos dos no
/// estaban en su lista (DECISIONES.md, 2026-09-03)—. Se escribian igual, con
/// <see cref="Columnas.SinDato"/>, por fidelidad a la hoja que el aprobo. El 2026-09-06 las
/// quito de la hoja —«son informaciones que no me pide verificar»— y con ellas se fueron estas
/// dos propiedades: una fila no lleva un dato que ninguna columna imprime.
/// </remarks>
public sealed record FilaDeTrabajo
{
    /// <summary>El numero del caso al que pertenece la persona.</summary>
    public string? NumeroCaso { get; init; }

    /// <summary>La fecha del viaje al templo, ISO-8601.</summary>
    public string? FechaViaje { get; init; }

    /// <summary>El templo al que viaja. Va en su columna desde el 2026-09-16, y en la cabecera cuando todas las filas van al mismo.</summary>
    public string? Templo { get; init; }

    /// <summary>
    /// El barrio o rama, tal como lo guarda la base y SIN el numero pegado detras.
    /// </summary>
    /// <remarks>
    /// ⚠️ Hasta el 2026-09-07 aqui llegaba «Cuatricentenaria (7000014)», armado por
    /// <c>Paquetes.UnidadConSuNumero</c>. El dueno pidio los dos datos separados —«el numero de
    /// unidad en un lado y al otro el nombre de la unidad»— y ahora cada uno viaja en su
    /// propiedad y sale en su columna. Lo que la base guarde es lo que se escribe: si el
    /// escaneo dejo el numero dentro del nombre, sale dentro del nombre, porque corregirlo
    /// aqui seria cambiar un dato leido.
    /// </remarks>
    public string? UnidadNombre { get; init; }

    /// <summary>El numero de la unidad, tal como lo guarda la base.</summary>
    public string? UnidadNumero { get; init; }

    /// <summary>El nombre de la persona tal como se leyo o se corrigio.</summary>
    public string? Nombre { get; init; }

    /// <summary>La cedula de miembro. Puede terminar en letra y no se toca.</summary>
    public string? Mrn { get; init; }

    /// <summary>A que va al templo, resumido de las seis casillas del formulario.</summary>
    public string? AQueVa { get; init; }

    /// <summary>La clave de fila <c>CASO:MRN:ID</c>, a la vista en la ultima columna.</summary>
    public string? Clave { get; init; }

    /// <summary>
    /// Las siete respuestas que el sistema ya tiene de esta persona, por nombre de columna:
    /// los seis pasos y la llamada al lider. Nulo donde nadie contesto.
    /// </summary>
    /// <remarks>
    /// ⚠️ Hasta el 2026-09-16 las siete salian SIEMPRE vacias, a proposito: «lo que se le
    /// manda a un companero es lo que tiene que mirar, no lo que otro contesto». El dueno lo
    /// cambio ese dia (PENDIENTES.md, v16, 5): <i>«el paquete de Excel no marca las preguntas
    /// que ya están completas. Eso debería hacerlo el programa»</i>. Lo que ya esta contestado
    /// sale escrito en su celda; lo que no, en blanco para que lo rellene el.
    /// </remarks>
    public IReadOnlyDictionary<string, bool?> Respuestas { get; init; } = new Dictionary<string, bool?>();

    /// <summary>Quien contesto los seis pasos, cuando y desde donde, ya en palabras; nulo si nadie.</summary>
    /// <remarks>Va como nota de cada celda de paso que sale contestada. Es <c>pasos_por</c> / <c>pasos_en</c> / <c>pasos_origen</c> leidos para una persona.</remarks>
    public string? NotaDeLasSeis { get; init; }

    /// <summary>Quien dijo si llamo al lider y cuando, ya en palabras; nulo si nadie.</summary>
    /// <remarks>La llamada NO es un paso y no la firma <c>pasos_por</c>: viene del Excel de un companero, asi que su nota sale de <c>propuesto_por</c> / <c>propuesto_en</c>.</remarks>
    public string? NotaDeLaLlamada { get; init; }

    /// <summary>El valor de una columna de esta fila, buscada por el nombre de la columna.</summary>
    /// <param name="nombreDeColumna">El nombre en la base; las siete respuestas salen como «Sí»/«No» o nulo; el motivo, el comentario y cualquier nombre desconocido dan nulo.</param>
    public string? ValorDe(string nombreDeColumna) => nombreDeColumna switch
    {
        "numero_caso" => NumeroCaso,
        "fecha_viaje" => FechaViaje,
        Columnas.ColumnaDelTemplo => Templo,
        Columnas.ColumnaDelNumeroDeUnidad => UnidadNumero,
        "unidad_nombre" => UnidadNombre,
        "nombre" => Nombre,
        "mrn" => Mrn,
        "a_que_va" => AQueVa,
        Columnas.ColumnaDeLaClave => Clave,
        // Las siete respuestas: lo que el sistema ya tiene, escrito como lo ofrece el menu.
        // Solo ellas estan en el diccionario; el motivo, el comentario y un nombre
        // desconocido caen fuera y dan nulo.
        _ => Respuestas.TryGetValue(nombreDeColumna, out var respuesta) ? Pasos.Escribir(respuesta) : null,
    };

    /// <summary>La nota que acompana a una celda de respuesta que sale contestada: quien y cuando.</summary>
    /// <param name="nombreDeColumna">El nombre en la base; la llamada tiene su nota y los seis pasos la suya; cualquier otra da nulo.</param>
    public string? NotaDe(string nombreDeColumna) => nombreDeColumna switch
    {
        Pasos.ColumnaDeLaLlamada => NotaDeLaLlamada,
        _ when Respuestas.ContainsKey(nombreDeColumna) => NotaDeLasSeis,
        _ => null,
    };
}

/// <summary>Lo que va arriba de la tabla y es igual para todas sus filas.</summary>
/// <param name="NumeroDeCaso">Se nombra solo cuando todas las filas son del mismo caso.</param>
/// <param name="Templo">Se nombra solo cuando todas las filas van al mismo templo.</param>
/// <param name="FechaDeSalida">La MAS temprana de las que traiga la hoja, ISO-8601.</param>
/// <param name="Agente">El companero al que se le entrega.</param>
public sealed record CabeceraDeLaHoja(string NumeroDeCaso, string Templo, string FechaDeSalida, string Agente);

/// <summary>
/// La hoja «Por verificar» que se le entrega a un companero, construida en memoria.
/// </summary>
/// <remarks>
/// No toca el disco: construye el libro y lo devuelve. Escribirlo es de <see cref="Paquetes"/>.
/// <para>
/// Es la hoja del programa viejo, que es la que el dueno llama perfecta (DECISIONES.md,
/// 2026-09-03). Lo que se copia de alli, y por que cada cosa: cinco filas de cabecera antes
/// de la tabla —lo que es igual para todas las filas se dice una vez arriba—; la fecha
/// limite sola y en rojo, una semana antes del viaje, porque si falta algo hace falta
/// tiempo para hablar con el lider; la clave A LA VISTA en gris pequeno; y fondo en lo que
/// rellena el companero, con menu de dos opciones.
/// </para>
/// <para>
/// ⚠️ <b>Nada de esta hoja va bloqueado desde el 2026-09-07</b>, por orden del dueno: «no
/// bloquees las celdas por favor, de los paquetes». Se quitaron las DOS mitades del bloqueo
/// —la marca de celda y la proteccion de la hoja— y hacia falta quitar las dos: en OOXML la
/// marca no impide nada mientras la hoja no este protegida, pero una hoja protegida bloquea
/// todo lo que no diga lo contrario, porque el valor por defecto de una celda es «bloqueada».
/// Dejar las marcas puestas seria una trampa para el dia que alguien proteja la hoja.
/// </para>
/// <para>
/// Lo que aquel bloqueo protegia era el par <c>numero_caso</c> + <c>mrn</c>, y ya no
/// reconcilia: desde el 2026-09-03 la hoja lleva la columna <c>clave</c> y la vuelta casa por
/// ella. La defensa de verdad siempre estuvo ahi —la fila cuya clave no casa no se aplica— y
/// esta medida en <c>PruebasDeLaClaveEstropeada</c>.
/// </para>
/// </remarks>
public static class LibroDeTrabajo
{
    /// <summary>La recomendacion tiene que estar lista antes de que salga el grupo, no el mismo dia.</summary>
    public const int DiasDeMargen = 7;

    /// <summary>La tinta del titulo y de la fila de titulos.</summary>
    public const string Tinta = "#16233A";

    /// <summary>El fondo de lo que rellena el companero.</summary>
    public const string Piel = "#FFF6DC";

    /// <summary>La linea fina que separa las filas de datos.</summary>
    public const string Linea = "#DBDAD2";

    /// <summary>Solo la fecha limite.</summary>
    public const string Rojo = "#A62E24";

    /// <summary>El gris del subtitulo de templo y salida.</summary>
    public const string GrisDelSubtitulo = "#4B5872";

    /// <summary>El gris de la clave, que va a la vista pero sin robar atencion.</summary>
    public const string GrisDeLaClave = "#8A93A5";

    /// <summary>El titulo de la hoja, en la fila 1.</summary>
    public const string Titulo = "Preparación para las ordenanzas";

    /// <summary>Lo que decia la instruccion del programa viejo, letra por letra.</summary>
    /// <remarks>Se guarda aparte para poder seguir comparandola contra la hoja del Python.</remarks>
    public const string InstruccionDelViejo =
        "Busca a cada persona en el sistema, entra en «Preparación para las ordenanzas» "
        + "y marca Sí o No en cada paso, tal como lo veas. Si algo no está completo, llama "
        + "al líder de su barrio y ayúdalo a terminarlo.";

    /// <summary>
    /// Lo que se anadio el 2026-09-05: donde decir que NO se pudo.
    /// </summary>
    /// <remarks>
    /// Sin esta frase las dos columnas nuevas estaban en la hoja pero nadie las nombraba, y
    /// una casilla que no se explica se queda vacia. Es la mitad que le faltaba a la
    /// instruccion vieja: decia que hacer cuando se consigue, y no decia nada de cuando no.
    /// </remarks>
    public const string InstruccionDeCuandoNoSePudo =
        " Si no lo conseguiste, dilo en «" + MotivosDeLaHoja.RotuloDelMotivo + "» y cuenta "
        + "lo que pasó en «" + MotivosDeLaHoja.RotuloDelComentario + "»: eso es lo que "
        + "vuelve al sistema.";

    /// <summary>
    /// Lo que se anadio el 2026-09-16: que lo que ya viene marcado lo puso el sistema, y que
    /// borrarlo no lo quita.
    /// </summary>
    /// <remarks>
    /// Sin esta frase el companero ve celdas amarillas ya rellenas y no sabe si alguien se
    /// las dejo por error. Y la segunda mitad es la regla de la vuelta dicha a quien la
    /// sufre: una celda que borre se conserva como estaba, asi que para cambiar un «Sí» tiene
    /// que escribir «No» (ver <see cref="Pasos.ConLoQueYaEstabaGuardado"/>).
    /// </remarks>
    public const string InstruccionDeLoQueYaVieneMarcado =
        " Lo que ya venga marcado con Sí o No lo tenía el sistema antes de darte esta hoja: "
        + "déjalo si sigue igual y escribe encima si ves otra cosa; borrarlo no lo quita. "
        + "Lo que esté en blanco es lo que falta por mirar.";

    /// <summary>La instruccion de la fila 5, que la lee una persona que no es Miguel.</summary>
    public const string Instruccion = InstruccionDelViejo + InstruccionDeCuandoNoSePudo + InstruccionDeLoQueYaVieneMarcado;

    /// <summary>Lo que se escribe cuando la hoja no dice a quien va.</summary>
    public const string SinAgente = "sin asignar";

    /// <summary>Cómo vienen las fechas de la base (ISO-8601), en la sintaxis de <c>DateTime.TryParseExact</c>.</summary>
    private const string FormatoDeFecha = "yyyy-MM-dd";

    /// <summary>Cómo se escribe una fecha en la cabecera para que la lea una persona: día-mes-año.</summary>
    private const string FormatoQueSeLee = "dd-MM-yyyy";

    /// <summary>El formato de número de Excel que fuerza texto; salva el cero de delante del MRN y del número de unidad.</summary>
    private const string FormatoDeTexto = "@";

    /// <summary>Como se escribe una fecha en la celda; es el que estampa openpyxl.</summary>
    private const string FormatoDeFechaEnExcel = "yyyy-mm-dd";

    /// <summary>Cuerpo de la letra del título de la fila 1.</summary>
    private const int TamanoDelTitulo = 14;

    /// <summary>Cuerpo de la letra de la columna «clave»: a la vista, pero sin robar atención.</summary>
    private const int TamanoDeLaClave = 8;

    /// <summary>Alto de la fila 5, la de la instrucción, para que quepa en dos líneas.</summary>
    private const double AltoDeLaInstruccion = 30;

    /// <summary>Alto de la fila de títulos, para que los rótulos largos quepan en dos líneas.</summary>
    private const double AltoDeLaCabecera = 32;

    /// <summary>Una fecha ISO escrita como la lee una persona, o vacio si no se puede.</summary>
    /// <param name="iso">«aaaa-MM-dd» exacto; nulo o cualquier otra forma da la cadena vacía.</param>
    public static string FechaLegible(string? iso)
        => DateTime.TryParseExact(iso, FormatoDeFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha.ToString(FormatoQueSeLee, CultureInfo.InvariantCulture)
            : string.Empty;

    /// <summary>
    /// Una semana antes del viaje, escrita como la lee una persona; vacio si la fecha de
    /// salida no se puede leer. No se inventa un margen sobre una fecha que no se entiende:
    /// una fecha limite falsa es peor que ninguna, porque el companero se organiza contra ella.
    /// </summary>
    /// <param name="fechaDeSalida">«aaaa-MM-dd» exacto; nulo o cualquier otra forma da la cadena vacía.</param>
    public static string FechaLimite(string? fechaDeSalida)
        => DateTime.TryParseExact(fechaDeSalida, FormatoDeFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var salida)
            ? salida.AddDays(-DiasDeMargen).ToString(FormatoQueSeLee, CultureInfo.InvariantCulture)
            : string.Empty;

    /// <summary>
    /// Lo que va arriba de la tabla, sacado de las propias filas.
    /// </summary>
    /// <remarks>
    /// El caso se nombra solo cuando todas las filas son del mismo: un titulo que dice un
    /// caso concreto sobre una hoja que lleva tres es peor que un titulo generico. Y la
    /// fecha de salida es la MAS temprana, porque una fecha limite calculada sobre el ultimo
    /// dejaria pasar sin aviso al grupo que sale antes.
    /// </remarks>
    /// <param name="filas">Las filas de la hoja; con ninguna, la cabecera sale con todo en blanco menos el agente.</param>
    /// <param name="agente">El compañero al que se entrega; nulo cuenta como vacío.</param>
    public static CabeceraDeLaHoja CabeceraDe(IReadOnlyList<FilaDeTrabajo> filas, string agente)
    {
        var casos = filas.Select(f => f.NumeroCaso).Where(v => !string.IsNullOrEmpty(v)).Distinct().ToArray();
        var templos = filas.Select(f => f.Templo).Where(v => !string.IsNullOrEmpty(v)).Distinct().ToArray();
        var salidas = filas.Select(f => f.FechaViaje).Where(v => !string.IsNullOrEmpty(v)).OrderBy(v => v, StringComparer.Ordinal).ToArray();
        return new CabeceraDeLaHoja(
            casos.Length == 1 ? casos[0]! : string.Empty,
            templos.Length == 1 ? templos[0]! : string.Empty,
            salidas.Length > 0 ? salidas[0]! : string.Empty,
            agente ?? string.Empty);
    }

    /// <summary>El libro que se le entrega al companero. Quien lo recibe lo cierra.</summary>
    /// <param name="filas">Una por persona, en el orden en que saldrán.</param>
    /// <param name="agente">El compañero al que se entrega; va en la fila 4.</param>
    /// <param name="cabecera">Una cabecera ya decidida; nula, se deduce de las filas con <see cref="CabeceraDe"/>.</param>
    /// <returns>Un libro con una sola pestaña, <see cref="Columnas.NombreDeLaHoja"/>, sin proteger.</returns>
    public static XLWorkbook Construir(IReadOnlyList<FilaDeTrabajo> filas, string agente, CabeceraDeLaHoja? cabecera = null)
    {
        var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet(Columnas.NombreDeLaHoja);

        EscribirLasCincoLineas(hoja, cabecera ?? CabeceraDe(filas, agente));
        EscribirLosTitulos(hoja);
        var ultimaFila = EscribirLasFilas(hoja, filas);
        PonerLosMenus(hoja, ultimaFila);
        EnsancharLasColumnas(hoja);

        hoja.SheetView.FreezeRows(Columnas.FilaDeLaCabecera);
        // ⛔ Aqui iba `hoja.Protect()`, que era lo que activaba los bloqueos de celda. El dueno
        // lo quito el 2026-09-07: «no bloquees las celdas por favor, de los paquetes».
        return libro;
    }

    /// <summary>
    /// Las filas 1 a 5: título con el caso, templo y salida, fecha límite en rojo (solo si se
    /// pudo calcular), agente e instrucción. Cada una en la columna A, sin combinar celdas.
    /// </summary>
    /// <param name="hoja">La pestaña «Por verificar» recién creada.</param>
    /// <param name="cabecera">Lo que es igual para todas las filas.</param>
    private static void EscribirLasCincoLineas(IXLWorksheet hoja, CabeceraDeLaHoja cabecera)
    {
        var titulo = Titulo + (cabecera.NumeroDeCaso.Length > 0 ? $" · {cabecera.NumeroDeCaso}" : string.Empty);
        var celdaDelTitulo = hoja.Cell(1, 1);
        celdaDelTitulo.Value = titulo;
        celdaDelTitulo.Style.Font.Bold = true;
        celdaDelTitulo.Style.Font.FontSize = TamanoDelTitulo;
        celdaDelTitulo.Style.Font.FontColor = XLColor.FromHtml(Tinta);

        // La linea del templo se arma juntando solo los trozos que existen: sin templo
        // guardado sale «Sale el ...» a secas, en vez de «Templo:  · Sale el ...», que
        // parece un dato que se perdio.
        var trozos = new List<string>();
        if (cabecera.Templo.Length > 0) trozos.Add($"Templo: {cabecera.Templo}");
        var salidaLegible = FechaLegible(cabecera.FechaDeSalida);
        if (salidaLegible.Length > 0) trozos.Add($"Sale el {salidaLegible}");
        var celdaDelTemplo = hoja.Cell(2, 1);
        celdaDelTemplo.Value = string.Join(" · ", trozos);
        celdaDelTemplo.Style.Font.FontColor = XLColor.FromHtml(GrisDelSubtitulo);

        // La fila 3 se escribe SOLO si la fecha limite se puede calcular. Es preferible a
        // «Todo verificado antes del » sin fecha detras, que no dice nada y ocupa el sitio.
        var limite = FechaLimite(cabecera.FechaDeSalida);
        if (limite.Length > 0)
        {
            var celdaDelLimite = hoja.Cell(3, 1);
            celdaDelLimite.Value = $"Todo verificado antes del {limite}";
            celdaDelLimite.Style.Font.Bold = true;
            celdaDelLimite.Style.Font.FontColor = XLColor.FromHtml(Rojo);
        }

        var celdaDelAgente = hoja.Cell(4, 1);
        celdaDelAgente.Value = $"Agente: {(cabecera.Agente.Length > 0 ? cabecera.Agente : SinAgente)}";
        celdaDelAgente.Style.Font.Bold = true;
        celdaDelAgente.Style.Font.FontColor = XLColor.FromHtml(Tinta);

        var celdaDeLaInstruccion = hoja.Cell(5, 1);
        celdaDeLaInstruccion.Value = Instruccion;
        celdaDeLaInstruccion.Style.Alignment.WrapText = true;
        celdaDeLaInstruccion.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        hoja.Row(5).Height = AltoDeLaInstruccion;
    }

    /// <summary>La fila 6: los títulos de <see cref="Columnas.Todas"/> en blanco sobre tinta, sin bloquear.</summary>
    /// <param name="hoja">La pestaña «Por verificar».</param>
    private static void EscribirLosTitulos(IXLWorksheet hoja)
    {
        var titulos = Columnas.Titulos();
        for (var numero = 1; numero <= titulos.Count; numero++)
        {
            var celda = hoja.Cell(Columnas.FilaDeLaCabecera, numero);
            celda.Value = titulos[numero - 1];
            celda.Style.Font.Bold = true;
            celda.Style.Font.FontColor = XLColor.White;
            celda.Style.Fill.BackgroundColor = XLColor.FromHtml(Tinta);
            celda.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            celda.Style.Alignment.WrapText = true;
            // Ni el titulo: la hoja entera queda libre desde el 2026-09-07.
            celda.Style.Protection.Locked = false;
        }
        hoja.Row(Columnas.FilaDeLaCabecera).Height = AltoDeLaCabecera;
    }

    /// <summary>Escribe las filas y devuelve el numero de la ultima; la cabecera si no hay ninguna.</summary>
    /// <param name="hoja">La pestaña «Por verificar».</param>
    /// <param name="filas">Una por persona; la primera va en <see cref="Columnas.PrimeraFilaDeDatos"/>.</param>
    private static int EscribirLasFilas(IXLWorksheet hoja, IReadOnlyList<FilaDeTrabajo> filas)
    {
        var numeroDeFila = Columnas.FilaDeLaCabecera;
        for (var indice = 0; indice < filas.Count; indice++)
        {
            numeroDeFila = Columnas.PrimeraFilaDeDatos + indice;
            for (var numeroDeColumna = 1; numeroDeColumna <= Columnas.Todas.Count; numeroDeColumna++)
            {
                var columna = Columnas.Todas[numeroDeColumna - 1];
                var valor = filas[indice].ValorDe(columna.Nombre);
                // Lo que el programa no sabe se dice con palabras y no con un hueco. Un hueco
                // lo lee el companero como «esto lo relleno yo», y las unicas celdas que
                // rellena el son las de fondo amarillo. La excepcion es «A qué va»: ahi el
                // blanco es lo que dice el papel (dueno, 2026-09-16: «"no consta" no es una
                // respuesta»).
                if (valor is null && !columna.EsRespuesta && !columna.EnBlancoSiFalta)
                    valor = Columnas.SinDato;
                // La nota solo acompana a una respuesta que sale escrita: dice quien la
                // contesto y cuando, para que el companero sepa que no es un error.
                var nota = valor is null ? null : filas[indice].NotaDe(columna.Nombre);
                EscribirCelda(hoja, numeroDeFila, numeroDeColumna, columna, valor, nota);
            }
        }
        return numeroDeFila;
    }

    /// <summary>
    /// Una celda de datos entera: el valor, el formato de texto o de fecha, la marca de
    /// bloqueo (siempre a «libre» desde el 2026-09-07), la línea de abajo, el fondo si es
    /// respuesta, la letra pequeña y gris si es la clave, y la nota si la lleva.
    /// </summary>
    /// <param name="hoja">La pestaña «Por verificar».</param>
    /// <param name="fila">El número de fila de Excel, base 1.</param>
    /// <param name="numeroDeColumna">El número de columna de Excel, base 1.</param>
    /// <param name="columna">La definición de la columna, que decide todo lo demás.</param>
    /// <param name="valor">El texto a escribir, o nulo para dejar la celda vacía con su estilo.</param>
    /// <param name="nota">El comentario de celda, o nulo si no lleva; solo lo llevan las respuestas que salen contestadas.</param>
    private static void EscribirCelda(
        IXLWorksheet hoja, int fila, int numeroDeColumna, ColumnaDeLaHoja columna, string? valor, string? nota)
    {
        var celda = hoja.Cell(fila, numeroDeColumna);
        PonerElValor(celda, columna, valor);
        if (nota is not null)
            PonerLaNota(celda, nota);

        if (columna.Clase == ClaseDeColumna.Texto)
            celda.Style.NumberFormat.Format = FormatoDeTexto;
        // El formato de fecha se escribe a mano y no se deja al que la biblioteca ponga sola:
        // openpyxl estampa `yyyy-mm-dd` y ClosedXML no estampa ninguno, y entonces la fecha se
        // ve segun la configuracion regional de la maquina del companero. Medido comparando la
        // hoja del Python con la del C#: es la unica diferencia de formato que salio.
        if (columna.Clase == ClaseDeColumna.Temporal && celda.DataType == XLDataType.DateTime)
            celda.Style.NumberFormat.Format = FormatoDeFechaEnExcel;
        // Se escribe a mano y no se deja al valor por defecto: en Excel una celda nace
        // «bloqueada», asi que callarse aqui dejaria la hoja lista para bloquearse sola el dia
        // que alguien la proteja.
        celda.Style.Protection.Locked = !columna.EsEditable;
        celda.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        celda.Style.Border.BottomBorderColor = XLColor.FromHtml(Linea);
        if (columna.EsRespuesta)
            celda.Style.Fill.BackgroundColor = XLColor.FromHtml(Piel);
        if (columna.Nombre == Columnas.ColumnaDeLaClave)
        {
            celda.Style.Font.FontSize = TamanoDeLaClave;
            celda.Style.Font.FontColor = XLColor.FromHtml(GrisDeLaClave);
        }
    }

    /// <summary>
    /// Mete el valor en la celda segun la clase de su columna.
    /// </summary>
    /// <remarks>
    /// Una fecha que no se puede leer se deja tal cual: no se adivina ni se descarta. La
    /// regla permanente 1 prohibe inventar un dato, y una fecha inventada en el papel que
    /// lleva un companero es peor que una fecha fea.
    /// <para>
    /// Todo lo demas entra con <c>SetValue(string)</c> y NO con <c>Value =</c>: lo segundo
    /// deja que ClosedXML adivine el tipo, y un nombre leido por OCR que empiece por «=» se
    /// convertiria en formula. Un nombre no es una formula.
    /// </para>
    /// </remarks>
    /// <param name="celda">La celda de destino.</param>
    /// <param name="columna">Solo importa su clase: temporal intenta leer la fecha, las demás escriben texto.</param>
    /// <param name="valor">El texto; nulo no escribe nada.</param>
    private static void PonerElValor(IXLCell celda, ColumnaDeLaHoja columna, string? valor)
    {
        if (valor is null)
            return;
        if (columna.Clase == ClaseDeColumna.Temporal
            && DateTime.TryParseExact(valor, FormatoDeFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
        {
            celda.Value = fecha;
            return;
        }
        celda.SetValue(valor);
    }

    /// <summary>
    /// El comentario de una celda que sale ya contestada: quien la contesto, cuando y desde
    /// donde. Se ve al pasar el raton y no ocupa ninguna columna.
    /// </summary>
    /// <remarks>
    /// Va como comentario y no como columna aparte porque la hoja ya es ancha
    /// (<see cref="Columnas.Todas"/>), y una columna mas que solo importa cuando la celda
    /// no esta vacia seria ruido en las filas que van enteras en blanco. La vuelta no lee comentarios (<see cref="LectorDeExcel"/> lee
    /// valores), asi que la nota no puede descolocar nada.
    /// </remarks>
    /// <param name="celda">La celda de respuesta que sale escrita.</param>
    /// <param name="nota">El texto, ya en palabras.</param>
    private static void PonerLaNota(IXLCell celda, string nota)
    {
        var comentario = celda.CreateComment();
        comentario.AddText(nota);
        comentario.Style.Alignment.AutomaticSize = true;
    }

    /// <summary>
    /// Un menu en cada columna que lo tiene: uno por columna y no uno compartido, para que
    /// el archivo se pueda mirar por dentro cuando una columna deje de ofrecerlo.
    /// </summary>
    /// <remarks>
    /// El menu NO rechaza lo que se escriba a mano, y es a proposito: es una ayuda, no una
    /// reja. Lo sostiene la vuelta, que lee una respuesta escrita a mano igual y avisa con
    /// su numero de fila si no la entiende. Bloquear la celda obligaria al companero a
    /// dejarla vacia cuando la realidad no cabe en dos opciones, y una celda vacia es peor:
    /// no distingue «no aplica» de «no lo mire».
    /// <para>
    /// El comentario NO lleva menu, y esa es toda la diferencia entre las dos columnas
    /// nuevas: el motivo es una de tres cosas que el dueno nombro, y el comentario es lo
    /// que no cabe en ninguna lista.
    /// </para>
    /// </remarks>
    /// <param name="hoja">La pestaña «Por verificar».</param>
    /// <param name="ultimaFila">Hasta dónde llega la tabla; si no hay filas de datos no se pone ningún menú.</param>
    private static void PonerLosMenus(IXLWorksheet hoja, int ultimaFila)
    {
        if (ultimaFila < Columnas.PrimeraFilaDeDatos)
            return;

        for (var numero = 1; numero <= Columnas.Todas.Count; numero++)
        {
            var lista = ListaDelMenu(Columnas.Todas[numero - 1].Respuesta);
            if (lista is null)
                continue;
            var validacion = hoja.Range(Columnas.PrimeraFilaDeDatos, numero, ultimaFila, numero).CreateDataValidation();
            validacion.List(lista, inCellDropdown: true);
            validacion.IgnoreBlanks = true;
            validacion.ShowErrorMessage = false;
        }
    }

    /// <summary>La formula del menu de esa clase de respuesta, o nulo si no lleva menu.</summary>
    /// <param name="respuesta">La clase de respuesta de la columna; solo sí/no y motivo llevan menú.</param>
    /// <returns>Las opciones entre comillas y separadas por comas, que es como Excel guarda una lista en línea.</returns>
    private static string? ListaDelMenu(ClaseDeRespuesta respuesta) => respuesta switch
    {
        ClaseDeRespuesta.SiONo => "\"" + string.Join(",", Pasos.Respuestas) + "\"",
        ClaseDeRespuesta.Motivo => "\"" + string.Join(",", MotivosDeLaHoja.Opciones) + "\"",
        _ => null,
    };

    /// <summary>Deja cada columna con el ancho de su contenido. Un MRN estrecho sale «#####».</summary>
    /// <param name="hoja">La pestaña «Por verificar».</param>
    private static void EnsancharLasColumnas(IXLWorksheet hoja)
    {
        for (var numero = 1; numero <= Columnas.Todas.Count; numero++)
            hoja.Column(numero).Width = Columnas.AnchoDe(Columnas.Todas[numero - 1].Nombre);
    }
}
