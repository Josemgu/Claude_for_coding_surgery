using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Contratos.Puertos;

/// <summary>
/// Los renglones de lo que no se pudo leer, y las filas del Excel que no entraron.
/// </summary>
/// <remarks>
/// Las dos listas viven juntas por el mismo motivo: son trabajo que se perdio y que
/// alguien tiene que poder mirar despues. Un cuadro que se cierra con Aceptar no vale.
/// <para>
/// <b>Quién lo implementa:</b> <c>Fichas.Datos.Repositorios.RepositorioDeIlegibles</c>
/// (en dos archivos: el segundo, <c>.Borrado</c>, lleva los dos métodos de borrar) y
/// <c>Fichas.Datos.Falso.RepositorioDeIlegiblesFalso</c>, que nunca borra porque no hay base
/// que copiar. <b>Quién lo consume:</b> Importar (<c>GuardadoDeHojas</c> registra,
/// <c>LoQueEntroSinInformacion</c> lista, <c>OperacionDeBorrarLosPdfIlegibles</c> borra),
/// la vuelta de Paquetes (lista las descartadas) y <c>Fichas.Paquetes</c> (las registra).
/// </para>
/// </remarks>
public interface IIlegibles
{
    /// <summary>Devuelve un trozo de la lista de documentos ilegibles, con el total detras.</summary>
    /// <remarks>Orden: lo más reciente primero. El filtro por ruta busca por contenido; el de motivo, por código exacto.</remarks>
    /// <param name="filtro">Qué renglones; <see cref="FiltroDeIlegibles.Todo"/> para todos.</param>
    /// <param name="trozo">Qué parte; más allá del final vuelve vacío.</param>
    /// <returns>Nunca nulo: sin nada, un trozo vacío con total 0.</returns>
    PaginaDe<RenglonIlegible> Listar(FiltroDeIlegibles filtro, Pagina trozo);

    /// <summary>Cuenta cuantos renglones ilegibles cumplen el filtro, sin traerlos.</summary>
    /// <remarks>Hoy no lo llama ninguna pantalla (grep del 2026-09-11); se documenta y no se toca.</remarks>
    /// <param name="filtro">El mismo que en <see cref="Listar"/>.</param>
    /// <returns>0 si ninguno.</returns>
    int Contar(FiltroDeIlegibles filtro);

    /// <summary>Anota que un PDF o una pagina no se pudo leer, con su codigo de motivo.</summary>
    /// <remarks>
    /// <b>Escribe</b> siempre una fila nueva: la tabla no tiene unicidad a propósito, porque el
    /// mismo archivo deja un renglón por hoja o por reintento, y registrar dos veces son dos
    /// renglones. La fecha vacía se rellena con el reloj. La base real ignora el
    /// <see cref="RenglonIlegible.Id"/> que venga, guarda un motivo vacío como
    /// <c>sin_motivo</c> y con la ruta vacía no escribe y lo dice; ⚠️ el doble respeta un id
    /// distinto de 0 y admite la ruta vacía (medido el 2026-09-11, apuntado en la entrega).
    /// </remarks>
    /// <param name="renglon">El renglón, con la ruta del PDF, que es lo único que lo hace útil.</param>
    /// <returns>El id del renglón nuevo, o no escrito con su motivo.</returns>
    ResultadoDeEscritura Registrar(RenglonIlegible renglon);

    /// <summary>Devuelve un trozo de las filas del Excel que no entraron, con el total detras.</summary>
    /// <param name="companeroId">Solo las de ese compañero; nulo para las de todos.</param>
    /// <param name="trozo">Qué parte; más allá del final vuelve vacío.</param>
    /// <returns>Nunca nulo; lo más reciente primero.</returns>
    PaginaDe<FilaDescartada> ListarDescartadas(long? companeroId, Pagina trozo);

    /// <summary>Anota una fila del Excel que no caso con nadie, tal como venia escrita.</summary>
    /// <remarks>
    /// <b>Escribe</b> siempre una fila nueva y <b>no valida el contenido</b>: ni el número de
    /// caso ni el MRN, porque lo que venía escrito puede ser justo lo que estaba mal. Lo único
    /// que tiene que existir es el compañero, que es clave foránea y no una regla de contenido.
    /// Un motivo vacío se guarda como «No se dijo por que no entro».
    /// </remarks>
    /// <param name="fila">La fila tal como volvió, con el número de fila del Excel para poder ir a mirarla.</param>
    /// <returns>El id de la fila nueva, o no escrito si el compañero no existe.</returns>
    ResultadoDeEscritura RegistrarDescartada(FilaDescartada fila);

    /// <summary>La tabla de estos renglones; es la marca que lleva un plan de borrado suyo.</summary>
    /// <remarks>
    /// Existe para que <see cref="IMantenimiento.Borrar"/> y
    /// <see cref="BorrarRenglonesSinCaso"/> puedan rechazar el plan del otro. Los dos
    /// devuelven el mismo <see cref="PlanDeBorrado"/>, y un plan de renglones ejecutado por
    /// el de documentos borraria CASOS con los ids de unos renglones: documentos que no
    /// tienen nada que ver, y que el dueno no vio en ninguna pregunta.
    /// </remarks>
    const string TablaDeLosRenglones = "documentos_ilegibles";

