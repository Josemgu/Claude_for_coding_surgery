using System.Globalization;
using ClosedXML.Excel;
using Fichas.Contratos.Modelos;

namespace Fichas.Paquetes;

/// <summary>Una fila del archivo, con el numero de fila DE EXCEL y sus celdas ya en texto.</summary>
/// <param name="Numero">El numero de fila de Excel, base 1; es lo que hace falta para ir a mirarla.</param>
/// <param name="Valores">Las celdas, en texto limpio; nulo donde no habia nada.</param>
public sealed record FilaLeida(int Numero, IReadOnlyList<string?> Valores);

/// <summary>Lo que se saco de un `.xlsx`: sus titulos, sus filas y lo que hay que decir.</summary>
/// <param name="Hoja">Como se llama la pestana que se leyo de verdad.</param>
/// <param name="Titulos">La fila de titulos, en texto.</param>
/// <param name="Filas">Las filas de datos, con su numero de fila de Excel.</param>
/// <param name="Avisos">Lo que hay que senalar en la franja.</param>
/// <param name="FilaDeTitulos">En que fila se encontraron los titulos, para poder comprobarlo.</param>
public sealed record LibroLeido(
    string Hoja,
    IReadOnlyList<string?> Titulos,
    IReadOnlyList<FilaLeida> Filas,
    IReadOnlyList<Aviso> Avisos,
    int FilaDeTitulos);

/// <summary>El archivo no se pudo abrir o no tiene forma de tabla.</summary>
public sealed class ErrorDeLectura : Exception
{
    /// <summary>Crea el error con su frase en espanol.</summary>
    /// <param name="mensaje">La frase que verá Miguel, con el nombre del archivo dentro.</param>
    public ErrorDeLectura(string mensaje) : base(mensaje) { }

    /// <summary>Crea el error con su frase en espanol y lo que dijo el sistema debajo.</summary>
    /// <param name="mensaje">La frase que verá Miguel.</param>
    /// <param name="causa">La excepción de ClosedXML o del sistema de archivos, para no perder su detalle.</param>
    public ErrorDeLectura(string mensaje, Exception causa) : base(mensaje, causa) { }
}

/// <summary>
/// Leer un `.xlsx` que vuelve, sea el de trabajo o uno cualquiera de Miguel.
/// </summary>
/// <remarks>
/// NO decide nada sobre los datos: los saca del archivo y los deja en texto. Quien decide
/// que hacer con ellos es <see cref="Reconciliacion"/>. Estan separados a proposito.
/// <para>
/// ⚠️ Lo que se lee se normaliza a texto, y ahi hay un peligro concreto: si el companero
/// teclea un MRN sobre una celda cuyo formato se perdio, Excel lo puede devolver como
/// numero —<c>5511113</c> en vez de <c>055-1111-3853</c>—. Ese valor no casa, y NO se
/// arregla aqui: rellenar ceros seria inventar un MRN, que es lo que la regla permanente 1
/// prohibe. Se deja como llego y la fila cae en descartados con su motivo.
/// </para>
/// </remarks>
public static class LectorDeExcel
{
    /// <summary>Donde estan los titulos cuando la hoja no dice donde estan.</summary>
    public const int FilaDeTitulosPorDefecto = 1;

    /// <summary>
    /// Hasta que fila se busca la fila de titulos. La hoja «Por verificar» la trae en la 6, y
    /// el tope deja aire por si alguien anade una linea a la cabecera. Mas abajo de eso, lo
    /// que se encontrara no es una cabecera sino una fila de datos que dice «clave».
    /// </summary>
    public const int UltimaFilaDondeSeBuscaLaCabecera = 30;

    /// <summary>
    /// Tope de filas que se leen de un archivo. Un `.xlsx` con la columna entera formateada
    /// trae un millon de filas vacias, y recorrerlas cuelga la ventana sin decir por que.
    /// 20 000 filas son mas personas de las que este programa vera nunca.
    /// </summary>
    public const int MaximoDeFilas = 20_000;

    /// <summary>ISO-8601 sin hora: una fecha de Excel vuelve así porque así la guarda la base. La hora, si la tenía, se pierde.</summary>
    private const string FormatoDeFecha = "yyyy-MM-dd";

