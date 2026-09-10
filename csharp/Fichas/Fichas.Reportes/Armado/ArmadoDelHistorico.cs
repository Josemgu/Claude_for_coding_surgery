using Fichas.Contratos.Modelos;
using Fichas.Reportes.Consultas;
using Fichas.Reportes.Modelo;
using Fichas.Reportes.Reglas;

namespace Fichas.Reportes.Armado;

/// <summary>
/// El historico: los casos archivados, marcados como archivados.
/// </summary>
/// <remarks>
/// ⚠️ <b>Esto NO es un porte del proyecto viejo: el viejo no tiene historico.</b> Medido por el
/// agente que lo leyo entero: <c>grep -rn -i "historico|histórico"</c> sobre
/// <c>pdf-a-excel</c> no devuelve ninguna linea, y su unico PDF de direccion es el informe del
/// periodo. La consulta que se porta es <c>casos_archivados</c> de <c>datos/archivo.py</c> del
/// Python NUEVO, que es lo unico que hay escrito. Va dicho en la entrega para que nadie lo lea
/// como «igual que el viejo».
///
/// Lo que manda la decision del dueno (DECISIONES.md, 2026-09-03, literal): «Lo que archivo
/// debe verse en el calendario, debe decir archivado». Aqui cada fila lo dice con la palabra y
/// con su fecha.
/// </remarks>
public static class ArmadoDelHistorico
{
    /// <summary>
    /// Que le pasa de verdad a un caso cuando se archiva. UNA frase, usada en los dos sitios.
    /// </summary>
    /// <remarks>
    /// <para>⛔ Existe porque el informe se contradecia a si mismo. Medido por QA sobre el
    /// paquete publicado el 2026-09-04: la portada decia «Lo que hacen es salir de las listas
    /// de trabajo del dia» y la nota de la tabla, en el mismo PDF, «Estos casos NO salen de
    /// las listas de trabajo del dia».</para>
    ///
    /// <para><b>Ninguna de las dos era cierta.</b> Y la frase que las sustituyo tampoco lo es
    /// ya: decia que un archivado <i>«sigue viéndose, marcado, en Revisar, en Asignar y en el
    /// calendario»</i>, y eso valia hasta que el dueno decidio el 2026-09-05 lo contrario —<i>«cuando
    /// yo archive, debe salir del sistema visible, pero se queda como histórico para los
    /// reportes»</i>— y otros programadores lo aplicaron en Inicio, Revisar y Asignar. <b>Una
    /// frase que se imprime en el PDF de los jefes no puede quedarse una version por detras del
    /// programa.</b></para>
    ///
    /// <para>Como se comporta HOY, medido sobre este arbol el 2026-09-05, sitio por sitio:</para>
    /// <list type="bullet">
    /// <item><b>Deja de salir</b> en el selector de Correccion —<c>FiltroDeCasos.Todo</c>, que
    /// trae <c>IncluirArchivados</c> en falso (<c>PaginaDeCorreccion.xaml.cs:99</c>)—; en
    /// Asignar —<c>ListaParaAsignar.SinFiltroDeEstadoNiArchivados</c>, con
    /// <c>IncluirArchivados: false</c> (<c>ListaParaAsignar.cs:58</c>)—; en Revisar, que pide
    /// con <c>conArchivados: false</c> mientras la casilla este sin marcar
    /// (<c>PaginaDeRevisar.xaml.cs:94</c>, <c>TableroDeRevisar.cs:82</c>); y en las cifras del
    /// Inicio, que se calculan sobre <c>todos.Where(c =&gt; !c.Archivado)</c>
    /// (<c>LectorDelInicio.cs:72</c>).</item>
    /// <item><b>Sigue viendose</b>, marcado, en el calendario del Inicio —que se arma sobre la
    /// lista entera, leida con <c>IncluirArchivados = true</c> en
    /// <c>LectorDelInicio.cs:158</c>—; y en Revisar cuando se marca «Ver los archivados», que
    /// es desde donde se desarchiva.</item>
    /// <item><b>Sigue contando</b> en los reportes del periodo:
    /// <c>LecturaParaReportes.cs:84</c> lee con <c>IncluirArchivados: true</c>.</item>
    /// </list>
    ///
    /// <para>La frase dice esas tres cosas y no una generalizacion, porque la generalizacion
    /// es justo lo que se pudo escribir dos veces al reves sin que nadie lo notara. Es
    /// <c>const</c> y no dos literales a proposito: dos copias se vuelven a separar.</para>
    ///
    /// <para>⚠️ <b>Quien la vuelva a cambiar tiene que volver a medir los cinco sitios.</b> La
    /// prueba que la vigila desde <c>Fichas.Pruebas.App</c> llama a los filtros de verdad de la
    /// aplicacion; la de <c>Fichas.Pruebas.Reportes</c> NO puede —ese proyecto no referencia la
    /// aplicacion— y lo dice en su propio texto.</para>
    /// </remarks>
    public const string QueLePasaAUnArchivado =
        "Archivar no borra nada, pero saca el caso de la vista entera: deja de salir en el "
        + "selector de Corrección, en Asignar, en Revisar, en el calendario y en las cifras "
        + "del Inicio, y no cuenta en ninguna de ellas. Solo vuelve a verse en Revisar cuando "
        + "se marca «Ver los archivados», que es desde donde se desarchiva. Y sigue contando "
        + "entero en los reportes del período y en este histórico.";

