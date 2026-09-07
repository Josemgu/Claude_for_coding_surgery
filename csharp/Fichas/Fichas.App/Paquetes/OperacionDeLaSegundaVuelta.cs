using Fichas.App.Reportes;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Paquetes;

/// <summary>
/// El paquete de la segunda vuelta: el Excel de lo que sube, y el reporte de lo que se intento.
/// </summary>
/// <remarks>
/// <para><b>Lo pidio el dueno el 2026-09-05</b> (<c>DECISIONES.md</c>): <i>«Si el agente no
/// pudo comunicarse con el líder y faltan cambios […] yo creo otro paquete para los gerentes,
/// para que ellos puedan comunicarse con los líderes de estaca y distrito, de lo que los
/// agentes no pudieron. […] Así que debo crear reporte para ellos con los comentarios de los
/// agentes»</i>. Son DOS archivos hermanos: la hoja con la que trabajar, y el PDF que dice qué
/// se intentó y qué contestó cada agente.</para>
///
/// <para><b>Es el MISMO generador de Excel que la primera vuelta, con otra lista de casos</b>
/// (criterio C15-3). No hay un segundo motor de paquetes: <see cref="IPaquetes"/> recibe los
/// casos que se le digan, y quién los elige es <see cref="SegundaVuelta"/>.</para>
///
/// <para>⚠️ <b>Este paquete NO asigna los documentos a quien lo recibe, y es a propósito.</b>
/// Asignar es una escritura, la puerta única es <c>OperacionDeAsignar</c> (criterio C5-1), y
/// hacerlo sola desde aquí tomaría por el dueño una decisión que él no ha pedido: hoy un caso
/// puede llevarlo más de un compañero a la vez (la P-11 sigue abierta y es suya). La línea del
/// resumen lo dice con todas las letras para que nadie crea que ya está repartido. <b>Queda
/// como pregunta para él</b>, y va en la entrega.</para>
///
/// <para>⚠️ <b>El reporte solo sale con el motor de verdad.</b> Quien lo escribe entra como
/// <see cref="IReporteDeLaSegundaVuelta"/> y puede llegar NULO: con <c>--falso</c> no hay motor
/// de PDF, y entonces el paquete sale con su Excel y lo dice, en vez de fingir un PDF que no
/// existe. Es el mismo trato que <c>IPaquetes.GenerarPdfDeCompanero</c> le da al almacén que no
/// sabe leer PDF, y el que <c>Servicios.Mantenimiento</c> le da al borrado.</para>
/// </remarks>
public sealed class OperacionDeLaSegundaVuelta
{
    private readonly IPaquetes _paquetes;
    private readonly IReporteDeLaSegundaVuelta? _reporte;
    private readonly ICasos _casos;
    private readonly IAsignaciones _asignaciones;
    private readonly ICompaneros _companeros;

    /// <summary>Se ata a los cinco puertos que necesita y a nada mas.</summary>
    /// <param name="paquetes">El generador del Excel; el MISMO de la primera vuelta.</param>
    /// <param name="reporte">Quien escribe el reporte, o NULO si este arranque no sabe.</param>
    /// <param name="casos">Los documentos.</param>
    /// <param name="asignaciones">Para saber quien lo intento antes.</param>
    /// <param name="companeros">Para saber en que peldano estaba.</param>
    public OperacionDeLaSegundaVuelta(
        IPaquetes paquetes, IReporteDeLaSegundaVuelta? reporte, ICasos casos,
        IAsignaciones asignaciones, ICompaneros companeros)
    {
        _paquetes = paquetes;
        _reporte = reporte;
        _casos = casos;
        _asignaciones = asignaciones;
        _companeros = companeros;
    }

    /// <summary>Lo que subiria al peldano de ese companero, para pintarlo antes de generar nada.</summary>
    public LoQueSube Mirar(Companero quienLoRecibe)
        => SegundaVuelta.Para(_casos, _asignaciones, _companeros, quienLoRecibe);

