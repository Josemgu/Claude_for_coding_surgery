using Fichas.Contratos.Consultas;
using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;

namespace Fichas.Contratos.Puertos;

/// <summary>
/// El Excel que va al companero y el que vuelve firmado por el.
/// </summary>
/// <remarks>
/// La regla del dueno (CLAUDE.md, regla permanente 5 precisada el 2026-09-03): el Excel
/// que devuelve el companero es el que escribe <c>completa</c> / <c>no_completa</c>, con
/// SU nombre. Eso NO es firmar campos: <c>procedencia_campo.verificado</c> sigue en 0.
/// La clave de reconciliacion es caso:MRN:id; una fila que no case va a
/// <c>filas_descartadas</c>, no se casa por nombre.
/// <para>
/// <b>Quién lo implementa:</b> <c>Fichas.Paquetes.Paquetes</c> (escribe y lee el Excel de
/// verdad y escribe en la base por <see cref="ICasos"/>, <see cref="IPersonas"/> e
/// <see cref="IIlegibles"/>) y <c>Fichas.Datos.Falso.PaquetesFalsos</c>, que no toca el
/// disco y aplica marcas solo al almacén en memoria. <b>Quién lo consume:</b>
/// <c>Fichas.App/Paquetes</c> (<c>OperacionDelPaquete</c>, <c>OperacionDeLaVuelta</c>,
/// <c>OperacionDeLaSegundaVuelta</c>).
/// </para>
/// </remarks>
public interface IPaquetes
{
    /// <summary>Genera el Excel de ida de un companero con los casos que lleva y lo deja en la ruta que se diga.</summary>
    /// <remarks>
    /// <b>No escribe en la base</b>; escribe un archivo. No se escribe, y se dice con un
    /// problema, cuando el compañero no existe, cuando los casos pedidos no tienen ninguna
    /// persona, o cuando el archivo está abierto por otro programa (el que hubiera queda
    /// intacto). Las personas sin MRN entran igual y salen avisadas: son las que volverán
    /// descartadas.
    /// </remarks>
    /// <param name="companeroId">De quién es el paquete; su nombre va en la hoja.</param>
    /// <param name="casoIds">Los casos que lleva, en el orden en que se quieren en la hoja.</param>
    /// <param name="rutaDestino">La ruta completa del <c>.xlsx</c>.</param>
    /// <returns>Escrito, con un aviso que dice cuántas personas y casos lleva la hoja; el id devuelto es el del compañero, no una fila. O no escrito con su motivo.</returns>
    ResultadoDeEscritura GenerarExcelDeCompanero(long companeroId, IReadOnlyList<long> casoIds, string rutaDestino);

    /// <summary>
    /// Junta en UN solo PDF las hojas de los documentos que lleva el companero, en el mismo
    /// orden en que salen en el Excel.
    /// </summary>
    /// <remarks>
    /// <para>Lo pidio el dueno con sus palabras: <i>«los paquetes debe colocar a todos los PDF
    /// originales en un solo para el agente; es decir, el Excel normal y tambien un PDF de
    /// todos, asignado, en un solo PDF»</i>. Hoy el companero recibe una hoja de calculo y
    /// veinte archivos sueltos que tiene que ir abriendo; con esto recibe dos cosas.</para>
    ///
    /// <para>⚠️ <b>El Excel NO cambia</b>, y esto no lo sustituye: son dos archivos hermanos
    /// del mismo paquete. Este metodo no escribe nada en la base.</para>
    ///
    /// <para><b>Un documento que no se pueda leer no tumba el paquete.</b> Esa hoja se queda
    /// fuera, el PDF sale con las demas, y el motivo va en <c>Avisos</c> nombrando el caso: un
    /// paquete que no se genera deja al companero sin nada, y uno al que le falta una hoja lo
    /// deja trabajando en las diecinueve que si estan.</para>
    ///
    /// <para>La implementacion por defecto NO escribe: existe para que un almacen que no sabe
    /// leer PDF —el de <c>--falso</c>, que no toca el disco— siga cumpliendo el contrato
    /// diciendolo, en vez de fingir un archivo que no esta.</para>
    /// </remarks>
    /// <param name="companeroId">De quien es el paquete; solo se usa para nombrarlo en los avisos.</param>
    /// <param name="casoIds">Los casos, EN EL MISMO ORDEN con el que se pidio el Excel.</param>
    /// <param name="rutaDestino">Donde queda el PDF unido.</param>
    /// <returns>Escrito, con los avisos de las hojas que quedaron fuera; o no escrito con su motivo, que en el doble es siempre «este almacén no sabe juntar PDF».</returns>
    ResultadoDeEscritura GenerarPdfDeCompanero(long companeroId, IReadOnlyList<long> casoIds, string rutaDestino)
        => ResultadoDeEscritura.NoSeEscribio(Aviso.Advierte(
            "Este almacén no sabe juntar los PDF de los documentos: el paquete sale solo con el Excel.",
            string.Empty,
            "Pasa con «--falso», que inventa los datos y no toca ningún archivo del disco. "
            + "Con la base de verdad el PDF unido sí se escribe."));

