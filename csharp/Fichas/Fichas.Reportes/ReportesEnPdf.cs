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
/// Los informes para los jefes y el historico, sobre los puertos de Fichas.Contratos.
/// </summary>
/// <remarks>
/// ⚠️ <b>NADA de aqui lanza una excepcion por un dato raro</b> (requisito 9 del dueno, «avisar,
/// nunca impedir»). Un periodo del reves, un companero que no existe o una ruta a la que no se
/// puede escribir salen como <see cref="ResultadoDeEscritura"/> con <c>SeEscribio</c> en falso
/// y su aviso de una linea, igual que todo lo demas del programa.
///
/// <b>El archivo se escribe primero en un temporal al lado del destino y se asciende
/// despues.</b> Si el disco se llena a la mitad, lo que queda es un archivo <c>.parcial</c> y no
/// un PDF cortado con el nombre del bueno, que es el que alguien abriria creyendo que esta
/// entero.
///
/// <para>⚠️ <b>El nombre de esta clase se quedo corto el 2026-09-07 y se deja a proposito.</b>
/// Desde ese dia escribe dos formatos —el dueno pidio <i>«también quiero uno con Excel»</i>— y
/// se sigue llamando <c>ReportesEnPdf</c>. Renombrarla tocaria <c>Fichas.App/Cascara</c> y
/// pruebas de otros dos proyectos por un cambio que no arregla nada; queda anotado como deuda
/// de nombre, no como descuido. Lo que importa es que <b>hay un solo motor</b>: los dos
/// formatos salen del MISMO <see cref="Documento"/>, y por eso no pueden decir cifras
/// distintas del mismo mes.</para>
/// </remarks>
public sealed class ReportesEnPdf : IReportes, IReportesDeLaEscalera, IReportesEnExcel
{
    /// <summary>El puerto de casos; se lee entero, archivados incluidos.</summary>
    private readonly ICasos _casos;
    /// <summary>El puerto de personas.</summary>
    private readonly IPersonas _personas;
    /// <summary>El puerto de compañeros; también resuelve el nombre del que se reporta.</summary>
    private readonly ICompaneros _companeros;
    /// <summary>El puerto de asignaciones; para el informe de un agente se leen vivas y retiradas.</summary>
    private readonly IAsignaciones _asignaciones;
    /// <summary>El puerto de procedencia, de donde sale la firma de cada campo.</summary>
    private readonly IProcedencia _procedencia;
    /// <summary>El reloj del programa; el único sitio de esta biblioteca que sabe qué día es.</summary>
    private readonly IReloj _reloj;

    /// <summary>Se ata a los cinco repositorios que necesita y al reloj.</summary>
    /// <remarks>
    /// El reloj entra por aqui y no se llama a <c>DateTime.Now</c> en ningun sitio: de que dia se
    /// considera «ya viajó» depende la cifra de la portada, y eso hay que poder probarlo sin
    /// esperar a que llegue el dia.
    /// </remarks>
    /// <param name="casos">El puerto de casos.</param>
    /// <param name="personas">El puerto de personas.</param>
    /// <param name="companeros">El puerto de compañeros.</param>
    /// <param name="asignaciones">El puerto de asignaciones.</param>
    /// <param name="procedencia">El puerto de procedencia.</param>
    /// <param name="reloj">El reloj; su <c>Ahora()</c> es el «generado el» de todos los informes.</param>
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
    /// <param name="desdeIso">El primer día del periodo, incluido, en «AAAA-MM-DD».</param>
    /// <param name="hastaIso">El último día del periodo, incluido, en «AAAA-MM-DD».</param>
    /// <param name="rutaDestino">Donde queda el PDF; su carpeta se crea si no existe.</param>
    /// <returns>Con <c>SeEscribio</c> en falso y el aviso si el periodo no se lee o la ruta no se puede escribir; si sale bien, cuántas páginas y, si los hay, cuántos caracteres no cupieron.</returns>
    public ResultadoDeEscritura GenerarReporteDelPeriodo(string desdeIso, string hastaIso, string rutaDestino)
    {
        var lectura = Periodo.Leer(desdeIso, hastaIso);
        if (lectura.Periodo is null) return ResultadoDeEscritura.NoSeEscribio(lectura.Problema!);

        var documento = DocumentoDelPeriodo(lectura.Periodo, _reloj.Ahora());
        return Escribir(documento, rutaDestino, $"Reporte del período {lectura.Periodo.EnTexto()}");
    }

