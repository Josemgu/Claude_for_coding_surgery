using Fichas.App.Asignar;
using Fichas.App.Correccion;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Asignar;

/// <summary>
/// El puesto de cada compañero: qué es y en qué peldaño está.
/// </summary>
/// <remarks>
/// <para>Nacen del criterio, no del codigo. Medido el 2026-09-05: el alta creaba siempre con
/// el rol por defecto (<c>PanelDelEquipo.cs:258</c>, <c>new Companero { Nombre = nombre }</c>)
/// y no habia selector de rol en ninguna pantalla. Consecuencia: el dueno leia
/// «para completar sin verificar, dese de alta usted como administrador» y <b>no tenia donde
/// hacerlo</b>. El atajo existia y era inalcanzable.</para>
///
/// <para>Los dos cuidados que el dueno fijo y que estas pruebas vigilan: <b>nadie se crea ni
/// se asciende solo</b>, y <b>cambiar el puesto de alguien no toca lo que ya firmo ni lo que
/// le asignaron</b>.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelPuestoDelCompanero
{
    /// <summary>El dueno se da de alta como administrador y en la base queda ese rol.</summary>
    [TestMethod]
    public void DarDeAltaComoAdministradorDejaEseRolEscrito()
    {
        var banco = new BaseDePrueba();
        var puestos = new PuestosDelEquipo(banco.Servicios.Companeros, banco.Avisos);

        var resultado = puestos.DarDeAlta("Miguel Anonimo", RolDeCompanero.Administrador, 3);

        var leido = banco.Servicios.Companeros.Obtener(resultado.Id)!;
        Assert.IsTrue(resultado.SeEscribio);
        Assert.AreEqual(RolDeCompanero.Administrador, leido.Rol);
        Assert.AreEqual(3, leido.Categoria);
        Assert.AreEqual("Miguel Anonimo", leido.Nombre);
        Assert.IsTrue(leido.Activo, "Quien entra, entra activo.");
    }

    /// <summary>Sin decir nada se entra como companero y en el primer peldano.</summary>
    /// <remarks>
    /// Es el defecto de la columna y no cambia: un alta que ascendiera sola seria justo lo
    /// contrario de «yo debo tener el control de quien se anade y quien no».
    /// </remarks>
    [TestMethod]
    public void SinDecirNadaSeEntraComoCompaneroYEnElPrimerPeldano()
    {
        var banco = new BaseDePrueba();
        var puestos = new PuestosDelEquipo(banco.Servicios.Companeros, banco.Avisos);

        var resultado = puestos.DarDeAlta("Alguien", RolDeCompanero.Companero, PuestosDelEquipo.CategoriaMinima);

        var leido = banco.Servicios.Companeros.Obtener(resultado.Id)!;
        Assert.AreEqual(RolDeCompanero.Companero, leido.Rol);
        Assert.AreEqual(1, leido.Categoria);
    }

    /// <summary>Dar de alta a un administrador no asciende a nadie mas del equipo.</summary>
    [TestMethod]
    public void DarDeAltaUnAdministradorNoAsciendeANadieMas()
    {
        var banco = new BaseDePrueba();
        var puestos = new PuestosDelEquipo(banco.Servicios.Companeros, banco.Avisos);
        var antes = banco.Servicios.Companeros
            .Listar(new FiltroDeCompaneros(SoloActivos: false), Pagina.Primera(int.MaxValue))
            .Elementos.ToDictionary(c => c.Id, c => (c.Rol, c.Categoria));

        puestos.DarDeAlta("Miguel Anonimo", RolDeCompanero.Administrador, 1);

        foreach (var (id, puesto) in antes)
        {
            var ahora = banco.Servicios.Companeros.Obtener(id)!;
            Assert.AreEqual(puesto.Rol, ahora.Rol, $"El compañero {id} cambio de rol sin que nadie se lo pidiera.");
            Assert.AreEqual(puesto.Categoria, ahora.Categoria, $"Y el compañero {id} cambio de peldaño solo.");
        }
    }

    /// <summary>
    /// Cambiar el puesto de alguien que ya existe NO toca lo que firmo ni lo que le asignaron.
    /// </summary>
    /// <remarks>
    /// Es la mitad del encargo que mas duele si se rompe: el dueno lo dijo asi —«nunca se
    /// pierde el rastro de quien hizo que»—. Se comprueba con el conteo antes y despues, no
    /// leyendo el codigo: una escritura que reescribe la fila entera puede llevarse por
    /// delante lo de al lado sin que nadie lo note.
    /// </remarks>
    [TestMethod]
    public void CambiarElPuestoNoTocaLoQueYaFirmoNiLoQueLeAsignaron()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();
        var quien = banco.Activos[0];
        var puestos = new PuestosDelEquipo(banco.Servicios.Companeros, banco.Avisos);

        banco.Operacion.AsignarVarios([ids[0], ids[1], ids[2]], quien.Id, quien.Nombre);
        banco.FirmarUnCampo(ids[3], quien.Id);
        var asignacionesAntes = banco.Servicios.Asignaciones
            .Listar(new FiltroDeAsignaciones(CompaneroId: quien.Id, SoloActivas: false), Pagina.Primera(int.MaxValue))
            .TotalDisponible;
        var firmadosAntes = banco.Servicios.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, ids[3]);
        var marcadoPorAntes = banco.Servicios.Casos.Obtener(ids[2])!.EstadoMarcadoPor;

        var resultado = puestos.Cambiar(quien.Id, RolDeCompanero.Gerente, 2);

        var despues = banco.Servicios.Companeros.Obtener(quien.Id)!;
        Assert.IsTrue(resultado.SeEscribio);
        Assert.AreEqual(RolDeCompanero.Gerente, despues.Rol, "El puesto si cambia: es lo que se pidio.");
        Assert.AreEqual(2, despues.Categoria);
        Assert.AreEqual(quien.Nombre, despues.Nombre, "Y no se lleva por delante el nombre.");
        Assert.AreEqual(quien.CreadoEn, despues.CreadoEn, "Ni la fecha de alta.");
        Assert.AreEqual(
            asignacionesAntes,
            banco.Servicios.Asignaciones
                .Listar(new FiltroDeAsignaciones(CompaneroId: quien.Id, SoloActivas: false), Pagina.Primera(int.MaxValue))
                .TotalDisponible,
            "Ni una asignación menos ni una más.");
        Assert.AreEqual(3, asignacionesAntes, "Y la medición vale porque de verdad llevaba tres.");
        Assert.AreEqual(
            firmadosAntes,
            banco.Servicios.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, ids[3]),
            "Ni una firma menos: lo que ya dio por bueno sigue dado por bueno.");
        Assert.AreEqual(1, firmadosAntes, "Y la medición vale porque de verdad habia firmado uno.");
        Assert.AreEqual(
            marcadoPorAntes,
            banco.Servicios.Casos.Obtener(ids[2])!.EstadoMarcadoPor,
            "Y su nombre sigue en el estado que marco.");
    }

    /// <summary>Cambiar el puesto de un desactivado no lo devuelve al trabajo.</summary>
    [TestMethod]
    public void CambiarElPuestoDeUnDesactivadoNoLoReactiva()
    {
        var banco = new BaseDePrueba();
        var victima = banco.Activos[0];
        var puestos = new PuestosDelEquipo(banco.Servicios.Companeros, banco.Avisos);
        banco.Servicios.Companeros.Desactivar(victima.Id, banco.Reloj.Ahora());

        puestos.Cambiar(victima.Id, RolDeCompanero.Gerente, 4);

        var leido = banco.Servicios.Companeros.Obtener(victima.Id)!;
        Assert.IsFalse(leido.Activo, "Poner un puesto no es una puerta trasera para reactivar.");
        Assert.AreEqual(banco.Reloj.Ahora(), leido.DesactivadoEn, "Y su fecha de baja se queda.");
        Assert.AreEqual(RolDeCompanero.Gerente, leido.Rol, "El puesto si se le puede poner estando de baja.");
    }

    /// <summary>
    /// Un peldano por debajo del primero no se escribe, y se dice con palabras del programa.
    /// </summary>
    /// <remarks>
    /// El <c>CHECK</c> del esquema lo rechazaria en la base de verdad, pero el doble no tiene
    /// ese <c>CHECK</c>: sin esta guarda, un cero pasaria en las pruebas y reventaria en la
    /// maquina del dueno con el error del motor en vez de con una frase.
    /// </remarks>
    [TestMethod]
    public void UnPeldanoPorDebajoDelPrimeroNoSeEscribeYSeDice()
    {
        var banco = new BaseDePrueba();
        var quien = banco.Activos[0];
        var puestos = new PuestosDelEquipo(banco.Servicios.Companeros, banco.Avisos);
        banco.Avisos.CerrarTodos();

        var resultado = puestos.Cambiar(quien.Id, RolDeCompanero.Gerente, 0);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.HasCount(1, banco.Avisos.Pendientes, "Se dice en la franja, no con un cuadro.");
        var leido = banco.Servicios.Companeros.Obtener(quien.Id)!;
        Assert.AreEqual(quien.Rol, leido.Rol, "Y no entra a medias: el rol tampoco se escribe.");
        Assert.AreEqual(quien.Categoria, leido.Categoria);
    }

    /// <summary>Cambiar el puesto de alguien que no esta se dice y no escribe nada.</summary>
    [TestMethod]
    public void CambiarElPuestoDeAlguienQueNoEstaSeDiceYNoEscribe()
    {
        var banco = new BaseDePrueba();
        var puestos = new PuestosDelEquipo(banco.Servicios.Companeros, banco.Avisos);
        var cuantosAntes = banco.Servicios.Companeros
            .Listar(new FiltroDeCompaneros(SoloActivos: false), Pagina.Primera(int.MaxValue)).TotalDisponible;
        banco.Avisos.CerrarTodos();

        var resultado = puestos.Cambiar(9999, RolDeCompanero.Administrador, 1);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.HasCount(1, banco.Avisos.Pendientes);
        Assert.AreEqual(
            cuantosAntes,
            banco.Servicios.Companeros
                .Listar(new FiltroDeCompaneros(SoloActivos: false), Pagina.Primera(int.MaxValue)).TotalDisponible,
            "Y desde luego no se crea uno nuevo por el camino.");
    }

    /// <summary>El desplegable ofrece los tres roles del esquema, con sus palabras.</summary>
    [TestMethod]
    public void ElDesplegableOfreceLosTresRolesConSusPalabras()
    {
        var roles = PuestosDelEquipo.LosTresRoles;
        var palabras = roles.Select(PuestosDelEquipo.DecirElRol).ToList();

        Assert.HasCount(3, roles);
        CollectionAssert.AreEqual(
            new List<RolDeCompanero>
            {
                RolDeCompanero.Companero,
                RolDeCompanero.Gerente,
                RolDeCompanero.Administrador,
            },
            roles.ToList(),
            "En el orden de la escalera: el defecto primero.");
        CollectionAssert.AreEqual(
            new List<string> { "Compañero", "Gerente", "Administrador" },
            palabras);
    }

    /// <summary>
    /// El circulo que el encargo cierra: con un administrador dado de alta, el atajo de
    /// Corrección deja de ser inalcanzable.
    /// </summary>
    /// <remarks>
    /// Se mide con la MISMA clase que decide en la pantalla de Corrección
    /// (<c>ElAdministrador</c>), no con una copia de su regla escrita aquí: una copia
    /// contestaria que si mientras la pantalla sigue diciendo que no.
    /// </remarks>
    [TestMethod]
    public void ConUnAdministradorDadoDeAltaElAtajoDeCorreccionDejaDeEstarCerrado()
    {
        var banco = new BaseDePrueba();
        var puestos = new PuestosDelEquipo(banco.Servicios.Companeros, banco.Avisos);
        var antes = ElAdministrador.De(banco.Servicios.Companeros.Activos());
        var lineaDeAntes = ElAdministrador.PorQueNoSePuede(banco.Servicios.Companeros.Activos()).Linea;

        puestos.DarDeAlta("Miguel Anonimo", RolDeCompanero.Administrador, 1);

        var despues = ElAdministrador.De(banco.Servicios.Companeros.Activos());
        Assert.IsNull(antes, "Sin nadie con ese rol no habia atajo: es el hueco que se cierra.");
        Assert.AreEqual("para completar sin verificar, dese de alta usted como administrador", lineaDeAntes);
        Assert.IsNotNull(despues, "Y ahora si lo hay.");
        Assert.AreEqual("Miguel Anonimo", despues.Nombre, "Firmado con su nombre, no con el del primero de la lista.");
    }

    /// <summary>
    /// Con dos administradores activos el atajo se va, y el panel lo dice en vez de callarlo.
    /// </summary>
    /// <remarks>
    /// Sin esto, el dueno asciende a un segundo, el boton de Corrección desaparece y no hay
    /// nada en pantalla que relacione una cosa con la otra.
    /// </remarks>
    [TestMethod]
    public void ConDosAdministradoresActivosSeDiceQueElAtajoSeVa()
    {
        var banco = new BaseDePrueba();
        var puestos = new PuestosDelEquipo(banco.Servicios.Companeros, banco.Avisos);

        puestos.DarDeAlta("Miguel Anonimo", RolDeCompanero.Administrador, 1);
        var conUno = puestos.LoQueLePasaAlAtajo();
        puestos.DarDeAlta("Otro Miguel", RolDeCompanero.Administrador, 1);
        var conDos = puestos.LoQueLePasaAlAtajo();

        Assert.IsNull(conUno, "Con uno no hay nada que decir: el atajo funciona.");
        Assert.IsNotNull(conDos);
        StringAssert.Contains(conDos.Linea, "2 administradores activos");
        Assert.IsNull(
            ElAdministrador.De(banco.Servicios.Companeros.Activos()),
            "Y la pantalla de Corrección, en efecto, ya no sabe cuál es él.");
    }

    /// <summary>Sin ningun administrador el panel no dice nada: un equipo sin uno es normal.</summary>
    [TestMethod]
    public void SinNingunAdministradorElPanelNoDiceNada()
    {
        var banco = new BaseDePrueba();
        var puestos = new PuestosDelEquipo(banco.Servicios.Companeros, banco.Avisos);

        Assert.IsNull(
            puestos.LoQueLePasaAlAtajo(),
            "El aviso de «dese de alta» es de la pantalla que ofrece el atajo, no de esta.");
    }
}
