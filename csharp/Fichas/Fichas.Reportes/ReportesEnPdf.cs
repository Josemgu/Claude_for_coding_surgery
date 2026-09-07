using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Reportes.Armado;
using Fichas.Reportes.Consultas;
using Fichas.Reportes.Formato;
using Fichas.Reportes.Modelo;
using Fichas.Reportes.Reglas;

namespace Fichas.Reportes;

/// <summary>
/// Los reportes en PDF para los jefes y el historico, sobre los puertos de Fichas.Contratos.
/// </summary>
/// <remarks>
/// ⚠️ <b>NADA de aqui lanza una excepcion por un dato raro</b> (requisito 9 del dueno, «avisar,
/// nunca impedir»). Un periodo del reves, un companero que no existe o una ruta a la que no se
/// puede escribir salen como <see cref="ResultadoDeEscritura"/> con <c>SeEscribio</c> en falso
/// y su aviso de una linea, igual que todo lo demas del programa.
///
/// <b>El PDF se escribe primero en un archivo temporal al lado del destino y se asciende
/// despues.</b> Si el disco se llena a la mitad, lo que queda es un archivo <c>.parcial</c> y no
/// un PDF cortado con el nombre del bueno, que es el que alguien abriria creyendo que esta
/// entero.
/// </remarks>
public sealed class ReportesEnPdf : IReportes, IReportesDeLaEscalera
{
    private readonly ICasos _casos;
    private readonly IPersonas _personas;
    private readonly ICompaneros _companeros;
    private readonly IAsignaciones _asignaciones;
    private readonly IProcedencia _procedencia;
    private readonly IReloj _reloj;

    /// <summary>Se ata a los cinco repositorios que necesita y al reloj.</summary>
    /// <remarks>
    /// El reloj entra por aqui y no se llama a <c>DateTime.Now</c> en ningun sitio: de que dia se
    /// considera «ya viajó» depende la cifra de la portada, y eso hay que poder probarlo sin
    /// esperar a que llegue el dia.
    /// </remarks>
    public ReportesEnPdf(
        ICasos casos, IPersonas personas, ICompaneros companeros,
        IAsignaciones asignaciones, IProcedencia procedencia, IReloj reloj)
    {
        _casos = casos;
        _personas = personas;
        _companeros = companeros;
        _asignaciones = asignaciones;
        _procedencia = procedencia;
        _reloj = reloj;
    }

    // ---- lo que pide el contrato -------------------------------------------

    /// <summary>Genera el reporte del periodo en PDF y lo deja en la ruta que se diga.</summary>
    public ResultadoDeEscritura GenerarReporteDelPeriodo(string desdeIso, string hastaIso, string rutaDestino)
    {
        var lectura = Periodo.Leer(desdeIso, hastaIso);
        if (lectura.Periodo is null) return ResultadoDeEscritura.NoSeEscribio(lectura.Problema!);

        var documento = DocumentoDelPeriodo(lectura.Periodo, _reloj.Ahora());
        return Escribir(documento, rutaDestino, $"Reporte del período {lectura.Periodo.EnTexto()}");
    }