    private static readonly Columna[] ColumnasDelHistorico =
    [
        new("N.º de caso", ClaseDeColumna.Texto, 12),
        // ⚠️ 2026-09-08: la unidad en DOS columnas, el numero delante y como Texto. Ver el
        // motivo entero en Vocabulario, donde vivia la funcion que las pegaba.
        new(Vocabulario.RotuloDelNumeroDeUnidad, ClaseDeColumna.Texto, 16),
        new("Unidad", ClaseDeColumna.Crudo, 26),
        new("Fecha de viaje", ClaseDeColumna.Temporal, 14),
        new("Estado de la recomendación", ClaseDeColumna.Crudo, 20),
        new("Personas", ClaseDeColumna.Crudo, 10),
        new("No pudieron viajar", ClaseDeColumna.Crudo, 14),
        new("Archivado", ClaseDeColumna.Crudo, 18),
    ];

    /// <summary>Arma el historico entero. Devuelve el documento, sin escribirlo.</summary>
    public static Documento Armar(LecturaParaReportes lectura, string generadoEn)
    {
        var archivados = lectura.Archivados();
        var sinPoderViajar = archivados.Sum(a => a.PersonasQueNoViajaron);
        var personas = archivados.Sum(a => a.Personas);

        return new Documento(
            Vocabulario.Titulo,
            "Histórico — todo lo que se ha archivado, desde lo último",
            generadoEn,
            Portada(archivados.Count, personas, sinPoderViajar, lectura.Casos.Count),
            Avisos(archivados.Count, lectura.Casos.Count),
            [Tabla(archivados)]);
    }

    /// <summary>La portada del historico: cuanto se ha cerrado y sobre cuanto.</summary>
    /// <remarks>
    /// El denominador va DELANTE a proposito: «40 casos archivados» no dice nada sin saber si
    /// la base tiene 45 o 3 000, y un numero sin su denominador es la forma mas facil de
    /// enganar a quien lee un informe.
    /// </remarks>
    private static Portada Portada(int archivados, int personas, int sinPoderViajar, int casosEnLaBase)
        => new(
            "Qué se ha archivado, y qué sigue contando",
            $"{archivados} de los {casosEnLaBase} casos de la base están archivados. "
            + QueLePasaAUnArchivado,
            [
                new Cifra(archivados, "casos archivados", TonoDeCifra.Neutro),
                new Cifra(casosEnLaBase - archivados, "casos todavía abiertos", TonoDeCifra.Neutro),
                new Cifra(personas, "personas en los archivados", TonoDeCifra.Neutro),
                new Cifra(
                    sinPoderViajar,
                    "de ellas no pudieron viajar",
                    sinPoderViajar > 0 ? TonoDeCifra.Malo : TonoDeCifra.Bueno),
            ]);

    /// <summary>De que no se fia este historico. Nace de una medicion, no esta escrito a mano.</summary>
    private static IReadOnlyList<string> Avisos(int archivados, int casosEnLaBase)
    {
        var avisos = new List<string>();

        if (archivados == 0 && casosEnLaBase > 0)
        {
            avisos.Add(
                $"Todavía no se ha archivado ningún caso de los {casosEnLaBase} que hay en la "
                + "base. Un histórico vacío no significa que no haya trabajo cerrado: significa "
                + "que nadie ha archivado nada todavía.");
        }

        return avisos;
    }

    private static Seccion Tabla(IReadOnlyList<CasoArchivado> archivados)
        => new(
            "Histórico — casos archivados",
            [
                "El último archivado va primero. «Archivado» dice la palabra y la fecha en que se "
                + "archivó, que es lo que el dueño pidió que se viera.",
                QueLePasaAUnArchivado,
            ],
            ColumnasDelHistorico,
            archivados
                .Select(a => (IReadOnlyList<string?>)new string?[]
                {
                    a.Caso.NumeroCaso,
                    Vocabulario.NumeroDeUnidad(a.Caso.UnidadNumero),
                    Vocabulario.NombreDeUnidad(a.Caso.UnidadNombre),
                    a.Caso.FechaViaje ?? Vocabulario.SinDato,
                    Vocabulario.TextoDelEstado(a.Caso.EstadoRecomendacion),
                    SeccionesDeDireccion.Numero(a.Personas),
                    SeccionesDeDireccion.Numero(a.PersonasQueNoViajaron),
                    TextoDeArchivado(a.Caso),
                })
                .ToList(),
            archivados.Count == 0
                ? "Todavía no se ha archivado ningún caso."
                : Plural.Con(archivados.Count, "caso archivado", "casos archivados"));

    /// <summary>La palabra «archivado» con su fecha, o sola si la fecha no consta.</summary>
    /// <remarks>
    /// El esquema ata las dos —o las dos o ninguna—, pero un dato que llega roto no puede
    /// tumbar el informe: se escribe lo que hay y se dice que falta la fecha (requisito 9).
    /// </remarks>
    private static string TextoDeArchivado(Caso caso)
        => string.IsNullOrWhiteSpace(caso.FechaArchivado)
            ? $"{Vocabulario.Archivado}, sin fecha"
            : $"{Vocabulario.Archivado} el {caso.FechaArchivado}";
}
