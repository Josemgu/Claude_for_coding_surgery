using Fichas.Contratos.Modelos;
using Fichas.App.Cascara;
using Fichas.App.Revisar;
using Fichas.Contratos.Puertos;
using Microsoft.UI.Xaml.Controls;

namespace Fichas.App.Correccion;

/// <summary>
/// El botón «Eliminar» del pie: una persona del documento abierto, o el documento entero.
/// </summary>
/// <remarks>
/// <para><b>De dónde sale.</b> Palabras del dueño el 2026-09-14: <i>«Agrega un botón en la
/// Corrección de eliminar información también. No lo tengo y es importante tenerlo»</i>. Y lo que
/// repitió dos veces el 2026-09-07 sobre un documento sin nadie dentro: <i>«debería permitirme
/// eliminarlo»</i>; el supervisor midió entonces que el botón existía en Revisar y que lo que
/// faltaba era el camino desde donde él lo estaba mirando, que es aquí.</para>
///
/// <para>⛔ <b>Las dos cosas preguntan antes y nunca se hacen a ciegas.</b> Una persona se
/// pregunta por <see cref="OperacionDeEliminarUnaPersona"/>; el documento entero va por la MISMA
/// puerta que Revisar —<see cref="OperacionDeBorrar"/>, con su plan, su copia previa y su
/// pregunta—, para que los dos caminos dejen la base igual. Esta clase pinta el menú, llama y
/// pasa al siguiente documento.</para>
///
/// <para>⛔ Y no firma nada (regla permanente 5).</para>
/// </remarks>
public sealed partial class PaginaDeCorreccion
{
    /// <summary>Rehace el menú cada vez que se abre: una opción por persona del documento y la del documento entero.</summary>
    /// <remarks>
    /// Se arma al abrir y no al cargar el documento para que siempre diga las personas de AHORA:
    /// añadir una a mano o eliminar otra cambian la lista sin volver a cargar.
    /// </remarks>
    private void AlAbrirElMenuDeEliminar(object quien, object cuando)
    {
        _menuDeEliminar.Items.Clear();
        if (_modelo?.Caso is null) return;

        foreach (var persona in _modelo.Personas)
        {
            var opcion = new MenuFlyoutItem { Text = TextoDeEliminar.OpcionDeLaPersona(persona), Tag = persona.Id };
            opcion.Click += AlElegirEliminarUnaPersona;
            _menuDeEliminar.Items.Add(opcion);
        }

        if (_modelo.Personas.Count > 0) _menuDeEliminar.Items.Add(new MenuFlyoutSeparator());

        var elDocumento = new MenuFlyoutItem { Text = TextoDeEliminar.OpcionDelDocumentoEntero };
        elDocumento.Click += AlElegirEliminarElDocumento;
        _menuDeEliminar.Items.Add(elDocumento);
    }

    /// <summary>Se eligió a una persona del menú: se pregunta, se elimina y se repinta.</summary>
    private async void AlElegirEliminarUnaPersona(object quien, Microsoft.UI.Xaml.RoutedEventArgs cuando)
    {
        if (_modelo is null || Servicios is null || quien is not MenuFlyoutItem opcion || opcion.Tag is not long personaId) return;

        var operacion = new OperacionDeEliminarUnaPersona(_modelo, Servicios.Avisos, Servicios.Registro);
        Servicios.Avisos.CerrarTodos();
        var resultado = await operacion.PreguntarYEliminar(personaId, XamlRoot).ConfigureAwait(true);

        if (resultado is null)
        {
            Decir("No se eliminó a nadie.");
            return;
        }

        Decir(resultado.LineaDelAcuse);
        if (!resultado.SeBorro) return;

        RepintarTrasCambiarLasPersonas();

        // Era la última: se dice y se pregunta por el documento entero, por la puerta de siempre.
        if (resultado.EraLaUltima)
        {
            Servicios.Avisos.Dejar(Aviso.Informa(TextoDeEliminar.EraLaUltimaDe(_modelo.Caso?.NumeroCaso)));
            await EliminarElDocumentoAbierto().ConfigureAwait(true);
        }
    }

    /// <summary>Se eligió el documento entero del menú.</summary>
    private async void AlElegirEliminarElDocumento(object quien, Microsoft.UI.Xaml.RoutedEventArgs cuando)
    {
        if (Servicios is null) return;
        Servicios.Avisos.CerrarTodos();
        await EliminarElDocumentoAbierto().ConfigureAwait(true);
    }

