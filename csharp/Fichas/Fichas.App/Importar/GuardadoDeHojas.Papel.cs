using Fichas.Contratos.Modelos;
using Fichas.Lectura;

namespace Fichas.App.Importar;

/// <summary>
/// La mitad del guardado que decide de qué PAPEL es cada hoja: la copia que guarda el caso,
/// y la hoja que no es un formulario de recomendación.
/// </summary>
/// <remarks>
/// Las dos cosas entraron el 2026-09-15 con el defecto del papel pegado y van en su propio
/// archivo por el mismo reparto que <c>GuardadoDeHojas.Filas.cs</c>: la clase ya pasaba de
/// las 300 líneas y esto es una responsabilidad aparte de unir hojas y escribir filas.
/// </remarks>
public sealed partial class GuardadoDeHojas
{
    /// <summary>
    /// La ruta que van a guardar los casos de este documento: su copia, hecha UNA vez y solo
    /// si alguna hoja abre caso.
    /// </summary>
    /// <remarks>
    /// Todas las hojas de un documento vienen del mismo archivo, así que la copia se hace
    /// una vez por documento y no una por hoja: con un formulario de grupo de seis hojas
    /// serían seis lecturas del mismo archivo para la misma huella. Y es perezosa: un PDF que
    /// no abre ningún caso —un impreso ajeno, uno que no se pudo leer— no deja copia, porque
    /// no habría documento que la usara.
    /// </remarks>
    /// <param name="hojas">Las hojas del documento; vacía da una ruta vacía sin avisos.</param>
    private Lazy<CopiaGuardada> PapelDelDocumento(IReadOnlyList<HojaLeida> hojas)
        => new(() => hojas.Count == 0 ? new CopiaGuardada(string.Empty, []) : _copias.Guardar(hojas[0].RutaPdf));

    /// <summary>Los mismos resultados con los avisos de la copia delante del primero, si la copia se hizo y avisó.</summary>
    /// <param name="resultados">Un resultado por hoja.</param>
    /// <param name="papel">La copia perezosa; si nadie la pidió, no hay nada que añadir.</param>
    private static IReadOnlyList<ResultadoDeLaHoja> ConLosAvisosDeLaCopia(
        List<ResultadoDeLaHoja> resultados, Lazy<CopiaGuardada> papel)
    {
        if (!papel.IsValueCreated || papel.Value.Avisos.Count == 0 || resultados.Count == 0) return resultados;

        resultados[0] = resultados[0] with { Avisos = [.. papel.Value.Avisos, .. resultados[0].Avisos] };
        return resultados;
    }

    /// <summary>
    /// Si esta hoja NO es un formulario de recomendación: no se encontró ninguna de sus
    /// nueve etiquetas impresas.
    /// </summary>
    /// <remarks>
    /// <para>⚠️ Medido el 2026-09-15 con el lector real sobre un impreso sintético de otra
    /// clase (fondos de ayuda, en francés, con un código «FORD2610» impreso): <b>9 de 9
    /// etiquetas ausentes</b> y un solo campo, <c>numero_caso = FORD2610</c>, leído del texto
    /// porque cuatro letras y cuatro cifras tienen la forma de un número de caso. Con eso
    /// nacía un caso «FORD2610 · sin unidad leída · 0 personas», que es lo que el dueño vio
    /// en su lista y en su base.</para>
    ///
    /// <para>La señal es la que deja la extracción: por cada etiqueta que no encuentra,
    /// <c>Fichas.Lectura.Extraccion.ProponerCamposDelCaso</c> deja un aviso con la clave de la
    /// etiqueta en <c>Campo</c>. Ninguna encontrada es «esto no es el formulario». Un
    /// formulario de verdad mal escaneado encuentra alguna: las páginas del revés del dueño
    /// encontraron la unidad y la fecha aunque leyeran basura debajo. Y sin ninguna etiqueta no
    /// se puede haber leído ningún otro campo, porque todos cuelgan de una.</para>
    ///
    /// <para>⛔ Lo limpio sería que la hoja leída dijera cuántas etiquetas encontró, en vez de
    /// contarlo por sus avisos. Eso es <c>Fichas.Lectura</c>, que no es terreno de este pase:
    /// queda dicho en la entrega.</para>
    /// </remarks>
    /// <param name="hoja">La hoja leída, con los avisos de la extracción.</param>
    private static bool EsUnFormularioDesconocido(HojaLeida hoja)
    {
        var etiquetasQueFaltan = hoja.Avisos
            .Select(aviso => aviso.Campo)
            .Where(Etiquetas.CamposDelFormulario.Contains)
            .Distinct(StringComparer.Ordinal)
            .Count();

        return etiquetasQueFaltan == Etiquetas.CamposDelFormulario.Count;
    }

    /// <summary>Lo que se devuelve de una hoja que no es un formulario de recomendación: nada entra, queda su renglón.</summary>
    /// <param name="hoja">La hoja leída.</param>
    /// <param name="campos">Sus campos, para decir en el aviso qué se leyó con forma de número.</param>
    private static ResultadoDeLaHoja HojaDeOtraClase(HojaLeida hoja, CamposDeLaHoja campos)
        => new(
            Entro: false,
            CasoNuevo: false,
            CasoId: null,
            NumeroCaso: null,
            PaginaPdf: hoja.Pagina,
            Personas: 0,
            Avisos: [AvisoDeLaHojaDeOtraClase(hoja, campos)],
            PendienteDeIdentificar: false,
            DuplicadoDe: null,
            Renglones: [MotivosDeIlegible.FormularioDesconocido]);

    /// <summary>El aviso de UNA línea de la hoja que no es del formulario, con lo leído en el detalle.</summary>
    /// <param name="hoja">La hoja leída.</param>
    /// <param name="campos">Sus campos; de aquí sale el token con forma de número, si lo hubo.</param>
    private static Aviso AvisoDeLaHojaDeOtraClase(HojaLeida hoja, CamposDeLaHoja campos)
        => Aviso.Advierte(
            $"La hoja {hoja.Pagina} de «{Path.GetFileName(hoja.RutaPdf)}» no es un formulario de recomendación: entró en la lista de lo que no se leyó, sin documento.",
            CamposDeLaHoja.CampoNumeroCaso,
            DetalleDeLaHojaDeOtraClase(campos));

    /// <summary>Por qué no es el formulario y qué se leyó, en una frase.</summary>
    /// <param name="campos">Los campos de la hoja.</param>
    private static string DetalleDeLaHojaDeOtraClase(CamposDeLaHoja campos)
    {
        var numero = campos.ValorDe(CamposDeLaHoja.CampoNumeroCaso);
        var loLeido = numero is null
            ? "No se leyó nada con forma de número de caso."
            : $"Lo único con forma de número de caso que se leyó fue «{numero}», que no abre documento porque no hay formulario detrás.";
        return "No se encontró en la hoja ninguna de las etiquetas impresas del formulario de recomendación "
            + "(fecha de viaje, unidad, nombres, cédulas, templo…), así que es un papel de otra clase. "
            + loLeido + " Si SÍ es un formulario, el escaneo salió tan mal que no se lee ni una etiqueta: "
            + "vuelva a escanearlo.";
    }
}
