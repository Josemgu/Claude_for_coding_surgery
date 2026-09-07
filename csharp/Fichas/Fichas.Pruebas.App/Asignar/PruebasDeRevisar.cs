using Fichas.App.Asignar;
using Fichas.App.Revisar;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Asignar;

/// <summary>
/// Las pruebas de la pantalla de Revisar, SIN ventana: tablero de completados, firma,
/// archivar en lote y la fecha ya pasada.
/// </summary>
/// <remarks>
/// Nacen de las palabras del dueno del 2026-09-03, no del codigo: «el documento que ellos
/// llenan de Excel es el que marca, y dice completado por Sandy», «del tablero de
/// completados es que yo puedo seleccionar completo y archivarlo», y «si hay documento que
/// se sube y ya paso la fecha debe decir: esta fecha ya paso, revisar».
/// </remarks>
[TestClass]
public sealed class PruebasDeRevisar
{
    /// <summary>
    /// El tablero carga lo que hay SIN archivar; el archivado sale de la vista.
    /// </summary>
    /// <remarks>
    /// ⛔ Esta prueba decia lo contrario hasta el 2026-09-05 y pasaba en verde: afirmaba que el
    /// tablero carga «TODO, archivados incluidos». El criterio estaba mal escrito, no el
    /// programa. El dueno: «cuando yo archive, debe salir del sistema visible pero se queda
    /// como historico para los reportes». Lo que la prueba sigue vigilando es lo mismo —que el
    /// tablero carga lo que se ve y nada mas— pero contra lo que se pidio.
    /// </remarks>
    [TestMethod]
    public void ElTableroNoCargaLosArchivados()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();

        banco.Tablero.Cargar();

