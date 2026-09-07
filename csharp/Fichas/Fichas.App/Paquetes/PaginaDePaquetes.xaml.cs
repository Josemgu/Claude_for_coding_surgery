using Fichas.App.Asignar;
using Fichas.App.Cascara;
using Fichas.App.Reportes;
using Fichas.Contratos.Modelos;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fichas.App.Paquetes;

/// <summary>
/// La pantalla de Paquetes: el Excel que va al companero y el que vuelve con sus marcas.
/// </summary>
/// <remarks>
/// <para>Aqui no se decide nada: la pantalla coloca controles y pasa mensajes. Lo que lleva un
/// companero lo cuenta <see cref="CargaDeUnCompanero"/>, la ida la hace
/// <see cref="OperacionDelPaquete"/> y la vuelta <see cref="OperacionDeLaVuelta"/>; las tres
/// se prueban sin abrir ventana. Que fila casa con quien es de <c>Fichas.Paquetes</c>.</para>
///
/// <para>⛔ Ni un cuadro modal (requisito 4). La vuelta sale en UNA linea con tres cifras
/// —«300 filas · 297 entraron · 3 no entraron»— porque el dueno lo dijo asi: «imagina que
/// tenga 3 000 formularios, me enviaron 300, no me puedo poner a ver uno a uno cuál se
/// completó y cuál no».</para>
///
/// <para>⚠️ <b>Ningun manejador de esta pantalla es <c>async void</c>.</b> Todos pasan por
/// <see cref="ManejadorSeguro"/>, por el fallo que QA midio el 2026-09-04 en Importar: sus dos
/// botones estaban muertos y no dejaban ni una linea en <c>fichas.log</c>.</para>
/// </remarks>
public sealed partial class PaginaDePaquetes : PaginaDeFichas
{
    private readonly ZonaDeResumen _resumenDeLaIda;
    private readonly ZonaDeResumen _resumenDeLaVuelta;
    private readonly ZonaDeResumen _resumenDeLaSubida;

    private OperacionDelPaquete? _ida;
    private OperacionDeLaVuelta? _vuelta;
    private OperacionDeLaSegundaVuelta? _subida;
    private OperacionDeAsignar? _reparto;
    private string? _ultimoArchivo;

    /// <summary>Monta la pantalla y ata los dos bloques de resumen a sus controles.</summary>
    public PaginaDePaquetes()
    {
        InitializeComponent();
        _resumenDeLaIda = new ZonaDeResumen(_zonaDeLaIda, _lineaDeLaIda, _verDeLaIda, _detalleDeLaIda, _textoDeLaIda);
        _resumenDeLaVuelta = new ZonaDeResumen(
            _zonaDeLaVuelta, _lineaDeLaVuelta, _verDeLaVuelta, _detalleDeLaVuelta, _textoDeLaVuelta);
        _resumenDeLaSubida = new ZonaDeResumen(
            _zonaDeLaSubida, _lineaDeLaSubida, _verDeLaSubida, _detalleDeLaSubida, _textoDeLaSubida);
    }

    /// <summary>Llena los dos desplegables con los companeros activos.</summary>
    /// <remarks>
    /// Solo los activos: un companero desactivado no recibe casos nuevos, asi que tampoco
    /// recibe paquetes. Su trabajo anterior no se toca y sigue en la base.
    /// </remarks>
    protected override void AlLlegar()
    {
        if (Servicios is null) return;

        _ida = new OperacionDelPaquete(Servicios.Paquetes, Servicios.Asignaciones, Servicios.Casos);

        // Con los cuatro puertos, no con dos: desde el 2026-09-06 la vuelta no solo aplica,
        // tambien tiene que ensenar lo que trajo para que el dueno lo de por bueno, y eso
        // sale de los casos y las personas.
        _vuelta = new OperacionDeLaVuelta(
            Servicios.Paquetes, Servicios.Ilegibles, Servicios.Casos, Servicios.Personas);
        _firma = new FirmaEnBloque(Servicios.Procedencia, Servicios.Reloj);

        // La MISMA puerta de asignar que usan Asignar y Revisar, no una copia: quitarle los
        // casos a alguien tiene que dejar la base igual que quitarselos uno a uno.
        _reparto = new OperacionDeAsignar(Servicios.Asignaciones, Servicios.Reloj, Servicios.Avisos);
        _subida = new OperacionDeLaSegundaVuelta(
            Servicios.Paquetes, Servicios.ReporteDeLaSegundaVuelta,
            Servicios.Casos, Servicios.Asignaciones, Servicios.Companeros);

        var activos = Servicios.Companeros.Activos();
        _aQuien.ItemsSource = activos;
        _deQuienViene.ItemsSource = activos;
        _aQuienSube.ItemsSource = activos;

        if (activos.Count == 0)
        {
            _loQueLleva.Text = "No hay ningún compañero activo al que darle un paquete.";
            _loQueSube.Text = "No hay ningún compañero activo al que pasarle una segunda vuelta.";
            return;
        }

        _aQuien.SelectedIndex = 0;
        _deQuienViene.SelectedIndex = 0;
        _aQuienSube.SelectedIndex = 0;
    }

