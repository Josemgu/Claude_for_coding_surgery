using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Un almacen de procedencia que se comporta como el de SQLite, NO como el falso.
/// </summary>
/// <remarks>
/// ⚠️ <b>Existe porque los dos no hacen lo mismo, y la diferencia tapaba un defecto.</b>
/// Medido el 2026-09-05 sobre una copia de la base del dueno con sus siete escaneos ya
/// importados, con el programa publicado y la ventana abierta:
/// <list type="bullet">
/// <item><c>RepositorioDeProcedenciaFalso.Firmar</c> <b>crea</b> la fila que le falta, asi
/// que firmar un campo sin procedencia sale bien en las pruebas.</item>
/// <item><c>RepositorioDeProcedencia.Firmar</c> es un <c>UPDATE</c>: sin fila cambia cero
/// filas y contesta «No se encontro la fila que se queria cambiar».</item>
/// </list>
/// En los siete documentos de verdad, <c>templo_nombre</c> NO tiene fila de procedencia en
/// ninguno de los nueve casos, y en el caso 3 es el UNICO campo de «Por comprobar»: o sea
/// que lo unico que el programa le pedia comprobar al dueno era justo lo que no se podia
/// firmar. Con el almacen falso esa prueba habria salido verde.
/// <para>
/// Copia la conducta de <c>Fichas.Datos.Repositorios.RepositorioDeProcedencia</c> en lo que
/// esta pantalla usa, y NADA mas. No se referencia esa clase a proposito: <c>Fichas.Datos</c>
/// es terreno de otro programador y esta pantalla solo conoce <c>Fichas.Contratos</c>.
/// </para>
/// </remarks>
internal sealed class ProcedenciaComoLaDeVerdad : IProcedencia
{
    /// <summary>Las filas por su clave (tabla, registro_id, campo), como la clave primaria de la tabla de verdad.</summary>
    private readonly Dictionary<string, ProcedenciaDeCampo> _filas = new(StringComparer.Ordinal);
    /// <summary>Los companeros que existen; firmar a nombre de otro se niega, como hace la clave foranea.</summary>
    private readonly HashSet<long> _companeros;
    /// <summary>El proximo id que se reparte al anotar, como el autoincremento de SQLite.</summary>
    private long _siguienteId = 1;

    /// <summary>Monta el almacen con los companeros que existen; sin ellos no se firma.</summary>
    /// <param name="companeros">Los ids de companero que se dan por existentes.</param>
    public ProcedenciaComoLaDeVerdad(params long[] companeros) => _companeros = [.. companeros];

    /// <inheritdoc />
    public IReadOnlyList<ProcedenciaDeCampo> DeRegistro(TablaDeProcedencia tabla, long registroId)
        => [.. _filas.Values
            .Where(fila => fila.Tabla == tabla && fila.RegistroId == registroId)
            .OrderBy(fila => fila.Campo, StringComparer.Ordinal)];

    /// <inheritdoc />
    public PaginaDe<ProcedenciaDeCampo> PorDebajoDeConfianza(double umbral, Pagina trozo)
        => PaginaDe<ProcedenciaDeCampo>.Vacia(trozo);

    /// <inheritdoc />
    /// <remarks>
    /// Las cinco condiciones, en el mismo orden que el <c>WHERE</c> del repositorio de
    /// verdad. Este doble existe justo para que una prueba no salga verde sobre una conducta
    /// que la base de SQLite no tiene, asi que copiarlas mal aqui seria peor que no tenerlo.
    /// </remarks>
    public IReadOnlyList<ProcedenciaDeCampo> LasQuePesanEnElVeredicto(double umbral)
        => [.. _filas.Values
            .Where(fila => fila.Verificado
                        || fila.AusenteEnElPapel
                        || fila.AnuladoPorTachon
                        || fila.Confianza is not double confianza
                        || confianza < umbral)
            .OrderBy(fila => fila.Tabla)
            .ThenBy(fila => fila.RegistroId)
            .ThenBy(fila => fila.Campo, StringComparer.Ordinal)];

