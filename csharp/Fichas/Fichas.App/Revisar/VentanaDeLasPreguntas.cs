using Fichas.App.Cascara;
using Fichas.Contratos.Modelos;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Fichas.App.Revisar;

/// <summary>
/// La ventana de un documento: sus personas, una a una, con sus seis preguntas.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es una ventana de verdad y no un cuadro.</b> El dueño lo pidió así el 2026-09-05:
/// <i>«ahí debe poder dar clic al documento, abrir otra ventana donde está la información,
/// comentarios o preguntas»</i>. No es modal: Revisar sigue viva detrás, se pueden tener
/// varias abiertas a la vez, y ninguna detiene el programa (requisito 9: ni un cuadro
/// modal).
/// </para>
/// <para>
/// <b>Un botón de guardar por PERSONA, y no uno para todo el documento.</b> Cada persona es
/// un ticket aparte: un documento de diez puede tener tres resueltas y siete no, y guardar
/// las diez de golpe firmaría a nombre de quien miró tres.
/// </para>
/// <para>
/// ⛔ <b>Aquí no se firma ningún campo.</b> «Todo correcto» —
/// <c>procedencia_campo.verificado</c>— es de Miguel, campo por campo, y vive en Corrección
/// (regla permanente 5). Esta ventana no conoce <c>IProcedencia</c>.
/// </para>
/// <para>
/// Se monta en código y no en XAML porque el número de personas y el de preguntas por
/// persona los pone el documento: una plantilla fija no sirve para diez personas. Todo lo
/// que se LEE sale de <see cref="PreguntasDeUnDocumento"/>, que se prueba sin abrir nada.
/// </para>
/// </remarks>
public sealed class VentanaDeLasPreguntas : Window
{
    private readonly Servicios _servicios;
    private readonly AccionesDeLasPreguntas _acciones;
    private readonly long _casoId;
    private readonly ElementTheme _tema;

