using Fichas.App.Cascara;
using Fichas.Reportes.Reglas;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Fichas.App.Asignar;

/// <summary>
/// La pantalla de asignar: TODOS los casos a cualquier companero activo.
/// </summary>
/// <remarks>
/// ⛔ Terreno del programador de Asignar y Revisar (fase C5). Nadie mas escribe aqui.
///
/// Aqui no hay ni una regla: la pantalla lee de <see cref="ListaParaAsignar"/> y escribe por
/// <see cref="OperacionDeAsignar"/>, que son las dos clases que se prueban sin ventana. Lo
/// que queda en este archivo es colocar controles y pasar mensajes.
/// </remarks>
public sealed partial class PaginaDeAsignar : PaginaDeFichas
{
    private ListaParaAsignar? _lectura;
    private OperacionDeAsignar? _asignar;
    private PanelDelEquipo? _equipo;

    /// <summary>Monta la pantalla y ata Ctrl+A a marcar todo lo que se ve.</summary>
    public PaginaDeAsignar()
    {
        InitializeComponent();
        _lista.SelectionChanged += AlCambiarLaSeleccion;
        KeyboardAccelerators.Add(new KeyboardAccelerator
        {
            Key = VirtualKey.A,
            Modifiers = VirtualKeyModifiers.Control,
        });
        KeyboardAccelerators[0].Invoked += AlPulsarMarcarTodo;
    }

    /// <summary>Lee la base entera y pinta la lista con su denominador.</summary>
    protected override void AlLlegar()
    {
        if (Servicios is null) return;

        _lectura = new ListaParaAsignar(
            Servicios.Casos, Servicios.Asignaciones, Servicios.Companeros, Servicios.Personas);
        _asignar = new OperacionDeAsignar(Servicios.Asignaciones, Servicios.Reloj, Servicios.Avisos);

        _equipo = new PanelDelEquipo(
            Servicios.Companeros,
            Servicios.Mantenimiento,
            new Revisar.OperacionDeBorrar(Servicios.Mantenimiento, Servicios.Avisos, Servicios.Registro),
            Servicios.Reloj,
            Servicios.Avisos,
            Acusar);

        // Un alta o una baja cambian a quien se le puede dar trabajo: el desplegable y el
        // denominador tienen que enterarse en el momento, no en la proxima visita.
        _equipo.Cambio += (_, _) => RepintarConLosDestinos();

        _destinos.ItemsSource = _lectura.Destinos();
        if (_destinos.Items.Count > 0) _destinos.SelectedIndex = 0;

        Repintar();
    }

    /// <summary>Vuelve a leer la base y a pintar la lista con lo que haya en el buscador.</summary>
    private void Repintar()
    {
        if (_lectura is null) return;

        // Se desmarca ANTES de cambiar la lista, por lo mismo que en la pantalla de
        // Revisar: un ItemsView con elementos marcados al que se le cambia la fuente se
        // queda apuntando a lo que ya no esta, y el proceso muere sin dejar rastro.
        // Y solo si YA hay lista: desmarcar una que todavia no tiene fuente lo mata igual.
        if (_lista.ItemsSource is not null) _lista.DeselectAll();

        var texto = _buscador.Text ?? string.Empty;
        // Se piden todos porque ItemsView virtualiza: construye los renglones que se ven,
        // no los 3 000. El coste de la lista esta medido en PruebasDeRapidez.
        var pagina = _lectura.Ofrecer(Pagina.Primera(int.MaxValue), texto);
        _lista.ItemsSource = pagina.Elementos;

        // ⚠️ 2026-09-06. Aqui se decia tambien «N archivados fuera de la lista», y el motivo
        // escrito era bueno: sin esa cifra, una lista que encoge no se distingue de una que
        // perdio casos. El dueno lo deshizo con un motivo que gana —«debe pasar a archivado y
        // no aparecer mas en ningun lado», porque verlo nombrado le confunde—. La cifra que
        // contesta «¿me falta algo?» vive ahora en Revisar, con «Ver los archivados».
        var sinArchivar = _lectura.CuantosSePuedenOfrecer();
        var desactivados = _lectura.CuantosDestinosDesactivados();
        _denominador.Text =
            Plural.Con(sinArchivar, "caso", "casos")
            + $" · {pagina.TotalDisponible} " + Plural.Palabra(pagina.TotalDisponible, "ofrecido", "ofrecidos")
            + " · " + Plural.Con(_lectura.Destinos().Count, "compañero activo", "compañeros activos")
            + " (" + Plural.Con(desactivados, "desactivado no recibe casos", "desactivados no reciben casos") + ")";

        ContarLoMarcado();
    }

