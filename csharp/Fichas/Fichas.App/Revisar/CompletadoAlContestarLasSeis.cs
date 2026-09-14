using System.Globalization;
using Fichas.App.Correccion;
using Fichas.App.Grupo;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Revisar;

/// <summary>
/// El completado que se deriva de las seis en «sí»: cuando todas las personas de un
/// documento tienen las seis y no le falta ningún campo, el documento pasa a completado
/// en el mismo gesto, firmado por el administrador.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué existe.</b> Del dueño, 2026-09-14: <i>«cuando se marcan las 6 preguntas que
/// sí, de manera automática debe marcarse como completado»</i>. Hasta hoy eran dos clics a
/// propósito —guardar las seis y, aparte, «Sí, completa» (decisión del 2026-09-07)—; desde
/// hoy el segundo se deriva del primero.
/// </para>
/// <para>
/// ⛔ <b>Qué es automático y qué no, para que la regla permanente 5 siga entera.</b> Las
/// seis las marca el dueño, una a una o de un tirón, y quedan con su firma
/// (<see cref="AccionesDeLasPreguntas"/>). Lo que se deriva es el ESTADO del documento
/// —<c>casos.estado_recomendacion</c>—, y solo cuando sus marcas ya lo dicen entero: todas
/// las personas con las seis en «sí» y ningún campo que falte según <see cref="LoQueLeFalta"/>,
/// que es la misma lectura que usan Corrección y el grupo. No hay «completo por defecto»: lo
/// que falte se dice y el documento se queda como estaba.
/// </para>
/// <para>
/// ⛔ <b>La firma es la del administrador y por la regla de <see cref="ElAdministrador"/>:</b>
/// un administrador activo → él; ninguno o dos → no se marca y se dice por qué. Nunca «el
/// primero activo», que en la base viva del dueño firmaría como Sandy.
/// </para>
/// <para>
/// ⚠️ <b>Lo que NO hace, a propósito.</b> No desmarca: un «sí» que vuelve a «no» después
/// deja el completado como estaba, y quitarlo lo decide él a mano. Y no pisa una marca que
/// ya estuviera: un documento que el Excel de un compañero ya dio por completo sigue siendo
/// de ese compañero.
/// </para>
/// <para>
/// Va sin ventana a propósito: lo que se escribe y lo que se dice se leen desde una prueba.
/// </para>
/// </remarks>
public sealed class CompletadoAlContestarLasSeis
{
    /// <summary>Lo que se guarda en <c>casos.estado_marcado_origen</c> cuando la marca se derivó de las seis.</summary>
    /// <remarks>
    /// Es el CUARTO valor de una columna que ya guardaba tres —«a mano en la pantalla
    /// Revisar», la ruta de un Excel y «el administrador lo hizo»—, sin columna nueva
    /// (ADR-0005 §6.1). Es distinto de <see cref="ElAdministrador.Origen"/> a propósito: aquel
    /// dice que se completó SIN verificar campo por campo, y este dice lo contrario, que
    /// todos los campos y las seis de cada persona estaban. El texto acaba en la base y lo
    /// leen los reportes: cambiarlo deja sin reconocer todo lo ya marcado por esta vía.
    /// </remarks>
    public const string Origen = "las seis en «sí» desde la pantalla";

    /// <summary>El puerto de documentos: de aquí se lee el documento y aquí se marca.</summary>
    private readonly ICasos _casos;
    /// <summary>El puerto de personas: de aquí se leen las seis de cada una.</summary>
    private readonly IPersonas _personas;
    /// <summary>La procedencia de campos: sin ella no se sabe si un campo falta.</summary>
    private readonly IProcedencia _procedencia;
    /// <summary>El equipo, para saber quién es el único administrador activo que firma.</summary>
    private readonly ICompaneros _companeros;

    /// <summary>Ata la derivación a los cuatro puertos que necesita.</summary>
    /// <param name="casos">El puerto de documentos.</param>
    /// <param name="personas">El puerto de personas.</param>
    /// <param name="procedencia">El puerto de procedencia de campos.</param>
    /// <param name="companeros">El puerto del equipo.</param>
    public CompletadoAlContestarLasSeis(
        ICasos casos, IPersonas personas, IProcedencia procedencia, ICompaneros companeros)
    {
        _casos = casos;
        _personas = personas;
        _procedencia = procedencia;
        _companeros = companeros;
    }

