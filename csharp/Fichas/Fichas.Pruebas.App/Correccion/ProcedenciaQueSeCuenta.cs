using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Un <see cref="IProcedencia"/> que solo hace una cosa de mas: contar cuantas veces le
/// preguntan por cada camino.
/// </summary>
/// <remarks>
/// <para><b>Por que un contador y no un cronometro.</b> Lo que hay que fijar de
/// <c>LoQueLeFaltaACadaDocumento</c> es que pregunta EN BLOQUE y no una vez por documento: es
/// la diferencia entre 50 ms y 4,4 s con las 18 000 lecturas reales, medida por otros dos
/// programadores. Un cronometro contesta eso de forma indirecta y ademas <b>se cae solo con la
/// maquina cargada</b>: medido el 2026-09-07 sobre la base inventada de 3 000 documentos, la
/// misma pasada dio <b>238 ms</b> y <b>493 ms</b> en dos ejecuciones seguidas sin nada mas
/// corriendo. Una prueba que depende de eso no dice si el codigo esta bien; dice como estaba la
/// maquina. Contando llamadas, la respuesta es la misma en cualquier maquina.</para>
///
/// <para>⛔ No cambia ninguna respuesta: pasa todo tal cual al de debajo.</para>
/// </remarks>
internal sealed class ProcedenciaQueSeCuenta : IProcedencia
{
    /// <summary>A quien se le pasa todo tal cual despues de contar.</summary>
    private readonly IProcedencia _deVerdad;

    /// <summary>Envuelve al de verdad.</summary>
    /// <param name="deVerdad">El almacen que contesta; este solo cuenta.</param>
    public ProcedenciaQueSeCuenta(IProcedencia deVerdad) => _deVerdad = deVerdad;

    /// <summary>Cuantas veces se pregunto por UN registro; es la llamada cara si se repite.</summary>
    public int VecesQueSePreguntoPorUnRegistro { get; private set; }

    /// <summary>Cuantas veces se pidieron las filas que pesan, que es la consulta en bloque.</summary>
    public int VecesEnBloque { get; private set; }

    /// <summary>Cuantas veces se pidieron los campos anotados, la otra consulta en bloque.</summary>
    public int VecesLosCamposAnotados { get; private set; }

    /// <summary>Cuantas escrituras salieron de aqui; tiene que ser cero en una lectura.</summary>
    public int Escrituras { get; private set; }

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
        => _deVerdad.ContarVerificados(tabla, registroId);

    /// <inheritdoc />
    public ResultadoDeEscritura Anotar(ProcedenciaDeCampo procedencia)
    {
        Escrituras++;
        return _deVerdad.Anotar(procedencia);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura Firmar(
        TablaDeProcedencia tabla, long registroId, string campo, long companeroId, string verificadoEn)
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
