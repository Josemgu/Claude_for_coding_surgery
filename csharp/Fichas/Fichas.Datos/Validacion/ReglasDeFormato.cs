using System.Globalization;
using System.Text.RegularExpressions;
using Fichas.Contratos.Modelos;

namespace Fichas.Datos.Validacion;

/// <summary>
/// Las reglas de formato de DECISIONES.md, convertidas en avisos y NUNCA en paredes.
/// </summary>
/// <remarks>
/// <para>
/// Portado de <c>datos/validacion.py</c> con UN cambio de fondo, que es el requisito 9
/// del dueno: alli las reglas levantaban <c>ErrorDeValidacion</c> y el dato no se
/// guardaba; aqui devuelven un <see cref="Aviso"/> y el dato SI se guarda. El motivo
/// esta medido dos veces en este proyecto: la regla de seis digitos para
/// <c>unidad_numero</c> dejo vacias 4 de 9 paginas, y la de «11 digitos» para el MRN
/// dejo vacias 2 de 7 cedulas. Las dos veces la regla era nuestra y el dato era
/// verdadero.
/// </para>
/// <para>
/// Cada metodo devuelve <c>null</c> cuando no hay nada que decir. Es a proposito: quien
/// llama junta lo que no sea nulo y no tiene que distinguir listas vacias.
/// </para>
/// </remarks>
public static partial class ReglasDeFormato
{
    /// <summary>Un ejemplo de MRN de solo digitos, para el mensaje de error.</summary>
    public const string EjemploDeMrnConDigitos = "055-1111-3853";

    /// <summary>Un ejemplo de MRN terminado en letra, para el mismo mensaje.</summary>
    /// <remarks>
    /// Van los DOS ejemplos: uno solo de la forma que ya se aceptaba deja a quien lee
    /// el aviso sin saber que la otra tambien vale.
    /// </remarks>
    public const string EjemploDeMrnConLetra = "066-2222-133A";

    /// <summary>
    /// Tope de longitud del nombre de la unidad.
    /// </summary>
    /// <remarks>
    /// No es una regla del papel: es el limite que impide que una linea de OCR
    /// desbocada entre entera en la base y desborde la celda del Excel. El nombre mas
    /// largo de los formularios de referencia mide 17 caracteres; 120 deja sitio de
    /// sobra sin dejar la puerta abierta.
    /// </remarks>
    public const int LargoMaximoDelNombreDeUnidad = 120;

    /// <summary>Cuatro letras mayúsculas y cuatro dígitos, como <c>CASP2609</c>: la unidad y el mes.</summary>
    /// <returns>La expresión generada en compilación.</returns>
    [GeneratedRegex(@"^[A-Z]{4}[0-9]{4}$")]
    private static partial Regex PatronDeNumeroDeCaso();

    // El ultimo caracter puede ser un digito o una LETRA (DECISIONES.md, 2026-09-04).
    /// <summary>La cédula de miembro en patrón 3-4-4, con el último carácter dígito o letra.</summary>
    /// <returns>La expresión generada en compilación.</returns>
    [GeneratedRegex(@"^[0-9]{3}-[0-9]{4}-[0-9]{3}[0-9A-Za-z]$")]
    private static partial Regex PatronDeMrn();

    // 6 o 7 digitos, no 6: 4 de las 9 paginas reales traen `7000011`.
    /// <summary>El número de unidad: seis o siete dígitos.</summary>
    /// <returns>La expresión generada en compilación.</returns>
    [GeneratedRegex(@"^[0-9]{6,7}$")]
    private static partial Regex PatronDeUnidad();

    /// <summary>Una fecha <c>AAAA-MM-DD</c> por su forma; que exista lo comprueba <see cref="RevisarFechaViaje"/> con los tres grupos.</summary>
    /// <returns>La expresión generada en compilación.</returns>
    [GeneratedRegex(@"^([0-9]{4})-([0-9]{2})-([0-9]{2})$")]
    private static partial Regex PatronDeFecha();

    /// <summary>
    /// Revisa el numero de caso. Nulo es un dato, no un fallo.
    /// </summary>
    /// <remarks>
    /// Desde la version 7 del esquema, una pagina cuyo numero no se pudo leer se guarda
    /// igual, pendiente de identificar, en vez de tirarse entera con los nombres, los
    /// MRN y la fecha ya leidos. Y NO se inventa un numero: nulo significa «todavia no
    /// se sabe», y se teclea a mano (regla permanente 1).
    /// </remarks>
    /// <param name="numeroCaso">El número tal como llegó; nulo si no se leyó.</param>
    /// <returns>Nulo si es nulo o tiene la forma habitual; si no, un aviso sobre <c>numero_caso</c>.</returns>
    public static Aviso? RevisarNumeroCaso(string? numeroCaso)
    {
        if (numeroCaso is null)
        {
            return null;
        }

        if (PatronDeNumeroDeCaso().IsMatch(numeroCaso))
        {
            return null;
        }

        return Aviso.Advierte(
            "El numero de caso no tiene la forma de siempre. Se guardo igual.",
            "numero_caso",
            $"Se recibio '{Recortar(numeroCaso)}' y lo habitual son 4 letras mayusculas " +
            "seguidas de 4 digitos, como 'CASP2609'. El dato NO se rechaza: puede ser " +
            "un numero legitimo que no habiamos visto, y perderlo seria peor.");
    }

