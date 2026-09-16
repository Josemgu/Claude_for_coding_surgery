using System.Runtime.InteropServices;
using Fichas.App.Asignar;
using Fichas.App.Cascara;
using Fichas.App.Grupo;
using Fichas.Reportes.Reglas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Fichas.App.Revisar;

/// <summary>
/// La pantalla de revisar: una tarjeta por documento, los seis tableros y archivar en lote.
/// </summary>
/// <remarks>
/// ⛔ Terreno del programador de Asignar y Revisar (fase C5). Nadie mas escribe aqui.
///
/// Las reglas viven en <see cref="TableroDeRevisar"/> y en <see cref="AccionesDeRevisar"/>,
/// que se prueban sin ventana; asignar sale por <see cref="OperacionDeAsignar"/>, la misma
/// que usa la pantalla de Asignar. Aqui solo se colocan controles y se pasan mensajes.
/// </remarks>
public sealed partial class PaginaDeRevisar : PaginaDeFichas
{
    /// <summary>La regla de qué tarjetas hay y en qué tablero; nula hasta que llegan los servicios.</summary>
    private TableroDeRevisar? _tablero;
    /// <summary>Lo que esta pantalla escribe: archivar, marcar a mano, poner la fecha; nula hasta <see cref="AlLlegar"/>.</summary>
    private AccionesDeRevisar? _acciones;
    /// <summary>La única puerta de asignar y retirar del programa, la misma que usa Asignar; nula hasta <see cref="AlLlegar"/>.</summary>
    private OperacionDeAsignar? _asignar;
    /// <summary>La única puerta de borrar del programa; nula hasta <see cref="AlLlegar"/>.</summary>
    private OperacionDeBorrar? _borrar;
    /// <summary>
    /// El tablero que se esta mirando. Nace en el que el dueno pidio ver primero.
    /// </summary>
    /// <remarks>
    /// ⛔ El valor NO se escribe aqui: lo dice <see cref="TableroDeRevisar.ElTableroConElQueSeAbre"/>,
    /// con el motivo y las palabras del dueno del 2026-09-06 —<i>«solo los que necesitan
    /// revisión son los que deben ser revisados, no todos»</i>—. Escrito a mano en este
    /// campo privado estuvo en «Todo» sin que ninguna prueba pudiera decirlo.
    /// </remarks>
    private FiltroDeTarjeta _tableroALaVista = TableroDeRevisar.ElTableroConElQueSeAbre;

    /// <summary>Las carpetas de ahora mismo: mes → fecha de viaje → unidad.</summary>
    private IReadOnlyList<GrupoDeMes> _carpetas = [];

    /// <summary>De cada rama del arbol, que documentos cuelgan de ella y de que mes es.</summary>
    /// <remarks>
    /// El arbol se pinta en modo suelto —nodos con su texto dentro— porque es el unico modo
    /// de <c>TreeView</c> que no depende de que una plantilla resuelva el enlace en tiempo
    /// de ejecucion. Lo que el nodo NO puede llevar dentro se guarda aqui al lado.
    /// </remarks>
    private readonly Dictionary<TreeViewNode, CarpetaDelArbol> _ramas = [];

    /// <summary>La carpeta elegida, o nula cuando se estan viendo todos los documentos.</summary>
    private CarpetaDelArbol? _carpetaALaVista;

    /// <summary>Si ya hay un cuadro de borrar abierto; dos a la vez tumban la ventana.</summary>
    private bool _hayUnCuadroAbierto;

    /// <summary>Monta la pantalla y ata Ctrl+A a marcar todo lo que se ve.</summary>
    public PaginaDeRevisar()
    {
        InitializeComponent();
        _rejilla.SelectionChanged += AlCambiarLaSeleccion;
        var ctrlA = new KeyboardAccelerator { Key = VirtualKey.A, Modifiers = VirtualKeyModifiers.Control };
        ctrlA.Invoked += AlPulsarMarcarTodo;
        KeyboardAccelerators.Add(ctrlA);
    }

