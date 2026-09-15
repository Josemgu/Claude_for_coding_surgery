using System.Diagnostics;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;
using Fichas.Reportes.Consultas;
using Fichas.Reportes.Reglas;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// R-7 (PENDIENTES.md, plan del 2026-09-15): la verificación de cada caso se lee EN BLOQUE y no
/// una consulta por caso y otra por persona.
/// </summary>
/// <remarks>
/// <para>Las tres pruebas salen del criterio de cierre y no del código: (1) cuántas consultas
/// de procedencia salen al leer 3 000 casos; (2) que lo leído en bloque dice EXACTAMENTE lo
/// mismo que leerlo registro a registro, que es lo que hacía el código hasta hoy; (3) que la
/// métrica 1 sigue contando un caso verificado dentro del período aunque su viaje caiga fuera,
/// porque la métrica se sitúa por <c>verificado_en</c> y no por <c>fecha_viaje</c>
/// (<see cref="Metricas"/>). La tercera es la que impide «recortar al período antes de leer la
/// procedencia», que era la opción B del plan tal como estaba escrita.</para>
/// </remarks>
[TestClass]
public class PruebaDeQueLaLecturaNoVaFilaAFila
{
    /// <summary>Los casos del requisito 5 del dueño.</summary>
    private const int TresMil = 3_000;

    /// <summary>Con las dos lecturas en bloque del contrato bastan tres consultas: las que pesan y los campos anotados de cada tabla.</summary>
    private const int ConsultasQueBastan = 3;

    /// <summary>Vigila que leer 3 000 casos no pregunta por ningún registro suelto, e imprime consultas y milisegundos.</summary>
    [TestMethod]
    public void ConTresMilCasosLaVerificacionSeLeeEnBloqueYNoRegistroARegistro()
    {
        var servicios = BaseDePrueba.Montar(TresMil);
        var contada = new ProcedenciaQueSeCuenta(servicios.Procedencia);

        var reloj = Stopwatch.StartNew();
        var lectura = LecturaParaReportes.Leer(
            servicios.Casos, servicios.Personas, servicios.Companeros, servicios.Asignaciones, contada);
        reloj.Stop();

        Console.WriteLine(
            $"MEDIDO · leer {TresMil} casos y {servicios.Almacen.Personas.Count} personas: "
            + $"{contada.ConsultasDeLectura} consultas de procedencia "
            + $"({contada.VecesQueSePreguntoPorUnRegistro} por registro, {contada.VecesEnBloque} en bloque, "
            + $"{contada.VecesLosCamposAnotados} de campos anotados) · {reloj.Elapsed.TotalMilliseconds:F0} ms");

        Assert.HasCount(TresMil, lectura.CasosConSuVerificacion);
        Assert.AreEqual(0, contada.VecesQueSePreguntoPorUnRegistro, "no se pregunta por ningún registro suelto");
        Assert.AreEqual(0, contada.VecesQueSeContaronVerificados, "tampoco se cuentan verificados registro a registro");
        Assert.IsLessThanOrEqualTo(ConsultasQueBastan, contada.ConsultasDeLectura);
        Assert.AreEqual(0, contada.Escrituras, "un reporte no escribe");
    }

