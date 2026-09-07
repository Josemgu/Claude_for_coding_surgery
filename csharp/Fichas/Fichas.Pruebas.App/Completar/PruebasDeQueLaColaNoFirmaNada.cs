using Fichas.App.Completar;
using Fichas.Pruebas.App.Inicio;

namespace Fichas.Pruebas.App.Completar;

/// <summary>
/// La cola encadena y NADA MAS: ni una firma sale de ella.
/// </summary>
/// <remarks>
/// <para>⛔ <b>Esta clase existe para vigilar la regla permanente 5 de <c>CLAUDE.md</c></b>,
/// que la entrada del 2026-09-06 repite al pie del encargo: <i>«La cola encadena y NADA
/// MÁS. No firma campos, no marca nada como verificado, no da nada por bueno»</i>.</para>
///
/// <para>Se mira el ALMACEN y no la pantalla, que es la unica forma de comprobarlo: una
/// pantalla puede decir «listo para asignar» sin haber escrito nada, y una firma se escribe
/// en <c>procedencia_campo.verificado</c> aunque no se pinte en ningun sitio. Lo que aqui
/// se cuenta son filas verificadas antes y despues.</para>
///
/// <para><b>Y la distincion que sostiene todo esto</b>, del 2026-09-05: «listo para
/// asignar» es una LECTURA del estado —¿le falta algo a este documento?— que contesta el
/// programa; <c>verificado = 1</c> es la firma de Miguel y nunca es automatica. Son dos
/// cosas y no se mezclan.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeQueLaColaNoFirmaNada
{
    /// <summary>
    /// Rellenar lo que falta y guardar desde la cola no deja ni una firma nueva.
    /// </summary>
    /// <remarks>
    /// Es la medicion que el pase pide aparte: <i>«ningún caso quedó con firma que yo no
    /// diera — enséñalo consultando la base, no la pantalla»</i>. Aqui se hace sobre el
    /// almacen falso; con la ventana abierta se repite sobre el SQLite.
    /// </remarks>
    [TestMethod]
    public void RellenarYGuardarDesdeLaColaNoDejaNingunaFirma()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "HUEC2609", "2026-09-08", temploNombre: null);
        BancoDeLaCola.DarleLaProcedenciaDeLaImportacion(servicios, casoId);

        var antes = BancoDeLaCola.CuantasFirmasHayEn(servicios);

        var modelo = BancoDeLaCola.ModeloDe(servicios);
        modelo.Cargar(casoId);
        var resultado = modelo.Guardar(BancoDeLaCola.RellenarElTemplo(modelo));

        Assert.IsTrue(resultado.ListoParaAsignar, "Con el templo puesto ya no le falta nada.");
        Assert.AreEqual(antes, BancoDeLaCola.CuantasFirmasHayEn(servicios),
            "Guardar NO firma: la regla permanente 5.");
    }

    /// <summary>
    /// Que la cola diga «listo para asignar» no escribe nada en el almacen.
    /// </summary>
    /// <remarks>
    /// La frase es una lectura, no un estado nuevo: no hay columna donde escribirla y no se
    /// escribe en ninguna. Si el dia de manana alguien le buscara sitio en la base, esta
    /// prueba se pondria roja, que es justo lo que tiene que pasar.
    /// </remarks>
    [TestMethod]
    public void DecirListoParaAsignarNoEscribeNadaEnElAlmacen()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "LLEN2609", "2026-09-08");

        var casoAntes = servicios.Casos.Obtener(casoId);
        var procedenciasAntes = servicios.Almacen.Procedencias.Count;

        var cola = ColaDeCompletar.Desde(BaseDeInicio.LectorDeIncompletosDe(servicios).Leer());
        var _ = cola.SacarYDecirCualToca(casoId, __ => false);

        Assert.AreEqual(casoAntes, servicios.Casos.Obtener(casoId), "El caso queda exactamente igual.");
        Assert.HasCount(procedenciasAntes, servicios.Almacen.Procedencias, "Ni una fila de procedencia nueva.");
    }

    /// <summary>
    /// Encadenar tres documentos de la cola no firma ni uno de sus campos, y la cola se vacia.
    /// </summary>
    /// <remarks>
    /// La prueba anterior mira un guardado; esta mira el FLUJO entero, que es lo que el
    /// dueno va a usar. Un encadenado que firmara «solo al pasar de largo» daria por bueno
    /// lo que nadie miro, con la fecha de viaje encima.
    /// </remarks>
    [TestMethod]
    public void EncadenarTresDocumentosNoFirmaNingunoYVaciaLaCola()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        foreach (var numero in new[] { "PRIM2609", "SEGU2609", "TERC2609" })
        {
            var id = BaseDeInicio.MeterCaso(servicios, numero, "2026-09-08", temploNombre: null);
            BancoDeLaCola.DarleLaProcedenciaDeLaImportacion(servicios, id);
        }

        var antes = BancoDeLaCola.CuantasFirmasHayEn(servicios);
        var cola = ColaDeCompletar.Desde(BaseDeInicio.LectorDeIncompletosDe(servicios).Leer());
        Assert.AreEqual(3, cola.Quedan, "Los tres entran en la cola: a los tres les falta el templo.");

        var actual = cola.Primero;
        var recorridos = 0;
        while (actual is long cual)
        {
            var modelo = BancoDeLaCola.ModeloDe(servicios);
            modelo.Cargar(cual);
            var resultado = modelo.Guardar(BancoDeLaCola.RellenarElTemplo(modelo));

            // El mensaje dice CUALES siguen dudosos y no solo que fallo: sin eso, esta
            // prueba en rojo obliga a montar la base a mano para saber que campo era.
            Assert.IsTrue(
                resultado.ListoParaAsignar,
                $"El documento {cual} quedó sin huecos. Siguen dudosos: "
                + string.Join(", ", modelo.Dudosos.Select(c => $"{c.EtiquetaCompleta}=«{modelo.ValorDe(c)}»")));
            recorridos++;

            actual = cola.SacarYDecirCualToca(cual, LeFaltaAlgo(servicios));
        }

        Assert.AreEqual(3, recorridos, "Los tres se recorren sin volver atrás.");
        Assert.IsTrue(cola.EstaVacia, "La cola se vacía sola.");
        Assert.AreEqual(antes, BancoDeLaCola.CuantasFirmasHayEn(servicios),
            "Tres documentos encadenados, cero firmas.");
    }

    /// <summary>
    /// Uno que se guarda SIN taparle el hueco no sale de la cola, y tampoco firma nada.
    /// </summary>
    /// <remarks>
    /// Es la otra mitad del criterio del dueno: <i>«abro otro, lo dejo incompleto a
    /// propósito, guardo, y ese sigue en la cola»</i>. Sin esta prueba, una cola que sacara
    /// todo lo que se guarda pasaria las demas.
    /// </remarks>
    [TestMethod]
    public void GuardarSinTaparElHuecoNiSacaDeLaColaNiFirma()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "HUEC2609", "2026-09-08", temploNombre: null);
        BancoDeLaCola.DarleLaProcedenciaDeLaImportacion(servicios, casoId);

        var antes = BancoDeLaCola.CuantasFirmasHayEn(servicios);
        var cola = ColaDeCompletar.Desde(BaseDeInicio.LectorDeIncompletosDe(servicios).Leer());

        var modelo = BancoDeLaCola.ModeloDe(servicios);
        modelo.Cargar(casoId);
        var resultado = modelo.Guardar();

        Assert.IsFalse(resultado.ListoParaAsignar, "Sigue faltándole el templo.");
        Assert.AreEqual(casoId, cola.DejarYDecirCualToca(casoId), "Sigue tocando el mismo.");
        Assert.AreEqual(1, cola.Quedan, "No sale de la cola.");
        Assert.AreEqual(antes, BancoDeLaCola.CuantasFirmasHayEn(servicios), "Y no firma nada.");
    }

    /// <summary>Pregunta al almacen si a ese documento TODAVIA le falta algun dato.</summary>
    private static Func<long, bool> LeFaltaAlgo(Fichas.Datos.Falso.ServiciosFalsos servicios)
        => casoId =>
        {
            var caso = servicios.Casos.Obtener(casoId);
            if (caso is null) return false;

            var personas = servicios.Personas.DeCaso(casoId);
            var procedencias = Fichas.App.Grupo.ProcedenciasDeUnaPasada.DeUnDocumento(
                servicios.Procedencia, caso, personas);
            return !Fichas.App.Grupo.LoQueLeFalta.EstaListo(caso, personas, procedencias);
        };
}
