using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Fichas.App.Correccion;

/// <summary>
/// Una fila de la correccion: etiqueta, valor, la palabra de su estado, y el motivo.
/// </summary>
/// <remarks>
/// Portado de <c>interfaz/campo.py</c>. Lo que decide qué se pinta esta en
/// <see cref="ModeloDeCorreccion"/> y en <see cref="EstadosDeCampo"/>, que se prueban sin
/// ventana; aqui solo se pinta.
/// <para>
/// <b>Se revalida al soltar cada tecla, no al guardar.</b> Un campo que se pone rojo media
/// hora despues, cuando ya se pulso Guardar, obliga a volver a buscar donde estaba el error.
/// </para>
/// </remarks>
public sealed partial class FichaDeCampo : UserControl
{
    private ModeloDeCorreccion? _modelo;
    private bool _pintando;

    /// <summary>Monta la ficha vacia.</summary>
    public FichaDeCampo() => InitializeComponent();

    /// <summary>Que campo pinta esta ficha; nulo hasta que se le da uno.</summary>
    public CampoEnPantalla? Campo { get; private set; }

    /// <summary>Salta al teclear, para que la pantalla vuelva a contar.</summary>
    public event EventHandler? Tecleo;

    /// <summary>Salta al tomar el foco, para que el visor ilumine la banda del campo.</summary>
    public event EventHandler? TomoElFoco;

    /// <summary>Salta cuando Miguel pulsa «Esta bien». Nunca salta solo.</summary>
    public event EventHandler? PidioFirmar;

    /// <summary>Salta cuando Miguel marca o desmarca «No está en el papel».</summary>
    /// <remarks>Lleva si quedo marcada, para que la pantalla no tenga que mirar el control.</remarks>
    public event EventHandler<bool>? PidioMarcarQueNoEstaEnElPapel;

    /// <summary>Lo que dice el motivo ahora mismo; lo lee la medicion sin desmontar nada.</summary>
    public string LoQueDiceElMotivo => _motivo.Text;

    /// <summary>La palabra del estado ahora mismo.</summary>
    public string LoQueDiceLaPalabra => _palabra.Text;

    /// <summary>Ata la ficha a un campo del modelo y la pinta por primera vez.</summary>
    public void Mostrar(ModeloDeCorreccion modelo, CampoEnPantalla campo)
    {
        _modelo = modelo;
        Campo = campo;

        PonerLosNombresParaElLector(campo);

        _pintando = true;
        _etiqueta.Text = campo.Etiqueta;
        _enElPapel.Text = campo.EtiquetaDelPapel;
        _valor.Text = campo.ValorGuardado ?? string.Empty;
        _valor.IsReadOnly = campo.SoloLectura;
        // Un campo que no se puede tocar tampoco gasta una parada de tabulacion.
        _valor.IsTabStop = !campo.SoloLectura;

        // ⚠️ El boton de firma va SIEMPRE, tambien en el campo que no se edita. Hasta el
        // 2026-09-05 se escondia, y con eso el numero de caso no se podia dar por bueno
        // NUNCA: el documento no llegaba entero a «listo para asignar» ni aunque Miguel
        // firmara todo lo demas. Firmar es dar por bueno el valor, no cambiarlo.
        // La casilla de «no está en el papel», en cambio, no aplica a un campo que no se
        // edita: el numero de caso siempre esta en la hoja.
        _noEstaEnElPapel.Visibility = campo.SoloLectura ? Visibility.Collapsed : Visibility.Visible;
        _pintando = false;

        Refrescar();
    }

    /// <summary>Vuelve a decidir el estado y a repintar la palabra, el motivo y lo leido.</summary>
    public void Refrescar()
    {
        if (_modelo is null || Campo is null) return;

        var estado = _modelo.EstadoDe(Campo);
        _palabra.Text = EstadosDeCampo.PalabraEnPantalla(estado, Campo.Procedencia);
        _palabra.Foreground = new SolidColorBrush(TintaDe(estado));
        _descripcion.Text = EstadosDeCampo.Descripcion(estado, Campo.Procedencia);

        // La casilla se repinta desde el almacen, nunca desde lo que se pulso: si la
        // escritura no entro, la casilla tiene que volver a como esta la base de verdad.
        var ausente = _modelo.EstaMarcadoComoAusente(Campo);
        _pintando = true;
        _noEstaEnElPapel.IsChecked = ausente;
        _pintando = false;

        // Un campo que no está en el papel no pide dato: el cuadro se apaga y no gasta una
        // parada de tabulacion. Se vuelve a encender al desmarcarlo.
        _valor.IsEnabled = !ausente;

        var motivo = _modelo.MotivoDe(Campo);
        _motivo.Text = motivo ?? string.Empty;
        _motivo.Visibility = motivo is null ? Visibility.Collapsed : Visibility.Visible;

        // El borde de «no valido» es mas grueso a proposito: asi la distincion sobrevive en
        // escala de grises y en una impresion, donde los cuatro fondos claros se parecen.
        _valor.BorderBrush = new SolidColorBrush(TintaDe(estado));
        _valor.BorderThickness = new Thickness(estado == EstadoDeCampo.NoValido ? 2 : 1);

        // Se mira el valor CARGADO y no lo tecleado: con lo tecleado la linea desapareceria
        // al pulsar la primera tecla, que es justo cuando Miguel la esta copiando.
        var leido = EstadosDeCampo.LineaDeLoQueSeLeyo(Campo.ValorGuardado, Campo.Procedencia);
        _loLeido.Text = leido;
        _loLeido.Visibility = leido.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        var firmado = Campo.Procedencia?.Verificado == true;
        _botonDeFirma.Content = firmado ? "firmado" : "Está bien";
        // Un campo marcado como que no está en el papel no se firma: no hay dato que dar
        // por bueno, y esa marca ya es la respuesta.
        _botonDeFirma.IsEnabled = !firmado && !ausente;

        // El estado tambien se dice con palabras: para quien no ve la pantalla, un boton
        // apagado no se distingue de uno que no esta.
        AutomationProperties.SetName(
            _botonDeFirma,
            firmado ? $"Ya dado por bueno: {Campo.NombreParaElLector}" : Campo.NombreDelBotonDeFirma);
    }