    /// <summary>Lee el Excel devuelto y devuelve lo que caso, lo que no y lo que hay que decir.</summary>
    /// <remarks>
    /// <b>Solo lee</b>: no escribe en la base ni registra descartadas; eso lo hace
    /// <see cref="AplicarMarcas"/> con lo que salga de aquí. Son dos pasos para que la
    /// pantalla pueda enseñar lo que casó y lo que no antes de tocar nada. Un archivo que no
    /// se puede abrir no lanza: vuelve sin marcas y con el problema en los avisos.
    /// </remarks>
    /// <param name="rutaExcel">La ruta del <c>.xlsx</c> que devolvió el compañero.</param>
    /// <param name="companeroId">De quién viene, para nombrarlo en los avisos.</param>
    /// <returns>Nunca nulo. Las marcas que se pudieron leer, cada una con la fila del Excel de la que salió; y los avisos. En el doble, siempre vacío y avisado.</returns>
    ResultadoDelExcelDevuelto LeerExcelDevuelto(string rutaExcel, long companeroId);

    /// <summary>Aplica a la base las marcas leidas; escribe estado, nunca firma campos.</summary>
    /// <remarks>
    /// <para><b>Escribe</b>, por cada marca que casa con UNA sola persona, la propuesta del
    /// compañero en esa persona (<see cref="IPersonas.AnotarPropuesta"/>), y una vez por
    /// caso el estado con el nombre del compañero y su motivo
    /// (<see cref="ICasos.MarcarEstadoDelCompanero"/>). La que no casa con nadie, o casa con
    /// más de una, va a <see cref="IIlegibles.RegistrarDescartada"/> con su motivo; no se
    /// casa por nombre. <b>Nunca toca</b> <c>procedencia_campo.verificado</c>.</para>
    /// <para>No es idempotente en las marcas de tiempo: aplicar dos veces la misma hoja deja
    /// el mismo estado con <c>_en</c> nuevo. No se escribe nada si el compañero no existe.</para>
    /// <para>⚠️ El doble se queda corto a propósito y hay que saberlo al probar contra él:
    /// casa solo por MRN y con la primera persona que lo tenga, no escribe el motivo del
    /// compañero, y contesta «escrito» aunque no haya aplicado ninguna fila.</para>
    /// </remarks>
    /// <param name="marcas">Lo que devolvió <see cref="LeerExcelDevuelto"/>.</param>
    /// <param name="companeroId">Quién devolvió la hoja; queda como <c>estado_del_companero_por</c> y <c>propuesto_por</c>.</param>
    /// <param name="rutaExcel">La ruta de la hoja; queda como origen de la marca y en cada descartada.</param>
    /// <returns>En la base real, escrito si se aplicó al menos una fila o se marcó un caso, con un aviso que dice cuántas se aplicaron, cuántas se descartaron y cuántos documentos se marcaron; el id es el del compañero.</returns>
    ResultadoDeEscritura AplicarMarcas(IReadOnlyList<MarcaDelCompanero> marcas, long companeroId, string rutaExcel);
}
