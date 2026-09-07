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
/// </remarks>
public interface IPaquetes
{
    /// <summary>Genera el Excel de ida de un companero con los casos que lleva y lo deja en la ruta que se diga.</summary>
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
    ResultadoDeEscritura GenerarPdfDeCompanero(long companeroId, IReadOnlyList<long> casoIds, string rutaDestino)
        => ResultadoDeEscritura.NoSeEscribio(Aviso.Advierte(
            "Este almacén no sabe juntar los PDF de los documentos: el paquete sale solo con el Excel.",
            string.Empty,
            "Pasa con «--falso», que inventa los datos y no toca ningún archivo del disco. "
            + "Con la base de verdad el PDF unido sí se escribe."));

    /// <summary>Lee el Excel devuelto y devuelve lo que caso, lo que no y lo que hay que decir.</summary>
    ResultadoDelExcelDevuelto LeerExcelDevuelto(string rutaExcel, long companeroId);

    /// <summary>Aplica a la base las marcas leidas; escribe estado, nunca firma campos.</summary>
    ResultadoDeEscritura AplicarMarcas(IReadOnlyList<MarcaDelCompanero> marcas, long companeroId, string rutaExcel);
}