    /// <summary>
    /// Le pone a cada control el nombre y el identificador de SU campo.
    /// </summary>
    /// <remarks>
    /// Se hace aqui y no en el XAML porque en el XAML no se puede: la plantilla del
    /// repetidor es una sola y su <c>x:Name</c> —<c>_valor</c>— viaja identico a las doce
    /// copias. Ese es el defecto que QA midio: doce cuadros con el mismo identificador y sin
    /// nombre, y once botones llamados todos «Esta bien».
    /// </remarks>
    private void PonerLosNombresParaElLector(CampoEnPantalla campo)
    {
        AutomationProperties.SetName(_valor, campo.NombreParaElLector);
        AutomationProperties.SetAutomationId(_valor, campo.Clave);
        AutomationProperties.SetName(_botonDeFirma, campo.NombreDelBotonDeFirma);
        AutomationProperties.SetAutomationId(_botonDeFirma, $"firma:{campo.Clave}");
        AutomationProperties.SetName(_noEstaEnElPapel, campo.NombreDeLaCasillaDeAusente);
        AutomationProperties.SetAutomationId(_noEstaEnElPapel, $"ausente:{campo.Clave}");
        AutomationProperties.SetName(this, campo.NombreParaElLector);
    }

    /// <summary>La tinta de cada estado; el color acompana a la palabra, no la sustituye.</summary>
    private static Windows.UI.Color TintaDe(EstadoDeCampo estado) => estado switch
    {
        EstadoDeCampo.Anotacion => Windows.UI.Color.FromArgb(255, 0x12, 0x6B, 0x3A),
        EstadoDeCampo.Ocr => Windows.UI.Color.FromArgb(255, 0x5A, 0x5F, 0x66),
        EstadoDeCampo.Revisar => Windows.UI.Color.FromArgb(255, 0x8A, 0x5A, 0x00),
        EstadoDeCampo.NoValido => Windows.UI.Color.FromArgb(255, 0xB3, 0x26, 0x1E),
        // «No está en el papel» va en el gris de lo que ya no pide nada, no en el ambar de
        // lo que hay que mirar: es una respuesta cerrada, no una alarma abierta.
        EstadoDeCampo.NoEstaEnElPapel => Windows.UI.Color.FromArgb(255, 0x5A, 0x5F, 0x66),
        _ => Windows.UI.Color.FromArgb(255, 0x8A, 0x5A, 0x00),
    };

    /// <summary>Apunta lo tecleado en el modelo y repinta en el acto.</summary>
    private void AlTeclear(object quien, TextChangedEventArgs cuando)
    {
        if (_pintando || _modelo is null || Campo is null) return;
        _modelo.Teclear(Campo.Clave, _valor.Text);
        Refrescar();
        Tecleo?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Avisa de que este campo tiene el foco, para que se ilumine su banda.</summary>
    private void AlTomarElFoco(object quien, RoutedEventArgs cuando) => TomoElFoco?.Invoke(this, EventArgs.Empty);

    /// <summary>Pide firmar. Regla permanente 5: esto solo ocurre porque Miguel lo pulso.</summary>
    private void AlPulsarLaFirma(object quien, RoutedEventArgs cuando) => PidioFirmar?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Pide marcar o desmarcar «No está en el papel». NUNCA salta al repintar.
    /// </summary>
    /// <remarks>
    /// El guardia <see cref="_pintando"/> es lo que separa «Miguel pulso la casilla» de
    /// «la pantalla la puso como esta la base». Sin el, repintar escribiria en el almacen.
    /// </remarks>
    private void AlCambiarSiEstaEnElPapel(object quien, RoutedEventArgs cuando)
    {
        if (_pintando || _modelo is null || Campo is null) return;
        PidioMarcarQueNoEstaEnElPapel?.Invoke(this, _noEstaEnElPapel.IsChecked == true);
    }
}