    /// <summary>
    /// El valor de una celda como texto limpio, o nulo si no hay nada.
    /// </summary>
    /// <remarks>
    /// Un entero llega de Excel como decimal cuando la celda tuvo formato de numero: <c>3.0</c>
    /// en vez de <c>3</c>. Se recorta a entero solo cuando no pierde nada, para que una fila
    /// <c>3</c> no se convierta en el texto <c>«3.0»</c> y deje de casar. Y una fecha se
    /// devuelve en ISO-8601, que es como las guarda toda la base.
    /// </remarks>
    /// <param name="celda">Cualquier celda; una vacía o con solo blancos da nulo.</param>
    /// <returns>Fecha en «aaaa-MM-dd», booleano como «1»/«0», número sin «.0» si es entero, o el texto recortado.</returns>
    public static string? TextoDeCelda(IXLCell celda)
    {
        if (celda.IsEmpty())
            return null;

        var valor = celda.Value;
        string texto;
        if (valor.IsDateTime)
            texto = valor.GetDateTime().ToString(FormatoDeFecha, CultureInfo.InvariantCulture);
        else if (valor.IsBoolean)
            texto = valor.GetBoolean() ? "1" : "0";
        else if (valor.IsNumber)
        {
            var numero = valor.GetNumber();
            texto = numero == Math.Floor(numero) && Math.Abs(numero) < 1e15
                ? ((long)numero).ToString(CultureInfo.InvariantCulture)
                : numero.ToString(CultureInfo.InvariantCulture);
        }
        else
            texto = celda.GetString();

        texto = texto.Trim();
        return texto.Length == 0 ? null : texto;
    }

    /// <summary>
    /// Abre el `.xlsx` y devuelve sus titulos y sus filas, sin interpretarlas.
    /// </summary>
    /// <remarks>
    /// Las filas totalmente vacias se saltan y no cuentan como descartadas: son el resto de
    /// haber borrado el contenido de una fila.
    /// </remarks>
    /// <param name="ruta">Ruta del <c>.xlsx</c> en disco.</param>
    /// <param name="nombreDeLaHoja">La pestaña que se busca; vacía o ausente, se lee la primera con aviso.</param>
    /// <exception cref="ErrorDeLectura">El archivo no existe, no se abre como libro, o la fila de títulos está toda en blanco.</exception>
    public static LibroLeido Leer(string ruta, string nombreDeLaHoja = Columnas.NombreDeLaHoja)
    {
        if (!File.Exists(ruta))
            throw new ErrorDeLectura($"No existe el archivo «{ruta}».");

        XLWorkbook libro;
        try
        {
            libro = new XLWorkbook(ruta);
        }
        catch (Exception causa)
        {
            throw new ErrorDeLectura(
                $"No se pudo abrir «{Path.GetFileName(ruta)}» como libro de Excel: {causa.Message} "
                + "Compruebe que es un archivo .xlsx y que no está dañado.", causa);
        }

        using (libro)
        {
            var (hoja, avisos) = ElegirHoja(libro, nombreDeLaHoja);
            var filaDeTitulos = BuscarLaFilaDeTitulos(hoja);
            var titulos = TitulosDeUnaFila(hoja, filaDeTitulos);
            if (!titulos.Any(titulo => titulo is not null))
                throw new ErrorDeLectura(
                    $"La hoja «{hoja.Name}» de «{Path.GetFileName(ruta)}» no tiene títulos en la "
                    + $"fila {filaDeTitulos}, así que no se sabe qué es cada columna.");

            var filas = LeerLasFilas(hoja, filaDeTitulos + 1, titulos.Count, avisos);
            return new LibroLeido(hoja.Name, titulos, filas, avisos, filaDeTitulos);
        }
    }

    /// <summary>Una fila leida como diccionario «titulo → valor».</summary>
    /// <remarks>
    /// Un titulo repetido se queda con el valor de la PRIMERA columna que lo lleva. Es
    /// arbitrario y hay que elegir algo; se elige la primera porque es la que ve quien mira
    /// el archivo de izquierda a derecha.
    /// </remarks>
    /// <param name="titulos">La fila de títulos tal como salió de <see cref="Leer"/>; los nulos no entran al diccionario.</param>
    /// <param name="fila">La fila a convertir; si tiene menos celdas que títulos, las que faltan no aparecen.</param>
    public static IReadOnlyDictionary<string, string?> FilaPorTitulo(IReadOnlyList<string?> titulos, FilaLeida fila)
    {
        var valores = new Dictionary<string, string?>();
        for (var indice = 0; indice < titulos.Count && indice < fila.Valores.Count; indice++)
        {
            var titulo = titulos[indice];
            if (titulo is not null && !valores.ContainsKey(titulo))
                valores[titulo] = fila.Valores[indice];
        }
        return valores;
    }

    /// <summary>
    /// La hoja pedida; si no esta, la activa, con su aviso.
    /// </summary>
    /// <remarks>
    /// Caer a la hoja activa no es tragarse un fallo: un companero puede guardar el archivo
    /// desde otro programa que renombre la pestana, y perder la ronda entera por el nombre de
    /// una pestana seria desproporcionado. Lo que no se hace es callarlo.
    /// </remarks>
    /// <param name="libro">El libro ya abierto.</param>
    /// <param name="nombreDeLaHoja">La pestaña pedida; con la cadena vacía se va directo a la primera.</param>
    /// <returns>La hoja y una lista de avisos: vacía si era la pedida, con uno si se cayó a la primera.</returns>
    private static (IXLWorksheet Hoja, List<Aviso> Avisos) ElegirHoja(XLWorkbook libro, string nombreDeLaHoja)
    {
        if (nombreDeLaHoja.Length > 0 && libro.TryGetWorksheet(nombreDeLaHoja, out var pedida))
            return (pedida, []);

        var hoja = libro.Worksheets.First();
        return (hoja, [Aviso.Advierte(
            $"El archivo no tiene ninguna hoja llamada «{nombreDeLaHoja}».",
            string.Empty,
            $"Se leyó la hoja «{hoja.Name}», que es la primera del libro. Compruebe que es la correcta.")]);
    }

