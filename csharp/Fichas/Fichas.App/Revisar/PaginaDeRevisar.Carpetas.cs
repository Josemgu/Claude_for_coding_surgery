using Fichas.App.Cascara;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fichas.App.Revisar;

/// <summary>
/// La mitad de carpetas de la pantalla de Revisar: el arbol de mes → fecha de viaje →
/// unidad, y volcar un mes al disco.
/// </summary>
/// <remarks>
/// <para>Va en su propio archivo y no porque el otro creciera: son dos responsabilidades
/// distintas. <c>PaginaDeRevisar.xaml.cs</c> es la pantalla de siempre —los tableros, el
/// buscador, archivar, marcar y borrar—; esto es lo que el dueno pidio el 2026-09-05, que
/// es ver lo mismo organizado por carpetas y poder bajarlas al disco. Es el mismo reparto
/// que ya usa <c>Importar/GuardadoDeHojas.Filas.cs</c>.</para>
///
/// <para>La regla no esta aqui: agrupar es <see cref="ArbolDeRevisar"/>, decidir que se
/// escribe es <see cref="PlanDeVolcado"/> y escribirlo es <see cref="VolcadoDeCarpetas"/>,
/// y los tres se prueban sin abrir ninguna ventana. Aqui solo se colocan controles y se
/// pasan mensajes.</para>
/// </remarks>
public sealed partial class PaginaDeRevisar
{
    /// <summary>
    /// Rehace el arbol de carpetas: mes, luego fecha de viaje, luego unidad.
    /// </summary>
    /// <remarks>
    /// Solo se abre el PRIMER mes, que es el que viaja antes. Con 3 000 documentos abrirlos
    /// todos construiria miles de filas de golpe; cerrados, <c>TreeView</c> solo materializa
    /// las que se ven, y los nodos que no se ven no son elementos vivos.
    /// </remarks>
    private void PintarLasCarpetas()
    {
        if (_tablero is null) return;

        _carpetas = ArbolDeRevisar.Agrupar(_tablero.Todas(_tableroALaVista));
        _carpetaALaVista = null;
        _ramas.Clear();
        _arbol.RootNodes.Clear();

        foreach (var mes in _carpetas)
        {
            var nodoDelMes = Rama(mes.Etiqueta, new CarpetaDelArbol(mes.Carpeta, DocumentosDe(mes), mes));
            foreach (var fecha in mes.Fechas)
            {
                var deLaFecha = fecha.Unidades.SelectMany(u => u.Documentos).ToList();
                var nodoDeLaFecha = Rama(fecha.Etiqueta, new CarpetaDelArbol(fecha.Carpeta, deLaFecha, mes));
                foreach (var unidad in fecha.Unidades)
                {
                    nodoDeLaFecha.Children.Add(
                        Rama(unidad.Etiqueta, new CarpetaDelArbol(unidad.Carpeta, unidad.Documentos, mes)));
                }
                nodoDelMes.Children.Add(nodoDeLaFecha);
            }
            _arbol.RootNodes.Add(nodoDelMes);
        }

        if (_arbol.RootNodes.Count > 0) _arbol.RootNodes[0].IsExpanded = true;
    }

    /// <summary>Crea un nodo del arbol con su texto dentro y lo apunta en el diccionario.</summary>
    private TreeViewNode Rama(string etiqueta, CarpetaDelArbol carpeta)
    {
        var nodo = new TreeViewNode { Content = etiqueta };
        _ramas[nodo] = carpeta;
        return nodo;
    }

    /// <summary>Todos los documentos de un mes, ya en el orden de las carpetas.</summary>
    private static List<TarjetaDeDocumento> DocumentosDe(GrupoDeMes mes)
        => [.. mes.Fechas.SelectMany(f => f.Unidades).SelectMany(u => u.Documentos)];

    /// <summary>
    /// Pinta las tarjetas de la carpeta elegida, o todas si no hay ninguna elegida.
    /// </summary>
    /// <remarks>
    /// Sin carpeta elegida el orden es el de las carpetas —lo que viaja antes, arriba—, que
    /// es lo que el dueno pidio: «la prioridad son los que viajaran pronto».
    /// </remarks>
    private void PintarLasTarjetas()
    {
        if (_tablero is null) return;

        var delTablero = _carpetas.SelectMany(DocumentosDe).ToList();
        var visibles = _carpetaALaVista?.Documentos ?? delTablero;

        // Se desmarca ANTES de cambiar la lista. Medido el 2026-09-04 con la ventana
        // abierta: archivar 13 tarjetas marcadas y repintar mataba el proceso sin dejar
        // ni una linea en fichas.log. Un ItemsView al que se le cambia la fuente con
        // elementos todavia marcados se queda apuntando a lo que ya no esta. Y solo si YA
        // hay rejilla: desmarcar una que todavia no tiene fuente lo mata igual.
        if (_rejilla.ItemsSource is not null) _rejilla.DeselectAll();
        _rejilla.ItemsSource = visibles;

        _cuentaDeLaVista.Text = _carpetaALaVista is null
            ? $"viendo {delTablero.Count} de {_tablero.Total} documentos"
            : $"viendo {visibles.Count} de {delTablero.Count} · «{_carpetaALaVista.Carpeta}» · "
              + $"{_tablero.Total} en la base";

        _botonDeVolcar.IsEnabled = _carpetaALaVista is not null;
        ContarLoMarcado();
    }

