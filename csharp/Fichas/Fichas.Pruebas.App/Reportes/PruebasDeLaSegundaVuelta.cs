using Fichas.App.Paquetes;
using Fichas.App.Reportes;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;
using Fichas.Contratos.Consultas;
using Fichas.Reportes;
using Fichas.Reportes.Armado;

namespace Fichas.Pruebas.App.Reportes;

/// <summary>
/// La escalera: que caso sube al peldano siguiente y cual no.
/// </summary>
/// <remarks>
/// <para><b>De donde sale el criterio.</b> De las palabras del dueno del 2026-09-05
/// (<c>DECISIONES.md</c>): <i>«los agentes categoría 1 no pudieron comunicarse con los
/// líderes, debo pasarlo a los agentes de categoría 2; si los de categoría 2 no pudieron, a
/// los de categoría 3»</i>. Y de la regla de entrada escrita en <c>PENDIENTES.md</c>, criterio
/// C15-2: entra el caso que <b>(a)</b> volvió con <c>no_completa</c> <b>y (b)</b> tiene
/// <c>motivo_del_companero</c> igual a «no se pudo comunicar» o «el líder no lo hizo». Un caso
/// <c>completa</c> no entra nunca, tenga el comentario que tenga.</para>
///
/// <para>⚠️ <b>«Otra razón» NO entra, y esa es una decisión del DUEÑO que sigue abierta.</b> El
/// ADR-0005 §5.4 la recomienda «sí, pero listada aparte», y aquí se implementa lo que dice el
/// criterio C15-2, que solo nombra los otros dos motivos. Para que la recomendación del ADR
/// pueda cumplirse sin cambiar la regla, la selección DEVUELVE esos casos en una lista aparte
/// y los cuenta: no entran en el paquete y no se pierden de vista.</para>
///
/// <para>El escenario está escrito a mano, caso por caso, y no sale del generador: cada uno
/// existe para probar UNA rama, y con datos inventados al azar no se puede decir cuántos
/// tenían que entrar.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaSegundaVuelta
{
    private const string Hoy = "2026-09-05";

    /// <summary>Los dos motivos que el dueno nombro suben al peldano siguiente.</summary>
    [TestMethod]
    public void SubenLosDosMotivosQueElDuenoNombro()
    {
        var mundo = Escenario();

        var seleccion = SegundaVuelta.Para(
            mundo.Servicios.Casos, mundo.Servicios.Asignaciones, mundo.Servicios.Companeros,
            mundo.DeCategoriaDos);

        CollectionAssert.AreEquivalent(
            new[] { mundo.NoSePudoComunicar, mundo.ElLiderNoLoHizo },
            seleccion.CasoIds.ToArray(),
            "A la categoría 2 suben exactamente los dos motivos del criterio C15-2, y ningún otro. "
            + $"Subieron: {string.Join(", ", seleccion.CasoIds)}");
    }

    /// <summary>«Otra razon» se queda fuera del paquete, pero se cuenta y se ve.</summary>
    [TestMethod]
    public void OtraRazonNoEntraEnElPaqueteYSeListaAparte()
    {
        var mundo = Escenario();

        var seleccion = SegundaVuelta.Para(
            mundo.Servicios.Casos, mundo.Servicios.Asignaciones, mundo.Servicios.Companeros,
            mundo.DeCategoriaDos);

        CollectionAssert.DoesNotContain(seleccion.CasoIds.ToArray(), mundo.OtraRazon);
        CollectionAssert.AreEquivalent(
            new[] { mundo.OtraRazon },
            seleccion.ConOtraRazon.Select(c => c.CasoId).ToArray());
    }

    /// <summary>Un caso completa no sube nunca, aunque su companero dejara un motivo escrito.</summary>
    /// <remarks>
    /// Es la mitad del criterio C15-2 que se olvida: la regla tiene DOS condiciones y esta
    /// prueba existe para que quitar la primera se note.
    /// </remarks>
    [TestMethod]
    public void UnCasoCompletoNoSubeAunqueTengaMotivoEscrito()
    {
        var mundo = Escenario();

        var seleccion = SegundaVuelta.Para(
            mundo.Servicios.Casos, mundo.Servicios.Asignaciones, mundo.Servicios.Companeros,
            mundo.DeCategoriaDos);

        CollectionAssert.DoesNotContain(seleccion.CasoIds.ToArray(), mundo.CompletaConMotivo);
    }

    /// <summary>Un caso sin motivo escrito no sube: nadie ha dicho por que.</summary>
    [TestMethod]
    public void UnCasoSinMotivoEscritoNoSubeYSeCuenta()
    {
        var mundo = Escenario();

        var seleccion = SegundaVuelta.Para(
            mundo.Servicios.Casos, mundo.Servicios.Asignaciones, mundo.Servicios.Companeros,
            mundo.DeCategoriaDos);

        CollectionAssert.DoesNotContain(seleccion.CasoIds.ToArray(), mundo.SinMotivo);
        Assert.AreEqual(1, seleccion.SinMotivoEscrito);
    }

    /// <summary>Un caso archivado no sube: esta cerrado.</summary>
    /// <remarks>
    /// Regla del dueno del 2026-09-05: <i>«cuando yo archive, debe salir del sistema visible,
    /// pero se queda como histórico para los reportes»</i>. Un paquete es sistema visible.
    /// </remarks>
    [TestMethod]
    public void UnCasoArchivadoNoSube()
    {
        var mundo = Escenario();

        var seleccion = SegundaVuelta.Para(
            mundo.Servicios.Casos, mundo.Servicios.Asignaciones, mundo.Servicios.Companeros,
            mundo.DeCategoriaDos);

        CollectionAssert.DoesNotContain(seleccion.CasoIds.ToArray(), mundo.ArchivadoConMotivo);
    }

    /// <summary>
    /// Lo que se quedo trabado en la categoria 2 NO entra en el paquete de la categoria 2.
    /// </summary>
    /// <remarks>
    /// Es la propiedad que hace que la escalera sea una escalera y no un bucle: un caso sube al
    /// peldano SIGUIENTE al de quien lo intento. Sin esto, el paquete de categoria 2 le
    /// devolveria al gerente el trabajo que el mismo acaba de no conseguir.
    /// </remarks>
    [TestMethod]
    public void LoTrabadoEnLaCategoriaDosSubeALaTresYNoSeQuedaEnLaDos()
    {
        var mundo = Escenario();

        var paraLaDos = SegundaVuelta.Para(
            mundo.Servicios.Casos, mundo.Servicios.Asignaciones, mundo.Servicios.Companeros,
            mundo.DeCategoriaDos);
        var paraLaTres = SegundaVuelta.Para(
            mundo.Servicios.Casos, mundo.Servicios.Asignaciones, mundo.Servicios.Companeros,
            mundo.DeCategoriaTres);

        CollectionAssert.DoesNotContain(paraLaDos.CasoIds.ToArray(), mundo.TrabadoEnLaDos);
        CollectionAssert.Contains(paraLaTres.CasoIds.ToArray(), mundo.TrabadoEnLaDos);
    }

    /// <summary>La linea dice cuantos entran de cuantos, que es lo que pide el criterio.</summary>
    [TestMethod]
    public void LaLineaDiceCuantosEntranDeCuantos()
    {
        var mundo = Escenario();

        var seleccion = SegundaVuelta.Para(
            mundo.Servicios.Casos, mundo.Servicios.Asignaciones, mundo.Servicios.Companeros,
            mundo.DeCategoriaDos);

        Assert.HasCount(2, seleccion.CasoIds);
        Assert.AreEqual(5, seleccion.NoCompletosMirados);
        StringAssert.Contains(seleccion.Linea, "2 de 5");
        StringAssert.Contains(seleccion.Linea, "categoría 2");
        Console.WriteLine($"MEDIDO · {seleccion.Linea}");
    }

    /// <summary>Un companero de categoria 1 no recibe segunda vuelta: no hay peldano debajo.</summary>
    [TestMethod]
    public void LaCategoriaUnoNoTieneSegundaVueltaQueRecibir()
    {
        var mundo = Escenario();

        var seleccion = SegundaVuelta.Para(
            mundo.Servicios.Casos, mundo.Servicios.Asignaciones, mundo.Servicios.Companeros,
            mundo.DeCategoriaUno);

        Assert.HasCount(0, seleccion.CasoIds);
        StringAssert.Contains(seleccion.Linea, "categoría 1");
    }

    /// <summary>Quien lo intento viaja con el caso: nombre y peldano.</summary>
    /// <remarks>
    /// Del dueno, en otro contexto y vale aqui: nunca se pierde el rastro de quien hizo que. Es
    /// ademas lo que el reporte del gerente necesita para decir «lo intentó Sandy y no pudo».
    /// </remarks>
    [TestMethod]
    public void CadaCasoQueSubeDiceQuienLoIntentoYEnQuePeldano()
    {
        var mundo = Escenario();

        var seleccion = SegundaVuelta.Para(
            mundo.Servicios.Casos, mundo.Servicios.Asignaciones, mundo.Servicios.Companeros,
            mundo.DeCategoriaDos);

        var uno = seleccion.Entran.Single(c => c.CasoId == mundo.NoSePudoComunicar);
        Assert.AreEqual("Sandy", uno.QuienLoIntento);
        Assert.AreEqual(1, uno.CategoriaDeQuienLoIntento);
        Assert.AreEqual(MotivoDeNoCompletar.NoSePudoComunicar, uno.Motivo);
    }

    // ---- el paquete entero, con su Excel y su reporte -----------------------

    /// <summary>El paquete escribe el Excel y, a su lado, el reporte con el mismo nombre.</summary>
    /// <remarks>
    /// Los dos archivos hermanos: es lo que el dueno pidio para la primera vuelta —«el Excel
    /// normal y también un PDF»— y lo mismo vale aqui, con el reporte en vez del PDF unido.
    /// </remarks>
    [TestMethod]
    public void ElPaqueteDejaElExcelYElReporteJuntos()
    {
        var mundo = Escenario();
        var carpeta = Path.Combine(Path.GetTempPath(), "fichas-pruebas-segunda-vuelta", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, "Segunda vuelta de Yudelka.xlsx");

        try
        {
            var resumen = Operacion(mundo).Generar(mundo.DeCategoriaDos, ruta);

            StringAssert.Contains(resumen.Linea, "2 de 5");
            StringAssert.Contains(resumen.Detalle, "NO asigna los documentos");
            Assert.IsTrue(resumen.SalioBien, resumen.Detalle);

            // Los dos archivos hermanos: el Excel donde se pidio y el reporte a su lado.
            Assert.IsTrue(File.Exists(ruta), "No se escribió el Excel.");
            var rutaDelReporte = Path.ChangeExtension(ruta, ".pdf");
            Assert.IsTrue(File.Exists(rutaDelReporte), $"No se escribió el reporte en «{rutaDelReporte}».");

            Console.WriteLine(
                $"MEDIDO · {resumen.Linea} · Excel {new FileInfo(ruta).Length} bytes · "
                + $"reporte {new FileInfo(rutaDelReporte).Length} bytes en {rutaDelReporte}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(carpeta)) Directory.Delete(carpeta, recursive: true);
            }
            catch (Exception causa) when (causa is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"No se pudo borrar «{carpeta}»: {causa.Message}");
            }
        }
    }

    /// <summary>Sin nada que subir NO se escribe ningun archivo, y se dice cuantos se miraron.</summary>
    [TestMethod]
    public void SinNadaQueSubirNoSeEscribeNadaYSeDiceElDenominador()
    {
        var mundo = Escenario();
        var ruta = Path.Combine(Path.GetTempPath(), "fichas-pruebas-segunda-vuelta", "no-deberia-existir.xlsx");

        var resumen = Operacion(mundo).Generar(mundo.DeCategoriaUno, ruta);

        Assert.IsFalse(resumen.SalioBien);
        Assert.IsNull(resumen.Ruta);
        Assert.IsFalse(File.Exists(ruta));
        StringAssert.Contains(resumen.Linea, "0 de 5");
    }

    /// <summary>
    /// La operacion con un <c>IPaquetes</c> que SI escribe y el motor de reportes de verdad.
    /// </summary>
    /// <remarks>
    /// ⚠️ <c>ServiciosFalsos.Paquetes</c> y <c>ServiciosFalsos.Reportes</c> dicen que
    /// escribieron y NO tocan el disco: es lo que permite abrir la pantalla con «--falso» sin
    /// base. Con ellos, esta prueba comprobaria el camino de «el archivo no está» y no el de
    /// generar. Aqui se pone el doble que escribe de verdad y el motor de PDF de verdad, que es
    /// lo unico que deja mirar los dos archivos hermanos.
    /// </remarks>
    private static OperacionDeLaSegundaVuelta Operacion(Mundo mundo)
        => new(
            new PaquetesDeMentirijilla(
                mundo.Servicios.Ilegibles, mundo.Servicios.Reloj, [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00]),
            new ReporteDeLaSegundaVueltaDeVerdad(
                new ReportesEnPdf(
                    mundo.Servicios.Casos, mundo.Servicios.Personas, mundo.Servicios.Companeros,
                    mundo.Servicios.Asignaciones, mundo.Servicios.Procedencia, mundo.Servicios.Reloj)),
            mundo.Servicios.Casos,
            mundo.Servicios.Asignaciones,
            mundo.Servicios.Companeros);

    // ---- el escenario, escrito caso por caso --------------------------------

    /// <summary>Los siete casos del escenario y los tres companeros de la escalera.</summary>
    private sealed record Mundo(
        ServiciosFalsos Servicios,
        Companero DeCategoriaUno,
        Companero DeCategoriaDos,
        Companero DeCategoriaTres,
        long NoSePudoComunicar,
        long ElLiderNoLoHizo,
        long OtraRazon,
        long SinMotivo,
        long CompletaConMotivo,
        long ArchivadoConMotivo,
        long TrabadoEnLaDos);

    /// <summary>
    /// Tres companeros, uno por peldano, y siete casos con un motivo cada uno.
    /// </summary>
    /// <remarks>
    /// Se monta con CERO casos del generador y se escriben los siete a mano: con casos
    /// inventados al azar encima, «cuántos entran» dejaria de tener una respuesta que se pueda
    /// escribir en la prueba, y el criterio pide decir cuántos de cuántos.
    /// </remarks>
    private static Mundo Escenario()
    {
        var servicios = new ServiciosFalsos(0, 7, new RelojFijo(Hoy));
        var almacen = servicios.Almacen;
        almacen.Companeros.Clear();

        var uno = Alta(almacen, "Sandy", 1);
        var dos = Alta(almacen, "Yudelka", 2);
        var tres = Alta(almacen, "Marisol", 3);

        var noSePudo = Caso(almacen, "CASP0001", "no_completa", "no_se_pudo_comunicar", uno);
        var noLoHizo = Caso(almacen, "CASP0002", "no_completa", "el_lider_no_lo_hizo", uno);
        var otra = Caso(almacen, "CASP0003", "no_completa", "otra_razon", uno);
        var sinMotivo = Caso(almacen, "CASP0004", "no_completa", null, uno);
        var completa = Caso(almacen, "CASP0005", "completa", "no_se_pudo_comunicar", uno);
        var archivado = Caso(almacen, "CASP0006", "no_completa", "no_se_pudo_comunicar", uno, archivado: true);
        var trabadoEnLaDos = Caso(almacen, "CASP0007", "no_completa", "no_se_pudo_comunicar", dos);

        return new Mundo(
            servicios, uno, dos, tres,
            noSePudo, noLoHizo, otra, sinMotivo, completa, archivado, trabadoEnLaDos);
    }

    private static Companero Alta(AlmacenFalso almacen, string nombre, int categoria)
    {
        var id = almacen.SiguienteId();
        var companero = new Companero
        {
            Id = id,
            Nombre = nombre,
            Activo = true,
            CreadoEn = $"{Hoy} 08:00:00",
            Rol = categoria == 1 ? RolDeCompanero.Companero : RolDeCompanero.Gerente,
            Categoria = categoria,
        };
        almacen.Companeros[id] = companero;
        return companero;
    }

    /// <summary>Un caso con su estado, su motivo, una persona dentro y su asignacion retirada.</summary>
    /// <remarks>
    /// La asignacion se deja RETIRADA a proposito: es como queda un caso cuando el companero ya
    /// devolvio su hoja. Si la seleccion solo mirara las asignaciones vivas, no encontraria a
    /// nadie que lo hubiera intentado y no subiria nada.
    /// </remarks>
    private static long Caso(
        AlmacenFalso almacen, string numero, string estado, string? motivo,
        Companero quienLoIntento, bool archivado = false)
    {
        var casoId = almacen.SiguienteId();
        almacen.Casos[casoId] = new Caso
        {
            Id = casoId,
            NumeroCaso = numero,
            UnidadNombre = "Castries Branch",
            UnidadNumero = "700001",
            FechaViaje = "2026-09-08",
            CreadoEn = $"{Hoy} 09:00:00",
            EstadoRecomendacion = estado,
            MotivoDelCompanero = motivo,
            Archivado = archivado,
            FechaArchivado = archivado ? Hoy : null,
        };

        var personaId = almacen.SiguienteId();
        almacen.Personas[personaId] = new Persona
        {
            Id = personaId,
            CasoId = casoId,
            Nombre = $"Persona de {numero}",
            Mrn = $"055-0000-{casoId:0000}",
            NotaCompanero = $"Lo intenté tres veces con el líder de {numero}.",
        };

        var asignacionId = almacen.SiguienteId();
        almacen.Asignaciones[asignacionId] = new Asignacion
        {
            Id = asignacionId,
            CasoId = casoId,
            CompaneroId = quienLoIntento.Id,
            AsignadoEn = $"{Hoy} 09:30:00",
            Activa = false,
            DesactivadaEn = $"{Hoy} 18:00:00",
        };

        return casoId;
    }
}

/// <summary>
/// El mismo enganche que monta la cascara, para poder probar el camino entero desde aqui.
/// </summary>
/// <remarks>
/// Es una copia de <c>Cascara/ReporteDeLaSegundaVueltaEnPdf</c>, que es <c>internal</c> y no se
/// ve desde este proyecto. No se prueba el adaptador —son tres lineas de traduccion—: lo que se
/// prueba es que la operacion, con un motor de PDF de VERDAD detras, deja los dos archivos
/// hermanos escritos en el disco.
/// </remarks>
internal sealed class ReporteDeLaSegundaVueltaDeVerdad : IReporteDeLaSegundaVuelta
{
    private readonly IReportesDeLaEscalera _motor;

    internal ReporteDeLaSegundaVueltaDeVerdad(IReportesDeLaEscalera motor) => _motor = motor;

    public ResultadoDeEscritura Escribir(int categoria, IReadOnlyList<CasoQueSube> casos, string rutaDestino)
        => _motor.GenerarReporteDeLaSegundaVuelta(
            categoria,
            [.. casos.Select(caso => new IntentoAnterior(
                caso.CasoId, caso.QuienLoIntento, caso.CategoriaDeQuienLoIntento, caso.Motivo))],
            rutaDestino);
}