    /// <summary>
    /// Revisa la cedula de miembro. Acepta nulo: el OCR puede no haberla leido.
    /// </summary>
    /// <remarks>
    /// La letra se guarda TAL COMO LLEGA. No se sube a mayuscula: si el papel la trae
    /// en minuscula, en minuscula queda. Corregir lo leido es lo que la regla
    /// permanente 1 prohibe, y da igual que la correccion parezca inofensiva.
    /// </remarks>
    /// <param name="mrn">La cédula tal como llegó; nulo si no se leyó.</param>
    /// <returns>Nulo si es nulo o cumple el patrón 3-4-4; si no, un aviso sobre <c>mrn</c> con los dos ejemplos.</returns>
    public static Aviso? RevisarMrn(string? mrn)
    {
        if (mrn is null)
        {
            return null;
        }

        if (PatronDeMrn().IsMatch(mrn))
        {
            return null;
        }

        return Aviso.Advierte(
            "La cedula no tiene la forma esperada. Se guardo igual.",
            "mrn",
            $"Se recibio '{Recortar(mrn)}' y se espera '{EjemploDeMrnConDigitos}' o " +
            $"'{EjemploDeMrnConLetra}': el ultimo puede ser letra.");
    }

    /// <summary>Revisa el numero de unidad: 6 o 7 digitos. Acepta nulo.</summary>
    /// <param name="unidadNumero">El número de unidad tal como llegó; nulo si no se leyó.</param>
    /// <returns>Nulo si es nulo o son 6 o 7 dígitos; si no, un aviso sobre <c>unidad_numero</c>.</returns>
    public static Aviso? RevisarUnidadNumero(string? unidadNumero)
    {
        if (unidadNumero is null)
        {
            return null;
        }

        if (PatronDeUnidad().IsMatch(unidadNumero))
        {
            return null;
        }

        return Aviso.Advierte(
            "El numero de unidad no tiene la forma esperada.",
            "unidad_numero",
            $"Se recibio '{Recortar(unidadNumero)}' y se esperaban 6 o 7 digitos, " +
            "como '123456' o '7000011'.");
    }

    /// <summary>
    /// Revisa la fecha de viaje: ISO-8601 <c>AAAA-MM-DD</c> y que exista de verdad.
    /// </summary>
    /// <remarks>
    /// La forma no basta: <c>2026-02-31</c> pasa cualquier GLOB del motor y no existe.
    /// Por eso se construye la fecha de verdad antes de darla por buena. La restriccion
    /// de la base es el respaldo, no el validador.
    /// </remarks>
    /// <param name="fechaViaje">La fecha tal como llegó; nulo si no se leyó.</param>
    /// <returns>Nulo si es nulo o es un día real; un aviso distinto según falle la forma o el calendario.</returns>
    public static Aviso? RevisarFechaViaje(string? fechaViaje)
    {
        if (fechaViaje is null)
        {
            return null;
        }

        var coincidencia = PatronDeFecha().Match(fechaViaje);
        if (!coincidencia.Success)
        {
            return Aviso.Advierte(
                "La fecha de viaje no tiene la forma esperada.",
                "fecha_viaje",
                $"Se recibio '{Recortar(fechaViaje)}' y se esperaba una fecha en " +
                "formato AAAA-MM-DD, como '2026-09-08'.");
        }

        var anio = int.Parse(coincidencia.Groups[1].Value, CultureInfo.InvariantCulture);
        var mes = int.Parse(coincidencia.Groups[2].Value, CultureInfo.InvariantCulture);
        var dia = int.Parse(coincidencia.Groups[3].Value, CultureInfo.InvariantCulture);

        if (mes is < 1 or > 12
            || dia < 1
            || dia > DateTime.DaysInMonth(anio, Math.Clamp(mes, 1, 12)))
        {
            return Aviso.Advierte(
                "La fecha de viaje tiene la forma correcta pero no existe.",
                "fecha_viaje",
                $"'{fechaViaje}' no es un dia que exista en el calendario.");
        }

        return null;
    }

