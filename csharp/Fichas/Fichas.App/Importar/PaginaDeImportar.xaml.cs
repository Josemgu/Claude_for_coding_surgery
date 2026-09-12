using System.Globalization;
using Fichas.App.Cascara;
using Fichas.Contratos.Modelos;
using Microsoft.UI.Xaml;

namespace Fichas.App.Importar;

/// <summary>
/// La pantalla por la que entra TODO: los PDF escaneados.
/// </summary>
/// <remarks>
/// <para>Solo dibuja y recoge lo que se pulsa. Todo lo que decide —que hoja se une a que
/// caso, que es un duplicado, que se guarda— vive en <see cref="MotorDeImportacion"/> y en
/// <see cref="GuardadoDeHojas"/>, que se prueban sin abrir ninguna ventana.</para>
///
/// <para>⛔ Ni un cuadro modal, y la ventana NO se bloquea: la lectura corre en un hilo
/// aparte y las otras cinco pantallas siguen respondiendo mientras la barra avanza. Con
/// 500 documentos a 9 segundos por hoja, bloquear la ventana serian mas de una hora de
/// programa secuestrado.</para>
/// </remarks>
public sealed partial class PaginaDeImportar : PaginaDeFichas
{
    /// <summary>El freno de la tanda en curso; nulo cuando no hay ninguna. «Detener» lo cancela y <see cref="Parar"/> lo suelta.</summary>
    private CancellationTokenSource? _freno;

    /// <summary>Monta la pantalla.</summary>
    public PaginaDeImportar() => InitializeComponent();

    /// <summary>Deja dicho si el lector no esta disponible, en vez de fallar al pulsar.</summary>
    protected override void AlLlegar()
    {
        if (Servicios?.SonDatosInventados == true)
        {
            _botonDeArchivos.IsEnabled = false;
            _botonDeCarpeta.IsEnabled = false;
            _loQueVaSaliendo.Text =
                "El programa se abrió con datos de ejemplo, así que no hay dónde guardar lo que se importe.";
            _zonaDeProgreso.Visibility = Visibility.Visible;
        }
    }

    /// <summary>Abre el selector de archivos y arranca la tanda con lo que se elija.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private async void AlElegirArchivos(object quien, RoutedEventArgs cuando)
        => await SinTragarseNadaAsync("Elegir archivos", async () =>
        {
            var elegidos = PedirAlSistema(
                "SELECTOR  se abre el de elegir varios PDF",
                ventana => SelectorDeArchivos.CualesAbrir(
                    ventana, "Los PDF escaneados", "PDF", RutasDePdf.ExtensionDePdf));
            if (elegidos.Count == 0) return;

            await ImportarAsync(elegidos);
        });

    /// <summary>Abre el selector de carpeta y arranca la tanda con todo lo que haya dentro.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private async void AlElegirCarpeta(object quien, RoutedEventArgs cuando)
        => await SinTragarseNadaAsync("Elegir una carpeta", async () =>
        {
            var elegida = PedirAlSistema(
                "SELECTOR  se abre el de elegir carpeta",
                ventana =>
                {
                    var carpeta = SelectorDeArchivos.QueCarpeta(
                        ventana, "La carpeta con los PDF escaneados");
                    return carpeta is null ? [] : (string[])[carpeta];
                });
            if (elegida.Count == 0) return;

            await ImportarAsync(elegida);
        });

    /// <summary>Pide parar. Lo ya procesado se queda guardado.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlDetener(object quien, RoutedEventArgs cuando)
        => SinTragarseNada("Detener", () => _freno?.Cancel());

    /// <summary>Abre o cierra el detalle. NO es un cuadro modal: se abre aqui debajo.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarVer(object quien, RoutedEventArgs cuando)
        => SinTragarseNada("Ver el detalle", () =>
        {
            var abierto = _zonaDelDetalle.Visibility == Visibility.Visible;
            _zonaDelDetalle.Visibility = abierto ? Visibility.Collapsed : Visibility.Visible;
            _botonDeVer.Content = abierto ? "ver" : "ocultar";
        });

