using Fichas.App.Correccion;
using Fichas.Datos.Falso;
using Fichas.App.Grupo;
using Fichas.App.Inicio;
using Fichas.App.Revisar;
using Fichas.Pruebas.App.Inicio;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Correccion es un sitio de PASO y no un almacen: un documento entra cuando le falta algo
/// y sale solo cuando deja de faltarle.
/// </summary>
/// <remarks>
/// <para><b>De donde sale.</b> Palabras del dueno el 2026-09-07 (<c>DECISIONES.md</c>, «EL
/// DUENO DICTA EL FLUJO ENTERO», apartado 4): <i>«Lo que está listo para asignar no puede
/// mezclarse con lo que se debe organizar. […] no leyó la fecha de 2 archivos que tenían 4
/// personas. Debe ponérmelo en la ventana de Corrección. Y los otros, que sí están correctos,
/// debe ponerle grupo viaja el 12 de septiembre. Cuando yo lo corrija y le ponga la
/// información, debe salir de Corrección y pasar al grupo de su fecha. Y si voy a Corrección
/// no debe estar ahí, porque ya está todo listo»</i>.</para>
///
/// <para><b>Lo que habia antes de este pase, medido:</b>
/// <c>PaginaDeCorreccion.LlenarLosGrupos</c> armaba los grupos con
/// <c>TableroDeRevisar.Cargadas</c> —TODOS los documentos no archivados— y no preguntaba ni
/// una vez si a alguno le faltaba algo. Corregir el ultimo campo de un documento no lo sacaba
/// de la lista: solo le cambiaba la frase del renglon.</para>
///
/// <para>⛔ <b>El veredicto NO se escribe aqui ni se copia.</b> Es
/// <see cref="LoQueLeFalta.EstaListo"/>, el mismo que usan la cola de Completar y la pantalla
/// del grupo, y llega por <see cref="LoQueLeFaltaACadaDocumento"/>. Escribir un segundo
/// criterio volveria a abrir el agujero que <c>DECISIONES.md</c> midio el 2026-09-06: seis
/// documentos que se leian al reves segun la pantalla, y uno que se quedaba fuera de todas
/// las listas.</para>
///
/// <para>⛔ De aqui no sale ni una escritura: se filtra lo ya leido. La regla permanente 5
/// sigue entera.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeQueCorreccionEsUnSitioDePaso
{
    /// <summary>La tanda que el dueno describe, en pequeno: una fecha, dos unidades.</summary>
    /// <remarks>
    /// Se monta con la MISMA base falsa que el resto de las pruebas de pantalla, y no con
    /// tarjetas a mano, porque lo que hay que comprobar es que el veredicto de la base y el
    /// filtro de la pantalla contestan lo mismo.
    /// </remarks>
    private sealed record Tanda(
        ServiciosFalsos Servicios,
        IReadOnlyList<long> Completos,
        IReadOnlyList<long> SinFecha);

    /// <summary>Como se llama la fecha del ejemplo del dueno.</summary>
    private const string ElDoceDeSeptiembre = "2026-09-12";

    /// <summary>La unidad grande del ejemplo del dueno.</summary>
    private const string RamaSanJuan = "325535";

    /// <summary>La unidad pequena.</summary>
    private const string BarrioMarito = "656351";

    /// <summary>
    /// Cinco documentos leidos enteros del 12 de septiembre y dos a los que no se les leyo la
    /// fecha.
    /// </summary>
    private static Tanda MontarLaTanda()
    {
        var servicios = BaseDeInicio.MontarServicios(0);

        var completos = new List<long>
        {
            BaseDeInicio.MeterCaso(servicios, "CASP2609", ElDoceDeSeptiembre, cuantasPersonas: 4, unidadNumero: RamaSanJuan),
            BaseDeInicio.MeterCaso(servicios, "CASP2610", ElDoceDeSeptiembre, cuantasPersonas: 3, unidadNumero: RamaSanJuan),
            BaseDeInicio.MeterCaso(servicios, "CASP2611", ElDoceDeSeptiembre, cuantasPersonas: 3, unidadNumero: RamaSanJuan),
            BaseDeInicio.MeterCaso(servicios, "CASP2612", ElDoceDeSeptiembre, cuantasPersonas: 3, unidadNumero: BarrioMarito),
            BaseDeInicio.MeterCaso(servicios, "CASP2613", ElDoceDeSeptiembre, cuantasPersonas: 2, unidadNumero: BarrioMarito),
        };

        // Los dos que el lector no pudo fechar: la regla del 2026-09-07 es dejarlo vacio y
        // mandarlo a Correccion, no adivinar.
        var sinFecha = new List<long>
        {
            BaseDeInicio.MeterCaso(servicios, "CASP2614", null, cuantasPersonas: 2, unidadNumero: RamaSanJuan),
            BaseDeInicio.MeterCaso(servicios, "CASP2615", null, cuantasPersonas: 2, unidadNumero: BarrioMarito),
        };

        return new Tanda(servicios, completos, sinFecha);
    }

    /// <summary>Los grupos de Correccion tal como los arma la pantalla, ya filtrados.</summary>
    private static IReadOnlyList<GrupoParaCorregir> LoQueEnsenaCorreccion(
        ServiciosFalsos servicios, long elQueSeEstaMirando = 0)
    {
        var tablero = new TableroDeRevisar(
            servicios.Casos, servicios.Asignaciones, servicios.Companeros, servicios.Reloj);
        tablero.Cargar();

        var loQueLeFalta = LoQueLeFaltaACadaDocumento.DeTodaLaBase(
            servicios.Casos, servicios.Personas, servicios.Procedencia);

        return QueEntraEnCorreccion.Filtrar(
            GruposParaCorregir.Armar(tablero.Cargadas), loQueLeFalta.LeFaltaAlgo, elQueSeEstaMirando);
    }

    /// <summary>Todos los grupos, sin filtrar; es a donde PASA lo que se resuelve.</summary>
    private static IReadOnlyList<GrupoParaCorregir> TodosLosGrupos(ServiciosFalsos servicios)
    {
        var tablero = new TableroDeRevisar(
            servicios.Casos, servicios.Asignaciones, servicios.Companeros, servicios.Reloj);
        tablero.Cargar();
        return GruposParaCorregir.Armar(tablero.Cargadas);
    }

    /// <summary>Los ids que Correccion ofrece ahora mismo.</summary>
    private static HashSet<long> IdsEnCorreccion(IReadOnlyList<GrupoParaCorregir> grupos)
        => [.. grupos.SelectMany(g => g.Documentos).Select(d => d.CasoId)];

    // ────────────────────────── el criterio, punto por punto ──────────────────────────

    /// <summary>
    /// Dado el ejemplo del dueno, Correccion solo ensena los dos a los que les falta la fecha.
    /// </summary>
    [TestMethod]
    public void CorreccionSoloEnsenaLoQueLeFaltaAlgo()
    {
        var tanda = MontarLaTanda();

        var enCorreccion = IdsEnCorreccion(LoQueEnsenaCorreccion(tanda.Servicios));

        Assert.HasCount(2, enCorreccion, "solo entran los dos que no tienen fecha");
        foreach (var casoId in tanda.SinFecha)
            Assert.Contains(casoId, enCorreccion, $"al caso {casoId} le falta la fecha: tiene que estar");
        foreach (var casoId in tanda.Completos)
            Assert.DoesNotContain(casoId, enCorreccion, $"al caso {casoId} no le falta nada: no puede estar");
    }

    /// <summary>Un grupo cuyos documentos estan todos resueltos no aparece en la lista.</summary>
    /// <remarks>
    /// Sin esto, el desplegable seguiria ofreciendo «Grupo del 12 de septiembre · 325535» con
    /// cero documentos dentro, que es un grupo que al elegirlo no abre nada.
    /// </remarks>
    [TestMethod]
    public void UnGrupoSinNadaQueFaltarNoSaleEnLaLista()
    {
        var tanda = MontarLaTanda();

        var grupos = LoQueEnsenaCorreccion(tanda.Servicios);

        Assert.IsEmpty(
            grupos.Where(g => g.FechaIso == ElDoceDeSeptiembre).ToList(),
            "el 12 de septiembre esta entero: no tiene nada que corregir");
        Assert.IsTrue(grupos.All(g => g.CuantosDocumentos > 0), "ningun grupo se ofrece vacio");
    }

    /// <summary>
    /// Al corregir el ultimo dato que faltaba, el documento SALE de Correccion.
    /// </summary>
    /// <remarks>
    /// Es la frase del dueno entera: <i>«si voy a Corrección no debe estar ahí, porque ya está
    /// todo listo»</i>. Se mide sobre la base, antes y despues de escribir la fecha.
    /// </remarks>
    [TestMethod]
    public void AlCorregirElUltimoDatoElDocumentoSaleDeCorreccion()
    {
        var tanda = MontarLaTanda();
        var elCorregido = tanda.SinFecha[0];

        Assert.Contains(elCorregido, IdsEnCorreccion(LoQueEnsenaCorreccion(tanda.Servicios)),
            "antes de corregir tiene que estar en Correccion");

        PonerleLaFecha(tanda.Servicios, elCorregido, ElDoceDeSeptiembre);

        var despues = IdsEnCorreccion(LoQueEnsenaCorreccion(tanda.Servicios));
        Assert.DoesNotContain(elCorregido, despues, "corregido, ya no esta en Correccion");
        Assert.HasCount(1, despues, "queda el otro, y solo el otro");
    }

    /// <summary>
    /// Y aparece en el grupo de SU fecha, con su unidad.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Esta es la mitad que impide que el documento se pierda.</b> «Sale de Correccion»
    /// no puede querer decir «desaparece»: en este programa perder un documento significa que
    /// alguien viaje con la recomendacion mal.
    /// </remarks>
    [TestMethod]
    public void ElDocumentoCorregidoAparaceEnElGrupoDeSuFecha()
    {
        var tanda = MontarLaTanda();
        var elCorregido = tanda.SinFecha[0];

        PonerleLaFecha(tanda.Servicios, elCorregido, ElDoceDeSeptiembre);

        var grupo = BaseDeInicio.LectorDeGruposDe(tanda.Servicios)
            .DelDia(DateOnly.Parse(ElDoceDeSeptiembre, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Contains(elCorregido, grupo.TodosLosCasos, "tiene que estar en el grupo del 12 de septiembre");

        var suUnidad = grupo.Unidades.Single(u => u.CasoIds.Contains(elCorregido));
        StringAssert.Contains(suUnidad.Titulo, RamaSanJuan, "y en la unidad de la que salio");
    }

    /// <summary>
    /// Se DICE a que grupo pasa, con la fecha y la unidad, para poder verlo pasar.
    /// </summary>
    [TestMethod]
    public void SeDiceAQueGrupoPasaElDocumentoQueSaleDeCorreccion()
    {
        var tanda = MontarLaTanda();
        var elCorregido = tanda.SinFecha[0];
        PonerleLaFecha(tanda.Servicios, elCorregido, ElDoceDeSeptiembre);

        var aDonde = QueEntraEnCorreccion.ADondePasa(TodosLosGrupos(tanda.Servicios), elCorregido);

        StringAssert.Contains(aDonde, "12 de septiembre", "la fecha del grupo al que pasa");
        StringAssert.Contains(aDonde, RamaSanJuan, "y su unidad");
    }

    /// <summary>De un documento que no esta en ningun grupo no se inventa un destino.</summary>
    [TestMethod]
    public void DeUnDocumentoQueNoEstaEnNingunGrupoNoSeDiceADondePasa()
    {
        var tanda = MontarLaTanda();

        Assert.AreEqual(string.Empty, QueEntraEnCorreccion.ADondePasa(TodosLosGrupos(tanda.Servicios), 99999));
    }

    /// <summary>
    /// El documento que se esta mirando NO desaparece de la pantalla por haberse resuelto.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>La trampa del pase, dicha al reves.</b> Si al guardar el ultimo dato el documento
    /// se cayera del desplegable en el acto, la cabecera diria «Grupo del 12 de septiembre · 4
    /// documentos» con OTRO documento delante, y el dueno no veria lo que acaba de resolver. Se
    /// queda mientras esta abierto —lo dice la banda— y no vuelve a la lista la proxima vez que
    /// se entra, que es lo que el pidio.
    /// </remarks>
    [TestMethod]
    public void ElDocumentoAbiertoSigueEnLaPantallaAunqueYaEsteResuelto()
    {
        var tanda = MontarLaTanda();
        var elCorregido = tanda.SinFecha[0];
        PonerleLaFecha(tanda.Servicios, elCorregido, ElDoceDeSeptiembre);

        var conElAbierto = IdsEnCorreccion(LoQueEnsenaCorreccion(tanda.Servicios, elCorregido));
        Assert.Contains(elCorregido, conElAbierto, "mientras esta abierto se sigue viendo");

        var sinAbrirNada = IdsEnCorreccion(LoQueEnsenaCorreccion(tanda.Servicios));
        Assert.DoesNotContain(elCorregido, sinAbrirNada, "al volver a entrar, ya no esta");
    }

    /// <summary>El invitado no se cuela dos veces en su grupo.</summary>
    [TestMethod]
    public void ElDocumentoAbiertoNoSaleDosVecesEnSuGrupo()
    {
        var tanda = MontarLaTanda();
        var elQueSigueFaltando = tanda.SinFecha[1];

        var grupos = LoQueEnsenaCorreccion(tanda.Servicios, elQueSigueFaltando);
        var cuantasVeces = grupos.SelectMany(g => g.Documentos).Count(d => d.CasoId == elQueSigueFaltando);

        Assert.AreEqual(1, cuantasVeces, "el que ya entraba por su cuenta no se anade otra vez");
    }

    // ────────────────────────── la trampa: nada se pierde ──────────────────────────

    /// <summary>
    /// Un documento SIN FECHA no sale de Correccion aunque no le falte ningun dato: no hay
    /// grupo de dia al que pasar.
    /// </summary>
    /// <remarks>
    /// <para>⚠️ <b>No es un caso teorico: lo encontro esta misma clase.</b> La primera vez que
    /// se ejecuto <see cref="NingunDocumentoSeQuedaFueraDeLasDosListas"/> senalo el caso 202
    /// —<c>CASD2608</c>— de la base inventada: fecha de viaje VACIA y
    /// <c>LoQueLeFalta.EstaListo</c> diciendo que si, porque alguien marco esa fecha como que
    /// <b>no esta en el papel</b> y ahi ya no queda nada que buscar. Sin esta regla, filtrar
    /// Correccion lo habria hecho desaparecer del programa entero.</para>
    ///
    /// <para>Se monta igual que el de la base inventada: el dato marcado como ausente, que es
    /// lo unico que deja un campo vacio sin que cuente como que falta.</para>
    /// </remarks>
    [TestMethod]
    public void UnDocumentoSinFechaNoSaleAunqueNoLeFalteNingunDato()
    {
        var tanda = MontarLaTanda();
        var elQueNoTieneFecha = tanda.SinFecha[0];

        MarcarLaFechaComoAusenteDelPapel(tanda.Servicios, elQueNoTieneFecha);

        var procedencias = ProcedenciasDeUnaPasada.DeTodaLaBase(tanda.Servicios.Procedencia);
        Assert.IsTrue(
            LoQueLeFalta.EstaListo(
                tanda.Servicios.Almacen.Casos[elQueNoTieneFecha],
                tanda.Servicios.Personas.DeCaso(elQueNoTieneFecha),
                procedencias),
            "el montaje tiene que dejar el documento SIN datos que falten; si no, la prueba mide otra cosa");

        Assert.Contains(
            elQueNoTieneFecha,
            IdsEnCorreccion(LoQueEnsenaCorreccion(tanda.Servicios)),
            "sin fecha no hay grupo al que pasar: sacarlo de Correccion seria perderlo");
    }

    /// <summary>Y el grupo sin fecha PIDE que lo miren, para que no se lea como un almacen.</summary>
    [TestMethod]
    public void ElGrupoSinFechaSigueLlamandoAQueLoMiren()
    {
        var tanda = MontarLaTanda();
        MarcarLaFechaComoAusenteDelPapel(tanda.Servicios, tanda.SinFecha[0]);

        var suGrupo = LoQueEnsenaCorreccion(tanda.Servicios)
            .Single(g => g.Documentos.Any(d => d.CasoId == tanda.SinFecha[0]));

        Assert.IsTrue(suGrupo.HayQueRevisarlo, "es el grupo de los que no tienen fecha");
        StringAssert.Contains(suGrupo.Etiqueta, ArbolDeRevisar.LlamadaARevisar);
    }

    /// <summary>
    /// Ningun documento se queda fuera de las DOS listas: o esta en Correccion, o esta en el
    /// grupo de su fecha.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>Es la prueba que cierra la trampa del pase</b>, y se comprueba sobre la base
    /// inventada entera y no sobre siete casos a mano: el agujero que <c>DECISIONES.md</c>
    /// midio el 2026-09-06 no salio de un caso raro, salio de dos caminos que casi siempre
    /// coincidian.</para>
    ///
    /// <para>La invariante que la sostiene: a un documento resuelto NO le falta la fecha —si le
    /// faltara, <see cref="LoQueLeFalta"/> lo contaria y estaria en Correccion—, asi que el
    /// grupo de su dia lo tiene por fuerza. No hay tercer sitio donde caerse.</para>
    /// </remarks>
    [TestMethod]
    public void NingunDocumentoSeQuedaFueraDeLasDosListas()
    {
        var servicios = BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba);

        var enCorreccion = IdsEnCorreccion(LoQueEnsenaCorreccion(servicios));
        var lector = BaseDeInicio.LectorDeGruposDe(servicios);
        var enAlgunGrupo = new HashSet<long>();

        foreach (var fecha in BaseDeInicio.TodosLosCasos(servicios)
                     .Select(c => FechasEnEspanol.Leer(c.FechaViaje))
                     .OfType<DateOnly>()
                     .Distinct())
        {
            foreach (var casoId in lector.DelDia(fecha).TodosLosCasos) enAlgunGrupo.Add(casoId);
        }

        var mirados = 0;
        foreach (var caso in BaseDeInicio.TodosLosCasos(servicios).Where(c => !c.Archivado))
        {
            Assert.IsTrue(
                enCorreccion.Contains(caso.Id) || enAlgunGrupo.Contains(caso.Id),
                $"el caso {caso.Id} no esta ni en Correccion ni en ningun grupo de fecha: se perdio");
            mirados++;
        }

        Assert.IsGreaterThan(50, mirados, "la comprobacion tiene que haber mirado documentos de verdad");
    }

    /// <summary>
    /// Entra en Correccion, y solo entra, lo que <see cref="LoQueLeFalta"/> dice que pide algo
    /// o lo que no tiene fecha de viaje a la que ir.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>La primera mitad es el veredicto unico y no se toca.</b> Es el mismo
    /// <see cref="LoQueLeFalta.EstaListo"/> de la cola de Completar y de la pantalla del grupo;
    /// aqui se compara contra el, no contra una copia. La cola trae ademas los que el Excel del
    /// companero devolvio <c>no_completa</c>, que es otra poblacion y otra pantalla.</para>
    ///
    /// <para>⚠️ <b>La segunda mitad no es un segundo veredicto: es la puerta de salida.</b> Sin
    /// fecha de viaje no hay grupo de dia que reciba al documento, asi que sacarlo de Correccion
    /// seria hacerlo desaparecer. Se escribe aqui en los terminos del dominio —«le falta algo o
    /// no tiene fecha legible»— y no llamando a <c>QueEntraEnCorreccion</c>, para que la prueba
    /// diga la regla y no repita el codigo.</para>
    /// </remarks>
    [TestMethod]
    public void EntraLoQuePideAlgoYLoQueNoTieneFechaALaQueIr()
    {
        var servicios = BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba);
        var enCorreccion = IdsEnCorreccion(LoQueEnsenaCorreccion(servicios));
        var procedencias = ProcedenciasDeUnaPasada.DeTodaLaBase(servicios.Procedencia);

        var comparados = 0;
        var sinFecha = 0;
        foreach (var caso in BaseDeInicio.TodosLosCasos(servicios).Where(c => !c.Archivado))
        {
            var personas = servicios.Personas.DeCaso(caso.Id);
            var leFaltaAlgo = !LoQueLeFalta.EstaListo(caso, personas, procedencias);
            var noTieneFechaALaQueIr = FechasEnEspanol.Leer(caso.FechaViaje) is null;
            if (noTieneFechaALaQueIr) sinFecha++;

            Assert.AreEqual(
                leFaltaAlgo || noTieneFechaALaQueIr,
                enCorreccion.Contains(caso.Id),
                $"el caso {caso.Id} entra o sale de Correccion por un criterio distinto del acordado");
            comparados++;
        }

        Assert.IsGreaterThan(50, comparados, "la comparacion tiene que haber mirado documentos de verdad");
        Assert.IsGreaterThan(0, sinFecha, "la base tiene que traer alguno sin fecha, o esto no prueba la segunda mitad");
    }

    // ────────────────────────── el idioma de dos palabras ──────────────────────────

    /// <summary>Lo que se dice al salir solo usa «resuelto» y «me falta».</summary>
    [TestMethod]
    public void SoloSeLeenLasDosPalabras()
    {
        var frases = new[]
        {
            TextoDeLaSalidaDeCorreccion.YaNoEstaEnCorreccion("CASP2609", "Grupo del 12 de septiembre · 325535", 3),
            TextoDeLaSalidaDeCorreccion.YaEstabaResuelto("Grupo del 12 de septiembre · 325535"),
            TextoDeLaSalidaDeCorreccion.NoQuedaNadaQueCorregir,
        };

        foreach (var frase in frases)
        {
            foreach (var retirada in new[] { "listo para asignar", "listo para viajar", "confirmada", "completa" })
            {
                Assert.IsFalse(
                    frase.Contains(retirada, StringComparison.OrdinalIgnoreCase),
                    $"«{retirada}» es una de las cuatro palabras que el dueno retiro el 2026-09-07: «{frase}»");
            }
        }
    }

    /// <summary>La frase de salida dice el numero del documento, a donde pasa y cuantos quedan.</summary>
    /// <remarks>Una cifra sin denominador no se puede comprobar (CLAUDE.md §8).</remarks>
    [TestMethod]
    public void LaFraseDeSalidaDiceCualEsADondeVaYCuantosQuedan()
    {
        var frase = TextoDeLaSalidaDeCorreccion.YaNoEstaEnCorreccion(
            "CASP2609", "Grupo del 12 de septiembre · 325535 · Rama San Juan", 3);

        StringAssert.Contains(frase, "CASP2609");
        StringAssert.Contains(frase, "12 de septiembre");
        StringAssert.Contains(frase, "325535");
        StringAssert.Contains(frase, "3");
    }

    /// <summary>Con uno solo se dice «queda 1», no «quedan 1».</summary>
    /// <remarks>
    /// ⚠️ <b>«quedan 1 documento» se leyo tal cual</b> en la medicion con la ventana abierta del
    /// 2026-09-09, sobre el paquete publicado. Delata lo mismo que un «1 campos»: que nadie leyo
    /// la frase.
    /// </remarks>
    [TestMethod]
    public void ConUnSoloDocumentoQueQuedaLaFraseVaEnSingular()
    {
        var frase = TextoDeLaSalidaDeCorreccion.YaNoEstaEnCorreccion("CASP2605", "Grupo del 12", 1);

        StringAssert.Contains(frase, "queda 1 documento");
        Assert.IsFalse(frase.Contains("quedan 1", StringComparison.Ordinal), $"«{frase}»");
    }

    /// <summary>
    /// La cifra de «con algo que falta» NO cuenta al invitado, que es el que acaba de salir.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Medido con la ventana abierta el 2026-09-09, sobre el paquete publicado:</b> tras
    /// corregir el ultimo dato, la cabecera decia «2 con algo que falta, de 6» y el pie de la
    /// misma pantalla «queda 1 documento con algo que falta». Dos cifras que se contradicen y
    /// ninguna forma de saber cual creer. La diferencia era el invitado.
    /// </remarks>
    [TestMethod]
    public void LaCuentaDeLoQueQuedaNoIncluyeAlQueAcabaDeResolverse()
    {
        var tanda = MontarLaTanda();
        var elCorregido = tanda.SinFecha[0];
        PonerleLaFecha(tanda.Servicios, elCorregido, ElDoceDeSeptiembre);

        var loQueLeFalta = LoQueLeFaltaACadaDocumento.DeTodaLaBase(
            tanda.Servicios.Casos, tanda.Servicios.Personas, tanda.Servicios.Procedencia);
        var grupos = LoQueEnsenaCorreccion(tanda.Servicios, elCorregido);

        Assert.AreEqual(
            2,
            grupos.Sum(g => g.CuantosDocumentos),
            "en la lista estan el que sigue pidiendo algo y el invitado");
        Assert.AreEqual(
            1,
            QueEntraEnCorreccion.CuantosPidenAlgo(grupos, loQueLeFalta.LeFaltaAlgo),
            "pero solo UNO sigue pidiendo algo: el invitado ya se resolvio");
    }

    // ────────────────────────── el ayudante que escribe la fecha ──────────────────────────

    /// <summary>
    /// Escribe la fecha de viaje de un documento como la escribe Correccion al guardar.
    /// </summary>
    /// <remarks>
    /// ⚠️ Se anota tambien la PROCEDENCIA, porque el veredicto la mira desde el 2026-09-06: un
    /// campo con valor y sin fila de la que salio sigue contando como dudoso, y sin esto la
    /// prueba mediria otra cosa. El origen es <c>Manual</c> —lo escribio Miguel— y
    /// <c>Anotar</c> nunca pone <c>verificado</c> (regla permanente 5).
    /// </remarks>
    /// <summary>
    /// Marca la fecha de viaje de un documento como que el formulario NO la trae.
    /// </summary>
    /// <remarks>
    /// Es la unica forma de que un campo vacio deje de contar como que falta: lo dice
    /// <see cref="EstadosDeCampo.EsDudoso"/> —«si Miguel marco que un dato no está en el papel,
    /// el hueco esta cerrado y no vuelve»—, y es como esta el caso 202 de la base inventada.
    /// Se escribe la fila entera porque <c>Anotar</c> reemplaza la que hubiera.
    /// </remarks>
    private static void MarcarLaFechaComoAusenteDelPapel(ServiciosFalsos servicios, long casoId)
        => servicios.Procedencia.Anotar(new Fichas.Contratos.Modelos.ProcedenciaDeCampo
        {
            Tabla = Fichas.Contratos.Modelos.TablaDeProcedencia.Casos,
            RegistroId = casoId,
            Campo = LoQueLeFalta.ColumnaDeLaFechaDeViaje,
            Origen = Fichas.Contratos.Modelos.OrigenDeCampo.Vacio,
            AusenteEnElPapel = true,
        });

    /// <summary>Escribe la fecha de viaje de un documento como la escribe Correccion al guardar: el valor y su procedencia manual.</summary>
    /// <param name="servicios">La base inventada de la prueba.</param>
    /// <param name="casoId">El documento al que se le pone la fecha.</param>
    /// <param name="fechaIso">La fecha en AAAA-MM-DD.</param>
    private static void PonerleLaFecha(ServiciosFalsos servicios, long casoId, string fechaIso)
    {
        var caso = servicios.Almacen.Casos[casoId];
        servicios.Almacen.Casos[casoId] = caso with { FechaViaje = fechaIso };

        servicios.Procedencia.Anotar(new Fichas.Contratos.Modelos.ProcedenciaDeCampo
        {
            Tabla = Fichas.Contratos.Modelos.TablaDeProcedencia.Casos,
            RegistroId = casoId,
            Campo = LoQueLeFalta.ColumnaDeLaFechaDeViaje,
            Origen = Fichas.Contratos.Modelos.OrigenDeCampo.Manual,
            Confianza = null,
        });
    }
}
