using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Lectura;

namespace Fichas.Pruebas.Lectura;

/// <summary>
/// La huella SHA-256 de lo que la lectura devuelve de un documento: la firma con la que se
/// comprueba que un cambio en el motor no cambió ni un campo leído.
/// </summary>
/// <remarks>
/// <para>Entra TODO lo que sale de <see cref="LectorDeFormularios.LeerDocumento"/> y que
/// depende del OCR o de la extracción: cada campo con su valor, origen, confianza, banda,
/// fila, valor de OCR, tachón y marca de revisión; cada aviso; el renglón de ilegible; la
/// captura manual; cuántas líneas leyó el OCR; y el texto leído entero, que es la prueba
/// más fina de que el motor devolvió los mismos caracteres.</para>
///
/// <para>Se dejan fuera, a propósito, la ruta (lleva el nombre de una persona) y los
/// segundos (cambian en cada máquina y en cada pasada). Los números van en cultura
/// invariante y con formato <c>R</c>: un <c>double</c> que cambie en el último bit cambia
/// la huella, que es lo que se quiere.</para>
/// </remarks>
public static class HuellaDeLaLectura
{
    /// <summary>La huella del documento entero: SHA-256 en hexadecimal de sus hojas, en orden.</summary>
    /// <param name="hojas">Las hojas tal como las devolvió el lector.</param>
    public static string DelDocumento(IReadOnlyList<HojaLeida> hojas)
    {
        var texto = new StringBuilder();
        foreach (var hoja in hojas) EscribirHoja(texto, hoja);
        return Sha256(texto.ToString());
    }

    /// <summary>La huella de un corpus: SHA-256 de las huellas de sus documentos, en el orden dado.</summary>
    /// <param name="huellasDeLosDocumentos">Las huellas de <see cref="DelDocumento"/>, una por documento.</param>
    public static string DelCorpus(IEnumerable<string> huellasDeLosDocumentos)
        => Sha256(string.Join("\n", huellasDeLosDocumentos));

    /// <summary>Una hoja como texto canónico: cabecera, campos, avisos y el texto leído.</summary>
    /// <param name="texto">Donde se escribe.</param>
    /// <param name="hoja">La hoja leída.</param>
    private static void EscribirHoja(StringBuilder texto, HojaLeida hoja)
    {
        texto.Append(CultureInfo.InvariantCulture,
            $"pag={hoja.Pagina}|captura={hoja.CapturaManual}|lineas={hoja.LineasLeidas}|ilegible={hoja.Ilegible?.Motivo}\n");
        foreach (var campo in hoja.Campos) EscribirCampo(texto, campo);
        foreach (var aviso in hoja.Avisos) EscribirAviso(texto, aviso);
        texto.Append("texto=").Append(hoja.TextoLeido).Append('\n');
    }

    /// <summary>Un campo propuesto, con sus nueve datos y su banda.</summary>
    /// <param name="texto">Donde se escribe.</param>
    /// <param name="campo">El campo propuesto.</param>
    private static void EscribirCampo(StringBuilder texto, CampoPropuesto campo)
    {
        texto.Append(CultureInfo.InvariantCulture,
            $"  campo|{campo.Tabla}|{campo.Campo}|{campo.Valor}|{campo.Origen}|{campo.Confianza:R}|{Banda(campo.Banda)}|{campo.FilaFormulario}|{campo.ValorOcr}|{campo.AnuladoPorTachon}|{campo.NecesitaRevision}\n");
    }

    /// <summary>Un aviso con su gravedad, su línea, su campo y su detalle.</summary>
    /// <param name="texto">Donde se escribe.</param>
    /// <param name="aviso">El aviso.</param>
    private static void EscribirAviso(StringBuilder texto, Aviso aviso)
        => texto.Append(CultureInfo.InvariantCulture, $"  aviso|{aviso.Gravedad}|{aviso.Linea}|{aviso.Campo}|{aviso.Detalle}\n");

    /// <summary>La banda como cuatro fracciones con todos sus decimales, o vacío si no hay.</summary>
    /// <param name="banda">La banda del campo, o nula.</param>
    private static string Banda(BandaDeLaPagina? banda)
        => banda is null
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"{banda.Value.X0:R},{banda.Value.Y0:R},{banda.Value.X1:R},{banda.Value.Y1:R}");

    /// <summary>SHA-256 del texto en UTF-8, en hexadecimal en mayúsculas.</summary>
    /// <param name="texto">El texto canónico.</param>
    private static string Sha256(string texto)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
}
