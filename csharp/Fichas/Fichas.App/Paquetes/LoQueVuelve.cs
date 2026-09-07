using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Paquetes;

/// <summary>
/// Arma lo que hay que ensenar de un Excel devuelto, leyendo la base DESPUES de aplicarlo.
/// </summary>
/// <remarks>
/// <para><b>Lee la base, no la hoja.</b> Lo que se pinta y lo que se firma son los valores
/// que hay guardados, no los que el companero tenia delante: entre que se genero el paquete
/// y que volvio, Miguel pudo corregir un numero de caso. Firmar lo que decia la hoja seria
/// firmar un valor que ya no existe.</para>
///
/// <para>⛔ <b>Aqui NO se vuelve a decidir que fila casa con quien.</b> Esa regla vive en
/// <c>Fichas.Paquetes</c> y esta probada alli. Lo unico que se hace es volver a encontrar a
/// la persona por el camino que NO puede quedar ambiguo —el id del caso que trae la clave
/// del Excel, mas el MRN—, y la fila que no llegue por ese camino se queda fuera del boton
/// diciendo por que. Repetir aqui la resolucion por numero de caso seria tener dos verdades
/// que se separan el dia que alguien toque una.</para>
/// </remarks>
public sealed class LoQueVuelve
{
    private readonly ICasos _casos;
    private readonly IPersonas _personas;

    /// <summary>Se ata a los dos puertos que necesita y a nada mas.</summary>
    public LoQueVuelve(ICasos casos, IPersonas personas)
    {
        _casos = casos;
        _personas = personas;
    }

    /// <summary>Lo que trajo esa hoja: lo que se puede mirar, lo que no caso y lo que no se pudo.</summary>
    public LoQueTrajoElPaquete De(
        string deQuien,
        IReadOnlyList<MarcaDelCompanero> marcas,
        IReadOnlyList<FilaDescartada> noEntraron)
    {
        ArgumentNullException.ThrowIfNull(marcas);
        ArgumentNullException.ThrowIfNull(noEntraron);

        var informaciones = new List<InformacionQueVolvio>();
        var sinPoder = new List<string>();

        // Una fila que se cayo AL APLICAR ya esta contada entre las que no entraron. Si
        // volviera a salir aqui, el dueno leeria la misma fila dos veces y en dos sitios que
        // se contradicen: «entró» y «no entró». Las dos listas no se solapan nunca.
        var seCayeron = noEntraron.Where(fila => fila.FilaExcel is not null)
                                  .Select(fila => fila.FilaExcel!.Value)
                                  .ToHashSet();

        foreach (var marca in marcas)
        {
            if (marca.FilaExcel is int fila && seCayeron.Contains(fila)) continue;

            var una = Mirar(marca, out var porQueNo);
            if (una is not null) informaciones.Add(una);
            else if (porQueNo is not null) sinPoder.Add(porQueNo);
        }

        return new LoQueTrajoElPaquete(deQuien, informaciones, noEntraron, sinPoder);
    }

    /// <summary>Convierte una marca en lo que se ensena, o dice por que no se puede.</summary>
    /// <remarks>
    /// Devuelve nulo con motivo escrito en los tres casos que existen, y ninguno es un
    /// error del programa: la hoja es de antes del id en la clave, el documento ya no esta,
    /// o la persona ya no esta. Los tres se ensenan aparte y ninguno se firma.
    /// </remarks>
    private InformacionQueVolvio? Mirar(MarcaDelCompanero marca, out string? porQueNo)
    {
        porQueNo = null;
        if (marca.CasoId is not long casoId)
        {
            porQueNo = $"Fila {Fila(marca)}: la clave de esa hoja no trae el número interno del "
                     + "documento, así que no se puede dar por buena en bloque sin arriesgarse a "
                     + "firmar sobre otra familia. Vuelva a generar el paquete.";
            return null;
        }

        var caso = _casos.Obtener(casoId);
        if (caso is null)
        {
            porQueNo = $"Fila {Fila(marca)}: el documento con el número interno {casoId} ya no está en la base.";
            return null;
        }

        var candidatas = _personas.DeCaso(casoId).Where(persona => persona.Mrn == marca.Mrn).ToList();
        if (candidatas.Count != 1)
        {
            var cuantas = candidatas.Count == 0
                ? "no hay ninguna persona"
                : $"hay {candidatas.Count} personas";
            porQueNo = $"Fila {Fila(marca)}: en el documento {caso.NumeroCaso ?? "sin número"} "
                     + $"{cuantas} con esa cédula, y en bloque solo se da por buena la que no admite duda.";
            return null;
        }

        return DeLaBase(marca, caso, candidatas[0]);
    }

    /// <summary>Los siete campos, leidos de la base, con el estado que puso el companero.</summary>
    private static InformacionQueVolvio DeLaBase(MarcaDelCompanero marca, Caso caso, Persona persona)
        => new()
        {
            FilaExcel = marca.FilaExcel,
            CasoId = caso.Id,
            PersonaId = persona.Id,
            NumeroCaso = caso.NumeroCaso,
            UnidadNumero = caso.UnidadNumero,
            UnidadNombre = caso.UnidadNombre,
            FechaViaje = caso.FechaViaje,
            TemploNombre = caso.TemploNombre,
            Nombre = persona.Nombre,
            Mrn = persona.Mrn,
            EstadoDelDocumento = Caso.LeerEstado(caso.EstadoRecomendacion),
        };

    /// <summary>El numero de fila para el mensaje; «?» cuando la hoja no lo dijo.</summary>
    private static string Fila(MarcaDelCompanero marca)
        => marca.FilaExcel?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?";
}
