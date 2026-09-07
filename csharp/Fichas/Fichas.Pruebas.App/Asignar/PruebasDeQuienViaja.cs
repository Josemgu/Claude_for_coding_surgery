using Fichas.App.Asignar;
using Fichas.Contratos.Consultas;

namespace Fichas.Pruebas.App.Asignar;

/// <summary>
/// En la lista de Asignar se ve QUIEN viaja, no solo el numero del papel y el barrio.
/// </summary>
/// <remarks>
/// <para><b>De donde sale este criterio.</b> Del dueno, 2026-09-06 (<c>DECISIONES.md</c>,
/// «EL DUEÑO MUEVE LA REGLA 5»): <i>«En asignado a los agentes necesito ver cuando se
/// completa el nombre de las personas, no de la rama o barrio»</i>. Y el motivo esta escrito
/// desde el 2026-09-05: <i>«es por persona que se revisa la informacion»</i> — a quien va a
/// llamar al obispo no le sirve un numero de expediente.</para>
///
/// <para>Estas pruebas se escribieron <b>desde esas dos frases</b> y desde el criterio de
/// aceptacion del pase, no mirando el codigo que sale despues.</para>
///
/// <para>⚠️ <b>Ni un nombre de persona real en este archivo.</b> Todos los que salen aqui
/// estan inventados, y las cedulas tambien: son 000-0000-000N a proposito, para que nadie
/// pueda confundir una de estas con una de verdad.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeQuienViaja
{
    /// <summary>Un documento de UNA persona dice su nombre, entero y sin adornos.</summary>
    [TestMethod]
    public void ElRenglonDeUnaSolaPersonaDiceSuNombre()
    {
        var banco = new BaseDePrueba();
        var caso = banco.Meter("BBBB0001", banco.FechaEn(10), null, false);
        banco.MeterPersonas(caso, "Ana Prueba");

        var renglon = banco.Lista.Ofrecer(Pagina.Primera(10)).Elementos.Single(r => r.CasoId == caso);

        Assert.AreEqual("Ana Prueba", renglon.QuienViaja, "Con una sola persona se dice su nombre y ya.");
        StringAssert.Contains(renglon.Titulo, "Ana Prueba", "Y va en la linea de arriba, que es donde el mira.");
    }

    /// <summary>
    /// Un documento de VARIAS dice el primero del formulario y cuantos mas van con el.
    /// </summary>
    /// <remarks>
    /// El caso normal es este: viajan familias. La linea es una y no se puede estirar, asi que
    /// se dice el primero —el de la fila 1 del formulario— y la cuenta de los demas. Lo que no
    /// se hace es pegar cuatro nombres y dejar que el recorte se coma el ultimo sin avisar: un
    /// renglon recortado no se distingue de uno que no tenia mas gente.
    /// </remarks>
    [TestMethod]
    public void ElRenglonDeVariasPersonasDiceElPrimeroYCuantosMasVanConEl()
    {
        var banco = new BaseDePrueba();
        var caso = banco.Meter("BBBB0002", banco.FechaEn(11), null, false);
        banco.MeterPersonas(caso, "Ana Prueba", "Beto Prueba", "Carla Prueba", "Dario Prueba");

        var renglon = banco.Lista.Ofrecer(Pagina.Primera(10)).Elementos.Single(r => r.CasoId == caso);

        Assert.AreEqual("Ana Prueba y 3 más", renglon.QuienViaja);
        Assert.AreEqual(4, renglon.Personas, "Y la cuenta sigue estando, que es otra pregunta.");
    }

    /// <summary>Con dos personas se dice «y 1 más», en singular; nunca «y 1 mases».</summary>
    [TestMethod]
    public void ConDosPersonasLaCuentaDeLosDemasVaEnSingular()
    {
        var banco = new BaseDePrueba();
        var caso = banco.Meter("BBBB0003", banco.FechaEn(12), null, false);
        banco.MeterPersonas(caso, "Ana Prueba", "Beto Prueba");

        var renglon = banco.Lista.Ofrecer(Pagina.Primera(10)).Elementos.Single(r => r.CasoId == caso);

        Assert.AreEqual("Ana Prueba y 1 más", renglon.QuienViaja);
    }

    /// <summary>
    /// El que lleva DIEZ no se queda en una cifra: los diez nombres estan, en el globo de ayuda.
    /// </summary>
    /// <remarks>
    /// Es la pregunta que el pase manda contestar —«que pasa con el que lleva diez»— y la
    /// respuesta es que la linea dice uno y la cuenta, y que los diez se leen sin salir de la
    /// pantalla. Si los diez solo estuvieran dentro del documento, la lista estaria escondiendo
    /// nueve tickets detras de uno.
    /// </remarks>
    [TestMethod]
    public void ElQueLlevaDiezDiceLosDiezNombresEnElGloboDeAyuda()
    {
        var banco = new BaseDePrueba();
        var caso = banco.Meter("BBBB0004", banco.FechaEn(13), null, false);
        var nombres = Enumerable.Range(1, 10).Select(n => $"Persona{n:D2} Prueba").ToArray();
        banco.MeterPersonas(caso, nombres);

        var renglon = banco.Lista.Ofrecer(Pagina.Primera(10)).Elementos.Single(r => r.CasoId == caso);

        Assert.AreEqual("Persona01 Prueba y 9 más", renglon.QuienViaja, "La linea dice el primero y la cuenta.");
        foreach (var nombre in nombres)
            StringAssert.Contains(renglon.QuienesViajan, nombre, $"«{nombre}» tiene que estar en el globo.");
        Assert.HasCount(10, renglon.QuienesViajan.Split('\n'), "Uno por linea: diez lineas.");
    }

    /// <summary>El barrio no desaparece: cambia de linea, que es lo que el dueno pidio.</summary>
    /// <remarks>
    /// Sus palabras son «ver el nombre», no «perder el barrio». La unidad baja al detalle, donde
    /// sigue estando a la vista y ya no le quita el sitio al nombre en la linea de arriba.
    /// </remarks>
    [TestMethod]
    public void LaUnidadSigueALaVistaEnElRenglon()
    {
        var banco = new BaseDePrueba();
        var caso = banco.Meter("BBBB0005", banco.FechaEn(14), null, false);
        banco.MeterPersonas(caso, "Ana Prueba");

        var renglon = banco.Lista.Ofrecer(Pagina.Primera(10)).Elementos.Single(r => r.CasoId == caso);

        StringAssert.Contains(renglon.Detalle, "Rama de prueba", "El barrio sigue leyendose en la fila.");
        Assert.DoesNotContain("Rama de prueba", renglon.Titulo, "Pero ya no ocupa la linea del nombre.");
    }

    /// <summary>
    /// Un documento del que no se leyo ninguna persona lo dice, y no se inventa un nombre.
    /// </summary>
    [TestMethod]
    public void UnDocumentoSinPersonasLoDiceYNoInventaNingunNombre()
    {
        var banco = new BaseDePrueba();
        var caso = banco.Meter("BBBB0006", banco.FechaEn(15), null, false);

        var renglon = banco.Lista.Ofrecer(Pagina.Primera(10)).Elementos.Single(r => r.CasoId == caso);

        Assert.AreEqual(RenglonParaAsignar.SinPersonas, renglon.QuienViaja);
        Assert.AreEqual(0, renglon.Personas);
    }

    /// <summary>
    /// Una persona sin nombre leido se dice asi, y su cedula NO sale en la lista.
    /// </summary>
    /// <remarks>
    /// Las dos mitades importan. Lo primero, porque un hueco se lee como «no hay nadie» y lo que
    /// pasa es que el reconocimiento no leyo ese nombre — es la misma palabra que ya usan Inicio
    /// (<c>LectorDelInicio.cs:212</c>) y Grupo (<c>LectorDeGrupos.cs:335</c>). Lo segundo,
    /// porque el MRN es la cedula de una persona de verdad y esta pantalla se mira con gente
    /// alrededor: en el reporte de la segunda vuelta va en su columna porque ese papel es para
    /// llamar por telefono, aqui no hace falta para reconocer el documento.
    /// </remarks>
    [TestMethod]
    public void UnaPersonaSinNombreLeidoSeDiceAsiYSuCedulaNoSaleEnLaLista()
    {
        var banco = new BaseDePrueba();
        var caso = banco.Meter("BBBB0007", banco.FechaEn(16), null, false);
        banco.MeterPersonaSoloConCedula(caso, "000-0000-0007");

        var renglon = banco.Lista.Ofrecer(Pagina.Primera(10)).Elementos.Single(r => r.CasoId == caso);

        Assert.AreEqual(RenglonParaAsignar.SinNombreLeido, renglon.QuienViaja);
        Assert.DoesNotContain("000-0000-0007", renglon.Titulo, "La cedula no se pinta en la lista.");
        Assert.DoesNotContain("000-0000-0007", renglon.Detalle);
        Assert.DoesNotContain("000-0000-0007", renglon.QuienesViajan);
    }

    /// <summary>Dar el caso a un companero no le quita el nombre al renglon.</summary>
    /// <remarks>
    /// Es el punto del criterio que parece de perogrullo y no lo es: el renglon se vuelve a
    /// componer despues de asignar, con otro camino de lectura, y ahi es donde un dato compuesto
    /// aparte se cae sin que nadie lo note.
    /// </remarks>
    [TestMethod]
    public void AsignarElCasoNoLeQuitaElNombreAlRenglon()
    {
        var banco = new BaseDePrueba();
        var caso = banco.Meter("BBBB0008", banco.FechaEn(17), null, false);
        banco.MeterPersonas(caso, "Ana Prueba", "Beto Prueba");
        var destino = banco.Activos[0];

        var antes = banco.Lista.Ofrecer(Pagina.Primera(10)).Elementos.Single(r => r.CasoId == caso);
        banco.Operacion.AsignarVarios([caso], destino.Id, destino.Nombre);
        var despues = banco.Lista.Ofrecer(Pagina.Primera(10)).Elementos.Single(r => r.CasoId == caso);

        Assert.AreEqual(RenglonParaAsignar.SinAsignar, antes.AsignadoA, "Antes no lo llevaba nadie.");
        Assert.AreEqual(destino.Nombre, despues.AsignadoA, "Ahora lo lleva el companero.");
        Assert.AreEqual("Ana Prueba y 1 más", despues.QuienViaja, "Y sigue diciendo quien viaja.");
        StringAssert.Contains(despues.Detalle, destino.Nombre, "Las dos cosas en la misma fila.");
    }

    /// <summary>
    /// Los nombres de una pantallada se piden de UNA vez, no una vez por fila.
    /// </summary>
    /// <remarks>
    /// Con 3 000 documentos, una consulta por fila son 3 000 consultas al recorrer la lista. El
    /// cronometro no lo caza —60 consultas pequenas caben de sobra en el techo— asi que lo que
    /// se vigila aqui es la forma de preguntar. Es la misma que ya usaba
    /// <c>ICasos.ContarPersonasDe</c> para las cuentas.
    /// </remarks>
    [TestMethod]
    public void LosNombresDeUnaPantalladaSePidenDeUnaVezYNoUnaVezPorFila()
    {
        var banco = new BaseDePrueba(200);
        banco.Lista.Ofrecer(new Pagina(0, 60));   // calentamiento
        banco.Personas.EmpezarDeCero();

        var trozo = banco.Lista.Ofrecer(new Pagina(0, 60));

        Assert.HasCount(60, trozo.Elementos, "La pantallada trae sus 60 filas.");
        Assert.AreEqual(
            0,
            banco.Personas.VecesQueSePreguntoPorUnCaso,
            "Ni una sola pregunta caso por caso: con 3 000 documentos eso son 3 000 consultas.");
        Assert.IsLessThanOrEqualTo(
            2,
            banco.Personas.PreguntasEnTotal,
            $"Toda la pantallada se lee en 2 preguntas como mucho; se hicieron {banco.Personas.PreguntasEnTotal}.");
    }

    /// <summary>
    /// El renglon sigue escribiendo «1 persona» con uno y «3 personas» con tres.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Esta prueba viene de <c>Cascara/PruebasDeLaConcordanciaConUno</c></b>, donde exigia
    /// ademas que la cifra fuera lo PRIMERO del detalle —<c>StartsWith("1 persona ·")</c>—. Eso
    /// dejo de ser cierto el 2026-09-06, cuando la unidad bajo a esa linea para dejarle la de
    /// arriba al nombre. Se movio en vez de borrarse porque lo que vigilaba sigue vivo y sigue
    /// siendo facil de romper: es el defecto que QA midio en pantalla el 2026-09-04
    /// («1 persona(s)»). Lo unico que se solto es la posicion; la concordancia se comprueba
    /// igual, con uno y con tres.
    /// </remarks>
    [TestMethod]
    public void ElRenglonDeAsignarConcuerdaConSusPersonas()
    {
        var uno = new RenglonParaAsignar { Personas = 1, FechaDeViaje = "2026-10-01" };
        var varios = uno with { Personas = 3 };

        StringAssert.Contains(uno.Detalle, "1 persona ·");
        StringAssert.Contains(varios.Detalle, "3 personas ·");
        Assert.DoesNotContain("(s)", uno.Detalle);
    }

    /// <summary>
    /// Con 3 000 documentos, ni un renglon se queda mudo y la cuenta concuerda con los nombres.
    /// </summary>
    /// <remarks>
    /// <para>Las dos cifras de la fila salen de DOS lecturas distintas —<c>ContarPersonasDe</c>
    /// para el numero y las personas para los nombres— y por eso pueden separarse en silencio.
    /// Aqui se obliga a que digan lo mismo en los 2 766 documentos, no en uno.</para>
    ///
    /// <para>Escribe ademas el reparto de personas por documento, que es la unica base de datos
    /// con la que se pudo medir esto: la del dueno no se toca ni para mirar.</para>
    /// </remarks>
    [TestMethod]
    public void ConTresMilDocumentosNiUnRenglonSeQuedaMudoYLaCuentaConcuerda()
    {
        var banco = new BaseDePrueba(3000);

        var ofrecidos = banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos;
        var reparto = ofrecidos
            .GroupBy(r => r.Personas)
            .OrderBy(g => g.Key)
            .Select(g => $"{g.Key}→{g.Count()}")
            .ToList();

        Console.WriteLine(
            $"Asignar · {ofrecidos.Count} documentos ofrecidos, "
            + $"{ofrecidos.Sum(r => r.Personas)} personas dentro");
        Console.WriteLine("Asignar · personas por documento: " + string.Join(" · ", reparto));

        Assert.IsEmpty(
            ofrecidos.Where(r => string.IsNullOrWhiteSpace(r.QuienViaja)).ToList(),
            "Ni un renglon en blanco: o dice quien viaja, o dice por que no puede decirlo.");
        Assert.IsEmpty(
            ofrecidos.Where(r => r.Personas > 0 && r.QuienesViajan.Split('\n').Length != r.Personas).ToList(),
            "La cuenta de personas y la lista de nombres salen de dos lecturas y tienen que concordar.");
    }

    /// <summary>Con diez personas el renglon sigue cabiendo en dos lineas (requisito 4).</summary>
    [TestMethod]
    public void ConDiezPersonasElRenglonSigueCabiendoEnDosLineas()
    {
        var banco = new BaseDePrueba();
        var caso = banco.Meter("BBBB0009", banco.FechaEn(18), null, false);
        banco.MeterPersonas(caso, Enumerable.Range(1, 10).Select(n => $"Persona{n:D2} Prueba").ToArray());

        var renglon = banco.Lista.Ofrecer(Pagina.Primera(10)).Elementos.Single(r => r.CasoId == caso);

        Assert.DoesNotContain("\n", renglon.Titulo, "El titulo es una linea.");
        Assert.DoesNotContain("\n", renglon.Detalle, "El detalle es una linea.");
    }
}