    /// <summary>Lee la base, pinta las pastillas y las tarjetas, y avisa de las fechas pasadas.</summary>
    protected override void AlLlegar()
    {
        if (Servicios is null) return;

        // Con el puerto de personas desde el 2026-09-16: la tarjeta a medias y los nombres salen de ahi.
        _tablero = new TableroDeRevisar(
            Servicios.Casos, Servicios.Asignaciones, Servicios.Companeros, Servicios.Reloj, Servicios.Personas);
        _asignar = new OperacionDeAsignar(Servicios.Asignaciones, Servicios.Reloj, Servicios.Avisos);
        // Archivar quita la asignacion por la MISMA puerta que asigna y retira esta pantalla
        // (2026-09-11): si fueran dos operaciones, sus avisos caerian en franjas distintas.
        _acciones = new AccionesDeRevisar(
            Servicios.Casos, Servicios.Reloj, Servicios.Avisos,
            new RetiradaAlArchivar(Servicios.Asignaciones, Servicios.Casos, _asignar));
        _borrar = new OperacionDeBorrar(Servicios.Mantenimiento, Servicios.Avisos, Servicios.Registro);

        // Con datos inventados no hay base que copiar, asi que no hay nada que borrar. El
        // boton se apaga y el porque sale al pulsarlo, no en una franja al entrar: una
        // pantalla no puede saludar con un problema que nadie ha provocado todavia.
        _botonDeEmpezarDeCero.IsEnabled = _borrar.SePuedeBorrarAqui;

        MontarLoDeLosDuplicados();
        MontarElAnchoDelPanel();
        Repintar();
        AvisarDeLasFechasPasadas();
    }

    /// <summary>
    /// Vuelve a leer la base y a pintar pastillas, carpetas y tarjetas.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Los archivados NO entran</b> salvo que la casilla «Ver los archivados» esté
    /// marcada: <c>conArchivados</c> se pasa con lo que diga la casilla, y por defecto está
    /// apagada. Del dueno, 2026-09-05: «cuando yo archive, debe salir del sistema visible, pero se
    /// queda como historico para los reportes», y el 2026-09-06 lo cerró: «debe pasar a
    /// archivado y no aparecer más en ningún lado». Archivar sigue sin borrar: el caso entero
    /// se queda en la base, cuenta en los reportes y sale en el historico; lo unico que
    /// cambia es que deja de estorbar aqui. La casilla existe porque para desarchivar algo
    /// hay que poder verlo primero.
    /// </remarks>
    private void Repintar()
    {
        if (_tablero is null) return;

        _tablero.Cargar(_buscador.Text ?? string.Empty, conArchivados: _verLosArchivados.IsChecked == true);
        PintarLasPastillas(_tablero.Cuentas());
        PintarLasCarpetas();
        PintarLasTarjetas();
    }

    /// <summary>Pinta una pastilla por tablero, con su cifra dentro (mockup v2).</summary>
    /// <param name="cuentas">Cuántas tarjetas hay en cada tablero, tal como las cuenta <see cref="TableroDeRevisar.Cuentas"/>.</param>
    private void PintarLasPastillas(IReadOnlyDictionary<FiltroDeTarjeta, int> cuentas)
    {
        _pastillas.Children.Clear();
        foreach (var filtro in Enum.GetValues<FiltroDeTarjeta>())
        {
            var pastilla = new ToggleButton
            {
                Content = $"{TableroDeRevisar.NombreDe(filtro)}  {cuentas[filtro]}",
                IsChecked = filtro == _tableroALaVista,
                Tag = filtro,
                FontSize = 13,
            };
            pastilla.Click += AlElegirUnTablero;
            _pastillas.Children.Add(pastilla);
        }
    }

    /// <summary>
    /// Cambia de tablero sin volver a leer la base: las tarjetas ya estan cargadas.
    /// </summary>
    /// <remarks>
    /// El arbol de carpetas se rehace, y no es un capricho: las carpetas cuentan lo que hay
    /// EN EL TABLERO que se esta mirando. Si no se rehiciera, las cifras de las carpetas
    /// serian las de otro tablero y no cuadrarian con las tarjetas de al lado.
    /// </remarks>
    /// <param name="quien">La pastilla pulsada; lleva en <c>Tag</c> su tablero.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlElegirUnTablero(object quien, RoutedEventArgs cuando)
    {
        if (_tablero is null || quien is not ToggleButton pastilla || pastilla.Tag is not FiltroDeTarjeta filtro) return;

        _tableroALaVista = filtro;
        foreach (var otra in _pastillas.Children.OfType<ToggleButton>())
            otra.IsChecked = ReferenceEquals(otra, pastilla);

        PintarLasCarpetas();
        PintarLasTarjetas();
    }

