using Fichas.App.Reportes;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Paquetes;

/// <summary>
/// La ida: generar el Excel de un companero y dejar dicho donde quedo.
/// </summary>
/// <remarks>
/// <para>Vive fuera de la pantalla para poder probarla sin abrir ventana (ADR-0003 §8.1). No
/// decide nada de lo que va dentro de la hoja: eso es de <c>Fichas.Paquetes</c>, detras de
/// <see cref="IPaquetes"/>.</para>
///
/// <para>⚠️ Igual que en los reportes, <b>despues de generar se MIRA el archivo</b>: que la
/// biblioteca conteste «escrito» no es que el archivo este. Con <c>--falso</c> contesta que si
/// y no toca el disco.</para>
///
/// <para>⚠️ <b>Esta clase NO toca la franja de avisos.</b> Los avisos salen dentro del
/// <see cref="ResumenEnPantalla"/> y los deja la pantalla, que es quien esta en el hilo de la
/// ventana. El motivo, medido, esta escrito en <see cref="ResumenEnPantalla"/>.</para>
/// </remarks>
public sealed class OperacionDelPaquete
{
    private readonly IPaquetes _paquetes;
    private readonly IAsignaciones _asignaciones;
    private readonly ICasos _casos;

    /// <summary>Se ata a los tres puertos que necesita y a nada mas.</summary>
    public OperacionDelPaquete(IPaquetes paquetes, IAsignaciones asignaciones, ICasos casos)
    {
        _paquetes = paquetes;
        _asignaciones = asignaciones;
        _casos = casos;
    }

    /// <summary>Lo que lleva ese companero, para pintarlo antes de generar nada.</summary>
    public CargaDelCompanero Carga(long companeroId)
        => CargaDeUnCompanero.Leer(_asignaciones, _casos, companeroId);

    /// <summary>
    /// Genera el Excel de ese companero en esa ruta.
    /// </summary>
    /// <remarks>
    /// Un companero sin ningun caso NO genera un archivo. Una hoja con cabeceras y sin filas
    /// se envia igual, el companero la abre y no sabe que mirar; decirlo aqui es decirlo en el
    /// unico momento en que todavia se le pueden asignar casos sin gastar el trabajo de nadie.
    /// </remarks>
    public ResumenEnPantalla Generar(Companero companero, string ruta)
    {
        ArgumentNullException.ThrowIfNull(companero);

        var carga = Carga(companero.Id);
        if (carga.CasoIds.Count == 0)
        {
            var vacio = NoHayNadaQueMandarle(companero, carga);
            return new ResumenEnPantalla(false, vacio.Linea, vacio.Detalle!, null, [vacio]);
        }

        var resultado = _paquetes.GenerarExcelDeCompanero(companero.Id, carga.CasoIds, ruta);
        var avisos = new List<Aviso>(resultado.Avisos);

        var nombreDelArchivo = Path.GetFileName(ruta);
        var detalle = ResumenEnPantalla.DetalleDe(
            resultado.Avisos,
            LoQueVaYLoQueNo(carga),
            $"Ruta completa: {ruta}");

        if (!resultado.SeEscribio)
        {
            var porQue = resultado.Avisos.Count > 0 ? resultado.Avisos[0].Linea : "no se dijo por qué.";
            return new ResumenEnPantalla(
                false, $"No se generó el paquete de «{companero.Nombre}»: {porQue}", detalle, null, avisos);
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
            return new ResumenEnPantalla(false, noEsta.Linea, detalle, null, avisos);
        }

        var pdf = JuntarLosDocumentos(companero, carga.CasoIds, ruta, avisos);

        return new ResumenEnPantalla(
            true,
            $"Paquete de «{companero.Nombre}»: {carga.Linea} · {nombreDelArchivo} · "
            + ResumenEnPantalla.EnBytes(tamano.Value) + pdf.Cola + ".",
            // El detalle se rehace con TODOS los avisos —los del Excel y los del PDF— porque
            // los del PDF no existian cuando se compuso el de arriba. Se compone de nuevo en
            // vez de pegarle un trozo detras: pegarlo dejaria los avisos del PDF fuera del
            // bloque de avisos y detras del «Ruta completa», donde nadie los busca.
            ResumenEnPantalla.DetalleDe(
                avisos,
                LoQueVaYLoQueNo(carga),
                $"Ruta completa: {ruta}",
                pdf.Detalle),
            ruta,
            avisos);
    }

