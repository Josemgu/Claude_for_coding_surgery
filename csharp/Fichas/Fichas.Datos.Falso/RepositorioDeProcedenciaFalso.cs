using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Datos.Falso;

/// <summary>La procedencia inventada. Cumple <see cref="IProcedencia"/> sin tocar ningun archivo.</summary>
public sealed class RepositorioDeProcedenciaFalso : IProcedencia
{
    private readonly AlmacenFalso _almacen;
    private Dictionary<(TablaDeProcedencia Tabla, long RegistroId), List<long>>? _idsPorRegistro;
    private int _filasCuandoSeIndexo = -1;

    /// <summary>Se ata al almacen que comparten los seis repositorios falsos.</summary>
    public RepositorioDeProcedenciaFalso(AlmacenFalso almacen) => _almacen = almacen;

    /// <summary>Devuelve la procedencia de todos los campos de una fila.</summary>
    public IReadOnlyList<ProcedenciaDeCampo> DeRegistro(TablaDeProcedencia tabla, long registroId)
        => [.. IdsDe(tabla, registroId)
            .Select(id => _almacen.Procedencias[id])
            .OrderBy(p => p.Campo, StringComparer.Ordinal)];

    /// <summary>
    /// Los ids de las filas de un registro, buscados por indice y no recorriendolas todas.
    /// </summary>
    /// <remarks>
    /// <para>⚠️ <b>Esto no es una optimizacion de gusto: sin ello la suite tardaba 24 s
    /// donde el tope son 10.</b> Hasta el 2026-09-06 el generador no escribia ni una fila de
    /// procedencia, asi que recorrerlas todas en cada lectura costaba cero y nadie lo veia.
    /// Al sembrarlas —<b>29 784 filas</b> con 3 000 casos— salio a la luz: los reportes hacen
    /// una lectura POR CASO y otra POR PERSONA (unas 10 500), y 10 500 × 29 784 son 313
    /// millones de comparaciones. Medido: <c>ConTresMilCasosElReporteDelMesSeGeneraYSeMideElTiempo</c>
    /// paso de pasar a <b>24,1 s</b>, y el historico a <b>20,8 s</b>.</para>
    ///
    /// <para><b>El indice guarda ids, NO filas, y esa es la parte que importa.</b> Firmar
    /// sustituye la fila entera conservando su id: si el indice guardara las filas, una firma
    /// dejaria dentro la version vieja y <see cref="ContarVerificados"/> seguiria diciendo
    /// cero despues de firmar. Guardando ids, la fila se lee siempre del almacen.</para>
    ///
    /// <para><b>Se rehace cuando aparece una fila nueva</b>, que es lo unico que cambia el
    /// numero de filas. Vale porque aqui <b>nada borra filas de procedencia</b>: no hay
    /// camino de borrado ni en este doble ni en <c>IProcedencia</c>. El dia que lo haya, esta
    /// cuenta deja de bastar y hay que invalidar el indice al borrar.</para>
    /// </remarks>
    private List<long> IdsDe(TablaDeProcedencia tabla, long registroId)
    {
        if (_idsPorRegistro is null || _filasCuandoSeIndexo != _almacen.Procedencias.Count)
        {
            _idsPorRegistro = _almacen.Procedencias
                .GroupBy(par => (par.Value.Tabla, par.Value.RegistroId))
                .ToDictionary(grupo => grupo.Key, grupo => grupo.Select(par => par.Key).ToList());
            _filasCuandoSeIndexo = _almacen.Procedencias.Count;
        }

        return _idsPorRegistro.GetValueOrDefault((tabla, registroId)) ?? [];
    }

    /// <summary>La fila de ese campo concreto, o nula si ese campo no tiene ninguna.</summary>
    private ProcedenciaDeCampo? LaDe(TablaDeProcedencia tabla, long registroId, string campo)
        => IdsDe(tabla, registroId)
            .Select(id => _almacen.Procedencias[id])
            .FirstOrDefault(fila => string.Equals(fila.Campo, campo, StringComparison.Ordinal));

    /// <summary>Devuelve un trozo de los campos por debajo de una confianza, que son los que hay que mirar.</summary>
    public PaginaDe<ProcedenciaDeCampo> PorDebajoDeConfianza(double umbral, Pagina trozo)
    {
        var flojos = _almacen.Procedencias.Values
            .Where(p => p.Confianza is double c && c < umbral)
            .OrderBy(p => p.Confianza)
            .ThenBy(p => p.Id)
            .ToList();
        return Trozos.Cortar(flojos, trozo);
    }

    /// <summary>Todas las filas que pueden cambiar el veredicto de «listo para asignar», de una vez.</summary>
    /// <remarks>
    /// Las cinco condiciones son las mismas y en el mismo orden que el <c>WHERE</c> de
    /// <c>RepositorioDeProcedencia</c>. Que no se separen lo vigila
    /// <c>PruebaDeLaParidadDeLosOtrosCuatro</c>: si un dia dicen cosas distintas, la pantalla
    /// probada contra este doble estaria verde sobre un hueco de la base de verdad, que es
    /// justo el defecto que ese archivo existe para cazar.
    /// </remarks>
    public IReadOnlyList<ProcedenciaDeCampo> LasQuePesanEnElVeredicto(double umbral)
        => [.. _almacen.Procedencias.Values
            .Where(fila => fila.Verificado
                        || fila.AusenteEnElPapel
                        || fila.AnuladoPorTachon
                        || fila.Confianza is not double confianza
                        || confianza < umbral)
            .OrderBy(fila => fila.Tabla)
            .ThenBy(fila => fila.RegistroId)
            .ThenBy(fila => fila.Campo, StringComparer.Ordinal)];

