using Fichas.App.Vocabulario;
using Fichas.App.Asignar;
using Fichas.App.Cascara;
using Fichas.App.Inicio;
using Fichas.App.Revisar;
using Fichas.Contratos.Consultas;
using Fichas.Datos.Falso;
using Fichas.Reportes.Armado;
using Fichas.Reportes.Reglas;

namespace Fichas.Pruebas.App.Reportes;

/// <summary>
/// La frase que el histórico imprime sobre los archivados, comprobada contra la aplicación.
/// </summary>
/// <remarks>
/// <para><b>Por qué existe este archivo, y es una lección cara.</b> La frase
/// <c>ArmadoDelHistorico.QueLePasaAUnArchivado</c> se IMPRIME en el PDF que leen los jefes.
/// La vigilaba <c>Fichas.Pruebas.Reportes/PruebaDeLaFraseDelArchivado.cs</c>, que no puede
/// referenciar <c>Fichas.App</c> y por eso escribía a mano el filtro que creía que usaban las
/// pantallas. Su propio comentario lo confesaba.</para>
///
/// <para>El 2026-09-05 el dueño decidió que un archivado <i>«debe salir del sistema visible,
/// pero se queda como histórico para los reportes»</i>, otros programadores lo aplicaron en
/// Inicio, Revisar y Asignar, y <b>aquella prueba siguió en verde</b> mientras el PDF de los
/// jefes decía lo contrario de lo que el programa hacía. Es el mismo fallo que ya costó dos
/// huecos en este proyecto: una prueba que pasa contra un doble que no se comporta como el
/// original.</para>
///
/// <para><b>Lo que hace esta:</b> pedir los casos con LOS MISMOS objetos que usan las
/// pantallas —<see cref="ListaParaAsignar.SinFiltroDeEstadoNiArchivados"/>,
/// <see cref="TableroDeRevisar"/> y <see cref="LectorDelInicio"/>— y comprobar que lo que
/// devuelven concuerda, sitio por sitio, con lo que la frase promete. Si mañana alguien vuelve
/// a cambiar un filtro, esta prueba se pone roja y la frase se corrige en el mismo commit.</para>
///
/// <para>⚠️ <b>Lo que NO cubre.</b> El selector de Corrección pide con
/// <c>FiltroDeCasos.Todo</c> desde el propio code-behind de su página
/// (<c>PaginaDeCorreccion.xaml.cs:99</c>), sin una clase intermedia a la que llamar sin abrir
/// ventana. Aquí se comprueba ese filtro, que es la constante que la página usa, y no la
/// página: si alguien cambia la línea de la página por otro filtro, esta prueba no lo ve.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaFraseDelArchivadoContraLaApp
{
    private const string Hoy = "2026-09-20";

    /// <summary>Asignar no ofrece un archivado, que es lo que dice la frase.</summary>
    /// <remarks>
    /// Se pide con la constante de la pantalla, no con una copia: es la diferencia entre
    /// vigilar la aplicación y vigilar lo que la prueba cree de ella.
    /// </remarks>
    [TestMethod]
    public void AsignarNoOfreceNingunArchivado()
    {
        var mundo = Escenario();

        var ofrecidos = mundo.Casos
            .Listar(ListaParaAsignar.SinFiltroDeEstadoNiArchivados, Pagina.Primera(int.MaxValue))
            .Elementos;

        Assert.HasCount(0, ofrecidos.Where(caso => caso.Archivado).ToList());
        Assert.HasCount(
            mundo.SinArchivar,
            ofrecidos,
            $"En la base hay {mundo.EnLaBase} casos y {mundo.EnLaBase - mundo.SinArchivar} archivados; "
            + $"Asignar tiene que ofrecer {mundo.SinArchivar} y ofrece {ofrecidos.Count}.");
    }

    /// <summary>Revisar tampoco, mientras la casilla «Ver los archivados» esté sin marcar.</summary>
    [TestMethod]
    public void RevisarNoEnsenaUnArchivadoConLaCasillaSinMarcar()
    {
        var mundo = Escenario();
        var tablero = Tablero(mundo);

        tablero.Cargar(conArchivados: false);

        Assert.HasCount(0, tablero.Cargadas.Where(tarjeta => tarjeta.Archivado).ToList());
    }

    /// <summary>Y con la casilla marcada sí, que es desde donde se desarchiva.</summary>
    /// <remarks>
    /// Las dos mitades en dos pruebas: la frase promete las dos cosas, y una sola las dejaría
    /// pasar juntas si el filtro dejara de mirar la casilla.
    /// </remarks>
    [TestMethod]
    public void RevisarSiLosEnsenaConLaCasillaMarcada()
    {
        var mundo = Escenario();
        var tablero = Tablero(mundo);

        tablero.Cargar(conArchivados: true);

        Assert.IsNotEmpty(tablero.Cargadas.Where(tarjeta => tarjeta.Archivado).ToList());
    }

    /// <summary>El Inicio no lo cuenta en las cifras NI lo enseña en el calendario.</summary>
    /// <remarks>
    /// <para>Es la frase entera en una sola pasada: las dos son promesas sobre la MISMA
    /// pantalla, y separarlas es justo lo que permitió que el informe se contradijera a sí
    /// mismo. Antes esta prueba se llamaba
    /// <c>ElInicioNoLoCuentaEnLasCifrasYSiLoEnsenaEnElCalendario</c> y exigía que el
    /// calendario SÍ los contara.</para>
    ///
    /// <para>⛔ <b>HALLAZGO ABIERTO, y es exactamente el fallo que este archivo existe para
    /// cazar.</b> La constante <c>ArmadoDelHistorico.QueLePasaAUnArchivado</c>, que se IMPRIME
    /// en el PDF de los jefes, sigue diciendo <i>«Sigue viéndose, marcado como archivado, en
    /// el calendario del Inicio»</i>. Desde el 2026-09-06 eso es FALSO. No se corrige aquí
    /// porque <c>Fichas.Reportes</c> queda fuera del terreno de este pase; queda anotado en
    /// <c>PENDIENTES.md</c> con su archivo y su línea. La comprobación de esa frase contra la
    /// aplicación es la del final de este archivo.</para>
    /// </remarks>
    [TestMethod]
    public void ElInicioNoLoCuentaEnLasCifrasNiLoEnsenaEnElCalendario()
    {
        var mundo = Escenario();
        var lector = new LectorDelInicio(
            mundo.Servicios.Casos, mundo.Servicios.Personas, mundo.Servicios.Companeros,
            mundo.Servicios.Asignaciones, mundo.Servicios.Reloj, mundo.Servicios.Procedencia);

        var resumen = lector.Leer();

        Assert.AreEqual(
            mundo.SinArchivar,
            resumen.Denominadores.CasosNoArchivados,
            "Las cifras del Inicio se calculan sobre los NO archivados.");
        Assert.AreEqual(mundo.EnLaBase, resumen.Denominadores.CasosEnLaBase);

        var enLasListas = resumen.Listos.Concat(resumen.Asignados).Select(r => r.CasoId).ToHashSet();
        Assert.HasCount(
            0,
            mundo.Archivados.Where(enLasListas.Contains).ToList(),
            "Ni un archivado puede salir en «listo para asignar» ni en «asignado».");

        // ⛔ 2026-09-07: EL CALENDARIO SÍ LOS ENSEÑA, y es lo único que cambió de esta prueba.
        // Del dueño: «debe salir de todos lados EXCEPTO del calendario. Aunque se archive, debe
        // quedarse en el calendario marcado en verde». Lo que sigue vigilando esta prueba —que
        // un archivado no salga en «listo para asignar» ni en «asignado»— está arriba y no se
        // toca. Aquí se mide la otra mitad: que se quede, y que no traiga la palabra.
        var antes = DocumentosEnElCalendario(resumen);
        Assert.IsGreaterThan(0, antes, "Si el calendario no pintara nada, esto no probaría nada.");

        var unoQueSeVe = UnCasoDelCalendario(mundo, resumen);
        new AccionesDeRevisar(mundo.Casos, mundo.Servicios.Reloj, new BuzonDeAvisos())
            .ArchivarEnLote([unoQueSeVe]);

        var despuesDeArchivar = lector.Leer();
        var despues = DocumentosEnElCalendario(despuesDeArchivar);

        Assert.AreEqual(
            antes,
            despues,
            "Archivar no lo saca del calendario: se queda, en verde (2026-09-07).");
        Assert.IsEmpty(
            despuesDeArchivar.Mes.Dias
                .SelectMany(dia => dia.Pastillas)
                .Where(p => p.Etiqueta.Contains("archivad", StringComparison.OrdinalIgnoreCase))
                .ToList(),
            "Pero la palabra «archivado» no vuelve a la etiqueta: era lo que le confundía.");
    }

    /// <summary>Cuantos documentos pinta el calendario del mes, sumando sus pastillas.</summary>
    private static int DocumentosEnElCalendario(ResumenDeInicio resumen)
        => resumen.Mes.Dias.SelectMany(dia => dia.Pastillas).Sum(pastilla => pastilla.CuantosDocumentos);

    /// <summary>Un caso abierto cuya fecha de viaje cae en el mes que el calendario ensena.</summary>
    private static long UnCasoDelCalendario(Mundo mundo, ResumenDeInicio resumen)
    {
        var delMes = resumen.Mes.Dias.Select(dia => dia.Fecha).ToHashSet();
        return mundo.Casos
            .Listar(FiltroDeCasos.Todo, Pagina.Primera(int.MaxValue))
            .Elementos
            .First(caso => FechasEnEspanol.Leer(caso.FechaViaje) is DateOnly fecha && delMes.Contains(fecha))
            .Id;
    }

    /// <summary>El selector de Corrección pide con un filtro que deja fuera los archivados.</summary>
    /// <remarks>
    /// ⚠️ Se comprueba la CONSTANTE que esa página usa (<c>FiltroDeCasos.Todo</c>), no la
    /// página: no hay clase intermedia a la que llamar sin abrir ventana. Va dicho aquí y en la
    /// cabecera para que nadie lea esto como más de lo que es.
    /// </remarks>
    [TestMethod]
    public void ElFiltroDelSelectorDeCorreccionDejaFueraLosArchivados()
    {
        var mundo = Escenario();

        var ofrecidos = mundo.Casos.Listar(FiltroDeCasos.Todo, Pagina.Primera(int.MaxValue)).Elementos;

        Assert.HasCount(0, ofrecidos.Where(caso => caso.Archivado).ToList());
    }

    /// <summary>La frase nombra los cinco sitios, y ninguno de los que ya no valen.</summary>
    /// <remarks>
    /// Es la única parte que mira las palabras y no el comportamiento, y hace falta: las cuatro
    /// pruebas de arriba pueden estar todas en verde con la frase escrita al revés.
    /// </remarks>
    [TestMethod]
    public void LaFraseNombraLoQueDeVerdadPasaYNoLoDeAntes()
    {
        var frase = ArmadoDelHistorico.QueLePasaAUnArchivado;

        StringAssert.Contains(
            frase, "deja de salir en el selector de Corrección, en Asignar, en Revisar, en el calendario");
        StringAssert.Contains(frase, "Ver los archivados");
        StringAssert.Contains(frase, "sigue contando");

        Assert.IsFalse(
            frase.Contains("en Revisar, en Asignar y en el calendario", StringComparison.Ordinal),
            "La frase volvió a decir que un archivado sigue viéndose en Revisar y en Asignar. Dejó "
            + "de ser cierto el 2026-09-05, y esta frase se imprime en el PDF que leen los jefes.");

        // ⚠️ 2026-09-06. Esta prueba PEDÍA «en el calendario del Inicio» porque hasta ese día la
        // frase decía que un archivado se seguía viendo ahí, marcado. El dueño lo deshizo con su
        // motivo —«si se queda en el tablero y dice archivado, lo que hace es que me confunda»— y
        // el calendario pasó a la lista de sitios de los que SALE. La prueba se mueve para vigilar
        // eso mismo al revés: lo que no puede volver es la promesa de que sigue viéndose.
        Assert.IsFalse(
            frase.Contains("Sigue viéndose, marcado como archivado", StringComparison.Ordinal),
            "La frase volvió a prometer que un archivado se sigue viendo marcado. Dejó de ser "
            + "cierto el 2026-09-06 y esta frase se imprime en el PDF que leen los jefes.");
    }

    // ---- el escenario -------------------------------------------------------

    private sealed record Mundo(
        ServiciosFalsos Servicios, int EnLaBase, int SinArchivar, IReadOnlyList<long> Archivados)
    {
        internal Contratos.Puertos.ICasos Casos => Servicios.Casos;
    }

    /// <summary>El tablero de Revisar tal como lo monta su pantalla.</summary>
    private static TableroDeRevisar Tablero(Mundo mundo)
        => new(
            mundo.Servicios.Casos, mundo.Servicios.Asignaciones,
            mundo.Servicios.Companeros, mundo.Servicios.Reloj);

    /// <summary>Una base inventada con unos cuantos casos archivados de verdad.</summary>
    private static Mundo Escenario()
    {
        var servicios = new ServiciosFalsos(60, 29, new RelojFijo(Hoy));

        // Se archiva uno de cada siete por la puerta de verdad, no tocando el almacen: asi el
        // caso queda archivado como lo dejaria Miguel, con su fecha.
        var todos = servicios.Casos
            .Listar(FiltroDeCasos.Todo with { IncluirArchivados = true }, Pagina.Primera(int.MaxValue))
            .Elementos;

        var orden = 0;
        foreach (var caso in todos)
        {
            if (orden++ % 7 != 0) continue;
            servicios.Casos.Archivar(caso.Id, true, Hoy);
        }

        var enLaBase = servicios.Casos.Contar(FiltroDeCasos.Todo with { IncluirArchivados = true });
        var sinArchivar = servicios.Casos.Contar(FiltroDeCasos.Todo);
        var archivados = servicios.Casos
            .Listar(FiltroDeCasos.Todo with { IncluirArchivados = true }, Pagina.Primera(int.MaxValue))
            .Elementos
            .Where(caso => caso.Archivado)
            .Select(caso => caso.Id)
            .ToList();

        Assert.IsGreaterThan(sinArchivar, enLaBase, "El escenario tenía que dejar casos archivados.");
        return new Mundo(servicios, enLaBase, sinArchivar, archivados);
    }
}
