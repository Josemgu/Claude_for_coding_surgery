using Fichas.App.Completar;
using Fichas.App.Correccion;
using Fichas.App.Grupo;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;
using Fichas.Pruebas.App.Inicio;

namespace Fichas.Pruebas.App.Completar;

/// <summary>
/// Un hueco que Miguel cierra diciendo «no está en el papel» sale de la cola y NO vuelve.
/// </summary>
/// <remarks>
/// <para>⛔ <b>Es el peor de los seis casos del 2026-09-06 y por eso tiene su propia
/// prueba.</b> <c>PruebasDeUnSoloVeredicto</c> comprueba que las dos preguntas contestan lo
/// mismo sobre un documento parado; esto comprueba la <b>secuencia entera</b>, que es lo que
/// el dueno vive: el documento entra en la cola, el la marca, sale, y no vuelve. Hasta ese
/// dia salia y volvia: Correccion lo daba por listo y la cola —que solo veia un campo
/// vacio— se lo quedaba, <b>para siempre</b> y sin forma de sacarlo.</para>
///
/// <para>⛔ <b>Marcar que un dato no esta en el papel NO es firmarlo</b> (regla permanente 5):
/// no toca <c>verificado</c>, y esta prueba lo comprueba contando las firmas antes y despues.
/// Es otra columna y es otra cosa: una dice «este dato no existe en la hoja», la otra dice
/// «Miguel dio este dato por bueno».</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeQueUnHuecoCerradoNoVuelve
{
    /// <summary>
    /// Dado un documento al que solo le falta el templo, cuando Miguel marca que no esta en
    /// el papel, entonces sale de la cola y una segunda lectura ya no lo trae.
    /// </summary>
    [TestMethod]
    public void UnTemploMarcadoComoQueNoEstaEnElPapelSaleDeLaColaYNoVuelve()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(
            servicios, "CASP2609", "2026-09-08", temploNombre: null);

        var cola = ColaDeCompletar.Desde(BaseDeInicio.LectorDeIncompletosDe(servicios).Leer());
        Assert.AreEqual(1, cola.Quedan, "Con el templo vacío, el documento tiene que entrar en la cola.");
        Assert.AreEqual(casoId, cola.Primero);

        var firmasAntes = CuantasFirmasHay(servicios);
        var modelo = MontarCorreccion(servicios);
        Assert.IsTrue(modelo.Cargar(casoId));

        var templo = modelo.Campos.Single(
            campo => campo.Tabla == TablaDeProcedencia.Casos
                  && campo.Campo == LoQueLeFalta.ColumnaDelTemplo);
        var marcado = modelo.MarcarQueNoEstaEnElPapel(templo, marcado: true);

        Assert.IsTrue(marcado.SeEscribio, "Marcar «no está en el papel» tiene que escribirse.");
        Assert.IsTrue(modelo.ListoParaAsignar, "Cerrado el hueco, Corrección lo suelta.");

        // ⛔ La regla permanente 5: marcar no es firmar.
        Assert.AreEqual(
            firmasAntes,
            CuantasFirmasHay(servicios),
            "Marcar que un dato no está en el papel NO puede firmar ningún campo.");

        // Y lo que faltaba hasta el 2026-09-06: que la cola diga lo mismo.
        Assert.IsFalse(
            TodaviaLeFalta(servicios, casoId),
            "La cola tiene que estar de acuerdo con Corrección: al documento ya no le falta nada.");

        var segundaLectura = ColaDeCompletar.Desde(BaseDeInicio.LectorDeIncompletosDe(servicios).Leer());
        Assert.AreEqual(
            0,
            segundaLectura.Quedan,
            "El documento volvió a la cola. Miguel cerró ese hueco y no tiene otra forma de sacarlo: "
            + "volvería a salirle cada vez que abriera la pantalla.");
    }

    /// <summary>
    /// Dado un documento sin ninguna persona leida, cuando Inicio arma sus listas, entonces
    /// NO sale como listo para asignar.
    /// </summary>
    /// <remarks>
    /// La sexta divergencia, vista desde la pantalla y no desde el modelo: a un documento sin
    /// personas no se le puede recomendar a nadie, y ofrecérselo al dueño para mandárselo a un
    /// compañero es mandarle una hoja sin nadie dentro.
    /// </remarks>
    [TestMethod]
    public void UnDocumentoSinNingunaPersonaNoSaleComoListoEnInicio()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var conNadie = BaseDeInicio.MeterCaso(
            servicios, "CASP2609", "2026-09-08", cuantasPersonas: 0);
        var conAlguien = BaseDeInicio.MeterCaso(
            servicios, "CASQ2609", "2026-09-08", cuantasPersonas: 1);

        var resumen = BaseDeInicio.LeerInicio(servicios);

        Assert.AreEqual(
            1,
            resumen.Contadores.ListoParaAsignar,
            "Solo el que trae persona está listo; al otro no hay a quién recomendarle.");
        Assert.AreEqual(conAlguien, resumen.Listos.Single().CasoId);
        Assert.DoesNotContain(conNadie, resumen.Listos.Select(r => r.CasoId));
    }

    /// <summary>La misma pregunta que hace la cola al avanzar, por el mismo camino.</summary>
    private static bool TodaviaLeFalta(ServiciosFalsos servicios, long casoId)
    {
        var caso = servicios.Casos.Obtener(casoId);
        Assert.IsNotNull(caso);
        var personas = servicios.Personas.DeCaso(casoId);
        var procedencias = ProcedenciasDeUnaPasada.DeUnDocumento(servicios.Procedencia, caso, personas);
        return !LoQueLeFalta.EstaListo(caso, personas, procedencias);
    }

    /// <summary>Cuantos campos hay firmados en toda la base inventada.</summary>
    private static int CuantasFirmasHay(ServiciosFalsos servicios)
        => servicios.Almacen.Procedencias.Values.Count(fila => fila.Verificado);

    /// <summary>El modelo de Correccion atado a los servicios falsos.</summary>
    private static ModeloDeCorreccion MontarCorreccion(ServiciosFalsos servicios)
        => new(
            servicios.Casos, servicios.Personas, servicios.Procedencia,
            servicios.LecturaDePdf, servicios.Extraccion, servicios.Reloj, servicios.Companeros);
}