    /// <summary>Revisa el nombre de la unidad: texto sin espacios de sobra y no muy largo.</summary>
    /// <param name="unidadNombre">El nombre tal como llegó; se mide sin los espacios de los extremos.</param>
    /// <returns>Nulo si cabe en <see cref="LargoMaximoDelNombreDeUnidad"/>; si no, un aviso sobre <c>unidad_nombre</c>.</returns>
    public static Aviso? RevisarUnidadNombre(string? unidadNombre)
    {
        if (unidadNombre is null)
        {
            return null;
        }

        var limpio = unidadNombre.Trim();
        if (limpio.Length <= LargoMaximoDelNombreDeUnidad)
        {
            return null;
        }

        return Aviso.Advierte(
            "El nombre de la unidad es mucho mas largo de lo normal.",
            "unidad_nombre",
            $"Se recibieron {limpio.Length} caracteres y el maximo habitual son " +
            $"{LargoMaximoDelNombreDeUnidad}. Un nombre mas largo que eso casi siempre " +
            "es una linea del OCR que se colo entera.");
    }

    /// <summary>Revisa una pagina de PDF: se cuentan desde 1, no desde 0. Acepta nulo.</summary>
    /// <param name="paginaPdf">El número de página; nulo si no se sabe.</param>
    /// <param name="campo">El nombre de la columna que va en el aviso, porque <c>casos</c> y <c>personas</c> tienen cada una la suya.</param>
    /// <returns>Nulo si es nulo o es al menos 1; si no, un aviso.</returns>
    public static Aviso? RevisarPaginaPdf(int? paginaPdf, string campo = "pagina_pdf")
    {
        if (paginaPdf is null or >= 1)
        {
            return null;
        }

        return Aviso.Advierte(
            "El numero de pagina no es valido.",
            campo,
            $"Se recibio {paginaPdf} y las paginas se cuentan desde 1, no desde 0.");
    }

    /// <summary>
    /// El aviso del mes cruzado: la fecha de viaje cae en otro periodo que el numero.
    /// </summary>
    /// <remarks>
    /// Va como AVISO y no como restriccion del motor por decision del dueno
    /// (DECISIONES.md 2026-09-02, P-2): como pared haria imposible guardar un viaje
    /// reprogramado a otro mes, que es una cosa que pasa. Es la regla permanente 5
    /// —el sistema propone, Miguel confirma— aplicada a una fecha.
    /// </remarks>
    /// <param name="numeroCaso">El número de caso; sus cuatro dígitos son <c>AAMM</c>.</param>
    /// <param name="fechaViaje">La fecha de viaje en <c>AAAA-MM-DD</c>.</param>
    /// <returns>Nulo si falta alguno, si alguno no tiene forma, o si el periodo coincide; si no, un aviso sobre <c>fecha_viaje</c>.</returns>
    public static Aviso? RevisarMesCruzado(string? numeroCaso, string? fechaViaje)
    {
        if (fechaViaje is null
            || numeroCaso is null
            || !PatronDeNumeroDeCaso().IsMatch(numeroCaso)
            || !PatronDeFecha().IsMatch(fechaViaje))
        {
            return null;
        }

        var esperado = numeroCaso.Substring(4, 4);
        var encontrado = fechaViaje.Substring(2, 2) + fechaViaje.Substring(5, 2);

        if (string.Equals(esperado, encontrado, StringComparison.Ordinal))
        {
            return null;
        }

        return Aviso.Advierte(
            "La fecha de viaje cae en un periodo distinto del que dice el numero de caso.",
            "fecha_viaje",
            $"La fecha {fechaViaje} cae en el periodo {encontrado}, pero el numero " +
            $"{numeroCaso} dice {esperado}. Puede ser un viaje reprogramado: el dato se " +
            "guarda igual y hay que confirmarlo a mano.");
    }

    /// <summary>
    /// Junta en una lista los avisos que no sean nulos.
    /// </summary>
    /// <param name="avisos">Lo que devolvieron las reglas, nulos incluidos.</param>
    /// <returns>Solo los que no son nulos, en el mismo orden; vacía si no hay ninguno.</returns>
    public static IReadOnlyList<Aviso> Juntar(params Aviso?[] avisos)
    {
        ArgumentNullException.ThrowIfNull(avisos);
        return [.. avisos.Where(a => a is not null).Select(a => a!)];
    }

    /// <summary>
    /// Recorta un valor largo para que quepa en un renglon del detalle.
    /// </summary>
    /// <remarks>
    /// Requisito 4 del dueno, «ni un parrafo en pantalla»: un OCR desbocado puede traer
    /// media pagina, y pegarla entera en un aviso convierte la franja en un muro.
    /// </remarks>
    /// <param name="valor">El texto recibido.</param>
    /// <param name="largo">Cuántos caracteres se enseñan antes de los puntos suspensivos.</param>
    /// <returns>El valor entero si cabe; si no, el principio, «…» y cuántos caracteres tenía.</returns>
    private static string Recortar(string valor, int largo = 60)
        => valor.Length <= largo ? valor : valor[..largo] + "… (" + valor.Length + " caracteres)";
}
