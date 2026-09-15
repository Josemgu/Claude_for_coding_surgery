using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// Un <see cref="IProcedencia"/> que solo hace una cosa de más: contar cuántas veces le
/// preguntan por cada camino.
/// </summary>
/// <remarks>
/// <para><b>Por qué un contador y no un cronómetro.</b> Lo que hay que fijar de
/// <c>LecturaParaReportes</c> es que pregunta EN BLOQUE y no una vez por caso y por persona:
/// con 3 000 casos eran 10 531 llamadas a <c>DeRegistro</c> (medido el 2026-09-15 sobre
/// <c>master</c>), y contra SQLite cada una es una consulta. Un cronómetro contesta eso de
/// forma indirecta y además se cae solo con la máquina cargada; contando llamadas, la
/// respuesta es la misma en cualquier máquina.</para>
///
/// <para>Es una copia de <c>Fichas.Pruebas.App.Correccion.ProcedenciaQueSeCuenta</c> y no una
/// referencia: este proyecto no referencia a <c>Fichas.Pruebas.App</c>, que arrastra WinUI.</para>
///
/// <para>⛔ No cambia ninguna respuesta: pasa todo tal cual al de debajo.</para>
/// </remarks>
internal sealed class ProcedenciaQueSeCuenta : IProcedencia
{
    /// <summary>A quien se le pasa todo tal cual después de contar.</summary>
    private readonly IProcedencia _deVerdad;

    /// <summary>Envuelve al de verdad.</summary>
    /// <param name="deVerdad">El almacén que contesta; este solo cuenta.</param>
    public ProcedenciaQueSeCuenta(IProcedencia deVerdad) => _deVerdad = deVerdad;

    /// <summary>Cuántas veces se preguntó por UN registro; es la llamada cara si se repite.</summary>
    public int VecesQueSePreguntoPorUnRegistro { get; private set; }

    /// <summary>Cuántas veces se pidieron las filas que pesan, que es la consulta en bloque.</summary>
    public int VecesEnBloque { get; private set; }

    /// <summary>Cuántas veces se pidieron los campos anotados, la otra consulta en bloque.</summary>
    public int VecesLosCamposAnotados { get; private set; }

    /// <summary>Cuántas veces se contaron los verificados de un registro; también es una por registro.</summary>
    public int VecesQueSeContaronVerificados { get; private set; }

    /// <summary>Cuántas escrituras salieron de aquí; tiene que ser cero en una lectura.</summary>
    public int Escrituras { get; private set; }

    /// <summary>Todas las consultas de lectura que salieron, sumadas: es la cifra del criterio de R-7.</summary>
    public int ConsultasDeLectura
        => VecesQueSePreguntoPorUnRegistro + VecesEnBloque + VecesLosCamposAnotados + VecesQueSeContaronVerificados;

    /// <inheritdoc />
    public IReadOnlyList<ProcedenciaDeCampo> DeRegistro(TablaDeProcedencia tabla, long registroId)
    {
        VecesQueSePreguntoPorUnRegistro++;
        return _deVerdad.DeRegistro(tabla, registroId);
    }

    /// <inheritdoc />
    public PaginaDe<ProcedenciaDeCampo> PorDebajoDeConfianza(double umbral, Pagina trozo)
        => _deVerdad.PorDebajoDeConfianza(umbral, trozo);

    /// <inheritdoc />
    public IReadOnlyList<ProcedenciaDeCampo> LasQuePesanEnElVeredicto(double umbral)
    {
        VecesEnBloque++;
        return _deVerdad.LasQuePesanEnElVeredicto(umbral);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<long, IReadOnlyList<string>> CamposAnotadosDe(TablaDeProcedencia tabla)
    {
        VecesLosCamposAnotados++;
        return _deVerdad.CamposAnotadosDe(tabla);
    }

    /// <inheritdoc />
    public int ContarVerificados(TablaDeProcedencia tabla, long registroId)
    {
        VecesQueSeContaronVerificados++;
        return _deVerdad.ContarVerificados(tabla, registroId);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura Anotar(ProcedenciaDeCampo procedencia)
    {
        Escrituras++;
        return _deVerdad.Anotar(procedencia);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura Firmar(TablaDeProcedencia tabla, long registroId, string campo, long companeroId, string verificadoEn)
    {
        Escrituras++;
        return _deVerdad.Firmar(tabla, registroId, campo, companeroId, verificadoEn);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura RetirarLaFirma(TablaDeProcedencia tabla, long registroId, string campo)
    {
        Escrituras++;
        return _deVerdad.RetirarLaFirma(tabla, registroId, campo);
    }
}
