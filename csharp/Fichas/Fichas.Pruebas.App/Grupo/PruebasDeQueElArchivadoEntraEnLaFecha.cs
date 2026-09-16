using Fichas.App.Grupo;
using Fichas.App.Inicio;
using Fichas.App.Vocabulario;
using Fichas.Datos.Falso;
using Fichas.Pruebas.App.Inicio;

namespace Fichas.Pruebas.App.Grupo;

/// <summary>
/// Un documento archivado ENTRA en el grupo de su fecha, en verde y marcado como archivado,
/// para que lo que dice el calendario de fuera y lo que se ve dentro cuadren.
/// </summary>
/// <remarks>
/// <para><b>De dónde sale.</b> El 2026-09-14 el dueño pidió que dentro de la fecha lo
/// completo saliera en verde y lo que no en rojo <i>«como se muestra en el calendario
/// afuera»</i>. El programador que lo hizo midió que no cuadraban: un archivado contaba fuera
/// —2026-09-07, <i>«aunque se archive, debe quedarse en el calendario marcado en verde»</i>— y
/// no entraba dentro —2026-09-06, <i>«no aparecer más en ningún lado»</i>—. Un día decía fuera
/// «me falta 4 de 5» y dentro «me falta 4 de 4», y un día resuelto solo por archivados se abría
/// vacío. Se le preguntó con las dos opciones y contestó «Dale» sin elegir; <b>el supervisor
/// decidió la A</b> —el archivado entra al grupo de la fecha, en verde— y lo dejó escrito para
/// que el dueño lo cambie si quiere.</para>
///
/// <para>⛔ <b>Lo que NO cambia:</b> Flujo, Corrección y Asignar siguen sin archivados (decisión
/// del 06), y un archivado dentro del grupo no se asigna ni se verifica desde ahí. Estas pruebas
/// se escribieron ANTES del código y salieron rojas al escribirlas.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeQueElArchivadoEntraEnLaFecha
{
    /// <summary>El día sobre el que se montan los casos de estas pruebas.</summary>
    private static readonly DateOnly ElDia = new(2026, 9, 17);

    /// <summary>La misma fecha, como la guarda la base.</summary>
    private const string ElDiaIso = "2026-09-17";

    /// <summary>Los renglones de personas del día, tal como los aplana la pantalla.</summary>
    /// <param name="servicios">Donde está montado el día.</param>
    private static List<RenglonDelGrupo> PersonasDelDia(ServiciosFalsos servicios)
        => BaseDeInicio.LectorDeGruposDe(servicios).DelDia(ElDia).EnUnaSolaLista()
            .Where(r => r.EsUnaPersona).ToList();

    // ---------------------------------------------- 1. el archivado entra, en verde

    /// <summary>
    /// Un día con un vivo y un archivado trae los DOS documentos, y el archivado va en verde,
    /// se lee «resuelto» y lleva la nota de archivado al lado; sin una tercera palabra.
    /// </summary>
    [TestMethod]
    public void UnArchivadoEntraEnElGrupoDeSuFechaYVaEnVerde()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var vivo = BaseDeInicio.MeterCaso(servicios, "VIVO2609", ElDiaIso, cuantasPersonas: 1);
        BaseDeInicio.DejarNoListaParaViajar(servicios, vivo, fila: 1);
        BaseDeInicio.MeterCaso(servicios, "ARCH2609", ElDiaIso, archivado: true, cuantasPersonas: 1);

        var grupo = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(ElDia);
        var renglones = PersonasDelDia(servicios);
        var archivado = renglones.Single(r => r.Detalle.Contains("ARCH2609", StringComparison.Ordinal));
        var elVivo = renglones.Single(r => r.Detalle.Contains("VIVO2609", StringComparison.Ordinal));

        Console.WriteLine($"«{archivado.Detalle}» → {archivado.Color}");
        Console.WriteLine($"«{elVivo.Detalle}» → {elVivo.Color}");

        Assert.AreEqual(2, grupo.CuantosDocumentos, "El archivado se cuenta.");
        Assert.AreEqual(2, grupo.CuantasPersonas);
        Assert.IsTrue(archivado.Archivado);
        Assert.AreEqual(ColorDeLaPastilla.Verde, archivado.Color);
        Assert.AreEqual(DosEstados.Resuelto, archivado.PalabraDelEstado, "Sigue siendo una de las dos palabras.");
        StringAssert.Contains(archivado.Detalle, "resuelto · archivado", StringComparison.Ordinal);
        Assert.IsFalse(elVivo.Archivado);
        // Cinco en sí y una en no: desde el 2026-09-16 es NARANJA (a medias), no rojo. Lo que
        // esta prueba vigila es que el vivo NO vaya en verde como el archivado.
        Assert.AreEqual(ColorDeLaPastilla.Naranja, elVivo.Color);
        Assert.AreNotEqual(ColorDeLaPastilla.Verde, elVivo.Color);
        Assert.DoesNotContain("archivado", elVivo.Detalle, StringComparison.Ordinal);
    }

    /// <summary>La nota del archivado no inventa un tercer estado: la palabra sigue siendo una de las dos.</summary>
    [TestMethod]
    public void LaNotaDeArchivadoNoEsUnaTerceraPalabraDeEstado()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "ARCH2609", ElDiaIso, archivado: true, cuantasPersonas: 2);

        foreach (var renglon in PersonasDelDia(servicios))
        {
            CollectionAssert.Contains(DosEstados.LasDos.ToList(), renglon.PalabraDelEstado);
            StringAssert.Contains(renglon.DetalleDelEstado, "archiv", StringComparison.Ordinal);
            Assert.IsFalse(renglon.DetalleDelEstado.Contains("te toca a ti", StringComparison.Ordinal),
                "A un archivado no le queda trabajo que hacer.");
        }
    }

    // ---------------------------------------------- 2. fuera y dentro cuadran

    /// <summary>
    /// Tres vivos sin confirmar y dos archivados en la misma unidad: la pastilla de fuera dice
    /// «me falta 3 de 5» y la cabecera de la unidad y la del día dicen lo mismo.
    /// </summary>
    [TestMethod]
    public void LaCuentaDeDentroCuadraConLaPastillaDeFueraCuandoHayUnArchivado()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "VIVO2601", ElDiaIso, cuantasPersonas: 3);
        BaseDeInicio.MeterCaso(servicios, "ARCH2602", ElDiaIso, archivado: true, cuantasPersonas: 2);

        var lector = BaseDeInicio.LectorDeGruposDe(servicios);
        var pastilla = lector.PorDia()[ElDia].Single();
        var grupo = lector.DelDia(ElDia);
        var unidad = grupo.Unidades.Single();
        var cabecera = grupo.EnUnaSolaLista().Single(r => r.EsCabecera);

        Console.WriteLine($"fuera: «{pastilla.Etiqueta}» {pastilla.Color}");
        Console.WriteLine($"dentro, unidad: «{unidad.Detalle}» {unidad.Color}");
        Console.WriteLine($"dentro, día: «{grupo.ComoVanLasPersonas}»");

        Assert.AreEqual("me falta 3 de 5", pastilla.Etiqueta);
        Assert.AreEqual("me falta 3 de 5", grupo.ComoVanLasPersonas, "La cabecera del día dice lo de fuera.");
        StringAssert.EndsWith(unidad.Detalle, "me falta 3 de 5", StringComparison.Ordinal);
        Assert.AreEqual(pastilla.Color, unidad.Color);
        Assert.AreEqual(pastilla.Color, cabecera.Color);
        Assert.AreEqual(pastilla.CuantasPersonasResueltas, unidad.CuantasPersonasResueltas);
        Assert.AreEqual(2, unidad.CuantasPersonasResueltas, "Las dos del archivado, y ninguna más.");
        Assert.AreEqual(0, unidad.CuantasPersonasConfirmadas, "Y sin decir que sus seis dicen que sí.");
    }

    /// <summary>Lo que le falta a la unidad no cuenta al archivado: a él no hay que verificarlo.</summary>
    [TestMethod]
    public void LoQueLeFaltaALaUnidadNoPideVerificarAlArchivado()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "VIVO2601", ElDiaIso, cuantasPersonas: 1);
        BaseDeInicio.MeterCaso(servicios, "ARCH2602", ElDiaIso, archivado: true, cuantasPersonas: 4);

        var unidad = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(ElDia).Unidades.Single();

        Console.WriteLine($"«{unidad.LoQueLeFaltaALaUnidad}»");
        StringAssert.Contains(unidad.LoQueLeFaltaALaUnidad, "falta verificar 1 recomendación", StringComparison.Ordinal);
        Assert.AreEqual(1, unidad.CuantasPersonasSinConfirmar);
    }

    // ---------------------------------------------- 3. el día resuelto solo por archivados

    /// <summary>
    /// Un día en el que todo lo que viajaba está archivado ya no se abre vacío: trae sus
    /// documentos, en verde, y dice «resuelto» igual que la pastilla de fuera.
    /// </summary>
    [TestMethod]
    public void UnDiaResueltoSoloPorArchivadosYaNoSeAbreVacio()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "ARCH2601", ElDiaIso, archivado: true, cuantasPersonas: 2);
        BaseDeInicio.MeterCaso(servicios, "ARCH2602", ElDiaIso, archivado: true, cuantasPersonas: 1);

        var lector = BaseDeInicio.LectorDeGruposDe(servicios);
        var pastilla = lector.PorDia()[ElDia].Single();
        var grupo = lector.DelDia(ElDia);
        var renglones = grupo.EnUnaSolaLista();

        Console.WriteLine($"fuera: «{pastilla.Etiqueta}» · dentro: «{grupo.ComoVanLasPersonas}» · {grupo.LineaDelDenominador}");

        Assert.IsFalse(grupo.EstaVacio);
        Assert.AreEqual(2, grupo.CuantosDocumentos);
        Assert.AreEqual(3, grupo.CuantasPersonas);
        Assert.AreEqual(DosEstados.Resuelto, pastilla.Etiqueta);
        Assert.AreEqual(DosEstados.Resuelto, grupo.ComoVanLasPersonas);
        Assert.AreEqual(DosEstados.Resuelto, grupo.Unidades.Single().LoQueLeFaltaALaUnidad);
        Assert.AreEqual(ColorDeLaPastilla.Verde, renglones.Single(r => r.EsCabecera).Color);
        Assert.IsTrue(renglones.Where(r => r.EsUnaPersona).All(r => r.Color == ColorDeLaPastilla.Verde));
        Assert.HasCount(3, renglones.Where(r => r.EsUnaPersona).ToList());
    }

    // ---------------------------------------------- 4. desde el grupo no se asigna ni se verifica

    /// <summary>
    /// El renglón de un archivado no se puede pulsar para verificar, y dice por qué; el de un
    /// vivo sí.
    /// </summary>
    [TestMethod]
    public void ElRenglonDeUnArchivadoNoSePuedeVerificarYDicePorQue()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "VIVO2609", ElDiaIso, cuantasPersonas: 1);
        BaseDeInicio.MeterCaso(servicios, "ARCH2609", ElDiaIso, archivado: true, cuantasPersonas: 1);

        var renglones = PersonasDelDia(servicios);
        var archivado = renglones.Single(r => r.Archivado);
        var vivo = renglones.Single(r => !r.Archivado);

        Console.WriteLine($"archivado: «{archivado.ParaElLector}»");

        Assert.IsFalse(archivado.SePuedeVerificar, "Un archivado no se abre en Corrección desde aquí.");
        Assert.IsTrue(vivo.SePuedeVerificar);
        StringAssert.Contains(archivado.ParaElLector, "Revisar", StringComparison.Ordinal);
        Assert.DoesNotContain("Pulse para verificar", archivado.ParaElLector, StringComparison.Ordinal);
        StringAssert.Contains(vivo.ParaElLector, "Pulse para verificar", StringComparison.Ordinal);
        StringAssert.Contains(archivado.LoQueLeFaltaAlDocumento, "archivado", StringComparison.Ordinal);
    }

    /// <summary>
    /// Ni «Asignar esta unidad» ni «Asignar el grupo entero» llevan al archivado, y una unidad
    /// que solo tiene archivados se queda sin botón.
    /// </summary>
    [TestMethod]
    public void AsignarDesdeElGrupoNoLlevaAlArchivado()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var vivo = BaseDeInicio.MeterCaso(servicios, "VIVO2601", ElDiaIso, unidadNumero: "700001");
        var archivadoEnLaMisma = BaseDeInicio.MeterCaso(servicios, "ARCH2602", ElDiaIso, archivado: true, unidadNumero: "700001");
        var archivadoSolo = BaseDeInicio.MeterCaso(servicios, "ARCH2603", ElDiaIso, archivado: true, unidadNumero: "990000");

        var grupo = BaseDeInicio.LectorDeGruposDe(servicios).DelDia(ElDia);
        var mixta = grupo.Unidades.Single(u => u.UnidadNumero == "700001");
        var soloArchivados = grupo.Unidades.Single(u => u.UnidadNumero == "990000");
        var cabeceras = grupo.EnUnaSolaLista().Where(r => r.EsCabecera).ToList();

        Assert.AreEqual(3, grupo.CuantosDocumentos, "Los tres se ven.");
        CollectionAssert.AreEquivalent(new[] { vivo }, grupo.LosQueSePuedenAsignar.ToList());
        CollectionAssert.AreEquivalent(new[] { vivo }, mixta.CasosQueSePuedenAsignar.ToList());
        Assert.IsTrue(mixta.SePuedeAsignar);
        Assert.IsEmpty(soloArchivados.CasosQueSePuedenAsignar);
        Assert.IsFalse(soloArchivados.SePuedeAsignar);
        Assert.IsFalse(cabeceras.Single(c => c.Titulo.StartsWith("990000", StringComparison.Ordinal)).SePuedeAsignarLaUnidad);
        CollectionAssert.AreEquivalent(
            new[] { vivo },
            cabeceras.Single(c => c.Titulo.StartsWith("700001", StringComparison.Ordinal)).CasosDeLaUnidad.ToList());
        Assert.DoesNotContain(archivadoEnLaMisma, grupo.LosQueSePuedenAsignar);
        Assert.DoesNotContain(archivadoSolo, grupo.LosQueSePuedenAsignar);
    }

    // ---------------------------------------------- 5. en toda la base inventada

    /// <summary>
    /// En toda la base inventada, día a día: el grupo trae EXACTAMENTE los documentos de esa
    /// fecha —vivos y archivados—, y cada pastilla de fuera cuadra con su unidad de dentro en
    /// cuenta y en color. Se dice cuántas unidades tienen archivados para saber que se probó
    /// algo.
    /// </summary>
    [TestMethod]
    public void EnTodaLaBaseInventadaFueraYDentroCuadranTambienConArchivados()
    {
        var servicios = BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba);
        var lector = BaseDeInicio.LectorDeGruposDe(servicios);
        var todos = BaseDeInicio.TodosLosCasos(servicios);
        var archivadosConFecha = todos.Count(c => c.Archivado && FechasEnEspanol.Leer(c.FechaViaje) is not null);

        var pastillas = 0;
        var conArchivado = 0;
        var archivadosVistos = 0;
        foreach (var (dia, delDia) in lector.PorDia())
        {
            var grupo = lector.DelDia(dia);
            var esperados = todos.Where(c => FechasEnEspanol.Leer(c.FechaViaje) == dia).Select(c => c.Id).ToList();
            CollectionAssert.AreEquivalent(esperados, grupo.TodosLosCasos.ToList(), $"{dia}: no trae exactamente los de la fecha");
            archivadosVistos += grupo.TodasLasPersonas.Where(p => p.Archivado).Select(p => p.CasoId).Distinct().Count();

            Assert.AreEqual(
                DosEstados.Cuenta(delDia.Sum(p => p.CuantasPersonasResueltas), delDia.Sum(p => p.CuantasPersonas)),
                grupo.ComoVanLasPersonas,
                $"{dia}: la cabecera del día no dice lo de fuera");

            foreach (var pastilla in delDia)
            {
                pastillas++;
                var unidad = grupo.Unidades.Single(u => u.UnidadNumero == pastilla.UnidadNumero);
                if (unidad.Personas.Any(p => p.Archivado)) conArchivado++;
                Assert.AreEqual(pastilla.Color, unidad.Color, $"{dia} · {pastilla.Titulo}");
                Assert.AreEqual(pastilla.CuantasPersonasResueltas, unidad.CuantasPersonasResueltas, $"{dia} · {pastilla.Titulo}");
                Assert.IsTrue(unidad.CasosQueSePuedenAsignar.All(id => !todos.Single(c => c.Id == id).Archivado),
                    $"{dia} · {pastilla.Titulo}: ofrece asignar un archivado");
            }
        }

        Console.WriteLine($"{pastillas} pastillas · {conArchivado} con algún archivado · {archivadosVistos} archivados con fecha vistos de {archivadosConFecha}");
        Assert.IsGreaterThan(0, conArchivado, "La base inventada trae archivados con fecha; si no, no se probó nada.");
        Assert.AreEqual(archivadosConFecha, archivadosVistos, "Todos los archivados con fecha entran en su día.");
    }
}
