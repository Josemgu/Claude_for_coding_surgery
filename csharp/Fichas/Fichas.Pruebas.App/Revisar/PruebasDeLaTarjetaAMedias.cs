using System.Xml.Linq;
using Fichas.App.Revisar;
using Fichas.App.Vocabulario;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// En Revisar, el documento con alguna persona A MEDIAS —alguna de las seis en sí, no las
/// seis— se ve naranja, sigue diciendo «me falta» y su detalle nombra a quién y qué le falta.
/// </summary>
/// <remarks>
/// <para><b>Palabras del dueño, 2026-09-16:</b> <i>«Las personas que se han completado, por
/// ejemplo 4 preguntas de las 6, deben pasar a color naranja e indicar que le falta; el nombre
/// y la unidad son datos importantes que deben ser más visibles»</i>.</para>
///
/// <para><b>Lo medido antes de tocar nada:</b> <c>TableroDeRevisar</c> solo pedía CUÁNTAS
/// personas tiene cada documento (<c>ICasos.ContarPersonasDe</c>), así que la tarjeta no podía
/// saber cómo iban sus seis; y el nombre de las personas no salía en la tarjeta.</para>
///
/// <para>⚠️ La tarjeta solo sabe de sus personas si el tablero se monta con el puerto de
/// personas. Sin él —como lo montan otras pruebas— se lee como hasta hoy, sin naranja y sin
/// nombres, y esta clase lo fija para que no se rompa a nadie por debajo.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaTarjetaAMedias
{
    /// <summary>Un banco con el tablero montado como lo monta la pantalla: con las personas.</summary>
    /// <param name="banco">El banco de carpetas ya montado.</param>
    private static TableroDeRevisar TableroConPersonas(BancoDeCarpetas banco)
        => new(banco.Servicios.Casos, banco.Servicios.Asignaciones, banco.Servicios.Companeros, banco.Reloj, banco.Servicios.Personas);

    /// <summary>Contesta las seis de una persona en la base falsa, tal cual.</summary>
    /// <param name="banco">Donde vive la persona.</param>
    /// <param name="personaId">Su número interno.</param>
    /// <param name="seis">Las seis respuestas, en el orden de la pantalla del líder.</param>
    private static void Contestar(BancoDeCarpetas banco, long personaId, params bool?[] seis)
    {
        var persona = banco.Servicios.Almacen.Personas[personaId];
        banco.Servicios.Almacen.Personas[personaId] = persona with
        {
            PasoPreparacion = seis[0],
            PasoInformacion = seis[1],
            PasoCitaDelTemplo = seis[2],
            PasoAccionesRequeridas = seis[3],
            PasoEntrevistas = seis[4],
            PasoListoParaElTemplo = seis[5],
        };
    }

    /// <summary>Un documento con una persona con 4 de 6 en sí: naranja, «me falta · a medias», y el detalle lo dice.</summary>
    [TestMethod]
    public void UnDocumentoConUnaPersonaCon4De6SeVeAMediasYSuDetalleDiceQueLeFalta()
    {
        var banco = new BancoDeCarpetas();
        var caso = banco.Meter("CASP2609", "2026-09-20", "7000011", "Castries Branch");
        var ana = banco.MeterPersona(caso, "Ana Pérez", "055-1111-3853");
        Contestar(banco, ana, true, true, true, true, null, null);

        var tablero = TableroConPersonas(banco);
        tablero.Cargar();
        var tarjeta = tablero.De(caso)!;

        Console.WriteLine($"«{tarjeta.PalabraDelEstado}» · «{tarjeta.NotaDeLaPastilla}» · «{tarjeta.DetalleDelEstado}»");
        Assert.AreEqual(DosEstados.MeFalta, tarjeta.PalabraDelEstado);
        Assert.IsTrue(tarjeta.SeVeAMedias);
        Assert.IsFalse(tarjeta.SeVeResuelto);
        Assert.IsTrue(tarjeta.SeVeMeFalta, "La pastilla «me falta» sigue encendida: a medias es un matiz, no otra palabra.");
        Assert.AreEqual(DosEstados.NotaDeAMedias, tarjeta.NotaDeLaPastilla);
        StringAssert.Contains(tarjeta.DetalleDelEstado, "Ana Pérez: le faltan 2 de 6: Entrevistas, Listo para el templo");
    }

    /// <summary>Con ninguna en sí no hay naranja: se lee como hoy.</summary>
    [TestMethod]
    public void UnDocumentoSinNingunaEnSiNoSeVeAMedias()
    {
        var banco = new BancoDeCarpetas();
        var caso = banco.Meter("CASP2609", "2026-09-20", "7000011", "Castries Branch");
        banco.MeterPersona(caso, "Ana Pérez", "055-1111-3853");

        var tablero = TableroConPersonas(banco);
        tablero.Cargar();
        var tarjeta = tablero.De(caso)!;

        Assert.IsFalse(tarjeta.SeVeAMedias);
        Assert.AreEqual(string.Empty, tarjeta.NotaDeLaPastilla);
    }

    /// <summary>La tarjeta a medias sigue entrando en el tablero «Me falta», que es el de partida.</summary>
    [TestMethod]
    public void LaTarjetaAMediasEntraEnElTableroMeFalta()
    {
        var banco = new BancoDeCarpetas();
        var caso = banco.Meter("CASP2609", "2026-09-20", "7000011", "Castries Branch");
        var ana = banco.MeterPersona(caso, "Ana Pérez", "055-1111-3853");
        Contestar(banco, ana, true, null, null, null, null, null);

        var tablero = TableroConPersonas(banco);
        tablero.Cargar();

        Assert.AreEqual(1, tablero.CuantasEn(FiltroDeTarjeta.MeFalta));
        Assert.AreEqual(0, tablero.CuantasEn(FiltroDeTarjeta.Resuelto));
        Assert.AreEqual(1, tablero.CuantasEn(FiltroDeTarjeta.Todo));
    }

    /// <summary>Un duplicado a medias se ve rojo de duplicado, no naranja: el duplicado manda.</summary>
    [TestMethod]
    public void UnDuplicadoAMediasSeVeDeDuplicadoYNoNaranja()
    {
        var banco = new BancoDeCarpetas();
        var original = banco.Meter("CASP2609", "2026-09-20", "7000011", "Castries Branch");
        var copia = banco.Meter("CASP2609", "2026-09-20", "7000011", "Castries Branch", rutaPdf: @"C:\pdf\copia.pdf");
        banco.Servicios.Almacen.Casos[copia] = banco.Servicios.Almacen.Casos[copia] with { DuplicadoDe = original };
        var ana = banco.MeterPersona(copia, "Ana Pérez", "055-1111-3853");
        Contestar(banco, ana, true, true, null, null, null, null);

        var tablero = TableroConPersonas(banco);
        tablero.Cargar();
        var tarjeta = tablero.De(copia)!;

        Assert.IsTrue(tarjeta.EsDuplicado);
        Assert.IsTrue(tarjeta.Lectura.AMedias, "La lectura sí está a medias…");
        Assert.IsFalse(tarjeta.SeVeAMedias, "…pero la capa naranja no se enciende encima de la roja del duplicado.");
    }

    /// <summary>Los nombres de las personas salen en la tarjeta, en el orden del formulario.</summary>
    [TestMethod]
    public void LaTarjetaDiceLosNombresDeSusPersonas()
    {
        var banco = new BancoDeCarpetas();
        var caso = banco.Meter("CASP2609", "2026-09-20", "7000011", "Castries Branch");
        banco.MeterPersona(caso, "Ana Pérez", "055-1111-3853");
        banco.MeterPersona(caso, "Luis Gómez", "055-1111-3854");

        var tablero = TableroConPersonas(banco);
        tablero.Cargar();
        var tarjeta = tablero.De(caso)!;

        Assert.AreEqual("Ana Pérez, Luis Gómez", tarjeta.NombresDeLasPersonas);
        Assert.IsTrue(tarjeta.TieneNombres);
        Assert.AreEqual("7000011 · Castries Branch", tarjeta.UnidadQueSeLee);
    }

    /// <summary>Sin el puerto de personas, la tarjeta se lee como hasta hoy: sin naranja y sin nombres.</summary>
    [TestMethod]
    public void SinElPuertoDePersonasLaTarjetaSeLeeComoHoy()
    {
        var banco = new BancoDeCarpetas();
        var caso = banco.Meter("CASP2609", "2026-09-20", "7000011", "Castries Branch");
        var ana = banco.MeterPersona(caso, "Ana Pérez", "055-1111-3853");
        Contestar(banco, ana, true, true, true, true, null, null);

        var tarjeta = banco.ComoLoVeLaPantalla().Single();

        Assert.IsFalse(tarjeta.SeVeAMedias);
        Assert.IsFalse(tarjeta.TieneNombres);
        Assert.AreEqual(DosEstados.MeFalta, tarjeta.PalabraDelEstado);
    }

    /// <summary>
    /// Toda la base inventada, con personas: cada tarjeta a medias tiene alguna persona con
    /// alguna en sí y ninguna lectura resuelta; y hay tarjetas a medias, o no se probó nada.
    /// </summary>
    [TestMethod]
    public void EnTodaLaBaseInventadaAMediasEsExactamenteLoQueDiceLaRegla()
    {
        var servicios = new ServiciosFalsos(300, 20260916, new RelojFijo("2026-09-16"));
        var tablero = new TableroDeRevisar(servicios.Casos, servicios.Asignaciones, servicios.Companeros, servicios.Reloj, servicios.Personas);
        tablero.Cargar();

        var aMedias = 0;
        foreach (var tarjeta in tablero.Todas(FiltroDeTarjeta.Todo))
        {
            var avanzo = tarjeta.PersonasLeidas.Any(p => p.Lectura.AMedias || p.Lectura.EsResuelto);
            var esperado = tarjeta.Lectura.EsMeFalta && avanzo;
            Assert.AreEqual(esperado, tarjeta.Lectura.AMedias, $"{tarjeta.NumeroDeCaso} · {tarjeta.Archivo}");
            if (tarjeta.Lectura.AMedias) aMedias++;
        }

        Console.WriteLine($"{tablero.Total} tarjetas · {aMedias} a medias");
        Assert.IsGreaterThan(0, aMedias);
    }

    /// <summary>
    /// La capa naranja de la tarjeta lleva la palabra al lado: el color nunca va solo (mockup v2).
    /// </summary>
    /// <remarks>
    /// Se mira el XAML porque es donde se rompería: la capa se enciende con <c>SeVeAMedias</c> y
    /// la nota «a medias» va en la pastilla, con <c>NotaDeLaPastilla</c>. Si alguien quita una
    /// de las dos, esta prueba se pone roja.
    /// </remarks>
    [TestMethod]
    public void LaCapaNaranjaYLaNotaEstanLasDosEnLaPantalla()
    {
        var xaml = File.ReadAllText(LaPantallaDeRevisar());

        StringAssert.Contains(xaml, $"{{x:Bind {nameof(TarjetaDeDocumento.SeVeAMedias)}}}");
        StringAssert.Contains(xaml, $"{{x:Bind {nameof(TarjetaDeDocumento.NotaDeLaPastilla)}}}");
        StringAssert.Contains(xaml, "PinturaDeInicio.NaranjaFondo");
        StringAssert.Contains(xaml, "PinturaDeInicio.NaranjaMarca");
        StringAssert.Contains(xaml, $"{{x:Bind {nameof(TarjetaDeDocumento.NombresDeLasPersonas)}}}");
        StringAssert.Contains(xaml, $"{{x:Bind {nameof(TarjetaDeDocumento.UnidadQueSeLee)}}}");
    }

    /// <summary>La ruta del XAML de Revisar, desde donde corre la prueba.</summary>
    private static string LaPantallaDeRevisar()
    {
        var raiz = AppContext.BaseDirectory;
        while (raiz is not null && !File.Exists(Path.Combine(raiz, "Fichas.sln")))
            raiz = Path.GetDirectoryName(raiz);
        Assert.IsNotNull(raiz, "No encuentro Fichas.sln subiendo desde la carpeta de la prueba.");
        return Path.Combine(raiz, "Fichas.App", "Revisar", "PaginaDeRevisar.xaml");
    }
}