    /// <summary>Dice en una linea cuantos hay marcados y a quien irian.</summary>
    private void ContarLoMarcado()
    {
        var cuantos = _lista.SelectedItems.Count;
        var destino = _destinos.SelectedItem as Companero;
        _botonDeAsignar.IsEnabled = cuantos > 0 && destino is not null;
        _botonDeRetirar.IsEnabled = cuantos > 0;
        _estadoDeLaSeleccion.Text = cuantos == 0
            ? "Marca los casos que quieras dar. Ctrl+A marca todos los que se ven."
            : Plural.Con(cuantos, "marcado", "marcados")
              + " · " + Plural.Palabra(cuantos, "iría", "irían")
              + $" a {destino?.Nombre ?? "nadie: elige un compañero"}";
    }

    /// <summary>Asigna lo marcado al companero elegido, por la unica puerta que hay.</summary>
    private void AlPulsarAsignar(object quien, RoutedEventArgs cuando)
    {
        if (_asignar is null || _destinos.SelectedItem is not Companero destino) return;

        var casos = CasosMarcados();
        if (casos.Count == 0) return;

        var resumen = _asignar.AsignarVarios(casos, destino.Id, destino.Nombre);
        Acusar(resumen.Linea);
        _lista.DeselectAll();
        Repintar();
    }

    /// <summary>Retira lo marcado: desactiva las asignaciones vivas, nunca las borra.</summary>
    private void AlPulsarRetirar(object quien, RoutedEventArgs cuando)
    {
        if (_asignar is null) return;

        var casos = CasosMarcados();
        if (casos.Count == 0) return;

        var retirados = casos.Count(caso => _asignar.RetirarDelCaso(caso).SeEscribio);
        Acusar(
            Plural.Con(retirados, "caso retirado", "casos retirados")
            + "; la fila de quien " + Plural.Palabra(retirados, "lo llevaba", "los llevaba") + " se conserva.");
        _lista.DeselectAll();
        Repintar();
    }

    /// <summary>Abre el panel del equipo anclado a su boton.</summary>
    private void AlPulsarEquipo(object quien, RoutedEventArgs cuando)
    {
        if (_equipo is null || quien is not FrameworkElement boton) return;
        _equipo.Abrir(boton);
    }

    /// <summary>
    /// Relee la lista Y el desplegable de destinos, que es lo que cambia un alta o una baja.
    /// </summary>
    /// <remarks>
    /// Se conserva a quien estuviera elegido: cambiar el equipo no puede mover el destino
    /// que el dueno ya habia puesto, salvo que sea justo a quien acaba de desactivar.
    /// </remarks>
    private void RepintarConLosDestinos()
    {
        if (_lectura is null) return;

        var elegido = (_destinos.SelectedItem as Companero)?.Id;
        var destinos = _lectura.Destinos();
        _destinos.ItemsSource = destinos;

        var sigue = destinos.ToList().FindIndex(c => c.Id == elegido);
        _destinos.SelectedIndex = sigue >= 0 ? sigue : (destinos.Count > 0 ? 0 : -1);

        Repintar();
    }

    /// <summary>Los numeros internos de los casos marcados ahora mismo.</summary>
    private List<long> CasosMarcados()
        => _lista.SelectedItems.OfType<RenglonParaAsignar>().Select(r => r.CasoId).ToList();

    /// <summary>Ctrl+A: marca todo lo que hay a la vista; si ya estaba todo, lo desmarca.</summary>
    private void AlPulsarMarcarTodo(KeyboardAccelerator quien, KeyboardAcceleratorInvokedEventArgs cuando)
    {
        cuando.Handled = true;
        if (_lista.ItemsSource is not IReadOnlyList<RenglonParaAsignar> renglones) return;
        if (MarcarTodo.HayQueMarcar(_lista.SelectedItems.Count, renglones.Count)) _lista.SelectAll();
        else _lista.DeselectAll();
    }

    /// <summary>Al cambiar lo marcado, solo se recuenta: la lista no se reconstruye.</summary>
    private void AlCambiarLaSeleccion(ItemsView quien, ItemsViewSelectionChangedEventArgs cuando)
        => ContarLoMarcado();

    /// <summary>Al cambiar de destino tampoco se relee la base: el destino no filtra casos.</summary>
    private void AlCambiarDeDestino(object quien, SelectionChangedEventArgs cuando) => ContarLoMarcado();

    /// <summary>Al escribir en el buscador se relee la lista con el texto puesto.</summary>
    private void AlEscribirEnElBuscador(object quien, TextChangedEventArgs cuando) => Repintar();

    /// <summary>Dice una linea en el acuse del pie; NO abre ningun cuadro (requisito 9).</summary>
    private static void Acusar(string linea)
    {
        if (App.Ventana is VentanaPrincipal ventana) ventana.AcuseDelPie.Decir(linea);
    }
}
