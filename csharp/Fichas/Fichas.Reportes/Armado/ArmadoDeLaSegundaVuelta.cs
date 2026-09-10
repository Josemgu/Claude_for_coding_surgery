using Fichas.Contratos.Modelos;
using Fichas.Reportes.Modelo;
using Fichas.Reportes.Reglas;

namespace Fichas.Reportes.Armado;

/// <summary>Un documento que sube de peldano, con quien lo intento antes y que motivo dio.</summary>
/// <remarks>
/// Vive aqui, y no se importa de <c>Fichas.App</c>, porque las dependencias van en un solo
/// sentido: la aplicacion conoce a los reportes y los reportes no conocen a la aplicacion.
/// Quien arma la escalera —<c>Fichas.App.Paquetes.SegundaVuelta</c>— traduce su resultado a
/// esto en tres lineas.
/// </remarks>
/// <param name="CasoId">El documento que sube.</param>
/// <param name="QuienLoIntento">Nombre del companero que lo llevaba en el peldano de abajo.</param>
/// <param name="CategoriaDeQuienLoIntento">En que peldano se quedo trabado.</param>
/// <param name="Motivo">Por que lo dijo la hoja de ese companero.</param>
public sealed record IntentoAnterior(
    long CasoId,
    string QuienLoIntento,
    int CategoriaDeQuienLoIntento,
    MotivoDeNoCompletar Motivo);

/// <summary>
/// El reporte de quien recibe la segunda vuelta: que se intento, por que no salio, y que dijo el agente.
/// </summary>
/// <remarks>
/// <para><b>Sus palabras, del 2026-09-05</b> (<c>DECISIONES.md</c>): <i>«yo creo otro paquete
/// para los gerentes, para que ellos puedan comunicarse con los líderes de estaca y distrito,
/// de lo que los agentes no pudieron. […] Así que debo crear reporte para ellos con los
/// comentarios de los agentes»</i>.</para>
///
/// <para><b>Un renglon por PERSONA y no por documento.</b> A un lider de estaca no se le llama
/// por un expediente: se le llama por alguien con nombre y con MRN, y un documento puede llevar
/// una persona o diez. Quien va a hacer esa llamada necesita el nombre delante.</para>
///
/// <para><b>De donde sale el comentario, medido el 2026-09-05 sobre este arbol.</b> La hoja
/// del companero trae la casilla «Comentario» (<c>MotivosDeLaHoja.RotuloDelComentario</c>,
/// <c>Columnas.Todas</c>, penúltima antes de la clave), la vuelta la guarda en <c>personas.nota_companero</c>
/// (<c>Paquetes.cs</c>, donde se copia <c>marca.NotaCompanero</c>) y este reporte la lee de ahi. O sea que el camino esta entero.
/// <b>Estuvo cortado hasta la FASE C14</b>, y una hoja devuelta ANTES de que la casilla
/// existiera vuelve sin comentario para siempre: no hay de donde sacarlo.</para>
///
/// <para>Por eso, cuando no hay ni un comentario en toda la lista, <b>se dice en un aviso</b>
/// en vez de dejar la columna en blanco: en blanco se lee como «el agente no dijo nada», y
/// puede ser eso o puede ser que esa hoja sea de antes.</para>
/// </remarks>
public static class ArmadoDeLaSegundaVuelta
{
    /// <summary>El titulo de la seccion; lo miran las pruebas y el indice del informe.</summary>
    public const string TituloDeLaSeccion = "Qué se intentó, y qué dijo el agente";

    /// <summary>El aviso que explica por que la columna del comentario esta vacia.</summary>
    /// <remarks>
    /// La frase nombra la casilla por su rotulo impreso —«Comentario»— a proposito: quien lea
    /// el aviso tiene que poder ir a la hoja y mirar esa casilla, no adivinar cual es.
    /// </remarks>
    internal const string PorQueNoHayComentarios =
        "Ningún documento de esta lista trae comentario del agente. Puede ser que no escribieran "
        + "nada en la casilla «Comentario» de su hoja, o que esa hoja se devolviera antes de que "
        + "la casilla existiera. En los dos casos, lo que dijo el agente hay que preguntárselo "
        + "a él.";

    private static readonly Columna[] Columnas =
    [
        new("Caso", ClaseDeColumna.Texto, 12),
        new("Persona", ClaseDeColumna.Crudo, 26),
        new("MRN", ClaseDeColumna.Texto, 14),
        // ⚠️ 2026-09-08: la unidad en DOS columnas, el numero delante y como Texto. Ver el
        // motivo entero en Vocabulario, donde vivia la funcion que las pegaba.
        new(Vocabulario.RotuloDelNumeroDeUnidad, ClaseDeColumna.Texto, 16),
        new("Barrio o rama", ClaseDeColumna.Crudo, 22),
        new("Viaja el", ClaseDeColumna.Temporal, 12),
        new("Lo intentó", ClaseDeColumna.Crudo, 18),
        new("Por qué no salió", ClaseDeColumna.Crudo, 26),
        new("Lo que dijo el agente", ClaseDeColumna.Crudo, 46),
    ];

