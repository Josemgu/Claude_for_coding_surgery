using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Paquetes;

/// <summary>Lo que lleva encima un companero ahora mismo: lo que le toca hacer y lo que ya hizo.</summary>
/// <param name="CompaneroId">De quien es la carga.</param>
/// <param name="CasoIds">Los casos que VAN en su paquete siguiente, sin repetir.</param>
/// <param name="Personas">Cuantas personas hay dentro de esos casos.</param>
/// <param name="YaLosDevolvioCompletos">Los que el ya devolvio COMPLETOS; se quedan fuera del paquete.</param>
/// <param name="VuelvenSinCompletar">De los que van, cuales ya devolvio SIN completar.</param>
public sealed record CargaDelCompanero(
    long CompaneroId,
    IReadOnlyList<long> CasoIds,
    int Personas,
    IReadOnlyList<long> YaLosDevolvioCompletos,
    IReadOnlyList<long> VuelvenSinCompletar)
{
    /// <summary>Cuantas asignaciones vivas lleva en total, vayan o no vayan al paquete.</summary>
    public int CasosQueLlevaEnTotal => CasoIds.Count + YaLosDevolvioCompletos.Count;

    /// <summary>
    /// La linea CORTA: los casos que van y su gente. Es la que viaja en el acuse del pie.
    /// </summary>
    /// <remarks>
    /// <para>Los casos y las personas, y no solo los casos: un paquete de 12 casos puede llevar
    /// 12 personas o 38, y el companero mide su trabajo en personas. Sin la segunda cifra no
    /// sabe a que se compromete.</para>
    ///
    /// <para>⚠️ <b>Corta a proposito y medida:</b> se mete dentro de la linea del resumen de la
    /// ida, que tiene un tope comprobado de 160 caracteres. Lo que se queda fuera del paquete
    /// se dice en <see cref="LineaConLoQueNoVa"/> y en el detalle, que son los dos sitios que
    /// si tienen sitio para explicarlo.</para>
    /// </remarks>
    public string Linea => CasoIds.Count == 0
        ? SinNadaQueMandar
        : $"{Plural(CasoIds.Count, "caso", "casos")} · {Plural(Personas, "persona", "personas")}";

    /// <summary>
    /// La linea LARGA, con lo que NO va y por que: la que se pinta al lado del desplegable.
    /// </summary>
    /// <remarks>
    /// Un numero que baja sin explicacion se lee como que algo se perdio. Con la cola puesta se
    /// lee como lo que es: ese trabajo ya esta hecho y no se le manda dos veces, y lo que volvio
    /// sin completar sigue dentro porque no esta hecho.
    /// </remarks>
    public string LineaConLoQueNoVa
    {
        get
        {
            var cola = new List<string>();
            if (YaLosDevolvioCompletos.Count > 0)
            {
                cola.Add(Plural(YaLosDevolvioCompletos.Count, "ya devuelto completo", "ya devueltos completos")
                    + " que no vuelve" + (YaLosDevolvioCompletos.Count == 1 ? string.Empty : "n") + " a ir");
            }

            if (VuelvenSinCompletar.Count > 0)
            {
                cola.Add($"{VuelvenSinCompletar.Count} "
                    + (VuelvenSinCompletar.Count == 1 ? "volvió" : "volvieron")
                    + " sin completar y sigue" + (VuelvenSinCompletar.Count == 1 ? string.Empty : "n") + " dentro");
            }

            return cola.Count == 0 ? Linea : $"{Linea} · {string.Join(" · ", cola)}";
        }
    }

    /// <summary>
    /// Que se dice cuando no va ningun caso; no es lo mismo no llevar nada que haberlo hecho ya.
    /// </summary>
    private string SinNadaQueMandar => YaLosDevolvioCompletos.Count == 0
        ? "ningún caso asignado"
        : $"nada pendiente · {Plural(YaLosDevolvioCompletos.Count, "caso ya devuelto completo", "casos ya devueltos completos")}";

    private static string Plural(int cuantos, string una, string varias)
        => $"{cuantos} {(cuantos == 1 ? una : varias)}";
}

