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
/// <para>
/// <b>Quién lo implementa:</b> <c>Fichas.Datos.Repositorios.RepositorioDeProcedencia</c>
/// sobre SQLite y <c>Fichas.Datos.Falso.RepositorioDeProcedenciaFalso</c> en memoria (con
/// un índice por registro, porque sin él la suite tardaba 24 s). <b>Quién lo consume:</b>
/// Corrección (anota al guardar, firma con el botón, cuenta las firmas), Importar (anota
/// lo extraído), Grupo (<c>ProcedenciasDeUnaPasada</c>, el veredicto de «listo para
/// asignar» en bloque), la firma en bloque de Paquetes y <c>Fichas.Reportes</c>. La clave
/// de una fila es (tabla, registro, campo): un campo tiene una fila o ninguna.
/// </para>
/// </remarks>
public interface IProcedencia
{
    /// <summary>Devuelve la procedencia de todos los campos de una fila.</summary>
    /// <param name="tabla">Si el registro es un caso o una persona.</param>
    /// <param name="registroId">El id en esa tabla; uno sin filas o que no existe da la lista vacía.</param>
    /// <returns>Una fila por campo anotado, por nombre de campo; nunca nulo. Un campo con valor y sin fila no sale: «no consta de dónde salió» se distingue con <see cref="CamposAnotadosDe"/>.</returns>
    IReadOnlyList<ProcedenciaDeCampo> DeRegistro(TablaDeProcedencia tabla, long registroId);

    /// <summary>Devuelve un trozo de los campos por debajo de una confianza, que son los que hay que mirar.</summary>
    /// <remarks>
    /// Pregunta <c>confianza IS NOT NULL AND confianza &lt; umbral</c>: deja fuera la
    /// confianza nula, el tachón, el ausente y la firma, que sí entran en
    /// <see cref="LasQuePesanEnElVeredicto"/>. Hoy no lo llama ninguna pantalla (grep del
    /// 2026-09-11); se documenta y no se toca.
    /// </remarks>
    /// <param name="umbral">Por debajo de esta confianza la fila sale; 0,6 es el que usa el programa.</param>
    /// <param name="trozo">Qué parte; más allá del final vuelve vacío.</param>
    /// <returns>De menor a mayor confianza; nunca nulo.</returns>
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
    /// <returns>Sin paginar, por tabla, registro y campo; vacía si ninguna pesa. Nunca nulo.</returns>
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
    /// <returns>Por id de registro, los nombres de campo que tienen fila, ordenados; nunca nulo, y sin entrada para el registro que no tiene ninguna.</returns>
    IReadOnlyDictionary<long, IReadOnlyList<string>> CamposAnotadosDe(TablaDeProcedencia tabla);

    /// <summary>Cuenta cuantos campos de una fila estan firmados; al recien extraer es 0.</summary>
    /// <param name="tabla">Si el registro es un caso o una persona.</param>
    /// <param name="registroId">El id en esa tabla; uno que no existe da 0.</param>
    /// <returns>Cuántas filas de ese registro tienen <see cref="ProcedenciaDeCampo.Verificado"/>.</returns>
    int ContarVerificados(TablaDeProcedencia tabla, long registroId);

    /// <summary>Anota la procedencia de un campo. Nunca pone verificado: eso es <see cref="Firmar"/>.</summary>
    /// <remarks>
    /// <para><b>Escribe</b> la fila de ese (tabla, registro, campo), pisando la que hubiera:
    /// la extracción vieja ya no vale. Si la procedencia que llega trae
    /// <see cref="ProcedenciaDeCampo.Verificado"/>, se anota igual pero SIN la firma; la
    /// base real lo dice con una advertencia, el doble lo hace en silencio.</para>
    /// <para>⚠️ <b>Sobre la firma que ya había, las dos implementaciones discrepan</b>
    /// (medido el 2026-09-11): la base real la retira —es un <c>INSERT OR REPLACE</c> con
    /// <c>verificado = 0</c>, y su comentario lo quiere así porque el valor de debajo
    /// cambió—; el doble la conserva, y una prueba suya
    /// (<c>VolverAAnotarNoBorraLaFirma</c>) vigila que la conserve. El contrato no lo fija;
    /// está apuntado en la entrega y no se toca aquí.</para>
    /// </remarks>
    /// <param name="procedencia">La fila; su <see cref="ProcedenciaDeCampo.Id"/> se ignora en la base real, y la firma que traiga también.</param>
    /// <returns>El id de la fila escrita; en la base real, siempre uno nuevo.</returns>
    ResultadoDeEscritura Anotar(ProcedenciaDeCampo procedencia);

