using Fichas.App.Importar;
using Fichas.App.Revisar;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Importar;

/// <summary>
/// Corregir, al terminar la importacion, los nombres de las carpetas que se veran en Revisar.
/// </summary>
/// <remarks>
/// <para>Peticion 6 del dueno del 2026-09-07, con sus palabras: <i>«en la ventana de
/// importar documentos, cuando carga los PDF, debe permitir editar las carpetas que se
/// mostraran en Revisar, porque a veces el sistema no pone los nombres de manera
/// correcta»</i>.</para>
///
/// <para>⚠️ <b>Lo primero que hubo que medir es que NO existe ningun «nombre de
/// carpeta».</b> <see cref="ArbolDeRevisar"/> compone los tres niveles a partir de tres
/// columnas del caso —<c>fecha_viaje</c>, <c>unidad_numero</c> y <c>unidad_nombre</c>— y
/// nada mas. Asi que «editar la carpeta» es corregir esas tres columnas de los documentos
/// que cuelgan de ella, y la comprobacion que de verdad cierra el encargo es que la
/// carpeta compuesta AQUI sea, letra por letra, la que compone Revisar.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLasCarpetasDeLaTanda : BaseDeImportacion
{
    /// <summary>
    /// La carpeta que se ensena al importar es la MISMA que Revisar compone despues.
    /// </summary>
    /// <remarks>
    /// No se compara contra un literal escrito a mano: se compara contra lo que
    /// <see cref="ArbolDeRevisar.Agrupar"/> devuelve. Un literal pasaria en verde el dia que
    /// Revisar cambiara la forma del nombre, y entonces el dueno corregiria una carpeta que
    /// alli se llama de otra manera.
    /// </remarks>
    [TestMethod]
    public void LaCarpetaQueSeEnsenaAlImportarEsLaMismaQueLaDeRevisar()
    {
        var casoId = SembrarCaso("CASP2609", "2026-09-20", "123456", "Barrio de prueba");

        var carpetas = CarpetasDeLaTanda.Componer(CasosDeLaTanda([casoId]));

        Assert.HasCount(1, carpetas);
        var carpeta = carpetas[0];
        Assert.AreEqual("Septiembre 2026", carpeta.CarpetaDelMes);
        Assert.AreEqual("Grupo del 20 de septiembre", carpeta.CarpetaDeLaFecha);
        Assert.AreEqual("123456 · Barrio de prueba", carpeta.CarpetaDeLaUnidad);
        Assert.AreEqual(
            RutaSegunRevisar(casoId),
            carpeta.Ruta,
            "Lo que se ensena al importar no coincide con lo que Revisar va a ensenar.");
    }

    /// <summary>
    /// Cambiar el nombre de la unidad reescribe los documentos de esa carpeta y solo esos.
    /// </summary>
    [TestMethod]
    public void CambiarElNombreDeLaUnidadCambiaComoSeLlamaLaCarpetaEnRevisar()
    {
        var deLaCarpeta = SembrarCaso("CASP2609", "2026-09-20", "123456", "Barrio de prueva");
        var deOtraCarpeta = SembrarCaso("BALC2609", "2026-09-20", "999999", "Otra unidad");

        var carpeta = CarpetaCon(CarpetasDeLaTanda.Componer(CasosDeLaTanda([deLaCarpeta, deOtraCarpeta])), "123456");
        var resultado = CarpetasDeLaTanda.Corregir(
            Datos.Casos, carpeta, "2026-09-20", "123456", "Rama San Juan");

        Assert.IsTrue(resultado.SeEscribio, Motivos(resultado));
        Assert.AreEqual(1, resultado.Documentos);
        Assert.AreEqual("Rama San Juan", Datos.Casos.Obtener(deLaCarpeta)!.UnidadNombre);
        Assert.AreEqual(
            "Septiembre 2026 / Grupo del 20 de septiembre / 123456 · Rama San Juan",
            RutaSegunRevisar(deLaCarpeta),
            "Revisar no ensena el nombre corregido.");
        Assert.AreEqual(
            "Otra unidad",
            Datos.Casos.Obtener(deOtraCarpeta)!.UnidadNombre,
            "Se llevo por delante un documento de OTRA carpeta.");
    }

    /// <summary>
    /// Poner la fecha que faltaba saca el documento de «Sin fecha de viaje» y lo mete en su grupo.
    /// </summary>
    /// <remarks>
    /// Es el ejemplo entero que dio el dueno el mismo dia (apartado 4): «cuando yo lo
    /// corrija y le ponga la informacion, debe salir de Correccion y pasar al grupo de su
    /// fecha».
    /// </remarks>
    [TestMethod]
    public void PonerLaFechaQueFaltabaMueveElDocumentoASuGrupo()
    {
        var casoId = SembrarCaso("CASP2609", null, "123456", "Barrio de prueba");

        var carpeta = CarpetasDeLaTanda.Componer(CasosDeLaTanda([casoId]))[0];
        Assert.AreEqual(ArbolDeRevisar.SinFecha, carpeta.CarpetaDelMes);

        var resultado = CarpetasDeLaTanda.Corregir(
            Datos.Casos, carpeta, "2026-09-12", "123456", "Barrio de prueba");

        Assert.IsTrue(resultado.SeEscribio, Motivos(resultado));
        Assert.AreEqual(
            "Septiembre 2026 / Grupo del 12 de septiembre / 123456 · Barrio de prueba",
            RutaSegunRevisar(casoId));
    }

    /// <summary>
    /// Una fecha que no es una fecha NO se escribe, y se dice por que. Nada se adivina.
    /// </summary>
    /// <remarks>
    /// Regla permanente 1. Escribir «12 de sept» en <c>fecha_viaje</c> dejaria el documento
    /// exactamente donde estaba —en «Sin fecha de viaje», porque el arbol no la sabe leer—
    /// pero con el dueno creyendo que ya la puso. Un arreglo que parece hecho y no lo esta
    /// es peor que no ofrecerlo.
    /// </remarks>
    [TestMethod]
    public void UnaFechaQueNoEsUnaFechaNoSeEscribeYSeDicePorQue()
    {
        var casoId = SembrarCaso("CASP2609", "2026-09-20", "123456", "Barrio de prueba");
        var carpeta = CarpetasDeLaTanda.Componer(CasosDeLaTanda([casoId]))[0];

        var resultado = CarpetasDeLaTanda.Corregir(
            Datos.Casos, carpeta, "12 de sept", "123456", "Rama San Juan");

        Assert.IsFalse(resultado.SeEscribio, "Se escribio una fecha que no es una fecha.");
        Assert.AreEqual(0, resultado.Documentos);
        Assert.IsNotEmpty(resultado.Avisos);
        Assert.AreEqual(
            "2026-09-20",
            Datos.Casos.Obtener(casoId)!.FechaViaje,
            "La fecha que ya estaba se toco.");
        Assert.AreEqual(
            "Barrio de prueba",
            Datos.Casos.Obtener(casoId)!.UnidadNombre,
            "Se escribio el nombre a pesar de que la fecha no valia: la carpeta quedaria a medias.");
    }

    /// <summary>
    /// Un numero de unidad que el esquema no admite NO se escribe, y se dice por que.
    /// </summary>
    [TestMethod]
    public void UnNumeroDeUnidadQueNoEsUnNumeroDeUnidadNoSeEscribe()
    {
        var casoId = SembrarCaso("CASP2609", "2026-09-20", "123456", "Barrio de prueba");
        var carpeta = CarpetasDeLaTanda.Componer(CasosDeLaTanda([casoId]))[0];

        var resultado = CarpetasDeLaTanda.Corregir(
            Datos.Casos, carpeta, "2026-09-20", "12-34", "Rama San Juan");

        Assert.IsFalse(resultado.SeEscribio);
        Assert.AreEqual("123456", Datos.Casos.Obtener(casoId)!.UnidadNumero);
    }

    /// <summary>
    /// Corregir la carpeta NO toca ninguna otra columna del documento.
    /// </summary>
    /// <remarks>
    /// ⚠️ Vale la pena escribirla porque <c>RepositorioDeCasos.Guardar</c> de un caso que ya
    /// existe hace un UPDATE de LAS VEINTIUNA columnas. Componer el caso a mano en vez de
    /// leerlo entero y cambiarle tres campos borraria el numero de caso, el templo, el
    /// estado y la firma de quien lo marco, sin que ninguna prueba de arriba se enterase.
    /// </remarks>
    [TestMethod]
    public void CorregirLaCarpetaNoBorraNadaDeLoDemasDelDocumento()
    {
        var casoId = SembrarCaso("CASP2609", "2026-09-20", "123456", "Barrio de prueba");
        var antes = Datos.Casos.Obtener(casoId)!;

        var carpeta = CarpetasDeLaTanda.Componer(CasosDeLaTanda([casoId]))[0];
        Assert.IsTrue(
            CarpetasDeLaTanda.Corregir(Datos.Casos, carpeta, "2026-09-20", "123456", "Rama San Juan")
                .SeEscribio);

        var despues = Datos.Casos.Obtener(casoId)!;
        Assert.AreEqual(antes.NumeroCaso, despues.NumeroCaso);
        Assert.AreEqual(antes.TemploNombre, despues.TemploNombre);
        Assert.AreEqual(antes.RutaPdf, despues.RutaPdf);
        Assert.AreEqual(antes.PaginaPdf, despues.PaginaPdf);
        Assert.AreEqual(antes.CreadoEn, despues.CreadoEn);
        Assert.AreEqual(antes.CapturaManual, despues.CapturaManual);
    }

    /// <summary>Dos documentos de la misma unidad y fecha van a UNA carpeta, no a dos.</summary>
    [TestMethod]
    public void DosDocumentosDeLaMismaUnidadYFechaVanALaMismaCarpeta()
    {
        var uno = SembrarCaso("CASP2609", "2026-09-20", "123456", "Barrio de prueba");
        var otro = SembrarCaso("CASP2609", "2026-09-20", "123456", "Barrio de prueba");

        var carpetas = CarpetasDeLaTanda.Componer(CasosDeLaTanda([uno, otro]));

        Assert.HasCount(1, carpetas);
        Assert.AreEqual(2, carpetas[0].CuantosDocumentos);
    }

    /// <summary>Lo que no tiene ni fecha ni unidad cae en las dos carpetas que lo dicen.</summary>
    /// <remarks>
    /// Los nombres son los de <see cref="ArbolDeRevisar"/> y no unos propios: si aqui se
    /// llamara de otra forma, el dueno corregiria una carpeta que en Revisar no existe.
    /// </remarks>
    [TestMethod]
    public void LoQueNoTieneNiFechaNiUnidadCaeEnLasCarpetasQueLoDicen()
    {
        var casoId = SembrarCaso("CASP2609", null, null, null);

        var carpeta = CarpetasDeLaTanda.Componer(CasosDeLaTanda([casoId]))[0];

        Assert.AreEqual(ArbolDeRevisar.SinFecha, carpeta.CarpetaDelMes);
        Assert.AreEqual(ArbolDeRevisar.SinFecha, carpeta.CarpetaDeLaFecha);
        Assert.AreEqual(ArbolDeRevisar.SinUnidad, carpeta.CarpetaDeLaUnidad);
        Assert.AreEqual(RutaSegunRevisar(casoId), carpeta.Ruta);
    }

    /// <summary>Una carpeta sin ningun documento detras no se corrige: no hay que escribir.</summary>
    [TestMethod]
    public void UnaCarpetaSinDocumentosNoEscribeNada()
    {
        var resultado = CarpetasDeLaTanda.Corregir(
            Datos.Casos, new CarpetaDeLaTanda(), "2026-09-20", "123456", "Rama San Juan");

        Assert.IsFalse(resultado.SeEscribio);
        Assert.AreEqual(0, resultado.Documentos);
    }

    /// <summary>
    /// El camino entero, sin ventana: importar una tanda, corregir su carpeta y verla
    /// cambiada en Revisar.
    /// </summary>
    /// <remarks>
    /// Es la prueba que de verdad cierra el encargo, porque ninguna de las de arriba toca el
    /// motor: sin ella, la tanda podria no acordarse de que casos nacieron en ella y las
    /// carpetas saldrian vacias con todas las demas en verde.
    /// </remarks>
    [TestMethod]
    public async Task ImportarUnaTandaYCorregirSuCarpetaLaCambiaEnRevisar()
    {
        var motor = new MotorDeImportacion(Guardado, ruta =>
            [Hoja(ruta, 1, "CASP2609", [("Alguien", "12345678")], unidadNombre: "Barrio de prueva")]);

        var resumen = await motor.ImportarAsync([@"C:\escaneos\a.pdf"], _ => { }, CancellationToken.None);

        Assert.AreEqual(1, resumen.Casos);
        Assert.HasCount(1, resumen.CasosDeLaTanda, "La tanda no se acordó de qué casos nacieron en ella.");

        var casoId = resumen.CasosDeLaTanda[0];
        var carpeta = CarpetasDeLaTanda.Componer(CasosDeLaTanda(resumen.CasosDeLaTanda))[0];
        Assert.AreEqual("123456 · Barrio de prueva", carpeta.CarpetaDeLaUnidad);

        var corregido = CarpetasDeLaTanda.Corregir(
            Datos.Casos, carpeta, carpeta.FechaDeViaje, carpeta.UnidadNumero, "Rama San Juan");

        Assert.IsTrue(corregido.SeEscribio, Motivos(corregido));
        Assert.AreEqual(
            "Septiembre 2026 / Grupo del 20 de septiembre / 123456 · Rama San Juan",
            RutaSegunRevisar(casoId));
    }

    // ==================================================================
    // Utilidades de estas pruebas.
    // ==================================================================

    /// <summary>Mete un caso en la base con los tres campos que deciden su carpeta.</summary>
    private long SembrarCaso(string numeroCaso, string? fechaViaje, string? unidadNumero, string? unidadNombre)
    {
        var escrito = Datos.Casos.Guardar(new Caso
        {
            NumeroCaso = numeroCaso,
            FechaViaje = fechaViaje,
            UnidadNumero = unidadNumero,
            UnidadNombre = unidadNombre,
            TemploNombre = "Panama City, Panama",
            RutaPdf = $@"C:\escaneos\{numeroCaso}.pdf",
            PaginaPdf = 1,
            CreadoEn = "2026-09-07 09:00:00",
        });
        Assert.IsTrue(escrito.SeEscribio, "No se pudo sembrar el caso de la prueba.");
        return escrito.Id;
    }

    /// <summary>Los casos de esos ids, tal como los leeria la pantalla al acabar la tanda.</summary>
    private IReadOnlyList<Caso> CasosDeLaTanda(IReadOnlyList<long> casoIds)
        => [.. casoIds.Select(id => Datos.Casos.Obtener(id)!)];

    /// <summary>La ruta de carpetas que Revisar compone HOY para ese caso, preguntandole a el.</summary>
    private string RutaSegunRevisar(long casoId)
    {
        var caso = Datos.Casos.Obtener(casoId)!;
        var mes = ArbolDeRevisar.Agrupar(
        [
            new TarjetaDeDocumento
            {
                CasoId = caso.Id,
                FechaDeViajeIso = caso.FechaViaje ?? string.Empty,
                UnidadNumero = caso.UnidadNumero ?? string.Empty,
                Unidad = string.IsNullOrWhiteSpace(caso.UnidadNombre)
                    ? TarjetaDeDocumento.SinUnidad
                    : caso.UnidadNombre,
            },
        ])[0];

        return string.Join(" / ", mes.Carpeta, mes.Fechas[0].Carpeta, mes.Fechas[0].Unidades[0].Carpeta);
    }

    /// <summary>La carpeta de esa unidad, para no depender del orden de la lista.</summary>
    private static CarpetaDeLaTanda CarpetaCon(IReadOnlyList<CarpetaDeLaTanda> carpetas, string unidadNumero)
        => carpetas.Single(c => c.UnidadNumero == unidadNumero);

    /// <summary>Los motivos de un resultado, para que el fallo de la prueba diga cual fue.</summary>
    private static string Motivos(ResultadoDeLaCorreccion resultado)
        => string.Join(" | ", resultado.Avisos.Select(aviso => aviso.Linea));
}