/// <summary>
/// Que casos lleva un companero, leidos por la unica puerta que hay: las asignaciones vivas.
/// </summary>
/// <remarks>
/// <para>Se lee una sola vez y de aqui salen las DOS cosas: la cifra que ve el dueno en la
/// pantalla y la lista que se manda al Excel. Si fueran dos lecturas podrian no coincidir, y
/// entonces la pantalla diria «12 casos» y el companero recibiria otros.</para>
///
/// <para>⚠️ <b>Lo que el ya devolvio COMPLETO no vuelve a entrar.</b> Lo pidio el dueno el
/// 2026-09-07 con estas palabras: <i>«si eliges otra fecha para el mismo agente, te carga todas
/// las fechas pasadas que ya completó y las fechas nuevas en un solo paquete. Es trabajar dos
/// veces»</i>.</para>
///
/// <para>⛔ <b>Y lo devuelto SIN completar si vuelve a entrar, que es lo contrario y es lo que
/// no puede fallar.</b> «Devolver» no es «completar»: un documento que volvio con «no se pudo
/// comunicar con el líder» <b>no esta hecho</b>, y sacarlo del paquete lo haria desaparecer del
/// trabajo de alguien sin que nadie lo decida. Se queda dentro y ademas se cuenta aparte en la
/// linea, para que se vea que sigue ahi.</para>
///
/// <para>⛔ <b>Aqui NO se retira ninguna asignacion, y eso no ha cambiado</b>: esta clase LEE.
/// Quien retira es <see cref="LimpiezaAlVolver"/>, desde la vuelta.</para>
///
/// <para>⚠️ <b>Lo que si cambio el 2026-09-07.</b> Este parrafo decia que retirar al devolver
/// NO era la forma elegida, «porque el informe de cada agente se recorta a sus asignaciones
/// vivas». <b>Esa razon era falsa</b>: <c>ReportesEnPdf.DocumentoDeCompanero</c> pide las
/// asignaciones con <c>SoloActivas: false</c> y arma sus casos con TODAS, vivas y retiradas. El
/// dueno pidio lo contrario de lo que se hizo —<i>«debe quitarle que ese caso esta asignado a
/// el; debe quedar limpio»</i>— y manda el. Este filtro se queda igual y es cinturon y
/// tirantes: alcanza lo que un dia se devolvio completo y por lo que fuera sigue asignado.</para>
/// </remarks>
public static class CargaDeUnCompanero
{
    /// <summary>Los casos que le tocan a ese companero y cuanta gente hay dentro.</summary>
    public static CargaDelCompanero Leer(IAsignaciones asignaciones, ICasos casos, long companeroId)
    {
        ArgumentNullException.ThrowIfNull(asignaciones);
        ArgumentNullException.ThrowIfNull(casos);

        var suyos = LosQueLleva(asignaciones, companeroId);
        var loQueYaDijo = LoQueEsteCompaneroYaDijoDeCadaUno(casos, companeroId);

        var van = new List<long>(suyos.Count);
        var yaCompletos = new List<long>();
        var sinCompletar = new List<long>();

        foreach (var casoId in suyos)
        {
            // El que NO esta en el diccionario entra: es un caso del que este companero no ha
            // dicho nada todavia. Ante la duda se manda, nunca se quita; la falta que hace
            // dano es la contraria.
            var loQueDijo = loQueYaDijo.GetValueOrDefault(casoId, EstadoDeRecomendacion.SinMarcar);
            if (loQueDijo == EstadoDeRecomendacion.Completa)
            {
                yaCompletos.Add(casoId);
                continue;
            }

            if (loQueDijo == EstadoDeRecomendacion.NoCompleta) sinCompletar.Add(casoId);
            van.Add(casoId);
        }

        var personas = van.Count == 0 ? 0 : casos.ContarPersonasDe(van).Values.Sum();
        return new CargaDelCompanero(companeroId, van, personas, yaCompletos, sinCompletar);
    }

    /// <summary>Los casos de sus asignaciones vivas, sin repetir.</summary>
    /// <remarks>
    /// Se piden todas de golpe: un companero lleva decenas de casos, no miles, y partirlo en
    /// trozos aqui obligaria a juntar los distintos despues de todos modos. Sin repetir porque
    /// un caso puede tener mas de una asignacion viva (la P-11 sigue abierta) y mandarlo dos
    /// veces al Excel duplicaria a toda su familia dentro de la hoja.
    /// </remarks>
    private static List<long> LosQueLleva(IAsignaciones asignaciones, long companeroId)
        => [.. asignaciones
            .Listar(
                new FiltroDeAsignaciones(CompaneroId: companeroId, SoloActivas: true),
                Pagina.Primera(int.MaxValue))
            .Elementos
            .Select(asignacion => asignacion.CasoId)
            .Distinct()];

    /// <summary>Que dijo ESTE companero de cada uno de los casos que lleva vivos.</summary>
    /// <remarks>
    /// <para>Una sola consulta y no una por caso: <c>FiltroDeCasos.CompaneroId</c> pide justo
    /// los casos con una asignacion VIVA a ese companero, que es el mismo conjunto que se acaba
    /// de leer de las asignaciones. Con cien casos, cien <c>Obtener</c> serian cien consultas
    /// cada vez que se cambia el desplegable.</para>
    ///
    /// <para><b>Se mira <c>estado_del_companero_por</c> y no solo el estado.</b> Un documento que
    /// completo OTRO agente no es trabajo que este haya hecho: si se mirara solo el estado, a
    /// este se le quitaria del paquete un caso que nunca ha visto.</para>
    ///
    /// <para>Los archivados entran en la consulta a proposito. Que un caso archivado deba o no
    /// ir en un paquete es otra pregunta y tiene su propia regla; dejarlo fuera aqui la
    /// cambiaria de tapadillo desde el sitio equivocado.</para>
    /// </remarks>
    private static Dictionary<long, EstadoDeRecomendacion> LoQueEsteCompaneroYaDijoDeCadaUno(
        ICasos casos, long companeroId)
        => casos
            .Listar(
                new FiltroDeCasos(CompaneroId: companeroId, IncluirArchivados: true),
                Pagina.Primera(int.MaxValue))
            .Elementos
            .Where(caso => caso.EstadoDelCompaneroPor == companeroId)
            .ToDictionary(caso => caso.Id, caso => Caso.LeerEstado(caso.EstadoDelCompanero));
}
