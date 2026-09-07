using Fichas.Lectura;

namespace Fichas.Pruebas.Lectura;

/// <summary>
/// El control negativo de las casillas de ordenanza (criterio C3b-4).
/// </summary>
/// <remarks>
/// ⛔ La lectura de casillas NO entra en esta fase: es la FASE C3b y esta BLOQUEADA por
/// C3b-0, la verdad conocida de las 42 casillas (7 documentos x 6), que la anota una
/// persona y hoy no existe. Aqui solo se deja la interfaz preparada y se vigila lo unico
/// que se puede vigilar sin calibrar: que una casilla no leida devuelva NULO y jamas 0.
/// </remarks>
[TestClass]
public class PruebaDeCasillasDeOrdenanza
{
    // El analizador avisa de que estas condiciones son constantes conocidas, y tiene
    // razon: LO SON. Eso es justo lo que se vigila. Son DECISIONES escritas en el codigo
    // —la lectura esta apagada, el umbral no se ha inventado— y esta prueba existe para
    // que cambiarlas rompa algo en vez de pasar desapercibido en una revision.
#pragma warning disable MSTEST0032
    [TestMethod]
    public void LaLecturaDeCasillasSigueApagadaYElUmbralSigueSinInventarse()
    {
        Assert.IsFalse(CasillasDeOrdenanza.LecturaActiva,
            "se enciende cuando haya 3 formularios con verdad conocida, no antes");
        Assert.IsNull(CasillasDeOrdenanza.UmbralDePixelOscuro,
            "un 0,15 puesto «de momento» se usa por accidente; un nulo no");
        Assert.AreEqual(0, CasillasDeOrdenanza.FormulariosConVerdadConocida);
        Assert.AreEqual(3, CasillasDeOrdenanza.FormulariosMinimosParaCalibrar);
    }
#pragma warning restore MSTEST0032

    [TestMethod]
    public void SonSeisCasillasYEnElOrdenDeLasColumnasDeLaTabla()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "ord_recibir_propias",
                "ord_observar_sellamiento",
                "ord_traductor",
                "ord_investidura",
                "ord_sellamiento_esposos",
                "ord_sellamiento_hijo_padres",
            },
            CasillasDeOrdenanza.Nombres.ToArray());
    }

    /// <summary>
    /// La regla que no se puede romper: 0 significa «se leyo y no estaba marcada»;
    /// nulo significa «no se leyo». Seis ceros en silencio dejarian a Miguel seis
    /// ordenanzas negativas en firme sin motivo para mirar el papel.
    /// </summary>
    [TestMethod]
    public void UnaCasillaNoLeidaDevuelveNuloYJamasCero()
    {
        var casillas = CasillasDeOrdenanza.NoLeidas();

        Assert.HasCount(6, casillas);
        foreach (var (nombre, valor) in casillas)
        {
            Assert.IsNull(valor, $"«{nombre}» devolvio un valor y la lectura esta apagada");
        }
    }
}
