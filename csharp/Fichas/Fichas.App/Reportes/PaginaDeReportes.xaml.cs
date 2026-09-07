using Fichas.App.Cascara;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fichas.App.Reportes;

/// <summary>
/// La pantalla de Reportes: el informe en PDF para los jefes, y el historico de lo archivado.
/// </summary>
/// <remarks>
/// <para>Aqui no se decide nada: la pantalla coloca controles y pasa mensajes. Que periodo se
/// esta pidiendo lo calcula <see cref="PeriodoDeLaPantalla"/>, que sale en el PDF lo decide
/// <c>Fichas.Reportes</c> detras de <c>IReportes</c>, y lo que se dice al terminar lo escribe
/// <see cref="OperacionDeReporte"/>. Las tres se prueban sin abrir ventana.</para>
///
/// <para>⛔ Ni un cuadro modal (requisito 4): el resumen es UNA linea con «ver» y con X, y los
/// avisos van a la franja de la cascara.</para>
///
/// <para>⚠️ <b>Ningun manejador de esta pantalla es <c>async void</c>.</b> Todos pasan por
/// <see cref="ManejadorSeguro"/>, que existe por el fallo que QA midio el 2026-09-04 en la
/// pantalla de Importar: sus dos botones estaban muertos —sin selector, sin error y sin una
/// linea en <c>fichas.log</c>— porque la excepcion del selector no la recogia nadie.</para>
/// </remarks>
public sealed partial class PaginaDeReportes : PaginaDeFichas
{
    private readonly ZonaDeResumen _resumen;
    private readonly ZonaDeResumen _resumenDelAgente;
    private OperacionDeReporte? _operacion;
    private string? _ultimoArchivo;

    /// <summary>Monta la pantalla.</summary>
    public PaginaDeReportes()
    {
        InitializeComponent();
        _resumen = new ZonaDeResumen(_zonaDelResumen, _lineaDelResumen, _botonDeVer, _zonaDelDetalle, _textoDelDetalle);
        _resumenDelAgente = new ZonaDeResumen(
            _zonaDelAgente, _lineaDelAgente, _verDelAgente, _detalleDelAgente, _textoDelAgente);
    }

    /// <summary>Pone el periodo del mes en curso y lee el historico.</summary>
    protected override void AlLlegar()
    {
        if (Servicios is null) return;

        _operacion = new OperacionDeReporte(Servicios.Reportes);
        PonerElPeriodo(PeriodoDeLaPantalla.DelMesDe(Servicios.Reloj.Hoy()));
        LeerElHistorico();

        // Los ACTIVOS y los desactivados no: un compañero desactivado sigue teniendo trabajo
        // hecho detrás y su informe se puede pedir. Se ofrecen todos y se dice cuál está activo
        // por su nombre, que es lo que la lista de compañeros ya hace en las demás pantallas.
        var equipo = Servicios.Companeros
            .Listar(new FiltroDeCompaneros(SoloActivos: false), Pagina.Primera(int.MaxValue))
            .Elementos;
        _deQuienEsElInforme.ItemsSource = equipo;
        if (equipo.Count > 0) _deQuienEsElInforme.SelectedIndex = 0;
    }

    // ---- el informe por agente ----------------------------------------------

    /// <summary>Al elegir a quién se enciende el botón.</summary>
    private void AlElegirElAgente(object quien, SelectionChangedEventArgs cuando)
        => _botonDelInformeDeAgente.IsEnabled = _deQuienEsElInforme.SelectedItem is Companero;

    /// <summary>Pide dónde guardar el informe de ese agente y lo genera.</summary>
    private void AlPulsarGenerarElInformeDeAgente(object quien, RoutedEventArgs cuando)
        => ManejadorSeguro.Correr("Generar el informe del agente…", Servicios, () =>
        {
            if (_deQuienEsElInforme.SelectedItem is not Companero elegido) return Task.CompletedTask;

            var periodo = PeriodoPuesto();
            return GenerarAsync(
                NombreDeArchivo.DelInformeDeAgente(elegido.Nombre, periodo),
                ruta => _operacion!.DeUnAgente(elegido, periodo, ruta),
                _resumenDelAgente);
        });

    /// <summary>Abre o cierra el detalle del informe del agente.</summary>
    private void AlPulsarVerElAgente(object quien, RoutedEventArgs cuando) => _resumenDelAgente.AlternarElDetalle();

    /// <summary>Cierra el resumen del informe del agente.</summary>
    private void AlPulsarCerrarElAgente(object quien, RoutedEventArgs cuando) => _resumenDelAgente.Cerrar();

    // ---- el periodo ---------------------------------------------------------

    /// <summary>«Este mes», contado sobre el dia de hoy.</summary>
    private void AlPulsarEsteMes(object quien, RoutedEventArgs cuando)
        => PonerElPeriodo(PeriodoDeLaPantalla.DelMesDe(Hoy()));

