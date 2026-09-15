using Fichas.App.Importar;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Lectura;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// Un papel que el lector no conoce queda como ilegible, sin abrir caso ni pegarse a nadie.
/// </summary>
/// <remarks>
/// <para>Medido el 2026-09-15 con el lector real sobre un impreso sintético de otra clase
/// (fondos de ayuda, en francés, con un código «FORD2610» impreso): salen <b>9 de 9
/// etiquetas ausentes</b> y un solo campo, <c>numero_caso = FORD2610</c>, leído del texto
/// porque tiene la forma de un número de caso. Con eso, hoy nace un caso «FORD2610 · sin
/// unidad leída · 0 personas», que es lo que el dueño vio en su lista.</para>
///
/// <para>La señal de «no es un formulario de recomendación» es que la extracción no
/// encontró NINGUNA de sus nueve etiquetas impresas: cada una que falta deja un aviso con
/// su clave en <c>Campo</c> (<c>Fichas.Lectura.Extraccion.ProponerCamposDelCaso</c>). Un
/// formulario de verdad mal escaneado encuentra alguna; el de las páginas del revés del
/// dueño encontró la unidad y la fecha aunque leyera basura debajo.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelFormularioDesconocido : BaseDeImportacion
{
    /// <summary>Una hoja con solo el número leído y los avisos de las etiquetas que faltan.</summary>
    /// <param name="ruta">El archivo.</param>
    /// <param name="pagina">La hoja, base 1.</param>
    /// <param name="numeroLeido">El token con forma de número de caso que se leyó del texto.</param>
    /// <param name="etiquetasQueFaltan">Cuántas de las nueve etiquetas NO se encontraron.</param>
    private static HojaLeida HojaConEtiquetasQueFaltan(string ruta, int pagina, string numeroLeido, int etiquetasQueFaltan)
    {
        var avisos = Etiquetas.CamposDelFormulario
            .Take(etiquetasQueFaltan)
            .Select(campo => Aviso.Advierte($"No se encontró en el papel la etiqueta «{campo}».", campo))
            .ToList();

        return new HojaLeida(
            RutaPdf: ruta,
            Pagina: pagina,
            Campos:
            [
                new CampoPropuesto(TablaDeProcedencia.Casos, "numero_caso", numeroLeido, OrigenDeCampo.Ocr, 1.0, null, null, ValorOcr: numeroLeido),
                new CampoPropuesto(TablaDeProcedencia.Casos, "fecha_viaje", null, OrigenDeCampo.Vacio, null, null, null),
                new CampoPropuesto(TablaDeProcedencia.Casos, "unidad_numero", null, OrigenDeCampo.Vacio, null, null, null),
                new CampoPropuesto(TablaDeProcedencia.Casos, "unidad_nombre", null, OrigenDeCampo.Vacio, null, null, null),
                new CampoPropuesto(TablaDeProcedencia.Casos, "templo_nombre", null, OrigenDeCampo.Vacio, null, null, null),
            ],
            Avisos: avisos,
            Ilegible: null,
            CapturaManual: true,
            LineasLeidas: 8,
            TextoLeido: "Demande et approbation Code: " + numeroLeido,
            Segundos: 0.1);
    }

    /// <summary>Los renglones guardados para esa ruta.</summary>
    /// <param name="ruta">El archivo, tal como se importó.</param>
    private IReadOnlyList<RenglonIlegible> RenglonesDe(string ruta)
        => Datos.Ilegibles.Listar(new FiltroDeIlegibles(RutaPdf: ruta), Pagina.Primera(50)).Elementos;

    /// <summary>
    /// Dada una hoja sin ninguna de las nueve etiquetas y con un token con forma de número;
    /// cuando se guarda; entonces no nace caso y queda un renglón que dice qué se leyó.
    /// </summary>
    [TestMethod]
    public void UnaHojaSinNingunaEtiquetaNoAbreCasoYQuedaComoIlegible()
    {
        var ruta = PapelDePrueba.Escribir(Path.Combine(Carpeta, "escaner"), "ajeno.pdf", "Demande FORD2610");
        Guardado.EmpezarUnaTanda();
        var resultado = Guardado.GuardarLasHojasDelDocumento(
            [HojaConEtiquetasQueFaltan(ruta, 1, "FORD2610", Etiquetas.CamposDelFormulario.Count)]);

        Assert.IsFalse(resultado[0].Entro);
        Assert.AreEqual(0L, Contar("casos"), "un impreso de otra clase no es un caso.");
        var renglon = RenglonesDe(ruta).Single();
        Assert.AreEqual(MotivosDeIlegible.FormularioDesconocido, renglon.Motivo);
        Assert.IsNull(renglon.CasoId, "no se pega a ningún caso.");
        Assert.AreEqual(1, renglon.PaginaPdf);
        Assert.Contains("FORD2610", renglon.Detalle ?? string.Empty, "lo que se leyó se conserva en el renglón.");
    }

    /// <summary>
    /// Dada una hoja en la que se encontró aunque sea UNA etiqueta; cuando se guarda; entonces
    /// sigue entrando como caso: un formulario mal escaneado no se tira.
    /// </summary>
    [TestMethod]
    public void UnaHojaConAlgunaEtiquetaSigueEntrandoComoCaso()
    {
        var ruta = PapelDePrueba.Escribir(Path.Combine(Carpeta, "escaner"), "flojo.pdf", "CASP2609");
        Guardado.EmpezarUnaTanda();
        var resultado = Guardado.GuardarLasHojasDelDocumento(
            [HojaConEtiquetasQueFaltan(ruta, 1, "CASP2609", Etiquetas.CamposDelFormulario.Count - 1)]);

        Assert.IsTrue(resultado[0].Entro);
        Assert.AreEqual(1L, Contar("casos"));
        Assert.IsFalse(RenglonesDe(ruta).Any(renglon => renglon.Motivo == MotivosDeIlegible.FormularioDesconocido));
    }

    /// <summary>
    /// Dado un PDF de tres hojas —dos formularios y un impreso ajeno—; cuando se guarda;
    /// entonces nacen dos casos, cada uno con su hoja, y la ajena queda aparte sin caso.
    /// </summary>
    [TestMethod]
    public void EnUnPdfDeVariasHojasLaHojaAjenaNoSePegaALasDemas()
    {
        var ruta = PapelDePrueba.Escribir(Path.Combine(Carpeta, "escaner"), "lote.pdf", "PULC2609", "SANM2609", "Demande FORD2610");
        Guardado.EmpezarUnaTanda();
        var resultados = Guardado.GuardarLasHojasDelDocumento(
        [
            Hoja(ruta, 1, "PULC2609", [("Ana Prueba", "055-1111-2221")]),
            Hoja(ruta, 2, "SANM2609", [("Dora Prueba", "055-4444-5554")], unidadNumero: "345678"),
            HojaConEtiquetasQueFaltan(ruta, 3, "FORD2610", Etiquetas.CamposDelFormulario.Count),
        ]);

        Assert.AreEqual(2L, Contar("casos"));
        Assert.IsTrue(resultados[0].Entro && resultados[1].Entro && !resultados[2].Entro);
        var pulc = Datos.Casos.Obtener(resultados[0].CasoId!.Value)!;
        var sanm = Datos.Casos.Obtener(resultados[1].CasoId!.Value)!;
        Assert.Contains("PULC2609", PapelDePrueba.LoQueDiceLaHoja(pulc.RutaPdf, pulc.PaginaPdf));
        Assert.Contains("SANM2609", PapelDePrueba.LoQueDiceLaHoja(sanm.RutaPdf, sanm.PaginaPdf));
        var ajeno = RenglonesDe(ruta).Single(renglon => renglon.Motivo == MotivosDeIlegible.FormularioDesconocido);
        Assert.AreEqual(3, ajeno.PaginaPdf);
        Assert.IsNull(ajeno.CasoId);
    }

    /// <summary>Un PDF que no abre ningún caso no deja copia en la carpeta de datos: no hay documento que la use.</summary>
    [TestMethod]
    public void UnPdfQueNoAbreCasoNoDejaCopia()
    {
        var ruta = PapelDePrueba.Escribir(Path.Combine(Carpeta, "escaner"), "ajeno.pdf", "Demande FORD2610");
        Guardado.EmpezarUnaTanda();
        Guardado.GuardarLasHojasDelDocumento(
            [HojaConEtiquetasQueFaltan(ruta, 1, "FORD2610", Etiquetas.CamposDelFormulario.Count)]);

        Assert.IsFalse(Directory.Exists(Copias.Carpeta) && Directory.GetFiles(Copias.Carpeta).Length > 0,
            "sin caso no hay copia.");
    }

    /// <summary>El código nuevo se lee en español en la pantalla, no como jerga.</summary>
    [TestMethod]
    public void ElMotivoSeLeeEnEspanol()
    {
        Assert.AreNotEqual(MotivosDeIlegible.FormularioDesconocido, MotivosDeIlegible.EnEspanol(MotivosDeIlegible.FormularioDesconocido));
        Assert.AreNotEqual(MotivosDeIlegible.PapelPerdido, MotivosDeIlegible.EnEspanol(MotivosDeIlegible.PapelPerdido));
    }
}
