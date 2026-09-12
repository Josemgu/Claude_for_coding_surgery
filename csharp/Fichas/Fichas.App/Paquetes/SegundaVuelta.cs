using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Paquetes;

/// <summary>Un documento que sube un peldano, con quien lo intento y por que no salio.</summary>
/// <param name="CasoId">El caso que sube.</param>
/// <param name="NumeroCaso">Su numero tal como esta escrito, para poder nombrarlo.</param>
/// <param name="QuienLoIntentoId">Quien lo llevaba en el peldano de abajo.</param>
/// <param name="QuienLoIntento">Su nombre, para el reporte de quien recibe la segunda vuelta.</param>
/// <param name="CategoriaDeQuienLoIntento">En que peldano se quedo trabado.</param>
/// <param name="Motivo">Por que lo dijo la hoja de ese companero.</param>
public sealed record CasoQueSube(
    long CasoId,
    string? NumeroCaso,
    long QuienLoIntentoId,
    string QuienLoIntento,
    int CategoriaDeQuienLoIntento,
    MotivoDeNoCompletar Motivo);

/// <summary>
/// Lo que sube a un peldano de la escalera, y las cuentas que lo explican.
/// </summary>
/// <param name="Categoria">El peldano al que se sube; el del companero que recibe el paquete.</param>
/// <param name="Entran">Los documentos que van dentro del paquete.</param>
/// <param name="ConOtraRazon">Los que dijeron «otra razón»; NO entran y se listan aparte.</param>
/// <param name="NoCompletosMirados">Cuantos documentos no completos y no archivados se miraron.</param>
/// <param name="SinMotivoEscrito">Cuantos de esos no traen ningun motivo escrito.</param>
/// <param name="YaEnEstePeldanoOMasArriba">Cuantos se trabaron en este peldano o mas arriba.</param>
public sealed record LoQueSube(
    int Categoria,
    IReadOnlyList<CasoQueSube> Entran,
    IReadOnlyList<CasoQueSube> ConOtraRazon,
    int NoCompletosMirados,
    int SinMotivoEscrito,
    int YaEnEstePeldanoOMasArriba)
{
    /// <summary>Los casos del paquete, en el orden en que se van a escribir.</summary>
    public IReadOnlyList<long> CasoIds => [.. Entran.Select(caso => caso.CasoId)];

    /// <summary>
    /// Cuantos entran de cuantos, con todo lo que se quedo fuera y por que.
    /// </summary>
    /// <remarks>
    /// Lleva el denominador y las tres exclusiones a proposito. «Entran 2» no dice nada;
    /// «2 de 5, y los otros 3 se quedaron fuera por esto» deja ver si la regla esta haciendo lo
    /// que se espera de ella antes de mandarle trabajo a nadie.
    /// </remarks>
    public string Linea
    {
        get
        {
            var cabeza = $"{Entran.Count} de {NoCompletosMirados} "
                + Plural(NoCompletosMirados, "documento no completo", "documentos no completos")
                + $" suben a la categoría {Categoria}";

            var fuera = new List<string>();
            if (ConOtraRazon.Count > 0) fuera.Add($"{ConOtraRazon.Count} con «otra razón»");
            if (SinMotivoEscrito > 0) fuera.Add($"{SinMotivoEscrito} sin motivo escrito");
            if (YaEnEstePeldanoOMasArriba > 0)
            {
                fuera.Add($"{YaEnEstePeldanoOMasArriba} de la categoría {Categoria} o más arriba");
            }

            return fuera.Count == 0 ? cabeza : $"{cabeza} · fuera: {string.Join(", ", fuera)}";
        }
    }

    /// <summary>La palabra que le toca a la cifra; solo la palabra, sin el número delante.</summary>
    /// <param name="cuantos">La cifra.</param>
    /// <param name="uno">La palabra en singular.</param>
    /// <param name="varios">La palabra en plural.</param>
    private static string Plural(int cuantos, string uno, string varios) => cuantos == 1 ? uno : varios;
}

