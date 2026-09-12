using Fichas.App.Correccion;
using Fichas.App.Grupo;
using Fichas.Contratos.Modelos;
using Fichas.Pruebas.App.Inicio;

namespace Fichas.Pruebas.App.Grupo;

/// <summary>
/// Los dos caminos para leer la procedencia contestan lo MISMO, campo por campo.
/// </summary>
/// <remarks>
/// <para><b>Por que existe, y por que no es una prueba de adorno.</b>
/// <see cref="ProcedenciasDeUnaPasada"/> tiene dos caminos y no traen lo mismo: el de un
/// documento trae TODAS las filas de ese documento; el que usan Inicio y la cola trae solo
/// las que <c>IProcedencia.LasQuePesanEnElVeredicto</c> deja pasar, y de las demas solo sabe
/// que existen. Que las dos contesten igual descansa en un razonamiento —una fila que el
/// filtro no deja pasar no puede ser dudosa mientras su valor este puesto y valga— y un
/// razonamiento no es una medicion.</para>
///
/// <para>Si el filtro del puerto y <see cref="EstadosDeCampo.EsDudoso"/> se separan alguna
/// vez, esto se pone rojo. Sin esto, la pantalla de Inicio daria por buenos campos que
/// Correccion marca, que es exactamente el defecto que se cerro el 2026-09-06.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLasDosPasadasDeProcedencia
{
    /// <summary>
    /// Dada la base inventada, cuando se pregunta por los dos caminos, entonces contestan
    /// lo mismo en todos los campos de todos los documentos.
    /// </summary>
    /// <remarks>
    /// Trescientos documentos y no 3 000: el camino de un documento hace una consulta por
    /// registro, y con 3 000 esta prueba tardaria segundos a proposito. Con 300 ya pasan por
    /// aqui las seis clases de fila que <c>ProcedenciaInventada</c> siembra, y eso se
    /// comprueba abajo en vez de darlo por hecho.
    /// </remarks>
    [TestMethod]
    public void LosDosCaminosContestanLoMismoEnTodosLosCamposDeLaBaseInventada()
    {
        var servicios = BaseDeInicio.MontarServicios(300);
        var enBloque = ProcedenciasDeUnaPasada.DeTodaLaBase(servicios.Procedencia);
        var casos = BaseDeInicio.TodosLosCasos(servicios);

        var comparados = 0;
        var dudosos = 0;

        foreach (var caso in casos)
        {
            var personas = servicios.Personas.DeCaso(caso.Id);
            var deUno = ProcedenciasDeUnaPasada.DeUnDocumento(servicios.Procedencia, caso, personas);

            comparados += CompararUnRegistro(
                enBloque, deUno, TablaDeProcedencia.Casos, caso.Id,
                LoQueLeFalta.ColumnasDelCaso, ValoresDelCaso(caso), ref dudosos);

            foreach (var persona in personas)
            {
                comparados += CompararUnRegistro(
                    enBloque, deUno, TablaDeProcedencia.Personas, persona.Id,
                    LoQueLeFalta.ColumnasDeLaPersona, [persona.Mrn, persona.Nombre], ref dudosos);
            }
        }

        Console.WriteLine($"Procedencia · {comparados} campos comparados, {dudosos} dudosos.");
        Assert.IsGreaterThan(1000, comparados, "La comparación tiene que recorrer miles de campos.");
        Assert.IsGreaterThan(0, dudosos, "Si ninguno sale dudoso, esta prueba no compara nada útil.");
    }

    /// <summary>
    /// Dada una fila que el filtro NO deja pasar, cuando se pregunta por el camino en
    /// bloque, entonces contesta que no es dudosa: es la unica suposicion del diseno.
    /// </summary>
    /// <remarks>
    /// Es el caso que el recorrido de arriba podria no tocar nunca si la base inventada
    /// cambiara de mezcla. Se monta a mano para que no dependa de eso.
    /// </remarks>
    [TestMethod]
    public void UnaFilaQueElFiltroNoDejaPasarNoEsDudosa()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = 4242L;
        servicios.Procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos,
            RegistroId = casoId,
            Campo = LoQueLeFalta.ColumnaDeLaUnidadNumero,
            Origen = OrigenDeCampo.Ocr,
            Confianza = 0.99,
        });

        var procedencias = ProcedenciasDeUnaPasada.DeTodaLaBase(servicios.Procedencia);

        Assert.IsFalse(
            procedencias.EsDudoso(
                TablaDeProcedencia.Casos, casoId, LoQueLeFalta.ColumnaDeLaUnidadNumero,
                "7000011", esValido: true),
            "Una fila leída con 0,99, sin firma, sin tachón y presente en el papel no es dudosa.");

        Assert.IsTrue(
            procedencias.EsDudoso(
                TablaDeProcedencia.Casos, casoId, LoQueLeFalta.ColumnaDelTemplo, "Panamá", esValido: true),
            "De un campo del que no consta ninguna fila no se sabe de dónde salió su valor.");
    }

    /// <summary>
    /// Las columnas que nombra la cola son las MISMAS que dibuja la pantalla de Correccion.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Esto ya fallo una vez de verdad.</b> La importacion llevaba su lista de campos y
    /// la pantalla la suya, se separaron sin que nadie lo viera, y <c>templo_nombre</c> acabo
    /// dibujado en una y sin fila de procedencia en la otra —0 de 7 casos reales—. Ahora hay
    /// una tercera lista, la de <see cref="LoQueLeFalta"/>, y si se separa de la de Correccion
    /// las dos pantallas volverian a preguntar por campos distintos <b>sin dar ningun error</b>:
    /// una columna mal escrita no existe, y un campo que no existe no tiene fila, y un campo
    /// sin fila sale como dudoso para siempre.
    /// </remarks>
    [TestMethod]
    public void LaColaYLaPantallaDeCorreccionNombranLasMismasColumnas()
    {
        CollectionAssert.AreEqual(
            ModeloDeCorreccion.CamposDelCasoQueSeDibujan.ToArray(),
            LoQueLeFalta.ColumnasDelCaso.ToArray(),
            "Las cinco columnas del caso, y en el mismo orden.");

        CollectionAssert.AreEqual(
            ModeloDeCorreccion.CamposDeLaPersonaQueSeDibujan.ToArray(),
            LoQueLeFalta.ColumnasDeLaPersona.ToArray(),
            "Las dos columnas de la persona, y en el mismo orden.");
    }

    /// <summary>Compara los campos de un registro por los dos caminos y cuenta los dudosos.</summary>
    /// <param name="enBloque">La procedencia leída de toda la base de una vez.</param>
    /// <param name="deUno">La procedencia leída solo de ese documento.</param>
    /// <param name="tabla">Si el registro es un caso o una persona.</param>
    /// <param name="registroId">El número interno del registro.</param>
    /// <param name="columnas">Las columnas que se comparan, en orden.</param>
    /// <param name="valores">El valor de cada columna, en el mismo orden.</param>
    /// <param name="dudosos">Donde se suman los campos que salieron dudosos.</param>
    /// <returns>Cuántos campos se compararon.</returns>
    private static int CompararUnRegistro(
        ProcedenciasDeUnaPasada enBloque,
        ProcedenciasDeUnaPasada deUno,
        TablaDeProcedencia tabla,
        long registroId,
        IReadOnlyList<string> columnas,
        IReadOnlyList<string?> valores,
        ref int dudosos)
    {
        for (var i = 0; i < columnas.Count; i++)
        {
            var esValido = ReglasDeCampo.MotivoDe(columnas[i], valores[i]) is null;
            var porBloque = enBloque.EsDudoso(tabla, registroId, columnas[i], valores[i], esValido);
            var porUno = deUno.EsDudoso(tabla, registroId, columnas[i], valores[i], esValido);

            Assert.AreEqual(
                porUno,
                porBloque,
                $"El campo «{columnas[i]}» de {tabla}:{registroId} sale dudoso por un camino y "
                + "no por el otro. Las dos pantallas dirían cosas distintas del mismo campo.");

            if (porUno) dudosos++;
        }

        return columnas.Count;
    }

    /// <summary>Los cinco valores del caso, en el orden de <see cref="LoQueLeFalta.ColumnasDelCaso"/>.</summary>
    /// <param name="caso">El documento del que se sacan.</param>
    private static IReadOnlyList<string?> ValoresDelCaso(Caso caso)
        => [caso.NumeroCaso, caso.UnidadNumero, caso.UnidadNombre, caso.FechaViaje, caso.TemploNombre];
}
