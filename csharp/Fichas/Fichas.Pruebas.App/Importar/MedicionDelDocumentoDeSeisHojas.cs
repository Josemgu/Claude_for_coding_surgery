using Fichas.App.Importar;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Lectura;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// El formulario de grupo de verdad, leído y guardado: cuántos casos, cuántas personas y
/// de qué hoja salió cada campo del caso.
/// </summary>
/// <remarks>
/// <para><b>Por qué existe.</b> Las demás pruebas de esta carpeta usan hojas de mentira, que
/// contestan lo que yo les diga. Esta lee el PDF de seis hojas del dueño con el lector de
/// verdad y lo guarda con el guardado de verdad en una base SQLite de verdad. Es la única
/// que puede contestar «de qué hoja salió cada campo» sobre el papel y no sobre mi idea del
/// papel.</para>
///
/// <para>⚠️ <b>Son datos personales.</b> No están en el repositorio y no se copian a él. Sin
/// ellos la prueba se declara NO CONCLUYENTE con su motivo, no falla: fallar diría «hay un
/// defecto» donde solo hay «no tengo el material». Lo que se imprime va con los dígitos
/// enmascarados.</para>
///
/// <para><b>Cuesta OCR de verdad</b> —seis hojas, unos 60 s—, y por eso es UNA sola prueba
/// que mide las tres cosas de una lectura, en vez de tres que lean el documento tres veces.</para>
/// </remarks>
[TestClass]
public sealed class MedicionDelDocumentoDeSeisHojas : BaseDeImportacion
{
    private const string CarpetaDeLosDocumentos =
        @"C:\Users\josem\.claude\uploads\e38428f3-e062-41e5-92f2-566aacd26e92";

    private const string ElDeSeisHojas = "cb2f18be-SURB2609_Suriname_Group_Complete.pdf";

    /// <summary>
    /// Importa el documento de seis hojas y dice, contra la base, qué salió de dónde.
    /// </summary>
    [TestMethod]
    public void ElDocumentoDeSeisHojasDiceDeQueHojaSaleCadaCampo()
    {
        var ruta = Path.Combine(CarpetaDeLosDocumentos, ElDeSeisHojas);
        if (!File.Exists(ruta))
        {
            Assert.Inconclusive(
                $"No está «{ElDeSeisHojas}» en «{CarpetaDeLosDocumentos}». "
                + "Son datos personales del dueño y no viven en el repositorio.");
        }

        var modelos = CarpetaDeLosModelos();
        if (modelos is null)
        {
            Assert.Inconclusive(
                "No están los cuatro modelos de OCR en la salida de Fichas.App. "
                + "Compile la solución entera antes de medir.");
        }

        using var lectura = new LecturaDePdf(modelos);
        var hojas = new LectorDeFormularios(lectura).LeerDocumento(ruta);

        foreach (var hoja in hojas)
        {
            Console.WriteLine(
                $"LEÍDO · pág {hoja.Pagina} · caso=«{Enmascarar(DeLaHoja(hoja, "numero_caso"))}»"
                + $" · unidad n.º=«{Enmascarar(DeLaHoja(hoja, "unidad_numero"))}»"
                + $" · unidad=«{DeLaHoja(hoja, "unidad_nombre")}»"
                + $" · fecha=«{DeLaHoja(hoja, "fecha_viaje")}»"
                + $" · templo=«{DeLaHoja(hoja, "templo_nombre")}»");
        }

        var salida = Guardado.GuardarLasHojasDelDocumento(hojas);

        Console.WriteLine($"GUARDADO · casos: {Contar("casos")} · personas: {Contar("personas")}");
        foreach (var resultado in salida)
        {
            Console.WriteLine(
                $"GUARDADO · pág {resultado.PaginaPdf} · caso {resultado.CasoId}"
                + $" · {(resultado.CasoNuevo ? "abre" : "se une")} · personas {resultado.Personas}"
                + $" · avisos {resultado.Avisos.Count} · renglones [{string.Join(", ", resultado.Renglones)}]");

            foreach (var aviso in resultado.Avisos)
            {
                Console.WriteLine($"  AVISO · pág {resultado.PaginaPdf} · {Enmascarar(aviso.Linea)}");
            }
        }

        foreach (var casoId in salida.Where(uno => uno.CasoId is not null)
                     .Select(uno => uno.CasoId!.Value).Distinct())
        {
            var caso = Datos.Casos.Obtener(casoId)!;
            Console.WriteLine(
                $"CASO {casoId} · lo abrió la página {caso.PaginaPdf}"
                + $" · caso=«{Enmascarar(caso.NumeroCaso)}» · unidad n.º=«{Enmascarar(caso.UnidadNumero)}»"
                + $" · unidad=«{caso.UnidadNombre}» · fecha=«{caso.FechaViaje}»"
                + $" · templo=«{caso.TemploNombre}»");

            foreach (var fila in Datos.Procedencia.DeRegistro(TablaDeProcedencia.Casos, casoId))
            {
                Console.WriteLine(
                    $"  PROCEDENCIA · {fila.Campo} · origen {fila.Origen}"
                    + $" · confianza {fila.Confianza?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) ?? "—"}"
                    + $" · verificado {fila.Verificado} · sale de la página {caso.PaginaPdf}");
            }
        }

        foreach (var renglon in Datos.Ilegibles
                     .Listar(new FiltroDeIlegibles(RutaPdf: ruta), Pagina.Primera(100)).Elementos)
        {
            Console.WriteLine($"RENGLÓN · pág {renglon.PaginaPdf} · {renglon.Motivo} · caso {renglon.CasoId}");
        }

        Console.WriteLine($"AVISOS EN TODA LA TANDA: {salida.Sum(uno => uno.Avisos.Count)}");

        // Lo único que se afirma, porque es lo único que este documento demuestra: todos los
        // campos de un caso salen de UNA hoja, la que lo abrió. Lo demás se imprime para
        // poder leerlo, no para darlo por bueno con un Assert que no lo mediría.
        foreach (var casoId in salida.Where(uno => uno.CasoId is not null)
                     .Select(uno => uno.CasoId!.Value).Distinct())
        {
            var caso = Datos.Casos.Obtener(casoId)!;

            // Sin pagina no hay a que atribuir nada, y eso ya lo dice el renglon de la hoja
            // que no se pudo leer: aqui no se afirma sobre lo que no se sabe.
            if (caso.PaginaPdf is null) continue;

            var laQueAbrio = hojas.Single(hoja => hoja.Pagina == caso.PaginaPdf);
            Assert.AreEqual(DeLaHoja(laQueAbrio, "unidad_numero"), caso.UnidadNumero,
                $"el caso {casoId} lleva un número de unidad que su página no leyó.");
            Assert.AreEqual(DeLaHoja(laQueAbrio, "fecha_viaje"), caso.FechaViaje,
                $"el caso {casoId} lleva una fecha que su página no leyó.");
        }
    }

