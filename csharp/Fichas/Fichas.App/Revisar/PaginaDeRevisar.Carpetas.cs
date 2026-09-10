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
    /// Rehace el arbol de carpetas —mes, fecha de viaje, unidad— y lo deja donde estaba el.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>El arbol se rehace de verdad y eso no es negociable</b>: cada carpeta lleva
    /// en su etiqueta cuantos documentos hay EN EL TABLERO que se mira, y tras marcar un caso
    /// esa cifra cambia. Lo que se conserva no son los nodos: es <b>donde estaba el</b>, y lo
    /// decide <see cref="MemoriaDelSitio"/>, que se prueba sin ventana.</para>
    ///
    /// <para>⛔ <b>Hasta el 2026-09-09 este metodo hacia <c>_carpetaALaVista = null</c></b>, y
    /// eso es lo que el dueno describio: <i>«te envia al inicio otra vez de Revisar y te
    /// coloca todos juntos»</i>. Medido con la ventana abierta ese mismo dia: con «Enero 2027»
    /// elegida, marcar un caso pasaba de «viendo 5 de 18 · «Enero 2027»» a «viendo 17 de 28
    /// documentos».</para>
    ///
    /// <para>Sin sitio recordado solo se abre el PRIMER mes, que es el que viaja antes. Con
    /// 3 000 documentos abrirlos todos construiria miles de filas de golpe; cerrados,
    /// <c>TreeView</c> solo materializa las que se ven.</para>
    /// </remarks>
    private void PintarLasCarpetas()
    {
        if (_tablero is null) return;

        var sitio = DondeEstaba();

        _carpetas = ArbolDeRevisar.Agrupar(_tablero.Todas(_tableroALaVista));
        _carpetaALaVista = null;
        _ramas.Clear();
        _arbol.RootNodes.Clear();

        var esElPrimerMes = true;
        foreach (var mes in _carpetas)
        {
            var nodoDelMes = Rama(
                mes.Etiqueta,
                new CarpetaDelArbol(MemoriaDelSitio.ClaveDelMes(mes), mes.Carpeta, DocumentosDe(mes), mes),
                sitio,
                esElPrimerMes);

            foreach (var fecha in mes.Fechas)
            {
                var deLaFecha = fecha.Unidades.SelectMany(u => u.Documentos).ToList();
                var nodoDeLaFecha = Rama(
                    fecha.Etiqueta,
                    new CarpetaDelArbol(MemoriaDelSitio.ClaveDeLaFecha(mes, fecha), fecha.Carpeta, deLaFecha, mes),
                    sitio,
                    esElPrimerMes: false);

                foreach (var unidad in fecha.Unidades)
                {
                    nodoDeLaFecha.Children.Add(Rama(
                        unidad.Etiqueta,
                        new CarpetaDelArbol(
                            MemoriaDelSitio.ClaveDeLaUnidad(mes, fecha, unidad), unidad.Carpeta, unidad.Documentos, mes),
                        sitio,
                        esElPrimerMes: false));
                }

                nodoDelMes.Children.Add(nodoDeLaFecha);
            }

            _arbol.RootNodes.Add(nodoDelMes);
            esElPrimerMes = false;
        }

        VolverALaCarpeta(MemoriaDelSitio.CarpetaQueVuelve(sitio, _ramas.Values.Select(c => c.Clave)));
    }

    /// <summary>Donde estaba el ahora mismo: su carpeta y las ramas que tenia abiertas.</summary>
    private SitioDeRevisar DondeEstaba()
        => MemoriaDelSitio.Recordar(
            _carpetaALaVista?.Clave,
            _ramas.Where(par => par.Key.IsExpanded).Select(par => par.Value.Clave));

    /// <summary>
    /// Vuelve a dejar elegida la carpeta donde el estaba, o ensena todos si ya no existe.
    /// </summary>
    /// <remarks>
    /// Se toca <see cref="_carpetaALaVista"/> directamente y no por <c>MostrarLaCarpeta</c>:
    /// aqui todavia no hay tarjetas pintadas, y <see cref="PintarLasTarjetas"/> viene detras.
    /// Marcar el nodo en el arbol es aparte, para que el borde de la rama tambien vuelva.
    /// </remarks>
    private void VolverALaCarpeta(string? clave)
    {
        if (clave is null) return;

        foreach (var (nodo, carpeta) in _ramas)
        {
            if (!string.Equals(carpeta.Clave, clave, StringComparison.Ordinal)) continue;

            _carpetaALaVista = carpeta;
            _arbol.SelectedNode = nodo;
            return;
        }
    }

    /// <summary>Crea un nodo del arbol, lo apunta en el diccionario y lo abre si lo estaba.</summary>
    private TreeViewNode Rama(string etiqueta, CarpetaDelArbol carpeta, SitioDeRevisar sitio, bool esElPrimerMes)
    {
        var nodo = new TreeViewNode
        {
            Content = etiqueta,
            IsExpanded = MemoriaDelSitio.SeAbre(sitio, carpeta.Clave, esElPrimerMes),
        };

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