    // ---- la segunda vuelta --------------------------------------------------

    /// <summary>Al elegir a quien se lo pasa se mira que subiria a su peldano.</summary>
    private void AlElegirAQuienSube(object quien, SelectionChangedEventArgs cuando)
        => ManejadorSeguro.Correr("Elegir quién recibe la segunda vuelta", Servicios, () =>
        {
            PintarLoQueSube();
            return Task.CompletedTask;
        });

    /// <summary>
    /// Escribe cuantos documentos subirian a ese peldano, de cuantos, y que se queda fuera.
    /// </summary>
    /// <remarks>
    /// Se pinta ANTES de generar nada y con el denominador delante. «Suben 2» no dice si la
    /// regla esta haciendo lo que se espera; «2 de 5, y los otros 3 por esto» sí, y se ve sin
    /// gastar el trabajo de nadie.
    /// </remarks>
    private void PintarLoQueSube()
    {
        if (_subida is null || _aQuienSube.SelectedItem is not Companero elegido)
        {
            _botonDeLaSegundaVuelta.IsEnabled = false;
            return;
        }

        var sube = _subida.Mirar(elegido);
        _loQueSube.Text = $"{elegido.Nombre} · categoría {elegido.Categoria}: {sube.Linea}";
        _botonDeLaSegundaVuelta.IsEnabled = sube.CasoIds.Count > 0;
    }

    /// <summary>Pide donde guardar y escribe el Excel de la segunda vuelta con su reporte.</summary>
    private void AlPulsarGenerarLaSegundaVuelta(object quien, RoutedEventArgs cuando)
        => ManejadorSeguro.Correr("Generar la segunda vuelta…", Servicios, GenerarLaSegundaVueltaAsync);

    /// <summary>Abre el selector de guardado y escribe los dos archivos donde se diga.</summary>
    /// <remarks>
    /// Fuera del hilo de la ventana, igual que la ida: son dos archivos y el segundo es un PDF.
    /// Unos segundos con la ventana congelada se leen como «el programa se colgó».
    /// </remarks>
    private async Task GenerarLaSegundaVueltaAsync()
    {
        if (_subida is null || Servicios is null || _aQuienSube.SelectedItem is not Companero elegido) return;

        var ruta = ElegirDondeGuardar(
            NombreDeArchivo.DeLaSegundaVuelta(elegido.Nombre, Servicios.Reloj.Hoy()));
        if (ruta is null) return;

        _botonDeLaSegundaVuelta.IsEnabled = false;
        try
        {
            var resumen = await Task.Run(() => _subida.Generar(elegido, ruta)).ConfigureAwait(true);
            _resumenDeLaSubida.Ensenar(resumen);
            Servicios.Avisos.Dejar(resumen.Avisos);
            GuardarQueArchivoSePuedeAbrir(resumen);
            Acusar(resumen.Linea, "SEGUNDA VUELTA");
        }
        finally
        {
            PintarLoQueSube();
        }
    }

    /// <summary>Abre o cierra el detalle de la segunda vuelta.</summary>
    private void AlPulsarVerLaSubida(object quien, RoutedEventArgs cuando) => _resumenDeLaSubida.AlternarElDetalle();

    /// <summary>Cierra el resumen de la segunda vuelta.</summary>
    private void AlPulsarCerrarLaSubida(object quien, RoutedEventArgs cuando) => _resumenDeLaSubida.Cerrar();

    // ---- la ida -------------------------------------------------------------

