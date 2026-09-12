using Fichas.App.Actualizacion;
using Fichas.Contratos.Modelos;
using Microsoft.UI.Xaml;

namespace Fichas.App.Cascara;

/// <summary>
/// La parte de la ventana que se ocupa de la actualización: preguntar al arrancar y a mano,
/// decirlo en la franja y, con el clic del dueño, bajar el instalador, lanzarlo y cerrar.
/// </summary>
/// <remarks>
/// <para>Nace el 2026-09-11 de la petición del dueño: «Haz un control de versiones que cuando
/// llegue una nueva versión se actualice todo.» El diseño está en DECISIONES.md, «EL DUEÑO
/// PIDE QUE EL PROGRAMA SE ACTUALICE SOLO». Lo que se puede comprobar sin ventana no está
/// aquí sino en <c>Fichas.App/Actualizacion/</c>; aquí solo se enchufa a la franja.</para>
///
/// <para>⛔ <b>Nunca se instala sin el clic.</b> Al arrancar solo se pregunta y, si hay
/// versión nueva, se deja el aviso con el botón. Bajar y lanzar pasan únicamente en
/// <see cref="ActualizarAhora"/>, que solo corre desde ese botón.</para>
///
/// <para>Va en un archivo aparte, como el tema, el icono y la cabecera: el <c>.xaml.cs</c> es
/// el arranque de la ventana y no se hincha.</para>
/// </remarks>
public sealed partial class VentanaPrincipal
{
    /// <summary>Si ya hay una búsqueda o una descarga en marcha, para no lanzar dos a la vez con dos clics seguidos.</summary>
    private bool _actualizandose;

    /// <summary>Puesta justo antes de <c>Close()</c> en <see cref="ActualizarAhora"/>: después de cerrar no se toca ningún control.</summary>
    private bool _cerrandoseParaActualizar;

    /// <summary>Pregunta en segundo plano al arrancar; con <c>--falso</c> o <c>--sin-actualizacion</c> no pregunta.</summary>
    /// <remarks>
    /// Se dispara y no se espera: la ventana ya está montada y activa cuando esto corre, y la
    /// respuesta llega al hilo de la ventana por el <c>await</c>, que vuelve al contexto de
    /// XAML. Un fallo dentro no puede tumbar la ventana: <c>Actualizador</c> nunca lanza.
    /// </remarks>
    private void BuscarActualizacionAlArrancar()
    {
        if (!_servicios.Argumentos.SeBuscaActualizacionAlArrancar) return;
        _ = BuscarYDecirlo(aMano: false);
    }

    /// <summary>El botón «Buscar actualización» de la cabecera: pregunta y contesta siempre.</summary>
    /// <param name="quien">El botón; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private async void AlPulsarBuscarActualizacion(object quien, RoutedEventArgs cuando)
        => await BuscarYDecirlo(aMano: true);

    /// <summary>Pregunta a GitHub y deja en la franja lo que toque, con botón si hay versión nueva.</summary>
    /// <param name="aMano">Verdadero si lo pidió el dueño con el botón; entonces se contesta aunque no haya nada.</param>
    private async Task BuscarYDecirlo(bool aMano)
    {
        if (_actualizandose) return;
        _actualizandose = true;
        _botonDeBuscarActualizacion.IsEnabled = false;
        try
        {
            if (aMano) _acuse.Decir("Preguntando si hay una versión nueva…");

            var hallazgo = await _servicios.Actualizador.Buscar();
            var aviso = TextosDeActualizacion.AvisoDe(hallazgo, _servicios.Actualizador.VersionActual, aMano);
            if (aviso is null) return;

            if (TextosDeActualizacion.LlevaBotonDeActualizar(hallazgo))
                _servicios.Avisos.Dejar(aviso, new AccionDelAviso(TextosDeActualizacion.ElBotonDeActualizar, () => _ = ActualizarAhora(hallazgo)));
            else
                _servicios.Avisos.Dejar(aviso);
        }
        finally
        {
            _actualizandose = false;
            _botonDeBuscarActualizacion.IsEnabled = true;
        }
    }

    /// <summary>
    /// «Actualizar ahora»: baja el instalador a la temporal, comprueba tamaño y huella, lo
    /// lanza y cierra el programa. Si algo no cuadra, no lanza nada y lo dice.
    /// </summary>
    /// <param name="hallazgo">La versión nueva que se encontró, con su instalador.</param>
    private async Task ActualizarAhora(ResultadoDeLaBusqueda hallazgo)
    {
        if (_actualizandose) return;
        _actualizandose = true;
        _botonDeBuscarActualizacion.IsEnabled = false;
        try
        {
            _servicios.Avisos.Dejar(Aviso.Informa(
                $"Descargando la {hallazgo.Etiqueta}… El programa se cerrará solo para instalarla.",
                string.Empty,
                $"Se baja {hallazgo.Instalador?.Nombre} ({EnMegas(hallazgo.Instalador?.Tamano ?? 0)}) a la carpeta temporal de Windows. "
                + "Antes de lanzarlo se comprueban su tamaño y su huella SHA-256 contra lo que declara GitHub."));

            var descarga = await _servicios.Actualizador.DescargarElInstalador(hallazgo);
            if (!descarga.Lista)
            {
                _servicios.Avisos.Dejar(Aviso.Problema(
                    "No se instaló nada: el instalador que se bajó no cuadra o no llegó entero.",
                    string.Empty,
                    $"Motivo: {descarga.Motivo}. No se ha lanzado nada. Puede volver a intentarlo con «Buscar actualización» "
                    + "o bajar el instalador a mano del Release de GitHub."));
                return;
            }

            if (!descarga.HuellaComprobada)
            {
                _servicios.Avisos.Dejar(Aviso.Advierte(
                    "GitHub no dio la huella del instalador: solo se comprobó el tamaño.",
                    string.Empty,
                    "La API no devolvió «digest» para este activo. El tamaño cuadra con el declarado y se instala igual."));
            }

            if (!_servicios.Actualizador.LanzarElInstalador(descarga))
            {
                _servicios.Avisos.Dejar(Aviso.Problema(
                    "El instalador se bajó y cuadra, pero Windows no lo dejó arrancar.",
                    string.Empty,
                    $"Quedó en {descarga.Ruta}. Puede abrirlo con doble clic: instala la versión nueva encima de esta."));
                return;
            }

            // El instalador ya corre. Cerrar la ventana libera la base (App engancha Closed) y
            // deja al instalador la carpeta del programa; al terminar, su [Run] vuelve a abrir
            // Fichas con la misma carpeta de datos. Después de Close() no se toca ningún control
            // —la ventana ya no existe—, por eso la bandera se deja puesta y se sale aquí.
            _servicios.Registro.Anotar($"ACTUALIZACION  se cierra el programa para instalar {hallazgo.Etiqueta}");
            _cerrandoseParaActualizar = true;
            Close();
        }
        finally
        {
            if (!_cerrandoseParaActualizar)
            {
                _actualizandose = false;
                _botonDeBuscarActualizacion.IsEnabled = true;
            }
        }
    }

    /// <summary>«96,1 MB» para decir cuánto se va a bajar.</summary>
    /// <param name="bytes">El tamaño declarado por GitHub.</param>
    private static string EnMegas(long bytes)
        => string.Create(System.Globalization.CultureInfo.GetCultureInfo("es-ES"), $"{bytes / 1024.0 / 1024.0:F1} MB");
}
