using Fichas.App.Completar;
using Fichas.App.Grupo;
using Fichas.Pruebas.App.Inicio;

namespace Fichas.Pruebas.App.Completar;

/// <summary>
/// La cola de «completar información», probada SIN VENTANA (ADR-0003 §8.1).
/// </summary>
/// <remarks>
/// <para>Sale de las palabras del dueno del 2026-09-06: <i>«Cuando algo no leído se lee y
/// yo lo reviso y le doy guardar, debe pasar a otro renglón de "listo para asignar" y me
/// envía a mí a la pantalla donde está todo lo que no está completo, para yo seguir
/// trabajando. […] un flujo de trabajo donde yo vaya resolviendo casos de manera
/// automática, vaya donde tenga que ir. Y debe haber una tab solo para esto: completar
/// información de documentos que faltan»</i>.</para>
///
/// <para>⛔ <b>Lo que estas pruebas NO comprueban, porque la cola no lo hace:</b> ninguna
/// firma. La cola encadena y nada mas (regla permanente 5). Que no firme se comprueba
/// aparte, en <see cref="PruebasDeQueLaColaNoFirmaNada"/>, y sobre el almacen.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaColaDeCompletar
{
    /// <summary>La cola de unos servicios falsos, leida como la lee la pestana al entrar.</summary>
    private static ColaDeCompletar ColaDe(Fichas.Datos.Falso.ServiciosFalsos servicios)
        => ColaDeCompletar.Desde(BaseDeInicio.LectorDeIncompletosDe(servicios).Leer());

    /// <summary>
    /// En la cola entran los documentos a los que les FALTA un dato, y solo esos.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Es un subconjunto de «lo que no esta completo», y hay que decir por que.</b>
    /// <see cref="LectorDeIncompletos"/> junta dos cosas: los que tienen huecos en nuestros
    /// campos y los que el Excel del companero devolvio marcados «no completa». La pestana
    /// del dueno es <i>«completar información de documentos que faltan»</i>, y un documento
    /// sin ningun hueco no se arregla escribiendo: se arregla hablando con el lider. Si
    /// entrara en la cola, se corregiria, se guardaria y NO saldria nunca, porque no hay
    /// nada que teclear que cambie lo que dijo el companero.
    /// </remarks>
    [TestMethod]
    public void EnLaColaEntranLosQueTienenHuecosYNoLosQueElCompaneroMarcoNoCompleta()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "HUEC2609", "2026-09-08", temploNombre: null);
        BaseDeInicio.MeterCaso(servicios, "DIJO2609", "2026-09-09", estado: "no_completa");
        BaseDeInicio.MeterCaso(servicios, "LLEN2609", "2026-09-10");

        var cola = ColaDe(servicios);

        Assert.AreEqual(1, cola.Quedan, "Solo el que tiene un hueco de verdad.");
        Assert.AreEqual("HUEC2609", cola.Documentos[0].NumeroCaso);
    }

    /// <summary>
    /// La cola sale ordenada por fecha de viaje, lo mas cercano primero.
    /// </summary>
    /// <remarks>
    /// Del dueno, 2026-09-05: <i>«la prioridad son de gente que viajará pronto»</i> y
    /// <i>«no quiero revisar de gente que viaja en noviembre estando en septiembre»</i>.
    /// El orden no se inventa aqui: es el que ya fija y prueba
    /// <see cref="LectorDeIncompletos"/> —lo que aun se puede salvar primero, del mas
    /// cercano al mas lejano; detras lo vencido; al final lo que no tiene fecha—, y la cola
    /// lo conserva al aplanarlo.
    /// </remarks>
    [TestMethod]
    public void LaColaPoneDelanteAlQueViajaAntes()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "NOVI2611", "2026-11-20", temploNombre: null);
        BaseDeInicio.MeterCaso(servicios, "PRON2609", "2026-09-08", temploNombre: null);
        BaseDeInicio.MeterCaso(servicios, "MEDI2610", "2026-10-05", temploNombre: null);

        var cola = ColaDe(servicios);

        CollectionAssert.AreEqual(
            new[] { "PRON2609", "MEDI2610", "NOVI2611" },
            cola.Documentos.Select(d => d.NumeroCaso).ToArray(),
            "Septiembre antes que octubre, y octubre antes que noviembre.");
    }

    /// <summary>Un documento archivado no entra en la cola, aunque le falten datos.</summary>
    /// <remarks>
    /// Decision del dueno del 2026-09-06: <i>«Debe pasar a archivado y no aparecer más en
    /// ningún lado»</i>. La cola es «ningún lado» tambien.
    /// </remarks>
    [TestMethod]
    public void UnArchivadoNoEntraEnLaColaAunqueLeFaltenDatos()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "ARCH2609", "2026-09-08", temploNombre: null, archivado: true);
        BaseDeInicio.MeterCaso(servicios, "VIVO2609", "2026-09-08", temploNombre: null);

        var cola = ColaDe(servicios);

        Assert.AreEqual(1, cola.Quedan);
        Assert.AreEqual("VIVO2609", cola.Documentos[0].NumeroCaso);
    }

    /// <summary>
    /// Resolver el que se esta mirando lo saca de la cola y deja delante el siguiente.
    /// </summary>
    /// <remarks>
    /// Es el encadenado entero, que es lo unico que el dueno pidio de nuevo:
    /// <i>«pulso Guardar UNA vez y sin tocar nada más estoy en el siguiente»</i>.
    /// </remarks>
    [TestMethod]
    public void SacarElQueSeResolvioDejaDelanteElSiguiente()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var primero = BaseDeInicio.MeterCaso(servicios, "PRIM2609", "2026-09-08", temploNombre: null);
        var segundo = BaseDeInicio.MeterCaso(servicios, "SEGU2609", "2026-09-09", temploNombre: null);
        var tercero = BaseDeInicio.MeterCaso(servicios, "TERC2609", "2026-09-10", temploNombre: null);

        var cola = ColaDe(servicios);
        Assert.AreEqual(primero, cola.Primero);

        var siguiente = cola.SacarYDecirCualToca(primero, _ => true);

        Assert.AreEqual(segundo, siguiente, "Tras resolver el primero toca el segundo.");
        Assert.AreEqual(2, cola.Quedan);
        CollectionAssert.AreEqual(new[] { segundo, tercero }, cola.Documentos.Select(d => d.CasoId).ToArray());
    }

    /// <summary>
    /// Resolver el ULTIMO deja la cola vacia y lo dice; no devuelve ningun documento.
    /// </summary>
    /// <remarks>
    /// Criterio del dueno: <i>«vacío la cola y la pantalla dice qué pasa»</i>. Devolver
    /// aqui el primero otra vez seria dar vueltas en redondo sobre lo ya resuelto.
    /// </remarks>
    [TestMethod]
    public void ResolverElUltimoDejaLaColaVaciaYNoDevuelveNinguno()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var unico = BaseDeInicio.MeterCaso(servicios, "SOLO2609", "2026-09-08", temploNombre: null);

        var cola = ColaDe(servicios);
        var siguiente = cola.SacarYDecirCualToca(unico, _ => true);

        Assert.IsNull(siguiente, "No queda ninguno detras.");
        Assert.IsTrue(cola.EstaVacia);
        Assert.AreEqual(0, cola.Quedan);
    }

    /// <summary>
    /// Sacar el ultimo de una cola de varios devuelve el que quedo delante de el, no nulo.
    /// </summary>
    /// <remarks>
    /// El que estaba al final no tiene «siguiente», y dejar ahi la pantalla en blanco con
    /// documentos aun por completar seria decirle que ha terminado cuando no ha terminado.
    /// </remarks>
    [TestMethod]
    public void SacarElUltimoDeVariosDevuelveElQueQuedaAntes()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var primero = BaseDeInicio.MeterCaso(servicios, "PRIM2609", "2026-09-08", temploNombre: null);
        var segundo = BaseDeInicio.MeterCaso(servicios, "SEGU2609", "2026-09-09", temploNombre: null);

        var cola = ColaDe(servicios);
        var siguiente = cola.SacarYDecirCualToca(segundo, _ => true);

        Assert.AreEqual(primero, siguiente);
        Assert.AreEqual(1, cola.Quedan);
    }

    /// <summary>
    /// Un documento que se guarda y SIGUE incompleto no sale de la cola y se queda delante.
    /// </summary>
    /// <remarks>
    /// Palabras del pase: <i>«Si no quedó completo, se queda en la cola con lo que le falta
    /// a la vista»</i>. La cola no adelanta a nadie por haber pulsado Guardar: adelanta por
    /// haber resuelto.
    /// </remarks>
    [TestMethod]
    public void ElQueSigueIncompletoNoSaleDeLaColaYSeQuedaDelante()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var primero = BaseDeInicio.MeterCaso(servicios, "PRIM2609", "2026-09-08", temploNombre: null);
        BaseDeInicio.MeterCaso(servicios, "SEGU2609", "2026-09-09", temploNombre: null);

        var cola = ColaDe(servicios);
        var sigue = cola.DejarYDecirCualToca(primero);

        Assert.AreEqual(primero, sigue, "Sigue tocando el mismo.");
        Assert.AreEqual(2, cola.Quedan, "No sale nadie de la cola.");
    }

    /// <summary>
    /// Un documento de la cola que ya se completo por otra via se salta y no se abre.
    /// </summary>
    /// <remarks>
    /// ⚠️ La cola se lee UNA vez al entrar en la pestana y se lleva en la mano mientras se
    /// encadena; leer la base entera despues de cada guardado costaria una pasada por los
    /// 3 000 documentos por cada pulsacion. El precio de llevarla en la mano es que puede
    /// quedar vieja —el mismo documento se pudo corregir desde el desplegable de Correccion—
    /// y por eso al avanzar se pregunta por cada candidato si TODAVIA le falta algo. Son dos
    /// consultas cortas por salto, no una pasada entera.
    /// </remarks>
    [TestMethod]
    public void ElQueYaSeCompletoPorOtraViaSeSaltaAlAvanzar()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var primero = BaseDeInicio.MeterCaso(servicios, "PRIM2609", "2026-09-08", temploNombre: null);
        var segundo = BaseDeInicio.MeterCaso(servicios, "SEGU2609", "2026-09-09", temploNombre: null);
        var tercero = BaseDeInicio.MeterCaso(servicios, "TERC2609", "2026-09-10", temploNombre: null);

        var cola = ColaDe(servicios);
        var siguiente = cola.SacarYDecirCualToca(primero, cual => cual != segundo);

        Assert.AreEqual(tercero, siguiente, "El segundo ya no tiene huecos: se salta.");
        Assert.AreEqual(1, cola.Quedan, "El saltado tampoco se queda en la cola.");
        Assert.AreEqual(tercero, cola.Primero);
    }

    /// <summary>La posicion se cuenta desde 1 y dice de cuantos; «3 de 8», no «2».</summary>
    /// <remarks>
    /// Sin el denominador, un numero suelto no se puede comprobar, que es la regla §8 de
    /// <c>CLAUDE.md</c> aplicada a la pantalla.
    /// </remarks>
    [TestMethod]
    public void LaPosicionSeDiceConSuDenominador()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "PRIM2609", "2026-09-08", temploNombre: null);
        var segundo = BaseDeInicio.MeterCaso(servicios, "SEGU2609", "2026-09-09", temploNombre: null);

        var cola = ColaDe(servicios);

        Assert.AreEqual("2 de 2", cola.PosicionDe(segundo));
        Assert.AreEqual(string.Empty, cola.PosicionDe(-1), "Uno que no esta en la cola no tiene posicion.");
    }

    /// <summary>Una cola vacia no se rompe al pedirle el primero ni al sacar de ella.</summary>
    [TestMethod]
    public void UnaColaVaciaNoSeRompe()
    {
        var cola = ColaDeCompletar.Desde(BaseDeInicio.LectorDeIncompletosDe(BaseDeInicio.MontarServicios(0)).Leer());

        Assert.IsTrue(cola.EstaVacia);
        Assert.IsNull(cola.Primero);
        Assert.IsNull(cola.SacarYDecirCualToca(99, _ => true));
        Assert.IsNull(cola.DejarYDecirCualToca(99));
    }

    /// <summary>
    /// La cola lleva el denominador de la base: cuantos hay sin archivar y cuantos en total.
    /// </summary>
    /// <remarks>
    /// Criterio del pase: <i>«veo N documentos […] con el total de la base al lado para
    /// saber de cuántos salen»</i>.
    /// </remarks>
    [TestMethod]
    public void LaColaLlevaElDenominadorDeLaBase()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "HUEC2609", "2026-09-08", temploNombre: null);
        BaseDeInicio.MeterCaso(servicios, "LLEN2609", "2026-09-09");
        BaseDeInicio.MeterCaso(servicios, "ARCH2609", "2026-09-10", temploNombre: null, archivado: true);

        var cola = ColaDe(servicios);

        Assert.AreEqual(1, cola.Quedan);
        Assert.AreEqual(2, cola.CasosNoArchivados);
        Assert.AreEqual(3, cola.CasosEnLaBase);
    }
}
