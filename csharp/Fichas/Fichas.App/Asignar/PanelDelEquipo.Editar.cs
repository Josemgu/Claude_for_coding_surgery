using Fichas.Contratos.Modelos;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace Fichas.App.Asignar;

/// <summary>
/// La mitad del panel que edita a un compañero: el nombre, en el propio renglón.
/// </summary>
/// <remarks>
/// <para>Del dueño, 2026-09-16: <i>«En Asignar debe dar la opción de eliminar, editar o
/// desactivar agentes»</i>. Medido ese día: desactivar y eliminar existían; editar no.
/// El rol y la categoría ya se cambiaban en el sitio con sus desplegables (desde el
/// 2026-09-05) y siguen igual; lo que faltaba era el <b>nombre</b>, y es lo que abre este
/// botón. Todo va por <see cref="PuestosDelEquipo.Editar"/>, que es la única puerta.</para>
///
/// <para>Se edita en el renglón y no en un cuadro: el panel es un flyout que no detiene el
/// trabajo (requisito 9), y un cuadro encima de un flyout es justo lo que se evita en
/// <see cref="BotonDeEliminar"/>.</para>
///
/// <para>Va en un archivo aparte porque <c>PanelDelEquipo.cs</c> ya pasaba de 300 líneas.</para>
/// </remarks>
public sealed partial class PanelDelEquipo
{
    /// <summary>A quién se le está editando el nombre ahora mismo; nulo si a nadie. Sobrevive al <see cref="Repintar"/>.</summary>
    private long? _editando;

    /// <summary>Abre la caja del nombre en el renglón de ese compañero.</summary>
    /// <param name="companero">A quién afecta el botón.</param>
    private Button BotonDeEditar(Companero companero)
    {
        var boton = new Button { Content = "Editar", FontSize = 12, IsEnabled = _editando != companero.Id };
        AutomationProperties.SetName(boton, "Editar a " + companero.Nombre);
        ToolTipService.SetToolTip(
            boton,
            "Cambia su nombre. El rol y la categoría se cambian en los desplegables de al lado. "
            + "Lo que ya firmó y lo que lleva asignado no se toca.");

        boton.Click += (_, _) =>
        {
            _editando = companero.Id;
            Repintar();
        };

        return boton;
    }

    /// <summary>La caja del nombre con Guardar y Cancelar; Entrar guarda y Escape cancela.</summary>
    /// <param name="companero">El compañero tal como está, para rellenar la caja.</param>
    private FrameworkElement FormularioDelNombre(Companero companero)
    {
        var caja = new TextBox { Text = companero.Nombre, MinWidth = 220 };
        AutomationProperties.SetName(caja, "Nombre de " + companero.Nombre);

        var guardar = new Button { Content = "Guardar", FontSize = 12 };
        AutomationProperties.SetName(guardar, "Guardar el nombre de " + companero.Nombre);
        guardar.Click += (_, _) => GuardarElNombre(companero, caja.Text);

        var cancelar = new Button { Content = "Cancelar", FontSize = 12 };
        AutomationProperties.SetName(cancelar, "Cancelar la edición de " + companero.Nombre);
        cancelar.Click += (_, _) => CerrarLaEdicion();

        // Las dos formas de confirmar hacen lo MISMO y por el mismo camino, igual que en la
        // fecha de viaje de Revisar: escribir y que Entrar no haga nada se descubre tarde.
        caja.KeyDown += (_, tecla) =>
        {
            if (tecla.Key == VirtualKey.Enter) { tecla.Handled = true; GuardarElNombre(companero, caja.Text); }
            else if (tecla.Key == VirtualKey.Escape) { tecla.Handled = true; CerrarLaEdicion(); }
        };

        var fila = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        fila.Children.Add(caja);
        fila.Children.Add(guardar);
        fila.Children.Add(cancelar);

        caja.Loaded += (_, _) => { caja.Focus(FocusState.Programmatic); caja.SelectAll(); };
        return fila;
    }

    /// <summary>Escribe el nombre por la única puerta y dice en una línea qué pasó; si no cambió nada, solo cierra.</summary>
    /// <param name="companero">El compañero tal como estaba.</param>
    /// <param name="nombreNuevo">Lo que hay en la caja.</param>
    private void GuardarElNombre(Companero companero, string nombreNuevo)
    {
        var resultado = _puestos.Editar(companero.Id, nombreNuevo, companero.Rol, companero.Categoria);
        if (!resultado.SeEscribio)
        {
            // Sin aviso es «no había nada que cambiar»: se cierra sin decir nada. Con aviso
            // —nombre en blanco, compañero que ya no está— la franja ya lo dice y la caja
            // se queda abierta para corregirlo.
            if (!resultado.HayAvisos) CerrarLaEdicion();
            return;
        }

        var nombreLimpio = (nombreNuevo ?? string.Empty).Trim();
        _acusar($"{companero.Nombre} ahora se llama {nombreLimpio}. "
                + "Lo que ya firmó y lo que lleva asignado sigue a su nombre.");
        CerrarLaEdicion();
        Cambio?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Cierra la caja del nombre y vuelve a pintar el renglón como estaba.</summary>
    private void CerrarLaEdicion()
    {
        _editando = null;
        Repintar();
    }
}
