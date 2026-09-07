using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Repositorios;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Que lo que la migracion 18 abrio en la base se pueda escribir y leer DE VERDAD.
/// </summary>
/// <remarks>
/// <para>
/// Una columna que existe en el esquema y que ningun repositorio escribe se queda nula
/// para siempre, y eso ya paso aqui: <c>casos.estado_del_companero</c> lleva desde la
/// migracion 14 sin que nada la escriba por el camino real. Estas pruebas van por el
/// repositorio y no por SQL a pelo, que es donde estaba el hueco.
/// </para>
/// <para>
/// ⚠️ Los criterios salen de las palabras del dueno del 2026-09-05 y de las fases C10 a
/// C16, no del codigo: los tres estados que quiere ver en el calendario, la escalera de
/// categorias, y que lo que dijo el companero no se borre cuando Miguel corrige encima.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebaDelVocabularioNuevo
{
    /// <summary>Desde el principio y con sitio de sobra; `Desde` es un salto, no un numero de pagina.</summary>
    private static readonly Pagina PrimeraPagina = new(0, 50);

    // ═══════════ Los tres estados que el dueno quiere ver en el calendario ═══════════

    /// <summary>
    /// Dado un caso no completo, cuando se guarda con cada uno de los tres motivos que el
    /// dueno nombro, entonces vuelve a leerse igual por el repositorio.
    /// </summary>
    /// <remarks>
    /// Sus palabras: <i>«en el calendario tambien puede decir el estado: no completado,
    /// no se pudo comunicar con el lider, o el lider no lo hizo»</i>. El primero es el
    /// estado; los otros dos son el motivo de ese estado, y aqui se comprueban los tres
    /// juntos porque es como se van a pintar.
    /// </remarks>
    [TestMethod]
    public void LosTresEstadosDelCalendarioSeEscribenYSeLeenPorElRepositorio()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);

        var esperado = new (MotivoDeNoCompletar Motivo, string Clave)[]
        {
            (MotivoDeNoCompletar.SinMotivo, "(sin motivo)"),
            (MotivoDeNoCompletar.NoSePudoComunicar, "no_se_pudo_comunicar"),
            (MotivoDeNoCompletar.ElLiderNoLoHizo, "el_lider_no_lo_hizo"),
            (MotivoDeNoCompletar.OtraRazon, "otra_razon"),
        };

        foreach (var (motivo, clave) in esperado)
        {
            var escritura = casos.Guardar(new Caso
            {
                NumeroCaso = "CASP2609",
                CreadoEn = "2026-09-05 10:00:00",
                EstadoRecomendacion = "no_completa",
                MotivoNoCompleta = Caso.EscribirMotivo(motivo),
            });

            Assert.IsTrue(
                escritura.SeEscribio,
                $"No se pudo guardar el motivo «{clave}»: " +
                string.Join(" | ", escritura.Avisos.Select(a => a.Linea)));

            var leido = casos.Obtener(escritura.Id)!;

            Assert.AreEqual(
                EstadoDeRecomendacion.NoCompleta,
                leido.Estado,
                "El estado dejo de ser «no completa» al ponerle un motivo, y no son lo mismo.");
            Assert.AreEqual(motivo, leido.Motivo, $"El motivo «{clave}» no volvio igual.");

            Console.WriteLine(
                "   caso {0}: estado = {1} · motivo_no_completa = {2}",
                leido.Id,
                leido.EstadoRecomendacion,
                leido.MotivoNoCompleta ?? "(nulo)");
        }
    }

    /// <summary>
    /// Dado un motivo que no esta en la lista cerrada, cuando se intenta guardar, entonces
    /// el repositorio lo DICE y no lanza: requisito 9, avisar sin tumbar la pantalla.
    /// </summary>
    [TestMethod]
    public void UnMotivoInventadoSeAvisaYNoTumbaLaPantalla()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);

        var escritura = casos.Guardar(new Caso
        {
            NumeroCaso = "CASP2609",
            CreadoEn = "2026-09-05 10:00:00",
            MotivoNoCompleta = "se me olvido preguntar",
        });

        Assert.IsFalse(escritura.SeEscribio, "Un motivo fuera de la lista entro en la base.");
        Assert.IsNotEmpty(escritura.Avisos, "El motivo se rechazo sin decir por que.");
        Console.WriteLine("   aviso: {0}", escritura.Avisos[^1].Detalle);
    }

    // ═══════════════ La escalera de categorias y el rol del companero ═══════════════

    /// <summary>
    /// Dado un companero, cuando se le pone una categoria y un rol, entonces se leen
    /// igual, y el peldano 7 entra como el 1 porque los topes los pone el dueno.
    /// </summary>
    [TestMethod]
    public void UnaCategoriaYUnRolSePonenYSeLeen()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);

        var alta = companeros.Guardar(new Companero { Nombre = "Sandy" });
        Assert.IsTrue(alta.SeEscribio, "No se pudo dar de alta a Sandy.");

        var recienCreado = companeros.Obtener(alta.Id)!;
        Assert.AreEqual(
            RolDeCompanero.Companero,
            recienCreado.Rol,
            "Un companero nuevo no nacio con el rol de companero.");
        Assert.AreEqual(1, recienCreado.Categoria, "Un companero nuevo no nacio en el primer peldano.");

        foreach (var (rol, categoria) in new[]
                 {
                     (RolDeCompanero.Gerente, 2),
                     (RolDeCompanero.Gerente, 3),
                     (RolDeCompanero.Administrador, 7),
                     (RolDeCompanero.Companero, 1),
                 })
        {
            var cambio = companeros.Guardar(recienCreado with { Rol = rol, Categoria = categoria });
            Assert.IsTrue(
                cambio.SeEscribio,
                $"No se pudo poner el rol {rol} con la categoria {categoria}: " +
                string.Join(" | ", cambio.Avisos.Select(a => a.Linea)));

            var leido = companeros.Obtener(alta.Id)!;
            Assert.AreEqual(rol, leido.Rol, "El rol no volvio igual.");
            Assert.AreEqual(categoria, leido.Categoria, "La categoria no volvio igual.");
            Console.WriteLine("   {0}: rol = {1} · categoria = {2}", leido.Nombre, leido.Rol, leido.Categoria);
        }
    }

    /// <summary>
    /// Dado un peldano anterior al primero, cuando se intenta guardar, entonces se avisa
    /// y no se escribe: no hay categoria 0 y el programa no la inventa por su cuenta.
    /// </summary>
    [TestMethod]
    public void UnPeldanoAnteriorAlPrimeroSeAvisaYNoSeEscribe()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);

        var escritura = companeros.Guardar(new Companero { Nombre = "Sandy", Categoria = 0 });

        Assert.IsFalse(escritura.SeEscribio, "Entro un companero en la categoria 0.");
        Assert.IsNotEmpty(escritura.Avisos, "Se rechazo la categoria 0 sin decir por que.");
        Console.WriteLine("   aviso: {0}", escritura.Avisos[^1].Detalle);
    }

    /// <summary>
    /// Dado el rol de cada quien, cuando se listan los activos, entonces el rol viaja en
    /// la lista: es lo que hace falta para saber quien es el administrador sin adivinarlo.
    /// </summary>
    [TestMethod]
    public void ElRolViajaEnLaListaDeActivosYEnElListadoConFiltro()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);

        companeros.Guardar(new Companero { Nombre = "Sandy" });
        companeros.Guardar(new Companero { Nombre = "Miguel", Rol = RolDeCompanero.Administrador });
        companeros.Guardar(new Companero { Nombre = "Ana", Rol = RolDeCompanero.Gerente, Categoria = 2 });

        var activos = companeros.Activos();
        var administradores = activos.Where(c => c.Rol == RolDeCompanero.Administrador).ToList();

        Assert.HasCount(3, activos, "No se leyeron los tres companeros activos.");
        Assert.HasCount(
            1,
            administradores,
            "El rol no llego entero en `Activos()`, y de ahi sale quien es el administrador.");
        Assert.AreEqual("Miguel", administradores[0].Nombre);

        var pagina = companeros.Listar(FiltroDeCompaneros.Activos, PrimeraPagina);
        Assert.AreEqual(
            RolDeCompanero.Gerente,
            pagina.Elementos.Single(c => c.Nombre == "Ana").Rol,
            "El rol no llego entero en `Listar`.");
        Assert.AreEqual(2, pagina.Elementos.Single(c => c.Nombre == "Ana").Categoria);
    }

    // ═══════ El hueco del 2026-09-05: `estado_del_companero` quedaba nulo siempre ═══════

    /// <summary>
    /// Dado un caso que un companero devuelve en su hoja, cuando se le aplica la marca,
    /// entonces <c>estado_del_companero</c>, <c>_por</c> y <c>_en</c> quedan ESCRITAS.
    /// </summary>
    /// <remarks>
    /// El hueco medido: <c>RepositorioDeCasos.MarcarEstado</c> escribia solo el estado
    /// vigente, asi que esas tres columnas quedaban nulas por todos los caminos reales.
    /// Las pruebas que corrian sobre el repositorio FALSO no lo veian, porque el falso si
    /// las escribia.
    /// </remarks>
    [TestMethod]
    public void LoQueDijoLaHojaDelCompaneroSeEscribeConSuNombreYSuFecha()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);

        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" }).Id;
        var caso = casos.Guardar(new Caso { NumeroCaso = "CASP2609", CreadoEn = "2026-09-05 10:00:00" }).Id;

        var marca = casos.MarcarEstadoDelCompanero(
            caso,
            EstadoDeRecomendacion.NoCompleta,
            MotivoDeNoCompletar.NoSePudoComunicar,
            sandy,
            @"C:\paquetes\por_verificar.xlsx");

        Assert.IsTrue(marca.SeEscribio, string.Join(" | ", marca.Avisos.Select(a => a.Linea)));

        var leido = casos.Obtener(caso)!;

        Console.WriteLine(
            "   estado_del_companero = {0} · _por = {1} · _en = {2} · motivo_del_companero = {3}",
            leido.EstadoDelCompanero ?? "(nulo)",
            leido.EstadoDelCompaneroPor?.ToString() ?? "(nulo)",
            leido.EstadoDelCompaneroEn ?? "(nulo)",
            leido.MotivoDelCompanero ?? "(nulo)");

        Assert.AreEqual("no_completa", leido.EstadoDelCompanero, "`estado_del_companero` sigue nula.");
        Assert.AreEqual(sandy, leido.EstadoDelCompaneroPor, "`estado_del_companero_por` sigue nula.");
        Assert.IsNotNull(leido.EstadoDelCompaneroEn, "`estado_del_companero_en` sigue nula.");
        Assert.AreEqual(
            MotivoDeNoCompletar.NoSePudoComunicar,
            leido.MotivoQueDijoElCompanero,
            "El motivo que dijo el companero no se guardo.");

        // Y el estado vigente tambien, que es el que se ve: son dos ejes, no dos verdades.
        Assert.AreEqual(EstadoDeRecomendacion.NoCompleta, leido.Estado);
        Assert.AreEqual(@"C:\paquetes\por_verificar.xlsx", leido.EstadoMarcadoOrigen);
    }

    /// <summary>
    /// Dado un caso que ya trae lo que dijo el companero, cuando Miguel lo marca a mano,
    /// entonces lo que dijo el companero NO se borra.
    /// </summary>
    /// <remarks>
    /// Es el motivo por el que la migracion 14 desdoblo el estado, dicho en su propio
    /// codigo: <i>«con un solo grupo la correccion pisaria el nombre del companero y nadie
    /// sabria que discreparon»</i>. Sin esta prueba, ese desdoble es decorativo.
    /// </remarks>
    [TestMethod]
    public void CuandoMiguelCorrigeEncimaNoSeBorraLoQueDijoElCompanero()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var companeros = new RepositorioDeCompaneros(baseDePrueba.Conexion);

        var sandy = companeros.Guardar(new Companero { Nombre = "Sandy" }).Id;
        var miguel = companeros.Guardar(
            new Companero { Nombre = "Miguel", Rol = RolDeCompanero.Administrador }).Id;
        var caso = casos.Guardar(new Caso { NumeroCaso = "CASP2609", CreadoEn = "2026-09-05 10:00:00" }).Id;

        casos.MarcarEstadoDelCompanero(
            caso,
            EstadoDeRecomendacion.NoCompleta,
            MotivoDeNoCompletar.ElLiderNoLoHizo,
            sandy,
            @"C:\paquetes\por_verificar.xlsx");

        casos.MarcarEstado(
            caso, EstadoDeRecomendacion.Completa, miguel, "a mano en la pantalla Revisar");

        var leido = casos.Obtener(caso)!;

        Assert.AreEqual(
            EstadoDeRecomendacion.Completa, leido.Estado, "La correccion de Miguel no mando.");
        Assert.AreEqual(miguel, leido.EstadoMarcadoPor, "La marca vigente no la firma Miguel.");
        Assert.AreEqual(
            "no_completa",
            leido.EstadoDelCompanero,
            "La correccion de Miguel se llevo por delante lo que dijo Sandy.");
        Assert.AreEqual(
            sandy, leido.EstadoDelCompaneroPor, "El nombre de Sandy se perdio al corregir encima.");
        Assert.AreEqual(
            MotivoDeNoCompletar.ElLiderNoLoHizo,
            leido.MotivoQueDijoElCompanero,
            "El motivo que dio Sandy se perdio al corregir encima.");
    }

    // ═══════════════════════════ Archivar y desarchivar ═══════════════════════════

    /// <summary>
    /// Dado un caso archivado, cuando se desarchiva, entonces vuelve a las listas de
    /// trabajo y se queda SIN fecha de archivado.
    /// </summary>
    /// <remarks>
    /// El dueno pidio el 2026-09-05 que archivar saque el caso de lo visible y lo deje en
    /// el historico; deshacerlo tiene que poder hacerse. Esta prueba mide que el camino ya
    /// existe en el repositorio de verdad, que era la duda.
    /// </remarks>
    [TestMethod]
    public void UnCasoArchivadoSeDesarchivaYVuelveALasListasSinFecha()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var casos = new RepositorioDeCasos(baseDePrueba.Conexion);
        var caso = casos.Guardar(new Caso { NumeroCaso = "CASP2609", CreadoEn = "2026-09-05 10:00:00" }).Id;

        Assert.IsTrue(casos.Archivar(caso, true, "2026-09-05").SeEscribio, "No se pudo archivar.");
        var archivado = casos.Obtener(caso)!;
        Assert.IsTrue(archivado.Archivado);
        Assert.AreEqual("2026-09-05", archivado.FechaArchivado);
        Assert.AreEqual(
            0,
            casos.Contar(new FiltroDeCasos()),
            "Un caso archivado sigue contando en la lista de trabajo.");

        Assert.IsTrue(
            casos.Archivar(caso, false, string.Empty).SeEscribio, "No se pudo desarchivar.");
        var desarchivado = casos.Obtener(caso)!;

        Assert.IsFalse(desarchivado.Archivado, "El caso no se desarchivo.");
        Assert.IsNull(
            desarchivado.FechaArchivado,
            "Desarchivar dejo la fecha puesta, y el esquema las ata: o las dos o ninguna.");
        Assert.AreEqual(
            1,
            casos.Contar(new FiltroDeCasos()),
            "El caso desarchivado no volvio a la lista de trabajo.");
    }
}
