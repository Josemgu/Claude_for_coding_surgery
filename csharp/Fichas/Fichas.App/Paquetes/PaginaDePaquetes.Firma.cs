using Fichas.App.Correccion;
using Fichas.App.Reportes;
using Microsoft.UI.Xaml;

namespace Fichas.App.Paquetes;

/// <summary>
/// La mitad de la pantalla de Paquetes que el dueno revisa: lo que trajo el agente y el
/// boton que lo da por bueno de una vez.
/// </summary>
/// <remarks>
/// <para>Va en su propio archivo, no porque el otro estuviera lleno, sino porque son dos
/// trabajos distintos: aquel manda archivos a los companeros y este es donde Miguel firma.
/// Es <c>partial</c> de la misma pantalla porque comparte sus controles.</para>
///
/// <para>El dueno lo pidio el 2026-09-06: <i>«debe haber botones donde aplique "todo
/// completo", igual en los paquetes, porque ir uno por uno si está bien pero no es
/// suficiente»</i>, y dijo por que: <i>«el sistema es para escanear información. Si yo tengo
/// que verificarla luego, ¿para qué me sirve el sistema si tengo que hacerlo igual?»</i></para>
///
/// <para>⛔ <b>Las dos mitades de la regla 5 no se mezclan aqui.</b> El estado de la
/// recomendacion lo escribio el Excel del companero con SU nombre y esta pantalla no lo
/// toca; lo que se firma son campos, y con el nombre del administrador.</para>
/// </remarks>
public sealed partial class PaginaDePaquetes
{
    private FirmaEnBloque? _firma;
    private LoQueTrajoElPaquete? _loQueTrajo;

    /// <summary>Pinta lo que trajo el paquete y decide si el boton se puede pulsar.</summary>
    /// <remarks>
    /// Se llama SIEMPRE al terminar una vuelta, aunque no haya traido nada: si no, la
    /// pantalla se quedaria ensenando el paquete anterior, y el dueno firmaria mirando lo
    /// que trajo otro.
    /// </remarks>
    private void PintarLoQueTrajo(LoQueTrajoElPaquete loQueTrajo)
    {
        _loQueTrajo = loQueTrajo;
        CerrarLaConfirmacion();
        _lineaDeLaFirmaHecha.Visibility = Visibility.Collapsed;

        _zonaDeLoQueTrajo.Visibility = loQueTrajo.HayQueRevisar || loQueTrajo.NoEntraron.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        _lineaDeLoQueTrajo.Text = loQueTrajo.Linea;
        _laListaDeLoQueTrajo.ItemsSource = loQueTrajo.Informaciones.Select(una => una.Renglon).ToList();

        Decir(_loQueFaltaDelPaquete, loQueTrajo.LoQueFalta);
        Decir(_lineaDeLoQueQuedaFuera, loQueTrajo.LoQueQuedaFuera);
        PintarLoQueQuedaFuera(loQueTrajo);

        _botonDeDarPorBueno.IsEnabled = loQueTrajo.HayQueRevisar;
    }

    /// <summary>La lista de lo que NO entra en el boton, con el motivo de cada renglon.</summary>
    /// <remarks>
    /// Va aparte y con su propia cuenta a proposito. Mezclarla con lo que si caso seria
    /// invitar a firmar en bloque algo que no vino, que es justo el error que el dueno
    /// quiere evitar.
    /// </remarks>
    private void PintarLoQueQuedaFuera(LoQueTrajoElPaquete loQueTrajo)
    {
        var renglones = loQueTrajo.NoEntraron
            .Select(fila => $"Fila {fila.FilaExcel?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?"}"
                          + $" · caso {fila.NumeroCaso ?? "sin número"}"
                          + $" · cédula {fila.Mrn ?? "sin cédula"}: {fila.Motivo}")
            .Concat(loQueTrajo.NoSePudieronMirar)
            .ToList();