        Assert.AreEqual(6, banco.Tablero.Total, "Los seis sin archivar; el septimo esta archivado.");
        Assert.IsNull(banco.Tablero.De(ids[6]), "El archivado no estorba en la vista de trabajo.");
        Assert.IsNotNull(banco.Servicios.Casos.Obtener(ids[6]), "Y sigue entero en la base: archivar no borra.");
    }

    /// <summary>
    /// El archivado se puede ver a proposito y desarchivar; si no, archivar no tendria vuelta.
    /// </summary>
    [TestMethod]
    public void ElArchivadoSeVeAPropositoYSeDesarchiva()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();

        // Es lo que hace la casilla «Ver los archivados» de la pantalla.
        banco.Tablero.Cargar(conArchivados: true);
        var aLaVista = banco.Tablero.De(ids[6]);

        banco.Acciones.DesarchivarEnLote([ids[6]]);
        banco.Tablero.Cargar();

        Assert.IsNotNull(aLaVista, "Pidiendolos se ven: para desarchivar algo hay que poder verlo.");
        Assert.IsTrue(aLaVista.Archivado, "Y se ve diciendo que lo esta.");
        Assert.AreEqual(7, banco.Tablero.Total, "Desarchivado, vuelve solo a la vista de trabajo.");
        Assert.IsFalse(banco.Tablero.De(ids[6])!.Archivado);
        Assert.AreEqual(string.Empty, banco.Tablero.De(ids[6])!.FechaDeArchivado, "Y sin fecha de archivado colgando.");
    }

    /// <summary>
    /// El tablero de completados ensena la FIRMA de quien lo completo: «Completada por
    /// Sandy · fecha». Es lo que el dueno pidio para no mirar 300 uno a uno.
    /// </summary>
    [TestMethod]
    public void ElTableroDeCompletadosEnsenaLaFirmaDeQuienLoCompleto()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();
        // El companero cuyo Excel volvio. Se toma del equipo real, no de un nombre escrito
        // aqui: la firma tiene que salir de la fila de «companeros», no de la prueba.
        var elCompanero = banco.Activos[0];
        banco.Servicios.Casos.MarcarEstado(ids[4], EstadoDeRecomendacion.Completa, elCompanero.Id, "paquete devuelto");
        banco.Tablero.Cargar();

        var completados = banco.Tablero.Todas(FiltroDeTarjeta.Completadas);

        Assert.HasCount(2, completados, "Los dos marcados completa, y ni uno mas.");
        foreach (var tarjeta in completados)
        {
            StringAssert.StartsWith(tarjeta.Firma, "Completada por ", "La firma dice el verbo y el nombre.");
            Assert.Contains(" · ", tarjeta.Firma, "Y la fecha detras del punto medio.");
        }
        var suya = completados.Single(t => t.CasoId == ids[4]);
        StringAssert.Contains(suya.Firma, elCompanero.Nombre, "Con el nombre de quien lo completo.");
        StringAssert.Contains(suya.Firma, banco.Reloj.Ahora(), "Y con la fecha en la que se marco.");
    }

    /// <summary>Lo no marcado no lleva firma: la firma es un hecho, no un adorno de la tarjeta.</summary>
    [TestMethod]
    public void LoQueNadieHaMarcadoNoLlevaFirma()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();
        banco.Tablero.Cargar();

        Assert.AreEqual(string.Empty, banco.Tablero.De(ids[0])!.Firma);
        Assert.AreEqual(EstadoDeRecomendacion.SinMarcar, banco.Tablero.De(ids[0])!.Estado);
    }

    /// <summary>Un «no completa» se firma con su verbo propio; no se confunde con un completo.</summary>
    [TestMethod]
    public void ElNoCompletaLlevaSuPropiaFirma()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();
        banco.Tablero.Cargar();

        var tarjeta = banco.Tablero.De(ids[3])!;

        Assert.AreEqual(EstadoDeRecomendacion.NoCompleta, tarjeta.Estado);
        StringAssert.StartsWith(tarjeta.Firma, "Marcada no completa por ");
        Assert.AreEqual("no está completa", tarjeta.PalabraDelEstado, "El color nunca va solo.");
    }

    /// <summary>
    /// Los seis tableros cuentan lo suyo, y «Todo» cuadra con lo que hay SIN archivar.
    /// </summary>
    /// <remarks>
    /// ⛔ Hasta el 2026-09-05 este denominador era siete —los archivados dentro— y estaba mal:
    /// una pastilla que dice «Todo 7» sobre seis tarjetas es una cifra que no cuadra con lo que
    /// se ve. Lo que la prueba vigila sigue siendo lo mismo: que cada tablero cuenta lo suyo y
    /// que «Todo» es la suma de lo cargado, no un numero de otro sitio.
    /// </remarks>
    [TestMethod]
    public void LosSeisTablerosCuentanLoSuyoYTodoEsElDenominadorDeLoSinArchivar()
    {
        var banco = new BaseDePrueba();
        banco.MeterUnCasoDeCadaEstado();
        banco.Tablero.Cargar();

        var cuentas = banco.Tablero.Cuentas();

        Assert.AreEqual(6, cuentas[FiltroDeTarjeta.Todo], "Seis sin archivar; el archivado no cuenta aqui.");
        Assert.AreEqual(banco.Tablero.Total, cuentas[FiltroDeTarjeta.Todo], "«Todo» es exactamente lo cargado.");
        Assert.AreEqual(4, cuentas[FiltroDeTarjeta.SinRevisar], "Seis menos el completa y el no completa.");
        Assert.AreEqual(1, cuentas[FiltroDeTarjeta.Completadas]);
        Assert.AreEqual(1, cuentas[FiltroDeTarjeta.Incompletas]);
        Assert.AreEqual(6, cuentas[FiltroDeTarjeta.SinAsignar], "Nadie lleva nada todavia.");
        Assert.AreEqual(1, cuentas[FiltroDeTarjeta.FechaPasada]);
    }

    /// <summary>
    /// La fecha ya pasada se marca en su tarjeta. Lo que el dueno pidio: «esta fecha ya
    /// paso, revisar, ¿quieres archivar o completar?» — se avisa, no se impide.
    /// </summary>
    [TestMethod]
    public void LaFechaYaPasadaSeMarcaEnSuTarjetaYNoDetieneNada()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();
        banco.Tablero.Cargar();

        var vencido = banco.Tablero.De(ids[5])!;
        var futuro = banco.Tablero.De(ids[0])!;

        Assert.IsTrue(vencido.FechaYaPasada);
        Assert.IsFalse(futuro.FechaYaPasada);
        Assert.IsFalse(banco.Tablero.De(ids[4])!.FechaYaPasada, "Sin fecha no es fecha pasada: es otra cosa.");
    }

    /// <summary>Las dos salidas de una fecha pasada funcionan: archivar y completar.</summary>
    [TestMethod]
    public void LaFechaPasadaTieneSusDosSalidasYLasDosEscriben()
    {
        var banco = new BaseDePrueba();
        var vencidoA = banco.Meter("BBBB0001", banco.FechaEn(-5), null, false);
        var vencidoB = banco.Meter("BBBB0002", banco.FechaEn(-6), null, false);
        var quien = banco.Activos[0];

        banco.Acciones.ArchivarEnLote([vencidoA]);
        banco.Acciones.MarcarAMano(vencidoB, EstadoDeRecomendacion.Completa, quien.Id);
        banco.Tablero.Cargar();

        // ⛔ Antes se comprobaba el archivado leyendo su tarjeta del tablero. Ya no sale ahi, y
        // eso es justo la salida que se pidio: se comprueba en la BASE, que es donde el caso
        // sigue estando, y en el tablero se comprueba que dejo de estorbar.
        var archivado = banco.Servicios.Casos.Obtener(vencidoA)!;
        Assert.IsTrue(archivado.Archivado, "Archivar deja el caso archivado.");
        Assert.AreEqual(banco.Reloj.Hoy(), archivado.FechaArchivado, "Con su fecha.");
        Assert.IsNull(banco.Tablero.De(vencidoA), "Y fuera de la vista de trabajo, que es la otra mitad de archivar.");
        Assert.AreEqual(EstadoDeRecomendacion.Completa, banco.Tablero.De(vencidoB)!.Estado);
        StringAssert.Contains(banco.Tablero.De(vencidoB)!.Firma, quien.Nombre, "Y con la firma de quien lo marco.");
    }

    /// <summary>
    /// «Del tablero de completados es que yo puedo seleccionar completo y archivarlo»:
    /// seleccionar varios y archivar los archiva TODOS.
    /// </summary>
    [TestMethod]
    public void SeleccionarVariosDelTableroDeCompletadosLosArchivaTodos()
    {
        var banco = new BaseDePrueba();
        var quien = banco.Activos[0];
        var completados = new List<long>();
        for (var i = 0; i < 12; i++)
        {
            var id = banco.Meter($"CCCC{i:D4}", banco.FechaEn(10 + i), null, false);
            banco.Servicios.Casos.MarcarEstado(id, EstadoDeRecomendacion.Completa, quien.Id, "hoja devuelta");
            completados.Add(id);
        }
        banco.Tablero.Cargar();

        // Lo que deja Ctrl+A sobre el tablero de completados: los que se estan viendo, y
        // solo esos. Es lo mismo que le pasa la pantalla a ArchivarEnLote.
        var marcados = banco.Tablero.Todas(FiltroDeTarjeta.Completadas).Select(t => t.CasoId).ToList();
        var resumen = banco.Acciones.ArchivarEnLote(marcados);
        banco.Tablero.Cargar();

        Assert.HasCount(12, marcados, "Ctrl+A marca los doce que se ven.");
        Assert.AreEqual(12, resumen.Hechos);
        Assert.AreEqual(0, resumen.NoSePudieron);
        Assert.AreEqual(
            12,
            completados.Count(id => banco.Servicios.Casos.Obtener(id)!.Archivado),
            "Los doce quedan archivados en la base.");
        Assert.AreEqual(0, banco.Tablero.Total, "Y los doce salen de la vista de golpe, que es para lo que se archiva.");
    }

    /// <summary>Archivar no borra: el caso sigue estando y sigue contando (criterio C8-3).</summary>
    [TestMethod]
    public void ArchivarNoBorraElCasoSigueEstando()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();
        var antes = banco.Servicios.Casos.Contar(new FiltroDeCasos(IncluirArchivados: true));

        banco.Acciones.ArchivarEnLote(ids.ToList());

        Assert.AreEqual(antes, banco.Servicios.Casos.Contar(new FiltroDeCasos(IncluirArchivados: true)));
        Assert.AreEqual(banco.Reloj.Hoy(), banco.Servicios.Casos.Obtener(ids[0])!.FechaArchivado);
    }

    /// <summary>
    /// Ctrl+A dos veces desmarca: es lo que espera quien lo pulsa por error. Lo unico que
    /// WinUI no decide de Ctrl+A es esto, y por eso es lo unico que hay escrito.
    /// </summary>
    [TestMethod]
    public void CtrlAdosVecesDesmarcaLoQueMarco()
    {
        Assert.IsTrue(MarcarTodo.HayQueMarcar(0, 12), "Con nada marcado, la primera marca los doce.");
        Assert.IsTrue(MarcarTodo.HayQueMarcar(5, 12), "Con algunos marcados, tambien marca.");
        Assert.IsFalse(MarcarTodo.HayQueMarcar(12, 12), "Con todos marcados, la segunda desmarca.");
    }

    /// <summary>Ctrl+A marca lo que se VE, no la base entera: archivar a ciegas no pasa.</summary>
    [TestMethod]
    public void CtrlAMarcaSoloLoQueSeEstaViendo()
    {
        var banco = new BaseDePrueba();
        banco.MeterUnCasoDeCadaEstado();
        banco.Tablero.Cargar();

        // Lo que Ctrl+A puede llegar a marcar es lo que el tablero esta ensenando.
        var aLaVista = banco.Tablero.Todas(FiltroDeTarjeta.Completadas);

        Assert.HasCount(1, aLaVista, "Solo el tablero que se mira, no los seis cargados.");
        Assert.AreEqual(6, banco.Tablero.Total, "Y cargados siguen estando los seis sin archivar.");
    }

    /// <summary>
    /// Asignar desde una tarjeta de Revisar cambia la asignacion DE VERDAD. Es lo que hoy
    /// no hace el programa viejo: «todavia no esta conectado, se asigna en la pantalla
    /// Asignar casos».
    /// </summary>
    [TestMethod]
    public void AsignarDesdeUnaTarjetaCambiaLaAsignacionDeVerdad()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();
        var destino = banco.Activos[1];
        banco.Tablero.Cargar();

        var antes = banco.Tablero.De(ids[0])!;
        banco.Operacion.Asignar(ids[0], destino.Id);
        banco.Tablero.Cargar();
        var despues = banco.Tablero.De(ids[0])!;

        Assert.IsTrue(antes.SinAsignar, "Antes no lo llevaba nadie.");
        Assert.AreEqual("sin asignar", antes.AsignadoA);
        Assert.IsFalse(despues.SinAsignar, "Despues si.");
        Assert.AreEqual(destino.Nombre, despues.AsignadoA, "Y la tarjeta dice su nombre.");
        Assert.HasCount(1, banco.Servicios.Asignaciones.VivasDeCaso(ids[0]), "Con su fila en la base.");
    }

    /// <summary>Quitar el companero desde la tarjeta retira la asignacion sin borrarla.</summary>
    [TestMethod]
    public void QuitarElCompaneroDesdeLaTarjetaRetiraSinBorrar()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();
        banco.Operacion.Asignar(ids[0], banco.Activos[0].Id);

        banco.Operacion.RetirarDelCaso(ids[0]);
        banco.Tablero.Cargar();

        Assert.IsTrue(banco.Tablero.De(ids[0])!.SinAsignar);
        var todas = banco.Servicios.Asignaciones
            .Listar(new FiltroDeAsignaciones(CasoId: ids[0], SoloActivas: false), Pagina.Primera(10));
        Assert.AreEqual(1, todas.TotalDisponible, "La fila retirada sigue guardada.");
    }

    /// <summary>El buscador de Revisar reduce el tablero y las cuentas se recalculan con el.</summary>
    [TestMethod]
    public void ElBuscadorReduceElTableroYSusCuentas()
    {
        var banco = new BaseDePrueba();
        banco.MeterUnCasoDeCadaEstado();

        banco.Tablero.Cargar("AAAA0003");

        Assert.AreEqual(1, banco.Tablero.Total);
        Assert.AreEqual(1, banco.Tablero.Cuentas()[FiltroDeTarjeta.Completadas]);
        Assert.AreEqual(0, banco.Tablero.Cuentas()[FiltroDeTarjeta.SinRevisar]);
    }

    /// <summary>Cada linea de la tarjeta cabe en un renglon: ni un parrafo (requisito 4).</summary>
    [TestMethod]
    public void CadaLineaDeLaTarjetaCabeEnUnRenglon()
    {
        var banco = new BaseDePrueba();
        banco.MeterUnCasoDeCadaEstado();
        banco.Tablero.Cargar();

        foreach (var tarjeta in banco.Tablero.Todas(FiltroDeTarjeta.Todo))
        {
            Assert.DoesNotContain("\n", tarjeta.Datos, "Los datos van en una linea.");
            Assert.DoesNotContain("\n", tarjeta.PieDeLaTarjeta, "El pie tambien.");
            Assert.IsLessThan(120, tarjeta.Datos.Length, $"«{tarjeta.Datos}» no cabe en la tarjeta.");
        }
    }

    /// <summary>
    /// Cuando se piden los archivados, cada uno dice CUANDO se archivo.
    /// </summary>
    /// <remarks>
    /// La fecha es lo que hace util la vista de archivados: sirve para decidir cual desarchivar
    /// y cual no. Se pide con la casilla puesta porque en la vista de trabajo ya no salen.
    /// </remarks>
    [TestMethod]
    public void ElArchivadoDiceCuandoSeArchivo()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();
        banco.Tablero.Cargar(conArchivados: true);

        StringAssert.Contains(banco.Tablero.De(ids[6])!.PieDeLaTarjeta, "archivado el ");
    }

    /// <summary>Quien firma a mano sale del equipo, y si el equipo esta vacio no se inventa.</summary>
    [TestMethod]
    public void QuienFirmaAManoSaleDelEquipoYNoSeInventa()
    {
        var banco = new BaseDePrueba();

        var conEquipo = AccionesDeRevisar.QuienFirmaAMano(banco.Activos);
        var sinEquipo = AccionesDeRevisar.QuienFirmaAMano([]);

        Assert.IsNotNull(conEquipo);
        Assert.AreEqual("Miguel", conEquipo.Nombre, "Con Miguel en el equipo, firma Miguel.");
        Assert.IsNull(sinEquipo, "Sin nadie activo no se firma: no se inventa una firma.");
    }
}