    /// <summary>
    /// Abre el cuadro del sistema que se le diga y deja escrito en el cuaderno que se abrio y
    /// que se eligio.
    /// </summary>
    /// <remarks>
    /// <para>Es el mismo <see cref="SelectorDeArchivos"/> de la cascara que usan Reportes y
    /// Paquetes, y no otro: el 2026-09-04 esta pantalla tuvo el suyo, con los selectores del
    /// Windows App SDK, y el supervisor midio sobre el paquete publicado que no abria ningun
    /// cuadro. El motivo entero esta escrito en ese archivo.</para>
    /// <para>Si no hay ventana se QUEJA en vez de colgar el cuadro de nada: la queja sube al
    /// manejador, que la deja en la franja de avisos. Y las dos lineas del cuaderno no son
    /// andamio de prueba: son lo que faltaba el dia que QA encontro muertos estos dos botones.
    /// Sin ellas, «no pasa nada al pulsar» y «se abrio y lo cerre sin querer» se leen igual
    /// desde fuera.</para>
    /// </remarks>
    /// <param name="queSeAbre">La línea que se anota en el cuaderno antes de abrir el cuadro.</param>
    /// <param name="abrirElCuadro">Quien abre el cuadro sobre el asa de la ventana y devuelve lo elegido.</param>
    /// <returns>Lo elegido; vacío si se cerró sin elegir.</returns>
    /// <exception cref="InvalidOperationException">Si no hay ventana principal a la que colgar el cuadro.</exception>
    private IReadOnlyList<string> PedirAlSistema(
        string queSeAbre, Func<nint, IReadOnlyList<string>> abrirElCuadro)
    {
        var ventana = App.Ventana
            ?? throw new InvalidOperationException(
                "No hay ventana principal a la que colgar el selector del sistema.");

        Servicios?.Registro.Anotar(queSeAbre);
        var elegido = abrirElCuadro(WinRT.Interop.WindowNative.GetWindowHandle(ventana));
        Servicios?.Registro.Anotar(
            elegido.Count == 0
                ? "SELECTOR  se cerró sin elegir nada."
                : $"SELECTOR  se eligieron {elegido.Count}, la primera «{elegido[0]}»");

        return elegido;
    }

    /// <summary>
    /// Corre lo que hace un manejador y, si algo falla, lo DICE.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ El <c>catch</c> es de <see cref="Exception"/> a proposito, y NO es «capturar para
    /// silenciar»: es lo contrario. Este es el borde de un <c>async void</c>, el ultimo
    /// sitio donde un fallo todavia se puede contar; mas alla solo queda el manejador de
    /// ultimo recurso de <c>App</c>, que anota y deja la ventana muerta.
    /// </para>
    /// <para>
    /// Se dice en los TRES sitios: la franja (donde Miguel mira los avisos), el pie (que
    /// acusa recibo de que la accion termino, aunque termine mal) y el cuaderno (que es lo
    /// que se lee despues, cuando la franja ya se cerro).
    /// </para>
    /// </remarks>
    /// <param name="accion">Que se estaba haciendo, tal como se lee en el boton.</param>
    /// <param name="trabajo">Lo que el manejador hace de verdad.</param>
    private async Task SinTragarseNadaAsync(string accion, Func<Task> trabajo)
    {
        try
        {
            await trabajo();
        }
        catch (Exception fallo)
        {
            Contar(accion, fallo);
        }
    }

    /// <summary>Lo mismo para un manejador que no espera a nada.</summary>
    /// <param name="accion">Que se estaba haciendo, tal como se lee en el botón.</param>
    /// <param name="trabajo">Lo que el manejador hace de verdad.</param>
    private void SinTragarseNada(string accion, Action trabajo)
    {
        try
        {
            trabajo();
        }
        catch (Exception fallo)
        {
            Contar(accion, fallo);
        }
    }

