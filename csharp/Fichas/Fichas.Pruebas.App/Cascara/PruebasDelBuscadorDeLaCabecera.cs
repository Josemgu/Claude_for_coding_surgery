using System.Diagnostics;
using Fichas.App.Cascara;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Cascara;

/// <summary>
/// El buscador de la cabecera: busca de verdad, dice exactamente lo que busca, y no saca a
/// la pantalla el nombre ni el MRN de nadie.
/// </summary>
/// <remarks>
/// <para><b>Por que estas tres cosas y no otras.</b> El mockup dibuja el buscador pero
/// declara en su propia tabla de controles que esta «pendiente del frontend real»: no dice
/// que hace. Lo unico que si deja escrito es la regla general —un control que no hace nada
/// esta roto—, asi que el criterio se saco de ahi y de dos hechos medidos el 2026-09-09:</para>
///
/// <list type="number">
///   <item><c>FiltroDeCasos.Texto</c> ya existe y <c>RepositorioDeCasos</c> lo resuelve por
///   numero de caso, nombre y MRN, con el texto como PARAMETRO (nunca pegado a la consulta).
///   O sea que buscar de verdad no pide ni una linea fuera de <c>Cascara/</c>.</item>
///   <item>El dueno puede tener 3 000 documentos. Un buscador que consulte en cada tecla
///   pulsada y se traiga todo lo que casa paga esos 3 000 por letra.</item>
///   <item>El mockup escribe «Buscar caso, unidad o cedula». <b>Unidad no se busca</b>
///   —<c>FiltroDeCasos.Texto</c> no mira <c>unidad_numero</c>— y anadirlo es tocar
///   <c>Fichas.Datos</c>, que no es este terreno. Un marcador que prometa un campo que no se
///   busca es la misma enfermedad que el boton mudo, asi que el marcador dice lo que hay.</item>
/// </list>
///
/// <para>⚠️ <b>Lo que NO miran:</b> el coste sobre SQLite de verdad. Aqui detras hay
/// <c>Fichas.Datos.Falso</c>, que filtra en memoria; los milisegundos que salgan de aqui son
/// del filtro en memoria, no de la base. Lo de la base va medido con la ventana abierta.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelBuscadorDeLaCabecera
{
    /// <summary>
    /// Cuantos casos inventados se ponen detras cuando la ESCALA es lo que se mide. Es la
    /// cifra del requisito 5 del dueno.
    /// </summary>
    /// <remarks>
    /// ⚠️ Solo la usa la prueba que afirma algo sobre la escala. Las demas montan
    /// <see cref="CasosQueBastanParaLaRegla"/>, y el motivo esta alli.
    /// </remarks>
    private const int CasosQueSePonenDetras = 3000;

    /// <summary>
    /// Cuantos casos bastan cuando lo que se comprueba no depende de la escala.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>No es tacaneria: es que esta suite corre en paralelo.</b>
    /// <c>Paralelismo.cs</c> declara <c>Parallelize(Workers = 0, Scope = MethodLevel)</c>, asi
    /// que cada base de 3 000 que se monta aqui le quita procesador a las pruebas de tiempo
    /// que corren a la vez — un efecto que este proyecto ya midio y escribio en
    /// <c>Inicio/PruebasDelHomeQueCuentaPersonas.cs</c>: la misma llamada, <b>94-103 ms</b>
    /// sola y <b>200 y 312 ms</b> con la suite encima.
    ///
    /// <para>Medido el 2026-09-09: bajar de 3 000 a 200 las tres pruebas que no afirman nada
    /// de la escala no quita ni una comprobacion —«C2» sigue casando con mas de los ocho que
    /// caben, que es lo unico que cada una necesita— y quita tres generaciones de 3 000.</para>
    /// </remarks>
    private const int CasosQueBastanParaLaRegla = 200;

    /// <summary>Con menos de esto tecleado no se toca la base.</summary>
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("a")]
    public void ConMuyPocoTecleadoNiSiquieraSePreguntaALaBase(string tecleado)
    {
        var espia = new CasosEspiados(new ServiciosFalsos(50).Casos);
        var encontrado = new BusquedaDeLaCabecera(espia).Buscar(tecleado);

        Console.WriteLine($"tecleado «{tecleado}» -> consultas a la base: {espia.CuantasVecesSePregunto}");

        Assert.AreEqual(
            0, espia.CuantasVecesSePregunto,
            "Preguntar a la base por una sola letra la recorre entera para nada.");
        Assert.IsEmpty(encontrado.Renglones);
        Assert.AreEqual(0, encontrado.Total);
    }

    /// <summary>Con texto suficiente se pregunta UNA vez, no una por cosa que se ensena.</summary>
    [TestMethod]
    public void ConTextoSuficienteSePreguntaUnaSolaVez()
    {
        var falsos = new ServiciosFalsos(CasosQueBastanParaLaRegla);
        var espia = new CasosEspiados(falsos.Casos);

        var alguno = falsos.Casos.Listar(FiltroDeCasos.Todo, Pagina.Primera(1)).Elementos[0];
        var encontrado = new BusquedaDeLaCabecera(espia).Buscar(alguno.NumeroCaso!);

        Console.WriteLine(
            $"buscando «{alguno.NumeroCaso}» -> {encontrado.Renglones.Count} renglones, "
            + $"{encontrado.Total} en total, {espia.CuantasVecesSePregunto} consultas");

        Assert.AreEqual(1, espia.CuantasVecesSePregunto);
        Assert.IsNotEmpty(encontrado.Renglones, "El caso que existe no aparecio en su propia busqueda.");
    }

    /// <summary>Nunca se traen mas renglones de los que caben en el desplegable.</summary>
    /// <remarks>
    /// Un texto corto casa con cientos. Traerlos todos para ensenar ocho es pagar filas para
    /// tirarlas. La escala de verdad —3 000— la afirma
    /// <see cref="TeclearSobreTresMilDocumentosCuestaUnaConsultaYUnTrozoAcotado"/>; aqui basta
    /// con que casen mas de los que caben, y eso ya pasa con 200.
    /// </remarks>
    [TestMethod]
    public void NuncaSeTraenMasRenglonesDeLosQueCabenEnElDesplegable()
    {
        var falsos = new ServiciosFalsos(CasosQueBastanParaLaRegla);
        var espia = new CasosEspiados(falsos.Casos);

        // Una sola letra repetida casa con muchisimos: es el peor caso, no uno comodo.
        var encontrado = new BusquedaDeLaCabecera(espia).Buscar("C2");

        Console.WriteLine(
            $"buscando «C2» sobre {CasosQueBastanParaLaRegla} casos -> {encontrado.Renglones.Count} renglones "
            + $"de {encontrado.Total} que casan; el trozo pedido fue de {espia.ElUltimoTrozo.Tamano}");

        Assert.IsLessThanOrEqualTo(
            BusquedaDeLaCabecera.RenglonesQueSeEnsenan, encontrado.Renglones.Count);
        Assert.IsLessThanOrEqualTo(
            BusquedaDeLaCabecera.RenglonesQueSeEnsenan, espia.ElUltimoTrozo.Tamano,
            "Se pidio a la base un trozo mas grande que lo que se va a ensenar.");
    }

    /// <summary>
    /// Teclear sobre 3 000 documentos cuesta UNA consulta y un trozo acotado por tecla,
    /// no un recorrido de los 3 000.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>Esta prueba CUENTA y no cronometra, y eso es deliberado.</b> La primera
    /// version que se escribio aqui afirmaba «menos de 250 ms por busqueda», y eso va contra
    /// una regla que este proyecto ya tenia escrita, en
    /// <c>Inicio/PruebasDelHomeQueCuentaPersonas.cs</c>: «un tope en milisegundos dentro de
    /// esta suite no mide el codigo: mide la maquina», porque
    /// <c>Paralelismo.cs</c> declara <c>Parallelize(Workers = 0)</c> y esta prueba comparte
    /// procesador con otras que montan bases de 3 000. Alli ya se midio la MISMA llamada en
    /// <b>94-103 ms</b> corriendo sola y <b>200 y 312 ms</b> con la suite encima.</para>
    ///
    /// <para><b>Lo que si es determinista, y es lo que de verdad protege al dueno:</b> que
    /// por cada tecla se vaya UNA vez a la base y se pida un trozo de como mucho lo que cabe
    /// en el desplegable. Eso no depende de la maquina, y es justo el defecto que se queria
    /// evitar: recorrer 3 000 filas para ensenar ocho.</para>
    ///
    /// <para>Los milisegundos se siguen <b>escribiendo</b> —son utiles para la entrega— pero
    /// no se afirman. Y son del filtro EN MEMORIA de <c>Fichas.Datos.Falso</c>, no de SQLite:
    /// no valen para afirmar nada de la base del dueno.</para>
    /// </remarks>
    [TestMethod]
    public void TeclearSobreTresMilDocumentosCuestaUnaConsultaYUnTrozoAcotado()
    {
        var espia = new CasosEspiados(new ServiciosFalsos(CasosQueSePonenDetras).Casos);
        var buscador = new BusquedaDeLaCabecera(espia);

        // «C2» es el peor caso: casa con casi todos los numeros de caso inventados.
        var encontrado = buscador.Buscar("C2");

        var reloj = Stopwatch.StartNew();
        for (var i = 0; i < 10; i++) buscador.Buscar("C2");
        reloj.Stop();
        var porBusqueda = reloj.Elapsed.TotalMilliseconds / 10;

        Console.WriteLine(
            $"sobre {CasosQueSePonenDetras} casos: {encontrado.Total} casan, se ensenan "
            + $"{encontrado.Renglones.Count}, el trozo pedido fue de {espia.ElUltimoTrozo.Tamano}.");
        Console.WriteLine(
            $"y 10 busquedas tardaron {porBusqueda:N1} ms cada una — cifra INFORMATIVA, "
            + "del filtro en memoria y con la suite en paralelo; no se afirma.");

        Assert.AreEqual(
            11, espia.CuantasVecesSePregunto,
            "Once busquedas tenian que ser once consultas: ni una de regalo por renglon ensenado.");
        Assert.IsLessThanOrEqualTo(
            BusquedaDeLaCabecera.RenglonesQueSeEnsenan, espia.ElUltimoTrozo.Tamano,
            $"Se pidio a la base un trozo de {espia.ElUltimoTrozo.Tamano} para ensenar "
            + $"{BusquedaDeLaCabecera.RenglonesQueSeEnsenan}: son filas traidas para tirarlas.");
        Assert.IsGreaterThan(
            BusquedaDeLaCabecera.RenglonesQueSeEnsenan, encontrado.Total,
            "Para que esta prueba signifique algo tienen que casar mas de los que caben.");
    }

    /// <summary>
    /// Lo que se ensena identifica el documento y NO saca a nadie por su nombre.
    /// </summary>
    /// <remarks>
    /// ⛔ El buscador BUSCA por nombre y por MRN —eso lo hace util— pero no los PINTA. Un
    /// desplegable que al teclear tres letras suelta una lista de nombres y numeros de
    /// expediente esta ensenando datos de personas reales a quien pase por delante de la
    /// pantalla, y nadie lo ha pedido. El numero de caso y la unidad identifican el
    /// documento igual de bien.
    /// </remarks>
    [TestMethod]
    public void NingunRenglonEnsenaElNombreNiElMrnDeNadie()
    {
        var falsos = new ServiciosFalsos(200);
        var buscador = new BusquedaDeLaCabecera(falsos.Casos);

        var caso = falsos.Casos.Listar(FiltroDeCasos.Todo, Pagina.Primera(1)).Elementos[0];
        var personas = falsos.Personas.DeCaso(caso.Id);
        Assert.IsNotEmpty(personas, "El caso de prueba no tiene personas: el barrido no comprobaria nada.");

        var encontrado = buscador.Buscar(caso.NumeroCaso!);
        var renglon = encontrado.Renglones.First(r => r.CasoId == caso.Id);
        Console.WriteLine($"renglon -> «{renglon.ComoSeLee}»");

        var filtrados = new List<string>();
        foreach (var persona in personas)
        {
            Console.WriteLine($"  no debe salir: nombre «{persona.Nombre}», MRN «{persona.Mrn}»");

            if (!string.IsNullOrWhiteSpace(persona.Nombre)
                && renglon.ComoSeLee.Contains(persona.Nombre, StringComparison.OrdinalIgnoreCase))
                filtrados.Add($"sale el nombre «{persona.Nombre}»");

            if (!string.IsNullOrWhiteSpace(persona.Mrn)
                && renglon.ComoSeLee.Contains(persona.Mrn, StringComparison.OrdinalIgnoreCase))
                filtrados.Add($"sale el MRN «{persona.Mrn}»");
        }

        Assert.IsEmpty(filtrados, string.Join(Environment.NewLine, filtrados));
        StringAssert.Contains(
            renglon.ComoSeLee, caso.NumeroCaso!,
            "Sin el numero de caso, el renglon no dice cual documento es.");
    }

    /// <summary>El marcador nombra los campos que se buscan, y solo esos.</summary>
    /// <remarks>
    /// ⛔ El mockup pone «Buscar caso, unidad o cedula». Medido sobre
    /// <c>RepositorioDeCasos.cs</c> el 2026-09-09, el filtro compara <c>numero_caso</c>,
    /// <c>personas.nombre</c> y <c>personas.mrn</c>: unidad NO. Prometerla es mentir al que
    /// teclea el numero de una unidad y no encuentra nada.
    /// </remarks>
    [TestMethod]
    public void ElMarcadorNombraLosCamposQueDeVerdadSeBuscan()
    {
        var marcador = BusquedaDeLaCabecera.ElMarcador;
        Console.WriteLine($"marcador -> «{marcador}»");

        foreach (var campo in new[] { "caso", "nombre", "MRN" })
            StringAssert.Contains(marcador, campo, $"El marcador no nombra «{campo}», que si se busca.");

        foreach (var campo in new[] { "unidad", "cédula", "cedula" })
            Assert.IsFalse(
                marcador.Contains(campo, StringComparison.OrdinalIgnoreCase),
                $"El marcador promete «{campo}» y el filtro no lo busca.");
    }

    /// <summary>Un texto que solo son comodines no arrastra la base entera.</summary>
    /// <remarks>
    /// <para><c>FiltroDeCasos.Texto</c> se pega entre <c>%</c> y se manda como parametro, asi
    /// que una comilla no es inyeccion. Pero <c>%</c> y <c>_</c> SI son comodines de
    /// <c>LIKE</c>: teclear <c>%</c> equivale a pedirlo todo sin querer.</para>
    ///
    /// <para>⚠️ Lo que esto NO arregla: <c>a%b</c> sigue siendo un comodin. Escaparlo pide un
    /// <c>ESCAPE</c> en la consulta, que vive en <c>Fichas.Datos</c> y no en este terreno.
    /// Va dicho en la entrega en vez de arreglado a escondidas.</para>
    /// </remarks>
    [TestMethod]
    [DataRow("%")]
    [DataRow("%%")]
    [DataRow("__")]
    [DataRow("%_%")]
    public void UnTextoQueSoloSonComodinesNoArrastraLaBaseEntera(string tecleado)
    {
        var espia = new CasosEspiados(new ServiciosFalsos(200).Casos);
        var encontrado = new BusquedaDeLaCabecera(espia).Buscar(tecleado);

        Console.WriteLine($"tecleado «{tecleado}» -> consultas: {espia.CuantasVecesSePregunto}, "
            + $"renglones: {encontrado.Renglones.Count}");

        Assert.AreEqual(0, espia.CuantasVecesSePregunto);
        Assert.IsEmpty(encontrado.Renglones);
    }

    /// <summary>Un texto con comillas o punto y coma no rompe nada ni trae de mas.</summary>
    [TestMethod]
    [DataRow("'; DROP TABLE casos; --")]
    [DataRow("\" OR 1=1 --")]
    public void UnTextoConPintaDeAtaqueSeTrataComoTextoYNoTraeNada(string tecleado)
    {
        var falsos = new ServiciosFalsos(200);
        var antes = falsos.Casos.Contar(FiltroDeCasos.Todo);

        var encontrado = new BusquedaDeLaCabecera(falsos.Casos).Buscar(tecleado);
        var despues = falsos.Casos.Contar(FiltroDeCasos.Todo);

        Console.WriteLine($"tecleado «{tecleado}» -> {encontrado.Total} casan; casos antes {antes}, despues {despues}");

        Assert.AreEqual(0, encontrado.Total, "Un texto que no es de nadie no puede casar con nada.");
        Assert.AreEqual(antes, despues, "La busqueda cambio el numero de casos.");
    }

    /// <summary>El resumen dice cuantos hay detras cuando no caben todos.</summary>
    [TestMethod]
    public void ElResumenDiceCuantosQuedanFueraDelDesplegable()
    {
        var buscador = new BusquedaDeLaCabecera(new ServiciosFalsos(CasosQueBastanParaLaRegla).Casos);
        var encontrado = buscador.Buscar("C2");

        Console.WriteLine($"resumen -> «{encontrado.Resumen}» (total {encontrado.Total})");

        Assert.IsGreaterThan(
            BusquedaDeLaCabecera.RenglonesQueSeEnsenan, encontrado.Total,
            "Para esta prueba hace falta que casen mas de los que caben.");
        StringAssert.Contains(
            encontrado.Resumen,
            encontrado.Total.ToString(System.Globalization.CultureInfo.CurrentCulture),
            "El resumen no dice cuantos casan en total, asi que el usuario cree que hay ocho.");
    }

    /// <summary>
    /// Cuando casa UNO, el resumen lo dice en singular.
    /// </summary>
    /// <remarks>
    /// ⚠️ Es la regla que <c>Cascara/PruebasDeLaConcordanciaConUno.cs</c> ya protege en el
    /// resto de la pantalla, y este resumen nacio sin ella cubierta: se comprobaba el caso de
    /// «mas de los que caben» y nunca el de uno. Se prueba con 1 y con 2, porque solo con 1
    /// una frase clavada en singular pasaria en verde estando igual de rota al reves.
    /// </remarks>
    [TestMethod]
    public void ElResumenConcuerdaCuandoCasaUnoSolo()
    {
        Console.WriteLine($"casan 1 -> «{BusquedaDeLaCabecera.Resumen(1)}»");
        Console.WriteLine($"casan 2 -> «{BusquedaDeLaCabecera.Resumen(2)}»");

        Assert.AreEqual("1 documento.", BusquedaDeLaCabecera.Resumen(1));
        Assert.AreEqual("2 documentos.", BusquedaDeLaCabecera.Resumen(2));
    }

    /// <summary>
    /// La frase cambia en el sitio justo: al pasar de lo que cabe a lo que no cabe.
    /// </summary>
    /// <remarks>
    /// ⚠️ Se prueba en el borde —ocho y nueve— y no en un numero comodo. Un «se ven los 8
    /// primeros» puesto cuando casan exactamente 8 seria mentira al reves: se ven TODOS.
    /// </remarks>
    [TestMethod]
    public void LaFraseSoloAvisaDeQueFaltanCuandoDeVerdadFaltan()
    {
        var caben = BusquedaDeLaCabecera.RenglonesQueSeEnsenan;

        var justoLosQueCaben = BusquedaDeLaCabecera.Resumen(caben);
        var unoMas = BusquedaDeLaCabecera.Resumen(caben + 1);

        Console.WriteLine($"casan {caben} (los que caben) -> «{justoLosQueCaben}»");
        Console.WriteLine($"casan {caben + 1} (uno mas)    -> «{unoMas}»");

        Assert.AreEqual($"{caben} documentos.", justoLosQueCaben);
        Assert.Contains(
            "primeros", unoMas, StringComparison.Ordinal,
            "Casan mas de los que caben y la frase no avisa: se leeria como que no hay mas.");
        StringAssert.Contains(
            unoMas, (caben + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            "La frase no dice cuantos casan en total.");
    }

    /// <summary>Cuando no casa ninguno, la frase lo dice y no se queda en blanco.</summary>
    [TestMethod]
    public void LaFraseDiceQueNoHayNadaCuandoNoCasaNinguno()
    {
        var sinNada = BusquedaDeLaCabecera.Resumen(0);
        Console.WriteLine($"casan 0 -> «{sinNada}»");

        Assert.IsFalse(
            string.IsNullOrWhiteSpace(sinNada),
            "Sin frase, «no hay nada» y «no se busco» se ven igual.");
        Assert.DoesNotContain(
            "0 documento", sinNada, StringComparison.Ordinal,
            "«0 documentos» se lee como una cifra; lo que hace falta es decirlo con palabras.");
    }

    /// <summary>Cuando no casa ninguno, se dice, y no se deja el pie en blanco.</summary>
    /// <remarks>
    /// Un buscador que ante «no hay nada» no dice nada se lee como un buscador roto: el que
    /// teclea no sabe si no hay, o si no busco.
    /// </remarks>
    [TestMethod]
    public void CuandoNoCasaNingunoElResumenLoDice()
    {
        var buscador = new BusquedaDeLaCabecera(new ServiciosFalsos(CasosQueBastanParaLaRegla).Casos);
        var encontrado = buscador.Buscar("ZZQXNOEXISTE");

        Console.WriteLine($"sin resultados -> «{encontrado.Resumen}»");

        Assert.AreEqual(0, encontrado.Total);
        Assert.IsEmpty(encontrado.Renglones);
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(encontrado.Resumen),
            "Sin resumen, «no hay nada» y «no se busco» se ven igual.");
    }

    // ---- el espia -------------------------------------------------------------

    /// <summary>
    /// Un <see cref="ICasos"/> que delega todo y va contando cuantas veces se le pregunta.
    /// </summary>
    /// <remarks>
    /// Envuelve al de <c>Fichas.Datos.Falso</c> en vez de inventarse datos: asi lo que se
    /// cuenta son las consultas y no hay un segundo generador que mantener.
    /// </remarks>
    private sealed class CasosEspiados(ICasos deVerdad) : ICasos
    {
        public int CuantasVecesSePregunto { get; private set; }

        public Pagina ElUltimoTrozo { get; private set; }

        public PaginaDe<Caso> Listar(FiltroDeCasos filtro, Pagina trozo)
        {
            CuantasVecesSePregunto++;
            ElUltimoTrozo = trozo;
            return deVerdad.Listar(filtro, trozo);
        }

        public int Contar(FiltroDeCasos filtro) => deVerdad.Contar(filtro);

        public Caso? Obtener(long id) => deVerdad.Obtener(id);

        public IReadOnlyDictionary<long, int> ContarPersonasDe(IReadOnlyList<long> casoIds)
            => deVerdad.ContarPersonasDe(casoIds);

        public ResultadoDeEscritura Guardar(Caso caso) => deVerdad.Guardar(caso);

        public ResultadoDeEscritura MarcarEstado(
            long casoId, EstadoDeRecomendacion estado, long companeroId, string origen)
            => deVerdad.MarcarEstado(casoId, estado, companeroId, origen);

        public ResultadoDeEscritura MarcarEstadoDelCompanero(
            long casoId, EstadoDeRecomendacion estado, MotivoDeNoCompletar motivo,
            long companeroId, string origen)
            => deVerdad.MarcarEstadoDelCompanero(casoId, estado, motivo, companeroId, origen);

        public ResultadoDeEscritura Archivar(long casoId, bool archivado, string fechaDeArchivado)
            => deVerdad.Archivar(casoId, archivado, fechaDeArchivado);
    }
}