    /// <summary>Firma un campo como bueno con quien y cuando; el unico camino a verificado.</summary>
    /// <remarks>
    /// <b>Escribe</b> <c>verificado = 1</c>, <c>verificado_por</c> y <c>verificado_en</c>.
    /// No se escribe, y se dice, si el compañero no existe: una firma sin nombre no dice
    /// quién firmó. Solo se comprueba que exista, no que esté activo. Firmar dos veces deja
    /// la última firma, sin aviso de «ya estaba». ⚠️ Con la fecha vacía la base real no
    /// escribe y lo dice; el doble pone la del reloj (medido el 2026-09-11, apuntado en la
    /// entrega). Y si el campo no tiene fila de procedencia, la base real no puede firmarlo
    /// (es un <c>UPDATE</c> que cambia cero filas) mientras el doble crea la fila con origen
    /// manual: es uno de los seis casos que <see cref="CamposAnotadosDe"/> existe para contar.
    /// </remarks>
    /// <param name="tabla">Si el registro es un caso o una persona.</param>
    /// <param name="registroId">El id en esa tabla.</param>
    /// <param name="campo">El nombre de la columna, como en la base (<c>fecha_viaje</c>).</param>
    /// <param name="companeroId">Quién firma; en el programa, siempre Miguel con el botón.</param>
    /// <param name="verificadoEn">Cuándo, ISO-8601.</param>
    /// <returns>El id de la fila firmada, o no escrito con su motivo.</returns>
    ResultadoDeEscritura Firmar(TablaDeProcedencia tabla, long registroId, string campo, long companeroId, string verificadoEn);

    /// <summary>Retira la firma de un campo. Se llama cuando su valor cambia.</summary>
    /// <remarks>
    /// <para>⚠️ <b>Esto no es higiene: es el fallo mas grave que ha tenido este proyecto.</b>
    /// En el Python la firma sobrevivia a un cambio de valor, asi que un campo podia
    /// decir «Todo correcto, firmado por Miguel» sobre un dato que Miguel nunca vio.
    /// Se arreglo el 2026-09-03 y aqui estaba a punto de repetirse: el terreno de
    /// Correccion lo encontro y lo devolvio porque no habia por donde retirarla.
    /// Lo anade el supervisor, titular de lo congelado, el 2026-09-04.</para>
    /// <para>Devuelve un aviso, nunca levanta: retirar una firma que no existe no es un
    /// error, es que no habia nada que retirar. <b>Escribe</b> <c>verificado = 0</c> y deja a
    /// nulo quién y cuándo; es idempotente. Hoy no lo llama ninguna pantalla (grep del
    /// 2026-09-11): la corrección, al guardar, llama a <see cref="Anotar"/> y a
    /// <see cref="Firmar"/>, no a esto. Se documenta y no se toca.</para>
    /// </remarks>
    /// <param name="tabla">Si el registro es un caso o una persona.</param>
    /// <param name="registroId">El id en esa tabla.</param>
    /// <param name="campo">El nombre de la columna, como en la base.</param>
    /// <returns>Escrito siempre: con la firma retirada, o con un aviso informativo de que no había ninguna.</returns>
    ResultadoDeEscritura RetirarLaFirma(TablaDeProcedencia tabla, long registroId, string campo);
}
