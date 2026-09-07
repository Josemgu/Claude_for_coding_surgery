using Fichas.App.Reportes;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Paquetes;

/// <summary>Lo que deja una vuelta: la linea de siempre y lo que hay que revisar.</summary>
/// <remarks>
/// Las dos cosas van juntas porque salen de la MISMA lectura del Excel. Separarlas en dos
/// llamadas obligaria a leer el archivo dos veces, y la segunda lectura volveria a dejar
/// los renglones de descarte: la base diria que el companero devolvio el doble de filas
/// malas de las que devolvio.
/// </remarks>
/// <param name="Resumen">La linea de tres cifras y su detalle detras de «ver».</param>
/// <param name="LoQueTrajo">Lo que caso, lo que no y lo que no se puede dar por bueno.</param>
public sealed record ResultadoDeLaVuelta(ResumenEnPantalla Resumen, LoQueTrajoElPaquete LoQueTrajo);

/// <summary>
/// La vuelta: leer el Excel que devolvio el companero, aplicar sus marcas y decirlo en UNA linea.
/// </summary>
/// <remarks>
/// <para>Es el criterio que el dueno dijo con un numero: «imagina que tenga 3 000 formularios,
/// me enviaron 300, no me puedo poner a ver uno a uno cuál se completó y cuál no». Por eso lo
/// que sale es una linea con tres cifras, y el detalle —fila por fila, con su motivo— solo al
/// pulsar «ver».</para>
///
/// <para>⚠️ <b>La cuenta de «no entraron» se mide, no se estima.</b> Se cuentan las filas
/// descartadas de ese companero ANTES y DESPUES de la carga, y la diferencia es exactamente
/// lo que no entro: las que no casaron al leer el Excel y las que se cayeron al aplicarlas.
/// Deducirla de las marcas leidas se dejaria fuera las segundas, que son las que pasan cuando
/// la base cambio entre una cosa y la otra.</para>
///
/// <para>Y quede dicho lo que NO hace esta clase: <b>no decide que fila casa con quien</b>. Eso
/// es de <c>Fichas.Paquetes</c>, donde esta probado que una clave ambigua no se aplica a
/// ninguna de las dos familias. Aqui solo se cuenta y se escribe.</para>
///
/// <para>⚠️ <b>Tampoco toca la franja de avisos.</b> Los avisos salen dentro del
/// <see cref="ResumenEnPantalla"/> y los deja la pantalla, que es quien esta en el hilo de la
/// ventana. El motivo, medido, esta escrito en <see cref="ResumenEnPantalla"/>.</para>
/// </remarks>
public sealed class OperacionDeLaVuelta
{
    /// <summary>Cuantas descartadas se piden a la vez al contar.</summary>
    private const int TamanoDelTrozo = 500;

    /// <summary>Cuantos motivos se escriben en el detalle como mucho.</summary>
    /// <remarks>
    /// Hay tope porque el detalle es un texto que alguien lee: con 300 filas descartadas,
    /// 300 renglones no se leen. Lo que el tope deja fuera se dice en el propio detalle, y
    /// TODAS siguen guardadas en la base, que es donde de verdad no se pierden.
    /// </remarks>
    private const int TopeDeMotivos = 50;

    private readonly IPaquetes _paquetes;
    private readonly IIlegibles _ilegibles;
    private readonly LoQueVuelve? _loQueVuelve;

    /// <summary>Se ata a los dos puertos que necesita para aplicar y decirlo en una linea.</summary>
    /// <remarks>
    /// Por esta puerta <see cref="AplicarYRevisar"/> devuelve la vuelta sin nada que revisar:
    /// para ensenar lo que trajo hacen falta los casos y las personas. Es la version honesta
    /// de «aquí no», la misma que usa <c>Servicios.Mantenimiento</c> con <c>--falso</c>.
    /// </remarks>
    public OperacionDeLaVuelta(IPaquetes paquetes, IIlegibles ilegibles)
    {
        _paquetes = paquetes;
        _ilegibles = ilegibles;
    }

    /// <summary>Con los dos puertos de mas que hacen falta para ensenar lo que trajo el paquete.</summary>
    /// <remarks>
    /// Es la que usa la pantalla desde el 2026-09-06, cuando el dueno movio su firma de los
    /// documentos a los paquetes que vuelven de los agentes.
    /// </remarks>
    public OperacionDeLaVuelta(IPaquetes paquetes, IIlegibles ilegibles, ICasos casos, IPersonas personas)
        : this(paquetes, ilegibles)
        => _loQueVuelve = new LoQueVuelve(casos, personas);

    /// <summary>Lee ese Excel como devuelto por ese companero y aplica lo que traiga.</summary>
    public ResumenEnPantalla Aplicar(Companero companero, string rutaExcel)
        => AplicarYRevisar(companero, rutaExcel).Resumen;

