using Fichas.App.Revisar;
using Fichas.Contratos.Modelos;
using Fichas.Pruebas.App.Asignar;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// Los tres estados que el dueno pidio, puestos por Miguel desde donde el trabaja.
/// </summary>
/// <remarks>
/// <para>Nacen del criterio, no del codigo. Palabras del dueno: «no completado, no se pudo
/// comunicar con el lider, o el lider no lo hizo». La migracion 18 dejo el vocabulario y las
/// dos columnas —<c>casos.motivo_no_completa</c> y <c>casos.motivo_del_companero</c>— y la
/// pantalla del grupo ya sabia leerlas; medido con grep el 2026-09-05, <b>nadie las
/// escribia</b>, asi que todo salia como «no completado».</para>
///
/// <para>Lo que se comprueba aqui es lo de MIGUEL. Lo que viene de la hoja del companero se
/// escribe por <c>ICasos.MarcarEstadoDelCompanero</c> y va a la OTRA columna; esa mitad es
/// de otro terreno y aqui solo se comprueba que esta mano no la pisa.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelMotivoDeMiguel
{
    /// <summary>Los tres motivos se escriben en <c>motivo_no_completa</c> y se releen tal cual.</summary>
    [TestMethod]
    public void LosTresQuePidioElDuenoSeEscribenYSeReleenEnLaColumnaVigente()
    {
        var banco = new BaseDePrueba();
        var quien = banco.Activos[0].Id;

        var esperado = new (MotivoDeNoCompletar Motivo, string? Clave)[]
        {
            (MotivoDeNoCompletar.SinMotivo, null),
            (MotivoDeNoCompletar.NoSePudoComunicar, "no_se_pudo_comunicar"),
            (MotivoDeNoCompletar.ElLiderNoLoHizo, "el_lider_no_lo_hizo"),
        };

        foreach (var (motivo, clave) in esperado)
        {
            var casoId = banco.Meter($"MMMM{(int)motivo:D4}", banco.FechaEn(12), null, false);

            var resultado = banco.Acciones.MarcarAMano(casoId, EstadoDeRecomendacion.NoCompleta, motivo, quien);

            var leido = banco.Servicios.Casos.Obtener(casoId)!;
            Assert.IsTrue(resultado.SeEscribio, $"«{motivo}» tiene que escribirse.");
            Assert.AreEqual(clave, leido.MotivoNoCompleta, $"«{motivo}» va a motivo_no_completa.");
            Assert.AreEqual(motivo, leido.Motivo, "Y se relee como el mismo motivo.");
            Assert.IsNull(leido.MotivoDelCompanero, "Y NUNCA a motivo_del_companero, que es del companero.");
            Assert.AreEqual(EstadoDeRecomendacion.NoCompleta, leido.Estado, "El estado sigue siendo «no completa».");
        }
    }

    /// <summary>El motivo se ve en la tarjeta, sin abrir el documento.</summary>
    [TestMethod]
    public void ElMotivoSeLeeEnLaTarjetaSinAbrirElDocumento()
    {
        var banco = new BaseDePrueba();
        var quien = banco.Activos[0].Id;
        var noSePudo = banco.Meter("NNNN0001", banco.FechaEn(12), null, false);
        var noLoHizo = banco.Meter("NNNN0002", banco.FechaEn(13), null, false);
        var aSecas = banco.Meter("NNNN0003", banco.FechaEn(14), null, false);

        banco.Acciones.MarcarAMano(noSePudo, EstadoDeRecomendacion.NoCompleta, MotivoDeNoCompletar.NoSePudoComunicar, quien);
        banco.Acciones.MarcarAMano(noLoHizo, EstadoDeRecomendacion.NoCompleta, MotivoDeNoCompletar.ElLiderNoLoHizo, quien);
        banco.Acciones.MarcarAMano(aSecas, EstadoDeRecomendacion.NoCompleta, MotivoDeNoCompletar.SinMotivo, quien);
        banco.Tablero.Cargar();

        Assert.AreEqual("no se pudo comunicar con el líder", banco.Tablero.De(noSePudo)!.LineaDelMotivo);
        Assert.AreEqual("el líder no lo hizo", banco.Tablero.De(noLoHizo)!.LineaDelMotivo);
        Assert.AreEqual(string.Empty, banco.Tablero.De(aSecas)!.LineaDelMotivo, "«No completado» a secas no inventa motivo.");
    }

    /// <summary>El motivo se cambia por otro y se quita, y la base y la tarjeta lo siguen.</summary>
    [TestMethod]
    public void ElMotivoSeCambiaYSeQuita()
    {
        var banco = new BaseDePrueba();
        var quien = banco.Activos[0].Id;
        var casoId = banco.Meter("QQQQ0001", banco.FechaEn(12), null, false);

        banco.Acciones.MarcarAMano(casoId, EstadoDeRecomendacion.NoCompleta, MotivoDeNoCompletar.NoSePudoComunicar, quien);
        var primero = banco.Servicios.Casos.Obtener(casoId)!.MotivoNoCompleta;

        banco.Acciones.MarcarAMano(casoId, EstadoDeRecomendacion.NoCompleta, MotivoDeNoCompletar.ElLiderNoLoHizo, quien);
        var cambiado = banco.Servicios.Casos.Obtener(casoId)!.MotivoNoCompleta;

        banco.Acciones.MarcarAMano(casoId, EstadoDeRecomendacion.NoCompleta, MotivoDeNoCompletar.SinMotivo, quien);
        var quitado = banco.Servicios.Casos.Obtener(casoId)!;
        banco.Tablero.Cargar();

        Assert.AreEqual("no_se_pudo_comunicar", primero);
        Assert.AreEqual("el_lider_no_lo_hizo", cambiado, "Cambiar de motivo pisa el anterior, no lo suma.");
        Assert.IsNull(quitado.MotivoNoCompleta, "Quitarlo deja la columna vacia, no una palabra que diga «ninguno».");
        Assert.AreEqual(EstadoDeRecomendacion.NoCompleta, quitado.Estado, "Y el caso sigue sin estar completo.");
        Assert.AreEqual(string.Empty, banco.Tablero.De(casoId)!.LineaDelMotivo);
    }

    /// <summary>
    /// Dar por completo un caso que tenia motivo se lo lleva: un motivo huerfano seria mentira.
    /// </summary>
    [TestMethod]
    public void CompletarSeLlevaElMotivoQueHabia()
    {
        var banco = new BaseDePrueba();
        var quien = banco.Activos[0].Id;
        var casoId = banco.Meter("CCCC9001", banco.FechaEn(12), null, false);
        banco.Acciones.MarcarAMano(casoId, EstadoDeRecomendacion.NoCompleta, MotivoDeNoCompletar.ElLiderNoLoHizo, quien);

        banco.Acciones.MarcarAMano(casoId, EstadoDeRecomendacion.Completa, quien);

        var leido = banco.Servicios.Casos.Obtener(casoId)!;
        Assert.AreEqual(EstadoDeRecomendacion.Completa, leido.Estado);
        Assert.IsNull(leido.MotivoNoCompleta, "Una recomendacion completa no puede llevar un «por que no se completo».");
    }

    /// <summary>
    /// Lo que dijo la hoja del companero NO se toca cuando Miguel pone el suyo, ni al reves.
    /// </summary>
    /// <remarks>
    /// Es la razon de ser de las dos columnas (ADR-0005 §3.3): si Miguel corrige encima, tiene
    /// que seguir sabiendose que el companero dijo otra cosa. Sin esto nadie sabria que
    /// discreparon.
    /// </remarks>
    [TestMethod]
    public void ElMotivoDeMiguelNoPisaElQueDijoElCompanero()
    {
        var banco = new BaseDePrueba();
        var elCompanero = banco.Activos[1];
        var miguel = banco.Activos[0];
        var casoId = banco.Meter("DDDD0001", banco.FechaEn(12), null, false);

        banco.Servicios.Casos.MarcarEstadoDelCompanero(
            casoId, EstadoDeRecomendacion.NoCompleta, MotivoDeNoCompletar.NoSePudoComunicar,
            elCompanero.Id, @"C:\hojas\devuelta.xlsx");
        banco.Acciones.MarcarAMano(casoId, EstadoDeRecomendacion.NoCompleta, MotivoDeNoCompletar.ElLiderNoLoHizo, miguel.Id);

        var leido = banco.Servicios.Casos.Obtener(casoId)!;
        Assert.AreEqual("el_lider_no_lo_hizo", leido.MotivoNoCompleta, "El de Miguel es el que vale ahora.");
        Assert.AreEqual("no_se_pudo_comunicar", leido.MotivoDelCompanero, "Y el del companero se queda tal cual.");
        Assert.AreEqual(elCompanero.Id, leido.EstadoDelCompaneroPor, "Con su nombre detras, sin pisar.");
    }

    /// <summary>
    /// Poner un motivo NO firma ningun campo: la firma es de Miguel sobre los datos y es otra cosa.
    /// </summary>
    /// <remarks>
    /// Regla permanente 5, precisada por el dueno el 2026-09-03. <c>IProcedencia.Firmar</c> es
    /// el unico camino a <c>verificado = 1</c>, y esto no pasa por ahi.
    /// </remarks>
    [TestMethod]
    public void PonerUnMotivoNoFirmaNingunCampo()
    {
        var banco = new BaseDePrueba();
        var quien = banco.Activos[0].Id;
        var casoId = banco.Meter("FFFF0001", banco.FechaEn(12), null, false);
        banco.Servicios.Procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos,
            RegistroId = casoId,
            Campo = nameof(Caso.NumeroCaso),
            Origen = OrigenDeCampo.Ocr,
            Confianza = 0.9,
        });

        banco.Acciones.MarcarAMano(casoId, EstadoDeRecomendacion.NoCompleta, MotivoDeNoCompletar.ElLiderNoLoHizo, quien);

        Assert.AreEqual(
            0,
            banco.Servicios.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, casoId),
            "Decir por que no se completo no es dar por bueno ningun dato del papel.");
    }

    /// <summary>
    /// Un motivo sobre un estado que no es «no completa» no se escribe, y se dice por que.
    /// </summary>
    /// <remarks>
    /// No es dato mal leido que haya que conservar: es una contradiccion del programa, y
    /// dejarla escrita pondria «completa porque el lider no lo hizo» en la base del dueno.
    /// </remarks>
    [TestMethod]
    public void UnMotivoSobreUnCasoCompletoNoSeEscribeYSeAvisa()
    {
        var banco = new BaseDePrueba();
        var quien = banco.Activos[0].Id;
        var casoId = banco.Meter("GGGG0001", banco.FechaEn(12), null, false);
        banco.Avisos.CerrarTodos();

        var resultado = banco.Acciones.MarcarAMano(
            casoId, EstadoDeRecomendacion.Completa, MotivoDeNoCompletar.ElLiderNoLoHizo, quien);

        Assert.IsFalse(resultado.SeEscribio, "No se escribe una contradiccion.");
        Assert.HasCount(1, banco.Avisos.Pendientes, "Se dice en la franja, no con un cuadro.");
        Assert.AreEqual(
            EstadoDeRecomendacion.SinMarcar,
            banco.Servicios.Casos.Obtener(casoId)!.Estado,
            "Y el caso se queda como estaba: ni el estado ni el motivo entran a medias.");
    }

    /// <summary>
    /// El menu de la tarjeta ofrece EXACTAMENTE los tres que el dueno nombro, en su orden.
    /// </summary>
    /// <remarks>
    /// <c>OtraRazon</c> existe en el vocabulario porque una hoja del companero puede traerlo,
    /// pero no se le ofrece a Miguel: el dueno nombro tres y un cajon de sastre en el menu es
    /// justo lo que hace que todo acabe ahi dentro.
    /// </remarks>
    [TestMethod]
    public void ElMenuOfreceLosTresDelDuenoYNadaMas()
    {
        var ofrecidos = AccionesDeRevisar.LosTresQuePidioElDueno;

        Assert.HasCount(3, ofrecidos);
        Assert.AreEqual(MotivoDeNoCompletar.SinMotivo, ofrecidos[0], "«No completado» a secas es el primero.");
        Assert.AreEqual(MotivoDeNoCompletar.NoSePudoComunicar, ofrecidos[1]);
        Assert.AreEqual(MotivoDeNoCompletar.ElLiderNoLoHizo, ofrecidos[2]);
        Assert.DoesNotContain(MotivoDeNoCompletar.OtraRazon, ofrecidos, "«Otra razon» es de la hoja, no del menu de Miguel.");
        CollectionAssert.AllItemsAreUnique(ofrecidos.ToList());
    }

    /// <summary>Cada entrada del menu dice su palabra en espanol, sin que el color vaya solo.</summary>
    [TestMethod]
    public void CadaEntradaDelMenuTraeSuPalabraEnEspanol()
    {
        var palabras = AccionesDeRevisar.LosTresQuePidioElDueno
            .Select(AccionesDeRevisar.DecirLaOpcion)
            .ToList();

        CollectionAssert.AreEqual(
            new List<string> { "No está completa, sin decir por qué", "No se pudo comunicar con el líder", "El líder no lo hizo" },
            palabras,
            "Son las palabras del dueno, y la primera dice ademas que quita el motivo.");
    }

    /// <summary>Si la hoja del companero dijo un motivo y Miguel no, la tarjeta lo dice de quien es.</summary>
    [TestMethod]
    public void LaTarjetaDistingueElMotivoDeMiguelDelQueDijoLaHoja()
    {
        var banco = new BaseDePrueba();
        var elCompanero = banco.Activos[1];
        var casoId = banco.Meter("HHHH0001", banco.FechaEn(12), null, false);

        banco.Servicios.Casos.MarcarEstadoDelCompanero(
            casoId, EstadoDeRecomendacion.NoCompleta, MotivoDeNoCompletar.NoSePudoComunicar,
            elCompanero.Id, @"C:\hojas\devuelta.xlsx");
        banco.Tablero.Cargar();

        Assert.AreEqual(
            "el compañero dijo: no se pudo comunicar con el líder",
            banco.Tablero.De(casoId)!.LineaDelMotivo,
            "Sin motivo propio se ensena el de la hoja, dicho de quien es.");
    }
}