    /// <summary>Al elegir companero se lee lo que lleva y se enciende el boton.</summary>
    private void AlElegirAQuien(object quien, SelectionChangedEventArgs cuando)
        => ManejadorSeguro.Correr("Elegir compañero", Servicios, () =>
        {
            PintarLoQueLleva();
            return Task.CompletedTask;
        });

    /// <summary>Escribe cuantos casos y cuantas personas lleva el companero elegido.</summary>
    /// <remarks>
    /// El boton de quitar mira las asignaciones VIVAS y no lo que va en el paquete: lo que se
    /// le quita es todo lo que lleva a su nombre, incluido lo que ya devolvio completo y por
    /// eso ya no entra en la hoja. Encenderlo con la cifra del paquete lo dejaria apagado
    /// justo con el companero que mas casos lleva encima.
    /// </remarks>
    private void PintarLoQueLleva()
    {
        EsconderLaFranjaDeQuitar();

        if (_ida is null || _aQuien.SelectedItem is not Companero elegido)
        {
            _botonDeGenerar.IsEnabled = false;
            _botonDeQuitarleTodo.IsEnabled = false;
            return;
        }

        var carga = _ida.Carga(elegido.Id);
        _loQueLleva.Text = $"{elegido.Nombre}: {carga.LineaConLoQueNoVa}";
        _botonDeGenerar.IsEnabled = carga.CasoIds.Count > 0;
        _botonDeQuitarleTodo.IsEnabled =
            _reparto is not null && _reparto.MirarLoQueSeLeQuitaria(elegido.Id, elegido.Nombre).HayAlgoQueQuitar;
    }

    // ---- quitarle todos los casos -------------------------------------------

    /// <summary>Enseña cuantos se le van a quitar y espera; todavia no toca la base.</summary>
    private void AlPulsarQuitarleTodo(object quien, RoutedEventArgs cuando)
        => ManejadorSeguro.Correr("Quitarle todos los casos", Servicios, () =>
        {
            if (_reparto is null || _aQuien.SelectedItem is not Companero elegido) return Task.CompletedTask;

            var loQueSeQuita = _reparto.MirarLoQueSeLeQuitaria(elegido.Id, elegido.Nombre);
            _lineaDeLoQueSeQuita.Text = loQueSeQuita.Pregunta;
            _zonaDeConfirmarElQuitar.Visibility = loQueSeQuita.HayAlgoQueQuitar
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (!loQueSeQuita.HayAlgoQueQuitar) Acusar(loQueSeQuita.Pregunta, "QUITAR");
            return Task.CompletedTask;
        });

    /// <summary>Se echa atras: se cierra la franja y en la base no se ha escrito nada.</summary>
    private void AlPulsarNoQuitarNada(object quien, RoutedEventArgs cuando)
        => ManejadorSeguro.Correr("No quitar nada", Servicios, () =>
        {
            EsconderLaFranjaDeQuitar();
            Acusar("No se quitó ningún caso.", "QUITAR");
            return Task.CompletedTask;
        });

    /// <summary>Ahora si: se le quitan todos, se dice cuantos y se vuelve a pintar lo que lleva.</summary>
    private void AlPulsarConfirmarElQuitar(object quien, RoutedEventArgs cuando)
        => ManejadorSeguro.Correr("Quitárselos ahora", Servicios, () =>
        {
            if (_reparto is null || _aQuien.SelectedItem is not Companero elegido) return Task.CompletedTask;

            var resumen = _reparto.QuitarleTodo(elegido.Id, elegido.Nombre);
            Acusar(resumen.Linea, "QUITAR");
            PintarLoQueLleva();
            return Task.CompletedTask;
        });

    /// <summary>Cierra la franja de quitar; se llama tambien al cambiar de companero.</summary>
    /// <remarks>
    /// Cerrarla al cambiar de companero no es cosmetica: una pregunta que dice «se le van a
    /// quitar 12 a Sandy» sobre un desplegable que ya pone otro nombre es la forma exacta de
    /// quitarle los casos a quien no era.
    /// </remarks>
    private void EsconderLaFranjaDeQuitar()
    {
        _zonaDeConfirmarElQuitar.Visibility = Visibility.Collapsed;
        _lineaDeLoQueSeQuita.Text = string.Empty;
    }