    /// <summary>Genera el reporte de un companero en PDF y lo deja en la ruta que se diga.</summary>
    /// <param name="companeroId">El número interno del compañero; uno desactivado sí se reporta.</param>
    /// <param name="desdeIso">El primer día del periodo, incluido, en «AAAA-MM-DD».</param>
    /// <param name="hastaIso">El último día del periodo, incluido, en «AAAA-MM-DD».</param>
    /// <param name="rutaDestino">Donde queda el PDF.</param>
    /// <returns>Con <c>SeEscribio</c> en falso si el compañero no existe, el periodo no se lee o la ruta falla.</returns>
    public ResultadoDeEscritura GenerarReporteDeCompanero(
        long companeroId, string desdeIso, string hastaIso, string rutaDestino)
    {
        var companero = _companeros.Obtener(companeroId);
        if (companero is null) return ResultadoDeEscritura.NoSeEscribio(NoHayEseCompanero(companeroId));

        var lectura = Periodo.Leer(desdeIso, hastaIso);
        if (lectura.Periodo is null) return ResultadoDeEscritura.NoSeEscribio(lectura.Problema!);

        var documento = DocumentoDeCompanero(companeroId, lectura.Periodo, _reloj.Ahora());
        return Escribir(documento, rutaDestino, $"Reporte de {companero.Nombre} en {lectura.Periodo.EnTexto()}");
    }

    /// <summary>Lo que se dice cuando el numero interno que llega no es de nadie.</summary>
    /// <param name="companeroId">El número que no se encontró.</param>
    private static Aviso NoHayEseCompanero(long companeroId)
        => Aviso.Problema(
            $"No hay ningún compañero con el número interno {companeroId}.",
            nameof(companeroId),
            "No se escribió ningún archivo. Un compañero desactivado sí se puede reportar: sigue "
            + "existiendo y sigue teniendo trabajo hecho detrás.");

    /// <summary>Genera el historico completo en PDF y lo deja en la ruta que se diga.</summary>
    /// <param name="rutaDestino">Donde queda el PDF.</param>
    /// <returns>Con <c>SeEscribio</c> en falso solo si la ruta falla: un histórico sin archivados se escribe igual, con su aviso dentro.</returns>
    public ResultadoDeEscritura GenerarHistorico(string rutaDestino)
        => Escribir(DocumentoDelHistorico(_reloj.Ahora()), rutaDestino, "Reporte del histórico completo");

    /// <summary>Genera el reporte de la segunda vuelta en PDF y lo deja en la ruta que se diga.</summary>
    /// <remarks>
    /// Sin nada que subir NO se escribe un PDF. Un reporte con una tabla vacia se manda igual,
    /// el gerente lo abre y no sabe si es que no hay trabajo o si es que algo se rompio al
    /// generarlo; decirlo aqui lo deja claro antes de que salga de la maquina.
    /// </remarks>
    /// <param name="categoria">El peldaño que recibe la vuelta.</param>
    /// <param name="intentos">Los documentos que suben; con la lista vacía no se escribe nada y se avisa.</param>
    /// <param name="rutaDestino">Donde queda el PDF.</param>
    /// <returns>Con <c>SeEscribio</c> en falso y una advertencia si no sube nada; un problema si la ruta falla.</returns>
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

    // ---- lo que pide el puerto del Excel ------------------------------------