    /// <summary>
    /// Genera el Excel de la segunda vuelta y, a su lado, el reporte con lo que dijo cada agente.
    /// </summary>
    /// <remarks>
    /// El PDF va con el MISMO nombre y solo cambia la extension, igual que en la primera vuelta:
    /// los dos archivos viajan juntos y se ven seguidos en la carpeta.
    /// </remarks>
    public ResumenEnPantalla Generar(Companero quienLoRecibe, string ruta)
    {
        ArgumentNullException.ThrowIfNull(quienLoRecibe);

        var sube = Mirar(quienLoRecibe);
        if (sube.CasoIds.Count == 0) return NoHayNadaQueSubir(quienLoRecibe, sube);

        var resultado = _paquetes.GenerarExcelDeCompanero(quienLoRecibe.Id, sube.CasoIds, ruta);
        var avisos = new List<Aviso>(resultado.Avisos);
        var nombreDelArchivo = Path.GetFileName(ruta);

        if (!resultado.SeEscribio)
        {
            var porQue = resultado.Avisos.Count > 0 ? resultado.Avisos[0].Linea : "no se dijo por qué.";
            return new ResumenEnPantalla(
                false,
                $"No se generó la segunda vuelta de «{quienLoRecibe.Nombre}»: {porQue}",
                ResumenEnPantalla.DetalleDe(avisos, sube.Linea, $"Ruta completa: {ruta}"),
                null,
                avisos);
        }

        var tamano = ResumenEnPantalla.TamanoDe(ruta);
        if (tamano is null)
        {
            var noEsta = Aviso.Problema(
                $"Se dijo que se escribió «{nombreDelArchivo}», pero el archivo no está.",
                string.Empty,
                $"Se buscó en «{ruta}» justo después de generarlo y no había nada que mirar. Pasa "
                + "siempre que el programa se abre con «--falso»: los datos son inventados y no se "
                + "escribe ningún archivo. Si NO se abrió así, avise: algo se lo llevó.");
            avisos.Add(noEsta);
            return new ResumenEnPantalla(
                false, noEsta.Linea, ResumenEnPantalla.DetalleDe(avisos, sube.Linea), null, avisos);
        }

        var reporte = EscribirElReporte(quienLoRecibe, sube, ruta, avisos);

        return new ResumenEnPantalla(
            true,
            $"Segunda vuelta de «{quienLoRecibe.Nombre}»: {sube.Linea} · {nombreDelArchivo} · "
            + ResumenEnPantalla.EnBytes(tamano.Value) + reporte.Cola + ".",
            ResumenEnPantalla.DetalleDe(
                avisos,
                sube.Linea,
                $"Ruta completa: {ruta}",
                reporte.Detalle,
                NoSeAsignaNada),
            ruta,
            avisos);
    }

    /// <summary>Lo que se dice cuando el peldano de abajo no dejo nada trabado.</summary>
    /// <remarks>
    /// No es un error y no se pinta como tal: es la respuesta buena. Lleva la cuenta entera
    /// —cuantos se miraron y por que se quedo fuera cada uno— porque «no hay nada» sin
    /// denominador se lee igual que «la consulta está rota».
    /// </remarks>
    private static ResumenEnPantalla NoHayNadaQueSubir(Companero quienLoRecibe, LoQueSube sube)
    {
        var nada = Aviso.Informa(
            $"No hay ningún documento que suba a «{quienLoRecibe.Nombre}». {sube.Linea}",
            string.Empty,
            "Sube un documento cuando su hoja volvió como NO completa y el compañero dijo que no se "
            + "pudo comunicar con el líder o que el líder no lo hizo, y siempre que quien lo "
            + $"intentó esté en una categoría más baja que la {sube.Categoria}. No se escribió "
            + "ningún archivo.");

        return new ResumenEnPantalla(false, nada.Linea, nada.Detalle!, null, [nada]);
    }

    /// <summary>Lo que se dice siempre: que el paquete no reparte trabajo por su cuenta.</summary>
    private const string NoSeAsignaNada =
        "Este paquete NO asigna los documentos a quien lo recibe: solo escribe la hoja y el "
        + "reporte. Repartirlos se hace desde la pantalla de Asignar, que es la única puerta del "
        + "programa que reparte trabajo.";

    /// <summary>El PDF con lo que se intento, al lado del Excel y con su mismo nombre.</summary>
    /// <remarks>
    /// ⚠️ <b>Un reporte que no sale NO tumba el paquete.</b> El Excel es lo que hay que mandar y
    /// ya esta escrito cuando se llega aqui: lo que se hace con el fallo es contarlo, nunca
    /// deshacer lo que si salio. Es la misma regla que la primera vuelta aplica a su PDF unido.
    /// </remarks>
    private (string Cola, string Detalle) EscribirElReporte(
        Companero quienLoRecibe, LoQueSube sube, string rutaDelExcel, List<Aviso> avisos)
    {
        if (_reporte is null)
        {
            return (string.Empty,
                "El paquete salió SIN el reporte de lo que se intentó: este arranque no trae motor "
                + "de PDF. Pasa con «--falso», que inventa los datos y no toca ningún archivo del "
                + "disco.");
        }

        var rutaDelPdf = Path.ChangeExtension(rutaDelExcel, ".pdf");
        var resultado = _reporte.Escribir(quienLoRecibe.Categoria, sube.Entran, rutaDelPdf);
        avisos.AddRange(resultado.Avisos);

        var tamano = resultado.SeEscribio ? ResumenEnPantalla.TamanoDe(rutaDelPdf) : null;
        if (tamano is null)
        {
            var porQue = resultado.Avisos.Count > 0 ? resultado.Avisos[0].Linea : "no se dijo por qué.";
            return (string.Empty,
                $"El paquete salió SIN el reporte de lo que se intentó: {porQue} El Excel de arriba "
                + "está escrito y se puede mandar igual.");
        }

        return ($" · reporte {ResumenEnPantalla.EnBytes(tamano.Value)}",
            $"Reporte de lo que se intentó: {rutaDelPdf}. Lleva un renglón por persona con quién lo "
            + "intentó, por qué no salió y lo que dijo ese agente.");
    }
}
