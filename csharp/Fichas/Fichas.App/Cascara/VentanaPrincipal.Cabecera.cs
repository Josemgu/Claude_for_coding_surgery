using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;

namespace Fichas.App.Cascara;

/// <summary>
/// La mitad de la ventana que dibuja el marco del mockup: la cabecera con su titulo, la
/// fecha de hoy y el buscador, y los nueve atajos del menu.
/// </summary>
/// <remarks>
/// <para>Nace el 2026-09-09, cuando el dueno abrio el programa y dijo que «no se parece en
/// nada al mockup de inicio v2», y despues acoto: «deja Inicio como esta, hazme el marco».
/// Medido antes de tocar nada sobre <c>VentanaPrincipal.xaml</c>: cero
/// <c>NavigationViewItemHeader</c>, cero <c>AutoSuggestBox</c>, cero
/// <c>KeyboardAccelerator</c>.</para>
///
/// <para>Va en un archivo aparte del <c>.xaml.cs</c> a proposito: ese archivo es el arranque
/// de la ventana y estuvo congelado dos veces; mezclar aqui el marco lo volveria a hinchar.
/// La logica que se puede comprobar sin abrir una ventana no esta aqui, sino en
/// <see cref="TituloDeLaPantalla"/>, <see cref="FechaDeLaCabecera"/> y
/// <see cref="BusquedaDeLaCabecera"/>.</para>
/// </remarks>
public sealed partial class VentanaPrincipal
{
    /// <summary>
    /// Cuanto se espera desde la ultima tecla antes de preguntar a la base.
    /// </summary>
    /// <remarks>
    /// Sin esta espera, escribir «RVSC2609» lanza ocho consultas y solo importa la ultima.
    /// Con 3 000 documentos detras eso son siete recorridos tirados por palabra tecleada.
    /// </remarks>
    private static readonly TimeSpan LoQueSeEsperaAlTeclear = TimeSpan.FromMilliseconds(250);

    /// <summary>Quien busca de verdad en los casos; nulo hasta que la cabecera se monta.</summary>
    private BusquedaDeLaCabecera? _busqueda;

    /// <summary>El reloj que espera <see cref="LoQueSeEsperaAlTeclear"/> desde la última tecla antes de consultar.</summary>
    private DispatcherTimer? _esperaAlTeclear;

    /// <summary>Lo que dio la última consulta, para saber adónde ir cuando se pulsa Intro.</summary>
    private LoQueSeEncontro _ultimoHallazgo = LoQueSeEncontro.Nada;

    /// <summary>
    /// Deja la cabecera lista: la fecha de hoy, la version del programa y el buscador con su
    /// motor detras.
    /// </summary>
    private void MontarLaCabecera()
    {
        _busqueda = new BusquedaDeLaCabecera(_servicios.Casos);

        _fechaDeHoy.Text = FechaDeLaCabecera.LargaDelReloj(_servicios.Reloj);
        _versionDelPrograma.Text = VersionDelPrograma.ComoSeLee;
        _buscador.PlaceholderText = BusquedaDeLaCabecera.ElMarcador;

        _esperaAlTeclear = new DispatcherTimer { Interval = LoQueSeEsperaAlTeclear };
        _esperaAlTeclear.Tick += AlCumplirseLaEspera;
    }

    /// <summary>Escribe en la cabecera el titulo de la pantalla a la que se acaba de ir.</summary>
    private void PonerElTituloDeLaPantalla(string etiqueta)
        => _tituloDeLaPantalla.Text = TituloDeLaPantalla.De(etiqueta);

    // ---- los atajos -----------------------------------------------------------

    /// <summary>
    /// Alt+1 … Alt+9: salta a la entrada dueña del atajo, desde donde se este.
    /// </summary>
    /// <remarks>
    /// Se llega a la entrada por <c>args.Element</c> —el elemento al que pertenece el
    /// acelerador— y no por un indice escrito aparte: asi anadir o mover una entrada del
    /// menu no puede dejar un atajo apuntando a otra pantalla sin que nadie se entere.
    /// </remarks>
    private void AlPulsarElAtajoDeUnaEntrada(
        KeyboardAccelerator quien, KeyboardAcceleratorInvokedEventArgs cuando)
    {
        if (cuando.Element is not NavigationViewItem entrada) return;

        cuando.Handled = true;
        _navegacion.SelectedItem = entrada;
    }