    /// <inheritdoc />
    public IReadOnlyDictionary<long, IReadOnlyList<string>> CamposAnotadosDe(TablaDeProcedencia tabla)
        => _filas.Values
            .Where(fila => fila.Tabla == tabla)
            .GroupBy(fila => fila.RegistroId)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => (IReadOnlyList<string>)[.. grupo
                    .Select(fila => fila.Campo)
                    .OrderBy(campo => campo, StringComparer.Ordinal)]);

    /// <inheritdoc />
    public int ContarVerificados(TablaDeProcedencia tabla, long registroId)
        => _filas.Values.Count(fila => fila.Tabla == tabla && fila.RegistroId == registroId && fila.Verificado);

    /// <summary>
    /// <c>INSERT OR REPLACE</c>: pisa la fila entera y <b>devuelve el campo a sin firmar</b>.
    /// </summary>
    /// <remarks>
    /// Es lo que hace la instruccion de verdad, y es lo que tiene que pasar: cuando el valor
    /// de debajo cambia, la firma deja de valer.
    /// </remarks>
    /// <param name="procedencia">La fila entera; su id se ignora y se reparte uno nuevo.</param>
    /// <exception cref="ArgumentNullException">Si la fila es nula.</exception>
    public ResultadoDeEscritura Anotar(ProcedenciaDeCampo procedencia)
    {
        ArgumentNullException.ThrowIfNull(procedencia);

        var clave = Clave(procedencia.Tabla, procedencia.RegistroId, procedencia.Campo);
        _filas[clave] = procedencia with
        {
            Id = _siguienteId++,
            Verificado = false,
            VerificadoPor = null,
            VerificadoEn = null,
        };
        return ResultadoDeEscritura.Bien(_filas[clave].Id);
    }

    /// <summary>Un <c>UPDATE</c>: sin fila que cambiar, NO se firma y se dice.</summary>
    /// <param name="tabla">Si es del caso o de una persona.</param>
    /// <param name="registroId">El id del caso o de la persona.</param>
    /// <param name="campo">El nombre de la columna.</param>
    /// <param name="companeroId">Quien firma; tiene que existir.</param>
    /// <param name="verificadoEn">Cuando; en blanco se niega, como el <c>NOT NULL</c> de la tabla.</param>
    public ResultadoDeEscritura Firmar(
        TablaDeProcedencia tabla, long registroId, string campo, long companeroId, string verificadoEn)
    {
        if (string.IsNullOrWhiteSpace(verificadoEn))
        {
            return ResultadoDeEscritura.NoSeEscribio(
                Aviso.Problema("Para firmar un campo hace falta la fecha."));
        }

        if (!_companeros.Contains(companeroId))
        {
            return ResultadoDeEscritura.NoSeEscribio(
                Aviso.Problema("No se puede firmar a nombre de alguien que no esta en la base."));
        }

        var clave = Clave(tabla, registroId, campo);
        if (!_filas.TryGetValue(clave, out var fila))
        {
            return ResultadoDeEscritura.NoSeEscribio(
                Aviso.Problema("No se encontro la fila que se queria cambiar."));
        }

        _filas[clave] = fila with
        {
            Verificado = true,
            VerificadoPor = companeroId,
            VerificadoEn = verificadoEn,
        };
        return ResultadoDeEscritura.Bien(fila.Id);
    }

    /// <inheritdoc />
    public ResultadoDeEscritura RetirarLaFirma(TablaDeProcedencia tabla, long registroId, string campo)
    {
        var clave = Clave(tabla, registroId, campo);
        if (!_filas.TryGetValue(clave, out var fila)) return ResultadoDeEscritura.Bien(0);

        _filas[clave] = fila with { Verificado = false, VerificadoPor = null, VerificadoEn = null };
        return ResultadoDeEscritura.Bien(fila.Id);
    }

    /// <summary>La clave de la tabla: (tabla, registro_id, campo), como en el esquema.</summary>
    /// <param name="tabla">Si es del caso o de una persona.</param>
    /// <param name="registroId">El id del caso o de la persona.</param>
    /// <param name="campo">El nombre de la columna.</param>
    private static string Clave(TablaDeProcedencia tabla, long registroId, string campo)
        => $"{tabla}:{registroId}:{campo}";
}
