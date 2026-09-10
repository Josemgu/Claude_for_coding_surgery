using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;

namespace Fichas.Lectura;

/// <summary>Un campo ya resuelto, con todo lo que hizo falta para decidirlo.</summary>
/// <remarks>
/// Tiene tres datos que <see cref="CampoPropuesto"/> no lleva —<see cref="ValorOcr"/>,
/// <see cref="AnuladoPorTachon"/> y <see cref="NecesitaRevision"/>— y por eso existe.
/// Son los que permiten cumplir el requisito 9: ENSENAR lo que el papel decia cuando el
/// valor no encaja, en vez de dejar el campo vacio en silencio.
/// </remarks>
/// <param name="Valor">Lo que vale el campo con su forma, o nulo si no la tiene.</param>
/// <param name="Origen">De donde salio.</param>
/// <param name="Confianza">De 0,0 a 1,0; nula si no aplica.</param>
/// <param name="ValorOcr">Lo que la maquina leyo ahi, tal cual, encaje o no.</param>
/// <param name="AnuladoPorTachon">Si un trazo rojo cruzaba la banda.</param>
/// <param name="NecesitaRevision">Si Miguel tiene que mirarlo si o si.</param>
/// <param name="LoLeidoEsDeEsteCampo">
/// Si <see cref="ValorOcr"/> se puede atribuir a este campo. Cierto por defecto.
/// <para>Falso significa: «se leyo esto cerca, pero no se puede afirmar que sea de aqui».
/// Entonces el campo va VACIO —nunca se rellena con texto de otro sitio— y lo leido viaja
/// igual, en <c>procedencia_campo.valor_ocr</c> y en el aviso, para poder enseñarlo. Es la
/// unica forma de cumplir a la vez las dos cosas que el dueño pidio el 2026-09-07: «si no
/// pudo leerla la deja vacia» y el requisito 9, «jamas vacio en silencio».</para>
/// </param>
public sealed record CampoExtraido(
    string? Valor,
    OrigenDeCampo Origen,
    double? Confianza,
    string? ValorOcr,
    bool AnuladoPorTachon,
    bool NecesitaRevision,
    bool LoLeidoEsDeEsteCampo = true);

/// <summary>
/// La precedencia de valores, que es lo que la lectura existe para construir.
/// </summary>
/// <remarks>
/// Portado de `extraccion/campos.py`. El problema real, y esta en los siete documentos
/// del dueno: en la fila «Date traveling to the temple» el papel dice «September 7,
/// 2026», encima hay un trazo rojo que lo cruza, y al lado una anotacion que dice «8
/// Sept 2026». El OCR lee las dos fechas y no tiene forma de saber cual vale. La capa de
/// anotaciones si lo sabe.
///
/// <para>El orden, tal como lo fija `DECISIONES.md`:</para>
/// <list type="number">
///   <item>Un tachon que solapa la banda ANULA lo que el OCR leyo ahi.</item>
///   <item>Una correccion escrita en la misma banda gana, con origen anotacion y confianza 1,0.</item>
///   <item>Si no hay ninguna de las dos, vale el OCR con la confianza que dio.</item>
///   <item>Si hubo tachon y NO hay correccion, el campo queda VACIO y marcado. Nunca se
///         recupera el valor tachado: alguien lo tacho por algo.</item>
/// </list>
///
/// <para>El punto 4 es el que hay que defender cuando alguien proponga «aprovechar» el
/// texto de debajo del tachon. Si se aprovecha, el sistema guarda justo el dato que una
/// persona marco como equivocado.</para>
/// </remarks>
public static class Campos
{
    /// <summary>Confianza de una correccion escrita: no es una lectura, es un dato exacto del PDF.</summary>
    public const double ConfianzaDeUnaAnotacion = 1.0;

    /// <summary>Un campo que no se pudo leer. Se marca para revision, no se rellena.</summary>
    public static CampoExtraido CampoVacio(
        string? valorOcr = null, bool anuladoPorTachon = false, bool loLeidoEsDeEsteCampo = true)
        => new(null, OrigenDeCampo.Vacio, null, valorOcr, anuladoPorTachon,
            NecesitaRevision: true, LoLeidoEsDeEsteCampo: loLeidoEsDeEsteCampo);

