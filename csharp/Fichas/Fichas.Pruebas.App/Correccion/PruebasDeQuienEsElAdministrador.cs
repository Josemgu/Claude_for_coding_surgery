using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Quien es el administrador, y que pasa cuando no se sabe.
/// </summary>
/// <remarks>
/// ⛔ <b>El defecto que estas pruebas cierran esta MEDIDO, no supuesto.</b>
/// <c>AccionesDeRevisar.QuienFirmaAMano</c> coge el companero activo llamado «Miguel» y,
/// si no lo hay, <b>el primer activo</b>. En la base viva del dueno la tabla
/// <c>companeros</c> tiene UNA fila, «Sandy» (ADR-0005 §6.3). Con esa regla, el atajo del
/// administrador quedaria firmado por Sandy y el reporte diria que Sandy completo algo que
/// no toco.
/// <para>
/// La regla que se fija aqui: el administrador es el <b>unico</b> companero activo con
/// <see cref="RolDeCompanero.Administrador"/>. Si no hay exactamente uno, no hay atajo y se
/// dice por que en una linea. <b>Nunca se firma a nombre de quien no fue.</b>
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDeQuienEsElAdministrador
{
    /// <summary>Un companero activo con el rol que se le pase.</summary>
    private static Companero Uno(long id, string nombre, RolDeCompanero rol)
        => new() { Id = id, Nombre = nombre, Activo = true, Rol = rol, CreadoEn = "2026-09-05" };

    // ---- el defecto medido: la base del dueno tiene solo a Sandy --------

    /// <summary>
    /// Dada la base del dueno tal cual —una fila, «Sandy», sin rol de administrador—,
    /// cuando se pregunta quien es el administrador, entonces no hay ninguno.
    /// </summary>
    [TestMethod]
    public void ConSoloSandyEnLaBaseNoHayAdministrador()
    {
        var activos = new[] { Uno(1, "Sandy", RolDeCompanero.Companero) };
        Assert.IsNull(ElAdministrador.De(activos));
    }

    /// <summary>
    /// Y lo que se dice entonces nombra el alta, no a Sandy.
    /// </summary>
    /// <remarks>
    /// El dueno ya dio media respuesta el 2026-09-04: «el se anade a si mismo y firma con su
    /// nombre». La linea tiene que llevarle ahi, y no puede nombrar a nadie mas.
    /// </remarks>
    [TestMethod]
    public void SinAdministradorLaLineaPideElAltaYNoNombraANadieMas()
    {
        var activos = new[] { Uno(1, "Sandy", RolDeCompanero.Companero) };
        var aviso = ElAdministrador.PorQueNoSePuede(activos);

        Assert.AreEqual(GravedadDeAviso.Problema, aviso.Gravedad);
        Assert.DoesNotContain("Sandy", aviso.Linea, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("administrador", aviso.Linea, StringComparison.OrdinalIgnoreCase);
    }

    // ---- el caso bueno --------------------------------------------------

    /// <summary>Con un solo administrador activo, ese es, y se le nombra.</summary>
    [TestMethod]
    public void ConUnSoloAdministradorEsEse()
    {
        var activos = new[]
        {
            Uno(1, "Sandy", RolDeCompanero.Companero),
            Uno(2, "Miguel", RolDeCompanero.Administrador),
            Uno(3, "Rosa", RolDeCompanero.Gerente),
        };

        var quien = ElAdministrador.De(activos);
        Assert.IsNotNull(quien);
        Assert.AreEqual(2, quien.Id);
        Assert.AreEqual("Miguel", quien.Nombre);
    }

    /// <summary>
    /// Un administrador que NO se llama Miguel sigue siendo el administrador.
    /// </summary>
    /// <remarks>
    /// Es la diferencia con <c>QuienFirmaAMano</c>, que busca el nombre «Miguel»: el rol
    /// es un dato de la base y el nombre es prosa. Quien se da de alta como administrador
    /// puede llamarse como quiera.
    /// </remarks>
    [TestMethod]
    public void ElAdministradorNoTieneQueLlamarseMiguel()
    {
        var activos = new[] { Uno(7, "José Miguel Anonimo", RolDeCompanero.Administrador) };
        Assert.AreEqual(7, ElAdministrador.De(activos)?.Id);
    }

    // ---- lo que nunca se adivina ---------------------------------------

    /// <summary>Con dos administradores activos no se elige uno: no hay atajo.</summary>
    /// <remarks>ADR-0005 §6.3: «si no hay exactamente uno, el boton no esta y se dice por que».</remarks>
    [TestMethod]
    public void ConDosAdministradoresNoSeAdivinaCual()
    {
        var activos = new[]
        {
            Uno(2, "Miguel", RolDeCompanero.Administrador),
            Uno(5, "Ana", RolDeCompanero.Administrador),
        };

        Assert.IsNull(ElAdministrador.De(activos));
        Assert.Contains("2", ElAdministrador.PorQueNoSePuede(activos).Linea, StringComparison.Ordinal);
    }

    /// <summary>Un administrador desactivado no cuenta: la lista que entra es la de activos.</summary>
    [TestMethod]
    public void UnAdministradorDesactivadoNoCuenta()
    {
        var desactivado = Uno(2, "Miguel", RolDeCompanero.Administrador) with
        {
            Activo = false,
            DesactivadoEn = "2026-09-01",
        };

        Assert.IsNull(ElAdministrador.De([desactivado]));
    }

    /// <summary>Sin ningun companero en la base tampoco se inventa uno.</summary>
    [TestMethod]
    public void SinNingunCompaneroNoHayAdministrador()
    {
        Assert.IsNull(ElAdministrador.De([]));
        Assert.AreEqual(GravedadDeAviso.Problema, ElAdministrador.PorQueNoSePuede([]).Gravedad);
    }

    // ⚠️ Que el origen sean las palabras literales del dueno NO se fija aqui: comparar dos
    // constantes es una prueba que el compilador ya sabe cierta y que no mira la base. Se
    // fija donde importa —sobre el valor RELEIDO de la base— en
    // `PruebasDelAtajoDelAdministrador.QuedaEscritoQueFueElAdministradorYQuienEs`.
}
