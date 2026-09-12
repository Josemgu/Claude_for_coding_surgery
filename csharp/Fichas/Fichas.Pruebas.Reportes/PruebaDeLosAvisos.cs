using Fichas.Contratos.Modelos;
using Fichas.Reportes.Consultas;
using Fichas.Reportes.Reglas;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// Los avisos del reporte, y los DOS defectos que el pase manda arreglar al portar.
/// </summary>
/// <remarks>
/// ⚠️ La premisa del pase se midio antes de escribir esto y no se sostiene entera:
///
///   • «reportes/avisos.py:64 hace sorted({numero_caso}) y revienta»: la linea 64 es
///     texto de la cadena de documentacion. El `sorted` de verdad esta en la linea 80
///     y YA lleva `or SIN_NUMERO_DE_CASO`. El defecto esta arreglado en el Python.
///     Aqui se porta arreglado, y esta prueba lo fija para que no vuelva.
///   • «avisos.py:100 tiene un “1 personas”»: el archivo tiene 97 lineas. El defecto
///     SI existe, en la linea 83, y esta prueba es la que lo cierra.
/// </remarks>
[TestClass]
public class PruebaDeLosAvisos
{
    /// <summary>Una persona anotada como que no pudo viajar, en un caso con ese número (o sin él).</summary>
    /// <param name="numeroCaso">El número del caso; nulo para probar el caso sin número.</param>
    /// <param name="personaId">El id, que se usa también como id del caso.</param>
    private static PersonaConSuCaso Fila(string? numeroCaso, long personaId)
        => new(
            new Persona { Id = personaId, CasoId = personaId, Nombre = "Ana Anonimo", PudoViajar = false },
            new Caso { Id = personaId, NumeroCaso = numeroCaso });

    /// <summary>Vigila que un caso sin número no tumba el aviso y sale como «(sin número de caso)» junto a los demás.</summary>
    [TestMethod]
    public void UnCasoSinNumeroNoRevientaElAvisoYSaleConSuPalabra()
    {
        var sinFecha = new[] { Fila(null, 1), Fila("CASP2609", 2) };

        var aviso = Avisos.DeLosQueNoCabenEnElPeriodo(sinFecha);

        Assert.IsNotNull(aviso);
        StringAssert.Contains(aviso, Avisos.SinNumeroDeCaso);
        StringAssert.Contains(aviso, "CASP2609");
    }

    /// <summary>Vigila que la palabra del caso sin número no se repite aunque haya dos.</summary>
    [TestMethod]
    public void DosCasosSinNumeroSalenUnaSolaVez()
    {
        var aviso = Avisos.DeLosQueNoCabenEnElPeriodo([Fila(null, 1), Fila(null, 2)]);

        Assert.IsNotNull(aviso);
        var veces = aviso!.Split(Avisos.SinNumeroDeCaso).Length - 1;
        Assert.AreEqual(1, veces, "El conjunto de numeros de caso no puede repetir la misma palabra.");
    }

    /// <summary>Vigila que el «1 personas» del Python no vuelve: con una, singular.</summary>
    [TestMethod]
    public void ConUnaSolaPersonaElAvisoHablaEnSingular()
    {
        var aviso = Avisos.DeLosQueNoCabenEnElPeriodo([Fila("CASP2609", 1)]);

        Assert.IsNotNull(aviso);
        Assert.DoesNotContain("1 personas", aviso!, $"Sigue diciendo «1 personas»: {aviso}");
        StringAssert.StartsWith(aviso, "1 persona anotada");
        StringAssert.Contains(aviso, "no pudo viajar");
    }

    /// <summary>Vigila que con dos personas el aviso va en plural.</summary>
    [TestMethod]
    public void ConDosPersonasElAvisoHablaEnPlural()
    {
        var aviso = Avisos.DeLosQueNoCabenEnElPeriodo([Fila("CASP2609", 1), Fila("CASD2610", 2)]);

        Assert.IsNotNull(aviso);
        StringAssert.StartsWith(aviso, "2 personas anotadas");
        StringAssert.Contains(aviso, "no pudieron viajar");
    }

    /// <summary>Vigila que con la lista vacía el aviso es nulo, no una frase con cero.</summary>
    [TestMethod]
    public void SinNadieSinFechaNoHayAviso()
        => Assert.IsNull(Avisos.DeLosQueNoCabenEnElPeriodo([]));

    /// <summary>Vigila que el aviso se apagó solo desde que «completa» resuelve (2026-09-03).</summary>
    [TestMethod]
    public void ElAvisoDeLaRecomendacionCompletaNoSaleYaQueCompletaResuelve()
    {
        // ESTADOS_QUE_RESUELVEN dejo de estar vacia el 2026-09-03: lleva «completa»
        // (DECISIONES.md). El aviso nacio de una medicion y se apaga solo cuando la
        // causa desaparece; aqui ya desaparecio.
        Assert.IsNull(Avisos.DeLaRecomendacionCompleta());
    }

    /// <summary>Vigila que el aviso abre con «N de los M casos», con el denominador delante.</summary>
    [TestMethod]
    public void ElAvisoDeLosCasosSinEstadoDiceElDenominador()
    {
        var deteccion = new Deteccion(
            CasosDelPeriodo: 40, ConProblemaRegistrado: 5, DetectadosATiempo: 3,
            DetectadosDespuesDelViaje: 2, ConProblemaSinFechaDeDeteccion: 0,
            SinEstadoRegistrado: 12, ConRecomendacionResuelta: 23);

        var aviso = Avisos.DeLosCasosSinEstado(deteccion);

        Assert.IsNotNull(aviso);
        StringAssert.StartsWith(aviso, "12 de los 40 casos");
    }

    /// <summary>Vigila que con cero casos sin estado el aviso es nulo.</summary>
    [TestMethod]
    public void SinCasosSinEstadoNoHayAviso()
    {
        var deteccion = new Deteccion(10, 0, 0, 0, 0, 0, 10);
        Assert.IsNull(Avisos.DeLosCasosSinEstado(deteccion));
    }
}
