using Fichas.App.Cascara;
using Fichas.Contratos.Modelos;
using Fichas.Lectura;
using Microsoft.UI.Xaml;

namespace Fichas.App.Importar;

/// <summary>
/// La zona de la pantalla de importar que comprueba que cada documento tiene su papel.
/// </summary>
/// <remarks>
/// <para>Es el arreglo de datos del 2026-09-15 para una base que ya tiene el daño, en su
/// sitio de la pantalla. Aquí no se decide nada: quién pierde el papel, quién lo conserva,
/// quién lo recupera y quién queda marcado lo decide <see cref="ComprobacionDeLosPapeles"/>,
/// que se prueba sin ventana. Esto pinta, encadena los tres pasos y lo cuenta.</para>
///
/// <para>⚠️ El OCR corre en otro hilo (<c>Task.Run</c> alrededor de
/// <see cref="ComprobacionDeLosPapeles.Leer"/>, que no toca la base) y las escrituras vuelven
/// a este, que es el que abrió la conexión: la misma regla que
/// <see cref="MotorDeImportacion.ImportarAsync"/>.</para>
/// </remarks>
public sealed partial class PaginaDeImportar
{
    /// <summary>Si hay una comprobación en marcha; el botón se apaga mientras tanto.</summary>
    private bool _comprobandoLosPapeles;

    /// <summary>
    /// Lee la base y el disco —sin OCR— y dice qué hay que comprobar; enciende el botón solo
    /// si hay algo.
    /// </summary>
    /// <param name="servicios">Los puertos de la ventana, ya montados.</param>
    private void PintarLosPapeles(Cascara.Servicios servicios)
    {
        if (servicios.SonDatosInventados || servicios.ObtenerElLector() is null)
        {
            _lineaDeLosPapeles.Text = "Con datos de ejemplo no hay base que comprobar.";
            _botonDeComprobarLosPapeles.IsEnabled = false;
            return;
        }

        var plan = Comprobacion(servicios).Planear();
        _lineaDeLosPapeles.Text = plan.HayAlgo
            ? plan.Linea + "."
            : "Nada que comprobar: ningún documento comparte papel con otro, ninguno ha perdido su escaneo y no hay repetidos sin marcar. " + plan.Linea + ".";
        _botonDeComprobarLosPapeles.IsEnabled = plan.HayAlgo && !_comprobandoLosPapeles;
    }

    /// <summary>La comprobación atada a los puertos y al lector de verdad.</summary>
    /// <param name="servicios">Los puertos de la ventana.</param>
    private static ComprobacionDeLosPapeles Comprobacion(Cascara.Servicios servicios)
        => new(
            servicios.Casos, servicios.Ilegibles, servicios.Reloj,
            new CopiaDelEscaneo(servicios.Argumentos.CarpetaDeDatos),
            (ruta, pagina) => NumeroDeCasoQueLee(servicios.ObtenerElLector(), ruta, pagina));

    /// <summary>El número de caso que el lector de formularios lee en esa hoja, o nulo.</summary>
    /// <remarks>Es el mismo lector de la importación, hoja a hoja: el número sale de donde sale siempre.</remarks>
    /// <param name="lector">El lector de formularios; nulo devuelve nulo.</param>
    /// <param name="ruta">El archivo.</param>
    /// <param name="pagina">La hoja, base 1.</param>
    private static string? NumeroDeCasoQueLee(LectorDeFormularios? lector, string ruta, int pagina)
    {
        if (lector is null) return null;
        var hoja = lector.LeerHoja(ruta, pagina);
        return new CamposDeLaHoja(hoja.Campos).ValorDe(CamposDeLaHoja.CampoNumeroCaso);
    }

    /// <summary>Planea, lee en otro hilo, aplica en este, y lo cuenta en la franja, el pie y el cuaderno.</summary>
    /// <param name="quien">El control que disparó el evento; no se usa.</param>
    /// <param name="cuando">Los datos del evento; no se usan.</param>
    private async void AlComprobarLosPapeles(object quien, RoutedEventArgs cuando)
        => await SinTragarseNadaAsync("Comprobar los papeles", async () =>
        {
            var servicios = Servicios;
            if (servicios is null || _comprobandoLosPapeles) return;

            var comprobacion = Comprobacion(servicios);
            var plan = comprobacion.Planear();
            if (!plan.HayAlgo)
            {
                PintarLosPapeles(servicios);
                return;
            }

            _comprobandoLosPapeles = true;
            _botonDeComprobarLosPapeles.IsEnabled = false;
            _lineaDeLosPapeles.Text = $"Leyendo {plan.Disputas.Count + plan.Perdidos.Count} archivos con el lector… {plan.Linea}.";
            try
            {
                var lecturas = await Task.Run(() => comprobacion.Leer(plan)).ConfigureAwait(true);
                var resultado = comprobacion.Aplicar(lecturas);
                ContarLoDeLosPapeles(servicios, resultado);
            }
            finally
            {
                _comprobandoLosPapeles = false;
            }

            PintarLosPapeles(servicios);
            if (_ultimaTanda is not null) PintarLoDeDespuesDeLaTanda(servicios);
        });

    /// <summary>Deja el resultado en la franja, en el pie y en el cuaderno, renglón a renglón.</summary>
    /// <param name="servicios">Por donde se llega a la franja y al cuaderno.</param>
    /// <param name="resultado">Lo que hizo la comprobación.</param>
    private static void ContarLoDeLosPapeles(Cascara.Servicios servicios, ResultadoDeLosPapeles resultado)
    {
        foreach (var renglon in resultado.RenglonesParaElCuaderno) servicios.Registro.Anotar(renglon);
        servicios.Registro.Anotar($"PAPELES  {resultado.Linea}");

        servicios.Avisos.Dejar([Aviso.Informa(resultado.Linea), .. resultado.Avisos]);
        (App.Ventana as VentanaPrincipal)?.AcuseDelPie.Decir(resultado.Linea);
    }
}
