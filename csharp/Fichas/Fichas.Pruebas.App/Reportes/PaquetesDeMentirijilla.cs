using Fichas.Contratos.Consultas;
using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Pruebas.App.Reportes;

/// <summary>
/// Un <see cref="IPaquetes"/> de prueba con la ida y la vuelta puestas a mano.
/// </summary>
/// <remarks>
/// <para>Sirve para lo unico que es de la PANTALLA: contar bien lo que entro y lo que no, y
/// no perder ni un motivo. Lo que decide QUE fila casa con quien es de <c>Fichas.Paquetes</c>
/// y ya esta probado alli —«una clave ambigua NO se aplica a ninguna de las dos familias»,
/// <c>Fichas.Pruebas.Paquetes/PruebasDeLaVuelta.cs</c>—; repetirlo aqui con un doble seria
/// probar el doble.</para>
///
/// <para>Escribe las descartadas en el mismo <see cref="IIlegibles"/> que le den, igual que
/// hace la biblioteca de verdad: es de ahi de donde la pantalla saca su cuenta exacta.</para>
/// </remarks>
internal sealed class PaquetesDeMentirijilla : IPaquetes
{
    private readonly IIlegibles _ilegibles;
    private readonly IReloj _reloj;
    private readonly List<MarcaDelCompanero> _marcasQueCasan;
    private readonly List<FilaDescartada> _descartadasAlLeer;
    private readonly int _cuantasSeCaenAlAplicar;
    private readonly byte[]? _contenidoDelExcel;

    /// <summary>La ida: escribe esos bytes donde le pidan, o nada si va nulo.</summary>
    internal PaquetesDeMentirijilla(IIlegibles ilegibles, IReloj reloj, byte[]? contenidoDelExcel = null)
        : this(ilegibles, reloj, [], [], 0, contenidoDelExcel)
    {
    }

    /// <summary>La vuelta: esas marcas casan, esas descartadas no, y N de las que casan se caen al aplicar.</summary>
    internal PaquetesDeMentirijilla(
        IIlegibles ilegibles,
        IReloj reloj,
        List<MarcaDelCompanero> marcasQueCasan,
        List<FilaDescartada> descartadasAlLeer,
        int cuantasSeCaenAlAplicar = 0,
        byte[]? contenidoDelExcel = null)
    {
        _ilegibles = ilegibles;
        _reloj = reloj;
        _marcasQueCasan = marcasQueCasan;
        _descartadasAlLeer = descartadasAlLeer;
        _cuantasSeCaenAlAplicar = cuantasSeCaenAlAplicar;
        _contenidoDelExcel = contenidoDelExcel;
    }

    /// <summary>Los casos que le pidieron meter en el paquete, para poder comprobarlos.</summary>
    internal IReadOnlyList<long> UltimosCasos { get; private set; } = [];

    /// <summary>Las marcas que le llegaron a aplicar; ninguna descartada tiene que estar aqui.</summary>
    internal IReadOnlyList<MarcaDelCompanero> UltimasMarcasAplicadas { get; private set; } = [];

    /// <inheritdoc />
    public ResultadoDeEscritura GenerarExcelDeCompanero(long companeroId, IReadOnlyList<long> casoIds, string rutaDestino)
    {
        UltimosCasos = casoIds;
        if (_contenidoDelExcel is not null) File.WriteAllBytes(rutaDestino, _contenidoDelExcel);

        return ResultadoDeEscritura.BienCon(companeroId, Aviso.Informa(
            $"Paquete con {casoIds.Count} caso(s).", string.Empty, "Lo dice la prueba."));
    }

    /// <inheritdoc />
    public ResultadoDelExcelDevuelto LeerExcelDevuelto(string rutaExcel, long companeroId)
    {
        foreach (var descartada in _descartadasAlLeer)
            _ilegibles.RegistrarDescartada(descartada with { CompaneroId = companeroId, RutaExcel = rutaExcel });

        return new ResultadoDelExcelDevuelto(
            _marcasQueCasan,
            _descartadasAlLeer,
            [Aviso.Informa($"Leídas {_marcasQueCasan.Count + _descartadasAlLeer.Count} filas.", string.Empty, "Lo dice la prueba.")]);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura AplicarMarcas(IReadOnlyList<MarcaDelCompanero> marcas, long companeroId, string rutaExcel)
    {
        UltimasMarcasAplicadas = marcas;

        // Las ultimas N se caen al aplicar, que es lo que pasa de verdad cuando la base
        // cambio entre leer el Excel y aplicarlo.
        foreach (var marca in marcas.TakeLast(_cuantasSeCaenAlAplicar))
        {
            _ilegibles.RegistrarDescartada(new FilaDescartada
            {
                CompaneroId = companeroId,
                RutaExcel = rutaExcel,
                FilaExcel = marca.FilaExcel,
                NumeroCaso = marca.NumeroCaso,
                Mrn = marca.Mrn,
                Nombre = marca.Nombre,
                Motivo = $"Fila {marca.FilaExcel}: la persona ya no está en la base.",
                RegistradoEn = _reloj.Ahora(),
            });
        }

        var aplicadas = marcas.Count - _cuantasSeCaenAlAplicar;
        return new ResultadoDeEscritura(aplicadas > 0, companeroId,
            [Aviso.Informa($"Aplicadas {aplicadas} fila(s).", string.Empty, "Lo dice la prueba.")]);
    }
}