    private readonly StackPanel _personas = new() { Spacing = 10 };
    private readonly TextBlock _resumen = new() { FontSize = 13, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _franja = new()
    {
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        Visibility = Visibility.Collapsed,
    };

    /// <summary>Abre la ventana de un documento con el tema que tenga la principal.</summary>
    public VentanaDeLasPreguntas(Servicios servicios, long casoId, ElementTheme tema)
    {
        _servicios = servicios;
        _casoId = casoId;
        _tema = tema;
        _acciones = new AccionesDeLasPreguntas(servicios.Personas, servicios.Companeros);

        Content = Montar();
        PonerElIcono();
        Repintar();
    }

    /// <summary>Lo que se lee cuando el documento ya no está: no se abre una ventana vacía.</summary>
    public const string DocumentoQueYaNoEsta =
        "Este documento ya no está en la base. Puede que se haya borrado desde otra ventana.";

    /// <summary>La raíz de la ventana: cabecera, la lista de personas y la franja de avisos.</summary>
    private FrameworkElement Montar()
    {
        var raiz = new Grid { Padding = new Thickness(16), RowSpacing = 10, RequestedTheme = _tema };
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        raiz.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var cabecera = new StackPanel { Spacing = 2 };
        cabecera.Children.Add(_resumen);
        Grid.SetRow(cabecera, 0);
        raiz.Children.Add(cabecera);

        var lista = new ScrollViewer
        {
            Content = _personas,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        AutomationProperties.SetName(lista, "Personas de este documento");
        Grid.SetRow(lista, 1);
        raiz.Children.Add(lista);

        // ⚠️ Sin `AutomationProperties.SetName` A PROPOSITO, y esto esta medido: con un
        // nombre puesto, quien lee la ventana por accesibilidad oia el rotulo fijo —«Lo que
        // pasó al guardar»— EN VEZ del mensaje. Un aviso que no se puede leer no es un
        // aviso. Lo que se anuncia es su texto, y `LiveSetting` hace que se anuncie solo
        // cuando cambia, sin robarle el foco a nadie.
        AutomationProperties.SetLiveSetting(_franja, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        Grid.SetRow(_franja, 2);
        raiz.Children.Add(_franja);

        return raiz;
    }

    /// <summary>Lee la base y vuelve a pintar el documento entero.</summary>
    /// <remarks>
    /// Se relee entero después de cada guardado, y no se retoca el control a mano: así lo
    /// que se ve es lo que hay ESCRITO, y no lo que la pantalla cree haber escrito.
    /// </remarks>
    private void Repintar()
    {
        var caso = _servicios.Casos.Obtener(_casoId);
        if (caso is null)
        {
            Title = "Las seis preguntas";
            _resumen.Text = DocumentoQueYaNoEsta;
            _personas.Children.Clear();
            return;
        }

        var personas = _servicios.Personas.DeCaso(_casoId);
        var documento = PreguntasDeUnDocumento.De(
            caso,
            personas,
            _servicios.Personas.FirmasDeLosPasosDelCaso(_casoId),
            _servicios.Companeros.Activos(),
            _acciones.QuienContesta());

        Title = documento.Titulo;
        _resumen.Text = $"{documento.Cabecera}\n{documento.Resumen}";

        _personas.Children.Clear();
        foreach (var ticket in documento.Personas)
        {
            _personas.Children.Add(TarjetaDe(ticket));
        }

        if (documento.Personas.Count == 0)
        {
            _personas.Children.Add(new TextBlock
            {
                Text = "De este documento no se sacó ninguna persona, así que no hay preguntas que contestar.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.85,
            });
        }
    }

    /// <summary>La tarjeta de una persona: quién es, cómo está, quién contestó y sus seis.</summary>
    private FrameworkElement TarjetaDe(TicketDeUnaPersona ticket)
    {
        var caja = new StackPanel { Spacing = 4 };

        caja.Children.Add(new TextBlock
        {
            Text = $"{ticket.DeQuien} · {ticket.Cedula}",
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });

        caja.Children.Add(new TextBlock
        {
            Text = ticket.FraseDelEstado,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
        });

        // ⛔ Criterio C19-9: quién contestó se dice ANTES de dejar cambiarlo. Sobrescribir
        // la respuesta de un agente sin que se vea que era suya es la forma de que nadie
        // sepa nunca de quién se fía.
        caja.Children.Add(new TextBlock
        {
            Text = ticket.LineaDeLaFirma,
            FontSize = 12,
            Opacity = 0.85,
            FontWeight = ticket.LoContestoOtro
                ? Microsoft.UI.Text.FontWeights.SemiBold
                : Microsoft.UI.Text.FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
        });

        var desplegables = new List<ComboBox>();
        foreach (var pregunta in ticket.Preguntas)
        {
            var fila = new Grid { ColumnSpacing = 8 };
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

            var rotulo = new TextBlock
            {
                Text = pregunta.Rotulo,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            };
            Grid.SetColumn(rotulo, 0);
            fila.Children.Add(rotulo);

            var desplegable = DesplegableDe(ticket, pregunta);
            desplegables.Add(desplegable);
            Grid.SetColumn(desplegable, 1);
            fila.Children.Add(desplegable);

            caja.Children.Add(fila);
        }

        var guardar = new Button
        {
            Content = "Guardar las seis",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        AutomationProperties.SetName(guardar, $"Guardar las seis preguntas de {ticket.DeQuien}");
        guardar.Click += (_, _) => Guardar(ticket, desplegables);
        caja.Children.Add(guardar);

        return new Border
        {
            Child = caja,
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray),
        };
    }

    /// <summary>El desplegable de una pregunta, con sus TRES respuestas.</summary>
    /// <remarks>
    /// «Sin mirar» va la primera y se puede volver a ella: si contestar fuera irreversible,
    /// nadie se atrevería a contestar (criterio C19-6).
    /// </remarks>
    private static ComboBox DesplegableDe(TicketDeUnaPersona ticket, PreguntaDeUnaPersona pregunta)
    {
        var desplegable = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };

        foreach (var respuesta in PreguntasDeUnDocumento.LasTresRespuestas)
        {
            desplegable.Items.Add(new ComboBoxItem
            {
                Content = PreguntasDeUnDocumento.DecirLaRespuesta(respuesta),
                Tag = respuesta,
            });
        }

        desplegable.SelectedIndex = PreguntasDeUnDocumento.LasTresRespuestas
            .Select((r, i) => (r, i))
            .First(par => par.r == pregunta.Respuesta)
            .i;

        AutomationProperties.SetName(desplegable, $"{pregunta.Rotulo} de {ticket.DeQuien}");
        return desplegable;
    }

    /// <summary>Guarda las seis de ESA persona y vuelve a leer la base.</summary>
    private void Guardar(TicketDeUnaPersona ticket, IReadOnlyList<ComboBox> desplegables)
    {
        var elegidas = desplegables
            .Select((desplegable, i) => new PreguntaDeUnaPersona(
                ticket.Preguntas[i].Rotulo,
                (desplegable.SelectedItem as ComboBoxItem)?.Tag as bool?))
            .ToList();

        var resultado = _acciones.Guardar(ticket.PersonaId, PreguntasDeUnDocumento.ComoSeGuarda(elegidas));

        if (!resultado.SeEscribio)
        {
            Decir(resultado.Avisos);
            return;
        }

        // Se relee la base ANTES de decir cómo quedó: la frase que se enseña sale de lo
        // escrito y no de lo que esta ventana creía estar escribiendo.
        Repintar();

        var recien = _servicios.Personas.Obtener(ticket.PersonaId);
        var frase = recien is null
            ? "guardado"
            : Grupo.LasDosPreguntas.FraseDeUnaPersona(
                Grupo.LasDosPreguntas.EstadoDe(recien), Grupo.LasDosPreguntas.SeQuedoEn(recien));

        Decir([
            AccionesDeLasPreguntas.LoQueSeGuardo(
                ticket.DeQuien, frase, _acciones.QuienContesta()?.Nombre ?? "usted"),
            .. resultado.Avisos,
        ]);
    }

    /// <summary>Enseña lo que pasó, sin detener nada y sin abrir un cuadro.</summary>
    private void Decir(IReadOnlyList<Aviso> avisos)
    {
        if (avisos.Count == 0)
        {
            _franja.Visibility = Visibility.Collapsed;
            return;
        }

        _franja.Text = string.Join("\n", avisos.Select(aviso =>
            string.IsNullOrWhiteSpace(aviso.Detalle) ? aviso.Linea : $"{aviso.Linea} — {aviso.Detalle}"));
        _franja.Visibility = Visibility.Visible;
    }

    /// <summary>Pone el icono del programa; si no se puede, se anota y no se detiene nada.</summary>
    /// <remarks>
    /// Sin esto la ventana enseña el icono verde de la plantilla de Microsoft, que es lo que
    /// ya se midió con la principal el 2026-09-05: una ventana sin icono propio deja que lo
    /// elija quien pinta la barra de título.
    /// </remarks>
    private void PonerElIcono()
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");

        try
        {
            if (File.Exists(ruta)) AppWindow.SetIcon(ruta);
        }
        catch (Exception fallo) when (fallo is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _servicios.Registro.Anotar($"SIN ICONO en la ventana de las preguntas: {fallo.Message}");
        }
    }
}
