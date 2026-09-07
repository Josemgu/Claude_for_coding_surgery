using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Datos.Falso;

/// <summary>Los renglones ilegibles inventados. Cumple <see cref="IIlegibles"/> sin tocar ningun archivo.</summary>
public sealed class RepositorioDeIlegiblesFalso : IIlegibles
{
    private readonly AlmacenFalso _almacen;

    /// <summary>Se ata al almacen que comparten los seis repositorios falsos.</summary>
    public RepositorioDeIlegiblesFalso(AlmacenFalso almacen) => _almacen = almacen;

    /// <summary>Devuelve un trozo de la lista de documentos ilegibles, con el total detras.</summary>
    public PaginaDe<RenglonIlegible> Listar(FiltroDeIlegibles filtro, Pagina trozo) => Trozos.Cortar(Filtrar(filtro), trozo);

    /// <summary>Cuenta cuantos renglones ilegibles cumplen el filtro, sin traerlos.</summary>
    public int Contar(FiltroDeIlegibles filtro) => Filtrar(filtro).Count;

    /// <summary>Anota que un PDF o una pagina no se pudo leer, con su codigo de motivo.</summary>
    public ResultadoDeEscritura Registrar(RenglonIlegible renglon)
    {
        // Esta tabla NO tiene unicidad a proposito: el mismo archivo puede dejar varios
        // renglones (uno por pagina, o uno por reintento). Ver ARQUITECTURA §2.8.
        var id = renglon.Id == 0 ? _almacen.SiguienteId() : renglon.Id;
        var cuando = string.IsNullOrWhiteSpace(renglon.RegistradoEn) ? _almacen.Reloj.Ahora() : renglon.RegistradoEn;
        _almacen.Ilegibles[id] = renglon with { Id = id, RegistradoEn = cuando };
        return ResultadoDeEscritura.Bien(id);
    }

    /// <summary>Devuelve un trozo de las filas del Excel que no entraron, con el total detras.</summary>
    public PaginaDe<FilaDescartada> ListarDescartadas(long? companeroId, Pagina trozo)
    {
        var filas = _almacen.Descartadas.Values
            .Where(f => companeroId is null || f.CompaneroId == companeroId)
            .OrderByDescending(f => f.RegistradoEn, StringComparer.Ordinal)
            .ThenBy(f => f.Id)
            .ToList();
        return Trozos.Cortar(filas, trozo);
    }

    /// <summary>Anota una fila del Excel que no caso con nadie, tal como venia escrita.</summary>
    public ResultadoDeEscritura RegistrarDescartada(FilaDescartada fila)
    {
        // Del CONTENIDO no se valida nada: esta tabla existe para guardar lo que NO entro,
        // y validarlo haria que rechazara justo aquello para lo que existe
        // (ARQUITECTURA §2.9). Pero el companero al que apunta si tiene que existir: es una
        // clave foranea con RESTRICT en el esquema, no una regla de contenido, y el de
        // verdad no puede guardarla. Hasta el 2026-09-05 aqui entraba igual.
        if (!_almacen.Companeros.ContainsKey(fila.CompaneroId))
        {
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                $"No hay ningun companero con el numero interno {fila.CompaneroId}.",
                nameof(FilaDescartada.CompaneroId),
                "Una fila descartada dice de QUIEN era la hoja que la trajo, y ese " +
                "companero tiene que estar en la lista."));
        }

        var id = fila.Id == 0 ? _almacen.SiguienteId() : fila.Id;
        var cuando = string.IsNullOrWhiteSpace(fila.RegistradoEn) ? _almacen.Reloj.Ahora() : fila.RegistradoEn;
        _almacen.Descartadas[id] = fila with { Id = id, RegistradoEn = cuando };
        return ResultadoDeEscritura.Bien(id);
    }

    /// <summary>Aplica los filtros simples y devuelve la lista de lo mas reciente a lo mas viejo.</summary>
    private List<RenglonIlegible> Filtrar(FiltroDeIlegibles filtro)
    {
        IEnumerable<RenglonIlegible> renglones = _almacen.Ilegibles.Values;
        if (!string.IsNullOrWhiteSpace(filtro.RutaPdf)) renglones = renglones.Where(r => Trozos.Contiene(r.RutaPdf, filtro.RutaPdf));
        if (!string.IsNullOrWhiteSpace(filtro.Motivo)) renglones = renglones.Where(r => r.Motivo == filtro.Motivo);
        return renglones
            .OrderByDescending(r => r.RegistradoEn, StringComparer.Ordinal)
            .ThenBy(r => r.Id)
            .ToList();
    }
}