    /// <summary>Al pulsar una carpeta del arbol, la rejilla ensena solo sus documentos.</summary>
    private void AlElegirUnaCarpeta(TreeView quien, TreeViewItemInvokedEventArgs cuando)
        => MostrarLaCarpeta(cuando.InvokedItem as TreeViewNode);

    /// <summary>
    /// Lo mismo cuando la carpeta se elige SIN pulsarla: con el teclado o por accesibilidad.
    /// </summary>
    /// <remarks>
    /// ⛔ No sobra. Medido el 2026-09-05 con la ventana abierta sobre el paquete publicado:
    /// con solo <c>ItemInvoked</c>, elegir la rama «Septiembre 2026» por
    /// <c>SelectionItemPattern</c> dejaba el pie diciendo «viendo 9 de 9» y el boton de
    /// volcar apagado —<c>encendido: False</c>—. Quien navegue el arbol con las flechas la
    /// selecciona sin invocarla, y no pasaba nada.
    /// </remarks>
    private void AlCambiarDeCarpeta(TreeView quien, TreeViewSelectionChangedEventArgs cuando)
        => MostrarLaCarpeta(cuando.AddedItems.OfType<TreeViewNode>().LastOrDefault());

    /// <summary>Ensena los documentos de esa rama; si ya se estaban viendo, no repinta.</summary>
    /// <remarks>
    /// La guarda no es un adorno: pulsar una rama dispara los DOS caminos —invocar y cambiar
    /// la seleccion— y repintar dos veces cambia la fuente de la rejilla dos veces seguidas,
    /// que es el patron que el 2026-09-04 tumbo el proceso sin dejar linea en el registro.
    /// </remarks>
    private void MostrarLaCarpeta(TreeViewNode? nodo)
    {
        if (nodo is null || !_ramas.TryGetValue(nodo, out var carpeta)) return;
        if (ReferenceEquals(_carpetaALaVista, carpeta)) return;

        _carpetaALaVista = carpeta;
        PintarLasTarjetas();
    }

    /// <summary>Quita el filtro de carpeta y vuelve a ensenar todos los documentos.</summary>
    private void AlPulsarVerTodo(object quien, RoutedEventArgs cuando)
    {
        _carpetaALaVista = null;
        _arbol.SelectedNodes.Clear();
        PintarLasTarjetas();
    }

    /// <summary>
    /// Vuelca al disco el mes de la carpeta elegida, con la misma organizacion que se ve.
    /// </summary>
    /// <remarks>
    /// Del dueno, 2026-09-05: «descargar ese conjunto de carpetas a mi escritorio en esa
    /// organizacion». Donde se vuelca lo elige el con el cuadro de carpeta de WINDOWS, que
    /// no es un aviso del programa de los que prohibe el requisito 4. Cerrarlo sin elegir
    /// nada NO deja aviso: no ha pasado nada que contar.
    /// </remarks>
    private void AlPulsarVolcar(object quien, RoutedEventArgs cuando)
    {
        if (Servicios is null || App.Ventana is null) return;
        if (_carpetaALaVista is not CarpetaDelArbol elegida)
        {
            Acusar("Elige antes una carpeta del árbol; se vuelca el mes al que pertenece.");
            return;
        }

        var mes = elegida.Mes;
        Servicios.Registro.Anotar($"VOLCADO   se abre el cuadro de carpeta para «{mes.Carpeta}»");
        var raiz = SelectorDeArchivos.QueCarpeta(
            WinRT.Interop.WindowNative.GetWindowHandle(App.Ventana),
            $"Dónde dejar las carpetas de {mes.Carpeta}");
        if (raiz is null)
        {
            Servicios.Registro.Anotar("VOLCADO   se cerró sin elegir carpeta; no se volcó nada.");
            return;
        }

        var resumen = VolcadoDeCarpetas.Volcar(raiz, PlanDeVolcado.Para(mes, Servicios.Personas));
        Servicios.Registro.Anotar($"VOLCADO   en «{raiz}» · {resumen.Linea}");
        Acusar("Volcado " + resumen.Linea);
    }
}