    /// <summary>Genera el informe del periodo en <c>.xlsx</c> —UNA hoja, la del mockup v3— y lo deja en la ruta que se diga.</summary>
    /// <remarks>
    /// <para>⚠️ <b>Desde el 2026-09-16 este Excel NO es el <see cref="Documento"/> del PDF con
    /// otra forma.</b> El dueno aprobo el mockup v3 —<i>«Así mismo es que quiero el reporte, como
    /// está en el mockup»</i>— y ese mockup es un <see cref="ResumenDelPeriodo"/>: tarjetas,
    /// tabla por unidad, tabla por agente, grafico por mes y solo los pendientes. Lo que sigue
    /// siendo UNO es lo que cuenta: <see cref="ArmadoDelResumen"/> parte del mismo
    /// <c>Preparacion.Recontar</c> que la portada del PDF, y hay una prueba que cruza las dos
    /// salidas sobre la misma base. Ver <see cref="Formato.HojaDelResumen"/>.</para>
    /// <para>El historico y el informe de agente en Excel siguen saliendo del
    /// <see cref="Documento"/> por <see cref="Formato.LibroDelInforme"/>: no se pidieron.</para>
    /// </remarks>
    /// <param name="desdeIso">El primer día del periodo, incluido, en «AAAA-MM-DD».</param>
    /// <param name="hastaIso">El último día del periodo, incluido, en «AAAA-MM-DD».</param>
    /// <param name="rutaDestino">Donde queda el <c>.xlsx</c>.</param>
    /// <returns>Si sale bien, lo que se puede comprobar abriendo: una hoja, cuántas unidades, agentes y pendientes, y un gráfico; si no, el aviso de qué falló.</returns>
    public ResultadoDeEscritura GenerarReporteDelPeriodoEnExcel(string desdeIso, string hastaIso, string rutaDestino)
    {
        var lectura = Periodo.Leer(desdeIso, hastaIso);
        if (lectura.Periodo is null) return ResultadoDeEscritura.NoSeEscribio(lectura.Problema!);

        var resumen = ArmadoDelResumen.DelPeriodo(Leer(), lectura.Periodo, _reloj.Ahora());
        var problema = Volcar(() => HojaDelResumen.EnBytes(resumen), rutaDestino, ".xlsx");
        if (problema is not null) return ResultadoDeEscritura.NoSeEscribio(problema);

        return ResultadoDeEscritura.BienCon(0, Aviso.Informa(
            $"Reporte del período {lectura.Periodo.EnTexto()} escrito en Excel: 1 hoja con "
            + Plural.Con(resumen.Unidades, "unidad", "unidades") + ", "
            + Plural.Con(resumen.Agentes, "agente", "agentes") + ", "
            + Plural.Con(resumen.Pendientes.Count, "pendiente", "pendientes") + " y 1 gráfico por mes.",
            string.Empty,
            $"El archivo está en «{rutaDestino}». Las cifras son las mismas que las del PDF del mismo período. "
            + LibroDelInforme.QueEsEsteArchivo));
    }

    /// <summary>Genera el informe de un companero en <c>.xlsx</c> y lo deja en la ruta que se diga.</summary>
    /// <param name="companeroId">El número interno del compañero.</param>
    /// <param name="desdeIso">El primer día del periodo, incluido, en «AAAA-MM-DD».</param>
    /// <param name="hastaIso">El último día del periodo, incluido, en «AAAA-MM-DD».</param>
    /// <param name="rutaDestino">Donde queda el <c>.xlsx</c>.</param>
    /// <returns>Con <c>SeEscribio</c> en falso si el compañero no existe, el periodo no se lee o la ruta falla.</returns>
    public ResultadoDeEscritura GenerarReporteDeCompaneroEnExcel(
        long companeroId, string desdeIso, string hastaIso, string rutaDestino)
    {
        var companero = _companeros.Obtener(companeroId);
        if (companero is null) return ResultadoDeEscritura.NoSeEscribio(NoHayEseCompanero(companeroId));

        var lectura = Periodo.Leer(desdeIso, hastaIso);
        if (lectura.Periodo is null) return ResultadoDeEscritura.NoSeEscribio(lectura.Problema!);

        return EscribirElExcel(
            DocumentoDeCompanero(companeroId, lectura.Periodo, _reloj.Ahora()),
            rutaDestino,
            $"Reporte de {companero.Nombre} en {lectura.Periodo.EnTexto()}");
    }

    /// <summary>Genera el historico completo en <c>.xlsx</c> y lo deja en la ruta que se diga.</summary>
    /// <param name="rutaDestino">Donde queda el <c>.xlsx</c>.</param>
    public ResultadoDeEscritura GenerarHistoricoEnExcel(string rutaDestino)
        => EscribirElExcel(DocumentoDelHistorico(_reloj.Ahora()), rutaDestino, "Reporte del histórico completo");

    // ---- los documentos, sin escribirlos ------------------------------------

