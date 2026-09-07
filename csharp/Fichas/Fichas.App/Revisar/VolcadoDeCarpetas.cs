using System.Text;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Revisar;

/// <summary>
/// Lo único que escribe en el disco al volcar un mes: crea las carpetas y copia dentro.
/// </summary>
/// <remarks>
/// <para>Del dueño, 2026-09-05: «descargar ese conjunto de carpetas a mi escritorio en esa
/// organización». Dónde se vuelca lo elige él con el cuadro de carpeta de Windows, que es
/// <c>Fichas.App/Cascara/SelectorDeArchivos.QueCarpeta</c> y NO se escribe otro: el 2026-09-04
/// se midió que los selectores de WinRT no abren en el paquete publicado.</para>
///
/// <para><b>Nunca falla callado.</b> Un PDF que ya no está donde la base dice, o una carpeta
/// donde Windows no deja escribir, se CUENTA y sale en la línea del acuse. Lo que no se
/// pudo hacer es justo lo que el dueño necesita saber antes de mandarle el paquete a un
/// compañero.</para>
/// </remarks>
public static class VolcadoDeCarpetas
{
    /// <summary>Lo que se pone delante de un nombre que Windows tiene reservado.</summary>
    private const string DelanteDeLoReservado = "_";

    /// <summary>Cómo se llama una carpeta cuyo nombre venía vacío.</summary>
    private const string SinNombre = "sin nombre";

    /// <summary>Los nombres que Windows no deja usar para un archivo ni para una carpeta.</summary>
    private static readonly HashSet<string> LoReservado = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// Escribe el plan bajo la raíz elegida y devuelve lo que hizo y lo que no pudo.
    /// </summary>
    /// <param name="raiz">La carpeta que eligió el dueño; se crea si no está.</param>
    /// <param name="plan">Lo que hay que crear, armado por <see cref="PlanDeVolcado.Para"/>.</param>
    public static ResumenDelVolcado Volcar(string raiz, PlanDeVolcado plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raiz);
        ArgumentNullException.ThrowIfNull(plan);

        var carpetas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cuenta = new Cuenta();

        foreach (var archivo in plan.Archivos)
        {
            var carpeta = Preparar(raiz, archivo.CarpetaRelativa, carpetas, cuenta);
            if (carpeta is null) continue;
            Copiar(archivo, carpeta, cuenta);
        }

        foreach (var texto in plan.Textos)
        {
            var carpeta = Preparar(raiz, texto.CarpetaRelativa, carpetas, cuenta);
            if (carpeta is null) continue;
            Escribir(texto, carpeta, cuenta);
        }

