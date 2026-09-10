using ClosedXML.Excel;
using Fichas.Paquetes;

namespace Fichas.Pruebas.Paquetes;

/// <summary>
/// Ninguna celda del paquete va bloqueada, y la hoja no va protegida.
/// </summary>
/// <remarks>
/// <para><b>Lo pidió el dueño el 2026-09-07:</b> <i>«No bloquees las celdas por favor, de los
/// paquetes.»</i> Manda él.</para>
///
/// <para><b>Qué protegía el bloqueo y por qué se puede quitar, medido.</b> Iban bloqueadas
/// <c>numero_caso</c> y <c>mrn</c> porque eran el par que reconciliaba la vuelta. Desde el
/// 2026-09-03 la hoja lleva la columna <c>clave</c>, y
/// <c>Reconciliacion.ParDeLaFila</c> casa POR LA CLAVE cuando la hoja la trae: el caso y el MRN
/// tecleados ya no deciden nada. Solo se cae al par suelto cuando la hoja no trae la columna
/// <c>clave</c>, y eso se mide aparte en <see cref="PruebasDeLaClaveEstropeada"/>.</para>
///
/// <para>⚠️ <b>Se comprueban las dos mitades, y hacen falta las dos.</b> En OOXML una celda
/// marcada «bloqueada» no impide nada mientras la hoja no esté protegida — y al revés, una hoja
/// protegida bloquea TODAS las celdas que no digan explícitamente lo contrario, porque el valor
/// por defecto de Excel es «bloqueada». Dejar las marcas puestas y quitar solo la protección
/// sería una trampa: el día que alguien proteja la hoja, vuelve el bloqueo sin que nadie lo
/// haya decidido.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeQueNingunaCeldaVaBloqueada
{
    /// <summary>Dado el paquete generado, cuando se mira la hoja, no está protegida.</summary>
    [TestMethod]
    public void LaHojaNoVaProtegida()
        => Assert.IsFalse(
            Hoja().Protection.IsProtected,
            "el dueño pidió que no se bloqueen las celdas de los paquetes");

    /// <summary>Ni una sola celda de datos queda marcada como bloqueada. Ni una.</summary>
    /// <remarks>
    /// Se recorren TODAS las columnas y no una muestra: la que se quedara bloqueada sería
    /// justo la que el compañero no puede tocar, y no hay forma de adivinar cuál va a
    /// necesitar.
    /// </remarks>
    [TestMethod]
    public void NingunaCeldaDeDatosQuedaBloqueada()
    {
        var hoja = Hoja();
        var bloqueadas = new List<string>();
        for (var numero = 1; numero <= Columnas.Todas.Count; numero++)
        {
            if (hoja.Cell(Columnas.PrimeraFilaDeDatos, numero).Style.Protection.Locked)
                bloqueadas.Add(Columnas.Todas[numero - 1].Nombre);
        }

        Assert.IsEmpty(bloqueadas, "quedaron bloqueadas: " + string.Join(", ", bloqueadas));
    }

    /// <summary>Las dos que estaban bloqueadas hasta el 2026-09-07, nombradas una a una.</summary>
    /// <remarks>
    /// Van con nombre además de entrar en el barrido de arriba: son las dos que el pase
    /// nombra, y una prueba que solo cuenta no dice cuál se quedó fuera si alguien las
    /// vuelve a bloquear.
    /// </remarks>
    [TestMethod]
    public void ElCasoYLaCedulaDeMiembroSePuedenTeclear()
    {
        var hoja = Hoja();

        Assert.IsFalse(hoja.Cell(7, Columnas.IndiceDe("numero_caso")).Style.Protection.Locked);
        Assert.IsFalse(hoja.Cell(7, Columnas.IndiceDe("mrn")).Style.Protection.Locked);
        Assert.IsFalse(hoja.Cell(7, Columnas.IndiceDe(Columnas.ColumnaDeLaClave)).Style.Protection.Locked);
    }

    /// <summary>Tampoco la fila de títulos: la hoja entera queda libre.</summary>
    [TestMethod]
    public void LaFilaDeTitulosTampocoVaBloqueada()
    {
        var hoja = Hoja();
        for (var numero = 1; numero <= Columnas.Todas.Count; numero++)
        {
            Assert.IsFalse(
                hoja.Cell(Columnas.FilaDeLaCabecera, numero).Style.Protection.Locked,
                $"el título de «{Columnas.Todas[numero - 1].Nombre}» quedó bloqueado");
        }
    }

    /// <summary>
    /// ⚠️ Que no haya bloqueo NO cambia la defensa de verdad: la vuelta sigue casando por la
    /// clave, no por lo que se teclee en el caso o en el MRN.
    /// </summary>
    /// <remarks>
    /// Es la prueba que sostiene la decisión entera. Sin ella, «quitamos el bloqueo» sería un
    /// cambio de estilo sin nadie que responda de lo que protegía.
    /// </remarks>
    [TestMethod]
    public void ElParQueReconciliaSigueDeclaradoAunqueYaNoSeBloquee()
        => CollectionAssert.AreEqual(
            new[] { "numero_caso", "mrn" },
            Columnas.Todas.Where(columna => columna.EsClave).Select(columna => columna.Nombre).ToArray(),
            "siguen siendo el par de respaldo cuando la hoja no trae la columna «clave»");

    /// <summary>Una hoja de una fila, escrita por el motor de verdad.</summary>
    private static IXLWorksheet Hoja()
        => LibroDeTrabajo.Construir(
            [
                new FilaDeTrabajo
                {
                    NumeroCaso = "BALC2609",
                    FechaViaje = "2026-10-15",
                    Templo = "Santo Domingo",
                    UnidadNombre = "Cuatricentenaria",
                    UnidadNumero = "7000014",
                    Nombre = "Persona de prueba A",
                    Mrn = "055-1111-3853",
                    AQueVa = "Investidura",
                    Clave = Columnas.ArmarLaClave("BALC2609", "055-1111-3853", 12),
                },
            ],
            "Agente de prueba").Worksheet(Columnas.NombreDeLaHoja);
}