    /// <summary>
    /// Prepara el borrado de los renglones ilegibles marcados que NO tienen documento:
    /// copia la base y cuenta.
    /// </summary>
    /// <remarks>
    /// <para>Por que existe, del dueno el 2026-09-07 y dicho dos veces el mismo dia:
    /// <i>«no se contempla eliminar PDF o documentos que no tienen informacion; debe poder
    /// eliminarlo»</i>. Un PDF que no se pudo leer en absoluto deja un renglon con
    /// <c>caso_id</c> NULO, no tiene documento al que agarrarse, y hasta hoy ningun borrado
    /// del programa lo alcanzaba: quedaba registrado para siempre.</para>
    ///
    /// <para>⛔ <b>Solo los que NO tienen documento.</b> El renglon CON <c>caso_id</c> es
    /// informacion sobre un documento que existe —el motivo por el que esta a medias— y se
    /// va con el cuando el documento se borre, porque <c>documentos_ilegibles</c> es uno de
    /// los pasos de <see cref="IMantenimiento.PlanearDocumentos"/>. Un id con documento que
    /// llegue aqui no entra en el plan y se dice por que.</para>
    ///
    /// <para><b>Son DOS metodos y no uno</b> por lo mismo que en
    /// <see cref="IMantenimiento"/>: el orden es copia → pregunta → borrado, la copia se
    /// hace AQUI —antes de que nadie conteste, para que la pregunta pueda decir donde
    /// quedo— y la pregunta ocurre en la ventana, que un puerto no conoce. Un solo metodo
    /// tendria que borrar sin preguntar.</para>
    ///
    /// <para>⚠️ <b>Del plan que vuelve NO sirven <see cref="PlanDeBorrado.Titulo"/> ni
    /// <see cref="PlanDeBorrado.TextoDelBoton"/>:</b> los dos cuentan la tabla
    /// <c>casos</c>, que aqui no entra, y dirian «Borrar 0 documentos». Lo que si sirve, y
    /// es lo que hace falta para preguntar, son <see cref="PlanDeBorrado.Conteos"/>,
    /// <see cref="PlanDeBorrado.RutaDeLaCopia"/>, <see cref="PlanDeBorrado.Pregunta"/> y
    /// <see cref="PlanDeBorrado.SePuedeBorrar"/>. El titulo y el boton los pone la pantalla.
    /// El motivo de no anadir un valor a <see cref="AlcanceDelBorrado"/> es que vive en
    /// <c>IMantenimiento.cs</c>, y de <c>Fichas.Contratos</c> solo esta descongelado este
    /// archivo (<c>.claude/congelados.txt</c>).</para>
    ///
    /// <para>⚠️ <b>Esto NO borra el PDF del disco</b>, que es del dueno y esta en su
    /// carpeta: borra el renglon que dice que no se pudo leer. Quien llame tiene que
    /// decirlo en pantalla, o el dueno creera que borro un archivo que sigue ahi.</para>
    /// </remarks>
    /// <param name="renglonIds">Los numeros internos de los renglones marcados.</param>
    /// <returns>
    /// Un plan con alcance <see cref="AlcanceDelBorrado.Documentos"/> y un único conteo, el de
    /// <see cref="TablaDeLosRenglones"/>. Sin permiso de borrar cuando no hay ninguno
    /// marcado, cuando todos tienen documento, o cuando la copia previa no se pudo hacer; en
    /// el doble, siempre sin permiso. No escribe en la base: solo copia el archivo y cuenta.
    /// </returns>
    PlanDeBorrado PlanearBorradoDeRenglonesSinCaso(IReadOnlyCollection<long> renglonIds);

    /// <summary>
    /// Ejecuta un plan de renglones que el dueno ya contesto que si. Todo o nada.
    /// </summary>
    /// <remarks>
    /// Igual que <see cref="IMantenimiento.Borrar"/>: quien llama tiene que haber
    /// preguntado antes —un puerto no puede saber si se pregunto—, un plan sin permiso o
    /// sin copia previa no se ejecuta, y se vuelve a comprobar que los renglones siguen sin
    /// documento, porque entre la pregunta y el «si» pudo cambiar algo.
    /// </remarks>
    /// <param name="plan">El que devolvió <see cref="PlanearBorradoDeRenglonesSinCaso"/>, con <see cref="PlanDeBorrado.SePuedeBorrar"/> y su copia; uno de <see cref="IMantenimiento"/> se rechaza.</param>
    /// <returns>
    /// Borrado con cuántos renglones cayeron, o no borrado con su motivo: plan de otro sitio,
    /// sin permiso, sin copia, renglones que ya tienen documento, o el motor que lo rechazó
    /// (y entonces nada cambió: va en una transacción). En el doble, nunca se borra.
    /// </returns>
    ResultadoDeBorrado BorrarRenglonesSinCaso(PlanDeBorrado plan);
}