    /// <summary>Genera el reporte de un companero en PDF y lo deja en la ruta que se diga.</summary>
    public ResultadoDeEscritura GenerarReporteDeCompanero(
        long companeroId, string desdeIso, string hastaIso, string rutaDestino)
    {
        var companero = _companeros.Obtener(companeroId);
        if (companero is null)
        {
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                $"No hay ningún compañero con el número interno {companeroId}.",
                nameof(companeroId),
                "No se escribió ningún PDF. Un compañero desactivado sí se puede reportar: sigue "
                + "existiendo y sigue teniendo trabajo hecho detrás."));
        }

        var lectura = Periodo.Leer(desdeIso, hastaIso);
        if (lectura.Periodo is null) return ResultadoDeEscritura.NoSeEscribio(lectura.Problema!);

        var documento = DocumentoDeCompanero(companeroId, lectura.Periodo, _reloj.Ahora());
        return Escribir(documento, rutaDestino, $"Reporte de {companero.Nombre} en {lectura.Periodo.EnTexto()}");
    }

    /// <summary>Genera el historico completo en PDF y lo deja en la ruta que se diga.</summary>
    public ResultadoDeEscritura GenerarHistorico(string rutaDestino)
        => Escribir(DocumentoDelHistorico(_reloj.Ahora()), rutaDestino, "Reporte del histórico completo");

    /// <summary>Genera el reporte de la segunda vuelta en PDF y lo deja en la ruta que se diga.</summary>
    /// <remarks>
    /// Sin nada que subir NO se escribe un PDF. Un reporte con una tabla vacia se manda igual,
    /// el gerente lo abre y no sabe si es que no hay trabajo o si es que algo se rompio al
    /// generarlo; decirlo aqui lo deja claro antes de que salga de la maquina.
    /// </remarks>
    public ResultadoDeEscritura GenerarReporteDeLaSegundaVuelta(
        int categoria, IReadOnlyList<IntentoAnterior> intentos, string rutaDestino)
    {
        ArgumentNullException.ThrowIfNull(intentos);

        if (intentos.Count == 0)
        {
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Advierte(
                $"No hay ningún documento que suba a la categoría {categoria}: no hay reporte que generar.",
                nameof(intentos),
                "Sube un documento cuando su hoja volvió como no completa y el compañero dijo que no "
                + "se pudo comunicar con el líder o que el líder no lo hizo. No se escribió ningún "
                + "archivo: un PDF con la tabla vacía se lee igual que uno que falló al generarse."));
        }

        var documento = DocumentoDeLaSegundaVuelta(categoria, intentos, _reloj.Ahora());
        return Escribir(documento, rutaDestino, $"Reporte de la segunda vuelta a la categoría {categoria}");
    }

    // ---- los documentos, sin escribirlos ------------------------------------

    /// <summary>El reporte del periodo armado, sin tocar el disco. Es lo que se puede medir.</summary>
    public Documento DocumentoDelPeriodo(Periodo periodo, string generadoEn)
        => ArmadoDelDocumento.DelPeriodo(Leer(), periodo, generadoEn);

    /// <summary>El historico armado, sin tocar el disco.</summary>
    public Documento DocumentoDelHistorico(string generadoEn)
        => ArmadoDelHistorico.Armar(Leer(), generadoEn);

    /// <summary>El reporte de la segunda vuelta armado, sin tocar el disco.</summary>
    /// <remarks>
    /// Lee los casos y sus personas UNO A UNO y no con <see cref="LecturaParaReportes"/>: lo que
    /// sube son unas decenas de documentos, y traer los 3 000 de la base para quedarse con
    /// veinte cuesta el segundo largo que ya esta medido en <c>PruebaDeRendimiento</c>.
    /// </remarks>
    public Documento DocumentoDeLaSegundaVuelta(
        int categoria, IReadOnlyList<IntentoAnterior> intentos, string generadoEn)
    {
        ArgumentNullException.ThrowIfNull(intentos);

        var casos = new Dictionary<long, Caso>();
        var personas = new Dictionary<long, IReadOnlyList<Persona>>();

        foreach (var intento in intentos)
        {
            if (casos.ContainsKey(intento.CasoId)) continue;

            var caso = _casos.Obtener(intento.CasoId);
            if (caso is null) continue;

            casos[intento.CasoId] = caso;
            personas[intento.CasoId] = _personas.DeCaso(intento.CasoId);
        }

        return ArmadoDeLaSegundaVuelta.Armar(categoria, intentos, casos, personas, generadoEn);
    }

    /// <summary>El informe de un agente: que hizo en el mes y en la semana, y luego sus casos.</summary>
    /// <remarks>
    /// <para><b>Son DOS preguntas y las contesta las dos, en este orden.</b> Delante va «Lo que
    /// hizo», que es la que hizo el dueno el 2026-09-05: <i>«lo que hicieron los agentes en ese
    /// mes y lo que hicieron en esa semana, qué hicieron»</i>. Detras va lo que este metodo ya
    /// hacia: el informe de los jefes recortado a sus casos, que contesta <i>«¿cómo están sus
    /// casos?»</i>. Aquello no se tira porque tampoco estaba mal: estaba incompleto.</para>
    ///
    /// <para>Se recorta la asignacion a la suya a proposito. Un caso puede llevarlo mas de un
    /// companero (la P-11 sigue abierta), y en SU informe la tabla del equipo tiene que hablar de
    /// el: la pregunta que contesta es «¿de qué respondo yo?», no «¿quién más lo lleva?».</para>
    ///
    /// <para>⚠️ <b>La portada cambia y es a proposito.</b> La del informe del periodo abre con
    /// «cuántas personas viajaron sin estar listas», que es una pregunta de la direccion sobre
    /// todo el trabajo. En el informe de UN agente esa cifra recortada a sus casos se lee como
    /// una acusacion sobre el, y ademas no es lo que se pidio. Abre con lo que hizo.</para>
    /// </remarks>
    public Documento DocumentoDeCompanero(long companeroId, Periodo periodo, string generadoEn)
    {
        var companero = _companeros.Obtener(companeroId);
        var nombre = companero?.Nombre ?? $"compañero {companeroId}";

        // TODAS sus asignaciones, vivas y retiradas: «lo que hizo» incluye lo que ya devolvio.
        //
        // ⚠️ 2026-09-07. Esta linea decia «devolver una hoja es justamente lo que retira la
        // asignacion», y es FALSO: nadie llama a `Retirar` en el camino de la vuelta —
        // comprobado con `grep -rn "Retirar" Fichas.App/Paquetes/ Fichas.Paquetes/`, cero
        // resultados fuera de los binarios—, y midiendolo en la base: tras devolver tres
        // filas, las tres asignaciones seguian `activa = 1`.
        //
        // La decision de pedirlas todas era correcta y su motivo era falso. El motivo de
        // verdad: una asignacion se retira A MANO, y desde el 2026-09-07 hay un boton que
        // retira las de un companero de golpe. Mirando solo las vivas, ese boton borraria del
        // informe del agente el trabajo que ya habia hecho.
        var todasLasSuyas = _asignaciones
            .Listar(new FiltroDeAsignaciones(CompaneroId: companeroId, SoloActivas: false), new Pagina(0, int.MaxValue))
            .Elementos;
        var vivas = todasLasSuyas.Where(a => a.Activa).Select(a => a.CasoId).ToHashSet();
        var suyos = todasLasSuyas.Select(a => a.CasoId).ToHashSet();

        var lectura = Leer();
        var susCasos = lectura.CasosConSuVerificacion
            .Where(c => suyos.Contains(c.Caso.Id))
            .Select(c => c.Caso)
            .ToList();

        var semana = TrabajoDeAgente.LaColaDe(periodo, ArmadoDelInformeDeAgente.DiasDeLaSemana);
        var loQueHizo = ArmadoDelInformeDeAgente.LoQueHizo(
            periodo,
            semana,
            TrabajoDeAgente.En(periodo, companeroId, susCasos, todasLasSuyas),
            TrabajoDeAgente.En(semana, companeroId, susCasos, todasLasSuyas),
            vivas.Count);

        var recortada = lectura.SoloEstosCasos(vivas, nombre);
        var documento = ArmadoDelDocumento.DelPeriodo(recortada, periodo, generadoEn);

        return documento with
        {
            Subtitulo = $"{nombre} · período: {periodo.EnTexto()}",
            Portada = PortadaDelAgente(nombre, loQueHizo),
            Secciones = [loQueHizo, .. documento.Secciones],
        };
    }

    /// <summary>La portada del informe de un agente: su nombre y las cuatro cifras que le tocan.</summary>
    /// <remarks>
    /// Las cuatro salen de la seccion que se acaba de armar y NO se vuelven a contar: dos
    /// codigos que cuentan lo mismo por su cuenta acaban dando dos numeros distintos, y una
    /// portada que no coincide con su propia tabla es peor que no tener portada.
    /// </remarks>
    private static Portada PortadaDelAgente(string nombre, Seccion loQueHizo)
    {
        var contesto = Cifra(loQueHizo, 1);
        var completas = Cifra(loQueHizo, 2);
        var noCompletas = Cifra(loQueHizo, 3);
        var asignados = Cifra(loQueHizo, 0);

        return new Portada(
            $"Qué hizo {nombre}",
            $"Contestó {contesto} de los {asignados} documentos que se le asignaron en el período. "
            + $"De los que contestó, {completas} quedaron completos y {noCompletas} no.",
            [
                new Cifra(contesto, "documentos contestó", TonoDeCifra.Neutro),
                new Cifra(completas, "quedaron completos", TonoDeCifra.Bueno),
                new Cifra(noCompletas, "quedaron sin completar", noCompletas > 0 ? TonoDeCifra.Malo : TonoDeCifra.Bueno),
                new Cifra(asignados, "se le asignaron", TonoDeCifra.Neutro),
            ]);
    }

    /// <summary>La cifra del mes de esa fila de «Lo que hizo».</summary>
    private static int Cifra(Seccion loQueHizo, int fila)
        => int.TryParse(loQueHizo.Filas[fila][1], out var valor) ? valor : 0;

    // ---- escribir en disco ---------------------------------------------------

    private LecturaParaReportes Leer()
        => LecturaParaReportes.Leer(_casos, _personas, _companeros, _asignaciones, _procedencia);

    /// <summary>Escribe el PDF en esa ruta pasando por un archivo parcial.</summary>
    private static ResultadoDeEscritura Escribir(Documento documento, string rutaDestino, string deQue)
    {
        if (string.IsNullOrWhiteSpace(rutaDestino))
        {
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                "No se dijo dónde guardar el reporte.",
                nameof(rutaDestino),
                "Hace falta la ruta completa del archivo .pdf que se quiere escribir."));
        }

        var parcial = rutaDestino + ".parcial";
        try
        {
            var carpeta = Path.GetDirectoryName(Path.GetFullPath(rutaDestino));
            if (!string.IsNullOrEmpty(carpeta)) Directory.CreateDirectory(carpeta);

            File.WriteAllBytes(parcial, Maqueta.ConstruirPdf(documento));
            File.Move(parcial, rutaDestino, overwrite: true);
        }
        catch (Exception causa) when (causa is IOException or UnauthorizedAccessException
                                          or NotSupportedException or ArgumentException)
        {
            Limpiar(parcial);
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                "No se pudo escribir el reporte en esa ruta.",
                nameof(rutaDestino),
                $"Se intentó escribir «{rutaDestino}» y el sistema contestó: {causa.Message}. "
                + "No se escribió nada a medias: si quedó algo, era el archivo temporal y se borró."));
        }

        var paginas = Maqueta.RepartirEnPaginas(Maqueta.LineasDelDocumento(documento, [])).Count;
        var perdidos = Maqueta.ContarCaracteresQueNoCaben(documento);

        var avisos = new List<Aviso>
        {
            Aviso.Informa(
                $"{deQue} escrito en {paginas} "
                + Plural.Palabra(paginas, "página", "páginas") + ".",
                string.Empty,
                $"El archivo está en «{rutaDestino}»."),
        };

        if (perdidos > 0)
        {
            avisos.Add(Aviso.Advierte(
                Plural.Con(perdidos, "carácter no cabe", "caracteres no caben")
                + " en la fuente del PDF y "
                + Plural.Palabra(perdidos, "sale", "salen") + " como «?».",
                string.Empty,
                Maqueta.AvisoDeCaracteresPerdidos(perdidos)));
        }

        return ResultadoDeEscritura.BienCon(0, [.. avisos]);
    }

    private static void Limpiar(string parcial)
    {
        try
        {
            if (File.Exists(parcial)) File.Delete(parcial);
        }
        catch (Exception causa) when (causa is IOException or UnauthorizedAccessException)
        {
            // Si tampoco se puede borrar el temporal, no hay nada mas que hacer y no es motivo
            // para tumbar la operacion: lo que importa —que el destino no quede a medias— ya se
            // cumplio, porque el destino nunca se llego a tocar.
        }
    }
}