    /// <summary>El reporte del periodo armado, sin tocar el disco. Es lo que se puede medir.</summary>
    /// <param name="periodo">El periodo ya validado.</param>
    /// <param name="generadoEn">La marca de tiempo del informe; de ella sale el «hoy».</param>
    public Documento DocumentoDelPeriodo(Periodo periodo, string generadoEn)
        => ArmadoDelDocumento.DelPeriodo(Leer(), periodo, generadoEn);

    /// <summary>El historico armado, sin tocar el disco.</summary>
    /// <param name="generadoEn">La marca de tiempo del informe.</param>
    public Documento DocumentoDelHistorico(string generadoEn)
        => ArmadoDelHistorico.Armar(Leer(), generadoEn);

    /// <summary>El reporte de la segunda vuelta armado, sin tocar el disco.</summary>
    /// <remarks>
    /// Lee los casos y sus personas UNO A UNO y no con <see cref="LecturaParaReportes"/>: lo que
    /// sube son unas decenas de documentos, y traer los 3 000 de la base para quedarse con
    /// veinte cuesta el segundo largo que ya esta medido en <c>PruebaDeRendimiento</c>.
    /// </remarks>
    /// <param name="categoria">El peldaño que recibe la vuelta.</param>
    /// <param name="intentos">Los documentos que suben; un caso que ya no existe se salta.</param>
    /// <param name="generadoEn">La marca de tiempo del informe.</param>
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
    /// <param name="companeroId">El número interno del compañero; si no existe, el informe sale igual con «compañero N» de nombre.</param>
    /// <param name="periodo">El periodo ya validado; la semana es su cola de <see cref="ArmadoDelInformeDeAgente.DiasDeLaSemana"/> días.</param>
    /// <param name="generadoEn">La marca de tiempo del informe.</param>
    /// <returns>El informe del periodo recortado a sus casos, con «Lo que hizo» delante y la portada cambiada.</returns>
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

        // ⚠️ 2026-09-08: se recorta con `suyos` —vivas Y retiradas— y no con `vivas`.
        //
        // Del dueno, literal: «en el informe del agente igual, aunque ya no lo tenga asignado»
        // y «que conserve lo retirado». Con `vivas`, este MISMO informe decia 7 arriba —«Lo que
        // hizo» ya se armaba con todas— y 3 de «Los viajes» en adelante: el trabajo terminado
        // desaparecia del informe justo por haberse terminado, porque devolver un caso completo
        // retira su asignacion desde el 2026-09-07.
        //
        // ⚠️ Lo que NO cambia: `vivas.Count` sigue entrando en `LoQueHizo` como
        // `llevaEncimaAhora`. Esa cifra contesta OTRA pregunta —«¿cuánto le queda por delante?»,
        // y su propio resumen dice «Esa cifra es de hoy, no del período»—, asi que sumarle los
        // retirados la dejaria sin poder contestarla. Las dos preguntas conviven a proposito.
        var recortada = lectura.SoloEstosCasos(suyos, nombre);
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
    /// <param name="nombre">El nombre del agente, para el titular.</param>
    /// <param name="loQueHizo">La sección ya armada por <see cref="ArmadoDelInformeDeAgente.LoQueHizo"/>; se leen sus cuatro primeras filas.</param>
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
    /// <param name="loQueHizo">La sección; su columna 1 es la del mes.</param>
    /// <param name="fila">Qué fila, base 0: 0 asignados, 1 contestó, 2 completos, 3 no completos.</param>
    /// <returns>El entero de la celda, o cero si no se puede leer.</returns>
    private static int Cifra(Seccion loQueHizo, int fila)
        => int.TryParse(loQueHizo.Filas[fila][1], out var valor) ? valor : 0;

    // ---- escribir en disco ---------------------------------------------------

    /// <summary>Lee de los cinco puertos todo lo que un informe necesita. Cada informe lee una vez; no hay caché.</summary>
    private LecturaParaReportes Leer()
        => LecturaParaReportes.Leer(_casos, _personas, _companeros, _asignaciones, _procedencia);