    /// <summary>El reporte armado, sin escribirlo.</summary>
    /// <param name="categoria">El peldano que lo recibe.</param>
    /// <param name="intentos">Los documentos que suben, con quien los intento.</param>
    /// <param name="casos">Los casos, por id.</param>
    /// <param name="personasDelCaso">Las personas de cada caso, por id de caso.</param>
    /// <param name="generadoEn">La marca de tiempo; entra desde fuera, no se lee el reloj.</param>
    public static Documento Armar(
        int categoria,
        IReadOnlyList<IntentoAnterior> intentos,
        IReadOnlyDictionary<long, Caso> casos,
        IReadOnlyDictionary<long, IReadOnlyList<Persona>> personasDelCaso,
        string generadoEn)
    {
        ArgumentNullException.ThrowIfNull(intentos);
        ArgumentNullException.ThrowIfNull(casos);
        ArgumentNullException.ThrowIfNull(personasDelCaso);

        var filas = new List<IReadOnlyList<string?>>();
        var conComentario = 0;
        var personas = 0;

        foreach (var intento in intentos)
        {
            if (!casos.TryGetValue(intento.CasoId, out var caso)) continue;
            var suyas = personasDelCaso.GetValueOrDefault(intento.CasoId, []);

            foreach (var persona in suyas)
            {
                personas++;
                var comentario = persona.NotaCompanero;
                if (!string.IsNullOrWhiteSpace(comentario)) conComentario++;

                filas.Add(
                [
                    caso.NumeroCaso,
                    Vocabulario.PersonaOSinNombre(persona.Nombre),
                    string.IsNullOrWhiteSpace(persona.Mrn) ? Vocabulario.SinDato : persona.Mrn,
                    Vocabulario.NumeroDeUnidad(caso.UnidadNumero),
                    Vocabulario.NombreDeUnidad(caso.UnidadNombre),
                    caso.FechaViaje,
                    $"{intento.QuienLoIntento} · categoría {intento.CategoriaDeQuienLoIntento}",
                    Estados.TextoDelMotivo(intento.Motivo),
                    string.IsNullOrWhiteSpace(comentario) ? Vocabulario.SinComentario : comentario,
                ]);
            }
        }

        var avisos = new List<string>();
        if (filas.Count > 0 && conComentario == 0) avisos.Add(PorQueNoHayComentarios);

        return new Documento(
            Vocabulario.Titulo,
            $"Segunda vuelta · categoría {categoria} · {intentos.Count} "
            + Plural.Palabra(intentos.Count, "documento", "documentos"),
            generadoEn,
            PortadaDeLaVuelta(categoria, intentos, filas.Count, conComentario),
            avisos,
            [Seccion(filas, conComentario)]);
    }

    /// <summary>La portada: a que peldano va, cuanta gente, y por que motivo.</summary>
    private static Portada PortadaDeLaVuelta(
        int categoria, IReadOnlyList<IntentoAnterior> intentos, int personas, int conComentario)
    {
        var noSePudo = intentos.Count(i => i.Motivo == MotivoDeNoCompletar.NoSePudoComunicar);
        var noLoHizo = intentos.Count(i => i.Motivo == MotivoDeNoCompletar.ElLiderNoLoHizo);

        return new Portada(
            $"Lo que la categoría {categoria} tiene que resolver",
            $"Son {intentos.Count} " + Plural.Palabra(intentos.Count, "documento", "documentos")
            + $" con {personas} " + Plural.Palabra(personas, "persona", "personas")
            + " que el peldaño de abajo no consiguió cerrar. "
            + $"De {conComentario} de esas personas hay un comentario del agente escrito; "
            + "el resto hay que preguntárselo a él.",
            [
                new Cifra(intentos.Count, "documentos suben", TonoDeCifra.Neutro),
                new Cifra(personas, "personas dentro", TonoDeCifra.Neutro),
                new Cifra(noSePudo, "no se pudo comunicar", noSePudo > 0 ? TonoDeCifra.Malo : TonoDeCifra.Neutro),
                new Cifra(noLoHizo, "el líder no lo hizo", noLoHizo > 0 ? TonoDeCifra.Malo : TonoDeCifra.Neutro),
            ]);
    }

    private static Seccion Seccion(IReadOnlyList<IReadOnlyList<string?>> filas, int conComentario)
        => new(
            TituloDeLaSeccion,
            [
                "Un renglón por persona y no por documento: al líder de estaca o de distrito no se "
                + "le llama por un expediente, se le llama por alguien con nombre y con MRN.",
                "«Lo intentó» dice quién lo llevaba antes y en qué peldaño se quedó. No se pierde: "
                + "si hace falta preguntarle algo, ahí está su nombre.",
                $"De las {filas.Count} " + Plural.Palabra(filas.Count, "persona", "personas")
                + $" de esta tabla, {conComentario} "
                + Plural.Palabra(conComentario, "trae", "traen") + " comentario del agente.",
            ],
            Columnas,
            filas,
            Plural.Con(filas.Count, "persona por resolver", "personas por resolver"));
}