    /// <summary>
    /// Junta el texto crudo del OCR de una banda, de izquierda a derecha.
    /// </summary>
    /// <remarks>
    /// Se guarda entero aunque luego no se use: existe para que Miguel vea QUE leyo la
    /// maquina cuando corrija, y un texto recortado no le sirve para decidir.
    /// </remarks>
    private static string? TextoDeLasLineas(IReadOnlyList<LineaDeOcr> lineas)
    {
        var partes = lineas
            .Where(linea => !string.IsNullOrWhiteSpace(linea.Texto))
            .Select(linea => linea.Texto.Trim());
        string junto = string.Join(" ", partes);
        return junto.Length == 0 ? null : junto;
    }

    /// <summary>
    /// La correccion que de verdad corrige ESTE campo, o nula.
    /// </summary>
    /// <remarks>
    /// Una anotacion solo cuenta como correccion del campo si su texto produce un valor
    /// con la forma que el campo pide. Una que no la produce esta corrigiendo otra cosa
    /// de la misma fila.
    ///
    /// <para>El caso medido que lo obliga: en la fila «Ward/Branch Name and Unit Number»
    /// de un formulario real hay una anotacion con el NOMBRE de la unidad y nada mas.
    /// Tomandola como correccion del NUMERO, el numero —que el OCR habia leido perfecto—
    /// se perdia y el campo salia vacio.</para>
    ///
    /// <para>Con varias validas gana la de mas a la izquierda: es una regla fija, para no
    /// dejarlo al azar del orden en que el PDF las guarde.</para>
    ///
    /// <para><b>Y una nota escrita a mano le gana a un campo tecleado del formulario</b>
    /// (2026-09-10), este donde este. El campo tecleado es lo que alguien relleno en el
    /// ordenador; la nota es lo que otra persona escribio encima DESPUES, para corregirlo. Si
    /// decidiera la posicion, una nota puesta a la derecha del campo perderia contra el valor
    /// que viene a corregir.</para>
    /// </remarks>
    private static AnotacionDelPdf? MejorCorreccion(
        IReadOnlyList<AnotacionDelPdf> correcciones, Func<string?, string?> darForma)
        => correcciones
            .Where(c => !string.IsNullOrWhiteSpace(c.Texto) && darForma(c.Texto) is not null)
            .OrderBy(c => Anotaciones.EsCorreccionAMano(c) ? 0 : 1)
            .ThenBy(c => c.Banda.X0)
            .FirstOrDefault();

    /// <summary>
    /// Lo tecleado en el campo del formulario de esta banda, tal cual, o nulo si no hay ninguno.
    /// </summary>
    /// <remarks>Con varios, el de mas a la izquierda, por la misma regla fija de arriba.</remarks>
    private static string? TextoTecleado(IReadOnlyList<AnotacionDelPdf> correcciones)
        => correcciones
            .Where(Anotaciones.EsCampoTecleado)
            .OrderBy(c => c.Banda.X0)
            .Select(c => c.Texto)
            .FirstOrDefault();