    /// <summary>Lo mismo, y ademas lo que trajo, para que el dueno lo mire y lo de por bueno.</summary>
    public ResultadoDeLaVuelta AplicarYRevisar(Companero companero, string rutaExcel)
    {
        ArgumentNullException.ThrowIfNull(companero);

        var antes = IdsDeLasDescartadas(companero.Id);
        var lectura = _paquetes.LeerExcelDevuelto(rutaExcel, companero.Id);
        var avisos = new List<Aviso>(lectura.Avisos);

        var filas = lectura.Marcas.Count + lectura.Descartadas.Count;
        if (filas == 0)
        {
            var vacio = Aviso.Advierte(
                $"«{companero.Nombre}»: el Excel no traía ninguna fila que aplicar.",
                string.Empty,
                $"Se leyó «{rutaExcel}» y no salió ni una persona. Compruebe que es el archivo que "
                + "devolvió el compañero y que conserva la hoja con la que se generó.");
            avisos.Add(vacio);
            return new ResultadoDeLaVuelta(
                new ResumenEnPantalla(
                    false, vacio.Linea, ResumenEnPantalla.DetalleDe(lectura.Avisos, vacio.Detalle!), null, avisos),
                LoQueTrajoElPaquete.Nada(companero.Nombre));
        }

        var escritura = lectura.Marcas.Count > 0
            ? _paquetes.AplicarMarcas(lectura.Marcas, companero.Id, rutaExcel)
            : ResultadoDeEscritura.NoSeEscribio();
        avisos.AddRange(escritura.Avisos);

        var nuevas = DescartadasNuevas(companero.Id, antes);
        var noEntraron = nuevas.Count;
        var entraron = filas - noEntraron;

        return new ResultadoDeLaVuelta(
            new ResumenEnPantalla(
                entraron > 0,
                Linea(companero.Nombre, filas, entraron, noEntraron),
                Detalle(lectura, escritura, nuevas, rutaExcel),
                null,
                avisos),
            _loQueVuelve is null
                ? LoQueTrajoElPaquete.Nada(companero.Nombre)
                : _loQueVuelve.De(companero.Nombre, lectura.Marcas, nuevas));
    }

    /// <summary>La linea de tres cifras: cuantas venian, cuantas entraron y cuantas no.</summary>
    private static string Linea(string quien, int filas, int entraron, int noEntraron)
    {
        var cola = noEntraron == 0
            ? "ninguna se quedó fuera"
            : $"{noEntraron} no {(noEntraron == 1 ? "entró" : "entraron")}";

        return $"{quien}: {filas} {(filas == 1 ? "fila" : "filas")} · "
             + $"{entraron} {(entraron == 1 ? "entró" : "entraron")} · {cola}.";
    }

    /// <summary>Lo que se ve al pulsar «ver»: los avisos y el motivo de cada fila que no entro.</summary>
    private static string Detalle(
        ResultadoDelExcelDevuelto lectura,
        ResultadoDeEscritura escritura,
        IReadOnlyList<FilaDescartada> nuevas,
        string rutaExcel)
    {
        var trozos = new List<string> { $"Archivo leído: {rutaExcel}" };
        if (nuevas.Count > 0)
        {
            trozos.Add("Las filas que NO entraron, con su motivo:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, nuevas.Take(TopeDeMotivos).Select(Renglon)));

            if (nuevas.Count > TopeDeMotivos)
            {
                trozos.Add($"Y {nuevas.Count - TopeDeMotivos} más que no caben aquí. Todas, sin excepción, "
                    + "quedaron guardadas en la base con su archivo, su fila y su motivo.");
            }
        }

        return ResumenEnPantalla.DetalleDe([.. lectura.Avisos, .. escritura.Avisos], [.. trozos]);
    }

    /// <summary>Una fila descartada escrita para leerla: donde estaba y por que no entro.</summary>
    private static string Renglon(FilaDescartada fila)
        => $"  · Fila {fila.FilaExcel?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?"}"
         + $" · caso {fila.NumeroCaso ?? "sin número"}"
         + $" · cédula {fila.Mrn ?? "sin cédula"}"
         + $" · {fila.Nombre ?? "sin nombre"}: {fila.Motivo}";

    /// <summary>Los numeros internos de las descartadas que ese companero ya tenia.</summary>
    private HashSet<long> IdsDeLasDescartadas(long companeroId)
        => [.. LeerLasDescartadas(companeroId).Select(fila => fila.Id)];

    /// <summary>Las descartadas de ese companero que NO estaban antes.</summary>
    private List<FilaDescartada> DescartadasNuevas(long companeroId, HashSet<long> antes)
        => [.. LeerLasDescartadas(companeroId).Where(fila => !antes.Contains(fila.Id)).OrderBy(fila => fila.FilaExcel)];

    /// <summary>Todas las descartadas de ese companero, pedidas por trozos.</summary>
    private List<FilaDescartada> LeerLasDescartadas(long companeroId)
    {
        var todas = new List<FilaDescartada>();
        var trozo = Pagina.Primera(TamanoDelTrozo);
        while (true)
        {
            var pagina = _ilegibles.ListarDescartadas(companeroId, trozo);
            todas.AddRange(pagina.Elementos);
            if (!pagina.HayMas) return todas;
            trozo = trozo.Siguiente();
        }
    }
}