    /// <summary>
    /// En que fila estan los titulos. Devuelve la 1 si no encuentra otra cosa.
    /// </summary>
    /// <remarks>
    /// ⚠️ NO se da por sabido que los titulos esten en la fila 1, y no es una concesion: la
    /// hoja «Por verificar» lleva CINCO filas de cabecera encima y los titulos empiezan en la
    /// 6. Leer la fila 1 devolveria el titulo del documento como si fuera una lista de
    /// columnas, y todas las filas se irian a descartados. Se busca por la columna «clave»,
    /// que es la unica que no puede faltar en una hoja de este programa; un archivo que
    /// Miguel arme por su cuenta no la tiene y cae a la fila 1, que es donde el la habra puesto.
    /// </remarks>
    /// <param name="hoja">La pestaña elegida.</param>
    /// <returns>La primera fila, hasta <see cref="UltimaFilaDondeSeBuscaLaCabecera"/>, con una celda que diga «clave»; si no, <see cref="FilaDeTitulosPorDefecto"/>.</returns>
    private static int BuscarLaFilaDeTitulos(IXLWorksheet hoja)
    {
        var tituloDeLaClave = Columnas.Por(Columnas.ColumnaDeLaClave).Titulo.Trim().ToLowerInvariant();
        var tope = Math.Min(Math.Max(hoja.LastRowUsed()?.RowNumber() ?? 1, 1), UltimaFilaDondeSeBuscaLaCabecera);
        for (var numero = 1; numero <= tope; numero++)
            if (TitulosDeUnaFila(hoja, numero).Any(titulo => (titulo ?? string.Empty).Trim().ToLowerInvariant() == tituloDeLaClave))
                return numero;
        return FilaDeTitulosPorDefecto;
    }

    /// <summary>Las celdas de una fila como texto, desde la columna 1 hasta la última usada de la hoja.</summary>
    /// <param name="hoja">La pestaña elegida.</param>
    /// <param name="numero">El número de fila de Excel, base 1.</param>
    /// <returns>Una entrada por columna usada, nula donde la celda está vacía; vacía si la hoja no tiene columnas.</returns>
    private static List<string?> TitulosDeUnaFila(IXLWorksheet hoja, int numero)
    {
        var ultima = hoja.LastColumnUsed()?.ColumnNumber() ?? 0;
        var titulos = new List<string?>(ultima);
        for (var columna = 1; columna <= ultima; columna++)
            titulos.Add(TextoDeCelda(hoja.Cell(numero, columna)));
        return titulos;
    }

    /// <summary>
    /// Las filas de datos, con su número de Excel, saltando las que están todas en blanco y
    /// parando en <see cref="MaximoDeFilas"/> con un aviso.
    /// </summary>
    /// <param name="hoja">La pestaña elegida.</param>
    /// <param name="primera">La fila justo debajo de los títulos.</param>
    /// <param name="cuantasColumnas">Cuántas celdas se leen por fila: tantas como títulos.</param>
    /// <param name="avisos">La lista de avisos del libro, a la que se añade el del tope si se alcanza.</param>
    private static List<FilaLeida> LeerLasFilas(IXLWorksheet hoja, int primera, int cuantasColumnas, List<Aviso> avisos)
    {
        var filas = new List<FilaLeida>();
        var ultima = hoja.LastRowUsed()?.RowNumber() ?? 0;
        for (var numero = primera; numero <= ultima; numero++)
        {
            if (filas.Count >= MaximoDeFilas)
            {
                avisos.Add(Aviso.Advierte(
                    $"El archivo trae más de {MaximoDeFilas} filas y solo se leyeron las primeras.",
                    string.Empty,
                    "Revise que sea el archivo correcto: 20 000 filas son más personas de las que este programa verá nunca."));
                break;
            }

            var valores = new List<string?>(cuantasColumnas);
            var hayAlgo = false;
            for (var columna = 1; columna <= cuantasColumnas; columna++)
            {
                var texto = TextoDeCelda(hoja.Cell(numero, columna));
                hayAlgo |= texto is not null;
                valores.Add(texto);
            }
            if (hayAlgo)
                filas.Add(new FilaLeida(numero, valores));
        }
        return filas;
    }
}