    /// <summary>Que campos de esa tabla tienen fila de procedencia, agrupados por registro.</summary>
    /// <remarks>
    /// ⚠️ <b>Se agrupa a mano y no con <c>GroupBy</c>, y eso se midio.</b> Con LINQ, montar
    /// esto sobre las 29 784 filas de la base inventada era la mitad del coste de la pantalla
    /// de Inicio. La version de abajo hace un solo recorrido y ordena listas de cinco
    /// elementos. Se ordena para que el doble y la base de verdad —que ordena en el
    /// <c>ORDER BY</c>— devuelvan lo mismo en el mismo orden.
    /// </remarks>
    public IReadOnlyDictionary<long, IReadOnlyList<string>> CamposAnotadosDe(TablaDeProcedencia tabla)
    {
        var porRegistro = new Dictionary<long, List<string>>();

        foreach (var fila in _almacen.Procedencias.Values)
        {
            if (fila.Tabla != tabla) continue;
            if (!porRegistro.TryGetValue(fila.RegistroId, out var suyos))
            {
                suyos = [];
                porRegistro[fila.RegistroId] = suyos;
            }

            suyos.Add(fila.Campo);
        }

        var listo = new Dictionary<long, IReadOnlyList<string>>(porRegistro.Count);
        foreach (var (registroId, suyos) in porRegistro)
        {
            suyos.Sort(StringComparer.Ordinal);
            listo[registroId] = suyos;
        }

        return listo;
    }

    /// <summary>Cuenta cuantos campos de una fila estan firmados; al recien extraer es 0.</summary>
    public int ContarVerificados(TablaDeProcedencia tabla, long registroId)
        => IdsDe(tabla, registroId).Count(id => _almacen.Procedencias[id].Verificado);

    /// <summary>Anota la procedencia de un campo. Nunca pone verificado: eso es <see cref="Firmar"/>.</summary>
    public ResultadoDeEscritura Anotar(ProcedenciaDeCampo procedencia)
    {
        // Regla permanente 5: por esta puerta NO se marca nada como verificado, venga como venga.
        var limpia = procedencia with { Verificado = false, VerificadoPor = null, VerificadoEn = null };

        var existente = LaDe(limpia.Tabla, limpia.RegistroId, limpia.Campo);

        var id = existente?.Id ?? (limpia.Id == 0 ? _almacen.SiguienteId() : limpia.Id);
        // Si el campo ya estaba firmado, anotar de nuevo NO le quita la firma a Miguel.
        _almacen.Procedencias[id] = limpia with
        {
            Id = id,
            Verificado = existente?.Verificado ?? false,
            VerificadoPor = existente?.VerificadoPor,
            VerificadoEn = existente?.VerificadoEn,
        };
        return ResultadoDeEscritura.Bien(id);
    }

    /// <summary>Retira la firma de un campo, que es lo que toca cuando su valor cambia.</summary>
    public ResultadoDeEscritura RetirarLaFirma(TablaDeProcedencia tabla, long registroId, string campo)
    {
        var existente = LaDe(tabla, registroId, campo);

        // Retirar una firma que no existe no es un error: es que no habia nada que retirar.
        // Se dice, y con las mismas palabras que `RepositorioDeProcedencia`: quien mire la
        // franja tiene que ver lo mismo corra sobre el doble o sobre la base.
        if (existente is null || !existente.Verificado)
        {
            return ResultadoDeEscritura.BienCon(
                existente?.Id ?? registroId,
                Aviso.Informa(
                    "No habia ninguna firma que retirar en ese campo.",
                    campo,
                    "Se llama al cambiar un valor, y un campo sin firmar es lo normal."));
        }

        // El registro es de solo lectura tras crearse, asi que se sustituye entero.
        _almacen.Procedencias[existente.Id] = existente with
        {
            Verificado = false,
            VerificadoPor = null,
            VerificadoEn = null,
        };
        return ResultadoDeEscritura.Bien(existente.Id);
    }

    /// <summary>Firma un campo como bueno con quien y cuando; el unico camino a verificado.</summary>
    public ResultadoDeEscritura Firmar(TablaDeProcedencia tabla, long registroId, string campo, long companeroId, string verificadoEn)
    {
        // El esquema lo impone y aqui tambien: no hay verificado sin quien y sin cuando.
        if (!_almacen.Companeros.ContainsKey(companeroId))
        {
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema(
                "No se puede firmar sin decir quien firma.",
                campo,
                $"No hay ningun companero con el numero interno {companeroId}. La regla permanente 5 " +
                "exige quien y cuando para poner un campo como verificado."));
        }

        var cuando = string.IsNullOrWhiteSpace(verificadoEn) ? _almacen.Reloj.Ahora() : verificadoEn;
        var existente = LaDe(tabla, registroId, campo);

        var id = existente?.Id ?? _almacen.SiguienteId();
        _almacen.Procedencias[id] = (existente ?? new ProcedenciaDeCampo
        {
            Tabla = tabla,
            RegistroId = registroId,
            Campo = campo,
            Origen = OrigenDeCampo.Manual,
        }) with
        {
            Id = id,
            Verificado = true,
            VerificadoPor = companeroId,
            VerificadoEn = cuando,
        };
        return ResultadoDeEscritura.Bien(id);
    }
}