    /// <summary>«Mes anterior», contado sobre el dia de hoy.</summary>
    private void AlPulsarMesAnterior(object quien, RoutedEventArgs cuando)
        => PonerElPeriodo(PeriodoDeLaPantalla.DelMesAnteriorA(Hoy()));

    /// <summary>«Últimos 90 días», con hoy dentro de los noventa.</summary>
    private void AlPulsarNoventaDias(object quien, RoutedEventArgs cuando)
        => PonerElPeriodo(PeriodoDeLaPantalla.DeLosUltimosDias(Hoy(), 90));

    /// <summary>Al teclear una fecha solo se repinta el titulo; no se valida nada aqui.</summary>
    private void AlEscribirUnaFecha(object quien, TextChangedEventArgs cuando) => PintarElTitulo();

    /// <summary>Escribe el periodo en las dos casillas y repinta el titulo.</summary>
    private void PonerElPeriodo(PeriodoDeLaPantalla periodo)
    {
        _desde.Text = periodo.Desde;
        _hasta.Text = periodo.Hasta;
        PintarElTitulo();
    }

    /// <summary>El periodo tal como esta escrito ahora mismo en las dos casillas.</summary>
    private PeriodoDeLaPantalla PeriodoPuesto()
        => new((_desde.Text ?? string.Empty).Trim(), (_hasta.Text ?? string.Empty).Trim());

    private void PintarElTitulo() => _tituloDelPeriodo.Text = "Período: " + PeriodoPuesto().EnTexto();

    private string Hoy() => Servicios?.Reloj.Hoy() ?? _desde.Text ?? string.Empty;

    // ---- generar ------------------------------------------------------------

    /// <summary>Pide donde guardar el informe del periodo y lo genera.</summary>
    private void AlPulsarGenerarElReporte(object quien, RoutedEventArgs cuando)
        => ManejadorSeguro.Correr("Generar el PDF…", Servicios, () =>
        {
            var periodo = PeriodoPuesto();
            return GenerarAsync(
                NombreDeArchivo.DelReporteDelPeriodo(periodo),
                ruta => _operacion!.DelPeriodo(periodo, ruta),
                _resumen);
        });

    /// <summary>Pide donde guardar el historico completo y lo genera.</summary>
    private void AlPulsarGenerarElHistorico(object quien, RoutedEventArgs cuando)
        => ManejadorSeguro.Correr("Generar el histórico en PDF…", Servicios, () => GenerarAsync(
            NombreDeArchivo.DelHistorico(Hoy()),
            ruta => _operacion!.Historico(ruta),
            _resumen));

    /// <summary>
    /// Abre el selector de guardado y genera el PDF donde se diga.
    /// </summary>
    /// <remarks>
    /// El trabajo de generar corre FUERA del hilo de la ventana. La cifra medida en
    /// <c>Fichas.Pruebas.Reportes</c> con 3 000 casos son segundos, y unos segundos con la
    /// ventana congelada se leen como «el programa se colgó».
    /// </remarks>
    /// <param name="donde">En qué bloque de la pantalla se enseña la línea del resultado.</param>
    private async Task GenerarAsync(
        string nombrePropuesto, Func<string, ResumenEnPantalla> generar, ZonaDeResumen donde)
    {
        if (_operacion is null) return;

        var ruta = ElegirDondeGuardar(nombrePropuesto, "PDF", ".pdf");
        if (ruta is null) return;

        BloquearLosBotones(true);
        try
        {
            var resumen = await Task.Run(() => generar(ruta)).ConfigureAwait(true);
            EnsenarElResumen(resumen, donde);
            LeerElHistorico();
        }
        finally
        {
            BloquearLosBotones(false);
        }
    }

    /// <summary>
    /// El cuadro de «guardar como» de Windows, colgado de esta ventana.
    /// </summary>
    /// <remarks>
    /// Es el de <c>comdlg32</c> y no el de WinRT ni el del Windows App SDK; el motivo, con lo
    /// que se midio en esta maquina, esta escrito en <see cref="SelectorDeArchivos"/>.
    /// <para>
    /// Las dos lineas del cuaderno no son andamio de prueba: son lo que faltaba el dia que QA
    /// encontro muertos los botones de Importar. Sin ellas, «no pasa nada al pulsar» y «se
    /// abrio y lo cerre sin querer» se leen igual desde fuera.
    /// </para>
    /// </remarks>
    private string? ElegirDondeGuardar(string nombrePropuesto, string queEs, string extension)
    {
        if (App.Ventana is null) return null;

        Servicios?.Registro.Anotar($"SELECTOR  se abre el de guardar, propuesto «{nombrePropuesto}»");
        // Empieza en la carpeta de datos, como hacia el programa viejo: los reportes viven al
        // lado de la base, y quien los busca una semana despues los busca ahi.
        var elegido = SelectorDeArchivos.DondeGuardar(
            WinRT.Interop.WindowNative.GetWindowHandle(App.Ventana),
            "Dónde guardar el reporte",
            nombrePropuesto,
            queEs,
            extension,
            Servicios?.Argumentos.CarpetaDeDatos);
        Servicios?.Registro.Anotar(
            elegido is null ? "SELECTOR  se cerró sin elegir nada." : $"SELECTOR  se eligió «{elegido}»");

        return elegido;
    }