/// <summary>
/// La escalera del dueno: que documento sube al peldano siguiente cuando el de abajo no pudo.
/// </summary>
/// <remarks>
/// <para><b>Sus palabras, del 2026-09-05</b> (<c>DECISIONES.md</c>): <i>«los agentes categoría 1
/// no pudieron comunicarse con los líderes, debo pasarlo a los agentes de categoría 2; si los
/// de categoría 2 no pudieron, a los de categoría 3. Así puedes agregarle a los gerentes
/// categoría y a los agentes categorías»</i>. Por eso aqui no hay ninguna nocion de «gerente»:
/// un gerente es un companero con la categoria mas alta, y el numero de peldanos lo pone el
/// dueno dando de alta gente, no este codigo.</para>
///
/// <para><b>La regla de entrada, y es la del criterio C15-2:</b> entra el documento que
/// <b>(a)</b> volvio con <c>no_completa</c> <b>y (b)</b> tiene <c>motivo_del_companero</c>
/// igual a «no se pudo comunicar» o «el líder no lo hizo». Uno <c>completa</c> no entra nunca,
/// tenga el comentario que tenga. Y <b>(c)</b>, que es lo que hace que la escalera sea una
/// escalera: quien lo intento tiene que estar en un peldano MAS BAJO que quien va a recibirlo.
/// Sin (c), el paquete de la categoria 2 le devolveria al gerente el trabajo que el mismo
/// acaba de no conseguir.</para>
///
/// <para>⚠️ <b>«Otra razón» se queda fuera del paquete y se devuelve aparte.</b> El ADR-0005
/// §5.4 recomienda al dueno que SI entre, pero listada aparte; el criterio C15-2 solo nombra
/// los otros dos motivos. <b>Es una decision suya que sigue abierta</b>, asi que aqui se hace
/// lo que dice el criterio y se devuelven en <see cref="LoQueSube.ConOtraRazon"/>: no entran, y
/// no se pierden de vista. El dia que el conteste, cambia UNA linea de este archivo.</para>
///
/// <para><b>Aqui no se escribe nada en la base.</b> Esto decide QUE va dentro del paquete; el
/// paquete lo genera <see cref="OperacionDelPaquete"/> por el mismo puerto que la primera
/// vuelta.</para>
/// </remarks>
public static class SegundaVuelta
{
    /// <summary>Los dos motivos que suben, tal como los nombro el dueno.</summary>
    /// <remarks>
    /// Se escriben aqui como lista y no como un <c>or</c> suelto dentro de la condicion: es la
    /// regla de negocio del criterio C15-2 y tiene que poder leerse sin seguir un condicional.
    /// </remarks>
    private static readonly MotivoDeNoCompletar[] LosQueSuben =
    [
        MotivoDeNoCompletar.NoSePudoComunicar,
        MotivoDeNoCompletar.ElLiderNoLoHizo,
    ];

    /// <summary>Lo que sube al peldano de ese companero, con sus cuentas.</summary>
    /// <remarks>
    /// Los archivados no entran, y no hace falta filtrarlos aqui: <see cref="FiltroDeCasos"/>
    /// los deja fuera por defecto. Es la regla del dueno del 2026-09-05, <i>«cuando yo archive,
    /// debe salir del sistema visible»</i>, y un paquete es sistema visible.
    /// </remarks>
    /// <param name="casos">Por donde se listan los documentos no completos.</param>
    /// <param name="asignaciones">Por donde se sabe quién llevó cada uno.</param>
    /// <param name="companeros">Por donde se lee la categoría de quien lo llevó.</param>
    /// <param name="quienLoRecibe">El compañero al que iría el paquete; su categoría es el peldaño.</param>
    /// <returns>Nunca nulo; con cero que entran cuando nadie de más abajo se trabó en un documento.</returns>
    public static LoQueSube Para(
        ICasos casos, IAsignaciones asignaciones, ICompaneros companeros, Companero quienLoRecibe)
    {
        ArgumentNullException.ThrowIfNull(casos);
        ArgumentNullException.ThrowIfNull(asignaciones);
        ArgumentNullException.ThrowIfNull(companeros);
        ArgumentNullException.ThrowIfNull(quienLoRecibe);

        var noCompletos = casos
            .Listar(new FiltroDeCasos(Estado: EstadoDeRecomendacion.NoCompleta), Pagina.Primera(int.MaxValue))
            .Elementos;

        var entran = new List<CasoQueSube>();
        var conOtraRazon = new List<CasoQueSube>();
        var sinMotivo = 0;
        var yaEnEstePeldano = 0;

        foreach (var caso in noCompletos)
        {
            var motivo = caso.MotivoQueDijoElCompanero;
            if (motivo == MotivoDeNoCompletar.SinMotivo)
            {
                sinMotivo++;
                continue;
            }

            var intento = QuienLoIntentoMasAbajo(asignaciones, companeros, caso.Id, quienLoRecibe.Categoria);
            if (intento is null)
            {
                yaEnEstePeldano++;
                continue;
            }

            var renglon = new CasoQueSube(
                caso.Id, caso.NumeroCaso, intento.Id, intento.Nombre, intento.Categoria, motivo);

            if (LosQueSuben.Contains(motivo)) entran.Add(renglon);
            else conOtraRazon.Add(renglon);
        }

        return new LoQueSube(
            quienLoRecibe.Categoria,
            entran,
            conOtraRazon,
            noCompletos.Count,
            sinMotivo,
            yaEnEstePeldano);
    }