    /// <summary>
    /// Aplica la precedencia y devuelve el campo con su procedencia.
    /// </summary>
    /// <remarks>
    /// Pasa de 30 lineas y NO se parte, a proposito: es el arbol de precedencia entero, y
    /// su valor esta en que las cinco ramas —correccion, tachon sin correccion, tecleado
    /// sin forma, OCR, y nada— se leen seguidas y en orden. Repartidas en cinco funciones,
    /// comprobar que el orden es el correcto obliga a saltar entre ellas, que es justo
    /// donde se cuelan los errores de precedencia. Lo que si esta fuera, porque son
    /// decisiones separables, es que texto cuenta como correccion y como se junta el texto
    /// del OCR.
    ///
    /// <para><b>La rama del tecleado sin forma</b> (2026-09-10): un campo del formulario
    /// rellenable con algo tecleado que NO pasa la forma del campo no se inventa ni se
    /// recorta, y tampoco se calla. Va vacio de valor con lo tecleado a la vista, por la
    /// misma rama que salva las cedulas terminadas en letra, y asi llega a Correccion sin
    /// la confianza de una lectura limpia. Va ANTES que el OCR porque, en un formulario
    /// rellenado a maquina, lo que el OCR lee en esa banda es la pintura del propio campo:
    /// preferir la lectura al texto exacto seria quedarse con la copia en vez del original.</para>
    /// </remarks>
    /// <param name="normalizar">
    /// Convierte el texto en el valor con formato. Si devuelve nulo, el campo queda sin
    /// valor y marcado —se leyo algo pero no tenia la forma esperada—, y lo leido viaja
    /// en <see cref="CampoExtraido.ValorOcr"/>. Nunca se pierde en silencio.
    /// </param>
    /// <param name="lineasQueNoSonDeEsteCampo">
    /// Lo que se leyo CERCA y no se puede atribuir a este campo: lineas que cruzan su banda
    /// pero no caben en ella, o el resto del renglon de una persona cuando la cedula no
    /// esta en el. Su texto <b>nunca</b> sale como valor; se conserva para poder enseñarlo
    /// y solo se mira cuando la banda propia no dio nada.
    /// </param>
    public static CampoExtraido ResolverCampo(
        IReadOnlyList<LineaDeOcr> lineasOcr,
        IReadOnlyList<AnotacionDelPdf> correcciones,
        bool hayTachon,
        Func<string?, string?>? normalizar = null,
        IReadOnlyList<LineaDeOcr>? lineasQueNoSonDeEsteCampo = null)
    {
        var darForma = normalizar ?? (texto => string.IsNullOrEmpty(texto) ? null : texto);
        string? valorOcr = TextoDeLasLineas(lineasOcr);
        string? textoAjeno = TextoDeLasLineas(lineasQueNoSonDeEsteCampo ?? []);
        string? textoTecleado = TextoTecleado(correcciones);

        var correccion = MejorCorreccion(correcciones, darForma);
        if (correccion is not null)
        {
            return new CampoExtraido(
                Valor: darForma(correccion.Texto),
                Origen: OrigenDeCampo.Anotacion,
                Confianza: ConfianzaDeUnaAnotacion,
                ValorOcr: valorOcr,
                AnuladoPorTachon: hayTachon,
                NecesitaRevision: false);
        }

        // Hubo tachon y nadie escribio el valor bueno. El dato de debajo esta marcado
        // como equivocado y NO se usa. Si debajo no habia nada suyo se enseña lo que se
        // leyo al lado: sigue siendo un tachon, y callarlo lo haria pasar por «no se leyo».
        if (hayTachon)
        {
            string? loTachado = textoTecleado ?? valorOcr;
            return loTachado is not null
                ? CampoVacio(loTachado, anuladoPorTachon: true)
                : CampoVacio(textoAjeno, anuladoPorTachon: true, loLeidoEsDeEsteCampo: textoAjeno is null);
        }

        // Hay un campo tecleado en la banda y no paso la forma (si la hubiera pasado, seria
        // la correccion de arriba). Se enseña tal cual y se manda a revision.
        if (textoTecleado is not null) return CampoVacio(textoTecleado);

        if (lineasOcr.Count > 0)
        {
            string? valor = darForma(valorOcr);
            if (valor is null) return CampoVacio(valorOcr);

            double? confianza = lineasOcr.Min(linea => linea.Confianza);
            return new CampoExtraido(
                valor, OrigenDeCampo.Ocr, confianza, valorOcr,
                AnuladoPorTachon: false, NecesitaRevision: false);
        }

        // La banda propia no trajo nada. Lo que se leyo al lado se conserva para poder
        // enseñarlo, pero NO puede ser el valor: es de otra fila o de otro campo.
        return textoAjeno is null ? CampoVacio() : CampoVacio(textoAjeno, loLeidoEsDeEsteCampo: false);
    }
}
