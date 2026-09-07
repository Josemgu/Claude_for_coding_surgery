using Fichas.App.Asignar;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Asignar;

/// <summary>
/// Las pruebas de la pantalla de Asignar, SIN ventana. Cada una nace de un criterio.
/// </summary>
/// <remarks>
/// Se escribieron desde el criterio de aceptacion de la FASE C5 y desde las palabras del
/// dueno, no desde el codigo: una prueba escrita mirando el codigo que acaba de salir solo
/// confirma lo que su autor entendio.
/// </remarks>
[TestClass]
public sealed class PruebasDeAsignar
{
    /// <summary>
    /// Criterio C5-4. Un caso en cada estado, y la pantalla los ofrece todos MENOS el
    /// archivado. Cero casos ocultos por su estado, con el denominador dicho.
    /// </summary>
    /// <remarks>
    /// Los siete siguen entrando en la base y el archivado sigue estando: lo que cambia es que
    /// no se ofrece para dar trabajo. Del dueno, 2026-09-05: «cuando yo archive, debe salir del
    /// sistema visible pero se queda como historico para los reportes».
    /// </remarks>
    [TestMethod]
    public void LaPantallaOfreceTodosLosCasosSeaCualSeaSuEstadoMenosElArchivado()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();

        var sinArchivar = banco.Lista.CuantosSePuedenOfrecer();
        var archivados = banco.Lista.CuantosHayArchivados();
        var ofrecidos = banco.Lista.CuantosSeOfrecen();
        var pagina = banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue));

        Assert.HasCount(7, ids, "La base de prueba tiene que traer los siete estados.");
        Assert.AreEqual(7, sinArchivar + archivados, "En la base siguen estando los siete: archivar no borra.");
        Assert.AreEqual(1, archivados, "Y uno de ellos esta archivado.");
        Assert.AreEqual(sinArchivar, ofrecidos, "Todo caso sin archivar se ofrece: N sin archivar, N ofrecidos.");
        Assert.AreEqual(sinArchivar, pagina.TotalDisponible, "El total de la pagina es el mismo denominador.");
        CollectionAssert.AreEquivalent(
            ids.Take(6).ToList(),
            pagina.Elementos.Select(r => r.CasoId).ToList(),
            "No falta ninguno de los seis vivos —ni el verificado ni el completo— y no sobra el archivado.");
    }

    /// <summary>
    /// Criterio C5-4, la parte que mas duele: el ya completo se ofrece igual; el archivado no.
    /// </summary>
    /// <remarks>
    /// Son los dos lados del mismo defecto medido el 2026-09-05. Esconder un caso por su
    /// ESTADO era el defecto del programa viejo y sigue prohibido; seguir ofreciendo uno
    /// ARCHIVADO era lo que dejaba dar trabajo sobre algo ya cerrado.
    /// </remarks>
    [TestMethod]
    public void ElYaCompletoSeSigueOfreciendoYElArchivadoNo()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();
        var ofrecidos = banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos;

        var completo = ofrecidos.Single(r => r.CasoId == ids[2]);

        Assert.AreEqual("completa", completo.PalabraDelEstado, "Un caso ya completo se sigue pudiendo asignar.");
        Assert.IsEmpty(
            ofrecidos.Where(r => r.CasoId == ids[6]).ToList(),
            "El archivado NO se ofrece: no se le da trabajo a nadie sobre algo ya cerrado.");
        Assert.IsEmpty(
            ofrecidos.Where(r => r.Detalle.Contains("archivado", StringComparison.OrdinalIgnoreCase)).ToList(),
            "Ni uno solo de los ofrecidos dice la palabra: desde el 2026-09-06 no hay marca que decir.");
        Assert.IsNotNull(banco.Servicios.Casos.Obtener(ids[6]), "Pero sigue en la base: archivar no borra.");
    }

    /// <summary>
    /// Criterio C5-5. Sin companero elegido la lista NO sale vacia. Es el defecto medido en
    /// <c>interfaz/asignacion.py::_casos_disponibles</c>, que devuelve <c>[]</c> sin companero.
    /// </summary>
    [TestMethod]
    public void SinCompaneroElegidoLaListaNoSaleVacia()
    {
        var banco = new BaseDePrueba();
        banco.MeterUnCasoDeCadaEstado();

        // No se elige ningun companero: no hay donde elegirlo. La lista de casos no
        // depende del destino, y por eso este defecto no se puede repetir.
        var ofrecidos = banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue));

        Assert.HasCount(6, ofrecidos.Elementos, "Sin companero elegido se ofrecen los seis sin archivar.");
    }

    /// <summary>
    /// Criterio C5-4, dicho en estructura: el filtro de esta pantalla no lleva NINGUN estado,
    /// y lo unico que deja fuera son los archivados.
    /// </summary>
    /// <remarks>
    /// Se comprueba campo por campo, que es la unica forma de que no se cuele uno manana.
    /// <c>IncluirArchivados</c> en falso es el UNICO campo que quita casos, y quita por el eje
    /// del archivado, no por el del estado de la recomendacion: son dos ejes distintos.
    /// </remarks>
    [TestMethod]
    public void ElFiltroDeLaPantallaSoloDejaFueraLosArchivados()
    {
        var filtro = ListaParaAsignar.SinFiltroDeEstadoNiArchivados;

        Assert.IsFalse(filtro.IncluirArchivados, "Lo unico que quita casos es el archivado, que el dueno saco de la vista.");
        Assert.IsFalse(filtro.SoloDeHoy);
        Assert.IsNull(filtro.VentanaDeDias);
        Assert.IsNull(filtro.CompaneroId);
        Assert.IsNull(filtro.Texto);
        Assert.IsFalse(filtro.SoloVencidos);
        Assert.IsNull(filtro.Estado, "Un estado aqui seria un caso escondido: es el defecto del programa viejo.");
    }

    /// <summary>
    /// Los archivados se cuentan para poder decirlos, no se esconden callando.
    /// </summary>
    /// <remarks>
    /// Es la misma regla que con los companeros desactivados. Sin la cifra, una lista que
    /// encoge no se distingue de una que perdio casos.
    /// </remarks>
    [TestMethod]
    public void LosArchivadosSeCuentanEnVezDeEsconderse()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();

        var antes = banco.Lista.CuantosHayArchivados();
        banco.Acciones.ArchivarEnLote([ids[0], ids[1]]);

        Assert.AreEqual(1, antes, "De los siete, uno entro archivado.");
        Assert.AreEqual(3, banco.Lista.CuantosHayArchivados(), "Archivar dos mas los suma a la cifra que se dice.");
        Assert.AreEqual(4, banco.Lista.CuantosSePuedenOfrecer(), "Y quedan cuatro que se pueden dar.");
    }

    /// <summary>
    /// Criterio C5-6. Lo unico que sigue filtrando es el companero desactivado: con tres
    /// companeros, desactivar uno deja dos destinos y los casos siguen siendo todos.
    /// </summary>
    [TestMethod]
    public void DesactivarUnCompaneroQuitaUnDestinoYNiUnCaso()
    {
        var banco = new BaseDePrueba();
        banco.MeterUnCasoDeCadaEstado();
        var antes = banco.Lista.Destinos().Count;
        var casosAntes = banco.Lista.CuantosSeOfrecen();

        banco.Servicios.Companeros.Desactivar(banco.Activos[0].Id, banco.Reloj.Ahora());

        Assert.HasCount(antes - 1, banco.Lista.Destinos(), "Un destino menos.");
        Assert.AreEqual(casosAntes, banco.Lista.CuantosSeOfrecen(), "Y ni un caso menos.");
    }

    /// <summary>
    /// Criterio C5-6, la unica condicion que queda: no se asigna a un desactivado. Y se
    /// AVISA, no se lanza (requisito 9: avisar, nunca impedir).
    /// </summary>
    [TestMethod]
    public void AUnCompaneroDesactivadoNoSeLeAsignaYSeDiceEnUnaLinea()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();
        var victima = banco.Activos[0];
        banco.Servicios.Companeros.Desactivar(victima.Id, banco.Reloj.Ahora());
        banco.Avisos.CerrarTodos();

        var resultado = banco.Operacion.Asignar(ids[0], victima.Id);

        Assert.IsFalse(resultado.SeEscribio, "No se le asigna a un desactivado.");
        Assert.HasCount(1, banco.Avisos.Pendientes, "Se dice en la franja, no con un cuadro.");
        StringAssert.Contains(banco.Avisos.Pendientes[0].Linea, victima.Nombre);
    }

    /// <summary>
    /// Criterio C5-2. La misma operacion llamada desde los TRES sitios —la lista de
    /// Asignar, la tarjeta de Revisar y la correccion— deja la fila IDENTICA.
    /// </summary>
    [TestMethod]
    public void AsignarDesdeLosTresSitiosDejaLaMismaFila()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();
        var destino = banco.Activos[0];
        banco.Tablero.Cargar();

        // 1 · desde la LISTA de Asignar, que asigna en lote por la misma puerta.
        banco.Operacion.AsignarVarios([ids[0]], destino.Id, destino.Nombre);
        // 2 · desde la TARJETA de Revisar; hoy el programa viejo dice aqui «todavia no
        //     esta conectado». Es la misma llamada, no una parecida.
        banco.Operacion.Asignar(ids[1], destino.Id);
        // 3 · desde la CORRECCION, que llama a la misma operacion sin pantalla de por medio.
        banco.Operacion.Asignar(ids[2], destino.Id);

        var filas = ids.Take(3)
            .Select(id => banco.Servicios.Asignaciones.VivasDeCaso(id).Single())
            .ToList();

        foreach (var fila in filas)
        {
            Assert.AreEqual(destino.Id, fila.CompaneroId);
            Assert.AreEqual(banco.Reloj.Ahora(), fila.AsignadoEn, "La marca de tiempo sale del mismo reloj.");
            Assert.IsTrue(fila.Activa);
            Assert.IsNull(fila.DesactivadaEn);
        }

        // La comparacion de verdad: las tres filas son iguales salvo su id y su caso.
        var normalizadas = filas.Select(f => f with { Id = 0, CasoId = 0 }).Distinct().ToList();
        Assert.HasCount(1, normalizadas, "Las tres filas salen identicas: es UNA sola operacion.");
    }

    /// <summary>Criterio C5-3. Retirar desactiva y deja la fila: el historial no se pierde.</summary>
    [TestMethod]
    public void RetirarUnCasoDesactivaLaFilaYNoLaBorra()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();
        var destino = banco.Activos[0];
        banco.Operacion.Asignar(ids[0], destino.Id);

        banco.Operacion.RetirarDelCaso(ids[0]);

        Assert.IsEmpty(banco.Servicios.Asignaciones.VivasDeCaso(ids[0]), "Ya no la lleva nadie.");
        var todas = banco.Servicios.Asignaciones
            .Listar(new FiltroDeAsignaciones(CasoId: ids[0], SoloActivas: false), Pagina.Primera(50));
        Assert.AreEqual(1, todas.TotalDisponible, "La fila sigue ahi: quien lo llevo no se borra.");
        Assert.IsFalse(todas.Elementos[0].Activa);
        Assert.AreEqual(banco.Reloj.Ahora(), todas.Elementos[0].DesactivadaEn, "Con su fecha de retirada.");
    }

    /// <summary>Asignar dos veces al mismo companero no duplica la fila; avisa y sigue.</summary>
    [TestMethod]
    public void AsignarDosVecesAlMismoNoDuplicaLaFila()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();
        var destino = banco.Activos[0];

        banco.Operacion.Asignar(ids[0], destino.Id);
        var segunda = banco.Operacion.Asignar(ids[0], destino.Id);

        Assert.IsTrue(segunda.SeEscribio, "No es un error: es pulsar dos veces.");
        Assert.HasCount(1, banco.Servicios.Asignaciones.VivasDeCaso(ids[0]));
    }

    /// <summary>El lote dice en una linea cuantos entraron y a quien; nada de cuadros.</summary>
    [TestMethod]
    public void ElLoteDiceEnUnaLineaCuantosEntraronYAQuien()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();
        var destino = banco.Activos[0];

        var resumen = banco.Operacion.AsignarVarios(ids.Take(4).ToList(), destino.Id, destino.Nombre);

        Assert.AreEqual(4, resumen.Asignados);
        Assert.AreEqual(0, resumen.NoSePudieron);
        StringAssert.Contains(resumen.Linea, destino.Nombre);
        Assert.HasCount(1, resumen.Linea.Split('\n'), "El acuse es UNA linea (requisito 4).");
    }

    /// <summary>El buscador reduce lo ofrecido sin esconder nada por estado.</summary>
    /// <remarks>
    /// Buscar el archivado por su numero tampoco lo saca: el buscador acota lo que se ofrece,
    /// no lo amplia. Verlo y desarchivarlo se hace en Revisar, con «Ver los archivados».
    /// </remarks>
    [TestMethod]
    public void ElBuscadorReduceLoOfrecidoYLoDiceConSuDenominador()
    {
        var banco = new BaseDePrueba();
        banco.MeterUnCasoDeCadaEstado();

        var sinArchivar = banco.Lista.CuantosSePuedenOfrecer();
        var alCompleto = banco.Lista.CuantosSeOfrecen("AAAA0003");
        var alArchivado = banco.Lista.CuantosSeOfrecen("AAAA0007");

        Assert.AreEqual(6, sinArchivar);
        Assert.AreEqual(1, alCompleto, "Un caso ya completo casa con la busqueda: el estado no lo esconde.");
        Assert.AreEqual(0, alArchivado, "El archivado no sale ni buscandolo por su numero.");
    }

    /// <summary>Un caso sin numero de caso se ofrece igual, y se dice que no lo tiene.</summary>
    [TestMethod]
    public void UnCasoSinNumeroSeOfreceYNoSeLeInventaUno()
    {
        var banco = new BaseDePrueba();
        var id = banco.Meter(null, null, null, false);

        var renglon = banco.Lista.Ofrecer(Pagina.Primera(10)).Elementos.Single(r => r.CasoId == id);

        Assert.IsFalse(renglon.TieneNumeroDeCaso);
        Assert.AreEqual(RenglonParaAsignar.SinNumero, renglon.NumeroDeCaso);
    }

    /// <summary>Los desactivados no son destino, pero se cuentan para poder decirlo.</summary>
    [TestMethod]
    public void LosCompanerosDesactivadosSeCuentanEnVezDeEsconderse()
    {
        var banco = new BaseDePrueba();

        var activos = banco.Lista.Destinos().Count;
        var desactivados = banco.Lista.CuantosDestinosDesactivados();

        Assert.AreEqual(6, activos, "El generador da 8 companeros y desactiva los dos ultimos.");
        Assert.AreEqual(2, desactivados);
    }

    /// <summary>El renglon no lleva ni un parrafo: dos lineas y ya (requisito 4).</summary>
    [TestMethod]
    public void ElRenglonCabeEnDosLineas()
    {
        var banco = new BaseDePrueba();
        banco.MeterUnCasoDeCadaEstado();

        foreach (var renglon in banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos)
        {
            Assert.DoesNotContain("\n", renglon.Titulo, "El titulo es una linea.");
            Assert.DoesNotContain("\n", renglon.Detalle, "El detalle es una linea.");
        }
    }
}