    /// <summary>
    /// Deja UNA linea en la franja si hay documentos con la fecha de viaje ya pasada.
    /// </summary>
    /// <remarks>
    /// Del dueno: «si hay documento que se sube y ya paso la fecha debe decir: esta fecha ya
    /// paso, revisar, ¿quieres archivar o completar?». Se dice en una linea con «ver», no en
    /// un cuadro: la pantalla sigue usandose mientras esta puesta.
    /// </remarks>
    private void AvisarDeLasFechasPasadas()
    {
        if (_tablero is null || Servicios is null) return;

        var cuantos = _tablero.CuantasEn(FiltroDeTarjeta.FechaPasada);
        if (cuantos == 0) return;

        var linea = Plural.Con(cuantos, "documento", "documentos")
            + " con la fecha de viaje ya pasada: revisa si archivar o completar.";

        // Los avisos iguales se agrupan (regla de interfaz del 2026-09-03): entrar dos veces
        // en esta pantalla no puede dejar dos franjas diciendo lo mismo.
        if (Servicios.Avisos.Pendientes.Any(a => a.Linea == linea)) return;

        Servicios.Avisos.Dejar(Aviso.Advierte(
            linea,
            nameof(Caso.FechaViaje),
            "Están en el tablero «Fecha pasada». En cada tarjeta, «Resuelto» los deja "
            + "contando en los reportes, y «Archivar los marcados» los cierra sin borrarlos."));
    }

    /// <summary>Con los archivados fuera de la vista, se vuelve a mirar todo desde cero.</summary>
    /// <param name="quien">La casilla «Ver los archivados».</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlCambiarSiSeVenLosArchivados(object quien, RoutedEventArgs cuando) => Repintar();

    /// <summary>
    /// Dice en una linea cuantas tarjetas hay marcadas.
    /// </summary>
    /// <remarks>
    /// Desarchivar solo se enciende con los archivados a la vista: con ellos escondidos, lo
    /// marcado no puede estar archivado, y el boton solo serviria para volver a escribir un
    /// cero donde ya habia un cero.
    /// </remarks>
    private void ContarLoMarcado()
    {
        var cuantas = _rejilla.SelectedItems.Count;
        _botonDeArchivar.IsEnabled = cuantas > 0;
        _botonDeDesarchivar.IsEnabled = cuantas > 0 && _verLosArchivados.IsChecked == true;
        _botonDeBorrar.IsEnabled = cuantas > 0 && _borrar?.SePuedeBorrarAqui == true;
        _estadoDeLaSeleccion.Text = cuantas == 0
            ? "Ctrl+A marca todo lo que se ve"
            : Plural.Con(cuantas, "marcado", "marcados");
    }

    /// <summary>Archiva de golpe lo marcado. Archivar no borra: el caso sigue contando.</summary>
    /// <param name="quien">El botón de archivar.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarArchivar(object quien, RoutedEventArgs cuando)
    {
        if (_acciones is null) return;
        var casos = CasosMarcados();
        if (casos.Count == 0) return;

        Acusar(_acciones.ArchivarEnLote(casos).Linea);
        Repintar();
    }

    /// <summary>Desarchiva de golpe lo marcado; es la vuelta atras de archivar.</summary>
    /// <param name="quien">El botón de desarchivar.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarDesarchivar(object quien, RoutedEventArgs cuando)
    {
        if (_acciones is null) return;
        var casos = CasosMarcados();
        if (casos.Count == 0) return;

        Acusar(_acciones.DesarchivarEnLote(casos).Linea);
        Repintar();
    }

    /// <summary>
    /// Borra de la base los documentos marcados. Es la unica accion de esta pantalla que
    /// pregunta antes, y la unica que no se puede deshacer.
    /// </summary>
    /// <param name="quien">El botón de borrar los marcados.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private async void AlPulsarBorrar(object quien, RoutedEventArgs cuando)
    {
        var casos = CasosMarcados();
        if (casos.Count == 0) return;
        await PreguntarYBorrar(mantenimiento => mantenimiento.PlanearDocumentos(casos));
    }

    /// <summary>Deja la base sin ningun documento; los companeros se quedan.</summary>
    /// <param name="quien">El botón de empezar de cero.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private async void AlPulsarEmpezarDeCero(object quien, RoutedEventArgs cuando)
        => await PreguntarYBorrar(mantenimiento => mantenimiento.PlanearEmpezarDeCero());

