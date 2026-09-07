using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Contratos.Puertos;

/// <summary>
/// De donde salio cada valor y quien lo dio por bueno.
/// </summary>
/// <remarks>
/// Regla permanente 5: <see cref="Firmar"/> es el UNICO camino a <c>verificado = 1</c>,
/// exige quien y cuando, y no lo llama ninguna importacion ni ningun Excel: solo el boton
/// que pulsa Miguel. Cargar un Excel escribe estado, no firma.
/// </remarks>
public interface IProcedencia
{
    /// <summary>Devuelve la procedencia de todos los campos de una fila.</summary>
    IReadOnlyList<ProcedenciaDeCampo> DeRegistro(TablaDeProcedencia tabla, long registroId);

    /// <summary>Devuelve un trozo de los campos por debajo de una confianza, que son los que hay que mirar.</summary>
    PaginaDe<ProcedenciaDeCampo> PorDebajoDeConfianza(double umbral, Pagina trozo);

    /// <summary>
    /// Todas las filas que pueden cambiar el veredicto de «listo para asignar», de una vez.
    /// </summary>
    /// <remarks>
    /// <para><b>Que devuelve, y por que ese filtro y no otro.</b> Las filas con
    /// <c>verificado</c>, con <c>ausente_en_el_papel</c>, con <c>anulado_por_tachon</c>, con
    /// la confianza NULA o con la confianza por debajo de <paramref name="umbral"/>. Son
    /// exactamente las cinco condiciones que <c>EstadosDeCampo.EsDudoso</c> mira en una fila:
    /// cualquier otra fila da la misma respuesta —no es dudosa mientras su valor esté puesto
    /// y cumpla su forma—, asi que traerla no anadiria informacion y si peso.</para>
    ///
    /// <para>⚠️ <b>Esto NO sustituye a <see cref="PorDebajoDeConfianza"/> y hay que decir por
    /// que.</b> Aquella pregunta <c>confianza IS NOT NULL AND confianza &lt; umbral</c>, y con
    /// eso deja fuera cuatro de las seis divergencias medidas el 2026-09-06: la confianza
    /// nula, el tachon, la marca de «no está en el papel» y la firma. Sirve para la lista de
    /// «lo que hay que mirar primero», que es otra pregunta.</para>
    ///
    /// <para><b>Por que en bloque.</b> Medido por dos programadores por separado sobre las
    /// 18 000 lecturas reales: preguntar con <see cref="DeRegistro"/> documento a documento
    /// cuesta <b>4,4 s</b>, que son 22 veces el presupuesto de la pantalla de Inicio; en
    /// bloque, <b>50 ms</b>. La pantalla no puede pedir el veredicto de 3 000 documentos de
    /// uno en uno.</para>
    ///
    /// <para>⛔ <b>Una fila que no sale aqui NO es una fila que no exista.</b> Para
    /// distinguir «se leyó bien» de «no consta de dónde salió» hace falta ademas
    /// <see cref="CamposAnotadosDe"/>: un campo con valor y SIN fila es uno de los seis casos
    /// y hay que contarlo como pendiente.</para>
    /// </remarks>
    /// <param name="umbral">Por debajo de esta confianza la fila pesa; hoy 0,6.</param>
    IReadOnlyList<ProcedenciaDeCampo> LasQuePesanEnElVeredicto(double umbral);

    /// <summary>
    /// Que campos de esa tabla tienen fila de procedencia, agrupados por registro.
    /// </summary>
    /// <remarks>
    /// Devuelve <b>cuales</b> y no <b>cuantos</b> a proposito: la pantalla tiene que poder
    /// NOMBRAR el campo del que no consta de donde salio —«Cédula de Ana Prueba»—, y una
    /// cuenta no se puede convertir en un nombre.
    /// <para>
    /// Un registro sin ninguna fila simplemente no esta en el diccionario; no se devuelve
    /// una lista vacia por cada uno de los 3 000 documentos.
    /// </para>
    /// </remarks>
    /// <param name="tabla">Si se preguntan los campos de <c>casos</c> o los de <c>personas</c>.</param>
    IReadOnlyDictionary<long, IReadOnlyList<string>> CamposAnotadosDe(TablaDeProcedencia tabla);

    /// <summary>Cuenta cuantos campos de una fila estan firmados; al recien extraer es 0.</summary>
    int ContarVerificados(TablaDeProcedencia tabla, long registroId);

    /// <summary>Anota la procedencia de un campo. Nunca pone verificado: eso es <see cref="Firmar"/>.</summary>
    ResultadoDeEscritura Anotar(ProcedenciaDeCampo procedencia);

    /// <summary>Firma un campo como bueno con quien y cuando; el unico camino a verificado.</summary>
    ResultadoDeEscritura Firmar(TablaDeProcedencia tabla, long registroId, string campo, long companeroId, string verificadoEn);

    /// <summary>
    /// Retira la firma de un campo. Se llama cuando su valor cambia.
    ///
    /// ⚠️ **Esto no es higiene: es el fallo mas grave que ha tenido este proyecto.**
    /// En el Python la firma sobrevivia a un cambio de valor, asi que un campo podia
    /// decir «Todo correcto, firmado por Miguel» sobre un dato que Miguel nunca vio.
    /// Se arreglo el 2026-09-03 y aqui estaba a punto de repetirse: el terreno de
    /// Correccion lo encontro y lo devolvio porque no habia por donde retirarla.
    /// Lo anade el supervisor, titular de lo congelado, el 2026-09-04.
    ///
    /// Devuelve un aviso, nunca levanta: retirar una firma que no existe no es un
    /// error, es que no habia nada que retirar.
    /// </summary>
    ResultadoDeEscritura RetirarLaFirma(TablaDeProcedencia tabla, long registroId, string campo);
}