    /// <summary>Pide donde guardar el Excel del companero elegido y lo genera.</summary>
    private void AlPulsarGenerar(object quien, RoutedEventArgs cuando)
        => ManejadorSeguro.Correr("Generar el paquete…", Servicios, GenerarAsync);

    /// <summary>
    /// Abre el selector de guardado y escribe el paquete donde se diga.
    /// </summary>
    /// <remarks>
    /// El trabajo corre FUERA del hilo de la ventana. La cifra medida en
    /// <c>Fichas.Pruebas.Paquetes</c> con 3 000 filas es de segundos, y unos segundos con la
    /// ventana congelada se leen como «el programa se colgó».
    /// </remarks>
    private async Task GenerarAsync()
    {
        if (_ida is null || Servicios is null || _aQuien.SelectedItem is not Companero elegido) return;

        var ruta = ElegirDondeGuardar(NombreDeArchivo.DelPaquete(elegido.Nombre, Servicios.Reloj.Hoy()));
        if (ruta is null) return;

        _botonDeGenerar.IsEnabled = false;
        try
        {
            var resumen = await Task.Run(() => _ida.Generar(elegido, ruta)).ConfigureAwait(true);
            _resumenDeLaIda.Ensenar(resumen);
            Servicios.Avisos.Dejar(resumen.Avisos);
            GuardarQueArchivoSePuedeAbrir(resumen);
            Acusar(resumen.Linea, "PAQUETE");
        }
        finally
        {
            PintarLoQueLleva();
        }
    }

    // ---- la vuelta ----------------------------------------------------------

    /// <summary>Al elegir de quien viene el Excel se enciende el boton de elegir archivo.</summary>
    private void AlElegirDeQuienViene(object quien, SelectionChangedEventArgs cuando)
        => _botonDeLaVuelta.IsEnabled = _deQuienViene.SelectedItem is Companero;

    /// <summary>Pide el Excel devuelto, lo lee, aplica sus marcas y lo dice en una linea.</summary>
    private void AlPulsarElegirElExcel(object quien, RoutedEventArgs cuando)
        => ManejadorSeguro.Correr("Elegir el Excel devuelto…", Servicios, AplicarLaVueltaAsync);

    /// <summary>Abre el selector de archivo y aplica lo que traiga el Excel elegido.</summary>
    private async Task AplicarLaVueltaAsync()
    {
        if (_vuelta is null || _deQuienViene.SelectedItem is not Companero deQuien) return;

        var elegido = ElegirElExcelDevuelto();
        if (elegido is null) return;

        _botonDeLaVuelta.IsEnabled = false;
        try
        {
            var vuelta = await Task.Run(() => _vuelta.AplicarYRevisar(deQuien, elegido)).ConfigureAwait(true);
            var resumen = vuelta.Resumen;
            _resumenDeLaVuelta.Ensenar(resumen);
            Servicios?.Avisos.Dejar(resumen.Avisos);
            Acusar(resumen.Linea, "VUELTA");

            // Y aqui aparece lo que el dueno revisa. Se pinta DESDE el hilo de la ventana,
            // como los avisos, por el mismo fallo medido el 2026-09-04: tocar controles desde
            // el hilo de Task.Run revienta con RPC_E_WRONG_THREAD y la pantalla no dice nada.
            PintarLoQueTrajo(vuelta.LoQueTrajo);

            // Lo que entro cambia lo que lleva el companero en la otra mitad de la pantalla.
            PintarLoQueLleva();
        }
        finally
        {
            _botonDeLaVuelta.IsEnabled = _deQuienViene.SelectedItem is Companero;
        }
    }

    // ---- abrir --------------------------------------------------------------

    /// <summary>Abre el ultimo Excel generado con el programa que Windows tenga puesto.</summary>
    private void AlPulsarAbrir(object quien, RoutedEventArgs cuando)
        => ManejadorSeguro.Correr("Abrir el Excel", Servicios, () =>
        {
            DejarSiHayAviso(AbrirElArchivo.Abrir(_ultimoArchivo));
            return Task.CompletedTask;
        });

    /// <summary>Abre en el Explorador la carpeta donde quedo el ultimo Excel.</summary>
    private void AlPulsarAbrirLaCarpeta(object quien, RoutedEventArgs cuando)
        => ManejadorSeguro.Correr("Ver la carpeta", Servicios, () =>
        {
            DejarSiHayAviso(AbrirElArchivo.AbrirLaCarpeta(_ultimoArchivo));
            return Task.CompletedTask;
        });

