using Fichas.App.Vocabulario;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Inicio;

/// <summary>
/// La ventana de lo que no esta completo, probada SIN VENTANA (ADR-0003 §8.1).
/// </summary>
/// <remarks>
/// Sale de las palabras del dueno del 2026-09-05: <i>«Luego crea una ventana de revisar lo
/// que no está completo, y ponlo por grupo de fechas»</i>, y del motivo que dio a
/// continuacion: <i>«No quiero revisar gente que viaja en noviembre estando en
/// septiembre»</i>.
/// </remarks>
[TestClass]
public sealed class PruebasDeLoIncompleto
{
    /// <summary>
    /// Un documento al que le falta un dato sale, y dice CUANTOS le faltan.
    /// </summary>
    /// <remarks>
    /// Es su peticion 5 del mismo dia: <i>«que todas las informaciones estén colocadas en el
    /// sistema, para que se pueda generar los paquetes de los agentes con toda la
    /// información»</i>.
    /// </remarks>
    [TestMethod]
    public void UnDocumentoAlQueLeFaltaUnDatoSaleYDiceCuantosLeFaltan()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "HUEC2609", "2026-09-08", temploNombre: null);

        var resumen = BaseDeInicio.LectorDeIncompletosDe(servicios).Leer();

        var renglon = resumen.Grupos.Single().Documentos.Single();
        Assert.AreEqual("HUEC2609", renglon.NumeroCaso);
        Assert.AreEqual(1, renglon.CuantoLeFalta);
        Assert.AreEqual(DosEstados.MeFalta, renglon.PalabraDelEstado);
        StringAssert.Contains(renglon.DetalleDelEstado, "le falta 1 dato");
        Assert.AreEqual(1, resumen.CuantosDocumentos);
    }

    /// <summary>
    /// Un documento sin huecos pero que el companero devolvio marcado «no completa» TAMBIEN
    /// sale, y se distingue del anterior.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Son dos cosas distintas y por eso cada renglon dice cual le pasa.</b> «Le
    /// faltan datos» se arregla en Correccion; «no completada» se arregla hablando con el
    /// lider. Meterlas en una sola cuenta sin distinguirlas obligaria a abrir documento por
    /// documento para saber cual es cual.
    /// </remarks>
    [TestMethod]
    public void UnDocumentoSinHuecosPeroMarcadoNoCompletaTambienSaleYSeDistingue()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "DIJO2609", "2026-09-08", estado: "no_completa");

        var renglon = BaseDeInicio.LectorDeIncompletosDe(servicios).Leer().Grupos.Single().Documentos.Single();

        Assert.AreEqual(0, renglon.CuantoLeFalta, "A este no le falta ningún dato en el sistema.");
        Assert.AreEqual(EstadoDeRecomendacion.NoCompleta, renglon.Estado,
            "En la base sigue diciendo lo que dijo el Excel del compañero; eso no se colapsa.");
        Assert.AreEqual(DosEstados.MeFalta, renglon.PalabraDelEstado);
        // ⛔ 2026-09-07: los dos renglones decían frases distintas —«le falta 1 dato» contra
        // «listo para asignar · no completada»— y ahora dicen la misma palabra. Lo que los
        // distingue no se pierde: está en el detalle, y sigue diciendo QUIÉN lo dijo.
        StringAssert.Contains(renglon.DetalleDelEstado, "el compañero dijo");
        Assert.IsFalse(
            renglon.DetalleDelEstado.Contains("dato", StringComparison.Ordinal),
            "A este no le falta ningún dato del papel: lo que falta lo dijo el compañero.");
    }

    /// <summary>Un documento sin huecos y marcado completa NO sale.</summary>
    [TestMethod]
    public void UnDocumentoSinHuecosYCompletoNoSale()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "BIEN2609", "2026-09-08", estado: "completa");

        var resumen = BaseDeInicio.LectorDeIncompletosDe(servicios).Leer();

        Assert.IsTrue(resumen.NoQuedaNada);
        Assert.AreEqual(0, resumen.CuantosDocumentos);
        Assert.IsEmpty(resumen.Grupos);
    }

    /// <summary>
    /// Un documento archivado NO sale aquí, aunque le falte todo.
    /// </summary>
    /// <remarks>
    /// Regla del dueno del 2026-09-05: <i>«cuando yo archive, debe salir del sistema visible
    /// pero se queda como histórico para los reportes»</i>. Sigue entero en la base —el
    /// denominador lo dice— y sigue en el calendario.
    /// </remarks>
    [TestMethod]
    public void UnDocumentoArchivadoNoSaleEnLaVentanaDeIncompletos()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "ARCH2609", "2026-09-08", archivado: true, temploNombre: null);

        var resumen = BaseDeInicio.LectorDeIncompletosDe(servicios).Leer();

        Assert.IsTrue(resumen.NoQuedaNada);
        Assert.AreEqual(1, resumen.CasosEnLaBase, "Sigue en la base: es histórico, no se borró.");
        Assert.AreEqual(0, resumen.CasosNoArchivados);
    }

    /// <summary>
    /// Los grupos van por fecha de viaje y lo que viaja antes va ARRIBA.
    /// </summary>
    /// <remarks>
    /// El orden es el mismo del criterio C1-4: lo que todavia se puede salvar del mas
    /// cercano al mas lejano, detras lo que ya viajo, y al final lo que no tiene fecha, que
    /// no desaparece (C7-4). Ordenar solo por fecha pondria agosto encima de septiembre.
    /// </remarks>
    [TestMethod]
    public void LosGruposVanPorFechaDeViajeYLoQueViajaAntesVaArriba()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "NOVI2611", "2026-11-20", temploNombre: null);
        BaseDeInicio.MeterCaso(servicios, "AGOS2608", "2026-08-20", temploNombre: null);
        BaseDeInicio.MeterCaso(servicios, "SEPT2609", "2026-09-06", temploNombre: null);
        BaseDeInicio.MeterCaso(servicios, "NADA0000", fechaViaje: null, temploNombre: null);

        var grupos = BaseDeInicio.LectorDeIncompletosDe(servicios).Leer().Grupos;

        var titulos = grupos.Select(g => g.Documentos[0].NumeroCaso).ToList();
        CollectionAssert.AreEqual(
            new[] { "SEPT2609", "NOVI2611", "AGOS2608", "NADA0000" },
            titulos,
            "El orden tiene que ser: lo que viaja pronto, lo que viaja lejos, lo vencido y al final lo que no tiene fecha.");

        Assert.AreEqual("domingo 6 de septiembre de 2026", grupos[0].Titulo);
        Assert.AreEqual("en 2 días", grupos[0].CuandoViaja);
        Assert.IsFalse(grupos[0].YaViajo);
        Assert.IsTrue(grupos[2].YaViajo);
        Assert.AreEqual("sin fecha de viaje", grupos[3].Titulo);
        Assert.IsFalse(grupos[3].SePuedeAbrirElGrupo, "Sin fecha no hay grupo que abrir.");
    }

    /// <summary>Los documentos del mismo dia caen en el mismo grupo, con su cuenta de personas.</summary>
    [TestMethod]
    public void LosDocumentosDelMismoDiaCaenEnElMismoGrupoConSuCuentaDePersonas()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "UNOO2609", "2026-09-08", temploNombre: null, cuantasPersonas: 3);
        BaseDeInicio.MeterCaso(servicios, "DOSS2609", "2026-09-08", temploNombre: null, cuantasPersonas: 2);

        var grupo = BaseDeInicio.LectorDeIncompletosDe(servicios).Leer().Grupos.Single();

        Assert.HasCount(2, grupo.Documentos);
        Assert.AreEqual(5, grupo.CuantasPersonas);
        StringAssert.Contains(grupo.Detalle, "2 documentos", StringComparison.Ordinal);
        StringAssert.Contains(grupo.Detalle, "5 personas", StringComparison.Ordinal);
        Assert.AreEqual("2026-09-08", grupo.FechaIso);
        Assert.IsTrue(grupo.SePuedeAbrirElGrupo);
    }

    /// <summary>El denominador se dice siempre; sin el, la cifra de arriba no se puede comprobar.</summary>
    [TestMethod]
    public void LaVentanaDiceSuDenominadorYCuadraConLaBase()
    {
        var servicios = BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba);
        var resumen = BaseDeInicio.LectorDeIncompletosDe(servicios).Leer();

        Assert.AreEqual(
            servicios.Casos.Contar(FiltroDeCasos.Todo with { IncluirArchivados = true }),
            resumen.CasosEnLaBase);
        Assert.AreEqual(servicios.Casos.Contar(FiltroDeCasos.Todo), resumen.CasosNoArchivados);
        Assert.AreEqual(resumen.Grupos.Sum(g => g.Documentos.Count), resumen.CuantosDocumentos);
        Assert.AreEqual(resumen.Grupos.Sum(g => g.CuantasPersonas), resumen.CuantasPersonas);
        // ⚠️ 2026-09-06. Pedia «sin archivar» en la linea. El dueno quito la palabra de todos
        // los contadores; las dos cifras de arriba siguen midiendose igual, que es lo que esta
        // prueba vigila de verdad.
        Assert.IsFalse(
            resumen.LineaDelDenominador.Contains("archiv", StringComparison.OrdinalIgnoreCase),
            "El denominador de lo incompleto volvio a nombrar el archivo: " + resumen.LineaDelDenominador);
    }

    /// <summary>
    /// Lo que Inicio dejo de ensenar esta aqui: nada se borro del programa.
    /// </summary>
    /// <remarks>
    /// Es la mitad de la peticion que no es «quitar de Home»: los documentos que ya viajaron
    /// sin resolver, y los que no tienen fecha, siguen teniendo donde verse. Si no la
    /// tuvieran, quitarlos de Inicio los haria desaparecer del programa.
    /// </remarks>
    [TestMethod]
    public void LoQueInicioDejoDeEnsenarSigueTeniendoDondeVerse()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "VENC2608", "2026-08-01", estado: "no_completa");
        BaseDeInicio.MeterCaso(servicios, "NADA0000", fechaViaje: null, temploNombre: null);

        var inicio = BaseDeInicio.LeerInicio(servicios);
        var incompletos = BaseDeInicio.LectorDeIncompletosDe(servicios).Leer();

        Assert.IsFalse(inicio.Listos.Any(r => r.NumeroCaso == "VENC2608"),
            "Un documento que el compañero devolvió «no completa» no es «listo para asignar».");
        Assert.IsFalse(inicio.Listos.Any(r => r.NumeroCaso == "NADA0000"),
            "Un documento sin fecha de viaje tampoco: la fecha es uno de los campos que le faltan.");
        Assert.AreEqual(2, incompletos.CuantosDocumentos);
        Assert.Contains("VENC2608", incompletos.Grupos.SelectMany(g => g.Documentos).Select(d => d.NumeroCaso).ToList());
        Assert.Contains("NADA0000", incompletos.Grupos.SelectMany(g => g.Documentos).Select(d => d.NumeroCaso).ToList());
    }

    /// <summary>
    /// La lista va aplanada: una cabecera por fecha y detras sus documentos.
    /// </summary>
    [TestMethod]
    public void LaListaDeIncompletosVaAplanadaConUnaCabeceraPorFecha()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "UNOO2609", "2026-09-08", temploNombre: null);
        BaseDeInicio.MeterCaso(servicios, "DOSS2609", "2026-09-08", temploNombre: null);
        BaseDeInicio.MeterCaso(servicios, "TRES2610", "2026-10-08", temploNombre: null);

        var renglones = BaseDeInicio.LectorDeIncompletosDe(servicios).Leer().EnUnaSolaLista();

        Assert.HasCount(5, renglones, "Dos cabeceras y tres documentos.");
        Assert.IsTrue(renglones[0].EsCabecera);
        Assert.IsTrue(renglones[0].SePuedeAbrirElGrupo);
        Assert.AreEqual("2026-09-08", renglones[0].FechaIso);
        Assert.IsTrue(renglones[1].EsUnDocumento);
        Assert.IsTrue(renglones[3].EsCabecera);
        StringAssert.Contains(renglones[1].ParaElLector, "Pulse para corregir", StringComparison.Ordinal);
    }

    /// <summary>
    /// Requisito 9: una fecha con forma rara no tumba nada, cae en «sin fecha» y deja UNA
    /// linea de aviso que cabe en un renglon.
    /// </summary>
    [TestMethod]
    public void UnaFechaConFormaRaraCaeEnSinFechaYDejaUnaLineaDeAviso()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "RARA2609", "08/09/2026", temploNombre: null);

        var resumen = BaseDeInicio.LectorDeIncompletosDe(servicios).Leer();

        Assert.HasCount(1, resumen.Grupos);
        Assert.AreEqual("sin fecha de viaje", resumen.Grupos[0].Titulo);
        Assert.HasCount(1, resumen.Avisos);
        Assert.AreEqual(GravedadDeAviso.Advertencia, resumen.Avisos[0].Gravedad);
        Assert.IsLessThanOrEqualTo(120, resumen.Avisos[0].Linea.Length);
    }

    /// <summary>Con la base vacía la ventana lo dice y no se queda en blanco.</summary>
    [TestMethod]
    public void ConLaBaseVaciaLaVentanaLoDiceYNoSeQuedaEnBlanco()
    {
        var resumen = BaseDeInicio.LectorDeIncompletosDe(BaseDeInicio.MontarServicios(0)).Leer();

        Assert.IsTrue(resumen.NoQuedaNada);
        Assert.IsEmpty(resumen.Grupos);
        Assert.IsEmpty(resumen.EnUnaSolaLista());
        StringAssert.Contains(resumen.LineaDelDenominador, "0 documentos sin completar", StringComparison.Ordinal);
    }

    /// <summary>
    /// Con 3 000 documentos la ventana sigue cuadrando, y ni un archivado se cuela.
    /// </summary>
    [TestMethod]
    public void ConTresMilDocumentosLaVentanaSigueCuadrandoYNiUnArchivadoSeCuela()
    {
        var servicios = BaseDeInicio.MontarServicios(3000);
        var resumen = BaseDeInicio.LectorDeIncompletosDe(servicios).Leer();

        var archivados = BaseDeInicio.TodosLosCasos(servicios).Where(c => c.Archivado).Select(c => c.Id).ToHashSet();
        var enPantalla = resumen.Grupos.SelectMany(g => g.Documentos).Select(d => d.CasoId).ToList();

        Assert.AreEqual(3000, resumen.CasosEnLaBase);
        Assert.AreEqual(2766, resumen.CasosNoArchivados);
        Assert.IsEmpty(enPantalla.Intersect(archivados), "Un archivado no puede salir en esta ventana.");
        Assert.AreEqual(enPantalla.Count, enPantalla.Distinct().Count(), "Ningún documento sale dos veces.");
        Assert.AreEqual(CifrasMedidas.DocumentosSinCompletar, resumen.CuantosDocumentos);
        Assert.AreEqual(CifrasMedidas.PersonasSinCompletar, resumen.CuantasPersonas);
        Assert.HasCount(CifrasMedidas.GruposDeFecha, resumen.Grupos);
    }

    /// <summary>Las cifras de la base inventada de 3 000, medidas y no estimadas.</summary>
    internal static class CifrasMedidas
    {
        /// <summary>Documentos sin archivar a los que les falta algo, o que volvieron «no completa».</summary>
        /// <remarks>
        /// ⚠️ <b>Subio de 1 546 a 1 888 el 2026-09-06.</b> Son <b>342 documentos</b> que la cola
        /// no enseñaba y que la pantalla de Correccion ya daba por incompletos: la cola miraba
        /// el vacio y la forma, y no la confianza, ni el tachon, ni si constaba de donde salio
        /// el valor. Subir esta cifra es la mitad del arreglo; la otra mitad es que 447 salen
        /// como listos donde antes salian 621.
        /// </remarks>
        public const int DocumentosSinCompletar = 1888;

        /// <summary>Personas que suman esos documentos.</summary>
        public const int PersonasSinCompletar = 4954;

        /// <summary>En cuantos grupos de fecha se reparten.</summary>
        /// <remarks>Tres fechas mas: dias enteros que no aparecian en esta ventana.</remarks>
        public const int GruposDeFecha = 239;
    }
}
