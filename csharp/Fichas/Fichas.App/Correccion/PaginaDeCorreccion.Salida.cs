using Fichas.App.Cascara;
using Fichas.App.Grupo;
using Fichas.App.Inicio;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;

namespace Fichas.App.Correccion;

/// <summary>
/// La salida: que pasa cuando a un documento deja de faltarle algo y se va de Correccion.
/// </summary>
/// <remarks>
/// <para><b>De donde sale.</b> Palabras del dueno el 2026-09-07 (<c>DECISIONES.md</c>, «EL
/// DUENO DICTA EL FLUJO ENTERO», apartado 4): <i>«Cuando yo lo corrija y le ponga la
/// información, debe salir de Corrección y pasar al grupo de su fecha. Y si voy a Corrección no
/// debe estar ahí, porque ya está todo listo»</i>.</para>
///
/// <para>⚠️ <b>La mitad peligrosa, y es la que gobierna este archivo entero.</b> «Sale de
/// Correccion» no puede querer decir «desaparece». Si el documento se va de una lista y el
/// dueno no ve que llego a la otra, lo pierde, y en este programa perder un documento significa
/// que alguien llegue al templo y no pueda entrar. Por eso el documento resuelto se queda a la
/// vista mientras lo tiene abierto, la banda dice A QUE GRUPO paso, y el boton le lleva a
/// verlo con sus propios ojos.</para>
///
/// <para>⛔ <b>Aqui no se decide si a un documento le falta algo.</b> Eso lo contesta
/// <see cref="LoQueLeFalta"/> a traves de <see cref="LoQueLeFaltaACadaDocumento"/>, y llega ya
/// hecho en el resultado del guardado. Este archivo pinta y navega.</para>
///
/// <para>⛔ Y no escribe ni una fila: la regla permanente 5 sigue entera. Lo unico que escribe
/// en toda la cadena sigue siendo <c>Guardar</c>.</para>
///
/// <para>Va en su propio archivo porque <c>PaginaDeCorreccion.Grupos.cs</c> ya rondaba el
/// limite blando de 300 lineas.</para>
/// </remarks>
public sealed partial class PaginaDeCorreccion
{
    /// <summary>Que dice ahora mismo la banda de salida; lo lee la medicion.</summary>
    public string LoQueDiceLaBandaDeLaSalida
        => _bandaDeLaSalida.Visibility == Visibility.Visible ? _yaEstaResuelto.Text : string.Empty;

    /// <summary>
    /// Ensena o esconde la banda del documento que ya no tiene nada que falte.
    /// </summary>
    /// <remarks>
    /// Se pregunta a la MISMA pasada que ya armo los grupos —<see cref="_loQueLeFalta"/>—, sin
    /// volver a la base: al abrir un documento esa pasada acaba de hacerse, y una segunda
    /// lectura por cada apertura costaria las dos decimas de la pantalla de Inicio en cada
    /// clic.
    /// </remarks>
    private void MostrarSiYaEstaResuelto()
    {
        if (_casoAbierto == 0 || _loQueLeFalta is null
            || !QueEntraEnCorreccion.SaleDeCorreccion(
                   _todosLosGrupos, _casoAbierto, _loQueLeFalta.LeFaltaAlgo(_casoAbierto)))
        {
            _bandaDeLaSalida.Visibility = Visibility.Collapsed;
            return;
        }

        var aDonde = QueEntraEnCorreccion.ADondePasa(_todosLosGrupos, _casoAbierto);
        _yaEstaResuelto.Text = TextoDeLaSalidaDeCorreccion.YaEstabaResuelto(aDonde);
        _irASuGrupo.Content = TextoDeLaSalidaDeCorreccion.BotonDeVerElGrupo(aDonde);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            _irASuGrupo, TextoDeLaSalidaDeCorreccion.BotonDeVerElGrupo(aDonde));

