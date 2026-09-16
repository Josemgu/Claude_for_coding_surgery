using Fichas.App.Asignar;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Asignar;

/// <summary>
/// Editar a un compañero desde el panel del equipo: nombre, rol y categoría en un gesto.
/// </summary>
/// <remarks>
/// <para>Nacen del dueño, 2026-09-16: <i>«En Asignar debe dar la opción de eliminar,
/// editar o desactivar agentes»</i>. Medido por el supervisor ese día: desactivar y quitar
/// existían; <b>editar</b> no (grep de <c>Editar|Renombrar</c> en <c>Asignar/</c>: 0).</para>
///
/// <para>Medido antes de escribir: todas las firmas de la base guardan el <b>id</b> del
/// compañero y no su nombre —<c>verificado_por</c>, <c>estado_marcado_por</c>,
/// <c>estado_del_companero_por</c>, <c>propuesto_por</c>, <c>contactado_por</c>,
/// <c>pasos_por</c>, todas <c>INTEGER</c>—. Así que cambiar el nombre no toca ninguna fila
/// firmada: la prueba de abajo lo mide contando antes y después.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeEditarAlCompanero
{
    /// <summary>Dado un compañero, cuando se editan nombre, rol y categoría, entonces los tres quedan escritos.</summary>
    [TestMethod]
    public void EditarCambiaNombreRolYCategoria()
    {
        var banco = new BaseDePrueba();
        var quien = banco.Activos[0];
        var puestos = new PuestosDelEquipo(banco.Servicios.Companeros, banco.Avisos);

        var resultado = puestos.Editar(quien.Id, "  Nombre Corregido  ", RolDeCompanero.Gerente, 2);

        var leido = banco.Servicios.Companeros.Obtener(quien.Id)!;
        Assert.IsTrue(resultado.SeEscribio);
        Assert.AreEqual("Nombre Corregido", leido.Nombre, "El nombre se guarda recortado.");
        Assert.AreEqual(RolDeCompanero.Gerente, leido.Rol);
        Assert.AreEqual(2, leido.Categoria);
        Assert.AreEqual(quien.CreadoEn, leido.CreadoEn, "La fecha de alta no se toca.");
        Assert.IsTrue(leido.Activo, "Editar no desactiva.");
    }

    /// <summary>Editar el nombre deja intactas las firmas y las asignaciones que ya llevaba con el nombre viejo.</summary>
    [TestMethod]
    public void EditarElNombreNoTocaLoQueYaFirmoNiLoQueLleva()
    {
        var banco = new BaseDePrueba();
        var ids = banco.MeterUnCasoDeCadaEstado();
        var quien = banco.Activos[0];
        var puestos = new PuestosDelEquipo(banco.Servicios.Companeros, banco.Avisos);
        banco.Operacion.AsignarVarios([ids[0], ids[1]], quien.Id, quien.Nombre);
        banco.FirmarUnCampo(ids[3], quien.Id);
        var firmadosAntes = banco.Servicios.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, ids[3]);
        var marcadoPorAntes = banco.Servicios.Casos.Obtener(ids[2])!.EstadoMarcadoPor;

        puestos.Editar(quien.Id, "Otro Nombre", quien.Rol, quien.Categoria);

        Assert.AreEqual(1, firmadosAntes);
        Assert.AreEqual(firmadosAntes, banco.Servicios.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, ids[3]));
        Assert.AreEqual(quien.Id, marcadoPorAntes, "El estado lo marcó él, por id.");
        Assert.AreEqual(marcadoPorAntes, banco.Servicios.Casos.Obtener(ids[2])!.EstadoMarcadoPor, "Y sigue siendo él.");
        Assert.AreEqual(2, banco.CuantasAsignacionesVivas(quien.Id), "Sus asignaciones siguen a su nombre.");
    }

    /// <summary>Con el nombre en blanco no se edita nada y se dice: una firma sin nombre no dice quién firmó.</summary>
    [TestMethod]
    public void EditarConElNombreEnBlancoNoEscribeYSeDice()
    {
        var banco = new BaseDePrueba();
        var quien = banco.Activos[0];
        var puestos = new PuestosDelEquipo(banco.Servicios.Companeros, banco.Avisos);
        banco.Avisos.CerrarTodos();

        var resultado = puestos.Editar(quien.Id, "   ", RolDeCompanero.Gerente, 3);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.HasCount(1, banco.Avisos.Pendientes, "Se dice en la franja, no con un cuadro.");
        var leido = banco.Servicios.Companeros.Obtener(quien.Id)!;
        Assert.AreEqual(quien.Nombre, leido.Nombre);
        Assert.AreEqual(quien.Rol, leido.Rol, "Y no entra a medias: el rol tampoco.");
    }

    /// <summary>Editar a un desactivado le cambia lo pedido y NO lo reactiva.</summary>
    [TestMethod]
    public void EditarAUnDesactivadoNoLoReactiva()
    {
        var banco = new BaseDePrueba();
        var quien = banco.Activos[0];
        var puestos = new PuestosDelEquipo(banco.Servicios.Companeros, banco.Avisos);
        banco.Servicios.Companeros.Desactivar(quien.Id, banco.Reloj.Ahora());

        puestos.Editar(quien.Id, "Nombre Nuevo", quien.Rol, quien.Categoria);

        var leido = banco.Servicios.Companeros.Obtener(quien.Id)!;
        Assert.IsFalse(leido.Activo);
        Assert.AreEqual(banco.Reloj.Ahora(), leido.DesactivadoEn);
        Assert.AreEqual("Nombre Nuevo", leido.Nombre);
    }

    /// <summary>Editar a alguien que ya no está se dice y no crea a nadie.</summary>
    [TestMethod]
    public void EditarAAlguienQueNoEstaSeDiceYNoCreaANadie()
    {
        var banco = new BaseDePrueba();
        var puestos = new PuestosDelEquipo(banco.Servicios.Companeros, banco.Avisos);
        var antes = banco.Servicios.Companeros
            .Listar(new FiltroDeCompaneros(SoloActivos: false), Pagina.Primera(int.MaxValue)).TotalDisponible;
        banco.Avisos.CerrarTodos();

        var resultado = puestos.Editar(9999, "Nadie", RolDeCompanero.Companero, 1);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.HasCount(1, banco.Avisos.Pendientes);
        Assert.AreEqual(antes, banco.Servicios.Companeros
            .Listar(new FiltroDeCompaneros(SoloActivos: false), Pagina.Primera(int.MaxValue)).TotalDisponible);
    }

    /// <summary>Editar sin cambiar nada no escribe: no hay línea de acuse que decir.</summary>
    [TestMethod]
    public void EditarSinCambiarNadaNoEscribe()
    {
        var banco = new BaseDePrueba();
        var quien = banco.Activos[0];
        var puestos = new PuestosDelEquipo(banco.Servicios.Companeros, banco.Avisos);

        var resultado = puestos.Editar(quien.Id, quien.Nombre, quien.Rol, quien.Categoria);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.IsEmpty(resultado.Avisos, "Y sin aviso: no es un error, es que no había nada que cambiar.");
    }
}
