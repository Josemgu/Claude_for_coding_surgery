using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Contratos.Puertos;

/// <summary>Todo lo que se puede hacer con los companeros. Se desactivan, no se borran.</summary>
public interface ICompaneros
{
    /// <summary>Devuelve un trozo de la lista de companeros que cumplen el filtro, con el total detras.</summary>
    PaginaDe<Companero> Listar(FiltroDeCompaneros filtro, Pagina trozo);

    /// <summary>Devuelve todos los activos; son pocos y toda pantalla de asignar los necesita enteros.</summary>
    IReadOnlyList<Companero> Activos();

    /// <summary>Devuelve un companero por su id, o nulo si no esta.</summary>
    Companero? Obtener(long id);

    /// <summary>Da de alta un companero o cambia su nombre.</summary>
    ResultadoDeEscritura Guardar(Companero companero);

    /// <summary>Desactiva un companero dejando su fecha; nunca lo borra.</summary>
    ResultadoDeEscritura Desactivar(long companeroId, string desactivadoEn);
}