    /// <summary>
    /// El camino comun de las dos: copiar, preguntar, borrar, decirlo y repintar.
    /// </summary>
    /// <remarks>
    /// El cerrojo no es un adorno: dos pulsaciones seguidas levantan dos cuadros sobre la
    /// misma raiz visual y eso tumba la ventana sin dejar linea en <c>fichas.log</c>, que
    /// es el mismo tipo de fallo que se midio el 2026-09-04 al repintar con tarjetas
    /// todavia marcadas.
    /// </remarks>
    /// <param name="planear">Qué plan se le pide al puerto: unos documentos o todo.</param>
    private async Task PreguntarYBorrar(Func<IMantenimiento, PlanDeBorrado> planear)
    {
        if (_borrar is null || _hayUnCuadroAbierto) return;

        _hayUnCuadroAbierto = true;
        try
        {
            var linea = await _borrar.PreguntarYBorrar(planear, XamlRoot);
            if (linea is not null) Acusar(linea);
        }
        catch (Exception fallo) when (fallo is InvalidOperationException or COMException)
        {
            // No se silencia: se DICE. Es lo que lanza WinUI cuando ya hay un cuadro
            // levantado o cuando la raiz visual se fue mientras se preguntaba.
            Servicios?.Avisos.Dejar(Aviso.Problema(
                "No se pudo abrir la pregunta de borrar, así que no se borró nada.",
                string.Empty,
                $"Detalle: {fallo.Message}. Cierra cualquier otro cuadro abierto y vuelve a intentarlo."));
        }
        finally
        {
            _hayUnCuadroAbierto = false;
        }

        Repintar();
    }

    /// <summary>«Si, completa»: lo marca a mano, con la firma de quien lo marca.</summary>
    /// <param name="quien">El botón «Sí, completa»; lleva en <c>Tag</c> el número interno de su documento.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlPulsarSiCompleta(object quien, RoutedEventArgs cuando)
        => MarcarAMano(quien, EstadoDeRecomendacion.Completa);

    /// <summary>
    /// «No esta completa»: abre el menu con los tres estados que el dueno nombro.
    /// </summary>
    /// <remarks>
    /// Es un menu ligero y no un cuadro, igual que el de asignar: se cierra pulsando fuera y
    /// no detiene la pantalla (requisito 9). Las tres entradas y sus palabras salen de
    /// <see cref="AccionesDeRevisar.LosTresQuePidioElDueno"/>, no se escriben aqui: la lista
    /// del menu y lo que se guarda tienen que ser la misma lista o se separan.
    /// </remarks>
    /// <param name="quien">El botón «No está completa»; lleva en <c>Tag</c> el número interno de su documento.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlAbrirElMenuDeNoCompleta(object quien, RoutedEventArgs cuando)
    {
        if (quien is not Button boton || boton.Tag is not long casoId) return;

        var menu = new MenuFlyout();
        foreach (var motivo in AccionesDeRevisar.LosTresQuePidioElDueno)
        {
            var entrada = new MenuFlyoutItem { Text = AccionesDeRevisar.DecirLaOpcion(motivo) };
            var elegido = motivo;
            entrada.Click += (_, _) => MarcarAMano(casoId, EstadoDeRecomendacion.NoCompleta, elegido);
            menu.Items.Add(entrada);
        }

        menu.ShowAt(boton);
    }

    /// <summary>Lo que hace el boton de «Si, completa»: marcar sin motivo, que es lo que es.</summary>
    /// <param name="quien">El botón pulsado; lleva en <c>Tag</c> el número interno de su documento.</param>
    /// <param name="estado">Lo que se marca.</param>
    private void MarcarAMano(object quien, EstadoDeRecomendacion estado)
    {
        if (quien is not FrameworkElement boton || boton.Tag is not long casoId) return;
        MarcarAMano(casoId, estado, MotivoDeNoCompletar.SinMotivo);
    }

    /// <summary>
    /// Escribe a mano el estado de un documento y su motivo, con el nombre de quien lo marca.
    /// </summary>
    /// <remarks>
    /// ⚠️ Esto NO es la firma de campos de Miguel («Todo correcto»), que sigue siendo suya y
    /// nunca automatica. Es el mismo campo que escribe el Excel del companero, y por eso la
    /// tarjeta dice de donde vino la marca (CLAUDE.md §1.5: son dos cosas y no se mezclan).
    /// </remarks>
    /// <param name="casoId">El documento que se marca.</param>
    /// <param name="estado">Lo que se dice de la recomendación.</param>
    /// <param name="motivo">Por qué no está completa; <c>SinMotivo</c> con «completa».</param>
    private void MarcarAMano(long casoId, EstadoDeRecomendacion estado, MotivoDeNoCompletar motivo)
    {
        if (_acciones is null || Servicios is null) return;

        var firma = AccionesDeRevisar.QuienFirmaAMano(Servicios.Companeros.Activos());
        if (firma is null)
        {
            Servicios.Avisos.Dejar(Aviso.Problema(
                "No hay ningún compañero activo que pueda firmar esta marca.",
                nameof(Caso.EstadoMarcadoPor),
                "El estado se escribe siempre con el nombre de quien lo pone, y aquí no hay "
                + "a quien ponerle. Da de alta un compañero en el equipo y vuelve a marcarlo."));
            return;
        }

        var resultado = _acciones.MarcarAMano(casoId, estado, motivo, firma.Id);
        if (resultado.SeEscribio)
        {
            var porque = motivo == MotivoDeNoCompletar.SinMotivo
                ? string.Empty
                : $": {PalabrasDelEstado.DecirElMotivo(motivo)}";
            Acusar($"Marcado «{RenglonParaAsignar.PalabraDe(estado)}{porque}» por {firma.Nombre}.");
        }

        Repintar();
    }

