using Fichas.Lectura;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Fichas.Pruebas.Lectura;

/// <summary>
/// Leer un documento abre el PDF con PdfPig UNA vez, lo rasteriza una vez por hoja y no
/// pasa la imagen por PNG para dárselo al OCR.
/// </summary>
/// <remarks>
/// <para>Criterio (plan R-3 del 2026-09-15): <b>dado</b> un PDF de seis hojas, <b>cuando</b>
/// se lee entero con <see cref="LectorDeFormularios.LeerDocumento"/>, <b>entonces</b> PdfPig
/// lo abre una sola vez (antes: 1 + 6×3 = 19), PDFium rasteriza seis veces (una por hoja) y
/// ninguna hoja se decodifica desde PNG (antes: una por hoja). Y <b>dado</b> una sola hoja
/// pedida con <see cref="LectorDeFormularios.LeerHoja(string, int)"/>, <b>entonces</b> una
/// apertura (antes: 3).</para>
///
/// <para>Las cuentas las lleva <see cref="LecturaDePdf"/> en tres contadores internos
/// que solo existen para esto. El PDF se fabrica aquí con PdfPig: seis hojas tamaño carta
/// con un rótulo cada una, sin datos de nadie. El OCR corre de verdad sobre las seis, así
/// que la prueba cuesta unos segundos por hoja; es el precio de contar sobre el camino
/// real y no sobre un doble.</para>
/// </remarks>
[TestClass]
public class PruebaDeUnaSolaPasadaPorElPdf
{
    /// <summary>Cuántas hojas tiene el PDF fabricado: las mismas que el documento real de seis del plan.</summary>
    private const int Hojas = 6;

    /// <summary>Fabrica un PDF de <see cref="Hojas"/> hojas con un rótulo por hoja y lo deja en una carpeta temporal.</summary>
    /// <returns>La ruta del archivo; quien llama lo borra.</returns>
    private static string PdfDeSeisHojas()
    {
        var constructor = new PdfDocumentBuilder();
        var tipografia = constructor.AddStandard14Font(Standard14Font.Helvetica);
        for (int hoja = 1; hoja <= Hojas; hoja++)
        {
            constructor.AddPage(PageSize.Letter).AddText($"HOJA {hoja}", 24, new PdfPoint(60, 700), tipografia);
        }
        var ruta = Path.Combine(Path.GetTempPath(), $"fichas-una-pasada-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(ruta, constructor.Build());
        return ruta;
    }

    /// <summary>Dado un PDF de seis hojas, cuando se lee entero, entonces una apertura, seis rasterizaciones y cero decodificaciones de PNG.</summary>
    [TestMethod]
    public void LeerElDocumentoAbreElPdfUnaVezYNoPasaPorPng()
    {
        var ruta = PdfDeSeisHojas();
        try
        {
            using var lectura = new LecturaDePdf();
            var hojas = new LectorDeFormularios(lectura).LeerDocumento(ruta);

            Assert.HasCount(Hojas, hojas, "Tienen que salir las seis hojas: sin eso las cuentas no comparan nada.");
            Assert.AreEqual(1, lectura.AperturasConPdfPig, "PdfPig tiene que abrir el documento UNA vez por lectura entera (antes eran 19).");
            Assert.AreEqual(Hojas, lectura.RasterizacionesConPdfium, "PDFium rasteriza una vez por hoja, ni una más.");
            Assert.AreEqual(0, lectura.DecodificacionesDePng, "El OCR recibe el mapa de bits de PDFium tal cual: ninguna hoja se decodifica desde PNG.");
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    /// <summary>Dado un PDF, cuando se pide una sola hoja por ruta y número, entonces una apertura y una rasterización.</summary>
    [TestMethod]
    public void LeerUnaHojaSueltaAbreElPdfUnaVez()
    {
        var ruta = PdfDeSeisHojas();
        try
        {
            using var lectura = new LecturaDePdf();
            var hoja = new LectorDeFormularios(lectura).LeerHoja(ruta, Hojas);

            Assert.AreEqual(Hojas, hoja.Pagina);
            Assert.IsNull(hoja.Ilegible, "La última hoja existe y se rasteriza: no puede volver ilegible.");
            Assert.AreEqual(1, lectura.AperturasConPdfPig, "Una hoja suelta abre el PDF una vez (antes eran 3).");
            Assert.AreEqual(1, lectura.RasterizacionesConPdfium);
            Assert.AreEqual(0, lectura.DecodificacionesDePng);
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    /// <summary>Dado un archivo que no es PDF, cuando se lee entero, entonces vuelve UNA hoja ilegible con el motivo de siempre y sin rasterizar nada.</summary>
    [TestMethod]
    public void UnArchivoQueNoEsPdfSigueVolviendoComoUnaHojaIlegible()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"fichas-no-es-pdf-{Guid.NewGuid():N}.pdf");
        File.WriteAllText(ruta, "esto no es un PDF");
        try
        {
            using var lectura = new LecturaDePdf();
            var hojas = new LectorDeFormularios(lectura).LeerDocumento(ruta);

            Assert.HasCount(1, hojas);
            Assert.AreEqual(0, hojas[0].Pagina);
            Assert.AreEqual("El archivo no se pudo abrir como PDF.", hojas[0].Ilegible?.Motivo);
            Assert.AreEqual(0, lectura.RasterizacionesConPdfium);
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    /// <summary>Dado un PDF de seis hojas, cuando se pide la hoja siete por ruta y número, entonces vuelve ilegible con el motivo de siempre, sin lanzar.</summary>
    [TestMethod]
    public void UnaHojaFueraDeRangoVuelveIlegibleComoAntes()
    {
        var ruta = PdfDeSeisHojas();
        try
        {
            using var lectura = new LecturaDePdf();
            var hoja = new LectorDeFormularios(lectura).LeerHoja(ruta, Hojas + 1);

            Assert.AreEqual(Hojas + 1, hoja.Pagina);
            Assert.AreEqual("La hoja no se pudo convertir en imagen.", hoja.Ilegible?.Motivo);
            Assert.IsEmpty(hoja.Campos);
        }
        finally
        {
            File.Delete(ruta);
        }
    }
}