    /// <summary>Alt+B: pone el cursor en el buscador sin tener que alcanzarlo con el ratón.</summary>
    private void AlPulsarElAtajoDelBuscador(
        KeyboardAccelerator quien, KeyboardAcceleratorInvokedEventArgs cuando)
    {
        cuando.Handled = true;
        _buscador.Focus(FocusState.Programmatic);
    }

    // ---- el buscador ----------------------------------------------------------

    /// <summary>Al teclear, arranca la espera; no se pregunta a la base en cada letra.</summary>
    private void AlEscribirEnElBuscador(AutoSuggestBox quien, AutoSuggestBoxTextChangedEventArgs cuando)
    {
        // Lo que cambia porque se eligio una sugerencia no vuelve a buscarse: seria
        // preguntar por lo que ya se encontro.
        if (cuando.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

        _esperaAlTeclear?.Stop();
        _esperaAlTeclear?.Start();
    }

    /// <summary>Cuando pasa la espera sin teclear mas, se busca de verdad.</summary>
    private void AlCumplirseLaEspera(object? quien, object cuando)
    {
        _esperaAlTeclear?.Stop();
        Buscar();
    }

    /// <summary>Al pulsar Intro o el icono de la lupa, se busca sin esperar.</summary>
    private void AlPedirLaBusqueda(AutoSuggestBox quien, AutoSuggestBoxQuerySubmittedEventArgs cuando)
    {
        _esperaAlTeclear?.Stop();
        Buscar();
    }

    /// <summary>Pregunta por lo tecleado y deja el resultado en el desplegable.</summary>
    private void Buscar()
    {
        if (_busqueda is null) return;

        _ultimoHallazgo = _busqueda.Buscar(_buscador.Text);
        _buscador.ItemsSource = _ultimoHallazgo.Renglones.Select(r => r.ComoSeLee).ToList();

        // El resumen va en el pie y no en un cuadro: requisito 9 del dueño, que ya cumple
        // el resto del programa. Y dice el total, para que ocho renglones no se lean como
        // «solo hay ocho».
        if (!string.IsNullOrEmpty(_ultimoHallazgo.Resumen)) _acuse.Decir(_ultimoHallazgo.Resumen);
    }

    /// <summary>
    /// Al elegir un documento del desplegable, se copia su número de caso y se dice cuál es.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Aquí NO se navega al documento, y es una frontera declarada, no un olvido.</b>
    /// <c>PaginaDeFichas.OnNavigatedTo</c> solo recoge un <see cref="Servicios"/> del
    /// parámetro de navegación —<c>cuando.Parameter as Servicios</c>—, así que entregarle un
    /// caso concreto a una pantalla obliga a cambiar las pantallas, que son de otros. Lo que
    /// sí cabe hacer aquí es dejar el número en el portapapeles, que es lo que hace falta
    /// para pegarlo en el buscador de Revisar o en el desplegable de Corrección.
    /// </remarks>
    private void AlElegirLoEncontrado(AutoSuggestBox quien, AutoSuggestBoxSuggestionChosenEventArgs cuando)
    {
        var elegido = _ultimoHallazgo.Renglones
            .FirstOrDefault(r => string.Equals(r.ComoSeLee, cuando.SelectedItem as string, StringComparison.Ordinal));

        if (elegido is null) return;

        Copiar(elegido.NumeroCaso);
        _acuse.Decir($"{elegido.ComoSeLee} — número de caso copiado.");
    }

    /// <summary>
    /// Deja un texto en el portapapeles, y si el sistema no deja, lo dice en vez de callarse.
    /// </summary>
    /// <remarks>
    /// El portapapeles lo puede tener tomado otro programa y la llamada falla. Tragarse ese
    /// fallo dejaría al dueño pegando lo que hubiera antes, creyendo que es el número de
    /// caso: eso es peor que decirle que no se pudo.
    /// </remarks>
    private void Copiar(string texto)
    {
        try
        {
            var paquete = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            paquete.SetText(texto);
            Clipboard.SetContent(paquete);
        }
        catch (Exception fallo) when (fallo is COMException or UnauthorizedAccessException)
        {
            _acuse.Decir($"No se pudo copiar «{texto}»: {fallo.Message}");
        }
    }
}
