using Microsoft.UI.Xaml;

namespace Fichas.App.Correccion;

/// <summary>
/// Las dos salidas del callejón: volver a donde se venía, y decir a mano quién va en un
/// documento del que no se leyó a nadie.
/// </summary>
/// <remarks>
/// <para>Las dos son quejas del dueno del 2026-09-07 probando el programa: <i>«No tengo opción
/// de regresar a la ventana de atrás, que quiero seguir trabajando»</i> y, sobre un renglon que
/// dice «sin ninguna persona leída», <i>«¿Cómo voy a confirmar la recomendación si no me da la
/// opción?»</i>.</para>
///
/// <para>⛔ <b>Aqui no se decide nada de lo que se escribe.</b> Quien escribe y con que origen
/// lo decide <see cref="ModeloDeCorreccion.AnadirUnaPersonaAMano"/>, que se prueba sin ventana;
/// esto pinta lo que salio. Y el programa no propone ningun nombre: el motivo entero, con los
/// dos escaneos reales que lo delatan, esta en <see cref="TextoDeLaPersonaAMano"/>.</para>
/// </remarks>
public sealed partial class PaginaDeCorreccion
{
    /// <summary>
    /// Ensena el boton de volver, con el nombre del sitio al que lleva.
    /// </summary>
    /// <remarks>
    /// <para>La llaman las pantallas que traen aqui un documento —la del grupo y la de lo que
    /// no esta completo— justo despues de navegar. Sin esto el boton no sale, que es lo
    /// correcto: entrando por la pestana de Correccion no hay «atras» al que volver que sea
    /// suyo, y un boton que lleve a una pantalla cualquiera es peor que ninguno.</para>
    ///
    /// <para>Se vuelve con <c>Frame.GoBack()</c> y no navegando de nuevo: el marco guarda el
    /// PARAMETRO con el que se llego —que dia era el grupo—, asi que se vuelve al mismo sitio
    /// y no a uno parecido. Y como la pantalla se vuelve a pintar al llegar, el renglon del
    /// documento que se acaba de corregir aparece ya con lo que le falta ahora.</para>
    /// </remarks>
    /// <param name="aDonde">
    /// Como se llama el sitio del que se vino, <b>con su preposicion ya puesta</b>: «al grupo
    /// del jueves 17 de septiembre», «a lo que no está completo». La trae quien llama y no se
    /// pega aqui: componiendo «Volver a » + «el grupo…» sale «Volver a el grupo», que se leyo
    /// tal cual en la medicion con la ventana abierta del 2026-09-07.
    /// </param>
    public void MostrarLaVueltaA(string aDonde)
    {
        var limpio = ReglasDeCampo.Limpiar(aDonde);
        if (limpio is null || Frame is null || !Frame.CanGoBack)
        {
            _vueltaAtras.Visibility = Visibility.Collapsed;
            return;
        }

        _vueltaAtras.Content = $"‹ Volver {limpio}";
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_vueltaAtras, $"Volver {limpio}");
        _vueltaAtras.Visibility = Visibility.Visible;
    }

    /// <summary>Vuelve a la pantalla de la que se vino, con lo que tuviera puesto.</summary>
    private void AlPulsarVolverAtras(object quien, RoutedEventArgs cuando)
    {
        if (Frame is null || !Frame.CanGoBack) return;
        Frame.GoBack();
    }

    /// <summary>
    /// Ensena el cuadro de escribir a mano quien va: abierto y con la banda del atasco cuando
    /// no hay nadie dentro, y cerrado como «una mas» cuando ya hay gente.
    /// </summary>
    /// <remarks>
    /// Hasta el 2026-09-14 solo se ensenaba sin nadie dentro —el atasco que el dueno enseño—.
    /// Desde entonces se ofrece siempre, que es lo que pidio el 2026-09-10: <i>«agregar
    /// personas que quizas el escaner no contemplo»</i>, o sea un documento con gente al que
    /// le falta una. El titulo lo decide el modelo; aqui solo se pinta.
    /// </remarks>
    private void MostrarElCuadroDeLaPersonaAMano()
    {
        if (_modelo is null) return;

        var haceFalta = _modelo.SinNingunaPersonaLeida;
        _marcoDeLaPersonaAMano.Visibility = _modelo.Caso is null ? Visibility.Collapsed : Visibility.Visible;
        _bandaDeLaPersonaAMano.Visibility = haceFalta ? Visibility.Visible : Visibility.Collapsed;
        _desplegableDeLaPersonaAMano.IsExpanded = haceFalta;
        _tituloDeAnadirOtra.Text = _modelo.TituloDelCuadroDeLaPersonaAMano;

        _tituloDeLaPersonaAMano.Text = TextoDeLaPersonaAMano.Titulo;
        _deQueVaLaPersonaAMano.Text = TextoDeLaPersonaAMano.DeQueVa;
        _deDondeSacarElNombre.Text = _modelo.DeDondeSacarElNombre;
        _botonDeAnadirPersona.Content = TextoDeLaPersonaAMano.BotonDeAnadir;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            _botonDeAnadirPersona, TextoDeLaPersonaAMano.BotonParaElLector);

        // Las dos casillas se vacian al cambiar de documento, y NO se rellenan con nada: lo
        // que el programa propone aqui es exactamente nada (regla permanente 1).
        _nombreAMano.Text = _modelo.NombrePropuestoParaLaPersonaNueva;
        _cedulaAMano.Text = _modelo.CedulaPropuestaParaLaPersonaNueva;
    }

    /// <summary>
    /// Escribe la persona que se acaba de teclear y vuelve a pintar la pantalla entera.
    /// </summary>
    /// <remarks>
    /// Se repinta pase lo que pase, incluso cuando no se escribio: las fichas, la cuenta del
    /// pie y la linea del desplegable salen de la base, y dejar las de antes diria algo que la
    /// base no dice. Y lo que salio se DICE siempre en el pie: un boton que hace su trabajo en
    /// silencio es, para quien lo mira, un boton roto (dueno, 2026-09-04).
    /// </remarks>
    private void AlPulsarAnadirUnaPersona(object quien, RoutedEventArgs cuando)
    {
        if (_modelo is null || Servicios is null) return;

        var resultado = _modelo.AnadirUnaPersonaAMano(_nombreAMano.Text, _cedulaAMano.Text);

        Servicios.Avisos.CerrarTodos();
        if (resultado.Avisos.Count > 0) Servicios.Avisos.Dejar(resultado.Avisos);
        Decir(resultado.LineaDelAcuse);

        if (resultado.SeEscribio)
        {
            _nombreAMano.Text = string.Empty;
            _cedulaAMano.Text = string.Empty;
        }

        RepartirLasFichas();
        MostrarLoQueContestoElCompanero();
        MostrarElCuadroDeLaPersonaAMano();
        Recontar();

        // La linea del desplegable lleva lo que le falta al documento: sin rehacerla seguiria
        // diciendo «sin ninguna persona leída» de un documento que ya tiene a alguien dentro.
        RehacerLaLineaDelDocumento();
    }
}