    /// <summary>Deja el fallo en la franja, en el pie y en el cuaderno, y suelta los botones.</summary>
    /// <param name="accion">Que se estaba haciendo, tal como se lee en el botón.</param>
    /// <param name="fallo">Lo que se escapó del manejador.</param>
    private void Contar(string accion, Exception fallo)
    {
        var aviso = AvisoDeUnFalloEnPantalla.Describir(accion, fallo);

        Servicios?.Avisos.Dejar(aviso);
        Servicios?.Registro.Anotar(AvisoDeUnFalloEnPantalla.LineaParaElCuaderno(accion, fallo));
        (App.Ventana as VentanaPrincipal)?.AcuseDelPie.Decir(aviso.Linea);

        // Una tanda a medias deja los botones bloqueados; hay que poder volver a pulsar.
        Parar();
    }

    /// <summary>Reune los PDF de lo elegido y los importa, avisando por la barra.</summary>
    /// <remarks>No hace nada si no hay servicios o el lector no está disponible; con datos inventados los botones ya vienen apagados.</remarks>
    /// <param name="origenes">Archivos o carpetas tal como salieron del selector.</param>
    private async Task ImportarAsync(IReadOnlyList<string> origenes)
    {
        var servicios = Servicios;
        var lector = servicios?.ObtenerElLector();
        if (servicios is null || lector is null) return;

        var encontrado = RutasDePdf.Reunir(origenes);
        ContarLoQueSeQuedoFuera(servicios, encontrado);
        if (encontrado.Pdf.Count == 0) return;

        var rutas = encontrado.Pdf;

        var motor = new MotorDeImportacion(
            new GuardadoDeHojas(
                servicios.Casos, servicios.Personas, servicios.Procedencia,
                servicios.Ilegibles, servicios.Reloj),
            lector.LeerDocumento);

        PonerEnMarcha(rutas.Count);
        try
        {
            var resumen = await motor.ImportarAsync(rutas, PintarElAvance, _freno!.Token);
            EnsenarElResumen(servicios, resumen);
        }
        finally
        {
            Parar();
        }
    }

    /// <summary>
    /// Dice en la franja, en el pie y en el cuaderno que hubo carpetas que no se dejaron leer.
    /// </summary>
    /// <remarks>
    /// <para>Requisito 9 y su otra mitad: la tanda sigue con lo que si se pudo leer, y lo
    /// que se quedo fuera se NOMBRA. Con 3 000 formularios, «no se pudo leer una carpeta»
    /// sin decir cual no es un aviso: es una adivinanza.</para>
    ///
    /// <para>El cuaderno lleva un renglon por carpeta con la ruta entera y sin recortar,
    /// porque la franja se cierra y esas rutas es lo que hace falta despues para saber
    /// exactamente que no entro.</para>
    /// </remarks>
    /// <param name="servicios">Por donde se llega a la franja y al cuaderno.</param>
    /// <param name="encontrado">Lo que salió de recorrer lo elegido.</param>
    private static void ContarLoQueSeQuedoFuera(Cascara.Servicios servicios, LoQueSeEncontro encontrado)
    {
        foreach (var renglon in encontrado.RenglonesParaElCuaderno()) servicios.Registro.Anotar(renglon);

        var aviso = encontrado.AvisoDeLaBusqueda();
        if (aviso is null) return;

        servicios.Avisos.Dejar(aviso);
        servicios.Registro.Anotar($"IMPORTACIÓN  {aviso.Linea}");
        (App.Ventana as VentanaPrincipal)?.AcuseDelPie.Decir(aviso.Linea);
    }

    /// <summary>Prepara la barra y bloquea los botones que no tocan durante la tanda.</summary>
    /// <param name="cuantosDocumentos">El tope de la barra: cuántos PDF va a leer la tanda.</param>
    private void PonerEnMarcha(int cuantosDocumentos)
    {
        _freno?.Dispose();
        _freno = new CancellationTokenSource();

        _botonDeArchivos.IsEnabled = false;
        _botonDeCarpeta.IsEnabled = false;
        _botonDeDetener.IsEnabled = true;

        _zonaDeProgreso.Visibility = Visibility.Visible;
        _zonaDelResumen.Visibility = Visibility.Collapsed;
        _barra.Maximum = cuantosDocumentos;
        _barra.Value = 0;
        _cuantosVan.Text = TextoDeLaCuenta(0, cuantosDocumentos);
        _loQueVaSaliendo.Text = "Leyendo…";
    }

