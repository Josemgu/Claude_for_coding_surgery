using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Contratos.Puertos;

/// <summary>
/// Los renglones de lo que no se pudo leer, y las filas del Excel que no entraron.
/// </summary>
/// <remarks>
/// Las dos listas viven juntas por el mismo motivo: son trabajo que se perdio y que
/// alguien tiene que poder mirar despues. Un cuadro que se cierra con Aceptar no vale.
/// </remarks>
public interface IIlegibles
{
    /// <summary>Devuelve un trozo de la lista de documentos ilegibles, con el total detras.</summary>
    PaginaDe<RenglonIlegible> Listar(FiltroDeIlegibles filtro, Pagina trozo);

    /// <summary>Cuenta cuantos renglones ilegibles cumplen el filtro, sin traerlos.</summary>
    int Contar(FiltroDeIlegibles filtro);

    /// <summary>Anota que un PDF o una pagina no se pudo leer, con su codigo de motivo.</summary>
    ResultadoDeEscritura Registrar(RenglonIlegible renglon);

    /// <summary>Devuelve un trozo de las filas del Excel que no entraron, con el total detras.</summary>
    PaginaDe<FilaDescartada> ListarDescartadas(long? companeroId, Pagina trozo);

    /// <summary>Anota una fila del Excel que no caso con nadie, tal como venia escrita.</summary>
    ResultadoDeEscritura RegistrarDescartada(FilaDescartada fila);
}
