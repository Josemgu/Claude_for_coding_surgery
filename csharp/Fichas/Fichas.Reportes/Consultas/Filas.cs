using Fichas.Contratos.Modelos;

namespace Fichas.Reportes.Consultas;

/// <summary>
/// Una persona con el caso al que pertenece, que es como se lee el reporte.
/// </summary>
/// <remarks>
/// En el Python esto era una fila de un <c>JOIN</c> (<c>reportes/consultas.py</c>,
/// <c>personas_del_periodo</c>): una sola consulta de la que salen las DOS mitades del
/// reporte —quien viajo y quien no pudo—, separadas despues por <c>pudo_viajar</c>. Es a
/// proposito: con dos consultas, un filtro escrito distinto en cada una podria dejar a
/// alguien fuera de las dos listas a la vez, y esa persona no apareceria en ninguna parte del
/// reporte sin que nadie lo notara. Aqui se conserva la misma propiedad juntando UNA vez.
/// </remarks>
/// <param name="Persona">La persona tal como esta en la base.</param>
/// <param name="Caso">El caso del que viene, con su fecha de viaje y su estado.</param>
public sealed record PersonaConSuCaso(Persona Persona, Caso Caso)
{
    /// <summary>El id del caso, que es por lo que se agrupa. NO por el numero de caso.</summary>
    /// <remarks>
    /// Desde la migracion 12 dos casos pueden llevar el mismo numero. Agrupar por numero
    /// fundiria en un renglon a dos familias distintas de la misma unidad y el mismo mes, y el
    /// informe diria «viajan 9» donde viajan 4 y 5 por separado.
    /// </remarks>
    public long CasoId => Caso.Id;

    /// <summary>La fecha de viaje del caso, que es la de todas sus personas.</summary>
    public string? FechaViaje => Caso.FechaViaje;
}

/// <summary>
/// Un caso con cuantos campos tiene, cuantos verificados, y cuando se termino de verificar.
/// </summary>
/// <remarks>
/// Portado de <c>reportes/consultas.py</c>, <c>casos_con_su_verificacion</c>.
/// <see cref="VerificadoEn"/> es la marca de tiempo del ULTIMO campo verificado del caso —los
/// suyos y los de sus personas—: el instante en que alguien termino de mirarlo. Es nula
/// mientras no haya ni un campo verificado.
///
/// Un caso con <see cref="Campos"/> en cero NO esta verificado, y no es lo mismo que uno con
/// todos sus campos verificados: cero campos sin verificar no significa todo verificado,
/// significa que nadie leyo nada, y contarlo como verificado inflaria la metrica de trabajo
/// del equipo con casos que nadie ha tocado.
/// </remarks>
/// <param name="Caso">El caso.</param>
/// <param name="Personas">Cuantas personas lleva.</param>
/// <param name="Campos">Cuantos campos de procedencia tiene, del caso y de sus personas.</param>
/// <param name="CamposVerificados">Cuantos de esos llevan la firma de Miguel.</param>
/// <param name="VerificadoEn">Cuando se firmo el ultimo; nula si no hay ninguno.</param>
public sealed record CasoConSuVerificacion(
    Caso Caso,
    int Personas,
    int Campos,
    int CamposVerificados,
    string? VerificadoEn);

/// <summary>Un renglon del historico: un caso archivado con lo que se lee de un vistazo.</summary>
/// <remarks>
/// Portado de <c>datos/archivo.py</c>, <c>casos_archivados</c>. Cada fila trae cuantas
/// personas lleva y cuantas de ellas quedaron anotadas como que no pudieron viajar, que es lo
/// que se lee de un vistazo en una lista de historico sin tener que entrar a cada caso.
/// </remarks>
/// <param name="Caso">El caso archivado.</param>
/// <param name="Personas">Cuantas personas lleva.</param>
/// <param name="PersonasQueNoViajaron">Cuantas de ellas constan como que no pudieron viajar.</param>
public sealed record CasoArchivado(Caso Caso, int Personas, int PersonasQueNoViajaron);
