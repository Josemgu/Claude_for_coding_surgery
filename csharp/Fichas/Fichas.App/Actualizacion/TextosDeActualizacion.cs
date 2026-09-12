using Fichas.Contratos.Modelos;

namespace Fichas.App.Actualizacion;

/// <summary>
/// Lo que la franja dice de cada resultado de la búsqueda, y cuándo se calla.
/// </summary>
/// <remarks>
/// <para>Las frases son las del pase del 2026-09-11, letra a letra. Al arrancar solo se
/// habla cuando hay algo que hacer: una versión nueva, o la clave que falta o no vale. Sin
/// red, o si algo raro pasó, el arranque se calla y queda la línea del cuaderno; a mano
/// —desde «Buscar actualización»— se contesta siempre, aunque sea para decir que no hay
/// nada nuevo.</para>
///
/// <para>⛔ Ninguna frase lleva la clave: los motivos que llegan aquí ya vienen limpios de
/// <see cref="ServidorDeReleasesDeGitHub"/>, y aquí no se añade nada que la contenga.</para>
/// </remarks>
public static class TextosDeActualizacion
{
    /// <summary>El rótulo del botón de la franja cuando hay versión nueva.</summary>
    public static readonly string ElBotonDeActualizar = "Actualizar ahora";

    /// <summary>Si el aviso de este resultado lleva el botón de actualizar: solo con versión nueva.</summary>
    /// <param name="hallazgo">Lo que devolvió la búsqueda.</param>
    public static bool LlevaBotonDeActualizar(ResultadoDeLaBusqueda hallazgo)
        => hallazgo.Que == QueSeEncontro.HayVersionNueva && hallazgo.Instalador is not null;

    /// <summary>El aviso para la franja, o nulo si con este resultado no se dice nada.</summary>
    /// <param name="hallazgo">Lo que devolvió la búsqueda.</param>
    /// <param name="versionActual">La versión abierta, para «Tienes la última versión, v11».</param>
    /// <param name="aMano">Verdadero si lo pidió el dueño con el botón; entonces se contesta siempre.</param>
    public static Aviso? AvisoDe(ResultadoDeLaBusqueda hallazgo, string versionActual, bool aMano) => hallazgo.Que switch
    {
        QueSeEncontro.HayVersionNueva => Aviso.Informa(
            $"Hay una versión nueva: {hallazgo.Etiqueta}",
            string.Empty,
            $"Tienes abierta la v{versionActual}. Con «{ElBotonDeActualizar}» se baja el instalador de la "
            + $"{hallazgo.Etiqueta}, se comprueba y se instala encima; el programa se cierra y vuelve a abrir solo. "
            + "Sus datos no se tocan: viven en la carpeta de datos, fuera del programa."),

        QueSeEncontro.EsLaUltima when aMano => Aviso.Informa($"Tienes la última versión, v{versionActual}"),

        QueSeEncontro.FaltaLaClave => Aviso.Informa(
            $"Para que el programa se actualice solo, pega la clave en {hallazgo.RutaDeLaClave}",
            string.Empty,
            "El programa vive en un repositorio privado de GitHub y necesita una clave de solo lectura para "
            + "ver si hay una versión nueva. Se crea en GitHub (Settings → Developer settings → Fine-grained "
            + "tokens) con el permiso «Contents: Read-only» sobre el repositorio del programa, se copia y se "
            + "pega en ese archivo, sola en la primera línea. Hasta entonces el programa funciona igual; solo "
            + "no puede avisar de versiones nuevas."),

        QueSeEncontro.LaClaveNoVale => Aviso.Advierte(
            "La clave de actualización no vale",
            string.Empty,
            $"GitHub no aceptó la clave de {hallazgo.RutaDeLaClave}{ConElMotivo(hallazgo)}. Puede haber caducado o no "
            + "tener permiso sobre el repositorio del programa. Crea otra en GitHub con «Contents: Read-only» y "
            + "pégala en ese archivo."),

        QueSeEncontro.SinRed when aMano => Aviso.Advierte(
            "No se pudo consultar si hay una versión nueva: sin conexión.",
            string.Empty,
            $"Motivo: {hallazgo.Motivo}. Se vuelve a intentar al abrir el programa o con «Buscar actualización»."),

        QueSeEncontro.NoSePudo when aMano => Aviso.Advierte(
            "No se pudo comprobar si hay una versión nueva.",
            string.Empty,
            $"Motivo: {hallazgo.Motivo}."),

        _ => null,
    };

    /// <summary>« (GitHub contestó 403)» si hay motivo, o nada.</summary>
    /// <param name="hallazgo">Lo que devolvió la búsqueda.</param>
    private static string ConElMotivo(ResultadoDeLaBusqueda hallazgo)
        => string.IsNullOrEmpty(hallazgo.Motivo) ? string.Empty : $" ({hallazgo.Motivo})";
}