    /// <summary>
    /// Vuelca unos bytes en esa ruta pasando por un archivo parcial. Nulo si salio bien.
    /// </summary>
    /// <remarks>
    /// El contenido se construye DENTRO del <c>try</c> y por eso entra como funcion: es lo que
    /// hacia antes de que hubiera dos formatos, y sacarlo fuera cambiaria en silencio que
    /// excepciones se recogen y cuales suben.
    /// </remarks>
    /// <param name="contenido">Lo que hay que escribir, todavia sin construir.</param>
    /// <param name="rutaDestino">Donde queda el archivo.</param>
    /// <param name="extension">Como acaba el archivo, para poder decirlo si falta la ruta.</param>
    /// <returns>Nulo si el archivo quedó en su sitio; el problema si faltó la ruta o el sistema no dejó escribir.</returns>
    private static Aviso? Volcar(Func<byte[]> contenido, string rutaDestino, string extension)
    {
        if (string.IsNullOrWhiteSpace(rutaDestino))
        {
            return Aviso.Problema(
                "No se dijo dónde guardar el reporte.",
                nameof(rutaDestino),
                $"Hace falta la ruta completa del archivo {extension} que se quiere escribir.");
        }

        var parcial = rutaDestino + ".parcial";
        try
        {
            var carpeta = Path.GetDirectoryName(Path.GetFullPath(rutaDestino));
            if (!string.IsNullOrEmpty(carpeta)) Directory.CreateDirectory(carpeta);

            File.WriteAllBytes(parcial, contenido());
            File.Move(parcial, rutaDestino, overwrite: true);
        }
        catch (Exception causa) when (causa is IOException or UnauthorizedAccessException
                                          or NotSupportedException or ArgumentException)
        {
            Limpiar(parcial);
            return Aviso.Problema(
                "No se pudo escribir el reporte en esa ruta.",
                nameof(rutaDestino),
                $"Se intentó escribir «{rutaDestino}» y el sistema contestó: {causa.Message}. "
                + "No se escribió nada a medias: si quedó algo, era el archivo temporal y se borró.");
        }

        return null;
    }

    /// <summary>
    /// Escribe el informe en <c>.xlsx</c> en esa ruta, y dice cuantas hojas y cuantas filas.
    /// </summary>
    /// <remarks>
    /// Las dos cifras del aviso son las que se pueden comprobar abriendo el archivo. «Escrito»
    /// a secas no se puede comprobar sin abrirlo, y esta pantalla ya tuvo el fallo de repetir
    /// lo que dijo quien escribia (ver <c>OperacionDeReporte</c>).
    /// </remarks>
    /// <param name="documento">El documento armado.</param>
    /// <param name="rutaDestino">Donde queda el <c>.xlsx</c>.</param>
    /// <param name="deQue">Cómo se llama el informe en el aviso: «Reporte del período …».</param>
    private static ResultadoDeEscritura EscribirElExcel(Documento documento, string rutaDestino, string deQue)
    {
        var problema = Volcar(() => LibroDelInforme.EnBytes(documento), rutaDestino, ".xlsx");
        if (problema is not null) return ResultadoDeEscritura.NoSeEscribio(problema);

        var hojas = documento.Secciones.Count + 1;
        var filas = LibroDelInforme.CuantasFilasLleva(documento);

        return ResultadoDeEscritura.BienCon(0, Aviso.Informa(
            $"{deQue} escrito en Excel: {hojas} "
            + Plural.Palabra(hojas, "hoja", "hojas") + " y "
            + Plural.Con(filas, "fila", "filas") + ".",
            string.Empty,
            $"El archivo está en «{rutaDestino}». La primera hoja es el resumen, con el índice; "
            + "las demás son una tabla cada una, con la fila 1 fija y el filtro puesto. "
            + LibroDelInforme.QueEsEsteArchivo));
    }

    /// <summary>Escribe el PDF en esa ruta pasando por un archivo parcial.</summary>
    /// <param name="documento">El documento armado.</param>
    /// <param name="rutaDestino">Donde queda el PDF.</param>
    /// <param name="deQue">Cómo se llama el informe en el aviso: «Reporte del período …».</param>
    /// <returns>El aviso con las páginas y, si algún carácter no cupo en la fuente, una advertencia más.</returns>
    private static ResultadoDeEscritura Escribir(Documento documento, string rutaDestino, string deQue)
    {
        var problema = Volcar(() => Maqueta.ConstruirPdf(documento), rutaDestino, ".pdf");
        if (problema is not null) return ResultadoDeEscritura.NoSeEscribio(problema);

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

    /// <summary>Borra el archivo parcial si quedó; si tampoco se puede borrar, no hace nada más (ver el comentario de dentro).</summary>
    /// <param name="parcial">La ruta del <c>.parcial</c>.</param>
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