    /// <summary>
    /// Quien llevo ese caso desde el peldano mas alto que este por DEBAJO del que va a recibirlo.
    /// </summary>
    /// <remarks>
    /// <para>El mas alto de los de abajo, y no el primero que aparezca: si un documento ya paso
    /// por la categoria 1 y por la 2, quien tiene algo que contarle a la categoria 3 es el de la
    /// 2, que es el ultimo que lo intento.</para>
    ///
    /// <para>Se miran TODAS las asignaciones, vivas y retiradas, porque el que lo intento
    /// cuenta aunque ya no lo lleve.</para>
    ///
    /// <para>⚠️ 2026-09-07. Aqui decia «un caso que ya volvio tiene su asignacion retirada», y
    /// es FALSO: devolver el Excel escribe el estado del caso y NO retira nada. Comprobado con
    /// <c>grep -rn "Retirar"</c> sobre este proyecto y sobre <c>Fichas.Paquetes</c> —cero
    /// resultados fuera de los binarios— y midiendolo en la base. Pedirlas todas sigue siendo
    /// lo correcto; lo falso era el porque. Una asignacion se retira a mano, y desde el
    /// 2026-09-07 hay un boton que retira las de un companero de golpe.</para>
    ///
    /// <para>⚠️ Y más tarde ese mismo 2026-09-07 cambió otra vez: la vuelta SÍ retira las
    /// asignaciones de lo que el compañero devolvió completo (<see cref="LimpiezaAlVolver"/>).
    /// Lo no completo —que es lo único que sube— sigue vivo, así que para esta regla da igual;
    /// pedirlas todas, vivas y retiradas, sigue siendo lo correcto.</para>
    ///
    /// <para>Devuelve nulo cuando nadie de un peldano mas bajo lo intento. Eso incluye el caso
    /// que nunca se asigno a nadie —no se puede escalar lo que no se ha intentado— y el que se
    /// trabo en este mismo peldano o mas arriba.</para>
    /// </remarks>
    /// <param name="asignaciones">Por donde se listan todas las asignaciones del caso.</param>
    /// <param name="companeros">Por donde se lee la categoría de cada uno.</param>
    /// <param name="casoId">El documento.</param>
    /// <param name="categoriaDeQuienLoRecibe">El peldaño al que se sube; solo cuentan los de más abajo.</param>
    private static Companero? QuienLoIntentoMasAbajo(
        IAsignaciones asignaciones, ICompaneros companeros, long casoId, int categoriaDeQuienLoRecibe)
    {
        var todas = asignaciones
            .Listar(new FiltroDeAsignaciones(CasoId: casoId, SoloActivas: false), Pagina.Primera(int.MaxValue))
            .Elementos;

        Companero? masAlto = null;
        foreach (var asignacion in todas)
        {
            var quien = companeros.Obtener(asignacion.CompaneroId);
            if (quien is null || quien.Categoria >= categoriaDeQuienLoRecibe) continue;
            if (masAlto is null || quien.Categoria > masAlto.Categoria) masAlto = quien;
        }

        return masAlto;
    }
}