        _laListaDeLoQueQuedaFuera.ItemsSource = renglones;
        _laListaDeLoQueQuedaFuera.Visibility = renglones.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Primer golpe del boton: cuenta lo que se firmaria y lo ensena. NO escribe nada.
    /// </summary>
    /// <remarks>
    /// El dueno tiene que poder no seguir, y para eso el numero va antes. Un boton que firma
    /// y luego dice cuantas firmo no ofrece esa salida.
    /// </remarks>
    private void AlPulsarDarPorBueno(object quien, RoutedEventArgs cuando)
        => ManejadorSeguro.Correr("Dar por buenas las informaciones", Servicios, () =>
        {
            EnsenarLaCuenta();
            return Task.CompletedTask;
        });

    /// <summary>Escribe en la franja cuantos campos se firmarian y a nombre de quien.</summary>
    private void EnsenarLaCuenta()
    {
        if (_firma is null || Servicios is null || _loQueTrajo is null) return;

        var quienFirma = ElAdministrador.De(Servicios.Companeros.Activos());
        if (quienFirma is null)
        {
            Servicios.Avisos.Dejar(ElAdministrador.PorQueNoSePuede(Servicios.Companeros.Activos()));
            return;
        }

        var cuenta = _firma.Contar(_loQueTrajo);
        _lineaDeLaCuenta.Text = $"{cuenta.Linea} Quedarán a nombre de {quienFirma.Nombre}.";
        _botonDeConfirmarLaFirma.IsEnabled = cuenta.HayAlgoQueFirmar;
        _zonaDeConfirmarLaFirma.Visibility = Visibility.Visible;
        _lineaDeLaFirmaHecha.Visibility = Visibility.Collapsed;
        Servicios.Registro.Anotar($"FIRMA EN BLOQUE  se ofrece: {cuenta.Linea}");
    }

    /// <summary>Segundo golpe: firma de verdad, y solo por esto (regla permanente 5).</summary>
    private void AlPulsarConfirmarLaFirma(object quien, RoutedEventArgs cuando)
        => ManejadorSeguro.Correr("Firmar las informaciones del paquete", Servicios, () =>
        {
            Firmar();
            return Task.CompletedTask;
        });

    /// <summary>Firma los campos contados y deja la cuenta de lo escrito en pantalla.</summary>
    private void Firmar()
    {
        if (_firma is null || Servicios is null || _loQueTrajo is null) return;

        var quienFirma = ElAdministrador.De(Servicios.Companeros.Activos());
        if (quienFirma is null)
        {
            Servicios.Avisos.Dejar(ElAdministrador.PorQueNoSePuede(Servicios.Companeros.Activos()));
            CerrarLaConfirmacion();
            return;
        }

        var resultado = _firma.Firmar(_loQueTrajo, quienFirma);
        CerrarLaConfirmacion();
        Decir(_lineaDeLaFirmaHecha, resultado.Linea);
        Servicios.Avisos.Dejar(resultado.Avisos);
        Acusar(resultado.Linea, "FIRMA EN BLOQUE");

        // Lo que acaba de firmarse ya no se vuelve a ofrecer: la cuenta siguiente es cero y
        // el boton lo dice, en vez de prometer un trabajo que ya esta hecho.
        _botonDeDarPorBueno.IsEnabled = _firma.Contar(_loQueTrajo).HayAlgoQueFirmar;
    }

    /// <summary>Cancelar: se cierra la franja y NO se escribe nada.</summary>
    private void AlPulsarCancelarLaFirma(object quien, RoutedEventArgs cuando) => CerrarLaConfirmacion();

    /// <summary>Esconde la franja de confirmar. No toca la base.</summary>
    private void CerrarLaConfirmacion()
    {
        _zonaDeConfirmarLaFirma.Visibility = Visibility.Collapsed;
        _lineaDeLaCuenta.Text = string.Empty;
    }

    /// <summary>Pinta una linea si la hay, y esconde el control si no la hay.</summary>
    /// <remarks>Un renglon vacio y visible se lee como un hueco de la pantalla, no como «no aplica».</remarks>
    private static void Decir(Microsoft.UI.Xaml.Controls.TextBlock donde, string? linea)
    {
        donde.Text = linea ?? string.Empty;
        donde.Visibility = string.IsNullOrEmpty(linea) ? Visibility.Collapsed : Visibility.Visible;
    }
}
