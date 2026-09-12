using Fichas.Contratos.Consultas;
using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Datos.Falso;

/// <summary>
/// Los paquetes inventados: no escriben ningun Excel ni leen ninguno.
/// </summary>
/// <remarks>
/// ⚠️ NO toca el disco. Existe para que la pantalla de Paquetes se pueda construir antes
/// que Fichas.Paquetes (fase C6). El Excel de verdad, con su reconciliacion por
/// caso:MRN:id, es de esa fase.
/// </remarks>
public sealed class PaquetesFalsos : IPaquetes
{
    /// <summary>El almacén en memoria que comparten todos los repositorios falsos; aquí no hay otra fuente.</summary>
    private readonly AlmacenFalso _almacen;

    /// <summary>Se ata al almacen que comparten los seis repositorios falsos.</summary>
    /// <param name="almacen">El almacén compartido; el mismo para todos los repositorios de una base.</param>
    public PaquetesFalsos(AlmacenFalso almacen) => _almacen = almacen;

    /// <summary>Dice que genero el Excel de ida, sin escribir nada, y avisa de que es falso.</summary>
    /// <param name="companeroId">Para quién; si no existe, no se escribe y se dice.</param>
    /// <param name="casoIds">Qué casos irían; solo se cuentan.</param>
    /// <param name="rutaDestino">Dónde iría el archivo; solo se nombra en el aviso.</param>
    public ResultadoDeEscritura GenerarExcelDeCompanero(long companeroId, IReadOnlyList<long> casoIds, string rutaDestino)
    {
        if (!_almacen.Companeros.TryGetValue(companeroId, out var companero))
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema($"No hay ningun companero con el numero interno {companeroId}."));

        return ResultadoDeEscritura.BienCon(companeroId, Aviso.Informa(
            $"Paquete de {companero.Nombre} con {casoIds.Count} caso(s): NO se escribio ningun archivo.",
            string.Empty,
            $"Los datos son inventados y el Excel de verdad llega en la fase C6. La ruta pedida era «{rutaDestino}»."));
    }

    /// <summary>Devuelve una vuelta vacia y dice por que; no lee ningun archivo.</summary>
    /// <param name="rutaExcel">La ruta pedida; solo se nombra en el aviso.</param>
    /// <param name="companeroId">De quién sería la hoja; no se usa.</param>
    public ResultadoDelExcelDevuelto LeerExcelDevuelto(string rutaExcel, long companeroId)
        => new([], [], [Aviso.Advierte(
            "Con los datos inventados no se lee ningun Excel devuelto.",
            string.Empty,
            $"La lectura de verdad llega en la fase C6. La ruta pedida era «{rutaExcel}».")]);

    /// <summary>Aplica las marcas al almacen escribiendo estado; NUNCA firma campos.</summary>
    /// <remarks>
    /// Casa cada marca por MRN y solo por MRN; la que no casa va a las descartadas con su
    /// motivo. Esta es la parte del doble que SÍ escribe, aunque no toque el disco.
    /// </remarks>
    /// <param name="marcas">Las filas de la hoja devuelta.</param>
    /// <param name="companeroId">De quién era la hoja; tiene que existir.</param>
    /// <param name="rutaExcel">De qué archivo vino; se guarda como origen de la marca.</param>
    public ResultadoDeEscritura AplicarMarcas(IReadOnlyList<MarcaDelCompanero> marcas, long companeroId, string rutaExcel)
    {
        if (!_almacen.Companeros.ContainsKey(companeroId))
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Problema($"No hay ningun companero con el numero interno {companeroId}."));

        var aplicadas = 0;
        var descartadas = 0;
        foreach (var marca in marcas)
        {
            // La clave es caso:MRN. Sin MRN no se casa por nombre: eso crearia registros fantasma.
            var persona = marca.Mrn is null
                ? null
                : _almacen.Personas.Values.FirstOrDefault(p => p.Mrn == marca.Mrn);

            if (persona is null)
            {
                descartadas++;
                var id = _almacen.SiguienteId();
                _almacen.Descartadas[id] = new FilaDescartada
                {
                    Id = id,
                    CompaneroId = companeroId,
                    RutaExcel = rutaExcel,
                    FilaExcel = marca.FilaExcel,
                    NumeroCaso = marca.NumeroCaso,
                    Mrn = marca.Mrn,
                    Nombre = marca.Nombre,
                    Motivo = marca.Mrn is null
                        ? $"La fila {marca.FilaExcel} volvio sin MRN y no se puede casar por nombre."
                        : $"La fila {marca.FilaExcel} traia el MRN «{marca.Mrn}», que no esta en la base.",
                    RegistradoEn = _almacen.Reloj.Ahora(),
                };
                continue;
            }

            _almacen.Personas[persona.Id] = persona with
            {
                EstadoPropuesto = marca.EstadoPropuesto,
                NotaCompanero = marca.NotaCompanero,
                PasoPreparacion = marca.PasoPreparacion,
                PasoInformacion = marca.PasoInformacion,
                PasoCitaDelTemplo = marca.PasoCitaDelTemplo,
                PasoAccionesRequeridas = marca.PasoAccionesRequeridas,
                PasoEntrevistas = marca.PasoEntrevistas,
                PasoListoParaElTemplo = marca.PasoListoParaElTemplo,
                LlamoAlLider = marca.LlamoAlLider,
                PropuestoPor = companeroId,
                PropuestoEn = _almacen.Reloj.Ahora(),
            };

            // El Excel escribe el ESTADO del caso con el nombre del companero. No firma campos.
            if (_almacen.Casos.TryGetValue(persona.CasoId, out var caso)
                && marca.EstadoDeLaRecomendacion != EstadoDeRecomendacion.SinMarcar)
            {
                var texto = Caso.EscribirEstado(marca.EstadoDeLaRecomendacion);
                _almacen.Casos[caso.Id] = caso with
                {
                    EstadoRecomendacion = texto,
                    EstadoDelCompanero = texto,
                    EstadoDelCompaneroPor = companeroId,
                    EstadoDelCompaneroEn = _almacen.Reloj.Ahora(),
                    EstadoMarcadoPor = companeroId,
                    EstadoMarcadoEn = _almacen.Reloj.Ahora(),
                    EstadoMarcadoOrigen = rutaExcel,
                };
            }

            aplicadas++;
        }

        var avisos = new List<Aviso> { Aviso.Informa($"Se aplicaron {aplicadas} fila(s) del paquete.") };
        if (descartadas > 0)
        {
            avisos.Add(Aviso.Advierte(
                $"{descartadas} fila(s) no casaron con nadie y quedan en la lista de descartadas.",
                string.Empty,
                "No se casan por nombre: casar por nombre crearia registros fantasma. Estan guardadas con su motivo."));
        }
        return new ResultadoDeEscritura(true, companeroId, avisos);
    }
}