        // La banda solo se pinta cuando el documento SALE, y salir exige tener grupo de dia,
        // asi que aqui el boton siempre lleva a alguna parte. No hace falta apagarlo.
        _bandaDeLaSalida.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Lo que pasa al guardar cuando el documento deja de tener nada que falte: sale de la
    /// lista y se dice a donde fue.
    /// </summary>
    /// <remarks>
    /// <para>Solo hace algo cuando el documento ACABA de resolverse, y por eso se le pasa como
    /// estaba antes: rehacer la lista en cada pulsacion de Guardar costaria una pasada por la
    /// base entera por cada campo corregido, y lo que cambia de poblacion es solo este salto.
    /// Cuando el documento sigue con huecos, la pantalla no se mueve —es lo pedido: «si no
    /// quedó completo, se queda con lo que le falta a la vista»—.</para>
    ///
    /// <para>⚠️ Dentro de la cola NO se dice nada aqui: la cola ya dice que el documento sale,
    /// cuantos quedan y cual toca, y encima abre el siguiente. Dos frases del mismo guardado
    /// contandose lo mismo es el defecto que se midio el 2026-09-06.</para>
    /// </remarks>
    /// <param name="resultado">Lo que devolvio el guardado que acaba de ocurrir.</param>
    /// <param name="leFaltabaAntes">Si a este documento le faltaba algo antes de guardar.</param>
    private void SeguirElFlujoDeTrabajo(ResultadoDeGuardado resultado, bool leFaltabaAntes)
    {
        if (_cola is not null || Servicios is null) return;
        if (!leFaltabaAntes || !resultado.ListoParaAsignar) return;

        var numero = _modelo?.Caso?.NumeroCaso ?? "Este documento";

        // Se rehace la lista dejando el documento abierto como invitado: sale de la poblacion
        // de trabajo y sigue en pantalla hasta que el cambie de documento.
        LlenarLosGrupos(_casoAbierto);

        // ⛔ Y si NO sale —un documento cuya fecha se marco como que no esta en el papel no
        // tiene grupo de dia al que pasar— no se anuncia ninguna salida. Anunciarla seria
        // decirle que el documento se movio cuando sigue exactamente donde estaba; el motivo
        // entero, con el caso medido, esta en QueEntraEnCorreccion.SaleDeCorreccion.
        if (!QueEntraEnCorreccion.SaleDeCorreccion(
                _todosLosGrupos, _casoAbierto, _loQueLeFalta?.LeFaltaAlgo(_casoAbierto) ?? true))
        {
            MostrarSiYaEstaResuelto();
            return;
        }

        var aDonde = QueEntraEnCorreccion.ADondePasa(_todosLosGrupos, _casoAbierto);

        // ⚠️ La cuenta sale de UN solo sitio —el mismo que escribe el denominador de la
        // cabecera—, y eso es lo que impide que las dos cifras de la pantalla se contradigan.
        var quedan = CuantosPidenAlgo();

        MostrarSiYaEstaResuelto();

        if (App.Ventana is VentanaPrincipal ventana)
        {
            ventana.AcuseDelPie.Decir(
                TextoDeLaSalidaDeCorreccion.YaNoEstaEnCorreccion(numero, aDonde, quedan));
        }
    }

    /// <summary>
    /// Lleva al grupo del dia de este documento, que es a donde acaba de pasar.
    /// </summary>
    /// <remarks>
    /// ⚠️ Se navega igual que desde Inicio y desde la ventana de incompletos, con
    /// <see cref="LlegadaAlGrupo"/>: la navegacion de la cascara solo sabe pasar los servicios,
    /// y el grupo necesita ademas QUE dia. El motivo entero esta en ese archivo.
    /// </remarks>
    private void AlPulsarIrASuGrupo(object quien, RoutedEventArgs cuando)
    {
        if (Frame is null || Servicios is null || _casoAbierto == 0) return;

        var fechaIso = QueEntraEnCorreccion.FechaDelGrupoAlQuePasa(_todosLosGrupos, _casoAbierto);
        if (FechasEnEspanol.Leer(fechaIso) is not DateOnly fecha) return;

        Frame.Navigate(
            typeof(PaginaDeGrupo),
            new LlegadaAlGrupo(Servicios, fecha),
            new SuppressNavigationTransitionInfo());
    }
}
