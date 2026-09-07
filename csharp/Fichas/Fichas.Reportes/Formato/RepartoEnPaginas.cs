namespace Fichas.Reportes.Formato;

/// <summary>
/// Lleva la cuenta de por donde va la pagina y cual es el encabezado vigente.
/// </summary>
/// <remarks>
/// Va como clase y no como un metodo con cinco variables sueltas porque son justo eso: cinco
/// cosas que cambian a la vez y que hay que dejar coherentes en cada salto de pagina.
/// </remarks>
internal sealed class RepartoEnPaginas
{
    private const double ProporcionDelInterlineado = 1.45;

    private readonly List<List<(double Y, Linea Linea)>> _paginas = [];
    private List<(double Y, Linea Linea)> _pagina = [];
    private List<Linea> _encabezado = [];
    private double _y = Maqueta.TopeSuperior;
    private bool _laAnteriorEraEncabezado;

    /// <summary>Guarda las lineas que hay que repetir arriba de cada pagina nueva.</summary>
    /// <remarks>
    /// Un titulo de seccion BORRA el encabezado vigente: si la pagina se corta entre el
    /// titulo de la seccion nueva y su tabla, repetir arriba los titulos de columna de la
    /// tabla ANTERIOR seria peor que no repetir ninguno.
    /// </remarks>
    internal void AnotarElEncabezado(Linea linea)
    {
        if (linea.Repetir && !_laAnteriorEraEncabezado) _encabezado = [];
        if (linea.Repetir) _encabezado.Add(linea);
        else if (linea.Tamano == Maqueta.TamanoDeSeccion) _encabezado = [];
        _laAnteriorEraEncabezado = linea.Repetir;
    }

    /// <summary>Situa una linea, cambiando de pagina si ya no cabe.</summary>
    internal void Colocar(Linea linea)
    {
        if (_y - (linea.Tamano * ProporcionDelInterlineado) < Maqueta.TopeInferior && _pagina.Count > 0)
        {
            CambiarDePagina();
        }
        BajarYColocar(linea);
    }

    /// <summary>Las paginas repartidas. Siempre al menos una, aunque este vacia.</summary>
    internal IReadOnlyList<IReadOnlyList<(double Y, Linea Linea)>> Paginas()
    {
        if (_pagina.Count > 0) _paginas.Add(_pagina);
        return _paginas.Count > 0 ? _paginas : [[]];
    }

    /// <summary>Baja el cursor el alto de la linea y la coloca en esa base.</summary>
    private void BajarYColocar(Linea linea)
    {
        _y -= linea.Tamano * ProporcionDelInterlineado;
        _pagina.Add((_y, linea));
    }

    /// <summary>Cierra la pagina y abre otra con el encabezado vigente repetido.</summary>
    private void CambiarDePagina()
    {
        _paginas.Add(_pagina);
        _pagina = [];
        _y = Maqueta.TopeSuperior;
        foreach (var repetida in _encabezado) BajarYColocar(repetida);
    }
}