    /// <summary>Vigila que campos, verificados y última firma de cada caso salen iguales en bloque que registro a registro.</summary>
    [TestMethod]
    public void LoLeidoEnBloqueDiceLoMismoQueLeerRegistroARegistro()
    {
        var servicios = BaseDePrueba.Montar(600, semilla: 11);
        FirmarEntero(servicios, CasoNumero(servicios, 3), "2026-09-12 11:00:00");
        FirmarEntero(servicios, CasoNumero(servicios, 8), "2026-08-03 11:00:00");

        var lectura = LecturaParaReportes.Leer(
            servicios.Casos, servicios.Personas, servicios.Companeros, servicios.Asignaciones, servicios.Procedencia);

        var esperado = servicios.Almacen.Casos.Values
            .Select(caso => RegistroARegistro(servicios, caso))
            .OrderBy(c => c.Caso.NumeroCaso ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(c => c.Caso.Id)
            .ToList();

        Assert.HasCount(esperado.Count, lectura.CasosConSuVerificacion);
        for (var i = 0; i < esperado.Count; i++)
        {
            var (quiero, tengo) = (esperado[i], lectura.CasosConSuVerificacion[i]);
            Assert.AreEqual(quiero.Caso.Id, tengo.Caso.Id, $"orden en la posición {i}");
            Assert.AreEqual(quiero.Personas, tengo.Personas, $"personas del caso {quiero.Caso.Id}");
            Assert.AreEqual(quiero.Campos, tengo.Campos, $"campos del caso {quiero.Caso.Id}");
            Assert.AreEqual(quiero.CamposVerificados, tengo.CamposVerificados, $"verificados del caso {quiero.Caso.Id}");
            Assert.AreEqual(quiero.VerificadoEn, tengo.VerificadoEn, $"última firma del caso {quiero.Caso.Id}");
        }

        Assert.IsGreaterThan(0, esperado.Count(Metricas.CasoVerificado), "la base tiene que traer casos enteros, o la prueba no compara nada");
        Assert.IsGreaterThan(0, esperado.Count(c => c.CamposVerificados > 0 && !Metricas.CasoVerificado(c)), "y casos a medias");
    }

    /// <summary>Vigila que un caso verificado en septiembre cuenta en la métrica 1 aunque viaje en marzo, y no al revés.</summary>
    [TestMethod]
    public void UnCasoVerificadoEnElPeriodoCuentaAunqueSuViajeCaigaFuera()
    {
        var servicios = BaseDePrueba.Montar(40, semilla: 5);
        var viajaEnMarzo = CasoNumero(servicios, 0);
        var viajaEnSeptiembre = CasoNumero(servicios, 1);
        servicios.Almacen.Casos[viajaEnMarzo] = servicios.Almacen.Casos[viajaEnMarzo] with { FechaViaje = "2026-03-01" };
        servicios.Almacen.Casos[viajaEnSeptiembre] = servicios.Almacen.Casos[viajaEnSeptiembre] with { FechaViaje = "2026-09-10" };
        FirmarEntero(servicios, viajaEnMarzo, "2026-09-12 11:00:00");
        FirmarEntero(servicios, viajaEnSeptiembre, "2026-08-03 11:00:00");

        var lectura = LecturaParaReportes.Leer(
            servicios.Casos, servicios.Personas, servicios.Companeros, servicios.Asignaciones, servicios.Procedencia);
        var septiembre = Periodo.Leer("2026-09-01", "2026-09-30").Periodo!;
        var verificados = Metricas.CasosVerificadosEnElPeriodo(lectura.CasosConSuVerificacion, septiembre);

        Assert.IsTrue(verificados.Any(c => c.Caso.Id == viajaEnMarzo), "verificado en septiembre: cuenta, viaje donde viaje");
        Assert.IsFalse(verificados.Any(c => c.Caso.Id == viajaEnSeptiembre), "verificado en agosto: no cuenta aunque viaje en septiembre");
        var delPeriodoPorViaje = lectura.CasosConSuVerificacion.Count(c => septiembre.ContieneFecha(c.Caso.FechaViaje));
        Assert.AreEqual(delPeriodoPorViaje, Metricas.DeteccionAntesDelViaje(lectura.CasosConSuVerificacion, septiembre).CasosDelPeriodo,
            "la métrica 3 sí se sitúa por la fecha de viaje: el de marzo no está en su universo y el de septiembre sí");
    }

    /// <summary>El id del caso que ocupa esa posición, con los ids en orden.</summary>
    /// <param name="servicios">La base inventada.</param>
    /// <param name="posicion">Cuál, empezando en cero.</param>
    private static long CasoNumero(ServiciosFalsos servicios, int posicion)
        => servicios.Almacen.Casos.Keys.Order().ElementAt(posicion);

    /// <summary>Firma todos los campos anotados de ese caso y de sus personas con esa marca.</summary>
    /// <param name="servicios">La base inventada; se firma por su puerto de procedencia.</param>
    /// <param name="casoId">El caso que queda verificado entero.</param>
    /// <param name="cuando">La marca «AAAA-MM-DD HH:mm:ss» de todas las firmas.</param>
    private static void FirmarEntero(ServiciosFalsos servicios, long casoId, string cuando)
    {
        var companero = servicios.Almacen.Companeros.Keys.Order().First();
        foreach (var fila in servicios.Procedencia.DeRegistro(TablaDeProcedencia.Casos, casoId))
            servicios.Procedencia.Firmar(TablaDeProcedencia.Casos, casoId, fila.Campo, companero, cuando);
        foreach (var persona in servicios.Almacen.PersonasDe(casoId))
            foreach (var fila in servicios.Procedencia.DeRegistro(TablaDeProcedencia.Personas, persona.Id))
                servicios.Procedencia.Firmar(TablaDeProcedencia.Personas, persona.Id, fila.Campo, companero, cuando);
    }

    /// <summary>Lo que decía el código hasta hoy, escrito desde el criterio: una consulta por caso y una por persona.</summary>
    /// <param name="servicios">La base inventada.</param>
    /// <param name="caso">El caso que se recuenta.</param>
    private static CasoConSuVerificacion RegistroARegistro(ServiciosFalsos servicios, Caso caso)
    {
        var personas = servicios.Almacen.PersonasDe(caso.Id).ToList();
        var filas = servicios.Procedencia.DeRegistro(TablaDeProcedencia.Casos, caso.Id)
            .Concat(personas.SelectMany(p => servicios.Procedencia.DeRegistro(TablaDeProcedencia.Personas, p.Id)))
            .ToList();
        var firmadas = filas.Where(f => f.Verificado).ToList();
        return new CasoConSuVerificacion(
            caso,
            personas.Count,
            filas.Count,
            firmadas.Count,
            firmadas.Select(f => f.VerificadoEn).Where(f => f is not null).OrderDescending(StringComparer.Ordinal).FirstOrDefault());
    }
}