    // ---- los dos resumenes --------------------------------------------------

    /// <summary>Abre o cierra el detalle de la ida.</summary>
    private void AlPulsarVerLaIda(object quien, RoutedEventArgs cuando) => _resumenDeLaIda.AlternarElDetalle();

    /// <summary>Cierra el resumen de la ida.</summary>
    private void AlPulsarCerrarLaIda(object quien, RoutedEventArgs cuando) => _resumenDeLaIda.Cerrar();

    /// <summary>Abre o cierra el detalle de la vuelta.</summary>
    private void AlPulsarVerLaVuelta(object quien, RoutedEventArgs cuando) => _resumenDeLaVuelta.AlternarElDetalle();

    /// <summary>Cierra el resumen de la vuelta.</summary>
    private void AlPulsarCerrarLaVuelta(object quien, RoutedEventArgs cuando) => _resumenDeLaVuelta.Cerrar();

    // ---- lo comun -----------------------------------------------------------

    /// <summary>
    /// El cuadro de «guardar como» de Windows, colgado de esta ventana.
    /// </summary>
    /// <remarks>
    /// Es el de <c>comdlg32</c> y no el de WinRT ni el del Windows App SDK; el motivo, con lo
    /// que se midio en esta maquina el 2026-09-04, esta escrito en
    /// <see cref="SelectorDeArchivos"/>.
    /// </remarks>
    private string? ElegirDondeGuardar(string nombrePropuesto)
        => PedirUnArchivo(
            "SELECTOR  se abre el de guardar el paquete",
            ventana => SelectorDeArchivos.DondeGuardar(
                ventana, "Dónde guardar el paquete", nombrePropuesto, "Excel", ".xlsx",
                Servicios?.Argumentos.CarpetaDeDatos));

    /// <summary>El cuadro de «abrir» para el Excel que devolvio el companero.</summary>
    private string? ElegirElExcelDevuelto()
        => PedirUnArchivo(
            "SELECTOR  se abre el de elegir el Excel devuelto",
            ventana => SelectorDeArchivos.CualAbrir(
                ventana, "El Excel que devolvió el compañero", "Excel", ".xlsx",
                Servicios?.Argumentos.CarpetaDeDatos));

    /// <summary>
    /// Abre el cuadro que se le diga y deja en el cuaderno que se abrio y que se eligio.
    /// </summary>
    /// <remarks>
    /// Las dos lineas del cuaderno no son andamio de prueba: son lo que faltaba el dia que QA
    /// encontro muertos los botones de Importar. Sin ellas, «no pasa nada al pulsar» y «se
    /// abrio y lo cerre sin querer» se leen igual desde fuera.
    /// </remarks>
    private string? PedirUnArchivo(string queSeAbre, Func<nint, string?> abrirElCuadro)
    {
        if (App.Ventana is null) return null;

        Servicios?.Registro.Anotar(queSeAbre);
        var elegido = abrirElCuadro(WinRT.Interop.WindowNative.GetWindowHandle(App.Ventana));
        Servicios?.Registro.Anotar(
            elegido is null ? "SELECTOR  se cerró sin elegir nada." : $"SELECTOR  se eligió «{elegido}»");

        return elegido;
    }

    /// <summary>Enciende «Abrir» y «Ver la carpeta» solo si de verdad hay archivo.</summary>
    private void GuardarQueArchivoSePuedeAbrir(ResumenEnPantalla resumen)
    {
        _ultimoArchivo = resumen.Ruta;
        _botonDeAbrir.IsEnabled = resumen.Ruta is not null;
        _botonDeLaCarpeta.IsEnabled = resumen.Ruta is not null;
    }

    /// <summary>Dice la linea en el pie y la anota en el cuaderno; NO abre ningun cuadro.</summary>
    private void Acusar(string linea, string queFue)
    {
        (App.Ventana as VentanaPrincipal)?.AcuseDelPie.Decir(linea);
        Servicios?.Registro.Anotar($"{queFue}  {linea}");
    }

    /// <summary>Deja el aviso en la franja si lo hay; si no lo hay, no molesta.</summary>
    private void DejarSiHayAviso(Aviso? aviso)
    {
        if (aviso is not null) Servicios?.Avisos.Dejar(aviso);
    }
}