    /// <summary>Si, tras guardar las seis de esa persona, toca intentar el completado.</summary>
    /// <remarks>
    /// Solo cuando las seis de ESA persona quedaron en «sí»: guardar un «no» o un «sin
    /// mirar» no puede completar nada, y hablar del completado ahí sería ruido.
    /// </remarks>
    /// <param name="persona">La persona cuyas seis se acaban de guardar, releída de la base.</param>
    public static bool TocaIntentarlo(Persona persona)
    {
        ArgumentNullException.ThrowIfNull(persona);
        return Pasos.Estado(persona) == true;
    }

    /// <summary>
    /// Marca el documento como completado si le corresponde; si no, dice exactamente qué
    /// lo impide y no escribe nada.
    /// </summary>
    /// <remarks>
    /// El orden es: releer el documento, ver qué lo impide, y solo si nada lo impide
    /// escribir. Un documento que ya estaba completado no se vuelve a marcar: su firma es de
    /// quien la puso.
    /// </remarks>
    /// <param name="casoId">El documento cuyas seis se acaban de guardar.</param>
    /// <returns>«Se escribió» con el acuse si se marcó; si no, «no se escribió» con el porqué.</returns>
    public ResultadoDeEscritura SiCorresponde(long casoId)
    {
        if (_casos.Obtener(casoId) is not Caso caso)
        {
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                "No se pudo derivar el completado: ese documento ya no está en la base.",
                string.Empty,
                $"Ningún documento con el número interno {casoId.ToString(CultureInfo.InvariantCulture)}."));
        }

        if (caso.Estado == EstadoDeRecomendacion.Completa) return YaEstaba(caso);

        var personas = _personas.DeCaso(casoId);
        var loQueFalta = LoQueLeFalta.DeUnDocumento(
            caso, personas, ProcedenciasDeUnaPasada.DeUnDocumento(_procedencia, caso, personas));
        var activos = _companeros.Activos();

        var impedimentos = LoQueLoImpide(personas, loQueFalta, activos);
        if (impedimentos.Count > 0) return ResultadoDeEscritura.NoSeEscribio(PorQueNoSeMarco(Numero(caso), impedimentos));

        // Si LoQueLoImpide no dijo nada del administrador, hay exactamente uno.
        var administrador = ElAdministrador.De(activos)!;
        return Marcar(caso, administrador);
    }

    /// <summary>
    /// Qué impide completar el documento: los campos que faltan, las personas sin las seis
    /// en «sí» y el administrador que no hay; vacío significa que se puede.
    /// </summary>
    /// <remarks>
    /// Se enumeran los TRES a la vez y no se para en el primero: si él arregla un campo y
    /// vuelve, tiene que encontrarse con la lista corta, no con otro impedimento nuevo.
    /// </remarks>
    /// <param name="personas">Las personas del documento, con sus seis.</param>
    /// <param name="loQueFalta">Los rótulos de los campos que faltan, según <see cref="LoQueLeFalta"/>.</param>
    /// <param name="activos">Los compañeros activos, entre los que tiene que haber un administrador y solo uno.</param>
    public static IReadOnlyList<string> LoQueLoImpide(
        IReadOnlyList<Persona> personas, IReadOnlyList<string> loQueFalta, IReadOnlyList<Companero> activos)
    {
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(loQueFalta);
        ArgumentNullException.ThrowIfNull(activos);

        var impedimentos = new List<string>();
        if (loQueFalta.Count > 0) impedimentos.Add($"le falta: {string.Join(", ", loQueFalta)}");

        var sinLasSeis = personas
            .Where(persona => Pasos.Estado(persona) != true)
            .Select(PersonaQueFalta)
            .ToList();
        if (sinLasSeis.Count > 0) impedimentos.Add($"sin las seis en «sí»: {string.Join("; ", sinLasSeis)}");

        if (ElAdministrador.De(activos) is null) impedimentos.Add(QueAdministradorFalta(activos));

        return impedimentos;
    }

    /// <summary>El acuse de que el documento quedó completado, con quién firmó.</summary>
    /// <param name="numero">El número del documento, tal como él lo lee en la tarjeta.</param>
    /// <param name="quienFirmo">El nombre del administrador.</param>
    public static Aviso LoQueSeMarco(string numero, string quienFirmo)
        => Aviso.Informa(
            $"Las seis en «sí»: {numero} queda completado (firma: {quienFirmo}).",
            string.Empty,
            "Todas sus personas tienen las seis en «sí» y no le falta ningún campo, así que el "
            + "documento pasa a completado sin un segundo clic. Esto NO firma ningún campo «Todo correcto».");

    /// <summary>El acuse de que NO se marcó, con la lista exacta de lo que lo impide.</summary>
    /// <param name="numero">El número del documento.</param>
    /// <param name="impedimentos">Lo que lo impide, tal como lo enumera <see cref="LoQueLoImpide"/>.</param>
    public static Aviso PorQueNoSeMarco(string numero, IReadOnlyList<string> impedimentos)
    {
        ArgumentNullException.ThrowIfNull(impedimentos);
        return Aviso.Advierte(
            $"Las seis quedan guardadas, pero {numero} no se marca completado todavía.",
            string.Empty,
            string.Join(" · ", impedimentos) + ".");
    }

    /// <summary>Lo que se dice cuando el documento ya estaba completado: nada se toca.</summary>
    /// <param name="caso">El documento, con la marca que ya tenía.</param>
    private static ResultadoDeEscritura YaEstaba(Caso caso)
        => ResultadoDeEscritura.NoSeEscribio(Aviso.Informa(
            $"{Numero(caso)} ya estaba completado; la marca que tenía no se toca.",
            string.Empty,
            "La firma del estado es de quien la puso, y las seis en «sí» no la pisan."));

    /// <summary>Escribe la marca con la firma del administrador y vacía el motivo que hubiera.</summary>
    /// <remarks>
    /// El motivo se vacía por lo mismo que en <see cref="AccionesDeRevisar.MarcarAMano(long, EstadoDeRecomendacion, MotivoDeNoCompletar, long)"/>:
    /// «completa porque el líder no lo hizo» es una contradicción y no puede quedar escrita.
    /// Y por el mismo hueco declarado allí: <see cref="ICasos"/> no tiene por dónde escribir
    /// solo el motivo, así que se pasa por <c>Guardar</c> con lo recién leído.
    /// </remarks>
    /// <param name="caso">El documento, tal como se acaba de leer.</param>
    /// <param name="administrador">Quien firma.</param>
    private ResultadoDeEscritura Marcar(Caso caso, Companero administrador)
    {
        var marca = _casos.MarcarEstado(caso.Id, EstadoDeRecomendacion.Completa, administrador.Id, Origen);
        if (!marca.SeEscribio) return marca;

        if (caso.Motivo != MotivoDeNoCompletar.SinMotivo && _casos.Obtener(caso.Id) is Caso marcado)
        {
            var sinMotivo = _casos.Guardar(marcado with { MotivoNoCompleta = null });
            if (!sinMotivo.SeEscribio) return sinMotivo;
        }

        return ResultadoDeEscritura.BienCon(caso.Id, LoQueSeMarco(Numero(caso), administrador.Nombre));
    }

    /// <summary>Cómo se nombra a una persona que no tiene las seis, y en qué se quedó.</summary>
    /// <param name="persona">La persona sin las seis en «sí».</param>
    private static string PersonaQueFalta(Persona persona)
    {
        var quien = string.IsNullOrWhiteSpace(persona.Nombre)
            ? $"la fila {(persona.FilaFormulario ?? 0).ToString(CultureInfo.InvariantCulture)}"
            : persona.Nombre.Trim();
        var enNo = Pasos.SinCompletar(persona);

        return enNo.Count == 0
            ? $"{quien} ({LasDosPreguntas.SinMirar})"
            : $"{quien} (en «no» en {string.Join(", ", enNo)})";
    }

    /// <summary>Qué falta del administrador: que haya uno, o que haya uno solo.</summary>
    /// <remarks>
    /// ⛔ La frase NO nombra a ningún compañero. Ofrecer un nombre aquí es justo el paso
    /// previo a firmar con él.
    /// </remarks>
    /// <param name="activos">Los compañeros activos.</param>
    private static string QueAdministradorFalta(IReadOnlyList<Companero> activos)
    {
        var cuantos = activos.Count(companero => companero.Activo && companero.Rol == RolDeCompanero.Administrador);
        return cuantos == 0
            ? "no hay ningún administrador activo que firme la marca: dese de alta usted como administrador"
            : $"hay {cuantos.ToString(CultureInfo.InvariantCulture)} administradores activos y el programa no adivina cuál es usted";
    }

    /// <summary>El número del documento tal como él lo lee, o su número interno si no lo tiene.</summary>
    /// <param name="caso">El documento.</param>
    private static string Numero(Caso caso)
        => string.IsNullOrWhiteSpace(caso.NumeroCaso)
            ? $"el documento n.º {caso.Id.ToString(CultureInfo.InvariantCulture)}"
            : caso.NumeroCaso.Trim();
}
