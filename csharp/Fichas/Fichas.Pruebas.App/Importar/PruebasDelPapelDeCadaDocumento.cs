using Fichas.App.Cascara;
using Fichas.App.Importar;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// Un documento nunca enseña el papel de otro, aunque el archivo del que salió cambie.
/// </summary>
/// <remarks>
/// <para>El defecto, con las palabras del dueño (2026-09-15, sobre la v13): <i>«un PDF se
/// queda pegado y se carga a lugares que no le corresponde»</i>. Medido en una base propia
/// con PDF sintéticos y la ventana abierta: el escáner escribe SIEMPRE <c>Scan.pdf</c>, el
/// caso guarda esa RUTA y no el papel, y cuando el archivo se sobrescribe con otro
/// formulario, todos los documentos que salieron de esa ruta enseñan el papel nuevo. Cuatro
/// casos con <c>Scan.pdf|1</c> y el visor de los cuatro con el impreso ajeno FORD2610.</para>
///
/// <para>Los criterios, en Given/When/Then, están en cada prueba. Todas se escribieron en
/// rojo antes del arreglo.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelPapelDeCadaDocumento : BaseDeImportacion
{
    /// <summary>La carpeta donde «el escáner» deja siempre el mismo archivo.</summary>
    private string Escaner => Path.Combine(Carpeta, "escaner");

    /// <summary>
    /// Dado un PDF importado; cuando el archivo se sobrescribe con otro papel y se importa
    /// otra vez; entonces cada documento sigue abriendo SU papel, y el segundo no es
    /// duplicado del primero.
    /// </summary>
    [TestMethod]
    public void ElPapelDeUnDocumentoNoCambiaAunqueElArchivoSeSobrescriba()
    {
        var ruta = PapelDePrueba.Escribir(Escaner, "Scan.pdf", "PULC2609");
        Guardado.EmpezarUnaTanda();
        var primera = Guardado.GuardarLasHojasDelDocumento(
            [Hoja(ruta, 1, "PULC2609", [("Ana Prueba", "055-1111-2221")])]);

        PapelDePrueba.Escribir(Escaner, "Scan.pdf", "DEJC2608");
        Guardado.EmpezarUnaTanda();
        var segunda = Guardado.GuardarLasHojasDelDocumento(
            [Hoja(ruta, 1, "DEJC2608", [("Beto Prueba", "055-2222-3332")], fechaViaje: "2026-08-30", unidadNumero: "234567")]);

        var pulc = Datos.Casos.Obtener(primera[0].CasoId!.Value)!;
        var dejc = Datos.Casos.Obtener(segunda[0].CasoId!.Value)!;

        Assert.Contains("PULC2609", PapelDePrueba.LoQueDiceLaHoja(pulc.RutaPdf, pulc.PaginaPdf),
            "el papel que abre PULC2609 tiene que seguir siendo el suyo, no el que hay ahora en Scan.pdf.");
        Assert.Contains("DEJC2608", PapelDePrueba.LoQueDiceLaHoja(dejc.RutaPdf, dejc.PaginaPdf));
        Assert.IsNull(segunda[0].DuplicadoDe, "otro papel en la misma ruta NO es un duplicado.");
    }

    /// <summary>
    /// Dado un PDF importado; cuando se importa otra vez sin cambiar; entonces el segundo
    /// entra avisado como duplicado del primero y los dos abren el mismo papel.
    /// </summary>
    [TestMethod]
    public void ReimportarElMismoArchivoAvisaDuplicadoYNoCambiaElPapelDeNadie()
    {
        var ruta = PapelDePrueba.Escribir(Escaner, "Scan.pdf", "DEJC2608");
        Guardado.EmpezarUnaTanda();
        var primera = Guardado.GuardarLasHojasDelDocumento([Hoja(ruta, 1, "DEJC2608", [("Beto Prueba", null)])]);
        Guardado.EmpezarUnaTanda();
        var segunda = Guardado.GuardarLasHojasDelDocumento([Hoja(ruta, 1, "DEJC2608", [("Beto Prueba", null)])]);

        var original = Datos.Casos.Obtener(primera[0].CasoId!.Value)!;
        var repetido = Datos.Casos.Obtener(segunda[0].CasoId!.Value)!;

        Assert.AreEqual(original.Id, segunda[0].DuplicadoDe, "la misma hoja del mismo archivo sigue siendo duplicado.");
        Assert.AreEqual(original.RutaPdf, repetido.RutaPdf, "el mismo papel es la misma ruta.");
        Assert.Contains("DEJC2608", PapelDePrueba.LoQueDiceLaHoja(original.RutaPdf, original.PaginaPdf));
    }

    /// <summary>
    /// Dado un PDF importado; entonces el caso guarda una copia en la carpeta de datos, no
    /// la ruta del escáner, y el archivo del escáner sigue donde estaba, sin tocar.
    /// </summary>
    [TestMethod]
    public void ElCasoGuardaSuCopiaEnLaCarpetaDeDatosYElOriginalNoSeToca()
    {
        var ruta = PapelDePrueba.Escribir(Escaner, "Scan.pdf", "PULC2609");
        var bytesAntes = File.ReadAllBytes(ruta);
        Guardado.EmpezarUnaTanda();
        var resultado = Guardado.GuardarLasHojasDelDocumento([Hoja(ruta, 1, "PULC2609", [("Ana Prueba", null)])]);

        var caso = Datos.Casos.Obtener(resultado[0].CasoId!.Value)!;
        Assert.IsNotNull(caso.RutaPdf);
        Assert.IsTrue(caso.RutaPdf.StartsWith(Copias.Carpeta, StringComparison.OrdinalIgnoreCase), "la copia va dentro de la carpeta de datos.");
        Assert.IsTrue(File.Exists(caso.RutaPdf), "la copia tiene que existir.");
        Assert.AreNotEqual(ruta, caso.RutaPdf, StringComparer.OrdinalIgnoreCase);
        CollectionAssert.AreEqual(bytesAntes, File.ReadAllBytes(ruta), "el archivo del escáner no se toca.");
        Assert.AreEqual(1, resultado[0].PaginaPdf);
    }

    /// <summary>
    /// Dado que la copia no se puede guardar; cuando se importa; entonces el documento entra
    /// igual con el archivo original y lo avisa (requisito 9: avisar, nunca impedir).
    /// </summary>
    [TestMethod]
    public void SiNoSePuedeGuardarLaCopiaElDocumentoEntraConElOriginalYAvisa()
    {
        var ruta = PapelDePrueba.Escribir(Escaner, "Scan.pdf", "PULC2609");
        // Un ARCHIVO donde tendría que ir la carpeta de datos: ahí no se puede crear «escaneos».
        var dondeNoSePuede = Path.Combine(Carpeta, "no-es-una-carpeta.txt");
        File.WriteAllText(dondeNoSePuede, "ocupado");
        var guardado = new GuardadoDeHojas(
            Datos.Casos, Datos.Personas, Datos.Procedencia, Datos.Ilegibles,
            new RelojDelSistema(), new CopiaDelEscaneo(dondeNoSePuede), () => new AmbitoDeGuardadoSobreSqlite(Conexion));

        guardado.EmpezarUnaTanda();
        var resultado = guardado.GuardarLasHojasDelDocumento([Hoja(ruta, 1, "PULC2609", [("Ana Prueba", null)])]);

        Assert.IsTrue(resultado[0].Entro, "la hoja entra igual.");
        var caso = Datos.Casos.Obtener(resultado[0].CasoId!.Value)!;
        Assert.AreEqual(ruta, caso.RutaPdf, "sin copia, se queda con el original.");
        Assert.IsTrue(resultado[0].Avisos.Any(aviso => aviso.Linea.Contains("copia", StringComparison.OrdinalIgnoreCase)),
            "tiene que decir que no pudo guardar la copia.");
    }

    /// <summary>
    /// Dado un PDF que no se pudo leer; entonces su renglón lleva la ruta ORIGINAL, que es
    /// la que el dueño busca en su carpeta para ir a mirarlo.
    /// </summary>
    [TestMethod]
    public void ElRenglonDeLoQueNoSeLeyoConservaLaRutaOriginal()
    {
        var ruta = PapelDePrueba.Escribir(Escaner, "Scan.pdf", "nada legible");
        Guardado.EmpezarUnaTanda();
        Guardado.GuardarLasHojasDelDocumento([HojaIlegible(ruta, "El OCR no leyó ni una línea.")]);

        var renglones = Datos.Ilegibles.Listar(new Contratos.Consultas.FiltroDeIlegibles(RutaPdf: ruta), Contratos.Consultas.Pagina.Primera(10));
        Assert.HasCount(1, renglones.Elementos, "el renglón se busca por la ruta original y tiene que estar.");
    }

    /// <summary>
    /// Dada la misma ficha en dos carpetas distintas, byte a byte igual y con otro nombre;
    /// cuando se importan las dos; entonces la segunda entra avisada como duplicado de la
    /// primera aunque el lector no haya leído ninguna cédula.
    /// </summary>
    /// <remarks>
    /// Es lo que había en la base del dueño el 2026-09-15, medido allí: cuatro pares de casos
    /// con el mismo archivo en «HAITI OCtubre\» y en «Octubre\», sin marca de duplicado. Y entraron
    /// en UNA sola tanda (la carpeta raíz entera, 66 documentos), que es como se importan aquí:
    /// una tanda, dos documentos. Hasta hoy el índice por hoja se armaba una vez por tanda y no
    /// veía los casos que nacían dentro de ella.
    /// </remarks>
    [TestMethod]
    public void LaMismaFichaEnDosCarpetasEsUnDuplicadoAunqueCambieElNombre()
    {
        var enOctubre = PapelDePrueba.Escribir(Path.Combine(Carpeta, "Octubre"), "ficha.pdf", "FORD2610");
        var enHaiti = Path.Combine(Carpeta, "HAITI Octubre", "ficha (copia).pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(enHaiti)!);
        File.Copy(enOctubre, enHaiti);

        Guardado.EmpezarUnaTanda();
        var primera = Guardado.GuardarLasHojasDelDocumento([Hoja(enOctubre, 1, "FORD2610", [("Sin cédula", null)])]);
        var segunda = Guardado.GuardarLasHojasDelDocumento([Hoja(enHaiti, 1, "FORD2610", [("Sin cédula", null)])]);

        Assert.AreEqual(primera[0].CasoId, segunda[0].DuplicadoDe, "el mismo papel es el mismo papel, se llame como se llame.");
    }

    /// <summary>
    /// Dado el mismo archivo importado dos veces; entonces en la carpeta de datos hay UNA copia,
    /// no dos: el mismo papel es el mismo archivo.
    /// </summary>
    [TestMethod]
    public void ElMismoPapelSeCopiaUnaSolaVez()
    {
        var ruta = PapelDePrueba.Escribir(Escaner, "Scan.pdf", "DEJC2608");
        Guardado.EmpezarUnaTanda();
        Guardado.GuardarLasHojasDelDocumento([Hoja(ruta, 1, "DEJC2608", [("Beto Prueba", null)])]);
        Guardado.EmpezarUnaTanda();
        Guardado.GuardarLasHojasDelDocumento([Hoja(ruta, 1, "DEJC2608", [("Beto Prueba", null)])]);

        Assert.HasCount(1, Directory.GetFiles(Copias.Carpeta, "*.pdf"));
    }
}