    /// <summary>Devuelve los botones a su sitio cuando la tanda termina o se para.</summary>
    private void Parar()
    {
        _botonDeArchivos.IsEnabled = Servicios?.SonDatosInventados != true;
        _botonDeCarpeta.IsEnabled = Servicios?.SonDatosInventados != true;
        _botonDeDetener.IsEnabled = false;
        _freno?.Dispose();
        _freno = null;
    }

    /// <summary>
    /// Pinta cuantos van de cuantos, con las cifras que lleva el resumen hasta ahora.
    /// </summary>
    /// <remarks>
    /// Las cifras salen del MISMO resumen que se ensena al terminar. Dos cuentas separadas
    /// —una para la barra y otra para el final— serian dos cuentas que pueden no coincidir,
    /// y entonces habria que preguntarse cual creerse.
    /// </remarks>
    /// <param name="enCurso">El resumen tal como va, que el motor entrega después de cada documento.</param>
    private void PintarElAvance(ResumenDeLaTanda enCurso)
    {
        _barra.Value = enCurso.Documentos;
        _cuantosVan.Text = TextoDeLaCuenta(enCurso.Documentos, enCurso.TotalDeDocumentos);
        _loQueVaSaliendo.Text = enCurso.Linea();
    }

    /// <summary>Deja el resumen en la pantalla y tambien en la franja y en el pie.</summary>
    /// <remarks>
    /// En los tres sitios a proposito: la franja es donde Miguel mira los avisos de todo el
    /// programa, el pie acusa recibo de que la accion termino, y la pantalla guarda el
    /// detalle para poder volver a leerlo sin repetir la importacion.
    /// </remarks>
    /// <param name="servicios">Por donde se llega a la franja y al cuaderno.</param>
    /// <param name="resumen">El resumen final de la tanda.</param>
    private void EnsenarElResumen(Cascara.Servicios servicios, ResumenDeLaTanda resumen)
    {
        _lineaDelResumen.Text = resumen.Linea();
        _textoDelDetalle.Text = resumen.Detalle();
        _zonaDelResumen.Visibility = Visibility.Visible;
        _zonaDelDetalle.Visibility = Visibility.Collapsed;
        _botonDeVer.Content = "ver";

        servicios.Avisos.Dejar(
            resumen.Ilegibles > 0 || resumen.Duplicados > 0
                ? Aviso.Advierte(resumen.Linea(), string.Empty, resumen.Detalle())
                : Aviso.Informa(resumen.Linea(), string.Empty, resumen.Detalle()));

        (App.Ventana as VentanaPrincipal)?.AcuseDelPie.Decir(resumen.Linea());
        servicios.Registro.Anotar($"IMPORTACIÓN  {resumen.Linea()}");

        // Las dos cosas que el dueno pidio el 2026-09-07 y que solo tienen sentido cuando la
        // tanda ya termino: corregir las carpetas que se veran en Revisar, y borrar lo que
        // entro sin ninguna persona. Estan en PaginaDeImportar.Carpetas.cs.
        _ultimaTanda = resumen;
        PintarLoDeDespuesDeLaTanda(servicios);
    }

    /// <summary>«3 de 500 documentos», con la concordancia bien puesta.</summary>
    /// <param name="van">Cuántos documentos ya se procesaron.</param>
    /// <param name="total">Cuántos tiene la tanda.</param>
    private static string TextoDeLaCuenta(int van, int total) => string.Format(
        CultureInfo.InvariantCulture,
        "{0} de {1} {2}", van, total, total == 1 ? "documento" : "documentos");
}