        return new ResumenDelVolcado(plan.Mes, carpetas.Count, cuenta.Copiados, cuenta.SinOrigen, cuenta.Fallos);
    }

    /// <summary>Crea la carpeta de destino y la apunta; devuelve nulo si Windows no dejó.</summary>
    private static string? Preparar(string raiz, string relativa, HashSet<string> carpetas, Cuenta cuenta)
    {
        var carpeta = Path.Combine(raiz, relativa);
        if (carpetas.Contains(carpeta)) return carpeta;

        try
        {
            Directory.CreateDirectory(carpeta);
            carpetas.Add(carpeta);
            return carpeta;
        }
        catch (Exception fallo) when (fallo is IOException or UnauthorizedAccessException
                                      or ArgumentException or NotSupportedException or PathTooLongException)
        {
            // No se silencia: se CUENTA, y la cifra sale en la línea del acuse.
            cuenta.Fallos++;
            return null;
        }
    }

    /// <summary>Copia un documento a su carpeta; si su PDF ya no está, lo cuenta aparte.</summary>
    private static void Copiar(ArchivoAVolcar archivo, string carpeta, Cuenta cuenta)
    {
        if (string.IsNullOrWhiteSpace(archivo.RutaDeOrigen) || !File.Exists(archivo.RutaDeOrigen))
        {
            cuenta.SinOrigen++;
            return;
        }

        try
        {
            File.Copy(archivo.RutaDeOrigen, Path.Combine(carpeta, NombreSeguro(archivo.NombreDeDestino)), overwrite: true);
            cuenta.Copiados++;
        }
        catch (Exception fallo) when (fallo is IOException or UnauthorizedAccessException
                                      or ArgumentException or NotSupportedException or PathTooLongException)
        {
            cuenta.Fallos++;
        }
    }

    /// <summary>Escribe la hoja de personas de una carpeta de unidad.</summary>
    /// <remarks>
    /// Con marca de orden de bytes a propósito: sin ella el Bloc de notas de Windows abre el
    /// archivo en la página de códigos del sistema y «Ana María Pérez» sale con garabatos.
    /// </remarks>
    private static void Escribir(TextoAVolcar texto, string carpeta, Cuenta cuenta)
    {
        try
        {
            File.WriteAllText(
                Path.Combine(carpeta, NombreSeguro(texto.Nombre)), texto.Contenido, new UTF8Encoding(true));
        }
        catch (Exception fallo) when (fallo is IOException or UnauthorizedAccessException
                                      or ArgumentException or NotSupportedException or PathTooLongException)
        {
            cuenta.Fallos++;
        }
    }

    /// <summary>
    /// Un nombre de carpeta o de archivo que Windows admite, salga lo que salga del papel.
    /// </summary>
    /// <remarks>
    /// El nombre de la unidad lo lee el OCR de una etiqueta pegada: puede traer cualquier
    /// cosa. Una barra dentro del nombre crearía una carpeta de más que nadie pidió, y un
    /// nombre acabado en punto Windows lo rechaza sin decir por qué.
    /// </remarks>
    public static string NombreSeguro(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return SinNombre;

        var letras = nombre.Trim().ToCharArray();
        var prohibidos = Path.GetInvalidFileNameChars();
        for (var i = 0; i < letras.Length; i++)
        {
            if (Array.IndexOf(prohibidos, letras[i]) >= 0) letras[i] = '-';
        }

        var limpio = new string(letras).TrimEnd('.', ' ');
        if (limpio.Length == 0) return SinNombre;

        var antesDelPunto = limpio.Split('.')[0];
        return LoReservado.Contains(antesDelPunto) ? DelanteDeLoReservado + limpio : limpio;
    }

    /// <summary>Las tres cifras que se van sumando mientras se vuelca.</summary>
    private sealed class Cuenta
    {
        /// <summary>Documentos copiados de verdad.</summary>
        public int Copiados { get; set; }

        /// <summary>Documentos cuyo PDF ya no está donde la base dice.</summary>
        public int SinOrigen { get; set; }

        /// <summary>Cosas que Windows no dejó escribir.</summary>
        public int Fallos { get; set; }
    }
}

/// <summary>Lo que dejó un volcado, para decirlo en UNA línea en el acuse del pie.</summary>
/// <param name="Mes">El mes que se volcó: «Septiembre 2026».</param>
/// <param name="Carpetas">Cuántas carpetas de unidad se crearon.</param>
/// <param name="Copiados">Cuántos documentos se copiaron.</param>
/// <param name="SinOrigen">Cuántos no tenían su PDF en el disco.</param>
/// <param name="Fallos">Cuántas escrituras rechazó Windows.</param>
public sealed record ResumenDelVolcado(string Mes, int Carpetas, int Copiados, int SinOrigen, int Fallos)
{
    /// <summary>La línea que se enseña en el acuse: cifras, y lo que no se pudo por delante.</summary>
    public string Linea
    {
        get
        {
            var hecho = $"«{Mes}»: {Plural.Con(Carpetas, "carpeta", "carpetas")}, "
                + Plural.Con(Copiados, "documento copiado", "documentos copiados");
            if (SinOrigen > 0) hecho += $"; {SinOrigen} sin su PDF en el disco";
            if (Fallos > 0) hecho += $"; {Fallos} no se pudo escribir";
            return hecho + ".";
        }
    }
}