    /// <summary>
    /// Borra el documento abierto por la puerta de Revisar —plan, copia previa, pregunta— y
    /// pasa al siguiente de la cola o del grupo.
    /// </summary>
    /// <remarks>
    /// El siguiente se decide ANTES de borrar, mirando la lista que se tiene delante: después
    /// del borrado el documento ya no está en ninguna lista y no habría desde dónde contar.
    /// </remarks>
    private async Task EliminarElDocumentoAbierto()
    {
        if (_modelo?.Caso is null || Servicios is null || _casoAbierto == 0)
        {
            Decir(TextoDeEliminar.SinDocumentoAbierto);
            return;
        }

        var casoId = _casoAbierto;
        var numero = _modelo.Caso.NumeroCaso;
        var siguiente = ElSiguienteDocumentoDelGrupo();

        PlanDeBorrado? plan = null;
        var borrar = new OperacionDeBorrar(Servicios.Mantenimiento, Servicios.Avisos, Servicios.Registro);
        var linea = await borrar.PreguntarYBorrar(
            mantenimiento => plan = mantenimiento.PlanearDocumentos([casoId]), XamlRoot).ConfigureAwait(true);

        // Nulo: no se pudo ni planear, y la franja ya dice por qué. Si el documento sigue en la
        // base, dijo que no, y la línea de la puerta ya dice dónde quedó la copia.
        if (linea is null) return;
        if (Servicios.Casos.Obtener(casoId) is not null)
        {
            Decir(linea);
            return;
        }

        Decir(TextoDeEliminar.AlEliminarElDocumento(numero, plan?.RutaDeLaCopia));
        PasarAlSiguienteDocumento(casoId, siguiente);
    }

    /// <summary>El documento que toca abrir cuando este se vaya: el de después en el desplegable, o el de antes; 0 si no hay otro.</summary>
    private long ElSiguienteDocumentoDelGrupo()
    {
        var donde = _documentos.ToList().FindIndex(entrada => entrada.Id == _casoAbierto);
        if (donde < 0) return 0;
        if (donde + 1 < _documentos.Count) return _documentos[donde + 1].Id;
        return donde > 0 ? _documentos[donde - 1].Id : 0;
    }

    /// <summary>
    /// Abre el que toca después de borrar: el siguiente de la cola si se entró por ella, y si no
    /// el del grupo; si no queda ninguno, deja la pantalla vacía y lo dice.
    /// </summary>
    /// <param name="borrado">El id del documento que acaba de salir de la base.</param>
    /// <param name="siguienteDelGrupo">El que se eligió antes de borrar; 0 si no había otro en el grupo.</param>
    private void PasarAlSiguienteDocumento(long borrado, long siguienteDelGrupo)
    {
        if (_cola is not null)
        {
            // Sacar de la cola con un candidato que ya no está no falla: TodaviaLeFalta contesta
            // «no» a un id que no existe y la cola lo salta.
            var cual = _cola.SacarYDecirCualToca(borrado, TodaviaLeFalta);
            if (cual is long queSigue)
            {
                AbrirElCaso(queSigue);
                PintarLaBandaDeLaCola();
                return;
            }

            VolverALaCola();
            return;
        }

        _casoAbierto = 0;
        _documentos = [];
        LlenarLosGrupos(siguienteDelGrupo);
        if (_casoAbierto == 0) DejarLaPantallaVacia();
    }

    /// <summary>Vuelve a pintar todo lo que depende de las personas del documento.</summary>
    /// <remarks>Es lo mismo que hace añadir una persona a mano; está aquí para que eliminar y añadir repinten igual.</remarks>
    private void RepintarTrasCambiarLasPersonas()
    {
        RepartirLasFichas();
        MostrarLoQueContestoElCompanero();
        MostrarElCuadroDeLaPersonaAMano();
        Recontar();
        RehacerLaLineaDelDocumento();
    }

    /// <summary>
    /// Deja la pantalla sin documento: sin fichas, sin hoja y con el pie diciéndolo.
    /// </summary>
    /// <remarks>
    /// Solo se llega aquí cuando se borró el último documento que pedía algo. Se cierra el modelo
    /// en vez de cargar el id borrado, para no dejar el aviso de «ese caso ya no está en la base»
    /// sobre un documento que él acaba de mandar borrar.
    /// </remarks>
    private void DejarLaPantallaVacia()
    {
        _modelo?.Cerrar();
        _fichas.Clear();
        _loDudoso.ItemsSource = null;
        _elResto.ItemsSource = null;
        _marcoDeLoDudoso.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        _marcoDelResto.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        _lasRespuestas.ItemsSource = null;
        _marcoDeLasRespuestas.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        _laRecomendacion.ItemsSource = null;
        _marcoDeLaRecomendacion.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        _marcoDeLaPersonaAMano.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        _marcoDeLaMarca.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        _bandaDeLaSalida.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        _visor.MostrarHoja(null, 0, TextoDeEliminar.NoQuedaNingunoAbierto);
        _cuenta.Text = TextoDeEliminar.NoQuedaNingunoAbierto;
    }
}
