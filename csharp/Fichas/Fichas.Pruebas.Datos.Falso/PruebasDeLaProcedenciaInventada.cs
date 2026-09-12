using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.Datos.Falso;

/// <summary>
/// La procedencia que el generador inventa: que exista, que se parezca a la de verdad y que
/// no firme nada.
/// </summary>
/// <remarks>
/// <para><b>Por que existe.</b> Hasta el 2026-09-06 el generador no escribia <b>ni una</b>
/// fila de procedencia —<c>grep Procedencias GeneradorFalso.cs</c> daba cero coincidencias—,
/// y con eso el modo <c>--falso</c> no podia medir nada que dependa de de donde salio un
/// valor: toda la base inventada se veia como si el lector no hubiera leido nada.</para>
///
/// <para>Las cifras de abajo son topes anchos, no cifras exactas: lo que se vigila es que la
/// mezcla siga teniendo de todo —alta, baja, tachado, ausente y sin fila—, no que salga un
/// numero concreto. Un tope estrecho se pondria rojo al cambiar una probabilidad y no diria
/// nada sobre si la base sirve.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaProcedenciaInventada
{
    /// <summary>El dia en el que se paran estas pruebas, para que no dependan del calendario.</summary>
    private const string ElDiaDeLaPrueba = "2026-09-04";

    /// <summary>Cuantos casos usa la mayoria de estas pruebas.</summary>
    private const int CasosDePrueba = 3000;

    /// <summary>La semilla; la misma semilla da siempre exactamente la misma base.</summary>
    private const int Semilla = 20260904;

    /// <summary>Genera la base inventada parada en el día de la prueba.</summary>
    /// <param name="cuantos">Cuántos casos; 3 000 por defecto, que es la cifra del requisito 5.</param>
    private static AlmacenFalso Base(int cuantos = CasosDePrueba)
        => GeneradorFalso.Generar(cuantos, Semilla, new RelojFijo(ElDiaDeLaPrueba));

    /// <summary>
    /// Dado el generador con 3 000 casos, cuando se genera la base, entonces casi todos los
    /// campos tienen su fila de procedencia.
    /// </summary>
    /// <remarks>
    /// «Casi» y no «todos» a proposito: 1 de cada 100 nace sin fila, que es un caso que pasa
    /// de verdad y que hay que poder medir.
    /// </remarks>
    [TestMethod]
    public void CasiTodosLosCamposTienenSuFilaDeProcedencia()
    {
        var almacen = Base();

        var esperadas = (almacen.Casos.Count * ProcedenciaInventada.CamposDelCaso.Count)
                        + (almacen.Personas.Count * ProcedenciaInventada.CamposDeLaPersona.Count);

        Assert.IsGreaterThan(0, almacen.Procedencias.Count, "El generador no escribió ni una fila.");
        Assert.IsGreaterThan(
            (int)(esperadas * 0.95),
            almacen.Procedencias.Count,
            $"Se esperaban del orden de {esperadas} filas y salieron {almacen.Procedencias.Count}.");
        Assert.IsLessThanOrEqualTo(esperadas, almacen.Procedencias.Count, "No puede haber más filas que campos.");
    }

    /// <summary>
    /// ⛔ Regla permanente 5: ni una de las 45 000 filas nace firmada.
    /// </summary>
    /// <remarks>
    /// Firmar es de Miguel y nunca es automatico. Una base inventada que naciera con firmas
    /// diria que el dio por buenos datos que no existen.
    /// </remarks>
    [TestMethod]
    public void NiUnaFilaNaceFirmada()
    {
        var almacen = Base();

        var firmadas = almacen.Procedencias.Values.Count(fila => fila.Verificado);

        Assert.AreEqual(0, firmadas, $"{firmadas} filas nacieron con la firma de alguien que no firmó nada.");
        Assert.IsEmpty(almacen.Procedencias.Values.Where(fila => fila.VerificadoPor is not null).ToList());
    }

    /// <summary>
    /// Dado el generador, cuando se genera la base, entonces la mezcla trae de las cinco
    /// clases que la regla de «listo para asignar» distingue.
    /// </summary>
    /// <remarks>
    /// Sin alguna de las cinco, esa clase no se ejerce nunca corriendo con <c>--falso</c>, y
    /// entonces el modo inventado deja de servir para lo unico para lo que existe.
    /// </remarks>
    [TestMethod]
    public void LaMezclaTraeDeLasCincoClases()
    {
        var almacen = Base();
        var filas = almacen.Procedencias.Values.ToList();

        Assert.IsGreaterThan(0, filas.Count(f => f.Confianza >= 0.6), "Ninguna con confianza alta.");
        Assert.IsGreaterThan(0, filas.Count(f => f.Confianza is double c && c < 0.6), "Ninguna con confianza baja.");
        Assert.IsGreaterThan(0, filas.Count(f => f.AnuladoPorTachon), "Ninguna tachada.");
        Assert.IsGreaterThan(0, filas.Count(f => f.AusenteEnElPapel), "Ninguna marcada «no está en el papel».");
        Assert.IsGreaterThan(0, filas.Count(f => f.Confianza is null), "Ninguna con la confianza nula.");
    }

    /// <summary>La mayoria se leyo bien: lo dudoso es la excepcion, no la norma.</summary>
    /// <remarks>
    /// Es lo que impide que la siembra convierta toda la base inventada en incompleta. Si
    /// esto se pusiera rojo, <c>--falso</c> dejaria de servir para medir las pantallas de
    /// trabajo, que es justo lo que la siembra existe para permitir.
    /// </remarks>
    [TestMethod]
    public void LaMayoriaSeLeyoConConfianzaAlta()
    {
        var almacen = Base();
        var filas = almacen.Procedencias.Values.ToList();

        var buenas = filas.Count(f => f.Confianza is double c && c >= 0.6 && !f.AnuladoPorTachon);

        Assert.IsGreaterThan(
            (int)(filas.Count * 0.8),
            buenas,
            $"Solo {buenas} de {filas.Count} filas se leyeron bien; la base inventada dejaría de parecerse a la real.");
    }

    /// <summary>
    /// Un campo marcado como que no está en el papel NUNCA lleva valor debajo.
    /// </summary>
    /// <remarks>
    /// La pantalla de Correccion se niega a marcar como ausente un campo que tiene dato
    /// —«son dos cosas que se contradicen»—, asi que una base inventada que lo hiciera
    /// tendria filas que el programa no deja crear.
    /// </remarks>
    [TestMethod]
    public void LoMarcadoComoAusenteNuncaTieneValorDebajo()
    {
        var almacen = Base();

        foreach (var fila in almacen.Procedencias.Values.Where(f => f.AusenteEnElPapel))
        {
            Assert.AreEqual(
                OrigenDeCampo.Vacio,
                fila.Origen,
                $"La fila {fila.Id} ({fila.Campo}) dice «no está en el papel» y su origen es {fila.Origen}.");
            Assert.IsNull(fila.Confianza, $"La fila {fila.Id} dice «no está en el papel» y trae confianza.");
        }
    }

    /// <summary>Toda fila apunta a un caso o a una persona que existe.</summary>
    [TestMethod]
    public void TodaFilaApuntaAUnRegistroQueExiste()
    {
        var almacen = Base(300);

        foreach (var fila in almacen.Procedencias.Values)
        {
            var existe = fila.Tabla == TablaDeProcedencia.Casos
                ? almacen.Casos.ContainsKey(fila.RegistroId)
                : almacen.Personas.ContainsKey(fila.RegistroId);
            Assert.IsTrue(existe, $"La fila {fila.Id} apunta al {fila.Tabla} {fila.RegistroId}, que no existe.");
        }
    }

    /// <summary>Y no hay dos filas para el mismo campo del mismo registro: es la clave de la tabla.</summary>
    [TestMethod]
    public void NoHayDosFilasParaElMismoCampo()
    {
        var almacen = Base(500);

        var claves = almacen.Procedencias.Values
            .Select(fila => $"{fila.Tabla}:{fila.RegistroId}:{fila.Campo}")
            .ToList();

        Assert.HasCount(claves.Count, claves.Distinct().ToList(), "Hay campos con dos filas de procedencia.");
    }

    /// <summary>
    /// La misma semilla sigue dando la misma procedencia, campo por campo.
    /// </summary>
    /// <remarks>
    /// Es la condicion de la FASE C0: sin ella, una cifra medida hoy y otra manana no se
    /// pueden comparar porque no se midieron sobre la misma base.
    /// </remarks>
    [TestMethod]
    public void LaMismaSemillaDaLaMismaProcedencia()
    {
        var primera = Base(200);
        var segunda = Base(200);

        Assert.HasCount(primera.Procedencias.Count, segunda.Procedencias);
        foreach (var (id, fila) in primera.Procedencias)
        {
            Assert.AreEqual(fila, segunda.Procedencias[id], $"La fila {id} salió distinta con la misma semilla.");
        }
    }

    /// <summary>
    /// ⚠️ Sembrar la procedencia NO cambió ni un caso, ni una persona, ni una asignación.
    /// </summary>
    /// <remarks>
    /// Se comprueba contra las cifras que la base ya daba antes de la siembra, medidas con la
    /// semilla 20260904 y 3 000 casos, leídas con la siembra DESACTIVADA: <b>3 000 casos, 7 531 personas, 1 490 asignaciones y
    /// 12 ilegibles</b>. Si el paso de la procedencia se moviera de sitio dentro de
    /// <c>Generar</c>, correría el sorteo de todo lo que viene detrás y estas cuatro cifras
    /// cambiarían sin que nadie lo pidiera.
    /// </remarks>
    [TestMethod]
    public void SembrarLaProcedenciaNoMovioNadaDeLoQueYaHabia()
    {
        var almacen = Base();

        Assert.HasCount(3000, almacen.Casos);
        Assert.HasCount(7531, almacen.Personas);
        Assert.HasCount(1490, almacen.Asignaciones);
        Assert.HasCount(12, almacen.Ilegibles);
    }
}
