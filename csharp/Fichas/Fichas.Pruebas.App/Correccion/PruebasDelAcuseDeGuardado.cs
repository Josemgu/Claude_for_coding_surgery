using Fichas.App.Correccion;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Lo que dice el pie despues de guardar. Sin ventana, para poder leerlo.
/// </summary>
/// <remarks>
/// Dueno, 2026-09-04, literal: «El boton Guardar no funciona cuando se revisa y se coloca
/// la informacion en un lateral faltante: no hace nada». Medido entonces: el guardado SI
/// ocurria y en la pantalla no habia una sola palabra que lo dijera. Un boton que hace su
/// trabajo en silencio es, para quien lo mira, un boton roto.
/// <para>
/// ⚠️ <b>Y el 2026-09-04, por la tarde, la frase «sin guardar» paso a ser un defecto en
/// casi todos los casos.</b> QA midio sobre el paquete publicado que el pie decia
/// «Guardado · 1 campo sin guardar: Cedula (1)» por un valor que la pantalla se habia
/// negado a escribir, y eso contradice el requisito 9 del dueno y el texto de la
/// migracion 17. Un valor raro ENTRA. La frase se queda solo para cuando el almacen de
/// verdad no admite la escritura.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDelAcuseDeGuardado
{
    /// <summary>Dado un campo guardado, entonces el acuse dice la hora y cuantos.</summary>
    [TestMethod]
    public void GuardarUnCampoDiceLaHoraYLaCuenta()
    {
        var linea = TextoDelAcuse.Componer("11:07", 1, [], []);
        StringAssert.Contains(linea, "11:07", StringComparison.Ordinal);
        StringAssert.Contains(linea, "1 campo", StringComparison.Ordinal);
    }

    /// <summary>Varios campos van en plural.</summary>
    [TestMethod]
    public void VariosCamposVanEnPlural()
        => StringAssert.Contains(TextoDelAcuse.Componer("11:07", 3, [], []), "3 campos", StringComparison.Ordinal);

    /// <summary>Sin cambios tambien acusa recibo: el silencio es lo que el dueno rechazo.</summary>
    [TestMethod]
    public void SinCambiosTambienAcusaRecibo()
    {
        var linea = TextoDelAcuse.Componer("11:07", 0, [], []);
        Assert.AreNotEqual(string.Empty, linea);
        StringAssert.Contains(linea, "sin cambios", StringComparison.Ordinal);
    }

    /// <summary>
    /// Dado un campo que se guardo y no vale, el acuse dice que se guardo Y lo nombra.
    /// </summary>
    /// <remarks>
    /// Las dos cosas van juntas a proposito: decir solo «guardado» esconde lo que hay que
    /// mirar, y decir solo «hay que mirar esto» deja a Miguel sin saber si entro.
    /// </remarks>
    [TestMethod]
    public void UnCampoSenaladoSeNombraYAunAsiSeDiceQueSeGuardo()
    {
        var linea = TextoDelAcuse.Componer("11:07", 4, ["Cédula (1)"], []);
        StringAssert.Contains(linea, "Cédula (1)", StringComparison.Ordinal);
        StringAssert.Contains(linea, "4 campos", StringComparison.Ordinal);
        StringAssert.Contains(linea, "11:07", StringComparison.Ordinal);
        Assert.DoesNotContain("sin guardar", linea, "El campo SI se guardo: decir lo contrario es mentir.");
    }

    /// <summary>Los tres campos raros salen los tres nombrados, y los tres guardados.</summary>
    [TestMethod]
    public void LosTresCamposRarosSalenLosTresNombrados()
    {
        var linea = TextoDelAcuse.Componer("11:07", 6, ["Cédula", "N.º de unidad", "Fecha de viaje"], []);
        StringAssert.Contains(linea, "6 campos", StringComparison.Ordinal);
        StringAssert.Contains(linea, "Cédula", StringComparison.Ordinal);
        StringAssert.Contains(linea, "N.º de unidad", StringComparison.Ordinal);
        StringAssert.Contains(linea, "Fecha de viaje", StringComparison.Ordinal);
        Assert.DoesNotContain("sin guardar", linea);
    }

    /// <summary>
    /// «Sin guardar» se dice cuando —y solo cuando— el almacen no admitio la escritura.
    /// </summary>
    /// <remarks>
    /// Es lo que pasa con dos personas del mismo caso a las que se les pone la misma
    /// cedula: <c>UNIQUE (caso_id, mrn)</c> sigue en pie despues de la migracion 17. Ahi
    /// la hora estorba: lo que hay que mirar no es cuando se guardo sino que no entro.
    /// </remarks>
    [TestMethod]
    public void SinGuardarSoloCuandoElAlmacenNoAdmitio()
    {
        var linea = TextoDelAcuse.Componer("11:07", 2, [], ["Cédula (2)"]);
        StringAssert.Contains(linea, "1 campo sin guardar", StringComparison.Ordinal);
        StringAssert.Contains(linea, "Cédula (2)", StringComparison.Ordinal);
        Assert.DoesNotContain("11:07", linea, "Con algo sin guardar, la hora estorba.");
    }

    /// <summary>El acuse cabe siempre en una linea del pie: requisito 4 del dueno.</summary>
    /// <remarks>
    /// El caso peor no es el de cuatro etiquetas: es el del formulario de grupo del dueno,
    /// con diez personas y las veinte etiquetas senaladas a la vez. Ahi la linea se recorta
    /// y dice cuantas quedaron fuera, en vez de partirse en dos renglones.
    /// </remarks>
    [TestMethod]
    public void ElAcuseCabeEnUnaLinea()
    {
        string[] cuatro = ["Cédula", "N.º de unidad", "Fecha de viaje", "Nombre de unidad"];
        var veinte = Enumerable.Range(1, 10)
            .SelectMany(fila => new[] { $"Cédula ({fila})", $"Nombre ({fila})" })
            .ToArray();

        var lineas = new[]
        {
            TextoDelAcuse.Componer("11:07", 0, [], []),
            TextoDelAcuse.Componer("11:07", 12, [], []),
            TextoDelAcuse.Componer("11:07", 6, cuatro, []),
            TextoDelAcuse.Componer("11:07", 20, veinte, []),
            TextoDelAcuse.Componer("11:07", 0, [], veinte),
        };
        foreach (var linea in lineas)
        {
            Assert.DoesNotContain("\n", linea, $"«{linea}» trae un salto de linea.");
            Assert.IsLessThanOrEqualTo(120, linea.Length, $"«{linea}» mide {linea.Length} y el tope del pie son 120.");
        }
    }

    /// <summary>Recortada o no, la linea SIEMPRE nombra al menos el primer campo.</summary>
    /// <remarks>
    /// Recortar hasta no nombrar ninguno seria volver al silencio que el dueno rechazo,
    /// solo que con mas palabras.
    /// </remarks>
    [TestMethod]
    public void LaLineaRecortadaSigueNombrandoAlPrimeroYDiceCuantosFaltan()
    {
        var veinte = Enumerable.Range(1, 10)
            .SelectMany(fila => new[] { $"Cédula ({fila})", $"Nombre ({fila})" })
            .ToArray();
        var linea = TextoDelAcuse.Componer("11:07", 20, veinte, []);

        StringAssert.Contains(linea, "Cédula (1)", StringComparison.Ordinal);
        StringAssert.Contains(linea, "más", StringComparison.Ordinal);
    }

    /// <summary>La hora se saca del reloj del programa, no de DateTime.Now por su cuenta.</summary>
    [TestMethod]
    public void LaHoraSaleDelRelojDelPrograma()
    {
        Assert.AreEqual("12:00", TextoDelAcuse.HoraDe("2026-09-04 12:00:00"));
        Assert.AreEqual("09:41", TextoDelAcuse.HoraDe("2026-09-04T09:41:33"));
    }

    /// <summary>Un instante que no se entiende no tumba el acuse: se queda sin hora.</summary>
    /// <remarks>Requisito 9: avisar, nunca impedir. Y menos por una hora.</remarks>
    [TestMethod]
    public void UnInstanteRaroNoTumbaElAcuse()
    {
        Assert.AreEqual(string.Empty, TextoDelAcuse.HoraDe("no es una hora"));
        var linea = TextoDelAcuse.Componer(TextoDelAcuse.HoraDe("no es una hora"), 2, [], []);
        StringAssert.Contains(linea, "2 campos", StringComparison.Ordinal);
    }
}