    /// <summary>
    /// Los modelos de OCR, que NO están en la salida de este proyecto de pruebas.
    /// </summary>
    /// <remarks>
    /// El paquete <c>RapidOcrNet</c> copia sus cuatro archivos a <c>models/v5</c> con un
    /// <c>.targets</c>, y un <c>.targets</c> corre en quien referencia el paquete y en quien
    /// lo referencia a él, pero no dos saltos más allá: <c>Fichas.Lectura</c> y
    /// <c>Fichas.App</c> los tienen; <c>Fichas.Pruebas.App</c>, que llega por
    /// <c>Fichas.App</c>, no. Medido: la lectura reventaba con
    /// <c>FileNotFoundException</c> nombrando los cuatro.
    /// <para>Se cogen prestados de la salida de <c>Fichas.App</c> —misma configuración, misma
    /// carpeta salvo el nombre del proyecto— en vez de añadir el paquete a este <c>.csproj</c>,
    /// que es un archivo compartido con los demás programadores de pantallas y no lo necesitan.
    /// </para>
    /// </remarks>
    private static string? CarpetaDeLosModelos()
    {
        var deLaApp = AppContext.BaseDirectory
            .Replace("Fichas.Pruebas.App", "Fichas.App", StringComparison.Ordinal);
        var carpeta = Path.Combine(deLaApp, "models", "v5");
        return Directory.Exists(carpeta) ? carpeta : null;
    }

    private static string? DeLaHoja(HojaLeida hoja, string campo)
        => hoja.Campos.FirstOrDefault(
            uno => uno.Tabla == TablaDeProcedencia.Casos && uno.Campo == campo)?.Valor;

    /// <summary>Deja los dígitos en «·»; son datos de personas de verdad.</summary>
    private static string Enmascarar(string? valor)
        => valor is null ? "" : new string([.. valor.Select(letra => char.IsDigit(letra) ? '·' : letra)]);
}