    /// <summary>
    /// Abre el menu de asignar de una tarjeta. Es un menu ligero, no un cuadro: se cierra
    /// pulsando fuera y no detiene nada (requisito 9).
    /// </summary>
    /// <param name="quien">El botón de asignar; lleva en <c>Tag</c> el número interno de su documento.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlAbrirElMenuDeAsignar(object quien, RoutedEventArgs cuando)
    {
        if (_asignar is null || Servicios is null || quien is not Button boton) return;
        if (boton.Tag is not long casoId) return;

        var menu = new MenuFlyout();
        var sinAsignar = new MenuFlyoutItem { Text = "Sin asignar (retirar)" };
        sinAsignar.Click += (_, _) => RetirarDelCaso(casoId);
        menu.Items.Add(sinAsignar);
        menu.Items.Add(new MenuFlyoutSeparator());

        // Solo los ACTIVOS: es la unica condicion que queda para asignar (requisito 8).
        foreach (var companero in Servicios.Companeros.Activos())
        {
            var entrada = new MenuFlyoutItem { Text = companero.Nombre };
            var destino = companero;
            entrada.Click += (_, _) => AsignarElCaso(casoId, destino);
            menu.Items.Add(entrada);
        }

        menu.ShowAt(boton);
    }

    /// <summary>Asigna desde la tarjeta, por la MISMA operacion que usa la lista de Asignar.</summary>
    /// <param name="casoId">El documento que se asigna.</param>
    /// <param name="destino">El compañero que lo va a llevar.</param>
    private void AsignarElCaso(long casoId, Companero destino)
    {
        if (_asignar is null) return;
        if (_asignar.Asignar(casoId, destino.Id).SeEscribio) Acusar($"Asignado a {destino.Nombre}.");
        Repintar();
    }

    /// <summary>Retira de la tarjeta: desactiva las asignaciones vivas, nunca las borra.</summary>
    /// <param name="casoId">El documento al que se le quita quien lo lleva.</param>
    private void RetirarDelCaso(long casoId)
    {
        if (_asignar is null) return;
        if (_asignar.RetirarDelCaso(casoId).SeEscribio) Acusar("Retirado; la fila de quien lo llevaba se conserva.");
        Repintar();
    }

    /// <summary>Los numeros internos de los documentos marcados ahora mismo.</summary>
    private List<long> CasosMarcados()
        => _rejilla.SelectedItems.OfType<TarjetaDeDocumento>().Select(t => t.CasoId).ToList();

    /// <summary>Ctrl+A: marca todo lo que hay a la vista; si ya estaba todo, lo desmarca.</summary>
    /// <param name="quien">El atajo Ctrl+A.</param>
    /// <param name="cuando">Los datos del atajo; se marca como atendido para que no siga subiendo.</param>
    private void AlPulsarMarcarTodo(KeyboardAccelerator quien, KeyboardAcceleratorInvokedEventArgs cuando)
    {
        cuando.Handled = true;
        if (_rejilla.ItemsSource is not IReadOnlyList<TarjetaDeDocumento> tarjetas) return;
        if (MarcarTodo.HayQueMarcar(_rejilla.SelectedItems.Count, tarjetas.Count)) _rejilla.SelectAll();
        else _rejilla.DeselectAll();
    }

    /// <summary>Al cambiar lo marcado solo se recuenta; la rejilla no se reconstruye.</summary>
    /// <param name="quien">La rejilla de tarjetas.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlCambiarLaSeleccion(ItemsView quien, ItemsViewSelectionChangedEventArgs cuando)
        => ContarLoMarcado();

    /// <summary>Al escribir en el buscador se relee el tablero con el texto puesto.</summary>
    /// <param name="quien">El buscador.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private void AlEscribirEnElBuscador(object quien, TextChangedEventArgs cuando) => Repintar();

    /// <summary>Dice una linea en el acuse del pie; NO abre ningun cuadro (requisito 9).</summary>
    /// <param name="linea">La frase que se enseña en el pie de la ventana principal.</param>
    private static void Acusar(string linea)
    {
        if (App.Ventana is VentanaPrincipal ventana) ventana.AcuseDelPie.Decir(linea);
    }
}