    // ---- abrir --------------------------------------------------------------

    /// <summary>Abre el ultimo PDF generado con el programa que Windows tenga puesto.</summary>
    private void AlPulsarAbrir(object quien, RoutedEventArgs cuando)
        => ManejadorSeguro.Correr("Abrir el PDF", Servicios, () =>
        {
            DejarSiHayAviso(AbrirElArchivo.Abrir(_ultimoArchivo));
            return Task.CompletedTask;
        });

    /// <summary>Abre en el Explorador la carpeta donde quedo el ultimo PDF.</summary>
    private void AlPulsarAbrirLaCarpeta(object quien, RoutedEventArgs cuando)
        => ManejadorSeguro.Correr("Ver la carpeta", Servicios, () =>
        {
            DejarSiHayAviso(AbrirElArchivo.AbrirLaCarpeta(_ultimoArchivo));
            return Task.CompletedTask;
        });

    // ---- el historico -------------------------------------------------------

    /// <summary>Vuelve a leer el historico de la base.</summary>
    private void AlPulsarRefrescar(object quien, RoutedEventArgs cuando)
        => ManejadorSeguro.Correr("Volver a leer", Servicios, () =>
        {
            LeerElHistorico();
            return Task.CompletedTask;
        });

    /// <summary>Lee lo archivado y lo pinta, diciendo cuantos se ensenan de cuantos hay.</summary>
    private void LeerElHistorico()
    {
        if (Servicios is null) return;

        var historico = ListaDeArchivados.Leer(Servicios.Casos);
        _renglonesDelHistorico.ItemsSource = historico.Renglones;

        _lineaDelHistorico.Text = historico.CuantosHayArchivados switch
        {
            0 => "Todavía no hay ningún caso archivado.",
            _ when historico.SeQuedaronFuera =>
                $"{historico.CuantosHayArchivados} casos archivados · se enseñan los "
                + $"{historico.Renglones.Count} primeros · siguen contando en los reportes",
            1 => "1 caso archivado · sigue contando en los reportes",
            var cuantos => $"{cuantos} casos archivados · siguen contando en los reportes",
        };
    }

    // ---- el resumen de una linea --------------------------------------------

    /// <summary>Abre o cierra el detalle. NO es un cuadro modal: se abre aqui debajo.</summary>
    private void AlPulsarVer(object quien, RoutedEventArgs cuando) => _resumen.AlternarElDetalle();

    /// <summary>Cierra el resumen entero con la X.</summary>
    private void AlPulsarCerrarElResumen(object quien, RoutedEventArgs cuando) => _resumen.Cerrar();

    /// <summary>
    /// Deja la linea en la pantalla, los avisos en la franja y el acuse en el pie.
    /// </summary>
    /// <remarks>
    /// ⚠️ Los avisos se dejan AQUI y no dentro de la operacion: esto corre en el hilo de la
    /// ventana y la operacion corre fuera. Dejarlos desde fuera hace que la franja se repinte
    /// desde otro hilo y revienta con <c>COMException 0x8001010E</c> —medido el 2026-09-04, con
    /// el PDF ya escrito y la pantalla sin decir nada—.
    /// </remarks>
    /// <param name="resumen">Lo que dejo la operacion.</param>
    /// <param name="donde">
    /// En que bloque se pinta. Cada accion tiene el suyo y no se pisan: generar el informe de un
    /// agente no puede borrar la linea que dejo el informe de los jefes tres pulsaciones antes.
    /// </param>
    private void EnsenarElResumen(ResumenEnPantalla resumen, ZonaDeResumen donde)
    {
        donde.Ensenar(resumen);
        Servicios?.Avisos.Dejar(resumen.Avisos);

        _ultimoArchivo = resumen.Ruta;
        _botonDeAbrir.IsEnabled = resumen.Ruta is not null;
        _botonDeLaCarpeta.IsEnabled = resumen.Ruta is not null;

        (App.Ventana as VentanaPrincipal)?.AcuseDelPie.Decir(resumen.Linea);
        Servicios?.Registro.Anotar($"REPORTE  {resumen.Linea}");
    }

    /// <summary>Deja el aviso en la franja si lo hay; si no lo hay, no molesta.</summary>
    private void DejarSiHayAviso(Aviso? aviso)
    {
        if (aviso is not null) Servicios?.Avisos.Dejar(aviso);
    }

    /// <summary>Apaga los botones mientras se genera; una segunda pulsada no arranca otra tanda.</summary>
    private void BloquearLosBotones(bool bloqueados)
    {
        _botonDeGenerar.IsEnabled = !bloqueados;
        _botonDelHistorico.IsEnabled = !bloqueados;
        _botonDeRefrescar.IsEnabled = !bloqueados;
        _botonDelInformeDeAgente.IsEnabled = !bloqueados && _deQuienEsElInforme.SelectedItem is Companero;
    }
}