    /// <summary>El renglon del detalle: lo que va dentro y lo que se quedo fuera, con su motivo.</summary>
    /// <remarks>
    /// Lo que se queda fuera se dice AQUI y no solo en la linea corta. La linea la lee quien
    /// mira de pasada; el detras de «ver» lo abre quien esta comprobando por que un paquete
    /// trae menos casos de los que esperaba, y ese necesita el numero y la razon juntos.
    /// </remarks>
    private static string LoQueVaYLoQueNo(CargaDelCompanero carga)
    {
        var renglon = $"Casos que van dentro: {carga.CasoIds.Count}. Personas: {carga.Personas}.";

        if (carga.YaLosDevolvioCompletos.Count > 0)
        {
            renglon += $" Fuera del paquete: {carga.YaLosDevolvioCompletos.Count} que él ya devolvió "
                + "completos. Siguen asignados a su nombre; solo no se le mandan otra vez.";
        }

        if (carga.VuelvenSinCompletar.Count > 0)
        {
            renglon += $" De los que van, {carga.VuelvenSinCompletar.Count} ya volvieron SIN completar: "
                + "no están hechos y por eso siguen dentro.";
        }

        return renglon;
    }

    /// <summary>
    /// Por que no hay paquete: porque no lleva nada, o porque todo lo suyo ya lo devolvio hecho.
    /// </summary>
    /// <remarks>
    /// Son dos situaciones distintas y decirlas igual manda al dueno a la pantalla equivocada.
    /// «No tiene nada» se arregla en Asignar; «ya lo devolvió todo completo» no se arregla,
    /// esta bien asi, y lo que hay que decirle es cuantos y donde estan.
    /// </remarks>
    private static Aviso NoHayNadaQueMandarle(Companero companero, CargaDelCompanero carga)
    {
        if (carga.YaLosDevolvioCompletos.Count == 0)
        {
            return Aviso.Advierte(
                $"«{companero.Nombre}» no tiene ningún caso asignado: no hay paquete que generar.",
                string.Empty,
                "Asígnele casos desde la pantalla de Asignar y vuelva aquí. No se escribió ningún "
                + "archivo: una hoja con cabeceras y sin ninguna fila no le dice nada a quien la recibe.");
        }

        // Las formas se calculan ANTES y no dentro del texto: un condicional en medio de la
        // frase la parte en trozos que ya no se leen como espanol.
        var asignados = carga.YaLosDevolvioCompletos.Count;
        var esUnoSolo = asignados == 1;
        var casosAsignados = esUnoSolo ? "caso asignado" : "casos asignados";
        var esos = esUnoSolo ? "ese" : "esos";
        var volvieron = esUnoSolo ? "volvió" : "volvieron";
        var seLeMandan = esUnoSolo ? "se le manda" : "se le mandan";

        return Aviso.Informa(
            $"«{companero.Nombre}» ya devolvió completo todo lo que lleva: no hay nada nuevo que mandarle.",
            string.Empty,
            $"Sigue teniendo {asignados} {casosAsignados}, y {esos} ya {volvieron} hechos con su nombre: "
            + $"no {seLeMandan} otra vez. Sus casos siguen siendo suyos en Asignar, en Inicio y en su "
            + "informe. No se escribió ningún archivo.");
    }

    /// <summary>
    /// El PDF con todos los documentos del paquete, al lado del Excel y con su mismo nombre.
    /// </summary>
    /// <remarks>
    /// <para>Lo pidio el dueno: <i>«el Excel normal y también un PDF de todos, asignado, en un
    /// solo PDF»</i>. Va con el MISMO nombre y solo cambia la extensión para que los dos
    /// archivos viajen juntos y se vean seguidos en la carpeta; el compañero recibe dos cosas,
    /// no veintiuna.</para>
    ///
    /// <para>⚠️ <b>Un PDF que no sale NO tumba el paquete.</b> El Excel es lo que hay que
    /// mandar, y ya está escrito cuando se llega aquí: lo que se hace con el fallo es contarlo
    /// —en el detalle y en la franja—, nunca deshacer lo que sí salió. Y se MIRA el archivo,
    /// igual que con el Excel: que la biblioteca conteste «escrito» no es que esté.</para>
    /// </remarks>
    private (string Cola, string Detalle) JuntarLosDocumentos(
        Companero companero, IReadOnlyList<long> casoIds, string rutaDelExcel, List<Aviso> avisos)
    {
        var rutaDelPdf = Path.ChangeExtension(rutaDelExcel, ".pdf");
        var resultado = _paquetes.GenerarPdfDeCompanero(companero.Id, casoIds, rutaDelPdf);
        avisos.AddRange(resultado.Avisos);

        var tamano = resultado.SeEscribio ? ResumenEnPantalla.TamanoDe(rutaDelPdf) : null;
        if (tamano is null)
        {
            var porQue = resultado.Avisos.Count > 0 ? resultado.Avisos[0].Linea : "no se dijo por qué.";
            return (string.Empty,
                $"El paquete salió SIN el PDF de los documentos: {porQue} El Excel de arriba está "
                + "escrito y se puede mandar igual; el compañero tendrá que abrir los escaneos uno a uno.");
        }

        return ($" · PDF {ResumenEnPantalla.EnBytes(tamano.Value)}",
            $"PDF de los documentos: {rutaDelPdf}. Sus hojas van en el mismo orden que los "
            + "renglones del Excel, una por documento.");
    }
}
