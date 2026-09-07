using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Mantenimiento;
using Fichas.Datos.Repositorios;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Alta, baja, reactivacion y borrado de companeros, con la regla que manda encima:
/// nunca se pierde el rastro de quien hizo que.
/// </summary>
/// <remarks>
/// <para>
/// El criterio es del dueno, 2026-09-05: «tampoco tengo la opcion de eliminar o quitar
/// agentes del sistema, eso es super importante», y antes «yo debo tener el control de
/// quien se anade y quien no».
/// </para>
/// <para>
/// ⛔ La distincion que estas pruebas guardan: <b>desactivar es lo normal</b> —deja de
/// recibir trabajo y su nombre sigue en todo lo que firmo— y <b>borrar de verdad solo si
/// no lleva nada</b>. La base ya distingue las dos cosas desde la version 1
/// (<c>companeros.activo</c> y <c>companeros.desactivado_en</c>).
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebaDelEquipo
{
    [TestMethod]
    public void UnCompaneroNuevoSeAnadeAManoYSaleActivo()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);

        var alta = companeros.Guardar(new Companero { Nombre = "Sandy" });

        Assert.IsTrue(alta.SeEscribio, "No se pudo dar de alta a un companero.");
        var leido = companeros.Obtener(alta.Id);
        Assert.IsNotNull(leido, "El companero recien dado de alta no se pudo releer.");
        Assert.AreEqual("Sandy", leido.Nombre);
        Assert.IsTrue(leido.Activo, "Un companero nuevo tiene que nacer activo.");
        Assert.IsNull(leido.DesactivadoEn, "Un companero activo no puede tener fecha de baja.");
    }

    [TestMethod]
    public void UnCompaneroSinNombreNoEntraYLoDiceSinLanzar()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);

        var alta = companeros.Guardar(new Companero { Nombre = "   " });

        Assert.IsFalse(alta.SeEscribio, "Entro un companero sin nombre.");
        Assert.IsTrue(alta.HayAvisos, "No dijo por que no entro.");
        Assert.AreEqual(0, baseDePrueba.ContarFilasDe("companeros"), "Se escribio algo igualmente.");
    }

    [TestMethod]
    public void DesactivarLeQuitaElTrabajoNuevoYLeDejaSuNombreEnLoQueYaHizo()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var asignaciones = new RepositorioDeAsignaciones(baseDePrueba.Conexion);
        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });
        var caso = SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "CASP2609", sandy.Id, 1);

        var baja = companeros.Desactivar(sandy.Id, "2026-09-05 11:00:00");

        Assert.IsTrue(baja.SeEscribio, "No se pudo desactivar.");
        Assert.IsFalse(
            companeros.Activos().Any(c => c.Id == sandy.Id),
            "Un companero desactivado sigue saliendo entre los que pueden recibir trabajo.");

        var leido = companeros.Obtener(sandy.Id);
        Assert.IsNotNull(leido);
        Assert.IsFalse(leido.Activo, "Sigue activo despues de desactivarlo.");
        Assert.AreEqual("2026-09-05 11:00:00", leido.DesactivadoEn, "La baja no dejo fecha.");

        // Y lo que de verdad importa: su nombre sigue en lo que ya llevaba.
        Assert.AreEqual(
            1,
            asignaciones.VivasDeCaso(caso).Count(a => a.CompaneroId == sandy.Id),
            "Desactivar se llevo por delante la asignacion que ya tenia.");
    }

    [TestMethod]
    public void ReactivarLoDevuelveAlEquipoYLeQuitaLaFechaDeBaja()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);
        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });
        companeros.Desactivar(sandy.Id, "2026-09-05 11:00:00");

        var vuelta = mantenimiento.Reactivar(sandy.Id);

        Assert.IsTrue(vuelta.SeEscribio, "No se pudo reactivar.");
        var leido = companeros.Obtener(sandy.Id);
        Assert.IsNotNull(leido);
        Assert.IsTrue(leido.Activo, "No volvio a estar activo.");
        Assert.IsNull(leido.DesactivadoEn, "Volvio activo pero con la fecha de baja puesta.");
        Assert.IsTrue(
            companeros.Activos().Any(c => c.Id == sandy.Id),
            "No vuelve a salir entre los que pueden recibir trabajo.");
    }

    [TestMethod]
    public void UnCompaneroQueNoLlevaNadaSeBorraDeVerdad()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);

        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });
        var miguel = companeros.Guardar(new Companero { Nombre = "Miguel" });
        SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "CASP2609", miguel.Id, 1);

        var carga = mantenimiento.Carga(sandy.Id);
        Assert.IsTrue(carga.NoLlevaNada, "Sandy lleva algo sin haber hecho nada: " + carga.Dicho);

        var plan = mantenimiento.PlanearCompanero(sandy.Id);
        Assert.IsTrue(plan.SePuedeBorrar, "No dejo borrar a quien no lleva nada.");
        StringAssert.Contains(plan.Titulo, "Sandy", "El titulo no dice a quien se borra.");
        Assert.IsNotNull(plan.RutaDeLaCopia, "Borro a un companero sin copia previa.");

        var resultado = mantenimiento.Borrar(plan);

        Assert.IsTrue(resultado.SeBorro, "No se borro a Sandy.");
        Assert.IsNull(companeros.Obtener(sandy.Id), "Sandy sigue en la base.");
        Assert.IsNotNull(companeros.Obtener(miguel.Id), "Se llevo por delante a Miguel.");
        SiembraParaBorrar.NoQuedaNadaColgando(baseDePrueba);
    }

    [TestMethod]
    public void UnCompaneroConTrabajoASuNombreNoSeBorraYSeDiceCuantoLleva()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);

        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });
        SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "CASP2609", sandy.Id, 2);

        var carga = mantenimiento.Carga(sandy.Id);
        Assert.IsFalse(carga.NoLlevaNada, "Dice que no lleva nada teniendo trabajo suyo.");
        Assert.AreEqual(
            1,
            carga.Detalle.First(d => d.Tabla == "asignaciones").Filas,
            "No conto la asignacion viva.");
        Assert.AreEqual(
            2,
            carga.Detalle.First(d => d.Tabla == "procedencia_campo").Filas,
            "No conto las dos firmas.");
        Assert.AreEqual(1, carga.Detalle.First(d => d.Tabla == "contactos").Filas, "No conto el contacto.");

        var plan = mantenimiento.PlanearCompanero(sandy.Id);

        Assert.IsFalse(plan.SePuedeBorrar, "Dejo borrar a quien lleva trabajo a su nombre.");
        Assert.IsNotEmpty(plan.Avisos, "No dijo por que no se puede borrar.");
        StringAssert.Contains(
            plan.Avisos[0].Detalle ?? string.Empty,
            "asignación",
            "El motivo no dice QUE lleva a su nombre.");

        var resultado = mantenimiento.Borrar(plan);
        Assert.IsFalse(resultado.SeBorro, "Lo borro igualmente.");
        Assert.IsNotNull(companeros.Obtener(sandy.Id), "Sandy desaparecio de la base.");
    }

    [TestMethod]
    public void BorrarUnCompaneroQueNoEstaNoRompeNada()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);

        var carga = mantenimiento.Carga(9999);
        Assert.AreEqual(string.Empty, carga.Nombre, "Invento un nombre para alguien que no esta.");

        var plan = mantenimiento.PlanearCompanero(9999);
        Assert.IsFalse(plan.SePuedeBorrar, "Dio permiso para borrar a alguien que no existe.");
        Assert.IsNotEmpty(plan.Avisos, "No dijo que no estaba.");
        Assert.IsNull(plan.RutaDeLaCopia, "Copio la base para borrar a quien no existe.");
    }

    [TestMethod]
    public void ElBorradoDeDocumentosNoSeLlevaALosCompaneros()
    {
        // La comprobacion cruzada: «empezar de cero» vacia documentos, no el equipo.
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);
        var mantenimiento = new RepositorioDeMantenimiento(baseDePrueba.Conexion);
        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" });

        // Se siembra ANTES de darlo de baja: asignar a un desactivado se rechaza, y con
        // razon —es justo lo que «desactivar» significa—, asi que el orden importa.
        SiembraParaBorrar.UnDocumentoCompleto(baseDePrueba, "CASP2609", sandy.Id, 1);
        companeros.Desactivar(sandy.Id, "2026-09-05 11:00:00");

        mantenimiento.Borrar(mantenimiento.PlanearEmpezarDeCero());

        var todos = companeros.Listar(new FiltroDeCompaneros(SoloActivos: false), new Pagina(0, 50));
        Assert.AreEqual(1, todos.TotalDisponible, "El equipo no sobrevivio a «empezar de cero».");
        Assert.IsFalse(
            todos.Elementos[0].Activo,
            "Ademas le cambio el estado a un companero que no se estaba tocando.");
    }
}
