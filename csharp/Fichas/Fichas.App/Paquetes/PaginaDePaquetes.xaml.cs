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
    /// <summary>El bloque de resumen del paquete que va.</summary>
    private readonly ZonaDeResumen _resumenDeLaIda;
    /// <summary>El bloque de resumen del Excel que vuelve.</summary>
    private readonly ZonaDeResumen _resumenDeLaVuelta;
    /// <summary>El bloque de resumen de la segunda vuelta.</summary>
    private readonly ZonaDeResumen _resumenDeLaSubida;

    /// <summary>La operación de generar el paquete; nula hasta que llegan los servicios.</summary>
    private OperacionDelPaquete? _ida;
    /// <summary>La operación de aplicar el Excel devuelto, con limpieza; nula hasta que llegan los servicios.</summary>
    private OperacionDeLaVuelta? _vuelta;
    /// <summary>La operación de la segunda vuelta; nula hasta que llegan los servicios.</summary>
    private OperacionDeLaSegundaVuelta? _subida;
    /// <summary>La única puerta de asignar y retirar, la misma de Asignar y Revisar; nula hasta que llegan los servicios.</summary>
    private OperacionDeAsignar? _reparto;
    /// <summary>El último Excel generado, que es lo que abren «Abrir» y «Ver la carpeta»; nulo si todavía no hay ninguno.</summary>
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

        // La MISMA puerta de asignar que usan Asignar y Revisar, no una copia: quitarle los
        // casos a alguien tiene que dejar la base igual que quitarselos uno a uno.
        _reparto = new OperacionDeAsignar(Servicios.Asignaciones, Servicios.Reloj, Servicios.Avisos);

        // Con la limpieza, no solo con los cuatro puertos. Desde el 2026-09-06 la vuelta ensena
        // lo que trajo para que el dueno lo de por bueno —eso sale de los casos y las personas—
        // y desde el 2026-09-07 ademas le quita al agente lo que devolvio completo: «debe
        // quitarle que ese caso esta asignado a el; debe quedar limpio».
        _vuelta = new OperacionDeLaVuelta(
            Servicios.Paquetes, Servicios.Ilegibles, Servicios.Casos, Servicios.Personas,
            new LimpiezaAlVolver(Servicios.Asignaciones, Servicios.Casos, _reparto));
        _firma = new FirmaEnBloque(Servicios.Procedencia, Servicios.Reloj);
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
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
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
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
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
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarVerLaSubida(object quien, RoutedEventArgs cuando) => _resumenDeLaSubida.AlternarElDetalle();

    /// <summary>Cierra el resumen de la segunda vuelta.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarCerrarLaSubida(object quien, RoutedEventArgs cuando) => _resumenDeLaSubida.Cerrar();

    // ---- la ida -------------------------------------------------------------

    /// <summary>Al elegir companero se lee lo que lleva y se enciende el boton.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
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
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
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
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarNoQuitarNada(object quien, RoutedEventArgs cuando)
        => ManejadorSeguro.Correr("No quitar nada", Servicios, () =>
        {
            EsconderLaFranjaDeQuitar();
            Acusar("No se quitó ningún caso.", "QUITAR");
            return Task.CompletedTask;
        });

    /// <summary>Ahora si: se le quitan todos, se dice cuantos y se vuelve a pintar lo que lleva.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
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
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
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
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlElegirDeQuienViene(object quien, SelectionChangedEventArgs cuando)
        => _botonDeLaVuelta.IsEnabled = _deQuienViene.SelectedItem is Companero;

    /// <summary>Pide el Excel devuelto, lo lee, aplica sus marcas y lo dice en una linea.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
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

            // Lo que se le quitó se dice en su propio acuse y no pegado a la línea de la vuelta:
            // la del pie tiene tope medido de 160 caracteres, y la cifra que le importa al dueño
            // —cuántas filas entraron— no puede quedar cortada por una frase de después.
            if (vuelta.Limpieza.Retirados > 0 || vuelta.Limpieza.NoSePudieron > 0)
                Acusar(vuelta.Limpieza.Linea, "LIMPIEZA");

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
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarAbrir(object quien, RoutedEventArgs cuando)
        => ManejadorSeguro.Correr("Abrir el Excel", Servicios, () =>
        {
            DejarSiHayAviso(AbrirElArchivo.Abrir(_ultimoArchivo));
            return Task.CompletedTask;
        });

    /// <summary>Abre en el Explorador la carpeta donde quedo el ultimo Excel.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarAbrirLaCarpeta(object quien, RoutedEventArgs cuando)
        => ManejadorSeguro.Correr("Ver la carpeta", Servicios, () =>
        {
            DejarSiHayAviso(AbrirElArchivo.AbrirLaCarpeta(_ultimoArchivo));
            return Task.CompletedTask;
        });

    // ---- los dos resumenes --------------------------------------------------

    /// <summary>Abre o cierra el detalle de la ida.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarVerLaIda(object quien, RoutedEventArgs cuando) => _resumenDeLaIda.AlternarElDetalle();

    /// <summary>Cierra el resumen de la ida.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarCerrarLaIda(object quien, RoutedEventArgs cuando) => _resumenDeLaIda.Cerrar();

    /// <summary>Abre o cierra el detalle de la vuelta.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarVerLaVuelta(object quien, RoutedEventArgs cuando) => _resumenDeLaVuelta.AlternarElDetalle();

    /// <summary>Cierra el resumen de la vuelta.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
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
    /// <param name="nombrePropuesto">El nombre de archivo que el cuadro trae puesto.</param>
    /// <returns>La ruta elegida, o nulo si se cerró sin elegir.</returns>
    private string? ElegirDondeGuardar(string nombrePropuesto)
        => PedirUnArchivo(
            "SELECTOR  se abre el de guardar el paquete",
            ventana => SelectorDeArchivos.DondeGuardar(
                ventana, "Dónde guardar el paquete", nombrePropuesto, "Excel", ".xlsx",
                Servicios?.Argumentos.CarpetaDeDatos));

    /// <summary>El cuadro de «abrir» para el Excel que devolvio el companero.</summary>
    /// <returns>La ruta elegida, o nulo si se cerró sin elegir.</returns>
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
    /// <param name="queSeAbre">La línea que se anota en el cuaderno antes de abrir el cuadro.</param>
    /// <param name="abrirElCuadro">Quien abre el cuadro sobre el asa de la ventana y devuelve lo elegido.</param>
    /// <returns>Lo elegido; nulo si se cerró sin elegir o si no hay ventana principal.</returns>
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
    /// <param name="resumen">El resumen de la operación; su <c>Ruta</c> es nula cuando no se escribió nada.</param>
    private void GuardarQueArchivoSePuedeAbrir(ResumenEnPantalla resumen)
    {
        _ultimoArchivo = resumen.Ruta;
        _botonDeAbrir.IsEnabled = resumen.Ruta is not null;
        _botonDeLaCarpeta.IsEnabled = resumen.Ruta is not null;
    }

    /// <summary>Dice la linea en el pie y la anota en el cuaderno; NO abre ningun cuadro.</summary>
    /// <param name="linea">Lo que se dice.</param>
    /// <param name="queFue">El rótulo del cuaderno: «PAQUETE», «VUELTA», «QUITAR»…</param>
    private void Acusar(string linea, string queFue)
    {
        (App.Ventana as VentanaPrincipal)?.AcuseDelPie.Decir(linea);
        Servicios?.Registro.Anotar($"{queFue}  {linea}");
    }

    /// <summary>Deja el aviso en la franja si lo hay; si no lo hay, no molesta.</summary>
    /// <param name="aviso">El aviso, o nulo cuando la acción salió bien.</param>
    private void DejarSiHayAviso(Aviso? aviso)
    {
        if (aviso is not null) Servicios?.Avisos.Dejar(aviso);
    }
}
